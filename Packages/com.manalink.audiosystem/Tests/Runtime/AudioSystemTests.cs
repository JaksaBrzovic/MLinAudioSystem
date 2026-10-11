using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MLin.Audio.Tests
{
    public class AudioSystemTests
    {
        private readonly List<Object> createdObjects = new List<Object>();
        private AudioSystemSettings settings;

        [SetUp]
        public void SetUp()
        {
            settings = Track(AudioSystemSettings.CreateDefault());
            settings.SaveVolumes = false;
            settings.DuckAttack = 0f;
            settings.DuckRelease = 0f;
        }

        [TearDown]
        public void TearDown()
        {
            AudioSystem.Shutdown();
            AudioSystem.SetVolumeStorage(null);
            AudioListener.pause = false;

            foreach (Object createdObject in createdObjects)
            {
                if (createdObject != null)
                    Object.Destroy(createdObject);
            }

            createdObjects.Clear();
        }

        #region HELPERS
        private T Track<T>(T createdObject) where T : Object
        {
            createdObjects.Add(createdObject);
            return createdObject;
        }

        private SoundDefinition CreateSound(float seconds = 0.1f, bool loop = false)
        {
            AudioClip clip = Track(AudioClip.Create("test", Mathf.Max(1, (int)(44100 * seconds)), 1, 44100, false));
            SoundDefinition sound = Track(SoundDefinition.Create(clip));
            sound.Loop = loop;
            return sound;
        }

        private void Initialize()
        {
            AudioSystem.Initialize(settings);
        }
        #endregion

        [UnityTest]
        public IEnumerator Play_ReturnsValidHandle_AndFinishesAfterClipLength()
        {
            Initialize();
            SoundHandle handle = AudioSystem.Play(CreateSound(0.1f));

            Assert.IsTrue(handle.IsValid);
            Assert.IsTrue(handle.IsPlaying);

            yield return new WaitForSecondsRealtime(0.5f);

            Assert.IsFalse(handle.IsPlaying);
            Assert.AreEqual(0, AudioSystem.Host.ActiveVoiceCount);
        }

        [UnityTest]
        public IEnumerator Play_CreatesSystemLazily()
        {
            Assert.IsFalse(AudioSystem.IsInitialized);

            SoundHandle handle = AudioSystem.Play(CreateSound());

            Assert.IsTrue(AudioSystem.IsInitialized);
            Assert.IsTrue(handle.IsPlaying);
            yield return null;
        }

        [Test]
        public void Play_WithNullOrEmptyDefinition_ReturnsInvalid()
        {
            Initialize();
            SoundDefinition empty = Track(SoundDefinition.Create());

            LogAssert.Expect(LogType.Warning, "[MLin Audio] Play was called with a null SoundDefinition.");
            Assert.IsFalse(AudioSystem.Play(null).IsValid);
            Assert.IsFalse(AudioSystem.Play(empty).IsValid);
        }

        [UnityTest]
        public IEnumerator Stop_WithFade_KeepsPlayingUntilFadeEnds()
        {
            Initialize();
            SoundHandle handle = AudioSystem.Play(CreateSound(5f));

            handle.Stop(0.3f);
            yield return new WaitForSecondsRealtime(0.1f);
            Assert.IsTrue(handle.IsPlaying);

            yield return new WaitForSecondsRealtime(0.4f);
            Assert.IsFalse(handle.IsPlaying);
        }

        [UnityTest]
        public IEnumerator StaleHandle_DoesNotAffectSoundThatReusedTheVoice()
        {
            settings.InitialVoices = 1;
            settings.MaxVoices = 1;
            Initialize();

            SoundHandle first = AudioSystem.Play(CreateSound(0.05f));
            yield return new WaitForSecondsRealtime(0.4f);
            SoundHandle second = AudioSystem.Play(CreateSound(5f));

            first.Stop();
            first.SetVolume(0f);

            Assert.IsTrue(second.IsPlaying);
            Assert.AreNotEqual(first, second);
        }

        [Test]
        public void Cooldown_RejectsPlayWithinCooldown()
        {
            Initialize();
            SoundDefinition sound = CreateSound();
            sound.Cooldown = 10f;

            Assert.IsTrue(AudioSystem.Play(sound).IsValid);
            Assert.IsFalse(AudioSystem.Play(sound).IsValid);
        }

        [Test]
        public void MaxInstances_RejectNew()
        {
            Initialize();
            SoundDefinition sound = CreateSound(5f);
            sound.MaxInstances = 2;
            sound.InstanceLimitMode = InstanceLimitMode.RejectNew;

            SoundHandle a = AudioSystem.Play(sound);
            SoundHandle b = AudioSystem.Play(sound);
            SoundHandle c = AudioSystem.Play(sound);

            Assert.IsTrue(a.IsPlaying);
            Assert.IsTrue(b.IsPlaying);
            Assert.IsFalse(c.IsValid);
        }

        [UnityTest]
        public IEnumerator MaxInstances_StopOldest()
        {
            Initialize();
            SoundDefinition sound = CreateSound(5f);
            sound.MaxInstances = 1;
            sound.InstanceLimitMode = InstanceLimitMode.StopOldest;

            SoundHandle a = AudioSystem.Play(sound);
            SoundHandle b = AudioSystem.Play(sound);

            yield return new WaitForSecondsRealtime(settings.VoiceStealFade + 0.1f);

            Assert.IsFalse(a.IsPlaying);
            Assert.IsTrue(b.IsPlaying);
        }

        [Test]
        public void FullPool_StealsOldestOfEqualPriority_ButNeverMusic()
        {
            settings.InitialVoices = 1;
            settings.MaxVoices = 3;
            Initialize();

            SoundHandle music = AudioSystem.PlayMusic(CreateSound(5f, loop: true), 0f);
            SoundHandle a = AudioSystem.Play(CreateSound(5f));
            SoundHandle b = AudioSystem.Play(CreateSound(5f));
            SoundHandle c = AudioSystem.Play(CreateSound(5f));

            Assert.AreEqual(3, AudioSystem.Host.VoiceCount);
            Assert.IsTrue(music.IsPlaying);
            Assert.IsFalse(a.IsPlaying);
            Assert.IsTrue(b.IsPlaying);
            Assert.IsTrue(c.IsPlaying);
        }

        [Test]
        public void FullPool_DoesNotStealMoreImportantSound()
        {
            settings.InitialVoices = 1;
            settings.MaxVoices = 1;
            Initialize();

            SoundDefinition important = CreateSound(5f);
            important.Priority = 0;
            SoundDefinition unimportant = CreateSound(5f);
            unimportant.Priority = 200;

            SoundHandle a = AudioSystem.Play(important);
            SoundHandle b = AudioSystem.Play(unimportant);

            Assert.IsTrue(a.IsPlaying);
            Assert.IsFalse(b.IsValid);
        }

        [Test]
        public void Music_SameTrackDoesNotRestart()
        {
            Initialize();
            SoundDefinition track = CreateSound(5f, loop: true);

            SoundHandle first = AudioSystem.PlayMusic(track, 0f);
            SoundHandle second = AudioSystem.PlayMusic(track, 0f);

            Assert.AreEqual(first, second);
            Assert.AreEqual(1, AudioSystem.Host.ActiveVoiceCount);
        }

        [UnityTest]
        public IEnumerator Music_CrossfadesToNewTrack()
        {
            Initialize();
            SoundDefinition trackA = CreateSound(5f, loop: true);
            SoundDefinition trackB = CreateSound(5f, loop: true);

            SoundHandle a = AudioSystem.PlayMusic(trackA, 0f);
            SoundHandle b = AudioSystem.PlayMusic(trackB, 0.2f);

            Assert.AreSame(trackB, AudioSystem.CurrentMusic);
            Assert.IsTrue(a.IsPlaying, "old track should still be fading out");

            yield return new WaitForSecondsRealtime(0.4f);

            Assert.IsFalse(a.IsPlaying);
            Assert.IsTrue(b.IsPlaying);
        }

        [Test]
        public void StopMusic_ClearsCurrentMusic()
        {
            Initialize();
            AudioSystem.PlayMusic(CreateSound(5f, loop: true), 0f);

            AudioSystem.StopMusic(0f);

            Assert.IsNull(AudioSystem.CurrentMusic);
            Assert.AreEqual(0, AudioSystem.Host.ActiveVoiceCount);
        }

        [UnityTest]
        public IEnumerator PauseAndResume_AfterStopMusic_DoesNotRestartMusic()
        {
            Initialize();
            AudioSystem.PlayMusic(CreateSound(5f, loop: true), 0f);
            AudioSystem.StopMusic(0f);

            AudioSystem.Pause(0f);
            yield return null;
            AudioSystem.Resume(0f);
            yield return null;

            Assert.IsNull(AudioSystem.CurrentMusic);
            Assert.AreEqual(0, AudioSystem.Host.ActiveVoiceCount);
        }

        [UnityTest]
        public IEnumerator Pause_FreezesPlayback_UntilResume()
        {
            Initialize();
            SoundHandle handle = AudioSystem.Play(CreateSound(0.2f));

            AudioSystem.Pause(0f);
            Assert.IsTrue(AudioListener.pause);
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.IsTrue(handle.IsPlaying, "paused sound must not finish");

            AudioSystem.Resume(0f);
            Assert.IsFalse(AudioListener.pause);
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.IsFalse(handle.IsPlaying);
        }

        [UnityTest]
        public IEnumerator IgnorePauseSound_KeepsPlayingWhilePaused()
        {
            Initialize();
            SoundDefinition uiSound = CreateSound(0.1f);
            uiSound.IgnorePause = true;

            AudioSystem.Pause(0f);
            SoundHandle handle = AudioSystem.Play(uiSound);
            yield return new WaitForSecondsRealtime(0.5f);

            Assert.IsFalse(handle.IsPlaying);
        }

        [UnityTest]
        public IEnumerator QuickPauseResume_NeverGetsStuckPaused()
        {
            Initialize();
            AudioSystem.PlayMusic(CreateSound(5f, loop: true), 0f);

            AudioSystem.Pause(0.5f);
            yield return null;
            AudioSystem.Resume(0.5f);
            yield return new WaitForSecondsRealtime(0.8f);

            Assert.IsFalse(AudioSystem.IsPaused);
            Assert.IsFalse(AudioListener.pause);
            Assert.AreEqual(1f, AudioSystem.Host.PauseGain);
            Assert.IsNotNull(AudioSystem.CurrentMusic);
        }

        [UnityTest]
        public IEnumerator InterruptedPauseFade_ContinuesFromCurrentLevel()
        {
            Initialize();

            AudioSystem.Pause(1f);
            yield return new WaitForSecondsRealtime(0.3f);
            float gainBeforeResume = AudioSystem.Host.PauseGain;
            AudioSystem.Resume(1f);
            yield return null;
            float gainAfterResume = AudioSystem.Host.PauseGain;

            Assert.That(gainBeforeResume, Is.InRange(0.05f, 0.95f));
            Assert.That(Mathf.Abs(gainAfterResume - gainBeforeResume), Is.LessThan(0.1f), "volume must not jump");
        }

        [UnityTest]
        public IEnumerator PlayAttached_StopsWhenTargetIsDestroyed()
        {
            Initialize();
            GameObject target = new GameObject("target");
            SoundHandle handle = AudioSystem.PlayAttached(CreateSound(5f), target.transform);

            Object.Destroy(target);
            yield return null;
            yield return null;

            Assert.IsFalse(handle.IsPlaying);
        }

        [UnityTest]
        public IEnumerator Ducking_LowersMusicWhileSoundPlays()
        {
            Initialize();
            AudioSystem.PlayMusic(CreateSound(5f, loop: true), 0f);
            SoundDefinition jingle = CreateSound(0.2f);
            jingle.DuckMusic = 0.75f;

            AudioSystem.Play(jingle);
            yield return null;
            Assert.AreEqual(0.25f, AudioSystem.Host.DuckGain, 0.001f);

            yield return new WaitForSecondsRealtime(0.6f);
            Assert.AreEqual(1f, AudioSystem.Host.DuckGain, 0.001f);
        }

        [UnityTest]
        public IEnumerator DuplicateHost_IsDestroyed()
        {
            Initialize();
            AudioSystemHost original = AudioSystem.Host;

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Another AudioSystemHost is already active"));
            GameObject duplicate = new GameObject("duplicate", typeof(AudioSystemHost));
            yield return null;

            Assert.AreSame(original, AudioSystem.Host);
            Assert.IsTrue(duplicate == null);
        }

        [Test]
        public void ChannelVolume_IsStoredThroughCustomStorage()
        {
            Initialize();
            RecordingStorage storage = new RecordingStorage();
            AudioSystem.SetVolumeStorage(storage);

            AudioSystem.SetChannelVolume("Music", 0.25f);

            Assert.AreEqual(0.25f, AudioSystem.GetChannelVolume("Music"));
            Assert.AreEqual(0.25f, storage.Values["Music"]);
        }

        [Test]
        public void Shutdown_ReleasesListenerPause()
        {
            Initialize();
            AudioSystem.Pause(0f);

            AudioSystem.Shutdown();

            Assert.IsFalse(AudioListener.pause);
            Assert.IsFalse(AudioSystem.IsInitialized);
        }

        private sealed class RecordingStorage : IVolumeStorage
        {
            public readonly Dictionary<string, float> Values = new Dictionary<string, float>();

            public bool TryLoad(string channel, out float volume) => Values.TryGetValue(channel, out volume);
            public void Store(string channel, float volume) => Values[channel] = volume;
            public void Flush() { }
        }
    }
}
