using System;
using System.Collections.Generic;

namespace OpenZ.Sim
{
    public partial class ZObject
    {
        const int RadarLook = 88;
        const double AllyBuffer = 14;
        readonly struct RadarContact
        {
            internal readonly ZObject Unit;
            internal readonly double X, Y, Vx, Vy, Spacing;
            internal RadarContact(ZObject unit, double spacing)
            {
                Unit = unit; X = unit.CenterX; Y = unit.CenterY; Spacing = spacing;
                Vx = unit.BehaviorStepDx; Vy = unit.BehaviorStepDy;
            }
        }

        List<RadarContact> radarContacts;
        long radarTick = -1;
        double radarBlockedUntil, radarBlockedBearing;
        int radarReverseStops, radarReverseStopX, radarReverseStopY;
        double radarReverseBlockedBearing;
        bool DodgeRadar => ExperimentalDodging && Behavior.On(ZBehaviorKey.ExperimentalDodgeRadar);
        bool RadarMoving => DodgeRadar && (BehaviorDodging || tacticalDriving || DuelKind != ZDuelKind.None);
        internal bool UsesDodgeRadar => DodgeRadar;
        public int ExperimentalRadarStops { get; private set; }

        static double RadarDistance(double x, double y, double ox, double oy) =>
            Math.Sqrt((x - ox) * (x - ox) + (y - oy) * (y - oy));

        void ScanDodgeRadar()
        {
            if (radarTick == World.TickIndex) return;
            radarTick = World.TickIndex;
            radarContacts ??= new List<RadarContact>();
            radarContacts.Clear();
            // A robot eligible for run-over contributes neither a collision penalty nor a route bonus.
            foreach (var other in World.NearUnits(CenterX, CenterY, 2 * RadarLook, World.Objects))
            {
                if (other == this || !other.IsUnit || other.Destroyed || other.KillAt >= 0 || other.ShuttleAirborne ||
                    Math.Abs(other.CenterX - CenterX) > 2 * RadarLook || Math.Abs(other.CenterY - CenterY) > 2 * RadarLook ||
                    ZWorld.CanRunOver(this, other)) continue;
                double buffer = other.Owner == Owner || World.AreAllied(Owner, other.Owner) ? AllyBuffer : 3;
                radarContacts.Add(new RadarContact(other, ZPathing.MinSpacing(this, other) + buffer +
                    (other.Kind == ZKind.Cannon ? 6 : 0)));
            }
        }

        bool RadarMissesTerrain(int gear)
        {
            if (gear < 0 && RadarReverseLimitReached) return false;
            // A single deterministic reading persists across candidate headings and quick re-aims.
            uint glance = unchecked((uint)(World.TickIndex / 75 * 2654435761L + Id * 97L));
            double sample = DodgeVariation(1, glance) - .5;
            return sample < Behavior[ZBehaviorKey.ExperimentalRadarError] * (gear < 0 ? 3 : 1) / 100;
        }

        static readonly double[] EscapeAngles = { 75, -75, 100, -100, 135, -135, 180 };

        double RadarApproach(double x, double y, ZObject enemy)
        {
            if (enemy == null || !enemy.IsUnit || enemy.Destroyed) return 0;
            double move = RadarDistance(CenterX, CenterY, x, y);
            if (move < 1) return 0;
            double start = Dist(CenterX, CenterY, enemy.CenterX, enemy.CenterY);
            double end = RadarDistance(x, y, enemy.CenterX, enemy.CenterY);
            return Math.Clamp((start - end) / move, 0, 1);
        }

        bool RadarReverseLimitReached => radarReverseStops >= 2 &&
            Dist(CenterX, CenterY, radarReverseStopX, radarReverseStopY) < 64;

        bool RadarRepeatsBlockedReverse(double x, double y)
        {
            if (!RadarReverseLimitReached) return false;
            double dx = x - CenterX, dy = y - CenterY;
            return dx * dx + dy * dy >= 16 &&
                Math.Abs(WrapRadians(Math.Atan2(dy, dx) - radarReverseBlockedBearing)) < Math.PI / 6;
        }

        void NoteRadarStop(double vx, double vy, bool terrain)
        {
            ExperimentalRadarStops++;
            radarBlockedBearing = Math.Atan2(vy, vx);
            radarBlockedUntil = World.Time + .8;
            if (!terrain || behaviorTravelSign >= 0) return;
            if (radarReverseStops == 0 || Dist(CenterX, CenterY, radarReverseStopX, radarReverseStopY) >= 64)
            {
                radarReverseStops = 0;
                radarReverseStopX = CenterX;
                radarReverseStopY = CenterY;
            }
            radarReverseStops = Math.Min(2, radarReverseStops + 1);
            radarReverseBlockedBearing = radarBlockedBearing;
        }

        DuelEnd RadarDuelAim(ZDuelState d, ZObject enemy, bool agro, double hx, double hy, int look, bool separating)
        {
            double bearing = Math.Atan2(hy, hx), best = double.NegativeInfinity;
            int bestX = 0, bestY = 0;
            // Try a fighting route first. When boxed in, choose an exit even if it interrupts the duel.
            for (int pass = 0; pass < 2 && double.IsNegativeInfinity(best); pass++)
            {
                for (int i = 0; i < RotTry.Length + (pass == 0 ? 0 : EscapeAngles.Length); i++)
                {
                    double offset = i < RotTry.Length ? RotTry[i] : EscapeAngles[i - RotTry.Length];
                    double angle = bearing + d.sign * offset * Math.PI / 180;
                    double ux = Math.Cos(angle), uy = Math.Sin(angle);
                    int step = pass == 0 ? Math.Min(look, ZDuel.LookPx) : ZDuel.ShortLookPx;
                    int x = (int)Math.Round(CenterX + ux * step), y = (int)Math.Round(CenterY + uy * step);
                    if (pass == 0 && !DuelHeadingOk(enemy, ux, uy, step, agro, out x, out y)) continue;
                    if (pass == 1 && agro && Dist(x, y, cur.agroX, cur.agroY) >
                        Math.Max(World.S.agro_distance + ZDuel.AgroLeash, Dist(CenterX, CenterY, cur.agroX, cur.agroY))) continue;
                    double approach = RadarApproach(x, y, enemy);
                    if (pass == 0 && approach > (d.kind == ZDuelKind.Close ? .85 : .55)) continue;
                    if (pass == 1 && Dist(x, y, enemy.CenterX, enemy.CenterY) <
                        Math.Min(Dist(CenterX, CenterY, enemy.CenterX, enemy.CenterY), ExperimentalDuelGap(enemy))) continue;
                    double runway = RadarRunway(ux, uy, RadarLook, 0);
                    if (runway < (pass == 0 ? step : 4)) continue;
                    double blocked = World.Time < radarBlockedUntil ?
                        Math.Max(0, Math.Cos(angle - radarBlockedBearing)) * 38 : 0;
                    double score = Math.Min(runway, 64) - Math.Abs(offset) * .27 - approach * 38 - blocked;
                    if (score <= best) continue;
                    best = score; bestX = x; bestY = y;
                }
            }
            if (double.IsNegativeInfinity(best)) return DuelEnd.Blocked;
            if (separating && !d.separating) ExperimentalSeparations++;
            d.separating = separating;
            SetTarget(bestX, bestY);
            SetVelocity();
            d.nextAim = World.Time + .16;
            return DuelEnd.Expired;
        }

        bool RadarTerrainClear(int x, int y)
        {
            if (!Behavior.On(ZBehaviorKey.ExperimentalLongBoostWeave)) return DuelBoxFree(x, y);
            // Leave room for the visual hull corners when the tank turns on the tile grid.
            const int cornerMargin = 4;
            return DuelBoxFree(x, y, cornerMargin) ||
                (!DuelBoxFree(LocX, LocY, cornerMargin) && DuelBoxFree(x, y));
        }

        bool RadarStep(double x, double y, double time, double distance, bool missesTerrain, ref double room)
        {
            if (RadarRepeatsBlockedReverse(x, y)) return false;
            if (!RadarTerrainClear((int)Math.Floor(x) - (WidthPix >> 1), (int)Math.Floor(y) - (HeightPix >> 1)) &&
                (!missesTerrain || distance <= 6 || x < WidthPix / 2 || y < HeightPix / 2 ||
                    x >= World.Map.WidthPix - WidthPix / 2 || y >= World.Map.HeightPix - HeightPix / 2)) return false;
            if (!tacticalDriving && World.Pathing?.Enabled == true && World.Pathing.DuelLaneAt((int)x, (int)y)) return false;
            foreach (var contact in radarContacts)
            {
                double lead = Math.Min(time, .55);
                double ox = contact.X + contact.Vx * lead, oy = contact.Y + contact.Vy * lead;
                double before = RadarDistance(CenterX, CenterY, ox, oy), after = RadarDistance(x, y, ox, oy);
                // Already inside the buffer: permit a route that opens the gap.
                if (after + .25 < Math.Min(before, contact.Spacing)) return false;
                room = Math.Min(room, Math.Max(0, after - contact.Spacing));
            }
            return true;
        }

        bool RadarTerrainStep(double x, double y)
        {
            double dx = x - PosX, dy = y - PosY;
            int probes = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(dx * dx + dy * dy) / 4));
            for (int i = 1; i <= probes; i++)
            {
                int px = (int)Math.Floor(PosX + dx * i / probes), py = (int)Math.Floor(PosY + dy * i / probes);
                if (World.Path.WithinImpassable(px + 1, py + 1, WidthPix - 2, HeightPix - 2, out _, out _, false) ||
                    Behavior.On(ZBehaviorKey.ExperimentalLongBoostWeave) && !RadarTerrainClear(px, py)) return false;
            }
            return true;
        }

        bool RadarActualStep(double x, double y)
        {
            ScanDodgeRadar();
            foreach (var contact in radarContacts)
            {
                var other = contact.Unit;
                double spacing = contact.Spacing - 2;
                double before = RadarDistance(PosX + (WidthPix >> 1), PosY + (HeightPix >> 1), other.CenterX, other.CenterY);
                double after = RadarDistance(x + (WidthPix >> 1), y + (HeightPix >> 1), other.CenterX, other.CenterY);
                if (after + 1e-6 < Math.Min(before, spacing)) return false;
            }
            return true;
        }

        double RadarRunway(double ux, double uy, int look, int gear)
        {
            ScanDodgeRadar();
            double bearing = Math.Atan2(uy, ux);
            double heading = BehaviorHeadingSet ? BehaviorHeading : bearing;
            var drive = new DodgeDrive(heading, behaviorTurnSpeed, BehaviorActualSpeed,
                behaviorReverseUntil - World.Time, behaviorTravelSign);
            int preferred = !Behavior.On(ZBehaviorKey.Reverse) ? 1 :
                tacticalDriving ? TacticalTravelGear(heading, bearing, behaviorTravelSign) :
                gear != 0 ? gear : NearestHullReverse(heading, bearing) ? -1 : 1;
            bool blind = RadarMissesTerrain(preferred);
            double x = PosX + (WidthPix >> 1), y = PosY + (HeightPix >> 1), travel = 0, room = 24;
            double speed = Math.Max(1, RealMoveSpeed * Behavior[ZBehaviorKey.CombatFlowSpeed]);
            const double dt = .04;
            for (int i = 0; i < 35 && travel < look; i++)
            {
                StepRangeAwareDodge(Behavior, ref drive, bearing, speed, look - travel, dt, 1, 1, CripplingAcceleration,
                    out double vx, out double vy, smoothing: Behavior[ZBehaviorKey.ExperimentalSteeringTime], gear: gear,
                    rollingTurn: tacticalDriving);
                double step = Math.Sqrt(vx * vx + vy * vy) * dt;
                int probes = Math.Max(1, (int)Math.Ceiling(step / 4));
                for (int n = 1; n <= probes; n++)
                {
                    double f = n / (double)probes;
                    if (!RadarStep(x + vx * dt * f, y + vy * dt * f, (i + f) * dt, travel + step * f, blind, ref room))
                        return travel + step * (n - 1) / probes;
                }
                x += vx * dt; y += vy * dt; travel += step;
            }
            return travel;
        }
    }
}
