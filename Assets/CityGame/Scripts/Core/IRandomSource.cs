using System;

namespace CityGame.Core
{
    /// <summary>Injected randomness so the simulation is deterministic under test.</summary>
    public interface IRandomSource
    {
        /// <summary>Returns a value in [0, 1).</summary>
        double NextDouble();
    }

    /// <summary>Seeded <see cref="System.Random"/> wrapper.</summary>
    public sealed class SystemRandomSource : IRandomSource
    {
        private readonly Random random;

        /// <summary>Creates a source with a fixed seed.</summary>
        public SystemRandomSource(int seed)
        {
            random = new Random(seed);
        }

        /// <inheritdoc />
        public double NextDouble()
        {
            return random.NextDouble();
        }
    }
}
