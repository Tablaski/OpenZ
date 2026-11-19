#if UNITY_EDITOR
// OpenZ (GPL-3.0)
// the Play check 'OpenZ/1.5/Checks/Robot Pieces/...' (editor only). Numbers + frames at the normal RTS camera.
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// Every robot type is put down near the camera, 8 at a time, and killed the ways robots die (through the world, health 0, as every wreck
    /// check): shot x3 (the pop: every kill that is not a splash or a flame), blown up x2 (a splash hit: ragdoll / Zod's flight), burnt x1 (a flame:
    /// the melt), run over x1 (DV65 pancake), a sniper's headshot x1 (DV116: the head falls off). Types: grunt, tough, sniper, pyro, psycho, laser,
    /// electrobot; in the Unit editor also the heroes (Brad and Alan) and EvilZod. Teams alternate per type (both teams). Then the owner's case:
    /// 8 grunts shot at night and 8 by day (frames every 0.2 s).
    /// EVERY piece is followed every 0.1 s for 8 s from the kill: each pop piece (its own rigid parts: the helmet, the pack, the hip plate, the gun;
    /// its baked body parts; the made-up chunks), the loose head of a headshot, the pancake, EvilZod's burst pieces and the body (ragdoll / death
    /// clip / flight) while it is drawn. Measure: its lowest DRAWN point (the vertices of its meshes; a mesh the editor cannot read: its own box's
    /// 8 corners; a skinned body: baked) over the support right under it (OzWreckGround.Under: the walk layer, the ground, what stands still).
    /// PASS for a piece = after 1.5 s from the kill no STILL sample (its lowest point moved under 1 cm in 0.1 s) more than 0.05 m over its support.
    /// Water and popping (end of life) samples are not judged. Report + frames: the kit's proof folder.
    /// A/B menu: the owner's case with OzRobotPieceRest.On = false (must show the old hover: the helmet and pack metres up).
    /// </summary>
    public sealed class OzRobotPiecesCheck : MonoBehaviour
    {
        public const string Tag = "[OpenZ 1.5 Robot Pieces]";
        public const float TolM = 0.05f, AfterS = 1.5f, TrackS = 8f, EveryS = 0.1f, StillM = 0.01f, OwnerFrameS = 0.2f, FrameS = 0.5f, Night = 0.62f, Day = 0.05f;
        public static string Folder = "";
        public static bool Running { get; private set; }
        public static bool AbOff { get; private set; }
        public static string Report { get; private set; } = "no run yet";

        enum Way { Shot, BlownUp, Flame, RunOver, Headshot }
        static readonly Way[] Ways = { Way.Shot, Way.Shot, Way.Shot, Way.BlownUp, Way.BlownUp, Way.Flame, Way.RunOver, Way.Headshot };
        static readonly string[] Types = { "grunt", "tough", "sniper", "pyro", "psycho", "laser", "electrobot" };
        const string Heroes = "heroes";

        ZGame G; ZWorld W; RtsCamera rts; DayNight day;
        readonly StringBuilder md = new StringBuilder(), fails = new StringBuilder();
        int pass, fail, frames, batches; float speed0; float tod = Night;

        public static void Begin(string folder, bool abOff = false)
        {
            if (Running || !ZGame.I || ZGame.I.World == null) { Debug.LogWarning(Tag + " enter Play first (the Unit editor, or a level)"); return; }
            Folder = folder; AbOff = abOff;
            ZGame.I.gameObject.AddComponent<OzRobotPiecesCheck>();
        }

        void Start() { Running = true; StartCoroutine(Run()); }
        void OnDestroy() { Running = false; Time.captureFramerate = 0; if (G) G.gameSpeed = speed0; if (AbOff) { OzRobotPieceRest.On = true; AbOff = false; } }

        IEnumerator Run()
        {
            G = ZGame.I; W = G.World; rts = FindFirstObjectByType<RtsCamera>(); day = FindFirstObjectByType<DayNight>();
            speed0 = G.gameSpeed; G.gameSpeed = 1f;
            if (!string.IsNullOrEmpty(Folder)) Directory.CreateDirectory(Folder);
            bool ue = OzUnitEditor.Active;
            if (AbOff) OzRobotPieceRest.On = false;
            md.AppendLine($"# Robot pieces check ({System.DateTime.Now:yyyy-MM-dd HH:mm}), {(ue ? "the Unit editor" : "a level")}, 60 fps frame-locked, x1{(AbOff ? ", A/B: THE FIX OFF (the owner's case only; a FAIL here = the check sees the old hover)" : "")}").AppendLine();
            md.AppendLine("gap = the lowest DRAWN point of the piece over the support right under it (OzWreckGround.Under). "
                + $"PASS = after {AfterS:0.0} s from the kill no still sample (its lowest point moved < {StillM * 100f:0} cm in {EveryS:0.0} s) more than {TolM:0.00} m over its support; "
                + $"every piece sampled every {EveryS:0.0} s for {TrackS:0} s from the kill (water and end-of-life pop-out samples not judged).").AppendLine();
            md.AppendLine("| batch | type | team | way | victim | death drawn | pieces | samples judged | worst still gap m | worst piece | at s | failed pieces | result |");
            md.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|");
            if (ue) OzUnitEditor.SetPanelsHidden(true);
            Time.captureFramerate = 60;
            int n = 0;
            { tod = Night; var e = Batch("grunt", "8 grunts shot, night", true, n++, (AbOff ? "AB_fixOFF_" : "") + "owner_grunts_night"); while (e.MoveNext()) yield return e.Current; }
            if (!AbOff)
            {
                { tod = Day; var e = Batch("grunt", "8 grunts shot, day", true, n++, "owner_grunts_day"); while (e.MoveNext()) yield return e.Current; }
                tod = Night;
                foreach (var t in Types) { var e = Batch(t, t + ": every way, night", false, n++, t); while (e.MoveNext()) yield return e.Current; }
                if (ue)
                {
                    { var e = Batch(Heroes, "heroes (Brad + Alan): every way, night", false, n++, "heroes"); while (e.MoveNext()) yield return e.Current; }
                    { var e = Batch(ZEvilZod.Type, "EvilZod: shot / blown up, night", false, n++, "evilzod"); while (e.MoveNext()) yield return e.Current; }
                }
                else md.AppendLine("| - | heroes, EvilZod | | | | | | | | | | | only in the Unit editor (a hero's death may end a level) |");
            }
            Time.captureFramerate = 0;
            G.gameSpeed = speed0;
            if (ue) { OzUePlayground.ClearField(); OzUnitEditor.SetPanelsHidden(false); }
            md.AppendLine().AppendLine($"{pass} PASS, {fail} FAIL victims; {batches} batches; {frames} frames. {OzRobotPieceRest.Status()}; pops {RobotPopDeath.Pops}, body parts thrown {RobotPopDeath.BodyPartsThrown}.");
            if (AbOff) { md.AppendLine($"A/B (the fix OFF): {fail} FAIL = {(fail > 0 ? "the check SEES the old hover (good)" : "the old hover was not reproduced (the check is not shown to catch it)")}."); OzRobotPieceRest.On = true; AbOff = false; }
            if (fails.Length > 0) md.AppendLine().AppendLine("## Failed pieces").AppendLine().AppendLine("| batch | victim | piece | worst still gap m | at s | lowest drawn point |").AppendLine("|---|---|---|---|---|---|").Append(fails);
            Report = md.ToString();
            if (!string.IsNullOrEmpty(Folder)) File.WriteAllText(Path.Combine(Folder, "robot_pieces_report.md"), Report);
            Debug.Log(Tag + "\n" + Report);
            Destroy(this);
        }

        void Tod() { if (day) day.timeOfDay = tod; }
        static string F(float v) => float.IsNaN(v) ? "-" : v.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);

        sealed class Victim { public ZObject o; public Way way; public Team team; public Vector3 at; public UnitView v; public string drawn = "-"; public int pieces, judged; public float worst = float.NaN, worstAt; public string worstWhat = "-"; public int failed; }
        sealed class Track { public Transform t; public string what; public Victim of; public bool body; public float worst = float.NaN, worstAt; public int judged; public Vector3 last, worstLow; public bool hasLast; }

        IEnumerator Batch(string type, string label, bool owner, int no, string shot)
        {
            batches++;
            if (OzUnitEditor.Active) { OzUePlayground.ClearField(); yield return null; }
            if (type == ZEvilZod.Type && !ZWorld.EvilZodDeviation) { md.AppendLine($"| {no} | {type} | | | | EvilZod is switched off in this game | | | | | | | SKIP |"); yield break; }
            var foe = G.player == Team.Red ? Team.Blue : Team.Red;
            Team team = type == Heroes ? Team.Red : type == ZEvilZod.Type ? Team.Blue : no % 2 == 0 ? foe : G.player;
            if (!Area(out int ox, out int oy, 18, 12)) { md.AppendLine($"| {no} | {type} | {team} | | | no free ground near the camera | | | | | | | FAIL |"); fail++; yield break; }
            var vs = new List<Victim>();
            int count = type == Heroes ? 4 : type == ZEvilZod.Type ? 2 : 8;
            for (int i = 0; i < count; i++)
            {
                int x = ox + (2 + (i % 4) * 4) * 16, y = oy + (2 + (i / 4) * 6) * 16;
                Way way = owner ? Way.Shot : type == ZEvilZod.Type ? (i == 0 ? Way.Shot : Way.BlownUp) : type == Heroes ? new[] { Way.Shot, Way.BlownUp, Way.Headshot, Way.RunOver }[i] : Ways[i];
                if (type == Heroes)
                {
                    if (i % 2 == 1) continue;                                                                   // a squad = Brad + Alan
                    var h = W.CreateHeroSquad(ZHeroSquads.AlanBrad, team, new[] { x >> 4, y >> 4 });
                    if (h == null) continue;
                    G.SpawnUnitView(h); vs.Add(new Victim { o = h, way = way, team = team });
                    foreach (var m in h.Minions) { G.SpawnUnitView(m); vs.Add(new Victim { o = m, way = new[] { Way.Shot, Way.BlownUp, Way.Headshot, Way.RunOver }[i + 1], team = team }); }
                    continue;
                }
                ZObject o;
                try { o = type == ZEvilZod.Type ? W.CreateRobotGroup(type, x, y, team, 1) : W.CreateObject("robot", type, x, y, team); }
                catch (System.Exception ex) { md.AppendLine($"| {no} | {type} | {team} | | | could not be made: {ex.Message} | | | | | | | SKIP |"); yield break; }
                if (o == null) { md.AppendLine($"| {no} | {type} | {team} | | | could not be made | | | | | | | SKIP |"); yield break; }
                G.SpawnUnitView(o);
                vs.Add(new Victim { o = o, way = way, team = team });
            }
            if (OzUnitEditor.Active) OzUePlayground.MarkChanged();
            var mid = ZGame.PxToWorld(ox + 9 * 16, oy + 6 * 16); if (rts) rts.FlyTo(mid);
            float until = Time.time + 1.2f; while (Time.time < until) { Tod(); yield return null; }
            foreach (var vi in vs) { G.Views.TryGetValue(vi.o.Id, out vi.v); vi.at = vi.v ? vi.v.transform.position : ZGame.PxToWorld(vi.o.CenterX, vi.o.CenterY); }

            // ---- what is there before the kills (pieces still lying from an earlier batch are not judged here)
            var before = new HashSet<Transform>();
            RobotPopDeath.ListPieces(info); foreach (var p in info) before.Add(p.t);
            foreach (var a in FindObjectsByType<AnticCan>(FindObjectsSortMode.None)) before.Add(a.transform);
            foreach (var s in FindObjectsByType<SquashedRemains>(FindObjectsSortMode.None)) before.Add(s.transform);
            foreach (var b in FindObjectsByType<OzEvilZodBurst>(FindObjectsSortMode.None)) foreach (Transform c in b.transform) before.Add(c);

            // ---- the kills, 0.15 s apart (the game's FullPerFrame shrink is not what is checked here)
            float t0 = Time.time;
            foreach (var vi in vs)
            {
                if (vi.o.Destroyed) { vi.drawn = "died before the kill"; continue; }
                Kill(vi.o, vi.way);
                float w = Time.time + 0.15f; while (Time.time < w) { Tod(); yield return null; }
            }

            // ---- follow every piece
            var tracks = new List<Track>(); var seen = new HashSet<Transform>(before);
            float nextSample = Time.time, nextFrame = Time.time, end = Time.time + TrackS, frameEvery = owner ? OwnerFrameS : FrameS;
            while (Time.time < end)
            {
                Tod(); yield return null;
                float now = Time.time;
                if (now >= nextFrame && !string.IsNullOrEmpty(Folder)) { nextFrame += frameEvery; ScreenCapture.CaptureScreenshot(Path.Combine(Folder, $"{shot}_{(now - t0):00.0}s.png")); frames++; }
                if (now < nextSample) continue;
                nextSample += EveryS;
                RobotPopDeath.ListPieces(info);
                var popping = new HashSet<Transform>(); var wet = new HashSet<Transform>();
                foreach (var p in info)
                {
                    if (p.popping) popping.Add(p.t); if (p.wet) wet.Add(p.t);
                    if (seen.Add(p.t)) tracks.Add(new Track { t = p.t, what = "pop piece " + MeshName(p.t), of = Near(vs, p.t.position) });
                }
                foreach (var a in FindObjectsByType<AnticCan>(FindObjectsSortMode.None))
                    if (a.name.StartsWith("OZ_HeadshotHead_") && seen.Add(a.transform)) tracks.Add(new Track { t = a.transform, what = "loose head (headshot)", of = Near(vs, a.transform.position) });
                foreach (var s in FindObjectsByType<SquashedRemains>(FindObjectsSortMode.None))
                    if (seen.Add(s.transform)) tracks.Add(new Track { t = s.transform, what = "pancake (run over)", of = Near(vs, s.transform.position) });
                foreach (var b in FindObjectsByType<OzEvilZodBurst>(FindObjectsSortMode.None))
                    foreach (Transform c in b.transform) if (seen.Add(c)) tracks.Add(new Track { t = c, what = "EvilZod piece " + c.name, of = Near(vs, c.position) });
                foreach (var vi in vs)
                {
                    if (!vi.v) G.Views.TryGetValue(vi.o.Id, out vi.v);
                    if (!vi.v || !vi.v.Dying) continue;
                    if (vi.drawn == "-") vi.drawn = vi.v.Popped ? "pop" : DeathName(vi.v);
                    if (!vi.v.Popped && vi.v.gameObject.activeInHierarchy && seen.Add(vi.v.transform)) tracks.Add(new Track { t = vi.v.transform, what = "body (" + vi.drawn + ")", of = vi, body = true });
                }
                foreach (var tr in tracks)
                {
                    if (!tr.t || !tr.t.gameObject.activeInHierarchy || popping.Contains(tr.t) || wet.Contains(tr.t) || PopOut.IsPopping(tr.t)) { tr.hasLast = false; continue; }
                    if (OzWaterLanding.InWater(tr.t)) { tr.hasLast = false; continue; }
                    float gap = Gap(tr.t, tr.body, out var low);
                    bool still = tr.hasLast && (low - tr.last).magnitude < StillM;
                    tr.last = low; tr.hasLast = true;
                    if (float.IsNaN(gap) || now - t0 < AfterS) continue;
                    tr.judged++;
                    if (still && (float.IsNaN(tr.worst) || gap > tr.worst)) { tr.worst = gap; tr.worstAt = now - t0; tr.worstLow = low; }
                }
            }
            // ---- per victim
            foreach (var tr in tracks)
            {
                var vi = tr.of; if (vi == null) continue;
                vi.pieces++; vi.judged += tr.judged;
                if (!float.IsNaN(tr.worst) && (float.IsNaN(vi.worst) || tr.worst > vi.worst)) { vi.worst = tr.worst; vi.worstAt = tr.worstAt; vi.worstWhat = tr.what; }
                if (!float.IsNaN(tr.worst) && tr.worst > TolM)
                {
                    vi.failed++;
                    fails.AppendLine($"| {no} {label} | {vi.o.Type} #{vi.o.Id} ({vi.way}) | {tr.what} | {F(tr.worst)} | {F(tr.worstAt)} | ({tr.worstLow.x:0.0}, {tr.worstLow.y:0.00}, {tr.worstLow.z:0.0}) |");
                }
            }
            foreach (var vi in vs)
            {
                bool ok = vi.failed == 0 && (vi.pieces > 0 || vi.drawn == "died before the kill");
                md.AppendLine($"| {no} {label} | {vi.o.Type} | {vi.team} | {vi.way} | #{vi.o.Id} | {vi.drawn} | {vi.pieces} | {vi.judged} | {F(vi.worst)} | {vi.worstWhat} | {F(vi.worstAt)} | {vi.failed} | {(ok ? "PASS" : vi.pieces == 0 ? "FAIL (nothing seen)" : "FAIL")} |");
                if (ok) pass++; else fail++;
            }
        }

        readonly List<RobotPopDeath.PieceInfo> info = new List<RobotPopDeath.PieceInfo>();

        static Victim Near(List<Victim> vs, Vector3 p)
        {
            Victim best = null; float bd = float.MaxValue;
            foreach (var v in vs) { var d = p - v.at; d.y = 0f; if (d.sqrMagnitude < bd) { bd = d.sqrMagnitude; best = v; } }
            return best;
        }

        static string MeshName(Transform t)
        {
            var mf = t.GetComponentInChildren<MeshFilter>(); return mf && mf.sharedMesh ? mf.sharedMesh.name : t.name;
        }

        static string DeathName(UnitView v)
        {
            var f = typeof(UnitView).GetField("death", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return f != null ? f.GetValue(v).ToString() : "dying";
        }

        /// <summary>The world's own death of a robot, flagged the way it is to be drawn (the death event reads the flags: ZWorld.RelayObjectDeath).</summary>
        void Kill(ZObject o, Way way)
        {
            switch (way)
            {
                case Way.BlownUp: o.LastMissileHit = W.Time; break;
                case Way.Flame: o.LastFireHit = W.Time; break;
                case Way.RunOver: o.Squashed = true; break;
                case Way.Headshot:
                    var pr = typeof(ZObject).GetProperty("HeadshotBy");
                    var set = pr != null ? pr.GetSetMethod(true) : null;
                    if (set != null) set.Invoke(o, new object[] { o.Id });                                  // a sniper's headshot (DV116); no shooter: no call
                    break;
            }
            o.SetHealth(0); W.UpdateObjectHealth(o, -1);
        }

        // ------------------------------------------------------------------ the measure: what is drawn
        static readonly Dictionary<Mesh, Vector3[]> used = new Dictionary<Mesh, Vector3[]>();
        static readonly List<Vector3> vb = new List<Vector3>(); static readonly List<int> ib = new List<int>();
        static readonly List<Renderer> rs = new List<Renderer>();
        readonly Vector3[] low = new Vector3[32];
        Mesh bake;

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
            if (!r || !r.enabled || !(r is MeshRenderer || r is SkinnedMeshRenderer)) return false;
            if (r.sharedMaterial && r.sharedMaterial.renderQueue >= 2450) return false;
            var n = r.name.ToLowerInvariant();
            return !(n.Contains("shadow") || n.Contains("label") || n.Contains("selection") || n.Contains("ring") || n.Contains("marker") || n.Contains("decal") || n.Contains("glow") || n.Contains("flame"));
        }

        /// <summary>The smallest gap of its 32 lowest drawn points to the support right under each; NaN = nothing drawn.</summary>
        float Gap(Transform root, bool body, out Vector3 lowest)
        {
            lowest = Vector3.zero; int n = 0;
            rs.Clear(); root.GetComponentsInChildren(false, rs);
            foreach (var r in rs)
            {
                if (!Solid(r)) continue;
                if (r is SkinnedMeshRenderer sk)
                {
                    if (!sk.sharedMesh) continue;
                    if (!bake) bake = new Mesh { name = "OzRobotPiecesCheck bake" };
                    sk.BakeMesh(bake, true); vb.Clear(); bake.GetVertices(vb);
                    var mt0 = Matrix4x4.TRS(sk.transform.position, sk.transform.rotation, Vector3.one);
                    int st = Mathf.Max(1, vb.Count / 1500);
                    for (int i = 0; i < vb.Count; i += st) Add(mt0.MultiplyPoint3x4(vb[i]), ref n);
                    continue;
                }
                if (!r.TryGetComponent<MeshFilter>(out var mf) || !mf.sharedMesh) continue;
                var m = mf.sharedMesh; var mt = r.localToWorldMatrix;
                if (!m.isReadable)
                {   // the mesh's own box: its 8 corners (the lowest corner is never over the lowest drawn point)
                    var b = m.bounds;
                    for (int i = 0; i < 8; i++) Add(mt.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))), ref n);
                    continue;
                }
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

        // ------------------------------------------------------------------ free ground near the camera (as kit gun_seat_wreck_r8's check)
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
