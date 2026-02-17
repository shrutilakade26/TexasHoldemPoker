using System;

// File: ActionType.cs
// Purpose: Enumerates the possible player action categories in Texas Hold'em.
// Responsible for: Defining the canonical set of action types used across validation and engine orchestration.
// Not responsible for: Holding action data (PlayerAction) or performing validation (ActionValidator handles legality checks).
// Fit: Shared contract ensuring consistent action semantics between UI inputs, validation, and engine processing.

namespace PokerEngine.Rules
{
    /// <summary>
    /// Canonical action type definitions (e.g., fold, check, call, bet, raise) for the rules layer.
    /// </summary>
    public enum ActionType
    {
        None = 0,
        Fold = 1,
        Check = 2,
        Call = 3,
        Bet = 4,
        Raise = 5,
        AllIn = 6
    }
}
