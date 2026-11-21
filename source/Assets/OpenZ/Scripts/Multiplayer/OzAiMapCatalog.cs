using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenZ
{
    /// <summary>Pure data shared by the menus, converter and authoritative duel validation.</summary>
    public sealed class OzAiMap
    {
        public readonly string Id, Name, World, File;
        public readonly int Number, Starts;
        public readonly bool Solo, Duel, Balanced, SoloException, ForkOriginal;
        readonly string officialScene;
        public string Scene => officialScene ?? Id + "_Converted";
        public string ScenePath => (IsOfficial ? "Assets/OpenZ/Scenes/" : "Packages/org.openz.aimaps/Scenes/") + Scene + ".unity";
        public bool IsOfficial => officialScene != null;
        public string Group => ForkOriginal ? "OPENZ-VK" : Number == 0 ? "MULTIPLAYER" : Number > 20 ? "Z EXPANSION" : "Z ORIGINAL";
        public string Title => (Number > 0 ? Number.ToString("00") + " · " : "") + Name;
        public string Variant => "V3";
        public OzAiMap(string id, string name, string world, string file, int number, bool solo, bool duel, bool balanced, bool soloException, bool forkOriginal = false, int starts = 2, string officialScene = null)
        { Id=id; Name=name; World=world; File=file; Number=number; Solo=solo; Duel=duel; Balanced=balanced; SoloException=soloException; ForkOriginal=forkOriginal; Starts=starts; this.officialScene=officialScene; }
    }

    public static partial class OzAiMapCatalog
    {
        public static readonly OzAiMap[] VolcanicV2 =
        {
            new OzAiMap("SM07V2", "Pyro Technics", "volcanic", "p02_bb_orig07.map", 7, true, false, false, false),
            new OzAiMap("SM08V2", "Molten Kombat", "volcanic", "p02_bb_orig08.map", 8, true, false, false, false)
        };
        public static readonly OzAiMap[] LandV2 =
        {
            new OzAiMap("SM16V2", "Jungle", "jungle", "p02_bb_orig16.map", 16, true, false, false, false),
            new OzAiMap("SM17V2", "Car Park", "city", "p02_bb_orig17.map", 17, true, false, false, false),
            new OzAiMap("SM02V2", "Desert", "desert", "p02_bb_orig02.map", 2, true, false, false, false)
        };
        public static readonly OzAiMap ChillyWillyV2 = new OzAiMap(
            "SMChillyWillyV2", "Chilly Willy", "arctic", "vk_chilly_willy.map", 0, true, false, false, false, forkOriginal: true);
        public static OzAiMap ThreePlayerSkirmish => Find("SMP03M01");
        public static IEnumerable<OzAiMap> V2 => LandV2.Concat(VolcanicV2).Append(ChillyWillyV2);
        public static IEnumerable<OzAiMap> Playable => All.Append(ChillyWillyV2);

        public static IEnumerable<OzAiMap> SkirmishMaps => Playable.Concat(V2).Concat(MultiplayerMaps).Distinct();
        internal static readonly OzAiMap[] OfficialSkirmishMaps =
        {
            Official("L02", "Desert", "desert", "L02_Desert", 2),
            Official("L07", "Pyro Technics", "volcanic", "L07_Volcanic", 7),
            Official("L16", "The Jungle", "jungle", "L16_Jungle", 16),
            Official("L17", "The Car Park", "city", "L17_CarPark", 17),
            Official("L18", "Bridge Game", "city", "L18_BridgeGame", 18)
        };

        static OzAiMap Official(string id, string name, string world, string scene, int number) =>
            new OzAiMap(id, name, world, $"p02_bb_orig{number:00}.map", number, true, true, false, false, officialScene: scene);

        public static OzAiMap Find(string id) => Playable.FirstOrDefault(map => map.Id == id) ?? Array.Find(MultiplayerMaps, map => map.Id == id);
        internal static OzAiMap FindSkirmishMap(string id) =>
            Find(id) ?? V2.FirstOrDefault(map => map.Id == id) ?? Array.Find(OfficialSkirmishMaps, map => map.Id == id);
        public static bool IsDuelMap(string id) => Find(id)?.Duel == true;
        public static OzAiMap[] SelectMultiplayer(int starts, string group, string world, int sort) => starts == 2
            ? Select(false, group, world, sort)
            : Sort(MultiplayerMaps.Where(m => m.Starts == starts && (world == "all" || m.World == world)), sort);

        public static OzAiMap[] Select(bool solo, string group = "ALL", string world = "all", int sort = 0)
        {
            var maps = Playable.Where(m => (solo ? m.Solo : m.Duel) && (group == "ALL" || m.Group == group) && (world == "all" || m.World == world));
            return Sort(maps, sort);
        }

        internal static OzAiMap[] Sort(IEnumerable<OzAiMap> maps, int sort) =>
            (sort == 1 ? maps.OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase).ThenBy(m => m.Number)
                : sort == 2 ? maps.OrderBy(m => m.World).ThenBy(m => m.Number == 0 ? 999 : m.Number).ThenBy(m => m.Name)
                : maps.OrderBy(m => m.Number == 0 ? 999 : m.Number).ThenBy(m => m.Name)).ToArray();
    }
}
