using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OpenZ.Sim;

string root = Path.GetFullPath(Environment.GetEnvironmentVariable("OPENZ_COMBAT_ROOT") ?? Directory.GetCurrentDirectory());
string data = Path.GetFullPath(Environment.GetEnvironmentVariable("OPENZ_COMBAT_DATA") ?? Path.Combine(root, ".local", "combat-lab"));
string resources = Path.Combine(root, "source", "Assets", "OpenZ", "Resources", "OpenZ");
string settings = File.ReadAllText(Path.Combine(resources, "default_settings.txt"));
string zodSource = Environment.GetEnvironmentVariable("OPENZ_COMBAT_ZOD_SOURCE") ?? @"D:\fenio-zod-engine";
string zodSettings = Environment.GetEnvironmentVariable("OPENZ_COMBAT_ZOD_SETTINGS") ?? Path.Combine(zodSource, "default_settings.txt");
var zod = new ZodProbe(Path.Combine(data, "zod-bin"), zodSource, zodSettings);
string candidatePath = Environment.GetEnvironmentVariable("OPENZ_COMBAT_PROFILE");
string profile = candidatePath == null ? null : ReadTanks(candidatePath).Encode();
ZBehaviorProfile ReadTanks(string path)
{
    if (new FileInfo(path).Length > 2_000_000) throw new FormatException("The candidate profile is too large.");
    string content = File.ReadAllText(path);
    ZBehaviorProfile selected;
    if (content.TrimStart().StartsWith("{", StringComparison.Ordinal))
    {
        using var document = JsonDocument.Parse(content);
        string tanks = document.RootElement.TryGetProperty("tanks", out var section) ? section.GetRawText() : content;
        selected = ZBehaviorJson.DecodeJson(tanks);
    }
    else selected = ZBehaviorProfile.Decode(content);
    return selected.family == ZBehaviorFamily.Tanks ? selected : throw new FormatException("Use an OpenZ tank or combined behavior profile.");
}
string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
string signature = Hash(File.ReadAllBytes(typeof(ZWorld).Assembly.Location)) + "/" + Hash(Encoding.UTF8.GetBytes(settings));
Directory.CreateDirectory(data);
using var owner = new FileStream(Path.Combine(data, "owner.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
using var db = new LabDatabase(Path.Combine(data, "results.sqlite3"));
using var lab = new Campaigns(db, new OpenZProbe(settings), profile == null ? null : new OpenZProbe(settings, profile), zod,
    signature, profile == null ? null : Hash(Encoding.UTF8.GetBytes(profile)), settings);

var builder = WebApplication.CreateBuilder(args);
int port = int.TryParse(Environment.GetEnvironmentVariable("OPENZ_COMBAT_PORT"), out int value) ? value : 8911;
string url = $"http://127.0.0.1:{port}";
builder.WebHost.UseUrls(url);
var app = builder.Build();
app.Use(async (context, next) =>
{
    if (context.Request.Host.Host != "127.0.0.1" ||
        (context.Request.Headers.TryGetValue("Origin", out var origin) && origin != url))
    {
        context.Response.StatusCode = 403;
        return;
    }
    try { await next(); }
    catch (InvalidOperationException error) when (!context.Response.HasStarted)
    {
        context.Response.StatusCode = 409;
        await context.Response.WriteAsJsonAsync(new { error = error.Message });
    }
});
app.MapGet("/", () => Results.File(Path.Combine(AppContext.BaseDirectory, "dashboard.html"), "text/html; charset=utf-8"));
app.MapGet("/api/status", () => lab.Status());
app.MapPost("/api/campaign", (Plan plan) => new { id = lab.Create(plan) });
app.MapPost("/api/campaign/{id:long}/resume", (long id, int? repeats) => { lab.Start(id, repeats); return Results.Ok(); });
app.MapPost("/api/pause", () => { lab.Pause(); return Results.Ok(); });
app.MapGet("/api/campaign/{id:long}/ranking", (long id) => Calibration.Rank(db, id));
app.MapGet("/api/profile/{name}", (string name) => Calibration.Profiles.TryGetValue(name, out string json)
    ? Results.File(Encoding.UTF8.GetBytes(ZBehaviorJson.EncodeJson(ZBehaviorProfile.Decode(json))), "application/json", $"Combat Lab {name}.json") : Results.NotFound());
app.MapGet("/api/campaign/{id:long}/results", (long id, string mode, int? terrain, int? health, int? distance) =>
{
    if (mode is not ("blast" or "duel" or "shot-idle" or "shot-move" or "shot-attack" or "move-normal" or "move-boost"))
        return Results.BadRequest();
    string filters = $"campaign={id} AND mode={LabDatabase.Quote(mode)}";
    if (terrain.HasValue) filters += $" AND terrain={terrain.Value}";
    if (health.HasValue) filters += $" AND health={health.Value}";
    if (distance.HasValue) filters += $" AND distance={distance.Value}";
    return Results.Json(db.Query($"""
        SELECT engine,shooter,target,distance,terrain,health,COUNT(*) AS n,SUM(shots) AS shots,
               ROUND(AVG(CASE WHEN shots>0 OR mode='duel' THEN lost END),2) AS lost,
               ROUND(100*AVG(CASE WHEN shots>0 OR mode='duel' THEN 1.0*lost/max_hp END),2) AS lost_percent,
               ROUND(AVG(CASE WHEN hits>0 THEN lost END),2) AS on_hit,
               ROUND(100.0*SUM(hits)/NULLIF(SUM(shots),0),2) AS hit_rate,
               ROUND(100.0*SUM(dodged)/NULLIF(SUM(shots),0),2) AS dodge_rate,
               ROUND(100.0*AVG(outcome=1),2) AS win_rate, ROUND(100.0*AVG(outcome=0),2) AS draw_rate,
               ROUND(AVG(shooter_lost),2) AS shooter_lost, ROUND(AVG(seconds),2) AS seconds,
               MAX(radius) AS radius, ROUND(AVG(travel/NULLIF(seconds,0)),2) AS speed
        FROM trial WHERE {filters} GROUP BY engine,shooter,target,distance,terrain,health
        ORDER BY engine,shooter,target,distance,terrain,health
        """));
});
app.MapGet("/api/campaign/{id:long}/export.csv", (long id) => Results.Stream(async stream =>
{
    await using var writer = new StreamWriter(stream);
    const string fields = "engine,task,seed,mode,shooter,target,distance,terrain,health,lost,max_hp,shooter_lost,radius,shots,hits,dodged,outcome,seconds,travel";
    await writer.WriteLineAsync(fields);
    var limits = db.Query($"SELECT engine, max(rowid) AS last FROM trial WHERE campaign={id} GROUP BY engine");
    foreach (var limit in limits)
    {
        long before = long.Parse(limit["last"]), after = 0;
        while (after < before)
        {
            var rows = db.Query($"SELECT {fields},rowid FROM trial WHERE rowid>{after} AND rowid<={before} AND campaign={id} AND engine={LabDatabase.Quote(limit["engine"])} ORDER BY rowid LIMIT 1000");
            if (rows.Count == 0) break;
            foreach (var row in rows) await writer.WriteLineAsync(string.Join(',', row.Values.SkipLast(1)));
            after = long.Parse(rows[^1]["rowid"]);
        }
    }
}, "text/csv", $"combat-lab-{id}.csv"));
if (Environment.GetEnvironmentVariable("OPENZ_COMBAT_BROWSER") == "1")
    app.Lifetime.ApplicationStarted.Register(() =>
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (System.ComponentModel.Win32Exception) { Console.WriteLine($"Open {url} in your browser."); }
    });
app.Run();
