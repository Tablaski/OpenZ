using System.Collections.Generic;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// Alert voice limiter: repeated alert / callout lines (a building or factory lost, a flag or territory lost, a cry for help /
    /// medic / under attack, a unit lost, the base under attack, warnings) are grouped by meaning; each group speaks at most once per
    /// GroupGap s and any two alert lines are at least GlobalGap s apart. A line the limiter blocks is skipped (never queued) and logged
    /// in OzSoundLog as "DROP alert cooldown". One-off story, briefing, victory / defeat and order / selection lines are not alerts.
    /// AudioDirector.PlayVoice asks Check before a voice line plays and calls Played when it does. View only.
    /// </summary>
    public static class AlertGate
    {
        public static bool Enabled = true;
        public static float GroupGap = 30f, GlobalGap = 7f;
        public static int Dropped, Passed;

        static readonly Dictionary<string, string> groups = new Dictionary<string, string>
        {
            // a building / factory lost
            ["ann_factory_lost"] = "building_lost", ["ann_building_destroyed"] = "building_lost", ["ann_repair_lost"] = "building_lost",
            ["vo_building_events"] = "building_lost",
            // 1.4 radar_voice_fix: "Radar lost." only when our LAST standing radar is taken (AudioDirector.VoiceFloor.cs): its own group, so a
            // factory lost a moment before no longer silences it (still at most once per GroupGap)
            ["ann_radar_lost"] = "radar_lost",
            // a flag / territory lost
            ["ann_territory_lost"] = "territory_lost",
            // help / medic / under attack
            ["vo_under_attack"] = "help", ["vo_under_attack_repeat"] = "help", ["vo_hurt"] = "help", ["vo_low_health"] = "help",
            // the base under attack
            ["ann_fort_under_attack"] = "base_attack", ["ann_base_under_attack"] = "base_attack",
            // a unit lost
            ["ann_vehicle_lost"] = "unit_lost", ["ann_gun_lost"] = "unit_lost", ["ann_driver_sniped"] = "unit_lost",
            // warnings that repeat in big fights
            ["vo_sniper_warning"] = "warning", ["vo_grenade_warning"] = "warning",
            ["ann_youre_losing"] = "losing",
        };
        static readonly Dictionary<string, float> groupLast = new Dictionary<string, float>();
        static float lastAlert = -999f;
        // 1.4 radar_voice_fix: the lines of ONE event ("Territory lost." and "Radar lost." when our last radar is taken) are not held by the
        // global gap between two alerts (each passes once, within its window); their group gaps still hold them
        static readonly Dictionary<string, float> pairs = new Dictionary<string, float>();
        /// <summary>Checks: lines of one event let through the global gap.</summary>
        public static int Paired;
        /// <summary>1.4 radar_voice_fix: the next Check of alert line `id` within `window` s (real time) is a line of the event that is
        /// speaking (the director asks it for both lines of a last-radar loss): the global gap between two alerts does not hold it; its
        /// group gap still does.</summary>
        public static void PairNext(string id, float window) { if (id != null) pairs[id] = Time.unscaledTime + window; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { groupLast.Clear(); lastAlert = -999f; Dropped = Passed = 0; pairs.Clear(); Paired = 0; }

        public static bool IsAlert(string id) => id != null && groups.ContainsKey(id);

        /// <summary>True when alert line `id` may speak now (non-alert lines always may); else `why` says which rule held it.</summary>
        public static bool Check(string id, out string why)
        {
            why = null;
            if (!Enabled || id == null || !groups.TryGetValue(id, out var g)) return true;
            float now = Time.unscaledTime;
            if (groupLast.TryGetValue(g, out var t) && now - t < GroupGap) { why = $"alert cooldown (group {g}: {now - t:F1} of {GroupGap:F0} s)"; Dropped++; return false; }
            bool paired = pairs.TryGetValue(id, out var pu) && now <= pu;                          // 1.4 radar_voice_fix: a line of one event (PairNext)
            if (!paired && now - lastAlert < GlobalGap) { why = $"alert cooldown (global gap {now - lastAlert:F1} of {GlobalGap:F0} s)"; Dropped++; return false; }
            return true;
        }

        /// <summary>An alert line started speaking: its group and the global gap start now.</summary>
        public static void Played(string id)
        {
            if (id == null || !groups.TryGetValue(id, out var g)) return;
            float now = Time.unscaledTime; groupLast[g] = now; lastAlert = now; Passed++;
            if (pairs.Remove(id)) Paired++;                                                        // 1.4 radar_voice_fix: its pass is used
        }
    }
}
