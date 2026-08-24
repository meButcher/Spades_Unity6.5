namespace Spades.Core.Cards
{
    /// <summary>
    /// The four card suits. Spades is declared last so it is the highest enum value,
    /// which matches its role as the permanent trump suit.
    /// </summary>
    public enum Suit : byte
    {
        Clubs = 0,
        Diamonds = 1,
        Hearts = 2,
        Spades = 3
    }
}
