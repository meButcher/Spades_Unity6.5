namespace Spades.Core.Util
{
    /// <summary>
    /// The engine's only source of randomness, injected rather than static.
    ///
    /// Two payoffs: every test is deterministic, and any game a player reports as broken
    /// is reproducible from its seed alone. It is also exactly the seam a server-authoritative
    /// build needs so that every client agrees on the deal.
    /// </summary>
    public interface IRandomSource
    {
        int Next(int maxExclusive);
    }
}
