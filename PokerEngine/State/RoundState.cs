using System;
using System.Collections.Generic;

// File: RoundState.cs
// Purpose: Captures transient data for the current betting round within a hand.
// Responsible for: Tracking current bets, player positions, and flags needed to determine when a round closes.
// Not responsible for: Overall session data (GameState) or pot distribution (PotManager handles chip movements).
// Fit: Round-scoped data that TurnManager and RoundManager consult to manage intra-hand flow.

namespace PokerEngine.State
{
    /// <summary>
    /// Holds per-round context to coordinate betting progress and readiness for phase advancement.
    /// </summary>
    public class RoundState
    {
        private readonly Dictionary<Guid, decimal> _contributions = new();
        private readonly HashSet<Guid> _contestingPlayers = new();
        private readonly HashSet<Guid> _playersActedThisRound = new();

        public decimal CurrentBet { get; private set; }

        public int? LastAggressorSeat { get; private set; }

        public decimal LastRaiseAmount { get; private set; }

        public IReadOnlyDictionary<Guid, decimal> Contributions => _contributions;

        public IReadOnlyCollection<Guid> ContestingPlayers => _contestingPlayers;

        public IReadOnlyCollection<Guid> PlayersActedThisRound => _playersActedThisRound;

        public bool CanClose { get; private set; }

        public void ResetForNewRound(IEnumerable<Guid> activePlayerIds)
        {
            _contributions.Clear();
            _contestingPlayers.Clear();
            _playersActedThisRound.Clear();
            foreach (var id in activePlayerIds)
            {
                _contestingPlayers.Add(id);
                _contributions[id] = 0m;
            }
            CurrentBet = 0m;
            LastAggressorSeat = null;
            LastRaiseAmount = 0m;
            CanClose = false;
        }

        public void RecordContribution(Guid playerId, decimal amount)
        {
            if (!_contributions.ContainsKey(playerId))
            {
                _contributions[playerId] = 0m;
            }
            _contributions[playerId] += amount;
        }

        public void SetCurrentBet(decimal betSize, int aggressorSeat, decimal raiseAmount)
        {
            CurrentBet = betSize;
            LastAggressorSeat = aggressorSeat;
            LastRaiseAmount = raiseAmount;
            CanClose = false;
            // When someone bets/raises, reset acted tracking - everyone needs to respond
            _playersActedThisRound.Clear();
        }

        public void MarkActed(Guid playerId) => _playersActedThisRound.Add(playerId);

        public bool HasActed(Guid playerId) => _playersActedThisRound.Contains(playerId);

        public void MarkFold(Guid playerId) => _contestingPlayers.Remove(playerId);

        public void MarkReadyToClose() => CanClose = true;

        public decimal GetContribution(Guid playerId)
        {
            return _contributions.TryGetValue(playerId, out var amount) ? amount : 0m;
        }
    }
}
