// OpenZ (GPL-3.0) - version 1.4: the counted checks of the optional FOG OF WAR (View/OzFogOfWar*.cs, View/OzFowMask.cs).
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using OpenZ.Sim;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace OpenZ
{
    /// <summary>
    /// Version 1.4 FOG OF WAR, counted checks (each writes its lines to the console and to Logs/oz_v14_fog_of_war_*.txt next to Assets):
    /// <list type="bullet">
    /// <item>Mask Count (headless, no Play): the reveal map's rules (stamp, soft edge, explore-once, the eased reveal, clipping at the map edge,
    /// cover again), the sight table, and its cost on a level 17 sized map.</item>
    /// <item>Play Count (in Play on a level as a human, any difficulty; run it after a Play start): the OPTIONS default, the fog switched on
    /// for this match from code as the testing panel does, the pass drawn on the player's camera, explore-once in the running game, the
    /// start area open, units in covered ground held off and every renderer given back, the hooks' question, the minimap cover, the three
    /// styles on the material (with the planet's palette), the cost a frame; then the match is put back as it was.</item>
    /// <item>Look Frames (in Play): the game view saved with the fog off and with each style (Logs/fog_of_war_looks/fog_0_off.png,
    /// fog_1_sea.png, fog_2_banks.png, fog_3_mist.png); each state is held until its file is written (a capture lands a frame or more later).</item>
    /// <item>Edge Frames (in Play, for the judges only): each style's edge map (debug view 2: red opacity, green the distance to the hiding
    /// contour at the surface, blue the cover at the surface), Logs/fog_of_war_looks/edge_*.png.</item>
    /// <item>Edge Count (in the editor, no Play): the soft edge field on the graphics card from made-up reveal maps, rows E1 - E6.</item>
    /// <item>Cost Bench (in Play, about 3 min, first Play after an editor restart): the fog's own processor time per style (B1); frame GPU
    /// with the fog on minus off in interleaved pairs (B2, information); the GPU time of the sheets pass timed directly (the pass alone at
    /// 1920 x 1080 with the player camera's matrices, into the camera's own kind of target, MSAA included, waited on until the card has
    /// finished): the view as it is and the field (B3), everything covered with the camera at its highest (B4), a view that is soft band
    /// almost everywhere (B5); B6 (information): frame GPU with the fog on but its pass not put into the camera (units held off, the
    /// camera's dithering) minus off, which splits the pass from everything else; then the reveal map, the camera, the style and the fog
    /// are put back.</item>
    /// <item>Edge Measure (in Play, round 4): at the camera as it is, each style's colour frame (as in play), its opacity (debug 1) and each
    /// sheet's alpha (debug 3) with the post effects off, and the fog off once, as lossless PNGs in Logs/fog_of_war_edges_r4/ (the tag in
    /// <see cref="MeasureTag"/>), for the edge metrics (staging tools/r4_fog_edge_metrics.py).</item>
    /// <item>Reveal Clips (in Play, round 4): for one planet (<see cref="ClipSpec"/>), the three styles back to back, each from the level
    /// loaded fresh: the camera still at the normal RTS height, one of the player's own units sent by the game's move order from explored
    /// ground deep into covered ground, recorded at a fixed 60 fps (OzFowClipRecorder) into <see cref="ClipDir"/>.</item>
    /// <item>Colour Grid (in Play, 5 Nov): the reveal clip's start replayed to clip frame <see cref="GridFrame"/>, the game paused with the
    /// clouds' drift and the reveal held, then 16 stills (colour TODAY / LAND / AIR / DUSK x cover 100 / 85 / 70 / 55 %), or with
    /// <see cref="GridCalibrate"/> the colour ideas' multipliers fitted on screen.</item>
    /// </list>
    /// </summary>
    public static class OzV14FogOfWarCheck
    {
        const string Tag = "[OpenZ V1.4 FogOfWar]";
        static readonly List<string> lines = new List<string>();
        static int pass, total;

        static void Begin(string what) { lines.Clear(); pass = total = 0; Say($"{Tag} {what}"); }
        static void Say(string s) { lines.Add(s); Debug.Log(s); }
        static void Row(string id, bool ok, string text) { total++; if (ok) pass++; Say($"  {(ok ? "PASS" : "FAIL")} {id} {text}"); }
        static void End(string file)
        {
            Say($"{Tag} {(pass == total ? "PASS" : "FAIL")} {pass}/{total}");
            try
            {
                var dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs"));
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, file), string.Join("\n", lines) + "\n");
            }
            catch (System.Exception e) { Debug.LogWarning($"{Tag} could not write the log file: {e.Message}"); }
        }

        // ================================================================ headless
        [MenuItem("OpenZ/V1.4/Fog of War: Mask Count (headless)")]
        public static void MaskCount()
        {
            Begin("Mask Count (headless): the reveal map and the sight table");
            var m = new OzFowMask(72 * 16, 90 * 16);
            Row("M1", m.W == 144 && m.H == 180, $"level 2's 72 x 90 tiles are {m.W} x {m.H} cells (2 a tile, 1 m)");

            int cx = 72 * 8, cy = 90 * 8;                                               // the map's centre in px
            bool opened = m.Stamp(cx, cy, 184);
            int c0 = m.CellX(cx), r0 = m.CellY(cy);
            byte At(int dx, int dy) => m.Target[(r0 + dy) * m.W + c0 + dx];
            Row("M2", opened && At(0, 0) == 255 && At(19, 0) == 255 && At(0, -20) == 255 && At(23, 0) == 0 && At(0, 24) == 0,
                $"a grunt's 184 px (23 cells) circle: centre {At(0, 0)}, 19 cells {At(19, 0)}, 20 up {At(0, -20)}, 23 cells {At(23, 0)}, 24 down {At(0, 24)}");
            bool mono = true; for (int d = 0; d < 24; d++) if (At(d + 1, 0) > At(d, 0)) mono = false;
            Row("M3", mono && At(21, 0) > 0 && At(21, 0) < 255, $"the edge falls off softly and only outwards (21 cells: {At(21, 0)}, 22 cells: {At(22, 0)})");

            var before = (byte[])m.Target.Clone();
            m.Stamp(cx + 60 * 8, cy, 184);                                              // the unit walked 60 m east
            bool never = true; for (int i = 0; i < before.Length; i++) if (m.Target[i] < before[i]) never = false;
            Row("M4", never && At(0, 0) == 255, "explore-once: after the unit walked 60 m away, no explored cell went down (the first circle still 255)");

            int s0 = m.StampsThatOpened; m.Stamp(cx, cy, 184);
            Row("M5", m.StampsThatOpened == s0, "the same stamp again changes nothing");

            Row("M6", m.Shown[r0 * m.W + c0] == 0 && m.Easing, "before the reveal eases, the shown map is still closed there");
            int steps = 0; bool above = false;
            while (m.Easing && steps < 1000) { m.Ease(32); steps++; for (int i = 0; i < m.Shown.Length; i += 7) if (m.Shown[i] > m.Target[i]) above = true; }
            bool same = true; for (int i = 0; i < m.Shown.Length; i++) if (m.Shown[i] != m.Target[i]) same = false;
            Row("M7", same && !above && steps == 8, $"the reveal eases up in {steps} steps of 32 (255 / 32 = 8), never above the explored map, then equals it");

            bool clipped = m.Stamp(0, 0, 184) && !m.Stamp(-5000, -5000, 184) && m.Stamp(72 * 16 + 40, 90 * 16 + 40, 184);
            Row("M8", clipped, "stamps at the map's corner and just outside it are clipped; one far outside opens nothing");

            m.ShowAll(); bool eq = true; for (int i = 0; i < m.Shown.Length; i++) if (m.Shown[i] != m.Target[i]) eq = false;
            m.Clear(); int nz = 0; for (int i = 0; i < m.Target.Length; i++) if (m.Target[i] != 0 || m.Shown[i] != 0) nz++;
            Row("M9", eq && nz == 0 && !m.Easing, "ShowAll copies the explored map; COVER AGAIN closes every cell");

            var k = m.Kernel(10); int n = 21; bool sym = true;
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++) if (k[y * n + x] != k[x * n + y] || k[y * n + x] != k[(n - 1 - y) * n + x]) sym = false;
            Row("M10", sym && k[10 * n + 10] == 255 && k[0] == 0, "the kernel is round and symmetric: 255 in the middle, 0 in the corners");

            int agro = 40;
            int Sight(string kind, int radius, int w = 16, string type = "x")
            {
                var o = new ZObject { Kind = kind == "robot" ? ZKind.Robot : kind == "building" ? ZKind.Building : kind == "flag" ? ZKind.MapItem : ZKind.Vehicle, Type = type, WidthPix = w, HeightPix = w };
                if (kind == "robot" || kind == "vehicle") o.U = new ZUnitSettings { attack_radius = radius };
                return OzFogOfWar.SightPx(o, agro);
            }
            int g = Sight("robot", 120), hw = Sight("vehicle", 200), sn = Sight("robot", 144), ap = Sight("vehicle", 0), fo = Sight("building", 0, 128), fl = Sight("flag", 0, 16, "flag"), rk = Sight("flag", 0, 16, "rock");
            Row("M11", g == 184 && hw == 264 && sn == 208 && ap == 184 && fo == 192 && fl == 128 && rk == 0,
                $"sight px: grunt {g} (23 m), howitzer {hw} (33 m), sniper {sn}, APC {ap}, a 128 px fort {fo} (24 m), a flag of ours {fl}, a rock {rk}");

            // cost on a level 17 sized map: 200 units moving 1 cell a round (each stamps), then the reveal over the whole box
            var big = new OzFowMask(110 * 16, 128 * 16); var rnd = new System.Random(7);
            var px = new int[200]; var py = new int[200];
            for (int i = 0; i < 200; i++) { px[i] = rnd.Next(100, 1660); py[i] = rnd.Next(100, 1948); }
            var sw = Stopwatch.StartNew(); int rounds = 50;
            for (int r = 0; r < rounds; r++) for (int i = 0; i < 200; i++) { px[i] += 8; big.Stamp(px[i], py[i], 184); }
            double stampMs = sw.Elapsed.TotalMilliseconds / rounds;
            sw.Restart(); int eases = 0; while (big.Easing && eases < 64) { big.Ease(16); eases++; }
            double easeMs = eases > 0 ? sw.Elapsed.TotalMilliseconds / eases : 0;
            Row("M12", stampMs < 1.5 && easeMs < 1.0, $"cost, level 17 map {big.W} x {big.H}: a round of 200 moving units {stampMs:0.000} ms (bar 1.5; in the game a round is every {OzFogOfWar.StampEvery} frames and only moved units stamp), a reveal step over the box {easeMs:0.000} ms (bar 1.0)");
            End("oz_v14_fog_of_war_mask.txt");
        }

        // ================================================================ Play
        static IEnumerator running;
        static readonly Stack<IEnumerator> nest = new Stack<IEnumerator>();   // w1 5 Nov: the waits (Frames / Seconds) are nested routines; one MoveNext on the outer one skipped them
        static void Run(IEnumerator it)
        {
            if (!EditorApplication.isPlaying) { Debug.LogWarning($"{Tag} press Play, start a level as a human, then run this again"); return; }
            if (running != null) { Debug.LogWarning($"{Tag} a check is already running"); return; }
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
            catch (System.Exception e) { Debug.LogError($"{Tag} the check stopped: {e}"); more = false; }
            if (!more) { EditorApplication.update -= Step; running = null; nest.Clear(); }
        }
        static IEnumerator Frames(int n) { int f = Time.frameCount + n; while (Time.frameCount < f) yield return null; }
        /// <summary>A game view capture, held until its file is written and stable (round 1's captures landed frames late, after the next
        /// state was set, so some files showed the wrong style or time of day). At most 6 s.</summary>
        public static IEnumerator Shot(string path)
        {
            var asked = System.DateTime.UtcNow.AddSeconds(-1);
            ScreenCapture.CaptureScreenshot(path);
            float until = Time.realtimeSinceStartup + 6f; long last = -1;
            while (Time.realtimeSinceStartup < until)
            {
                yield return null;
                var fi = new FileInfo(path);
                if (!fi.Exists || fi.LastWriteTimeUtc < asked) continue;
                if (fi.Length > 0 && fi.Length == last) yield break;
                last = fi.Length;
            }
            Debug.LogWarning($"{Tag} capture not written in 6 s: {path}");
        }
        static IEnumerator Seconds(float s) { float t = Time.realtimeSinceStartup + s; while (Time.realtimeSinceStartup < t) yield return null; }

        [MenuItem("OpenZ/V1.4/Fog of War: Play Count (in Play, a level as a human)")]
        public static void PlayCount() => Run(PlayCountRoutine());

        static IEnumerator PlayCountRoutine()
        {
            Begin("Play Count (a level as a human)");
            var g = ZGame.I; var f = OzFogOfWar.I;
            if (!g || g.World == null || !f) { Row("P0", false, "no level running (ZGame / OzFogOfWar missing): start a level as a human"); End("oz_v14_fog_of_war_play.txt"); yield break; }
            var W = g.World; var player = g.player;

            bool had = PlayerPrefs.HasKey(OzFogOfWarOption.Pref); int was = PlayerPrefs.GetInt(OzFogOfWarOption.Pref, 0);
            PlayerPrefs.DeleteKey(OzFogOfWarOption.Pref); bool freshOff = !OzFogOfWarOption.On;
            if (had) PlayerPrefs.SetInt(OzFogOfWarOption.Pref, was);
            Row("P1", freshOff && !OzFogOfWarOption.DefaultOn, $"OPTIONS FOG OF WAR on a fresh install: OFF (the saved value {(had ? was.ToString() : "none")} put back)");
            bool wasWanted = OzFogOfWar.Wanted;
            Say($"  this match started with the fog {(wasWanted ? "ON" : "off")}: {OzFogOfWar.WhyOffNow ?? "OPTIONS on"}");

            // the renderers held off by other scripts before the fog (to compare after)
            int heldBefore = HeldOff(g);

            bool on = f.SetForced(true, out var refused);
            Row("P2", on && OzFogOfWar.Wanted, on ? "the fog switched on for this match (as the testing panel's FOG ON)" : "refused: " + refused + " (start a level as a human, not the Unit editor, 'Zod exact' off)");
            if (!on) { End("oz_v14_fog_of_war_play.txt"); yield break; }

            float t0 = Time.realtimeSinceStartup;
            while (f.GateWhy() != null && Time.realtimeSinceStartup - t0 < 90f) yield return null;
            string gate = f.GateWhy();
            yield return Seconds(OzFogOfWar.FadeInSeconds + 0.6f);
            Row("P3", gate == null && OzFogOfWar.Strength >= 0.999f, gate == null ? $"the camera is the player's, the fog faded in: strength {OzFogOfWar.Strength:0.00}" : $"still {gate} after 90 s (skip the briefing with SPACE and run again)");

            var mat = OzFogOfWar.Mat;
            var dt4 = OzFogOfWar.DistTexture;
            Row("P4", mat && mat.shader && mat.shader.isSupported && !ShaderUtil.ShaderHasError(mat.shader) && OzFogOfWar.FieldTexture && dt4 && OzFogOfWar.Prewarms > 0, mat ? $"shader {mat.shader.name}: supported, no errors, {mat.shader.passCount} passes; the soft edge field {(OzFogOfWar.FieldTexture ? OzFogOfWar.FieldTexture.width + " x " + OzFogOfWar.FieldTexture.height + " " + OzFogOfWar.FieldTexture.graphicsFormat : "MISSING")}, the signed distance {(dt4 ? dt4.width + " x " + dt4.height + " " + dt4.graphicsFormat : "MISSING")}, {OzFogOfWar.FieldRuns} runs; the pass prewarmed {OzFogOfWar.Prewarms} times" : "no material: " + (OzFogOfWar.MatWhy ?? "shader OpenZ/FogOfWar not found"));

            int q0 = OzFogOfWar.PassesQueued, r0 = OzFogOfWar.PassesRecorded, d0 = OzFogOfWar.PassesDrawn;
            yield return Frames(30);
            int q = OzFogOfWar.PassesQueued - q0, r = OzFogOfWar.PassesRecorded - r0, d = OzFogOfWar.PassesDrawn - d0;
            Row("P5", q >= 25 && r >= 25 && d >= 25, $"30 frames: the pass put into the player's camera {q} times, recorded {r}, drawn {d} (each at least 25)");

            var mask = OzFogOfWar.Mask;
            var snap = (byte[])mask.Target.Clone();
            yield return Seconds(5f);
            int down = 0, over = 0;
            for (int i = 0; i < snap.Length; i++) { if (mask.Target[i] < snap[i]) down++; if (mask.Shown[i] > mask.Target[i]) over++; }
            Row("P6", down == 0 && over == 0, $"explore-once over 5 s of play: explored cells that went down {down}, shown cells above the explored map {over} (both 0); explored now {mask.ExploredShare() * 100f:0.0} %");

            int own = 0, closed = 0;
            foreach (var o in W.Objects) if (o.Owner == player && o.IsUnit && !o.Destroyed && o.KillAt < 0) { own++; if (mask.ShownAtPx(o.CenterX, o.CenterY) < OzFowMask.Revealed) closed++; }
            Row("P7", own > 0 && closed == 0, $"every unit of the player's side stands in open ground: {own} units, {closed} under the fog");

            f.CoverAgain();                                                             // the enemy's ground covered again (its base at least)
            yield return Seconds(OzFogOfWar.RevealSeconds + 0.5f);
            int covered = 0; ZObject one = null;
            foreach (var v in g.LiveViewValues)
            {
                var o = v ? v.Obj : null; if (o == null || o.Owner == player || !o.IsUnit) continue;
                if (mask.ShownAtPx(o.CenterX, o.CenterY) < OzFowMask.Revealed && OzFogOfWar.HeldUnder(o)) { covered++; one ??= o; }   // 1.4 mayhem_headstart: the early band's units are drawn (EarlyViews)
            }
            yield return Frames(2);
            int hv = OzFogOfWar.HiddenViews, ho = OzFogOfWar.HiddenSkippedOutside;
            Row("P8", covered >= 1 && Mathf.Abs(hv + ho - covered) <= 2, $"units of other sides in covered ground {covered}: held off on the player's camera's last frame {hv} views / {OzFogOfWar.HiddenRenderers} renderers, {ho} left alone outside its view and their shadows' reach (together within 2 of {covered})" + (covered == 0 ? " (none in covered ground: drop an enemy far away with Alt+P and run again)" : ""));

            yield return Frames(3);
            int heldAfter = HeldOff(g);
            Row("P9", OzFogOfWar.RestoreSurprises == 0 && Mathf.Abs(heldAfter - heldBefore) <= 2, $"every renderer given back after each frame: held off outside the camera before the fog {heldBefore}, now {heldAfter} (other scripts' own, within 2), surprises {OzFogOfWar.RestoreSurprises}");

            bool ownFree = true; foreach (var o in W.Objects) if (o.Owner == player && OzFogOfWar.Covers(o)) ownFree = false;
            Row("P10", (one == null || OzFogOfWar.Covers(one)) && ownFree && one != null, $"the hooks' question: a covered enemy {(one != null ? one.Type + " #" + one.Id : "-")} is covered (no hover, bar, badge or marker), no object of the player's side ever is");

            var mc = OzFogOfWar.MinimapCover; var mt = mc ? mc.texture : null;
            var markers = mc ? mc.transform.parent.Find("Markers") : null;
            Row("P11", mc && mc.enabled && mt && mt.width == mask.W && mt.height == mask.H && markers && mc.transform.GetSiblingIndex() > markers.GetSiblingIndex(),
                mc ? $"the minimap cover: on, {(mt ? mt.width + " x " + mt.height : "-")}, over the markers (index {mc.transform.GetSiblingIndex()} > {(markers ? markers.GetSiblingIndex() : -1)}), {OzFogOfWar.MinimapPaints} paints" : "no minimap cover (the HUD's minimap missing?)");

            bool looks = true; var sb = new StringBuilder(); int lookWas = OzFogOfWar.LookIndex;
            sb.Append($"planet {OzFogOfWar.PlanetName} ({g.level}): ");
            for (int i = 0; i < OzFogOfWar.Looks.Length; i++)
            {
                OzFogOfWar.LookIndex = i; yield return Frames(2);
                bool ok = OzFogOfWar.MaterialHasLook(i); looks &= ok; sb.Append($"{OzFogOfWar.Looks[i].Name} {(ok ? "on the material" : "MISSING")}; ");
            }
            OzFogOfWar.LookIndex = OzFogOfWar.Recommended; yield return Frames(2);
            bool rec = OzFogOfWar.MaterialHasLook(OzFogOfWar.Recommended);
            OzFogOfWar.LookIndex = lookWas; yield return Frames(2);
            Row("P12", looks && rec, sb + $"the recommended {OzFogOfWar.Looks[OzFogOfWar.Recommended].Name} again, then back to {OzFogOfWar.LookName} (OPTIONS FOG LOOK {OzFogLookOption.Name})");

            OzFogOfWar.CostReset(); yield return Frames(300);
            var c = OzFogOfWar.CostMs();
            Row("P13", c[5] < 0.25 && c[6] < 2.0, $"cost over 300 frames (processor, ms a frame): stamp {c[0]:0.000}, reveal {c[1]:0.000}, upload {c[2]:0.000}, hide {c[3]:0.000}, minimap {c[4]:0.000}, total {c[5]:0.000} (bar 0.25 in the editor; the exe's per-minute line: 0.15), worst frame {c[6]:0.000} (bar 2.0); {W.Objects.Count} objects");

            f.SetForced(wasWanted, out _);
            yield return Frames(3);
            Row("P14", wasWanted || (OzFogOfWar.Strength == 0f && OzFogOfWar.HiddenViews >= 0 && (!mc || !mc.enabled)), $"the match put back as it started: fog {(OzFogOfWar.Wanted ? "ON" : "off")}, strength {OzFogOfWar.Strength:0.00}, minimap cover {(mc && mc.enabled ? "on" : "off")}");
            End("oz_v14_fog_of_war_play.txt");
        }

        /// <summary>Renderers of live unit views held off outside the camera's drawing (by other scripts; the fog gives its own back).</summary>
        static int HeldOff(ZGame g)
        {
            int n = 0; var list = new List<Renderer>();
            foreach (var v in g.LiveViewValues) { if (!v) continue; v.GetComponentsInChildren(true, list); foreach (var r in list) if (r && r.forceRenderingOff) n++; }
            return n;
        }

        [MenuItem("OpenZ/V1.4/Fog of War: Look Frames (in Play)")]
        public static void LookFrames() => Run(LookFramesRoutine(false));

        [MenuItem("OpenZ/V1.4/Fog of War: Edge Frames (in Play)")]
        public static void EdgeFrames() => Run(LookFramesRoutine(true));

        static IEnumerator LookFramesRoutine(bool edge)
        {
            Begin(edge ? "Edge Frames: each style's edge map (debug view 2), for the judges" : "Look Frames: the game view with the fog off and with each style");
            string log = edge ? "oz_v14_fog_of_war_edges.txt" : "oz_v14_fog_of_war_frames.txt";
            var f = OzFogOfWar.I;
            if (!f) { Row("F0", false, "no level running"); End(log); yield break; }
            bool wasWanted = OzFogOfWar.Wanted; int wasLook = OzFogOfWar.LookIndex;
            var dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "fog_of_war_looks"));
            Directory.CreateDirectory(dir);
            if (!edge)
            {
                f.SetForced(false, out _); yield return Frames(20);
                yield return Shot(Path.Combine(dir, "fog_0_off.png"));
            }
            if (!f.SetForced(true, out var refused)) { Row("F1", false, "refused: " + refused); End(log); yield break; }
            yield return Seconds(OzFogOfWar.FadeInSeconds + 0.5f);
            var names = new List<string>();
            for (int i = 0; i < OzFogOfWar.Looks.Length; i++)
            {
                OzFogOfWar.LookIndex = i; OzFogOfWar.DebugMode = edge ? 2 : 0; yield return Frames(20);
                string name = edge ? "edge_" + OzFogOfWar.Looks[i].File + ".png" : "fog_" + (i + 1) + "_" + OzFogOfWar.Looks[i].File + ".png";
                yield return Shot(Path.Combine(dir, name));
                names.Add(name);
                Say($"  {OzFogOfWar.Looks[i].Name} ({OzFogOfWar.PlanetName}): {name}");
            }
            OzFogOfWar.DebugMode = 0;
            OzFogOfWar.LookIndex = wasLook; f.SetForced(wasWanted, out _);
            Row("F1", true, $"{names.Count + (edge ? 0 : 1)} frames in Logs/fog_of_war_looks ({(edge ? "" : "fog_0_off.png, ")}{string.Join(", ", names)}); put the camera on the edge of the explored ground first (Alt+P COVER AGAIN shows the reveal)");
            End(log);
        }

        // ================================================================ Edge Count (editor, no Play)
        static double DistOf(float s) { double v = Mathf.Clamp(s, 1e-5f, 1f - 1e-5f); return -OzFogOfWar.EdgeK * System.Math.Log(v / (1 - v)); }
        // round 4: each sheet has its own eased ramp (smootherstep from D0 to D1 m past the hiding line); a style's cover is the largest of
        // its four, the edge wander opening up to WanderM more
        static double Sm5(double t) { t = System.Math.Max(0, System.Math.Min(1, t)); return t * t * t * (t * (t * 6 - 15) + 10); }
        static double CovOf(double d, OzFogOfWar.Style k)
        {
            double c = 0;
            foreach (var l in k.L) c = System.Math.Max(c, Sm5((d - l.D0) / System.Math.Max(0.5, l.D1 - l.D0)));
            return c;
        }
        /// <summary>How deep inside the explored ground (m) a style's lowest ramp can start (its deepest D0); the wander only opens.</summary>
        static double DeepestIn(OzFogOfWar.Style k) { double m = 0; foreach (var l in k.L) m = System.Math.Min(m, l.D0); return -m; }

        [MenuItem("OpenZ/V1.4/Fog of War: Edge Count (in editor)")]
        public static void EdgeCount()
        {
            Begin("Edge Count (in the editor): the soft edge field on the graphics card from made-up reveal maps");
            const int Mw = 220, Mh = 256;                                                  // level 17's reveal map (cells, 1 m)
            int fw, fh; int pad = OzFogOfWar.FieldPad; float tm = OzFogOfWar.FieldTexelM;
            double X(int i) => i * tm + tm * 0.5 - pad * tm;                             // a field texel's centre (m)

            // E1: a straight edge at x = 110 m (cells 0..109 open), d = 0 there and rising monotonically
            var m1 = new OzFowMask(Mw * OzFowMask.CellPx, Mh * OzFowMask.CellPx);
            for (int y = 0; y < Mh; y++) for (int x = 0; x < 110; x++) { m1.Target[y * Mw + x] = 255; m1.Shown[y * Mw + x] = 255; }
            var f1 = OzFogOfWar.FieldOf(m1, out fw, out fh);
            if (f1 == null) { Row("E0", false, "no material or no read-back: " + OzFogOfWar.MatWhy); End("oz_v14_fog_of_war_edge.txt"); return; }
            int row = fh / 2; double zero = double.NaN; bool mono = true; double prev = double.NegativeInfinity;
            for (int i = 0; i < fw; i++)
            {
                double x = X(i); if (x < 60 || x > 160) continue;
                double d = DistOf(f1[row * fw + i]);
                if (d < prev - 1e-6) mono = false;
                if (double.IsNaN(zero) && prev < 0 && d >= 0) { double x0 = X(i - 1); zero = x0 + (0 - prev) / (d - prev) * (x - x0); }
                prev = d;
            }
            Row("E1", !double.IsNaN(zero) && System.Math.Abs(zero - 110) <= 0.75 && mono, $"a straight edge at the Shown = 128 line x = 110.0 m: d = 0 at x = {zero:0.00} m (bar +-0.75), d rises monotonically from 60 to 160 m: {(mono ? "yes" : "NO")} (field {fw} x {fh})");

            // E2: deeper than a style's deepest ramp start inside the contour the cover is exactly 0, every style
            var sb = new StringBuilder(); bool e2 = true;
            foreach (var k in OzFogOfWar.Looks)
            {
                int bad = 0, n = 0; double deep = DeepestIn(k);
                for (int j = pad + 8; j < fh - pad - 8; j++) for (int i = 0; i < fw; i++)
                {
                    double x = X(i); if (x < 0 || x > 110 - deep - 0.75) continue;
                    n++; if (CovOf(DistOf(f1[j * fw + i]), k) != 0) bad++;
                }
                e2 &= bad == 0; sb.Append($"{k.Name} (from {deep:0} m) {bad} of {n} texels over 0; ");
            }
            Row("E2", e2, $"explored ground deeper than each style's deepest ramp start inside the contour (+ the 0.75 m of E1; the edge wander opens up to {OzFogOfWar.WanderM:0.0} m more) has cover exactly 0: " + sb);

            // E3: a grunt's 23 m circle: cover 0 at its centre, the d = 0 circle at most 2 m inside the Shown = 128 circle
            var m3 = new OzFowMask(Mw * OzFowMask.CellPx, Mh * OzFowMask.CellPx);
            int cx = Mw / 2 * OzFowMask.CellPx, cy = Mh / 2 * OzFowMask.CellPx;
            m3.Stamp(cx, cy, 184); m3.ShowAll();
            var f3 = OzFogOfWar.FieldOf(m3, out fw, out fh);
            int ccx = m3.CellX(cx), ccy = m3.CellY(cy);
            double r128 = double.NaN;
            for (int x = ccx; x < ccx + 40; x++) { int a = m3.Shown[ccy * Mw + x], b = m3.Shown[ccy * Mw + x + 1]; if (a >= 128 && b < 128) { r128 = (x - ccx) + (a - 128.0) / (a - b); break; } }
            double centreX = ccx + 0.5, centreZ = ccy + 0.5;                              // the circle's centre in metres (x and -z)
            int ci = (int)System.Math.Round((centreX + pad * tm) / tm - 0.5), cj = (int)System.Math.Round((centreZ + pad * tm) / tm - 0.5);
            double rd = double.NaN, prevD = double.NegativeInfinity, prevX = 0;
            for (int i = ci; i < fw; i++)
            {
                double x = X(i) - centreX; double d = DistOf(f3[cj * fw + i]);
                if (prevD < 0 && d >= 0 && i > ci) { rd = prevX + (0 - prevD) / (d - prevD) * (x - prevX); break; }
                prevD = d; prevX = x;
            }
            double dc = DistOf(f3[cj * fw + ci]); bool c0 = true; var sb3 = new StringBuilder();
            foreach (var k in OzFogOfWar.Looks) { double c = CovOf(dc, k); c0 &= c == 0; sb3.Append($"{k.Name} {c:0.000}; "); }
            Row("E3", c0 && !double.IsNaN(rd) && r128 - rd <= 2.0 && r128 - rd >= -0.75, $"a grunt's 184 px circle: the Shown = 128 radius {r128:0.00} m, the d = 0 radius {rd:0.00} m (shrinks {r128 - rd:0.00} m, bar 2), d at the centre {dc:0.0} m, cover there: {sb3}");

            // E4: a further stamp never lowers the field
            var m4 = new OzFowMask(Mw * OzFowMask.CellPx, Mh * OzFowMask.CellPx);
            m4.Stamp(cx, cy, 184); m4.ShowAll();
            var fa = OzFogOfWar.FieldOf(m4, out fw, out fh);
            m4.Stamp(cx + 30 * OzFowMask.CellPx, cy + 10 * OzFowMask.CellPx, 264); m4.ShowAll();
            var fb = OzFogOfWar.FieldOf(m4, out fw, out fh);
            double worst = 0; int lower = 0;
            for (int i = 0; i < fa.Length; i++) { double dd = fb[i] - fa[i]; if (dd < worst) worst = dd; if (dd < -1.0 / 65535.0) lower++; }
            Row("E4", lower == 0, $"a further stamp (a howitzer's 264 px, overlapping): texels lower than before by more than 1 step (1 / 65535) {lower}, the largest drop {-worst * 65535:0.00} steps");

            // E5: 20 m outside the map the cover is 1 (the circle in the middle; and, for information, a map edge explored to the border)
            bool e5 = true; var sb5 = new StringBuilder();
            double[] outs = { -20, Mw + 20 };
            foreach (var k in OzFogOfWar.Looks)
            {
                double mn = 1;
                foreach (double ox in outs) { int i = (int)System.Math.Round((ox + pad * tm) / tm - 0.5); mn = System.Math.Min(mn, CovOf(DistOf(f3[cj * fw + i]), k)); }
                e5 &= mn >= 0.9999; sb5.Append($"{k.Name} {mn:0.0000}; ");
            }
            int ie = (int)System.Math.Round((Mw + 20 + pad * tm) / tm - 0.5);
            var m5 = new OzFowMask(Mw * OzFowMask.CellPx, Mh * OzFowMask.CellPx);
            for (int y = 0; y < Mh; y++) for (int x = 150; x < Mw; x++) { m5.Target[y * Mw + x] = 255; m5.Shown[y * Mw + x] = 255; }
            var f5 = OzFogOfWar.FieldOf(m5, out fw, out fh);
            double dEdge = DistOf(f5[(fh / 2) * fw + ie]);
            Row("E5", e5, $"20 m outside the map: cover {sb5}(info: with the map's east edge explored to the border, d there is {dEdge:0.0} m, cover {CovOf(dEdge, OzFogOfWar.Looks[0]):0.00} for CLOUD SEA: the 20 m closing)");

            // E6: the field's three passes at level 17's size on the graphics card (201 runs against 1, read back each time)
            var sw = Stopwatch.StartNew(); OzFogOfWar.FieldOf(m4, out fw, out fh, 1); double t1 = sw.Elapsed.TotalMilliseconds;
            sw.Restart(); OzFogOfWar.FieldOf(m4, out fw, out fh, 201); double t201 = sw.Elapsed.TotalMilliseconds;
            double per = (t201 - t1) / 200.0;
            Row("E6", per <= 0.05, $"the field (down + blur H + blur V + the signed distance) at level 17's size: {per:0.0000} ms a run (201 runs {t201:0.0} ms against 1 run {t1:0.0} ms; includes the command submission, so an upper bound of the GPU time; bar 0.05)");
            End("oz_v14_fog_of_war_edge.txt");
        }

        [MenuItem("OpenZ/V1.4/Fog of War: Cost Bench (in Play, about 3 min)")]
        public static void CostBench() => Run(CostBenchRoutine());

        // fix round 2 (5 Nov): the passes' GPU time is timed directly: the passes alone, submitted 41 times and once into scratch targets with
        // the player camera's matrices, each waited on until the graphics card has finished, (41 - 1) / 40, the median of 5. A frame's GPU
        // time hides added GPU work while the processor is the slower side (round 1's frame-based figures came out noisy, one negative), so
        // frame GPU with the fog on minus off is information only (B2, interleaved off / on phases, so a drift over the run cancels).
        const int Reps = 41;

        static IEnumerator Measure(double[] outGpu, double[] outCpu, int i, int frames)
        {
            var timings = new FrameTiming[1]; double gs = 0, cs = 0; int n = 0;
            for (int k = 0; k < frames; k++)
            {
                FrameTimingManager.CaptureFrameTimings();
                if (FrameTimingManager.GetLatestTimings(1, timings) > 0 && timings[0].gpuFrameTime > 0) { gs += timings[0].gpuFrameTime; cs += timings[0].cpuMainThreadFrameTime; n++; }
                yield return null;
            }
            outGpu[i] = n > 0 ? gs / n : 0; outCpu[i] = n > 0 ? cs / n : 0;
        }

        /// <summary>A stand-in for the camera's depth texture at w x h: the device depth of the floor plane under every pixel (row 0 = the
        /// top of the screen, as the camera's own depth texture on D3D), so the composite takes its ground path (fill, shadows, the explored
        /// surface test) as in the game.</summary>
        static Texture2D DepthStub(Camera cam, int w, int h, float floorY)
        {
            var t = new Texture2D(w, h, TextureFormat.RFloat, false, true) { hideFlags = HideFlags.DontSave, filterMode = FilterMode.Point };
            var data = new float[w * h];
            var gp = GL.GetGPUProjectionMatrix(cam.projectionMatrix, true) * cam.worldToCameraMatrix;
            var c2w = cam.cameraToWorldMatrix; Vector3 o = cam.transform.position;
            float ty = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad), tx = ty * w / (float)h;
            for (int r = 0; r < h; r++)
            {
                float ny = 1f - 2f * (r + 0.5f) / h;
                for (int x = 0; x < w; x++)
                {
                    float nx = 2f * (x + 0.5f) / w - 1f;
                    Vector3 d = c2w.MultiplyVector(new Vector3(nx * tx, ny * ty, -1f));
                    float z = 0f;
                    if (d.y < -1e-3f) { var p = o + d * ((o.y - floorY) / -d.y); var cp = gp * new Vector4(p.x, p.y, p.z, 1f); z = cp.z / cp.w; }
                    data[r * w + x] = z;
                }
            }
            t.SetPixelData(data, 0); t.Apply(false, false);
            return t;
        }

        /// <summary>The wall-clock time (ms) of n submissions of one part, waited on until the card has finished: 0 the sheets pass (n draws
        /// into a 1920 x 1080 target of the camera's own format and MSAA, as in the game), 2 the soft edge field's four passes.</summary>
        static double PartMs(Camera cam, int n, int which, Texture depth)
        {
            var m = OzFogOfWar.Mat; var f = OzFogOfWar.I; if (!m || !f) return 0;
            const int w = 1920, h = 1080;
            var fd = new RenderTextureDescriptor(w, h, OzFogOfWar.LastColorFormat, 0) { msaaSamples = Mathf.Max(1, OzFogOfWar.LastMsaa) };
            var rtF = new RenderTexture(fd) { hideFlags = HideFlags.DontSave }; rtF.Create();
            var rtW = new RenderTexture(4, 4, 0, UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8A8_UNorm) { hideFlags = HideFlags.DontSave }; rtW.Create();
            var cmd = new UnityEngine.Rendering.CommandBuffer { name = "OZ Fow bench" };
            var proj = GL.GetGPUProjectionMatrix(cam.projectionMatrix, true); var view = cam.worldToCameraMatrix; var vp = proj * view;
            cmd.SetGlobalMatrix("unity_MatrixV", view); cmd.SetGlobalMatrix("glstate_matrix_projection", proj); cmd.SetGlobalMatrix("unity_MatrixVP", vp);
            cmd.SetGlobalMatrix("unity_MatrixInvVP", vp.inverse); cmd.SetGlobalVector("_WorldSpaceCameraPos", cam.transform.position);
            cmd.SetGlobalTexture("_CameraDepthTexture", depth);
            var b = new MaterialPropertyBlock();
            b.SetVector("_BlitScaleBias", new Vector4(1f, 1f, 0f, 0f)); b.SetVector("_OzFowSize", new Vector4(w, h, 1f / w, 1f / h));
            b.SetFloat("_OzFowFmt", OzFogOfWar.LastColorFormat == UnityEngine.Experimental.Rendering.GraphicsFormat.B10G11R11_UFloatPack32 ? 1f : 0f);
            int ps = m.FindPass("Sheets");
            for (int i = 0; i < n && which == 0; i++) { cmd.SetRenderTarget(rtF); cmd.DrawProcedural(Matrix4x4.identity, m, ps, MeshTopology.Triangles, 3, 1, b); }
            // (an MSAA target cannot be read back: a tiny target cleared after the draws is read back instead; its read-back completes on a
            // fence that waits for all the card's earlier work, the draws included)
            cmd.SetRenderTarget(rtW); cmd.ClearRenderTarget(false, true, Color.black);
            var wait = new UnityEngine.Rendering.CommandBuffer { name = "OZ Fow bench wait" }; wait.SetRenderTarget(rtW); wait.ClearRenderTarget(false, true, Color.black);
            Graphics.ExecuteCommandBuffer(wait);
            UnityEngine.Rendering.AsyncGPUReadback.Request(rtW, 0, 0, 1, 0, 1, 0, 1).WaitForCompletion();   // the card's earlier work done first
            var sw = Stopwatch.StartNew();
            if (which == 2) { f.BenchFieldRun(n); Graphics.ExecuteCommandBuffer(wait); } else Graphics.ExecuteCommandBuffer(cmd);
            UnityEngine.Rendering.AsyncGPUReadback.Request(rtW, 0, 0, 1, 0, 1, 0, 1).WaitForCompletion();
            double ms = sw.Elapsed.TotalMilliseconds;
            cmd.Release(); wait.Release(); rtF.Release(); rtW.Release(); Object.DestroyImmediate(rtF); Object.DestroyImmediate(rtW);
            return ms;
        }

        /// <summary>One part's GPU time (ms): (41 runs - 1 run) / 40, the median of 5; a negative or wildly spread result is written out, not hidden.</summary>
        static IEnumerator PartTime(Camera cam, int which, Texture depth, double[] outMs, int slot, StringBuilder notes)
        {
            var v = new List<double>();
            for (int k = 0; k < 5; k++) { v.Add((PartMs(cam, Reps, which, depth) - PartMs(cam, 1, which, depth)) / (Reps - 1)); yield return null; }
            v.Sort(); outMs[slot] = v[2];
            if (v[2] <= 0 || v[4] - v[0] > System.Math.Max(0.2, v[2])) notes.Append($"[{(which == 0 ? "sheets" : "field")} spread {v[0]:0.000}..{v[4]:0.000}] ");
        }

        static IEnumerator CostBenchRoutine()
        {
            Begin("Cost Bench: processor time per style with off phases between (B1, B2 info, B6 info), then the sheets pass's GPU time timed directly: the view as it is (B3), everything covered at the highest camera (B4), soft band almost everywhere (B5)");
            var f = OzFogOfWar.I; var g = ZGame.I;
            if (!f || !g) { Row("B0", false, "no level running"); End("oz_v14_fog_of_war_bench.txt"); yield break; }
            bool wasWanted = OzFogOfWar.Wanted; int wasLook = OzFogOfWar.LookIndex;
            Say($"  {g.level} ({OzFogOfWar.PlanetName}), {Screen.width} x {Screen.height}, graphics {OzQuality.Name}, {g.World.Objects.Count} objects");
            // B1 / B2 / B6: off, S1, off, S2, off, S3, off, on-without-the-pass, off, twice (210 measured frames each after 90)
            const int NoPass = 9;
            var phs = new List<int> { -1 };
            for (int round = 0; round < 2; round++) { for (int s = 0; s < 3; s++) { phs.Add(s); phs.Add(-1); } phs.Add(NoPass); phs.Add(-1); }
            var gpu = new double[phs.Count]; var cpu = new double[phs.Count]; var own = new double[phs.Count];
            for (int ph = 0; ph < phs.Count; ph++)
            {
                int style = phs[ph]; bool on = style >= 0;
                OzFogOfWar.BenchNoPass = style == NoPass;
                if (on && style != NoPass) OzFogOfWar.LookIndex = style;
                if (!f.SetForced(on, out var refused) && on) { OzFogOfWar.BenchNoPass = false; Row("B1", false, "refused: " + refused); End("oz_v14_fog_of_war_bench.txt"); yield break; }
                yield return Frames(90);
                OzFogOfWar.CostReset();
                yield return Measure(gpu, cpu, ph, 210);
                own[ph] = OzFogOfWar.CostMs()[5];
                Say($"  {(style == NoPass ? "on, no pass (B6)" : on ? OzFogOfWar.Looks[style].Name : "off")}: frame GPU {(gpu[ph] > 0 ? gpu[ph].ToString("0.000") : "n/a")} ms, main thread {(cpu[ph] > 0 ? cpu[ph].ToString("0.00") : "n/a")} ms, the fog's own processor time {own[ph]:0.000} ms, hidden {OzFogOfWar.HiddenViews} views ({OzFogOfWar.HiddenSkippedOutside} left alone outside the view)");
            }
            OzFogOfWar.BenchNoPass = false;
            double oc = 0, ocMax = 0; int on6 = 0; for (int ph = 0; ph < phs.Count; ph++) if (phs[ph] >= 0 && phs[ph] != NoPass) { oc += own[ph]; ocMax = System.Math.Max(ocMax, own[ph]); on6++; }
            oc /= System.Math.Max(1, on6);
            Row("B1", oc < 0.25 && ocMax < 0.35, $"the fog's own processor time on: {oc:0.000} ms a frame on average, the highest phase {ocMax:0.000} ms (bars 0.25 and 0.35 in the editor; the exe's per-minute line: 0.15)");
            var sb2 = new StringBuilder();
            for (int s = 0; s <= NoPass; s++)
            {
                if (s >= 3 && s != NoPass) continue;
                double d = 0; int n = 0;
                for (int ph = 1; ph < phs.Count - 1; ph++) if (phs[ph] == s && gpu[ph] > 0 && gpu[ph - 1] > 0 && gpu[ph + 1] > 0) { d += gpu[ph] - 0.5 * (gpu[ph - 1] + gpu[ph + 1]); n++; }
                sb2.Append($"{(s == NoPass ? "B6 on without the pass" : OzFogOfWar.Looks[s].Name)} {(n > 0 ? (d / n).ToString("+0.000;-0.000") : "n/a")} ms; ");
            }
            Say("  B2 / B6 (info): frame GPU with the fog on minus the mean of the off phases on either side: " + sb2 + "(units under the fog are not drawn and a frame's GPU time hides work while the processor is the slower side: information only)");

            var cam = Object.FindFirstObjectByType<RtsCamera>(); var rc = cam ? cam.GetComponent<Camera>() : null;
            if (!rc) { Row("B3", false, "no RtsCamera"); End("oz_v14_fog_of_war_bench.txt"); yield break; }
            float hWas = cam.height;
            var mask = OzFogOfWar.Mask; var saveT = mask != null ? (byte[])mask.Target.Clone() : null; var saveS = mask != null ? (byte[])mask.Shown.Clone() : null;
            f.SetForced(true, out _); yield return Frames(30);
            float floorY = SlabFloor();

            // B3: the view as the player left it, each style; and the field
            var notes = new StringBuilder(); var r3 = new double[7];
            var depth = DepthStub(rc, 1920, 1080, floorY);
            for (int s = 0; s < 3; s++) { OzFogOfWar.LookIndex = s; yield return Frames(20); yield return PartTime(rc, 0, depth, r3, s, notes); }
            yield return PartTime(rc, 2, depth, r3, 6, notes);
            bool ok3 = true; var sb3 = new StringBuilder();
            for (int s = 0; s < 3; s++) { ok3 &= r3[s] <= 1.0 && r3[s] > 0; sb3.Append($"{OzFogOfWar.Looks[s].Name} {r3[s]:0.000} ms; "); }
            ok3 &= r3[6] > 0 && r3[6] <= 0.1;
            Row("B3", ok3, $"GPU time of the sheets pass timed directly, the view as it is, camera {cam.height:0} m, into {OzFogOfWar.LastColorFormat} x{OzFogOfWar.LastMsaa} MSAA (target 1.0, hard cap 1.5): " + sb3 + $"the soft edge field + distance {r3[6]:0.000} ms a run (only on frames where the reveal map changed; bar 0.1) " + notes);

            // B4: everything covered, the camera at its highest
            notes.Clear(); var r4 = new double[3];
            f.CoverAgain(); cam.SetHeightImmediate(cam.maxHeight); yield return Frames(30);
            Object.DestroyImmediate(depth); depth = DepthStub(rc, 1920, 1080, floorY);
            for (int s = 0; s < 3; s++) { OzFogOfWar.LookIndex = s; yield return Frames(20); yield return PartTime(rc, 0, depth, r4, s, notes); }
            double w4 = 0; var sb4 = new StringBuilder(); bool pos4 = true;
            for (int s = 0; s < 3; s++) { w4 = System.Math.Max(w4, r4[s]); pos4 &= r4[s] > 0; sb4.Append($"{OzFogOfWar.Looks[s].Name} {r4[s]:0.000} ms; "); }
            Row("B4", pos4 && w4 <= 1.5, $"worst case, everything covered, camera {cam.height:0} m: " + sb4 + $"(hard cap 1.5, target 1.0: {(w4 <= 1.0 ? "within" : "OVER")}) " + notes);

            // B5: soft band almost everywhere (sight circles of 20 m on a staggered 40 m grid), the camera at the normal 70 m
            notes.Clear(); var r5 = new double[3];
            f.BenchPattern(320, 160); cam.SetHeightImmediate(70f); yield return Frames(30);
            Object.DestroyImmediate(depth); depth = DepthStub(rc, 1920, 1080, floorY);
            for (int s = 0; s < 3; s++) { OzFogOfWar.LookIndex = s; yield return Frames(20); yield return PartTime(rc, 0, depth, r5, s, notes); }
            double w5 = 0; var sb5 = new StringBuilder(); bool pos5 = true;
            for (int s = 0; s < 3; s++) { w5 = System.Math.Max(w5, r5[s]); pos5 &= r5[s] > 0; sb5.Append($"{OzFogOfWar.Looks[s].Name} {r5[s]:0.000} ms; "); }
            Row("B5", pos5 && w5 <= 1.5, $"soft band almost everywhere (20 m sight circles every 40 m), camera 70 m: " + sb5 + $"(hard cap 1.5, target 1.0: {(w5 <= 1.0 ? "within" : "OVER")}) " + notes);
            Object.DestroyImmediate(depth);

            cam.SetHeightImmediate(hWas);
            f.RestoreMask(saveT, saveS);
            OzFogOfWar.LookIndex = wasLook; f.SetForced(wasWanted, out _);
            Say($"  the match put back: the reveal map as before the bench ({(mask != null ? mask.ExploredShare() * 100f : 0f):0.0} % explored), camera {hWas:0} m, look {OzFogOfWar.LookName}, fog {(OzFogOfWar.Wanted ? "ON" : "off")}");
            End("oz_v14_fog_of_war_bench.txt");
        }

        /// <summary>The blanket floor the fog uses (the sheets' floor on the material), for the bench's depth stand-in.</summary>
        static float SlabFloor() { var m = OzFogOfWar.Mat; return m ? m.GetFloat("_OzFowY0") : 0f; }

        // ================================================================ Edge Measure (round 4, in Play)
        /// <summary>Edge Measure: the tag in the file names (the spot: "border", "tilt25", "zoom200" ...).</summary>
        public static string MeasureTag = "border";

        [MenuItem("OpenZ/V1.4/Fog of War: Edge Measure (in Play)")]
        public static void EdgeMeasure() => Run(EdgeMeasureRoutine());

        static IEnumerator EdgeMeasureRoutine()
        {
            Begin("Edge Measure: each style's colour frame, opacity (debug 1), sheet alphas (debug 3) and the cloud's own opacity (debug 4; post effects off), and the fog off, at the camera as it is");
            var f = OzFogOfWar.I; var g = ZGame.I;
            if (!f || !g) { Row("G0", false, "no level running"); End("oz_v14_fog_of_war_measure.txt"); yield break; }
            var rts = Object.FindFirstObjectByType<RtsCamera>(); var cam = rts ? rts.GetComponent<Camera>() : null;
            var ucd = cam ? cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>() : null;
            if (!ucd) { Row("G0", false, "no RTS camera"); End("oz_v14_fog_of_war_measure.txt"); yield break; }
            bool wasWanted = OzFogOfWar.Wanted; int wasLook = OzFogOfWar.LookIndex; bool postWas = ucd.renderPostProcessing;
            var dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "fog_of_war_edges_r4"));
            Directory.CreateDirectory(dir);
            string pre = g.level + "_" + MeasureTag + "_";
            var files = new List<string>();
            f.SetForced(false, out _); yield return Frames(20);
            yield return Shot(Path.Combine(dir, pre + "off.png")); files.Add(pre + "off.png");
            f.SetForced(true, out _); yield return Seconds(OzFogOfWar.FadeInSeconds + 0.4f);
            // the debug frames without the HUD (its badges and toasts are not cloud); the colour frames keep it, as in play
            var hud = new List<Canvas>();
            foreach (var cv in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) if (cv && cv.isRootCanvas && cv.enabled && cv.renderMode != RenderMode.WorldSpace) hud.Add(cv);
            for (int i = 0; i < OzFogOfWar.Looks.Length; i++)
            {
                OzFogOfWar.LookIndex = i; string s = "S" + (i + 1);
                OzFogOfWar.DebugMode = 0; ucd.renderPostProcessing = postWas; foreach (var cv in hud) if (cv) cv.enabled = true; yield return Frames(12);
                yield return Shot(Path.Combine(dir, pre + s + "_color.png")); files.Add(pre + s + "_color.png");
                ucd.renderPostProcessing = false; foreach (var cv in hud) if (cv) cv.enabled = false;
                OzFogOfWar.DebugMode = 1; yield return Frames(6);
                yield return Shot(Path.Combine(dir, pre + s + "_op.png")); files.Add(pre + s + "_op.png");
                OzFogOfWar.DebugMode = 3; yield return Frames(6);
                yield return Shot(Path.Combine(dir, pre + s + "_lay.png")); files.Add(pre + s + "_lay.png");
                OzFogOfWar.DebugMode = 4; yield return Frames(6);
                yield return Shot(Path.Combine(dir, pre + s + "_own.png")); files.Add(pre + s + "_own.png");
            }
            OzFogOfWar.DebugMode = 0; ucd.renderPostProcessing = postWas; foreach (var cv in hud) if (cv) cv.enabled = true;
            OzFogOfWar.LookIndex = wasLook; f.SetForced(wasWanted, out _);
            Row("G1", files.Count == 13, $"{files.Count} frames in Logs/fog_of_war_edges_r4 ({string.Join(", ", files)}), camera {rts.height:0} m, tilt {rts.Tilt:0} deg, focus {rts.focus.x:0},{rts.focus.z:0}");
            End("oz_v14_fog_of_war_measure.txt");
        }

        // ================================================================ Reveal Clips (round 4, in Play)
        /// <summary>Reveal Clips: one planet's spot, "level|scene|camX|camZ|unit|startX|startZ|toX|toZ|orderFrame|frames|fps|crf" (metres; unit =
        /// a vehicle or robot type; the camera at 70 m, pitch 55, held still).</summary>
        public static string ClipSpec = "";
        /// <summary>Reveal Clips: the output folder (reveal_S1_L17.mp4 ...) and the styles to record (0..2).</summary>
        public static string ClipDir = "Logs/fog_of_war_clips"; public static int ClipStyleFrom = 0, ClipStyleTo = 2;
        public static string ClipLast = "";

        [MenuItem("OpenZ/V1.4/Fog of War: Reveal Clips (in Play, one planet, about 4 min)")]
        public static void RevealClips() => Run(RevealRoutine());

        static float Fl(string s) => float.Parse(s, System.Globalization.CultureInfo.InvariantCulture);

        static IEnumerator RevealRoutine()
        {
            Begin("Reveal Clips: " + ClipSpec);
            var p = ClipSpec.Split('|');
            if (p.Length < 13) { Row("R0", false, "ClipSpec needs 13 fields"); End("oz_v14_fog_of_war_clips.txt"); yield break; }
            string level = p[0], scene = p[1], unit = p[4];
            float cx = Fl(p[2]), cz = Fl(p[3]), sx = Fl(p[5]), sz = Fl(p[6]), tx = Fl(p[7]), tz = Fl(p[8]);
            int orderAt = int.Parse(p[9]), frames = int.Parse(p[10]), fps = int.Parse(p[11]), crf = int.Parse(p[12]);
            var outDir = Path.IsPathRooted(ClipDir) ? ClipDir : Path.GetFullPath(Path.Combine(Application.dataPath, "..", ClipDir));
            Directory.CreateDirectory(outDir);
            for (int s = ClipStyleFrom; s <= ClipStyleTo; s++)
            {
                // the level loaded fresh (the same start for every style)
                Time.captureFramerate = 0;
                UnityEngine.SceneManagement.SceneManager.LoadScene(scene);
                yield return Frames(10);
                float t0 = Time.realtimeSinceStartup;
                while ((!ZGame.I || ZGame.I.World == null || !OzFogOfWar.I || ZGame.I.level != level) && Time.realtimeSinceStartup - t0 < 120f) yield return null;
                var g = ZGame.I; var f = OzFogOfWar.I;
                if (!g || g.World == null || !f) { Row("R" + (s + 1), false, "the level did not load: " + scene); break; }
                // the briefing skipped as the SPACE key does, then the flyover / hand-over
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
                if (!rts || !forced) { Row("R" + (s + 1), false, "no RTS camera or the fog refused: " + refused); break; }
                float edgeWas = rts.edgePx; rts.edgePx = 0f;
                System.Action hold = () => { rts.yaw = 0f; rts.focus = new Vector3(cx, rts.focus.y, cz); rts.SetHeightImmediate(70f); };
                hold();
                // the player's own unit at its start (a vehicle, or a robot squad)
                var W = g.World; var known = new HashSet<int>(); foreach (var o in W.Objects) if (o != null) known.Add(o.Id);
                string team = g.player == Team.Red ? "r" : "b";
                int stx = Mathf.RoundToInt(sx / 2f), sty = Mathf.RoundToInt(-sz / 2f);
                string staged = CaptureKit.StageBattle($"{team} {unit} {stx} {sty}", 1f);
                ZObject lead = null;
                foreach (var o in W.Objects) { if (o == null || known.Contains(o.Id) || !o.IsUnit || o.Owner != g.player) continue; if (lead == null || o.Minions.Count > 0) lead = o; }
                if (lead == null) { rts.edgePx = edgeWas; Row("R" + (s + 1), false, "the unit was not staged: " + staged); break; }
                // the fog fades in, the unit's own sight eases open (real time), the camera held
                float tw = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - tw < 3f) { hold(); yield return null; }
                var pxT = ZGame.WorldToPx(new Vector3(tx, 0f, tz));
                string order = "";
                string file = Path.Combine(outDir, $"reveal_S{s + 1}_{level}.mp4");
                var rec = OzFowClipRecorder.Begin(file, frames, fps, crf, i =>
                {
                    hold();
                    if (i == orderAt && lead != null && !lead.Destroyed)
                        order = W.SendWaypoints(lead.Owner, lead, new List<ZWaypoint> { ZWaypoint.Move(pxT.x, pxT.y) }) ? "move order given" : "move order REFUSED";
                });
                if (rec == null) { rts.edgePx = edgeWas; Row("R" + (s + 1), false, "the recorder did not start"); break; }
                while (rec.Running) yield return null;                                   // (a managed reference: its result outlives its game object)
                string res = rec.Result;
                rts.edgePx = edgeWas;
                var cw = g.CenterWorld(lead);
                ClipLast = res;
                Row("R" + (s + 1), res.StartsWith("OK") && order == "move order given", $"{OzFogOfWar.Looks[s].Name} {level}: {res}; {staged}; {lead.Type} #{lead.Id} {order} at frame {orderAt}, now at {cw.x:0},{cw.z:0} (target {tx:0},{tz:0}); explored {OzFogOfWar.Mask.ExploredShare() * 100f:0.0} %");
                yield return Frames(5);
            }
            Time.captureFramerate = 0;
            OzFogOfWar.SessionLook = -1;
            End("oz_v14_fog_of_war_clips.txt");
        }

        // ================================================================ Colour Grid (5 Nov: the owner's colour and see-through ideas, in Play)
        /// <summary>Colour Grid: the style on screen (<see cref="GridStyle"/>, 0..2).</summary>
        public static int GridStyle = 0;
        /// <summary>Colour Grid: the clip frame the game is paused after (480 = 8.0 s at 60 fps, the owner's moment).</summary>
        public static int GridFrame = 480;
        /// <summary>Colour Grid: the output folder (grid_S1_L17_C0_100.png ...).</summary>
        public static string GridDir = "Logs/fog_colour_grid";
        /// <summary>Colour Grid: fit the colour ideas' multipliers instead of the stills (the planet's palette is put back after).</summary>
        public static bool GridCalibrate;
        /// <summary>Colour Grid, calibrate: the planet's ground colour measured in its fog-off frame, CIE Lab "L|a|b".</summary>
        public static string GridGround = "";
        /// <summary>Colour Grid, calibrate: the fitted multipliers, "LAND r g b|AIR r g b|DUSK r g b" (linear).</summary>
        public static string GridFit = "";

        [MenuItem("OpenZ/V1.4/Fog of War: Colour Grid (in Play, one planet and style, about 2 min)")]
        public static void ColourGrid() => Run(GridRoutine());

        // the held frame: the sheets pass's drift offsets and the signed distance put back before every render, and the reveal map held as
        // it was shown on that frame (its explored map set to it, so nothing eases on: the units held off, the hover, the bars and the
        // minimap stay as on that frame; the real explored map is put back when the hold ends)
        static Vector4[] fzOfs, fzOfs2; static Vector4 fzFlowP, fzGlowP; static RenderTexture fzDist; static byte[] fzShown, fzTarget;
        static readonly int FzOfs = Shader.PropertyToID("_OzFowOfs"), FzOfs2 = Shader.PropertyToID("_OzFowOfs2"), FzFlowP = Shader.PropertyToID("_OzFowFlowP"),
                            FzGlowP = Shader.PropertyToID("_OzFowGlowP"), FzDist = Shader.PropertyToID("_OzFowDist");

        static bool FreezeLook()
        {
            var m = OzFogOfWar.Mat; var d = OzFogOfWar.DistTexture; var mk = OzFogOfWar.Mask;
            if (!m || !d || mk == null) return false;
            fzOfs = m.GetVectorArray(FzOfs); fzOfs2 = m.GetVectorArray(FzOfs2); fzFlowP = m.GetVector(FzFlowP); fzGlowP = m.GetVector(FzGlowP);
            fzDist = new RenderTexture(d.descriptor) { name = "OZ_FowDistHeld", hideFlags = HideFlags.DontSave };
            fzDist.Create();
            Graphics.CopyTexture(d, fzDist);
            fzShown = (byte[])mk.Shown.Clone(); fzTarget = (byte[])mk.Target.Clone();
            HoldMask();
            UnityEngine.Rendering.RenderPipelineManager.beginContextRendering -= HoldLook;
            UnityEngine.Rendering.RenderPipelineManager.beginContextRendering += HoldLook;
            return fzOfs != null && fzOfs.Length >= 4;
        }

        static void HoldMask()
        {
            var mk = OzFogOfWar.Mask; if (mk == null || fzShown == null || fzShown.Length != mk.Shown.Length) return;
            System.Buffer.BlockCopy(fzShown, 0, mk.Shown, 0, fzShown.Length); System.Buffer.BlockCopy(fzShown, 0, mk.Target, 0, fzShown.Length);
        }

        static void HoldLook(UnityEngine.Rendering.ScriptableRenderContext ctx, List<Camera> cams)
        {
            var m = OzFogOfWar.Mat; if (!m || fzOfs == null) return;
            m.SetVectorArray(FzOfs, fzOfs); m.SetVectorArray(FzOfs2, fzOfs2); m.SetVector(FzFlowP, fzFlowP); m.SetVector(FzGlowP, fzGlowP);
            if (fzDist) m.SetTexture(FzDist, fzDist);
            HoldMask();
            ParkPointer();
        }

        /// <summary>The game's pointer parked off the screen for the stills (the Game view keeps the last mouse position it saw; a stale
        /// position over a unit draws that unit's hover box and name). Only the game's own mouse state: the real cursor is not touched.</summary>
        static void ParkPointer()
        {
            var ms = UnityEngine.InputSystem.Mouse.current;
            if (ms != null && ms.position.ReadValue() != new Vector2(-4000f, -4000f)) UnityEngine.InputSystem.LowLevel.InputState.Change(ms.position, new Vector2(-4000f, -4000f));
        }

        static void ThawLook()
        {
            UnityEngine.Rendering.RenderPipelineManager.beginContextRendering -= HoldLook;
            fzOfs = fzOfs2 = null;
            var m = OzFogOfWar.Mat; if (m && OzFogOfWar.DistTexture) m.SetTexture(FzDist, OzFogOfWar.DistTexture);
            if (fzDist) { fzDist.Release(); Object.DestroyImmediate(fzDist); fzDist = null; }
            if (OzFogOfWar.I && fzTarget != null && fzShown != null) OzFogOfWar.I.RestoreMask(fzTarget, fzShown);   // the explored map of that frame again
            fzShown = fzTarget = null;
        }

        // colour maths (sRGB D65, CIE Lab)
        static double SrgbToLin(double c) => c <= 0.04045 ? c / 12.92 : System.Math.Pow((c + 0.055) / 1.055, 2.4);
        static double LinToSrgb(double c) { c = System.Math.Max(0, System.Math.Min(1, c)); return c <= 0.0031308 ? c * 12.92 : 1.055 * System.Math.Pow(c, 1 / 2.4) - 0.055; }
        static Vector3 LinToLab(Vector3 l)
        {
            double x = (0.4124564 * l.x + 0.3575761 * l.y + 0.1804375 * l.z) / 0.95047, y = 0.2126729 * l.x + 0.7151522 * l.y + 0.0721750 * l.z,
                   z = (0.0193339 * l.x + 0.1191920 * l.y + 0.9503041 * l.z) / 1.08883;
            double F(double t) => t > 0.008856451679 ? System.Math.Pow(t, 1.0 / 3.0) : t / 0.12841854934 + 0.137931034;
            double fx = F(x), fy = F(y), fz = F(z);
            return new Vector3((float)(116 * fy - 16), (float)(500 * (fx - fy)), (float)(200 * (fy - fz)));
        }
        static Vector3 LabToLin(Vector3 lab)
        {
            double fy = (lab.x + 16) / 116.0, fx = fy + lab.y / 500.0, fz = fy - lab.z / 200.0;
            double G(double f) => f > 0.206896552 ? f * f * f : 0.12841854934 * (f - 0.137931034);
            double x = G(fx) * 0.95047, y = G(fy), z = G(fz) * 1.08883;
            double r = 3.2404542 * x - 1.5371385 * y - 0.4985314 * z, g = -0.9692660 * x + 1.8760108 * y + 0.0415560 * z, b = 0.0556434 * x - 0.2040259 * y + 1.0572252 * z;
            return new Vector3((float)System.Math.Max(0, r), (float)System.Math.Max(0, g), (float)System.Math.Max(0, b));
        }
        static string Hex(Vector3 lin) => string.Format("{0:X2}{1:X2}{2:X2}", (int)System.Math.Round(LinToSrgb(lin.x) * 255), (int)System.Math.Round(LinToSrgb(lin.y) * 255), (int)System.Math.Round(LinToSrgb(lin.z) * 255));
        static string V3(Vector3 v) => string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0.0000}f, {1:0.0000}f, {2:0.0000}f", v.x, v.y, v.z);
        static string Lab3(Vector3 v) => string.Format(System.Globalization.CultureInfo.InvariantCulture, "({0:0.0}, {1:0.0}, {2:0.0})", v.x, v.y, v.z);
        static readonly float[] LinLut = MakeLut();
        static float[] MakeLut() { var t = new float[256]; for (int i = 0; i < 256; i++) t[i] = (float)SrgbToLin(i / 255.0); return t; }
        // the HUD boxes at 1920 x 1080 (x, y from the top left, w, h): top bar and toast line, MENU, groups row, unit panel, minimap
        static readonly RectInt[] GridHud = { new RectInt(565, 0, 791, 162), new RectInt(1785, 8, 135, 50), new RectInt(10, 995, 725, 75), new RectInt(1075, 870, 490, 200), new RectInt(1565, 700, 355, 380) };

        static Texture2D LoadPng(string path) { var t = new Texture2D(2, 2, TextureFormat.RGBA32, false); t.LoadImage(File.ReadAllBytes(path)); return t; }

        /// <summary>The deep cloud: the cloud's own opacity (debug 4, post effects and HUD off) at least 0.95, the HUD boxes left out.</summary>
        static bool[] DeepMask(Texture2D dbg, out int n)
        {
            var px = dbg.GetPixels32(); int w = dbg.width, h = dbg.height; var m = new bool[px.Length]; n = 0;
            for (int i = 0; i < px.Length; i++) m[i] = px[i].r >= 250;
            foreach (var b in GridHud)
                for (int y = b.yMin; y < b.yMax && y < h; y++)
                    for (int x = b.xMin; x < b.xMax && x < w; x++) m[(h - 1 - y) * w + x] = false;          // the texture's row 0 is the bottom
            for (int i = 0; i < m.Length; i++) if (m[i]) n++;
            return m;
        }

        static Vector3 MeanLin(Texture2D t, bool[] m)
        {
            var px = t.GetPixels32(); double r = 0, g = 0, b = 0; int n = 0;
            for (int i = 0; i < px.Length && i < m.Length; i++) if (m[i]) { r += LinLut[px[i].r]; g += LinLut[px[i].g]; b += LinLut[px[i].b]; n++; }
            return n > 0 ? new Vector3((float)(r / n), (float)(g / n), (float)(b / n)) : Vector3.zero;
        }

        static void SetTint(int planet, int idea, Vector3 v)
        {
            if (idea == OzFogOfWar.ColourLand) OzFogOfWar.Palettes[planet].Land = v;
            else if (idea == OzFogOfWar.ColourAir) OzFogOfWar.Palettes[planet].Air = v;
            else if (idea == OzFogOfWar.ColourDusk) OzFogOfWar.Palettes[planet].Dusk = v;
        }

        /// <summary>The clip's fixed step without a recording, in OzFowClipRecorder's own order (the cue before each frame, then the end of
        /// that frame), run as a coroutine of the level's fog component.</summary>
        static IEnumerator FixedSteps(int frames, int fps, System.Action<int> onFrame, bool[] done)
        {
            int capWas = Time.captureFramerate;
            Time.captureFramerate = fps;
            for (int i = 0; i < frames; i++)
            {
                try { onFrame(i); } catch (System.Exception e) { Debug.LogError($"{Tag} fixed step cue at frame {i}: {e}"); }
                yield return new WaitForEndOfFrame();
            }
            Time.captureFramerate = capWas;
            done[0] = true;
        }

        static IEnumerator GridRoutine()
        {
            Begin($"Colour Grid: {ClipSpec}, style {GridStyle}, paused after frame {GridFrame}{(GridCalibrate ? ", calibrate (ground Lab " + GridGround + ")" : "")}");
            const string log = "oz_v14_fog_colour_grid.txt";
            var p = ClipSpec.Split('|');
            if (p.Length < 13) { Row("K0", false, "ClipSpec needs 13 fields"); End(log); yield break; }
            string level = p[0], scene = p[1], unit = p[4];
            float cx = Fl(p[2]), cz = Fl(p[3]), sx = Fl(p[5]), sz = Fl(p[6]), tx = Fl(p[7]), tz = Fl(p[8]);
            int orderAt = int.Parse(p[9]), fps = int.Parse(p[11]), crf = int.Parse(p[12]);
            int s = Mathf.Clamp(GridStyle, 0, OzFogOfWar.Looks.Length - 1);
            var outDir = Path.IsPathRooted(GridDir) ? GridDir : Path.GetFullPath(Path.Combine(Application.dataPath, "..", GridDir));
            Directory.CreateDirectory(outDir);
            string tag = $"S{s + 1}_{level}";

            // the reveal clip's start, as Reveal Clips makes it: the level loaded fresh, the briefing skipped, the computer off, the camera held
            OzFogOfWar.ColourIdea = OzFogOfWar.ColourToday; OzFogOfWar.Cover = 1f;
            Time.captureFramerate = 0; Time.timeScale = 1f;
            UnityEngine.SceneManagement.SceneManager.LoadScene(scene);
            yield return Frames(10);
            float t0 = Time.realtimeSinceStartup;
            while ((!ZGame.I || ZGame.I.World == null || !OzFogOfWar.I || ZGame.I.level != level) && Time.realtimeSinceStartup - t0 < 120f) yield return null;
            var g = ZGame.I; var f = OzFogOfWar.I;
            if (!g || g.World == null || !f) { Row("K1", false, "the level did not load: " + scene); End(log); yield break; }
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
            if (!rts || !forced) { Row("K1", false, "no RTS camera or the fog refused: " + refused); End(log); yield break; }
            float edgeWas = rts.edgePx; rts.edgePx = 0f;
            System.Action hold = () => { rts.yaw = 0f; rts.focus = new Vector3(cx, rts.focus.y, cz); rts.SetHeightImmediate(70f); ParkPointer(); };
            hold();
            var W = g.World; var known = new HashSet<int>(); foreach (var o in W.Objects) if (o != null) known.Add(o.Id);
            string team = g.player == Team.Red ? "r" : "b";
            string staged = CaptureKit.StageBattle($"{team} {unit} {Mathf.RoundToInt(sx / 2f)} {Mathf.RoundToInt(-sz / 2f)}", 1f);
            ZObject lead = null;
            foreach (var o in W.Objects) { if (o == null || known.Contains(o.Id) || !o.IsUnit || o.Owner != g.player) continue; if (lead == null || o.Minions.Count > 0) lead = o; }
            if (lead == null) { rts.edgePx = edgeWas; Row("K1", false, "the unit was not staged: " + staged); End(log); yield break; }
            float tw = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - tw < 3f) { hold(); yield return null; }

            // the clip's fixed 60 fps step up to the moment (the recorder's own order, without a recording: no encoder to wait on), the game
            // paused right after clip frame GridFrame
            var pxT = ZGame.WorldToPx(new Vector3(tx, 0f, tz));
            string order = ""; bool held = false; int freezeAt = GridFrame + 1; var done = new bool[1];
            f.StartCoroutine(FixedSteps(GridFrame + 2, fps, i =>
            {
                hold();
                if (i == orderAt && lead != null && !lead.Destroyed)
                    order = W.SendWaypoints(lead.Owner, lead, new List<ZWaypoint> { ZWaypoint.Move(pxT.x, pxT.y) }) ? "move order given" : "move order REFUSED";
                if (i == freezeAt) { Time.timeScale = 0f; held = FreezeLook(); }
            }, done));
            float ts0 = Time.realtimeSinceStartup;
            while (!done[0] && Time.realtimeSinceStartup - ts0 < 600f) yield return null;
            Row("K1", done[0] && order == "move order given" && held && Time.timeScale == 0f,
                $"{OzFogOfWar.Looks[s].Name} {level}: the clip's fixed {fps} fps step replayed to frame {GridFrame} ({GridFrame / (float)fps:0.00} s) in {Time.realtimeSinceStartup - ts0:0} s; {staged}; {lead.Type} #{lead.Id} {order} at frame {orderAt}; paused (time scale {Time.timeScale}), drift and reveal held {held}");

            // the toasts of that moment go (real time; the game stands still), then the deep-cloud mask
            tw = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - tw < 7f) { hold(); yield return null; }
            var cam = rts.GetComponent<Camera>(); var ucd = cam ? cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>() : null;
            bool postWas = ucd && ucd.renderPostProcessing;
            var hud = new List<Canvas>();
            foreach (var cv in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) if (cv && cv.isRootCanvas && cv.enabled && cv.renderMode != RenderMode.WorldSpace) hud.Add(cv);
            if (ucd) ucd.renderPostProcessing = false; foreach (var cv in hud) if (cv) cv.enabled = false;
            OzFogOfWar.DebugMode = 4; yield return Frames(4);
            string maskPath = Path.Combine(outDir, $"grid_{tag}_mask.png");
            yield return Shot(maskPath);
            OzFogOfWar.DebugMode = 0; if (ucd) ucd.renderPostProcessing = postWas; foreach (var cv in hud) if (cv) cv.enabled = true;
            yield return Frames(4);
            var mt = LoadPng(maskPath); var deep = DeepMask(mt, out int nDeep); int sw = mt.width, sh = mt.height; Object.DestroyImmediate(mt);
            Row("K2", sw == 1920 && sh == 1080 && nDeep > 50000, $"the deep-cloud mask (debug 4: the cloud's own opacity >= 0.95, HUD boxes out): {nDeep} pixels of a {sw} x {sh} capture");

            int planet = OzFogOfWar.PlanetIndex;
            string[] cn = { "C0", "C1", "C2", "C3" };
            if (GridCalibrate)
            {
                var keep = OzFogOfWar.Palettes[planet];
                OzFogOfWar.Cover = 1f; OzFogOfWar.ColourIdea = OzFogOfWar.ColourToday; yield return Frames(4);
                string c0p = Path.Combine(outDir, $"cal_{tag}_C0.png"); yield return Shot(c0p);
                var c0t = LoadPng(c0p); var c0 = MeanLin(c0t, deep); Object.DestroyImmediate(c0t);
                var c0Lab = LinToLab(c0);
                var gp = GridGround.Split('|'); var gLab = new Vector3(Fl(gp[0]), Fl(gp[1]), Fl(gp[2]));
                Color fogC = RenderSettings.fogColor;
                var airLin = new Vector3((float)SrgbToLin(fogC.r), (float)SrgbToLin(fogC.g), (float)SrgbToLin(fogC.b)); var airLab = LinToLab(airLin);
                var t1 = new Vector3(gLab.x + 0.5f * (c0Lab.x - gLab.x), gLab.y, gLab.z);                          // LAND: the ground, lifted halfway to today's cloud
                var t2 = new Vector3(c0Lab.x, airLab.y, airLab.z);                                                   // AIR: the horizon haze at today's cloud lightness
                var mixLin = LabToLin(new Vector3(t1.x, 0.3f * (t1.y + t2.y), 0.3f * (t1.z + t2.z)));               // DUSK: the mix at 60 % chroma ...
                var duskLin = new Vector3((float)SrgbToLin(0.6 * LinToSrgb(mixLin.x)), (float)SrgbToLin(0.6 * LinToSrgb(mixLin.y)), (float)SrgbToLin(0.6 * LinToSrgb(mixLin.z)));
                var t3 = LinToLab(duskLin);                                                                          // ... at 60 % of LAND's value
                var tgt = new[] { c0Lab, t1, t2, t3 };
                Say($"  today's cloud (deep, S{s + 1}): {Hex(c0)} Lab {Lab3(c0Lab)}; ground {Hex(LabToLin(gLab))} Lab {Lab3(gLab)}; horizon haze RenderSettings.fogColor {Hex(airLin)} Lab {Lab3(airLab)} (day clock {(Object.FindFirstObjectByType<DayNight>() is DayNight dn ? dn.timeOfDay.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture) : "-")})");
                var fit = new Vector3[4]; var sb = new StringBuilder();
                for (int idea = 1; idea <= 3; idea++)
                {
                    var tl = LabToLin(tgt[idea]);
                    var m = new Vector3(tl.x / Mathf.Max(1e-5f, c0.x), tl.y / Mathf.Max(1e-5f, c0.y), tl.z / Mathf.Max(1e-5f, c0.z));
                    float dE = 99f; int it = 0; Vector3 mean = Vector3.zero;
                    for (; it < 6; it++)
                    {
                        SetTint(planet, idea, m); OzFogOfWar.ColourIdea = OzFogOfWar.ColourToday; OzFogOfWar.ColourIdea = idea; yield return Frames(4);
                        string cp = Path.Combine(outDir, $"cal_{tag}_{cn[idea]}_{it}.png"); yield return Shot(cp);
                        var ct = LoadPng(cp); mean = MeanLin(ct, deep); Object.DestroyImmediate(ct);
                        dE = (LinToLab(mean) - tgt[idea]).magnitude;
                        Say($"    {OzFogOfWar.ColourNames[idea]} try {it}: multiplier ({V3(m)}) -> deep mean {Hex(mean)} Lab {Lab3(LinToLab(mean))}, target {Hex(tl)} Lab {Lab3(tgt[idea])}, dE {dE:0.00}");
                        if (dE < 0.8f || it == 5) break;
                        m = new Vector3(Mathf.Clamp(m.x * tl.x / Mathf.Max(1e-5f, mean.x), 0.02f, 4f), Mathf.Clamp(m.y * tl.y / Mathf.Max(1e-5f, mean.y), 0.02f, 4f), Mathf.Clamp(m.z * tl.z / Mathf.Max(1e-5f, mean.z), 0.02f, 4f));
                    }
                    fit[idea] = m;
                    Row("K" + (2 + idea), dE < 1.5f, $"{OzFogOfWar.ColourNames[idea]}: target {Hex(tl)} Lab {Lab3(tgt[idea])}, on screen {Hex(mean)} (dE {dE:0.00} after {it + 1} renders), multiplier ({V3(m)})");
                    sb.Append(sb.Length > 0 ? "|" : "").Append(OzFogOfWar.ColourNames[idea]).Append(' ').Append(V3(m).Replace("f", "").Replace(",", ""));
                }
                GridFit = sb.ToString();
                OzFogOfWar.Palettes[planet] = keep;                                                                  // the palette as compiled
                Say($"  FIT {level} {GridFit}");
            }
            else
            {
                int n = 0; var sb = new StringBuilder();
                for (int r = 0; r < 4; r++)
                    for (int c = 0; c < OzFogOfWar.CoverLevels.Length; c++)
                    {
                        OzFogOfWar.ColourIdea = r; OzFogOfWar.Cover = OzFogOfWar.CoverLevels[c]; yield return Frames(4);
                        string fp = Path.Combine(outDir, $"grid_{tag}_{cn[r]}_{Mathf.RoundToInt(OzFogOfWar.CoverLevels[c] * 100f)}.png");
                        yield return Shot(fp);
                        if (!File.Exists(fp)) continue;
                        var st = LoadPng(fp); var mean = MeanLin(st, deep); bool size = st.width == 1920 && st.height == 1080; Object.DestroyImmediate(st);
                        if (size) n++;
                        sb.Append($"{cn[r]}/{Mathf.RoundToInt(OzFogOfWar.CoverLevels[c] * 100f)} {Hex(mean)}; ");
                    }
                Say("  deep-cloud mean on screen: " + sb);
                // the held frame again at the end: the same picture (the drift and the reveal really held)
                OzFogOfWar.ColourIdea = OzFogOfWar.ColourToday; OzFogOfWar.Cover = 1f; yield return Frames(4);
                string again = Path.Combine(outDir, $"grid_{tag}_C0_100_again.png"); yield return Shot(again);
                var a = LoadPng(again); var b = LoadPng(Path.Combine(outDir, $"grid_{tag}_C0_100.png"));
                double diff = 0; int nd = 0; var pa = a.GetPixels32(); var pb = b.GetPixels32();
                for (int i = 0; i < pa.Length && i < pb.Length; i++) if (deep[i]) { diff += Mathf.Abs(pa[i].r - pb[i].r) + Mathf.Abs(pa[i].g - pb[i].g) + Mathf.Abs(pa[i].b - pb[i].b); nd++; }
                Object.DestroyImmediate(a); Object.DestroyImmediate(b);
                double md = nd > 0 ? diff / (3.0 * nd) : 99;
                Row("K3", n == 16, $"{n} of 16 stills (colour TODAY LAND AIR DUSK x cover 100 85 70 55 %) at 1920 x 1080 in {outDir}");
                Row("K4", md < 0.5, $"the held frame at the end: the deep cloud differs from the first still by {md:0.000} levels a channel (the drift and the reveal held)");
            }

            ThawLook();
            OzFogOfWar.ColourIdea = OzFogOfWar.ColourToday; OzFogOfWar.Cover = 1f; OzFogOfWar.DebugMode = 0;
            Time.timeScale = 1f; rts.edgePx = edgeWas;
            OzFogOfWar.SessionLook = -1;
            End(log);
        }
    }
}
