using System.Collections.Generic;
using NUnit.Framework;
using Spades.Core.Ai;
using Spades.Core.Cards;
using Spades.Core.Rules;
using Spades.Core.State;
using Spades.Core.Util;

namespace Spades.Core.Tests
{
    public class BiddingTests
    {
        private HeuristicBiddingStrategy _strategy;
        private GameRules _rules;

        [SetUp]
        public void SetUp()
        {
            _strategy = new HeuristicBiddingStrategy();
            _rules = GameRules.Standard4Player();
        }

        private static Card C(Suit suit, Rank rank) => new Card(suit, rank);

        /// <summary>Builds the minimum SeatView a bidding strategy actually reads.</summary>
        private static SeatView ViewWith(GameRules rules, IReadOnlyList<Card> hand)
        {
            var bids = new int[rules.PlayerCount];
            var tricks = new int[rules.PlayerCount];
            var counts = new int[rules.PlayerCount];
            for (int i = 0; i < bids.Length; i++) bids[i] = SeatState.NoBid;
            counts[0] = hand.Count;

            return new SeatView(
                seat: new Seat(0),
                teamId: 0,
                rules: rules,
                dealer: new Seat(rules.PlayerCount - 1),
                hand: hand,
                bids: bids,
                tricksWon: tricks,
                handCounts: counts,
                currentTrick: null,
                playedCards: new Card[0],
                spadesBroken: false,
                scores: new ScoreSnapshot(new int[rules.TeamCount], new int[rules.TeamCount]));
        }

        [Test]
        public void StrongHand_BidsAtLeastFour()
        {
            // Ace, king and queen of spades plus two side aces.
            var hand = new List<Card>
            {
                C(Suit.Spades, Rank.Ace), C(Suit.Spades, Rank.King), C(Suit.Spades, Rank.Queen),
                C(Suit.Hearts, Rank.Ace), C(Suit.Hearts, Rank.Seven), C(Suit.Hearts, Rank.Five), C(Suit.Hearts, Rank.Three),
                C(Suit.Diamonds, Rank.Ace), C(Suit.Diamonds, Rank.Nine), C(Suit.Diamonds, Rank.Six), C(Suit.Diamonds, Rank.Two),
                C(Suit.Clubs, Rank.Eight), C(Suit.Clubs, Rank.Four)
            };

            int bid = _strategy.ChooseBid(ViewWith(_rules, hand), _rules);

            Assert.GreaterOrEqual(bid, 4, "Three top spades and two aces is at least a four bid.");
        }

        [Test]
        public void WeakHand_WithNoHonoursAndNoVoid_BidsNil()
        {
            var hand = new List<Card>
            {
                C(Suit.Spades, Rank.Two), C(Suit.Spades, Rank.Three),
                C(Suit.Hearts, Rank.Two), C(Suit.Hearts, Rank.Three), C(Suit.Hearts, Rank.Four), C(Suit.Hearts, Rank.Five),
                C(Suit.Diamonds, Rank.Two), C(Suit.Diamonds, Rank.Three), C(Suit.Diamonds, Rank.Four), C(Suit.Diamonds, Rank.Five),
                C(Suit.Clubs, Rank.Two), C(Suit.Clubs, Rank.Three), C(Suit.Clubs, Rank.Four)
            };

            Assert.AreEqual(0, _strategy.ChooseBid(ViewWith(_rules, hand), _rules));
        }

        [Test]
        public void HandWithAHighSpade_NeverBidsNil()
        {
            // A singleton king of spades scores nothing (no length behind it), so the raw count
            // rounds to zero, but going Nil holding it is how a naive bot throws a hundred points.
            var hand = new List<Card>
            {
                C(Suit.Spades, Rank.King),
                C(Suit.Hearts, Rank.Two), C(Suit.Hearts, Rank.Three), C(Suit.Hearts, Rank.Four), C(Suit.Hearts, Rank.Five),
                C(Suit.Diamonds, Rank.Two), C(Suit.Diamonds, Rank.Three), C(Suit.Diamonds, Rank.Four), C(Suit.Diamonds, Rank.Five),
                C(Suit.Clubs, Rank.Two), C(Suit.Clubs, Rank.Three), C(Suit.Clubs, Rank.Four), C(Suit.Clubs, Rank.Five)
            };

            Assert.AreEqual(1, _strategy.ChooseBid(ViewWith(_rules, hand), _rules));
        }

        [Test]
        public void NilIsNeverBid_WhenTheRulesDisallowIt()
        {
            var noNil = new GameRules(4, 13, 500, 10, -100, 100, allowNil: false, usesDrawPhase: false);

            var hand = new List<Card>
            {
                C(Suit.Spades, Rank.Two), C(Suit.Spades, Rank.Three),
                C(Suit.Hearts, Rank.Two), C(Suit.Hearts, Rank.Three), C(Suit.Hearts, Rank.Four), C(Suit.Hearts, Rank.Five),
                C(Suit.Diamonds, Rank.Two), C(Suit.Diamonds, Rank.Three), C(Suit.Diamonds, Rank.Four), C(Suit.Diamonds, Rank.Five),
                C(Suit.Clubs, Rank.Two), C(Suit.Clubs, Rank.Three), C(Suit.Clubs, Rank.Four)
            };

            Assert.AreEqual(1, _strategy.ChooseBid(ViewWith(noNil, hand), noNil));
        }

        [Test]
        public void BidIsAlwaysWithinRange_AcrossManyRandomHands()
        {
            for (int seed = 0; seed < 500; seed++)
            {
                Card[] deck = Deck.CreateStandard();
                Deck.Shuffle(deck, new SeededRandomSource(seed));

                var hand = new List<Card>(13);
                for (int i = 0; i < 13; i++) hand.Add(deck[i]);

                int bid = _strategy.ChooseBid(ViewWith(_rules, hand), _rules);

                Assert.GreaterOrEqual(bid, 0, "seed " + seed);
                Assert.LessOrEqual(bid, _rules.HandSize, "seed " + seed);
            }
        }

        [Test]
        public void BidsAreDeterministic_ForTheSameHand()
        {
            Card[] deck = Deck.CreateStandard();
            Deck.Shuffle(deck, new SeededRandomSource(42));

            var hand = new List<Card>(13);
            for (int i = 0; i < 13; i++) hand.Add(deck[i]);

            SeatView view = ViewWith(_rules, hand);
            Assert.AreEqual(_strategy.ChooseBid(view, _rules), _strategy.ChooseBid(view, _rules));
        }
    }
}
