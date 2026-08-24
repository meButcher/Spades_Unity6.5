using System;
using Spades.Core.Events;
using Spades.Core.Flow;
using Spades.Core.Players;
using Spades.Core.Rules;
using Spades.Core.State;
using Spades.Core.Util;
using Spades.Unity.Presentation;
using Spades.Unity.UI;
using Spades.Unity.Views;
using Spades.Unity.Visuals;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Spades.Unity.Bootstrap
{
    /// <summary>
    /// The composition root: the one place in the project where concrete types are constructed
    /// and wired together.
    ///
    /// No singletons, no FindObjectOfType, no dependency-injection framework. The object graph is
    /// about a dozen lines, and a framework would hide it rather than explain it. What the
    /// explicit graph buys is that GameLoop can be built in a unit test with no scene at all,
    /// and that swapping the human seat for a bot -- or later for a networked controller -- is a
    /// one-line change here and nowhere else.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class GameBootstrap : MonoBehaviour
    {
        [Header("Optional authored data (defaults are used when empty)")]
        [SerializeField] private LayoutSettings _layoutSettings;
        [SerializeField] private GameRulesAsset _fourPlayerRules;
        [SerializeField] private GameRulesAsset _twoPlayerRules;

        [Header("Session")]
        [Tooltip("Zero picks a fresh seed each game. Any other value replays the identical game.")]
        [SerializeField] private int _seed;

        [Tooltip("Skips the menu and deals immediately. Handy while iterating.")]
        [SerializeField] private bool _startImmediately;

        [SerializeField] private int _immediatePlayerCount = 4;

        [Range(0.25f, 4f)]
        [SerializeField] private float _animationSpeed = 1f;

        // Persistent shell.
        private LayoutSettings _layout;
        private CardArt _art;
        private TweenRunner _tweens;
        private GamePresenter _presenter;
        private RectTransform _tableRoot;
        private RectTransform _hudRoot;
        private RectTransform _gamePanelRoot;
        private MainMenuPanel _mainMenu;

        // Rebuilt for each game.
        private CardViewPool _pool;
        private TableView _table;
        private ScoreboardView _scoreboard;
        private BidPanel _bidPanel;
        private DrawPanel _drawPanel;
        private HandSummaryPanel _summaryPanel;
        private GameOverPanel _gameOverPanel;
        private MessageBanner _banner;
        private int _lastPlayerCount = 4;

        private bool _ownsLayout;

        private void Awake()
        {
            _ownsLayout = _layoutSettings == null;
            _layout = _ownsLayout ? LayoutSettings.CreateDefault() : _layoutSettings;

            RectTransform canvas = BuildCanvas();
            EnsureEventSystem();

            _tweens = gameObject.AddComponent<TweenRunner>();
            _tweens.TimeScale = _animationSpeed;

            _art = new CardArt();

            _tableRoot = UiFactory.Stretch("Table", canvas);
            _hudRoot = UiFactory.Stretch("Hud", canvas);
            _gamePanelRoot = UiFactory.Stretch("Panels", canvas);
            RectTransform menuRoot = UiFactory.Stretch("Menu", canvas);

            _presenter = gameObject.AddComponent<GamePresenter>();

            _mainMenu = new MainMenuPanel(menuRoot, _layout, _tweens, _art);
            _mainMenu.StartRequested += StartGame;
            _mainMenu.QuitRequested += Quit;
        }

        /// <summary>Lets the animation speed slider be dragged live while play-testing.</summary>
        private void OnValidate()
        {
            if (_tweens != null) _tweens.TimeScale = _animationSpeed;
        }

        private void Start()
        {
            if (_startImmediately) StartGame(_immediatePlayerCount);
            else _mainMenu.Show();
        }

        private void OnDestroy()
        {
            if (_mainMenu != null)
            {
                _mainMenu.StartRequested -= StartGame;
                _mainMenu.QuitRequested -= Quit;
            }

            if (_presenter != null) _presenter.Unbind();
            if (_art != null) _art.Dispose();

            // Only the instance this component created; an authored asset must not be destroyed.
            if (_ownsLayout && _layout != null) Destroy(_layout);
        }

        /// <summary>
        /// Builds the entire object graph for one game. Read top to bottom, this is the whole
        /// dependency story of the project.
        /// </summary>
        public void StartGame(int playerCount)
        {
            TearDownGame();

            _lastPlayerCount = playerCount;
            _mainMenu.Hide();

            GameRules rules = ResolveRules(playerCount);
            int seed = _seed != 0 ? _seed : Environment.TickCount & 0x7FFFFFFF;

            var human = new HumanPlayerController();
            var humanSeat = new Seat(0);

            var controllers = new IPlayerController[rules.PlayerCount];
            controllers[0] = human;
            for (int i = 1; i < controllers.Length; i++) controllers[i] = AiPlayerController.CreateDefault();

            var state = new GameState(rules, controllers);
            var events = new GameEventQueue();
            var loop = new GameLoop(state, new SeededRandomSource(seed), events);

            BuildGameViews(rules, humanSeat);

            var naming = new SeatNaming(rules.PlayerCount, humanSeat.Index, rules.TeamIdForSeat(humanSeat));

            for (int seatIndex = 0; seatIndex < rules.PlayerCount; seatIndex++)
            {
                _table.SetSeatName(seatIndex, naming.SeatName(seatIndex));
                _table.SetSeatDetail(seatIndex, -1, 0);
            }

            _scoreboard.SetTarget(rules.TargetScore);
            for (int team = 0; team < rules.TeamCount; team++)
            {
                _scoreboard.SetTeamName(team, naming.TeamName(team));
                _scoreboard.SetScore(team, 0, 0);
            }

            _gameOverPanel.PlayAgain += OnPlayAgain;
            _gameOverPanel.MainMenu += OnReturnToMenu;

            _presenter.Bind(
                loop, events, _table, _scoreboard, _bidPanel, _drawPanel, _summaryPanel,
                _gameOverPanel, _banner, _tweens, _layout, naming, human, humanSeat);

            _presenter.StartGame();

            Debug.Log("[Spades] " + rules.PlayerCount + "-player game started with seed " + seed + ".");
        }

        private GameRules ResolveRules(int playerCount)
        {
            GameRulesAsset asset = playerCount == 2 ? _twoPlayerRules : _fourPlayerRules;

            if (asset != null && asset.PlayerCount == playerCount) return asset.ToGameRules();

            return playerCount == 2 ? GameRules.Standard2Player() : GameRules.Standard4Player();
        }

        private void BuildGameViews(GameRules rules, Seat humanSeat)
        {
            // The parking node for idle cards is created first, so it sits behind the felt. Cards
            // are reparented into a hand or the trick when rented, which is what actually decides
            // their draw order.
            RectTransform poolRoot = UiFactory.Stretch("CardPool", _tableRoot);
            _pool = new CardViewPool(poolRoot, _layout, _art, _tweens);

            _table = new TableView(_tableRoot, rules.PlayerCount, humanSeat.Index, _layout, _tweens, _art, _pool);

            _scoreboard = new ScoreboardView(_hudRoot, _layout, _tweens, _art, rules.TeamCount);
            _banner = new MessageBanner(_hudRoot, _layout, _tweens, _art);

            _bidPanel = new BidPanel(_gamePanelRoot, _layout, _tweens, _art);
            _drawPanel = new DrawPanel(_gamePanelRoot, _layout, _tweens, _art);
            _summaryPanel = new HandSummaryPanel(_gamePanelRoot, _layout, _tweens, _art, rules.TeamCount);
            _gameOverPanel = new GameOverPanel(_gamePanelRoot, _layout, _tweens, _art);
        }

        private void TearDownGame()
        {
            if (_gameOverPanel != null)
            {
                _gameOverPanel.PlayAgain -= OnPlayAgain;
                _gameOverPanel.MainMenu -= OnReturnToMenu;
            }

            _presenter.Unbind();
            _tweens.KillAll();

            DestroyChildren(_tableRoot);
            DestroyChildren(_hudRoot);
            DestroyChildren(_gamePanelRoot);

            _pool = null;
            _table = null;
            _scoreboard = null;
            _banner = null;
            _bidPanel = null;
            _drawPanel = null;
            _summaryPanel = null;
            _gameOverPanel = null;
        }

        private void OnPlayAgain() => StartGame(_lastPlayerCount);

        private void OnReturnToMenu()
        {
            TearDownGame();
            _mainMenu.Show();
        }

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // -- scene shell ----------------------------------------------------------------------

        private RectTransform BuildCanvas()
        {
            var go = new GameObject("Spades Canvas", typeof(RectTransform));
            go.transform.SetParent(transform, false);

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            // Matching height keeps the fanned hand and the seat plates in the same relationship
            // on a wide monitor as on a 16:9 one; matching width would crop the table vertically.
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;

            go.AddComponent<GraphicRaycaster>();

            return (RectTransform)go.transform;
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;

            var go = new GameObject("EventSystem", typeof(EventSystem));

            // The project is configured for the Input System package only, so the legacy
            // StandaloneInputModule would throw on the first frame.
            go.AddComponent<InputSystemUIInputModule>();
        }

        private static void DestroyChildren(RectTransform root)
        {
            if (root == null) return;

            for (int i = root.childCount - 1; i >= 0; i--)
            {
                GameObject child = root.GetChild(i).gameObject;

                // Deactivated first because Destroy is deferred to the end of the frame, and a
                // torn-down table should not render for one more frame behind the new one.
                child.SetActive(false);
                Destroy(child);
            }
        }
    }
}
