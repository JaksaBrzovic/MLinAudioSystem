using NUnit.Framework;

namespace MLin.Audio.Editor.Tests
{
    public class AudioMathTests
    {
        [Test]
        public void FullVolume_IsZeroDecibels()
        {
            Assert.AreEqual(0f, AudioMath.LinearToDecibels(1f), 0.0001f);
        }

        [Test]
        public void HalfVolume_IsAboutMinusSixDecibels()
        {
            Assert.AreEqual(-6.0206f, AudioMath.LinearToDecibels(0.5f), 0.001f);
        }

        [Test]
        public void Silence_IsClampedToMinDecibels()
        {
            Assert.AreEqual(AudioMath.MIN_DECIBELS, AudioMath.LinearToDecibels(0f));
            Assert.AreEqual(AudioMath.MIN_DECIBELS, AudioMath.LinearToDecibels(-1f));
        }

        [TestCase(1f)]
        [TestCase(0.5f)]
        [TestCase(0.1f)]
        public void Conversion_RoundTrips(float linear)
        {
            Assert.AreEqual(linear, AudioMath.DecibelsToLinear(AudioMath.LinearToDecibels(linear)), 0.0001f);
        }

        [TestCase(FadeCurve.Linear)]
        [TestCase(FadeCurve.EqualPower)]
        public void FadeGain_StartsSilentAndEndsAtFullVolume(FadeCurve curve)
        {
            Assert.AreEqual(0f, AudioMath.FadeGain(0f, curve), 0.0001f);
            Assert.AreEqual(1f, AudioMath.FadeGain(1f, curve), 0.0001f);
        }

        [TestCase(4f, 2f, 0f, 0f, Description = "exactly on a boundary with no lead time: switch now")]
        [TestCase(4f, 2f, 0.05f, 2f, Description = "exactly on a boundary with lead time: the next one")]
        [TestCase(0.5f, 2f, 0.05f, 1.5f)]
        [TestCase(1.98f, 2f, 0.05f, 2.02f, Description = "too close: skip to the following bar")]
        [TestCase(5.5f, 2f, 0.05f, 0.5f, Description = "position wraps over several bars")]
        public void TimeToNextBoundary_FindsNextBeat(float position, float interval, float lead, float expected)
        {
            Assert.AreEqual(expected, AudioMath.TimeToNextBoundary(position, interval, lead), 0.0001f);
        }

        [Test]
        public void TimeToNextBoundary_WithoutInterval_IsImmediate()
        {
            Assert.AreEqual(0f, AudioMath.TimeToNextBoundary(1.3f, 0f, 0.05f));
        }

        [TestCase(0.1f)]
        [TestCase(0.5f)]
        [TestCase(0.8f)]
        public void EqualPowerCrossfade_KeepsSummedPowerConstant(float progress)
        {
            float fadingIn = AudioMath.FadeGain(progress, FadeCurve.EqualPower);
            float fadingOut = AudioMath.FadeGain(1f - progress, FadeCurve.EqualPower);

            Assert.AreEqual(1f, fadingIn * fadingIn + fadingOut * fadingOut, 0.0001f);
        }
    }
}
