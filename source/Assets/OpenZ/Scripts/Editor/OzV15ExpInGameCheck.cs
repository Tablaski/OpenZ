using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using OpenZ.Sim;
using UnityEditor;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// <list type="bullet">
    /// <item>1 Count + materials: the switches, the counts of View/OzExpInGame.cs, every material of the seven explosions' channels (shader,
    /// queue, soft fade, depth test) into Logs/exp_in_game_check.txt; PASS lines.</item>
    /// <item>
    /// the outer world: the game camera low (4 m) inside a map edge, looking out over it toward the horizon, the blast 12 m inside the edge;
    /// day (DayNight 0.08) and dusk (0.32), each with the kit's fog rule ON and OFF. One sheet per explosion, 2560 x 1440, four blocks of
    /// 25 frames (every 6th frame of 5 s at 30 frames a second): top left DAY / fog rule ON, top right DAY / OFF, bottom left DUSK / ON,
    /// bottom right DUSK / OFF. Into owner_frames/explosions6/fog_order_check_exp_v2_n.jpg. Edge: Logs/exp_fog_edge.txt (S, N, E or W; S
    /// when there is none).</item>
    /// <item>3 / 4: the next on-screen death swaps / the next thrown head bursts at once (for a clip).</item>
    /// <item>5 Film takes: the cinematic clips (a film camera on a slow arc with a push-in, no HUD, no selection, no music, the game's own
    /// sound), frame-locked, from the take list Logs/exp_in_game_film.txt (format below).</item>
    /// </list>
    /// Take list: line 1 = the output folder (trailing slash). Each further line is one take:
    ///   name | frames before the first kept frame | kept frames | light (DayNight.timeOfDay, -1 = as it is) | camera | vehicles | events
    ///   camera:   tx:ty:tz:distance:pitch:yaw:yaw rate (the point looked at, metres from it, degrees down, degrees round, degrees a second)
    ///   vehicles: type,R or B or N,x,z[,health left]; ...  (world metres; stood there by the rules' own CreateObject)
    ///   events (frame first):  f,T,tx,ty,tz,distance  the camera's new aim and distance (eased)  |  f,K,kind,x,z  the nearest object of
    ///   kind (vehicle, cannon, building, fort, shuttle) to x,z is destroyed by the rules (DamageHealth + UpdateObjectHealth)  |  f,M,x,z  the
    ///   nearest fort's megacannon is blown off (FortGunView.TestToss, view)  |  f,S  the next on-screen death swaps  |  f,H  the next head
    ///   bursts  |  f,A  the camera follows the highest flying shuttle (feet over 3 m) from now  |  f,KA  that shuttle is destroyed (the camera then stays on its burst).
    /// Per take: f_00001.jpg ..., take.wav, done.txt; log.txt in the output folder. Mux: ffmpeg -framerate 30 -i f_%05d.jpg -i take.wav
    /// -c:v libx264 -crf 17 -pix_fmt yuv420p -c:a aac -b:a 192k -shortest name.mp4
    /// </summary>
    public static class OzV15ExpInGameCheck
    {
        const string Tag = "[OpenZ FX check]";
        const string Menu = "OpenZ/V1.5/Explosions in game/";
        const string Sheets = "../work/staging/v1_5/owner_frames/explosions6/";
        static readonly string[] Names = { "COLUMN", "FUEL", "CHAIN", "GEYSER", "FLAK", "SHOCK", "BURST" };
        static bool Ready(out ZGame g)
        {
            g = ZGame.I;
            if (!Application.isPlaying || !g || g.World == null || OzUnitEditor.I) { Debug.LogWarning($"{Tag} enter Play in a level first"); return false; }
            return true;
        }

        // ------------------------------------------------------------------------------------------------ 1
        [MenuItem(Menu + "1 Count + materials (Play, a level)")]
        static void Count()
        {
            if (!Ready(out var g)) return;
            var sb = new StringBuilder(); int pass = 0, fail = 0;
            void Line(bool ok, string s) { sb.Append(ok ? "PASS " : "FAIL ").Append(s).Append('\n'); if (ok) pass++; else fail++; }
            var cam = Camera.main;
            if (cam) OzUeBlastFx.Warm(cam.transform.position + cam.transform.forward * 12f);
            Line(OzExpInGame.InLevel(), "in a level (not the Unit editor)");
            Line(OzExpInGame.BuildingOn && OzExpInGame.MegaOn && OzExpInGame.SwapOn && OzExpInGame.HeadOn && OzExpInGame.FogShape, "every switch on (buildings, megacannon, swap, head, fog rule)");
            Line(OzExpInGame.SwapTurn.Every == 60.0 && OzExpInGame.HeadTurn.Every == 60.0, "both turns 60 s of game time");
            Line(OzUeBlasts.Count == 7, "seven explosions (" + OzUeBlasts.Count + ")");
            Line(OzExpInGame.TooSmallType("jeep") && OzExpInGame.TooSmallType("light") && OzExpInGame.TooSmallType("gatling") && OzExpInGame.TooSmallType("gun")
                 && !OzExpInGame.TooSmallType("medium") && !OzExpInGame.TooSmallType("apc") && !OzExpInGame.TooSmallType("missile_launcher") && !OzExpInGame.TooSmallType("howitzer"),
                 "R3.27: jeep, light tank, gatling, light gun never swap; medium, APC, MLRS, howitzer may");
            Line(OzExpInGame.AlwaysType("heavy") && OzExpInGame.AlwaysType(OpenZ.Sim.ZBigTanks.Bizon) && OzExpInGame.AlwaysType(OpenZ.Sim.ZBigTanks.Mammoth) && !OzExpInGame.AlwaysType("medium"),
                 "R3.28: heavy, Bizon, Mammoth: a new explosion at every death, outside the turn");
            int blast = 0, add = 0, soft = 0;
            foreach (var r in Object.FindObjectsByType<ParticleSystemRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var m = r ? r.sharedMaterial : null; if (!m || !m.shader) continue;
                string n = r.gameObject.name;
                if (n.IndexOf("ueb_", System.StringComparison.Ordinal) < 0) continue;
                float sf = m.HasProperty("_SoftFade") ? m.GetFloat("_SoftFade") : -1f;
                float zt = m.HasProperty("_ZTest") ? m.GetFloat("_ZTest") : 4f;
                bool own = m.shader.name == "OpenZ/FxBlast";
                if (own) blast++; else add++;
                bool smoke = own;                                                                            // every smoke and fire puff draws with OpenZ/FxBlast
                if (smoke && sf > 0f) soft++;
                sb.Append($"     {n}: shader {m.shader.name}, queue {m.renderQueue}, soft fade {sf:0.0#} m, depth test {(int)zt} (4 = LEqual), fog in the shader (MixFog)\n");
                if (smoke) Line(sf > 0f && (int)zt == 4, $"{n}: soft fade {sf:0.0#} m and depth tested");
            }
            Line(blast >= 16, $"the smoke and fire channels draw with OpenZ/FxBlast ({blast}), the light channels with the game's additive shader ({add})");
            Line(soft == blast && blast > 0, $"every smoke / fire material has a soft depth fade ({soft} of {blast})");
            sb.Append(OzExpInGame.Report()).Append('\n').Append(OzUeBlasts.Report()).Append('\n');
            sb.Append($"ALL: {pass} PASS, {fail} FAIL\n");
            Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/exp_in_game_check.txt", sb.ToString());
            Debug.Log($"{Tag} {pass} PASS, {fail} FAIL (Logs/exp_in_game_check.txt)\n{sb}");
        }

        [MenuItem(Menu + "3 Force the next on-screen swap (Play)")]
        static void ForceSwap() { OzExpInGame.ForceSwap = true; Debug.Log($"{Tag} the next on-screen vehicle / turret death plays a new explosion"); }

        [MenuItem(Menu + "4 Force the next head burst (Play)")]
        static void ForceHead() { OzExpInGame.ForceHead = true; Debug.Log($"{Tag} the next thrown head bursts in the air"); }

        // ------------------------------------------------------------------------------------------------ 2 the fog order sheets
        [MenuItem(Menu + "2 Fog order contact sheets (Play, a level)")]
        static void FogSheets()
        {
            if (!Ready(out var g)) return;
            g.StartCoroutine(FogRun(g));
            Debug.Log($"{Tag} fog order sheets: 7 explosions x day / dusk x fog rule on / off, about 7 minutes");
        }

        static IEnumerator Settle()
        {   // the level's opening skipped as the SPACE key does
            for (int i = 0; i < 40; i++)
            {
                bool a = false;
                try { a = ZodBriefing.ProofSkipBriefing() || OzBlueCameo.ProofSkipBriefing(); } catch (System.Exception) { }
                if (a) break;
                if (!ZodBriefing.BriefingActive && !OzBlueCameo.BriefingActive && !(ZodFlyover.Live != null && ZodFlyover.Live.Active) && i > 6) break;
                yield return new WaitForSecondsRealtime(0.5f);
            }
            float w0 = Time.realtimeSinceStartup;
            while (ZodFlyover.Live != null && ZodFlyover.Live.Active && Time.realtimeSinceStartup - w0 < 60f) yield return new WaitForSecondsRealtime(0.5f);
            yield return new WaitForSecondsRealtime(1.5f);
        }

        sealed class Quiet
        {
            readonly List<Canvas> hide; readonly PlayerInput pin; readonly List<SelectionOverlay> sel; readonly List<AudioSource> music = new List<AudioSource>(); int musicAt = -1000;
            readonly System.Reflection.PropertyInfo hov = typeof(PlayerInput).GetProperty("Hover");
            public Quiet()
            {
                hide = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.isRootCanvas && c.enabled && c.renderMode != RenderMode.WorldSpace).ToList();
                pin = Object.FindFirstObjectByType<PlayerInput>();
                sel = Object.FindObjectsByType<SelectionOverlay>(FindObjectsSortMode.None).Where(s => s.enabled).ToList();
            }
            public void Tick()
            {
                if (Time.frameCount - musicAt > 60)
                {
                    musicAt = Time.frameCount;
                    foreach (var s in Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                        if (s && s.gameObject.name.StartsWith("Music") && !music.Contains(s)) music.Add(s);
                }
                foreach (var s in music) if (s) s.mute = true;
                foreach (var c in hide) if (c) c.enabled = false;
                foreach (var s in sel) if (s) s.enabled = false;
                if (pin) { pin.enabled = false; if (hov != null) hov.SetValue(pin, null, null); pin.Selection.Clear(); }
            }
            public void Back()
            {
                foreach (var c in hide) if (c) c.enabled = true;
                foreach (var s in sel) if (s) s.enabled = true;
                foreach (var s in music) if (s) s.mute = false;
                if (pin) pin.enabled = true;
            }
        }

        static IEnumerator FogRun(ZGame g)
        {
            yield return Settle();
            var rts = Object.FindFirstObjectByType<RtsCamera>(); var cam = Camera.main; var dn = Object.FindFirstObjectByType<DayNight>();
            if (!cam) { Debug.LogWarning($"{Tag} no game camera"); yield break; }
            var q = new Quiet();
            bool bb = g.blueBot, rb = g.redBot; g.blueBot = false; g.redBot = false;
            float tod0 = dn ? dn.timeOfDay : 0f; bool run0 = dn && dn.running; bool fog0 = OzExpInGame.FogShape;
            if (rts) rts.enabled = false;
            Vector2 map = rts ? rts.mapSizeMeters : new Vector2(256f, 240f);
            string side = File.Exists("Logs/exp_fog_edge.txt") ? File.ReadAllText("Logs/exp_fog_edge.txt").Trim().ToUpperInvariant() : "S";
            if (side.Length == 0) side = "S";
            Vector3 mid = new Vector3(map.x * 0.5f, 0f, -map.y * 0.5f), outward, edge;
            switch (side[0])
            {
                case 'N': outward = Vector3.forward; edge = new Vector3(mid.x, 0f, 0f); break;
                case 'E': outward = Vector3.right; edge = new Vector3(map.x, 0f, mid.z); break;
                case 'W': outward = Vector3.left; edge = new Vector3(0f, 0f, mid.z); break;
                default: outward = Vector3.back; edge = new Vector3(mid.x, 0f, -map.y); break;
            }
            var at = edge - outward * 12f; at.y = g.GroundY(at.x, at.z);
            var cp = edge - outward * 45f; cp.y = g.GroundY(cp.x, cp.z) + 4f;
            var rot = Quaternion.LookRotation(outward, Vector3.up) * Quaternion.Euler(-3f, 0f, 0f);         // a little up: the horizon and the haze in the picture
            const int FPS = 30, N = 150, Every = 6, Cols = 5, TW = 256, TH = 144;
            var eof = new WaitForEndOfFrame(); var log = new StringBuilder();
            var tiny = new RenderTexture(TW, TH, 0, RenderTextureFormat.ARGB32);
            Time.captureFramerate = FPS;
            for (int type = 0; type < 7; type++)
            {
                var sheet = new Texture2D(Cols * TW * 2, Cols * TH * 2, TextureFormat.RGB24, false);
                for (int block = 0; block < 4; block++)
                {
                    bool dusk = block >= 2, on = (block & 1) == 0;
                    float tod = dusk ? 0.32f : 0.08f;
                    if (dn) { dn.running = false; dn.timeOfDay = tod; dn.Apply(tod); }
                    OzExpInGame.FogShape = on;
                    float w0 = Time.realtimeSinceStartup;
                    while (OzUeBlastFx.Live > 0 && Time.realtimeSinceStartup - w0 < 20f) { q.Tick(); cam.transform.SetPositionAndRotation(cp, rot); yield return null; }
                    for (int i = 0; i < 20; i++) { q.Tick(); cam.transform.SetPositionAndRotation(cp, rot); yield return null; }
                    bool ok = OzExpInGame.Play(type, at, 1f, 1f);
                    log.Append($"exp_v2_{type + 1} {Names[type]} {(dusk ? "dusk" : "day")} fog rule {(on ? "ON" : "OFF")}: fired {ok} at ({at.x:0.0}, {at.y:0.0}, {at.z:0.0}), camera ({cp.x:0.0}, {cp.y:0.0}, {cp.z:0.0}) looking {side}\n");
                    int tile = 0;
                    for (int f = 0; f < N; f++)
                    {
                        q.Tick(); cam.transform.SetPositionAndRotation(cp, rot);
                        yield return eof;
                        if (f % Every != 0) continue;
                        var shot = ScreenCapture.CaptureScreenshotAsTexture();
                        if (!shot) continue;
                        Graphics.Blit(shot, tiny);
                        var was = RenderTexture.active; RenderTexture.active = tiny;
                        var px = new Texture2D(TW, TH, TextureFormat.RGB24, false); px.ReadPixels(new Rect(0, 0, TW, TH), 0, 0); px.Apply();
                        RenderTexture.active = was; Object.Destroy(shot);
                        int cx = tile % Cols, cy = tile / Cols;                                                  // rows from the top
                        int ox = (block & 1) * Cols * TW + cx * TW, oy = sheet.height - ((block >> 1) * Cols * TH + (cy + 1) * TH);
                        sheet.SetPixels(ox, oy, TW, TH, px.GetPixels()); Object.Destroy(px);
                        tile++;
                    }
                }
                sheet.Apply();
                Directory.CreateDirectory(Sheets);
                string path = Sheets + "fog_order_check_exp_v2_" + (type + 1) + ".jpg";
                File.WriteAllBytes(path, sheet.EncodeToJPG(90)); Object.Destroy(sheet);
                log.Append("  sheet " + path + "\n");
            }
            Time.captureFramerate = 0; tiny.Release(); Object.Destroy(tiny);
            OzExpInGame.FogShape = fog0; g.blueBot = bb; g.redBot = rb;
            if (dn) { dn.timeOfDay = tod0; dn.Apply(tod0); dn.running = run0; }
            if (rts) rts.enabled = true;
            q.Back();
            log.Append("blocks: top left DAY / fog rule ON, top right DAY / OFF, bottom left DUSK / ON, bottom right DUSK / OFF; every 6th frame of 5 s\n");
            Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/exp_fog_check.txt", log.ToString());
            Debug.Log($"{Tag} fog order sheets written (Logs/exp_fog_check.txt)\n{log}");
        }

        // ------------------------------------------------------------------------------------------------ 5 film takes
        const string ListPath = "Logs/exp_in_game_film.txt";

        [MenuItem(Menu + "5 Film takes (Play, a level)")]
        static void Film()
        {
            if (!Ready(out var g)) return;
            if (!File.Exists(ListPath)) { Debug.LogWarning($"{Tag} no take list at {ListPath}"); return; }
            var lines = File.ReadAllLines(ListPath).Where(s => s.Trim().Length > 0 && !s.StartsWith("#")).ToArray();
            if (lines.Length < 2) { Debug.LogWarning($"{Tag} the take list is empty"); return; }
            string root = lines[0].Trim();
            if (File.Exists(root + "started.txt")) { Debug.LogWarning($"{Tag} this take list was already started ({root})"); return; }
            Directory.CreateDirectory(root); File.WriteAllText(root + "started.txt", "x");
            g.StartCoroutine(FilmRun(g, root, lines));
            Debug.Log($"{Tag} filming {lines.Length - 1} take(s) into {root}");
        }

        static float F(string s) => float.Parse(s, CultureInfo.InvariantCulture);

        static ZObject Nearest(ZWorld w, string kind, Vector3 at)
        {
            ZObject best = null; float bd = float.MaxValue;
            foreach (var o in w.Objects)
            {
                if (o == null || o.Destroyed) continue;
                bool fit = kind == "shuttle" ? o.IsShuttle : kind == "vehicle" ? o.Kind == ZKind.Vehicle && !o.IsShuttle : kind == "cannon" ? o.Kind == ZKind.Cannon
                         : kind == "fort" ? o.IsFort : kind == "building" && o.Kind == ZKind.Building && !o.IsBridge && !o.IsFort;
                if (!fit) continue;
                var p = ZGame.PxToWorld(o.CenterX, o.CenterY); float d = (p.x - at.x) * (p.x - at.x) + (p.z - at.z) * (p.z - at.z);
                if (d < bd) { bd = d; best = o; }
            }
            return best;
        }

        static IEnumerator FilmRun(ZGame g, string root, string[] L)
        {
            var log = new StringBuilder(); var ic = CultureInfo.InvariantCulture;
            yield return Settle();
            var w = g.World; var rts = Object.FindFirstObjectByType<RtsCamera>(); var cam = Camera.main; var dn = Object.FindFirstObjectByType<DayNight>();
            var q = new Quiet();
            bool bb = g.blueBot, rb = g.redBot; g.blueBot = false; g.redBot = false;
            float tod0 = dn ? dn.timeOfDay : 0f; bool run0 = dn && dn.running;
            float h0 = rts ? rts.height : 70f; Vector3 focus0 = rts ? rts.focus : Vector3.zero;
            const int FPS = 30; int rate = AudioSettings.outputSampleRate; const int ch = 2; var eof = new WaitForEndOfFrame();
            for (int ci = 1; ci < L.Length; ci++)
            {
                var sh = L[ci].Split('|'); string nm = sh[0].Trim(); int pre = int.Parse(sh[1]), N = int.Parse(sh[2]); string dir = root + nm + "\\";
                Directory.CreateDirectory(dir);
                string[] evs = new string[0]; ZObject follow = null;
                Vector3 tgt = Vector3.zero, goalT = Vector3.zero, velT = Vector3.zero; float dist = 60f, goalD = 60f, velD = 0f, pitch = 35f, yaw = 0f, yawRate = 0f;
                int s0 = OzExpInGame.Swaps, hd0 = OzExpInGame.Heads, b0 = OzExpInGame.Buildings, m0 = OzExpInGame.Megas, a0 = OzShuttleAirBurst.Played;
                try
                {
                    float tod = F(sh[3]);
                    if (dn && tod >= 0f) { dn.running = false; dn.timeOfDay = tod; dn.Apply(tod); }
                    var cc = sh[4].Trim().Split(':');
                    tgt = goalT = new Vector3(F(cc[0]), F(cc[1]), F(cc[2])); dist = goalD = F(cc[3]); pitch = F(cc[4]); yaw = F(cc[5]); yawRate = F(cc[6]);
                    if (rts) rts.enabled = false;
                    foreach (var us in sh[5].Split(';'))
                    {
                        var u = us.Trim().Split(','); if (u.Length < 4) continue;
                        var px = ZGame.WorldToPx(new Vector3(F(u[2]), 0f, F(u[3]))); int tx = (px.x >> 4) - 1, ty = (px.y >> 4) - 1;
                        var o = w.CreateObject("vehicle", u[0], tx * 16, ty * 16, u[1] == "R" ? Team.Red : u[1] == "N" ? Team.Neutral : Team.Blue);
                        if (o == null) { log.Append(nm + " vehicle " + u[0] + " not made\n"); continue; }
                        g.SpawnUnitView(o);
                        if (u.Length > 4) o.Health = Mathf.Min(o.Health, int.Parse(u[4]));
                        log.Append(nm + " vehicle " + u[0] + " #" + o.Id + " health " + o.Health + " of " + o.MaxHealth + "\n");
                    }
                    evs = sh.Length > 6 ? sh[6].Split(';') : new string[0];
                }
                catch (System.Exception ex) { log.Append(nm + " setup " + ex + "\n"); }

                var samples = new List<float>(N * rate / FPS * ch + 4096); bool audioOn = false;
                Time.captureFramerate = FPS;
                for (int f0 = 0; ; f0++)
                {
                    q.Tick();
                    int f = f0 - pre;
                    if (f == 0) audioOn = AudioRenderer.Start();
                    if (f > 0)
                    {
                        var tex = ScreenCapture.CaptureScreenshotAsTexture();
                        if (tex)
                        {
                            var raw = tex.GetRawTextureData(); int twd = tex.width, th = tex.height; var fmt = tex.graphicsFormat; string path = dir + string.Format("f_{0:00000}.jpg", f);
                            Object.Destroy(tex);
                            System.Threading.Tasks.Task.Run(() => File.WriteAllBytes(path, ImageConversion.EncodeArrayToJPG(raw, fmt, (uint)twd, (uint)th, 0, 92)));
                        }
                        if (audioOn)
                        {
                            int avail = AudioRenderer.GetSampleCountForCaptureFrame();
                            var nb = new Unity.Collections.NativeArray<float>(Mathf.Max(0, avail) * ch, Unity.Collections.Allocator.Temp);
                            AudioRenderer.Render(nb);
                            for (int k = 0; k < nb.Length; k++) samples.Add(nb[k]);
                            nb.Dispose();
                        }
                    }
                    foreach (var es in evs)
                    {
                        var e = es.Trim().Split(','); if (e.Length < 2 || int.Parse(e[0]) != f) continue;
                        try
                        {
                            switch (e[1])
                            {
                                case "T": goalT = new Vector3(F(e[2]), F(e[3]), F(e[4])); goalD = F(e[5]); break;
                                case "S": OzExpInGame.ForceSwap = true; break;
                                case "H": OzExpInGame.ForceHead = true; break;
                                case "K":
                                {
                                    var o = Nearest(w, e[2], new Vector3(F(e[3]), 0f, F(e[4])));
                                    if (o != null) { o.DamageHealth(o.Health); w.UpdateObjectHealth(o); }
                                    log.Append($"{nm} f{f} kill {e[2]}: {(o != null ? o.Type + " #" + o.Id : "none")}\n");
                                    break;
                                }
                                case "M":
                                {
                                    var fort = Nearest(w, "fort", new Vector3(F(e[2]), 0f, F(e[3])));
                                    var gun = fort == null ? null : Object.FindObjectsByType<FortGunView>(FindObjectsSortMode.None).FirstOrDefault(v => v && v.Bound && v.Fort == fort);
                                    if (gun) gun.TestToss();
                                    log.Append($"{nm} f{f} megacannon off: {(gun ? "fort #" + fort.Id : "none")}\n");
                                    break;
                                }
                                case "A":
                                {
                                    follow = null; float best = 3f;
                                    foreach (var o in w.Objects)
                                    {
                                        if (o == null || o.Destroyed || !o.IsShuttle) continue;
                                        var ps = OzPlayableShuttle.Of(o);
                                        if (ps && ps.FeetHeight > best) { best = ps.FeetHeight; follow = o; }
                                    }
                                    log.Append($"{nm} f{f} follow shuttle: {(follow != null ? "#" + follow.Id + " feet " + best.ToString("0.0", ic) + " m" : "none in the air")}\n");
                                    break;
                                }
                                case "KA":
                                    if (follow != null && !follow.Destroyed) { follow.DamageHealth(follow.Health); w.UpdateObjectHealth(follow); }
                                    log.Append($"{nm} f{f} kill the followed shuttle: {(follow != null ? "#" + follow.Id : "none")}\n");
                                    follow = null;                                                                        // the camera stays on the burst
                                    break;
                            }
                        }
                        catch (System.Exception ex) { log.Append(nm + " ev " + f + " " + ex.Message + "\n"); }
                    }
                    if (follow != null)
                    {
                        var ps = OzPlayableShuttle.Of(follow);
                        if (ps) goalT = ps.transform.position;
                    }
                    tgt = Vector3.SmoothDamp(tgt, goalT, ref velT, 0.7f, Mathf.Infinity, 1f / FPS);
                    dist = Mathf.SmoothDamp(dist, goalD, ref velD, 1.6f, Mathf.Infinity, 1f / FPS);
                    yaw += yawRate / FPS;
                    if (cam)
                    {
                        var rot = Quaternion.Euler(pitch, yaw, 0f);
                        cam.transform.SetPositionAndRotation(tgt - rot * Vector3.forward * dist, rot);
                    }
                    if (rts) rts.focus = new Vector3(tgt.x, 0f, tgt.z);                                      // the game's ear goes with the film camera's aim
                    if (f >= N) break;
                    yield return eof;
                }
                if (audioOn) AudioRenderer.Stop();
                using (var fs = new FileStream(dir + "take.wav", FileMode.Create)) using (var bw = new BinaryWriter(fs))
                {
                    int bytes = samples.Count * 4;
                    bw.Write(Encoding.ASCII.GetBytes("RIFF")); bw.Write(36 + bytes); bw.Write(Encoding.ASCII.GetBytes("WAVE")); bw.Write(Encoding.ASCII.GetBytes("fmt ")); bw.Write(16); bw.Write((short)3); bw.Write((short)ch);
                    bw.Write(rate); bw.Write(rate * ch * 4); bw.Write((short)(ch * 4)); bw.Write((short)32); bw.Write(Encoding.ASCII.GetBytes("data")); bw.Write(bytes);
                    foreach (var s in samples) bw.Write(s);
                }
                log.Append($"{nm} end: swaps +{OzExpInGame.Swaps - s0}, heads +{OzExpInGame.Heads - hd0}, buildings +{OzExpInGame.Buildings - b0}, megacannons +{OzExpInGame.Megas - m0}, shuttle air bursts +{OzShuttleAirBurst.Played - a0} | {OzExpInGame.LastLine}\n");
                File.WriteAllText(dir + "done.txt", "frames " + N + " fps " + FPS + " samples " + samples.Count);
                File.WriteAllText(root + "log.txt", log.ToString());
                Time.captureFramerate = 0;
                yield return new WaitForSecondsRealtime(1.5f);
            }
            Time.captureFramerate = 0;
            g.blueBot = bb; g.redBot = rb;
            if (dn) { dn.timeOfDay = tod0; dn.Apply(tod0); dn.running = run0; }
            if (rts) { rts.enabled = true; rts.focus = focus0; rts.SetHeightImmediate(h0); }
            q.Back();
            File.WriteAllText(root + "log.txt", log.ToString()); File.WriteAllText(root + "done.txt", "ok");
            Debug.Log($"{Tag} takes filmed: {root}");
        }
    }
}
