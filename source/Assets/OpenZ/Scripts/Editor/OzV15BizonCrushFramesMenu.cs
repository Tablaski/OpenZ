#if !OPENZ_HARNESS
namespace OpenZ.EditorTools
{
    /// <summary>1.5 kit bizon_crush_v3: the Bizon crush frames check (View/OzBizonCrushFramesCheck.cs) from the menu. Needs Play with the Unit
    /// editor open; the frames and the report go to the kit's proof folder (frames_&lt;date_time&gt;).</summary>
    public static class OzV15BizonCrushFramesMenu
    {
        public static string ProofDir => System.IO.Path.GetFullPath(System.IO.Path.Combine(UnityEngine.Application.dataPath, "../../../../work/staging/v1_5/kits/bizon_crush_v3/proof"));

        [UnityEditor.MenuItem("OpenZ/1.5/Checks/Bizon Crush Frames")]
        static void Run()
        {
            if (!UnityEngine.Application.isPlaying || !OzUnitEditor.Active) { UnityEngine.Debug.LogWarning(OzBizonCrushFramesCheck.Tag + " enter Play in the Unit editor first (the check stages its own Bizons and targets)."); return; }
            var dir = System.IO.Path.Combine(ProofDir, "frames_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            OzBizonCrushFramesCheck.Begin(dir);
            UnityEngine.Debug.Log(OzBizonCrushFramesCheck.Tag + " started: " + dir);
        }

        [UnityEditor.MenuItem("OpenZ/1.5/Checks/Bizon Crush Frames (every other kind)")]
        static void RunOthers()
        {
            if (!UnityEngine.Application.isPlaying || !OzUnitEditor.Active) { UnityEngine.Debug.LogWarning(OzBizonCrushFramesCheck.Tag + " enter Play in the Unit editor first."); return; }
            var dir = System.IO.Path.Combine(ProofDir, "frames_others_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            OzBizonCrushFramesCheck.Begin(dir, OzBizonCrushFramesCheck.Others);
            UnityEngine.Debug.Log(OzBizonCrushFramesCheck.Tag + " started (jeep, APC, crane, missile launcher, guns, Mammoth, Bizon): " + dir);
        }

        [UnityEditor.MenuItem("OpenZ/1.5/Checks/Bizon Crush Frames (every other kind)", true)]
        static bool CanOthers() => UnityEngine.Application.isPlaying;

        [UnityEditor.MenuItem("OpenZ/1.5/Checks/Bizon Crush Frames", true)]
        static bool Can() => UnityEngine.Application.isPlaying;
    }
}
#endif
