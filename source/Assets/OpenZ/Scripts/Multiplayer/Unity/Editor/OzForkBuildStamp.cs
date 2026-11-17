using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace OpenZ.Multiplayer.Editor
{
    public sealed class OzForkBuildStamp : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        public int callbackOrder => 10000;
        static OzForkVersion.BuildIdentity identity;
        [Serializable] sealed class ResourceIdentity { public string version, resourcePackage, commit, builtUtc; public bool dirty; }
        static ResourceIdentity resourceIdentity;
        static void PrepareResources(string root)
        {
            string pointer = Path.Combine(root, "SOURCE_RESOURCES.json");
            // Historical revisions predate resource identities and retain their original build path.
            if (!File.Exists(pointer)) { resourceIdentity = null; return; }
            var required = JsonUtility.FromJson<ResourceIdentity>(File.ReadAllText(pointer));
            string assets = Path.Combine(Application.dataPath, "OpenZ/Resources/OpenZ");
            string marker = Path.Combine(assets, "resource_package.json");
            if (!File.Exists(marker) || JsonUtility.FromJson<ResourceIdentity>(File.ReadAllText(marker)).version != required.version)
                throw new BuildFailedException("The Unity resource package does not match SOURCE_RESOURCES.json. Prepare the required resources with the installer before building.");
            resourceIdentity = new ResourceIdentity { version = identity.version, resourcePackage = required.version, commit = identity.commit, builtUtc = identity.builtUtc, dirty = identity.dirty };
            File.WriteAllText(Path.Combine(assets, "fork_build_resources.json"), JsonUtility.ToJson(resourceIdentity, true));
            UnityEditor.AssetDatabase.Refresh(UnityEditor.ImportAssetOptions.ForceSynchronousImport);
        }
        [Serializable] sealed class SourceFile { public string path, sha256; }
        [Serializable] sealed class SourceIdentity { public string repository, commit; public bool compatibilityPatch; public SourceFile[] files; }
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
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
            string snapshot = Path.Combine(root, "FORK_SOURCE.json");
            if (!Directory.Exists(Path.Combine(root, ".git")) && File.Exists(snapshot))
            {
                var source = JsonUtility.FromJson<SourceIdentity>(File.ReadAllText(snapshot));
                if (source == null || source.repository != "Tablaski/OpenZ" || source.commit == null ||
                    !System.Text.RegularExpressions.Regex.IsMatch(source.commit, "^[a-f0-9]{40}$") || source.files == null || source.files.Length == 0)
                    throw new BuildFailedException("Invalid installer source identity.");
                bool modified = source.compatibilityPatch;
                foreach (var entry in source.files)
                {
                    string path = Path.GetFullPath(Path.Combine(root, entry.path));
                    if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new BuildFailedException("Invalid source identity path.");
                    if (!File.Exists(path)) { modified = true; continue; }
                    using (var input = File.OpenRead(path)) using (var sha = SHA256.Create())
                        if (BitConverter.ToString(sha.ComputeHash(input)).Replace("-", "").ToLowerInvariant() != entry.sha256) modified = true;
                }
                var known = new System.Collections.Generic.HashSet<string>(source.files.Select(f => f.path.Replace('\\', '/')), StringComparer.OrdinalIgnoreCase);
                foreach (string path in Directory.GetFiles(Path.Combine(Application.dataPath, "OpenZ/Scripts"), "*.cs", SearchOption.AllDirectories))
                    if (!known.Contains(path.Substring(root.Length + 1).Replace('\\', '/'))) modified = true;
                identity = new OzForkVersion.BuildIdentity { version = OzForkVersion.Number, commit = source.commit,
                    dirty = modified, builtUtc = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss 'UTC'") };
                PrepareResources(root); return;
            }
            identity = new OzForkVersion.BuildIdentity { version = OzForkVersion.Number, commit = Git("rev-parse HEAD"),
                dirty = Git("status --porcelain").Length > 0, builtUtc = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss 'UTC'") };
            PrepareResources(root);
        }
        public void OnPostprocessBuild(BuildReport report)
        {
            string folder = Path.GetDirectoryName(report.summary.outputPath);
            File.WriteAllText(Path.Combine(folder, "FORK_BUILD.json"), resourceIdentity == null ? JsonUtility.ToJson(identity, true) : JsonUtility.ToJson(resourceIdentity, true));
        }
    }
}
