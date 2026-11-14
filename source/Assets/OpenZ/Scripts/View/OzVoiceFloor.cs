using System.Collections.Generic;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// <list type="bullet">
    /// <item>A voice line never starts while another voice line is audible (Rachel, Alan / Brad / the heroes / EvilZod on the robot
    /// channels, a briefing or cutscene voice: Commander Zod, EvilZod's comms, the boys' giggles).</item>
    /// <item>A line that meets a speaking voice WAITS and starts <see cref="GapSec"/> after that voice ended, instead of overlapping it.
    /// How long it may wait is its own: Rachel's lines (the announcer, the radar lines, the strike warning) wait in her queue until the
    /// queue's existing stale rule drops them (ranks 4-9 after 4 s, ranks 1-3 never); a unit reply (select / order / idle, the heroes'
    /// lines) waits at most <see cref="UnitReplyWait"/> s, Alan's call-outs (alerts, quips, warnings) <see cref="RobotCallWait"/> s, the
    /// end lines <see cref="EndLineWait"/> s, else it is skipped (logged "skipped" in OzSoundLog). Only the latest robot line waits.</item>
    /// <item>While Rachel has a line waiting, the robot lines wait behind it (information first; a pair of lines of one event such as
    /// "Territory lost." then "Radar lost." stays back to back).</item>
    /// <item>The briefing / cutscene voices always win: every in-match line waits while they speak.</item>
    /// <item>No line is cut mid-word (the director no longer stops a speaking line for a newer or higher one).</item>
    /// </list>
    /// The rules live in AudioDirector (View/AudioDirector.VoiceFloor.cs); this class holds the numbers, the voice sources and a log of
    /// every voice line's real start and end on its AudioSource (the 1.4 Play check reads it: View/OzVoiceOverlapProof.cs). View only.
    /// </summary>
    public static class OzVoiceFloor
    {
        /// <summary>The quiet time between a voice line's end and a waiting line's start (s, real time).</summary>
        public const float GapSec = 0.3f;
        /// <summary>How long a waiting line may wait before it is skipped (s, real time): unit replies, Alan's call-outs, the end lines.</summary>
        public static float UnitReplyWait = 1.0f, RobotCallWait = 1.5f, EndLineWait = 6f;

        sealed class Src { public AudioSource s; public string who, id; public bool on; public AudioClip clip; public float lastTime; public int rec = -1, serial; }
        static readonly List<Src> sources = new List<Src>(12);
        static float lastAudible = -100f;
        static int frameTicked = -1;

        /// <summary>One voice line as it really played: its source, event (when the director played it), clip, start and end (real
        /// time) and its length at the pitch it played (a line that ended before that was cut).</summary>
        public struct LineRec { public string who, id, clip; public float start, end, len; public bool open; public int serial; }
        public const int LogMax = 512;
        static readonly LineRec[] log = new LineRec[LogMax];
        static int logCount, logHead;
        /// <summary>Lines recorded since the start of Play (the log keeps the last <see cref="LogMax"/>).</summary>
        public static int LinesSeen;

        /// <summary>A robot line that waited for the floor: its event, when it was asked, how long it may wait, when it started (-1 = not
        /// started: skipped or replaced). The Play check measures each started one against the line log.</summary>
        public struct WaitRec { public string id; public float asked, wait, started; }
        static readonly List<WaitRec> waits = new List<WaitRec>(64);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { sources.Clear(); lastAudible = -100f; frameTicked = -1; logCount = logHead = 0; LinesSeen = 0; waits.Clear(); externalSerial = 0; }

        /// <summary>A source counts as audible while it plays a clip (a source the listener pause holds is silent then).</summary>
        public static bool Audible(AudioSource s) => s && s.isPlaying && s.clip && (!AudioListener.pause || s.ignoreListenerPause);

        /// <summary>Fix round 1: a known source caught mid-line by the listener pause (Commander Zod, EvilZod, BS-01: they do not ignore
        /// the pause; Rachel and Alan do). It is silent now but goes on where it stopped when the game goes on, so it keeps the floor: no
        /// line may start on top of it (Rachel's "Game paused." would otherwise start into the frozen line and the line resume under her).</summary>
        static bool Held(Src x) => x.on && x.s && AudioListener.pause && !x.s.ignoreListenerPause && x.s.clip && x.s.time > 0f;

        /// <summary>Any known voice line held mid-line by the listener pause (it keeps the floor).</summary>
        public static bool AnyHeld() { for (int i = 0; i < sources.Count; i++) if (Held(sources[i])) return true; return false; }

        /// <summary>The director (each frame) and other voice players hand their sources here; who = a short name for the log.</summary>
        public static void Sample(AudioSource s, string who, string id)
        {
            if (!s) return;
            for (int i = 0; i < sources.Count; i++) if (sources[i].s == s) { sources[i].who = who; sources[i].id = id; return; }
            sources.Add(new Src { s = s, who = who, id = id });
        }

        /// <summary>The name prefix of an outside voice source (a briefing / cutscene voice registered with AudioDirector.RegisterExternalVoice).</summary>
        public const string External = "external";
        static int externalSerial;

        /// <summary>Fix round 2: an outside voice source (Commander Zod, EvilZod's comms, BS-01, the boys' giggles, a check's own cutscene
        /// source) gets ITS OWN name, given once on first sight ("external:" + its GameObject + "#" + a serial; no string is made every
        /// frame). One name = one source, so the Play check counts two outside voices at once as an overlap (was: every outside source was
        /// sampled as "external", and V1 skipped any pair with one name).</summary>
        public static void SampleExternal(AudioSource s)
        {
            if (!s) return;
            for (int i = 0; i < sources.Count; i++) if (sources[i].s == s) return;
            sources.Add(new Src { s = s, who = External + ":" + s.gameObject.name + "#" + (++externalSerial) });
        }

        /// <summary>A name given by SampleExternal (an outside voice source).</summary>
        public static bool IsExternal(string who) => who != null && who.StartsWith(External + ":");

        /// <summary>The floor's name of a known source (null = not known yet: the director samples it on its next frame).</summary>
        public static string NameOf(AudioSource s) { for (int i = 0; i < sources.Count; i++) if (sources[i].s == s) return sources[i].who; return null; }

        public static bool Known(AudioSource s) { for (int i = 0; i < sources.Count; i++) if (sources[i].s == s) return true; return false; }

        /// <summary>Any known voice source audible now, or held mid-line by the listener pause (it has the floor too); `except` (may be
        /// null) is left out.</summary>
        public static bool AnyAudible(AudioSource except = null)
        {
            for (int i = 0; i < sources.Count; i++) { var x = sources[i]; if (x.s != except && (Audible(x.s) || Held(x))) return true; }
            return false;
        }

        /// <summary>Seconds left of the line audible on the source named `who` (rachel, alan_main, alan_alert, combat_alan, combat_rachel, or
        /// an outside source's own name: NameOf); -1 = none.</summary>
        public static float LeftOn(string who)
        {
            for (int i = 0; i < sources.Count; i++)
            {
                var x = sources[i]; if (x.who != who || !Audible(x.s)) continue;
                return (x.s.clip.length - x.s.time) / Mathf.Max(0.05f, Mathf.Abs(x.s.pitch));
            }
            return -1f;
        }

        /// <summary>Fix round 3: seconds left of the longest voice line audible now on any known source (-1 = none audible; +Infinity = a
        /// line the listener pause holds: when it ends is not known). AudioDirector.FloorFreeAt reads it.</summary>
        public static float LongestLeft()
        {
            float best = -1f;
            for (int i = 0; i < sources.Count; i++)
            {
                var x = sources[i];
                if (Held(x)) return float.PositiveInfinity;
                if (Audible(x.s)) best = Mathf.Max(best, (x.s.clip.length - x.s.time) / Mathf.Max(0.05f, Mathf.Abs(x.s.pitch)));
            }
            return best;
        }

        /// <summary>The director: a robot line starts to wait for the floor (asked now, may wait `wait` s).</summary>
        public static void NoteWait(string id, float asked, float wait)
        {
            if (waits.Count >= LogMax) waits.RemoveAt(0);
            waits.Add(new WaitRec { id = id, asked = asked, wait = wait, started = -1f });
        }

        /// <summary>The director: the latest waiting line of event `id` started now (after its wait).</summary>
        public static void NoteRelease(string id, float now)
        {
            for (int i = waits.Count - 1; i >= 0; i--)
                if (waits[i].id == id && waits[i].started < 0f) { var w = waits[i]; w.started = now; waits[i] = w; return; }
        }

        /// <summary>The waits noted at or after t0 (real time), oldest first.</summary>
        public static List<WaitRec> WaitsSince(float t0)
        {
            var o = new List<WaitRec>();
            foreach (var w in waits) if (w.asked >= t0) o.Add(w);
            return o;
        }

        /// <summary>A briefing / cutscene voice has the floor (AudioDirector.ExternalVoiceBusy: Commander Zod, EvilZod's comms).</summary>
        public static bool ExternalBusy() => AudioDirector.ExternalVoiceBusy != null && AudioDirector.ExternalVoiceBusy();

        /// <summary>The floor is taken: a voice is audible, a briefing / cutscene voice speaks, or a voice ended less than GapSec ago.</summary>
        public static bool Busy(float now) => AnyAudible() || ExternalBusy() || now - lastAudible < GapSec;

        /// <summary>Real seconds since the last voice was audible (large when none was).</summary>
        public static float QuietFor(float now) => AnyAudible() ? 0f : now - lastAudible;

        /// <summary>Once a frame (AudioDirector.UpdateChannels): who is audible, and the line log (a new clip, a restart or the end).</summary>
        public static void Tick(float now)
        {
            if (frameTicked == Time.frameCount) return;
            frameTicked = Time.frameCount;
            for (int i = sources.Count - 1; i >= 0; i--)
            {
                var x = sources[i];
                if (!x.s) { Close(x, now); sources.RemoveAt(i); continue; }
                bool a = Audible(x.s);
                if (a)
                {
                    lastAudible = now;
                    bool fresh = !x.on || x.s.clip != x.clip || x.s.time + 0.05f < x.lastTime;      // a new clip, or the same clip started again
                    if (fresh) { Close(x, now); Open(x, now); }
                    x.lastTime = x.s.time;
                }
                else if (Held(x)) lastAudible = now;                                               // a line the listener pause holds goes on later: it keeps the floor
                else if (x.on) Close(x, now);
            }
        }

        static void Open(Src x, float now)
        {
            var c = x.s.clip;
            var r = new LineRec { who = x.who, id = x.id, clip = c ? c.name : null, start = now, end = -1f, len = c ? c.length / Mathf.Max(0.05f, Mathf.Abs(x.s.pitch)) : 0f, open = true, serial = ++LinesSeen };
            int at = logCount < LogMax ? logCount : logHead;
            log[at] = r;
            if (logCount < LogMax) logCount++; else logHead = (logHead + 1) % LogMax;
            x.rec = at; x.serial = r.serial; x.clip = c; x.on = true; x.lastTime = x.s.time;
        }

        static void Close(Src x, float now)
        {
            if (x.rec < 0) return;
            var r = log[x.rec];
            if (r.open && r.serial == x.serial) { r.end = now; r.open = false; log[x.rec] = r; }   // the ring may have moved on
            x.rec = -1; x.on = false;
        }

        /// <summary>The recorded lines that started at or after t0 (real time), oldest first; open ones end "now".</summary>
        public static List<LineRec> Since(float t0)
        {
            var o = new List<LineRec>();
            for (int i = 0; i < logCount; i++)
            {
                var r = log[(logHead + i) % logCount];
                if (r.start < t0) continue;
                if (r.open) r.end = Time.unscaledTime;
                o.Add(r);
            }
            return o;
        }

        /// <summary>One-line state for a Play check.</summary>
        public static string Status(float now)
        {
            int on = 0, held = 0; for (int i = 0; i < sources.Count; i++) { if (Audible(sources[i].s)) on++; if (Held(sources[i])) held++; }
            return $"voice sources {sources.Count} ({on} audible, {held} held by the pause), external {(ExternalBusy() ? "speaking" : "quiet")}, quiet for {QuietFor(now):0.00} s, lines seen {LinesSeen}";
        }
    }
}
