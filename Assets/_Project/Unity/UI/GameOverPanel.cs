using System;
using System.Collections.Generic;
using System.Text;
using Spades.Unity.Bootstrap;
using Spades.Unity.Visuals;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Spades.Unity.UI
{
    public sealed class GameOverPanel : PanelBase
    {
        private readonly TextMeshProUGUI _title;
        private readonly TextMeshProUGUI _detail;
        private readonly StringBuilder _builder = new StringBuilder(120);

        public event Action PlayAgain;
        public event Action MainMenu;

        public GameOverPanel(RectTransform parent, LayoutSettings layout, TweenRunner tweens, CardArt art)
            : base(parent, "GameOver", layout, tweens, art)
        {
            AddScrim(0.7f);
            Image card = AddCard("GameOverCard", 700f, 420f);

            _title = UiFactory.Text("Title", card.rectTransform, "", 54f, layout.AccentColor,
                TextAlignmentOptions.Center, FontStyles.Bold);
            UiFactory.Place(_title.rectTransform, 0f, 120f, 640f, 70f);

            _detail = UiFactory.Text("Detail", card.rectTransform, "", 26f, layout.TextColor);
            UiFactory.Place(_detail.rectTransform, 0f, 30f, 640f, 90f);
            _detail.textWrappingMode = TextWrappingModes.Normal;

            Button again = UiFactory.Button("PlayAgain", card.rectTransform, "Play again",
                art.SharpPanel, layout.AccentColor, Color.black, 26f, () => PlayAgain?.Invoke());
            UiFactory.Place(again.image.rectTransform, 0f, -70f, 320f, 64f);

            Button menu = UiFactory.Button("MainMenu", card.rectTransform, "Main menu",
                art.SharpPanel, new Color(0.20f, 0.24f, 0.31f), layout.TextColor, 24f, () => MainMenu?.Invoke());
            UiFactory.Place(menu.image.rectTransform, 0f, -148f, 320f, 58f);
        }

        public void SetResult(bool humanTeamWon, IReadOnlyList<int> finalScores, Func<int, string> teamName)
        {
            _title.text = humanTeamWon ? "You win" : "You lose";

            _builder.Clear();
            for (int t = 0; t < finalScores.Count; t++)
            {
                if (t > 0) _builder.Append("        ");
                _builder.Append(teamName(t)).Append("  <b>").Append(finalScores[t]).Append("</b>");
            }

            _detail.text = _builder.ToString();
        }
    }
}
