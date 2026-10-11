using System;

namespace SlotGame.Core
{
    /// <summary>
    /// Random Number Generator for the slot machine.
    ///
    /// Wraps <see cref="System.Random"/> with a weighted symbol table so that
    /// high-value symbols (Seven) appear less frequently than low-value ones
    /// (Cherry), exactly like a real weighted slot reel.
    ///
    /// Kept free of any UnityEngine dependency so the game logic can be
    /// unit-tested outside of the engine.
    /// </summary>
    public sealed class SlotRNG
    {
        /// <summary>Total number of distinct symbols.</summary>
        public static readonly int SymbolCount = Enum.GetValues(typeof(SymbolType)).Length;

        private readonly Random _random;

        // Cumulative weight table, e.g. weights {1,2,3,4} -> cumulative {1,3,6,10}.
        private readonly int[] _cumulativeWeights;
        private readonly int _totalWeight;

        /// <summary>
        /// Creates an RNG with custom per-symbol weights and an explicit seed.
        /// </summary>
        /// <param name="weights">
        /// Relative weight per symbol, indexed by (int)SymbolType.
        /// A higher weight means the symbol is rolled more often.
        /// </param>
        /// <param name="seed">Seed for reproducible results (useful in tests).</param>
        public SlotRNG(int[] weights, int seed)
        {
            if (weights == null || weights.Length != SymbolCount)
                throw new ArgumentException("Weights must contain exactly one entry per symbol.", nameof(weights));

            _cumulativeWeights = new int[SymbolCount];
            int running = 0;
            for (int i = 0; i < SymbolCount; i++)
            {
                if (weights[i] <= 0)
                    throw new ArgumentException("All weights must be positive.", nameof(weights));
                running += weights[i];
                _cumulativeWeights[i] = running;
            }
            _totalWeight = running;
            _random = new Random(seed);
        }

        /// <summary>
        /// Creates the default game RNG.
        /// Weights make Seven rare (jackpot should feel special) and Cherry common.
        /// The seed combines the tick count with a random GUID hash so two machines
        /// created in the same millisecond still produce different sequences.
        /// </summary>
        public static SlotRNG CreateDefault()
        {
            // Index order follows the SymbolType enum: Seven, Bar, Bell, Cherry.
            int[] weights = { 10, 20, 30, 40 };
            int seed = Environment.TickCount ^ Guid.NewGuid().GetHashCode();
            return new SlotRNG(weights, seed);
        }

        /// <summary>Rolls a single weighted random symbol.</summary>
        public SymbolType NextSymbol()
        {
            int roll = _random.Next(_totalWeight); // [0, totalWeight)
            for (int i = 0; i < _cumulativeWeights.Length; i++)
            {
                if (roll < _cumulativeWeights[i])
                    return (SymbolType)i;
            }
            // Unreachable, but keeps the compiler happy.
            return (SymbolType)(SymbolCount - 1);
        }

        /// <summary>Rolls an array of independent weighted random symbols.</summary>
        public SymbolType[] NextSymbols(int count)
        {
            var result = new SymbolType[count];
            for (int i = 0; i < count; i++)
                result[i] = NextSymbol();
            return result;
        }

        /// <summary>Returns a random float in [0, 1). Used for bonus chance rolls.</summary>
        public float NextFloat()
        {
            return (float)_random.NextDouble();
        }
    }
}
