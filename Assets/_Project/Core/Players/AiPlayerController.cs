using System;
using System.Collections.Generic;
using Spades.Core.Ai;
using Spades.Core.Cards;
using Spades.Core.State;

namespace Spades.Core.Players
{
    /// <summary>
    /// Composes the three strategies and answers immediately.
    ///
    /// There is no thinking pause here on purpose. A delay in the controller would put timing
    /// inside the engine, where it cannot be unit-tested and would make a five-hundred-game
    /// simulation take five hundred game-lengths. The pause the player sees is added by the
    /// view, which is the only layer that knows what a second is.
    /// </summary>
    public sealed class AiPlayerController : IPlayerController
    {
        private readonly IBiddingStrategy _bidding;
        private readonly ICardStrategy _cards;
        private readonly IDrawStrategy _draw;

        public AiPlayerController(IBiddingStrategy bidding, ICardStrategy cards, IDrawStrategy draw)
        {
            _bidding = bidding ?? throw new ArgumentNullException(nameof(bidding));
            _cards = cards ?? throw new ArgumentNullException(nameof(cards));
            _draw = draw ?? throw new ArgumentNullException(nameof(draw));
        }

        /// <summary>Convenience factory for the default bot.</summary>
        public static AiPlayerController CreateDefault()
        {
            return new AiPlayerController(
                new HeuristicBiddingStrategy(),
                new HeuristicCardStrategy(),
                new HeuristicDrawStrategy());
        }

        public void RequestBid(SeatView view, Action<int> submit)
        {
            submit(_bidding.ChooseBid(view, view.Rules));
        }

        public void RequestCard(SeatView view, IReadOnlyList<Card> legalMoves, Action<Card> submit)
        {
            // Read immediately, never stored: the engine reuses this buffer for the next seat.
            submit(_cards.ChooseCard(view, legalMoves));
        }

        public void RequestDrawDecision(SeatView view, Card drawn, Action<bool> submit)
        {
            submit(_draw.ShouldKeep(view, drawn));
        }
    }
}
