using System;

namespace OpenZ.Sim
{
    /// <summary>
    /// FAILS A14.
    /// <para>Cause: DV26 (combat_nb_v1, Sim/ZObject.CombatNB.cs WallAim) aims every rocket, shell and grenade at a building at the point of its
    /// walls nearest to the shooter, 4 px in. A bridge has no wall table (ZClearance.Mesh), so its "walls" are its whole rectangle and that point
    /// is the middle of the bridge's near END, the entrance, when the shooter stands on the road before it (its near corner from the side).
    /// Zod's scatter (+-16 px, a grenade +-24 px) then spreads the shots from 1.5 m in front of the entrance to 2.4 m onto it: at the feet of a
    /// unit that stands at the bridge head, and nearly half of them (45 %, counted) outside the bridge (the bridge takes only part of those).</para>
    /// <para>DV210: the aim point at a bridge is on the deck's centre line, <see cref="ZWorld.BridgeAimDepthPx"/> (40 px = 5 m) past the end
    /// nearer to the shooter, spread +-<see cref="ZWorld.BridgeAimSpreadPx"/> (8 px = 1 m) along the deck from shot to shot by an integer hash
    /// of the shooter, the bridge and the tick (no random draw: Zod's random stream is drawn exactly as before, the scatter below included),
    /// never past the middle of the bridge (a 10 m bridge 4-5 m, a 12 m one 4-6 m, a 22-24 m one 4-6 m). A shooter beside the deck (a robot
    /// wading in the river by it) aims at the deck point nearest to it, at least that far in; a shooter standing on the deck aims about 5 m
    /// ahead of itself along the deck, never in its last 2 m. Zod's scatter is added after it as before, so the shots land 2-8 m onto the deck,
    /// every one inside the bridge.</para>
    /// <para>Unchanged: the range (measured to the bridge's nearest point, DV26: units stop before the bridge where they stop today), every
    /// weapon's damage and blast radius, DV207's 46546 hit points, no friendly fire (ZWorld.ProcessMissileDamage), every other target (a
    /// building keeps DV26's nearest wall), the heavy tank's second barrel (Zod's aim at the bridge's centre, on the deck already), a Tough
    /// hatch driver's rocket, the Pyro's flame (DV113: its ground fire at the near end its flame touches). The bridge takes the full blast of
    /// every shot now, so it falls to somewhat fewer shots than with DV210 off (counted in the kit's README; the MLRS stays at Z's 6).</para>
    /// <para>Switch <see cref="ZWorld.BridgeDeckAimDeviation"/>: View/ZGame.cs on at every level start, off in 'Zod exact'; headless default
    /// off; off in every counted check unless the check sets it (Editor/OzV11Kit.cs); the parity report's Zod rows and probes hold it off; its
    /// row DV210 (Editor/OpenZParity.V15BridgeAim.cs). Integers only, no random draw, deterministic. The view half (shells drawn on the deck at
    /// this point, the MLRS's cosmetic ripple kept off the shooter's side): View/OzBridgeDeckHits.cs.</para>
    /// </summary>
    public sealed partial class ZWorld
    {
        /// <summary>DV210 switch (static like the others). View/ZGame.cs sets it at every level start (off in 'Zod exact').</summary>
        public static bool BridgeDeckAimDeviation = false;

        /// <summary>DV210: how far past the bridge's near end a shot is aimed (px; 40 = 5 m), never past the middle of the bridge.</summary>
        public const int BridgeAimDepthPx = 40;

        /// <summary>DV210: the spread of that depth from shot to shot (+- px; 8 = 1 m).</summary>
        public const int BridgeAimSpreadPx = 8;

        public static IDisposable BridgeDeckAimOff() => new BridgeDeckAimHold(false);
        public static IDisposable BridgeDeckAimOn() => new BridgeDeckAimHold(true);
        sealed class BridgeDeckAimHold : IDisposable
        {
            readonly bool was = BridgeDeckAimDeviation;
            public BridgeDeckAimHold(bool on) { BridgeDeckAimDeviation = on; }
            public void Dispose() { BridgeDeckAimDeviation = was; }
        }
    }

    public partial class ZObject
    {
        /// <summary>DV210, in DV26's WallAim: where a rocket, shell or grenade of this unit at bridge t is aimed (before Zod's scatter).
        /// False = t is no bridge or the switch is off (DV26's nearest wall point, as before).</summary>
        internal bool BridgeDeckAim(ZObject t, out int tx, out int ty)
        {
            tx = ty = 0;
            if (!ZWorld.BridgeDeckAimDeviation || t == null || !t.IsBridge) return false;
            uint tick = World != null ? unchecked((uint)World.TickIndex) : 0u;
            BridgeDeckPoint(t, CenterX, CenterY, BridgeAimHash(unchecked((uint)Id), unchecked((uint)t.Id), tick), out tx, out ty);
            return true;
        }

        /// <summary>DV210: the aim point on bridge b for a shooter at (fromX, fromY), `roll` choosing the depth inside its spread (any uint).
        /// Along the bridge: the end nearer to the shooter is its near end; depth past it = max(the shooter's own depth, 40 px) +- 8 px, at most
        /// half the bridge's length; a shooter on the deck: its own depth + 40 px +- 8 px, at most 16 px short of the far end. Across: the
        /// deck's centre line. Integers only; the counted check calls it too.</summary>
        public static void BridgeDeckPoint(ZObject b, int fromX, int fromY, uint roll, out int tx, out int ty)
        {
            bool vert = b.Type == "bridge_vert";
            int a0 = vert ? b.LocY : b.LocX, len = vert ? b.HeightPix : b.WidthPix;                 // along the deck (px)
            int c0 = vert ? b.LocX : b.LocY, wid = vert ? b.WidthPix : b.HeightPix;                 // across it
            int s = (vert ? fromY : fromX) - a0, c = (vert ? fromX : fromY) - c0;
            bool fromStart = 2 * s <= len;                                                          // nearer the north / west end
            int p = fromStart ? s : len - s;                                                        // the shooter's own depth past its near end (< 0: before it)
            bool onDeck = p > 0 && c >= 0 && c < wid;                                               // the shooter stands on this bridge
            int centre = onDeck ? p + ZWorld.BridgeAimDepthPx : Math.Max(p, ZWorld.BridgeAimDepthPx);
            int cap = onDeck ? len - 2 * ZWorld.BridgeAimSpreadPx : len / 2;                        // never past the middle (on the deck: never off it)
            int lo = Math.Min(centre - ZWorld.BridgeAimSpreadPx, cap), hi = Math.Min(centre + ZWorld.BridgeAimSpreadPx, cap);
            int depth = lo + (int)(roll % (uint)(hi - lo + 1));
            int along = fromStart ? a0 + depth : a0 + len - depth, across = c0 + (wid >> 1);
            tx = vert ? across : along; ty = vert ? along : across;
        }

        /// <summary>DV210: an integer mix of three numbers (the shooter, the bridge, the tick): the depth's spread without a random draw.</summary>
        internal static uint BridgeAimHash(uint a, uint b, uint c)
        {
            uint h = unchecked(a * 2654435761u ^ b * 40503u ^ c * 374761393u ^ 0x9E3779B9u);
            h ^= h >> 15; h = unchecked(h * 2246822519u); h ^= h >> 13; h = unchecked(h * 3266489917u); h ^= h >> 16;
            return h;
        }
    }
}
