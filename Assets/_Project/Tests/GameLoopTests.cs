using System.Collections.Generic;
using NUnit.Framework;
using Spades.Core.Cards;
using Spades.Core.Commands;
using Spades.Core.Events;
using Spades.Core.Flow;
using Spades.Core.Players;
using Spades.Core.Rules;
using Spades.Core.State;

namespace Spades.Core.Tests
{
    public class GameLoopTests
    {
        private static IPlayerController[] Bots(int count)
        {
            var controllers = new IPlayerController[count];
            for (int i = 0; i < count; i++) controllers[i] = AiPlayerController.CreateDefault();
            return controllers;
        }

        /// <summary>A table of humans, so a test can script every decision itself.</summary>
        private static HumanPlayerController[] Humans(int count)
        {
            var controllers = new HumanPlayerController[count];
            for (int i = 0; i < count; i++) controllers[i] = new HumanPlayerController();
            return controllers;
        }

        [Test]
        public void FirstAdvance_DealsThirteenCardsToEverySeat()
        {
            var harness = GameHarness.AllBots(GameRules.Standard4Player(), seed: 1);

            harness.Step();

            Assert.AreEqual(GamePhase.Bidding, harness.Loop.Phase);
            Assert.AreEqual(4, harness.CountOf<HandDealt>());

            foreach (HandDealt dealt in harness.AllOf<HandDealt>())
            {
                Assert.AreEqual(13, dealt.Cards.Count, dealt.Seat + " was not dealt thirteen cards.");
            }
        }

        [Test]
        public void EveryDealtCard_IsDistinct()
        {
            var harness = GameHarness.AllBots(GameRules.Standard4Player(), seed: 5);
            harness.Step();

            var seen = new HashSet<Card>();
            foreach (HandDealt dealt in harness.AllOf<HandDealt>())
            {
                foreach (Card card in dealt.Cards)
                {
                    Assert.IsTrue(seen.Add(card), card + " was dealt twice.");
                }
            }

            Assert.AreEqual(52, seen.Count);
        }

        [Test]
        public void Advance_NeverPlaysMoreThanOneCardPerCall()
        {
            // The regression guard for the re-entrancy trap: if a controller ever executed
            // instead of enqueuing, one Advance would run through an entire hand.
            var harness = GameHarness.AllBots(GameRules.Standard4Player(), seed: 11);

            for (int step = 0; step < 400 && harness.Loop.Phase != GamePhase.HandComplete; step++)
            {
                int before = harness.Log.Count;
                harness.Step();

                int played = 0;
                for (int i = before; i < harness.Log.Count; i++)
                {
                    if (harness.Log[i] is CardPlayed) played++;
                }

                Assert.LessOrEqual(played, 1, "Advance() emitted " + played + " CardPlayed events.");
            }
        }

        [Test]
        public void OneHand_PlaysThirteenTricksAndScoresBothTeams()
        {
            var harness = GameHarness.AllBots(GameRules.Standard4Player(), seed: 3);

            while (harness.CountOf<HandScored>() == 0)
            {
                harness.Step();
            }

            Assert.AreEqual(13, harness.CountOf<TrickWon>());
            Assert.AreEqual(52, harness.CountOf<CardPlayed>());

            HandScored scored = harness.AllOf<HandScored>()[0];
            Assert.AreEqual(2, scored.Lines.Count);
        }

        [Test]
        public void PhasesFollowTheDeclaredOrder()
        {
            var harness = GameHarness.AllBots(GameRules.Standard4Player(), seed: 8);
            var phases = new List<GamePhase>();

            for (int i = 0; i < 400 && harness.CountOf<HandScored>() == 0; i++)
            {
                if (phases.Count == 0 || phases[phases.Count - 1] != harness.Loop.Phase)
                    phases.Add(harness.Loop.Phase);

                harness.Step();
            }

            CollectionAssert.AreEqual(
                new[] { GamePhase.Dealing, GamePhase.Bidding, GamePhase.Playing, GamePhase.HandComplete },
                phases);
        }

        [Test]
        public void DealerRotates_AfterEachHand()
        {
            var harness = GameHarness.AllBots(GameRules.Standard4Player(), seed: 21);

            Seat firstDealer = harness.Loop.Dealer;
            while (harness.CountOf<HandScored>() == 0) harness.Step();

            // One more step turns the scored hand into the next deal.
            harness.Step();

            Assert.AreEqual(firstDealer.Next(4), harness.Loop.Dealer);
        }

        [Test]
        public void LoopParks_WhenAHumanSeatIsAsked()
        {
            var controllers = Bots(4);
            var human = new HumanPlayerController();
            controllers[1] = human;   // seat 1 acts first when seat 0 deals

            var harness = new GameHarness(GameRules.Standard4Player(), 4, controllers);

            harness.Step();   // deal, then ask seat 1 to bid
            harness.Step();

            Assert.IsTrue(harness.Loop.IsAwaitingInput);
            Assert.AreEqual(new Seat(1), harness.Loop.AwaitingSeat);
            Assert.IsTrue(human.PendingBid);

            // The machine must not drift forward on its own while a human is thinking.
            int logSize = harness.Log.Count;
            for (int i = 0; i < 20; i++) harness.Step();

            Assert.AreEqual(logSize, harness.Log.Count, "The loop advanced without an answer.");
            Assert.AreEqual(GamePhase.Bidding, harness.Loop.Phase);

            human.SubmitBid(3);
            harness.Step();

            Assert.AreEqual(1, harness.CountOf<BidPlaced>());
        }

        [Test]
        public void IllegalCard_IsRejectedWithAReason()
        {
            // Search for a deal in which the leader holds both spades and non-spades, so that
            // "cannot lead a spade before they are broken" actually excludes something. Any
            // realistic deal qualifies; searching rather than hard-coding a seed keeps the test
            // from silently going vacuous if the shuffle ever changes.
            GameHarness harness = null;
            HumanPlayerController controller = null;
            Seat leader = default;

            for (int seed = 0; seed < 100 && controller == null; seed++)
            {
                HumanPlayerController[] humans = Humans(4);
                var candidate = new GameHarness(GameRules.Standard4Player(), seed, humans);

                candidate.Step();   // deal and start bidding

                for (int i = 0; i < 4; i++)
                {
                    candidate.Step();
                    humans[candidate.Loop.AwaitingSeat.Index].SubmitBid(3);
                    candidate.Step();
                }

                Assert.AreEqual(GamePhase.Playing, candidate.Loop.Phase);

                candidate.Step();   // ask the leader for a card
                Seat seat = candidate.Loop.AwaitingSeat;
                HumanPlayerController seatController = humans[seat.Index];

                if (seatController.LegalMoves.Count < candidate.Loop.ViewFor(seat).Hand.Count)
                {
                    harness = candidate;
                    controller = seatController;
                    leader = seat;
                }
            }

            Assert.NotNull(controller, "No deal in the first hundred seeds restricted the leader.");

            IReadOnlyList<Card> hand = harness.Loop.ViewFor(leader).Hand;
            int rejected = 0;

            // Every card the validator excluded must also be refused by the loop, with a reason.
            foreach (Card card in hand)
            {
                if (controller.IsLegal(card)) continue;

                bool accepted = harness.Loop.TrySubmit(new PlayCardCommand(leader, card), out string reason);

                Assert.IsFalse(accepted, card + " should have been rejected.");
                Assert.IsNotEmpty(reason);
                Assert.IsFalse(controller.SubmitCard(card), "The controller should refuse it too.");
                rejected++;
            }

            Assert.Greater(rejected, 0);
        }

        [Test]
        public void CommandFromTheWrongSeat_IsRejected()
        {
            // Human seats, so the loop is genuinely parked with an empty mailbox: a bot would
            // have answered synchronously and the rejection would be "already queued" instead.
            var harness = new GameHarness(GameRules.Standard4Player(), 2, Humans(4));
            harness.Step();   // deal
            harness.Step();   // ask the first seat to bid

            Seat wrong = harness.Loop.AwaitingSeat.Next(4);
            bool accepted = harness.Loop.TrySubmit(new PlaceBidCommand(wrong, 3), out string reason);

            Assert.IsFalse(accepted);
            StringAssert.Contains("turn", reason);
        }

        [Test]
        public void BidAboveTheHandSize_IsRejected()
        {
            var harness = new GameHarness(GameRules.Standard4Player(), 2, Humans(4));
            harness.Step();
            harness.Step();

            Seat seat = harness.Loop.AwaitingSeat;
            Assert.IsFalse(harness.Loop.TrySubmit(new PlaceBidCommand(seat, 14), out string reason));
            StringAssert.Contains("between 0 and 13", reason);
        }

        [Test]
        public void TwoPlayerDrawPhase_GivesEachPlayerThirteenCards()
        {
            var harness = GameHarness.AllBots(GameRules.Standard2Player(), seed: 6);

            harness.Step();
            Assert.AreEqual(GamePhase.Drawing, harness.Loop.Phase);

            while (harness.Loop.Phase == GamePhase.Drawing) harness.Step();

            Assert.AreEqual(GamePhase.Bidding, harness.Loop.Phase);
            Assert.AreEqual(2, harness.CountOf<HandDealt>());

            foreach (HandDealt dealt in harness.AllOf<HandDealt>())
            {
                Assert.AreEqual(13, dealt.Cards.Count);
            }

            // Twenty-six draw turns, each of which put exactly one card into a hand.
            Assert.AreEqual(26, harness.CountOf<CardDrawn>());
        }

        [Test]
        public void TwoPlayerDrawPhase_NeverRepeatsACard()
        {
            var harness = GameHarness.AllBots(GameRules.Standard2Player(), seed: 30);
            harness.Step();
            while (harness.Loop.Phase == GamePhase.Drawing) harness.Step();

            var seen = new HashSet<Card>();
            foreach (CardDrawn drawn in harness.AllOf<CardDrawn>())
            {
                Assert.IsTrue(seen.Add(drawn.Card), drawn.Card + " entered a hand twice.");
                if (!drawn.Kept) Assert.IsTrue(seen.Add(drawn.Discarded), drawn.Discarded + " reappeared.");
            }
        }
    }
}
