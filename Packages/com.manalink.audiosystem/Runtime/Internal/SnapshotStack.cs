using System.Collections.Generic;
using UnityEngine.Audio;

namespace MLin.Audio
{
    /// <summary>
    /// Open snapshot requests. The active one is the highest priority request; on equal priority the most recent wins.
    /// Requests are released by id, in any order.
    /// </summary>
    internal sealed class SnapshotStack
    {
        private readonly List<Request> requests = new List<Request>();
        private int nextId = 1;

        public int Count => requests.Count;

        public int Push(AudioMixerSnapshot snapshot, int priority)
        {
            int id = nextId;
            nextId = nextId == int.MaxValue ? 1 : nextId + 1;
            requests.Add(new Request(id, snapshot, priority));
            return id;
        }

        /// <returns>False if the id was not open (already released or never issued).</returns>
        public bool Remove(int id)
        {
            for (int i = 0; i < requests.Count; i++)
            {
                if (requests[i].Id == id)
                {
                    requests.RemoveAt(i);
                    return true;
                }
            }

            return false;
        }

        /// <returns>False when there are no open requests.</returns>
        public bool TryGetActive(out AudioMixerSnapshot snapshot, out int id)
        {
            int best = -1;
            for (int i = 0; i < requests.Count; i++)
            {
                // later entries were pushed later, so >= lets the most recent request win a tie
                if (best < 0 || requests[i].Priority >= requests[best].Priority)
                {
                    best = i;
                }
            }

            snapshot = best >= 0 ? requests[best].Snapshot : null;
            id = best >= 0 ? requests[best].Id : 0;
            return best >= 0;
        }

        private readonly struct Request
        {
            public Request(int id, AudioMixerSnapshot snapshot, int priority)
            {
                Id = id;
                Snapshot = snapshot;
                Priority = priority;
            }

            public int Id { get; }
            public AudioMixerSnapshot Snapshot { get; }
            public int Priority { get; }
        }
    }
}
