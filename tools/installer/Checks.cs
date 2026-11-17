using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace OpenZVK.Update
{
    public static class Checks
    {
        static int count;
        static void Check(bool value, string message) { if (!value) throw new Exception(message); count++; }
        static void Reject(Action action, string message) { try { action(); } catch (InvalidDataException) { count++; return; } throw new Exception(message); }
        sealed class Observer : IProgress<Progress> { public Progress Last; public void Report(Progress value) { Last = value; } }
        sealed class Ranges : HttpMessageHandler
        {
            public string Folder; public bool Ignore;
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellation)
            {
                string path = Path.Combine(Folder, Uri.UnescapeDataString(request.RequestUri.Segments.Last()));
                var item = request.Headers.Range.Ranges.Single(); long start = item.From.Value, end = item.To.Value;
                byte[] bytes = new byte[(int)(end - start + 1)];
                using (var input = File.OpenRead(path)) { input.Position = start; if (input.Read(bytes, 0, bytes.Length) != bytes.Length) throw new IOException("Fixture is truncated."); }
                var response = new HttpResponseMessage(Ignore ? HttpStatusCode.OK : HttpStatusCode.PartialContent) { Content = new ByteArrayContent(bytes) };
                response.Content.Headers.ContentRange = new ContentRangeHeaderValue(start, end, new FileInfo(path).Length);
                return Task.FromResult(response);
            }
        }
        public static void Run() { RunAsync().GetAwaiter().GetResult(); }
        static async Task RunAsync()
        {
            string root = Path.Combine(Environment.CurrentDirectory, ".local", "installer-checks-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            try
            {
                string player = Path.Combine(root, "player"), firstPacks = Path.Combine(root, "v1"), secondPacks = Path.Combine(root, "v2"), game = Path.Combine(root, "Game");
                Directory.CreateDirectory(player); Directory.CreateDirectory(Path.Combine(player, "openz-vk_Data"));
                byte[] resource = new byte[25 * 1024 * 1024]; new Random(73).NextBytes(resource);
                File.WriteAllBytes(Path.Combine(player, "openz-vk_Data", "resources.bin"), resource);
                File.WriteAllText(Path.Combine(player, "openz-vk.exe"), "Fixture executable; never launched.");
                File.WriteAllBytes(Path.Combine(player, "empty.txt"), new byte[0]);
                File.WriteAllText(Path.Combine(player, "FORK_BUILD.json"), "{\"version\":\"1.0.0-test\",\"commit\":\"" + new string('a', 40) + "\",\"dirty\":false}");
                var v1 = Packager.Build(player, firstPacks, "stable", "fixture-v1", "First test build");
                Check(v1.files.Count == 4 && v1.files.Sum(f => f.length) > resource.Length, "Complete player manifest missing files.");
                var observer = new Observer(); int requests = 0;
                Func<string, Manifest, Chunk, byte[]> local = (folder, manifest, chunk) =>
                {
                    var bytes = new byte[chunk.packedLength]; using (var input = File.OpenRead(Path.Combine(folder, chunk.asset))) { input.Position = chunk.offset; if (input.Read(bytes, 0, bytes.Length) != bytes.Length) throw new IOException(); }
                    return Blocks.Inflate(bytes, chunk);
                };
                var install = new Installer((m, c, token) => { requests++; return Task.FromResult(local(firstPacks, m, c)); });
                await install.Install(v1, game, null, observer, CancellationToken.None);
                Check(requests > 0 && observer.Last.downloaded > 0, "Fresh install did not download.");
                foreach (var file in v1.files) Check(Format.FileHash(Format.SafePath(game, file.path)) == file.sha256, "Installed bytes differ.");
                Check(!Directory.Exists(Path.Combine(root, ".Game-update")), "Completed install left a duplicate block cache.");
                requests = 0;
                await install.Install(v1, game, null, observer, CancellationToken.None);
                Check(requests == 0 && observer.Last.downloaded == 0, "Unchanged build downloaded assets again.");
                // Shift the entire resource by inserting bytes. Content-defined chunks
                // must recover the unchanged tail, unlike offset-based replacement.
                byte[] shifted = new byte[resource.Length + 12345]; Buffer.BlockCopy(resource, 0, shifted, 12345, resource.Length);
                File.WriteAllBytes(Path.Combine(player, "openz-vk_Data", "resources.bin"), shifted);
                File.WriteAllText(Path.Combine(player, "FORK_BUILD.json"), "{\"version\":\"1.0.1-test\",\"commit\":\"" + new string('b', 40) + "\",\"dirty\":false}");
                var v2 = Packager.Build(player, secondPacks, "dev", "fixture-v2", "Shifted resource test");
                var v2Chunks = v2.files.SelectMany(f => f.chunks).Select(c => c.hash).ToList();
                long shared = v1.files.SelectMany(f => f.chunks).Where(c => v2Chunks.Contains(c.hash)).Sum(c => (long)c.length);
                Check(shared > resource.Length / 2, "Resource insertion defeated block reuse.");
                var broken = new Installer((m, c, token) => Task.FromResult(new byte[c.length]));
                bool failed = false;
                try { await broken.Install(v2, game, null, observer, CancellationToken.None); } catch (IOException) { failed = true; }
                Check(failed && Format.Read(File.ReadAllText(Path.Combine(game, Format.InstalledName))).version == v1.version, "Failed update replaced the working installation.");
                requests = 0;
                var second = new Installer((m, c, token) => { requests++; return Task.FromResult(local(secondPacks, m, c)); });
                await second.Install(v2, game, null, observer, CancellationToken.None);
                Check(requests > 0 && observer.Last.reused > shared / 2 && observer.Last.downloaded < resource.Length / 2, "Update did not reuse unchanged resources.");
                Check(File.ReadAllBytes(Path.Combine(game, "openz-vk_Data", "resources.bin")).SequenceEqual(shifted), "Updated file was corrupted.");
                Check(Format.Read(File.ReadAllText(Path.Combine(game, Format.InstalledName))).channel == "dev", "Channel switch was not recorded.");
                await install.Install(v1, game, null, observer, CancellationToken.None);
                Check(Format.Read(File.ReadAllText(Path.Combine(game, Format.InstalledName))).version == v1.version, "Selected older version did not install.");
                string seeded = Path.Combine(root, "Seeded"); requests = 0;
                await install.Install(v1, seeded, game, observer, CancellationToken.None);
                Check(requests == 0, "First install did not reuse an extracted game folder.");
                var cancel = new CancellationTokenSource(); cancel.Cancel(); bool cancelled = false;
                try { await second.Install(v2, game, null, observer, cancel.Token); } catch (OperationCanceledException) { cancelled = true; }
                Check(cancelled && Format.Read(File.ReadAllText(Path.Combine(game, Format.InstalledName))).version == v1.version, "Cancellation damaged the installed version.");
                string original = v1.files[0].path;
                foreach (string invalid in new[] { "../outside", "C:/outside", "/outside", "sub/../outside", "x:stream", "CON.txt", "dir./x", "x\\y" })
                { v1.files[0].path = invalid; Reject(() => Format.Validate(v1), "Unsafe path accepted: " + invalid); }
                v1.files[0].path = original;
                string hash = v1.files[0].sha256; v1.files[0].sha256 = "bad"; Reject(() => Format.Validate(v1), "Bad checksum accepted."); v1.files[0].sha256 = hash;
                v1.sourceBranch = "dev"; Reject(() => Format.Validate(v1), "Stable manifest accepted a dev source branch."); v1.sourceBranch = "main";
                var duplicate = v1.files[0]; v1.files.Add(duplicate); Reject(() => Format.Validate(v1), "Duplicate paths accepted."); v1.files.RemoveAt(v1.files.Count - 1);
                string unmanaged = Path.Combine(root, "Unmanaged"); Directory.CreateDirectory(unmanaged); File.WriteAllText(Path.Combine(unmanaged, "personal.txt"), "keep");
                bool refused = false; try { await install.Install(v1, unmanaged, null, observer, CancellationToken.None); } catch (IOException) { refused = true; }
                Check(refused && File.Exists(Path.Combine(unmanaged, "personal.txt")), "Unmanaged folder was changed.");
                var sample = v1.files.SelectMany(f => f.chunks).First();
                using (var network = new GitHub(new Ranges { Folder = firstPacks }))
                    Check((await network.Download(v1, sample, CancellationToken.None)).Length == sample.length, "HTTP byte-range reconstruction failed.");
                bool ignored = false;
                using (var network = new GitHub(new Ranges { Folder = firstPacks, Ignore = true }))
                    try { await network.Download(v1, sample, CancellationToken.None); } catch (IOException) { ignored = true; }
                Check(ignored, "Server ignoring partial downloads was accepted.");
                string sourceRoot = Path.Combine(root, "Source"), snapshot = Path.Combine(root, "snapshot.zip"); Directory.CreateDirectory(sourceRoot);
                string old = "source/Assets/OpenZ/Scripts/Old.cs", current = "source/Assets/OpenZ/Scripts/Current.cs";
                Directory.CreateDirectory(Path.GetDirectoryName(Format.SafePath(sourceRoot, old))); File.WriteAllText(Format.SafePath(sourceRoot, old), "old tracked source");
                string asset = Format.SafePath(sourceRoot, "source/Assets/personal-local-resource.txt"); File.WriteAllText(asset, "retain ignored local assets");
                var before = new SourceIdentity(); before.files.Add(new SourceFile { path = old, sha256 = Format.FileHash(Format.SafePath(sourceRoot, old)) });
                using (var zip = ZipFile.Open(snapshot, ZipArchiveMode.Create))
                {
                    using (var writer = new StreamWriter(zip.CreateEntry("OpenZ-fixture/" + current).Open())) writer.Write("new tracked source");
                    using (var writer = new StreamWriter(zip.CreateEntry("OpenZ-fixture/source/ProjectSettings/ProjectVersion.txt").Open())) writer.Write("m_EditorVersion: 6000.3.27f2\n");
                }
                var after = new SourceIdentity(); SourceManager.ApplyArchive(sourceRoot, snapshot, before, after, CancellationToken.None);
                Check(!File.Exists(Format.SafePath(sourceRoot, old)) && File.ReadAllText(Format.SafePath(sourceRoot, current)) == "new tracked source", "Source snapshot did not remove obsolete tracked files or apply new ones.");
                Check(File.ReadAllText(asset) == "retain ignored local assets", "Source update erased retained resources.");
                Check(SourceManager.RequiredUnity(sourceRoot) == "6000.3.27f2", "Required Unity version detection failed.");
                Check(after.files.All(f => Format.FileHash(Format.SafePath(sourceRoot, f.path)) == f.sha256), "Git-free source provenance differs from extracted files.");
                using (var zip = ZipFile.Open(Path.Combine(root, "bad-source.zip"), ZipArchiveMode.Create)) using (var writer = new StreamWriter(zip.CreateEntry("OpenZ-fixture/../../outside.txt").Open())) writer.Write("invalid");
                Reject(() => SourceManager.ReadArchive(Path.Combine(root, "bad-source.zip"), true), "Unsafe source archive path accepted.");
                Check(SourceManager.FindGit() == null || File.Exists(SourceManager.FindGit()), "Git detection returned an invalid executable.");
                string resourceWorkspace=Path.Combine(root,"ResourceEpochs");Directory.CreateDirectory(resourceWorkspace);
                var resourceV1=ResourceFixture(root,"epoch1",new Dictionary<string,string>{{"changed.txt","first"},{"obsolete.txt","old"},{"user-edited.txt","original"}});
                SourceManager.ApplyResources(resourceWorkspace,resourceV1,new Dictionary<string,string>{{"epoch1.zip",Path.Combine(root,"epoch1.zip")}},CancellationToken.None);
                string prefix="source/Assets/";
                File.WriteAllText(Format.SafePath(resourceWorkspace,prefix+"user-edited.txt"),"personal change");
                File.WriteAllText(Format.SafePath(resourceWorkspace,prefix+"unknown.txt"),"keep unknown");
                var resourceV2=ResourceFixture(root,"epoch2",new Dictionary<string,string>{{"changed.txt","other"},{"added.txt","arctic"},{"user-edited.txt","replacement"}});
                SourceManager.ApplyResources(resourceWorkspace,resourceV2,new Dictionary<string,string>{{"epoch2.zip",Path.Combine(root,"epoch2.zip")}},CancellationToken.None);
                Check(File.ReadAllText(Format.SafePath(resourceWorkspace,prefix+"changed.txt"))=="other","Equal-size resource update was skipped.");
                Check(File.Exists(Format.SafePath(resourceWorkspace,prefix+"added.txt"))&&!File.Exists(Format.SafePath(resourceWorkspace,prefix+"obsolete.txt")),"Resource epoch did not add/remove managed assets.");
                Check(File.ReadAllText(Format.SafePath(resourceWorkspace,prefix+"unknown.txt"))=="keep unknown","Unknown local resource was deleted.");
                Check(Directory.GetFiles(Path.Combine(resourceWorkspace,".local/resource-backups"),"user-edited.txt",SearchOption.AllDirectories).Any(f=>File.ReadAllText(f)=="personal change"),"Modified resource was not backed up.");
                SourceManager.ApplyResources(resourceWorkspace,resourceV2,new Dictionary<string,string>(),CancellationToken.None);
                Check(Format.Json().Deserialize<ResourceState>(File.ReadAllText(Path.Combine(resourceWorkspace,".local/resources-state.json"))).version=="epoch2","Unchanged resource epoch needed an archive or lost identity.");
                Console.WriteLine("PASS: " + count + " installer checks: install, byte-range transport, SHA-256, shifted-resource reuse, zero-download reinstall, extracted-build seed, stable/dev switch, downgrade, failed/cancelled-update rollback, unsafe paths and unmanaged-folder protection.");
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }
        static ResourceIndex ResourceFixture(string root,string version,Dictionary<string,string> entries)
        {
            var index=new ResourceIndex{schema=2,version=version,archives=new List<ResourceArchive>{new ResourceArchive{name=version+".zip"}},files=new List<ResourceFile>()};
            using(var zip=ZipFile.Open(Path.Combine(root,version+".zip"),ZipArchiveMode.Create))foreach(var item in entries)
            {
                string path="source/Assets/"+item.Key;
                using(var writer=new StreamWriter(zip.CreateEntry(path).Open(),new System.Text.UTF8Encoding(false)))writer.Write(item.Value);
                string hashFile=Path.Combine(root,"hash-fixture");File.WriteAllText(hashFile,item.Value,new System.Text.UTF8Encoding(false));
                index.files.Add(new ResourceFile{path=path,length=new FileInfo(hashFile).Length,sha256=Format.FileHash(hashFile),archive=version+".zip"});
            }
            return index;
        }
    }
}
