#if !OPENZ_HARNESS
namespace OpenZ.EditorTools
{
    /// <summary>
    /// The check needs Play with the Unit editor open; numbers only (no pictures): its report goes to the kit's proof folder
    /// (check_&lt;date_time&gt;). The switch is for the side-by-side frames (off = every fire where the test exe makes it).</summary>
    public static class OzV15FireSourceMenu
    {
        public static string ProofDir => System.IO.Path.GetFullPath(System.IO.Path.Combine(UnityEngine.Application.dataPath, "../../../../work/staging/v1_5/kits/fire_no_source_n33/proof"));

        [UnityEditor.MenuItem("OpenZ/1.5/Checks/Fire Has A Source")]
        static void Run()
        {
            if (!UnityEngine.Application.isPlaying || !OzUnitEditor.Active) { UnityEngine.Debug.LogWarning(OzFireSourceCheck.Tag + " enter Play in the Unit editor first (the check stages its own big tanks and targets)."); return; }
            var dir = System.IO.Path.Combine(ProofDir, "check_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            OzFireSourceCheck.Begin(dir, "before,after");
            UnityEngine.Debug.Log(OzFireSourceCheck.Tag + " started (8 rams, 6 crushes, 4 big-tank deaths, 2 shoved burning wrecks; before = the tie off, after = the tie on; about 15 min): " + dir);
        }

        [UnityEditor.MenuItem("OpenZ/1.5/Checks/Fire Has A Source", true)]
        static bool Can() => UnityEngine.Application.isPlaying;

        [UnityEditor.MenuItem("OpenZ/1.5/Checks/Fire Source Tie: OFF (as the test exe)")]
        static void Off() { OzFireSource.On = false; UnityEngine.Debug.Log(OzFireSourceCheck.Tag + " the tie is OFF until the next Play or 'ON': " + OzFireSource.Status()); }

        [UnityEditor.MenuItem("OpenZ/1.5/Checks/Fire Source Tie: ON (the kit)")]
        static void OnAgain() { OzFireSource.On = true; UnityEngine.Debug.Log(OzFireSourceCheck.Tag + " the tie is ON: " + OzFireSource.Status()); }

        [UnityEditor.MenuItem("OpenZ/1.5/Checks/Fire Source Status")]
        static void Status() { UnityEngine.Debug.Log(OzFireSourceCheck.Tag + " " + OzFireSource.Status()); }
    }
}
#endif
