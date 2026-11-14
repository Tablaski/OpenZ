// 1.2 item 6, View/OzUnitEditor.cs (the unit editor's playground): the cells of the four stats sets and the OpenZ set read LIVE from the
// game's own rule tables (a fresh parse of OpenZ/default_settings: the same ZUnitSettings rows the Sim builds its units from) through the
// game's own rule functions, never from a CSV. It also says how each value reaches the Sim (a row field, a rule value, a value set on
// the unit itself) or which rule of the game fixes it. Sim only, no UnityEngine: the kit's headless check compiles this file with the
// Sim folder (k6_unit_editor/tools/p3_headless.py).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using OpenZ.Sim;

namespace OpenZ
{
    /// <summary></summary>
    public enum UeSet { OpenZ = 0, ZodEngine = 1, ZWiki = 2, Custom = 3 }

    /// <summary>The 14 stats, display order (build time first, owner 6e).</summary>
    public enum UeStat { Build = 0, Squad = 1, Health = 2, Damage = 3, VsGrunt = 4, Reload = 5, Hit = 6, Range = 7, Blast = 8, Shell = 9, Snipe = 10, Speed = 11, Run = 12, Dps = 13 }

    /// <summary>What a cell holds: a number, "n/a" (does not apply), "no roll" (shells and rockets), "--" (the source does not list it),
    /// "--" (the unit is not in that set).</summary>
    public enum UeCellKind { Number = 0, NotApplicable = 1, NoRoll = 2, NotListed = 3, NotInSet = 4 }

    public struct UeCell
    {
        public UeCellKind kind;
        public double value;
        public bool HasNumber => kind == UeCellKind.Number;
        public static UeCell Num(double v) => new UeCell { kind = UeCellKind.Number, value = v };
        public static UeCell Of(UeCellKind k) => new UeCell { kind = k, value = double.NaN };
    }

    /// <summary>How a Custom value reaches the playground's Sim.</summary>
    public enum UeRoute
    {
        None = 0,      // nothing to edit: n/a, no roll, DPS (worked out from damage, reload and hit chance)
        Row = 1,       // a field of the playground world's own rule-table row (World.S), changed in place
        Sniper = 2,    // the 1.2 sniper balance values (ZWorld.SniperHitChance, ZWorld.SniperReload) while that rule is on
        Electro = 3,   // the Electrobot's bolt cycle (ZElectro.CycleTicks)
        Pair = 4,      // the rifle-pair table (ZWorld.RiflePair: this robot's rifle on a grunt)
        Unit = 5,      // set on the unit itself after it is made (the shuttle's health and flight speed)
        Fixed = 6,     // follows from other values by a rule of the game (shown, not edited)
    }

    /// <summary>The rule switches and rule values the stats depend on, as the level start set them (ZGame.Awake), plus the values a
    /// set gives the playground for its rule values (the sniper balance, the bolt cycle, the rifle pairs).</summary>
    public sealed class UeRules
    {
        public bool sniperOn, pairOn, craneGun, apcCrew, rocketOn;
        public double sniperHit, sniperReload;
        public int electroCycle;
        public readonly Dictionary<(string attacker, string target), int> pairs = new Dictionary<(string attacker, string target), int>();

        /// <summary>The game's switches and rule values now.</summary>
        public static UeRules FromGame()
        {
            var r = new UeRules
            {
                sniperOn = ZWorld.SniperBalanceDeviation, pairOn = ZWorld.RiflePairDeviation, craneGun = ZWorld.CraneGunDeviation,
                apcCrew = ZWorld.ApcCrewDeviation, rocketOn = ZWorld.RocketSpeedDeviation, sniperHit = ZWorld.SniperHitChance, sniperReload = ZWorld.SniperReload,
                electroCycle = ZElectro.CycleTicks,
            };
            foreach (var kv in ZWorld.RiflePair) r.pairs[kv.Key] = kv.Value;
            return r;
        }

        /// <summary>The Zod engine's own rules: every OpenZ rule of the stats off (no sniper balance, no rifle pairs, no crane gun, the APC's
        /// lone driver x1, no slower rockets), the bolt cycle as the game has it (the Electrobot is not in Zod).</summary>
        public static UeRules ZodRules() => new UeRules { sniperHit = 0, sniperReload = 0, electroCycle = ZElectro.CycleTicks };

        public UeRules Clone()
        {
            var r = new UeRules
            {
                sniperOn = sniperOn, pairOn = pairOn, craneGun = craneGun, apcCrew = apcCrew, rocketOn = rocketOn, sniperHit = sniperHit,
                sniperReload = sniperReload, electroCycle = electroCycle,
            };
            foreach (var kv in pairs) r.pairs[kv.Key] = kv.Value;
            return r;
        }

        /// <summary>A pair table as one text ("grunt>grunt=125;..."), sorted: the statics guard holds the game's table by it.</summary>
        public static string PairText(Dictionary<(string attacker, string target), int> d)
        {
            var keys = new List<string>();
            foreach (var kv in d) keys.Add(kv.Key.attacker + ">" + kv.Key.target + "=" + kv.Value.ToString(CultureInfo.InvariantCulture));
            keys.Sort(StringComparer.Ordinal);
            return string.Join(";", keys);
        }

        /// <summary>Puts the pairs of such a text back into a table (the table is cleared first).</summary>
        public static void SetPairs(Dictionary<(string attacker, string target), int> d, string text)
        {
            d.Clear();
            if (string.IsNullOrEmpty(text)) return;
            foreach (var part in text.Split(';'))
            {
                int gt = part.IndexOf('>'), eq = part.IndexOf('=');
                if (gt <= 0 || eq <= gt) continue;
                if (int.TryParse(part.Substring(eq + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v))
                    d[(part.Substring(0, gt), part.Substring(gt + 1, eq - gt - 1))] = v;
            }
        }
    }

    /// <summary>One stat's names, unit, step and limits (DESIGN_SPEC 6.1).</summary>
    public sealed class UeStatDef
    {
        public UeStat stat; public string key, full, shortName, head, unit, unitLong, longText, meaning; public double step, min, max;
    }

    /// <summary>The OpenZ set, live, and the rules that decide how every value reaches the Sim.</summary>
    public static class OzUeLiveStats
    {
        public const int StatCount = 14;
        public const string Heroes = "heroes", Shuttle = ZWorld.ShuttleType;

        /// <summary>The 22 units in the game's build-menu order (robots, vehicles, guns), then the heroes, EvilZod and the shuttle.</summary>
        public static readonly string[] Units =
        {
            "grunt", "psycho", "sniper", "tough", "pyro", "laser", "electrobot", "jeep", "light", "medium", ZMTank.Type, "heavy", "apc",   // DV145: + the medium tank
            "missile_launcher", "crane", "gatling", "gun", "howitzer", "missile_cannon", Heroes, ZEvilZod.Type, Shuttle,
        };

        public static readonly UeStatDef[] Defs =
        {
            D(UeStat.Build, "build", "Build time", "Build", "BUILD", "s", "s", "Base build time (all flags halve it, as in ZodEngine)", "Base build time: lower is faster", 1, 1, 1200),
            D(UeStat.Squad, "squad", "Squad", "Squad", "SQUAD", "", "robots", "Robots per squad", "Robots in one squad", 1, 1, 8),
            D(UeStat.Health, "hp", "Health", "HP", "HEALTH", "HP", "HP", "Health per robot / vehicle", "Health of one robot or vehicle", 1, 1, 200000),
            D(UeStat.Damage, "dmg", "Damage", "Damage", "DAMAGE", "HP", "HP", "Damage per hit (explosives: at the blast centre)", "Damage per hit (explosives: at the centre)", 1, 0, 100000),
            D(UeStat.VsGrunt, "dmgGrunt", "vs Grunt", "vs Grunt", "VS GRUNT", "HP", "HP", "Damage per hit when the target is a grunt", "Damage per hit on a grunt", 1, 0, 100000),
            D(UeStat.Reload, "reload", "Reload", "Reload", "RELOAD", "s", "s", "Time between shots", "Seconds between shots: lower is faster", 0.01, 0.05, 60),
            D(UeStat.Hit, "hit", "Hit chance", "Hit", "HIT", "%", "%", "Hit chance (shells and rockets have no hit roll)", "Chance to hit; shells and rockets have no roll", 1, 0, 100),
            D(UeStat.Range, "range", "Range", "Range", "RANGE", "m", "m", "Weapon range (1 tile = 2 m)", "Weapon range in metres (1 tile = 2 m)", 0.5, 0, 80),
            D(UeStat.Blast, "blast", "Blast", "Blast", "BLAST", "m", "m", "Blast radius (0 = direct hit only)", "Blast radius; 0 = direct hit only", 0.25, 0, 30),
            D(UeStat.Shell, "shell", "Shell speed", "Shell", "SHELL", "m/s", "m/s", "Shell / rocket flight speed", "Flight speed of shells and rockets", 0.25, 1, 120),
            D(UeStat.Snipe, "snipe", "Snipe", "Snipe", "SNIPE", "%", "%", "Share of hits that go to the driver of an open vehicle or gun", "Share of hits that go to the driver", 1, 0, 100),
            D(UeStat.Speed, "speed", "Speed", "Speed", "SPEED", "m/s", "m/s", "Move speed", "Move speed", 0.05, 0, 20),
            D(UeStat.Run, "run", "Run time", "Run", "RUN", "s", "s", "Max run time at 1.8x speed", "Max run time at 1.8x speed", 0.1, 0, 60),
            D(UeStat.Dps, "dps", "DPS", "DPS", "DPS", "HP/s", "HP/s", "Damage per second of one unit standing still: damage / reload x hit chance", "Damage per second standing still", 1, 0, 1e9),
        };

        static UeStatDef D(UeStat s, string key, string full, string sh, string head, string unit, string unitLong, string lng, string meaning, double step, double min, double max) =>
            new UeStatDef { stat = s, key = key, full = full, shortName = sh, head = head, unit = unit, unitLong = unitLong, longText = lng, meaning = meaning, step = step, min = min, max = max };

        public static UeStatDef Def(UeStat s) => Defs[(int)s];

        public static bool TryKey(string key, out UeStat st)
        {
            foreach (var d in Defs) if (d.key == key && d.stat != UeStat.Dps) { st = d.stat; return true; }
            st = UeStat.Build; return false;
        }

        // ------------------------------------------------------------------ unit classes (the Sim's own lists and class flags)
        public static bool IsRobot(string id) => ZBuildLists.IsRobot(id);
        public static bool IsVehicle(string id) => Array.IndexOf(ZSettings.Vehicles, id) >= 0;
        public static bool IsGun(string id) => ZBuildLists.IsCannon(id);
        public static bool Known(string id) => Array.IndexOf(Units, id) >= 0;

        /// <summary>Shells, rockets and missiles: the Sim's DamageIsMissile class flag (ZWorld.SetClassFlags). No hit roll.</summary>
        public static bool Missile(string id) =>
            id == "tough" || id == "light" || id == "medium" || id == ZMTank.Type || id == "heavy" || id == "missile_launcher" || id == "gun" || id == "howitzer" || id == "missile_cannon";   // DV145 the medium tank (Sim/ZMTank.cs)

        /// <summary>The weapons that can hit drivers: the Sim's CanSnipe class flag (ZWorld.SetClassFlags).</summary>
        public static bool CanSnipe(string id) =>
            id == "grunt" || id == "psycho" || id == "sniper" || id == "pyro" || id == "laser" || id == "jeep" || id == "gatling";

        /// <summary>The robots whose rifle hits go through the rifle-pair table (hitscan robots; ZObject.BaseHitDamage).</summary>
        public static bool Hitscan(string id) => id == "grunt" || id == "psycho" || id == "sniper" || id == "pyro" || id == "laser";

        public static double M(int px) => px * (double)ZConst.PX_TO_M;
        static double Pct(double frac) => Math.Round(frac * 100.0, 6);

        // ------------------------------------------------------------------ the cells
        /// <summary>Every stat of one unit under these rule-table rows and rule values, as the game uses them in play.</summary>
        public static UeCell[] Cells(ZSettings s, UeRules r, string id)
        {
            var c = new UeCell[StatCount];
            for (int i = 0; i < StatCount; i++) c[i] = UeCell.Of(UeCellKind.NotApplicable);
            if (s == null || r == null || id == null) return c;
            if (id == Heroes) HeroCells(s, r, c);
            else if (id == Shuttle) ShuttleCells(s, c);
            else
            {
                var u = s.Unit(id);
                if (u == null) return c;
                c[(int)UeStat.Build] = UeCell.Num(u.build_time);
                c[(int)UeStat.Squad] = UeCell.Num(IsRobot(id) ? Math.Max(1, u.group_amount) : 1);
                c[(int)UeStat.Health] = UeCell.Num(u.MaxHealth);
                c[(int)UeStat.Speed] = UeCell.Num(M(u.move_speed));                                   // guns: 0 (the game's CensorSettings)
                c[(int)UeStat.Run] = IsGun(id) ? UeCell.Of(UeCellKind.NotApplicable) : UeCell.Num(u.max_run_time);
                if (id == "crane") CraneCells(s, r, c);
                else if (id == "apc") ApcCells(s, r, c);
                else WeaponCells(id, u, r, c);
            }
            if (id == ZEvilZod.Type) c[(int)UeStat.Build] = UeCell.Of(UeCellKind.NotApplicable);                   // DV161: he is never built (Sim/ZEvilZod.cs)
            c[(int)UeStat.Dps] = DpsCell(c[(int)UeStat.Damage], c[(int)UeStat.Reload], c[(int)UeStat.Hit]);
            return c;
        }

        static void WeaponCells(string id, ZUnitSettings u, UeRules r, UeCell[] c)
        {
            bool missile = Missile(id), sniper = id == "sniper" && r.sniperOn;
            c[(int)UeStat.Damage] = UeCell.Num(u.Damage);
            c[(int)UeStat.VsGrunt] = UeCell.Num(VsGrunt(id, u.Damage, r));
            c[(int)UeStat.Reload] = UeCell.Num(id == ZElectro.Type ? r.electroCycle * ZConst.TICK : sniper ? r.sniperReload : u.attack_speed);
            c[(int)UeStat.Hit] = missile ? UeCell.Of(UeCellKind.NoRoll) : UeCell.Num(Pct(sniper ? r.sniperHit : u.attack_damage_chance));
            c[(int)UeStat.Range] = UeCell.Num(M(u.attack_radius));
            c[(int)UeStat.Blast] = UeCell.Num(missile ? M(u.attack_damage_radius) : 0);
            c[(int)UeStat.Shell] = missile ? UeCell.Num(M(Rocket(id, u.attack_missile_speed, r))) : UeCell.Of(UeCellKind.NotApplicable);
            c[(int)UeStat.Snipe] = CanSnipe(id) ? UeCell.Num(Pct(u.attack_snipe_chance)) : UeCell.Of(UeCellKind.NotApplicable);
        }

        /// <summary>The rocket speed in play (px/s): ZWorld.RocketSpeed with the switch of these rules (x0.88 for the Tough and the missile
        /// cannon while it is on, rounded as the game rounds).</summary>
        public static int Rocket(string id, int px, UeRules r) =>
            r.rocketOn && px > 0 && (id == "tough" || id == "missile_cannon") ? (int)Math.Round(px * ZWorld.RocketSpeedFactor) : px;

        /// <summary>The HP one hit of this unit removes from a grunt: the rifle pair when it is higher than the damage (ZObject.BaseHitDamage).</summary>
        public static int VsGrunt(string id, int damage, UeRules r) =>
            Hitscan(id) && r.pairOn && r.pairs.TryGetValue((id, "grunt"), out int p) && p > damage ? p : damage;

        /// <summary>The crane's driver fires back with a grunt squad's rifle while the crane-gun rule is on (ZObject.CraneGunStep).</summary>
        static void CraneCells(ZSettings s, UeRules r, UeCell[] c)
        {
            var g = s.Unit("grunt");
            if (!r.craneGun || g == null) return;
            int dmg = g.Damage * ZWorld.CrewRifleMul;
            c[(int)UeStat.Damage] = UeCell.Num(dmg); c[(int)UeStat.VsGrunt] = UeCell.Num(dmg);
            c[(int)UeStat.Reload] = UeCell.Num(g.attack_speed); c[(int)UeStat.Hit] = UeCell.Num(Pct(g.attack_damage_chance));
            c[(int)UeStat.Range] = UeCell.Num(M(g.attack_radius)); c[(int)UeStat.Blast] = UeCell.Num(0);
        }

        /// <summary>The APC fires the weapon of its crew (ZObject.ResetDamageInfo): here its lone grunt driver, x3 with the crew rule.</summary>
        static void ApcCells(ZSettings s, UeRules r, UeCell[] c)
        {
            var g = s.Unit("grunt");
            if (g == null) return;
            int dmg = g.Damage * (r.apcCrew ? ZWorld.CrewRifleMul : 1);
            c[(int)UeStat.Damage] = UeCell.Num(dmg); c[(int)UeStat.VsGrunt] = UeCell.Num(dmg);
            c[(int)UeStat.Reload] = UeCell.Num(g.attack_speed); c[(int)UeStat.Hit] = UeCell.Num(Pct(g.attack_damage_chance));
            c[(int)UeStat.Range] = UeCell.Num(M(g.attack_radius)); c[(int)UeStat.Blast] = UeCell.Num(0);
        }

        /// <summary>Alan and Brad: two grunts with the hero squad's own rules (Sim/ZHeroSquad*.cs: health x3 and the boosts, the strong
        /// shot, the slower trigger, each member's own range; the longer range of the two is shown).</summary>
        static void HeroCells(ZSettings s, UeRules r, UeCell[] c)
        {
            var k = ZHeroSquads.Get(ZHeroSquads.AlanBrad);
            var g = k != null ? s.Unit(k.BaseType) : null;
            if (g == null) return;
            int mul = Math.Max(1, k.DamageMul);
            c[(int)UeStat.Squad] = UeCell.Num(k.Members.Length);
            c[(int)UeStat.Health] = UeCell.Num(ZHeroBoost.Health(g.MaxHealth * Math.Max(1, k.HealthMul), k));
            c[(int)UeStat.Damage] = UeCell.Num(ZHeroBoost.Damage(g.Damage * mul, k));
            c[(int)UeStat.VsGrunt] = UeCell.Num(ZHeroBoost.Damage(VsGrunt(k.BaseType, g.Damage, r) * mul, k));
            c[(int)UeStat.Reload] = UeCell.Num(g.attack_speed * Math.Max(0.1, k.CycleMul));
            c[(int)UeStat.Hit] = UeCell.Num(Pct(g.attack_damage_chance));
            int radius = 0;
            for (int m = 0; m < k.Members.Length; m++) radius = Math.Max(radius, ZHeroBoost.Range(ZHeroRange.Radius(g.attack_radius, k, m), k));
            c[(int)UeStat.Range] = UeCell.Num(M(radius));
            c[(int)UeStat.Blast] = UeCell.Num(0);
            c[(int)UeStat.Snipe] = UeCell.Num(Pct(g.attack_snipe_chance));
            c[(int)UeStat.Speed] = UeCell.Num(M(g.move_speed));
            c[(int)UeStat.Run] = UeCell.Num(g.max_run_time);
        }

        /// <summary>The shuttle: the light tank's hit points by the shuttle rule, the jeep's speed x the shuttle factor; no weapon.</summary>
        static void ShuttleCells(ZSettings s, UeCell[] c)
        {
            var light = s.Unit("light"); var jeep = s.Unit("jeep");
            c[(int)UeStat.Squad] = UeCell.Num(1);
            c[(int)UeStat.Health] = UeCell.Num(ZWorld.ShuttleHpFrom(light != null ? light.MaxHealth : ZWorld.ShuttleBaseHp));
            c[(int)UeStat.Speed] = UeCell.Num(M((int)Math.Round((jeep != null ? jeep.move_speed : 17) * ZWorld.ShuttleJeepFactor)));   // ZWorld.CreatePlayableShuttle
        }

        /// <summary>DPS = damage / reload x hit chance (1 for shells and rockets; 0.7 when the hit chance is not listed); n/a without a weapon.</summary>
        public static UeCell DpsCell(UeCell dmg, UeCell reload, UeCell hit)
        {
            if (!dmg.HasNumber || !reload.HasNumber || reload.value <= 0) return UeCell.Of(UeCellKind.NotApplicable);
            double f = hit.kind == UeCellKind.NoRoll ? 1.0 : hit.HasNumber ? hit.value / 100.0 : 0.7;
            return UeCell.Num(dmg.value / reload.value * f);
        }

        /// <summary>The whole live table: unit id -> 14 cells.</summary>
        public static Dictionary<string, UeCell[]> Table(ZSettings s, UeRules r)
        {
            var t = new Dictionary<string, UeCell[]>();
            foreach (var id in Units) t[id] = Cells(s, r, id);
            return t;
        }

        // ------------------------------------------------------------------ routes: how an edited value reaches the Sim
        public const string RuleEvilZod = "EvilZod's numbers are his own";
        public const string RuleHero = "Alan and Brad are two grunts: these values follow the grunt's, with the hero squad's own rules";
        public const string RuleOneShuttle = "one shuttle per drop";
        public const string RuleSingle = "one vehicle or gun per drop";
        public const string RuleGunsStill = "guns do not move";
        public const string RuleDirect = "a direct-fire weapon hits one target only (no blast)";
        public const string RuleVehicleGrunt = "vehicles and guns hit a grunt with their normal damage";
        public const string RuleRocketGrunt = "a rocket hits a grunt with its normal damage";
        public const string RuleBoltGrunt = "the bolt hits a grunt with its normal damage";
        public const string RuleBoltHit = "the bolt never misses";
        public const string RulePairsOff = "the rifle pair rule is off in this game";
        public const string RuleApc = "the APC fires the weapon of its crew (here its lone grunt driver)";
        public const string RuleCrane = "the crane's driver fires a grunt rifle x3: it follows the grunt's values";

        /// <summary>How a Custom value of this unit and stat reaches the Sim; rule = the words of the rule that fixes it (Fixed only).</summary>
        public static UeRoute Route(string id, UeStat st, UeRules r, out string rule)
        {
            rule = null;
            if (st == UeStat.Dps || id == null || r == null) return UeRoute.None;
            if (id == ZEvilZod.Type)
            {
                if (st == UeStat.Build || st == UeStat.Shell) return UeRoute.None;
                rule = RuleEvilZod; return UeRoute.Fixed;                                                     // DV161: no Custom value reaches him (Sim/ZEvilZod.cs)
            }
            if (id == Heroes)
            {
                if (st == UeStat.Build || st == UeStat.Shell) return UeRoute.None;
                rule = RuleHero; return UeRoute.Fixed;
            }
            if (id == Shuttle)
            {
                if (st == UeStat.Health || st == UeStat.Speed) return UeRoute.Unit;
                if (st == UeStat.Squad) { rule = RuleOneShuttle; return UeRoute.Fixed; }
                return UeRoute.None;
            }
            if (IsRobot(id)) return RobotRoute(id, st, r, out rule);
            bool gun = IsGun(id);
            if (!gun && !IsVehicle(id)) return UeRoute.None;
            switch (st)
            {
                case UeStat.Build: case UeStat.Health: return UeRoute.Row;
                case UeStat.Squad: rule = RuleSingle; return UeRoute.Fixed;
                case UeStat.Speed: if (gun) { rule = RuleGunsStill; return UeRoute.Fixed; } return UeRoute.Row;
                case UeStat.Run: return gun ? UeRoute.None : UeRoute.Row;
            }
            if (id == "crane")
            {
                if (st == UeStat.Shell || st == UeStat.Snipe || !r.craneGun) return UeRoute.None;
                rule = RuleCrane; return UeRoute.Fixed;
            }
            if (id == "apc")
            {
                if (st == UeStat.Shell || st == UeStat.Snipe) return UeRoute.None;
                rule = RuleApc; return UeRoute.Fixed;
            }
            bool missile = Missile(id);
            switch (st)
            {
                case UeStat.Damage: case UeStat.Reload: case UeStat.Range: return UeRoute.Row;
                case UeStat.VsGrunt: rule = RuleVehicleGrunt; return UeRoute.Fixed;
                case UeStat.Hit: return missile ? UeRoute.None : UeRoute.Row;
                case UeStat.Blast: if (missile) return UeRoute.Row; rule = RuleDirect; return UeRoute.Fixed;
                case UeStat.Shell: return missile ? UeRoute.Row : UeRoute.None;
                case UeStat.Snipe: return CanSnipe(id) ? UeRoute.Row : UeRoute.None;
            }
            return UeRoute.None;
        }

        static UeRoute RobotRoute(string id, UeStat st, UeRules r, out string rule)
        {
            rule = null;
            bool electro = id == ZElectro.Type, missile = Missile(id), sniper = id == "sniper" && r.sniperOn;
            switch (st)
            {
                case UeStat.Build: case UeStat.Squad: case UeStat.Health: case UeStat.Damage: case UeStat.Range: case UeStat.Speed: case UeStat.Run:
                    return UeRoute.Row;
                case UeStat.VsGrunt:
                    if (Hitscan(id)) { if (r.pairOn) return UeRoute.Pair; rule = RulePairsOff; return UeRoute.Fixed; }
                    rule = electro ? RuleBoltGrunt : RuleRocketGrunt; return UeRoute.Fixed;
                case UeStat.Reload: return electro ? UeRoute.Electro : sniper ? UeRoute.Sniper : UeRoute.Row;
                case UeStat.Hit:
                    if (missile) return UeRoute.None;
                    if (electro) { rule = RuleBoltHit; return UeRoute.Fixed; }
                    return sniper ? UeRoute.Sniper : UeRoute.Row;
                case UeStat.Blast: if (missile) return UeRoute.Row; rule = RuleDirect; return UeRoute.Fixed;
                case UeStat.Shell: return missile ? UeRoute.Row : UeRoute.None;
                case UeStat.Snipe: return CanSnipe(id) ? UeRoute.Row : UeRoute.None;
            }
            return UeRoute.None;
        }

        public static bool Editable(UeRoute r) => r != UeRoute.None && r != UeRoute.Fixed;

        // ------------------------------------------------------------------ values
        /// <summary>A value clamped to the stat's limits and rounded to its step (whole numbers when the step is 1 or more, else three
        /// decimals). Distances and speeds go to the game's own resolution (one rule pixel = 0.125 m), so the value kept is the value
        /// the Sim uses. The Electrobot's cycle never goes under its windup.</summary>
        public static double Clamp(string id, UeStat st, double v)
        {
            var d = Def(st);
            double min = d.min;
            if (st == UeStat.Reload && id == ZElectro.Type) min = Math.Max(min, (ZElectro.WindupTicks + 1) * ZConst.TICK);
            if (double.IsNaN(v) || double.IsInfinity(v)) v = min;
            v = Math.Max(min, Math.Min(d.max, v));
            if (Quantum(st) > 0) v = Math.Max(min, Math.Round(v / Quantum(st), MidpointRounding.AwayFromZero) * Quantum(st));
            return d.step >= 1 ? Math.Round(v, MidpointRounding.AwayFromZero) : Math.Round(v, 3, MidpointRounding.AwayFromZero);
        }

        /// <summary>A value kept inside the stat's limits, not rounded (values copied from a set or read back from a file stay exact).</summary>
        public static double Limit(string id, UeStat st, double v)
        {
            var d = Def(st);
            double min = d.min;
            if (st == UeStat.Reload && id == ZElectro.Type) min = Math.Max(min, (ZElectro.WindupTicks + 1) * ZConst.TICK);
            return double.IsNaN(v) || double.IsInfinity(v) ? min : Math.Max(min, Math.Min(d.max, v));
        }

        /// <summary>The game's resolution of a stat in player units (0 = none): one rule pixel for ranges, blasts and speeds.</summary>
        public static double Quantum(UeStat st) =>
            st == UeStat.Range || st == UeStat.Blast || st == UeStat.Shell || st == UeStat.Speed ? (double)ZConst.PX_TO_M : 0.0;

        /// <summary>One wheel / arrow step of an edited value: 1 % (Shift: 10 %), at least the stat's step and the game's resolution.</summary>
        public static double StepOf(UeStat st, double v, bool big)
        {
            var d = Def(st);
            double step = Math.Max(Math.Abs(v) * (big ? 0.1 : 0.01), Math.Max(d.step, Quantum(st)));
            return d.step >= 1 ? Math.Max(1.0, Math.Round(step, MidpointRounding.AwayFromZero)) : step;
        }

        /// <summary>Two values are the same when they differ by less than a millionth (doubles from different sums).</summary>
        public static bool Same(double a, double b) => Math.Abs(a - b) <= 1e-6 * Math.Max(1.0, Math.Max(Math.Abs(a), Math.Abs(b)));

        /// <summary>Whole numbers for build, squad, health, damage, vs grunt, hit, snipe and DPS; run time to one decimal (its 0.1 s step, as
        /// prototype C shows it: 11.1 s, not 11.143); the others up to three decimals, trailing zeros cut (DESIGN_SPEC 6.1).</summary>
        public static string Format(UeStat st, double v)
        {
            if (double.IsNaN(v)) return "--";
            switch (st)
            {
                case UeStat.Build: case UeStat.Squad: case UeStat.Health: case UeStat.Damage: case UeStat.VsGrunt: case UeStat.Hit: case UeStat.Snipe: case UeStat.Dps:
                    return Math.Round(v, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture);
                case UeStat.Run:
                    return Math.Round(v, 1, MidpointRounding.AwayFromZero).ToString("0.#", CultureInfo.InvariantCulture);
            }
            return Math.Round(v, 3, MidpointRounding.AwayFromZero).ToString("0.###", CultureInfo.InvariantCulture);
        }

        public static string Text(UeStat st, UeCell c)
        {
            switch (c.kind)
            {
                case UeCellKind.Number: return Format(st, c.value);
                case UeCellKind.NotApplicable: return "n/a";
                case UeCellKind.NoRoll: return "no roll";
            }
            return "--";
        }

        /// <summary>Lower is better for these (the +/- chips: green when the change is better for the unit).</summary>
        public static bool LowerBetter(UeStat st) => st == UeStat.Reload || st == UeStat.Build;

        /// <summary>v against b in whole per cent; int.MinValue when not comparable or equal.</summary>
        public static int PctOf(double v, double b)
        {
            if (double.IsNaN(v) || double.IsNaN(b) || Same(v, b) || Math.Abs(b) < 1e-12) return int.MinValue;
            return (int)Math.Round((v - b) / Math.Abs(b) * 100.0, MidpointRounding.AwayFromZero);
        }

        /// <summary>A short text of a whole table (checks and logs): "grunt build 72 squad 3 ...".</summary>
        public static string Dump(Dictionary<string, UeCell[]> t)
        {
            var sb = new StringBuilder();
            foreach (var id in Units)
            {
                if (!t.TryGetValue(id, out var c)) continue;
                sb.Append(id);
                for (int i = 0; i < StatCount; i++) sb.Append(' ').Append(Defs[i].key).Append(' ').Append(Text((UeStat)i, c[i]));
                sb.Append('\n');
            }
            return sb.ToString();
        }
    }
}
