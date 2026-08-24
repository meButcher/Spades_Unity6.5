using System;
using System.Collections;
using Spades.Core.Cards;
using Spades.Core.Events;
using Spades.Core.Players;
using Spades.Core.State;
using Spades.Core.Flow;
using Spades.Unity.Bootstrap;
using Spades.Unity.UI;
using Spades.Unity.Views;
using Spades.Unity.Visuals;
using UnityEngine;

namespace Spades.Unity.Presentation
{
    /// <summary>
    /// The single coroutine that turns the engine's event stream into animation, and the only
    /// place in the project where input is unlocked.
    ///
    /// The loop is: drive the core one step, drain everything it emitted and animate each event
    /// to completion, then -- and only then -- ask whether the core is parked on the human. That
    /// ordering is what makes click-during-animation and double-submit bugs structurally
    /// impossible rather than patched: while an animation is running, the code that could accept
    /// a click has not been reached yet.
    /// </summary>
    public sealed class GamePresenter : MonoBehaviour
    {
        private GameLoop _loop;
        private GameEventQueue _events;
        private TableView _table;
        private ScoreboardView _scoreboard;
        private BidPanel _bidPanel;
        private DrawPanel _drawPanel;
        private HandSummaryPanel _summaryPanel;
        private GameOverPanel _gameOverPanel;
        private MessageBanner _banner;
        private TweenRunner _tweens;
        private LayoutSettings _layout;
        private SeatNaming _naming;
        private HumanPlayerController _human;
        private Seat _humanSeat;

        private Coroutine _running;
        private bool _summaryAcknowledged;

        public bool IsRunning => _running != null;

        public event Action GameFinished;

        public void Bind(
            GameLoop loop,
            GameEventQueue events,
            TableView table,
            ScoreboardView scoreboard,
            BidPanel bidPanel,
            DrawPanel drawPanel,
            HandSummaryPanel summaryPanel,
            GameOverPanel gameOverPanel,
            MessageBanner banner,
            TweenRunner tweens,
            LayoutSettings layout,
            SeatNaming naming,
            HumanPlayerController human,
            Seat humanSeat)
        {
            _loop = loop;
            _events = events;
            _table = table;
            _scoreboard = scoreboard;
            _bidPanel = bidPanel;
            _drawPanel = drawPanel;
            _summaryPanel = summaryPanel;
            _gameOverPanel = gameOverPanel;
            _banner = banner;
            _tweens = tweens;
            _layout = layout;
            _naming = naming;
            _human = human;
            _humanSeat = humanSeat;

            _bidPanel.BidChosen += OnBidChosen;
            _drawPanel.DecisionMade += OnDrawDecision;
            _summaryPanel.Continued += OnSummaryContinued;
        }

        public void Unbind()
        {
            StopGame();

            if (_bidPanel != null) _bidPanel.BidChosen -= OnBidChosen;
            if (_drawPanel != null) _drawPanel.DecisionMade -= OnDrawDecision;
            if (_summaryPanel != null) _summaryPanel.Continued -= OnSummaryContinued;

            _loop = null;
        }

        public void StartGame()
        {
            StopGame();
            _running = StartCoroutine(Run());
        }

        public void StopGame()
        {
            if (_running == null) return;
            StopCoroutine(_running);
            _running = null;
        }

        // -- the loop ---------------------------------------------------------------------------

        private IEnumerator Run()
        {
            while (true)
            {
                _loop.Advance();

                // Animate everything that step produced, one event at a time, to completion.
                while (_events.TryDequeue(out IGameEvent gameEvent))
                {
                    yield return Present(gameEvent);
                }

                if (_loop.IsGameOver) break;

                if (_loop.IsAwaitingInput)
                {
                    if (_loop.AwaitingSeat == _humanSeat)
                    {
                        yield return AwaitHuman();
                    }
                    else
                    {
                        // A remote seat that has not answered yet. Nothing to do but wait; this is
                        // the branch a networked build would sit in.
                        yield return null;
                    }

                    continue;
                }

                // A bot answered instantly. Hold a beat so its move is legible, then apply it on
                // the next Advance. The pause lives here rather than in the bot, which is why a
                // five-hundred-game simulation runs in a second.
                if (_loop.HasPendingCommand && _layout.BotThinkDelay > 0f)
                {
                    yield return Hold(_layout.BotThinkDelay);
                }
            }

            _running = null;
            GameFinished?.Invoke();
        }

        private IEnumerator AwaitHuman()
        {
            if (_human.PendingBid)
            {
                _bidPanel.Configure(_loop.Rules.HandSize, _loop.Rules.AllowNil, BidHint());
                _bidPanel.Show();

                while (_human.PendingBid) yield return null;

                _bidPanel.Hide();
                yield break;
            }

            if (_human.PendingDraw)
            {
                SeatView view = _loop.ViewFor(_humanSeat);
                _drawPanel.SetOffer(_human.OfferedCard, view.Hand.Count, _loop.Rules.HandSize);
                _drawPanel.Show();

                while (_human.PendingDraw) yield return null;

                _drawPanel.Hide();
                yield break;
            }

            if (_human.PendingCard)
            {
                HandView hand = _table.HandOf(_humanSeat.Index);
                hand.ApplyLegalMoves(_human.LegalMoves);
                SubscribeHand(hand, true);

                while (_human.PendingCard) yield return null;

                SubscribeHand(hand, false);
                hand.ClearInteractable();
                yield break;
            }

            // The engine says it is parked on this seat but nothing was requested of it, which
            // should be unreachable. Yielding rather than returning immediately means a logic slip
            // here degrades into a visible stall instead of freezing the editor in a tight loop.
            Debug.LogWarning("[Spades] The loop is awaiting the human seat with no pending request.");
            yield return null;
        }

        // -- event presentation ------------------------------------------------------------------

        private IEnumerator Present(IGameEvent gameEvent)
        {
            switch (gameEvent)
            {
                case HandStarted started:
                    yield return OnHandStarted(started);
                    break;

                case HandDealt dealt:
                    yield return OnHandDealt(dealt);
                    break;

                case DrawPhaseStarted _:
                    _banner.Flash("Draw phase");
                    yield return Hold(0.35f);
                    break;

                case CardOffered offered:
                    yield return OnCardOffered(offered);
                    break;

                case CardDrawn drawn:
                    yield return OnCardDrawn(drawn);
                    break;

                case BiddingStarted started:
                    _table.SetActiveSeat(started.First.Index);
                    _banner.Flash("Bidding");
                    yield return Hold(0.25f);
                    break;

                case BidPlaced bid:
                    yield return OnBidPlaced(bid);
                    break;

                case BiddingComplete _:
                    _bidPanel.Hide();
                    break;

                case TrickStarted started:
                    _table.SetActiveSeat(started.Leader.Index);
                    break;

                case TurnChanged changed:
                    _table.SetActiveSeat(changed.Seat.Index);
                    break;

                case CardPlayed played:
                    yield return OnCardPlayed(played);
                    break;

                case TrickWon won:
                    yield return OnTrickWon(won);
                    break;

                case HandScored scored:
                    yield return OnHandScored(scored);
                    break;

                case GameEnded ended:
                    yield return OnGameEnded(ended);
                    break;
            }
        }

        private IEnumerator OnHandStarted(HandStarted started)
        {
            _table.ClearAllHands();
            _banner.Clear();

            for (int seat = 0; seat < _loop.Rules.PlayerCount; seat++)
            {
                _table.SetSeatDetail(seat, -1, 0);
            }

            _table.SetActiveSeat(-1);
            _banner.Flash("Hand " + started.HandNumber + "  ·  " + _naming.SeatName(started.Dealer.Index) + " deals", 0.6f);
            yield return Hold(0.3f);
        }

        private IEnumerator OnHandDealt(HandDealt dealt)
        {
            HandView hand = _table.HandOf(dealt.Seat.Index);
            bool wasEmpty = hand.Count == 0;

            if (dealt.Seat == _humanSeat)
            {
                hand.SetFaceUpHand(dealt.Cards);
            }
            else
            {
                // Note that the event carried the cards and the view deliberately ignores them
                // for every seat but the player's. In a networked build the server would filter
                // this event per client and the view would look exactly the same.
                hand.SetHiddenHand(dealt.Cards.Count);
            }

            if (wasEmpty)
            {
                hand.StackAt(hand.WorldToLocal(_table.DeckAnchor.position));
                hand.LayoutCards(true, _layout.DealDuration, _layout.DealStagger);
                yield return _tweens.WaitAll();
            }
            else
            {
                // The two-player draw phase already put these cards on the table; this is just the
                // sorted re-layout, so it should settle rather than deal again.
                hand.LayoutCards(true, 0.22f);
                yield return _tweens.WaitAll();
            }
        }

        private IEnumerator OnCardOffered(CardOffered offered)
        {
            _table.SetActiveSeat(offered.Seat.Index);

            if (offered.Seat != _humanSeat)
            {
                // The opponent's offered card is not shown. The panel is the player's own view.
                yield break;
            }

            yield return null;
        }

        private IEnumerator OnCardDrawn(CardDrawn drawn)
        {
            HandView hand = _table.HandOf(drawn.Seat.Index);
            bool isHuman = drawn.Seat == _humanSeat;

            // Rebuild the hand at its new size. Rebuilding rather than appending keeps the human's
            // hand in the engine's sorted order after every single draw, which is what a player
            // expects while they are assembling it.
            if (isHuman)
            {
                SeatView seatView = _loop.ViewFor(_humanSeat);
                hand.SetFaceUpHand(seatView.Hand);
            }
            else
            {
                hand.SetHiddenHand(hand.Count + 1);
            }

            hand.StackAt(hand.WorldToLocal(_table.DeckAnchor.position));
            hand.LayoutCards(true, _layout.DrawRevealDuration, 0.01f);

            if (!drawn.Kept && isHuman)
            {
                _banner.Flash("Discarded " + drawn.Discarded + ", drew blind", 0.5f);
            }

            yield return _tweens.WaitAll();
        }

        private IEnumerator OnBidPlaced(BidPlaced bid)
        {
            // Always projected for the player's own seat. Every field read here is public
            // information, and going through the human's projection keeps it impossible for the
            // view to reach another seat's cards even by accident.
            SeatView view = _loop.ViewFor(_humanSeat);
            _table.SetSeatDetail(bid.Seat.Index, bid.Bid, view.TricksWon[bid.Seat.Index]);

            if (bid.IsNil)
            {
                _banner.Flash(_naming.SeatName(bid.Seat.Index) + " " +
                              (bid.Seat == _humanSeat ? "bid" : "bids") + " NIL", 0.7f);
            }

            yield return Hold(_layout.BidRevealDuration);
        }

        private IEnumerator OnCardPlayed(CardPlayed played)
        {
            HandView hand = _table.HandOf(played.Seat.Index);
            CardView view = hand.Detach(played.Card);

            if (view == null) yield break;

            _table.Trick.Play(view, _table.PositionOf(played.Seat.Index));
            hand.LayoutCards(true, 0.18f);

            yield return _tweens.WaitAll();

            if (played.BrokeSpades) _banner.Flash("Spades broken", 0.7f);
        }

        private IEnumerator OnTrickWon(TrickWon won)
        {
            yield return Hold(_layout.TrickHoldDuration);

            SeatView view = _loop.ViewFor(_humanSeat);
            _table.SetSeatDetail(won.Winner.Index, view.Bids[won.Winner.Index], view.TricksWon[won.Winner.Index]);

            _table.Trick.CollectTo(_table.HandOf(won.Winner.Index).WorldAnchor);
            yield return _tweens.WaitAll();

            _table.Trick.ReturnAll();
        }

        private IEnumerator OnHandScored(HandScored scored)
        {
            _table.SetActiveSeat(-1);

            _summaryPanel.SetSummary(scored.HandNumber, scored.Lines, _naming.TeamName);
            _summaryPanel.Show();

            for (int i = 0; i < scored.Lines.Count; i++)
            {
                var line = scored.Lines[i];
                _scoreboard.AnimateScore(line.TeamId, line.TotalScore, line.BagsAfter, _layout.ScoreCountDuration);
            }

            _summaryAcknowledged = false;
            while (!_summaryAcknowledged) yield return null;

            _summaryPanel.Hide();
        }

        private IEnumerator OnGameEnded(GameEnded ended)
        {
            _gameOverPanel.SetResult(_naming.IsHumanTeam(ended.WinningTeamId), ended.FinalScores, _naming.TeamName);
            _gameOverPanel.Show();
            yield return null;
        }

        // -- input ---------------------------------------------------------------------------------

        private void SubscribeHand(HandView hand, bool subscribe)
        {
            for (int i = 0; i < hand.Cards.Count; i++)
            {
                CardView card = hand.Cards[i];
                card.Clicked -= OnCardClicked;
                if (subscribe) card.Clicked += OnCardClicked;
            }
        }

        private void OnCardClicked(CardView card)
        {
            if (!_human.PendingCard) return;

            // The refusal and the greying-out both come from the engine's legal-move list, so the
            // card that shakes is exactly the card that looked unplayable.
            if (!_human.SubmitCard(card.Card))
            {
                card.Shake();
                _banner.Flash(IllegalReason(), 0.8f);
            }
        }

        private void OnBidChosen(int bid)
        {
            if (!_human.PendingBid) return;
            _human.SubmitBid(bid);
        }

        private void OnDrawDecision(bool keep)
        {
            if (!_human.PendingDraw) return;
            _human.SubmitDrawDecision(keep);
        }

        private void OnSummaryContinued()
        {
            _summaryAcknowledged = true;
        }

        // -- helpers ---------------------------------------------------------------------------------

        private string IllegalReason()
        {
            SeatView view = _loop.ViewFor(_humanSeat);
            TrickState trick = view.CurrentTrick;

            if (trick == null || trick.LedSuit == null)
                return "Spades are not broken yet";

            return "You must follow " + trick.LedSuit.Value;
        }

        private string BidHint()
        {
            SeatView view = _loop.ViewFor(_humanSeat);

            if (_loop.Rules.PlayerCount == 2)
                return "Zero is a Nil: a hundred points if you take no tricks, minus a hundred if you take one.";

            int partnerBid = -1;
            for (int i = 0; i < view.Bids.Count; i++)
            {
                if (i == _humanSeat.Index) continue;
                if (_loop.Rules.TeamIdForSeat(new Seat(i)) != view.TeamId) continue;
                partnerBid = view.Bids[i];
            }

            return partnerBid < 0
                ? "Your partner has not bid yet. Zero is a Nil."
                : "Your partner bid " + (partnerBid == 0 ? "NIL" : partnerBid.ToString()) + ". Zero is a Nil.";
        }

        private IEnumerator Hold(float seconds)
        {
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime * _tweens.TimeScale;
                yield return null;
            }
        }
    }
}
