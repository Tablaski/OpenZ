#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using OpenZ.Sim;
using Unity.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OpenZ
{
    /// <summary>
    /// Heavy tank v2 films (1.2 items 126, 129, 130, 132, 132b; editor only). One auto-started take per job on L02 (PlayerPrefs oz_hv2films_auto =
    /// "tread" / "battle" / "board" / "board_close" / "fight" / "snipe" / "turret" / "recoil", set by the menus OpenZ/Heavy Tank v2/4 .. 8, 11, 12).
    /// "turret" is item 132.1's counted test (the turret's heading every frame while it switches between five targets round it, 6 to 16 m,
    /// and while it re-obtains them one by one; v2 once with UnitAim.SteadyOn off as the control, then on; v1 on), "recoil" item 132.2 / 132b's
    /// (a red v2 and a red v1 fire at a target: the head's slide and rock and the hull's rock and slide measured on the drawn model every
    /// frame round each shot pair, crops 1/30 s apart). Each job stages its scene with the
    /// game's own units, rules and orders (the computer players cleared, the briefing skipped, the day held, every staged change written
    /// in the log), films it from the RTS camera at the level's own default height (1920 x 1080, 30 fps, the game's sound: frames and the
    /// sound capture in step, ffmpeg joins them), and writes:
    ///   hv2_tread_L02 / hv2_battle_L02 / hv2_board_L02 / hv2_board_close_L02 / hv2_fight_to_death_L02 / hv2_driver_snipe_L02
    ///   .mp4 + _sheet.jpg (2 frames a second), _crops.png (zoomed crops round the main tank every 0.5 s; the tread job:
    ///   both track ends every 0.1 s on each straight leg) and _log.txt (the counted facts at 10 Hz, each crop's time and state, the FACTS
    /// Nothing here changes a rule of the game: the units are made with ZWorld.CreateObject / CreateRobotGroup and ordered with
    /// SendWaypoints as the player orders them.
    /// k81c: every film stands on FIXED tiles (View/HeavyV2Spots.cs: chosen off the editor on a scan of the whole map with the sim's own
    /// tests); each heavy slot is asked again in Play (ZPathFinder.StepOk, ZWorld.SnipeSafeSpot) and a blocked or protected slot FAILS the
    /// take loudly (no search, no fall-back to a start tile). The camera is the game's default play zoom (70 m, where every level starts)
    /// and stays on the subject. Two more jobs: "combat" (item 132 in a real fight: a driving v2 among moving enemies, some dying; the
    /// turret with the code before k81b, with k81b's alone, and with the world-heading slew of View/UnitAim.Combat.cs) and "plant" (items
    /// 137 / 146: v1 and v2 driving and firing on flat ground and over a bridge's ramps, the tracks against the ground and the hull over
    /// the tracks every frame, the planting of View/UnitView.GroundGear.cs off as the control, then on).
    /// </summary>
    public partial class HeavyV2Films : MonoBehaviour                                                  // k84: + View/HeavyV2Films.Subject.cs (the unit filmed: heavy or light)
    {
        public const string AutoPref = "oz_hv2films_auto";
        public static readonly string[] Jobs = { "tread", "battle", "board", "board_close", "fight", "snipe", "turret", "recoil", "combat", "plant" };
        /// <summary>The camera height of the films (m): the game's default play zoom, where every level starts (RtsCamera, 70 m).</summary>
        public const float PlayHeight = 70f;
        /// <summary>The take's file name (the MP4, its sheets and its log) of a job.</summary>
        public static string TakeName(string job) => job == "fight" ? S.Prefix + "_fight_to_death_L02" : job == "snipe" ? S.Prefix + "_driver_snipe_L02" : S.Prefix + "_" + job + "_L02";   // k84: hv2_ / lt2_
        /// <summary>The close boarding take (picture a: hands on the rungs, the hop, the LEFT cupola): the camera at this height instead of the level's.</summary>
        public const float CloseHeight = 32f;
        static readonly string Root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../.."));
        static string Dir => Path.Combine(Root, S.ProofDir);                                              // k84: the unit filmed's proof folder
        const int Fps = 30, Jpg = 92;

        string job, take, sub;
        readonly StringBuilder log = new StringBuilder();
        readonly List<string> facts = new List<string>();
        bool done; int errors; float lastMark, hot = -1f, lastSample = -1f; int peak = -1;
        readonly System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
        float Real => (float)clock.Elapsed.TotalSeconds;
        float TakeT => frameNo / (float)Fps;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Auto()
        {
            string j = PlayerPrefs.GetString(AutoPref, "");
            if (string.IsNullOrEmpty(j)) return;
            PlayerPrefs.DeleteKey(AutoPref); PlayerPrefs.Save();
            var go = new GameObject("HeavyV2Films"); DontDestroyOnLoad(go);
            go.AddComponent<HeavyV2Films>().job = j;
        }

        void Note(string s) { lastMark = Real; string l = recording ? $"[take {TakeT,6:0.00}] {s}" : $"[real {Real,6:0.00}] {s}"; log.AppendLine(l); Debug.Log("[hv2films] " + s); }
        void Fact(string s) { facts.Add(s); Note("FACT " + s); }
        void OnEnable() { Application.logMessageReceived += OnLog; }
        void OnDisable() { Application.logMessageReceived -= OnLog; if (!done) Finish("disabled"); }
        void OnLog(string c, string st, LogType type)
        {
            if (c.StartsWith("[hv2films]")) return;
            if (type == LogType.Error || type == LogType.Exception) { errors++; log.AppendLine($"[real {Real,6:0.00}] ERROR {c.Split('\n')[0]}"); }
        }

        static int GpuTemp()
        {
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = a.GetType("OpenZ.EditorTools.GpuGuard"); if (t == null) continue;
                var p = t.GetProperty("LastTemp"); return p != null ? (int)p.GetValue(null) : -1;
            }
            return -1;
        }
        IEnumerator Heat()
        {   // the same guard as the Play proofs: stop at 90 C, or at 86 C held 30 s; a watchdog when nothing moves on for 90 s
            while (!done)
            {
                int gpu = GpuTemp(); peak = Mathf.Max(peak, gpu);
                float step = lastSample < 0f ? 0.5f : Real - lastSample; lastSample = Real;
                if (gpu >= 90) { Finish("GPU 90 C"); yield break; }
                if (gpu >= 86) { hot = hot < 0f ? step : hot + step; if (hot >= 30f) { Finish("GPU >= 86 C for 30 s"); yield break; } }
                else if (gpu >= 0 && gpu < 84) hot = -1f;
                if (Real - lastMark > 90f) { Finish("watchdog: no progress for 90 s"); yield break; }
                yield return new WaitForSecondsRealtime(0.5f);
            }
        }

        // ================================================================================================ the level
        ZGame g; ZWorld W; RtsCamera cam; DayNight dn; Camera ucam; float h0;

        IEnumerator Start()
        {
            take = TakeName(job); sub = Path.Combine(Dir, "films_" + job);
            Directory.CreateDirectory(sub); lastMark = Real;
            Note($"{S.Label} v2 film '{job}', scene {SceneManager.GetActiveScene().name}, switch Version {S.Version()}, v2 prefab {(S.PrefabV2() ? "found" : "MISSING")}");   // k84: the unit filmed
            StartCoroutine(Heat());
            float w0 = Real;
            while ((!ZGame.I || ZGame.I.World == null) && Real - w0 < 90f) yield return null;
            if (!ZGame.I || ZGame.I.World == null) { Finish("the level never came up within 90 s"); yield break; }
            g = ZGame.I; W = g.World; cam = FindFirstObjectByType<RtsCamera>(); dn = FindFirstObjectByType<DayNight>();
            ucam = cam ? cam.GetComponent<Camera>() : null; if (!ucam) ucam = Camera.main;
            if (!cam || !ucam) { Finish("no RTS camera"); yield break; }
            h0 = job == "board_close" ? CloseHeight : PlayHeight;                                        // k81c: the game's default play zoom, never the level's wide start view
            if (g.level != "L02") { SpotFail($"the films' fixed spots are for L02, this is {g.level}"); Finish("spot check failed"); yield break; }
            int bots = W.Bots.Count; W.Bots.Clear();
            yield return SkipBriefing();
            if (done) yield break;
            if (dn) { dn.running = false; dn.timeOfDay = 0.08f; dn.Apply(0.08f); }
            Note($"level {g.level}, the RTS camera at {h0:0.0} m for the whole take ({(job == "board_close" ? "close, picture a" : "the game's default play zoom")}), on the subject; computer players cleared ({bots}); the day held at 0.08");
            if (!S.PrefabV2()) { Finish("no v2 prefab (" + S.BuildMenu + ")"); yield break; }
            switch (job)
            {
                case "tread": yield return Tread(); break;
                case "battle": yield return Battle(); break;
                case "board": case "board_close": yield return Board(); break;
                case "fight": yield return Fight(); break;
                case "snipe": yield return SnipeK81d(); break;                                       // k81d, item 130: the driver up and firing before the sniper gets him
                case "turret": yield return Turret(); break;
                case "recoil": yield return Recoil(); break;
                case "combat": yield return Combat(); break;
                case "plant": yield return PlantK81d(); break;                                       // k81d, items 137 / 146: the fair track test (the bank, the stand, the switch frame apart)
                case "factory": yield return MTankFactory(); break;                               // DV145: the medium tank built after the mech, driving off (View/HeavyV2Films.MTank.cs)
                case "lineup": yield return MTankLineup(); break;                                 // DV145: light / medium / heavy side by side
                case "turn_k87d": yield return TurnK87d(); break;                                 // k87d, item 181: the light and the medium tank turn onto targets and fire (View/HeavyV2Films.K87d.cs)
                case "rockets": yield return MlrsRockets(); break;                                 // 1.2 v2 units: every MLRS rocket leaves its own tube (View/HeavyV2Films.Mlrs.cs)
                case "launcher": yield return MlrsLauncher(); break;                               // 1.2 v2 units: the MLRS launcher kick and hull rock, v1 and v2 side by side
                case "walk": yield return MechWalkTake(false); break;                              // 1.2 v2 units: the mech's feet stay planted, only its body rocks, v1 and v2 side by side (View/HeavyV2Films.Mech.cs)
                case "walk_close": yield return MechWalkTake(true); break;                         // 1.2 v2 units: the same walk, the camera at 32 m on the mech v2
                case "gun_side": yield return GunSideTake(false); break;                           // 1.2 v2 units: the gun v1 and v2 side by side, the turret's turn and kick, the base still (View/HeavyV2Films.Gun.cs)
                case "gun_close": yield return GunSideTake(true); break;                           // 1.2 v2 units: the same at 32 m on the gun v2
                case "gun_board": yield return GunBoardTake(false); break;                         // 1.2 v2 units: a robot mans an empty gun v2
                case "gun_board_close": yield return GunBoardTake(true); break;                    // 1.2 v2 units: the same, the camera at 32 m
                case "gun_fight": yield return GunFightTake(); break;                              // 1.2 v2 units: one gun v2 fights to its death
                case "gun_turret": yield return GunTurretTake(); break;                            // 1.2 v2 units: the gun's turret never shakes between targets (lesson 5)
                case "gun_death": case "gun_death_close": yield return GunDeathTakeK97b(job == "gun_death_close"); break;   // k97b, item 176: a v1 and a v2 gun die side by side, their parts fly at the death (View/HeavyV2Films.K97b.cs)
                default: Finish("unknown job '" + job + "' (" + string.Join(", ", Jobs) + ")"); yield break;
            }
            Finish(spotFailed ? "spot check failed (see SPOT FAIL)" : null);
        }

        IEnumerator SkipBriefing()
        {
            float w0 = Real; bool aborted = false; var pf = typeof(ZodBriefing).GetField("perf", BindingFlags.NonPublic | BindingFlags.Instance);
            double sim0 = W.Time;
            while (Real - w0 < 45f && !done)
            {
                if (ZodBriefing.BriefingActive)
                {
                    if (!aborted && ZodBriefing.I && pf != null && Real - w0 > 0.6f)
                    {
                        var p = pf.GetValue(ZodBriefing.I);
                        if (p != null) { p.GetType().GetMethod("Abort").Invoke(p, new object[] { 0.22f }); aborted = true; Note("briefing: Abort(0.22 s) sent"); }
                    }
                }
                else if (!g.paused && W.Time > sim0 + 0.5) break;
                yield return null;
            }
            Note($"briefing over after {Real - w0:0.0} s");
        }

        IEnumerator Hold(float s) { float end = Real + s; while (Real < end && !done) yield return null; }

        // ================================================================================================ places (fixed) and units
        /// <summary>The spot table's job name of this film (the close boarding take stands where the boarding take stands).</summary>
        string SpotJob => job == "board_close" ? "board" : job;
        bool spotFailed;
        /// <summary>A fixed tile of this film (View/HeavyV2Spots.cs, chosen off the editor on the whole-map scan): none = the take FAILS.</summary>
        bool Spot(string what, out int x, out int y, string sjob = null)
        {
            if (HeavyV2Spots.Get(g.level, sjob ?? SpotJob, what, out x, out y)) { Note($"fixed spot {g.level}/{sjob ?? SpotJob}/{what}: tile ({x},{y})"); return true; }
            SpotFail($"no fixed tile '{g.level}/{sjob ?? SpotJob}/{what}' in HeavyV2Spots"); return false;
        }
        void SpotFail(string why)
        {
            if (spotFailed) return;
            spotFailed = true;
            Fact("SPOT FAIL: " + why + ": FAIL (the take is stopped; regenerate View/HeavyV2Spots.cs with the kit's tools/headless/run_scan.py and tools/choose_spots.py)");
        }
        /// <summary>A heavy slot asked again with the sim's own tests: the footprint passes (ZPathFinder.StepOk) and, when `open` (a heavy whose
        /// lid and driver the film shows), it is no bridge / door spot (ZWorld.SnipeSafeSpot). A failed slot FAILS the take.</summary>
        bool CheckHeavy(ZObject o, string tag, bool open)
        {
            if (o == null) { SpotFail(tag + ": not made"); return false; }
            int tx = o.LocX >> 4, ty = o.LocY >> 4;
            bool foot = W.Path.StepOk(tx, ty, tx, ty, false, false), prot = W.SnipeSafeSpot(o);
            Note($"spot check {tag}: tile ({tx},{ty}), footprint {(foot ? "passes" : "BLOCKED")}, {(prot ? "ON A BRIDGE / DOOR SPOT" : "no bridge / door spot")}");
            if (!foot || (open && prot)) { SpotFail($"{tag} at tile ({tx},{ty}): footprint {(foot ? "passes" : "blocked")}, bridge / door spot {prot}"); return false; }
            return true;
        }

        readonly List<(ZObject o, string tag)> cast = new List<(ZObject, string)>();
        ZObject Heavy(int tx, int ty, Team t, int model, string tag)
        {
            var o = W.CreateObject("vehicle", S.Type, tx * 16, ty * 16, t);                          // k84: the unit filmed (heavy or light)
            S.Pin(o, model); g.SpawnUnitView(o); cast.Add((o, tag));
            Note($"{tag}: {t} {S.Label} drawn as v{model}, id {o.Id}, tile ({tx},{ty}){(W.SnipeSafeSpot(o) ? ", ON A BRIDGE / DOOR SPOT" : "")}");
            return o;
        }
        ZObject Vehicle(string type, int tx, int ty, Team t, string tag)
        {
            var o = W.CreateObject("vehicle", type, tx * 16, ty * 16, t);
            if (type == OzHeavyTank.Type) OzHeavyTank.Pin(o, 1);                                       // an enemy heavy is v1: v2 stays the one to watch
            g.SpawnUnitView(o); cast.Add((o, tag));
            Note($"{tag}: {t} {type}{(type == OzHeavyTank.Type ? " (v1)" : "")}, id {o.Id}, tile ({tx},{ty})");
            return o;
        }
        ZObject Squad(string type, int tx, int ty, Team t, int amount, string tag)
        {
            if (!OzCheatSpawner.FreeTile(W, tx, ty, 1, true, out int fx, out int fy)) { fx = tx; fy = ty; }
            var o = W.CreateRobotGroup(type, fx * 16, fy * 16, t, amount);
            g.SpawnUnitView(o); foreach (var m in o.Minions) g.SpawnUnitView(m);
            cast.Add((o, tag));
            Note($"{tag}: {t} {type} x{1 + o.Minions.Count}, leader id {o.Id}, tile ({fx},{fy})");
            return o;
        }
        bool Order(ZObject o, ZWaypoint wp) => o != null && !o.Destroyed && W.SendWaypoints(o.Owner, o, new List<ZWaypoint> { wp });
        static ZWaypoint Enter(ZObject v) => new ZWaypoint { mode = WpMode.Enter, refId = v.Id, x = v.CenterX, y = v.CenterY };
        static ZWaypoint Attack(ZObject t) => new ZWaypoint { mode = WpMode.Attack, refId = t.Id, x = t.CenterX, y = t.CenterY };
        static ZWaypoint MoveTo(int tx, int ty, bool fight) => ZWaypoint.Move(tx * 16 + 8, ty * 16 + 8, fight);
        UnitView View(ZObject o) => o != null && g.Views.TryGetValue(o.Id, out var v) ? v : null;
        static Transform Node(UnitView v, string name)
        {
            if (!v) return null;
            foreach (var t in v.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }
        static Vector3 At(ZObject o) => ZGame.PxToWorld(o.CenterX, o.CenterY);

        // ---------------------------------------------------------------- BoardingView's rig of a vehicle (read only, by reflection)
        static readonly FieldInfo RigsField = typeof(BoardingView).GetField("rigs", BindingFlags.NonPublic | BindingFlags.Instance);
        static object RigOf(UnitView v)
        {
            if (!v || !BoardingView.I || RigsField == null) return null;
            var d = RigsField.GetValue(BoardingView.I) as IDictionary;
            return d != null && d.Contains(v) ? d[v] : null;
        }
        static T RigGet<T>(object rig, string field, T fallback)
        {
            var f = rig?.GetType().GetField(field); if (f == null) return fallback;
            var o = f.GetValue(rig); return o is T t ? t : fallback;
        }
        /// <summary>The lid's angle from its rest (deg), the hatch commander up (BoardingView: shown and fully risen) and his top over the rim (m).</summary>
        static float LidAngle(UnitView v, out bool commander, out float topOverRim)
        {
            commander = false; topOverRim = -9f;
            var rig = RigOf(v); if (rig == null) return 0f;
            var hinge = RigGet<Transform>(rig, "hinge", null);
            float a = hinge ? Quaternion.Angle(RigGet(rig, "hingeRest", hinge.localRotation), hinge.localRotation) : 0f;
            var crew = RigGet<GameObject>(rig, "crew", null);
            if (crew && crew.activeInHierarchy && RigGet(rig, "crewPop", 0f) >= 0.99f)
            {
                commander = true; float top = -1e9f;
                foreach (var r in crew.GetComponentsInChildren<Renderer>()) if (r.enabled) top = Mathf.Max(top, r.bounds.max.y);
                var round = RigGet<RoundHatch>(rig, "round", null);
                topOverRim = top - (round ? round.RimWorld.y : crew.transform.position.y);
            }
            return a;
        }
        static int Boarding(UnitView v) => RigGet(RigOf(v), "boarding", 0);
        /// <summary>The scroll (V offset, tiles of 4 m) each belt's DRAW uses (its material slot block, VehicleMotion.DrawnScroll; k81c: the
        /// whole-renderer block that k81 / k81b read is not what the draw uses): "L / R".</summary>
        static string TreadV(UnitView v)
        {
            if (!v) return "-";
            var sb = new StringBuilder();
            foreach (var n in S.Tracks)
            {
                var t = Node(v, n); var r = t ? t.GetComponent<Renderer>() : null;
                if (!r) { sb.Append("- "); continue; }
                float w = VehicleMotion.DrawnScroll(r); sb.Append(float.IsNaN(w) ? "none" : w.ToString("0.000")).Append(' ');
            }
            return sb.ToString().Trim().Replace(' ', '/');
        }
        static string Paint(UnitView v) { var tp = v ? v.GetComponent<TeamPaint>() : null; return tp ? tp.Team.ToString() : "-"; }

        // ================================================================================================ the take (frames + sound in step)
        bool recording, audioOn; int frameNo, renderFails, rate, ch, per; float peak0, soundT0; string framesDir;
        readonly List<float> samples = new List<float>();
        ZObject hero; readonly List<ZObject> follow = new List<ZObject>();
        IEnumerator StartTake()
        {
            OzFinalGateView.SetFixed(1920, 1080, "OpenZ heavy v2 film 1920x1080", out var info); Note("Game view: " + info);
            float tw = Real;
            while ((Screen.width != 1920 || Screen.height != 1080) && Real - tw < 6f) yield return null;
            Fact($"the Game view is {Screen.width} x {Screen.height} for the take (1920 x 1080 wanted): {(Screen.width == 1920 && Screen.height == 1080 ? "PASS" : "FAIL")}");
            int n = 1; while (Directory.Exists(Path.Combine(sub, "take_" + n))) n++;
            framesDir = Path.Combine(sub, "take_" + n); Directory.CreateDirectory(framesDir);           // a new folder per take: nothing is overwritten
            rate = AudioSettings.outputSampleRate; ch = Channels(AudioSettings.speakerMode); per = rate / Fps;
            samples.Clear(); frameNo = 0; renderFails = 0; peak0 = 0f; soundT0 = Time.unscaledTime;
            if (cam) { cam.SetHeightImmediate(h0); if (Aim(out var at)) cam.focus = at; }
            Time.captureFramerate = Fps; audioOn = AudioRenderer.Start();
            Note($"sound capture {(audioOn ? "started" : "NOT started")} ({rate} Hz, {ch} channels); frames into {framesDir}");
            recording = true;
            StartCoroutine(Grabber()); StartCoroutine(Director());
            yield return null;
        }
        static int Channels(AudioSpeakerMode m)
        {
            switch (m) { case AudioSpeakerMode.Mono: return 1; case AudioSpeakerMode.Quad: return 4; case AudioSpeakerMode.Surround: return 5; case AudioSpeakerMode.Mode5point1: return 6; case AudioSpeakerMode.Mode7point1: return 8; default: return 2; }
        }
        IEnumerator TakeWait(float s) { int end = frameNo + Mathf.RoundToInt(s * Fps); while (frameNo < end && !done && recording) yield return null; }
        IEnumerator Grabber()
        {
            while (recording)
            {
                yield return new WaitForEndOfFrame();
                if (!recording) break;
                if (Time.captureFramerate <= 0) continue;
                var tex = ScreenCapture.CaptureScreenshotAsTexture();
                try { OnFrame(tex); } catch (Exception e) { Note("crop failed: " + e.Message); }
                File.WriteAllBytes(Path.Combine(framesDir, $"f_{frameNo:00000}.jpg"), tex.EncodeToJPG(Jpg)); Destroy(tex);
                if (audioOn)
                {
                    int avail = AudioRenderer.GetSampleCountForCaptureFrame();
                    using (var nb = new NativeArray<float>(Mathf.Max(0, avail) * ch, Allocator.Temp))
                    {
                        if (!AudioRenderer.Render(nb)) renderFails++;
                        for (int k = 0; k < nb.Length; k++) { float s = nb[k]; samples.Add(s); if (Mathf.Abs(s) > peak0) peak0 = Mathf.Abs(s); }
                    }
                }
                frameNo++;
            }
        }

        /// <summary>Every frame of the take: the camera follows (the level's own height), and 10 times a second a state line.</summary>
        IEnumerator Director()
        {
            int lastLine = -100;
            while (recording && !done)
            {
                if (cam && Aim(out var want)) cam.focus = Vector3.Lerp(cam.focus, want, 1f - Mathf.Exp(-6f * Mathf.Max(0.001f, Time.deltaTime)));   // k81c: the subject stays centred
                if (frameNo - lastLine >= 3)
                {
                    lastLine = frameNo; lastMark = Real;
                    if (hero != null)
                    {
                        var v = View(hero);
                        float lid = LidAngle(v, out bool up, out float over);
                        log.AppendLine($"[take {TakeT,6:0.00}] hero {hero.Owner} hp {100f * hero.Health / Mathf.Max(1, hero.MaxHealth):0}% {(hero.Destroyed ? "DESTROYED " : "")}pos {hero.CenterX},{hero.CenterY} moving {hero.IsMoving} lid(sim) {hero.LidOpen} lid {lid:0} deg commander {(up ? $"up {over:0.00} m" : "-")} drivers {hero.Drivers.Count}{(hero.Drivers.Count > 0 ? " (" + hero.Drivers[0] + " hp)" : "")} driver shots {hero.DriverShots}/{hero.DriverHits} boarding {Boarding(v)} paint {Paint(v)} tread {TreadV(v)} | {Census()}");
                    }
                }
                yield return null;
            }
        }
        /// <summary>Where the camera looks: the middle of the followed units still in the world, else the main tank (its last place).</summary>
        bool Aim(out Vector3 at)
        {
            at = Vector3.zero; int n = 0;
            foreach (var o in follow) if (o != null && W.Get(o.Id) == o) { at += At(o); n++; }
            if (n == 0 && hero != null) { at = At(hero); n = 1; }
            if (n == 0) return false;
            at /= n; at.y = 0f;
            return true;
        }
        string Census()
        {
            int rv = 0, rr = 0, bv = 0, br = 0;
            foreach (var (o, _) in cast)
            {
                if (o == null) continue;
                foreach (var m in Members(o))
                {
                    if (m.Destroyed || W.Get(m.Id) != m) continue;
                    bool veh = m.Kind == ZKind.Vehicle;
                    if (m.Owner == Team.Red) { if (veh) rv++; else rr++; } else if (m.Owner == Team.Blue) { if (veh) bv++; else br++; }
                }
            }
            return $"red {rv} vehicles {rr} robots, blue {bv} vehicles {br} robots";
        }
        static IEnumerable<ZObject> Members(ZObject o) { yield return o; foreach (var m in o.Minions) yield return m; }

        void StopTake()
        {
            if (!recording) return;
            recording = false;
            if (audioOn) { AudioRenderer.Stop(); audioOn = false; }
            Time.captureFramerate = 0;
            string wav = Path.Combine(sub, take + "_take.wav");
            int want = frameNo * per * ch;
            while (samples.Count < want) samples.Add(0f);
            if (samples.Count > want) samples.RemoveRange(want, samples.Count - want);
            using (var fs = new FileStream(wav, FileMode.Create)) using (var bw = new BinaryWriter(fs))
            {
                int bytes = samples.Count * 4;
                bw.Write(Encoding.ASCII.GetBytes("RIFF")); bw.Write(36 + bytes); bw.Write(Encoding.ASCII.GetBytes("WAVE")); bw.Write(Encoding.ASCII.GetBytes("fmt ")); bw.Write(16); bw.Write((short)3); bw.Write((short)ch); bw.Write(rate); bw.Write(rate * ch * 4); bw.Write((short)(ch * 4)); bw.Write((short)32);
                bw.Write(Encoding.ASCII.GetBytes("data")); bw.Write(bytes); foreach (var s in samples) bw.Write(s);
            }
            Fact($"take: {frameNo} frames at {Fps} ({TakeT:0.0} s), sound peak {peak0:0.000}, render fails {renderFails}: {(frameNo > Fps * 5 && peak0 > 0.01f && renderFails == 0 ? "PASS" : "FAIL")} (frames and sound)");
            Fact($"sounds in the take (sound log): {S.ShotKey} {Count(S.ShotKey)}, exp_large {Count("exp_large")}, dst_turret_toss {Count("dst_turret_toss")}, hatch {Count("hatch")}");
            string ff = FfmpegExe();
            if (ff == null) { Fact("ffmpeg NOT found: join by hand (" + framesDir + " + " + wav + ")"); return; }
            string mp4 = Path.Combine(Dir, take + ".mp4").Replace('\\', '/'), sheet = Path.Combine(Dir, take + "_sheet.jpg").Replace('\\', '/');
            bool ok = Ffmpeg(ff, $"-y -framerate {Fps} -i \"{framesDir.Replace('\\', '/')}/f_%05d.jpg\" -i \"{wav.Replace('\\', '/')}\" -c:v libx264 -preset medium -crf 17 -pix_fmt yuv420p -vf scale=1920:1080:flags=lanczos -c:a aac -b:a 192k -movflags +faststart -shortest \"{mp4}\"", 600000) && File.Exists(mp4) && new FileInfo(mp4).Length > 50000;
            if (ok)
            {
                var inv = System.Globalization.CultureInfo.InvariantCulture;
                float secs = Mathf.Max(1f, TakeT); float rateS = Mathf.Min(2f, 168f / secs); int rows = Mathf.Clamp(Mathf.CeilToInt(secs * rateS / 6f), 1, 28);
                Ffmpeg(ff, $"-y -i \"{mp4}\" -vf \"fps={rateS.ToString("0.###", inv)},scale=320:-1,tile=6x{rows}\" -frames:v 1 -q:v 3 \"{sheet}\"", 180000);
                Fact($"the MP4 is written: {mp4} (1920 x 1080, {Fps} fps, with the game's sound); frame sheet {sheet} ({rateS:0.##} frames a second, 6 across, read left to right)");
            }
            else Fact("the MP4 was NOT written: " + mp4);
        }
        int Count(string key) { int n = 0; foreach (var e in OzSoundLog.Since(soundT0)) if (e.key == key && e.outcome != null && e.outcome.StartsWith("PLAY")) n++; return n; }
        static string FfmpegExe()
        {
            var env = Environment.GetEnvironmentVariable("OPENZ_FFMPEG"); if (!string.IsNullOrEmpty(env) && File.Exists(env)) return env;
            foreach (var d in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator)) { try { var p = Path.Combine(d.Trim('"'), "ffmpeg.exe"); if (File.Exists(p)) return p; } catch (Exception) { } }
            var la = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var link = Path.Combine(la, "Microsoft", "WinGet", "Links", "ffmpeg.exe"); if (File.Exists(link)) return link;
            var pk = Path.Combine(la, "Microsoft", "WinGet", "Packages");
            if (Directory.Exists(pk)) foreach (var d in Directory.GetDirectories(pk, "*FFmpeg*")) foreach (var f in Directory.GetFiles(d, "ffmpeg.exe", SearchOption.AllDirectories)) return f;
            return null;
        }
        bool Ffmpeg(string exe, string args, int waitMs)
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo(exe, args) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true };
                using (var p = System.Diagnostics.Process.Start(psi))
                {
                    string err = p.StandardError.ReadToEnd();
                    if (!p.WaitForExit(waitMs)) { p.Kill(); Note("ffmpeg timed out: " + args); return false; }
                    if (p.ExitCode != 0) { Note("ffmpeg exit " + p.ExitCode + ": " + (err.Length > 500 ? err.Substring(err.Length - 500) : err)); return false; }
                    return true;
                }
            }
            catch (Exception e) { Note("ffmpeg failed: " + e.Message); return false; }
        }

        // ================================================================================================ crops
        // hero crops: every 15 frames (0.5 s) a crop round the main tank, 320 x 240 cells, 6 across; tread crops: both track ends, 10 crops
        // 0.1 s apart (every 3rd frame) on each straight leg, 4 x nearest
        const int CellW = 320, CellH = 240, Across = 6;
        readonly List<(Color32[] px, string what)> heroCrops = new List<(Color32[], string)>();
        int nextHeroCrop;
        readonly List<List<(Color32[] px, int w, int h)>> treadRows = new List<List<(Color32[], int, int)>>();
        readonly List<string> treadLabels = new List<string>();
        int treadLeft, treadEvery = 3, treadNext; List<(Color32[], int, int)> treadFront, treadRear; UnitView treadView;

        void OnFrame(Texture2D tex)
        {
            SampleYaw(); SampleRecoil(tex); SamplePlant(); SampleMlrs(tex); SampleMech(tex); SampleGun(tex);   // items 132 / 132b / 137: every frame of the take, after every LateUpdate; 1.2 v2 units: the MLRS launcher (View/HeavyV2Films.Mlrs.cs); the mech's feet (View/HeavyV2Films.Mech.cs); the gun (View/HeavyV2Films.Gun.cs)
            if (hero != null && frameNo >= nextHeroCrop)
            {
                nextHeroCrop = frameNo + 15;
                var v = View(hero);
                Rect r = v ? ScreenBox(v.gameObject, 0.45f) : ScreenBox(At(hero), 6f);
                float lid = LidAngle(v, out bool up, out float over);
                heroCrops.Add((Cell(tex, r, CellW, CellH, false), $"crop {heroCrops.Count} at {TakeT:0.0} s: {hero.Owner}, hp {100f * hero.Health / Mathf.Max(1, hero.MaxHealth):0}%{(hero.Destroyed ? " DESTROYED" : "")}, lid {lid:0} deg, commander {(up ? $"up ({over:0.00} m over the rim)" : "-")}, boarding {Boarding(v)}, paint {Paint(v)}, drivers {hero.Drivers.Count}"));
            }
            if (treadLeft > 0 && treadView && frameNo >= treadNext)
            {
                treadNext = frameNo + treadEvery; treadLeft--;
                foreach (var front in new[] { true, false })
                {
                    var r = TrackEnd(treadView, front);
                    int w = Mathf.Max(8, Mathf.RoundToInt(r.width)), h = Mathf.Max(8, Mathf.RoundToInt(r.height));
                    (front ? treadFront : treadRear).Add((Cell(tex, r, w * 4, h * 4, true), w * 4, h * 4));
                }
                if (treadLeft == 0) { treadRows.Add(treadFront); treadRows.Add(treadRear); }
            }
        }

        /// <summary>Starts 10 track-end crops 0.1 s apart (both ends).</summary>
        void TreadCrops(UnitView v, string label)
        {
            treadView = v; treadLeft = 10; treadNext = frameNo; treadFront = new List<(Color32[], int, int)>(); treadRear = new List<(Color32[], int, int)>();
            treadLabels.Add(label + " / front end"); treadLabels.Add(label + " / rear end");
        }

        /// <summary>Screen rect (pixels from the bottom left, as the captured texture) of one track end of both belts: the front end
        /// (the belt's first 0.95 m) or the rear end (its last 1.1 m), from the belt meshes' own bounds (front = -z), + 4 px.</summary>
        Rect TrackEnd(UnitView v, bool front)
        {
            float x0 = 1e9f, y0 = 1e9f, x1 = -1e9f, y1 = -1e9f;
            foreach (var n in S.Tracks)
            {
                var t = Node(v, n); var mf = t ? t.GetComponent<MeshFilter>() : null;
                if (!mf || !mf.sharedMesh) continue;
                var b = mf.sharedMesh.bounds;
                float za = front ? b.min.z : b.max.z - 1.1f, zb = front ? b.min.z + 0.95f : b.max.z;
                for (int i = 0; i < 8; i++)
                {
                    var p = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? za : zb);
                    var s = ucam.WorldToScreenPoint(t.TransformPoint(p));
                    if (s.z <= 0f) continue;
                    x0 = Mathf.Min(x0, s.x); x1 = Mathf.Max(x1, s.x); y0 = Mathf.Min(y0, s.y); y1 = Mathf.Max(y1, s.y);
                }
            }
            if (x1 < x0) return new Rect(0, 0, 8, 8);
            return Rect.MinMaxRect(x0 - 4f, y0 - 4f, x1 + 4f, y1 + 4f);
        }
        Rect ScreenBox(GameObject go, float grow)
        {
            bool any = false; var b = new Bounds();
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            if (!any) return ScreenBox(go.transform.position, 6f);
            float x0 = 1e9f, y0 = 1e9f, x1 = -1e9f, y1 = -1e9f;
            for (int i = 0; i < 8; i++)
            {
                var c = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) * 2 - 1, (i & 2) - 1, (i & 4) / 2 - 1));
                var s = ucam.WorldToScreenPoint(c);
                x0 = Mathf.Min(x0, s.x); x1 = Mathf.Max(x1, s.x); y0 = Mathf.Min(y0, s.y); y1 = Mathf.Max(y1, s.y);
            }
            float gx = (x1 - x0) * grow, gy = (y1 - y0) * grow;
            return Rect.MinMaxRect(x0 - gx, y0 - gy, x1 + gx, y1 + gy);
        }
        Rect ScreenBox(Vector3 world, float metres)
        {
            var s = ucam.WorldToScreenPoint(world); var e = ucam.WorldToScreenPoint(world + ucam.transform.right * metres);
            float r = Mathf.Max(20f, (e - s).magnitude);
            return new Rect(s.x - r, s.y - r * 0.75f, 2f * r, 1.5f * r);
        }
        /// <summary>The rect of the frame scaled into a w x h cell (the aspect kept, the rest dark; nearest = pixels kept sharp).</summary>
        static Color32[] Cell(Texture2D src, Rect r, int w, int h, bool nearest)
        {
            var px = new Color32[w * h]; var dark = new Color32(12, 12, 14, 255);
            float sc = Mathf.Min(w / Mathf.Max(1f, r.width), h / Mathf.Max(1f, r.height));
            float ox = (w - r.width * sc) * 0.5f, oy = (h - r.height * sc) * 0.5f;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float sx = r.x + (x + 0.5f - ox) / sc, sy = r.y + (y + 0.5f - oy) / sc;
                    if (sx < r.x || sy < r.y || sx > r.xMax || sy > r.yMax || sx < 0 || sy < 0 || sx >= src.width || sy >= src.height) { px[y * w + x] = dark; continue; }
                    px[y * w + x] = nearest ? (Color32)src.GetPixel((int)sx, (int)sy) : (Color32)src.GetPixelBilinear(sx / src.width, sy / src.height);
                }
            return px;
        }
        void WriteCrops()
        {
            WriteRecoilCrops();
            if (heroCrops.Count > 0)
            {
                int rows = Mathf.CeilToInt(heroCrops.Count / (float)Across);
                var t = new Texture2D(Across * (CellW + 4), rows * (CellH + 4), TextureFormat.RGB24, false);
                var fill = new Color32[t.width * t.height]; for (int i = 0; i < fill.Length; i++) fill[i] = new Color32(0, 0, 0, 255); t.SetPixels32(fill);
                for (int i = 0; i < heroCrops.Count; i++)
                {
                    int cx = (i % Across) * (CellW + 4), cy = t.height - (i / Across + 1) * (CellH + 4);   // row 0 at the top, read left to right
                    t.SetPixels32(cx, cy, CellW, CellH, heroCrops[i].px);
                }
                t.Apply(false);
                string p = Path.Combine(Dir, take + "_crops.png");
                File.WriteAllBytes(p, t.EncodeToPNG()); Destroy(t);
                Fact($"crops round the main tank every 0.5 s: {p} ({heroCrops.Count} crops, {Across} across, read left to right, top to bottom; each crop's state below)");
                foreach (var c in heroCrops) log.AppendLine("  " + c.what);
            }
            if (treadRows.Count > 0)
            {
                int cw = 0, chh = 0; foreach (var row in treadRows) foreach (var c in row) { cw = Mathf.Max(cw, c.Item2); chh = Mathf.Max(chh, c.Item3); }
                var t = new Texture2D(10 * (cw + 6), treadRows.Count * (chh + 6), TextureFormat.RGB24, false);
                var fill = new Color32[t.width * t.height]; for (int i = 0; i < fill.Length; i++) fill[i] = new Color32(0, 0, 0, 255); t.SetPixels32(fill);
                for (int ri = 0; ri < treadRows.Count; ri++)
                    for (int ci = 0; ci < treadRows[ri].Count && ci < 10; ci++)
                    {
                        var c = treadRows[ri][ci];
                        t.SetPixels32(ci * (cw + 6), t.height - (ri + 1) * (chh + 6), c.Item2, c.Item3, c.Item1);
                    }
                t.Apply(false);
                string p = Path.Combine(Dir, S.Prefix + "_tread_crops_L02.png");
                File.WriteAllBytes(p, t.EncodeToPNG()); Destroy(t);
                Fact($"track-end crops 0.1 s apart from the RTS camera (x4, nearest): {p}; rows top to bottom:");
                for (int i = 0; i < treadLabels.Count; i++) log.AppendLine($"  row {i + 1}: {treadLabels[i]}");
            }
        }

        // ================================================================================================ the jobs
        /// <summary>Item 129: one red v2 drives four straight legs (away from the camera: the rear end faces it; toward it: the front end;
        /// east and west: the sides); on each leg, once it runs straight, 10 crops of both track ends 0.1 s apart.</summary>
        IEnumerator Tread()
        {
            if (!Spot("v2", out int cx, out int cy) || !Spot("north", out int nx, out int ny) || !Spot("east", out int ex, out int ey)) yield break;
            hero = Heavy(cx, cy, Team.Red, 2, "tread v2");
            if (!CheckHeavy(hero, "tread v2", false)) yield break;
            hero.AttackRadius = 0;                                                                      // staged: it does not stop to fight
            Note("staged: the tank's attack radius 0 (it drives its legs without stopping to fight)");
            yield return Hold(2.5f);
            yield return StartTake();
            yield return TakeWait(1.0f);
            var legs = new[] { ("north, away from the camera", nx, ny), ("south, toward the camera", cx, cy), ("east", ex, ey), ("west", cx, cy) };
            foreach (var (label, tx, ty) in legs)
            {
                bool sent = Order(hero, MoveTo(tx, ty, false));
                Note($"leg {label}: to tile ({tx},{ty}), order {(sent ? "taken" : "REFUSED")}, tread {TreadV(View(hero))}");
                int sx = hero.CenterX, sy = hero.CenterY; int startF = frameNo; bool cropped = false; int still = 0;
                while (!done && frameNo - startF < 16 * Fps)
                {
                    float moved = Mathf.Sqrt((hero.CenterX - sx) * (hero.CenterX - sx) + (hero.CenterY - sy) * (hero.CenterY - sy));
                    if (!cropped && moved > 28f && hero.IsMoving) { TreadCrops(View(hero), label); cropped = true; Note($"leg {label}: crops start, tread {TreadV(View(hero))}"); }
                    still = hero.IsMoving ? 0 : still + 1;
                    if (cropped && treadLeft == 0 && still > 15) break;
                    yield return null;
                }
                Note($"leg {label}: done, moved to {hero.CenterX},{hero.CenterY}, tread {TreadV(View(hero))}{(cropped ? "" : ", NO crops (it never ran straight long enough)")}");
                yield return TakeWait(0.5f);
            }
            yield return TakeWait(0.5f);
            StopTake(); WriteCrops();
            Fact("pass rule (crops): on each leg the belt that faces the camera (its end and its side) is dark grey-black like the reference, no light or white stripes, and its dark-on-dark links step the same way from crop to crop, about 2 to 3 px at a time: toward the ground at the front end and up from it at the rear end while the tank drives forward; never a pattern that stands still or flickers back and forth; the tread values in the log (what the draw uses) change on every leg");
        }

        /// <summary>
        /// One v2 starts at 40 % (staged, logged) so one of them is lost in the fight; the camera follows the red heavies.</summary>
        IEnumerator Battle()
        {
            var names = new[] { "red v2 A", "red v2 B", "red v2 C", "red grunts", "blue light 1", "blue light 2", "blue medium", "blue heavy", "blue toughs", "blue grunts", "red goal", "blue goal" };
            var at = new Dictionary<string, (int x, int y)>();
            foreach (var n in names) { if (!Spot(n, out int sx, out int sy)) yield break; at[n] = (sx, sy); }
            var a = Heavy(at["red v2 A"].x, at["red v2 A"].y, Team.Red, 2, "red v2 A"); var b = Heavy(at["red v2 B"].x, at["red v2 B"].y, Team.Red, 2, "red v2 B");
            var c = Heavy(at["red v2 C"].x, at["red v2 C"].y, Team.Red, 2, "red v2 C (starts at 40 %)");
            foreach (var (o, t) in new[] { (a, "red v2 A"), (b, "red v2 B"), (c, "red v2 C") }) if (!CheckHeavy(o, t, false)) yield break;
            c.SetHealth(c.MaxHealth / 100 * 40); W.UpdateObjectHealth(c);
            var rg = Squad("grunt", at["red grunts"].x, at["red grunts"].y, Team.Red, -1, "red grunts");
            var e1 = Vehicle("light", at["blue light 1"].x, at["blue light 1"].y, Team.Blue, "blue light 1"); var e2 = Vehicle("light", at["blue light 2"].x, at["blue light 2"].y, Team.Blue, "blue light 2");
            var e3 = Vehicle("medium", at["blue medium"].x, at["blue medium"].y, Team.Blue, "blue medium"); var e4 = Vehicle(OzHeavyTank.Type, at["blue heavy"].x, at["blue heavy"].y, Team.Blue, "blue heavy");
            var e5 = Squad("tough", at["blue toughs"].x, at["blue toughs"].y, Team.Blue, -1, "blue toughs"); var e6 = Squad("grunt", at["blue grunts"].x, at["blue grunts"].y, Team.Blue, -1, "blue grunts");
            hero = c; follow.AddRange(new[] { a, b, c });
            TrackYaw(a, "red v2 A"); TrackYaw(b, "red v2 B"); TrackYaw(c, "red v2 C"); yawPhase = "battle";
            int br0 = OzTankWreck.Breaks;
            yield return Hold(2.5f);
            yield return StartTake();
            yield return TakeWait(1.0f);
            foreach (var o in new[] { a, b, c, rg }) Order(o, MoveTo(at["red goal"].x, at["red goal"].y, true));
            foreach (var o in new[] { e1, e2, e3, e4, e5, e6 }) Order(o, MoveTo(at["blue goal"].x, at["blue goal"].y, true));
            Note("orders: both sides attack-move toward each other");
            int deadAt = -1; ZObject lost = null; int startF = frameNo; bool forced = false;
            while (!done && frameNo - startF < 75 * Fps)
            {
                if (lost == null) foreach (var o in new[] { a, b, c }) if (o.Destroyed) { lost = o; deadAt = frameNo; hero = o; follow.Clear(); follow.Add(o); Note($"a red v2 is destroyed: id {o.Id}; the camera on it"); break; }
                if (lost != null && frameNo - deadAt > 8 * Fps) break;
                if (lost == null && frameNo - startF > 60 * Fps && !forced)
                {   // staged fallback: the weakest red v2 is destroyed, written as FORCED (the fight did not end one in 60 s)
                    var w = a; foreach (var o in new[] { b, c }) if (!o.Destroyed && (w.Destroyed || o.Health < w.Health)) w = o;
                    w.SetHealth(0); W.UpdateObjectHealth(w); forced = true; Note($"FORCED: no red v2 lost in 60 s; id {w.Id} destroyed by the script");
                }
                yield return null;
            }
            yield return TakeWait(0.5f);
            StopTake(); WriteCrops();
            Fact($"a red v2 destroyed: {(lost != null ? $"id {lost.Id} at {deadAt / (float)Fps:0.0} s{(forced ? " (FORCED by the script)" : " (in the fight)")}" : "NONE")}; tank break-ups {OzTankWreck.Breaks - br0} (last: {OzTankWreck.LastBreak}); {Census()}");
            YawFacts("battle");
            Fact("pass rule (MP4 + sheet + crops): the v2s drive with the tread moving, turn their turrets smoothly from target to target (no shake, no jump: the turret lines above), fire with both barrels sliding back, the turret (head) kicking back and the hull rocking back a little and settling, the heavyshot sound on each shot; hits on the blue units; smoke on a damaged v2; one v2 explodes, its turret is tossed, the hull breaks apart and the wreck stays");
        }

        /// <summary>Item 130 a: an empty (neutral, grey) v2; one red robot walks up on the ladder side, climbs the ladder, the real hatch
        /// opens, he drops in, the stripes take the red, the tank drives off.</summary>
        IEnumerator Board()
        {
            if (!Spot("empty v2", out int cx, out int cy) || !Spot("drive", out int dxT, out int dyT)) yield break;
            hero = Heavy(cx, cy, Team.Neutral, 2, "empty v2");
            if (!CheckHeavy(hero, "empty v2", true)) yield break;
            yield return Hold(2.5f);
            var hv = View(hero);
            var ap = Node(hv, S.Approach);                                                                  // k84: the unit filmed's boarding approach
            Vector3 c0 = hv ? hv.transform.position : At(hero);
            Vector3 side = ap ? Vector3.ProjectOnPlane(ap.position - c0, Vector3.up) : (hv ? hv.transform.right : Vector3.right);
            if (side.sqrMagnitude < 1e-4f) side = Vector3.right;
            var spot = (ap ? ap.position : c0) + side.normalized * 9f;
            var p = ZGame.WorldToPx(spot);
            var rb = Squad("grunt", p.x >> 4, p.y >> 4, Team.Red, 1, "red grunt (ladder side)");
            BoardStageK92(hero);                                                                      // k92: the mech's take: the level's own units near it stand still, unarmed (a blue robot boarded it)
            Fact($"the empty v2's paint before: {Paint(hv)} (TeamPaint), owner {hero.Owner}");
            yield return StartTake();
            yield return TakeWait(1.5f);
            bool sent = Order(rb, Enter(hero));
            Note($"order: the grunt enters the empty v2 ({(sent ? "taken" : "REFUSED")})");
            float maxLid = 0f; int ownedAt = -1, ladderFrom = -1, ladderTo = -1, startF = frameNo;
            while (!done && frameNo - startF < 40 * Fps)
            {
                var v = View(hero);
                maxLid = Mathf.Max(maxLid, LidAngle(v, out _, out _));
                int bd = Boarding(v);
                if (bd > 0 && ladderFrom < 0) ladderFrom = frameNo;
                if (bd > 0) ladderTo = frameNo;
                if (ownedAt < 0 && hero.Owner == Team.Red) { ownedAt = frameNo; Note("the v2 is red now (the sim's enter)"); }
                if (ownedAt >= 0 && bd == 0 && frameNo - ownedAt > Fps) break;
                yield return null;
            }
            yield return TakeWait(1.5f);
            var hv2 = View(hero);
            Fact($"boarding: the view's boarding animation ran from {(ladderFrom >= 0 ? (ladderFrom / (float)Fps).ToString("0.0") : "-")} s to {(ladderTo >= 0 ? (ladderTo / (float)Fps).ToString("0.0") : "-")} s; the lid opened up to {maxLid:0} deg (v1: 95); owner {hero.Owner} from {(ownedAt >= 0 ? (ownedAt / (float)Fps).ToString("0.0") : "never")} s; paint now {Paint(hv2)}");
            int tx = dxT, ty = dyT;
            string t0 = TreadV(hv2);
            bool drove = Order(hero, MoveTo(tx, ty, false));
            Note($"order: the v2 drives off to tile ({tx},{ty}) ({(drove ? "taken" : "REFUSED")})");
            yield return TakeWait(8f);
            Fact($"drive: tread {t0} -> {TreadV(View(hero))} (V offsets of the belts; they move when it drives)");
            StopTake(); WriteCrops();
            Fact("pass rule (crops + MP4): the robot walks to the ladder side, climbs the ladder (hands on the rungs), steps up, the lid of the LEFT cupola swings up about 95 deg as on v1, he drops in, the lid closes, the stripes turn from grey to red, the tank drives away with the tread running");
        }

        /// <summary>Item 130 b: one red v2 (the camera stays on it) fights blue tanks and robots until it is destroyed. It starts at 70 %
        /// (staged, logged) so the fight ends inside the take; a fallback kill after 75 s is written as FORCED.</summary>
        IEnumerator Fight()
        {
            var at = new Dictionary<string, (int x, int y)>();
            foreach (var n in new[] { "red v2", "blue light", "blue medium", "blue heavy", "blue toughs", "blue grunts", "red goal", "blue goal" })
            { if (!Spot(n, out int sx, out int sy)) yield break; at[n] = (sx, sy); }
            hero = Heavy(at["red v2"].x, at["red v2"].y, Team.Red, 2, "red v2 (the one filmed)");
            if (!CheckHeavy(hero, "red v2", false)) yield break;
            hero.SetHealth(hero.MaxHealth / 100 * 70); W.UpdateObjectHealth(hero);
            Note("staged: the red v2 starts at 70 % so the fight ends inside the take");
            TrackYaw(hero, "red v2"); yawPhase = "fight";
            var e1 = Vehicle("light", at["blue light"].x, at["blue light"].y, Team.Blue, "blue light"); var e2 = Vehicle("medium", at["blue medium"].x, at["blue medium"].y, Team.Blue, "blue medium");
            var e3 = Vehicle(OzHeavyTank.Type, at["blue heavy"].x, at["blue heavy"].y, Team.Blue, "blue heavy");
            var e4 = Squad("tough", at["blue toughs"].x, at["blue toughs"].y, Team.Blue, -1, "blue toughs"); var e5 = Squad("grunt", at["blue grunts"].x, at["blue grunts"].y, Team.Blue, -1, "blue grunts");
            int br0 = OzTankWreck.Breaks, ign0 = OpenZDamageStates.Ignited;
            yield return Hold(2.5f);
            yield return StartTake();
            yield return TakeWait(1.0f);
            Order(hero, MoveTo(at["red goal"].x, at["red goal"].y, true));
            foreach (var o in new[] { e1, e2, e3, e4, e5 }) Order(o, MoveTo(at["blue goal"].x, at["blue goal"].y, true));
            Note("orders: the v2 and the blue units attack-move toward each other");
            int deadAt = -1, startF = frameNo; bool forced = false; float minHp = 100f;
            while (!done && frameNo - startF < 90 * Fps)
            {
                minHp = Mathf.Min(minHp, 100f * hero.Health / Mathf.Max(1, hero.MaxHealth));
                if (deadAt < 0 && hero.Destroyed) { deadAt = frameNo; Note("the v2 is destroyed"); }
                if (deadAt >= 0 && frameNo - deadAt > 8 * Fps) break;
                if (deadAt < 0 && !forced && frameNo - startF > 75 * Fps) { hero.SetHealth(0); W.UpdateObjectHealth(hero); forced = true; Note("FORCED: the v2 still stood after 75 s; destroyed by the script"); }
                yield return null;
            }
            yield return TakeWait(0.5f);
            StopTake(); WriteCrops();
            Fact($"the v2 destroyed at {(deadAt >= 0 ? (deadAt / (float)Fps).ToString("0.0") + " s" : "-")}{(forced ? $" (FORCED; its lowest health before: {minHp:0} %)" : " (in the fight)")}; fires lit {OpenZDamageStates.Ignited - ign0}; tank break-ups {OzTankWreck.Breaks - br0} (last: {OzTankWreck.LastBreak}); {Census()}");
            YawFacts("fight");
            Fact("pass rule (MP4 + crops): the camera stays on the v2; its turret turns smoothly onto each target (no shake, no jump: the turret line above); each shot slides both barrels back, kicks the turret (head) back and rocks the hull back a little, which settles in about half a second, with the heavyshot sound; hits on it and on the blue units; smoke at about half health, fire at a quarter; then the explosion, the turret tossed, the hull breaking apart, the wreck staying");
        }

        /// <summary>Item 130 c: the red v2's driver comes up out of the real (left) hatch and fires at blue robots; blue snipers shoot him
        /// out (the game's snipe rule); the tank is left empty and grey with its lid open; then a red robot climbs in again. Staged and
        /// logged: the v2 x10 health (it outlives the robots' hull hits), the snipers x10 health (the tank's gun does not end them before
        /// they shoot). Nothing forces the snipe: if it does not come in 100 s the log says so.</summary>
        IEnumerator Snipe()
        {
            var at = new Dictionary<string, (int x, int y)>();
            foreach (var n in new[] { "red v2", "sniper 1", "sniper 2", "blue grunts", "grunts goal", "away" })
            { if (!Spot(n, out int sx, out int sy)) yield break; at[n] = (sx, sy); }
            hero = Heavy(at["red v2"].x, at["red v2"].y, Team.Red, 2, "red v2 (the one filmed)");
            if (!CheckHeavy(hero, "red v2 (stands all take: its lid and driver are filmed)", true)) yield break;
            hero.MaxHealth *= 10; hero.SetHealth(hero.MaxHealth); W.UpdateObjectHealth(hero);
            var su = W.S.Unit("sniper"); var gu = W.S.Unit("grunt");
            int sr = su != null ? su.attack_radius : 200, gr = gu != null ? gu.attack_radius : 120;   // px
            var s1 = Squad("sniper", at["sniper 1"].x, at["sniper 1"].y, Team.Blue, 1, "blue sniper 1"); var s2 = Squad("sniper", at["sniper 2"].x, at["sniper 2"].y, Team.Blue, 1, "blue sniper 2");
            foreach (var s in new[] { s1, s2 }) { s.MaxHealth *= 10; s.SetHealth(s.MaxHealth); W.UpdateObjectHealth(s); }
            var gq = Squad("grunt", at["blue grunts"].x, at["blue grunts"].y, Team.Blue, -1, "blue grunts");
            Note($"staged: the v2 x10 health, the snipers x10 health (their range {sr} px, the grunts' {gr} px; the fixed spots put them about 7 and 6 tiles out)");
            yield return Hold(2.5f);
            yield return StartTake();
            yield return TakeWait(1.0f);
            Order(gq, MoveTo(at["grunts goal"].x, at["grunts goal"].y, true));
            foreach (var s in new[] { s1, s2 }) Order(s, Attack(hero));
            Note("orders: the snipers attack the v2, the grunts attack-move toward it; the v2 has no order (it fights by itself)");
            int snipedAt = -1, upFirst = -1, startF = frameNo; float maxLid = 0f, bestOver = -9f; int shots0 = hero.DriverShots;
            while (!done && frameNo - startF < 100 * Fps)
            {
                var v = View(hero);
                float lid = LidAngle(v, out bool up, out float over); maxLid = Mathf.Max(maxLid, lid);
                if (up) { if (upFirst < 0) { upFirst = frameNo; Note($"the driver is up: lid {lid:0} deg, {over:0.00} m over the rim"); } bestOver = Mathf.Max(bestOver, over); }
                if (snipedAt < 0 && (hero.Owner == Team.Neutral || hero.Drivers.Count == 0)) { snipedAt = frameNo; Note("the driver is out: the tank is empty (neutral)"); break; }
                yield return null;
            }
            Fact($"driver up from {(upFirst >= 0 ? (upFirst / (float)Fps).ToString("0.0") + " s" : "NEVER")}: lid up to {maxLid:0} deg (v1: 95), his top {bestOver:0.00} m over the rim; his shots {hero.DriverShots - shots0}, hits {hero.DriverHits}");
            Fact($"sniped: {(snipedAt >= 0 ? (snipedAt / (float)Fps).ToString("0.0") + " s, the tank " + hero.Owner : "NOT within 100 s (re-run; nothing forces it)")}");
            if (snipedAt >= 0)
            {   // staged, logged: the blue robots stand down after the snipe so the red robot's climb back in can be filmed
                foreach (var q in new[] { s1, s2, gq })
                    foreach (var m in Members(q)) { if (m.Destroyed) continue; m.Damage = 0; m.AttackRadius = 0; m.AttackObject = null; }
                foreach (var q in new[] { s1, s2, gq }) if (!q.Destroyed) Order(q, MoveTo(at["away"].x, at["away"].y, false));
                Note("staged: the blue robots stand down (no weapon) and walk away after the snipe");
                yield return TakeWait(5f);
                var v = View(hero);
                Fact($"empty after the snipe: paint {Paint(v)}, lid {LidAngle(v, out _, out _):0} deg (a sniped tank keeps its lid open), owner {hero.Owner}");
                var hv = View(hero); var ap = Node(hv, S.Approach);
                Vector3 c0 = hv ? hv.transform.position : At(hero);
                Vector3 side = ap ? Vector3.ProjectOnPlane(ap.position - c0, Vector3.up) : Vector3.right;
                if (side.sqrMagnitude < 1e-4f) side = Vector3.right;
                var p = ZGame.WorldToPx((ap ? ap.position : c0) + side.normalized * 8f);
                var rb = Squad("grunt", p.x >> 4, p.y >> 4, Team.Red, 1, "red grunt (boards again)");
                yield return TakeWait(0.5f);
                Order(rb, Enter(hero));
                int t0 = frameNo; while (!done && frameNo - t0 < 30 * Fps && !(hero.Owner == Team.Red && Boarding(View(hero)) == 0)) yield return null;
                yield return TakeWait(3f);
                Fact($"boarded again: owner {hero.Owner}, paint {Paint(View(hero))}");
            }
            yield return TakeWait(0.5f);
            StopTake(); WriteCrops();
            Fact("pass rule (crops + MP4): the LEFT cupola's lid swings up about 95 deg and the driver rises out of it, aims and fires his rifle at the blue robots (shots, the rifle sound); a sniper's hit takes him out: he drops back, the tank turns grey (empty) with its lid left open; a red robot climbs the ladder and the tank turns red again");
        }


        // ================================================================================================ item 132.1: the turret's heading every frame
        static readonly System.Globalization.CultureInfo Inv = System.Globalization.CultureInfo.InvariantCulture;
        static string F(float x, string fmt) => float.IsNaN(x) ? "" : x.ToString(fmt, Inv);
        sealed class YawTrack
        {
            public ZObject o; public string tag; public UnitAim aim; public UnitView view;
            public readonly List<int> frame = new List<int>(), tgt = new List<int>(), held = new List<int>();
            public readonly List<float> head = new List<float>(), aimB = new List<float>();
            public readonly List<string> phase = new List<string>();
        }
        readonly List<YawTrack> yawTracks = new List<YawTrack>();
        string yawPhase = "-";
        /// <summary>A frame step over this (deg) is a jump: faster than any eased slew at 30 fps (the fastest a waiting shot may ask, 720 deg/s, is 24 deg a frame).</summary>
        const float JumpDeg = 25f;
        /// <summary>A turn reversal = the turn speed changes sign (over 3 deg/s on both sides). A FLUTTER = two reversals within 8 frames (0.27 s)
        /// with no new aim between them and less than 15 deg between them: a shake. An UNEXPLAINED reversal (told, not judged) = more than 15
        /// frames (0.5 s) after the last change of aim (a new target, a shot held or let go, the aim point jumping over 3 deg in a frame).</summary>
        const int FlutterF = 8, ExplainF = 15;
        const float FlutterSwing = 15f, RevSpeed = 3f;

        void TrackYaw(ZObject o, string tag) { if (o != null) yawTracks.Add(new YawTrack { o = o, tag = tag }); }
        /// <summary>End of every frame of the take: each tracked gun's barrel heading (world, deg), the bearing of the point it aims at, its target, its waiting shots.</summary>
        void SampleYaw()
        {
            foreach (var y in yawTracks)
            {
                if (y.o == null || y.o.Destroyed) continue;
                var v = View(y.o); if (!v || v.Dying) continue;
                if (y.view != v) { y.view = v; y.aim = v.GetComponent<UnitAim>(); }
                if (!y.aim || !y.aim.ProofBarrelHeading(out float h)) continue;
                float b = float.NaN;
                if (y.aim.ProofTarget(out var p)) { var d = p - y.aim.ProofPivot; d.y = 0f; if (d.sqrMagnitude > 1e-4f) b = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg; }
                y.frame.Add(frameNo); y.head.Add(h); y.aimB.Add(b); y.tgt.Add(y.o.AttackObject != null ? y.o.AttackObject.Id : -1); y.held.Add(y.aim.ProofHeld);
                y.phase.Add(yawPhase + (!UnitAim.SteadyOn ? " [steady OFF]" : !UnitAim.CombatSteady ? " [k81b only]" : ""));
            }
        }
        struct YawStats { public int frames, reversals, unexplained, flutter, jumps, aimChanges; public float maxStep, maxSpeed; }
        YawStats Analyse(YawTrack y, string phase)
        {
            var s = new YawStats(); float dt = 1f / Fps;
            int lastSign = 0, lastRevF = -1000, lastAimF = -1000; float headAtRev = 0f;
            for (int i = 1; i < y.head.Count; i++)
            {
                if ((phase != null && y.phase[i] != phase) || y.frame[i] != y.frame[i - 1] + 1) { lastSign = 0; lastRevF = -1000; continue; }
                s.frames++;
                float step = Mathf.DeltaAngle(y.head[i - 1], y.head[i]), w = step / dt;
                s.maxStep = Mathf.Max(s.maxStep, Mathf.Abs(step)); s.maxSpeed = Mathf.Max(s.maxSpeed, Mathf.Abs(w));
                if (Mathf.Abs(step) > JumpDeg) s.jumps++;
                bool n0 = float.IsNaN(y.aimB[i - 1]), n1 = float.IsNaN(y.aimB[i]);
                bool aimChanged = n0 != n1 || (!n0 && Mathf.Abs(Mathf.DeltaAngle(y.aimB[i - 1], y.aimB[i])) > 3f) || y.tgt[i] != y.tgt[i - 1] || y.held[i] != y.held[i - 1];
                if (aimChanged) { s.aimChanges++; lastAimF = y.frame[i]; }
                if (Mathf.Abs(w) < RevSpeed) continue;
                int sg = w > 0f ? 1 : -1;
                if (lastSign != 0 && sg != lastSign)
                {
                    s.reversals++;
                    if (y.frame[i] - lastAimF > ExplainF) s.unexplained++;
                    if (y.frame[i] - lastRevF <= FlutterF && lastAimF < lastRevF && Mathf.Abs(Mathf.DeltaAngle(headAtRev, y.head[i])) < FlutterSwing && Mathf.Abs(Mathf.DeltaAngle(headAtRev, y.head[i])) >= FlutterMinSwingK92) s.flutter++;   // k92: a shake swings at least the slew's dead band (View/HeavyV2Films.K92.cs)
                    lastRevF = y.frame[i]; headAtRev = y.head[i];
                }
                lastSign = sg;
            }
            return s;
        }
        static string Line(YawStats s) => $"{s.frames} frames, turn reversals {s.reversals} (unexplained {s.unexplained}), flutters {s.flutter}, jumps {s.jumps}, aim changes {s.aimChanges}, fastest {s.maxSpeed:0} deg/s ({s.maxStep:0.0} deg in a frame)";
        /// <summary>The take's turret facts: per tracked gun and phase the counts and the verdict (flutters 0, jumps 0), and the per-frame CSV.</summary>
        void YawFacts(string what)
        {
            if (yawTracks.Count == 0) return;
            var sb = new StringBuilder("unit,frame,t,phase,heading_deg,aim_bearing_deg,target_id,held_shots\n");
            foreach (var y in yawTracks)
                for (int i = 0; i < y.head.Count; i++)
                    sb.Append(y.tag).Append(',').Append(y.frame[i]).Append(',').Append(F(y.frame[i] / (float)Fps, "0.000")).Append(',').Append(y.phase[i]).Append(',')
                      .Append(F(y.head[i], "0.00")).Append(',').Append(F(y.aimB[i], "0.00")).Append(',').Append(y.tgt[i]).Append(',').Append(y.held[i]).Append('\n');
            string csv = Path.Combine(Dir, take + "_turret_yaw.csv");
            File.WriteAllText(csv, sb.ToString());
            Fact($"turret headings every frame ({what}): {csv}");
            foreach (var y in yawTracks)
            {
                var phases = new List<string>(); foreach (var ph in y.phase) if (!phases.Contains(ph)) phases.Add(ph);
                foreach (var ph in phases)
                {
                    if (ph == "-") continue;
                    var s = Analyse(y, ph);
                    bool pass = s.flutter == 0 && s.jumps == 0;
                    Fact(ph.Contains("[")
                        ? $"turret {y.tag} / {ph} (a CONTROL: {(ph.Contains("[steady OFF]") ? "the turret code before k81b" : "k81b's steady turret without the world-heading slew")}): {Line(s)} -> {(s.flutter > 0 || s.jumps > 0 ? "the shake SHOWN with that code" : "no shake with that code in this run")}"
                        : $"turret {y.tag} / {ph}: {Line(s)}: {(pass ? "PASS" : "FAIL")} (flutters 0, jumps 0)");
                }
            }
            Fact($"turret code counters: Newton-scaled steps {UnitAim.SteadyGainCuts}, frames kept on the last target {UnitAim.SteadyLingerFrames}, slews sped up for a waiting shot {UnitAim.SteadyDeadlineSlews}, snaps at a wait cap {UnitAim.StatSnaps}; world-heading slew: goal jumps held back {UnitAim.CombatHoldFrames} frames, held still in the dead band {UnitAim.CombatDeadFrames} frames, re-syncs {UnitAim.CombatResyncs}, frames on the live target after a shot {UnitAim.CombatMarksSkipped}, largest heading correction drawn {UnitAim.CombatCorrMax:0.00} deg");
        }

        /// <summary>Item 132.1's counted test: a red heavy that cannot be destroyed and five blue grunts standing round it at 6, 7, 9, 12 and
        /// 16 m (staged: no weapon, unkillable while switching). The tank is switched to the next grunt every 1.5 s (ZObject.Engage, as the
        /// sim's own pick); then the grunts are taken out one by one every 2.5 s and the sim re-obtains the next by itself. v2: once with
        /// the old turret code (UnitAim.SteadyOn off: the control), then with the new; v1 with the new, in a second window.</summary>
        IEnumerator Turret()
        {
            if (!Spot("v2", out int cx, out int cy) || !Spot("v2", out int bx, out int by, "turret_v1")) yield break;
            var v2 = Heavy(cx, cy, Team.Red, 2, "red v2 (turret test)"); Unhurt(v2);
            if (!CheckHeavy(v2, "red v2 (turret test)", false)) yield break;
            CombatStageK87b(v2, "turret test");                                                        // k87b: the turret test too (the level own enemies near it unarmed; not the heavy take)
            var ring2 = Ring(cx, cy, "v2");
            hero = v2; follow.Add(v2); TrackYaw(v2, "v2");
            int snaps0 = UnitAim.StatSnaps;
            yield return Hold(2.5f);
            yield return StartTake();
            yield return TakeWait(1.0f);
            UnitAim.SteadyOn = false; Note("UnitAim.SteadyOn OFF (the control: the old turret code)");
            yawPhase = "v2 switching"; yield return Cycle(v2, ring2, 12f);
            UnitAim.SteadyOn = true; Note("UnitAim.SteadyOn ON");
            yield return Cycle(v2, ring2, 12f);
            yawPhase = "v2 re-obtaining"; yield return Reobtain(v2, ring2);
            yawPhase = "-";
            var v1 = Heavy(bx, by, Team.Red, 1, "red v1 (turret test)"); Unhurt(v1);
            if (!CheckHeavy(v1, "red v1 (turret test)", false)) yield break;
            CombatStageK87b(v1, "turret test");
            var ring1 = Ring(bx, by, "v1");
            hero = v1; follow.Clear(); follow.Add(v1); TrackYaw(v1, "v1");
            yield return TakeWait(2.5f);
            yawPhase = "v1 switching"; yield return Cycle(v1, ring1, 12f);
            yawPhase = "v1 re-obtaining"; yield return Reobtain(v1, ring1);
            yawPhase = "-";
            yield return TakeWait(0.5f);
            StopTake(); WriteCrops();
            YawFacts("turret test");
            Fact($"snaps at a wait cap during the test: {UnitAim.StatSnaps - snaps0}");
            Fact("pass rule (the counts above + the MP4): every line without [steady OFF] PASS (no flutter, no jump); in the MP4 the turret swings onto each grunt in one eased move and holds still on it, never shaking back and forth, also on the grunts 6-7 m away, and between two grunts it never swings back to the front first; the [steady OFF] control line should show the old shake on v2 (the cause)");
        }
        void Unhurt(ZObject o) { o.MaxHealth = 100000000; o.SetHealth(o.MaxHealth); W.UpdateObjectHealth(o); Note($"staged: id {o.Id} cannot be destroyed in the test"); }
        /// <summary>Five blue grunts round a heavy whose top-left tile is (cx, cy): bearings 0 / 75 / 150 / 220 / 300 deg at 6 / 7 / 9 / 12 / 16 m from
        /// its centre (staged: no weapon, unkillable until Reobtain takes them out).</summary>
        List<ZObject> Ring(int cx, int cy, string tag)
        {
            var list = new List<ZObject>();
            var spots = new[] { (0f, 6f), (75f, 7f), (150f, 9f), (220f, 12f), (300f, 16f) };
            foreach (var (b, d) in spots)
            {
                float r = b * Mathf.Deg2Rad;
                int tx = Mathf.RoundToInt(cx + 1f + d * 0.5f * Mathf.Sin(r) - 0.5f), ty = Mathf.RoundToInt(cy + 1f - d * 0.5f * Mathf.Cos(r) - 0.5f);
                var o = Squad("grunt", tx, ty, Team.Blue, 1, $"{tag} target {b:0} deg / {d:0} m");
                foreach (var m in Members(o)) { m.Damage = 0; m.AttackRadius = 0; m.MaxHealth = 100000000; m.SetHealth(m.MaxHealth); W.UpdateObjectHealth(m); }
                list.Add(o);
            }
            Note($"staged ({tag}): the five grunts have no weapon and cannot be destroyed until the re-obtain phase");
            return list;
        }
        IEnumerator Cycle(ZObject tank, List<ZObject> ring, float seconds)
        {
            int end = frameNo + Mathf.RoundToInt(seconds * Fps), k = 0, next = frameNo;
            while (!done && frameNo < end && !tank.Destroyed)
            {
                if (frameNo >= next)
                {
                    next = frameNo + Mathf.RoundToInt(1.5f * Fps);
                    for (int i = 0; i < ring.Count; i++) { var t = ring[(k + i) % ring.Count]; if (t != null && !t.Destroyed && t.KillAt < 0) { tank.Engage(t); k = (k + i + 1) % ring.Count; break; } }
                }
                yield return null;
            }
        }
        IEnumerator Reobtain(ZObject tank, List<ZObject> ring)
        {
            Note($"re-obtain: the grunts are taken out one by one every 2.5 s, the {tank.Owner} heavy picks the next by itself");
            foreach (var t in ring)
            {
                yield return TakeWait(2.5f);
                if (done) yield break;
                foreach (var m in Members(t)) if (!m.Destroyed) { m.SetHealth(0); W.UpdateObjectHealth(m); }
            }
            yield return TakeWait(2.5f);
        }

        // ================================================================================================ items 132.2 / 132b: the head and the hull at each shot
        sealed class RecWin
        {
            public int start; public Vector3 baseHead, baseRoot, baseUp, baseFront, fire;
            public readonly List<int> shots = new List<int>();
            public readonly List<float> head = new List<float>(), headRock = new List<float>(), hull = new List<float>(), slide = new List<float>();
            public readonly List<string> rows = new List<string>();
        }
        sealed class RecWatch
        {
            public ZObject o; public string tag; public int model; public UnitView v; public Transform yaw;
            public float lastFire = -1f; public RecWin cur; public readonly List<RecWin> done = new List<RecWin>();
            public Vector3 prevHead, prevRoot, prevUp, prevFront; public bool hasPrev, prevRest;
            public Color32[] prevCrop; public Rect cropRect; public List<Color32[]> burst; public int burstLeft; public bool burstTaken;
        }
        readonly List<RecWatch> recWatch = new List<RecWatch>();
        readonly List<(string label, List<Color32[]> cells)> recBursts = new List<(string, List<Color32[]>)>();
        const int WinFrames = 36, BurstCells = 20, BurstW = 200, BurstH = 150;

        /// <summary>End of every frame: for each watched heavy the drawn head's place and front and the hull's up and place; a shot that finds the
        /// tank at rest opens a 1.2 s window measured against the frame before it; its second window also takes 20 crops 1/30 s apart.</summary>
        void SampleRecoil(Texture2D tex)
        {
            foreach (var w in recWatch)
            {
                var v = View(w.o); if (!v || v.Dying || w.o.Destroyed) continue;
                if (w.v != v) { w.v = v; w.yaw = Node(v, S.Yaw); w.hasPrev = false; }
                if (!w.yaw) continue;
                var root = v.transform;
                var headL = root.InverseTransformPoint(w.yaw.position);
                var frontL = root.InverseTransformDirection(-w.yaw.forward);                            // the head's front is -z
                bool shot = v.LastFireAt > w.lastFire + 1e-4f; if (shot) w.lastFire = v.LastFireAt;
                var hr = v.HeavyRecoilView;
                if (shot) RecSplitAtK102(w);                                                            // k102: a shot that finds the tank settled again is judged in its own window (View/HeavyV2Films.K102.cs)
                if (shot && w.cur == null && w.hasPrev && w.prevRest)                                    // the frame before the shot found it at rest
                {
                    var fd = hr ? hr.LastFireDir : -root.forward; fd.y = 0f; fd = fd.sqrMagnitude > 1e-6f ? fd.normalized : Vector3.forward;
                    w.cur = new RecWin { start = frameNo, baseHead = w.prevHead, baseRoot = w.prevRoot, baseUp = w.prevUp, baseFront = w.prevFront, fire = fd };
                    RecOpenK102(w, v);                                                                   // k102: the shot's direction in the hull's frame as it stood at the shot
                    if (w.done.Count == 1 && !w.burstTaken)
                    {   // the second clean shot pair: 20 crops 1/30 s apart, the frame before the shot first, the crop box fixed for the burst
                        w.cropRect = ScreenBox(v.gameObject, 0.3f); w.burst = new List<Color32[]>(); w.burstTaken = true;
                        if (w.prevCrop != null) w.burst.Add(w.prevCrop);
                        w.burstLeft = BurstCells - w.burst.Count;
                    }
                }
                if (w.cur != null)
                {
                    var c = w.cur; int f = frameNo - c.start;
                    var fdL = root.InverseTransformDirection(c.fire); fdL.y = 0f; fdL.Normalize();
                    float head = Vector3.Dot(headL - c.baseHead, -fdL);
                    float headRock = (Mathf.Asin(Mathf.Clamp(frontL.y, -1f, 1f)) - Mathf.Asin(Mathf.Clamp(c.baseFront.y, -1f, 1f))) * Mathf.Rad2Deg;
                    float hull = -Vector3.SignedAngle(c.baseUp, root.up, Vector3.Cross(Vector3.up, c.fire));   // + = the side it fires toward is up
                    var dp = root.position - c.baseRoot; dp.y = 0f; float slide = Vector3.Dot(dp, -c.fire);
                    if (shot) c.shots.Add(f);
                    c.head.Add(head); c.headRock.Add(headRock); c.hull.Add(hull); c.slide.Add(slide);
                    c.rows.Add($"{w.tag},{w.done.Count + 1},{f},{F(f / (float)Fps, "0.000")},{(shot ? 1 : 0)},{F(head, "0.0000")},{F(headRock, "0.000")},{F(hull, "0.000")},{F(slide, "0.0000")},{F(hr ? hr.HeadSlideNow : float.NaN, "0.0000")},{F(hr ? hr.HeadRockNow : float.NaN, "0.000")},{F(hr ? hr.BodyRockNow : float.NaN, "0.000")},{F(hr ? hr.BodyPushNow : float.NaN, "0.0000")}");
                    if (f >= RecEndK102(c) - 1) { w.done.Add(c); w.cur = null; }   // k102: 22 frames past its last shot at least
                }
                if (w.burst != null && w.burstLeft > 0)
                {
                    w.burst.Add(Cell(tex, w.cropRect, BurstW, BurstH, false)); w.burstLeft--;
                    if (w.burstLeft == 0) recBursts.Add(($"{w.tag}: the frame before its shot, then 19 frames 1/30 s apart (left to right)", w.burst));
                }
                w.prevCrop = w.done.Count == 1 && !w.burstTaken && w.cur == null ? Cell(tex, ScreenBox(v.gameObject, 0.3f), BurstW, BurstH, false) : null;
                w.prevHead = headL; w.prevRoot = root.position; w.prevUp = root.up; w.prevFront = frontL; w.hasPrev = true;
                w.prevRest = hr == null || (Mathf.Abs(hr.HeadSlideNow) < 0.01f && Mathf.Abs(hr.BodyRockNow) < 0.1f && hr.BodyPushNow < 0.01f);
            }
        }
        void WriteRecoilCrops()
        {
            if (recBursts.Count == 0) return;
            var t = new Texture2D(BurstCells * (BurstW + 4), recBursts.Count * (BurstH + 4), TextureFormat.RGB24, false);
            var fill = new Color32[t.width * t.height]; for (int i = 0; i < fill.Length; i++) fill[i] = new Color32(0, 0, 0, 255); t.SetPixels32(fill);
            for (int r = 0; r < recBursts.Count; r++)
                for (int c = 0; c < recBursts[r].cells.Count && c < BurstCells; c++)
                    t.SetPixels32(c * (BurstW + 4), t.height - (r + 1) * (BurstH + 4), BurstW, BurstH, recBursts[r].cells[c]);
            t.Apply(false);
            string p = Path.Combine(Dir, S.Prefix + "_recoil_crops_L02.png");
            File.WriteAllBytes(p, t.EncodeToPNG()); Destroy(t);
            Fact($"crops 1/30 s apart round a shot pair from the RTS camera: {p}; rows top to bottom:");
            for (int r = 0; r < recBursts.Count; r++) log.AppendLine($"  row {r + 1}: {recBursts[r].label}");
        }

        /// <summary>Items 132.2 / 132b: a red v2 and a red v1 (6 tiles apart) fire at a blue jeep about 14 m north (staged: unkillable, no
        /// weapon); every shot pair that finds a tank at rest is measured on the drawn model for 1.2 s (36 frames at 30 fps).</summary>
        IEnumerator Recoil()
        {
            if (!Spot("v2", out int ax2, out int ay2) || !Spot("v1", out int ax1, out int ay1) || !Spot("jeep", out int jx, out int jy)) yield break;
            RecoilTargetInReach(ax2, ay2, ax1, ay1, ref jx, ref jy);                                      // k84b: a gun that cannot reach the heavy's target tile from both tanks gets a nearer open tile (View/HeavyV2Films.K84b.cs)
            var v2 = Heavy(ax2, ay2, Team.Red, 2, "red v2 (recoil)"); var v1 = Heavy(ax1, ay1, Team.Red, 1, "red v1 (recoil)");
            if (!CheckHeavy(v2, "red v2 (recoil)", false) || !CheckHeavy(v1, "red v1 (recoil)", false)) yield break;
            var jeep = Vehicle("jeep", jx, jy, Team.Blue, "blue jeep (the target)");
            jeep.MaxHealth = 100000000; jeep.SetHealth(jeep.MaxHealth); W.UpdateObjectHealth(jeep); jeep.Damage = 0; jeep.AttackRadius = 0;
            Note("staged: the jeep cannot be destroyed and has no weapon (it only stands as the target)");
            recWatch.Add(new RecWatch { o = v2, tag = "v2", model = 2 }); recWatch.Add(new RecWatch { o = v1, tag = "v1", model = 1 });
            RecModelsK87b();                                                                           // k87b: a unit with one model (the medium tank) is judged as that model in both windows
            hero = v2; follow.Add(v2); follow.Add(v1);
            yield return Hold(2.5f);
            yield return StartTake();
            int f0 = frameNo;
            while (!done && frameNo - f0 < 20 * Fps)
            {
                bool enough = true; foreach (var w in recWatch) if (w.done.Count < 3 || !RecAlongK102(w)) enough = false;   // k102: and a window along its hull
                if (enough) break;
                yield return null;
            }
            yield return TakeWait(0.5f);
            StopTake(); WriteCrops();
            var sb = new StringBuilder("unit,window,frame,t,shot,head_slide_m,head_rock_deg,hull_rock_deg,hull_slide_m,spring_head_m,spring_head_deg,spring_hull_deg,spring_hull_m\n");
            foreach (var w in recWatch) foreach (var c in w.done) foreach (var r in c.rows) sb.Append(r).Append('\n');
            string csv = Path.Combine(Dir, S.Prefix + "_recoil_frames_L02.csv"); File.WriteAllText(csv, sb.ToString());
            Fact($"the head and the hull every frame round each shot pair (measured on the drawn model; spring_* = what HeavyRecoil drew): {csv}");
            foreach (var w in recWatch)
            {
                if (w.done.Count == 0) { Fact($"{w.tag}: NO shot pair found the tank at rest: FAIL"); continue; }
                for (int i = 0; i < w.done.Count; i++) Fact(RecVerdict(w, w.done[i], i + 1));
            }
            Fact($"{S.ShotKey} plays in the take {Count(S.ShotKey)} (one per shot; sound log)");
            Fact(RecPassRule());                                                                         // k84b: the heavy's words as before, the others' own (View/HeavyV2Films.K84b.cs)
            Fact(RecSplitNoteK102());
        }
        /// <summary>One shot pair's verdict (the numbers of the kit's tools/recoil_model.py with room for the suspension's own side rock).</summary>
        string RecVerdict(RecWatch w, RecWin c, int index)
        {
            int n = c.head.Count; if (n < 2) return $"{w.tag} shot window {index}: too short ({n} frames): FAIL";
            int last = c.shots.Count > 0 ? c.shots[c.shots.Count - 1] : 0, second = c.shots.Count > 1 ? c.shots[1] : n;
            float hp = 0f, hrp = 0f, up = 0f, down = 0f, sp = 0f, stepHull = 0f, stepSlide = 0f, stepHead = 0f; int hpF = 0;
            for (int i = 0; i < n; i++)
            {
                if (i < second && c.head[i] > hp) { hp = c.head[i]; hpF = i; }                         // the first barrel's kick
                hrp = Mathf.Max(hrp, c.headRock[i]); up = Mathf.Max(up, c.hull[i]); sp = Mathf.Max(sp, c.slide[i]);
                if (i < second) down = Mathf.Min(down, c.hull[i]);                                     // the opposite rock before the second barrel
                if (i > 0)
                {
                    stepHull = Mathf.Max(stepHull, Mathf.Abs(c.hull[i] - c.hull[i - 1])); stepSlide = Mathf.Max(stepSlide, Mathf.Abs(c.slide[i] - c.slide[i - 1]));
                    stepHead = Mathf.Max(stepHead, Mathf.Abs(c.head[i] - c.head[i - 1]));
                }
            }
            int homeF = RecSampleK87b(c, n, last, 12), stillF = RecSampleK87b(c, n, last, 21);   // k87b: a single-barrel gun's settled sample before its next shot (the heavy as before)
            float headHome = Mathf.Abs(c.head[homeF]), hullStill = Mathf.Abs(c.hull[stillF]);
            var R = RecRuleFor(w);                                                                       // k84b: each unit against its own design; the heavy's numbers as before (View/HeavyV2Films.K84b.cs)
            RecRuleK93(w, c, ref R);                                                                     // k93, item 152: the hull bands for this shot's direction (View/HeavyV2Films.K93.cs)
            bool okHead = hp >= R.headMin && hp <= R.headMax && hpF >= 1 && hpF <= 4 && headHome < R.homeMax && hrp >= R.rockLo && hrp <= R.rockHi;
            bool okHull = up >= R.hullLo && up <= R.hullHi && down > -0.45f * up && hullStill < 0.2f && sp >= R.slideLo && sp <= R.slideHi;
            bool okSteps = stepHull <= R.stepHull && stepSlide <= R.stepSlide && stepHead <= R.stepHead;
            return $"{w.tag} shot window {index} (shots at frames {string.Join("/", c.shots)}): HEAD slide peak {hp:0.000} m at frame {hpF} (+{hpF / (float)Fps:0.000} s), rock peak {hrp:0.00} deg, " +
                   $"{headHome:0.000} m off rest 0.4 s after the last shot -> {(okHead ? "PASS" : "FAIL")} ({R.head}); " +
                   $"HULL rock peak {up:0.00} deg (fire side up), opposite rock {down:0.00} deg, {hullStill:0.00} deg 0.7 s after the last shot, slide back peak {sp:0.000} m -> {(okHull ? "PASS" : "FAIL")} ({R.hull}); " +
                   $"largest frame steps: hull {stepHull:0.00} deg, slide {stepSlide:0.000} m, head {stepHead:0.000} m -> {(okSteps ? "PASS" : "FAIL")} ({R.steps})";
        }

        // ================================================================================================ k81c: item 132 in a fight (the combat turret test)
        /// <summary>Item 132 in a real fight (k81c): a red v2 that cannot be destroyed drives to and fro between two fixed points while six
        /// blue grunts walk round it 8 to 14 m out (staged: no weapon; each walks on to the next one's place every 4 s, so the targets keep
        /// moving), one is taken out every 3 s (the v2's own shots take others) and the tank re-obtains the next; a fresh six for each
        /// phase. Three phases of 20 s: the turret code before k81b (UnitAim.SteadyOn off: a control), k81b's steady turret alone
        /// (UnitAim.CombatSteady off: a control), then both on. The barrels' heading every frame is counted as in the battle film.</summary>
        IEnumerator Combat()
        {
            if (!Spot("v2", out int cx, out int cy) || !Spot("leg a", out int ax, out int ay) || !Spot("leg b", out int bx, out int by)) yield break;
            var ring = new List<(int x, int y)>();
            for (int i = 0; i < 6; i++) { if (!Spot("enemy " + i, out int ex, out int ey)) yield break; ring.Add((ex, ey)); }
            var v2 = Heavy(cx, cy, Team.Red, 2, "red v2 (combat test)"); Unhurt(v2);
            if (!CheckHeavy(v2, "red v2 (combat test)", false)) yield break;
            CombatStageK87b(v2);                                                                       // k87b: the level's own enemies near the tank have no weapon for the test (not the heavy's take)
            hero = v2; follow.Add(v2); TrackYaw(v2, "v2");
            int snaps0 = UnitAim.StatSnaps;
            yield return Hold(2.5f);
            yield return StartTake();
            yield return TakeWait(1.0f);
            var phases = new[] { (false, false, "UnitAim.SteadyOn OFF (a control: the turret code before k81b)"),
                                 (true, false, "UnitAim.SteadyOn ON, UnitAim.CombatSteady OFF (a control: k81b's steady turret alone)"),
                                 (true, true, "UnitAim.SteadyOn ON, UnitAim.CombatSteady ON (the world-heading slew)") };
            int pi = 0;
            foreach (var (steady, combat, what) in phases)
            {
                UnitAim.SteadyOn = steady; UnitAim.CombatSteady = combat; Note(what);
                var foes = new List<ZObject>();
                for (int i = 0; i < ring.Count; i++)
                {
                    var o = Squad("grunt", ring[i].x, ring[i].y, Team.Blue, 1, $"phase {pi + 1} grunt {i}");
                    foreach (var m in Members(o)) { m.Damage = 0; m.AttackRadius = 0; }
                    foes.Add(o);
                }
                Note($"staged (phase {pi + 1}): six blue grunts round the v2, no weapon; one taken out every 3 s");
                yawPhase = "combat";
                int end = frameNo + Mathf.RoundToInt(20f * Fps), nextLeg = frameNo, nextKill = frameNo + 3 * Fps, nextWalk = frameNo; bool toA = true; int k = 0;
                while (!done && frameNo < end)
                {
                    if (frameNo >= nextLeg) { nextLeg = frameNo + 6 * Fps; Order(v2, MoveTo(toA ? ax : bx, toA ? ay : by, false)); toA = !toA; }
                    if (frameNo >= nextWalk)
                    {   // each grunt walks on to the next one's place round the tank: the targets keep moving
                        nextWalk = frameNo + 4 * Fps; k++;
                        for (int i = 0; i < foes.Count; i++)
                        {
                            var f = foes[i]; var to = ring[(i + k) % ring.Count];
                            if (f != null && !f.Destroyed) Order(f, MoveTo(to.x, to.y, false));
                        }
                    }
                    if (frameNo >= nextKill)
                    {   // staged: one is taken out (the v2's own shots take others); the tank re-obtains the next by itself
                        nextKill = frameNo + 3 * Fps;
                        foreach (var f in foes)
                        {
                            if (f == null || f.Destroyed || f.KillAt >= 0) continue;
                            foreach (var m in Members(f)) if (!m.Destroyed) { m.SetHealth(0); W.UpdateObjectHealth(m); }
                            Note("staged: a grunt taken out"); break;
                        }
                    }
                    yield return null;
                }
                yawPhase = "-";
                foreach (var f in foes) foreach (var m in Members(f)) if (!m.Destroyed) { m.SetHealth(0); W.UpdateObjectHealth(m); }
                yield return TakeWait(1.5f);
                pi++;
            }
            UnitAim.SteadyOn = true; UnitAim.CombatSteady = true; Note("UnitAim.SteadyOn ON, UnitAim.CombatSteady ON (after the test)");
            yield return TakeWait(0.5f);
            StopTake(); WriteCrops();
            YawFacts("combat test");
            CombatEngagedK87b();                                                                       // k87b: how many targets the tank really engaged
            CombatTurnK87d();                                                                          // k87d, item 181: the turn speed against the light tank's (View/HeavyV2Films.K87d.cs)
            Fact($"snaps at a wait cap during the test: {UnitAim.StatSnaps - snaps0}");
            Fact("pass rule (the counts above + the MP4): the line WITHOUT a bracket (both steady layers on) PASS (flutters 0, jumps 0) while the v2 drives to and fro among moving grunts that die one by one; the [steady OFF] and [k81b only] lines are the controls (what the older code did in the same kind of fight: flutters expected there); in the MP4 the turret swings onto each grunt in one eased move, follows it without a wiggle and holds its heading when the tank fires or turns under it");
        }

        // ================================================================================================ k81c: items 137 / 146 (the tracks stay on the ground)
        sealed class GearRow { public int frame; public string tag, phase; public bool moving, fired; public float slope, gPitch, gRoll, tPitch, tRoll, mid, sink, hPitch, hRoll, lift; }
        readonly List<GearRow> gearRows = new List<GearRow>();
        readonly List<(ZObject o, string tag)> gearWatch = new List<(ZObject, string)>();
        string gearPhase = "-";
        const string GearOffPhase = "planting OFF (the control: the drawing before, the way out)", GearOnPhase = "planting ON (the way back)";
        /// <summary>End of every frame of the test, for v1 and v2 (UnitView.ProofGear): the tracks' pitch and roll; the ground's own pitch
        /// and roll under the tank, sampled 2.6 m ahead / behind and 1.5 m to each side along its heading (not the planting's own samples);
        /// the bottom middle of a track over the ground; the deepest point of both edges of both tracks below the ground; the hull's pitch and
        /// roll over the tracks and its lift; moving; a shot in the last 0.5 s.</summary>
        void SamplePlant()
        {
            if (gearPhase == "-") return;
            foreach (var (o, tag) in gearWatch)
            {
                if (o == null || o.Destroyed) continue;
                var v = View(o); if (!v || v.Dying) continue;
                if (!v.ProofGear(out float tp, out float tr, out float mid, out float sink, out float hp, out float hr, out float lift)) continue;
                var p = v.transform.position;
                var f = v.transform.rotation * Vector3.back; f.y = 0f; f = f.sqrMagnitude > 1e-6f ? f.normalized : Vector3.forward;   // the model's front is -z
                var rt = v.transform.rotation * Vector3.right; rt.y = 0f; rt = rt.sqrMagnitude > 1e-6f ? rt.normalized : Vector3.right;
                float gp = Mathf.Atan2(g.GroundY(p.x + f.x * 2.6f, p.z + f.z * 2.6f) - g.GroundY(p.x - f.x * 2.6f, p.z - f.z * 2.6f), 5.2f) * Mathf.Rad2Deg;
                float gr = Mathf.Atan2(g.GroundY(p.x + rt.x * 1.5f, p.z + rt.z * 1.5f) - g.GroundY(p.x - rt.x * 1.5f, p.z - rt.z * 1.5f), 3f) * Mathf.Rad2Deg;
                gearRows.Add(new GearRow { frame = frameNo, tag = tag, phase = gearPhase, moving = o.IsMoving, fired = Time.time - v.LastFireAt < 0.5f,
                                           slope = Mathf.Sqrt(gp * gp + gr * gr), gPitch = gp, gRoll = gr, tPitch = tp, tRoll = tr, mid = mid, sink = sink, hPitch = hp, hRoll = hr, lift = lift });
            }
        }

        /// <summary>Back-and-forth swings of a series: the turns of direction whose swing from the last turn point is over `min`.</summary>
        static int Swings(List<float> s, float min)
        {
            if (s.Count < 3) return 0;
            int n = 0, dir = 0; float hi = s[0], lo = s[0], ext = s[0];
            for (int i = 1; i < s.Count; i++)
            {
                float x = s[i];
                if (dir == 0)
                {   // no direction yet: the first move of more than `min` sets it
                    hi = Mathf.Max(hi, x); lo = Mathf.Min(lo, x);
                    if (hi - x > min) { dir = -1; ext = x; } else if (x - lo > min) { dir = 1; ext = x; }
                    continue;
                }
                if (dir > 0) { if (x > ext) ext = x; else if (ext - x > min) { n++; dir = -1; ext = x; } }
                else { if (x < ext) ext = x; else if (x - ext > min) { n++; dir = 1; ext = x; } }
            }
            return n;
        }
        static float P95(List<float> s) { if (s.Count == 0) return 0f; var c = new List<float>(s); c.Sort(); return c[Mathf.Clamp(Mathf.CeilToInt(0.95f * c.Count) - 1, 0, c.Count - 1)]; }

        /// <summary>Items 137 / 146 (k81c): a red v2 and a red v1 (unhurt) drive from their fixed places on open ground down to the river
        /// bridge north of them, over its ramps and its deck, to the far side, firing on the way at a blue jeep (staged: unkillable, no
        /// weapon), with the planting OFF (the control: TrackGround.Enabled off, the drawing before: the tracks ride the hull's wobble); then
        /// the same way back with it ON (View/UnitView.GroundGear.cs: the tracks on the ground, only the hull and turret rock). Every frame,
        /// per tank: SamplePlant. CSV hv2_tracks_ground_L02_frames.csv.</summary>
        IEnumerator Plant()
        {
            var at = new Dictionary<string, (int x, int y)>();
            foreach (var n in new[] { "v2", "v1", "bridge north", "v2 south", "v1 south", "jeep" }) { if (!Spot(n, out int sx, out int sy)) yield break; at[n] = (sx, sy); }
            var v2 = Heavy(at["v2"].x, at["v2"].y, Team.Red, 2, "red v2 (track test)"); Unhurt(v2);
            var v1 = Heavy(at["v1"].x, at["v1"].y, Team.Red, 1, "red v1 (track test)"); Unhurt(v1);
            if (!CheckHeavy(v2, "red v2 (track test)", false) || !CheckHeavy(v1, "red v1 (track test)", false)) yield break;
            var jeep = Vehicle("jeep", at["jeep"].x, at["jeep"].y, Team.Blue, "blue jeep (the target)");
            jeep.MaxHealth = 100000000; jeep.SetHealth(jeep.MaxHealth); W.UpdateObjectHealth(jeep); jeep.Damage = 0; jeep.AttackRadius = 0;
            Note("staged: the jeep cannot be destroyed and has no weapon (the target the tanks fire at on the way)");
            gearWatch.Add((v2, "v2")); gearWatch.Add((v1, "v1"));
            hero = v2; follow.Add(v2); follow.Add(v1);
            int frames0 = TrackGround.Frames; TrackGround.MaxHullLiftM = 0f;
            Note($"TrackGround: hull feel {TrackGround.HullFeel:0.00}, tilt rate {TrackGround.TiltRate:0} /s; VehicleMotion.SlotScroll {VehicleMotion.SlotScroll}");
            yield return Hold(2.5f);
            yield return StartTake();
            yield return TakeWait(1.0f);
            for (int phase = 0; phase < 2; phase++)
            {
                bool on = phase == 1;
                TrackGround.Enabled = on; gearPhase = on ? GearOnPhase : GearOffPhase;
                Note(gearPhase);
                // the way out: open ground to the bridge, over it to the far side (v2 first, v1 behind); the way back: the same, v1 first
                var first = on ? v1 : v2; var second = on ? v2 : v1;
                var goals = on ? new[] { (v1, at["bridge north"]), (v1, at["v1"]), (v2, at["bridge north"]), (v2, at["v2"]) }
                               : new[] { (v2, at["bridge north"]), (v2, at["v2 south"]), (v1, at["bridge north"]), (v1, at["v1 south"]) };
                Order(first, MoveTo(goals[0].Item2.x, goals[0].Item2.y, false));
                int t0 = frameNo; bool secondSent = false, firstOn = false, secondOn = false;
                while (!done && frameNo - t0 < 80 * Fps)
                {
                    if (!firstOn && Near(first, goals[0].Item2, 2)) { firstOn = true; Order(first, MoveTo(goals[1].Item2.x, goals[1].Item2.y, false)); }
                    if (!secondSent && frameNo - t0 > 4 * Fps) { secondSent = true; Order(second, MoveTo(goals[2].Item2.x, goals[2].Item2.y, false)); }
                    if (secondSent && !secondOn && Near(second, goals[2].Item2, 2)) { secondOn = true; Order(second, MoveTo(goals[3].Item2.x, goals[3].Item2.y, false)); }
                    if (firstOn && secondOn && Near(first, goals[1].Item2, 1) && Near(second, goals[3].Item2, 1) && !first.IsMoving && !second.IsMoving) break;
                    yield return null;
                }
                Note($"{gearPhase}: done after {(frameNo - t0) / (float)Fps:0.0} s; v2 at tile ({v2.CenterX >> 4},{v2.CenterY >> 4}), v1 at ({v1.CenterX >> 4},{v1.CenterY >> 4})");
                gearPhase = "-";
                yield return TakeWait(1.0f);
            }
            TrackGround.Enabled = true; Note("TrackGround.Enabled ON (after the test)");
            yield return TakeWait(0.5f);
            StopTake(); WriteCrops();
            var sb = new StringBuilder("unit,frame,t,phase,moving,fired,ground_slope_deg,ground_pitch_deg,ground_roll_deg,track_pitch_deg,track_roll_deg,track_mid_over_ground_m,track_deepest_below_ground_m,hull_pitch_over_tracks_deg,hull_roll_over_tracks_deg,hull_lift_m\n");
            foreach (var r in gearRows)
                sb.Append(r.tag).Append(',').Append(r.frame).Append(',').Append(F(r.frame / (float)Fps, "0.000")).Append(',').Append(r.phase == GearOnPhase ? "ON" : "OFF").Append(',')
                  .Append(r.moving ? 1 : 0).Append(',').Append(r.fired ? 1 : 0).Append(',').Append(F(r.slope, "0.00")).Append(',').Append(F(r.gPitch, "0.00")).Append(',').Append(F(r.gRoll, "0.00")).Append(',')
                  .Append(F(r.tPitch, "0.000")).Append(',').Append(F(r.tRoll, "0.000")).Append(',').Append(F(r.mid, "0.000")).Append(',').Append(F(r.sink, "0.000")).Append(',')
                  .Append(F(r.hPitch, "0.000")).Append(',').Append(F(r.hRoll, "0.000")).Append(',').Append(F(r.lift, "0.000")).Append('\n');
            string csv = Path.Combine(Dir, S.Prefix + "_tracks_ground_L02_frames.csv"); File.WriteAllText(csv, sb.ToString());
            Fact($"the tracks and the hull against the ground every frame: {csv}");
            foreach (var tag in new[] { "v2", "v1" })
                foreach (var ph in new[] { GearOffPhase, GearOnPhase })
                {
                    int n = 0, moving = 0, fired = 0, sloped = 0, flat = 0, sunk = 0; float deepest = -9f, flatP = 0f, flatR = 0f, midLo = 9f, midHi = -9f, maxLift = 0f;
                    var dp = new List<float>(); var dr = new List<float>(); var lagP = new List<float>(); var lagR = new List<float>(); var hP = new List<float>(); var hR = new List<float>();
                    foreach (var r in gearRows)
                    {
                        if (r.tag != tag || r.phase != ph) continue;
                        n++; if (r.moving) moving++; if (r.fired) fired++;
                        if (r.slope >= 3f) sloped++;
                        if (r.slope < 0.5f) { flat++; flatP = Mathf.Max(flatP, Mathf.Abs(r.tPitch - r.gPitch)); flatR = Mathf.Max(flatR, Mathf.Abs(r.tRoll - r.gRoll)); midLo = Mathf.Min(midLo, r.sink); midHi = Mathf.Max(midHi, r.sink); }
                        if (r.sink > 0.03f) sunk++;
                        deepest = Mathf.Max(deepest, r.sink); maxLift = Mathf.Max(maxLift, r.lift);
                        dp.Add(r.tPitch - r.gPitch); dr.Add(r.tRoll - r.gRoll); lagP.Add(Mathf.Abs(r.tPitch - r.gPitch)); lagR.Add(Mathf.Abs(r.tRoll - r.gRoll));
                        hP.Add(Mathf.Abs(r.hPitch)); hR.Add(Mathf.Abs(r.hRoll));
                    }
                    bool on = ph == GearOnPhase;
                    int swP = Swings(dp, 0.5f), swR = Swings(dr, 0.5f);
                    float spread = flat > 0 ? midHi - midLo : 0f, secs = n / (float)Fps, lp = P95(lagP), lr = P95(lagR), hp95 = P95(hP), hr95 = P95(hR);
                    string what = $"{n} frames ({secs:0.0} s; {moving} moving, {fired} within 0.5 s of a shot, {sloped} on ground sloped 3 deg or more, {flat} on flat ground): " +
                                  $"TRACKS on flat ground: pitch off the ground's up to {flatP:0.00} deg, roll up to {flatR:0.00} deg, their lowest point {midLo:0.000}..{midHi:0.000} m in the ground (spread {spread:0.000} m: the bob); " +
                                  $"tracks against the ground's own pitch / roll (all frames): p95 {lp:0.00} / {lr:0.00} deg, back-and-forth swings over 0.5 deg {swP} / {swR}; " +
                                  $"frames with a track point more than 3 cm in the ground {sunk}, deepest {deepest:0.000} m; HULL over the tracks: p95 pitch {hp95:0.00} deg, roll {hr95:0.00} deg, largest lift {maxLift:0.000} m";
                    bool pass = n > 30 && moving > 0 && fired > 0 && sloped >= 10 && flat >= 10 && sunk == 0 && flatP <= 0.3f && flatR <= 0.3f && spread <= 0.03f && lp <= 1.0f && lr <= 1.0f
                                && (hp95 >= 0.1f || hr95 >= 0.1f) && hp95 <= K93HullCap() && hr95 <= K93HullCap();   // k93: the bigger shot rock (View/HeavyV2Films.K93.cs)
                    Fact(on ? $"tracks {tag} / {ph}: {what}: {(pass ? "PASS" : "FAIL")} (tracks level and at one height on flat ground, with the ground on slopes, never more than 3 cm in it; the hull still rocks a little, under 2.5 deg; driving, firing, flat and sloped ground all seen)"
                            : $"tracks {tag} / {ph} (the CONTROL): {what} -> {(swP + swR > 0 || sunk > 0 || flatP > 0.3f || flatR > 0.3f ? "the tracks wobbling with the hull SHOWN without the planting" : "no track wobble shown without the planting in this run")}");
                }
            Fact($"planting counters: frames planted {TrackGround.Frames - frames0}, the hull's largest lift over its tracks {TrackGround.MaxHullLiftM:0.000} m; {S.ShotKey} plays {Count(S.ShotKey)}");
            Fact("pass rule (the lines above + the MP4): with the planting ON the tracks of v1 and v2 never wobble, pitch, roll, bob or kick: level and at one height on flat ground, following only the slope on the bridge ramps, never more than 3 cm in the ground, while the tanks drive, wobble and fire; the hull and turret still rock a little above them; the OFF lines are the control (the old drawing: the tracks ride the hull's wobble)");
        }
        bool Near(ZObject o, (int x, int y) t, int tiles) => o != null && Mathf.Abs((o.CenterX >> 4) - t.x) <= tiles + 1 && Mathf.Abs((o.CenterY >> 4) - t.y) <= tiles + 1;

        // ================================================================================================ k81d: items 137 / 146, the fair track test (menu 14)
        /// <summary>The bank stretch of the k81d track test (L02, absolute tiles, the top-left tile of a 2 x 2 heavy): the 3-5 deg canal bank
        /// just west of the test's start spots, where the real ground slopes under both models (chosen off the editor on the level's own
        /// terrain mesh, the kit's tools/slope_tiles.py and tools/plant_sim.py; vehicle tiles of k81c's whole-map scan).</summary>
        static readonly Dictionary<string, (int x, int y)> PlantBank = new Dictionary<string, (int x, int y)> { { "bank v2", (46, 39) }, { "bank v1", (46, 41) } };
        sealed class BeltRow { public int frame; public string tag; public bool on, first, moving, fired; public float slope, gPitch, gRoll, pL, pR, roll, sink, gap, mid, hPitch, hRoll, lift; }
        readonly List<BeltRow> beltRows = new List<BeltRow>();
        readonly List<(ZObject o, string tag)> beltWatch = new List<(ZObject, string)>();
        readonly HashSet<string> beltSeen = new HashSet<string>();
        string beltPhase = "-";

        /// <summary>End of every frame of the k81d track test, per tank: each belt's drawn pitch, the shared roll, the deepest of the belts'
        /// proof points below the ground and the largest gap under a belt (UnitView.ProofBelts: 17 stations x 3 across each belt's bottom,
        /// read from the DRAWN belt nodes), the hull over the first belt and its lift (UnitView.ProofGear), the ground's own pitch and roll
        /// sampled 2.6 m ahead / behind and 1.5 m to each side (not the planting's samples), moving, a shot in the last 0.5 s. The first row
        /// of each phase is the switch frame (drawn by the phase before: the switch is set after that frame's Sync): counted on its own.</summary>
        IEnumerator BeltSampler()
        {
            while (recording && !done)
            {
                yield return new WaitForEndOfFrame();
                if (!recording || beltPhase == "-") continue;
                foreach (var (o, tag) in beltWatch)
                {
                    if (o == null || o.Destroyed) continue;
                    var v = View(o); if (!v || v.Dying) continue;
                    if (!v.ProofBelts(out var pitch, out float roll, out float sink, out float gap, out float mid)) continue;
                    v.ProofGear(out _, out _, out _, out _, out float hp, out float hr, out float lift);
                    var p = v.transform.position;
                    var f = v.transform.rotation * Vector3.back; f.y = 0f; f = f.sqrMagnitude > 1e-6f ? f.normalized : Vector3.forward;   // the model's front is -z
                    var rt = v.transform.rotation * Vector3.right; rt.y = 0f; rt = rt.sqrMagnitude > 1e-6f ? rt.normalized : Vector3.right;
                    float gp = Mathf.Atan2(g.GroundY(p.x + f.x * 2.6f, p.z + f.z * 2.6f) - g.GroundY(p.x - f.x * 2.6f, p.z - f.z * 2.6f), 5.2f) * Mathf.Rad2Deg;
                    float gr = Mathf.Atan2(g.GroundY(p.x + rt.x * 1.5f, p.z + rt.z * 1.5f) - g.GroundY(p.x - rt.x * 1.5f, p.z - rt.z * 1.5f), 3f) * Mathf.Rad2Deg;
                    bool first = beltSeen.Add(tag + "|" + beltPhase);
                    beltRows.Add(new BeltRow { frame = frameNo, tag = tag, on = beltPhase == GearOnPhase, first = first, moving = o.IsMoving || DrawnMovedK92(v), fired = Time.time - v.LastFireAt < 0.5f,   // k92: standing = the drawn tank too
                                               slope = Mathf.Sqrt(gp * gp + gr * gr), gPitch = gp, gRoll = gr, pL = pitch[0], pR = pitch[1], roll = roll, sink = sink, gap = gap, mid = mid,
                                               hPitch = hp, hRoll = hr, lift = lift });
                }
            }
        }

        /// <summary>A tank's way in the track test: its goals in order; a goal with a hold is reached standing (it waits there, firing at the
        /// jeep when it is in range); a goal without one is passed on the way.</summary>
        sealed class Leg { public ZObject o; public List<(int x, int y, float hold)> goals; public int i = -1; public int holdEnd = -1; public bool Done => goals == null || i >= goals.Count; }
        void LegStep(Leg l)
        {
            if (l.Done || l.o == null || l.o.Destroyed) return;
            if (l.i < 0) { l.i = 0; Order(l.o, MoveTo(l.goals[0].x, l.goals[0].y, false)); return; }
            var gl = l.goals[l.i];
            bool last = l.i == l.goals.Count - 1;
            if (gl.hold > 0f || last)
            {
                if (l.holdEnd < 0) { if (Near(l.o, (gl.x, gl.y), 1) && !l.o.IsMoving) { l.holdEnd = frameNo + Mathf.RoundToInt(gl.hold * Fps); Note($"id {l.o.Id} stands at ({gl.x},{gl.y}){(gl.hold > 0f ? $" for {gl.hold:0.0} s" : "")}"); LegStandK87b(l.o, gl.hold); } return; }   // k87b: at a stand a target beyond reach is dropped, the jeep taken when in reach
                if (frameNo < l.holdEnd) return;
            }
            else if (!Near(l.o, (gl.x, gl.y), 2)) return;
            LegLeaveK87b(l.o);                                                                         // k87b: leaving a goal it drops a target beyond its reach (no chase)
            l.i++; l.holdEnd = -1;
            if (!l.Done) Order(l.o, MoveTo(l.goals[l.i].x, l.goals[l.i].y, false));
        }

        /// <summary>Items 137 / 146, k81d (menu 14): a red v2 and a red v1 (unhurt) drive from their places over the canal bank west of them
        /// (3-5 deg of real slope), down to the river bridge, stand there 3 s firing at a blue jeep (staged: unkillable, no weapon), over the
        /// bridge's ramps and deck to the far side; the planting OFF (the control: the drawing before), then the same way back with it ON
        /// (View/UnitView.GroundGear.cs: each belt on the ground under it, only the hull and turret rock). Every frame, per tank:
        /// BeltSampler. PASS (planting ON, the switch frame counted on its own): no frame with a belt point more than 3 cm in the ground;
        /// the air under the belts small; standing and firing, the belts do not move while the hull rocks over them; driving, firing, sloped
        /// and flat ground and standing all seen. CSV *_tracks_ground_L02_frames.csv.</summary>
        IEnumerator PlantK81d()
        {
            var at = new Dictionary<string, (int x, int y)>();
            foreach (var n in new[] { "v2", "v1", "bridge north", "v2 south", "v1 south", "jeep" }) { if (!Spot(n, out int sx, out int sy)) yield break; at[n] = (sx, sy); }
            foreach (var kv in PlantBank) { at[kv.Key] = kv.Value; Note($"bank stretch {kv.Key}: tile ({kv.Value.x},{kv.Value.y}) (k81d: the canal bank's real slope)"); }
            var v2 = Heavy(at["v2"].x, at["v2"].y, Team.Red, 2, "red v2 (track test)"); Unhurt(v2);
            var v1 = Heavy(at["v1"].x, at["v1"].y, Team.Red, 1, "red v1 (track test)"); Unhurt(v1);
            if (!CheckHeavy(v2, "red v2 (track test)", false) || !CheckHeavy(v1, "red v1 (track test)", false)) yield break;
            var jeep = Vehicle("jeep", at["jeep"].x, at["jeep"].y, Team.Blue, "blue jeep (the target)");
            jeep.MaxHealth = 100000000; jeep.SetHealth(jeep.MaxHealth); W.UpdateObjectHealth(jeep); jeep.Damage = 0; jeep.AttackRadius = 0;
            Note("staged: the jeep cannot be destroyed and has no weapon (the target the tanks fire at while they stand at the bridge)");
            PlantJeepK87b(jeep);                                                                       // k87b: the target a tank takes at a stand within its gun's reach; it never steps aside (View/HeavyV2Films.K87b.cs)
            beltWatch.Add((v2, "v2")); beltWatch.Add((v1, "v1"));
            hero = v2; follow.Add(v2); follow.Add(v1);
            int frames0 = TrackGround.Frames; TrackGround.MaxHullLiftM = 0f;
            Note($"TrackGround: hull feel {TrackGround.HullFeel:0.00}, tilt rate {TrackGround.TiltRate:0} /s, {TrackGround.Stations} stations a belt (proof {TrackGround.ProofStations} x 3); VehicleMotion.SlotScroll {VehicleMotion.SlotScroll}");
            yield return Hold(2.5f);
            yield return StartTake();
            StartCoroutine(BeltSampler());
            yield return TakeWait(1.0f);
            for (int phase = 0; phase < 2; phase++)
            {
                bool on = phase == 1;
                TrackGround.Enabled = on; beltPhase = on ? GearOnPhase : GearOffPhase;
                Note(beltPhase);
                // the way out (OFF): v2 first, v1 4 s behind; the way back (ON): v1 first, v2 4 s behind
                var a = new Leg { o = on ? v1 : v2 }; var b = new Leg { o = on ? v2 : v1 };
                if (!on)
                {
                    a.goals = new List<(int, int, float)> { (at["bank v2"].x, at["bank v2"].y, 0f), (at["bridge north"].x, at["bridge north"].y, 3f), (at["v2 south"].x, at["v2 south"].y, 0f) };
                    b.goals = new List<(int, int, float)> { (at["bank v1"].x, at["bank v1"].y, 0f), (at["bridge north"].x, at["bridge north"].y, 3f), (at["v1 south"].x, at["v1 south"].y, 0f) };
                }
                else
                {
                    a.goals = PlantBackK87b(at, "v1");                                                    // k87b, items 5 / 9 / 15: the bridge stand, then 2.5 s ON the canal bank, then home (View/HeavyV2Films.K87b.cs)
                    b.goals = PlantBackK87b(at, "v2");
                }
                int t0 = frameNo;
                while (!done && frameNo - t0 < 130 * Fps && !(a.Done && b.Done))
                {
                    LegStep(a);
                    if (frameNo - t0 > 4 * Fps) LegStep(b);
                    yield return null;
                }
                Note($"{beltPhase}: done after {(frameNo - t0) / (float)Fps:0.0} s{(a.Done && b.Done ? "" : " (CUT at 130 s)")}; v2 at tile ({v2.CenterX >> 4},{v2.CenterY >> 4}), v1 at ({v1.CenterX >> 4},{v1.CenterY >> 4})");
                beltPhase = "-";
                yield return TakeWait(1.0f);
            }
            TrackGround.Enabled = true; Note("TrackGround.Enabled ON (after the test)");
            yield return TakeWait(0.5f);
            StopTake(); WriteCrops();
            var sb = new StringBuilder("unit,frame,t,phase,switch_frame,moving,fired,ground_slope_deg,ground_pitch_deg,ground_roll_deg,belt_l_pitch_deg,belt_r_pitch_deg,belts_roll_deg,belt_deepest_below_ground_m,belt_largest_gap_m,belt_mid_over_ground_m,hull_pitch_over_belt_deg,hull_roll_over_belt_deg,hull_lift_m\n");
            foreach (var r in beltRows)
                sb.Append(r.tag).Append(',').Append(r.frame).Append(',').Append(F(r.frame / (float)Fps, "0.000")).Append(',').Append(r.on ? "ON" : "OFF").Append(',').Append(r.first ? 1 : 0).Append(',')
                  .Append(r.moving ? 1 : 0).Append(',').Append(r.fired ? 1 : 0).Append(',').Append(F(r.slope, "0.00")).Append(',').Append(F(r.gPitch, "0.00")).Append(',').Append(F(r.gRoll, "0.00")).Append(',')
                  .Append(F(r.pL, "0.000")).Append(',').Append(F(r.pR, "0.000")).Append(',').Append(F(r.roll, "0.000")).Append(',').Append(F(r.sink, "0.000")).Append(',').Append(F(r.gap, "0.000")).Append(',')
                  .Append(F(r.mid, "0.000")).Append(',').Append(F(r.hPitch, "0.000")).Append(',').Append(F(r.hRoll, "0.000")).Append(',').Append(F(r.lift, "0.000")).Append('\n');
            string rowsCsv = Path.Combine(Dir, take.Replace("_plant_", "_tracks_ground_") + "_frames.csv");
            File.WriteAllText(rowsCsv, sb.ToString());
            Fact($"the belts and the hull against the ground every frame: {rowsCsv}");
            foreach (var tag in new[] { "v2", "v1" })
                foreach (bool on in new[] { false, true })
                {
                    int n = 0, moving = 0, fired = 0, sloped = 0, flat = 0, sunk = 0, standing = 0, blocks = 0; float deepest = -9f, maxLift = 0f, standP = 0f, standR = 0f, standM = 0f, sw = -9f;
                    var gaps = new List<float>(); var offP = new List<float>(); var offR = new List<float>(); var hP = new List<float>(); var hR = new List<float>();
                    float bpLo = 99f, bpHi = -99f, brLo = 99f, brHi = -99f, bmLo = 99f, bmHi = -99f; int run = 0, lastFrame = -9;
                    foreach (var r in beltRows)
                    {
                        if (r.tag != tag || r.on != on) continue;
                        if (r.first) { sw = r.sink; continue; }                                        // the switch frame: on its own
                        n++; if (r.moving) moving++; if (r.fired) fired++;
                        if (r.slope >= 3f) sloped++;
                        if (r.slope < 0.5f) flat++;
                        if (r.sink > 0.03f) sunk++;
                        deepest = Mathf.Max(deepest, r.sink); maxLift = Mathf.Max(maxLift, r.lift); gaps.Add(r.gap);
                        offP.Add(Mathf.Abs(0.5f * (r.pL + r.pR) - r.gPitch)); offR.Add(Mathf.Abs(r.roll - r.gRoll));
                        hP.Add(Mathf.Abs(r.hPitch)); hR.Add(Mathf.Abs(r.hRoll));
                        // standing: a block of frames without moving; its first 0.3 s are left out (the belts' pitch settles after the stop)
                        if (!r.moving && r.frame == lastFrame + 1) run++; else run = r.moving ? 0 : 1;
                        lastFrame = r.frame;
                        if (!r.moving && run == 1) { bpLo = brLo = bmLo = 99f; bpHi = brHi = bmHi = -99f; }
                        if (!r.moving && run > Mathf.RoundToInt(0.3f * Fps))
                        {
                            standing++; if (run == Mathf.RoundToInt(0.3f * Fps) + 1) blocks++;
                            float pm = 0.5f * (r.pL + r.pR);
                            bpLo = Mathf.Min(bpLo, pm); bpHi = Mathf.Max(bpHi, pm); brLo = Mathf.Min(brLo, r.roll); brHi = Mathf.Max(brHi, r.roll); bmLo = Mathf.Min(bmLo, r.mid); bmHi = Mathf.Max(bmHi, r.mid);
                            standP = Mathf.Max(standP, bpHi - bpLo); standR = Mathf.Max(standR, brHi - brLo); standM = Mathf.Max(standM, bmHi - bmLo);
                        }
                    }
                    float g95 = P95(gaps), hp95 = P95(hP), hr95 = P95(hR), op95 = P95(offP), or95 = P95(offR);
                    string ph = on ? GearOnPhase : GearOffPhase;
                    string what = $"{n} frames ({n / (float)Fps:0.0} s; {moving} moving, {fired} within 0.5 s of a shot, {sloped} on ground sloped 3 deg or more, {flat} on flat ground, {standing} standing in {blocks} stand(s)): " +
                                  $"frames with a belt point more than 3 cm in the ground {sunk}, deepest {deepest:0.000} m; the gap under a belt p95 {g95:0.000} m; " +
                                  $"STANDING (the hull rocks over them): the belts' pitch moved up to {standP:0.00} deg, roll {standR:0.00} deg, their bottom {standM:0.000} m; " +
                                  $"HULL over the belts: p95 pitch {hp95:0.00} deg, roll {hr95:0.00} deg, largest lift {maxLift:0.000} m; " +
                                  $"(told, not judged: the belts against the ground's 2-point pitch / roll p95 {op95:0.00} / {or95:0.00} deg: a rigid belt rests on the high points of the real ground under it); " +
                                  $"the switch frame (the first frame of this phase, drawn by the phase before): deepest {(sw > -8f ? sw.ToString("0.000") : "-")} m";
                    bool pass = n > 30 && moving > 0 && fired > 0 && sloped >= 10 && flat >= 10 && standing >= 30 && sunk == 0 && g95 <= 0.03f
                                && standP <= 0.3f && standR <= 0.3f && standM <= 0.03f && (hp95 >= 0.1f || hr95 >= 0.1f) && hp95 <= K93HullCap() && hr95 <= K93HullCap();   // k93: the bigger shot rock (View/HeavyV2Films.K93.cs)
                    Fact(on ? $"tracks {tag} / {ph}: {what}: {(pass ? "PASS" : "FAIL")} (never more than 3 cm in the ground, little air under them; standing, the belts still while the hull rocks a little over them, under 2.5 deg; driving, firing, sloped and flat ground and standing all seen)"
                            : $"tracks {tag} / {ph} (the CONTROL): {what} -> {(sunk > 0 || standP > 0.3f || standR > 0.3f || standM > 0.03f ? "the tracks in the ground / moving with the hull SHOWN without the planting" : "no track fault shown without the planting in this run")}");
                }
            Fact($"planting counters: frames planted {TrackGround.Frames - frames0}, the hull's largest lift over its belts {TrackGround.MaxHullLiftM:0.000} m; tank shots in the take (sound log) {Count("heavyshot") + Count("tankshot")}");
            Fact("pass rule (the lines above + the MP4): with the planting ON each belt of v1 and v2 lies on the ground under it, never more than 3 cm in it, over the canal bank's slope, the bridge's ramps and deck and the flat; it never wobbles with the hull: standing and firing it does not move while the hull and turret rock a little above it; the OFF lines are the control (the old drawing)");
        }

        // ================================================================================================ k81d: item 130, the driver against a sniper (film 8)
        /// <summary>k81d, item 130 : the red v2's driver comes UP out of the real (left) hatch and fires
        /// several shots before a blue sniper gets him. Staged and logged: the level's own robots near the stage stand down and walk away first
        /// (in k81c's take its armed grunts two tiles off the tank shot the resting driver 1.0 s in, and one of them boarded the empty tank);
        /// two blue snipers stand in the tank's range without a weapon (the tank takes one as its target: Zod's lid roll opens the hatch, the
        /// driver comes up and fires at it with his own rifle); after his fourth shot a third, armed sniper walks in from out of range and snipes
        /// him (the game's snipe rule; nothing forces it); every blue robot then stands down and walks away; the empty, grey tank stands alone
        /// with its lid open; only then a red grunt walks up and boards it. The pass rule is counted (the FACT lines).</summary>
        IEnumerator SnipeK81d()
        {
            var at = new Dictionary<string, (int x, int y)>();
            foreach (var n in new[] { "red v2", "sniper 1", "sniper 2", "away" }) { if (!Spot(n, out int sx, out int sy)) yield break; at[n] = (sx, sy); }
            var walkIn = (x: 40, y: 10);                                                               // out of every range of the tank (220 px), open ground north-east of it
            Note($"the walking sniper starts at tile ({walkIn.x},{walkIn.y}) (k81d: out of the tank's and the driver's range)");
            int cx = at["red v2"].x * 16 + 16, cy = at["red v2"].y * 16 + 16;
            // 1. the level's own units near the stage: no weapon, the robots walk away (staged)
            var level = new List<ZObject>();
            foreach (var o in W.Objects)
                if (o != null && !o.Destroyed && o.Owner != Team.Neutral && (o.Kind == ZKind.Robot || o.Kind == ZKind.Cannon || o.Kind == ZKind.Vehicle)
                    && Mathf.Abs(o.CenterX - cx) <= 22 * 16 && Mathf.Abs(o.CenterY - cy) <= 22 * 16) level.Add(o);
            foreach (var o in level) { o.Damage = 0; o.AttackRadius = 0; o.AttackObject = null; }
            int walkers = 0;
            foreach (var o in level) if (o.Kind == ZKind.Robot && !o.IsMinion && Order(o, MoveTo(at["away"].x, at["away"].y, false))) walkers++;
            Note($"staged: the level's own {level.Count} unit(s) within 22 tiles of the stage have no weapon now; {walkers} robot group(s) walk away to ({at["away"].x},{at["away"].y})");
            float w0 = Real;
            while (!done && Real - w0 < 40f)
            {
                bool near = false;
                foreach (var o in level) if (o.Kind == ZKind.Robot && !o.Destroyed && Mathf.Abs(o.CenterX - cx) < 13 * 16 && Mathf.Abs(o.CenterY - cy) < 13 * 16) { near = true; break; }
                if (!near) break;
                yield return null;
            }
            Note($"the level's robots are clear of the stage (13 tiles) after {Real - w0:0.0} s");
            // 2. the cast
            hero = Heavy(at["red v2"].x, at["red v2"].y, Team.Red, 2, "red v2 (the one filmed)");
            if (!CheckHeavy(hero, "red v2 (stands all take: its lid and driver are filmed)", true)) yield break;
            hero.MaxHealth *= 10; hero.SetHealth(hero.MaxHealth); W.UpdateObjectHealth(hero);
            int heroDamage = hero.Damage, heroRadius = hero.AttackRadius; hero.Damage = 0; hero.AttackRadius = 0;
            var s1 = Squad("sniper", at["sniper 1"].x, at["sniper 1"].y, Team.Blue, 1, "blue sniper 1 (no weapon: the tank's target)");
            var s2 = Squad("sniper", at["sniper 2"].x, at["sniper 2"].y, Team.Blue, 1, "blue sniper 2 (no weapon: the spare target)");
            var s3 = Squad("sniper", walkIn.x, walkIn.y, Team.Blue, 1, "blue sniper 3 (walks in after the driver's shots)");
            foreach (var q in new[] { s1, s2, s3 }) foreach (var m in Members(q)) { m.MaxHealth *= 100; m.SetHealth(m.MaxHealth); W.UpdateObjectHealth(m); m.Damage = 0; m.AttackRadius = 0; }
            Note("staged: the v2 x10 health and no target until the take runs; the three snipers x100 health (the tank's gun does not end them) and no weapon; sniper 3 gets his back after the driver's 4th shot");
            yield return Hold(2.5f);
            yield return StartTake();
            yield return TakeWait(1.0f);
            // 3. the tank takes sniper 1 as its target (Zod: the lid opens 4 times in 5 on a target that snipes); another roll on sniper 2 if not
            hero.Damage = heroDamage; hero.AttackRadius = heroRadius;
            int shots0 = hero.DriverShots, hits0 = hero.DriverHits, tries = 0, upFirst = -1, snipedAt = -1, armedAt = -1, shotsAtSnipe = -1;
            float maxLid = 0f, bestOver = -9f;
            var targets = new[] { s1, s2 };
            while (!done && tries < 4 && !hero.LidOpen)
            {
                var t = targets[tries % 2]; tries++;
                Order(hero, Attack(t)); if (hero.AttackObject != t) hero.Engage(t);
                Note($"the v2 takes {(t == s1 ? "sniper 1" : "sniper 2")} as its target (try {tries}); lid {(hero.LidOpen ? "OPEN" : "shut")}");
                int tw = frameNo; while (!done && frameNo - tw < 2 * Fps && !hero.LidOpen) yield return null;
            }
            Note($"the lid (sim) is {(hero.LidOpen ? "open" : "STILL SHUT")} after {tries} target(s)");
            // 4. the driver comes up and fires; after his 4th shot sniper 3 walks in armed
            int startF = frameNo;
            while (!done && frameNo - startF < 60 * Fps)
            {
                var v = View(hero);
                float lid = LidAngle(v, out bool up, out float over); maxLid = Mathf.Max(maxLid, lid);
                if (up) { if (upFirst < 0) { upFirst = frameNo; Note($"the driver is up: lid {lid:0} deg, {over:0.00} m over the rim"); } bestOver = Mathf.Max(bestOver, over); }
                if (armedAt < 0 && (hero.DriverShots - shots0 >= 4 || frameNo - startF > 25 * Fps))
                {
                    armedAt = frameNo;
                    foreach (var m in Members(s3)) { m.Damage = m.U.Damage; m.AttackRadius = m.U.attack_radius; }
                    Order(s3, Attack(hero));
                    Note($"sniper 3 gets his weapon and walks in to attack the v2 (the driver's shots so far {hero.DriverShots - shots0})");
                }
                if (hero.Owner == Team.Neutral || hero.Drivers.Count == 0) { snipedAt = frameNo; shotsAtSnipe = hero.DriverShots - shots0; Note("the driver is out: the tank is empty (neutral)"); break; }
                yield return null;
            }
            Fact($"driver up from {(upFirst >= 0 ? (upFirst / (float)Fps).ToString("0.0") + " s" : "NEVER")}: lid up to {maxLid:0} deg (v1: 95), his top {bestOver:0.00} m over the rim; his shots {hero.DriverShots - shots0}, hits {hero.DriverHits - hits0}");
            Fact($"sniped: {(snipedAt >= 0 ? (snipedAt / (float)Fps).ToString("0.0") + " s, by sniper 3 (the only blue robot with a weapon), the tank " + hero.Owner + $"; the driver's shots before it {shotsAtSnipe}" : "NOT within 60 s of the lid (re-run; nothing forces it)")}");
            int emptyF = 0; bool reboardedEarly = false; string emptyPaint = "-"; float emptyLid = 0f;
            int boardF = -1; string boardOwner = "-", boardPaint = "-";
            if (snipedAt >= 0)
            {   // staged, logged: every blue robot stands down and walks away; the empty tank stands alone, then a red grunt boards it
                foreach (var q in new[] { s1, s2, s3 }) foreach (var m in Members(q)) { if (m.Destroyed) continue; m.Damage = 0; m.AttackRadius = 0; m.AttackObject = null; }
                foreach (var q in new[] { s1, s2, s3 }) if (!q.Destroyed) Order(q, MoveTo(at["away"].x, at["away"].y, false));
                Note("staged: the blue robots stand down (no weapon) and walk away after the snipe; nobody else near the tank");
                int e0 = frameNo;
                while (!done && frameNo - e0 < 5 * Fps)
                {
                    var v = View(hero);
                    if (hero.Owner == Team.Neutral && Boarding(v) == 0) emptyF++; else reboardedEarly = true;
                    yield return null;
                }
                { var v = View(hero); emptyPaint = Paint(v); emptyLid = LidAngle(v, out _, out _); }
                Fact($"empty after the snipe: {emptyF / (float)Fps:0.0} s with nobody boarding (owner {hero.Owner}), paint {emptyPaint}, lid {emptyLid:0} deg (a sniped tank keeps its lid open){(reboardedEarly ? "; SOMEBODY BOARDED IT EARLY" : "")}");
                var hv = View(hero); Transform ap = null;
                if (hv) foreach (var tr in hv.GetComponentsInChildren<Transform>(true)) if (tr.name.EndsWith("_Board_Approach")) { ap = tr; break; }
                Vector3 c0 = hv ? hv.transform.position : At(hero);
                Vector3 side = ap ? Vector3.ProjectOnPlane(ap.position - c0, Vector3.up) : Vector3.right;
                if (side.sqrMagnitude < 1e-4f) side = Vector3.right;
                var p = ZGame.WorldToPx((ap ? ap.position : c0) + side.normalized * 8f);
                var rb = Squad("grunt", p.x >> 4, p.y >> 4, Team.Red, 1, "red grunt (boards the empty tank)");
                yield return TakeWait(0.5f);
                Order(rb, Enter(hero));
                int t0 = frameNo; while (!done && frameNo - t0 < 30 * Fps && !(hero.Owner != Team.Neutral && Boarding(View(hero)) == 0)) yield return null;
                boardF = frameNo; boardOwner = hero.Owner.ToString();
                yield return TakeWait(3f);
                boardPaint = Paint(View(hero));
                Fact($"boarded again: owner {boardOwner}, paint {boardPaint}, {(boardF - snipedAt) / (float)Fps:0.0} s after the snipe");
            }
            yield return TakeWait(0.5f);
            StopTake(); WriteCrops();
            bool pass = upFirst >= 0 && snipedAt > upFirst && shotsAtSnipe >= 3 && emptyF >= 3 * Fps && !reboardedEarly && emptyPaint == Team.Neutral.ToString()
                        && boardOwner == Team.Red.ToString() && boardPaint == Team.Red.ToString();
            Fact($"snipe film: {(pass ? "PASS" : "FAIL")} (the driver up before the snipe: {(upFirst >= 0 && snipedAt > upFirst ? "yes" : "NO")}; his shots before it {shotsAtSnipe} (3 or more); sniped: {(snipedAt >= 0 ? "yes" : "NO")}; the empty grey tank alone {emptyF / (float)Fps:0.0} s (3 s or more, nobody boarding: {(!reboardedEarly ? "yes" : "NO")}, paint {emptyPaint}); boarded again by a RED robot: owner {boardOwner}, paint {boardPaint})");
            Fact("pass rule (the counts above + crops + MP4): the LEFT cupola's lid swings up about 95 deg and the driver rises out of it, aims and fires his rifle at the blue sniper several times (shots, the rifle sound); a third sniper walks in and his hit takes the driver out: he drops back, the tank turns grey (empty) with its lid left open and stands alone for a few seconds; then a red robot climbs the ladder and the tank turns red again");
        }

        // ================================================================================================ the end
        void Finish(string why)
        {
            if (done) return; done = true;
            UnitAim.SteadyOn = true; UnitAim.CombatSteady = true; TrackGround.Enabled = true;         // the tests' control runs never outlive the take
            try
            {
                if (recording) { Note("the take was cut: " + why); StopTake(); WriteCrops(); }
                Note(why == null ? "DONE" : "STOPPED: " + why);
                log.AppendLine($"errors/exceptions in the Console during the run: {errors}; GPU peak (GpuGuard) {peak} C");
                log.AppendLine("## FACTS"); foreach (var f in facts) log.AppendLine("  " + f);
                Directory.CreateDirectory(Dir);
                File.WriteAllText(Path.Combine(Dir, (take ?? "hv2_film") + "_log.txt"), log.ToString());
            }
            catch (Exception e) { Debug.LogError("[hv2films] finish: " + e); }
            if (Time.captureFramerate != 0) Time.captureFramerate = 0;
            UnityEditor.EditorApplication.isPlaying = false;
        }

        void OnDestroy()
        {
            UnitAim.SteadyOn = true; UnitAim.CombatSteady = true; TrackGround.Enabled = true;
            recording = false;
            if (audioOn) { AudioRenderer.Stop(); audioOn = false; }
            if (Time.captureFramerate != 0) Time.captureFramerate = 0;
        }
    }
}
#endif
