// 1.2 item 6, View/OzUnitEditor.cs: the stats set in use DRIVES the playground's fight (DESIGN_SPEC 6.5, ARCHITECTURE D9). A set becomes
// a plan: the playground world's own rule-table rows with the set's values written in (a fresh parse of the game's settings, so every
// value the set does not change stays exactly as the game parsed it) and the rule values it needs (the sniper balance, the bolt cycle,
// the rifle pairs). Applying the plan changes the rows of World.S IN PLACE (every unit's U keeps its reference), sets the rule values
// and copies the new numbers into every live unit the way the game makes a unit (health keeps its fraction). Only the playground's
// world is touched; the rule values are statics, held and restored by the editor (OzUeStatics, see OzUeSets.HoldRuleValues).
// Sim only, no UnityEngine: the headless check runs it on a headless world (tools/p3_headless.py).
using System;
using System.Collections.Generic;
using OpenZ.Sim;

namespace OpenZ
{
    public static class OzUeStatsApply
    {
        public sealed class Plan
        {
            public ZSettings rows;                      // a fresh parse with the set's values written in
            public UeRules rules;                       // the rule values the set gives
            public double shuttleHp, shuttleSpeed;      // set on every shuttle after it is made (HP, m/s)
            public Dictionary<string, UeCell[]> table;  // what the playground uses with this plan (InPlay)
            public int rowWrites, ruleWrites;
        }

        /// <summary>The plan of a set. value(id, stat) = the set's own value for an editable cell, NaN when it has none (then the OpenZ
        /// value is used: values a set does not have fall back to OpenZ).</summary>
        public static Plan Make(string settingsText, UeRules gameRules, Dictionary<string, UeCell[]> openz, Func<string, UeStat, double> value)
        {
            var p = new Plan { rows = ZSettings.Parse(settingsText), rules = gameRules.Clone() };
            foreach (var id in OzUeLiveStats.Units)
            {
                if (!openz.TryGetValue(id, out var oz)) continue;
                for (int i = 0; i < (int)UeStat.Dps; i++)
                {
                    var st = (UeStat)i;
                    var route = OzUeLiveStats.Route(id, st, gameRules, out _);
                    if (!OzUeLiveStats.Editable(route) || !oz[i].HasNumber) continue;
                    double v = value != null ? value(id, st) : double.NaN;
                    v = OzUeLiveStats.Limit(id, st, double.IsNaN(v) ? oz[i].value : v);              // inside the limits, not rounded: a set's own value stays exact
                    if (route == UeRoute.Unit)
                    {
                        if (st == UeStat.Health) p.shuttleHp = v; else p.shuttleSpeed = v;
                        continue;
                    }
                    if (route == UeRoute.Row && OzUeLiveStats.Same(v, oz[i].value)) continue;   // the game's own row value stays as parsed
                    Write(p, id, st, route, v);
                }
            }
            if (openz.TryGetValue(OzUeLiveStats.Shuttle, out var sh))
            {
                if (p.shuttleHp <= 0 && sh[(int)UeStat.Health].HasNumber) p.shuttleHp = sh[(int)UeStat.Health].value;
                if (p.shuttleSpeed <= 0 && sh[(int)UeStat.Speed].HasNumber) p.shuttleSpeed = sh[(int)UeStat.Speed].value;
            }
            p.table = OzUeLiveStats.Table(p.rows, p.rules);
            if (p.table.TryGetValue(OzUeLiveStats.Shuttle, out var c))
            {
                c[(int)UeStat.Health] = UeCell.Num(Math.Round(p.shuttleHp, MidpointRounding.AwayFromZero));
                c[(int)UeStat.Speed] = UeCell.Num(ShuttleSpeedPx(p.shuttleSpeed) * (double)ZConst.PX_TO_M);
            }
            return p;
        }

        public static int ShuttleSpeedPx(double mps) => Math.Max(0, (int)Math.Round(mps / ZConst.PX_TO_M, MidpointRounding.AwayFromZero));

        static void Write(Plan p, string id, UeStat st, UeRoute route, double v)
        {
            switch (route)
            {
                case UeRoute.Row:
                {
                    var u = p.rows.Unit(id);
                    if (u == null) return;
                    WriteRow(u, id, st, v);
                    p.rowWrites++;
                    return;
                }
                case UeRoute.Sniper:
                    if (st == UeStat.Hit) p.rules.sniperHit = v / 100.0; else p.rules.sniperReload = v;
                    p.ruleWrites++;
                    return;
                case UeRoute.Electro:
                    p.rules.electroCycle = Math.Max(ZElectro.WindupTicks + 1, (int)Math.Round(v / ZConst.TICK, MidpointRounding.AwayFromZero));
                    p.ruleWrites++;
                    return;
                case UeRoute.Pair:
                {
                    var u = p.rows.Unit(id);
                    int dmg = u != null ? u.Damage : 0, g = (int)Math.Round(v, MidpointRounding.AwayFromZero);
                    int dv212 = p.rules.smallArmsOn ? ZWorld.SmallArmsVsGrunt(id, 0) : 0; if (dv212 > 0 && g <= dv212) { p.ruleWrites++; return; }   // 1.5 DV212 (kit small_arms_balance_r8): at or under DV212's own value the sim hits with DV212's: the game's pair stays as it is
                    if (g > dmg) p.rules.pairs[(id, "grunt")] = g; else p.rules.pairs.Remove((id, "grunt"));
                    p.ruleWrites++;
                    return;
                }
            }
        }

        /// <summary>A value in player units into the row field the Sim reads (DESIGN_SPEC 6.5). Health and damage get a half point
        /// so the game's truncation (MaxHealth = (int)(health x 10000)) gives exactly the whole number shown.</summary>
        public static void WriteRow(ZUnitSettings u, string id, UeStat st, double v)
        {
            const double H = ZConst.MAX_UNIT_HEALTH;
            int R(double x) => (int)Math.Round(x, MidpointRounding.AwayFromZero);
            double px = 1.0 / ZConst.PX_TO_M;
            switch (st)
            {
                case UeStat.Build: u.build_time = R(v); break;
                case UeStat.Squad: u.group_amount = R(v); break;
                case UeStat.Health: u.health = (R(v) + 0.5) / H; break;
                case UeStat.Damage: u.attack_damage = (R(v) + 0.5) / H; break;
                case UeStat.Reload: u.attack_speed = v; break;
                case UeStat.Hit: u.attack_damage_chance = Math.Max(0, Math.Min(1, v / 100.0)); break;
                case UeStat.Range: u.attack_radius = R(v * px); break;
                case UeStat.Blast: u.attack_damage_radius = R(v * px); break;
                case UeStat.Shell: u.attack_missile_speed = RocketRow(id, R(v * px)); break;
                case UeStat.Snipe: u.attack_snipe_chance = Math.Max(0, Math.Min(1, v / 100.0)); break;
                case UeStat.Speed: u.move_speed = R(v * px); break;
                case UeStat.Run: u.max_run_time = v; break;
            }
        }

        /// <summary>The row value whose in-play rocket speed (ZWorld.RocketSpeed: x0.88 for the Tough and the missile cannon while that
        /// rule is on) is the wanted speed; the nearest one when no whole row value gives it exactly.</summary>
        public static int RocketRow(string id, int targetPx)
        {
            if (ZWorld.RocketSpeed(id, targetPx) == targetPx) return targetPx;
            int best = targetPx, bestErr = int.MaxValue, mid = (int)Math.Round(targetPx / ZWorld.RocketSpeedFactor);
            for (int row = Math.Max(1, mid - 4); row <= mid + 4; row++)
            {
                int err = Math.Abs(ZWorld.RocketSpeed(id, row) - targetPx);
                if (err < bestErr) { bestErr = err; best = row; }
            }
            return best;
        }

        public static void CopyRow(ZUnitSettings from, ZUnitSettings to)
        {
            to.group_amount = from.group_amount; to.move_speed = from.move_speed; to.attack_radius = from.attack_radius;
            to.attack_damage_radius = from.attack_damage_radius; to.attack_missile_speed = from.attack_missile_speed; to.build_time = from.build_time;
            to.attack_damage = from.attack_damage; to.attack_damage_chance = from.attack_damage_chance; to.attack_speed = from.attack_speed;
            to.attack_snipe_chance = from.attack_snipe_chance; to.health = from.health; to.max_run_time = from.max_run_time;
        }

        /// <summary>The rule values of a plan into the game's statics (the editor holds the game's own values first).</summary>
        public static void SetRuleValues(UeRules r)
        {
            ZWorld.SniperHitChance = r.sniperHit; ZWorld.SniperReload = r.sniperReload;
            ZElectro.CycleTicks = r.electroCycle;
            ZWorld.RiflePair.Clear();
            foreach (var kv in r.pairs) ZWorld.RiflePair[kv.Key] = kv.Value;
        }

        /// <summary>Applies a plan to a world: rows in place, rule values, every live unit. Returns the units changed.</summary>
        public static int ApplyToWorld(ZWorld w, Plan p)
        {
            if (w == null || w.S == null || p == null) return 0;
            SetRuleValues(p.rules);
            foreach (var kv in p.rows.Units)
            {
                var dst = w.S.Unit(kv.Key);
                if (dst != null && !ReferenceEquals(dst, kv.Value)) CopyRow(kv.Value, dst);
            }
            int n = 0;
            foreach (var o in w.Objects) if (Recopy(w, o, p)) n++;
            return n;
        }

        /// <summary>One live unit takes its row's numbers again exactly as ZWorld.CreateObject gives them (plus the hero squad's own
        /// numbers as ZWorld.MakeHero sets them, the APC's crew weapon, the sniper balance, the shuttle's values); health and stamina keep
        /// their fraction.</summary>
        public static bool Recopy(ZWorld w, ZObject o, Plan p)
        {
            if (o == null || o.KillAt >= 0 || o.Destroyed) return false;
            if (o.IsShuttle)
            {
                if (p.shuttleHp > 0) SetMax(o, (int)Math.Round(p.shuttleHp, MidpointRounding.AwayFromZero));
                if (p.shuttleSpeed > 0) { o.MoveSpeed = ShuttleSpeedPx(p.shuttleSpeed); o.RealMoveSpeed = o.MoveSpeed; }
                return true;
            }
            if (o.Kind != ZKind.Robot && o.Kind != ZKind.Vehicle && o.Kind != ZKind.Cannon) return false;
            var u = o.U;
            if (u == null) return false;
            var k = o.Hero;
            SetMax(o, k != null ? ZHeroBoost.Health(u.MaxHealth * Math.Max(1, k.HealthMul), k) : u.MaxHealth);
            double st = o.MaxStamina;
            o.MaxStamina = u.max_run_time;
            o.Stamina = st > 1e-9 ? Math.Min(o.MaxStamina, o.Stamina * o.MaxStamina / st) : o.MaxStamina;
            o.MoveSpeed = u.move_speed;
            if (w.Map != null) o.InitRealMoveSpeed(w.Map);
            o.Damage = u.Damage; o.AttackRadius = u.attack_radius; o.DamageChance = u.attack_damage_chance;
            o.DamageRadius = u.attack_damage_radius; o.MissileSpeed = ZWorld.RocketSpeed(o.Type, u.attack_missile_speed); o.AttackSpeed = u.attack_speed;
            o.SnipeChance = u.attack_snipe_chance;
            if (ZWorld.SniperBalanceDeviation && o.Type == "sniper") { o.DamageChance = ZWorld.SniperHitChance; o.AttackSpeed = ZWorld.SniperReload; }
            if (k != null)
            {
                o.AttackSpeed = u.attack_speed * Math.Max(0.1, k.CycleMul);
                o.AttackRadius = ZHeroBoost.Range(ZHeroRange.Radius(u.attack_radius, k, o.HeroMember), k);
            }
            o.ResetDamageInfo();                                                                   // the APC: its crew's weapon (every other unit: nothing)
            return true;
        }

        static void SetMax(ZObject o, int max)
        {
            int old = o.MaxHealth;
            o.MaxHealth = max;
            if (old > 0 && o.Health > 0 && old != max)
                o.Health = Math.Max(1, Math.Min(max, (int)Math.Round((double)o.Health * max / old, MidpointRounding.AwayFromZero)));
            else if (o.Health > max) o.Health = max;
        }
    }
}
