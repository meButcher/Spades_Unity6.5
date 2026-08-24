using System;
using System.Collections.Generic;
using Spades.Unity.Bootstrap;
using Spades.Unity.Visuals;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Spades.Unity.UI
{
    /// <summary>
    /// Bidding zero to the hand size, with zero labelled Nil because it is not a contract of no
    /// tricks, it is a hundred-point side bet.
    /// </summary>
    public sealed class BidPanel : PanelBase
    {
        private readonly List<Button> _buttons = new List<Button>(14);
        private readonly TextMeshProUGUI _hint;
        private readonly RectTransform _grid;

        public event Action<int> BidChosen;

        public BidPanel(RectTransform parent, LayoutSettings layout, TweenRunner tweens, CardArt art)
            : base(parent, "BidPanel", layout, tweens, art)
        {
            AddScrim(0.4f);
            Image card = AddCard("BidCard", 820f, 300f, -60f);

            TextMeshProUGUI title = UiFactory.Text("Title", card.rectTransform, "Your bid",
                38f, layout.TextColor, TextAlignmentOptions.Center, FontStyles.Bold);
            UiFactory.Place(title.rectTransform, 0f, 108f, 700f, 46f);

            _hint = UiFactory.Text("Hint", card.rectTransform, "", 21f, layout.MutedTextColor);
            UiFactory.Place(_hint.rectTransform, 0f, 68f, 760f, 30f);

            _grid = UiFactory.Root("Grid", card.rectTransform);
            UiFactory.Place(_grid, 0f, -20f, 760f, 150f);
        }

        /// <summary>
        /// Rebuilds the buttons for the current rules, so a variant with a different hand size or
        /// with Nil switched off needs no change here.
        /// </summary>
        public void Configure(int handSize, bool allowNil, string hint)
        {
            for (int i = 0; i < _buttons.Count; i++)
            {
                if (_buttons[i] == null) continue;

                // Deactivated before destroying, because Destroy is deferred to the end of the
                // frame and the replacements are created immediately below.
                _buttons[i].gameObject.SetActive(false);
                UnityEngine.Object.Destroy(_buttons[i].gameObject);
            }
            _buttons.Clear();

            _hint.text = hint;

            int first = allowNil ? 0 : 1;
            int count = handSize - first + 1;

            const float buttonWidth = 76f;
            const float buttonHeight = 62f;
            const float gap = 8f;

            int perRow = Mathf.CeilToInt(count / 2f);

            for (int i = 0; i < count; i++)
            {
                int bid = first + i;
                int row = i / perRow;
                int column = i % perRow;
                int inThisRow = row == 0 ? perRow : count - perRow;

                float rowWidth = inThisRow * buttonWidth + (inThisRow - 1) * gap;
                float x = -rowWidth * 0.5f + buttonWidth * 0.5f + column * (buttonWidth + gap);
                float y = row == 0 ? 38f : -38f;

                bool isNil = bid == 0;
                int captured = bid;

                Button button = UiFactory.Button(
                    "Bid " + bid,
                    _grid,
                    isNil ? "NIL" : bid.ToString(),
                    Art.SharpPanel,
                    isNil ? Layout.AccentColor : new Color(0.20f, 0.24f, 0.31f),
                    isNil ? Color.black : Layout.TextColor,
                    isNil ? 24f : 30f,
                    () => BidChosen?.Invoke(captured));

                UiFactory.Place(button.image.rectTransform, x, y, buttonWidth, buttonHeight);
                _buttons.Add(button);
            }
        }
    }
}
