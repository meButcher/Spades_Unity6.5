using NUnit.Framework;
using Spades.Core.Rules;

namespace Spades.Core.Tests
{
    public class ScoreCalculatorTests
    {
        private GameRules _rules;

        [SetUp]
        public void SetUp()
        {
            _rules = GameRules.Standard4Player();
        }

        [Test]
        public void MadeExactly_ScoresTenTimesBid_WithNoBags()
        {
            HandScoreResult r = ScoreCalculator.ScoreTeam(
                teamBid: 4, contractTricks: 4, failedNilTricks: 0, currentBags: 0, rules: _rules);

            Assert.AreEqual(40, r.Points);
            Assert.AreEqual(0, r.NewBagCount);
        }

        [Test]
        public void Overtricks_AddOnePointAndOneBagEach()
        {
            HandScoreResult r = ScoreCalculator.ScoreTeam(
                teamBid: 4, contractTricks: 6, failedNilTricks: 0, currentBags: 0, rules: _rules);

            Assert.AreEqual(42, r.Points);
            Assert.AreEqual(2, r.NewBagCount);
        }

        [Test]
        public void SetContract_ScoresNegativeTenTimesBid_AndLeavesBagsUnchanged()
        {
            HandScoreResult r = ScoreCalculator.ScoreTeam(
                teamBid: 5, contractTricks: 3, failedNilTricks: 0, currentBags: 3, rules: _rules);

            Assert.AreEqual(-50, r.Points);
            Assert.AreEqual(3, r.NewBagCount);
        }

        [Test]
        public void BagPenalty_CarriesTheRemainder_AndDoesNotZeroTheCounter()
        {
            // Nine bags plus three overtricks is twelve: minus a hundred, and two carried over.
            HandScoreResult r = ScoreCalculator.ScoreTeam(
                teamBid: 2, contractTricks: 5, failedNilTricks: 0, currentBags: 9, rules: _rules);

            Assert.AreEqual(20 + 3 - 100, r.Points);
            Assert.AreEqual(2, r.NewBagCount, "The remainder must carry, not reset to zero.");
            Assert.AreEqual(1, r.PenaltiesApplied);
        }

        [Test]
        public void BagPenalty_AppliesTwice_WhenBagsExceedTwiceTheThreshold()
        {
            // Both partners bid Nil, so the contract is zero, and the team took all thirteen
            // tricks. Nine plus thirteen is twenty-two, which needs two penalties, not one.
            HandScoreResult r = ScoreCalculator.ScoreTeam(
                teamBid: 0, contractTricks: 13, failedNilTricks: 0, currentBags: 9, rules: _rules);

            Assert.AreEqual(13 - 200, r.Points);
            Assert.AreEqual(2, r.NewBagCount);
            Assert.AreEqual(2, r.PenaltiesApplied);
        }

        [Test]
        public void NilMade_ScoresPlusOneHundred()
        {
            Assert.AreEqual(100, ScoreCalculator.ScoreNil(true, _rules));
        }

        [Test]
        public void NilFailed_ScoresMinusOneHundred()
        {
            Assert.AreEqual(-100, ScoreCalculator.ScoreNil(false, _rules));
        }

        [Test]
        public void FailedNilTricks_BecomeBags_ButDoNotSatisfyThePartnersContract()
        {
            // The partner bid five and took three. The Nil bidder took two and failed.
            // Five tricks are on the table, but the contract is still set, because the Nil
            // bidder's tricks do not count toward it.
            HandScoreResult r = ScoreCalculator.ScoreTeam(
                teamBid: 5, contractTricks: 3, failedNilTricks: 2, currentBags: 0, rules: _rules);

            Assert.AreEqual(-50, r.Points, "The contract is set despite five team tricks.");
            Assert.AreEqual(2, r.NewBagCount, "The failed Nil's tricks still cost bags.");

            int handTotal = r.Points + ScoreCalculator.ScoreNil(false, _rules);
            Assert.AreEqual(-150, handTotal);
        }

        [Test]
        public void FailedNil_AddsBags_EvenWhenThePartnerMakesTheContract()
        {
            // Partner bid three and took four. The Nil bidder took two and failed.
            HandScoreResult r = ScoreCalculator.ScoreTeam(
                teamBid: 3, contractTricks: 4, failedNilTricks: 2, currentBags: 0, rules: _rules);

            Assert.AreEqual(31, r.Points);                   // 30 for the contract, 1 overtrick
            Assert.AreEqual(3, r.NewBagCount);               // 1 overtrick plus the 2 Nil tricks

            int handTotal = r.Points + ScoreCalculator.ScoreNil(false, _rules);
            Assert.AreEqual(-69, handTotal);
        }

        [Test]
        public void MadeNil_CostsTheTeamNothingExtra()
        {
            HandScoreResult r = ScoreCalculator.ScoreTeam(
                teamBid: 4, contractTricks: 4, failedNilTricks: 0, currentBags: 0, rules: _rules);

            int handTotal = r.Points + ScoreCalculator.ScoreNil(true, _rules);
            Assert.AreEqual(140, handTotal);
            Assert.AreEqual(0, r.NewBagCount);
        }

        [Test]
        public void ZeroBid_WithoutNil_MakesEveryTrickABag()
        {
            // With Nil disabled a bid of zero is an ordinary contract of zero tricks, which is
            // trivially made, so everything taken is an overtrick.
            var noNil = new GameRules(4, 13, 500, 10, -100, 100, allowNil: false, usesDrawPhase: false);

            HandScoreResult r = ScoreCalculator.ScoreTeam(0, 3, 0, 0, noNil);

            Assert.AreEqual(3, r.Points);
            Assert.AreEqual(3, r.NewBagCount);
        }
    }
}
