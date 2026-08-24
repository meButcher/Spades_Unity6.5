using System;
using System.Collections.Generic;
using Spades.Core.Cards;
using Spades.Core.Commands;
using Spades.Core.Events;
using Spades.Core.Rules;
using Spades.Core.State;
using Spades.Core.Util;

namespace Spades.Core.Flow
{
    /// <summary>
    /// The state machine. It never blocks, never yields, and contains the word Time zero times.
    ///
    /// The animation-timing problem this design exists to solve: the core resolves a trick in
    /// microseconds, but the player needs most of a second of cards flying to understand what
    /// happened. Putting that wait inside the rules makes them untestable and couples them to
    /// the frame rate. Letting the core run ahead of the view creates two disagreeing versions
    /// of "what is happening" and every input has to be checked against a state the player
    /// cannot see. So instead the core is a step machine that only moves when driven, and the
    /// view drives it: one Advance per animation, and input is unlocked at exactly one place.
    ///
    /// The re-entrancy rule that makes it work: an AI controller calls submit synchronously,
    /// inside Advance. If submit executed the command, a single Advance would recurse through
    /// an entire hand before the view drew a frame. So submit never executes. It parks the
    /// command in a single-slot mailbox and the NEXT Advance consumes it. One decision per call,
    /// same code path for a human and a bot.
    /// </summary>
    public sealed class GameLoop
    {
        private static readonly Comparison<Card> HandOrder = CompareForDisplay;

        private readonly GameState _state;
        private readonly IRandomSource _rng;
        private readonly GameEventQueue _events;

        // Allocated once. These capture only "this", so a decision costs no delegate allocation
        // even though there are roughly sixty of them per hand.
        private readonly Action<int> _submitBid;
        private readonly Action<Card> _submitCard;
        private readonly Action<bool> _submitDraw;

        private readonly List<Card> _legalBuffer = new List<Card>(13);
        private readonly List<TeamScoreLine> _scoreLines = new List<TeamScoreLine>(2);

        /// <summary>The single-slot mailbox. At most one command is ever waiting.</summary>
        private IGameCommand _pending;

        private bool _decisionRequested;
        private Seat _awaitingSeat;

        private bool _drawOfferActive;
        private Card _offeredCard;
        private int _trickNumber;

        private int _cardsPlayedThisAdvance;

        public GameLoop(GameState state, IRandomSource rng, GameEventQueue events)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
            _events = events ?? throw new ArgumentNullException(nameof(events));

            _submitBid = bid => SubmitFromController(new PlaceBidCommand(_state.CurrentTurn, bid));
            _submitCard = card => SubmitFromController(new PlayCardCommand(_state.CurrentTurn, card));
            _submitDraw = keep => SubmitFromController(new DrawDecisionCommand(_state.CurrentTurn, keep));

            Phase = GamePhase.Dealing;
        }

        public GamePhase Phase { get; private set; }

        public GameRules Rules => _state.Rules;

        /// <summary>True when the machine is parked waiting for a command that has not arrived.</summary>
        public bool IsAwaitingInput => _decisionRequested && _pending == null;

        /// <summary>
        /// True when an answer is sitting in the mailbox waiting for the next Advance. A bot fills
        /// this synchronously, which is how the view knows to hold a beat before showing the move
        /// rather than having the bot itself sleep.
        /// </summary>
        public bool HasPendingCommand => _pending != null;

        public Seat AwaitingSeat => _awaitingSeat;

        public bool IsGameOver => Phase == GamePhase.GameOver;

        public int TrickNumber => _trickNumber;

        /// <summary>The only way a caller outside the engine reads state.</summary>
        public SeatView ViewFor(Seat seat) => _state.ProjectFor(seat);

        public int ScoreForTeam(int teamId) => _state.TeamAt(teamId).Score;
        public int BagsForTeam(int teamId) => _state.TeamAt(teamId).Bags;
        public Seat Dealer => _state.Dealer;
        public int HandNumber => _state.HandNumber;

        /// <summary>
        /// Do the next unit of work: process a parked command, or ask the current seat for a
        /// decision, or run a phase transition. Emits events and returns. Never recurses.
        /// </summary>
        public void Advance()
        {
            _cardsPlayedThisAdvance = 0;

            if (Phase == GamePhase.GameOver) return;

            if (_pending != null)
            {
                IGameCommand command = _pending;
                _pending = null;
                _decisionRequested = false;
                ApplyCommand(command);

                // The guard against the recursion this whole design exists to prevent. If a
                // refactor ever lets submit execute, this fires immediately instead of the game
                // silently finishing a hand inside a single frame.
                if (_cardsPlayedThisAdvance > 1)
                    throw new InvalidOperationException("Advance() played more than one card. Check that submit enqueues rather than executes.");

                return;
            }

            // Parked on a seat that has not answered yet. That is a human, and the presenter is
            // waiting on a click.
            if (_decisionRequested) return;

            switch (Phase)
            {
                case GamePhase.Dealing:
                    BeginHand();
                    return;
                case GamePhase.Drawing:
                    AskForDrawDecision();
                    return;
                case GamePhase.Bidding:
                    AskForBid();
                    return;
                case GamePhase.Playing:
                    AskForCard();
                    return;
                case GamePhase.HandComplete:
                    ScoreHand();
                    return;
            }
        }

        /// <summary>
        /// Validate a command and park it. Named submit rather than execute because that is the
        /// truth: nothing happens until the next Advance.
        /// </summary>
        public bool TrySubmit(IGameCommand command, out string rejectionReason)
        {
            if (command == null)
            {
                rejectionReason = "Command was null.";
                return false;
            }

            if (_pending != null)
            {
                rejectionReason = "A command is already queued for this step.";
                return false;
            }

            if (!Validate(command, out rejectionReason)) return false;

            _pending = command;
            rejectionReason = null;
            return true;
        }

        // -- phases ---------------------------------------------------------------------------

        private void BeginHand()
        {
            _state.ResetForNewHand();
            _events.Enqueue(new HandStarted(_state.HandNumber, _state.Dealer));

            Card[] deck = Deck.CreateStandard();
            Deck.Shuffle(deck, _rng);

            if (_state.Rules.UsesDrawPhase)
            {
                // The stock is drawn from the end of the list, so taking the top card is O(1).
                for (int i = 0; i < deck.Length; i++) _state.Stock.Add(deck[i]);

                // The non-dealer draws first, which is the same seat that acts first everywhere
                // else in the game.
                _state.CurrentTurn = _state.Rules.FirstToAct(_state.Dealer);
                _drawOfferActive = false;
                Phase = GamePhase.Drawing;
                _events.Enqueue(new DrawPhaseStarted(_state.Stock.Count));
                return;
            }

            DealHands(deck);
            StartBidding();
        }

        private void DealHands(Card[] deck)
        {
            GameRules rules = _state.Rules;
            int first = rules.FirstToAct(_state.Dealer).Index;
            int next = 0;

            // One card at a time round the table, as it is dealt at a real one.
            for (int round = 0; round < rules.HandSize; round++)
            {
                for (int i = 0; i < rules.PlayerCount; i++)
                {
                    var seat = new Seat((first + i) % rules.PlayerCount);
                    _state.GiveCard(seat, deck[next++]);
                }
            }

            SortAllHands();
            EmitHandDealt(first);
        }

        private void StartBidding()
        {
            _state.CurrentTurn = _state.Rules.FirstToAct(_state.Dealer);
            Phase = GamePhase.Bidding;
            _events.Enqueue(new BiddingStarted(_state.CurrentTurn));
        }

        private void StartPlaying()
        {
            _state.CurrentTurn = _state.Rules.FirstToAct(_state.Dealer);
            _trickNumber = 1;
            _state.CurrentTrick = new TrickState(_state.Rules.PlayerCount, _state.CurrentTurn);
            Phase = GamePhase.Playing;
            _events.Enqueue(new TrickStarted(_state.CurrentTurn, _trickNumber));
        }

        private void ScoreHand()
        {
            GameRules rules = _state.Rules;
            _scoreLines.Clear();

            for (int t = 0; t < rules.TeamCount; t++)
            {
                TeamState team = _state.TeamAt(t);

                int teamBid = 0;
                int contractTricks = 0;
                int failedNilTricks = 0;
                int nilPoints = 0;
                int nilTricks = 0;

                for (int i = 0; i < team.Seats.Count; i++)
                {
                    SeatState seatState = _state.SeatAt(team.Seats[i]);
                    bool isNil = rules.AllowNil && seatState.Bid == 0;

                    if (isNil)
                    {
                        bool made = seatState.TricksWon == 0;
                        nilPoints += ScoreCalculator.ScoreNil(made, rules);
                        nilTricks += seatState.TricksWon;
                        if (!made) failedNilTricks += seatState.TricksWon;
                    }
                    else
                    {
                        // A NoBid cannot occur here: the phase only advances once every seat bid.
                        teamBid += seatState.Bid;
                        contractTricks += seatState.TricksWon;
                    }
                }

                int bagsBefore = team.Bags;
                HandScoreResult result = ScoreCalculator.ScoreTeam(
                    teamBid, contractTricks, failedNilTricks, bagsBefore, rules);

                _state.ApplyTeamScore(t, result.Points + nilPoints, result.NewBagCount);

                _scoreLines.Add(new TeamScoreLine(
                    teamId: t,
                    bid: teamBid,
                    tricksWon: contractTricks + nilTricks,
                    contractPoints: result.Points,
                    nilPoints: nilPoints,
                    bagsBefore: bagsBefore,
                    bagsAfter: result.NewBagCount,
                    bagPenaltyApplied: result.BagPenaltyApplied,
                    totalScore: team.Score));
            }

            _events.Enqueue(new HandScored(_state.HandNumber, _scoreLines.ToArray()));

            int winningTeam = DetermineWinningTeam();
            if (winningTeam >= 0)
            {
                Phase = GamePhase.GameOver;
                _events.Enqueue(new GameEnded(winningTeam, CurrentScores()));
                return;
            }

            _state.Dealer = _state.Dealer.Next(rules.PlayerCount);
            Phase = GamePhase.Dealing;
        }

        /// <summary>
        /// The team id that has won, or -1 if the game continues. If both teams cross the target
        /// in the same hand the higher score takes it; an exact tie plays another hand.
        /// </summary>
        private int DetermineWinningTeam()
        {
            GameRules rules = _state.Rules;
            int bestTeam = -1;
            int bestScore = int.MinValue;
            bool tied = false;

            for (int t = 0; t < rules.TeamCount; t++)
            {
                int score = _state.TeamAt(t).Score;
                if (score < rules.TargetScore) continue;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestTeam = t;
                    tied = false;
                }
                else if (score == bestScore)
                {
                    tied = true;
                }
            }

            return tied ? -1 : bestTeam;
        }

        // -- decision requests -----------------------------------------------------------------

        private void AskForBid()
        {
            Seat seat = _state.CurrentTurn;
            _awaitingSeat = seat;
            _decisionRequested = true;
            _state.SeatAt(seat).Controller.RequestBid(_state.ProjectFor(seat), _submitBid);
        }

        private void AskForCard()
        {
            Seat seat = _state.CurrentTurn;
            SeatState seatState = _state.SeatAt(seat);

            LegalMoveValidator.GetLegalMovesNonAlloc(
                seatState.Hand, _state.CurrentTrick, _state.SpadesBroken, _legalBuffer);

            _awaitingSeat = seat;
            _decisionRequested = true;
            seatState.Controller.RequestCard(_state.ProjectFor(seat), _legalBuffer, _submitCard);
        }

        private void AskForDrawDecision()
        {
            if (!_drawOfferActive)
            {
                if (_state.Stock.Count == 0)
                    throw new InvalidOperationException("Stock exhausted during the draw phase.");

                int top = _state.Stock.Count - 1;
                _offeredCard = _state.Stock[top];
                _state.Stock.RemoveAt(top);
                _drawOfferActive = true;
                _events.Enqueue(new CardOffered(_state.CurrentTurn, _offeredCard, _state.Stock.Count));
            }

            Seat seat = _state.CurrentTurn;
            _awaitingSeat = seat;
            _decisionRequested = true;
            _state.SeatAt(seat).Controller.RequestDrawDecision(
                _state.ProjectFor(seat), _offeredCard, _submitDraw);
        }

        // -- command handling --------------------------------------------------------------------

        private void SubmitFromController(IGameCommand command)
        {
            if (TrySubmit(command, out string reason)) return;

            // A controller produced an answer the rules refuse. For an AI that is a strategy bug;
            // for a human the UI already filtered on the same validator, so it is a wiring bug.
            // Either way it should be loud rather than silently dropped.
            throw new InvalidOperationException("Controller submitted an invalid command (" + command + "): " + reason);
        }

        private bool Validate(IGameCommand command, out string reason)
        {
            if (command.Seat.Index < 0 || command.Seat.Index >= _state.Rules.PlayerCount)
            {
                reason = "Seat is not at this table.";
                return false;
            }

            if (command.Seat != _state.CurrentTurn)
            {
                reason = "It is not " + command.Seat + "'s turn.";
                return false;
            }

            switch (command)
            {
                case PlaceBidCommand bid:
                    return ValidateBid(bid, out reason);
                case PlayCardCommand play:
                    return ValidatePlay(play, out reason);
                case DrawDecisionCommand draw:
                    return ValidateDraw(draw, out reason);
                default:
                    reason = "Unknown command type " + command.GetType().Name + ".";
                    return false;
            }
        }

        private bool ValidateBid(PlaceBidCommand command, out string reason)
        {
            if (Phase != GamePhase.Bidding)
            {
                reason = "Not in the bidding phase.";
                return false;
            }

            if (command.Bid < 0 || command.Bid > _state.Rules.HandSize)
            {
                reason = "Bid must be between 0 and " + _state.Rules.HandSize + ".";
                return false;
            }

            if (command.Bid == 0 && !_state.Rules.AllowNil)
            {
                reason = "Nil is disabled in these rules.";
                return false;
            }

            reason = null;
            return true;
        }

        private bool ValidatePlay(PlayCardCommand command, out string reason)
        {
            if (Phase != GamePhase.Playing)
            {
                reason = "Not in the playing phase.";
                return false;
            }

            SeatState seatState = _state.SeatAt(command.Seat);
            if (!seatState.Hand.Contains(command.Card))
            {
                reason = command.Seat + " does not hold " + command.Card + ".";
                return false;
            }

            if (!LegalMoveValidator.IsLegal(command.Card, seatState.Hand, _state.CurrentTrick, _state.SpadesBroken))
            {
                reason = _state.CurrentTrick.LedSuit == null
                    ? "Cannot lead a spade until spades are broken."
                    : "Must follow " + _state.CurrentTrick.LedSuit.Value + ".";
                return false;
            }

            reason = null;
            return true;
        }

        private bool ValidateDraw(DrawDecisionCommand command, out string reason)
        {
            if (Phase != GamePhase.Drawing)
            {
                reason = "Not in the draw phase.";
                return false;
            }

            if (!_drawOfferActive)
            {
                reason = "No card is currently on offer.";
                return false;
            }

            if (!command.Keep && _state.Stock.Count == 0)
            {
                reason = "No card left to take sight-unseen.";
                return false;
            }

            reason = null;
            return true;
        }

        private void ApplyCommand(IGameCommand command)
        {
            switch (command)
            {
                case PlaceBidCommand bid:
                    ApplyBid(bid);
                    return;
                case PlayCardCommand play:
                    ApplyPlay(play);
                    return;
                case DrawDecisionCommand draw:
                    ApplyDraw(draw);
                    return;
                default:
                    throw new InvalidOperationException("Unhandled command " + command.GetType().Name + ".");
            }
        }

        private void ApplyBid(PlaceBidCommand command)
        {
            _state.SetBid(command.Seat, command.Bid);
            _events.Enqueue(new BidPlaced(command.Seat, command.Bid));

            if (_state.AllSeatsHaveBid())
            {
                _events.Enqueue(new BiddingComplete(CurrentBids()));
                StartPlaying();
                return;
            }

            _state.CurrentTurn = command.Seat.Next(_state.Rules.PlayerCount);
            _events.Enqueue(new TurnChanged(_state.CurrentTurn));
        }

        private void ApplyPlay(PlayCardCommand command)
        {
            bool brokeSpades = command.Card.IsSpade && !_state.SpadesBroken;

            _state.RemoveCard(command.Seat, command.Card);
            _state.CurrentTrick.Add(command.Seat, command.Card);
            if (command.Card.IsSpade) _state.SpadesBroken = true;

            _events.Enqueue(new CardPlayed(command.Seat, command.Card, brokeSpades));
            _cardsPlayedThisAdvance++;

            if (!_state.CurrentTrick.IsComplete)
            {
                _state.CurrentTurn = command.Seat.Next(_state.Rules.PlayerCount);
                _events.Enqueue(new TurnChanged(_state.CurrentTurn));
                return;
            }

            Seat winner = TrickResolver.DetermineWinner(_state.CurrentTrick);
            _state.AwardTrick(winner);
            _events.Enqueue(new TrickWon(winner, _state.CurrentTrick.Cards, _trickNumber));

            if (_state.AllHandsEmpty())
            {
                Phase = GamePhase.HandComplete;
                return;
            }

            _trickNumber++;
            _state.CurrentTurn = winner;
            _state.CurrentTrick = new TrickState(_state.Rules.PlayerCount, winner);
            _events.Enqueue(new TrickStarted(winner, _trickNumber));
        }

        private void ApplyDraw(DrawDecisionCommand command)
        {
            Card taken;
            Card discarded = default;

            if (command.Keep)
            {
                taken = _offeredCard;
            }
            else
            {
                // Declining commits you to the next card sight-unseen. Twenty-six turns can
                // consume at most fifty-two cards, so the stock provably cannot run dry here;
                // the validator rejects the command if it somehow did.
                discarded = _offeredCard;
                _state.Discards.Add(discarded);

                int top = _state.Stock.Count - 1;
                taken = _state.Stock[top];
                _state.Stock.RemoveAt(top);
            }

            _state.GiveCard(command.Seat, taken);
            _drawOfferActive = false;
            _events.Enqueue(new CardDrawn(command.Seat, taken, command.Keep, discarded, _state.Stock.Count));

            if (DrawPhaseComplete())
            {
                SortAllHands();
                EmitHandDealt(_state.Rules.FirstToAct(_state.Dealer).Index);
                StartBidding();
                return;
            }

            _state.CurrentTurn = command.Seat.Next(_state.Rules.PlayerCount);
            _events.Enqueue(new TurnChanged(_state.CurrentTurn));
        }

        private bool DrawPhaseComplete()
        {
            for (int i = 0; i < _state.Seats.Count; i++)
            {
                if (_state.Seats[i].Hand.Count < _state.Rules.HandSize) return false;
            }
            return true;
        }

        // -- helpers -----------------------------------------------------------------------------

        private void EmitHandDealt(int firstSeatIndex)
        {
            GameRules rules = _state.Rules;
            for (int i = 0; i < rules.PlayerCount; i++)
            {
                var seat = new Seat((firstSeatIndex + i) % rules.PlayerCount);
                _events.Enqueue(new HandDealt(seat, _state.SeatAt(seat).Hand.ToArray()));
            }
        }

        /// <summary>
        /// Sorting changes nothing about legality: it makes GetLegalMoves return a stable order,
        /// which keeps the on-screen hand from reshuffling itself between turns.
        /// </summary>
        private void SortAllHands()
        {
            for (int i = 0; i < _state.Seats.Count; i++)
            {
                _state.Seats[i].Hand.Sort(HandOrder);
            }
        }

        private static int CompareForDisplay(Card a, Card b)
        {
            if (a.Suit != b.Suit) return a.Suit.CompareTo(b.Suit);
            return a.Rank.CompareTo(b.Rank);
        }

        private int[] CurrentBids()
        {
            var bids = new int[_state.Rules.PlayerCount];
            for (int i = 0; i < bids.Length; i++) bids[i] = _state.Seats[i].Bid;
            return bids;
        }

        private int[] CurrentScores()
        {
            var scores = new int[_state.Rules.TeamCount];
            for (int t = 0; t < scores.Length; t++) scores[t] = _state.TeamAt(t).Score;
            return scores;
        }
    }
}
