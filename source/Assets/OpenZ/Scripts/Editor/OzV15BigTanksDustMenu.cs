namespace OpenZ.EditorTools
{
    /// <summary>1.5 kit big_tanks_dust_redo: the Play checks (View/OzBigTanksDustCheck.cs, the Unit editor in Play; pick the Desert dunes floor first).
    /// Reports and frames go to the kit's proof/play folder.</summary>
    public static class OzV15BigTanksDustMenu
    {
        static string Proof(string f) => System.IO.Path.GetFullPath(System.IO.Path.Combine(UnityEngine.Application.dataPath, "../../../../work/staging/v1_5/kits/big_tanks_dust_redo/proof/" + f));

        [UnityEditor.MenuItem("OpenZ/V1.5/Big Tanks Dust Redo: Play Check (Unit editor in Play)")]
        static void Play()
        {
            if (!UnityEngine.Application.isPlaying) { UnityEngine.Debug.LogWarning("[OpenZ V1.5] Big Tanks Dust Redo: enter Play, open the Unit editor (Desert dunes floor) first."); return; }
            OpenZ.OzBigTanksDustCheck.Begin(Proof("play_" + System.DateTime.Now.ToString("yyyyMMdd_HHmm")), false);
        }

        [UnityEditor.MenuItem("OpenZ/V1.5/Big Tanks Dust Redo: Count 50 (Unit editor in Play)")]
        static void Count()
        {
            if (!UnityEngine.Application.isPlaying) { UnityEngine.Debug.LogWarning("[OpenZ V1.5] Big Tanks Dust Redo: enter Play, open the Unit editor first."); return; }
            OpenZ.OzBigTanksDustCheck.Begin(Proof("count_" + System.DateTime.Now.ToString("yyyyMMdd_HHmm")), true);
        }
    }
}
