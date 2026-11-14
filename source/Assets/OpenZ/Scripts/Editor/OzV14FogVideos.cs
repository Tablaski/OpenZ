// OpenZ (GPL-3.0) - version 1.4, FOG OF WAR checks (editor only): the owner's fog opening videos, 12 looks out of one run.
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using OpenZ.Sim;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace OpenZ
{
    /// <summary>
    /// Version 1.4 FOG OF WAR, the owner's opening videos (5 Nov, editor only, nothing in the game changes): the reveal clip of one planet
    /// and style (the same spot, unit, move order and start as Reveal Clips in OzV14FogOfWarCheck) recorded ONCE at a fixed 60 fps, and every
    /// recorded frame drawn 12 times inside that one frame (colour TODAY / LAND / AIR / DUSK x deep fog 100 / 90 / 85 %), only the cloud's
    /// colour and cover changed on the fog's material between the draws: the game does not advance between them (one game frame, one moment).
    /// The 12 looks stream down one pipe to ONE ffmpeg with 12 H.264 outputs (yuv420p, +faststart, no audio, below-normal priority, about
    /// 0.8 GB). The main camera is drawn again to the
    /// game view per look (method "screen": the HUD included, as the player sees it) or into a render texture (method "rt": no HUD). The game's
    /// random state is put back before every draw and after the last, so the extra draws change nothing in the match. The fog's own look on
    /// screen stays today's (the palettes' compiled multipliers; L16 / L07 get theirs for the run only and are put back after).
    /// </summary>
    public static class OzV14FogVideos
    {
        /// <summary>The reveal clip's spec, "level|scene|camX|camZ|unit|startX|startZ|toX|toZ|orderFrame|frames|fps|crf" (OzV14FogOfWarCheck.ClipSpec).</summary>
        public static string Spec = "";
        /// <summary>The style (0 CLOUD SEA, 1 CLOUD BANKS, 2 MIST LAYERS).</summary>
        public static int Style = 0;
        /// <summary>The output folder: fogvid_L17_S1_TODAY_100.mp4 ...</summary>
        public static string OutDir = "Logs/fog_videos";
        /// <summary>The clip frames written: [From, To) of the reveal clip's 60 fps frames (120 .. 660 = 2.0 .. 11.0 s, the order at 2.5 s).</summary>
        public static int From = 120, To = 660;
        /// <summary>Test: > 0 records only that many frames from From (files *_test.mp4).</summary>
        public static int TestFrames = 0;
        public static string Ffmpeg = "ffmpeg";
        public static string Encoder = "-c:v libx264 -preset veryfast -tune zerolatency -crf 18 -x264-params keyint=600:min-keyint=600:scenecut=0:ipratio=1.0 -threads 1";   // one I-frame: a mid-clip I-frame made the cloud jump then hold a frame
        /// <summary>"screen" (the HUD included) or "rt" (into a render texture, no HUD).</summary>
        public static string Method = "screen";
        /// <summary>Optional colour multipliers for the run, "LAND r g b|AIR r g b|DUSK r g b" (linear; Colour Grid calibrate's FIT line).</summary>
        public static string Tints = "";
        /// <summary>Proof frames (relative to From): the 12 looks saved as PNG and compared where the fog is clear.</summary>
        public static int[] ProofAt = { 0, 180, 360, 539 };
        public static string Status = "idle";

        const string Tag = "[OpenZ V1.4 FogVideos]";
        static readonly string[] CN = { "TODAY", "LAND", "AIR", "DUSK" };
        static readonly float[] Lv = { 1f, 0.90f, 0.85f };
        static readonly string[] LN = { "100", "90", "85" };
        static readonly int IdTop = Shader.PropertyToID("_OzFowTop"), IdShade = Shader.PropertyToID("_OzFowShade"), IdRim = Shader.PropertyToID("_OzFowRim"),
                            IdMistTint = Shader.PropertyToID("_OzFowMistTint"), IdSee = Shader.PropertyToID("_OzFowSee"), IdDebug = Shader.PropertyToID("_OzFowDebug");
        // the HUD boxes at 1920 x 1080 (x, y from the top left, w, h): top bar and toast line, MENU, groups row, unit panel, minimap
        static readonly RectInt[] Hud = { new RectInt(565, 0, 791, 162), new RectInt(1785, 8, 135, 50), new RectInt(10, 995, 725, 75), new RectInt(1075, 870, 490, 200), new RectInt(1565, 700, 355, 380) };

        static readonly List<string> lines = new List<string>();
        static void Say(string s) { lines.Add(s); Debug.Log(s); }
        static int pass, total;
        static void Row(string id, bool ok, string text) { total++; if (ok) pass++; Say($"  {(ok ? "PASS" : "FAIL")} {id} {text}"); }

        // ---------------------------------------------------------------- the editor-stepped routine (as OzV14FogOfWarCheck.Run)
        static IEnumerator running;
        static readonly Stack<IEnumerator> nest = new Stack<IEnumerator>();
        static void Run(IEnumerator it)
        {
            if (!EditorApplication.isPlaying) { Debug.LogWarning($"{Tag} press Play first"); return; }
            if (running != null) { Debug.LogWarning($"{Tag} already running"); return; }
            running = it; nest.Clear(); nest.Push(it); EditorApplication.update += Step;
        }
        static void Step()
        {
            bool more = false;
            try
            {
                while (EditorApplication.isPlaying && nest.Count > 0)
                {
                    var top = nest.Peek();
                    if (!top.MoveNext()) { nest.Pop(); continue; }
                    if (top.Current is IEnumerator inner) { nest.Push(inner); continue; }
                    more = true; break;
                }
            }
            catch (System.Exception e) { Debug.LogError($"{Tag} stopped: {e}"); Status = "FAILED " + e.Message; more = false; }
            if (!more) { EditorApplication.update -= Step; running = null; nest.Clear(); CloseAll(); }
        }
        static IEnumerator Frames(int n) { int f = Time.frameCount + n; while (Time.frameCount < f) yield return null; }
        static float Fl(string s) => float.Parse(s, System.Globalization.CultureInfo.InvariantCulture);

        [MenuItem("OpenZ/V1.4/Fog of War: Opening Videos (in Play, one planet and style, 12 looks)")]
        public static void Videos() { Status = "starting"; Run(Routine()); }

        // ---------------------------------------------------------------- the encoder (5 Nov: ONE ffmpeg for the 12 looks, about 0.8 GB, instead of
        // 12 processes of 0.18-0.33 GB each: the frame's 12 looks go down one pipe as one tall frame, split and cropped into 12 H.264 outputs)
        sealed class Enc
        {
            public string[] paths; public Process ff; public Stream stdin; public Thread th; public BlockingCollection<byte[][]> q; public int written; public string err = "";
            public readonly StringBuilder log = new StringBuilder();
        }
        static Enc enc;
        static readonly ConcurrentBag<byte[]> pool = new ConcurrentBag<byte[]>();

        static Enc Open(string[] paths, string pix, int w, int h, int fps)
        {
            int n = paths.Length;
            var e = new Enc { paths = paths, q = new BlockingCollection<byte[][]>(2) };
            // the looks are written last to first, each with its rows bottom up: flipped, the tall frame holds look i at rows i * h .. (i + 1) * h - 1
            var fc = new StringBuilder($"[0:v]vflip,split={n}");
            for (int i = 0; i < n; i++) fc.Append($"[s{i}]");
            for (int i = 0; i < n; i++) fc.Append($";[s{i}]crop={w}:{h}:0:{i * h}[o{i}]");
            var args = new StringBuilder($"-y -loglevel error -thread_queue_size 1 -f rawvideo -pix_fmt {pix} -s {w}x{h * n} -r {fps} -i - -filter_complex \"{fc}\"");
            for (int i = 0; i < n; i++) args.Append($" -map [o{i}] -an {Encoder} -pix_fmt yuv420p -movflags +faststart \"{paths[i]}\"");
            var psi = new ProcessStartInfo(Ffmpeg, args.ToString()) { UseShellExecute = false, RedirectStandardInput = true, RedirectStandardError = true, RedirectStandardOutput = true, CreateNoWindow = true };
            e.ff = Process.Start(psi);
            try { e.ff.PriorityClass = ProcessPriorityClass.BelowNormal; } catch { }
            e.ff.ErrorDataReceived += (s, a) => { if (!string.IsNullOrEmpty(a.Data)) lock (e.log) e.log.Append(a.Data).Append(' '); };
            e.ff.OutputDataReceived += (s, a) => { };
            e.ff.BeginErrorReadLine(); e.ff.BeginOutputReadLine();
            e.stdin = e.ff.StandardInput.BaseStream;
            e.th = new Thread(() =>
            {
                try
                {
                    foreach (var fr in e.q.GetConsumingEnumerable())
                    {
                        for (int i = fr.Length - 1; i >= 0; i--) e.stdin.Write(fr[i], 0, fr[i].Length);
                        e.written++;
                        foreach (var b in fr) pool.Add(b);
                    }
                }
                catch (System.Exception x) { e.err = x.Message; }
                try { e.stdin.Flush(); e.stdin.Close(); } catch { }
            }) { IsBackground = true, Name = "OZ fog videos encoder" };
            e.th.Start();
            return e;
        }

        static void CloseAll()
        {
            if (enc != null && !enc.q.IsAddingCompleted) enc.q.CompleteAdding();
        }

        // ---------------------------------------------------------------- the looks
        struct Look { public Vector4 top, shade, rim, mist; public float see; }
        static Vector4 Lin(Color c, float a) { var l = c.linear; return new Vector4(l.r, l.g, l.b, a); }
        static Vector4 Tn(Vector4 v, Vector3 t) => new Vector4(v.x * t.x, v.y * t.y, v.z * t.z, v.w);

        static void Put(Material m, Look k)
        {
            m.SetVector(IdTop, k.top); m.SetVector(IdShade, k.shade); m.SetVector(IdRim, k.rim); m.SetVector(IdMistTint, k.mist); m.SetFloat(IdSee, k.see);
        }

        static void ParkPointer()
        {
            var ms = UnityEngine.InputSystem.Mouse.current;
            if (ms != null && ms.position.ReadValue() != new Vector2(-4000f, -4000f)) UnityEngine.InputSystem.LowLevel.InputState.Change(ms.position, new Vector2(-4000f, -4000f));
        }

        // the frame's 12 draws (in the frame's end, after its own draw): returns the raw frames (rows bottom up) or null on a failure
        static RenderTexture rt;
        static Texture2D rtRead;
        static string pixFmt; static int capW, capH;

        static byte[] Grab(Camera cam)
        {
            if (Method == "rt")
            {
                if (!rt) { rt = new RenderTexture(capW, capH, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { name = "OZ_FogVideoRT", antiAliasing = 1 }; rt.Create(); }
                if (!rtRead) rtRead = new Texture2D(capW, capH, TextureFormat.RGBA32, false, false);
                var was = cam.targetTexture; cam.targetTexture = rt; cam.Render(); cam.targetTexture = was;
                var a = RenderTexture.active; RenderTexture.active = rt; rtRead.ReadPixels(new Rect(0, 0, capW, capH), 0, 0, false); RenderTexture.active = a;
                var d = rtRead.GetRawTextureData<byte>();
                if (!pool.TryTake(out var b) || b.Length != d.Length) b = new byte[d.Length];
                d.CopyTo(b); return b;
            }
            cam.Render();
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                var d = tex.GetRawTextureData<byte>();
                if (!pool.TryTake(out var b) || b.Length != d.Length) b = new byte[d.Length];
                d.CopyTo(b); return b;
            }
            finally { Object.Destroy(tex); }
        }

        static void SavePng(byte[] raw, string path)
        {
            var t = new Texture2D(capW, capH, pixFmt == "rgb24" ? TextureFormat.RGB24 : TextureFormat.RGBA32, false);
            t.LoadRawTextureData(raw); t.Apply(false); File.WriteAllBytes(path, t.EncodeToPNG()); Object.DestroyImmediate(t);
        }

        // ---------------------------------------------------------------- the run
        static IEnumerator Routine()
        {
            lines.Clear(); pass = total = 0;
            var p = Spec.Split('|');
            if (p.Length < 13) { Say($"{Tag} Spec needs 13 fields"); Status = "FAILED spec"; yield break; }
            string level = p[0], scene = p[1], unit = p[4];
            float cx = Fl(p[2]), cz = Fl(p[3]), sx = Fl(p[5]), sz = Fl(p[6]), tx = Fl(p[7]), tz = Fl(p[8]);
            int orderAt = int.Parse(p[9]), fps = int.Parse(p[11]);
            int s = Mathf.Clamp(Style, 0, OzFogOfWar.Looks.Length - 1);
            int to = TestFrames > 0 ? Mathf.Min(To, From + TestFrames) : To;
            string outDir = Path.IsPathRooted(OutDir) ? OutDir : Path.GetFullPath(Path.Combine(Application.dataPath, "..", OutDir));
            Directory.CreateDirectory(outDir);
            string tag = $"{level}_S{s + 1}", sfx = TestFrames > 0 ? "_test" : "";
            Say($"{Tag} Opening Videos: {Spec}, style {OzFogOfWar.Looks[s].Name}, frames {From}..{to} ({(to - From) / (float)fps:0.00} s), method {Method}, encoder '{Encoder}', tints '{Tints}'");
            Status = "loading " + level;

            // the reveal clip's start, as Reveal Clips makes it: the level loaded fresh, the briefing skipped, the computer off, the camera held
            OzFogOfWar.ColourIdea = OzFogOfWar.ColourToday; OzFogOfWar.Cover = 1f;
            Time.captureFramerate = 0; Time.timeScale = 1f;
            UnityEngine.SceneManagement.SceneManager.LoadScene(scene);
            yield return Frames(10);
            float t0 = Time.realtimeSinceStartup;
            while ((!ZGame.I || ZGame.I.World == null || !OzFogOfWar.I || ZGame.I.level != level) && Time.realtimeSinceStartup - t0 < 120f) yield return null;
            var g = ZGame.I; var f = OzFogOfWar.I;
            if (!g || g.World == null || !f) { Row("V1", false, "the level did not load: " + scene); Status = "FAILED load"; yield break; }
            t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 120f)
            {
                if (ZodBriefing.BriefingActive && Time.realtimeSinceStartup - t0 > 1f) ZodBriefing.ProofSkipBriefing();
                if (!ZodBriefing.BriefingActive && f.GateWhy() == null && Time.realtimeSinceStartup - t0 > 2f) break;
                yield return null;
            }
            g.World.Bots.Clear();
            OzFogOfWar.SessionLook = s;
            bool forced = f.SetForced(true, out var refused);
            var rts = Object.FindFirstObjectByType<RtsCamera>();
            if (!rts || !forced) { Row("V1", false, "no RTS camera or the fog refused: " + refused); Status = "FAILED fog"; yield break; }
            var cam = rts.GetComponent<Camera>();
            float edgeWas = rts.edgePx; rts.edgePx = 0f;
            System.Action hold = () => { rts.yaw = 0f; rts.focus = new Vector3(cx, rts.focus.y, cz); rts.SetHeightImmediate(70f); ParkPointer(); };
            hold();
            var W = g.World; var known = new HashSet<int>(); foreach (var o in W.Objects) if (o != null) known.Add(o.Id);
            string team = g.player == Team.Red ? "r" : "b";
            string staged = CaptureKit.StageBattle($"{team} {unit} {Mathf.RoundToInt(sx / 2f)} {Mathf.RoundToInt(-sz / 2f)}", 1f);
            ZObject lead = null;
            foreach (var o in W.Objects) { if (o == null || known.Contains(o.Id) || !o.IsUnit || o.Owner != g.player) continue; if (lead == null || o.Minions.Count > 0) lead = o; }
            if (lead == null) { rts.edgePx = edgeWas; Row("V1", false, "the unit was not staged: " + staged); Status = "FAILED stage"; yield break; }
            float tw = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - tw < 3f) { hold(); yield return null; }

            // the looks: today's palette numbers times each colour idea's multipliers (the planet's compiled ones, or Tints for this run)
            int planet = OzFogOfWar.PlanetIndex;
            var keep = OzFogOfWar.Palettes[planet];
            if (!string.IsNullOrEmpty(Tints))
                foreach (var part in Tints.Split('|'))
                {
                    var q = part.Trim().Split(' '); if (q.Length < 4) continue;
                    var v = new Vector3(Fl(q[1]), Fl(q[2]), Fl(q[3]));
                    if (q[0] == "LAND") OzFogOfWar.Palettes[planet].Land = v; else if (q[0] == "AIR") OzFogOfWar.Palettes[planet].Air = v; else if (q[0] == "DUSK") OzFogOfWar.Palettes[planet].Dusk = v;
                }
            var pal = OzFogOfWar.Palettes[planet];
            var looks = new Look[12]; var names = new string[12];
            var sbl = new StringBuilder();
            for (int c = 0; c < 4; c++)
            {
                var tn = OzFogOfWar.TintOf(planet, c);
                sbl.Append($"{CN[c]} ({tn.x:0.0000} {tn.y:0.0000} {tn.z:0.0000}) ");
                for (int k = 0; k < 3; k++)
                {
                    int i = c * 3 + k;
                    looks[i] = new Look { top = Tn(Lin(pal.Top, 1f), tn), shade = Tn(Lin(pal.Shade, 1f), tn), rim = Tn(Lin(pal.Rim, 1f), tn), mist = Tn(Lin(pal.MistTint, pal.MistTintK), tn), see = 1f - Lv[k] };
                    names[i] = $"fogvid_{level}_S{s + 1}_{CN[c]}_{LN[k]}";
                }
            }
            Say($"  planet {OzFogOfWar.PlanetNames[planet]}, multipliers {sbl}; cover 100 / 90 / 85 %");
            var mat = OzFogOfWar.Mat;
            if (!mat || !cam) { Row("V1", false, "no fog material or camera"); Status = "FAILED material"; OzFogOfWar.Palettes[planet] = keep; yield break; }

            // the run: the clip's fixed 60 fps step from frame 0 (the cues as Reveal Clips), the 12 looks drawn in the end of frames From .. to-1
            capW = Method == "rt" ? 1920 : Screen.width; capH = Method == "rt" ? 1080 : Screen.height;
            enc = null; pixFmt = null;
            string order = ""; var csv = new StringBuilder("clipFrame,time,frameCountBefore,frameCountAfter,timeAfter,leadX,leadZ,leadXAfter,leadZAfter,drawMs\n");
            var proof = new StringBuilder(); bool failed = false; string failWhy = "";
            var swAll = Stopwatch.StartNew(); double drawMsSum = 0; int drawn = 0;
            var pxT = ZGame.WorldToPx(new Vector3(tx, 0f, tz));
            bool done = false;
            Status = $"recording {tag}";
            IEnumerator Loop()
            {
                int capWas = Time.captureFramerate;
                Time.captureFramerate = fps;
                for (int i = 0; i < to && !failed; i++)
                {
                    try
                    {
                        hold();
                        if (i == orderAt && lead != null && !lead.Destroyed)
                            order = W.SendWaypoints(lead.Owner, lead, new List<ZWaypoint> { ZWaypoint.Move(pxT.x, pxT.y) }) ? "move order given" : "move order REFUSED";
                    }
                    catch (System.Exception e) { Debug.LogError($"{Tag} cue at frame {i}: {e}"); }
                    yield return new WaitForEndOfFrame();
                    if (i < From) continue;
                    try
                    {
                        int rel = i - From;
                        var cwB = g.CenterWorld(lead); int fcB = Time.frameCount; float tB = Time.time;
                        var rs = Random.state;
                        var sw = Stopwatch.StartNew();
                        bool isProof = System.Array.IndexOf(ProofAt, rel) >= 0;
                        var raws = new byte[12][];
                        for (int v = 0; v < 12; v++)
                        {
                            Random.state = rs;
                            Put(mat, looks[v]);
                            raws[v] = Grab(cam);
                        }
                        byte[] dbg = null, again = null;
                        if (isProof)
                        {   // the cloud's own opacity on that frame (debug 4) for the clear-ground mask, and TODAY 100 drawn once more (the noise floor:
                            // the camera's dithering takes the next blue-noise tile on every draw)
                            Random.state = rs; Put(mat, looks[0]); again = Grab(cam);
                            Random.state = rs; mat.SetFloat(IdDebug, 4f); dbg = Grab(cam); mat.SetFloat(IdDebug, OzFogOfWar.DebugMode);
                        }
                        Random.state = rs;
                        Put(mat, looks[0]);
                        double ms = sw.Elapsed.TotalMilliseconds; drawMsSum += ms; drawn++;
                        var cwA = g.CenterWorld(lead);
                        csv.Append(System.FormattableString.Invariant($"{i},{tB:0.0000},{fcB},{Time.frameCount},{Time.time:0.0000},{cwB.x:0.000},{cwB.z:0.000},{cwA.x:0.000},{cwA.z:0.000},{ms:0.0}\n"));
                        if (pixFmt == null)
                        {
                            int bpp = raws[0].Length / (capW * capH);
                            pixFmt = bpp == 3 ? "rgb24" : "rgba";
                            var paths = new string[12]; for (int v = 0; v < 12; v++) paths[v] = Path.Combine(outDir, names[v] + sfx + ".mp4");
                            enc = Open(paths, pixFmt, capW, capH, fps);
                            Say($"  capture {capW} x {capH} {pixFmt} ({raws[0].Length} bytes a look), one ffmpeg for the 12 outputs: '{Encoder}'");
                        }
                        if (isProof)
                        {
                            int bpp = pixFmt == "rgb24" ? 3 : 4;
                            var clear = new bool[capW * capH]; var fog = new bool[capW * capH]; int nc = 0, nf = 0;
                            for (int y = 0; y < capH; y++)
                                for (int x = 0; x < capW; x++)
                                {
                                    int idx = y * capW + x; int yt = capH - 1 - y; bool inHud = false;            // rows bottom up; the HUD boxes from the top
                                    foreach (var b in Hud) if (x >= b.xMin && x < b.xMax && yt >= b.yMin && yt < b.yMax) { inHud = true; break; }
                                    if (inHud) continue;
                                    byte o = dbg[idx * bpp];
                                    if (o <= 1) { clear[idx] = true; nc++; } else if (o >= 180) { fog[idx] = true; nf++; }     // (debug 4 after the grade: deep fog 190-210)
                                }
                            var pr = new StringBuilder($"    proof frame {i} ({i / (float)fps:0.00} s, frame count {fcB}, game time {tB:0.0000} s, lead at {cwB.x:0.00},{cwB.z:0.00}): {nc} clear-ground px, {nf} deep-fog px; vs TODAY 100:");
                            for (int v = 1; v <= 12; v++)
                            {
                                var bv = v < 12 ? raws[v] : again;
                                double dc = 0, df = 0; int over2 = 0;
                                for (int idx = 0; idx < clear.Length; idx++)
                                {
                                    if (!clear[idx] && !fog[idx]) continue;
                                    int o = idx * bpp; int d0 = System.Math.Abs(bv[o] - raws[0][o]), d1 = System.Math.Abs(bv[o + 1] - raws[0][o + 1]), d2 = System.Math.Abs(bv[o + 2] - raws[0][o + 2]);
                                    if (clear[idx]) { dc += d0 + d1 + d2; if (System.Math.Max(d0, System.Math.Max(d1, d2)) > 2) over2++; }
                                    else df += d0 + d1 + d2;
                                }
                                string nm = v < 12 ? $"{CN[v / 3]}_{LN[v % 3]}" : "TODAY_100 again (noise floor)";
                                pr.Append(System.FormattableString.Invariant($" {nm}: clear ground {dc / (3.0 * System.Math.Max(1, nc)):0.000} levels ({over2 * 100.0 / System.Math.Max(1, nc):0.000} % of px over 2), deep fog {df / (3.0 * System.Math.Max(1, nf)):0.0};"));
                            }
                            proof.AppendLine(pr.ToString());
                            string pd = Path.Combine(outDir, "proof", tag); Directory.CreateDirectory(pd);
                            for (int v = 0; v < 12; v++) SavePng(raws[v], Path.Combine(pd, $"{names[v]}_f{i}{sfx}.png"));
                            SavePng(dbg, Path.Combine(pd, $"opacity_{tag}_f{i}{sfx}.png"));
                            pool.Add(again); pool.Add(dbg);
                        }
                        enc.q.Add(raws);
                        if (rel % 60 == 0) Status = $"recording {tag}: frame {i} of {to}, {drawMsSum / System.Math.Max(1, drawn):0} ms for 12 draws";
                    }
                    catch (System.Exception e) { failed = true; failWhy = $"frame {i}: {e}"; Debug.LogError($"{Tag} {failWhy}"); }
                }
                Time.captureFramerate = capWas;
                done = true;
            }
            f.StartCoroutine(Loop());
            while (!done && EditorApplication.isPlaying) yield return null;
            double recS = swAll.Elapsed.TotalSeconds;
            Put(mat, looks[0]);
            OzFogOfWar.Palettes[planet] = keep;                                                           // the palette as compiled
            rts.edgePx = edgeWas;
            Status = $"encoding {tag}";
            CloseAll();
            float tc = Time.realtimeSinceStartup;
            while (enc != null && Time.realtimeSinceStartup - tc < 300f && (enc.th.IsAlive || !enc.ff.HasExited)) yield return null;
            int okFiles = 0; var sbe = new StringBuilder();
            if (enc != null)
            {
                bool exited = enc.ff.HasExited; int code = exited ? enc.ff.ExitCode : -1;
                if (!exited) { try { enc.ff.Kill(); } catch { } }
                bool okRun = exited && code == 0 && enc.written == to - From && string.IsNullOrEmpty(enc.err);
                sbe.Append($"ffmpeg exit {code}, {enc.written} frames piped{(okRun ? "" : $" FAILED {enc.err} {enc.log}")}: ");
                foreach (var path in enc.paths)
                {
                    long bytes = File.Exists(path) ? new FileInfo(path).Length : 0;
                    if (okRun && bytes > 0) okFiles++;
                    sbe.Append($"{Path.GetFileName(path)} {bytes / 1048576.0:0.0} MB; ");
                }
                enc.ff.Dispose();
            }
            double totS = swAll.Elapsed.TotalSeconds;
            Row("V1", !failed && order == "move order given", $"{OzFogOfWar.Looks[s].Name} {level}: {staged}; {lead.Type} #{lead.Id} {order} at frame {orderAt}; recorded frames {From}..{to - 1} ({drawn} frames x 12 draws, {drawMsSum / System.Math.Max(1, drawn):0} ms a frame for the 12 draws) in {recS:0} s, all encoded in {totS:0} s{(failed ? "; " + failWhy : "")}");
            Row("V2", okFiles == 12, $"{okFiles} of 12 files: {sbe}");
            Say(proof.ToString().TrimEnd());
            File.WriteAllText(Path.Combine(outDir, $"frames_{tag}{sfx}.csv"), csv.ToString());
            Say($"{Tag} {(pass == total ? "PASS" : "FAIL")} {pass}/{total}");
            File.WriteAllText(Path.Combine(outDir, $"log_{tag}{sfx}.txt"), string.Join("\n", lines) + "\n");
            Status = (pass == total ? "DONE " : "FAILED ") + tag;
            enc = null;
            OzFogOfWar.SessionLook = -1;
        }
    }
}
