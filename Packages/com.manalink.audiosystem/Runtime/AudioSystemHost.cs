using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

namespace MLin.Audio
{
    /// <summary>
    /// Runtime owner of all audio voices. Normally created automatically by <see cref="AudioSystem"/>;
    /// can also be placed in a scene with a settings asset assigned. Only one instance is ever kept.
    /// Use the static <see cref="AudioSystem"/> API instead of calling this directly.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-1000)]
    [AddComponentMenu("MLin/Audio/Audio System Host")]
    public sealed class AudioSystemHost : MonoBehaviour
    {
        // a synced music switch closer than this to the next beat waits for the following one
        private const float MIN_SYNC_LEAD = 0.05f;
        private const string UNITY_DEFAULT_SNAPSHOT = "Snapshot";

        [SerializeField] private AudioSystemSettings settings;

        private readonly List<AudioVoice> voices = new List<AudioVoice>();
        private readonly Dictionary<SoundDefinition, float> lastPlayTimes = new Dictionary<SoundDefinition, float>();
        private readonly HashSet<string> loggedWarnings = new HashSet<string>();
        // last known position of music definitions with ResumeMusicPosition, updated every frame while they play
        private readonly Dictionary<SoundDefinition, MusicPosition> musicPositions = new Dictionary<SoundDefinition, MusicPosition>();
        private readonly SnapshotStack snapshots = new SnapshotStack();
        private VolumeController volumes;
        private DuckingController ducking;
        private Transform voiceRoot;

        private int nextId = 1;
        private long playCounter;

        private SoundHandle currentMusic;
        private SoundDefinition currentMusicDefinition;

        private bool isPaused;
        private float pauseGain = 1f;
        private float pauseTarget = 1f;
        private float pauseSpeed;
        private bool isListenerPausedByHost;

        private float musicDuckGain = 1f;
        private AudioMixerSnapshot appliedSnapshot;

        private bool isMuteWhenUnfocused;
        private bool isFocused = true;
        private float focusGain = 1f;

        private bool isRegistered;
        private bool isTornDown;

        public AudioSystemSettings Settings => settings;
        public int VoiceCount => voices.Count;

        public int ActiveVoiceCount
        {
            get
            {
                int count = 0;
                foreach (AudioVoice voice in voices)
                {
                    if (voice.IsActive)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        /// <summary>Diagnostics: gain from the per-sound Duck Music setting (bus rules not included).</summary>
        public float MusicDuckGain => musicDuckGain;
        /// <summary>Diagnostics: 0 while muted because the application is unfocused, 1 otherwise.</summary>
        public float FocusGain => focusGain;

        internal bool IsPaused => isPaused;
        internal float PauseGain => pauseGain;
        internal VolumeController Volumes => volumes;
        internal DuckingController Ducking => ducking;
        internal SoundDefinition CurrentMusic => GetVoice(currentMusic) != null ? currentMusicDefinition : null;
        internal SoundHandle CurrentMusicHandle => GetVoice(currentMusic) != null ? currentMusic : SoundHandle.Invalid;

        internal bool MuteWhenUnfocused
        {
            get => isMuteWhenUnfocused;
            // Update fades back in when muting no longer applies
            set => isMuteWhenUnfocused = value;
        }

        private bool ShouldMuteForFocus => !isFocused && isMuteWhenUnfocused && (!Application.isEditor || settings.MuteWhenUnfocusedInEditor);

        #region DIAGNOSTICS
        /// <summary>Gain the bus ducking rules currently apply to sounds routed to <paramref name="group"/> (1 = not ducked).</summary>
        public float GetGroupDuckGain(AudioMixerGroup group)
        {
            return ducking != null ? ducking.GetGain(group) : 1f;
        }

        /// <returns>The clip that is heard (intro or main clip), or null for a stale handle.</returns>
        public AudioClip GetPlayingClip(SoundHandle handle)
        {
            AudioVoice voice = GetVoice(handle);
            return voice != null ? voice.Clip : null;
        }

        /// <returns>Position in seconds within <see cref="GetPlayingClip"/>, or -1 for a stale handle.</returns>
        public float GetPlaybackTime(SoundHandle handle)
        {
            AudioVoice voice = GetVoice(handle);
            return voice != null && voice.Clip != null ? (float)voice.PlaybackSample / voice.Clip.frequency : -1f;
        }
        #endregion

        #region LIFETIME
        /// <summary>Used by AudioSystem.Initialize before the GameObject is activated (i.e. before Awake).</summary>
        internal void SetSettings(AudioSystemSettings newSettings)
        {
            settings = newSettings;
        }

        /// <summary>Releases everything this host owns. Safe to call more than once.</summary>
        internal void Teardown()
        {
            if (isTornDown)
            {
                return;
            }

            isTornDown = true;

            if (!isRegistered)
            {
                return;
            }

            SceneManager.activeSceneChanged -= HandleActiveSceneChanged;

            foreach (AudioVoice voice in voices)
            {
                voice.Release();
            }

            if (isListenerPausedByHost)
            {
                SetListenerPause(false);
            }

            if (focusGain < 1f)
            {
                SetFocusGain(1f);
            }

            RestoreDefaultSnapshot();
            volumes?.Flush();
            AudioSystem.Unregister(this);
        }
        #endregion

        #region PLAYBACK
        /// <param name="resumeClip">Clip to continue instead of starting the definition from the beginning (music resume).</param>
        /// <param name="resumeSample">Sample of <paramref name="resumeClip"/> to continue from.</param>
        /// <param name="startDelay">Seconds until the sound starts (music sync).</param>
        internal SoundHandle Play(SoundDefinition definition, bool isMusic, float fadeIn, Vector3? position, Transform follow,
            AudioClip resumeClip = null, int resumeSample = 0, float startDelay = 0f)
        {
            if (definition == null)
            {
                Debug.LogWarning("[MLin Audio] Play was called with a null SoundDefinition.");
                return SoundHandle.Invalid;
            }

            float now = Time.unscaledTime;
            if (definition.Cooldown > 0f && lastPlayTimes.TryGetValue(definition, out float lastPlayTime) && now - lastPlayTime < definition.Cooldown)
            {
                return SoundHandle.Invalid;
            }

            if (definition.MaxInstances > 0 && !MakeRoomForInstance(definition))
            {
                return SoundHandle.Invalid;
            }

            VoiceStart start = CreateVoiceStart(definition, isMusic, fadeIn, resumeClip, resumeSample, startDelay);
            if (start.MainClip == null)
            {
                WarnOnce($"SoundDefinition '{definition.name}' has no clips assigned.");
                return SoundHandle.Invalid;
            }

            AudioVoice voice = AcquireVoice(definition.Priority);
            if (voice == null)
            {
                WarnOnce($"All {settings.MaxVoices} voices are busy with more important sounds, '{definition.name}' was skipped. Consider raising Max Voices.");
                return SoundHandle.Invalid;
            }

            lastPlayTimes[definition] = now;

            int id = nextId;
            nextId = nextId == int.MaxValue ? 1 : nextId + 1;

            AudioMixerGroup output = definition.OutputGroup != null ? definition.OutputGroup : isMusic ? settings.MusicGroup : settings.SfxGroup;
            voice.Play(definition, start, output, id, ++playCounter, isMusic, position, follow);
            voice.ApplyVolume(pauseGain, GetDuckGain(voice));

            return new SoundHandle(voice.Index, id);
        }

        internal void Stop(SoundHandle handle, float fadeOut)
        {
            GetVoice(handle)?.Stop(fadeOut);
        }

        internal bool IsPlaying(SoundHandle handle)
        {
            return GetVoice(handle) != null;
        }

        /// <returns>Sample position of the clip that is heard, or -1 for a stale handle.</returns>
        internal int GetPlaybackSample(SoundHandle handle)
        {
            AudioVoice voice = GetVoice(handle);
            return voice != null ? voice.PlaybackSample : -1;
        }

        internal void SetVolume(SoundHandle handle, float volume)
        {
            AudioVoice voice = GetVoice(handle);
            if (voice == null)
            {
                return;
            }

            voice.SetUserVolume(volume);
            voice.ApplyVolume(pauseGain, GetDuckGain(voice));
        }

        internal void StopAll(SoundDefinition definition, float fadeOut)
        {
            foreach (AudioVoice voice in voices)
            {
                if (voice.IsActive && voice.Definition == definition)
                {
                    voice.Stop(fadeOut);
                }
            }
        }

        internal void StopAllSounds(float fadeOut, bool includeMusic)
        {
            foreach (AudioVoice voice in voices)
            {
                if (voice.IsActive && (includeMusic || !voice.IsMusic))
                {
                    voice.Stop(fadeOut);
                }
            }

            if (includeMusic)
            {
                currentMusic = SoundHandle.Invalid;
                currentMusicDefinition = null;
            }
        }
        #endregion

        #region MUSIC
        internal SoundHandle PlayMusic(SoundDefinition music, float crossfade, bool restartIfPlaying, MusicSync sync)
        {
            if (music == null)
            {
                Debug.LogWarning("[MLin Audio] PlayMusic was called with a null SoundDefinition.");
                return SoundHandle.Invalid;
            }

            if (crossfade < 0f)
            {
                crossfade = settings.DefaultMusicFade;
            }

            AudioVoice current = GetVoice(currentMusic);
            if (!restartIfPlaying && current != null && !current.IsStopping && currentMusicDefinition == music)
            {
                return currentMusic;
            }

            // going back to a track that is still fading out: fade that same voice back in instead of starting a second copy
            AudioVoice fadingOut = restartIfPlaying ? null : FindFadingOutMusic(music);
            if (fadingOut != null)
            {
                if (current != null && current != fadingOut)
                {
                    current.Stop(crossfade);
                }

                fadingOut.CancelStop(crossfade);
                currentMusic = new SoundHandle(fadingOut.Index, fadingOut.Id);
                currentMusicDefinition = music;
                return currentMusic;
            }

            AudioClip resumeClip = null;
            int resumeSample = 0;
            if (!restartIfPlaying && music.ResumeMusicPosition && musicPositions.TryGetValue(music, out MusicPosition saved) && CanResume(music, saved))
            {
                resumeClip = saved.Clip;
                // a non-looping track that had (almost) finished starts over instead of resuming into its last moments
                bool isNearEnd = saved.Sample >= saved.Clip.samples - saved.Clip.frequency / 2;
                resumeSample = !music.Loop && isNearEnd ? 0 : saved.Sample;
            }

            float delay = GetSyncDelay(current, sync);
            SoundHandle handle = Play(music, true, crossfade, null, null, resumeClip, resumeSample, delay);
            // keep the old track if the new one could not start
            if (!handle.IsValid)
            {
                return handle;
            }

            current?.Stop(crossfade, delay);
            currentMusic = handle;
            currentMusicDefinition = music;
            return handle;
        }

        internal void StopMusic(float fadeOut)
        {
            if (fadeOut < 0f)
            {
                fadeOut = settings.DefaultMusicFade;
            }

            GetVoice(currentMusic)?.Stop(fadeOut);
            currentMusic = SoundHandle.Invalid;
            currentMusicDefinition = null;
        }
        #endregion

        #region PAUSE
        internal void Pause(float fade)
        {
            if (fade < 0f)
            {
                fade = settings.DefaultPauseFade;
            }

            if (isPaused)
            {
                return;
            }

            isPaused = true;
            pauseTarget = 0f;

            if (fade <= 0f || pauseGain <= 0f)
            {
                pauseGain = 0f;
                SetListenerPause(true);
            }
            else
            {
                // fade from wherever we are now, so interrupting a resume never jumps
                pauseSpeed = pauseGain / fade;
            }
        }

        internal void Resume(float fade)
        {
            if (fade < 0f)
            {
                fade = settings.DefaultPauseFade;
            }

            if (!isPaused)
            {
                return;
            }

            isPaused = false;
            pauseTarget = 1f;
            SetListenerPause(false);

            if (fade <= 0f)
            {
                pauseGain = 1f;
            }
            else
            {
                pauseSpeed = (1f - pauseGain) / fade;
            }
        }
        #endregion

        #region SNAPSHOTS
        internal SnapshotHandle RequestSnapshot(AudioMixerSnapshot snapshot, int priority, float transition)
        {
            if (snapshot == null)
            {
                Debug.LogWarning("[MLin Audio] RequestSnapshot was called with a null snapshot.");
                return SnapshotHandle.Invalid;
            }

            if (settings.Mixer != null && snapshot.audioMixer != settings.Mixer)
            {
                WarnOnce($"Snapshot '{snapshot.name}' belongs to mixer '{snapshot.audioMixer.name}', not to '{settings.Mixer.name}' from {settings.name}.");
            }

            int id = snapshots.Push(snapshot, priority);
            ApplyActiveSnapshot(transition);
            return new SnapshotHandle(id);
        }

        internal void ReleaseSnapshot(SnapshotHandle handle, float transition)
        {
            if (handle.IsValid && snapshots.Remove(handle.Id))
            {
                ApplyActiveSnapshot(transition);
            }
        }
        #endregion

        #region VOLUME
        internal void SetVolumeStorage(IVolumeStorage storage)
        {
            volumes.SetStorage(storage ?? CreateDefaultStorage(settings));
            volumes.ApplyAll();
        }
        #endregion

        #region UNITY LIFECYCLE
        private void Awake()
        {
            if (!AudioSystem.TryRegister(this))
            {
                Debug.LogWarning($"[MLin Audio] Another AudioSystemHost is already active, destroying the duplicate on '{name}'.");
                // only take the whole object down if it exists just for us
                if (GetComponents<Component>().Length <= 2)
                {
                    Destroy(gameObject);
                }
                else
                {
                    Destroy(this);
                }

                return;
            }

            isRegistered = true;

            if (settings == null)
            {
                settings = AudioSystem.LoadSettingsOrDefault();
            }

            if (settings.PersistAcrossScenes)
            {
                transform.SetParent(null);
                DontDestroyOnLoad(gameObject);
            }

            voiceRoot = transform;
            volumes = new VolumeController(settings, AudioSystem.ConfiguredStorage ?? CreateDefaultStorage(settings));
            ducking = new DuckingController(settings.DuckingRules);
            isMuteWhenUnfocused = settings.MuteWhenUnfocused;

            int initialVoices = Mathf.Min(settings.InitialVoices, settings.MaxVoices);
            for (int i = 0; i < initialVoices; i++)
            {
                CreateVoice();
            }

            SceneManager.activeSceneChanged += HandleActiveSceneChanged;
        }

        private void Start()
        {
            // AudioMixer.SetFloat is ignored when called during Awake, so channel volumes are applied here
            volumes.ApplyAll();
        }

        private void Update()
        {
            float deltaTime = Time.unscaledDeltaTime;

            UpdateFocus(deltaTime);
            UpdatePause(deltaTime);
            UpdateDucking(deltaTime);

            bool isListenerPaused = AudioListener.pause;
            foreach (AudioVoice voice in voices)
            {
                if (!voice.IsActive)
                {
                    continue;
                }

                if (voice.IsMusic && voice.Definition.ResumeMusicPosition && !voice.IsWaitingToStart)
                {
                    musicPositions[voice.Definition] = new MusicPosition(voice.Clip, voice.PlaybackSample);
                }

                voice.Tick(deltaTime, isListenerPaused, pauseGain, GetDuckGain(voice));
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            isFocused = hasFocus;

            // with Run In Background off, Update stops while unfocused, so mute right here instead of fading
            if (ShouldMuteForFocus)
            {
                SetFocusGain(0f);
            }
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                volumes?.Flush();
            }
        }

        private void OnApplicationQuit()
        {
            volumes?.Flush();
        }

        private void OnDestroy()
        {
            Teardown();
        }
        #endregion

        #region PAUSE, DUCKING AND FOCUS
        private void UpdatePause(float deltaTime)
        {
            if (pauseGain != pauseTarget)
            {
                pauseGain = Mathf.MoveTowards(pauseGain, pauseTarget, pauseSpeed * deltaTime);
            }

            if (isPaused && pauseGain <= 0f && !AudioListener.pause)
            {
                SetListenerPause(true);
            }
        }

        private void SetListenerPause(bool isListenerPaused)
        {
            AudioListener.pause = isListenerPaused;
            isListenerPausedByHost = isListenerPaused;
        }

        private void UpdateDucking(float deltaTime)
        {
            float strongestMusicDuck = 0f;
            bool isListenerPaused = AudioListener.pause;
            ducking.ClearTriggers();

            foreach (AudioVoice voice in voices)
            {
                if (!IsAudible(voice, isListenerPaused))
                {
                    continue;
                }

                strongestMusicDuck = Mathf.Max(strongestMusicDuck, voice.DuckAmount);
                ducking.Trigger(voice.Output);
            }

            float target = 1f - strongestMusicDuck;
            float time = target < musicDuckGain ? settings.DuckAttack : settings.DuckRelease;
            musicDuckGain = time <= 0f ? target : Mathf.MoveTowards(musicDuckGain, target, deltaTime / time);

            ducking.Update(deltaTime);
        }

        private float GetDuckGain(AudioVoice voice)
        {
            float gain = voice.IsMusic ? musicDuckGain : 1f;
            return ducking.HasRules ? gain * ducking.GetGain(voice.Output) : gain;
        }

        private void UpdateFocus(float deltaTime)
        {
            float target = ShouldMuteForFocus ? 0f : 1f;
            if (focusGain == target)
            {
                return;
            }

            float fadeIn = settings.FocusFadeIn;
            bool isImmediate = target < focusGain || fadeIn <= 0f;
            SetFocusGain(isImmediate ? target : Mathf.MoveTowards(focusGain, target, deltaTime / fadeIn));
        }

        private void SetFocusGain(float gain)
        {
            focusGain = gain;
            AudioListener.volume = gain;
        }
        #endregion

        #region SNAPSHOT HELPERS
        private void ApplyActiveSnapshot(float transition)
        {
            if (transition < 0f)
            {
                transition = settings.DefaultSnapshotTransition;
            }

            AudioMixerSnapshot target = snapshots.TryGetActive(out AudioMixerSnapshot requested, out _) ? requested : GetDefaultSnapshot();
            if (target == null)
            {
                WarnOnce($"No default snapshot to return to. Assign Default Snapshot in {settings.name} or keep a snapshot named '{UNITY_DEFAULT_SNAPSHOT}' on the mixer.");
                return;
            }

            if (target == appliedSnapshot)
            {
                return;
            }

            appliedSnapshot = target;
            target.TransitionTo(transition);
        }

        private AudioMixerSnapshot GetDefaultSnapshot()
        {
            if (settings.DefaultSnapshot != null)
            {
                return settings.DefaultSnapshot;
            }

            return settings.Mixer != null ? settings.Mixer.FindSnapshot(UNITY_DEFAULT_SNAPSHOT) : null;
        }

        private void RestoreDefaultSnapshot()
        {
            if (appliedSnapshot == null)
            {
                return;
            }

            AudioMixerSnapshot defaultSnapshot = GetDefaultSnapshot();
            if (defaultSnapshot != null && defaultSnapshot != appliedSnapshot)
            {
                defaultSnapshot.TransitionTo(0f);
            }

            appliedSnapshot = null;
        }
        #endregion

        #region VOICES
        private AudioVoice GetVoice(SoundHandle handle)
        {
            if (!handle.IsValid || handle.VoiceIndex < 0 || handle.VoiceIndex >= voices.Count)
            {
                return null;
            }

            AudioVoice voice = voices[handle.VoiceIndex];
            return voice.IsActive && voice.Id == handle.Id ? voice : null;
        }

        private AudioVoice CreateVoice()
        {
            AudioVoice voice = new AudioVoice(voices.Count, voiceRoot);
            voices.Add(voice);
            return voice;
        }

        private AudioVoice AcquireVoice(int priority)
        {
            foreach (AudioVoice voice in voices)
            {
                if (!voice.IsActive)
                {
                    return voice;
                }
            }

            if (voices.Count < settings.MaxVoices)
            {
                return CreateVoice();
            }

            // steal the least important sound (highest priority number), oldest first; music is never stolen
            AudioVoice candidate = null;
            foreach (AudioVoice voice in voices)
            {
                if (voice.IsMusic || voice.Priority < priority)
                {
                    continue;
                }

                if (candidate == null
                    || voice.Priority > candidate.Priority
                    || (voice.Priority == candidate.Priority && voice.PlayOrder < candidate.PlayOrder))
                {
                    candidate = voice;
                }
            }

            candidate?.Release();
            return candidate;
        }

        private AudioVoice FindFadingOutMusic(SoundDefinition music)
        {
            foreach (AudioVoice voice in voices)
            {
                if (voice.IsActive && voice.IsMusic && voice.IsStopping && voice.Definition == music)
                {
                    return voice;
                }
            }

            return null;
        }

        /// <returns>False if the play request has to be rejected.</returns>
        private bool MakeRoomForInstance(SoundDefinition definition)
        {
            while (true)
            {
                int count = 0;
                AudioVoice oldest = null;
                foreach (AudioVoice voice in voices)
                {
                    // instances already fading out because of a stop do not count
                    if (!voice.IsActive || voice.IsStopping || voice.Definition != definition)
                    {
                        continue;
                    }

                    count++;
                    if (oldest == null || voice.PlayOrder < oldest.PlayOrder)
                    {
                        oldest = voice;
                    }
                }

                if (count < definition.MaxInstances)
                {
                    return true;
                }

                if (definition.InstanceLimitMode == InstanceLimitMode.RejectNew)
                {
                    return false;
                }

                oldest.Stop(settings.VoiceStealFade);
            }
        }
        #endregion

        private static IVolumeStorage CreateDefaultStorage(AudioSystemSettings settings)
        {
            return settings.SaveVolumes ? new PlayerPrefsVolumeStorage(settings.PlayerPrefsKeyPrefix) : new NullVolumeStorage();
        }

        private static bool IsAudible(AudioVoice voice, bool isListenerPaused)
        {
            return voice.IsActive && !voice.IsStopping && !voice.IsWaitingToStart && (!isListenerPaused || voice.IgnorePause);
        }

        private static bool CanResume(SoundDefinition music, MusicPosition saved)
        {
            return saved.Clip != null && (music.HasClip(saved.Clip) || saved.Clip == music.IntroClip);
        }

        private VoiceStart CreateVoiceStart(SoundDefinition definition, bool isMusic, float fadeIn, AudioClip resumeClip, int resumeSample, float startDelay)
        {
            FadeCurve curve = isMusic ? settings.MusicFadeCurve : FadeCurve.Linear;

            if (resumeClip == null)
            {
                return new VoiceStart(definition.IntroClip, definition.PickClip(), 0, fadeIn, curve, startDelay);
            }

            // resuming inside the intro keeps the intro; resuming inside the main clip skips it
            return resumeClip == definition.IntroClip
                ? new VoiceStart(resumeClip, definition.PickClip(), resumeSample, fadeIn, curve, startDelay)
                : new VoiceStart(null, resumeClip, resumeSample, fadeIn, curve, startDelay);
        }

        private float GetSyncDelay(AudioVoice current, MusicSync sync)
        {
            if (current == null || sync == MusicSync.Immediate)
            {
                return 0f;
            }

            // the current track is itself waiting for its beat: take its place on that same beat
            if (current.IsWaitingToStart)
            {
                return current.StartDelay;
            }

            float interval = current.Definition.GetSyncInterval(sync);
            AudioClip clip = current.Clip;
            if (interval <= 0f || clip == null)
            {
                return 0f;
            }

            // the beat grid is in clip time; pitch changes how fast the clip runs in real time
            float pitch = current.Pitch;
            float position = (float)current.PlaybackSample / clip.frequency;
            return AudioMath.TimeToNextBoundary(position, interval, MIN_SYNC_LEAD * pitch) / pitch;
        }

        private void HandleActiveSceneChanged(Scene previous, Scene next)
        {
            if (settings.StopSoundsOnSceneChange)
            {
                StopAllSounds(settings.SceneChangeStopFade, includeMusic: false);
            }
        }

        private void WarnOnce(string message)
        {
            if (loggedWarnings.Add(message))
            {
                Debug.LogWarning("[MLin Audio] " + message);
            }
        }

        private readonly struct MusicPosition
        {
            public MusicPosition(AudioClip clip, int sample)
            {
                Clip = clip;
                Sample = sample;
            }

            public AudioClip Clip { get; }
            public int Sample { get; }
        }
    }
}
