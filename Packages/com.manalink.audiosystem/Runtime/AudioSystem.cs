using UnityEngine;
using UnityEngine.Audio;

namespace MLin.Audio
{
    /// <summary>
    /// Static entry point for all audio. Safe to call from anywhere: the system creates itself on first use
    /// (or before the first scene, if a settings asset exists at Resources/AudioSystemSettings).
    /// Fade arguments are in seconds of unscaled time; a negative value means "use the default from settings".
    /// </summary>
    public static class AudioSystem
    {
        private static AudioSystemHost host;
        private static bool isQuitting;
        private static IVolumeStorage configuredStorage;

        public static bool IsInitialized => host != null;
        public static AudioSystemHost Host => host;
        public static AudioSystemSettings Settings => host != null ? host.Settings : null;
        public static SoundDefinition CurrentMusic => host != null ? host.CurrentMusic : null;
        public static SoundHandle CurrentMusicHandle => host != null ? host.CurrentMusicHandle : SoundHandle.Invalid;
        public static bool IsPaused => host != null && host.IsPaused;

        /// <summary>
        /// Silence all audio while the application is not focused. Starts with the value from settings;
        /// set it from an options menu. Uses AudioListener.volume.
        /// </summary>
        public static bool MuteWhenUnfocused
        {
            get
            {
                AudioSystemHost activeHost = GetOrCreateHost();
                return activeHost != null && activeHost.MuteWhenUnfocused;
            }
            set
            {
                AudioSystemHost activeHost = GetOrCreateHost();
                if (activeHost != null)
                {
                    activeHost.MuteWhenUnfocused = value;
                }
            }
        }

        internal static IVolumeStorage ConfiguredStorage => configuredStorage;

        #region LIFETIME
        /// <summary>
        /// Creates the audio system explicitly. Without settings, Resources/AudioSystemSettings is used,
        /// or built-in defaults if that does not exist. Does nothing if already initialized.
        /// </summary>
        public static void Initialize(AudioSystemSettings settings = null)
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[MLin Audio] AudioSystem can only be initialized in play mode.");
                return;
            }

            if (host != null)
            {
                if (settings != null && settings != host.Settings)
                {
                    Debug.LogWarning($"[MLin Audio] Already initialized with '{host.Settings.name}', ignoring '{settings.name}'.");
                }

                return;
            }

            GameObject gameObject = new GameObject("[MLin Audio System]");
            gameObject.SetActive(false);
            AudioSystemHost newHost = gameObject.AddComponent<AudioSystemHost>();
            newHost.SetSettings(settings != null ? settings : LoadSettingsOrDefault());
            gameObject.SetActive(true);
        }

        /// <summary>Stops everything and destroys the system. The next call to any API creates it again.</summary>
        public static void Shutdown()
        {
            if (host == null)
            {
                return;
            }

            AudioSystemHost oldHost = host;
            oldHost.Teardown();
            Object.Destroy(oldHost.gameObject);
            host = null;
        }
        #endregion

        #region SOUNDS
        /// <summary>Plays a 2D sound.</summary>
        public static SoundHandle Play(SoundDefinition sound, float fadeIn = 0f)
        {
            AudioSystemHost activeHost = GetOrCreateHost();
            return activeHost != null ? activeHost.Play(sound, false, fadeIn, null, null) : SoundHandle.Invalid;
        }

        /// <summary>Plays a sound at a fixed world position (uses the definition's 3D settings).</summary>
        public static SoundHandle PlayAt(SoundDefinition sound, Vector3 position, float fadeIn = 0f)
        {
            AudioSystemHost activeHost = GetOrCreateHost();
            return activeHost != null ? activeHost.Play(sound, false, fadeIn, position, null) : SoundHandle.Invalid;
        }

        /// <summary>Plays a sound that follows a transform. Stops automatically if the transform is destroyed.</summary>
        public static SoundHandle PlayAttached(SoundDefinition sound, Transform target, float fadeIn = 0f)
        {
            if (target == null)
            {
                Debug.LogWarning("[MLin Audio] PlayAttached was called with a null target.");
                return SoundHandle.Invalid;
            }

            AudioSystemHost activeHost = GetOrCreateHost();
            return activeHost != null ? activeHost.Play(sound, false, fadeIn, null, target) : SoundHandle.Invalid;
        }

        public static void Stop(SoundHandle handle, float fadeOut = 0f)
        {
            if (host != null)
            {
                host.Stop(handle, fadeOut);
            }
        }

        /// <summary>Stops every playing instance of a definition.</summary>
        public static void StopAll(SoundDefinition sound, float fadeOut = 0f)
        {
            if (host != null && sound != null)
            {
                host.StopAll(sound, fadeOut);
            }
        }

        public static void StopAllSounds(float fadeOut = 0f, bool includeMusic = false)
        {
            if (host != null)
            {
                host.StopAllSounds(fadeOut, includeMusic);
            }
        }

        public static bool IsPlaying(SoundHandle handle)
        {
            return host != null && host.IsPlaying(handle);
        }

        /// <summary>Per-sound volume multiplier (0..1) on top of the definition volume.</summary>
        public static void SetVolume(SoundHandle handle, float volume)
        {
            if (host != null)
            {
                host.SetVolume(handle, volume);
            }
        }
        #endregion

        #region MUSIC
        /// <summary>
        /// Crossfades to a new music track. Requesting the track that is already playing does nothing
        /// unless <paramref name="restartIfPlaying"/> is true.
        /// </summary>
        /// <param name="sync">
        /// Wait for the next beat or bar of the current track (its Bpm) before the crossfade starts; the new track
        /// starts sample-accurately on that beat. Immediate when the current track has no tempo.
        /// </param>
        public static SoundHandle PlayMusic(SoundDefinition music, float crossfade = -1f, bool restartIfPlaying = false, MusicSync sync = MusicSync.Immediate)
        {
            AudioSystemHost activeHost = GetOrCreateHost();
            return activeHost != null ? activeHost.PlayMusic(music, crossfade, restartIfPlaying, sync) : SoundHandle.Invalid;
        }

        public static void StopMusic(float fadeOut = -1f)
        {
            if (host != null)
            {
                host.StopMusic(fadeOut);
            }
        }
        #endregion

        #region PAUSE
        /// <summary>
        /// Fades out and pauses every sound except definitions marked Ignore Pause. Can be interrupted by Resume at any time.
        /// Uses AudioListener.pause internally.
        /// </summary>
        public static void Pause(float fade = -1f)
        {
            AudioSystemHost activeHost = GetOrCreateHost();
            if (activeHost != null)
            {
                activeHost.Pause(fade);
            }
        }

        public static void Resume(float fade = -1f)
        {
            if (host != null)
            {
                host.Resume(fade);
            }
        }
        #endregion

        #region SNAPSHOTS
        /// <summary>
        /// Asks for a mixer snapshot (pause menu, underwater, low health...). The highest priority open request is
        /// active; on equal priority the most recent wins. When every request is released the default snapshot returns.
        /// </summary>
        /// <param name="transition">Seconds; negative = the default from settings.</param>
        public static SnapshotHandle RequestSnapshot(AudioMixerSnapshot snapshot, int priority = 0, float transition = -1f)
        {
            AudioSystemHost activeHost = GetOrCreateHost();
            return activeHost != null ? activeHost.RequestSnapshot(snapshot, priority, transition) : SnapshotHandle.Invalid;
        }

        public static void ReleaseSnapshot(SnapshotHandle handle, float transition = -1f)
        {
            if (host != null)
            {
                host.ReleaseSnapshot(handle, transition);
            }
        }
        #endregion

        #region VOLUME CHANNELS
        public static float GetChannelVolume(string channel)
        {
            AudioSystemHost activeHost = GetOrCreateHost();
            return activeHost != null ? activeHost.Volumes.Get(channel) : 1f;
        }

        /// <summary>Sets a channel volume (0..1, linear). Applied to the mixer in dB and stored via the volume storage.</summary>
        public static void SetChannelVolume(string channel, float volume)
        {
            AudioSystemHost activeHost = GetOrCreateHost();
            if (activeHost != null)
            {
                activeHost.Volumes.Set(channel, volume);
            }
        }

        /// <summary>
        /// Replaces where volumes are saved (null = default from settings). Can be called before or after initialization;
        /// volumes are reloaded from the new storage.
        /// </summary>
        public static void SetVolumeStorage(IVolumeStorage storage)
        {
            configuredStorage = storage;
            if (host != null)
            {
                host.SetVolumeStorage(storage);
            }
        }

        /// <summary>Forces the volume storage to write to disk (also happens automatically on pause/quit).</summary>
        public static void SaveVolumes()
        {
            if (host != null)
            {
                host.Volumes.Flush();
            }
        }
        #endregion

        internal static bool TryRegister(AudioSystemHost newHost)
        {
            if (host != null && host != newHost)
            {
                return false;
            }

            host = newHost;
            return true;
        }

        internal static void Unregister(AudioSystemHost oldHost)
        {
            if (host == oldHost)
            {
                host = null;
            }
        }

        internal static AudioSystemSettings LoadSettingsOrDefault()
        {
            AudioSystemSettings settings = Resources.Load<AudioSystemSettings>(AudioSystemSettings.DEFAULT_RESOURCES_PATH);
            return settings != null ? settings : AudioSystemSettings.CreateDefault();
        }

        #region UNITY LIFECYCLE
        // Runs before anything else on every play mode start, so static state is clean even with Domain Reload disabled.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            host = null;
            isQuitting = false;
            configuredStorage = null;
            // AudioListener.pause survives between play sessions in the editor
            AudioListener.pause = false;
            Application.quitting -= HandleQuitting;
            Application.quitting += HandleQuitting;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoInitialize()
        {
            AudioSystemSettings settings = Resources.Load<AudioSystemSettings>(AudioSystemSettings.DEFAULT_RESOURCES_PATH);
            if (settings != null && settings.AutoInitialize)
            {
                Initialize(settings);
            }
        }
        #endregion

        private static AudioSystemHost GetOrCreateHost()
        {
            if (host == null && !isQuitting && Application.isPlaying)
            {
                Initialize();
            }

            return host;
        }

        private static void HandleQuitting()
        {
            isQuitting = true;
        }
    }
}
