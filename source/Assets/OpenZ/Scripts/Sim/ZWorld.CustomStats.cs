using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace OpenZ.Sim
{
    /// <summary>
    /// 1.2 item 60, deviation DV142: CUSTOM UNIT STATS in the levels. The unit stats the player keeps in the Unit editor (its Custom set,
    /// on this computer) can be played in a level: the battlefield screen's UNIT STATS selector (OPENZ | CUSTOM, shown only while a
    /// Custom profile with at least one change exists; UI/OpenZMainMenu.UnitStats.cs), armed by the player's own DEPLOY and read before
    /// tick 0 by View/OzLevelStats.cs. The values reach the rules through the one mechanism the Unit editor uses (View/OzUeStatsApply.cs:
    /// a fresh parse of the game's settings with the Custom values written in, the rule values a set gives, the shuttle's own values),
    /// here BEFORE the map's objects are made, so every unit of every team is made from the Custom rows exactly as the game makes a unit:
    /// the player's and the CPU's, neutral / empty vehicles and guns, the map's start squads, the head start, every unit built later,
    /// the hero squads (they follow the grunt's row), PLAY AS BLUE too.
    /// <list type="bullet">
    /// <item><see cref="ApplyCustomStats"/> (ZWorld.Create, right after the settings are parsed): each row of <see cref="CustomStats"/> is
    /// copied into this world's own row of that unit (World.S), so CreateObject, the squads' sizes, the drivers, the factories' build
    /// times and every later reading of the row use it.</item>
    /// <item><see cref="CustomShuttle"/> (ZWorld.CreatePlayableShuttle): the shuttle's hit points and flight speed are set on the ship
    /// itself, as the Unit editor sets them on its ships.</item>
    /// <item>The rule values a set may change (the sniper balance, the Electrobot's bolt cycle, the rifle pairs) are statics: the level
    /// start sets them and puts them back (View/OzLevelStatsPlan.cs).</item>
    /// </list>
    /// Values the player did not change stay OpenZ's (the rows are a fresh parse with only the changed values written in).
    /// Deterministic: the rows are set once, before the world is built, from the saved text (parsed with the invariant culture, whole
    /// numbers in the int fields); nothing is read during the match; no random draw, no clock. The same text gives the same match on
    /// every computer and on REPLAY (the level start keeps the text the DEPLOY read).
    /// Switch <see cref="CustomStatsDeviation"/>, static like the others: View/ZGame.cs sets it at every level start (off in 'Zod exact',
    /// off unless the player picked CUSTOM and his DEPLOY armed it); off by default, so headless worlds, the parity rows and the tests
    /// are unchanged. Off, or no rows given: nothing happens (OPENZ = the game's own numbers, tick for tick).
    /// </summary>
    public sealed partial class ZWorld
    {
        /// <summary>DV142 switch: worlds built while it is on take <see cref="CustomStats"/>. Set by View/ZGame.cs at every level start.</summary>
        public static bool CustomStatsDeviation = false;
        /// <summary>DV142: the Custom rows and shuttle values the next world is built with (null = none). Set by View/OzLevelStats.cs.</summary>
        public static ZCustomStats CustomStats;
        /// <summary>The Custom stats this world was built with; null = the game's own numbers.</summary>
        public ZCustomStats CustomStatsInUse { get; private set; }
        /// <summary>Checks: worlds built with Custom rows, shuttles given the Custom values (since the game started).</summary>
        public static int CustomStatsWorlds, CustomStatsShuttles;

        /// <summary>ZWorld.Create, right after the settings are parsed and before any object is made.</summary>
        void ApplyCustomStats()
        {
            var c = CustomStatsDeviation ? CustomStats : null;
            if (c == null || S == null) return;
            c.CopyInto(S);
            CustomStatsInUse = c;
            CustomStatsWorlds++;
        }

        /// <summary>ZWorld.CreatePlayableShuttle, after the ship has its rule values: the Custom set's own shuttle values (as the Unit
        /// editor sets them on a ship it makes; 0 = the rule's).</summary>
        void CustomShuttle(ZObject s)
        {
            var c = CustomStatsInUse;
            if (c == null || s == null) return;
            if (c.ShuttleHp > 0) s.MaxHealth = s.Health = c.ShuttleHp;
            if (c.ShuttleSpeedPx > 0) { s.MoveSpeed = c.ShuttleSpeedPx; s.RealMoveSpeed = s.MoveSpeed; }
            CustomStatsShuttles++;
        }

        /// <summary>The checks and the parity rows measure the game's own numbers whatever a Play session left: the switch off and no rows.</summary>
        public static CustomStatsHold CustomStatsOff() => new CustomStatsHold();
        public sealed class CustomStatsHold : IDisposable
        {
            readonly bool was; readonly ZCustomStats data;
            internal CustomStatsHold() { was = CustomStatsDeviation; data = CustomStats; CustomStatsDeviation = false; CustomStats = null; }
            public void Dispose() { CustomStatsDeviation = was; CustomStats = data; }
        }
    }

    /// <summary>DV142: the rows a world is built with instead of the parsed ones (each a whole row: every field of it), and the shuttle's
    /// own values. Made by View/OzLevelStatsPlan.cs from the Unit editor's plan of the Custom set.</summary>
    public sealed class ZCustomStats
    {
        /// <summary>The profile's name (logs and the checks).</summary>
        public string Name = "";
        /// <summary>Unit type -> the row to use (only the rows with a value that differs from the game's own parse).</summary>
        public readonly Dictionary<string, ZUnitSettings> Rows = new Dictionary<string, ZUnitSettings>();
        /// <summary>The shuttle's hit points and flight speed (rule pixels per second); 0 = the shuttle rule's own.</summary>
        public int ShuttleHp, ShuttleSpeedPx;

        /// <summary>Every row into the world's own row of the same unit (the world keeps its own row objects: every unit's U stays valid).</summary>
        public void CopyInto(ZSettings s)
        {
            if (s == null) return;
            foreach (var kv in Rows)
            {
                var dst = s.Unit(kv.Key);
                if (dst == null || kv.Value == null || ReferenceEquals(dst, kv.Value)) continue;
                Copy(kv.Value, dst);
            }
        }

        /// <summary>The twelve values of a unit's row (the fields View/OzUeStatsApply.cs writes and copies).</summary>
        public static void Copy(ZUnitSettings from, ZUnitSettings to)
        {
            to.group_amount = from.group_amount; to.move_speed = from.move_speed; to.attack_radius = from.attack_radius;
            to.attack_damage_radius = from.attack_damage_radius; to.attack_missile_speed = from.attack_missile_speed; to.build_time = from.build_time;
            to.attack_damage = from.attack_damage; to.attack_damage_chance = from.attack_damage_chance; to.attack_speed = from.attack_speed;
            to.attack_snipe_chance = from.attack_snipe_chance; to.health = from.health; to.max_run_time = from.max_run_time;
        }

        /// <summary>The same twelve values (exact).</summary>
        public static bool Same(ZUnitSettings a, ZUnitSettings b) =>
            a != null && b != null && a.group_amount == b.group_amount && a.move_speed == b.move_speed && a.attack_radius == b.attack_radius
            && a.attack_damage_radius == b.attack_damage_radius && a.attack_missile_speed == b.attack_missile_speed && a.build_time == b.build_time
            && a.attack_damage.Equals(b.attack_damage) && a.attack_damage_chance.Equals(b.attack_damage_chance) && a.attack_speed.Equals(b.attack_speed)
            && a.attack_snipe_chance.Equals(b.attack_snipe_chance) && a.health.Equals(b.health) && a.max_run_time.Equals(b.max_run_time);

        /// <summary>One line per row (sorted by unit) and the shuttle's values, exact (logs, the checks' fingerprints).</summary>
        public string Text()
        {
            var keys = new List<string>(Rows.Keys); keys.Sort(StringComparer.Ordinal);
            var sb = new StringBuilder();
            foreach (var k in keys)
            {
                var u = Rows[k]; if (u == null) continue;
                sb.Append(k).Append(": squad ").Append(u.group_amount).Append(", hp ").Append(u.MaxHealth).Append(", damage ").Append(u.Damage)
                  .Append(", reload ").Append(R(u.attack_speed)).Append(", hit ").Append(R(u.attack_damage_chance)).Append(", range ").Append(u.attack_radius)
                  .Append(", blast ").Append(u.attack_damage_radius).Append(", shell ").Append(u.attack_missile_speed).Append(", snipe ").Append(R(u.attack_snipe_chance))
                  .Append(", speed ").Append(u.move_speed).Append(", run ").Append(R(u.max_run_time)).Append(", build ").Append(u.build_time).Append('\n');
            }
            sb.Append("shuttle: hp ").Append(ShuttleHp).Append(", speed ").Append(ShuttleSpeedPx).Append(" px/s");
            return sb.ToString();
        }

        static string R(double v) => v.ToString("R", CultureInfo.InvariantCulture);
    }
}
