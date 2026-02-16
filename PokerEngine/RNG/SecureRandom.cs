using System;
using System.Security.Cryptography;

// File: SecureRandom.cs
// Purpose: Provides cryptographically strong random numbers for shuffling and seeding.
// Responsible for: Centralizing entropy generation so gameplay randomness can be audited and seeded when needed.
// Not responsible for: Shuffling algorithms (handled by ShuffleService) or any game logic.
// Fit: Only approved source of randomness for the engine to maintain determinism and security.

namespace PokerEngine.RNG
{
    /// <summary>
    /// Cryptographically secure RNG wrapper intended for deck seeding and any stochastic operations.
    /// </summary>
    public sealed class SecureRandom : IDisposable
    {
        private readonly RandomNumberGenerator _rng = RandomNumberGenerator.Create();
        private bool _disposed;

        public int NextInt(int maxExclusive)
        {
            if (maxExclusive <= 0) throw new ArgumentOutOfRangeException(nameof(maxExclusive));
            Span<byte> buffer = stackalloc byte[4];
            _rng.GetBytes(buffer);
            var value = BitConverter.ToUInt32(buffer);
            return (int)(value % (uint)maxExclusive);
        }

        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (minInclusive >= maxExclusive) throw new ArgumentOutOfRangeException(nameof(maxExclusive));
            var range = maxExclusive - minInclusive;
            return minInclusive + NextInt(range);
        }

        public void FillBytes(Span<byte> destination) => _rng.GetBytes(destination);

        public void Dispose()
        {
            if (_disposed) return;
            _rng.Dispose();
            _disposed = true;
        }
    }
}
