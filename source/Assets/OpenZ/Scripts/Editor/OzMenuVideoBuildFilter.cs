using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// 1.3 item 31 (GitHub issue 2): each build carries only the menu flybys its player can play. Resources are always packed, so
    /// without this every build would carry both sets:
    ///  * a Linux build leaves out the H.264 .mp4 flybys (the 8 full clips and item 23's 8 light copies, about 470 MB): the Linux player
    ///    cannot decode H.264. It plays the VP8 copies &lt;name&gt;_vp8.webm (UI/OzMenuVideo.cs, LinuxSuffix);
    ///  * every other build (Windows, Mac) leaves out the VP8 copies: those players play the .mp4 as in 1.2. Nothing they show changes.
    /// How: just before the build the files are moved (AssetDatabase.MoveAsset: same GUIDs, same import settings) from
    /// Assets/OpenZ/Resources/OpenZ/Menu to Assets/OpenZ/MenuVideoParked (not a Resources folder, nothing refers to them, so the build
    /// does not take them), and moved back as soon as the build returns: by the build script (OpenZLinuxBuild), else at the first editor
    /// update after the build, else at the next editor start or script reload. Every move is written to
    /// Library/OpenZ_menu_video_parked.txt at once, so a crash in the middle leaves an exact list to move back. Nothing is deleted.
    /// </summary>
    [InitializeOnLoad]
    public sealed class OzMenuVideoBuildFilter : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        public const string MenuDir = "Assets/OpenZ/Resources/OpenZ/Menu";
        public const string ParkParent = "Assets/OpenZ", ParkName = "MenuVideoParked";
        public static string ParkDir => ParkParent + "/" + ParkName;
        const string Tag = "[OpenZ MenuVideo]";
        static string Manifest => Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/OpenZ_menu_video_parked.txt"));

        static OzMenuVideoBuildFilter() { EditorApplication.delayCall += () => RestoreAll("editor start or script reload"); }

        public int callbackOrder => -100;                                                                    // before the other build hooks

        /// <summary>The files this platform's build leaves out (project-relative paths).</summary>
        public static List<string> LeftOut(BuildTarget target)
        {
            bool linux = target == BuildTarget.StandaloneLinux64;
            var res = new List<string>();
            if (!Directory.Exists(MenuDir)) return res;
            foreach (var f in Directory.GetFiles(MenuDir))
            {
                string p = f.Replace('\\', '/');
                if (p.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;
                bool mp4 = p.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase);
                bool vp8 = p.EndsWith(OzMenuVideo.LinuxSuffix + ".webm", StringComparison.OrdinalIgnoreCase);
                if (linux ? mp4 : vp8) res.Add(p);
            }
            return res;
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            RestoreAll("before a build");                                                                  // a crash left files parked: back first
            var target = report.summary.platform;
            var files = LeftOut(target);
            if (files.Count == 0) { Debug.Log($"{Tag} {target} build: no menu flyby to leave out"); return; }
            if (!AssetDatabase.IsValidFolder(ParkDir)) AssetDatabase.CreateFolder(ParkParent, ParkName);
            var done = new List<string>(); long bytes = 0;
            foreach (var src in files)
            {
                string dst = ParkDir + "/" + Path.GetFileName(src);
                if (File.Exists(dst)) { Debug.LogWarning($"{Tag} {dst} exists already: {src} stays in the build"); continue; }
                long size = new FileInfo(src).Length;
                string err = AssetDatabase.MoveAsset(src, dst);
                if (!string.IsNullOrEmpty(err)) { Debug.LogWarning($"{Tag} could not move {src} out of the build: {err} (it stays in the build)"); continue; }
                done.Add(src + "|" + dst); bytes += size;
                File.WriteAllLines(Manifest, done);                                                          // after every move
            }
            Debug.Log($"{Tag} {target} build: {done.Count} of {files.Count} menu flyby files left out of this build, {bytes / 1048576.0:0.0} MiB "
                      + (target == BuildTarget.StandaloneLinux64 ? "(the H.264 .mp4: the Linux player cannot play them; it plays the VP8 copies)" : "(the Linux VP8 copies: this player plays the .mp4)")
                      + $", parked in {ParkDir} until the build returns");
            EditorApplication.update -= AfterBuild; EditorApplication.update += AfterBuild;
        }

        public void OnPostprocessBuild(BuildReport report) { }                                              // moved back after the build returns (AfterBuild), not inside it

        static void AfterBuild()
        {
            if (BuildPipeline.isBuildingPlayer) return;
            EditorApplication.update -= AfterBuild;
            RestoreAll("the build returned");
        }

        /// <summary>Every parked file back to its place (safe to call any time; does nothing when nothing is parked).</summary>
        public static int RestoreAll(string when)
        {
            if (!File.Exists(Manifest)) return 0;
            var lines = File.ReadAllLines(Manifest).Where(l => l.Contains("|")).ToArray();
            if (lines.Length == 0) return 0;
            var left = new List<string>(); int back = 0;
            foreach (var l in lines)
            {
                var p = l.Split('|');
                if (File.Exists(p[0]) || !File.Exists(p[1])) continue;                                       // back already (or gone by hand)
                string err = AssetDatabase.MoveAsset(p[1], p[0]);
                if (string.IsNullOrEmpty(err)) back++; else { left.Add(l); Debug.LogError($"{Tag} could not move {p[1]} back to {p[0]}: {err}"); }
            }
            File.WriteAllLines(Manifest, left);                                                              // an empty list = nothing parked
            Debug.Log($"{Tag} {when}: {back} menu flyby file(s) back in {MenuDir}" + (left.Count > 0 ? $", {left.Count} NOT back (listed in {Manifest})" : ""));
            return back;
        }

        /// <summary>The build record line: which menu flybys the finished build holds (from its report).</summary>
        public static string Audit(BuildReport rep)
        {
            int mp4 = 0, vp8 = 0; long mp4B = 0, vp8B = 0;
            try
            {
                foreach (var pa in rep.packedAssets)
                    foreach (var c in pa.contents)
                    {
                        string s = c.sourceAssetPath ?? "";
                        if (!s.StartsWith(MenuDir + "/", StringComparison.OrdinalIgnoreCase) && !s.StartsWith(ParkDir + "/", StringComparison.OrdinalIgnoreCase)) continue;
                        if (s.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase)) { mp4++; mp4B += (long)c.packedSize; }
                        else if (s.EndsWith(".webm", StringComparison.OrdinalIgnoreCase)) { vp8++; vp8B += (long)c.packedSize; }
                    }
            }
            catch (Exception e) { return $"menu flybys in the build: not read ({e.GetType().Name})"; }
            return $"menu flybys in the build: {vp8} VP8 .webm ({vp8B / 1048576.0:0.0} MiB), {mp4} H.264 .mp4 ({mp4B / 1048576.0:0.0} MiB)";
        }
    }
}
