using System;
using Spades.Unity.Bootstrap;
using Spades.Unity.Visuals;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Spades.Unity.UI
{
    public sealed class MainMenuPanel : PanelBase
    {
        /// <summary>Carries the number of players for the requested game.</summary>
        public event Action<int> StartRequested;

        public event Action QuitRequested;

        public MainMenuPanel(RectTransform parent, LayoutSettings layout, TweenRunner tweens, CardArt art)
            : base(parent, "MainMenu", layout, tweens, art)
        {
            UiFactory.Panel("MenuBackground", Root, art.White, layout.TableEdgeColor, stretch: true)
                .raycastTarget = true;

            Image card = AddCard("MenuCard", 720f, 560f);

            TextMeshProUGUI title = UiFactory.Text("Title", card.rectTransform, "SPADES",
                86f, layout.AccentColor, TextAlignmentOptions.Center, FontStyles.Bold);
            UiFactory.Place(title.rectTransform, 0f, 190f, 640f, 110f);

            TextMeshProUGUI subtitle = UiFactory.Text("Subtitle", card.rectTransform,
                "Full ruleset with Nil, bags and spade breaking.",
                22f, layout.MutedTextColor);
            UiFactory.Place(subtitle.rectTransform, 0f, 122f, 660f, 30f);

            Button fourPlayer = UiFactory.Button("FourPlayer", card.rectTransform, "4 Player  (partnerships)",
                art.SharpPanel, layout.AccentColor, Color.black, 28f, () => StartRequested?.Invoke(4));
            UiFactory.Place(fourPlayer.image.rectTransform, 0f, 34f, 480f, 76f);

            Button twoPlayer = UiFactory.Button("TwoPlayer", card.rectTransform, "2 Player  (head to head)",
                art.SharpPanel, new Color(0.20f, 0.24f, 0.31f), layout.TextColor, 28f,
                () => StartRequested?.Invoke(2));
            UiFactory.Place(twoPlayer.image.rectTransform, 0f, -60f, 480f, 76f);

            TextMeshProUGUI note = UiFactory.Text("Note", card.rectTransform,
                "Two-player deals through a draw phase: keep the card you see, or take the next one blind.",
                18f, layout.MutedTextColor);
            UiFactory.Place(note.rectTransform, 0f, -136f, 620f, 50f);
            note.textWrappingMode = TextWrappingModes.Normal;

            Button quit = UiFactory.Button("Quit", card.rectTransform, "Quit",
                art.SharpPanel, new Color(0.16f, 0.18f, 0.23f), layout.MutedTextColor, 22f,
                () => QuitRequested?.Invoke());
            UiFactory.Place(quit.image.rectTransform, 0f, -212f, 200f, 52f);
        }
    }
}
