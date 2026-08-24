using System.Collections.Generic;
using Spades.Core.Cards;
using Spades.Core.State;

namespace Spades.Core.Events
{
    /// <summary>
    /// The event vocabulary.
    ///
    /// All of them are readonly structs: short-lived value messages created many times per
    /// hand, so keeping them off the heap is free. They do get boxed on the way into the queue,
    /// because the queue holds IGameEvent. At roughly sixty events per hand that is not worth a
    /// generic visitor to avoid, and knowing the cost while judging it irrelevant is a better
    /// position than either ignoring it or engineering around it.
    /// </summary>
    public readonly struct HandStarted : IGameEvent
    {
        public HandStarted(int handNumber, Seat dealer)
        {
            HandNumber = handNumber;
            Dealer = dealer;
        }

        public int HandNumber { get; }
        public Seat Dealer { get; }
    }

    /// <summary>
    /// A seat received its starting hand.
    ///
    /// Note that this event carries the cards. In a networked build it is the one event the
    /// server filters per client: every other event in this file is already public information.
    /// </summary>
    public readonly struct HandDealt : IGameEvent
    {
        public HandDealt(Seat seat, IReadOnlyList<Card> cards)
        {
            Seat = seat;
            Cards = cards;
        }

        public Seat Seat { get; }
        public IReadOnlyList<Card> Cards { get; }
    }

    /// <summary>The 2-player draw phase has begun and the stock is face down.</summary>
    public readonly struct DrawPhaseStarted : IGameEvent
    {
        public DrawPhaseStarted(int stockCount)
        {
            StockCount = stockCount;
        }

        public int StockCount { get; }
    }

    /// <summary>A seat is looking at the top card of the stock and must keep or discard it.</summary>
    public readonly struct CardOffered : IGameEvent
    {
        public CardOffered(Seat seat, Card card, int stockCount)
        {
            Seat = seat;
            Card = card;
            StockCount = stockCount;
        }

        public Seat Seat { get; }
        public Card Card { get; }
        public int StockCount { get; }
    }

    /// <summary>
    /// A draw turn resolved. <see cref="Card"/> is the card that actually entered the hand,
    /// which is the blind next card when the offer was declined.
    /// </summary>
    public readonly struct CardDrawn : IGameEvent
    {
        public CardDrawn(Seat seat, Card card, bool kept, Card discarded, int stockCount)
        {
            Seat = seat;
            Card = card;
            Kept = kept;
            Discarded = discarded;
            StockCount = stockCount;
        }

        public Seat Seat { get; }
        public Card Card { get; }
        public bool Kept { get; }

        /// <summary>The declined card, face up on the discard pile. Meaningless when Kept is true.</summary>
        public Card Discarded { get; }

        public int StockCount { get; }
    }

    public readonly struct BiddingStarted : IGameEvent
    {
        public BiddingStarted(Seat first)
        {
            First = first;
        }

        public Seat First { get; }
    }

    public readonly struct BidPlaced : IGameEvent
    {
        public BidPlaced(Seat seat, int bid)
        {
            Seat = seat;
            Bid = bid;
        }

        public Seat Seat { get; }
        public int Bid { get; }
        public bool IsNil => Bid == 0;
    }

    public readonly struct BiddingComplete : IGameEvent
    {
        public BiddingComplete(IReadOnlyList<int> bids)
        {
            Bids = bids;
        }

        public IReadOnlyList<int> Bids { get; }
    }

    public readonly struct TrickStarted : IGameEvent
    {
        public TrickStarted(Seat leader, int trickNumber)
        {
            Leader = leader;
            TrickNumber = trickNumber;
        }

        public Seat Leader { get; }
        public int TrickNumber { get; }
    }

    /// <summary>The turn passed to a seat without anything else changing.</summary>
    public readonly struct TurnChanged : IGameEvent
    {
        public TurnChanged(Seat seat)
        {
            Seat = seat;
        }

        public Seat Seat { get; }
    }

    public readonly struct CardPlayed : IGameEvent
    {
        public CardPlayed(Seat seat, Card card, bool brokeSpades)
        {
            Seat = seat;
            Card = card;
            BrokeSpades = brokeSpades;
        }

        public Seat Seat { get; }
        public Card Card { get; }

        /// <summary>True only on the card that broke spades, so the view can announce it once.</summary>
        public bool BrokeSpades { get; }
    }

    public readonly struct TrickWon : IGameEvent
    {
        public TrickWon(Seat winner, IReadOnlyList<PlayedCard> cards, int trickNumber)
        {
            Winner = winner;
            Cards = cards;
            TrickNumber = trickNumber;
        }

        public Seat Winner { get; }
        public IReadOnlyList<PlayedCard> Cards { get; }
        public int TrickNumber { get; }
    }

    public readonly struct HandScored : IGameEvent
    {
        public HandScored(int handNumber, IReadOnlyList<TeamScoreLine> lines)
        {
            HandNumber = handNumber;
            Lines = lines;
        }

        public int HandNumber { get; }
        public IReadOnlyList<TeamScoreLine> Lines { get; }
    }

    /// <summary>
    /// Named GameEnded rather than GameOver so it never reads ambiguously against
    /// GamePhase.GameOver at a call site.
    /// </summary>
    public readonly struct GameEnded : IGameEvent
    {
        public GameEnded(int winningTeamId, IReadOnlyList<int> finalScores)
        {
            WinningTeamId = winningTeamId;
            FinalScores = finalScores;
        }

        public int WinningTeamId { get; }
        public IReadOnlyList<int> FinalScores { get; }
    }
}
