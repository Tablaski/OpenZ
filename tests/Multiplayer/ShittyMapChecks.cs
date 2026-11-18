using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenZ;
using OpenZ.Multiplayer;
using OpenZ.Sim;

static class ShittyMapChecks
{
    public static void Run(Action<bool,string> check,Action<Action,string> reject)
    {
        string root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../"));
        string settings=File.ReadAllText(Path.Combine(root,"source/Assets/OpenZ/Resources/OpenZ/default_settings.txt"));
        check(OzShittyMapCatalog.All.Select(m=>m.Id).Distinct().Count()==OzShittyMapCatalog.All.Length,"Unique converted map identities");
        check(OzShittyMapCatalog.All.All(m=>!new[]{2,7,16,17}.Contains(m.Number)),"Already restored OpenZ campaign maps excluded");
        check(OzShittyMapCatalog.All.Count(m=>m.Solo)==31,"31 solo campaign choices");
        check(OzShittyMapCatalog.All.Where(m=>m.Solo&&m.Balanced).All(m=>m.SoloException&&(m.Number==21||m.Number==27)),"Fixed solo files limited to approved exceptions");
        check(OzShittyMapCatalog.Select(true,"Z EXPANSION","arctic").Select(m=>m.Number).SequenceEqual(new[]{27,28,29}),"Arctic expansion filtering and numeric order");
        foreach(var group in OzShittyMapCatalog.All.Where(m=>m.Number>0).GroupBy(m=>m.Number))
        {
            var duel=group.Single(m=>m.Duel);
            check(duel.Balanced==group.Any(m=>m.Balanced),"Preferred fixed variant for campaign "+group.Key);
            check(group.Count(m=>m.Solo)==1,"One solo variant for campaign "+group.Key);
        }
        reject(()=>new DuelRules{Map="SM99"}.Validate(),"Unknown converted map rejected");
        CheckVolcanicV2(root, settings, check, reject);
        CheckLandV2(root, settings, check, reject);
        CheckArctic(root, settings, check, reject);
        foreach(var entry in OzShittyMapCatalog.All)
        {
            var mapFile=Path.Combine(root,"shittymaps/Resources/OpenZ/Maps",entry.Id+"_grid.json");
            string json=File.ReadAllText(mapFile); var map=ZMapData.FromJson(json); var original=File.ReadAllBytes(Path.Combine(root,"shittymaps/Sources~/maps",entry.File));
            check(map.file==entry.File&&map.planet==entry.World&&map.width==BitConverter.ToUInt16(original,0)&&map.height==BitConverter.ToUInt16(original,2),entry.Id+": source identity and world");
            check(map.objects.Count==BitConverter.ToUInt16(original,56)&&map.tiles.Length==map.width*map.height,entry.Id+": source object and tile counts");
            var world=ZWorld.Create(settings,json,1935);
            check(world.Objects.Count(o=>o.IsFort&&o.Owner==Team.Red)==1&&world.Objects.Count(o=>o.IsFort&&o.Owner==Team.Blue)==1,entry.Id+": two-team fort ownership");
            if(!entry.Duel) { reject(()=>new DuelRules{Map=entry.Id}.Validate(),entry.Id+": solo-only original cannot bypass duel selection"); continue; }
            var rules=new DuelRules{Map=entry.Id,Common=DuelMissionDefaults.Create()}; rules.Validate();
            world.ConfigureDuel(rules,false); world.DuelApplyStartingArmies();
            check(world.Objects.Any(o=>o.IsMobile&&o.Owner==Team.Red)&&world.Objects.Any(o=>o.IsMobile&&o.Owner==Team.Blue),entry.Id+": lobby armies spawn for both teams");
            var receiver=new DuelOrderReceiver(world,1); var red=world.Objects.First(o=>o.IsMobile&&o.Owner==Team.Red); var blue=world.Objects.First(o=>o.IsMobile&&o.Owner==Team.Blue);
            check(!receiver.Execute(new DuelOrder{Sequence=1,ObjectId=red.Id,Kind=DuelOrderKind.Stop})&&receiver.Execute(new DuelOrder{Sequence=2,ObjectId=blue.Id,Kind=DuelOrderKind.Stop}),entry.Id+": authoritative orders preserve team control");
            var state=DuelReplica.Capture(world); var client=ZWorld.Create(settings,json,1935); client.ConfigureDuel(rules,true); DuelReplica.Apply(client,state);
            check(client.Objects.Count==world.Objects.Count,entry.Id+": initial replica object count");
        }
    }

    static void CheckVolcanicV2(string root, string settings, Action<bool, string> check, Action<Action, string> reject)
    {
        check(OzShittyMapCatalog.Select(true, world: "volcanic").Count(m => m.Number == 8) == 2,
            "Both versions of Molten Kombat are available in solo");
        foreach (var entry in OzShittyMapCatalog.VolcanicV2)
        {
            reject(() => new DuelRules { Map = entry.Id }.Validate(), entry.Id + ": the prototype is solo only");
            string json = File.ReadAllText(Path.Combine(root, "shittymaps/Resources/OpenZ/Maps", entry.Id + "_grid.json"));
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
        foreach (var entry in OzShittyMapCatalog.LandV2)
        {
            check(OzShittyMapCatalog.Select(true, world: entry.World).Contains(entry), entry.Id + ": selectable in the solo gallery");
            reject(() => new DuelRules { Map = entry.Id }.Validate(), entry.Id + ": the prototype is solo only");
            string json = File.ReadAllText(Path.Combine(root, "shittymaps/Resources/OpenZ/Maps", entry.Id + "_grid.json"));
            byte[] native = File.ReadAllBytes(Path.Combine(root, "shittymaps/Sources~/maps", entry.File));
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

    static void CheckArctic(string root, string settings, Action<bool, string> check, Action<Action, string> reject)
    {
        var entry = OzShittyMapCatalog.ChillyWillyV2;
        check(OzShittyMapCatalog.Select(true, "OPENZ-VK", "arctic").Contains(entry),
            "Chilly Willy is selectable as an original solo Arctic map");
        reject(() => new DuelRules { Map = entry.Id }.Validate(), "Chilly Willy V2 is a solo prototype");
        var json = File.ReadAllText(Path.Combine(root, "shittymaps/Resources/OpenZ/Maps", entry.Id + "_grid.json"));
        var native = File.ReadAllBytes(Path.Combine(root, "shittymaps/Sources~/maps", entry.File));
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
