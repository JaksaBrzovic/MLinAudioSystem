using UnityEngine;
using UnityEngine.Audio;

namespace MLin.Audio
{
    /// <summary>What a voice should play and how it starts.</summary>
    internal readonly struct VoiceStart
    {
        public VoiceStart(AudioClip introClip, AudioClip mainClip, int startSample, float fadeIn, FadeCurve curve, float startDelay, double scheduledDspTime = 0d)
        {
            IntroClip = introClip;
            MainClip = mainClip;
            StartSample = startSample;
            FadeIn = fadeIn;
            Curve = curve;
            StartDelay = startDelay;
            ScheduledDspTime = scheduledDspTime;
        }

        /// <summary>Played once before <see cref="MainClip"/>; null for no intro (or when resuming past it).</summary>
        public AudioClip IntroClip { get; }
        public AudioClip MainClip { get; }
        /// <summary>Sample of the first clip (the intro if there is one) to start from.</summary>
        public int StartSample { get; }
        public float FadeIn { get; }
        public FadeCurve Curve { get; }
        /// <summary>Seconds to wait before the sound starts (music sync). The fade starts with the sound.</summary>
        public float StartDelay { get; }
        /// <summary>
        /// Exact DSP time to start at, shared by voices that must stay sample-aligned (music layers). 0 = derive the
        /// start from <see cref="StartDelay"/>.
        /// </summary>
        public double ScheduledDspTime { get; }
    }

    /// <summary>
    /// One pooled sound slot. Playback length is tracked with our own timer (frozen while the listener is paused)
    /// instead of relying only on AudioSource.isPlaying, which is unreliable while paused and when audio is disabled.
    /// An intro clip plays on the first AudioSource and the main clip is scheduled sample-accurately on a second one.
    /// </summary>
    internal sealed class AudioVoice
    {
        // after the expected end, wait at most this long for the AudioSource to report it stopped (streaming/start latency)
        private const float END_GRACE_TIME = 0.25f;
        // a scheduled start needs some lead time, otherwise the requested DSP time is already in the past
        private const double SCHEDULE_LEAD = 0.05;

        private readonly Transform transform;
        private readonly Transform parent;
        private readonly AudioSource firstSource;
        private AudioSource mainSource;

        private float baseVolume;
        private float userVolume;
        private float userVolumeTarget;
        private float userVolumeSpeed;
        private float fade;
        private float fadeTarget;
        private float fadeSpeed;
        private FadeCurve fadeCurve;
        private bool isStopping;
        private float stopDelay;
        private float pendingFadeOut;
        private float startDelay;
        private double scheduledDspTime;
        private int startSample;
        private bool hasIntro;
        private bool isInIntro;
        private float introRemaining;
        private bool isScheduled;
        private bool isFrozen;
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

            firstSource = gameObject.AddComponent<AudioSource>();
            firstSource.playOnAwake = false;
        }

        public int Index { get; }
        public bool IsActive { get; private set; }
        public int Id { get; private set; }
        public long PlayOrder { get; private set; }
        public SoundDefinition Definition { get; private set; }
        public AudioMixerGroup Output { get; private set; }
        public bool IsMusic { get; private set; }
        /// <summary>True from Stop until release, including a delayed stop that has not started fading yet.</summary>
        public bool IsStopping => isStopping;
        /// <summary>True while a synced start is waiting for its beat.</summary>
        public bool IsWaitingToStart => startDelay > 0f;
        public float StartDelay => startDelay;
        public bool IgnorePause { get; private set; }
        public float DuckAmount { get; private set; }
        public int Priority { get; private set; }
        /// <summary>Clip that is currently heard (the intro, then the main clip) and its sample position.</summary>
        public AudioClip Clip => ActiveSource.clip;
        public int PlaybackSample => ActiveSource.timeSamples;
        public float Pitch => firstSource.pitch;
        /// <summary>Volume last written to the AudioSource (all gains applied).</summary>
        public float AppliedVolume => firstSource.volume;

        private AudioSource ActiveSource => hasIntro && !isInIntro ? mainSource : firstSource;

        public void Play(SoundDefinition definition, VoiceStart start, AudioMixerGroup output, int id, long playOrder,
            bool isMusic, Vector3? position, Transform follow)
        {
            Definition = definition;
            Output = output;
            Id = id;
            PlayOrder = playOrder;
            IsMusic = isMusic;
            IgnorePause = definition.IgnorePause;
            DuckAmount = isMusic ? 0f : definition.DuckMusic;
            Priority = definition.Priority;
            IsActive = true;

            baseVolume = definition.PickVolume();
            userVolume = 1f;
            userVolumeTarget = 1f;
            userVolumeSpeed = 0f;
            isStopping = false;
            stopDelay = 0f;
            fadeCurve = start.Curve;
            fadeTarget = 1f;
            if (start.FadeIn > 0f)
            {
                fade = 0f;
                fadeSpeed = 1f / start.FadeIn;
            }
            else
            {
                fade = 1f;
                fadeSpeed = 0f;
            }

            float pitch = definition.PickPitch();
            hasIntro = start.IntroClip != null;
            isInIntro = hasIntro;
            AudioClip firstClip = hasIntro ? start.IntroClip : start.MainClip;
            startSample = Mathf.Clamp(start.StartSample, 0, Mathf.Max(0, firstClip.samples - 1));
            float firstClipRemaining = (firstClip.samples - startSample) / (float)firstClip.frequency / pitch;
            introRemaining = hasIntro ? firstClipRemaining : 0f;
            float mainLength = hasIntro ? start.MainClip.length / pitch : firstClipRemaining;
            duration = definition.Loop ? float.PositiveInfinity : introRemaining + mainLength;
            elapsed = 0f;
            startDelay = Mathf.Max(0f, start.StartDelay);
            scheduledDspTime = start.ScheduledDspTime;

            followTarget = follow;
            isFollowing = follow != null;
            bool isPositional = isFollowing || position.HasValue;
            if (isFollowing)
            {
                transform.position = follow.position;
            }
            else
            {
                transform.position = position ?? parent.position;
            }

            Configure(firstSource, firstClip, output, pitch, isPositional);
            firstSource.loop = !hasIntro && definition.Loop;
            if (hasIntro)
            {
                EnsureMainSource();
                Configure(mainSource, start.MainClip, output, pitch, isPositional);
                mainSource.loop = definition.Loop;
            }

            // while the listener is paused nothing is started; the first unpaused Tick schedules playback
            isScheduled = false;
            isFrozen = AudioListener.pause && !IgnorePause;
            if (!isFrozen)
            {
                StartPlayback();
            }
        }

        /// <param name="fade">Seconds to move from the current level to <paramref name="volume"/>; 0 = immediately.</param>
        public void SetUserVolume(float volume, float fade = 0f)
        {
            userVolumeTarget = Mathf.Clamp01(volume);
            if (fade <= 0f)
            {
                userVolume = userVolumeTarget;
                userVolumeSpeed = 0f;
                return;
            }

            userVolumeSpeed = Mathf.Abs(userVolumeTarget - userVolume) / fade;
        }

        /// <summary>Fades from the current level (never jumps) and releases the voice when silent.</summary>
        /// <param name="delay">Seconds to keep playing before the fade starts (music sync).</param>
        public void Stop(float fadeOut, float delay = 0f)
        {
            if (delay > 0f)
            {
                isStopping = true;
                stopDelay = delay;
                pendingFadeOut = fadeOut;
                return;
            }

            BeginFadeOut(fadeOut);
        }

        /// <summary>Cancels a running or pending Stop and fades back up from the current level.</summary>
        public void CancelStop(float fadeIn)
        {
            isStopping = false;
            stopDelay = 0f;
            fadeTarget = 1f;
            if (fadeIn > 0f)
            {
                fadeSpeed = (1f - fade) / fadeIn;
            }
            else
            {
                fade = 1f;
                fadeSpeed = 0f;
            }
        }

        /// <returns>False when the voice finished during this tick and was released.</returns>
        public bool Tick(float deltaTime, bool isListenerPaused, float pauseGain, float duckGain)
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

            bool shouldFreeze = isListenerPaused && !IgnorePause;
            if (shouldFreeze != isFrozen)
            {
                isFrozen = shouldFreeze;
                HandleFreezeChanged();
            }

            if (!isFrozen)
            {
                if (startDelay > 0f)
                {
                    startDelay = Mathf.Max(0f, startDelay - deltaTime);
                }

                if (stopDelay > 0f)
                {
                    stopDelay -= deltaTime;
                    if (stopDelay <= 0f && !BeginFadeOut(pendingFadeOut))
                    {
                        return false;
                    }
                }
            }

            // nothing runs before a synced start, not even the fade
            if (startDelay > 0f)
            {
                ApplyVolume(pauseGain, duckGain);
                return true;
            }

            // fades keep running while paused (the voice is silent anyway) so a Stop during pause still completes
            if (fade != fadeTarget)
            {
                fade = Mathf.MoveTowards(fade, fadeTarget, fadeSpeed * deltaTime);
            }

            if (userVolume != userVolumeTarget)
            {
                userVolume = Mathf.MoveTowards(userVolume, userVolumeTarget, userVolumeSpeed * deltaTime);
            }

            if (isStopping && stopDelay <= 0f && fade <= 0f)
            {
                Release();
                return false;
            }

            if (!isFrozen)
            {
                elapsed += deltaTime;
                UpdateIntro(deltaTime);
            }

            if (elapsed >= duration && (!ActiveSource.isPlaying || elapsed >= duration + END_GRACE_TIME))
            {
                Release();
                return false;
            }

            ApplyVolume(pauseGain, duckGain);
            return true;
        }

        /// <param name="duckGain">Combined ducking for this voice (Duck Music and bus rules).</param>
        public void ApplyVolume(float pauseGain, float duckGain)
        {
            float volume = baseVolume * userVolume * AudioMath.FadeGain(fade, fadeCurve) * duckGain;
            if (!IgnorePause)
            {
                volume *= pauseGain;
            }

            firstSource.volume = volume;
            if (mainSource != null)
            {
                mainSource.volume = volume;
            }
        }

        public void Release()
        {
            StopSources();
            if (firstSource != null)
            {
                firstSource.clip = null;
            }

            if (mainSource != null)
            {
                mainSource.clip = null;
            }

            IsActive = false;
            Id = 0;
            Definition = null;
            Output = null;
            followTarget = null;
            isFollowing = false;
            isStopping = false;
            stopDelay = 0f;
            startDelay = 0f;
            scheduledDspTime = 0d;
            hasIntro = false;
            isInIntro = false;
            isScheduled = false;
        }

        private void Configure(AudioSource source, AudioClip clip, AudioMixerGroup output, float pitch, bool isPositional)
        {
            source.clip = clip;
            source.outputAudioMixerGroup = output;
            source.pitch = pitch;
            source.priority = Definition.Priority;
            source.ignoreListenerPause = Definition.IgnorePause;
            source.spatialBlend = isPositional ? Definition.SpatialBlend : 0f;
            source.minDistance = Definition.MinDistance;
            source.maxDistance = Mathf.Max(Definition.MinDistance, Definition.MaxDistance);
            source.rolloffMode = Definition.RolloffMode;
            source.dopplerLevel = Definition.DopplerLevel;
            source.volume = 0f;
        }

        private void EnsureMainSource()
        {
            if (mainSource == null)
            {
                mainSource = transform.gameObject.AddComponent<AudioSource>();
                mainSource.playOnAwake = false;
            }
        }

        private void StartPlayback()
        {
            isScheduled = true;
            firstSource.timeSamples = startSample;

            if (!hasIntro && startDelay <= 0f)
            {
                firstSource.Play();
                return;
            }

            // a shared absolute start keeps layered voices sample-aligned; after a pause it has passed and the delay is used
            double now = AudioSettings.dspTime;
            double startTime = scheduledDspTime > now ? scheduledDspTime : now + System.Math.Max(SCHEDULE_LEAD, startDelay);
            firstSource.PlayScheduled(startTime);
            if (isInIntro)
            {
                mainSource.PlayScheduled(startTime + introRemaining);
            }
        }

        private void HandleFreezeChanged()
        {
            if (isFrozen)
            {
                // Unity pauses sources that are playing, but a source that is only scheduled would still start on time
                if (startDelay > 0f)
                {
                    StopSources();
                    isScheduled = false;
                }
                else if (isInIntro)
                {
                    mainSource.Stop();
                }

                return;
            }

            if (!isScheduled)
            {
                StartPlayback();
            }
            else if (isInIntro)
            {
                // the intro resumes where it was paused, so schedule the main clip from the intro's real position
                AudioClip intro = firstSource.clip;
                introRemaining = (intro.samples - firstSource.timeSamples) / (float)intro.frequency / firstSource.pitch;
                mainSource.PlayScheduled(AudioSettings.dspTime + introRemaining);
            }
        }

        private void UpdateIntro(float deltaTime)
        {
            if (!isInIntro)
            {
                return;
            }

            // follow the audio clock while the intro plays, so the switch to the main clip does not drift
            if (firstSource.isPlaying)
            {
                AudioClip intro = firstSource.clip;
                introRemaining = (intro.samples - firstSource.timeSamples) / (float)intro.frequency / firstSource.pitch;
            }
            else
            {
                introRemaining -= deltaTime;
            }

            if (introRemaining <= 0f)
            {
                isInIntro = false;
            }
        }

        /// <returns>False when the voice was released right away.</returns>
        private bool BeginFadeOut(float fadeOut)
        {
            stopDelay = 0f;
            if (fadeOut <= 0f || fade <= 0f)
            {
                Release();
                return false;
            }

            isStopping = true;
            fadeTarget = 0f;
            fadeSpeed = fade / fadeOut;
            return true;
        }

        private void StopSources()
        {
            if (firstSource != null)
            {
                firstSource.Stop();
            }

            if (mainSource != null)
            {
                mainSource.Stop();
            }
        }
    }
}
