#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OpenZ
{
    /// <summary>
    /// k75a4 Play proof of the faction screen's polish (1.2 owner items 102, 104, 108, 112, 113, 115); menu OpenZ/V1.2/Faction Screen: 75a4
    /// Play Proof (Play), Editor/OzV12FactionPolishCheck.cs. Started on the MainMenu scene in Play it walks by itself (about 90 s), hands
    /// off, at 1920 x 1080 and then 1366 x 768:
    ///  R1  the screen opens: the renders supersampled (2 x the poster's pixels, mipmaps, no MSAA), each poster's render on its own material
    ///      (Zod's glint, EvilZod's glow), EvilZod's render magnified about the band, the shadow and glow of the posters under the cards.
    ///  R2  smooth edges: each render read back and averaged 2 x 2 as the screen samples it; the share of its silhouette's edge pixels that
    ///      are partly covered (anti-aliased), against the same render sampled one texel in four (what an un-smoothed render gives).
    ///  R3  the key hints row (top right) inside the screen, clear of BACK, the title and the posters.
    ///  R4  the pointer on Zod's poster: the glint runs across his lenses (its band from before the left lens to past the right one); the
    ///      lenses brighter mid-pass than between passes, the face round them unchanged. Pictures (and close-ups of the glasses).
    ///  R5  his card: the sound log has ui_dossier_in once and ui_dossier_row five times (one per row, never two within 40 ms), no paper
    ///      sound of k75a3; the rows in Courier Prime, the same rows on two lines as before (Zod 2, 5), none cut.
    ///  R6  to EvilZod's poster: ui_dossier_out once (Zod's card back), ui_dossier_in once, five row ticks; his rows (EvilZod 1 on two lines);
    ///      his eyes' glow pulses (low .. high, smooth); the halo round the eyes brighter at its high. Pictures (and close-ups of the eyes).
    ///  R7  the cape: the fill on for EvilZod; in his render the strip above the band has N columns with gaps (the torn ends) that the fill
    ///      closes (0 left); close-ups of the poster's bottom left with the pointer at his poster's left, middle and right (three angles).
    ///  R8  the shade: his card out, the poster's shade on it light and narrow; the card's paper beside the poster's edge as light as the
    ///      paper further in (the icons' column clear). Close-up.
    ///  R9  1366 x 768: the renders' size, the rows, the key row; pictures. R10 back on the main menu: the renders' materials gone.
    /// Test only (editor).
    /// </summary>
    public sealed class OzFactionPolishProof : MonoBehaviour
    {
        public static string Report = "idle";
        public static OzFactionPolishProof I { get; private set; }
        const string T = "[OzFactionPolishProof] ";

        bool started, putBack; int viewWas = -1;
        string dir; readonly StringBuilder sb = new StringBuilder(); int passes, fails, pics;

        /// <summary></summary>
        public static string Folder()
        {
            var d = new DirectoryInfo(Application.dataPath);
            for (int i = 0; i < 8 && d != null; i++, d = d.Parent)
            {
                var staging = Path.Combine(d.FullName, "work", "staging");
                if (!Directory.Exists(staging)) continue;
                foreach (var stage in Directory.GetDirectories(staging))
                {
                    var kit = Path.Combine(stage, "k75a4_faction_polish");
                    if (Directory.Exists(kit)) return Path.Combine(kit, "proof");
                }
            }
            return Path.Combine(Application.persistentDataPath, "k75a4_faction_polish");
        }

        public static void Run()
        {
            if (!Application.isPlaying) { Debug.LogWarning(T + "enter Play on the MainMenu scene first"); return; }
            if (I) { Debug.LogWarning(T + "already running"); return; }
            if (!FindFirstObjectByType<OpenZMainMenu>()) { Debug.LogWarning(T + "start it on the main menu (the MainMenu scene in Play)"); return; }
            Report = "running";
            var go = new GameObject("OzFactionPolishProof"); DontDestroyOnLoad(go);
            go.AddComponent<OzFactionPolishProof>();
        }

        public static void Stop()
        {
            if (!I) { Debug.Log(T + "not running"); return; }
            I.StopAllCoroutines(); I.Log("stopped by the menu"); I.Finish();
        }

        void Awake() { I = this; }

        void Start()
        {
            string root = Folder(); int n = 1;
            while (Directory.Exists(Path.Combine(root, "play_" + n))) n++;
            dir = Path.Combine(root, "play_" + n); Directory.CreateDirectory(dir);
            viewWas = OzFinalGateView.SelectedIndex();
            started = true;
            Log("k75a4 faction polish Play proof, Game view " + Screen.width + " x " + Screen.height + ", into " + dir);
            StartCoroutine(All());
        }

        void OnDestroy()
        {
            if (started && !putBack) { PutBack(); Debug.Log(T + "stopped before its end: the settings put back"); }
            if (I == this) I = null;
        }

        void PutBack()
        {
            putBack = true;
            var m = Menu(); if (m) m.ProofFactionPointer(null);
            if (viewWas >= 0) OzFinalGateView.Select(viewWas);
        }

        void Finish()
        {
            if (!putBack) PutBack();
            Report = $"{T}{(fails == 0 ? "PASS" : "FAIL")} {passes}/{passes + fails} checks, {pics} pictures; {dir.Replace('\\', '/')}";
            Log(Report);
            File.WriteAllText(Path.Combine(dir, "checks.txt"), sb.ToString());
            Destroy(gameObject);
        }

        // ------------------------------------------------------------------------------------------------ helpers
        void Log(string t) { sb.AppendLine(t); Debug.Log(T + t); }
        void Check(string what, bool ok) { if (ok) passes++; else fails++; Log((ok ? "PASS " : "FAIL ") + what); }
        static IEnumerator Real(float s) { float t = Time.realtimeSinceStartup + s; while (Time.realtimeSinceStartup < t) yield return null; }
        static IEnumerator Until(System.Func<bool> c, float max) { float t = Time.realtimeSinceStartup + max; while (Time.realtimeSinceStartup < t && !SafeTrue(c)) yield return null; }
        static bool SafeTrue(System.Func<bool> c) { try { return c(); } catch (System.Exception) { return false; } }
        static OpenZMainMenu Menu() => FindFirstObjectByType<OpenZMainMenu>();
        static float LogNow => Time.unscaledTime;
        static List<OzSoundLog.Entry> Played(float l0, string key) => OzSoundLog.Since(l0).Where(e => e.key == key && e.outcome != null && e.outcome.StartsWith("PLAY")).ToList();
        static string Plain(string s) => System.Text.RegularExpressions.Regex.Replace(s ?? "", "<[^>]+>", "");

        IEnumerator Shot(string name)
        {
            string p = Path.Combine(dir, name + "_" + Screen.width + "x" + Screen.height + ".png");
            ScreenCapture.CaptureScreenshot(p); pics++; Log("   picture " + Path.GetFileName(p));
            yield return OzProofPictures.AfterGrab(); yield return OzProofPictures.Written(p);
        }

        Texture2D grab;
        /// <summary>The frame as it is now (bottom-left origin), kept in <see cref="grab"/>.</summary>
        IEnumerator Grab()
        {
            yield return new WaitForEndOfFrame();
            if (grab) Destroy(grab);
            grab = ScreenCapture.CaptureScreenshotAsTexture();
        }

        /// <summary>A close-up of a screen rect from the last grab (x scale, nearest: the pixels as they are).</summary>
        void Crop(string name, Rect r, int scale)
        {
            if (!grab) return;
            int x0 = Mathf.Clamp(Mathf.FloorToInt(r.xMin), 0, grab.width - 1), y0 = Mathf.Clamp(Mathf.FloorToInt(r.yMin), 0, grab.height - 1);
            int w = Mathf.Clamp(Mathf.CeilToInt(r.width), 1, grab.width - x0), h = Mathf.Clamp(Mathf.CeilToInt(r.height), 1, grab.height - y0);
            var src = grab.GetPixels(x0, y0, w, h);
            var c = new Texture2D(w * scale, h * scale, TextureFormat.RGBA32, false);
            var px = new Color[w * scale * h * scale];
            for (int y = 0; y < h * scale; y++) for (int x = 0; x < w * scale; x++) px[y * w * scale + x] = src[(y / scale) * w + x / scale];
            c.SetPixels(px); c.Apply(false);
            File.WriteAllBytes(Path.Combine(dir, name + "_" + Screen.width + "x" + Screen.height + ".png"), c.EncodeToPNG());
            Destroy(c); pics++;
            Log("   picture " + name + " (a close-up, x" + scale + ")");
        }

        IEnumerator SetSize(int w, int h)
        {
            OzFinalGateView.SetFixed(w, h, "OpenZ " + w + "x" + h, out string info);
            yield return Until(() => Screen.width == w && Screen.height == h, 5f);
            yield return Real(0.8f);
            if (Screen.width != w || Screen.height != h) Check($"the Game view shows {w} x {h} ({info}; it shows {Screen.width} x {Screen.height})", false);
            else Log("Game view " + w + " x " + h + " (" + info + ")");
        }

        static Rect ScreenRect(RectTransform rt)
        {
            if (!rt) return new Rect();
            var c = new Vector3[4]; rt.GetWorldCorners(c);
            return Rect.MinMaxRect(Mathf.Min(c[0].x, c[1].x, c[2].x, c[3].x), Mathf.Min(c[0].y, c[1].y, c[2].y, c[3].y), Mathf.Max(c[0].x, c[1].x, c[2].x, c[3].x), Mathf.Max(c[0].y, c[1].y, c[2].y, c[3].y));
        }

        IEnumerator Open()
        {
            var m = Menu();
            if (!m) { Check("the main menu", false); yield break; }
            if (m.ProofView != "Main") { m.ProofMain(); yield return Real(0.8f); }
            m.ProofFactionPointer(Away);
            m.ProofFaction();
            yield return Until(() => m.ProofFactionShown && m.ProofRig && m.ProofRig.Ready && m.ProofDeckReady && m.ProofPrintAlpha(0) > 0.99f && m.ProofPrintAlpha(1) > 0.99f && m.ProofFigureFx(0) && m.ProofFigureFx(1), 15f);
            yield return Real(0.8f);
        }

        static Vector2 Away => new Vector2(Screen.width * 0.5f, Screen.height * 0.06f);

        /// <summary>Render uv -> screen px through a figure's rect and uv rect.</summary>
        static Vector2 UvToScreen(RawImage fig, Vector2 uv)
        {
            var r = ScreenRect(fig.rectTransform); var u = fig.uvRect;
            return new Vector2(r.xMin + (uv.x - u.x) / u.width * r.width, r.yMin + (uv.y - u.y) / u.height * r.height);
        }

        /// <summary>A screen point's render uv.</summary>
        static Vector2 ScreenToUv(RawImage fig, Vector2 p)
        {
            var r = ScreenRect(fig.rectTransform); var u = fig.uvRect;
            return new Vector2(u.x + (p.x - r.xMin) / r.width * u.width, u.y + (p.y - r.yMin) / r.height * u.height);
        }

        /// <summary>The spots' ellipses as the material holds them now: (middle, map) x 2.</summary>
        static void Spots(Material m, out Vector2 c0, out Vector4 m0, out Vector2 c1, out Vector4 m1)
        {
            var c = m.GetVector(OzFactionPolish.IdSpotC);
            c0 = new Vector2(c.x, c.y); c1 = new Vector2(c.z, c.w); m0 = m.GetVector(OzFactionPolish.IdSpotM1); m1 = m.GetVector(OzFactionPolish.IdSpotM2);
        }
        static float R(Vector2 uv, Vector2 c, Vector4 m) { var d = uv - c; return new Vector2(m.x * d.x + m.y * d.y, m.z * d.x + m.w * d.y).magnitude; }

        /// <summary>The mean light (linear luma) of the grabbed pixels inside the screen rect r whose render uv's ellipse distance to the
        /// spot lies in [r0, r1).</summary>
        float MeanIn(RawImage fig, Rect r, Vector2 c, Vector4 m, float r0, float r1)
        {
            if (!grab) return 0f;
            double s = 0; int n = 0;
            for (int y = Mathf.Max(0, (int)r.yMin); y < Mathf.Min(grab.height, (int)r.yMax); y++)
                for (int x = Mathf.Max(0, (int)r.xMin); x < Mathf.Min(grab.width, (int)r.xMax); x++)
                {
                    float d = R(ScreenToUv(fig, new Vector2(x + 0.5f, y + 0.5f)), c, m);
                    if (d < r0 || d >= r1) continue;
                    var p = grab.GetPixel(x, y).linear; s += 0.2126 * p.r + 0.7152 * p.g + 0.0722 * p.b; n++;
                }
            return n > 0 ? (float)(s / n) : 0f;
        }

        /// <summary>A screen rect round both spots (their ellipses' extents x k).</summary>
        static Rect SpotRect(RawImage fig, Vector2 c0, Vector2 c1, float pad)
        {
            Vector2 a = UvToScreen(fig, c0), b = UvToScreen(fig, c1);
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x) - pad, Mathf.Min(a.y, b.y) - pad * 0.6f, Mathf.Max(a.x, b.x) + pad, Mathf.Max(a.y, b.y) + pad * 0.6f);
        }

        // ------------------------------------------------------------------------------------------------ the walk
        IEnumerator All()
        {
            yield return Real(1f);
            yield return SetSize(1920, 1080);
            yield return Open();
            var m = Menu(); var rig = m.ProofRig;
            Renders(m, rig, "R1");
            yield return Real(0.5f);
            yield return Smooth(m, rig);
            yield return Keys(m, "R3");
            yield return Zod(m);
            yield return Evil(m);
            yield return Cape(m, rig);
            yield return Shade(m);
            // R9 1366 x 768
            m.ProofFactionPointer(Away); yield return Real(1f);
            m.ProofFactionBack(); yield return Real(1.5f);
            yield return SetSize(1366, 768);
            yield return Open();
            m = Menu(); rig = m.ProofRig;
            Renders(m, rig, "R9");
            yield return Keys(m, "R9");
            yield return Shot("faction");
            m.ProofFactionPointer(ScreenRect(m.ProofPoster(0)).center); yield return Real(1.5f);
            Rows(m, 0, "R9");
            yield return Shot("hover_zod_card");
            m.ProofFactionPointer(ScreenRect(m.ProofPoster(1)).center); yield return Real(1.8f);
            Rows(m, 1, "R9");
            yield return Shot("hover_evilzod_card");
            // R10 back on the main menu
            m.ProofFactionPointer(Away); yield return Real(1f);
            m.ProofFactionBack(); yield return Real(2.5f);
            Check($"R10 back on the main menu: the renders' own materials gone {(!m.ProofFigureFx(0) && !m.ProofFigureFx(1))}, the rig closed {(!m.ProofRig)}", !m.ProofFigureFx(0) && !m.ProofFigureFx(1) && !m.ProofRig);
            yield return SetSize(1920, 1080);
            if (grab) Destroy(grab);
            Finish();
        }

        void Renders(OpenZMainMenu m, OzFactionPosters rig, string tag)
        {
            int wantH = Mathf.Clamp(Mathf.RoundToInt(OzFaction.PosterH * Screen.height / OzLayout.RefH * 1.04f), 256, 2048), wantW = Mathf.RoundToInt(wantH * OzFaction.PosterAspect);
            var want = OzFactionPosters.RtSize(wantW, wantH);
            bool rts = true; string sizes = "";
            for (int i = 0; i < 2; i++)
            {
                var rt = rig ? rig.P[i].rt : null;
                rts &= rt && rt.width == want.x && rt.height == want.y && rt.antiAliasing == 1 && rt.useMipMap && rt.autoGenerateMips && rt.filterMode == FilterMode.Trilinear && rt.mipmapCount > 3;
                sizes += (i > 0 ? ", " : "") + (rt ? rt.width + " x " + rt.height + " (" + rt.mipmapCount + " mipmaps, MSAA " + rt.antiAliasing + ")" : "none");
            }
            var f0 = m.ProofFigure(0); var f1 = m.ProofFigure(1); var x0 = m.ProofFigureFx(0); var x1 = m.ProofFigureFx(1);
            bool mats = f0 && f1 && x0 && x1 && f0.material == x0 && f1.material == x1 && x0.GetFloat(OzFactionPolish.IdFxMode) == OzFactionPolish.FxGlint && x1.GetFloat(OzFactionPolish.IdFxMode) == OzFactionPolish.FxGlow;
            bool uv = f0 && f1 && f0.uvRect == OzFactionPolish.FigureUv(0) && f1.uvRect == OzFactionPolish.FigureUv(1);
            bool under = true;
            for (int i = 0; i < 2; i++)
            {
                var u = m.ProofPosterUnder(i); var d = m.ProofDossier(i); var sh = m.ProofPosterShadow(i); var gl = m.ProofPosterGlow(i);
                under &= u && d && u.parent == d.parent && u.GetSiblingIndex() < d.GetSiblingIndex() && sh && sh.transform.parent == u && (!gl || gl.transform.parent == u)
                         && (u.localScale - m.ProofPoster(i).localScale).sqrMagnitude < 1e-6f;
            }
            Check($"{tag} the renders supersampled: {sizes} ({want.x} x {want.y} wanted: {OzFactionPosters.Supersample:0} x the poster's {wantW} x {wantH} px) {rts}; each on its own material (Zod's glint {x0 && x0.GetFloat(OzFactionPolish.IdFxMode) == 1f}, EvilZod's glow {x1 && x1.GetFloat(OzFactionPolish.IdFxMode) == 2f}) {mats}; "
                  + $"EvilZod's render x {OzFactionPolish.EvilScale:0.00} about the band (uv {(f1 ? f1.uvRect.ToString("0.0000") : "-")}) {uv}; the posters' shadow and glow under the cards, at the posters' scale {under}", rts && mats && uv && under);
        }

        IEnumerator Smooth(OpenZMainMenu m, OzFactionPosters rig)
        {
            yield return new WaitForEndOfFrame();
            var lines = new List<string>(); bool ok = true;
            for (int i = 0; i < 2; i++)
            {
                var rt = rig.P[i].rt; var px = ReadBack(rt); if (px == null) { ok = false; continue; }
                int w = rt.width, h = rt.height, W = w / 2, H = h / 2;
                var avg = new float[W * H]; var one = new float[W * H];
                for (int y = 0; y < H; y++) for (int x = 0; x < W; x++)
                {
                    int a = (2 * y) * w + 2 * x;
                    avg[y * W + x] = (px[a].a + px[a + 1].a + px[a + w].a + px[a + w + 1].a) / (4f * 255f);
                    one[y * W + x] = px[a].a / 255f;
                }
                float sa = EdgeShare(avg, W, H), so = EdgeShare(one, W, H);
                // the GPU's own mipmap 1 (what the screen samples): made (its coverage as the 2 x 2 average's) and smooth itself
                var m1 = ReadMip1(rt, out int mw, out int mh);
                float gpuEdge = -1f, mean0 = 0f, mean1 = 0f;
                bool gpu = m1 != null && mw == W && mh == H;
                if (gpu)
                {
                    var g = new float[W * H]; double s0 = 0, s1 = 0;
                    for (int k = 0; k < g.Length; k++) { g[k] = m1[k].a / 255f; s0 += avg[k]; s1 += g[k]; }
                    mean0 = (float)(s0 / g.Length); mean1 = (float)(s1 / g.Length); gpuEdge = EdgeShare(g, W, H);
                    gpu = mean1 > 0.02f && Mathf.Abs(mean1 - mean0) < 0.01f && gpuEdge > 0.5f;
                }
                ok &= sa > 0.5f && sa > so + 0.3f && gpu;
                lines.Add($"{rig.P[i].id}: {sa * 100f:0} % of its silhouette's edge pixels partly covered (sampled one texel in four: {so * 100f:0} %); "
                          + (m1 == null ? "its GPU mipmap 1 could not be read" : $"its GPU mipmap 1 ({mw} x {mh}) covers {mean1 * 100f:0.0} % (the 2 x 2 average {mean0 * 100f:0.0} %), {gpuEdge * 100f:0} % of its edge pixels partly covered {gpu}"));
            }
            Check("R2 smooth edges (each render read back, averaged 2 x 2 as the screen samples its first mipmap, and that mipmap as the GPU made it): " + string.Join("; ", lines), ok);
        }

        /// <summary>A render texture's mipmap 1 as the GPU made it (the level the screen samples), read back; null when it has none.</summary>
        static Color32[] ReadMip1(RenderTexture rt, out int mw, out int mh)
        {
            mw = mh = 0;
            if (!rt || !rt.useMipMap || rt.mipmapCount < 2 || SystemInfo.copyTextureSupport == UnityEngine.Rendering.CopyTextureSupport.None) return null;
            mw = Mathf.Max(1, rt.width >> 1); mh = Mathf.Max(1, rt.height >> 1);
            var tmp = new RenderTexture(mw, mh, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { useMipMap = false, autoGenerateMips = false };
            tmp.Create();
            Graphics.CopyTexture(rt, 0, 1, tmp, 0, 0);
            var prev = RenderTexture.active; RenderTexture.active = tmp;
            var tex = new Texture2D(mw, mh, TextureFormat.RGBA32, false, false);
            tex.ReadPixels(new Rect(0, 0, mw, mh), 0, 0); tex.Apply(false);
            RenderTexture.active = prev;
            var px = tex.GetPixels32(); Destroy(tex);
            tmp.Release(); Destroy(tmp);
            return px;
        }

        /// <summary>Of the edge pixels (a 4-neighbour on the other side of half coverage), the share partly covered (0.08 .. 0.92).</summary>
        static float EdgeShare(float[] a, int W, int H)
        {
            int edge = 0, part = 0;
            for (int y = 1; y < H - 1; y++) for (int x = 1; x < W - 1; x++)
            {
                float v = a[y * W + x]; bool inside = v >= 0.5f;
                bool e = (a[y * W + x + 1] >= 0.5f) != inside || (a[y * W + x - 1] >= 0.5f) != inside || (a[(y + 1) * W + x] >= 0.5f) != inside || (a[(y - 1) * W + x] >= 0.5f) != inside;
                if (!e) continue;
                edge++; if (v > 0.08f && v < 0.92f) part++;
            }
            return edge > 0 ? part / (float)edge : 0f;
        }

        static Color32[] ReadBack(RenderTexture rt)
        {
            if (!rt) return null;
            var tmp = RenderTexture.GetTemporary(rt.width, rt.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(rt, tmp);
            var prev = RenderTexture.active; RenderTexture.active = tmp;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply(false);
            RenderTexture.active = prev; RenderTexture.ReleaseTemporary(tmp);
            var px = tex.GetPixels32(); Destroy(tex);
            return px;
        }

        IEnumerator Keys(OpenZMainMenu m, string tag)
        {
            yield return null;
            var root = m.ProofFactionRoot;
            var keys = root ? root.GetComponentsInChildren<Graphic>(true).FirstOrDefault(g => g.name == "Keys" || (g.transform.parent && g.transform.parent.name == "Keys")) : null;
            Rect kr = new Rect(); string how = "not found";
            if (keys is TMP_Text t)
            {
                t.ForceMeshUpdate();
                var b = t.textBounds;
                Vector3 a = t.transform.TransformPoint(b.min), c = t.transform.TransformPoint(b.max);
                kr = Rect.MinMaxRect(Mathf.Min(a.x, c.x), Mathf.Min(a.y, c.y), Mathf.Max(a.x, c.x), Mathf.Max(a.y, c.y)); how = "TextMeshPro, " + t.textInfo.lineCount + " line(s), \"" + t.text + "\"";
            }
            else if (keys is Text l)
            {
                var r = ScreenRect(l.rectTransform); float pw = l.preferredWidth * l.rectTransform.lossyScale.x;
                kr = l.alignment == TextAnchor.UpperRight || l.alignment == TextAnchor.MiddleRight || l.alignment == TextAnchor.LowerRight ? Rect.MinMaxRect(r.xMax - pw, r.yMin, r.xMax, r.yMax) : new Rect(r.x, r.y, pw, r.height);
                how = "plain text";
            }
            var back = m.ProofFactionBackRt ? ScreenRect(m.ProofFactionBackRt) : new Rect();
            Rect p0 = ScreenRect(m.ProofPoster(0)), p1 = ScreenRect(m.ProofPoster(1));
            var title = root ? root.GetComponentsInChildren<Graphic>(true).FirstOrDefault(g => g.name == "Title" || (g.transform.parent && g.transform.parent.name == "Title")) : null;
            var tr = title ? ScreenRect(title.rectTransform) : new Rect();
            var s = new Rect(0, 0, Screen.width, Screen.height);
            bool inside = kr.width > 10f && s.Contains(kr.min) && s.Contains(kr.max);
            bool clear = !kr.Overlaps(back) && !kr.Overlaps(p0) && !kr.Overlaps(p1) && !kr.Overlaps(tr) && kr.yMin > Mathf.Max(p0.yMax, p1.yMax) - 1f;
            Check($"{tag} the key hints row ({how}): {kr.xMin:0}..{kr.xMax:0} x {kr.yMin:0}..{kr.yMax:0} px, inside the screen {inside}, clear of BACK ({back.xMin:0}..{back.xMax:0} x {back.yMin:0}..{back.yMax:0}), the title and the posters (their tops {Mathf.Max(p0.yMax, p1.yMax):0}) {clear}", keys && inside && clear);
            yield return Grab();
            Crop("keys_row", new Rect(kr.xMin - 20f, kr.yMin - 30f, kr.width + 40f, kr.height + 70f), 2);
        }

        IEnumerator Zod(OpenZMainMenu m)
        {
            float l0 = LogNow; int ticks0 = OpenZMainMenu.FactionRowTicks;
            var r0 = ScreenRect(m.ProofPoster(0));
            m.ProofFactionPointer(new Vector2(r0.center.x, r0.center.y + r0.height * 0.2f));
            var fx = m.ProofFigureFx(0); var fig = m.ProofFigure(0);
            // R4 a glint pass: its band from before the left lens to past the right one; the lenses' light mid-pass against between passes
            float lo = 9f, hi = -9f, t0 = Time.realtimeSinceStartup; bool shotMid = false, shotOff = false; float lensOn = 0f, lensOff = 0f, ringOn = 0f, ringOff = 0f; Rect glasses = new Rect();
            while (Time.realtimeSinceStartup - t0 < OzFactionPolish.GlintPeriod * 1.6f && !(shotMid && shotOff))
            {
                yield return null;
                var g = fx.GetVector(OzFactionPolish.IdGlint);
                if (g.x > -1f) { lo = Mathf.Min(lo, g.x); hi = Mathf.Max(hi, g.x); }
                if (!shotMid && g.x > 0.15f && g.x < 0.35f)
                {
                    yield return Grab();
                    Spots(fx, out var c0, out var m0, out var c1, out _);
                    glasses = SpotRect(fig, c0, c1, 60f);
                    lensOn = MeanIn(fig, glasses, c0, m0, 0f, 0.8f); ringOn = MeanIn(fig, glasses, c0, m0, 1.2f, 1.7f);
                    Crop("zod_glint_mid", glasses, 4); shotMid = true;
                    yield return Shot("hover_zod_glint");
                }
                else if (!shotOff && g.x < -1f && Time.realtimeSinceStartup - t0 > 1.5f)
                {
                    yield return Grab();
                    Spots(fx, out var c0, out var m0, out var c1, out _);
                    var r = SpotRect(fig, c0, c1, 60f);
                    lensOff = MeanIn(fig, r, c0, m0, 0f, 0.8f); ringOff = MeanIn(fig, r, c0, m0, 1.2f, 1.7f);
                    Crop("zod_glint_off", r, 4); shotOff = true;
                }
            }
            float k = lensOff > 0f ? lensOn / lensOff : 0f, kr = ringOff > 0f ? ringOn / ringOff : 0f;
            Check($"R4 the glint across Zod's glasses: its band ran from {lo:0.00} to {hi:0.00} (before the left lens .. past the right one: under 0, over 1); the left lens {k:0.00} x as light mid-pass as between passes (over 1.3), the face round it {kr:0.00} x (0.85 .. 1.15)",
                  lo < 0f && hi > 1f && k > 1.3f && kr > 0.85f && kr < 1.15f);
            // R5 his card: the sounds, the rows
            yield return Until(() => m.ProofDossierState(0) == "Shown", 3f); yield return Real(0.6f);
            var ins = Played(l0, OzFactionDeck.SoundIn); var rows = Played(l0, OzFactionPolish.SoundRow);
            var paper = Played(l0, "ui_paper_slide").Count + Played(l0, "ui_paper_back").Count;
            float gap = rows.Count > 1 ? rows.Zip(rows.Skip(1), (a, b) => b.t - a.t).Min() : 9f;
            Check($"R5 Zod's card out: the sound log has {OzFactionDeck.SoundIn} {ins.Count} x, {OzFactionPolish.SoundRow} {rows.Count} x (the menu's count {OpenZMainMenu.FactionRowTicks - ticks0}; the closest two {gap * 1000f:0} ms apart), k75a3's paper sounds {paper} x",
                  ins.Count == 1 && rows.Count == OzFactionDeck.Rows && OpenZMainMenu.FactionRowTicks - ticks0 == OzFactionDeck.Rows && gap >= 0.039f && paper == 0);
            Rows(m, 0, "R5");
            yield return Shot("hover_zod_card");
            yield return Grab();
            var cr = ScreenRect(m.ProofDossier(0));
            Crop("zod_card", cr, 2);
        }

        /// <summary>The card's rows: Courier Prime, the same rows on two lines as before, none cut.</summary>
        void Rows(OpenZMainMenu m, int side, string tag)
        {
            var deck = m.ProofDeck;
            bool font = deck && deck.Body && deck.Body.fontNames != null && deck.Body.fontNames.Contains("Courier Prime") && deck.Bold && deck.Bold.fontNames.Contains("Courier Prime");
            var lines = new List<string>(); bool same = true, cut = false;
            var root = m.ProofDossier(side);
            var words = root ? root.GetComponentsInChildren<TMP_Text>(true).Where(t => t.name == "Words").ToList() : new List<TMP_Text>();
            for (int r = 0; r < words.Count && r < OzFactionDeck.Rows; r++)
            {
                var t = words[r]; t.ForceMeshUpdate();
                int want = (side == 0 && (r == 1 || r == 4)) || (side == 1 && r == 0) ? 2 : 1;
                same &= t.textInfo.lineCount == want;
                var rt = (RectTransform)t.transform;
                if (t.GetPreferredValues(t.text, rt.rect.width, 0f).x > rt.rect.width + 1f || t.isTextOverflowing) cut = true;
                lines.Add(Plain(t.text).Replace("\n", " / ") + " (" + t.textInfo.lineCount + ")");
            }
            Check($"{tag} {(side == 0 ? "Zod's" : "EvilZod's")} rows in Courier Prime {font} ({(deck && deck.BodyTmp ? "TextMeshPro" : "plain text")}): {string.Join("; ", lines)}; the same rows on two lines as before {same}; none cut {!cut}",
                  font && words.Count == OzFactionDeck.Rows && same && !cut);
        }

        IEnumerator Evil(OpenZMainMenu m)
        {
            float l0 = LogNow; int ticks0 = OpenZMainMenu.FactionRowTicks;
            var r1 = ScreenRect(m.ProofPoster(1));
            m.ProofFactionPointer(new Vector2(r1.center.x, r1.center.y + r1.height * 0.2f));
            yield return Until(() => m.ProofDossierState(1) == "Shown", 3f); yield return Real(0.6f);
            var outs = Played(l0, OzFactionDeck.SoundOut); var ins = Played(l0, OzFactionDeck.SoundIn); var rows = Played(l0, OzFactionPolish.SoundRow);
            Check($"R6 the pointer to EvilZod: {OzFactionDeck.SoundOut} {outs.Count} x (Zod's card back), {OzFactionDeck.SoundIn} {ins.Count} x, {OzFactionPolish.SoundRow} {rows.Count} x (the menu's count {OpenZMainMenu.FactionRowTicks - ticks0})",
                  outs.Count == 1 && ins.Count == 1 && rows.Count == OzFactionDeck.Rows);
            Rows(m, 1, "R6");
            yield return Shot("hover_evilzod_card");
            // the eyes' glow over a pulse
            var fx = m.ProofFigureFx(1); var fig = m.ProofFigure(1);
            float lo = 9f, hi = -9f, step = 0f, prev = fx.GetVector(OzFactionPolish.IdEyeGlow).x, t0 = Time.realtimeSinceStartup;
            float haloLo = 0f, haloHi = 0f; bool gotLo = false, gotHi = false;
            while (Time.realtimeSinceStartup - t0 < OzFactionPolish.GlowPeriod * 1.6f)
            {
                yield return null;
                float k = fx.GetVector(OzFactionPolish.IdEyeGlow).x;
                lo = Mathf.Min(lo, k); hi = Mathf.Max(hi, k); step = Mathf.Max(step, Mathf.Abs(k - prev)); prev = k;
                bool low = !gotLo && k < OzFactionPolish.GlowBase + 0.04f, high = !gotHi && k > 0.96f;
                if (low || high)
                {
                    yield return Grab();
                    Spots(fx, out var c0, out var m0, out var c1, out _);
                    var r = SpotRect(fig, c0, c1, 45f);
                    float halo = MeanIn(fig, r, c0, m0, 1.4f, 4f);
                    if (low) { haloLo = halo; gotLo = true; Crop("evil_eyes_low", r, 4); } else { haloHi = halo; gotHi = true; Crop("evil_eyes_high", r, 4); yield return Shot("hover_evilzod_glow_high"); }
                }
            }
            float kh = haloLo > 0f ? haloHi / haloLo : 0f;
            Check($"R6 EvilZod's eyes glow: the pulse {lo:0.00} .. {hi:0.00} ({OzFactionPolish.GlowBase:0.00} .. 1 wanted), the largest step in a frame {step:0.000} (smooth); round the eyes {kh:0.00} x as light at its high as at its low (over 1.1)",
                  lo < OzFactionPolish.GlowBase + 0.05f && hi > 0.95f && step < 0.05f && gotLo && gotHi && kh > 1.1f);
        }

        IEnumerator Cape(OpenZMainMenu m, OzFactionPosters rig)
        {
            var fx = m.ProofFigureFx(1); var fig = m.ProofFigure(1);
            var f = fx.GetVector(OzFactionPolish.IdFill);
            // the gaps in the render's strip above the band (the torn ends), and what the fill leaves of them
            yield return new WaitForEndOfFrame();
            var rt = rig.P[1].rt; var px = ReadBack(rt);
            int gaps = 0, left = 0;
            if (px != null)
            {
                int w = rt.width, h = rt.height, yb = Mathf.RoundToInt(f.x * h), ys = Mathf.RoundToInt(f.y * h), xr = Mathf.RoundToInt(f.z * w);
                for (int x = 0; x < xr; x++)
                {
                    float sa = px[Mathf.Clamp(ys, 0, h - 1) * w + x].a / 255f; if (sa < 0.5f) continue;
                    bool gap = false, gapAfter = false;
                    for (int y = Mathf.Max(0, yb); y < Mathf.Min(h, ys); y++)
                    {
                        float a = px[y * w + x].a / 255f;
                        if (a < 0.5f) gap = true;
                        if (a + sa * (1f - a) < 0.5f) gapAfter = true;
                    }
                    if (gap) gaps++; if (gapAfter) left++;
                }
            }
            Check($"R7 EvilZod's cape: the fill on (strip {f.x:0.000}..{f.y:0.000}, to {f.z:0.000}); in the render {gaps} columns of the strip have gaps above the band (its torn ends, the print showing between them), after the fill {left}", f.w > 0f && px != null && left == 0);
            var r1 = ScreenRect(m.ProofPoster(1));
            string[] names = { "evil_cape_left", "evil_cape_middle", "evil_cape_right" }; float[] at = { 0.08f, 0.5f, 0.92f };
            for (int q = 0; q < 3; q++)
            {
                m.ProofFactionPointer(new Vector2(r1.xMin + r1.width * at[q], r1.center.y)); yield return Real(1.2f);
                yield return Grab();
                Vector2 a = UvToScreen(fig, new Vector2(0f, f.x)), b = UvToScreen(fig, new Vector2(f.z, f.y));
                Crop(names[q], Rect.MinMaxRect(a.x - 4f, a.y - 30f, b.x + 40f, b.y + 50f), 3);
            }
        }

        IEnumerator Shade(OpenZMainMenu m)
        {
            var r1 = ScreenRect(m.ProofPoster(1));
            m.ProofFactionPointer(new Vector2(r1.center.x, r1.center.y)); yield return Until(() => m.ProofDossierState(1) == "Shown", 3f); yield return Real(0.8f);
            var sh = m.ProofCardShade(1); var sr = sh ? ScreenRect(sh.rectTransform) : new Rect();
            float k = Screen.height / OzLayout.RefH;
            bool light = sh && sh.enabled && Mathf.Abs(sh.color.a - OzFactionPolish.ShadeAlpha) < 0.02f && sr.width <= (OzFactionPolish.ShadeW + 1f) * k * 1.06f;
            yield return Grab();
            // the card's paper just beside the poster's edge against the paper further in, along the rows (rows' middle band)
            var card = ScreenRect(m.ProofDossier(1)); float x0 = ScreenRect(m.ProofPoster(1)).xMax;
            float near = Mean(new Rect(x0 + 2f * k, card.yMin + card.height * 0.25f, 8f * k, card.height * 0.45f)), far = Mean(new Rect(card.center.x + card.width * 0.25f, card.yMin + card.height * 0.25f, 8f * k, card.height * 0.45f));
            float ratio = far > 0f ? near / far : 0f;
            Check($"R8 the poster's shade on EvilZod's card: {(sh ? sh.color.a : 0f):0.00} black, {sr.width:0.0} px wide ({OzFactionPolish.ShadeAlpha:0.00}, {OzFactionPolish.ShadeW:0} u wanted) {light}; the paper beside the poster's edge {ratio:0.00} x as light as further in (over 0.8; k75a3: the hard shadow and the glow)",
                  light && ratio > 0.8f);
            Crop("evil_card_edge", new Rect(x0 - 30f * k, card.yMin, 120f * k, card.height), 3);
        }

        float Mean(Rect r)
        {
            if (!grab) return 0f;
            double s = 0; int n = 0;
            for (int y = Mathf.Max(0, (int)r.yMin); y < Mathf.Min(grab.height, (int)r.yMax); y++)
                for (int x = Mathf.Max(0, (int)r.xMin); x < Mathf.Min(grab.width, (int)r.xMax); x++) { var p = grab.GetPixel(x, y).linear; s += 0.2126 * p.r + 0.7152 * p.g + 0.0722 * p.b; n++; }
            return n > 0 ? (float)(s / n) : 0f;
        }
    }
}
#endif
