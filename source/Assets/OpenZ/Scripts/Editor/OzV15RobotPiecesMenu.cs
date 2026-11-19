#if !OPENZ_HARNESS
namespace OpenZ.EditorTools
{
    /// <summary>
    /// check (View/OzRobotPiecesCheck.cs) from the menu. Needs Play (the Unit editor, or a level); the report and the frames go to the kit's proof
    /// folder (check_&lt;date_time&gt;_&lt;where&gt;/).</summary>
    public static class OzV15RobotPiecesMenu
    {
        public static string ProofDir => System.IO.Path.GetFullPath(System.IO.Path.Combine(UnityEngine.Application.dataPath, "../../../../work/staging/v1_5/kits/robot_pieces_ground_r10/proof"));

        [UnityEditor.MenuItem("OpenZ/1.5/Checks/Robot Pieces/Every robot killed every way, every piece on the ground (Unit editor or a level)")]
        static void Go()
        {
            if (!UnityEngine.Application.isPlaying) { UnityEngine.Debug.LogWarning(OzRobotPiecesCheck.Tag + " enter Play first (the Unit editor, or a level)"); return; }
            var dir = System.IO.Path.Combine(ProofDir, "check_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + (OzUnitEditor.Active ? "_unit_editor" : "_level"));
            OzRobotPiecesCheck.Begin(dir);
            UnityEngine.Debug.Log(OzRobotPiecesCheck.Tag + " started (the 8 grunts night + day; grunt, tough, sniper, pyro, psycho, laser, electrobot x8 each: shot x3, blown up x2, burnt, run over, headshot; in the Unit editor also the heroes and EvilZod; about 3 min): " + dir);
        }

        [UnityEditor.MenuItem("OpenZ/1.5/Checks/Robot Pieces/Every robot killed every way, every piece on the ground (Unit editor or a level)", true)]
        static bool Can() => UnityEngine.Application.isPlaying && !OzRobotPiecesCheck.Running;

        // A/B: the fix OFF, the owner's case only (8 grunts shot at night): a FAIL there shows the check sees the old hover. Informative, run it first.
        [UnityEditor.MenuItem("OpenZ/1.5/Checks/Robot Pieces/A-B: the owner's grunts with the fix OFF (must show the old hover)")]
        static void GoAb()
        {
            if (!UnityEngine.Application.isPlaying) { UnityEngine.Debug.LogWarning(OzRobotPiecesCheck.Tag + " enter Play first (the Unit editor, or a level)"); return; }
            var dir = System.IO.Path.Combine(ProofDir, "check_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + (OzUnitEditor.Active ? "_unit_editor" : "_level") + "_AB_fix_off");
            OzRobotPiecesCheck.Begin(dir, true);
            UnityEngine.Debug.Log(OzRobotPiecesCheck.Tag + " A/B started (the fix OFF; the 8 grunts at night; about 20 s; the fix is switched back on at its end): " + dir);
        }

        [UnityEditor.MenuItem("OpenZ/1.5/Checks/Robot Pieces/A-B: the owner's grunts with the fix OFF (must show the old hover)", true)]
        static bool CanAb() => UnityEngine.Application.isPlaying && !OzRobotPiecesCheck.Running;
    }
}
#endif
