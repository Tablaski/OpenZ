#if UNITY_EDITOR
// OpenZ (GPL-3.0) - 1.5 kit issue8_slow_chip_picture, part B: the counted Play check of the silhouette short pass (View/OzSilhouetteHull.cs).
// Editor Play only (menu OpenZ/Checks/V1.5/Issue 8 hover replay (Play), or OzHoverReplayCheck.Run(levels)). On each level in turn
// (loaded as the 1.3 hover pre-cull check loads them: the real scene, its real views, the computer player running) it holds the RTS
// camera still and replays a FIXED pointer path over FIXED camera poses (seeded: the same poses and points on every run):
//  * at every pose: every building's (and the shuttle's) silhouette box with the short pass off and on, in the same frame, compared
//    bit for bit (xMin, yMin, xMax, yMax, visible);
//  * at every point of the path: the hovered object (PlayerInput.HoverAt through HoverProbe) with the short pass off and on, in the
//    same frame, the order swapped every point: it must be the same object;
//  * the time of a whole hover pass with the short pass off and on (Stopwatch, the editor's numbers: a guide);
// then on the first level the game's own counter (View/OzPerfNumbers.cs, the "hover ... ms a frame" of the minute line): the same
// number of frames with the short pass off and on, the pointer (a stand-in mouse) moving along the same loop every frame.
// PASS = every box the same, every hovered object the same, hulls built for the buildings. Report: Report (and the console line
// "[OpenZ issue 8 hover] DONE ..."). The camera, the selection, the short-pass switch and the stand-in mouse are put back.
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace OpenZ
{
    public sealed class OzHoverReplayCheck : MonoBehaviour
    {
        const string Tag = "[OpenZ issue 8 hover]";
        public static readonly string[] AllLevels = { "L02", "L16", "L17", "L18" };
        public const int Poses = 24, Seed = 81512, PerfFrames = 600;
        public const float LoadWaitS = 420f, SettleS = 6f;                                  // a level loads slowly in a low-priority batch run
        public static string Report = "not run";
        /// <summary>A builder's tuning aid (null = off): the silhouette points of one building of each type a level, as text files in this folder.</summary>
        public static string DumpDir;

        static void Dump(PlayerInput input, List<ZObject> sils, string level)
        {
            try
            {
                var f = typeof(PlayerInput).GetField("meshPts", BindingFlags.NonPublic | BindingFlags.Instance);
                var d = f != null ? f.GetValue(input) as Dictionary<int, Vector3[]> : null; if (d == null) return;
                System.IO.Directory.CreateDirectory(DumpDir); var seen = new HashSet<string>();
                foreach (var o in sils)
                {
                    if (!d.TryGetValue(o.Id, out var pts) || pts == null || !seen.Add(o.Type)) continue;
                    var sb = new StringBuilder();
                    foreach (var p in pts) sb.Append(p.x.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append(' ').Append(p.y.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append(' ').Append(p.z.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append('\n');
                    System.IO.File.WriteAllText(System.IO.Path.Combine(DumpDir, $"sil_{level}_{o.Type}.txt"), sb.ToString());
                }
            }
            catch (System.Exception e) { Debug.LogWarning(Tag + " dump: " + e.Message); }
        }
        public static bool Running => I;
        static OzHoverReplayCheck I;
        string[] levels = AllLevels;

        readonly StringBuilder lines = new StringBuilder();
        int totalPoints, totalDiff, totalBoxes, totalBoxDiff, totalShort; bool failed;
        string perf = "not measured";

        public static void Run(string[] which = null)
        {
            if (!Application.isPlaying) { Debug.LogWarning(Tag + " Play mode only"); return; }
            if (I) { Debug.LogWarning(Tag + " already running"); return; }
            Report = "running";
            var go = new GameObject("OzHoverReplayCheck"); DontDestroyOnLoad(go); I = go.AddComponent<OzHoverReplayCheck>();
            if (which != null && which.Length > 0) I.levels = which;
        }

        IEnumerator Start()
        {
            bool was = OzSilhouetteHull.On;
            lines.AppendLine("OpenZ 1.5 issue 8: the silhouette short pass (View/OzSilhouetteHull.cs): the same boxes and the same hovered object, off and on");
            lines.AppendLine($"{System.DateTime.Now:yyyy-MM-dd HH:mm}  screen {Screen.width}x{Screen.height}  {Poses} poses a level, seed {Seed}; the editor's times are a guide, the counts are exact");
            for (int i = 0; i < levels.Length; i++) yield return Level(levels[i], i);
            OzSilhouetteHull.On = was;
            bool pass = !failed && totalDiff == 0 && totalBoxDiff == 0 && totalPoints > 0 && totalBoxes > 0 && totalShort > 0;
            lines.AppendLine(perf);
            lines.AppendLine((pass ? "PASS" : "FAIL") + $" the same hovered object at {totalPoints - totalDiff} of {totalPoints} points ({totalDiff} different); the same silhouette box {totalBoxes - totalBoxDiff} of {totalBoxes} ({totalBoxDiff} different); boxes by the short pass {totalShort}{(failed ? "; a level did not run" : "")}");
            Report = lines.ToString();
            Debug.Log($"{Tag} DONE {(pass ? "PASS" : "FAIL")}: {totalPoints - totalDiff} of {totalPoints} hovers and {totalBoxes - totalBoxDiff} of {totalBoxes} boxes the same\n{Report}");
            I = null; Destroy(gameObject);
        }

        static bool LevelUp(string id)
        {
            var g = ZGame.I;
            return g && g.World != null && g.enabled && g.level == id && !ZodBriefing.BriefingActive;
        }

        static string Name(ZObject o) => o == null ? "nothing" : $"{o.Type} #{o.Id}";
        static bool Same(Rect a, Rect b) => a.x.Equals(b.x) && a.y.Equals(b.y) && a.width.Equals(b.width) && a.height.Equals(b.height);   // the four floats the Rect holds, bit for bit

        IEnumerator Level(string id, int index)
        {
            OzLevelDef def = null;
            foreach (var d in OzLevelDef.All) if (d.Id == id) def = d;
            string scene = def != null ? def.Scene : id == "L18" ? "L18_BridgeGame" : null;
            if (scene == null) { lines.AppendLine($"LEVEL {id}: unknown level"); failed = true; yield break; }
            if (!LevelUp(id))
            {
                if (SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single) == null) { lines.AppendLine($"LEVEL {id}: the scene {scene} could not be loaded"); failed = true; yield break; }
                float t0 = Time.realtimeSinceStartup;
                while (!LevelUp(id) && Time.realtimeSinceStartup - t0 < LoadWaitS) yield return null;
                if (!LevelUp(id)) { lines.AppendLine($"LEVEL {id}: not running {LoadWaitS:0} s after the load"); failed = true; yield break; }
            }
            float ts = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - ts < SettleS) yield return null;
            var g = ZGame.I; var W = g ? g.World : null;
            var input = FindFirstObjectByType<PlayerInput>();
            var rts = input && input.cam ? input.cam : FindFirstObjectByType<RtsCamera>();
            var cam = rts ? rts.GetComponent<Camera>() : Camera.main;
            if (W == null || !input || !rts || !cam) { lines.AppendLine($"LEVEL {id}: no world, input or camera"); failed = true; yield break; }
            RenderTexture stand = null; var tex0 = cam.targetTexture;
            if (cam.pixelRect.width < 64 || cam.pixelRect.height < 64)
            {   // a batch run has no game view: the camera draws (or would draw) into a 1280x720 picture for the check, put back at the end
                stand = new RenderTexture(1280, 720, 24) { name = "OzHoverReplayView" }; cam.targetTexture = stand;
                lines.AppendLine($"LEVEL {id}: no game view (screen {Screen.width}x{Screen.height}): the camera's picture is a 1280x720 stand-in for the check");
            }
            var px = cam.pixelRect;
            if (px.width < 64 || px.height < 64) { lines.AppendLine($"LEVEL {id}: the camera has no picture size ({px.width}x{px.height})"); failed = true; if (stand) { cam.targetTexture = tex0; stand.Release(); Destroy(stand); } yield break; }

            var rng = new System.Random(Seed + 1000 * index);
            float mw = W.Map.WidthPix / 8f, mh = W.Map.HeightPix / 8f;
            float[] heights = { rts.minHeight, 70f, (70f + rts.maxHeight) * 0.5f, rts.maxHeight };
            var selWas = new List<ZObject>(input.Selection);
            bool rtsWas = rts.enabled; rts.enabled = false;
            Vector3 camPos = cam.transform.position; Quaternion camRot = cam.transform.rotation;
            var ownRobots = new List<ZObject>();
            foreach (var o in W.OwnedSelectable(g.player)) if (o.Kind == ZKind.Robot) ownRobots.Add(o);

            // the hulls: one a frame, so a few frames first (the short pass is asked by every MeshRect of a building)
            var sils = new List<ZObject>();
            foreach (var o in W.Objects) if ((o.Kind == ZKind.Building || o.IsShuttle) && input.ProofSilhouettePoints(o.Id) >= 0) sils.Add(o);
            OzSilhouetteHull.On = true;
            int b0 = OzSilhouetteHull.Built + OzSilhouetteHull.Refused;
            for (int f = 0; f < sils.Count + 30; f++) { input.HoverProbe(new Vector2(-1e5f, -1e5f), true); yield return null; }
            int built = OzSilhouetteHull.Built, refused = OzSilhouetteHull.Refused;
            if (!string.IsNullOrEmpty(DumpDir)) Dump(input, sils, id);
            int points = 0, diff = 0, boxes = 0, boxDiff = 0, shortP = 0, withSil = 0, silPts = 0, keptPts = 0;
            long tOn = 0, tOff = 0; int passes = 0;
            var shown = new List<string>();
            var rects = new List<(ZObject o, Rect r)>();
            var off = new Dictionary<int, (Rect r, bool ok)>();
            for (int p = 0; p < Poses; p++)
            {
                yield return null;
                if (!LevelUp(id)) { lines.AppendLine($"LEVEL {id}: the level stopped at pose {p}"); failed = true; break; }
                bool free = (p / 4) % 2 == 1; float h = heights[p % heights.Length];
                float tilt = free ? Mathf.Lerp(22f, 89f, (float)rng.NextDouble()) : rts.pitch, yaw = free ? Mathf.Lerp(-180f, 180f, (float)rng.NextDouble()) : 0f;
                float fx = Mathf.Lerp(0.1f, 0.9f, (float)rng.NextDouble()) * mw, fz = -Mathf.Lerp(0.1f, 0.9f, (float)rng.NextDouble()) * mh;
                var focus = new Vector3(fx, g.GroundY(fx, fz), fz);
                float dist = h / Mathf.Sin(rts.pitch * Mathf.Deg2Rad), rr = tilt * Mathf.Deg2Rad;
                var turn = Quaternion.Euler(0f, yaw, 0f);
                var pos = free ? focus + turn * new Vector3(0f, dist * Mathf.Sin(rr), -dist * Mathf.Cos(rr))
                               : new Vector3(fx, 0f, fz) + turn * new Vector3(0f, h, -h / Mathf.Tan(rts.pitch * Mathf.Deg2Rad));
                cam.transform.SetPositionAndRotation(pos, Quaternion.Euler(tilt, yaw, 0f));
                input.Selection.Clear();
                if (p % 2 == 1) input.Selection.AddRange(ownRobots);

                // (1) every silhouette box, off and on, this frame
                off.Clear();
                OzSilhouetteHull.On = false; input.HoverProbe(new Vector2(-1e5f, -1e5f), true);
                foreach (var o in sils) { bool ok = input.MeshRect(o, out var r); off[o.Id] = (r, ok); }
                int s0 = OzSilhouetteHull.ShortPasses;
                OzSilhouetteHull.On = true; input.HoverProbe(new Vector2(-1e5f, -1e5f), true);
                shortP += OzSilhouetteHull.ShortPasses - s0;
                rects.Clear();
                foreach (var o in sils)
                {
                    bool ok = input.MeshRect(o, out var r); var a = off[o.Id]; boxes++;
                    if (ok != a.ok || (ok && !Same(r, a.r)))
                    {
                        boxDiff++;
                        if (shown.Count < 12) shown.Add($"  BOX pose {p} {Name(o)}: off {a.ok} {a.r.xMin:R},{a.r.yMin:R},{a.r.xMax:R},{a.r.yMax:R} / on {ok} {r.xMin:R},{r.yMin:R},{r.xMax:R},{r.yMax:R}");
                    }
                    if (ok && r.width > 0f && r.height > 0f && r.Overlaps(px)) rects.Add((o, r));
                }
                foreach (var o in W.Objects)
                {
                    if (o.Destroyed || o.Kind == ZKind.Building) continue;
                    var b = input.ScreenRect(o);
                    if (b.width > 0f && b.height > 0f && b.Overlaps(px)) rects.Add((o, b));
                }
                // (2) the fixed pointer path: 16 anywhere, 16 inside a box, 24 on a box's edge (the exact edge float, +-0.25, +-1 px)
                for (int k = 0; k < 56; k++)
                {
                    Vector2 mp;
                    if (k < 16 || rects.Count == 0) mp = new Vector2(px.x + (float)rng.NextDouble() * px.width, px.y + (float)rng.NextDouble() * px.height);
                    else
                    {
                        var b = rects[rng.Next(rects.Count)].r;
                        if (k < 32) mp = new Vector2(Mathf.Lerp(b.xMin, b.xMax, (float)rng.NextDouble()), Mathf.Lerp(b.yMin, b.yMax, (float)rng.NextDouble()));
                        else
                        {
                            float e = new[] { 0f, -0.25f, 0.25f, -1f, 1f }[rng.Next(5)], along = (float)rng.NextDouble();
                            switch (rng.Next(4))
                            {
                                case 0: mp = new Vector2(b.xMin + e, Mathf.Lerp(b.yMin, b.yMax, along)); break;
                                case 1: mp = new Vector2(b.xMax + e, Mathf.Lerp(b.yMin, b.yMax, along)); break;
                                case 2: mp = new Vector2(Mathf.Lerp(b.xMin, b.xMax, along), b.yMin + e); break;
                                default: mp = new Vector2(Mathf.Lerp(b.xMin, b.xMax, along), b.yMax + e); break;
                            }
                        }
                    }
                    ZObject hOn, hOff; long t;
                    if ((k & 1) == 0)
                    {
                        OzSilhouetteHull.On = false; t = Stopwatch.GetTimestamp(); hOff = input.HoverProbe(mp, true); tOff += Stopwatch.GetTimestamp() - t;
                        OzSilhouetteHull.On = true; t = Stopwatch.GetTimestamp(); hOn = input.HoverProbe(mp, true); tOn += Stopwatch.GetTimestamp() - t;
                    }
                    else
                    {
                        OzSilhouetteHull.On = true; t = Stopwatch.GetTimestamp(); hOn = input.HoverProbe(mp, true); tOn += Stopwatch.GetTimestamp() - t;
                        OzSilhouetteHull.On = false; t = Stopwatch.GetTimestamp(); hOff = input.HoverProbe(mp, true); tOff += Stopwatch.GetTimestamp() - t;
                    }
                    passes++; points++;
                    if (hOn != hOff) { diff++; if (shown.Count < 12) shown.Add($"  HOVER pose {p} point {mp.x:R},{mp.y:R}: off {Name(hOff)} / on {Name(hOn)}"); }
                }
                OzSilhouetteHull.On = true;
            }
            foreach (var o in sils) { int n = input.ProofSilhouettePoints(o.Id); if (n > 0) { withSil++; silPts += n; } }
            keptPts = (int)Kept(sils);
            // (3) the game's own counter (the first level only): the stand-in mouse moves along a loop, the hover runs in PlayerInput.Update
            if (index == 0)
            {   // the normal RTS view at 70 m over the map's middle (the view a player plays from)
                cam.transform.SetPositionAndRotation(new Vector3(mw * 0.5f, 70f, -mh * 0.5f - 70f / Mathf.Tan(rts.pitch * Mathf.Deg2Rad)), Quaternion.Euler(rts.pitch, 0f, 0f));
                input.Selection.Clear();
                yield return Perf(input, cam, px);
            }
            input.Selection.Clear(); input.Selection.AddRange(selWas);
            cam.transform.SetPositionAndRotation(camPos, camRot); rts.enabled = rtsWas;
            if (stand) { cam.targetTexture = tex0; stand.Release(); Destroy(stand); }
            double ms = 1000.0 / Stopwatch.Frequency;
            lines.AppendLine($"LEVEL {id}: {sils.Count} buildings / shuttle, {withSil} with a silhouette ({silPts} points, {keptPts} kept by the short pass = {(silPts > 0 ? 100.0 * keptPts / silPts : 0):0.0} %); hulls built {built - 0}, refused {refused} (session totals)");
            lines.AppendLine("  the hulls:" + Packs(sils));
            lines.AppendLine($"  boxes: {boxes} compared, {boxes - boxDiff} the same, {boxDiff} different; by the short pass {shortP}");
            lines.AppendLine($"  pointer path: {points} points, {points - diff} the same, {diff} different");
            lines.AppendLine($"  a whole hover pass (editor): {(passes > 0 ? tOff * ms / passes : 0):0.000} ms off, {(passes > 0 ? tOn * ms / passes : 0):0.000} ms on");
            foreach (var s in shown) lines.AppendLine(s);
            lines.AppendLine($"DATA {id} | buildings {sils.Count} | sil points {silPts} | kept {keptPts} | boxes {boxes} | box diff {boxDiff} | short {shortP} | points {points} | diff {diff} | ms off {(passes > 0 ? tOff * ms / passes : 0):0.000} | ms on {(passes > 0 ? tOn * ms / passes : 0):0.000}");
            totalPoints += points; totalDiff += diff; totalBoxes += boxes; totalBoxDiff += boxDiff; totalShort += shortP;
        }

        static long Kept(List<ZObject> sils)
        {
            long n = 0;
            foreach (var o in sils) { var pk = OzSilhouetteHull.Of(o.Id); if (pk != null) n += pk.Kept; }
            return n;
        }

        static string Packs(List<ZObject> sils)
        {
            var sb = new StringBuilder();
            foreach (var o in sils) { var pk = OzSilhouetteHull.Of(o.Id); if (pk != null && sb.Length < 1600) sb.AppendLine().Append("    " + Name(o) + ": " + pk.why); }
            return sb.ToString();
        }

        static long Field(string name) { var f = typeof(OzPerfNumbers).GetField(name, BindingFlags.NonPublic | BindingFlags.Static); return f != null ? System.Convert.ToInt64(f.GetValue(null)) : -1; }

        IEnumerator Perf(PlayerInput input, Camera cam, Rect px)
        {
            Mouse made = null; Keyboard keys = null; var had = Mouse.current;
            try
            {
                made = InputSystem.AddDevice<Mouse>("OzHoverReplayMouse"); made.MakeCurrent();
                if (Keyboard.current == null) keys = InputSystem.AddDevice<Keyboard>("OzHoverReplayKeys");   // PlayerInput.Update needs a keyboard (a batch run has none)
            }
            catch (System.Exception e) { perf = "PERF the stand-in mouse could not be made: " + e.Message; }
            if (made == null) yield break;
            var res = new double[2, 3];
            for (int phase = 0; phase < 2; phase++)
            {
                OzSilhouetteHull.On = phase == 1;
                yield return null;
                OzPerfNumbers.Reset();
                long hs = 0, hn = 0, os = 0, on = 0, lhs = 0, lhn = 0, los = 0, lon = 0;                      // summed frame by frame: the perf line starts its sums again every minute
                for (int f = 0; f < PerfFrames; f++)
                {
                    float a = f * 0.013f;
                    var mp = new Vector2(px.x + px.width * (0.5f + 0.38f * Mathf.Cos(a)), px.y + px.height * (0.5f + 0.36f * Mathf.Sin(a * 1.7f)));   // inside 12-88 %: no edge scroll
                    InputState.Change(made.position, mp);
                    yield return null;
                    long chs = Field("hoverSum"), chn = Field("hoverN"), cos = Field("overlaySum"), con = Field("overlayN");
                    if (chn >= lhn) { hs += chs - lhs; hn += chn - lhn; } else { hs += chs; hn += chn; }
                    if (con >= lon) { os += cos - los; on += con - lon; } else { os += cos; on += con; }
                    lhs = chs; lhn = chn; los = cos; lon = con;
                }
                double k = 1000.0 / Stopwatch.Frequency;
                res[phase, 0] = hn > 0 ? hs * k / hn : -1; res[phase, 1] = hn; res[phase, 2] = on > 0 ? os * k / on : -1;
            }
            OzSilhouetteHull.On = true;
            try { InputSystem.RemoveDevice(made); if (keys != null) InputSystem.RemoveDevice(keys); if (had != null && had.added) had.MakeCurrent(); } catch (System.Exception) { }
            perf = $"PERF the game's own counter (View/OzPerfNumbers.cs), {PerfFrames} frames each, the pointer on the same loop, the camera still: hover {res[0, 0]:0.000} ms a frame with the short pass off ({res[0, 1]:0} picks), {res[1, 0]:0.000} ms on ({res[1, 1]:0} picks); overlay {res[0, 2]:0.000} / {res[1, 2]:0.000} ms";
        }
    }
}
#endif
