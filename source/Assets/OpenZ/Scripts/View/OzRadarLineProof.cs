using System.Collections;
using System.Collections.Generic;
using System.Text;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.2 item 16 Play proof (View/OzRadarLine.cs): on any level in Play (menu OpenZ/V1.2/Radar: First vs Back Online Line Proof,
    /// Editor/OzRadarLineMenu.cs). Set-up: the CPU teams removed (their orders would take the zones), the day clock held. The zones of
    /// the level's two radars change owner through the game's own capture rule (ZWorld.AwardZone, as a robot touching the flag), so
    /// FlagView plays the whole capture and asks for Rachel's line as in a match; the line is read from the sound log (the clip that
    /// really played). Radar lines of one kind are 15 s apart in the game (AudioDirector.KindGap), so each step waits for that.
    ///  A. first radar of the game, after a short tug of war (ours, the enemy's, ours again, 0.25 s apart, as when both sides touch
    ///     the flag)                                     -> ann_radar_first, clip rachel_radar_online_first; the minimap gets its radar
    ///  P. the second radar while the first one stands   -> ann_radar_online (Radar online. / Radar active.), never "back online"
    ///  L. both radar zones taken by the enemy            -> "Radar lost." for us, no radar line for the enemy, the minimap loses its radar
    ///  B. one radar taken back                           -> ann_radar_back_online, clip rachel_s3_radar_is_back_online
    ///  R. a radar that is a ruin taken                   -> no radar line (it does nothing yet)
    /// Counted PASS / FAIL lines and pictures (normal RTS camera, full screen, the minimap in the corner) in <see cref="Folder"/>. Test only.
    /// </summary>
    public sealed class OzRadarLineProof : MonoBehaviour
    {
        public static string Report = "idle";
        /// <summary>
        /// run time above the project; else a folder in the player's data.</summary>
        public static string Folder()
        {
            try
            {
                var d = new System.IO.DirectoryInfo(Application.dataPath);
                for (int i = 0; i < 8 && d != null; i++, d = d.Parent)
                {
                    var st = System.IO.Path.Combine(d.FullName, "work", "staging");
                    if (!System.IO.Directory.Exists(st)) continue;
                    foreach (var sub in System.IO.Directory.GetDirectories(st))
                    {
                        var kit = System.IO.Path.Combine(sub, "k16_radar_line");
                        if (System.IO.Directory.Exists(kit)) return System.IO.Path.Combine(kit, "proof");
                    }
                }
            }
            catch (System.Exception ex) { Debug.LogWarning("[OzRadarLineProof] " + ex.Message); }
            return System.IO.Path.Combine(Application.persistentDataPath, "k16_radar_line");
        }
        public static void Run() { Report = "running"; new GameObject("OzRadarLineProof").AddComponent<OzRadarLineProof>(); }

        const float KindWait = 17f, LineWait = 14f, CamHeight = 70f;                             // 70 m = the RTS camera's start height
        ZGame g; ZWorld W; RtsCamera rts; Team me = Team.Red, foe = Team.Blue;
        readonly StringBuilder sb = new StringBuilder(); int fails, passes; string dir;

        void Start()
        {
            g = ZGame.I; W = g ? g.World : null; rts = FindFirstObjectByType<RtsCamera>();
            dir = Folder(); System.IO.Directory.CreateDirectory(dir);
            StartCoroutine(All());
        }

        void Log(string t) { sb.AppendLine(t); Debug.Log("[OzRadarLineProof] " + t); }
        void Check(string what, bool ok) { if (ok) passes++; else fails++; Log((ok ? "PASS " : "FAIL ") + what); }
        IEnumerator Wait(float s0) { float t = 0f; while (t < s0) { t += Time.unscaledDeltaTime; yield return null; } }
        void Cam(ZObject o) { if (!rts || o == null) return; rts.FreeLookOff(); var p = g.CenterWorld(o); rts.focus = new Vector3(p.x, 0f, p.z); rts.SetHeightImmediate(CamHeight); }
        void Shot(string name) { ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, name + ".png")); Log("   picture " + name + ".png"); }

        /// <summary>Waits for a played Rachel line of one of the radar events since t0; returns (event, clip) or (null, null).</summary>
        IEnumerator WaitLine(float t0, float max, string[] result)
        {
            float t = 0f; result[0] = result[1] = null;
            while (t < max)
            {
                foreach (var e in OzSoundLog.Since(t0))
                    if (e.src == "VO" && e.key != null && (e.key == OzRadarLineRule.First || e.key == OzRadarLineRule.Back || e.key == OzRadarLineRule.More)
                        && e.outcome != null && e.outcome.StartsWith("PLAY")) { result[0] = e.key; result[1] = e.clip; yield break; }
                t += Time.unscaledDeltaTime; yield return null;
            }
        }
        bool LostLinePlayed(float t0) { foreach (var e in OzSoundLog.Since(t0)) if (e.src == "VO" && e.key == "ann_radar_lost" && e.outcome != null && e.outcome.StartsWith("PLAY")) return true; return false; }

        ZObject FlagOf(ZObject radar) { foreach (var f in W.Flags) foreach (var b in f.LinkedObjects) if (b == radar) return f; return null; }

        IEnumerator All()
        {
            if (W == null || !FlagView.Active || !AudioDirector.I || !OzRadarLine.I) { Log("no game with FlagView, AudioDirector and OzRadarLine (a level in Play)"); fails++; Finish(); yield break; }
            me = g.player; foe = me == Team.Red ? Team.Blue : Team.Red;
            Log($"1.2 item 16 radar line proof, {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}, {Screen.width}x{Screen.height}, world time {W.Time:F0} s, player {me}");
            int bots = W.Bots.Count; W.Bots.Clear();
            var dn = FindFirstObjectByType<DayNight>(); if (dn) { dn.timeOfDay = 0.10f; dn.Apply(0.10f); dn.running = false; }
            Log($"set-up: CPU removed {bots}, day 0.10 held; announcer chatter {AudioDirector.Chatter.Names[AudioDirector.Chatter.Level]}; {OzRadarLine.Status()}");
            if (AudioDirector.Chatter.Level == 2) { Log("ANNOUNCER is MINIMAL in the options: radar lines are silent there by design; set it to FULL and run again"); fails++; Finish(); yield break; }
            var radars = new List<ZObject>();
            foreach (var o in W.Objects) if (o.Kind == ZKind.Building && o.Type == "radar" && FlagOf(o) != null) radars.Add(o);
            if (radars.Count < 2) { Log($"this level has {radars.Count} radar(s) with a flag; the proof needs 2"); fails++; Finish(); yield break; }
            ZObject r1 = radars[0], r2 = radars[1], f1 = FlagOf(r1), f2 = FlagOf(r2);
            Log($"radar 1 #{r1.Id} ({r1.Owner}, flag #{f1.Id}), radar 2 #{r2.Id} ({r2.Owner}, flag #{f2.Id})");
            Check("P0 the player starts this game without a radar picture (the first case can be shown)", !OzRadarLine.Rule.Covered(me) && OzRadarLine.Rule.Pictures(me) == 0 && !OzRadarLine.Rule.Had(me));
            if (r1.Destroyed || r2.Destroyed) { Log("a radar is a ruin at the start: run the proof at a level start"); fails++; Finish(); yield break; }
            var res = new string[2];
            Cam(r1); yield return Wait(1.0f); Shot("K16_R0_before_no_radar");

            // ---- A: the first radar of the game, after a short tug of war (both sides touch the flag: it flips before we keep it)
            float t0 = Time.unscaledTime;
            W.AwardZone(f1, me); yield return Wait(0.25f);
            W.AwardZone(f1, foe); yield return Wait(0.25f);
            W.AwardZone(f1, me);
            yield return WaitLine(t0, LineWait, res);
            Check($"A first radar after a tug of war (our pictures {OzRadarLine.Rule.Pictures(me)}): Rachel says the first-radar line, never back online (event {res[0] ?? "none"}, clip {res[1] ?? "none"})",
                  res[0] == OzRadarLineRule.First && res[1] == "rachel_radar_online_first");
            Log($"INFO A Rachel said a radar-lost line during the tug of war: {(LostLinePlayed(t0) ? "yes (AudioDirector.OnFlag speaks on every loss, as before; not this kit's rule)" : "no")}");
            yield return Wait(1.2f);
            Check($"A the minimap draws the enemy units now (OpenZMinimap.HasRadar {OpenZMinimap.HasRadar}, rule {OpenZMinimap.RadarRule})", OpenZMinimap.HasRadar);
            Shot("K16_R1_first_radar_line");
            yield return Wait(KindWait);

            // ---- P: the second radar while the first one stands
            t0 = Time.unscaledTime;
            W.AwardZone(f2, me);
            Cam(r2);
            yield return WaitLine(t0, LineWait, res);
            Check($"P second radar while the first stands: a plain radar line, never back online (event {res[0] ?? "none"}, clip {res[1] ?? "none"})",
                  res[0] == OzRadarLineRule.More && res[1] != null && res[1] != "rachel_s3_radar_is_back_online");
            yield return Wait(1.2f); Shot("K16_R2_second_radar_plain_line");
            yield return Wait(KindWait);

            // ---- L: both radar zones taken by the enemy
            t0 = Time.unscaledTime;
            W.AwardZone(f1, foe); W.AwardZone(f2, foe);
            yield return WaitLine(t0, 8f, res);
            Check($"L both radars lost: no radar-online line for the enemy's captures (event {res[0] ?? "none"})", res[0] == null);
            Log($"INFO L Rachel said a radar-lost line: {(LostLinePlayed(t0) ? "yes" : "no (the alert limiter or the queue can drop it; not this kit's rule)")}");
            Check($"L our radar picture is gone (rule {OzRadarLine.Rule.Covered(me)}, minimap {OpenZMinimap.HasRadar}); the enemy's is followed (pictures {OzRadarLine.Rule.Pictures(foe)})",
                  !OzRadarLine.Rule.Covered(me) && !OpenZMinimap.HasRadar && OzRadarLine.Rule.Covered(foe));
            yield return Wait(KindWait);

            // ---- B: one radar taken back
            t0 = Time.unscaledTime;
            W.AwardZone(f1, me);
            Cam(r1);
            yield return WaitLine(t0, LineWait, res);
            Check($"B radar taken back: Rachel says Radar is back online (event {res[0] ?? "none"}, clip {res[1] ?? "none"})", res[0] == OzRadarLineRule.Back && res[1] == "rachel_s3_radar_is_back_online");
            yield return Wait(1.2f); Shot("K16_R3_back_online_line");
            yield return Wait(KindWait);

            // ---- R: a radar that is a ruin
            r2.SetHealth(0); W.UpdateObjectHealth(r2);
            yield return Wait(2f);
            t0 = Time.unscaledTime;
            if (r2.Destroyed) W.AwardZone(f2, me);
            yield return WaitLine(t0, LineWait, res);
            Check($"R a ruined radar taken: no radar line (ruin {r2.Destroyed}, event {res[0] ?? "none"}, last choice {OzRadarLine.Last})", r2.Destroyed && res[0] == null && OzRadarLine.Last.StartsWith("none"));

            Log("rule: " + OzRadarLine.Status());
            Finish();
        }

        void Finish()
        {
            Log($"item 16 result: {passes} PASS, {fails} FAIL");
            Report = fails == 0 ? $"PASS ({passes})" : $"FAIL ({fails} of {passes + fails})";
            try { System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "radar_line_proof.txt"), sb.ToString()); }
            catch (System.Exception ex) { Debug.LogWarning("[OzRadarLineProof] " + ex.Message); }
            StopAllCoroutines();
            Destroy(gameObject);
        }
    }
}
