using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;

internal sealed class ZodProbe(string directory, string sourceRoot, string settings)
{
    readonly string exe = Path.Combine(directory, "zod.exe");
    internal bool Available => File.Exists(exe) && Directory.Exists(Path.Combine(sourceRoot, "assets")) && File.Exists(settings);
    internal string Identity => Available ? Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(exe))) + "/" +
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(settings))) + "/" +
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(sourceRoot, "assets/planets/desert.tileinfo")))) : "unavailable";

    internal async Task<Sample[]> Run(Work[] items, CancellationToken cancellation)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        timeout.CancelAfter(TimeSpan.FromMinutes(3));
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo(exe)
            {
                WorkingDirectory = sourceRoot, RedirectStandardInput = true, RedirectStandardOutput = true,
                RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true,
                ArgumentList = { "--combat-lab", settings }
            }
        };
        process.Start();
        using var stop = timeout.Token.Register(() =>
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { }
        });
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> errors = process.StandardError.ReadToEndAsync();
        foreach (var item in items)
        {
            var s = item.Scenario;
            int shooter = Array.IndexOf(OpenZProbe.Weapons, s.Shooter);
            int target = Array.IndexOf(OpenZProbe.Weapons, s.Target);
            await process.StandardInput.WriteLineAsync($"{item.Seed} {s.Mode} {shooter} {target} {s.Distance} {s.Terrain} {s.Health}");
        }
        process.StandardInput.Close();
        await process.WaitForExitAsync();
        cancellation.ThrowIfCancellationRequested();
        if (timeout.IsCancellationRequested) throw new IOException("Zod worker exceeded its three-minute timeout.");
        string stdout = await output;
        string stderr = await errors;
        var results = stdout.Split('\n').Where(line => line.StartsWith("LAB ", StringComparison.Ordinal)).Select(Parse).ToArray();
        if (process.ExitCode != 0 || results.Length != items.Length)
            throw new IOException($"Zod returned {results.Length} of {items.Length} trials (exit {process.ExitCode}). {stderr}\n{string.Join('\n', stdout.Split('\n').TakeLast(8))}");
        return results;
    }

    static Sample Parse(string line)
    {
        string[] fields = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        int I(int field) => int.Parse(fields[field], CultureInfo.InvariantCulture);
        double D(int field) => double.Parse(fields[field], CultureInfo.InvariantCulture);
        return new Sample(I(1), I(2), I(3), I(4), I(5), I(6), I(7), I(8), D(9), D(10));
    }
}
