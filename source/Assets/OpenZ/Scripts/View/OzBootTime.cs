// OpenZ (GPL-3.0) - 1.5, kit player_reports_1111, P2 (players on 1.0 / 1.1, 11 Nov 2026: "takes minutes to load on the first run of
// the day ... if I use a shortcut! If i use the exe directly, it's all good", "First startup 3 minutes", "Shortcut do not start the game").
// Start-up timing in Player.log, so a player's log shows where the minutes go. Every line starts "[OpenZ Startup] +<seconds> s:", the
// seconds counted from the moment Windows started the game's process (the engine's own loading before the first game code included;
// Unity's clock when the process start cannot be read):
//   game code starts (with: the time of day the process started, how long the system has been up, whether the working folder is the
//     game's folder, whether the game's folder is a cloud-synced placeholder (file attributes only), the free space on its drive: no
//     folder names, nothing personal)
//   first scene <name> loaded
//   main menu ready (first frame shown)                                   View/OzMenuPerf.cs, its "first picture" moment
//   shader warm-up start / shader warm-up end                             View/OzShaderWarm.cs
//   menu flyby <level> prepared (once per level)                          UI/OzLevelPreview.cs
//   loading-screen warm list <level>: start / end                         View/OzLevelWarm.cs
// Built games only (the editor has its own timing); nothing is sent anywhere, nothing is saved.
// P2b (a player on 1.4, 11 Nov 2026; reproduced on our PC
// with the 1.4 and the 1.5 test exe: kit player_reports_1111 tools/shortcut_menu_test.py + tools/startupinfo_isolate.py): a start
// through ANY Windows shortcut (.lnk: the desktop, Send to, the Start menu, the taskbar) hands the game one thing a double-click on
// OpenZ.exe does not: the STARTUPINFO flag STARTF_TITLEISLINKNAME (0x800; the title field then holds the shortcut's path). With that
// flag the engine stops right after its "Using Windows.Gaming.Input" line: no window, the process waits for good. The game's first
// code (SubsystemRegistration, before that point) takes the flag out of this process's own start-up parameters (the PEB's
// RTL_USER_PROCESS_PARAMETERS.WindowFlags, what GetStartupInfo reads), so the engine goes on as after a double-click on the exe.
// Windows 64-bit only; the "game code starts" line says "started from a shortcut: yes". Nothing else of the start changes.
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OpenZ
{
    public static class OzBootTime
    {
        public const string Tag = "[OpenZ Startup]";
        /// <summary>Seconds between the process start and Unity's clock zero (0 when the process start could not be read).</summary>
        static double offset;
        static bool on, firstScene;
        static readonly HashSet<string> said = new HashSet<string>();
        /// <summary>The lines of this run (checks).</summary>
        public static readonly List<string> Lines = new List<string>();

        /// <summary>Seconds since the game's process started (built games), Unity's clock in the editor.</summary>
        public static double Since => Math.Max(0.0, Time.realtimeSinceStartupAsDouble + offset);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Boot()
        {
            said.Clear(); Lines.Clear(); firstScene = false; offset = 0.0;
            on = !Application.isEditor;
            if (!on) return;
            string shortcut = ShortcutStart();                                                              // P2b: first of all, before the engine goes on
            string started = "?", clock = "";
            try
            {
                using (var p = System.Diagnostics.Process.GetCurrentProcess())
                {
                    DateTime st = p.StartTime;
                    offset = Math.Max(0.0, (DateTime.Now - st).TotalSeconds - Time.realtimeSinceStartupAsDouble);
                    started = st.ToString("HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
                }
            }
            catch (Exception) { offset = 0.0; clock = "; the process start could not be read: Unity's clock"; }
            if (started == "?" && StartWin(out DateTime stw))
            {   // fixes_r5 (A11): the IL2CPP player has no Process.StartTime: kernel32 GetProcessTimes
                offset = Math.Max(0.0, (DateTime.Now - stw).TotalSeconds - Time.realtimeSinceStartupAsDouble);
                started = stw.ToString("HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture); clock = "";
            }
            Mark("game code starts (process started " + started + ", system up " + Uptime() + ", " + Folders() + ", started from a shortcut: " + shortcut + ", " + OzVersion.Current + clock + ")");
            SceneManager.sceneLoaded -= OnSceneLoaded; SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene s, LoadSceneMode mode)
        {
            if (firstScene) return;
            firstScene = true;
            Mark("first scene " + s.name + " loaded");
        }

        /// <summary>One start-up line (built games only).</summary>
        public static void Mark(string what)
        {
            if (!on) return;
            string l = $"{Tag} +{Since:0.0} s: {what}";
            Lines.Add(l);
            Debug.Log(l);
        }

        /// <summary>One start-up line, only the first time this key comes up in this run.</summary>
        public static void Once(string key, string what)
        {
            if (!on || !said.Add(key)) return;
            Mark(what);
        }

        /// <summary>P2b: STARTF_TITLEISLINKNAME, set by Windows for every start through a .lnk shortcut.</summary>
        public const int TitleIsLinkName = 0x800;
        /// <summary>x64: the PEB's ProcessParameters pointer (+0x20) and RTL_USER_PROCESS_PARAMETERS.WindowFlags (+0xA4).</summary>
        public const int PebParams = 0x20, ParamsWindowFlags = 0xA4;

        /// <summary>P2b: takes STARTF_TITLEISLINKNAME out of this process's start-up parameters. "yes (...)" when it was set,
        /// "no", or "?" (not Windows 64-bit, or the read failed: nothing changed).</summary>
        static string ShortcutStart()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (IntPtr.Size != 8) return "?";
            try
            {
                var pbi = new BasicInfo();
                if (NtQueryInformationProcess(GetCurrentProcess(), 0, ref pbi, Marshal.SizeOf(typeof(BasicInfo)), out _) != 0 || pbi.PebBaseAddress == IntPtr.Zero) return "?";
                IntPtr pp = Marshal.ReadIntPtr(pbi.PebBaseAddress, PebParams);
                if (pp == IntPtr.Zero) return "?";
                int flags = Marshal.ReadInt32(pp, ParamsWindowFlags);
                if ((flags & TitleIsLinkName) == 0) return "no";
                Marshal.WriteInt32(pp, ParamsWindowFlags, flags & ~TitleIsLinkName);
                return "yes (the shortcut's start flag taken out: the engine starts as from the exe)";
            }
            catch (Exception) { return "?"; }
#else
            return "?";
#endif
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        [StructLayout(LayoutKind.Sequential)]
        struct BasicInfo { public IntPtr ExitStatus, PebBaseAddress, AffinityMask, BasePriority, UniqueProcessId, InheritedFromUniqueProcessId; }
        [DllImport("ntdll.dll")] static extern int NtQueryInformationProcess(IntPtr process, int infoClass, ref BasicInfo info, int size, out int returned);
        [DllImport("kernel32.dll")] static extern IntPtr GetCurrentProcess();
#endif

        static string Uptime()
        {
            try
            {
                long ms = (uint)Environment.TickCount;                                                     // up to 49 days
                long min = ms / 60000;
                return min < 60 ? min + " min" : (min / 60) + " h " + (min % 60).ToString("00") + " min";
            }
            catch (Exception) { return "?"; }
        }

        /// <summary>"working folder = the game's folder: yes, game folder cloud-synced: no (UnityPlayer.dll: no), free on its drive 41 GB" (no names).</summary>
        // fixes_r5 (A11): the process start and the free disk through kernel32 (the IL2CPP player has neither Process.StartTime nor DriveInfo)
        [DllImport("kernel32.dll", SetLastError = true)] static extern bool GetProcessTimes(IntPtr process, out long creation, out long exit, out long kernel, out long user);
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)] static extern bool GetDiskFreeSpaceEx(string dir, out ulong freeForCaller, out ulong total, out ulong totalFree);
        static bool StartWin(out DateTime start)
        {
            start = default;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            try { if (GetProcessTimes(GetCurrentProcess(), out long c, out _, out _, out _) && c > 0) { start = DateTime.FromFileTime(c); return true; } } catch (Exception) { }
#endif
            return false;
        }
        static string FreeWin(string dir)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            try { if (GetDiskFreeSpaceEx(Path.GetPathRoot(dir), out ulong f, out _, out _)) return (f / (1024.0 * 1024 * 1024)).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " GB"; } catch (Exception) { }
#endif
            return "?";
        }

        static string Folders()
        {
            string game = null;
            try { game = Path.GetFullPath(Path.GetDirectoryName(Application.dataPath)); } catch (Exception) { }
            string wd = "?", od = "?", free = "?";
            try
            {
                string cur = Path.GetFullPath(Directory.GetCurrentDirectory());
                if (game != null) wd = string.Equals(cur.TrimEnd('\\', '/'), game.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase) ? "yes" : "no (another folder)";
            }
            catch (Exception) { }
            try
            {
                if (game != null)
                {
                    string dll = Path.Combine(game, "UnityPlayer.dll");
                    od = CloudState(game) + " (UnityPlayer.dll: " + (File.Exists(dll) ? CloudState(dll) : "?") + ")";
                }
            }
            catch (Exception) { }
            try
            {
                if (game != null)
                {
                    var d = new DriveInfo(Path.GetPathRoot(game));
                    free = (d.AvailableFreeSpace / (1024.0 * 1024 * 1024)).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " GB";
                }
            }
            catch (Exception) { }
            if (free == "?" && game != null) free = FreeWin(game);                                      // fixes_r5 (A11): no DriveInfo in the IL2CPP player
            return "working folder = the game's folder: " + wd + ", game folder cloud-synced: " + od + ", free on its drive " + free;
        }

        /// <summary>The cloud-sync placeholder state of a file or folder from its attributes alone (no names): a synced folder's
        /// placeholders carry a reparse point, online-only files the recall / offline bits (their first read downloads them), a file
        /// kept on the device the pinned bit. A plain local file: "no".</summary>
        static string CloudState(string path)
        {
            try
            {
                int a = (int)File.GetAttributes(path);
                if ((a & (0x400000 | 0x40000 | 0x1000)) != 0) return "online-only, downloaded on first read";   // RECALL_ON_DATA_ACCESS / RECALL_ON_OPEN / OFFLINE
                if ((a & 0x100000) != 0) return "synced, not kept on the device";                                 // UNPINNED
                if ((a & 0x80000) != 0) return "synced, kept on the device";                                       // PINNED
                if ((a & 0x400) != 0) return "reparse point (synced or linked)";                                   // REPARSE_POINT
                return "no";
            }
            catch (Exception) { return "?"; }
        }
    }
}
