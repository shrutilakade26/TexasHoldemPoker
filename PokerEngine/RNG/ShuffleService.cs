using System;
using System.Collections.Generic;

// File: ShuffleService.cs
// Purpose: Applies shuffling strategies to decks using SecureRandom-provided entropy.
// Responsible for: Producing deterministic, replayable shuffles when seeded; ensuring no other class manipulates deck order.
// Not responsible for: Generating randomness (SecureRandom), dealing cards, or managing game state.
// Fit: Bridge between RNG entropy and Core.Deck usage within the engine lifecycle.

namespace PokerEngine.RNG
{
    /// <summary>
    /// Encapsulates deck shuffling logic to keep randomness centralized and auditable.
    /// </summary>
    public sealed class ShuffleService
    {
        public void ShuffleInPlace(IList<Core.Card> cards, SecureRandom rng)
        {
            if (cards == null) throw new ArgumentNullException(nameof(cards));
            if (rng == null) throw new ArgumentNullException(nameof(rng));

            for (var i = cards.Count - 1; i > 0; i--)
            {
                var swapIndex = rng.NextInt(i + 1);
                (cards[i], cards[swapIndex]) = (cards[swapIndex], cards[i]);
            }
        }
    }
}
