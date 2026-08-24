using Spades.Unity.Bootstrap;
using Spades.Unity.Visuals;
using UnityEngine;
using UnityEngine.UI;

namespace Spades.Unity.UI
{
    /// <summary>
    /// Shared show and hide behaviour for the overlay panels. A CanvasGroup does the fading and
    /// also switches raycasting off, so a panel that is on its way out cannot swallow a click.
    /// </summary>
    public abstract class PanelBase
    {
        protected PanelBase(RectTransform parent, string name, LayoutSettings layout, TweenRunner tweens, CardArt art)
        {
            Layout = layout;
            Tweens = tweens;
            Art = art;

            Root = UiFactory.Stretch(name, parent);
            Group = UiFactory.Group(Root);
            Group.alpha = 0f;
            Group.blocksRaycasts = false;
            Group.interactable = false;
            Root.gameObject.SetActive(false);
        }

        protected LayoutSettings Layout { get; }
        protected TweenRunner Tweens { get; }
        protected CardArt Art { get; }
        protected RectTransform Root { get; }
        protected CanvasGroup Group { get; }

        public bool IsVisible { get; private set; }

        /// <summary>A dimmed full-screen backdrop, for panels that should stop play behind them.</summary>
        protected Image AddScrim(float alpha = 0.55f)
        {
            Image scrim = UiFactory.Panel("Scrim", Root, Art.White, new Color(0f, 0f, 0f, alpha), stretch: true);
            scrim.raycastTarget = true;   // swallows clicks aimed at the table underneath
            return scrim;
        }

        protected Image AddCard(string title, float width, float height, float y = 0f)
        {
            Image card = UiFactory.Panel(title, Root, Art.Panel, Layout.PanelColor);
            UiFactory.Place(card.rectTransform, 0f, y, width, height);
            return card;
        }

        public virtual void Show()
        {
            if (IsVisible) return;
            IsVisible = true;

            Root.gameObject.SetActive(true);
            Group.blocksRaycasts = true;
            Group.interactable = true;
            Tweens.Fade(Group, 1f, 0.16f);
        }

        public virtual void Hide()
        {
            if (!IsVisible) return;
            IsVisible = false;

            Group.blocksRaycasts = false;
            Group.interactable = false;
            Tweens.Fade(Group, 0f, 0.14f, 0f, Ease.Linear, () =>
            {
                if (!IsVisible && Root != null) Root.gameObject.SetActive(false);
            });
        }

        /// <summary>Hides without animating. Used when tearing a game down.</summary>
        public void HideImmediate()
        {
            IsVisible = false;
            Tweens.KillFor(Group);
            Group.alpha = 0f;
            Group.blocksRaycasts = false;
            Group.interactable = false;
            Root.gameObject.SetActive(false);
        }
    }
}
