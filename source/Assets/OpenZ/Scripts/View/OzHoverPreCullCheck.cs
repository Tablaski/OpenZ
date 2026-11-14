#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace OpenZ
{
    /// <summary>
    /// 1.3 item 39, the counted check of the hover pre-cull (View/PlayerInput.HoverCull.cs). Editor Play only (menu OpenZ/V1.3/Hover pre-cull
    /// check, Editor/OzV13HoverPreCullMenu.cs). On L02, L16 and L17 in turn (each loaded as the menu's DEPLOY does, the real level with its
    /// real views, the computer player running), 2000 seeded pointer positions per level at 40 camera poses: 4 heights (the closest zoom,
    /// 70 m, half way, the widest) x the RTS view and the free camera (tilt 22-89 degrees, any heading, placed as the free look places it),
    /// 3 selections (none; our robots: our APCs with room become targets; our robots and a crane: broken bridges). Per pose 50 points: 20
    /// anywhere on the screen, 15 inside an object's pick box, 15 on the edge of one (+-2 px, the hard case). At each point the hover with
    /// the pre-cull on and off, in the same frame (the order swapped every point): the object must be the same. Stopwatch time of both
    /// passes, objects skipped and asked. Review: after every pose the ground (ZGame.GroundY) under every map item is also tested against
    /// the height range the pre-cull used (the one bound not mirrored from ScreenRect). PASS = 0 different and 0 ground outside the range on
    /// all three levels. Writes
    /// The RTS camera is held still while it runs and put back at the end of each level; the selection is put back too.
    /// </summary>
    public sealed class OzHoverPreCullCheck : MonoBehaviour
    {
        const string Tag = "[OpenZ HoverPreCull]";
        public static readonly string[] Levels = { "L02", "L16", "L17" };
        public const int Poses = 40, PerPose = 50, AnyPoints = 20, InPoints = 15, Seed = 39039;
        public const float LoadWaitS = 150f, SettleS = 6f, EdgePx = 2f;
        public static string Report = "not run";
        public static bool Running => I;
        static OzHoverPreCullCheck I;
        static string Dir => Path.GetFullPath(Path.Combine(Application.dataPath, "../../../../work/staging/v1_3/solutions/item39_hover_precull/proof"));

        readonly StringBuilder lines = new StringBuilder();
        int totalPoints, totalDiff, totalGroundOut; bool failed;

        public static void Run()
        {
            if (!Application.isPlaying) { Debug.LogWarning(Tag + " Play mode only"); return; }
            if (I) { Debug.LogWarning(Tag + " already running"); return; }
            Report = "running";
            var go = new GameObject("OzHoverPreCullCheck"); DontDestroyOnLoad(go); I = go.AddComponent<OzHoverPreCullCheck>();
        }

        IEnumerator Start()
        {
            lines.AppendLine("OpenZ 1.3 item 39: hover pre-cull, same hovered object with the pre-cull on and off (View/PlayerInput.HoverCull.cs)");
            lines.AppendLine($"{System.DateTime.Now:yyyy-MM-dd HH:mm}  screen {Screen.width}x{Screen.height}  {Poses} poses x {PerPose} points a level, seed {Seed}");
            lines.AppendLine("the editor's times are only a guide (the editor's own overhead); the counts are exact");
            lines.AppendLine();
            for (int i = 0; i < Levels.Length; i++) yield return Level(Levels[i], i);
            bool pass = !failed && totalDiff == 0 && totalGroundOut == 0 && totalPoints >= Levels.Length * Poses * PerPose;   // review: the ground under every map item inside the range used, too
            lines.AppendLine();
            lines.AppendLine((pass ? "PASS" : "FAIL") + $" same hover with the pre-cull on and off: {totalPoints - totalDiff} of {totalPoints} the same, {totalDiff} different; ground under a map item outside the range used: {totalGroundOut}{(failed ? "; a level did not run" : "")}");
            Report = lines.ToString();
            try { Directory.CreateDirectory(Dir); File.WriteAllText(Path.Combine(Dir, "hover_precull_check.txt"), Report); } catch (System.Exception e) { Debug.LogWarning(Tag + " could not write the report: " + e.Message); }
            Debug.Log($"{Tag} DONE {(pass ? "PASS" : "FAIL")}: {totalPoints - totalDiff} of {totalPoints} the same; report {Path.Combine(Dir, "hover_precull_check.txt")}\n{Report}");
            I = null; Destroy(gameObject);
        }

        static bool LevelUp(string id)
        {
            var g = ZGame.I;
            return g && g.World != null && g.enabled && g.level == id && !ZodBriefing.BriefingActive;
        }

        IEnumerator Level(string id, int index)
        {
            OzLevelDef def = null;
            foreach (var d in OzLevelDef.All) if (d.Id == id) def = d;
            if (def == null) { lines.AppendLine($"LEVEL {id}: unknown level"); failed = true; yield break; }
            if (!LevelUp(id))
            {
                if (SceneManager.LoadSceneAsync(def.Scene, LoadSceneMode.Single) == null) { lines.AppendLine($"LEVEL {id}: the scene {def.Scene} could not be loaded"); failed = true; yield break; }
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

            var rng = new System.Random(Seed + 1000 * index);
            float mw = W.Map.WidthPix / 8f, mh = W.Map.HeightPix / 8f;
            float[] heights = { rts.minHeight, 70f, (70f + rts.maxHeight) * 0.5f, rts.maxHeight };
            var selWas = new List<ZObject>(input.Selection);
            bool rtsWas = rts.enabled; rts.enabled = false;
            Vector3 camPos = cam.transform.position; Quaternion camRot = cam.transform.rotation;
            var ownRobots = new List<ZObject>(); ZObject crane = null;
            foreach (var o in W.OwnedSelectable(g.player)) { if (o.Kind == ZKind.Robot) ownRobots.Add(o); if (o.Type == "crane" && crane == null) crane = o; }

            int points = 0, diff = 0, hitUnit = 0, hitItem = 0, hitBuilding = 0, none = 0, edgePts = 0, inPts = 0, anyPts = 0;
            long tOn = 0, tOff = 0; long skipped = 0, askedOn = 0; int passes = 0;
            int groundTests = 0, groundOut = 0; float padUsed = float.NegativeInfinity;                  // review: the one bound not mirrored from ScreenRect
            var shown = new List<string>();
            var boxes = new List<(ZObject o, Rect r)>();
            for (int p = 0; p < Poses; p++)
            {
                yield return null;                                                                        // a fresh frame: the game moved its views
                if (!LevelUp(id)) { lines.AppendLine($"LEVEL {id}: the level stopped at pose {p}"); failed = true; break; }
                bool free = (p / 4) % 2 == 1; float h = heights[p % heights.Length];
                float tilt = free ? Mathf.Lerp(22f, 89f, (float)rng.NextDouble()) : rts.pitch, yaw = free ? Mathf.Lerp(-180f, 180f, (float)rng.NextDouble()) : 0f;
                float fx = Mathf.Lerp(0.1f, 0.9f, (float)rng.NextDouble()) * mw, fz = -Mathf.Lerp(0.1f, 0.9f, (float)rng.NextDouble()) * mh;
                var focus = new Vector3(fx, g.GroundY(fx, fz), fz);
                float dist = h / Mathf.Sin(rts.pitch * Mathf.Deg2Rad), r = tilt * Mathf.Deg2Rad;          // the free look's distance at this zoom (RtsCamera.PlaceFree)
                var turn = Quaternion.Euler(0f, yaw, 0f);
                var pos = free ? focus + turn * new Vector3(0f, dist * Mathf.Sin(r), -dist * Mathf.Cos(r))
                               : new Vector3(fx, 0f, fz) + turn * new Vector3(0f, h, -h / Mathf.Tan(rts.pitch * Mathf.Deg2Rad));   // RtsCamera.Place
                cam.transform.SetPositionAndRotation(pos, Quaternion.Euler(tilt, yaw, 0f));
                int sel = p % 3;
                input.Selection.Clear();
                if (sel >= 1) input.Selection.AddRange(ownRobots);
                if (sel == 2 && crane != null) input.Selection.Add(crane);

                boxes.Clear();
                input.HoverProbe(new Vector2(-1e5f, -1e5f), false);                                    // the frame's silhouettes for this pose
                var screen = new Rect(0f, 0f, Screen.width, Screen.height);
                foreach (var o in W.Objects)
                {
                    if (o.Destroyed && o.Kind != ZKind.Building) continue;
                    var b = input.ScreenRect(o);
                    if (b.width > 0f && b.height > 0f && b.Overlaps(screen)) boxes.Add((o, b));
                }
                for (int k = 0; k < PerPose; k++)
                {
                    Vector2 mp; int kind;
                    if (k < AnyPoints || boxes.Count == 0) { mp = new Vector2((float)rng.NextDouble() * Screen.width, (float)rng.NextDouble() * Screen.height); kind = 0; }
                    else
                    {
                        var b = boxes[rng.Next(boxes.Count)].r;
                        if (k < AnyPoints + InPoints) { mp = new Vector2(Mathf.Lerp(b.xMin, b.xMax, (float)rng.NextDouble()), Mathf.Lerp(b.yMin, b.yMax, (float)rng.NextDouble())); kind = 1; }
                        else
                        {
                            float e = Mathf.Lerp(-EdgePx, EdgePx, (float)rng.NextDouble()), along = (float)rng.NextDouble();
                            switch (rng.Next(4))
                            {
                                case 0: mp = new Vector2(b.xMin + e, Mathf.Lerp(b.yMin, b.yMax, along)); break;
                                case 1: mp = new Vector2(b.xMax + e, Mathf.Lerp(b.yMin, b.yMax, along)); break;
                                case 2: mp = new Vector2(Mathf.Lerp(b.xMin, b.xMax, along), b.yMin + e); break;
                                default: mp = new Vector2(Mathf.Lerp(b.xMin, b.xMax, along), b.yMax + e); break;
                            }
                            kind = 2;
                        }
                    }
                    if (kind == 0) anyPts++; else if (kind == 1) inPts++; else edgePts++;
                    ZObject on, off; long t;
                    if ((k & 1) == 0)
                    {
                        t = Stopwatch.GetTimestamp(); on = input.HoverProbe(mp, true); tOn += Stopwatch.GetTimestamp() - t; skipped += PlayerInput.HoverSkipped; askedOn += PlayerInput.HoverAsked;
                        t = Stopwatch.GetTimestamp(); off = input.HoverProbe(mp, false); tOff += Stopwatch.GetTimestamp() - t;
                    }
                    else
                    {
                        t = Stopwatch.GetTimestamp(); off = input.HoverProbe(mp, false); tOff += Stopwatch.GetTimestamp() - t;
                        t = Stopwatch.GetTimestamp(); on = input.HoverProbe(mp, true); tOn += Stopwatch.GetTimestamp() - t; skipped += PlayerInput.HoverSkipped; askedOn += PlayerInput.HoverAsked;
                    }
                    passes++; points++;
                    if (off == null) none++; else if (off.Kind == ZKind.Building) hitBuilding++; else if (off.IsUnit && !off.Destroyed) hitUnit++; else hitItem++;
                    if (on != off)
                    {
                        diff++;
                        if (shown.Count < 12) shown.Add($"  DIFF pose {p} ({(free ? "free" : "rts")} h {h:0} tilt {tilt:0} yaw {yaw:0} sel {sel}) point {mp.x:0.0},{mp.y:0.0} ({(kind == 0 ? "anywhere" : kind == 1 ? "inside" : "edge")}): on {Name(on)} / off {Name(off)}");
                    }
                }
                // review: the footprint path's one assumption, checked directly: GroundY under every map item lies inside the height range
                // the pre-cull used this pose (its walk-collider range plus the pad). padUsed = how far it went beyond the colliders' own range.
                if (input.ProofGroundRange(out float plo, out float phi))
                    foreach (var o in W.Objects)
                    {
                        if (o.Destroyed || o.IsUnit || o.IsFortDefender || o.Kind == ZKind.Building || o.Kind == ZKind.Cannon) continue;
                        var a = ZGame.PxToWorld(o.PosX, o.PosY); var b = ZGame.PxToWorld(o.PosX + o.WidthPix, o.PosY + o.HeightPix);
                        float gy = g.GroundY((a.x + b.x) * 0.5f, (a.z + b.z) * 0.5f); groundTests++;
                        if (!(gy >= plo && gy <= phi)) { groundOut++; if (shown.Count < 12) shown.Add($"  GROUND pose {p}: {Name(o)} ground {gy:0.00} m outside {plo:0.0} .. {phi:0.0}"); }
                        padUsed = Mathf.Max(padUsed, Mathf.Max(plo + PlayerInput.GroundPadM - gy, gy - (phi - PlayerInput.GroundPadM)));
                    }
            }
            input.Selection.Clear(); input.Selection.AddRange(selWas);
            cam.transform.SetPositionAndRotation(camPos, camRot); rts.enabled = rtsWas;
            input.ProofGroundRange(out float glo, out float ghi);
            double ms = 1000.0 / Stopwatch.Frequency, on1 = passes > 0 ? tOn * ms / passes : 0, off1 = passes > 0 ? tOff * ms / passes : 0;
            double sk = passes > 0 ? (double)skipped / passes : 0, ask = passes > 0 ? (double)askedOn / passes : 0;
            lines.AppendLine($"LEVEL {id}: {points} points ({anyPts} anywhere, {inPts} inside a box, {edgePts} on an edge), {points - diff} the same, {diff} different");
            lines.AppendLine($"  hovered (pre-cull off): unit {hitUnit}, map item {hitItem}, building {hitBuilding}, nothing {none}");
            lines.AppendLine($"  hover pass: {off1:0.000} ms off, {on1:0.000} ms on, saved {off1 - on1:0.000} ms a frame ({(off1 > 0 ? 100.0 * (off1 - on1) / off1 : 0):0} %); objects a pass: {sk:0.0} skipped, {ask:0.0} asked ({(sk + ask > 0 ? 100.0 * sk / (sk + ask) : 0):0} % skipped)");
            lines.AppendLine($"  walk ground height range used: {glo:0.0} .. {ghi:0.0} m (with the {PlayerInput.GroundPadM:0} m pad)");
            lines.AppendLine($"  ground under the map items: {groundTests} tests, {groundOut} outside the range used (must be 0); farthest beyond the walk colliders' own range: {(groundTests > 0 ? padUsed.ToString("0.00") : "-")} m (0 or less = the pad was not needed)");
            foreach (var s in shown) lines.AppendLine(s);
            lines.AppendLine($"DATA {id} | points {points} | same {points - diff} | different {diff} | ms off {off1:0.000} | ms on {on1:0.000} | saved {off1 - on1:0.000} | skipped {sk:0.0} | asked {ask:0.0} | ground tests {groundTests} | ground out {groundOut}");
            totalPoints += points; totalDiff += diff; totalGroundOut += groundOut;
        }

        static string Name(ZObject o) => o == null ? "nothing" : $"{o.Type} #{o.Id} ({o.Kind}, {o.Owner})";
    }
}
#endif
