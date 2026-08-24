using System;

namespace Spades.Core.Util
{
    public sealed class SeededRandomSource : IRandomSource
    {
        private readonly Random _random;

        public SeededRandomSource(int seed)
        {
            Seed = seed;
            _random = new Random(seed);
        }

        /// <summary>Kept so a failing simulation run can report the seed that broke it.</summary>
        public int Seed { get; }

        public int Next(int maxExclusive)
        {
            if (maxExclusive <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxExclusive), "maxExclusive must be positive.");

            return _random.Next(maxExclusive);
        }
    }
}
