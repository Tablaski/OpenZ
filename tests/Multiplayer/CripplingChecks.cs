using System;
using System.Reflection;
using OpenZ;
using OpenZ.Sim;

static class CripplingChecks
{
    static readonly MethodInfo Velocity = typeof(ZObject).GetMethod("BehaviorVelocity", BindingFlags.NonPublic | BindingFlags.Instance);
    static (double x, double y) Step(ZObject tank, double dt, double speed)
    {
        var args = new object[] { dt, speed, 0d }; Velocity.Invoke(tank, args);
        return ((double)args[1], (double)args[2]);
    }
    static double Speed((double x, double y) v) => Math.Sqrt(v.x * v.x + v.y * v.y);
    public static void Run(string settings, string map, int x, int y, Action<bool, string> check)
    {
        var world = ZWorld.Create(settings, map, 1027);
        var p = new ZBehaviorProfile { family = ZBehaviorFamily.Tanks, master = 100 };
        p.ConfigureTurnFirst(""); p.Set("", ZBehaviorKey.Acceleration, 0); p.Set("", ZBehaviorKey.Braking, 0);
        p.Set("", ZBehaviorKey.ReverseDelay, 0); p.Set("", ZBehaviorKey.ReverseSpeed, 100);
        p.Set("", ZBehaviorKey.AlignmentTolerance, 0); p.Set("", ZBehaviorKey.TurnRate, 90);
        world.SetBehavior(p);
        for (int angle = -180; angle <= 180; angle += 15)
        {
            var t = world.CreateObject("vehicle", "light", x + 48, y + 48, Team.Red);
            double radians = angle * Math.PI / 180;
            t.BehaviorHeadingSet = true; t.BehaviorHeading = 0;
            t.SetTarget(t.CenterX + (int)Math.Round(Math.Cos(radians) * 48), t.CenterY + (int)Math.Round(Math.Sin(radians) * 48));
            Step(t, 1, 20);
            check(Math.Abs(t.BehaviorHeading) <= Math.PI / 2 + 1e-8, "Shortest hull axis at " + angle + " degrees never turns over 90 degrees");
            var velocity = Step(t, 1, 20);
            check(velocity.x * Math.Cos(radians) + velocity.y * Math.Sin(radians) > 19.9, "Forward/reverse travel reaches the requested quadrant at " + angle + " degrees");
        }
        var tank = world.CreateObject("vehicle", "light", x + 48, y + 48, Team.Red);
        tank.BehaviorHeadingSet = true; tank.BehaviorHeading = 0;
        tank.SetTarget(tank.CenterX - 48, tank.CenterY);
        p.Set("", ZBehaviorKey.ReverseSpeed, 50); world.SetBehavior(p);
        check(Math.Abs(Step(tank, .1, 20).x + 10) < 1e-8 && Math.Abs(tank.BehaviorHeading) < 1e-8, "Rear move backs straight up at configured reverse speed without a half-turn");
        p.Set("", ZBehaviorKey.Reverse, 0); world.SetBehavior(p);
        Step(tank, 1, 20); Step(tank, 1, 20);
        check(Math.Abs(Math.Abs(tank.BehaviorHeading) - Math.PI) < 1e-8, "Explicit reversing OFF still permits forward-only half turns");

        p.Set("", ZBehaviorKey.Physical, 0); p.Set("", ZBehaviorKey.Crippling, 1);
        p.Set("", ZBehaviorKey.GreenSpeed, 100); p.Set("", ZBehaviorKey.GreenAcceleration, 0);
        p.Set("", ZBehaviorKey.YellowSpeed, 45); p.Set("", ZBehaviorKey.YellowAcceleration, 0);
        p.Set("", ZBehaviorKey.RedSpeed, 20); p.Set("", ZBehaviorKey.RedAcceleration, 0);
        world.SetBehavior(p); tank.MaxHealth = 10000;
        foreach (var band in new[] { (hp: 5001, band: ZHealthBand.Green, speed: 100d / 90), (hp: 5000, band: ZHealthBand.Yellow, speed: .5),
            (hp: 2501, band: ZHealthBand.Yellow, speed: .5625), (hp: 2500, band: ZHealthBand.Red, speed: .25) })
        {
            tank.Health = band.hp;
            check(tank.HealthBand == band.band && Math.Abs(Speed(Step(tank, .1, 20)) - 20 * band.speed) < .0001,
                "Health colour boundary and replacement speed at " + band.hp + " HP");
        }
        tank.Health = 2500;
        p.Set("", ZBehaviorKey.RedSpeed, 0); world.SetBehavior(p);
        check(Speed(Step(tank, .1, 20)) == 0, "Zero-percent damage speed stops actual displacement without deleting orders");
        p.Set("", ZBehaviorKey.RedSpeed, 80); p.Set("", ZBehaviorKey.RedAcceleration, 2); world.SetBehavior(p);
        tank.CripplingActualSpeed = 0;
        double initial = Speed(Step(tank, .1, 20)), later = Speed(Step(tank, .1, 20));
        check(initial > 0 && later > initial && later < 20, "Damage acceleration loss ramps real velocity with physical limits OFF");
        p.Set("", ZBehaviorKey.Crippling, 0); world.SetBehavior(p);
        check(Speed(Step(tank, .1, 20)) == 20, "Custom crippling OFF immediately restores native command speed");

        var enemy = world.CreateObject("vehicle", "light", x + 48, y + 48, Team.Blue);
        var ally = world.CreateObject("vehicle", "light", x + 48, y + 48, Team.Red);
        p.Set("", ZBehaviorKey.Impact, 1); p.Set("", ZBehaviorKey.ImpactMin, 100);
        p.Set("", ZBehaviorKey.HeavyImpactMin, 500); p.Set("", ZBehaviorKey.ImpactSpeed, 40);
        p.Set("", ZBehaviorKey.HeavyImpactSpeed, 0); p.Set("", ZBehaviorKey.ImpactDuration, .2);
        p.Set("", ZBehaviorKey.HeavyImpactDuration, .8); world.SetBehavior(p);
        tank.SetHealth(10000); tank.DamageHealth(99, enemy.Id);
        check(!tank.CripplingHitActive, "Damage below the actual HP threshold does not penalize movement");
        tank.DamageHealth(100, enemy.Id);
        check(tank.CripplingHitActive && Math.Abs(Speed(Step(tank, .1, 20)) - 8) < 1e-8, "Light enemy hit applies the first speed cap at its exact threshold");
        tank.DamageHealth(500, enemy.Id);
        double until = tank.CripplingImpactUntil;
        check(Speed(Step(tank, .1, 20)) == 0 && until == world.Time + .8, "Heavy enemy hit stops movement temporarily at its separate threshold");
        tank.DamageHealth(100, enemy.Id);
        check(tank.CripplingImpactFactor == 0 && tank.CripplingImpactUntil >= until, "A weaker follow-up cannot weaken or shorten a heavy penalty");
        var guest = ZWorld.Create(settings, map, 1027); guest.ConfigureDuel(new OpenZ.Multiplayer.DuelRules { Map = "L02" }, true);
        DuelReplica.Apply(guest, DuelReplica.Capture(world));
        check(guest.Get(tank.Id).CripplingImpactUntil == tank.CripplingImpactUntil && guest.Get(tank.Id).CripplingImpactFactor == 0 &&
            guest.Get(tank.Id).HealthBand == tank.HealthBand, "Impact state, health bands and profiles replicate to the guest");
        tank.CripplingImpactUntil = -1;
        check(Speed(Step(tank, .1, 20)) == 20, "Movement recovers automatically after the impact expires");
        tank.DamageHealth(500, ally.Id); tank.SetHealth(8000); tank.DamageHealth(0, enemy.Id); tank.DamageHealth(-100, enemy.Id);
        check(!tank.CripplingHitActive, "Friendly fire, HP edits, zero damage and healing do not trigger impact penalties");
        tank.DamageHealth(500);
        check(tank.CripplingHitActive, "Explosion with a removed or unknown attacker still penalizes actual HP loss");
        p.Set("", ZBehaviorKey.Impact, 0); world.SetBehavior(p);
        check(Speed(Step(tank, .1, 20)) == 20, "Disabling hit penalties live releases an active stop");
        var saved = ZBehaviorProfile.Decode(p.Encode());
        check(saved.Get("light", ZBehaviorKey.RedAcceleration) == 2 && saved.Get("light", ZBehaviorKey.YellowSpeed) == 45,
            "New per-colour values survive save/load while existing numeric key IDs stay unchanged");
    }
}
