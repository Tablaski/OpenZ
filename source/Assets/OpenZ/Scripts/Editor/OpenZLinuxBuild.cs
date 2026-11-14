using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// The EXPERIMENTAL Linux player: a build method of its own beside the Windows release menu and the Mac method, which it never
    /// calls or changes. Menu items, used in this order, each safe to call again:
    ///   OpenZ/Linux experimental/1 Switch the editor to Linux       (the first switch imports the project again for the Linux target)
    ///   OpenZ/Linux experimental/2 Build the Linux test app         (once per editor session, behind a SessionState guard)
    ///   OpenZ/Linux experimental/3 Switch the editor back to Windows
    ///  * target Linux x86-64, Mono, non-development; the same scenes, scripting define, company name, version tag and player
    ///    settings as the Windows release build, for this build only (restored afterwards);
    ///  * needs the editor's Linux build module (Linux Build Support, Mono); this file only names the core build target, so it
    ///    also compiles in an editor without that module (the menu items then say the module is missing);
    ///  * output: release\OpenZ_&lt;tag&gt;_linux_EXPERIMENTAL\OpenZ.x86_64 (+ OpenZ_Data, UnityPlayer.so), and NEXT to that folder
    ///    the build record OpenZ_&lt;tag&gt;_linux_EXPERIMENTAL_BUILD_INFO.txt. The header of the built program is read back: it must
    ///    be a 64-bit x86-64 ELF file, else the build counts as not usable;
    ///  * 1.3 item 31 (GitHub issue 2): the graphics list Vulkan first, OpenGL Core second, for this build only (restored afterwards).
    ///    With the engine's automatic list the 1.2 Linux player ran on OpenGL Core (the player's log) although HOW_TO_START_ON_LINUX
    ///    says it uses Vulkan; a PC without Vulkan still falls back to OpenGL Core by itself. The menu flybys: the H.264 .mp4 stay out
    ///    of this build and the VP8 copies go in (Editor/OzMenuVideoBuildFilter.cs); the build record counts them;
    ///    keeps the run permission) is done by the post-build script outside the editor.
    /// The result has never been run on a real Linux machine: it is handed out as it is, labelled experimental.
    /// A heavy job: run it alone (no Play, no render).
    /// </summary>
    public static class OpenZLinuxBuild
    {
        const string GuardKey = "oz_linux_experimental_build_started";
        const string ResultKey = "oz_linux_experimental_build_result";
        const string FolderSuffix = "_linux_EXPERIMENTAL";
        const string ProgramName = "OpenZ.x86_64";
        static readonly string[] RequiredScenes = { "Intro", "MainMenu", "L02_Desert", "L07_Volcanic", "L16_Jungle", "L17_CarPark" };

        static string JobRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "../../../.."));
        static bool ModuleSeen => BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneLinux64);

        [MenuItem("OpenZ/Linux experimental/1 Switch the editor to Linux", false, 70)]
        public static string SwitchToLinux()
        {
            if (EditorApplication.isPlaying) return Note("stop Play first");
            if (EditorApplication.isCompiling) return Note("wait for the compile to finish");
            if (!ModuleSeen) return Fail("this editor does not see the Linux build module (Linux Build Support, Mono): install it, then quit the editor and start it again once");
            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.StandaloneLinux64) return Note("the editor is on Linux already");
            UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes(); AssetDatabase.SaveAssets();
            DateTime t0 = DateTime.Now;
            bool ok = EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneLinux64);
            return Note((ok ? "switched to Linux" : "the switch to Linux was REFUSED") + $" after {(DateTime.Now - t0).TotalMinutes:F1} min; active target now {EditorUserBuildSettings.activeBuildTarget}");
        }

        [MenuItem("OpenZ/Linux experimental/2 Build the Linux test app", false, 71)]
        public static string BuildLinux()
        {
            if (SessionState.GetBool(GuardKey, false))
                return Note("the Linux build was already started in this editor session and is not started twice. Its result: "
                            + SessionState.GetString(ResultKey, "none recorded yet (still running, or interrupted: read the console and the build record)"));
            if (EditorApplication.isPlaying) return Note("stop Play first");
            if (EditorApplication.isCompiling) return Note("wait for the compile to finish");
            if (!ModuleSeen) return Fail("this editor does not see the Linux build module (Linux Build Support, Mono): install it, then quit the editor and start it again once");
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneLinux64)
                return Fail("the editor is not on Linux: call '1 Switch the editor to Linux' first and wait until it has finished compiling");
            if (PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone) != ScriptingImplementation.Mono2x)
                return Fail("the standalone scripting backend is not Mono (this method builds the Mono player only)");

            // ---- pre-flight, the same two checks as the Windows release build
            var scenes = Array.ConvertAll(Array.FindAll(EditorBuildSettings.scenes, e => e.enabled), e => e.path);
            var missing = RequiredScenes.Where(r => !scenes.Any(s => Path.GetFileNameWithoutExtension(s) == r)).ToArray();
            if (missing.Length > 0) return Fail("Build Settings scenes miss " + string.Join(", ", missing));
            string introSrc = Path.Combine(Application.dataPath, "OpenZ/Scripts/Intro/OzIntroData.cs");
            if (File.Exists(introSrc) && !File.ReadAllText(introSrc).Contains("PlayerPrefs.GetInt(Pref, 0) == 1"))
                return Fail("the intro default is not OFF in OzIntroData.cs (expected PlayerPrefs.GetInt(Pref, 0) == 1)");

            SessionState.SetBool(GuardKey, true);          // from here on a repeated call builds nothing
            string result;
            try { result = Run(scenes); }
            catch (Exception e) { result = Fail("the Linux build method threw " + e.GetType().Name + ": " + e.Message); }
            SessionState.SetString(ResultKey, result.Length > 1500 ? result.Substring(0, 1500) : result);
            return result;
        }

        [MenuItem("OpenZ/Linux experimental/3 Switch the editor back to Windows", false, 72)]
        public static string SwitchBackToWindows()
        {
            if (EditorApplication.isPlaying) return Note("stop Play first");
            if (EditorApplication.isCompiling) return Note("wait for the compile to finish");
            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.StandaloneWindows64) return Note("the editor is on Windows already");
            UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes(); AssetDatabase.SaveAssets();
            DateTime t0 = DateTime.Now;
            bool ok = EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);
            return Note((ok ? "switched back to Windows" : "the switch back to Windows was REFUSED") + $" after {(DateTime.Now - t0).TotalMinutes:F1} min; active target now {EditorUserBuildSettings.activeBuildTarget}");
        }

        [MenuItem("OpenZ/Linux experimental/State", false, 90)]
        public static string State()
        {
            return Note($"Linux module seen: {ModuleSeen}; active target: {EditorUserBuildSettings.activeBuildTarget}; build started in this session: {SessionState.GetBool(GuardKey, false)}; "
                        + "last result: " + SessionState.GetString(ResultKey, "none"));
        }

        [MenuItem("OpenZ/Linux experimental/Allow one more Linux build in this session", false, 91)]
        public static string AllowOneMore()
        {
            SessionState.EraseBool(GuardKey); SessionState.EraseString(ResultKey);
            return Note("the guard is cleared: '2 Build the Linux test app' will build once more");
        }

        static string Run(string[] scenes)
        {
            UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes(); AssetDatabase.SaveAssets();
            DateTime now = DateTime.Now;
            string tag = OzVersion.Tag(now);
            string relRoot = Path.Combine(JobRoot, "release");
            string dir = Path.Combine(relRoot, "OpenZ_" + tag + FolderSuffix);
            string record = dir + "_BUILD_INFO.txt";
            Directory.CreateDirectory(relRoot);
            string kept = null;
            if (Directory.Exists(dir))
            {
                kept = dir + "_prev_" + now.ToString("yyyyMMdd_HHmmss");
                try { Directory.Move(dir, kept); }
                catch (Exception e) { return Fail("could not keep the older same-day Linux folder " + dir + ": " + e.Message); }
            }
            Directory.CreateDirectory(dir);

            // ---- the player settings the Windows release build sets (the same values; they stay as that build leaves them)
            PlayerPrefs.SetInt("oz_testmode", 0); PlayerPrefs.SetInt("oz_gate", 0); PlayerPrefs.Save();
            Debug.Log("[OpenZ Linux] " + OpenZBuild.IncludeRuntimeShaders());
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow; PlayerSettings.defaultIsNativeResolution = true; PlayerSettings.allowFullscreenSwitch = true;
            PlayerSettings.defaultScreenWidth = 1920; PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.resizableWindow = true; PlayerSettings.runInBackground = true;

            // ---- release settings, for this build only (restored in finally)
            string oldCompany = PlayerSettings.companyName, oldVersion = PlayerSettings.bundleVersion;
            bool oldAutoApi = PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.StandaloneLinux64);          // item 31
            var oldApis = PlayerSettings.GetGraphicsAPIs(BuildTarget.StandaloneLinux64);
            string apisBefore = (oldAutoApi ? "automatic: " : "") + string.Join(", ", oldApis);
            var opts = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = Path.Combine(dir, ProgramName),
                target = BuildTarget.StandaloneLinux64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None,
                extraScriptingDefines = new[] { OpenZReleaseBuild.Define },
            };

            BuildReport rep;
            try
            {
                PlayerSettings.companyName = OpenZReleaseBuild.ReleaseCompany;
                PlayerSettings.bundleVersion = tag;
                PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneLinux64, false);                // item 31: Vulkan first, OpenGL Core as the fallback
                PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneLinux64, new[] { GraphicsDeviceType.Vulkan, GraphicsDeviceType.OpenGLCore });
                OpenZReleaseBuild.Building = true;
                rep = BuildPipeline.BuildPlayer(opts);
            }
            catch (Exception e) { return Fail("BuildPlayer threw " + e.GetType().Name + ": " + e.Message); }
            finally
            {
                OpenZReleaseBuild.Building = false;
                PlayerSettings.companyName = oldCompany;
                PlayerSettings.bundleVersion = oldVersion;
                PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneLinux64, oldApis);                        // item 31: as it was
                PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneLinux64, oldAutoApi);
                OzMenuVideoBuildFilter.RestoreAll("the Linux build returned");                                 // item 31: the parked .mp4 back at once
                AssetDatabase.SaveAssets();
            }
            if (rep == null) return Fail("BuildPlayer returned no report");

            var s = rep.summary;
            string exe = Path.Combine(dir, ProgramName);
            string elf = File.Exists(exe) ? ElfProcessor(exe) : "no program (" + exe + ")";
            bool built = s.result == BuildResult.Succeeded && (s.options & BuildOptions.Development) == 0;
            bool usable = built && elf == "x86_64";

            var info = new StringBuilder();
            info.AppendLine($"OpenZ EXPERIMENTAL Linux build {OzVersion.Display(now)}  (tag {tag})");
            info.AppendLine($"result: {s.result}");
            info.AppendLine("usable: " + (usable ? "yes" : "NO"));
            info.AppendLine($"time: {now:yyyy-MM-dd HH:mm}  duration: {s.totalTime}");
            info.AppendLine($"errors: {s.totalErrors}, warnings: {s.totalWarnings}");
            info.AppendLine($"options: {s.options} (non-development), define {OpenZReleaseBuild.Define}, company {OpenZReleaseBuild.ReleaseCompany}, bundleVersion {tag} (restored to '{oldCompany}' / '{oldVersion}')");
            info.AppendLine("scenes: " + string.Join(", ", scenes.Select(Path.GetFileNameWithoutExtension)));
            info.AppendLine($"target: {s.platform}");
            info.AppendLine("program header: " + elf);
            info.AppendLine($"graphics APIs: Vulkan, OpenGLCore for this build (the project's list before and after: {apisBefore})");   // item 31
            info.AppendLine(OzMenuVideoBuildFilter.Audit(rep));
            info.AppendLine("output: " + dir);
            if (kept != null) info.AppendLine("older same-day Linux folder kept: " + kept);
            info.AppendLine(BuildMessages(rep));
            info.AppendLine("next: the post-build script for the Linux folder, outside the editor");
            try { File.WriteAllText(record, info.ToString()); }
            catch (Exception e) { Debug.LogError("[OpenZ Linux] could not write the build record " + record + ": " + e.Message); }

            if (!built) return Fail($"build {s.result} ({s.totalErrors} errors), see the console; record {record}");
            if (!usable) return Fail("the built program is not a 64-bit x86-64 ELF file (" + elf + "); record " + record);
            Debug.Log("[OpenZ Linux] " + info);
            return info.ToString();
        }

        /// <summary>The processor type in the header of an ELF file: "x86_64", or a text that says what else it is.</summary>
        public static string ElfProcessor(string path)
        {
            try
            {
                var h = new byte[20];
                using (var fs = File.OpenRead(path)) { if (fs.Read(h, 0, 20) < 20) return "too short to be a program"; }
                if (h[0] != 0x7F || h[1] != (byte)'E' || h[2] != (byte)'L' || h[3] != (byte)'F') return "not an ELF file (first bytes 0x" + BitConverter.ToString(h, 0, 4).Replace("-", "") + ")";
                if (h[4] != 2) return "a 32-bit ELF file";
                int machine = h[18] | (h[19] << 8);
                return machine == 0x3E ? "x86_64" : machine == 0xB7 ? "arm64" : "processor type 0x" + machine.ToString("X4");
            }
            catch (Exception e) { return "unreadable: " + e.Message; }
        }

        /// <summary>The error lines of the build report, and every line that names a shader, for the build record (at most 30).</summary>
        static string BuildMessages(BuildReport rep)
        {
            var lines = new List<string>(); int errors = 0, shader = 0;
            try
            {
                foreach (var step in rep.steps)
                    foreach (var m in step.messages)
                    {
                        bool isError = m.type == LogType.Error || m.type == LogType.Exception || m.type == LogType.Assert;
                        bool isShader = m.content != null && m.content.IndexOf("shader", StringComparison.OrdinalIgnoreCase) >= 0;
                        if (isError) errors++;
                        if (isShader) shader++;
                        if ((isError || isShader) && lines.Count < 30)
                        {
                            string c = (m.content ?? "").Replace("\r", " ").Replace("\n", " ");
                            lines.Add("   " + m.type + ": " + (c.Length > 300 ? c.Substring(0, 300) : c));
                        }
                    }
            }
            catch (Exception e) { lines.Add("   (the build report's messages could not be read: " + e.GetType().Name + ")"); }
            return $"build report messages: {errors} error line(s), {shader} line(s) that name a shader" + (lines.Count > 0 ? "\n" + string.Join("\n", lines) : "");
        }

        static string Note(string what) { Debug.Log("[OpenZ Linux] " + what); return what; }
        static string Fail(string why) { Debug.LogError("[OpenZ Linux] NOT BUILT: " + why); return "FAILED: " + why; }
    }
}
