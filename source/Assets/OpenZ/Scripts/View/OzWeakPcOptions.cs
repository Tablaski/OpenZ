// OpenZ (GPL-3.0) - version 1.3, item 25 (h): Options rows for slower computers (GRAPHICS tab). Every row starts on today's
// behaviour: with no saved value nothing in the game changes, and none of them is ever switched on by the game itself.
//  * SMOOTH FRAMES ON SLOW PCS   OFF (default) / ON   oz_smooth_frames   live
//      ON: one frame carries at most FrameStepOn s of game time instead of ZGame.MaxFrameStep (0.1 s), so a frame that comes late
//      runs at most 5 rule ticks (1x) and 2-3 physics steps instead of 10 and 5: under load the battle runs a little slower for that
//      moment instead of the next frames getting long too. Every rule tick is the same; only how much game time a slow frame catches up.
//  * ENDING DEBRIS               FULL (default) / LIGHT  oz_ending_debris  live
//      LIGHT: while the end-of-level sequence runs, buildings that break throw half of their small (S / XS) concrete chunks, and
//      those small chunks cast no shadow and are not smoothed between physics steps. Big chunks, the fort, dust, fire and sound stay.
//  * SUN SHADOW STEPS            4 (default) / 2   oz_sun_steps   live
//      2: the sun's shadow map is drawn in 2 steps instead of HIGH's 4 (MEDIUM and LOW already use 2 and 1: nothing changes there).
//      Written on the settings file in force for this session only and put back on 4, on another preset and when the game ends.
//  * GRAPHICS API                DX12 (default) / DX11   oz_gfx_api   from the next start, Windows only
//      DX11: at the next start the game starts itself again once with the engine's own -force-d3d11 switch (the look is the same:
//      same settings, same shaders). A start with -force-d3d11 typed by the player is never touched.
// View only: no rule of the game reads any of it.
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace OpenZ
{
    public static class OzSmoothFrames
    {
        public const string Label = "SMOOTH FRAMES ON SLOW PCS", Pref = "oz_smooth_frames";
        public const string Tip = "SMOOTH FRAMES ON SLOW PCS|Off (default). On: when the computer cannot keep up, the battle runs a little slower for a moment instead of the next frames stuttering. Units, shots and the rules never change.|";
        /// <summary>The most game time one frame carries with the row ON (s): 5 rule ticks at 1x.</summary>
        public const float FrameStepOn = 0.05f;
        public static bool On
        {
            get => PlayerPrefs.GetInt(Pref, 0) == 1;
            set
            {
                PlayerPrefs.SetInt(Pref, value ? 1 : 0); PlayerPrefs.Save();
                if (ZGame.I) Time.maximumDeltaTime = FrameStep;                                            // live in a match (ZGame.Awake sets it at the start of each one)
                Debug.Log($"[OpenZ Options] smooth frames on slow computers: {(value ? "ON" : "OFF")} ({Pref} {(value ? 1 : 0)}, saved, live; largest frame step {FrameStep:0.00} s)");
            }
        }
        /// <summary>The largest step of game time one frame may carry (Time.maximumDeltaTime in a match).</summary>
        public static float FrameStep => On ? FrameStepOn : ZGame.MaxFrameStep;
    }

    public static class OzEndingDebris
    {
        public const string Label = "ENDING DEBRIS", Pref = "oz_ending_debris";
        public const string Tip = "ENDING DEBRIS|Full (default): every building of the beaten side breaks into all its chunks at the end of a level. Light, for slower computers: half of the small chunks, and those without shadows. Big chunks, the fort, dust, fire and sound stay.|";
        public static readonly string[] Names = { "FULL", "LIGHT" };
        public static bool Light => PlayerPrefs.GetInt(Pref, 0) == 1;
        public static string Name => Names[Light ? 1 : 0];
        public static void Step(int dir)
        {
            bool v = !Light;
            PlayerPrefs.SetInt(Pref, v ? 1 : 0); PlayerPrefs.Save();
            Debug.Log($"[OpenZ Options] ending debris: {Names[v ? 1 : 0]} ({Pref} {(v ? 1 : 0)}, saved, live)");
        }
    }

    public static class OzSunSteps
    {
        public const string Label = "SUN SHADOW STEPS", Pref = "oz_sun_steps";
        public const string Tip = "SUN SHADOW STEPS|4 (default) on HIGH. 2 asks less of a slower computer: the sun's shadows are a little softer far from the camera. Medium and Low already use fewer steps.|";
        public static readonly string[] Names = { "4", "2" };
        public static bool Two => PlayerPrefs.GetInt(Pref, 0) == 1;
        public static string Name => Names[Two ? 1 : 0];
        static UniversalRenderPipelineAsset touched; static int touched0; static bool hooked;
        /// <summary>The settings file whose steps this row lowered now (null = none), for the checks.</summary>
        public static UniversalRenderPipelineAsset Touched => touched;
        public static void Step(int dir)
        {
            bool v = !Two;
            PlayerPrefs.SetInt(Pref, v ? 1 : 0); PlayerPrefs.Save();
            Apply();
            Debug.Log($"[OpenZ Options] sun shadow steps: {Names[v ? 1 : 0]} ({Pref} {(v ? 1 : 0)}, saved, live)");
        }
        /// <summary>OzQuality.Apply (every preset change) and the row. With the row on 4 and nothing lowered before, it does nothing.</summary>
        public static void Apply()
        {
            var cur = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (touched && (touched != cur || !Two)) Restore();
            if (!Two || !cur || touched == cur || cur.shadowCascadeCount <= 2) return;
            touched = cur; touched0 = cur.shadowCascadeCount; cur.shadowCascadeCount = 2;
            if (!hooked) { hooked = true; Application.quitting += Restore; }                                // the editor: Play stop puts the settings file back
        }
        static void Restore()
        {
            if (touched) touched.shadowCascadeCount = touched0;
            touched = null;
        }
    }

    public static class OzGfxApi
    {
        public const string Label = "GRAPHICS API", Pref = "oz_gfx_api";
        public const string Tip = "GRAPHICS API|DX12 (default). DX11 can run better on some PCs. Takes effect at the next start of the game. The picture is the same.|";
        public static readonly string[] Names = { "DX12", "DX11" };
        const string RelaunchMark = "-oz-api-restart", ForceDx11 = "-force-d3d11";
        /// <summary>The row is built on Windows only (the other systems have their own graphics API).</summary>
        public static bool Available => Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor;
        public static bool Dx11 => PlayerPrefs.GetInt(Pref, 0) == 1;
        public static string Name => Names[Dx11 ? 1 : 0];
        public static void Step(int dir)
        {
            bool v = !Dx11;
            PlayerPrefs.SetInt(Pref, v ? 1 : 0); PlayerPrefs.Save();
            Debug.Log($"[OpenZ Options] graphics API: {Names[v ? 1 : 0]} ({Pref} {(v ? 1 : 0)}, saved, from the next start; now {SystemInfo.graphicsDeviceType})");
        }

        [System.Runtime.InteropServices.DllImport("shell32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        static extern System.IntPtr ShellExecuteW(System.IntPtr hwnd, string op, string file, string args, string dir, int show);   // 1.5 exe 14c: the DX11 start again (IL2CPP has no CreateProcess)

        static bool HasArg(string a)
        {
            foreach (var s in System.Environment.GetCommandLineArgs()) if (s.Equals(a, System.StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        /// <summary>DX11 chosen and the game came up on DX12 without being told otherwise: start again with -force-d3d11, once.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        static void Boot()
        {
            if (Application.isEditor || Application.platform != RuntimePlatform.WindowsPlayer || !Dx11) return;
            if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Direct3D12 || HasArg(ForceDx11) || HasArg(RelaunchMark)) return;
            try
            {
                var exe = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath), Application.productName + ".exe");   // 1.5 exe 14c: was Process.MainModule (Mono only; IL2CPP threw 'Collection cannot be null' and stayed on DX12)
                if (!System.IO.File.Exists(exe)) exe = System.IO.Path.GetFullPath(System.Environment.GetCommandLineArgs()[0]);   // a renamed exe: the path it was started with
                var args = System.Environment.GetCommandLineArgs(); var sb = new System.Text.StringBuilder();
                for (int i = 1; i < args.Length; i++) sb.Append('"').Append(args[i].Replace("\"", "\\\"")).Append("\" ");
                sb.Append(ForceDx11).Append(' ').Append(RelaunchMark);
                long r = (long)ShellExecuteW(System.IntPtr.Zero, "open", exe, sb.ToString(), System.IO.Path.GetDirectoryName(exe), 1);   // 1.5 exe 14c: was Process.Start (IL2CPP: 'Native error= Success', no new start)
                if (r <= 32) throw new System.Exception("ShellExecute returned " + r);
                Debug.Log("[OpenZ Options] graphics API DX11: starting again with " + ForceDx11);
                Application.Quit();
            }
            catch (System.Exception e) { Debug.LogWarning("[OpenZ Options] graphics API DX11: the new start failed, staying on DX12 (" + e.Message + ")"); }
        }
    }
}
