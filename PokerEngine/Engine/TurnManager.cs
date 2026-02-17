using System;
using System.Collections.Generic;
using System.Linq;
using PokerEngine.State;

// File: TurnManager.cs
// Purpose: Enforces player turn order and manages action sequencing within a betting round.
// Responsible for: Determining the next actor, handling blinds/straddles kickoff, and ensuring rotations respect folds and all-ins.
// Not responsible for: Validating action legality (rules layer) or adjusting pots (PotManager handles chip movement).
// Fit: Serves GameEngine by providing deterministic turn advancement tied to current GameState and RoundState.

namespace PokerEngine.Engine
{
    /// <summary>
    /// Maintains orderly progression of player turns while signaling when a betting round can close.
    /// </summary>
    internal sealed class TurnManager
    {
        public int NextSeat(GameState state)
        {
            var seats = state.Players.Select(p => p.SeatIndex).OrderBy(s => s).ToList();
            if (seats.Count == 0) return state.CurrentSeatToAct;

            var currentIndex = seats.IndexOf(state.CurrentSeatToAct);
            if (currentIndex < 0)
            {
                currentIndex = 0;
            }
            for (var i = 1; i <= seats.Count; i++)
            {
                var seat = seats[(currentIndex + i) % seats.Count];
                var player = state.GetPlayerBySeat(seat);
                if (!player.IsFolded && !player.IsAllIn && player.Stack > 0)
                {
                    return seat;
                }
            }

            return state.CurrentSeatToAct;
        }

        public bool ShouldCloseRound(GameState state)
        {
            var round = state.RoundState;
            
            // Get active players who can still act (not folded, not all-in, have chips)
            var activePlayers = state.Players.Where(p => !p.IsFolded && !p.IsAllIn && p.Stack > 0).ToList();
            
            // If no active players remain (all folded or all-in), round closes
            if (activePlayers.Count == 0)
            {
                return true;
            }
            
            // Check if all active players have:
            // 1. Acted this round (check/fold/call/bet/raise)
            // 2. Matched the current bet (or are all-in)
            foreach (var player in activePlayers)
            {
                // If player hasn't acted yet this round, can't close
                if (!round.HasActed(player.Id))
                {
                    return false;
                }
                
                // If player hasn't matched current bet, can't close
                var contribution = round.GetContribution(player.Id);
                if (contribution < round.CurrentBet)
                {
                    return false;
                }
            }
            
            // All active players have acted and matched the bet - round can close
            return true;
        }
    }
}
