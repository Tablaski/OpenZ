#if !OPENZ_HARNESS
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using OpenZ.Sim;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// Editor only, session only: nothing of the game changes, no asset is written, Resources/OpenZ/Loading/L17_loading_1.png is NOT touched.
    /// Menu "OpenZ/1.5/Cards/L17 card variants (Play)": in Play (from the main menu), deploys level 17 as red with the briefing off, waits
    /// for the level-start flight to end, takes the CPU players off for the stills, and renders every pose of the list below the way the
    /// morning's card still was made: View/OzStillCam (the game camera with its post, HUD and cursor hidden, no selection), 3840 x 2400
    /// with MSAA 4x, then a Lanczos-3 copy 2560 x 1600 RGB PNG (the size / format of every card). Night 0.62 (lamps, building lights and
    /// searchlights on) unless the pose says dusk; the two storm poses come last under a forced shower (OzWeather.ForceStorm).
    /// sheet L17_card_variants_sheet.jpg and variants_log.txt. At the end everything it changed is put back: the clock and its running
    /// flag, the CPU players, the storm and the NIGHT &amp; RAIN option, the camera (OzStillCam.End), the briefing and PLAY AS BLUE boxes.
    /// The poses were planned off Unity from the map data (kit_tools/plan_poses.py: every frame's ground footprint inside the developed
    /// play area, x 12..206, z -244..-12 m: no outside ground, no map edge, no sky). From code: OzV15L17CardVariantsMenu.Begin("3,11")
    /// renders only those numbers; poll OzV15L17CardVariantsMenu.Status.
    /// </summary>
    public static class OzV15L17CardVariantsMenu
    {
        public const string Mark = "l17_card_variants_r7";
        const string Tag = "[OzV15L17CardVariants]";
        const int MasterW = 3840, MasterH = 2400, CardW = 2560, CardH = 1600;
        public static string Status = "idle";
        static string outSub = "candidates"; static bool gentle;
        static Pose[] Active => gentle ? Gentle : Poses;
        static string Pre => gentle ? "v" : "";
        public static string OutDir => Path.GetFullPath(Path.Combine(Application.dataPath, "../../../../work/staging/v1_5/kits/l17_card_variants_r7/proof/" + outSub)).Replace('\\', '/');

        struct Pose { public int n; public string name; public Vector3 p, l; public float fov, tod; public bool storm; }
        static Pose V(int n, string name, float px, float py, float pz, float lx, float ly, float lz, float fov, float tod, bool storm) =>
            new Pose { n = n, name = name, p = new Vector3(px, py, pz), l = new Vector3(lx, ly, lz), fov = fov, tod = tod, storm = storm };

        /// <summary>The planned poses (kit_tools/plan_poses.py -> proof/pose_plan.json; the README has what each one shows). Unity metres.</summary>
        static readonly Pose[] Poses =
        {
            V(1, "west_lot_high_from_north", 68.6f, 52f, -157.4f, 72f, 1.5f, -206f, 26f, 0.62f, false),
            V(2, "canal_two_bridges", 110f, 56f, -135.5f, 110f, 1.5f, -196f, 28f, 0.62f, false),
            V(3, "city_canal_lamps", 110f, 30f, -41.5f, 110f, 1.5f, -112f, 20f, 0.62f, false),
            V(4, "red_fort_searchlights", 186.8f, 36f, -24.8f, 171f, 1.5f, -80f, 24f, 0.62f, false),
            V(5, "blue_fort_howitzers", 32.7f, 42f, -89.4f, 47f, 1.5f, -50f, 24f, 0.62f, false),
            V(6, "central_crossings_road", 175.8f, 32f, -90.5f, 108f, 1.5f, -100f, 18f, 0.62f, false),
            V(7, "pier_rd_bridge_road", 37.9f, 28f, -198.5f, 100f, 1.5f, -192f, 19f, 0.62f, false),
            V(8, "east_lot_down_the_rows", 189f, 30f, -218.1f, 150f, 1.5f, -214f, 22f, 0.62f, false),
            V(9, "car_park_wide", 15f, 50f, -210.1f, 90f, 1.5f, -190f, 32f, 0.44f, false),
            V(10, "west_lot_down_the_rows", 124.2f, 24f, -217.7f, 76f, 1.5f, -216f, 20f, 0.62f, false),
            V(11, "war_machines_canal_side", 111.4f, 30f, -192.8f, 72f, 1.5f, -212f, 20f, 0.62f, true),
            V(12, "canal_bridge_diagonal", 83.5f, 40f, -148.1f, 110f, 1.5f, -194f, 24f, 0.62f, true),
        };

        // the SAME view as the current card (42, 6, -238) -> (84, 2.5, -211.1) FOV 34, changed only a little.
        static readonly Vector3 CardP = new Vector3(42f, 6f, -238f), CardL = new Vector3(84f, 2.5f, -211.1f);
        static Pose G(int n, string name, float back, float up, float yaw, float lookDy, float fov, bool storm)
        {
            Vector3 h = CardL - CardP; h.y = 0f; h.Normalize();
            Vector3 p = CardP - h * back + Vector3.up * up;
            Vector3 l = p + Quaternion.AngleAxis(yaw, Vector3.up) * (CardL + Vector3.up * lookDy - p);
            return new Pose { n = n, name = name, p = p, l = l, fov = fov, tod = 0.62f, storm = storm };
        }
        static readonly Pose[] Gentle =
        {
            G(1, "current_pose_control", 0f, 0f, 0f, 0f, 34f, false),
            G(2, "fov38", 0f, 0f, 0f, 0f, 38f, false),
            // take 2 (take 1 pulled back 4-8 m: a near lamp head and the west factory got cut by the frame; raised poses cut the walker's feet):
            // no pull back, zoom by FOV only, the look point lowered so the walker stays whole and the sky band shrinks.
            G(3, "fov40_up1", 0f, 1f, 0f, -1f, 40f, false),
            G(4, "up3_fov38", 0f, 3f, 0f, -3f, 38f, false),
            G(5, "up2_fov41_yaw-2", 0f, 2f, -2f, -2f, 41f, false),
            G(6, "fov40_up1", 0f, 1f, 0f, -1f, 40f, true),
            G(7, "up3_fov38", 0f, 3f, 0f, -3f, 38f, true),
            G(8, "up2_fov41_yaw-2", 0f, 2f, -2f, -2f, 41f, true),
            G(9, "up5_fov42", 0f, 5f, 0f, -4.5f, 42f, true),
            G(10, "yaw+4_up2_fov38", 0f, 2f, 4f, -2.5f, 38f, true),
        };

        [MenuItem("OpenZ/1.5/Cards/L17 card gentle variants (Play)")]
        static void MenuGentle() => BeginGentle("");
        [MenuItem("OpenZ/1.5/Cards/L17 card gentle variants (Play)", true)]
        static bool MenuGentleCan() => Application.isPlaying;
        /// <summary>Gentle variations of the current card; a new proof/gentle_HHmm folder each full run, the last one again for a re-take.</summary>
        public static string BeginGentle(string only)
        {
            if (run != null) return "a run is still going: " + Status;
            gentle = true;
            if (string.IsNullOrEmpty(only) || !outSub.StartsWith("gentle_")) outSub = "gentle_" + DateTime.Now.ToString("HHmm");
            return Begin(only);
        }
        public static string GentleDir { get => outSub; set { outSub = value; gentle = true; } }

        static IEnumerator<double> run; static double waitUntil; static StringBuilder log;
        static bool changed, weatherWas, blueWas, runWas, stillOn, stormOn; static int briefWas; static float todWas;
        static DayNight clock; static ZWorld botWorld; static List<ZBot> botsOff;
        static double Now => EditorApplication.timeSinceStartup;
        static bool Flying => ZodFlyover.Live != null && ZodFlyover.Live.Active;
        static bool Briefing => ZodBriefing.BriefingActive || OzBlueCameo.BriefingActive;
        static void Log(string s) { log.AppendLine(DateTime.Now.ToString("HH:mm:ss") + " " + s); Debug.Log(Tag + " " + s); }

        [MenuItem("OpenZ/1.5/Cards/L17 card variants (Play)")]
        static void MenuRun() { gentle = false; outSub = "candidates"; Begin(""); }

        [MenuItem("OpenZ/1.5/Cards/L17 card variants (Play)", true)]
        static bool MenuCan() => Application.isPlaying;

        /// <summary>only: "" = every pose, or numbers like "3,11".</summary>
        public static string Begin(string only)
        {
            if (!Application.isPlaying) { Debug.LogWarning(Tag + " enter Play on the MainMenu scene first"); return "not in Play"; }
            if (run != null) return "a run is still going: " + Status;
            Directory.CreateDirectory(OutDir + "/_masters_3840x2400"); log = new StringBuilder(); Status = "running";
            run = Run(only ?? ""); waitUntil = 0; EditorApplication.update -= Tick; EditorApplication.update += Tick;
            return "started: " + OutDir;
        }

        static void Tick()
        {
            if (run == null) { EditorApplication.update -= Tick; return; }
            if (!Application.isPlaying) { Log("Play left: stopped"); Status = "stopped: Play ended"; Done(); return; }
            if (Now < waitUntil) return;
            bool more;
            try { more = run.MoveNext(); } catch (Exception e) { Log("EXCEPTION " + e); Status = "FAILED: " + e.Message; more = false; }
            if (!more) { Done(); return; }
            waitUntil = Now + run.Current;
        }

        static void Done()
        {
            EditorApplication.update -= Tick; run = null;
            try { PutBack(); } catch (Exception e) { Log("EXCEPTION while putting back " + e); }
            try { File.WriteAllText(OutDir + "/variants_log.txt", log.ToString()); } catch (Exception) { }
            Debug.Log(Tag + " " + Status + " | log " + OutDir + "/variants_log.txt");
        }

        /// <summary>Everything the run changed goes back (also after a failure or when Play was left part way).</summary>
        static void PutBack()
        {
            if (!changed) return;
            changed = false;
            bool play = Application.isPlaying;
            if (play && stormOn && OzWeather.I) OzWeather.I.StopStorm(3f);
            stormOn = false;
            if (play && stillOn) Log(OzStillCam.End());
            stillOn = false;
            if (play && clock) { clock.timeOfDay = todWas; clock.Apply(todWas); clock.running = runWas; }
            if (play && botsOff != null && botWorld != null && ZGame.I && ZGame.I.World == botWorld && botWorld.Bots.Count == 0) botWorld.Bots.AddRange(botsOff);
            botsOff = null; botWorld = null; clock = null;
            if (OzWeather.Enabled != weatherWas) OzWeather.Enabled = weatherWas;
            PlayerPrefs.SetInt(ZodBriefing.PrefBriefing, briefWas); OzPlayBlue.Box = blueWas; PlayerPrefs.Save();
            Log($"put back: briefing {PlayerPrefs.GetInt(ZodBriefing.PrefBriefing, 1)}, PLAY AS BLUE {OzPlayBlue.Box}, NIGHT & RAIN {OzWeather.Enabled}"
                + (play && ZGame.I && ZGame.I.World != null ? $", clock {todWas:0.000} running {runWas}, CPU players {ZGame.I.World.Bots.Count}, camera handed back" : " (Play had ended: the session state is gone with it)"));
        }

        static void Light(float t) { if (!clock) return; clock.running = false; clock.timeOfDay = t; clock.Apply(t); }

        static IEnumerator<double> Run(string only)
        {
            var pick = new HashSet<int>();
            foreach (string s in only.Split(',')) if (int.TryParse(s.Trim(), out int k)) pick.Add(k);
            briefWas = PlayerPrefs.GetInt(ZodBriefing.PrefBriefing, 1); blueWas = OzPlayBlue.Box; weatherWas = OzWeather.Enabled; changed = true;
            Log($"settings before: briefing {briefWas}, PLAY AS BLUE {blueWas}, NIGHT & RAIN {weatherWas}, difficulty {OpenZDifficulty.Choice}, head start {OpenZDifficulty.HeadStartToggle}, SHUTTLE box {OzShuttleStart.On} (the last three are left as they are)");

            // ---- level 17 as red from the main menu, the briefing off
            if (!UnityEngine.Object.FindFirstObjectByType<OpenZMainMenu>())
            {
                OzScenes.MainMenu(); double tm = Now;
                while (!UnityEngine.Object.FindFirstObjectByType<OpenZMainMenu>() && Now - tm < 60) yield return 0.2;
                yield return 2.5;
            }
            var m = UnityEngine.Object.FindFirstObjectByType<OpenZMainMenu>();
            int card = Array.FindIndex(OzLevelDef.All, x => x.Id == "L17");
            if (!m || card < 0) { Status = "FAILED: no main menu (" + (bool)m + ") or no L17 card (" + card + ")"; Log(Status); yield break; }
            PlayerPrefs.SetInt(ZodBriefing.PrefBriefing, 0); OzPlayBlue.Box = false;
            yield return 0.8; m.ProofSelect(); yield return 1.0; m.ProofFocus(card); yield return 0.8; m.ProofDeploy();
            double t0 = Now; while ((!ZGame.I || ZGame.I.World == null || ZGame.I.level != "L17") && Now - t0 < 150) yield return 0.2;
            if (!ZGame.I || ZGame.I.World == null || ZGame.I.level != "L17") { Status = "FAILED: level 17 did not load in 150 s"; Log(Status); yield break; }
            double tw = Now; bool sawFly = false;
            while (Now - tw < 90)
            {
                if (Briefing) { try { if (!ZodBriefing.ProofSkipBriefing()) OzBlueCameo.ProofSkipBriefing(); } catch (Exception) { } }
                if (Flying) sawFly = true; else if (Now - tw > 6 && !Briefing) break;
                yield return 0.25;
            }
            if (Flying || Briefing) { Status = "FAILED: the level-start flight / briefing did not end in 90 s"; Log(Status); yield break; }
            yield return 2.0;
            Log($"level {ZGame.I.level} up after {tw - t0:F1} s, human {ZGame.I.player}, PLAY AS BLUE {OzPlayBlue.Active}, level-start flight {(sawFly ? "seen and over" : "not seen")} after {Now - tw:F1} s");

            // ---- the stills' state: CPU players off, the clock held, the still camera
            botWorld = ZGame.I.World; botsOff = new List<ZBot>(botWorld.Bots); botWorld.Bots.Clear();
            clock = UnityEngine.Object.FindFirstObjectByType<DayNight>();
            if (clock) { todWas = clock.timeOfDay; runWas = clock.running; }
            string begun = OzStillCam.Begin(); stillOn = OzStillCam.I;
            Log($"CPU players off for the stills: {botsOff.Count}; clock {(clock ? todWas.ToString("0.000") + " running " + runWas : "NOT FOUND")}; {begun}");
            if (!stillOn) { Status = "FAILED: the still camera did not start (" + begun + ")"; Log(Status); yield break; }
            yield return 0.6;

            // ---- the poses: the dry ones by their light, the storm ones last
            var order = new List<Pose>();
            foreach (var q in Active) if (!q.storm && (pick.Count == 0 || pick.Contains(q.n))) order.Add(q);
            order.Sort((a, b) => a.tod != b.tod ? b.tod.CompareTo(a.tod) : a.n.CompareTo(b.n));
            foreach (var q in Active) if (q.storm && (pick.Count == 0 || pick.Contains(q.n))) order.Add(q);
            int ok = 0, bad = 0; float lit = -1f; var done = new List<Pose>();
            foreach (var q in order)
            {
                if (q.storm && !stormOn)
                {
                    if (!OzWeather.Enabled) OzWeather.Enabled = true;
                    if (!OzWeather.I) { Log($"{q.n:00} {q.name}: SKIPPED, this level has no OzWeather (no rain look to force)"); bad++; continue; }
                    Light(q.tod); lit = q.tod; yield return 1.0;
                    OzWeather.I.ForceStorm(600f, false); stormOn = true;
                    double ts = Now; while ((OzWeather.I.Rain < 0.95f || OzWeather.I.Wetness < 0.9f) && Now - ts < 60) yield return 0.5;
                    Light(q.tod);
                    Log($"storm forced: after {Now - ts:F1} s {OzWeather.I.Status()}");
                    yield return 2.0;
                }
                if (lit != q.tod) { Light(q.tod); lit = q.tod; yield return 3.0; }
                string pose = OzStillCam.Pose(q.p, q.l, q.fov);
                yield return 0.6;
                string file = $"{Pre}{q.n:00}_{q.name}{(q.storm ? "_storm" : q.tod < 0.5f ? "_dusk" : "_night")}";
                string master = OutDir + "/_masters_3840x2400/" + file + "_3840x2400.png", cardPng = OutDir + "/" + file + ".png";
                OzStillCam.Status = "idle";
                OzStillCam.Shoot(master, MasterW, MasterH, 12);
                double t1 = Now; while (!OzStillCam.Status.StartsWith("shot OK") && !OzStillCam.Status.StartsWith("shot FAILED") && Now - t1 < 40) yield return 0.1;
                if (!OzStillCam.Status.StartsWith("shot OK")) { bad++; Log($"{q.n:00} {q.name}: {pose} -> NO PICTURE ({OzStillCam.Status})"); continue; }
                yield return 0.05;
                string sized = ToCard(master, cardPng);
                if (sized.Length > 0) { bad++; Log($"{q.n:00} {q.name}: {pose} -> master OK, the 2560 x 1600 copy FAILED: {sized}"); continue; }
                ok++; done.Add(q);
                Log($"{q.n:00} {q.name}: {pose}; clock {(clock ? clock.timeOfDay : -1f):0.000}{(q.storm && OzWeather.I ? ", rain " + OzWeather.I.Rain.ToString("0.00") + " wet " + OzWeather.I.Wetness.ToString("0.00") + " flash " + OzWeather.I.FlashNow.ToString("0.00") : "")} -> {file}.png");
                Status = $"running: {ok} of {order.Count}";
                yield return 0.1;
            }
            if (stormOn && OzWeather.I)
            {
                OzWeather.I.StopStorm(3f); stormOn = false;
                double te = Now; while (OzWeather.I && OzWeather.I.StormActive && Now - te < 20) yield return 0.5;
                Log("storm stopped: " + (OzWeather.I ? OzWeather.I.Status() : "-"));
            }
            string sheet = "";
            try { sheet = Sheet(pick.Count == 0 ? null : pick); } catch (Exception e) { sheet = "sheet FAILED: " + e.Message; }
            Log(sheet);
            Status = $"DONE: {ok} candidate(s) 2560 x 1600, {bad} failed / skipped, in {OutDir}";
            Log(Status);
        }

        // ------------------------------------------------------------------ pictures (plain C#, no asset touched)
        static Color32[] Load(string path, out int w, out int h)
        {
            var t = new Texture2D(2, 2, TextureFormat.RGB24, false);
            try { t.LoadImage(File.ReadAllBytes(path)); w = t.width; h = t.height; return t.GetPixels32(); }
            finally { UnityEngine.Object.DestroyImmediate(t); }
        }

        static void Save(string path, Color32[] px, int w, int h, bool jpg)
        {
            var t = new Texture2D(w, h, TextureFormat.RGB24, false);
            try { t.SetPixels32(px); t.Apply(false); File.WriteAllBytes(path, jpg ? t.EncodeToJPG(92) : t.EncodeToPNG()); }
            finally { UnityEngine.Object.DestroyImmediate(t); }
        }

        /// <summary>The master as the card's 2560 x 1600 RGB PNG (Lanczos-3, as the morning's still). "" = written.</summary>
        static string ToCard(string master, string card)
        {
            try
            {
                var src = Load(master, out int w, out int h);
                if (w != MasterW || h != MasterH) return $"the master is {w} x {h}";
                Save(card, Resize(src, w, h, CardW, CardH), CardW, CardH, false);
                return "";
            }
            catch (Exception e) { return e.Message; }
        }

        static double Lanczos(double x)
        {
            x = Math.Abs(x);
            if (x < 1e-9) return 1.0;
            if (x >= 3.0) return 0.0;
            double a = Math.PI * x;
            return 3.0 * Math.Sin(a) * Math.Sin(a / 3.0) / (a * a);
        }

        static void Weights(int s, int d, out int[] first, out float[][] wt)
        {
            double scale = (double)s / d, fs = Math.Max(1.0, scale), sup = 3.0 * fs;
            first = new int[d]; wt = new float[d][];
            for (int i = 0; i < d; i++)
            {
                double c = (i + 0.5) * scale;
                int lo = Math.Max(0, (int)Math.Floor(c - sup)), hi = Math.Min(s - 1, (int)Math.Ceiling(c + sup));
                var row = new float[hi - lo + 1]; double sum = 0;
                for (int j = lo; j <= hi; j++) { double v = Lanczos((j + 0.5 - c) / fs); row[j - lo] = (float)v; sum += v; }
                for (int j = 0; j < row.Length; j++) row[j] = (float)(row[j] / sum);
                first[i] = lo; wt[i] = row;
            }
        }

        static byte B(float v) => (byte)(v <= 0f ? 0 : v >= 255f ? 255 : (int)(v + 0.5f));

        static Color32[] Resize(Color32[] src, int sw, int sh, int dw, int dh)
        {
            Weights(sw, dw, out int[] fx, out float[][] wx); Weights(sh, dh, out int[] fy, out float[][] wy);
            var tmp = new float[dw * sh * 3];
            for (int y = 0; y < sh; y++)
            {
                int so = y * sw, to = y * dw * 3;
                for (int x = 0; x < dw; x++)
                {
                    var w = wx[x]; int f = so + fx[x]; float r = 0f, g = 0f, b = 0f;
                    for (int k = 0; k < w.Length; k++) { var c = src[f + k]; float q = w[k]; r += c.r * q; g += c.g * q; b += c.b * q; }
                    tmp[to] = r; tmp[to + 1] = g; tmp[to + 2] = b; to += 3;
                }
            }
            var dst = new Color32[dw * dh];
            for (int y = 0; y < dh; y++)
            {
                var w = wy[y]; int f = fy[y];
                for (int x = 0; x < dw; x++)
                {
                    float r = 0f, g = 0f, b = 0f;
                    for (int k = 0; k < w.Length; k++) { int o = ((f + k) * dw + x) * 3; float q = w[k]; r += tmp[o] * q; g += tmp[o + 1] * q; b += tmp[o + 2] * q; }
                    dst[y * dw + x] = new Color32(B(r), B(g), B(b), 255);
                }
            }
            return dst;
        }

        // ---- the contact sheet: every candidate in the folder, numbered (a 1-bit label font, made off Unity by kit_tools/make_cs.py)
        const int GW = 18, GH = 34;
        const string Glyphs = " 0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ-.,:/+";
        const string FontB64 = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA+AD/gH/wPj4PB4eB4eB8eH8eP8c+8f88f48fg4fB4eB4PDwH/wD/gB+AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAeAB+AH+AP+APeAEeAAeAAeAAeAAeAAeAAeAAeAAeAAeAAeAP/4P/4P/4AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAB+AH/gP/gPHwEDwADwADwADwAHwAHgAPAAeAA8AB4ADwAHgAP/4P/4P/4AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAD+AP/gP/gEHwADwADwADwAHgD/AD/AD/wAH4AB4AB4AB4MD4P/wP/gH+AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAPgAfgA/gA/gB/gB3gD3gHngHHgPHgeHgcHgf/8f/8f/8AHgAHgAHgAHgAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAH/wH/wH/wHAAHAAHAAHAAH+AH/gH/wAHwAD4AB4AB4ADwAHwP/gP/AP8AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAfwB/wD/wHwAHgAPAAPAAP/AP/wf/4fD4eB4eA4PB4PB4Pj4H/wD/gB+AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAf/4f/4f/4AD4ADwAHwAHgAHgAPAAPAAeAAeAA8AA8AB4AB4ADwADwAHgAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAB/AH/gH/wPD4PB4PBwPjwH/gD/AB/AH/wPnwPB4OB4eB4PD4P/wH/gB/AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAB+AH/gP/wPHweD4eB4eB4eB4fD4P/4H/4D94AB4ADwADwAPgP/gP+AP4AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA+AB+AB/AB/AD3ADnADngHngHjgHDwHDwPDwP/4P/4f/4eB4cA88A88A8AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAP/AP/gP/wPD4PB4PB4PDwPDwP/gP/gP/wPD4PB4PB4PB4PD4P/wP/wP/AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAfwB/4D/4HwYPgAPAAPAAeAAeAAeAAeAAeAAfAAPAAPgAHwYH/4D/4A/gAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAf+Af/gf/weH4eD4eB4eB8eA8eA8eA8eA8eA8eB4eB4eD4eHwf/gf/Af8AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAH/wH/wH/wHAAHAAHAAHAAHAAH/wH/wH/wHAAHAAHAAHAAHAAH/wH/wH/wAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAH/wH/wH/wHAAHAAHAAHAAHAAH/wH/wH/wHAAHAAHAAHAAHAAHAAHAAHAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA/wD/4H/4PwYPAAfAAeAAeAAeP4eP4eP4eA4eA4eA4PA4Ph4H/4D/4A/wAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAeB4eB4eB4eB4eB4eB4eB4eB4f/4f/4f/4eB4eB4eB4eB4eB4eB4eB4eB4AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAP/wP/wP/wA8AA8AA8AA8AA8AA8AA8AA8AA8AA8AA8AA8AA8AP/wP/wP/wAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAH/gH/gH/gADgADgADgADgADgADgADgADgADgADgAHgAHgEPgP/AP+AD8AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAPB4PD4PDwPHgPPgPPAPeAPcAP8AP4AP8APeAPeAPPAPPgPHwPDwPD4PB8AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAHgAHgAHgAHgAHgAHgAHgAHgAHgAHgAHgAHgAHgAHgAHgAHgAH/4H/4H/4AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAfB4fD4fD4fj4fn4dn4d24d+4c+4c84c84c88cY8cA8cA8cA8cAccAccAcAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAfA4fg4fg4fg4fw4fw4f44e44e84e84ee4ee4eP4eP4eH4eH4eD4eD4eB4AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAB/AD/gH/wPj4eB4eA8eA8eA8cA88A8cA8eA8eA8eA4eB4Pj4P/wH/gB+AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAP+AP/gP/wPD4PB4PB4PB4PB4PB4PDwP/wP/gP+APAAPAAPAAPAAPAAPAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAB+AD/gH/wPj4eB4eA8eA8eA8cA88A8cA8eA8eA8eA4eB4Pj4P/wH/gB+AA8AAcAAeIAf8AP8AH4AAAAAAAAAAAAAAAAAAAAAAAAAAAP+AP/gP/wPHwPDwPBwPDwPHwP/gP+AP+APPAPHgPHgPDwPDwPD4PB4PB8AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAB/AH/wP/wPgwPAAPAAPgAP4AH/AD/gA/wAH4AD4AB4AB4cD4f/wf/gH+AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAf/4f/4f/4A8AA8AA8AA8AA8AA8AA8AA8AA8AA8AA8AA8AA8AA8AA8AA8AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAeA4eA4eA4eA4eA4eA4eA4eA4eA4eA4eA4eA4eA4eB4eB4PD4P/wH/gB+AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA8A8+A8eA8eA4eB4PB4PB4PDwHjwHjwHjgHngD3gD3AD/AB/AB+AB+AA+AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA8Ac8AccAccAccAccAcccccccc8cc+cc+4d+4d24dn4fn4fj4fD4fD4fD4AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAeB8fB4PDwPnwH3gD/gD/AB+AB+AA+AB+AB/AD/AHngHnwPDwPD4eB8+B8AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA8A8eA8eB4PB4PDwHjwHngD3gD/AB/AB+AA8AA8AA8AA8AA8AA8AA8AA8AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAP/4P/4P/4ADwAHgAHgAPAAfAAeAA8AA8AB4ADwADwAHgAPAAP/4f/4f/4AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAD/gD/gD/gAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAcAA+AB+AB+AA+AAcAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA4AA8AA+AA+AAeAAeAAeAA8AH8AHwAHgAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAcAA+AA+AA+AAcAAAAAAAAAAAAAAAAAcAA+AA+AA+AAcAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAB4ABwADwADgAHgAHAAPAAPAAOAAeAAcAA8AA8AB4AB4ABwADwADgAHgAHgAPAAPAAOAAeAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAcAAcAAcAAcAAcAAcAf/4f/4f/4AcAAcAAcAAcAAcAAcAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";
        static byte[] fontBits;

        static void Fill(Color32[] cv, int cw, int ch, int x, int yTop, int w, int h, Color32 c)
        {
            for (int j = 0; j < h; j++)
            {
                int y = ch - 1 - (yTop + j); if (y < 0 || y >= ch) continue;
                for (int i = 0; i < w; i++) { int xx = x + i; if (xx >= 0 && xx < cw) cv[y * cw + xx] = c; }
            }
        }

        static void Text(Color32[] cv, int cw, int ch, int x, int yTop, string s, int scale, Color32 c)
        {
            if (fontBits == null) fontBits = Convert.FromBase64String(FontB64);
            foreach (char raw in s.ToUpperInvariant())
            {
                int g = Glyphs.IndexOf(raw == '_' ? ' ' : raw);
                if (g > 0)
                    for (int j = 0; j < GH; j++)
                        for (int i = 0; i < GW; i++)
                        {
                            int bit = (g * GH + j) * GW + i;
                            if ((fontBits[bit >> 3] & (0x80 >> (bit & 7))) != 0) Fill(cv, cw, ch, x + i * scale, yTop + j * scale, scale, scale, c);
                        }
                x += GW * scale;
            }
        }

        static string Sheet(HashSet<int> pick)
        {
            const int Cols = 4, TW = 960, TH = 600, Bar = 48, Head = 64;
            var have = new List<Pose>(); var files = new List<string>();
            foreach (var q in Active)
            {
                var f = Directory.GetFiles(OutDir, Pre + q.n.ToString("00") + "_*.png");
                if (f.Length > 0) { have.Add(q); files.Add(f[0]); }
            }
            if (have.Count == 0) return "sheet: no candidate in the folder";
            int rows = (have.Count + Cols - 1) / Cols, cw = Cols * TW, ch = Head + rows * (TH + Bar);
            var cv = new Color32[cw * ch];
            Fill(cv, cw, ch, 0, 0, cw, ch, new Color32(10, 10, 12, 255));
            Text(cv, cw, ch, 16, 14, gentle ? "L17 CAR PARK CARD - SAME VIEW, GENTLE VARIATIONS - REAL GAME FRAMES 2560 X 1600 - PICK BY NUMBER" : "L17 CAR PARK CARD CANDIDATES - REAL GAME FRAMES 2560 X 1600 - PICK BY NUMBER", 1, new Color32(255, 200, 80, 255));
            for (int k = 0; k < have.Count; k++)
            {
                var px = Load(files[k], out int w, out int h);
                var th = Resize(px, w, h, TW, TH);
                int x0 = (k % Cols) * TW, y0 = Head + (k / Cols) * (TH + Bar);
                for (int j = 0; j < TH; j++) Array.Copy(th, j * TW, cv, (ch - 1 - (y0 + Bar + (TH - 1 - j))) * cw + x0, TW);
                Fill(cv, cw, ch, x0, y0, TW, Bar, new Color32(24, 24, 28, 255));
                Fill(cv, cw, ch, x0 + 6, y0 + 4, GW * 2 + 20, Bar - 8, new Color32(255, 200, 80, 255));
                Text(cv, cw, ch, x0 + 16 + (have[k].n < 10 ? GW / 2 : 0), y0 + (Bar - GH) / 2, have[k].n.ToString(), 1, new Color32(0, 0, 0, 255));
                Text(cv, cw, ch, x0 + GW * 2 + 40, y0 + (Bar - GH) / 2, have[k].name + (have[k].storm ? " - STORM" : have[k].tod < 0.5f ? " - DUSK" : ""), 1, new Color32(235, 235, 235, 255));
            }
            string path = OutDir + (gentle ? "/L17_card_gentle_sheet.jpg" : "/L17_card_variants_sheet.jpg");
            Save(path, cv, cw, ch, true);
            return $"sheet: {have.Count} candidate(s) numbered -> {path} ({cw} x {ch})";
        }
    }
}
#endif
