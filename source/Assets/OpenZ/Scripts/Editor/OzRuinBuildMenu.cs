#if !OPENZ_HARNESS
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>The 1.3 item 4 menu (rule DV155, Sim/ZWorld.RuinProduction.cs: no build clock on a destroyed factory): the counted check out of
    /// Play (Editor/OzRuinBuildCount.cs, headless on levels 2, 16 and 17; about 15 matches of 12-20 game minutes each) and the Play frames
    /// (View/OzRuinBuildWatch.cs).</summary>
    public static class OzRuinBuildMenu
    {
        [MenuItem("OpenZ/V1.3/Rules: Item 4 Destroyed Factory No Build Check (out of Play)")]
        static void CheckMenu() => OzV11Kit.RunMenu("1.3 item 4 destroyed factory: no build clock, rebuilt empty (DV155)", () => OzRuinBuildCount.Check(OzV11Kit.SettingsText, OzV11Kit.MapText));

        [MenuItem("OpenZ/V1.3/Rules: Item 4 Destroyed Factory Frames (a level in Play)")]
        static void Frames()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[OpenZ] item 4 frames: start a level in Play first (a few minutes in, the computer building)"); return; }
            OzRuinBuildWatch.Run();
            Debug.Log("[OpenZ] item 4 frames started (about 3 real minutes at 4x); its report: OzRuinBuildWatch.Report and " + OzRuinBuildWatch.Folder);
        }
    }
}
#endif
