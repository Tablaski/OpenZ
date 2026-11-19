#if !OPENZ_HARNESS
namespace OpenZ.EditorTools
{
    /// <summary>1.5 kit hit_feedback_sounds_r8 (FAILS A31, a player's report: a bullet that hits must sound like a hit; the owner's caps, on screen
    /// only): the hit feedback sounds check (View/OzHitFeedbackCheck.cs) from the menu. Needs Play with the Unit editor open; the report, the game's
    /// own sound (wav) and a frame per scene go to the kit's proof folder (check_&lt;date_time&gt;/). Real time: film the Game view while it runs.</summary>
    public static class OzV15HitFeedbackMenu
    {
        public static string ProofDir => System.IO.Path.GetFullPath(System.IO.Path.Combine(UnityEngine.Application.dataPath, "../../../../work/staging/v1_5/kits/hit_feedback_sounds_r8/proof"));

        [UnityEditor.MenuItem("OpenZ/1.5/Checks/Hit Feedback Sounds/Seven scenes, the caps counted (Play, the Unit editor open; about 2 min)")]
        static void Go()
        {
            if (!UnityEngine.Application.isPlaying || !OzUnitEditor.Active) { UnityEngine.Debug.LogWarning(OzHitFeedbackCheck.Tag + " enter Play and open the Unit editor first"); return; }
            var dir = System.IO.Path.Combine(ProofDir, "check_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            OzHitFeedbackCheck.Begin(dir);
            UnityEngine.Debug.Log(OzHitFeedbackCheck.Tag + " started (jeep on a squad, the jeep's rounds at empty ground, a squad fight, snipers, a gatling, a busy fight, the busy fight off screen; real time, about 2 min): " + dir);
        }

        [UnityEditor.MenuItem("OpenZ/1.5/Checks/Hit Feedback Sounds/Seven scenes, the caps counted (Play, the Unit editor open; about 2 min)", true)]
        static bool Can() => UnityEngine.Application.isPlaying && !OzHitFeedbackCheck.Running;

        // A/B for the owner's ears: scenes 1-3 with the kit OFF = the sounds as they are today. No gate.
        [UnityEditor.MenuItem("OpenZ/1.5/Checks/Hit Feedback Sounds/A-B: scenes 1-3 with the kit OFF (today's sounds; about 40 s)")]
        static void GoAb()
        {
            if (!UnityEngine.Application.isPlaying || !OzUnitEditor.Active) { UnityEngine.Debug.LogWarning(OzHitFeedbackCheck.Tag + " enter Play and open the Unit editor first"); return; }
            var dir = System.IO.Path.Combine(ProofDir, "check_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_AB_kit_off");
            OzHitFeedbackCheck.Begin(dir, true);
            UnityEngine.Debug.Log(OzHitFeedbackCheck.Tag + " A/B started (the kit OFF: today's sounds; scenes 1-3; switched back on at its end): " + dir);
        }

        [UnityEditor.MenuItem("OpenZ/1.5/Checks/Hit Feedback Sounds/A-B: scenes 1-3 with the kit OFF (today's sounds; about 40 s)", true)]
        static bool CanAb() => UnityEngine.Application.isPlaying && !OzHitFeedbackCheck.Running;
    }
}
#endif
