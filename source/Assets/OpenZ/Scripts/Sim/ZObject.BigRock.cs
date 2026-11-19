using System;

namespace OpenZ.Sim
{
    /// <summary>
    /// DV164 "the heavy tank clears a rock it clips" (Sim/ZObject.HeavyRock.cs) for the MAMMOTH and the BIZON
    /// (DV180 / DV181, Sim/ZBigTanks.cs) too. Everything is the heavy tank's rule, word for word: a crewed tank standing idle for
    /// <see cref="ZWorld.HeavyRockIdleSeconds"/>, no enemy unit in its agro reach (seen or not), turns its gun on the nearest rock it clips and shoots it
    /// until it is gone (the gun's turn wait, the game's own shells aimed at the rock's foot, the blast hurting that rock alone, the Mammoth's two barrels
    /// one-two like the heavy's), then stands idle again; it never moves; an order, a target, an agro win at once. Only WHAT CLIPS is theirs, from their
    /// bigger drawn models.
    /// <para>What clips, measured on the drawn models (Models/BigTanks/mammoth_game.glb, bizon_game.glb: game metres, the prefab at scale 1, every
    /// vehicle drawn at OPTIONS VEHICLE SIZE like the heavy tank; kit r14_r16 tools/measure_glb.py, which gives the heavy tank v2 its known 5.21 x 4.11 m,
    /// ring 0.36 m, tips 4.12 m), 1 m = 8 px at full size (<see cref="BigRockShape"/>):</para>
    /// <list type="bullet">
    /// <item>the MAMMOTH: hull 7.05 x 4.99 m, the turret ring 0.47 m behind its middle, the barrels' tips 4.37 m from the ring (the turret's farthest
    /// point 4.39 m), the hull's farthest point from the ring 4.37 m: like the heavy tank its hull lies inside the circle its barrels sweep, so the
    /// heavy's one test with its own numbers: the rock's foot within 35 px of the ring + the rock's 9 px.</item>
    /// <item>the BIZON: hull 7.47 x 4.97 m, the ring 0.30 m behind its middle, a SHORT barrel (tips 2.50 m from the ring) on a long hull that reaches
    /// 4.19 m ahead of the ring, 3.28 m behind it, 2.49 m to each side, at most 4.55 m (the front corners are cut): the barrels' circle (20 px) does not
    /// cover its hull, so a rock also clips where the hull stands on it: the foot within the rock's 9 px of the hull's box (34 px ahead, 26 behind,
    /// 20 each side of the ring, in the hull's own heading) and within 36 px + 9 of the ring (the cut corners).</item>
    /// </list>
    /// <para>The hull's heading is its last move (<see cref="ZObject.FaceX"/>, as whole numbers x 1024 like the Mammoth's ram); a tank that has not moved
    /// yet has no known heading: its ring at its middle (the heavy's rule) and its hull at any heading: the circle of its hull's farthest point from its
    /// middle (the Mammoth 3.91 m, the Bizon 4.29 m) or of its barrels, whichever is bigger. Whole pixels at the drawn size
    /// (<see cref="ZWorld.HeavyRockDrawnPct"/>), whole-number sums, the object list's order (the nearest rock, the first of equals), no random draw of
    /// its own: deterministic. The heavy tank's own test is untouched (it never comes here). Switch: <see cref="ZWorld.HeavyRockDeviation"/> (DV164's own:
    /// on in a game, off in 'Zod exact', off by default headless, in the parity rows and in every counted check): with it off nothing of this runs.</para>
    /// </summary>
    public partial class ZObject
    {
        /// <summary>DV164's tanks: the heavy tank and, 1.5 r14_r16, the Mammoth and the Bizon.</summary>
        bool HeavyRockType => Type == "heavy" || ZBigTanks.Is(Type);

        /// <summary>HeavyRockPick for the Mammoth and the Bizon: the nearest rock (from the turret ring) this tank clips that it can fire at, the first in
        /// the object list of equals, or null.</summary>
        ZObject BigRockPick()
        {
            var g = ZWorld.BigRockShapeOf(Type, ZWorld.HeavyRockDrawnPct);
            BigRockFrame(g, out int rx, out int ry, out bool head, out int hx, out int hy);
            long best = long.MaxValue; ZObject pick = null;
            foreach (var o in World.Objects)
            {
                if (o.Type != "rock" || o.Destroyed || o.KillAt >= 0 || o.MaxHealth <= 0 || !o.CanBeDestroyed) continue;
                long dx = o.CenterX - rx, dy = o.LocY + ZWorld.RockFootPx - ry, d2 = dx * dx + dy * dy;
                if (d2 >= best || !ZWorld.BigRockClips(g, head, hx, hy, dx, dy) || !CanAttackObject(o) || !WithinAttackRadius(o)) continue;
                best = d2; pick = o;
            }
            return pick;
        }

        /// <summary>The turret ring (whole px) and the hull's heading (x 1024) from the last move; no heading: the ring at the middle.</summary>
        void BigRockFrame(in ZWorld.BigRockShape g, out int rx, out int ry, out bool head, out int hx, out int hy)
        {
            rx = CenterX; ry = CenterY; hx = hy = 0;
            double fx = FaceX, fy = FaceY, fl = Math.Sqrt(fx * fx + fy * fy);
            head = fl > 1e-6;
            if (!head) return;
            hx = (int)Math.Round(fx * 1024.0 / fl); hy = (int)Math.Round(fy * 1024.0 / fl);
            rx -= (int)Math.Round(fx / fl * g.Back); ry -= (int)Math.Round(fy / fl * g.Back);
        }

        /// <summary>Checks: this big tank clips the rock r now (its foot, at the drawn size), by the rule's own sum.</summary>
        public bool BigRockClipsNow(ZObject r)
        {
            if (r == null || !ZBigTanks.Is(Type)) return false;
            var g = ZWorld.BigRockShapeOf(Type, ZWorld.HeavyRockDrawnPct);
            BigRockFrame(g, out int rx, out int ry, out bool head, out int hx, out int hy);
            return ZWorld.BigRockClips(g, head, hx, hy, r.CenterX - rx, r.LocY + ZWorld.RockFootPx - ry);
        }
    }

    public sealed partial class ZWorld
    {
        /// <summary>A big tank's outline for DV164, whole px (1 m = 8 px): Back = the turret ring behind the hull's middle; Barrel = the barrels' (and
        /// turret's) reach from the ring; Front / Rear / Half = the hull's box from the ring (ahead, behind, each side); Far = the hull's farthest point
        /// from the ring; FarMid = from the hull's middle (a tank that has not moved yet).</summary>
        public readonly struct BigRockShape
        {
            public readonly int Back, Barrel, Front, Rear, Half, Far, FarMid;
            public BigRockShape(int back, int barrel, int front, int rear, int half, int far, int farMid) { Back = back; Barrel = barrel; Front = front; Rear = rear; Half = half; Far = far; FarMid = farMid; }
            /// <summary>At a drawn size (per cent), each to the nearest whole pixel (the heavy tank's rounding).</summary>
            public BigRockShape At(int pct) => new BigRockShape(S(Back, pct), S(Barrel, pct), S(Front, pct), S(Rear, pct), S(Half, pct), S(Far, pct), S(FarMid, pct));
            static int S(int v, int pct) => (v * pct + 50) / 100;
            public override string ToString() => $"ring {Back} px behind the middle, barrels {Barrel} px, hull {Front} ahead / {Rear} behind / {Half} each side of the ring, at most {Far} px (from the middle {FarMid})";
        }

        /// <summary>Measured at full size (kit r14_r16 tools/measure_glb.py): the Mammoth 0.468 / 4.386 / 4.331, 2.721, 2.501 / 4.372 / 3.908 m; the Bizon
        /// 0.300 / 2.495 / 4.191, 3.282, 2.491 / 4.548 / 4.293 m.</summary>
        public static readonly BigRockShape MammothRock = new BigRockShape(4, 35, 35, 22, 20, 35, 31), BizonRock = new BigRockShape(2, 20, 34, 26, 20, 36, 34);

        /// <summary>A big tank's outline at a drawn size.</summary>
        public static BigRockShape BigRockShapeOf(string type, int pct) => (type == ZBigTanks.Mammoth ? MammothRock : BizonRock).At(pct);

        /// <summary>A rock's foot (dx, dy) from the turret ring clips the tank: within the barrels' sweep + the rock's radius at any bearing; or, with a
        /// known heading (hx, hy, x 1024), within the rock's radius of the hull's box and within its farthest point + the rock's radius; with no heading,
        /// within the hull's farthest point from the middle + the rock's radius (any heading). Whole numbers only.</summary>
        public static bool BigRockClips(in BigRockShape g, bool head, int hx, int hy, long dx, long dy)
        {
            long r = HeavyRockRadiusPx, d2 = dx * dx + dy * dy;
            long sweep = g.Barrel + r;
            if (d2 <= sweep * sweep) return true;                                           // the barrels sweep all round (the heavy tank's own test)
            long far = (head ? g.Far : Math.Max(g.Barrel, g.FarMid)) + r;
            if (d2 > far * far) return false;                                               // never past the hull's farthest point
            if (!head) return true;                                                         // not moved yet: the hull may stand at any heading
            long a = dx * hx + dy * hy, s = dy * hx - dx * hy;                              // along / across the hull, x 1024
            long ca = Math.Max(-g.Rear * 1024L, Math.Min(g.Front * 1024L, a)), cs = Math.Max(-g.Half * 1024L, Math.Min(g.Half * 1024L, s));
            long ea = a - ca, es = s - cs;
            return ea * ea + es * es <= r * r * 1024L * 1024L;                              // within the rock's radius of the hull's box
        }

        /// <summary>The parity row's words.</summary>
        public static string BigRockText => $"the Mammoth: {MammothRock}; the Bizon: {BizonRock}; + the rock's {HeavyRockRadiusPx} px; whole px at the drawn size, heading from the last move (x 1024)";
    }
}
