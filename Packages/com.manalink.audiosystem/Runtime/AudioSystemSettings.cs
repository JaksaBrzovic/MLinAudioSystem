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
        [SerializeField] private string name;
        [Tooltip("Exposed parameter on the AudioMixer (right click a group's Volume > Expose).")]
        [SerializeField] private string exposedParameter;
        [Range(0f, 1f)]
        [SerializeField] private float defaultVolume = 1f;

        public VolumeChannel()
        {
        }

        public VolumeChannel(string name, string exposedParameter, float defaultVolume = 1f)
        {
            this.name = name;
            this.exposedParameter = exposedParameter;
            this.defaultVolume = defaultVolume;
        }

        public string Name => name;
        public string ExposedParameter => exposedParameter;
        public float DefaultVolume => defaultVolume;
    }

    /// <summary>
    /// Bus ducking: while any sound routed to <see cref="Trigger"/> plays, sounds routed to <see cref="Target"/> are lowered.
    /// Groups are matched exactly; a sound in a child group of the target is not ducked, so add a rule per group.
    /// </summary>
    [Serializable]
    public class DuckingRule
    {
        [Tooltip("While any sound routed to this group plays...")]
        [SerializeField] private AudioMixerGroup trigger;
        [Tooltip("...sounds routed to this group are lowered.")]
        [SerializeField] private AudioMixerGroup target;
        [Tooltip("How much the target is lowered, in dB.")]
        [Range(-40f, 0f)]
        [SerializeField] private float duckDecibels = -8f;
        [Tooltip("Seconds (unscaled) to reach the full duck.")]
        [Min(0f)]
        [SerializeField] private float attack = 0.1f;
        [Tooltip("Seconds (unscaled) to recover after the last trigger sound stops.")]
        [Min(0f)]
        [SerializeField] private float release = 0.5f;

        public DuckingRule()
        {
        }

        public DuckingRule(AudioMixerGroup trigger, AudioMixerGroup target, float duckDecibels, float attack, float release)
        {
            this.trigger = trigger;
            this.target = target;
            this.duckDecibels = Mathf.Min(0f, duckDecibels);
            this.attack = Mathf.Max(0f, attack);
            this.release = Mathf.Max(0f, release);
        }

        public AudioMixerGroup Trigger => trigger;
        public AudioMixerGroup Target => target;
        public float DuckDecibels => duckDecibels;
        public float Attack => attack;
        public float Release => release;
        /// <summary>False when a group is missing or the rule would duck its own trigger.</summary>
        public bool IsValid => trigger != null && target != null && trigger != target;
    }

    /// <summary>
    /// Project-wide configuration. Put one named "AudioSystemSettings" in a Resources folder to have the
    /// system created automatically before the first scene loads, or assign one to an AudioSystemHost in a scene.
    /// </summary>
    [CreateAssetMenu(fileName = DEFAULT_RESOURCES_PATH, menuName = "MLin/Audio/Audio System Settings", order = 1)]
    public class AudioSystemSettings : ScriptableObject
    {
        public const string DEFAULT_RESOURCES_PATH = "AudioSystemSettings";

        [Header("Initialization")]
        [Tooltip("Create the audio system automatically before the first scene loads (only when this asset is at Resources/" + DEFAULT_RESOURCES_PATH + ").")]
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
        [Min(1)]
        [SerializeField] private int initialVoices = 16;
        [Tooltip("Hard cap on simultaneous sounds. When reached, the least important / oldest sound is stolen.")]
        [Min(1)]
        [SerializeField] private int maxVoices = 64;

        [Header("Fades (seconds, unscaled time)")]
        [Min(0f)]
        [SerializeField] private float defaultMusicFade = 1f;
        [Tooltip("Curve for music fades and crossfades. Equal Power keeps the loudness steady through a crossfade.")]
        [SerializeField] private FadeCurve musicFadeCurve = FadeCurve.EqualPower;
        [Min(0f)]
        [SerializeField] private float defaultPauseFade = 0.3f;
        [Tooltip("Fade used when StopOldest makes room for a new instance.")]
        [Min(0f)]
        [SerializeField] private float voiceStealFade = 0.05f;

        [Header("Ducking")]
        [Tooltip("Attack of the per-sound Duck Music setting.")]
        [Min(0f)]
        [SerializeField] private float duckAttack = 0.1f;
        [Tooltip("Release of the per-sound Duck Music setting.")]
        [Min(0f)]
        [SerializeField] private float duckRelease = 0.5f;
        [Tooltip("Bus ducking, e.g. Dialogue lowers Music by 8 dB and Ambience by 4 dB. When several rules hit one group, the strongest wins.")]
        [SerializeField] private DuckingRule[] duckingRules = Array.Empty<DuckingRule>();

        [Header("Snapshots")]
        [Tooltip("Snapshot used when no snapshot is requested. Empty = the mixer's snapshot named 'Snapshot' (Unity's default).")]
        [SerializeField] private AudioMixerSnapshot defaultSnapshot;
        [Tooltip("Default transition time in seconds for RequestSnapshot / ReleaseSnapshot.")]
        [Min(0f)]
        [SerializeField] private float defaultSnapshotTransition = 0.5f;

        [Header("Focus")]
        [Tooltip("Silence all audio while the application window is not focused (alt-tab). Can be changed at runtime with AudioSystem.MuteWhenUnfocused.")]
        [SerializeField] private bool muteWhenUnfocused = true;
        [Tooltip("Also mute in the Editor when another application is focused. Usually off so audio keeps playing while you work.")]
        [SerializeField] private bool muteWhenUnfocusedInEditor;
        [Tooltip("Seconds to fade back in when focus returns.")]
        [Min(0f)]
        [SerializeField] private float focusFadeIn = 0.25f;

        [Header("Scenes")]
        [Tooltip("Stop all non-music sounds when the active scene changes. Music is never stopped automatically.")]
        [SerializeField] private bool stopSoundsOnSceneChange;
        [Min(0f)]
        [SerializeField] private float sceneChangeStopFade = 0.1f;

        public bool AutoInitialize
        {
            get => autoInitialize;
            internal set => autoInitialize = value;
        }

        public bool PersistAcrossScenes
        {
            get => dontDestroyOnLoad;
            internal set => dontDestroyOnLoad = value;
        }

        public AudioMixer Mixer
        {
            get => mixer;
            internal set => mixer = value;
        }

        public AudioMixerGroup MusicGroup
        {
            get => musicGroup;
            internal set => musicGroup = value;
        }

        public AudioMixerGroup SfxGroup
        {
            get => sfxGroup;
            internal set => sfxGroup = value;
        }

        public VolumeChannel[] VolumeChannels
        {
            get => volumeChannels;
            internal set => volumeChannels = value ?? Array.Empty<VolumeChannel>();
        }

        public bool SaveVolumes
        {
            get => saveVolumes;
            internal set => saveVolumes = value;
        }

        public string PlayerPrefsKeyPrefix
        {
            get => playerPrefsKeyPrefix;
            internal set => playerPrefsKeyPrefix = value;
        }

        public int InitialVoices
        {
            get => initialVoices;
            internal set => initialVoices = Mathf.Max(1, value);
        }

        public int MaxVoices
        {
            get => maxVoices;
            internal set => maxVoices = Mathf.Max(1, value);
        }

        public float DefaultMusicFade
        {
            get => defaultMusicFade;
            internal set => defaultMusicFade = Mathf.Max(0f, value);
        }

        public FadeCurve MusicFadeCurve
        {
            get => musicFadeCurve;
            internal set => musicFadeCurve = value;
        }

        public float DefaultPauseFade
        {
            get => defaultPauseFade;
            internal set => defaultPauseFade = Mathf.Max(0f, value);
        }

        public float VoiceStealFade
        {
            get => voiceStealFade;
            internal set => voiceStealFade = Mathf.Max(0f, value);
        }

        public float DuckAttack
        {
            get => duckAttack;
            internal set => duckAttack = Mathf.Max(0f, value);
        }

        public float DuckRelease
        {
            get => duckRelease;
            internal set => duckRelease = Mathf.Max(0f, value);
        }

        public DuckingRule[] DuckingRules
        {
            get => duckingRules;
            internal set => duckingRules = value ?? Array.Empty<DuckingRule>();
        }

        public AudioMixerSnapshot DefaultSnapshot
        {
            get => defaultSnapshot;
            internal set => defaultSnapshot = value;
        }

        public float DefaultSnapshotTransition
        {
            get => defaultSnapshotTransition;
            internal set => defaultSnapshotTransition = Mathf.Max(0f, value);
        }

        public bool MuteWhenUnfocused
        {
            get => muteWhenUnfocused;
            internal set => muteWhenUnfocused = value;
        }

        public bool MuteWhenUnfocusedInEditor
        {
            get => muteWhenUnfocusedInEditor;
            internal set => muteWhenUnfocusedInEditor = value;
        }

        public float FocusFadeIn
        {
            get => focusFadeIn;
            internal set => focusFadeIn = Mathf.Max(0f, value);
        }

        public bool StopSoundsOnSceneChange
        {
            get => stopSoundsOnSceneChange;
            internal set => stopSoundsOnSceneChange = value;
        }

        public float SceneChangeStopFade
        {
            get => sceneChangeStopFade;
            internal set => sceneChangeStopFade = Mathf.Max(0f, value);
        }

        internal static AudioSystemSettings CreateDefault()
        {
            AudioSystemSettings settings = CreateInstance<AudioSystemSettings>();
            settings.name = "AudioSystemSettings (Default)";
            return settings;
        }

        private void OnValidate()
        {
            if (initialVoices > maxVoices)
            {
                initialVoices = maxVoices;
            }
        }
    }
}
