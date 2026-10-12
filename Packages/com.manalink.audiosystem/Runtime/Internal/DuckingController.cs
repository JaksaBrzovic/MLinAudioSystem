using System;
using UnityEngine;
using UnityEngine.Audio;

namespace MLin.Audio
{
    /// <summary>
    /// Runs the bus ducking rules. Each frame the host clears the triggers, marks the groups that have an audible
    /// sound, and calls <see cref="Update"/>; voices then read the gain for their own output group.
    /// </summary>
    internal sealed class DuckingController
    {
        private readonly DuckingRule[] rules;
        private readonly float[] gains;
        private readonly bool[] isTriggered;

        public DuckingController(DuckingRule[] rules)
        {
            this.rules = rules ?? Array.Empty<DuckingRule>();
            gains = new float[this.rules.Length];
            isTriggered = new bool[this.rules.Length];

            for (int i = 0; i < gains.Length; i++)
            {
                gains[i] = 1f;
            }
        }

        public bool HasRules => rules.Length > 0;

        public void ClearTriggers()
        {
            Array.Clear(isTriggered, 0, isTriggered.Length);
        }

        /// <summary>Marks every rule whose trigger is <paramref name="group"/>.</summary>
        public void Trigger(AudioMixerGroup group)
        {
            if (group == null)
            {
                return;
            }

            for (int i = 0; i < rules.Length; i++)
            {
                if (rules[i] != null && rules[i].IsValid && rules[i].Trigger == group)
                {
                    isTriggered[i] = true;
                }
            }
        }

        /// <summary>Marks one rule directly (tests and tools).</summary>
        public void TriggerRule(int ruleIndex)
        {
            isTriggered[ruleIndex] = true;
        }

        /// <summary>Moves every rule towards its target with the rule's attack or release time.</summary>
        public void Update(float deltaTime)
        {
            for (int i = 0; i < rules.Length; i++)
            {
                if (rules[i] == null)
                {
                    continue;
                }

                float duckedGain = AudioMath.DecibelsToLinear(rules[i].DuckDecibels);
                float target = isTriggered[i] ? duckedGain : 1f;
                float time = target < gains[i] ? rules[i].Attack : rules[i].Release;
                float range = 1f - duckedGain;

                // attack/release are the time for the full range, so partial movements take proportionally less
                gains[i] = time <= 0f || range <= 0f ? target : Mathf.MoveTowards(gains[i], target, range * deltaTime / time);
            }
        }

        /// <summary>Gain for sounds routed to <paramref name="group"/>; the strongest duck wins.</summary>
        public float GetGain(AudioMixerGroup group)
        {
            if (group == null)
            {
                return 1f;
            }

            float gain = 1f;
            for (int i = 0; i < rules.Length; i++)
            {
                if (rules[i] != null && rules[i].IsValid && rules[i].Target == group)
                {
                    gain = Mathf.Min(gain, gains[i]);
                }
            }

            return gain;
        }

        public float GetRuleGain(int ruleIndex)
        {
            return gains[ruleIndex];
        }
    }
}
