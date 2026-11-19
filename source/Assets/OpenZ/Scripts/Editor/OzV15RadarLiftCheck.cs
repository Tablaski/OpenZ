// OpenZ (GPL-3.0) - version 1.5 kit r14_r16: the Play check of the line-of-sight fog lifted while a radar of ours stands (View/OzFogOfWar.LosRadarLift.cs).
using System.Collections;
using System.Collections.Generic;
using System.IO;
using OpenZ.Sim;
using UnityEditor;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// Version 1.5 kit r14_r16, R16: OpenZ/V1.5/LOS Fog: Radar Lift Check (in Play, a MAYHEM match as a human, after the briefing; about 8 s). The radar is
    /// stood in by <see cref="OzFogOfWar.LosLiftForce"/> (as if none, then one, then none), so no rule of the match is touched; the game's own radar test is
    /// read at the end. Rows in the console and Logs/oz_v15_radar_lift.txt:
    /// RL1 no radar: the lift at 0, the line-of-sight fog as it is; RL2 a radar gained: the lift rises linearly, never back, fully lifted within about 1 s,
    /// the enemy units on explored ground all shown (none covered), their count; RL3 the last radar lost: back within about 1 s, never up, the units on
    /// explored ground not seen now hidden again (exactly where the seen map says); RL4 the game's own test now (a standing radar of ours or not).
    /// </summary>
    public static class OzV15RadarLiftCheck
    {
        const string Tag = "[OpenZ V1.5 RadarLift]", LogFile = "oz_v15_radar_lift.txt";
        static readonly List<string> lines = new List<string>();
        static int pass, total; static bool running;

        static void Say(string s) { lines.Add(s); Debug.Log(s); }
        static void Row(string id, bool ok, string text) { total++; if (ok) pass++; Say($"  {(ok ? "PASS" : "FAIL")} {id} {text}"); }
        static void End()
        {
            Say($"{Tag} {(pass == total ? "PASS" : "FAIL")} {pass}/{total}");
            try
            {
                var dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs")); Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, LogFile), string.Join("\n", lines) + "\n");
            }
            catch (System.Exception e) { Debug.LogWarning($"{Tag} could not write the log file: {e.Message}"); }
        }

        [MenuItem("OpenZ/V1.5/LOS Fog: Radar Lift Check (in Play, a MAYHEM match, about 8 s)")]
        public static void Run()
        {
            if (!EditorApplication.isPlaying) { Debug.LogWarning($"{Tag} press Play, start a MAYHEM match as a human, then run this again"); return; }
            var g = ZGame.I;
            if (!g) { Debug.LogWarning($"{Tag} no level running: start a MAYHEM match as a human"); return; }
            if (running) { Debug.LogWarning($"{Tag} the check is already running"); return; }
            running = true;
            g.StartCoroutine(Guard(Routine()));
        }

        static IEnumerator Guard(IEnumerator it)
        {
            try
            {
                while (true)
                {
                    bool more;
                    try { more = it.MoveNext(); }
                    catch (System.Exception e) { Debug.LogError($"{Tag} the check stopped: {e}"); more = false; }
                    if (!more) break;
                    yield return it.Current;
                }
            }
            finally { running = false; OzFogOfWar.LosLiftForce = -1; }
        }

        /// <summary>The enemy robots and vehicles on explored ground: how many, how many covered, how many covered against the seen map's own word.</summary>
        static void Enemies(ZWorld W, Team me, out int n, out int covered, out int wrongHidden)
        {
            n = covered = wrongHidden = 0;
            var mask = OzFogOfWar.Mask; var seen = OzFogOfWar.Seen;
            foreach (var o in W.Objects)
            {
                if (o == null || o.Owner == me || o.Owner == Team.Neutral || o.Destroyed || (o.Kind != ZKind.Robot && o.Kind != ZKind.Vehicle)) continue;
                if (mask == null || mask.ShownAtPx(o.CenterX, o.CenterY) < OzFowMask.Revealed) continue;
                n++;
                bool c = OzFogOfWar.Covers(o); if (c) covered++;
                byte v = seen != null ? seen.ShownAtPx(o.CenterX, o.CenterY) : (byte)255;
                if (c && v >= OzFogOfWar.LosShowAt) wrongHidden++;                                  // covered where the seen map shows it: wrong
            }
        }

        static IEnumerator Routine()
        {
            lines.Clear(); pass = total = 0; Say($"{Tag} {System.DateTime.Now:dd MMM yyyy HH:mm}");
            var g = ZGame.I; var W = g.World; var me = g.player;
            if (!OzFogOfWar.Wanted || OzFogOfWar.Seen == null || OzFogOfWar.RadarLift == null)
            { Row("RL0", false, $"no MAYHEM match with the line-of-sight fog running ({OzFogOfWar.WhyOffNow}): start one as a human"); End(); yield break; }
            for (int i = 0; i < 1800 && !OzFogOfWar.GameplayView; i++) yield return null;
            Row("RL0", OzFogOfWar.GameplayView, $"the player plays with the fog on screen: {OzFogOfWar.GateLine()}");
            if (!OzFogOfWar.GameplayView) { End(); yield break; }
            var L = OzFogOfWar.RadarLift;

            OzFogOfWar.LosLiftForce = 0;
            float t = Time.realtimeSinceStartup; while (Time.realtimeSinceStartup < t + 1.5f) yield return null;
            Enemies(W, me, out int n0, out int c0, out int w0);
            Row("RL1", L.Value == 0f, $"as if no radar of ours: the lift {L.Value:0.00}; enemy units on explored ground {n0}, {c0} covered by the line-of-sight fog");

            OzFogOfWar.LosLiftForce = 1;
            float t0 = Time.realtimeSinceStartup, full = -1f, half = -1f, last = L.Value; int back = 0;
            while (Time.realtimeSinceStartup < t0 + 2f)
            {
                yield return null;
                float v = L.Value, s = Time.realtimeSinceStartup - t0;
                if (v < last - 1e-6f) back++;
                if (half < 0f && v >= 0.5f) half = s;
                if (full < 0f && v >= 1f) full = s;
                last = v;
            }
            Enemies(W, me, out int n1, out int c1, out int w1);
            Row("RL2", back == 0 && full > 0.8f && full < 1.3f && half > 0.35f && half < 0.7f && c1 == 0,
                $"a radar gained: half way {half:0.00} s, fully lifted {full:0.00} s (wanted about {OzFowRadarLift.Seconds:0.0} s), steps back {back}; enemy units on explored ground {n1}, covered {c1} (wanted 0)");

            OzFogOfWar.LosLiftForce = 0;
            t0 = Time.realtimeSinceStartup; full = -1f; half = -1f; last = L.Value; int up = 0;
            while (Time.realtimeSinceStartup < t0 + 2f)
            {
                yield return null;
                float v = L.Value, s = Time.realtimeSinceStartup - t0;
                if (v > last + 1e-6f) up++;
                if (half < 0f && v <= 0.5f) half = s;
                if (full < 0f && v <= 0f) full = s;
                last = v;
            }
            Enemies(W, me, out int n2, out int c2, out int w2);
            Row("RL3", up == 0 && full > 0.8f && full < 1.3f && half > 0.35f && half < 0.7f && w2 == 0,
                $"the last radar lost: half way {half:0.00} s, fully back {full:0.00} s, steps up {up}; enemy units on explored ground {n2}, covered again {c2} (covered where the seen map shows them: {w2}, wanted 0)");

            OzFogOfWar.LosLiftForce = -1;
            t = Time.realtimeSinceStartup; while (Time.realtimeSinceStartup < t + 1.3f) yield return null;
            bool ours = false;
            foreach (var o in W.Objects) if (o != null && o.Kind == ZKind.Building && o.Type == "radar" && o.Owner == me && !o.Destroyed && o.KillAt < 0) { ours = true; break; }
            Row("RL4", L.Radar == ours && L.Value == (ours ? 1f : 0f), $"the game's own test again: a standing radar of ours {(ours ? "yes" : "no")}, the lift {L.Value:0.00} (gained {L.Gains}, lost {L.Losses} in this match)");
            End();
        }
    }
}
