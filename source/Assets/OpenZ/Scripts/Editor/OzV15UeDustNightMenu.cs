namespace OpenZ.EditorTools
{
    /// <summary>1.5 kit ue_dust_night: the menu of the Unit editor's dust + night Play check (View/OzUeDustNightCheck.cs). Frames and the report go
    /// </summary>
    public static class OzV15UeDustNightMenu
    {
        static string Proof(string f) => System.IO.Path.GetFullPath(System.IO.Path.Combine(UnityEngine.Application.dataPath, "../../../../work/staging/v1_5/kits/ue_dust_night/proof/" + f));

        [UnityEditor.MenuItem("OpenZ/V1.5/Unit Editor: Dust + Night Check (Play, the unit editor open)")]
        static void Play()
        {
            if (!UnityEngine.Application.isPlaying) { UnityEngine.Debug.LogWarning("[OpenZ V1.5] UE dust + night check: enter Play and open the Unit editor first."); return; }
            OpenZ.OzUeDustNightCheck.Begin(Proof("play/" + System.DateTime.Now.ToString("yyyyMMdd_HHmm")));
        }
    }
}
