// OpenZ (GPL-3.0)
//  * Every flyby Resources/OpenZ/Menu/<id>_preview[_k] (2560x1440, 60 fps) has a LIGHT twin <id>_preview[_k]_1080p30 (1920x1080,
//    30 fps): the same frames (every second one), the same cuts, the same sound track.
//    (View/OzMenuPerf.cs, "the AUTO pick"). Only a PC that falls behind switches to the light copies, and that is remembered on this
//    PC (PlayerPrefs oz_menu_video_auto, with the graphics chip it was measured on: a new graphics card measures again). A later start
//    on that PC opens the light copies at once. A PC that keeps up never changes: today's 2560x1440 60 fps.
//  * FULL / LIGHT: the same choice by hand (oz_menu_video 1 / 2). There is no Options row in 1.3 (SOLUTION.md section 3); Name / Step
//  * The still-picture fallback (no clip, or a VideoPlayer error) is untouched (UI/OzLevelPreview.cs).
//  * 1.3 item 31 (GitHub issue 2, Linux): the Linux player has no H.264 decoder, so every 1.2 flyby failed there and the menu showed
//    the still. Linux now opens its own copies <name>_vp8 (VP8 .webm, 1920x1080, 30 fps: item 23's light frames; Vorbis sound). The
//    AUTO probe does not run there (one size only). Windows and Mac never open them (and their builds leave them out:
//    fails on purpose: the still path's counted check).
using UnityEngine;

namespace OpenZ
{
    public static class OzMenuVideo
    {
        public const string Pref = "oz_menu_video", PrefAuto = "oz_menu_video_auto", PrefAutoGpu = "oz_menu_video_auto_gpu";
        public const int Auto = 0, Full = 1, Light = 2;
        public static readonly string[] Names = { "AUTO", "FULL", "LIGHT" };
        public const string Label = "MENU VIDEO";
        /// <summary>The light twin's name: &lt;full clip name&gt; + this.</summary>
        public const string Suffix = "_1080p30";

        // ------------------------------------------------------------------ 1.3 item 31: the Linux copies (GitHub issue 2)
        /// <summary>The Linux twin's name: &lt;full clip name&gt; + this (VP8 .webm, 1920x1080, 30 fps, Vorbis sound; item 23's light frames).
        /// Unity's manual (video file compatibility): the format for Linux is a .webm container with VP8 video and Vorbis sound.</summary>
        public const string LinuxSuffix = "_vp8";
        /// <summary>This player cannot decode the H.264 .mp4 flybys: the Linux player and the Linux editor.</summary>
        public static bool NoH264 => Application.platform == RuntimePlatform.LinuxPlayer || Application.platform == RuntimePlatform.LinuxEditor;
        /// <summary></summary>
        public static bool ProofLinuxSet;
        /// <summary>Checks only: every flyby's player is pointed at a missing file, so the real VideoPlayer error path runs; never saved.</summary>
        public static bool ProofForceFail;
        static bool devLinux, devFail;
        /// <summary>The menu opens the Linux VP8 copies: always on Linux; elsewhere only for a check.</summary>
        public static bool LinuxSet { get { ReadArgs(); return NoH264 || devLinux || ProofLinuxSet; } }
        /// <summary>Every flyby fails on purpose (dev flag or a check).</summary>
        public static bool ForceFail { get { ReadArgs(); return devFail || ProofForceFail; } }

        // ------------------------------------------------------------------ the AUTO pick's numbers (SOLUTION.md section 3 says why)
        /// <summary>Seconds of playing video ignored first (decoder start, the first frames of a cut, a first-show shader compile).</summary>
        public const float WarmUp = 2f;
        /// <summary>Seconds of playing video per measured window.</summary>
        public const float Window = 4f;
        /// <summary>Windows in a row that decide: this many failing windows = the light copies; this many good ones = keep the full flybys.</summary>
        public const int Decide = 2;
        /// <summary>The probe gives up (no change) after this many windows without two in a row either way.</summary>
        public const int MaxWindows = 8;
        /// <summary>Video: new pictures on screen a second must reach this share of the rate the screen can show (the clip's rate, or less
        /// under a lower FRAME RATE LIMIT / refresh rate). 0.85 of 60 = 51.</summary>
        public const float MinShownShare = 0.85f;
        /// <summary>Video: frames the VideoPlayer skipped to catch up (frameDropped) must stay under this share of the clip's rate (3 a second at 60).</summary>
        public const float MaxDropShare = 0.05f;
        /// <summary>Menu frames a second: the in-game slow-computer tip's own line (UI/OpenZHud.SlowTip.cs: 45 fps, or 75 % of a lower limit).</summary>
        public const float SlowFps = 45f, SlowFpsShare = 0.75f;
        /// <summary>A single frame longer than this is a hitch (loading, a garbage collection), not "falling behind": left out of the window,
        /// as the slow-computer tip does (SlowTipHitch 0.25 s).</summary>
        public const float HitchSec = 0.25f;
        /// <summary>A SLIDE SHOW is not a hitch: frames over HitchSec (each under SlideMaxFrame; a longer one is a stall, such as a dragged
        /// window) for this many seconds in a row count as one window that falls behind.</summary>
        public const float SlideRunSec = 6f, SlideMaxFrame = 2f;

        static int session = -1;                                                                           // a dev flag's choice for this session only (never saved)
        static bool argsRead;
        static void ReadArgs()
        {
            if (argsRead) return; argsRead = true;
            if (GameBoot.DevArg("-ozmenuvideofull")) session = Full;
            else if (GameBoot.DevArg("-ozmenuvideolight")) session = Light;
            if (session >= 0) Debug.Log($"[OpenZ MenuPerf] menu video {Names[session]} for this session (dev flag)");
            devLinux = GameBoot.DevArg("-ozmenuvideolinux"); devFail = GameBoot.DevArg("-ozmenuvideofail");   // item 31
            if (devLinux) Debug.Log("[OpenZ MenuPerf] menu video: the Linux VP8 copies for this session (dev flag -ozmenuvideolinux)");
            if (devFail) Debug.Log("[OpenZ MenuPerf] menu video: every flyby fails on purpose in this session (dev flag -ozmenuvideofail)");
        }

        /// <summary>AUTO (default) / FULL / LIGHT; saved at once.</summary>
        public static int Mode
        {
            get { ReadArgs(); return session >= 0 ? session : Mathf.Clamp(PlayerPrefs.GetInt(Pref, Auto), Auto, Light); }
            set { session = -1; PlayerPrefs.SetInt(Pref, Mathf.Clamp(value, Auto, Light)); PlayerPrefs.Save(); Debug.Log($"[OpenZ MenuPerf] menu video set to {Name} (oz_menu_video, saved)"); }
        }
        public static string Name => Names[Mode];
        /// <summary>For an Options cycle row (AUTO / FULL / LIGHT), if one is added.</summary>
        public static void Step(int d) => Mode = ((Mode + d) % 3 + 3) % 3;

        static string Gpu => SystemInfo.graphicsDeviceName ?? "";
        /// <summary></summary>
        public static bool AutoLight => PlayerPrefs.GetInt(PrefAuto, 0) == Light && PlayerPrefs.GetString(PrefAutoGpu, "") == Gpu;
        /// <summary>The copies the menu opens with.</summary>
        public static bool UseLight => LinuxSet || Mode == Light || (Mode == Auto && AutoLight);                 // item 31: the Linux copies are 1920x1080 30 fps
        /// <summary></summary>
        public static bool Probing => !LinuxSet && Mode == Auto && !AutoLight;
        public static string Why => LinuxSet ? (NoH264 ? "LINUX: the VP8 copies (this player plays no H.264)" : "the Linux VP8 copies for a check")
                                  : Mode == Light ? "LIGHT by hand" : Mode == Full ? "FULL by hand" : AutoLight ? "AUTO: this PC fell behind before, remembered" : "AUTO: measuring";

        /// <summary></summary>
        public static void RememberLight(string why)
        {
            PlayerPrefs.SetInt(PrefAuto, Light); PlayerPrefs.SetString(PrefAutoGpu, Gpu); PlayerPrefs.Save();
            Debug.Log($"[OpenZ MenuPerf] menu video AUTO: this PC falls behind with the full flybys ({why}) -> the light copies 1920x1080 30 fps from now on, remembered on this PC (oz_menu_video_auto, graphics {Gpu})");
        }

        /// <summary>Checks: the AUTO verdict forgotten (the next start measures again).</summary>
        public static void ProofForget() { PlayerPrefs.DeleteKey(PrefAuto); PlayerPrefs.DeleteKey(PrefAutoGpu); PlayerPrefs.Save(); }

        /// <summary>The frames a second the screen can show at most now: the vsync refresh, else the FRAME RATE LIMIT, else none (0).</summary>
        public static float ScreenCap()
        {
            if (QualitySettings.vSyncCount > 0) { double hz = Screen.currentResolution.refreshRateRatio.value; return hz > 1.0 ? (float)(hz / QualitySettings.vSyncCount) : 0f; }
            return Application.targetFrameRate > 0 ? Application.targetFrameRate : 0f;
        }

        /// <summary>One measured window of playing video.</summary>
        public struct Sample
        {
            public float Seconds;          // playing time counted (hitches left out)
            public int MenuFrames;         // menu frames in it
            public int Shown;              // video pictures that could reach the screen: delivered (frame-index steps minus dropped), at most one a menu frame
            public int Dropped;            // frames the VideoPlayer skipped to catch up (frameDropped)
            public float ClipFps;          // the playing clip's rate
            public float Cap;              // ScreenCap() (0 = none)
            public float Fps => MenuFrames / Mathf.Max(0.001f, Seconds);
            public float ShownPerSec => Shown / Mathf.Max(0.001f, Seconds);
            public float DropPerSec => Dropped / Mathf.Max(0.001f, Seconds);
            /// <summary>The new pictures a second a PC that keeps up shows: the clip's rate, or the screen's lower cap.</summary>
            public float Expected => Cap > 0f ? Mathf.Min(ClipFps, Cap) : ClipFps;
            public float FpsLine => Cap > 0f ? Mathf.Min(SlowFps, Cap * SlowFpsShare) : SlowFps;
            public override string ToString() => $"{Fps:0.0} fps (line {FpsLine:0}), video {ShownPerSec:0.0} of {Expected:0} new pictures a second (line {Expected * MinShownShare:0.0}), {DropPerSec:0.0} dropped a second (line {ClipFps * MaxDropShare:0.0}), {Seconds:0.0} s";
        }

        /// <summary>The rule: a window falls behind when ANY of the three misses its line.</summary>
        public static bool FallsBehind(Sample s, out string why)
        {
            why = null;
            if (s.Seconds <= 0f || s.ClipFps <= 0f) return false;
            if (s.ShownPerSec < s.Expected * MinShownShare) why = "video pictures";
            else if (s.DropPerSec > s.ClipFps * MaxDropShare) why = "dropped frames";
            else if (s.Fps < s.FpsLine) why = "menu fps";
            return why != null;
        }
    }
}
