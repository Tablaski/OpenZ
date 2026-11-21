using System;
using System.IO;

namespace OpenZ.Multiplayer
{
    /// <summary>Bounded entity-delta payload codec; reject malformed input before modifying a replica.</summary>
    public static class DuelDeltaCodec
    {
        const int MaxEntities = 262144, MaxRecord = 65536;

        public static byte[] Encode(DuelDelta delta)
        {
            var payload = new byte[Measure(delta)];
            using var stream = new MemoryStream(payload, true);
            using var writer = new BinaryWriter(stream);
            Write(writer, delta);
            return payload;
        }

        internal static int Measure(DuelDelta delta)
        {
            if (delta == null) throw new ArgumentNullException(nameof(delta));
            if (delta.Sequence <= 0 || delta.Baseline < 0 || delta.Baseline >= delta.Sequence ||
                delta.Changed.Count > MaxEntities || delta.Removed.Count > MaxEntities || (delta.Full && delta.Removed.Count != 0))
                throw new InvalidDataException("Invalid delta header.");
            long size = 25L + 4L * delta.Removed.Count;
            foreach (var pair in delta.Changed)
            {
                if (pair.Key < 0 || pair.Value == null || pair.Value.Length > MaxRecord) throw new InvalidDataException("Invalid entity payload.");
                size += 8L + pair.Value.Length;
            }
            if (size > DuelWire.MaxPayload) throw new InvalidDataException("Delta too large.");
            if (delta.Removed.Count > 0)
            {
                var seen = new System.Collections.Generic.HashSet<int>();
                foreach (int id in delta.Removed)
                    if (id < 0 || delta.Changed.ContainsKey(id) || !seen.Add(id)) throw new InvalidDataException("Invalid removal.");
            }
            return (int)size;
        }

        // Write an already measured delta directly into its packet.
        internal static void Write(BinaryWriter writer, DuelDelta delta)
        {
            writer.Write(delta.Sequence); writer.Write(delta.Baseline); writer.Write(delta.Full);
            writer.Write(delta.Changed.Count);
            foreach (var pair in delta.Changed)
            {
                writer.Write(pair.Key); writer.Write(pair.Value.Length); writer.Write(pair.Value);
            }
            writer.Write(delta.Removed.Count);
            foreach (int id in delta.Removed) writer.Write(id);
        }

        public static DuelDelta Decode(byte[] payload)
        {
            if (payload == null) throw new InvalidDataException("Missing delta.");
            return Decode(payload, 0, payload.Length);
        }

        internal static DuelDelta Decode(byte[] payload, int offset, int count)
        {
            if (count > DuelWire.MaxPayload) throw new InvalidDataException("Delta too large.");
            using (var stream = new MemoryStream(payload, offset, count, false))
            using (var reader = new BinaryReader(stream))
            {
                var delta = new DuelDelta { Sequence = reader.ReadInt64(), Baseline = reader.ReadInt64() };
                byte full = reader.ReadByte();
                if (full > 1 || delta.Sequence <= 0 || delta.Baseline < 0 || delta.Baseline >= delta.Sequence)
                    throw new InvalidDataException("Invalid delta sequence.");
                delta.Full = full == 1;
                int changed = Count(reader);
                for (int i = 0; i < changed; i++)
                {
                    int id = reader.ReadInt32(), length = reader.ReadInt32();
                    if (id < 0 || delta.Changed.ContainsKey(id) || length < 0 || length > MaxRecord || length > stream.Length - stream.Position)
                        throw new InvalidDataException("Invalid entity record.");
                    delta.Changed.Add(id, reader.ReadBytes(length));
                }
                int removed = Count(reader);
                if (delta.Full && removed != 0) throw new InvalidDataException("Full state cannot contain removals.");
                if (removed > 0)
                {
                    var seen = new System.Collections.Generic.HashSet<int>();
                    for (int i = 0; i < removed; i++)
                    {
                        int id = reader.ReadInt32();
                        if (id < 0 || delta.Changed.ContainsKey(id) || !seen.Add(id)) throw new InvalidDataException("Invalid removal.");
                        delta.Removed.Add(id);
                    }
                }
                if (stream.Position != stream.Length) throw new InvalidDataException("Trailing delta bytes.");
                return delta;
            }
        }

        static int Count(BinaryReader reader)
        {
            int count = reader.ReadInt32();
            if (count < 0 || count > MaxEntities) throw new InvalidDataException("Invalid entity count.");
            return count;
        }
    }
}
