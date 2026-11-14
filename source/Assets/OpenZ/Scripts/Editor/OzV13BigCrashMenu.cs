#if !OPENZ_HARNESS
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>The 1.3 item 7 menus (rule DV154, Sim/ZWorld.ShuttleBigCrash.cs: one shuttle crash landing in 3 is the BIG crash): the counted
    /// check out of Play (Editor/OzV13BigCrashCount.cs, headless on level 16's data) and the Play proof View/OzBigCrashProof.cs on level 16
    /// (its pictures, sound logs and shake curves: OzBigCrashProof.Folder(), built at run time from the project's place).</summary>
    public static class OzV13BigCrashMenu
    {
        [MenuItem("OpenZ/V1.3/Shuttle: 7 Big Crash Check (out of Play)")]
        static void CheckMenu() => OzV11Kit.RunMenu("1.3 item 7 big crash check (DV154)", () => OzV13BigCrashCount.Check(OzV11Kit.SettingsText, OzV11Kit.MapText("L16")));

        [MenuItem("OpenZ/V1.3/Shuttle: 7 Big Crash Proof (Level 16, Play)")]
        static void Proof()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[OpenZ] 1.3 item 7 big crash proof: start level 16 in Play first"); return; }
            OzBigCrashProof.Run();
            Debug.Log("[OpenZ] 1.3 item 7 big crash proof started; its report: OzBigCrashProof.Report, the log, pictures and sound logs in " + OzBigCrashProof.Folder());
        }
    }
}
#endif
