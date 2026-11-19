#if UNITY_EDITOR
// OpenZ (GPL-3.0)
// 'OpenZ/1.5/Checks/Gun Seat Wreck/...' (editor only). Numbers + dense frames at the normal RTS camera.
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// Each gun type (gun v1, gun v2, gatling, howitzer, missile cannon) is put down near the camera, killed the normal way (health 0 through the
    /// world, as every check of the wreck kits), with its base whole (WreckFx.Force 2; the gun type: Force 0, the seeded draw, as Force 2 also stops
    /// its break-up) and with its base broken (Force 1). Every
    /// part of its wreck (the base, the thrown turret once it landed, every wreck piece of it: the seat kit, the gun's parts, the base's pieces) is
    /// sampled every 0.1 s for 6 s from the moment it exists: its lowest DRAWN point (the vertices its triangles use, never the unused ones) against
    /// the support right under it (OzWreckGround.Under). PASS for a part = after 1.5 s no still sample (moved under 2 cm since the last) more than
    /// 0.05 m over its support, and (checker 15 Nov) no LATE hover: followed on after its 6 s until it goes (the base sinks at 14 s), never still
    /// more than 0.05 m over its support for longer than 2 s in a row. The owner's case at the end: three gatlings at night (two with the base
    /// whole, one the game's draw). Frames: every 0.2 s for 10 s from the kill (the owner's gatlings: to the end), then every 1 s, at the normal RTS
    /// camera (FlyTo, its own zoom and angle), night (0.62). In a level (not the Unit editor) the guns
    /// are put on free ground near the camera and their wrecks are left as they are.
    /// </summary>
    public sealed class OzGunSeatWreckCheck : MonoBehaviour
    {
        public const string Tag = "[OpenZ 1.5 Gun Seat Wreck]";
        public const float TolM = 0.05f, AfterS = 1.5f, TrackS = 6f, EveryS = 0.1f, StillM = 0.02f, FrameEveryS = 0.2f;
        /// <summary>Checker 15 Nov (A29): the owner's frame has no base under the hanging seat: a seat / turret that rested on the base (or on the thrown
        /// turret) is left in the air when that support sinks (the base fades at WreckFx.SmoulderSeconds 14 s, the seat lives 9-17 s after its throw),
        /// long after the 6 s window. So every part is also followed after its 6 s until it goes (at most LateEndS from the kill): LATE FAIL = still
        /// and more than TolM over its support for longer than HoldS in a row (the rest rules' own reaction: PieceGraceS 1 s + WatchEveryS 0.5 s).
        /// Frames: every 0.2 s for the first DenseS, then every LateFrameS (the owner's gatlings: every 0.2 s throughout).</summary>
        public const float LateEndS = 26f, HoldS = 2f, DenseS = 10f, LateFrameS = 1f;
        public static string Folder = "";
        public static bool Running { get; private set; }
        /// <summary>A/B run: the fix OFF (OzGunSeatWreck.On = false, GunChairDebris's split cache emptied), the owner's case only: a FAIL here shows that
        /// the check sees the old hover. Informative, never the gate.</summary>
        public static bool AbOff { get; private set; }
        public static string Report { get; private set; } = "no run yet";
        static readonly (string type, int ver, string label)[] Kinds =
        {
            ("gun", 1, "gun v1"), ("gun", 2, "gun v2"), ("gatling", 0, "gatling"), ("howitzer", 0, "howitzer"), ("missile_cannon", 0, "missile cannon"),
        };

        ZGame G; ZWorld W; RtsCamera rts; DayNight day;
        readonly StringBuilder md = new StringBuilder();
        int pass, fail, frames; int force0; float speed0;

        public static void Begin(string folder, bool abOff = false)
        {
            if (Running || !ZGame.I || ZGame.I.World == null) { Debug.LogWarning(Tag + " enter Play first (the Unit editor, or a level)"); return; }
            Folder = folder; AbOff = abOff;
            ZGame.I.gameObject.AddComponent<OzGunSeatWreckCheck>();
        }

        void Start() { Running = true; StartCoroutine(Run()); }
        void OnDestroy() { Running = false; Time.captureFramerate = 0; if (G) { WreckFx.Force = force0; G.gameSpeed = speed0; } if (AbOff) { OzGunSeatWreck.On = true; ClearSplits(); AbOff = false; } }

        /// <summary>GunChairDebris keeps its split meshes per source mesh: emptied so an A/B run (and the run after it) builds them with the switch it has.</summary>
        static void ClearSplits()
        {
            var f = typeof(GunChairDebris).GetField("cache", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            if (f != null && f.GetValue(null) is System.Collections.IDictionary d) d.Clear();
        }

        static string Base(int f) => f == 1 ? "broken" : f == 2 ? "whole" : "game's draw";

        IEnumerator Run()
        {
            G = ZGame.I; W = G.World; rts = FindFirstObjectByType<RtsCamera>(); day = FindFirstObjectByType<DayNight>();
            force0 = WreckFx.Force; speed0 = G.gameSpeed; G.gameSpeed = 1f;
            if (!string.IsNullOrEmpty(Folder)) Directory.CreateDirectory(Folder);
            bool ue = OzUnitEditor.Active;
            ClearSplits(); if (AbOff) OzGunSeatWreck.On = false;
            md.AppendLine($"# Gun seat wreck check ({System.DateTime.Now:yyyy-MM-dd HH:mm}), {(ue ? "the Unit editor" : "a level")}, 60 fps frame-locked, night (0.62), x1{(AbOff ? ", A/B: THE FIX OFF (the owner's case only; a FAIL here = the check sees the old hover)" : "")}").AppendLine();
            md.AppendLine("gap = the lowest DRAWN point of the part (the vertices its triangles use) over the support right under it (OzWreckGround.Under). "
                + $"PASS = after {AfterS:0.0} s no still sample (moved < {StillM * 100f:0} cm in {EveryS:0.0} s) more than {TolM:0.00} m over its support, sampled every {EveryS:0.0} s for {TrackS:0} s from the moment the part exists "
                + "(the thrown turret: from its landing). "
                + $"LATE: after its {TrackS:0} s each part is followed on until it goes (at most {LateEndS:0} s from the kill: a base fades at 14 s and what lay on it must come down); "
                + $"LATE FAIL = still and more than {TolM:0.00} m over its support for longer than {HoldS:0.0} s in a row. "
                + "base: whole = WreckFx.Force 2 (never breaks; the gun type keeps the game's draw, as Force 2 also stops its break-up), broken = Force 1, game's draw = Force 0.").AppendLine();
            md.AppendLine("| gun | base | id | part | samples judged | worst still gap m | worst any gap m (after 1.5 s) | at s | late: longest still hover s | late: its gap m | result |");
            md.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|");
            if (ue) OzUnitEditor.SetPanelsHidden(true);
            Time.captureFramerate = 60;
            if (!AbOff)
                foreach (var k in Kinds)
                    foreach (var f in k.type == "gun" ? new[] { 0, 1 } : new[] { 2, 1 })
                    {
                        var e = One(k.type, k.ver, k.label, f, k.label.Replace(" ", "_") + (f == 1 ? "_basebroken" : f == 2 ? "_basewhole" : "_basedraw"), false); while (e.MoveNext()) yield return e.Current;
                    }
            for (int i = 0; i < (AbOff ? 5 : 3); i++) { int f = i % 3 == 2 ? 0 : 2; var e = One("gatling", 0, "gatling (owner's case, night)", f, (AbOff ? "AB_fixOFF_" : "") + "owner_gatling_night_" + i, true); while (e.MoveNext()) yield return e.Current; }
            Time.captureFramerate = 0;
            WreckFx.Force = force0; G.gameSpeed = speed0;
            if (ue) { OzUePlayground.ClearField(); OzUnitEditor.SetPanelsHidden(false); }
            md.AppendLine().AppendLine($"{pass} PASS, {fail} FAIL; {frames} frames. {OzGunSeatWreck.Status()}; seats broken off (GunChairDebris) {GunChairDebris.Broken}.");
            if (AbOff) { md.AppendLine($"A/B (the fix OFF): {fail} FAIL = {(fail > 0 ? "the check SEES the old hover (good)" : "the old hover was not reproduced by these ids (the check is not shown to catch it)")}."); OzGunSeatWreck.On = true; ClearSplits(); AbOff = false; }
            md.AppendLine(OzWreckGround.Status());
            Report = md.ToString();
            if (!string.IsNullOrEmpty(Folder)) File.WriteAllText(Path.Combine(Folder, "gun_seat_wreck_report.md"), Report);
            Debug.Log(Tag + "\n" + Report);
            Destroy(this);
        }

        void Night() { if (day) day.timeOfDay = 0.62f; }
        static string F(float v) => float.IsNaN(v) ? "-" : v.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);

        sealed class Track { public Transform t; public string what; public float born = -1f, worstStill = float.NaN, worstAny = float.NaN, worstAt, stillSince = -1f, lateHeld, lateGap = float.NaN; public int judged; public Vector3 last; public bool hasLast; }

        IEnumerator One(string type, int ver, string label, int force, string shotName, bool owner)
        {
            if (OzUnitEditor.Active) { OzUePlayground.ClearField(); yield return null; }
            var foe = G.player == Team.Red ? Team.Blue : Team.Red;
            if (!Area(out int ox, out int oy, 12, 10)) { md.AppendLine($"| {label} | {Base(force)} | - | no free ground near the camera | | | | | | | FAIL |"); fail++; yield break; }
            WreckFx.Force = force;
            var o = W.CreateObject("cannon", type, ox + 6 * 16 - 16, oy + 5 * 16 - 16, foe);
            if (type == "gun" && ver > 0) OzGun.Models.Pin(o, ver);
            W.Emit(ZEventType.Created, o);
            if (OzUnitEditor.Active) OzUePlayground.MarkChanged();
            yield return null; yield return null;
            var mid = ZGame.PxToWorld(o.CenterX, o.CenterY); if (rts) rts.FlyTo(mid);
            float until = Time.time + 1f; while (Time.time < until) { Night(); yield return null; }
            o.SetHealth(0); W.UpdateObjectHealth(o, -1);                                                       // killed the normal way
            UnitView v = null; until = Time.time + 4f;
            while (Time.time < until) { Night(); if (G.Views.TryGetValue(o.Id, out v) && v && v.Dying) break; yield return null; }
            if (!v || !v.Dying) { md.AppendLine($"| {label} | {Base(force)} | {o.Id} | the gun did not die | | | | | | | FAIL |"); fail++; yield break; }
            var tracks = new List<Track>(); var seen = new HashSet<Transform>();
            float t0 = Time.time, nextSample = t0, nextFrame = t0, end = t0 + LateEndS;
            while (Time.time < end)
            {
                Night(); yield return null;
                float now = Time.time;
                if (now >= nextFrame && !string.IsNullOrEmpty(Folder)) { nextFrame += owner || now - t0 < DenseS ? FrameEveryS : LateFrameS; ScreenCapture.CaptureScreenshot(Path.Combine(Folder, $"{shotName}_{(now - t0):00.0}s.png")); frames++; }
                if (now < nextSample) continue;
                nextSample += EveryS;
                if (v && !seen.Contains(v.transform)) { seen.Add(v.transform); tracks.Add(new Track { t = v.transform, what = "base", born = now }); }
                if (v && v.TossedPiece && v.TossedLanded && !seen.Contains(v.TossedPiece)) { seen.Add(v.TossedPiece); tracks.Add(new Track { t = v.TossedPiece, what = "thrown turret", born = now }); }
                foreach (var p in FindObjectsByType<WreckPieces>(FindObjectsSortMode.None))
                    if (p && p.UnitId == o.Id && !p.Fading && !seen.Contains(p.transform)) { seen.Add(p.transform); tracks.Add(new Track { t = p.transform, what = PieceName(p.transform), born = now }); }
                bool any = false;
                foreach (var tr in tracks)
                {
                    if (!tr.t) continue;
                    bool late = now - tr.born > TrackS;
                    if (!late) any = true;
                    var wp = tr.t.GetComponent<WreckPieces>(); if (wp && wp.Fading) { any = true; tr.stillSince = -1f; continue; }
                    float gap = Gap(tr.t, tr.what == "base" ? v.TossedPiece : null, out var low);
                    bool still = tr.hasLast && (low - tr.last).magnitude < StillM;
                    tr.last = low; tr.hasLast = true;
                    if (float.IsNaN(gap)) { tr.stillSince = -1f; continue; }
                    if (late)
                    {   // checker 15 Nov: followed on until it goes; its support may sink away under it
                        any = true;
                        if (still && gap > TolM) { if (tr.stillSince < 0f) tr.stillSince = now - EveryS; float held = now - tr.stillSince; if (held > tr.lateHeld) { tr.lateHeld = held; tr.lateGap = gap; } }
                        else tr.stillSince = -1f;
                        continue;
                    }
                    if (now - tr.born < AfterS) continue;
                    tr.judged++;
                    if (float.IsNaN(tr.worstAny) || gap > tr.worstAny) tr.worstAny = gap;
                    if (still && (float.IsNaN(tr.worstStill) || gap > tr.worstStill)) { tr.worstStill = gap; tr.worstAt = now - t0; }
                }
                if (!any && now - t0 > 8f) break;                                                              // every part had its 6 s and is gone (or no longer drawn)
            }
            foreach (var tr in tracks)
            {
                bool ok = tr.judged > 0 && (float.IsNaN(tr.worstStill) || tr.worstStill <= TolM);
                if (tr.judged == 0 && tr.what == "base") ok = true;                                            // a base that sank / broke at once: nothing to judge
                bool lateOk = tr.lateHeld <= HoldS; ok &= lateOk;
                md.AppendLine($"| {label} | {Base(force)} | {o.Id} | {tr.what} | {tr.judged} | {F(tr.worstStill)} | {F(tr.worstAny)} | {F(tr.worstAt)} | {F(tr.lateHeld)} | {F(tr.lateGap)} | {(ok ? "PASS" : lateOk ? "FAIL" : "LATE FAIL")} |");
                if (ok) pass++; else fail++;
            }
        }

        static string PieceName(Transform piece)
        {
            foreach (Transform c in piece) if (c.name != "Hull") return c.name;
            return piece.name;
        }

        // ------------------------------------------------------------------ the measure: the drawn points only
        static readonly Dictionary<Mesh, Vector3[]> used = new Dictionary<Mesh, Vector3[]>();
        static readonly List<Vector3> vb = new List<Vector3>(); static readonly List<int> ib = new List<int>();
        static readonly List<Renderer> rs = new List<Renderer>();
        readonly Vector3[] low = new Vector3[32];

        static Vector3[] Used(Mesh m)
        {
            if (used.TryGetValue(m, out var a)) return a;
            var set = new HashSet<int>(); vb.Clear(); m.GetVertices(vb);
            for (int s = 0; s < m.subMeshCount; s++) { if (m.GetTopology(s) != MeshTopology.Triangles) continue; ib.Clear(); m.GetIndices(ib, s); foreach (int i in ib) set.Add(i); }
            var l = new List<int>(set); int st = Mathf.Max(1, Mathf.CeilToInt(l.Count / 800f));
            var o = new List<Vector3>(); for (int i = 0; i < l.Count; i += st) o.Add(vb[l[i]]);
            a = o.ToArray(); used[m] = a; return a;
        }

        static bool Solid(Renderer r)
        {
            if (!r || !r.enabled || !(r is MeshRenderer)) return false;
            if (r.sharedMaterial && r.sharedMaterial.renderQueue >= 2450) return false;
            var n = r.name.ToLowerInvariant();
            return !(n.Contains("shadow") || n.Contains("label") || n.Contains("selection") || n.Contains("ring") || n.Contains("marker") || n.Contains("decal") || OzWreckGround.FxName(n));
        }

        /// <summary>The smallest gap of its 32 lowest drawn points to the support right under each; NaN = nothing drawn. skip: a subtree left out
        /// (the base without its thrown turret); a robot riding on it (a UnitView of its own) is left out.</summary>
        float Gap(Transform root, Transform skip, out Vector3 lowest)
        {
            lowest = Vector3.zero; int n = 0;
            rs.Clear(); root.GetComponentsInChildren(false, rs);
            var self = root.GetComponent<UnitView>();
            foreach (var r in rs)
            {
                if (!Solid(r) || (skip && r.transform.IsChildOf(skip))) continue;
                var owner = r.GetComponentInParent<UnitView>(); if (owner && owner != self) continue;
                if (!r.TryGetComponent<MeshFilter>(out var mf) || !mf.sharedMesh) continue;
                var m = mf.sharedMesh; var mt = r.localToWorldMatrix;
                if (!m.isReadable) { var b = r.bounds; Add(new Vector3(b.center.x, b.min.y, b.center.z), ref n); continue; }
                foreach (var p in Used(m)) Add(mt.MultiplyPoint3x4(p), ref n);
            }
            if (n == 0) return float.NaN;
            float gap = float.MaxValue; lowest = low[0];
            for (int i = 0; i < n; i++)
            {
                if (low[i].y < lowest.y) lowest = low[i];
                float s = OzWreckGround.Under(low[i], root); if (s > float.MinValue) gap = Mathf.Min(gap, low[i].y - s);
            }
            return gap == float.MaxValue ? float.NaN : gap;
        }

        void Add(Vector3 w, ref int n)
        {
            if (n < low.Length) { low[n++] = w; return; }
            int hi = 0; for (int k = 1; k < n; k++) if (low[k].y > low[hi].y) hi = k;
            if (w.y < low[hi].y) low[hi] = w;
        }

        // ------------------------------------------------------------------ free ground near the camera (as kit wreck_rest_ground_r6's check)
        bool Area(out int ox, out int oy, int w, int h)
        {
            ox = oy = 0;
            var f = rts ? rts.focus : Vector3.zero;
            int cx = Mathf.RoundToInt(f.x * 8f) >> 4, cy = Mathf.RoundToInt(-f.z * 8f) >> 4;
            for (int r = 0; r < 50; r += 2)
                for (int dy = -r; dy <= r; dy += 2)
                    for (int dx = -r; dx <= r; dx += 2)
                    {
                        int tx = cx + dx - w / 2, ty = cy + dy - h / 2;
                        if (Free(tx, ty, w, h)) { ox = tx * 16; oy = ty * 16; return true; }
                    }
            return false;
        }

        bool Free(int tx, int ty, int w, int h)
        {
            if (tx < 1 || ty < 1 || tx + w > W.Map.width - 1 || ty + h > W.Map.height - 1) return false;
            for (int j = ty; j < ty + h; j++)
                for (int i = tx; i < tx + w; i++)
                {
                    if (!W.Path.TilePassableGrid(i, j, false, false)) return false;
                    var k = W.Map.ClassAt(i, j); if (k != ZClass.Ground && k != ZClass.Road) return false;
                    if (W.PlatformAtTile(i, j) != 0) return false;
                }
            int x0 = tx * 16 - 32, y0 = ty * 16 - 32, x1 = (tx + w) * 16 + 32, y1 = (ty + h) * 16 + 32;
            if (W.Pathing != null) foreach (var L in W.Pathing.Lanes) if (L.ZX1 > x0 && L.ZX0 < x1 && L.ZY1 > y0 && L.ZY0 < y1) return false;
            foreach (var o in W.Objects) if (!o.Destroyed && o.CenterX > x0 && o.CenterX < x1 && o.CenterY > y0 && o.CenterY < y1 && (o.IsUnit || o.Kind == ZKind.Building || o.Type == ZWorld.PlatformType)) return false;
            return true;
        }
    }
}
#endif
