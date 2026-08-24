using Spades.Unity.Bootstrap;
using Spades.Unity.Visuals;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Spades.Unity.UI
{
    /// <summary>
    /// A short announcement across the middle of the table: spades broken, a Nil declared, a
    /// trick won. It fades itself out, so nothing has to remember to clear it.
    /// </summary>
    public sealed class MessageBanner
    {
        private readonly CanvasGroup _group;
        private readonly TextMeshProUGUI _text;
        private readonly TweenRunner _tweens;

        public MessageBanner(RectTransform parent, LayoutSettings layout, TweenRunner tweens, CardArt art)
        {
            _tweens = tweens;

            Image panel = UiFactory.Panel("Banner", parent, art.SharpPanel, new Color(0f, 0f, 0f, 0.72f));
            UiFactory.Place(panel.rectTransform, 0f, 250f, 520f, 62f);

            _group = UiFactory.Group(panel.rectTransform);
            _group.alpha = 0f;
            _group.blocksRaycasts = false;

            _text = UiFactory.Text("BannerText", panel.rectTransform, "", 30f, layout.AccentColor,
                TextAlignmentOptions.Center, FontStyles.Bold);
            UiFactory.Place(_text.rectTransform, 0f, 0f, 500f, 50f);
        }

        public void Flash(string message, float holdSeconds = 0.9f)
        {
            _text.text = message;
            _tweens.KillFor(_group);

            _group.alpha = 0f;
            _tweens.Fade(_group, 1f, 0.14f, 0f, Ease.Linear,
                () => _tweens.Fade(_group, 0f, 0.3f, holdSeconds));
        }

        public void Clear()
        {
            _tweens.KillFor(_group);
            _group.alpha = 0f;
        }
    }
}
