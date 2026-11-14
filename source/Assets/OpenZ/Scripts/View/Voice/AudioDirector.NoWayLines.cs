// OpenZ (GPL-3.0) - version 1.2, item 62: the no-way answer of a weak unit sent at a far stronger one, as in Zod, heard again
// (View/AudioDirector.cs PollInput calls OrderVoice; the pacing rule is OzNoWayGate in View/Voice/OzOrderKillRules.cs).
using System.Collections;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// Version 1.2 item 62: "when in Z we ask a super weak unit to kill super strong they say like 'you joking' lines ... when you ask
    /// grunt to kill heavy tank or heavy turret he goes for it but says lines like you must be joking ... check that they dont repeat
    /// too often, and dont overlap".
    /// Zod's rule, as the game had it (NoWayOrder in View/AudioDirector.cs = zplayer.cpp DevWayPointsNoWay + the UCR_WILL_DIE rows of
    /// zunitrating.cpp): exactly one unit selected (a squad counts as one), its order an attack, the target a unit type it cannot beat
    /// (a grunt at any tank, jeep, gun or stronger robot; a jeep at a tank or a heavy gun; ...) -> a no-way line instead of the
    /// acknowledgement. The order itself goes out as always: the unit goes. A building or an even fight never gets one.
    /// Why it was hardly ever heard: the line went through the ordinary order answer, which says nothing while a robot line has more
    /// than unitOrderWaitMax s left and for AlanMainGap s after any robot line ends; the usual click (select the unit, then send it)
    /// comes right after the selection line, so the no-way line was dropped. The four types of the second voice (psycho, laser, heavy,
    /// missile launcher) had no no-way line at all. Now:
    ///  - the no-way line (the vo_order_no_way pool: "No way!", "Forget it!", "Get outta here!", "You're joking!" and the rest) waits for
    ///    the voices (both robot channels, the announcer, Commander Zod, the shuttle landing line) to be quiet for a breath, at most
    ///    OzNoWayGate.Wait s after the order, and then plays; it never starts over another voice and never cuts one;
    ///  - at most one no-way answer per OzNoWayGate.Gap s; an order inside that gap gets its normal answer; an order given while the
    ///    line still waits gets no second answer on top; never the same line twice in a row;
    ///  - the second voice has no such takes: those types use the takes of the shared robot voice (the voice of the robots' shared
    ///    shouts); a hero answers as before (ALAN with this line, BRAD stays quiet: no take in his voice);
    ///  - it is a unit's answer to an order, not a line about the state of a match: the Unit editor's playground plays it too.
    /// View only: the simulation is untouched.
    /// </summary>
    public partial class AudioDirector
    {
        public const string NoWayEvent = "vo_order_no_way";
        readonly OzNoWayGate nwGate = new OzNoWayGate();
        /// <summary>Checks: the no-way gate of this level's director (its counts and the last line).</summary>
        public static OzNoWayGate NoWayGate => I ? I.nwGate : null;
        /// <summary>Checks: the last no-way clip that played, when it started (unscaled s), and whether another voice played at its start.</summary>
        public static string NoWayLastClip { get; private set; }
        public static float NoWayLastStart { get; private set; } = -100f;
        public static bool NoWayLastOverVoice { get; private set; }

        /// <summary>Zod's strength test of one attacker type against one target type (the game's own table, NoWayOrder).</summary>
        public static bool NoWayPair(string attacker, string target) =>
            attacker != null && target != null && WillDie.TryGetValue(attacker, out var loses) && loses.Contains(target);

        /// <summary>PollInput: the voice of the order the player just gave (unit_voices_v1, and item 62's no-way answer).</summary>
        void OrderVoice(bool attack)
        {
            if (LavaOrderVoice()) return;                                      // level 7: one robot's line at a click on lava (Voice/AudioDirector.LavaLines.cs)
            if (nwGate.Waiting) { OzSoundLog.Add("dir", NoWayEvent, null, "an order while the no-way line waits: no second answer on top"); return; }
            bool noWay = attack && NoWayOrder() && NoWayDue();
            var one = noWay ? input.Selection[0] : null;
            if (one != null && !one.IsHero) { NoWaySay(one); return; }          // not silenced by the order answer's waiting rules: it waits itself
            if (UvOrder(attack, noWay)) return;                                // a type line, a hero's own line, or silence on purpose
            if (noWay) NoWaySay(one);                                          // a hero: ALAN's no-way line
            else ClassicOrderLine(attack, false);                              // the shared pool of the answering unit's voice
        }

        /// <summary>An order Zod answers with a no-way line: true = this one may; false = its normal answer (inside the gap).</summary>
        bool NoWayDue()
        {
            double now = Time.unscaledTimeAsDouble;
            if (nwGate.Due(now)) return true;
            OzSoundLog.Add("dir", NoWayEvent, null, nwGate.Waiting ? "no-way line already waiting: the normal answer"
                                                                   : "no-way gap (" + (now - nwGate.Last).ToString("0.0") + " of " + nwGate.Gap.ToString("0") + " s): the normal answer");
            return false;
        }

        void NoWaySay(ZObject u)
        {
            var d = Def(NoWayEvent);
            if (u == null || d == null || d.loaded == null || d.loaded.Count == 0 || !voices) return;
            string line = nwGate.Ask(d.lines, Time.unscaledTimeAsDouble, () => Random.value);
            if (line == null) return;
            var target = u.Waypoints.Count > 0 ? W.Get(u.Waypoints[u.Waypoints.Count - 1].refId) : null;
            string what = u.Type + " #" + u.Id + " sent at " + (target != null ? target.Type + " #" + target.Id : "?");
            OzSoundLog.Add("dir", NoWayEvent, null, "no-way: " + what + " (it goes); \"" + line + "\" waits for quiet voices");
            StartCoroutine(NoWayRun(what));
        }

        /// <summary>No voice the robots must not talk over plays now.</summary>
        bool NoWayQuiet() =>
            alanMain != null && alanAlert != null && rachel != null && !AlanBusy && !(rachel.busy && rachel.src && rachel.src.isPlaying)
            && !(ExternalVoiceBusy != null && ExternalVoiceBusy()) && !ShuttleRachelLanding && pendingPromo == null
            && QuietLineFloorFree();                                           // 1.4 radar_voice_fix: and the voice floor is free (Rachel's queue empty, 0.3 s after any voice, no hero exchange open): the line then starts at once, never in the robot pending slot (View/AudioDirector.VoiceFloor.cs)

        IEnumerator NoWayRun(string what)
        {
            while (true)
            {
                var s = nwGate.Tick(Time.unscaledTimeAsDouble, NoWayQuiet());
                if (s == OzNoWayGate.Step.Say) break;
                if (s != OzNoWayGate.Step.Wait)
                {
                    OzSoundLog.Add("dir", NoWayEvent, null, "no-way line dropped: the voices did not go quiet within " + nwGate.Wait.ToString("0.0") + " s");
                    yield break;
                }
                yield return null;
            }
            string line = nwGate.Pending;
            int before = Plays.TryGetValue(NoWayEvent, out var n0) ? n0 : 0;
            float t0 = Time.unscaledTime;
            bool keep = shuttleReleasing; shuttleReleasing = true;              // it waited for quiet voices itself: no robot line gap on top
            try { SayImpl(NoWayEvent, "=" + line); } finally { shuttleReleasing = keep; }
            bool played = (Plays.TryGetValue(NoWayEvent, out var n1) ? n1 : 0) > before && alanMain.src && alanMain.src.isPlaying;
            nwGate.Spoke(Time.unscaledTimeAsDouble, played);
            if (!played) { OzSoundLog.Add("dir", NoWayEvent, null, "no-way line held by the voice rules: \"" + line + "\""); yield break; }
            NoWayLastClip = alanMain.src.clip ? alanMain.src.clip.name : null; NoWayLastStart = t0;
            NoWayLastOverVoice = (alanAlert.src && alanAlert.src.isPlaying) || (rachel.src && rachel.src.isPlaying);
            OzSoundLog.Add("dir", NoWayEvent, NoWayLastClip, "no-way line: " + what + ", \"" + line + "\"");
        }
    }
}
