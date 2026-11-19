using System;

namespace OpenZ.Sim
{
    /// <summary>
    /// OpenZ deviation DV164 "the heavy tank clears a rock it clips" (1.3). Zod blasts a rock only when a path runs into it (zobject.cpp:3716-3747); a unit that stands
    /// by one leaves it alone. The heavy tank is the one unit drawn so much bigger than its 32 x 32 px box that it goes into a rock while it stands: a long barrel on a turret
    /// that keeps turning.
    /// <para>The rule: a crewed heavy tank, standing idle for <see cref="ZWorld.HeavyRockIdleSeconds"/>, no enemy in reach, turns its gun on a rock it clips and shoots until
    /// that rock is gone, then stands idle again. It never moves. Idle is the passive check's own idle (no waypoint, no hold order, not wary of a crocodile), standing,
    /// not stunned, no target, not in the calm start, not after the game's end. Boarding and repairing are waypoints, so they are not idle. An order, an enemy target, an
    /// agro, a flag or crate taken by itself all win at once: <see cref="HeavyRockYields"/> lets the passive check look round every 0.5 s as if the tank were idle, and whatever it
    /// starts ends this. An enemy unit inside the agro reach stops it too, seen or not, so a rock is never shot away with an enemy just behind it.</para>
    /// <para>What clips, measured on the model (heavy_tank_v2, 1 m = 8 px): the hull is 5.2 x 4.1 m, the turret ring lies 0.36 m behind its middle, the barrels' tips 4.13 m
    /// from the ring. A standing tank's turret steps 45 deg a second all round (the view's idle scan, as Zod's), so a barrel goes through every bearing in 8 s, at a moment
    /// the rules cannot know and never ask: the circle the tips sweep is the real reach, at any hull facing, and the hull lies inside that circle at every size. So one test:
    /// the rock's middle (its bottom tile, where it is drawn) within <see cref="ZWorld.HeavyRockReachPx"/> of the ring, the tips' reach at the drawn size + the
    /// rock's own <see cref="ZWorld.HeavyRockRadiusPx"/>. The ring is found from the last heading (<see cref="FaceX"/>); a tank that has not moved yet has the ring at its middle.
    /// The drawn size is <see cref="ZWorld.HeavyRockDrawnPct"/>, which the view keeps equal to OPTIONS VEHICLE SIZE.</para>
    /// <para>The shot is the game's own: reload, twin barrels, shell, event, sound, the rock's damage and break. Three things are this rule's: the first shell waits
    /// <see cref="ZWorld.HeavyRockTurnSeconds"/> so the drawn turret has turned; the aim is the rock's drawn foot, no scatter (the random numbers are still drawn), not the middle of
    /// its 1 x 3 sprite, 2 m to the north of it; and the blast hurts that rock alone (<see cref="ZMissile.onlyId"/>): a shell's 50 px splash would take the rocks next to it, and an
    /// empty vehicle or a hut, with no order given. A rock takes explosives only, and a heavy tank has them; a rock that cannot be hurt is passed over.</para>
    /// <para>Both teams and the computer alike: nothing here asks who drives. Deterministic: whole pixels, the sim clock, the object list's order (the nearest rock, the first
    /// of equals), no random draw of its own. Switch <see cref="ZWorld.HeavyRockDeviation"/>: View/ZGame.cs sets it at every level start, off in 'Zod exact'; static default off, so
    /// headless worlds, the parity's rows and every older counted check are as they were, tick for tick. Pure C# like the rest of the Sim folder.</para>
    /// </summary>
    public partial class ZObject
    {
        int hrRockId = -1, hrShellRock = -1; bool hrLooked; double hrCalmSince = -1, hrNextScan, hrGunWas = -1, hrGunAt = -1;

        /// <summary>This heavy tank is shooting t by this rule (view: the gun turns to the rock's foot).</summary>
        public bool HeavyRockOn(ZObject t) => t != null && hrRockId == t.Id;

        /// <summary>Standing idle with a crew, as the passive check calls it.</summary>
        bool HeavyRockIdle => Waypoints.Count == 0 && !IsMoving && !HoldPosition && !CrocWary && !Stunned && Drivers.Count > 0 && Owner != Team.Neutral && !World.GameOver && !World.CalmStart(Owner);

        /// <summary>ProcessServer, right after the passive check and before the shot, every tick.</summary>
        void HeavyRockStep(double time)
        {
            if (!ZWorld.HeavyRockDeviation || !HeavyRockType || Kind != ZKind.Vehicle) return;   // 1.5 r14_r16: the heavy tank, the Mammoth and the Bizon (Sim/ZObject.BigRock.cs)
            if (hrRockId >= 0) { HeavyRockKeep(time); return; }
            if (!HeavyRockIdle || AttackObject != null) { hrCalmSince = time; return; }
            if (hrCalmSince < 0) hrCalmSince = time;
            if (time < hrNextScan || time - hrCalmSince < ZWorld.HeavyRockIdleSeconds) return;
            hrNextScan = time + ZWorld.HeavyRockScanSeconds;
            if (HeavyRockEnemyNear()) return;
            var r = HeavyRockPick();
            if (r == null) return;
            hrRockId = r.Id; Engage(r);
            hrGunWas = nextDamageTime; hrGunAt = nextDamageTime = Math.Max(nextDamageTime, time + ZWorld.HeavyRockTurnSeconds);   // the gun turns first (given back if no shell left)
            World.HeavyRockTargets++;
        }

        /// <summary>On a rock: back on it after the passive check looked round, or the end of it.</summary>
        void HeavyRockKeep(double time)
        {
            var r = World.Get(hrRockId);
            if (r == null || r.Destroyed || r.KillAt >= 0) { HeavyRockGunBack(); hrRockId = -1; hrLooked = false; hrCalmSince = time; return; }   // gone: a second barrel already loaded still leaves, at the laid point
            if (hrLooked) { hrLooked = false; if (AttackObject == null && Waypoints.Count == 0) AttackObject = r; }
            bool end = !HeavyRockIdle || AttackObject != r;
            if (!end && (time >= hrNextScan || time >= nextDamageTime)) { hrNextScan = time + ZWorld.HeavyRockScanSeconds; end = HeavyRockEnemyNear(); }   // and right before every shell
            if (!end) return;
            if (AttackObject == r) Disengage();
            HeavyRockGunBack(); hpSecondAt = -1; hrRockId = -1; hrCalmSince = time;                    // whatever came wins at once: no second barrel at the rock either
        }

        /// <summary>The gun's turn wait goes back to what it was when no shell left for the rock: an order or an enemy that ends it fires as soon as it would have with no rock near.</summary>
        void HeavyRockGunBack() { if (nextDamageTime == hrGunAt) nextDamageTime = hrGunWas; }

        /// <summary>The passive check, on a tank with a target: true = the target is this rule's rock, put aside so the check looks round as for an idle tank. No lid, no random
        /// draw: a rock never opened the lid. <see cref="HeavyRockKeep"/> takes it back in this tick if the check started nothing.</summary>
        bool HeavyRockYields()
        {
            if (hrRockId < 0 || AttackObject == null || AttackObject.Id != hrRockId) return false;
            AttackObject = null; hrLooked = true;
            return true;
        }

        /// <summary>An enemy unit inside the agro reach, seen or not.</summary>
        bool HeavyRockEnemyNear()
        {
            foreach (var o in World.Objects)
                if (o.IsUnit && !o.Destroyed && o.KillAt < 0 && o.Owner != Team.Neutral && o.Owner != Owner && Within(CenterX, CenterY, o.CenterX, o.CenterY, AgroReachTo(o))) return true;
            return false;
        }

        /// <summary>The nearest rock this tank clips that it can fire at (the first in the object list of equals), or null.</summary>
        ZObject HeavyRockPick()
        {
            if (ZBigTanks.Is(Type)) return BigRockPick();                                          // 1.5 r14_r16: what a Mammoth / Bizon clips, from its bigger model (Sim/ZObject.BigRock.cs)
            int pct = ZWorld.HeavyRockDrawnPct, reach = ZWorld.HeavyRockReachPx(pct), back = ZWorld.HeavyRockRingBackPx(pct);
            int rx = CenterX, ry = CenterY;
            double fl = Math.Sqrt((double)FaceX * FaceX + (double)FaceY * FaceY);
            if (fl > 1e-6) { rx -= (int)Math.Round(FaceX / fl * back); ry -= (int)Math.Round(FaceY / fl * back); }
            long best = (long)reach * reach + 1; ZObject pick = null;
            foreach (var o in World.Objects)
            {
                if (o.Type != "rock" || o.Destroyed || o.KillAt >= 0 || o.MaxHealth <= 0 || !o.CanBeDestroyed) continue;
                long dx = o.CenterX - rx, dy = o.LocY + ZWorld.RockFootPx - ry, d2 = dx * dx + dy * dy;
                if (d2 >= best || !CanAttackObject(o) || !WithinAttackRadius(o)) continue;
                best = d2; pick = o;
            }
            return pick;
        }

        /// <summary>ProcessAttackDamage and the second barrel, after the aim's own draws: this rule's shell goes to the rock's drawn foot.</summary>
        void HeavyRockAim(ZObject t, ref int tx, ref int ty) { if (HeavyRockOn(t)) { tx = t.CenterX; ty = t.LocY + ZWorld.RockFootPx; } }

        /// <summary>ProcessAttackDamage: what the shell may hurt: the rock of this rule, or -1 = all it reaches (every other shell). Kept for the second barrel.</summary>
        int HeavyRockShell(ZObject t) => hrShellRock = HeavyRockOn(t) ? t.Id : -1;
    }

    public sealed partial class ZWorld
    {
        /// <summary>The rule's switch: see <see cref="ZObject"/>'s notes above (the game: on, off in 'Zod exact'; headless default off).</summary>
        public static bool HeavyRockDeviation = false;
        /// <summary>The drawn vehicle size, per cent. The view keeps it equal to OPTIONS VEHICLE SIZE; headless 90, that row's default.</summary>
        public static int HeavyRockDrawnPct = 90;
        /// <summary>Seconds the tank stands idle before it looks at a rock; between two looks (the passive check's own 0.5 s); the first shell's wait: 180 deg at the heavy's 150 deg/s, eased.</summary>
        public static double HeavyRockIdleSeconds = 2.0, HeavyRockScanSeconds = 0.5, HeavyRockTurnSeconds = 1.5;
        /// <summary>Measured, px at full size: ring to barrel tip (4.13 m), hull middle to ring (0.36 m). A rock's drawn radius, 1.13 m: the median edge at barrel height is 1.08 m on
        /// level 7, 1.36 m on levels 2 and 17, about 1.26 m on level 16. Rocks stand on whole tiles, so the distances come in steps: at the default size 8 to 10 px all shoot the same
        /// rocks on level 7, every rock the tips touch and a few the tips pass by 0.23 m or less; 7 px would leave about a fifth of the touched ones standing.</summary>
        public const int HeavyRockBarrelPx = 33, HeavyRockRingPx = 3, HeavyRockRadiusPx = 9;
        /// <summary>A rock's drawn foot below its top-left: the middle of its bottom tile (orock.cpp:675).</summary>
        public const int RockFootPx = 40;
        /// <summary>Rocks taken on by this rule in this world (checks).</summary>
        public int HeavyRockTargets;

        /// <summary>How far from the turret ring a rock's foot is clipped at a drawn size, whole pixels.</summary>
        public static int HeavyRockReachPx(int pct) => (HeavyRockBarrelPx * pct + 50) / 100 + HeavyRockRadiusPx;
        /// <summary>The ring behind the hull's middle at a drawn size, whole pixels.</summary>
        public static int HeavyRockRingBackPx(int pct) => (HeavyRockRingPx * pct + 50) / 100;

        /// <summary>The parity report: no rock cleared while this lives (a Play session leaves the switch on).</summary>
        public static IDisposable HeavyRockOff() => new HeavyRockHold();
        sealed class HeavyRockHold : IDisposable
        {
            readonly bool was = HeavyRockDeviation;
            public HeavyRockHold() { HeavyRockDeviation = false; }
            public void Dispose() { HeavyRockDeviation = was; }
        }
    }
}
