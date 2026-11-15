using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace OpenZ.Sim
{
    public enum ZBehaviorFamily { Tanks, Robots }
    public enum ZBehaviorKey
    {
        Enabled, Idle, Move, AttackOrder, Engaged, Retreat, ForceMove, Approach, Hold,
        StandFirm, Reaction, ReactionSpread, Chance, Cooldown, SquadCooldown, ShootFirst,
        ThreatShell, ThreatRocket, ThreatGrenade, NearbyThreats, VisibleOnly, MinDamage,
        Distance, MinDistance, MaxDistance, Direction, Forward, Backward, Sideways,
        Replan, Commit, MaxDuration, EndAfterBlast, MaxDeviation,
        Physical, Acceleration, Braking, ReverseDelay, ReverseSpeed, Reverse, TurnRate,
        TurnAcceleration, Pivot, TurnMoving, CornerSpeed, Lateral, SlipAngle,
        ImmediateTurn, HeadingHold, HeadingCommit, DeadZone, ShortTurn, DirectView,
        Boost, BoostSpeed, BoostChance, BoostDuration, Endurance, Drain, Recharge, MinStamina,
        DamagedBoost, PremiumBoost, FireDodge, FireBoost, Accuracy, Settle, KeepTarget,
        FireMoving, BreakForRetreat, Cohesion, RegroupDelay, CombatManoeuvres, ResumeAttack,
        TurnFirst, PivotSpeed, AlignmentTolerance, Count
    }

    /// <summary>One schema drives validation, simulation, the editor and exported presets. Zero acceleration means instant.</summary>
    public sealed class ZBehaviorDef
    {
        public readonly ZBehaviorKey Key;
        public readonly string Label, Group, Unit, Tip;
        public readonly double Min, Max, Native, Zod;
        public readonly string[] Choices;
        public bool Boolean => Choices != null && Choices.Length == 2 && Choices[0] == "OFF";
        public ZBehaviorDef(ZBehaviorKey key, string label, string group, string unit, double min, double max,
            double native, double zod, string tip, string[] choices = null)
        { Key = key; Label = label; Group = group; Unit = unit; Min = min; Max = max; Native = native; Zod = zod; Tip = tip; Choices = choices; }
        public double Clamp(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) throw new ArgumentException("Non-finite behavior value: " + Key);
            // Max describes the suggested UI scale, not a cap on custom numeric values.
            // Probabilities and discrete choices retain their mathematical bounds.
            value = Math.Max(Min, Choices != null || Key == ZBehaviorKey.Chance || Key == ZBehaviorKey.BoostChance ? Math.Min(Max, value) : value);
            return Choices == null ? value : Math.Round(value, MidpointRounding.AwayFromZero);
        }
        public string Format(double value) => Choices != null ? Choices[(int)Clamp(value)] :
            value.ToString("0.###", CultureInfo.InvariantCulture) + (Unit.Length == 0 ? "" : " " + Unit);
    }

    public static class ZBehaviorSchema
    {
        static readonly string[] Bool = { "OFF", "ON" };
        static ZBehaviorDef N(ZBehaviorKey k, string label, string group, string unit, double min, double max, double a, double b, string tip)
            => new ZBehaviorDef(k, label, group, unit, min, max, a, b, tip);
        static ZBehaviorDef B(ZBehaviorKey k, string label, string group, bool a, bool b, string tip)
            => new ZBehaviorDef(k, label, group, "", 0, 1, a ? 1 : 0, b ? 1 : 0, tip, Bool);
        public static readonly ZBehaviorDef[] All = {
            B(ZBehaviorKey.Enabled,"Dodging enabled","DODGING",true,true,"Allow this unit type to react to explosive projectiles."),
            B(ZBehaviorKey.Idle,"Dodge while idle","DODGING",true,true,"React without an active order."),
            B(ZBehaviorKey.Move,"Dodge during movement","DODGING",true,true,"Temporarily interrupt a normal move; resume it afterwards."),
            B(ZBehaviorKey.AttackOrder,"Dodge during attack orders","DODGING",false,true,"Allow a defensive step ahead of a named attack order. Robots still need dodge while engaged."),
            B(ZBehaviorKey.Engaged,"Dodge while engaged","DODGING",true,true,"React while a target is acquired, independently of the order."),
            B(ZBehaviorKey.Retreat,"Dodge during retreat","DODGING",false,true,"A move away from an acquired enemy counts as retreat. No new retreat command is needed."),
            B(ZBehaviorKey.ForceMove,"Dodge during forced movement","DODGING",false,false,"Allow interruption of a forced move. Door and repair transitions remain protected."),
            B(ZBehaviorKey.Approach,"Dodge during capture / entry approach","DODGING",false,true,"Only the approach can be interrupted; never a protected entry stage."),
            B(ZBehaviorKey.Hold,"Dodge while holding position","DODGING",true,true,"Allow a limited defensive step while holding position."),
            B(ZBehaviorKey.StandFirm,"Keep native stand-and-fire restrictions","DODGING",true,false,"Toughs, Snipers and Electrobots otherwise stand firm. Turn off to allow their defensive steps."),
            N(ZBehaviorKey.Reaction,"Reaction delay","DODGING","s",0,3,0,0,"Simulation seconds before starting; another shot never postpones the first reaction."),
            N(ZBehaviorKey.ReactionSpread,"Individual reaction variation","DODGING","s",0,2,0,0,"Deterministic variation added to the reaction delay."),
            N(ZBehaviorKey.Chance,"Reaction chance","DODGING","%",0,100,100,100,"Chance to attempt a dodge, not a guaranteed chance to avoid damage."),
            N(ZBehaviorKey.Cooldown,"Time between dodges","DODGING","s",0,30,0,0,"Minimum time between the start of two dodges."),
            B(ZBehaviorKey.SquadCooldown,"Share cooldown across squad","DODGING",false,false,"Any member's dodge starts the squad cooldown."),
            N(ZBehaviorKey.ShootFirst,"Shots required before another dodge","DODGING","shots",0,10,0,0,"Only while engaged. Counts this unit's shots since its last dodge."),
            B(ZBehaviorKey.ThreatShell,"React to shells","DODGING",true,true,"React to explosive shells. Instant bullets cannot be dodged after firing."),
            B(ZBehaviorKey.ThreatRocket,"React to rockets","DODGING",true,true,"React to rockets, including crew weapons."),
            B(ZBehaviorKey.ThreatGrenade,"React to grenades","DODGING",true,true,"React to timed grenades."),
            B(ZBehaviorKey.NearbyThreats,"React to nearby blast threats","DODGING",false,false,"Also alert units not directly targeted whose current position is inside the predicted blast."),
            B(ZBehaviorKey.VisibleOnly,"Require visible attacker","DODGING",false,false,"Ignore alerts from attackers this unit cannot see."),
            N(ZBehaviorKey.MinDamage,"Minimum incoming damage","DODGING","HP",0,10000,0,0,"Ignore threats below this raw damage, before armor and distance falloff."),
            N(ZBehaviorKey.Distance,"Dodge distance multiplier","DODGING","x",0.1,4,1,1,"Multiplies the native time-to-impact times speed distance, including its random extension."),
            N(ZBehaviorKey.MinDistance,"Minimum dodge distance","DODGING","m",0,20,0,0,"Lower limit for a defensive step."),
            N(ZBehaviorKey.MaxDistance,"Maximum dodge distance (0 = unlimited)","DODGING","m",0,40,0,0,"Upper limit; also respects maximum deviation from the interrupted order."),
            new ZBehaviorDef(ZBehaviorKey.Direction,"Direction strategy","DODGING","",0,2,0,0,"Predictive samples reachable, unblocked trajectories against the predicted explosion.",new[]{"ZOD RANDOM","AWAY FROM BLAST","PREDICTIVE"}),
            B(ZBehaviorKey.Forward,"Allow forward dodge","DODGING",true,true,"Direction relative to the current simulation heading."),
            B(ZBehaviorKey.Backward,"Allow backward dodge","DODGING",true,true,"Direction relative to the current simulation heading."),
            B(ZBehaviorKey.Sideways,"Allow sideways dodge","DODGING",true,true,"Allow directions predominantly across the current heading."),
            B(ZBehaviorKey.Replan,"Replan on another projectile","DODGING",true,true,"A new threat can change an active dodge after its commitment time."),
            N(ZBehaviorKey.Commit,"Minimum dodge commitment","DODGING","s",0,5,0,0,"Hold the chosen defensive direction this long before replanning."),
            N(ZBehaviorKey.MaxDuration,"Maximum dodge duration (0 = unlimited)","DODGING","s",0,10,0,0,"Stop the defensive step and resume the interrupted order."),
            B(ZBehaviorKey.EndAfterBlast,"End dodge after blast","DODGING",false,false,"End 0.1 seconds after the latest relevant blast instead of walking to the endpoint."),
            N(ZBehaviorKey.MaxDeviation,"Maximum deviation (0 = unlimited)","DODGING","m",0,40,0,0,"Distance from the first interrupted position, across successive replans."),
            B(ZBehaviorKey.Physical,"Simulate physical movement limits","MOVEMENT",false,false,"Changes real positions and damage outcomes. Off preserves instantaneous native simulation movement."),
            N(ZBehaviorKey.Acceleration,"Time to full speed (0 = instant)","MOVEMENT","s",0,5,0,0,"Affects real movement only with physical limits enabled."),
            N(ZBehaviorKey.Braking,"Braking time (0 = instant)","MOVEMENT","s",0,5,0,0,"Slow down when approaching the destination. Stop orders and blocked movement still stop safely."),
            N(ZBehaviorKey.ReverseDelay,"Direction reversal delay","MOVEMENT","s",0,3,0,0,"Physical mode: pause before reversing longitudinal motion."),
            N(ZBehaviorKey.ReverseSpeed,"Reverse speed","MOVEMENT","%",5,150,100,100,"Physical mode: speed when backing along the hull axis."),
            B(ZBehaviorKey.Reverse,"Allow reversing","MOVEMENT",true,true,"Physical mode: back up rather than turn around for a rear target."),
            N(ZBehaviorKey.TurnRate,"Maximum turn rate (0 = instant)","MOVEMENT","deg/s",0,1440,150,0,"Physical mode limits real turning. Also sets the displayed hull rate."),
            N(ZBehaviorKey.TurnAcceleration,"Angular acceleration (0 = instant)","MOVEMENT","deg/s2",0,6000,1400,0,"Physical mode limits changes in angular speed."),
            B(ZBehaviorKey.Pivot,"Allow pivot on the spot","MOVEMENT",true,true,"Physical mode: turn before driving into a sharp corner."),
            B(ZBehaviorKey.TurnMoving,"Allow turning while moving","MOVEMENT",true,true,"Physical mode: disabling requires alignment before forward motion."),
            N(ZBehaviorKey.CornerSpeed,"Speed in sharp turns","MOVEMENT","%",0,100,100,100,"Physical mode: retained speed at a 90 degree turn."),
            B(ZBehaviorKey.Lateral,"Allow free lateral movement","MOVEMENT",false,true,"Physical mode: travel direction may change independently of hull heading. Visual follow is controlled separately."),
            N(ZBehaviorKey.SlipAngle,"Visual sideslip angle","MOVEMENT","deg",0,90,7,90,"Maximum lateral travel angle for the 3D follower; visual only."),
            B(ZBehaviorKey.ImmediateTurn,"Immediate defensive steering","MOVEMENT",false,true,"During dodges bypass heading confirmation and commitment in the 3D hull."),
            N(ZBehaviorKey.HeadingHold,"Visual heading confirmation","MOVEMENT","s",0,1.5,.35,0,"Visual only: how long a new heading must persist."),
            N(ZBehaviorKey.HeadingCommit,"Visual heading commitment","MOVEMENT","s",0,3,1.4,0,"Visual only: how long to keep a chosen hull heading."),
            N(ZBehaviorKey.DeadZone,"Visual heading dead zone","MOVEMENT","deg",0,45,9,0,"Visual only: ignore smaller heading changes."),
            N(ZBehaviorKey.ShortTurn,"Visual short-move turn limit","MOVEMENT","deg",0,180,35,180,"Visual only: hull rotation allowed toward a short step."),
            B(ZBehaviorKey.DirectView,"Follow 2D simulation directly","MOVEMENT",false,true,"Recommended for Z-style dodges: follow the combat position without catch-up arcs. Maximum turn rate still limits the hull. OFF uses the legacy follower, which can lag and curve while catching up. Physical limits take priority."),
            B(ZBehaviorKey.Boost,"Defensive boost enabled","BOOST",true,true,"Allow sprint during a dodge."),
            N(ZBehaviorKey.BoostSpeed,"Defensive boost speed","BOOST","x",1,5,1.8,1.8,"Replaces the defensive sprint multiplier; existing speed bonuses use the strongest factor."),
            N(ZBehaviorKey.BoostChance,"Boost activation chance","BOOST","%",0,100,80,80,"Native Zod attempts start a run 80 percent of the time."),
            N(ZBehaviorKey.BoostDuration,"Maximum boost duration (0 = unlimited)","BOOST","s",0,20,0,0,"Maximum defensive sprint duration, still bounded by endurance when enabled."),
            B(ZBehaviorKey.Endurance,"Boost consumes endurance","BOOST",true,true,"Disable for unlimited arcade sprint; does not refill the current reserve."),
            N(ZBehaviorKey.Drain,"Endurance consumption","BOOST","x",0,5,1,1,"Defensive boost consumption relative to native running."),
            N(ZBehaviorKey.Recharge,"Endurance recharge","BOOST","x",0,5,1,1,"Recharge multiplier. Reserve capacity still comes from unit statistics."),
            N(ZBehaviorKey.MinStamina,"Minimum starting endurance","BOOST","s",0,10,.3,.3,"Minimum reserve to start a defensive boost."),
            B(ZBehaviorKey.DamagedBoost,"Allow boost when badly damaged","BOOST",false,false,"Ignore the native damage restriction for defensive sprint."),
            B(ZBehaviorKey.PremiumBoost,"Keep premium driver dodge bonus","BOOST",true,false,"Preserve the native 1.08 premium-driver bonus; use strongest bonus, never multiply boosts."),
            B(ZBehaviorKey.FireDodge,"Can fire during dodge","COMBAT",true,true,"Keep acquired targets and permit shots while dodging."),
            B(ZBehaviorKey.FireBoost,"Can fire during defensive boost","COMBAT",true,true,"Allow shots while sprinting defensively."),
            N(ZBehaviorKey.Accuracy,"Accuracy during dodge","COMBAT","%",0,200,100,100,"Multiplies normal hit chance; moving-robot penalties still apply."),
            N(ZBehaviorKey.Settle,"Post-dodge firing delay","COMBAT","s",0,5,0,0,"Wait after the defensive step before firing again."),
            B(ZBehaviorKey.KeepTarget,"Keep target during dodge","COMBAT",true,true,"Robot dodge movement keeps a valid target instead of disengaging."),
            B(ZBehaviorKey.FireMoving,"Allow robot fire on the move","COMBAT",true,false,"Enables existing grunt, psycho and laser moving fire. Does not invent moving attacks for stationary weapon types."),
            B(ZBehaviorKey.BreakForRetreat,"Retreat breaks firing stance","COMBAT",false,true,"A move away from a target can release sniper/electrobot standing fire."),
            N(ZBehaviorKey.Cohesion,"Maximum squad separation (0 = unlimited)","COMBAT","m",0,30,0,0,"Reject dodge endpoints farther than this from the squad leader."),
            N(ZBehaviorKey.RegroupDelay,"Delay before post-dodge regroup","COMBAT","s",0,5,0,0,"Delay autonomous minion movement after a dodge; direct orders still resume immediately."),
            B(ZBehaviorKey.CombatManoeuvres,"OpenZ offensive combat manoeuvres","COMBAT",true,false,"OpenZ's flanks, arcs and reverses while attacking. OFF uses Zod's stop-and-fire behavior once in range. Defensive dodges remain separate."),
            B(ZBehaviorKey.ResumeAttack,"Resume attack after the threat passes","DODGING",false,false,"Custom combat safeguard: end the dodge 0.1 s after the first triggering blast and resume the interrupted attack. Later shots can change the direction but cannot postpone this return indefinitely. Stops long boosted dodge endpoints from behaving like a retreat. Enabled automatically when you manually tune a profile; turn it off to reproduce Zod's full random step. New player orders always take precedence."),
            B(ZBehaviorKey.TurnFirst,"Brake, pivot, then accelerate","MOVEMENT",false,false,"Requires physical movement limits. On direction changes, brake along the current hull axis, pivot only once slow enough, then accelerate once aligned. Applies to normal movement and dodges. Overrides lateral movement, reversing and the legacy pivot/turn-while-moving switches while enabled."),
            N(ZBehaviorKey.PivotSpeed,"Retained speed during pivot","MOVEMENT","%",0,100,0,0,"Brake to this fraction of commanded speed before pivoting. 0 = stop completely; 5 = almost stop. Effective range 0-100%. Requires brake-pivot-accelerate mode."),
            N(ZBehaviorKey.AlignmentTolerance,"Alignment before acceleration","MOVEMENT","deg",0,45,5,5,"Start accelerating when the hull is within this angle of the movement direction. Effective maximum 45 degrees. Requires brake-pivot-accelerate mode.")
        };
        public static ZBehaviorDef Def(ZBehaviorKey key) => All[(int)key];
        public static readonly string[] TankTypes = { "light", ZMTank.Type, "heavy", "medium" };
        public static readonly string[] RobotTypes = { "grunt", "psycho", "tough", "sniper", "pyro", "laser", "electrobot" };
        public static bool IsTank(string type) => Array.IndexOf(TankTypes, type) >= 0;
        public static bool IsRobot(string type) => Array.IndexOf(RobotTypes, type) >= 0;
        public static double Endpoint(ZBehaviorKey key, ZBehaviorFamily family, string type, bool zod)
        {
            var d = Def(key); double n = d.Native, z = d.Zod;
            if (family == ZBehaviorFamily.Tanks && key == ZBehaviorKey.Retreat) n = 1;
            if (family == ZBehaviorFamily.Robots)
            {
                if (key == ZBehaviorKey.Engaged) n = z = 0;
                if (key == ZBehaviorKey.Cooldown) n = 8;
                if (key == ZBehaviorKey.SquadCooldown || key == ZBehaviorKey.ShootFirst) n = 1;
                if (key == ZBehaviorKey.TurnRate) n = 360;
                if (key == ZBehaviorKey.Lateral || key == ZBehaviorKey.DirectView) n = 1;
            }
            if (type == "heavy") { if (key == ZBehaviorKey.TurnRate) n = 110; if (key == ZBehaviorKey.TurnAcceleration) n = 900; if (key == ZBehaviorKey.SlipAngle) n = 6; }
            if (type == ZMTank.Type) { if (key == ZBehaviorKey.TurnRate) n = 130; if (key == ZBehaviorKey.TurnAcceleration) n = 1150; if (key == ZBehaviorKey.SlipAngle) n = 6.5; }
            if (type == "medium") { if (key == ZBehaviorKey.TurnRate) n = 140; if (key == ZBehaviorKey.TurnAcceleration) n = 1200; if (key == ZBehaviorKey.SlipAngle) n = 9; }
            return zod ? z : n;
        }
    }

    [Serializable] public sealed class ZBehaviorOverride
    {
        public string type = "";
        public ZBehaviorKey key;
        public double value;
    }

    [Serializable] public sealed class ZBehaviorProfile
    {
        public string id = "", name = "Custom";
        public ZBehaviorFamily family;
        public double master;
        public List<ZBehaviorOverride> overrides = new List<ZBehaviorOverride>();
        public List<ZBehaviorOverride> nativeReferences = new List<ZBehaviorOverride>(), zodReferences = new List<ZBehaviorOverride>();
        public bool NativeFor(string type) => master == 0 && !overrides.Exists(o => o.type == "" || o.type == type);
        public bool Has(string type, ZBehaviorKey key) => overrides.Exists(o => o.type == type && o.key == key);
        public void Reset(string type, ZBehaviorKey key) => overrides.RemoveAll(o => o.type == type && o.key == key);
        public void Set(string type, ZBehaviorKey key, double value)
        {
            if ((int)key < 0 || key >= ZBehaviorKey.Count) throw new ArgumentException("Unknown behavior key.");
            if (!KnownType(type)) throw new ArgumentException("Unit type does not belong to this behavior family.");
            value = ZBehaviorSchema.Def(key).Clamp(value);
            Reset(type, key); overrides.Add(new ZBehaviorOverride { type = type, key = key, value = value });
        }
        public bool KnownType(string type) => type == "" || (family == ZBehaviorFamily.Tanks ? ZBehaviorSchema.IsTank(type) : ZBehaviorSchema.IsRobot(type));
        public void ConfigureTurnFirst(string type)
        {
            // A movement-only starting point: retain every dodge/combat choice and type exception.
            Set(type, ZBehaviorKey.Physical, 1); Set(type, ZBehaviorKey.TurnFirst, 1);
            Set(type, ZBehaviorKey.PivotSpeed, 0); Set(type, ZBehaviorKey.AlignmentTolerance, 5);
            Set(type, ZBehaviorKey.TurnRate, 720); Set(type, ZBehaviorKey.TurnAcceleration, 0);
            Set(type, ZBehaviorKey.Acceleration, .25); Set(type, ZBehaviorKey.Braking, .15);
        }
        public double Get(string type, ZBehaviorKey key)
        {
            for (int i = overrides.Count - 1; i >= 0; i--) if (overrides[i].key == key && overrides[i].type == type) return overrides[i].value;
            if (type != "") for (int i = overrides.Count - 1; i >= 0; i--) if (overrides[i].key == key && overrides[i].type == "") return overrides[i].value;
            double a = Reference(type, key, false), b = Reference(type, key, true);
            var d = ZBehaviorSchema.Def(key);
            // Discrete rules switch at 50%; an exact endpoint has no probabilistic switches.
            return d.Choices != null ? (master < 50 ? a : b) : d.Clamp(a + (b - a) * master / 100.0);
        }
        public double Reference(string type, ZBehaviorKey key, bool zod)
        {
            var refs = zod ? zodReferences : nativeReferences;
            var entry = refs.Find(o => o.type == type && o.key == key);
            return entry != null ? entry.value : ZBehaviorSchema.Endpoint(key, family, type, zod);
        }
        public void FreezeReferences()
        {
            foreach (string type in family == ZBehaviorFamily.Tanks ? ZBehaviorSchema.TankTypes : ZBehaviorSchema.RobotTypes)
                foreach (var d in ZBehaviorSchema.All)
                    for (int z = 0; z < 2; z++)
                    {
                        var refs = z == 0 ? nativeReferences : zodReferences;
                        if (!refs.Exists(o => o.type == type && o.key == d.Key)) refs.Add(new ZBehaviorOverride { type = type, key = d.Key, value = ZBehaviorSchema.Endpoint(d.Key, family, type, z == 1) });
                    }
        }
        public ZBehaviorProfile Clone() => Decode(Encode());
        public string Encode()
        {
            var s = new StringBuilder("openz-behavior-v1\n");
            s.Append((int)family).Append('\n').Append(master.ToString("R", CultureInfo.InvariantCulture)).Append('\n');
            s.Append(Convert.ToBase64String(Encoding.UTF8.GetBytes(id ?? ""))).Append('\n');
            s.Append(Convert.ToBase64String(Encoding.UTF8.GetBytes(name ?? "Custom"))).Append('\n');
            foreach (var o in overrides) s.Append(o.type).Append('|').Append((int)o.key).Append('|').Append(o.value.ToString("R", CultureInfo.InvariantCulture)).Append('\n');
            foreach (var o in nativeReferences) s.Append("N|").Append(o.type).Append('|').Append((int)o.key).Append('|').Append(o.value.ToString("R", CultureInfo.InvariantCulture)).Append('\n');
            foreach (var o in zodReferences) s.Append("Z|").Append(o.type).Append('|').Append((int)o.key).Append('|').Append(o.value.ToString("R", CultureInfo.InvariantCulture)).Append('\n');
            return s.ToString();
        }
        public static ZBehaviorProfile Decode(string text)
        {
            if (text == null || text.Length > 200000) throw new FormatException("Invalid behavior preset size.");
            var l = text.Replace("\r", "").Split('\n');
            if (l.Length < 6 || l[0] != "openz-behavior-v1") throw new FormatException("Unsupported behavior preset format.");
            int f = int.Parse(l[1], CultureInfo.InvariantCulture);
            if (f < 0 || f > 1) throw new FormatException("Invalid behavior family.");
            var p = new ZBehaviorProfile { family = (ZBehaviorFamily)f, master = double.Parse(l[2], CultureInfo.InvariantCulture),
                id = Encoding.UTF8.GetString(Convert.FromBase64String(l[3])), name = Encoding.UTF8.GetString(Convert.FromBase64String(l[4])) };
            if (double.IsNaN(p.master) || double.IsInfinity(p.master) || p.master < 0 || p.master > 100 || p.name.Length > 80 || p.id.Length > 100)
                throw new FormatException("Invalid behavior preset header.");
            for (int i = 5; i < l.Length; i++)
            {
                if (l[i].Length == 0) continue;
                var v = l[i].Split('|');
                if (v.Length == 3) p.Set(v[0], (ZBehaviorKey)int.Parse(v[1], CultureInfo.InvariantCulture), double.Parse(v[2], CultureInfo.InvariantCulture));
                else if (v.Length == 4 && (v[0] == "N" || v[0] == "Z"))
                {
                    var key = (ZBehaviorKey)int.Parse(v[2], CultureInfo.InvariantCulture);
                    if (key < 0 || key >= ZBehaviorKey.Count || !p.KnownType(v[1])) throw new FormatException("Invalid behavior reference.");
                    double value = ZBehaviorSchema.Def(key).Clamp(double.Parse(v[3], CultureInfo.InvariantCulture));
                    var refs = v[0] == "N" ? p.nativeReferences : p.zodReferences;
                    if (refs.Exists(o => o.type == v[1] && o.key == key)) throw new FormatException("Duplicate behavior reference.");
                    refs.Add(new ZBehaviorOverride { type = v[1], key = key, value = value });
                }
                else throw new FormatException("Invalid behavior override.");
            }
            return p;
        }
        public ZBehaviorValues Resolve(string type)
        {
            if (NativeFor(type)) return null;
            var v = new ZBehaviorValues { ClassicReference = master == 100 && overrides.Count == 0 };
            for (int i = 0; i < (int)ZBehaviorKey.Count; i++) v.values[i] = Get(type, (ZBehaviorKey)i);
            return v;
        }
    }
    public sealed class ZBehaviorValues
    {
        public bool ClassicReference { get; internal set; }
        internal readonly double[] values = new double[(int)ZBehaviorKey.Count];
        public double this[ZBehaviorKey key] => values[(int)key];
        public bool On(ZBehaviorKey key) => this[key] >= .5;
    }

    public sealed partial class ZWorld
    {
        ZBehaviorProfile tankBehavior, robotBehavior;
        readonly Dictionary<string, ZBehaviorValues> behaviorCache = new Dictionary<string, ZBehaviorValues>();
        public int BehaviorRevision { get; private set; }
        public void SetBehavior(ZBehaviorProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            var copy = profile.Clone();
            UnitTesting?.BehaviorChanging(BehaviorProfile(copy.family), copy);
            if (copy.family == ZBehaviorFamily.Tanks) tankBehavior = copy; else robotBehavior = copy;
            behaviorCache.Clear(); BehaviorRevision++;
            foreach (var o in Objects) o.BehaviorChanged();
        }
        public ZBehaviorProfile BehaviorProfile(ZBehaviorFamily family) =>
            (family == ZBehaviorFamily.Tanks ? tankBehavior : robotBehavior)?.Clone() ?? new ZBehaviorProfile { family = family, name = "OpenZ Native" };
        public ZBehaviorValues BehaviorFor(ZObject o)
        {
            if (o == null || o.IsFortDefender || o.IsEvilZod) return null;
            ZBehaviorProfile p = o.IsRobot ? robotBehavior : o.Kind == ZKind.Vehicle && ZBehaviorSchema.IsTank(o.Type) ? tankBehavior : null;
            if (p == null) return null;
            if (!behaviorCache.TryGetValue(o.Type, out var v)) behaviorCache[o.Type] = v = p.Resolve(o.Type);
            return v;
        }
        public void WriteBehavior(System.IO.BinaryWriter writer)
        { writer.Write(BehaviorProfile(ZBehaviorFamily.Tanks).Encode()); writer.Write(BehaviorProfile(ZBehaviorFamily.Robots).Encode()); }
        public void ReadBehavior(System.IO.BinaryReader reader)
        {
            string a = reader.ReadString(), b = reader.ReadString();
            if ((tankBehavior?.Encode() ?? BehaviorProfile(ZBehaviorFamily.Tanks).Encode()) != a) SetBehavior(ZBehaviorProfile.Decode(a));
            if ((robotBehavior?.Encode() ?? BehaviorProfile(ZBehaviorFamily.Robots).Encode()) != b) SetBehavior(ZBehaviorProfile.Decode(b));
        }
    }
}
