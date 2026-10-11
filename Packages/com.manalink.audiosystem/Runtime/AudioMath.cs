using UnityEngine;

namespace MLin.Audio
{
    /// <summary>Conversions between linear volume (0..1, what sliders use) and decibels (what AudioMixer uses).</summary>
    public static class AudioMath
    {
        /// <summary>Lowest value an AudioMixer volume parameter accepts; treated as silence.</summary>
        public const float MinDecibels = -80f;

        public static float LinearToDecibels(float linear)
        {
            if (linear <= 0.0001f)
                return MinDecibels;

            return Mathf.Max(MinDecibels, 20f * Mathf.Log10(linear));
        }

        public static float DecibelsToLinear(float decibels)
        {
            if (decibels <= MinDecibels)
                return 0f;

            return Mathf.Pow(10f, decibels / 20f);
        }
    }
}
