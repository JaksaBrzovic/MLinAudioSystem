using UnityEngine;
using UnityEngine.Audio;

namespace MLin.Audio
{
    /// <summary>
    /// One pooled AudioSource. Playback length is tracked with our own timer (frozen while the listener is paused)
    /// instead of relying only on AudioSource.isPlaying, which is unreliable while paused and when audio is disabled.
    /// </summary>
    internal sealed class AudioVoice
    {
        // after the expected end, wait at most this long for the AudioSource to report it stopped (streaming/start latency)
        private const float EndGraceTime = 0.25f;

        public readonly int Index;
        public readonly AudioSource Source;
        private readonly Transform transform;
        private readonly Transform parent;

        public bool Active { get; private set; }
        public int Id { get; private set; }
        public long PlayOrder { get; private set; }
        public SoundDefinition Definition { get; private set; }
        public bool IsMusic { get; private set; }
        public bool IsStopping => stopWhenSilent;
        public bool IgnorePause { get; private set; }
        public float DuckAmount { get; private set; }
        public int Priority { get; private set; }

        private float baseVolume;
        private float userVolume;
        private float fade;
        private float fadeTarget;
        private float fadeSpeed;
        private bool stopWhenSilent;
        private float elapsed;
        private float duration;
        private Transform followTarget;
        private bool isFollowing;

        public AudioVoice(int index, Transform parent)
        {
            Index = index;
            this.parent = parent;

            GameObject gameObject = new GameObject($"Voice {index:00}");
            transform = gameObject.transform;
            transform.SetParent(parent, false);

            Source = gameObject.AddComponent<AudioSource>();
            Source.playOnAwake = false;
        }

        public void Play(SoundDefinition definition, AudioClip clip, AudioMixerGroup output, int id, long playOrder,
            bool isMusic, float fadeIn, Vector3? position, Transform follow)
        {
            Definition = definition;
            Id = id;
            PlayOrder = playOrder;
            IsMusic = isMusic;
            IgnorePause = definition.IgnorePause;
            DuckAmount = isMusic ? 0f : definition.DuckMusic;
            Priority = definition.Priority;
            Active = true;

            baseVolume = definition.PickVolume();
            userVolume = 1f;
            stopWhenSilent = false;
            fadeTarget = 1f;
            if (fadeIn > 0f)
            {
                fade = 0f;
                fadeSpeed = 1f / fadeIn;
            }
            else
            {
                fade = 1f;
                fadeSpeed = 0f;
            }

            float pitch = definition.PickPitch();
            elapsed = 0f;
            duration = definition.Loop ? float.PositiveInfinity : clip.length / pitch;

            followTarget = follow;
            isFollowing = follow != null;
            bool positional = isFollowing || position.HasValue;
            if (isFollowing)
                transform.position = follow.position;
            else
                transform.position = position ?? parent.position;

            Source.clip = clip;
            Source.outputAudioMixerGroup = output;
            Source.loop = definition.Loop;
            Source.pitch = pitch;
            Source.priority = definition.Priority;
            Source.ignoreListenerPause = definition.IgnorePause;
            Source.spatialBlend = positional ? definition.SpatialBlend : 0f;
            Source.minDistance = definition.MinDistance;
            Source.maxDistance = Mathf.Max(definition.MinDistance, definition.MaxDistance);
            Source.rolloffMode = definition.RolloffMode;
            Source.dopplerLevel = definition.DopplerLevel;
            Source.volume = 0f;
            Source.Play();
        }

        public void SetUserVolume(float volume)
        {
            userVolume = Mathf.Clamp01(volume);
        }

        /// <summary>Fades from the current level (never jumps) and releases the voice when silent.</summary>
        public void Stop(float fadeOut)
        {
            if (fadeOut <= 0f || fade <= 0f)
            {
                Release();
                return;
            }

            stopWhenSilent = true;
            fadeTarget = 0f;
            fadeSpeed = fade / fadeOut;
        }

        /// <returns>False when the voice finished during this tick and was released.</returns>
        public bool Tick(float deltaTime, bool listenerPaused, float pauseGain, float duckGain)
        {
            if (isFollowing)
            {
                // the object we were attached to got destroyed
                if (followTarget == null)
                {
                    Release();
                    return false;
                }

                transform.position = followTarget.position;
            }

            // fades keep running while paused (the voice is silent anyway) so a Stop during pause still completes
            if (fade != fadeTarget)
                fade = Mathf.MoveTowards(fade, fadeTarget, fadeSpeed * deltaTime);

            if (stopWhenSilent && fade <= 0f)
            {
                Release();
                return false;
            }

            bool frozen = listenerPaused && !IgnorePause;
            if (!frozen)
                elapsed += deltaTime;

            if (elapsed >= duration && (!Source.isPlaying || elapsed >= duration + EndGraceTime))
            {
                Release();
                return false;
            }

            ApplyVolume(pauseGain, duckGain);
            return true;
        }

        public void ApplyVolume(float pauseGain, float duckGain)
        {
            float volume = baseVolume * userVolume * fade;
            if (!IgnorePause)
                volume *= pauseGain;
            if (IsMusic)
                volume *= duckGain;

            Source.volume = volume;
        }

        public void Release()
        {
            if (Source != null)
            {
                Source.Stop();
                Source.clip = null;
            }

            Active = false;
            Id = 0;
            Definition = null;
            followTarget = null;
            isFollowing = false;
            stopWhenSilent = false;
        }
    }
}
