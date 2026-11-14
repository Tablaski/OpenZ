#if !OPENZ_HARNESS
using System.Linq;
using OpenZ.Sim;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>The 1.2 item 111 menus (rule DV144: the SHUTTLE box for both sides, level 17's two empty car-park shuttles, the head start off
    /// every shuttle): the headless counted check (no Play; run it after a level 17 Play session and it also measures the scene's drawn props,
    /// which ZGame keeps for the parking rule; its report also goes to OzSideShuttleProof.Folder()/check.txt) and the Play proof
    /// (View/OzSideShuttleProof.cs, started on the MainMenu scene in Play: it deploys levels 16 and 17 as red and as blue itself).</summary>
    public static class OzSideShuttleMenu
    {
        [MenuItem("OpenZ/V1.2/Shuttle: 111 Both Sides Check (headless, no Play)")]
        static void Check()
        {
            if (SessionState.GetBool("oz_k82_check_running", false)) { Debug.LogWarning("[OpenZ] 1.2 item 111 check: already running (a re-sent menu call is ignored)"); return; }
            SessionState.SetBool("oz_k82_check_running", true);
            try
            {
                OzV11Kit.RunMenu("1.2 item 111 both sides shuttle check (DV144)", () =>
                {
                    string head = SameRules() + "\n" + (ZGame.HeadStartTypes.SequenceEqual(OzV12PlayBlueCount.HeadL02) && ZGame.HeadStartL16Types.SequenceEqual(OzV12PlayBlueCount.HeadL16)
                                  && ZGame.EasyL17Types.SequenceEqual(OzV12PlayBlueCount.HeadL17) ? "PASS  the check's head start lists are ZGame's own" : "FAIL  the check's head start lists differ from ZGame's (View/ZGame.cs)");
                    string report = head + "\n" +OzV12SideShuttleCount.Check(OzV11Kit.SettingsText, OzV11Kit.MapText, OzL17ShuttleCount.EditorKeepOut(), null, false,
                                                                                         s => EditorUtility.DisplayProgressBar("1.2 item 111 check", "part " + s, 0.5f));
                    try
                    {
                        System.IO.Directory.CreateDirectory(OzSideShuttleProof.Folder());
                        System.IO.File.WriteAllText(System.IO.Path.Combine(OzSideShuttleProof.Folder(), "check.txt"), report);
                    }
                    catch (System.Exception ex) { Debug.LogWarning("[OpenZ] 1.2 item 111 check: the report file: " + ex.Message); }
                    return report;
                });
            }
            finally { EditorUtility.ClearProgressBar(); SessionState.SetBool("oz_k82_check_running", false); }
        }

        /// <summary>The check's copy of the box rule against the game's own (UI/OzShuttleStart.cs): every level, difficulty and box state.</summary>
        static string SameRules()
        {
            var bad = new System.Text.StringBuilder();
            foreach (var lv in new[] { "L02", "L16", "L17" })
                foreach (var d in new[] { GameDifficulty.Easy, GameDifficulty.Medium, GameDifficulty.Hard })
                    foreach (bool box in new[] { true, false })
                        if (OzShuttleStart.Rule(lv, d, box) != OzV12SideShuttleCount.BoxRule(lv, d, box) || OzShuttleStart.L17Start(lv, d, box) != OzV12SideShuttleCount.BoxL17(lv, d, box)
                            || OzShuttleStart.Offered(lv, d, false) != (lv != "L02" && d != GameDifficulty.Hard))
                            bad.Append($" {lv} {d} box {box};");
            return bad.Length == 0 ? "PASS  the check's box rule is the game's own (UI/OzShuttleStart.cs: Rule, L17Start, Offered for levels 16 / 17 on EASY / MEDIUM)" : "FAIL  the check's box rule differs from the game's:" + bad;
        }

        [MenuItem("OpenZ/V1.2/Shuttle: 111 Both Sides Proof (MainMenu, Play)")]
        static void Proof()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[OpenZ] 1.2 item 111 proof: start the MainMenu scene in Play first (it deploys levels 16 and 17 as red and as blue itself)"); return; }
            OzSideShuttleProof.Run();
            Debug.Log("[OpenZ] 1.2 item 111 proof started; its report: OzSideShuttleProof.Report, the log and pictures in " + OzSideShuttleProof.Folder());
        }

        [MenuItem("OpenZ/V1.2/Shuttle: 111 Both Sides Status (Play)")]
        static void Status()
        {
            var g = ZGame.I; var w = g ? g.World : null;
            if (w == null) { Debug.Log("[OpenZ] 1.2 item 111 status: no level in Play; proof " + OzSideShuttleProof.Report); return; }
            string ships = string.Join("; ", w.Objects.Where(o => o.IsShuttle && !o.Destroyed).Select(o => $"#{o.Id} {o.Owner} tile {o.CenterX >> 4},{o.CenterY >> 4} heading {o.ShuttleHeadingDeg:0} pilots {o.Drivers.Count}"));
            Debug.Log($"[OpenZ] 1.2 item 111 status: {g.level}, the player {g.player}, DV144 {ZWorld.SideShuttlesDeviation}, made {w.SideShuttlesMade}, moved {w.SideShuttlesMoved} {w.SideShuttleWhy}; ships: {ships}; "
                      + $"head start places refused for a shuttle {w.HeadStartShuttleRefusals}; proof {OzSideShuttleProof.Report}");
        }
    }
}
#endif
