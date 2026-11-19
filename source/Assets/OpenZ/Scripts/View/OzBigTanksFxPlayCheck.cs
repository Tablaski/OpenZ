#if UNITY_EDITOR
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
    /// 1.5 kit big_tanks_fx: the Play check (editor only; menu OpenZ/V1.5/Big Tanks FX: Play Check, the Unit editor in Play). Rows, one after
    /// another on free open ground near the camera, at the normal RTS camera; dense frames (every 2nd rendered frame) per row to folder/&lt;row&gt;/:
    ///   P  both tanks dropped: the pads and parts built (neon runs, markers, searchlight, beacon, exhaust stubs), every part shown with the hull;
    ///   D  each drives 10 tiles, turns and drives back: dust volleys and puffs, exhaust puffs and dark bursts (with the diesel chuff), counted;
    ///   N  at night (the day clock set to 0.55, put back after): neon, markers, searchlight (switch-on sound, the beam, a real light or its pool),
    ///      the Bizon's beacon turning; the same tank made neutral (driver out): every lamp fades out, grey neon;
    ///   R  a Mammoth rams an enemy heavy, a Bizon and a Mammoth (each facing it, gap 20 px): FRAME BY FRAME the two barrels (MammothRecoil_L/_R
    ///      to MammothMuzzle_L/_R, radius 0.31 m) against every renderer box of the rammed vehicle (each renderer's own mesh box, turned with it):
    ///      frames where a barrel ENTERS the target after being clear must be 0; the overlap a ram may start with is counted apart;
    ///   C  a Bizon destroyed: 10-16 fuel cans thrown, 2-4 fire puffs, the can and the ignite sounds heard; the lamps dead, the parts gone with the hull.
    /// Report: folder/play_report.md.
    /// </summary>
    public class OzBigTanksFxPlayCheck : MonoBehaviour
    {
        public const string Tag = "[OpenZ V1.5 Big Tanks FX Play]";
        public static string Folder = "";
        public static bool Running { get; private set; }
        public static string Report { get; private set; } = "no Play check run yet";

        ZGame G; ZWorld W; RtsCamera rts; PlayerInput input;
        readonly StringBuilder md = new StringBuilder();
        int pass, fail; int capUntilFrame = -1; string capDir;

        public static void Begin(string folder)
        {
            if (Running || !ZGame.I || ZGame.I.World == null || !OzUnitEditor.Active) { Debug.LogWarning(Tag + " open the Unit editor in Play first"); return; }
            if (ZGame.I.GetComponent<OzBigTanksFxPlayCheck>()) { Debug.LogWarning(Tag + " already running"); return; }
            Running = true; Folder = folder; ZGame.I.gameObject.AddComponent<OzBigTanksFxPlayCheck>();
        }

        void Start() { Running = true; StartCoroutine(Run()); StartCoroutine(Capture()); }
        void OnDestroy() { Running = false; }

        IEnumerator Capture()
        {
            var eof = new WaitForEndOfFrame();
            while (true)
            {
                yield return eof;
                if (capDir == null || Time.frameCount > capUntilFrame) { capDir = null; continue; }
                if (Time.frameCount % 2 != 0) continue;
                var tex = ScreenCapture.CaptureScreenshotAsTexture(); if (!tex) continue;
                var raw = tex.GetRawTextureData(); int w = tex.width, h = tex.height; var fmt = tex.graphicsFormat;
                string path = Path.Combine(capDir, $"f{Time.frameCount:000000}_{Time.time:0000.000}s.jpg");
                Destroy(tex);
                System.Threading.Tasks.Task.Run(() => { File.WriteAllBytes(path, ImageConversion.EncodeArrayToJPG(raw, fmt, (uint)w, (uint)h, 0, 92)); });
            }
        }

        void Frames(string row, float seconds) { capDir = Path.Combine(Folder, row); Directory.CreateDirectory(capDir); capUntilFrame = Time.frameCount + Mathf.CeilToInt(seconds * 60f); }

        IEnumerator Run()
        {
            G = ZGame.I; W = G.World; rts = FindFirstObjectByType<RtsCamera>(); input = OzUnitEditor.I.Input;
            Directory.CreateDirectory(Folder);
            md.AppendLine($"# Big tanks FX Play check ({System.DateTime.Now:yyyy-MM-dd HH:mm}), the Unit editor, Game view {Screen.width} x {Screen.height}").AppendLine();
            md.AppendLine($"switches: Mammoth {ZWorld.MammothDeviation}, Bizon {ZWorld.BizonDeviation}, ram {ZWorld.MammothRamDeviation}, crush {ZWorld.BizonCrushDeviation}, duel {ZWorld.RamDuelDeviation}; fx on {OzBigTanksFx.On}").AppendLine();
            if (rts) rts.ZoomTo(62f);
            OzUnitEditor.SetPanelsHidden(true);
            var me = G.player; var foe = me == Team.Red ? Team.Blue : Team.Red;
            string[] big = { ZBigTanks.Mammoth, ZBigTanks.Bizon };
            var dn = FindFirstObjectByType<DayNight>(); float tod0 = dn ? dn.timeOfDay : -1f;

            // ---- P + D: parts, dust, exhaust
            foreach (var x in big)
            {
                if (!Area(out int ox, out int oy)) { Row("P/D " + x, false, "no free ground"); continue; }
                int sx = ox + 4 * 16, sy = oy + 5 * 16;
                var m = Make(x, sx, sy, me); yield return new WaitForSeconds(1.5f);
                Fly(sx + 5 * 16, sy); yield return new WaitForSeconds(1.2f);
                var fx = OzBigTanksFx.I; var t = fx ? fx.Of(m.Id) : null;
                bool partsShown = t != null && t.Holder("hull").GetComponentsInChildren<Renderer>(true).Where(r => r.name.EndsWith("_Pad") || r.name.StartsWith("Exhaust_") || r.name.StartsWith("Marker_Base")).All(r => r.enabled);
                int want = x == ZBigTanks.Mammoth ? OzBigTanksFx.MammothNeon.Length : OzBigTanksFx.BizonNeon.Length;
                Row("P " + x, t != null && t.NeonRuns == want && t.PadCount >= 4 && partsShown, t == null ? "no fx record" : $"neon runs {t.NeonRuns} (want {want}), pads {t.PadCount}, hull parts shown {partsShown}; {OzBigTanksFx.Last}");
                Deselect(); yield return Still("P_" + x + "_parts");
                int d0 = OzBigTanksFx.DustPuffs, v0 = OzBigTanksFx.DustVolleys, s0 = OzBigTanksFx.SmokePuffs, b0 = OzBigTanksFx.SmokeBursts, c0 = OzBigTankExhaust.Chuffs; float t0 = Time.unscaledTime;
                Frames("D_" + x + "_drive", 6f);
                W.SendWaypoints(m.Owner, m, new[] { ZWaypoint.Move(sx + 10 * 16, sy) });
                float until = Time.time + 10f; while (Time.time < until && m.Waypoints.Count > 0) yield return null;
                Frames("D_" + x + "_turn_back", 6f);
                W.SendWaypoints(m.Owner, m, new[] { ZWaypoint.Move(sx - 2 * 16, sy + 16) });
                until = Time.time + 12f; while (Time.time < until && m.Waypoints.Count > 0) yield return null;
                var snd = OzSoundLog.Since(t0).Select(e => e.key).Distinct().ToList();
                Row("D " + x, OzBigTanksFx.DustVolleys > v0 && OzBigTanksFx.DustPuffs - d0 >= 40 && OzBigTanksFx.SmokePuffs - s0 >= 20 && OzBigTanksFx.SmokeBursts > b0 && OzBigTankExhaust.Chuffs > c0,
                    $"dust volleys {OzBigTanksFx.DustVolleys - v0}, puffs {OzBigTanksFx.DustPuffs - d0}; smoke puffs {OzBigTanksFx.SmokePuffs - s0}, dark bursts {OzBigTanksFx.SmokeBursts - b0}, chuffs {OzBigTankExhaust.Chuffs - c0}; sounds: {string.Join(", ", snd)}");
                // ---- N: night lamps, then neutral
                if (dn) dn.timeOfDay = 0.55f;
                int so = OzBigTanksFx.SearchSwitchOns; t0 = Time.unscaledTime;
                yield return new WaitForSeconds(2f);
                Frames("N_" + x + "_night", 4f); yield return new WaitForSeconds(4f);
                bool beacon = x != ZBigTanks.Bizon || (t != null && t.BeaconLevel > 0.9f);
                bool lit = t != null && t.Power > 0.9f && t.SearchLevel > 0.9f && t.NeonGlow.Any(r => r.enabled) && beacon;
                snd = OzSoundLog.Since(t0).Select(e => e.key).Distinct().ToList();
                Row("N " + x + " night", lit && OzBigTanksFx.SearchSwitchOns > so && snd.Contains(OzBigTanksFx.SearchOnEvent),
                    t == null ? "no fx record" : $"power {t.Power:0.00}, searchlight {t.SearchLevel:0.00} (real light {(t.SearchLight && t.SearchLight.enabled ? "granted" : "pool picture")}), neon lit {t.NeonGlow.Count(r => r.enabled)}/{t.NeonGlow.Count}, beacon {t.BeaconLevel:0.00}; switch-ons {OzBigTanksFx.SearchSwitchOns - so}; sounds: {string.Join(", ", snd)}");
                m.DamageDriverHealth(100000000);                                                              // the driver sniped: the tank stands empty, neutral (Zod's way)
                yield return new WaitForSeconds(1.5f);
                Frames("N_" + x + "_neutral", 2f); yield return new WaitForSeconds(1.2f);
                Row("N " + x + " neutral", t != null && t.Power < 0.01f && !t.NeonGlow.Any(r => r.enabled) && t.SearchLevel < 0.05f, t == null ? "-" : $"power {t.Power:0.00}, neon lit {t.NeonGlow.Count(r => r.enabled)}, searchlight {t.SearchLevel:0.00}");
                if (dn) dn.timeOfDay = tod0;
                OzUePlayground.Remove(m); yield return new WaitForSeconds(1f);
            }

            // ---- R: the Mammoth's barrels in rams, frame by frame
            foreach (var target in new[] { "heavy", ZBigTanks.Bizon, ZBigTanks.Mammoth })
            {
                if (!Area(out int ox, out int oy)) { Row("R " + target, false, "no free ground"); continue; }
                int sx = ox + 4 * 16, sy = oy + 5 * 16;
                var m = Make(ZBigTanks.Mammoth, sx, sy, me); m.Damage = 0; m.FaceX = 9f; m.FaceY = 0f;
                int r = ZPathing.Radius(m) + ZPathing.VehicleRadiusOf(target) + 20;
                var t = Make(target, sx + r, sy, foe); t.Damage = 0; t.FaceX = 0f; t.FaceY = 9f;
                yield return null;
                Fly(sx + r / 2, sy); Deselect();
                var mv = ViewOf(m); var tv = ViewOf(t);
                var bars = new List<(Transform a, Transform b)>();
                foreach (var s in new[] { "_L", "_R" }) { var a = Deep(mv, "MammothRecoil" + s); var b = Deep(mv, "MammothMuzzle" + s); if (a && b) bars.Add((a, b)); }
                int frames = 0, entered = 0, startOverlap = 0; bool clearOnce = false; float tStart = Time.time;
                Frames("R_mammoth_vs_" + target, 5f);
                while (Time.time - tStart < 6f && OzV11Alive(t))
                {
                    yield return new WaitForEndOfFrame();
                    if (!tv || !mv) break;
                    frames++;
                    bool hit = false;
                    var rs = tv.GetComponentsInChildren<MeshRenderer>().Where(q => q.enabled && q.gameObject.activeInHierarchy && !q.name.ToLowerInvariant().Contains("shadow")).ToArray();
                    foreach (var (a, b) in bars)
                        for (int k = 0; k <= 30 && !hit; k++)
                        {
                            var p = Vector3.Lerp(a.position, b.position + (b.position - a.position).normalized * 0.02f, k / 30f);
                            foreach (var q in rs)
                            {
                                var mf = q.GetComponent<MeshFilter>(); if (!mf || !mf.sharedMesh) continue;
                                var lp = q.transform.InverseTransformPoint(p); var bb = mf.sharedMesh.bounds; var sc = q.transform.lossyScale;
                                var d = Vector3.Max(Vector3.zero, Vector3.Max(bb.min - lp, lp - bb.max)); d = Vector3.Scale(d, sc);
                                if (d.magnitude < 0.31f) { hit = true; break; }
                            }
                        }
                    if (hit) { if (clearOnce) entered++; else startOverlap++; } else clearOnce = true;
                }
                yield return new WaitForSeconds(2f);
                Row("R " + target, frames > 30 && entered == 0, $"{frames} frames from the start; a barrel ENTERED the {target} in {entered} (want 0); start overlap {startOverlap} frames; heaves held {OzRamBarrels.FlipWaits} (longest {OzRamBarrels.FlipWaitMax:0.00} s); "
                    + $"clear frames: pre {UnitAim.PreClearFrames}, ram {UnitAim.RamClearFrames}; target destroyed {t.Destroyed}");
                OzUePlayground.Remove(m); if (OzV11Alive(t)) OzUePlayground.Remove(t); yield return new WaitForSeconds(1f);
            }

            // ---- C: the Bizon's death: fuel cans
            if (Area(out int cx, out int cy))
            {
                int sx = cx + 6 * 16, sy = cy + 5 * 16;
                var bz = Make(ZBigTanks.Bizon, sx, sy, me); yield return new WaitForSeconds(1.5f);
                Fly(sx, sy); Deselect(); yield return new WaitForSeconds(1f);
                var t = OzBigTanksFx.I ? OzBigTanksFx.I.Of(bz.Id) : null;
                int th0 = OzBigTanksFx.CansThrown, f0 = OzBigTanksFx.CanFires, ld0 = OzBigTanksFx.LampDeaths, br0 = OzBigTankWreck.Breaks; float t0 = Time.unscaledTime;
                Frames("C_bizon_death_cans", 8f);
                bz.SetHealth(0); W.UpdateObjectHealth(bz, -1);
                float xd = Time.time; while (Time.time - xd < 45f && OzBigTankWreck.Breaks == br0) yield return null;
                Frames("C_bizon_cans_land", 6f);
                yield return new WaitForSeconds(6f);
                var snd = OzSoundLog.Since(t0).Select(e => e.key).Distinct().ToList();
                int thrown = OzBigTanksFx.CansThrown - th0, fires = OzBigTanksFx.CanFires - f0;
                bool partsGone = t == null || t.Holder("hull").GetComponentsInChildren<Renderer>(true).All(r => !r.enabled);
                Row("C bizon death", thrown >= OzBigTanksFx.CanMin && thrown <= OzBigTanksFx.CanMax && fires >= 1 && snd.Contains(OzFuelCan.LandEvent) && snd.Contains(OzBigTanksFx.CanFireEvent) && partsGone && (t == null || t.Power <= 0f),
                    $"cans thrown {thrown} (want {OzBigTanksFx.CanMin}-{OzBigTanksFx.CanMax}), fire puffs {fires}; lamps dead (power {(t != null ? t.Power : 0f):0.00}, deaths {OzBigTanksFx.LampDeaths - ld0}); hull parts hidden {partsGone}; {OzFuelCan.Line(OzFuelCan.LastBatch)}; sounds: {string.Join(", ", snd)}");
            }

            OzUnitEditor.SetPanelsHidden(false);
            md.AppendLine().AppendLine(OzBigTanksFx.Status()).AppendLine().AppendLine($"{pass} PASS, {fail} FAIL. Frames: {Folder}");
            Report = md.ToString();
            File.WriteAllText(Path.Combine(Folder, "play_report.md"), Report);
            Debug.Log(Tag + "\n" + Report);
            yield return new WaitForSeconds(1f);
            Destroy(this);
        }

        static bool OzV11Alive(ZObject o) => o != null && !o.Destroyed && o.KillAt < 0;

        IEnumerator Still(string name)
        {
            yield return new WaitForEndOfFrame();
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            if (tex) { File.WriteAllBytes(Path.Combine(Folder, name + ".jpg"), tex.EncodeToJPG(95)); Destroy(tex); }
        }

        ZObject Make(string type, int cx, int cy, Team t)
        {
            var o = W.CreateObject("vehicle", type, cx - 16, cy - 16, t); W.Emit(ZEventType.Created, o);
            typeof(OzUePlayground).GetMethod("MarkChanged", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)?.Invoke(null, null);
            return o;
        }

        UnitView ViewOf(ZObject o) => o != null && G.Views.TryGetValue(o.Id, out var v) ? v : null;
        static Transform Deep(Component c, string n) => c ? c.GetComponentsInChildren<Transform>(true).FirstOrDefault(q => q.name == n) : null;
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
            if (W.Pathing != null) foreach (var L in W.Pathing.Lanes) if (L.ZX1 > x0 && L.ZX0 < x1 && L.ZY1 > y0 && L.ZY0 < y1) return false;
            foreach (var o in W.Objects) if (!o.Destroyed && o.CenterX > x0 && o.CenterX < x1 && o.CenterY > y0 && o.CenterY < y1 && (o.IsUnit || o.Kind == ZKind.Building)) return false;
            return true;
        }

        void Row(string row, bool ok, string what) { md.AppendLine($"- {(ok ? "PASS" : "FAIL")} {row}: {what}"); if (ok) pass++; else fail++; }
    }
}
#endif
