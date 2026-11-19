// OpenZ (GPL-3.0) - version 1.5, level 18 "Bridge Game" (kit level18_map): the counted check of level 18's per-level switches (edit mode, no
// Play): none of them falls through to the desert, and none of the Car Park's own rules is taken over. Run it after the scene is built and
// the kit's register_l18.py has run. Pattern: Editor/OzV13L07SwitchCheck.cs.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using OpenZ.Sim;

namespace OpenZ.EditorTools
{
    public static class OzV15L18SwitchCheck
    {
        [MenuItem("OpenZ/Checks/V1.5/L18 level switches (no Play)", false, 7710)]
        public static string Run()
        {
            var lines = new List<string>(); int ok = 0, n = 0;
            void Line(bool good, string what) { n++; if (good) ok++; lines.Add((good ? "PASS " : "FAIL ") + what); }
            string L = OzCity.BridgeGame;
            Line(OzCity.Is(L) && OzCity.Is(OzCity.CarPark) && !OzCity.Is("L02") && !OzCity.Is("L16") && !OzCity.Is(OzVolcanic.Level), "city levels: L17 and L18 only");
            Line(OzLevelL18.Scene == "L18_BridgeGame", "card scene name");
            Line(EditorBuildSettings.scenes.Any(s => s.path.EndsWith("/L18_BridgeGame.unity") && s.enabled), "scene in the build settings");
            Line(OzLevelL18.Registered, "card registered (the kit's register_l18.py)");
            var ids = OzLevelDef.All.Select(d => d.Id).ToArray();
            Line(OzLevelL07.ByNumber(ids), "menu order by number: " + string.Join(" ", ids));
            // the map and its rules
            var map = Resources.Load<TextAsset>("OpenZ/Maps/L18_grid");
            Line(map, "map data Resources/OpenZ/Maps/L18_grid");
            if (map)
            {
                var w = ZWorld.Create(Resources.Load<TextAsset>("OpenZ/default_settings").text, map.text, 1);
                var br = w.Objects.Where(o => o.IsBridge).ToList();
                Line(w.Zones.Count == 14 && w.Zones.Count(z => z.Flag != null) == 12, $"zones {w.Zones.Count} (14), flags {w.Zones.Count(z => z.Flag != null)} (12)");
                Line(br.Count == 5 && br.Count(o => o.Destroyed) == 4, $"bridges {br.Count} (5), destroyed at the start {br.Count(o => o.Destroyed)} (4: the three centre bridges and the south-west one, as the original)");
                Line(!w.CarParkMap, "none of the Car Park's map rules fire on level 18");
                Line(br.Count(o => OzBridgeKit.Long(L, OzBridgeKit.Tiles(o))) == 3, "three bridges take the long city family");
            }
            Line(!ZGame.ParkedStartOn(L), "no parked start (the Car Park's rule)");
            // look, sound, labels
            Line(OzBridgeKit.Biome(L) == "city" && OzBridgeKit.Key(L, 12, "destroyed") == "bridge_city_long_destroyed" && OzBridgeKit.Key(L, 6, "intact") == "bridge_city_intact", "bridge kit keys");
            Line(OzFogBiomeLooks.BiomeOf(L) == OzFogBiomeLooks.BiomeOf(OzCity.CarPark), "MAYHEM fog look: the city's");
            Line(FlybySky.Entry(L) != null && FlybySky.Entry(L) == FlybySky.Entry(OzCity.CarPark), "flyby sky: the harbour dawn (no sky = no sun disc in the briefing flights)");
            Line(OzVolcanic.PortraitBg(L) == "city", "portrait backdrop: " + OzVolcanic.PortraitBg(L));
            Line(OpenZFxCatalog.RippleColorFor(L) == OpenZFxCatalog.RippleColorFor(OzCity.CarPark), "water ripples: the harbour's colour");
            Line(Mathf.Approximately(OzCamEdge.ReachCap(L), OzCamEdge.ReachCap(OzCity.CarPark)), "camera edge reach as the Car Park");
            Line(Mathf.Approximately(DetailKit.WaterY(L), -0.8f) || Mathf.Approximately(DetailKit.WaterY(L), DetailKit.WaterY(OzCity.CarPark)), "water level -0.8 m: " + DetailKit.WaterY(L).ToString("0.00"));
            Line(OpenZDifficulty.HeadStartCount(L) == ZGame.HeadStartL18Types.Length && OpenZDifficulty.HeadStartHint(L).Length > 0 && OzDeployBar.HeadHint(L).Length > 0, "head start count and hints: " + OpenZDifficulty.HeadStartHint(L));
            Line(!ZGame.HeadStartL18Types.Contains("crane") && ZGame.HeadStartL18Types.SequenceEqual(ZGame.HeadStartL16Types), "head start holds no crane (owner addition 49: cranes are found EMPTY further away), two jeeps and a light tank as on level 16");
            Line(OzCity.BlueBriefingScene(L) == OzCity.BlueStartScene || OzCity.BlueBriefingScene(L) == OzBlueScenes.Briefing, "blue briefing scene: " + OzCity.BlueBriefingScene(L));
            Line(OzCity.BlueBriefingScene(OzVolcanic.Level) == OzVolcanic.BlueBriefingScene(OzVolcanic.Level) && OzCity.BlueBriefingScene("L02") == OzBlueScenes.Briefing, "the other levels' blue briefing scenes are unchanged");
            Line(System.Array.IndexOf(OzLevelWarm.Older, L) < 0, "start work runs ahead on both sides (not in the older levels' list)");
            Line(File.Exists("Assets/OpenZ/Resources/OpenZ/Shaders/Levels/L18_warm.shadervariants"), "warm list Resources/OpenZ/Shaders/Levels/L18_warm.shadervariants (menu OpenZ/Shaders/Level warm lists - make now)");
            Line(Resources.Load<TextAsset>("OpenZ/RoadMarkings/L18_road_markings"), "bridge deck road markings data");
            Line(AssetDatabase.LoadAssetAtPath<Material>("Assets/OpenZ/Materials/Env/M_L18_Ground.mat"), "ground material M_L18_Ground");
            // the briefings (kit level18_briefings): reported, they do not fail this kit's check
            lines.Add("INFO red shot list: " + ZodShotList.For(L).level + (ZodShotList.For(L).level == "L18" ? "" : " (level 18's own list comes with kit level18_briefings)"));
            lines.Add("INFO blue shot list: " + ZodShotList.ForBlue(L).level + (ZodShotList.ForBlue(L).level == "L18B" ? "" : " (level 18's own list comes with kit level18_briefings: no player build before it is in)"));
            lines.Add($"{(ok == n ? "PASS" : "FAIL")} {ok}/{n}");
            var text = "[OpenZ] L18 level switches\n" + string.Join("\n", lines);
            if (ok == n) Debug.Log(text); else Debug.LogError(text);
            return text;
        }
    }
}
