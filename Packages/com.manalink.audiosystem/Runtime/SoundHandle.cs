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

        internal readonly int VoiceIndex;
        internal readonly int Id;

        internal SoundHandle(int voiceIndex, int id)
        {
            VoiceIndex = voiceIndex;
            Id = id;
        }

        /// <summary>False for <see cref="Invalid"/> (e.g. a play request that was rejected). Does not say whether the sound is still playing.</summary>
        public bool IsValid => Id != 0;

        /// <summary>True until the sound finishes or is stopped (including while paused or fading out).</summary>
        public bool IsPlaying => AudioSystem.IsPlaying(this);

        public void Stop(float fadeOut = 0f) => AudioSystem.Stop(this, fadeOut);

        /// <summary>Multiplier on top of the definition volume, 0..1.</summary>
        public void SetVolume(float volume) => AudioSystem.SetVolume(this, volume);

        public bool Equals(SoundHandle other) => VoiceIndex == other.VoiceIndex && Id == other.Id;
        public override bool Equals(object obj) => obj is SoundHandle other && Equals(other);
        public override int GetHashCode() => (VoiceIndex * 397) ^ Id;
        public static bool operator ==(SoundHandle a, SoundHandle b) => a.Equals(b);
        public static bool operator !=(SoundHandle a, SoundHandle b) => !a.Equals(b);
        public override string ToString() => IsValid ? $"SoundHandle(voice {VoiceIndex}, id {Id})" : "SoundHandle(Invalid)";
    }
}
