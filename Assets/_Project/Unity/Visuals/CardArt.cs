using System.Collections.Generic;
using Spades.Core.Cards;
using UnityEngine;

namespace Spades.Unity.Visuals
{
    /// <summary>
    /// Generates every sprite the game uses at boot: rounded panels and the four suit pips.
    ///
    /// Why procedural rather than an imported card pack. The pips are drawn from their implicit
    /// equations, so they are crisp at any resolution and the project carries no texture assets,
    /// no atlas and no third-party licence. It also removes a real failure mode: a font that
    /// happens not to contain U+2660 renders the whole deck as empty boxes, and no font is
    /// involved here.
    ///
    /// Swapping in an imported deck later is a change to CardView.Bind and nothing else.
    /// </summary>
    public sealed class CardArt
    {
        private const int PipResolution = 128;
        private const int Supersample = 3;

        private readonly List<Texture2D> _textures = new List<Texture2D>(8);
        private readonly Sprite[] _pips = new Sprite[4];

        public CardArt()
        {
            Panel = CreateRoundedRect(48, 10);
            SharpPanel = CreateRoundedRect(24, 4);
            White = CreateSolid();

            for (int s = 0; s < 4; s++) _pips[s] = CreatePip((Suit)s);
        }

        /// <summary>A soft-cornered nine-sliced panel. Used for cards and dialogs.</summary>
        public Sprite Panel { get; }

        /// <summary>The same shape with a tighter radius, for buttons and small chips.</summary>
        public Sprite SharpPanel { get; }

        public Sprite White { get; }

        public Sprite PipFor(Suit suit) => _pips[(int)suit];

        public static Color ColorFor(Suit suit)
        {
            return suit == Suit.Hearts || suit == Suit.Diamonds
                ? new Color(0.78f, 0.13f, 0.16f)
                : new Color(0.10f, 0.11f, 0.14f);
        }

        /// <summary>Textures are created by code, so they have to be released by code.</summary>
        public void Dispose()
        {
            for (int i = 0; i < _textures.Count; i++)
            {
                if (_textures[i] != null) Object.Destroy(_textures[i]);
            }
            _textures.Clear();
        }

        // -- generation --------------------------------------------------------------------------

        private Sprite CreateSolid()
        {
            var texture = NewTexture(4, 4);
            var pixels = new Color32[16];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
            texture.SetPixels32(pixels);
            texture.Apply();

            return Sprite.Create(texture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100f,
                0, SpriteMeshType.FullRect);
        }

        /// <summary>
        /// A rounded square with a nine-slice border equal to the corner radius, so an Image set
        /// to Sliced can stretch it to any card or panel size without distorting the corners.
        /// </summary>
        private Sprite CreateRoundedRect(int size, int radius)
        {
            var texture = NewTexture(size, size);
            var pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float coverage = RoundedRectCoverage(x, y, size, radius);
                    var alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(coverage) * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();

            var border = new Vector4(radius, radius, radius, radius);
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f,
                0, SpriteMeshType.FullRect, border);
        }

        private static float RoundedRectCoverage(int px, int py, int size, int radius)
        {
            int hits = 0;
            int samples = Supersample * Supersample;

            for (int sy = 0; sy < Supersample; sy++)
            {
                for (int sx = 0; sx < Supersample; sx++)
                {
                    float x = px + (sx + 0.5f) / Supersample;
                    float y = py + (sy + 0.5f) / Supersample;

                    // Distance from the nearest corner circle centre, clamped to the straight edges.
                    float cx = Mathf.Clamp(x, radius, size - radius);
                    float cy = Mathf.Clamp(y, radius, size - radius);
                    float dx = x - cx;
                    float dy = y - cy;

                    if (dx * dx + dy * dy <= radius * radius) hits++;
                }
            }

            return (float)hits / samples;
        }

        private Sprite CreatePip(Suit suit)
        {
            var texture = NewTexture(PipResolution, PipResolution);
            var pixels = new Color32[PipResolution * PipResolution];

            for (int py = 0; py < PipResolution; py++)
            {
                for (int px = 0; px < PipResolution; px++)
                {
                    int hits = 0;
                    for (int sy = 0; sy < Supersample; sy++)
                    {
                        for (int sx = 0; sx < Supersample; sx++)
                        {
                            float u = (px + (sx + 0.5f) / Supersample) / PipResolution;
                            float v = (py + (sy + 0.5f) / Supersample) / PipResolution;

                            // Map the pixel into [-1.35, 1.35] with y pointing up.
                            float x = (u * 2f - 1f) * 1.35f;
                            float y = (v * 2f - 1f) * 1.35f;

                            if (IsInsidePip(suit, x, y)) hits++;
                        }
                    }

                    var alpha = (byte)Mathf.RoundToInt(255f * hits / (Supersample * Supersample));
                    pixels[py * PipResolution + px] = new Color32(255, 255, 255, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();

            return Sprite.Create(texture, new Rect(0, 0, PipResolution, PipResolution),
                new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        }

        private static bool IsInsidePip(Suit suit, float x, float y)
        {
            switch (suit)
            {
                case Suit.Diamonds:
                    return Mathf.Abs(x) / 0.72f + Mathf.Abs(y) / 1.08f <= 1f;

                case Suit.Hearts:
                    return InsideHeart(x, y * 1.02f);

                case Suit.Spades:
                    // A heart turned over, with a tapering stem beneath it.
                    return InsideHeart(x, -y * 1.02f) || InsideStem(x, y, -0.55f, -1.24f, 0.06f, 0.34f);

                default: // Clubs
                    return InsideCircle(x, y, 0f, 0.46f, 0.40f)
                           || InsideCircle(x, y, -0.44f, -0.16f, 0.40f)
                           || InsideCircle(x, y, 0.44f, -0.16f, 0.40f)
                           || InsideStem(x, y, -0.30f, -1.20f, 0.06f, 0.32f);
            }
        }

        /// <summary>
        /// The classic implicit heart, (x^2 + y^2 - 1)^3 - x^2 * y^3 &lt;= 0, with the point at the
        /// bottom. Negating y gives the top half of a spade for free.
        /// </summary>
        private static bool InsideHeart(float x, float y)
        {
            float a = x * x + y * y - 1f;
            return a * a * a - x * x * y * y * y <= 0f;
        }

        private static bool InsideCircle(float x, float y, float cx, float cy, float radius)
        {
            float dx = x - cx;
            float dy = y - cy;
            return dx * dx + dy * dy <= radius * radius;
        }

        /// <summary>A stem that flares as it descends, from topY down to bottomY.</summary>
        private static bool InsideStem(float x, float y, float topY, float bottomY, float topHalf, float bottomHalf)
        {
            if (y > topY || y < bottomY) return false;

            float t = (topY - y) / (topY - bottomY);
            float halfWidth = Mathf.Lerp(topHalf, bottomHalf, t * t);
            return Mathf.Abs(x) <= halfWidth;
        }

        private Texture2D NewTexture(int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            _textures.Add(texture);
            return texture;
        }
    }
}
