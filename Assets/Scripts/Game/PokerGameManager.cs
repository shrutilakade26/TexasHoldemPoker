using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using PokerEngine.Core;
using PokerEngine.Engine;
using PokerEngine.Rules;
using PokerEngine.State;
using PokerEngine.RNG;
using PokerEngine.Interfaces;

/// <summary>
/// Main Unity controller for the poker game.
/// Bridges the PokerEngine with Unity's MonoBehaviour lifecycle.
/// </summary>
public class PokerGameManager : MonoBehaviour, IGameObserver
{
    [Header("Game Settings")]
    [SerializeField] private int numberOfPlayers = 6;
    [SerializeField] private float startingStack = 1000f;
    [SerializeField] private float smallBlind = 5f;
    [SerializeField] private float bigBlind = 10f;
    [SerializeField] private float aiThinkTime = 1f;

    [Header("References")]
    [SerializeField] private UIManager uiManager;
    [SerializeField] private GameOverUI gameOverUI;
    [SerializeField] private WinnerCelebration winnerCelebration;
    [SerializeField] private ShowdownUI showdownUI;
    [SerializeField] private PotAnimator potAnimator;
    [SerializeField] private RaiseControl raiseControl;
    [SerializeField] private CardDealerManager cardDealerManager;

    private GameEngine gameEngine;
    private GameState gameState;
    private SecureRandom secureRandom;
    private ShuffleService shuffleService;
    private int handsPlayed = 0;

    // Human player is always seat 0
    private const int HUMAN_PLAYER_SEAT = 0;

    private void Start()
    {
        InitializeGame();
    }

    private void InitializeGame()
    {
        // Initialize RNG services
        secureRandom = new SecureRandom();
        shuffleService = new ShuffleService();
        gameEngine = new GameEngine();

        // Create players
        var players = new Player[numberOfPlayers];
        for (int i = 0; i < numberOfPlayers; i++)
        {
            string playerName = i == HUMAN_PLAYER_SEAT ? "You" : $"AI Player {i}";
            players[i] = new Player(
                Guid.NewGuid(),
                playerName,
                seatIndex: i,
                stack: (decimal)startingStack
            );
        }

        // Create game state with fixed blinds (standard cash game)
        gameState = gameEngine.CreateGameState(
            players,
            (decimal)smallBlind,
            (decimal)bigBlind,
            dealerSeat: 0,
            secureRandom,
            shuffleService
        );

        Debug.Log($"Poker game initialized with {numberOfPlayers} players. Blinds: ${smallBlind}/${bigBlind}");
        
        if (uiManager != null)
        {
            uiManager.UpdateGameState(gameState);
        }
    }

    public void StartNewHand()
    {
        if (gameState == null)
        {
            Debug.LogError("Game state not initialized!");
            return;
        }

        gameEngine.StartHand(gameState);
        handsPlayed++;
        Debug.Log($"Hand #{handsPlayed} started. Phase: {gameState.Phase}");

        // Start hand with dealing animation
        StartCoroutine(StartHandWithAnimation());
    }

    private IEnumerator StartHandWithAnimation()
    {
        // Deal hole cards with animation
        if (cardDealerManager != null)
        {
            yield return cardDealerManager.DealHoleCards(gameState.Players);
        }
        else
        {
            // No animation, just wait a moment
            yield return new WaitForSeconds(0.5f);
        }

        // Update UI after dealing
        if (uiManager != null)
        {
            uiManager.UpdateGameState(gameState);
        }

        // Start AI turn processing
        StartCoroutine(ProcessTurns());
    }

    private IEnumerator ProcessTurns()
    {
        while (!gameState.HandComplete && gameState.Phase != GamePhase.NotStarted && gameState.Phase != GamePhase.Showdown)
        {
            var currentPlayer = gameState.GetPlayerBySeat(gameState.CurrentSeatToAct);

            if (currentPlayer.IsFolded || currentPlayer.IsAllIn)
            {
                // Skip folded or all-in players
                yield return new WaitForSeconds(0.1f);
                continue;
            }

            // Store phase before action
            var phaseBefore = gameState.Phase;
            
            // Store bet information BEFORE action (in case phase changes and bets get cleared)
            List<(Transform position, decimal amount)> currentBets = new List<(Transform, decimal)>();
            if (uiManager != null)
            {
                BetDisplay[] displays = uiManager.GetAllBetDisplays();
                if (displays != null)
                {
                    foreach (var display in displays)
                    {
                        if (display != null && display.GetCurrentBet() > 0)
                        {
                            currentBets.Add((display.transform, display.GetCurrentBet()));
                        }
                    }
                }
            }

            // Check if it's human player's turn
            if (gameState.CurrentSeatToAct == HUMAN_PLAYER_SEAT)
            {
                Debug.Log("Your turn!");
                uiManager?.EnablePlayerActions(true);
                
                // Wait for human action
                yield return new WaitUntil(() => !IsHumanPlayerTurn());
            }
            else
            {
                // AI player turn
                Debug.Log($"{currentPlayer.Name}'s turn");
                uiManager?.EnablePlayerActions(false);
                
                yield return new WaitForSeconds(aiThinkTime);
                
                var aiAction = GetAIAction(currentPlayer);
                ProcessPlayerAction(aiAction);
            }

            // Check if phase changed after action
            var phaseAfter = gameState.Phase;
            if (phaseBefore != phaseAfter)
            {
                // Phase changed! Betting round complete
                Debug.Log($"Phase transition: {phaseBefore} → {phaseAfter}");
                
                // Animate chips flying from saved bet positions to center pot
                if (potAnimator != null && currentBets.Count > 0)
                {
                    yield return StartCoroutine(AnimateChipsToPot(currentBets));
                }
                
                // Animate community cards based on new phase
                if (cardDealerManager != null)
                {
                    switch (phaseAfter)
                    {
                        case GamePhase.Flop:
                            yield return cardDealerManager.DealFlop();
                            break;
                        case GamePhase.Turn:
                            yield return cardDealerManager.DealTurn();
                            break;
                        case GamePhase.River:
                            yield return cardDealerManager.DealRiver();
                            break;
                    }
                }
                
                yield return new WaitForSeconds(1.0f); // Delay after phase change
            }
            else
            {
                // Normal delay between actions
                yield return new WaitForSeconds(0.2f);
            }
        }

        // Hand complete - start enhanced showdown sequence
        yield return StartCoroutine(HandleHandComplete());
    }

    private bool IsHumanPlayerTurn()
    {
        return !gameState.HandComplete && 
               gameState.CurrentSeatToAct == HUMAN_PLAYER_SEAT &&
               !gameState.GetPlayerBySeat(HUMAN_PLAYER_SEAT).IsFolded;
    }

    public void ProcessPlayerAction(PlayerAction action)
    {
        if (gameState == null || gameState.HandComplete)
        {
            Debug.LogWarning("Cannot process action - hand is complete or game not started");
            return;
        }

        var result = gameEngine.ApplyAction(gameState, action);

        if (!result.IsValid)
        {
            Debug.LogWarning("Invalid action: " + string.Join(", ", result.Errors));
            return;
        }

        var player = gameState.GetPlayerById(action.PlayerId);
        Debug.Log($"Action processed: {action.Type} by {player.Name}");

        // Show action on player's UI panel
        if (uiManager != null)
        {
            string actionText = action.Type.ToString();
            decimal displayAmount = 0;

            // Get the amount to display based on action type
            switch (action.Type)
            {
                case ActionType.Call:
                case ActionType.Bet:
                case ActionType.Raise:
                case ActionType.AllIn:
                    displayAmount = action.Amount;
                    break;
            }

            uiManager.ShowPlayerAction(player.SeatIndex, actionText, displayAmount);
        }

        // Update UI
        if (uiManager != null)
        {
            uiManager.UpdateGameState(gameState);
        }

        // Check if hand is complete
        if (gameState.HandComplete)
        {
            Debug.Log("Hand complete!");
        }
    }

    private PlayerAction GetAIAction(Player player)
    {
        // Simple AI logic
        var round = gameState.RoundState;
        var contribution = round.GetContribution(player.Id);
        var toCall = round.CurrentBet - contribution;

        // Random decision making
        var random = UnityEngine.Random.value;

        // If no bet to call, check or bet small
        if (toCall == 0)
        {
            if (random > 0.7f && player.Stack > (decimal)bigBlind * 2)
            {
                // Bet
                var betAmount = round.CurrentBet + (decimal)bigBlind;
                return PlayerAction.Bet(player.Id, betAmount);
            }
            else
            {
                // Check
                return PlayerAction.Check(player.Id);
            }
        }
        else
        {
            // There's a bet to call
            if (random > 0.6f && player.Stack >= toCall)
            {
                // Call
                return PlayerAction.Call(player.Id);
            }
            else if (random > 0.8f && player.Stack > toCall + (decimal)bigBlind)
            {
                // Raise
                var raiseAmount = round.CurrentBet + (decimal)bigBlind;
                return PlayerAction.Raise(player.Id, raiseAmount);
            }
            else
            {
                // Fold
                return PlayerAction.Fold(player.Id);
            }
        }
    }

    private IEnumerator HandleHandComplete()
    {
        // Disable player actions during showdown
        uiManager?.EnablePlayerActions(false);

        if (gameState.Phase == GamePhase.Showdown)
        {
            Debug.Log("=== SHOWDOWN PHASE ===");
            
            // 1. Reveal all active players' cards
            yield return StartCoroutine(RevealShowdownCards());
            
            // 2. Evaluate hands and determine winners
            var winners = PerformShowdownEvaluation();
            
            // 3. Display winner(s) and hand rankings (panel stays visible)
            yield return StartCoroutine(DisplayWinners(winners));
            
            // 4. Animate pot transfer to winner(s) (chips flying to winner)
            // Winner panel stays visible during chip animation
            yield return StartCoroutine(AnimatePotTransfer(winners));
            
            // 5. Hide winner panel after chip animation completes
            if (showdownUI != null)
            {
                showdownUI.HideWinnerPanel();
            }
            
            // 6. Brief pause after all animations complete before countdown
            yield return new WaitForSeconds(1f);
        }
        else if (gameState.HandComplete)
        {
            Debug.Log("=== HAND WON BY FOLD ===");
            
            // Find winner by fold
            var winner = gameState.Players.FirstOrDefault(p => !p.IsFolded);
            if (winner != null)
            {
                var winAmount = gameState.TotalContributions.Values.Sum();
                Debug.Log($"{winner.Name} wins ${winAmount} by fold!");
                
                // Show winner announcement (panel stays visible)
                yield return StartCoroutine(DisplayFoldWinner(winner, winAmount));
                
                // Animate pot transfer
                var winners = new List<(Player player, decimal amount, string handName)> 
                { 
                    (winner, winAmount, "Won by Fold") 
                };
                yield return StartCoroutine(AnimatePotTransfer(winners));
                
                // Hide winner panel after chip animation
                if (showdownUI != null)
                {
                    showdownUI.HideWinnerPanel();
                }
                
                // Brief pause after chip animation
                yield return new WaitForSeconds(1f);
            }
        }

        // 5. Check for game over
        var playersWithChips = gameState.Players.Count(p => p.Stack > 0);
        if (playersWithChips <= 1)
        {
            yield return new WaitForSeconds(2f);
            ShowGameOver();
            yield break;
        }

        // 6. Countdown to next hand
        yield return StartCoroutine(CountdownToNextHand());
        
        // 7. Start next hand
        PrepareNextHand();
        StartNewHand();
    }

    private IEnumerator RevealShowdownCards()
    {
        Debug.Log("Revealing showdown cards...");
        
        // Update UI to show all active players' cards
        if (uiManager != null)
        {
            uiManager.UpdateGameStateShowdown(gameState);
        }
        
        // Wait for cards to be visible
        yield return new WaitForSeconds(2f);
    }

    private List<(Player player, decimal amount, string handName)> PerformShowdownEvaluation()
    {
        var evaluator = new HandEvaluatorWrapper();
        var activePlayers = gameState.Players.Where(p => !p.IsFolded).ToList();
        var handRanks = new Dictionary<Guid, int>();
        var handNames = new Dictionary<Guid, string>();
        
        Debug.Log($"Community Cards: {string.Join(", ", gameState.CommunityCards)}");
        
        // Evaluate all hands
        foreach (var player in activePlayers)
        {
            var rank = evaluator.EvaluateHand(player.HoleCards, gameState.CommunityCards);
            var handName = evaluator.GetHandName(player.HoleCards, gameState.CommunityCards);
            
            handRanks[player.Id] = rank;
            handNames[player.Id] = handName;
            
            var cards = string.Join(", ", player.HoleCards);
            Debug.Log($"{player.Name}: {cards} - {handName} (rank: {rank})");
        }

        // Distribute winnings
        var payouts = gameEngine.Showdown(gameState, handRanks);
        
        // Create winners list
        var winners = new List<(Player player, decimal amount, string handName)>();
        foreach (var payout in payouts.Where(p => p.Value > 0))
        {
            var player = gameState.Players.First(p => p.Id == payout.Key);
            var handName = handNames[player.Id];
            winners.Add((player, payout.Value, handName));
        }
        
        return winners;
    }

    private IEnumerator DisplayWinners(List<(Player player, decimal amount, string handName)> winners)
    {
        Debug.Log("=== WINNERS ===");
        
        foreach (var (player, amount, handName) in winners)
        {
            Debug.Log($"{player.Name} wins ${amount} with {handName}!");
            
            // Display winner UI
            if (showdownUI != null)
            {
                yield return StartCoroutine(showdownUI.ShowWinner(player.Name, amount, handName));
            }
            else
            {
                yield return new WaitForSeconds(3f);
            }
        }
    }

    private IEnumerator DisplayFoldWinner(Player winner, decimal amount)
    {
        Debug.Log($"{winner.Name} wins ${amount} - All others folded!");
        
        // Display fold winner UI
        if (showdownUI != null)
        {
            yield return StartCoroutine(showdownUI.ShowFoldWinner(winner.Name, amount));
        }
        else
        {
            yield return new WaitForSeconds(3f);
        }
    }

    private IEnumerator AnimatePotTransfer(List<(Player player, decimal amount, string handName)> winners)
    {
        if (potAnimator == null || uiManager == null)
        {
            yield break;
        }
        
        foreach (var (player, amount, handName) in winners)
        {
            // Find player's UI panel for animation target
            Transform playerTransform = uiManager.GetPlayerPanelTransform(player.SeatIndex);
            
            if (playerTransform != null)
            {
                // Animate chips flying from pot to winner
                yield return StartCoroutine(potAnimator.AnimatePotToWinner(playerTransform, amount));
            }
            else
            {
                yield return new WaitForSeconds(0.5f);
            }
        }
        
        // Update UI with final amounts
        if (uiManager != null)
        {
            uiManager.UpdateGameState(gameState);
        }
        
        // Wait after chip animation completes so players can see the result
        yield return new WaitForSeconds(2f);
    }

    /// <summary>
    /// Animate chips flying from bet positions to center pot.
    /// </summary>
    private IEnumerator AnimateChipsToPot(List<(Transform position, decimal amount)> bets)
    {
        if (potAnimator == null || bets == null || bets.Count == 0)
            yield break;
        
        yield return StartCoroutine(potAnimator.AnimateBetsFromPositions(bets));
    }

    private IEnumerator CountdownToNextHand()
    {
        Debug.Log("Starting countdown to next hand...");
        
        // Display countdown UI
        if (showdownUI != null)
        {
            yield return StartCoroutine(showdownUI.ShowCountdown());
        }
        else
        {
            // Fallback countdown
            for (int i = 3; i >= 1; i--)
            {
                Debug.Log($"Next hand starts in: {i}");
                yield return new WaitForSeconds(1f);
            }
            Debug.Log("Starting new hand!");
        }
    }

    private void PrepareNextHand()
    {
        // Rotate dealer to next active player (with chips)
        int nextDealer = gameState.DealerSeat;
        int attempts = 0;
        int maxAttempts = gameState.Players.Count;
        
        do
        {
            nextDealer = (nextDealer + 1) % gameState.Players.Count;
            attempts++;
            
            var player = gameState.GetPlayerBySeat(nextDealer);
            if (player.Stack > 0)
            {
                gameState.DealerSeat = nextDealer;
                Debug.Log($"Dealer button moved to seat {nextDealer} ({player.Name})");
                break;
            }
        } while (attempts < maxAttempts);
        
        // Reset and reshuffle deck
        gameState.Deck.ResetAndShuffle(secureRandom, shuffleService);
    }

    public void PerformShowdown()
    {
        if (gameState == null)
        {
            Debug.LogWarning("Cannot perform showdown - game state is null");
            return;
        }

        Debug.Log("=== SHOWDOWN ===");

        // Evaluate hands
        var evaluator = new HandEvaluatorWrapper();
        var activePlayers = gameState.Players.Where(p => !p.IsFolded).ToList();

        if (activePlayers.Count == 0)
        {
            Debug.LogWarning("No active players for showdown");
            return;
        }

        var handRanks = new Dictionary<Guid, int>();
        
        Debug.Log($"Community Cards: {string.Join(", ", gameState.CommunityCards)}");
        
        foreach (var player in activePlayers)
        {
            var rank = evaluator.EvaluateHand(player.HoleCards, gameState.CommunityCards);
            handRanks[player.Id] = rank;
            
            var handName = evaluator.GetHandName(player.HoleCards, gameState.CommunityCards);
            var cards = string.Join(", ", player.HoleCards);
            Debug.Log($"{player.Name}: {cards} - {handName} (rank: {rank})");
        }

        // Distribute winnings
        var payouts = gameEngine.Showdown(gameState, handRanks);

        Debug.Log("=== WINNERS ===");
        foreach (var payout in payouts.Where(p => p.Value > 0))
        {
            var player = gameState.Players.First(p => p.Id == payout.Key);
            Debug.Log($"{player.Name} wins ${payout.Value}! New stack: ${player.Stack}");
        }

        // Update UI with showdown cards (show all active players' cards)
        if (uiManager != null)
        {
            uiManager.UpdateGameStateShowdown(gameState);
        }
    }

    private void ShowGameOver()
    {
        var winner = gameState.Players.FirstOrDefault(p => p.Stack > 0);
        if (winner != null && gameOverUI != null)
        {
            Debug.Log($"Game Over! {winner.Name} wins with ${winner.Stack}!");
            gameOverUI.Show(winner.Name, winner.Stack);
        }
    }

    // Public methods for UI buttons
    public void OnFoldClicked()
    {
        if (!IsHumanPlayerTurn()) return;
        
        // Hide raise control if visible
        if (raiseControl != null) raiseControl.Hide();
        
        var player = gameState.GetPlayerBySeat(HUMAN_PLAYER_SEAT);
        var action = PlayerAction.Fold(player.Id);
        ProcessPlayerAction(action);
    }

    public void OnCheckClicked()
    {
        if (!IsHumanPlayerTurn()) return;
        
        // Hide raise control if visible
        if (raiseControl != null) raiseControl.Hide();
        
        var player = gameState.GetPlayerBySeat(HUMAN_PLAYER_SEAT);
        var action = PlayerAction.Check(player.Id);
        ProcessPlayerAction(action);
    }

    public void OnCallClicked()
    {
        if (!IsHumanPlayerTurn()) return;
        
        // Hide raise control if visible
        if (raiseControl != null) raiseControl.Hide();
        
        var player = gameState.GetPlayerBySeat(HUMAN_PLAYER_SEAT);
        var action = PlayerAction.Call(player.Id);
        ProcessPlayerAction(action);
    }

    public void OnBetClicked()
    {
        if (!IsHumanPlayerTurn()) return;
        
        var player = gameState.GetPlayerBySeat(HUMAN_PLAYER_SEAT);
        var round = gameState.RoundState;
        
        // Two-step process for Bet (same as Raise):
        // Step 1: If slider not visible, show it
        if (raiseControl != null && !raiseControl.IsVisible())
        {
            // Calculate min and max bet
            decimal minBet = (decimal)bigBlind; // Minimum bet = big blind
            decimal maxBet = player.Stack; // Maximum = all chips
            
            raiseControl.Show(minBet, maxBet);
            Debug.Log("Bet control shown - adjust amount and click Bet again");
            return; // Don't place bet yet, wait for second click
        }
        
        // Step 2: Slider is visible, get amount and place bet
        decimal betAmount;
        if (raiseControl != null && raiseControl.IsVisible())
        {
            betAmount = raiseControl.GetRaiseAmount();
            raiseControl.Hide(); // Hide the control after confirming
            Debug.Log($"Betting ${betAmount}");
        }
        else
        {
            // Fallback: simple bet (big blind)
            betAmount = (decimal)bigBlind;
        }
        
        var action = PlayerAction.Bet(player.Id, betAmount);
        ProcessPlayerAction(action);
    }

    public void OnRaiseClicked()
    {
        if (!IsHumanPlayerTurn()) return;
        
        var player = gameState.GetPlayerBySeat(HUMAN_PLAYER_SEAT);
        var round = gameState.RoundState;
        
        // Two-step process:
        // Step 1: If slider not visible, show it
        if (raiseControl != null && !raiseControl.IsVisible())
        {
            // Calculate min and max raise
            decimal minRaise = round.CurrentBet + (decimal)bigBlind; // Minimum raise
            decimal maxRaise = player.Stack; // Maximum = all chips
            
            raiseControl.Show(minRaise, maxRaise);
            Debug.Log("Raise control shown - adjust amount and click Raise again");
            return; // Don't place bet yet, wait for second click
        }
        
        // Step 2: Slider is visible, get amount and place bet
        decimal raiseAmount;
        if (raiseControl != null && raiseControl.IsVisible())
        {
            raiseAmount = raiseControl.GetRaiseAmount();
            raiseControl.Hide(); // Hide the control after confirming
            Debug.Log($"Raising to ${raiseAmount}");
        }
        else
        {
            // Fallback: simple raise (current bet + big blind)
            raiseAmount = round.CurrentBet + (decimal)bigBlind;
        }
        
        var action = PlayerAction.Raise(player.Id, raiseAmount);
        ProcessPlayerAction(action);
    }

    public void OnAllInClicked()
    {
        if (!IsHumanPlayerTurn()) return;
        
        var player = gameState.GetPlayerBySeat(HUMAN_PLAYER_SEAT);
        
        // All-in: bet entire stack
        var action = PlayerAction.AllIn(player.Id, player.Stack);
        ProcessPlayerAction(action);
    }

    // IGameObserver implementation
    public void OnGameEvent(string eventType, object data)
    {
        Debug.Log($"Game Event: {eventType}");
    }

    // Public getters for UI
    public GameState GetGameState() => gameState;
    public bool IsHandActive() => gameState != null && !gameState.HandComplete;
    public bool IsHumanTurn() => IsHumanPlayerTurn();
}
