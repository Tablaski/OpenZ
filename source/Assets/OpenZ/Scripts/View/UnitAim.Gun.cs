using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.2 v2 units (the gun, owner items 135 / 140, lesson 5: "the turret must never jitter on target switches (eased, speed-limited view
    /// rotation; check UnitAim.Steady's minimum range against the v2 muzzle reach)"; "nothing clipping"):
    ///  * v2's barrel elevation stop is <see cref="OzGun.PitchMaxV2"/> (v1 40): above about 33 deg its octagonal collar would sweep over the
    ///    box's front bar. Everything else of the gun's slew (turn and pitch speeds, easing, tolerance, kick, the barrel slide) is v1's.
    ///  * The gun's turret (v1 and v2) joins k81c's world-heading slew of View/UnitAim.Combat.cs (until now every vehicle turret but the MLRS
    ///    rack, never a cannon): a critically damped, speed-limited slew of the barrel's heading, a new aim followed only when it has held
    ///    0.25 s (no back-and-forth on a goal flip), the gun kept on its live target after a shot instead of turning to that shell's own
    ///    landing point and back, held still in a 0.35 deg dead band; k84b's rest after a shot that waited for the gun
    ///    (UnitAim.ShotSettleTypes) and k87b's rest before a turn back (UnitAim.TurnRestTypes). k81b's steady solve already served the gun
    ///    (it is not type-bound): its minimum range is at least 1.25 x the muzzle's flat reach from the turn axis: v2 2.48 m x 1.25 = 3.1 m,
    ///    inside the 4 m floor, as v1's 2.44 m (on a fort tower x 0.72).
    /// View only: the sim's targets, shots and their timing are untouched; shells still leave only when the muzzle is on them.
    /// </summary>
    public partial class UnitAim
    {
        /// <summary>The switch for the gun's world-heading slew (off = the gun's turret code before this kit, k81b's steady solve only).</summary>
        public static bool CombatGun = true;

        /// <summary>CombatOn: this gun is a gun cannon (v1 or v2) and the switch is on.</summary>
        bool CombatGunOn => CombatGun && obj != null && obj.Kind == ZKind.Cannon && obj.Type == OzGun.Type;

        /// <summary>The gun rests after a shot that waited for it, as the light tank does (k84b's UnitAim.ShotSettleTypes), and never starts back
        /// within 0.3 s of its last turn while its goal is only a little back, as the medium tank and the MLRS do (k87b's UnitAim.TurnRestTypes):
        /// both lists are kept as k84b / k87b wrote them; the gun's entries are added once at the start of every session, as k87b adds its own.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void K88GunTypes() { ShotSettleTypes.Add(OzGun.Type); TurnRestTypes.Add(OzGun.Type); }

        /// <summary>Bind: the slew with the drawn gun model's own elevation stop (v2); every other unit and model as it was.</summary>
        static Slew GunSlew(Slew s, ZObject o, UnitView v)
        {
            if (o == null || !v || o.Kind != ZKind.Cannon || o.Type != OzGun.Type || !OzGun.Models.IsV2(v.transform)) return s;
            s.pitchMax = OzGun.PitchMaxV2;
            return s;
        }
    }
}
