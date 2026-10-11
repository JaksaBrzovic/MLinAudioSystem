using UnityEngine;
using UnityEngine.Audio;

namespace MLin.Audio.Samples
{
    /// <summary>
    /// On-screen test panel for the audio system. All sounds are generated procedurally so the sample has no audio files;
    /// in a real project SoundDefinitions are assets created via Create > MLin > Audio > Sound Definition.
    /// </summary>
    public class AudioSystemDemo : MonoBehaviour
    {
        private const int SampleRate = 44100;

        [SerializeField] private AudioMixerGroup uiGroup;

        private SoundDefinition click;
        private SoundDefinition confirm;
        private SoundDefinition jingle;
        private SoundDefinition blip3D;
        private SoundDefinition hum;
        private SoundDefinition musicA;
        private SoundDefinition musicB;

        private SoundHandle humHandle;
        private Vector2 scroll;

        private void Awake()
        {
            click = SoundDefinition.Create(Tone("Click 1", 0.06f, 820f), Tone("Click 2", 0.06f, 880f), Tone("Click 3", 0.06f, 940f));
            click.name = "Click";
            click.PitchVariation = 0.05f;
            click.Cooldown = 0.05f;
            click.MaxInstances = 3;

            confirm = SoundDefinition.Create(Arpeggio("Confirm", 0.08f, 660f, 990f));
            confirm.name = "Confirm (ignores pause, UI group)";
            confirm.IgnorePause = true;
            confirm.OutputGroup = uiGroup;

            jingle = SoundDefinition.Create(Arpeggio("Jingle", 0.25f, 523.25f, 659.25f, 783.99f, 1046.5f));
            jingle.name = "Jingle (ducks music)";
            jingle.DuckMusic = 0.8f;
            jingle.MaxInstances = 1;
            jingle.InstanceLimitMode = InstanceLimitMode.RejectNew;

            blip3D = SoundDefinition.Create(Tone("Blip", 0.3f, 440f));
            blip3D.name = "3D Blip";
            blip3D.SpatialBlend = 1f;
            blip3D.MaxDistance = 20f;

            hum = SoundDefinition.Create(LoopingPad("Hum", 2f, 110f, 165f));
            hum.name = "Hum (loop)";
            hum.Loop = true;
            hum.Volume = 0.4f;
            hum.MaxInstances = 1;

            musicA = SoundDefinition.Create(LoopingPad("Music A", 4f, 220f, 261.63f, 329.63f));
            musicA.name = "Music A (A minor)";
            musicA.Loop = true;
            musicA.Volume = 0.5f;

            musicB = SoundDefinition.Create(LoopingPad("Music B", 4f, 261.63f, 329.63f, 392f));
            musicB.name = "Music B (C major)";
            musicB.Loop = true;
            musicB.Volume = 0.5f;
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 10, 360, Screen.height - 20), GUI.skin.box);
            scroll = GUILayout.BeginScrollView(scroll);

            GUILayout.Label("<b>MLin Audio System - Demo</b>", new GUIStyle(GUI.skin.label) { richText = true });
            if (AudioSystem.Host != null)
                GUILayout.Label($"Voices: {AudioSystem.Host.ActiveVoiceCount} active / {AudioSystem.Host.VoiceCount} pooled");
            GUILayout.Label($"Music: {(AudioSystem.CurrentMusic != null ? AudioSystem.CurrentMusic.name : "-")}");
            GUILayout.Label($"Paused: {AudioSystem.IsPaused}");

            GUILayout.Space(8);
            GUILayout.Label("Sounds");
            if (GUILayout.Button("Click (3 variations, cooldown, max 3)"))
                AudioSystem.Play(click);
            if (GUILayout.Button("Click x10 in one frame (limits in action)"))
            {
                for (int i = 0; i < 10; i++)
                    AudioSystem.Play(click);
            }
            if (GUILayout.Button(confirm.name))
                AudioSystem.Play(confirm);
            if (GUILayout.Button(jingle.name))
                AudioSystem.Play(jingle);
            if (GUILayout.Button("3D Blip at random position"))
                AudioSystem.PlayAt(blip3D, Random.insideUnitSphere * 10f);
            if (GUILayout.Button(humHandle.IsPlaying ? "Stop Hum (1s fade)" : "Start Hum (1s fade in)"))
            {
                if (humHandle.IsPlaying)
                    humHandle.Stop(1f);
                else
                    humHandle = AudioSystem.Play(hum, 1f);
            }

            GUILayout.Space(8);
            GUILayout.Label("Music");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Music A"))
                AudioSystem.PlayMusic(musicA, 2f);
            if (GUILayout.Button("Music B"))
                AudioSystem.PlayMusic(musicB, 2f);
            if (GUILayout.Button("Stop"))
                AudioSystem.StopMusic(1f);
            GUILayout.EndHorizontal();

            GUILayout.Space(8);
            GUILayout.Label("Pause (spam it - it never gets stuck)");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Pause"))
                AudioSystem.Pause(0.5f);
            if (GUILayout.Button("Resume"))
                AudioSystem.Resume(0.5f);
            if (GUILayout.Button("Toggle"))
            {
                if (AudioSystem.IsPaused)
                    AudioSystem.Resume(0.5f);
                else
                    AudioSystem.Pause(0.5f);
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(8);
            GUILayout.Label("Volume channels (saved in PlayerPrefs)");
            if (AudioSystem.Settings != null)
            {
                foreach (VolumeChannel channel in AudioSystem.Settings.VolumeChannels)
                    VolumeSlider(channel.name);
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private static void VolumeSlider(string channel)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(channel, GUILayout.Width(60));
            float current = AudioSystem.GetChannelVolume(channel);
            float next = GUILayout.HorizontalSlider(current, 0f, 1f);
            GUILayout.Label(next.ToString("0.00"), GUILayout.Width(40));
            GUILayout.EndHorizontal();

            if (!Mathf.Approximately(current, next))
                AudioSystem.SetChannelVolume(channel, next);
        }

        #region PROCEDURAL CLIPS
        private static AudioClip Tone(string name, float seconds, float frequency)
        {
            int length = (int)(SampleRate * seconds);
            float[] data = new float[length];
            for (int i = 0; i < length; i++)
                data[i] = Mathf.Sin(2f * Mathf.PI * frequency * i / SampleRate) * Envelope(i, length) * 0.5f;

            return CreateClip(name, data);
        }

        private static AudioClip Arpeggio(string name, float noteSeconds, params float[] frequencies)
        {
            int noteLength = (int)(SampleRate * noteSeconds);
            float[] data = new float[noteLength * frequencies.Length];
            for (int note = 0; note < frequencies.Length; note++)
            {
                for (int i = 0; i < noteLength; i++)
                    data[note * noteLength + i] = Mathf.Sin(2f * Mathf.PI * frequencies[note] * i / SampleRate) * Envelope(i, noteLength) * 0.4f;
            }

            return CreateClip(name, data);
        }

        /// <summary>Chord whose frequencies are rounded to whole cycles over the clip length, so it loops without clicks.</summary>
        private static AudioClip LoopingPad(string name, float seconds, params float[] frequencies)
        {
            int length = (int)(SampleRate * seconds);
            float[] data = new float[length];
            foreach (float frequency in frequencies)
            {
                float loopFrequency = Mathf.Round(frequency * seconds) / seconds;
                for (int i = 0; i < length; i++)
                    data[i] += Mathf.Sin(2f * Mathf.PI * loopFrequency * i / SampleRate) * 0.25f / frequencies.Length;
            }

            return CreateClip(name, data);
        }

        private static float Envelope(int sample, int length)
        {
            float t = (float)sample / length;
            float attack = Mathf.Clamp01(t / 0.05f);
            float release = Mathf.Clamp01((1f - t) / 0.4f);
            return attack * release;
        }

        private static AudioClip CreateClip(string name, float[] data)
        {
            AudioClip clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
        #endregion
    }
}
