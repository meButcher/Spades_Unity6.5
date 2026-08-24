using System;
using Spades.Core.Cards;
using Spades.Core.State;

namespace Spades.Core.Rules
{
    public static class TrickResolver
    {
        /// <summary>
        /// The highest spade wins; if no spade was played, the highest card of the led suit
        /// wins. Implemented as a single reigning-champion pass rather than two searches.
        /// </summary>
        public static Seat DetermineWinner(TrickState trick)
        {
            if (trick == null) throw new ArgumentNullException(nameof(trick));
            if (trick.Count == 0)
                throw new ArgumentException("Cannot determine the winner of an empty trick.", nameof(trick));

            PlayedCard best = trick.Cards[0];
            for (int i = 1; i < trick.Count; i++)
            {
                PlayedCard challenger = trick.Cards[i];
                if (Beats(challenger.Card, best.Card)) best = challenger;
            }

            return best.Seat;
        }

        /// <summary>
        /// A spade beats a non-spade. Between two cards of the same suit, the higher rank wins.
        /// Anything else loses, and that last clause is what makes an off-suit discard lose
        /// without a special case: after the first card, the incumbent is always either the led
        /// suit or a spade, so a card of any third suit cannot be comparable to it.
        /// </summary>
        public static bool Beats(Card challenger, Card incumbent)
        {
            if (challenger.IsSpade != incumbent.IsSpade) return challenger.IsSpade;
            if (challenger.Suit != incumbent.Suit) return false;
            return challenger.Rank > incumbent.Rank;
        }

        /// <summary>The card currently winning the trick. Used by the AI to decide whether it can beat it.</summary>
        public static PlayedCard CurrentBest(TrickState trick)
        {
            if (trick == null) throw new ArgumentNullException(nameof(trick));
            if (trick.Count == 0)
                throw new ArgumentException("An empty trick has no best card.", nameof(trick));

            PlayedCard best = trick.Cards[0];
            for (int i = 1; i < trick.Count; i++)
            {
                if (Beats(trick.Cards[i].Card, best.Card)) best = trick.Cards[i];
            }

            return best;
        }
    }
}
