using System.Collections.Generic;
using Spades.Unity.Bootstrap;
using Spades.Unity.Visuals;
using UnityEngine;

namespace Spades.Unity.Views
{
    /// <summary>The middle of the table: the cards making up the trick in progress.</summary>
    public sealed class TrickView
    {
        private readonly List<CardView> _cards = new List<CardView>(4);
        private readonly RectTransform _root;
        private readonly LayoutSettings _layout;
        private readonly TweenRunner _tweens;
        private readonly CardViewPool _pool;

        public TrickView(RectTransform parent, LayoutSettings layout, TweenRunner tweens, CardViewPool pool)
        {
            _layout = layout;
            _tweens = tweens;
            _pool = pool;

            _root = UiFactory.Root("Trick", parent);
        }

        public int Count => _cards.Count;
        public RectTransform Root => _root;

        /// <summary>
        /// Takes a card that is currently sitting in a hand and throws it into the trick.
        ///
        /// Reparenting with worldPositionStays keeps the card exactly where the player last saw
        /// it, so the tween starts from the card's real position rather than snapping first.
        /// </summary>
        public void Play(CardView view, TablePosition from, bool arc = true)
        {
            view.Rect.SetParent(_root, true);
            view.Rect.SetAsLastSibling();

            Vector2 slot = TablePositions.TrickSlot(from, _layout);
            float rotation = TablePositions.TrickRotation(from);

            _cards.Add(view);

            _tweens.ScaleTo(view.Rect, Vector3.one, _layout.PlayDuration, 0f, Ease.OutQuad);
            _tweens.RotateTo(view.Rect, rotation, _layout.PlayDuration, 0f, Ease.OutQuad);

            if (arc)
            {
                _tweens.ArcTo(view.Rect, slot, _layout.PlayArcHeight, _layout.PlayDuration, 0f, Ease.InOutQuad);
            }
            else
            {
                _tweens.MoveTo(view.Rect, slot, _layout.PlayDuration, 0f, Ease.OutQuad);
            }
        }

        /// <summary>Gathers the trick and sends it to the winner, fading as it goes.</summary>
        public void CollectTo(Vector3 worldTarget)
        {
            Vector2 target = _root.InverseTransformPoint(worldTarget);

            for (int i = 0; i < _cards.Count; i++)
            {
                CardView view = _cards[i];
                float delay = i * 0.04f;

                _tweens.MoveTo(view.Rect, target, _layout.TrickCollectDuration, delay, Ease.InQuad);
                _tweens.ScaleTo(view.Rect, Vector3.one * 0.6f, _layout.TrickCollectDuration, delay, Ease.InQuad);
                _tweens.RotateTo(view.Rect, 0f, _layout.TrickCollectDuration, delay, Ease.InQuad);
            }
        }

        /// <summary>Called once the collect animation has finished. Every card goes back to the pool.</summary>
        public void ReturnAll()
        {
            for (int i = 0; i < _cards.Count; i++) _pool.Return(_cards[i]);
            _cards.Clear();
        }
    }
}
