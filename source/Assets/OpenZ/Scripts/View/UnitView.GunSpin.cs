using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.3 item 17: a destroyed gun's and howitzer's spinning turret flight (the deliberate Z-style toss) about 30 % shorter. View only.
    /// <para>What it was (UnitView.BeginWreck / TossTurret): a dead cannon stands burning for Zod's 2-4 s (ECannonDeath), then its turret is
    /// thrown and spins at 300-620 deg/s until it lands where and when the rules' turret debris blast is: Zod's cannon debris explodes 7-10 s
    /// after the death (ZWorld.DeathDebris: 7 + 0-3 s), so the spinning flight lasted 3-8 s (5.5 s on average), the longest of every
    /// toss in the game (a tank's turret flies 3-4 s).</para>
    /// <para>Now (<see cref="GunSpin.Keep"/> = 0.7): the flight keeps 70 % of its time. Default way (<see cref="GunSpin.EndsSooner"/> false): the
    /// toss starts 30 % of that flight later and the turret still lands ON the rules' debris blast (its 40 HP blast, the trees it fells, its
    /// clang and dust together, as before); the gun stands burning that much longer (about 1.6 s on average). The other way (true): the toss
    /// starts at Zod's moment and the turret lands 30 % sooner, then the rules' blast comes where it lies. Either way the start of the toss
    /// looks the same (sparks, whoosh, the same spin speed and apex) and the other cannons, the tanks and the fort's own big gun are unchanged.
    /// Switch: <see cref="GunSpin.Enabled"/> (off = as before).</para>
    /// </summary>
    public static class GunSpin
    {
        public static bool Enabled = true;
        /// <summary>Share of the old flight time the spinning flight keeps (0.65-0.75 = 25-35 % shorter).</summary>
        public static float Keep = 0.7f;
        /// <summary>false (default) = the toss starts later and lands on the rules' blast; true = it starts at Zod's moment and lands sooner.</summary>
        public static bool EndsSooner = false;
        public static readonly HashSet<string> Types = new HashSet<string> { "gun", "howitzer" };
        internal static bool Applies(ZObject o) => Enabled && o != null && o.Kind == ZKind.Cannon && Types.Contains(o.Type);

        /// <summary>One toss for the check: the unit, its type, when it started (Time.time), its flight (real s), the rules' debris missile it
        /// lands on (null = none found).</summary>
        public sealed class Toss { public int id; public string type; public float start, flight, oldFlight = -1f; public ZMissile debris; public bool rule; }
        /// <summary>The flight a toss moved by the rule would have had (unit id -> real s), for the check.</summary>
        public static readonly Dictionary<int, float> OldFlight = new Dictionary<int, float>();
        public static readonly List<Toss> Last = new List<Toss>();
        public static int Later, Sooner;
    }

    public partial class UnitView
    {
        ZMissile GunSpinDebris()
        {
            if (game == null || game.World == null || Obj == null) return null;
            foreach (var x in game.World.PendingMissiles) if (x.debris && x.attackerId == Obj.Id) return x;
            foreach (var x in game.World.QueuedMissiles) if (x.debris && x.attackerId == Obj.Id) return x;
            return null;
        }

        /// <summary>BeginWreck (a cannon): the toss moment, later by (1 - Keep) of the flight it would have had (the default way).</summary>
        float GunSpinTossAt(float at)
        {
            if (!GunSpin.Applies(Obj) || GunSpin.EndsSooner) return at;
            var m = GunSpinDebris(); if (m == null) return at;
            float land = deathT + OpenZFxPlayer.RealFlight((float)(m.explodeTime - game.World.Time), game.gameSpeed);
            float flight = land - at;
            if (flight <= 0.6f) return at;
            GunSpin.Later++; GunSpin.OldFlight[Obj.Id] = flight;
            if (GunSpin.OldFlight.Count > 128) GunSpin.OldFlight.Clear();
            return at + flight * (1f - GunSpin.Keep);
        }

        /// <summary>TossTurret: the flight (the other way only: GunSpin.EndsSooner).</summary>
        float GunSpinFlight(float dur)
        {
            if (!GunSpin.Applies(Obj) || !GunSpin.EndsSooner) return dur;
            GunSpin.Sooner++; GunSpin.OldFlight[Obj.Id] = dur;
            if (GunSpin.OldFlight.Count > 128) GunSpin.OldFlight.Clear();
            return Mathf.Max(0.6f, dur * GunSpin.Keep);
        }

        void GunSpinNote(ZMissile m, float dur)
        {
            if (Obj == null || Obj.Kind != ZKind.Cannon) return;                                     // every cannon's toss (the check compares the others too)
            GunSpin.Last.Add(new GunSpin.Toss { id = Obj.Id, type = Obj.Type, start = Time.time, flight = dur, debris = m, rule = GunSpin.Applies(Obj),
                                                oldFlight = GunSpin.OldFlight.TryGetValue(Obj.Id, out float of) ? of : -1f });
            if (GunSpin.Last.Count > 64) GunSpin.Last.RemoveAt(0);
        }
    }
}
