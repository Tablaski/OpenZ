namespace OpenZ.EditorTools
{
    /// <summary>1.4 kit u2_followups: the Play check (View/OzU2FollowupPlayCheck.cs: the portrait star after a hidden HUD, the camera's picture
    /// in and after the Alt+F view with Stop NaN off, a committed fort gun and a gun's EJECT with nobody shooting the emptied one, the death
    /// owning a dying robot's body; the hero pilot's shuttle on level 16) and its last report. The reports go to the kit's proof/play. The
    /// headless count: OpenZ/V1.4/U2 Followups: Emptied Shots Count (headless, DV176) (Editor/OzV14U2FollowupsCount.cs).</summary>
    public static class OzV14U2FollowupsMenu
    {
        public static string PlayFolder => System.IO.Path.GetFullPath(System.IO.Path.Combine(UnityEngine.Application.dataPath, "../../../../work/staging/v1_4/kits/u2_followups/proof/play"));

        [UnityEditor.MenuItem("OpenZ/V1.4/U2 Followups: Play Check (L02 at night: star, blob, fort, eject, death)")]
        static void Play() => Start(OzU2FollowupPlayCheck.Rows.Main, "level 2 at night, the normal mode");

        [UnityEditor.MenuItem("OpenZ/V1.4/U2 Followups: Shuttle Row (level 16)")]
        static void Shuttle() => Start(OzU2FollowupPlayCheck.Rows.Shuttle, "level 16, the normal mode (ALAN & BRAD on the map)");

        static void Start(OzU2FollowupPlayCheck.Rows rows, string where)
        {
            if (!UnityEngine.Application.isPlaying) { UnityEngine.Debug.LogWarning(OzU2FollowupPlayCheck.Tag + " start a level in Play first (" + where + ")"); return; }
            OzU2FollowupPlayCheck.Folder = PlayFolder;
            OzU2FollowupPlayCheck.Begin(rows);
        }

        [UnityEditor.MenuItem("OpenZ/V1.4/U2 Followups: Play Check, last report")]
        static void Last() => UnityEngine.Debug.Log(OzU2FollowupPlayCheck.Running ? OzU2FollowupPlayCheck.Tag + " running" : OzU2FollowupPlayCheck.Report);
    }
}
