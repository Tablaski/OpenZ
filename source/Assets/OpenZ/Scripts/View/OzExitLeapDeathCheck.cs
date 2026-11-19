#if UNITY_EDITOR && !OZ_RELEASE
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.5 kit exit_leap_death: the Play check of DV179 and its look (Sim/ZObject.DriverThrown.cs, View/BoardingView.HatchThrow.cs), on a level
    /// playing (level 2, the normal mode). Menus: OpenZ/V1.5/Exit Leap Death: Play Check / last report (Editor/OzV15ExitLeapDeathMenu.cs); the
    /// report and three pictures per row (the throw's top, on the ground, standing) in Folder. The level's own enemies are quieted while it runs
    /// (their Damage 0, put back at the end); the check's own tanks are made on free ground near the camera and the EXIT is the rules' own
    /// command (ZWorld.Eject, as the EXIT DRIVER tab sends it); the hull is destroyed by the rules' own death (a fatal hit). The drivers of rows
    /// T1, T2, T4 and T5 get a huge health for the row (the turret's debris must not decide it); row T3 kills him on purpose.
    ///  T1 the light tank and the mech, the hull destroyed 0.20 s after the EXIT (the lid opening): the rules threw him (DriverThrownFromId), the
    ///     view threw his figure (a HatchThrows record from "inside" or "crouch"), no 1.4 jump at once (no somersault: the drawn turn under 120
    ///     deg), the figure's feet within 0.25 m of the ground at the landing, he lay and stood up within 0.35 s of the rules' release, alive in
    ///     the rules and in the view (his model not dying), the rules never moved him while thrown, the three sounds played (dst_ragdoll_whoosh,
    ///     rob_knockdown_land, rob_getup) and the gun-ready sound after it; no figure left on the wreck;
    ///  T2 the same at 0.35 s (his crouch on the rim);
    ///  T3 the light tank at 0.20 s, the driver killed by the rules 0.25 s into the throw's flight: his own model takes the figure's place
    ///     (HatchExitLastDieFrom "figure"), a death sound, nobody stands up;
    ///  T4 the light tank at 0.70 s (after his take-off): not thrown in the rules, no throw drawn (1.4's look);
    ///  T5 DV179 off for the row: the light tank at 0.20 s: 1.4's jump at once (the record's bailed), no throw.
    /// </summary>
    public class OzExitLeapDeathCheck : MonoBehaviour
    {
        public const string Tag = "[OpenZ V1.5 exit leap death play]";
        public static bool Running; public static string Report = "not run";
        public static string Folder;
        public static float GroundTolM = 0.25f, StandTolSec = 0.35f, NoFlipDeg = 120f;

        ZGame g; ZWorld W; Team me;
        readonly StringBuilder sb = new StringBuilder(); int pass, total;
        readonly List<ZObject> made = new List<ZObject>();
        readonly Dictionary<ZObject, int> quieted = new Dictionary<ZObject, int>();

        public static void Begin()
        {
            if (Running) { Debug.LogWarning(Tag + " already running"); return; }
            new GameObject("OZ_ExitLeapDeathCheck").AddComponent<OzExitLeapDeathCheck>();
        }

        void Log(string row, bool ok, string detail) { total++; if (ok) pass++; sb.AppendLine((ok ? "PASS  " : "FAIL  ") + row + ": " + detail); }

        IEnumerator Start()
        {
            Running = true; Report = "running";
            bool thrown0 = ZWorld.DriverThrownDeviation;
            try
            {
                g = ZGame.I ? ZGame.I : FindFirstObjectByType<ZGame>(); W = g ? g.World : null;
                if (!g || W == null || !BoardingView.I) { Report = Tag + " FAIL: no level playing"; Debug.LogWarning(Report); yield break; }
                me = OpenZHud.I ? OpenZHud.I.Player : Team.Red;
                if (!string.IsNullOrEmpty(Folder)) Directory.CreateDirectory(Folder);
                sb.AppendLine($"level {g.level}, DV171 {ZWorld.VehicleExitDeviation}, DV179 {ZWorld.DriverThrownDeviation}, DV132 {ZWorld.ExitWalkHoldOn}, window {ZObject.DriverThrownWindowTicks} ticks, thrown hold {ZObject.DriverThrownTicks} ticks");
                StartCoroutine(Peace());
                BoardingView.HatchThrowStatsReset(); BoardingView.HatchExitStatsReset();
                yield return Row("T1", "light", 0.20f, false, true);
                yield return Row("T1", ZMTank.Mech, 0.20f, false, true);
                yield return Row("T2", "light", 0.35f, false, true);
                yield return Row("T2", ZMTank.Mech, 0.35f, false, true);
                yield return Row("T3", "light", 0.20f, true, true);
                yield return Row("T4", "light", 0.70f, false, true);
                ZWorld.DriverThrownDeviation = false;
                yield return Row("T5", "light", 0.20f, false, false);
                ZWorld.DriverThrownDeviation = thrown0;
            }
            finally
            {
                ZWorld.DriverThrownDeviation = thrown0;
                foreach (var kv in quieted) if (kv.Key != null) kv.Key.Damage = kv.Value;
                foreach (var o in made) if (o != null && o.KillAt < 0 && !o.Destroyed && W != null) o.KillAt = W.Time;
                Report = $"{Tag} {(pass == total && total > 0 ? "PASS" : "FAIL")} {pass}/{total}\n{sb}";
                if (!string.IsNullOrEmpty(Folder)) { try { File.WriteAllText(Path.Combine(Folder, "exit_leap_death_play.txt"), Report); } catch (System.Exception) { } }
                Debug.Log(Report);
                Running = false;
                Destroy(gameObject);
            }
        }

        IEnumerator Peace()
        {
            while (Running && W != null)
            {
                foreach (var o in W.Objects) if (o.Owner != me && o.Owner != Team.Neutral && !o.Destroyed && o.Damage > 0 && !made.Contains(o) && !quieted.ContainsKey(o)) { quieted[o] = o.Damage; o.Damage = 0; }
                yield return new WaitForSecondsRealtime(0.5f);
            }
        }

        /// <summary>A free 6 x 6-tile block near the camera's focus (robot-passable, no building or unit on it); false: none within 40 tiles.</summary>
        bool FreeBlock(out int bx, out int by)
        {
            var rts = FindFirstObjectByType<RtsCamera>();
            var c = ZGame.WorldToPx(rts ? rts.focus : Vector3.zero); int fx = c.x >> 4, fy = c.y >> 4;
            for (int rad = 0; rad <= 40; rad += 2)
                for (int dy = -rad; dy <= rad; dy += 2)
                    for (int dx = -rad; dx <= rad; dx += 2)
                    {
                        if (System.Math.Max(System.Math.Abs(dx), System.Math.Abs(dy)) != rad) continue;
                        bx = fx + dx - 3; by = fy + dy - 3;
                        if (bx < 1 || by < 1 || (bx + 7) * 16 > W.Path.WidthPix || (by + 7) * 16 > W.Path.HeightPix) continue;
                        if (W.Path.WithinImpassable(bx * 16, by * 16, 96, 96, out _, out _, true)) continue;
                        int x0 = bx * 16, y0 = by * 16; bool busy = false;
                        foreach (var o in W.Objects)
                        {
                            if (o.Destroyed || o.KillAt >= 0) continue;
                            if (o.LocX < x0 + 96 + 16 && o.LocX + o.WidthPix > x0 - 16 && o.LocY < y0 + 96 + 16 && o.LocY + o.HeightPix > y0 - 16) { busy = true; break; }
                        }
                        if (!busy) return true;
                    }
            bx = by = -1; return false;
        }

        static IEnumerator Real(float s) { yield return new WaitForSecondsRealtime(s); }

        void Shot(string name)
        {
            if (string.IsNullOrEmpty(Folder)) return;
            try { ScreenCapture.CaptureScreenshot(Path.Combine(Folder, name)); } catch (System.Exception) { }
        }

        IEnumerator Row(string row, string type, float killAt, bool killDriver, bool expectThrow)
        {
            string name = $"{row} {(type == "light" ? "light tank" : "mech")} destroyed {killAt:0.00} s after the EXIT";
            if (!FreeBlock(out int bx, out int by)) { Log(name, false, "no free ground near the camera"); yield break; }
            var rts = FindFirstObjectByType<RtsCamera>();
            if (rts) { rts.FlyTo(ZGame.PxToWorld((bx + 3) * 16, (by + 3) * 16)); yield return Real(1.6f); }
            var v = W.CreateObject("vehicle", type, (bx + 2) * 16, (by + 2) * 16, me);
            g.SpawnUnitView(v); made.Add(v);
            yield return Real(1.2f);
            if (!v.CanExitDriver(out string why)) { Log(name, false, "EXIT not possible here: " + why); yield break; }
            float t0 = Time.unscaledTime;
            if (!W.Eject(me, v)) { Log(name, false, "the EXIT was refused"); yield break; }
            double exitSim = W.Time;
            ZObject r = null; bool killed = false, driverKilled = false; double throwSimAt = -1, thrownPx = double.NaN, thrownPy = 0, simMovedWhileThrown = 0;
            float maxTurn = 0f; Vector3 lastUp = Vector3.up; bool upInit = false; bool shotTop = false, shotDown = false, shotUp = false;
            float until = Time.unscaledTime + 9f;
            while (Time.unscaledTime < until)
            {
                yield return new WaitForEndOfFrame();
                if (r == null) foreach (var o in W.Objects) if (o.Kind == ZKind.Robot && o.DriverOutFromId == v.Id) { r = o; made.Add(o); if (!killDriver) { o.MaxHealth = o.Health = 1000000000; } break; }
                if (!killed && W.Time - exitSim >= killAt) { killed = true; v.SetHealth(0); W.UpdateObjectHealth(v); }
                if (r == null) continue;
                if (r.DriverThrownHeld) { if (double.IsNaN(thrownPx)) { thrownPx = r.PosX; thrownPy = r.PosY; } simMovedWhileThrown = System.Math.Max(simMovedWhileThrown, System.Math.Sqrt((r.PosX - thrownPx) * (r.PosX - thrownPx) + (r.PosY - thrownPy) * (r.PosY - thrownPy))); }
                var go = BoardingView.HatchFigureGo(r.Id, out string phase);
                if (go && go.activeInHierarchy && killed)
                {
                    var up = go.transform.up;
                    if (upInit) maxTurn += Vector3.Angle(lastUp, up);
                    lastUp = up; upInit = true;
                }
                BoardingView.HatchThrows.TryGetValue(r.Id, out var tr);
                if (tr != null && throwSimAt < 0) throwSimAt = W.Time;
                if (killDriver && tr != null && !driverKilled && W.Time - throwSimAt >= 0.25) { driverKilled = true; r.SetHealth(0); W.UpdateObjectHealth(r); }
                if (tr != null && !shotTop && !tr.landed && W.Time - throwSimAt >= tr.flight * 0.45f) { shotTop = true; Shot($"{row}_{type}_{killAt:0.00}_1_throw.png"); }
                if (tr != null && !shotDown && tr.landed && !tr.getup) { shotDown = true; Shot($"{row}_{type}_{killAt:0.00}_2_down.png"); }
                if (tr != null && !shotUp && tr.stood) { shotUp = true; Shot($"{row}_{type}_{killAt:0.00}_3_up.png"); }
                if (killed && tr != null && (tr.stood || tr.dead) && Time.unscaledTime - t0 > 4.5f) break;
                if (killed && tr == null && Time.unscaledTime - t0 > 4f && go == null) break;
            }
            // review fix: is read once his walk on has had its time (it hands over on his robot); 0.5 s after the stand was a race
            float handBy = Time.unscaledTime + 4f;
            while (r != null && Time.unscaledTime < handBy && BoardingView.HatchFigureNow(r.Id, out _, out _, out _, out _) != 0) yield return null;
            yield return Real(0.5f);
            BoardingView.HatchThrows.TryGetValue(r != null ? r.Id : -1, out var rec);
            BoardingView.HatchJumpRec jr = null; if (r != null) BoardingView.HatchJumps.TryGetValue(r.Id, out jr);
            bool simThrew = r != null && r.DriverThrownFromId == v.Id;
            var log = OzSoundLog.Since(t0);
            bool Heard(string key) => log.Any(e => e.key == key && !(e.outcome ?? "").StartsWith("DROP") && !(e.outcome ?? "").Contains("dropped") && !(e.outcome ?? "").Contains("skipped"));
            bool aliveRules = r != null && !r.Destroyed && r.KillAt < 0;
            g.Views.TryGetValue(r != null ? r.Id : -1, out var uv);
            bool aliveView = uv && !uv.Dying;
            bool figureLeft = r != null && BoardingView.HatchFigureNow(r.Id, out _, out _, out _, out _) != 0;
            if (row == "T1" || row == "T2")
            {
                bool feet = rec != null && !float.IsNaN(rec.feetAboveAtLand) && Mathf.Abs(rec.feetAboveAtLand) <= GroundTolM;
                bool stoodOk = rec != null && rec.stood && !float.IsNaN(rec.standLateSec) && Mathf.Abs(rec.standLateSec) <= StandTolSec;
                bool sounds = Heard("dst_ragdoll_whoosh") && Heard("rob_knockdown_land") && Heard("rob_getup");
                bool ok = simThrew && rec != null && maxTurn < NoFlipDeg && feet && stoodOk && aliveRules && aliveView && simMovedWhileThrown < 0.01 && sounds && !figureLeft;
                Log(name, ok, $"the rules threw him {simThrew}; the view's throw {(rec != null ? $"from {rec.from}, {rec.flight:0.00} s flight, {rec.landM:0.00} m out, top {rec.apexM:0.00} m, {rec.lieSec:0.00} s down, {rec.upSec:0.00} s up" : "NONE")}; "
                    + $"turned {maxTurn:0} deg in all (a somersault is 360+); feet at the landing {(rec != null ? rec.feetAboveAtLand.ToString("0.00") : "-")} m; stood {(rec != null ? rec.standLateSec.ToString("+0.00;-0.00") : "-")} s against the rules' release; "
                    + $"alive in the rules {aliveRules} and the view {aliveView}; the rules moved him {simMovedWhileThrown:0.00} px while thrown; sounds whoosh / thud / get-up {Heard("dst_ragdoll_whoosh")} / {Heard("rob_knockdown_land")} / {Heard("rob_getup")}; "
                    + $"gun-ready {Heard("wpn_sniper_bolt")}; a figure left {figureLeft}");
            }
            else if (row == "T3")
            {
                bool fromFigure = BoardingView.HatchExitLastDieFrom.StartsWith("figure");
                bool death = log.Any(e => e.key != null && (e.key.Contains("death") || e.key.Contains("die") || e.key.StartsWith("rob_pop")));
                bool ok = simThrew && rec != null && driverKilled && !aliveRules && fromFigure && death && (rec == null || !rec.stood);
                Log(name + ", the driver killed in the throw", ok, $"the rules threw him {simThrew}; killed {driverKilled} 0.25 s into the throw; his model shown from \"{BoardingView.HatchExitLastDieFrom}\"; a death sound {death}; stood up {(rec != null && rec.stood)} (must not)");
            }
            else if (row == "T4")
            {
                bool ok = !simThrew && rec == null && aliveRules;
                Log(name + " (after his take-off)", ok, $"the rules threw him {simThrew} (must not); a throw drawn {rec != null} (must not); alive {aliveRules}");
            }
            else
            {
                bool ok = !simThrew && rec == null && jr != null && jr.bailed;
                Log(name + " with DV179 off", ok, $"the rules threw him {simThrew} (must not); a throw drawn {rec != null} (must not); 1.4's jump at once {(jr != null && jr.bailed)}");
            }
        }
    }
}
#endif
