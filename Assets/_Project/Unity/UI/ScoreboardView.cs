using Spades.Unity.Bootstrap;
using Spades.Unity.Visuals;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Spades.Unity.UI
{
    /// <summary>Running scores, bags and the target, anchored to the corner of the screen.</summary>
    public sealed class ScoreboardView
    {
        private readonly TextMeshProUGUI[] _names;
        private readonly TextMeshProUGUI[] _scores;
        private readonly TextMeshProUGUI[] _bags;
        private readonly TextMeshProUGUI _header;
        private readonly TweenRunner _tweens;
        private readonly int[] _displayed;

        public ScoreboardView(RectTransform parent, LayoutSettings layout, TweenRunner tweens, CardArt art, int teamCount)
        {
            _tweens = tweens;
            _names = new TextMeshProUGUI[teamCount];
            _scores = new TextMeshProUGUI[teamCount];
            _bags = new TextMeshProUGUI[teamCount];
            _displayed = new int[teamCount];

            Image panel = UiFactory.Panel("Scoreboard", parent, art.SharpPanel, layout.PanelColor);
            UiFactory.Corner(panel.rectTransform, new Vector2(0f, 1f), 24f, -24f, 330f, 44f + teamCount * 52f);

            _header = UiFactory.Text("Header", panel.rectTransform, "First to 500",
                20f, layout.MutedTextColor, TextAlignmentOptions.Left);
            UiFactory.Place(_header.rectTransform, 0f, (44f + teamCount * 52f) * 0.5f - 24f, 290f, 26f);

            for (int t = 0; t < teamCount; t++)
            {
                float y = (44f + teamCount * 52f) * 0.5f - 62f - t * 52f;

                _names[t] = UiFactory.Text("Team" + t, panel.rectTransform, "Team " + t,
                    24f, layout.TextColor, TextAlignmentOptions.Left, FontStyles.Bold);
                UiFactory.Place(_names[t].rectTransform, -35f, y, 200f, 30f);

                _scores[t] = UiFactory.Text("Score" + t, panel.rectTransform, "0",
                    28f, layout.AccentColor, TextAlignmentOptions.Right, FontStyles.Bold);
                UiFactory.Place(_scores[t].rectTransform, 90f, y, 90f, 30f);

                _bags[t] = UiFactory.Text("Bags" + t, panel.rectTransform, "0 bags",
                    17f, layout.MutedTextColor, TextAlignmentOptions.Right);
                UiFactory.Place(_bags[t].rectTransform, 138f, y - 1f, 70f, 26f);
            }
        }

        public void SetTarget(int targetScore) => _header.text = "First to " + targetScore;

        public void SetTeamName(int teamId, string name) => _names[teamId].text = name;

        /// <summary>Snaps to a value without animating. Used when a game starts.</summary>
        public void SetScore(int teamId, int score, int bags)
        {
            _displayed[teamId] = score;
            _scores[teamId].text = score.ToString();
            _bags[teamId].text = bags + (bags == 1 ? " bag" : " bags");
        }

        /// <summary>
        /// Counts up to the new score. Cheap to write and it draws the eye to the number that
        /// changed, which a straight text swap does not.
        /// </summary>
        public void AnimateScore(int teamId, int score, int bags, float duration)
        {
            int from = _displayed[teamId];
            _displayed[teamId] = score;
            _bags[teamId].text = bags + (bags == 1 ? " bag" : " bags");

            TextMeshProUGUI label = _scores[teamId];
            _tweens.CountTo(from, score, duration, value =>
            {
                if (label != null) label.text = value.ToString();
            });
        }
    }
}
