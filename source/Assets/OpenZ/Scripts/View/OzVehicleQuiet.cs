using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// The view. The rules already stop such a vehicle dead in
    /// the tick its driver leaves it (EXIT DRIVER, ZWorld.ExitDriver; a sniped or killed driver, ZObject.DamageDriverHealth; any other way: it turns
    /// neutral): DV79 (Sim/ZObject.Unmanned.cs, on at every start) drops its order, path and target and ends its tick before every fire step, so
    /// the rules fire no new shot from then on (counted: Editor/OzV14VehicleExitStopCount.cs). The view, though, went on drawing for up to 1.5 s:
    /// shots the rules fired just before were still held by UnitAim until the turret was on target (up to 1.5 s for a shell) and the turret kept
    /// turning onto them and onto the target it had predicted; the mech's hand gun played out its armed burst (WeaponsV2Logic.MgArmHold 0.4 s +
    /// the burst's clip); the hitscan fire clocks drew on (CombatView.StepFireClocks, Zod's client fire sprite clock). Now, from the frame the view
    /// first sees the vehicle empty (nobody inside: neutral or no driver; <see cref="EmptiedFrame"/>):
    /// <list type="bullet">
    /// <item>its turret, gun and torso stop where they are: no aim, no idle scan, no spin, the traverse loop off (View/UnitAim.Quiet.cs); the shots
    /// it still held are drawn in that frame (the rules fired them before the click), none after;</item>
    /// <item>the mech's hand gun: its burst and its clip stop, the arm stays where it is (View/MechHandGunView.Quiet.cs);</item>
    /// <item>its hitscan fire clock is dropped, and any shot of it that would still be drawn after that frame is not drawn (<see cref="Mute"/>,
    /// CombatView.DrawShot, MissileFired, StepFireClocks); a shot drawn after that frame is counted (<see cref="DrawnAfter"/>: must stay 0).</item>
    /// <item>fix round 1: a DV87 hatch driver's own weapon (BoardingView.DriverFire.cs) goes quiet in that same frame too: his figure has no
    /// muzzle any more (<see cref="Quiet"/> in BoardingView.DriverMuzzleNow), so WeaponsV2Runner's machine-gun burst and laser volley stop
    /// and OpenZPyroFire's jet ends in that frame (its flame loop fades out over its own 0.25 s); the burst's MG clip is stopped
    /// (<see cref="NoteVoice"/>, <see cref="HatchGunQuiet"/>); the rules' DriverShot events seen from that frame on are not drawn
    /// (BoardingView.DriverShotSeen; counted).</item>
    /// <item>fix round 1: the held shots of the emptied frame are drawn only when the gun is already on them (UnitAim.Quiet.cs): none snaps the
    /// turret and none leaves a barrel that points elsewhere; the others are dropped, and, fix round 2 (review: an explosion with no shot
    /// fired), so are their impacts (<see cref="DropImpact"/>, CombatView's MissileExploded: <see cref="ImpactDropped"/>; the rules' damage
    /// stays).</item>
    /// <item>fix round 2 (review): every driven vehicle's machine-gun burst in WeaponsV2Runner (an APC's crew too, not only a tank's hatch
    /// driver) stops in the emptied frame with its MG clip (<see cref="HatchGunQuiet"/>, <see cref="NoteVoice"/>); a hatch pyro's flame loop
    /// stops in that frame (OpenZPyroFire.EndJet: a 0.02 s de-click fade, not its usual 0.25 s; <see cref="FlameLoopCut"/>).</item>
    /// </list>
    /// A vehicle crewed again is a normal vehicle from that frame. Guns (cannons) are not touched. View only.
    /// </summary>
    public static class OzVehicleQuiet
    {
        /// <summary>On (off: as before).</summary>
        public static bool On = true;
        /// <summary>Proof counters (since load or <see cref="ResetStats"/>): turrets / guns frozen, held shots drawn in the emptied frame, mech bursts
        /// cut, fire clocks dropped, shots not drawn (after the emptied frame), shots drawn after the emptied frame (all vehicles; must stay 0).</summary>
        public static int TurretsFrozen, HeldFlushed, BurstsCut, ClocksDropped, ShotsMuted, ShotsDrawnAfter;
        /// <summary>Fix round 1 proof counters: held shots of the emptied frame dropped (the gun not on them), a hatch driver's muzzle refused (his
        /// weapon quiet), his DriverShot events not drawn, burst MG clips stopped (the hatch driver's and the mech's hand gun's).</summary>
        public static int HeldDropped, DriverMuzzlesRefused, DriverShotsDropped, HatchClipsStopped;
        /// <summary>Fix round 2 proof counters: the impacts of dropped held shells not drawn, hatch flame loops cut in the emptied frame.</summary>
        public static int ImpactsDropped, FlameLoopsCut;
        /// <summary>Fix round 3 proof counters: the mech hand gun's killing rounds pending in its emptied frame (fired by the rules in a tick before
        /// the stop): drawn (the arm on its target) / dropped (the arm off it).</summary>
        public static int KillRoundsDrawn, KillRoundsDropped;
        /// <summary>A held shot of the emptied frame is drawn only when the gun is within this many degrees of it (and within its own tolerance).</summary>
        public static float QuietDrawDeg = 4f;
        static readonly Dictionary<int, int> emptied = new Dictionary<int, int>();
        static readonly Dictionary<int, int> drawn = new Dictionary<int, int>(), drawnAfter = new Dictionary<int, int>(), muted = new Dictionary<int, int>();
        static readonly Dictionary<int, int> heldDrawn = new Dictionary<int, int>(), heldDropped = new Dictionary<int, int>(), driverDropped = new Dictionary<int, int>();
        static readonly Dictionary<int, int> heardBefore = new Dictionary<int, int>(), heardAfter = new Dictionary<int, int>();
        static readonly Dictionary<int, int> impactsDropped = new Dictionary<int, int>(), flameCut = new Dictionary<int, int>();
        struct Dropped { public int attacker, target, x, y; public double end; }
        static readonly List<Dropped> dropped = new List<Dropped>();
        sealed class Voice { public AudioSource src; public string clip; }
        static readonly Dictionary<int, Voice> voices = new Dictionary<int, Voice>();

        public static void ResetStats()
        {
            TurretsFrozen = HeldFlushed = BurstsCut = ClocksDropped = ShotsMuted = ShotsDrawnAfter = 0; HeldDropped = DriverMuzzlesRefused = DriverShotsDropped = HatchClipsStopped = 0;
            drawn.Clear(); drawnAfter.Clear(); muted.Clear(); heldDrawn.Clear(); heldDropped.Clear(); driverDropped.Clear(); heardBefore.Clear(); heardAfter.Clear();
            ImpactsDropped = FlameLoopsCut = 0; impactsDropped.Clear(); flameCut.Clear(); dropped.Clear(); KillRoundsDrawn = KillRoundsDropped = 0;
        }
        static void Inc(Dictionary<int, int> d, int id) { if (d.Count > 8192) d.Clear(); d.TryGetValue(id, out int n); d[id] = n + 1; }
        static int Of(Dictionary<int, int> d, int id) => d.TryGetValue(id, out int n) ? n : 0;

        /// <summary>A driven vehicle (a tank, the mech, the jeep, the APC, ...) with nobody inside now (neutral or no driver), not destroyed.</summary>
        public static bool Empty(ZObject v) => On && v != null && v.Kind == ZKind.Vehicle && !v.Destroyed && v.KillAt < 0 && (v.Owner == Team.Neutral || v.Drivers.Count == 0);

        /// <summary>The frame the view first saw this vehicle empty (registered now when it is); -1 while somebody is inside (forgotten then).</summary>
        public static int EmptiedFrame(ZObject v)
        {
            if (v == null) return -1;
            if (!Empty(v)) { if (emptied.Count > 0) emptied.Remove(v.Id); return -1; }
            if (!emptied.TryGetValue(v.Id, out int f))
            {
                if (emptied.Count > 1024) emptied.Clear();
                f = Time.frameCount; emptied[v.Id] = f;
            }
            return f;
        }

        /// <summary>CombatView.DrawShot and MissileFired (their first lines): true = a shot of a vehicle emptied in an EARLIER frame: not drawn (counted).
        /// In the emptied frame itself the shots it still held are drawn (UnitAim.QuietStep flushes them).</summary>
        public static bool Mute(ZObject a)
        {
            if (!On || a == null || a.Kind != ZKind.Vehicle) return false;
            int f = EmptiedFrame(a);
            if (f < 0 || Time.frameCount <= f) return false;
            ShotsMuted++; Inc(muted, a.Id);
            return true;
        }

        /// <summary>Fix round 1: true = nobody is inside this vehicle (registers its emptied frame now, as <see cref="EmptiedFrame"/>): its hatch
        /// driver's weapon is quiet from this frame (BoardingView.DriverMuzzleNow and DriverShotSeen).</summary>
        public static bool Quiet(ZObject v) => On && v != null && EmptiedFrame(v) >= 0;

        /// <summary>BoardingView.DriverMuzzleNow refused (counted) / DriverShotSeen dropped an event of this vehicle (counted per vehicle).</summary>
        public static void MuzzleRefused() => DriverMuzzlesRefused++;
        public static void DriverShotDropped(ZObject v) { DriverShotsDropped++; if (v != null) Inc(driverDropped, v.Id); }

        /// <summary>WeaponsV2Runner.StepMg, right after a burst clip was played at `pos` for `id` (fix round 2: every vehicle rig, an APC's crew
        /// too; a robot's own rig returns at once): the voice that plays it (the one of that clip just started, the nearest to pos: also a burst
        /// heard from off screen at CombatAudio's distant spot), so the quiet can stop it.</summary>
        public static void NoteVoice(int id, string clip, Vector3 pos)
        {
            var au = CombatAudio.I; if (!au || clip == null) return;
            var gz = ZGame.I; var o = gz && gz.World != null ? gz.World.GetAny(id) : null; if (o == null || o.Kind != ZKind.Vehicle) return;
            AudioSource best = null; float bd = float.MaxValue;
            foreach (var a in au.GetComponentsInChildren<AudioSource>())
            {
                if (!a.isPlaying || !a.clip || a.clip.name != clip || a.time > 0.05f) continue;
                float d = (a.transform.position - pos).sqrMagnitude;
                if (d < bd) { bd = d; best = a; }
            }
            if (voices.Count > 256) voices.Clear();
            voices[id] = new Voice { src = best, clip = clip };
        }

        /// <summary>WeaponsV2Runner.StepMg, a rig (fix round 2: every rig; a vehicle's, `id`) before its burst clock: true = nobody is inside: the
        /// burst ends this frame (the caller drops its clock) and its MG clip stops (the voice NoteVoice found, if it still plays that clip).</summary>
        public static bool HatchGunQuiet(int id)
        {
            var g = ZGame.I; var v = g && g.World != null ? g.World.GetAny(id) : null;
            if (!Quiet(v)) return false;
            if (voices.TryGetValue(id, out var vc))
            {
                if (vc.src && vc.src.isPlaying && vc.src.clip && vc.src.clip.name == vc.clip) { vc.src.Stop(); HatchClipsStopped++; }
                voices.Remove(id);
            }
            return true;
        }

        /// <summary>Fix round 1, proof: CombatAudio.PlayPitched started a voice on its weapons bus (fix round 2, review: or a tank gun's launch,
        /// 'tankshot' / 'heavyshot' on the explosions bus) for `emitter` (a unit's id; a hatch driver's own
        /// slot BoardingView.CommanderEmitter + the vehicle's id; the mech hand gun's MechHandGunView.Emitter + the mech's id: both private
        /// there, 7000000 and 1 &lt;&lt; 28): counted per vehicle, apart after its emptied frame. The Play check's own count of what a vehicle was
        /// HEARD to fire after its stop (HeardAfter must stay 0), independent of the mutes above.</summary>
        public static void Heard(int emitter)
        {
            int id = emitter >= (1 << 28) ? emitter - (1 << 28) : emitter >= 7000000 ? emitter - 7000000 : emitter;
            if (id <= 0) return;
            if (emptied.TryGetValue(id, out int f) && Time.frameCount > f) Inc(heardAfter, id); else Inc(heardBefore, id);
        }
        public static int HeardBefore(int id) => Of(heardBefore, id);
        public static int HeardAfter(int id) => Of(heardAfter, id);
        public static int HeardOf(int id) => Of(heardBefore, id) + Of(heardAfter, id);

        /// <summary>Fix round 2 (review): UnitAim.QuietStep, the emptied frame: a held shell of this vehicle dropped (not drawn): its impact (the
        /// rules' MissileExploded of the same attacker, target and point, at its detonation) is not drawn either.</summary>
        public static void DropImpact(ZObject v, ZEvent e)
        {
            if (v == null || e.type != ZEventType.MissileFired) return;
            if (dropped.Count > 64) dropped.RemoveAt(0);
            dropped.Add(new Dropped { attacker = e.objId, target = e.otherId, x = e.x, y = e.y, end = e.end });
        }
        /// <summary>CombatView's MissileExploded (its first line): true = the impact of a shell dropped in its vehicle's emptied frame: not drawn
        /// (counted per vehicle); the rules' damage stays.</summary>
        public static bool ImpactDropped(ZEvent e, double now)
        {
            if (!On || dropped.Count == 0) return false;
            for (int i = dropped.Count - 1; i >= 0; i--)
            {
                var d = dropped[i];
                if (now > d.end + 2.0) { dropped.RemoveAt(i); continue; }
                if (d.attacker != e.objId || d.target != e.otherId || System.Math.Abs(d.x - e.x) > 2 || System.Math.Abs(d.y - e.y) > 2 || System.Math.Abs(now - d.end) > 0.5) continue;
                dropped.RemoveAt(i); ImpactsDropped++; Inc(impactsDropped, e.objId);
                return true;
            }
            return false;
        }
        /// <summary>Fix round 2 (review): OpenZPyroFire.EndJet of a hatch jet (vehicle id): true = the vehicle is empty: its flame loop stops in this
        /// frame (counted per vehicle).</summary>
        public static bool FlameLoopCut(int id)
        {
            var gz = ZGame.I; var v = gz && gz.World != null ? gz.World.GetAny(id) : null;
            if (!Quiet(v)) return false;
            FlameLoopsCut++; Inc(flameCut, id);
            return true;
        }
        public static int ImpactsDroppedOf(int id) => Of(impactsDropped, id);
        public static int FlameLoopsCutOf(int id) => Of(flameCut, id);

        /// <summary>UnitAim.QuietStep, the emptied frame: a held shot drawn (the gun on it) or dropped (counted per vehicle).</summary>
        public static void NoteHeld(ZObject v, bool drawnNow) { if (v == null) return; if (drawnNow) { HeldFlushed++; Inc(heldDrawn, v.Id); } else { HeldDropped++; Inc(heldDropped, v.Id); } }

        /// <summary>CombatView.StepFireClocks: true = the attacker is an empty vehicle: its fire clock is dropped at once (counted).</summary>
        public static bool DropClock(ZObject a)
        {
            if (!On || a == null || a.Kind != ZKind.Vehicle || EmptiedFrame(a) < 0) return false;
            ClocksDropped++;
            return true;
        }

        /// <summary>CombatView: a shot of this unit is drawn now (after the holds): counted per vehicle, and apart when it is after its emptied frame.</summary>
        public static void Drawn(ZObject a)
        {
            if (a == null || a.Kind != ZKind.Vehicle) return;
            drawn.TryGetValue(a.Id, out int n); drawn[a.Id] = n + 1;
            if (emptied.TryGetValue(a.Id, out int f) && Time.frameCount > f && Empty(a)) { drawnAfter.TryGetValue(a.Id, out int m); drawnAfter[a.Id] = m + 1; ShotsDrawnAfter++; }
        }

        /// <summary>Proof (Play check): shots of that vehicle drawn so far, and drawn after the frame it was seen empty.</summary>
        public static int DrawnOf(int id) => drawn.TryGetValue(id, out int n) ? n : 0;
        public static int DrawnAfter(int id) => drawnAfter.TryGetValue(id, out int n) ? n : 0;
        /// <summary>Proof: the frame that vehicle was seen empty (-1: not empty / not seen).</summary>
        public static int EmptiedFrameOf(int id) => emptied.TryGetValue(id, out int f) ? f : -1;
        /// <summary>Proof (fix round 1, per vehicle): its shots the rules sent after its emptied frame and that were not drawn (the sign that a shot
        /// came after the stop: must stay 0); its held shots of the emptied frame drawn / dropped; its hatch driver's DriverShot events not drawn.</summary>
        public static int MutedOf(int id) => Of(muted, id);
        public static int HeldDrawnOf(int id) => Of(heldDrawn, id);
        public static int HeldDroppedOf(int id) => Of(heldDropped, id);
        public static int DriverDroppedOf(int id) => Of(driverDropped, id);
    }
}
