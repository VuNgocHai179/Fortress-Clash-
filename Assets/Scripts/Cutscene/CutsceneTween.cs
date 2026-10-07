using UnityEngine;

namespace FortressClash.Cutscene
{
    /// <summary>
    /// Easing curves used by the book.  All of them map 0..1 to 0..1 (plus a deliberate
    /// overshoot in the spring ones).
    /// </summary>
    public static class CutsceneTween
    {
        public static float SmoothStep(float t) => t * t * (3f - 2f * t);

        public static float EaseOutCubic(float t)
        {
            float u = 1f - Mathf.Clamp01(t);
            return 1f - u * u * u;
        }

        public static float EaseOutQuint(float t)
        {
            float u = 1f - Mathf.Clamp01(t);
            return 1f - u * u * u * u * u;
        }

        public static float EaseInOutCubic(float t)
        {
            t = Mathf.Clamp01(t);
            return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
        }

        public static float EaseInCubic(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * t;
        }

        /// <summary>Shoots past 1 and comes back - paper snapping into place.</summary>
        public static float EaseOutBack(float t, float overshoot = 1.7f)
        {
            float u = Mathf.Clamp01(t) - 1f;
            return 1f + (overshoot + 1f) * u * u * u + overshoot * u * u;
        }

        /// <summary>
        /// Step response of a damped spring: 0 at t = 0, overshoots, settles on 1 at t = 1.
        /// Used for the little wobble after a card lands.
        /// </summary>
        public static float SpringOut(float t, float damping = 0.55f, float omega = 11f)
        {
            t = Mathf.Clamp01(t);
            float wd = omega * Mathf.Sqrt(1f - damping * damping);
            float y = 1f - Mathf.Exp(-damping * omega * t) * (Mathf.Cos(wd * t) + damping * omega / wd * Mathf.Sin(wd * t));
            return t >= 1f ? 1f : y;
        }

        /// <summary>0 at both ends, 1 partway through.</summary>
        public static float Bump(float t)
        {
            t = Mathf.Clamp01(t);
            return Mathf.Sin(t * Mathf.PI);
        }

        public static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }
}
