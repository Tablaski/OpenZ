#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// tanks' hull pitch in the Unit editor (editor only; menu OpenZ/1.5/Checks/Big Tank Pitch Frames, Editor/OzV15BigTankPitchFramesMenu.cs).
    /// For the Mammoth, the Bizon and the heavy tank (the model the Unit editor makes: v2 by default) it stages, each from the owner's LOW SIDE VIEW
    /// (camera 2.1 m up, 15 m beside the tank, following it) and from the RTS camera (following it at the owner's zoom):
    /// <list type="bullet">
    /// <item>up: from the floor 10 m before a medium ramp platform's foot up the 18 deg ramp over its crest onto the deck;</item>
    /// <item>down: from the deck over the crest, down the ramp, over its foot onto the floor;</item>
    /// <item>side (the owner's frame 26): on the floor along the platform's side wall, close enough that a belt lies over the wall's foot;</item>
    /// </list>
    /// and for the Mammoth two rams on a medium tank: ram_flat (the floor) and ram_slope (the target half way up the ramp, the Mammoth from the
    /// floor below, uphill), each followed 4 s after the ram (never left tilted).
    /// The game runs frame-locked (<see cref="CaptureFps"/>); EVERY 2nd frame is saved (JPEG, 960 wide) to folder/&lt;row&gt;/ and EVERY frame's
    /// numbers to numbers.csv: the drawn track pitch (the belts' mean, + = front up), the hull's pitch, the planting's target, the GROUND's pitch
    /// under the tank measured here independently (the best-fit line of the upper envelope of the ground along both belts over the real contact
    /// length, a platform wall between the centre and a sample keeping the level), the errors, the frame-to-frame step of the track pitch, the
    /// roll, the deepest real track sample in the ground and the air under the tracks. Report folder/big_tank_pitch_report.md (per row: max
    /// |track - ground|, max |hull - ground|, the largest step, sink, air; after a ram: the error over its last 2 s). Sheets: python
    /// make_sheets.py "&lt;folder&gt;". Staged units / platforms removed after
    /// each row.
    /// </summary>
    public class OzBigTankPitchFramesCheck : MonoBehaviour
    {
        public const string Tag = "[OpenZ 1.5 Big Tank Pitch Frames]";
        public static bool Running { get; private set; }
        public static string Folder = "", Report = "no run yet";
        public static int FramesWritten;
        public static int CaptureFps = 30, ShotWidth = 960;
        public static readonly string[] Tanks = { ZBigTanks.Mammoth, ZBigTanks.Bizon, OzHeavyTank.Type };
        public static readonly string[] Drives = { "up", "down", "side" };
        /// <summary>Row limits: track pitch off the ground (deg), hull off the ground (deg: + its drive wobble / rock), the largest frame-to-frame step
        /// of the track pitch (deg at CaptureFps), the deepest real track sample in the ground (m), the most air under a track (m), after a ram (deg).</summary>
        public static float MaxErrDeg = 3f, MaxHullErrDeg = 4.5f, MaxStepDeg = 1.5f, MaxSinkM = 0.05f, MaxAirM = 0.15f, MaxAfterRamDeg = 1f;

        ZGame G; ZWorld W; RtsCamera rts; PlayerInput input;
        readonly StringBuilder md = new StringBuilder();
        int pass, fail;
        // the capture
        string capDir; StringBuilder csv; bool capOn, sideCam; UnitView watchV; ZObject watchO; Vector3 sideDir;
        float prevTrack = float.NaN, maxErr, maxHullErr, maxStep, maxSink, maxAir, ramEndAt = -1f, maxAfterRam; int frames;

        public static void Begin(string folder)
        {
            if (Running || !ZGame.I || ZGame.I.World == null || !OzUnitEditor.Active) { Debug.LogWarning(Tag + " open the Unit editor in Play first"); return; }
            if (ZGame.I.GetComponent<OzBigTankPitchFramesCheck>()) { Debug.LogWarning(Tag + " already running"); return; }
            Running = true; Folder = folder; ZGame.I.gameObject.AddComponent<OzBigTankPitchFramesCheck>();
        }

        void Start() { Running = true; StartCoroutine(Run()); StartCoroutine(Capture()); }
        void OnDestroy() { Running = false; if (rts) rts.enabled = true; Time.captureFramerate = 0; }

        static string F(float v, string f = "0.00") => float.IsNaN(v) ? "" : v.ToString(f, CultureInfo.InvariantCulture);

        void Update()
        {   // the cameras follow the watched tank
            if (!capOn || !watchV) return;
            var p = watchV.transform.position;
            if (sideCam) { var cam = Camera.main; if (cam) { var cp = p + sideDir * 15f + Vector3.up * 2.1f; cam.transform.SetPositionAndRotation(cp, Quaternion.LookRotation((p + Vector3.up * 1.3f) - cp)); } }
            else if (rts) rts.focus = new Vector3(p.x, rts.focus.y, p.z);
        }

        IEnumerator Capture()
        {
            var eof = new WaitForEndOfFrame();
            Texture2D small = null; RenderTexture rt = null;
            while (true)
            {
                yield return eof;
                if (!capOn || capDir == null || !watchV || watchO == null) continue;
                Numbers();
                if ((Time.frameCount & 1) != 0) continue;
                var tex = ScreenCapture.CaptureScreenshotAsTexture();
                if (!tex) continue;
                int w = Mathf.Min(ShotWidth, tex.width), h = Mathf.Max(2, Mathf.RoundToInt(tex.height * (w / (float)tex.width)));
                if (!rt || rt.width != w || rt.height != h) { if (rt) rt.Release(); rt = new RenderTexture(w, h, 0); small = new Texture2D(w, h, TextureFormat.RGB24, false); }
                Graphics.Blit(tex, rt); Destroy(tex);
                var was = RenderTexture.active; RenderTexture.active = rt; small.ReadPixels(new Rect(0, 0, w, h), 0, 0); small.Apply(false); RenderTexture.active = was;
                File.WriteAllBytes(Path.Combine(capDir, $"f{Time.frameCount:000000}_{F(Time.time, "0000.000")}s.jpg"), small.EncodeToJPG(85));
                FramesWritten++;
            }
        }

        /// <summary>This frame's numbers of the watched tank (one csv line).</summary>
        void Numbers()
        {
            var v = watchV; if (!v.ProofBelts(out var bp, out float roll, out _, out _, out _)) return;
            float track = 0.5f * (bp[0] + bp[1]);
            var front = v.transform.TransformDirection(Vector3.back);                                   // the model's front is -z
            float hull = Mathf.Asin(Mathf.Clamp(front.normalized.y, -1f, 1f)) * Mathf.Rad2Deg;
            float ground = GroundPitch(v, front);
            v.BigProofContact(out float sink, out float air);
            float err = track - ground, herr = hull - ground, step = float.IsNaN(prevTrack) ? 0f : Mathf.Abs(track - prevTrack);
            prevTrack = track; frames++;
            if (!float.IsNaN(ground)) { maxErr = Mathf.Max(maxErr, Mathf.Abs(err)); maxHullErr = Mathf.Max(maxHullErr, Mathf.Abs(herr)); }
            maxStep = Mathf.Max(maxStep, step); maxSink = Mathf.Max(maxSink, sink); maxAir = Mathf.Max(maxAir, air);
            bool ram = watchO.Ramming;
            if (ramEndAt >= 0f && Time.time - ramEndAt > 2f && !float.IsNaN(ground)) maxAfterRam = Mathf.Max(maxAfterRam, Mathf.Abs(err));
            csv?.AppendLine(string.Join(",", Time.frameCount.ToString(), F(Time.time, "0.000"), F(track), F(hull), F(v.BigPitchTargetDeg), F(ground), F(err), F(herr), F(step), F(roll),
                                        F(sink, "0.000"), F(air, "0.000"), F(v.HeavyInertiaNow), ram ? "ram" : ramEndAt >= 0f ? "after" : "drive"));
        }

        /// <summary>The ground's pitch under the tank (deg, + = rising toward its front), measured here: along both belts (the half gauge
        /// beside the centre) over the real contact length, 41 samples each, a platform wall between the centre and a sample keeping the level;
        /// the best-fit line of the upper envelope of each; the mean of the two. NaN = no contact numbers.</summary>
        float GroundPitch(UnitView v, Vector3 front)
        {
            float s = Mathf.Abs(v.transform.lossyScale.x); if (s < 1e-4f) s = 1f;
            float L = v.BigContactHalfM * s, gauge = Mathf.Max(0f, v.BigGaugeHalfM) * s;
            if (L <= 0.2f) return float.NaN;
            var f = new Vector3(front.x, 0f, front.z); if (f.sqrMagnitude < 1e-6f) return float.NaN; f.Normalize();
            var r = Vector3.Cross(Vector3.up, f); var c = v.transform.position;
            const int N = 41; var z = new float[N]; var g = new float[N]; var hz = new float[N]; var hg = new float[N];
            float sum = 0f; int sides = 0;
            foreach (float side in new[] { -1f, 1f })
            {
                var o = c + r * (side * gauge); float g0 = G.GroundY(o.x, o.z);
                for (int half = 0; half < 2; half++)
                {   // from the middle out to each end, so a wall keeps the level the middle stands on
                    float prev = g0;
                    for (int k = 0; k <= N / 2; k++)
                    {
                        int j = half == 0 ? N / 2 + k : N / 2 - k;
                        float u = (j - N / 2) / (float)(N / 2) * L;
                        var p = o + f * u; float h = G.GroundY(p.x, p.z);
                        if (Mathf.Abs(h - prev) > 0.15f && W.PlatformHeightM(p.x * 8.0, -p.z * 8.0) + W.PlatformHeightM(o.x * 8.0, -o.z * 8.0) > 0.0) h = prev; else prev = h;
                        z[j] = -u; g[j] = h;                                                              // FitPitch's z runs toward the rear (+z = the model's back)
                    }
                }
                System.Array.Reverse(z); System.Array.Reverse(g);                                        // ascending z
                sum += BigTankPitch.FitPitch(z, g, null, N, hz, hg); sides++;                            // the Euler x angle of the fitted line: + = rising toward the front
            }
            return sides > 0 ? sum / sides : float.NaN;
        }

        IEnumerator Run()
        {
            G = ZGame.I; W = G.World; rts = FindFirstObjectByType<RtsCamera>(); input = OzUnitEditor.I.Input;
            Directory.CreateDirectory(Folder);
            md.AppendLine($"# Big tank pitch frames ({System.DateTime.Now:yyyy-MM-dd HH:mm}), the Unit editor, Game view {Screen.width} x {Screen.height}, {CaptureFps} fps frame-locked, every 2nd frame saved").AppendLine();
            md.AppendLine($"switches: BigTankPitch.On {BigTankPitch.On}, ProfileOn {BigTankPitch.ProfileOn}, spring {BigTankPitch.Hz} Hz (lag <= {BigTankPitch.MaxLagDeg} deg), wall step {BigTankPitch.WallStepM} m; "
                          + $"TrackGround {TrackGround.Enabled}, N8 slope {TrackGroundSlope.On}, inertia {OzHeavyInertia.On}; ram: Mammoth {ZWorld.MammothRamDeviation}, polish {ZWorld.RamPolishDeviation}, lever {OzRamLever.On}").AppendLine();
            md.AppendLine($"limits: |track - ground| <= {MaxErrDeg} deg, |hull - ground| <= {MaxHullErrDeg} deg (its wobble / shot rock / inertia on top), step <= {MaxStepDeg} deg a frame, sink <= {MaxSinkM * 100f:0} cm (not in 'side': the drawn tank overlaps the platform's body there), air <= {MaxAirM * 100f:0} cm, 2 s after a ram <= {MaxAfterRamDeg} deg").AppendLine();
            OzUnitEditor.SetPanelsHidden(true);
            BigTankPitch.ResetStats();
            var me = G.player; var foe = me == Team.Red ? Team.Blue : Team.Red;
            var rows = new List<(string tank, string what)>();
            foreach (var t in Tanks) foreach (var d in Drives) rows.Add((t, d));
            rows.Add((ZBigTanks.Mammoth, "ram_flat")); rows.Add((ZBigTanks.Mammoth, "ram_slope"));
            foreach (var (tank, what) in rows)
                foreach (var side in new[] { true, false })
                {
                    string row = $"{what}_{tank}_{(side ? "lowside" : "rts")}";
                    ZObject plat = null, m = null, t = null; ZWorld.PlatformInfo p = null; Vector3 mid;
                    int gx, gy;
                    if (what == "ram_flat")
                    {
                        if (!Area(out int ox, out int oy, 30, 12)) { Row(row, false, "no free ground near the camera"); continue; }
                        int mx = ox + 3 * 16, my = oy + 6 * 16;
                        m = Make(tank, mx, my, me);
                        t = Make(OzMediumTank.Type, mx + 6 * 16 + ZPathing.Radius(m) + 46, my, foe);
                        gx = mx + 24 * 16; gy = my;
                    }
                    else
                    {
                        if (!PlatformArea(out int tx, out int ty)) { Row(row, false, "no free ground for a medium platform (10 x 16 tiles + 6 behind + 8 beyond its foot) near the camera"); continue; }
                        plat = W.PlacePlatform(tx, ty, ZWorld.PlatformMid, 0, -1, out string why);
                        if (plat == null) { Row(row, false, "the platform could not be placed: " + why); continue; }
                        OzUePlatformViews.Add(plat); OzUePlayground.MarkChanged(); Physics.SyncTransforms();
                        p = W.PlatformOf(plat.Id);
                        double a0 = what == "side" ? -0.5 : 5.0;                                          // across (tiles): the side row hugs the left wall's foot
                        double l0 = what == "down" ? 3.0 : 21.0, l1 = what == "down" ? 23.0 : what == "side" ? 2.0 : 3.0;
                        At(p, a0, l0, out int mx, out int my); At(p, a0, l1, out gx, out gy);
                        m = Make(tank, mx, my, me);
                        if (what == "ram_slope") { At(p, 5.0, 12.4, out int ax, out int ay); t = Make(OzMediumTank.Type, ax, ay, foe); }
                    }
                    m.Damage = 0; if (t != null) t.Damage = 0;
                    yield return null;
                    var mv = ViewOf(m);
                    var travel = (ZGame.PxToWorld(gx, gy) - ZGame.PxToWorld(m.CenterX, m.CenterY)); travel.y = 0f; travel = travel.sqrMagnitude > 1e-4f ? travel.normalized : Vector3.forward;
                    mid = mv ? mv.transform.position : ZGame.PxToWorld(m.CenterX, m.CenterY);
                    if (rts) { rts.enabled = true; rts.FlyTo(mid); rts.ZoomTo(side ? 46f : 52f); }
                    yield return new WaitForSeconds(2f);
                    sideCam = side; sideDir = Vector3.Cross(Vector3.up, travel).normalized; if (what == "side") sideDir = -sideDir;   // the side row: the camera on the open side, the wall behind the tank
                    if (side && rts) rts.enabled = false;
                    if (input) input.Selection.Clear();
                    // the capture, frame-locked, from the order
                    capDir = Path.Combine(Folder, row); Directory.CreateDirectory(capDir);
                    csv = new StringBuilder("frame,time,track_pitch,hull_pitch,target_pitch,ground_pitch,track_err,hull_err,step,roll,sink_m,air_m,inertia_deg,state\n");
                    watchO = m; watchV = mv; prevTrack = float.NaN; maxErr = maxHullErr = maxStep = maxSink = maxAir = maxAfterRam = 0f; ramEndAt = -1f; frames = 0;
                    Time.captureFramerate = CaptureFps; capOn = true;
                    W.SendWaypoints(me, m, new[] { ZWaypoint.Move(gx, gy) });
                    bool rammed = false, moved = false; float stillFor = 0f, until = Time.time + 45f, endAt = -1f;
                    var last = new Vector2(m.CenterX, m.CenterY);
                    while (Time.time < until)
                    {
                        yield return null;
                        if (!watchV) watchV = ViewOf(m);
                        if (m.Ramming) rammed = true;
                        if (rammed && !m.Ramming && ramEndAt < 0f) { ramEndAt = Time.time; endAt = Time.time + 4f; }
                        var now = new Vector2(m.CenterX, m.CenterY);
                        if ((now - last).sqrMagnitude > 0.5f) { moved = true; stillFor = 0f; last = now; } else stillFor += Time.deltaTime;
                        if (endAt < 0f && moved && stillFor > 1.5f && !m.Ramming) endAt = Time.time + 1f;
                        if (endAt >= 0f && Time.time >= endAt) break;
                    }
                    capOn = false; Time.captureFramerate = 0;
                    File.WriteAllText(Path.Combine(capDir, "numbers.csv"), csv.ToString()); csv = null;
                    bool ramRow = what.StartsWith("ram");
                    bool ok = moved && frames > 10 && maxErr <= MaxErrDeg && maxHullErr <= MaxHullErrDeg && maxStep <= MaxStepDeg && maxAir <= MaxAirM
                              && (what == "side" || maxSink <= MaxSinkM) && (!ramRow || (rammed && maxAfterRam <= MaxAfterRamDeg));
                    Row(row, ok, $"{m.Type} {(moved ? "drove" : "did NOT move")} ({frames} frames{(ramRow ? rammed ? ", rammed" : ", NO ram" : "")}): max |track - ground| {F(maxErr, "0.0")} deg, max |hull - ground| {F(maxHullErr, "0.0")} deg, "
                        + $"largest step {F(maxStep, "0.00")} deg/frame, deepest real track sample {F(maxSink * 100f, "0.0")} cm, most air {F(maxAir * 100f, "0.0")} cm{(ramRow ? $", 2+ s after the ram {F(maxAfterRam, "0.0")} deg" : "")}; "
                        + $"walls kept {BigTankPitch.Walls}, lead samples {BigTankPitch.LungeSamples}, profile belts {BigTankPitch.ProfileBelts}, largest lag {F(BigTankPitch.MaxLagSeen, "0.0")} deg");
                    Tidy(m, t, plat, mid);
                    yield return new WaitForSeconds(1.5f);
                }
            OzUnitEditor.SetPanelsHidden(false);
            md.AppendLine().AppendLine($"{pass} PASS, {fail} FAIL. Frames written {FramesWritten}: {Folder}");
            md.AppendLine($"Sheets (every 2nd frame, side + RTS, with the numbers): python make_sheets.py \"{Folder}\"");
            Report = md.ToString();
            File.WriteAllText(Path.Combine(Folder, "big_tank_pitch_report.md"), Report);
            Debug.Log(Tag + "\n" + Report);
            yield return new WaitForSeconds(0.5f);
            Destroy(this);
        }

        void Tidy(ZObject m, ZObject t, ZObject plat, Vector3 mid)
        {
            capOn = false; Time.captureFramerate = 0; capDir = null; csv = null; watchV = null; watchO = null;
            if (rts) rts.enabled = true;
            if (m != null) OzUePlayground.Remove(m);
            if (t != null) OzUePlayground.Remove(t);
            if (plat != null) OzUePlayground.Remove(plat);
            OzUePlayground.SweepDebrisIn(q => (q - mid).sqrMagnitude < 60f * 60f);
        }

        void Row(string row, bool ok, string what)
        {
            md.AppendLine($"- {(ok ? "PASS" : "FAIL")} {row}: {what}");
            if (ok) pass++; else fail++;
        }

        ZObject Make(string type, int cx, int cy, Team team)
        {
            var o = W.CreateObject("vehicle", type, cx - 16, cy - 16, team); W.Emit(ZEventType.Created, o); OzUePlayground.MarkChanged(); return o;
        }

        static void At(ZWorld.PlatformInfo p, double across, double along, out int px, out int py)
        {
            p.Map(across, along, out double x, out double y); px = (int)System.Math.Round(x); py = (int)System.Math.Round(y);
        }

        UnitView ViewOf(ZObject o) => o != null && G.Views.TryGetValue(o.Id, out var v) ? v : null;

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

        /// <summary>A medium platform's place (ramp foot toward +ty): its tiles free, plus 6 free rows behind its back wall, 9 beyond its foot and
        /// 2 columns each side (the side row drives there).</summary>
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
                        if (!Free(x - 3, y - 6, wide + 6, deep + 15)) continue;
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
