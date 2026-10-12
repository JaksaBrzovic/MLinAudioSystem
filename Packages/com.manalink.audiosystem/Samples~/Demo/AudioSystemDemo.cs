using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace MLin.Audio.Samples
{
    /// <summary>
    /// Drives the demo panel: turns panel requests into AudioSystem calls and shows the resulting state.
    /// All sounds are generated procedurally so the sample has no audio files;
    /// in a real project SoundDefinitions are assets created via Create > MLin > Audio > Sound Definition.
    /// </summary>
    public class AudioSystemDemo : MonoBehaviour
    {
        private const int SAMPLE_RATE = 44100;
        private const int CLICK_BURST_COUNT = 10;
        private const float HUM_FADE = 1f;
        private const float MUSIC_CROSSFADE = 2f;
        private const float MUSIC_STOP_FADE = 1f;
        private const float PAUSE_FADE = 0.5f;
        private const float BLIP_RADIUS = 10f;

        [SerializeField] private DemoPanelView panelView;
        [SerializeField] private AudioMixerGroup uiGroup;

        private readonly List<SoundDefinition> ownedDefinitions = new List<SoundDefinition>();
        private readonly List<AudioClip> ownedClips = new List<AudioClip>();

        private SoundDefinition click;
        private SoundDefinition confirm;
        private SoundDefinition jingle;
        private SoundDefinition blip3D;
        private SoundDefinition hum;
        private SoundDefinition musicA;
        private SoundDefinition musicB;

        private SoundHandle humHandle;

        #region UNITY LIFECYCLE
        private void Awake()
        {
            if (panelView == null)
            {
                Debug.LogError($"[MLin Audio Demo] '{name}' has no Panel View assigned. Assign the DemoUi instance in the scene.", this);
                enabled = false;
                return;
            }

            CreateSounds();
        }

        private void OnEnable()
        {
            if (panelView == null)
            {
                return;
            }

            panelView.ActionRequested += HandleActionRequested;
            panelView.VolumeChanged += HandleVolumeChanged;
        }

        private void Start()
        {
            // the AudioSystemHost in the scene runs first (DefaultExecutionOrder), so settings are available here
            if (AudioSystem.Settings == null)
            {
                return;
            }

            foreach (VolumeChannel channel in AudioSystem.Settings.VolumeChannels)
            {
                if (channel != null && !string.IsNullOrEmpty(channel.Name))
                {
                    panelView.AddVolumeRow(channel.Name, AudioSystem.GetChannelVolume(channel.Name));
                }
            }
        }

        private void Update()
        {
            AudioSystemHost host = AudioSystem.Host;
            panelView.ShowVoices(host != null ? host.ActiveVoiceCount : 0, host != null ? host.VoiceCount : 0);
            panelView.ShowMusic(AudioSystem.CurrentMusic);
            panelView.ShowPaused(AudioSystem.IsPaused);
            panelView.ShowHumPlaying(humHandle.IsPlaying);
        }

        private void OnDisable()
        {
            if (panelView == null)
            {
                return;
            }

            panelView.ActionRequested -= HandleActionRequested;
            panelView.VolumeChanged -= HandleVolumeChanged;
        }

        private void OnDestroy()
        {
            // stop everything that still uses the runtime clips before releasing them
            foreach (SoundDefinition definition in ownedDefinitions)
            {
                if (AudioSystem.CurrentMusic == definition)
                {
                    AudioSystem.StopMusic(0f);
                }

                AudioSystem.StopAll(definition);
                Destroy(definition);
            }

            foreach (AudioClip clip in ownedClips)
            {
                Destroy(clip);
            }

            ownedDefinitions.Clear();
            ownedClips.Clear();
        }
        #endregion

        #region HANDLERS
        private void HandleActionRequested(DemoAction action)
        {
            switch (action)
            {
                case DemoAction.PlayClick:
                    AudioSystem.Play(click);
                    break;

                case DemoAction.PlayClickBurst:
                    for (int i = 0; i < CLICK_BURST_COUNT; i++)
                    {
                        AudioSystem.Play(click);
                    }
                    break;

                case DemoAction.PlayConfirm:
                    AudioSystem.Play(confirm);
                    break;

                case DemoAction.PlayJingle:
                    AudioSystem.Play(jingle);
                    break;

                case DemoAction.PlayBlip3D:
                    AudioSystem.PlayAt(blip3D, Random.insideUnitSphere * BLIP_RADIUS);
                    break;

                case DemoAction.ToggleHum:
                    if (humHandle.IsPlaying)
                    {
                        humHandle.Stop(HUM_FADE);
                    }
                    else
                    {
                        humHandle = AudioSystem.Play(hum, HUM_FADE);
                    }
                    break;

                case DemoAction.PlayMusicA:
                    AudioSystem.PlayMusic(musicA, MUSIC_CROSSFADE);
                    break;

                case DemoAction.PlayMusicB:
                    AudioSystem.PlayMusic(musicB, MUSIC_CROSSFADE);
                    break;

                case DemoAction.StopMusic:
                    AudioSystem.StopMusic(MUSIC_STOP_FADE);
                    break;

                case DemoAction.Pause:
                    AudioSystem.Pause(PAUSE_FADE);
                    break;

                case DemoAction.Resume:
                    AudioSystem.Resume(PAUSE_FADE);
                    break;

                case DemoAction.TogglePause:
                    if (AudioSystem.IsPaused)
                    {
                        AudioSystem.Resume(PAUSE_FADE);
                    }
                    else
                    {
                        AudioSystem.Pause(PAUSE_FADE);
                    }
                    break;
            }
        }

        private void HandleVolumeChanged(string channel, float volume)
        {
            AudioSystem.SetChannelVolume(channel, volume);
        }
        #endregion

        #region SOUNDS
        private void CreateSounds()
        {
            click = CreateDefinition("Click", Tone("Click 1", 0.06f, 820f), Tone("Click 2", 0.06f, 880f), Tone("Click 3", 0.06f, 940f));
            click.PitchVariation = 0.05f;
            click.Cooldown = 0.05f;
            click.MaxInstances = 3;

            confirm = CreateDefinition("Confirm", Arpeggio("Confirm", 0.08f, 660f, 990f));
            confirm.IgnorePause = true;
            confirm.OutputGroup = uiGroup;

            jingle = CreateDefinition("Jingle", Arpeggio("Jingle", 0.25f, 523.25f, 659.25f, 783.99f, 1046.5f));
            jingle.DuckMusic = 0.8f;
            jingle.MaxInstances = 1;
            jingle.InstanceLimitMode = InstanceLimitMode.RejectNew;

            blip3D = CreateDefinition("3D Blip", Tone("Blip", 0.3f, 440f));
            blip3D.SpatialBlend = 1f;
            blip3D.MaxDistance = 20f;

            hum = CreateDefinition("Hum", LoopingPad("Hum", 2f, 110f, 165f));
            hum.Loop = true;
            hum.Volume = 0.4f;
            hum.MaxInstances = 1;

            musicA = CreateDefinition("Music A (A minor)", LoopingPad("Music A", 4f, 220f, 261.63f, 329.63f));
            musicA.Loop = true;
            musicA.Volume = 0.5f;

            musicB = CreateDefinition("Music B (C major)", LoopingPad("Music B", 4f, 261.63f, 329.63f, 392f));
            musicB.Loop = true;
            musicB.Volume = 0.5f;
        }

        private SoundDefinition CreateDefinition(string definitionName, params AudioClip[] clips)
        {
            SoundDefinition definition = SoundDefinition.Create(clips);
            definition.name = definitionName;
            ownedDefinitions.Add(definition);
            return definition;
        }
        #endregion

        #region PROCEDURAL CLIPS
        private AudioClip Tone(string clipName, float seconds, float frequency)
        {
            int length = (int)(SAMPLE_RATE * seconds);
            float[] data = new float[length];
            for (int i = 0; i < length; i++)
            {
                data[i] = Mathf.Sin(2f * Mathf.PI * frequency * i / SAMPLE_RATE) * Envelope(i, length) * 0.5f;
            }

            return CreateClip(clipName, data);
        }

        private AudioClip Arpeggio(string clipName, float noteSeconds, params float[] frequencies)
        {
            int noteLength = (int)(SAMPLE_RATE * noteSeconds);
            float[] data = new float[noteLength * frequencies.Length];
            for (int note = 0; note < frequencies.Length; note++)
            {
                for (int i = 0; i < noteLength; i++)
                {
                    data[note * noteLength + i] = Mathf.Sin(2f * Mathf.PI * frequencies[note] * i / SAMPLE_RATE) * Envelope(i, noteLength) * 0.4f;
                }
            }

            return CreateClip(clipName, data);
        }

        /// <summary>Chord whose frequencies are rounded to whole cycles over the clip length, so it loops without clicks.</summary>
        private AudioClip LoopingPad(string clipName, float seconds, params float[] frequencies)
        {
            int length = (int)(SAMPLE_RATE * seconds);
            float[] data = new float[length];
            foreach (float frequency in frequencies)
            {
                float loopFrequency = Mathf.Round(frequency * seconds) / seconds;
                for (int i = 0; i < length; i++)
                {
                    data[i] += Mathf.Sin(2f * Mathf.PI * loopFrequency * i / SAMPLE_RATE) * 0.25f / frequencies.Length;
                }
            }

            return CreateClip(clipName, data);
        }

        private static float Envelope(int sample, int length)
        {
            float t = (float)sample / length;
            float attack = Mathf.Clamp01(t / 0.05f);
            float release = Mathf.Clamp01((1f - t) / 0.4f);
            return attack * release;
        }

        private AudioClip CreateClip(string clipName, float[] data)
        {
            AudioClip clip = AudioClip.Create(clipName, data.Length, 1, SAMPLE_RATE, false);
            clip.SetData(data, 0);
            ownedClips.Add(clip);
            return clip;
        }
        #endregion
    }
}
