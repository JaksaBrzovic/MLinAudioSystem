using System;

namespace MLin.Audio
{
    /// <summary>
    /// One open request from <see cref="AudioSystem.RequestSnapshot"/>. Release it when the state that asked for the
    /// snapshot ends (menu closed, player left the water); releasing twice does nothing.
    /// </summary>
    public readonly struct SnapshotHandle : IEquatable<SnapshotHandle>
    {
        public static readonly SnapshotHandle Invalid = default;

        internal SnapshotHandle(int id)
        {
            Id = id;
        }

        /// <summary>False for <see cref="Invalid"/> (e.g. a request without a snapshot). Stays true after release.</summary>
        public bool IsValid => Id != 0;

        internal int Id { get; }

        public static bool operator ==(SnapshotHandle a, SnapshotHandle b)
        {
            return a.Equals(b);
        }

        public static bool operator !=(SnapshotHandle a, SnapshotHandle b)
        {
            return !a.Equals(b);
        }

        /// <param name="transition">Seconds; negative = the default from settings.</param>
        public void Release(float transition = -1f)
        {
            AudioSystem.ReleaseSnapshot(this, transition);
        }

        public bool Equals(SnapshotHandle other)
        {
            return Id == other.Id;
        }

        public override bool Equals(object obj)
        {
            return obj is SnapshotHandle other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Id;
        }

        public override string ToString()
        {
            return IsValid ? $"SnapshotHandle({Id})" : "SnapshotHandle(Invalid)";
        }
    }
}
