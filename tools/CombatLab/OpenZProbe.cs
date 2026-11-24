using System.Text.Json;
using OpenZ.Sim;

namespace OpenZ.Sim
{
    public sealed partial class ZWorld
    {
        // Calls the same damage resolver used by world ticks. The projectile
        // reaches this method after being emitted by the actual weapon.
        internal void CombatLabExplode(ZMissile missile) => ProcessMissileDamage(missile);
        internal void CombatLabClearProjectiles() { missiles.Clear(); newMissiles.Clear(); }
    }
}

internal record Scenario(string Mode, string Shooter, string Target, int Distance, int Terrain, int Health);
internal record Sample(int Lost, int Max, int ShooterLost, int Radius, int Shots, int Hits, int Dodged,
    int Outcome, double Seconds, double Travel);

internal sealed class OpenZProbe(string settings, string candidate = null)
{
    internal static readonly string[] Tanks = ["light", "medium", "heavy"];
    internal static readonly string[] Weapons = [.. Tanks, "tough", "gun", "howitzer", "missile_cannon", "missile_launcher"];
    static readonly string[] Arenas = Enumerable.Range(0, 3).Select(terrain => JsonSerializer.Serialize(new
    {
        file = "combat_lab_arena", planet = "desert", width = 64, height = 48,
        classes = Enumerable.Repeat(terrain, 64 * 48), tiles = new int[64 * 48],
        zones = new[] { new { x = 0, y = 0, w = 64, h = 48 } }, objects = Array.Empty<object>()
    })).ToArray();

    internal Sample Run(Scenario scenario, int seed)
    {
        ZWorld world = ZWorld.Create(settings, Arenas[scenario.Terrain], seed);
        world.NoGameEnd = true;
        if (candidate != null) world.SetBehavior(ZBehaviorProfile.Decode(candidate));
        ZObject Make(string type, int x, Team team, int health = 100) => world.CreateObject(type switch
        {
            "tough" => "robot", "gun" or "howitzer" or "missile_cannon" => "cannon", _ => "vehicle"
        }, type, x, 384, team, healthPercent: health);
        var target = Make(scenario.Target, 512, Team.Blue, scenario.Health);
        int hp = target.Health;
        if (scenario.Mode.StartsWith("move-", StringComparison.Ordinal))
        {
            int start = target.CenterX;
            target.Waypoints.Add(new ZWaypoint { mode = WpMode.Move, x = start + 320, y = target.CenterY, playerGiven = true });
            for (int i = 0; i < 200; i++) { world.Tick(ZConst.TICK); world.Events.Clear(); }
            return new Sample(0, target.MaxHealth, 0, 0, 0, 0, 0, 0, world.Time, target.CenterX - start);
        }

        var shooter = Make(scenario.Shooter, 512 - (scenario.Mode == "blast" ? 64 : scenario.Distance), Team.Red);
        int shooterHp = shooter.Health;
        void OrderAttack(ZObject unit, ZObject enemy) => unit.Waypoints.Add(new ZWaypoint
        {
            mode = WpMode.Attack, refId = enemy.Id, x = enemy.CenterX, y = enemy.CenterY, playerGiven = true
        });
        OrderAttack(shooter, target);
        if (scenario.Mode is "duel" or "shot-attack") OrderAttack(target, shooter);
        if (scenario.Mode == "shot-move")
            target.Waypoints.Add(new ZWaypoint { mode = WpMode.Move, x = target.CenterX, y = target.CenterY + 160, playerGiven = true });

        if (scenario.Mode != "duel")
        {
            ZMissile shot = null;
            for (int i = 0; i < 600 && shot == null && !shooter.Destroyed && !target.Destroyed; i++)
            {
                world.Tick(ZConst.TICK);
                shot = world.PendingMissiles.Concat(world.QueuedMissiles).FirstOrDefault(m => m.attackerId == shooter.Id && m.targetId == target.Id);
                world.Events.Clear();
            }
            if (shot == null) return new Sample(0, target.MaxHealth, 0, 0, 0, 0, 0, 0, world.Time, 0);
            bool dodged = target.Waypoints.Any(wp => wp.mode == WpMode.Dodge);
            if (scenario.Mode == "blast")
            {
                hp = target.Health;
                shot.x = target.CenterX + scenario.Distance;
                shot.y = target.CenterY;
                world.CombatLabExplode(shot);
            }
            else
            {
                // Keep the initial dodge notification and the real movement ticks.
                // Resolve the selected projectile alone at its scheduled tick.
                hp = target.Health;
                world.CombatLabClearProjectiles();
                while (world.Time < shot.explodeTime && !target.Destroyed)
                {
                    world.Tick(ZConst.TICK);
                    dodged |= target.Waypoints.Any(wp => wp.mode == WpMode.Dodge);
                    world.CombatLabClearProjectiles();
                    world.Events.Clear();
                }
                if (!target.Destroyed) world.CombatLabExplode(shot);
            }
            int lost = hp - target.Health;
            return new Sample(lost, target.MaxHealth, 0, shot.radius, 1, lost > 0 ? 1 : 0,
                scenario.Mode != "blast" && dodged ? 1 : 0, 0, world.Time, 0);
        }

        int shots = 0;
        while (world.Time < 45 && !shooter.Destroyed && !target.Destroyed)
        {
            world.Tick(ZConst.TICK);
            shots += world.Events.Count(e => e.type == ZEventType.MissileFired && e.objId == shooter.Id && e.otherId == target.Id);
            world.Events.Clear();
        }
        int outcome = shooter.Destroyed == target.Destroyed ? 0 : target.Destroyed ? 1 : -1;
        return new Sample(hp - target.Health, target.MaxHealth, shooterHp - shooter.Health, 0, shots, 0, 0, outcome, world.Time, 0);
    }
}
