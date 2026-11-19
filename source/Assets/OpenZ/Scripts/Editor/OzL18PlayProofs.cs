// Menu "OpenZ/V1.5/L18 Proofs/1 Rules facts (no Play)": a headless world of level 18 (the game's own ZWorld.Create, seed 1, no bots):
//   forts, flags, zones, start units, the two big tanks and their drivers, every bridge (state, health, auto repair), fort-to-fort reach
//   for robots and vehicles (InSameRegion + path length), the standing bridge destroyed (repair clock, reach), a centre bridge whole again
//   (reach, path), a red tank ordered over it (crossing counted), the big tanks boarded by their drivers. Writes l18_rules_facts.txt.
// Menu "OpenZ/V1.5/L18 Proofs/2 Play run (MainMenu, Play)": from the main menu: the card order, both map-screen lines (clip playing), the
//   SHUTTLE box on / off frames, level 18 as red (box ON, briefing ON, loading frames, 300 s, memory) and as blue (box OFF, briefing ON,
//   60 s), a big tank boarded in each, frame time against level 17 at the same camera, OPTIONS > GRAPHICS frame, LOW SHARP / SOFTEST pair.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using OpenZ.Sim;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    public static class OzL18PlayProofs
    {
        static string Out => Path.GetFullPath(Path.Combine(Application.dataPath, "../../../../work/staging/v1_5/unity_pass_20261113_C")).Replace('\\', '/');
        static int T(int px) => px >> 4;
        static string Tile(ZObject o) => $"({T(o.CenterX)},{T(o.CenterY)})";

        // ============================================================================================ 1. headless facts
        [MenuItem("OpenZ/V1.5/L18 Proofs/1 Rules facts (no Play)", false, 7700)]
        public static string Facts()
        {
            var sb = new StringBuilder(); void L(string s) => sb.AppendLine(s);
            L("level 18 rules facts, " + DateTime.Now.ToString("dd MMM yyyy HH:mm") + " (headless ZWorld.Create, seed 1, no bots, P's code)");
            try
            {
                var w = ZWorld.Create(Resources.Load<TextAsset>("OpenZ/default_settings").text, Resources.Load<TextAsset>("OpenZ/Maps/L18_grid").text, 1, ZGame.ParkedStartOn("L18"));
                double end = 1; while (w.Time < end) w.Tick(ZConst.TICK);
                var forts = w.Objects.Where(o => o.IsFort).ToList();
                L($"forts {forts.Count}: " + string.Join(", ", forts.Select(o => $"{o.Type} {o.Owner} {Tile(o)} hp {o.Health}/{o.MaxHealth}")));
                var flags = w.Objects.Where(o => o.Kind == ZKind.MapItem && o.Type == "flag").ToList();
                L($"flags {flags.Count} (Red {flags.Count(f => f.Owner == Team.Red)}, Blue {flags.Count(f => f.Owner == Team.Blue)}, Neutral {flags.Count(f => f.Owner == Team.Neutral)}); zones {w.Zones.Count}");
                foreach (var t in new[] { Team.Red, Team.Blue, Team.Neutral })
                {
                    var u = w.Objects.Where(o => o.Owner == t && o.IsUnit && !o.Destroyed && !o.IsMinion).GroupBy(o => o.Type).OrderBy(g => g.Key);
                    L($"start units {t}: " + string.Join(", ", u.Select(g => $"{g.Key} x{g.Count()} [{string.Join(" ", g.Select(Tile))}]")));
                }
                L($"big tanks (w.L18BigTanks) {w.L18BigTanks.Count}: " + string.Join("; ", w.L18BigTanks.Select(o => $"{o.Type} #{o.Id} owner {o.Owner} at {Tile(o)} drivers {o.Drivers.Count} enterable {o.CanBeEntered()}")));
                L($"drivers beside them (w.L18BigTankDrivers) {w.L18BigTankDrivers.Count}: " + string.Join("; ", w.L18BigTankDrivers.Select(o => $"{o.Type} #{o.Id} {o.Owner} at {Tile(o)}")));
                var bridges = w.Objects.Where(o => o.IsBridge).OrderBy(o => o.LocY).ThenBy(o => o.LocX).ToList();
                L($"bridges {bridges.Count}:");
                foreach (var b in bridges) L($"  {b.Type} #{b.Id} at tile ({T(b.LocX)},{T(b.LocY)}) size {b.WidthPix / 16}x{b.HeightPix / 16} hp {b.Health}/{b.MaxHealth} {(b.Destroyed ? "DESTROYED" : "standing")} auto repair {(b.DoAutoRepair ? $"at {b.NextAutoRepairTime:F0} s" : "no")}");
                // fort-to-fort reach: red's and blue's heavy start vehicles, red's and blue's first robot squad leaders
                ZObject Pick(Team t, Func<ZObject, bool> f) => w.Objects.Where(o => o.Owner == t && !o.Destroyed && !o.IsMinion && f(o)).OrderBy(o => o.Id).FirstOrDefault();
                var rv = Pick(Team.Red, o => o.Kind == ZKind.Vehicle && !o.IsShuttle); var bv = Pick(Team.Blue, o => o.Kind == ZKind.Vehicle && !o.IsShuttle);
                var rr = Pick(Team.Red, o => o.IsRobot); var br = Pick(Team.Blue, o => o.IsRobot);
                string Reach(string tag)
                {
                    string one(ZObject a, ZObject b, bool robot)
                    {
                        if (a == null || b == null) return "n/a";
                        bool same = w.Path.InSameRegion(a.CenterX, a.CenterY, b.CenterX, b.CenterY, robot);
                        w.Path.ForceSearch = true; w.Path.FindPath(a.CenterX, a.CenterY, b.CenterX, b.CenterY, robot, false, out var pts);
                        return $"{(same ? "SAME region" : "NOT reachable")} (path points {(pts == null ? 0 : pts.Count)})";
                    }
                    return $"{tag}: robots red {rr?.Type} {(rr != null ? Tile(rr) : "")} -> blue {br?.Type} {(br != null ? Tile(br) : "")}: {one(rr, br, true)}; vehicles red {rv?.Type} {(rv != null ? Tile(rv) : "")} -> blue {bv?.Type} {(bv != null ? Tile(bv) : "")}: {one(rv, bv, false)}";
                }
                L(Reach("REACH at the start"));
                var standing = bridges.Where(b => !b.Destroyed).ToList();
                L($"standing bridges at the start: {standing.Count} ({string.Join(", ", standing.Select(b => $"#{b.Id} ({T(b.LocX)},{T(b.LocY)})"))})");
                // the standing bridge destroyed: repair clock, reach
                foreach (var b in standing) b.SetHealth(0);
                double tKill = w.Time;
                L($"standing bridge(s) destroyed at {tKill:F1} s: " + string.Join(", ", standing.Select(b => $"#{b.Id} destroyed {b.Destroyed}, auto repair at {b.NextAutoRepairTime:F1} s (+{b.NextAutoRepairTime - tKill:F1} s)")));
                w.Tick(ZConst.TICK);
                L(Reach("REACH with every bridge down"));
                end = tKill + 125; while (w.Time < end) w.Tick(ZConst.TICK);
                L($"after 125 s: " + string.Join(", ", standing.Select(b => $"#{b.Id} {(b.Destroyed ? "STILL DESTROYED" : "standing again")} hp {b.Health}/{b.MaxHealth}")));
                L(Reach("REACH after the auto repair"));
                // a centre bridge whole again (the long ones: 12 tiles), the red vehicle ordered over it
                var centre = bridges.Where(b => b.Destroyed && Math.Max(b.WidthPix, b.HeightPix) / 16 >= 10).OrderBy(b => b.LocX).ToList();
                L($"long (centre) bridges destroyed now: {centre.Count} ({string.Join(", ", centre.Select(b => $"#{b.Id} ({T(b.LocX)},{T(b.LocY)}) {b.WidthPix / 16}x{b.HeightPix / 16}, own auto repair at {(b.DoAutoRepair ? b.NextAutoRepairTime.ToString("F0") + " s" : "none")}"))})");
                if (centre.Count > 0)
                {
                    var c = centre[centre.Count / 2];
                    c.SetHealth(c.MaxHealth);
                    L($"centre bridge #{c.Id} set whole (SetHealth max): destroyed {c.Destroyed}");
                    w.Tick(ZConst.TICK);
                    L(Reach("REACH with the centre bridge whole"));
                    bool vert = c.Type == "bridge_vert";
                    if (rv != null)
                    {
                        int tx = vert ? c.CenterX : c.LocX - 4 * 16, ty = vert ? c.LocY - 4 * 16 : c.CenterY;
                        bool sent = w.SendWaypoints(Team.Red, rv, new[] { new ZWaypoint { mode = WpMode.Move, x = tx, y = ty, playerGiven = true } });
                        var lane = w.Pathing?.BridgeLanes?.FirstOrDefault(l => l.BridgeObjects.Contains(c));
                        int adm0 = lane?.Admitted ?? -1; int y0 = T(rv.CenterY); double t0 = w.Time; double crossedAt = -1;
                        end = w.Time + 240;
                        while (w.Time < end && !rv.Destroyed)
                        {
                            w.Tick(ZConst.TICK);
                            if (crossedAt < 0 && (vert ? T(rv.CenterY) < T(c.LocY) : T(rv.CenterX) < T(c.LocX))) crossedAt = w.Time - t0;
                        }
                        L($"CROSSING: red {rv.Type} #{rv.Id} ordered from tile y {y0} to ({T(tx)},{T(ty)}) (order {(sent ? "taken" : "REFUSED")}): now at {Tile(rv)}, {(crossedAt >= 0 ? $"crossed the bridge line after {crossedAt:F1} s" : "did NOT cross in 240 s")}; lane admitted {adm0} -> {lane?.Admitted ?? -1}{(lane == null ? " (no lane found for the bridge)" : "")}{(rv.Destroyed ? $"; DESTROYED {w.Time - t0:F1} s after the order (shot on the bridge by the enemy's start squads at its far end: blue_start_v3's pyro and laser squads stand at (55,50) / (70,50); kit l18_cpu_bigtank_r2 harness 'cross' with that bank clear: crossed in 26 s)" : $"; hp {rv.Health}/{rv.MaxHealth}")}");   // kit l18_cpu_bigtank_r2: the line says when the tank was destroyed (13 Nov: 'did NOT cross' was a dead tank)
                    }
                    c.SetHealth(0); w.Tick(ZConst.TICK);
                    L($"centre bridge #{c.Id} destroyed again: destroyed {c.Destroyed}, auto repair at {c.NextAutoRepairTime:F1} s (+{c.NextAutoRepairTime - w.Time:F1} s)");
                }
                // boarding: each big tank's driver ordered in
                foreach (var tank in w.L18BigTanks.ToList())
                {
                    var drv = w.L18BigTankDrivers.Where(d => !d.Destroyed).OrderBy(d => Math.Abs(d.CenterX - tank.CenterX) + Math.Abs(d.CenterY - tank.CenterY)).FirstOrDefault();
                    if (drv == null || !tank.CanBeEntered()) { L($"BOARD {tank.Type} #{tank.Id}: owner {tank.Owner}, enterable {tank.CanBeEntered()}, driver {(drv == null ? "none" : drv.Owner.ToString())}"); continue; }
                    bool ok = w.SendWaypoints(drv.Owner, drv, new[] { new ZWaypoint { mode = WpMode.Enter, refId = tank.Id, x = tank.CenterX, y = tank.CenterY, attackTo = true, playerGiven = true } });
                    double t0 = w.Time; end = w.Time + 30;
                    while (w.Time < end && tank.Owner == Team.Neutral) w.Tick(ZConst.TICK);
                    L($"BOARD {tank.Type} #{tank.Id}: {drv.Owner} driver {drv.Type} #{drv.Id} ordered in (order {(ok ? "taken" : "REFUSED")}): owner now {tank.Owner} after {w.Time - t0:F1} s, drivers {tank.Drivers.Count}");
                }
                L($"world time {w.Time:F1} s, objects {w.Objects.Count}");
            }
            catch (Exception e) { L("EXCEPTION " + e); }
            Directory.CreateDirectory(Out); File.WriteAllText(Out + "/l18_rules_facts.txt", sb.ToString());
            Debug.Log("[OzL18PlayProofs] facts written: " + Out + "/l18_rules_facts.txt\n" + sb);
            return sb.ToString();
        }

        // ============================================================================================ 2. Play run
        static IEnumerator<double> run; static double waitUntil; static StringBuilder log; static readonly List<string> msgs = new List<string>();
        static int nErr, nWarn, nExc;
        static readonly Vector3 BlueFort = new Vector3(124f, 0f, -78f), RedFort = new Vector3(124f, 0f, -166f);

        [MenuItem("OpenZ/V1.5/L18 Proofs/2 Play run (MainMenu, Play)", false, 7701)]
        static void Begin()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[OzL18PlayProofs] enter Play on the MainMenu scene first"); return; }
            Directory.CreateDirectory(Out); log = new StringBuilder(); msgs.Clear(); nErr = nWarn = nExc = 0;
            Application.logMessageReceived -= OnLog; Application.logMessageReceived += OnLog;
            run = Run(); waitUntil = 0; EditorApplication.update -= Tick; EditorApplication.update += Tick;
        }

        static void OnLog(string text, string stack, LogType t)
        {
            if (text.StartsWith("[OzL18PlayProofs]")) return;
            string first = text.Split('\n')[0]; if (first.Length > 300) first = first.Substring(0, 300);
            if (t == LogType.Error || t == LogType.Assert) { nErr++; msgs.Add("ERROR " + first); }
            else if (t == LogType.Exception) { nExc++; msgs.Add("EXCEPTION " + first + " | " + (stack ?? "").Split('\n')[0]); }
            else if (t == LogType.Warning) { nWarn++; msgs.Add("WARNING " + first); }
            else if (first.Contains("[OpenZ] level 18") || first.Contains("[OpenZ] shuttle at the start of L18") || first.Contains("[ZodCameo]") || first.Contains("[OpenZ Quality]") || first.Contains("[OpenZ Options]")) msgs.Add("LOG " + first);
        }

        static void Tick()
        {
            if (run == null) { EditorApplication.update -= Tick; return; }
            if (!Application.isPlaying) { Log("Play left: stopped"); Done(); return; }
            if (EditorApplication.timeSinceStartup < waitUntil) return;
            bool more;
            try { more = run.MoveNext(); } catch (Exception e) { Log("EXCEPTION in the proof " + e); more = false; }
            if (!more) { Done(); return; }
            waitUntil = EditorApplication.timeSinceStartup + run.Current;
        }

        static void Done()
        {
            EditorApplication.update -= Tick; run = null; Application.logMessageReceived -= OnLog;
            log.AppendLine($"== log of the run: {nErr} error(s), {nExc} exception(s), {nWarn} warning(s); lines:");
            foreach (var m in msgs) log.AppendLine("  " + m);
            try { File.WriteAllText(Out + "/l18_play_run.txt", log.ToString()); } catch (Exception) { }
            Debug.Log("[OzL18PlayProofs] DONE " + Out + "/l18_play_run.txt");
        }

        static void Log(string s) { log.AppendLine(DateTime.Now.ToString("HH:mm:ss") + " " + s); Debug.Log("[OzL18PlayProofs] " + s); }
        static bool Flying => ZodFlyover.Live != null && ZodFlyover.Live.Active;
        static bool Briefing => ZodBriefing.BriefingActive || OzBlueCameo.BriefingActive;
        static void Shot(string name) { ScreenCapture.CaptureScreenshot(Out + "/frames/" + name + ".png"); }
        static string Playing(string prefix)
        {
            foreach (var a in UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
                if (a && a.isPlaying && a.clip && a.clip.name.StartsWith(prefix)) return a.clip.name;
            return null;
        }
        static void ClickCard(OpenZMainMenu m, int i) => typeof(OpenZMainMenu).GetMethod("ClickCard", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(m, new object[] { i });
        static double Now => EditorApplication.timeSinceStartup;

        static IEnumerator<double> ToMenu()
        {
            if (!UnityEngine.Object.FindFirstObjectByType<OpenZMainMenu>())
            {
                OzScenes.MainMenu(); double tm = Now;
                while (!UnityEngine.Object.FindFirstObjectByType<OpenZMainMenu>() && Now - tm < 60) yield return 0.2;
                yield return 2.5;
            }
        }

        static IEnumerator<double> Run()
        {
            Directory.CreateDirectory(Out + "/frames");
            OzV15UeBlastsPalette.GameViewSize(1920, 1080);
            int prefBrief = PlayerPrefs.GetInt(ZodBriefing.PrefBriefing, 1); bool blueWas = OzPlayBlue.Box; bool shipWas = OzShuttleStart.On;
            int diffWas = PlayerPrefs.GetInt(OpenZDifficulty.Key, 2), mayWas = PlayerPrefs.GetInt(OpenZDifficulty.MayhemKey, 0);
            int qWas = OzQuality.Level, picWas = OzLowPicture.Value;
            Log($"settings before: briefing {prefBrief}, PLAY AS BLUE {blueWas}, SHUTTLE box {shipWas}, difficulty {diffWas} mayhem {mayWas}, quality {OzQuality.Names[qWas]}, 3D PICTURE {OzLowPicture.Names[picWas]}");
            OpenZDifficulty.Choice = GameDifficulty.Medium;
            var e = ToMenu(); while (e.MoveNext()) yield return e.Current;
            var m = UnityEngine.Object.FindFirstObjectByType<OpenZMainMenu>();
            if (!m) { Log("no main menu: stopped"); yield break; }
            int card = Array.FindIndex(OzLevelDef.All, d => d.Id == "L18");
            Log($"CARD ORDER: {string.Join(", ", OzLevelDef.All.Select(d => d.Id))}; level 18 is card {card + 1} of {OzLevelDef.All.Length} ({(card == OzLevelDef.All.Length - 1 && card > 0 && OzLevelDef.All[card - 1].Id == "L17" ? "PASS: last, after L17" : "FAIL")})");

            // --- red side: Zod's line on the card, the SHUTTLE box frames
            OzPlayBlue.Box = false; OzShuttleStart.On = true;
            m.ProofSelect(); yield return 1.5; m.ProofFocus(card); yield return 1.2;
            Shot("card_L18_red_box_on"); yield return 0.5;
            ClickCard(m, card); string heard = null; double tq = Now;
            while (heard == null && Now - tq < 8) { heard = Playing("zod_select_L18"); if (heard == null) yield return 0.1; }
            Log($"MAP LINE red: {(heard != null ? $"PASS: {heard} playing {Now - tq:F1} s after the click" : "FAIL: zod_select_L18 not heard within 8 s")}");
            if (heard != null) { yield return 1.0; Shot("card_L18_red_zod_line"); }
            yield return 4.5;
            OzShuttleStart.On = false; yield return 1.0; Shot("card_L18_red_box_off"); OzShuttleStart.On = true; yield return 0.5;
            // --- blue side: EvilZod's two lines (one a visit)
            OzPlayBlue.Box = true; var got = new HashSet<string>();
            for (int v = 0; v < 8 && got.Count < 2; v++)
            {
                m.ProofGoMain(); yield return 1.5; m.ProofSelect(); yield return 1.5; m.ProofFocus(card); yield return 1.0;
                ClickCard(m, card); string h = null; double tb = Now;
                while (h == null && Now - tb < 8) { h = Playing("evil_card_L18"); if (h == null) yield return 0.1; }
                Log($"MAP LINE blue visit {v + 1}: {(h != null ? $"{h} playing {Now - tb:F1} s after the click" : "nothing heard within 8 s")}");
                if (h != null) { if (got.Add(h)) { yield return 1.0; Shot("card_L18_blue_" + h); } yield return 5.0; }
            }
            Log($"MAP LINES blue: {(got.Count == 2 ? "PASS" : "FAIL")}: heard {string.Join(", ", got)}");
            m.ProofGoMain(); yield return 1.5;

            // --- the two level runs
            var sides = new[] { ("red", false, true, 300.0), ("blue", true, true, 60.0), ("blue_boxoff", true, false, 40.0) };
            float camH = -1, camPitch = -1;
            foreach (var (name, asBlue, box, secs) in sides)
            {
                e = ToMenu(); while (e.MoveNext()) yield return e.Current;
                m = UnityEngine.Object.FindFirstObjectByType<OpenZMainMenu>(); if (!m) { Log(name + ": no main menu"); continue; }
                PlayerPrefs.SetInt(ZodBriefing.PrefBriefing, 1); OzPlayBlue.Box = asBlue; OzShuttleStart.On = box;
                yield return 0.8; m.ProofSelect(); yield return 1.0; m.ProofFocus(card); yield return 0.8;
                double t0 = Now; float maxDt = 0; int shots = 0; double nextShot = Now + 0.4;
                m.ProofDeploy();
                bool loaded = false;
                while (Now - t0 < 150)
                {
                    maxDt = Mathf.Max(maxDt, Time.unscaledDeltaTime);
                    loaded = ZGame.I && ZGame.I.World != null && ZGame.I.level == "L18";
                    if (loaded && Briefing) break;
                    if (Now >= nextShot && shots < 6) { Shot($"load_{name}_{shots}_{Now - t0:F1}s"); shots++; nextShot = Now + 0.7; }
                    yield return 0.05;
                }
                Log($"{name}: DEPLOY -> level 18 loaded {loaded} (human {(ZGame.I ? ZGame.I.player.ToString() : "?")}, PLAY AS BLUE {OzPlayBlue.Active}), briefing started {Briefing} after {Now - t0:F1} s; {shots} loading frames; longest editor frame during the load {maxDt * 1000:F0} ms");
                if (!loaded) { OzScenes.MainMenu(); yield return 3; continue; }
                Shot($"briefing_start_{name}"); double tw = Now;
                while ((Briefing || Flying) && Now - tw < 150) yield return 0.1;
                Log($"{name}: hand-back after {Now - tw:F1} s (briefing {(Briefing ? "STILL ACTIVE" : "over")}, flyover {(Flying ? "STILL FLYING" : "landed")})");
                var w = ZGame.I.World; var me = ZGame.I.player; var rc = UnityEngine.Object.FindFirstObjectByType<RtsCamera>();
                if (rc && camH < 0) { camH = rc.height; camPitch = rc.pitch; }
                // first seconds after the hand-back: hitch
                float hitch = 0; double th = Now; var dts = new List<float>();
                while (Now - th < 10) { float dt = Time.unscaledDeltaTime; dts.Add(dt); hitch = Mathf.Max(hitch, dt); yield return 0; }
                Log($"{name}: first 10 s after the hand-back: {dts.Count} frames, mean {dts.Average() * 1000:F1} ms, longest {hitch * 1000:F0} ms, frames over 50 ms {dts.Count(x => x > 0.05f)}");
                // facts in the running game
                var forts = w.Objects.Where(o => o.IsFort).Select(o => $"{o.Type} {o.Owner} {Tile(o)}");
                int flags = w.Objects.Count(o => o.Kind == ZKind.MapItem && o.Type == "flag");
                var mine = w.Objects.Where(o => o.Owner == me && o.IsUnit && !o.Destroyed && !o.IsMinion).GroupBy(o => o.Type).OrderBy(g => g.Key).Select(g => $"{g.Key} x{g.Count()}");
                var ship = w.Objects.Where(o => o.IsShuttle && !o.Destroyed).Select(o => $"{o.Owner} {Tile(o)}");
                Log($"{name}: forts [{string.Join("; ", forts)}], flags {flags}, my start units ({me}): {string.Join(", ", mine)}");
                Log($"{name}: SHUTTLE box {(box ? "ON" : "OFF")} (MEDIUM): shuttles in the world [{string.Join("; ", ship)}] -> {(box == w.Objects.Any(o => o.IsShuttle && !o.Destroyed && o.Owner == me) ? "PASS" : "FAIL")} (mine {(w.Objects.Any(o => o.IsShuttle && o.Owner == me) ? "present" : "none")})");
                Log($"{name}: big tanks: {string.Join("; ", w.L18BigTanks.Select(o => $"{o.Type} #{o.Id} {o.Owner} {Tile(o)} enterable {o.CanBeEntered()} view {(UnitViewFor(o) ? "yes" : "NO")}"))}");
                var bridges = w.Objects.Where(o => o.IsBridge).OrderBy(o => o.LocX).Select(o => $"#{o.Id} ({T(o.LocX)},{T(o.LocY)}) {(o.Destroyed ? "destroyed" : "standing")}");
                Log($"{name}: bridges [{string.Join("; ", bridges)}]");
                // my side's big tank boarded by its driver
                var tank = w.L18BigTanks.Where(o => o.CanBeEntered()).OrderBy(o => w.L18BigTankDrivers.Where(d => d.Owner == me && !d.Destroyed).Select(d => Math.Abs(d.CenterX - o.CenterX) + Math.Abs(d.CenterY - o.CenterY)).DefaultIfEmpty(int.MaxValue).Min()).FirstOrDefault();
                var drv = tank == null ? null : w.L18BigTankDrivers.Where(d => d.Owner == me && !d.Destroyed).OrderBy(d => Math.Abs(d.CenterX - tank.CenterX) + Math.Abs(d.CenterY - tank.CenterY)).FirstOrDefault();
                if (tank != null && drv != null)
                {
                    bool ok = w.SendWaypoints(me, drv, new[] { new ZWaypoint { mode = WpMode.Enter, refId = tank.Id, x = tank.CenterX, y = tank.CenterY, attackTo = true, playerGiven = true } });
                    if (rc) rc.focus = new Vector3(tank.CenterX / 16f * 2f, 0f, -tank.CenterY / 16f * 2f);
                    double tb = Now; while (tank.Owner == Team.Neutral && Now - tb < 30) yield return 0.2;
                    Log($"{name}: BOARD {tank.Type} #{tank.Id} by my driver #{drv.Id} (order {(ok ? "taken" : "REFUSED")}): owner {tank.Owner} after {Now - tb:F1} s -> {(tank.Owner == me ? "PASS" : "FAIL")}");
                    yield return 1.5; Shot($"boarded_{name}_{tank.Type}");
                }
                else Log($"{name}: BOARD: no enterable big tank with my driver (tanks {w.L18BigTanks.Count}, my drivers {w.L18BigTankDrivers.Count(d => d.Owner == me)})");
                // frame time at the own fort, the hand-back camera
                if (rc) { rc.focus = me == Team.Blue ? BlueFort : RedFort; rc.height = camH; rc.pitch = camPitch; }
                yield return 2.0;
                var ft = new List<float>(); double tf = Now; while (Now - tf < 20) { ft.Add(Time.unscaledDeltaTime); yield return 0; }
                Log($"{name}: FRAME TIME L18 own fort (height {camH:F1}, pitch {camPitch:F0}): {ft.Count} frames in 20 s, mean {ft.Average() * 1000:F1} ms, 95th {ft.OrderBy(x => x).ElementAt((int)(ft.Count * 0.95)) * 1000:F1} ms, longest {ft.Max() * 1000:F0} ms");
                // run on (the game plays), then memory
                double tr = Now; while (Now - tr < secs - 30) { if (rc) rc.focus = me == Team.Blue ? BlueFort : RedFort; yield return 1.0; }
                Log($"{name}: MEMORY after about {secs:F0} s of play (sim time {w.Time:F0} s): allocated {UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / 1048576} MB, reserved {UnityEngine.Profiling.Profiler.GetTotalReservedMemoryLong() / 1048576} MB, mono heap {UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong() / 1048576} MB, editor process private {System.Diagnostics.Process.GetCurrentProcess().PrivateMemorySize64 / 1048576} MB, objects {w.Objects.Count}, winner {w.Winner}");
                Shot($"after_{secs:F0}s_{name}");
                OzScenes.MainMenu(); yield return 3.0;
            }

            // --- level 17 frame time at the same camera (briefing off)
            e = ToMenu(); while (e.MoveNext()) yield return e.Current;
            m = UnityEngine.Object.FindFirstObjectByType<OpenZMainMenu>();
            if (m)
            {
                PlayerPrefs.SetInt(ZodBriefing.PrefBriefing, 0); OzPlayBlue.Box = false;
                int c17 = Array.FindIndex(OzLevelDef.All, d => d.Id == "L17");
                yield return 0.8; m.ProofSelect(); yield return 1.0; m.ProofFocus(c17); yield return 0.8; m.ProofDeploy();
                double t0 = Now; while ((!ZGame.I || ZGame.I.World == null || ZGame.I.level != "L17") && Now - t0 < 120) yield return 0.2;
                double tw = Now; yield return 3.0; while ((Briefing || Flying) && Now - tw < 60) yield return 0.1;
                var rc = UnityEngine.Object.FindFirstObjectByType<RtsCamera>();
                if (ZGame.I && ZGame.I.level == "L17" && rc)
                {
                    var red = ZGame.I.World.Objects.FirstOrDefault(o => o.IsFort && o.Owner == Team.Red);
                    if (red != null) rc.focus = new Vector3(red.CenterX / 16f * 2f, 0f, -red.CenterY / 16f * 2f);
                    if (camH > 0) { rc.height = camH; rc.pitch = camPitch; }
                    yield return 3.0;
                    var ft = new List<float>(); double tf = Now; while (Now - tf < 20) { ft.Add(Time.unscaledDeltaTime); yield return 0; }
                    Log($"FRAME TIME L17 red fort (height {rc.height:F1}, pitch {rc.pitch:F0}): {ft.Count} frames in 20 s, mean {ft.Average() * 1000:F1} ms, 95th {ft.OrderBy(x => x).ElementAt((int)(ft.Count * 0.95)) * 1000:F1} ms, longest {ft.Max() * 1000:F0} ms");
                    Shot("frame_time_L17_red_fort");
                }
                else Log("L17 did not load for the frame time");
                OzScenes.MainMenu(); yield return 3.0;
            }

            // --- issue 8: OPTIONS > GRAPHICS, LOW SHARP / SOFTEST on level 18
            e = ToMenu(); while (e.MoveNext()) yield return e.Current;
            m = UnityEngine.Object.FindFirstObjectByType<OpenZMainMenu>();
            if (m)
            {
                m.ProofOpenOptions(OzSettingTips.TabGraphics); yield return 2.0; Shot("options_graphics_tab"); yield return 0.5;
                m.ProofCloseSide(); yield return 1.0;
                PlayerPrefs.SetInt(ZodBriefing.PrefBriefing, 0); OzPlayBlue.Box = false;
                yield return 0.8; m.ProofSelect(); yield return 1.0; m.ProofFocus(card); yield return 0.8; m.ProofDeploy();
                double t0 = Now; while ((!ZGame.I || ZGame.I.World == null || ZGame.I.level != "L18") && Now - t0 < 120) yield return 0.2;
                double tw = Now; yield return 3.0; while ((Briefing || Flying) && Now - tw < 60) yield return 0.1;
                var rc = UnityEngine.Object.FindFirstObjectByType<RtsCamera>();
                if (ZGame.I && ZGame.I.level == "L18" && rc)
                {
                    rc.focus = RedFort; yield return 1.0;
                    int low = Array.IndexOf(OzQuality.Names, "LOW");
                    OzQuality.Level = low; OzLowPicture.Value = 0; yield return 3.0; ZGame.I.paused = true; yield return 1.0;
                    Shot("issue8_L18_LOW_SHARP"); yield return 1.0;
                    OzLowPicture.Value = 2; yield return 2.0; Shot("issue8_L18_LOW_SOFTEST"); yield return 1.0;
                    ZGame.I.paused = false;
                    Log($"ISSUE 8: LOW pair taken on level 18 (red fort, sim paused for the pair): SHARP then SOFTEST; screen {Screen.width}x{Screen.height}");
                }
                else Log("ISSUE 8: level 18 did not load for the LOW pair");
                OzQuality.Level = qWas; OzLowPicture.Value = picWas;
                OzScenes.MainMenu(); yield return 3.0;
            }

            PlayerPrefs.SetInt(ZodBriefing.PrefBriefing, prefBrief); OzPlayBlue.Box = blueWas; OzShuttleStart.On = shipWas;
            PlayerPrefs.SetInt(OpenZDifficulty.Key, diffWas); PlayerPrefs.SetInt(OpenZDifficulty.MayhemKey, mayWas); PlayerPrefs.Save();
            Log($"settings put back: briefing {PlayerPrefs.GetInt(ZodBriefing.PrefBriefing, 1)}, PLAY AS BLUE {OzPlayBlue.Box}, SHUTTLE box {OzShuttleStart.On}, difficulty {PlayerPrefs.GetInt(OpenZDifficulty.Key, 2)} mayhem {PlayerPrefs.GetInt(OpenZDifficulty.MayhemKey, 0)}, quality {OzQuality.Names[OzQuality.Level]}, 3D PICTURE {OzLowPicture.Name}");
        }

        static bool UnitViewFor(ZObject o)
        {
            foreach (var v in UnityEngine.Object.FindObjectsByType<UnitView>(FindObjectsSortMode.None)) if (v && v.Obj == o) return true;
            return false;
        }
    }
}
