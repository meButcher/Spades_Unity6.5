using Spades.Core.Cards;
using Spades.Core.State;

namespace Spades.Core.Ai
{
    /// <summary>2-player draw phase only: keep the card just turned over, or take a blind one.</summary>
    public interface IDrawStrategy
    {
        bool ShouldKeep(SeatView view, Card drawn);
    }
}
