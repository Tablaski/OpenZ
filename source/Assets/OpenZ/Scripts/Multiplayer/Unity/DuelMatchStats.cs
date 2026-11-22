using System;
using System.IO;
using System.Linq;
using OpenZ.Sim;

namespace OpenZ.Multiplayer
{
    /// <summary>Authoritative final counters: both peers show the host's match results.</summary>
    [Serializable] public sealed class DuelMatchStats
    {
        [Serializable] public struct Sample { public float time; public int red, blue; public int[] forces; }
        [Serializable] public struct Capture { public float time; public Team team; }
        [Serializable] public sealed class TeamCounters
        {
            public int[] built, kills, losses, peak;
            public Team[] forts;
        }
        public int[] built, kills, losses, peak;
        public TeamCounters allTeams;
        public Sample[] samples;
        public Capture[] captures;
        public Team[] forts;
        static int[] Values(System.Collections.Generic.Dictionary<Team, int> values, int teamCount) =>
            Enumerable.Range(1, teamCount).Select(team => OpenZMatchStats.Get(values, (Team)team)).ToArray();
        public static DuelMatchStats From(OpenZMatchStats stats, int teamCount) => new DuelMatchStats
        {
            built = Values(stats.Built, 2), kills = Values(stats.Kills, 2),
            losses = Values(stats.Losses, 2), peak = Values(stats.PeakZones, 2),
            allTeams = teamCount > 2 ? new TeamCounters
            {
                built = Values(stats.Built, teamCount), kills = Values(stats.Kills, teamCount),
                losses = Values(stats.Losses, teamCount), peak = Values(stats.PeakZones, teamCount),
                forts = stats.FortLost.ToArray()
            } : null,
            samples = stats.Samples.Where((s, i) => i % Math.Max(1, (stats.Samples.Count + 511) / 512) == 0).Select(s => new Sample { time = s.t, red = s.forces[0], blue = s.forces[1], forces = teamCount > 2 ? s.forces : null }).ToArray(),
            captures = stats.Captures.Take(512).Select(c => new Capture { time = c.t, team = c.team }).ToArray(), forts = stats.FortLost.Where(team => team == Team.Red || team == Team.Blue).ToArray()
        };
        static void ApplyValues(int[] values, System.Collections.Generic.Dictionary<Team, int> destination)
        {
            if (values == null || (values.Length != 2 && values.Length != OpenZMatchStats.TeamCount) || values.Any(v => v < 0)) throw new InvalidDataException("Invalid final counter.");
            destination.Clear();
            for (int i = 0; i < values.Length; i++) destination[(Team)(i + 1)] = values[i];
        }
        public void Apply(OpenZMatchStats stats)
        {
            if (samples == null || captures == null || forts == null || samples.Length > 512 || captures.Length > 512 || forts.Length > OpenZMatchStats.TeamCount) throw new InvalidDataException("Invalid final statistics.");
            ApplyValues(allTeams?.built ?? built, stats.Built);
            ApplyValues(allTeams?.kills ?? kills, stats.Kills);
            ApplyValues(allTeams?.losses ?? losses, stats.Losses);
            ApplyValues(allTeams?.peak ?? peak, stats.PeakZones);
            stats.Samples.Clear();
            foreach (var sample in samples)
            {
                var forces = sample.forces;
                if (forces == null || forces.Length == 0)
                {
                    forces = new int[OpenZMatchStats.TeamCount];
                    forces[0] = sample.red;
                    forces[1] = sample.blue;
                }
                if (forces.Length != OpenZMatchStats.TeamCount || forces.Any(count => count < 0))
                    throw new InvalidDataException("Invalid final force counts.");
                stats.Samples.Add((sample.time, forces));
            }
            stats.Captures.Clear(); foreach (var c in captures) stats.Captures.Add((c.time, c.team));
            stats.FortLost.Clear(); foreach (var f in allTeams?.forts ?? forts) stats.FortLost.Add(f);
        }
    }
}
