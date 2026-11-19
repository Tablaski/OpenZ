#if UNITY_EDITOR
// OpenZ (GPL-3.0)
// "NO piece hovering on any robot"): two mistakes of the round 11 measure fixed (see Gap), the A/B also switches OzRestSurface off.
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// Every robot kind killed by every death on flat ground, on a slope (the 18 degree ramp of a medium platform) and at a platform edge (its 4.5 m deck,
    /// beside the side rails), in the Unit editor (a level: flat ground only, no heroes / EvilZod). Kinds: grunt, tough, sniper, pyro, psycho, laser,
    /// electrobot (9 each), the heroes Alan and Brad (10), EvilZod (2). Deaths: shot x2 (the pop: round 10's pieces), headshot (ragdoll + the loose head),
    /// headshot with every ragdoll taken (the death clip + the loose head), a death clip (a bullet kill with every ragdoll taken: death_back / death_side),
    /// blown up (ragdoll / Zod's flight), blown up with every ragdoll taken (Zod's flight, the landing, death_back), burnt (the melt), run over (the
    /// pancake). "Every ragdoll taken" = RobotRagdoll.CheckFull for that one kill (what MaxActive falling bodies do in a battle).
    /// EVERY piece and body is followed every 0.1 s for 8 s: the pop pieces, the loose heads, the pancakes, EvilZod's pieces and the body while drawn.
    /// Measure: its lowest DRAWN point over the ground under it (skins baked as drawn now, every vertex; rigid meshes by OzDrawnPoints: their own
    /// vertices / the bake / box corners, the report says which) against OzWreckGround.Under (the walk layer, the ground, what stands still).
    /// AT REST = a pop piece lying (its own rest flag) and still, any other item still (lowest point moved under 1 cm for 0.3 s); a melt is never still:
    /// its samples after 1.2 s. PASS = every at-rest sample within +-0.02 m of the ground (ragdoll bodies and the no-ragdoll blast landing: none over
    /// +0.02 m; their sink is listed: a ragdoll rests on its physics capsules, the blast landing keeps its pose as before (root tipped 100 deg, death_back);
    /// this kit only takes their hover). Report + frames (every 1 s at the RTS camera): the kit's proof folder.
    /// A/B menu: OzBodyGround.On = false, grunts on flat ground: the clip deaths must FAIL (the check sees the old hover).
    /// </summary>
    public sealed class OzRobotPiecesCheckR11 : MonoBehaviour
    {
        public const string Tag = "[OpenZ 1.5 Robot Pieces r11]";
        public const float TolM = 0.02f, RagdollTolM = 0.02f, MeltTolM = 0.03f, TrackS = 8f, EveryS = 0.1f, StillM = 0.01f, FrameS = 1f, Night = 0.62f;
        public const int StillSamples = 3;
        public static bool Running { get; private set; }
        public static string Report { get; private set; } = "no run yet";
        static string folder = ""; static bool abOff;

        enum Way { Shot, Headshot, HeadshotClip, Clip, BlownUp, BlownUpClip, Flame, RunOver }
        static readonly Way[] Ways = { Way.Shot, Way.Shot, Way.Headshot, Way.HeadshotClip, Way.Clip, Way.BlownUp, Way.BlownUpClip, Way.Flame, Way.RunOver };
        static readonly Way[] HeroWays = { Way.Shot, Way.Headshot, Way.HeadshotClip, Way.Clip, Way.BlownUp, Way.BlownUpClip, Way.Flame, Way.RunOver, Way.Shot, Way.Clip };
        static readonly string[] Types = { "grunt", "tough", "sniper", "pyro", "psycho", "laser", "electrobot" };
        const string Heroes = "heroes";
        enum Ground { Flat, Slope, Edge }

        ZGame G; ZWorld W; RtsCamera rts; DayNight day;
        readonly StringBuilder md = new StringBuilder(), fails = new StringBuilder();
        int pass, fail, frames, batches, judgedAll; float speed0;
        RobotDeathRule.DeathMix mix0; float share0;

        public static void Begin(string dir, bool ab = false)
        {
            if (Running || !ZGame.I || ZGame.I.World == null) { Debug.LogWarning(Tag + " enter Play first (the Unit editor, or a level)"); return; }
            folder = dir; abOff = ab;
            ZGame.I.gameObject.AddComponent<OzRobotPiecesCheckR11>();
        }

        void Start() { Running = true; StartCoroutine(Run()); }

        void OnDestroy()
        {
            Running = false; Time.captureFramerate = 0; if (G) G.gameSpeed = speed0;
            RobotRagdoll.CheckFull = false; RobotDeathRule.Mix = mix0; RobotDeathRule.ShareBullet = share0; OzBodyGround.On = true; OzRestSurface.On = true;
        }

        IEnumerator Run()
        {
            G = ZGame.I; W = G.World; rts = FindFirstObjectByType<RtsCamera>(); day = FindFirstObjectByType<DayNight>();
            speed0 = G.gameSpeed; G.gameSpeed = 1f; mix0 = RobotDeathRule.Mix; share0 = RobotDeathRule.ShareBullet;
            if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
            bool ue = OzUnitEditor.Active;
            if (abOff) { OzBodyGround.On = false; OzRestSurface.On = false; }
            md.AppendLine($"# Robot pieces check r11b ({System.DateTime.Now:yyyy-MM-dd HH:mm}), {(ue ? "the Unit editor" : "a level")}, 60 fps frame-locked, x1, night{(abOff ? ", A/B: OzBodyGround + OzRestSurface OFF (grunts, flat ground; a FAIL on the clip deaths = the check sees the old hover)" : "")}").AppendLine();
            md.AppendLine("r11b measure: every skin vertex as drawn (BakeMesh with useScale = the renderer's own space, mapped by its localToWorldMatrix; round 11 mapped it "
                + $"without the renderer's scale: the melt's 0.12 height squash and the heroes' 1.296 model scale read wrong), every point binned in {CellM:0.00} m cells, of each cell the point lowest over the ground, "
                + "against the support right under it (round 11 kept the 256 lowest in WORLD height: on a slope or over an edge only the downhill / hanging end was measured).").AppendLine();
            md.AppendLine($"gap = the lowest DRAWN point over the ground right under it (OzWreckGround.Under). At rest = a pop piece lying (its rest flag) and still, any other item still "
                + $"(lowest point moved < {StillM * 100f:0} cm for {StillSamples * EveryS:0.0} s); a melt: its samples after 1.2 s. PASS = every at-rest sample within +-{TolM:0.00} m "
                + $"(ragdoll bodies: none over +{RagdollTolM:0.00} m, sink listed; r11b: the no-ragdoll blast landing (Airborne) lands upright in death_back's lying end and is judged as every clip body, +-{TolM:0.00} m; melt: +-{MeltTolM:0.00} m). {OzDrawnPoints.Status()}.").AppendLine();
            md.AppendLine("| batch | ground | type | team | way | victim | death drawn | items | at-rest samples | lowest gap at rest m | highest gap at rest m | worst item | failed items | result |");
            md.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|");
            if (ue) OzUnitEditor.SetPanelsHidden(true);
            Time.captureFramerate = 60;
            int n = 0;
            var grounds = abOff || !ue ? new[] { Ground.Flat } : new[] { Ground.Flat, Ground.Slope, Ground.Edge };
            foreach (var gk in grounds)
            {
                ZObject plat = null; ZWorld.PlatformInfo pi = null;
                if (gk != Ground.Flat)
                {
                    if (plat == null) { if (ue) OzUePlayground.ClearField(); yield return null; }
                    if (!PlatformArea(out int tx, out int ty)) { md.AppendLine($"| {n++} | {gk} | | | | | no free ground for a medium platform near the camera | | | | | | | FAIL |"); fail++; continue; }
                    plat = W.PlacePlatform(tx, ty, ZWorld.PlatformMid, 0, -1, out string why);
                    if (plat == null) { md.AppendLine($"| {n++} | {gk} | | | | | the platform could not be placed: {why} | | | | | | | FAIL |"); fail++; continue; }
                    OzUePlatformViews.Add(plat); OzUePlayground.MarkChanged(); Physics.SyncTransforms(); pi = W.PlatformOf(plat.Id);
                    yield return null;
                }
                var types = new List<string>(Types); if (abOff) { types.Clear(); types.Add("grunt"); }
                if (ue && !abOff) { types.Add(Heroes); types.Add(ZEvilZod.Type); }
                foreach (var t in types) { var e = Batch(t, gk, pi, n++); while (e.MoveNext()) yield return e.Current; }
                if (plat != null) { OzUePlayground.Remove(plat); yield return null; }
            }
            if (!ue) md.AppendLine("| - | slope, edge | heroes, EvilZod | | | | only in the Unit editor (its ramp platform; a hero's death may end a level) | | | | | | | - |");
            Time.captureFramerate = 0; G.gameSpeed = speed0;
            if (ue) { OzUePlayground.ClearField(); OzUnitEditor.SetPanelsHidden(false); }
            md.AppendLine().AppendLine($"{pass} PASS, {fail} FAIL victims; {batches} batches; {judgedAll} at-rest samples; {frames} frames.");
            md.AppendLine(OzBodyGround.Status() + "; " + OzRobotPieceRest.Status() + "; " + OzDrawnPoints.Status() + $"; pops {RobotPopDeath.Pops}, body parts thrown {RobotPopDeath.BodyPartsThrown}.");
            if (abOff) md.AppendLine($"A/B (OzBodyGround + OzRestSurface OFF): {fail} FAIL = {(fail > 0 ? "the check SEES the old clip-body hover (good)" : "the old hover was not reproduced (the check is not shown to catch it)")}.");
            if (fails.Length > 0) md.AppendLine().AppendLine("## Failed items").AppendLine().AppendLine("| batch | victim | item | lowest gap at rest m | highest gap at rest m | at s | lowest drawn point |").AppendLine("|---|---|---|---|---|---|---|").Append(fails);
            Report = md.ToString();
            if (!string.IsNullOrEmpty(folder)) File.WriteAllText(Path.Combine(folder, "robot_pieces_r11_report.md"), Report);
            Debug.Log(Tag + "\n" + Report);
            OzBodyGround.On = true; OzRestSurface.On = true;
            Destroy(this);
        }

        void Tod() { if (day) day.timeOfDay = Night; }
        static string F(float v) => float.IsNaN(v) ? "-" : v.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture);

        sealed class Victim { public ZObject o; public Way way; public Team team; public Vector3 at; public UnitView v; public string drawn = "-"; public int items, judged, failed; public float lo = float.NaN, hi = float.NaN; public string worst = "-"; }
        sealed class Track
        {
            public Transform t; public string what, type; public Victim of; public int kind;     // 0 pop piece, 1 loose head, 2 pancake, 3 EvilZod piece, 4 body
            public float lo = float.NaN, hi = float.NaN, at; public int judged, still; public Vector3 last, low; public bool hasLast; public float born;
        }

        IEnumerator Batch(string type, Ground gk, ZWorld.PlatformInfo pi, int no)
        {
            batches++;
            if (OzUnitEditor.Active) { if (pi != null) OzUePlayground.ClearUnits(); else OzUePlayground.ClearField(); yield return null; }
            if (type == ZEvilZod.Type && !ZWorld.EvilZodDeviation) { md.AppendLine($"| {no} | {gk} | {type} | | | | EvilZod is switched off in this game | | | | | | | SKIP |"); yield break; }
            var foe = G.player == Team.Red ? Team.Blue : Team.Red;
            Team team = type == Heroes ? Team.Red : type == ZEvilZod.Type ? Team.Blue : no % 2 == 0 ? foe : G.player;
            var spots = new List<Vector2Int>();
            if (pi == null)
            {
                if (!Area(out int ox, out int oy, 18, 14)) { md.AppendLine($"| {no} | {gk} | {type} | {team} | | | no free ground near the camera | | | | | | | FAIL |"); fail++; yield break; }
                for (int i = 0; i < 12; i++) spots.Add(new Vector2Int(ox + (2 + (i % 4) * 4) * 16, oy + (2 + (i / 4) * 4) * 16));
            }
            else
            {   // slope: the ramp (rows 9-16 from the back wall), across 2-8; edge: the deck (rows 1-9) along both side rails (rails = columns 0 and 9)
                var at = new List<(double a, double l)>();
                if (gk == Ground.Slope) { foreach (var l in new[] { 10.6, 12.6, 14.6, 11.6 }) foreach (var a in new[] { 2.2, 5.0, 7.8 }) at.Add((a, l)); }
                else { foreach (var l in new[] { 2.0, 3.6, 5.2, 6.8, 8.4, 2.8 }) { at.Add((1.25, l)); at.Add((8.75, l)); } }
                foreach (var (a, l) in at) { pi.Map(a, l, out double px, out double py); spots.Add(new Vector2Int((int)System.Math.Round(px), (int)System.Math.Round(py))); }
            }
            var vs = new List<Victim>();
            if (type == Heroes)
            {
                for (int s = 0, k = 0; s < 5 && k + 1 < spots.Count; s++, k += 2)
                {
                    var h = W.CreateHeroSquad(ZHeroSquads.AlanBrad, team, new[] { spots[k].x >> 4, spots[k].y >> 4 });
                    if (h == null) continue;
                    G.SpawnUnitView(h); vs.Add(new Victim { o = h, way = HeroWays[(s * 2) % HeroWays.Length], team = team });
                    foreach (var m in h.Minions) { if (m == null) continue; G.SpawnUnitView(m); vs.Add(new Victim { o = m, way = HeroWays[(s * 2 + 1) % HeroWays.Length], team = team }); }
                }
            }
            else
            {
                int count = type == ZEvilZod.Type ? 2 : Ways.Length;
                for (int i = 0; i < count && i < spots.Count; i++)
                {
                    ZObject o;
                    try { o = type == ZEvilZod.Type ? W.CreateRobotGroup(type, spots[i].x, spots[i].y, team, 1) : W.CreateObject("robot", type, spots[i].x, spots[i].y, team); }
                    catch (System.Exception ex) { md.AppendLine($"| {no} | {gk} | {type} | {team} | | | could not be made: {ex.Message} | | | | | | | SKIP |"); yield break; }
                    if (o == null) { md.AppendLine($"| {no} | {gk} | {type} | {team} | | | could not be made | | | | | | | SKIP |"); yield break; }
                    G.SpawnUnitView(o); vs.Add(new Victim { o = o, way = type == ZEvilZod.Type ? (i == 0 ? Way.Shot : Way.BlownUp) : Ways[i], team = team });
                }
            }
            if (OzUnitEditor.Active) OzUePlayground.MarkChanged();
            var mid = Vector3.zero; foreach (var s in spots) mid += ZGame.PxToWorld(s.x, s.y); mid /= Mathf.Max(1, spots.Count);
            if (rts) rts.FlyTo(mid);
            float until = Time.time + 1.2f; while (Time.time < until) { Tod(); yield return null; }
            foreach (var vi in vs) { G.Views.TryGetValue(vi.o.Id, out vi.v); vi.at = vi.v ? vi.v.transform.position : ZGame.PxToWorld(vi.o.CenterX, vi.o.CenterY); }

            var before = new HashSet<Transform>();
            RobotPopDeath.ListPieces(info); foreach (var p in info) before.Add(p.t);
            foreach (var a in FindObjectsByType<AnticCan>(FindObjectsSortMode.None)) before.Add(a.transform);
            foreach (var s in FindObjectsByType<SquashedRemains>(FindObjectsSortMode.None)) before.Add(s.transform);
            foreach (var b in FindObjectsByType<OzEvilZodBurst>(FindObjectsSortMode.None)) foreach (Transform c in b.transform) before.Add(c);

            float t0 = Time.time;
            foreach (var vi in vs)
            {
                if (vi.o.Destroyed) { vi.drawn = "died before the kill"; continue; }
                bool full = vi.way == Way.HeadshotClip || vi.way == Way.Clip || vi.way == Way.BlownUpClip;
                RobotRagdoll.CheckFull = full;
                if (vi.way == Way.Clip) { RobotDeathRule.Mix = RobotDeathRule.DeathMix.Shares; RobotDeathRule.ShareBullet = 0f; }   // a bullet kill that does not pop: the ragdoll, here taken: the death clip
                Kill(vi.o, vi.way);
                float w = Time.time + 0.6f;
                while (Time.time < w) { Tod(); yield return null; if (!vi.v) G.Views.TryGetValue(vi.o.Id, out vi.v); if (!vi.v || vi.v.Dying || !vi.v.gameObject.activeInHierarchy) break; }
                RobotRagdoll.CheckFull = false; RobotDeathRule.Mix = mix0; RobotDeathRule.ShareBullet = share0;
                w = Time.time + 0.1f; while (Time.time < w) { Tod(); yield return null; }
            }

            var tracks = new List<Track>(); var seen = new HashSet<Transform>(before);
            float nextSample = Time.time, nextFrame = Time.time, end = Time.time + TrackS;
            while (Time.time < end)
            {
                Tod(); yield return null;
                float now = Time.time;
                if (now >= nextFrame && !string.IsNullOrEmpty(folder)) { nextFrame += FrameS; ScreenCapture.CaptureScreenshot(Path.Combine(folder, $"{gk}_{type}_{(now - t0):00.0}s.png".ToLowerInvariant())); frames++; }
                if (now < nextSample) continue;
                nextSample += EveryS;
                RobotPopDeath.ListPieces(info);
                var rest = new HashSet<Transform>(); var popping = new HashSet<Transform>(); var wet = new HashSet<Transform>();
                foreach (var p in info)
                {
                    if (p.popping) popping.Add(p.t); if (p.wet) wet.Add(p.t); if (p.rest) rest.Add(p.t);
                    if (seen.Add(p.t)) { var of = Near(vs, p.t.position); tracks.Add(new Track { t = p.t, what = "pop piece " + MeshName(p.t), type = of != null ? of.o.Type : type, of = of, kind = 0, born = now }); }
                }
                foreach (var a in FindObjectsByType<AnticCan>(FindObjectsSortMode.None))
                    if (a.name.StartsWith("OZ_HeadshotHead_") && seen.Add(a.transform)) { var of = Near(vs, a.transform.position); tracks.Add(new Track { t = a.transform, what = "loose head", type = of != null ? of.o.Type : type, of = of, kind = 1, born = now }); }
                foreach (var s in FindObjectsByType<SquashedRemains>(FindObjectsSortMode.None))
                    if (seen.Add(s.transform)) { var of = Near(vs, s.transform.position); tracks.Add(new Track { t = s.transform, what = "pancake", type = of != null ? of.o.Type : type, of = of, kind = 2, born = now }); }
                foreach (var b in FindObjectsByType<OzEvilZodBurst>(FindObjectsSortMode.None))
                    foreach (Transform c in b.transform) if (seen.Add(c)) tracks.Add(new Track { t = c, what = "EvilZod piece " + c.name, type = ZEvilZod.Type, of = Near(vs, c.position), kind = 3, born = now });
                foreach (var vi in vs)
                {
                    if (!vi.v) G.Views.TryGetValue(vi.o.Id, out vi.v);
                    if (!vi.v || !vi.v.Dying) continue;
                    if (vi.drawn == "-") vi.drawn = vi.v.Popped ? "pop" : DeathName(vi.v);
                    if (!vi.v.Popped && vi.v.gameObject.activeInHierarchy && seen.Add(vi.v.transform)) tracks.Add(new Track { t = vi.v.transform, what = "body (" + vi.drawn + ")", type = vi.o.Type, of = vi, kind = 4, born = now });
                }
                foreach (var tr in tracks)
                {
                    if (!tr.t || !tr.t.gameObject.activeInHierarchy || popping.Contains(tr.t) || wet.Contains(tr.t) || PopOut.IsPopping(tr.t) || OzWaterLanding.InWater(tr.t)) { tr.hasLast = false; tr.still = 0; continue; }
                    float gap = Gap(tr, out var low);
                    if (float.IsNaN(gap)) { tr.hasLast = false; tr.still = 0; continue; }
                    bool still = tr.hasLast && (low - tr.last).magnitude < StillM;
                    tr.last = low; tr.hasLast = true; tr.still = still ? tr.still + 1 : 0;
                    string death = tr.kind == 4 && tr.of != null && tr.of.v ? DeathName(tr.of.v) : "";
                    bool atRest = death == "Melt" ? now - t0 >= 1.2f : tr.still >= StillSamples && (tr.kind != 0 || rest.Contains(tr.t)) && (tr.kind != 2 || now - tr.born >= 0.7f);
                    if (!atRest) continue;
                    tr.judged++; judgedAll++;
                    if (float.IsNaN(tr.lo) || gap < tr.lo) { tr.lo = gap; tr.low = low; }
                    if (float.IsNaN(tr.hi) || gap > tr.hi) { tr.hi = gap; tr.at = now - t0; if (float.IsNaN(tr.lo) || gap <= tr.lo) tr.low = low; }
                }
            }
            foreach (var tr in tracks)
            {
                var vi = tr.of; if (vi == null) continue;
                vi.items++; vi.judged += tr.judged;
                if (tr.judged == 0) continue;
                string death = tr.kind == 4 ? tr.what : "";
                bool ragdoll = death.Contains("Ragdoll") || (death.Contains("Airborne") && !OzRestSurface.On), melt = death.Contains("Melt");   // r11b: the Airborne landing lies upright in death_back's end and is grounded as every clip body (round 11 / OzRestSurface off: its root tipped 100 deg, hover judged, sink listed)
                float tol = melt ? MeltTolM : TolM;
                bool bad = tr.hi > (ragdoll ? RagdollTolM : tol) || (!ragdoll && tr.lo < -tol);
                if (float.IsNaN(vi.lo) || tr.lo < vi.lo) vi.lo = tr.lo;
                if (float.IsNaN(vi.hi) || tr.hi > vi.hi) { vi.hi = tr.hi; vi.worst = tr.what; }
                if (bad)
                {
                    vi.failed++;
                    fails.AppendLine($"| {no} {gk} {type} | {vi.o.Type} #{vi.o.Id} ({vi.way}) | {tr.what} | {F(tr.lo)} | {F(tr.hi)} | {F(tr.at)} | ({tr.low.x:0.0}, {tr.low.y:0.00}, {tr.low.z:0.0}) |");
                }
            }
            foreach (var vi in vs)
            {
                bool ok = vi.failed == 0 && (vi.judged > 0 || vi.drawn == "died before the kill");
                md.AppendLine($"| {no} | {gk} | {vi.o.Type} | {vi.team} | {vi.way} | #{vi.o.Id} | {vi.drawn} | {vi.items} | {vi.judged} | {F(vi.lo)} | {F(vi.hi)} | {vi.worst} | {vi.failed} | {(ok ? "PASS" : vi.items == 0 ? "FAIL (nothing seen)" : vi.judged == 0 ? "FAIL (nothing came to rest)" : "FAIL")} |");
                if (ok) pass++; else fail++;
            }
        }

        readonly List<RobotPopDeath.PieceInfo> info = new List<RobotPopDeath.PieceInfo>();
        readonly List<Vector3> pts = new List<Vector3>(4096), vb = new List<Vector3>(8192);
        readonly List<Renderer> rs = new List<Renderer>(32);

        /// <summary>r11b: the cell size of the measure (m).</summary>
        public const float CellM = 0.2f;
        static Mesh bakeAll;
        readonly Dictionary<long, int> cellLow = new Dictionary<long, int>(256);
        readonly Dictionary<long, float> cellD = new Dictionary<long, float>(256);

        /// <summary>The lowest drawn point of the item over the support right under it. Points: skins as drawn now (BakeMesh(useScale true) = the
        /// renderer's own space, so its localToWorldMatrix gives the world; every vertex), a mesh baked into a pool (the pancake's copies) every vertex,
        /// any other mesh by OzDrawnPoints. Measure: the points binned in CellM cells, of each cell the point lowest over GroundY right under it, that
        /// point against OzWreckGround.Under right under it (one ray per cell), the smallest gap. Round 11's two mistakes (fixed here, r11b): (1) skins were mapped by TRS(position, rotation, 1),
        /// which drops the renderer's own scale: the melt (root y scale down to 0.12) and the heroes (model scale 1.296) read sunk or up by tens of cm;
        /// (2) only the 256 lowest vertices in WORLD height were kept: on a slope or over an edge that is the downhill / hanging end alone, so a body or
        /// pancake touching the ramp with its uphill side read 0.1-1.8 m up.</summary>
        float Gap(Track tr, out Vector3 low)
        {
            low = Vector3.zero; pts.Clear(); rs.Clear(); tr.t.GetComponentsInChildren(false, rs);
            foreach (var r in rs)
            {
                if (!OzDrawnPoints.Solid(r)) continue;
                if (r is SkinnedMeshRenderer sk)
                {
                    if (!sk.sharedMesh) continue;
                    if (!bakeAll) bakeAll = new Mesh { name = "OzRobotPiecesCheckR11 bake" };
                    sk.BakeMesh(bakeAll, true); vb.Clear(); bakeAll.GetVertices(vb);
                    var st = sk.transform.localToWorldMatrix;
                    for (int i = 0; i < vb.Count; i++) pts.Add(st.MultiplyPoint3x4(vb[i]));
                    continue;
                }
                if (!r.TryGetComponent<MeshFilter>(out var mf) || !mf.sharedMesh) continue;
                var m = mf.sharedMesh; var mt = r.transform.localToWorldMatrix;
                if (m.isReadable && m.name.StartsWith("squash_")) { vb.Clear(); m.GetVertices(vb); for (int i = 0; i < vb.Count; i++) pts.Add(mt.MultiplyPoint3x4(vb[i])); continue; }
                var p = OzDrawnPoints.Rigid(m); if (p == null) continue;
                for (int i = 0; i < p.Length; i++) pts.Add(mt.MultiplyPoint3x4(p[i]));
            }
            if (pts.Count == 0) return float.NaN;
            cellLow.Clear(); cellD.Clear();
            for (int i = 0; i < pts.Count; i++)
            {
                var p = pts[i]; float e = p.y - G.GroundY(p.x, p.z);                                      // its height over the ground right under it (on a slope the lowest in world height is not the lowest over it)
                long key = ((long)Mathf.FloorToInt(p.x / CellM) << 32) ^ (uint)Mathf.FloorToInt(p.z / CellM);
                if (cellLow.TryGetValue(key, out int j)) { if (e < cellD[key]) { cellLow[key] = i; cellD[key] = e; } }
                else { cellLow[key] = i; cellD[key] = e; }
            }
            float gap = float.MaxValue;
            foreach (var kv in cellLow)
            {
                var p = pts[kv.Value];
                float s = OzWreckGround.Under(p, tr.t); if (s <= float.MinValue) continue;
                float d = p.y - s; if (d < gap) { gap = d; low = p; }
            }
            return gap == float.MaxValue ? float.NaN : gap;
        }

        static Victim Near(List<Victim> vs, Vector3 p)
        {
            Victim best = null; float bd = float.MaxValue;
            foreach (var v in vs) { var d = p - v.at; d.y = 0f; if (d.sqrMagnitude < bd) { bd = d.sqrMagnitude; best = v; } }
            return best;
        }

        static string MeshName(Transform t) { var mf = t.GetComponentInChildren<MeshFilter>(); return mf && mf.sharedMesh ? mf.sharedMesh.name : t.name; }

        static string DeathName(UnitView v)
        {
            var f = typeof(UnitView).GetField("death", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return f != null ? f.GetValue(v).ToString() : "dying";
        }

        /// <summary>The world's own death of a robot, flagged the way it is to be drawn (the death event reads the flags).</summary>
        void Kill(ZObject o, Way way)
        {
            switch (way)
            {
                case Way.BlownUp: case Way.BlownUpClip: o.LastMissileHit = W.Time; break;
                case Way.Flame: o.LastFireHit = W.Time; break;
                case Way.RunOver: o.Squashed = true; break;
                case Way.Headshot: case Way.HeadshotClip:
                    var pr = typeof(ZObject).GetProperty("HeadshotBy");
                    var set = pr != null ? pr.GetSetMethod(true) : null;
                    if (set != null) set.Invoke(o, new object[] { o.Id });
                    break;
            }
            o.SetHealth(0); W.UpdateObjectHealth(o, -1);
        }

        // ------------------------------------------------------------------ free ground / a platform spot near the camera (as the round 10 check / OzBigTankPitchFramesCheck)
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

        bool PlatformArea(out int tx, out int ty)
        {
            tx = ty = 0;
            ZWorld.PlatformSize(ZWorld.PlatformMid, 0, out int wide, out int deep);
            var f = rts ? rts.focus : Vector3.zero;
            int cx = Mathf.RoundToInt(f.x * 8f) >> 4, cy = Mathf.RoundToInt(-f.z * 8f) >> 4;
            for (int r = 0; r < 50; r += 2)
                for (int dy = -r; dy <= r; dy += 2)
                    for (int dx = -r; dx <= r; dx += 2)
                    {
                        int x = cx + dx - wide / 2, y = cy + dy - deep / 2;
                        if (!Free(x - 5, y - 6, wide + 10, deep + 15)) continue;
                        if (!W.PlatformSpotFree(x, y, wide, deep, out _)) continue;
                        tx = x; ty = y; return true;
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
