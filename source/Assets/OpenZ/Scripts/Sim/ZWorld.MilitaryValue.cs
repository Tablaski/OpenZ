using System;

namespace OpenZ.Sim
{
    public sealed partial class ZWorld
    {
        /// <summary>Normal production seconds of surviving offensive mobile units; a robot squad is counted once.</summary>
        public double MilitaryValue(Team team)
        {
            if (team != Team.Red && team != Team.Blue) return 0;
            double value = 0;
            foreach (var unit in Objects)
                if (unit.Owner == team && unit.IsMobile && !unit.IsMinion && !unit.IsFortDefender && unit.Type != "crane" && unit.CanAttack())
                    value += Math.Max(0, S.Unit(unit.Type)?.build_time ?? 0);
            return value;
        }
    }

    public static class ZForceComparison
    {
        public static double Share(double own, double other) => own + other > 0 ? own / (own + other) : .5;

        /// <summary>Signed lead relative to the smaller army, so both sides show the same gap.</summary>
        public static double RelativePercent(double own, double other)
        {
            if (own == other) return 0;
            double smaller = Math.Min(own, other);
            if (smaller <= 0) return own > other ? double.PositiveInfinity : double.NegativeInfinity;
            return (own > other ? 1 : -1) * (Math.Max(own, other) / smaller - 1) * 100;
        }
    }
}
