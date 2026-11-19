using System;
using System.Collections.Generic;
using OpenZ.Sim;

namespace OpenZ.Multiplayer
{
    // Pure C#: no scene, PlayerPrefs, renderer or transport dependencies.
    public enum DuelPhase { Lobby, Loading, Playing, Closed }
    public enum RadarLossRule { Off, Fog, Zoom, FogAndZoom }

    [Serializable]
    public sealed class DuelArmySettings
    {
        public double BuildDuration = 1;
        public readonly List<string> StartingUnits = new List<string>();
        public string[] FortTurrets = { "", "", "", "" };

        internal DuelArmySettings Copy()
        {
            var copy = new DuelArmySettings { BuildDuration = BuildDuration, FortTurrets = (string[])FortTurrets.Clone() };
            copy.StartingUnits.AddRange(StartingUnits);
            return copy;
        }
    }

    [Serializable]
    public sealed class DuelRules
    {
        public const int MaxPlanningSeconds = 10;
        public string Map = "L02";
        public int PlanningSeconds;
        public double TerritoryBonus = .5;
        public double MinimumBuildSeconds = 1;
        public double FortLoserMaximum;
        public double TerritoryReductionCap = 1;
        public bool TankDamageEffects, TankDamageShake;
        public RadarLossRule RadarLoss;
        public DuelArmySettings Common = new DuelArmySettings();
        // Null keeps the legacy shared mounts until a team is edited independently.
        public string[] RedFortTurrets, BlueFortTurrets;
        public string[] FortTurretsFor(int slot)
        {
            if (slot != 0 && slot != 1) throw new ArgumentOutOfRangeException(nameof(slot));
            return (string[])(slot == 0 ? RedFortTurrets ?? Common.FortTurrets : BlueFortTurrets ?? Common.FortTurrets).Clone();
        }
        public void SetFortTurret(int seat, int mount, string type)
        {
            if (mount < 0 || mount > 3) throw new ArgumentOutOfRangeException(nameof(mount));
            var turrets = FortTurretsFor(seat); turrets[mount] = type;
            if (seat == 0) RedFortTurrets = turrets; else BlueFortTurrets = turrets;
        }
        public bool HandicapEnabled;
        public int HandicapSlot;
        public DuelArmySettings Handicap = new DuelArmySettings();
        public string TankDynamics = "", RobotDynamics = "";
        public DuelUnitPreset UnitPreset = DuelUnitPreset.OpenZ;
        public List<string> AllowedUnits = new List<string>(DuelUnitRoster.All);
        // These map-only units default to allowed in existing saved OpenZ and Custom lobbies.
        public bool ExcludeMammoth, ExcludeBizon;

        public bool Allows(string type) => type == ZBigTanks.Mammoth ? UnitPreset != DuelUnitPreset.Z1996 && !ExcludeMammoth :
            type == ZBigTanks.Bizon ? UnitPreset != DuelUnitPreset.Z1996 && !ExcludeBizon : AllowedUnits != null && AllowedUnits.Contains(type);
        public bool HasStartingArmies
        {
            get
            {
                for (int seat = 0; seat < 2; seat++)
                {
                    var army = EffectiveFor(seat);
                    if (army.StartingUnits.Count == 0 && !Array.Exists(army.FortTurrets, t => !string.IsNullOrEmpty(t))) return false;
                }
                return true;
            }
        }
        public void SetUnitPreset(DuelUnitPreset preset)
        {
            if (preset == DuelUnitPreset.Custom) { UnitPreset = preset; return; }
            if (preset != DuelUnitPreset.OpenZ && preset != DuelUnitPreset.Z1996) throw new ArgumentOutOfRangeException(nameof(preset));
            UnitPreset = preset; AllowedUnits = new List<string>(preset == DuelUnitPreset.OpenZ ? DuelUnitRoster.All : DuelUnitRoster.Z1996);
            ExcludeMammoth = ExcludeBizon = preset == DuelUnitPreset.Z1996;
            RemoveDisallowedStartingUnits();
        }
        public void SetUnitAllowed(string type, bool allowed)
        {
            if (type == ZBigTanks.Mammoth) ExcludeMammoth = !allowed;
            else if (type == ZBigTanks.Bizon) ExcludeBizon = !allowed;
            else
            {
                if (!DuelUnitRoster.Known(type)) throw new ArgumentException("Unknown allowed unit.");
                if (allowed) { if (!AllowedUnits.Contains(type)) AllowedUnits.Add(type); }
                else AllowedUnits.Remove(type);
            }
            UnitPreset = DuelUnitPreset.Custom; RemoveDisallowedStartingUnits();
        }
        void RemoveDisallowedStartingUnits()
        {
            foreach (var turrets in new[] { RedFortTurrets, BlueFortTurrets })
                if (turrets != null) for (int i = 0; i < turrets.Length; i++) if (!Allows(turrets[i])) turrets[i] = "";
            foreach (var army in new[] { Common, Handicap })
            {
                for (int i = 0; i < army.StartingUnits.Count; i++) if (!Allows(army.StartingUnits[i])) army.StartingUnits[i] = "";
                for (int i = 0; i < army.FortTurrets.Length; i++) if (!Allows(army.FortTurrets[i])) army.FortTurrets[i] = "";
            }
        }

        public void EnableHandicap(int slot)
        {
            if (slot != 0 && slot != 1) throw new ArgumentOutOfRangeException(nameof(slot));
            if (!HandicapEnabled) Handicap = new DuelArmySettings { BuildDuration = Common.BuildDuration };
            HandicapSlot = slot; HandicapEnabled = true;
        }
        public DuelArmySettings EffectiveFor(int slot)
        {
            if (slot != 0 && slot != 1) throw new ArgumentOutOfRangeException(nameof(slot));
            var army = Common.Copy();
            army.FortTurrets = FortTurretsFor(slot);
            army.StartingUnits.RemoveAll(string.IsNullOrEmpty);
            if (HandicapEnabled && HandicapSlot == slot)
            {
                army.StartingUnits.AddRange(Handicap.StartingUnits.FindAll(t => !string.IsNullOrEmpty(t)));
                army.BuildDuration = Handicap.BuildDuration;
            }
            return army;
        }

        public DuelRules Copy() => new DuelRules
        {
            Map = Map, PlanningSeconds = PlanningSeconds, TerritoryBonus = TerritoryBonus, MinimumBuildSeconds = MinimumBuildSeconds, FortLoserMaximum = FortLoserMaximum, TerritoryReductionCap = TerritoryReductionCap,
            TankDamageEffects = TankDamageEffects, TankDamageShake = TankDamageShake, RadarLoss = RadarLoss,
            Common = Common.Copy(), Handicap = Handicap.Copy(), HandicapEnabled = HandicapEnabled, HandicapSlot = HandicapSlot,
            RedFortTurrets = RedFortTurrets == null ? null : (string[])RedFortTurrets.Clone(),
            BlueFortTurrets = BlueFortTurrets == null ? null : (string[])BlueFortTurrets.Clone(),
            TankDynamics = TankDynamics, RobotDynamics = RobotDynamics, UnitPreset = UnitPreset, AllowedUnits = new List<string>(AllowedUnits),
            ExcludeMammoth = ExcludeMammoth, ExcludeBizon = ExcludeBizon
        };

        public void Validate()
        {
            if (PlanningSeconds < 0 || PlanningSeconds > MaxPlanningSeconds) throw new ArgumentException("Invalid opening order time.");
            if (!IsMapAllowed(Map))
                throw new ArgumentException("Unknown duel map.");
            if (!Enum.IsDefined(typeof(RadarLossRule), RadarLoss)) throw new ArgumentException("Invalid radar loss rule.");
            if (!InRange(TerritoryBonus, 0, .9) || !InRange(MinimumBuildSeconds, .1, 600))
                throw new ArgumentException("Invalid territory rule.");
            if (!InRange(TerritoryReductionCap, 0, 1)) throw new ArgumentException("Invalid territory cap.");
            if (!InRange(FortLoserMaximum, 0, .75)) throw new ArgumentException("Invalid fort loser bonus.");
            if (HandicapSlot != 0 && HandicapSlot != 1) throw new ArgumentException("Invalid handicap seat.");
            CheckArmy(Common); CheckArmy(Handicap);
            foreach (var turrets in new[] { RedFortTurrets, BlueFortTurrets })
                if (turrets != null)
                {
                    if (turrets.Length != 4) throw new ArgumentException("Invalid fort mounts.");
                    foreach (string type in turrets)
                        if (type == null || type != "" && (Array.IndexOf(AllowedFortTurrets, type) < 0 || !Allows(type)))
                            throw new ArgumentException("Invalid team fort cannon.");
                }
            if (TankDynamics == null || RobotDynamics == null || TankDynamics.Length > 200000 || RobotDynamics.Length > 200000)
                throw new ArgumentException("Invalid dynamics profile size.");
            if (!Enum.IsDefined(typeof(DuelUnitPreset), UnitPreset) || AllowedUnits == null || AllowedUnits.Count > DuelUnitRoster.All.Length)
                throw new ArgumentException("Invalid allowed units.");
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string type in AllowedUnits) if (!DuelUnitRoster.Known(type) || !seen.Add(type)) throw new ArgumentException("Unknown or duplicate allowed unit.");
            if (UnitPreset == DuelUnitPreset.OpenZ && (ExcludeMammoth || ExcludeBizon || !DuelUnitRoster.Matches(AllowedUnits, DuelUnitRoster.All)) ||
                UnitPreset == DuelUnitPreset.Z1996 && !DuelUnitRoster.Matches(AllowedUnits, DuelUnitRoster.Z1996)) throw new ArgumentException("Unit selection does not match the named preset.");
            foreach (var army in new[] { Common, Handicap })
            {
                foreach (string type in army.StartingUnits) if (type != "" && !Allows(type)) throw new ArgumentException("Starting unit is disabled: " + type);
                foreach (string type in army.FortTurrets) if (type != "" && !Allows(type)) throw new ArgumentException("Fort cannon is disabled: " + type);
            }
        }
        public static bool IsMapAllowed(string map) => map == "L02" || map == "L07" || map == "L16" ||
            map == "L17" || map == "L18" || map == "L19" || OzAiMapCatalog.IsDuelMap(map);

        static bool InRange(double v, double min, double max) => !double.IsNaN(v) && v >= min && v <= max;
        static void CheckArmy(DuelArmySettings h)
        {
            if (h == null || !InRange(h.BuildDuration, .1, 10) || h.StartingUnits.Count > 4 || h.FortTurrets == null || h.FortTurrets.Length != 4)
                throw new ArgumentException("Invalid handicap.");
            foreach (var type in h.StartingUnits)
                if (type != "" && Array.IndexOf(AllowedExtras, type) < 0) throw new ArgumentException("Unknown starting unit.");
            foreach (var type in h.FortTurrets)
                if (type != "" && Array.IndexOf(AllowedFortTurrets, type) < 0)
                    throw new ArgumentException("Unknown fort turret.");
        }

        public static readonly string[] AllowedExtras =
        {
            "grunt", "psycho", "sniper", "tough", "pyro", "laser", "electrobot", "jeep", "light", "medium", "mtank", "heavy", "missile_launcher", "crane", "apc"
        };

        public static readonly string[] AllowedFortTurrets = { "gun", "gatling", "howitzer", "missile_cannon" };

        /// <summary>Enemy advantage relative to the smaller offensive army: 0 at +30%, full reduction at +90%.</summary>
        public double FortLoserReduction(double ownValue, double enemyValue)
        {
            if (!InRange(ownValue, 0, double.MaxValue) || !InRange(enemyValue, 0, double.MaxValue)) throw new ArgumentOutOfRangeException();
            if (FortLoserMaximum <= 0 || enemyValue <= ownValue) return 0;
            if (ownValue == 0) return FortLoserMaximum;
            double lead = enemyValue / ownValue - 1;
            return FortLoserMaximum * Math.Max(0, Math.Min(1, (lead - .3) / .6));
        }

        public double BuildSeconds(int slot, double baseSeconds, double flagFraction, double healthFraction)
        {
            Validate();
            if (slot < 0 || slot > 1 || !InRange(baseSeconds, 0, 86400) ||
                !InRange(flagFraction, 0, 1) || !InRange(healthFraction, 0, 1))
                throw new ArgumentOutOfRangeException();
            return Math.Max(MinimumBuildSeconds, baseSeconds * (1 - Math.Min(TerritoryBonus * flagFraction, TerritoryReductionCap)) *
                (1 + 1.25 * (1 - healthFraction)) * (HandicapEnabled && HandicapSlot == slot ? Handicap : Common).BuildDuration);
        }
    }

    /// <summary>Host-owned two-seat lobby. Remote callers cannot change rules or impersonate seat zero.</summary>
    public sealed class DuelLobby
    {
        readonly string[] names = new string[2];
        readonly bool[] ready = new bool[2], loaded = new bool[2];
        DuelRules rules = new DuelRules();
        public DuelPhase Phase { get; private set; } = DuelPhase.Lobby;
        public int Revision { get; private set; }
        public bool TeamsSwapped { get; private set; }
        public double StartAt { get; private set; } = -1;
        public string CloseReason { get; private set; }
        DuelRules proposal;
        int proposalId;
        public int ProposalId => proposalId;
        public DuelRules Proposal => proposal?.Copy();
        public bool Propose(int senderSlot, int revision, DuelRules next)
        {
            Require(DuelPhase.Lobby);
            if (senderSlot != 1 || names[1] == null) throw new InvalidOperationException("Only the guest proposes rules.");
            if (revision != Revision) return false;
            if (next == null) throw new ArgumentNullException(nameof(next));
            next.Validate();
            if (DuelRuleChanges.Describe(rules, next).Count == 0) return false;
            proposal = next.Copy(); proposalId++; return true;
        }
        public bool ResolveProposal(int senderSlot, int id, bool accept)
        {
            Require(DuelPhase.Lobby);
            if (senderSlot != 0) throw new InvalidOperationException("Only the host reviews proposals.");
            if (proposal == null || id != proposalId) return false;
            var next = proposal; proposal = null;
            if (accept) ChangeRules(0, next);
            return true;
        }
        public void CancelProposal(int senderSlot)
        {
            Require(DuelPhase.Lobby);
            if (senderSlot != 1) throw new InvalidOperationException("Only the guest withdraws a proposal.");
            proposal = null;
        }
        public DuelRules Rules => rules.Copy();
        public string Name(int slot) { CheckSlot(slot); return names[slot]; }
        public bool Ready(int slot) { CheckSlot(slot); return ready[slot]; }
        internal static int TeamSlot(int seat, bool swapped)
        { CheckSlot(seat); return swapped ? 1 - seat : seat; }

        public void SwapTeams(int senderSlot)
        {
            Require(DuelPhase.Lobby);
            if (senderSlot != 0 || names[0] == null || names[1] == null)
                throw new InvalidOperationException("Only the host can swap teams once both players have joined.");
            TeamsSwapped = !TeamsSwapped;
            InvalidateReady();
        }

        public void Join(int slot, string name)
        {
            CheckSlot(slot); Require(DuelPhase.Lobby);
            if (names[slot] != null) throw new InvalidOperationException("Seat occupied.");
            names[slot] = NormalizeName(name); InvalidateReady();
        }

        public bool Rename(int senderSlot, string name)
        {
            CheckSlot(senderSlot); Require(DuelPhase.Lobby);
            if (names[senderSlot] == null) throw new InvalidOperationException("Seat is empty.");
            string next = NormalizeName(name);
            if (names[senderSlot] == next) return false;
            names[senderSlot] = next;
            InvalidateReady();
            return true;
        }

        internal static string NormalizeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Length > 32) throw new ArgumentException("Invalid name.");
            foreach (char c in name) if (char.IsControl(c)) throw new ArgumentException("Invalid name.");
            return name.Trim();
        }

        public void ChangeRules(int senderSlot, DuelRules next)
        {
            Require(DuelPhase.Lobby);
            if (senderSlot != 0) throw new InvalidOperationException("Only the host changes rules.");
            if (next == null) throw new ArgumentNullException(nameof(next));
            next.Validate(); rules = next.Copy(); InvalidateReady();
        }

        public void SetReady(int slot, int revision, bool value)
        {
            CheckSlot(slot); Require(DuelPhase.Lobby);
            if (names[slot] == null || revision != Revision) throw new InvalidOperationException("Stale lobby or empty seat.");
            ready[slot] = value;
            if (ready[0] && ready[1]) { proposal = null; Phase = DuelPhase.Loading; }
        }

        // Host clock only. The wire protocol must distribute the start epoch to the client.
        public void MarkLoaded(int slot, int revision, double now)
        {
            CheckSlot(slot); Require(DuelPhase.Loading); CheckTime(now);
            if (revision != Revision) throw new InvalidOperationException("Stale load acknowledgment.");
            loaded[slot] = true;
            if (loaded[0] && loaded[1] && StartAt < 0) StartAt = now + 3 + rules.PlanningSeconds;
        }

        public bool Advance(double now)
        {
            CheckTime(now);
            if (Phase != DuelPhase.Loading || StartAt < 0 || now < StartAt) return false;
            Phase = DuelPhase.Playing; return true;
        }

        public void ReturnToLobby()
        {
            Require(DuelPhase.Playing);
            Array.Clear(ready, 0, 2); Array.Clear(loaded, 0, 2); StartAt = -1; Phase = DuelPhase.Lobby; Revision++;
        }

        public void Restart() => Restart(null);

        public void Restart(string map)
        {
            Require(DuelPhase.Playing);
            if (map != null)
            {
                var next = rules.Copy();
                next.Map = map;
                next.Validate();
                rules = next;
                Revision++;
            }
            Array.Clear(loaded, 0, 2); StartAt = -1; Phase = DuelPhase.Loading;
        }

        public void Disconnect(string reason)
        {
            proposal = null;
            Phase = DuelPhase.Closed; CloseReason = reason ?? "Disconnected";
            Array.Clear(ready, 0, 2); Array.Clear(loaded, 0, 2); StartAt = -1;
        }

        void InvalidateReady() { proposal = null; Revision++; Array.Clear(ready, 0, 2); }
        static void CheckSlot(int slot) { if (slot < 0 || slot > 1) throw new ArgumentOutOfRangeException(nameof(slot)); }
        static void CheckTime(double now)
        {
            if (double.IsNaN(now) || double.IsInfinity(now) || now < 0) throw new ArgumentOutOfRangeException(nameof(now));
        }
        void Require(DuelPhase phase) { if (Phase != phase) throw new InvalidOperationException("Invalid lobby phase."); }
    }
}
