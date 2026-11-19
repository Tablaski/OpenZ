#if !OPENZ_HARNESS
namespace OpenZ.EditorTools
{
    /// <summary>
    /// Needs Play with the Unit editor open; numbers only (no pictures): the report goes to the kit's proof folder (check_&lt;date_time&gt;).</summary>
    public static class OzV15RammedWreckRestMenu
    {
        public static string ProofDir => System.IO.Path.GetFullPath(System.IO.Path.Combine(UnityEngine.Application.dataPath, "../../../../work/staging/v1_5/kits/rammed_wreck_rest_n30/proof"));

        [UnityEditor.MenuItem("OpenZ/1.5/Checks/Rammed Wreck Rest")]
        static void Run()
        {
            if (!UnityEngine.Application.isPlaying || !OzUnitEditor.Active) { UnityEngine.Debug.LogWarning(OzRammedWreckRestCheck.Tag + " enter Play in the Unit editor first (the check stages its own Mammoths and targets)."); return; }
            var dir = System.IO.Path.Combine(ProofDir, "check_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            OzRammedWreckRestCheck.Begin(dir, "before,after");
            UnityEngine.Debug.Log(OzRammedWreckRestCheck.Tag + " started (14 kinds rammed on flat ground at night, before = the test exe's way, after = the kit; about 10 min): " + dir);
        }

        [UnityEditor.MenuItem("OpenZ/1.5/Checks/Rammed Wreck Rest", true)]
        static bool Can() => UnityEngine.Application.isPlaying;
    }
}
#endif
