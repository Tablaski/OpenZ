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
            if (delta == null) throw new ArgumentNullException(nameof(delta));
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(delta.Sequence); writer.Write(delta.Baseline); writer.Write(delta.Full);
                writer.Write(delta.Changed.Count);
                foreach (var pair in delta.Changed)
                {
                    if (pair.Value == null || pair.Value.Length > MaxRecord) throw new InvalidDataException("Invalid entity payload.");
                    writer.Write(pair.Key); writer.Write(pair.Value.Length); writer.Write(pair.Value);
                    if (stream.Length > DuelWire.MaxPayload) throw new InvalidDataException("Delta too large.");
                }
                writer.Write(delta.Removed.Count);
                foreach (int id in delta.Removed) writer.Write(id);
                byte[] payload = stream.ToArray();
                Decode(payload); // One validation contract for local and remote producers.
                return payload;
            }
        }

        public static DuelDelta Decode(byte[] payload)
        {
            if (payload == null || payload.Length > DuelWire.MaxPayload) throw new InvalidDataException("Delta too large.");
            using (var stream = new MemoryStream(payload, false))
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
                var seen = new System.Collections.Generic.HashSet<int>();
                for (int i = 0; i < removed; i++)
                {
                    int id = reader.ReadInt32();
                    if (id < 0 || delta.Changed.ContainsKey(id) || !seen.Add(id)) throw new InvalidDataException("Invalid removal.");
                    delta.Removed.Add(id);
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
