using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// Version 1.2, level 16: the land beyond the rim as a landscape (OzL16Outskirts.Landscape.cs): the Play check and its pictures.
    /// Menu OpenZ/V1.2/L16 outskirts landscape: check + pictures (Play), in Play on level 16 once the land is shown (the start flyover
    /// OzL16Outskirts.LandscapeOn off and the level restarted):
    ///  - LANDSCAPE_&lt;round&gt;.txt: OutskirtsLandscapeCheck() (the counted check), RimFixCheck(), TreeCheck(), OutskirtsPropsCheck(),
    ///    FarMistStatus(), Status();
    ///  - the pictures, each with the frame's own counters (batches, triangles) and the landscape's instanced drawing in SHOTS_&lt;round&gt;.txt:
    ///    the owner's view (the FREE CAMERA over the red fort turned south, widest zoom, tilt 35) and the same with the fixed camera;
    ///    the FREE CAMERA at the widest zoom, tilt 25, looking out south, north, east and west; the widest fixed views of the north,
    ///    east, south, west, north-west and south-east; 125 m and 70 m at the north and west rims.
    /// The FREE CAMERA option is turned on for the run and put back as the player had it. Editor only, changes nothing in the game (the
    /// camera is given back to the player at the end).
    /// </summary>
    public static class OzV12OutskirtsLandscapeShots
    {
        const string Tag = "[OzV12OutskirtsLandscapeShots]";
        static IEnumerator run; static int waitFrame; static bool prefsHeld, freePref; static int freeWas;
        static readonly List<string> log = new List<string>();

        static string Out
        {
            get { var p = Path.Combine(OpenZPaths.Records, "milestones", "l16_outskirts_landscape_v12"); Directory.CreateDirectory(p); return p; }
        }

        public static string Round => OzL16Outskirts.LandscapeOn ? (OzL16Outskirts.LandBold ? "k55b" : "k55") : "r5e";

        [MenuItem("OpenZ/V1.2/L16 outskirts landscape: check + pictures (Play)")]
        public static void Menu() => Run();

        public static string Run()
        {
            if (!EditorApplication.isPlaying || ZGame.I == null || ZGame.I.level != OzL16Outskirts.Level) { Debug.LogWarning(Tag + " start Play on level 16 first"); return "not in Play on level 16"; }
            if (!OzL16Outskirts.I || OzL16Outskirts.BuildMs <= 0f || OzL16FlyoverFlora.LowLookOn) { Debug.LogWarning(Tag + " the land is not shown yet (wait for the start flyover to end)"); return "land not shown yet"; }
            if (run != null) return "running already";
            log.Clear(); run = Walk(); waitFrame = 0;
            EditorApplication.update -= Step; EditorApplication.update += Step;
            return "running: " + Out;
        }

        static void PutBackPrefs()
        {
            if (!prefsHeld) return;
            prefsHeld = false;
            if (freePref) OzFreeCam.On = freeWas == 1; else { OzFreeCam.On = true; PlayerPrefs.DeleteKey(OzFreeCam.PrefKey); PlayerPrefs.Save(); }   // never set: the default (ON)
        }

        static void Step()
        {
            if (run == null || !EditorApplication.isPlaying) { run = null; EditorApplication.update -= Step; PutBackPrefs(); return; }
            if (Time.frameCount < waitFrame) return;
            bool more;
            try { more = run.MoveNext(); }
            catch (System.Exception ex) { Debug.LogException(ex); log.Add("EXCEPTION " + ex.Message); more = false; }
            if (!more)
            {
                run = null; EditorApplication.update -= Step; PutBackPrefs();
                File.WriteAllLines(Path.Combine(Out, "SHOTS_" + Round + ".txt"), log);
                Debug.Log(Tag + " done: " + Out);
                return;
            }
            waitFrame = Time.frameCount + (run.Current is int n ? n : 1);
        }

        struct View
        {
            public string name; public float x, z, height, heading, tilt;
            public View(string n, float x, float z, float h, float heading, float tilt) { name = n; this.x = x; this.z = z; height = h; this.heading = heading; this.tilt = tilt; }
        }

        static void Pose(RtsCamera rts, View v)
        {
            rts.FreeLookOff(); rts.yawHeld = false; rts.yaw = 0f; rts.unclamped = false;
            rts.focus = new Vector3(v.x, 0f, v.z); rts.SetHeightImmediate(v.height > 0f ? v.height : rts.maxHeight); rts.TakeShake();
            if (v.tilt > 0f) rts.FreeTurn(new Vector2(v.heading / RtsCamera.FreeLookSpeed, (rts.pitch - v.tilt) / RtsCamera.FreeLookSpeed));
        }

        static IEnumerator Walk()
        {
            var hud = OpenZHud.I; var rts = hud ? hud.rts : Object.FindFirstObjectByType<RtsCamera>();
            var cam = rts ? rts.GetComponent<Camera>() : Camera.main;
            if (!rts || !cam) { log.Add("no RTS camera"); yield break; }
            string dir = Out, r = Round;
            Vector3 focus0 = rts.focus; float h0 = rts.height; var follow0 = rts.follow; Vector2 drift0 = rts.autoDrift;
            freePref = PlayerPrefs.HasKey(OzFreeCam.PrefKey); freeWas = PlayerPrefs.GetInt(OzFreeCam.PrefKey, 0); prefsHeld = true;
            try
            {
                rts.follow = null; rts.autoDrift = Vector2.zero;
                if (!OzFreeCam.On) OzFreeCam.On = true;
                string check = OzL16Outskirts.OutskirtsBolderCheck() + "\n" + OzL16Outskirts.OutskirtsLandscapeCheck() + "\n" + OzL16Outskirts.RimFixCheck() + "\n" + OzL16Outskirts.TreeCheck() + "\n" +
                               OzL16Outskirts.OutskirtsPropsCheck() + "\n" + OzL16Outskirts.FarMistStatus() + "\n" + OzL16Outskirts.Status() + "\n";
                File.WriteAllText(Path.Combine(dir, "LANDSCAPE_" + r + ".txt"), check);
                Debug.Log(Tag + " " + check);
                log.Add($"L16 outskirts landscape, round {r} | screen {Screen.width}x{Screen.height} | graphics {OzQuality.Name} | picture | camera | frame counters | instanced");
                float W = 208f, H = 256f;
                var views = new List<View>
                {
                    new View("owner_south_free_t35", 104f, -229f, 0f, 180f, 35f), new View("owner_south_fixed_165", 104f, -229f, 0f, 0f, 0f),
                    new View("free_south_165_t25", 104f, -229f, 0f, 180f, 25f), new View("free_north_165_t25", 104f, -20f, 0f, 0f, 25f),
                    new View("free_east_165_t25", W, -H * 0.5f, 0f, 90f, 25f), new View("free_west_165_t25", 0f, -H * 0.5f, 0f, -90f, 25f),
                    new View("fixed_north_165", W * 0.5f, -10f, 0f, 0f, 0f), new View("fixed_east_165", W, -H * 0.5f, 0f, 0f, 0f),
                    new View("fixed_south_165", W * 0.5f, -H, 0f, 0f, 0f), new View("fixed_west_165", 0f, -H * 0.5f, 0f, 0f, 0f),
                    new View("fixed_nw_165", 0f, 0f, 0f, 0f, 0f), new View("fixed_se_165", W, -H, 0f, 0f, 0f),
                    new View("fixed_north_125", W * 0.5f, -10f, 125f, 0f, 0f), new View("fixed_west_125", 0f, -H * 0.5f, 125f, 0f, 0f),
                    new View("fixed_north_70", W * 0.5f, -10f, 70f, 0f, 0f), new View("fixed_west_70", 0f, -H * 0.5f, 70f, 0f, 0f),
                };
                foreach (var v in views)
                {
                    Pose(rts, v);
                    yield return 40;                                                                       // the camera and the frame settle
                    Pose(rts, v);
                    yield return 8;
                    string file = Path.Combine(dir, $"L16_land_{v.name}_{r}.png");
                    ScreenCapture.CaptureScreenshot(file);
                    var t = cam.transform;
                    log.Add($"{v.name} | {Path.GetFileName(file)} | camera {t.position.x:0.0},{t.position.y:0.0},{t.position.z:0.0} tilt {t.eulerAngles.x:0.0} heading {t.eulerAngles.y:0.0}, focus {rts.focus.x:0.0},{rts.focus.z:0.0} at {rts.height:0} m"
                            + $" | batches {UnityStats.batches} tris {UnityStats.triangles} setpass {UnityStats.setPassCalls} | {OzL16Outskirts.LandscapeViewCount()} | {OzL16Outskirts.RimViewCount()}");
                    yield return 12;
                }
            }
            finally
            {
                rts.FreeLookOff(); rts.yawHeld = false; rts.yaw = 0f; rts.unclamped = false;
                rts.follow = follow0; rts.autoDrift = drift0; rts.focus = focus0; rts.SetHeightImmediate(h0);
                PutBackPrefs();
            }
        }
    }
}
