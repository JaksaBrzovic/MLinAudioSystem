using NUnit.Framework;
using UnityEngine;

namespace MLin.Audio.Editor.Tests
{
    public class SoundDefinitionTests
    {
        private AudioClip[] clips;

        [SetUp]
        public void SetUp()
        {
            clips = new[]
            {
                AudioClip.Create("a", 441, 1, 44100, false),
                AudioClip.Create("b", 441, 1, 44100, false),
                AudioClip.Create("c", 441, 1, 44100, false),
            };
        }

        [TearDown]
        public void TearDown()
        {
            foreach (AudioClip clip in clips)
            {
                Object.DestroyImmediate(clip);
            }
        }

        [Test]
        public void RandomNoRepeat_NeverPicksSameClipTwiceInARow()
        {
            SoundDefinition definition = SoundDefinition.Create(clips);
            definition.SelectionMode = ClipSelectionMode.RandomNoRepeat;

            AudioClip previous = definition.PickClip();
            for (int i = 0; i < 500; i++)
            {
                AudioClip next = definition.PickClip();
                Assert.AreNotSame(previous, next);
                previous = next;
            }

            Object.DestroyImmediate(definition);
        }

        [Test]
        public void Sequential_CyclesInOrder()
        {
            SoundDefinition definition = SoundDefinition.Create(clips);
            definition.SelectionMode = ClipSelectionMode.Sequential;

            for (int i = 0; i < 7; i++)
            {
                Assert.AreSame(clips[i % clips.Length], definition.PickClip());
            }

            Object.DestroyImmediate(definition);
        }

        [Test]
        public void NullEntries_AreSkipped()
        {
            SoundDefinition definition = SoundDefinition.Create(null, clips[1], null);

            for (int i = 0; i < 20; i++)
            {
                Assert.AreSame(clips[1], definition.PickClip());
            }

            Object.DestroyImmediate(definition);
        }

        [Test]
        public void NoClips_ReturnsNull()
        {
            SoundDefinition definition = SoundDefinition.Create();

            Assert.IsFalse(definition.HasClips);
            Assert.IsNull(definition.PickClip());

            Object.DestroyImmediate(definition);
        }

        [Test]
        public void SyncInterval_UsesTempoAndBarLength()
        {
            SoundDefinition definition = SoundDefinition.Create(clips);
            definition.Bpm = 120f;
            definition.BeatsPerBar = 3;

            Assert.AreEqual(0f, definition.GetSyncInterval(MusicSync.Immediate));
            Assert.AreEqual(0.5f, definition.GetSyncInterval(MusicSync.NextBeat), 0.0001f);
            Assert.AreEqual(1.5f, definition.GetSyncInterval(MusicSync.NextBar), 0.0001f);

            definition.Bpm = 0f;
            Assert.AreEqual(0f, definition.GetSyncInterval(MusicSync.NextBar), "no tempo means no sync");

            Object.DestroyImmediate(definition);
        }

        [Test]
        public void Variation_StaysInRange()
        {
            SoundDefinition definition = SoundDefinition.Create(clips);
            definition.Volume = 0.9f;
            definition.VolumeVariation = 0.3f;
            definition.Pitch = 1f;
            definition.PitchVariation = 0.2f;

            for (int i = 0; i < 200; i++)
            {
                float volume = definition.PickVolume();
                float pitch = definition.PickPitch();
                Assert.That(volume, Is.InRange(0.6f, 1f));
                Assert.That(pitch, Is.InRange(0.8f, 1.2f));
            }

            Object.DestroyImmediate(definition);
        }
    }
}
