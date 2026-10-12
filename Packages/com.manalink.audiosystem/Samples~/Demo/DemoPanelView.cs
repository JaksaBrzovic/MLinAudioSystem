using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MLin.Audio.Samples
{
    /// <summary>
    /// The demo panel. Turns button and slider input into requests and shows the state it is given;
    /// it never calls the audio system itself. Values are only written to the UI when they change.
    /// </summary>
    public class DemoPanelView : MonoBehaviour
    {
        [Header("Status")]
        [SerializeField] private TMP_Text voicesText;
        [SerializeField] private TMP_Text musicText;
        [SerializeField] private TMP_Text pausedText;

        [Header("Sounds")]
        [SerializeField] private Button clickButton;
        [SerializeField] private Button clickBurstButton;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button jingleButton;
        [SerializeField] private Button blip3DButton;
        [SerializeField] private Button humButton;
        [SerializeField] private TMP_Text humButtonText;

        [Header("Music")]
        [SerializeField] private Button musicAButton;
        [SerializeField] private Button musicBButton;
        [SerializeField] private Button stopMusicButton;

        [Header("Pause")]
        [SerializeField] private Button pauseButton;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button togglePauseButton;

        [Header("Volume")]
        [SerializeField] private RectTransform volumeSliders;
        [SerializeField] private VolumeSliderView volumeSliderPrefab;

        private readonly List<KeyValuePair<Button, UnityAction>> buttonBindings = new List<KeyValuePair<Button, UnityAction>>();
        private readonly List<VolumeSliderView> volumeRows = new List<VolumeSliderView>();

        // every Show method writes its text on the first call and afterwards only when the value changed
        private bool hasShownVoices;
        private int shownActiveVoices;
        private int shownPooledVoices;
        private bool hasShownMusic;
        private SoundDefinition shownMusic;
        private bool hasShownPaused;
        private bool isShowingPaused;
        private bool hasShownHum;
        private bool isShowingHumPlaying;

        public event Action<DemoAction> ActionRequested;
        public event Action<string, float> VolumeChanged;

        public void ShowVoices(int activeVoices, int pooledVoices)
        {
            if (hasShownVoices && activeVoices == shownActiveVoices && pooledVoices == shownPooledVoices)
            {
                return;
            }

            hasShownVoices = true;
            shownActiveVoices = activeVoices;
            shownPooledVoices = pooledVoices;
            voicesText.SetText("Voices: {0} active / {1} pooled", activeVoices, pooledVoices);
        }

        public void ShowMusic(SoundDefinition music)
        {
            if (hasShownMusic && music == shownMusic)
            {
                return;
            }

            hasShownMusic = true;
            shownMusic = music;
            musicText.SetText(music != null ? $"Music: {music.name}" : "Music: -");
        }

        public void ShowPaused(bool isPaused)
        {
            if (hasShownPaused && isPaused == isShowingPaused)
            {
                return;
            }

            hasShownPaused = true;
            isShowingPaused = isPaused;
            pausedText.SetText(isPaused ? "Paused: yes" : "Paused: no");
        }

        public void ShowHumPlaying(bool isPlaying)
        {
            if (hasShownHum && isPlaying == isShowingHumPlaying)
            {
                return;
            }

            hasShownHum = true;
            isShowingHumPlaying = isPlaying;
            humButtonText.SetText(isPlaying ? "Stop Hum (1s fade)" : "Start Hum (1s fade in)");
        }

        /// <summary>Instantiates one slider row from the row prefab.</summary>
        public void AddVolumeRow(string channel, float volume)
        {
            VolumeSliderView row = Instantiate(volumeSliderPrefab, volumeSliders);
            row.name = $"{channel}VolumeSlider";
            row.Bind(channel, volume);
            row.VolumeChanged += HandleRowVolumeChanged;
            volumeRows.Add(row);
        }

        private void Awake()
        {
            if (!HasAllReferences())
            {
                Debug.LogError($"[MLin Audio Demo] '{name}' has unassigned references. Open the DemoUi prefab and assign every field of Demo Panel View.", this);
                enabled = false;
                return;
            }

            Bind(clickButton, DemoAction.PlayClick);
            Bind(clickBurstButton, DemoAction.PlayClickBurst);
            Bind(confirmButton, DemoAction.PlayConfirm);
            Bind(jingleButton, DemoAction.PlayJingle);
            Bind(blip3DButton, DemoAction.PlayBlip3D);
            Bind(humButton, DemoAction.ToggleHum);
            Bind(musicAButton, DemoAction.PlayMusicA);
            Bind(musicBButton, DemoAction.PlayMusicB);
            Bind(stopMusicButton, DemoAction.StopMusic);
            Bind(pauseButton, DemoAction.Pause);
            Bind(resumeButton, DemoAction.Resume);
            Bind(togglePauseButton, DemoAction.TogglePause);
        }

        private void OnDestroy()
        {
            foreach (KeyValuePair<Button, UnityAction> binding in buttonBindings)
            {
                if (binding.Key != null)
                {
                    binding.Key.onClick.RemoveListener(binding.Value);
                }
            }

            foreach (VolumeSliderView row in volumeRows)
            {
                if (row != null)
                {
                    row.VolumeChanged -= HandleRowVolumeChanged;
                }
            }

            buttonBindings.Clear();
            volumeRows.Clear();
        }

        private void Bind(Button button, DemoAction action)
        {
            UnityAction listener = () => ActionRequested?.Invoke(action);
            button.onClick.AddListener(listener);
            buttonBindings.Add(new KeyValuePair<Button, UnityAction>(button, listener));
        }

        private void HandleRowVolumeChanged(string channel, float volume)
        {
            VolumeChanged?.Invoke(channel, volume);
        }

        private bool HasAllReferences()
        {
            return voicesText != null && musicText != null && pausedText != null
                && clickButton != null && clickBurstButton != null && confirmButton != null && jingleButton != null
                && blip3DButton != null && humButton != null && humButtonText != null
                && musicAButton != null && musicBButton != null && stopMusicButton != null
                && pauseButton != null && resumeButton != null && togglePauseButton != null
                && volumeSliders != null && volumeSliderPrefab != null;
        }
    }
}
