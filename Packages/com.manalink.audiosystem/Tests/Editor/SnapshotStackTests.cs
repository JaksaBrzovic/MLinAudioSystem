using NUnit.Framework;
using UnityEngine.Audio;

namespace MLin.Audio.Editor.Tests
{
    public class SnapshotStackTests
    {
        [Test]
        public void Empty_HasNoActiveRequest()
        {
            SnapshotStack stack = new SnapshotStack();

            Assert.IsFalse(stack.TryGetActive(out _, out _));
        }

        [Test]
        public void HighestPriority_IsActive()
        {
            SnapshotStack stack = new SnapshotStack();
            int menu = stack.Push(null, 10);
            stack.Push(null, 0);

            Assert.IsTrue(stack.TryGetActive(out _, out int active));
            Assert.AreEqual(menu, active);
        }

        [Test]
        public void EqualPriority_MostRecentWins()
        {
            SnapshotStack stack = new SnapshotStack();
            stack.Push(null, 5);
            int latest = stack.Push(null, 5);

            stack.TryGetActive(out AudioMixerSnapshot _, out int active);

            Assert.AreEqual(latest, active);
        }

        [Test]
        public void ReleasingActiveRequest_FallsBackToNextOne()
        {
            SnapshotStack stack = new SnapshotStack();
            int underwater = stack.Push(null, 0);
            int menu = stack.Push(null, 10);

            Assert.IsTrue(stack.Remove(menu));
            stack.TryGetActive(out _, out int active);

            Assert.AreEqual(underwater, active);
        }

        [Test]
        public void ReleasingTwice_ReturnsFalse()
        {
            SnapshotStack stack = new SnapshotStack();
            int id = stack.Push(null, 0);

            Assert.IsTrue(stack.Remove(id));
            Assert.IsFalse(stack.Remove(id));
            Assert.AreEqual(0, stack.Count);
        }
    }
}
