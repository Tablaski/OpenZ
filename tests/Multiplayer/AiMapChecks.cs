using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenZ;
using OpenZ.Experimental;
using OpenZ.Multiplayer;
using OpenZ.Sim;

static class AiMapChecks
{
    public static void Run(Action<bool,string> check,Action<Action,string> reject)
    {
        string root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../"));
        string settings=File.ReadAllText(Path.Combine(root,"source/Assets/OpenZ/Resources/OpenZ/default_settings.txt"));
        check(OzAiMapCatalog.All.Select(m=>m.Id).Distinct().Count()==OzAiMapCatalog.All.Length,"Unique converted map identities");
        check(OzAiMapCatalog.Find("SMBallermansMapV3") == null, "Ballerman's Map remains excluded");
        check(OzAiMapCatalog.All.Count(m=>m.Solo&&m.Number>0)==35,"35 solo campaign choices");
        foreach (int number in new[] { 2, 7, 16, 17 })
        {
            string id = $"SM{number:00}";
            check(OzAiMapCatalog.Select(true, "Z ORIGINAL").Single(m => m.Number == number).Id == id &&
                OzAiMapCatalog.SelectMultiplayer(2, "Z ORIGINAL", "all", 0).Single(m => m.Number == number).Id == id,
                $"Level {number} offers its original AI map in solo and 1 vs 1");
            check(OzAiMapCatalog.OfficialSkirmishMaps.Single(m => m.Number == number).Id == $"L{number:00}",
                $"Level {number} retains its official OpenZ selection");
        }
        check(OzAiMapCatalog.Select(true, "MULTIPLAYER").Length == 13
            && OzAiMapCatalog.Select(true, "MULTIPLAYER").All(map => map.Duel && map.Starts == 2),
            "Only the 13 two-start Zod Engine maps join the solo campaign selector");
        foreach (bool solo in new[] { true, false })
        {
            var bridge = OzAiMapCatalog.Select(solo, "Z ORIGINAL", "city").Single(m => m.Number == 18);
            check(bridge.Id == (solo ? "SM18" : "SM18F") && bridge.Balanced == !solo,
                "Bridge Game uses the original AI map in solo and the balanced AI map in 1 vs 1");
            var grid = File.ReadAllText(Path.Combine(root, "aimaps/Resources/OpenZ/Maps", bridge.Id + "_grid.json"));
            var world = ZWorld.Create(settings, grid, 1935);
            check(world.Map.aiMap && !world.L18Map && world.Objects.All(o => !ZBigTanks.Is(o.Type)),
                "AI Bridge Game keeps its converted starting units");
        }
        new DuelRules { Map = "L18" }.Validate();
        new DuelRules { Map = "SM18F" }.Validate();
        reject(() => new DuelRules { Map = "SM18" }.Validate(), "Unbalanced solo Bridge Game cannot bypass duel selection");
        check(OzAiMapCatalog.All.Where(m=>m.Solo&&m.Balanced).All(m=>m.SoloException&&(m.Number==21||m.Number==27)),"Fixed solo files limited to approved exceptions");
        check(OzAiMapCatalog.Select(true,"Z EXPANSION","arctic").Select(m=>m.Number).SequenceEqual(new[]{27,28,29}),"Arctic expansion filtering and numeric order");
        foreach(var group in OzAiMapCatalog.All.Where(m=>m.Number>0).GroupBy(m=>m.Number))
        {
            var duel=group.Single(m=>m.Duel);
            check(duel.Balanced==group.Any(m=>m.Balanced),"Preferred fixed variant for campaign "+group.Key);
            check(group.Count(m=>m.Solo)==1,"One solo variant for campaign "+group.Key);
        }
        reject(()=>new DuelRules{Map="SM99"}.Validate(),"Unknown converted map rejected");
        CheckVolcanicV2(root, settings, check, reject);
        CheckLandV2(root, settings, check, reject);
        CheckArctic(root, settings, check, reject);
        CheckCatalogV3(root, settings, check);
        CheckTerrainV4(root, check);
        CheckTerrainV5(root, check);
        foreach(var entry in OzAiMapCatalog.All)
        {
            var mapFile=Path.Combine(root,"aimaps/Resources/OpenZ/Maps",entry.Id+"_grid.json");
            string json=File.ReadAllText(mapFile); var map=ZMapData.FromJson(json); var original=File.ReadAllBytes(Path.Combine(root,"aimaps/Sources~/maps",entry.File));
            check(map.file==entry.File&&map.planet==entry.World&&map.width==BitConverter.ToUInt16(original,0)&&map.height==BitConverter.ToUInt16(original,2),entry.Id+": source identity and world");
            check(map.objects.Count==BitConverter.ToUInt16(original,56)&&map.tiles.Length==map.width*map.height,entry.Id+": source object and tile counts");
            var world=ZWorld.Create(settings,json,1935);
            check(world.Objects.Count(o=>o.IsFort&&o.Owner==Team.Red)==1&&world.Objects.Count(o=>o.IsFort&&o.Owner==Team.Blue)==1,entry.Id+": two-team fort ownership");
            CheckNeutralMechs(entry.Id, settings, json, world, check);
            if(!entry.Duel) { reject(()=>new DuelRules{Map=entry.Id}.Validate(),entry.Id+": solo-only original cannot bypass duel selection"); continue; }
            var rules=new DuelRules{Map=entry.Id,Common=DuelMissionDefaults.Create()}; rules.Validate();
            DuelMissionDefaults.ApplyOutsidePreset(rules, new[] { "light", "", "", "" });
            world.ConfigureDuel(rules,false); world.DuelApplyStartingArmies();
            check(world.Objects.Any(o=>o.IsMobile&&o.Owner==Team.Red)&&world.Objects.Any(o=>o.IsMobile&&o.Owner==Team.Blue),entry.Id+": lobby armies spawn for both teams");
            var receiver=new DuelOrderReceiver(world,1); var red=world.Objects.First(o=>o.IsMobile&&o.Owner==Team.Red); var blue=world.Objects.First(o=>o.IsMobile&&o.Owner==Team.Blue);
            check(!receiver.Execute(new DuelOrder{Sequence=1,ObjectId=red.Id,Kind=DuelOrderKind.Stop})&&receiver.Execute(new DuelOrder{Sequence=2,ObjectId=blue.Id,Kind=DuelOrderKind.Stop}),entry.Id+": authoritative orders preserve team control");
            var state=DuelReplica.Capture(world); var client=ZWorld.Create(settings,json,1935); client.ConfigureDuel(rules,true); DuelReplica.Apply(client,state);
            check(client.Objects.Count==world.Objects.Count,entry.Id+": initial replica object count");
        }
    }

    static void CheckTerrainV5(string root, Action<bool, string> check)
    {
        var examples = OzAiMapCatalog.SkirmishMaps.Where(map => OzAiTerrainV5.HasExample(map.Id)).ToArray();
        check(examples.Length == 5 && examples.Select(map => map.World).Distinct().Count() == 5,
            "V5 contains one example per biome");
        foreach (var example in examples)
        {
            var map = ZMapData.FromJson(File.ReadAllText(Path.Combine(root, "aimaps/Resources/OpenZ/Maps", example.Id + "_grid.json")));
            for (int y = 0; y <= map.height * 2; y++)
            for (int x = 0; x <= map.width * 2; x++)
                if (OzAiTerrainV5.RimWeight(OzAiTerrainV5.RimDistance(x, y, map.width, map.height)) != 0)
                    throw new InvalidOperationException(example.Id + ": V5 rim overlaps a playable terrain vertex");
            check(OzAiTerrainV5.RimWeight(1) == 0 && OzAiTerrainV5.RimWeight(7) == 1,
                example.Id + ": V5 rim blends only outside the map");
            if (map.planet == "arctic")
                check(map.tiles.Any(OzAiTerrainV5.ArcticIceArt) && !OzAiTerrainV5.ArcticIceArt(175) && !OzAiTerrainV5.ArcticIceArt(432),
                    "V5 Arctic uses native fractured ice artwork without replacing blue open water");
        }
    }

    static void CheckTerrainV4(string root, Action<bool, string> check)
    {
        var grids = Path.Combine(root, "aimaps/Resources/OpenZ/Maps");
        var arctic = OzAiMapCatalog.Playable.Concat(OzAiMapCatalog.MultiplayerMaps).Where(map => map.World == "arctic").ToArray();
        check(OzAiTerrainV4.Profile("arctic") != 0, "Arctic uses the V4 terrain renderer");
        foreach (string id in new[] { "SM02", "SM08", "SM16", "SM17", "SMP03M01" }.Concat(arctic.Select(map => map.Id)))
        {
            var map = ZMapData.FromJson(File.ReadAllText(Path.Combine(grids, id + "_grid.json")));
            var bridges = OzAiTerrainV4.Bridges(map);
            var source = map.objects.Where(o => o.Type == "bridge_vert" || o.Type == "bridge_horz").ToArray();
            check(bridges.Length == source.Length, id + ": V4 covers every native bridge, including ruins");
            if (map.planet == "arctic")
                check(map.classes.Contains((int)ZClass.Water) && !map.classes.Contains((int)ZClass.DeepWater) &&
                    map.objects.Where(obj => obj.Type.StartsWith("map_object", StringComparison.Ordinal))
                        .All(obj => map.ClassAt(obj.x, obj.y) != ZClass.Water), id + ": native open water and shore props retain their classes");
            for (int i = 0; i < bridges.Length; i++)
            {
                var record = source[i];
                bool vertical = record.Type == "bridge_vert";
                float length = (5 + (record.extra_links < 100 ? record.extra_links : 0)) * 2;
                float At(float across, float along) => bridges[i].Clearance(record.x * 2 + (vertical ? across : along),
                    record.y * 2 + (vertical ? along : across));
                check(At(-1, length / 2) == 1 && At(4, length / 2) == 1 && At(9, length / 2) == 1,
                    id + ": V4 clears both outer shoulders and the full deck width");
                check(At(-3, length / 2) == 0 && At(11, length / 2) == 0 && At(4, 0) == 0 && At(4, length) == 0,
                    id + ": V4 leaves adjoining land and both bridge approaches at their existing grade");
            }

            var ridges = OzAiTerrainV4.StraightRidges(map);
            check(ridges.Select((ridge, index) => !ridge || (map.classes[index] == (int)ZClass.Impassable &&
                (!map.HasLava || map.lava[index] == 0))).All(valid => valid), id + ": straight ridges never extend onto roads, water or lava");
            if (id == "SM16")
            {
                check(Enumerable.Range(0, map.width).Count(x => Enumerable.Range(0, map.height - 19)
                    .Any(y => Enumerable.Range(y, 20).All(row => ridges[row * map.width + x]))) >= 2,
                    "SM16: native terrain preserves at least two long straight wall runs");
                var terraces = new OzJungleTerracesV4(map);
                float HeightAt(int x, int y) => terraces.TryGetHeight(y * map.width + x, out float height) ? height : 0;
                foreach (int row in new[] { 36, 40, 47, 54, 68 })
                    check(HeightAt(51, row) > 0 && HeightAt(52, row) > 0 && HeightAt(44, row) < 0 && HeightAt(58, row) < 0,
                        $"SM16 row {row}: the central road is a raised shelf above both ravines");
                check(HeightAt(51, 24) == 0 && HeightAt(52, 99) == 0,
                    "SM16: terrace rules leave both outer approaches open and unraised");
            }
        }
        foreach (var entry in OzAiMapCatalog.Playable.Concat(OzAiMapCatalog.MultiplayerMaps).Where(map => map.World == "jungle"))
        {
            var map = ZMapData.FromJson(File.ReadAllText(Path.Combine(grids, entry.Id + "_grid.json")));
            var terraces = new OzJungleTerracesV4(map);
            bool roadClear = map.classes.Select((type, index) => type != (int)ZClass.Road ||
                !terraces.TryGetHeight(index, out float height) || height > 0).All(valid => valid);
            check(roadClear, entry.Id + ": jungle terrace detection never drops an existing road into a ravine");
            if (entry.Number == 31)
                check(terraces.HasTerraces, entry.Id + ": expansion layout with the same jungle palette also gets terraces");
        }
    }

    static void CheckNeutralMechs(string mapId, string settings, string json, ZWorld initial, Action<bool, string> check)
    {
        foreach (var placed in initial.Objects.Where(o => o.Type == "medium" && o.Owner == Team.Neutral))
        {
            bool boarded = false;
            foreach (var direction in new[] { (x: 1, y: 0), (x: -1, y: 0), (x: 0, y: 1), (x: 0, y: -1) })
            {
                int x = placed.CenterX + direction.x * 40, y = placed.CenterY + direction.y * 40;
                if (!initial.Path.TilePassable(x / 16, y / 16, true)) continue;
                var world = ZWorld.Create(settings, json, 1935);
                var mech = world.Get(placed.Id);
                // Isolate the boarding order from crossfire and nearby squads auto-boarding first.
                world.S.auto_grab_vehicle_distance = 0;
                foreach (var unit in world.Objects) unit.Damage = 0;
                var driver = world.CreateObject("robot", "grunt", x - 8, y - 8, Team.Red);
                driver.Damage = 0;
                world.SendWaypoints(Team.Red, driver, new[] { new ZWaypoint { mode = WpMode.Enter, refId = mech.Id, x = mech.CenterX, y = mech.CenterY } });
                for (int i = 0; i < 800 && mech.Owner == Team.Neutral && driver.Waypoints.Count > 0; i++) world.Tick(.01);
                boarded = mech.Owner == Team.Red && mech.Drivers.Count > 0 && driver.KillAt >= 0;
                if (boarded) break;
            }
            check(boarded, $"{mapId}: board neutral Medium Mech at ({placed.Source.x}, {placed.Source.y})");
        }
    }

    static void CheckVolcanicV2(string root, string settings, Action<bool, string> check, Action<Action, string> reject)
    {
        check(OzAiMapCatalog.Select(true, world: "volcanic").Single(m => m.Number == 8).Id == "SM08",
            "Molten Kombat has a single shared catalog entry");
        foreach (var entry in OzAiMapCatalog.VolcanicV2)
        {
            reject(() => new DuelRules { Map = entry.Id }.Validate(), entry.Id + ": the prototype is solo only");
            string json = File.ReadAllText(Path.Combine(root, "aimaps/Resources/OpenZ/Maps", entry.Id + "_grid.json"));
            var map = ZMapData.FromJson(json);
            var world = ZWorld.Create(settings, json, 1935);
            int lava = map.lava.Count(k => k > 0);
            check(map.HasLava && lava == (entry.Number == 7 ? 332 : 717)
                && map.lava.Select((kind, tile) => kind == 0 || map.classes[tile] == (int)ZClass.Impassable).All(ok => ok),
                entry.Id + ": lava drawn on impassable native tiles");
            var bridges = world.Objects.Where(obj => obj.IsBridge).ToArray();
            check(bridges.Length == (entry.Number == 7 ? 0 : 2), entry.Id + ": native bridge count");
            foreach (var bridge in bridges)
            {
                int x = bridge.CenterX / 16, y = bridge.CenterY / 16;
                bool intact = world.Path.TilePassable(x, y, false);
                bridge.SetHealth(0);
                world.UpdateObjectHealth(bridge);
                check(intact && !world.Path.TilePassable(x, y, false), entry.Id + ": destroying " + bridge.Type + " closes the crossing");
            }
        }
    }

    static void CheckLandV2(string root, string settings, Action<bool, string> check, Action<Action, string> reject)
    {
        foreach (var entry in OzAiMapCatalog.LandV2)
        {
            check(OzAiMapCatalog.Find(entry.Id) == null, entry.Id + ": the V2 reference is excluded from the campaign catalog");
            reject(() => new DuelRules { Map = entry.Id }.Validate(), entry.Id + ": the prototype is solo only");
            string json = File.ReadAllText(Path.Combine(root, "aimaps/Resources/OpenZ/Maps", entry.Id + "_grid.json"));
            byte[] native = File.ReadAllBytes(Path.Combine(root, "aimaps/Sources~/maps", entry.File));
            var map = ZMapData.FromJson(json);
            var world = ZWorld.Create(settings, json, 1935);
            check(map.planet == entry.World && map.objects.Count == BitConverter.ToUInt16(native, 56) &&
                map.zones.Count == BitConverter.ToUInt16(native, 60), entry.Id + ": native objects and territories");
            var bridges = world.Objects.Where(obj => obj.IsBridge).ToArray();
            int expectedBridges = entry.Number == 16 ? 1 : entry.Number == 17 ? 2 : 4;
            check(bridges.Length == expectedBridges, entry.Id + ": native bridge count");
            foreach (var bridge in bridges)
            {
                int x = bridge.CenterX / 16, y = bridge.CenterY / 16;
                bool intact = entry.Number != 17;
                check(bridge.Destroyed != intact && world.Path.TilePassable(x, y, false) == intact,
                    entry.Id + ": " + bridge.Type + " retains its native starting state");
                if (!intact) continue;
                bridge.SetHealth(0);
                world.UpdateObjectHealth(bridge);
                check(!world.Path.TilePassable(x, y, false), entry.Id + ": destroying " + bridge.Type + " closes the route");
            }
            var water = Enumerable.Range(0, map.width * map.height)
                .Where(i => map.classes[i] == (int)ZClass.Water && world.Path.TilePassable(i % map.width, i / map.width, true));
            check(water.Any(i => !world.Path.TilePassable(i % map.width, i / map.width, false)),
                entry.Id + ": robots can wade where vehicles cannot");
        }
    }

    static void CheckCatalogV3(string root, string settings, Action<bool,string> check)
    {
        var entries = OzAiMapCatalog.Playable.ToArray();
        check(entries.Length == 65 && entries.Select(e => e.File).Distinct().Count() == entries.Length,
            "65 unique AI map variants including the restored campaign maps");
        bool lavaRule = ZWorld.LavaDeviation, crocodileRule = ZWorld.CrocodileDeviation;
        ZWorld.LavaDeviation = true;
        ZWorld.CrocodileDeviation = true;
        int aquaticMaps = 0, crocodiles = 0, destructibleBridges = 0, destructibleRocks = 0;
        try
        {
            foreach (var entry in entries)
            {
                var grid = File.ReadAllText(Path.Combine(root, "aimaps/Resources/OpenZ/Maps", entry.Id + "_grid.json"));
                var world = ZWorld.Create(settings, grid, 1935);
                var map = world.Map;
                check(map.aiMap, entry.Id + ": uses AI map environment rules");
                var bridges = world.Objects.Where(o => o.IsBridge).ToArray();
                bool DeckAt(int x, int y) => bridges.Any(o => !o.Destroyed && x * 16 + 8 >= o.LocX
                    && x * 16 + 8 < o.LocX + o.WidthPix && y * 16 + 8 >= o.LocY && y * 16 + 8 < o.LocY + o.HeightPix);
                bool coastal = false, terrainOk = true;
                for (int index = 0; index < map.classes.Length; index++)
                {
                    int x = index % map.width, y = index / map.width;
                    var terrain = (ZClass)map.classes[index];
                    if (map.LavaAt(x,y))
                    {
                        terrainOk &= map.TileWalkSpeed(x * 16 + 8, y * 16 + 8) == ZWorld.LavaWalkSpeed;
                        ZWorld.LavaDeviation = false;
                        terrainOk &= map.TileWalkSpeed(x * 16 + 8, y * 16 + 8) == 0;
                        ZWorld.LavaDeviation = true;
                    }
                    if (DeckAt(x,y)) continue;
                    bool robot = world.Path.TilePassable(x,y,true), tank = world.Path.TilePassable(x,y,false);
                    if (terrain == ZClass.Water) { terrainOk &= !tank; coastal |= robot; }
                    if (terrain == ZClass.Impassable || terrain == ZClass.DeepWater) terrainOk &= !robot && !tank;
                }
                check(terrainOk, entry.Id + ": water, lava and native solid cells respect travel classes");
                if (coastal) aquaticMaps++;
                check(map.objects.Where(o => o.Type == "flag" || o.Category == "building").All(o =>
                    world.Objects.Any(active => active.Type == o.Type && active.Source == o
                        && active.LocX == o.x * 16 && active.LocY == o.y * 16)),
                    entry.Id + ": flags and buildings match native placements");
                foreach (var croc in world.Crocs)
                {
                    check(world.CrocFreeAt(croc.X,croc.Y) >= ZWorld.CrocTurnM
                        && map.ClassAt((int)croc.X / 16, (int)croc.Y / 16) == ZClass.Water,
                        entry.Id + ": crocodile stays in its swamp");
                    crocodiles++;
                }
                foreach (var bridge in bridges.Where(o => !o.Destroyed))
                {
                    int x = bridge.CenterX / 16, y = bridge.CenterY / 16;
                    check(world.Path.TilePassable(x,y,false), entry.Id + ": intact crossing carries vehicles");
                    bridge.SetHealth(Math.Max(1, bridge.MaxHealth / 4));
                    world.UpdateObjectHealth(bridge);
                    check(world.Path.TilePassable(x,y,false), entry.Id + ": damaged crossing still carries vehicles");
                    bridge.SetHealth(0);
                    world.UpdateObjectHealth(bridge);
                    check(!world.Path.TilePassable(x,y,false), entry.Id + ": destroyed crossing closes to vehicles");
                    bridge.SetHealth(bridge.MaxHealth);
                    world.UpdateObjectHealth(bridge);
                    check(world.Path.TilePassable(x,y,false), entry.Id + ": repaired crossing carries vehicles again");
                    destructibleBridges++;
                }
                var rock = world.Objects.Where(o => o.Type == "rock" && !o.Destroyed)
                    .GroupBy(o => (x: o.LocX / 16, y: o.LocY / 16 + 2)).Where(g => g.Count() == 1)
                    .FirstOrDefault(g => map.ClassAt(g.Key.x,g.Key.y) == ZClass.Ground
                        && world.Path.TilePassableGrid(g.Key.x,g.Key.y,true,true));
                if (rock != null)
                {
                    var unit = rock.Single();
                    int x = rock.Key.x, y = rock.Key.y;
                    check(!world.Path.TilePassable(x,y,true) && !world.Path.TilePassable(x,y,false),
                        entry.Id + ": intact rock blocks units");
                    unit.SetHealth(0);
                    world.UpdateObjectHealth(unit);
                    world.Tick(ZConst.TICK);
                    check(world.Path.TilePassable(x,y,true), entry.Id + ": the obstructed cell opens on rock destruction");
                    destructibleRocks++;
                }
            }
            check(aquaticMaps > 0 && crocodiles > 0 && destructibleBridges > 0 && destructibleRocks > 0,
                "Catalog regression exercises water traversal, live crocodiles and destructible obstacles");
            var official = ZMapData.FromJson(File.ReadAllText(Path.Combine(root, "source/Assets/OpenZ/Resources/OpenZ/Maps/L07_grid.json")));
            check(!official.aiMap && Enumerable.Range(0, official.classes.Length).Any(i =>
                official.LavaAt(i % official.width,i / official.width) &&
                official.TileWalkSpeed(i % official.width * 16 + 8,i / official.width * 16 + 8) > 0),
                "The official Volcano retains its optional lava walk");
        }
        finally
        {
            ZWorld.LavaDeviation = lavaRule;
            ZWorld.CrocodileDeviation = crocodileRule;
        }
    }

    static void CheckArctic(string root, string settings, Action<bool, string> check, Action<Action, string> reject)
    {
        var entry = OzAiMapCatalog.ChillyWillyV2;
        check(OzAiMapCatalog.Select(true, "OPENZ-VK", "arctic").Contains(entry),
            "Chilly Willy is selectable as an original solo Arctic map");
        reject(() => new DuelRules { Map = entry.Id }.Validate(), "Chilly Willy V2 is a solo prototype");
        var json = File.ReadAllText(Path.Combine(root, "aimaps/Resources/OpenZ/Maps", entry.Id + "_grid.json"));
        var native = File.ReadAllBytes(Path.Combine(root, "aimaps/Sources~/maps", entry.File));
        var world = ZWorld.Create(settings, json, 19);
        var map = world.Map;
        check(map.planet == "arctic" && map.objects.Count == BitConverter.ToUInt16(native, 56) &&
            map.zones.Count == 9 && map.zones.Count == BitConverter.ToUInt16(native, 60), "Arctic prototype native objects and territory count");
        var bridges = world.Objects.Where(o => o.IsBridge).ToArray();
        check(bridges.Length == 3 && bridges.Count(o => o.Destroyed) == 1, "Chilly Willy V2 starts with two intact bridges and one ruin");
        bool Passes(ZObject o) => world.Path.TilePassable(o.CenterX / 16, o.CenterY / 16, false);
        bool Connected()
        {
            var seen = new HashSet<(int, int)>();
            var queue = new Queue<(int x, int y)>();
            void Add(int x, int y)
            {
                if (x < 0 || y < 0 || x + 1 >= map.width || y + 1 >= map.height) return;
                if (!world.Path.TilePassable(x, y, false) || !world.Path.TilePassable(x + 1, y, false) ||
                    !world.Path.TilePassable(x, y + 1, false) || !world.Path.TilePassable(x + 1, y + 1, false)) return;
                if (seen.Add((x, y))) queue.Enqueue((x, y));
            }
            Add(64, 25);
            while (queue.Count > 0)
            {
                var (x, y) = queue.Dequeue();
                if (x == 24 && y == 76) return true;
                Add(x - 1, y);
                Add(x + 1, y);
                Add(x, y - 1);
                Add(x, y + 1);
            }
            return false;
        }
        check(Connected(), "Chilly Willy V2 has a continuous vehicle route between the bases");
        var west = bridges.Single(o => o.Source.x == 23);
        var east = bridges.Single(o => o.Destroyed);
        west.SetHealth(0);
        world.UpdateObjectHealth(west);
        check(!Passes(west) && !Connected(), "Losing the west bridge cuts the vehicle route");
        east.SetHealth(east.MaxHealth);
        check(Passes(east) && Connected(), "Restoring the east bridge reopens a vehicle route");

        var water = Enumerable.Range(0, map.width * map.height).Where(i => map.classes[i] == (int)ZClass.Water);
        check(water.Any(i => world.Path.TilePassable(i % map.width, i / map.width, true) &&
            !world.Path.TilePassable(i % map.width, i / map.width, false)), "Arctic meltwater admits robots and blocks vehicles");
        var rock = world.Objects.First(o => o.Type == "rock");
        int xRock = rock.LocX / 16, yRock = rock.LocY / 16 + 2;
        bool blocked = !world.Path.TilePassable(xRock, yRock, true);
        rock.SetHealth(0);
        world.UpdateObjectHealth(rock);
        world.Tick(ZConst.TICK);
        check(blocked && world.Path.TilePassable(xRock, yRock, true), "Blasting an Arctic rock opens its blocking tile");
    }
}
