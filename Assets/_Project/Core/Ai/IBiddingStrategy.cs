using Spades.Core.Rules;
using Spades.Core.State;

namespace Spades.Core.Ai
{
    public interface IBiddingStrategy
    {
        int ChooseBid(SeatView view, GameRules rules);
    }
}
