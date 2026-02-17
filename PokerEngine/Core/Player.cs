using System;
using System.Collections.Generic;

// File: Player.cs
// Purpose: Represents a participant in a poker session with identity and stack data.
// Responsible for: Tracking player-specific state (chips, seat, status) that feeds into GameState.
// Not responsible for: Turn logic, action validation, UI input, or networking identity management.
// Fit: Serves as the domain entity referenced by actions, state transitions, and pot distribution.

namespace PokerEngine.Core
{
    /// <summary>
    /// Domain entity describing a player; designed to integrate with GameState and action processing.
    /// </summary>
    public class Player
    {
        private readonly List<Card> _holeCards = new(2);

        public Player(Guid id, string name, int seatIndex, decimal stack)
        {
            if (seatIndex < 0) throw new ArgumentOutOfRangeException(nameof(seatIndex));
            if (stack < 0) throw new ArgumentOutOfRangeException(nameof(stack));

            Id = id;
            Name = name;
            SeatIndex = seatIndex;
            Stack = stack;
        }

        public Guid Id { get; }

        public string Name { get; }

        public int SeatIndex { get; }

        public decimal Stack { get; private set; }

        public bool IsFolded { get; private set; }

        public bool IsAllIn => Stack <= 0 && !IsFolded;

        public IReadOnlyList<Card> HoleCards => _holeCards;

        public void GiveHoleCards(Card first, Card second)
        {
            _holeCards.Clear();
            _holeCards.Add(first);
            _holeCards.Add(second);
        }

        public void ResetForNewHand()
        {
            IsFolded = false;
            _holeCards.Clear();
        }

        public decimal CommitChips(decimal amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            var committed = Math.Min(amount, Stack);
            Stack -= committed;
            return committed;
        }

        public void Fold() => IsFolded = true;

        public bool IsActive => !IsFolded && Stack > 0;
        
        public void ReceivePayout(decimal amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            Stack += amount;
        }
    }
}
