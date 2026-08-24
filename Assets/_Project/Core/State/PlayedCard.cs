using Spades.Core.Cards;

namespace Spades.Core.State
{
    /// <summary>A card together with the seat that played it.</summary>
    public readonly struct PlayedCard
    {
        public Seat Seat { get; }
        public Card Card { get; }

        public PlayedCard(Seat seat, Card card)
        {
            Seat = seat;
            Card = card;
        }

        public override string ToString() => Seat + ":" + Card;
    }
}
