using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
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
            AudioListener.volume = 1f;

            foreach (Object createdObject in createdObjects)
            {
                if (createdObject != null)
                {
                    Object.Destroy(createdObject);
                }
            }

            createdObjects.Clear();
        }

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

        [UnityTest]
        public IEnumerator Music_ReturningDuringFadeOut_FadesSameVoiceBackIn()
        {
            Initialize();
            SoundDefinition worldMap = CreateSound(5f, loop: true);
            SoundDefinition town = CreateSound(5f, loop: true);

            SoundHandle first = AudioSystem.PlayMusic(worldMap, 0f);
            SoundHandle townHandle = AudioSystem.PlayMusic(town, 1f);
            yield return new WaitForSecondsRealtime(0.2f);
            SoundHandle back = AudioSystem.PlayMusic(worldMap, 0.3f);

            Assert.AreEqual(first, back, "the fading out voice is reused");
            Assert.AreEqual(2, AudioSystem.Host.ActiveVoiceCount);

            yield return new WaitForSecondsRealtime(0.6f);

            Assert.IsTrue(back.IsPlaying);
            Assert.IsFalse(townHandle.IsPlaying);
            Assert.AreSame(worldMap, AudioSystem.CurrentMusic);
        }

        [UnityTest]
        public IEnumerator Music_WithResumePosition_ContinuesWhereItStopped()
        {
            Initialize();
            SoundDefinition worldMap = CreateSound(5f, loop: true);
            worldMap.ResumeMusicPosition = true;
            SoundDefinition town = CreateSound(5f, loop: true);

            SoundHandle first = AudioSystem.PlayMusic(worldMap, 0f);
            yield return new WaitForSecondsRealtime(0.5f);
            int sampleBeforeSwitch = AudioSystem.Host.GetPlaybackSample(first);
            AudioSystem.PlayMusic(town, 0f);
            yield return null;

            SoundHandle resumed = AudioSystem.PlayMusic(worldMap, 0f);

            Assert.That(sampleBeforeSwitch, Is.GreaterThan(0), "playback advanced before the switch");
            // the position is recorded once per frame, so allow up to one frame (~0.1 s) of difference
            Assert.That(AudioSystem.Host.GetPlaybackSample(resumed), Is.InRange(sampleBeforeSwitch - 4410, sampleBeforeSwitch + 4410));
        }

        [UnityTest]
        public IEnumerator Music_WithoutResumePosition_StartsFromBeginning()
        {
            Initialize();
            SoundDefinition worldMap = CreateSound(5f, loop: true);
            SoundDefinition town = CreateSound(5f, loop: true);

            AudioSystem.PlayMusic(worldMap, 0f);
            yield return new WaitForSecondsRealtime(0.5f);
            AudioSystem.PlayMusic(town, 0f);
            yield return null;

            SoundHandle restarted = AudioSystem.PlayMusic(worldMap, 0f);

            Assert.That(AudioSystem.Host.GetPlaybackSample(restarted), Is.LessThan(44100 * 0.1f));
        }

        [UnityTest]
        public IEnumerator IntroClip_PlaysOnceThenMainClipLoops()
        {
            Initialize();
            AudioClip intro = Track(AudioClip.Create("intro", (int)(44100 * 0.3f), 1, 44100, false));
            SoundDefinition music = CreateSound(5f, loop: true);
            music.IntroClip = intro;

            SoundHandle handle = AudioSystem.PlayMusic(music, 0f);
            yield return null;
            Assert.AreSame(intro, AudioSystem.Host.GetPlayingClip(handle));

            yield return new WaitForSecondsRealtime(0.8f);
            Assert.AreSame(music.Clips[0], AudioSystem.Host.GetPlayingClip(handle));
            Assert.IsTrue(handle.IsPlaying);
        }

        [UnityTest]
        public IEnumerator IntroClip_NonLoopingSoundEndsAfterIntroAndMainClip()
        {
            Initialize();
            SoundDefinition sound = CreateSound(0.3f);
            sound.IntroClip = Track(AudioClip.Create("intro", (int)(44100 * 0.3f), 1, 44100, false));

            SoundHandle handle = AudioSystem.Play(sound);
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.IsTrue(handle.IsPlaying, "intro is done, main clip still playing");

            yield return new WaitForSecondsRealtime(0.8f);
            Assert.IsFalse(handle.IsPlaying);
        }

        [UnityTest]
        public IEnumerator PauseDuringIntro_MainClipStillFollows()
        {
            Initialize();
            SoundDefinition music = CreateSound(5f, loop: true);
            music.IntroClip = Track(AudioClip.Create("intro", (int)(44100 * 0.4f), 1, 44100, false));

            SoundHandle handle = AudioSystem.PlayMusic(music, 0f);
            yield return new WaitForSecondsRealtime(0.1f);
            AudioSystem.Pause(0f);
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.AreSame(music.IntroClip, AudioSystem.Host.GetPlayingClip(handle), "the intro does not advance while paused");

            AudioSystem.Resume(0f);
            yield return new WaitForSecondsRealtime(0.8f);
            Assert.AreSame(music.Clips[0], AudioSystem.Host.GetPlayingClip(handle));
            Assert.IsTrue(handle.IsPlaying);
        }

        [UnityTest]
        public IEnumerator SyncedMusicSwitch_WaitsForNextBeat()
        {
            Initialize();
            SoundDefinition first = CreateSound(10f, loop: true);
            first.Bpm = 60f;
            SoundDefinition second = CreateSound(10f, loop: true);

            SoundHandle firstHandle = AudioSystem.PlayMusic(first, 0f);
            yield return new WaitForSecondsRealtime(0.2f);
            SoundHandle secondHandle = AudioSystem.PlayMusic(second, 0f, sync: MusicSync.NextBeat);

            yield return new WaitForSecondsRealtime(0.2f);
            Assert.IsTrue(firstHandle.IsPlaying, "the old track plays until the beat");
            Assert.AreEqual(0, AudioSystem.Host.GetPlaybackSample(secondHandle), "the new track has not started yet");
            Assert.AreSame(second, AudioSystem.CurrentMusic);

            yield return new WaitForSecondsRealtime(1f);
            Assert.IsFalse(firstHandle.IsPlaying);
            Assert.Greater(AudioSystem.Host.GetPlaybackSample(secondHandle), 0);
        }

        [UnityTest]
        public IEnumerator FocusLoss_MutesAndFocusReturnFadesBackIn()
        {
            settings.MuteWhenUnfocusedInEditor = true;
            settings.FocusFadeIn = 0.2f;
            Initialize();

            AudioSystem.Host.SendMessage("OnApplicationFocus", false);
            Assert.AreEqual(0f, AudioListener.volume);
            yield return null;
            Assert.AreEqual(0f, AudioListener.volume, "stays muted while unfocused");

            AudioSystem.Host.SendMessage("OnApplicationFocus", true);
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.AreEqual(1f, AudioListener.volume);
        }

        [Test]
        public void FocusLoss_WithMuteWhenUnfocusedOff_KeepsVolume()
        {
            settings.MuteWhenUnfocusedInEditor = true;
            Initialize();
            AudioSystem.MuteWhenUnfocused = false;

            AudioSystem.Host.SendMessage("OnApplicationFocus", false);

            Assert.AreEqual(1f, AudioListener.volume);
        }

        [UnityTest]
        public IEnumerator PlayLayers_StartSampleAligned()
        {
            Initialize();
            SoundDefinition[] layers = { CreateSound(5f, loop: true), CreateSound(5f, loop: true), CreateSound(5f, loop: true) };

            SoundHandle[] handles = AudioSystem.PlayLayers(layers);
            yield return new WaitForSecondsRealtime(0.5f);

            Assert.AreEqual(3, handles.Length);
            int first = AudioSystem.Host.GetPlaybackSample(handles[0]);
            Assert.Greater(first, 0, "layers are playing");
            foreach (SoundHandle handle in handles)
            {
                Assert.IsTrue(handle.IsPlaying);
                Assert.AreEqual(first, AudioSystem.Host.GetPlaybackSample(handle), 64, "layers stay on the same sample");
            }
        }

        [UnityTest]
        public IEnumerator PlayLayers_AreMusicAndNeverStolen()
        {
            settings.InitialVoices = 2;
            settings.MaxVoices = 2;
            Initialize();
            SoundHandle[] layers = AudioSystem.PlayLayers(new[] { CreateSound(5f, loop: true), CreateSound(5f, loop: true) });

            SoundHandle effect = AudioSystem.Play(CreateSound(5f));
            yield return null;

            Assert.IsFalse(effect.IsValid, "a full pool of layers rejects ordinary sounds");
            Assert.IsTrue(layers[0].IsPlaying && layers[1].IsPlaying);
        }

        [UnityTest]
        public IEnumerator SetVolume_WithFade_MovesGraduallyToTarget()
        {
            Initialize();
            SoundHandle handle = AudioSystem.Play(CreateSound(5f, loop: true));
            yield return null;
            Assert.AreEqual(1f, AudioSystem.Host.GetAppliedVolume(handle), 0.001f);

            handle.SetVolume(0f, 0.4f);
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.That(AudioSystem.Host.GetAppliedVolume(handle), Is.InRange(0.15f, 0.85f), "halfway through the fade");

            yield return new WaitForSecondsRealtime(0.4f);
            Assert.AreEqual(0f, AudioSystem.Host.GetAppliedVolume(handle), 0.001f);
            Assert.IsTrue(handle.IsPlaying, "a volume fade does not stop the sound");
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
            Assert.AreEqual(0.25f, AudioSystem.Host.MusicDuckGain, 0.001f);

            yield return new WaitForSecondsRealtime(0.6f);
            Assert.AreEqual(1f, AudioSystem.Host.MusicDuckGain, 0.001f);
        }

        [UnityTest]
        public IEnumerator DuplicateHost_IsDestroyed()
        {
            Initialize();
            AudioSystemHost original = AudioSystem.Host;

            LogAssert.Expect(LogType.Warning, new Regex("Another AudioSystemHost is already active"));
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

        private sealed class RecordingStorage : IVolumeStorage
        {
            public Dictionary<string, float> Values { get; } = new Dictionary<string, float>();

            public bool TryLoad(string channel, out float volume)
            {
                return Values.TryGetValue(channel, out volume);
            }

            public void Store(string channel, float volume)
            {
                Values[channel] = volume;
            }

            public void Flush()
            {
            }
        }
    }
}
