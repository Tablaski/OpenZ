using System;
using System.Diagnostics;
using System.IO;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace OpenZ.Multiplayer.Editor
{
    public sealed class OzForkBuildStamp : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        public int callbackOrder => 10000;
        static OzForkVersion.BuildIdentity identity;
        static string Git(string arguments)
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
            var info = new ProcessStartInfo("git", "-c safe.directory=\"" + root.Replace('\\', '/') + "\" " + arguments)
            { WorkingDirectory = root, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
            using (var process = Process.Start(info))
            {
                string output = process.StandardOutput.ReadToEnd(); string error = process.StandardError.ReadToEnd(); process.WaitForExit();
                if (process.ExitCode != 0) throw new BuildFailedException("Cannot identify fork source: " + error);
                return output.Trim();
            }
        }
        public void OnPreprocessBuild(BuildReport report)
        {
            identity = new OzForkVersion.BuildIdentity { version = OzForkVersion.Number, commit = Git("rev-parse HEAD"),
                dirty = Git("status --porcelain").Length > 0, builtUtc = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss 'UTC'") };
        }
        public void OnPostprocessBuild(BuildReport report)
        {
            string folder = Path.GetDirectoryName(report.summary.outputPath);
            File.WriteAllText(Path.Combine(folder, "FORK_BUILD.json"), JsonUtility.ToJson(identity, true));
        }
    }
}
