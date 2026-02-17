using System;
using System.Collections.Generic;
using System.Text;

// File: IGameObserver.cs
// Purpose: Defines the contract for receiving game events without mutating engine state.
// Responsible for: Exposing callbacks/notifications that external systems can implement for logging or UI updates.
// Not responsible for: Changing GameState, validating actions, or performing side effects inside the engine.
// Fit: Decouples engine progression from presentation and transport layers while keeping the core headless.

namespace PokerEngine.Interfaces
{
    /// <summary>
    /// Observer hook for consuming state changes and events emitted by the engine.
    /// </summary>
    public interface IGameObserver
    {
    }
}
