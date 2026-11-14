#if !OPENZ_HARNESS
using System.Linq;
using System.Text;
using OpenZ.Sim;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>The 1.2 item 12 menus (rule DV138, Sim/ZBot.CpuShuttle.cs: the computer flies its shuttles): the headless counted check
    /// Editor/OzV12CpuShuttleCount.cs (outside Play, about 3 minutes); in Play the proof View/OzCpuShuttleProof.cs (ships dropped by the
    /// computer's fort, its run watched, pictures) and a look at what the computer's shuttle plan is doing (each computer player's state, its
    /// ships and its last rows).</summary>
    public static class OzCpuShuttleMenu
    {
        [MenuItem("OpenZ/V1.2/CPU: 12 Shuttles Check (headless)")]
        static void Count() => OzV11Kit.RunMenu("1.2 item 12 the computer flies its shuttles (DV138)",
            () => OzV12CpuShuttleCount.Check(OzV11Kit.SettingsText, OzV11Kit.MapText("L02"), OzV11Kit.MapText("L16"), OzV11Kit.MapText("L17")));

        [MenuItem("OpenZ/V1.2/CPU: 12 Shuttles Proof (Play)")]
        static void Proof()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[OpenZ] 1.2 item 12 shuttles proof: start a level in Play first (level 2 recommended)"); return; }
            OzCpuShuttleProof.Run();
            Debug.Log("[OpenZ] 1.2 item 12 shuttles proof started (up to 10 minutes of game time); its report: OzCpuShuttleProof.Report, the log and pictures in " + OzCpuShuttleProof.Folder());
        }

        [MenuItem("OpenZ/V1.2/CPU: 12 Shuttles Status (Play)")]
        static void Status()
        {
            var g = Object.FindFirstObjectByType<ZGame>();
            var w = g != null ? g.World : null;
            if (!Application.isPlaying || w == null) { Debug.LogWarning("[OpenZ] 1.2 item 12: start a level in Play first"); return; }
            var sb = new StringBuilder();
            sb.AppendLine($"1.2 item 12 computer shuttles (DV138 {(ZWorld.CpuShuttleDeviation ? "on" : "OFF")}), game time {w.Time:F0} s");
            foreach (var b in w.Bots)
            {
                sb.AppendLine($"  {b.Team}{(b.Ignored ? " (ignored)" : "")}: {b.CpuShuttlePhase}, ship #{b.CpuShuttleShip}, run {(b.CpuShuttleRunActive ? "on" : "none")}, next take-off from {b.CpuShuttleCooldownUntil:F0} s; " +
                              $"runs {b.CpuShuttleRuns}, landings {b.CpuShuttleLandings}, unloads {b.CpuShuttleUnloads}, captures {b.CpuShuttleCaptures}, empty ships taken {b.CpuShuttleEmptyTaken}");
                foreach (var e in b.CpuShuttleEvents.Skip(System.Math.Max(0, b.CpuShuttleEvents.Count - 12)))
                    sb.AppendLine($"     {e.time,6:F0} s {e.kind} ship #{e.ship}{(e.flag >= 0 ? " flag #" + e.flag : "")}{(e.unit >= 0 ? " unit #" + e.unit : "")} tile {e.x >> 4},{e.y >> 4}{(e.threatFlag >= 0 ? $" defence {e.threatFlag}/{e.threatSpot}" : "")}{(e.note != null ? " (" + e.note + ")" : "")}");
            }
            foreach (var s in w.Objects.Where(o => o.IsShuttle && !o.Destroyed))
                sb.AppendLine($"  ship #{s.Id} {s.Owner}: {s.ShuttleState}, tile {s.CenterX >> 4},{s.CenterY >> 4}, pilot {s.Drivers.Count}, aboard {s.Passengers.Count}, flights {s.ShuttleFlights}, landings {s.ShuttleLandings}, hovers {s.ShuttleHovers}");
            Debug.Log("[OpenZ] " + sb);
        }
    }
}
#endif
