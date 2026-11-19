// OpenZ (GPL-3.0)
// an NVIDIA card next to the processor's own graphics.
// ONE quit path for every way out of a built game: the main menu's QUIT (UI/OpenZMainMenu.cs Quit, after its confirm), the window's
// close button, Alt+F4, and every Application.Quit a tool calls (all of them pass Application.wantsToQuit):
//   1. every VideoPlayer is stopped (that releases its decoder: the menu flybys, the level previews, the intro film; Windows plays them
//      through Media Foundation, which can hold a quit while a video plays or prepares) and every playing AudioSource is stopped,
//   2. PlayerPrefs.Save,
//   3. the Player.log line "[OpenZ] quit: start (...)"; the engine's own quitting event adds "[OpenZ] quit: the engine shuts down",
//   4. a quit watchdog starts (a background thread): if the game still runs WatchdogSeconds later it writes
//      "[OpenZ] quit watchdog: forced exit after N s" to the log, marks the session for OzCrashLog (its log is kept in CrashLogs at the
//      next start) and ends the process. On Windows a second, native backstop (a Windows timer that calls TerminateProcess, no managed
//      code) ends it BackstopSeconds later still, in case the engine's shutdown has already stopped the managed thread,
//   5. Application.Quit (the main menu's QUIT: two frames after step 1, so the stopped players are released before the engine goes down;
//      the window close: at once, as before).
//   6. (Windows, 1.5 kit quit_loading_crash_r5, row EXE-QUIT) at the END of the engine's quitting event the game ends its own process
//      (TerminateProcess) with exit code 0, or with the code a tool passed to Application.Quit (it sets ExitCode first), after one more
//      PlayerPrefs.Save. Its handler is added at the quit's start, so every quitting handler added before (the crash-log keeper's marker
//      0xC0000005 after a quit from the main menu or during a loading screen; their crash dumps all show the engine's own shutdown on the
//      main thread inside D3D12Core.dll (the graphics device going down, a read of freed memory), after the last Player.log line. During
//      a loading screen the engine even finished the held level load inside its shutdown (the level's Awake work after "the engine shuts
//      down"). The game's own part is done by then, so only the engine's unload and its graphics shutdown are skipped. EndAtQuitting =
//      false: the engine's own shutdown as before (an A/B check only).
// The settings are saved in step 2, before anything can hang; what the player sees and what is saved do not change. Editor: none of
// this (Play stops as before). A hang inside Windows' own process exit (a driver's unload) is past any code of the game.
using System;
using System.Collections;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;
using UnityEngine.Video;

namespace OpenZ
{
    public sealed class OzQuit : MonoBehaviour
    {
        /// <summary>Seconds from the quit's start until the watchdog ends a game that is still running.</summary>
        public static float WatchdogSeconds = 8f;
        /// <summary>Windows: seconds after the watchdog until the native backstop ends the process (when the watchdog thread is gone).</summary>
        public static float BackstopSeconds = 3f;
        /// <summary>The process exit code of a forced exit.</summary>
        public const int ForcedExitCode = 1;
        /// <summary>The quit has started (this run).</summary>
        public static bool Started { get; private set; }
        /// <summary>What started it.</summary>
        public static string Why { get; private set; } = "";
        /// <summary>1.5 kit quit_loading_crash_r5 (step 6): a built game on Windows ends its process at the end of the engine's quitting
        /// event (no engine unload, no graphics shutdown: the D3D12 crash of exe 13a). False = the engine's own shutdown as before.</summary>
        public static bool EndAtQuitting = true;
        /// <summary>1.5 kit quit_loading_crash_r5: the exit code of that end: 0, or the code a tool passes to Application.Quit (the tool
        /// sets it first: CraterField's self test, OzFpsBench).</summary>
        public static int ExitCode;
        static bool appQuitSeen;

        static OzQuit host;
        static Thread watchdog;
        static volatile bool backstopArmed;
        static int pid = -1;
        static string markerPath;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Started = false; Why = ""; host = null; watchdog = null; ExitCode = 0; appQuitSeen = false; }   // + 1.5 kit quit_loading_crash_r5

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            if (Application.isEditor) return;
            Application.wantsToQuit -= WantsToQuit; Application.wantsToQuit += WantsToQuit;
            Application.quitting -= Quitting; Application.quitting += Quitting;
            try { using (var p = System.Diagnostics.Process.GetCurrentProcess()) pid = p.Id; } catch (Exception) { pid = -1; }
            try { if (!string.IsNullOrEmpty(OzCrashLog.Dir)) markerPath = Path.Combine(Path.GetDirectoryName(OzCrashLog.Dir), OzCrashLog.Marker); } catch (Exception) { markerPath = null; }
            var go = new GameObject("OzQuit") { hideFlags = HideFlags.HideInHierarchy };
            DontDestroyOnLoad(go);
            host = go.AddComponent<OzQuit>();
        }

        /// <summary>The main menu's QUIT (after its confirm): the quit's start (steps 1-4), then Application.Quit two frames later.</summary>
        public static void Request(string why)
        {
            if (Application.isEditor) { Application.Quit(); return; }
            Prepare(why);
            if (host) host.StartCoroutine(host.QuitSoon());
            else Application.Quit();
        }

        IEnumerator QuitSoon()
        {
            yield return null;                                                                              // the stopped players release their decoders
            yield return null;
            Application.Quit();
        }

        /// <summary>Every quit of a built game passes here (the window close, Alt+F4, Application.Quit): never refused.</summary>
        static bool WantsToQuit()
        {
            try { Prepare("window close / Alt+F4 / Application.Quit"); } catch (Exception) { }
            return true;
        }

        /// <summary>The engine's own quitting event: one line, so a player's log shows the quit went past the game's part.</summary>
        static void Quitting()
        {
            try { Debug.Log($"[OpenZ] quit: the engine shuts down (Application.quitting; {(Started ? Why : "no quit start seen")})"); } catch (Exception) { }
        }

        /// <summary>The engine's OnApplicationQuit (sent to every object before it shuts down): logged with the end (step 6).</summary>
        void OnApplicationQuit() { appQuitSeen = true; }

        /// <summary>1.5 kit quit_loading_crash_r5: step 6 joins the quitting event at the quit's start, after every handler added before it.</summary>
        static void HookEnd()
        {
#if UNITY_STANDALONE_WIN
            if (Application.isEditor || !EndAtQuitting) return;
            Application.quitting -= EndNow; Application.quitting += EndNow;
#endif
        }

        /// <summary>1.5 kit quit_loading_crash_r5 (row EXE-QUIT, step 6 of the notes): the end of a built game's quit on Windows, the last
        /// quitting handler. The settings once more, one line, then the process ends with ExitCode: the engine's unload of the scene (and a
        /// held level load it would finish first) and its graphics shutdown, where exe 13a crashed, never run.</summary>
        static void EndNow()
        {
#if UNITY_STANDALONE_WIN
            if (Application.isEditor || !EndAtQuitting || !Started) return;
            bool saved = SaveSettings();                                                                     // what an OnApplicationQuit / quitting handler wrote after the start
            try { Debug.Log($"[OpenZ] quit: the game ends here (exit code {ExitCode}; {Why}; settings {(saved ? "saved" : "NOT saved")}; OnApplicationQuit {(appQuitSeen ? "sent" : "not sent yet")}; the engine's unload and graphics shutdown are skipped)"); } catch (Exception) { }
            try { TerminateProcess(GetCurrentProcess(), (uint)ExitCode); } catch (Exception) { }
#endif
        }

        static void Prepare(string why)
        {
            if (Started) return;
            Started = true; Why = why;
            int videos = 0, sounds = 0;
            try
            {
                foreach (var vp in FindObjectsByType<VideoPlayer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (!vp) continue;
                    try { vp.Stop(); videos++; } catch (Exception) { }
                }
            }
            catch (Exception) { }
            try
            {
                foreach (var a in FindObjectsByType<AudioSource>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                {
                    if (!a || !a.isPlaying) continue;
                    try { a.Stop(); sounds++; } catch (Exception) { }
                }
            }
            catch (Exception) { }
            HeatGuardRuntime.StopPolling();                                                                  // no new temperature reading starts while the game shuts down
            bool saved = SaveSettings();
            Debug.Log($"[OpenZ] quit: start ({why}; {videos} video player(s) and {sounds} sound(s) stopped, settings {(saved ? "saved" : "NOT saved")}; watchdog {WatchdogSeconds:0} s)");
            StartWatchdog();
            HookEnd();                                                                                       // 1.5 kit quit_loading_crash_r5: step 6 (Windows)
        }

        /// <summary>PlayerPrefs.Save that never throws: at the quit's start (step 2) and once more just before the Windows end (step 6).</summary>
        static bool SaveSettings()
        {
            try { PlayerPrefs.Save(); return true; } catch (Exception) { return false; }
        }

        static void StartWatchdog()
        {
            if (watchdog != null) return;
            try
            {
                watchdog = new Thread(Watch) { IsBackground = true, Name = "OpenZ quit watchdog" };
                watchdog.Start();
            }
            catch (Exception) { watchdog = null; }
            ArmBackstop((WatchdogSeconds + BackstopSeconds) * 1000f);
        }

        static void Watch()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (sw.Elapsed.TotalSeconds < WatchdogSeconds) Thread.Sleep(100);
            string line = $"[OpenZ] quit watchdog: forced exit after {sw.Elapsed.TotalSeconds:0.0} s ({Why}; the settings were saved when the quit started)";
            var log = new Thread(() => { try { Debug.Log(line); } catch (Exception) { } }) { IsBackground = true };   // never wait on a stuck log for long
            try { log.Start(); log.Join(1500); } catch (Exception) { }
            KeepThisSessionsLog();
            Thread.Sleep(200);                                                                              // the line reaches Player.log
            End();
        }

        /// <summary>OzCrashLog's marker with this process id: the next start keeps this session's log in CrashLogs (a forced close).</summary>
        static void KeepThisSessionsLog()
        {
            try { if (markerPath != null && pid > 0) File.WriteAllText(markerPath, pid.ToString()); } catch (Exception) { }
        }

        static void End()
        {
#if UNITY_STANDALONE_WIN
            try { TerminateProcess(GetCurrentProcess(), (uint)ForcedExitCode); } catch (Exception) { }
#endif
            try { using (var p = System.Diagnostics.Process.GetCurrentProcess()) p.Kill(); } catch (Exception) { }
        }

        /// <summary>Windows: a one-shot Windows timer whose callback is TerminateProcess itself (parameter: this process), so it ends the
        /// process even when no managed thread runs any more. Nothing on other systems.</summary>
        static void ArmBackstop(float ms)
        {
#if UNITY_STANDALONE_WIN
            if (backstopArmed) return;
            try
            {
                IntPtr k32 = GetModuleHandleW("kernel32.dll");
                IntPtr term = k32 != IntPtr.Zero ? GetProcAddress(k32, "TerminateProcess") : IntPtr.Zero;
                if (term == IntPtr.Zero) return;
                backstopArmed = CreateTimerQueueTimer(out _, IntPtr.Zero, term, new IntPtr(-1), (uint)Mathf.Max(1000f, ms), 0u, 0x8u);   // WT_EXECUTEONLYONCE; -1 = this process
            }
            catch (Exception) { }
#endif
        }

#if UNITY_STANDALONE_WIN
        [DllImport("kernel32.dll")] static extern IntPtr GetCurrentProcess();
        [DllImport("kernel32.dll")] static extern bool TerminateProcess(IntPtr process, uint exitCode);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern IntPtr GetModuleHandleW(string name);
        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true)] static extern IntPtr GetProcAddress(IntPtr module, string name);
        [DllImport("kernel32.dll")] static extern bool CreateTimerQueueTimer(out IntPtr timer, IntPtr queue, IntPtr callback, IntPtr parameter, uint dueMs, uint periodMs, uint flags);
#endif
    }
}
