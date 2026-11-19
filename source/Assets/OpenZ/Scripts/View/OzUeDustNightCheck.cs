#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.5 kit ue_dust_night: the Play check (editor only; menu OpenZ/V1.5/Unit Editor: Dust + Night Check, the Unit editor open in Play). Rows,
    /// at the normal RTS camera (zoom 62, panels hidden), dense frames (every 2nd rendered frame) per row into folder/&lt;row&gt;/:
    ///   S  the floors' table (Desert dunes, Snow field; ten dark floors without dust);
    ///   G  a dark floor (Survey grid): a Bizon drives 8 tiles: no track print written (the levels' sand look never on a line grid);
    ///   D  Desert dunes: a Bizon, then a Mammoth, each drives 10 tiles, turns and drives back: prints written, the big tanks' heavy dust
    ///      (OzBigTanksFx puffs) and the dust in the air (DustTrails live puffs), counted;
    ///   N  NIGHT clicked (OzUeNight.Set, as the switch): NightK 0 -> 1 in about EaseSeconds (0.8 - 1.5 s), the clock in DayNight's night, each tank's
    ///      searchlight up, neon lit, the Bizon's beacon turning; a Mammoth drives at night on the dunes (frames: lamps + dust);
    ///   B  DAY clicked: NightK back to 0, the sun and the ambient back to the playground's day exactly, the lamps out;
    ///   W  the floor switched to a dark one and back: the prints and the dust wiped.
    /// The floor in use and DAY are put back at the end. Report: folder/play_report.md.
    /// </summary>
    public class OzUeDustNightCheck : MonoBehaviour
    {
        public const string Tag = "[OpenZ V1.5 UE Dust + Night]";
        public static string Folder = "";
        public static bool Running { get; private set; }

        ZGame G; ZWorld W; RtsCamera rts; PlayerInput input;
        readonly StringBuilder md = new StringBuilder();
        int pass, fail; int capUntilFrame = -1; string capDir;

        public static void Begin(string folder)
        {
            if (Running || !ZGame.I || ZGame.I.World == null || !OzUnitEditor.Active) { Debug.LogWarning(Tag + " open the Unit editor in Play first"); return; }
            Running = true; Folder = folder; ZGame.I.gameObject.AddComponent<OzUeDustNightCheck>();
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
            int style0 = OzUeBackdrop.Style;
            md.AppendLine($"# Unit editor dust + night Play check ({System.DateTime.Now:yyyy-MM-dd HH:mm}), Game view {Screen.width} x {Screen.height}").AppendLine();
            md.AppendLine($"tracks option {PlayerPrefs.GetInt("oz_tracks", 1)}, dust option {PlayerPrefs.GetInt("oz_dusttrails", 1)}; TrackMarks {(TrackMarks.I ? "made" : "MISSING")}, DustTrails {(DustTrails.I ? "made" : "MISSING")}; big tanks fx on {OzBigTanksFx.On}").AppendLine();
            if (rts) rts.ZoomTo(62f);
            OzUnitEditor.SetPanelsHidden(true);
            var me = G.player;

            // ---- S
            var sl = new StringBuilder(); int sf = OzUeDust.SelfCheck(sl);
            Row("S floors", sf == 0, sl.ToString().Replace("\n", " ").Trim());

            // ---- G: a dark floor, no prints
            OzUeBackdrop.SetStyle(0); yield return null; yield return null;
            if (Area(out int gx, out int gy))
            {
                int sx = gx + 4 * 16, sy = gy + 5 * 16;
                var b = Make(OzBigTanks.Bizon, sx, sy, me); yield return new WaitForSeconds(1.2f);
                Fly(sx + 4 * 16, sy); Deselect();
                int q0 = TrackMarks.I ? TrackMarks.I.QuadsWritten : -1;
                Frames("G_dark_floor_no_prints", 3f);
                W.SendWaypoints(b.Owner, b, new[] { ZWaypoint.Move(sx + 8 * 16, sy) });
                float until = Time.time + 9f; while (Time.time < until && b.Waypoints.Count > 0) yield return null;
                int q1 = TrackMarks.I ? TrackMarks.I.QuadsWritten : -1;
                Row("G dark floor", TrackMarks.I && q1 == q0, $"floor {OzUeBackdrop.Names[OzUeBackdrop.Style]}: print quads written {q1 - q0} (want 0)");
                OzUePlayground.Remove(b); yield return new WaitForSeconds(1f);
            }
            else Row("G dark floor", false, "no free ground");

            // ---- D + N + B on Desert dunes
            OzUeBackdrop.SetStyle(OzUeDust.DunesStyle); yield return null; yield return null;
            var sun = RenderSettings.sun; float dayInt = sun ? sun.intensity : 0f; var dayCol = sun ? sun.color : Color.black; var daySky = RenderSettings.ambientSkyColor;
            foreach (var x in new[] { OzBigTanks.Bizon, OzBigTanks.Mammoth })
            {
                if (!Area(out int ox, out int oy)) { Row("D " + x, false, "no free ground"); continue; }
                int sx = ox + 4 * 16, sy = oy + 5 * 16;
                var m = Make(x, sx, sy, me); yield return new WaitForSeconds(1.5f);
                Fly(sx + 5 * 16, sy); Deselect(); yield return new WaitForSeconds(1f);
                int q0 = TrackMarks.I ? TrackMarks.I.QuadsWritten : 0, d0 = OzBigTanksFx.DustPuffs, live = 0;
                Frames("D_" + x + "_dunes_drive", 6f);
                W.SendWaypoints(m.Owner, m, new[] { ZWaypoint.Move(sx + 10 * 16, sy) });
                float until = Time.time + 10f; while (Time.time < until && m.Waypoints.Count > 0) { if (DustTrails.I) live = Mathf.Max(live, DustTrails.I.Live); yield return null; }
                Frames("D_" + x + "_dunes_turn_back", 6f);
                W.SendWaypoints(m.Owner, m, new[] { ZWaypoint.Move(sx - 2 * 16, sy + 16) });
                until = Time.time + 12f; while (Time.time < until && m.Waypoints.Count > 0) { if (DustTrails.I) live = Mathf.Max(live, DustTrails.I.Live); yield return null; }
                int q = (TrackMarks.I ? TrackMarks.I.QuadsWritten : 0) - q0, d = OzBigTanksFx.DustPuffs - d0;
                Row("D " + x, q > 40 && d >= 40 && live >= 20, $"print quads {q} (want > 40), big tank dust puffs {d} (want >= 40), dust in the air at most {live} puffs (want >= 20); colour {(Color32)OzUeDust.Colour}");

                // ---- N: NIGHT
                var t = OzBigTanksFx.I ? OzBigTanksFx.I.Of(m.Id) : null;
                int so = OzBigTanksFx.SearchSwitchOns; float t0 = Time.unscaledTime, reached = -1f;
                OzUeNight.Set(true);
                Frames("N_" + x + "_night_ease", 2f);
                while (Time.unscaledTime - t0 < 3f) { if (reached < 0f && NightGrade.NightK > 0.99f) reached = Time.unscaledTime - t0; yield return null; }
                yield return new WaitForSeconds(1.5f);
                bool beacon = x != OzBigTanks.Bizon || (t != null && t.BeaconLevel > 0.9f);
                bool lit = t != null && t.Power > 0.9f && t.SearchLevel > 0.9f && t.NeonGlow.Any(r => r.enabled) && beacon;
                Row("N " + x + " night", lit && reached > 0.8f && reached < 1.5f && NightGrade.TimeOfDay > 0.56f && NightGrade.TimeOfDay < 0.80f && OzBigTanksFx.SearchSwitchOns > so,
                    t == null ? "no fx record" : $"NightK 1 after {reached:0.00} s (want 0.8 - 1.5), clock {NightGrade.TimeOfDay:0.000}; power {t.Power:0.00}, searchlight {t.SearchLevel:0.00}, neon lit {t.NeonGlow.Count(r => r.enabled)}/{t.NeonGlow.Count}, beacon {t.BeaconLevel:0.00}, switch-ons {OzBigTanksFx.SearchSwitchOns - so}; sun {(sun ? sun.intensity : 0f):0.00} (day {dayInt:0.00})");
                Frames("N_" + x + "_night_drive", 5f);
                W.SendWaypoints(m.Owner, m, new[] { ZWaypoint.Move(sx + 6 * 16, sy - 16) });
                until = Time.time + 8f; while (Time.time < until && m.Waypoints.Count > 0) yield return null;

                // ---- B: DAY
                OzUeNight.Set(false);
                yield return new WaitForSeconds(2f);
                bool back = NightGrade.NightK < 0.001f && (!sun || (Mathf.Abs(sun.intensity - dayInt) < 1e-4f && sun.color == dayCol)) && RenderSettings.ambientSkyColor == daySky;
                Row("B " + x + " day again", back && (t == null || t.SearchLevel < 0.05f || t.Power < 0.01f || !OzBigTanksFx.Night(out _)),
                    $"NightK {NightGrade.NightK:0.000}, sun {(sun ? sun.intensity : 0f):0.0000} (day {dayInt:0.0000}), ambient sky {RenderSettings.ambientSkyColor} (day {daySky}); searchlight {(t != null ? t.SearchLevel : 0f):0.00}");
                Frames("B_" + x + "_day_again", 1f); yield return new WaitForSeconds(1f);
                OzUePlayground.Remove(m); yield return new WaitForSeconds(1f);
            }

            // ---- W: another floor wipes
            int c0 = OzUeDust.Clears;
            OzUeBackdrop.SetStyle(0); yield return null; yield return null; yield return null;
            Row("W wipe", OzUeDust.Clears > c0 && (!DustTrails.I || DustTrails.I.Live == 0), $"wipes {OzUeDust.Clears - c0}, dust in the air {(DustTrails.I ? DustTrails.I.Live : 0)}");

            OzUeBackdrop.SetStyle(style0); OzUeNight.Set(false);
            OzUnitEditor.SetPanelsHidden(false);
            md.AppendLine().AppendLine(OzUeNight.Status()).AppendLine().AppendLine(OzBigTanksFx.Status()).AppendLine().AppendLine($"{pass} PASS, {fail} FAIL. Frames: {Folder}");
            File.WriteAllText(Path.Combine(Folder, "play_report.md"), md.ToString());
            Debug.Log(Tag + "\n" + md);
            yield return new WaitForSeconds(1f);
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
