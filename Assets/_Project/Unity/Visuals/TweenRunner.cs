using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Spades.Unity.Visuals
{
    /// <summary>
    /// A small update-driven tween system.
    ///
    /// Everything the presenter animates goes through here, and the presenter waits on
    /// <see cref="WaitAll"/> rather than guessing durations with WaitForSeconds. That is the
    /// property that matters: the view never has to know how long an animation takes, so
    /// retiming the game cannot desynchronise it.
    ///
    /// Two habits are built in because they are the two ways tween code normally crashes:
    /// a tween whose target has been destroyed or recycled is dropped on the next frame rather
    /// than throwing from a completion callback, and <see cref="KillFor"/> lets a pooled object
    /// cancel its own tweens the moment it is returned.
    /// </summary>
    public sealed class TweenRunner : MonoBehaviour
    {
        private sealed class Tween
        {
            public object Owner;
            public Transform Target;      // null for non-transform tweens; used for the liveness check
            public float Elapsed;
            public float Delay;
            public float Duration;
            public Ease Ease;
            public Action<float> Apply;
            public Action OnComplete;
        }

        private readonly List<Tween> _active = new List<Tween>(128);
        private readonly Stack<Tween> _pool = new Stack<Tween>(128);
        // Completion callbacks are collected here and invoked after the active list is stable,
        // so a callback is free to start new tweens without mutating the list mid-iteration.
        private readonly List<Action> _completions = new List<Action>(32);

        public int ActiveCount => _active.Count;

        /// <summary>Global speed multiplier, so the whole game can be sped up for testing.</summary>
        public float TimeScale { get; set; } = 1f;

        private void Update()
        {
            if (_active.Count == 0) return;

            float dt = Time.unscaledDeltaTime * TimeScale;
            _completions.Clear();

            for (int i = _active.Count - 1; i >= 0; i--)
            {
                Tween tween = _active[i];

                // The target was destroyed or recycled mid-flight. Drop it silently: this is the
                // MissingReferenceException-inside-a-callback bug, removed rather than caught.
                if (tween.Target == null && tween.Owner is Component)
                {
                    Recycle(tween, i);
                    continue;
                }

                tween.Elapsed += dt;
                float local = tween.Elapsed - tween.Delay;
                if (local < 0f) continue;

                float u = tween.Duration <= 0f ? 1f : Mathf.Clamp01(local / tween.Duration);
                tween.Apply(Easing.Evaluate(tween.Ease, u));

                if (u < 1f) continue;

                // Captured before recycling, which clears the tween's fields.
                if (tween.OnComplete != null) _completions.Add(tween.OnComplete);
                Recycle(tween, i);
            }

            for (int i = 0; i < _completions.Count; i++) _completions[i].Invoke();
            _completions.Clear();
        }

        // -- public API -------------------------------------------------------------------------

        public void MoveTo(RectTransform target, Vector2 to, float duration,
                           float delay = 0f, Ease ease = Ease.OutQuad, Action onComplete = null)
        {
            if (target == null) return;
            Vector2 from = target.anchoredPosition;
            Add(target, target, duration, delay, ease,
                t => { if (target != null) target.anchoredPosition = Vector2.LerpUnclamped(from, to, t); },
                onComplete);
        }

        /// <summary>
        /// A move that bows out perpendicular to the path. A card that arcs into the trick reads
        /// as a deliberate throw; the same move in a straight line reads as a slide.
        /// </summary>
        public void ArcTo(RectTransform target, Vector2 to, float archHeight, float duration,
                          float delay = 0f, Ease ease = Ease.InOutQuad, Action onComplete = null)
        {
            if (target == null) return;

            Vector2 from = target.anchoredPosition;
            Vector2 delta = to - from;
            Vector2 normal = new Vector2(-delta.y, delta.x).normalized;

            Add(target, target, duration, delay, ease,
                t =>
                {
                    if (target == null) return;
                    Vector2 point = Vector2.LerpUnclamped(from, to, t);
                    float bow = Mathf.Sin(t * Mathf.PI) * archHeight;
                    target.anchoredPosition = point + normal * bow;
                },
                onComplete);
        }

        public void RotateTo(RectTransform target, float toZ, float duration,
                             float delay = 0f, Ease ease = Ease.OutQuad, Action onComplete = null)
        {
            if (target == null) return;
            float from = target.localEulerAngles.z;
            if (from > 180f) from -= 360f;

            Add(target, target, duration, delay, ease,
                t => { if (target != null) target.localRotation = Quaternion.Euler(0f, 0f, Mathf.LerpUnclamped(from, toZ, t)); },
                onComplete);
        }

        public void ScaleTo(RectTransform target, Vector3 to, float duration,
                            float delay = 0f, Ease ease = Ease.OutBack, Action onComplete = null)
        {
            if (target == null) return;
            Vector3 from = target.localScale;

            Add(target, target, duration, delay, ease,
                t => { if (target != null) target.localScale = Vector3.LerpUnclamped(from, to, t); },
                onComplete);
        }

        public void Fade(CanvasGroup group, float to, float duration,
                         float delay = 0f, Ease ease = Ease.Linear, Action onComplete = null)
        {
            if (group == null) return;
            float from = group.alpha;

            Add(group, group.transform, duration, delay, ease,
                t => { if (group != null) group.alpha = Mathf.LerpUnclamped(from, to, t); },
                onComplete);
        }

        public void FadeGraphic(Graphic graphic, float to, float duration,
                                float delay = 0f, Ease ease = Ease.Linear, Action onComplete = null)
        {
            if (graphic == null) return;
            Color from = graphic.color;
            Color target = new Color(from.r, from.g, from.b, to);

            Add(graphic, graphic.transform, duration, delay, ease,
                t => { if (graphic != null) graphic.color = Color.LerpUnclamped(from, target, t); },
                onComplete);
        }

        /// <summary>A short horizontal shake. The feedback for an illegal move, without a modal.</summary>
        public void Shake(RectTransform target, float amplitude, float duration, Action onComplete = null)
        {
            if (target == null) return;
            Vector2 origin = target.anchoredPosition;

            Add(target, target, duration, 0f, Ease.Linear,
                t =>
                {
                    if (target == null) return;
                    float decay = 1f - t;
                    float offset = Mathf.Sin(t * Mathf.PI * 8f) * amplitude * decay;
                    target.anchoredPosition = origin + new Vector2(offset, 0f);
                },
                () =>
                {
                    if (target != null) target.anchoredPosition = origin;
                    onComplete?.Invoke();
                });
        }

        /// <summary>Counts an integer from one value to another. Used by the scoreboard.</summary>
        public void CountTo(int from, int to, float duration, Action<int> onValue, Action onComplete = null)
        {
            Add(null, null, duration, 0f, Ease.OutCubic,
                t => onValue(Mathf.RoundToInt(Mathf.LerpUnclamped(from, to, t))),
                onComplete);
        }

        /// <summary>A pure delay, so a presenter can hold a beat without a WaitForSeconds.</summary>
        public void Wait(float duration, Action onComplete)
        {
            Add(null, null, duration, 0f, Ease.Linear, _ => { }, onComplete);
        }

        /// <summary>
        /// Yields until nothing is animating. This is the barrier the presenter waits on after
        /// every event, and the reason no part of the view needs to know a duration.
        /// </summary>
        public IEnumerator WaitAll(float timeout = 10f)
        {
            float waited = 0f;
            while (_active.Count > 0)
            {
                waited += Time.unscaledDeltaTime;
                if (waited > timeout)
                {
                    Debug.LogWarning("[TweenRunner] Timed out waiting for " + _active.Count +
                                     " tween(s); forcing completion so the presenter cannot deadlock.");
                    KillAll();
                    yield break;
                }

                yield return null;
            }
        }

        /// <summary>Cancels every tween belonging to one object. Called when a card is pooled.</summary>
        public void KillFor(object owner)
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(_active[i].Owner, owner)) Recycle(_active[i], i);
            }
        }

        public void KillAll()
        {
            for (int i = _active.Count - 1; i >= 0; i--) Recycle(_active[i], i);
        }

        // -- internals ---------------------------------------------------------------------------

        private void Add(object owner, Transform target, float duration, float delay, Ease ease,
                         Action<float> apply, Action onComplete)
        {
            Tween tween = _pool.Count > 0 ? _pool.Pop() : new Tween();

            tween.Owner = owner;
            tween.Target = target;
            tween.Elapsed = 0f;
            tween.Delay = delay;
            tween.Duration = duration;
            tween.Ease = ease;
            tween.Apply = apply;
            tween.OnComplete = onComplete;

            _active.Add(tween);
        }

        private void Recycle(Tween tween, int index)
        {
            _active.RemoveAt(index);

            tween.Apply = null;
            tween.OnComplete = null;
            tween.Owner = null;
            tween.Target = null;

            _pool.Push(tween);
        }
    }
}
