using System;
using System.Collections.Generic;
using Spades.Core.Cards;

namespace Spades.Core.State
{
    /// <summary>One trick in progress. Mutable and short-lived: one instance per trick.</summary>
    public sealed class TrickState
    {
        private readonly List<PlayedCard> _cards;
        private readonly int _playerCount;

        public TrickState(int playerCount, Seat leader)
        {
            if (playerCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(playerCount), "Player count must be positive.");

            _playerCount = playerCount;
            _cards = new List<PlayedCard>(playerCount);
            Leader = leader;
        }

        public Seat Leader { get; }

        /// <summary>
        /// Null until the first card is played, which is exactly the "this seat is leading"
        /// case that the legality rules branch on. A nullable makes the compiler enforce that
        /// the caller handles it.
        /// </summary>
        public Suit? LedSuit { get; private set; }

        public IReadOnlyList<PlayedCard> Cards => _cards;
        public int Count => _cards.Count;
        public bool IsComplete => _cards.Count == _playerCount;

        public void Add(Seat seat, Card card)
        {
            if (IsComplete)
                throw new InvalidOperationException("Cannot add a card to a completed trick.");

            if (_cards.Count == 0)
                LedSuit = card.Suit;

            _cards.Add(new PlayedCard(seat, card));
        }
    }
}
