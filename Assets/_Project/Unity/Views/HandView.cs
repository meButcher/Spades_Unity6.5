using System.Collections.Generic;
using Spades.Core.Cards;
using Spades.Unity.Bootstrap;
using Spades.Unity.Visuals;
using UnityEngine;

namespace Spades.Unity.Views
{
    /// <summary>
    /// One seat's cards.
    ///
    /// The human's hand holds real bound cards face up. An opponent's hand holds face-down
    /// placeholders that are only bound to a real card at the moment it is played, which means
    /// the view literally cannot leak an opponent's hand even by accident: it was never told
    /// what those cards are.
    /// </summary>
    public sealed class HandView
    {
        private readonly List<CardView> _cards = new List<CardView>(13);
        private readonly LayoutSettings _layout;
        private readonly TweenRunner _tweens;
        private readonly CardViewPool _pool;
        private readonly RectTransform _root;

        public HandView(RectTransform parent, TablePosition position, bool isHuman,
                        LayoutSettings layout, TweenRunner tweens, CardViewPool pool)
        {
            _layout = layout;
            _tweens = tweens;
            _pool = pool;

            Position = position;
            IsHuman = isHuman;

            _root = UiFactory.Root("Hand " + position, parent);
            UiFactory.At(_root, TablePositions.HandAnchor(position, layout).x,
                                TablePositions.HandAnchor(position, layout).y);
        }

        public TablePosition Position { get; }
        public bool IsHuman { get; }
        public RectTransform Root => _root;
        public int Count => _cards.Count;
        public IReadOnlyList<CardView> Cards => _cards;

        /// <summary>World-space point a dealt card should fly towards. Used by the deal animation.</summary>
        public Vector3 WorldAnchor => _root.position;

        public void Clear()
        {
            for (int i = 0; i < _cards.Count; i++) _pool.Return(_cards[i]);
            _cards.Clear();
        }

        /// <summary>Rebuilds the human's hand from the cards the engine says they hold.</summary>
        public void SetFaceUpHand(IReadOnlyList<Card> cards)
        {
            Clear();

            for (int i = 0; i < cards.Count; i++)
            {
                CardView view = _pool.Rent();
                if (view == null) return;

                view.Rect.SetParent(_root, false);
                view.Bind(cards[i]);
                view.SetFaceUp(true);
                view.HoverEnabled = IsHuman;
                view.SetInteractable(false);
                _cards.Add(view);
            }
        }

        /// <summary>Rebuilds an opponent's hand as face-down placeholders.</summary>
        public void SetHiddenHand(int count)
        {
            Clear();

            for (int i = 0; i < count; i++)
            {
                CardView view = _pool.Rent();
                if (view == null) return;

                view.Rect.SetParent(_root, false);
                view.SetFaceUp(false);
                view.HoverEnabled = false;
                view.SetInteractable(false);
                view.Rect.localScale = Vector3.one * _layout.OpponentCardScale;
                _cards.Add(view);
            }
        }

        /// <summary>
        /// Pulls a card out of the hand ready to be played. For the human it is the view already
        /// bound to that card; for an opponent it is any placeholder, bound and turned face up
        /// at this moment and not before.
        /// </summary>
        public CardView Detach(Card card)
        {
            int index = -1;

            if (IsHuman)
            {
                for (int i = 0; i < _cards.Count; i++)
                {
                    if (_cards[i].Card == card) { index = i; break; }
                }
            }

            if (index < 0) index = _cards.Count - 1;
            if (index < 0) return null;

            CardView view = _cards[index];
            _cards.RemoveAt(index);

            view.Bind(card);
            view.SetFaceUp(true);
            view.HoverEnabled = false;
            view.SetInteractable(false);

            return view;
        }

        /// <summary>Marks which cards may be clicked, straight from the engine's legal move list.</summary>
        public void ApplyLegalMoves(IReadOnlyList<Card> legalMoves)
        {
            for (int i = 0; i < _cards.Count; i++)
            {
                bool legal = false;
                for (int j = 0; j < legalMoves.Count; j++)
                {
                    if (legalMoves[j] == _cards[i].Card) { legal = true; break; }
                }

                _cards[i].SetInteractable(legal);
                _cards[i].SetDimmed(!legal);
            }
        }

        public void ClearInteractable()
        {
            for (int i = 0; i < _cards.Count; i++)
            {
                _cards[i].SetInteractable(false);
                _cards[i].SetDimmed(false);
            }
        }

        /// <summary>
        /// Positions every card. Passing animate:false snaps, which is what a rebuild wants;
        /// animate:true is used after a card leaves so the rest of the hand closes the gap.
        /// </summary>
        public void LayoutCards(bool animate, float duration = 0.2f, float stagger = 0f)
        {
            int count = _cards.Count;
            if (count == 0) return;

            bool vertical = TablePositions.IsVertical(Position);
            float spacing = IsHuman ? _layout.HandSpacing : _layout.OpponentCardSpacing;
            float span = (count - 1) * spacing;

            for (int i = 0; i < count; i++)
            {
                float offset = -span * 0.5f + i * spacing;
                Vector2 target;
                float rotation;

                if (vertical)
                {
                    target = new Vector2(0f, -offset);
                    rotation = Position == TablePosition.Left ? -90f : 90f;
                }
                else if (IsHuman)
                {
                    // A shallow arc plus a fan, which is what makes a hand read as held rather
                    // than laid out in a row.
                    float normalised = span <= 0f ? 0f : offset / (span * 0.5f);
                    target = new Vector2(offset, -_layout.HandArcHeight * normalised * normalised);
                    rotation = -_layout.HandFanAngle * normalised;
                }
                else
                {
                    target = new Vector2(offset, 0f);
                    rotation = 0f;
                }

                CardView view = _cards[i];
                view.Rect.SetSiblingIndex(i);
                view.SetRestPosition(target);

                if (animate)
                {
                    _tweens.MoveTo(view.Rect, target, duration, i * stagger, Ease.OutQuad);
                    _tweens.RotateTo(view.Rect, rotation, duration, i * stagger, Ease.OutQuad);
                }
                else
                {
                    view.Rect.anchoredPosition = target;
                    view.Rect.localRotation = Quaternion.Euler(0f, 0f, rotation);
                }
            }
        }

        /// <summary>Drops every card at a single point, ready to fly out to its place in the hand.</summary>
        public void StackAt(Vector2 localPosition)
        {
            for (int i = 0; i < _cards.Count; i++)
            {
                _cards[i].Rect.anchoredPosition = localPosition;
                _cards[i].Rect.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-8f, 8f));
            }
        }

        /// <summary>Converts a world point into this hand's local space, for deal animations.</summary>
        public Vector2 WorldToLocal(Vector3 worldPosition)
        {
            return _root.InverseTransformPoint(worldPosition);
        }
    }
}
