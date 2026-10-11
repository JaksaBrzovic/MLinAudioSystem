using UnityEngine;

namespace MLin.Audio
{
    /// <summary>
    /// Where channel volumes are persisted. The default uses PlayerPrefs; implement this to use
    /// your own save system (console save data, cloud saves, a settings file...).
    /// </summary>
    public interface IVolumeStorage
    {
        bool TryLoad(string channel, out float volume);
        void Store(string channel, float volume);
        /// <summary>Called on application pause/quit and from AudioSystem.SaveVolumes.</summary>
        void Flush();
    }

    public sealed class PlayerPrefsVolumeStorage : IVolumeStorage
    {
        private readonly string keyPrefix;

        public PlayerPrefsVolumeStorage(string keyPrefix)
        {
            this.keyPrefix = keyPrefix ?? string.Empty;
        }

        public bool TryLoad(string channel, out float volume)
        {
            string key = keyPrefix + channel;
            if (PlayerPrefs.HasKey(key))
            {
                volume = PlayerPrefs.GetFloat(key);
                return true;
            }

            volume = 1f;
            return false;
        }

        public void Store(string channel, float volume)
        {
            PlayerPrefs.SetFloat(keyPrefix + channel, volume);
        }

        public void Flush()
        {
            PlayerPrefs.Save();
        }
    }

    /// <summary>Volumes live only for the current session.</summary>
    public sealed class NullVolumeStorage : IVolumeStorage
    {
        public bool TryLoad(string channel, out float volume)
        {
            volume = 1f;
            return false;
        }

        public void Store(string channel, float volume) { }
        public void Flush() { }
    }
}
