using UnityEngine;

namespace Spades.Unity.Bootstrap
{
    /// <summary>
    /// Authored numbers: how big things are and how long they take.
    ///
    /// This is the whole rule for what belongs in a ScriptableObject on this project: data that
    /// is decided before the game starts and never changes during it. Game state -- hands,
    /// scores, whose turn it is -- deliberately does not live in an asset, because asset state
    /// survives between play-mode entries, cannot exist in an EditMode unit test, and would put
    /// mutable state on the wrong side of the boundary the assembly definitions enforce.
    ///
    /// Tuning game feel is a fifty-iteration loop. Doing it live in play mode without a
    /// recompile is the difference between polished and merely working.
    /// </summary>
    [CreateAssetMenu(fileName = "LayoutSettings", menuName = "Spades/Layout Settings")]
    public sealed class LayoutSettings : ScriptableObject
    {
        [Header("Card size")]
        public Vector2 CardSize = new Vector2(104f, 148f);
        public float CardCornerRadius = 10f;

        [Header("Table")]
        public float SeatRadiusX = 700f;
        public float SeatRadiusY = 360f;
        public float TrickRadius = 135f;

        [Header("Human hand")]
        public float HandSpacing = 74f;
        public float HandFanAngle = 12f;
        public float HandArcHeight = 26f;
        public float HoverLift = 34f;

        [Header("Opponent hands")]
        public float OpponentCardSpacing = 15f;
        public float OpponentCardScale = 0.62f;

        [Header("Timing")]
        public float DealDuration = 0.32f;
        public float DealStagger = 0.035f;
        public float PlayDuration = 0.30f;
        public float PlayArcHeight = 42f;
        public float TrickCollectDuration = 0.34f;
        public float TrickHoldDuration = 0.45f;
        public float BidRevealDuration = 0.22f;
        public float DrawRevealDuration = 0.25f;
        public float ScoreCountDuration = 0.7f;
        public float HoverDuration = 0.11f;
        public float ShakeDuration = 0.28f;
        public float ShakeAmplitude = 14f;

        [Header("Pacing")]
        [Tooltip("Pause before a bot acts, so its move is legible rather than instant.")]
        public float BotThinkDelay = 0.32f;

        [Header("Palette")]
        public Color TableColor = new Color(0.10f, 0.30f, 0.20f);
        public Color TableEdgeColor = new Color(0.07f, 0.21f, 0.14f);
        public Color CardFaceColor = Color.white;
        public Color CardBackColor = new Color(0.16f, 0.26f, 0.48f);
        public Color CardBackPattern = new Color(0.30f, 0.44f, 0.72f);
        public Color PanelColor = new Color(0.09f, 0.11f, 0.15f, 0.96f);
        public Color AccentColor = new Color(0.98f, 0.76f, 0.24f);
        public Color TextColor = new Color(0.93f, 0.95f, 0.97f);
        public Color MutedTextColor = new Color(0.62f, 0.67f, 0.74f);
        public Color IllegalTint = new Color(0.55f, 0.55f, 0.58f);

        /// <summary>
        /// Used when no asset is assigned, so the game runs correctly from a scene containing
        /// nothing but the bootstrap object. A missing reference should never be fatal for data
        /// that has a sensible default.
        /// </summary>
        public static LayoutSettings CreateDefault()
        {
            var settings = CreateInstance<LayoutSettings>();
            settings.name = "LayoutSettings (defaults)";
            return settings;
        }
    }
}
