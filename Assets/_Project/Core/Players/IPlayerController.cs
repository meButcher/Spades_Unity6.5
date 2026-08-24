using System;
using System.Collections.Generic;
using Spades.Core.Cards;
using Spades.Core.State;

namespace Spades.Core.Players
{
    /// <summary>
    /// The seam between the engine and whoever is making the decisions.
    ///
    /// Two implementations ship: AI and human. A third, RemotePlayerController, is the whole
    /// multiplayer story: one class that forwards the request over the network and calls
    /// submit when the answer arrives, with no change to anything else.
    ///
    /// Note what is passed in: a SeatView, never GameState. An implementation physically cannot
    /// look at another seat's cards because it was never handed them.
    ///
    /// Note also what submit must NOT do: execute. It enqueues. See GameLoop.Advance.
    /// </summary>
    public interface IPlayerController
    {
        void RequestBid(SeatView view, Action<int> submit);

        /// <summary>
        /// <paramref name="legalMoves"/> is a buffer owned by the engine and reused between
        /// decisions. An implementation that stores it rather than reading it immediately must
        /// copy it first.
        /// </summary>
        void RequestCard(SeatView view, IReadOnlyList<Card> legalMoves, Action<Card> submit);

        /// <summary>Keep the drawn card, or discard it and take the next sight-unseen. 2-player only.</summary>
        void RequestDrawDecision(SeatView view, Card drawn, Action<bool> submit);
    }
}
