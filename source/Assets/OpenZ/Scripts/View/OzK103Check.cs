#if !OZ_RELEASE
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// The counted checks of items 168 and 169, run in Play (a development path: compiled out of the release build, as every other check).
    /// <list type="bullet">
    /// <item><see cref="RunZoom"/> (item 168): the standing tank with scrolling belts nearest the screen centre (an owned one first) is put
    /// 28 % of the screen width right of the centre; the camera zooms in and out by wheel-sized steps and back (35..200 m); every frame the
    /// scroll each belt's draw uses (VehicleMotion.DrawnScroll) is read: it must not change at all (0), the tank must not move. It also
    /// counts the frames the belts were softened (View/OzTreadSteady.cs: must be some while the picture runs, 0 half a second after the
    /// zoom stops). Then, for an owned tank, a move order 20 m east: its belts' scroll must change (it still rolls when it drives).</item>
    /// <item><see cref="PlaceEmpties"/> (item 169): drops every tile of the testing overlay's EMPTY row (vehicles and guns) through the overlay's
    /// own drop code in a grid round the screen centre and lists each one's drawn facing (must be UnitView.SideYaw, 90).</item>
    /// <item><see cref="FacingReport"/>: the drawn facing of every unit turned so far.</item>
    /// </list>
    /// Results: <see cref="Report"/> and one log line each ("[K103] ...").
    /// </summary>
    public class OzK103Check : MonoBehaviour
    {
        public static string Report = "idle";
        static readonly List<Renderer> rs = new List<Renderer>();

        /// <summary>Starts the zoom check (about 12 s); the result lands in <see cref="Report"/>.</summary>
        public static void RunZoom()
        {
            Report = "running";
            var go = new GameObject("OzK103Check");
            var c = go.AddComponent<OzK103Check>();
            c.StartCoroutine(c.Zoom());
        }

        void Done(string s)
        {
            Report = s;
            Debug.Log("[K103] " + s);
            Destroy(gameObject);
        }

        static bool Belts(UnitView v, List<Renderer> into)
        {
            into.Clear();
            var vm = v ? v.GetComponent<VehicleMotion>() : null; if (!vm) return false;
            vm.TrackRenderers(into);
            for (int i = into.Count - 1; i >= 0; i--) if (!VehicleMotion.ScrollsBelt(into[i])) into.RemoveAt(i);
            return into.Count > 0;
        }

        IEnumerator Zoom()
        {
            var cam = Camera.main; var rts = cam ? cam.GetComponent<RtsCamera>() : null; if (!rts) rts = FindFirstObjectByType<RtsCamera>();
            var g = ZGame.I;
            if (!cam || !rts || g == null || g.World == null) { Done("ZOOM CHECK FAIL: no game camera / world"); yield break; }
            UnitView best = null; float bd = float.MaxValue;
            foreach (var v in FindObjectsByType<UnitView>(FindObjectsSortMode.None))
            {
                if (!v || v.Obj == null || v.Obj.Destroyed || v.Dying || v.Obj.Kind != ZKind.Vehicle || v.Obj.IsMoving || !Belts(v, rs)) continue;
                var s = cam.WorldToViewportPoint(v.transform.position); if (s.z <= 0f) continue;
                float d = (new Vector2(s.x, s.y) - new Vector2(0.5f, 0.5f)).sqrMagnitude + (v.Obj.Owner == Team.Neutral ? 10f : 0f);   // an owned one first (it can drive afterwards)
                if (d < bd) { bd = d; best = v; }
            }
            if (!best) { Done("ZOOM CHECK FAIL: no standing vehicle with scrolling belts (place a light tank v2 with P near the screen centre)"); yield break; }
            var o = best.Obj; var belts = new List<Renderer>(); Belts(best, belts);
            float h0 = rts.height; var f0 = rts.focus;
            // the tank 28 % of the screen width right of the centre (where the picture runs during a zoom), at the 70 m default
            rts.ZoomTo(70f);
            float span = 2f * (70f / Mathf.Sin(rts.pitch * Mathf.Deg2Rad)) * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * cam.aspect;
            var tp = best.transform.position;
            rts.focus = new Vector3(tp.x - 0.28f * span, 0f, tp.z);
            yield return new WaitForSecondsRealtime(1.5f);                                              // the camera settles; OzTreadSteady scans the belts (every second)
            int n = belts.Count;
            var lo = new float[n]; var hi = new float[n];
            for (int i = 0; i < n; i++) { lo[i] = float.MaxValue; hi[i] = float.MinValue; }
            int frames = 0, softFrames = 0, stoodFrames = 0; float hMin = float.MaxValue, hMax = float.MinValue, maxSoft = 0f, moved = 0f;
            var p0 = best.transform.position; float shoes0 = OzTreadSteady.MaxShoesPerFrame; OzTreadSteady.MaxShoesPerFrame = 0f;
            void Sample()
            {
                frames++;
                if (!best) return;
                if (!o.IsMoving) stoodFrames++;
                for (int i = 0; i < n; i++)
                {
                    if (!belts[i]) continue;
                    float w = VehicleMotion.DrawnScroll(belts[i]);
                    if (float.IsNaN(w)) continue;
                    lo[i] = Mathf.Min(lo[i], w); hi[i] = Mathf.Max(hi[i], w);
                    float k = OzTreadSteady.SoftOf(belts[i]); if (k > 0f) { maxSoft = Mathf.Max(maxSoft, k); }
                }
                bool soft = false; for (int i = 0; i < n; i++) if (belts[i] && OzTreadSteady.SoftOf(belts[i]) > 0f) soft = true;
                if (soft) softFrames++;
                hMin = Mathf.Min(hMin, rts.height); hMax = Mathf.Max(hMax, rts.height);
                moved = Mathf.Max(moved, (best.transform.position - p0).magnitude);
            }
            // wheel-sized steps: 6 in, a pause, 12 out, a pause, then back to 70 m, sampled every frame
            for (int s = 0; s < 6; s++) { rts.ZoomTo(rts.height * 0.85f); for (int k = 0; k < 2; k++) { yield return null; Sample(); } }
            for (float t = 0f; t < 0.6f; t += Time.unscaledDeltaTime) { yield return null; Sample(); }
            for (int s = 0; s < 12; s++) { rts.ZoomTo(rts.height / 0.85f); for (int k = 0; k < 2; k++) { yield return null; Sample(); } }
            for (float t = 0f; t < 0.6f; t += Time.unscaledDeltaTime) { yield return null; Sample(); }
            rts.ZoomTo(70f);
            for (float t = 0f; t < 1.2f; t += Time.unscaledDeltaTime) { yield return null; Sample(); }
            // at rest: the belts sharp again
            for (float t = 0f; t < 0.5f; t += Time.unscaledDeltaTime) yield return null;
            float restSoft = 0f; for (int i = 0; i < n; i++) if (belts[i]) restSoft = Mathf.Max(restSoft, OzTreadSteady.SoftOf(belts[i]));
            var sb = new StringBuilder();
            float change = 0f;
            for (int i = 0; i < n; i++) { float c = hi[i] >= lo[i] ? hi[i] - lo[i] : 0f; change = Mathf.Max(change, c); sb.Append(i == 0 ? "" : " / ").Append($"{lo[i]:0.000000}..{hi[i]:0.000000}"); }
            bool stood = stoodFrames == frames && moved < 0.01f;
            bool passA = stood && change == 0f;
            bool passB = softFrames > 0 && restSoft == 0f;
            string a = $"{o.Type} id {o.Id} ({o.Owner}), {n} belts, {frames} frames, camera {hMin:0}..{hMax:0} m: drawn scroll {sb} = change {change:0.000000} tiles " +
                       $"({(passA ? "PASS: 0" : stood ? "FAIL: it changed" : "INVALID: the tank moved or was ordered")}); tank moved {moved:0.0000} m; " +
                       $"belts softened in {softFrames} frames (most {maxSoft:0.00}; the picture ran up to {OzTreadSteady.MaxShoesPerFrame:0.00} shoes a frame), {restSoft:0.00} at rest ({(passB ? "PASS" : "FAIL")})";
            OzTreadSteady.MaxShoesPerFrame = Mathf.Max(shoes0, OzTreadSteady.MaxShoesPerFrame);
            // driving: an owned tank still rolls its tread
            string drive;
            if (o.Owner == Team.Neutral || !best) drive = "drive part SKIPPED (the tank is empty: place an owned light tank v2 to see it drive)";
            else
            {
                var s0 = new float[n]; for (int i = 0; i < n; i++) s0[i] = belts[i] ? VehicleMotion.DrawnScroll(belts[i]) : float.NaN;
                bool sent = g.World.SendWaypoints(o.Owner, o, new[] { new ZWaypoint { mode = WpMode.Move, x = o.CenterX + 160, y = o.CenterY } });
                for (float t = 0f; t < 3f; t += Time.unscaledDeltaTime) yield return null;
                float least = float.MaxValue; var ds = new StringBuilder();
                for (int i = 0; i < n; i++)
                {
                    float d = belts[i] ? Mathf.Abs(VehicleMotion.DrawnScroll(belts[i]) - s0[i]) : 0f; if (float.IsNaN(d)) d = 0f;
                    least = Mathf.Min(least, d); ds.Append(i == 0 ? "" : " / ").Append(d.ToString("0.000"));
                }
                drive = $"driving 20 m east (order sent {sent}): belt scroll changed {ds} tiles in 3 s ({(sent && least > 0.05f ? "PASS: it rolls" : "FAIL")})";
            }
            rts.focus = f0; rts.ZoomTo(h0);
            Done($"ZOOM CHECK {(passA && passB ? "PASS" : "FAIL")}: {a}; {drive}");
        }

        /// <summary>Drops every tile of the testing overlay's EMPTY row (its vehicles, then its guns) through the overlay's own drop code
        /// (UI/OzCheatSpawner.cs Spawn), six a row, 11 m apart, round the screen centre, and lists each one's drawn facing.</summary>
        public static string PlaceEmpties()
        {
            var sp = OzCheatSpawner.I; var g = ZGame.I; var cam = Camera.main;
            if (!sp || g == null || g.World == null || !cam) return Done2("PLACE EMPTIES FAIL: no testing overlay / world / camera (Play a level with the dev flag on)");
            const BindingFlags NS = BindingFlags.NonPublic | BindingFlags.Static, NI = BindingFlags.NonPublic | BindingFlags.Instance;
            var spawn = typeof(OzCheatSpawner).GetMethod("Spawn", NI, null, new[] { typeof(string), typeof(Team), typeof(Vector2Int) }, null);
            var veh = typeof(OzCheatSpawner).GetField("Vehicles", NS)?.GetValue(null) as string[];
            var guns = typeof(OzCheatSpawner).GetField("Guns", NS)?.GetValue(null) as string[];
            if (spawn == null || veh == null || guns == null) return Done2("PLACE EMPTIES FAIL: the overlay's drop code or tile lists were not found");
            if (!g.GroundPoint(cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f)), out var centre)) return Done2("PLACE EMPTIES FAIL: no ground at the screen centre");
            var all = new List<string>(veh); all.AddRange(guns);
            var sb = new StringBuilder(); int ok = 0, made = 0;
            const int Cols = 6; const float Step = 11f;
            for (int i = 0; i < all.Count; i++)
            {
                int col = i % Cols, row = i / Cols;
                var wp = centre + new Vector3((col - (Cols - 1) * 0.5f) * Step, 0f, (1 - row) * Step);
                int n0 = g.World.Objects.Count;
                spawn.Invoke(sp, new object[] { all[i], Team.Neutral, ZGame.WorldToPx(wp) });
                ZObject o = null;
                for (int k = g.World.Objects.Count - 1; k >= n0; k--) { var x = g.World.Objects[k]; if (x != null && (x.Kind == ZKind.Vehicle || x.Kind == ZKind.Cannon)) { o = x; break; } }
                if (o == null) { sb.Append($"{all[i]}: not placed; "); continue; }
                made++;
                float yaw = g.Views.TryGetValue(o.Id, out var v) && v ? v.DrawnYaw : float.NaN;
                bool good = !float.IsNaN(yaw) && Mathf.Abs(Mathf.DeltaAngle(yaw, UnitView.SideYaw)) < 0.5f && UnitView.SideFacedIds.Contains(o.Id);
                if (good) ok++;
                sb.Append($"{all[i]} ({o.Type} id {o.Id}): {yaw:0.0} deg{(good ? "" : " WRONG")}; ");
            }
            return Done2($"PLACE EMPTIES {(ok == all.Count ? "PASS" : "FAIL")}: {made} of {all.Count} tiles placed, {ok} face {UnitView.SideYaw:0} deg (the screen's right): {sb}");
        }

        /// <summary>The drawn facing now of every unit turned to the side so far.</summary>
        public static string FacingReport()
        {
            var g = ZGame.I; var sb = new StringBuilder(); int n = 0;
            if (g != null) foreach (var id in UnitView.SideFacedIds)
                    if (g.Views.TryGetValue(id, out var v) && v && v.Obj != null) { n++; sb.Append($"{v.Obj.Type} id {id} {v.Obj.Owner} {v.DrawnYaw:0.0} deg{(v.Obj.IsMoving ? " (moving)" : "")}; "); }
            return Done2($"FACING: {n} units turned to the side: {sb}");
        }

        static string Done2(string s) { Report = s; Debug.Log("[K103] " + s); return s; }
    }
}
#endif
