using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// DV163, level 7: draws a robot's shot at a dragon. Each weapon's own round, muzzle, recoil and sound, from the drawn gun, up into the air:
    /// grunt, psycho, laser, sniper through WeaponsV2Runner (QueueSkyShot: no target object, the end point remembered as a sky miss, so nothing
    /// lands up there and nothing puffs on the ground), the Pyro through his jet (OpenZPyroFire.SkyCue), the Tough through his tube with a small air burst
    /// when a hit arrives, the Electrobot through his windup and bolt (OpenZElectroFx.SkyBolt, the rules' flight of his shot is the windup; lightning never misses).
    /// The rules' roll is the rules' (ZDragonKind.Shot, flag = hit); a round that misses flies on past the dragon.
    /// </summary>
    public static class OzDragonShots
    {
        struct Burst { public float at; public Vector3 p; public int id; }
        static readonly List<Burst> bursts = new List<Burst>(8);
        struct Bolt { public float at; public Vector3 p; public ZObject a; public Transform dragon; }
        static readonly List<Bolt> bolts = new List<Bolt>(4);

        public static void Draw(ZGame game, ZEvent e, ZObject a, ZDragonPass pass, Vector3 bird, Transform dragon)
        {
            if (a == null || !game || !game.Views.TryGetValue(a.Id, out var av) || !av || !av.isActiveAndEnabled) return;
            float flight = e.value; bool hit = e.flag;
            var at = bird;
            if (pass != null)
            {
                pass.PointAt(game.World.Time - pass.StartAt + flight, out double x, out double y, out double z);
                at = new Vector3((float)x, (float)y, (float)z);
            }
            var from = av.MuzzlePos();
            float near = hit ? 0.1f : 1.4f, far = hit ? 0.6f : 2.8f;
            bool drawn = false;
            switch (a.Type)
            {
                case "grunt": case "laser": case "sniper":
                {
                    var r = WeaponsV2Runner.Ensure();
                    if (r) drawn = r.QueueSkyShot(a, a.Type, av, from, OzShuttleAim.MissPast(from, at, near, far, 12f));
                    break;
                }
                case "psycho":
                {
                    var r = WeaponsV2Runner.Ensure();
                    if (r) drawn = r.QueueSkyShot(a, a.Type, av, from, at);                                 // its burst spreads every round itself (MgRound)
                    break;
                }
                case "pyro":
                    drawn = OpenZPyroFire.SkyCue(av, a, at);
                    if (drawn) av.OnFire();
                    break;
                case "tough":
                {
                    var ze = new ZEvent { type = ZEventType.MissileFired, objId = a.Id, otherId = -1, value = flight, end = game.World.Time + flight };
                    var to = hit ? at : OzShuttleAim.MissPast(from, at, 1.5f, 3f, 12f);
                    if (!WeaponsV2.LaunchTough(av, a, ze, to, flight))
                    {
                        LegacyMatch.Launch(a, "tough", from, to, flight, FxManager.I);
                        LegacyMatch.LaunchSound("tough", from, a.Id, CombatAudio.I);
                        LegacyMatch.RocketFlight(from, to, flight, a.Id, CombatAudio.I);
                    }
                    if (hit) bursts.Add(new Burst { at = Time.time + OpenZFxPlayer.RealFlight(flight, game.gameSpeed), p = at, id = a.Id });
                    drawn = true;
                    break;
                }
                case ZElectro.Type:
                    OpenZElectroFx.Handle(new ZEvent { type = ZEventType.ElectroCharge, objId = a.Id, otherId = -1, value = flight }, a, null);
                    bolts.Add(new Bolt { at = Time.time + OpenZFxPlayer.RealFlight(flight, game.gameSpeed), p = at, a = a, dragon = dragon });
                    drawn = OpenZElectroFx.Enabled;
                    break;
            }
            OzDragonProof.NoteShot(a.Type, drawn);
        }

        public static void Step()
        {
            for (int i = bursts.Count - 1; i >= 0; i--)
            {
                if (Time.time < bursts[i].at) continue;
                OzAirShots.DragonBurst(bursts[i].p, bursts[i].id);
                bursts.RemoveAt(i);
            }
            for (int i = bolts.Count - 1; i >= 0; i--)
            {
                var b = bolts[i];
                if (Time.time < b.at) continue;
                bolts.RemoveAt(i);
                bool seen = b.dragon && b.dragon.gameObject.activeInHierarchy;
                if (!b.a.Destroyed) OpenZElectroFx.SkyBolt(b.a, seen ? b.dragon.position : b.p, seen ? b.dragon : null);
            }
        }
    }

    public static partial class OzAirShots
    {
        /// <summary>DV163: a Tough's rocket that hits a dragon: the bazooka's blast in its air form, up there, nothing on the ground, its own sound.</summary>
        public static void DragonBurst(Vector3 at, int id)
        {
            var z = OpenZFxPlayer.I; if (!z || !z.isActiveAndEnabled) return;
            var d = LegacyMatch.Bazooka;
            Pop(z, at, d, 1f, id * 7919 + 61, id, 1f, d.pitch, false);
        }
    }
}
