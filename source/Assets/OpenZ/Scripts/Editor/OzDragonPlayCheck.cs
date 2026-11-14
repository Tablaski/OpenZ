#if !OPENZ_HARNESS
// OpenZ (GPL-3.0)
//   OpenZ/L07 Lava/Checks/Dragons/1 Fire on robots ...
//   .../2 Robots shoot back and kill                         "robots can shot it back and kill"
//   .../3 Not in the first minute ...                        "not in the first minute ... only if on screen and if dragon accidently gets close, not on purpose"
//   .../4 Never through a shuttle, can attack it             "ensure dragons dont overlap shattle but can attack it also"
//   .../5 No trace on other levels                           the job's rule: level 7 only
//   .../6 True attack rate                                   "sometimes": a whole match as it is played, at the sim's true pace, nothing planted
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenZ.Sim;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    public static class OzDragonPlayCheck
    {
        static IEnumerator run; static string tag; static int pass, n; static float t0; static readonly List<string> lines = new List<string>();

        static void Line(bool ok, string s) { n++; if (ok) pass++; lines.Add((ok ? "[" + tag + "] ok   " : "[" + tag + "] FAIL ") + s); }
        static int Played(string key) => OzSoundLog.Since(t0).Count(e => e.key == key && (e.outcome == null || !e.outcome.StartsWith("DROP")));
        static void Cam(Vector3 at, float h) { var rts = Object.FindFirstObjectByType<RtsCamera>(); if (rts) { rts.focus = new Vector3(at.x, 0f, at.z); rts.SetHeightImmediate(h); } }
        static string Shot(int i) => Path.Combine(OpenZPaths.Records, $"l07_dragon_{tag.Substring(tag.Length - 1)}_{i:00}.png");

        static bool Ready(bool l07)
        {
            var g = ZGame.I;
            if (!Application.isPlaying || !g || g.World == null || !AmbientLife.I) { Debug.LogError("[DRAGON] FAIL enter Play on a level first"); return false; }
            if (l07 && !g.World.Map.HasLava) { Debug.LogError("[DRAGON] FAIL this one runs on level 7"); return false; }
            return true;
        }

        static void Start(string t, IEnumerator r)
        {
            tag = t; pass = n = 0; lines.Clear(); t0 = Time.unscaledTime; run = r;
            OzDragonProof.Reset();
            EditorApplication.update -= Pump; EditorApplication.update += Pump;
        }

        static void Stop()
        {
            EditorApplication.update -= Pump;
            OzDragonProof.Recording = false; OzDragonProof.NoAsks = false;
            var g = ZGame.I;
            if (g) { g.gameSpeed = 1f; if (g.World != null) g.World.DragonProofForce = false; }
        }

        static void Pump()
        {
            if (!Application.isPlaying) { Stop(); Debug.LogError("[" + tag + "] FAIL Play ended during the check"); return; }
            bool more;
            try { more = run.MoveNext(); }
            catch (System.Exception ex) { Stop(); Debug.LogError("[" + tag + "] FAIL the check threw: " + ex); return; }
            if (more) return;
            Stop();
            var path = Path.Combine(OpenZPaths.Records, $"l07_dragon_{tag.Substring(tag.Length - 1)}_sound_log.txt");
            File.WriteAllText(path, OzSoundLog.Format(OzSoundLog.Since(t0)));
            lines.Add($"[{tag}] sound log {path}");
            lines.Add($"[{tag}] {(pass == n ? "PASS" : "FAIL")} {pass}/{n}");
            Debug.Log(string.Join("\n", lines));
        }

        /// <summary>The fastest game speed at which the dragons keep the sim's pace: AmbientLife clamps its time to 2. A check that needs true flight runs at this speed, never above.</summary>
        const float TrueFlightSpeed = 2f;

        static IEnumerator Past60(ZGame g)
        {
            g.gameSpeed = 4f;
            while (g.World.Time < ZWorld.DragonFirstSeconds + 1) yield return null;
            g.gameSpeed = 1f;
        }
        static IEnumerator Wait(float s) { float end = Time.unscaledTime + s; while (Time.unscaledTime < end) yield return null; }

        static ZObject Robot(ZGame g, string type, int x, int y)
        {
            var o = g.World.CreateRobotGroup(type, x - 8, y - 8, g.player, 1);
            g.SpawnUnitView(o); g.World.SendHold(g.player, o);
            return o;
        }

        // ------------------------------------------------------------------ 1
        [MenuItem("OpenZ/L07 Lava/Checks/Dragons/1 Fire on robots, not vehicles, top to bottom (Play, level 7)")]
        public static void One() { if (Ready(true)) Start("DRAGON1", OneRun()); }

        static IEnumerator OneRun()
        {
            var g = ZGame.I; var w = g.World; var life = AmbientLife.I;
            yield return Past60(g);
            OzDragonProof.NoAsks = true; OzDragonProof.Recording = true; w.DragonProofForce = true;
            var (x, y) = OzDragonCount.Open(w);
            var r = Robot(g, "grunt", x, y); var v = w.CreateObject("vehicle", "jeep", x + 72, y + 8, g.player); g.SpawnUnitView(v);
            var around = g.World.CreateObject("vehicle", "light", x - 88, y + 8, g.player); g.SpawnUnitView(around);
            Cam(ZGame.PxToWorld(x, y), 42f);
            yield return Wait(1f);
            int a = life.ProofAskOnly(v), a2 = life.ProofAskOnly(around);
            Line(a == ZWorld.WhyTarget && a2 == ZWorld.WhyTarget, $"a jeep and a light tank as targets: {ZWorld.DragonWhyName[a]}, {ZWorld.DragonWhyName[a2]}");
            int hp0 = r.Health, vh0 = v.Health, th0 = around.Health, wp0 = r.Waypoints.Count;
            int ans = life.ProofPass(r, 30f);
            Line(ans == 0, $"the robot as target: {(ans >= 0 ? ZWorld.DragonWhyName[ans] : "no clear path")}");
            int jets = 0, sprites = 0, burn = 0, frames = 0; float nextShot = Time.unscaledTime;
            var fire = OpenZPyroFire.I;
            while (w.DragonPass != null)
            {
                if (fire) { jets = Mathf.Max(jets, fire.Jets); sprites = Mathf.Max(sprites, fire.Sprites); burn = Mathf.Max(burn, fire.Burning); }
                if (Time.unscaledTime >= nextShot && frames < 12) { nextShot += 0.55f; ScreenCapture.CaptureScreenshot(Shot(frames++)); }
                yield return null;
            }
            yield return Wait(1f);
            var bs = OzDragonProof.Bites;
            Line(jets >= 1 && sprites >= 30, $"the Pyro's jet: {jets} jet(s), {sprites} sprites at most");
            Line(burn >= 1, $"sticky fire on what it hit: {burn} burning at most");
            Line(bs.Count >= 7 && bs.Count <= ZWorld.DragonBites, $"{bs.Count} flame beats, {bs.Count(b => b.hit)} landed");
            float wide = bs.Where(b => b.hit).Select(b => b.deg).DefaultIfEmpty(180f).Max();
            Line(wide <= 45f, $"top to bottom: the widest bite that landed, drawn mouth to end, {wide:F1} degrees from straight down (45 at most)");
            float low = bs.Select(b => b.mouthUp).DefaultIfEmpty(0f).Min(), top = bs.Select(b => b.endUp).DefaultIfEmpty(99f).Max();
            Line(low >= 6f && top <= 3f, $"from above: mouth at least {low:F1} m up, the flame ends at most {top:F1} m over the ground");
            Line(r.Health < hp0 && !r.Destroyed && r.Health >= r.MaxHealth * 0.24f, $"a healthy robot lives: {100f * r.Health / r.MaxHealth:F1} % left (the flame takes 59 % at most, with the burning ground under it 76 %)");
            Line(v.Health == vh0 && around.Health == th0, $"the vehicles beside it took {vh0 - v.Health} and {th0 - around.Health} HP");
            Line(r.Waypoints.Count == wp0, "the robot keeps its orders");
            Line(OzDragonScream.Ids.Sum(sc => Played(sc)) >= 1, $"its scream at the start (1.4 ptero_scream: one of the owner's three; the call no longer plays at an attack): {OzDragonScream.Ids.Sum(sc => Played(sc))}");
            Line(Played("wpn_pyro_flame_start") >= 1 && Played("wpn_pyro_flame_stop") >= 1, $"the flame's sounds: start {Played("wpn_pyro_flame_start")}, stop {Played("wpn_pyro_flame_stop")}");
            Line(frames >= 8, $"{frames} frames written for the crop check, by eye against a Pyro's flame: {OpenZPaths.Records}");
            // the pause menu's two slow speeds: the jet stays lit between the beats, its start and stop sound once
            foreach (float speed in new[] { 0.25f, 0.5f })
            {
                r.SetHealth(r.MaxHealth);
                int start0 = Played("wpn_pyro_flame_start"), stop0 = Played("wpn_pyro_flame_stop");
                g.gameSpeed = speed;
                int slow = life.ProofPass(r, 30f);
                int gaps = 0; bool was = false;
                while (w.DragonPass != null)
                {
                    bool on = fire && fire.Jets > 0;
                    if (was && !on && !w.DragonPass.FlameOut) gaps++;
                    was = on;
                    yield return null;
                }
                g.gameSpeed = 1f;
                yield return Wait(1f);
                int starts = Played("wpn_pyro_flame_start") - start0, stops = Played("wpn_pyro_flame_stop") - stop0;
                Line(slow == 0 && gaps == 0 && starts == 1 && stops == 1, $"at speed {speed}: the flame went out {gaps} times between its beats, start sound {starts}, stop sound {stops}");
            }
        }

        // ------------------------------------------------------------------ 2
        [MenuItem("OpenZ/L07 Lava/Checks/Dragons/2 Robots shoot back and kill (Play, level 7)")]
        public static void Two() { if (Ready(true)) Start("DRAGON2", TwoRun()); }

        static IEnumerator TwoRun()
        {
            var g = ZGame.I; var w = g.World; var life = AmbientLife.I;
            yield return Past60(g);
            OzDragonProof.NoAsks = true; w.DragonProofForce = true;
            var (x, y) = OzDragonCount.Open(w);
            string[] mix = { "grunt", "psycho", "laser", "pyro", "tough", "sniper", "electrobot" };
            var rs = new List<ZObject>();
            for (int i = 0; i < mix.Length; i++) rs.Add(Robot(g, mix[i], x + 20 * (i - 3), y + (i % 2 == 0 ? 24 : -24)));
            Cam(ZGame.PxToWorld(x, y), 42f);
            yield return Wait(1f);
            int alive0 = life.ProofAlive(), credit0 = rs.Sum(o => o.Kills);
            var hit0 = rs.Select(o => o.LastEnemyHit).ToArray();
            int tries = 0, frames = 0, aimers = 0; float nextShot = Time.unscaledTime;
            while (w.DragonKills == 0 && tries < 5)
            {
                tries++;
                int ans = life.ProofPass(rs[tries % rs.Count], 45f * tries);
                if (ans != 0) { Line(false, $"pass {tries}: {(ans >= 0 ? ZWorld.DragonWhyName[ans] : "no clear path")}"); yield break; }
                while (w.DragonPass != null)
                {
                    aimers = Mathf.Max(aimers, rs.Count(o => o.DragonAiming));
                    if (Time.unscaledTime >= nextShot && frames < 12) { nextShot += 0.45f; ScreenCapture.CaptureScreenshot(Shot(frames++)); }
                    yield return null;
                }
                yield return Wait(0.3f);
                foreach (var o in rs) o.SetHealth(o.MaxHealth);
            }
            Line(w.DragonKills >= 1, $"robots killed the dragon on pass {tries} of at most 5: {w.DragonShotsFired} shots, {w.DragonShotsHit} hits");
            Line(aimers >= 3, $"up to {aimers} robots aimed at it at once");
            foreach (var ty in mix)
            {
                OzDragonProof.Shots.TryGetValue(ty, out int s); OzDragonProof.Undrawn.TryGetValue(ty, out int u);
                Line(s >= 1 && u == 0, $"{ty}: {s} shots drawn with its own weapon, {u} not drawn");
            }
            int eBolts = OpenZElectroFx.I ? OpenZElectroFx.I.Bolts : 0;
            Line((eBolts >= 1 && Played("wpn_electro_fire") >= 1 || w.DragonKills >= 1) && Played("wpn_electro_charge") >= 1, $"the Electrobot's bolt into the air: {eBolts} bolts, charge {Played("wpn_electro_charge")}, fire {Played("wpn_electro_fire")}");
            Line(life.ProofCount("fall") >= 1 && life.ProofAlive() == alive0 - 1, $"it falls: {life.ProofCount("fall")} down, {life.ProofAlive()} of {alive0} flying");
            Line(Played("critter_dragon_hit") >= 1, $"the hit's sound: {Played("critter_dragon_hit")}");
            yield return Wait(7f);
            Line(Played("critter_bird_thud") + Played("lava_melt") >= 1, $"it lands: thud {Played("critter_bird_thud")}, lava {Played("lava_melt")}");
            Line(rs.All(o => o.AttackObject == null) && rs.All(o => o.Waypoints.Count == 0), "nobody took it as a target or was pulled off a place");
            Line(rs.Sum(o => o.Kills) == credit0, "no kill credit: a dragon is no unit");
            Line(rs.Select(o => o.LastEnemyHit).SequenceEqual(hit0), "no under-attack mark on any robot");
            Line(frames >= 6, $"{frames} frames written for the crop check (steep rifles: {OpenZPaths.Records})");
        }

        // ------------------------------------------------------------------ 3
        [MenuItem("OpenZ/L07 Lava/Checks/Dragons/3 Not in the first minute, only on screen, only by chance (Play, fresh match on level 7)")]
        public static void Three() { if (Ready(true)) Start("DRAGON3", ThreeRun()); }

        static IEnumerator ThreeRun()
        {
            var g = ZGame.I; var w = g.World; var life = AmbientLife.I;
            if (w.Time > 45) { Line(false, $"start this one in a fresh match, the clock is at {w.Time:F0} s"); yield break; }
            foreach (var b in w.Bots) b.Ignored = true;                                                    // a lone match ends with the computer's win: it holds its fire for this count
            var one = w.Objects.First(o => o.Kind == ZKind.Robot && o.Owner == g.player && !o.Destroyed);
            int early = life.ProofAskOnly(one);
            Line(early == ZWorld.WhyEarly, $"an order at {w.Time:F1} s: {ZWorld.DragonWhyName[early]}");
            // grunts round the dragons' circles, standing: they will be met
            Vector2[] rings = { new Vector2(94f, -81f), new Vector2(69f, -107f), new Vector2(92f, -139f), new Vector2(105f, -109f) };
            foreach (var c in rings)
            {
                int put = 0;
                for (int k = 0; k < 16 && put < 4; k++)
                {
                    float an = k * 22.5f * Mathf.Deg2Rad, rr = 11f + 3f * (k % 3);
                    int px = (int)((c.x + Mathf.Cos(an) * rr) * 8f), py = (int)(-(c.y + Mathf.Sin(an) * rr) * 8f);
                    if (w.Path.TilePassable(px >> 4, py >> 4, true) && !w.Map.LavaAt(px >> 4, py >> 4)) { Robot(g, "grunt", (px >> 4) * 16 + 8, (py >> 4) * 16 + 8); put++; }
                }
            }
            g.gameSpeed = TrueFlightSpeed;
            while (w.Time < ZWorld.DragonFirstSeconds - 0.5) { Cam(life.ProofPos(), 55f); yield return null; }
            Line(OzDragonProof.Orders.Count == 0 && w.DragonOrders == 0, $"first minute over, the camera on a dragon all the way: {w.DragonOrders} attacks");
            // 2 minutes with the camera in an empty corner: nothing off screen happens
            Cam(ZGame.PxToWorld(100, 100), 40f);
            double until = w.Time + 120;
            while (w.Time < until) yield return null;
            Line(w.DragonOrders == 0 && OzDragonProof.OffScreen >= 1, $"camera away for 120 s: {OzDragonProof.Encounters} robots met on its line, {OzDragonProof.OffScreen} left alone as off screen, {w.DragonOrders} attacks");
            // then on a dragon, up to 12 minutes, until 3 attacks: enough to read their shape; how often they come in a real match is check 6
            OzDragonProof.Recording = true;
            int off0 = OzDragonProof.OffScreen; until = w.Time + 720; double from = w.Time;
            while (w.Time < until && w.DragonOrders < 3) { Cam(life.ProofPos(), 55f); yield return null; }
            g.gameSpeed = 1f;
            double span = w.Time - from;
            var os = OzDragonProof.Orders.ToList();
            Line(os.Count >= 1, $"staged, not the true rate (16 grunts planted, camera on a dragon): {os.Count} attacks in {span:F0} s. The true rate is check 6");
            Line(os.All(o => o.t >= ZWorld.DragonFirstSeconds), "none in the first minute");
            bool In(Vector3 v) => v.z > 0f && v.x > 0f && v.x < 1f && v.y > 0f && v.y < 1f;
            Line(os.All(o => In(o.targetVp) && In(o.fireVp)), "each with the target and the fire point on screen");
            Line(os.All(o => Mathf.Abs(o.turnDeg) <= 1.5f), $"none turned toward anyone: the fire point on its own line, the widest {os.Select(o => Mathf.Abs(o.turnDeg)).DefaultIfEmpty(0f).Max():F2} degrees");
            Line(os.All(o => o.along >= 14f && o.along <= 28f && o.side <= 7f), $"each a robot that happened to be 14 to 28 m ahead, at most 7 m to the side: {string.Join(", ", os.Select(o => $"{o.along:F0} + {o.side:F1}"))}");
            Line(w.DragonRefused[ZWorld.WhyRoll] + w.DragonRefused[ZWorld.WhyOwnCool] + OzDragonProof.Encounters - os.Count > 0, $"sometimes, not always: {OzDragonProof.Encounters} robots met, {os.Count} attacks, {w.DragonRefused[ZWorld.WhyRoll]} lost the chance");
            Line(os.All(o => o.type != "jeep" && o.type != "light"), "only robots");
        }

        // ------------------------------------------------------------------ 4
        [MenuItem("OpenZ/L07 Lava/Checks/Dragons/4 Never through a shuttle, can attack it (Play, level 7)")]
        public static void Four() { if (Ready(true)) Start("DRAGON4", FourRun()); }

        static IEnumerator FourRun()
        {
            var g = ZGame.I; var w = g.World; var life = AmbientLife.I;
            yield return Past60(g);
            OzDragonProof.NoAsks = true; OzDragonProof.Recording = true; w.DragonProofForce = true;
            var (x, y) = OzDragonCount.Open(w);
            var s = w.SpawnShuttle(x, y, g.player, 0f, out string why);
            Line(s != null, $"the testing panel's shuttle on level 7: {(s != null ? "made" : why)}");
            if (s == null) yield break;
            OzPlayableShuttle.SpawnView(g, s);
            Cam(ZGame.PxToWorld(x, y), 60f);
            yield return Wait(1f);
            int landed = life.ProofAskOnly(s);
            Line(landed == ZWorld.WhyTarget, $"on the ground it is a vehicle: {ZWorld.DragonWhyName[landed]}");
            w.SendWaypoints(g.player, s, new[] { ZWaypoint.Move(x + 320, y) });
            double end = w.Time + 40;
            while (w.Time < end && !(s.ShuttleAirborne && s.ShuttleHeightM >= 11.9 && !s.IsMoving)) { Cam(ZGame.PxToWorld(s.CenterX, s.CenterY), 60f); yield return null; }
            Line(s.ShuttleHeightM >= 11.9, $"it flies: {s.ShuttleHeightM:F1} m");
            int hp0 = s.Health, patches0 = OpenZPyroFire.I ? OpenZPyroFire.I.Patches : 0, frames = 0;
            int ans = life.ProofPass(s, 90f);
            Line(ans == 0, $"a pass at the flying ship: {(ans >= 0 ? ZWorld.DragonWhyName[ans] : "no clear path")}");
            float nextShot = Time.unscaledTime;
            while (w.DragonPass != null)
            {
                if (Time.unscaledTime >= nextShot && frames < 12) { nextShot += 0.55f; ScreenCapture.CaptureScreenshot(Shot(frames++)); }
                yield return null;
            }
            float took = (hp0 - s.Health) / (float)s.MaxHealth;
            Line(w.DragonShipBites >= 1 && took <= 0.181f, $"the flame reached it: {w.DragonShipBites} bites, {took * 100f:F1} % of its hit points (18 at most)");
            var bs = OzDragonProof.Bites;
            float wide = bs.Where(b => b.hit).Select(b => b.deg).DefaultIfEmpty(180f).Max();
            Line(wide <= 45f, $"down onto the hull: the widest {wide:F1} degrees from straight down");
            Line(!OpenZPyroFire.I || OpenZPyroFire.I.Patches <= patches0, "nothing burning on the ground under it");
            // the two ways in that the first independent check found, staged: a flying ship runs into a dip, a plain swoop goes at a ship
            var r = Robot(g, "grunt", x, y);
            double FlatM(ZObject o) { double dx = (s.CenterX - o.CenterX) / 8.0, dy = (s.CenterY - o.CenterY) / 8.0; return System.Math.Sqrt(dx * dx + dy * dy); }
            void Follow() => Cam(ZGame.PxToWorld(s.CenterX, s.CenterY), 60f);
            w.SendWaypoints(g.player, s, new[] { ZWaypoint.Move(x - 320, y) });
            double limit = w.Time + 20;
            while (w.Time < limit && FlatM(r) > 38.0) { Follow(); yield return null; }
            int brk0 = w.DragonBreakOffs, dip = life.ProofPass(r, 0f);
            Line(dip == 0, $"a dip across the ship's way, {FlatM(r):F0} m from it: {(dip >= 0 ? ZWorld.DragonWhyName[dip] : "no clear path")}");
            while (w.DragonPass != null) { Follow(); yield return null; }
            Line(w.DragonBreakOffs == brk0 + 1, $"the ship flew into the dip: {w.DragonBreakOffs - brk0} break-off");
            limit = w.Time + 25;
            while (w.Time < limit && (s.IsMoving || s.ShuttleHeightM < 11.9)) { Follow(); yield return null; }
            int away0 = OzDragonProof.ClimbAways;
            bool swoop1 = life.ProofSwoop(ZGame.PxToWorld(s.CenterX, s.CenterY), 90f);
            for (double end2 = w.Time + 7; w.Time < end2;) { Follow(); yield return null; }
            w.SendWaypoints(g.player, s, new[] { ZWaypoint.Move(x + 320, y) });
            for (double end2 = w.Time + 1.5; w.Time < end2;) { Follow(); yield return null; }
            bool swoop2 = life.ProofSwoop(ZGame.PxToWorld(s.CenterX + 224, s.CenterY), 0f);
            for (double end2 = w.Time + 7; w.Time < end2;) { Follow(); yield return null; }
            Line(swoop1 && swoop2 && OzDragonProof.ClimbAways >= away0 + 2, $"plain swoops at a hovering and a flying ship, the start guard taken away: {OzDragonProof.ClimbAways - away0} left their path; smallest gap so far {OzDragonProof.MinGap:F1} m");
            // a minute under the dragons' circles, on their own, with the chance and the cooldowns back
            w.DragonProofForce = false; OzDragonProof.NoAsks = false;
            Vector2[] ways = { new Vector2(94f, -81f), new Vector2(69f, -107f), new Vector2(92f, -139f), new Vector2(105f, -109f) };
            for (int i = 0; i < 12; i++)
            {
                var c = ways[i % ways.Length];
                w.SendWaypoints(g.player, s, new[] { ZWaypoint.Move((int)(c.x * 8f), (int)(-c.y * 8f)) });
                double next = w.Time + 7;
                while (w.Time < next) { Cam(ZGame.PxToWorld(s.CenterX, s.CenterY), 60f); yield return null; }
            }
            Line(OzDragonProof.MinGap > 0f, $"never overlapped, staged and on their own: smallest gap from a dragon, wings spread, to the hull {OzDragonProof.MinGap:F1} m; the shield's hard edge held it {OzDragonProof.ShieldHeld} frames");
            Line(frames >= 6, $"{frames} frames written for the crop check: {OpenZPaths.Records}");
        }

        // ------------------------------------------------------------------ 5
        [MenuItem("OpenZ/L07 Lava/Checks/Dragons/5 No trace on other levels (Play, on L02 L16 L17)")]
        public static void Five() { if (Ready(false)) Start("DRAGON5", FiveRun()); }

        static IEnumerator FiveRun()
        {
            var g = ZGame.I; var w = g.World; var life = AmbientLife.I;
            Line(!w.Map.HasLava, $"{g.level} has no lava");
            yield return Wait(3f);
            Line(life.ProofAlive() == 0, $"{life.ProofAlive()} dragons");
            Line(w.DragonAsked == 0 && OzDragonProof.Encounters == 0, $"{w.DragonAsked} orders, {OzDragonProof.Encounters} robots met");
            var one = w.Objects.FirstOrDefault(o => o.Kind == ZKind.Robot && !o.Destroyed);
            if (one != null) { int a = life.ProofAskOnly(one); Line(!ZWorld.DragonDeviation || a == ZWorld.WhyNoLava, $"an order forced through: {ZWorld.DragonWhyName[a]}"); }
        }

        // ------------------------------------------------------------------ 6
        [MenuItem("OpenZ/L07 Lava/Checks/Dragons/6 True attack rate (Play, fresh match on level 7, about 8 minutes)")]
        public static void Six() { if (Ready(true)) Start("DRAGON6", SixRun()); }

        /// <summary>How long it watches a match after the first minute (game s), the shortest stretch that counts, and what "sometimes" means: one attack in this many seconds of play.</summary>
        const double RateSeconds = 900, RateShortest = 300, RateMost = 90, RateLeast = 420;
        static double nextWatch;

        /// <summary>The camera as a player's: every 15 s of game time on his robot nearest an enemy, at the height he has.</summary>
        static void Watch(ZGame g, ZWorld w)
        {
            if (w.Time < nextWatch) return;
            nextWatch = w.Time + 15;
            ZObject best = null; long bd = long.MaxValue;
            foreach (var o in w.Objects)
            {
                if (o.Kind != ZKind.Robot || o.Owner != g.player || o.Destroyed || o.KillAt >= 0) continue;
                foreach (var e in w.Objects)
                {
                    if ((e.Kind != ZKind.Robot && e.Kind != ZKind.Vehicle) || e.Owner == g.player || e.Owner == Team.Neutral || e.Destroyed) continue;
                    long dx = o.CenterX - e.CenterX, dy = o.CenterY - e.CenterY, d = dx * dx + dy * dy;
                    if (d < bd) { bd = d; best = o; }
                }
            }
            var rts = Object.FindFirstObjectByType<RtsCamera>();
            if (best != null && rts) Cam(ZGame.PxToWorld(best.CenterX, best.CenterY), rts.height);
        }

        static IEnumerator SixRun()
        {
            var g = ZGame.I; var w = g.World; var life = AmbientLife.I;
            if (w.Time > 45) { Line(false, $"start this one in a fresh match, the clock is at {w.Time:F0} s"); yield break; }
            // nothing planted: the level's own units. The computer plays the player's side too, so squads walk and fight as in a match
            if (w.Bots.All(b => b.Team != g.player)) w.Bots.Add(new ZBot(w, g.player, g.seed + 202) { Difficulty = ZBotDifficulty.Normal });
            nextWatch = 0; g.gameSpeed = TrueFlightSpeed;
            while (w.Time < ZWorld.DragonFirstSeconds && !w.GameOver) { Watch(g, w); yield return null; }
            double from = w.Time; float clock0 = life.ProofClock();
            while (w.Time < from + RateSeconds && !w.GameOver) { Watch(g, w); yield return null; }
            g.gameSpeed = 1f;
            double span = w.Time - from;
            float pace = span > 0 ? (float)((life.ProofClock() - clock0) / span) : 0f;
            int n = OzDragonProof.Orders.Count; double each = n > 0 ? span / n : 0;
            Line(Mathf.Abs(pace - 1f) <= 0.1f, $"the dragons flew at the sim's pace: {pace:F2} of it. Under 0.9 the frame rate held them back: run again at a lower speed");
            Line(span >= RateShortest, $"{span:F0} s of a match after the first minute{(w.GameOver ? ", it ended there" : "")}: nothing planted, the computer on both sides, the camera on the player's fight");
            Line(n >= 1 && each >= RateMost && each <= RateLeast, $"sometimes: {n} attacks, one in {each:F0} s of play, wanted one in {RateMost:F0} to {RateLeast:F0}. Too many or too few: ZWorld.DragonChancePerMille, now {ZWorld.DragonChancePerMille}");
            int met = OzDragonProof.Encounters;
            Line(met > 0, $"how it got there: {met} robots met on a dragon's line, {OzDragonProof.OffScreen} of them off screen ({(met > 0 ? 100 * OzDragonProof.OffScreen / met : 0)} %), {OzDragonProof.NoPath} no clear path, {w.DragonRefused[ZWorld.WhyRoll]} lost the chance");
            Line(OzDragonProof.Orders.All(o => o.t >= ZWorld.DragonFirstSeconds), "none in the first minute");
        }
    }
}
#endif
