using UnityEngine;

namespace MLin.Audio
{
    /// <summary>How fade progress (0..1) is turned into gain.</summary>
    public enum FadeCurve
    {
        /// <summary>Gain follows progress directly. Two tracks crossfading this way dip about 3 dB in the middle.</summary>
        Linear,
        /// <summary>Sine curve: during a crossfade the summed power of both tracks stays constant, so loudness does not dip.</summary>
        EqualPower
    }

    /// <summary>Conversions between linear volume (0..1, what sliders use) and decibels (what AudioMixer uses), and fade curves.</summary>
    public static class AudioMath
    {
        /// <summary>Lowest value an AudioMixer volume parameter accepts; treated as silence.</summary>
        public const float MIN_DECIBELS = -80f;

        public static float LinearToDecibels(float linear)
        {
            if (linear <= 0.0001f)
            {
                return MIN_DECIBELS;
            }

            return Mathf.Max(MIN_DECIBELS, 20f * Mathf.Log10(linear));
        }

        public static float DecibelsToLinear(float decibels)
        {
            if (decibels <= MIN_DECIBELS)
            {
                return 0f;
            }

            return Mathf.Pow(10f, decibels / 20f);
        }

        /// <summary>Gain at a fade progress of 0 (silent) to 1 (full volume).</summary>
        public static float FadeGain(float progress, FadeCurve curve)
        {
            progress = Mathf.Clamp01(progress);
            return curve == FadeCurve.EqualPower ? Mathf.Sin(progress * Mathf.PI * 0.5f) : progress;
        }

        /// <summary>
        /// Seconds from <paramref name="position"/> to the next multiple of <paramref name="interval"/> (the next beat or bar).
        /// A boundary closer than <paramref name="minimumLead"/> is skipped, because it can not be scheduled reliably anymore.
        /// </summary>
        public static float TimeToNextBoundary(float position, float interval, float minimumLead)
        {
            if (interval <= 0f)
            {
                return 0f;
            }

            float remainder = Mathf.Repeat(Mathf.Max(0f, position), interval);
            float wait = remainder <= 0f ? 0f : interval - remainder;
            if (wait < minimumLead)
            {
                wait += interval;
            }

            return wait;
        }
    }
}
