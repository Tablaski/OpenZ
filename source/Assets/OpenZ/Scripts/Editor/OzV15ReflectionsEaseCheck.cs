// Menu OpenZ/1.5/Checks/Reflections Ease/1 ... : enter Play (the MainMenu scene, or level 18 already playing). It deploys level 18 as the
// player does (briefings off for that start; the pref is put back), waits for play, holds the sim (time scale 0), sets the day (time of day
// 0.08), hides the HUD, the screen canvases, the rings, the zone wash and the flag rings, and then:
//   A  height sweep 35 -> 200 -> 35 m (7.5 % steps, plus 50 / 70 / 100 / 140) at the owner's spot (201, -44: the forts north of the north
//      canal, the leaning container pair N3 / N4 on the canal under the screen's middle, his pictures of 15 Nov) and at the centre bridge
//      (126, -120);
//   B  a pan across the r6 kit's switch line: the focus from z -34 to z -68 in 1 m steps at x 201, at 45 m and at 70 m;
//   C  the fade itself at the owner's spot, 45 m: the picture sent to the sea skirt and back (PickR6 on / off), every frame, with the
//      kit's fade and with the r6 switch at once;
//   D  the cost at the widest zoom (200 m), both spots: draw calls, SetPass calls and batches over 40 frames, r6 choice against the kit's.
// A and B run BEFORE (OzWaterMirror.PickR6 on + FadeSeconds 0 = the r6 kit exactly) and AFTER (the kit). Each step takes three pictures of
// the same frame, the game camera drawn into a texture of the game view's size: ON, the object reflection OFF (OzWaterPlayCalm.ObjReflect 0)
// and NO WATER (the surfaces of the two water shaders hidden). The water = where ON and NO WATER differ; R = the mean change the object
// reflection makes on the water (ON against OFF, grey levels 0..255). Per sweep: the largest step-to-step change of R against the sweep's
// median step (a switch = one step far over the others; the BEFORE pan shows the r6 switch at the line, z about -51.5).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    public static class OzV15ReflectionsEaseCheck
    {
        const string Tag = "[OzV15ReflectionsEase]";
        const string Menu = "OpenZ/1.5/Checks/Reflections Ease/1 L18 sweeps: height 35-200-35, pan across the old line, the fade, the cost (MainMenu or L18, Play)";
        static readonly Vector3 Owner = new Vector3(201f, 0f, -44f), Bridge = new Vector3(126f, 0f, -120f);
        static readonly float[] Keys = { 35f, 50f, 70f, 100f, 140f, 200f };
        const float RiverY = -0.8f;

        static IEnumerator<double> run; static double waitUntil; static string dir;
        static StringBuilder log, csv, fcsv;
        static readonly List<Action> putBack = new List<Action>();
        static readonly List<string> verdicts = new List<string>();
        static readonly List<Renderer> waters = new List<Renderer>();
        static RtsCamera rc; static Camera cam; static RenderTexture rt; static Texture2D tex;
        static Color32[] pOn, pOff, pDry;
        static bool failed; static int afterRows, afterBad, snapsAfter;

        static string F(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);

        [MenuItem(Menu, false, 7701)]
        static void Begin()
        {
            if (!Application.isPlaying) { Debug.LogWarning(Tag + " enter Play first (the MainMenu scene, or level 18 playing)"); return; }
            dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../../work/staging/v1_5/kits/reflections_ease_r7/proof/check_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"))).Replace('\\', '/');
            Directory.CreateDirectory(dir);
            log = new StringBuilder();
            csv = new StringBuilder("mode,sweep,dir,step,height_m,focus_x,focus_z,cam_y,plane_y,share,live,pictures,water_share,R,R_cover\n");
            fcsv = new StringBuilder("case,frame,t_s,plane_y,share,R\n");
            putBack.Clear(); verdicts.Clear(); waters.Clear(); failed = false; afterRows = afterBad = snapsAfter = 0;
            run = Run(); waitUntil = 0; EditorApplication.update -= Tick; EditorApplication.update += Tick;
            Log("started: " + dir);
        }

        [MenuItem(Menu, true)]
        static bool Can() => Application.isPlaying && run == null;

        static void Tick()
        {
            if (run == null) { EditorApplication.update -= Tick; return; }
            if (!Application.isPlaying) { Log("Play left: stopped"); failed = true; Done(); return; }
            if (EditorApplication.timeSinceStartup < waitUntil) return;
            bool more;
            try { more = run.MoveNext(); } catch (Exception e) { Log("EXCEPTION " + e); failed = true; more = false; }
            if (!more) { Done(); return; }
            waitUntil = EditorApplication.timeSinceStartup + run.Current;
        }

        static void Kill(UnityEngine.Object o) { if (!o) return; if (Application.isPlaying) UnityEngine.Object.Destroy(o); else UnityEngine.Object.DestroyImmediate(o); }

        static void Done()
        {
            EditorApplication.update -= Tick; run = null;
            foreach (var r in waters) if (r) r.enabled = true;
            for (int i = putBack.Count - 1; i >= 0; i--) { try { putBack[i](); } catch (Exception e) { Log("put back failed: " + e.Message); } }
            putBack.Clear();
            if (rt) { rt.Release(); Kill(rt); rt = null; }
            Kill(tex); tex = null; pOn = pOff = pDry = null;
            bool pass = !failed && afterRows > 0 && afterBad == 0 && snapsAfter == 0;
            var rep = new StringBuilder();
            rep.AppendLine("# reflections_ease_r7 Play check (" + DateTime.Now.ToString("dd MMM yyyy HH:mm", CultureInfo.InvariantCulture) + ")");
            rep.AppendLine();
            rep.AppendLine("## Verdicts");
            foreach (var v in verdicts) rep.AppendLine("- " + v);
            rep.AppendLine();
            rep.AppendLine($"AFTER rows {afterRows}: the picture made for the river (y {F(RiverY)}), its share 1 and live: {afterRows - afterBad} of {afterRows}; AFTER sweeps with a switch: {snapsAfter}");
            rep.AppendLine("CHECK " + (pass ? "PASS" : "FAIL") + " (the frames are judged by eye as well: README Unity step 4)");
            rep.AppendLine();
            rep.AppendLine("## Log");
            rep.Append(log);
            try
            {
                File.WriteAllText(Path.Combine(dir, "report.md"), rep.ToString());
                File.WriteAllText(Path.Combine(dir, "steps.csv"), csv.ToString());
                File.WriteAllText(Path.Combine(dir, "fade.csv"), fcsv.ToString());
            }
            catch (Exception e) { Debug.LogError(Tag + " could not write the report: " + e.Message); }
            Debug.Log(Tag + " DONE: CHECK " + (pass ? "PASS" : "FAIL") + " -> " + dir + "/report.md");
        }

        static void Log(string s) { log?.AppendLine(DateTime.Now.ToString("HH:mm:ss") + " " + s); Debug.Log(Tag + " " + s); }
        static void Fail(string s) { failed = true; Log("FAIL: " + s); }

        static float Median(List<float> l)
        {
            if (l.Count == 0) return 0f;
            var c = new List<float>(l); c.Sort(); int n = c.Count;
            return n % 2 == 1 ? c[n / 2] : 0.5f * (c[n / 2 - 1] + c[n / 2]);
        }

        static List<float> Heights()
        {
            var l = new List<float>();
            for (int i = 0; i <= 24; i++) l.Add(Mathf.Round(35f * Mathf.Pow(200f / 35f, i / 24f) * 10f) / 10f);
            foreach (var k in Keys) if (!l.Exists(x => Mathf.Abs(x - k) < 0.05f)) l.Add(k);
            l.Sort(); return l;
        }

        static bool IsKey(float h) { foreach (var k in Keys) if (Mathf.Abs(h - k) < 0.05f) return true; return false; }

        static void Hold(Vector3 f, float h) { if (!rc) return; rc.focus = f; rc.SetHeightImmediate(h); }

        static void SetMode(string mode, float fadeKit)
        {
            bool before = mode == "BEFORE";
            OzWaterMirror.PickR6 = before;
            OzWaterMirror.FadeSeconds = before ? 0f : fadeKit;
        }

        /// <summary>The game camera drawn once into the check's texture; its pixels into px (and a JPG when save is given).</summary>
        static void Grab(ref Color32[] px, string save)
        {
            var was = cam.targetTexture; cam.targetTexture = rt; cam.Render(); cam.targetTexture = was;
            var act = RenderTexture.active; RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0, false); tex.Apply(false);
            RenderTexture.active = act;
            if (px == null || px.Length != rt.width * rt.height) px = new Color32[rt.width * rt.height];
            tex.GetPixelData<Color32>(0).CopyTo(px);
            if (save != null) File.WriteAllBytes(Path.Combine(dir, save + ".jpg"), tex.EncodeToJPG(92));
        }

        /// <summary>ON and OFF (the object reflection off for one picture, inside the same frame).</summary>
        static void GrabOnOff(string save)
        {
            Grab(ref pOn, save);
            float k = OzWaterPlayCalm.ObjReflect; string lv = ZGame.I ? ZGame.I.level : "L18";
            OzWaterPlayCalm.ObjReflect = 0f; OzWaterPlayCalm.Apply(lv);
            try { Grab(ref pOff, null); }
            finally { OzWaterPlayCalm.ObjReflect = k; OzWaterPlayCalm.Apply(lv); }
        }

        static void GrabDry()
        {
            foreach (var r in waters) if (r) r.enabled = false;
            try { Grab(ref pDry, null); }
            finally { foreach (var r in waters) if (r) r.enabled = true; }
        }

        /// <summary>The water share of the picture, R (the mean change the object reflection makes on the water, grey levels) and the share of
        /// the water it changes by more than 6 grey levels. Every 2nd pixel in x and y.</summary>
        static void Score(out float waterShare, out float R, out float cover)
        {
            int w = rt.width, h = rt.height; long n = 0, nw = 0, nr = 0; double sum = 0;
            for (int y = 0; y < h; y += 2)
                for (int x = 0; x < w; x += 2)
                {
                    int i = y * w + x; n++;
                    Color32 a = pOn[i], b = pOff[i], c = pDry[i];
                    int dw = Mathf.Abs(a.r - c.r) + Mathf.Abs(a.g - c.g) + Mathf.Abs(a.b - c.b);
                    if (dw <= 24) continue;
                    nw++;
                    int dr = Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b);
                    sum += dr / 3.0; if (dr > 18) nr++;
                }
            waterShare = n > 0 ? (float)nw / n : 0f; R = nw > 0 ? (float)(sum / nw) : 0f; cover = nw > 0 ? (float)nr / nw : 0f;
        }

        /// <summary>One step of a sweep: the three pictures, the numbers, one CSV row. Returns R.</summary>
        static float Measure(string mode, string sweep, string dirn, int k, string save)
        {
            int p0 = OzWaterMirror.Pictures;
            GrabOnOff(save);
            bool live = OzWaterMirror.Live; float py = OzWaterMirror.ShownY, sh = OzWaterMirror.Share;
            GrabDry();
            Score(out float ws, out float R, out float cover);
            var t = cam.transform.position;
            csv.AppendLine(string.Join(",", mode, sweep, dirn, k.ToString(CultureInfo.InvariantCulture), F(rc.height), F(rc.focus.x), F(rc.focus.z), F(t.y),
                live ? F(py) : "none", F(sh), live ? "1" : "0", (OzWaterMirror.Pictures - p0).ToString(CultureInfo.InvariantCulture), F(ws), F(R), F(cover)));
            if (mode == "AFTER")
            {
                afterRows++;
                bool ok = live && Mathf.Abs(py - RiverY) < 0.06f && sh >= 0.999f;
                if (!ok) { afterBad++; if (afterBad <= 12) Log($"AFTER {sweep} step {k} at {F(rc.height)} m, focus z {F(rc.focus.z)}: live {live}, plane {F(py)}, share {F(sh)} (wanted: live, {F(RiverY)}, 1)"); }
            }
            return R;
        }

        /// <summary>A sweep's verdict: its largest step-to-step change of R against its median step.</summary>
        static void Judge(string mode, string sweep, List<float> R, List<float> zs)
        {
            float mx = 0f; int at = -1; var d = new List<float>();
            for (int i = 1; i < R.Count; i++) { float x = Mathf.Abs(R[i] - R[i - 1]); d.Add(x); if (x > mx) { mx = x; at = i; } }
            float med = Median(d), medR = Median(R), floor = Mathf.Max(med, 0.05f);
            bool snap = mx > 4f * floor && mx > 0.1f * Mathf.Max(medR, 0.5f);
            string where = at < 1 ? "-" : zs != null ? $"between focus z {F(zs[at - 1])} and {F(zs[at])}" : $"between steps {at - 1} and {at}";
            string line = $"{mode} {sweep}: R median {medR:0.00} grey levels; largest step-to-step change {mx:0.00} {where} = {mx / floor:0.0} x the median step ({med:0.000}): {(snap ? "SWITCH" : "smooth")}";
            if (mode == "AFTER" && snap) snapsAfter++;
            if (mode == "BEFORE" && zs != null) line += snap && at >= 1 && zs[at] <= -50f && zs[at - 1] >= -53f ? " (the r6 switch at the line: the measure sees it)" : " (the r6 switch NOT seen at the line: look at the frames)";
            verdicts.Add(line); Log(line);
        }

        static IEnumerator<double> Run()
        {
            // ---------------------------------------------------------------- level 18 in play
            if (!ZGame.I || ZGame.I.World == null || ZGame.I.level != "L18")
            {
                if (!UnityEngine.Object.FindFirstObjectByType<OpenZMainMenu>())
                {
                    OzScenes.MainMenu(); double tm = EditorApplication.timeSinceStartup;
                    while (!UnityEngine.Object.FindFirstObjectByType<OpenZMainMenu>() && EditorApplication.timeSinceStartup - tm < 40) yield return 0.2;
                    yield return 2.0;
                }
                var m = UnityEngine.Object.FindFirstObjectByType<OpenZMainMenu>();
                if (!m) { Fail("no main menu, and level 18 is not playing"); yield break; }
                int prefWas = PlayerPrefs.GetInt(ZodBriefing.PrefBriefing, 1);
                PlayerPrefs.SetInt(ZodBriefing.PrefBriefing, 0); PlayerPrefs.Save();
                putBack.Add(() => { PlayerPrefs.SetInt(ZodBriefing.PrefBriefing, prefWas); PlayerPrefs.Save(); });
                int card = Array.FindIndex(OzLevelDef.All, x => x.Id == "L18");
                yield return 0.8; m.ProofSelect(); yield return 0.8; m.ProofFocus(card); yield return 0.6; m.ProofDeploy();
                double t0 = EditorApplication.timeSinceStartup;
                while ((!ZGame.I || ZGame.I.World == null || ZGame.I.level != "L18") && EditorApplication.timeSinceStartup - t0 < 150) yield return 0.25;
                if (!ZGame.I || ZGame.I.World == null || ZGame.I.level != "L18") { Fail("level 18 did not load in 150 s"); yield break; }
                Log("level 18 deployed from the menu (briefings off for this start; the pref is put back at the end)");
            }
            double tw = EditorApplication.timeSinceStartup;
            while (Shader.GetGlobalVector("_OZWaterObjRefl").x <= 0f && EditorApplication.timeSinceStartup - tw < 180) yield return 0.5;   // in play: no briefing, no flyover
            if (Shader.GetGlobalVector("_OZWaterObjRefl").x <= 0f) { Fail("the object reflection never came on (a briefing or flyover still running, or GRAPHICS LOW)"); yield break; }
            yield return 2.0;

            // ---------------------------------------------------------------- set up (all put back in Done)
            rc = UnityEngine.Object.FindFirstObjectByType<RtsCamera>(); cam = rc ? rc.GetComponent<Camera>() : null;
            if (!rc || !cam) { Fail("no RtsCamera with a Camera"); yield break; }
            Vector3 focus0 = rc.focus; float h0 = rc.height;
            putBack.Add(() => { if (rc) { rc.focus = focus0; rc.SetHeightImmediate(h0); } });
            float ts0 = Time.timeScale; Time.timeScale = 0f; putBack.Add(() => Time.timeScale = ts0);
            var dn = UnityEngine.Object.FindFirstObjectByType<DayNight>();
            if (dn) { float tod0 = dn.timeOfDay; dn.timeOfDay = 0.08f; putBack.Add(() => { if (dn) dn.timeOfDay = tod0; }); }
            bool pick0 = OzWaterMirror.PickR6; float fade0 = OzWaterMirror.FadeSeconds, obj0 = OzWaterPlayCalm.ObjReflect;
            putBack.Add(() => { OzWaterMirror.PickR6 = pick0; OzWaterMirror.FadeSeconds = fade0; OzWaterPlayCalm.ObjReflect = obj0; if (ZGame.I) OzWaterPlayCalm.Apply(ZGame.I.level); });
            float fadeKit = fade0 > 0f ? fade0 : 0.35f;
            if (OpenZHud.I && OpenZHud.I.hudVisible) { OpenZHud.I.SetVisible(false); putBack.Add(() => { if (OpenZHud.I) OpenZHud.I.SetVisible(true); }); }
            foreach (var c in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (c && c.isRootCanvas && c.enabled && c.renderMode != RenderMode.WorldSpace) { c.enabled = false; var cc = c; putBack.Add(() => { if (cc) cc.enabled = true; }); }
            foreach (var b in UnityEngine.Object.FindObjectsByType<SelectionOverlay>(FindObjectsSortMode.None))
                if (b && b.enabled) { b.enabled = false; var bb = b; putBack.Add(() => { if (bb) bb.enabled = true; }); }
            foreach (var r in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                if (r && (r.gameObject.name == "ZoneWash" || r.gameObject.name == "FlagRing") && r.gameObject.activeSelf) { var g = r.gameObject; g.SetActive(false); putBack.Add(() => { if (g) g.SetActive(true); }); }
            Shader sa = Shader.Find("OpenZ/WaterStylized"), sb = Shader.Find("OpenZ/WaterV4");
            foreach (var r in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                var mt = r ? r.sharedMaterial : null;
                if (mt && mt.shader && (mt.shader == sa || mt.shader == sb) && r.enabled) waters.Add(r);
            }
            int w = Mathf.Max(64, cam.pixelWidth), h = Mathf.Max(64, cam.pixelHeight);
            rt = new RenderTexture(new RenderTextureDescriptor(w, h, RenderTextureFormat.ARGB32, 24) { sRGB = true, msaaSamples = 1 }) { name = "OZ_ReflEaseCheck" };
            rt.Create();
            tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var urp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
            Log($"level {ZGame.I.level}, GRAPHICS {QualitySettings.names[QualitySettings.GetQualityLevel()]} (render pipeline asset {(urp ? urp.name : "none")}), _OZWaterLite {Shader.GetGlobalVector("_OZWaterLite")}, " +
                $"_OZWaterObjRefl {Shader.GetGlobalVector("_OZWaterObjRefl")}; pictures {w} x {h}; camera pitch {F(rc.pitch)}, FOV {F(cam.fieldOfView)}, heights {F(rc.minHeight)}-{F(rc.maxHeight)}");
            Log($"the mirror: pools {OzWaterMirror.Pools}, Strength {F(OzWaterMirror.Strength)}, Bend {F(OzWaterMirror.Bend)}, Scale {OzWaterMirror.Scale}, EveryNth {OzWaterMirror.EveryNth}, " +
                $"ThinAbove {F(OzWaterMirror.ThinAbove)}, FadeSeconds {F(fadeKit)}, NearReach {F(OzWaterMirror.NearReach)}; water surfaces hidden for the NO WATER picture: {waters.Count}");
            if (!OzWaterMirror.On) Log("NOTE: OzWaterMirror.On was false (the fps bench's switch): set true for the check");
            bool on0 = OzWaterMirror.On; OzWaterMirror.On = true; putBack.Add(() => OzWaterMirror.On = on0);

            // ---------------------------------------------------------------- A + B: the sweeps, BEFORE (r6) and AFTER (the kit)
            var hs = Heights();
            foreach (var mode in new[] { "BEFORE", "AFTER" })
            {
                SetMode(mode, fadeKit);
                foreach (var (name, f) in new[] { ("owner_spot", Owner), ("centre_bridge", Bridge) })
                {
                    var seq = new List<(float h, string d)>();
                    foreach (var x in hs) seq.Add((x, "up"));
                    for (int i = hs.Count - 2; i >= 0; i--) seq.Add((hs[i], "down"));
                    var Rs = new List<float>();
                    Hold(f, seq[0].h); yield return 1.0;
                    for (int k = 0; k < seq.Count; k++)
                    {
                        Hold(f, seq[k].h); yield return 0.3; Hold(f, seq[k].h);
                        bool keep = seq[k].d == "up" && IsKey(seq[k].h) && (mode == "AFTER" || name == "owner_spot");
                        Rs.Add(Measure(mode, "height_" + name, seq[k].d, k, keep ? $"{name}_{mode}_h{seq[k].h:000}" : null));
                    }
                    Judge(mode, "height 35-200-35 at the " + name.Replace('_', ' '), Rs, null);
                }
                foreach (float ph in new[] { 45f, 70f })
                {
                    var Rs = new List<float>(); var zs = new List<float>();
                    Hold(new Vector3(Owner.x, 0f, -34f), ph); yield return 1.0;
                    for (int k = 0; k <= 34; k++)
                    {
                        float z = -34f - k; var f = new Vector3(Owner.x, 0f, z);
                        Hold(f, ph); yield return 0.3; Hold(f, ph);
                        Rs.Add(Measure(mode, $"pan_{ph:0}m", "south", k, (k == 14 || k == 21) ? $"pan{ph:0}_{mode}_z{-z:0}" : null));   // z -48 / -55: either side of the r6 line
                        zs.Add(rc.focus.z);
                    }
                    Judge(mode, $"pan across z -51 at {ph:0} m (x {F(Owner.x)})", Rs, zs);
                }
            }

            SetMode("AFTER", fadeKit);
            Hold(Owner, 45f); yield return 1.0; Hold(Owner, 45f);
            Measure("AFTER", "fade_base", "-", 0, null);                                            // the water of this view (NO WATER picture kept)
            foreach (var (cname, fs) in new[] { ("kit_fade", fadeKit), ("r6_at_once", 0f) })
            {
                OzWaterMirror.FadeSeconds = fs;
                foreach (bool toSkirt in new[] { true, false })
                {
                    string cs = cname + (toSkirt ? "_to_skirt" : "_back_to_river");
                    OzWaterMirror.PickR6 = toSkirt;
                    double t0 = EditorApplication.timeSinceStartup; int fr = 0; var Rs = new List<float>();
                    while (EditorApplication.timeSinceStartup - t0 < 1.2)
                    {
                        Hold(Owner, 45f);
                        GrabOnOff(null);
                        float sh = OzWaterMirror.Share, py = OzWaterMirror.ShownY;
                        Score(out _, out float R, out _);
                        Rs.Add(R);
                        fcsv.AppendLine(string.Join(",", cs, fr.ToString(CultureInfo.InvariantCulture), F((float)(EditorApplication.timeSinceStartup - t0)), F(py), F(sh), F(R)));
                        fr++;
                        yield return 0;
                    }
                    float lo = float.MaxValue, hi = float.MinValue, big = 0f;
                    foreach (var r in Rs) { lo = Mathf.Min(lo, r); hi = Mathf.Max(hi, r); }
                    for (int i = 1; i < Rs.Count; i++) big = Mathf.Max(big, Mathf.Abs(Rs[i] - Rs[i - 1]));
                    int moving = 0; for (int i = 1; i < Rs.Count; i++) if (Mathf.Abs(Rs[i] - Rs[i - 1]) > 0.1f * Mathf.Max(hi - lo, 0.01f)) moving++;
                    string line = $"fade {cs}: R from {F(Rs.Count > 0 ? Rs[0] : 0f)} to {F(Rs.Count > 0 ? Rs[Rs.Count - 1] : 0f)} in {fr} frames; largest one-frame change {big:0.00} = {100f * big / Mathf.Max(hi - lo, 0.01f):0} % of the whole change, spread over {moving} frame(s)";
                    verdicts.Add(line); Log(line);
                    OzWaterMirror.PickR6 = false; Hold(Owner, 45f); yield return 1.0;              // settled on the river again
                }
            }

            // ---------------------------------------------------------------- D: the cost at the widest zoom
            foreach (var (name, f) in new[] { ("owner_spot", Owner), ("centre_bridge", Bridge) })
                foreach (var mode in new[] { "BEFORE", "AFTER" })
                {
                    SetMode(mode, fadeKit); Hold(f, 200f); yield return 1.0;
                    var rd = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
                    var rs = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
                    var rb = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
                    yield return 0.2;
                    var a = new List<float>(); var s = new List<float>(); var b = new List<float>(); var ms = new List<float>();
                    int pics0 = OzWaterMirror.Pictures;
                    for (int i = 0; i < 40; i++)
                    {
                        Hold(f, 200f); yield return 0;
                        if (rd.Valid) a.Add(rd.LastValue); if (rs.Valid) s.Add(rs.LastValue); if (rb.Valid) b.Add(rb.LastValue);
                        ms.Add(Time.unscaledDeltaTime * 1000f);
                    }
                    int pics = OzWaterMirror.Pictures - pics0;
                    rd.Dispose(); rs.Dispose(); rb.Dispose();
                    string line = $"cost at 200 m, {name.Replace('_', ' ')}, {mode}: draw calls {(a.Count > 0 ? F(Median(a)) : "n/a")}, SetPass {(s.Count > 0 ? F(Median(s)) : "n/a")}, batches {(b.Count > 0 ? F(Median(b)) : "n/a")} (medians of 40 frames), " +
                                  $"frame ms {Median(ms):0.0} (editor: a smoke test only), mirror pictures in 40 frames {pics}, picture for y {F(OzWaterMirror.ShownY)}";
                    verdicts.Add(line); Log(line);
                }
            SetMode("AFTER", fadeKit);
            Log("done; putting everything back");
        }
    }
}
