using System;
using System.Collections.Generic;
using System.Linq;
using PokerEngine.State;

// File: PotManager.cs
// Purpose: Encapsulates pot creation, contribution tracking, and payout calculation.
// Responsible for: Managing wagers, side pots, and distributing winnings post-showdown based on validated outcomes.
// Not responsible for: Determining winner rankings (delegated to hand evaluation) or validating actions (rules layer).
// Fit: Called by GameEngine during betting rounds and showdown to keep GameState pot data consistent.

namespace PokerEngine.Engine
{
    /// <summary>
    /// Maintains pots and applies chip movements, keeping betting history aligned with GameState invariants.
    /// </summary>
    internal class PotManager
    {
        public void ApplyContribution(GameState state, Guid playerId, decimal amount)
        {
            if (!state.TotalContributions.ContainsKey(playerId))
            {
                state.TotalContributions[playerId] = 0m;
            }
            state.TotalContributions[playerId] += amount;
        }

        public List<Pot> BuildPots(GameState state, IReadOnlyCollection<Guid> eligiblePlayers)
        {
            // Get all contributions > 0, sorted by amount
            var contributions = state.TotalContributions
                .Where(kv => kv.Value > 0)
                .OrderBy(kv => kv.Value)
                .ToList();

            if (contributions.Count == 0)
            {
                state.Pots.Clear();
                return new List<Pot>();
            }

            var pots = new List<Pot>();
            decimal previousLevel = 0m;

            // Build pots using standard side‑pot logic:
            // for each DISTINCT contribution level, create a pot slice funded
            // by all players whose total contribution reaches at least that level.
            var levels = contributions
                .Select(c => c.Value)
                .Distinct()
                .OrderBy(v => v);

            foreach (var currentLevel in levels)
            {
                var slice = currentLevel - previousLevel;
                if (slice <= 0)
                {
                    previousLevel = currentLevel;
                    continue;
                }

                // All players whose total contribution reaches this level
                var contributorsAtThisLevel = contributions
                    .Where(c => c.Value >= currentLevel)
                    .Select(c => c.Key)
                    .ToList();

                if (contributorsAtThisLevel.Count == 0)
                {
                    previousLevel = currentLevel;
                    continue;
                }

                var amount = slice * contributorsAtThisLevel.Count;

                // Eligible players for this pot: contributors who have not folded
                var eligibleForPot = contributorsAtThisLevel
                    .Where(id => eligiblePlayers.Contains(id))
                    .ToArray();

                if (eligibleForPot.Length > 0)
                {
                    pots.Add(new Pot(amount, eligibleForPot));
                }
                else if (amount > 0 && pots.Count > 0)
                {
                    // Dead money - roll into previous pot if possible
                    var lastPot = pots[^1];
                    pots[^1] = new Pot(lastPot.Amount + amount, lastPot.EligiblePlayers);
                }

                previousLevel = currentLevel;
            }

            state.Pots.Clear();
            state.Pots.AddRange(pots);
            return pots;
        }

        public Dictionary<Guid, decimal> Settle(GameState state, IReadOnlyDictionary<Guid, int> handRanks, IReadOnlyCollection<Guid> eligiblePlayers)
        {
            var pots = BuildPots(state, eligiblePlayers);
            var payouts = eligiblePlayers.ToDictionary(id => id, _ => 0m);

            foreach (var pot in pots)
            {
                var contenders = pot.EligiblePlayers.Where(handRanks.ContainsKey).ToList();
                if (contenders.Count == 0)
                {
                    continue;
                }

                var bestRank = contenders.Min(id => handRanks[id]);
                var winners = contenders.Where(id => handRanks[id] == bestRank).ToList();
                var share = pot.Amount / winners.Count;

                foreach (var winner in winners)
                {
                    payouts[winner] += share;
                }
            }

            foreach (var payout in payouts)
            {
                var player = state.GetPlayerById(payout.Key);
                player.ReceivePayout(payout.Value);
            }

            return payouts;
        }
    }

    /// <summary>
    /// Represents a discrete pot with a set of eligible players.
    /// </summary>
    public sealed class Pot
    {
        public Pot(decimal amount, IEnumerable<Guid> eligiblePlayers)
        {
            Amount = amount;
            EligiblePlayers = new HashSet<Guid>(eligiblePlayers);
        }

        public decimal Amount { get; }

        public HashSet<Guid> EligiblePlayers { get; }
    }
}
