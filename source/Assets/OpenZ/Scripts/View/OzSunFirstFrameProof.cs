#if UNITY_EDITOR
// OpenZ (GPL-3.0)
// menu OpenZ/V1.2/Sun First Frame (Editor/OzSunFirstFrameMenu.cs). Started on the MainMenu scene in Play:
//  Red   level 16 deployed as red as the player does, twice: first the new way (the panorama preloaded with the level, the sky full from
//        the flyover's first frame), then the earlier way for the measure (FlybySky.SunFirstFrame off for that one start: the load when
//        the flight begins and the 1 s fade-in); back to the main menu after each briefing has handed over.
//  Blue  a level (16 unless another is asked) deployed as blue. Its start flyover is watched when one begins by itself (the blue
//        briefing); when none begins within BlueWait s, the proof flies the level's shot list itself from the blue start view (camera
//        only, as the blue flyover kit's runner does) and watches that flight.
// For each watched flight: pictures of the first frame and of the frames at 0.1 and 0.5 s (the earlier way also 1.0 and 1.5 s), each with
// the sky's weight, the drawn sun's place (inside the picture between the briefing bars) and the brightness of the pixels at the disc;
// the panorama's load (preloaded or not, the wait on the first frame, its size), the level load to the first flyover frame on screen,
// and the panorama released after the flyover. Counted PASS / FAIL lines; the report and the pictures in
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OpenZ
{
    [DefaultExecutionOrder(6000)]                                                                       // LateUpdate after FlybySky (5000): the frame as it is drawn
    public sealed class OzSunFirstFrameProof : MonoBehaviour
    {
        public enum Job { Red, Blue }
        public static OzSunFirstFrameProof I { get; private set; }
        public static string Report = "idle";
        /// <summary>The thresholds: the sky weight that counts as full, the disc's brightest pixel (luma 0..1), the most the disc may differ
        /// from the first frame at 0.1 / 0.5 s, the longest wait for the panorama on the first frame (ms; preloaded / loaded on that frame),
        /// the release after the flight's end (s), the wait for a blue briefing's own flight (s), the proof's own flight (s).</summary>
        public static float FullWeight = 0.999f, DiscLuma = 0.85f, DiscSame = 0.08f, MaxWaitMs = 150f, MaxWaitNoPreloadMs = 400f, ReleaseS = 6f,
            BlueWait = 4f, OwnFlightS = 2.2f;
        /// <summary>The briefing letterbox's bars (of 1080 at the top / bottom): the picture between them.</summary>
        public static float BarBottom = 112f / 1080f, BarTop = 64f / 1080f;

        sealed class Grabbed
        {
            public string name; public float t, weight, lumaMax = -1f, lumaMean = -1f, shownAt = -1f; public bool inPic, resident, captured;
            public Vector2 vp; public Texture2D pic;
        }

        sealed class Watch
        {
            public string tag; public float[] at; public bool blue, own, newWay;
            public ZodFlyover fly; public float t0 = -1f; public int next, plainFrames, levelFrame = -1, preloadsBefore;
            public readonly List<Grabbed> frames = new List<Grabbed>();
            public readonly List<float> dts = new List<float>();
            public float fullAt = -1f, endAt = -1f, releasedAt = -1f, levelToFirstMs = -1f, waitMs = -1f, readyMs = -1f, mb, frame0Ms = -1f;
            public bool preloaded, closed, seenResident; public string preloadLine = "", firstLine = "", unloadLine = "";
        }

        Job job; string lv; OzLevelDef def; string dir;
        readonly StringBuilder sb = new StringBuilder(); int passes, fails; string numbers = "";
        bool boxWas, boxChanged; int briefWas = int.MinValue, cameoWas = int.MinValue;
        Watch watch; ZodFlyover own; ZGame ownGame; OpenZHud ownHud;

        public static string Folder()
        {
            var d = new DirectoryInfo(Application.dataPath);
            for (int i = 0; i < 8 && d != null; i++, d = d.Parent)
                if (Directory.Exists(Path.Combine(d.FullName, "work", "records")))
                    return Path.Combine(d.FullName, "work", "records", "milestones", "sun_first_frame_k78");
            return Path.Combine(Application.persistentDataPath, "sun_first_frame_k78");
        }

        public static void Run(Job job, string level)
        {
            if (I) { Debug.LogWarning("[OzSunFirstFrameProof] already running (" + I.job + " " + I.lv + ")"); return; }
            Report = "running"; var go = new GameObject("OzSunFirstFrameProof"); DontDestroyOnLoad(go);
            var p = go.AddComponent<OzSunFirstFrameProof>(); p.job = job; p.lv = level;
        }

        void Awake() { I = this; }
        void Start()
        {
            def = Array.Find(OzLevelDef.All, d => d.Id == lv); dir = Folder(); Directory.CreateDirectory(dir);
            if (!FlybySky.SunFirstFrame) { Log("FlybySky.SunFirstFrame was off: switched on (its default) for the proof"); FlybySky.SunFirstFrame = true; }
            StartCoroutine(All());
        }
        void OnDestroy() { if (I == this) I = null; Restore(); }

        void Log(string t) { sb.AppendLine(t); Debug.Log("[OzSunFirstFrameProof] " + t); }
        void Check(string what, bool ok) { if (ok) passes++; else fails++; Log((ok ? "PASS " : "FAIL ") + what); }
        static IEnumerator Real(float s) { float t = Time.realtimeSinceStartup + s; while (Time.realtimeSinceStartup < t) yield return null; }

        // ================================================================================================================ the whole run
        IEnumerator All()
        {
            if (def == null) { Check("level " + lv + " is a level of the game", false); Finish(); yield break; }
            if (ZGame.I || !FindFirstObjectByType<OpenZMainMenu>()) { Check("started on the MAIN MENU scene in Play (the level's first frames are the measure)", false); Finish(); yield break; }
            Prefs();
            if (job == Job.Red)
            {
                ArmSide(false);
                FlybySky.SunFirstFrame = true;
                var b = new Watch { tag = "red_new", at = new[] { 0f, 0.1f, 0.5f } };
                yield return Deploy(b);
                yield return Menu();
                FlybySky.SunFirstFrame = false;                                                         // the earlier way, for this one start only
                var a = new Watch { tag = "red_before", at = new[] { 0f, 0.1f, 0.5f, 1.0f, 1.5f } };
                yield return Deploy(a);
                FlybySky.SunFirstFrame = true;
                yield return Menu();
                CheckNew(b, "RED " + lv, true);
                CheckBefore(a, b);
            }
            else
            {
                ArmSide(true);
                var u = new Watch { tag = "blue_" + lv, blue = true, at = new[] { 0f, 0.1f, 0.5f } };
                yield return Deploy(u);
                yield return Menu();
                CheckNew(u, "BLUE " + lv, false);
            }
            Finish();
        }

        void Prefs()
        {
            int b = PlayerPrefs.GetInt(ZodBriefing.PrefBriefing, 1), c = PlayerPrefs.GetInt(ZodBriefing.PrefCameo, 1);
            if (b <= 0) { briefWas = b; PlayerPrefs.SetInt(ZodBriefing.PrefBriefing, 1); Log("the level-start briefing was switched off in the prefs: on for the proof (put back at the end)"); }
            if (c != 1) { cameoWas = c; PlayerPrefs.SetInt(ZodBriefing.PrefCameo, 1); Log("Commander Zod's cameo was switched off in the prefs: on for the proof (put back at the end)"); }
        }

        /// <summary>The side of the next DEPLOY: the side setting (OzPlayBlue.Box: the box, or the faction screen's pick), put back at the end.</summary>
        void ArmSide(bool blue)
        {
            if (!boxChanged) boxWas = OzPlayBlue.Box;
            if (OzPlayBlue.Box != blue) { OzPlayBlue.Box = blue; boxChanged = true; }
        }

        void Restore()
        {
            FlybySky.SunFirstFrame = true;
            if (boxChanged) { OzPlayBlue.Box = boxWas; boxChanged = false; }
            if (briefWas != int.MinValue) { PlayerPrefs.SetInt(ZodBriefing.PrefBriefing, briefWas); briefWas = int.MinValue; }
            if (cameoWas != int.MinValue) { PlayerPrefs.SetInt(ZodBriefing.PrefCameo, cameoWas); cameoWas = int.MinValue; }
            PlayerPrefs.Save();
            EndOwn();
            if (watch != null) foreach (var f in watch.frames) if (f.pic) Destroy(f.pic);
        }

        // ================================================================================================================ one start
        IEnumerator Deploy(Watch w)
        {
            var m = FindFirstObjectByType<OpenZMainMenu>();
            if (!m) { Check(w.tag + ": the MAIN MENU is up for the DEPLOY", false); yield break; }
            yield return Real(0.8f);
            m.ProofSelect(); yield return Real(0.8f);
            m.ProofFocus(Array.FindIndex(OzLevelDef.All, d => d.Id == lv)); yield return Real(0.6f);
            w.preloadsBefore = FlybySky.Preloads; w.newWay = FlybySky.SunFirstFrame;
            float deployAt = Time.realtimeSinceStartup;
            watch = w;
            m.ProofDeploy();
            Log(w.tag + ": DEPLOY " + lv + " as the player, the side " + (w.blue ? "BLUE" : "RED") + ", the sun from the first frame "
                + (w.newWay ? "ON (the new way)" : "OFF (the earlier way, for the measure)"));
            float t0 = Time.realtimeSinceStartup;
            while ((!ZGame.I || SceneManager.GetActiveScene().name != def.Scene) && Time.realtimeSinceStartup - t0 < 120f) yield return null;
            bool loaded = ZGame.I && SceneManager.GetActiveScene().name == def.Scene;
            Check(w.tag + ": the level loaded (" + SceneManager.GetActiveScene().name + (loaded && FlybySky.LevelLoadedAt > deployAt ? ", " + (FlybySky.LevelLoadedAt - deployAt).ToString("0.0") + " s after the DEPLOY" : "") + ")", loaded);
            if (!loaded) { watch = null; yield break; }
            var g = ZGame.I;
            bool sideOk = w.blue ? OzPlayBlue.Active && g.player == Team.Blue : !OzPlayBlue.Active && g.player == Team.Red;
            Check(w.tag + ": the level runs as " + (w.blue ? "BLUE" : "RED") + " (the player " + g.player + ")", sideOk);

            // the flight: the level's own (the red briefing / the blue briefing) on its first frames; blue without one: the proof's own
            float tl = Time.realtimeSinceStartup;
            while (w.fly == null && (Time.realtimeSinceStartup - tl < (w.blue ? BlueWait : 10f) || (ZodBriefing.BriefingActive && Time.realtimeSinceStartup - tl < 10f))) yield return null;
            if (w.fly == null && w.blue) yield return OwnFlight(w);
            if (w.fly == null)
            {
                Check(w.tag + ": a start flyover took the camera (" + (w.blue ? "no blue briefing flew and the proof's own flight did not begin" : "the red briefing did not fly: see its [ZodCameo] line") + ")", false);
                watch = null; yield break;
            }

            // the first seconds (each picture is taken at the end of its frame)
            float tw = Time.realtimeSinceStartup;
            while ((w.next < w.at.Length || w.frames.Exists(f => !f.captured)) && Time.realtimeSinceStartup - tw < 8f) yield return null;
            var f0 = w.frames[0];
            if (w.newWay)
            {
                w.preloaded = FlybySky.LastFirstFramePreloaded && FlybySky.Preloads > w.preloadsBefore;
                w.waitMs = FlybySky.LastWaitMs; w.readyMs = FlybySky.LastPreloadReadyMs; w.mb = FlybySky.LastPanoramaMB;
                w.preloadLine = FlybySky.Preloads > w.preloadsBefore ? FlybySky.LastPreload : "no preload at this level load (" + FlybySky.LastPreload + ")";
                w.firstLine = FlybySky.LastFirstFrame;
            }
            else
            {
                w.preloaded = false; w.waitMs = 0f; w.readyMs = -1f;
                w.preloadLine = "the earlier way: nothing loads at the level load"; w.firstLine = "the earlier way: the load starts with the flight, the sky fades in";
            }
            if (!w.own && f0.shownAt > 0f && FlybySky.LevelLoadedAt > deployAt) w.levelToFirstMs = (f0.shownAt - FlybySky.LevelLoadedAt) * 1000f;   // the proof's own flight begins seconds later by design
            w.frame0Ms = w.dts.Count > 1 ? w.dts[1] * 1000f : -1f;

            // the end of the flight (the hand-over) and the release of the panorama
            float te = Time.realtimeSinceStartup;
            while ((ZodBriefing.BriefingActive || w.fly.Active) && Time.realtimeSinceStartup - te < 90f) yield return null;
            float tr = Time.realtimeSinceStartup;
            while (w.releasedAt < 0f && Time.realtimeSinceStartup - tr < ReleaseS + 1f) yield return null;
            if (!w.newWay && w.mb <= 0f) w.mb = FlybySky.LastPanoramaMB;
            w.unloadLine = FlybySky.LastUnload;
            w.closed = true; watch = null;
            Save(w);
            Log(w.tag + ": first frame weight " + f0.weight.ToString("0.000") + ", sun in the picture " + f0.inPic + ", disc luma " + L(f0.lumaMax) + "; the sky full at "
                + (w.fullAt >= 0f ? w.fullAt.ToString("0.00") + " s" : "-") + "; " + w.firstLine + "; the level load to the first flyover frame on screen "
                + (w.levelToFirstMs >= 0f ? w.levelToFirstMs.ToString("0") + " ms" : "-") + "; frames +1..+5 " + Dts(w) + "; released " + (w.releasedAt >= 0f ? "at " + w.releasedAt.ToString("0.0") + " s" : "NOT seen")
                + " (the flight ended at " + (w.endAt >= 0f ? w.endAt.ToString("0.0") + " s" : "-") + ")");
        }

        /// <summary>Blue without a briefing that flies by itself: the level's shot list from the blue start view, camera only, OwnFlightS s,
        /// then the skip's hand-back (the simulation and the HUD wait under it, as under a briefing).</summary>
        IEnumerator OwnFlight(Watch w)
        {
            var g = ZGame.I; var rts = FindFirstObjectByType<RtsCamera>();
            if (!g || !rts) { Log(w.tag + ": no RTS camera for the proof's own flight"); yield break; }
            float tp = Time.realtimeSinceStartup;
            while ((!OzPlayBlueLevel.I || !OzPlayBlueLevel.I.Placed) && Time.realtimeSinceStartup - tp < 3f) yield return null;
            ownHud = OpenZHud.I ? OpenZHud.I : FindFirstObjectByType<OpenZHud>();
            while (ownHud && ownHud.Canvas == null && Time.realtimeSinceStartup - tp < 4f) yield return null;
            if (ownHud) { ownHud.SetVisible(false); ownHud.enabled = false; }
            ownGame = g; g.enabled = false;
            w.own = true;
            var fly = new ZodFlyover(rts, g);
            own = fly;
            bool began = fly.Begin("overview");
            Log(w.tag + ": no blue briefing flew within " + BlueWait.ToString("0") + " s: the proof flies the level's shot list itself from the blue start view (camera only) "
                + (began ? (fly.Cine ? "- the shot list, first shot " + fly.Current : "- the orbit flight") : "- it did NOT begin"));
            if (!began) { EndOwn(); yield break; }
            float t = 0f;
            while (t < OwnFlightS) { yield return null; float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f); t += dt; fly.Tick(dt); }
            fly.HandBack(true);
            float h = 0f;
            while (fly.HomeError > 0.04f && h < 1.6f) { yield return null; float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f); h += dt; fly.Tick(dt); }
            Log(w.tag + ": the proof's flight handed back (error " + fly.HomeError.ToString("0.000") + " after " + h.ToString("0.00") + " s)");
            EndOwn();
        }

        void EndOwn()
        {
            if (own != null && own.Active) own.End();
            own = null;
            if (ownGame) ownGame.enabled = true;
            ownGame = null;
            if (ownHud) { ownHud.enabled = true; ownHud.SetVisible(true); }
            ownHud = null;
        }

        IEnumerator Menu()
        {
            OzScenes.MainMenu();
            float t0 = Time.realtimeSinceStartup;
            while ((ZGame.I || !FindFirstObjectByType<OpenZMainMenu>()) && Time.realtimeSinceStartup - t0 < 60f) yield return null;
            Log("back on the main menu (" + SceneManager.GetActiveScene().name + ")");
            yield return Real(1.5f);
        }

        // ================================================================================================================ every frame
        void LateUpdate()
        {
            var w = watch; if (w == null || w.closed || def == null) return;
            var fly = ZodFlyover.Live; bool live = fly != null && fly.Active;
            bool inLevel = ZGame.I && SceneManager.GetActiveScene().name == def.Scene;
            float now = Time.realtimeSinceStartup;
            if (w.fly == null)
            {
                if (live && inLevel && fly.SkyWanted)
                {   // the flight's first frame (SkyWanted: a flight of this level; one left over from a level unloaded mid-flight has no camera)
                    w.fly = fly; w.t0 = now; w.levelFrame = FlybySky.LevelLoadedFrame >= 0 ? Time.frameCount - FlybySky.LevelLoadedFrame : -1;
                    w.dts.Add(Time.unscaledDeltaTime);
                    if (FlybySky.I && FlybySky.I.Weight >= FullWeight) w.fullAt = 0f;
                    Grab(w, "frame0", 0f); w.next = 1;
                }
                else if (inLevel) w.plainFrames++;
                return;
            }
            float t = now - w.t0;
            if (w.dts.Count < 6) w.dts.Add(Time.unscaledDeltaTime);
            if (w.fullAt < 0f && FlybySky.I && FlybySky.I.Weight >= FullWeight) w.fullAt = t;
            if (w.next < w.at.Length && t >= w.at[w.next]) { Grab(w, "t" + Mathf.RoundToInt(w.at[w.next] * 100f).ToString("000"), t); w.next++; }
            if (w.endAt < 0f && (!live || fly != w.fly)) w.endAt = t;
            bool resident = FlybySky.I && FlybySky.I.Panorama;
            if (resident) w.seenResident = true;
            else if (w.seenResident && w.releasedAt < 0f) w.releasedAt = t;                             // released after it was there (the earlier way loads it a few frames in)
        }

        void Grab(Watch w, string name, float t)
        {
            var f = new Grabbed { name = name, t = t, weight = FlybySky.I ? FlybySky.I.Weight : 0f, resident = FlybySky.I && FlybySky.I.Panorama };
            var cam = Camera.main;
            if (cam)
            {
                var sun = RenderSettings.sun;
                var vs = FlybySky.VisualSun(sun ? -sun.transform.forward : new Vector3(0.94f, 0.16f, -0.3f).normalized);
                var v = cam.WorldToViewportPoint(cam.transform.position + vs * 1000f);
                f.vp = new Vector2(v.x, v.y);
                f.inPic = v.z > 0f && v.x > 0.03f && v.x < 0.97f && v.y > BarBottom + 0.01f && v.y < 1f - BarTop - 0.01f;
            }
            w.frames.Add(f);
            StartCoroutine(Capture(f));
        }

        IEnumerator Capture(Grabbed f)
        {
            yield return new WaitForEndOfFrame();
            f.shownAt = Time.realtimeSinceStartup;
            Texture2D tex = null;
            try { tex = ScreenCapture.CaptureScreenshotAsTexture(); }
            catch (Exception e) { Log("picture " + f.name + " failed: " + e.Message); }
            if (tex) { f.pic = tex; if (f.inPic) Probe(tex, f); }
            f.captured = true;
        }

        /// <summary>The pixels at the drawn sun (a 9 x 9 block at 1080 lines): the brightest and the mean luma (0..1).</summary>
        static void Probe(Texture2D tex, Grabbed f)
        {
            int r = Mathf.Max(2, Mathf.RoundToInt(tex.height / 270f));
            int cx = Mathf.RoundToInt(f.vp.x * tex.width), cy = Mathf.RoundToInt(f.vp.y * tex.height);
            int x0 = Mathf.Clamp(cx - r, 0, tex.width - 1), y0 = Mathf.Clamp(cy - r, 0, tex.height - 1);
            int x1 = Mathf.Clamp(cx + r, 0, tex.width - 1), y1 = Mathf.Clamp(cy + r, 0, tex.height - 1);
            var px = tex.GetPixels(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
            float mx = 0f, sum = 0f;
            foreach (var c in px) { float l = 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b; if (l > mx) mx = l; sum += l; }
            f.lumaMax = mx; f.lumaMean = px.Length > 0 ? sum / px.Length : 0f;
        }

        void Save(Watch w)
        {
            foreach (var f in w.frames)
            {
                if (!f.pic) continue;
                string path = Path.Combine(dir, w.tag + "_" + f.name + ".png");
                try { File.WriteAllBytes(path, f.pic.EncodeToPNG()); Log("picture " + path + " (at " + f.t.ToString("0.00") + " s, sky weight " + f.weight.ToString("0.000") + ", disc luma " + L(f.lumaMax) + ")"); }
                catch (Exception e) { Log("picture " + path + " not written: " + e.Message); }
                Destroy(f.pic); f.pic = null;
            }
        }

        // ================================================================================================================ the checks
        void CheckNew(Watch w, string side, bool red)
        {
            if (w.fly == null || w.frames.Count == 0) { Check(side + ": the start flyover was watched", false); return; }
            var f0 = w.frames[0]; var f1 = Find(w, "t010"); var f5 = Find(w, "t050");
            Check(side + ": a start flyover took the camera (" + (w.own ? "the proof's own flight from the blue start view: no blue briefing flew by itself" : "the level's own briefing") + ")", true);
            if (red) Check(side + ": no plain frame of the level before the flyover's first frame (" + w.plainFrames + "; the flyover's first frame is frame +" + w.levelFrame + " after the level load)", w.plainFrames == 0);
            else Log(side + ": " + w.plainFrames + " plain frame(s) of the level before the flyover's first frame (frame +" + w.levelFrame + " after the level load)");
            if (red) Check(side + ": the level load preloaded the panorama (" + w.preloadLine + ")", w.preloaded);
            else Log(side + ": " + w.preloadLine + (w.preloaded ? " - taken by the flight" : " - so the flight's first frame loaded it"));
            Check(side + ": first frame: the painted sky full at once (weight " + f0.weight.ToString("0.000") + ", at least " + FullWeight.ToString("0.000") + "): " + w.firstLine, f0.weight >= FullWeight);
            Check(side + ": first frame: the panorama was resident to draw it", f0.resident);
            float lim = w.preloaded ? MaxWaitMs : MaxWaitNoPreloadMs;
            Check(side + ": the main thread's wait for the panorama on the first frame: " + w.waitMs.ToString("0") + " ms (at most " + lim.ToString("0") + " ms, "
                  + (w.preloaded ? "preloaded with the level" : "loaded on that frame") + ")", w.waitMs >= 0f && w.waitMs <= lim);
            Check(side + ": first frame: the drawn sun inside the picture (viewport x " + f0.vp.x.ToString("0.00") + ", y " + f0.vp.y.ToString("0.00") + ", between the briefing bars)", f0.inPic);
            Check(side + ": first frame: the sun disc drawn (its brightest pixel luma " + L(f0.lumaMax) + ", at least " + DiscLuma.ToString("0.00") + "; the block's mean " + L(f0.lumaMean) + ")", f0.inPic && f0.lumaMax >= DiscLuma);
            bool same = f1 != null && f5 != null && f1.weight >= FullWeight && f5.weight >= FullWeight && f1.inPic && f5.inPic
                        && Mathf.Abs(f1.lumaMax - f0.lumaMax) <= DiscSame && Mathf.Abs(f5.lumaMax - f0.lumaMax) <= DiscSame;
            Check(side + ": at 0.1 s (" + T(f1) + ") and 0.5 s (" + T(f5) + ") the sky stays full (weights " + W(f1) + " / " + W(f5) + ") and the disc as bright as on the first frame (luma "
                  + L(f0.lumaMax) + " / " + L(f1) + " / " + L(f5) + ", within " + DiscSame.ToString("0.00") + ")", same);
            Check(side + ": after the flyover the panorama was released (" + (w.releasedAt >= 0f ? "at " + w.releasedAt.ToString("0.0") + " s of the flight" : "NOT released")
                  + ", the flight ended at " + (w.endAt >= 0f ? w.endAt.ToString("0.0") + " s" : "-") + "; " + w.unloadLine + ")",
                  w.releasedAt >= 0f && (w.endAt < 0f || w.releasedAt <= w.endAt + ReleaseS));
            numbers += side + ": first frame wait " + w.waitMs.ToString("0") + " ms (" + (w.preloaded ? "preloaded, resident " + w.readyMs.ToString("0") + " ms after the level load" : "no preload") + "), panorama "
                       + w.mb.ToString("0.0") + " MB, the level load to the first flyover frame on screen " + (w.levelToFirstMs >= 0f ? w.levelToFirstMs.ToString("0") + " ms" : "-") + ", the first frame took "
                       + (w.frame0Ms >= 0f ? w.frame0Ms.ToString("0") + " ms" : "-") + ", released " + (w.releasedAt >= 0f ? w.releasedAt.ToString("0.0") + " s after the first frame" : "-") + "\n";
        }

        void CheckBefore(Watch a, Watch b)
        {
            if (a.fly == null || a.frames.Count == 0) { Check("RED " + lv + ", the earlier way (the measure): the start flyover was watched", false); return; }
            var a0 = a.frames[0];
            Check("RED " + lv + ", the earlier way (the measure only): its first frame had the plain sky (weight " + a0.weight.ToString("0.000") + "): this is the start the kit changes", a0.weight <= 0.05f);
            var ws = new StringBuilder(); var ls = new StringBuilder();
            foreach (var f in a.frames) { ws.Append(f.name).Append(' ').Append(f.weight.ToString("0.00")).Append("  "); ls.Append(f.name).Append(' ').Append(L(f.lumaMax)).Append("  "); }
            Log("RED " + lv + ", the earlier way: the sky full " + (a.fullAt >= 0f ? "at " + a.fullAt.ToString("0.00") + " s" : "not within the watch") + "; weights " + ws + "; the disc's luma " + ls);
            if (b.frames.Count > 0) Log("RED " + lv + ": the first frame's pixels at the sun: the new way luma " + L(b.frames[0].lumaMax) + ", the earlier way " + L(a0.lumaMax));
            float d = b.levelToFirstMs >= 0f && a.levelToFirstMs >= 0f ? b.levelToFirstMs - a.levelToFirstMs : float.NaN;
            string load = "LOADING TIME (RED " + lv + "): the level load to the first flyover frame on screen: the new way " + Ms(b.levelToFirstMs) + ", the earlier way " + Ms(a.levelToFirstMs)
                          + ": " + (float.IsNaN(d) ? "-" : d.ToString("+0;-0") + " ms") + " (disk cache noise included: the new way ran first); of it the main thread's wait for the panorama on the first frame "
                          + b.waitMs.ToString("0") + " ms; the background read was done " + Ms(b.readyMs) + " after the level load; the level's first frame took " + Ms(b.frame0Ms) + " (new) / " + Ms(a.frame0Ms) + " (earlier)";
            string mem = "MEMORY (RED " + lv + "): the panorama " + b.mb.ToString("0.0") + " MB; resident in the new way from the level load until " + Rel(b) + "; in the earlier way from the flight's first frame until " + Rel(a);
            Log(load); Log(mem);
            numbers += load + "\n" + mem + "\n";
        }

        static Grabbed Find(Watch w, string name) => w.frames.Find(f => f.name == name);
        static string L(float l) => l >= 0f ? l.ToString("0.00") : "-";
        static string L(Grabbed f) => f != null ? L(f.lumaMax) : "-";
        static string W(Grabbed f) => f != null ? f.weight.ToString("0.000") : "-";
        static string T(Grabbed f) => f != null ? "the frame at " + f.t.ToString("0.00") + " s" : "no frame";
        static string Ms(float v) => v >= 0f ? v.ToString("0") + " ms" : "-";
        static string Rel(Watch w) => w.releasedAt >= 0f ? w.releasedAt.ToString("0.0") + " s after the flight's first frame (the flight ended at " + (w.endAt >= 0f ? w.endAt.ToString("0.0") : "-") + " s)" : "not seen released";
        static string Dts(Watch w)
        {
            var s = new StringBuilder();
            for (int i = 1; i < w.dts.Count; i++) s.Append(i > 1 ? " / " : "").Append((w.dts[i] * 1000f).ToString("0"));
            return s.Length > 0 ? s + " ms" : "-";
        }

        // ================================================================================================================ the end
        void Finish()
        {
            string head = (fails == 0 ? "PASS" : "FAIL") + " " + passes + "/" + (passes + fails) + " checks (" + job + " " + lv + "); pictures and report in " + dir;
            Report = head + "\n" + (numbers.Length > 0 ? "NUMBERS\n" + numbers : "") + sb;
            try { File.WriteAllText(Path.Combine(dir, "report_" + job + "_" + lv + ".txt"), Report); } catch (Exception) { }
            Debug.Log("[OzSunFirstFrameProof] " + head + (numbers.Length > 0 ? "\n" + numbers : ""));
            Destroy(gameObject);
        }
    }
}
#endif
