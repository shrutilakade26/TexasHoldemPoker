using System;
using System.Collections.Generic;
using System.Linq;
using PokerEngine.State;
using PokerEngine.Core;

// File: ActionValidator.cs
// Purpose: Enforces Texas Hold'em betting rules and verifies whether a PlayerAction is legal in the current state.
// Responsible for: Checking turn order, stack sufficiency, bet sizing rules, and phase-specific constraints before mutations occur.
// Not responsible for: Executing actions (GameEngine handles mutations) or managing pots (PotManager) once validated.
// Fit: Gatekeeper between external intent (PlayerAction) and internal state changes to preserve invariants.

namespace PokerEngine.Rules
{
    /// <summary>
    /// Validates player intents against GameState and RoundState to prevent illegal or contradictory actions.
    /// </summary>
    internal sealed class ActionValidator
    {
        public ValidationResult Validate(GameState state, PlayerAction action)
        {
            var errors = new List<string>();

            if (state.HandComplete)
            {
                errors.Add("Hand already complete.");
                return ValidationResult.Fail(errors);
            }

            if (state.Phase >= GamePhase.Showdown)
            {
                errors.Add("No actions allowed during showdown.");
                return ValidationResult.Fail(errors);
            }

            var player = state.Players.FirstOrDefault(p => p.Id == action.PlayerId);
            if (player == null)
            {
                errors.Add("Unknown player.");
                return ValidationResult.Fail(errors);
            }

            if (player.IsFolded)
            {
                errors.Add("Player already folded.");
            }

            if (player.IsAllIn)
            {
                errors.Add("Player is all-in and cannot act.");
            }

            if (player.SeatIndex != state.CurrentSeatToAct)
            {
                errors.Add("Not this player's turn.");
            }

            var round = state.RoundState;
            var contribution = round.GetContribution(player.Id);
            var toCall = round.CurrentBet - contribution;

            switch (action.Type)
            {
                case ActionType.Fold:
                    break;
                case ActionType.Check:
                    if (round.CurrentBet > contribution)
                    {
                        errors.Add("Cannot check facing a bet.");
                    }
                    break;
                case ActionType.Call:
                    if (toCall <= 0)
                    {
                        errors.Add("Nothing to call.");
                    }
                    else if (player.Stack <= 0)
                    {
                        errors.Add("Insufficient stack to call.");
                    }
                    break;
                case ActionType.Bet:
                    if (round.CurrentBet > 0)
                    {
                        errors.Add("Bet not allowed when a bet exists. Use raise.");
                    }
                    if (action.Amount <= 0)
                    {
                        errors.Add("Bet amount must be positive.");
                    }
                    if (action.Amount < state.BigBlind)
                    {
                        errors.Add("Bet must be at least big blind.");
                    }
                    if (action.Amount > player.Stack)
                    {
                        errors.Add("Bet exceeds stack. Use all-in.");
                    }
                    break;
                case ActionType.Raise:
                    if (round.CurrentBet <= 0)
                    {
                        errors.Add("No bet to raise.");
                        break;
                    }
                    var lastIncrement = round.LastRaiseAmount > 0 ? round.LastRaiseAmount : state.BigBlind;
                    var minRaiseTarget = round.CurrentBet + lastIncrement;
                    if (action.Amount < minRaiseTarget && action.Amount < player.Stack)
                    {
                        errors.Add("Raise must meet or exceed last increment.");
                    }
                    if (action.Amount <= round.CurrentBet)
                    {
                        errors.Add("Raise must exceed current bet.");
                    }
                    if (action.Amount > player.Stack)
                    {
                        errors.Add("Raise exceeds stack. Use all-in.");
                    }
                    break;
                case ActionType.AllIn:
                    if (player.Stack <= 0)
                    {
                        errors.Add("No chips available for all-in.");
                    }
                    if (action.Amount > player.Stack)
                    {
                        errors.Add("All-in cannot exceed available stack.");
                    }
                    break;
                default:
                    errors.Add("Unsupported action type.");
                    break;
            }

            return ValidationResult.From(errors);
        }
    }

    public sealed class ValidationResult
    {
        private ValidationResult(IEnumerable<string> errors)
        {
            Errors = errors.ToList();
        }

        public bool IsValid => Errors.Count == 0;

        public List<string> Errors { get; }

        public static ValidationResult From(IEnumerable<string> errors)
        {
            var list = errors.Where(e => !string.IsNullOrWhiteSpace(e)).ToList();
            return list.Count == 0 ? Success() : new ValidationResult(list);
        }

        public static ValidationResult Success() => new ValidationResult(Array.Empty<string>());

        public static ValidationResult Fail(IEnumerable<string> errors) => new ValidationResult(errors);
    }
}
