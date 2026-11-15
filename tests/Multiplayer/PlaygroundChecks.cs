using System;
using System.IO;
using System.Linq;
using OpenZ;
using OpenZ.Sim;

static class PlaygroundChecks
{
    public static void Run(Action<bool, string> check, Action<Action, string> reject)
    {
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../source/Assets/OpenZ/Resources/OpenZ"));
        var w = ZWorld.Create(File.ReadAllText(Path.Combine(root, "default_settings.txt")), File.ReadAllText(Path.Combine(root, "Maps/L02_grid.json")), 817);
        check(w.UnitTesting == null, "Ordinary matches have no testing rules");
        reject(w.EnableUnitTesting, "Testing requires a playground");
        w.NoGameEnd = true; w.EnableUnitTesting(); var t = w.UnitTesting;
        var red = w.CreateObject("vehicle", "light", 240, 240, Team.Red);
        var blue = w.CreateObject("vehicle", "heavy", 600, 240, Team.Blue);
        red.Waypoints.Add(new ZWaypoint { mode = WpMode.Attack, refId = blue.Id, playerGiven = true });
        t.SetHpPercent(Team.Red, 37); t.SetHpPercent(Team.Blue, 64);
        int reference = t.ReferenceHp(red);
        check(red.Health == reference && blue.Health == t.ReferenceHp(blue), "Independent percentages apply to existing units");
        var future = w.CreateObject("robot", "grunt", 400, 400, Team.Red);
        check(future.Health == t.ReferenceHp(future), "New units inherit their team's reference HP");
        t.Configure(Team.Red, true, false);
        red.DamageHealth(reference - 1, blue.Id);
        check(red.Health == reference && t.For(red.Id).Deaths == 0, "Invulnerable HP stay pinned while theoretical damage accumulates");
        red.DamageHealth(1, blue.Id);
        check(red.Health == reference && t.For(red.Id).Deaths == 1 && t.For(blue.Id).Kills == 1, "Invulnerable theoretical lethal hit counts a kill and death");
        check(!red.Destroyed && !red.HasProcessedDeath && red.Waypoints.Count == 1, "Virtual death preserves existence and orders");
        bool theoretical = false; while (t.TryReadHit(out var h)) theoretical |= h.Theoretical && h.Deaths == 1 && h.Attacker == blue.Id;
        check(theoretical, "Theoretical damage events retain attribution while display is off");
        t.Configure(Team.Red, true, true);
        red.DamageHealth(1, blue.Id);
        check(red.Health == reference - 1, "Circular immortals take real partial damage");
        red.DamageHealth(int.MaxValue, blue.Id);
        check(red.Health == reference && t.For(red.Id).Deaths == 2 && t.For(blue.Id).Kills == 2, "Circular overkill resets exactly to selected HP and counts one life");
        t.SetHpPercent(Team.Red, 21); reference = t.ReferenceHp(red);
        check(red.Health == reference && t.For(red.Id).Deaths == 2 && blue.Health == t.ReferenceHp(blue), "Changing red HP resets current HP without resetting scores or blue HP");
        red.DamageHealth(reference, blue.Id);
        check(red.Health == reference && t.For(red.Id).Deaths == 3, "Subsequent cycle uses the updated reference percentage");
        red.Drivers.Clear(); red.Drivers.Add(10);
        red.DamageDriverHealth(20, blue.Id);
        check(red.Owner == Team.Red && red.Drivers.Count == 1 && red.Drivers[0] > 0 && red.Waypoints.Count == 1, "Immortality protects crew and attack orders from sniper defeat");
        check(t.For(red.Id).Deaths == 4 && t.For(blue.Id).Kills == 4, "Crew defeat counts as a virtual defeat");
        t.Configure(Team.Red, false, false);
        future.DamageHealth(future.Health, blue.Id); future.DamageHealth(100, blue.Id);
        check(future.Destroyed && t.For(future.Id).Deaths == 1 && t.For(blue.Id).Kills == 5, "Mortal death counts once and follows ordinary destruction");
        t.Configure(Team.Red, true, false); t.SetHpPercent(Team.Red, 12);
        red.SetHealth(red.MaxHealth);
        check(red.Health == t.ReferenceHp(red), "Healing cannot move pinned immortal HP away from reference");
        t.SetHpPercent(Team.Blue, 0); check(t.HpPercent(Team.Blue) == 1 && blue.Health >= 1, "HP percentage has a living one-percent floor");
        t.SetHpPercent(Team.Blue, 200); check(t.HpPercent(Team.Blue) == 100, "HP percentage cannot exceed maximum health");
        reject(() => t.SetHpPercent(Team.Red, double.NaN), "Non-finite HP rejected");
        reject(() => t.Configure(Team.Neutral, true, true), "Neutral testing configuration rejected");

        var p = new ZBehaviorProfile { family = ZBehaviorFamily.Tanks, master = 100 };
        check(!p.Resolve("light").On(ZBehaviorKey.CombatManoeuvres), "Zod reference disables OpenZ offensive flanking");
        p.Set("", ZBehaviorKey.BoostSpeed, 25); p.Set("", ZBehaviorKey.Distance, 12);
        check(ZBehaviorProfile.Decode(p.Encode()).Get("light", ZBehaviorKey.BoostSpeed) == 25 && p.Get("light", ZBehaviorKey.Distance) == 12, "Custom numeric values above old slider limits round-trip");
        p.Set("", ZBehaviorKey.BoostChance, 200); check(p.Get("light", ZBehaviorKey.BoostChance) == 100, "Probabilities retain meaningful bounds");
        p.Set("", ZBehaviorKey.ResumeAttack, 1); p.Set("", ZBehaviorKey.Replan, 1);
        p.Set("", ZBehaviorKey.Endurance, 0); p.Set("", ZBehaviorKey.Commit, 0);
        w.SetBehavior(p);
        check(red.DodgeMissile(red.CenterX, red.CenterY, .1), "Strong custom boost starts a defensive dodge during attack");
        check(red.Waypoints.Count == 2 && red.Waypoints[1].refId == blue.Id, "Strong dodge retains the original target");
        var endpoint = red.Waypoints[0];
        check(endpoint.x >= 0 && endpoint.y >= 0 && endpoint.x <= w.Map.WidthPix && endpoint.y <= w.Map.HeightPix, "Extreme defensive target remains inside the map");
        // Later threats may change direction but may not starve the interrupted attack.
        w.Time += .15; red.DodgeMissile(red.CenterX, red.CenterY, 10);
        w.Time += .1; red.ProcessServer(w.Time);
        check(!red.Waypoints.Any(x => x.mode == WpMode.Dodge) && red.Waypoints.Any(x => x.refId == blue.Id), "Volley replanning cannot postpone attack resumption indefinitely");

        var score = t.For(red.Id);
        score.Kills = 3; score.Deaths = 0; check(score.SuccessPercent == 100, "Three kills without death gives 100 percent success");
        score.Kills = 2; score.Deaths = 2; check(score.SuccessPercent == 50, "Equal kills and deaths give 50 percent success");
        score.Kills = long.MaxValue; score.Deaths = long.MaxValue;
        check(score.SuccessPercent == 50, "Success ratio cannot overflow long counters");
        t.Configure(Team.Red, true, false); t.SetHpPercent(Team.Red, 50);
        int hp = red.Health; red.DamageHealth(hp - 1, blue.Id);
        t.ResetCounters();
        check(score.Kills == 0 && score.Deaths == 0 && score.SuccessPercent == 0 && t.For(blue.Id).Kills == 0, "Reset clears every score and empty ratio");
        check(red.Health == hp && red.Waypoints.Any(x => x.refId == blue.Id), "Reset preserves health and attack orders");
        red.DamageHealth(1, blue.Id);
        check(score.Deaths == 1 && t.For(blue.Id).Kills == 1, "Reset does not erase partial theoretical damage");
        score.Kills = 7; t.ResetAutomaticallyOnParametersChange = true;
        w.SetBehavior(p.Clone());
        check(score.Kills == 7, "Identical effective profile does not auto-reset counters");
        var renamed = p.Clone(); renamed.name = "Renamed only"; w.SetBehavior(renamed);
        check(score.Kills == 7, "Renaming a profile does not auto-reset counters");
        p.Set("medium", ZBehaviorKey.Reaction, .3); w.SetBehavior(p);
        check(score.Kills == 7, "Editing an unused fighting type does not reset counters");
        p.Set("light", ZBehaviorKey.Reaction, .3); w.SetBehavior(p);
        check(score.Kills == 0 && score.Deaths == 0 && t.For(blue.Id).Kills == 0, "Effective change affecting one fighter resets all units' scores");
        t.ResetAutomaticallyOnParametersChange = false; score.Kills = 4;
        p.Set("light", ZBehaviorKey.Reaction, .5); w.SetBehavior(p);
        check(score.Kills == 4, "Disabling automatic reset preserves counters on parameter changes");
        t.ResetAutomaticallyOnParametersChange = false;
        int rx = 0, ry = 0, bx = 0, by = 0;
        for (int y = 64; y < w.Map.HeightPix - 64 && bx == 0; y += 32)
            for (int x = 64; x < w.Map.WidthPix - 64; x += 32)
            {
                if (!t.CanPlace(red, x, y) || !t.CanPlace(blue, x, y)) continue;
                if (rx == 0) { rx = x; ry = y; }
                else if (Math.Abs(x - rx) + Math.Abs(y - ry) >= 320) { bx = x; by = y; break; }
            }
        check(bx > 0 && t.SetRespawnPoint(Team.Red, rx, ry) && t.SetRespawnPoint(Team.Blue, bx, by), "Both respawn points accept clear ground");
        check(!t.SetRespawnPoint(Team.Red, -1, ry), "Invalid point preserves the previous respawn point");
        t.Respawning = true; t.ResetCounters();
        foreach (bool immortal in new[] { false, true }) foreach (bool cycling in new[] { false, true })
        {
            t.Configure(Team.Red, immortal, cycling); t.Configure(Team.Blue, immortal, cycling);
            for (int round = 0; round < 20; round++)
            {
                var loser = round % 2 == 0 ? red : blue; var winner = loser == red ? blue : red;
                long deaths = t.For(loser.Id).Deaths, kills = t.For(winner.Id).Kills;
                loser.DamageHealth(int.MaxValue, winner.Id);
                loser.DamageHealth(int.MaxValue, winner.Id);
                check(!loser.Destroyed && t.For(loser.Id).Deaths == deaths + 1 && t.For(winner.Id).Kills == kills + 1,
                    "Respawn counts one defeat per round for mortal and both immortal modes");
                w.AddMissileForTest(new ZMissile { attackerId = winner.Id, targetId = loser.Id });
                t.FinishRespawns();
                check(red.CenterX == rx && red.CenterY == ry && blue.CenterX == bx && blue.CenterY == by &&
                    red.Health == t.ReferenceHp(red) && blue.Health == t.ReferenceHp(blue), "Every duel restarts at the same points and reference HP");
                check(red.Waypoints.Single().refId == blue.Id && blue.Waypoints.Single().refId == red.Id &&
                    !w.QueuedMissiles.Any(m => m.attackerId == winner.Id), "Both opponents attack again without old projectiles");
            }
        }
        t.Configure(Team.Red, false, false); red.Drivers.Clear(); red.Drivers.Add(1);
        red.DamageDriverHealth(100, blue.Id); t.FinishRespawns();
        check(red.Owner == Team.Red && red.Drivers.Count == 1 && red.Drivers[0] > 1, "Mortal crew defeat restores the original crew and ownership");
        t.Hold(red, true); int heldHp = red.Health;
        red.DamageHealth(int.MaxValue, blue.Id); red.DamageDriverHealth(int.MaxValue, blue.Id);
        red.ProcessServer(w.Time + .1);
        check(red.Health == heldHp && red.Owner == Team.Red && red.Waypoints.Count == 0 && !blue.CanAttackObject(red),
            "Lifted units stop fighting and cannot receive hull or crew damage or be targeted");
        t.Hold(red, false); check(!t.Held(red), "Dropping releases the lifted unit");
        red.DamageHealth(int.MaxValue, blue.Id); w.Tick(ZConst.TICK);
        check(!t.Held(red) && red.CenterX == rx && red.CenterY == ry && blue.CenterX == bx && blue.CenterY == by &&
            red.Waypoints.Any(x => x.refId == blue.Id) && blue.Waypoints.Any(x => x.refId == red.Id),
            "The world tick completes deferred respawns and restarts both attacks");
        t.Respawning = false; red.DamageHealth(int.MaxValue, blue.Id);
        check(red.Destroyed, "Disabling respawning restores ordinary mortal death");
    }
}
