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
    /// 1.5 kit bizon_crush_v3: the frames check of the Bizon crush in the Unit editor (editor only; the menu OpenZ/1.5/Checks/Bizon Crush
    /// Frames, Editor/OzV15BizonCrushFramesMenu.cs). For each target (heavy, medium, medium tank, light) it stages a Bizon crush twice on free
    /// open ground: once seen from the owner's LOW SIDE VIEW (the camera 2 m up, 15 m beside the crush, the Bizon coming from the left) and once
    /// from the normal RTS CAMERA above. Every 2nd rendered frame (JPEG) of each crush goes to folder/&lt;row&gt;/, the ride's and the crush's
    /// numbers per frame to folder/&lt;row&gt;/numbers.csv, and a report to folder/crush_frames_report.md: the parts dented / not readable, the
    /// deepest press, the turret (when it was torn off after the first press, its travel, its landing and rest), the barrel's bend, the pieces
    /// torn off, the pitch / roll smoothness (direction reversals per second, the largest frame-to-frame change of the pitch's rate), the
    /// longest dent pass. Contact sheets: kits/bizon_crush_v3/tools/make_sheets.py. Staged units are removed after each row.
    /// </summary>
    public class OzBizonCrushFramesCheck : MonoBehaviour
    {
        public const string Tag = "[OpenZ 1.5 Bizon Crush Frames]";
        public static bool Running { get; private set; }
        public static string Folder = "", Report = "no run yet";
        public static int FramesWritten;

        ZGame G; ZWorld W; RtsCamera rts; PlayerInput input;
        readonly StringBuilder md = new StringBuilder();
        int capUntilFrame = -1; string capDir; StringBuilder csv; OzCrushSquash watch;

        /// <summary>The targets of the menu's two runs: the tanks the owner named (heavy, medium, medium tank, light), and every other kind the
        /// Bizon crushes (jeep, APC, crane, missile launcher, the four ground guns, the Mammoth, a Bizon).</summary>
        public static readonly string[] Tanks = { "heavy", "medium", ZMTank.Type, "light" };
        public static readonly string[] Others = { "jeep", "apc", "crane", "missile_launcher", "cannon:gun", "cannon:gatling", "cannon:howitzer", "cannon:missile_cannon", ZBigTanks.Mammoth, ZBigTanks.Bizon };
        static string[] kinds = Tanks;

        public static void Begin(string folder) => Begin(folder, Tanks);

        public static void Begin(string folder, string[] targets)
        {
            kinds = targets ?? Tanks;
            if (Running || !ZGame.I || ZGame.I.World == null || !OzUnitEditor.Active) { Debug.LogWarning(Tag + " open the Unit editor in Play first"); return; }
            if (ZGame.I.GetComponent<OzBizonCrushFramesCheck>()) { Debug.LogWarning(Tag + " already running"); return; }
            Running = true; Folder = folder; ZGame.I.gameObject.AddComponent<OzBizonCrushFramesCheck>();
        }

        void Start() { Running = true; StartCoroutine(Run()); StartCoroutine(Capture()); }
        void OnDestroy() { Running = false; if (rts) rts.enabled = true; }

        IEnumerator Capture()
        {
            var eof = new WaitForEndOfFrame();
            while (true)
            {
                yield return eof;
                if (capDir == null || Time.frameCount > capUntilFrame) { capDir = null; continue; }
                if (csv != null)
                    csv.AppendLine($"{Time.frameCount},{Time.time:0.000},{OzCrushRide.Pitch:0.000},{OzCrushRide.Roll:0.000},{OzCrushRide.Lift:0.000},{OzCrushRide.GoalPitch:0.000},"
                                   + $"{(watch ? watch.Amount : 0f):0.000},{(watch ? watch.TurretState : "-")},{(watch ? watch.BarrelDeg : 0f):0.0},{OzCrushSquash.LastDeformMs:0.00}");
                if (Time.frameCount % 2 != 0) continue;
                var tex = ScreenCapture.CaptureScreenshotAsTexture();
                if (!tex) continue;
                var raw = tex.GetRawTextureData(); int w = tex.width, h = tex.height; var fmt = tex.graphicsFormat;
                string path = Path.Combine(capDir, $"f{Time.frameCount:000000}_{Time.time:0000.000}s.jpg");
                Destroy(tex);
                System.Threading.Tasks.Task.Run(() => { File.WriteAllBytes(path, ImageConversion.EncodeArrayToJPG(raw, fmt, (uint)w, (uint)h, 0, 92)); });
                FramesWritten++;
            }
        }

        void Frames(string row, float seconds)
        {
            capDir = Path.Combine(Folder, row); Directory.CreateDirectory(capDir);
            capUntilFrame = Time.frameCount + Mathf.CeilToInt(seconds * Mathf.Max(20f, 1f / Mathf.Max(0.005f, Time.smoothDeltaTime)));
            csv = new StringBuilder("frame,time,pitch,roll,lift,goal_pitch,crush_amount,turret,barrel_deg,deform_ms\n");
        }

        IEnumerator Run()
        {
            G = ZGame.I; W = G.World; rts = FindFirstObjectByType<RtsCamera>(); input = OzUnitEditor.I.Input;
            Directory.CreateDirectory(Folder);
            md.AppendLine($"# Bizon crush frames ({System.DateTime.Now:yyyy-MM-dd HH:mm}), the Unit editor, Game view {Screen.width} x {Screen.height}").AppendLine();
            md.AppendLine($"switches: Bizon {ZWorld.BizonDeviation}, crush {ZWorld.BizonCrushDeviation}; dents {OzCrushSquash.Deform}, turret fall {OzCrushSquash.TurretFall}, barrel bend {OzCrushSquash.BarrelBend}").AppendLine();
            OzUnitEditor.SetPanelsHidden(true);
            var me = G.player; var foe = me == Team.Red ? Team.Blue : Team.Red;
            foreach (var kind in kinds)
                foreach (var side in new[] { true, false })
                {
                    bool gun = kind.StartsWith("cannon:"); string type = gun ? kind.Substring(7) : kind;
                    string row = $"{type}_{(side ? "lowside" : "rts")}";
                    if (!Area(out int ox, out int oy)) { md.AppendLine($"- FAIL {row}: no free ground"); continue; }
                    int mx = ox + 3 * 16, my = oy + 6 * 16;
                    var m = Make(ZBigTanks.Bizon, mx, my, me);
                    int ax = mx + 6 * 16 + ZPathing.Radius(m) + 20 + 26;
                    var t = gun ? MakeCannon(type, ax, my, foe) : Make(type, ax, my, foe);
                    m.Damage = 0; t.Damage = 0;
                    var mid = ZGame.PxToWorld((mx + ax) * 0.5 + 24, my); mid.y = G.GroundY(mid.x, mid.z);
                    if (rts) { rts.enabled = true; rts.FlyTo(mid); rts.ZoomTo(side ? 46f : 52f); }
                    yield return new WaitForSeconds(2f);
                    var cam = Camera.main;
                    if (side && cam)
                    {   // the owner's low side view: the crush runs to the right across the screen
                        if (rts) rts.enabled = false;
                        cam.transform.SetPositionAndRotation(mid + new Vector3(0f, 2.1f, -15f), Quaternion.LookRotation((mid + Vector3.up * 1.3f) - (mid + new Vector3(0f, 2.1f, -15f))));
                    }
                    Deselect(); yield return null;
                    int b0 = OzCrushSquash.TurretBreaks, l0 = OzCrushSquash.TurretLandings, r0 = OzCrushSquash.TurretRests, p0 = OzCrushSquash.PiecesOff, d0 = OzCrushSquash.PartsDeformed,
                        u0 = OzCrushSquash.PartsUnreadable, bb0 = OzBizonCrushView.WreckBreaks;
                    OzCrushSquash.MaxDeformMs = 0f; OzCrushSquash.LastBarrelDeg = 0f; OzCrushSquash.LastTurretTravelM = 0f; OzCrushSquash.LastPressM = 0f; OzCrushSquash.LastBreakAfterTouchS = -1f; OzCrushSquash.LastTearWhy = "-"; OzCrushSquash.LastTurretSinkM = 0f;   // 1.5 N5
                    OzCrushRide.MaxPitchSeen = OzCrushRide.MaxRollSeen = OzCrushRide.MaxLiftSeen = 0f;
                    W.SendWaypoints(me, m, new[] { ZWaypoint.Move(mx + 22 * 16, my) });
                    Frames(row, 10f);
                    float until = Time.time + 20f; while (Time.time < until && !t.RamDoomed) { yield return null; }
                    var tv = ViewOf(t); watch = tv ? tv.GetComponent<OzCrushSquash>() : null;
                    float hitAt = Time.time, deathAt = -1f;
                    while (Time.time - hitAt < 9f && deathAt < 0f) { if (t.Destroyed || t.KillAt >= 0) deathAt = Time.time - hitAt; yield return null; }
                    yield return new WaitForSeconds(4f);
                    capDir = null;
                    if (csv != null) { File.WriteAllText(Path.Combine(Folder, row, "numbers.csv"), csv.ToString()); csv = null; }
                    Smoothness(out float revPerS, out float maxJerk, out float rollRev);
                    bool crushed = t.RamCrushed, dented = OzCrushSquash.PartsDeformed > d0;
                    md.AppendLine($"- {(crushed && dented ? "DONE" : "FAIL")} {row}: crushed {crushed}, destroyed {deathAt:0.00} s after the contact; parts dented {OzCrushSquash.PartsDeformed - d0}, not readable {OzCrushSquash.PartsUnreadable - u0}; "
                                  + $"deepest press {OzCrushSquash.LastPressM:0.00} m; turret torn {OzCrushSquash.TurretBreaks - b0} ({OzCrushSquash.LastBreakAfterTouchS:0.00} s after the first press: {OzCrushSquash.LastTearWhy}; pressed into the roof {OzCrushSquash.LastTurretSinkM:0.00} m before), travel {OzCrushSquash.LastTurretTravelM:0.0} m, "
                                  + $"landings {OzCrushSquash.TurretLandings - l0}, at rest {OzCrushSquash.TurretRests - r0} ({OzCrushSquash.LastTurretState}); barrel bend {OzCrushSquash.LastBarrelDeg:0} deg; pieces torn off {OzCrushSquash.PiecesOff - p0}; "
                                  + $"ride: pitch max {OzCrushRide.MaxPitchSeen:0.0}, roll max {OzCrushRide.MaxRollSeen:0.0} deg, lift max {OzCrushRide.MaxLiftSeen:0.00} m; pitch reversals {revPerS:0.0}/s (roll {rollRev:0.0}/s), "
                                  + $"largest pitch-rate change {maxJerk:0.00} deg/frame; dent pass max {OzCrushSquash.MaxDeformMs:0.0} ms; wreck kept {OzBizonCrushView.WreckBreaks - bb0 > 0 && OzCrushSquash.KeepsWreck(tv)}");
                    if (rts) rts.enabled = true;
                    OzUePlayground.Remove(m); OzUePlayground.Remove(t); watch = null;
                    yield return new WaitForSeconds(1.5f);
                }
            OzUnitEditor.SetPanelsHidden(false);
            md.AppendLine().AppendLine($"Frames written {FramesWritten}: {Folder}. Sheets: python make_sheets.py \"{Folder}\"");
            Report = md.ToString();
            File.WriteAllText(Path.Combine(Folder, "crush_frames_report.md"), Report);
            Debug.Log(Tag + "\n" + Report);
            yield return new WaitForSeconds(1f);
            Destroy(this);
        }

        /// <summary>The ride's smoothness from its trace: direction reversals of the pitch (and roll) per second where the rate on both sides is
        /// above 0.08 deg/frame, the largest change of the pitch's rate between frames.</summary>
        static void Smoothness(out float revPerS, out float maxJerk, out float rollRev)
        {
            revPerS = maxJerk = rollRev = 0f;
            var tr = OzCrushRide.Trace; if (tr.Count < 4) return;
            int rev = 0, rrev = 0;
            for (int i = 2; i < tr.Count; i++)
            {
                float d1 = tr[i - 1].y - tr[i - 2].y, d2 = tr[i].y - tr[i - 1].y;
                if (Mathf.Abs(d1) > 0.08f && Mathf.Abs(d2) > 0.08f && Mathf.Sign(d1) != Mathf.Sign(d2)) rev++;
                maxJerk = Mathf.Max(maxJerk, Mathf.Abs(d2 - d1));
                float r1 = tr[i - 1].z - tr[i - 2].z, r2 = tr[i].z - tr[i - 1].z;
                if (Mathf.Abs(r1) > 0.08f && Mathf.Abs(r2) > 0.08f && Mathf.Sign(r1) != Mathf.Sign(r2)) rrev++;
            }
            float span = Mathf.Max(0.1f, tr[tr.Count - 1].x - tr[0].x);
            revPerS = rev / span; rollRev = rrev / span;
        }

        ZObject Make(string type, int cx, int cy, Team t)
        {
            var o = W.CreateObject("vehicle", type, cx - 16, cy - 16, t); W.Emit(ZEventType.Created, o); Changed(); return o;
        }

        ZObject MakeCannon(string type, int cx, int cy, Team t)
        {
            var o = W.CreateObject("cannon", type, cx - 16, cy - 16, t); W.Emit(ZEventType.Created, o); Changed(); return o;
        }

        static void Changed() { typeof(OzUePlayground).GetMethod("MarkChanged", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)?.Invoke(null, null); }

        UnitView ViewOf(ZObject o) => o != null && G.Views.TryGetValue(o.Id, out var v) ? v : null;

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
                        int tx = cx + dx - 16, ty = cy + dy - 6;
                        if (Free(tx, ty, 32, 13)) { ox = tx * 16; oy = ty * 16; return true; }
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
    }
}
#endif
