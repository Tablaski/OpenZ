#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.5 kit big_tanks_dust_redo: the Play checks (the Unit editor in Play; menu OpenZ/V1.5/Big Tanks Dust Redo: ...). Report folder/report.md.
    ///   D  each big tank (Mammoth, Bizon) drives off, runs straight, turns back, stops: dense frames at two RTS zooms (45 m, 62 m: the owner's frames);
    ///      counted: dust and exhaust emitted while driving, a burst on setting off, NO dust 1.2 s after it stands (the smoothed ramp), idle wisps.
    ///   K  a driving Mammoth destroyed: from that frame no new particle, the live ones gone within FadeSeconds + 0.3 s, the emitter destroyed (frames).
    ///   N  the counted 50: particle systems, emitters and OZ_BigTankDust objects counted; 25 Mammoths + 25 Bizons placed and driven (100 systems more,
    ///      every system under its cap); 20 REMOVEd, 10 destroyed, the rest gone by CLEAR FIELD; 2 s later every count is back to the start, no
    ///      released emitter emitted again.
    /// </summary>
    public class OzBigTanksDustCheck : MonoBehaviour
    {
        public const string Tag = "[OpenZ V1.5 Big Tanks Dust Redo]";
        public static string Folder = "";
        public static bool Running { get; private set; }
        public static string Report { get; private set; } = "no check run yet";
        static bool countOnly;

        ZGame G; ZWorld W; RtsCamera rts; PlayerInput input;
        readonly StringBuilder md = new StringBuilder();
        int pass, fail; int capUntilFrame = -1; string capDir;

        public static void Begin(string folder, bool onlyCount)
        {
            if (Running || !ZGame.I || ZGame.I.World == null || !OzUnitEditor.Active) { Debug.LogWarning(Tag + " open the Unit editor in Play first"); return; }
            Running = true; Folder = folder; countOnly = onlyCount; ZGame.I.gameObject.AddComponent<OzBigTanksDustCheck>();
        }

        void Start() { StartCoroutine(Run()); StartCoroutine(Capture()); }
        void OnDestroy() { Running = false; }

        IEnumerator Capture()
        {
            var eof = new WaitForEndOfFrame();
            while (true)
            {
                yield return eof;
                if (capDir == null || Time.frameCount > capUntilFrame) { capDir = null; continue; }
                if (Time.frameCount % 3 != 0) continue;                                                // dense: every 3rd frame (~20 a second)
                var tex = ScreenCapture.CaptureScreenshotAsTexture(); if (!tex) continue;
                var raw = tex.GetRawTextureData(); int w = tex.width, h = tex.height; var fmt = tex.graphicsFormat;
                string path = Path.Combine(capDir, $"f{Time.frameCount:000000}_{Time.time:0000.000}s.jpg");
                Destroy(tex);
                System.Threading.Tasks.Task.Run(() => { File.WriteAllBytes(path, ImageConversion.EncodeArrayToJPG(raw, fmt, (uint)w, (uint)h, 0, 92)); });
            }
        }

        void Frames(string row, float seconds) { capDir = Path.Combine(Folder, row); Directory.CreateDirectory(capDir); capUntilFrame = Time.frameCount + Mathf.CeilToInt(seconds * 60f); }

        static int Systems() => FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
        static int RedoSystems()
        {
            int n = 0;
            foreach (var p in FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (p && p.GetComponentInParent<OzBigTanksDust>(true)) n++;
            return n;
        }
        static int Holders()
        {
            int n = 0;
            foreach (var e in FindObjectsByType<OzBigTanksDust>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (e) n++;
            return n;
        }

        IEnumerator Run()
        {
            G = ZGame.I; W = G.World; rts = FindFirstObjectByType<RtsCamera>(); input = OzUnitEditor.I.Input;
            Directory.CreateDirectory(Folder);
            md.AppendLine($"# Big tanks dust redo check ({System.DateTime.Now:yyyy-MM-dd HH:mm}), the Unit editor, floor style {OzUeBackdrop.Style}, Game view {Screen.width} x {Screen.height}").AppendLine();
            md.AppendLine($"switches: redo {OzBigTanksDust.On}, big tanks fx {OzBigTanksFx.On}; dust option {(DustTrails.I ? DustTrails.I.isActiveAndEnabled.ToString() : "no DustTrails")}, ground answers {(TrackMarks.I && TrackMarks.I.Surface != null)}").AppendLine();
            OzUnitEditor.SetPanelsHidden(true);
            var me = G.player;
            string[] big = { ZBigTanks.Mammoth, ZBigTanks.Bizon };

            if (!countOnly)
            {
                // ---- D: drive, turn, stop, at two zooms
                foreach (var x in big)
                    foreach (var zoom in new[] { 45f, 62f })
                    {
                        if (!Area(out int ox, out int oy)) { Row($"D {x} {zoom:0}", false, "no free ground"); continue; }
                        int sx = ox + 4 * 16, sy = oy + 5 * 16;
                        var m = Make(x, sx, sy, me); yield return new WaitForSeconds(1.5f);
                        if (rts) rts.ZoomTo(zoom);
                        Fly(sx + 5 * 16, sy); Deselect(); yield return new WaitForSeconds(1.5f);
                        var t = OzBigTanksFx.I ? OzBigTanksFx.I.Of(m.Id) : null;
                        int d0 = OzBigTanksDust.DustEmitted, s0 = OzBigTanksDust.SmokeEmitted, b0 = OzBigTanksFx.SmokeBursts;
                        string row = $"D_{x}_{zoom:0}m";
                        Frames(row + "_1_setoff_straight", 7f);
                        W.SendWaypoints(m.Owner, m, new[] { ZWaypoint.Move(sx + 10 * 16, sy) });
                        float until = Time.time + 10f; while (Time.time < until && m.Waypoints.Count > 0) yield return null;
                        Frames(row + "_2_turn_back", 7f);
                        W.SendWaypoints(m.Owner, m, new[] { ZWaypoint.Move(sx - 2 * 16, sy + 16) });
                        until = Time.time + 12f; while (Time.time < until && m.Waypoints.Count > 0) yield return null;
                        int dDrive = OzBigTanksDust.DustEmitted - d0, sDrive = OzBigTanksDust.SmokeEmitted - s0, bursts = OzBigTanksFx.SmokeBursts - b0;
                        Frames(row + "_3_stop_settle", 4f);
                        yield return new WaitForSeconds(1.2f);
                        int d1 = OzBigTanksDust.DustEmitted, s1 = OzBigTanksDust.SmokeEmitted;
                        yield return new WaitForSeconds(2.5f);
                        int dStill = OzBigTanksDust.DustEmitted - d1, sStill = OzBigTanksDust.SmokeEmitted - s1;
                        var e = t != null ? t.DustFx : null;
                        bool ok = e != null && dDrive >= 80 && sDrive >= 40 && bursts >= 1 && dStill == 0 && sStill > 0 && e.Level < 0.01f
                                  && e.DustLive <= OzBigTanksDust.DustCap && e.SmokeLive <= OzBigTanksDust.SmokeCap;
                        Row($"D {x} {zoom:0} m", ok, e == null ? "no emitter" : $"driving: dust {dDrive}, exhaust {sDrive}, dark bursts {bursts}; standing (1.2 s after the stop, 2.5 s): dust {dStill} (want 0), idle wisps {sStill} (want > 0); level {e.Level:0.00}; live dust {e.DustLive}/{OzBigTanksDust.DustCap}, smoke {e.SmokeLive}/{OzBigTanksDust.SmokeCap}");
                        OzUePlayground.Remove(m); yield return new WaitForSeconds(1.6f);
                    }

                // ---- K: a driving Mammoth destroyed
                if (Area(out int kx, out int ky))
                {
                    int sx = kx + 4 * 16, sy = ky + 5 * 16;
                    var m = Make(ZBigTanks.Mammoth, sx, sy, me); yield return new WaitForSeconds(1.5f);
                    if (rts) rts.ZoomTo(45f);
                    Fly(sx + 5 * 16, sy); Deselect();
                    W.SendWaypoints(m.Owner, m, new[] { ZWaypoint.Move(sx + 14 * 16, sy) });
                    yield return new WaitForSeconds(3f);
                    var t = OzBigTanksFx.I ? OzBigTanksFx.I.Of(m.Id) : null; var e = t != null ? t.DustFx : null;
                    int live0 = e ? e.DustLive + e.SmokeLive : 0;
                    Frames("K_mammoth_death_fade", 5f);
                    m.SetHealth(0); W.UpdateObjectHealth(m, -1);
                    float t0 = Time.time; int after = 0, d0 = OzBigTanksDust.DustEmitted, s0 = OzBigTanksDust.SmokeEmitted; bool released = false;
                    while (Time.time - t0 < OzBigTanksDust.FadeSeconds + 0.6f)
                    {
                        if (e && e.Released) { if (!released) { released = true; d0 = OzBigTanksDust.DustEmitted; s0 = OzBigTanksDust.SmokeEmitted; } }
                        else if (released) break;
                        yield return null;
                    }
                    after = OzBigTanksDust.DustEmitted - d0 + OzBigTanksDust.SmokeEmitted - s0;
                    bool gone = !e;
                    Row("K mammoth death", released && gone && after == 0 && live0 > 0 && OzBigTanksDust.LastFadeSeconds <= OzBigTanksDust.FadeSeconds + 0.05f,
                        $"live particles at the kill {live0}; released {released}; emitted after the release {after} (want 0); emitter destroyed {gone} after {OzBigTanksDust.LastFadeSeconds:0.00} s (fade {OzBigTanksDust.FadeSeconds} s)");
                    yield return new WaitForSeconds(1f);
                    OzUePlayground.Remove(m); yield return new WaitForSeconds(1f);
                }
            }

            // ---- N: the counted 50
            {
                yield return new WaitForSeconds(1.5f);
                int ps0 = Systems(), rs0 = RedoSystems(), h0 = Holders(), a0 = OzBigTanksDust.Alive, made0 = OzBigTanksDust.Made, des0 = OzBigTanksDust.Destroyed;
                var list = new List<ZObject>();
                var f = rts ? rts.focus : Vector3.zero;
                int cx = Mathf.RoundToInt(f.x * 8f), cy = Mathf.RoundToInt(-f.z * 8f);
                for (int i = 0; i < 50; i++)
                {
                    int gx = cx + ((i % 10) - 5) * 6 * 16, gy = cy + ((i / 10) - 2) * 6 * 16;
                    list.Add(Make(i % 2 == 0 ? ZBigTanks.Mammoth : ZBigTanks.Bizon, gx, gy, me));
                }
                if (rts) rts.ZoomTo(120f);
                yield return new WaitForSeconds(2f);
                foreach (var o in list) if (o != null && !o.Destroyed) W.SendWaypoints(o.Owner, o, new[] { ZWaypoint.Move(o.CenterX + 3 * 16, o.CenterY) });
                Frames("N_fifty_driving", 2f);
                yield return new WaitForSeconds(3f);
                int ps1 = Systems(), rs1 = RedoSystems(), h1 = Holders(), a1 = OzBigTanksDust.Alive; int overCap = 0, emitting = 0;
                foreach (var e in FindObjectsByType<OzBigTanksDust>(FindObjectsSortMode.None))
                {
                    if (e.DustLive > OzBigTanksDust.DustCap || e.SmokeLive > OzBigTanksDust.SmokeCap) overCap++;
                    if (e.DustLive + e.SmokeLive > 0) emitting++;
                }
                Row("N placed 50", a1 - a0 == 50 && h1 - h0 == 50 && rs1 - rs0 == 100 && overCap == 0,
                    $"emitters {a0} -> {a1} (+{a1 - a0}), OZ_BigTankDust objects {h0} -> {h1}, the redo's particle systems {rs0} -> {rs1} (+{rs1 - rs0}, want +100), all particle systems in the scene {ps0} -> {ps1}; with particles {emitting}; over the cap {overCap}");
                int r0 = OzBigTanksDust.Releases;
                for (int i = 0; i < 20; i++) OzUePlayground.Remove(list[i]);                                    // REMOVE
                for (int i = 20; i < 30; i++) { var o = list[i]; if (o != null && !o.Destroyed) { o.SetHealth(0); W.UpdateObjectHealth(o, -1); } }   // destroyed
                yield return new WaitForSeconds(0.6f);
                int de0 = OzBigTanksDust.DustEmitted, se0 = OzBigTanksDust.SmokeEmitted;
                int stillOn = 0; foreach (var e in FindObjectsByType<OzBigTanksDust>(FindObjectsSortMode.None)) if (!e.Released) stillOn++;
                OzUePlayground.ClearField();                                                                   // CLEAR FIELD: the rest (and the wrecks)
                yield return new WaitForSeconds(0.6f);
                int afterRelease = 0; foreach (var e in FindObjectsByType<OzBigTanksDust>(FindObjectsSortMode.None)) if (!e.Released) afterRelease++;
                yield return new WaitForSeconds(OzBigTanksDust.FadeSeconds + 1.4f);
                int ps2 = Systems(), rs2 = RedoSystems(), h2 = Holders(), a2 = OzBigTanksDust.Alive;
                Row("N removed 50", rs2 == rs0 && h2 == h0 && a2 == a0 && afterRelease == 0 && OzBigTanksDust.Releases - r0 >= 50 && OzBigTanksDust.Destroyed - des0 == OzBigTanksDust.Made - made0,
                    $"after 20 REMOVE + 10 destroyed + CLEAR FIELD: the redo's particle systems {rs2} (start {rs0}), all particle systems in the scene {ps2} (start {ps0}{(ps2 == ps0 ? ", the same" : ": other effects (cans, wrecks) differ")}), OZ_BigTankDust objects {h2} (start {h0}), emitters alive {a2} (start {a0}); releases {OzBigTanksDust.Releases - r0}; made {OzBigTanksDust.Made - made0}, destroyed {OzBigTanksDust.Destroyed - des0}; still emitting before CLEAR FIELD {stillOn} (the 20 living), after it {afterRelease}");
                int de1 = OzBigTanksDust.DustEmitted - de0, se1 = OzBigTanksDust.SmokeEmitted - se0;
                md.AppendLine($"  (emitted by the 20 still living until CLEAR FIELD: dust {de1}, exhaust {se1})");
            }

            OzUnitEditor.SetPanelsHidden(false);
            md.AppendLine().AppendLine(OzBigTanksFx.Status()).AppendLine()
              .AppendLine($"redo: made {OzBigTanksDust.Made}, released {OzBigTanksDust.Releases}, destroyed {OzBigTanksDust.Destroyed}, alive {OzBigTanksDust.Alive}, hull boxes {OzBigTanksDust.HullBoxes}, material fails {OzBigTanksDust.MaterialFails}; dust {OzBigTanksDust.DustEmitted}, exhaust {OzBigTanksDust.SmokeEmitted}")
              .AppendLine().AppendLine($"{pass} PASS, {fail} FAIL. Frames: {Folder}");
            Report = md.ToString();
            File.WriteAllText(Path.Combine(Folder, "report.md"), Report);
            Debug.Log(Tag + " done: " + pass + " PASS, " + fail + " FAIL (" + Path.Combine(Folder, "report.md") + ")\n" + Report);
            Running = false;
            Destroy(this);
        }

        ZObject Make(string type, int cx, int cy, Team t)
        {
            var o = W.CreateObject("vehicle", type, cx - 16, cy - 16, t); W.Emit(ZEventType.Created, o);
            typeof(OzUePlayground).GetMethod("MarkChanged", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)?.Invoke(null, null);
            return o;
        }

        void Fly(int px, int py) { if (rts) rts.FlyTo(ZGame.PxToWorld(px, py)); }
        void Deselect() { if (input) input.Selection.Clear(); }

        bool Area(out int ox, out int oy)
        {
            ox = oy = 0;
            var f = rts ? rts.focus : Vector3.zero;
            int cx = Mathf.RoundToInt(f.x * 8f) >> 4, cy = Mathf.RoundToInt(-f.z * 8f) >> 4;
            for (int r = 0; r < 50; r += 2)
                for (int dy = -r; dy <= r; dy += 2)
                    for (int dx = -r; dx <= r; dx += 2)
                    {
                        int tx = cx + dx - 14, ty = cy + dy - 6;
                        if (Free(tx, ty, 28, 13)) { ox = tx * 16; oy = ty * 16; return true; }
                    }
            return false;
        }

        bool Free(int tx, int ty, int w, int h)
        {
            for (int j = ty; j < ty + h; j++)
                for (int i = tx; i < tx + w; i++)
                {
                    if (!W.Path.TilePassableGrid(i, j, false, false)) return false;
                    var k = W.Map.ClassAt(i, j); if (k != ZClass.Ground && k != ZClass.Road) return false;
                }
            int x0 = tx * 16 - 32, y0 = ty * 16 - 32, x1 = (tx + w) * 16 + 32, y1 = (ty + h) * 16 + 32;
            foreach (var o in W.Objects) if (!o.Destroyed && o.CenterX > x0 && o.CenterX < x1 && o.CenterY > y0 && o.CenterY < y1 && (o.IsUnit || o.Kind == ZKind.Building)) return false;
            return true;
        }

        void Row(string row, bool ok, string what) { md.AppendLine($"- {(ok ? "PASS" : "FAIL")} {row}: {what}"); if (ok) pass++; else fail++; }
    }
}
#endif
