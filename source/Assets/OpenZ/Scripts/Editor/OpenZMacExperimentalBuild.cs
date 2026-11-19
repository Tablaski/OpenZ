using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// The EXPERIMENTAL macOS player: a build method of its own beside the Windows release menu, which it never calls or changes.
    /// Three menu items, used in this order, each safe to call again:
    ///   OpenZ/Mac experimental/1 Switch the editor to macOS       (the first switch imports the project again for the Mac target)
    ///   OpenZ/Mac experimental/2 Build the Mac test app            (once per editor session, behind a SessionState guard)
    ///   OpenZ/Mac experimental/3 Switch the editor back to Windows
    ///    target macOS UNIVERSAL (Intel 64-bit + Apple chip in one app, Unity's x64ARM64 player), Mono, non-development, so both kinds
    ///    of Mac run it natively; the same scenes, scripting define, company name, version tag and player settings as the Windows
    ///    release build, for this build only (restored afterwards). Unity's own Mac build step gives every program file an ad-hoc
    ///    signature on Windows too (MacOSCodeSigning; the 1.2 files carry one: CodeDirectory flags 0x2), one per processor slice,
    ///    which is all an Apple-chip program needs to start; after the post-build edits the chain seals them again (mac_reseal_1_3.py).
    ///    The fallback items build Apple chip only (beside an Intel set, never instead of a working one) or Intel only (the 1.2 build).
    ///    1.2 and older: Intel 64-bit only (AskForIntel64); the Apple-chip Macs ran it through Rosetta 2;
    ///  * the Mac build module is used without a compile-time reference (its architecture setting is found by name), so this file
    ///    also compiles in an editor that has no Mac module installed;
    ///  * output: release\OpenZ_&lt;tag&gt;_mac_EXPERIMENTAL\OpenZ.app (Apple chip only: _mac_applechip_EXPERIMENTAL), and NEXT to that
    ///    folder the build record OpenZ_&lt;tag&gt;_mac_EXPERIMENTAL_BUILD_INFO.txt. The header of EVERY program file in the app (the
    ///    executable, Frameworks, PlugIns) is read back: the record names each one's processor types and whether each slice carries a
    ///    signature, and the build counts as not usable when one of them lacks a processor type the build asked for or a signature;
    ///    that keeps the Unix run permission) is done by the post-build scripts outside the editor.
    /// The result is ad-hoc signed only (no Apple certificate) and has never been run on a real Mac: it is handed out as it is,
    /// labelled experimental, until a player has started it on an Apple-chip Mac.
    /// A heavy job: run it alone (no Play, no render).
    /// </summary>
    public static class OpenZMacExperimentalBuild
    {
        const string GuardKey = "oz_mac_experimental_build_started";
        const string ResultKey = "oz_mac_experimental_build_result";
        const string MacBundleId = "com.OpenZ.OpenZ";
        const string FolderSuffix = "_mac_EXPERIMENTAL";
        /// <summary>1.3 item 13: the processor choice of a build. Universal = the default menu item; AppleChip = the fallback beside an
        /// Intel set (its own folder); Intel = the 1.2 build.</summary>
        public enum MacArch { Universal, AppleChip, Intel }
        /// <summary>The module's own names (UnityEditor.Build.OSArchitecture in this editor's Mac module: x64, ARM64, x64ARM64) and the
        /// processor types every program file must then carry.</summary>
        static string ArchName(MacArch a) => a == MacArch.Universal ? "x64ARM64" : a == MacArch.AppleChip ? "ARM64" : "x64";
        static string ArchWant(MacArch a) => a == MacArch.Universal ? "arm64+x86_64" : a == MacArch.AppleChip ? "arm64" : "x86_64";
        static string ArchWords(MacArch a) => a == MacArch.Universal ? "Universal (Intel 64-bit + Apple chip)" : a == MacArch.AppleChip ? "Apple chip only (arm64)" : "Intel 64-bit only";
        static string Suffix(MacArch a) => a == MacArch.AppleChip ? "_mac_applechip_EXPERIMENTAL" : FolderSuffix;
        static readonly string[] RequiredScenes = { "Intro", "MainMenu", "L02_Desert", "L07_Volcanic", "L16_Jungle", "L17_CarPark", "L18_BridgeGame" };

        static string JobRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "../../../.."));
        static bool ModuleSeen => BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneOSX);

        [MenuItem("OpenZ/Mac experimental/1 Switch the editor to macOS", false, 60)]
        public static string SwitchToMac()
        {
            if (EditorApplication.isPlaying) return Note("stop Play first");
            if (EditorApplication.isCompiling) return Note("wait for the compile to finish");
            if (!ModuleSeen) return Fail("this editor does not see the Mac build module: quit the editor and start it again once");
            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.StandaloneOSX) return Note("the editor is on macOS already");
            UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes(); AssetDatabase.SaveAssets();
            DateTime t0 = DateTime.Now;
            bool ok = EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneOSX);
            return Note((ok ? "switched to macOS" : "the switch to macOS was REFUSED") + $" after {(DateTime.Now - t0).TotalMinutes:F1} min; active target now {EditorUserBuildSettings.activeBuildTarget}");
        }

        [MenuItem("OpenZ/Mac experimental/2 Build the Mac test app (Universal: Intel + Apple chip)", false, 61)]
        public static string BuildMac() => BuildMac(MacArch.Universal);

        [MenuItem("OpenZ/Mac experimental/2b Fallback: build Apple chip only (beside an Intel set)", false, 63)]
        public static string BuildMacAppleChip() => BuildMac(MacArch.AppleChip);

        [MenuItem("OpenZ/Mac experimental/2c Fallback: build Intel only (the 1.2 build)", false, 64)]
        public static string BuildMacIntel() => BuildMac(MacArch.Intel);

        public static string BuildMac(MacArch arch)
        {
            if (SessionState.GetBool(GuardKey, false))
                return Note("the Mac build was already started in this editor session and is not started twice. Its result: "
                            + SessionState.GetString(ResultKey, "none recorded yet (still running, or interrupted: read the console and the build record)"));
            if (EditorApplication.isPlaying) return Note("stop Play first");
            if (EditorApplication.isCompiling) return Note("wait for the compile to finish");
            if (!ModuleSeen) return Fail("this editor does not see the Mac build module: quit the editor and start it again once");
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneOSX)
                return Fail("the editor is not on macOS: call '1 Switch the editor to macOS' first and wait until it has finished compiling");
            if (PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone) != ScriptingImplementation.Mono2x)
                return Fail("the standalone scripting backend is not Mono (the Mac player can only be built from Windows with Mono)");

            // ---- pre-flight, the same two checks as the Windows release build
            var scenes = Array.ConvertAll(Array.FindAll(EditorBuildSettings.scenes, e => e.enabled), e => e.path);
            var missing = RequiredScenes.Where(r => !scenes.Any(s => Path.GetFileNameWithoutExtension(s) == r)).ToArray();
            if (missing.Length > 0) return Fail("Build Settings scenes miss " + string.Join(", ", missing));
            string introSrc = Path.Combine(Application.dataPath, "OpenZ/Scripts/Intro/OzIntroData.cs");
            if (File.Exists(introSrc) && !File.ReadAllText(introSrc).Contains("PlayerPrefs.GetInt(Pref, 0) == 1"))
                return Fail("the intro default is not OFF in OzIntroData.cs (expected PlayerPrefs.GetInt(Pref, 0) == 1)");

            SessionState.SetBool(GuardKey, true);          // from here on a repeated call builds nothing
            string result;
            try { result = Run(scenes, arch); }
            catch (Exception e) { result = Fail("the Mac build method threw " + e.GetType().Name + ": " + e.Message); }
            SessionState.SetString(ResultKey, result.Length > 1500 ? result.Substring(0, 1500) : result);
            return result;
        }

        [MenuItem("OpenZ/Mac experimental/3 Switch the editor back to Windows", false, 62)]
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

        [MenuItem("OpenZ/Mac experimental/State", false, 80)]
        public static string State()
        {
            return Note($"Mac module seen: {ModuleSeen}; active target: {EditorUserBuildSettings.activeBuildTarget}; build started in this session: {SessionState.GetBool(GuardKey, false)}; "
                        + "last result: " + SessionState.GetString(ResultKey, "none"));
        }

        [MenuItem("OpenZ/Mac experimental/Allow one more Mac build in this session", false, 81)]
        public static string AllowOneMore()
        {
            SessionState.EraseBool(GuardKey); SessionState.EraseString(ResultKey);
            return Note("the guard is cleared: '2 Build the Mac test app' will build once more");
        }

        static string Run(string[] scenes, MacArch want)
        {
            UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes(); AssetDatabase.SaveAssets();
            DateTime now = DateTime.Now;
            string tag = OzVersion.Tag(now);
            string relRoot = Path.Combine(JobRoot, "release");
            string dir = Path.Combine(relRoot, "OpenZ_" + tag + Suffix(want));
            string record = dir + "_BUILD_INFO.txt";
            Directory.CreateDirectory(relRoot);
            string kept = null;
            if (Directory.Exists(dir))
            {
                kept = dir + "_prev_" + now.ToString("yyyyMMdd_HHmmss");
                try { Directory.Move(dir, kept); }
                catch (Exception e) { return Fail("could not keep the older same-day Mac folder " + dir + ": " + e.Message); }
            }
            Directory.CreateDirectory(dir);

            // ---- the player settings the Windows release build sets (the same values; they stay as that build leaves them)
            PlayerPrefs.SetInt("oz_testmode", 0); PlayerPrefs.SetInt("oz_gate", 0); PlayerPrefs.Save();
            Debug.Log("[OpenZ Mac] " + OpenZBuild.IncludeRuntimeShaders());
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow; PlayerSettings.defaultIsNativeResolution = true; PlayerSettings.allowFullscreenSwitch = true;
            PlayerSettings.defaultScreenWidth = 1920; PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.resizableWindow = true; PlayerSettings.runInBackground = true;

            // ---- release settings, for this build only (restored in finally)
            string oldCompany = PlayerSettings.companyName, oldVersion = PlayerSettings.bundleVersion;
            string oldId = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Standalone);
            string archNote = AskForArch(ArchName(want));

            var opts = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = Path.Combine(dir, "OpenZ.app"),
                target = BuildTarget.StandaloneOSX,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None,
                extraScriptingDefines = new[] { OpenZReleaseBuild.Define },
            };

            BuildReport rep;
            try
            {
                PlayerSettings.companyName = OpenZReleaseBuild.ReleaseCompany;
                PlayerSettings.bundleVersion = tag;
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, MacBundleId);
                OpenZReleaseBuild.Building = true;
                rep = BuildPipeline.BuildPlayer(opts);
            }
            catch (Exception e) { return Fail("BuildPlayer threw " + e.GetType().Name + ": " + e.Message); }
            finally
            {
                OpenZReleaseBuild.Building = false;
                PlayerSettings.companyName = oldCompany;
                PlayerSettings.bundleVersion = oldVersion;
                if (!string.IsNullOrEmpty(oldId)) PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, oldId);
                AssetDatabase.SaveAssets();
            }
            if (rep == null) return Fail("BuildPlayer returned no report");

            var s = rep.summary;
            string exe = Path.Combine(dir, "OpenZ.app", "Contents", "MacOS", "OpenZ");
            string arch = File.Exists(exe) ? MachoSlices(exe) : "no executable (" + exe + ")";
            bool built = s.result == BuildResult.Succeeded && (s.options & BuildOptions.Development) == 0;
            // 1.3 item 13: every program file of the app, its processor types and a signature on every slice
            var natives = new List<string>(); int bad = 0, files = 0;
            string app = Path.Combine(dir, "OpenZ.app");
            if (Directory.Exists(app))
                foreach (var f in Directory.GetFiles(app, "*", SearchOption.AllDirectories).OrderBy(x => x, StringComparer.Ordinal))
                {
                    string slices = MachoSlices(f, out bool signedAll);
                    if (slices == null) continue;
                    files++;
                    bool fine = slices == ArchWant(want) && signedAll;
                    if (!fine) bad++;
                    natives.Add($"   {(fine ? "ok  " : "BAD ")} {f.Substring(dir.Length + 1).Replace(Path.DirectorySeparatorChar, '/')}: {slices}, {(signedAll ? "signed (every slice)" : "a slice WITHOUT a signature")}");
                }
            bool usable = built && arch == ArchWant(want) && files > 0 && bad == 0;

            var info = new StringBuilder();
            info.AppendLine($"OpenZ EXPERIMENTAL Mac build {OzVersion.Display(now)}  (tag {tag})");
            info.AppendLine($"result: {s.result}");
            info.AppendLine("usable: " + (usable ? "yes" : "NO"));
            info.AppendLine($"time: {now:yyyy-MM-dd HH:mm}  duration: {s.totalTime}");
            info.AppendLine($"errors: {s.totalErrors}, warnings: {s.totalWarnings}");
            info.AppendLine($"options: {s.options} (non-development), define {OpenZReleaseBuild.Define}, company {OpenZReleaseBuild.ReleaseCompany}, bundle id {MacBundleId}, bundleVersion {tag} (restored to '{oldCompany}' / '{oldId}' / '{oldVersion}')");
            info.AppendLine("scenes: " + string.Join(", ", scenes.Select(Path.GetFileNameWithoutExtension)));
            info.AppendLine($"target: {s.platform}, asked for {ArchWords(want)} ({archNote})");
            info.AppendLine("executable header: " + arch);
            info.AppendLine($"program files: {files}, every one {ArchWant(want)} and signed: {(files > 0 && bad == 0 ? "yes" : "NO (" + bad + " not)")}");
            foreach (var n in natives) info.AppendLine(n);
            info.AppendLine("output: " + dir);
            if (kept != null) info.AppendLine("older same-day Mac folder kept: " + kept);
            info.AppendLine(BuildMessages(rep));
            info.AppendLine("next: the post-build script for the Mac folder, outside the editor");
            try { File.WriteAllText(record, info.ToString()); }
            catch (Exception e) { Debug.LogError("[OpenZ Mac] could not write the build record " + record + ": " + e.Message); }

            if (!built) return Fail($"build {s.result} ({s.totalErrors} errors), see the console; record {record}");
            if (!usable) return Fail($"not every program file is {ArchWords(want)} with a signature on every slice (the executable: {arch}; {bad} of {files} files not); record " + record);
            Debug.Log("[OpenZ Mac] " + info);
            return info.ToString();
        }

        /// <summary>Asks the Mac build module for the player of these processors (its own names: x64, ARM64, x64ARM64). The module is
        /// not referenced at compile time: its setting is looked up by name, and the plain platform setting of the same meaning is
        /// written as well. Whether it took effect is proven after the build by reading every program file's header. (1.2: Intel only,
        /// AskForIntel64.)</summary>
        static string AskForArch(string want)
        {
            var notes = new List<string>();
            try
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type t = asm.GetType("UnityEditor.OSXStandalone.UserBuildSettings", false);
                    if (t == null) continue;
                    PropertyInfo p = t.GetProperty("architecture", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                    if (p == null || !p.CanWrite || !p.PropertyType.IsEnum) continue;
                    object before = p.GetValue(null, null);
                    p.SetValue(null, Enum.Parse(p.PropertyType, want, true), null);
                    notes.Add("module setting " + before + " -> " + p.GetValue(null, null));
                    break;
                }
            }
            catch (Exception e) { notes.Add("module setting not set: " + e.GetType().Name); }
            try
            {
                string before = EditorUserBuildSettings.GetPlatformSettings("OSXUniversal", "Architecture");
                if (!string.Equals(before, want, StringComparison.OrdinalIgnoreCase))
                {
                    EditorUserBuildSettings.SetPlatformSettings("OSXUniversal", "Architecture", want);
                    notes.Add("platform setting '" + before + "' -> '" + want + "'");
                }
                else notes.Add("platform setting " + want);
            }
            catch (Exception e) { notes.Add("platform setting not set: " + e.GetType().Name); }
            return string.Join("; ", notes);
        }

        /// <summary>The processor type in the header of a Mach-O file: "x86_64", "arm64", or a text that says what else it is.</summary>
        public static string MachoProcessor(string path)
        {
            try
            {
                var h = new byte[8];
                using (var fs = File.OpenRead(path)) { if (fs.Read(h, 0, 8) < 8) return "too short to be a program"; }
                uint magic = BitConverter.ToUInt32(h, 0), cpu = BitConverter.ToUInt32(h, 4);
                if (magic == 0xFEEDFACF) return cpu == 0x01000007 ? "x86_64" : cpu == 0x0100000C ? "arm64" : "processor type 0x" + cpu.ToString("X8");
                if (magic == 0xBEBAFECA || magic == 0xBFBAFECA) return "universal (more than one processor type)";
                return "not a 64-bit Mach-O file (first bytes 0x" + magic.ToString("X8") + ")";
            }
            catch (Exception e) { return "unreadable: " + e.Message; }
        }

        /// <summary>1.3 item 13: the processor types of a Mach-O file, sorted and joined with "+" ("x86_64", "arm64", "arm64+x86_64"),
        /// or null when the file is no 64-bit Mach-O program; signedAll = every slice has a code signature (LC_CODE_SIGNATURE pointing
        /// at a signature superblob). The one-argument form returns a text for the record.</summary>
        public static string MachoSlices(string path) => MachoSlices(path, out _) ?? "not a 64-bit Mach-O program";
        public static string MachoSlices(string path, out bool signedAll)
        {
            signedAll = false;
            try
            {
                byte[] b;
                using (var fs = File.OpenRead(path))
                {
                    if (fs.Length < 32) return null;
                    var h = new byte[4]; if (fs.Read(h, 0, 4) < 4) return null;
                    uint m = BitConverter.ToUInt32(h, 0);
                    if (m != 0xFEEDFACF && m != 0xBEBAFECA && m != 0xBFBAFECA) return null;                   // thin 64-bit (little endian) or fat (big endian on disk)
                    fs.Position = 0; b = new byte[fs.Length]; int got = 0;
                    while (got < b.Length) { int r = fs.Read(b, got, b.Length - got); if (r <= 0) break; got += r; }
                }
                uint magic = BitConverter.ToUInt32(b, 0);
                var cpus = new List<string>(); bool all = true;
                if (magic == 0xFEEDFACF) { cpus.Add(Cpu(BitConverter.ToUInt32(b, 4))); all = HasSignature(b, 0); }
                else
                {
                    bool fat64 = magic == 0xBFBAFECA; uint n = BE32(b, 4);
                    if (n == 0 || n > 8) return null;                                                          // a Java class file starts the same: never 1..8 slices of 64-bit Mach-O
                    for (int i = 0; i < n; i++)
                    {
                        int at = 8 + i * (fat64 ? 32 : 20);
                        uint cpu = BE32(b, at);
                        long off = fat64 ? (long)BE64(b, at + 8) : BE32(b, at + 8);
                        if (off < 0 || off + 32 > b.Length || BitConverter.ToUInt32(b, (int)off) != 0xFEEDFACF) return null;
                        cpus.Add(Cpu(cpu)); all &= HasSignature(b, (int)off);
                    }
                }
                cpus.Sort(StringComparer.Ordinal);
                signedAll = all;
                return string.Join("+", cpus);
            }
            catch (Exception) { return null; }
        }
        static string Cpu(uint c) => c == 0x01000007 ? "x86_64" : c == 0x0100000C ? "arm64" : "cpu 0x" + c.ToString("X8");
        static uint BE32(byte[] b, int at) => (uint)(b[at] << 24 | b[at + 1] << 16 | b[at + 2] << 8 | b[at + 3]);
        static ulong BE64(byte[] b, int at) => (ulong)BE32(b, at) << 32 | BE32(b, at + 4);
        /// <summary>A thin 64-bit Mach-O image at `at` has an LC_CODE_SIGNATURE load command pointing at a signature superblob.</summary>
        static bool HasSignature(byte[] b, int at)
        {
            uint ncmds = BitConverter.ToUInt32(b, at + 16); int off = at + 32;
            for (int i = 0; i < ncmds && off + 16 <= b.Length; i++)
            {
                uint cmd = BitConverter.ToUInt32(b, off), size = BitConverter.ToUInt32(b, off + 4);
                if (cmd == 0x1D)
                {
                    long so = at + (long)BitConverter.ToUInt32(b, off + 8);
                    return so + 12 <= b.Length && BE32(b, (int)so) == 0xFADE0CC0;
                }
                if (size < 8) return false;
                off += (int)size;
            }
            return false;
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

        static string Note(string what) { Debug.Log("[OpenZ Mac] " + what); return what; }
        static string Fail(string why) { Debug.LogError("[OpenZ Mac] NOT BUILT: " + why); return "FAILED: " + why; }
    }
}
