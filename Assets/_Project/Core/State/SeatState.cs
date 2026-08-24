using System.Collections.Generic;
using Spades.Core.Cards;
using Spades.Core.Players;

namespace Spades.Core.State
{
    /// <summary>Everything the engine tracks about one player position during a game.</summary>
    public sealed class SeatState
    {
        public const int NoBid = -1;

        public SeatState(Seat seat, int teamId, IPlayerController controller)
        {
            Seat = seat;
            TeamId = teamId;
            Controller = controller;
            Hand = new List<Card>(13);
            Bid = NoBid;
        }

        public Seat Seat { get; }
        public int TeamId { get; }
        public IPlayerController Controller { get; }
        public List<Card> Hand { get; }

        /// <summary>The bid for the current hand, or NoBid (-1) before this seat has bid.</summary>
        public int Bid { get; set; }

        public int TricksWon { get; set; }

        public bool HasBid => Bid != NoBid;

        /// <summary>A bid of exactly zero is a Nil declaration, not a contract of zero tricks.</summary>
        public bool IsNil => Bid == 0;

        public void ResetForNewHand()
        {
            Hand.Clear();
            Bid = NoBid;
            TricksWon = 0;
        }
    }
}
