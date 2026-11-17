using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace OpenZVK.Update
{
    public sealed class Chunk { public string hash, asset; public long offset; public int length, packedLength; }
    public sealed class PlayerFile { public string path, sha256; public long length; public List<Chunk> chunks = new List<Chunk>(); }
    public sealed class Pack { public string name, sha256; public long length; }
    public sealed class Manifest
    {
        public int schema = 1;
        public string version, channel, sourceBranch, commit, releaseTag, createdUtc, notes;
        public string entrypoint = "openz-vk.exe";
        public List<PlayerFile> files = new List<PlayerFile>();
        public List<Pack> packs = new List<Pack>();
    }
    public sealed class ReleaseAsset { public string name, browser_download_url; public long size; }
    public sealed class Release
    {
        public string tag_name, name, body, published_at;
        public bool draft, prerelease;
        public List<ReleaseAsset> assets = new List<ReleaseAsset>();
        public override string ToString() { return name ?? tag_name; }
    }
    public sealed class Progress
    {
        public string message; public long done, total, downloaded, reused;
    }
    public static class Format
    {
        public const string Repository = "Tablaski/OpenZ";
        public const string ManifestName = "OpenZ-VK-update.json";
        public const string InstalledName = ".openz-vk-install.json";
        public static JavaScriptSerializer Json() { return new JavaScriptSerializer { MaxJsonLength = 32 * 1024 * 1024, RecursionLimit = 100 }; }
        public static Manifest Read(string json) { var m = Json().Deserialize<Manifest>(json); Validate(m); return m; }
        public static string Hash(byte[] data) { using (var h = SHA256.Create()) return Hex(h.ComputeHash(data)); }
        public static string FileHash(string path) { using (var stream = File.OpenRead(path)) using (var h = SHA256.Create()) return Hex(h.ComputeHash(stream)); }
        static string Hex(byte[] hash) { return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant(); }
        public static bool IsHash(string text) { return text != null && text.Length == 64 && text.All(c => c >= '0' && c <= '9' || c >= 'a' && c <= 'f'); }
        public static string SafePath(string root, string relative)
        {
            if (String.IsNullOrWhiteSpace(relative) || relative.Length > 220 || relative.Contains("\\") || relative.Contains(":") || relative.StartsWith("/") || relative.Any(c => c < 32)) throw new InvalidDataException("Invalid update path.");
            foreach (string part in relative.Split('/'))
            {
                string stem = part.Split('.')[0].ToUpperInvariant();
                if (part == "." || part == ".." || part.Length == 0 || part.EndsWith(".") || part.EndsWith(" ") || part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                    stem == "CON" || stem == "PRN" || stem == "AUX" || stem == "NUL" || System.Text.RegularExpressions.Regex.IsMatch(stem, "^(COM|LPT)[1-9]$"))
                    throw new InvalidDataException("Unsafe update filename.");
            }
            string prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string target = Path.GetFullPath(Path.Combine(prefix, relative.Replace('/', Path.DirectorySeparatorChar)));
            if (!target.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Update escapes the game directory.");
            string current = Path.GetPathRoot(target);
            foreach (string part in target.Substring(current.Length).Split(Path.DirectorySeparatorChar))
            {
                current = Path.Combine(current, part);
                if ((Directory.Exists(current) || File.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Update paths cannot contain directory links: " + current);
            }
            return target;
        }
        public static void Validate(Manifest m)
        {
            if (m == null || m.schema != 1 || m.files == null || m.packs == null || m.files.Count == 0 || m.files.Count > 100000 ||
                (m.channel != "stable" && m.channel != "dev") || m.sourceBranch != (m.channel == "stable" ? "main" : "dev") ||
                String.IsNullOrWhiteSpace(m.version) || String.IsNullOrWhiteSpace(m.releaseTag) || m.commit == null || !System.Text.RegularExpressions.Regex.IsMatch(m.commit, "^[a-f0-9]{40}$") || m.entrypoint != "openz-vk.exe")
                throw new InvalidDataException("Unsupported player manifest.");
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var packs = new Dictionary<string, Pack>(StringComparer.Ordinal);
            foreach (var pack in m.packs)
            {
                if (String.IsNullOrEmpty(pack.name) || !System.Text.RegularExpressions.Regex.IsMatch(pack.name, "^OpenZ-VK-payload-[0-9]{3}\\.pack$") || pack.length <= 0 || pack.length >= 2000000000 || !IsHash(pack.sha256) || packs.ContainsKey(pack.name)) throw new InvalidDataException("Invalid payload asset.");
                packs.Add(pack.name, pack);
            }
            long total = 0;
            foreach (var file in m.files)
            {
                SafePath(Path.Combine(Path.GetPathRoot(Environment.CurrentDirectory), "OpenZ-VK-path-check"), file.path);
                if (!names.Add(file.path) || file.path.Equals(InstalledName, StringComparison.OrdinalIgnoreCase) || file.length < 0 || !IsHash(file.sha256) || file.chunks == null) throw new InvalidDataException("Invalid player file.");
                long sum = 0;
                foreach (var chunk in file.chunks)
                {
                    Pack pack;
                    if (chunk == null || !IsHash(chunk.hash) || chunk.length <= 0 || chunk.length > Blocks.Maximum || chunk.packedLength <= 0 || chunk.packedLength > Blocks.Maximum + 65536 || !packs.TryGetValue(chunk.asset ?? "", out pack) || chunk.offset < 0 || chunk.offset > pack.length - chunk.packedLength) throw new InvalidDataException("Invalid resource block.");
                    sum += chunk.length;
                }
                total += file.length;
                if (sum != file.length || total > 32L * 1024 * 1024 * 1024) throw new InvalidDataException("Invalid installed size.");
            }
            if (!names.Contains(m.entrypoint) || !names.Contains("FORK_BUILD.json")) throw new InvalidDataException("Incomplete player manifest.");
            foreach (var name in names)
                if (names.Contains(name + "/") || names.Any(other => other.StartsWith(name + "/", StringComparison.OrdinalIgnoreCase))) throw new InvalidDataException("Conflicting player paths.");
        }
        public static string Size(long bytes) { return bytes >= 1024 * 1024 * 1024 ? (bytes / (1024.0 * 1024 * 1024)).ToString("0.00") + " GB" : (bytes / (1024.0 * 1024)).ToString("0.0") + " MB"; }
    }
    public static class Blocks
    {
        public const int Minimum = 1024 * 1024, Maximum = 8 * 1024 * 1024;
        static readonly ulong[] Gear = MakeGear();
        static ulong[] MakeGear()
        {
            var result = new ulong[256]; ulong seed = 0x4f70656e5a564b01UL;
            unchecked { for (int i = 0; i < result.Length; i++) { seed += 0x9e3779b97f4a7c15UL; ulong z = seed; z = (z ^ (z >> 30)) * 0xbf58476d1ce4e5b9UL; z = (z ^ (z >> 27)) * 0x94d049bb133111ebUL; result[i] = z ^ (z >> 31); } }
            return result;
        }
        // Content-defined boundaries recover unchanged resources after insertions shift file offsets.
        public static void Scan(Stream input, Action<byte[]> consume, CancellationToken cancellation)
        {
            byte[] buffer = new byte[65536], block = new byte[Maximum]; int count = 0, read; ulong state = 0;
            while ((read = input.Read(buffer, 0, buffer.Length)) != 0)
            {
                cancellation.ThrowIfCancellationRequested();
                for (int i = 0; i < read; i++)
                {
                    byte value = buffer[i]; block[count++] = value;
                    unchecked { state = (state << 1) + Gear[value]; }
                    if (count >= Minimum && ((state & 0x3fffffUL) == 0 || count == Maximum))
                    { var data = new byte[count]; Buffer.BlockCopy(block, 0, data, 0, count); consume(data); count = 0; state = 0; }
                }
            }
            if (count > 0) { var data = new byte[count]; Buffer.BlockCopy(block, 0, data, 0, count); consume(data); }
        }
        public static byte[] Inflate(byte[] packed, Chunk chunk)
        {
            var result = new byte[chunk.length];
            using (var stream = new GZipStream(new MemoryStream(packed, false), CompressionMode.Decompress))
            {
                int offset = 0, count;
                while (offset < result.Length && (count = stream.Read(result, offset, result.Length - offset)) > 0) offset += count;
                if (offset != result.Length || stream.ReadByte() != -1) throw new InvalidDataException("Unexpected expanded resource size.");
            }
            if (Format.Hash(result) != chunk.hash) throw new InvalidDataException("Resource block checksum mismatch.");
            return result;
        }
    }
    public sealed class GitHub : IDisposable
    {
        readonly HttpClient http;
        public GitHub() : this(new HttpClientHandler { AllowAutoRedirect = true }) { }
        public GitHub(HttpMessageHandler handler)
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            http = new HttpClient(handler); http.Timeout = TimeSpan.FromMinutes(10);
            http.DefaultRequestHeaders.UserAgent.ParseAdd("OpenZ-VK-Installer/1.0");
        }
        async Task<string> Text(string url, int maximum, CancellationToken cancellation)
        {
            using (var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellation).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                using (var input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false)) using (var output = new MemoryStream())
                {
                    var buffer = new byte[65536]; int count;
                    while ((count = await input.ReadAsync(buffer, 0, buffer.Length, cancellation).ConfigureAwait(false)) > 0)
                    { if (output.Length + count > maximum) throw new InvalidDataException("Update metadata is too large."); output.Write(buffer, 0, count); }
                    return Encoding.UTF8.GetString(output.ToArray());
                }
            }
        }
        public async Task<List<Release>> Releases(string channel, CancellationToken cancellation)
        {
            var all = Format.Json().Deserialize<List<Release>>(await Text("https://api.github.com/repos/" + Format.Repository + "/releases?per_page=100", 8 * 1024 * 1024, cancellation).ConfigureAwait(false));
            return all.Where(r => !r.draft && r.prerelease == (channel == "dev") && r.assets != null && r.assets.Any(a => a.name == Format.ManifestName)).ToList();
        }
        public async Task<Manifest> Manifest(Release release, string channel, CancellationToken cancellation)
        {
            var asset = release.assets.Single(a => a.name == Format.ManifestName);
            string url = AssetUrl(release.tag_name, asset.name);
            var manifest = Format.Read(await Text(url, 32 * 1024 * 1024, cancellation).ConfigureAwait(false));
            if (manifest.channel != channel || manifest.releaseTag != release.tag_name) throw new InvalidDataException("Channel or release identity mismatch.");
            foreach (var pack in manifest.packs)
                if (!release.assets.Any(a => a.name == pack.name && a.size == pack.length)) throw new InvalidDataException("Missing update payload: " + pack.name);
            return manifest;
        }
        public static string AssetUrl(string tag, string name) { return "https://github.com/" + Format.Repository + "/releases/download/" + Uri.EscapeDataString(tag) + "/" + Uri.EscapeDataString(name); }
        public async Task<byte[]> Download(Manifest manifest, Chunk chunk, CancellationToken cancellation)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get, AssetUrl(manifest.releaseTag, chunk.asset)))
            {
                request.Headers.Range = new RangeHeaderValue(chunk.offset, chunk.offset + chunk.packedLength - 1);
                using (var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellation).ConfigureAwait(false))
                {
                    response.EnsureSuccessStatusCode();
                    var range = response.Content.Headers.ContentRange;
                    if (response.StatusCode != HttpStatusCode.PartialContent || range == null || range.From != chunk.offset || range.To != chunk.offset + chunk.packedLength - 1)
                        throw new IOException("The download server did not honor a partial download. Retry later; the complete ZIPs remain available.");
                    if (response.Content.Headers.ContentLength.HasValue && response.Content.Headers.ContentLength != chunk.packedLength) throw new InvalidDataException("Unexpected resource download length.");
                    var bytes = new byte[chunk.packedLength]; int offset = 0, count;
                    using (var input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                    {
                        while (offset < bytes.Length && (count = await input.ReadAsync(bytes, offset, bytes.Length - offset, cancellation).ConfigureAwait(false)) > 0) offset += count;
                        if (offset != bytes.Length || input.ReadByte() != -1) throw new InvalidDataException("Truncated or oversized resource download.");
                    }
                    return Blocks.Inflate(bytes, chunk);
                }
            }
        }
        public void Dispose() { http.Dispose(); }
    }
    public sealed class Installer
    {
        readonly Func<Manifest, Chunk, CancellationToken, Task<byte[]>> fetch;
        public Installer(Func<Manifest, Chunk, CancellationToken, Task<byte[]>> fetch) { this.fetch = fetch; }
        public async Task Install(Manifest manifest, string target, string seed, IProgress<Progress> report, CancellationToken cancellation)
        {
            Format.Validate(manifest);
            target = Path.GetFullPath(target).TrimEnd(Path.DirectorySeparatorChar);
            if (target.Length < 8 || target == Path.GetPathRoot(target)) throw new IOException("Choose a dedicated game folder.");
            Format.SafePath(Path.GetDirectoryName(target), Path.GetFileName(target));
            string marker = Path.Combine(target, Format.InstalledName);
            if (Directory.Exists(target) && Directory.EnumerateFileSystemEntries(target).Any() && !File.Exists(marker)) throw new IOException("This folder is not managed by the installer. Choose an empty destination and select this existing build under REUSE FILES.");
            if (File.Exists(marker)) Format.Read(File.ReadAllText(marker));
            foreach (var process in Process.GetProcessesByName("openz-vk"))
            {
                using (process)
                {
                    try { if (process.MainModule.FileName.StartsWith(target + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new IOException("Close OpenZ-VK and the Unit Editor before updating."); }
                    catch (System.ComponentModel.Win32Exception) { throw new IOException("Close all OpenZ-VK processes before updating."); }
                }
            }
            string parent = Path.GetDirectoryName(target), name = Path.GetFileName(target);
            string work = Path.Combine(parent, "." + name + "-update"), stage = Path.Combine(work, "staging"), cache = Path.Combine(work, "blocks"), backup = Path.Combine(parent, "." + name + "-previous");
            Format.SafePath(parent, Path.GetFileName(work)); Format.SafePath(parent, Path.GetFileName(backup));
            Directory.CreateDirectory(parent);
            using (var updateLock = new FileStream(Path.Combine(parent, "." + name + "-update.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            {
                string owner = Path.Combine(work, "OWNER.txt");
                if (Directory.Exists(work) && (!File.Exists(owner) || File.ReadAllText(owner) != target)) throw new IOException("An unrelated update work directory already exists.");
                Directory.CreateDirectory(work); File.WriteAllText(owner, target);
                if (Directory.Exists(backup) && !File.Exists(Path.Combine(backup, Format.InstalledName))) throw new IOException("An unrelated backup directory already exists.");
                // Recover a process interruption between the two directory renames.
                if (!Directory.Exists(target) && Directory.Exists(backup)) Directory.Move(backup, target);
                if (Directory.Exists(backup)) SafeDelete(backup, parent);
                if (Directory.Exists(stage)) SafeDelete(stage, work);
                Directory.CreateDirectory(stage); Directory.CreateDirectory(cache);
                long total = manifest.files.Sum(f => f.length), done = 0, reused = 0, downloaded = 0;
                long available = new DriveInfo(Path.GetPathRoot(target)).AvailableFreeSpace;
                if (available < total * 2 + Blocks.Maximum * 3L) throw new IOException("Not enough free space to cache, stage and verify the complete game.");
                var wanted = manifest.files.SelectMany(f => f.chunks).GroupBy(c => c.hash).ToDictionary(g => g.Key, g => g.First());
                var reusedHashes = new HashSet<string>();
                foreach (string source in new[] { target, seed }.Where(s => !String.IsNullOrWhiteSpace(s) && Directory.Exists(s)).Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    foreach (var file in manifest.files)
                    {
                        cancellation.ThrowIfCancellationRequested();
                        string oldFile = Format.SafePath(source, file.path);
                        if (!File.Exists(oldFile)) continue;
                        report.Report(new Progress { message = "Checking reusable files: " + file.path, done = done, total = total, reused = reused, downloaded = downloaded });
                        using (var input = File.OpenRead(oldFile)) Blocks.Scan(input, data =>
                        {
                            string hash = Format.Hash(data); Chunk chunk;
                            if (wanted.TryGetValue(hash, out chunk) && chunk.length == data.Length)
                            {
                                string cached = Path.Combine(cache, hash);
                                if (!File.Exists(cached)) File.WriteAllBytes(cached, data);
                                reusedHashes.Add(hash);
                            }
                        }, cancellation);
                    }
                }
                foreach (var file in manifest.files)
                {
                    cancellation.ThrowIfCancellationRequested();
                    string output = Format.SafePath(stage, file.path); Directory.CreateDirectory(Path.GetDirectoryName(output));
                    using (var stream = File.Create(output))
                    {
                        foreach (var chunk in file.chunks)
                        {
                            cancellation.ThrowIfCancellationRequested();
                            string cached = Path.Combine(cache, chunk.hash); byte[] data = null;
                            if (File.Exists(cached))
                            {
                                var info = new FileInfo(cached);
                                if (info.Length == chunk.length) { var bytes = File.ReadAllBytes(cached); if (Format.Hash(bytes) == chunk.hash) data = bytes; }
                            }
                            if (data == null)
                            {
                                Exception failure = null;
                                for (int attempt = 0; attempt < 3 && data == null; attempt++)
                                {
                                    try { data = await fetch(manifest, chunk, cancellation).ConfigureAwait(false); if (data.Length != chunk.length || Format.Hash(data) != chunk.hash) throw new InvalidDataException("Downloaded block verification failed."); }
                                    catch (OperationCanceledException) { throw; }
                                    catch (Exception error) { failure = error; data = null; }
                                    if (data == null && attempt < 2) await Task.Delay(500 * (attempt + 1), cancellation).ConfigureAwait(false);
                                }
                                if (data == null) throw new IOException("Could not download a verified resource block.", failure);
                                File.WriteAllBytes(cached, data); downloaded += chunk.packedLength;
                            }
                            else if (reusedHashes.Contains(chunk.hash)) reused += chunk.length;
                            stream.Write(data, 0, data.Length); done += data.Length;
                            report.Report(new Progress { message = "Installing " + file.path, done = done, total = total, downloaded = downloaded, reused = reused });
                        }
                    }
                    if (Format.FileHash(output) != file.sha256) throw new InvalidDataException("Installed file checksum mismatch: " + file.path);
                }
                File.WriteAllText(Path.Combine(stage, Format.InstalledName), Format.Json().Serialize(manifest), new UTF8Encoding(false));
                cancellation.ThrowIfCancellationRequested();
                bool movedOld = false;
                try
                {
                    if (Directory.Exists(target)) { Directory.Move(target, backup); movedOld = true; }
                    Directory.Move(stage, target);
                }
                catch { if (!Directory.Exists(target) && movedOld) Directory.Move(backup, target); throw; }
                if (Directory.Exists(backup)) SafeDelete(backup, parent);
                SafeDelete(work, parent);
                report.Report(new Progress { message = "Installed OpenZ-VK " + manifest.version + ". Downloaded " + Format.Size(downloaded) + "; reused " + Format.Size(reused) + ".", done = total, total = total, downloaded = downloaded, reused = reused });
            }
        }
        static void SafeDelete(string path, string parent)
        {
            string resolved = Path.GetFullPath(path), prefix = Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!resolved.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || resolved == prefix.TrimEnd(Path.DirectorySeparatorChar)) throw new IOException("Invalid temporary directory.");
            foreach (var entry in Directory.EnumerateFileSystemEntries(resolved))
                if ((File.GetAttributes(entry) & FileAttributes.ReparsePoint) != 0) throw new IOException("Cannot clean an update directory containing links.");
                else if (Directory.Exists(entry)) SafeDelete(entry, resolved); else File.Delete(entry);
            Directory.Delete(resolved);
        }
    }
}
