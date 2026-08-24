using System.Collections.Generic;
using Spades.Unity.Bootstrap;
using Spades.Unity.Views;
using UnityEngine;

namespace Spades.Unity.Visuals
{
    /// <summary>
    /// Fifty-two card views built once at boot and recycled for the rest of the session. Nothing
    /// is instantiated or destroyed after startup.
    ///
    /// Two reasons, and both are concrete rather than folklore. Instantiate and Destroy during
    /// play produce garbage-collection spikes in the middle of an animation, which is exactly
    /// when a hitch is most visible. And destroying an object that a tween still points at is the
    /// classic MissingReferenceException inside a completion callback; a pool plus
    /// <see cref="CardView.Recycle"/> removes the possibility rather than catching the symptom.
    /// </summary>
    public sealed class CardViewPool
    {
        private readonly List<CardView> _all;
        private readonly Stack<CardView> _free;
        private readonly Transform _parent;

        public CardViewPool(Transform parent, LayoutSettings layout, CardArt art, TweenRunner tweens, int capacity = 52)
        {
            _parent = parent;
            _all = new List<CardView>(capacity);
            _free = new Stack<CardView>(capacity);

            for (int i = 0; i < capacity; i++)
            {
                CardView view = CardView.Create(parent, layout, art, tweens);
                view.gameObject.SetActive(false);
                _all.Add(view);
                _free.Push(view);
            }
        }

        public int FreeCount => _free.Count;
        public int Capacity => _all.Count;

        public CardView Rent()
        {
            if (_free.Count == 0)
            {
                // Never expected: a game of Spades has exactly fifty-two cards. Reaching here
                // means a card was rented and never returned, so say so rather than papering
                // over it by quietly growing the pool.
                Debug.LogError("[CardViewPool] Exhausted. A card view was rented and never returned.");
                return null;
            }

            CardView view = _free.Pop();
            view.gameObject.SetActive(true);
            view.transform.SetAsLastSibling();
            return view;
        }

        public void Return(CardView view)
        {
            if (view == null) return;

            view.Recycle();
            view.Rect.SetParent(_parent, false);
            _free.Push(view);
        }

        public void ReturnAll()
        {
            _free.Clear();

            for (int i = 0; i < _all.Count; i++)
            {
                _all[i].Recycle();
                _all[i].Rect.SetParent(_parent, false);
                _free.Push(_all[i]);
            }
        }
    }
}
