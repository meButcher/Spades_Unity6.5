using System;
using Spades.Core.Util;

namespace Spades.Core.Cards
{
    public static class Deck
    {
        public const int StandardSize = 52;

        // Explicit arrays rather than Enum.GetValues: no reflection and no boxing, and the
        // canonical deck order is defined here instead of by enum declaration order.
        private static readonly Suit[] AllSuits =
        {
            Suit.Clubs, Suit.Diamonds, Suit.Hearts, Suit.Spades
        };

        private static readonly Rank[] AllRanks =
        {
            Rank.Two, Rank.Three, Rank.Four, Rank.Five, Rank.Six, Rank.Seven,
            Rank.Eight, Rank.Nine, Rank.Ten, Rank.Jack, Rank.Queen, Rank.King, Rank.Ace
        };

        public static Card[] CreateStandard()
        {
            var cards = new Card[StandardSize];
            int i = 0;

            for (int s = 0; s < AllSuits.Length; s++)
            {
                for (int r = 0; r < AllRanks.Length; r++)
                {
                    cards[i++] = new Card(AllSuits[s], AllRanks[r]);
                }
            }

            return cards;
        }

        /// <summary>
        /// In-place Fisher-Yates. One array for the whole game, zero garbage per shuffle,
        /// and provably uniform.
        ///
        /// Note rng.Next(i + 1) rather than rng.Next(i): index i must be able to receive its
        /// own card. The off-by-one version never leaves a card in place and is measurably
        /// biased, but looks correct at a glance.
        /// </summary>
        public static void Shuffle(Card[] cards, IRandomSource rng)
        {
            if (cards == null) throw new ArgumentNullException(nameof(cards));
            if (rng == null) throw new ArgumentNullException(nameof(rng));

            for (int i = cards.Length - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                Card temp = cards[i];
                cards[i] = cards[j];
                cards[j] = temp;
            }
        }
    }
}
