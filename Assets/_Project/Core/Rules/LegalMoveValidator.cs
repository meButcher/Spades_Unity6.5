using System;
using System.Collections.Generic;
using Spades.Core.Cards;
using Spades.Core.State;

namespace Spades.Core.Rules
{
    /// <summary>
    /// The single source of truth about what may be played.
    ///
    /// Three callers share it: the UI greys out illegal cards with it, the AI chooses only
    /// from GetLegalMoves, and GameLoop rejects invalid commands with it. One implementation
    /// means the UI can never disagree with the engine about what is playable, which is the
    /// classic card-game bug where a card looks legal and then is refused.
    /// </summary>
    public static class LegalMoveValidator
    {
        public static bool IsLegal(Card card, IReadOnlyList<Card> hand, TrickState trick, bool spadesBroken)
        {
            if (hand == null) throw new ArgumentNullException(nameof(hand));
            if (trick == null) throw new ArgumentNullException(nameof(trick));

            // Leading.
            if (trick.LedSuit == null)
            {
                if (!card.IsSpade) return true;
                if (spadesBroken) return true;

                // The exception. Without it, a seat holding nothing but spades has zero legal
                // moves before spades are broken and the hand deadlocks with no error.
                return HandIsAllSpades(hand);
            }

            // Following.
            Suit led = trick.LedSuit.Value;
            if (card.Suit == led) return true;
            return !HandContainsSuit(hand, led);
        }

        /// <summary>
        /// Deliberately implemented in terms of IsLegal rather than duplicating the rules.
        /// Two parallel implementations would eventually drift, and the drift is invisible
        /// until a player clicks a card the engine then refuses.
        /// </summary>
        public static List<Card> GetLegalMoves(IReadOnlyList<Card> hand, TrickState trick, bool spadesBroken)
        {
            if (hand == null) throw new ArgumentNullException(nameof(hand));

            var legal = new List<Card>(hand.Count);
            for (int i = 0; i < hand.Count; i++)
            {
                if (IsLegal(hand[i], hand, trick, spadesBroken)) legal.Add(hand[i]);
            }

            // A non-empty hand always has at least one legal move. If it does not, the rules
            // are wrong, and this assertion points straight at the hand that proves it rather
            // than letting the game silently hang.
            if (hand.Count > 0 && legal.Count == 0)
                throw new InvalidOperationException("No legal moves for a non-empty hand: rule bug.");

            return legal;
        }

        /// <summary>
        /// Fills a caller-owned buffer instead of allocating. Used on the AI's hot path, where
        /// this runs once per card played across hundreds of simulated games.
        /// </summary>
        public static void GetLegalMovesNonAlloc(
            IReadOnlyList<Card> hand, TrickState trick, bool spadesBroken, List<Card> buffer)
        {
            if (hand == null) throw new ArgumentNullException(nameof(hand));
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));

            buffer.Clear();
            for (int i = 0; i < hand.Count; i++)
            {
                if (IsLegal(hand[i], hand, trick, spadesBroken)) buffer.Add(hand[i]);
            }

            if (hand.Count > 0 && buffer.Count == 0)
                throw new InvalidOperationException("No legal moves for a non-empty hand: rule bug.");
        }

        private static bool HandIsAllSpades(IReadOnlyList<Card> hand)
        {
            for (int i = 0; i < hand.Count; i++)
            {
                if (!hand[i].IsSpade) return false;
            }
            return true;
        }

        private static bool HandContainsSuit(IReadOnlyList<Card> hand, Suit suit)
        {
            for (int i = 0; i < hand.Count; i++)
            {
                if (hand[i].Suit == suit) return true;
            }
            return false;
        }
    }
}
