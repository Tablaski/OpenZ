using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    // ============================================================================================================================
    // 1.2 items 25 / 25a: the repair lines (View/Voice/OzRepairLines.cs says which vehicles an order sent and which line is next).
    // Hook (one line in UvOrder, AudioDirector.UnitVoices.cs): when the order just given sends vehicles to a repair station (the R key,
    // the wrench key, the touch / gamepad REPAIR key: all PlayerInput.SendToRepair; or a right-click on an own repair station with
    // damaged vehicles selected: their UNIT_REPAIR order), ONE of those vehicles answers with a line of its own repair pool
    // vo_order_x_<type>_repair instead of a move line. The speaker is the unit that spoke last if it is among them, else one at random
    // (as every order). The line goes through SayImpl on Alan's main channel with the order rules of UnitVoices.DecideOrder: never
    // over another robot line, the quiet gap after a line (AlanMainGap), one answer per right-click burst, the voice and robot chatter
    // options (every vo_order line stays even on MINIMAL). The one difference from a move answer: a repair line that meets a selection
    // line (or the quiet gap after one) is not dropped but waits until the gap is over (unitRepairWait s at most, while the vehicle is
    // still on its way and no newer order came), because a repair order usually follows the selection of the damaged vehicle at once;
    // behind an order line it stays silent like every order. A refused repair order (no own station, nothing damaged, every damaged
    // one already on its way) gives no order marker, so it keeps its own blip and says nothing.
    // Console proof: "[repair_lines] live: ..." once when the pools load, "[repair_lines] first <event>: ..." the first time each
    // pool really plays.
    // ============================================================================================================================
    public partial class AudioDirector
    {
        [Header("Repair lines (1.2 item 25)")]
        [Tooltip("A vehicle sent to a repair station (R, the wrench key, the touch / gamepad REPAIR key, a right-click on the station) says one of its repair lines (off = it answers like a move order, as before)")]
        public bool unitRepairLines = true;
        [Tooltip("Share of repair orders that say a repair line (the rest answer like a move order)")]
        [Range(0, 1)] public float unitRepairShare = 1f;
        [Tooltip("A repair line that meets a speaking robot line or the quiet gap after one waits until the channel is free and the gap is " +
                 "over, at most this many seconds after the order, and only while its vehicle is still on its way and no newer order was " +
                 "given (0 = it is dropped like the other order lines)")]
        [Range(0f, 8f)] public float unitRepairWait = 5f;
        int rvWaitSerial;

        readonly Dictionary<string, List<string>> rvLines = new Dictionary<string, List<string>>();
        readonly Dictionary<string, SoundEventDef> rvLinesOf = new Dictionary<string, SoundEventDef>();
        readonly HashSet<string> rvFirst = new HashSet<string>();
        bool rvBootLogged;
        /// <summary>Proof: the last repair line that started (event, line, the clip, the unit that said it) and when.</summary>
        public static string RepairLastEvent, RepairLastLine, RepairLastClip; public static int RepairLastUnit = -1; public static float RepairLastAt = -10f;
        /// <summary>Proof: Alan's main channel is silent and its quiet gap over (a select / order line may start now).</summary>
        public static bool RobotChannelFree => I && !I.AlanMainSpeaking && Time.unscaledTime >= I.alanMainUntil + AlanMainGap + 0.1f;

        /// <summary>The distinct lines of a repair pool (its loaded clips), in table order; rebuilt when the table reloads.</summary>
        List<string> RvLines(string id)
        {
            var d = Def(id);
            if (rvLines.TryGetValue(id, out var l) && rvLinesOf.TryGetValue(id, out var of) && of == d) return l;
            l = new List<string>();
            if (d != null && d.lines != null) foreach (var s in d.lines) if (!string.IsNullOrEmpty(s) && !l.Contains(s)) l.Add(s);
            rvLines[id] = l; rvLinesOf[id] = d;
            return l;
        }

        void RvBootLog()
        {
            rvBootLogged = true;
            if (!unitVoicesLog) return;
            int pools = 0, clips = 0;
            foreach (var t in OzRepairLines.Types) { var d = Def(OzRepairLines.EventFor(t)); if (d != null && d.loaded != null && d.loaded.Count > 0) { pools++; clips += d.loaded.Count; } }
            if (pools > 0) Debug.Log($"[repair_lines] live: {pools} of {OzRepairLines.Types.Length} vehicle repair pools, {clips} clips (share {unitRepairShare:P0})");
            else Debug.LogWarning("[repair_lines] no vo_order_x_*_repair pools in the sound table: repair orders answer like move orders (apply k25_repair_lines)");
        }

        /// <summary>UvOrder hook. true = a repair order: a sent vehicle said (or started) its repair line, or the order stays silent on
        /// purpose (a robot line speaking, the quiet gap, spam, the options). false = not a repair order (or no pool for the speaker's
        /// type): the order answers as before.</summary>
        bool RvOrder()
        {
            if (!unitRepairLines || input == null) return false;
            if (!rvBootLogged && Loaded) RvBootLog();
            List<ZObject> sent = null;
            foreach (var o in input.Selection) if (o != null && o.Owner == Player && OzRepairLines.Sent(o)) (sent ??= new List<ZObject>()).Add(o);   // own vehicles only
            if (sent == null) return false;
            var u = UvPickSpeaker(sent);
            string id = OzRepairLines.EventFor(u.Type);
            var pool = RvLines(id);
            if (pool.Count == 0) return false;                                                    // a vehicle type without a repair pool: as before
            if (unitRepairShare < 1f && UnityEngine.Random.value >= unitRepairShare) return false; // the share left to the move answers
            OzRepairLines.Orders++;
            uvOrderUnit = u; uvSpeaker = u.Id;
            var uv = Uv();
            switch (uv.DecideOrder(false, UvCanSpeak(id), false, UvChannel(), () => -1.0, Time.unscaledTimeAsDouble))   // -1: the move share never applies (unitRepairShare is the repair share)
            {
                case UnitVoices.OrderAct.TypeLine:
                    if (!RvSay(u, id, OzRepairLines.Pick(pool, () => UnityEngine.Random.value), false)) RvWait(u, id);   // the quiet gap: it waits
                    return true;
                case UnitVoices.OrderAct.Nothing: RvWait(u, id); return true;                       // a line speaking / a burst: it waits
                default: OzRepairLines.Silent++; return true;                                       // muted (the voice options): silent, never a move line
            }
        }

        /// <summary>The repair line waits for a free channel and the end of the quiet gap (unitRepairWait s at most), but only behind a line
        /// that is not an order answer (a selection line, a joke, a shout): behind an order line (right-click spam, two orders in a row)
        /// it stays silent, as every order (one answer per burst). Only the latest repair order waits; any newer order (a new order
        /// marker), the trip called off or the vehicle lost drops it.</summary>
        void RvWait(ZObject u, string id)
        {
            rvWaitSerial++;
            if (unitRepairWait <= 0f || input == null || UnitVoices.IsOrderLine(alanMainId)) { OzRepairLines.Silent++; return; }
            StartCoroutine(RvWaitRun(u, id, rvWaitSerial, input.MarkerTime));
        }

        IEnumerator RvWaitRun(ZObject u, string id, int serial, float marker)
        {
            float t0 = Time.unscaledTime;
            while (Time.unscaledTime - t0 <= unitRepairWait)
            {
                if (serial != rvWaitSerial || !input || input.MarkerTime != marker || !OzRepairLines.Sent(u)) break;
                if (!AlanMainSpeaking && Time.unscaledTime >= alanMainUntil + AlanMainGap + 0.05f)
                {   // the channel is free and the gap over: one try (a line held by another rule, e.g. the shuttle landing line, is not asked again)
                    if (UvCanSpeak(id) && RvSay(u, id, OzRepairLines.Pick(RvLines(id), () => UnityEngine.Random.value), true)) { OzRepairLines.Waited++; yield break; }
                    break;
                }
                yield return null;
            }
            OzRepairLines.Silent++;
        }

        /// <summary>Says the repair line through SayImpl (the never-cut rule, the quiet gap, the cooldown, the options); it counts as said
        /// only when it really starts.</summary>
        bool RvSay(ZObject u, string id, string line, bool waited)
        {
            if (string.IsNullOrEmpty(line)) return false;
            float t0 = Time.unscaledTime;
            SayImpl(id, "=" + line);
            if (!(lastLine.TryGetValue(id, out var lt) && lt >= t0 && UvPlaysNow(id, line))) return RvSayWaits(u, id, line, t0);   // 1.4 radar_voice_fix: true = it waits for the voice floor (committed when it starts)
            OzRepairLines.Commit(line);
            RepairLastEvent = id; RepairLastLine = line; RepairLastUnit = u != null ? u.Id : -1; RepairLastAt = t0;
            RepairLastClip = alanMain.src && alanMain.src.clip ? alanMain.src.clip.name : null;
            if (unitVoicesLog && rvFirst.Add(id))
                Debug.Log($"[repair_lines] first {id}: \"{line}\" (playing{(waited ? " after the quiet gap" : "")}, {(u != null ? u.Type + "#" + u.Id : "?")}; {RvLines(id).Count} lines in the pool)");
            return true;
        }

        /// <summary>Checks: the next repair line of a type now, through the game's path (e.g. <c>return OpenZ.AudioDirector.DebugRepairLine("heavy");</c>).</summary>
        public static string DebugRepairLine(string type)
        {
            if (!I) return "no AudioDirector";
            if (!OzRepairLines.Known(type)) return "type = " + string.Join(" / ", OzRepairLines.Types);
            string id = OzRepairLines.EventFor(type);
            var pool = I.RvLines(id);
            if (pool.Count == 0) return $"no lines for {type} ({id} missing: apply k25_repair_lines)";
            var line = OzRepairLines.Pick(pool, () => UnityEngine.Random.value);
            bool on = I.RvSay(null, id, line, false);
            return $"{type}: \"{line}\" ({(on ? "playing" : "dropped: a robot line speaks, the quiet gap, the cooldown or muted")}) ({pool.Count} lines) | {OzRepairLines.Summary()}";
        }

        /// <summary>Play proof: says every (event, line) once: waits for Alan's speaking line and the quiet gap first, so every line starts,
        /// then logs the clip alan_main plays.</summary>
        internal IEnumerator RepairLinesPlayRun(List<KeyValuePair<string, string>> takes, StringBuilder log, Action done)
        {
            foreach (var t in takes)
            {
                string ev = t.Key, line = t.Value;
                float t0 = Time.unscaledTime;
                while ((AlanBusy || Time.unscaledTime < alanMainUntil + AlanMainGap + 0.1f) && Time.unscaledTime - t0 < 12f) yield return null;
                lastLine.Remove(ev);                                                                 // the proof paces itself (no cooldown)
                SayImpl(ev, "=" + line);
                yield return null;
                bool on = alanMain.src && alanMain.src.isPlaying && alanMain.src.clip && alanMain.def != null && alanMain.def.id == ev;
                string clip = on ? alanMain.src.clip.name + " (" + alanMain.src.clip.length.ToString("0.00") + " s)" : "-";
                string row = "[repair_lines] " + (on ? "OK   " : "FAIL ") + ev + " \"" + line + "\" clip " + clip;
                log.AppendLine(row); Debug.Log(row);
            }
            done?.Invoke();
        }

        /// <summary>The check menu's live block: per vehicle type its repair pool (lines, clips loaded) + the counters.</summary>
        public static string RepairLinesLiveReport()
        {
            if (!I) return "no AudioDirector";
            var sb = new StringBuilder();
            foreach (var t in OzRepairLines.Types)
            {
                var d = I.Def(OzRepairLines.EventFor(t));
                sb.AppendLine($"  - {t}: {(d == null ? "NO POOL" : I.RvLines(d.id).Count + " lines, " + (d.loaded?.Count ?? 0) + " of " + (d.clips?.Length ?? 0) + " clips load")}");
            }
            sb.AppendLine("  - " + OzRepairLines.Summary());
            return sb.ToString();
        }
    }
}
