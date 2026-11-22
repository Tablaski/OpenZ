using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using OpenZ;
using OpenZ.Multiplayer;
using OpenZ.Sim;

static class CaptureCacheChecks
{
    public static void Run(Action<bool, string> check)
    {
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../source/Assets/OpenZ/Resources/OpenZ"));
        string settings = File.ReadAllText(Path.Combine(root, "default_settings.txt"));
        string map = File.ReadAllText(Path.Combine(root, "Maps/L02_grid.json"));
        var world = ZWorld.Create(settings, map); world.ConfigureDuel(new DuelRules { Map = "L02" }, false);
        var cached = new DuelReplica.CaptureBuffer();
        var ordinary = new DuelReplica.CaptureBuffer(false);
        var room = new DuelReplica.CaptureBuffer();
        var cachedSender = new DuelDeltaSender(); var ordinarySender = new DuelDeltaSender();
        byte[] retained = null, retainedCopy = null;
        int samples = 0;
        ZWorld comparedWorld = null;
        void Compare(string label)
        {
            var actual = cached.Capture(world); var expected = ordinary.Capture(world);
            check(actual.Count == expected.Count && actual.All(p => expected.TryGetValue(p.Key, out var bytes) && bytes.SequenceEqual(p.Value)), label + ": stale cached field");
            bool full = samples++ % 17 == 0 || world != comparedWorld;
            comparedWorld = world;
            var wire = DuelDeltaCodec.Encode(ordinarySender.CaptureImmutable(expected, full));
            check(DuelDeltaCodec.Encode(cachedSender.CaptureImmutable(actual, full)).SequenceEqual(wire), label + ": cache changed delta bytes");
            check(DuelDeltaCodec.Encode(room.CaptureDelta(world, full)).SequenceEqual(wire), label + ": room capture changed delta bytes");
            if (retained == null) { retained = actual[0]; retainedCopy = (byte[])retained.Clone(); }
            check(retained.SequenceEqual(retainedCopy), label + ": cache changed a retained packet");
        }
        Compare("initial");
        check(cached.SerializedFieldsLastCapture > 0 && cached.ReusedFieldsLastCapture == 0, "First capture must initialize all field records");
        Compare("unchanged");
        check(cached.SerializedFieldsLastCapture == 0 && cached.ReusedFieldsLastCapture > 0, "Unchanged world still serializes fields");

        var unit = world.Objects.First(o => o.IsUnit);
        // Mutate each wire field independently, including fields that look immutable today.
        // No simulation tick is run with synthetic values; restore them immediately.
        foreach (var field in typeof(ZObject).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(f => (f.IsPublic || f.Name == "stunEnd") && !f.Name.Contains("<") && Supported(f.FieldType)))
        {
            object old = field.GetValue(unit);
            if (old is IList list && !field.FieldType.IsArray)
            {
                var contents = list.Cast<object>().ToArray();
                list.Clear(); list.Add(Changed(field.FieldType.GetGenericArguments()[0], null));
                Compare(field.Name + " list insert");
                list[0] = Changed(field.FieldType.GetGenericArguments()[0], list[0]);
                Compare(field.Name + " same-count list edit");
                list.Clear(); Compare(field.Name + " list clear");
                foreach (var item in contents) list.Add(item);
            }
            else field.SetValue(unit, field.Name == "Id" ? 1000000 : Changed(field.FieldType, old));
            Compare(field.Name + " changed");
            if (!(old is IList) || field.FieldType.IsArray) field.SetValue(unit, old);
            Compare(field.Name + " restored");
        }
        foreach (var property in typeof(ZObject).GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(p => p.GetSetMethod(true) != null && Supported(p.PropertyType)))
        {
            object old = property.GetValue(unit); property.SetValue(unit, Changed(property.PropertyType, old));
            Compare(property.Name + " property"); property.SetValue(unit, old); Compare(property.Name + " restored");
        }

        var source = new ZMapData.Obj { kind = "vehicle:jeep", owner = "red", at = new[] { 1, 2 }, camp = new[] { 3, 4 } };
        unit.Source = source; Compare("new source");
        foreach (var field in typeof(ZMapData.Obj).GetFields().Where(f => !f.IsStatic))
        {
            object old = field.GetValue(source); field.SetValue(source, Changed(field.FieldType, old)); Compare("source " + field.Name);
            field.SetValue(source, old); Compare("source restored " + field.Name);
        }
        source.at[0]++; source.camp[1]++; Compare("source in-place nested arrays");
        unit.Source = null; Compare("null source"); unit.Source = source; Compare("source after null");

        unit.BlockTiles = new[] { 1, 2 }; Compare("new tile array"); unit.BlockTiles[0]++; Compare("in-place tile array");
        unit.BlockTiles = Array.Empty<int>(); Compare("empty tile array"); unit.BlockTiles = null; Compare("null tile array");
        unit.Waypoints.Clear(); unit.Waypoints.Add(ZWaypoint.Move(16, 32)); Compare("waypoint");
        foreach (var field in typeof(ZWaypoint).GetFields().Where(f => !f.IsStatic))
        {
            object waypoint = unit.Waypoints[0]; field.SetValue(waypoint, Changed(field.FieldType, field.GetValue(waypoint)));
            unit.Waypoints[0] = (ZWaypoint)waypoint; Compare("waypoint " + field.Name);
        }
        var reference = new ZObject { Id = 999 };
        unit.AttackObject = reference; unit.Minions.Add(reference); Compare("object references");
        reference.Id++; Compare("in-place referenced ID");
        unit.AttackObject = null; unit.Minions.Clear(); Compare("removed references");
        foreach (long bits in new[] { 0L, long.MinValue, 0x7ff8000000000001L, 0x7ff8000000000002L })
        { unit.XOver = BitConverter.Int64BitsToDouble(bits); Compare("double bits " + bits); }
        foreach (int bits in new[] { 0, int.MinValue, 0x7fc00001, 0x7fc00002 })
        { unit.Dx = BitConverter.Int32BitsToSingle(bits); Compare("float bits " + bits); }

        // Destruction, restoration and graveyard eviction must invalidate cached presence as well as health.
        unit.XOver = 0; unit.Dx = 0; unit.Waypoints.Clear(); unit.Source = null;
        var prop = world.Objects.First(o => o.Kind == ZKind.MapItem && o.MaxHealth > 0);
        prop.SetCords(prop.LocX + 16, prop.LocY); Compare("moved decor");
        prop.BlockTiles = new[] { 1, 2 }; Compare("decor tile mask");
        prop.BlockTiles[0]++; Compare("decor tile mask edited in place");
        prop.Source = new ZMapData.Obj { kind = "mapitem:rock", at = new[] { 1, 2 } }; Compare("decor source");
        prop.Source.at[0]++; Compare("decor source edited in place");
        prop.LinkedObjects.Add(unit); Compare("decor link");
        prop.LinkedObjects.Clear(); Compare("decor link removed");
        prop.Health = 0; prop.HasProcessedDeath = true; Compare("destroyed decor");
        prop.Health = prop.MaxHealth; prop.HasProcessedDeath = false; Compare("restored decor");
        var building = world.Objects.First(o => o.Kind == ZKind.Building);
        building.Health = 0; Compare("destroyed building"); building.Health = building.MaxHealth; Compare("repaired building");
        building.Owner = Team.Blue; Compare("captured building");
        int forgotten = prop.Id; world.DuelSetReplicaLive(forgotten, false); Compare("graveyard transition");
        world.DuelForgetGhost(forgotten); Compare("graveyard eviction");
        check(cached.CachedObjectCount == world.DuelPresentationObjects().Count(), "Cache retains an evicted object");
        var replacement = world.CreateObject("mapitem", "rock", 256, 256, Team.Neutral);
        Compare("created obstacle"); replacement.Health = 0; Compare("new obstacle destroyed");

        world = ZWorld.Create(settings, map); world.ConfigureDuel(new DuelRules { Map = "L02" }, false);
        Compare("new match reusing IDs");
        check(cached.ReusedFieldsLastCapture == 0, "Cache reused another match's field history");
        // Recovery after a failed capture must rebuild snapshots instead of reusing a partial scan.
        unit = world.Objects.First(o => o.IsUnit); string type = unit.Type; unit.Type = null;
        bool rejected = false; try { cached.Capture(world); } catch (ArgumentNullException) { rejected = true; }
        check(rejected, "Invalid entity type should reject capture"); unit.Type = type; Compare("recovery after rejected capture");
        ConvertedMaps(root, settings, check);
    }
    static void ConvertedMaps(string resourceRoot, string settings, Action<bool, string> check)
    {
        string maps = Path.GetFullPath(Path.Combine(resourceRoot, "../../../../../aimaps/Resources/OpenZ/Maps"));
        foreach (var entry in OzAiMapCatalog.All.Where(m => m.Duel))
        {
            string json = File.ReadAllText(Path.Combine(maps, entry.Id + "_grid.json"));
            var host = ZWorld.Create(settings, json, 1935); var rules = new DuelRules { Map = entry.Id, Common = DuelMissionDefaults.Create() };
            DuelMissionDefaults.ApplyOutsidePreset(rules, new[] { "light", "", "", "" });
            host.ConfigureDuel(rules, false); host.DuelApplyStartingArmies();
            var guest = ZWorld.Create(settings, json, 1935); guest.ConfigureDuel(rules, true);
            var cache = new DuelReplica.CaptureBuffer(); var sender = new DuelDeltaSender(); var reference = new DuelDeltaSender();
            var receiver = new DuelDeltaReceiver();
            var unit = host.Objects.First(o => o.IsMobile && o.Owner == Team.Red);
            for (int frame = 0; frame < 6; frame++)
            {
                if (frame == 1) { unit.LocX++; unit.CenterX++; unit.Waypoints.Add(ZWaypoint.Move(160, 160)); }
                if (frame == 2) unit.DamageHealth(unit.MaxHealth);
                if (frame == 3)
                {
                    var building = host.Objects.First(o => o.Kind == ZKind.Building && !o.IsFort);
                    building.DamageHealth(building.MaxHealth); host.AutoRepair(building);
                    host.AddMissileForTest(new ZMissile { x = 321, y = 456, attackerId = unit.Id, explodeTime = host.Time + 3, kick = -1 });
                }
                if (frame == 5) host.DuelSurrender(0);
                host.Tick(.01);
                var state = cache.Capture(host); var canonical = DuelReplica.Capture(host);
                check(state.Count == canonical.Count && state.All(p => canonical[p.Key].SequenceEqual(p.Value)), entry.Id + ": cache lost simulation/destruction state");
                var delta = sender.CaptureImmutable(state, frame == 4);
                var payload = DuelDeltaCodec.Encode(delta);
                check(payload.SequenceEqual(DuelDeltaCodec.Encode(reference.CaptureImmutable(canonical, frame == 4))), entry.Id + ": cache changed packets");
                var decoded = DuelDeltaCodec.Decode(payload); check(receiver.ApplyOwned(decoded), entry.Id + ": cache broke baseline");
                DuelReplica.ApplyDelta(guest, decoded);
                var received = receiver.Snapshot();
                check(received.Count == state.Count && received.All(p => state[p.Key].SequenceEqual(p.Value)), entry.Id + ": guest lost cached/debris state");
                host.Events.Clear();
            }
        }
    }
    static bool Supported(Type t) => t.IsEnum || t == typeof(bool) || t == typeof(int) || t == typeof(long) || t == typeof(float) ||
        t == typeof(double) || t == typeof(string) || t == typeof(ZObject) || t == typeof(ZMapData.Obj) || t == typeof(ZWaypoint) ||
        t == typeof(int[]) || (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(System.Collections.Generic.List<>) && Supported(t.GetGenericArguments()[0]));
    static object Changed(Type t, object old)
    {
        if (t.IsEnum) return Enum.ToObject(t, old == null ? 1 : Convert.ToInt32(old) ^ 1);
        if (t == typeof(bool)) return !(old is bool value && value);
        if (t == typeof(int)) return old == null ? 1 : unchecked((int)old + 1);
        if (t == typeof(long)) return old == null ? 1L : unchecked((long)old + 1);
        if (t == typeof(float)) return old == null ? 1f : (float)old + 1f;
        if (t == typeof(double)) return old == null ? 1d : (double)old + 1d;
        if (t == typeof(string)) return old == null ? "" : (string)old + " é雪";
        if (t == typeof(ZObject)) return new ZObject { Id = 999 };
        if (t == typeof(ZMapData.Obj)) return new ZMapData.Obj { kind = "vehicle:jeep", at = new[] { 2, 3 } };
        if (t == typeof(ZWaypoint)) return old == null ? ZWaypoint.Move(16, 32) : ZWaypoint.Move(64, 96);
        if (t == typeof(int[])) return old == null ? new[] { 1, 2 } : new[] { 3, 4, 5 };
        throw new Exception("Missing cache mutation coverage: " + t);
    }
}
