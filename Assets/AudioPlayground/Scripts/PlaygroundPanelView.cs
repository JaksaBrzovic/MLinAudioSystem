using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MLin.Audio.Playground
{
    /// <summary>
    /// The playground panel. Turns buttons, toggles and sliders into requests and shows the text it is given;
    /// it never calls the audio system itself.
    /// </summary>
    public class PlaygroundPanelView : MonoBehaviour
    {
        private const string CROSSFADE_KEY = "Crossfade";

        [Header("Status")]
        [SerializeField] private TMP_Text musicText;
        [SerializeField] private TMP_Text positionText;
        [SerializeField] private TMP_Text voicesText;
        [SerializeField] private TMP_Text duckingText;
        [SerializeField] private TMP_Text mixText;

        [Header("Controls")]
        [SerializeField] private ActionButton[] actionButtons;
        [SerializeField] private SwitchToggle[] switchToggles;
        [SerializeField] private PlaygroundSliderView crossfadeSlider;

        [Header("Volume")]
        [SerializeField] private RectTransform volumeSliders;
        [SerializeField] private PlaygroundSliderView volumeSliderPrefab;

        private readonly List<KeyValuePair<Button, UnityAction>> buttonBindings = new List<KeyValuePair<Button, UnityAction>>();
        private readonly List<KeyValuePair<Toggle, UnityAction<bool>>> toggleBindings = new List<KeyValuePair<Toggle, UnityAction<bool>>>();
        private readonly List<PlaygroundSliderView> volumeRows = new List<PlaygroundSliderView>();

        public event Action<PlaygroundAction> ActionRequested;
        public event Action<PlaygroundSwitch, bool> SwitchChanged;
        public event Action<float> CrossfadeChanged;
        public event Action<string, float> VolumeChanged;

        public void ShowMusic(string text)
        {
            musicText.SetText(text);
        }

        public void ShowPosition(string text)
        {
            positionText.SetText(text);
        }

        public void ShowVoices(int activeVoices, int pooledVoices)
        {
            voicesText.SetText("Voices: {0} active / {1} pooled", activeVoices, pooledVoices);
        }

        public void ShowDucking(string text)
        {
            duckingText.SetText(text);
        }

        public void ShowMix(string text)
        {
            mixText.SetText(text);
        }

        /// <summary>Sets a toggle without raising <see cref="SwitchChanged"/>.</summary>
        public void SetSwitch(PlaygroundSwitch target, bool isOn)
        {
            foreach (SwitchToggle binding in switchToggles)
            {
                if (binding.Target == target)
                {
                    binding.Toggle.SetIsOnWithoutNotify(isOn);
                }
            }
        }

        public void ShowCrossfade(float seconds)
        {
            crossfadeSlider.Bind(CROSSFADE_KEY, "Crossfade", seconds);
        }

        /// <summary>Instantiates one volume row from the row prefab.</summary>
        public void AddVolumeRow(string channel, float volume)
        {
            PlaygroundSliderView row = Instantiate(volumeSliderPrefab, volumeSliders);
            row.name = $"{channel}VolumeSlider";
            row.Bind(channel, channel, volume);
            row.ValueChanged += HandleVolumeRowChanged;
            volumeRows.Add(row);
        }

        private void Awake()
        {
            if (!HasAllReferences())
            {
                Debug.LogError($"[Audio Playground] '{name}' has unassigned references. Open the PlaygroundUi prefab and assign every field of Playground Panel View.", this);
                enabled = false;
                return;
            }

            foreach (ActionButton binding in actionButtons)
            {
                PlaygroundAction action = binding.Action;
                UnityAction listener = () => ActionRequested?.Invoke(action);
                binding.Button.onClick.AddListener(listener);
                buttonBindings.Add(new KeyValuePair<Button, UnityAction>(binding.Button, listener));
            }

            foreach (SwitchToggle binding in switchToggles)
            {
                PlaygroundSwitch target = binding.Target;
                UnityAction<bool> listener = isOn => SwitchChanged?.Invoke(target, isOn);
                binding.Toggle.onValueChanged.AddListener(listener);
                toggleBindings.Add(new KeyValuePair<Toggle, UnityAction<bool>>(binding.Toggle, listener));
            }

            crossfadeSlider.ValueChanged += HandleCrossfadeChanged;
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

            foreach (KeyValuePair<Toggle, UnityAction<bool>> binding in toggleBindings)
            {
                if (binding.Key != null)
                {
                    binding.Key.onValueChanged.RemoveListener(binding.Value);
                }
            }

            foreach (PlaygroundSliderView row in volumeRows)
            {
                if (row != null)
                {
                    row.ValueChanged -= HandleVolumeRowChanged;
                }
            }

            if (crossfadeSlider != null)
            {
                crossfadeSlider.ValueChanged -= HandleCrossfadeChanged;
            }

            buttonBindings.Clear();
            toggleBindings.Clear();
            volumeRows.Clear();
        }

        private void HandleCrossfadeChanged(string key, float seconds)
        {
            CrossfadeChanged?.Invoke(seconds);
        }

        private void HandleVolumeRowChanged(string channel, float volume)
        {
            VolumeChanged?.Invoke(channel, volume);
        }

        private bool HasAllReferences()
        {
            if (musicText == null || positionText == null || voicesText == null || duckingText == null || mixText == null
                || crossfadeSlider == null || volumeSliders == null || volumeSliderPrefab == null
                || actionButtons == null || switchToggles == null)
            {
                return false;
            }

            foreach (ActionButton binding in actionButtons)
            {
                if (binding.Button == null)
                {
                    return false;
                }
            }

            foreach (SwitchToggle binding in switchToggles)
            {
                if (binding.Toggle == null)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
