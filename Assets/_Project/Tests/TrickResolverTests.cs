using System;
using NUnit.Framework;
using Spades.Core.Cards;
using Spades.Core.Rules;
using Spades.Core.State;

namespace Spades.Core.Tests
{
    public class TrickResolverTests
    {
        private static Card C(Suit suit, Rank rank) => new Card(suit, rank);

        /// <summary>Builds a trick in which seat i played cards[i], with seat 0 leading.</summary>
        private static TrickState Trick(params Card[] cards)
        {
            var trick = new TrickState(cards.Length, new Seat(0));
            for (int i = 0; i < cards.Length; i++) trick.Add(new Seat(i), cards[i]);
            return trick;
        }

        [Test]
        public void HighestOfLedSuit_Wins_WhenNoSpadesPlayed()
        {
            TrickState trick = Trick(
                C(Suit.Hearts, Rank.Five),
                C(Suit.Hearts, Rank.King),
                C(Suit.Hearts, Rank.Three),
                C(Suit.Hearts, Rank.Nine));

            Assert.AreEqual(new Seat(1), TrickResolver.DetermineWinner(trick));
        }

        [Test]
        public void AnySpade_Beats_AnyNonSpade()
        {
            // The two of spades takes a trick containing the ace of hearts.
            TrickState trick = Trick(
                C(Suit.Hearts, Rank.Five),
                C(Suit.Hearts, Rank.Ace),
                C(Suit.Spades, Rank.Two),
                C(Suit.Hearts, Rank.King));

            Assert.AreEqual(new Seat(2), TrickResolver.DetermineWinner(trick));
        }

        [Test]
        public void HighestSpade_Wins_WhenSeveralArePlayed()
        {
            TrickState trick = Trick(
                C(Suit.Hearts, Rank.Five),
                C(Suit.Spades, Rank.Two),
                C(Suit.Spades, Rank.Nine),
                C(Suit.Spades, Rank.Four));

            Assert.AreEqual(new Seat(2), TrickResolver.DetermineWinner(trick));
        }

        [Test]
        public void OffSuitDiscard_NeverWins_EvenWhenItOutranksTheLead()
        {
            // Clubs led with a five; an ace of diamonds is a discard and loses to it.
            TrickState trick = Trick(
                C(Suit.Clubs, Rank.Five),
                C(Suit.Diamonds, Rank.Ace),
                C(Suit.Clubs, Rank.Two),
                C(Suit.Diamonds, Rank.Three));

            Assert.AreEqual(new Seat(0), TrickResolver.DetermineWinner(trick));
        }

        [Test]
        public void SpadeLead_ResolvesByRank()
        {
            TrickState trick = Trick(
                C(Suit.Spades, Rank.Ten),
                C(Suit.Spades, Rank.Ace),
                C(Suit.Spades, Rank.Three),
                C(Suit.Spades, Rank.King));

            Assert.AreEqual(new Seat(1), TrickResolver.DetermineWinner(trick));
        }

        [Test]
        public void TwoPlayerTrick_Resolves()
        {
            var trick = new TrickState(2, new Seat(0));
            trick.Add(new Seat(0), C(Suit.Diamonds, Rank.Queen));
            trick.Add(new Seat(1), C(Suit.Diamonds, Rank.King));

            Assert.AreEqual(new Seat(1), TrickResolver.DetermineWinner(trick));
        }

        [Test]
        public void EmptyTrick_Throws()
        {
            var trick = new TrickState(4, new Seat(0));
            Assert.Throws<ArgumentException>(() => TrickResolver.DetermineWinner(trick));
        }
    }
}
