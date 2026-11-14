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
        static readonly string[] RequiredScenes = { "Intro", "MainMenu", "L02_Desert", "L07_Volcanic", "L16_Jungle", "L17_CarPark" };
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
            if (missing.Length > 0) return Fail("Build Settings scenes miss " + string.Join(", ", missing) + " (Intro, MainMenu, L02_Desert, L07_Volcanic, L16_Jungle, L17_CarPark must be enabled)");
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
Free and non-commercial. Four maps: Z level 2 (desert), Z level 7 (volcano), Z level 16 (jungle) and Z level 17 ""Car Park"" (city).

START
- Run OpenZ.exe. The game opens full screen at your screen's resolution (Alt+Enter or OPTIONS > FULLSCREEN for a window).
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

NEW IN 1.4
New:
- MAYHEM, a fourth difficulty after HARD: the computer plays as on HARD, and the battlefield starts under the fog of war, a drifting cloud that opens wherever your units, buildings and flags have been. Explored ground stays open for the whole match, and enemies in unexplored ground stay hidden, on the minimap and under the pointer too. The cloud is drawn for each kind of planet, and a captured radar opens a wide circle in it. MAYHEM keeps its own high scores.
- On MAYHEM the enemy also builds 10 % faster: every unit and gun in its fort and factories is ready 10 % sooner than the same one in yours.
- EXIT DRIVER (the button above the unit panel, or E): the driver of a tank or the mech springs out of the hatch with one quick flip and lands on his feet beside it. For 30 seconds nobody climbs in by himself, so you can right-click the robot you want onto it as the new driver.
- A tank or the mech whose driver jumps out or is sniped stops dead at once and its guns fall silent: no driving, turning or firing until someone climbs back in.
- A tank or the mech with a driver who is not a grunt steps aside from incoming shells a little faster. Grunt drivers and other vehicles dodge as before.
- Rally points from the minimap: after SET RALLY, click the minimap to set the rally point there; Shift with clicks makes a route.
- OPTIONS, GAME tab: RANGE RINGS ON SELECTED UNITS. FADE (the default) shows the firing-range rings for about 2 seconds after you select, as before; ALWAYS keeps them on while your units stay selected.
- REPEAT in every build menu has three answers for when the list runs out: OFF (the factory goes idle), LAST (the last unit keeps being built, the default as before) or ALL (the whole list loops).
- OPTIONS, GAME tab: SCROLL SPEED (50 to 300 %), RIGHT-DRAG SCROLL (hold the right button and drag to move the map) and MIDDLE MOUSE (turn the view, drag the map, or drag the map and turn it with Alt) in place of FREE CAMERA. The defaults play as before.
- Robots standing near the enemy fort's door with no orders run inside by themselves and finish it, as soon as no enemy guards the door. Your own orders and HOLD POSITION come first.
- UNIT EDITOR: X10 and X20 drop counts for setting up big battles fast, and the NUKE grows a new glowing mushroom cloud, with dust that rolls out in soft puffs of many sizes. Its column of fire is gone before the cloud starts to fade.
Changed:
- The game runs faster: the same battles take about 10 to 20 % less work from your computer, so busy views keep a higher frame rate.
- One Windows download for everyone: nothing to choose.
- The gatling reaches 10 % farther and each shot does 15 % more damage. It fires as fast as before.
- The laser robot's gun glows green for a moment before each shot, and its beam flies a little quicker and looks crisper. Damage, range and fire rate are as before.
- General EvilZod turns to face his target before he fires. His power dash recharges twice as fast, and when he is shot at from too far away to fire back, he dashes at the nearest enemy by himself.
- Enemy health bars show only while you own a standing radar, like the enemy dots on the minimap. Your own units keep theirs.
- The dramatic music comes in as soon as a fort is really under fire, even from a single tank, and it stands out clearly from the battle songs.
- A right-click on the enemy fort's door, the ground in front of its gate or its walls and roof sends your selected units in: a much bigger area to click.
- STOP sits at the end of the build progress bar, beside the unit it stops, and a small X beside SET RALLY clears the rally points (it was the CLEAR button).
- The map lags behind less when the game slows down for a moment.
- Level 7's card on the battlefield screen plays its familiar volcano flight, recorded again sharp and smooth, and the loading screen now gets level 7 ready before its first briefing.
- MAYHEM: HEAD START's extra vehicles are locked off, as MAYHEM was too easy with them.
- MAYHEM: enemies and empty vehicles come into view smoothly at the edge of the fog instead of popping up.
- General EvilZod's win lines after a kill come much less often.
- After jumping out of a vehicle, the driver always brings his gun up quickly once he lands, whatever kind of robot he is.
- No unit, not even a fort or the computer's units, keeps shooting at a vehicle once its crew is out.
- A robot killed by a rocket falls by his death only, without the hit wobble fighting it.
Fixed:
- Radar voices: no ""RADAR ACTIVATED"" for a ruined radar, ""Radar lost."" only when your last radar is really gone, and voice lines no longer talk over each other.
- A turret blown off a tank or a gun still spins through the air, then lies still where it lands: no more hops or slides up a slope.
- Level 7: a lava pterodactyl that swoops down to attack where you can see it always screams, with one of three new screams, never the same one twice in a row.
- UNIT EDITOR: EvilZod is offered only for the blue side, Alan and Brad only for the red side.
- The main menu shows the right version number.
- The blurred background of the level selection is now smooth and soft: no more big flickering blocks.
- MAYHEM: after the briefing the fog of war eases in smoothly instead of snapping in.
- The rank star in the unit panel no longer stays huge after the HUD has been hidden.
- CHANGES & UPDATES in the main menu lists every change, those of 1.3, 1.2, 1.1 and 1.0 included, and opens with the main additions of 1.4.

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
