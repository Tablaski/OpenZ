using System;
using System.Collections.Generic;

namespace OpenZ.Multiplayer
{
    internal enum MapChangeBallot { Go, Veto }
    internal enum MapChangeResult { Pending, Apply, Refuse }

    /// <summary>Host-owned vote. Process ballots in their authoritative receipt order.</summary>
    internal sealed class MapChangeVote
    {
        public const double DurationSeconds = 30;

        readonly HashSet<int> eligible;
        readonly HashSet<int> voted = new HashSet<int>();
        readonly int threshold;

        public double Deadline { get; }
        public int GoCount { get; private set; }
        public int VetoCount { get; private set; }
        public MapChangeResult Result { get; private set; }

        public MapChangeVote(IEnumerable<int> humanSeats, double openedAt)
        {
            eligible = new HashSet<int>(humanSeats);
            if (eligible.Count == 0 || eligible.Count > 8)
                throw new ArgumentException("A map vote requires between one and eight human seats.", nameof(humanSeats));
            foreach (int seat in eligible)
                if (seat < 0 || seat >= 8) throw new ArgumentOutOfRangeException(nameof(humanSeats));
            if (double.IsNaN(openedAt) || double.IsInfinity(openedAt) || openedAt < 0)
                throw new ArgumentOutOfRangeException(nameof(openedAt));
            Deadline = openedAt + DurationSeconds;
            threshold = (eligible.Count + 1) / 2;
        }

        public MapChangeResult Cast(int seat, MapChangeBallot ballot, double receivedAt)
        {
            if (Advance(receivedAt) != MapChangeResult.Pending || !eligible.Contains(seat) ||
                (ballot != MapChangeBallot.Go && ballot != MapChangeBallot.Veto) || !voted.Add(seat))
                return Result;

            if (ballot == MapChangeBallot.Go && ++GoCount == threshold) Result = MapChangeResult.Apply;
            if (ballot == MapChangeBallot.Veto && ++VetoCount == threshold) Result = MapChangeResult.Refuse;
            return Result;
        }

        public MapChangeResult Advance(double now)
        {
            if (Result == MapChangeResult.Pending && now >= Deadline) Result = MapChangeResult.Apply;
            return Result;
        }
    }

}
