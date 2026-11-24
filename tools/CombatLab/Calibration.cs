using OpenZ.Sim;

internal static class Calibration
{
    internal static IReadOnlyDictionary<string, string> Profiles { get; } = MakeProfiles();

    static Dictionary<string, string> MakeProfiles()
    {
        var profiles = new Dictionary<string, string>();
        foreach (int response in new[] { 1, 2 })
        foreach (double boost in new[] { 1.35, 1.65 })
        foreach (double distance in new[] { .85, 1.1 })
        {
            string name = $"Tune {profiles.Count + 1}";
            var profile = new ZBehaviorProfile { family = ZBehaviorFamily.Tanks, master = 0, name = $"Combat Lab - {name}" };
            profile.Set("", ZBehaviorKey.Physical, 1);
            profile.Set("", ZBehaviorKey.Lateral, 0);
            profile.Set("", ZBehaviorKey.TurnFirst, 1);
            profile.Set("", ZBehaviorKey.DodgePhysical, 1);
            profile.Set("", ZBehaviorKey.Direction, response);
            profile.Set("", ZBehaviorKey.BoostSpeed, boost);
            profile.Set("", ZBehaviorKey.Distance, distance);
            profile.Set("", ZBehaviorKey.TurnAcceleration, 900);
            profile.Set("light", ZBehaviorKey.TurnRate, 150);
            profile.Set("medium", ZBehaviorKey.TurnRate, 120);
            profile.Set("heavy", ZBehaviorKey.TurnRate, 90);
            profiles.Add(name, profile.Encode());
        }
        return profiles;
    }

    internal static object Rank(LabDatabase db, long campaign) => db.Query($"""
        WITH means AS (
            SELECT engine, task % 2 AS train, mode, shooter, target, distance,
                   AVG(CASE WHEN shots>0 THEN 1.0*lost/max_hp END) AS hp_loss,
                   1.0*SUM(hits)/NULLIF(SUM(shots),0) AS hits, AVG(shots) AS fire_rate
            FROM trial WHERE campaign={campaign} AND mode LIKE 'shot-%'
            GROUP BY engine,train,mode,shooter,target,distance
        ), errors AS (
            SELECT x.engine, x.train, abs(COALESCE(x.hp_loss,0)-COALESCE(z.hp_loss,0)) AS hp_gap,
                   abs(COALESCE(x.hits,0)-COALESCE(z.hits,0)) AS hit_gap,
                   abs(x.fire_rate-z.fire_rate) AS fire_gap
            FROM means x JOIN means z USING (train,mode,shooter,target,distance)
            WHERE x.engine<>'Zod' AND z.engine='Zod'
        )
        SELECT engine, SUM(train) AS training_cases, SUM(1-train) AS validation_cases,
               ROUND(100*AVG(CASE WHEN train=1 THEN hp_gap+.15*hit_gap+.15*fire_gap END),2) AS score,
               ROUND(100*AVG(CASE WHEN train=0 THEN hp_gap+.15*hit_gap+.15*fire_gap END),2) AS validation
        FROM errors GROUP BY engine ORDER BY score IS NULL, score
        """);
}
