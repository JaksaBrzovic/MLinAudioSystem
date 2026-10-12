using System;

namespace MLin.Audio
{
    /// <summary>
    /// Lightweight reference to one playing sound. Safe to keep after the sound finished:
    /// operations on a stale handle simply do nothing, they never affect a different sound that reused the same voice.
    /// </summary>
    public readonly struct SoundHandle : IEquatable<SoundHandle>
    {
        public static readonly SoundHandle Invalid = default;

        internal SoundHandle(int voiceIndex, int id)
        {
            VoiceIndex = voiceIndex;
            Id = id;
        }

        /// <summary>False for <see cref="Invalid"/> (e.g. a play request that was rejected). Does not say whether the sound is still playing.</summary>
        public bool IsValid => Id != 0;

        /// <summary>True until the sound finishes or is stopped (including while paused or fading out).</summary>
        public bool IsPlaying => AudioSystem.IsPlaying(this);

        internal int VoiceIndex { get; }
        internal int Id { get; }

        public static bool operator ==(SoundHandle a, SoundHandle b)
        {
            return a.Equals(b);
        }

        public static bool operator !=(SoundHandle a, SoundHandle b)
        {
            return !a.Equals(b);
        }

        public void Stop(float fadeOut = 0f)
        {
            AudioSystem.Stop(this, fadeOut);
        }

        /// <summary>Multiplier on top of the definition volume, 0..1.</summary>
        /// <param name="fade">Seconds (unscaled) to move from the current level; 0 = immediately.</param>
        public void SetVolume(float volume, float fade = 0f)
        {
            AudioSystem.SetVolume(this, volume, fade);
        }

        public bool Equals(SoundHandle other)
        {
            return VoiceIndex == other.VoiceIndex && Id == other.Id;
        }

        public override bool Equals(object obj)
        {
            return obj is SoundHandle other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (VoiceIndex * 397) ^ Id;
        }

        public override string ToString()
        {
            return IsValid ? $"SoundHandle(voice {VoiceIndex}, id {Id})" : "SoundHandle(Invalid)";
        }
    }
}
