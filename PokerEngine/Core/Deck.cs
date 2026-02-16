using System;
using System.Collections.Generic;
using System.Linq;
using PokerEngine.RNG;

// File: Deck.cs
// Purpose: Models a standard 52-card deck lifecycle for a hand.
// Responsible for: Holding an ordered collection of Card instances and exposing draw operations.
// Not responsible for: Randomness (delegated to ShuffleService), betting logic, or player state.
// Fit: Supplies cards to GameEngine and RoundManager while preserving deterministic ordering.

namespace PokerEngine.Core
{
    /// <summary>
    /// Represents a standard deck; intended to be constructed, shuffled externally, and consumed by the engine.
    /// </summary>
    public class Deck
    {
        private readonly List<Card> _cards;
        private int _position;

        public Deck(IEnumerable<Card> orderedCards)
        {
            _cards = orderedCards.ToList();
            if (_cards.Count == 0)
            {
                throw new ArgumentException("Deck cannot be empty", nameof(orderedCards));
            }
            _position = 0;
        }

        public static Deck CreateStandard(SecureRandom rng, ShuffleService shuffleService)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            if (shuffleService == null) throw new ArgumentNullException(nameof(shuffleService));

            var cards = new List<Card>(52);
            foreach (Suit suit in Enum.GetValues(typeof(Suit)))
            {
                foreach (Rank rank in Enum.GetValues(typeof(Rank)))
                {
                    cards.Add(new Card(rank, suit));
                }
            }

            shuffleService.ShuffleInPlace(cards, rng);
            return new Deck(cards);
        }

        public IReadOnlyList<Card> Cards => _cards;

        public bool HasCards(int count = 1) => _position + count <= _cards.Count;

        public Card Draw()
        {
            if (!HasCards())
            {
                throw new InvalidOperationException("Deck exhausted");
            }

            var card = _cards[_position];
            _position++;
            return card;
        }

        public IReadOnlyList<Card> Draw(int count)
        {
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            if (!HasCards(count))
            {
                throw new InvalidOperationException("Not enough cards to draw");
            }

            var slice = _cards.Skip(_position).Take(count).ToArray();
            _position += count;
            return slice;
        }

        public void Burn()
        {
            if (!HasCards())
            {
                throw new InvalidOperationException("Deck exhausted; cannot burn");
            }
            _position++;
        }

        /// <summary>
        /// Resets the deck position to start, effectively making all cards available again.
        /// Used for starting a new hand with the same shuffled deck.
        /// </summary>
        public void Reset()
        {
            _position = 0;
        }

        /// <summary>
        /// Resets and reshuffles the deck for a new hand.
        /// </summary>
        public void ResetAndShuffle(SecureRandom rng, ShuffleService shuffleService)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            if (shuffleService == null) throw new ArgumentNullException(nameof(shuffleService));
            
            _position = 0;
            shuffleService.ShuffleInPlace(_cards, rng);
        }
    }
}
