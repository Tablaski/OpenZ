using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace OpenZ.Sim
{
    /// <summary>
    /// Replication schema: individual public presentation fields are independent delta records.
    /// Game logic, RNGs, path caches and settings never travel from the client to the host.
    /// Call from the simulation/main thread, not the socket read thread.
    /// </summary>
    public static class DuelReplica
    {
        const int Stride = 1024;
        sealed class Slot
        {
            public string Name; public Type Type; public FieldInfo Field; public PropertyInfo Property;
            public Action<BinaryWriter, ZObject> Writer;
            public object Get(ZObject o) => Field != null ? Field.GetValue(o) : Property.GetValue(o);
            public void Set(ZObject o, object value)
            {
                if (Field != null && typeof(IList).IsAssignableFrom(Type) && !Type.IsArray)
                {
                    var target = (IList)Field.GetValue(o);
                    if (value == null || target == null) { Field.SetValue(o, value); return; }
                    target.Clear();
                    foreach (var item in (IList)value) target.Add(item);
                }
                else if (Field != null) Field.SetValue(o, value);
                else Property.SetValue(o, value);
            }
            public void ReadInto(BinaryReader reader, ZObject o, ZWorld world)
            {
                // Preserve list identity and avoid allocating and copying an intermediate list.
                if (Field != null && Type.IsGenericType && Type.GetGenericTypeDefinition() == typeof(List<>) && Field.GetValue(o) is IList list)
                {
                    int count = reader.ReadInt32();
                    if (count < -1 || count > 16384) throw new InvalidDataException("Invalid list size.");
                    if (count == -1) { Set(o, null); return; }
                    list.Clear(); var element = Type.GetGenericArguments()[0];
                    for (int i = 0; i < count; i++) list.Add(Read(reader, element, world));
                }
                else Set(o, Read(reader, Type, world));
            }
        }
        static readonly Slot[] Slots = Schema();
        static readonly HashSet<string> MapItemFields = new HashSet<string>(StringComparer.Ordinal)
        {
            "F:Id", "F:Owner", "F:Kind", "F:Type", "F:LocX", "F:LocY", "F:WidthPix", "F:HeightPix",
            "F:CenterX", "F:CenterY", "F:Health", "F:MaxHealth", "F:KillAt", "F:HasProcessedDeath",
            "F:BlockTiles", "F:ZoneIndex", "F:LinkedObjects", "F:CanBeDestroyed", "F:IsDestroyableImpass", "F:Source"
        };
        static readonly int[] AllSlots = Enumerable.Range(0, Slots.Length).ToArray();
        static readonly int[] MapSlots = AllSlots.Where(i => MapItemFields.Contains(Slots[i].Name)).ToArray();
        public static string SchemaHash
        {
            get
            {
                using (var hash = SHA256.Create())
                    return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(string.Join("\n",
                        Slots.Select(s => s.Name + ":" + s.Type.FullName)) + "\nworld-v2-auxiliary-motion"))).Replace("-", "");
            }
        }
        static Slot[] Schema()
        {
            var fields = typeof(ZObject).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(f => (f.IsPublic || f.Name == "stunEnd") && !f.Name.Contains("<") && Supported(f.FieldType)).Select(f => new Slot { Name = "F:" + f.Name, Type = f.FieldType, Field = f });
            var properties = typeof(ZObject).GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(p => p.GetSetMethod(true) != null && Supported(p.PropertyType))
                .Select(p => new Slot { Name = "P:" + p.Name, Type = p.PropertyType, Property = p });
            var result = fields.Concat(properties).OrderBy(s => s.Name, StringComparer.Ordinal).ToArray();
            if (result.Length >= Stride - 1) throw new InvalidOperationException("Entity schema outgrew record stride.");
            foreach (var slot in result)
            {
                // Mono releases can compile direct typed accessors once at load time. AOT retains the wire-identical fallback.
                slot.Writer = (writer, obj) => Write(writer, slot.Type, slot.Get(obj));
#if !ENABLE_IL2CPP
                var valueType = slot.Type.IsEnum ? typeof(int) : slot.Type;
                var method = typeof(BinaryWriter).GetMethod("Write", new[] { valueType });
                if (method == null || slot.Type == typeof(string)) continue;
                var w = Expression.Parameter(typeof(BinaryWriter), "writer");
                var o = Expression.Parameter(typeof(ZObject), "obj");
                Expression value = slot.Field != null ? (Expression)Expression.Field(o, slot.Field) : Expression.Property(o, slot.Property);
                if (slot.Type.IsEnum) value = Expression.Convert(value, typeof(int));
                slot.Writer = Expression.Lambda<Action<BinaryWriter, ZObject>>(Expression.Call(w, method, value), w, o).Compile();
#endif
            }
            return result;
        }
        static bool Supported(Type t) => t.IsEnum || t == typeof(bool) || t == typeof(int) || t == typeof(long) ||
            t == typeof(float) || t == typeof(double) || t == typeof(string) || t == typeof(ZObject) ||
            t == typeof(ZWaypoint) || t == typeof(ZMapData.Obj) || t == typeof(int[]) || (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>) && Supported(t.GetGenericArguments()[0]));

        public sealed class CaptureBuffer
        {
            Dictionary<int, byte[]> previous = new Dictionary<int, byte[]>(), next = new Dictionary<int, byte[]>();
            readonly MemoryStream stream = new MemoryStream(512);
            readonly BinaryWriter writer;
            public CaptureBuffer() { writer = new BinaryWriter(stream); }
            void Begin() { stream.Position = 0; stream.SetLength(0); }
            void End(int key)
            {
                int length = checked((int)stream.Length);
                var buffer = stream.GetBuffer();
                if (previous.TryGetValue(key, out var old) && old.Length == length)
                {
                    int i = 0; while (i < length && old[i] == buffer[i]) i++;
                    if (i == length) { next.Add(key, old); return; }
                }
                next.Add(key, stream.ToArray());
            }
            /// <summary>Borrowed dictionary, valid until the next capture. Published arrays must remain immutable.</summary>
            public Dictionary<int, byte[]> Capture(ZWorld world)
            {
                if (world.DuelIsReplica) throw new InvalidOperationException("A replica is not an authority.");
                next.Clear(); Begin();
                writer.Write(world.Time); writer.Write(world.GameOver); writer.Write((int)world.Winner); writer.Write(world.TickIndex);
                writer.Write(world.Zones.Count); foreach (var zone in world.Zones) writer.Write((int)zone.Owner);
                End(0); Begin(); world.DuelWriteAuxiliary(writer); End(1);
                foreach (var o in world.DuelPresentationObjects())
                {
                    int key = checked(o.Id * Stride);
                    Begin(); writer.Write(o.Type); writer.Write((int)o.Kind); writer.Write(world.Get(o.Id) != null); End(key);
                    foreach (int i in o.Kind == ZKind.MapItem ? MapSlots : AllSlots)
                    {
                        var slot = Slots[i];
                        Begin(); slot.Writer(writer, o); End(key + i + 1);
                    }
                    if (o.IsUnit) { Begin(); o.DuelWriteMotion(writer); End(key + Stride - 1); }
                }
                var swap = previous; previous = next; next = swap; return previous;
            }
        }
        public static Dictionary<int, byte[]> Capture(ZWorld world) => new CaptureBuffer().Capture(world);

        public static void ApplyDelta(ZWorld world, OpenZ.Multiplayer.DuelDelta delta)
        {
            Apply(world, delta.Changed, !delta.Full);
            foreach (int key in delta.Removed) if (key > 0 && key % Stride == 0) world.DuelForgetGhost(key / Stride);
        }
        public static void Apply(ZWorld world, IDictionary<int, byte[]> records, bool partial = false)
        {
            if (!world.DuelIsReplica) throw new InvalidOperationException("Cannot overwrite the authority.");
            if (!partial && !records.ContainsKey(0)) throw new InvalidDataException("Missing world record.");
            var ids = new HashSet<int>(records.Keys.Where(k => k >= Stride && (partial || k % Stride == 0)).Select(k => k / Stride));
            var live = new HashSet<int>();
            var described = new HashSet<int>();
            // Allocate all objects first, so cross-object references resolve regardless of record order.
            foreach (int id in ids)
            {
                if (!records.TryGetValue(id * Stride, out var descriptor))
                {
                    if (world.GetAny(id) == null) throw new InvalidDataException("Unknown entity in delta.");
                    continue;
                }
                described.Add(id);
                using (var r = Reader(descriptor))
                {
                    string type = r.ReadString(); var kind = (ZKind)r.ReadInt32();
                    if (r.ReadBoolean()) live.Add(id);
                    if (type.Length > 64 || !Enum.IsDefined(typeof(ZKind), kind)) throw new InvalidDataException("Invalid entity descriptor.");
                    world.DuelReplicaObject(id, type, kind);
                }
            }
            using (var input = new RecordInput())
            using (var r = new BinaryReader(input))
            {
                if (partial)
                {
                    // A moving object often changes only a handful of fields. Visit those records directly.
                    foreach (var record in records)
                    {
                        if (record.Key < Stride) continue;
                        int field = record.Key % Stride;
                        if (field == 0 || field == Stride - 1) continue;
                        if (field > Slots.Length) throw new InvalidDataException("Unknown entity field.");
                        var obj = world.GetAny(record.Key / Stride);
                        input.Bind(record.Value); Slots[field - 1].ReadInto(r, obj, world);
                        if (input.Position != input.Length) throw new InvalidDataException("Trailing field data.");
                    }
                }
                foreach (int id in ids)
                {
                    var o = world.GetAny(id);
                    if (!partial)
                    foreach (int i in o.Kind == ZKind.MapItem ? MapSlots : AllSlots)
                    {
                        if (!records.TryGetValue(id * Stride + i + 1, out var data))
                            throw new InvalidDataException("Incomplete entity state.");
                        input.Bind(data); Slots[i].ReadInto(r, o, world);
                        if (input.Position != input.Length) throw new InvalidDataException("Trailing field data.");
                    }
                    if (o.IsUnit && records.TryGetValue(id * Stride + Stride - 1, out var motionData))
                        using (var motion = Reader(motionData)) o.DuelReadMotion(motion);
                }
            }
            if (partial) foreach (int id in described) world.DuelSetReplicaLive(id, live.Contains(id));
            else
            {
                world.DuelRemoveReplicaObjects(live);
                foreach (var zone in world.Zones) zone.Flag = world.Flags.FirstOrDefault(f => f.ZoneIndex == zone.Id);
            }
            if (records.TryGetValue(1, out var auxiliary))
                using (var r = Reader(auxiliary)) world.DuelReadAuxiliary(r);
            if (!records.TryGetValue(0, out var clock)) return;
            using (var r = Reader(clock))
            {
                double time = r.ReadDouble(); bool ended = r.ReadBoolean(); var winner = (Team)r.ReadInt32(); long tick = r.ReadInt64();
                if (double.IsNaN(time) || double.IsInfinity(time) || time < 0 || !Enum.IsDefined(typeof(Team), winner))
                    throw new InvalidDataException("Invalid world clock.");
                int count = r.ReadInt32();
                if (count != world.Zones.Count) throw new InvalidDataException("Different maps.");
                foreach (var z in world.Zones) z.Owner = (Team)r.ReadInt32();
                world.DuelReplicaClock(time, ended, winner, tick);
            }
        }

        // Rebind one reader per delta instead of allocating streams for each changed field.
        sealed class RecordInput : Stream
        {
            byte[] bytes = Array.Empty<byte>(); int position;
            public void Bind(byte[] value) { bytes = value; position = 0; }
            public override bool CanRead => true;
            public override bool CanSeek => true;
            public override bool CanWrite => false;
            public override long Length => bytes.Length;
            public override long Position { get => position; set { if (value < 0 || value > bytes.Length) throw new IOException("Invalid record position."); position = (int)value; } }
            public override int Read(byte[] buffer, int offset, int count)
            { int n = Math.Min(count, bytes.Length - position); Array.Copy(bytes, position, buffer, offset, n); position += n; return n; }
            public override int Read(Span<byte> buffer)
            { int n = Math.Min(buffer.Length, bytes.Length - position); new ReadOnlySpan<byte>(bytes, position, n).CopyTo(buffer); position += n; return n; }
            public override int ReadByte() => position < bytes.Length ? bytes[position++] : -1;
            public override long Seek(long offset, SeekOrigin origin)
            { Position = offset + (origin == SeekOrigin.Begin ? 0 : origin == SeekOrigin.Current ? position : bytes.Length); return position; }
            public override void Flush() { }
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }
        static BinaryReader Reader(byte[] bytes) => new BinaryReader(new MemoryStream(bytes, false));
        static void Write(BinaryWriter w, Type t, object value)
        {
            if (t.IsEnum) w.Write(Convert.ToInt32(value));
            else if (t == typeof(bool)) w.Write((bool)value);
            else if (t == typeof(int)) w.Write((int)value);
            else if (t == typeof(long)) w.Write((long)value);
            else if (t == typeof(float)) w.Write((float)value);
            else if (t == typeof(double)) w.Write((double)value);
            else if (t == typeof(string)) { w.Write(value != null); if (value != null) w.Write((string)value); }
            else if (t == typeof(ZObject)) w.Write(((ZObject)value)?.Id ?? -1);
            else if (t == typeof(ZMapData.Obj))
            {
                w.Write(value != null);
                if (value != null) foreach (var f in MapObjectFields) Write(w, f.FieldType, f.GetValue(value));
            }
            else if (t == typeof(ZWaypoint))
                foreach (var f in WaypointFields) Write(w, f.FieldType, f.GetValue(value));
            else
            {
                var list = value as IList; w.Write(list?.Count ?? -1);
                if (list != null) foreach (var item in list) Write(w, t.IsArray ? t.GetElementType() : t.GetGenericArguments()[0], item);
            }
        }
        static readonly FieldInfo[] WaypointFields = typeof(ZWaypoint).GetFields().Where(f => !f.IsStatic).OrderBy(f => f.Name, StringComparer.Ordinal).ToArray();
        static readonly FieldInfo[] MapObjectFields = typeof(ZMapData.Obj).GetFields().Where(f => !f.IsStatic).OrderBy(f => f.Name, StringComparer.Ordinal).ToArray();
        static object Read(BinaryReader r, Type t, ZWorld world)
        {
            if (t.IsEnum) return Enum.ToObject(t, r.ReadInt32());
            if (t == typeof(bool)) return r.ReadBoolean();
            if (t == typeof(int)) return r.ReadInt32();
            if (t == typeof(long)) return r.ReadInt64();
            if (t == typeof(float)) return r.ReadSingle();
            if (t == typeof(double)) return r.ReadDouble();
            if (t == typeof(string)) return r.ReadBoolean() ? r.ReadString() : null;
            if (t == typeof(ZObject)) return world.GetAny(r.ReadInt32());
            if (t == typeof(ZMapData.Obj))
            {
                if (!r.ReadBoolean()) return null;
                var source = new ZMapData.Obj();
                foreach (var f in MapObjectFields) f.SetValue(source, Read(r, f.FieldType, world));
                return source;
            }
            if (t == typeof(ZWaypoint))
            {
                object wp = new ZWaypoint(); foreach (var f in WaypointFields) f.SetValue(wp, Read(r, f.FieldType, world)); return wp;
            }
            int count = r.ReadInt32();
            if (count < -1 || count > 16384) throw new InvalidDataException("Invalid list size.");
            if (count == -1) return null;
            Type element = t.IsArray ? t.GetElementType() : t.GetGenericArguments()[0];
            IList list = t.IsArray ? (IList)Array.CreateInstance(element, count) : (IList)Activator.CreateInstance(t);
            for (int i = 0; i < count; i++) { var item = Read(r, element, world); if (t.IsArray) list[i] = item; else list.Add(item); }
            return list;
        }
    }

    public sealed partial class ZWorld
    {
        internal ZObject DuelReplicaObject(int id, string type, ZKind kind)
        {
            var o = GetAny(id);
            if (o != null)
            {
                if (o.Type != type || o.Kind != kind) throw new InvalidDataException("Reused entity identifier.");
                return o;
            }
            o = new ZObject { Id = id, Type = type, Kind = kind, World = this, U = S.Unit(type) };
            Objects.Add(o); byId.Add(id, o);
            if (type == "flag") Flags.Add(o);
            return o;
        }
        internal void DuelRemoveReplicaObjects(HashSet<int> live)
        {
            foreach (var o in Objects.ToArray())
                if (!live.Contains(o.Id))
                {
                    Objects.Remove(o); Flags.Remove(o); byId.Remove(o.Id);
                    graveyard[o.Id] = o; graveOrder.Enqueue(o.Id);
                }
            while (graveOrder.Count > 512) graveyard.Remove(graveOrder.Dequeue());
        }
        internal void DuelReplicaClock(double time, bool ended, Team winner, long tick)
        { Time = time; GameOver = ended; Winner = winner; TickIndex = tick; }
        internal IEnumerable<ZObject> DuelPresentationObjects() => Objects.Concat(graveyard.Values);
        internal void DuelForgetGhost(int id) { graveyard.Remove(id); }
        internal void DuelSetReplicaLive(int id, bool live)
        {
            var o = GetAny(id); if (o == null) return;
            if (live)
            {
                if (Get(id) != null) return;
                graveyard.Remove(id); byId[id] = o; Objects.Add(o); if (o.Type == "flag") Flags.Add(o);
            }
            else if (Get(id) != null)
            {
                Objects.Remove(o); Flags.Remove(o); byId.Remove(id); graveyard[id] = o; graveOrder.Enqueue(id);
                while (graveOrder.Count > 512) graveyard.Remove(graveOrder.Dequeue());
            }
        }
    }
    public partial class ZObject
    {
        internal void DuelWriteMotion(BinaryWriter writer)
        {
            writer.Write(cur.x); writer.Write(cur.y); writer.Write((int)cur.stage); writer.Write(cur.gotPfResponse);
            writer.Write(cur.pathFindingId); writer.Write(cur.pfPoints?.Count ?? 0);
            if (cur.pfPoints != null) foreach (var point in cur.pfPoints) { writer.Write(point.x); writer.Write(point.y); }
        }
        internal void DuelReadMotion(BinaryReader reader)
        {
            cur.x = reader.ReadInt32(); cur.y = reader.ReadInt32(); cur.stage = (Stage)reader.ReadInt32();
            cur.gotPfResponse = reader.ReadBoolean(); cur.pathFindingId = reader.ReadInt32(); int count = reader.ReadInt32();
            if (count < 0 || count > 8192) throw new InvalidDataException("Invalid presentation path.");
            if (cur.pfPoints == null) cur.pfPoints = new List<(int x, int y)>(); else cur.pfPoints.Clear();
            for (int i = 0; i < count; i++) cur.pfPoints.Add((reader.ReadInt32(), reader.ReadInt32()));
        }
    }
}
