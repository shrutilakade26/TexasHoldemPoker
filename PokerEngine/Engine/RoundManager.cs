using System;
using System.Collections.Generic;
using System.Linq;
using PokerEngine.Core;
using PokerEngine.State;

// File: RoundManager.cs
// Purpose: Advances the hand through Texas Hold'em phases (pre-flop, flop, turn, river, showdown).
// Responsible for: Controlling phase transitions, dealing community cards, and resetting round-specific state between hands.
// Not responsible for: Validating individual player actions or calculating pots (delegated to rules and PotManager).
// Fit: Works with GameEngine to ensure legal progression and to expose clear state transitions to observers.

namespace PokerEngine.Engine
{
    /// <summary>
    /// Coordinates phase changes and card reveals while preserving deterministic ordering from the deck.
    /// </summary>
    internal sealed class RoundManager
    {
        public void StartHand(GameState state)
        {
            state.Phase = GamePhase.PreFlop;
            state.CommunityCards.Clear();
            var active = state.Players.Where(p => p.Stack > 0).Select(p => p.Id).ToList();
            state.RoundState.ResetForNewRound(active);

            foreach (var player in state.Players)
            {
                if (player.Stack <= 0)
                {
                    continue;
                }
                player.ResetForNewHand();
                var first = state.Deck.Draw();
                var second = state.Deck.Draw();
                player.GiveHoleCards(first, second);
            }
        }

        public void AdvancePhase(GameState state)
        {
            switch (state.Phase)
            {
                case GamePhase.PreFlop:
                    RevealFlop(state);
                    state.Phase = GamePhase.Flop;
                    break;
                case GamePhase.Flop:
                    RevealTurn(state);
                    state.Phase = GamePhase.Turn;
                    break;
                case GamePhase.Turn:
                    RevealRiver(state);
                    state.Phase = GamePhase.River;
                    break;
                case GamePhase.River:
                    state.Phase = GamePhase.Showdown;
                    break;
                default:
                    state.Phase = GamePhase.Complete;
                    break;
            }

            var active = state.Players.Where(p => !p.IsFolded).Select(p => p.Id);
            state.RoundState.ResetForNewRound(active);
        }

        private static void RevealFlop(GameState state)
        {
            state.Deck.Burn();
            var cards = state.Deck.Draw(3);
            state.CommunityCards.AddRange(cards);
        }

        private static void RevealTurn(GameState state)
        {
            state.Deck.Burn();
            state.CommunityCards.Add(state.Deck.Draw());
        }

        private static void RevealRiver(GameState state)
        {
            state.Deck.Burn();
            state.CommunityCards.Add(state.Deck.Draw());
        }
    }
}
