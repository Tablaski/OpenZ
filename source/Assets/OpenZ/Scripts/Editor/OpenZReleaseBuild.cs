using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>
    ///    project itself stays Mono2x, put back after every build), the Build Settings scenes (Intro, MainMenu, L02_Desert,
    ///    L17_CarPark all required; the intro ships but stays OFF by default, checked in OzIntroData.cs);
    ///  * scripting define OZ_RELEASE for this build only (extraScriptingDefines): every -ozdev / take / capture / cheat path is
    ///  * version from ONE source: OzVersion.Number + the build day (OzVersion.Tag) -> PlayerSettings.bundleVersion (build only),
    ///    output release\OpenZ_&lt;tag&gt;\OpenZ.exe (an older same-day folder is kept as OpenZ_&lt;tag&gt;_prev_&lt;yyyyMMdd_HHmm&gt;,
    ///    nothing deleted, the builds\ convention);
    ///  * company name "OpenZ" set again for this build, in case the project's own ever differs (exe properties, app.info, registry
    ///    and AppData folders carry this name only); PDB copy off; restored afterwards;
    ///  * post-build: the Burst debug info (*_DoNotShip), IL2CPP's *_BackUpThisFolder_ButDontShipItWithYourGame and any *.pdb move to
    ///    writes into GameAssembly.dll are overwritten with neutral text of the same length BEFORE the checksums and the leak check;
    ///    README.txt (player readme, no personal info) + LICENSES.txt at the top, then SHA256SUMS.txt over every shipped file;
    ///    the dev record goes NEXT to the folder (release\OpenZ_&lt;tag&gt;_BUILD_INFO.txt), not into it.
    /// </summary>
    public static class OpenZReleaseBuild
    {
        public const string Define = "OZ_RELEASE";
        public const string ReleaseCompany = "OpenZ";
        static readonly string[] RequiredScenes = { "Intro", "MainMenu", "L02_Desert", "L07_Volcanic", "L16_Jungle", "L17_CarPark", "L18_BridgeGame" };
        internal static bool Building;
        /// <summary>A letter after the day tag for a second private test build on one day (release\OpenZ_&lt;tag&gt;&lt;suffix&gt;); empty by
        /// default. Only the folder names change: bundleVersion stays the day tag, so the in-game version text still reads the build day.</summary>
        public static string FolderSuffix = "";
        /// <summary>
        /// 1.3 settings), the test exe and the final one alike: ONE Windows download, no Mono Windows exe. A constant, so no bridge can
        /// switch it off. The project keeps Mono2x on disk (the editor's own runs; the Mac and Linux builders require it): BuildFinal sets
        /// IL2CPP for the build only and puts Mono2x back in its finally, also when the build fails or throws. (1.3 item 25 step b.)</summary>
        public const bool Il2CppWindows = true;
        static string Il2CppVariation => Path.Combine(EditorApplication.applicationContentsPath, "PlaybackEngines", "windowsstandalonesupport", "Variations", "win64_player_nondevelopment_il2cpp");

        static string JobRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "../../../.."));

        [MenuItem("OpenZ/Release/Build final exe", false, 30)]
        public static string BuildFinal()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[OpenZ Release] stop Play first"); return "playing"; }
            if (EditorApplication.isCompiling) { Debug.LogWarning("[OpenZ Release] wait for the compile to finish"); return "compiling"; }
            UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes(); AssetDatabase.SaveAssets();

            // ---- pre-flight
            var scenes = Array.ConvertAll(Array.FindAll(EditorBuildSettings.scenes, e => e.enabled), e => e.path);
            var missing = RequiredScenes.Where(r => !scenes.Any(s => Path.GetFileNameWithoutExtension(s) == r)).ToArray();
            if (missing.Length > 0) return Fail("Build Settings scenes miss " + string.Join(", ", missing) + " (Intro, MainMenu, L02_Desert, L07_Volcanic, L16_Jungle, L17_CarPark, L18_BridgeGame must be enabled)");
            string introSrc = Path.Combine(Application.dataPath, "OpenZ/Scripts/Intro/OzIntroData.cs");
            if (File.Exists(introSrc) && !File.ReadAllText(introSrc).Contains("PlayerPrefs.GetInt(Pref, 0) == 1"))
                return Fail("the intro default is not OFF in OzIntroData.cs (expected PlayerPrefs.GetInt(Pref, 0) == 1)");
            if (!Directory.Exists(Il2CppVariation))   // 1.4: IL2CPP only
                return Fail("this editor has no Windows IL2CPP player (" + Il2CppVariation + "): add 'Windows Build Support (IL2CPP)' in the Hub, and the Visual Studio C++ build tools");

            DateTime now = DateTime.Now;
            string tag = OzVersion.Tag(now);
            string relRoot = Path.Combine(JobRoot, "release");
            string dir = Path.Combine(relRoot, "OpenZ_" + tag + FolderSuffix);
            Directory.CreateDirectory(relRoot);
            string kept = null;
            if (Directory.Exists(dir))
            {
                kept = dir + "_prev_" + now.ToString("yyyyMMdd_HHmm");
                if (Directory.Exists(kept)) kept = dir + "_prev_" + now.ToString("yyyyMMdd_HHmmss");
                try { Directory.Move(dir, kept); }
                catch (Exception e) { return Fail("could not keep the older same-day release " + dir + " (open in Explorer / the exe running?): " + e.Message); }
            }
            Directory.CreateDirectory(dir);

            // ---- release settings (build only, restored in finally)
            string oldCompany = PlayerSettings.companyName, oldVersion = PlayerSettings.bundleVersion;
            string oldPdb = EditorUserBuildSettings.GetPlatformSettings("Standalone", "CopyPDBFiles");
            var oldBackend = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone);                 // 1.4: recorded; Mono2x is what goes back after the build
            var oldIlConfig = PlayerSettings.GetIl2CppCompilerConfiguration(NamedBuildTarget.Standalone);
            var oldStrip = PlayerSettings.GetManagedStrippingLevel(NamedBuildTarget.Standalone);
            PlayerPrefs.SetInt("oz_testmode", 0); PlayerPrefs.SetInt("oz_gate", 0); PlayerPrefs.Save();
            Debug.Log("[OpenZ Release] " + OpenZBuild.IncludeRuntimeShaders());
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow; PlayerSettings.defaultIsNativeResolution = true; PlayerSettings.allowFullscreenSwitch = true;
            PlayerSettings.defaultScreenWidth = 1920; PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.resizableWindow = true; PlayerSettings.runInBackground = true;

            var opts = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = Path.Combine(dir, "OpenZ.exe"),
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None,
                extraScriptingDefines = new[] { Define },
            };
            if ((opts.options & (BuildOptions.Development | BuildOptions.AllowDebugging | BuildOptions.ConnectWithProfiler | BuildOptions.EnableDeepProfilingSupport)) != 0)
                return Fail("development options set");

            BuildReport rep;
            try
            {
                PlayerSettings.companyName = ReleaseCompany;
                PlayerSettings.bundleVersion = tag;
                EditorUserBuildSettings.SetPlatformSettings("Standalone", "CopyPDBFiles", "false");
                // code is stripped: link.xml keeps it whole too). Set inside the try: the finally below puts Mono2x back whatever happens.
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.IL2CPP);
                PlayerSettings.SetIl2CppCompilerConfiguration(NamedBuildTarget.Standalone, Il2CppCompilerConfiguration.Release);
                PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Standalone, ManagedStrippingLevel.Minimal);
                Building = true;
                rep = BuildPipeline.BuildPlayer(opts);
            }
            catch (Exception e) { return Fail("BuildPlayer threw " + e.GetType().Name + ": " + e.Message); }
            finally
            {
                Building = false;
                PlayerSettings.companyName = oldCompany;
                PlayerSettings.bundleVersion = oldVersion;
                EditorUserBuildSettings.SetPlatformSettings("Standalone", "CopyPDBFiles", string.IsNullOrEmpty(oldPdb) ? "false" : oldPdb);
                // 1.4: Mono2x again on disk (the editor's own runs; the Mac / Linux builders refuse anything else), also after a failed or
                // thrown build. Mono2x, not oldBackend: a project that an interrupted earlier build left on IL2CPP is put right here too.
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
                PlayerSettings.SetIl2CppCompilerConfiguration(NamedBuildTarget.Standalone, oldIlConfig);
                PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Standalone, oldStrip);
                AssetDatabase.SaveAssets();
            }

            if (rep == null) return Fail("BuildPlayer returned no report");
            var s = rep.summary;
            var info = new StringBuilder();
            info.AppendLine($"OpenZ release build {OzVersion.Display(now)}  (tag {tag}{(string.IsNullOrEmpty(FolderSuffix) ? "" : ", folder OpenZ_" + tag + FolderSuffix)})");
            info.AppendLine($"result: {s.result}");
            info.AppendLine($"time: {now:yyyy-MM-dd HH:mm}  duration: {s.totalTime}");
            info.AppendLine($"errors: {s.totalErrors}, warnings: {s.totalWarnings}");
            info.AppendLine($"options: {s.options} (non-development), define {Define}, company {ReleaseCompany}, bundleVersion {tag} (both restored to '{oldCompany}' / '{oldVersion}')");
            var nowBackend = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone);                 // 1.4: IL2CPP only
            info.AppendLine("scripting: IL2CPP (Release, Minimal stripping); the project setting after the build: " + nowBackend
                            + (oldBackend == ScriptingImplementation.Mono2x ? "" : " (it was " + oldBackend + " before this build: put back to Mono2x)"));
            info.AppendLine("scenes: " + string.Join(", ", scenes.Select(Path.GetFileNameWithoutExtension)));
            info.AppendLine("output: " + dir);
            if (kept != null) info.AppendLine("older same-day release kept: " + kept);
            if (nowBackend != ScriptingImplementation.Mono2x)   // 1.4: the editor and the Mac / Linux builders need Mono2x on disk
            {
                File.WriteAllText(dir + "_BUILD_INFO.txt", info.ToString());
                return Fail("the project's scripting backend is " + nowBackend + " after the build, not Mono2x: set it back in Player Settings before anything else; record " + dir + "_BUILD_INFO.txt");
            }
            if (s.result != BuildResult.Succeeded || (s.options & BuildOptions.Development) != 0)
            {
                File.WriteAllText(dir + "_BUILD_INFO.txt", info.ToString());
                return Fail($"build {s.result} ({s.totalErrors} errors), see the console; record {dir}_BUILD_INFO.txt");
            }

            // ---- post-build: nothing debug ships
            string sym = Path.Combine(relRoot, "_symbols", "OpenZ_" + tag + FolderSuffix + (kept != null ? "_" + now.ToString("HHmm") : ""));
            if (Directory.Exists(sym)) sym += "_" + now.ToString("HHmmss");
            try
            {
            int moved = 0;
            foreach (var d in Directory.GetDirectories(dir))
            {
                string n = Path.GetFileName(d);
                if (n.EndsWith("_DoNotShip", StringComparison.OrdinalIgnoreCase) || n.IndexOf("ButDontShipItWithYourGame", StringComparison.OrdinalIgnoreCase) >= 0)
                { Directory.CreateDirectory(sym); Directory.Move(d, Path.Combine(sym, n)); moved++; }
            }
            foreach (var f in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
            {
                string ext = Path.GetExtension(f).ToLowerInvariant();
                if (ext == ".pdb" || ext == ".mdb" || ext == ".log" || ext == ".dmp")
                {
                    string to = Path.Combine(sym, f.Substring(dir.Length + 1));
                    Directory.CreateDirectory(Path.GetDirectoryName(to)); File.Move(f, to); moved++;
                }
            }
            info.AppendLine($"debug files moved out of the release: {moved} -> {sym}");

            info.AppendLine(ScrubPdbPaths(dir));
            string form = Il2CppForm(dir);                                       // 1.4: IL2CPP only, nothing of a Mono player in the folder
            info.AppendLine("IL2CPP form: " + form);
            if (form != "ok") throw new InvalidOperationException("the folder is not a clean IL2CPP build: " + form);
            info.AppendLine(ScrubIl2CppPaths(dir));                              // before SHA256SUMS.txt and the quick leak check
            File.WriteAllText(Path.Combine(dir, "README.txt"), PlayerReadme(now).Replace("\n", "\r\n"), new UTF8Encoding(false));
            string lic = Path.Combine(dir, "OpenZ_Data", "StreamingAssets", "LICENSES.txt");
            if (File.Exists(lic)) File.Copy(lic, Path.Combine(dir, "LICENSES.txt"), true);
            else info.AppendLine("WARNING: OpenZ_Data/StreamingAssets/LICENSES.txt missing (the licence texts must ship)");
            string licAssets = Path.Combine(dir, "OpenZ_Data", "StreamingAssets", "LICENSE_ASSETS.txt");
            if (File.Exists(licAssets)) File.Copy(licAssets, Path.Combine(dir, "LICENSE_ASSETS.txt"), true);
            else info.AppendLine("WARNING: OpenZ_Data/StreamingAssets/LICENSE_ASSETS.txt missing (the assets licence text must ship)");

            int n2 = WriteSha256(dir, out long bytes);
            info.AppendLine($"SHA256SUMS.txt: {n2} files, {bytes / (1024.0 * 1024.0):F0} MB");
            info.AppendLine(QuickLeakCheck(dir, oldCompany));
            }
            catch (Exception e)
            {
                info.AppendLine("POST-BUILD FAILED (" + e.GetType().Name + ": " + e.Message + "): do NOT ship this folder, fix and build again");
                File.WriteAllText(dir + "_BUILD_INFO.txt", info.ToString());
                return Fail("post-build step threw " + e.GetType().Name + ": " + e.Message + "; record " + dir + "_BUILD_INFO.txt");
            }
            info.AppendLine("next: the release chain's post-build (scrub_authtoken.py, scrub_il2cpp_paths as the check, scan_build_privacy.py with the engine's own hits accepted by name, check_defaults.py --build), then the 2-minute smoke test");
            File.WriteAllText(dir + "_BUILD_INFO.txt", info.ToString());
            Debug.Log("[OpenZ Release] " + info);
            return info.ToString();
        }

        /// <summary>Our assemblies carry the absolute path of their PDB in the PE debug directory (CodeView "RSDS" record:
        /// with the bare file name (zero padded, same length), in every Managed / Plugins dll whose record holds a drive path.
        /// Unity's own signed binaries (the exe stub, UnityPlayer.dll, MonoBleedingEdge) are not touched.</summary>
        public static string ScrubPdbPaths(string dir)
        {
            int files = 0, recs = 0;
            var roots = new[] { Path.Combine(dir, "OpenZ_Data", "Managed"), Path.Combine(dir, "OpenZ_Data", "Plugins"), dir };
            foreach (var root in roots)
            {
                if (!Directory.Exists(root)) continue;
                var dlls = root == dir ? Directory.GetFiles(dir, "GameAssembly.dll", SearchOption.TopDirectoryOnly)   // 1.3 item 25 step b: an IL2CPP build's game code (ours; Unity's own signed binaries beside it stay untouched)
                                       : Directory.GetFiles(root, "*.dll", SearchOption.AllDirectories);
                foreach (var f in dlls)
                {
                    byte[] d = File.ReadAllBytes(f); int changed = 0;
                    for (int i = 0; i + 24 < d.Length; i++)
                    {
                        if (d[i] != (byte)'R' || d[i + 1] != (byte)'S' || d[i + 2] != (byte)'D' || d[i + 3] != (byte)'S') continue;
                        int p0 = i + 24, p1 = p0;
                        while (p1 < d.Length && d[p1] != 0 && p1 - p0 < 1024) p1++;
                        if (p1 >= d.Length || p1 - p0 < 4) continue;
                        string path = Encoding.UTF8.GetString(d, p0, p1 - p0);
                        if (path.Length < 3 || path[1] != ':' || !path.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase)) continue;
                        byte[] name = Encoding.UTF8.GetBytes(Path.GetFileName(path.Replace('/', '\\')));
                        for (int k = p0; k < p1; k++) d[k] = k - p0 < name.Length ? name[k - p0] : (byte)0;
                        changed++; i = p1;
                    }
                    if (changed > 0) { File.WriteAllBytes(f, d); files++; recs += changed; }
                }
            }
            return $"PDB paths scrubbed: {recs} record(s) in {files} dll(s)";
        }

        /// <summary>1.4: the folder must be an IL2CPP player: GameAssembly.dll and the metadata there, nothing of a Mono player.</summary>
        static string Il2CppForm(string dir)
        {
            var bad = new List<string>();
            if (!File.Exists(Path.Combine(dir, "GameAssembly.dll"))) bad.Add("no GameAssembly.dll");
            if (!File.Exists(Path.Combine(dir, "OpenZ_Data", "il2cpp_data", "Metadata", "global-metadata.dat"))) bad.Add("no OpenZ_Data/il2cpp_data/Metadata/global-metadata.dat");
            if (Directory.Exists(Path.Combine(dir, "OpenZ_Data", "Managed"))) bad.Add("OpenZ_Data/Managed is there");
            if (Directory.Exists(Path.Combine(dir, "MonoBleedingEdge"))) bad.Add("MonoBleedingEdge is there");
            return bad.Count == 0 ? "ok" : string.Join("; ", bad);
        }

        /// <summary>
        /// (the error texts of the engine's libil2cpp, ...\Editor\Data\il2cpp\libil2cpp\...) and the project folder (the debug file
        /// record, if ScrubPdbPaths left one). Each prefix, worked out at run time (this file names no folder), is overwritten in place by
        /// neutral text of the SAME length: underscores, then its last folder names (the file keeps its size and every offset; the rest of
        /// each text stays), in UTF-8 and UTF-16. The release chain's scrub_il2cpp_paths_1_4.py writes the same bytes and runs again after
        /// the build as the check. GameAssembly.dll and il2cpp_data/Metadata/global-metadata.dat only; Unity's own binaries stay.</summary>
        public static string ScrubIl2CppPaths(string dir)
        {
            string editorData = Path.GetFullPath(EditorApplication.applicationContentsPath).TrimEnd('\\', '/');
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).TrimEnd('\\', '/');
            var prefixes = new[]
            {
                new[] { editorData + "\\", "\\" + Path.GetFileName(Path.GetDirectoryName(editorData)) + "\\" + Path.GetFileName(editorData) + "\\" },
                new[] { project + "\\", "\\" + Path.GetFileName(project) + "\\" },
            };
            var parts = new List<string>();
            foreach (var rel in new[] { "GameAssembly.dll", "OpenZ_Data/il2cpp_data/Metadata/global-metadata.dat" })
            {
                string p = Path.Combine(dir, rel);
                if (!File.Exists(p)) { parts.Add(rel + " not there"); continue; }
                byte[] d = File.ReadAllBytes(p); int n = 0;
                foreach (var pr in prefixes) n += SameLengthScrub(d, pr[0], pr[1]);
                if (n > 0) File.WriteAllBytes(p, d);
                parts.Add(rel + " " + n);
            }
            return "IL2CPP paths scrubbed (same length, before the checksums and the leak check): " + string.Join(", ", parts) + " prefix(es)";
        }

        // >>> same-length scrub (pure: no Unity API; the release kit's proof compiles this block alone)
        /// <summary>Overwrites every copy of prefix in d (ASCII letters in either case; UTF-8 and UTF-16 LE) with underscores and then
        /// tail, the same number of bytes. Returns how many copies it overwrote.</summary>
        public static int SameLengthScrub(byte[] d, string prefix, string tail)
        {
            int n = 0;
            string neutral = new string('_', Math.Max(0, prefix.Length - tail.Length)) + tail;
            foreach (var enc in new Encoding[] { new UTF8Encoding(false), new UnicodeEncoding(false, false) })
            {
                byte[] pat = enc.GetBytes(prefix), rep = enc.GetBytes(neutral);
                if (pat.Length == 0 || rep.Length != pat.Length) continue;          // a path with other letters: never another length
                byte first = LowerAscii(pat[0]);
                for (int i = 0; i + pat.Length <= d.Length; i++)
                {
                    if (LowerAscii(d[i]) != first) continue;
                    int k = 1;
                    while (k < pat.Length && LowerAscii(d[i + k]) == LowerAscii(pat[k])) k++;
                    if (k < pat.Length) continue;
                    Buffer.BlockCopy(rep, 0, d, i, rep.Length); n++; i += pat.Length - 1;
                }
            }
            return n;
        }

        static byte LowerAscii(byte c) { return c >= (byte)'A' && c <= (byte)'Z' ? (byte)(c + 32) : c; }
        // <<< same-length scrub

        static string Fail(string why) { Debug.LogError("[OpenZ Release] NOT BUILT: " + why); return "FAILED: " + why; }

        /// <summary>SHA256SUMS.txt in the `sha256sum -c` format ("&lt;hex&gt; *&lt;path&gt;", forward slashes), every file but itself.</summary>
        public static int WriteSha256(string dir, out long bytes)
        {
            bytes = 0;
            var files = Directory.GetFiles(dir, "*", SearchOption.AllDirectories)
                .Select(f => f.Substring(dir.Length + 1).Replace('\\', '/'))
                .Where(r => !r.Equals("SHA256SUMS.txt", StringComparison.OrdinalIgnoreCase))
                .OrderBy(r => r, StringComparer.Ordinal).ToList();
            var sb = new StringBuilder();
            try
            {
                using (var sha = SHA256.Create())
                    for (int i = 0; i < files.Count; i++)
                    {
                        string p = Path.Combine(dir, files[i]);
                        if (i % 8 == 0) EditorUtility.DisplayProgressBar("OpenZ release", "SHA256 " + files[i], i / (float)files.Count);
                        using (var fs = File.OpenRead(p)) { bytes += fs.Length; sb.Append(BitConverter.ToString(sha.ComputeHash(fs)).Replace("-", "").ToLowerInvariant()).Append(" *").Append(files[i]).Append('\n'); }
                    }
            }
            finally { EditorUtility.ClearProgressBar(); }
            File.WriteAllText(Path.Combine(dir, "SHA256SUMS.txt"), sb.ToString(), new UTF8Encoding(false));
            return files.Count;
        }

        /// <summary>A fast in-editor look at the game dll + app.info for the job folder, user and company names.</summary>
        static string QuickLeakCheck(string dir, string projectCompany)
        {
            // project's own company name), so this source file carries no dev folder or channel name itself
            var terms = new List<string> { Path.GetFileName(Path.GetDirectoryName(JobRoot)), Environment.UserName, projectCompany == ReleaseCompany ? null : projectCompany };   // the game's own name is no leak
            var hits = new List<string>();
            foreach (var rel in new[] { "OpenZ_Data/Managed/Assembly-CSharp.dll", "OpenZ_Data/app.info", "OpenZ.exe",
                                        "GameAssembly.dll", "OpenZ_Data/il2cpp_data/Metadata/global-metadata.dat" })   // (the last two: an IL2CPP build only, 1.3 item 25 step b)
            {
                string p = Path.Combine(dir, rel);
                if (!File.Exists(p)) continue;
                byte[] data = File.ReadAllBytes(p);
                string a = Encoding.ASCII.GetString(data), u = Encoding.Unicode.GetString(data);
                foreach (var t in terms)
                    if (!string.IsNullOrEmpty(t) && t.Length >= 3 && (a.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0 || u.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0))
                        hits.Add(rel + ": " + (t == Environment.UserName ? "<windows user name>" : t == projectCompany ? "<project company name>" : "<job-root folder name>"));
            }
            if (hits.Count > 0) Debug.LogError("[OpenZ Release] quick leak check FAILED: " + string.Join("; ", hits));
            return hits.Count == 0 ? "quick leak check (dll / app.info / exe): clean" : "quick leak check FAILED: " + string.Join("; ", hits);
        }

        static string PlayerReadme(DateTime d)
        {
            return
$@"{OzVersion.Display(d)}
==============================
A faithful fan tribute remake of Z by The Bitmap Brothers (1996), made for its 30th anniversary.
Free and non-commercial. Five maps: Z level 2 (desert), Z level 7 (volcano), Z level 16 (jungle), Z level 17 ""Car Park"" (city) and Z level 18 ""Bridge Game"" (city).

START
- Run OpenZ.exe. The game opens full screen at your screen's resolution (Alt+Enter or OPTIONS > FULLSCREEN for a window).
- A desktop shortcut: right-click OpenZ.exe > Send to > Desktop (create shortcut). Keep OpenZ.exe in its folder: a copy of OpenZ.exe on its own does not start (Windows says UnityPlayer.dll was not found).
- Pick a map in the main menu, choose the difficulty and deploy.
- Capture the territory flags, build robots and vehicles in your factories and fort, and destroy the enemy fort.
- CONTROLS in the main menu lists every key and lets you change them. OPTIONS holds music, screen and game settings.

SYSTEM
- Windows 10 / 11, 64-bit, a DirectX 12 graphics card. About 5 GB on disk.
- Settings are stored per Windows user. A log for bug reports is written to
  %USERPROFILE%\AppData\LocalLow\OpenZ\OpenZ\Player.log
- If the game crashes or is closed by force, the log of that session is kept at the next start (the last 10) in
  %USERPROFILE%\AppData\LocalLow\OpenZ\OpenZ\CrashLogs
  Please attach the newest file from there to a bug report. The game itself sends nothing anywhere.

NEW IN 1.5
New:
- A new city level, BRIDGE GAME (level 18), the last card after level 17: the three long bridges in the middle lie blown up from the start, robots wade across, vehicles go round over the one bridge still standing, a crane can rebuild a bridge; a ruined harbour, half-sunk tower stumps and rusty containers in the water, the city's night storm, a briefing for each side, a line of each general on the level card, and an empty BIZON and MAMMOTH to take.
- UNIT EDITOR: two big tanks, the MAMMOTH and the BIZON, each driven by a Psycho, with their own voice lines, real moving tracks with worn, dusty links, lamps and glowing team strips at night, and dozens of flying pieces when destroyed. No factory builds them: place them in the Unit editor, or take the empty ones that wait on level 18.
- UNIT EDITOR: the MAMMOTH fires from two heavy barrels in turn and rams an enemy vehicle with a crew that comes close in front of it, rolling it onto its back. Enemy robots caught behind the rolling hull are crushed.
- UNIT EDITOR: the BIZON, the toughest and slowest tank of all, never backs away: it climbs onto an enemy vehicle or ground gun right in front of it, rides over it and crushes it flat. It is the only vehicle that can cross water, and even lava, which slowly burns it.
- Soft shadows of passing clouds drift across the battlefield by day and fade out at night and in the rain.
- Forts show their damage stage by stage: soot, cracks, breaches, broken parapets, smoke and fire, until a heavy ruin stands just before the fort falls.
- The big cannon on a fort's roof goes up in a towering geyser of fire with a column of flame and a shake of the screen when it is shot off a standing fort, and after a won battle the beaten fort's cannon is blown off before the final talk, leaving a burning ruin.
- Crocodile attacks: no more siren. A robot of yours nearby calls out a warning instead, in one of six lines.
- UNIT EDITOR: a DAY / NIGHT switch under BACKGROUND, track prints and dust on the Desert dunes and Snow field floors, and an eye button in the top right corner that hides and shows the whole interface, like F10.
- TACTICAL PAUSE in OPTIONS (on by default): press Space to freeze time, look around, give orders and change production, then Space again to go on. Switched off, Space goes to the last message as before.
- UNIT EDITOR: seven test explosions on the SPECIAL tab (COLUMN, FUEL, CHAIN, GEYSER, FLAK, SHOCK, BURST): drag one onto the field, or click it and then the field.
- Now and then a vehicle or gun that blows up in view goes up in one of four big new explosions, the heavy tank, the MAMMOTH and the BIZON every time (jeeps, light tanks, the GATLING and the GUN never), a turret thrown high may burst in the air, and every destroyed building sends out a shock wave.
- OPTIONS > GRAPHICS: a new row 3D PICTURE (SHARP, SOFT, SOFTEST) sets how small GRAPHICS QUALITY LOW draws the battlefield; menus and text stay sharp. LOW only, not on the Mac.
- OPTIONS > GAME: a new row ROBOT SIZE, under VEHICLE SIZE, draws the robots smaller, down to 60 %; only the look changes. The default is 100 %, as before.
- A repaired bridge comes back with see-through orange pieces flying into place like a puzzle while orange light sweeps over it; a repaired factory, radar or repair station comes back the same way.
- Every river, canal and swamp reflects the glitter of the sun and the light of fires and explosions, softly in battle and brighter in the briefings; the ripples on levels 17 and 18 are low and calm.
- The OpenZ Launcher (Windows), a small program in the game folder: start the game with it, and from the next version on it downloads, checks and installs each new version by itself; settings and saves stay as they are.
- A squad losing people on your screen calls for help on the radio with real urgency while the fight is still on, never after it has fallen or the duel is won.
- Machine gun and sniper bullets that hit sound like hits: one sound on a robot, another on metal, another on a building.
- Water mirrors the bridges, banks and units in it, and rockets and shells landing in it throw up white spray.
Changed:
- Missile cannons on fort towers fire half as fast (every 2.2 s); missile cannons on the ground are unchanged.
- A vehicle that captures a flag and has no further order rolls a short way off it to a free spot beside the flag.
- Captured or abandoned vehicles and guns fill into their new colour over a moment instead of switching at once.
- Crocodiles no longer stop and slowly surface before they strike: they swim straight in and lunge.
- When a tank or the mech blows up while its driver is still climbing out, the blast throws him clear: he lies stunned for a moment, then gets up.
- Heavy hits knock chips of painted armour off where the shell strikes, and the chips lie on the ground for a while.
- The smoke at a tank's muzzle and behind shells and rockets is a soft grey wisp instead of a row of white balls.
- Level 2 has one dust devil, a tall twisting column of sand with dust swirling round its foot.
- UNIT EDITOR: EXIT DRIVER sits in a picked tank's card, flags can stand on PLATFORM and TOWER decks and ramps, and towers and platforms collapse into concrete slabs and rubble with dust and sound.
- The fort's mega cannon wreck now shakes the screen when it crashes down.
- The Bizon and the Mammoth now wear desert paint like the heavy tank.
- MINIMAL UI: the OPTIONS tip now says that in a match the MINIMAL UI key (U, see CONTROLS) switches it.
- MINIMAL UI: in a match, H with nothing selected now switches it too, as H hides the panels in the Unit editor; with units selected, H still orders HOLD POSITION.
- While your squad marches, the one robot nearest a flag that is not yours runs off to take it, and the one nearest an empty gun by the way goes to man it; the rest keep walking.
- Units shoot at the guns on an enemy fort's towers as soon as the tower is within their reach, before the robots on the roof or anything else.
- Pits and ravines lower than the ground around them no longer block sight or shots; they still cannot be crossed.
- Bridges go down about as fast as in Z: an MLRS needs six volleys instead of eleven, and other weapons need fewer shots too.
- A destroyed bridge goes up in a chain of blasts, a long bridge in three big charges along its span.
- A crane repairs a bridge from the bank at its end instead of driving out onto the broken span, to a new construction sound, and you hear 'Bridge repaired' once.
- GRAPHICS QUALITY LOW leaves out the sun and fire glints on the water and draws the far scenery more simply, to run lighter.
- Grunts, Psychos, the jeep and the GATLING hit robots on foot harder.
- A missile cannon takes 40 % longer to build than a howitzer: 4:11 instead of 3:02.
- A transport ship sent to a spot where it cannot land flies to the nearest place where it can.
- The MAMMOTH and the BIZON: the driver rides up in the hatch for most of a fight, so a good sniper can pick him off.
- UNIT EDITOR: the zoom tools sit in the BACKGROUND box, the RED / BLUE / NEUTRAL buttons are bigger and show their team colour, the FPS counter sits in the top bar, and a paused fight darkens the corners.
- The dark smoke of a turret bursting in the air clears in a few seconds, and a fort taken by one of your units falls without a shock wave.
- The loading bar no longer stands still at the end: while the battlefield is built the screen says so. New loading pictures for level 2 and the CAR PARK card.
- Mac: lighter on big screens and older Macs. Macs with Intel or AMD graphics start on LOW, and the loading screen gets the graphics ready before the battle.
Fixed:
- Tanks no longer lose their hatch lid when they are hit hard.
- Crater marks fade softly into any ground, with no pale ring round the edge.
- Shell hits leave soft scorch marks with no ring round them.
- A tank stepping aside from a shell no longer throws up round pale balls of dust.
- The card of a selected tank or gun names its real driver, so after a driver swap it no longer looks as if a Grunt still drives it.
- Wrecks and wading robots sit in the water instead of on top of it.
- A vehicle with a track over the water dips that side into it, and a turret thrown onto the river's edge goes into the water instead of lying on top of it.
- Tanks and robots kick up the same soft dust everywhere, the Unit editor and the big tanks included, darker at night, with no round puffs.
- UNIT EDITOR: unit names show only when you point at a unit or pick it, instead of staying stuck over every unit, and a tank on a platform ramp lies along the ramp.
- GRAPHICS QUALITY LOW is much lighter on big screens: the battlefield is drawn at a smaller size there, while menus and text stay sharp.
- Slow graphics chips: the first start picks GRAPHICS QUALITY LOW, LIGHTEST FOR SLOW PCS and FRAME RATE LIMIT 30 by itself; shadows and the other lightest rows are off on LOW only.
- Linux: the game keeps a copy of your settings in its own folder and puts them back if they get lost.
- On the jungle level the gravel paths from the factories to the roads no longer fade away when you zoom out.
- Wrecks, thrown turrets, gun seats and the helmets and packs of fallen robots no longer hang in the air, and no fire burns in mid-air.
- The jeep's wheels have hubcaps instead of a hole in the middle.
- RESOLUTION: a left click picks the next larger size, a right click (or Shift + click) the next smaller one; with FULLSCREEN off the window always fits on your screen with its title bar.
- Choosing DX11 under GRAPHICS API really starts the game on DX11 (it used to stay on DX12).
- CHANGES & UPDATES in the main menu lists every change, those of 1.4, 1.3, 1.2, 1.1 and 1.0 included, and opens with the main additions of 1.5.

EXPERIMENTAL (provided as is, not tried on real devices: tell us how it works for you)
- Touch screen and gamepad input (the TOUCH and GAMEPAD row on the ADVANCED tab of OPTIONS).
- Mac and Linux versions, as separate downloads.

FILES
- LICENSES.txt: the licence texts (GPL-3.0, the Zod Engine notices, the SIL Open Font License of the Inter Tight font).
- LICENSE_ASSETS.txt: the licence of the art, sound and all other assets (CC BY-NC-SA 4.0).
- SHA256SUMS.txt: a checksum of every file. Check one in PowerShell: Get-FileHash OpenZ.exe -Algorithm SHA256

NOTES
- OpenZ is an unofficial, free, non-commercial fan remake, not affiliated with or endorsed by Rebellion.
- No copyrighted material is used: every asset was made from scratch. The game rules follow the open-source Zod Engine (GPL-3.0).
- The program code is under the GNU GPL v3; the art, sound and all other assets are under the Creative Commons
  Attribution-NonCommercial-ShareAlike 4.0 International licence (CC BY-NC-SA 4.0). See LICENSES.txt and LICENSE_ASSETS.txt.
";
        }
    }

    /// <summary>
    /// of the release player. Other builds are untouched.</summary>
    class OzReleaseAssemblyFilter : IFilterBuildAssemblies
    {
        public int callbackOrder => 0;
        public string[] OnFilterAssemblies(BuildOptions buildOptions, string[] assemblies)
        {
            if (!OpenZReleaseBuild.Building) return assemblies;
            var keep = assemblies.Where(a => !Path.GetFileName(a).StartsWith("EditorBridge", StringComparison.OrdinalIgnoreCase)).ToArray();
            if (keep.Length != assemblies.Length) Debug.Log($"[OpenZ Release] left out of the player: {string.Join(", ", assemblies.Except(keep).Select(Path.GetFileName))}");
            return keep;
        }
    }
}
