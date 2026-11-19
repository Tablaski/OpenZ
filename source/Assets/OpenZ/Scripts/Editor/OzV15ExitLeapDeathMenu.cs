namespace OpenZ.EditorTools
{
    /// <summary>1.5 kit exit_leap_death: the Play check (View/OzExitLeapDeathCheck.cs: a tank or the mech destroyed while its driver gets out,
    /// DV179's throw drawn and timed to the rules) and its last report; the report and pictures go to the kit's proof/play. The headless count:
    /// OpenZ/V1.5/Exit Leap Death: Count (headless, DV179) (Editor/OzV15ExitLeapDeathCount.cs).</summary>
    public static class OzV15ExitLeapDeathMenu
    {
        public static string PlayFolder => System.IO.Path.GetFullPath(System.IO.Path.Combine(UnityEngine.Application.dataPath, "../../../../work/staging/v1_5/kits/exit_leap_death/proof/play"));

        [UnityEditor.MenuItem("OpenZ/V1.5/Exit Leap Death: Play Check (level 2 playing)")]
        static void Play()
        {
            if (!UnityEngine.Application.isPlaying) { UnityEngine.Debug.LogWarning(OzExitLeapDeathCheck.Tag + " start a level in Play first (level 2, the normal mode)"); return; }
            OzExitLeapDeathCheck.Folder = PlayFolder;
            OzExitLeapDeathCheck.Begin();
        }

        [UnityEditor.MenuItem("OpenZ/V1.5/Exit Leap Death: Play Check, last report")]
        static void Last() => UnityEngine.Debug.Log(OzExitLeapDeathCheck.Running ? OzExitLeapDeathCheck.Tag + " running" : OzExitLeapDeathCheck.Report);
    }
}
