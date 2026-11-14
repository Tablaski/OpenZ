// OpenZ (GPL-3.0) - version 1.4 ue_nuke_stem_gone (editor only): the Play check of the Unit editor's NUKE stem (View/OzUeMushroom.Curve.cs).
#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using OpenZ.Sim;
using Unity.Collections;
using UnityEditor.Media;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using C = OpenZ.OzUeMushroomCurve;

namespace OpenZ
{
    /// <summary>
    /// Editor/Nuke Stem Gone: 60 fps Capture + Check + Owner Clip (Editor/OzUeStemGoneMenu.cs), in Play with the unit editor open. Through
    /// the editor's own calls (no mouse); the field, the play state, the speed, the switch and the size put back. About 1 to 3 minutes.
    /// The view: the editor's start camera (compass home, Fit, centred on the drop point), the cloud at the mean size 1.05, by day as set.
    /// The field: a red and a blue grunt squad 6 m from the point, a red light tank and a blue heavy tank 20 m out, two empty light tanks 55 m out.
    /// <list type="bullet">
    /// <item>Take 1 (measured, no video): Time.captureFramerate = 60 (each frame exactly 1/60 s of game time); every frame from 2.0 s after the
    /// impact to the cloud's end: (a) the screen frame as shown (post, bloom, HUD): the mean sRGB luma of the COLUMN region (the stem's
    /// screen outline inside the head's: the stem's open cylinder and the cap's sphere projected through the camera, convex outlines) and
    /// of the HEAD region (the head's outline shrunk x 0.8 about its middle, off its thin rim, outside the stem's outline grown x 1.25); (b) the camera's HDR (post off,
    /// 1/4 size) three times: all drawn, the stem hidden, the cap hidden: the stem's own added light in the column region and the head's own
    /// added light in the head region; plus once with post (bloom, ACES) and the stem hidden: the head's own look in the head region (its
    /// thinning frame, G2); (c) the stem's state (drawn or not, its _Fade). Frames every 1/15 s from 3.0 s to the end as JPG
    /// (Folder/take1/t03.00s.jpg ..) and a contact sheet (Folder/sheet_take1.jpg, 8 a row, in time order, the legend in report.txt).</item>
    /// <item>
    /// 60 fps fixed, the game's sound (MediaEncoder + AudioRenderer) into Folder/ue_nuke_stem_gone.mp4; stills at 3.0 / 3.5 / 4.0 / 4.5 /
    /// 5.0 / 5.5 s; the same screen rows (a) and (c) and its sheet (Folder/take2/, Folder/sheet_take2.jpg).</item>
    /// </list>
    /// Rows (report.txt):
    ///  G0 the curve compiled in this editor: the stem gone (C.StemFadeEnd) before the shader starts to cool the head (C.HeadCoolFrom);
    ///  G1 each take: the call accepted, the impact seen, one cloud, the frames measured (no PASS on 0 samples);
    ///  G2 the head's first thinning frame (take 1): the first frame (from 2.0 s) whose head-region luma in the head's own look (post on,
    ///     the stem hidden; the mean of the last 5 frames) is under 90 % of its highest so far (the off-Unity model's bar); take 2 (the same
    ///     drop, view and size) uses take 1's frame; the screen frame's own head luma (90 % and 95 %) is printed as information;
    ///  G3 from that frame (or from C.HeadCoolFrom, whichever is first) to the end: the stem not drawn (inactive, renderer off, or _Fade at most 0.02);
    ///  G4 take 1, the column row: from the thinning frame to the end, the stem's added HDR light in the column region at most 2 % of the
    ///     head's added light in the head region, every frame (the row printed for every frame; information: the last frame it was over 2 %);
    ///  G5 the life as before: the cloud ended at C.Life (6.94 s) +- 0.05 s;
    ///  G6 take 2, the clip: every frame written, the sound recorded, the file there.
    /// </summary>
    public sealed class OzUeStemGoneCheck : MonoBehaviour
    {
        const string Tag = "[OpenZ NukeStemGone]";
        public static string Report = "not run";
        /// <summary>The run's folder (report.txt, take1/, take2/, the sheets, the clip). The menu sets a fresh one under the kit's proof/.</summary>
        public static string Folder;
        public static bool Running => run;
        static OzUeStemGoneCheck run;
        public const int Fps = 60;
        public const float LeadSec = 0.5f, AfterSec = 8.0f, MeasureFrom = 2.0f, SheetFrom = 3.0f, ThinShare = 0.90f, ThinInfo = 0.95f, StemBar = 0.02f, FadeOff = 0.02f;
        const int Grid = 4, HdrDiv = 4, TileW = 320, TileH = 180, SheetCols = 8;
        static readonly float[] Stills = { 3.0f, 3.5f, 4.0f, 4.5f, 5.0f, 5.5f };
        static readonly int FadeId = Shader.PropertyToID("_Fade");

        int pass, fail;
        readonly StringBuilder lines = new StringBuilder(), table = new StringBuilder();

        sealed class Sample
        {
            public float t; public bool stemDrawn; public float fade;
            public float colLum, headLum; public int colN, headN;              // the screen frame (sRGB luma 0..255)
            public float stemAdd = -1f, headAdd = -1f; public int hColN, hHeadN; // the HDR renders (linear luma), take 1 only
            public float headLook = -1f;                                         // the head region with post (bloom, ACES) and the stem hidden, take 1 only
        }

        sealed class Take
        {
            public string name; public bool callOk, impact; public string words; public float size, height, endedAt = -1f;
            public int frames, wanted, saved; public bool audio; public string clip; public long bytes; public long samples;   // E1: samples = the sound actually handed to the encoder
            public readonly List<Sample> s = new List<Sample>();
            public readonly List<Texture2D> tiles = new List<Texture2D>(); public readonly List<float> tileAt = new List<float>();
            public float thinAt = -1f;
        }

        public static void Begin()
        {
            if (run) { Debug.LogWarning(Tag + " a run is still going"); return; }
            if (!Application.isPlaying || !OzUnitEditor.UiBuilt || OzUnitEditor.I == null || OzUnitEditor.I.World == null) { Debug.LogWarning(Tag + " enter Play and open the unit editor first"); return; }
            var go = new GameObject("OzUe_StemGoneCheck"); DontDestroyOnLoad(go);
            run = go.AddComponent<OzUeStemGoneCheck>();
        }

        bool changed, on0; float size0;
        void Start() { StartCoroutine(Main()); }
        void OnDestroy() { Time.captureFramerate = 0; PutBack(); if (run == this) run = null; }
        void PutBack() { if (!changed) return; changed = false; OzUeMushroom.On = on0; OzUeMushroom.FixedSize = size0; }

        void Check(bool ok, string what) { if (ok) pass++; else fail++; lines.Append(ok ? "PASS  " : "FAIL  ").AppendLine(what); }
        void Info(string what) { lines.Append("INFO  ").AppendLine(what); }
        static IEnumerator Seconds(float s) { float t0 = Time.realtimeSinceStartup; while (Time.realtimeSinceStartup - t0 < s) yield return null; }

        static Vector3 Focus(OzUnitEditor ed)
        {
            Vector3 f = ed && ed.Rts ? ed.Rts.focus : new Vector3(160, 0, -100);
            var map = ed && ed.World != null ? ed.World.Map : null;
            if (map != null) { f.x = Mathf.Clamp(f.x, 60f, map.width * 2f - 60f); f.z = Mathf.Clamp(f.z, -(map.height * 2f - 60f), -60f); }
            f.y = 0f; return f;
        }

        static void SetField(Vector3 f)
        {
            OzUePlace.Stop(); OzUePick.ClearInspect();
            OzUePlayground.ClearField();
            OzUePlayground.Drop("grunt", Team.Red, f + new Vector3(6f, 0f, 0f), 1);
            OzUePlayground.Drop("grunt", Team.Blue, f + new Vector3(-6f, 0f, 0f), 1);
            OzUePlayground.Drop("light", Team.Red, f + new Vector3(0f, 0f, 20f), 1);
            OzUePlayground.Drop("heavy", Team.Blue, f + new Vector3(0f, 0f, -20f), 1);
            OzUePlayground.Drop("light", Team.Neutral, f + new Vector3(55f, 0f, 0f), 1);
            OzUePlayground.Drop("light", Team.Neutral, f + new Vector3(-55f, 0f, 0f), 1);
        }

        IEnumerator Main()
        {
            Report = "running";
            var ed = OzUnitEditor.I;
            bool playing0 = OzUeControls.Playing; float speed0 = OzUeControls.Speed;
            on0 = OzUeMushroom.On; size0 = OzUeMushroom.FixedSize; changed = true;
            var cam = ed.Rts ? ed.Rts.GetComponent<Camera>() : Camera.main;
            OzUeMushroom.On = true; OzUeMushroom.FixedSize = 1.05f;
            OzUeNuke.Reset();
            OzUeControls.SetSpeed(1f); OzUeControls.SetPlaying(true);
            OzUePlayground.ClearField();
            OzUeControls.CompassHome(); OzUeControls.Fit();
            var f = Focus(ed); f.y = ed.Game != null ? ed.Game.GroundY(f.x, f.z) : 0f;
            OzUeControls.CentreOn(f);
            yield return Seconds(1.5f);

            Check(C.StemFadeEnd < C.HeadCoolFrom && C.StemShown(C.StemFadeEnd + 0.01f) == false && C.StemFade(C.StemFadeEnd) <= 0.0001f,
                  $"G0 the curve as compiled here: the stem fades {C.StemFadeFrom:0.00} -> {C.StemFadeEnd:0.00} s and is not drawn after it; the shader starts to cool the head at "
                  + $"{C.HeadCoolFrom:0.00} s (p {C.CoolFrom:0.00}); the head's opacity fade from {C.HeadFadeFrom:0.00} s; life {C.Life:0.00} s");
            Info($"view: camera {(cam ? cam.name : "none")} at {(cam ? cam.transform.position.y : 0f):0.0} m, screen {Screen.width} x {Screen.height}, night {NightGrade.NightK:0.00}, the drop point ({f.x:0.0}, {f.z:0.0})");

            var t1 = new Take { name = "take1" };
            yield return RunTake(cam, f, t1, true, null);
            yield return WaitClear();
            OzUeNuke.Reset();
            var t2 = new Take { name = "take2", clip = Folder != null ? Path.Combine(Folder, "ue_nuke_stem_gone.mp4") : null };
            yield return RunTake(cam, f, t2, false, t2.clip);
            yield return WaitClear();

            Rows(t1, true, -1f); Rows(t2, false, t1.thinAt);
            Check(t2.clip != null && t2.frames == t2.wanted && t2.wanted > 0 && t2.audio && t2.samples > 0 && t2.bytes > 0,
                  $"G6 take 2, the owner's clip: {t2.frames} of {t2.wanted} frames at {Fps} fps (0.5 s before the call to {AfterSec:0.0} s after the impact), the sound {(t2.audio && t2.samples > 0 ? "recorded" : "NOT RECORDED")} ({t2.samples} sample frames), "
                  + $"{t2.bytes / 1048576.0:0.0} MB, {t2.clip ?? "no folder set"}");
            SaveSheet(t1); SaveSheet(t2);

            PutBack();
            OzUePlayground.ClearField();
            OzUeNuke.Reset();
            OzUeControls.SetSpeed(speed0); OzUeControls.SetPlaying(playing0);
            var head = new StringBuilder();
            head.AppendLine("OzUeStemGoneCheck (1.4 ue_nuke_stem_gone), " + System.DateTime.Now.ToString("d MMM yyyy HH:mm") + ": " + (fail == 0 ? "PASS" : "FAIL") + " " + pass + "/" + (pass + fail));
            head.AppendLine("folder: " + (Folder ?? "none") + " (take1/ and take2/ frames every 1/15 s from 3.0 s, sheet_take1.jpg / sheet_take2.jpg: 8 a row in time order, row r column c = 3.0 s + (8 r + c) / 15 s; take 2 = ue_nuke_stem_gone.mp4)");
            head.AppendLine("owner clip content: the Unit editor's playground at the start camera (compass home, Fit), a field of two grunt squads 6 m from the point, a red light tank and a blue heavy tank 20 m out,");
            head.AppendLine("  two empty light tanks 55 m out; 0.5 s before the NUKE call, the bomber's pass, the bomb, the blast (flash, ring, dust, side explosions), the mushroom rising, the stem fading and gone,");
            head.AppendLine("  the head cooling and thinning away, the ground dust after it, to 8.0 s after the impact; 60 fps fixed (Time.captureFramerate), the screen as shown (HUD), the game's sound.");
            Report = head.ToString() + lines + "\nframe rows (t = the cloud's age; column / head = mean sRGB luma of the screen frame; stem add / head add = HDR linear luma added by the stem in the column region / by the cap in the head region; share = stem add / head add):\n" + table;
            Debug.Log(Tag + " " + (fail == 0 ? "PASS " : "FAIL ") + pass + "/" + (pass + fail) + "\n" + lines);
            if (!string.IsNullOrEmpty(Folder))
            {
                try { Directory.CreateDirectory(Folder); File.WriteAllText(Path.Combine(Folder, "report.txt"), Report.Replace("\r\n", "\n")); }
                catch (System.Exception e) { Debug.LogWarning(Tag + " report not written: " + e.Message); }
            }
            foreach (var t in new[] { t1, t2 }) foreach (var x in t.tiles) if (x) Destroy(x);
            Destroy(gameObject);
        }

        /// <summary>The last blast's cloud and NukeFx gone (at most 12 s): the next take starts on a quiet field.</summary>
        static IEnumerator WaitClear()
        {
            float t0 = Time.realtimeSinceStartup;
            while ((OzUeMushroom.Current || NukeFx.Current) && Time.realtimeSinceStartup - t0 < 12f) yield return null;
            yield return Seconds(1f);
        }

        IEnumerator RunTake(Camera cam, Vector3 f, Take tk, bool hdr, string clip)
        {
            SetField(f);
            OzUeControls.CentreOn(f);
            yield return Seconds(1f);
            string frameDir = Folder != null ? Path.Combine(Folder, tk.name) : null;
            if (frameDir != null) Directory.CreateDirectory(frameDir);
            int w = Screen.width - Screen.width % 2, h = Screen.height - Screen.height % 2;
            int ch = AudioSettings.speakerMode == AudioSpeakerMode.Mono ? 1 : 2, rate = AudioSettings.outputSampleRate;
            int lead = Mathf.RoundToInt(LeadSec * Fps), endFrame = lead + Fps * 20, impactFrame = -1, impacts0 = OzUeNuke.Impacts;
            MediaEncoder enc = null; OzUeMushroom m = null; var stillHit = new bool[Stills.Length];
            try
            {
                if (clip != null)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(clip));
                    var vattr = new VideoTrackAttributes { frameRate = new MediaRational(Fps), width = (uint)w, height = (uint)h, includeAlpha = false, bitRateMode = UnityEditor.VideoBitrateMode.High };
                    var aattr = new AudioTrackAttributes { sampleRate = new MediaRational(rate), channelCount = (ushort)ch, language = "en" };
                    enc = new MediaEncoder(clip, vattr, aattr);
                }
                Time.captureFramerate = Fps;
                yield return null;
                if (enc != null) tk.audio = AudioRenderer.Start();
                for (int k = 0; k < endFrame; k++)
                {
                    if (k == lead)
                    {
                        var d = OzUePlayground.Drop(OzUePlayground.Nuke, Team.Neutral, f, 1);
                        tk.callOk = d.ok && d.why == OzUeNuke.InboundWords; tk.words = d.why;
                        if (!d.ok) endFrame = k + 1;
                    }
                    if (k == lead + Fps * 15 && impactFrame < 0) endFrame = k + 1;                    // no impact 15 s after the call
                    yield return new WaitForEndOfFrame();
                    if (impactFrame < 0 && OzUeNuke.Impacts != impacts0)
                    {
                        impactFrame = k; tk.impact = true; m = OzUeMushroom.Current;
                        if (m) { tk.size = m.Size; tk.height = m.Height; }
                        endFrame = k + Mathf.RoundToInt(AfterSec * Fps) + 1;
                    }
                    var tex = Rgba(ScreenCapture.CaptureScreenshotAsTexture(), w, h);
                    try
                    {
                        if (enc != null) enc.AddFrame(tex);
                        tk.frames++;
                        if (m)
                        {
                            float t = m.Age;
                            if (m.Ended && tk.endedAt < 0f) tk.endedAt = t;
                            if (!m.Ended && t >= MeasureFrom) tk.s.Add(Measure(cam, m, tex, hdr, t));
                            if (t >= SheetFrom - 0.001f && t <= C.Life + 0.5f && (k - impactFrame) % 4 == 0)
                            {
                                if (frameDir != null) File.WriteAllBytes(Path.Combine(frameDir, "t" + t.ToString("00.00") + "s.jpg"), tex.EncodeToJPG(90));
                                tk.tiles.Add(Tile(tex)); tk.tileAt.Add(t); tk.saved++;
                            }
                            for (int i = 0; i < Stills.Length; i++)
                                if (!stillHit[i] && t >= Stills[i] && frameDir != null) { stillHit[i] = true; File.WriteAllBytes(Path.Combine(Folder, $"{tk.name}_still_{Stills[i]:0.0}s.jpg"), tex.EncodeToJPG(92)); }
                        }
                    }
                    finally { Destroy(tex); }
                    if (tk.audio)
                    {
                        int n = (int)AudioRenderer.GetSampleCountForCaptureFrame();
                        if (n > 0) using (var buf = new NativeArray<float>(n * ch, Allocator.Temp)) { AudioRenderer.Render(buf); enc.AddSamples(buf); tk.samples += n; }
                    }
                }
                tk.wanted = endFrame;
            }
            finally
            {
                Time.captureFramerate = 0;
                if (tk.audio) AudioRenderer.Stop();
                if (enc != null) enc.Dispose();
            }
            if (clip != null && File.Exists(clip)) tk.bytes = new FileInfo(clip).Length;
            Debug.Log($"{Tag} {tk.name}: {tk.frames} frames, impact {(tk.impact ? "at frame " + impactFrame : "NOT SEEN")}, {tk.s.Count} measured, {tk.saved} sheet frames{(clip != null ? ", clip " + clip : "")}");
        }

        /// <summary>The capture as RGBA32 at w x h (the encoder's and the measure's format).</summary>
        static Texture2D Rgba(Texture2D tex, int w, int h)
        {
            if (tex.format == TextureFormat.RGBA32 && tex.width == w && tex.height == h) return tex;
            var t2 = new Texture2D(w, h, TextureFormat.RGBA32, false);
            if (tex.width >= w && tex.height >= h) t2.SetPixels(tex.GetPixels(0, 0, w, h));
            t2.Apply(false); Destroy(tex); return t2;
        }

        // ------------------------------------------------------------------------------------------------ one frame measured
        Sample Measure(Camera cam, OzUeMushroom m, Texture2D tex, bool hdr, float t)
        {
            var s = new Sample { t = t };
            var sr = m.StemRenderer;
            s.stemDrawn = m.Stem && m.Stem.gameObject.activeInHierarchy && sr && sr.enabled;
            s.fade = sr && sr.sharedMaterial ? sr.sharedMaterial.GetFloat(FadeId) : 0f;
            var stemHull = Hull(StemOutline(cam, m.Stem)); var capHull = Hull(SphereOutline(cam, m.Cap));
            var stemOut = Grow(stemHull, 1.25f); var capIn = Grow(capHull, 0.8f);                   // the head region: off the cap's thin rim and off the stem
            if (stemHull.Count < 3 || capHull.Count < 3) return s;
            var px = tex.GetPixelData<Color32>(0); int w = tex.width, h = tex.height;
            Bounds2(capHull, w, h, out int x0, out int y0, out int x1, out int y1);
            double cs = 0, hs = 0;
            for (int y = y0; y <= y1; y += Grid)
                for (int x = x0; x <= x1; x += Grid)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    if (!Inside(capHull, p)) continue;
                    var c = px[y * w + x]; float l = 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
                    if (Inside(stemHull, p)) { cs += l; s.colN++; }
                    else if (!Inside(stemOut, p) && Inside(capIn, p)) { hs += l; s.headN++; }
                }
            s.colLum = s.colN > 0 ? (float)(cs / s.colN) : 0f; s.headLum = s.headN > 0 ? (float)(hs / s.headN) : 0f;
            if (!hdr || !cam) return s;
            int hw = Mathf.Max(64, w / HdrDiv), hh = Mathf.Max(36, h / HdrDiv);
            var all = Hdr(cam, hw, hh);
            Color[] noStem, noCap;
            bool stemOn = sr && sr.enabled; if (sr) sr.enabled = false;
            try { noStem = Hdr(cam, hw, hh); } finally { if (sr) sr.enabled = stemOn; }
            var cr = m.CapRenderer; bool capOn = cr && cr.enabled; if (cr) cr.enabled = false;
            try { noCap = Hdr(cam, hw, hh); } finally { if (cr) cr.enabled = capOn; }
            Color[] look;                                                                           // the head's own look on screen: post on, the stem hidden (no stem light or bloom in it)
            if (sr) sr.enabled = false;
            try { look = Hdr(cam, hw, hh, true); } finally { if (sr) sr.enabled = stemOn; }
            double sa = 0, ha = 0, hl = 0; float kx = (float)w / hw, ky = (float)h / hh;
            for (int j = 0; j < hh; j++)
                for (int i = 0; i < hw; i++)
                {
                    var p = new Vector2((i + 0.5f) * kx, (j + 0.5f) * ky);
                    if (!Inside(capHull, p)) continue;
                    int q = j * hw + i;
                    if (Inside(stemHull, p)) { sa += Mathf.Max(0f, Lum(all[q]) - Lum(noStem[q])); s.hColN++; }
                    else if (!Inside(stemOut, p) && Inside(capIn, p)) { ha += Mathf.Max(0f, Lum(all[q]) - Lum(noCap[q])); hl += Lum(look[q]); s.hHeadN++; }
                }
            s.stemAdd = s.hColN > 0 ? (float)(sa / s.hColN) : 0f; s.headAdd = s.hHeadN > 0 ? (float)(ha / s.hHeadN) : 0f;
            s.headLook = s.hHeadN > 0 ? (float)(hl / s.hHeadN) : -1f;
            return s;
        }

        static float Lum(Color c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;

        /// <summary>The camera's HDR (post off) at w x h, linear (as OzUeMushroomCheck.CaptureFloat, smaller); post = true: with the
        /// camera's post (bloom, ACES), for the head's look.</summary>
        static Color[] Hdr(Camera c, int w, int h, bool post = false)
        {
            var desc = new RenderTextureDescriptor(w, h, RenderTextureFormat.ARGBHalf, 24) { msaaSamples = 1, sRGB = false };
            var rt = RenderTexture.GetTemporary(desc);
            var data = c.GetUniversalAdditionalCameraData();
            bool oldPost = data && data.renderPostProcessing; var oldT = c.targetTexture;
            try { if (data) data.renderPostProcessing = post && oldPost; c.targetTexture = rt; c.Render(); }
            finally { c.targetTexture = oldT; if (data) data.renderPostProcessing = oldPost; }
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGBAHalf, false, true);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply(false);
            RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
            var px = tex.GetPixels(); Destroy(tex);
            return px;
        }

        // ------------------------------------------------------------------------------------------------ outlines on screen
        /// <summary>The stem's open cylinder (radius 1 at its top y +0.5, 0.8 at its foot y -0.5, OzUeMushroom.Cylinder) through the camera.</summary>
        static List<Vector2> StemOutline(Camera cam, Transform stem)
        {
            var pts = new List<Vector2>(64); if (!cam || !stem) return pts;
            var M = stem.localToWorldMatrix;
            for (int i = 0; i < 32; i++)
            {
                float a = i / 32f * Mathf.PI * 2f, s = Mathf.Sin(a), c = Mathf.Cos(a);
                Add(cam, M.MultiplyPoint3x4(new Vector3(s, 0.5f, -c)), pts);
                Add(cam, M.MultiplyPoint3x4(new Vector3(0.8f * s, -0.5f, -0.8f * c)), pts);
            }
            return pts;
        }

        /// <summary>The cap's unit sphere through the camera.</summary>
        static List<Vector2> SphereOutline(Camera cam, Transform cap)
        {
            var pts = new List<Vector2>(300); if (!cam || !cap) return pts;
            var M = cap.localToWorldMatrix;
            for (int iy = 1; iy < 12; iy++)
                for (int ix = 0; ix < 24; ix++)
                {
                    float th = iy / 12f * Mathf.PI, ph = ix / 24f * Mathf.PI * 2f;
                    Add(cam, M.MultiplyPoint3x4(new Vector3(Mathf.Cos(ph) * Mathf.Sin(th), Mathf.Cos(th), Mathf.Sin(ph) * Mathf.Sin(th))), pts);
                }
            Add(cam, M.MultiplyPoint3x4(Vector3.up), pts); Add(cam, M.MultiplyPoint3x4(Vector3.down), pts);
            return pts;
        }

        static void Add(Camera cam, Vector3 world, List<Vector2> pts) { var s = cam.WorldToScreenPoint(world); if (s.z > 0.01f) pts.Add(new Vector2(s.x, s.y)); }

        /// <summary>Convex hull, counter-clockwise (Andrew's monotone chain).</summary>
        static List<Vector2> Hull(List<Vector2> p)
        {
            var h = new List<Vector2>();
            if (p.Count < 3) return h;
            p.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            for (int pass = 0; pass < 2; pass++)
            {
                int start = h.Count;
                for (int k = 0; k < p.Count; k++)
                {
                    var q = pass == 0 ? p[k] : p[p.Count - 1 - k];
                    while (h.Count >= start + 2 && Cross(h[h.Count - 2], h[h.Count - 1], q) <= 0f) h.RemoveAt(h.Count - 1);
                    h.Add(q);
                }
                h.RemoveAt(h.Count - 1);
            }
            return h;
        }

        static float Cross(Vector2 o, Vector2 a, Vector2 b) => (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);

        static bool Inside(List<Vector2> hull, Vector2 p)
        {
            if (hull.Count < 3) return false;
            for (int i = 0; i < hull.Count; i++) if (Cross(hull[i], hull[(i + 1) % hull.Count], p) < 0f) return false;
            return true;
        }

        static List<Vector2> Grow(List<Vector2> hull, float k)
        {
            var g = new List<Vector2>(hull.Count); if (hull.Count == 0) return g;
            var c = Vector2.zero; foreach (var v in hull) c += v; c /= hull.Count;
            foreach (var v in hull) g.Add(c + (v - c) * k);
            return g;
        }

        static void Bounds2(List<Vector2> hull, int w, int h, out int x0, out int y0, out int x1, out int y1)
        {
            float a = float.MaxValue, b = float.MaxValue, c = float.MinValue, d = float.MinValue;
            foreach (var v in hull) { a = Mathf.Min(a, v.x); b = Mathf.Min(b, v.y); c = Mathf.Max(c, v.x); d = Mathf.Max(d, v.y); }
            x0 = Mathf.Clamp(Mathf.FloorToInt(a), 0, w - 1); y0 = Mathf.Clamp(Mathf.FloorToInt(b), 0, h - 1);
            x1 = Mathf.Clamp(Mathf.CeilToInt(c), 0, w - 1); y1 = Mathf.Clamp(Mathf.CeilToInt(d), 0, h - 1);
        }

        // ------------------------------------------------------------------------------------------------ the rows of a take
        /// <summary>The first frame (from MeasureFrom) whose value, the mean of the last 5 frames (1/12 s: no single-frame flicker), is under
        /// share x its highest so far; -1 when none. Values under 0 (not measured) are skipped; n = the frames measured.</summary>
        static float FirstThin(List<Sample> s, System.Func<Sample, float> val, float share, out int n, out float peak)
        {
            n = 0; peak = 0f; float at = -1f; var last = new Queue<float>();
            foreach (var x in s)
            {
                float a = val(x); if (a < 0f) continue; n++;
                last.Enqueue(a); if (last.Count > 5) last.Dequeue();
                float v = 0f; foreach (var q in last) v += q; v /= last.Count;
                if (v > peak) peak = v;
                else if (at < 0f && v < share * peak) at = x.t;
            }
            return at;
        }

        void Rows(Take tk, bool hdr, float refThin)
        {
            string T = tk.name == "take1" ? "take 1 (measured)" : "take 2";
            Check(tk.callOk && tk.impact && tk.s.Count >= 200,
                  $"G1 {T}: the call {(tk.callOk ? "accepted" : "NOT ACCEPTED (" + (tk.words ?? "no words") + ")")}, the impact {(tk.impact ? "seen" : "NOT SEEN")}, the cloud size x{tk.size:0.00} "
                  + $"{tk.height:0.0} m high, {tk.s.Count} frames measured from {MeasureFrom:0.0} s to the end (want at least 200), {tk.saved} sheet frames from {SheetFrom:0.0} s");
            // G2: the head's first thinning frame. Take 1: the head's own look (post on, the stem hidden: the stem's light and bloom leaving
            // the column do not count as the head thinning), under 90 % of its highest (the off-Unity model's "head shown" bar,
            // proof/proof_column.txt). Take 2 (the same drop, view and size): take 1's frame. The screen frame's head region: information.
            float scr = FirstThin(tk.s, x => x.headN > 0 ? x.headLum : -1f, ThinShare, out int nScr, out float pScr);
            float scr95 = FirstThin(tk.s, x => x.headN > 0 ? x.headLum : -1f, ThinInfo, out _, out _);
            if (hdr)
            {
                tk.thinAt = FirstThin(tk.s, x => x.hHeadN > 0 ? x.headLook : -1f, ThinShare, out int heads, out float peak);
                float look95 = FirstThin(tk.s, x => x.hHeadN > 0 ? x.headLook : -1f, ThinInfo, out _, out _);
                Check(heads > 0 && tk.thinAt > 0f,
                      $"G2 {T}: the head's first thinning frame (its own look: post on, the stem hidden; the head-region luma, the mean of the last 5 frames, under {ThinShare * 100f:0} % of its highest so far): "
                      + (tk.thinAt > 0f ? $"{tk.thinAt:0.000} s" : "NOT FOUND") + $" ({heads} frames with the head measured; its highest {peak:0.000}); the shader starts to cool the head at {C.HeadCoolFrom:0.00} s");
                Info($"G2 {T}, information: under {ThinInfo * 100f:0} % (its own look) from {(look95 > 0f ? look95.ToString("0.000") + " s" : "never")}; the screen frame's head region "
                     + $"(with the stem's light and bloom, the HUD) under {ThinShare * 100f:0} % from {(scr > 0f ? scr.ToString("0.000") + " s" : "never")}, under {ThinInfo * 100f:0} % from {(scr95 > 0f ? scr95.ToString("0.000") + " s" : "never")} ({nScr} frames, highest {pScr:0.0})");
            }
            else
            {
                tk.thinAt = refThin;
                Info($"G2 {T}: the head's first thinning frame of take 1 used (the same drop, view and size): {(refThin > 0f ? refThin.ToString("0.000") + " s" : "NOT FOUND (the cooling start used)")}; "
                     + $"this take's screen frame (with the stem's light and bloom, the HUD): under {ThinShare * 100f:0} % from {(scr > 0f ? scr.ToString("0.000") + " s" : "never")}, under {ThinInfo * 100f:0} % from {(scr95 > 0f ? scr95.ToString("0.000") + " s" : "never")} ({nScr} frames, highest {pScr:0.0})");
            }
            float from = tk.thinAt > 0f ? Mathf.Min(tk.thinAt, C.HeadCoolFrom) : C.HeadCoolFrom;
            int n3 = 0, bad3 = 0; float firstBad3 = -1f, lastDrawn = -1f;
            foreach (var x in tk.s)
            {
                if (x.stemDrawn && x.fade > FadeOff) lastDrawn = x.t;
                if (x.t < from - 0.0001f) continue;
                n3++;
                if (x.stemDrawn && x.fade > FadeOff) { bad3++; if (firstBad3 < 0f) firstBad3 = x.t; }
            }
            Check(n3 > 0 && bad3 == 0, $"G3 {T}: from {from:0.000} s (the thinning frame or the cooling start, the earlier) to the end the stem is not drawn: {n3} frames, {bad3} with the stem drawn"
                  + (bad3 > 0 ? $" (first at {firstBad3:0.000} s)" : "") + $"; the last frame with the stem drawn (_Fade over {FadeOff}) at {(lastDrawn >= 0f ? lastDrawn.ToString("0.000") + " s" : "none")}");
            if (hdr)
            {
                int n4 = 0, bad4 = 0; float worst = 0f, lastOver = -1f, maxBefore = 0f;
                foreach (var x in tk.s)
                {
                    if (x.stemAdd < 0f || x.hColN == 0 || x.hHeadN == 0) continue;
                    float share = x.stemAdd <= 1e-4f ? 0f : x.stemAdd / Mathf.Max(x.headAdd, 1e-4f);
                    if (share > StemBar) lastOver = x.t;
                    if (x.t < from - 0.0001f) { maxBefore = Mathf.Max(maxBefore, share); continue; }
                    n4++; worst = Mathf.Max(worst, share);
                    if (share > StemBar) bad4++;
                }
                Check(n4 > 0 && bad4 == 0, $"G4 {T}, the column row: from {from:0.000} s to the end the stem's added light in the column is at most {StemBar * 100f:0} % of the head's in the head region: "
                      + $"{n4} frames, {bad4} over, the highest {worst * 100f:0.00} %; the last frame over {StemBar * 100f:0} % at {(lastOver >= 0f ? lastOver.ToString("0.000") + " s" : "none")}; before {from:0.000} s up to {maxBefore * 100f:0.0} %");
            }
            Check(tk.endedAt > 0f && Mathf.Abs(tk.endedAt - C.Life) <= 0.05f, $"G5 {T}: the cloud ended at {(tk.endedAt > 0f ? tk.endedAt.ToString("0.000") + " s" : "NOT SEEN")} (its life {C.Life:0.000} s, as before)");
            table.AppendLine($"--- {T}; the head's first thinning frame {(tk.thinAt > 0f ? tk.thinAt.ToString("0.000") + " s" : "not found")}");
            table.AppendLine("   t s  | stem drawn | _Fade | column | head  | col-head | stem add | head add | share % | head look");
            foreach (var x in tk.s)
                table.AppendLine($"  {x.t,6:0.000} | {(x.stemDrawn ? "yes" : "no "),-10} | {x.fade,5:0.000} | {x.colLum,6:0.0} | {x.headLum,5:0.0} | {x.colLum - x.headLum,8:+0.0;-0.0} | "
                                 + (x.stemAdd >= 0f ? $"{x.stemAdd,8:0.0000} | {x.headAdd,8:0.0000} | {(x.stemAdd <= 1e-4f ? 0f : x.stemAdd / Mathf.Max(x.headAdd, 1e-4f)) * 100f,7:0.00} | {x.headLook,9:0.0000}" : "       - |        - |       - |         -")
                                 + (tk.thinAt > 0f && Mathf.Abs(x.t - tk.thinAt) < 0.0001f ? "   <- the head's first thinning frame" : ""));
        }

        // ------------------------------------------------------------------------------------------------ the contact sheets
        static Texture2D Tile(Texture2D src)
        {
            var rt = RenderTexture.GetTemporary(TileW, TileH, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(src, rt);
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var t = new Texture2D(TileW, TileH, TextureFormat.RGB24, false);
            t.ReadPixels(new Rect(0, 0, TileW, TileH), 0, 0); t.Apply(false);
            RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
            return t;
        }

        void SaveSheet(Take tk)
        {
            if (tk.tiles.Count == 0 || string.IsNullOrEmpty(Folder)) return;
            int rows = (tk.tiles.Count + SheetCols - 1) / SheetCols;
            var sheet = new Texture2D(SheetCols * TileW, rows * TileH, TextureFormat.RGB24, false);
            var black = new Color32[sheet.width * sheet.height]; sheet.SetPixels32(black);
            for (int i = 0; i < tk.tiles.Count; i++)
            {
                int r = i / SheetCols, c = i % SheetCols;
                sheet.SetPixels(c * TileW, (rows - 1 - r) * TileH, TileW, TileH, tk.tiles[i].GetPixels());   // row 0 at the top
            }
            sheet.Apply(false);
            try { File.WriteAllBytes(Path.Combine(Folder, "sheet_" + tk.name + ".jpg"), sheet.EncodeToJPG(88)); }
            catch (System.Exception e) { Debug.LogWarning(Tag + " sheet not written: " + e.Message); }
            Destroy(sheet);
            var legend = new StringBuilder();
            for (int i = 0; i < tk.tileAt.Count; i++) legend.Append(i % SheetCols == 0 ? (i == 0 ? "" : "\n") + $"  row {i / SheetCols}:" : "").Append($" {tk.tileAt[i]:0.00}");
            Info($"sheet_{tk.name}.jpg: {tk.tiles.Count} frames every 1/15 s, ages (s) by row:\n{legend}");
        }
    }
}
#endif
