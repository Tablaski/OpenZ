namespace OpenZ.EditorTools
{
    /// <summary>1.5 kit mammoth_ram: the headless counted check (Editor/OzV15MammothRamCount.cs), the Play check (View/OzMammothRamPlayCheck.cs:
    /// the ram drawn in a running level, sounds and frames) and its last report; the report and pictures go to the kit's proof/play.</summary>
    public static class OzV15MammothRamMenu
    {
        public static string PlayFolder => System.IO.Path.GetFullPath(System.IO.Path.Combine(UnityEngine.Application.dataPath, "../../../../work/staging/v1_5/kits/mammoth_ram/proof/play"));
        public static string CountFile => System.IO.Path.GetFullPath(System.IO.Path.Combine(UnityEngine.Application.dataPath, "../../../../work/staging/v1_5/kits/mammoth_ram/proof/count_unity.txt"));

        [UnityEditor.MenuItem("OpenZ/V1.5/Mammoth Ram: Count (headless, DV182)")]
        static void Count()
        {
            if (UnityEngine.Application.isPlaying) { UnityEngine.Debug.LogWarning("[OpenZ V1.5] Mammoth Ram count: leave Play mode first (the check sets the simulation's static switches)."); return; }
            string r; using (OzV11Kit.GameSwitches()) r = OzV15MammothRamCount.Run(OzV11Kit.SettingsText, OzV11Kit.MapText);
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(CountFile));
            System.IO.File.WriteAllText(CountFile, r);
            UnityEngine.Debug.Log("[OpenZ V1.5] Mammoth Ram count (" + CountFile + ")\n" + r);
        }

        [UnityEditor.MenuItem("OpenZ/V1.5/Mammoth Ram: Play Check (a level playing)")]
        static void Play()
        {
            if (!UnityEngine.Application.isPlaying) { UnityEngine.Debug.LogWarning(OzMammothRamPlayCheck.Tag + " start a level (or the Unit editor) in Play first"); return; }
            OzMammothRamPlayCheck.Folder = PlayFolder;
            OzMammothRamPlayCheck.Begin();
        }

        [UnityEditor.MenuItem("OpenZ/V1.5/Mammoth Ram: Play Check, last report")]
        static void Last() => UnityEngine.Debug.Log(OzMammothRamPlayCheck.Running ? OzMammothRamPlayCheck.Tag + " running" : OzMammothRamPlayCheck.Report);
    }
}
