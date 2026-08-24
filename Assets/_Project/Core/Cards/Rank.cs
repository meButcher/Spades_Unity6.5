namespace Spades.Core.Cards
{
    /// <summary>
    /// Card ranks. The values are the real rank numbers, so (int)rank IS the rank and
    /// every rank comparison in the engine is a direct integer compare with no lookup.
    /// </summary>
    public enum Rank : byte
    {
        Two = 2,
        Three,
        Four,
        Five,
        Six,
        Seven,
        Eight,
        Nine,
        Ten,    // 10
        Jack,   // 11
        Queen,  // 12
        King,   // 13
        Ace     // 14
    }
}
