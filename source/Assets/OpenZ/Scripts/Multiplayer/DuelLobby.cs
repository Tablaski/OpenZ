using System;
using System.Collections.Generic;

namespace OpenZ.Multiplayer
{
    // Pure C#: no scene, PlayerPrefs, renderer or transport dependencies.
    public enum DuelPhase { Lobby, Loading, Playing, Closed }

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
        public string Map = "L02";
        public double TerritoryBonus = .5;
        public double MinimumBuildSeconds = 1;
        public double FortLoserMaximum;
        public double TerritoryReductionCap = 1;
        public DuelArmySettings Common = new DuelArmySettings();
        public bool HandicapEnabled;
        public int HandicapSlot;
        public DuelArmySettings Handicap = new DuelArmySettings();

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
            Map = Map, TerritoryBonus = TerritoryBonus, MinimumBuildSeconds = MinimumBuildSeconds, FortLoserMaximum = FortLoserMaximum, TerritoryReductionCap = TerritoryReductionCap,
            Common = Common.Copy(), Handicap = Handicap.Copy(), HandicapEnabled = HandicapEnabled, HandicapSlot = HandicapSlot
        };

        public void Validate()
        {
            if (Map != "L02" && Map != "L07" && Map != "L16" && Map != "L17")
                throw new ArgumentException("Unknown duel map.");
            if (!InRange(TerritoryBonus, 0, .9) || !InRange(MinimumBuildSeconds, .1, 600))
                throw new ArgumentException("Invalid territory rule.");
            if (!InRange(TerritoryReductionCap, 0, 1)) throw new ArgumentException("Invalid territory cap.");
            if (!InRange(FortLoserMaximum, 0, .75)) throw new ArgumentException("Invalid fort loser bonus.");
            if (HandicapSlot != 0 && HandicapSlot != 1) throw new ArgumentException("Invalid handicap seat.");
            CheckArmy(Common); CheckArmy(Handicap);
        }

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
            "grunt", "psycho", "sniper", "tough", "pyro", "laser", "jeep", "light", "medium", "mtank", "heavy", "missile_launcher", "crane", "apc"
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
        public double StartAt { get; private set; } = -1;
        public string CloseReason { get; private set; }
        public DuelRules Rules => rules.Copy();
        public string Name(int slot) { CheckSlot(slot); return names[slot]; }
        public bool Ready(int slot) { CheckSlot(slot); return ready[slot]; }

        public void Join(int slot, string name)
        {
            CheckSlot(slot); Require(DuelPhase.Lobby);
            if (names[slot] != null) throw new InvalidOperationException("Seat occupied.");
            if (string.IsNullOrWhiteSpace(name) || name.Length > 32) throw new ArgumentException("Invalid name.");
            foreach (char c in name) if (char.IsControl(c)) throw new ArgumentException("Invalid name.");
            names[slot] = name.Trim(); InvalidateReady();
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
            if (ready[0] && ready[1]) Phase = DuelPhase.Loading;
        }

        // Host clock only. The wire protocol must distribute the start epoch to the client.
        public void MarkLoaded(int slot, int revision, double now)
        {
            CheckSlot(slot); Require(DuelPhase.Loading); CheckTime(now);
            if (revision != Revision) throw new InvalidOperationException("Stale load acknowledgment.");
            loaded[slot] = true;
            if (loaded[0] && loaded[1] && StartAt < 0) StartAt = now + 3;
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

        public void Restart()
        {
            Require(DuelPhase.Playing);
            Array.Clear(loaded, 0, 2); StartAt = -1; Phase = DuelPhase.Loading;
        }

        public void Disconnect(string reason)
        {
            Phase = DuelPhase.Closed; CloseReason = reason ?? "Disconnected";
            Array.Clear(ready, 0, 2); Array.Clear(loaded, 0, 2); StartAt = -1;
        }

        void InvalidateReady() { Revision++; Array.Clear(ready, 0, 2); }
        static void CheckSlot(int slot) { if (slot < 0 || slot > 1) throw new ArgumentOutOfRangeException(nameof(slot)); }
        static void CheckTime(double now)
        {
            if (double.IsNaN(now) || double.IsInfinity(now) || now < 0) throw new ArgumentOutOfRangeException(nameof(now));
        }
        void Require(DuelPhase phase) { if (Phase != phase) throw new InvalidOperationException("Invalid lobby phase."); }
    }
}
