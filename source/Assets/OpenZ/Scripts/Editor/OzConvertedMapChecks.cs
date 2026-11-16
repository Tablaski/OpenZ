using System;
using System.IO;
using System.Linq;
using OpenZ.Sim;
using UnityEditor;
using UnityEngine;
namespace OpenZ.EditorTools
{
    public static class OzConvertedMapChecks
    {
        public static void Run()
        {
            var text=Resources.Load<TextAsset>("OpenZ/Maps/L19_grid");if(!text)throw new Exception("Mayhem grid missing from game Resources");
            var settings=Resources.Load<TextAsset>("OpenZ/default_settings");var map=ZMapData.FromJson(text.text);var world=ZWorld.Create(settings.text,text.text,19);
            if(map.file!="p02_bb_orig19_fixed.map"||map.planet!="city"||map.width!=128||map.height!=116||map.objects.Count!=453)throw new Exception("Original Mayhem identity mismatch");
            int checkedUnits=0;
            foreach(var source in map.objects.Where(o=>(o.owner=="red"||o.owner=="blue")&&(o.ot==3||o.ot==4||o.ot==5)))
            {
                var owner=source.owner=="red"?Team.Red:Team.Blue;
                if(!world.Objects.Any(o=>o.Owner==owner&&o.Type==source.Type&&o.LocX==source.x*16&&o.LocY==source.y*16))throw new Exception("Game starting unit mismatch: "+source.kind+" "+source.x+","+source.y);
                checkedUnits++;
            }
            if(checkedUnits!=16)throw new Exception("Expected eight starting red and eight starting blue unit objects");
            if(!EditorBuildSettings.scenes.Any(s=>s.enabled&&s.path.EndsWith("L19_Mayhem_Converted.unity")))throw new Exception("Converted scene missing from release build settings");
            File.WriteAllText(Path.GetFullPath("../.local/mayhem-game-checks.txt"),"PASS original Mayhem file and City identity\nPASS 453 source objects\nPASS all 16 red/blue starting unit types, owners and positions in the actual game simulation\nPASS converted scene enabled for the next player build\nPlayer menu/rendering/gameplay validation remains pending a new game build.\n");
        }
    }
}
