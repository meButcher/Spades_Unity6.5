using System;

namespace Spades.Core.Cards
{
    /// <summary>
    /// An immutable playing card.
    ///
    /// A readonly struct rather than a class: there are only 52 distinct values, they are
    /// value identities (the seven of diamonds IS the seven of diamonds), and they are
    /// copied constantly inside the AI's evaluation loops. A struct means no heap
    /// allocation and no GC pressure on the hot path.
    /// </summary>
    public readonly struct Card : IEquatable<Card>
    {
        // "\u2663\u2666\u2665\u2660" == club, diamond, heart, spade.
        private static readonly string[] SuitGlyphs = { "\u2663", "\u2666", "\u2665", "\u2660" };

        private static readonly string[] RankGlyphs =
        {
            "", "",                                       // indices 0 and 1 are unused
            "2", "3", "4", "5", "6", "7", "8", "9", "10",
            "J", "Q", "K", "A"
        };

        public Suit Suit { get; }
        public Rank Rank { get; }

        public Card(Suit suit, Rank rank)
        {
            Suit = suit;
            Rank = rank;
        }

        public bool IsSpade => Suit == Suit.Spades;

        /// <summary>Hearts and diamonds are printed red; clubs and spades black.</summary>
        public bool IsRedSuit => Suit == Suit.Hearts || Suit == Suit.Diamonds;

        public string SuitGlyph => SuitGlyphs[(int)Suit];
        public string RankGlyph => RankGlyphs[(int)Rank];

        public bool Equals(Card other) => Suit == other.Suit && Rank == other.Rank;

        public override bool Equals(object obj) => obj is Card other && Equals(other);

        /// <summary>
        /// A perfect hash. Rank never exceeds 14, so it fits in four bits; shifting the
        /// suit above it produces 52 distinct values in the range 0..63 with no collisions.
        /// </summary>
        public override int GetHashCode() => ((int)Suit << 4) | (int)Rank;

        public static bool operator ==(Card a, Card b) => a.Equals(b);
        public static bool operator !=(Card a, Card b) => !a.Equals(b);

        public override string ToString() => RankGlyph + SuitGlyph;
    }
}
