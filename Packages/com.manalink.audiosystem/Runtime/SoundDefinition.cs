using System;
using UnityEngine;
using UnityEngine.Audio;

namespace MLin.Audio
{
    public enum ClipSelectionMode
    {
        /// <summary>Any clip, repeats allowed.</summary>
        Random,
        /// <summary>Random clip, never the same one twice in a row.</summary>
        RandomNoRepeat,
        /// <summary>Clips in order, wrapping around.</summary>
        Sequential
    }

    public enum InstanceLimitMode
    {
        /// <summary>When the limit is reached the oldest instance is quickly faded out to make room.</summary>
        StopOldest,
        /// <summary>When the limit is reached the new play request is ignored.</summary>
        RejectNew
    }

    /// <summary>
    /// Everything needed to play one logical sound (a footstep, a UI click, a music track).
    /// Gameplay code references these assets directly instead of enum keys, so adding a sound never requires a code change.
    /// </summary>
    [CreateAssetMenu(fileName = "NewSound", menuName = "MLin/Audio/Sound Definition", order = 0)]
    public class SoundDefinition : ScriptableObject
    {
        [Header("Clips")]
        [SerializeField] private AudioClip[] clips = Array.Empty<AudioClip>();
        [SerializeField] private ClipSelectionMode selectionMode = ClipSelectionMode.RandomNoRepeat;

        [Header("Output")]
        [Tooltip("Mixer group to route to. Empty = the default Music/SFX group from AudioSystemSettings.")]
        [SerializeField] private AudioMixerGroup outputGroup;
        [SerializeField, Range(0f, 1f)] private float volume = 1f;
        [Tooltip("Random +/- offset applied to volume on every play.")]
        [SerializeField, Range(0f, 0.5f)] private float volumeVariation;
        [SerializeField, Range(0.1f, 3f)] private float pitch = 1f;
        [Tooltip("Random +/- offset applied to pitch on every play.")]
        [SerializeField, Range(0f, 0.5f)] private float pitchVariation;
        [SerializeField] private bool loop;
        [Tooltip("Unity AudioSource priority: 0 = most important, 256 = least. Also used when voices have to be stolen.")]
        [SerializeField, Range(0, 256)] private int priority = 128;

        [Header("Playback Rules")]
        [Tooltip("Minimum time in seconds (unscaled) between two plays of this sound. 0 = no cooldown.")]
        [SerializeField, Min(0f)] private float cooldown;
        [Tooltip("Maximum simultaneous instances of this sound. 0 = unlimited.")]
        [SerializeField, Min(0)] private int maxInstances;
        [SerializeField] private InstanceLimitMode instanceLimitMode = InstanceLimitMode.StopOldest;
        [Tooltip("Keep playing while the audio system is paused (e.g. pause menu UI sounds).")]
        [SerializeField] private bool ignorePause;

        [Header("Ducking")]
        [Tooltip("How much music is lowered while this sound plays. 0 = not at all, 1 = fully muted.")]
        [SerializeField, Range(0f, 1f)] private float duckMusic;

        [Header("3D")]
        [Tooltip("0 = 2D, 1 = fully 3D. Only matters for PlayAt / PlayAttached.")]
        [SerializeField, Range(0f, 1f)] private float spatialBlend;
        [SerializeField, Min(0f)] private float minDistance = 1f;
        [SerializeField, Min(0f)] private float maxDistance = 50f;
        [SerializeField] private AudioRolloffMode rolloffMode = AudioRolloffMode.Logarithmic;
        [SerializeField, Range(0f, 5f)] private float dopplerLevel;

        [NonSerialized] private int lastClipIndex = -1;
        [NonSerialized] private int nextSequentialIndex;

        #region PROPERTIES
        public AudioClip[] Clips { get => clips; set => clips = value ?? Array.Empty<AudioClip>(); }
        public ClipSelectionMode SelectionMode { get => selectionMode; set => selectionMode = value; }
        public AudioMixerGroup OutputGroup { get => outputGroup; set => outputGroup = value; }
        public float Volume { get => volume; set => volume = Mathf.Clamp01(value); }
        public float VolumeVariation { get => volumeVariation; set => volumeVariation = Mathf.Max(0f, value); }
        public float Pitch { get => pitch; set => pitch = Mathf.Max(0.01f, value); }
        public float PitchVariation { get => pitchVariation; set => pitchVariation = Mathf.Max(0f, value); }
        public bool Loop { get => loop; set => loop = value; }
        public int Priority { get => priority; set => priority = Mathf.Clamp(value, 0, 256); }
        public float Cooldown { get => cooldown; set => cooldown = Mathf.Max(0f, value); }
        public int MaxInstances { get => maxInstances; set => maxInstances = Mathf.Max(0, value); }
        public InstanceLimitMode InstanceLimitMode { get => instanceLimitMode; set => instanceLimitMode = value; }
        public bool IgnorePause { get => ignorePause; set => ignorePause = value; }
        public float DuckMusic { get => duckMusic; set => duckMusic = Mathf.Clamp01(value); }
        public float SpatialBlend { get => spatialBlend; set => spatialBlend = Mathf.Clamp01(value); }
        public float MinDistance { get => minDistance; set => minDistance = Mathf.Max(0f, value); }
        public float MaxDistance { get => maxDistance; set => maxDistance = Mathf.Max(0f, value); }
        public AudioRolloffMode RolloffMode { get => rolloffMode; set => rolloffMode = value; }
        public float DopplerLevel { get => dopplerLevel; set => dopplerLevel = Mathf.Max(0f, value); }

        public bool HasClips
        {
            get
            {
                if (clips == null)
                    return false;

                foreach (AudioClip clip in clips)
                {
                    if (clip != null)
                        return true;
                }

                return false;
            }
        }
        #endregion

        /// <summary>Creates a definition at runtime (procedural audio, tests, prototyping).</summary>
        public static SoundDefinition Create(params AudioClip[] clips)
        {
            SoundDefinition definition = CreateInstance<SoundDefinition>();
            definition.Clips = clips;
            return definition;
        }

        /// <summary>Picks the next clip according to <see cref="SelectionMode"/>. Null entries are skipped.</summary>
        public AudioClip PickClip()
        {
            if (clips == null)
                return null;

            int validCount = 0;
            foreach (AudioClip clip in clips)
            {
                if (clip != null)
                    validCount++;
            }

            if (validCount == 0)
                return null;

            // selection works on the non-null clips only, so empty slots never cause a failed play
            int validIndex = NextIndex(validCount);
            foreach (AudioClip clip in clips)
            {
                if (clip == null)
                    continue;

                if (validIndex == 0)
                    return clip;

                validIndex--;
            }

            return null;
        }

        public float PickVolume()
        {
            return Mathf.Clamp01(volume + UnityEngine.Random.Range(-volumeVariation, volumeVariation));
        }

        public float PickPitch()
        {
            return Mathf.Max(0.01f, pitch + UnityEngine.Random.Range(-pitchVariation, pitchVariation));
        }

        private int NextIndex(int count)
        {
            if (count == 1)
                return 0;

            int index;
            switch (selectionMode)
            {
                case ClipSelectionMode.Sequential:
                    index = nextSequentialIndex % count;
                    nextSequentialIndex = index + 1;
                    break;

                case ClipSelectionMode.RandomNoRepeat:
                    if (lastClipIndex < 0 || lastClipIndex >= count)
                    {
                        index = UnityEngine.Random.Range(0, count);
                    }
                    else
                    {
                        // pick from the other count-1 clips by skipping over the last one
                        index = UnityEngine.Random.Range(0, count - 1);
                        if (index >= lastClipIndex)
                            index++;
                    }
                    break;

                default:
                    index = UnityEngine.Random.Range(0, count);
                    break;
            }

            lastClipIndex = index;
            return index;
        }

        private void OnValidate()
        {
            if (maxDistance < minDistance)
                maxDistance = minDistance;
        }
    }
}
