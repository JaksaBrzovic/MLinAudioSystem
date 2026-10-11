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
            Assert.AreEqual(AudioMath.MinDecibels, AudioMath.LinearToDecibels(0f));
            Assert.AreEqual(AudioMath.MinDecibels, AudioMath.LinearToDecibels(-1f));
        }

        [TestCase(1f)]
        [TestCase(0.5f)]
        [TestCase(0.1f)]
        public void Conversion_RoundTrips(float linear)
        {
            Assert.AreEqual(linear, AudioMath.DecibelsToLinear(AudioMath.LinearToDecibels(linear)), 0.0001f);
        }
    }
}
