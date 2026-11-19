#if UNITY_EDITOR
// OpenZ (GPL-3.0)
// (editor only; numbers, no pictures).
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// In Play in the Unit editor: for every vehicle kind a Mammoth can ram (jeep, light v1 / v2, mech v1 / v2, medium tank, heavy v1 / v2,
    /// MLRS v1 / v2, crane, APC, Bizon, Mammoth; the shuttle is never rammed by the rules) a player Mammoth drives on a plain MOVE order through
    /// an enemy one standing on flat ground, at night (the lamps lit). From the hit it records every frame for <see cref="Seconds"/> s after the
    /// death is drawn (transforms only): the wreck hull's lowest drawn point against the ground under it, its turn rate (yaw / pitch / roll) and
    /// speed, the shove frames; a census of every light of the vehicle (real lights, lamp / glow / beacon / neon / cone / pool / lens parts,
    /// transparent glows, the big tanks' code-built lamp holders, VehicleLights' drawn glows / cones / pool / spot) at 1, 3, 6, 12 and 20 s after
    /// the death: shown, and hovering = shown with no solid part of the wreck (hull, thrown turret, its pieces) within 0.6 m; during the flip
    /// the big-tank holders' offset from the hull. Sets: "before" = the kit's three switches off and the old lever (what the test exe
    /// test_1_5_20261111b shows), "after" = the switches on (with the physics flip when it is in the project), "after_lever" = the switches on
    /// with the old lever. PASS per row: a hull that stays lies at 0 +- 5 cm and turns no more after it settled (at most 0.5 deg/s), 0 hovering
    /// lights at every census and nothing drawn by VehicleLights after the death; pieces (physics bodies) are reported.
    /// </summary>
    public class OzRammedWreckRestCheck : MonoBehaviour
    {
        public const string Tag = "[OpenZ 1.5 Rammed Wreck Rest]";
        public static string Folder = "", Sets = "before,after";
        public static float Seconds = 20f;
        public static bool Running { get; private set; }
        public static string Report { get; private set; } = "no run yet";
        /// <summary>Kinds (type, model version 0 = as drawn, label).</summary>
        public static readonly (string type, int ver, string label)[] Kinds =
        {
            ("jeep", 0, "jeep"), ("light", 1, "light v1"), ("light", 2, "light v2"), ("medium", 1, "mech v1"), ("medium", 2, "mech v2"),
            (ZMTank.Type, 0, "medium tank"), ("heavy", 1, "heavy v1"), ("heavy", 2, "heavy v2"), ("missile_launcher", 1, "MLRS v1"),
            ("missile_launcher", 2, "MLRS v2"), ("crane", 0, "crane"), ("apc", 0, "APC"), (ZBigTanks.Bizon, 0, "Bizon"), (ZBigTanks.Mammoth, 0, "Mammoth"),
        };
        static readonly float[] CensusAt = { 1f, 3f, 6f, 12f, 20f };
        public const float HoverM = 0.6f, TolM = 0.05f, StillDegS = 0.5f;

        ZGame G; ZWorld W; RtsCamera rts; DayNight day;
        readonly StringBuilder md = new StringBuilder();
        int pass, fail;

        public static void Begin(string folder, string sets = null)
        {
            if (Running || !ZGame.I || ZGame.I.World == null) { Debug.LogWarning(Tag + " enter Play in the Unit editor first"); return; }
            Folder = folder; if (!string.IsNullOrEmpty(sets)) Sets = sets;
            ZGame.I.gameObject.AddComponent<OzRammedWreckRestCheck>();
        }

        void Start() { Running = true; StartCoroutine(Run()); }
        void OnDestroy() { Running = false; Time.captureFramerate = 0; }

        // ------------------------------------------------------------------ switches
        static readonly System.Type flipT = typeof(OzRamLever).Assembly.GetType("OpenZ.OzRamFlipPhysics");
        static readonly FieldInfo flipOn = flipT != null ? flipT.GetField("On", BindingFlags.Public | BindingFlags.Static) : null;
        static bool Phys { get => flipOn != null && (bool)flipOn.GetValue(null); set { if (flipOn != null) flipOn.SetValue(null, value); } }

        static void Set(string set)
        {
            bool on = set != "before";
            OzRammedWreckRest.RestOn = on; OzRammedWreckRest.SteadyShove = on; OzRammedWreckRest.LightsOff = on;
            Phys = set == "after";
        }

        // ------------------------------------------------------------------ the run
        IEnumerator Run()
        {
            G = ZGame.I; W = G.World; rts = FindFirstObjectByType<RtsCamera>(); day = FindFirstObjectByType<DayNight>();
            if (!string.IsNullOrEmpty(Folder)) Directory.CreateDirectory(Folder);
            bool rest0 = OzRammedWreckRest.RestOn, shove0 = OzRammedWreckRest.SteadyShove, lights0 = OzRammedWreckRest.LightsOff, phys0 = Phys;
            md.AppendLine($"# Rammed wreck rest check ({System.DateTime.Now:yyyy-MM-dd HH:mm}), level {G.level}, {(OzUnitEditor.Active ? "the Unit editor" : "a level")}, 60 fps frame-locked, night (0.62)").AppendLine();
            md.AppendLine($"physics flip (mammoth_flip_physics_v2) in the project: {(flipT != null ? "yes" : "no")}; ram rules: Mammoth {ZWorld.MammothRamDeviation}; big tank wreck shove {OzBigTankWreckShove.On}; lever {OzRamLever.On}").AppendLine();
            md.AppendLine("Columns: death = s from the hit to the drawn death; gap@death / end = the wreck hull's lowest drawn point over the ground under it (m; pieces = no hull left, its pieces' lowest points); settle = s after the death until it lies (0 +- 5 cm) and is still (< 1 deg/s, < 2 cm/s) for 0.5 s; "
                + "turn after = largest turn rate after it settled (deg/s; yaw / pitch / roll); spin frames = frames > 30 deg/s from 2 s after the death; shove / re-add = frames the shove moved it / frames its 'keeps the offset' pass added the whole offset and turn again; held = frames another writer's change was put back; "
                + "lights @1/3/6/12/20 s = shown / hovering (no solid part of the wreck within 0.6 m); VL = VehicleLights drew a glow / cone / pool / spot after the death; follow = largest drift of the big-tank lamp holders from the hull during the flip (m).").AppendLine();
            if (!ZWorld.MammothRamDeviation) { md.AppendLine("DV182 is off: nothing to check"); Finish(rest0, shove0, lights0, phys0); yield break; }
            if (OzUnitEditor.Active) OzUnitEditor.SetPanelsHidden(true);
            var me = G.player; var foe = me == Team.Red ? Team.Blue : Team.Red;
            foreach (var set in Sets.Split(','))
            {
                if (set == "after_lever" && flipT == null) continue;
                Set(set);
                md.AppendLine().AppendLine($"## {set}: rest {OzRammedWreckRest.RestOn}, steady shove {OzRammedWreckRest.SteadyShove}, lights off {OzRammedWreckRest.LightsOff}, physics flip {Phys}").AppendLine();
                md.AppendLine("| kind | death | gap@death | settle | gap end | turn after (y/p/r) | spin frames | shove / re-add | held | lights 1/3/6/12/20 s | VL | follow | hovering parts | result |");
                md.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|");
                foreach (var k in Kinds)
                {
                    Row r = null;
                    var e = OneKind(set, k, me, foe, x => r = x);
                    while (e.MoveNext()) yield return e.Current;
                    if (r != null) Line(set, k.label, r);
                }
            }
            Time.captureFramerate = 0;
            if (OzUnitEditor.Active) OzUnitEditor.SetPanelsHidden(false);
            Finish(rest0, shove0, lights0, phys0);
        }

        sealed class Row
        {
            public bool hit, died, hull = true; public string why = "";
            public float deathAt = -1f, gapDeath = float.NaN, gapEnd = float.NaN, settle = -1f, turnAfter, yawA, pitchA, rollA, follow; public int spin, shove, held, vlAfter;
            public readonly int[] shown = new int[5], hover = new int[5]; public readonly HashSet<string> hovering = new HashSet<string>();
            public float piecesMax = float.NaN, piecesMin = float.NaN; public int pieces, reapplied;
        }

        IEnumerator OneKind(string set, (string type, int ver, string label) k, Team me, Team foe, System.Action<Row> done)
        {
            var row = new Row(); done(row);
            if (OzUnitEditor.Active) { OzUePlayground.ClearField(); yield return null; }
            if (!Area(out int ox, out int oy, 30, 12)) { row.why = "no free ground near the camera"; yield break; }
            int mx = ox + 4 * 16, my = oy + 6 * 16;
            var m = Make(ZBigTanks.Mammoth, mx, my, me, 0);
            var t = Make(k.type, mx + ZPathing.Radius(m) + 3 * 16 + 20, my, foe, k.ver);
            m.Damage = 0; t.Damage = 0;
            yield return null;
            var mid = ZGame.PxToWorld(t.CenterX, t.CenterY);
            if (rts) { rts.FlyTo(mid); }
            Time.captureFramerate = 60;
            float until = Time.time + 1.5f; while (Time.time < until) { Night(); yield return null; }
            W.SendWaypoints(me, m, new[] { ZWaypoint.Move(mx + 22 * 16, my) });
            until = Time.time + 30f;
            while (Time.time < until && !t.RamDoomed) { Night(); yield return null; }
            if (!t.RamDoomed) { row.why = $"no ram in 30 s (the Mammoth at {m.CenterX},{m.CenterY}, ramming {m.Ramming})"; Tidy(m, null, mid); yield break; }
            row.hit = true;
            float hitAt = Time.time;
            var tv = G.Views.TryGetValue(t.Id, out var v0) ? v0 : null;
            if (!tv) { row.why = "no view of the target"; Tidy(m, null, mid); yield break; }
            // the big tanks' lamp holders: their place in the hull's frame at the hit
            var holders = new List<(Transform h, Vector3 local)>();
            var fx = OzBigTanksFx.I; var bt = fx && OzBigTanks.Is(t.Type) ? fx.Of(t.Id) : null;
            if (bt != null) foreach (var a in new[] { "root", "hull" }) { var h = bt.Holder(a); if (h) holders.Add((h, tv.transform.InverseTransformPoint(h.position))); }
            var probe = new Probe();
            int shove0 = OzBigTankWreckShove.Shoves, held0 = OzRammedWreckRest.HeldStill, census = 0;
            Quaternion prevR = tv.transform.rotation; Vector3 prevP = tv.transform.position; float stillFor = 0f, deathAt = -1f;
            int lastShoves = OzBigTankWreckShove.Shoves, lastRe = OzBigTankWreckShove.Reapplied;
            while (tv && Time.time - hitAt < 40f)
            {
                Night();
                yield return null;
                if (!tv) break;
                var tr = tv.transform; float dt = Time.deltaTime; if (dt <= 0f) continue;
                if (deathAt < 0f)
                {
                    foreach (var (h, local) in holders) if (h && h.gameObject.activeInHierarchy) row.follow = Mathf.Max(row.follow, (tr.InverseTransformPoint(h.position) - local).magnitude * tr.lossyScale.x);
                    if (tv.Dying) { deathAt = Time.time; row.died = true; row.deathAt = deathAt - hitAt; row.gapDeath = probe.Gap(tr); prevR = tr.rotation; prevP = tr.position; }
                    continue;
                }
                float since = Time.time - deathAt;
                if (since > Seconds + 0.01f) break;
                // the turn rate (deg/s) and the speed of the wreck's root
                var dq = tr.rotation * Quaternion.Inverse(prevR); dq.ToAngleAxis(out float ang, out Vector3 ax); if (ang > 180f) ang -= 360f;
                float w = Mathf.Abs(ang) / dt, sp = (tr.position - prevP).magnitude / dt;
                prevR = tr.rotation; prevP = tr.position;
                if (OzBigTankWreckShove.Shoves != lastShoves) { row.shove++; lastShoves = OzBigTankWreckShove.Shoves; }
                if (OzBigTankWreckShove.Reapplied != lastRe) { row.reapplied++; lastRe = OzBigTankWreckShove.Reapplied; }
                if (since >= 2f && w > 30f) row.spin++;
                bool alive = tv.gameObject.activeInHierarchy && !tv.WreckSinking;
                float gap = alive ? probe.Gap(tr) : float.NaN;
                if (float.IsNaN(gap) && alive) row.hull = false;
                bool lying = float.IsNaN(gap) || Mathf.Abs(gap) <= TolM;
                if (row.settle < 0f)
                {
                    stillFor = lying && w < 1f && sp < 0.02f ? stillFor + dt : 0f;
                    if (stillFor >= 0.5f) row.settle = since;
                }
                else if (alive && w > row.turnAfter)
                {
                    row.turnAfter = w; float wy = ang / dt;
                    row.yawA = wy * ax.y; row.pitchA = wy * Vector3.Dot(ax, tr.right); row.rollA = wy * Vector3.Dot(ax, tr.forward);
                }
                if (alive) row.gapEnd = gap;
                if (Vl(tv)) row.vlAfter++;
                if (census < CensusAt.Length && since >= CensusAt[census]) { Census(tv, t.Id, row, census); census++; }
            }
            row.held = OzRammedWreckRest.HeldStill - held0;
            if (!row.hull) Pieces(t.Id, row);
            Tidy(m, tv, mid);
            yield return null;
        }

        void Night()
        {
            if (day) day.timeOfDay = 0.62f;
        }

        ZObject Make(string type, int cx, int cy, Team team, int ver)
        {
            var o = W.CreateObject("vehicle", type, cx - 16, cy - 16, team);
            if (ver > 0)
            {
                if (type == "heavy") OzHeavyTank.Pin(o, ver);
                else foreach (var u in OzV2Units.All) if (u.Type == type) u.Pin(o, ver);
            }
            W.Emit(ZEventType.Created, o);
            if (OzUnitEditor.Active) OzUePlayground.MarkChanged();
            return o;
        }

        void Tidy(ZObject m, UnitView tv, Vector3 mid)
        {
            Time.captureFramerate = 0;
            if (m != null && !m.Destroyed && m.KillAt < 0) { if (OzUnitEditor.Active) OzUePlayground.Remove(m); else { m.SetHealth(0); W.UpdateObjectHealth(m, -1); } }
            if (tv) { foreach (var kv in new List<KeyValuePair<int, UnitView>>(G.Views)) if (kv.Value == tv) G.Views.Remove(kv.Key); Destroy(tv.gameObject); }
            if (OzUnitEditor.Active) { OzUePlayground.SweepDebrisIn(q => (q - mid).sqrMagnitude < 60f * 60f); OzUePlayground.ClearField(); }
        }

        // ------------------------------------------------------------------ the lights
        static bool LampName(string n)
        {
            n = n.ToLowerInvariant();
            if (n.StartsWith("z_light") || n.Contains("light_tank") || n.Contains("lighttank") || n.Contains("lightv2") || n.Contains("light_v2")) return false;   // the light tank's own nodes
            return n.Contains("lamp") || n.Contains("light") || n.Contains("beacon") || n.Contains("neon") || n.Contains("glow") || n.Contains("halo") || n.Contains("flare")
                || n.Contains("lens") || n.Contains("bulb") || n.Contains("beam") || n.Contains("cone") || n.Contains("pool") || n.Contains("led");
        }
        static bool Flat(string n) { n = n.ToLowerInvariant(); return n.Contains("shadow") || n.Contains("label") || n.Contains("selection") || n.Contains("fire") || n.Contains("smoke") || n.Contains("flame") || n.Contains("scorch"); }
        static bool Glass(Renderer r) => r.sharedMaterial && r.sharedMaterial.renderQueue >= 2450;
        static bool Shown(Renderer r) => r && r.enabled && r.gameObject.activeInHierarchy && !r.forceRenderingOff;
        static bool SolidPart(Renderer r) => Shown(r) && (r is MeshRenderer || r is SkinnedMeshRenderer) && !Glass(r) && !LampName(r.name) && !Flat(r.name);

        void Census(UnitView tv, int id, Row row, int at)
        {
            var solid = new List<Bounds>();
            var tossed = tv.RamRestTossed;
            foreach (var t in new[] { tv.transform, tossed }) if (t) foreach (var r in t.GetComponentsInChildren<Renderer>()) if (SolidPart(r)) solid.Add(r.bounds);
            foreach (var p in PiecesOf(id)) foreach (var r in p.GetComponentsInChildren<Renderer>()) if (SolidPart(r)) solid.Add(r.bounds);
            bool Near(Bounds b) { foreach (var s in solid) { var e = s; e.Expand(HoverM * 2f); if (e.Intersects(b)) return true; } return false; }
            void One(string name, Bounds b) { row.shown[at]++; if (!Near(b)) { row.hover[at]++; row.hovering.Add(name); } }
            foreach (var t in new[] { tv.transform, tossed })
            {
                if (!t) continue;
                foreach (var r in t.GetComponentsInChildren<Renderer>())
                    if (Shown(r) && (r is MeshRenderer || r is SkinnedMeshRenderer) && !Flat(r.name) && (LampName(r.name) || Glass(r))) One(r.name, r.bounds);
                foreach (var l in t.GetComponentsInChildren<Light>())
                    if (l.enabled && l.gameObject.activeInHierarchy && l.intensity > 0f) One("Light " + l.name, new Bounds(l.transform.position, Vector3.one * 0.1f));
            }
            var fx = OzBigTanksFx.I; var bt = fx && OzBigTanks.Is(tv.Obj != null ? tv.Obj.Type : "") ? fx.Of(id) : null;
            if (bt != null)
                foreach (var a in new[] { "root", "hull", "turret" })
                {
                    var h = bt.Holder(a); if (!h || !h.gameObject.activeInHierarchy) continue;
                    foreach (var r in h.GetComponentsInChildren<Renderer>()) if (Shown(r)) One("holder " + a + ": " + r.name, r.bounds);
                    foreach (var l in h.GetComponentsInChildren<Light>()) if (l.enabled && l.intensity > 0f) One("holder " + a + ": Light " + l.name, new Bounds(l.transform.position, Vector3.one * 0.1f));
                }
        }

        static readonly FieldInfo rigOfF = typeof(VehicleLights).GetField("rigOf", BindingFlags.NonPublic | BindingFlags.Instance);
        /// <summary>VehicleLights draws something of this view now (a glow, a cone, the pool, the real spot).</summary>
        static bool Vl(UnitView v)
        {
            var vl = VehicleLights.I; if (!vl || rigOfF == null) return false;
            var d = rigOfF.GetValue(vl) as System.Collections.IDictionary; if (d == null || !d.Contains(v)) return false;
            var r = d[v]; if (r == null) return false;
            var tp = r.GetType();
            bool B(string f) { var fi = tp.GetField(f); return fi != null && (bool)fi.GetValue(r); }
            var spot = tp.GetField("Spot")?.GetValue(r) as Light;
            return B("GlowOn") || B("BeamOn") || B("PoolOn") || (spot && spot.enabled);
        }

        static readonly FieldInfo pieceId = typeof(WreckPieces).GetField("id", BindingFlags.NonPublic | BindingFlags.Instance);
        static List<WreckPieces> PiecesOf(int id)
        {
            var l = new List<WreckPieces>();
            if (pieceId == null) return l;
            foreach (var p in FindObjectsByType<WreckPieces>(FindObjectsSortMode.None)) if (p && (int)pieceId.GetValue(p) == id && !p.Fading) l.Add(p);
            return l;
        }

        void Pieces(int id, Row row)
        {
            float mx = float.MinValue, mn = float.MaxValue;
            foreach (var p in PiecesOf(id))
            {
                var g = new Probe().Gap(p.transform); if (float.IsNaN(g)) continue;
                row.pieces++; mx = Mathf.Max(mx, g); mn = Mathf.Min(mn, g);
            }
            if (row.pieces > 0) { row.piecesMax = mx; row.piecesMin = mn; }
        }

        /// <summary>The lowest drawn solid points of a transform's renderers against the ground right under them (as OzRammedWreckRest measures).</summary>
        sealed class Probe
        {
            readonly List<Vector3> pts = new List<Vector3>(); int sig = int.MinValue;
            static readonly RaycastHit[] hits = new RaycastHit[8];
            public float Gap(Transform root)
            {
                var rs = root.GetComponentsInChildren<Renderer>();
                int s = 17; foreach (var r in rs) if (SolidPart(r)) unchecked { s = s * 31 + r.GetInstanceID(); s = s * 31 + r.transform.localPosition.GetHashCode() + r.transform.localRotation.GetHashCode(); }
                if (s != sig)
                {
                    sig = s; pts.Clear(); var inv = root.worldToLocalMatrix;
                    foreach (var r in rs)
                    {
                        if (!SolidPart(r)) continue;
                        Mesh m = null; if (r is MeshRenderer && r.TryGetComponent<MeshFilter>(out var mf)) m = mf.sharedMesh;
                        var to = inv * r.localToWorldMatrix;
                        if (m && m.isReadable && m.vertexCount > 0) { var v = m.vertices; int st = Mathf.Max(1, v.Length / 300); for (int i = 0; i < v.Length; i += st) pts.Add(to.MultiplyPoint3x4(v[i])); }
                        else { var b = r.bounds; for (int c = 0; c < 8; c++) pts.Add(inv.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1)))); }
                    }
                }
                if (pts.Count == 0) return float.NaN;
                var mtx = root.localToWorldMatrix; var low = new List<Vector3>(pts.Count);
                foreach (var p in pts) low.Add(mtx.MultiplyPoint3x4(p));
                low.Sort((a, b) => a.y.CompareTo(b.y));
                float gap = float.MaxValue;
                for (int i = 0; i < low.Count && i < 32; i++)
                {
                    var p = low[i]; float best = float.MinValue;
                    if (ZGame.WalkLayer >= 0)
                    {
                        int n = Physics.RaycastNonAlloc(new Vector3(p.x, p.y + 1.5f, p.z), Vector3.down, hits, 81.5f, 1 << ZGame.WalkLayer, QueryTriggerInteraction.Ignore);
                        for (int j = 0; j < n; j++) { var c = hits[j].collider; if (c && c.transform.IsChildOf(root)) continue; best = Mathf.Max(best, hits[j].point.y); }
                    }
                    var g = ZGame.I; if (g) { float gy = g.GroundY(p.x, p.z); if (gy <= p.y + 1.5f) best = Mathf.Max(best, gy); }
                    if (best > float.MinValue) gap = Mathf.Min(gap, p.y - best);
                }
                return gap == float.MaxValue ? float.NaN : gap;
            }
        }

        // ------------------------------------------------------------------ the report
        static string F(float v, string f = "0.00") => float.IsNaN(v) ? "-" : v.ToString(f, System.Globalization.CultureInfo.InvariantCulture);

        void Line(string set, string label, Row r)
        {
            bool hullOk = !r.hull || (Mathf.Abs(r.gapEnd) <= TolM && r.settle >= 0f && r.turnAfter <= StillDegS);
            int hov = 0; foreach (var h in r.hover) hov += h;
            bool ok = r.hit && r.died && hullOk && hov == 0 && r.vlAfter == 0;
            string lights = string.Join(" ", System.Linq.Enumerable.Select(System.Linq.Enumerable.Range(0, 5), i => r.shown[i] + "/" + r.hover[i]));
            string gapEnd = r.hull ? F(r.gapEnd) : r.pieces > 0 ? $"pieces {r.pieces}: {F(r.piecesMin)}..{F(r.piecesMax)}" : "pieces gone";
            string what = r.hit ? "" : r.why;
            md.AppendLine($"| {label} | {(r.died ? F(r.deathAt) : "no death")} | {F(r.gapDeath)} | {(r.settle >= 0f ? F(r.settle) : "never")} | {gapEnd} | {F(r.turnAfter, "0.0")} ({F(r.yawA, "0.0")}/{F(r.pitchA, "0.0")}/{F(r.rollA, "0.0")}) | {r.spin} | {r.shove} / {r.reapplied} | {r.held} | {lights} | {r.vlAfter} | {F(r.follow)} | {string.Join(", ", r.hovering)} | {(ok ? "PASS" : "FAIL")} {what} |");
            if (set != "before") { if (ok) pass++; else fail++; }
        }

        void Finish(bool rest0, bool shove0, bool lights0, bool phys0)
        {
            OzRammedWreckRest.RestOn = rest0; OzRammedWreckRest.SteadyShove = shove0; OzRammedWreckRest.LightsOff = lights0; Phys = phys0;
            Time.captureFramerate = 0;
            md.AppendLine().AppendLine($"{pass} PASS, {fail} FAIL (the 'before' rows are the test exe's behaviour, not counted). {OzRammedWreckRest.Status()}");
            md.AppendLine(OzBigTankWreckShove.Status());
            Report = md.ToString();
            if (!string.IsNullOrEmpty(Folder)) File.WriteAllText(Path.Combine(Folder, "rammed_wreck_rest_report.md"), Report);
            Debug.Log(Tag + "\n" + Report);
            Destroy(this);
        }

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
