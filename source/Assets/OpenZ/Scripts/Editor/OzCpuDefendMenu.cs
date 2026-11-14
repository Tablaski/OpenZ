#if !OPENZ_HARNESS
using System.Linq;
using System.Text;
using OpenZ.Sim;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>The 1.2 items 14 + 14a menus (rule DV136, Sim/ZBot.CpuDefend.cs: from minute 8 one in four of the computer's attack / capture
    /// orders defends a frontier flag for two minutes): the headless counted check Editor/OzV12CpuDefendCount.cs (outside Play, three levels,
    /// two seeds, both sides, the rule off and on, plus one EASY match: about 15 minutes), and in Play a list of the computer's guards now
    /// (where to look for the pictures).</summary>
    public static class OzCpuDefendMenu
    {
        [MenuItem("OpenZ/V1.2/CPU: 14 Defend Orders Check (headless)")]
        static void Count() => OzV11Kit.RunMenu("1.2 items 14 + 14a the computer's defend orders (DV136)", () => OzV12CpuDefendCount.Check(OzV11Kit.SettingsText, OzV11Kit.MapText, 2, 22, null, null, 1, null, false, true));

        [MenuItem("OpenZ/V1.2/CPU: 14 Defend Guards Now (Play)")]
        static void Now()
        {
            var g = ZGame.I;
            if (!Application.isPlaying || g == null || g.World == null) { Debug.LogWarning("[OpenZ] 1.2 item 14 defend guards: start a level in Play first (the rule acts from 8:00 of the game clock)"); return; }
            var w = g.World; var sb = new StringBuilder();
            sb.AppendLine($"[OpenZ] 1.2 item 14 defend guards at {(int)(w.Time / 60)}:{(int)(w.Time % 60):00} (the rule {(ZWorld.CpuDefendDeviation ? "ON" : "off")}; it acts from {(int)(ZWorld.CpuDefendStartSec / 60)}:00)");
            foreach (var b in w.Bots)
            {
                sb.AppendLine($"{b.Team} computer: {b.DefendOrders} defend orders of {b.DefendCounted} counted, {b.DefendGuardCount} guards now, {b.DefendReleased} released, {b.DefendLost} lost");
                foreach (var v in b.DefendGuards().OrderBy(v => v.flagId).ThenBy(v => v.unitId))
                {
                    var o = w.Get(v.unitId); var f = w.Get(v.flagId);
                    if (o == null) continue;
                    sb.AppendLine($"   {o.Type}#{o.Id} at tile ({o.CenterX / 16},{o.CenterY / 16}), spot tile ({v.spotX / 16},{v.spotY / 16}), flag #{v.flagId}{(f != null ? $" at tile ({f.CenterX / 16},{f.CenterY / 16})" : "")}, " +
                                  $"enemy direction ({v.dirX:0.00},{v.dirY:0.00}), {(v.holding ? $"holding for {w.Time - v.arrived:0} s" : v.arrived >= 0 ? "walking back to its spot" : $"on its way for {w.Time - v.ordered:0} s")}");
                }
            }
            Debug.Log(sb.ToString());
        }
    }
}
#endif
