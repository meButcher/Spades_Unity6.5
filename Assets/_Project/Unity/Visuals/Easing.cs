namespace Spades.Unity.Visuals
{
    public enum Ease
    {
        Linear,
        OutQuad,
        InQuad,
        InOutQuad,
        OutCubic,
        OutBack
    }

    public static class Easing
    {
        private const float BackOvershoot = 1.70158f;

        public static float Evaluate(Ease ease, float t)
        {
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;

            switch (ease)
            {
                case Ease.OutQuad:
                    return 1f - (1f - t) * (1f - t);

                case Ease.InQuad:
                    return t * t;

                case Ease.InOutQuad:
                    return t < 0.5f
                        ? 2f * t * t
                        : 1f - 2f * (1f - t) * (1f - t);

                case Ease.OutCubic:
                {
                    float inv = 1f - t;
                    return 1f - inv * inv * inv;
                }

                case Ease.OutBack:
                {
                    float inv = t - 1f;
                    return 1f + (BackOvershoot + 1f) * inv * inv * inv + BackOvershoot * inv * inv;
                }

                default:
                    return t;
            }
        }
    }
}
