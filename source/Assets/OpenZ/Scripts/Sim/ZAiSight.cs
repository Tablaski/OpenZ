using System;
using System.Collections.Generic;
using OpenZ.Multiplayer;

namespace OpenZ.Sim
{
    /// <summary>Shared sight geometry for radar loss and deterministic bot observations.</summary>
    internal static class ZBattleSight
    {
        public const int UnitSightPadPx = 24, UnitSightMinPx = 184, BuildingSightPadPx = 128, FlagSightPx = 128;
        public const int RadarSightPx = 384;

        internal static int Radius(ZObject o, int agro)
        {
            if (o.IsUnit) return Math.Max(UnitSightMinPx, (o.U?.attack_radius ?? 0) + agro + UnitSightPadPx);
            if (o.Kind == ZKind.Building) return Math.Max(o.WidthPix, o.HeightPix) / 2 + BuildingSightPadPx;
            return o.Kind == ZKind.MapItem && o.Type == "flag" ? FlagSightPx : 0;
        }

        internal static bool MapHasRadar(ZMapData map)
        {
            if (map?.objects == null) return false;
            foreach (var o in map.objects)
                if (o.kind == "building:radar") return true;
            return false;
        }

        internal static bool RadarFog(RadarLossRule rule) => rule == RadarLossRule.Fog || rule == RadarLossRule.FogAndZoom;
        internal static bool RadarZoom(RadarLossRule rule) => rule == RadarLossRule.Zoom || rule == RadarLossRule.FogAndZoom;

        internal static void StampRadarLoss(ZWorld world, Team team, OzFowMask sight)
        {
            sight.Clear();
            foreach (var zone in world.Zones)
            {
                if (!world.AreAllied(zone.Owner, team)) continue;
                int left = sight.CellX(zone.X), right = sight.CellX(zone.X + zone.W - 1);
                int top = sight.CellY(zone.Y), bottom = sight.CellY(zone.Y + zone.H - 1);
                for (int y = top; y <= bottom; y++)
                    for (int x = left; x <= right; x++) sight.Target[y * sight.W + x] = 255;
            }
            foreach (var unit in world.Objects)
                if (world.AreAllied(unit.Owner, team) && unit.IsUnit && !unit.Destroyed)
                    sight.Stamp(unit.CenterX, unit.CenterY, Radius(unit, world.S.agro_distance));
        }
    }

    public sealed partial class ZWorld
    {
        internal bool ModernBotFog, ModernBotLineOfSight;
        internal RadarLossRule ModernBotSoloRadar;
        internal Team CampaignFogTeam;
        readonly Dictionary<Team, OrderSight> orderSight = new Dictionary<Team, OrderSight>();
        bool? fogMapHasRadar;
        double nextOrderSight;

        sealed class OrderSight
        {
            internal readonly ZAiSight Sight;
            internal bool RadarOnline;
            internal readonly Dictionary<int, (int X, int Y)> LastSeen = new();
            internal readonly HashSet<int> ConfirmedLost = new();
            internal readonly HashSet<(int X, int Y)> HiddenRockTiles = new();
            internal readonly Dictionary<int, (int X, int Y)> KnownRocks = new();
            internal OrderSight(ZWorld world, Team team) { Sight = new ZAiSight(world, team, true); }
        }

        void ObservePlayerOrders()
        {
            var rule = Map.skirmish || HasDuelRules ? DuelRadarLoss : ModernBotSoloRadar;
            bool radarFog = ZBattleSight.RadarFog(rule) &&
                (fogMapHasRadar ??= ZBattleSight.MapHasRadar(Map));
            if (!radarFog && CampaignFogTeam == Team.Neutral) return;
            if (orderSight.Count == 0)
            {
                foreach (Team team in Enum.GetValues(typeof(Team)))
                {
                    if (!IsHumanTeam(team) || (!radarFog && team != CampaignFogTeam)) continue;
                    if (!Objects.Exists(o => o.Owner == team)) continue;
                    orderSight.Add(team, new OrderSight(this, team));
                }
            }
            bool refreshSight = Time >= nextOrderSight;
            if (refreshSight) nextOrderSight = Time + .05;
            foreach (var pair in orderSight)
            {
                var state = pair.Value;
                bool radarOnline = false;
                foreach (var obj in Objects)
                    if (obj.ProvidesRadarTo(pair.Key)) { radarOnline = true; break; }
                if (refreshSight || state.RadarOnline != radarOnline)
                {
                    state.RadarOnline = radarOnline;
                    state.Sight.Observe(radarOnline);
                }
                state.HiddenRockTiles.Clear();
                foreach (var target in Objects)
                {
                    if (!target.IsUnit && target.Kind != ZKind.Building && !target.IsDestroyableImpass) continue;
                    bool visible = AreAllied(pair.Key, target.Owner) ||
                        state.Sight.CanSee(target.CenterX, target.CenterY);
                    if (visible)
                    {
                        if (target.Destroyed || target.KillAt >= 0)
                        {
                            if (state.LastSeen.Remove(target.Id)) state.ConfirmedLost.Add(target.Id);
                        }
                        else
                        {
                            state.ConfirmedLost.Remove(target.Id);
                            state.LastSeen[target.Id] = (target.CenterX, target.CenterY);
                        }
                    }
                    if (!target.IsDestroyableImpass || target.Type != "rock") continue;
                    if (visible && (target.Destroyed || target.KillAt >= 0))
                        state.KnownRocks.Remove(target.Id);
                    else if (!target.Destroyed && target.KillAt < 0 && (visible || TickIndex == 1))
                        state.KnownRocks[target.Id] = (target.LocX >> 4, (target.LocY >> 4) + 2);
                }
                foreach (var rock in state.KnownRocks.Values)
                {
                    if (!state.Sight.CanSee(rock.X * 16 + 8, rock.Y * 16 + 8) &&
                        !Path.HasDestroyableBarrier(rock.X, rock.Y))
                        state.HiddenRockTiles.Add(rock);
                }
            }
        }

        internal bool HiddenFromOrders(Team team, ZObject obj) =>
            obj != null && orderSight.TryGetValue(team, out var state) &&
            !AreAllied(team, obj.Owner) && !state.Sight.CanSee(obj.CenterX, obj.CenterY);

        internal bool TryFogAttack(Team team, ZWaypoint wp, ZObject target, out (int X, int Y) point)
        {
            point = (wp.x, wp.y);
            if (!wp.playerGiven || wp.mode != WpMode.Attack || !orderSight.TryGetValue(team, out var state)) return false;
            if (target != null && !HiddenFromOrders(team, target)) return false;
            if (state.ConfirmedLost.Contains(wp.refId)) return false;
            if (state.LastSeen.TryGetValue(wp.refId, out var remembered)) point = remembered;
            return !state.Sight.CanSee(point.X, point.Y);
        }

        internal bool PlayerCanSeeOrderPoint(Team team, int x, int y) =>
            !orderSight.TryGetValue(team, out var state) || state.Sight.CanSee(x, y);

        internal HashSet<(int X, int Y)> FogRockTiles(Team team) =>
            orderSight.TryGetValue(team, out var state) && state.HiddenRockTiles.Count != 0 ? state.HiddenRockTiles : null;
    }

    internal sealed class ZAiSight
    {
        readonly ZWorld world;
        readonly Team team;
        readonly bool mapHasRadar;
        readonly Dictionary<int, (int X, int Y, int Radius)> exploredStamps =
            new Dictionary<int, (int X, int Y, int Radius)>();
        OzFowMask explored, current, radarLoss;
        bool limitedSight, jammedFog;
        internal bool ZoomLimited { get; private set; }
        internal bool HasLimitedInformation => jammedFog || world.ModernBotFog;

        readonly bool playerOrders;
        bool CampaignFog => playerOrders ? world.CampaignFogTeam == team : world.ModernBotFog;

        internal ZAiSight(ZWorld world, Team team, bool playerOrders = false)
        {
            this.world = world;
            this.team = team;
            this.playerOrders = playerOrders;
            mapHasRadar = ZBattleSight.MapHasRadar(world.Map);
        }

        internal void Observe(bool? knownRadarOnline = null)
        {
            var rule = world.Map.skirmish || world.HasDuelRules ? world.DuelRadarLoss : world.ModernBotSoloRadar;
            bool radarOnline = knownRadarOnline ?? false;
            if (!knownRadarOnline.HasValue && mapHasRadar && (CampaignFog || rule != RadarLossRule.Off))
                foreach (var o in world.Objects)
                    if (o.ProvidesRadarTo(team)) { radarOnline = true; break; }
            bool jammed = mapHasRadar && !radarOnline;
            ZoomLimited = jammed && ZBattleSight.RadarZoom(rule);

            jammedFog = jammed && ZBattleSight.RadarFog(rule);
            if (jammedFog)
            {
                radarLoss ??= NewMask();
                ZBattleSight.StampRadarLoss(world, team, radarLoss);
            }

            if (!CampaignFog || playerOrders && jammedFog) return;
            explored ??= NewMask();
            limitedSight = world.ModernBotLineOfSight && !radarOnline;
            if (limitedSight)
            {
                current ??= NewMask();
                current.Clear();
            }
            foreach (var o in world.Objects)
            {
                if (!world.AreAllied(team, o.Owner) || o.Owner == Team.Neutral || !ZAi2026.Alive(o)) continue;
                int radius = ZBattleSight.Radius(o, world.S.agro_distance);
                if (o.ProvidesRadarTo(team)) radius = Math.Max(radius, ZBattleSight.RadarSightPx);
                if (radius == 0) continue;
                var stamp = (explored.CellX(o.CenterX), explored.CellY(o.CenterY), radius);
                if (!exploredStamps.TryGetValue(o.Id, out var previous) || previous != stamp)
                {
                    explored.Stamp(o.CenterX, o.CenterY, radius);
                    exploredStamps[o.Id] = stamp;
                }
                if (limitedSight) current.Stamp(o.CenterX, o.CenterY, radius);
            }
        }

        internal bool CanSee(int x, int y, bool mobile = true) =>
            (!jammedFog || Open(radarLoss, x, y)) &&
            (!CampaignFog || playerOrders && jammedFog || Open(explored, x, y) && (!mobile || !limitedSight || Open(current, x, y)));

        OzFowMask NewMask() => new OzFowMask(world.Map.WidthPix, world.Map.HeightPix);
        static bool Open(OzFowMask mask, int x, int y) => mask == null || mask.TargetAtPx(x, y) >= OzFowMask.Revealed;
    }
}
