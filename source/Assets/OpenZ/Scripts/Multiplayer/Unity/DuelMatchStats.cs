using System;
using System.IO;
using System.Linq;
using OpenZ.Sim;

namespace OpenZ.Multiplayer
{
    /// <summary>Authoritative final counters: both peers show the host's match results.</summary>
    [Serializable] public sealed class DuelMatchStats
    {
        [Serializable] public struct Sample { public float time; public int red, blue; }
        [Serializable] public struct Capture { public float time; public Team team; }
        public int[] built, kills, losses, peak;
        public Sample[] samples;
        public Capture[] captures;
        public Team[] forts;
        static int[] Values(System.Collections.Generic.Dictionary<Team, int> values) => new[] { OpenZMatchStats.Get(values, Team.Red), OpenZMatchStats.Get(values, Team.Blue) };
        public static DuelMatchStats From(OpenZMatchStats stats) => new DuelMatchStats
        {
            built = Values(stats.Built), kills = Values(stats.Kills), losses = Values(stats.Losses), peak = Values(stats.PeakZones),
            samples = stats.Samples.Where((s, i) => i % Math.Max(1, (stats.Samples.Count + 511) / 512) == 0).Select(s => new Sample { time = s.t, red = s.red, blue = s.blue }).ToArray(),
            captures = stats.Captures.Take(512).Select(c => new Capture { time = c.t, team = c.team }).ToArray(), forts = stats.FortLost.ToArray()
        };
        static void ApplyValues(int[] values, System.Collections.Generic.Dictionary<Team, int> destination)
        {
            if (values == null || values.Length != 2 || values.Any(v => v < 0)) throw new InvalidDataException("Invalid final counter.");
            destination.Clear(); destination[Team.Red] = values[0]; destination[Team.Blue] = values[1];
        }
        public void Apply(OpenZMatchStats stats)
        {
            if (samples == null || captures == null || forts == null || samples.Length > 512 || captures.Length > 512 || forts.Length > 2) throw new InvalidDataException("Invalid final statistics.");
            ApplyValues(built, stats.Built); ApplyValues(kills, stats.Kills); ApplyValues(losses, stats.Losses); ApplyValues(peak, stats.PeakZones);
            stats.Samples.Clear(); foreach (var s in samples) stats.Samples.Add((s.time, s.red, s.blue));
            stats.Captures.Clear(); foreach (var c in captures) stats.Captures.Add((c.time, c.team));
            stats.FortLost.Clear(); foreach (var f in forts) stats.FortLost.Add(f);
        }
    }
}
