using System;

// File: Card.cs
// Purpose: Defines the playing card value object used throughout the poker domain.
// Responsible for: Representing rank and suit in an immutable manner suitable for comparisons and evaluations.
// Not responsible for: Shuffling, dealing, player ownership, or UI representation.
// Fit: Core primitive shared by deck construction, hand evaluation, and state tracking.

namespace PokerEngine.Core
{
    /// <summary>
    /// Value object for a single playing card; intended to remain immutable once constructed.
    /// </summary>
    public readonly struct Card : IEquatable<Card>
    {
        public Card(Rank rank, Suit suit)
        {
            Rank = rank;
            Suit = suit;
        }

        public Rank Rank { get; }

        public Suit Suit { get; }

        public bool Equals(Card other) => Rank == other.Rank && Suit == other.Suit;

        public override bool Equals(object obj) => obj is Card other && Equals(other);

        public override int GetHashCode() => HashCode.Combine((int)Rank, (int)Suit);

        public override string ToString() => $"{Rank} of {Suit}";
    }

    public enum Suit
    {
        Clubs = 0,
        Diamonds = 1,
        Hearts = 2,
        Spades = 3
    }

    public enum Rank
    {
        Two = 2,
        Three = 3,
        Four = 4,
        Five = 5,
        Six = 6,
        Seven = 7,
        Eight = 8,
        Nine = 9,
        Ten = 10,
        Jack = 11,
        Queen = 12,
        King = 13,
        Ace = 14
    }
}
