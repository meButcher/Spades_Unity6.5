using NUnit.Framework;
using Spades.Core.Events;
using Spades.Core.Rules;

namespace Spades.Core.Tests
{
    /// <summary>
    /// The headline test. It plays complete games of Spades with no scene, no GameObject and no
    /// renderer, which is the single fact that demonstrates the whole architecture: if the rules
    /// engine could not run headless, none of the rest of the separation would be real.
    ///
    /// Running it across hundreds of seeds is what finds the deal nobody thought of. The two
    /// classic failures it catches are a seat holding only spades before they are broken (zero
    /// legal moves, so the game hangs) and a controller that executes instead of enqueuing (so
    /// one Advance recurses through a hand).
    /// </summary>
    public class FullGameSimulationTests
    {
        private const int SeedCount = 500;

        [Test]
        public void FourPlayerGame_CompletesAndHoldsEveryInvariant()
        {
            var harness = GameHarness.AllBots(GameRules.Standard4Player(), seed: 12345);
            harness.RunToCompletion();

            Assert.IsNull(harness.ValidateEventStream());
            AssertGameEndedCleanly(harness);
        }

        [Test]
        public void FourPlayerGames_CompleteAcross500Seeds()
        {
            for (int seed = 0; seed < SeedCount; seed++)
            {
                var harness = GameHarness.AllBots(GameRules.Standard4Player(), seed);

                try
                {
                    harness.RunToCompletion();
                }
                catch (System.Exception ex)
                {
                    Assert.Fail("Seed " + seed + " threw: " + ex);
                }

                string violation = harness.ValidateEventStream();
                Assert.IsNull(violation, "Seed " + seed + ": " + violation);
                AssertGameEndedCleanly(harness, seed);
            }
        }

        [Test]
        public void TwoPlayerGames_CompleteAcross500Seeds()
        {
            for (int seed = 0; seed < SeedCount; seed++)
            {
                var harness = GameHarness.AllBots(GameRules.Standard2Player(), seed);

                try
                {
                    harness.RunToCompletion();
                }
                catch (System.Exception ex)
                {
                    Assert.Fail("Seed " + seed + " threw: " + ex);
                }

                string violation = harness.ValidateEventStream();
                Assert.IsNull(violation, "Seed " + seed + ": " + violation);
                AssertGameEndedCleanly(harness, seed);
            }
        }

        [Test]
        public void SameSeed_ProducesTheIdenticalGame()
        {
            var a = GameHarness.AllBots(GameRules.Standard4Player(), seed: 777);
            var b = GameHarness.AllBots(GameRules.Standard4Player(), seed: 777);

            a.RunToCompletion();
            b.RunToCompletion();

            Assert.AreEqual(a.Log.Count, b.Log.Count, "Two runs of the same seed diverged in length.");

            var playsA = a.AllOf<CardPlayed>();
            var playsB = b.AllOf<CardPlayed>();

            for (int i = 0; i < playsA.Count; i++)
            {
                Assert.AreEqual(playsA[i].Card, playsB[i].Card, "Play " + i + " differed.");
                Assert.AreEqual(playsA[i].Seat, playsB[i].Seat, "Play " + i + " differed.");
            }
        }

        [Test]
        public void BagsNeverGoNegativeOrExceedTheThreshold()
        {
            for (int seed = 0; seed < 50; seed++)
            {
                var harness = GameHarness.AllBots(GameRules.Standard4Player(), seed);
                harness.RunToCompletion();

                foreach (HandScored scored in harness.AllOf<HandScored>())
                {
                    foreach (var line in scored.Lines)
                    {
                        Assert.GreaterOrEqual(line.BagsAfter, 0, "seed " + seed);
                        Assert.Less(line.BagsAfter, 10,
                            "seed " + seed + ": a carried bag count must always be below the penalty threshold.");
                    }
                }
            }
        }

        [Test]
        public void EveryNilIsScoredAsExactlyPlusOrMinusOneHundred()
        {
            for (int seed = 0; seed < 50; seed++)
            {
                var harness = GameHarness.AllBots(GameRules.Standard4Player(), seed);
                harness.RunToCompletion();

                foreach (HandScored scored in harness.AllOf<HandScored>())
                {
                    foreach (var line in scored.Lines)
                    {
                        Assert.AreEqual(0, line.NilPoints % 100,
                            "seed " + seed + ": Nil points must be a multiple of a hundred.");
                        Assert.LessOrEqual(System.Math.Abs(line.NilPoints), 200,
                            "seed " + seed + ": at most two Nils per team per hand.");
                    }
                }
            }
        }

        private static void AssertGameEndedCleanly(GameHarness harness, int seed = -1)
        {
            string label = seed < 0 ? "" : "seed " + seed + ": ";

            Assert.IsTrue(harness.Loop.IsGameOver, label + "The loop did not reach GameOver.");

            var ended = harness.AllOf<GameEnded>();
            Assert.AreEqual(1, ended.Count, label + "Expected exactly one GameEnded.");

            int winner = ended[0].WinningTeamId;
            Assert.GreaterOrEqual(winner, 0, label + "No winning team.");

            int winningScore = harness.Loop.ScoreForTeam(winner);
            Assert.GreaterOrEqual(winningScore, harness.Rules.TargetScore,
                label + "The winner is below the target score.");

            for (int t = 0; t < harness.Rules.TeamCount; t++)
            {
                if (t == winner) continue;
                Assert.Less(harness.Loop.ScoreForTeam(t), winningScore,
                    label + "The winner did not have the highest score.");
            }
        }
    }
}
