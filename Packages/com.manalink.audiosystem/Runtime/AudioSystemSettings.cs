using System;
using UnityEngine;
using UnityEngine.Audio;

namespace MLin.Audio
{
    /// <summary>One user-facing volume slider mapped to an exposed AudioMixer parameter.</summary>
    [Serializable]
    public class VolumeChannel
    {
        [Tooltip("Name used in code, e.g. AudioSystem.SetChannelVolume(\"Music\", 0.5f).")]
        public string name;
        [Tooltip("Exposed parameter on the AudioMixer (right click a group's Volume > Expose).")]
        public string exposedParameter;
        [Range(0f, 1f)] public float defaultVolume = 1f;

        public VolumeChannel() { }

        public VolumeChannel(string name, string exposedParameter, float defaultVolume = 1f)
        {
            this.name = name;
            this.exposedParameter = exposedParameter;
            this.defaultVolume = defaultVolume;
        }
    }

    /// <summary>
    /// Project-wide configuration. Put one named "AudioSystemSettings" in a Resources folder to have the
    /// system created automatically before the first scene loads, or assign one to an AudioSystemHost in a scene.
    /// </summary>
    [CreateAssetMenu(fileName = DefaultResourcesPath, menuName = "MLin/Audio/Audio System Settings", order = 1)]
    public class AudioSystemSettings : ScriptableObject
    {
        public const string DefaultResourcesPath = "AudioSystemSettings";

        [Header("Initialization")]
        [Tooltip("Create the audio system automatically before the first scene loads (only when this asset is at Resources/" + DefaultResourcesPath + ").")]
        [SerializeField] private bool autoInitialize = true;
        [SerializeField] private bool dontDestroyOnLoad = true;

        [Header("Mixer")]
        [SerializeField] private AudioMixer mixer;
        [Tooltip("Default output for music when the SoundDefinition has no output group.")]
        [SerializeField] private AudioMixerGroup musicGroup;
        [Tooltip("Default output for everything else when the SoundDefinition has no output group.")]
        [SerializeField] private AudioMixerGroup sfxGroup;
        [SerializeField] private VolumeChannel[] volumeChannels =
        {
            new VolumeChannel("Master", "MasterVolume"),
            new VolumeChannel("Music", "MusicVolume"),
            new VolumeChannel("SFX", "SFXVolume"),
        };

        [Header("Volume Persistence")]
        [Tooltip("Save channel volumes with the default PlayerPrefs storage. Replace it with AudioSystem.SetVolumeStorage for console save data etc.")]
        [SerializeField] private bool saveVolumes = true;
        [SerializeField] private string playerPrefsKeyPrefix = "MLin.Audio.Volume.";

        [Header("Voices")]
        [SerializeField, Min(1)] private int initialVoices = 16;
        [Tooltip("Hard cap on simultaneous sounds. When reached, the least important / oldest sound is stolen.")]
        [SerializeField, Min(1)] private int maxVoices = 64;

        [Header("Fades (seconds, unscaled time)")]
        [SerializeField, Min(0f)] private float defaultMusicFade = 1f;
        [SerializeField, Min(0f)] private float defaultPauseFade = 0.3f;
        [Tooltip("Fade used when StopOldest makes room for a new instance.")]
        [SerializeField, Min(0f)] private float voiceStealFade = 0.05f;

        [Header("Ducking")]
        [SerializeField, Min(0f)] private float duckAttack = 0.1f;
        [SerializeField, Min(0f)] private float duckRelease = 0.5f;

        [Header("Scenes")]
        [Tooltip("Stop all non-music sounds when the active scene changes. Music is never stopped automatically.")]
        [SerializeField] private bool stopSoundsOnSceneChange;
        [SerializeField, Min(0f)] private float sceneChangeStopFade = 0.1f;

        public bool AutoInitialize { get => autoInitialize; internal set => autoInitialize = value; }
        public bool PersistAcrossScenes { get => dontDestroyOnLoad; internal set => dontDestroyOnLoad = value; }
        public AudioMixer Mixer { get => mixer; internal set => mixer = value; }
        public AudioMixerGroup MusicGroup { get => musicGroup; internal set => musicGroup = value; }
        public AudioMixerGroup SfxGroup { get => sfxGroup; internal set => sfxGroup = value; }
        public VolumeChannel[] VolumeChannels { get => volumeChannels; internal set => volumeChannels = value ?? Array.Empty<VolumeChannel>(); }
        public bool SaveVolumes { get => saveVolumes; internal set => saveVolumes = value; }
        public string PlayerPrefsKeyPrefix { get => playerPrefsKeyPrefix; internal set => playerPrefsKeyPrefix = value; }
        public int InitialVoices { get => initialVoices; internal set => initialVoices = Mathf.Max(1, value); }
        public int MaxVoices { get => maxVoices; internal set => maxVoices = Mathf.Max(1, value); }
        public float DefaultMusicFade { get => defaultMusicFade; internal set => defaultMusicFade = Mathf.Max(0f, value); }
        public float DefaultPauseFade { get => defaultPauseFade; internal set => defaultPauseFade = Mathf.Max(0f, value); }
        public float VoiceStealFade { get => voiceStealFade; internal set => voiceStealFade = Mathf.Max(0f, value); }
        public float DuckAttack { get => duckAttack; internal set => duckAttack = Mathf.Max(0f, value); }
        public float DuckRelease { get => duckRelease; internal set => duckRelease = Mathf.Max(0f, value); }
        public bool StopSoundsOnSceneChange { get => stopSoundsOnSceneChange; internal set => stopSoundsOnSceneChange = value; }
        public float SceneChangeStopFade { get => sceneChangeStopFade; internal set => sceneChangeStopFade = Mathf.Max(0f, value); }

        internal static AudioSystemSettings CreateDefault()
        {
            AudioSystemSettings settings = CreateInstance<AudioSystemSettings>();
            settings.name = "AudioSystemSettings (Default)";
            return settings;
        }

        private void OnValidate()
        {
            if (initialVoices > maxVoices)
                initialVoices = maxVoices;
        }
    }
}
