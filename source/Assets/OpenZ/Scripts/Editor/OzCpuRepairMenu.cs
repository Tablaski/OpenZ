#if !OPENZ_HARNESS
using OpenZ.Sim;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>The 1.2 item 13 menu (rule DV137, Sim/ZBot.CpuRepair.cs: the computer sends its strongest damaged vehicle to its repair station
    /// and back, once every 5 minutes): the headless counted check Editor/OzV12CpuRepairCount.cs (outside Play; whole matches, several minutes),
    /// and in Play a look at what the running game's computer players did (their repair trips so far, the trip under way, the next one's time).</summary>
    public static class OzCpuRepairMenu
    {
        [MenuItem("OpenZ/V1.2/CPU: 13 Repairs Check (headless)")]
        static void Count() => OzV11Kit.RunMenu("1.2 item 13 the computer repairs (DV137)", () => OzV12CpuRepairCount.Check(OzV11Kit.SettingsText, OzV11Kit.MapText));

        [MenuItem("OpenZ/V1.2/CPU: 13 Repairs Watch (Play)")]
        static void Watch()
        {
            var g = ZGame.I; var w = g != null ? g.World : null;
            if (!Application.isPlaying || w == null) { Debug.LogWarning("[OpenZ] 1.2 item 13 repairs watch: start a level in Play first"); return; }
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[OpenZ] 1.2 item 13 repairs watch at {(int)(w.Time / 60)}:{(int)(w.Time % 60):00}: rule {(ZWorld.CpuRepairDeviation ? "ON" : "off")}, the pad repair {(ZWorld.RepairInPlaceDeviation ? "on" : "OFF (the rule waits for it)")}");
            foreach (var bot in w.Bots)
            {
                sb.AppendLine($"{bot.Team} computer: trips {bot.CpuRepairTrips}, refused {bot.CpuRepairRefused}, the next one from {(bot.CpuRepairNextAt < 0 ? "-" : $"{(int)(bot.CpuRepairNextAt / 60)}:{(int)(bot.CpuRepairNextAt % 60):00}")}" +
                              (bot.CpuRepairUnitId >= 0 ? $", under way: {bot.CpuRepairNow.type}#{bot.CpuRepairUnitId} to repair#{bot.CpuRepairNow.stationId}" : ""));
                foreach (var t in bot.CpuRepairLog)
                    sb.AppendLine($"   {(int)(t.sentAt / 60)}:{(int)(t.sentAt % 60):00} {t.type}#{t.unitId} {t.hpAtSend}/{t.maxHp} -> repair#{t.stationId}: {t.end} after {t.endedAt - t.sentAt:0} s, hit points {t.hpEnd}/{t.maxHp}, {(t.BackPx < 0 ? "-" : t.BackPx + " px")} from where it was");
            }
            Debug.Log(sb.ToString());
        }
    }
}
#endif
