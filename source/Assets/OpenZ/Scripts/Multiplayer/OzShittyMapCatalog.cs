using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenZ
{
    /// <summary>Pure data shared by the menus, converter and authoritative duel validation.</summary>
    public sealed class OzShittyMap
    {
        public readonly string Id, Name, World, File;
        public readonly int Number;
        public readonly bool Solo, Duel, Balanced, SoloException;
        public string Scene => Id + "_Converted";
        public string ScenePath => "Packages/org.openz.shittymaps/Scenes/" + Scene + ".unity";
        public string Group => Number == 0 ? "MULTIPLAYER" : Number > 20 ? "Z EXPANSION" : "Z ORIGINAL";
        public string Title => (Number > 0 ? Number.ToString("00") + " · " : "") + Name;
        public string Variant => Id.EndsWith("V2", StringComparison.Ordinal) ? "V2" : Balanced ? File.Contains("remixFDL") ? "FDL REMIX" : "FIXED" : "ORIGINAL";
        public OzShittyMap(string id, string name, string world, string file, int number, bool solo, bool duel, bool balanced, bool soloException)
        { Id=id; Name=name; World=world; File=file; Number=number; Solo=solo; Duel=duel; Balanced=balanced; SoloException=soloException; }
    }

    public static partial class OzShittyMapCatalog
    {
        public static readonly OzShittyMap[] VolcanicV2 =
        {
            new OzShittyMap("SM07V2", "Pyro Technics", "volcanic", "p02_bb_orig07.map", 7, true, false, false, false),
            new OzShittyMap("SM08V2", "Molten Kombat", "volcanic", "p02_bb_orig08.map", 8, true, false, false, false)
        };
        public static readonly OzShittyMap[] LandV2 =
        {
            new OzShittyMap("SM16V2", "Jungle", "jungle", "p02_bb_orig16.map", 16, true, false, false, false),
            new OzShittyMap("SM17V2", "Car Park", "city", "p02_bb_orig17.map", 17, true, false, false, false),
            new OzShittyMap("SM02V2", "Desert", "desert", "p02_bb_orig02.map", 2, true, false, false, false)
        };
        public static IEnumerable<OzShittyMap> V2 => LandV2.Concat(VolcanicV2);
        public static IEnumerable<OzShittyMap> Playable => All.Concat(V2);

        public static OzShittyMap Find(string id) => Playable.FirstOrDefault(m => m.Id == id);
        public static bool IsDuelMap(string id) => Find(id)?.Duel == true;
        public static OzShittyMap[] Select(bool solo, string group = "ALL", string world = "all", int sort = 0)
        {
            var maps = Playable.Where(m => (solo ? m.Solo : m.Duel) && (group == "ALL" || m.Group == group) && (world == "all" || m.World == world));
            return (sort == 1 ? maps.OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase).ThenBy(m => m.Number)
                : sort == 2 ? maps.OrderBy(m => m.World).ThenBy(m => m.Number == 0 ? 999 : m.Number).ThenBy(m => m.Name)
                : maps.OrderBy(m => m.Number == 0 ? 999 : m.Number).ThenBy(m => m.Name)).ToArray();
        }
    }
}
