using Spades.Core.Cards;
using Spades.Core.State;

namespace Spades.Core.Ai
{
    /// <summary>
    /// The 2-player draw decision. Declining a card means taking the next one blind, so the
    /// question is only ever "is this card better than an unknown card?".
    ///
    /// An average card is about a nine. Spades are worth keeping at any rank because they are
    /// trumps, and side-suit honours are worth keeping because they win tricks outright.
    /// Everything else is a coin flip that costs nothing to re-roll.
    /// </summary>
    public sealed class HeuristicDrawStrategy : IDrawStrategy
    {
        public bool ShouldKeep(SeatView view, Card drawn)
        {
            if (drawn.IsSpade) return true;
            return drawn.Rank >= Rank.Queen;
        }
    }
}
