using System;

// File: GamePhase.cs
// Purpose: Defines the discrete phases of a Texas Hold'em hand.
// Responsible for: Providing a canonical set of phases to drive RoundManager transitions and validation rules.
// Not responsible for: Holding state beyond the phase identifier or advancing turns.
// Fit: Shared reference for engine and rules to ensure consistent phase-aware behavior.

namespace PokerEngine.State
{
    /// <summary>
    /// Enumerated hand phases (e.g., pre-flop through showdown) used to gate logic across the engine.
    /// </summary>
    public enum GamePhase
    {
        NotStarted = 0,
        PreFlop = 1,
        Flop = 2,
        Turn = 3,
        River = 4,
        Showdown = 5,
        Complete = 6
    }
}
