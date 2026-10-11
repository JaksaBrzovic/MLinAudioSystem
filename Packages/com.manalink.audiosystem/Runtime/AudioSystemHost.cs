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
        [SerializeField] private AudioSystemSettings settings;

        private readonly List<AudioVoice> voices = new List<AudioVoice>();
        private readonly Dictionary<SoundDefinition, float> lastPlayTimes = new Dictionary<SoundDefinition, float>();
        private readonly HashSet<string> loggedWarnings = new HashSet<string>();
        private VolumeController volumes;
        private Transform voiceRoot;

        private int nextId = 1;
        private long playCounter;

        private SoundHandle currentMusic;
        private SoundDefinition currentMusicDefinition;

        private bool isPaused;
        private float pauseGain = 1f;
        private float pauseTarget = 1f;
        private float pauseSpeed;
        private bool listenerPausedByUs;

        private float duckGain = 1f;

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
                    if (voice.Active)
                        count++;
                }

                return count;
            }
        }

        internal bool IsPaused => isPaused;
        internal float PauseGain => pauseGain;
        internal float DuckGain => duckGain;
        internal VolumeController Volumes => volumes;
        internal SoundDefinition CurrentMusic => GetVoice(currentMusic) != null ? currentMusicDefinition : null;
        internal SoundHandle CurrentMusicHandle => GetVoice(currentMusic) != null ? currentMusic : SoundHandle.Invalid;

        /// <summary>Used by AudioSystem.Initialize before the GameObject is activated (i.e. before Awake).</summary>
        internal void SetSettings(AudioSystemSettings newSettings)
        {
            settings = newSettings;
        }

        #region UNITY LIFECYCLE
        private void Awake()
        {
            if (!AudioSystem.TryRegister(this))
            {
                Debug.LogWarning($"[MLin Audio] Another AudioSystemHost is already active, destroying the duplicate on '{name}'.");
                // only take the whole object down if it exists just for us
                if (GetComponents<Component>().Length <= 2)
                    Destroy(gameObject);
                else
                    Destroy(this);
                return;
            }

            isRegistered = true;

            if (settings == null)
                settings = AudioSystem.LoadSettingsOrDefault();

            if (settings.PersistAcrossScenes)
            {
                transform.SetParent(null);
                DontDestroyOnLoad(gameObject);
            }

            voiceRoot = transform;
            volumes = new VolumeController(settings, AudioSystem.ConfiguredStorage ?? CreateDefaultStorage(settings));

            int initialVoices = Mathf.Min(settings.InitialVoices, settings.MaxVoices);
            for (int i = 0; i < initialVoices; i++)
                CreateVoice();

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

            UpdatePause(deltaTime);
            UpdateDucking(deltaTime);

            bool listenerPaused = AudioListener.pause;
            foreach (AudioVoice voice in voices)
            {
                if (voice.Active)
                    voice.Tick(deltaTime, listenerPaused, pauseGain, duckGain);
            }
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
                volumes?.Flush();
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

        /// <summary>Releases everything this host owns. Safe to call more than once.</summary>
        internal void Teardown()
        {
            if (isTornDown)
                return;

            isTornDown = true;

            if (!isRegistered)
                return;

            SceneManager.activeSceneChanged -= HandleActiveSceneChanged;

            foreach (AudioVoice voice in voices)
                voice.Release();

            if (listenerPausedByUs)
                SetListenerPause(false);

            volumes?.Flush();
            AudioSystem.Unregister(this);
        }

        #region PLAYBACK
        internal SoundHandle Play(SoundDefinition definition, bool isMusic, float fadeIn, Vector3? position, Transform follow)
        {
            if (definition == null)
            {
                Debug.LogWarning("[MLin Audio] Play was called with a null SoundDefinition.");
                return SoundHandle.Invalid;
            }

            float now = Time.unscaledTime;
            if (definition.Cooldown > 0f && lastPlayTimes.TryGetValue(definition, out float lastPlayTime) && now - lastPlayTime < definition.Cooldown)
                return SoundHandle.Invalid;

            if (definition.MaxInstances > 0 && !MakeRoomForInstance(definition))
                return SoundHandle.Invalid;

            AudioClip clip = definition.PickClip();
            if (clip == null)
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
            voice.Play(definition, clip, output, id, ++playCounter, isMusic, fadeIn, position, follow);
            voice.ApplyVolume(pauseGain, duckGain);

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

        internal void SetVolume(SoundHandle handle, float volume)
        {
            AudioVoice voice = GetVoice(handle);
            if (voice == null)
                return;

            voice.SetUserVolume(volume);
            voice.ApplyVolume(pauseGain, duckGain);
        }

        internal void StopAll(SoundDefinition definition, float fadeOut)
        {
            foreach (AudioVoice voice in voices)
            {
                if (voice.Active && voice.Definition == definition)
                    voice.Stop(fadeOut);
            }
        }

        internal void StopAllSounds(float fadeOut, bool includeMusic)
        {
            foreach (AudioVoice voice in voices)
            {
                if (voice.Active && (includeMusic || !voice.IsMusic))
                    voice.Stop(fadeOut);
            }

            if (includeMusic)
            {
                currentMusic = SoundHandle.Invalid;
                currentMusicDefinition = null;
            }
        }
        #endregion

        #region MUSIC
        internal SoundHandle PlayMusic(SoundDefinition music, float crossfade, bool restartIfPlaying)
        {
            if (music == null)
            {
                Debug.LogWarning("[MLin Audio] PlayMusic was called with a null SoundDefinition.");
                return SoundHandle.Invalid;
            }

            if (crossfade < 0f)
                crossfade = settings.DefaultMusicFade;

            AudioVoice current = GetVoice(currentMusic);
            if (!restartIfPlaying && current != null && !current.IsStopping && currentMusicDefinition == music)
                return currentMusic;

            SoundHandle handle = Play(music, true, crossfade, null, null);
            // keep the old track if the new one could not start
            if (!handle.IsValid)
                return handle;

            current?.Stop(crossfade);
            currentMusic = handle;
            currentMusicDefinition = music;
            return handle;
        }

        internal void StopMusic(float fadeOut)
        {
            if (fadeOut < 0f)
                fadeOut = settings.DefaultMusicFade;

            GetVoice(currentMusic)?.Stop(fadeOut);
            currentMusic = SoundHandle.Invalid;
            currentMusicDefinition = null;
        }
        #endregion

        #region PAUSE
        internal void Pause(float fade)
        {
            if (fade < 0f)
                fade = settings.DefaultPauseFade;

            if (isPaused)
                return;

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
                fade = settings.DefaultPauseFade;

            if (!isPaused)
                return;

            isPaused = false;
            pauseTarget = 1f;
            SetListenerPause(false);

            if (fade <= 0f)
                pauseGain = 1f;
            else
                pauseSpeed = (1f - pauseGain) / fade;
        }

        private void UpdatePause(float deltaTime)
        {
            if (pauseGain != pauseTarget)
                pauseGain = Mathf.MoveTowards(pauseGain, pauseTarget, pauseSpeed * deltaTime);

            if (isPaused && pauseGain <= 0f && !AudioListener.pause)
                SetListenerPause(true);
        }

        private void SetListenerPause(bool paused)
        {
            AudioListener.pause = paused;
            listenerPausedByUs = paused;
        }
        #endregion

        #region DUCKING
        private void UpdateDucking(float deltaTime)
        {
            float strongestDuck = 0f;
            bool listenerPaused = AudioListener.pause;
            foreach (AudioVoice voice in voices)
            {
                if (!voice.Active || voice.IsStopping || voice.DuckAmount <= 0f)
                    continue;

                if (listenerPaused && !voice.IgnorePause)
                    continue;

                strongestDuck = Mathf.Max(strongestDuck, voice.DuckAmount);
            }

            float target = 1f - strongestDuck;
            float time = target < duckGain ? settings.DuckAttack : settings.DuckRelease;
            duckGain = time <= 0f ? target : Mathf.MoveTowards(duckGain, target, deltaTime / time);
        }
        #endregion

        #region VOLUME
        internal void SetVolumeStorage(IVolumeStorage storage)
        {
            volumes.SetStorage(storage ?? CreateDefaultStorage(settings));
            volumes.ApplyAll();
        }

        private static IVolumeStorage CreateDefaultStorage(AudioSystemSettings settings)
        {
            return settings.SaveVolumes ? new PlayerPrefsVolumeStorage(settings.PlayerPrefsKeyPrefix) : new NullVolumeStorage();
        }
        #endregion

        #region VOICES
        private AudioVoice GetVoice(SoundHandle handle)
        {
            if (!handle.IsValid || handle.VoiceIndex < 0 || handle.VoiceIndex >= voices.Count)
                return null;

            AudioVoice voice = voices[handle.VoiceIndex];
            return voice.Active && voice.Id == handle.Id ? voice : null;
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
                if (!voice.Active)
                    return voice;
            }

            if (voices.Count < settings.MaxVoices)
                return CreateVoice();

            // steal the least important sound (highest priority number), oldest first; music is never stolen
            AudioVoice candidate = null;
            foreach (AudioVoice voice in voices)
            {
                if (voice.IsMusic || voice.Priority < priority)
                    continue;

                if (candidate == null
                    || voice.Priority > candidate.Priority
                    || (voice.Priority == candidate.Priority && voice.PlayOrder < candidate.PlayOrder))
                    candidate = voice;
            }

            candidate?.Release();
            return candidate;
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
                    if (!voice.Active || voice.IsStopping || voice.Definition != definition)
                        continue;

                    count++;
                    if (oldest == null || voice.PlayOrder < oldest.PlayOrder)
                        oldest = voice;
                }

                if (count < definition.MaxInstances)
                    return true;

                if (definition.InstanceLimitMode == InstanceLimitMode.RejectNew)
                    return false;

                oldest.Stop(settings.VoiceStealFade);
            }
        }
        #endregion

        private void HandleActiveSceneChanged(Scene previous, Scene next)
        {
            if (settings.StopSoundsOnSceneChange)
                StopAllSounds(settings.SceneChangeStopFade, includeMusic: false);
        }

        private void WarnOnce(string message)
        {
            if (loggedWarnings.Add(message))
                Debug.LogWarning("[MLin Audio] " + message);
        }
    }
}
