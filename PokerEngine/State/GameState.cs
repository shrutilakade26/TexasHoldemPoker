using System;
using System.Collections.Generic;
using System.Linq;
using PokerEngine.Core;
using PokerEngine.Engine;

// File: GameState.cs
// Purpose: Single source of truth describing the current poker session and active hand state.
// Responsible for: Holding player data, deck references, pots, betting history, and current phase/turn context.
// Not responsible for: Business rules enforcement (rules layer), randomness, or presenting data to UI.
// Fit: Central aggregate mutated by GameEngine after validation; all other components read from this authoritative state.

namespace PokerEngine.State
{
    /// <summary>
    /// Authoritative snapshot of session and hand data; only mutated through validated engine operations.
    /// </summary>
    public class GameState
    {
        private readonly List<Player> _players;

        public GameState(IEnumerable<Player> players, Deck deck, decimal smallBlind, decimal bigBlind, int dealerSeat)
        {
            if (players == null) throw new ArgumentNullException(nameof(players));
            _players = players.ToList();
            if (_players.Count < 2) throw new ArgumentException("At least two players required.", nameof(players));
            if (_players.Any(p => p == null)) throw new ArgumentException("Players cannot contain null.", nameof(players));
            if (_players.Select(p => p.SeatIndex).Distinct().Count() != _players.Count)
            {
                throw new ArgumentException("Seat indices must be unique across players.", nameof(players));
            }
            if (_players.Select(p => p.Id).Distinct().Count() != _players.Count)
            {
                throw new ArgumentException("Player IDs must be unique across players.", nameof(players));
            }

            _players = _players.OrderBy(p => p.SeatIndex).ToList();
            Deck = deck ?? throw new ArgumentNullException(nameof(deck));
            if (smallBlind <= 0) throw new ArgumentOutOfRangeException(nameof(smallBlind));
            if (bigBlind <= 0 || bigBlind < smallBlind) throw new ArgumentOutOfRangeException(nameof(bigBlind));
            SmallBlind = smallBlind;
            BigBlind = bigBlind;
            DealerSeat = dealerSeat;
            CurrentSeatToAct = dealerSeat;
            Phase = GamePhase.NotStarted;
            CommunityCards = new List<Card>(5);
            RoundState = new RoundState();
            Pots = new List<Pot>();
            TotalContributions = new Dictionary<Guid, decimal>();
            foreach (var player in _players)
            {
                TotalContributions[player.Id] = 0m;
            }
        }

        public IReadOnlyList<Player> Players => _players;

        public Deck Deck { get; }

        public List<Card> CommunityCards { get; }

        public GamePhase Phase { get; set; }

        public int DealerSeat { get; set; }
        
        public int BigBlindSeat { get; set; }

        public int CurrentSeatToAct { get; set; }

        public decimal SmallBlind { get; }

        public decimal BigBlind { get; }

        public RoundState RoundState { get; }

        public Dictionary<Guid, decimal> TotalContributions { get; }

        public List<Pot> Pots { get; }

        public bool HandComplete { get; set; }

        public Player GetPlayerById(Guid id) => _players.First(p => p.Id == id);

        public Player GetPlayerBySeat(int seat) => _players.First(p => p.SeatIndex == seat);

        public IEnumerable<Player> ActivePlayers() => _players.Where(p => !p.IsFolded);

        /// <summary>
        /// Clears all pot contributions after winnings have been distributed.
        /// Should be called after pot distribution animations are complete.
        /// </summary>
        public void ClearPotContributions()
        {
            foreach (var playerId in TotalContributions.Keys.ToList())
            {
                TotalContributions[playerId] = 0m;
            }
            Pots.Clear();
        }
    }
}
