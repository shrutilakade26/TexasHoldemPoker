using System;

// File: PlayerAction.cs
// Purpose: Represents an immutable intent submitted by a player (action type, target amounts, metadata).
// Responsible for: Capturing user intent in a structured form for validation and processing.
// Not responsible for: Enforcing legality (ActionValidator) or applying changes to GameState (GameEngine handles mutations).
// Fit: Boundary DTO moving from external input layers into the rules/engine pipeline.

namespace PokerEngine.Rules
{
    /// <summary>
    /// Intent model describing what a player wishes to do on their turn; consumed by validators and the engine.
    /// </summary>
    public sealed class PlayerAction
    {
        public PlayerAction(Guid playerId, ActionType type, decimal amount = 0)
        {
            PlayerId = playerId;
            Type = type;
            Amount = amount;
        }

        public Guid PlayerId { get; }

        public ActionType Type { get; }

        public decimal Amount { get; }

        public static PlayerAction Fold(Guid playerId) => new(playerId, ActionType.Fold);

        public static PlayerAction Check(Guid playerId) => new(playerId, ActionType.Check);

        public static PlayerAction Call(Guid playerId) => new(playerId, ActionType.Call);

        public static PlayerAction Bet(Guid playerId, decimal amount) => new(playerId, ActionType.Bet, amount);

        public static PlayerAction Raise(Guid playerId, decimal amount) => new(playerId, ActionType.Raise, amount);

        public static PlayerAction AllIn(Guid playerId, decimal amount) => new(playerId, ActionType.AllIn, amount);
    }
}
