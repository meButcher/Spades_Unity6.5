using System;

namespace Spades.Core.Rules
{
    public static class ScoreCalculator
    {
        /// <summary>
        /// Scores one team's contract for one hand.
        ///
        /// The trick counts are split in three because Nil is a side bet, not part of the
        /// contract: a Nil bidder's bid contributes nothing to <paramref name="teamBid"/>, and
        /// their tricks never satisfy their partner's contract. If the Nil fails, those tricks
        /// still land on the team as bags. That is the rule most implementations get wrong.
        /// </summary>
        /// <param name="teamBid">Sum of the non-Nil bids on this team.</param>
        /// <param name="contractTricks">Tricks taken by the non-Nil members. These satisfy the contract.</param>
        /// <param name="failedNilTricks">Tricks taken by members whose Nil failed. Bags only.</param>
        /// <param name="currentBags">The team's running bag count coming into this hand.</param>
        public static HandScoreResult ScoreTeam(
            int teamBid,
            int contractTricks,
            int failedNilTricks,
            int currentBags,
            GameRules rules)
        {
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            if (teamBid < 0) throw new ArgumentOutOfRangeException(nameof(teamBid));
            if (contractTricks < 0) throw new ArgumentOutOfRangeException(nameof(contractTricks));
            if (failedNilTricks < 0) throw new ArgumentOutOfRangeException(nameof(failedNilTricks));
            if (currentBags < 0) throw new ArgumentOutOfRangeException(nameof(currentBags));

            int points;
            int bags = currentBags + failedNilTricks;

            if (contractTricks >= teamBid)
            {
                int overtricks = contractTricks - teamBid;
                points = teamBid * 10 + overtricks;
                bags += overtricks;
            }
            else
            {
                // Contract set. Overtricks are irrelevant because there are none.
                points = -(teamBid * 10);
            }

            // A loop rather than a single test: a team on 9 bags whose partners both bid Nil and
            // who then took all 13 tricks lands on 22 bags, and one subtraction would leave it
            // still above the threshold with the second penalty never applied.
            int penalties = 0;
            while (bags >= rules.BagPenaltyThreshold)
            {
                points += rules.BagPenaltyPoints;    // adding a negative
                bags -= rules.BagPenaltyThreshold;   // carry the remainder; do not zero it
                penalties++;
            }

            return new HandScoreResult(points, bags, penalties);
        }

        /// <summary>A Nil is a side bet, scored independently of the partner's contract.</summary>
        public static int ScoreNil(bool succeeded, GameRules rules)
        {
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            return succeeded ? rules.NilBonus : -rules.NilBonus;
        }
    }
}
