using System;
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
        foreach(var entry in OzShittyMapCatalog.All)
        {
            var mapFile=Path.Combine(root,"shittymaps/Resources/OpenZ/Maps",entry.Id+"_grid.json");
            string json=File.ReadAllText(mapFile); var map=ZMapData.FromJson(json); var original=File.ReadAllBytes(Path.Combine(root,"shittymaps/Sources~/maps",entry.File));
            check(map.file==entry.File&&map.planet==entry.World&&map.width==BitConverter.ToUInt16(original,0)&&map.height==BitConverter.ToUInt16(original,2),entry.Id+": source identity and world");
            check(map.objects.Count==BitConverter.ToUInt16(original,56)&&map.tiles.Length==map.width*map.height,entry.Id+": source object and tile counts");
            var world=ZWorld.Create(settings,json,1935);
            check(world.Objects.Count(o=>o.IsFort&&o.Owner==Team.Red)==1&&world.Objects.Count(o=>o.IsFort&&o.Owner==Team.Blue)==1,entry.Id+": two-team fort ownership");
            if(!entry.Duel) { reject(()=>new DuelRules{Map=entry.Id}.Validate(),entry.Id+": solo-only original cannot bypass duel selection"); continue; }
            var rules=new DuelRules{Map=entry.Id,Common=DuelMissionDefaults.Read(map)}; rules.Validate();
            world.ConfigureDuel(rules,false); world.DuelApplyStartingArmies();
            check(world.Objects.Any(o=>o.IsMobile&&o.Owner==Team.Red)&&world.Objects.Any(o=>o.IsMobile&&o.Owner==Team.Blue),entry.Id+": lobby armies spawn for both teams");
            var receiver=new DuelOrderReceiver(world,1); var red=world.Objects.First(o=>o.IsMobile&&o.Owner==Team.Red); var blue=world.Objects.First(o=>o.IsMobile&&o.Owner==Team.Blue);
            check(!receiver.Execute(new DuelOrder{Sequence=1,ObjectId=red.Id,Kind=DuelOrderKind.Stop})&&receiver.Execute(new DuelOrder{Sequence=2,ObjectId=blue.Id,Kind=DuelOrderKind.Stop}),entry.Id+": authoritative orders preserve team control");
            var state=DuelReplica.Capture(world); var client=ZWorld.Create(settings,json,1935); client.ConfigureDuel(rules,true); DuelReplica.Apply(client,state);
            check(client.Objects.Count==world.Objects.Count,entry.Id+": initial replica object count");
        }
    }
}
