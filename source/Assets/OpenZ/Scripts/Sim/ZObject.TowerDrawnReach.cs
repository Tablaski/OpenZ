using System;

namespace OpenZ.Sim
{
    /// <summary>
    /// OpenZ deviation DV208 "the reach to a fort tower gun is measured to where its tower is drawn" (1.5 kit tower_reach_dv208; owner R3.39,
    /// 13 Nov 2026: a red medium tank drives at the blue fort's missile tower and "does not shoot and keeps going and then even shoots the sniper
    /// on top of blue fort which is further"; his choice:
    /// <para>Cause (kit README, proof/scene_forts.txt): Zod keeps a fort's tower guns on its four turret spots, fort-local px (16,0) (112,0)
    /// (16,48) (112,48) (bfort.cpp:534-548), all four in the north part of the fort's rectangle. The view (View/TowerMounts.cs, crews_v1 /
    /// howitzer_towers_v1) draws each one on a corner tower of the fort model, on the pad of that corner: measured on all ten forts of the five
    /// level scenes, the drawn gun stands 56 px east / west and 27-30 px (north row) or 74-77 px (south row) SOUTH of its rule spot, the same
    /// on every level and both teams. Level 7's blue spot (112,48): rule centre (720,160), drawn (728,235), 9.4 m nearer to a unit coming
    /// from the south. So in the rules the tower was out of a tank's reach while it looked close, and the roof sniper (rule spot at the
    /// fort's edge) was the nearer target.</para>
    /// <para>The rule: a weapon's range at a fort tower gun (<see cref="IsTowerGun"/>) is measured to where its tower is drawn
    /// (<see cref="TowerDrawnAt"/>) when that is nearer to the attacker than the gun's rule centre, else to the rule centre as before (no
    /// attacker loses reach anywhere). Added in <see cref="HighRange"/> as an extra pad (<see cref="TowerDrawnPad"/>: the attacker's distance
    /// to the rule centre minus its distance to the drawn tower, whole pixels, rounded so that "distance to the rule centre within the range +
    /// pad" is "distance to the drawn tower within the range", to 1 px), so every reader of a range at one target from where the unit stands
    /// gets it the same, like DV204's tower body (24 px, kept, now round the drawn tower): WithinAttackRadius (when it fires), DV204's "tower
    /// gun in reach first", ApproachPlan's "already in reach", the duel's bands, the sniper's and the Electrobot's steady rules, the CPU's
    /// "really fighting", the second weapons (the mech's hand gun, the hatch driver, the crane rifle). Every attacker, both sides, every fort
    /// on every level.</para>
    /// <para>
    /// blue fort's missile tower:
    /// the rule above measured only the ATTACKER's range to the drawn tower; the tower gun still measured its own range from its
    /// rule centre, 75 px north of where it is drawn on level 7's south row. A 120 px unit (medium tank, light tank, grunts) hit the tower
    /// from up to 144 px of the drawn tower (219 px of the rule centre) while the tower reached 158 px of its rule centre: a 62 px band south
    /// of every south-row tower in which the unit hits the tower and the tower cannot answer (the clip: 0 shots). Now ONE distance, used both
    /// ways (<see cref="TowerPairPad"/>: the pair's nearest points, a tower gun's points being its rule centre and its drawn tower): the
    /// tower reaches a unit from where it is drawn exactly as far as its range, a unit that hits it from inside the tower's own reach is
    /// always hit back, and (<see cref="TowerBodyOver"/>) a weapon whose table range is shorter than the tower gun's never outreaches it
    /// through DV204's 24 px body. A tower gun's neighbour scan (<see cref="TowerScanPad"/>) and DV204's look square (ZWorld.TowerDrawnLookPad) reach as far.</para>
    /// <para>Not changed: the gun's rule spot, its aim, damage and target choice (by the same distance as its attackers since round 6), the
    /// line-of-sight test (the gun's own fort never blocks a line to it, ZSight.ExemptOf), the walking plans' spots (they stop the moment
    /// WithinAttackRadius says yes), the view and its range rings, any other target. Whole pixels, integer square roots, no random draw, no
    /// event; the drawn point is worked out once per gun from its fort (type, place, size) and kept.</para>
    /// <para>Switch <see cref="ZWorld.TowerDrawnReachDeviation"/>: View/ZGame.cs turns it on at every start, off in 'Zod exact' (Zod has
    /// no drawn tower); static default off, so headless worlds, the parity's Zod rows and every older counted check keep the rules as they
    /// were. Off = P before this kit, tick for tick.</para>
    /// </summary>
    public partial class ZObject
    {
        /// <summary>DV208: the drawn tower point of this fort tower gun (px), worked out once; int.MinValue = not yet, int.MaxValue = none.</summary>
        int towerDrawnX = int.MinValue, towerDrawnY;

        /// <summary>DV208: where this fort tower gun is drawn (px, whole): the pad of its fort's corner tower (View/TowerMounts.PadLocal
        /// and PadForSpot, the fort model as OpenZSetup places it: centre of the fort's rectangle, fort_front turned 180 deg, fort_back 0).
        /// false (x, y = the rule centre): no fort tower gun, or not on a fort's turret spot.</summary>
        public bool TowerDrawnAt(out int x, out int y)
        {
            x = CenterX; y = CenterY;
            if (!IsTowerGun || World == null) return false;
            if (towerDrawnX == int.MinValue)
            {
                towerDrawnX = int.MaxValue;
                foreach (var f in World.Objects)
                {
                    if (!f.IsFort) continue;
                    int lx = LocX - f.LocX, ly = LocY - f.LocY;
                    if ((lx != 16 && lx != 112) || (ly != 0 && ly != 48)) continue;   // the four turret spots (bfort.cpp:534-548; ZWorld.MarkFortTurretCannons)
                    bool front = f.Type == "fort_front", north = ly < 24;
                    towerDrawnX = f.LocX + (f.WidthPix >> 1) + (lx < 64 ? -ZWorld.TowerDrawnSidePx : ZWorld.TowerDrawnSidePx);
                    towerDrawnY = f.LocY + (f.HeightPix >> 1) + (north ? -(front ? ZWorld.TowerDrawnFarPx : ZWorld.TowerDrawnNearPx) : (front ? ZWorld.TowerDrawnNearPx : ZWorld.TowerDrawnFarPx));
                    break;
                }
            }
            if (towerDrawnX == int.MaxValue) return false;
            x = towerDrawnX; y = towerDrawnY;
            return true;
        }

        /// <summary>DV208: the extra range (px) of a weapon of this unit at t when one of the two is a fort tower gun, so that its range
        /// measured centre to centre (as WithinAttackRadius tests it) reaches as far as the same range measured over the pair's nearest
        /// points (<see cref="TowerPairPad"/>, the same number from either end). An attacker at a fort tower gun: the pad of the 14 Nov
        /// rule, unchanged (to the drawn tower when that is nearer); round 6 (kit tower_reach_fair_r6): a fort tower gun at a unit (robot,
        /// vehicle, gun) the same pad from its drawn tower, so both measure ONE distance; less <see cref="TowerBodyOver"/> when this
        /// weapon's table range is shorter than the tower gun's. 0 when the switch is off, for a weaponless unit, t itself, a pair
        /// without a fort tower gun, or a tower gun at a building / map item (its rule centre as before).</summary>
        internal int TowerDrawnPad(int radius, ZObject t)
        {
            if (!ZWorld.TowerDrawnReachDeviation || radius <= 0 || t == null || t == this) return 0;
            bool theirs = t.IsTowerGun;
            if (!theirs && (!IsTowerGun || !t.IsUnit)) return 0;
            int pad = TowerPairPad(t);
            return theirs ? pad - TowerBodyOver(radius, t) : pad;
        }

        /// <summary>DV208 round 6: the pad (px) of the pair (this, t), the same number from either end: (distance between the two centres,
        /// rounded up) - (the shortest distance between a point of one and a point of the other, the centre-to-centre pair left out, rounded
        /// down), never below 0. A fort tower gun's points: its rule centre and its drawn tower (<see cref="TowerDrawnAt"/>); anything
        /// else: its centre. So "centre distance within range + pad" is "the nearest-point distance within range" to 1 px, for the
        /// attacker and for the tower alike. 0 when neither is a fort tower gun on a turret spot.</summary>
        internal int TowerPairPad(ZObject t)
        {
            bool a = TowerDrawnAt(out int ax, out int ay), b = t.TowerDrawnAt(out int bx, out int by);
            if (!a && !b) return 0;
            long best = long.MaxValue;
            if (b) best = Math.Min(best, TowerSq(bx - CenterX, by - CenterY));                 // this centre to t's drawn tower (the 14 Nov rule)
            if (a) best = Math.Min(best, TowerSq(t.CenterX - ax, t.CenterY - ay));             // this drawn tower to t's centre (round 6: the tower's own side)
            if (a && b) best = Math.Min(best, TowerSq(bx - ax, by - ay));                      // two tower guns: drawn tower to drawn tower
            long pad = ZWorld.TowerSqrtUp(TowerSq(t.CenterX - CenterX, t.CenterY - CenterY)) - ZWorld.TowerSqrtDown(best);
            return pad > 0 ? (int)pad : 0;
        }

        static long TowerSq(long dx, long dy) => dx * dx + dy * dy;

        /// <summary>DV208 round 6: how far (px) DV204's tower body would carry a weapon of this unit (range `radius`) past the reach of fort
        /// tower gun t back at it, when the weapon's table range is shorter than the gun's: cut off, so a shorter gun never outreaches the
        /// tower (on the default table: a laser at a missile tower 2 px, a 120 px unit at a 'gun' tower 3 px; everything else 0). The
        /// body stays for an equal or longer table range. 0 otherwise.</summary>
        int TowerBodyOver(int radius, ZObject t)
        {
            if (radius > t.AttackRadius) return 0;
            int mine = HighRangeFlat(radius, t) + TowerBodyPad(radius, t), back = t.HighRangeFlat(t.AttackRadius, this) + t.TowerBodyPad(t.AttackRadius, this);
            return mine > back ? mine - back : 0;
        }

        /// <summary>DV208 round 6 (ZWorld.NeighbourGrid.cs NbRangeReach): how much farther (px) from its rule centre a fort tower gun's
        /// range reaches a unit through <see cref="TowerPairPad"/>: the distance to its drawn tower, rounded up, + 1 (the pad is never
        /// more). 0 for any other unit or with the switch off.</summary>
        internal int TowerScanPad()
        {
            if (!ZWorld.TowerDrawnReachDeviation || !TowerDrawnAt(out int x, out int y)) return 0;
            return (int)ZWorld.TowerSqrtUp(TowerSq(x - CenterX, y - CenterY)) + 1;
        }
    }

    public sealed partial class ZWorld
    {
        /// <summary>DV208 switch (code switch; View/ZGame.cs: on at every start, off in 'Zod exact'; headless default off).</summary>
        public static bool TowerDrawnReachDeviation = false;

        /// <summary>DV208: the drawn tower pads from the centre of the fort's rectangle (px, 8 px = 1 m; View/TowerMounts.PadLocal x 8, the
        /// fort model turned as OpenZSetup places it): 56 px east / west; fort_front (turned 180 deg): north pads 53 px north, south pads
        /// 43 px south; fort_back: north pads 43 px north, south pads 53 px south. Measured on all ten forts of the five level scenes (kit
        /// proof/scene_forts.txt), within 0.5 px.</summary>
        public const int TowerDrawnSidePx = 56, TowerDrawnFarPx = 53, TowerDrawnNearPx = 43;

        /// <summary>DV208 round 6 (kit tower_reach_fair_r6): how much farther (px) DV204's look square (FortGunLookPx, Sim/ZObject.FortGunAnswer.cs:
        /// the fort tower guns a unit looks for, the ones shooting at it and the ones it can hit) reaches while the switch is on. A fort tower gun's
        /// reach both ways is padded by at most its drawn-tower distance + 1 (77.4 px on a back fort's south row: 79), so the square must grow by
        /// that much or a unit shelled by a south-row howitzer tower from 220 px of its drawn tower (297 px of its rule centre) would not see it.</summary>
        public static int TowerDrawnLookPad => TowerDrawnReachDeviation ? TowerDrawnLookPx : 0;
        public const int TowerDrawnLookPx = 80;

        /// <summary>DV208: whole-pixel square root, rounded down (exact: corrected in integers after the double estimate).</summary>
        public static long TowerSqrtDown(long v)
        {
            if (v <= 0) return 0;
            long r = (long)Math.Sqrt(v);
            while (r > 0 && r * r > v) r--;
            while ((r + 1) * (r + 1) <= v) r++;
            return r;
        }

        /// <summary>DV208: whole-pixel square root, rounded up.</summary>
        public static long TowerSqrtUp(long v) { long r = TowerSqrtDown(v); return r * r == v ? r : r + 1; }

        /// <summary>The parity report and the older counted checks: no DV208 while this lives (a Play session leaves the switch on).</summary>
        public static IDisposable TowerDrawnReachOff() => new TowerDrawnReachHold();
        sealed class TowerDrawnReachHold : IDisposable
        {
            readonly bool was = TowerDrawnReachDeviation;
            public TowerDrawnReachHold() { TowerDrawnReachDeviation = false; }
            public void Dispose() { TowerDrawnReachDeviation = was; }
        }
    }
}
