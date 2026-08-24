using System;
using Spades.Core.Cards;
using Spades.Unity.Bootstrap;
using Spades.Unity.Visuals;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Spades.Unity.Views
{
    /// <summary>
    /// One card on the table. Built once at boot and then rebound, never re-created.
    ///
    /// Everything that can go wrong with a pooled, tweened object is handled in one place here:
    /// returning a card to the pool kills its tweens first, so no completion callback can fire
    /// against a recycled transform.
    /// </summary>
    public sealed class CardView : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        private LayoutSettings _layout;
        private CardArt _art;
        private TweenRunner _tweens;

        private Image _background;
        private RectTransform _faceRoot;
        private RectTransform _backRoot;

        private TextMeshProUGUI _cornerRank;
        private TextMeshProUGUI _cornerRankFlipped;
        private Image _cornerPip;
        private Image _cornerPipFlipped;
        private Image _centrePip;
        private Image _backPattern;

        private bool _hovered;
        private float _restY;

        public event Action<CardView> Clicked;

        public RectTransform Rect { get; private set; }
        public Card Card { get; private set; }
        public bool FaceUp { get; private set; }

        /// <summary>Whether clicking this card does anything. Set by HandView from the legal moves.</summary>
        public bool Interactable { get; private set; }

        /// <summary>Whether hovering lifts the card. Only the human's own hand does.</summary>
        public bool HoverEnabled { get; set; }

        public static CardView Create(Transform parent, LayoutSettings layout, CardArt art, TweenRunner tweens)
        {
            RectTransform rect = UiFactory.Root("Card", parent);
            UiFactory.Size(rect, layout.CardSize.x, layout.CardSize.y);

            var view = rect.gameObject.AddComponent<CardView>();
            view.Build(rect, layout, art, tweens);
            return view;
        }

        private void Build(RectTransform rect, LayoutSettings layout, CardArt art, TweenRunner tweens)
        {
            Rect = rect;
            _layout = layout;
            _art = art;
            _tweens = tweens;

            _background = rect.gameObject.AddComponent<Image>();
            _background.sprite = art.Panel;
            _background.type = Image.Type.Sliced;
            _background.color = layout.CardFaceColor;
            _background.raycastTarget = true;   // the whole card is the click target

            float w = layout.CardSize.x;
            float h = layout.CardSize.y;

            // -- face --------------------------------------------------------------------------
            _faceRoot = UiFactory.Stretch("Face", rect);

            _cornerRank = UiFactory.Text("Rank", _faceRoot, "A", w * 0.30f, Color.black,
                TextAlignmentOptions.Center, FontStyles.Bold);
            UiFactory.Place(_cornerRank.rectTransform, -w * 0.32f, h * 0.34f, w * 0.42f, h * 0.24f);

            _cornerPip = UiFactory.Pip("CornerPip", _faceRoot, art.PipFor(Suit.Spades), Color.black, w * 0.20f);
            UiFactory.At(_cornerPip.rectTransform, -w * 0.32f, h * 0.16f);

            _centrePip = UiFactory.Pip("CentrePip", _faceRoot, art.PipFor(Suit.Spades), Color.black, w * 0.52f);
            UiFactory.At(_centrePip.rectTransform, w * 0.06f, -h * 0.04f);

            _cornerRankFlipped = UiFactory.Text("RankFlipped", _faceRoot, "A", w * 0.30f, Color.black,
                TextAlignmentOptions.Center, FontStyles.Bold);
            UiFactory.Place(_cornerRankFlipped.rectTransform, w * 0.32f, -h * 0.34f, w * 0.42f, h * 0.24f);
            _cornerRankFlipped.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 180f);

            _cornerPipFlipped = UiFactory.Pip("CornerPipFlipped", _faceRoot, art.PipFor(Suit.Spades), Color.black, w * 0.20f);
            UiFactory.At(_cornerPipFlipped.rectTransform, w * 0.32f, -h * 0.16f);
            _cornerPipFlipped.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 180f);

            // -- back --------------------------------------------------------------------------
            _backRoot = UiFactory.Stretch("Back", rect);

            Image backFill = UiFactory.Panel("BackFill", _backRoot, art.Panel, layout.CardBackColor);
            backFill.rectTransform.anchorMin = Vector2.zero;
            backFill.rectTransform.anchorMax = Vector2.one;
            backFill.rectTransform.offsetMin = new Vector2(5f, 5f);
            backFill.rectTransform.offsetMax = new Vector2(-5f, -5f);

            _backPattern = UiFactory.Pip("BackPip", _backRoot, art.PipFor(Suit.Spades), layout.CardBackPattern, w * 0.46f);

            SetFaceUp(false);
        }

        /// <summary>
        /// The single point where a card learns what it is. Swapping to an imported card atlas
        /// later is a change to this method and nothing else in the project.
        /// </summary>
        public void Bind(Card card)
        {
            Card = card;

            Color ink = CardArt.ColorFor(card.Suit);
            Sprite pip = _art.PipFor(card.Suit);

            _cornerRank.text = card.RankGlyph;
            _cornerRank.color = ink;
            _cornerRankFlipped.text = card.RankGlyph;
            _cornerRankFlipped.color = ink;

            _cornerPip.sprite = pip;
            _cornerPip.color = ink;
            _cornerPipFlipped.sprite = pip;
            _cornerPipFlipped.color = ink;
            _centrePip.sprite = pip;
            _centrePip.color = ink;

            SetDimmed(false);
        }

        public void SetFaceUp(bool faceUp)
        {
            FaceUp = faceUp;
            _faceRoot.gameObject.SetActive(faceUp);
            _backRoot.gameObject.SetActive(!faceUp);
            _background.color = faceUp ? _layout.CardFaceColor : _layout.CardBackColor;
        }

        /// <summary>
        /// Whether a click on this card does anything. Deliberately separate from the greying-out
        /// in <see cref="SetDimmed"/>: a card in a freshly dealt hand is not clickable yet, but it
        /// is not illegal either, and showing the whole hand greyed before bidding is finished
        /// would say the wrong thing.
        /// </summary>
        public void SetInteractable(bool interactable)
        {
            Interactable = interactable;
        }

        /// <summary>
        /// Greys a card the rules will not accept. The state comes from the engine's own list of
        /// legal moves, so what looks playable and what is playable cannot disagree.
        /// </summary>
        public void SetDimmed(bool dimmed)
        {
            Color tint = dimmed ? _layout.IllegalTint : _layout.CardFaceColor;
            if (FaceUp) _background.color = tint;

            float alpha = dimmed ? 0.55f : 1f;
            SetAlpha(_cornerRank, alpha);
            SetAlpha(_cornerRankFlipped, alpha);
            SetAlpha(_cornerPip, alpha);
            SetAlpha(_cornerPipFlipped, alpha);
            SetAlpha(_centrePip, alpha);
        }

        /// <summary>Records where the card sits so a hover lift knows where to return to.</summary>
        public void SetRestPosition(Vector2 position)
        {
            _restY = position.y;
            _hovered = false;
        }

        public void Shake()
        {
            _tweens.Shake(Rect, _layout.ShakeAmplitude, _layout.ShakeDuration);
        }

        /// <summary>Called by the pool. Kills tweens first: that is the whole trick to safe pooling.</summary>
        public void Recycle()
        {
            _tweens.KillFor(this);
            Clicked = null;
            _hovered = false;
            HoverEnabled = false;
            Interactable = false;
            Rect.localScale = Vector3.one;
            Rect.localRotation = Quaternion.identity;
            SetDimmed(false);
            gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            // Belt and braces for the same problem: a card disabled by any other route also
            // drops its tweens rather than leaving one running against a hidden transform.
            if (_tweens != null) _tweens.KillFor(this);
        }

        // -- pointer -------------------------------------------------------------------------

        public void OnPointerClick(PointerEventData eventData)
        {
            Clicked?.Invoke(this);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!HoverEnabled || _hovered) return;
            _hovered = true;
            _tweens.MoveTo(Rect, new Vector2(Rect.anchoredPosition.x, _restY + _layout.HoverLift),
                _layout.HoverDuration, 0f, Ease.OutQuad);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!_hovered) return;
            _hovered = false;
            _tweens.MoveTo(Rect, new Vector2(Rect.anchoredPosition.x, _restY),
                _layout.HoverDuration, 0f, Ease.OutQuad);
        }

        private static void SetAlpha(Graphic graphic, float alpha)
        {
            Color color = graphic.color;
            color.a = alpha;
            graphic.color = color;
        }
    }
}
