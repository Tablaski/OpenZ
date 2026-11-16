using System;
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
        public string Variant => Balanced ? File.Contains("remixFDL") ? "FDL REMIX" : "FIXED" : "ORIGINAL";
        public OzShittyMap(string id, string name, string world, string file, int number, bool solo, bool duel, bool balanced, bool soloException)
        { Id=id; Name=name; World=world; File=file; Number=number; Solo=solo; Duel=duel; Balanced=balanced; SoloException=soloException; }
    }

    public static partial class OzShittyMapCatalog
    {
        public static OzShittyMap Find(string id) => Array.Find(All, m => m.Id == id);
        public static bool IsDuelMap(string id) => Find(id)?.Duel == true;
        public static OzShittyMap[] Select(bool solo, string group = "ALL", string world = "all", int sort = 0)
        {
            var maps = All.Where(m => (solo ? m.Solo : m.Duel) && (group == "ALL" || m.Group == group) && (world == "all" || m.World == world));
            return (sort == 1 ? maps.OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase).ThenBy(m => m.Number)
                : sort == 2 ? maps.OrderBy(m => m.World).ThenBy(m => m.Number == 0 ? 999 : m.Number).ThenBy(m => m.Name)
                : maps.OrderBy(m => m.Number == 0 ? 999 : m.Number).ThenBy(m => m.Name)).ToArray();
        }
    }
}
