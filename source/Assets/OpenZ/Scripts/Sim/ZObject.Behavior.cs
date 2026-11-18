using System;

namespace OpenZ.Sim
{
    public enum ZThreatKind { Shell, Rocket, Grenade }
    public partial class ZObject
    {
        public ZBehaviorValues Behavior => World?.BehaviorFor(this);
        public bool BehaviorDodging => Waypoints.Count > 0 && Waypoints[0].mode == WpMode.Dodge;
        public string BehaviorDodgeReason = "No threat";
        public double BehaviorDodgeDistance => behaviorDodgeTravel * ZConst.PX_TO_M;
        public double BehaviorBoostDistance => behaviorBoostTravel * ZConst.PX_TO_M;
        public int BehaviorAlerts, BehaviorAttempts, BehaviorRefusals, BehaviorReplans;
        public double BehaviorHeading, BehaviorActualSpeed;
        public bool BehaviorHeadingSet;
        public float BehaviorStepDx, BehaviorStepDy;
        public string BehaviorMotionPhase = "Idle";
        public int BehaviorThreatX, BehaviorThreatY;
        public double BehaviorThreatUntil;
        internal bool BehaviorRocketWeapon => Type == "tough" || Type == "medium" || Type == "missile_launcher" || Type == "missile_cannon";
        double behaviorTurnSpeed, behaviorReverseUntil, behaviorDodgeStart = -100, behaviorBlastAt,
            behaviorLastDodge = -100, behaviorFireUntil, behaviorRegroupUntil;
        double behaviorDodgeTravel, behaviorBoostTravel, behaviorBoostStart;
        double dodgeSpeedFactor = 1, dodgeAccelerationFactor = 1, dodgeBoostFactor = 1, dodgeDistanceFactor = 1, dodgePivotFactor = 1;
        public bool BehaviorSteersHull => Behavior != null && (Behavior.On(ZBehaviorKey.Physical) || BehaviorDodging && Behavior.On(ZBehaviorKey.DodgePhysical));
        uint dodgeBoostRoll;

        double DodgeVariation(double spread, uint salt)
        {
            unchecked
            {
                uint sample = (uint)Id * 0x9e3779b9u + (uint)BehaviorAttempts * 0x85ebca6bu + salt;
                sample = (sample ^ (sample >> 16)) * 0x7feb352du;
                sample = (sample ^ (sample >> 15)) * 0x846ca68bu;
                return 1 + ((sample ^ (sample >> 16)) / (double)uint.MaxValue - .5) * spread;
            }
        }
        bool behaviorBoostStarted, behaviorBoostSettled;
        bool behaviorReversing, behaviorWasDodging, behaviorPending;
        int behaviorTravelSign = 1;
        double behaviorReactAt, behaviorPendingBlast, behaviorAttackReturnAt;
        int behaviorPendingX, behaviorPendingY, behaviorOriginX, behaviorOriginY, behaviorShots;
        ZThreatKind behaviorPendingKind;
        int behaviorPendingDamage, behaviorPendingAttacker;
        bool behaviorPendingNearby;
        ZWaypoint behaviorPendingOrder;
        int behaviorPendingOrderCount;
        public bool BehaviorRetreating
        {
            get
            {
                if (AttackObject == null || Waypoints.Count == 0) return false;
                var w = Waypoints[0];
                if (w.mode == WpMode.Dodge && Waypoints.Count > 1) w = Waypoints[1];
                if (w.mode != WpMode.Move && w.mode != WpMode.ForceMove) return false;
                return (double)(w.x - CenterX) * (AttackObject.CenterX - CenterX) + (double)(w.y - CenterY) * (AttackObject.CenterY - CenterY) < 0;
            }
        }
        internal void BehaviorChanged()
        {
            behaviorPending = false;
            // Changing a profile never gives back endurance or discards the underlying order.
            if (Behavior == null || !Behavior.On(ZBehaviorKey.Enabled))
            {
                if (BehaviorDodging && behaviorWasDodging) KillWP();
                behaviorWasDodging = false;
            }
            // Live tuning must not snap an established physical heading to a new target.
            if (Behavior == null || !Behavior.On(ZBehaviorKey.Physical))
            { BehaviorHeadingSet = false; BehaviorActualSpeed = 0; behaviorTurnSpeed = 0; }
        }
        bool BehaviorRefuse(string reason)
        { BehaviorDodgeReason = reason; BehaviorRefusals++; return false; }

        bool BehaviorDodge(int tx, int ty, double time, ZThreatKind kind, int damage, int attacker, bool nearby, bool resumed = false)
        {
            var p = Behavior; if (p == null) return false;
            if (!resumed) BehaviorAlerts++;
            BehaviorThreatX = tx; BehaviorThreatY = ty; BehaviorThreatUntil = World.Time + time;
            if (!p.On(ZBehaviorKey.Enabled)) return BehaviorRefuse("Dodging disabled");
            if (Destroyed || Stunned || Owner == Team.Neutral || MoveSpeed <= 0 || RealMoveSpeed <= 0 || IsEvilZod || IsFortDefender)
                return BehaviorRefuse("Unit cannot move");
            if (time <= 0) return BehaviorRefuse("Impact already due");
            if (fortRush || FortRunOn || UnmannedHeld) return BehaviorRefuse("Protected transition");
            var threatKey = kind == ZThreatKind.Grenade ? ZBehaviorKey.ThreatGrenade : kind == ZThreatKind.Rocket ? ZBehaviorKey.ThreatRocket : ZBehaviorKey.ThreatShell;
            if (!p.On(threatKey) || (nearby && !p.On(ZBehaviorKey.NearbyThreats))) return BehaviorRefuse("Threat filter");
            if (damage > 0 && damage < p[ZBehaviorKey.MinDamage]) return BehaviorRefuse("Damage below threshold");
            var from = World.Get(attacker);
            if (p.On(ZBehaviorKey.VisibleOnly) && from != null && !CanSee(from)) return BehaviorRefuse("Attacker not visible");
            if (HoldPosition && !p.On(ZBehaviorKey.Hold)) return BehaviorRefuse("Holding position");
            bool retreat = BehaviorRetreating;
            if (p.On(ZBehaviorKey.StandFirm) && IsRobot && (Type == "tough" || Type == "sniper" || Type == "electrobot"))
                return BehaviorRefuse("Stand-and-fire restriction");
            if (!CanOverwriteWP() && !(Waypoints.Count > 0 && Waypoints[0].mode == WpMode.ForceMove && p.On(ZBehaviorKey.ForceMove)))
                return BehaviorRefuse("Protected order stage");
            if (!BehaviorDodging)
            {
                if (retreat)
                { if (!p.On(ZBehaviorKey.Retreat)) return BehaviorRefuse("Retreat dodge disabled"); }
                else
                {
                    if (AttackObject != null && !p.On(ZBehaviorKey.Engaged)) return BehaviorRefuse("Engaged dodge disabled");
                    if (Waypoints.Count == 0 && !p.On(ZBehaviorKey.Idle)) return BehaviorRefuse("Idle dodge disabled");
                    if (Waypoints.Count > 0)
                    {
                        var w = Waypoints[0];
                        var k = w.mode == WpMode.Attack || w.mode == WpMode.Agro ? ZBehaviorKey.AttackOrder :
                            w.mode == WpMode.ForceMove ? ZBehaviorKey.ForceMove :
                            w.mode == WpMode.Move ? ZBehaviorKey.Move : ZBehaviorKey.Approach;
                        if (!p.On(k)) return BehaviorRefuse("Order forbids dodge: " + w.mode);
                    }
                }
                double last = behaviorLastDodge;
                if (p.On(ZBehaviorKey.SquadCooldown))
                {
                    var lead = Leader ?? this;
                    last = Math.Max(last, lead.behaviorLastDodge);
                    foreach (var m in lead.Minions) last = Math.Max(last, m.behaviorLastDodge);
                }
                if (World.Time - last < p[ZBehaviorKey.Cooldown]) return BehaviorRefuse("Dodge cooldown");
                if (AttackObject != null && behaviorLastDodge > -99 && behaviorShots < p[ZBehaviorKey.ShootFirst]) return BehaviorRefuse("Must shoot first");
                if (!resumed && p[ZBehaviorKey.Chance] < 100 && World.Rng.Next(10000) >= p[ZBehaviorKey.Chance] * 100) return BehaviorRefuse("Reaction chance");
            }
            else if (!p.On(ZBehaviorKey.Replan) || World.Time - behaviorDodgeStart < p[ZBehaviorKey.Commit])
                return BehaviorRefuse("Dodge direction committed");
            double delay = resumed ? 0 : p[ZBehaviorKey.Reaction];
            if (!resumed && p[ZBehaviorKey.ReactionSpread] > 0) delay += World.Rng.Next(10000) / 10000.0 * p[ZBehaviorKey.ReactionSpread];
            if (delay >= time) return BehaviorRefuse("Reaction too late");
            if (delay > 0)
            {
                if (!behaviorPending) behaviorReactAt = World.Time + delay;
                behaviorPending = true; behaviorPendingX = tx; behaviorPendingY = ty; behaviorPendingBlast = World.Time + time;
                behaviorPendingKind = kind; behaviorPendingDamage = damage; behaviorPendingAttacker = attacker; behaviorPendingNearby = nearby;
                behaviorPendingOrderCount = Waypoints.Count; behaviorPendingOrder = Waypoints.Count > 0 ? Waypoints[0] : ZWaypoint.Cleared;
                BehaviorDodgeReason = "Reaction pending"; return true;
            }
            return BehaviorStartDodge(tx, ty, time);
        }

        bool BehaviorStartDodge(int tx, int ty, double time)
        {
            var p = Behavior;
            if (p == null || !p.On(ZBehaviorKey.Enabled) || Stunned || Destroyed || Owner == Team.Neutral || time <= 0) return BehaviorRefuse("Reaction cancelled");
            BehaviorAttempts++;
            bool replan = BehaviorDodging;
            double distanceFactor = replan ? dodgeDistanceFactor : DodgeVariation(p[ZBehaviorKey.DistanceVariance], 1);
            double speedFactor = replan ? dodgeSpeedFactor : p[ZBehaviorKey.DodgeSpeed] * DodgeVariation(p[ZBehaviorKey.SpeedVariance], 2);
            double acceleration = replan ? dodgeAccelerationFactor : DodgeVariation(p[ZBehaviorKey.AccelerationVariance], 3);
            double pivot = replan ? dodgePivotFactor : DodgeVariation(p[ZBehaviorKey.PivotVariance], 5);
            double boostSpeed = replan ? 1 + (p[ZBehaviorKey.BoostSpeed] - 1) * dodgeBoostFactor : p[ZBehaviorKey.BoostSpeed];
            double speed = p.ClassicReference ? RealMoveSpeed : BehaviorBaseMoveSpeed;
            speed *= speedFactor;
            if (p.On(ZBehaviorKey.Boost) && (!p.On(ZBehaviorKey.Endurance) || Stamina >= time)) speed *= boostSpeed;
            double dist = Math.Min(BehaviorMapSpan, time * speed);
            int spread = Math.Max(1, (int)(dist * .5));
            double length = ((int)dist + World.Rng.Next(spread)) * p[ZBehaviorKey.Distance] * distanceFactor;
            length = Math.Max(p[ZBehaviorKey.MinDistance] / ZConst.PX_TO_M, length);
            if (p[ZBehaviorKey.MaxDistance] > 0) length = Math.Min(length, p[ZBehaviorKey.MaxDistance] / ZConst.PX_TO_M);
            length = Math.Min(BehaviorMapSpan, length);
            int k = World.Rng.Next(1000); if (k == 0) k = 1000;
            double random = 2 * 3.14159 * (1000.0 / k);
            bool useHeading = BehaviorHeadingSet && (!p.On(ZBehaviorKey.DodgePhysical) || replan || p.On(ZBehaviorKey.Physical));
            double heading = useHeading ? BehaviorHeading : p.On(ZBehaviorKey.TurnFirst) || p.On(ZBehaviorKey.DodgePhysical) ?
                (FaceX != 0 || FaceY != 0 ? Math.Atan2(FaceY, FaceX) : -Math.PI / 2) : (Dx != 0 || Dy != 0 ? Math.Atan2(Dy, Dx) : FaceX != 0 || FaceY != 0 ? Math.Atan2(FaceY, FaceX) : -Math.PI / 2);
            int bestX = CenterX, bestY = CenterY; double best = double.NegativeInfinity;
            int strategy = (int)p[ZBehaviorKey.Direction];
            int direction = strategy == 3 ? BehaviorWeightedDirection(p, World.Rng.Next(1000000) / 1000000.0) : -1;
            if (strategy == 3 && direction < 0) return BehaviorRefuse("No enabled direction weight");
            int samples = strategy == 0 || strategy == 3 ? 1 : strategy == 1 ? 9 : 24;
            for (int i = 0; i < samples; i++)
            {
                double angle = strategy == 3 ? heading + direction * Math.PI / 8 : strategy == 0 ? random : strategy == 1 ? Math.Atan2(CenterY - ty, CenterX - tx) + (i == 0 ? 0 : ((i + 1) / 2) * (i % 2 == 0 ? -1 : 1) * Math.PI / 8) : heading + i * Math.PI / 12;
                double off = WrapRadians(angle - heading), abs = Math.Abs(off);
                var allow = abs < Math.PI / 4 ? ZBehaviorKey.Forward : abs > 3 * Math.PI / 4 ? ZBehaviorKey.Backward : ZBehaviorKey.Sideways;
                if (strategy == 3 ? !BehaviorDirectionAllowed(p, direction) : !p.On(allow)) continue;
                int x = (int)Math.Max(WidthPix / 2, Math.Min(World.Map.WidthPix - WidthPix / 2, CenterX + Math.Cos(angle) * length));
                int y = (int)Math.Max(HeightPix / 2, Math.Min(World.Map.HeightPix - HeightPix / 2, CenterY + Math.Sin(angle) * length));
                int ox = BehaviorDodging ? behaviorOriginX : CenterX, oy = BehaviorDodging ? behaviorOriginY : CenterY;
                if (p[ZBehaviorKey.MaxDeviation] > 0 && Dist(x, y, ox, oy) * ZConst.PX_TO_M > p[ZBehaviorKey.MaxDeviation]) continue;
                if (Leader != null && p[ZBehaviorKey.Cohesion] > 0 && Dist(x, y, Leader.CenterX, Leader.CenterY) * ZConst.PX_TO_M > p[ZBehaviorKey.Cohesion]) continue;
                if (strategy != 0 && !BehaviorClearLine(x, y)) continue;
                double reachable = Math.Min(length, BehaviorBaseMoveSpeed * speedFactor * time * (p.On(ZBehaviorKey.Boost) ? boostSpeed : 1));
                if (p.On(ZBehaviorKey.Physical) || p.On(ZBehaviorKey.DodgePhysical))
                {
                    double turnAngle = p.On(ZBehaviorKey.Reverse) ? Math.Min(abs, Math.PI - abs) : abs;
                    double turn = p.On(ZBehaviorKey.Lateral) && !p.On(ZBehaviorKey.TurnFirst) && !p.On(ZBehaviorKey.DodgePhysical) || p[ZBehaviorKey.TurnRate] == 0 ? 0 :
                        turnAngle / (p[ZBehaviorKey.TurnRate] * pivot * Math.PI / 180);
                    double available = Math.Max(0, time - turn);
                    reachable = Math.Min(reachable, RealMoveSpeed * available);
                    if (p[ZBehaviorKey.Acceleration] > 0) reachable *= Math.Min(1, available / (p[ZBehaviorKey.Acceleration] * acceleration) * .5 + BehaviorActualSpeed / Math.Max(1, RealMoveSpeed));
                }
                double rx = CenterX + Math.Cos(angle) * reachable, ry = CenterY + Math.Sin(angle) * reachable;
                double score = Dist((int)rx, (int)ry, tx, ty);
                if (score <= best) continue;
                best = score; bestX = x; bestY = y;
            }
            if (double.IsNegativeInfinity(best)) return BehaviorRefuse("No permitted reachable direction");
            if (BehaviorDodging)
            {
                var w = Waypoints[0]; w.x = bestX; w.y = bestY; Waypoints[0] = w; BehaviorReplans++;
            }
            else
            {
                dodgeDistanceFactor = distanceFactor; dodgeSpeedFactor = speedFactor;
                dodgeAccelerationFactor = acceleration; dodgeBoostFactor = 1; dodgePivotFactor = pivot;
                if (p.On(ZBehaviorKey.DodgePhysical) && !p.On(ZBehaviorKey.Physical))
                {
                    BehaviorHeading = heading;
                    BehaviorHeadingSet = true;
                    behaviorTurnSpeed = 0;
                    BehaviorActualSpeed = Math.Sqrt(Dx * Dx + Dy * Dy);
                }
                behaviorOriginX = CenterX; behaviorOriginY = CenterY; behaviorDodgeStart = World.Time;
                behaviorDodgeTravel = behaviorBoostTravel = 0; behaviorBoostStarted = behaviorBoostSettled = false;
                // Replanning a volley must not keep postponing the interrupted attack forever.
                behaviorAttackReturnAt = World.Time + time + .1;
                behaviorLastDodge = World.Time; behaviorShots = 0;
                Waypoints.Insert(0, new ZWaypoint { mode = WpMode.Dodge, refId = -1, x = bestX, y = bestY });
            }
            behaviorBlastAt = World.Time + time; behaviorWasDodging = true;
            if (!p.On(ZBehaviorKey.KeepTarget)) Disengage();
            BehaviorDodgeReason = "Dodging"; return true;
        }

        bool BehaviorClearLine(int x, int y)
        {
            double d = Dist(CenterX, CenterY, x, y); int n = Math.Max(1, (int)(d / 4));
            for (int i = 1; i <= n; i++)
            {
                int nx = (int)(CenterX + (x - CenterX) * (double)i / n) - (WidthPix >> 1);
                int ny = (int)(CenterY + (y - CenterY) * (double)i / n) - (HeightPix >> 1);
                if (World.Path.WithinImpassable(nx + 1, ny + 1, WidthPix - 2, HeightPix - 2, out _, out _, IsRobot)) return false;
            }
            return true;
        }

        void BehaviorTick()
        {
            var p = Behavior; if (p == null) return;
            if (behaviorWasDodging && !BehaviorDodging)
            {
                behaviorWasDodging = false; behaviorFireUntil = World.Time + p[ZBehaviorKey.Settle];
                behaviorRegroupUntil = World.Time + p[ZBehaviorKey.RegroupDelay];
            }
            if (behaviorPending && World.Time >= behaviorReactAt)
            {
                behaviorPending = false;
                // Recheck the current order/state without adding another reaction delay.
                if (Waypoints.Count != behaviorPendingOrderCount || Waypoints.Count > 0 && !Waypoints[0].Equals(behaviorPendingOrder)) BehaviorRefuse("Order changed during reaction");
                else BehaviorDodge(behaviorPendingX, behaviorPendingY, behaviorPendingBlast - World.Time, behaviorPendingKind, behaviorPendingDamage, behaviorPendingAttacker, behaviorPendingNearby, true);
            }
            if (BehaviorDodging && BehaviorBoostExhausted && !behaviorBoostSettled)
            {
                behaviorBoostSettled = true; IsRunning = false;
                double previous = RealMoveSpeed;
                double tile = World.Map.TileWalkSpeed(CenterX, CenterY);
                if (World.Pathing != null && World.Pathing.Enabled) tile = World.Pathing.SafeTileSpeed(this, tile);
                RealMoveSpeed = MoveSpeed * tile * DamagedSpeed() * RunSpeed();
                if (strikePace < 1.0) RealMoveSpeed *= StrikePaceNow;
                if (previous > 0) { Dx = (float)(Dx / previous * RealMoveSpeed); Dy = (float)(Dy / previous * RealMoveSpeed); UpdatedVelocity = true; }
            }
            bool returnToAttack = p.On(ZBehaviorKey.ResumeAttack) && Waypoints.Count > 1 &&
                (Waypoints[1].mode == WpMode.Attack || Waypoints[1].mode == WpMode.Agro);
            if (BehaviorDodging && ((p[ZBehaviorKey.MaxDuration] > 0 && World.Time - behaviorDodgeStart >= p[ZBehaviorKey.MaxDuration]) ||
                (p[ZBehaviorKey.MaxDistance] > 0 && behaviorDodgeTravel * ZConst.PX_TO_M >= p[ZBehaviorKey.MaxDistance] - 1e-8) ||
                (p.On(ZBehaviorKey.EndAfterBlast) && World.Time > behaviorBlastAt + .1) ||
                (returnToAttack && World.Time > behaviorAttackReturnAt))) KillWP();
        }

        bool BehaviorHoldFire()
        {
            var p = Behavior; if (p == null) return false;
            if (World.Time < behaviorFireUntil) return true;
            return BehaviorDodging && (!p.On(ZBehaviorKey.FireDodge) || IsRunning && !p.On(ZBehaviorKey.FireBoost));
        }
        double BehaviorAccuracy => BehaviorDodging && Behavior != null ? Behavior[ZBehaviorKey.Accuracy] / 100 : 1;
        int BehaviorScatter(int offset) => BehaviorAccuracy == 1 ? offset : (int)Math.Round(offset / Math.Max(.05, Math.Sqrt(BehaviorAccuracy)));
        bool BehaviorReleaseStance => Behavior != null && (BehaviorDodging || BehaviorRetreating && Behavior.On(ZBehaviorKey.BreakForRetreat));
        bool BehaviorHoldRegroup => IsMinion && Behavior != null && World.Time < behaviorRegroupUntil && !PlayerOrderHeld;

        bool BehaviorStartRun()
        {
            var p = Behavior; if (p == null || !BehaviorDodging) return false;
            if (IsRunning || !p.On(ZBehaviorKey.Boost) || BehaviorBoostExhausted) return true;
            if (p.On(ZBehaviorKey.Endurance) && Stamina < p[ZBehaviorKey.MinStamina]) return true;
            if (ShowDamaged && !p.On(ZBehaviorKey.DamagedBoost)) return true;
            if (p[ZBehaviorKey.BoostChance] == 80 ? World.Rng.Next(5) != 0 : World.Rng.Next(10000) < p[ZBehaviorKey.BoostChance] * 100)
            {
                IsRunning = true;
                dodgeBoostFactor = DodgeVariation(p[ZBehaviorKey.BoostVariance], unchecked(0xa68e92b1u * ++dodgeBoostRoll));
                if (!behaviorBoostStarted) { behaviorBoostStarted = true; behaviorBoostStart = World.Time; }
            }
            return true;
        }
        bool BehaviorStamina(double dt)
        {
            var p = Behavior; if (p == null) return false;
            if (IsRunning)
            {
                if (!BehaviorDodging || p.On(ZBehaviorKey.Endurance)) Stamina = Math.Max(0, Stamina - dt * (BehaviorDodging ? p[ZBehaviorKey.Drain] : 1));
                if ((Stamina <= 0 && (!BehaviorDodging || p.On(ZBehaviorKey.Endurance))) ||
                    BehaviorDodging && BehaviorBoostExhausted) IsRunning = false;
            }
            else Stamina = Math.Min(MaxStamina, Stamina + dt * World.S.run_recharge_rate * p[ZBehaviorKey.Recharge]);
            return true;
        }
        double BehaviorRunSpeed()
        {
            var p = Behavior; if (p == null || !BehaviorDodging) return -1;
            return Math.Min(BehaviorBoostMultiplier(p) * dodgeSpeedFactor, BehaviorMapSpan / Math.Max(1, MoveSpeed) / ZConst.TICK);
        }
        double BehaviorBoostMultiplier(ZBehaviorValues p)
        {
            if (BehaviorBoostExhausted) return 1;
            double run = p.On(ZBehaviorKey.Boost) && (Leader != null ? Leader.IsRunning : IsRunning) && (!ShowDamaged || p.On(ZBehaviorKey.DamagedBoost)) ? 1 + (p[ZBehaviorKey.BoostSpeed] - 1) * dodgeBoostFactor : 1;
            return p.On(ZBehaviorKey.PremiumBoost) && PremiumDriven ? Math.Max(run, ZWorld.PremiumDodgeSpeedMul) : run;
        }
        bool BehaviorBoostExhausted => Behavior != null &&
            ((Behavior[ZBehaviorKey.BoostDuration] > 0 && behaviorBoostStarted && World.Time - behaviorBoostStart >= Behavior[ZBehaviorKey.BoostDuration]) ||
             (Behavior[ZBehaviorKey.BoostMaxDistance] > 0 && behaviorBoostTravel * ZConst.PX_TO_M >= Behavior[ZBehaviorKey.BoostMaxDistance] - 1e-8));
        public static bool BehaviorDirectionAllowed(ZBehaviorValues p, int i)
        {
            int offset = Math.Min(i, 16 - i);
            return p.On(offset < 2 ? ZBehaviorKey.Forward : offset > 6 ? ZBehaviorKey.Backward : ZBehaviorKey.Sideways);
        }
        static double BehaviorDirectionScale(ZBehaviorValues p)
        {
            double scale = 0;
            for (int i = 0; i < 16; i++) if (BehaviorDirectionAllowed(p, i)) scale = Math.Max(scale, p[(ZBehaviorKey)((int)ZBehaviorKey.DodgeWeight0 + i)]);
            return scale;
        }
        public static double BehaviorDirectionProbability(ZBehaviorValues p, int direction)
        {
            double scale = BehaviorDirectionScale(p), total = 0;
            if (scale <= 0 || !BehaviorDirectionAllowed(p, direction)) return 0;
            for (int i = 0; i < 16; i++) if (BehaviorDirectionAllowed(p, i)) total += p[(ZBehaviorKey)((int)ZBehaviorKey.DodgeWeight0 + i)] / scale;
            return p[(ZBehaviorKey)((int)ZBehaviorKey.DodgeWeight0 + direction)] / scale / total;
        }
        public static int BehaviorWeightedDirection(ZBehaviorValues p, double draw)
        {
            double scale = BehaviorDirectionScale(p), total = 0;
            if (scale <= 0) return -1;
            for (int i = 0; i < 16; i++) if (BehaviorDirectionAllowed(p, i)) total += p[(ZBehaviorKey)((int)ZBehaviorKey.DodgeWeight0 + i)] / scale;
            if (total <= 0) return -1;
            double target = Math.Max(0, Math.Min(.999999999, draw)) * total;
            for (int i = 0; i < 16; i++) if (BehaviorDirectionAllowed(p, i))
            { target -= p[(ZBehaviorKey)((int)ZBehaviorKey.DodgeWeight0 + i)] / scale; if (target < 0) return i; }
            return -1;
        }
        // Count accepted travel across turns and replans.
        void BehaviorRecordTravel(double distance)
        {
            var p = Behavior;
            if (!BehaviorDodging || p == null) return;
            behaviorDodgeTravel += distance;
            if (BehaviorBoostMultiplier(p) > 1)
            {
                if (!behaviorBoostStarted) { behaviorBoostStarted = true; behaviorBoostStart = World.Time; }
                behaviorBoostTravel += distance;
            }
        }
        void BehaviorLimitStep(double dt, ref double vx, ref double vy)
        {
            var p = Behavior;
            if (p == null || !BehaviorDodging || dt <= 0) return;
            double length = Math.Sqrt(vx * vx + vy * vy) * dt, allowed = length;
            if (p[ZBehaviorKey.MaxDistance] > 0) allowed = Math.Min(allowed, Math.Max(0, p[ZBehaviorKey.MaxDistance] / ZConst.PX_TO_M - behaviorDodgeTravel));
            if (p[ZBehaviorKey.MaxDuration] > 0) allowed = Math.Min(allowed, length * Math.Max(0, Math.Min(1, (behaviorDodgeStart + p[ZBehaviorKey.MaxDuration] - World.Time) / dt)));
            if (BehaviorBoostMultiplier(p) > 1)
            {
                double fraction = 1;
                if (p[ZBehaviorKey.BoostMaxDistance] > 0) fraction = Math.Min(fraction, Math.Max(0, (p[ZBehaviorKey.BoostMaxDistance] / ZConst.PX_TO_M - behaviorBoostTravel) / Math.Max(1e-12, length)));
                if (p[ZBehaviorKey.BoostDuration] > 0 && behaviorBoostStarted) fraction = Math.Min(fraction, Math.Max(0, (behaviorBoostStart + p[ZBehaviorKey.BoostDuration] - World.Time) / dt));
                allowed = Math.Min(allowed, length * fraction);
            }
            if (length > 0 && allowed < length) { vx *= allowed / length; vy *= allowed / length; }
        }
        double BehaviorMapSpan => World.Map.WidthPix + (double)World.Map.HeightPix;
        double BehaviorBaseMoveSpeed => MoveSpeed * World.Map.TileWalkSpeed(CenterX, CenterY) * DamagedSpeed();

        static double WrapRadians(double a) { a %= Math.PI * 2; if (a > Math.PI) a -= Math.PI * 2; if (a < -Math.PI) a += Math.PI * 2; return a; }
        static double Toward(double a, double b, double step) => a < b ? Math.Min(b, a + step) : Math.Max(b, a - step);
        void BehaviorVelocity(double dt, ref double vx, ref double vy)
        {
            var p = Behavior;
            if (dt <= 0) return;
            CripplingCommand(ref vx, ref vy);
            if (p == null || !BehaviorSteersHull) { CripplingFreeVelocity(dt, ref vx, ref vy); return; }
            double desired = Math.Sqrt(vx * vx + vy * vy), angle = Math.Atan2(cur.y - CenterY, cur.x - CenterX);
            if (!BehaviorHeadingSet)
            {
                BehaviorHeading = p.On(ZBehaviorKey.TurnFirst) ?
                    (FaceX != 0 || FaceY != 0 ? Math.Atan2(FaceY, FaceX) : -Math.PI / 2) : angle;
                BehaviorHeadingSet = true;
            }
            if (p.On(ZBehaviorKey.TurnFirst) || BehaviorDodging && p.On(ZBehaviorKey.DodgePhysical))
            { BehaviorTurnFirst(dt, desired, angle, ref vx, ref vy); return; }
            bool reverse = (!p.On(ZBehaviorKey.Lateral) || ZBehaviorSchema.IsTank(Type)) &&
                p.On(ZBehaviorKey.Reverse) && NearestHullReverse(BehaviorHeading, angle);
            if (reverse != behaviorReversing) { behaviorReversing = reverse; behaviorReverseUntil = World.Time + p[ZBehaviorKey.ReverseDelay]; }
            double aim = reverse ? angle + Math.PI : angle;
            double delta = WrapRadians(aim - BehaviorHeading);
            double rotation = BehaviorDodging ? dodgePivotFactor : 1;
            double maxRate = p[ZBehaviorKey.TurnRate] * rotation * Math.PI / 180;
            if (maxRate == 0) { BehaviorHeading = aim; behaviorTurnSpeed = 0; }
            else
            {
                double want = Math.Max(-maxRate, Math.Min(maxRate, delta / dt));
                behaviorTurnSpeed = p[ZBehaviorKey.TurnAcceleration] == 0 ? want : Toward(behaviorTurnSpeed, want, p[ZBehaviorKey.TurnAcceleration] * rotation * Math.PI / 180 * dt);
                double step = behaviorTurnSpeed * dt;
                BehaviorHeading += Math.Abs(step) > Math.Abs(delta) ? delta : step;
            }
            double error = Math.Abs(WrapRadians(aim - BehaviorHeading));
            double target = desired * (reverse ? p[ZBehaviorKey.ReverseSpeed] / 100 : 1);
            target *= 1 - Math.Min(1, error / (Math.PI / 2)) * (1 - p[ZBehaviorKey.CornerSpeed] / 100);
            if ((!p.On(ZBehaviorKey.TurnMoving) || p.On(ZBehaviorKey.Pivot) && error > Math.PI / 3) && error > Math.PI / 12) target = 0;
            if (World.Time < behaviorReverseUntil) target = 0;
            double remaining = Dist(CenterX, CenterY, cur.x, cur.y);
            if (p[ZBehaviorKey.Braking] > 0) target = Math.Min(target, Math.Sqrt(2 * desired / p[ZBehaviorKey.Braking] * remaining));
            double ramp = target < BehaviorActualSpeed ? p[ZBehaviorKey.Braking] : p[ZBehaviorKey.Acceleration];
            if (BehaviorDodging && target >= BehaviorActualSpeed) ramp *= dodgeAccelerationFactor;
            if (target >= BehaviorActualSpeed) ramp += CripplingAcceleration;
            if (CripplingHitActive || CripplingActive) BehaviorActualSpeed = Math.Min(BehaviorActualSpeed, desired);
            BehaviorActualSpeed = ramp <= 0 ? target : Toward(BehaviorActualSpeed, target, Math.Max(1, desired) / ramp * dt);
            double speed = Math.Min(BehaviorActualSpeed, remaining / dt);
            double travel = p.On(ZBehaviorKey.Lateral) ? angle : BehaviorHeading + (reverse ? Math.PI : 0);
            vx = Math.Cos(travel) * speed; vy = Math.Sin(travel) * speed;
        }
        void BehaviorTurnFirst(double dt, double desired, double angle, ref double vx, ref double vy)
        {
            var p = Behavior;
            bool reverse = p.On(ZBehaviorKey.Reverse) && NearestHullReverse(BehaviorHeading, angle);
            double aim = WrapRadians(angle + (reverse ? Math.PI : 0));
            int sign = reverse ? -1 : 1;
            if (reverse != behaviorReversing) { behaviorReversing = reverse; behaviorReverseUntil = World.Time + p[ZBehaviorKey.ReverseDelay]; }
            desired *= reverse ? p[ZBehaviorKey.ReverseSpeed] / 100 : 1;
            double tolerance = Math.Min(45, p[ZBehaviorKey.AlignmentTolerance]) * Math.PI / 180;
            double error = Math.Abs(WrapRadians(aim - BehaviorHeading));
            double pivotSpeed = desired * Math.Min(100, p[ZBehaviorKey.PivotSpeed]) / 100;
            bool turning = error > Math.Max(1e-6, tolerance);
            bool reversing = sign != behaviorTravelSign && BehaviorActualSpeed > 1e-6;
            double remaining = Dist(CenterX, CenterY, cur.x, cur.y);
            double target = reversing || World.Time < behaviorReverseUntil ? 0 : turning ? Math.Min(BehaviorActualSpeed, pivotSpeed) : desired;
            if (p[ZBehaviorKey.Braking] > 0)
                target = Math.Min(target, Math.Sqrt(2 * desired / p[ZBehaviorKey.Braking] * remaining));
            double ramp = target < BehaviorActualSpeed ? p[ZBehaviorKey.Braking] : p[ZBehaviorKey.Acceleration];
            if (BehaviorDodging && target >= BehaviorActualSpeed) ramp *= dodgeAccelerationFactor;
            if (target >= BehaviorActualSpeed) ramp += CripplingAcceleration;
            if (CripplingHitActive || CripplingActive) BehaviorActualSpeed = Math.Min(BehaviorActualSpeed, desired);
            BehaviorActualSpeed = ramp <= 0 ? target : Toward(BehaviorActualSpeed, target, Math.Max(1, desired) / ramp * dt);
            bool braking = (turning && BehaviorActualSpeed > pivotSpeed + 1e-6) || (reversing && BehaviorActualSpeed > 1e-6);
            if (!braking) behaviorTravelSign = sign;
            BehaviorMotionPhase = braking ? "Braking" : turning ? "Pivoting" : BehaviorActualSpeed < desired - 1e-6 ? "Accelerating" : "Driving";
            if (braking) behaviorTurnSpeed = 0;
            else
            {
                double delta = WrapRadians(aim - BehaviorHeading);
                double rotation = BehaviorDodging ? dodgePivotFactor : 1;
                double cap = p[ZBehaviorKey.TurnRate] * rotation * Math.PI / 180;
                if (cap == 0) { BehaviorHeading = aim; behaviorTurnSpeed = 0; }
                else
                {
                    double want = Math.Max(-cap, Math.Min(cap, delta / dt));
                    behaviorTurnSpeed = p[ZBehaviorKey.TurnAcceleration] == 0 ? want :
                        Toward(behaviorTurnSpeed, want, p[ZBehaviorKey.TurnAcceleration] * rotation * Math.PI / 180 * dt);
                    behaviorTurnSpeed = Math.Max(-cap, Math.Min(cap, behaviorTurnSpeed));
                    double step = behaviorTurnSpeed * dt;
                    // Never carry angular momentum past alignment or turn away after a replan.
                    if (step * delta <= 0) step = 0;
                    if (Math.Abs(step) >= Math.Abs(delta)) { step = delta; behaviorTurnSpeed = 0; }
                    BehaviorHeading = WrapRadians(BehaviorHeading + step);
                }
            }
            double speed = Math.Min(BehaviorActualSpeed, remaining / dt);
            vx = Math.Cos(BehaviorHeading) * speed * behaviorTravelSign; vy = Math.Sin(BehaviorHeading) * speed * behaviorTravelSign;
        }
        bool BehaviorReached(out bool reached)
        {
            reached = false;
            if (!BehaviorSteersHull) return false;
            reached = Dist(CenterX, CenterY, cur.x, cur.y) <= 1.5;
            return true;
        }
    }

    public sealed partial class ZWorld
    {
        public void AlertBehaviorThreat(ZObject target, ZMissile missile, ZThreatKind kind)
        {
            if (target != null) target.DodgeMissile(missile.x, missile.y, missile.explodeTime - Time, kind, missile.damage, missile.attackerId);
            foreach (var o in Objects)
            {
                if (o == target || o.Destroyed || o.Owner == missile.team || o.Owner == Team.Neutral) continue;
                var p = o.Behavior;
                if (p == null || !p.On(ZBehaviorKey.NearbyThreats)) continue;
                double dx = o.CenterX - missile.x, dy = o.CenterY - missile.y;
                if (dx * dx + dy * dy <= (double)missile.radius * missile.radius)
                    o.DodgeMissile(missile.x, missile.y, missile.explodeTime - Time, kind, missile.damage, missile.attackerId, true);
            }
        }
    }
}
