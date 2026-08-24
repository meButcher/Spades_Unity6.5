namespace Spades.Core.Rules
{
    /// <summary>The outcome of scoring one team's contract for one hand.</summary>
    public readonly struct HandScoreResult
    {
        public HandScoreResult(int points, int newBagCount, int penaltiesApplied)
        {
            Points = points;
            NewBagCount = newBagCount;
            PenaltiesApplied = penaltiesApplied;
        }

        /// <summary>Contract points for the hand, with any bag penalty already folded in.</summary>
        public int Points { get; }

        /// <summary>The team's bag count carried into the next hand.</summary>
        public int NewBagCount { get; }

        public int PenaltiesApplied { get; }

        public bool BagPenaltyApplied => PenaltiesApplied > 0;

        public override string ToString()
        {
            return "Points=" + Points + ", Bags=" + NewBagCount + ", Penalties=" + PenaltiesApplied;
        }
    }
}
