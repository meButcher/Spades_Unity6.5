using System.Collections.Generic;
using Spades.Core.Cards;
using Spades.Core.Rules;

namespace Spades.Core.State
{
    /// <summary>
    /// Everything one seat is allowed to know, and nothing else.
    ///
    /// Neither the AI nor the UI ever receives GameState. They receive this projection, which
    /// contains this seat's hand and otherwise only public information. That makes an AI that
    /// peeks at your cards structurally impossible rather than merely discouraged, and it is
    /// the exact hidden-information boundary a card-game server needs.
    ///
    /// A readonly struct over the live backing arrays, so building one allocates nothing.
    /// </summary>
    public readonly struct SeatView
    {
        public SeatView(
            Seat seat,
            int teamId,
            GameRules rules,
            Seat dealer,
            IReadOnlyList<Card> hand,
            IReadOnlyList<int> bids,
            IReadOnlyList<int> tricksWon,
            IReadOnlyList<int> handCounts,
            TrickState currentTrick,
            IReadOnlyList<Card> playedCards,
            bool spadesBroken,
            ScoreSnapshot scores)
        {
            Seat = seat;
            TeamId = teamId;
            Rules = rules;
            Dealer = dealer;
            Hand = hand;
            Bids = bids;
            TricksWon = tricksWon;
            HandCounts = handCounts;
            CurrentTrick = currentTrick;
            PlayedCards = playedCards;
            SpadesBroken = spadesBroken;
            Scores = scores;
        }

        public Seat Seat { get; }
        public int TeamId { get; }
        public GameRules Rules { get; }
        public Seat Dealer { get; }

        /// <summary>This seat's cards. The only private information in the projection.</summary>
        public IReadOnlyList<Card> Hand { get; }

        /// <summary>Bids by seat index, or SeatState.NoBid where a seat has not bid yet.</summary>
        public IReadOnlyList<int> Bids { get; }

        public IReadOnlyList<int> TricksWon { get; }

        /// <summary>How many cards each seat holds. Public; the card identities are not.</summary>
        public IReadOnlyList<int> HandCounts { get; }

        public TrickState CurrentTrick { get; }

        /// <summary>Every card played face-up this hand, in order. The basis for card counting.</summary>
        public IReadOnlyList<Card> PlayedCards { get; }

        public bool SpadesBroken { get; }
        public ScoreSnapshot Scores { get; }

        public int MyBid => Bids[Seat.Index];
        public int MyTricks => TricksWon[Seat.Index];

        /// <summary>
        /// The sum of the non-Nil bids on this seat's team. A Nil bid contributes nothing to the
        /// team contract because it is scored as a separate side bet.
        /// </summary>
        public int TeamBid
        {
            get
            {
                int total = 0;
                for (int i = 0; i < Bids.Count; i++)
                {
                    if (Rules.TeamIdForSeat(new Seat(i)) != TeamId) continue;
                    if (Bids[i] <= 0) continue;   // NoBid (-1) and Nil (0) both contribute zero
                    total += Bids[i];
                }
                return total;
            }
        }

        /// <summary>Tricks taken so far this hand by this seat's team.</summary>
        public int TeamTricks
        {
            get
            {
                int total = 0;
                for (int i = 0; i < TricksWon.Count; i++)
                {
                    if (Rules.TeamIdForSeat(new Seat(i)) == TeamId) total += TricksWon[i];
                }
                return total;
            }
        }
    }
}
