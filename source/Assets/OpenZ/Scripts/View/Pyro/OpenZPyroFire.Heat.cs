// OpenZ (GPL-3.0) - version 1.3: what the fire tells the hot metal (View/OzFireHeat.cs). View only: the sim is read, never written.
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// Three signals that are already here. A flame reaching a hit target (Arrive, Pyro, hatch Pyro and dragon alike). Burning ground under a unit: the drawn patches, so heat follows
    /// what is seen, and the patch one flame lit is burning ground like any other: a unit that walks off it in time shows nothing, one that stays heats. Sticky fire still burning,
    /// which only holds the heat; small damage fires never count. Fire on a wreck, a ruin or a camp is no source. A level with no fire pays one int test a frame here.
    /// </summary>
    public sealed partial class OpenZPyroFire
    {
        /// <summary>A patch heats metal once its flames are up: below this fade it is fading in or out.</summary>
        public const float HeatFade = 0.35f;
        /// <summary>Inside the seeded footprint the tongues stand in, and this near in height (m): decks and tower tops.</summary>
        public const float HeatFootprint = 0.25f, HeatReachY = 2.2f;

        readonly OzFireHeat heat = new OzFireHeat();
        int patchesOn;

        /// <summary>A hit target at Arrive. A miss lights a patch, which counts by itself.</summary>
        void HeatFlame(UnitView tv, bool structure, float now) { if (tv && !structure) heat.Flame(tv, now); }

        /// <summary>From UnitView.Die and Squash, before any copy of its blocks.</summary>
        public static void HeatDeath(UnitView v) { if (I && I.ready) I.heat.Death(v); }

        void HeatClear() => heat.Clear();

        /// <summary>Water only: lava counts as wet for the fire's effects, not for the heat: there fire cools at its own pace and the stronger of the two stays.</summary>
        public static bool InWater(Vector3 p) => OpenZFxPlayer.IsWaterTile(p) && Wet(p);

        static bool PatchHeats(OpenZPyroGroundFire.Patch p) => p.active && p.fadeX >= HeatFade;

        void StepHeat(float dt, float now)
        {
            if (patchesOn == 0 && heat.Count == 0) return;
            var game = ZGame.I; if (!game) return;
            int frame = Time.frameCount;
            if (patchesOn > 0) ScanGround(game, frame);
            heat.Step(game.paused ? 0f : dt, now, frame, game.gameSpeed, game.World != null && game.World.Map.HasLava);
        }

        /// <summary>A quarter of the live units a frame: out at the box of all patches, nearly all of them, then each patch that heats.</summary>
        void ScanGround(ZGame game, int frame)
        {
            var ps = ground.patches;
            float x0 = float.MaxValue, x1 = float.MinValue, z0 = float.MaxValue, z1 = float.MinValue;
            for (int k = 0; k < ps.Count; k++)
            {
                var p = ps[k]; if (!PatchHeats(p)) continue;
                float R = p.r * 1.12f;
                if (p.c.x - R < x0) x0 = p.c.x - R;
                if (p.c.x + R > x1) x1 = p.c.x + R;
                if (p.c.z - R < z0) z0 = p.c.z - R;
                if (p.c.z + R > z1) z1 = p.c.z + R;
            }
            if (x0 > x1) return;
            var views = game.LiveViewValues;
            for (int i = 0; i < views.Count; i++)
            {
                if (((frame + i) & 3) != 0) continue;
                var v = views[i]; if (!v) continue;
                var q = v.transform.position;
                if (q.x < x0 || q.x > x1 || q.z < z0 || q.z > z1) continue;
                for (int k = 0; k < ps.Count; k++)
                {
                    var p = ps[k]; if (!PatchHeats(p)) continue;
                    float dy = q.y - p.c.y; if (dy > HeatReachY || dy < -HeatReachY) continue;
                    float R = p.r * 1.12f, dx = (q.x - p.c.x) / R, dz = (q.z - p.c.z) / R;
                    if (dx < -1f || dx > 1f || dz < -1f || dz > 1f) continue;
                    if (OpenZPyroGroundFire.Shape(dx, dz, p.seed) <= HeatFootprint) continue;
                    heat.OnGround(v, frame);
                    break;
                }
            }
        }

        /// <summary>Checks: the writer's numbers for one unit, and what it holds.</summary>
        public OzFireHeat HeatForCheck => heat;
    }
}
