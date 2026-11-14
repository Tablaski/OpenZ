#if UNITY_EDITOR && !OZ_RELEASE
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace OpenZ
{
    /// <summary>
    /// 1.4 kit u2_followups: the Play check of the kit's items, on a level playing (L02 at night for STAR + BLOB; level 16 for SHUTTLE), every
    /// row read at the END of frames (what is drawn) or from the rules' own events; no row passes on 0 samples. Menus: OpenZ/V1.4/U2 Followups:
    /// Play Check (L02 night) / Shuttle Row (L16) / last report (Editor/OzV14U2FollowupsMenu.cs); report in Folder. The level's own enemies are
    /// quieted while it runs (their Damage 0, put back at the end); the check's own units are taken out after each row.
    ///  STAR   our light tank selected alone; a normal promotion with the HUD on is the reference (the portrait star's gold pixels on the screen
    ///         round the portrait, read back from the screen, minus the same frame without the star; its last built mesh's size from a mesh
    ///         probe on VetStar: OzStarMeshProbe). Then for Alt+F and for the F10 hide key: the HUD hidden, the tank promoted, 1.6 s, the HUD
    ///         shown again: the same gold pixels (within 35 % or 40 px) and the same mesh size (within 3 u), 5 cycles each. Pass A (once per
    ///         path, VeteranMarks.PortraitMeshFix off = P before this kit): REPRODUCED / NOT REPRODUCED. Round 2 (review): both STAR rows
    ///         PASS only when pass A REPRODUCED the owner's star on at least one path; otherwise they FAIL as NOT PROVEN.
    ///  BLOB   night (L02: the time of day is set to night when it is not): three of our light tanks parked side by side, lit; Alt+F in the
    ///         middle one, the game camera's Stop NaN OFF for this row only (put back after): 3 pictures of the camera's HDR target read back
    ///         (post-processing off for that read, its value put back) in the view, then Esc, 3 more at 0.1 / 0.8 / 2 s: non-finite pixels = 0
    ///         in every one; the screen after the leave (post on): the biggest white-hot patch at most 2 % of the screen. The first non-finite
    ///         pixel, if any, is named with the world point under it (depth) and the nearest unit: the lead for the source.
    ///  FORT   an enemy fort's gun laid and COMMITTED on our light tank (put inside its reach and envelope), EXIT DRIVER: shells the fort starts
    ///         at the tank after the EXIT's tick in 7 s = 0 (its MissileFired events).
    ///  EJECT  our gatling gun shot by an enemy jeep and grunt squad (their Damage 0), EJECT: shots started at the gun after its tick in 6 s = 0,
    ///         nobody left with it as target or an attack order on it.
    ///  SHUTTLE (level 16, its own menu) ALAN &amp; BRAD take an empty shuttle; an enemy jeep shoots it; EXIT: everyone out, the ship neutral;
    ///         the jeep's shots started at the empty ship in 6 s = 0.
    ///  DEATH  (round 2) our MLRS fires on an enemy light tank; enemy grunts (30 HP) inside its splash are knocked down so that they are in
    ///         their blast arc when the shot kills them (the knockdown started 0.15 s of sim time before it explodes), up to 3 salvos until one
    ///         of them is held for a ripple rocket: deaths in the arc >= 1, each thrown (DeathWinsLog), held ones >= 1; thrown before
    ///         eventAt + wave = 0 (the owner's NB3 rule: never before his rocket); thrown in the event's own frame while held = 0; frames a body
    ///         dead in the rules and not yet thrown is drawn above its ground (> HangM) once his arc's own time is over = 0 (samples > 0: no
    ///         hanging); a reaction and a death on one body = 0; a very tough enemy grunt in the same splash still wobbles.
    ///  DEATH-GRIP (round 2) enemy grunts knocked down, then killed by a flame in the rules (the melt: the Animator stays on), half in the
    ///         arc, half lying: dying frames with the Animator on > 0; the grip flag on = 0; the left fist on the gun's forward grip (3 frames
    ///         running under LeftFistM) = 0; a living grunt beside them is the calibration (his fist on it, else the row fails).
    /// </summary>
    public class OzU2FollowupPlayCheck : MonoBehaviour
    {
        public const string Tag = "[OpenZ V1.4 u2_followups]";
        public static bool Running; public static string Report = "not run";
        public static string Folder;
        public static int StarCycles = 5; public static float StarWaitSec = 1.6f, StarTolerance = 0.35f, StarMinPx = 40f, StarMeshTolU = 3f, BlobLimit = 0.02f, HangM = 0.15f, HangSlackSec = 0.05f, LeftFistM = 0.06f;
        public enum Rows { Main, Shuttle }

        Rows rows; ZGame g; ZWorld W; PlayerInput input; OpenZHud hud; RtsCamera rts; Camera cam; Team me, foe;
        readonly StringBuilder sb = new StringBuilder(); int pass, total;
        readonly List<ZObject> made = new List<ZObject>();
        readonly Dictionary<ZObject, int> quieted = new Dictionary<ZObject, int>();
        readonly HashSet<long> used = new HashSet<long>();

        public static void Begin(Rows r)
        {
            if (Running) { Debug.LogWarning(Tag + " already running"); return; }
            var c = new GameObject("OZ_U2FollowupPlayCheck").AddComponent<OzU2FollowupPlayCheck>(); c.rows = r;
        }

        IEnumerator Start()
        {
            Running = true; Report = "running";
            bool fix0 = VeteranMarks.PortraitMeshFix;
            try
            {
                g = ZGame.I ? ZGame.I : FindFirstObjectByType<ZGame>(); W = g ? g.World : null; input = FindFirstObjectByType<PlayerInput>(); hud = OpenZHud.I;
                rts = FindFirstObjectByType<RtsCamera>(); cam = rts ? rts.GetComponent<Camera>() : Camera.main;
                if (!g || W == null || !input || !hud || !cam) { Report = Tag + " FAIL: no level playing"; Debug.LogWarning(Report); yield break; }
                me = hud.Player; foe = me == Team.Red ? Team.Blue : Team.Red;
                if (!string.IsNullOrEmpty(Folder)) Directory.CreateDirectory(Folder);
                sb.AppendLine($"level {g.level}, DV176 {ZWorld.EmptiedTargetsDeviation}, DV171 {ZWorld.VehicleExitDeviation}, night {NightGrade.NightK:0.00}, death owns: held arcs land {UnitView.DeathHoldLands}, star fix {VeteranMarks.PortraitMeshFix}");
                StartCoroutine(Peace());
                if (rows == Rows.Shuttle) yield return ShuttleRow();
                else
                {
                    yield return StarRows();
                    yield return BlobRow();
                    yield return FortRow();
                    yield return EjectRow();
                    yield return DeathRow();
                    yield return DeathGripRow();
                }
            }
            finally
            {
                VeteranMarks.PortraitMeshFix = fix0;
                if (OzFpsView.On && OzFpsView.I) OzFpsView.I.Leave("u2 check end", false, true);
                if (hud && !hud.hudVisible) hud.SetVisible(true);
                foreach (var kv in quieted) if (kv.Key != null) kv.Key.Damage = kv.Value;
                ClearMade();
                Report = $"{Tag} {(pass == total && total > 0 ? "PASS" : "FAIL")} {pass}/{total}\n" + sb;
                if (!string.IsNullOrEmpty(Folder)) { try { File.WriteAllText(Path.Combine(Folder, rows == Rows.Shuttle ? "u2_followups_shuttle_report.txt" : "u2_followups_play_report.txt"), Report); } catch { } }
                Debug.Log(Report);
                Running = false; Destroy(gameObject);
            }
        }

        void Log(string what, bool ok, string detail) { total++; if (ok) pass++; sb.AppendLine($"{(ok ? "PASS" : "FAIL")}  {what}: {detail}"); }
        void Note(string s) => sb.AppendLine("      " + s);
        static IEnumerator Real(float s) { yield return new WaitForSecondsRealtime(s); }
        static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }

        IEnumerator Peace()
        {
            while (Running && W != null)
            {
                foreach (var o in W.Objects) if (o.Owner != me && o.Owner != Team.Neutral && !o.Destroyed && o.Damage > 0 && !made.Contains(o) && !quieted.ContainsKey(o)) { quieted[o] = o.Damage; o.Damage = 0; }
                yield return new WaitForSecondsRealtime(0.5f);
            }
        }

        // ------------------------------------------------------------------ units and places (as View/OzVehicleExitGunCheck.cs)
        ZObject Make(string cat, string type, Team t, int x, int y, int amount = -1)
        {
            var o = cat == "robot" ? W.CreateRobotGroup(type, x, y, t, amount) : W.CreateObject(cat, type, x, y, t);
            g.SpawnUnitView(o); foreach (var m in o.Minions) g.SpawnUnitView(m);
            made.Add(o); made.AddRange(o.Minions);
            return o;
        }
        static void Tough(ZObject o) { o.MaxHealth = o.Health = 1000000000; foreach (var m in o.Minions) m.MaxHealth = m.Health = 1000000000; }
        UnitView ViewOf(ZObject o) => o != null && g.Views.TryGetValue(o.Id, out var v) ? v : null;
        void Select(ZObject o) { input.Selection.Clear(); if (o != null) input.Selection.Add(o); }

        IEnumerator Spot(int w, int h, int[] at, bool clear = true)
        {
            at[0] = at[1] = -1;
            if (clear) ClearMade();
            var c = ZGame.WorldToPx(rts ? rts.focus : Vector3.zero); int fx = c.x >> 4, fy = c.y >> 4;
            for (int r = 0; r < 120 && at[0] < 0; r++)
                for (int dx = -r; dx <= r && at[0] < 0; dx++)
                    for (int dy = -r; dy <= r && at[0] < 0; dy++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;
                        int bx = fx + dx, by = fy + dy;                                       // U3 check fix 3: every tile (was every 2nd: L02's few free 12x8 places sit on odd tiles)
                        if (used.Contains(((long)bx << 32) ^ (uint)by) || !Free(bx, by, w, h)) continue;
                        at[0] = bx; at[1] = by;
                    }
            if (at[0] < 0) yield break;
            for (int x = -2; x < w + 2; x++) for (int y = -2; y < h + 2; y++) used.Add(((long)(at[0] + x) << 32) ^ (uint)(at[1] + y));
            if (rts) { rts.FlyTo(ZGame.PxToWorld((at[0] + w / 2) * 16, (at[1] + h / 2) * 16)); yield return Real(1.2f); }
        }
        void ClearMade()
        {
            if (W == null || made.Count == 0) return;
            var sel = input ? input.Selection : null;
            foreach (var o in made.ToList())
            {
                if (o == null) continue;
                var ids = new List<int> { o.Id }; foreach (var m in o.Minions) if (m != null) ids.Add(m.Id);
                if (sel != null) sel.RemoveAll(s => s != null && ids.Contains(s.Id));
                if (!o.Destroyed && W.Get(o.Id) != null) W.RemoveNow(o);
                foreach (var id in ids) if (g.Views.TryGetValue(id, out var v)) { g.Views.Remove(id); if (v) { v.gameObject.SetActive(false); Destroy(v.gameObject); } }
            }
            made.Clear(); used.Clear();
        }
        bool Free(int bx, int by, int w, int h)
        {
            for (int x = 0; x < w; x++) for (int y = 0; y < h; y++) if (!W.Path.TilePassable(bx + x, by + y, true)) return false;
            int x0 = (bx - 3) * 16, y0 = (by - 3) * 16, x1 = (bx + w + 3) * 16, y1 = (by + h + 3) * 16;
            foreach (var o in W.Objects) if (!o.Destroyed && o.CenterX >= x0 && o.CenterX <= x1 && o.CenterY >= y0 && o.CenterY <= y1) return false;
            return true;
        }
        static bool ShotAt(ZEvent e, ZObject v) => e.otherId == v.Id && (e.type == ZEventType.Hitscan || e.type == ZEventType.MissileFired);
        void CpuAttack(ZObject u, ZObject t) => W.SendWaypoints(u.Owner, u, new[] { new ZWaypoint { mode = WpMode.Attack, refId = t.Id, attackTo = true, playerGiven = true, x = t.LocX + 8, y = t.LocY + 8 } });
        int Holders(ZObject v) { int n = 0; foreach (var o in W.Objects) if (o != v && !o.Destroyed && (o.AttackObject == v || o.Waypoints.Any(q => (q.mode == WpMode.Attack || q.mode == WpMode.Agro) && q.refId == v.Id))) n++; return n; }

        static Texture2D Shot() => ScreenCapture.CaptureScreenshotAsTexture();

        // ------------------------------------------------------------------ STAR: the portrait star after a hidden HUD
        /// <summary>Records the size of the mesh VetStar's Image really built (its vertices, local units) and how often it was built.</summary>
        public class OzStarMeshProbe : BaseMeshEffect
        {
            public int builds; public Vector2 size; public bool finite = true;
            public override void ModifyMesh(VertexHelper vh)
            {
                builds++; if (vh.currentVertCount == 0) { size = Vector2.zero; return; }
                var v = new UIVertex(); Vector2 lo = new Vector2(float.MaxValue, float.MaxValue), hi = new Vector2(float.MinValue, float.MinValue); finite = true;
                for (int i = 0; i < vh.currentVertCount; i++)
                {
                    vh.PopulateUIVertex(ref v, i); var p = v.position;
                    if (float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsInfinity(p.x) || float.IsInfinity(p.y)) { finite = false; continue; }
                    lo = Vector2.Min(lo, p); hi = Vector2.Max(hi, p);
                }
                size = finite ? hi - lo : new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            }
        }

        RectTransform Portrait() { var up = hud ? hud.UnitPanelRect : null; var p = up ? up.Find("Portrait") : null; return p ? (RectTransform)p : up; }
        OzStarMeshProbe Probe() { var p = Portrait(); var s = p ? p.Find("VetStar") : null; if (!s) return null; var pr = s.GetComponent<OzStarMeshProbe>(); return pr ? pr : s.gameObject.AddComponent<OzStarMeshProbe>(); }

        /// <summary>The screen box round the portrait (overlay canvas: its world corners are screen pixels), 4x its size each way, clamped.</summary>
        RectInt Around()
        {
            var p = Portrait(); if (!p) return new RectInt(0, 0, Screen.width, Screen.height);
            var c = new Vector3[4]; p.GetWorldCorners(c);
            float w = c[2].x - c[0].x, h = c[2].y - c[0].y, m = Mathf.Max(w, h) * 1.5f;   // U3 check fix 2: 1.5x (was 4x: a 1404x812 box took in the night world's lights; the owner's 13x star still falls inside)
            int x0 = Mathf.Clamp((int)(c[0].x - m), 0, Screen.width - 1), y0 = Mathf.Clamp((int)(c[0].y - m), 0, Screen.height - 1);
            int x1 = Mathf.Clamp((int)(c[2].x + m), 1, Screen.width), y1 = Mathf.Clamp((int)(c[2].y + m), 1, Screen.height);
            return new RectInt(x0, y0, x1 - x0, y1 - y0);
        }
        static bool Gold(Color32 c) => c.r > 200 && c.g > 100 && c.b < 175 && c.r - c.b > 80;
        /// <summary>Gold pixels in the box and their bounding box's width.</summary>
        static int GoldIn(Texture2D t, RectInt r, out int width, Color32[] before, out Color32[] px)
        {
            width = 0; px = null; if (!t) return -1;
            px = t.GetPixels32(); int n = 0, lo = int.MaxValue, hi = int.MinValue; bool diff = before != null && before.Length == px.Length;
            for (int y = r.yMin; y < Mathf.Min(r.yMax, t.height); y++)
                for (int x = r.xMin; x < Mathf.Min(r.xMax, t.width); x++)
                {
                    int i = y * t.width + x;
                    if (Gold(px[i]) && !(diff && Gold(before[i]))) { n++; lo = Mathf.Min(lo, x); hi = Mathf.Max(hi, x); }
                }
            width = n > 0 ? hi - lo + 1 : 0;
            return n;
        }
        // U3 check fix 1: the 'after' picture counts only the pixels that are gold now and were not gold in the picture before (+ the before count,
        // so after - before = the new gold); its width is that new gold's. Count minus count read the day's sand and moving units (gold -6626).
        Color32[] goldBase; int goldBaseN, goldPic;
        IEnumerator GoldNow(RectInt r, int[] res, bool after = false)
        {
            yield return new WaitForEndOfFrame(); var t = Shot();
            if (!after) { res[0] = GoldIn(t, r, out res[1], null, out goldBase); goldBaseN = res[0]; }
            else { res[0] = goldBaseN + GoldIn(t, r, out res[1], goldBase, out _); }
            if (!string.IsNullOrEmpty(Folder) && t)
            {
                try
                {
                    var crop = new Texture2D(r.width, r.height, TextureFormat.RGB24, false);
                    crop.SetPixels(t.GetPixels(r.xMin, r.yMin, Mathf.Min(r.width, t.width - r.xMin), Mathf.Min(r.height, t.height - r.yMin)));
                    crop.Apply(false); Directory.CreateDirectory(Path.Combine(Folder, "star_u3"));
                    File.WriteAllBytes(Path.Combine(Folder, "star_u3", $"star_{++goldPic:000}_{(after ? "after" : "before")}_fix{(VeteranMarks.PortraitMeshFix ? 1 : 0)}.png"), crop.EncodeToPNG()); Destroy(crop);
                }
                catch (System.Exception e) { Note("star picture not kept: " + e.Message); }
            }
            Destroy(t);
        }

        void Unpromote(ZObject u) { u.Veteran = false; u.PromotedAt = -1; }

        IEnumerator StarRows()
        {
            string row = "STAR the portrait star after the HUD was hidden (Alt+F, F10) during the promotion";
            { var dn = FindFirstObjectByType<DayNight>(); if (NightGrade.NightK < 0.6f && dn) { dn.timeOfDay = 0.62f; yield return Real(2.5f); } Note($"STAR at night {NightGrade.NightK:0.00} (U3 check fix 1: the owner's frame is at night; the day's sand reads as gold)"); }
            var at = new int[2]; yield return Spot(6, 6, at);
            if (at[0] < 0) { Log(row, false, "no free ground near the camera"); yield break; }
            var u = Make("vehicle", "light", me, (at[0] + 2) * 16, (at[1] + 2) * 16); Tough(u);
            Unpromote(u); Select(u); yield return Real(0.6f);
            var box = Around(); var r0 = new int[2]; var r1 = new int[2];
            // the reference: a normal promotion with the HUD on
            yield return GoldNow(box, r0);
            u.Veteran = true; u.PromotedAt = W.Time; yield return Real(StarWaitSec);
            var probe = Probe();
            if (!probe) { Log(row, false, "no VetStar under the unit panel's portrait after a promotion (the HUD shows another unit?)"); yield break; }
            probe.enabled = false; probe.enabled = true; yield return Frames(3);                               // one rebuild at the rest scale: the probe's reference size
            yield return GoldNow(box, r1, true);
            int refGold = r1[0] - r0[0], refW = r1[1]; var refMesh = probe.size;
            Note($"STAR reference (HUD on): gold pixels {refGold} (box {box.width}x{box.height} px round the portrait, the star's box {refW} px wide), mesh {refMesh.x:0.0} x {refMesh.y:0.0} u");
            if (refGold <= 0 || refMesh.x <= 0f) { Log(row, false, $"no reference star drawn (gold {refGold}, mesh {refMesh})"); yield break; }
            var results = new List<(string path, bool good, string detail)>(); var passAs = new List<string>(); int reproduced = 0;
            foreach (var path in new[] { "Alt+F", "F10" })
            {
                var bad = new List<string>(); int ok = 0, tried = 0; string passAWas = "NOT RUN";
                for (int pa = 0; pa < 1 + StarCycles; pa++)
                {
                    bool passA = pa == 0; VeteranMarks.PortraitMeshFix = !passA;
                    Unpromote(u); Select(u); yield return Frames(4);
                    yield return GoldNow(box, r0);
                    int b0 = probe.builds;
                    if (path == "Alt+F") { if (!OzFpsView.TryEnter(input)) { bad.Add($"cycle {pa}: Alt+F refused ({OzFpsView.LastRefusal})"); if (passA) passAWas = $"NOT RUN (Alt+F refused: {OzFpsView.LastRefusal})"; continue; } }
                    else hud.SetVisible(false);
                    yield return Frames(3);
                    bool hidden = !(hud.Canvas && hud.Canvas.enabled);
                    u.Veteran = true; u.PromotedAt = W.Time;
                    yield return Real(StarWaitSec);
                    if (path == "Alt+F") { if (OzFpsView.I) OzFpsView.I.Leave("u2 check star"); }
                    else hud.SetVisible(true);
                    yield return Frames(4);
                    yield return GoldNow(box, r1, true);
                    int gold = r1[0] - r0[0]; var mesh = probe.size; int builds = probe.builds - b0;
                    bool same = hidden && probe.finite && Mathf.Abs(gold - refGold) <= Mathf.Max(StarTolerance * refGold, StarMinPx) && Mathf.Abs(mesh.x - refMesh.x) <= StarMeshTolU && Mathf.Abs(mesh.y - refMesh.y) <= StarMeshTolU;
                    string d = $"gold {gold} (box {r1[1]} px wide), mesh {mesh.x:0.0} x {mesh.y:0.0} u{(probe.finite ? "" : " NON-FINITE")}, meshes built {builds}, HUD hidden {hidden}";
                    if (passA)
                    {   // round 2 (review): P before the kit must show the owner's fault here (the HUD really hidden, the star back wrong), or no cycle below proves the fix
                        passAWas = !hidden ? "NOT RUN (the HUD was not hidden)" : same ? "NOT REPRODUCED" : "REPRODUCED";
                        if (hidden && !same) reproduced++;
                        passAs.Add($"STAR {path} pass A (the fix off = P before this kit): {passAWas}: {d}");
                        continue;
                    }
                    tried++; if (same) ok++; else bad.Add($"cycle {pa}: {d}");
                }
                VeteranMarks.PortraitMeshFix = true;
                results.Add((path, tried == StarCycles && ok == tried, $"pass A {passAWas}; {ok}/{tried} cycles with the fix like the reference (gold {refGold} +-{Mathf.Max(StarTolerance * refGold, StarMinPx):0}, mesh +-{StarMeshTolU} u){(bad.Count > 0 ? "; " + string.Join("; ", bad) : "")}; rebuilds {VeteranMarks.PortraitRebuilds}, on a show {VeteranMarks.PortraitShowRebuilds}"));
            }
            foreach (var s in passAs) Note(s);
            bool proven = reproduced > 0;                                                                     // round 2 (review): PASS only when pass A reproduced the owner's star on at least one path
            foreach (var r in results)
                Log($"{row}: {r.path}", proven && r.good, (proven ? "" : "NOT PROVEN: pass A (P before this kit) did not reproduce the owner's star on either path in this editor, so these cycles cannot show that the fix removes it (FAIL until it is reproduced); ") + r.detail);
            Unpromote(u); Select(null);
        }

        // ------------------------------------------------------------------ BLOB: no non-finite pixel in the view or after it
        sealed class Hdr { public int nonFinite; public string first = ""; }

        /// <summary>The game camera's picture rendered once more into an HDR target with post-processing off (its value put back) and read back:
        /// NaN / infinite pixels counted; the first one named with the world point under it.</summary>
        Hdr ReadHdr(string when)
        {
            var res = new Hdr();
            var data = cam.GetUniversalAdditionalCameraData(); bool post0 = data.renderPostProcessing;
            var rt = new RenderTexture(cam.pixelWidth, cam.pixelHeight, 24, RenderTextureFormat.ARGBHalf) { name = "OZ_U2_hdr" };
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBAHalf, false);
            try
            {
                data.renderPostProcessing = false;
                var req = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
                if (RenderPipeline.SupportsRenderRequest(cam, req)) RenderPipeline.SubmitRenderRequest(cam, req);
                else { var t0 = cam.targetTexture; cam.targetTexture = rt; cam.Render(); cam.targetTexture = t0; }
                var a0 = RenderTexture.active; RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0, false); tex.Apply(false); RenderTexture.active = a0;
                var px = tex.GetPixels();
                for (int i = 0; i < px.Length; i++)
                {
                    var c = px[i];
                    if (!(float.IsNaN(c.r) || float.IsNaN(c.g) || float.IsNaN(c.b) || float.IsInfinity(c.r) || float.IsInfinity(c.g) || float.IsInfinity(c.b))) continue;
                    if (res.nonFinite++ > 0) continue;
                    int x = i % rt.width, y = i / rt.width;
                    var ray = cam.ScreenPointToRay(new Vector3(x + 0.5f, y + 0.5f, 0f));
                    string hit = Physics.Raycast(ray, out var rh, 2000f) ? $"collider {rh.collider.name} at {rh.point}" : "no collider";
                    ZObject near = null; float nd = float.MaxValue;
                    foreach (var kv in g.Views) { if (!kv.Value) continue; var sp = cam.WorldToScreenPoint(kv.Value.transform.position); if (sp.z <= 0f) continue; float d = (new Vector2(sp.x, sp.y) - new Vector2(x, y)).sqrMagnitude; if (d < nd) { nd = d; near = kv.Value.Obj; } }
                    res.first = $"{when}: pixel ({x},{y}) = {c}; {hit}; nearest unit on screen {(near != null ? near.Type + " #" + near.Id + " " + near.Owner : "-")} ({Mathf.Sqrt(nd):0} px)";
                }
            }
            finally { data.renderPostProcessing = post0; rt.Release(); Destroy(rt); Destroy(tex); }
            return res;
        }

        static float BiggestWhite(Texture2D t)
        {
            if (!t) return -1f;
            int cw = t.width / 8, ch = t.height / 8; var px = t.GetPixels32(); var hot = new bool[cw * ch];
            for (int y = 0; y < ch; y++) for (int x = 0; x < cw; x++)
                {
                    int n = 0, h = 0;
                    for (int j = 1; j < 8; j += 3) for (int i = 1; i < 8; i += 3) { var c = px[(y * 8 + j) * t.width + x * 8 + i]; n++; if (c.r > 234 && c.g > 234 && c.b > 225) h++; }
                    hot[y * cw + x] = h * 2 > n;
                }
            var seen = new bool[hot.Length]; var st = new Stack<int>(); int best = 0;
            for (int s = 0; s < hot.Length; s++)
            {
                if (!hot[s] || seen[s]) continue;
                int size = 0; st.Push(s); seen[s] = true;
                while (st.Count > 0)
                {
                    int k = st.Pop(); size++; int x = k % cw, y = k / cw;
                    if (x > 0 && hot[k - 1] && !seen[k - 1]) { seen[k - 1] = true; st.Push(k - 1); }
                    if (x < cw - 1 && hot[k + 1] && !seen[k + 1]) { seen[k + 1] = true; st.Push(k + 1); }
                    if (y > 0 && hot[k - cw] && !seen[k - cw]) { seen[k - cw] = true; st.Push(k - cw); }
                    if (y < ch - 1 && hot[k + cw] && !seen[k + cw]) { seen[k + cw] = true; st.Push(k + cw); }
                }
                best = Mathf.Max(best, size);
            }
            return best / (float)Mathf.Max(1, hot.Length);
        }

        IEnumerator BlobRow()
        {
            string row = "BLOB night, Alt+F beside 3 lit tanks with Stop NaN off: no non-finite pixel in the camera's picture, no white blob after the leave";
            var dn = FindFirstObjectByType<DayNight>(); float tod0 = dn ? dn.timeOfDay : -1f;
            if (NightGrade.NightK < 0.6f && dn) { dn.timeOfDay = 0.62f; yield return Real(2.5f); }
            if (NightGrade.NightK < 0.6f) { Log(row, false, $"not night (night {NightGrade.NightK:0.00}) and the time of day could not be set"); yield break; }
            var at = new int[2]; yield return Spot(12, 8, at);   // U3 check fix 1: L02 has no free 14x8 place at all (12x8: 6); the tanks at +2 / +6 / +10 fit
            if (at[0] < 0) { Log(row, false, "no free ground near the camera"); yield break; }
            var tanks = new List<ZObject>();
            for (int k = 0; k < 3; k++) { var t = Make("vehicle", "light", me, (at[0] + 2 + 4 * k) * 16, (at[1] + 3) * 16); Tough(t); tanks.Add(t); }
            yield return Real(1.5f);                                                                         // the headlights come on (0.35 s) and the lamps are picked
            yield return new WaitForEndOfFrame(); var b = Shot(); float base0 = BiggestWhite(b); Destroy(b);
            var data = cam.GetUniversalAdditionalCameraData(); bool nan0 = data.stopNaN;
            Select(tanks[1]); yield return Frames(2);
            int pics = 0, bad = 0; string first = ""; float worst = 0f; int shots = 0;
            try
            {
                if (!OzFpsView.TryEnter(input)) { Log(row, false, "Alt+F refused: " + OzFpsView.LastRefusal); yield break; }
                yield return Frames(2);
                data.stopNaN = false;                                                                        // this row only (OzFpsGlow set it on at the entry)
                var fv = OzFpsView.I;
                for (int k = 0; k < 3; k++)
                {
                    yield return Real(0.35f);
                    var h = ReadHdr($"in the view, picture {k + 1}"); pics++; bad += h.nonFinite; if (first == "" && h.first != "") first = h.first;
                }
                if (fv) fv.Leave("u2 check blob", true);
                foreach (float wait in new[] { 0.1f, 0.7f, 1.2f })
                {
                    yield return Real(wait);
                    var h = ReadHdr($"after the leave +{wait:0.0} s"); pics++; bad += h.nonFinite; if (first == "" && h.first != "") first = h.first;
                    yield return new WaitForEndOfFrame(); var s = Shot(); float w = BiggestWhite(s); Destroy(s); shots++; worst = Mathf.Max(worst, w);
                }
            }
            finally { data.stopNaN = nan0; if (dn && tod0 >= 0f) dn.timeOfDay = tod0; }
            Log(row, pics >= 6 && shots >= 3 && bad == 0 && worst <= BlobLimit,
                $"HDR pictures read back {pics}, non-finite pixels {bad} (want 0){(first != "" ? "; first: " + first : "")}; screens after the leave {shots}, biggest white-hot patch {worst * 100f:0.00} % (limit {BlobLimit * 100f:0} %; before the entry {base0 * 100f:0.00} %); Stop NaN back to {nan0}");
            Select(null);
        }

        // ------------------------------------------------------------------ FORT / EJECT / SHUTTLE: nobody shoots the emptied one
        IEnumerator FortRow()
        {
            string row = "FORT an enemy fort's gun committed on our light tank, EXIT DRIVER: 0 shells started by it at the empty tank";
            ClearMade();
            ZObject fort = W.Objects.FirstOrDefault(o => o.IsFort && o.Owner == foe && !o.Destroyed && !o.FortGunDestroyed);
            if (fort == null) { Log(row, false, "no enemy fort with its gun on this level"); yield break; }
            if (!ZWorld.FortGunDeviation) { Log(row, false, "the fort gun (DV7) is off in this session"); yield break; }
            var u = W.S.Unit("howitzer"); int range = ZObject.FortGunRange(u);
            int px = -1, py = -1;
            for (int d = 96; d < range - 24 && px < 0; d += 8)
                for (int a = 0; a < 24 && px < 0; a++)
                {
                    float ang = a * 15f * Mathf.Deg2Rad;
                    int x = fort.CenterX + (int)(Mathf.Cos(ang) * d), y = fort.CenterY + (int)(Mathf.Sin(ang) * d);
                    if (!fort.FortGunCanReach(x, y) || !Free((x >> 4) - 1, (y >> 4) - 1, 2, 2)) continue;
                    px = x; py = y;
                }
            if (px < 0) { Log(row, false, "no free ground inside the fort gun's reach and envelope"); yield break; }
            if (rts) { rts.FlyTo(ZGame.PxToWorld(px, py)); yield return Real(1.2f); }
            var v = Make("vehicle", "light", me, px - 16, py - 16); Tough(v); v.Damage = 0;
            float t0 = Time.time;
            while (Time.time - t0 < 45f && !(fort.FortGunTarget == v && fort.FortGunCommitted && W.Time < fort.FortGunReadyAt - 0.2)) yield return null;
            if (!(fort.FortGunTarget == v && fort.FortGunCommitted)) { Log(row, false, $"the fort gun never committed on the tank in 45 s (its target {(fort.FortGunTarget != null ? fort.FortGunTarget.Type : "none")})"); yield break; }
            double due = fort.FortGunReadyAt - W.Time;
            yield return new WaitForFixedUpdate();
            if (!input.EjectUnit(v)) { Log(row, false, "EXIT DRIVER refused: " + (v.CanExitDriver(out string why) ? "?" : why)); yield break; }
            long exitTick = W.TickIndex, last = W.TickIndex; int after = 0; t0 = Time.time;
            while (Time.time - t0 < 7f)
            {
                yield return null;
                if (W.TickIndex == last) continue; last = W.TickIndex;
                foreach (var e in W.Events) if (e.type == ZEventType.MissileFired && e.objId == fort.Id && e.otherId == v.Id && W.TickIndex > exitTick) after++;
            }
            Log(row, after == 0, $"committed, its shot due in {due:0.00} s at the EXIT (tick {exitTick}); shells started by the fort at the empty tank in 7 s: {after} (want 0); its target now {(fort.FortGunTarget != null ? fort.FortGunTarget.Type + " #" + fort.FortGunTarget.Id : "none")}");
        }

        IEnumerator EjectRow()
        {
            string row = "EJECT our gun shot by an enemy jeep and grunt squad, EJECT: 0 shots started at the empty gun";
            var at = new int[2]; yield return Spot(16, 10, at);
            if (at[0] < 0) { Log(row, false, "no free ground near the camera"); yield break; }
            ZObject gun = null;
            foreach (var t in new[] { "gatling", "gun", "missile_cannon", "howitzer" })
            {
                var c = Make("cannon", t, me, (at[0] + 2) * 16, (at[1] + 4) * 16);
                if (c.CanEjectDrivers() && c.Drivers.Count > 0) { gun = c; break; }
                W.RemoveNow(c);
            }
            if (gun == null) { Log(row, false, "no ejectable crewed gun"); yield break; }
            Tough(gun); gun.Damage = 0;
            var jeep = Make("vehicle", "jeep", foe, (at[0] + 12) * 16, (at[1] + 4) * 16); Tough(jeep); jeep.Damage = 1;                        // U3 check fix 4: Damage 1, not 0 (ZObject.cs: a unit with Damage 0 never fires: 'shots at it before 0')
            var squad = Make("robot", "grunt", foe, (at[0] + 10) * 16, (at[1] + 8) * 16, 3); Tough(squad); foreach (var m in OzV11KitLite.Squad(squad)) m.Damage = 1;
            CpuAttack(jeep, gun); CpuAttack(squad, gun);
            var foes = new HashSet<int>(new[] { jeep.Id }.Concat(OzV11KitLite.Squad(squad).Select(o => o.Id)));
            yield return EmptyWatch(row, gun, foes, () => input.EjectUnit(gun) || W.Eject(me, gun), 6f);
        }

        /// <summary>Until both kinds of attacker fired (or 40 s), the emptying action, then the shots started at it after its tick.</summary>
        IEnumerator EmptyWatch(string row, ZObject v, HashSet<int> foes, System.Func<bool> empty, float sec)
        {
            long last = -1; int before = 0, after = 0; bool emptied = false; long tick = -1; float t0 = Time.time, tEmpty = -1f; var afterWhy = new StringBuilder();
            while (Time.time - t0 < 60f)
            {
                yield return null;
                if (W.TickIndex == last) continue; last = W.TickIndex;
                foreach (var e in W.Events)
                {
                    if (!ShotAt(e, v) || !foes.Contains(e.objId)) continue;
                    if (emptied && tick >= 0 && W.TickIndex > tick) { after++;   // U3 check fix 6: 'after' = after the tick the vehicle turned neutral (a shuttle EXIT turns it neutral only when the heroes are down its ramp: tick -1 before)
                                                                                                          var s0 = W.Get(e.objId); if (afterWhy.Length < 600) afterWhy.Append($" [tick {W.TickIndex} (+{W.TickIndex - tick}) {e.type} by #{e.objId} {s0?.Type}: its target {(s0?.AttackObject != null ? "#" + s0.AttackObject.Id : "none")}, waypoints {s0?.Waypoints.Count}, the ship {v.Owner}]"); }
                    else before++;
                }   // U3 check fix 5: every shot after the emptying named (tick, attacker, its target and orders)
                if (!emptied && before >= 2) { emptied = empty(); if (!emptied) break; }
                if (emptied && tick < 0 && v.Owner == Team.Neutral) { tick = W.TickIndex; tEmpty = Time.time; }
                if (tEmpty >= 0f && Time.time - tEmpty >= sec) break;
            }
            int holders = tick >= 0 ? Holders(v) : -1;
            Log(row, tick >= 0 && before > 0 && after == 0 && holders == 0, $"shots at it before {before}; emptied {(tick >= 0 ? "at tick " + tick : "NO")}; shots STARTED at it after that in {(tEmpty >= 0f ? Time.time - tEmpty : 0f):0.0} s: {after} (want 0){afterWhy}; units still on it {holders}");
        }

        IEnumerator ShuttleRow()
        {
            string row = "SHUTTLE ALAN & BRAD's shuttle shot by an enemy jeep, EXIT (everyone out, the ship neutral): 0 shots started at the empty ship";
            var brad = W.Objects.FirstOrDefault(o => o.IsHero && o.HeroMemberName == "brad" && !o.Destroyed && o.KillAt < 0 && o.Owner == me);
            if (brad == null) { Log(row, false, "no ALAN & BRAD of ours on this level (run it on level 16)"); yield break; }
            ZObject ship = null;
            for (int d = 200; d <= 700 && ship == null; d += 32)
                for (int a = 0; a < 16 && ship == null; a++)
                {
                    float ang = a * Mathf.PI / 8f;
                    ship = W.SpawnShuttle(brad.CenterX + (int)(Mathf.Cos(ang) * d), brad.CenterY + (int)(Mathf.Sin(ang) * d), Team.Neutral, -90f, out _);
                }
            if (ship == null) { Log(row, false, "no empty shuttle could be dropped near the heroes"); yield break; }
            g.SpawnUnitView(ship); made.Add(ship);
            if (rts) { rts.FlyTo(g.CenterWorld(ship)); yield return Real(1.2f); }
            W.SendWaypoints(me, brad, new[] { new ZWaypoint { mode = WpMode.Enter, refId = ship.Id, x = ship.CenterX, y = ship.CenterY } });
            float t0 = Time.time;
            while (Time.time - t0 < 60f && !(ship.ShuttleHeroPilot && ship.ShuttleRampPhase == ZShuttleRamp.Shut && !ship.ShuttleExitPending && W.Time >= ship.ShuttleRampHoldUntil)) yield return null;
            if (!ship.ShuttleHeroPilot || ship.Owner != me) { Log(row, false, $"the heroes did not take the ship in 60 s (hero pilot {ship.ShuttleHeroPilot}, team {ship.Owner})"); yield break; }
            Tough(ship);
            var jeep = Make("vehicle", "jeep", foe, ship.CenterX + 120 - 16, ship.CenterY + 60 - 16); Tough(jeep); jeep.Damage = 1;   // U3 check fix 4: Damage 1, not 0 (a unit with Damage 0 never fires)
            CpuAttack(jeep, ship);
            yield return EmptyWatch(row, ship, new HashSet<int> { jeep.Id }, () => W.Eject(me, ship), 6f);
        }

        // ------------------------------------------------------------------ DEATH: the death owns the dying body
        /// <summary>Round 2 (review): the owner's NB3 rule stays (BlastWave.cs: robots fly on the impact frame, never before). A knockdown in its
        /// blast arc at his death event keeps his hold, finishes the arc to the ground during it and is thrown when his rocket lands. The row
        /// reads, for every grunt dead in the rules and not yet thrown, his height once his arc's own time is over (at most HangM), and for every
        /// one in a knockdown's arc at his death event his throw against eventAt + wave (vehicle_exit_gun's DeathWinsLog: never before).</summary>
        IEnumerator DeathRow()
        {
            string row = "DEATH a rocket kills robots in a knockdown's arc: held for his rocket, he lands from the arc (no hanging) and is thrown when it lands (never before), a survivor still wobbles";
            int flyers = 0, flyersThrown = 0, heldFlyers = 0, before = 0, sameFrame = 0, hangFrames = 0, heldSamples = 0, both = 0, survivorFrames = 0, salvos = 0;
            float survivorMoved = 0f, maxUp = 0f, minLate = float.MaxValue;
            int af0 = UnitView.DeathHoldArcFrames, al0 = UnitView.DeathHoldLanded;
            var notes = new List<string>(); var lines = new List<string>();
            for (int tryN = 0; tryN < 3 && heldFlyers == 0; tryN++)
            {
                var at = new int[2]; yield return Spot(26, 14, at);
                if (at[0] < 0) { notes.Add("no free ground near the camera"); break; }
                var mlrs = Make("vehicle", "missile_launcher", me, (at[0] + 1) * 16, (at[1] + 6) * 16); Tough(mlrs);
                var target = Make("vehicle", "light", foe, (at[0] + 10) * 16, (at[1] + 6) * 16); Tough(target); target.Damage = 0;
                CpuAttack(mlrs, target);
                ZMissile shot = null; float t0 = Time.time;
                while (shot == null && Time.time - t0 < 12f)
                {
                    yield return null;
                    foreach (var m in W.PendingMissiles) if (m.attackerId == mlrs.Id && !m.debris && !m.grenade) shot = m;
                    foreach (var m in W.QueuedMissiles) if (m.attackerId == mlrs.Id && !m.debris && !m.grenade) shot = m;
                }
                if (shot == null) { notes.Add("the MLRS did not fire within 12 s"); continue; }
                salvos++;
                var dies = new List<ZObject>();
                for (int k = 0; k < 16 && dies.Count < 6; k++)
                {
                    float ang = k * 22.5f * Mathf.Deg2Rad, rr = (k & 1) == 0 ? 30f : Mathf.Max(40f, shot.radius - 16f);
                    if (rr > shot.radius - 10) continue;
                    int cx = shot.x + (int)(Mathf.Cos(ang) * rr), cy = shot.y + (int)(Mathf.Sin(ang) * rr);
                    if (!W.Path.TilePassable(cx >> 4, cy >> 4, true)) continue;
                    if (W.Objects.Any(o => o.IsUnit && !o.Destroyed && Mathf.Abs(o.CenterX - cx) < 18 && Mathf.Abs(o.CenterY - cy) < 18)) continue;
                    var r = W.CreateObject("robot", "grunt", cx - 8, cy - 8, foe); g.SpawnUnitView(r); made.Add(r);
                    r.Damage = 0; r.SetHealth(30); dies.Add(r);
                }
                ZObject live = null;
                {   // the survivor: in the same splash, far too tough to die
                    int cx = shot.x + 24, cy = shot.y - 24;
                    if (W.Path.TilePassable(cx >> 4, cy >> 4, true)) { live = W.CreateObject("robot", "grunt", cx - 8, cy - 8, foe); g.SpawnUnitView(live); made.Add(live); live.Damage = 0; Tough(live); }
                }
                if (dies.Count == 0) { notes.Add($"no open spot inside the MLRS shot's splash ({shot.radius} px)"); continue; }
                yield return Frames(3);
                var blast = ZGame.PxToWorld(shot.x, shot.y);
                var ground = new Dictionary<int, float>();
                foreach (var o in dies.Concat(live != null ? new[] { live } : new ZObject[0])) { var uv = ViewOf(o); if (uv) ground[o.Id] = uv.transform.position.y - g.GroundY(uv.transform.position.x, uv.transform.position.z); }
                while (W.Time < shot.explodeTime - 0.15 && Time.time - t0 < 20f) yield return null;      // the knockdown is started so that its arc is running when the shot explodes
                uint n = 1;
                foreach (var o in dies) { var uv = ViewOf(o); if (uv && !uv.Dying) uv.React(blast + Vector3.right * 0.5f, 3, (uint)o.Id * 2654435761u + n++, 0f, 6f); }
                if (live != null) { var lv = ViewOf(live); if (lv) lv.React(blast, 2, 977u, 0f); }
                var eof = new WaitForEndOfFrame(); var seenDead = new HashSet<int>(); var flyerIds = new HashSet<int>(); var wasInArc = new Dictionary<int, bool>();
                var arcLeft = new Dictionary<int, float>(); var arcOver = new Dictionary<int, float>();
                Vector3 l0 = live != null && ViewOf(live) ? ViewOf(live).transform.position : Vector3.zero;
                t0 = Time.time;
                while (Time.time - t0 < 8f)
                {
                    yield return eof;
                    foreach (var o in dies)
                    {
                        var uv = ViewOf(o); if (!uv) continue;
                        bool deadRules = o.Destroyed || o.KillAt >= 0;
                        if (deadRules && seenDead.Add(o.Id))
                        {   // his death event's frame: in the arc at the end of the frame before it; his arc's own time ends at most arcLeft later
                            if (uv.InKnockdownArc || (wasInArc.TryGetValue(o.Id, out bool arc) && arc)) flyerIds.Add(o.Id);
                            arcOver[o.Id] = Time.time + (arcLeft.TryGetValue(o.Id, out float left) ? left : 0f) + HangSlackSec;
                        }
                        if (!deadRules) { wasInArc[o.Id] = uv.InKnockdownArc; arcLeft[o.Id] = uv.KnockArcLeft; }
                        if (deadRules && !uv.Dying && arcOver.TryGetValue(o.Id, out float over) && Time.time > over && ground.TryGetValue(o.Id, out float g0))
                        {   // dead in the rules, held for his rocket, his arc's time over: he must be on the ground
                            float up = uv.transform.position.y - g.GroundY(uv.transform.position.x, uv.transform.position.z) - g0;
                            heldSamples++; maxUp = Mathf.Max(maxUp, up);
                            if (up > HangM) hangFrames++;
                        }
                        if (uv.ReactAndDeathBoth) both++;
                    }
                    if (live != null) { var lv = ViewOf(live); if (lv && !lv.Dying && lv.ReactingAny) { survivorFrames++; survivorMoved = Mathf.Max(survivorMoved, (lv.transform.position - l0).magnitude); } }
                    if (seenDead.Count >= dies.Count && dies.All(o => { var uv = ViewOf(o); return !uv || uv.Dying; }) && Time.time - t0 > 4f) break;
                }
                var log = UnitView.DeathWinsLog.Where(x => flyerIds.Contains(x.id)).GroupBy(x => x.id).Select(q => q.Last()).ToList();
                flyers += flyerIds.Count; flyersThrown += log.Count;
                foreach (var x in log)
                {
                    bool held = x.wave > 0.015f; if (held) heldFlyers++;
                    float late = x.thrownAt - (x.eventAt + x.wave); minLate = Mathf.Min(minLate, late);
                    if (late < -0.02f) before++;
                    if (held && x.thrownFrame == x.eventFrame) sameFrame++;
                    lines.Add($"#{x.id}: wave {x.wave:0.00} s, thrown {x.thrownAt - x.eventAt:0.00} s after his death event");
                }
                notes.Add($"salvo {salvos}: {dies.Count} grunts in the splash, dead in the rules {seenDead.Count}, in a knockdown's arc at their death {flyerIds.Count}, of them thrown with a record {log.Count}");
            }
            if (minLate == float.MaxValue) minLate = 0f;
            bool ok = flyers > 0 && flyersThrown == flyers && heldFlyers > 0 && before == 0 && sameFrame == 0 && heldSamples > 0 && hangFrames == 0 && both == 0 && survivorFrames > 0 && survivorMoved > 0.05f;
            Log(row, ok, $"deaths in a knockdown's arc {flyers} (want > 0), thrown {flyersThrown} (want all), of them held for their rocket {heldFlyers} (want > 0); thrown before eventAt + wave {before} (want 0; the earliest {minLate:+0.000;-0.000} s against it), "
                         + $"in the death event's own frame while held {sameFrame} (want 0); held frames after the arc's time {heldSamples} (want > 0), of them above the ground (> {HangM} m) {hangFrames} (want 0; the highest {maxUp:0.00} m); "
                         + $"a reaction and a death on one body {both} (want 0); the survivor's reaction frames {survivorFrames} (want > 0), it moved {survivorMoved:0.00} m");
            Note($"INFO {string.Join("; ", notes)}; held arcs run on {UnitView.DeathHoldArcFrames - af0} frames, landed {UnitView.DeathHoldLanded - al0}; {string.Join(", ", lines)}");
        }

        // ------------------------------------------------------------------ DEATH-GRIP: no gun grip on a dying body whose Animator stays on
        /// <summary>The left fist (LeftHandGrip, else the left hand bone) and the gun's forward grip, as WeaponHold finds them; the gap between
        /// them in metres (the robot's scale taken out), -1 when one is missing. WeaponHold's left-arm IK puts that fist on the forward grip.</summary>
        static float LeftFistGap(UnitView uv)
        {
            if (!uv) return -1f;
            Transform fist = null, lHand = null, fore = null;
            foreach (var t in uv.GetComponentsInChildren<Transform>(true))
            {
                switch (t.name)
                {
                    case "LeftHandGrip": fist = t; break;
                    case "mixamorigLeftHand": lHand = t; break;
                    case "Rifle_forward_grip": case "Bazooka_forward_grip": case "Foregrip": if (t.gameObject.activeInHierarchy) fore = t; break;
                }
            }
            var a = uv.GetComponentInChildren<Animator>();
            if (a && a.isHuman) { var h = a.GetBoneTransform(HumanBodyBones.LeftHand); if (h) lHand = h; }
            if (!fist) fist = lHand;
            if (!fist || !fore) return -1f;
            return Vector3.Distance(fist.position, fore.position) / Mathf.Max(0.2f, uv.transform.lossyScale.y);
        }

        /// <summary>Round 2 (review): most splash deaths are rag dolls with the Animator off, where WeaponHold does nothing whether the fix is in or
        /// not. This row makes dying bodies whose Animator stays on: enemy grunts knocked down (a long knockdown) and then killed by a flame in
        /// the rules (the melt: death_side with the Animator on; no splash), half in the arc, half lying. Read at the end of every frame: the
        /// left fist's gap to the forward grip (3 frames running under LeftFistM = a grip); a living grunt standing beside them is the
        /// calibration (his fist must sit on it, or the reading is blind and the row fails).</summary>
        IEnumerator DeathGripRow()
        {
            string row = "DEATH-GRIP knocked-down grunts killed by a flame (the melt, the Animator on): no gun grip on the dying body (the left fist read against the forward grip)";
            var at = new int[2]; yield return Spot(14, 10, at);
            if (at[0] < 0) { Log(row, false, "no free ground near the camera"); yield break; }
            int go0 = UnitView.DeathGripOffs;
            var cal = W.CreateObject("robot", "grunt", (at[0] + 1) * 16, (at[1] + 1) * 16, foe); g.SpawnUnitView(cal); made.Add(cal); cal.Damage = 0; Tough(cal);
            var dies = new List<ZObject>();
            for (int k = 0; k < 8; k++)
            {
                int cx = (at[0] + 3 + (k % 4) * 3) * 16, cy = (at[1] + 3 + (k / 4) * 4) * 16;
                if (!W.Path.TilePassable(cx >> 4, cy >> 4, true)) continue;
                var r = W.CreateObject("robot", "grunt", cx, cy, foe); g.SpawnUnitView(r); made.Add(r); r.Damage = 0; r.SetHealth(30); dies.Add(r);
            }
            if (dies.Count == 0) { Log(row, false, "no open ground for the grunts"); yield break; }
            yield return Real(1.2f);
            var eof = new WaitForEndOfFrame();
            var calGaps = new List<float>();
            for (int i = 0; i < 30; i++) { yield return eof; float gp = LeftFistGap(ViewOf(cal)); if (gp >= 0f) calGaps.Add(gp); }
            calGaps.Sort(); float calMed = calGaps.Count > 0 ? calGaps[calGaps.Count / 2] : -1f;
            var blast = ZGame.PxToWorld(at[0] * 16, (at[1] + 5) * 16);
            uint n = 1; foreach (var o in dies) { var uv = ViewOf(o); if (uv) uv.React(blast, 3, (uint)o.Id * 2246822519u + n++, 0f, 6f); }
            var killed = new HashSet<int>(); int animFrames = 0, flagFrames = 0, fistFrames = 0, measured = 0; float minGap = float.MaxValue;
            var run = new Dictionary<int, int>(); var melted = new HashSet<int>();
            float t0 = Time.time;
            while (Time.time - t0 < 5f)
            {
                yield return null;                                                                            // after Update, before CombatView's LateUpdate reads the events
                for (int i = 0; i < dies.Count; i++)
                {
                    var o = dies[i]; if (killed.Contains(o.Id) || o.Destroyed) continue;
                    float when = (i & 1) == 0 ? 0.15f : 0.9f;                                                      // in the arc / lying
                    if (Time.time - t0 < when) continue;
                    o.LastFireHit = W.Time; o.SetHealth(0); W.UpdateObjectHealth(o, -1); killed.Add(o.Id);       // a flame kill in the rules (the Death event's fire flag: the melt)
                }
                yield return eof;
                foreach (var o in dies)
                {
                    var uv = ViewOf(o); if (!uv || !uv.isActiveAndEnabled || !uv.Dying || !uv.AnimOn) { run[o.Id] = 0; continue; }   // a pop hides the body (nothing to pose)
                    animFrames++; melted.Add(o.Id);
                    if (uv.GripPosing) flagFrames++;
                    float gp = LeftFistGap(uv); if (gp < 0f) { run[o.Id] = 0; continue; }
                    measured++; minGap = Mathf.Min(minGap, gp);
                    int rn = gp < LeftFistM ? (run.TryGetValue(o.Id, out int r0) ? r0 : 0) + 1 : 0; run[o.Id] = rn;
                    if (rn >= 3) fistFrames++;                                                                    // the fist ON the forward grip 3 frames running (a clip passing by is not a grip)
                }
            }
            if (minGap == float.MaxValue) minGap = -1f;
            bool calOk = calMed >= 0f && calMed < LeftFistM;
            bool ok = calOk && killed.Count > 0 && animFrames > 0 && measured > 0 && flagFrames == 0 && fistFrames == 0;
            Log(row, ok, $"calibration: the living grunt's left fist {calMed:0.000} m from his forward grip (median of {calGaps.Count} frames; want < {LeftFistM} m: the reading sees a grip); "
                         + $"flame kills {killed.Count} of {dies.Count}, bodies dying with the Animator on {melted.Count}, their frames {animFrames} (want > 0), read {measured} (want > 0); "
                         + $"frames the grip flag was on {flagFrames} (want 0), frames the left fist sat on the forward grip (< {LeftFistM} m, 3 running) {fistFrames} (want 0; the closest {minGap:0.000} m)");
            Note($"INFO DEATH-GRIP: grips switched off again after Die {UnitView.DeathGripOffs - go0}");
        }

        /// <summary>A squad's members (the leader first), for the rows above.</summary>
        static class OzV11KitLite { public static IEnumerable<ZObject> Squad(ZObject o) { yield return o; foreach (var m in o.Minions) yield return m; } }
    }
}
#endif
