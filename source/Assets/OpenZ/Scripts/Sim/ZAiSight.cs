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

        internal ZAiSight(ZWorld world, Team team)
        {
            this.world = world;
            this.team = team;
            mapHasRadar = ZBattleSight.MapHasRadar(world.Map);
        }

        internal void Observe()
        {
            var rule = world.Map.skirmish || world.HasDuelRules ? world.DuelRadarLoss : world.ModernBotSoloRadar;
            bool radarOnline = false;
            if (mapHasRadar && (world.ModernBotFog || rule != RadarLossRule.Off))
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

            if (!world.ModernBotFog) return;
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
            (!world.ModernBotFog || Open(explored, x, y) && (!mobile || !limitedSight || Open(current, x, y)));

        OzFowMask NewMask() => new OzFowMask(world.Map.WidthPix, world.Map.HeightPix);
        static bool Open(OzFowMask mask, int x, int y) => mask == null || mask.TargetAtPx(x, y) >= OzFowMask.Revealed;
    }
}
