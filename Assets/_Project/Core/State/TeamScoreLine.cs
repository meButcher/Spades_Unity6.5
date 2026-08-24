namespace Spades.Core.State
{
    /// <summary>One row of the end-of-hand summary. Carried by the HandScored event.</summary>
    public readonly struct TeamScoreLine
    {
        public TeamScoreLine(int teamId, int bid, int tricksWon, int contractPoints,
                             int nilPoints, int bagsBefore, int bagsAfter,
                             bool bagPenaltyApplied, int totalScore)
        {
            TeamId = teamId;
            Bid = bid;
            TricksWon = tricksWon;
            ContractPoints = contractPoints;
            NilPoints = nilPoints;
            BagsBefore = bagsBefore;
            BagsAfter = bagsAfter;
            BagPenaltyApplied = bagPenaltyApplied;
            TotalScore = totalScore;
        }

        public int TeamId { get; }
        public int Bid { get; }
        public int TricksWon { get; }
        public int ContractPoints { get; }
        public int NilPoints { get; }
        public int BagsBefore { get; }
        public int BagsAfter { get; }
        public bool BagPenaltyApplied { get; }

        /// <summary>The team's running total after this hand was applied.</summary>
        public int TotalScore { get; }

        public int HandPoints => ContractPoints + NilPoints;
    }
}
