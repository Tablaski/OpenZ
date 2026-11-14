using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// Version 1.2, level 16 (testing the free camera): the counted check of the rain, the
    /// mist and the far fall in the views a player can reach, with pictures. Menu OpenZ/V1.2/L16 far rain: check + pictures (Play), in Play
    /// on level 16 once the land beyond the rim is shown (the start flyover over), the HUD up, no menu open, the pointer inside the Game
    /// view away from its edges, hands off. It forces a rain spell (WEATHER on for the run), then walks:
    ///  - the nine widest-zoom fixed views of OzV12EdgeShots (middle, the four corners, the four edge middles; the game's own clamp, so the
    ///    north cut, the south give and item 29's side cut hold);
    ///  - the FREE CAMERA's lowest tilt (25 deg) at the widest zoom looking out over the four sides and the four corners, and the owner's
    ///    view (north over the far fall's place, heading 6);
    /// and per view counts: the rain's three streak layers (each box covers the view's corners at 10 / 50 / 90 % of its depth band; no
    /// column of the frame where the streaks stop: hard edges 0), the mist's ends in the frame (0; round 4's end is counted beside it for
    /// before / after), the far fall framed and drawn (never both), the splashes beyond the plateau off their ground by more than 0.2 m
    /// (0). Then the same free views at night (no rain falls at night: the mist and the far fall) and the owner's view on the three
    /// graphics presets. A full-screen picture of every view. Everything is put back at the end (camera, FREE CAMERA, WEATHER, preset,
    /// </summary>
    public static class OzV12FarRainCheck
    {
        const string Tag = "[OzV12FarRainCheck]";
        public static string Report = "not run";
        static IEnumerator run; static int waitFrame;
        static readonly List<string> lines = new List<string>();
        static int ok, bad;
        static bool freePref, wxPref, prefsHeld; static int freeWas; static bool wxWas;

        static string Out { get { var p = Path.Combine(OpenZPaths.Records, "milestones", "l16_far_rain_v12"); Directory.CreateDirectory(p); return p; } }

        [MenuItem("OpenZ/V1.2/L16 far rain: check + pictures (Play)")]
        public static void Menu() => Run();

        public static string Run()
        {
            if (!EditorApplication.isPlaying || ZGame.I == null || ZGame.I.World == null || ZGame.I.level != OzL16Outskirts.Level) { Debug.LogWarning(Tag + " start Play on level 16 first"); return "not in Play on level 16"; }
            if (!OzL16Outskirts.I || OzL16Outskirts.BuildMs <= 0f || OzL16FlyoverFlora.LowLookOn) { Debug.LogWarning(Tag + " the land is not shown yet (wait for the start flyover to end)"); return "land not shown yet"; }
            if (!OzJungleWeather.I) { Debug.LogWarning(Tag + " no level 16 weather in this scene"); return "no weather"; }
            if (run != null) return "running already";
            lines.Clear(); ok = bad = 0; Report = "running";
            run = Walk(); waitFrame = 0; EditorApplication.update -= Step; EditorApplication.update += Step;
            return "running: " + Out;
        }

        /// <summary>The two options the run turns on (FREE CAMERA, WEATHER) back as the player had them (also when Play stops in the middle).</summary>
        static void PutBackPrefs()
        {
            if (!prefsHeld) return;
            prefsHeld = false;
            if (freePref) OzFreeCam.On = freeWas == 1; else { OzFreeCam.On = true; PlayerPrefs.DeleteKey(OzFreeCam.PrefKey); PlayerPrefs.Save(); }   // never set: the default (ON)
            if (wxPref) { if (OzWeather.Enabled != wxWas) OzWeather.Enabled = wxWas; } else { PlayerPrefs.DeleteKey(OzWeather.Pref); PlayerPrefs.Save(); }
        }

        static void Step()
        {
            if (run == null || !EditorApplication.isPlaying) { run = null; EditorApplication.update -= Step; PutBackPrefs(); return; }
            if (Time.frameCount < waitFrame) return;
            bool more;
            try { more = run.MoveNext(); }
            catch (System.Exception ex) { Debug.LogException(ex); bad++; lines.Add("EXCEPTION " + ex.Message); more = false; }
            if (!more) { run = null; EditorApplication.update -= Step; PutBackPrefs(); Finish(); return; }
            waitFrame = Time.frameCount + (run.Current is int n ? n : 1);
        }

        struct View { public string name; public float x, z, heading, tilt; public View(string n, float x, float z, float h, float t) { name = n; this.x = x; this.z = z; heading = h; tilt = t; } }

        static void Pose(RtsCamera rts, View v)
        {
            rts.FreeLookOff();
            rts.focus = new Vector3(v.x, 0f, v.z); rts.SetHeightImmediate(rts.maxHeight); rts.TakeShake();
            if (v.tilt > 0f) rts.FreeTurn(new Vector2(v.heading / RtsCamera.FreeLookSpeed, (rts.pitch - v.tilt) / RtsCamera.FreeLookSpeed));
        }

        /// <summary>One view's counts; true = every layer passes.</summary>
        static bool Measure(string label, Camera cam, bool needRain, out string line)
        {
            var wx = OzJungleWeather.I;
            bool raining = wx.Rain > 0.05f;
            int uncovered = 0, edges = 0, beyond = 0, off = 0;
            string streaks = raining ? wx.StreakCheck(out uncovered, out edges) : "no rain";
            OzL16Outskirts.MistEndsInView(cam, out int ends, out int ends4);
            bool framed = OzL16FlyoverFlora.FarFallInView(cam, out bool drawn);
            string splash = raining ? wx.FarSplashCheck(out beyond, out off) : "";
            bool pass = uncovered == 0 && edges == 0 && ends == 0 && !(framed && drawn) && off == 0 && (raining || !needRain);
            var t = cam.transform;
            line = $"{label} | {(pass ? "PASS" : "FAIL")}{(needRain && !raining ? " (the rain is not falling)" : "")} | camera {t.position.x:0.0},{t.position.y:0.0},{t.position.z:0.0} tilt {t.eulerAngles.x:0.0} heading {t.eulerAngles.y:0.0}"
                 + $" | rain: box corners uncovered {uncovered}, hard edges {edges} | mist ends in the frame {ends} (round 4's end would show at {ends4} points)"
                 + $" | far fall framed {(framed ? "yes" : "no")}, drawn {(drawn ? "YES" : "no")} | splashes beyond the plateau {beyond}, off their ground {off}"
                 + $" | batches {UnityStats.batches} tris {UnityStats.triangles}"
                 + (raining ? "\n      " + streaks + "\n      " + splash : "");
            if (pass) ok++; else bad++;
            return pass;
        }

        static IEnumerator Walk()
        {
            var g = ZGame.I; var hud = OpenZHud.I; var rts = hud ? hud.rts : Object.FindFirstObjectByType<RtsCamera>();
            var cam = rts ? rts.GetComponent<Camera>() : Camera.main; var wx = OzJungleWeather.I; var dn = Object.FindFirstObjectByType<DayNight>();
            if (!rts || !cam || !wx) { lines.Add("no RTS camera or weather"); bad++; yield break; }
            string dir = Out;
            // what is put back at the end
            Vector3 focus0 = rts.focus; float h0 = rts.height; var follow0 = rts.follow; Vector2 drift0 = rts.autoDrift;
            freePref = PlayerPrefs.HasKey(OzFreeCam.PrefKey); freeWas = PlayerPrefs.GetInt(OzFreeCam.PrefKey, 0);
            wxPref = PlayerPrefs.HasKey(OzWeather.Pref); wxWas = OzWeather.Enabled; prefsHeld = true;
            int quality0 = OzQuality.Level; float clock0 = dn ? dn.timeOfDay : 0f;
            try
            {
                rts.follow = null; rts.autoDrift = Vector2.zero;
                if (!OzWeather.Enabled) OzWeather.Enabled = true;
                if (!OzFreeCam.On) OzFreeCam.On = true;
                float W = g.World.Map.WidthPix / 8f, H = g.World.Map.HeightPix / 8f;
                lines.Add($"L16 far rain check | screen {Screen.width}x{Screen.height} | widest zoom {rts.maxHeight:0} m, north cut {rts.northCut:0}, south give {rts.southGive:0}, side cut {rts.sideCut:0} | graphics {OzQuality.Name}");
                lines.Add(OzL16FlyoverFlora.FarFallStatus());
                lines.Add(OzL16Outskirts.FarMistStatus());
                bool cut = OzL16FlyoverFlora.FarFallCutNow(out var fb);
                lines.Add($"the far fall stands back in the steep look: {(cut ? "PASS" : "FAIL")} (its box x {fb.min.x:F1}..{fb.max.x:F1} y {fb.min.y:F1}..{fb.max.y:F1} z {fb.min.z:F1}..{fb.max.z:F1})");
                if (cut) ok++; else bad++;
                bool mist = OzL16Outskirts.MistPoints > 0;
                lines.Add($"the mist runs on over the far plain: {(mist ? "PASS" : "FAIL")} ({OzL16Outskirts.MistPoints} points, {OzL16Outskirts.MistTriangles} triangles)");
                if (mist) ok++; else bad++;
                var views = new List<View>
                {
                    new View("fixed middle", W * 0.5f, -H * 0.5f, 0f, 0f), new View("fixed nw", 0f, 0f, 0f, 0f), new View("fixed ne", W, 0f, 0f, 0f),
                    new View("fixed sw", 0f, -H, 0f, 0f), new View("fixed se", W, -H, 0f, 0f), new View("fixed n", W * 0.5f, 0f, 0f, 0f),
                    new View("fixed s", W * 0.5f, -H, 0f, 0f), new View("fixed w", 0f, -H * 0.5f, 0f, 0f), new View("fixed e", W, -H * 0.5f, 0f, 0f),
                    new View("free north", W * 0.5f, 0f, 0f, 25f), new View("free north-east", W, 0f, 45f, 25f), new View("free east", W, -H * 0.5f, 90f, 25f),
                    new View("free south-east", W, -H, 135f, 25f), new View("free south", W * 0.5f, -H, 180f, 25f), new View("free south-west", 0f, -H, 225f, 25f),
                    new View("free west", 0f, -H * 0.5f, 270f, 25f), new View("free north-west", 0f, 0f, 315f, 25f),
                    new View("owner view (free, north over the far fall)", W * 0.5f, 0f, 6f, 25f),
                };
                // ---- rain on (by day)
                wx.ForceSpell(600f, -1f, true);
                for (int i = 0; i < 3000 && wx.Rain < 0.75f; i++) yield return 1;                       // 8 s of wind, 15 s fade-in, then a slow swell 0.8..1
                lines.Add($"rain forced: {wx.Rain:F2} | " + wx.LayersStatus());
                lines.Add("== day, rain on, widest zoom");
                foreach (var v in views)
                {
                    Pose(rts, v); yield return 90;                                                     // the streaks of the old view are gone (they live 0.85 s)
                    Measure(v.name, cam, true, out string line); lines.Add(line);
                    string file = Path.Combine(dir, "FARRAIN_day_" + v.name.Split(' ')[0] + "_" + v.name.Split(' ')[1].Replace("(", "") + ".png");
                    ScreenCapture.CaptureScreenshot(file); yield return 8;
                }
                // ---- the three graphics presets, the owner's view, rain on
                lines.Add("== the owner's view on the three graphics presets (rain on)");
                var owner = views[views.Count - 1];
                for (int q = 0; q < 3; q++)
                {
                    OzQuality.Level = q; yield return 30;
                    Pose(rts, owner); yield return 90;
                    Measure($"owner view, graphics {OzQuality.Name}", cam, true, out string line); lines.Add(line);
                    ScreenCapture.CaptureScreenshot(Path.Combine(dir, $"FARRAIN_owner_view_{OzQuality.Name}.png")); yield return 8;
                }
                OzQuality.Level = quality0; yield return 30;
                // ---- night: no rain falls at night on this level; the mist and the far fall
                wx.StopSpell(0.5f);
                for (int i = 0; i < 600 && wx.Rain > 0f; i++) yield return 1;
                if (dn) { dn.timeOfDay = 0.66f; dn.Apply(0.66f); }
                yield return 60;
                lines.Add($"== night (clock {(dn ? dn.timeOfDay : 0f):F2}), rain off");
                foreach (var v in views)
                {
                    if (v.tilt <= 0f) continue;
                    Pose(rts, v); yield return 45;
                    Measure(v.name + ", night", cam, false, out string line); lines.Add(line);
                    if (v.name.StartsWith("owner") || v.name == "free north" || v.name == "free east")
                    { ScreenCapture.CaptureScreenshot(Path.Combine(dir, "FARRAIN_night_" + v.name.Split(' ')[0] + "_" + v.name.Split(' ')[1] + ".png")); yield return 8; }
                }
                lines.Add(OzL16FlyoverFlora.FarFallStatus());
                lines.Add(OzL16Outskirts.FarMistStatus());
                lines.Add(wx.Status());
            }
            finally
            {
                if (dn) { dn.timeOfDay = clock0; dn.Apply(clock0); }
                if (OzQuality.Level != quality0) OzQuality.Level = quality0;
                rts.FreeLookOff();
                PutBackPrefs();
                rts.focus = focus0; rts.SetHeightImmediate(h0); rts.follow = follow0; rts.autoDrift = drift0;
            }
        }

        static void Finish()
        {
            Report = $"{Tag} {(bad == 0 && ok > 0 ? "PASS" : "FAIL")} {ok}/{ok + bad} (the two state lines and every view)";
            lines.Insert(0, Report);
            File.WriteAllLines(Path.Combine(Out, "FARRAIN_CHECK.txt"), lines);
            if (bad == 0 && ok > 0) Debug.Log(Report + "\n" + string.Join("\n", lines)); else Debug.LogError(Report + "\n" + string.Join("\n", lines));
        }
    }
}
