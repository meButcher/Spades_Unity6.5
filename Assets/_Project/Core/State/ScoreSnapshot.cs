using System.Collections.Generic;

namespace Spades.Core.State
{
    /// <summary>
    /// A read-only window onto the scoreboard, indexed by team id. Backed by the live arrays
    /// in GameState, so projecting one costs no allocation.
    /// </summary>
    public readonly struct ScoreSnapshot
    {
        public ScoreSnapshot(IReadOnlyList<int> scores, IReadOnlyList<int> bags)
        {
            Scores = scores;
            Bags = bags;
        }

        public IReadOnlyList<int> Scores { get; }
        public IReadOnlyList<int> Bags { get; }
    }
}
