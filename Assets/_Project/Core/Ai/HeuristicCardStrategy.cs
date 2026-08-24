using System.Collections.Generic;
using Spades.Core.Cards;
using Spades.Core.Rules;
using Spades.Core.State;

namespace Spades.Core.Ai
{
    /// <summary>
    /// A priority list evaluated top down: am I on a Nil, do I still need tricks, or am I
    /// avoiding bags. Each branch reduces to picking a card by one cost function.
    ///
    /// Deliberately heuristic rather than search. With thirteen cards and hidden information
    /// the correct answer is a determinised Monte-Carlo search, and it costs a day. A rule-based
    /// bot that plays plausibly and never plays illegally is the right trade for a prototype;
    /// the README says so and says what would replace it.
    /// </summary>
    public sealed class HeuristicCardStrategy : ICardStrategy
    {
        // Spades are scarce: any non-spade is cheaper to give up than any spade. Encoding that
        // as one ordering keeps every "play my lowest" branch to a single comparison.
        private const int SpadePenalty = 100;

        private readonly int[] _suitCounts = new int[4];

        public Card ChooseCard(SeatView view, IReadOnlyList<Card> legalMoves)
        {
            if (legalMoves.Count == 1) return legalMoves[0];

            TrickState trick = view.CurrentTrick;
            bool leading = trick == null || trick.Count == 0;
            bool onNil = view.MyBid == 0;

            if (onNil)
            {
                return leading
                    ? Cheapest(legalMoves)
                    : HighestCardThatLoses(view, legalMoves);
            }

            if (StillNeedsTricks(view))
            {
                return leading
                    ? LeadForATrick(view, legalMoves)
                    : FollowForATrick(view, legalMoves);
            }

            // Contract already made: every further trick is a bag, so shed value and duck.
            return leading
                ? Cheapest(legalMoves)
                : CheapestCardThatLoses(view, legalMoves);
        }

        // -- decisions ----------------------------------------------------------------------

        private Card LeadForATrick(SeatView view, IReadOnlyList<Card> legal)
        {
            // Forced into trumps: lead the highest to draw the opponents' spades out.
            if (AllSpades(legal)) return Dearest(legal);

            // A side-suit ace is the cheapest trick in the game.
            for (int i = 0; i < legal.Count; i++)
            {
                if (!legal[i].IsSpade && legal[i].Rank == Rank.Ace) return legal[i];
            }

            // Otherwise lead low from length: the long suit is the one that eventually runs
            // the opponents out and wins late tricks.
            CountSuits(view.Hand);
            Suit longest = LongestSideSuit();

            Card best = default;
            bool found = false;
            for (int i = 0; i < legal.Count; i++)
            {
                Card c = legal[i];
                if (c.Suit != longest) continue;
                if (!found || c.Rank < best.Rank)
                {
                    best = c;
                    found = true;
                }
            }

            return found ? best : Cheapest(legal);
        }

        private Card FollowForATrick(SeatView view, IReadOnlyList<Card> legal)
        {
            PlayedCard currentBest = TrickResolver.CurrentBest(view.CurrentTrick);

            // The partner is already winning it. Taking the trick from them gains the team
            // nothing and burns a high card, so duck instead.
            bool partnerIsWinning = view.Rules.TeamIdForSeat(currentBest.Seat) == view.TeamId
                                    && currentBest.Seat != view.Seat;
            if (partnerIsWinning) return CheapestCardThatLoses(view, legal);

            // Win it as cheaply as possible. The cost ordering means a low non-spade is chosen
            // over a spade, and a low spade over a high one, without a separate branch.
            Card winner = default;
            bool found = false;
            for (int i = 0; i < legal.Count; i++)
            {
                Card c = legal[i];
                if (!TrickResolver.Beats(c, currentBest.Card)) continue;
                if (!found || Cost(c) < Cost(winner))
                {
                    winner = c;
                    found = true;
                }
            }

            if (found) return winner;

            // Cannot win: throw the least valuable card away.
            return Cheapest(legal);
        }

        // -- card selection helpers ----------------------------------------------------------

        /// <summary>The highest-ranked legal card that still loses the trick. The Nil play.</summary>
        private static Card HighestCardThatLoses(SeatView view, IReadOnlyList<Card> legal)
        {
            PlayedCard currentBest = TrickResolver.CurrentBest(view.CurrentTrick);

            Card best = default;
            bool found = false;
            for (int i = 0; i < legal.Count; i++)
            {
                Card c = legal[i];
                if (TrickResolver.Beats(c, currentBest.Card)) continue;
                if (!found || c.Rank > best.Rank)
                {
                    best = c;
                    found = true;
                }
            }

            // Every legal card wins. The Nil is about to break; lose as little as possible.
            return found ? best : Cheapest(legal);
        }

        /// <summary>The cheapest legal card that still loses. The bag-avoidance play.</summary>
        private static Card CheapestCardThatLoses(SeatView view, IReadOnlyList<Card> legal)
        {
            PlayedCard currentBest = TrickResolver.CurrentBest(view.CurrentTrick);

            Card best = default;
            bool found = false;
            for (int i = 0; i < legal.Count; i++)
            {
                Card c = legal[i];
                if (TrickResolver.Beats(c, currentBest.Card)) continue;
                if (!found || Cost(c) < Cost(best))
                {
                    best = c;
                    found = true;
                }
            }

            return found ? best : Cheapest(legal);
        }

        private static Card Cheapest(IReadOnlyList<Card> legal)
        {
            Card best = legal[0];
            for (int i = 1; i < legal.Count; i++)
            {
                if (Cost(legal[i]) < Cost(best)) best = legal[i];
            }
            return best;
        }

        private static Card Dearest(IReadOnlyList<Card> legal)
        {
            Card best = legal[0];
            for (int i = 1; i < legal.Count; i++)
            {
                if (Cost(legal[i]) > Cost(best)) best = legal[i];
            }
            return best;
        }

        private static int Cost(Card card) => (card.IsSpade ? SpadePenalty : 0) + (int)card.Rank;

        private static bool AllSpades(IReadOnlyList<Card> cards)
        {
            for (int i = 0; i < cards.Count; i++)
            {
                if (!cards[i].IsSpade) return false;
            }
            return true;
        }

        // -- team bookkeeping ----------------------------------------------------------------

        /// <summary>
        /// Compares tricks that count toward the contract against the contract itself.
        /// A Nil partner's tricks are excluded on both sides: they never satisfy the contract,
        /// and their bid of zero contributes nothing to it.
        /// </summary>
        private static bool StillNeedsTricks(SeatView view)
        {
            int contractTricks = 0;
            for (int i = 0; i < view.Bids.Count; i++)
            {
                if (view.Rules.TeamIdForSeat(new Seat(i)) != view.TeamId) continue;
                if (view.Bids[i] <= 0) continue;   // NoBid or Nil
                contractTricks += view.TricksWon[i];
            }

            return contractTricks < view.TeamBid;
        }

        private void CountSuits(IReadOnlyList<Card> hand)
        {
            for (int i = 0; i < _suitCounts.Length; i++) _suitCounts[i] = 0;
            for (int i = 0; i < hand.Count; i++) _suitCounts[(int)hand[i].Suit]++;
        }

        private Suit LongestSideSuit()
        {
            var longest = Suit.Clubs;
            int bestCount = -1;

            for (int s = 0; s < 4; s++)
            {
                if ((Suit)s == Suit.Spades) continue;
                if (_suitCounts[s] > bestCount)
                {
                    bestCount = _suitCounts[s];
                    longest = (Suit)s;
                }
            }

            return longest;
        }
    }
}
