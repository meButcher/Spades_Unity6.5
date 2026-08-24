using System;
using Spades.Core.Cards;
using Spades.Unity.Bootstrap;
using Spades.Unity.Views;
using Spades.Unity.Visuals;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Spades.Unity.UI
{
    /// <summary>
    /// The two-player draw phase: keep the card you can see, or throw it away and take the next
    /// one without looking. Existing in one mode and not the other is the point of it -- the
    /// trick engine underneath is untouched.
    /// </summary>
    public sealed class DrawPanel : PanelBase
    {
        private readonly CardView _offered;
        private readonly TextMeshProUGUI _status;
        private readonly TextMeshProUGUI _stock;

        public event Action<bool> DecisionMade;

        public DrawPanel(RectTransform parent, LayoutSettings layout, TweenRunner tweens, CardArt art)
            : base(parent, "DrawPanel", layout, tweens, art)
        {
            AddScrim(0.45f);
            Image card = AddCard("DrawCard", 660f, 460f);

            TextMeshProUGUI title = UiFactory.Text("Title", card.rectTransform, "Build your hand",
                36f, layout.TextColor, TextAlignmentOptions.Center, FontStyles.Bold);
            UiFactory.Place(title.rectTransform, 0f, 186f, 600f, 44f);

            _status = UiFactory.Text("Status", card.rectTransform,
                "Keep this card, or discard it and take the next one unseen.",
                20f, layout.MutedTextColor);
            UiFactory.Place(_status.rectTransform, 0f, 148f, 620f, 28f);

            _offered = CardView.Create(card.rectTransform, layout, art, tweens);
            UiFactory.At(_offered.Rect, 0f, 24f);
            _offered.Rect.localScale = Vector3.one * 1.35f;

            _stock = UiFactory.Text("Stock", card.rectTransform, "", 19f, layout.MutedTextColor);
            UiFactory.Place(_stock.rectTransform, 0f, -122f, 600f, 26f);

            Button keep = UiFactory.Button("Keep", card.rectTransform, "Keep", art.SharpPanel,
                Layout.AccentColor, Color.black, 26f, () => DecisionMade?.Invoke(true));
            UiFactory.Place(keep.image.rectTransform, -140f, -180f, 240f, 62f);

            Button discard = UiFactory.Button("Discard", card.rectTransform, "Discard", art.SharpPanel,
                new Color(0.20f, 0.24f, 0.31f), Layout.TextColor, 26f, () => DecisionMade?.Invoke(false));
            UiFactory.Place(discard.image.rectTransform, 140f, -180f, 240f, 62f);
        }

        public void SetOffer(Card card, int handCount, int handSize)
        {
            _offered.Bind(card);
            _offered.SetFaceUp(true);
            _stock.text = "Your hand: " + handCount + " / " + handSize;
        }
    }
}
