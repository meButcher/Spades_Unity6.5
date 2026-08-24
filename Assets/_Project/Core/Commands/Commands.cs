using Spades.Core.Cards;
using Spades.Core.State;

namespace Spades.Core.Commands
{
    /// <summary>Declare a contract for the hand. A bid of 0 is a Nil declaration.</summary>
    public readonly struct PlaceBidCommand : IGameCommand
    {
        public PlaceBidCommand(Seat seat, int bid)
        {
            Seat = seat;
            Bid = bid;
        }

        public Seat Seat { get; }
        public int Bid { get; }

        public override string ToString() => Seat + " bids " + Bid;
    }

    /// <summary>Play one card to the current trick.</summary>
    public readonly struct PlayCardCommand : IGameCommand
    {
        public PlayCardCommand(Seat seat, Card card)
        {
            Seat = seat;
            Card = card;
        }

        public Seat Seat { get; }
        public Card Card { get; }

        public override string ToString() => Seat + " plays " + Card;
    }

    /// <summary>
    /// Keep the card just drawn, or discard it and take the next one sight-unseen.
    /// 2-player draw phase only.
    /// </summary>
    public readonly struct DrawDecisionCommand : IGameCommand
    {
        public DrawDecisionCommand(Seat seat, bool keep)
        {
            Seat = seat;
            Keep = keep;
        }

        public Seat Seat { get; }
        public bool Keep { get; }

        public override string ToString() => Seat + (Keep ? " keeps" : " discards");
    }
}
