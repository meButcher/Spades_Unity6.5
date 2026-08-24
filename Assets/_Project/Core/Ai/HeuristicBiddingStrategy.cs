using System;
using System.Collections.Generic;
using Spades.Core.Cards;
using Spades.Core.Rules;
using Spades.Core.State;

namespace Spades.Core.Ai
{
    /// <summary>
    /// Counts expected tricks with a scoring function rather than a decision tree.
    ///
    /// That shape matters: it is one testable method, and any bid it makes can be explained by
    /// reading off the terms that contributed. A tree of if-statements produces the same numbers
    /// and cannot be explained or tuned.
    /// </summary>
    public sealed class HeuristicBiddingStrategy : IBiddingStrategy
    {
        private readonly int[] _suitCounts = new int[4];

        public int ChooseBid(SeatView view, GameRules rules)
        {
            if (rules == null) throw new ArgumentNullException(nameof(rules));

            IReadOnlyList<Card> hand = view.Hand;

            for (int i = 0; i < _suitCounts.Length; i++) _suitCounts[i] = 0;
            for (int i = 0; i < hand.Count; i++) _suitCounts[(int)hand[i].Suit]++;

            int spadeCount = _suitCounts[(int)Suit.Spades];
            double tricks = 0.0;

            // Trumps. A high spade is only reliable if it is protected by length behind it.
            for (int i = 0; i < hand.Count; i++)
            {
                Card c = hand[i];
                if (!c.IsSpade) continue;

                if (c.Rank == Rank.Ace) tricks += 1.0;
                else if (c.Rank == Rank.King && spadeCount >= 2) tricks += 1.0;
                else if (c.Rank == Rank.Queen && spadeCount >= 3) tricks += 1.0;
            }

            // Length in trumps beyond the third spade tends to win late tricks by exhaustion.
            if (spadeCount > 3) tricks += 0.5 * (spadeCount - 3);

            // Side suits.
            for (int s = 0; s < 4; s++)
            {
                var suit = (Suit)s;
                if (suit == Suit.Spades) continue;

                int lengthInSuit = _suitCounts[s];

                if (lengthInSuit == 0)
                {
                    // Void: every lead of this suit is a chance to trump.
                    tricks += 0.5;
                    continue;
                }

                if (lengthInSuit == 1) tricks += 0.25;

                for (int i = 0; i < hand.Count; i++)
                {
                    Card c = hand[i];
                    if (c.Suit != suit) continue;

                    if (c.Rank == Rank.Ace) tricks += 1.0;
                    else if (c.Rank == Rank.King && lengthInSuit >= 2) tricks += 0.7;
                    else if (c.Rank == Rank.Queen && lengthInSuit >= 3) tricks += 0.3;
                }
            }

            int bid = (int)Math.Round(tricks, MidpointRounding.AwayFromZero);
            if (bid < 0) bid = 0;
            if (bid > rules.HandSize) bid = rules.HandSize;

            if (bid > 0) return bid;

            // A zero score is only worth turning into a Nil if the hand genuinely cannot be
            // forced to win a trick. A high spade or a side ace is the usual way a naive bot
            // throws away a hundred points.
            if (rules.AllowNil && IsSafeForNil(hand)) return 0;

            return 1;
        }

        private static bool IsSafeForNil(IReadOnlyList<Card> hand)
        {
            for (int i = 0; i < hand.Count; i++)
            {
                Card c = hand[i];
                if (c.IsSpade && c.Rank > Rank.Jack) return false;
                if (!c.IsSpade && c.Rank == Rank.Ace) return false;
            }
            return true;
        }
    }
}
