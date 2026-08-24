using System;
using System.Collections.Generic;
using Spades.Core.Cards;
using Spades.Core.State;

namespace Spades.Core.Players
{
    /// <summary>
    /// Parks the engine's request and waits. The UI polls the Pending* flags to decide what to
    /// unlock, and calls the matching Submit method when the player acts.
    ///
    /// This controller answers no question by itself, which is the point: the engine treats a
    /// human and an AI identically and simply does not advance until an answer arrives.
    /// </summary>
    public sealed class HumanPlayerController : IPlayerController
    {
        private readonly List<Card> _legalMoves = new List<Card>(13);

        private Action<int> _bidSubmit;
        private Action<Card> _cardSubmit;
        private Action<bool> _drawSubmit;

        public bool PendingBid => _bidSubmit != null;
        public bool PendingCard => _cardSubmit != null;
        public bool PendingDraw => _drawSubmit != null;

        /// <summary>The cards the UI should leave clickable. Empty unless PendingCard is true.</summary>
        public IReadOnlyList<Card> LegalMoves => _legalMoves;

        /// <summary>The card currently on offer during the 2-player draw phase.</summary>
        public Card OfferedCard { get; private set; }

        public SeatView CurrentView { get; private set; }

        public void RequestBid(SeatView view, Action<int> submit)
        {
            CurrentView = view;
            _bidSubmit = submit;
        }

        public void RequestCard(SeatView view, IReadOnlyList<Card> legalMoves, Action<Card> submit)
        {
            CurrentView = view;

            // Copied, not aliased: the engine reuses that buffer for the next seat's decision.
            _legalMoves.Clear();
            for (int i = 0; i < legalMoves.Count; i++) _legalMoves.Add(legalMoves[i]);

            _cardSubmit = submit;
        }

        public void RequestDrawDecision(SeatView view, Card drawn, Action<bool> submit)
        {
            CurrentView = view;
            OfferedCard = drawn;
            _drawSubmit = submit;
        }

        public bool SubmitBid(int bid)
        {
            if (_bidSubmit == null) return false;

            Action<int> submit = _bidSubmit;
            _bidSubmit = null;
            submit(bid);
            return true;
        }

        /// <summary>Returns false if no card was requested, or the card is not one of the legal moves.</summary>
        public bool SubmitCard(Card card)
        {
            if (_cardSubmit == null) return false;
            if (!IsLegal(card)) return false;

            Action<Card> submit = _cardSubmit;
            _cardSubmit = null;
            _legalMoves.Clear();
            submit(card);
            return true;
        }

        public bool SubmitDrawDecision(bool keep)
        {
            if (_drawSubmit == null) return false;

            Action<bool> submit = _drawSubmit;
            _drawSubmit = null;
            submit(keep);
            return true;
        }

        /// <summary>
        /// What the UI asks before it lets a card be clicked. It is answering from the very list
        /// the engine produced, so the greyed-out state and the engine's verdict cannot disagree.
        /// </summary>
        public bool IsLegal(Card card)
        {
            for (int i = 0; i < _legalMoves.Count; i++)
            {
                if (_legalMoves[i] == card) return true;
            }
            return false;
        }

        public void Reset()
        {
            _bidSubmit = null;
            _cardSubmit = null;
            _drawSubmit = null;
            _legalMoves.Clear();
        }
    }
}
