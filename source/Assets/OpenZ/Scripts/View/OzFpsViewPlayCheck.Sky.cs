#if !OZ_RELEASE
// OpenZ (GPL-3.0) - version 1.4 kit fps_view_sky: the short Play check (menu OpenZ/V1.4/FPS View: Sky + Gun + Fire Check (short)), about 3 minutes,
// the match goes on. Rows: (1) every listed type's eye over fps_view_fix's eye (EyeLift) and its gun copy's own pixels <= 15 % of the screen;
// (2) the sky in the view: Weight reaches 1, the dome drawn, the panorama (or the plain map) resident and its MB, the sun disc on screen when
// looking toward the sun by day, no disc at night and a darker dome; after Esc Weight 0 within 1.5 s and the map unloaded (LastUnload);
// (3) the leave: L02 at night, a selected own light tank with its headlights on, the game still: every light, unit renderer, the camera and the
// globals the view touches compared before the entry and after the leave, and the still RTS frame before = after (pixels); (4) left click:
// on an enemy an attack order and shots, held = repeated shots, on the ground no order, never a selection change; (5) a flyby's sky after the
// round trip (ForceOn, the flybys' path): its own low sun (VisualSun), FpsMode off. No row passes on 0 samples.
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace OpenZ
{
    public sealed partial class OzFpsViewPlayCheck
    {
        bool skyRun;
        /// <summary>fps_view_sky: the short check (see the file's head).</summary>
        public static void RunSky() { Report = "running"; var c = new GameObject("OzFpsViewPlayCheck").AddComponent<OzFpsViewPlayCheck>(); c.skyRun = true; }
        static readonly (string cat, string type)[] SkyTypes = { ("robot", "grunt"), ("robot", "sniper"), ("robot", "tough"), ("robot", "laser"), ("vehicle", "light"), ("vehicle", "jeep"), ("vehicle", "mtank"), ("vehicle", "heavy"), ("cannon", "gun") };

        IEnumerator SkyAll()
        {
            g = ZGame.I; W = g ? g.World : null; pi = FindFirstObjectByType<PlayerInput>(); rts = pi ? pi.cam : null; hud = OpenZHud.I; cam = rts ? rts.GetComponent<Camera>() : null;
            if (W == null || !rts || !pi || !cam) { Log("no level in Play"); fail++; Finish(); yield break; }
            Directory.CreateDirectory(Folder());
            me = g.player; foe = me == Team.Red ? Team.Blue : Team.Red;
            yield return WaitFor(() => OpenZHud.ControlAt > 0f && OzMotion.Now >= OpenZHud.ControlAt + 1f, 40f);
            W.Bots.Clear();
            startIds = new HashSet<int>(W.UnitObjects.Where(o => o != null).Select(o => o.Id));
            string lv = g.level;
            Log($"SKY CHECK level {lv}, game speed {g.gameSpeed:0.00}, RTS height {rts.height:0.0}, field of view {cam.fieldOfView:0}; the view's sky here: {FlybySky.FpsSkyChoice(lv)}");
            yield return WaitFor(() => Application.isFocused, 10f);
            Check($"the Unity window has the focus: {Application.isFocused}", Application.isFocused);
            Devices(true);
            if (OzFpsView.On && OzFpsView.I) OzFpsView.I.Leave("the check starts");
            var dn = FindAnyObjectByType<DayNight>(); float t0 = dn ? dn.timeOfDay : 0f; bool run0 = dn && dn.running;
            var sky = FlybySky.I;
            Check($"the RTS camera before the view: no sky (Weight {(sky ? sky.Weight : -1f):0.00}, first-person sky {(sky && sky.FpsMode)}, map resident {(sky ? sky.ResidentMB : -1f):0.0} MB)", sky && sky.Weight <= 0.001f && !sky.FpsMode && sky.ResidentMB <= 0f);

            // ---- (1) eye and gun copy per type
            foreach (var (cat, type) in SkyTypes) yield return EyeGunRow(cat, type);

            // ---- (2) the sky in the view, day and night, and its unload
            yield return SkyRow(dn);

            yield return LeaveRow(dn);

            // ---- (4) left click fires
            yield return FireRow();

            // ---- (5) a flyby's own sun after the round trip
            yield return FlybyRow();

            if (dn) { dn.timeOfDay = t0; dn.running = run0; }
            Clean();
            if (errors > 0) Check($"no error or exception logged during the run ({errors}: {string.Join(" | ", errorLines)})", false);
            Finish();
        }

        IEnumerator Leave1()
        {
            if (!OzFpsView.On) yield break;
            yield return Tap(Key.Escape); yield return Frames(3);
        }

        // ------------------------------------------------------------------ (1)
        IEnumerator EyeGunRow(string cat, string type)
        {
            if (!FindSpot(out int x, out int y, cat != "robot")) { Check($"{type}: a free place near the camera ({LastSpotWhy})", false); yield break; }
            rts.focus = ZGame.PxToWorld(x, y); yield return Real(0.4f);
            var u = Make(cat, type, x, y, me); yield return Real(1.2f);
            var v = ViewOf(u);
            if (u == null || !v) { Check(type + ": made with its view", false); yield break; }
            var b = Snap(v);
            yield return Enter(type, u, b);
            var fv = OzFpsView.I; if (!OzFpsView.On || !fv) yield break;
            yield return Real(0.5f);
            var cp = cam.transform.position; float ground = g.GroundY(cp.x, cp.z), lift = cp.y - fv.EyeBase.y;
            Check($"{type}: the eye {lift:0.00} m over fps_view_fix's eye (wanted {fv.EyeLift:0.00}: 0.2 robots, 0.3 vehicles and guns), now {cp.y - ground:0.00} m over the ground (was {fv.EyeBase.y - ground:0.00})",
                  Mathf.Abs(lift - fv.EyeLift) < 0.08f && lift > 0.1f && lift < 0.45f);
            float share = -1f, lum = 0f; int n = 0;
            yield return GunPixels(fv, (s, l, k) => { share = s; lum = l; n = k; });
            Check($"{type}: the gun copy ({fv.GunRootName}, {fv.GunParts} parts{(fv.GunCoverBox0 >= 0f ? $", fitted {fv.GunFit:0.00}, its boxes {fv.GunCoverBox0 * 100f:0.0} -> {fv.GunCoverBox * 100f:0.0} %" : "")}) covers {share * 100f:0.0} % of the screen by its own pixels ({n} samples; at most 15 %), its box x {gunPixBox.xMin:0.00}-{gunPixBox.xMax:0.00} y {gunPixBox.yMin:0.00}-{gunPixBox.yMax:0.00}",
                  n > 20 && share > 0f && share <= 0.15f);
            yield return new WaitForEndOfFrame(); var gt = ScreenCapture.CaptureScreenshotAsTexture(); try { File.WriteAllBytes(Path.Combine(Folder(), "fps_sky_gun_" + type + ".png"), gt.EncodeToPNG()); } catch (IOException) { } Destroy(gt); yield return Frames(3);   // U1 (check only): the picture as the screen shows it (CaptureScreenshot to a file left the gun copy out)
            yield return Leave1();
            Check($"{type}: Esc leaves ({OzFpsView.LastLeave})", !OzFpsView.On);
            yield return Real(0.3f);
        }

        // ------------------------------------------------------------------ (2)
        IEnumerator SkyRow(DayNight dn)
        {
            var sky = FlybySky.I; if (!sky) { Check("FlybySky is booted", false); yield break; }
            if (!FindSpot(out int x, out int y)) { Check($"sky: a free place ({LastSpotWhy})", false); yield break; }
            rts.focus = ZGame.PxToWorld(x, y); yield return Real(0.4f);
            var u = Make("robot", "grunt", x, y, me); yield return Real(1.2f);
            var v = ViewOf(u); if (u == null || !v) { Check("sky: a grunt made", false); yield break; }
            // a time of day with the sun 8 - 50 deg up (the view looks at most 60 deg up)
            float day = -1f;
            if (dn)
            {
                dn.running = false;
                foreach (float tt in new[] { 0.25f, 0.2f, 0.3f, 0.15f, 0.33f, 0.1f, 0.05f })
                {
                    dn.timeOfDay = tt; yield return Real(0.6f);
                    var sn = RenderSettings.sun ? RenderSettings.sun : dn.sun; if (!sn) break;
                    float el = Mathf.Asin(Mathf.Clamp(-sn.transform.forward.y, -1f, 1f)) * Mathf.Rad2Deg;
                    if (el >= 8f && el <= 50f) { day = tt; break; }
                }
            }
            Log($"INFO sky: day time {day:0.00} (DayNight's clock; -1 = none found with the sun 8 - 50 deg up)");
            int f0 = FlybySky.FpsDrawFrames;
            yield return Enter("sky grunt", u, Snap(v));
            var fv = OzFpsView.I; if (!OzFpsView.On || !fv) yield break;
            float tIn = Time.unscaledTime;
            yield return WaitFor(() => sky.Weight >= 0.999f, 4f);
            float tFull = Time.unscaledTime - tIn, mb = sky.ResidentMB;
            Check($"sky in the view: Weight {sky.Weight:0.00} after {tFull:0.00} s (load + the {FlybySky.FpsFadeSeconds:0.0} s fade), first-person sky {sky.FpsMode}, the dome drawn {FlybySky.FpsDrawFrames - f0} frames, map resident {mb:0.0} MB{(sky.PlainSky ? " (the plain gradient map)" : "")}",
                  sky.Weight >= 0.999f && sky.FpsMode && FlybySky.FpsDrawFrames - f0 > 5 && mb > 0f);
            Log($"INFO sky: {FlybySky.Status()}");
            Vector4 fog = OzFpsView.FogUnderNow;
            Log($"INFO sky: the MAYHEM wall's global in the view {fog} (x 1 = on; the fog's pass draws after the dome; the full check's fog row is the proof under MAYHEM)");
            // the sun by day
            var sun = RenderSettings.sun ? RenderSettings.sun : dn ? dn.sun : null;
            if (sun && day >= 0f)
            {
                Vector3 sd = -sun.transform.forward; float el = Mathf.Asin(Mathf.Clamp(sd.y, -1f, 1f)) * Mathf.Rad2Deg;
                yield return Aim(fv, cam.transform.position + sd * 300f, 0f); yield return Real(0.4f);
                var vp = cam.WorldToViewportPoint(cam.transform.position + sd * 300f);
                var sdir = Shader.GetGlobalVector("_FlybySunDir");
                yield return new WaitForEndOfFrame(); var tex = ScreenCapture.CaptureScreenshotAsTexture();
                float lumSun = Lum(tex, vp), lumSide = Lum(tex, new Vector3(vp.x + 0.25f, vp.y, 1f));
                try { File.WriteAllBytes(Path.Combine(Folder(), "fps_sky_sun_day.png"), tex.EncodeToPNG()); } catch (IOException) { }
                Destroy(tex);
                Check($"sky by day: the sun disc on the key light's real direction ({el:0.0} deg up; the drawn direction {(Vector3)sdir:F2}, strength {sdir.w:0.00}) on the screen at ({vp.x:0.00}, {vp.y:0.00}), brightness there {lumSun:0.00} against {lumSide:0.00} beside it",
                      vp.z > 0f && vp.x > 0.05f && vp.x < 0.95f && vp.y > 0.05f && vp.y < 0.95f && sdir.w > 0.05f && Vector3.Angle(sdir, sd) < 1f && lumSun > lumSide + 0.05f);
            }
            else Check("sky by day: a sun 8 - 50 deg up to look at (none: no key light or no such time on DayNight's clock)", false);
            // night: no disc, a darker dome
            float topDay = -1f, topNight = -1f;
            if (dn)
            {
                yield return Aim(fv, cam.transform.position + new Vector3(1f, 0.35f, 0.3f) * 100f, 0f);
                yield return new WaitForEndOfFrame(); var a = ScreenCapture.CaptureScreenshotAsTexture(); topDay = TopLum(a); Destroy(a);
                dn.timeOfDay = 0.66f; yield return Real(1.2f);
                var sdir = Shader.GetGlobalVector("_FlybySunDir");
                yield return new WaitForEndOfFrame(); var b = ScreenCapture.CaptureScreenshotAsTexture(); topNight = TopLum(b);
                try { File.WriteAllBytes(Path.Combine(Folder(), "fps_sky_night.png"), b.EncodeToPNG()); } catch (IOException) { }
                Destroy(b);
                Check($"sky at night (DayNight 0.66): no sun disc (strength {sdir.w:0.00}), the dome still drawn (Weight {sky.Weight:0.00}), the sky's upper third {topNight:0.00} against {topDay:0.00} by day (darker)",
                      sdir.w <= 0.001f && sky.Weight >= 0.999f && topDay > 0f && topNight < topDay * 0.8f);
                if (day >= 0f) dn.timeOfDay = day;
            }
            // Esc: the sky goes, the map is unloaded
            string un0 = FlybySky.LastUnload; float tEsc = Time.unscaledTime;
            yield return Tap(Key.Escape);
            yield return WaitFor(() => sky.Weight <= 0.001f, 3f);
            float tOff = Time.unscaledTime - tEsc;
            yield return WaitFor(() => sky.ResidentMB <= 0f, 5f);
            float tUn = Time.unscaledTime - tEsc;
            Check($"sky after Esc: Weight 0 after {tOff:0.00} s (at most 1.5), the map unloaded after {tUn:0.00} s ({mb:0.0} MB freed; last unload: {FlybySky.LastUnload}), first-person sky {sky.FpsMode}",
                  !OzFpsView.On && tOff <= 1.5f && mb > 0f && sky.ResidentMB <= 0f && !sky.FpsMode);   // U1 (check only): resident before, 0 after = unloaded (LastUnload's text is the same words on every unload of the same map)
        }

        static float Lum(Texture2D t, Vector3 vp)
        {
            if (!t || vp.z <= 0f) return -1f;
            int cx = Mathf.RoundToInt(vp.x * t.width), cy = Mathf.RoundToInt(vp.y * t.height); if (cx < 3 || cy < 3 || cx >= t.width - 3 || cy >= t.height - 3) return -1f;
            float s = 0f; int n = 0;
            for (int j = -2; j <= 2; j++) for (int i = -2; i <= 2; i++) { var c = t.GetPixel(cx + i, cy + j); s += 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b; n++; }
            return s / n;
        }
        static float TopLum(Texture2D t)
        {
            if (!t) return -1f; double s = 0; int n = 0;
            for (int y = (int)(t.height * 0.7f); y < t.height; y += 4) for (int x = 0; x < t.width; x += 4) { var c = t.GetPixel(x, y); s += 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b; n++; }
            return n > 0 ? (float)(s / n) : -1f;
        }

        // ------------------------------------------------------------------ (3)
        /// <summary>Everything the view could leave behind: every light (on, intensity, range, place), every unit view's renderers (held off, on,
        /// active), the camera (pose, field of view, near and far clip), the globals the view and the sky set.</summary>
        Dictionary<string, string> StateList()
        {
            var d = new Dictionary<string, string>();
            foreach (var l in FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (l) d["light " + l.GetInstanceID() + " " + l.name] = $"{l.enabled && l.gameObject.activeInHierarchy} {l.intensity:0.000} {l.range:0.00} {l.transform.position:F2}";
            foreach (var kv in g.Views)
            {
                var v = kv.Value; if (!v) continue;
                foreach (var r in v.GetComponentsInChildren<Renderer>(true)) if (r) d["r " + r.GetInstanceID() + " " + r.name] = $"{r.forceRenderingOff} {r.enabled} {r.gameObject.activeInHierarchy}";
            }
            foreach (var c in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (c) d["canvas " + c.GetInstanceID() + " " + c.name] = $"{c.enabled && c.gameObject.activeInHierarchy}";
            d["camera"] = $"{cam.transform.position:F3} {cam.transform.rotation.eulerAngles:F2} {cam.fieldOfView:0.00} {cam.nearClipPlane:0.000} {cam.farClipPlane:0.0}";
            d["_OzFowUnder"] = OzFpsView.FogUnderNow.ToString("F3");
            d["_FlybySkyW"] = Shader.GetGlobalFloat("_FlybySkyW").ToString("0.000");
            d["cursor"] = $"{Cursor.lockState} {Cursor.visible}";
            return d;
        }

        IEnumerator LeaveRow(DayNight dn)
        {
            if (!FindSpot(out int x, out int y, true)) { Check($"leave: a free place ({LastSpotWhy})", false); yield break; }
            if (dn) { dn.running = false; dn.timeOfDay = 0.66f; }
            var u = Make("vehicle", "light", x, y, me); yield return Real(0.3f);
            var v = ViewOf(u); if (u == null || !v) { Check("leave: a light tank made", false); yield break; }
            Select(u);
            rts.focus = ZGame.PxToWorld(x, y); yield return Real(2f);                                        // headlights on at night, the selection rings up
            var cd = cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>(); var aa0 = cd ? cd.antialiasing : UnityEngine.Rendering.Universal.AntialiasingMode.None;
            bool paused0 = g.paused; float ts0 = Time.timeScale;
            g.paused = true; Time.timeScale = 0f; if (cd) cd.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.None;
            yield return Frames(8);
            Vector3 focus0 = rts.focus; float h0 = rts.height, p0 = rts.pitch;
            yield return new WaitForEndOfFrame(); var a = ScreenCapture.CaptureScreenshotAsTexture();
            yield return Frames(3); yield return new WaitForEndOfFrame(); var a2 = ScreenCapture.CaptureScreenshotAsTexture();
            var s0 = StateList();
            yield return Real(0.5f); var s0b = StateList();                                                  // U1 (check only): states that change on their own while the game is still (a flickering lamp or ember) are its noise, not the view's
            var own = new HashSet<string>(s0.Where(kv => !s0b.TryGetValue(kv.Key, out var w0) || w0 != kv.Value).Select(kv => kv.Key));
            yield return Enter("leave light tank (night, selected)", u, Snap(v));
            if (OzFpsView.On && OzFpsView.I)
            {
                yield return MouseBy(new Vector2(-300f, -200f)); yield return Real(0.6f);                    // look down the hull and round, the headlights by the eye
                yield return MouseBy(new Vector2(600f, 150f)); yield return Real(0.6f);
                yield return new WaitForEndOfFrame(); var inside = ScreenCapture.CaptureScreenshotAsTexture();
                float white = 0f; { var px = inside.GetPixels32(); int n = 0; for (int i = 0; i < px.Length; i += 7) { if (px[i].r > 245 && px[i].g > 245 && px[i].b > 235) white++; n++; } white /= Mathf.Max(1, n); }
                try { File.WriteAllBytes(Path.Combine(Folder(), "fps_leave_inside.png"), inside.EncodeToPNG()); } catch (IOException) { }
                Destroy(inside);
                Check($"leave: inside the view at night the tank's own lamp glows are not drawn (held {OzFpsView.Holds(v)}), white-hot pixels {white * 100f:0.0} % of the screen (at most 5 %)", OzFpsView.Holds(v) && white <= 0.05f);
                yield return Tap(Key.Escape); yield return Real(1.6f);                                        // the sky (if any) fades and the leave settles
            }
            rts.focus = focus0; yield return Frames(4);
            var s1 = StateList();
            var diff = new List<string>();
            foreach (var kv in s0) { if (!s1.TryGetValue(kv.Key, out var w)) diff.Add(kv.Key + ": gone"); else if (w != kv.Value && !own.Contains(kv.Key)) diff.Add($"{kv.Key}: {kv.Value} -> {w}"); }
            if (own.Count > 0) Log($"INFO leave: {own.Count} state(s) change on their own with the game still (left out): {string.Join("; ", own.Take(6))}");
            foreach (var kv in s1) if (!s0.ContainsKey(kv.Key)) diff.Add(kv.Key + ": new " + kv.Value);
            Check($"leave: {s0.Count} states (every light, every unit renderer, every canvas, the camera, the view's and the sky's globals, the cursor) the same before the entry and after the leave; differ: {diff.Count}{(diff.Count > 0 ? " (" + string.Join("; ", diff.Take(8)) + ")" : "")}; RTS height {h0:0.0} -> {rts.height:0.0}, tilt {p0:0.0} -> {rts.pitch:0.0}",
                  s0.Count > 10 && diff.Count == 0 && !OzFpsView.On);
            yield return new WaitForEndOfFrame(); var bTex = ScreenCapture.CaptureScreenshotAsTexture();
            float noise = DifferShift(a, a2), ab = DifferShift(a, bTex);
            try { File.WriteAllBytes(Path.Combine(Folder(), "fps_leave_before.png"), a.EncodeToPNG()); File.WriteAllBytes(Path.Combine(Folder(), "fps_leave_after.png"), bTex.EncodeToPNG()); } catch (IOException) { }
            Check($"leave: the still RTS frame (L02 night, the light tank selected, headlights on, the game still) before the entry and after the leave differ on {ab * 100f:0.000} % of the screen (its own noise {noise * 100f:0.000} %; at most noise + 0.1 %)", a && bTex && ab <= noise + 0.001f);
            Destroy(a); Destroy(a2); Destroy(bTex);
            if (cd) cd.antialiasing = aa0; Time.timeScale = ts0; g.paused = paused0;
            yield return Real(0.5f);
        }

        // ------------------------------------------------------------------ (4)
        IEnumerator LeftButton(bool left)
        {
            var c = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            InputSystem.QueueStateEvent(proofMouse, new MouseState { position = c, delta = Vector2.zero }.WithButton(MouseButton.Left, left)); yield return null;
        }

        IEnumerator FireRow()
        {
            if (!FindSpot(out int x, out int y)) { Check($"fire: a free place ({LastSpotWhy})", false); yield break; }
            rts.focus = ZGame.PxToWorld(x, y); yield return Real(0.4f);
            var u = Make("robot", "grunt", x, y, me);
            var e = Make("robot", "grunt", x + 80, y, foe, 1);                                                 // 10 m to the east, one robot
            if (e != null) { e.Damage = 0; e.MaxHealth = 10000000; e.Health = 10000000; }
            yield return Real(1.2f);
            var v = ViewOf(u); var ev = ViewOf(e);
            if (u == null || !v || e == null || !ev) { Check("fire: a grunt and an enemy dummy made", false); yield break; }
            W.SendStop(me, u); yield return Real(0.3f);
            yield return Enter("fire grunt", u, Snap(v));
            var fv = OzFpsView.I; if (!OzFpsView.On || !fv) yield break;
            yield return Aim(fv, ev.transform.position + Vector3.up * 1.1f, 0f); yield return Real(0.3f);
            int o0 = OzFpsView.FireOrders, c0 = OzFpsView.FireClicks; var sel0 = pi.Selection.ToList();
            float last = v.LastFireAt; int shots = 0; bool attackSeen = false;
            yield return LeftButton(true);
            float tHold = Time.unscaledTime;
            while (Time.unscaledTime - tHold < 3.5f)
            {
                if (v.LastFireAt != last) { shots++; last = v.LastFireAt; }
                foreach (var w in u.Waypoints) if (w.mode == WpMode.Attack && w.refId == e.Id) attackSeen = true;
                yield return null;
            }
            yield return LeftButton(false); yield return Frames(3);
            bool selSame = pi.Selection.Count == sel0.Count && pi.Selection.All(s => sel0.Contains(s));
            Check($"fire: left click on the enemy dummy at the centre: {OzFpsView.FireClicks - c0} press, {OzFpsView.FireOrders - o0} attack order(s) ({OzFpsView.LastFire}), an attack waypoint on it {attackSeen}; held 3.5 s: {shots} shots of its own (at least 2), the selection unchanged {selSame}",
                  OzFpsView.FireClicks - c0 == 1 && OzFpsView.FireOrders - o0 >= 1 && attackSeen && shots >= 2 && selSame);
            // the ground: no order, no selection change
            W.SendStop(me, u); yield return Real(0.4f);
            yield return Aim(fv, cam.transform.position + new Vector3(-0.6f, -0.45f, -0.6f) * 8f, 0f); yield return Real(0.2f);   // down at the ground away from the dummy
            int n0 = OzFpsView.FireNone, o1 = OzFpsView.FireOrders;
            yield return LeftButton(true); yield return Real(0.6f); yield return LeftButton(false); yield return Frames(3);
            bool noAtk = !u.Waypoints.Any(w => w.mode == WpMode.Attack);
            selSame = pi.Selection.Count == sel0.Count && pi.Selection.All(s => sel0.Contains(s));
            Check($"fire: left click on the ground: no order ({OzFpsView.FireOrders - o1} sent, {OzFpsView.FireNone - n0} 'nothing to fire at'; the game has no attack-ground order), no attack waypoint {noAtk}, the selection unchanged {selSame}",
                  OzFpsView.FireNone - n0 == 1 && OzFpsView.FireOrders == o1 && noAtk && selSame);
            yield return Leave1();
            Kill(W, e); yield return Real(0.3f);
        }

        // ------------------------------------------------------------------ (5)
        IEnumerator FlybyRow()
        {
            var sky = FlybySky.I; if (!sky) yield break;
            if (OzFpsView.On) yield return Leave1();
            FlybySky.ForceOn = true;                                                                         // the flybys' own path (as CaptureKit's tools use it)
            yield return WaitFor(() => sky.Weight >= 0.999f, 6f);
            var sun = RenderSettings.sun; var sdir = Shader.GetGlobalVector("_FlybySunDir");
            Vector3 want = sun ? FlybySky.VisualSun(-sun.transform.forward) : Vector3.zero;
            float el = Mathf.Asin(Mathf.Clamp(sdir.y, -1f, 1f)) * Mathf.Rad2Deg;
            Check($"flyby sky after the round trip (ForceOn): first-person sky {sky.FpsMode} (off), Weight {sky.Weight:0.00}, its sun {el:0.0} deg up = the flybys' low drawn sun (VisualSun, at most {FlybySky.SunVisualMaxDeg:0.0} deg; {Vector3.Angle(sdir, want):0.00} deg off it)",
                  !sky.FpsMode && sky.Weight >= 0.999f && sun && Vector3.Angle(sdir, want) < 0.5f && el <= FlybySky.SunVisualMaxDeg + 0.05f);
            FlybySky.ForceOn = false;
            yield return WaitFor(() => sky.Weight <= 0.001f, 3f);
            yield return WaitFor(() => sky.ResidentMB <= 0f, 5f);
            Check($"flyby sky off again: Weight {sky.Weight:0.00}, map resident {sky.ResidentMB:0.0} MB", sky.Weight <= 0.001f && sky.ResidentMB <= 0f);
        }
    }
}
#endif
