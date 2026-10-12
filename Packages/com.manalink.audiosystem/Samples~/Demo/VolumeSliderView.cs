using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MLin.Audio.Samples
{
    /// <summary>One volume channel row: forwards slider changes and shows the bound value.</summary>
    public class VolumeSliderView : MonoBehaviour
    {
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private Slider slider;
        [SerializeField] private TMP_Text valueText;

        private string channel;

        public event Action<string, float> VolumeChanged;

        /// <summary>Shows a channel without raising <see cref="VolumeChanged"/>.</summary>
        public void Bind(string channelName, float volume)
        {
            channel = channelName;
            nameText.SetText(channelName);
            slider.SetValueWithoutNotify(volume);
            ShowValue(volume);
        }

        private void Awake()
        {
            if (nameText == null || slider == null || valueText == null)
            {
                Debug.LogError($"[MLin Audio Demo] '{name}' is missing a Name Text, Slider or Value Text reference.", this);
                enabled = false;
                return;
            }

            slider.onValueChanged.AddListener(HandleSliderValueChanged);
        }

        private void OnDestroy()
        {
            if (slider != null)
            {
                slider.onValueChanged.RemoveListener(HandleSliderValueChanged);
            }
        }

        private void HandleSliderValueChanged(float volume)
        {
            ShowValue(volume);
            VolumeChanged?.Invoke(channel, volume);
        }

        private void ShowValue(float volume)
        {
            valueText.SetText("{0:2}", volume);
        }
    }
}
