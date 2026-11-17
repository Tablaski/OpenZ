using System;
using System.Collections.Generic;
using System.IO;
using OpenZ.Sim;

namespace OpenZ.Multiplayer
{
    internal static class DuelEventCodec
    {
        internal const int MaxEvents = 4096;
        const int RecordBytes = 34;
        static readonly int EventTypeCount = Enum.GetValues(typeof(ZEventType)).Length;

        internal static void Write(BinaryWriter writer, IReadOnlyList<ZEvent> events)
        {
            if (events.Count > MaxEvents) throw new InvalidDataException("Too many state events.");
            writer.Write((ushort)events.Count);
            foreach (var e in events)
            {
                if ((uint)e.type >= EventTypeCount || (uint)e.type > byte.MaxValue) throw new InvalidDataException("Invalid state event type.");
                writer.Write((byte)e.type);
                writer.Write(e.objId); writer.Write(e.otherId);
                writer.Write(e.x); writer.Write(e.y); writer.Write(e.radius);
                writer.Write(e.value);
                writer.Write((byte)((e.flag ? 1 : 0) | (e.flag2 ? 2 : 0) | (e.driver ? 4 : 0)));
                writer.Write(e.end);
            }
        }

        internal static ZEvent[] Read(BinaryReader reader)
        {
            int count = reader.ReadUInt16();
            if (count > MaxEvents || reader.BaseStream.Length - reader.BaseStream.Position != (long)count * RecordBytes)
                throw new InvalidDataException("Invalid state event length.");
            if (count == 0) return Array.Empty<ZEvent>();
            var events = new ZEvent[count];
            for (int i = 0; i < count; i++)
            {
                var e = new ZEvent
                {
                    type = (ZEventType)reader.ReadByte(),
                    objId = reader.ReadInt32(), otherId = reader.ReadInt32(),
                    x = reader.ReadInt32(), y = reader.ReadInt32(), radius = reader.ReadInt32(),
                    value = reader.ReadSingle()
                };
                byte flags = reader.ReadByte();
                if ((int)e.type >= EventTypeCount || flags > 7) throw new InvalidDataException("Invalid state event.");
                e.flag = (flags & 1) != 0;
                e.flag2 = (flags & 2) != 0;
                e.driver = (flags & 4) != 0;
                e.end = reader.ReadDouble();
                events[i] = e;
            }
            return events;
        }
    }
}
