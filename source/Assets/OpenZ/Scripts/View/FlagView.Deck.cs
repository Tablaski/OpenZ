using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// flag of the Unit editor's playground may stand on a ramp platform (PLATFORM or TOWER, Sim/ZWorld.Platforms.cs): on its deck or on its
    /// ramp, never on a rail or the back wall (those tiles are closed). The flag's view here:
    /// <list type="bullet">
    /// <item>The pole's foot: the rules' own height of the platform at the flag (ZWorld.PlatformHeightM, the numbers the walk surface is
    /// built from, View/OzUePlatformView.cs), so the pole stands on the deck or the slope the frame it is made, upright, never in the air and
    /// never under the deck (a field loaded from a file makes the platforms and the flags in one frame).</item>
    /// <item>The capture ring: draped on the platform's own surface (OzNeutralFlagWaves.Ground: the walk deck), its cells that step more than
    /// OzNeutralFlagWaves.StepMax off the flag's level (the floor beside the walls) are not drawn, the neutral flag waves' rule.</item>
    /// <item>A platform destroyed: the flag stays (flags are never destroyed, as in Z) and comes down to the floor with its ring, its looks and
    /// its wave (<see cref="PlatformGone"/>, from View/OzUePlatformView.cs OzUePlatformViews.Died).</item>
    /// </list>
    /// A flag on the ground and every flag of every level: not one line here runs (no platform table there: ZWorld.PlatformTablesMade false).
    /// </summary>
    public partial class FlagView
    {
        /// <summary>Checks: capture rings draped on a platform, flags brought down to the floor by a platform's fall.</summary>
        public static int DeckRings, FloorDrops;

        /// <summary>The flag stands on a ramp platform (its deck or its ramp). False in every level.</summary>
        public static bool OnPlatform(ZObject o)
        {
            var w = o != null ? o.World : null;
            return w != null && w.PlatformTablesMade && w.PlatformAtTile(o.CenterX >> 4, o.CenterY >> 4) != 0;
        }

        /// <summary>AddZoneFlag: on a platform the pole's foot is the platform's height at the flag (the rules' number), not a ray's answer.</summary>
        static void DeckFoot(ZObject o, ref Vector3 basePos)
        {
            if (!OnPlatform(o)) return;
            basePos.y = (float)o.World.PlatformHeightM(o.CenterX, o.CenterY);
        }

        /// <summary>NewRing: a flag on a platform drapes its capture ring on the platform (Fill asks <see cref="DeckY"/> for every point).</summary>
        void DeckRing(Flag f, Draped d)
        {
            if (f == null || f.fort || d == null || !OnPlatform(f.obj)) return;
            d.off = new bool[d.v.Length];
            d.footY = (float)f.obj.World.PlatformHeightM(f.obj.CenterX, f.obj.CenterY);
            DeckRings++;
        }

        /// <summary>Fill, a ring on a platform: the surface under the point (the deck, the slope, or the floor beside the walls); a point more than
        /// StepMax off the flag's level is left out (its quads are not drawn).</summary>
        float DeckY(Draped d, Vector3 p)
        {
            float y = OzNeutralFlagWaves.Ground(p.x, p.z, out _);
            d.off[d.filled] = Mathf.Abs(y - d.footY) > OzNeutralFlagWaves.StepMax;
            return y;
        }

        /// <summary>A ramp platform was destroyed (its walk surface is already off, the rules' tiles open): every flag whose centre stood on it
        /// is built again where it is now, on the floor (pole, cloth, ring, looks), and its wave forgets the deck. Returns the flags brought down.</summary>
        public static int PlatformGone(ZObject platform)
        {
            var v = I;
            if (!v || platform == null) return 0;
            var on = new List<ZObject>();
            foreach (var f in v.flags) if (!f.fort && f.obj != null && !f.obj.Destroyed && platform.Contains(f.obj.CenterX, f.obj.CenterY)) on.Add(f.obj);
            foreach (var o in on)
            {
                RemoveRuntimeFlag(o);
                AddRuntimeFlag(o);
                OzNeutralFlagWaves.Forget(o.Id);
            }
            FloorDrops += on.Count;
            if (on.Count > 0) Debug.Log($"[OpenZ] FlagView: {on.Count} flag(s) of platform #{platform.Id} brought down to the floor");
            return on.Count;
        }
    }
}
