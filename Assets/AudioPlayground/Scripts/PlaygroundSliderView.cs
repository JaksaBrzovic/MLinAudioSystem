using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MLin.Audio.Playground
{
    /// <summary>A labelled slider row: forwards changes with its key and shows the bound value.</summary>
    public class PlaygroundSliderView : MonoBehaviour
    {
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private Slider slider;
        [SerializeField] private TMP_Text valueText;
        [Tooltip("Format for the value text, e.g. {0:2} or {0:1} s.")]
        [SerializeField] private string valueFormat = "{0:2}";

        private string key;

        public event Action<string, float> ValueChanged;

        /// <summary>Shows a value without raising <see cref="ValueChanged"/>.</summary>
        public void Bind(string boundKey, string label, float value)
        {
            key = boundKey;
            nameText.SetText(label);
            slider.SetValueWithoutNotify(value);
            ShowValue(value);
        }

        private void Awake()
        {
            if (nameText == null || slider == null || valueText == null)
            {
                Debug.LogError($"[Audio Playground] '{name}' is missing a Name Text, Slider or Value Text reference.", this);
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

        private void HandleSliderValueChanged(float value)
        {
            ShowValue(value);
            ValueChanged?.Invoke(key, value);
        }

        private void ShowValue(float value)
        {
            valueText.SetText(valueFormat, value);
        }
    }
}
