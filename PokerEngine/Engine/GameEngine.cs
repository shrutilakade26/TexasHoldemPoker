using System;
using System.Collections.Generic;
using System.Linq;
using PokerEngine.Core;
using PokerEngine.RNG;
using PokerEngine.Rules;
using PokerEngine.State;

// File: GameEngine.cs
// Purpose: Central orchestrator coordinating game phases, player turns, and pot management.
// Responsible for: Sequencing play, applying validated actions to GameState, delegating to managers for turns, rounds, and pots.
// Not responsible for: UI input/output, networking, or randomness generation (delegated to RNG services).
// Fit: Entry point for consuming PlayerAction instances and emitting state changes/observer notifications.

namespace PokerEngine.Engine
{
    /// <summary>
    /// Drives the poker hand lifecycle by invoking TurnManager, RoundManager, and PotManager while mutating GameState safely.
    /// </summary>
    public sealed class GameEngine
    {
        private readonly ActionValidator _validator = new();
        private readonly TurnManager _turnManager = new();
        private readonly RoundManager _roundManager = new();
        private readonly PotManager _potManager = new();

        public GameState CreateGameState(IEnumerable<Player> players, decimal smallBlind, decimal bigBlind, int dealerSeat, SecureRandom rng, ShuffleService shuffle)
        {
            var deck = Deck.CreateStandard(rng, shuffle);
            return new GameState(players, deck, smallBlind, bigBlind, dealerSeat);
        }

        public void StartHand(GameState state)
        {
            state.HandComplete = false;
            state.Pots.Clear();
            foreach (var player in state.Players)
            {
                state.TotalContributions[player.Id] = 0m;
            }

            _roundManager.StartHand(state);
            PostBlinds(state);
            state.CurrentSeatToAct = FirstToActAfterBlinds(state);
        }

        public ValidationResult ApplyAction(GameState state, PlayerAction action)
        {
            var validation = _validator.Validate(state, action);
            if (!validation.IsValid)
            {
                return validation;
            }

            var player = state.GetPlayerById(action.PlayerId);
            var round = state.RoundState;

            switch (action.Type)
            {
                case ActionType.Fold:
                    player.Fold();
                    round.MarkFold(player.Id);
                    round.MarkActed(player.Id);
                    break;
                case ActionType.Check:
                    round.MarkActed(player.Id);
                    break;
                case ActionType.Call:
                    HandleCall(state, player);
                    round.MarkActed(player.Id);
                    break;
                case ActionType.Bet:
                    HandleBet(state, player, action.Amount);
                    round.MarkActed(player.Id);
                    break;
                case ActionType.Raise:
                    HandleRaise(state, player, action.Amount);
                    round.MarkActed(player.Id);
                    break;
                case ActionType.AllIn:
                    HandleAllIn(state, player, action.Amount);
                    round.MarkActed(player.Id);
                    break;
            }

            var remaining = state.Players.Count(p => !p.IsFolded);
            if (remaining <= 1)
            {
                var winner = state.Players.First(p => !p.IsFolded);
                var pots = _potManager.BuildPots(state, new[] { winner.Id });
                var potTotal = pots.Sum(p => p.Amount);
                winner.ReceivePayout(potTotal);
                state.HandComplete = true;
                state.Phase = GamePhase.Complete;
                return validation;
            }

            // Check if all remaining players are all-in (no more betting possible)
            var canAct = state.Players.Any(p => !p.IsFolded && !p.IsAllIn && p.Stack > 0);
            if (!canAct)
            {
                // Run out to showdown - deal remaining community cards
                RunOutToShowdown(state);
                return validation;
            }

            if (_turnManager.ShouldCloseRound(state))
            {
                _roundManager.AdvancePhase(state);
                state.CurrentSeatToAct = FirstToActAfterBlinds(state);
                
                // After phase advance, check again if anyone can act
                canAct = state.Players.Any(p => !p.IsFolded && !p.IsAllIn && p.Stack > 0);
                if (!canAct)
                {
                    RunOutToShowdown(state);
                }
            }
            else
            {
                state.CurrentSeatToAct = _turnManager.NextSeat(state);
            }

            return validation;
        }

        private void RunOutToShowdown(GameState state)
        {
            // Deal remaining community cards without betting
            while (state.Phase < GamePhase.Showdown)
            {
                _roundManager.AdvancePhase(state);
            }
            state.Phase = GamePhase.Showdown;
        }

        public Dictionary<Guid, decimal> Showdown(GameState state, IReadOnlyDictionary<Guid, int> handRanks)
        {
            state.Phase = GamePhase.Showdown;
            var eligible = state.Players.Where(p => !p.IsFolded).Select(p => p.Id).ToArray();
            var payouts = _potManager.Settle(state, handRanks, eligible);
            state.HandComplete = true;
            state.Phase = GamePhase.Complete;
            return payouts;
        }

        private void HandleCall(GameState state, Player player)
        {
            var round = state.RoundState;
            var contribution = round.GetContribution(player.Id);
            var toCall = Math.Max(0m, round.CurrentBet - contribution);
            var committed = player.CommitChips(toCall);
            round.RecordContribution(player.Id, committed);
            _potManager.ApplyContribution(state, player.Id, committed);
            if (player.IsAllIn)
            {
                round.MarkReadyToClose();
            }
        }

        private void HandleBet(GameState state, Player player, decimal amount)
        {
            var committed = player.CommitChips(amount);
            state.RoundState.RecordContribution(player.Id, committed);
            state.RoundState.SetCurrentBet(committed, player.SeatIndex, committed);
            _potManager.ApplyContribution(state, player.Id, committed);
        }

        private void HandleRaise(GameState state, Player player, decimal amount)
        {
            var round = state.RoundState;
            var contribution = round.GetContribution(player.Id);
            var target = amount - contribution;
            var committed = player.CommitChips(target);
            round.RecordContribution(player.Id, committed);
            var newBet = contribution + committed;
            var raiseAmount = newBet - round.CurrentBet;
            round.SetCurrentBet(newBet, player.SeatIndex, raiseAmount);
            _potManager.ApplyContribution(state, player.Id, committed);
        }

        private void HandleAllIn(GameState state, Player player, decimal amount)
        {
            var round = state.RoundState;
            var contribution = round.GetContribution(player.Id);
            var committed = player.CommitChips(player.Stack);
            round.RecordContribution(player.Id, committed);
            _potManager.ApplyContribution(state, player.Id, committed);

            var newTotal = contribution + committed;
            if (newTotal > round.CurrentBet)
            {
                var raiseAmount = newTotal - round.CurrentBet;
                round.SetCurrentBet(newTotal, player.SeatIndex, raiseAmount);
            }

            round.MarkReadyToClose();
        }

        private void PostBlinds(GameState state)
        {
            int smallBlindSeat, bigBlindSeat;
            
            // Heads-up special case: dealer is small blind
            if (state.Players.Count(p => p.Stack > 0) == 2)
            {
                smallBlindSeat = state.DealerSeat;
                bigBlindSeat = NextOccupiedSeat(state, state.DealerSeat);
            }
            else
            {
                smallBlindSeat = NextOccupiedSeat(state, state.DealerSeat);
                bigBlindSeat = NextOccupiedSeat(state, smallBlindSeat);
            }

            var small = state.GetPlayerBySeat(smallBlindSeat);
            var big = state.GetPlayerBySeat(bigBlindSeat);

            var smallContribution = small.CommitChips(state.SmallBlind);
            var bigContribution = big.CommitChips(state.BigBlind);

            state.RoundState.ResetForNewRound(state.Players.Select(p => p.Id));
            state.RoundState.RecordContribution(small.Id, smallContribution);
            state.RoundState.RecordContribution(big.Id, bigContribution);
            state.RoundState.SetCurrentBet(bigContribution, big.SeatIndex, state.BigBlind);
            
            // Store BB seat for preflop first-to-act calculation
            state.BigBlindSeat = bigBlindSeat;

            _potManager.ApplyContribution(state, small.Id, smallContribution);
            _potManager.ApplyContribution(state, big.Id, bigContribution);
        }

        private int FirstToActAfterBlinds(GameState state)
        {
            if (state.Phase == GamePhase.PreFlop)
            {
                // PreFlop: First to act is UTG (seat after big blind)
                // For heads-up: dealer (SB) acts first preflop
                if (state.Players.Count(p => !p.IsFolded && p.Stack >= 0) == 2)
                {
                    return state.DealerSeat;
                }
                return NextOccupiedSeat(state, state.BigBlindSeat);
            }
            else
            {
                // Post-flop: First active player after dealer
                return NextOccupiedSeat(state, state.DealerSeat);
            }
        }

        private static int NextOccupiedSeat(GameState state, int fromSeat)
        {
            var seats = state.Players.Select(p => p.SeatIndex).OrderBy(s => s).ToList();
            var startIndex = seats.IndexOf(fromSeat);
            if (startIndex < 0)
            {
                startIndex = 0;
            }
            for (var i = 1; i <= seats.Count; i++)
            {
                var seat = seats[(startIndex + i) % seats.Count];
                var player = state.GetPlayerBySeat(seat);
                // Skip folded and all-in players
                if (!player.IsFolded && !player.IsAllIn && player.Stack > 0)
                {
                    return seat;
                }
            }

            return fromSeat;
        }
    }
}
