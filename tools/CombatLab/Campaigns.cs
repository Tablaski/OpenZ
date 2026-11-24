using System.Text;
using System.Text.Json;

internal record Plan(string Suite, int Repeats, bool CompareZod, bool Candidate);
internal record Work(int Index, int Seed, Scenario Scenario);

internal sealed class Campaigns(LabDatabase db, OpenZProbe open, OpenZProbe candidate, ZodProbe zod, string sourceSignature) : IDisposable
{
    readonly object gate = new();
    CancellationTokenSource stop;
    Task runner;
    long? active;
    string error;
    static string Q(string value) => LabDatabase.Quote(value);

    internal object Status()
    {
        lock (gate)
            return new
            {
                active, error, zod = zod.Available, candidate = candidate != null,
                campaigns = db.Query("SELECT c.id,c.created,c.plan,(SELECT COUNT(*) FROM trial t WHERE t.campaign=c.id) AS done FROM campaign c ORDER BY id DESC")
                    .Select(row => new
                    {
                        id = long.Parse(row["id"]), created = row["created"], plan = JsonSerializer.Deserialize<Plan>(row["plan"]),
                        done = int.Parse(row["done"]), total = Count(JsonSerializer.Deserialize<Plan>(row["plan"]))
                    }).ToArray()
            };
    }

    internal long Create(Plan plan)
    {
        lock (gate)
        {
            Validate(plan);
            if (active.HasValue) throw new InvalidOperationException("Pause the current campaign before starting another.");
            db.Transaction($"INSERT INTO campaign(created,plan,signature) VALUES ({Q(DateTime.UtcNow.ToString("s") + "Z")},{Q(JsonSerializer.Serialize(plan))},{Q(Signature(plan))});");
            long id = long.Parse(db.Query("SELECT last_insert_rowid() AS id")[0]["id"]);
            Start(id);
            return id;
        }
    }

    internal void Start(long id, int? repeats = null)
    {
        lock (gate)
        {
            if (active.HasValue) throw new InvalidOperationException("A campaign is already running.");
            var rows = db.Query($"SELECT plan,signature FROM campaign WHERE id={id}");
            if (rows.Count != 1) throw new InvalidOperationException("Campaign not found.");
            var plan = JsonSerializer.Deserialize<Plan>(rows[0]["plan"]);
            Validate(plan);
            if (rows[0]["signature"] != Signature(plan))
                throw new InvalidOperationException("The engine or settings for this campaign have changed. Start a new campaign to keep results comparable.");
            if (repeats.HasValue)
            {
                if (plan.Suite == "blast" || repeats <= plan.Repeats || repeats > 5000)
                    throw new InvalidOperationException("Choose a larger repeat count up to 5000 for a combat campaign.");
                plan = plan with { Repeats = repeats.Value };
                db.Transaction($"UPDATE campaign SET plan={Q(JsonSerializer.Serialize(plan))} WHERE id={id};");
            }
            error = null;
            active = id;
            stop = new CancellationTokenSource();
            runner = Task.Run(async () =>
            {
                try { await Run(id, plan, stop.Token); }
                catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
                catch (Exception ex) { lock (gate) error = ex.Message; }
                finally
                {
                    lock (gate) { stop.Dispose(); stop = null; active = null; }
                }
            });
        }
    }

    internal void Pause() { lock (gate) stop?.Cancel(); }

    void Validate(Plan plan)
    {
        if (plan == null || plan.Suite is not ("blast" or "tanks" or "terrain") || plan.Repeats < 1 || plan.Repeats > 5000)
            throw new InvalidOperationException("Choose a suite and between 1 and 5000 repetitions.");
        if (plan.CompareZod && !zod.Available) throw new InvalidOperationException("The Zod worker is missing. Run Setup-Zod.ps1 first.");
        if (plan.Candidate && candidate == null) throw new InvalidOperationException("Load a tank profile with Start-CombatLab.ps1 -CandidateProfile first.");
    }

    string Signature(Plan plan) => sourceSignature + (plan.CompareZod ? "/" + zod.Identity : "");

    static IEnumerable<Scenario> Scenarios(string suite)
    {
        if (suite == "blast")
        {
            foreach (string shooter in OpenZProbe.Weapons)
            foreach (string target in OpenZProbe.Tanks)
            for (int distance = 0; distance <= 128; distance++)
                yield return new("blast", shooter, target, distance, 0, 100);
            yield break;
        }
        int[] terrains = suite == "terrain" ? [0, 1] : [0];
        int[] healths = suite == "terrain" ? [100, 45, 20] : [100];
        foreach (int terrain in terrains)
        foreach (int health in healths)
        foreach (string shooter in OpenZProbe.Tanks)
        foreach (string target in OpenZProbe.Tanks)
        foreach (int distance in new[] { 56, 96 })
        foreach (string mode in new[] { "shot-idle", "shot-move", "shot-attack", "duel" })
            yield return new(mode, shooter, target, distance, terrain, health);
        foreach (string unit in OpenZProbe.Weapons.Take(4))
        foreach (int terrain in suite == "terrain" ? new[] { 0, 1, 2 } : terrains)
        foreach (int health in healths)
            yield return new("move-normal", unit, unit, 0, terrain, health);
    }

    static int Count(Plan plan) => Scenarios(plan.Suite).Sum(s => Repeats(s, plan)) *
        (1 + (plan.CompareZod ? 1 : 0) + (plan.Candidate ? 1 : 0));

    static int Repeats(Scenario scenario, Plan plan) => scenario.Mode is "blast" or "move-normal" ? 1 : plan.Repeats;

    async Task Run(long id, Plan plan, CancellationToken token)
    {
        var tasks = Scenarios(plan.Suite).SelectMany((scenario, index) => Enumerable.Range(1, Repeats(scenario, plan))
            .Select(seed => new Work(index * 5000 + seed, seed, scenario)));
        var engines = new List<string> { "OpenZ" };
        if (plan.CompareZod) engines.Add("Zod");
        if (plan.Candidate) engines.Add("Candidate");
        var done = db.Query($"SELECT engine,task FROM trial WHERE campaign={id}")
            .ToLookup(row => row["engine"], row => int.Parse(row["task"]));
        var batches = engines.Select(engine =>
        {
            var complete = done[engine].ToHashSet();
            return tasks.Where(task => !complete.Contains(task.Index)).Chunk(24).Select(batch => (engine, batch));
        }).ToArray();
        await Parallel.ForEachAsync(batches.SelectMany(batch => batch), new ParallelOptions
        {
            CancellationToken = token, MaxDegreeOfParallelism = Math.Clamp(Environment.ProcessorCount - 1, 1, 8)
        }, async (job, cancellation) =>
        {
            Sample[] results;
            if (job.engine == "Zod")
            {
                try { results = await zod.Run(job.batch, cancellation); }
                catch (IOException) when (!cancellation.IsCancellationRequested) { results = await zod.Run(job.batch, cancellation); }
            }
            else
            {
                var probe = job.engine == "Candidate" ? candidate : open;
                results = new Sample[job.batch.Length];
                for (int i = 0; i < results.Length; i++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    results[i] = probe.Run(job.batch[i].Scenario, job.batch[i].Seed);
                }
            }
            var sql = new StringBuilder();
            foreach (var pair in job.batch.Zip(results))
            {
                var (work, result) = pair;
                var s = work.Scenario;
                sql.Append(FormattableString.Invariant($"INSERT OR IGNORE INTO trial VALUES ({id},{Q(job.engine)},{work.Index},{work.Seed},{Q(s.Mode)},{Q(s.Shooter)},{Q(s.Target)},{s.Distance},{s.Terrain},{s.Health},{result.Lost},{result.Max},{result.ShooterLost},{result.Radius},{result.Shots},{result.Hits},{result.Dodged},{result.Outcome},{result.Seconds},{result.Travel});"));
            }
            db.Transaction(sql.ToString());
        });
    }

    public void Dispose()
    {
        Pause();
        runner?.GetAwaiter().GetResult();
    }
}
