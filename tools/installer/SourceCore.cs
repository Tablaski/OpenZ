using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace OpenZVK.Update
{
    public sealed class CommitAuthor { public string date; }
    public sealed class CommitBody { public string message; public CommitAuthor author; }
    public sealed class CommitChoice
    {
        public string sha; public CommitBody commit;
        public override string ToString() { return sha.Substring(0, 10) + " · " + (commit.message ?? "").Split('\n')[0]; }
    }
    public sealed class SourceFile { public string path, sha256; }
    public sealed class SourceIdentity
    {
        public string repository = Format.Repository, commit, branch, mode;
        public bool compatibilityPatch;
        public List<SourceFile> files = new List<SourceFile>();
    }
    public sealed class ResourceArchive { public string name, sha256; public long length; }
    public sealed class ResourceFile { public string path, sha256, archive; public long length; }
    public sealed class ResourceIndex { public int schema; public string version, releaseTag; public List<ResourceArchive> archives; public List<ResourceFile> files; }
    public sealed class ResourceState { public string version; public List<ResourceFile> files = new List<ResourceFile>(); }
    public sealed class SourceManager
    {
        readonly GitHub github;
        public SourceManager(GitHub github) { this.github = github; }
        public static void ValidateWorkspace(string workspace)
        {
            string full = Path.GetFullPath(workspace);
            if (full.Substring(Path.GetPathRoot(full).Length).Split(Path.DirectorySeparatorChar).Any(p => p.StartsWith(".", StringComparison.Ordinal)))
                throw new IOException("Unity source projects cannot be inside dot-prefixed folders. Choose a regular folder such as D:\\OpenZ-VK-Source.");
        }
        public static string FindGit()
        {
            var candidates = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator).Select(p => Path.Combine(p.Trim('"'), "git.exe")).ToList();
            candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Git", "cmd", "git.exe"));
            return candidates.FirstOrDefault(File.Exists);
        }
        public static async Task<string> Run(string executable, string directory, IEnumerable<string> arguments, Action<string> log, CancellationToken cancellation)
        {
            var info = new ProcessStartInfo(executable, String.Join(" ", arguments.Select(Quote))) { WorkingDirectory = directory, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            info.EnvironmentVariables["GIT_TERMINAL_PROMPT"] = "0";
            using (var process = Process.Start(info))
            {
                var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
                while (!process.HasExited)
                {
                    if (cancellation.IsCancellationRequested) { try { process.Kill(); } catch { } cancellation.ThrowIfCancellationRequested(); }
                    await Task.Delay(200).ConfigureAwait(false);
                }
                string output = await stdout.ConfigureAwait(false), error = await stderr.ConfigureAwait(false);
                if (!String.IsNullOrWhiteSpace(output)) log(output.Trim()); if (!String.IsNullOrWhiteSpace(error)) log(error.Trim());
                if (process.ExitCode != 0) throw new IOException(Path.GetFileName(executable) + " failed (" + process.ExitCode + "). " + error);
                return output.Trim();
            }
        }
        static string Quote(string value)
        {
            // Windows command-line quoting; no command shell is involved.
            var result = new StringBuilder("\""); int slashes = 0;
            foreach (char c in value) { if (c == '\\') { slashes++; continue; } if (c == '"') result.Append('\\', slashes * 2 + 1); else result.Append('\\', slashes); result.Append(c); slashes = 0; }
            result.Append('\\', slashes * 2); return result.Append('"').ToString();
        }
        public async Task<List<CommitChoice>> Commits(string branch, int page, CancellationToken cancellation)
        {
            if (branch != "main" && branch != "dev") throw new ArgumentException("Choose main or dev.");
            return await github.Api<List<CommitChoice>>("commits?sha=" + branch + "&per_page=100&page=" + page, cancellation).ConfigureAwait(false);
        }
        public async Task<SourceIdentity> Retrieve(string workspace, string branch, string revision, bool useGit, Action<string> log, CancellationToken cancellation)
        {
            if (branch != "main" && branch != "dev") throw new ArgumentException("Choose main or dev.");
            if (!Regex.IsMatch(revision ?? "", "^[a-fA-F0-9]{7,40}$")) throw new ArgumentException("Choose a commit or enter its 7–40 character hexadecimal ID.");
            var commit = await github.Api<CommitChoice>("commits/" + revision, cancellation).ConfigureAwait(false);
            if (!Regex.IsMatch(commit.sha ?? "", "^[a-f0-9]{40}$")) throw new InvalidDataException("GitHub did not identify the source commit.");
            workspace = Path.GetFullPath(workspace).TrimEnd(Path.DirectorySeparatorChar);
            ValidateWorkspace(workspace);
            Format.SafePath(Path.GetDirectoryName(workspace), Path.GetFileName(workspace));
            string marker = Path.Combine(workspace, "FORK_SOURCE.json");
            SourceIdentity previous = null;
            if (File.Exists(marker)) { previous = Format.Json().Deserialize<SourceIdentity>(File.ReadAllText(marker)); if (previous.repository != Format.Repository) throw new IOException("This workspace belongs to another project."); }
            else if (Directory.Exists(workspace) && Directory.EnumerateFileSystemEntries(workspace).Any()) throw new IOException("Choose an empty source workspace. Existing projects can be used as a resource source.");
            var identity = new SourceIdentity { commit = commit.sha, branch = branch, mode = useGit ? "git" : "archive" };
            if (useGit)
            {
                string git = FindGit(); if (git == null) throw new IOException("Git is not installed. Continue without Git or install it from git-scm.com.");
                bool cloned = false;
                if (!Directory.Exists(Path.Combine(workspace, ".git")))
                {
                    if (Directory.Exists(workspace) && Directory.EnumerateFileSystemEntries(workspace).Any()) throw new IOException("Use a new empty workspace when switching from archive mode to Git.");
                    Directory.CreateDirectory(Path.GetDirectoryName(workspace));
                    await Run(git, Path.GetDirectoryName(workspace), new[] { "clone", "--filter=blob:none", "--no-checkout", "https://github.com/" + Format.Repository + ".git", workspace }, log, cancellation).ConfigureAwait(false);
                    cloned = true;
                }
                string exclude = Path.Combine(workspace, ".git", "info", "exclude");
                File.AppendAllText(exclude, "\n/FORK_SOURCE.json\n/.local/\n/builds/\n");
                string dirty = cloned ? "" : await Run(git, workspace, new[] { "status", "--porcelain", "--untracked-files=no" }, log, cancellation).ConfigureAwait(false);
                if (dirty.Length != 0) throw new IOException("This checkout contains local source changes. Save them or choose another workspace before switching versions.");
                await Run(git, workspace, new[] { "fetch", "origin", branch }, log, cancellation).ConfigureAwait(false);
                await Run(git, workspace, new[] { "checkout", "--detach", commit.sha }, log, cancellation).ConfigureAwait(false);
                string tracked = await Run(git, workspace, new[] { "ls-files" }, s => { }, cancellation).ConfigureAwait(false);
                foreach (string path in tracked.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                    if (File.Exists(Format.SafePath(workspace, path))) identity.files.Add(new SourceFile { path = path, sha256 = Format.FileHash(Format.SafePath(workspace, path)) });
            }
            else
            {
                if (Directory.Exists(Path.Combine(workspace, ".git"))) throw new IOException("This workspace uses Git. Install Git or choose a separate archive workspace.");
                Directory.CreateDirectory(workspace);
                if (previous == null) File.WriteAllText(marker, Format.Json().Serialize(new SourceIdentity { mode = "archive", branch = branch }), new UTF8Encoding(false));
                if (previous != null)
                    foreach (var file in previous.files)
                    {
                        string path = Format.SafePath(workspace, file.path);
                        if (File.Exists(path) && Format.FileHash(path) != file.sha256) throw new IOException("Local source changes are preserved. Use another workspace or save your changes before switching: " + file.path);
                    }
                string archive = Path.Combine(workspace, ".local", "downloads", commit.sha + ".zip");
                log("Downloading source snapshot " + commit.sha.Substring(0, 10) + " without Git. Game resources will be retained.");
                await github.DownloadFile("https://codeload.github.com/" + Format.Repository + "/zip/" + commit.sha, archive, null, (n, total) => log("Source download: " + Format.Size(n)), cancellation).ConfigureAwait(false);
                ApplyArchive(workspace, archive, previous, identity, cancellation);
                string stamp = Path.Combine(workspace, "source/Assets/OpenZ/Scripts/Multiplayer/Unity/Editor/OzForkBuildStamp.cs");
                if (File.Exists(stamp) && !File.ReadAllText(stamp).Contains("FORK_SOURCE.json"))
                {
                    using (var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("OpenZVK.BuildStamp"))
                    using (var input = new StreamReader(resource)) File.WriteAllText(stamp, input.ReadToEnd(), new UTF8Encoding(false));
                    identity.compatibilityPatch = true;
                    // This is an explicit build-provenance compatibility change. Keep
                    // it visible in the player stamp and permit a later source switch.
                    identity.files.First(f => f.path == "source/Assets/OpenZ/Scripts/Multiplayer/Unity/Editor/OzForkBuildStamp.cs").sha256 = Format.FileHash(stamp);
                }
            }
            File.WriteAllText(marker, Format.Json().Serialize(identity), new UTF8Encoding(false));
            log("Source ready: " + branch + " · " + commit.sha + " · " + identity.mode + ".");
            return identity;
        }
        public static void ApplyArchive(string workspace, string archive, SourceIdentity previous, SourceIdentity identity, CancellationToken cancellation)
        {
            var entries = ReadArchive(archive, true);
            string staging = Path.Combine(workspace, ".local", "source-stage-" + Guid.NewGuid().ToString("N"));
            string backup = Path.Combine(staging, "backup"), incoming = Path.Combine(staging, "incoming");
            var touched = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using (var zip = ZipFile.OpenRead(archive)) foreach (var item in entries)
                {
                    cancellation.ThrowIfCancellationRequested(); string output = Format.SafePath(incoming, item.Key); Directory.CreateDirectory(Path.GetDirectoryName(output));
                    using (var input = zip.GetEntry(item.Value).Open()) using (var stream = File.Create(output)) input.CopyTo(stream);
                    identity.files.Add(new SourceFile { path = item.Key, sha256 = Format.FileHash(output) });
                }
                foreach (string relative in entries.Keys.Concat(previous == null ? new string[0] : previous.files.Select(f => f.path)).Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    cancellation.ThrowIfCancellationRequested(); string target = Format.SafePath(workspace, relative);
                    bool existed = File.Exists(target);
                    if (existed) { string saved = Format.SafePath(backup, relative); Directory.CreateDirectory(Path.GetDirectoryName(saved)); File.Copy(target, saved); }
                    touched.Add(relative, existed);
                    if (entries.ContainsKey(relative)) { Directory.CreateDirectory(Path.GetDirectoryName(target)); File.Copy(Format.SafePath(incoming, relative), target, true); }
                    else if (existed) File.Delete(target);
                }
            }
            catch
            {
                foreach (var item in touched) { string path = Format.SafePath(workspace, item.Key); if (item.Value) File.Copy(Format.SafePath(backup, item.Key), path, true); else if (File.Exists(path)) File.Delete(path); }
                throw;
            }
            finally { if (Directory.Exists(staging)) Directory.Delete(staging, true); }
        }
        public static Dictionary<string, string> ReadArchive(string archive, bool source)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); long total = 0;
            using (var zip = ZipFile.OpenRead(archive))
                foreach (var entry in zip.Entries)
                {
                    if (entry.FullName.EndsWith("/")) continue;
                    string path = entry.FullName;
                    if (source) { int slash = path.IndexOf('/'); if (slash < 0) throw new InvalidDataException("Missing snapshot root."); path = path.Substring(slash + 1); }
                    Format.SafePath(Path.Combine(Path.GetPathRoot(Environment.CurrentDirectory), "source-check"), path);
                    if (path.Split('/').Any(s => s == ".git" || s == ".local") || path == "FORK_SOURCE.json") throw new InvalidDataException("Unexpected source metadata in archive.");
                    if (!source && !path.StartsWith("source/Assets/", StringComparison.Ordinal) && !path.StartsWith("source/Packages/org.openz.datachannel/", StringComparison.Ordinal)) throw new InvalidDataException("Unexpected resource archive entry.");
                    total += entry.Length;
                    if (entry.Length > 4L * 1024 * 1024 * 1024 || total > (source ? 2L : 16L) * 1024 * 1024 * 1024 || result.Count > 100000 || result.ContainsKey(path)) throw new InvalidDataException("Invalid archive size or duplicate path.");
                    result.Add(path, entry.FullName);
                }
            return result;
        }
        public async Task Resources(string workspace, string existingProject, Action<string> log, CancellationToken cancellation, string localIndex = null)
        {
            if (!File.Exists(Path.Combine(workspace, "FORK_SOURCE.json"))) throw new IOException("Retrieve a source revision first.");
            string marker = Path.Combine(workspace, ".local", "resources-ready.txt");
            string pointerPath = Path.Combine(workspace, "SOURCE_RESOURCES.json");
            string wantedVersion = File.Exists(pointerPath) ? (string)Format.Json().Deserialize<Dictionary<string, object>>(File.ReadAllText(pointerPath))["version"] : null;
            string statePath = Path.Combine(workspace, ".local", "resources-state.json");
            if (File.Exists(statePath))
            {
                var state = Format.Json().Deserialize<ResourceState>(File.ReadAllText(statePath));
                if (state.version == wantedVersion && state.files.Count > 0 && state.files.All(f => Matches(workspace,f)))
                { log("The required Unity resource package is already installed. No download needed."); return; }
            }
            if (!String.IsNullOrWhiteSpace(existingProject))
            {
                var reused=new ResourceState{version=wantedVersion};
                string project = Directory.Exists(Path.Combine(existingProject, "Assets")) ? existingProject : Path.Combine(existingProject, "source");
                if (!File.Exists(Path.Combine(project, "Assets/OpenZ/Scenes/MainMenu.unity"))) throw new IOException("Select a complete Unity project with the OpenZ scenes.");
                string package = Path.Combine(project, "Assets/OpenZ/Resources/OpenZ/resource_package.json");
                if (wantedVersion != null && (!File.Exists(package) || (string)Format.Json().Deserialize<Dictionary<string,object>>(File.ReadAllText(package))["version"] != wantedVersion))
                    throw new IOException("This local Unity project has a different resource package. Choose the package required by the selected source version: " + wantedVersion);
                foreach (string file in Directory.GetFiles(Path.Combine(project, "Assets"), "*", SearchOption.AllDirectories))
                {
                    cancellation.ThrowIfCancellationRequested(); string relative = "source/" + file.Substring(project.Length + 1).Replace('\\', '/');
                    if (relative.StartsWith("source/Assets/OpenZ/Scripts/", StringComparison.Ordinal) || relative.StartsWith("source/Assets/OpenZ/Shaders/", StringComparison.Ordinal)) continue;
                    if (relative.Contains("/fork_build_resources.json") || relative.Contains("/Menu/build_info.json")) continue;
                    reused.files.Add(new ResourceFile{path=relative,length=new FileInfo(file).Length,sha256=Format.FileHash(file)});
                    string output = Format.SafePath(workspace, relative); if (File.Exists(output) && Format.FileHash(output) == Format.FileHash(file)) continue;
                    BackupResource(workspace, relative); Directory.CreateDirectory(Path.GetDirectoryName(output)); File.Copy(file, output, true);
                }
                string plugin = Path.Combine(project, "Packages/org.openz.datachannel/Plugins/x86_64/datachannel.dll");
                if (File.Exists(plugin)) { string output = Format.SafePath(workspace, "source/Packages/org.openz.datachannel/Plugins/x86_64/datachannel.dll"); Directory.CreateDirectory(Path.GetDirectoryName(output)); File.Copy(plugin, output, true); }
                if(File.Exists(plugin))reused.files.Add(new ResourceFile{path="source/Packages/org.openz.datachannel/Plugins/x86_64/datachannel.dll",length=new FileInfo(plugin).Length,sha256=Format.FileHash(plugin)});
                if(File.Exists(statePath))
                {
                    var prior=Format.Json().Deserialize<ResourceState>(File.ReadAllText(statePath));var paths=new HashSet<string>(reused.files.Select(f=>f.path),StringComparer.OrdinalIgnoreCase);
                    foreach(var old in prior.files.Where(f=>!paths.Contains(f.path))){string path=Format.SafePath(workspace,old.path);if(File.Exists(path)){if(!Matches(workspace,old))BackupResource(workspace,old.path);File.Delete(path);}}
                }
                Directory.CreateDirectory(Path.GetDirectoryName(statePath));File.WriteAllText(statePath,Format.Json().Serialize(reused));
                log("Reused local Unity resources without downloading them.");
            }
            else
            {
                Release release = null; ResourceIndex index;
                if (localIndex != null) index = Format.Json().Deserialize<ResourceIndex>(File.ReadAllText(localIndex));
                else
                {
                    var releases = await github.Api<List<Release>>("releases?per_page=100", cancellation).ConfigureAwait(false);
                    string wantedTag = null; string pointer = Path.Combine(workspace, "SOURCE_RESOURCES.json");
                    if (File.Exists(pointer)) wantedTag = (string)Format.Json().Deserialize<Dictionary<string, object>>(File.ReadAllText(pointer))["releaseTag"];
                    release = releases.FirstOrDefault(r => !r.draft && (wantedTag == null || r.tag_name == wantedTag) && r.assets.Any(a => a.name == "OpenZ-VK-source-resources.json"));
                    if (release == null) throw new IOException("The source resource bundle is not published yet. Select an existing full Unity project or the local review bundle.");
                    index = Format.Json().Deserialize<ResourceIndex>(await github.Text(GitHub.AssetUrl(release.tag_name, "OpenZ-VK-source-resources.json"), 16 * 1024 * 1024, cancellation).ConfigureAwait(false));
                }
                if (index.schema != 2 || index.version != wantedVersion || (release != null && index.releaseTag != release.tag_name) || index.archives == null || index.archives.Count > 20 || index.files == null || index.files.Count == 0) throw new InvalidDataException("The resource bundle does not match the selected source version.");
                ValidateResources(workspace,index);
                var archives = new Dictionary<string,string>(StringComparer.Ordinal);
                foreach (var asset in index.archives)
                {
                    if (!Regex.IsMatch(asset.name ?? "", "^OpenZ-VK-UnityResources-part[0-9]{2}\\.zip$") || !Format.IsHash(asset.sha256) || asset.length <= 0 || asset.length >= 2000000000 || (release != null && !release.assets.Any(a => a.name == asset.name && a.size == asset.length))) throw new InvalidDataException("Invalid source resource asset.");
                    if (index.files.Where(f=>f.archive==asset.name).All(f=>Matches(workspace,f))) continue;
                    string archive = localIndex != null ? Path.Combine(Path.GetDirectoryName(localIndex), asset.name) : Path.Combine(workspace, ".local", "downloads", index.version, asset.name);
                    log("Preparing required Unity resources: " + asset.name);
                    if (localIndex != null) { if (!File.Exists(archive) || new FileInfo(archive).Length != asset.length || Format.FileHash(archive) != asset.sha256) throw new InvalidDataException("Local resource bundle checksum mismatch."); }
                    else await github.DownloadFile(GitHub.AssetUrl(release.tag_name, asset.name), archive, asset.sha256, (n, total) => log(asset.name + ": " + Format.Size(n) + " / " + Format.Size(total)), cancellation).ConfigureAwait(false);
                    archives.Add(asset.name,archive);
                }
                ApplyResources(workspace,index,archives,cancellation);
            }
            if (!File.Exists(Path.Combine(workspace, "source/Packages/org.openz.datachannel/Plugins/x86_64/datachannel.dll"))) throw new IOException("The native datachannel dependency is missing. Use the published resource bundle or a complete fork Unity project.");
            Directory.CreateDirectory(Path.GetDirectoryName(marker)); File.WriteAllText(marker, "Resources retained across source revisions.");
        }
        static bool Matches(string workspace, ResourceFile file)
        {
            string path=Format.SafePath(workspace,file.path);
            return File.Exists(path)&&new FileInfo(path).Length==file.length&&Format.FileHash(path)==file.sha256;
        }
        static void ValidateResources(string workspace,ResourceIndex index)
        {
            if(!Regex.IsMatch(index.version??"","^[a-zA-Z0-9._-]{1,80}$"))throw new InvalidDataException("Invalid resource package ID.");
            var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(var file in index.files)
            {
                Format.SafePath(workspace,file.path);
                if(!seen.Add(file.path)||!Format.IsHash(file.sha256)||file.length<0||!index.archives.Any(a=>a.name==file.archive)||
                    !(file.path.StartsWith("source/Assets/",StringComparison.Ordinal)||file.path=="source/Packages/org.openz.datachannel/Plugins/x86_64/datachannel.dll")||
                    file.path.StartsWith("source/Assets/OpenZ/Scripts/",StringComparison.Ordinal)||file.path.StartsWith("source/Assets/OpenZ/Shaders/",StringComparison.Ordinal))
                    throw new InvalidDataException("Invalid managed resource path.");
            }
        }
        static void BackupResource(string workspace,string relative)
        {
            string path=Format.SafePath(workspace,relative);if(!File.Exists(path))return;
            string backup=Format.SafePath(workspace,".local/resource-backups/"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff")+"/"+relative);
            Directory.CreateDirectory(Path.GetDirectoryName(backup));File.Copy(path,backup);
        }
        public static void ApplyResources(string workspace,ResourceIndex index,Dictionary<string,string> archives,CancellationToken cancellation)
        {
            ValidateResources(workspace,index);
            string statePath=Path.Combine(workspace,".local/resources-state.json");
            var previous=File.Exists(statePath)?Format.Json().Deserialize<ResourceState>(File.ReadAllText(statePath)):new ResourceState();
            var stage=Path.Combine(workspace,".local/resource-stage");Directory.CreateDirectory(stage);
            // Verify every changed file before changing the working resource set.
            foreach(var group in index.files.Where(f=>!Matches(workspace,f)).GroupBy(f=>f.archive))
            {
                using(var zip=ZipFile.OpenRead(archives[group.Key]))foreach(var file in group)
                {
                    cancellation.ThrowIfCancellationRequested();var entry=zip.GetEntry(file.path);
                    if(entry==null||entry.Length!=file.length)throw new InvalidDataException("Resource archive is incomplete.");
                    string path=Format.SafePath(stage,file.path);Directory.CreateDirectory(Path.GetDirectoryName(path));
                    using(var input=entry.Open())using(var output=File.Create(path))input.CopyTo(output);
                    if(Format.FileHash(path)!=file.sha256)throw new InvalidDataException("Resource file checksum mismatch.");
                }
            }
            cancellation.ThrowIfCancellationRequested();
            foreach(var file in index.files.Where(f=>!Matches(workspace,f)))
            {
                string path=Format.SafePath(workspace,file.path);var old=previous.files.FirstOrDefault(f=>f.path==file.path);
                if(File.Exists(path)&&(old==null||!Matches(workspace,old)))BackupResource(workspace,file.path);
                Directory.CreateDirectory(Path.GetDirectoryName(path));File.Copy(Format.SafePath(stage,file.path),path,true);
            }
            var nextPaths=new HashSet<string>(index.files.Select(f=>f.path),StringComparer.OrdinalIgnoreCase);
            foreach(var old in previous.files.Where(f=>!nextPaths.Contains(f.path)))
            {
                string path=Format.SafePath(workspace,old.path);if(!File.Exists(path))continue;
                if(!Matches(workspace,old))BackupResource(workspace,old.path);File.Delete(path);
            }
            Directory.CreateDirectory(Path.GetDirectoryName(statePath));File.WriteAllText(statePath,Format.Json().Serialize(new ResourceState{version=index.version,files=index.files}));
            Directory.Delete(stage,true);
        }
        public static string RequiredUnity(string workspace)
        {
            string path = Path.Combine(workspace, "source/ProjectSettings/ProjectVersion.txt");
            if (!File.Exists(path)) return "Retrieve sources first";
            var match = Regex.Match(File.ReadAllText(path), @"m_EditorVersion:\s*([^\r\n]+)"); return match.Success ? match.Groups[1].Value.Trim() : "Unknown Unity version";
        }
        public static string FindUnity(string version)
        {
            var paths = new List<string>(); string configured = Environment.GetEnvironmentVariable("UNITY_EDITOR_PATH"); if (!String.IsNullOrWhiteSpace(configured)) paths.Add(configured);
            paths.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Unity", "Hub", "Editor", version, "Editor", "Unity.exe"));
            foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed)) paths.Add(Path.Combine(drive.RootDirectory.FullName, "Unity", version, "Editor", "Unity.exe"));
            return paths.FirstOrDefault(File.Exists);
        }
        public async Task Build(string workspace, string unity, Action<string> log, CancellationToken cancellation)
        {
            ValidateWorkspace(workspace);
            if (!File.Exists(unity) || !File.Exists(Path.Combine(workspace, "FORK_SOURCE.json"))) throw new IOException("Choose the required Unity editor and retrieve a source version first.");
            if (!File.Exists(Path.Combine(workspace, ".local", "resources-ready.txt"))) throw new IOException("Prepare the Unity resources before building.");
            string resourcePointer=Path.Combine(workspace,"SOURCE_RESOURCES.json");
            if(File.Exists(resourcePointer))
            {
                string required=(string)Format.Json().Deserialize<Dictionary<string,object>>(File.ReadAllText(resourcePointer))["version"];
                string actual=Path.Combine(workspace,"source/Assets/OpenZ/Resources/OpenZ/resource_package.json");
                if(!File.Exists(actual)||(string)Format.Json().Deserialize<Dictionary<string,object>>(File.ReadAllText(actual))["version"]!=required)
                    throw new IOException("This source revision needs a different Unity resource package. Return to Resources and prepare the required update before building.");
            }
            string project = Path.Combine(workspace, "source"), output = Path.Combine(workspace, "builds", "OpenZ-VK-Local"), buildLog = Path.Combine(workspace, ".local", "Unity-build.log");
            if (File.Exists(Path.Combine(project, "Temp/UnityLockfile")))
            {
                try { using (var probe = File.Open(Path.Combine(project, "Temp/UnityLockfile"), FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { } }
                catch (IOException) { throw new IOException("Close the Unity editor for this project before building."); }
            }
            string method = "OpenZ.Multiplayer.Editor.DuelBuild.Windows";
            if (!File.Exists(Path.Combine(project, "Assets/OpenZ/Scripts/Multiplayer/Unity/Editor/DuelBuild.cs")))
            {
                string bridge = Path.Combine(project, "Assets/Editor/OpenZVKInstallerBuild.cs"); Directory.CreateDirectory(Path.GetDirectoryName(bridge));
                using (var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("OpenZVK.LegacyBuild")) using (var input = new StreamReader(resource)) File.WriteAllText(bridge, input.ReadToEnd(), new UTF8Encoding(false));
                method = "OpenZVKInstallerBuild.Windows";
            }
            Directory.CreateDirectory(output);
            log("Building a release player with Unity " + RequiredUnity(workspace) + ". Progress: " + buildLog);
            await Run(unity, workspace, new[] { "-batchmode", "-nographics", "-quit", "-projectPath", project, "-executeMethod", method, "-oz-duel-output", output, "-logFile", buildLog }, log, cancellation).ConfigureAwait(false);
            string result = Path.Combine(output, "BUILD_RESULT.txt");
            if (!File.Exists(result) || !File.ReadAllText(result).StartsWith("Succeeded") || !File.Exists(Path.Combine(output, "openz-vk.exe"))) throw new IOException("Unity did not report a successful build. Read " + buildLog);
            if (!File.Exists(Path.Combine(output, "FORK_BUILD.json")))
            {
                var source = Format.Json().Deserialize<SourceIdentity>(File.ReadAllText(Path.Combine(workspace, "FORK_SOURCE.json")));
                File.WriteAllText(Path.Combine(output, "FORK_BUILD.json"), Format.Json().Serialize(new { version = "source-" + source.commit.Substring(0, 10), commit = source.commit, dirty = true, builtUtc = DateTime.UtcNow.ToString("o") }));
            }
            log("SUCCESS: " + Path.Combine(output, "openz-vk.exe") + ". The game was not launched.");
        }
    }
}
