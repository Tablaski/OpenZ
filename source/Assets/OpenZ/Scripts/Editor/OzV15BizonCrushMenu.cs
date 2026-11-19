namespace OpenZ.EditorTools
{
    /// <summary>1.5 kit bizon_crush: the headless counted check (Editor/OzV15BizonCrushCount.cs); the report goes to the kit's proof folder.</summary>
    public static class OzV15BizonCrushMenu
    {
        public static string CountFile => System.IO.Path.GetFullPath(System.IO.Path.Combine(UnityEngine.Application.dataPath, "../../../../work/staging/v1_5/kits/bizon_crush/proof/count_unity.txt"));

        [UnityEditor.MenuItem("OpenZ/V1.5/Bizon Crush: Count (headless, DV183)")]
        static void Count()
        {
            if (UnityEngine.Application.isPlaying) { UnityEngine.Debug.LogWarning("[OpenZ V1.5] Bizon Crush count: leave Play mode first (the check sets the simulation's static switches)."); return; }
            string r; using (OzV11Kit.GameSwitches()) r = OzV15BizonCrushCount.Run(OzV11Kit.SettingsText, OzV11Kit.MapText);
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(CountFile));
            System.IO.File.WriteAllText(CountFile, r);
            UnityEngine.Debug.Log("[OpenZ V1.5] Bizon Crush count (" + CountFile + ")\n" + r);
        }
    }
}
