using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;

namespace OpenZVK.Update
{
    public static class Packager
    {
        public static Manifest Build(string player, string output, string channel, string tag, string notes)
        {
            player = Path.GetFullPath(player); output = Path.GetFullPath(output);
            if (output.StartsWith(player + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Payloads must be outside the player directory.");
            var identity = Format.Json().Deserialize<Dictionary<string, object>>(File.ReadAllText(Path.Combine(player, "FORK_BUILD.json")));
            if (Convert.ToBoolean(identity["dirty"])) throw new InvalidDataException("Commit source changes before publishing an update.");
            Directory.CreateDirectory(output);
            if (Directory.EnumerateFiles(output, "*.pack").Any()) throw new IOException("Choose an empty payload output directory.");
            var result = new Manifest { version = (string)identity["version"], commit = (string)identity["commit"], channel = channel,
                sourceBranch = channel == "stable" ? "main" : "dev", releaseTag = tag, createdUtc = DateTime.UtcNow.ToString("o"), notes = notes };
            var unique = new Dictionary<string, Chunk>(); FileStream packStream = null; Pack pack = null; int number = 0;
            try
            {
                foreach (string path in Directory.GetFiles(player, "*", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.Ordinal))
                {
                    string relative = path.Substring(player.Length + 1).Replace('\\', '/');
                    if (relative.Split('/').Any(s => s.EndsWith("_BurstDebugInformation_DoNotShip") || s.EndsWith("_BackUpThisFolder_ButDontShipItWithYourGame")) || relative == Format.InstalledName) continue;
                    Format.SafePath(player, relative);
                    var file = new PlayerFile { path = relative, length = new FileInfo(path).Length, sha256 = Format.FileHash(path) };
                    using (var input = File.OpenRead(path)) Blocks.Scan(input, data =>
                    {
                        string hash = Format.Hash(data); Chunk chunk;
                        if (!unique.TryGetValue(hash, out chunk))
                        {
                            byte[] packed;
                            using (var memory = new MemoryStream()) { using (var gzip = new GZipStream(memory, CompressionLevel.Optimal, true)) gzip.Write(data, 0, data.Length); packed = memory.ToArray(); }
                            if (packStream == null || packStream.Length + packed.Length > 512L * 1024 * 1024)
                            {
                                if (packStream != null) { pack.length = packStream.Length; packStream.Dispose(); pack.sha256 = Format.FileHash(Path.Combine(output, pack.name)); }
                                pack = new Pack { name = "OpenZ-VK-payload-" + (++number).ToString("000") + ".pack" }; result.packs.Add(pack);
                                packStream = File.Create(Path.Combine(output, pack.name));
                            }
                            chunk = new Chunk { hash = hash, length = data.Length, packedLength = packed.Length, asset = pack.name, offset = packStream.Position };
                            packStream.Write(packed, 0, packed.Length); unique.Add(hash, chunk);
                        }
                        file.chunks.Add(chunk);
                    }, CancellationToken.None);
                    result.files.Add(file);
                }
                if (packStream != null) { pack.length = packStream.Length; packStream.Dispose(); packStream = null; pack.sha256 = Format.FileHash(Path.Combine(output, pack.name)); }
                Format.Validate(result);
                File.WriteAllText(Path.Combine(output, Format.ManifestName), Format.Json().Serialize(result), new UTF8Encoding(false));
                return result;
            }
            finally { if (packStream != null) packStream.Dispose(); }
        }
    }
    public static class UpdateTools
    {
        public static int Main(string[] args)
        {
            try
            {
                if (args.Length == 1 && args[0] == "--test") { Checks.Run(); return 0; }
                if (args.Length == 6 && args[0] == "--pack")
                {
                    var result = Packager.Build(args[1], args[2], args[3], args[4], File.ReadAllText(args[5]));
                    Console.WriteLine("PASS: " + result.files.Count + " player files; " + result.packs.Count + " payload packs; source " + result.commit); return 0;
                }
                Console.Error.WriteLine("Usage: UpdateTools --test | --pack PLAYER OUTPUT stable|dev TAG NOTES_FILE"); return 2;
            }
            catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }
    }
}
