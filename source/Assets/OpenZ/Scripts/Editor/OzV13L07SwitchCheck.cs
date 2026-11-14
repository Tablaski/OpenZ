// OpenZ (GPL-3.0) - version 1.3, item 12 part B7: the counted check of level 7's per-level switches (edit mode, no Play): none of them
// falls through to the desert. Run it after the scene is built and item 30's register_l07.py has run.
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    public static class OzV13L07SwitchCheck
    {
        [MenuItem("OpenZ/Checks/V1.3/L07 level switches (no Play)", false, 7640)]
        public static string Run()
        {
            var lines = new List<string>(); int ok = 0, n = 0;
            void Line(bool good, string what) { n++; if (good) ok++; lines.Add((good ? "PASS " : "FAIL ") + what); }
            string L = OzVolcanic.Level;
            Line(OzLevelL07.Scene == "L07_Volcanic", "card scene name");
            Line(EditorBuildSettings.scenes.Any(s => s.path.EndsWith("/L07_Volcanic.unity")), "scene in the build settings");
            Line(OzLevelL07.Registered, "card registered (item 30's register_l07.py)");
            Line(OzLevelL07.ByNumber(OzLevelL07.MenuOrder()), "menu order " + string.Join(" ", OzLevelL07.MenuOrder()));
            Line(ZodShotList.For(L).level == "L07", "red shot list is level 7's own");
            Line(ZodShotList.ForBlue(L) == ZodShotList.B07, "blue shot list is level 7's own");
            Line(OpenZDifficulty.HeadStartCount(L) == ZGame.HeadStartL07Types.Length && OpenZDifficulty.HeadStartHint(L).Length > 0, "head start count and hint");
            Line(Enumerable.SequenceEqual(ZGame.HeadStartL07Types, ZGame.HeadStartL16Types), "head start as level 16");
            Line(OzVolcanic.PortraitBg(L) != "desert", "portrait backdrop is not the desert's: " + OzVolcanic.PortraitBg(L));
            Line(OzVolcanic.BlueBriefingScene(L) == OzVolcanic.BlueStartScene || OzVolcanic.BlueBriefingScene(L) == OzBlueScenes.Briefing, "blue briefing scene: " + OzVolcanic.BlueBriefingScene(L));
            Line(FlybySky.Entry(L) != null, "a flyby sky for the level: no sky = no sun disc in the briefing flights");
            Line(AssetDatabase.LoadAssetAtPath<Material>("Assets/OpenZ/Materials/Env/M_L07_Ground.mat"), "ground material M_L07_Ground");
            Line(AssetDatabase.LoadAssetAtPath<Material>("Assets/OpenZ/Models/RocksZ/M_RockZ_Volcanic.mat"), "rock material M_RockZ_Volcanic");
            lines.Add($"PASS {ok}/{n}");
            var text = "[OpenZ] L07 level switches\n" + string.Join("\n", lines);
            Debug.Log(text); return text;
        }
    }
}
