#if UNITY_EDITOR && !OZ_RELEASE
// OpenZ (GPL-3.0) - version 1.5 kit chips_white: the Play check (editor only, never in a build).
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// mode, from the menu OpenZ/V1.5/Chips (Editor/OzV15ChipsMenu.cs). It stages a light tank, a medium tank, a heavy tank and a mech (red) and a
    /// blue light tank as the shooter 8 tiles away, finds the point a shell from the shooter meets each hull (HitSurface.OnBody, the game's IM1
    /// line, aimed at the hull's side 1 m up, never at the hatch) and puts one heavy burst there through the game's own HC1 path
    /// (HeavyChips.OnBlast, as CombatView calls it). Rows per hull:
    /// <list type="bullet">
    /// <item>1 from the hit point: every chip starts within 0.2 m of the burst, none nearer the hatch hinge than the burst is, and leaves along the
    /// surface's outward normal (velocity . normal &gt; 0, normal towards the shooter).</item>
    /// <item>2 proper texture: the chips carry the struck part's own material (not the white piece material), each with its own texture window
    /// (_BaseMap_ST in the slot-0 block), still there 4 frames later (after OzGlowFade.Dim ran); else "no readable part": the gunmetal / team
    /// colour of the piece material (counted as a FAIL only when no hull at all gives a textured chip).</item>
    /// <item>3 sizes: the largest chip at least 2.5 x the smallest's width, every chip at least 0.28 x as thick as wide.</item>
    /// </list>
    /// Row 4: the piece-material path (HeavyChips' buildings, a hull with no readable part): a chip's colour is still the asked colour 4 frames
    /// after the throw (the white fault: the slot-0 block of OzGlowFade.Dim overruled the renderer-wide colour).
    /// Two pictures per run (the chips in the air, on the ground), the camera close over the row. Everything made is removed at the end.
    /// </summary>
    [DefaultExecutionOrder(32000)]
    public sealed class OzChipsCheck : MonoBehaviour
    {
        const string Tag = "[OpenZ V1.5 chips play]";
        public static string Report = "not run", Folder;
        public static bool Running;
        static readonly string[] Types = { "light", ZMTank.Type, "heavy", ZMTank.Mech };
        static readonly int StId = Shader.PropertyToID("_BaseMap_ST"), BaseColorId = Shader.PropertyToID("_BaseColor");

        ZGame g; ZWorld W; Team me, them;
        readonly StringBuilder sb = new StringBuilder(); int pass, total;
        readonly List<ZObject> made = new List<ZObject>();

        public static void Begin()
        {
            if (Running) { Debug.LogWarning(Tag + " already running"); return; }
            new GameObject("OZ_ChipsCheck").AddComponent<OzChipsCheck>();
        }

        void Log(string what, bool ok, string detail) { total++; if (ok) pass++; sb.AppendLine($"{(ok ? "PASS" : "FAIL")}  {what}: {detail}"); }
        void Note(string s) => sb.AppendLine("      " + s);
        static IEnumerator Real(float s) { yield return new WaitForSecondsRealtime(s); }
        static string F(Vector3 v) => $"({v.x:0.00}, {v.y:0.00}, {v.z:0.00})";

        IEnumerator Start()
        {
            Running = true; Report = "running";
            float speed = 1f; var rts = FindFirstObjectByType<RtsCamera>(); float h0 = rts ? rts.height : 0f;
            try
            {
                g = ZGame.I ? ZGame.I : FindFirstObjectByType<ZGame>(); W = g ? g.World : null;
                if (!g || W == null) { Report = Tag + " FAIL: no level playing"; Debug.LogWarning(Report); yield break; }
                me = OpenZHud.I ? OpenZHud.I.Player : g.player; them = me == Team.Red ? Team.Blue : Team.Red;
                speed = g.gameSpeed; g.gameSpeed = 1f;
                if (!string.IsNullOrEmpty(Folder)) Directory.CreateDirectory(Folder);
                yield return Main(rts);
            }
            finally
            {
                if (g) g.gameSpeed = speed;
                if (rts && h0 > 0f) rts.SetHeightImmediate(h0);
                foreach (var o in made) if (o != null && !o.Destroyed && o.KillAt < 0) { o.SetHealth(0); W.UpdateObjectHealth(o); }
                Report = $"{Tag}: {(pass == total && total > 0 ? "PASS" : "FAIL")} {pass}/{total}\n" + sb;
                if (!string.IsNullOrEmpty(Folder)) { try { File.WriteAllText(Path.Combine(Folder, "play_check_report.txt"), Report); } catch { } }
                Debug.Log(Report);
                Running = false; Destroy(gameObject);
            }
        }

        ZObject Unit(string type, Team team, int tx, int ty)
        {
            var o = W.CreateObject("vehicle", type, tx * 16, ty * 16, team);
            if (o == null) return null;
            g.SpawnUnitView(o); made.Add(o); o.Damage = 0;                                                       // the check's units never fight
            return o;
        }

        bool Free(int x, int y)
        {
            for (int j = 0; j < 4; j++) for (int i = 0; i < 4; i++)
            {
                int tx = x + i, ty = y + j;
                if (tx < 3 || ty < 3 || tx >= W.Path.W - 3 || ty >= W.Path.H - 3 || !W.Path.TilePassable(tx, ty, false) || W.LavaAtPx(tx * 16 + 8, ty * 16 + 8)) return false;
            }
            return !W.Objects.Any(o => !o.Destroyed && o.KillAt < 0 && o.Kind != ZKind.MapItem && o.LocX < (x + 4) * 16 && o.LocX + o.WidthPix > x * 16 && o.LocY < (y + 4) * 16 && o.LocY + o.HeightPix > y * 16);
        }

        bool Spot(out int x0, out int y0)
        {
            var cam = Camera.main; var c = Vector3.zero;
            if (cam) g.GroundPoint(cam.ScreenPointToRay(new Vector3(UnityEngine.Screen.width * 0.5f, UnityEngine.Screen.height * 0.5f, 0f)), out c);
            var f = ZGame.WorldToPx(c); int fx = f.x >> 4, fy = f.y >> 4;
            for (int r = 0; r <= 40; r++)
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;
                        int x = fx + dx, y = fy + dy; bool ok = Free(x + 9, y + 9);                              // the shooter's stand, then the four hulls in a row
                        for (int k = 0; ok && k < 4; k++) ok = Free(x + 5 * k, y);
                        if (ok) { x0 = x; y0 = y; return true; }
                    }
            x0 = fx; y0 = fy; return false;
        }

        IEnumerator Picture(string file)
        {
            if (string.IsNullOrEmpty(Folder)) yield break;
            yield return new WaitForEndOfFrame();
            Texture2D tex = null;
            try { tex = ScreenCapture.CaptureScreenshotAsTexture(); File.WriteAllBytes(Path.Combine(Folder, file), tex.EncodeToPNG()); Note("picture " + file); }
            catch (System.Exception ex) { Note("picture " + file + " failed: " + ex.Message); }
            finally { if (tex) Destroy(tex); }
        }

        IEnumerator Main(RtsCamera rts)
        {
            if (!Spot(out int x0, out int y0)) { Log("0 free ground near the camera", false, "no free row of four 4 x 4-tile stands and a shooter's stand near the camera: move the camera to open ground and run again"); yield break; }
            if (rts) { rts.FlyTo(ZGame.PxToWorld((x0 + 9) * 16, (y0 + 5) * 16)); rts.SetHeightImmediate(Mathf.Max(rts.minHeight, 26f)); }
            var hulls = new List<ZObject>();
            for (int k = 0; k < Types.Length; k++) hulls.Add(Unit(Types[k], me, x0 + 1 + 5 * k, y0 + 1));
            var shooter = Unit("light", them, x0 + 10, y0 + 10);
            yield return Real(2f);
            if (shooter == null || !g.Views.TryGetValue(shooter.Id, out var sv) || !sv) { Log("0 staged", false, "the shooter could not be made"); yield break; }
            Log("0 staged", hulls.All(o => o != null && g.Views.ContainsKey(o.Id)), $"tiles ({x0}, {y0}); " + string.Join(", ", hulls.Select((o, i) => $"{Types[i]} #{(o != null ? o.Id : -1)}")) + $"; shooter blue light tank #{shooter.Id}");
            int textured = 0;
            bool pic = false;
            for (int k = 0; k < hulls.Count; k++)
            {
                var o = hulls[k]; if (o == null || !g.Views.TryGetValue(o.Id, out var v) || !v) continue;
                var bb = new Bounds(v.transform.position, Vector3.zero); bool any = false;
                foreach (var r in v.GetComponentsInChildren<MeshRenderer>()) { if (!r.enabled) continue; if (!any) { bb = r.bounds; any = true; } else bb.Encapsulate(r.bounds); }
                var from = sv.transform.position + Vector3.up * 1.2f;
                var aim = new Vector3(bb.center.x, bb.min.y + 1f, bb.center.z);                                // the hull's side, 1 m up (a tank shell's line, never the hatch)
                if (!HitSurface.OnBody(v.transform, from, aim, out var at, out _)) at = aim;
                Transform hinge = null; foreach (var x in v.GetComponentsInChildren<Transform>(true)) if (x.name.EndsWith("_HatchHinge")) { hinge = x; break; }
                var e = new ZEvent { type = ZEventType.MissileExploded, objId = shooter.Id, otherId = o.Id, x = o.CenterX, y = o.CenterY, radius = 8 };
                int n0 = RobotPopDeath.HullChipsThrown;
                HeavyChips.OnBlast(g, e, shooter, o, at);
                var chips = new List<RobotPopDeath.HullChipNote>(RobotPopDeath.LastHullChips);
                var nrm = RobotPopDeath.LastHullNormal; string part = RobotPopDeath.LastHullPart;
                if (RobotPopDeath.HullChipsThrown == n0 || chips.Count == 0) { Log($"{Types[k]} #{o.Id}: chips thrown", false, "HeavyChips.OnBlast threw no chip (the burst point was not on the body?) at " + F(at)); continue; }
                if (!pic) { pic = true; yield return null; yield return null; yield return null; StartCoroutine(Picture("chips_in_air.png")); }
                // 1 from the hit point, along the surface normal
                float far = chips.Max(c => (c.start - at).magnitude);
                bool toShooter = Vector3.Dot(nrm, from - at) > 0f, outward = chips.All(c => Vector3.Dot(c.v, nrm) > 0f);
                bool notHatch = !hinge || chips.All(c => (c.start - hinge.position).magnitude >= (at - hinge.position).magnitude - 0.25f);
                Log($"1 {Types[k]} #{o.Id}: the chips leave the hit point along the surface", far <= 0.2f && toShooter && outward && notHatch,
                    $"burst {F(at)} on '{(part.Length > 0 ? part : "(no readable part)")}', normal {F(nrm)} (towards the shooter {toShooter}); farthest start {far:0.00} m (at most 0.2); all outward {outward}; "
                    + (hinge ? $"hatch hinge {(at - hinge.position).magnitude:0.00} m from the burst, none started nearer it: {notHatch}" : "no hatch on this hull"));
                // 2 proper texture, kept after the glow fade's first frames
                yield return null; yield return null; yield return null; yield return null;
                var mpb = new MaterialPropertyBlock(); int keep = 0, tex = 0, white = 0;
                foreach (var c in chips)
                {
                    if (!c.r) continue;
                    c.r.GetPropertyBlock(mpb, 0);
                    if (c.textured) { tex++; if (mpb.GetVector(StId) == c.st) keep++; }
                    else if (mpb.GetColor(BaseColorId).maxColorComponent > 0.9f || c.r.sharedMaterial.name == "OZ_PopPiece" && mpb.isEmpty) white++;
                }
                if (tex > 0) textured++;
                Log($"2 {Types[k]} #{o.Id}: proper texture (the struck part's own material, its own window of it, kept after the glow fade)",
                    tex > 0 ? keep == tex : white == 0,
                    tex > 0 ? $"{tex} of {chips.Count} chips in '{chips[0].r.sharedMaterial.name}', texture window kept 4 frames on {keep} of {tex}; window {chips[0].st}"
                            : $"no readable part at the hit point: {chips.Count} gunmetal / team chips, {white} white");
                // 3 sizes
                float wMax = chips.Max(c => c.size.x), wMin = chips.Min(c => c.size.x), thin = chips.Min(c => c.size.y / c.size.x);
                Log($"3 {Types[k]} #{o.Id}: varied, chunky sizes", wMax >= 2.5f * wMin && thin >= 0.28f,
                    $"{chips.Count} chips, widths {string.Join(" / ", chips.Select(c => c.size.x.ToString("0.00")))} m (largest / smallest {wMax / Mathf.Max(wMin, 1e-3f):0.0}, at least 2.5); thinnest {thin:0.00} x its width (at least 0.28)");
                yield return Real(0.4f);
            }
            Log("2b at least one hull gives textured chips (readable parts at the hit point)", textured > 0, $"{textured} of {hulls.Count} hulls");
            // 4 the piece-material path keeps its colour (the white fault)
            var spot = ZGame.PxToWorld((x0 + 3) * 16, (y0 + 6) * 16); spot.y = g.GroundY(spot.x, spot.z) + 1f;
            var want = new Color(0.62f, 0.58f, 0.52f);
            int c0 = RobotPopDeath.ChipsThrown;
            RobotPopDeath.Chips(spot, Vector3.right, want, 3, 1.2f, 4242u, true);
            int thrown = RobotPopDeath.ChipsThrown - c0;
            yield return null; yield return null; yield return null; yield return null;
            int right = 0, seen = 0; var b4 = new MaterialPropertyBlock();
            var pd = FindFirstObjectByType<RobotPopDeath>();
            if (pd)
                foreach (var r in pd.GetComponentsInChildren<MeshRenderer>())
                {
                    if (r.sharedMaterial == null || r.sharedMaterial.name != "OZ_PopPiece" || (r.transform.position - spot).magnitude > 6f) continue;
                    r.GetPropertyBlock(b4, 0); var c = b4.GetColor(BaseColorId);
                    if (c.a <= 0f) continue;
                    seen++; if (Mathf.Abs(c.r - want.r) < 0.25f && Mathf.Abs(c.g - want.g) < 0.25f && Mathf.Abs(c.b - want.b) < 0.25f) right++;
                }
            Log("4 the piece-material chips keep their colour after the glow fade (no white)", thrown > 0 && seen > 0 && right == seen, $"{thrown} thrown, {seen} read 4 frames later in slot 0, {right} in the asked colour");
            yield return Real(2.5f);
            int r1 = RobotPopDeath.ChipReport(out int flat1, out float low1, out _), e0 = RobotPopDeath.ChipsRecycledEarly;   // 1.5 kit chips_ground_rest
            Log("5 the landed chips lie flat ON the surface (their thickness above it)", r1 > 0 && flat1 == r1 && low1 >= -0.01f,
                $"{r1} chips lying, {flat1} flat on the ground's slope; lowest bottom {low1:0.000} m against GroundY (at least -0.01)");
            yield return Picture("chips_landed.png");
            yield return Real(4f);
            int r2 = RobotPopDeath.ChipReport(out int flat2, out float low2, out float long2);
            Log("6 they stay (8-12 s once down, none taken back early by the pool)", r2 >= r1 && flat2 == r2 && long2 >= 6f && RobotPopDeath.ChipsRecycledEarly == e0,
                $"{r2} chips still lying 4 s later (were {r1}), {flat2} flat; the longest down {long2:0.0} s (at least 6); taken back early {RobotPopDeath.ChipsRecycledEarly - e0}; lowest bottom {low2:0.000} m");
            yield return Picture("chips_down_6s.png");
        }
    }
}
#endif
