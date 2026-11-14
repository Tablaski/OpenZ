#if UNITY_EDITOR
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// The k93 exit check (menu "1 Exit fit"), staging for the vehicle factory gate and pictures of the gates (test tools only, nothing saved):
    ///  * a level where the player owns no vehicle factory (L02 red): the vehicle factory nearest to the player's fort is given to the player for
    ///    the check through its own flag (the game's zone capture, AwardZone: the factory with its zone), and handed back to its owner after;
    ///    a factory without a flag of its own (a fort's zone) is told, not taken;
    ///  * the camera over the gate the unit leaves by: a gate that opens AWAY from the normal view (the fort's back gate faces north; the normal
    ///    RTS camera looks north and saw only the fort's roof over it) is looked into from outside (the view turned round, its tilt and height
    ///    the normal RTS ones); a gate facing the normal view keeps the normal camera. The view eases back to north after the check.
    /// </summary>
    public partial class K93Proof
    {
        ZObject k102Flag; Team k102Old; bool k102Staged;

        /// <summary>Exits: the player's vehicle factory, or one given to the player for the check (null = none to take: told).</summary>
        ZObject K102Factory(Team me, ZObject fort, ZObject fac)
        {
            if (fac != null) return fac;
            var any = Nearest(W, fort, o => o.Type == "vehicle_factory" && o.Kind == ZKind.Building);
            if (any == null) { Line("told  no vehicle factory on this level: run the check on a level that has one"); return null; }
            ZObject flag = null;
            foreach (var f in W.Flags) if (f != null && !f.Destroyed && f.LinkedObjects.Contains(any)) { flag = f; break; }
            if (flag == null) { Line($"told  the vehicle factory #{any.Id} ({any.Owner}) has no flag of its own to take: run the check where the player owns one"); return null; }
            k102Flag = flag; k102Old = flag.Owner; k102Staged = true;
            W.AwardZone(flag, me);
            Line($"staged: the vehicle factory #{any.Id} at tile ({any.LocX / 16},{any.LocY / 16}), {k102Old}'s, given to {me} for the check with its zone (its flag #{flag.Id}); handed back after");
            return any.Owner == me ? any : null;
        }

        /// <summary>Exits, at the end: the staged zone back to its owner, the camera's heading released (it eases back to north).</summary>
        void K102Restore()
        {
            if (k102Staged && k102Flag != null && !k102Flag.Destroyed) { W.AwardZone(k102Flag, k102Old); Line($"staged zone handed back to {k102Old}"); }
            k102Staged = false;
            if (cam) cam.yawHeld = false;
        }

        /// <summary>Build: the camera on the gate's open side (see the class notes); the normal view when the gate faces it.</summary>
        void K102Look(ZObject b, FactoryView fv)
        {
            if (!cam) return;
            if (!(b.GetCreationPoint(out int ax, out int ay) && b.GetCreationMovePoint(out int bx, out int by)) || (ax == bx && ay == by))
            {
                cam.yawHeld = false;
                if (fv) { var c = fv.transform.position; Focus(c + fv.transform.forward * 6f, 30f); }
                return;
            }
            Vector3 a = ZGame.PxToWorld(ax, ay), e = ZGame.PxToWorld(bx, by), dir = e - a; dir.y = 0f; dir.Normalize();
            bool away = dir.z > 0.3f;                                                                       // the normal view looks north (+z)
            cam.yawHeld = away; cam.yaw = away ? Mathf.Atan2(-dir.x, -dir.z) * Mathf.Rad2Deg : 0f;
            Focus(e + dir * 2f, 30f);
            Line($"      camera: {(away ? $"turned {cam.yaw:0} deg to look into the {b.Type} gate from outside (it opens away from the normal view)" : "the normal view (the gate faces it)")}, 30 m over its outer mouth");
        }
    }
}
#endif
