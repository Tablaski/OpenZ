#if !OPENZ_HARNESS
namespace OpenZ.EditorTools
{
    /// <summary>
    /// (View/OzCaptureDustRingCheck.cs) from the menu. Needs Play in a level; every case ends the match (Play again for the next one). Report + frames
    /// go to the kit's proof folder (check_&lt;date_time&gt;_case&lt;n&gt;/).</summary>
    public static class OzV15CaptureDustRingMenu
    {
        public static string ProofDir => System.IO.Path.GetFullPath(System.IO.Path.Combine(UnityEngine.Application.dataPath, "../../../../work/staging/v1_5/kits/capture_dust_ring_r11/proof"));
        const string Root = "OpenZ/1.5/Checks/Capture Dust Ring/";

        static void Go(OzCaptureDustRingCheck.Case c)
        {
            if (!UnityEngine.Application.isPlaying) { UnityEngine.Debug.LogWarning(OzCaptureDustRingCheck.Tag + " enter Play in a level first"); return; }
            var dir = System.IO.Path.Combine(ProofDir, "check_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_case" + (int)c);
            OzCaptureDustRingCheck.Begin(c, dir);
            UnityEngine.Debug.Log(OzCaptureDustRingCheck.Tag + " case " + (int)c + " started: " + dir);
        }

        static bool Can() => UnityEngine.Application.isPlaying && !OzCaptureDustRingCheck.Running;

        [UnityEditor.MenuItem(Root + "Case 1: capture the enemy fort, fix on (dust ring 0, nuke full; ends the match)")]
        static void Case1() => Go(OzCaptureDustRingCheck.Case.Capture);
        [UnityEditor.MenuItem(Root + "Case 1: capture the enemy fort, fix on (dust ring 0, nuke full; ends the match)", true)]
        static bool Can1() => Can();

        [UnityEditor.MenuItem(Root + "Case 2: capture the enemy fort, fix OFF (A/B: the dust ring back; ends the match)")]
        static void Case2() => Go(OzCaptureDustRingCheck.Case.CaptureFixOff);
        [UnityEditor.MenuItem(Root + "Case 2: capture the enemy fort, fix OFF (A/B: the dust ring back; ends the match)", true)]
        static bool Can2() => Can();

        [UnityEditor.MenuItem(Root + "Case 3: control, the enemy fort falls without a capture (SHOCK kept; ends the match)")]
        static void Case3() => Go(OzCaptureDustRingCheck.Case.FallNoCapture);
        [UnityEditor.MenuItem(Root + "Case 3: control, the enemy fort falls without a capture (SHOCK kept; ends the match)", true)]
        static bool Can3() => Can();
    }
}
#endif
