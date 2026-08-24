using System;
using System.Collections.Generic;
using System.Text;
using Spades.Core.State;
using Spades.Unity.Bootstrap;
using Spades.Unity.Visuals;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Spades.Unity.UI
{
    /// <summary>
    /// The end-of-hand breakdown. It spells out contract points, Nil points and bags separately,
    /// because a player who just lost a hundred points deserves to see which rule did it.
    /// </summary>
    public sealed class HandSummaryPanel : PanelBase
    {
        private readonly TextMeshProUGUI _title;
        private readonly TextMeshProUGUI[] _rows;
        private readonly StringBuilder _builder = new StringBuilder(160);

        public event Action Continued;

        public HandSummaryPanel(RectTransform parent, LayoutSettings layout, TweenRunner tweens, CardArt art, int teamCount)
            : base(parent, "HandSummary", layout, tweens, art)
        {
            AddScrim();
            Image card = AddCard("SummaryCard", 760f, 130f + teamCount * 120f);

            float half = (130f + teamCount * 120f) * 0.5f;

            _title = UiFactory.Text("Title", card.rectTransform, "Hand complete",
                36f, layout.TextColor, TextAlignmentOptions.Center, FontStyles.Bold);
            UiFactory.Place(_title.rectTransform, 0f, half - 46f, 700f, 44f);

            _rows = new TextMeshProUGUI[teamCount];
            for (int t = 0; t < teamCount; t++)
            {
                _rows[t] = UiFactory.Text("Row" + t, card.rectTransform, "",
                    22f, layout.TextColor, TextAlignmentOptions.TopLeft);
                UiFactory.Place(_rows[t].rectTransform, 0f, half - 120f - t * 120f, 660f, 110f);
                _rows[t].textWrappingMode = TextWrappingModes.Normal;
            }

            Button continueButton = UiFactory.Button("Continue", card.rectTransform, "Next hand",
                art.SharpPanel, layout.AccentColor, Color.black, 26f, () => Continued?.Invoke());
            UiFactory.Place(continueButton.image.rectTransform, 0f, -half + 52f, 260f, 62f);
        }

        public void SetSummary(int handNumber, IReadOnlyList<TeamScoreLine> lines, Func<int, string> teamName)
        {
            _title.text = "Hand " + handNumber + " complete";

            for (int i = 0; i < _rows.Length && i < lines.Count; i++)
            {
                TeamScoreLine line = lines[i];

                _builder.Clear();
                _builder.Append("<b>").Append(teamName(line.TeamId)).Append("</b>   ")
                        .Append("bid ").Append(line.Bid == 0 ? "0" : line.Bid.ToString())
                        .Append(", took ").Append(line.TricksWon).AppendLine();

                _builder.Append("Contract ").Append(Signed(line.ContractPoints));

                if (line.NilPoints != 0)
                {
                    _builder.Append("     Nil ").Append(Signed(line.NilPoints));
                }

                if (line.BagPenaltyApplied)
                {
                    _builder.Append("     <color=#E2585B>bag penalty</color>");
                }

                _builder.AppendLine();
                _builder.Append("Bags ").Append(line.BagsBefore).Append(" -> ").Append(line.BagsAfter)
                        .Append("        <b>Total ").Append(line.TotalScore).Append("</b>");

                _rows[i].text = _builder.ToString();
            }
        }

        private static string Signed(int value) => value >= 0 ? "+" + value : value.ToString();
    }
}
