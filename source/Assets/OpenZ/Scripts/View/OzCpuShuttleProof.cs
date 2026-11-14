#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using System.Text;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.2 item 12 Play proof (rule DV138, Sim/ZBot.CpuShuttle.cs: the computer flies its shuttles), on any level in Play with a computer
    /// player (level 2 recommended; menu OpenZ/V1.2/CPU: 12 Shuttles Proof (Play), Editor/OzCpuShuttleMenu.cs). Set-up, the testing panel's
    /// own drop (OzCheatShuttle.Spawn): for the first computer player a crewed ship of its team and an EMPTY ship, both by its fort; once a
    /// minute, while its ship waits for a target, the computer's farthest territory is handed to the player's side with nobody in it (an
    /// undefended enemy territory, as the headless check does). Then it only watches, at the game's own speed, for at most
    /// <see cref="WatchSec"/> s of game time: the empty ship taken, a squad boarding, the take-off (the destination's defence 0), the
    /// landing (the landing spot rule), the squad let out and the flag captured, the two minutes before the next run; the ship's own sounds
    /// (take-off, landing) played while its crew lines and Rachel's landing line are left out (the view's CpuQuietLines). Pictures at the
    /// normal RTS camera (70 m, full screen) in <see cref="Folder"/>. Test only.
    /// </summary>
    public sealed class OzCpuShuttleProof : MonoBehaviour
    {
        public static string Report = "idle";
        public const float CamHeight = 70f;
        public const double WatchSec = 600.0;

        /// <summary></summary>
        public static string Folder()
        {
            var d = new System.IO.DirectoryInfo(Application.dataPath);
            for (int i = 0; i < 8 && d != null; i++, d = d.Parent)
            {
                var staging = System.IO.Path.Combine(d.FullName, "work", "staging");
                if (!System.IO.Directory.Exists(staging)) continue;
                foreach (var stage in System.IO.Directory.GetDirectories(staging))
                {
                    var kit = System.IO.Path.Combine(stage, "k12_cpu_shuttle");
                    if (System.IO.Directory.Exists(kit)) return System.IO.Path.Combine(kit, "proof");
                }
            }
            return System.IO.Path.Combine(Application.persistentDataPath, "k12_cpu_shuttle");
        }

        public static void Run() { Report = "running"; new GameObject("OzCpuShuttleProof").AddComponent<OzCpuShuttleProof>(); }

        ZGame g; ZWorld W; RtsCamera rts; ZBot bot; ZObject ship, empty;
        readonly StringBuilder sb = new StringBuilder(); int fails, passes; string dir;

        void Start()
        {
            g = ZGame.I; W = g ? g.World : null; rts = FindFirstObjectByType<RtsCamera>();
            dir = Folder(); System.IO.Directory.CreateDirectory(dir);
            StartCoroutine(All());
        }

        void Log(string t) { sb.AppendLine(t); Debug.Log("[OzCpuShuttleProof] " + t); }
        void Check(string what, bool ok) { if (ok) passes++; else fails++; Log((ok ? "PASS " : "FAIL ") + what); }
        IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }
        void Cam(int px, int py) { if (!rts) return; rts.FreeLookOff(); var f = ZGame.PxToWorld(px, py); rts.focus = new Vector3(f.x, 0f, f.z); rts.SetHeightImmediate(CamHeight); }
        IEnumerator Shot(string name, int px, int py) { Cam(px, py); yield return Frames(4); ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, name + ".png")); Log("   picture " + name + ".png"); yield return Frames(2); }
        IEnumerator ShotOf(string name, ZObject o) { if (o != null) yield return Shot(name, o.CenterX, o.CenterY); }
        static bool Alive(ZObject o) => o != null && !o.Destroyed && o.KillAt < 0;

        ZObject Drop(Team team, int skip)
        {
            var fort = W.Objects.FirstOrDefault(o => o.IsFort && o.Owner == bot.Team && ZWorld.StrikeGate(o, out _, out _, out _, out _, out _)) ?? W.Objects.FirstOrDefault(o => o.IsFort && o.Owner == bot.Team);
            if (fort == null) return null;
            int k = 0;
            for (int r = 9; r <= 15; r += 3)
                foreach (var (dx, dy) in new[] { (1, 1), (-1, 1), (1, -1), (-1, -1), (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    int px = fort.CenterX + dx * r * 16, py = fort.CenterY + dy * r * 16;
                    if (px < 64 || py < 64 || px > W.Map.WidthPix - 64 || py > W.Map.HeightPix - 64) continue;
                    if (!W.ShuttlePadFree(px >> 4 << 4 | 8, py >> 4 << 4 | 8, null, out _)) continue;
                    if (k++ < skip) continue;
                    var s = OzCheatShuttle.Spawn(g, null, team, new Vector2Int(px, py));
                    if (s != null) return s;
                }
            return null;
        }

        IEnumerator All()
        {
            if (W == null) { Report = "FAIL (no world)"; yield break; }
            bot = W.Bots.FirstOrDefault(b => !b.Ignored);
            Log($"1.2 item 12 Play proof, {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}, game time {W.Time:F0} s, DV138 {(ZWorld.CpuShuttleDeviation ? "on" : "OFF")}, computer {(bot != null ? bot.Team + " " + bot.Difficulty : "none")}");
            Check("the rule is on and a computer plays", ZWorld.CpuShuttleDeviation && bot != null);
            if (!ZWorld.CpuShuttleDeviation || bot == null) { Finish(); yield break; }
            ship = Drop(bot.Team, 0); empty = Drop(Team.Neutral, 1);
            Check($"the computer's ship and an EMPTY ship dropped by its fort (#{(ship != null ? ship.Id : -1)}, #{(empty != null ? empty.Id : -1)})", ship != null && empty != null);
            if (ship == null) { Finish(); yield break; }
            yield return Shot("k12_1_ships_by_the_computers_fort_70m", ship.CenterX, ship.CenterY);
            int seen = bot.CpuShuttleEvents.Count; double t0 = W.Time, nextGift = W.Time + 60.0;
            bool board = false, boarded = false, take = false, landed = false, unloaded = false, captured = false, emptyTaken = false, sawTakeOffSound = false, sawLandingSound = false, flew = false;
            int gifts = 0, threatBad = 0, landBad = 0, quiet0 = QuietLines(); double runEnd = -1, nextTake = -1, maxHover = 0, hoverSince = -1; int capFlag = -1;
            double capturedAt = -1;
            while (W.Time - t0 < WatchSec && !W.GameOver && !(captured && (nextTake >= 0 || W.Time - capturedAt > ZWorld.CpuShuttleCooldownSec * bot.EasySlowScale + 90)))
            {
                yield return null;
                bool hovering = false;
                for (int i = 0; i < OzPlayableShuttle.Count; i++)
                {   // every ship of the computer: its own sounds, a hover
                    var v = OzPlayableShuttle.At(i);
                    if (!v || v.Obj == null || v.Obj.Owner != bot.Team) continue;
                    if (v.LastSound == "shuttle_takeoff") sawTakeOffSound = true;
                    if (v.LastSound == "shuttle_landing") sawLandingSound = true;
                    if (Alive(v.Obj) && v.Obj.ShuttleState == ZShuttleState.Hover) hovering = true;
                }
                if (hovering) { if (hoverSince < 0) hoverSince = W.Time; maxHover = System.Math.Max(maxHover, W.Time - hoverSince); } else hoverSince = -1;
                if (gifts < 4 && W.Time >= nextGift && bot.CpuShuttlePhase == "Idle" && Alive(ship))
                {   // an undefended enemy territory for the run (the player's idle side takes none by itself): the computer's farthest flag nobody of the player's side is near
                    nextGift = W.Time + 60.0;
                    var give = W.Flags.Where(f => f.Owner == bot.Team && W.CpuShuttleThreat(bot.Team, f.CenterX, f.CenterY) == 0)
                                      .OrderByDescending(f => (long)(f.CenterX - ship.CenterX) * (f.CenterX - ship.CenterX) + (long)(f.CenterY - ship.CenterY) * (f.CenterY - ship.CenterY)).ThenBy(f => f.Id).FirstOrDefault();
                    if (give != null) gifts++;
                    var human = g.player;
                    if (give != null && human != bot.Team) { W.AwardZone(give, human); Log($"   {W.Time:F0} s: flag #{give.Id} handed to {human} with nobody in it"); }
                }
                for (; seen < bot.CpuShuttleEvents.Count; seen++)
                {
                    var e = bot.CpuShuttleEvents[seen];
                    Log($"   {e.time,6:F0} s {e.kind} ship #{e.ship}{(e.flag >= 0 ? " flag #" + e.flag : "")}{(e.unit >= 0 ? " unit #" + e.unit : "")} tile {e.x >> 4},{e.y >> 4}{(e.threatFlag >= 0 ? $" defence {e.threatFlag}/{e.threatSpot}" : "")}{(e.note != null ? " (" + e.note + ")" : "")}");
                    var s = W.GetAny(e.ship);
                    switch (e.kind)
                    {
                        case CpuShuttleEventKind.EmptyTaken: if (!emptyTaken) { emptyTaken = true; yield return ShotOf("k12_2_empty_ship_taken_70m", s); } break;
                        case CpuShuttleEventKind.Board: if (!board) { board = true; yield return ShotOf("k12_3_squad_walks_into_the_ship_70m", s); } break;
                        case CpuShuttleEventKind.Boarded: boarded = true; break;
                        case CpuShuttleEventKind.TakeOff:
                        case CpuShuttleEventKind.Divert:
                            if (e.threatFlag != 0 || e.threatSpot != 0) threatBad++;
                            if (e.kind == CpuShuttleEventKind.TakeOff && captured && nextTake < 0) nextTake = e.time;
                            if (!take && e.kind == CpuShuttleEventKind.TakeOff) { take = true; yield return ShotOf("k12_4_take_off_70m", s); }
                            break;
                        case CpuShuttleEventKind.Landed:
                        {
                            bool ok = s != null && W.ShuttlePadFreeAt(s.CenterX, s.CenterY, s, s.ShuttleHeadingDeg, out _);
                            if (!ok) landBad++;
                            if (!landed) { landed = true; yield return ShotOf("k12_5_landed_by_the_target_70m", s); }
                            break;
                        }
                        case CpuShuttleEventKind.Unloaded: if (!unloaded && s != null) { unloaded = true; capFlag = e.flag; var f = W.Get(e.flag); yield return Shot("k12_6_squad_out_to_the_flag_70m", f != null ? (s.CenterX + f.CenterX) / 2 : s.CenterX, f != null ? (s.CenterY + f.CenterY) / 2 : s.CenterY); } break;
                        case CpuShuttleEventKind.Captured: if (!captured) { captured = true; capturedAt = W.Time; yield return ShotOf("k12_7_flag_captured_70m", W.Get(e.flag)); } break;
                        case CpuShuttleEventKind.RunEnd: runEnd = e.time; break;
                    }
                }
                var up = W.GetAny(bot.CpuShuttleShip);
                if (!flew && take && Alive(up) && up.ShuttleAirborne && up.ShuttleLift >= 1.0) { flew = true; yield return Shot("k12_4b_in_the_air_70m", up.CenterX, up.CenterY); }
            }
            Check("the EMPTY ship taken by a computer's squad (its leader the pilot, the ship the computer's)", emptyTaken && empty != null && empty.Owner == bot.Team);
            Check("a squad boarded a ship of the computer (or the squad that took the empty one is aboard)", (board && boarded) || (emptyTaken && take));
            Check($"a run took off, every destination's defence 0 at the take-off ({threatBad} not)", take && threatBad == 0);
            Check($"it landed by the target, the landing spot rule kept ({landBad} not)", landed && landBad == 0);
            Check($"the squad came out and captured the flag #{capFlag}", unloaded && captured);
            Check($"the next run waited the cooldown: {(nextTake >= 0 && runEnd >= 0 ? (nextTake - runEnd).ToString("F0") + " s" : "no second take-off in the watch (fine)")}", nextTake < 0 || nextTake - runEnd >= ZWorld.CpuShuttleCooldownSec * bot.EasySlowScale - 0.01);
            Check($"no hover of 40 s (longest {maxHover:F0} s)", maxHover <= 40);
            int quiet = QuietLines() - quiet0;
            Check($"the ship's own sounds played (take-off {(sawTakeOffSound ? "yes" : "no")}, landing {(sawLandingSound ? "yes" : "no")}), its crew lines, Rachel's line and the error beep left out ({quiet} left out)", sawTakeOffSound && sawLandingSound && quiet > 0);
            Finish();
        }

        int QuietLines()
        {
            int n = 0;
            for (int i = 0; i < OzPlayableShuttle.Count; i++) { var v = OzPlayableShuttle.At(i); if (v) n += v.CpuQuietLines; }
            return n;
        }

        void Finish()
        {
            Report = fails == 0 ? $"PASS ({passes})" : $"FAIL ({fails} of {passes + fails})";
            Log("RESULT " + Report);
            try { System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "k12_cpu_shuttle_proof.txt"), sb.ToString()); } catch (System.Exception ex) { Debug.LogWarning("[OzCpuShuttleProof] " + ex.Message); }
            Destroy(gameObject);
        }
    }
}
#endif
