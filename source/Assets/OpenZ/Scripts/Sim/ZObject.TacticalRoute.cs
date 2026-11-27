using System;
using System.Collections.Generic;

namespace OpenZ.Sim
{
    public partial class ZObject
    {
        const int TacticalCell = 12, TacticalRadius = 9, TacticalWidth = 2 * TacticalRadius + 1;
        static readonly (int x, int y)[] TacticalSteps =
        {
            (1, 0), (0, 1), (-1, 0), (0, -1), (1, 1), (-1, 1), (-1, -1), (1, -1)
        };

        sealed class TacticalRoute
        {
            internal readonly List<(int x, int y)> Points = new List<(int x, int y)>();
            internal readonly short[] Parent = new short[TacticalWidth * TacticalWidth];
            internal readonly byte[] Depth = new byte[TacticalWidth * TacticalWidth];
            internal readonly byte[] FirstStep = new byte[TacticalWidth * TacticalWidth];
            internal readonly sbyte[] Passable = new sbyte[TacticalWidth * TacticalWidth];
            internal readonly int[] Queue = new int[TacticalWidth * TacticalWidth];
            internal int Enemy = -1, EnemyX, EnemyY, Index, Anchor, AimX, AimY, WorldVersion;
            internal double Expiry, Retry, NextAim, MovedAt, Pressure;
            internal int MovedX, MovedY;
            internal bool ResumeAfterDodge;
            internal readonly ZDuelState Feint = new ZDuelState { active = true };
        }

        TacticalRoute tactical;
        bool tacticalDriving;
        int tacticalGear;
        double tacticalPreviousDodge = double.NaN, tacticalPreferredDodge = double.NaN;
        double tacticalFollowupWeight;
        double tacticalPreviousDodgeAt, tacticalTravelAt, tacticalTravelX, tacticalTravelY;
        public int TacticalRoutePlans { get; private set; }
        public int TacticalRoutePointsReached { get; private set; }
        public int TacticalRouteDodgeResumes { get; private set; }
        public double TacticalPivotSeconds { get; private set; }
        public bool HasTacticalRoute => UsesDodgeRadar && Behavior.On(ZBehaviorKey.ExperimentalTacticalRoute) &&
            tactical != null && tactical.Index < tactical.Points.Count && World.Time < tactical.Expiry &&
            Waypoints.Count > 0 && (Waypoints[0].mode == WpMode.Dodge || Waypoints[0].refId == tactical.Enemy &&
                (Waypoints[0].mode == WpMode.Attack || Waypoints[0].mode == WpMode.Agro));

        public (int x, int y) TacticalDestination => HasTacticalRoute ? tactical.Points[tactical.Points.Count - 1] : (CenterX, CenterY);
        bool TacticalFeintInProgress => HasTacticalRoute && tactical.Feint.feintCounted && tactical.Feint.feintEnd > World.Time;

        internal static double TacticalFollowupAngle(double sample, double room)
        {
            double shallow = .94 - .40 * room;
            double degrees = sample < shallow ? 7 + 5 * room + (11 + 3 * room) * sample / shallow :
                25 + 15 * room + (12 + 12 * room) * (sample - shallow) / (1 - shallow);
            return degrees * Math.PI / 180;
        }

        internal static double TacticalPressure(int health, int opposingHealth, int id, int opponentId)
        {
            double advantage = (health - (double)opposingHealth) / Math.Max(1, Math.Max(health, opposingHealth));
            return Math.Abs(advantage) >= .08 ? Math.Clamp(advantage * 2.4, -1, 1) : id < opponentId ? .22 : -.22;
        }

        bool TacticalRouteStep(ZObject enemy, double dt, bool agro)
        {
            if (!DodgeRadar || !Behavior.On(ZBehaviorKey.ExperimentalTacticalRoute) || !BehaviorCombatFlow ||
                !BehaviorSteersHull || !ZWorld.DuelDeviation || !DuelCapable ||
                enemy.Kind != ZKind.Vehicle && enemy.Kind != ZKind.Cannon) return false;
            if (Dist(CenterX, CenterY, enemy.CenterX, enemy.CenterY) < ExperimentalDuelGap(enemy))
            {
                tactical?.Points.Clear();
                return false; // Point-blank separation takes priority over the planned firing position.
            }

            var route = tactical ??= new TacticalRoute();
            if (route.Enemy != enemy.Id || !TacticalFeintInProgress && (route.WorldVersion != World.Path.Version ||
                Dist(route.EnemyX, route.EnemyY, enemy.CenterX, enemy.CenterY) > 56))
            {
                route.Points.Clear();
                if (route.Enemy != enemy.Id) tacticalPreviousDodge = double.NaN;
                route.Enemy = enemy.Id;
                route.ResumeAfterDodge = false;
            }
            if (!HasTacticalRoute)
            {
                if (World.Time < route.Retry || Dist(CenterX, CenterY, enemy.CenterX, enemy.CenterY) > RangeTo(enemy) + 32)
                    return false;
                route.Retry = World.Time + .65;
                if (!PlanTacticalRoute(route, enemy, agro)) return false;
            }

            if (ExperimentalDodging) ObserveExperimentalMotion(enemy);
            if (WithinAttackRadius(enemy)) Engage(enemy);
            else Disengage();
            ScanDodgeRadar();
            if (route.ResumeAfterDodge)
            {
                route.ResumeAfterDodge = false;
                route.NextAim = World.Time;
                route.Anchor = -1;
                route.MovedAt = World.Time;
                TacticalRouteDodgeResumes++;
            }
            if (!TacticalFeintInProgress && route.Anchor >= route.Index &&
                Dist(CenterX, CenterY, route.Points[route.Anchor].x, route.Points[route.Anchor].y) <= 9)
            {
                TacticalRoutePointsReached += route.Anchor + 1 - route.Index;
                route.Index = route.Anchor + 1;
            }
            while (!TacticalFeintInProgress && route.Index < route.Points.Count &&
                Dist(CenterX, CenterY, route.Points[route.Index].x, route.Points[route.Index].y) <= 9)
            {
                route.Index++;
                TacticalRoutePointsReached++;
            }
            if (!HasTacticalRoute) return false;
            if (!TacticalFeintInProgress && (route.Anchor < route.Index || World.Time >= route.NextAim))
            {
                if (!AimTacticalRoute(route, enemy))
                    return AbandonTacticalRoute(route);
            }
            if (Dist(CenterX, CenterY, route.MovedX, route.MovedY) >= 5)
            {
                route.MovedAt = World.Time;
                route.MovedX = CenterX;
                route.MovedY = CenterY;
            }
            if (!TacticalFeintInProgress && World.Time - route.MovedAt > .75) return AbandonTacticalRoute(route);
            tacticalDriving = true;
            try
            {
                int gear = tacticalGear;
                bool feint = (TacticalFeintInProgress || route.Expiry - World.Time > 1 &&
                    Dist(CenterX, CenterY, enemy.CenterX, enemy.CenterY) > ExperimentalDuelGap(enemy) + 12) &&
                    TryExperimentalFeint(route.Feint, enemy, agro);
                if (feint) tacticalGear = route.Feint.experimentalGear;
                else { SetTarget(route.AimX, route.AimY); SetVelocity(); }
                double heading = BehaviorHeading;
                bool moved = ProcessMove(dt, true, out _, out _);
                tacticalGear = gear;
                if (BehaviorStepDx * BehaviorStepDx + BehaviorStepDy * BehaviorStepDy > 16)
                {
                    tacticalTravelAt = World.Time;
                    tacticalTravelX = BehaviorStepDx;
                    tacticalTravelY = BehaviorStepDy;
                }
                if (Math.Abs(WrapRadians(BehaviorHeading - heading)) > dt * .35 &&
                    BehaviorStepDx * BehaviorStepDx + BehaviorStepDy * BehaviorStepDy < 16) TacticalPivotSeconds += dt;
                if (!moved) return AbandonTacticalRoute(route);
            }
            finally { tacticalDriving = false; }
            return true;
        }

        bool AbandonTacticalRoute(TacticalRoute route)
        {
            route.Points.Clear();
            route.Retry = World.Time + .2;
            StopMove();
            ClearCur();
            return true;
        }

        bool PlanTacticalRoute(TacticalRoute route, ZObject enemy, bool agro)
        {
            route.Points.Clear();
            ScanDodgeRadar();
            Array.Fill(route.Parent, (short)-1);
            Array.Clear(route.Passable, 0, route.Passable.Length);
            int originX = CenterX, originY = CenterY, range = RangeTo(enemy);
            double gap = ExperimentalDuelGap(enemy), startDistance = Dist(originX, originY, enemy.CenterX, enemy.CenterY);
            route.Pressure = TacticalPressure(Health, enemy.Health, Id, enemy.Id);
            double preferredRange = Math.Max(gap + 12, range * (.81 - route.Pressure * .13));
            double floor = Math.Min(startDistance, gap);
            int start = TacticalRadius * TacticalWidth + TacticalRadius;
            int head = 0, tail = 1, best = -1;
            double bestScore = double.NegativeInfinity;
            route.Queue[0] = start;
            route.Parent[start] = (short)start;
            route.Depth[start] = 0;
            Radial(enemy, out double rx, out double ry);
            int side = duel?.sign ?? 0;
            if (side == 0) side = Id % 2 == 0 ? 1 : -1;
            while (head < tail)
            {
                int cell = route.Queue[head++], col = cell % TacticalWidth, row = cell / TacticalWidth;
                int x = originX + (col - TacticalRadius) * TacticalCell, y = originY + (row - TacticalRadius) * TacticalCell;
                double distance = Dist(x, y, enemy.CenterX, enemy.CenterY);
                if (route.Depth[cell] >= 3 && distance >= gap && distance <= range - 4 &&
                    !World.EngageBarrierBetween(x, y, enemy.CenterX, enemy.CenterY) &&
                    (!LosActive || World.Sight.ClearFrom(x, y, 0, enemy)))
                {
                    int open = 0;
                    foreach (var step in TacticalSteps)
                        if (TacticalPointFree(x + step.x * 14, y + step.y * 14) &&
                            TacticalPointFree(x + step.x * 28, y + step.y * 28)) open++;
                    double lateral = ((y - originY) * rx - (x - originX) * ry) * side;
                    double score = 8 * open + .13 * lateral - .5 * Math.Abs(distance - preferredRange) - 3 * route.Depth[cell];
                    if (BehaviorHeadingSet)
                    {
                        var initial = TacticalSteps[route.FirstStep[cell]];
                        double bearing = Math.Atan2(initial.y, initial.x);
                        double facing = Math.Cos(BehaviorHeading - bearing);
                        score += 12 * Math.Abs(facing) + 6 * facing * behaviorTravelSign;
                        if (World.Time < radarBlockedUntil)
                            score -= 26 * Math.Max(0, Math.Cos(bearing - radarBlockedBearing));
                    }
                    if (score > bestScore) { bestScore = score; best = cell; }
                }
                for (byte step = 0; step < TacticalSteps.Length; step++)
                {
                    var (dx, dy) = TacticalSteps[step];
                    int nx = col + dx, ny = row + dy;
                    if (nx < 0 || ny < 0 || nx >= TacticalWidth || ny >= TacticalWidth) continue;
                    int next = ny * TacticalWidth + nx;
                    if (route.Parent[next] >= 0 || route.Passable[next] < 0) continue;
                    int px = x + dx * TacticalCell, py = y + dy * TacticalCell;
                    if (route.Passable[next] == 0)
                    {
                        bool valid = Dist(px, py, enemy.CenterX, enemy.CenterY) + 1 >= floor &&
                            (!agro || Dist(px, py, cur.agroX, cur.agroY) <= World.S.agro_distance + ZDuel.AgroLeash) &&
                            TacticalPointFree(px, py);
                        route.Passable[next] = (sbyte)(valid ? 1 : -1);
                        if (!valid) continue;
                    }
                    if (!TacticalSegment(x, y, px, py)) continue;
                    route.Parent[next] = (short)cell;
                    route.Depth[next] = (byte)(route.Depth[cell] + 1);
                    route.FirstStep[next] = cell == start ? step : route.FirstStep[cell];
                    route.Queue[tail++] = next;
                }
            }
            if (best < 0) return false;
            for (int cell = best; cell != start; cell = route.Parent[cell])
                route.Points.Add((originX + (cell % TacticalWidth - TacticalRadius) * TacticalCell,
                    originY + (cell / TacticalWidth - TacticalRadius) * TacticalCell));
            route.Points.Reverse();
            route.EnemyX = enemy.CenterX; route.EnemyY = enemy.CenterY;
            route.WorldVersion = World.Path.Version;
            route.Index = 0; route.Anchor = -1; route.NextAim = World.Time;
            route.Expiry = World.Time + 8; route.MovedAt = World.Time;
            route.MovedX = originX; route.MovedY = originY;
            if (duel != null && duel.active) EndDuel(duel, DuelEnd.Other);
            route.Feint.sign = side;
            route.Feint.feintEnd = 0;
            route.Feint.nextFeint = World.Time + .65;
            ClearCur();
            TacticalRoutePlans++;
            return true;
        }

        bool TacticalPointFree(int x, int y)
        {
            if (!DuelBoxFree(x - (WidthPix >> 1), y - (HeightPix >> 1))) return false;
            foreach (var other in radarContacts)
            {
                if (RadarDistance(x, y, other.X, other.Y) + .25 <
                    Math.Min(other.Spacing, RadarDistance(CenterX, CenterY, other.X, other.Y))) return false;
            }
            return true;
        }

        bool TacticalSegment(int x, int y, int ex, int ey)
        {
            int count = Math.Max(1, (int)Math.Ceiling(Dist(x, y, ex, ey) / 4));
            for (int i = 1; i <= count; i++)
                if (!TacticalPointFree(x + (ex - x) * i / count, y + (ey - y) * i / count)) return false;
            return true;
        }

        bool AimTacticalRoute(TacticalRoute route, ZObject enemy)
        {
            int anchor = route.Index;
            for (int i = route.Index; i < route.Points.Count && i < route.Index + 4; i++)
            {
                var point = route.Points[i];
                if (Dist(CenterX, CenterY, point.x, point.y) > 46 ||
                    !TacticalSegment(CenterX, CenterY, point.x, point.y)) break;
                anchor = i;
            }
            var goal = route.Points[anchor];
            if (!TacticalSegment(CenterX, CenterY, goal.x, goal.y)) return false;
            if (route.Anchor != anchor)
            {
                double bearing = Math.Atan2(goal.y - CenterY, goal.x - CenterX);
                double facing = BehaviorHeadingSet ? Math.Cos(BehaviorHeading - bearing) : 1;
                if (tacticalGear == 0 || facing * tacticalGear < -.19)
                    tacticalGear = facing < 0 ? -1 : 1;
                route.Anchor = anchor;
            }
            double dx = goal.x - CenterX, dy = goal.y - CenterY, length = Dist(CenterX, CenterY, goal.x, goal.y);
            double weave = Math.Sin(World.Time * 4 + Id) * Math.Min(7, length * .16) *
                Behavior[ZBehaviorKey.ExperimentalCurveVariance] / 100;
            int x = goal.x - (int)Math.Round(dy * weave / Math.Max(1, length));
            int y = goal.y + (int)Math.Round(dx * weave / Math.Max(1, length));
            bool safe = TacticalSegment(CenterX, CenterY, x, y) && RadarApproach(x, y, enemy) < .65;
            route.AimX = safe ? x : goal.x; route.AimY = safe ? y : goal.y;
            route.NextAim = World.Time + .19;
            return true;
        }

        double TacticalDodgeProgress(double x, double y)
        {
            if (!HasTacticalRoute) return 0;
            var next = tactical.Points[tactical.Index];
            return .16 * Math.Clamp((Dist(CenterX, CenterY, next.x, next.y) - RadarDistance(x, y, next.x, next.y)) / 30, -1, 1);
        }

        double TacticalApproachPenalty => HasTacticalRoute ? .65 - tactical.Pressure * .22 : .62;
    }
}
