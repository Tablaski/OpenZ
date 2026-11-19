#if !OPENZ_HARNESS
namespace OpenZ.EditorTools
{
    /// <summary>
    /// (View/OzGunSeatWreckCheck.cs) from the menu. Needs Play (the Unit editor, or a level for the night level run); the report and the dense frames
    /// go to the kit's proof folder (check_&lt;date_time&gt;_&lt;where&gt;/).</summary>
    public static class OzV15GunSeatWreckMenu
    {
        public static string ProofDir => System.IO.Path.GetFullPath(System.IO.Path.Combine(UnityEngine.Application.dataPath, "../../../../work/staging/v1_5/kits/gun_seat_wreck_r8/proof"));

        [UnityEditor.MenuItem("OpenZ/1.5/Checks/Gun Seat Wreck/Every gun killed, every wreck part on its support (Unit editor or a level, night)")]
        static void Go()
        {
            if (!UnityEngine.Application.isPlaying) { UnityEngine.Debug.LogWarning(OzGunSeatWreckCheck.Tag + " enter Play first (the Unit editor, or a level)"); return; }
            var dir = System.IO.Path.Combine(ProofDir, "check_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + (OzUnitEditor.Active ? "_unit_editor" : "_level"));
            OzGunSeatWreckCheck.Begin(dir);
            UnityEngine.Debug.Log(OzGunSeatWreckCheck.Tag + " started (gun v1, gun v2, gatling, howitzer, missile cannon; base whole and broken; + the gatling x3 at night; each followed until its parts go, up to 26 s; about 8 min): " + dir);
        }

        [UnityEditor.MenuItem("OpenZ/1.5/Checks/Gun Seat Wreck/Every gun killed, every wreck part on its support (Unit editor or a level, night)", true)]
        static bool Can() => UnityEngine.Application.isPlaying && !OzGunSeatWreckCheck.Running;

        // checker 15 Nov: A/B, the fix OFF, the owner's case only (5 gatlings at night): a FAIL there shows the check sees the old hover. Informative.
        [UnityEditor.MenuItem("OpenZ/1.5/Checks/Gun Seat Wreck/A-B: the owner's gatling at night with the fix OFF (must show the old hover)")]
        static void GoAb()
        {
            if (!UnityEngine.Application.isPlaying) { UnityEngine.Debug.LogWarning(OzGunSeatWreckCheck.Tag + " enter Play first (the Unit editor, or a level)"); return; }
            var dir = System.IO.Path.Combine(ProofDir, "check_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + (OzUnitEditor.Active ? "_unit_editor" : "_level") + "_AB_fix_off");
            OzGunSeatWreckCheck.Begin(dir, true);
            UnityEngine.Debug.Log(OzGunSeatWreckCheck.Tag + " A/B started (the fix OFF; the gatling x5 at night; about 3 min; the fix is switched back on at its end): " + dir);
        }

        [UnityEditor.MenuItem("OpenZ/1.5/Checks/Gun Seat Wreck/A-B: the owner's gatling at night with the fix OFF (must show the old hover)", true)]
        static bool CanAb() => UnityEngine.Application.isPlaying && !OzGunSeatWreckCheck.Running;
    }
}
#endif
