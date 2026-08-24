using System.Collections.Generic;
using NUnit.Framework;
using Spades.Core.Cards;
using Spades.Core.Util;

namespace Spades.Core.Tests
{
    public class DeckTests
    {
        [Test]
        public void CreateStandard_Has52Cards()
        {
            Assert.AreEqual(52, Deck.CreateStandard().Length);
        }

        [Test]
        public void CreateStandard_ContainsNoDuplicates()
        {
            // Doubles as a test of Card.Equals and Card.GetHashCode.
            var unique = new HashSet<Card>(Deck.CreateStandard());
            Assert.AreEqual(52, unique.Count);
        }

        [Test]
        public void CreateStandard_Has13CardsOfEachSuit()
        {
            Card[] deck = Deck.CreateStandard();

            foreach (Suit suit in new[] { Suit.Clubs, Suit.Diamonds, Suit.Hearts, Suit.Spades })
            {
                int count = 0;
                for (int i = 0; i < deck.Length; i++)
                {
                    if (deck[i].Suit == suit) count++;
                }

                Assert.AreEqual(13, count, "Wrong number of " + suit);
            }
        }

        [Test]
        public void Shuffle_PreservesTheExactSetOfCards()
        {
            // Catches a broken swap that drops or duplicates a card.
            Card[] deck = Deck.CreateStandard();
            var before = new HashSet<Card>(deck);

            Deck.Shuffle(deck, new SeededRandomSource(99));

            Assert.AreEqual(52, deck.Length);
            CollectionAssert.AreEquivalent(before, new HashSet<Card>(deck));
        }

        [Test]
        public void Shuffle_WithSameSeed_ProducesIdenticalOrder()
        {
            // The determinism the whole test suite and the seeded simulation rest on.
            Card[] a = Deck.CreateStandard();
            Card[] b = Deck.CreateStandard();

            Deck.Shuffle(a, new SeededRandomSource(1234));
            Deck.Shuffle(b, new SeededRandomSource(1234));

            CollectionAssert.AreEqual(a, b);
        }

        [Test]
        public void Shuffle_WithDifferentSeeds_ProducesDifferentOrder()
        {
            Card[] a = Deck.CreateStandard();
            Card[] b = Deck.CreateStandard();

            Deck.Shuffle(a, new SeededRandomSource(1));
            Deck.Shuffle(b, new SeededRandomSource(2));

            CollectionAssert.AreNotEqual(a, b);
        }

        [Test]
        public void Shuffle_MovesMostCards()
        {
            // A shuffle that leaves the deck almost sorted would pass every test above.
            Card[] ordered = Deck.CreateStandard();
            Card[] shuffled = Deck.CreateStandard();
            Deck.Shuffle(shuffled, new SeededRandomSource(7));

            int inPlace = 0;
            for (int i = 0; i < ordered.Length; i++)
            {
                if (ordered[i] == shuffled[i]) inPlace++;
            }

            // A uniform shuffle leaves about one card in place on average; five is a generous
            // ceiling that still fails instantly for an identity or near-identity permutation.
            Assert.Less(inPlace, 5, "Too many cards stayed where they started.");
        }
    }
}
