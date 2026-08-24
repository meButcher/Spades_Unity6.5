using System.Collections.Generic;
using Spades.Core.Cards;
using Spades.Core.State;

namespace Spades.Core.Ai
{
    public interface ICardStrategy
    {
        /// <summary>
        /// Must return one of <paramref name="legalMoves"/>. The strategy is never given the
        /// full hand-filtering job, so an AI that plays an illegal card is not expressible here.
        /// </summary>
        Card ChooseCard(SeatView view, IReadOnlyList<Card> legalMoves);
    }
}
