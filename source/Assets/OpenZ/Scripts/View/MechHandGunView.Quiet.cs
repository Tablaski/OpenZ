using System.Collections.Generic;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.4 vehicle_exit_jump, part B (View/OzVehicleQuiet.cs): the mech's hand gun when its driver is gone (EXIT DRIVER, sniped: nobody inside).
    /// Before: a sim shot armed the burst clock for WeaponsV2Logic.MgArmHold (0.4 s) and the burst then ran its clip's rounds and sound to the end:
    /// after the click the gun still fired rounds and its MG clip for up to about 1 s (the mech continues shooting). Now, from the frame
    /// the view sees the mech empty: the burst ends, its clip stops, no round is drawn, and the arm stays where it was (no turn to rest, no
    /// wobble). Fix round 3: a killing round the rules fired in a tick before the stop is still drawn in that frame when the arm is on its
    /// target (its robot falls to a drawn round). Crewed again: as before.
    /// </summary>
    public sealed partial class MechHandGunView
    {
        readonly HashSet<int> quietGuns = new HashSet<int>();
        /// <summary>Proof: the rounds the gun had drawn when the view saw the mech empty (MgBurst.Rounds), by mech id: RoundsAfterQuiet reads the rounds since.</summary>
        static readonly Dictionary<int, int> roundsAtQuiet = new Dictionary<int, int>();

        /// <summary>LateUpdate, per gun before its pose and burst: true = the mech is empty, its gun stands quiet this frame.</summary>
        bool QuietGun(Gun g)
        {
            var o = g.view ? g.view.Obj : null;
            if (!OzVehicleQuiet.On || o == null || OzVehicleQuiet.EmptiedFrame(o) < 0) { if (quietGuns.Count > 0) quietGuns.Remove(g.id); return false; }
            bool first = quietGuns.Add(g.id);
            if (first)
            {
                bool running = g.mg.InBurst || g.mg.Armed(Time.time);
                if (running) { OzVehicleQuiet.BurstsCut++; StopClip(g); }
                roundsAtQuiet[g.id] = g.mg.Rounds; cutAtQuiet[g.id] = running;
            }
            bool kill = first && g.kill;                                                                        // fix round 3: a killing round the rules fired in a tick before the stop (Update armed it this frame)
            g.mg.Clip = -1; g.mg.ArmedUntil = -1f; g.kill = false; g.yawVel = g.pitchVel = 0f; g.wob = 0f;
            Rest(g, out var restRot, out var f0, out _);                                                       // the arm at its last angles (Update put it back at rest this frame)
            var flat = new Vector3(f0.x, 0f, f0.z); var h = Vector3.Cross(Vector3.up, flat.sqrMagnitude > 1e-6f ? flat.normalized : g.view.transform.forward);
            g.elbow.rotation = Quaternion.AngleAxis(g.yaw, Vector3.up) * Quaternion.AngleAxis(-g.pitch, h.sqrMagnitude > 1e-6f ? h.normalized : Vector3.right) * restRot;
            if (kill)
            {   // fix round 3 (review: its target fell with no round drawn): with the arm already on the target (the killing round only along the
                // arm, as LateUpdate draws it) that one round is drawn now, then the gun is quiet; else it is dropped (counted)
                if (Enabled && g.muzzle && MechHandGunAim.KillOn(g.muzzle.position, (g.elbow.rotation * g.dirE).normalized, g.lastTo))
                {
                    var t = game && game.World != null ? game.World.GetAny(g.lastTarget) : null;
                    Round(g, Time.time, t, true); KillRounds++; killAtQuiet[g.id] = 1; OzVehicleQuiet.KillRoundsDrawn++;
                }
                else { killAtQuiet[g.id] = 0; OzVehicleQuiet.KillRoundsDropped++; }
            }
            return true;
        }
        static readonly Dictionary<int, int> killAtQuiet = new Dictionary<int, int>();
        /// <summary>Fix round 3, proof (Play check S1's text): that mech's killing round pending in its emptied frame: 1 drawn (the arm on its
        /// target), 0 dropped (the arm off it), -1 none was pending.</summary>
        public static int KillAtQuiet(int mechId) => killAtQuiet.TryGetValue(mechId, out int k) ? k : -1;

        /// <summary>The burst's MG clip stops: the voice MechHandGunAim.BurstSound recorded for this gun (fix round 1: also a burst heard from off
        /// screen, whose voice plays at CombatAudio's distant spot); none recorded: a voice playing one of the burst clips within 3 m of the muzzle.</summary>
        static void StopClip(Gun g)
        {
            if (MechHandGunAim.StopBurstVoice(g.id)) { OzVehicleQuiet.HatchClipsStopped++; return; }
            var au = CombatAudio.I; if (!au || !g.muzzle) return;
            foreach (var a in au.GetComponentsInChildren<AudioSource>())
            {
                if (!a.isPlaying || !a.clip || (a.transform.position - g.muzzle.position).sqrMagnitude > 9f) continue;
                foreach (var names in MgClipNames) if (names[0] == a.clip.name) { a.Stop(); break; }
            }
        }

        /// <summary>Proof (Play check): rounds that mech's hand gun drew after the view saw it empty (-1: not seen empty / no gun).</summary>
        public static int RoundsAfterQuiet(int mechId)
        {
            var v = I; if (!v || !roundsAtQuiet.TryGetValue(mechId, out int r0) || !v.guns.TryGetValue(mechId, out var g)) return -1;
            return g.mg.Rounds - r0;
        }
        static readonly Dictionary<int, bool> cutAtQuiet = new Dictionary<int, bool>();
        /// <summary>Proof (fix round 1): the rounds that mech's hand gun had drawn when the view saw it empty (-1: not seen empty); whether a burst
        /// was armed or running then (cut by the quiet); a burst armed or running now; the rounds drawn so far (-1: no gun).</summary>
        public static int RoundsAtQuiet(int mechId) => roundsAtQuiet.TryGetValue(mechId, out int r0) ? r0 : -1;
        public static bool CutAtQuiet(int mechId) => cutAtQuiet.TryGetValue(mechId, out bool c) && c;
        public static bool BurstNow(int mechId) { var v = I; return v && v.guns.TryGetValue(mechId, out var g) && (g.mg.InBurst || g.mg.Armed(Time.time)); }
        public static int RoundsOf(int mechId) { var v = I; return v && v.guns.TryGetValue(mechId, out var g) ? g.mg.Rounds : -1; }
    }
}
