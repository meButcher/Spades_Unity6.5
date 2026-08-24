using System.Collections.Generic;
using NUnit.Framework;
using Spades.Core.Cards;
using Spades.Core.Rules;
using Spades.Core.State;

namespace Spades.Core.Tests
{
    public class LegalMoveValidatorTests
    {
        private static Card C(Suit suit, Rank rank) => new Card(suit, rank);

        private static List<Card> Hand(params Card[] cards) => new List<Card>(cards);

        private static TrickState EmptyTrick() => new TrickState(4, new Seat(0));

        private static TrickState TrickLedWith(Card card)
        {
            var trick = new TrickState(4, new Seat(0));
            trick.Add(new Seat(0), card);
            return trick;
        }

        [Test]
        public void MustFollowLedSuit_WhenHoldingIt()
        {
            List<Card> hand = Hand(
                C(Suit.Hearts, Rank.Four),
                C(Suit.Clubs, Rank.King));

            TrickState trick = TrickLedWith(C(Suit.Hearts, Rank.Nine));

            Assert.IsTrue(LegalMoveValidator.IsLegal(C(Suit.Hearts, Rank.Four), hand, trick, false));
            Assert.IsFalse(LegalMoveValidator.IsLegal(C(Suit.Clubs, Rank.King), hand, trick, false));
        }

        [Test]
        public void AnyCardIsLegal_WhenVoidInLedSuit()
        {
            List<Card> hand = Hand(
                C(Suit.Clubs, Rank.Two),
                C(Suit.Spades, Rank.Ace));

            TrickState trick = TrickLedWith(C(Suit.Hearts, Rank.Nine));

            Assert.IsTrue(LegalMoveValidator.IsLegal(C(Suit.Clubs, Rank.Two), hand, trick, false));
            Assert.IsTrue(LegalMoveValidator.IsLegal(C(Suit.Spades, Rank.Ace), hand, trick, false),
                "Trumping in when void is how spades get broken.");
        }

        [Test]
        public void CannotLeadSpade_BeforeSpadesAreBroken()
        {
            List<Card> hand = Hand(
                C(Suit.Spades, Rank.Ace),
                C(Suit.Hearts, Rank.Two));

            Assert.IsFalse(LegalMoveValidator.IsLegal(
                C(Suit.Spades, Rank.Ace), hand, EmptyTrick(), spadesBroken: false));
        }

        [Test]
        public void CanLeadSpade_AfterSpadesAreBroken()
        {
            List<Card> hand = Hand(
                C(Suit.Spades, Rank.Ace),
                C(Suit.Hearts, Rank.Two));

            Assert.IsTrue(LegalMoveValidator.IsLegal(
                C(Suit.Spades, Rank.Ace), hand, EmptyTrick(), spadesBroken: true));
        }

        [Test]
        public void CanLeadSpade_WhenHandContainsOnlySpades()
        {
            // The exception. Without it this hand has no legal move at all and the game deadlocks.
            List<Card> hand = Hand(
                C(Suit.Spades, Rank.Ace),
                C(Suit.Spades, Rank.Three));

            Assert.IsTrue(LegalMoveValidator.IsLegal(
                C(Suit.Spades, Rank.Ace), hand, EmptyTrick(), spadesBroken: false));
        }

        [Test]
        public void GetLegalMoves_ReturnsEveryCard_ForAnAllSpadeHandLeading()
        {
            List<Card> hand = Hand(
                C(Suit.Spades, Rank.Ace),
                C(Suit.Spades, Rank.Three));

            List<Card> legal = LegalMoveValidator.GetLegalMoves(hand, EmptyTrick(), spadesBroken: false);

            Assert.AreEqual(2, legal.Count);
        }

        [Test]
        public void GetLegalMoves_ReturnsOnlyTheLedSuit_WhenHoldingIt()
        {
            List<Card> hand = Hand(
                C(Suit.Hearts, Rank.Four),
                C(Suit.Hearts, Rank.King),
                C(Suit.Clubs, Rank.Two),
                C(Suit.Spades, Rank.Ten));

            List<Card> legal = LegalMoveValidator.GetLegalMoves(
                hand, TrickLedWith(C(Suit.Hearts, Rank.Nine)), spadesBroken: false);

            Assert.AreEqual(2, legal.Count);
            CollectionAssert.Contains(legal, C(Suit.Hearts, Rank.Four));
            CollectionAssert.Contains(legal, C(Suit.Hearts, Rank.King));
        }

        [Test]
        public void GetLegalMoves_AgreesWithIsLegal_ForEveryCardInHand()
        {
            // The guard against the two ever drifting apart, which is the bug where the UI lets
            // you click a card the engine then refuses.
            List<Card> hand = Hand(
                C(Suit.Hearts, Rank.Four),
                C(Suit.Hearts, Rank.King),
                C(Suit.Clubs, Rank.Two),
                C(Suit.Spades, Rank.Ten));

            TrickState trick = TrickLedWith(C(Suit.Hearts, Rank.Nine));
            List<Card> legal = LegalMoveValidator.GetLegalMoves(hand, trick, spadesBroken: false);

            foreach (Card card in hand)
            {
                bool isLegal = LegalMoveValidator.IsLegal(card, hand, trick, false);
                Assert.AreEqual(isLegal, legal.Contains(card), card + " disagreed");
            }
        }

        [Test]
        public void GetLegalMovesNonAlloc_MatchesTheAllocatingVersion()
        {
            List<Card> hand = Hand(
                C(Suit.Diamonds, Rank.Seven),
                C(Suit.Spades, Rank.Two),
                C(Suit.Clubs, Rank.Ace));

            TrickState trick = TrickLedWith(C(Suit.Hearts, Rank.Nine));

            List<Card> expected = LegalMoveValidator.GetLegalMoves(hand, trick, false);
            var actual = new List<Card>();
            LegalMoveValidator.GetLegalMovesNonAlloc(hand, trick, false, actual);

            CollectionAssert.AreEqual(expected, actual);
        }
    }
}
