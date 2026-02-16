using System;
using System.Collections.Generic;
using System.Linq;

// File: HandEvaluatorWrapper.cs
// Purpose: Evaluates Texas Hold'em poker hands and ranks them.
// Responsible for: Determining hand strength from 7 cards (2 hole + 5 community), ranking hands for showdown.
// Not responsible for: Managing pots, determining winners based on betting context, or mutating GameState.
// Fit: Supplies PotManager and GameEngine with ranked outcomes during showdown.

namespace PokerEngine.Rules
{
    /// <summary>
    /// Texas Hold'em hand evaluator. Lower rank value = better hand.
    /// </summary>
    public sealed class HandEvaluatorWrapper
    {
        /// <summary>
        /// Evaluates a player's best 5-card hand from hole cards + community cards.
        /// Returns a rank where lower = better hand.
        /// </summary>
        public int EvaluateHand(IReadOnlyList<Core.Card> holeCards, IReadOnlyList<Core.Card> communityCards)
        {
            if (holeCards.Count < 2)
                throw new ArgumentException("Need at least 2 hole cards", nameof(holeCards));
            if (communityCards.Count < 3)
                throw new ArgumentException("Need at least 3 community cards", nameof(communityCards));

            var allCards = holeCards.Concat(communityCards).ToList();
            var bestHand = FindBestHand(allCards);
            return bestHand.Rank;
        }

        /// <summary>
        /// Evaluates multiple players and returns their ranks (lower = better).
        /// </summary>
        public Dictionary<Guid, int> EvaluateAllHands(
            IEnumerable<(Guid PlayerId, IReadOnlyList<Core.Card> HoleCards)> players,
            IReadOnlyList<Core.Card> communityCards)
        {
            var results = new Dictionary<Guid, int>();

            foreach (var (playerId, holeCards) in players)
            {
                var rank = EvaluateHand(holeCards, communityCards);
                results[playerId] = rank;
            }

            return results;
        }

        /// <summary>
        /// Gets the hand category name for display purposes.
        /// </summary>
        public string GetHandName(IReadOnlyList<Core.Card> holeCards, IReadOnlyList<Core.Card> communityCards)
        {
            if (holeCards.Count < 2 || communityCards.Count < 3)
                return "Unknown";

            var allCards = holeCards.Concat(communityCards).ToList();
            var bestHand = FindBestHand(allCards);
            return bestHand.Category.ToString();
        }

        /// <summary>
        /// Gets the hand category.
        /// </summary>
        public HandCategory GetHandCategory(IReadOnlyList<Core.Card> holeCards, IReadOnlyList<Core.Card> communityCards)
        {
            if (holeCards.Count < 2 || communityCards.Count < 3)
                return HandCategory.HighCard;

            var allCards = holeCards.Concat(communityCards).ToList();
            var bestHand = FindBestHand(allCards);
            return bestHand.Category;
        }

        private HandResult FindBestHand(List<Core.Card> cards)
        {
            var bestHand = new HandResult { Rank = int.MaxValue, Category = HandCategory.HighCard };

            // Generate all 5-card combinations from 7 cards
            foreach (var combo in GetCombinations(cards, 5))
            {
                var hand = EvaluateFiveCards(combo);
                if (hand.Rank < bestHand.Rank)
                {
                    bestHand = hand;
                }
            }

            return bestHand;
        }

        private HandResult EvaluateFiveCards(List<Core.Card> cards)
        {
            var sorted = cards.OrderByDescending(c => c.Rank).ToList();
            var ranks = sorted.Select(c => (int)c.Rank).ToList();
            var suits = sorted.Select(c => c.Suit).ToList();

            var isFlush = suits.Distinct().Count() == 1;
            var isStraight = IsStraight(ranks);

            var rankGroups = ranks.GroupBy(r => r).OrderByDescending(g => g.Count()).ThenByDescending(g => g.Key).ToList();

            // Straight Flush
            if (isFlush && isStraight)
            {
                var highCard = ranks[0] == 14 && ranks[4] == 2 ? 5 : ranks[0]; // Ace-low straight
                return new HandResult { Category = HandCategory.StraightFlush, Rank = 1000000 + highCard };
            }

            // Four of a Kind
            if (rankGroups[0].Count() == 4)
            {
                return new HandResult { Category = HandCategory.FourOfAKind, Rank = 2000000 + rankGroups[0].Key * 100 + rankGroups[1].Key };
            }

            // Full House
            if (rankGroups[0].Count() == 3 && rankGroups[1].Count() == 2)
            {
                return new HandResult { Category = HandCategory.FullHouse, Rank = 3000000 + rankGroups[0].Key * 100 + rankGroups[1].Key };
            }

            // Flush
            if (isFlush)
            {
                return new HandResult { Category = HandCategory.Flush, Rank = 4000000 + ranks[0] * 100000 + ranks[1] * 10000 + ranks[2] * 1000 + ranks[3] * 100 + ranks[4] };
            }

            // Straight
            if (isStraight)
            {
                var highCard = ranks[0] == 14 && ranks[4] == 2 ? 5 : ranks[0];
                return new HandResult { Category = HandCategory.Straight, Rank = 5000000 + highCard };
            }

            // Three of a Kind
            if (rankGroups[0].Count() == 3)
            {
                return new HandResult { Category = HandCategory.ThreeOfAKind, Rank = 6000000 + rankGroups[0].Key * 10000 + rankGroups[1].Key * 100 + rankGroups[2].Key };
            }

            // Two Pair
            if (rankGroups[0].Count() == 2 && rankGroups[1].Count() == 2)
            {
                return new HandResult { Category = HandCategory.TwoPair, Rank = 7000000 + rankGroups[0].Key * 10000 + rankGroups[1].Key * 100 + rankGroups[2].Key };
            }

            // One Pair
            if (rankGroups[0].Count() == 2)
            {
                return new HandResult { Category = HandCategory.OnePair, Rank = 8000000 + rankGroups[0].Key * 100000 + rankGroups[1].Key * 1000 + rankGroups[2].Key * 10 + rankGroups[3].Key };
            }

            // High Card
            return new HandResult { Category = HandCategory.HighCard, Rank = 9000000 + ranks[0] * 100000 + ranks[1] * 10000 + ranks[2] * 1000 + ranks[3] * 100 + ranks[4] };
        }

        private bool IsStraight(List<int> ranks)
        {
            // Check normal straight
            for (int i = 0; i < ranks.Count - 1; i++)
            {
                if (ranks[i] - ranks[i + 1] != 1)
                {
                    // Check for Ace-low straight (A-2-3-4-5)
                    if (ranks[0] == 14 && ranks[1] == 5 && ranks[2] == 4 && ranks[3] == 3 && ranks[4] == 2)
                        return true;
                    return false;
                }
            }
            return true;
        }

        private IEnumerable<List<T>> GetCombinations<T>(List<T> list, int length)
        {
            if (length == 1) return list.Select(t => new List<T> { t });

            return GetCombinations(list, length - 1)
                .SelectMany(t => list.Where(e => list.IndexOf(e) > list.IndexOf(t.Last())),
                    (t1, t2) => t1.Concat(new[] { t2 }).ToList());
        }

        private class HandResult
        {
            public HandCategory Category { get; set; }
            public int Rank { get; set; }
        }
    }

    public enum HandCategory
    {
        StraightFlush = 1,
        FourOfAKind = 2,
        FullHouse = 3,
        Flush = 4,
        Straight = 5,
        ThreeOfAKind = 6,
        TwoPair = 7,
        OnePair = 8,
        HighCard = 9
    }
}
