#if !OPENZ_HARNESS
namespace OpenZ.EditorTools
{
    /// <summary>
    /// fort the 1.4 blast): the Play check (View/OzMegaBlastSplitCheck.cs) from the menu. Needs Play in a level. Case 1 and then case 3 can run in one
    /// level run (case 1 uses OUR fort's cannon); case 2 and case 3 end the match (Play again for the other one). Report + frames go to the kit's proof
    /// folder (check_&lt;date_time&gt;_case&lt;n&gt;/).</summary>
    public static class OzV15MegaBlastSplitMenu
    {
        public static string ProofDir => System.IO.Path.GetFullPath(System.IO.Path.Combine(UnityEngine.Application.dataPath, "../../../../work/staging/v1_5/kits/megacannon_blast_split_r10/proof"));
        const string Root = "OpenZ/1.5/Checks/Megacannon Blast Split/";

        static void Go(OzMegaBlastSplitCheck.Case c)
        {
            if (!UnityEngine.Application.isPlaying) { UnityEngine.Debug.LogWarning(OzMegaBlastSplitCheck.Tag + " enter Play in a level first"); return; }
            var dir = System.IO.Path.Combine(ProofDir, "check_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_case" + (int)c);
            OzMegaBlastSplitCheck.Begin(c, dir);
            UnityEngine.Debug.Log(OzMegaBlastSplitCheck.Tag + " case " + (int)c + " started: " + dir);
        }

        static bool Can() => UnityEngine.Application.isPlaying && !OzMegaBlastSplitCheck.Running;

        [UnityEditor.MenuItem(Root + "Case 1: shoot our cannon off our standing fort (GEYSER + COLUMN 1)")]
        static void Case1() => Go(OzMegaBlastSplitCheck.Case.ShotOff);
        [UnityEditor.MenuItem(Root + "Case 1: shoot our cannon off our standing fort (GEYSER + COLUMN 1)", true)]
        static bool Can1() => Can();

        [UnityEditor.MenuItem(Root + "Case 2: our fort falls with its cannon (GEYSER 0, 1.4 blast 1; ends the match)")]
        static void Case2() => Go(OzMegaBlastSplitCheck.Case.FortFalls);
        [UnityEditor.MenuItem(Root + "Case 2: our fort falls with its cannon (GEYSER 0, 1.4 blast 1; ends the match)", true)]
        static bool Can2() => Can();

        [UnityEditor.MenuItem(Root + "Case 3: the enemy fort falls, the nuke ending's beat (play as blue; GEYSER 0, 1.4 blast 1; ends the match)")]
        static void Case3() => Go(OzMegaBlastSplitCheck.Case.NukeOutro);
        [UnityEditor.MenuItem(Root + "Case 3: the enemy fort falls, the nuke ending's beat (play as blue; GEYSER 0, 1.4 blast 1; ends the match)", true)]
        static bool Can3() => Can();
    }
}
#endif
