using System;
using System.Collections.Generic;
using Spades.Core.Cards;
using Spades.Core.Players;
using Spades.Core.Rules;

namespace Spades.Core.State
{
    /// <summary>
    /// The single mutable source of truth for a game in progress.
    ///
    /// Only GameLoop mutates it, and nothing outside the engine reads it directly: every
    /// consumer goes through ProjectFor(seat).
    /// </summary>
    public sealed class GameState
    {
        private readonly SeatState[] _seats;
        private readonly TeamState[] _teams;

        // Flat mirrors of per-seat and per-team data, kept in step with the objects above so a
        // SeatView can be handed out as a window onto live arrays instead of copying anything.
        private readonly int[] _bids;
        private readonly int[] _tricksWon;
        private readonly int[] _handCounts;
        private readonly int[] _scores;
        private readonly int[] _bags;

        private readonly List<Card> _playedThisHand;

        public GameState(GameRules rules, IReadOnlyList<IPlayerController> controllers)
        {
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            if (controllers == null) throw new ArgumentNullException(nameof(controllers));
            if (controllers.Count != rules.PlayerCount)
            {
                throw new ArgumentException(
                    "Expected " + rules.PlayerCount + " controllers but got " + controllers.Count + ".",
                    nameof(controllers));
            }

            Rules = rules;

            _teams = new TeamState[rules.TeamCount];
            for (int t = 0; t < _teams.Length; t++) _teams[t] = new TeamState(t);

            _seats = new SeatState[rules.PlayerCount];
            for (int i = 0; i < _seats.Length; i++)
            {
                var seat = new Seat(i);
                int teamId = rules.TeamIdForSeat(seat);
                _seats[i] = new SeatState(seat, teamId, controllers[i]);
                _teams[teamId].AddSeat(seat);
            }

            _bids = new int[rules.PlayerCount];
            _tricksWon = new int[rules.PlayerCount];
            _handCounts = new int[rules.PlayerCount];
            _scores = new int[rules.TeamCount];
            _bags = new int[rules.TeamCount];
            _playedThisHand = new List<Card>(Deck.StandardSize);

            Stock = new List<Card>(Deck.StandardSize);
            Discards = new List<Card>(Deck.StandardSize);

            Dealer = new Seat(0);
            CurrentTurn = new Seat(0);
            HandNumber = 0;

            SyncMirrors();
        }

        public GameRules Rules { get; }
        public IReadOnlyList<SeatState> Seats => _seats;
        public IReadOnlyList<TeamState> Teams => _teams;

        public Seat Dealer { get; set; }
        public Seat CurrentTurn { get; set; }
        public bool SpadesBroken { get; set; }
        public TrickState CurrentTrick { get; set; }
        public int HandNumber { get; set; }

        /// <summary>Face-down draw pile. Used only by the 2-player draw phase.</summary>
        public List<Card> Stock { get; }

        /// <summary>Cards discarded face-up during the 2-player draw phase.</summary>
        public List<Card> Discards { get; }

        public IReadOnlyList<Card> PlayedThisHand => _playedThisHand;

        public SeatState SeatAt(Seat seat) => _seats[seat.Index];
        public TeamState TeamAt(int teamId) => _teams[teamId];
        public TeamState TeamOf(Seat seat) => _teams[Rules.TeamIdForSeat(seat)];

        // -- mutation, all of it driven from GameLoop ---------------------------------------

        public void ResetForNewHand()
        {
            for (int i = 0; i < _seats.Length; i++) _seats[i].ResetForNewHand();

            SpadesBroken = false;
            CurrentTrick = null;
            _playedThisHand.Clear();
            Stock.Clear();
            Discards.Clear();
            HandNumber++;

            SyncMirrors();
        }

        public void SetBid(Seat seat, int bid)
        {
            _seats[seat.Index].Bid = bid;
            _bids[seat.Index] = bid;
        }

        public void GiveCard(Seat seat, Card card)
        {
            _seats[seat.Index].Hand.Add(card);
            _handCounts[seat.Index] = _seats[seat.Index].Hand.Count;
        }

        public void RemoveCard(Seat seat, Card card)
        {
            if (!_seats[seat.Index].Hand.Remove(card))
                throw new InvalidOperationException(seat + " does not hold " + card + ".");

            _handCounts[seat.Index] = _seats[seat.Index].Hand.Count;
            _playedThisHand.Add(card);
        }

        public void AwardTrick(Seat winner)
        {
            _seats[winner.Index].TricksWon++;
            _tricksWon[winner.Index] = _seats[winner.Index].TricksWon;
        }

        public void ApplyTeamScore(int teamId, int points, int newBags)
        {
            _teams[teamId].Score += points;
            _teams[teamId].Bags = newBags;
            _scores[teamId] = _teams[teamId].Score;
            _bags[teamId] = newBags;
        }

        public bool AllHandsEmpty()
        {
            for (int i = 0; i < _seats.Length; i++)
            {
                if (_seats[i].Hand.Count > 0) return false;
            }
            return true;
        }

        public bool AllSeatsHaveBid()
        {
            for (int i = 0; i < _seats.Length; i++)
            {
                if (!_seats[i].HasBid) return false;
            }
            return true;
        }

        /// <summary>The only way anyone outside the engine reads state. Allocation-free.</summary>
        public SeatView ProjectFor(Seat seat)
        {
            SeatState s = _seats[seat.Index];

            return new SeatView(
                seat: seat,
                teamId: s.TeamId,
                rules: Rules,
                dealer: Dealer,
                hand: s.Hand,
                bids: _bids,
                tricksWon: _tricksWon,
                handCounts: _handCounts,
                currentTrick: CurrentTrick,
                playedCards: _playedThisHand,
                spadesBroken: SpadesBroken,
                scores: new ScoreSnapshot(_scores, _bags));
        }

        private void SyncMirrors()
        {
            for (int i = 0; i < _seats.Length; i++)
            {
                _bids[i] = _seats[i].Bid;
                _tricksWon[i] = _seats[i].TricksWon;
                _handCounts[i] = _seats[i].Hand.Count;
            }

            for (int t = 0; t < _teams.Length; t++)
            {
                _scores[t] = _teams[t].Score;
                _bags[t] = _teams[t].Bags;
            }
        }
    }
}
