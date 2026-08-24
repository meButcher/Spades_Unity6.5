using Spades.Unity.Bootstrap;
using Spades.Unity.Visuals;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Spades.Unity.Views
{
    /// <summary>
    /// The table: felt, seat plates, one hand per seat, and the trick in the middle.
    /// Built entirely from code so there is nothing in the scene to lose a reference to.
    /// </summary>
    public sealed class TableView
    {
        private sealed class SeatPlate
        {
            public Image Background;
            public TextMeshProUGUI Name;
            public TextMeshProUGUI Detail;
        }

        private readonly SeatPlate[] _plates;
        private readonly LayoutSettings _layout;

        public TableView(RectTransform parent, int playerCount, int humanSeat,
                         LayoutSettings layout, TweenRunner tweens, CardArt art, CardViewPool pool)
        {
            _layout = layout;
            PlayerCount = playerCount;
            HumanSeat = humanSeat;

            // Felt.
            UiFactory.Panel("TableBackground", parent, art.White, layout.TableEdgeColor, stretch: true);
            Image felt = UiFactory.Panel("Felt", parent, art.Panel, layout.TableColor);
            UiFactory.Place(felt.rectTransform, 0f, 0f, 1500f, 830f);

            Hands = new HandView[playerCount];
            _plates = new SeatPlate[playerCount];

            for (int seat = 0; seat < playerCount; seat++)
            {
                TablePosition position = TablePositions.For(seat, humanSeat, playerCount);
                Hands[seat] = new HandView(parent, position, seat == humanSeat, layout, tweens, pool);
                _plates[seat] = BuildPlate(parent, position, art, layout);
            }

            Trick = new TrickView(parent, layout, tweens, pool);

            // The point every dealt card flies out of.
            DeckAnchor = UiFactory.Root("DeckAnchor", parent);
            UiFactory.At(DeckAnchor, -520f, 190f);
        }

        public int PlayerCount { get; }
        public int HumanSeat { get; }
        public HandView[] Hands { get; }
        public TrickView Trick { get; }
        public RectTransform DeckAnchor { get; }

        public TablePosition PositionOf(int seatIndex) =>
            TablePositions.For(seatIndex, HumanSeat, PlayerCount);

        public HandView HandOf(int seatIndex) => Hands[seatIndex];

        public void SetSeatName(int seat, string name) => _plates[seat].Name.text = name;

        /// <summary>The bid and trick tally under a seat's name. Bid below zero means "not yet".</summary>
        public void SetSeatDetail(int seat, int bid, int tricks)
        {
            string bidText = bid < 0 ? "-" : (bid == 0 ? "NIL" : bid.ToString());
            _plates[seat].Detail.text = "Bid " + bidText + "   Won " + tricks;
        }

        /// <summary>Highlights whoever is on turn, so the player is never guessing who to wait for.</summary>
        public void SetActiveSeat(int seat)
        {
            for (int i = 0; i < _plates.Length; i++)
            {
                bool active = i == seat;
                _plates[i].Background.color = active
                    ? _layout.AccentColor
                    : _layout.PanelColor;
                _plates[i].Name.color = active ? Color.black : _layout.TextColor;
                _plates[i].Detail.color = active ? new Color(0f, 0f, 0f, 0.7f) : _layout.MutedTextColor;
            }
        }

        public void ClearAllHands()
        {
            for (int i = 0; i < Hands.Length; i++) Hands[i].Clear();
            Trick.ReturnAll();
        }

        private static SeatPlate BuildPlate(RectTransform parent, TablePosition position, CardArt art, LayoutSettings layout)
        {
            Image background = UiFactory.Panel("SeatPlate " + position, parent, art.SharpPanel, layout.PanelColor);
            Vector2 anchor = TablePositions.LabelAnchor(position, layout);
            UiFactory.Place(background.rectTransform, anchor.x, anchor.y, 210f, 66f);

            TextMeshProUGUI name = UiFactory.Text("Name", background.rectTransform, "Seat",
                26f, layout.TextColor, TextAlignmentOptions.Center, FontStyles.Bold);
            UiFactory.Place(name.rectTransform, 0f, 13f, 200f, 30f);

            TextMeshProUGUI detail = UiFactory.Text("Detail", background.rectTransform, "Bid -   Won 0",
                20f, layout.MutedTextColor);
            UiFactory.Place(detail.rectTransform, 0f, -15f, 200f, 26f);

            return new SeatPlate { Background = background, Name = name, Detail = detail };
        }
    }
}
