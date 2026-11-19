// OpenZ (GPL-3.0)
// "in game ... very slow responding", the player lowered RESOLUTION to get it playable). GRAPHICS QUALITY LOW already turns MSAA off,
// cuts the shadows to 1024 px / 1 cascade / 120 m, turns SSAO off, runs the fog ray-march at 9 steps and dims the lamps (View/OzQuality.cs,
// OzLitePresets.cs), but it drew the 3D picture at every pixel of the screen (render scale 1.0 on every preset), so on a 1920x1080 or
// larger screen LOW still paid the full price of every full-screen pass (fog, SMAA, water copies, post stack).
//
// THE CHANGE (LOW only; HIGH and MEDIUM are never touched): while LOW is in force the 3D picture is drawn at most at about 1440x810
// worth of pixels through the URP render scale of the LOW preset asset (OZ_URP_Low). The interface (screen-space overlay canvases:
// menus, HUD, text) is still drawn at the screen's own size, so it stays sharp. A screen up to 1.25 x that (1366x768, 1600x900) keeps
// the full picture; 1920x1080: scale 0.75 (1440x810); 2560x1440: 0.5625; 3840x2160: 0.375 (1440x810). A lower RESOLUTION step or a
// smaller window is measured as it is. The preset's own scale is never raised (a -ozrenderscale dev run stays lower); never under 0.3.
// Upscaling: the asset's own filter (Auto, bilinear); FSR is not switched on.
// 1.5 kit issue8_slow_chip_picture: the 3D PICTURE row (View/OzLowPicture.cs) picks LOW's budget: SHARP = the one above, exactly as
// before (the preset's own filter, scale at least 0.3); SOFT 1024x576 and SOFTEST 800x450 (scale at least 0.2, bilinear upscale). HIGH and
// MEDIUM: still never touched. Live and Player.log as above (the line names the step when it is not SHARP).
// Mac: the Mac player has its own pixel budget per preset (View/OzMacPerf.cs, kit mac_perf_1_5, LOW 1920x1080): when that file is in
// the Mac player this one does nothing there (no double scaling). Editor: nothing (a URP asset changed in Play would be the project's).
// Looked at again only after a GRAPHICS QUALITY step, a RESOLUTION / FULLSCREEN change or a window resize; one Player.log line each.
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace OpenZ
{
    /// <summary>1.5 kit issue8_resolution_low: GRAPHICS QUALITY LOW draws the 3D picture within a pixel budget (see the head of this file).
    /// One object for the session; "[OpenZ Quality] LOW: ..." in Player.log names every change.</summary>
    public sealed class OzLowScale : MonoBehaviour
    {
        /// <summary>The most pixels of LOW's drawn 3D picture (BudgetW x BudgetH), the slack a screen may be over it and still keep the
        /// full picture, and the lowest scale.</summary>
        public const int BudgetW = 1440, BudgetH = 810;
        public const float Slack = 1.25f, MinScale = 0.3f;
        /// <summary>The render scale set last (1 = the full picture) and the last log line (for checks).</summary>
        public static float Scale = 1f;
        public static string Line = "";

        UniversalRenderPipelineAsset seen, low; float lowOwn = 1f; int seenW = -1, seenH = -1, seenLevel = -1;
        int seenStep = -1; UpscalingFilterSelection lowFilter = UpscalingFilterSelection.Auto;           // 1.5 kit issue8_slow_chip_picture: the 3D PICTURE step seen last, the LOW asset's own upscale filter
        /// <summary>1.5 kit issue8_slow_chip_picture: the object lives (a Windows or Linux player), and LOW's budget is in force now (the
        /// 3D PICTURE row then leaves its log line to this file).</summary>
        public static bool Live, OnLow;
        void Awake() { Live = true; }
        void OnDestroy() { Live = false; OnLow = false; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            if (Application.isEditor || MacKitOwnsIt) return;
            var go = new GameObject("OzLowScale"); DontDestroyOnLoad(go); go.AddComponent<OzLowScale>();
        }

#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
        /// <summary>The Mac player with the mac_perf_1_5 kit in it: OzMacPerf owns the render scale there.</summary>
        static bool MacKitOwnsIt => System.Type.GetType("OpenZ.OzMacPerf") != null;
#else
        static bool MacKitOwnsIt => false;
#endif

        /// <summary>LOW's render scale for a w x h picture: 1 when it fits the budget with the slack, else the square root of budget /
        /// pixels (both sides by the same factor), never under MinScale.</summary>
        public static float For(int w, int h)
        {
            long px = (long)w * h, budget = (long)BudgetW * BudgetH;
            if (w <= 0 || h <= 0 || px <= budget * Slack) return 1f;
            return Mathf.Clamp((float)System.Math.Sqrt((double)budget / px), MinScale, 1f);
        }

        /// <summary>1.5 kit issue8_slow_chip_picture: LOW's render scale for a w x h picture on a 3D PICTURE step (View/OzLowPicture.cs):
        /// SHARP = For(w, h) exactly; SOFT / SOFTEST: the same rule with their own budget and the lowest scale OzLowPicture.SoftMinScale.</summary>
        public static float For(int w, int h, int step)
        {
            if (step <= 0) return For(w, h);
            long px = (long)w * h, budget = OzLowPicture.Budget(step);
            if (w <= 0 || h <= 0 || px <= budget * Slack) return 1f;
            return Mathf.Clamp((float)System.Math.Sqrt((double)budget / px), OzLowPicture.SoftMinScale, 1f);
        }

        /// <summary>1.5 kit issue8_slow_chip_picture: the render scale LateUpdate puts on LOW's asset, or -1 = not touched (HIGH, MEDIUM, or
        /// LOW's asset missing). lowOwn: the preset's own scale.</summary>
        public static float ScaleFor(int level, bool lowAsset, float lowOwn, int w, int h, int step) => level != 2 || !lowAsset ? -1f : Mathf.Min(lowOwn, For(w, h, step));

        /// <summary>1.5 kit issue8_slow_chip_picture: the upscale filter on LOW's asset: SHARP its own (as before), SOFT / SOFTEST bilinear (the
        /// Automatic filter picks nearest-neighbour at a scale of exactly 1/2, 1/3 ...: hard square pixels that crawl on thin lines).</summary>
        public static UpscalingFilterSelection FilterFor(int step, UpscalingFilterSelection own) => step > 0 ? UpscalingFilterSelection.Linear : own;

        void LateUpdate()
        {
            var cur = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            int w = Screen.width, h = Screen.height, l = OzQuality.Level, st = OzLowPicture.Value;   // 1.5 kit issue8_slow_chip_picture: the 3D PICTURE step too
            if (cur == seen && w == seenW && h == seenH && l == seenLevel && st == seenStep) return;          // only after a preset step, a screen / window change
            seen = cur; seenW = w; seenH = h; seenLevel = l; seenStep = st;
            if (!cur) return;
            if (l != 2 || cur.name != OzQuality.LowAsset)
            {   // HIGH, MEDIUM (or LOW's asset missing: HIGH's asset in force): their own scale, never touched
                OnLow = false;
                if (Scale != 1f) { Line = $"[OpenZ Quality] {OzQuality.Names[l]}: the 3D picture at the screen's own size ({w}x{h}), as the preset has it"; Debug.Log(Line); }
                Scale = 1f; return;
            }
            OnLow = true;
            if (cur != low) { low = cur; lowOwn = cur.renderScale; lowFilter = cur.upscalingFilter; }                         // the preset's own scale (1.0 as shipped; a dev run's lower one)
            float s = ScaleFor(l, true, lowOwn, w, h, st);                                    // SHARP: Mathf.Min(lowOwn, For(w, h)) as before
            if (Mathf.Abs(cur.renderScale - s) > 0.0005f) cur.renderScale = s;
            var filter = FilterFor(st, lowFilter);
            if (cur.upscalingFilter != filter) cur.upscalingFilter = filter;                       // SHARP: the asset's own, untouched as before; SOFT / SOFTEST: bilinear
            Scale = s;
            Line = $"[OpenZ Quality] LOW: screen {w}x{h}: the 3D picture is drawn at {Mathf.RoundToInt(w * s)}x{Mathf.RoundToInt(h * s)} (render scale {s:0.###}, "
                   + $"at most {(st > 0 ? OzLowPicture.BudgetName(st) + " for LOW with 3D PICTURE " + OzLowPicture.Names[st] + ", bilinear upscale" : BudgetW + "x" + BudgetH + " for LOW")}), the interface at {w}x{h}";
            Debug.Log(Line);
        }
    }
}
