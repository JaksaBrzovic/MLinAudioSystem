using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace MLin.Audio.Editor.Tests
{
    public class VolumeControllerTests
    {
        private sealed class FakeStorage : IVolumeStorage
        {
            public readonly Dictionary<string, float> Values = new Dictionary<string, float>();
            public int FlushCount;

            public bool TryLoad(string channel, out float volume) => Values.TryGetValue(channel, out volume);
            public void Store(string channel, float volume) => Values[channel] = volume;
            public void Flush() => FlushCount++;
        }

        private AudioSystemSettings settings;

        [SetUp]
        public void SetUp()
        {
            settings = AudioSystemSettings.CreateDefault();
            settings.VolumeChannels = new[]
            {
                new VolumeChannel("Master", "MasterVolume"),
                new VolumeChannel("Music", "MusicVolume", 0.7f),
            };
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(settings);
        }

        [Test]
        public void UsesDefaults_WhenStorageIsEmpty()
        {
            VolumeController controller = new VolumeController(settings, new FakeStorage());

            Assert.AreEqual(1f, controller.Get("Master"));
            Assert.AreEqual(0.7f, controller.Get("Music"));
        }

        [Test]
        public void LoadsStoredValues()
        {
            FakeStorage storage = new FakeStorage();
            storage.Values["Music"] = 0.2f;

            VolumeController controller = new VolumeController(settings, storage);

            Assert.AreEqual(0.2f, controller.Get("Music"));
        }

        [Test]
        public void Set_ClampsAndStores_AndChannelNamesAreCaseInsensitive()
        {
            FakeStorage storage = new FakeStorage();
            VolumeController controller = new VolumeController(settings, storage);

            controller.Set("music", 2f);

            Assert.AreEqual(1f, controller.Get("Music"));
            Assert.AreEqual(1f, storage.Values["Music"]);
        }

        [Test]
        public void UnknownChannel_IsIgnored()
        {
            FakeStorage storage = new FakeStorage();
            VolumeController controller = new VolumeController(settings, storage);

            controller.Set("Voice", 0.5f);

            Assert.IsFalse(storage.Values.ContainsKey("Voice"));
            Assert.AreEqual(1f, controller.Get("Voice"));
        }

        [Test]
        public void SwappingStorage_ReloadsValues()
        {
            VolumeController controller = new VolumeController(settings, new FakeStorage());
            FakeStorage other = new FakeStorage();
            other.Values["Master"] = 0.3f;

            controller.SetStorage(other);

            Assert.AreEqual(0.3f, controller.Get("Master"));
        }
    }
}
