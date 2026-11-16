using System;
using System.Collections.Generic;

namespace OpenZ.Multiplayer
{
    public sealed class DuelDelta
    {
        public long Sequence, Baseline;
        public bool Full;
        public readonly Dictionary<int, byte[]> Changed = new Dictionary<int, byte[]>();
        public readonly List<int> Removed = new List<int>();
    }

    /// <summary>
    /// Delta encoding over opaque entity records. Use one sender per connection, on an ordered reliable stream.
    /// The simulation adapter must encode records canonically; untouched entities produce no payload.
    /// </summary>
    public sealed class DuelDeltaSender
    {
        Dictionary<int, byte[]> previous = new Dictionary<int, byte[]>();
        Dictionary<int, byte[]> spare = new Dictionary<int, byte[]>();
        long sequence;
        public DuelDelta Capture(IDictionary<int, byte[]> state, bool full = false)
            => CaptureCore(state, full, false);
        /// <summary>Internal pipeline: published record arrays must remain immutable.</summary>
        internal DuelDelta CaptureImmutable(IDictionary<int, byte[]> state, bool full = false) => CaptureCore(state, full, true);
        DuelDelta CaptureCore(IDictionary<int, byte[]> state, bool full, bool immutable)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            var delta = new DuelDelta { Sequence = sequence + 1, Baseline = sequence, Full = full || sequence == 0 };
            var next = spare; next.Clear();
            foreach (var pair in state)
            {
                if (pair.Key < 0 || pair.Value == null) throw new ArgumentException("Invalid entity record.");
                bool unchanged = previous.TryGetValue(pair.Key, out var old) && (ReferenceEquals(old, pair.Value) || Equal(old, pair.Value));
                byte[] stable = unchanged ? old : (immutable ? pair.Value : (byte[])pair.Value.Clone()); next.Add(pair.Key, stable);
                if (delta.Full || !unchanged) delta.Changed.Add(pair.Key, immutable ? stable : (byte[])stable.Clone());
            }
            if (!delta.Full) foreach (var id in previous.Keys) if (!next.ContainsKey(id)) delta.Removed.Add(id);
            spare = previous; previous = next; sequence = delta.Sequence;
            return delta;
        }

        internal static Dictionary<int, byte[]> Clone(IDictionary<int, byte[]> state)
        {
            var copy = new Dictionary<int, byte[]>();
            foreach (var pair in state)
            {
                if (pair.Key < 0 || pair.Value == null) throw new ArgumentException("Invalid entity record.");
                copy.Add(pair.Key, (byte[])pair.Value.Clone());
            }
            return copy;
        }
        static bool Equal(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }
    }

    public sealed class DuelDeltaReceiver
    {
        Dictionary<int, byte[]> state = new Dictionary<int, byte[]>();
        public long Sequence { get; private set; }
        public Dictionary<int, byte[]> Snapshot() => DuelDeltaSender.Clone(state);
        public bool Apply(DuelDelta delta)
            => ApplyCore(delta, false);
        /// <summary>Transfers validated decoder output; do not mutate it after applying.</summary>
        internal bool ApplyOwned(DuelDelta delta) => ApplyCore(delta, true);
        bool ApplyCore(DuelDelta delta, bool owned)
        {
            if (delta == null || delta.Sequence <= Sequence ||
                (!delta.Full && (delta.Baseline != Sequence || delta.Sequence != Sequence + 1))) return false;
            var changes = owned ? delta.Changed : DuelDeltaSender.Clone(delta.Changed);
            foreach (int id in delta.Removed)
                if (id < 0 || changes.ContainsKey(id)) throw new ArgumentException("Invalid deletion.");
            var next = delta.Full ? new Dictionary<int, byte[]>() : state;
            foreach (int id in delta.Removed) next.Remove(id);
            foreach (var pair in changes) next[pair.Key] = pair.Value;
            state = next; Sequence = delta.Sequence; return true;
        }
    }
}
