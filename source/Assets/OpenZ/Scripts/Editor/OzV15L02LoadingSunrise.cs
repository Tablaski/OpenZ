#if !OPENZ_HARNESS
// OpenZ (GPL-3.0)
// the sunrise sky and sun. Editor only, never in a build.
using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// <para>
    /// brought to 2560 x 1600 with a Lanczos filter; its numbers were never written down. The kit solved them from the picture: the sim grid's
    /// roads (Resources/OpenZ/Maps/L02_grid.json) laid over the picture's asphalt, the windmill, sign posts and the waterfall-spring palm of
    /// Resources/OpenZ/DetailsV2/placement_L02.json as checks: OzStillCam.Pose((118, 42, -182) -> (52, 0, -62), vertical fov 48, roll 0)
    /// (yaw -28.81, pitch 17.05 down); road edges sit 1.0 px (median) from it, the six landmarks 3.1 px RMS (kit README).</para>
    /// <para>THE SKY AND SUN. Not a paint-over: the game's own fly-over sky. OzStillCam.Begin hands the view to the capture kit's cinematic
    /// mode, so View/FlybySky draws the level's painted dawn panorama, the HDR sun disc with its bloom halo and the sun's glow in the haze,
    /// exactly as the level-start fly-over does. FlybySky draws the disc at the key light's azimuth (at most 1.4 deg up) and turns the
    /// painting with it (Shaders/FlybySky.shader: the panorama's u is measured from the sun's azimuth). At the level start (DayNight
    /// top and no sun. For this capture only, SunTurn turns the key light about the vertical (its elevation, colour and strength kept), so
    /// the disc, the painted sky, the glow and every shadow turn together into the frame: three pictures with the disc on the left third,
    /// the centre and the right third of the sky band (row ~200 of 1600, above the far mesas, inside a 16:9 screen's crop). The menu's own
    /// desert sunrise (Resources/OpenZ/Menu/L02_preview.mp4) has its sun on the right third. Nothing of the game changes: the turn lives
    /// on a component of this run and is gone with it.</para>
    /// <para>THE RUN. In Play on the main menu: battlefield, level 2, red (the PLAY AS BLUE box off, put back after DEPLOY read it), the
    /// level-start briefing as a player has it (briefing on, no MAYHEM fog, no bot match, no test mode: for this run, put back at its end); waits (time capped) until the
    /// briefing has handed the camera back, holds the clock at the level-start sunrise (DayNight.LevelStartTod), switches the rock
    /// then shoots the OLD look (the game's own sun: the angle proof) and the three sunrise pictures, each 3840 x 2400 (MSAA + the game
    /// camera's post, OzStillCam.Shoot) and down-scaled here to 2560 x 1600 RGB PNG with Pillow's LANCZOS (OzL02SunrisePix.Downscale: on the
    /// cursor, briefing bars or screen marks. Then the angle proof against L02_loading_3.png: a red / cyan overlay, a 50 / 50 blend and the
    /// shift of the roads and water between the two (OzL02SunrisePix.BestShift). Everything goes to
    /// Resources: the lead installs the chosen picture after the owner's OK.</para>
    /// </summary>
    public static class OzV15L02LoadingSunrise
    {
        public const string Tag = "[OpenZ L02 sunrise loading]", Level = "L02";
        public const int W = 2560, H = 1600, Super = 3840, SuperH = 2400;
        /// <summary></summary>
        public static Vector3 Pos = new Vector3(118f, 42f, -182f), Look = new Vector3(52f, 0f, -62f);
        public static float Fov = 48f;
        /// <summary>The pictures: the old look (the game's own sun, NaN) and the drawn sun's azimuth (deg, atan2(x, z) toward the sun) that puts
        /// the disc on the left third / centre / right third line of the 2560 px picture (kit_tools/sun_plan.py).</summary>
        public static string[] Names = { "GAME_LOOK_same_pose" };
        public static float[] SunYaw = { float.NaN };
        /// <summary>
        /// (FlybySky's _FlybySunDir global: the painting, disc and sky glow; the key light, shadows and ground fog stay the game's), LookY = the look-at
        /// height (pitch raise), GlowK = scale of FlybySky.GlowWide / GlowGain for the shot, Tod = time of day held for the shot. NaN / missing = unchanged.</summary>
        public static float[] SkyYaw = { }, LookY = { }, GlowK = { }, Tod = { };
        static float At(float[] a, int i) => a != null && i < a.Length ? a[i] : float.NaN;
        /// <summary>Time caps (real seconds): the level start after DEPLOY, the briefing's hand-back, the fly-over sky's fade in, one shot.</summary>
        public static float LevelCapS = 120f, BriefingCapS = 300f, SkyCapS = 40f, ShotCapS = 120f;
        /// <summary>A level with no briefing (switched off elsewhere): how long to wait for one before going on.</summary>
        public static float NoBriefingS = 25f;
        /// <summary>Optional: let the match run this long (real s, the clock held at the sunrise) before the freeze. 0 = the level-start moment.</summary>
        public static float PlaySeconds = 0f;
        /// <summary>The first picture row (from the top) of the angle test: the ground under the far mesas.</summary>
        public static int AngleRowFrom = 450;
        public static string OutRoot = "";
        /// <summary>Play check: idle, going, waiting ..., shooting ..., done ..., or what stopped it.</summary>
        public static string Status = "idle";
        public static string LastDir = "";
        public const string OldPicture = "OpenZ/Resources/OpenZ/Loading/L02_loading_3.png";   // under Application.dataPath; read only

        /// <summary>The switches a player's start has (for this run only, each put back at the end): the level-start briefing on, no MAYHEM
        /// </summary>
        static readonly string[] HoldKeys = { ZodBriefing.PrefBriefing, "oz_mayhem", "oz_botmatch", "oz_testmode" };
        static readonly int[] HoldWant = { 1, 0, 0, 0 };
        static readonly int[] HoldDefault = { 1, 0, 0, 0 };
        static readonly int[] holdWas = { int.MinValue, int.MinValue, int.MinValue, int.MinValue };
        static bool boxWas, boxHeld;
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        [MenuItem("OpenZ/1.5/Loading/L02 sunrise loading picture (Play, main menu)", false, 7700)]
        static void MenuGo() => Go();

        [MenuItem("OpenZ/1.5/Loading/L02 sunrise loading picture: status", false, 7701)]
        static void MenuStatus() => Debug.Log(Tag + " " + Status + (LastDir.Length > 0 ? " -> " + LastDir : ""));

        static string Fail(string why) { Status = "stopped: " + why; Debug.LogError(Tag + " " + Status); return Status; }

        static bool Busy => Status == "going" || Status.StartsWith("waiting", StringComparison.Ordinal) || Status.StartsWith("shooting", StringComparison.Ordinal) || Status == "writing";

        /// <summary>Starts the run from the main menu (returns the status line).</summary>
        public static string Go()
        {
            if (!Application.isPlaying) return Fail("not in Play: start Play on the main menu first");
            if (Busy) return Fail("a run is already going (" + Status + ")");
            var m = UnityEngine.Object.FindAnyObjectByType<OpenZMainMenu>();
            if (!m) return Fail("no main menu: start Play on the main menu scene");
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urp && Mathf.Abs(urp.renderScale - 1f) > 1e-3f) return Fail("render scale " + urp.renderScale.ToString("0.00", Inv) + " on " + urp.name + ": the picture needs the full quality (render scale 1)");
            int card = Array.FindIndex(OzLevelDef.All, d => d.Id == Level);
            if (card < 0) return Fail("level 2 is not one of the menu's levels in this tree");
            if (!File.Exists(Path.Combine(Application.dataPath, OldPicture))) return Fail("Assets/" + OldPicture + " is missing (the angle proof needs it)");
            boxWas = OzPlayBlue.Box; boxHeld = true;
            OzPlayBlue.Box = false;
            var held = new StringBuilder();
            for (int i = 0; i < HoldKeys.Length; i++)
            {
                int v = PlayerPrefs.GetInt(HoldKeys[i], HoldDefault[i]);
                if (v == HoldWant[i]) continue;
                holdWas[i] = v; PlayerPrefs.SetInt(HoldKeys[i], HoldWant[i]); held.Append(" " + HoldKeys[i] + " " + v + " -> " + HoldWant[i]);
            }
            if (held.Length > 0) Debug.Log(Tag + " for this run (put back at its end):" + held);
            Status = "going"; LastDir = "";
            int step = 0; double next = EditorApplication.timeSinceStartup + 0.8, giveUp = EditorApplication.timeSinceStartup + LevelCapS;
            EditorApplication.CallbackFunction f = null;
            f = () =>
            {
                if (!Application.isPlaying) { EditorApplication.update -= f; RestorePrefs(); if (Status == "going") Status = "stopped: Play ended"; return; }
                double now = EditorApplication.timeSinceStartup;
                if (now > giveUp) { EditorApplication.update -= f; RestorePrefs(); Fail("level 2 did not start within " + LevelCapS.ToString("0", Inv) + " s"); return; }
                if (now < next) return;
                next = now + 0.8;
                if (step == 0) { m.ProofSelect(); step = 1; return; }
                if (step == 1) { m.ProofFocus(card); step = 2; return; }
                if (step == 2) { m.ProofDeploy(); step = 3; next = now + 1.0; return; }
                var g = ZGame.I;
                if (!g || g.World == null) return;
                EditorApplication.update -= f;
                OzPlayBlue.Box = boxWas; boxHeld = false;                                                    // the player's own box back (DEPLOY has read it)
                if (g.level != Level || g.player != Team.Red) { RestorePrefs(); Fail("the level is " + g.level + " as " + g.player + ", not L02 as red"); return; }
                Status = "waiting for the briefing's hand-back";
                var go = new GameObject("OzV15L02LoadingSunrise"); UnityEngine.Object.DontDestroyOnLoad(go);
                go.AddComponent<Run>();
                Debug.Log(Tag + " level 2 as red started: waiting for the briefing's hand-back (cap " + BriefingCapS.ToString("0", Inv) + " s), then the old look + 3 sunrise pictures");
            };
            EditorApplication.update += f;
            Debug.Log(Tag + " going: battlefield, level 2, red, deploy");
            return Status;
        }

        static void RestorePrefs()
        {
            if (boxHeld) { OzPlayBlue.Box = boxWas; boxHeld = false; }
            bool any = false;
            for (int i = 0; i < HoldKeys.Length; i++)
                if (holdWas[i] != int.MinValue) { PlayerPrefs.SetInt(HoldKeys[i], holdWas[i]); holdWas[i] = int.MinValue; any = true; }
            if (any) PlayerPrefs.Save();
        }

        /// <summary>Where a world direction lands in the 2560 x 1600 picture of the pose (x right, y down; NaN behind the camera).</summary>
        public static Vector2 PicturePx(Vector3 dir)
        {
            Vector3 f = (Look - Pos).normalized, r = Vector3.Cross(Vector3.up, f).normalized, u = Vector3.Cross(f, r);
            float k = H * 0.5f / Mathf.Tan(Fov * 0.5f * Mathf.Deg2Rad);
            float xc = Vector3.Dot(dir, r), yc = Vector3.Dot(dir, u), zc = Vector3.Dot(dir, f);
            return zc <= 1e-4f ? new Vector2(float.NaN, float.NaN) : new Vector2(W * 0.5f + k * xc / zc, H * 0.5f - k * yc / zc);
        }

        static string Root()
        {
            if (!string.IsNullOrEmpty(OutRoot)) return OutRoot;
            return Path.GetFullPath(Path.Combine(Application.dataPath, "../../../../work/staging/v1_5/kits/l02_loading_sunrise_r7/proof")).Replace('\\', '/');
        }

        /// <summary>Top-down RGB bytes of a texture (Unity holds the bottom row first).</summary>
        static byte[] RgbTopDown(Texture2D t)
        {
            var px = t.GetPixels32(); int w = t.width, h = t.height; var o = new byte[w * h * 3];
            for (int y = 0; y < h; y++)
            {
                int src = (h - 1 - y) * w, dst = y * w * 3;
                for (int x = 0; x < w; x++) { var c = px[src + x]; int p = dst + x * 3; o[p] = c.r; o[p + 1] = c.g; o[p + 2] = c.b; }
            }
            return o;
        }

        static byte[] LoadRgb(string path, out int w, out int h)
        {
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            try
            {
                if (!t.LoadImage(File.ReadAllBytes(path), false)) throw new IOException("not a picture: " + path);
                w = t.width; h = t.height; return RgbTopDown(t);
            }
            finally { UnityEngine.Object.DestroyImmediate(t); }
        }

        /// <summary>Writes top-down RGB bytes as an 8-bit RGB PNG.</summary>
        static void WritePng(string path, byte[] rgb, int w, int h)
        {
            var flip = new byte[rgb.Length]; int row = w * 3;
            for (int y = 0; y < h; y++) Buffer.BlockCopy(rgb, y * row, flip, (h - 1 - y) * row, row);
            var t = new Texture2D(w, h, TextureFormat.RGB24, false, false);
            try { t.LoadRawTextureData(flip); t.Apply(false); File.WriteAllBytes(path, t.EncodeToPNG()); }
            finally { UnityEngine.Object.DestroyImmediate(t); }
        }

        static string Md5(string path)
        {
            using (var md = System.Security.Cryptography.MD5.Create())
                return BitConverter.ToString(md.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
        }

        /// <summary>The run, after the level started (a component of its own object: gone with Play).</summary>
        public sealed class Run : MonoBehaviour
        {
            DayNight dn; bool runWas = true, rockWas, begun, cleaned, glowHeld; RockSeeThru rock; SunTurn turn; float glowWide0, glowGain0; Vector3 pxLook;
            readonly StringBuilder sum = new StringBuilder(), csv = new StringBuilder("index,name,file,key_euler_x,key_euler_y,drawn_sun_yaw,drawn_sun_elev,disc_px_x,disc_px_y,master_md5,picture_md5\n");

            void Line(string s) { sum.AppendLine(s); Debug.Log(Tag + " " + s); }

            IEnumerator Start()
            {
                float born = Time.realtimeSinceStartup; bool seen = false;
                while (true)
                {
                    if (!ZGame.I) { Stop("the level ended while waiting for the briefing"); yield break; }
                    bool brief = ZodBriefing.BriefingActive || (ZodFlyover.Live != null && ZodFlyover.Live.Active);
                    if (brief) seen = true;
                    var rtsNow = FindAnyObjectByType<RtsCamera>();
                    float t = Time.realtimeSinceStartup - born;
                    if (!brief && rtsNow && rtsNow.enabled && (seen || t > NoBriefingS)) break;
                    if (t > BriefingCapS) { Stop("the briefing did not hand the camera back within " + BriefingCapS.ToString("0", Inv) + " s"); yield break; }
                    yield return null;
                }
                Line("level 2 as red; briefing " + (seen ? "seen and handed back after " : "not seen in ") + (Time.realtimeSinceStartup - born).ToString("0.0", Inv) + " s");

                dn = FindAnyObjectByType<DayNight>();
                float tod = DayNight.LevelStartTod >= 0f ? DayNight.LevelStartTod : DayNight.SunriseStart;
                if (dn) { runWas = dn.running; dn.running = false; dn.timeOfDay = tod; dn.Apply(tod); }
                Line("clock held at the level-start sunrise: time of day " + tod.ToString("0.000", Inv) + (dn ? "" : " (no DayNight in the scene!)"));
                if (PlaySeconds > 0f) { Status = "waiting: the match runs " + PlaySeconds.ToString("0", Inv) + " s"; yield return new WaitForSecondsRealtime(PlaySeconds); if (!ZGame.I) { Stop("the level ended"); yield break; } }

                rock = RockSeeThru.I; rockWas = rock && rock.enabled; if (rock) rock.enabled = false;
                string sc = OzStillCam.Begin(); Line(sc); begun = true;                                          // End() is safe after any Begin()
                if (!OzStillCam.I || !sc.StartsWith("still cam on", StringComparison.Ordinal)) { Stop("the still camera did not start: " + sc); yield break; }
                Line(OzStillCam.Pose(Pos, Look, Fov, 0f));
                Line(OzStillCam.Freeze(true));
                var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
                Line("quality " + QualitySettings.names[QualitySettings.GetQualityLevel()] + ", pipeline " + (urp ? urp.name + " render scale " + urp.renderScale.ToString("0.00", Inv) + " MSAA " + urp.msaaSampleCount : "-"));

                Status = "waiting for the fly-over sky"; float skyFrom = Time.realtimeSinceStartup;
                while (!(FlybySky.I && FlybySky.I.Panorama && FlybySky.I.Weight >= 0.999f))
                {
                    if (Time.realtimeSinceStartup - skyFrom > SkyCapS) { Stop("the fly-over sky did not come in within " + SkyCapS.ToString("0", Inv) + " s: " + FlybySky.Status()); yield break; }
                    yield return null;
                }
                Line(FlybySky.Status());

                string dir = Root() + "/unity_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", Inv);
                Directory.CreateDirectory(dir + "/masters"); LastDir = dir;
                turn = gameObject.AddComponent<SunTurn>(); turn.Dn = dn;
                byte[] oldLook = null; glowWide0 = FlybySky.GlowWide; glowGain0 = FlybySky.GlowGain; glowHeld = true;
                for (int i = 0; i < Names.Length; i++)
                {
                    turn.Yaw = At(SunYaw, i); turn.SkyYaw = At(SkyYaw, i);
                    float gk = At(GlowK, i); FlybySky.GlowWide = glowWide0 * (float.IsNaN(gk) ? 1f : gk); FlybySky.GlowGain = glowGain0 * (float.IsNaN(gk) ? 1f : Mathf.Lerp(1f, gk, 0.5f));
                    float ly = At(LookY, i); if (!float.IsNaN(ly)) OzStillCam.Pose(Pos, new Vector3(Look.x, ly, Look.z), Fov, 0f); else OzStillCam.Pose(Pos, Look, Fov, 0f);
                    float td = At(Tod, i); if (dn) { float tt = float.IsNaN(td) ? tod : td; dn.timeOfDay = tt; dn.Apply(tt); }
                    for (int k = 0; k < 8; k++) yield return null;                                         // light, shadows, sky globals, haze settle
                    yield return new WaitForSecondsRealtime(0.3f);
                    Light sun = RenderSettings.sun ? RenderSettings.sun : dn ? dn.sun : null;
                    Vector3 eul = sun ? sun.transform.eulerAngles : Vector3.zero;
                    Vector3 drawn = sun ? FlybySky.VisualSun(-sun.transform.forward).normalized : Vector3.zero;
                    if (!float.IsNaN(turn.SkyYaw)) drawn = turn.SkyDir(drawn);
                    pxLook = float.IsNaN(ly) ? Look : new Vector3(Look.x, ly, Look.z);
                    float dYaw = Mathf.Atan2(drawn.x, drawn.z) * Mathf.Rad2Deg, dEl = Mathf.Asin(Mathf.Clamp(drawn.y, -1f, 1f)) * Mathf.Rad2Deg;
                    Vector2 px; { var keep2 = Look; Look = pxLook; px = PicturePx(drawn); Look = keep2; }
                    string master = dir + "/masters/L02_loading_3_" + Names[i] + "_3840x2400.png";
                    Status = "shooting " + Names[i];
                    OzStillCam.Shoot(master, Super, SuperH, 16);
                    float shotFrom = Time.realtimeSinceStartup;
                    while (!OzStillCam.Status.StartsWith("shot OK", StringComparison.Ordinal) && !OzStillCam.Status.StartsWith("shot FAILED", StringComparison.Ordinal))
                    {
                        if (Time.realtimeSinceStartup - shotFrom > ShotCapS) { Stop("the shot " + Names[i] + " did not end within " + ShotCapS.ToString("0", Inv) + " s"); yield break; }
                        yield return null;
                    }
                    if (!OzStillCam.Status.StartsWith("shot OK", StringComparison.Ordinal)) { Stop(Names[i] + ": " + OzStillCam.Status); yield break; }
                    Status = "writing";
                    var big = LoadRgb(master, out int bw, out int bh);
                    if (bw != Super || bh != SuperH) { Stop(Names[i] + ": the master is " + bw + " x " + bh); yield break; }
                    var small = OzL02SunrisePix.Downscale(big, Super, SuperH, W, H); big = null;
                    string file = dir + "/L02_loading_3_" + Names[i] + ".png";
                    WritePng(file, small, W, H);
                    if (i == 0) oldLook = small;
                    bool onScreen = !float.IsNaN(px.x) && px.x >= 0f && px.x < W && px.y >= 0f && px.y < H;
                    Line(Names[i] + ": key light Euler (" + eul.x.ToString("0.00", Inv) + ", " + eul.y.ToString("0.00", Inv) + ", 0); drawn sun yaw " + dYaw.ToString("0.00", Inv)
                         + " elev " + dEl.ToString("0.00", Inv) + " -> " + (onScreen ? "disc centre x " + px.x.ToString("0", Inv) + " y " + px.y.ToString("0", Inv) + " of 2560 x 1600" : "not in the picture (" + (float.IsNaN(px.x) ? "behind the camera" : "x " + px.x.ToString("0", Inv) + " y " + px.y.ToString("0", Inv)) + ")")
                         + "; " + Path.GetFileName(file));
                    csv.AppendLine(i + "," + Names[i] + "," + Path.GetFileName(file) + "," + eul.x.ToString("0.000", Inv) + "," + eul.y.ToString("0.000", Inv) + "," + dYaw.ToString("0.000", Inv) + ","
                                   + dEl.ToString("0.000", Inv) + "," + (onScreen ? px.x.ToString("0.0", Inv) : "") + "," + (onScreen ? px.y.ToString("0.0", Inv) : "") + "," + Md5(master) + "," + Md5(file));
                    yield return null;
                }
                turn.Yaw = float.NaN; turn.SkyYaw = float.NaN;

                Status = "writing";
                string oldPath = Path.Combine(Application.dataPath, OldPicture);
                var old = LoadRgb(oldPath, out int ow, out int oh);
                if (ow == W && oh == H && oldLook != null)
                {
                    WritePng(dir + "/angle_overlay_red_7Oct_picture_cyan_old_look_now.png", OzL02SunrisePix.Overlay(old, oldLook), W, H);
                    WritePng(dir + "/angle_blend_7Oct_picture_and_old_look_now.png", OzL02SunrisePix.Blend(old, oldLook), W, H);
                    OzL02SunrisePix.BestShift(old, oldLook, W, H, AngleRowFrom, false, out int dx, out int dy, out double iou0, out double iouB);
                    Line("ANGLE: Assets/" + OldPicture + " (md5 " + Md5(oldPath) + ") against the old look shot now: roads + water (rows " + AngleRowFrom + "-" + H + ") sit at ("
                         + dx + ", " + dy + ") px from the ones (0, 0 = the same place; searched +-32 px), overlap " + iou0.ToString("0.000", Inv) + " as shot, "
                         + iouB.ToString("0.000", Inv) + " at the best shift: " + (Mathf.Abs(dx) <= 2 && Mathf.Abs(dy) <= 2 ? "SAME ANGLE" : "ANGLE DIFFERS: check the pose"));
                }
                else Line("ANGLE: Assets/" + OldPicture + " is " + ow + " x " + oh + ": no overlay");
                File.WriteAllText(dir + "/capture.csv", csv.ToString());
                Line("pictures: " + dir + " (the 2560 x 1600 pictures = the loading pictures' size and format; masters/ = the 3840 x 2400 renders). Nothing in Resources was changed.");
                File.WriteAllText(dir + "/summary.txt", sum.ToString());
                Cleanup();
                Status = "done: " + Names.Length + " pictures + the angle proof in " + dir;
                Debug.Log(Tag + " " + Status);
                Destroy(gameObject);
            }

            void Stop(string why)
            {
                Line("STOPPED: " + why);
                if (!string.IsNullOrEmpty(LastDir) && Directory.Exists(LastDir)) { try { File.WriteAllText(LastDir + "/summary.txt", sum.ToString()); File.WriteAllText(LastDir + "/capture.csv", csv.ToString()); } catch (Exception) { } }
                Cleanup();
                Fail(why);
                Destroy(gameObject);
            }

            /// <summary>Everything back: the key light's own turn, the camera, the world clock and speed, the rock holes, the switches.</summary>
            void Cleanup()
            {
                if (cleaned) return; cleaned = true;
                if (turn) { turn.Yaw = float.NaN; turn.SkyYaw = float.NaN; Destroy(turn); }
                if (glowHeld) { FlybySky.GlowWide = glowWide0; FlybySky.GlowGain = glowGain0; glowHeld = false; }
                if (begun) OzStillCam.End();
                if (rock) rock.enabled = rockWas;
                if (dn) { dn.running = runWas; dn.Apply(dn.timeOfDay); }
                RestorePrefs();
            }

            void OnDestroy() { if (cleaned) return; Cleanup(); if (Busy) Status = "stopped: Play ended (or the run's object was destroyed) during " + Status; }
        }

        /// <summary>This run's key-light turn: the light keeps DayNight's elevation, colour and strength; only its azimuth is set so the
        /// drawn sun (FlybySky: the key light's azimuth) is at Yaw. Absolute, so it never adds up; in Update after DayNight (order 0) and
        /// again in LateUpdate before FlybySky (5000) reads it and the frame's shadows are drawn. NaN = the game's own sun.</summary>
        [DefaultExecutionOrder(50)]
        public sealed class SunTurn : MonoBehaviour
        {
            public float Yaw = float.NaN, SkyYaw = float.NaN;
            static readonly int IdSunDir = Shader.PropertyToID("_FlybySunDir");
            SkyLate late;
            void Awake() { late = gameObject.AddComponent<SkyLate>(); late.T = this; }
            void OnDestroy() { if (late) Destroy(late); }
            /// <summary>The drawn sun at SkyYaw, at the elevation FlybySky gave it.</summary>
            public Vector3 SkyDir(Vector3 gameDrawn)
            {
                float el = Mathf.Asin(Mathf.Clamp(gameDrawn.normalized.y, -1f, 1f)), y = SkyYaw * Mathf.Deg2Rad;
                return new Vector3(Mathf.Sin(y) * Mathf.Cos(el), Mathf.Sin(el), Mathf.Cos(y) * Mathf.Cos(el));
            }
            /// <summary>After FlybySky (order 5000) wrote the frame's sky globals: only the drawn sun's azimuth is replaced (absolute: never adds up).</summary>
            public void SkyApply()
            {
                if (float.IsNaN(SkyYaw)) return;
                Vector4 v = Shader.GetGlobalVector(IdSunDir); Vector3 d = SkyDir(new Vector3(v.x, v.y, v.z));
                Shader.SetGlobalVector(IdSunDir, new Vector4(d.x, d.y, d.z, v.w));
            }
            public DayNight Dn;

            void Update() => Turn();
            void LateUpdate() => Turn();

            void Turn()
            {
                if (float.IsNaN(Yaw)) return;
                Set(RenderSettings.sun);
                if (Dn && Dn.sun && Dn.sun != RenderSettings.sun) Set(Dn.sun);
            }

            void Set(Light l)
            {
                if (!l) return;
                float el = Mathf.Asin(Mathf.Clamp(-l.transform.forward.y, -1f, 1f)) * Mathf.Rad2Deg;          // DayNight's Euler x
                l.transform.rotation = Quaternion.Euler(el, Yaw - 180f, 0f);                                    // to the sun = -forward: yaw Yaw
            }
        }
    }

    [DefaultExecutionOrder(6000)]
    public sealed class SkyLate : MonoBehaviour
    {
        public OzV15L02LoadingSunrise.SunTurn T;
        void LateUpdate() { if (T) T.SkyApply(); }
    }

    // ---- PIX BEGIN (plain C#, no UnityEngine: kit_tools/test_pix.py compiles this block alone and checks it against Pillow)
    /// <summary>The picture work of the L02 sunrise loading picture, on plain RGB byte arrays (3 bytes a pixel, rows in either order):
    /// with it), the red / cyan overlay and the 50 / 50 blend of two pictures, and the angle test: the asphalt and water of the two pictures
    /// (blue / red colour ratio: the desert sand is warm, the roads grey, the water blue) compared at every shift up to +-32 px.</summary>
    public static class OzL02SunrisePix
    {
        static double Sinc(double x) { if (x == 0.0) return 1.0; x *= Math.PI; return Math.Sin(x) / x; }
        static double Lanczos3(double x) => x > -3.0 && x < 3.0 ? Sinc(x) * Sinc(x / 3.0) : 0.0;

        /// <summary>Pillow's resample coefficients (precompute_coeffs: support 3 x the scale when shrinking).</summary>
        static void Coeffs(int inSize, int outSize, out int[] start, out int[] count, out double[] w, out int k)
        {
            double scale = (double)inSize / outSize, fs = Math.Max(scale, 1.0), support = 3.0 * fs, ss = 1.0 / fs;
            k = (int)Math.Ceiling(support) * 2 + 1;
            start = new int[outSize]; count = new int[outSize]; w = new double[outSize * k];
            for (int x = 0; x < outSize; x++)
            {
                double center = (x + 0.5) * scale;
                int xmin = (int)(center - support + 0.5); if (xmin < 0) xmin = 0;
                int xmax = (int)(center + support + 0.5); if (xmax > inSize) xmax = inSize;
                xmax -= xmin;
                double total = 0.0;
                for (int i = 0; i < xmax; i++) { double v = Lanczos3((i + xmin - center + 0.5) * ss); w[x * k + i] = v; total += v; }
                if (total != 0.0) for (int i = 0; i < xmax; i++) w[x * k + i] /= total;
                start[x] = xmin; count[x] = xmax;
            }
        }

        /// <summary>Pillow's fixed point: 22 fraction bits, each weight rounded away from zero.</summary>
        static int[] Fixed(double[] w)
        {
            const double One = 1 << 22;
            var r = new int[w.Length];
            for (int i = 0; i < w.Length; i++) r[i] = w[i] < 0.0 ? (int)(-0.5 + w[i] * One) : (int)(0.5 + w[i] * One);
            return r;
        }

        static byte Clip(long ss) { ss >>= 22; return ss < 0 ? (byte)0 : ss > 255 ? (byte)255 : (byte)ss; }

        /// <summary>RGB (3 bytes a pixel) from sw x sh to dw x dh, Lanczos 3, the horizontal pass first, rounded and clipped after each
        /// pass (as Pillow does).</summary>
        public static byte[] Downscale(byte[] src, int sw, int sh, int dw, int dh)
        {
            Coeffs(sw, dw, out var hs, out var hc, out var hwd, out int hk); var hw = Fixed(hwd);
            Coeffs(sh, dh, out var vs, out var vc, out var vwd, out int vk); var vw = Fixed(vwd);
            var mid = new byte[dw * sh * 3];
            for (int y = 0; y < sh; y++)
            {
                int row = y * sw * 3, orow = y * dw * 3;
                for (int x = 0; x < dw; x++)
                {
                    long r = 1L << 21, g = 1L << 21, b = 1L << 21; int s0 = hs[x], n = hc[x], kb = x * hk;
                    for (int i = 0; i < n; i++) { int p = row + (s0 + i) * 3; long c = hw[kb + i]; r += src[p] * c; g += src[p + 1] * c; b += src[p + 2] * c; }
                    int o = orow + x * 3; mid[o] = Clip(r); mid[o + 1] = Clip(g); mid[o + 2] = Clip(b);
                }
            }
            var dst = new byte[dw * dh * 3];
            for (int y = 0; y < dh; y++)
            {
                int s0 = vs[y], n = vc[y], kb = y * vk, orow = y * dw * 3;
                for (int x = 0; x < dw; x++)
                {
                    long r = 1L << 21, g = 1L << 21, b = 1L << 21;
                    for (int i = 0; i < n; i++) { int p = ((s0 + i) * dw + x) * 3; long c = vw[kb + i]; r += mid[p] * c; g += mid[p + 1] * c; b += mid[p + 2] * c; }
                    int o = orow + x * 3; dst[o] = Clip(r); dst[o + 1] = Clip(g); dst[o + 2] = Clip(b);
                }
            }
            return dst;
        }

        static int Lum(byte[] a, int p) => (a[p] * 54 + a[p + 1] * 183 + a[p + 2] * 19) >> 8;

        /// <summary>Red = the first picture's brightness, green + blue = the second's: what sits in the same place reads grey, a shift
        /// shows as red / cyan fringes.</summary>
        public static byte[] Overlay(byte[] a, byte[] b)
        {
            var o = new byte[a.Length];
            for (int p = 0; p + 2 < a.Length; p += 3) { o[p] = (byte)Lum(a, p); o[p + 1] = o[p + 2] = (byte)Lum(b, p); }
            return o;
        }

        public static byte[] Blend(byte[] a, byte[] b)
        {
            var o = new byte[a.Length];
            for (int p = 0; p < a.Length; p++) o[p] = (byte)((a[p] + b[p] + 1) >> 1);
            return o;
        }

        /// <summary>Asphalt or water (a cool pixel): blue / (red + 8) above 0.405; the warm sand and rock stay far below it.</summary>
        static bool Cool(byte[] a, int p) => a[p + 2] > 0.405 * (a[p] + 8);

        /// <summary>The angle test. rowFrom: the first picture row (from the top) that counts (the ground, under the far mesas). bottomUp:
        /// the arrays hold the bottom row first (Unity's GetPixels32 order). Gives where the second picture's cool pixels sit against the
        /// first's (dx, dy in picture pixels, +x right, +y down; 0, 0 = the same place) and their overlap (IoU) at no shift and at that shift.</summary>
        public static void BestShift(byte[] a, byte[] b, int w, int h, int rowFrom, bool bottomUp, out int dx, out int dy, out double iou0, out double iouBest)
        {
            var A = new bool[w * h]; var B = new bool[w * h];
            for (int y = 0; y < h; y++)
            {
                int ty = bottomUp ? h - 1 - y : y;
                for (int x = 0; x < w; x++) { int p = (y * w + x) * 3; A[ty * w + x] = Cool(a, p); B[ty * w + x] = Cool(b, p); }
            }
            const int R = 32, M = R + 6;
            int y0 = Math.Max(rowFrom, M), y1 = h - M, x0 = M, x1 = w - M;
            int bx = 0, by = 0; double best = -1.0;
            for (int sy = -R; sy <= R; sy += 4)
                for (int sx = -R; sx <= R; sx += 4) { double v = Iou(A, B, w, x0, x1, y0, y1, sx, sy, 4); if (v > best) { best = v; bx = sx; by = sy; } }
            int cx = bx, cy = by; best = -1.0;
            for (int sy = cy - 4; sy <= cy + 4; sy++)
                for (int sx = cx - 4; sx <= cx + 4; sx++) { double v = Iou(A, B, w, x0, x1, y0, y1, sx, sy, 2); if (v > best) { best = v; bx = sx; by = sy; } }
            dx = -bx; dy = -by; iouBest = Iou(A, B, w, x0, x1, y0, y1, bx, by, 1); iou0 = Iou(A, B, w, x0, x1, y0, y1, 0, 0, 1);
        }

        /// <summary>IoU of A and B shifted by (sx, sy): B's pixel (x - sx, y - sy) against A's (x, y), rows from the top.</summary>
        static double Iou(bool[] A, bool[] B, int w, int x0, int x1, int y0, int y1, int sx, int sy, int step)
        {
            long i = 0, u = 0;
            for (int y = y0; y < y1; y += step)
                for (int x = x0; x < x1; x += step)
                {
                    bool a = A[y * w + x], b = B[(y - sy) * w + (x - sx)];
                    if (a && b) i++;
                    if (a || b) u++;
                }
            return u > 0 ? (double)i / u : 0.0;
        }
    }
    // ---- PIX END
}
#endif
