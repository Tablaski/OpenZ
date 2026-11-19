using System;
using System.Collections.Generic;

namespace OpenZ.Sim
{
    /// <summary>
    /// OpenZ 1.5 deviation DV201 "level 18's shipping containers in the water" (kit level18_containers). Z has no such object. Each of the
    /// half-sunk, tilted rusty containers in level 18's water is a destructible map item made EXACTLY as the game's other destructible objects
    /// in water, the Car Park's harbour wrecks (DV39, Sim/ZWorld.Wrecks.cs), on Zod's own rules for destroyable map barriers (rocks, huts, map
    /// objects: orock.cpp:678-686, ohut.cpp:247, omapobject.cpp:77):
    /// <list type="bullet">
    /// <item>a neutral map item of <see cref="WreckType"/> with Zod's map-object hit points (map_item.map_item.health x MAX_UNIT_HEALTH = 1667),
    /// attacked by explosives only (Toughs, tanks, cannons, the MLRS, grenades; rifles cannot hurt it) and hurt by any missile splash;</item>
    /// <item>its whole footprint (<see cref="L18ContainerTiles"/>: 3 x 1 or 1 x 3 shallow-water tiles; 2 x 3 for the west one of the leaning pair) is a
    /// destroyable impassable: robots and the amphibious Bizon path round it, units with explosives blast it when it is in their way, it bars a
    /// line of fire like a rock (ZWorld.EngageBarrierBetween), the view keeps drawn units off it (ZClearance: HasDestroyableBarrier);</item>
    /// <item>destroyed: the game's Death event, the kill, every footprint tile gets back its map class (WreckImpassables). No debris missile (Zod
    /// throws one for map_object types only); the view breaks it up and leaves its wreck low in the water (View/OzL18Containers.cs);</item>
    /// <item>not a car wreck (<see cref="ZObject.VehicleWreck"/> false): the Mammoth and the Bizon do not crush a steel box.</item>
    /// </list>
    /// The spots were chosen and are proven by the kit's counted check (Editor/OzV15L18ContainersCount.cs): every tile shallow water, passable
    /// and free at map load; none in a lane's keep-clear area, none within 2 tiles of a flag, a building, a cannon or a start unit; every
    /// bridge (standing or destroyed) 3 tiles or more off its span's side and 5 past its ends (the crane's approach and repair area); no robot
    /// region split; the robots' fort-to-fort wade the same length and every way between the flags, the fort gates and a 9-tile lattice at most
    /// 1 tile longer (the harbour wrecks' rule); the wade round a container from right beside it at most 4.5 tiles longer (as round the river
    /// stumps, DV197); the Bizon's river crossing the same. Made at map load AFTER every other object (ZWorld.Create's last object step), so
    /// every older object keeps its id; deterministic (fixed data of this file, object order, no random draw, no clock). Nothing here reads a
    /// level id: the map file is tested (<see cref="L18Map"/>). Switch <see cref="L18ContainersDeviation"/>: the game sets it at every start
    /// (View/ZGame.cs, off in 'Zod exact'); headless default off, so every older check, bot line and parity row keeps its numbers.
    /// </summary>
    public sealed partial class ZWorld
    {
        /// <summary>DV201 switch (off = Zod: level 18's water is open everywhere, no container).</summary>
        public static bool L18ContainersDeviation = false;
        /// <summary>Tile x, tile y, width, height (tiles) of each container's footprint (see View/OzL18Containers.cs for the drawn pose).</summary>
        public static readonly int[] L18ContainerTiles =
        {
            7, 29, 3, 1,      // N1 the north-west harbour mouth (west of the dry crossing), tilted
            92, 44, 1, 3,     // N2 the east side canal north of the river, nose down
            99, 28, 2, 3,     // N3 the north canal east of the fort: a pair leaning on each other (2 tiles wide: it leans out over its west tile) ...
            101, 28, 1, 3,    // N4 ... its partner
            47, 64, 3, 1,     // P1 the west pool beside the west bridge's span, rolled
            33, 70, 1, 3,     // S1 the west side canal south of the river, on its side
            27, 100, 1, 3,    // S2 the south-west canal arm, tilted
        };
        public static readonly string[] L18ContainerIds = { "N1", "N2", "N3", "N4", "P1", "S1", "S2" };
        /// <summary>The containers made at map load (read only for the view and the checks).</summary>
        public readonly List<ZObject> L18Containers = new List<ZObject>();
        /// <summary>Footprints refused at map load (a tile not open shallow water, or an object on it).</summary>
        public int L18ContainersRefused { get; private set; }

        /// <summary>ZWorld.Create hook, the last object step (after DV198's drivers): each container as a DV39-style wreck map item.</summary>
        void AddL18Containers()
        {
            L18Containers.Clear(); L18ContainersRefused = 0;
            if (!L18ContainersDeviation || !L18Map) return;
            var t = L18ContainerTiles;
            for (int k = 0; k + 3 < t.Length; k += 4)
            {
                int tx = t[k], ty = t[k + 1], w = t[k + 2], h = t[k + 3];
                if (!WreckSpotFree(tx, ty, w, h)) { L18ContainersRefused++; continue; }   // the harbour wrecks' own test: every tile shallow water, passable, no object
                var o = CreateObject("map_item", WreckType, tx * 16, ty * 16, Team.Neutral);
                o.WidthPix = w * 16; o.HeightPix = h * 16; o.SetCords(tx * 16, ty * 16);
                o.IsDestroyableImpass = true;                                                 // SetClassFlags gave AttackedByExplosives (a map item)
                o.VehicleWreck = false;                                                       // a steel container, not a car wreck: never crushed (DV193)
                o.MaxHealth = o.Health = (int)(S.map_item_health * ZConst.MAX_UNIT_HEALTH);   // Zod map object HP (1667)
                SetMapImpassables(o);                                                         // the whole footprint, destroyable (WreckImpassables)
                L18Containers.Add(o);
            }
        }

        /// <summary>DV201: the container index of a sim object (-1 = not one of them); the view and the checks.</summary>
        public int L18ContainerIndex(ZObject o)
        {
            if (o == null) return -1;
            var t = L18ContainerTiles;
            for (int k = 0; k + 3 < t.Length; k += 4)
                if (o.Type == WreckType && o.LocX == t[k] * 16 && o.LocY == t[k + 1] * 16 && o.WidthPix == t[k + 2] * 16 && o.HeightPix == t[k + 3] * 16) return k / 4;
            return -1;
        }

        /// <summary>The checks and the parity report: DV201 held off until disposed (a Play session leaves it on).</summary>
        public static IDisposable L18ContainersOff() => new L18ContainersOffHold();
        sealed class L18ContainersOffHold : IDisposable
        {
            readonly bool a = L18ContainersDeviation;
            public L18ContainersOffHold() { L18ContainersDeviation = false; }
            public void Dispose() { L18ContainersDeviation = a; }
        }
    }
}
