using System.Collections.Generic;
using NUnit.Framework;
using Spades.Core.Cards;
using Spades.Core.Events;
using Spades.Core.Players;
using Spades.Core.Rules;
using Spades.Core.State;

namespace Spades.Core.Tests
{
    /// <summary>
    /// Runs a full game through the exact control flow GamePresenter uses, with one human seat.
    ///
    /// GamePresenter itself is a MonoBehaviour and its animation cannot run in an EditMode test,
    /// but its drive sequence -- advance, drain everything that came out, and only then ask
    /// whether the machine is parked on a human -- is pure logic and is the part most likely to
    /// deadlock. Reproducing that sequence here means a change to the parking rules fails in the
    /// test suite rather than as a frozen table.
    /// </summary>
    public class PresentationLoopTests
    {
        /// <summary>
        /// The presenter's loop with the animation removed. Structurally identical: nothing is
        /// asked of the human until the event queue has been fully drained.
        /// </summary>
        private static GameHarness PlayWithHuman(GameRules rules, int seed, out int humanDecisions)
        {
            var human = new HumanPlayerController();
            var controllers = new IPlayerController[rules.PlayerCount];
            controllers[0] = human;
            for (int i = 1; i < controllers.Length; i++) controllers[i] = AiPlayerController.CreateDefault();

            var harness = new GameHarness(rules, seed, controllers);
            var humanSeat = new Seat(0);

            humanDecisions = 0;
            int steps = 0;

            while (!harness.Loop.IsGameOver)
            {
                Assert.Less(steps++, 400000, "The presenter loop failed to make progress (seed " + seed + ").");

                harness.Loop.Advance();
                harness.DrainEvents();          // stands in for "animate every event to completion"

                if (harness.Loop.IsGameOver) break;

                if (!harness.Loop.IsAwaitingInput) continue;

                Assert.AreEqual(humanSeat, harness.Loop.AwaitingSeat,
                    "Only the human seat should ever park the loop.");

                humanDecisions++;
                Answer(human, harness);
            }

            return harness;
        }

        /// <summary>A deliberately simple player: bid three, and always play the first legal card.</summary>
        private static void Answer(HumanPlayerController human, GameHarness harness)
        {
            if (human.PendingBid)
            {
                Assert.IsTrue(human.SubmitBid(3), "A bid of three must always be accepted.");
                return;
            }

            if (human.PendingCard)
            {
                IReadOnlyList<Card> legal = human.LegalMoves;
                Assert.Greater(legal.Count, 0, "The human was asked for a card with no legal move.");
                Assert.IsTrue(human.SubmitCard(legal[0]), "A card from the legal list must be accepted.");
                return;
            }

            if (human.PendingDraw)
            {
                Assert.IsTrue(human.SubmitDrawDecision(true));
                return;
            }

            Assert.Fail("The loop parked on the human with no pending request.");
        }

        [Test]
        public void FourPlayerGameWithAHuman_RunsToCompletion()
        {
            GameHarness harness = PlayWithHuman(GameRules.Standard4Player(), 1001, out int decisions);

            Assert.IsNull(harness.ValidateEventStream());
            Assert.AreEqual(1, harness.CountOf<GameEnded>());
            Assert.Greater(decisions, 13, "The human should have been asked for a bid and thirteen cards a hand.");
        }

        [Test]
        public void TwoPlayerGameWithAHuman_RunsToCompletion()
        {
            GameHarness harness = PlayWithHuman(GameRules.Standard2Player(), 2002, out _);

            Assert.IsNull(harness.ValidateEventStream());
            Assert.AreEqual(1, harness.CountOf<GameEnded>());
        }

        [Test]
        public void HumanGames_CompleteAcrossManySeeds()
        {
            for (int seed = 0; seed < 60; seed++)
            {
                GameHarness four = PlayWithHuman(GameRules.Standard4Player(), seed, out _);
                Assert.IsNull(four.ValidateEventStream(), "4P seed " + seed);

                GameHarness two = PlayWithHuman(GameRules.Standard2Player(), seed, out _);
                Assert.IsNull(two.ValidateEventStream(), "2P seed " + seed);
            }
        }

        [Test]
        public void TheLoopIsNeverAskedToActWithAnEmptyMailboxAndNoRequest()
        {
            // The deadlock this guards against: the machine reports it is waiting, but no
            // controller was actually asked anything, so nothing can ever arrive.
            var human = new HumanPlayerController();
            var controllers = new IPlayerController[4];
            controllers[0] = human;
            for (int i = 1; i < 4; i++) controllers[i] = AiPlayerController.CreateDefault();

            var harness = new GameHarness(GameRules.Standard4Player(), 55, controllers);

            for (int step = 0; step < 5000 && !harness.Loop.IsGameOver; step++)
            {
                harness.Step();

                if (!harness.Loop.IsAwaitingInput) continue;

                Assert.IsTrue(human.PendingBid || human.PendingCard || human.PendingDraw,
                    "The loop parked without a pending request at step " + step + ".");

                Answer(human, harness);
            }
        }

        [Test]
        public void AdvanceIsIdempotent_WhileParkedOnAHuman()
        {
            // The presenter calls Advance every iteration whatever the state. Extra calls while a
            // human is thinking must be free rather than skipping their turn.
            var human = new HumanPlayerController();
            var controllers = new IPlayerController[4];
            controllers[0] = human;
            for (int i = 1; i < 4; i++) controllers[i] = AiPlayerController.CreateDefault();

            var harness = new GameHarness(GameRules.Standard4Player(), 91, controllers);

            while (!harness.Loop.IsAwaitingInput) harness.Step();

            int logSize = harness.Log.Count;
            for (int i = 0; i < 50; i++) harness.Step();

            Assert.AreEqual(logSize, harness.Log.Count);
            Assert.IsTrue(harness.Loop.IsAwaitingInput);
            Assert.IsTrue(human.PendingBid || human.PendingCard || human.PendingDraw);
        }
    }
}
