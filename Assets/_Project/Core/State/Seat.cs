using System;

namespace Spades.Core.State
{
    /// <summary>
    /// A player position at the table. A typed wrapper over an int so a seat can never be
    /// silently passed where a bid, a trick count or a score was expected.
    /// </summary>
    public readonly struct Seat : IEquatable<Seat>
    {
        public int Index { get; }

        public Seat(int index)
        {
            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(index), "Seat index cannot be negative.");

            Index = index;
        }

        /// <summary>
        /// The next seat clockwise. Player count is a parameter rather than a constant, which
        /// is why 2-player and 4-player share every rotation in the engine unchanged.
        /// </summary>
        public Seat Next(int playerCount)
        {
            if (playerCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(playerCount), "Player count must be positive.");

            return new Seat((Index + 1) % playerCount);
        }

        public bool Equals(Seat other) => Index == other.Index;
        public override bool Equals(object obj) => obj is Seat other && Equals(other);
        public override int GetHashCode() => Index;

        public static bool operator ==(Seat a, Seat b) => a.Index == b.Index;
        public static bool operator !=(Seat a, Seat b) => a.Index != b.Index;

        // Deliberately not "Seat 0 (You)": the core does not know which seat a human occupies.
        // Display names belong to the presentation layer.
        public override string ToString() => "S" + Index;
    }
}
