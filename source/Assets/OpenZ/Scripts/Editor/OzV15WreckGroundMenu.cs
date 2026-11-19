#if !OPENZ_HARNESS
namespace OpenZ.EditorTools
{
    /// <summary>
    /// (View/OzWreckGroundCheck.cs) from the menu. Needs Play with the Unit editor open; numbers only: the report goes to the kit's proof folder
    /// (check_&lt;date_time&gt;/wreck_rest_ground_report.md).</summary>
    public static class OzV15WreckGroundMenu
    {
        public static string ProofDir => System.IO.Path.GetFullPath(System.IO.Path.Combine(UnityEngine.Application.dataPath, "../../../../work/staging/v1_5/kits/wreck_rest_ground_r6/proof"));

        static void Go(string mode, string what)
        {
            if (!UnityEngine.Application.isPlaying || !OzUnitEditor.Active) { UnityEngine.Debug.LogWarning(OzWreckGroundCheck.Tag + " enter Play in the Unit editor first (the check stages its own units)."); return; }
            var dir = System.IO.Path.Combine(ProofDir, "check_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_" + mode);
            OzWreckGroundCheck.Begin(dir, mode);
            UnityEngine.Debug.Log(OzWreckGroundCheck.Tag + " started (" + what + "): " + dir);
        }

        [UnityEditor.MenuItem("OpenZ/1.5/Checks/Wreck Rest Ground/1 Lifted wrecks come down (every kind, pieces, support gone)")]
        static void Lifted() => Go("lifted", "every vehicle kind's whole hull put in the air as in the frame, Bizon pieces, a block taken away, a Bizon over a deck edge, a heavy on a prop taken away; before + after; about 10 min");

        [UnityEditor.MenuItem("OpenZ/1.5/Checks/Wreck Rest Ground/2 Piles at night x1 and x4 (the owner's case)")]
        static void Piles() => Go("piles", "6 Bizons + 3 Mammoths + 3 heavies rammed, crushed and shot in a pile at night, x1 and x4 after + x4 before, with the frame time and the rule's own cost; about 6 min");

        [UnityEditor.MenuItem("OpenZ/1.5/Checks/Wreck Rest Ground/3 Both")]
        static void Both() => Go("all", "1 then 2; about 16 min");

        [UnityEditor.MenuItem("OpenZ/1.5/Checks/Wreck Rest Ground/1 Lifted wrecks come down (every kind, pieces, support gone)", true)]
        [UnityEditor.MenuItem("OpenZ/1.5/Checks/Wreck Rest Ground/2 Piles at night x1 and x4 (the owner's case)", true)]
        [UnityEditor.MenuItem("OpenZ/1.5/Checks/Wreck Rest Ground/3 Both", true)]
        static bool Can() => UnityEngine.Application.isPlaying && !OzWreckGroundCheck.Running;
    }
}
#endif
