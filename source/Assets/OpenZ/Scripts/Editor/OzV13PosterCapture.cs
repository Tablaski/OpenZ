#if !OPENZ_HARNESS
// OpenZ (GPL-3.0) - version 1.3: level 7's loading picture and level card still as a real game frame. Editor only, never in a build.
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// Menu OpenZ/V1.3/Poster. The level 7 poster (Resources/OpenZ/Menu/L07_poster.png: the loading screen and, as level 7 has no loading
    /// stills of its own, the level card) is a frame of the red briefing's sunrise fly-over, shot 1 (dawn_lava), at about 2 s: the big sun,
    /// haze, dragons, rocks, the fort, with the level-start vignette.
    ///  * Capture L07 sunrise burst: in Play, on the main menu. Deploys level 7 as red the way a player does (the PLAY AS BLUE box is put
    ///    back after), and while the briefing's shot list flies, renders the game camera into a 2560 x 1600 texture every Step s of the
    ///    briefing clock from From to To (OzV13PosterGrab). A camera render into a texture draws the world with every post effect of that
    ///    moment (bloom, the start vignette volume, the briefing grade, the fly-by sky and sun disc) and none of the screen overlays: no
    ///  * MatchWidth: the 16:10 picture keeps the 16:9 frame's width (the vertical field of view widened to match), so a 16:9 screen, where
    ///    the loading screen covers, shows the very frame of the briefing; a 16:10 screen shows a little more sky and ground.
    /// Nothing in the game changes. The frame is picked against the owner's still and installed by the poster kit, off Unity.
    /// </summary>
    public static class OzV13PosterCapture
    {
        public const string Tag = "[OpenZ poster]", Level = "L07";
        public const int W = 2560, H = 1600;
        /// <summary>The briefing clock window (s) and its step. The owner's moment lies at about 2 to 3.6 s of it.</summary>
        public static float From = 1.0f, To = 5.0f, Step = 0.1f;
        /// <summary>Keep the 16:9 frame's width in the 16:10 picture (false = the game's own vertical field of view, a narrower picture).</summary>
        public static bool MatchWidth = true;
        /// <summary>Play check: idle, going, waiting for the briefing, capturing, writing, done, or what stopped it.</summary>
        public static string Status = "idle";
        /// <summary>The folder of the last burst.</summary>
        public static string LastDir = "";

        [MenuItem("OpenZ/V1.3/Poster/Capture L07 sunrise burst (Play, main menu)")]
        static void MenuGo() => Go();

        [MenuItem("OpenZ/V1.3/Poster/Status")]
        static void MenuStatus() => Debug.Log(Tag + " " + Status + (LastDir.Length > 0 ? " -> " + LastDir : ""));

        /// <summary>The vertical field of view of the 16:10 picture for the game camera's own one (deg).</summary>
        public static float PictureFov(float gameFov) =>
            MatchWidth ? 2f * Mathf.Atan(Mathf.Tan(gameFov * 0.5f * Mathf.Deg2Rad) * (16f / 9f) / ((float)W / H)) * Mathf.Rad2Deg : gameFov;

        static string Fail(string why) { Status = "stopped: " + why; Debug.LogError(Tag + " " + Status); return Status; }

        /// <summary>Starts the burst from the main menu (returns the status line).</summary>
        public static string Go()
        {
            if (!Application.isPlaying) return Fail("not in Play: start Play on the main menu first");
            if (Status == "going" || Status.StartsWith("waiting") || Status == "capturing" || Status == "writing") return Fail("a burst is already running");
            var m = UnityEngine.Object.FindAnyObjectByType<OpenZMainMenu>();
            if (!m) return Fail("no main menu: start Play on the main menu scene");
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urp && Mathf.Abs(urp.renderScale - 1f) > 1e-3f) return Fail("render scale " + urp.renderScale.ToString("0.00") + " on " + urp.name + ": the poster needs the full quality (render scale 1)");
            int card = Array.FindIndex(OzLevelDef.All, d => d.Id == Level);
            if (card < 0) return Fail("level 7 is not one of the menu's levels in this tree");
            OzFinalGateView.SetFixed(1920, 1080, "OpenZ 1920x1080", out _);
            bool blueWas = OzPlayBlue.Box;
            OzPlayBlue.Box = false;                                                                          // red: the hot tub briefing and its sunrise fly-over
            Status = "going"; LastDir = "";
            int step = 0; double next = EditorApplication.timeSinceStartup + 0.8, giveUp = EditorApplication.timeSinceStartup + 90.0;
            EditorApplication.CallbackFunction f = null;
            f = () =>
            {
                if (!Application.isPlaying) { EditorApplication.update -= f; OzPlayBlue.Box = blueWas; if (Status == "going") Status = "stopped: Play ended"; return; }
                double now = EditorApplication.timeSinceStartup;
                if (now > giveUp) { EditorApplication.update -= f; OzPlayBlue.Box = blueWas; Fail("level 7 did not start within 90 s"); return; }
                if (now < next) return;
                next = now + 0.8;
                if (step == 0) { m.ProofSelect(); step = 1; return; }
                if (step == 1) { m.ProofFocus(card); step = 2; return; }
                if (step == 2) { m.ProofDeploy(); step = 3; next = now + 1.0; return; }
                var g = ZGame.I;
                if (!g || g.World == null) return;
                EditorApplication.update -= f;
                OzPlayBlue.Box = blueWas;                                                                    // the player's own box back (DEPLOY has read it)
                if (!OzVolcanic.Is(g.level) || g.player != Team.Red) { Fail("the level is " + g.level + " as " + g.player + ", not L07 as red"); return; }
                Status = "waiting for the briefing";
                var go = new GameObject("OZ_PosterGrab");
                go.AddComponent<OzV13PosterGrab.Run>();
                Debug.Log(Tag + " level 7 as red started: waiting for the briefing's shot list, then " + (Mathf.RoundToInt((To - From) / Step) + 1) + " frames at "
                          + W + " x " + H + ", briefing clock " + From.ToString("0.0") + " to " + To.ToString("0.0") + " s");
            };
            EditorApplication.update += f;
            Debug.Log(Tag + " going: battlefield, level 7, red, deploy");
            return Status;
        }
    }
}
#endif
