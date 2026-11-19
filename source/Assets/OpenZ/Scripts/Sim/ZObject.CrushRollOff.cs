using System;

namespace OpenZ.Sim
{
    /// <summary>
    /// OpenZ 1.5 deviation DV187 "the Bizon rolls off the far side" (kit bizon_crush_climb, on DV183 the Bizon's crush, Sim/ZObject.BizonCrush.cs).
    /// drive over the crushed target ended with the Bizon's centre on the target's spot: its rear was still on the wreck, so the view could never
    /// tip it down and roll it off on the far side.
    /// <para>With DV187 on, the drive's end is Radius(target) + <see cref="CrushRollOffPx"/> px BEYOND the target's centre along the crush (the
    /// line the Bizon already drives on): its middle passes the wreck's far edge and its front comes down on the far side; the time limit of that
    /// longer drive is CrushMaxTicks + <see cref="CrushRollOffTicks"/>. Everything else is DV183's: the contact, the hold, the kill tick (the
    /// front two thirds across, CrushKillGap), the 3 s fallback, the boost, the end when blocked by another unit's room or held (never driven
    /// through anything), the order after it. Whole numbers only (deterministic).</para>
    /// <para>
    /// The drive goes on until the Bizon's REAR is off the wreck: the clear point = the target's drawn far extent + the Bizon's drawn rear
    /// reach + <see cref="CrushClearPx"/> beyond the target's centre along the crush (RamDrawnExtent / RamDrawnBox, whole px); the target is
    /// destroyed <see cref="CrushBlastTicks"/> after the Bizon's centre passes it (never earlier under its front: it never needs to be slid out
    /// from under the tracks), or <see cref="CrushBlastTicks"/> after a drive that ended early. The drive's end is <see cref="CrushBrakePx"/>
    /// beyond the clear point, and over its last part the Bizon brakes at <see cref="CrushBrakeAccelPx"/> px/s2 (its speed capped by
    /// sqrt(2 a d), d = whole px left, never below its plain speed): the crush runs at the boost's momentum, then it stops.</para>
    /// <para>Switch <see cref="ZWorld.CrushRollOffDeviation"/>: View/ZGame.cs on at every start (off in 'Zod exact'); headless default off; the
    /// parity report's Zod rows and the counted checks hold it off; it acts only with DV183 on. Off: not one field changes.</para>
    /// </summary>
    public partial class ZObject
    {
        /// <summary>How far past the target's centre the drive ends: the target's radius + this (1 m): the Bizon's middle beyond the wreck's far
        /// edge, its front down on the ground on the far side.</summary>
        public const int CrushRollOffPx = 8;
        /// <summary>The longer drive's extra time over CrushMaxTicks (3.5 s: the Bizon crawls at about 9 px/s with the boost).</summary>
        public const int CrushRollOffTicks = 350;

        bool crushRolled;
        /// <summary>1.5 rework 14i: the Bizon's rear past the wreck's far edge by this much (px), the brake run after the clear point (px; the
        /// boost's 56 px/s needs 19.6 px at 80 px/s2), the braking (px/s2), the time from the clear point to the target's death (0.3 s: the blast
        /// right behind the Bizon).</summary>
        public const int CrushClearPx = 2, CrushBrakePx = 20, CrushBrakeAccelPx = 80, CrushBlastTicks = 30;
        int crushTx, crushTy, crushDirX, crushDirY, crushClear; long crushBest, crushBestTick;
        /// <summary>1.5 rework 14i: the target's centre to the clear point along the crush (px; 0 = no rolled drive).</summary>
        public int CrushClearPlan => crushRolled ? crushClear : 0;

        /// <summary>CrushDrive's limit: DV183's CrushMaxTicks, + CrushRollOffTicks for a drive DV187 lengthened.</summary>
        int CrushLimitTicks => CrushMaxTicks + (crushRolled ? CrushRollOffTicks : 0);

        /// <summary>CrushImpact, after DV183 set the drive's end on the target's spot: with DV187 it goes on past it (the far side).</summary>
        void CrushRollOffEnd(ZObject t)
        {
            crushRolled = false;
            if (!ZWorld.CrushRollOffDeviation || t == null || World == null) return;
            crushRolled = true;
            crushTx = t.CenterX; crushTy = t.CenterY; crushDirX = t.RamDirX; crushDirY = t.RamDirY; crushBest = long.MinValue; crushBestTick = World.TickIndex;
            RamDrawnBox(Type, out _, out int rear, out _);
            if (rear < 0) rear = ZPathing.Radius(this);
            crushClear = RamDrawnExtent(t, t.RamDirX, t.RamDirY, true) + rear + CrushClearPx;   // 1.5 rework 14i: the Bizon's rear off the wreck
            int over = crushClear + CrushBrakePx;                                        // 1.5 rework 14i: was ZPathing.Radius(t) + CrushRollOffPx
            crushEndX = t.CenterX + (int)((long)t.RamDirX * over / 1024);
            crushEndY = t.CenterY + (int)((long)t.RamDirY * over / 1024);
            World.CrushRollOffs++;
        }

        /// <summary>1.5 rework 14i, CrushDrive: the Bizon's centre has passed the clear point (its rear off the wreck): the target is destroyed
        /// CrushBlastTicks from now (whole px along the crush).</summary>
        void CrushClearKill(ZObject t, long now)
        {
            long past = ((long)(CenterX - crushTx) * crushDirX + (long)(CenterY - crushTy) * crushDirY) / 1024;
            if (past < crushClear || t.RamDoomTick <= now + CrushBlastTicks) return;
            t.RamDoomTick = now + CrushBlastTicks; World.BizonCrushOnTime++; World.CrushRearClears++;
        }

        /// <summary>1.5 rework 14i, CrushDrive: no whole px of progress along the crush for RamHeldTicks (held by another unit's room, sliding
        /// along it): the drive is over ("held"), never a 10 s push against it.</summary>
        bool CrushStalled(long now)
        {
            if (!crushRolled) return false;
            long adv = ((long)(CenterX - crushTx) * crushDirX + (long)(CenterY - crushTy) * crushDirY) / 1024;
            if (adv > crushBest) { crushBest = adv; crushBestTick = now; }
            return now - crushBestTick > RamHeldTicks;
        }

        /// <summary>1.5 rework 14i, CrushFinish: the drive ended (over, blocked, held, time): a crushed target still alive dies CrushBlastTicks
        /// from now (never left standing until the fallback).</summary>
        void CrushRollOffFinish()
        {
            if (!crushRolled || ram == null || World == null) return;
            var t = World.Get(ram.targetId); long now = World.TickIndex;
            if (t != null && t.RamDoomBy == Id && !t.Destroyed && t.KillAt < 0 && t.RamDoomTick > now + CrushBlastTicks) t.RamDoomTick = now + CrushBlastTicks;
        }

        /// <summary>1.5 rework 14i, RamBrakeCap: the drive over the crushed target brakes toward its end (px/s; negative = none).</summary>
        double CrushBrakeCap()
        {
            if (!crushOn || !crushRolled) return -1.0;
            long left = ((long)(crushEndX - CenterX) * crushDirX + (long)(crushEndY - CenterY) * crushDirY) / 1024;
            if (left < 0) left = 0;
            return Math.Sqrt(2.0 * CrushBrakeAccelPx * left);
        }
    }

    public sealed partial class ZWorld
    {
        /// <summary>DV187 (kit bizon_crush_climb). ZGame turns it on at every start (off in 'Zod exact'); headless default off.</summary>
        public static bool CrushRollOffDeviation = false;

        /// <summary>The parity report and the older counted checks: off while this lives.</summary>
        public static IDisposable CrushRollOffOff() => new CrushRollOffHold();
        sealed class CrushRollOffHold : IDisposable
        {
            readonly bool was = CrushRollOffDeviation;
            public CrushRollOffHold() { CrushRollOffDeviation = false; }
            public void Dispose() { CrushRollOffDeviation = was; }
        }

        /// <summary>Statistics (the counted check): crush drives whose end was put on the far side; 1.5 rework 14i: kills set as the rear cleared.</summary>
        public int CrushRollOffs, CrushRearClears;
    }
}
