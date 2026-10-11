using System;
using System.Collections.Generic;
using UnityEngine;

namespace MLin.Audio
{
    /// <summary>
    /// Owns user-facing channel volumes. Volumes are applied to exposed mixer parameters only, never to
    /// AudioSource.volume, so they can not collide with fades, ducking or pause.
    /// </summary>
    internal sealed class VolumeController
    {
        private readonly AudioSystemSettings settings;
        private readonly Dictionary<string, VolumeChannel> channels = new Dictionary<string, VolumeChannel>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, float> volumes = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> loggedWarnings = new HashSet<string>();
        private IVolumeStorage storage;

        public IVolumeStorage Storage => storage;

        public VolumeController(AudioSystemSettings settings, IVolumeStorage storage)
        {
            this.settings = settings;

            foreach (VolumeChannel channel in settings.VolumeChannels)
            {
                if (channel == null || string.IsNullOrEmpty(channel.name))
                    continue;

                if (channels.ContainsKey(channel.name))
                {
                    Debug.LogWarning($"[MLin Audio] Duplicate volume channel '{channel.name}' in {settings.name}, ignoring the second one.");
                    continue;
                }

                channels.Add(channel.name, channel);
            }

            SetStorage(storage);
        }

        public IEnumerable<string> ChannelNames => channels.Keys;

        public bool HasChannel(string channel) => channel != null && channels.ContainsKey(channel);

        /// <summary>Replaces the storage and reloads every channel from it.</summary>
        public void SetStorage(IVolumeStorage newStorage)
        {
            storage = newStorage ?? new NullVolumeStorage();

            foreach (VolumeChannel channel in channels.Values)
            {
                float volume = storage.TryLoad(channel.name, out float stored) ? stored : channel.defaultVolume;
                volumes[channel.name] = Mathf.Clamp01(volume);
            }
        }

        public float Get(string channel)
        {
            if (channel != null && volumes.TryGetValue(channel, out float volume))
                return volume;

            WarnOnce($"Unknown volume channel '{channel}'. Add it to the Volume Channels list in {settings.name}.");
            return 1f;
        }

        public void Set(string channel, float volume)
        {
            if (!HasChannel(channel))
            {
                WarnOnce($"Unknown volume channel '{channel}'. Add it to the Volume Channels list in {settings.name}.");
                return;
            }

            volume = Mathf.Clamp01(volume);
            volumes[channel] = volume;
            storage.Store(channels[channel].name, volume);
            Apply(channels[channel], volume);
        }

        public void ApplyAll()
        {
            foreach (VolumeChannel channel in channels.Values)
                Apply(channel, volumes[channel.name]);
        }

        public void Flush()
        {
            storage.Flush();
        }

        private void Apply(VolumeChannel channel, float volume)
        {
            if (settings.Mixer == null)
            {
                WarnOnce($"{settings.name} has no AudioMixer assigned, channel volumes are stored but have no audible effect.");
                return;
            }

            if (string.IsNullOrEmpty(channel.exposedParameter) || !settings.Mixer.SetFloat(channel.exposedParameter, AudioMath.LinearToDecibels(volume)))
                WarnOnce($"Volume channel '{channel.name}': exposed parameter '{channel.exposedParameter}' not found on mixer '{settings.Mixer.name}'.");
        }

        private void WarnOnce(string message)
        {
            if (loggedWarnings.Add(message))
                Debug.LogWarning("[MLin Audio] " + message);
        }
    }
}
