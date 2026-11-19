// OpenZ (GPL-3.0)
// His two logs: a 1280x720 window on GRAPHICS
// QUALITY LOW gave 12-14 fps (the graphics chip 70-74 ms a frame), the whole game at 640x360 gave 22-23 fps (27-43 ms), and at
// 640x360 every menu, HUD line and text is drawn at 640x360 too. LOW's pixel budget (View/OzLowScale.cs, 1440x810) does nothing in
// his window: 1280x720 is already under it.
//
// THE ROW (OPTIONS > GRAPHICS, one of the LIGHTEST FOR SLOW PCS rows, after WRECKS AND DEBRIS): 3D PICTURE SHARP (default) / SOFT /
// SOFTEST, saved as oz_lite_picture 0 / 1 / 2, live. It only changes the pixel budget OzLowScale draws LOW's 3D picture within:
//   SHARP    LOW's own budget, exactly as before (1440x810, the lowest scale 0.3, the LOW asset's own upscale filter);
//   SOFT     1024x576;
//   SOFTEST  800x450.
// The interface (screen-space overlay canvases: menus, HUD, every text) is drawn at the screen's own size on every step, so it stays
// sharp. It acts only while GRAPHICS QUALITY is LOW (the scale of HIGH and MEDIUM is never touched; with MEDIUM or HIGH the row is
// saved and waits). SOFT and SOFTEST may go down to scale 0.2 (an 800x450 picture still on a 3840x2160 screen) and upscale with the
// bilinear filter (the LOW asset's own "Automatic" filter picks nearest-neighbour whenever the scale is exactly 1/2, 1/3, 1/4: on a
// 1600x900 screen SOFTEST is exactly 0.5 and would show hard square pixels that crawl along thin lines while the camera scrolls).
// SHARP puts the asset's own filter back. No feature is switched off on any step: the same passes, effects and lights, fewer pixels.
// The first start on a slow graphics chip (View/OzSlowChipStart.cs) picks SOFTEST together with LOW, the other lightest rows and
// FRAME RATE LIMIT 30, under the same rules as those rows (back to SHARP by itself when the player picks MEDIUM / HIGH, unless he set
// the row himself). Mac: OzMacPerf owns the render scale there (OzLowScale stands aside), so the row is not built there (InOptions).
// Player.log: one line a change, "[OpenZ Quality] LOW: ..." from OzLowScale while LOW is in force, else the row's own line here.
// View only: no rule of the game reads any of it.
using UnityEngine;

namespace OpenZ
{
    public static class OzLowPicture
    {
        public const string Label = "3D PICTURE", Pref = "oz_lite_picture";
        public const string Tip = "3D PICTURE|Works on GRAPHICS QUALITY LOW. Sharp (default). Soft and Softest draw the battlefield with fewer pixels so slow graphics chips keep up; the battlefield looks softer. Menus and text stay sharp. Units and rules never change.|";
        public static readonly string[] Names = { "SHARP", "SOFT", "SOFTEST" };
        /// <summary>LOW's pixel budget per step (SHARP = OzLowScale's own, unchanged).</summary>
        public static readonly int[] BudgetW = { OzLowScale.BudgetW, 1024, 800 }, BudgetH = { OzLowScale.BudgetH, 576, 450 };
        /// <summary>The lowest render scale of SOFT and SOFTEST (SHARP keeps OzLowScale.MinScale): 800x450 still fits a 3840x2160 screen.</summary>
        public const float SoftMinScale = 0.2f;
        public const int Sharp = 0, Soft = 1, Softest = 2;

        static int v = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { v = -1; }

        /// <summary>The saved step: 0 SHARP (default), 1 SOFT, 2 SOFTEST (read once, the same pattern as the other lightest rows).</summary>
        public static int Value
        {
            get { if (v < 0) { int s = PlayerPrefs.GetInt(Pref, 0); v = s >= 1 && s <= 2 ? s : 0; } return v; }
            set
            {
                v = value == 1 || value == 2 ? value : 0; PlayerPrefs.SetInt(Pref, v); PlayerPrefs.Save();
                if (!(OzLowScale.Live && OzLowScale.OnLow))                                              // on LOW OzLowScale's own line names the change (the next frame)
                    Debug.Log($"[OpenZ Quality] LOW: 3D PICTURE {Names[v]} saved ({Pref} {v}); it works while GRAPHICS QUALITY is LOW (now {OzQuality.Names[Mathf.Clamp(OzQuality.Level, 0, 2)]})");
            }
        }
        public static string Name => Names[Value];
        /// <summary>The row's arrows: SHARP -> SOFT -> SOFTEST -> SHARP (dir &lt; 0 the other way).</summary>
        public static void Step(int dir) => Value = (Value + (dir < 0 ? 2 : 1)) % 3;
        /// <summary>Read the saved value again (OzSlowChipStart after it set the row back, the checks).</summary>
        public static void Reload() { v = -1; }

        /// <summary>The pixel budget of a step.</summary>
        public static long Budget(int step) { int s = Mathf.Clamp(step, 0, 2); return (long)BudgetW[s] * BudgetH[s]; }
        public static string BudgetName(int step) { int s = Mathf.Clamp(step, 0, 2); return BudgetW[s] + "x" + BudgetH[s]; }

        /// <summary>The row is built in OPTIONS (not in the Mac player: OzMacPerf owns the scale there).</summary>
        public static bool InOptions => Shown(Application.platform);
        public static bool Shown(RuntimePlatform p) => p != RuntimePlatform.OSXPlayer;
    }
}
