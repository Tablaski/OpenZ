#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace OpenZ
{
    /// <summary>
    /// 1.4 radar_voice_fix Play check (menu OpenZ/V1.4/Voices: No Overlap + Radar Lines Check (L02, Play), Editor/OzV14VoiceOverlapMenu.cs).
    /// On L02 (any level with two radar zones) in Play, after the briefing. Set-up as the 1.2 radar proof (View/OzRadarLineProof.cs): the
    /// CPU teams removed (their orders would take the zones), the day clock held, ANNOUNCER and ROBOT CHATTER at FULL for the run (put back
    /// after). The zones change owner through the game's own rule (ZWorld.AwardZone, as a robot touching the flag), radars are ruined
    /// through the game's own health rule (SetHealth 0 + UpdateObjectHealth), so FlagView, the HUD and the director play everything as in a
    /// match. Steps (each a few seconds after the last, 31 s where AlertGate's group gap would hold "Territory lost."):
    ///  S1 a zone with a standing radar taken: TERRITORY CAPTURED + RADAR ACTIVATED toasts, a radar-online line; unit lines spammed meanwhile
    ///  S2 the second radar zone taken (the first stands): the toast, the plain radar line; real mouse clicks (select, move) meanwhile
    ///  S3 the strike-group warning (the view's own rule, a march noted) while lines speak: said, not dropped, never over a voice
    ///  S4 a zone lost while another radar of ours stands: "Territory lost.", NO "Radar lost."
    ///  S5 our last radar zone lost while a cutscene voice speaks (a 3.5 s Zod clip: the pair waits about 3 s, more than Rachel's 4 s stale
    ///     rule leaves "Radar lost." when its age counts from the queueing): "Territory lost." then "Radar lost.", back to back (0.2-1.5 s
    ///     apart), after the voice; unit lines spammed meanwhile
    ///  S6 a radar zone taken back: "Radar is back online." + the toast
    ///  S5b our last radar zone lost again just after an ALERT started (the base under attack, rank 3: AlertGate's 7 s gap would drop
    ///     "Territory lost."): the alert, then "Territory lost." then "Radar lost.", back to back
    ///  S6b the radar zone taken back again: "Radar is back online." + the toast (S7 needs it ours)
    ///  S7 our radar destroyed: "Our radar has been destroyed!", NO "Radar lost."
    ///  S8 a zone with a RUINED radar taken: TERRITORY CAPTURED, NO RADAR ACTIVATED toast, no radar line
    ///  S9 a zone with a ruined radar lost: "Territory lost.", NO "Radar lost."
    ///  S10 a cutscene voice (a Commander Zod clip on a source registered as an outside voice): Rachel and the robots wait for it
    ///  S10b the game paused during that cutscene voice and resumed 0.9 s later: no "Game paused." (it could only have started after the
    ///     resume, under the voice going on), "Game resumed." after the voice; nothing over the held voice
    ///  S11 a unit reply asked near the end of a Rachel line: it waits, then starts 0.3 s after her (at least 0.25 s, within its 1 s wait)
    ///  S12 (fix rounds 3 + 4, final test pass) the V6 probes: in 14 set moments (the floor free; only Rachel speaking, her queue empty =
    ///     K407; Rachel speaking; her next line waiting while nothing is audible; Rachel speaking with two lines of hers waiting; a cutscene
    ///     voice with more than 1.5 s left, and with 0.5 s or less left; Rachel just started with one line of hers waiting = the hold band;
    ///     the gate and the banter while a cutscene voice speaks; a unit reply waiting in the robot slot behind Rachel; the robot line gap;
    ///     final test pass: a line of Rachel's queued in the landing's own frame = known row (a); the no-way gate's OWN flow, a player's
    ///     attack order at the start of a long Rachel line) a caller's test is answered and its line asked in the same frame, as that caller
    ///     does (V6.13: the order itself, OrderVoice -> NoWayRun, no direct Say); each set-up is tried up to 3 times and a set-up that never
    ///     comes fails V6
    /// Rows over the whole run: V1 zero overlaps (every source under its own name: two outside voices at once count too; V1a proves the
    /// counter on a made-up pair), V2 nothing cut, V3 no unknown voice source, V4 the floor's counters since the run's start (lines waited,
    /// started after waiting, Rachel waited), V5 every robot line that waited and started: 0.25 s or more after the line before it ended and
    /// no later than its wait after it was asked, V6a (final test pass) the numbers in V6's oracle, hard-coded: P's (DV118's 3.0 s hold and
    /// 0.3 s gap, the robot line gap 3.0 s, the no-way gate's 3.0 s / 0.25 s) and the agreed design's for the kit's own limits (a unit reply
    /// waits 1.0 s, the floor's gap 0.3 s), equal the live game's, V6 every probe of S12 judged on its line's
    /// real fate (read only from the director's own records: the line's AudioSource start / end, its held / waits / skipped / dropped rows)
    /// against P's fate MEASURED from the probe's own timings with P's numbers (P = the game before this kit, no floor: Rachel's lines back
    /// to back, DV118's hold, the robot line gap, a robot line over any other voice; no hand label). A line P said that the kit does not say
    /// is an ALLOWED SKIP only when one of the agreed rules (AllowedRules) explains it from the records; V6 prints their number by rule. V6
    /// fails only on: two voices audible at once (or a probe line under 0.25 s after another), an information line lost (a set-up line of
    /// Rachel's never said while the floor was free for her before her stale time), a paired line lost (S5 / S5b), a line said later than its
    /// rule allows (a gate's "go" for a line that did not start at once included; final test pass review: a set-up line of Rachel's that
    /// started more than 0.15 s + 2 frames after the floor was first free for it, i.e. held with the floor free, included), a skip no allowed
    /// rule explains (the landing test's "yes" for a line then dropped, a gate's "wait" for a line the floor took at once, a line lost without
    /// a reason included), a probe missed after 3 tries. (Was, rounds 3 + 4: P's fate was a hand label per probe, so V6.11's skip passed as
    /// "no requirement"; round 2: the tests were compared with the expression they are built from.)
    /// Measured, not claimed: every voice line's real start and end on its AudioSource (OzVoiceFloor's line log: the director's three
    /// channels, the registered outside voices, CombatAudio's own two) plus a scan of every other playing AudioSource for voice clips
    /// (fix round 4: every clip of a voice-channel event in the sound table, alan2_* / shuttle_v* / shuttle_r* / rachel-battle-start-*
    /// included, and the names zod_ / rachel / alan / brad / ez_ / evilzod_; the death cries, hit grunts and yelps are world sounds and stay
    /// out). Rows: zero overlaps, no
    /// line cut before its clip's end, every expected line played, no wrong toast or line. Writes
    /// oz_v14_voice_overlap.txt (PASS / FAIL rows + the line table). Test only: the level
    /// is spent afterwards (restart it).
    /// </summary>
    public sealed class OzVoiceOverlapProof : MonoBehaviour
    {
        public static string Report = "idle";
        public static string LogPath => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "oz_v14_voice_overlap.txt"));
        public static void Run()
        {
            if (FindFirstObjectByType<OzVoiceOverlapProof>()) { Debug.LogWarning("[OzVoiceOverlap] already running"); return; }
            Report = "running"; new GameObject("OzVoiceOverlapProof").AddComponent<OzVoiceOverlapProof>();
        }

        const float KindWait = 17f, TerrWait = 31f, LineWait = 14f, Overlap = 0.05f, CutSlack = 0.15f;
        ZGame g; ZWorld W; RtsCamera rts; PlayerInput input; Team me = Team.Red, foe = Team.Blue;
        readonly StringBuilder sb = new StringBuilder(); int passes, fails; float T0;
        int keepAnn = -1, keepRobot = -1, keepIdle = -1; bool restored;
        readonly HashSet<string> cries = new HashSet<string>();
        struct Stray { public AudioSource s; public string clip; public float a, b; public bool open; }
        readonly List<Stray> strays = new List<Stray>();
        readonly Dictionary<AudioSource, int> strayOpen = new Dictionary<AudioSource, int>();
        float nextScan; int spamAsked; int clicksAsked; bool clicksReached;
        ZObject squad; AudioSource zodSrc; AudioClip zodLong, zodIdle;
        int held0, released0, skipped0, rachelWaited0, guard0, paired0;   // the floor's counters at the run's start (V4 counts from there)
        const float WaitGapMin = 0.25f, WaitSlack = 0.1f;
        // V6 (fix round 3; final test pass: P's fate measured, the allowed skips counted). A probe: a caller's test answered now, then its line
        // asked in the same frame as that caller asks it; once its window is over (at the next probe's quiet start, while the sound log's
        // 2000-row ring still holds its rows) the line's fate is read only from the director's own records: its real start / end on an
        // AudioSource (OzVoiceFloor's line log) and the director's rows for it (OzSoundLog: held / waits / skipped / dropped with the reason).
        // P's fate (the game before this kit, no floor) is measured from the same records and the probe's own timings with P's numbers (PModel).
        const string LandLine = "vo_shuttle_landed", GateLine = AudioDirector.NoWayEvent;
        const float AtOnce = 0.15f;                                                                  // Its AudioSource plays within this (one frame or two)
        /// <summary>P's numbers, hard-coded (row V6a checks that the live game has the same): DV118's hold behind Rachel and its gap
        /// (OzShuttleVoiceGate.ShuttleHoldMaxSec, GapSec: this kit edits that file, so the oracle does not read them), the robot line gap
        /// (AudioDirector.AlanMainGap), the no-way gate's wait and breath (OzNoWayGate.Wait, After).</summary>
        const float PHoldSec = 3.0f, PGapSec = 0.3f, PRobotGapSec = 3.0f, PGateWait = 3.0f, PGateAfter = 0.25f;
        /// <summary>Final test pass review: the agreed design's numbers for the kit's own limits, hard-coded too (row V6a checks that the live
        /// game has the same; the oracle never reads them from the game): a unit reply waits at most 1.0 s for the floor
        /// (OzVoiceFloor.UnitReplyWait, a static the game could change), the floor's gap after a voice 0.3 s (OzVoiceFloor.GapSec). The kit's
        /// DV118 hold and the no-way gate's wait are P's (PHoldSec, PGateWait / PGateAfter). Rachel's queue drops a line of rank 4-9 RStaleSec
        /// after it was asked (ranks 1-3 never).</summary>
        const float DUnitWait = 1.0f, DGapSec = 0.3f, RStaleSec = 4f;
        /// <summary>
        /// say is ALLOWED only when one of these explains it from the probe's own records; anything else fails V6. README "Allowed skips" has
        /// an example of each.</summary>
        const string RWait = "a robot line waits at most its own wait for the floor, then is skipped rather than talk over another voice",
                     RInfo = "information first: a line of Rachel's waiting but not started goes first (known row a)",
                     RGap = "the floor's 0.3 s gap after a voice ran past the line's own wait (known row a, second form; the no-way gate's band)",
                     RSlot = "the robot slot: a robot line waiting there goes first, then the robot line gap (known row b)",
                     RPromo = "the promotion slot goes first",
                     RRobotGap = "the robot line gap after a robot line the floor started later than P did",
                     RLongest = "the landing test counts Rachel's waiting lines at their longest take",
                     RSlack = "the FloorSlack band: each limit met 3.5 frames early, the release on whole frames";
        static readonly string[] AllowedRules = { RWait, RInfo, RGap, RSlot, RPromo, RRobotGap, RLongest, RSlack };
        static readonly string[] AllowedShort = { "wait, not over a voice", "information first (row a)", "the 0.3 s gap", "the robot slot (row b)", "the promotion slot",
                                                  "the robot line gap", "the longest-take estimate", "the FloorSlack band" };
        enum PKind { Landing, Gate, GateFlow }
        /// <summary>A line of Rachel's a probe's set-up asked (Ann): its event, when, taken into her queue or not.</summary>
        struct RAsk { public string id; public float t; public bool queued; }
        sealed class Probe
        {
            public string what, test, id, reply; public PKind kind; public bool yes, twin, settled;
            public float ask, until, dt, slack, rachelLeft, replyAt = -1f; public int rachelNext;
            public readonly List<RAsk> rachel = new List<RAsk>();
            // the verdict (Settle): P said it, the kit says it (the landing: the test said yes and it was said), the faults, the allowed rule
            public bool pSaid, kitSays, over, late, badSkip, k407, agrees; public int infoLost, infoLate; public string rule;
        }
        readonly List<Probe> probes = new List<Probe>();
        /// <summary>The set-up of the probe being made: Rachel's lines it asked and the unit reply it asked (cleared by ProbeQuiet).</summary>
        readonly List<RAsk> setupRachel = new List<RAsk>(); string setupReply; float setupReplyAt = -1f;
        readonly StringBuilder v6Rows = new StringBuilder();
        bool v6Has;
        /// <summary>Fix round 4: the judgements S12 plans (a landing probe 1, a gate + banter probe 2, the gate's own flow 1) and the set-ups
        /// that never came (each fails V6: every planned probe must be judged).</summary>
        int v6Planned; readonly List<string> v6Missed = new List<string>();
        void Plan(int n) { v6Planned += n; }
        void Missed(string why) { v6Missed.Add(why); Log("PROBE MISSED " + why + " (3 tries; V6 fails: every planned probe must be judged)"); }
        /// <summary>Final test pass: S5 / S5b's radar pairs (V6 counts a pair with a half lost as a fault too).</summary>
        int pairsChecked, pairsLost;
        /// <summary>Fix round 4: the clip names of every voice-channel event in the sound table (the scan for unregistered voices).</summary>
        HashSet<string> voiceClips = new HashSet<string>();

        void Start() { StartCoroutine(All()); }

        /// <summary>V6 set-ups: one of Rachel's lines into her queue as the game asks it (AnnounceImpl, as Announce does), noted for the probe
        /// that follows (P's Rachel and the information-line count are measured from these); true = taken into her queue.</summary>
        bool Ann(string id)
        {
            bool q = AudioDirector.AnnounceQueued(id, null);
            setupRachel.Add(new RAsk { id = id, t = Now, queued = q });
            return q;
        }

        /// <summary>V6 set-ups: a unit reply asked as the game asks it (AudioDirector.Say), noted for the probe that follows.</summary>
        void Reply(string id) { AudioDirector.Say(id); setupReply = id; setupReplyAt = Now; }

        /// <summary>V6: a probe made now: its window, this frame's step and FloorSlack (as AudioDirector.FloorSlack), what Rachel does now,
        /// and its set-up's Rachel lines and unit reply.</summary>
        Probe NewProbe(string what, string test, string id, PKind kind, float window)
        {
            float dt = Mathf.Max(0.001f, Time.unscaledDeltaTime);
            var pr = new Probe { what = what, test = test, id = id, kind = kind, ask = Now, until = Now + window, dt = dt, slack = Mathf.Clamp(3.5f * dt, 0.05f, 0.25f),
                                 rachelLeft = OzVoiceFloor.LeftOn("rachel"), rachelNext = AudioDirector.RachelWaiting, reply = setupReply, replyAt = setupReplyAt };
            pr.rachel.AddRange(setupRachel);
            return pr;
        }

        /// <summary>V6: the landing chooser's robot test, then its ask in the same frame, as OzPlayableShuttle.PickLandingVoice and
        /// LandingOneVoice do it (AudioDirector.SayWouldPlay(robotId), then AudioDirector.Say(robotId)).</summary>
        void AskLanding(string what, float window)
        {
            var pr = NewProbe(what, "landing robot test (SayWouldPlay)", LandLine, PKind.Landing, window);
            pr.yes = AudioDirector.SayWouldPlay(LandLine);
            probes.Add(pr);
            AudioDirector.Say(LandLine);
        }

        /// <summary>V6: the no-way / lava gate's quiet test (NoWayQuiet) and the big crash banter's test (it waits while VoiceBusy or not
        /// RobotLineFree, OzPlayableShuttle.BigCrash.cs:160), then a robot line asked in the same frame: the gate's own event. The robot line
        /// gap is over before every probe (ProbeQuiet), so the line meets what the gate's line meets: the floor. One line, two judgements of
        /// the tests; the line's own fate is counted once (the banter's judgement is its twin).</summary>
        void AskGate(string what, float window)
        {
            bool gate = AudioDirector.QuietLineMayGoNow, banter = !AudioDirector.VoiceBusy && AudioDirector.RobotLineFree;
            var a = NewProbe(what, "no-way / lava gate (NoWayQuiet)", GateLine, PKind.Gate, window); a.yes = gate;
            var b = NewProbe(what, "big crash banter test (RobotLineFree)", GateLine, PKind.Gate, window); b.yes = banter; b.twin = true;
            probes.Add(a); probes.Add(b);
            AudioDirector.Say(GateLine);
        }

        /// <summary>V6.13 (final test pass): the no-way gate's OWN flow, as after a player's attack order: `unit` alone selected, its attack
        /// order at `target` sent (W.SendWaypoints, as a right-click sends it), then the order's voice exactly as PollInput gives it
        /// (OrderVoice, through P's own proof hook NoWayKillProofOrder, View/Voice/AudioDirector.NoWayKillProof.cs:24): NoWaySay, then NoWayRun
        /// polls NoWayQuiet once a frame up to OzNoWayGate.Wait (3 s) and says the line After (0.25 s) after the voices went quiet, through
        /// SayImpl. False = the order did not reach the gate (nothing asked; the set-up is tried again).</summary>
        bool AskGateFlow(string what, float window, ZObject unit, ZObject target)
        {
            var gate = AudioDirector.NoWayGate; var ad = AudioDirector.I;
            if (gate == null || !ad || input == null || unit == null || target == null || unit.Destroyed || target.Destroyed) return false;
            if (gate.Waiting || Time.unscaledTimeAsDouble - gate.Last < gate.Gap) return false;      // a line waits, or inside its 25 s gap: the order would get its normal answer
            input.Selection.Clear(); input.Selection.Add(unit);
            if (!W.SendWaypoints(unit.Owner, unit, new[] { new ZWaypoint { mode = WpMode.Attack, refId = target.Id, x = target.CenterX, y = target.CenterY, attackTo = true, playerGiven = true } })) return false;
            var pr = NewProbe(what, "the no-way gate's own flow (OrderVoice -> NoWayRun: NoWayQuiet up to 3 s, then SayImpl)", GateLine, PKind.GateFlow, window);
            int asked0 = gate.Asked;
            ad.NoWayKillProofOrder(true);
            if (gate.Asked == asked0) return false;
            pr.yes = true; probes.Add(pr);
            return true;
        }

        /// <summary>V6.13: a lone grunt of ours near our fort and an enemy heavy tank near theirs (both away from every flag), so the grunt's
        /// attack order is one Zod answers with a no-way line (AudioDirector.NoWayPair grunt / heavy) and no fight starts in the probe.</summary>
        void SpawnNoWayPair(out ZObject unit, out ZObject target)
        {
            ZObject ours = null, theirs = null;
            foreach (var o in W.Objects) if (o.IsFort && !o.Destroyed) { if (o.Owner == me) ours = ours ?? o; else if (o.Owner == foe) theirs = theirs ?? o; }
            unit = SpawnNear(ours, "robot", "grunt", me);
            target = SpawnNear(theirs, "vehicle", "heavy", foe);
        }

        ZObject SpawnNear(ZObject at, string cat, string type, Team team)
        {
            if (at == null) return null;
            bool robot = cat == "robot";
            for (int k = 0; k < 24; k++)
            {
                int px = at.CenterX + 120 * Mathf.RoundToInt(Mathf.Cos(k * 0.79f + 0.4f) * (1 + k / 8)), py = at.CenterY + 120 * Mathf.RoundToInt(Mathf.Sin(k * 0.79f + 0.4f) * (1 + k / 8));
                bool nearFlag = false; foreach (var f in W.Flags) if (Mathf.Abs(f.CenterX - px) + Mathf.Abs(f.CenterY - py) < 260) { nearFlag = true; break; }
                if (nearFlag || !OzCheatSpawner.FreeTile(W, px >> 4, py >> 4, robot ? 1 : 2, robot, out int tx, out int ty)) continue;
                var o = robot ? W.CreateRobotGroup(type, tx * 16, ty * 16, team, 1) : W.CreateObject(cat, type, tx * 16, ty * 16, team);
                if (o == null) continue;
                g.SpawnUnitView(o); foreach (var m in o.Minions) g.SpawnUnitView(m);
                return o;
            }
            return null;
        }

        /// <summary>V6.13 done: the grunt's attack order taken back (a move to where it stands) and the selection emptied.</summary>
        void StopUnit(ZObject u)
        {
            if (u != null && !u.Destroyed) W.SendWaypoints(u.Owner, u, new[] { new ZWaypoint { mode = WpMode.Move, x = u.CenterX, y = u.CenterY, playerGiven = true } });
            if (input != null) input.Selection.Clear();
        }

        /// <summary>V6, before a probe: nothing audible for 1 s and the robot line gap (AudioDirector.AlanMainGap after the last robot line)
        /// over, so only the floor (and DV118's hold behind Rachel) can hold the probe's line. Final test pass: the probes before it are
        /// judged now (their lines have ended; the sound log keeps only its last 2000 rows), then a new set-up starts.</summary>
        IEnumerator ProbeQuiet()
        {
            float end = Now + 30f;
            while (Now < end)
            {
                yield return Quiet(10f);
                float last = -100f; foreach (var r in OzVoiceFloor.Since(T0)) if (r.who == "alan_main" || r.who == "alan_alert") last = Mathf.Max(last, r.end);
                if (Now - last >= AudioDirector.AlanMainGap + 0.3f && !AudioDirector.VoiceBusy && OzVoiceFloor.QuietFor(Now) >= 1f) break;
                yield return Wait(0.25f);
            }
            SettleDue();
            setupRachel.Clear(); setupReply = null; setupReplyAt = -1f;
        }

        struct Fate { public bool said, atOnce, dvHeld, gateParked; public float start, end, waitAt, whyAt; public string who, why; }

        /// <summary>V6: what happened to the probe's line between its ask and the end of its window, from the director's own records only
        /// (never from a test): the first line of its event in OzVoiceFloor's line log (its real AudioSource start / end and source), and the
        /// director's rows for its event: held by DV118 behind Rachel, waits in the robot slot (when), let go by the no-way gate into a held
        /// floor ("held by the voice rules"), the first skipped / dropped / DROP row (why, when).</summary>
        Fate FateOf(Probe pr)
        {
            var f = new Fate { start = -1f, end = -1f, waitAt = -1f, whyAt = -1f };
            foreach (var r in OzVoiceFloor.Since(pr.ask)) if (r.id == pr.id && r.start <= pr.until) { f.said = true; f.start = r.start; f.end = r.end; f.who = r.who; break; }
            foreach (var e in OzSoundLog.Since(pr.ask))
            {
                if (e.key != pr.id || e.t > pr.until) continue;
                string o = e.outcome ?? "";
                if (o.StartsWith("held (Rachel")) f.dvHeld = true;
                else if (o.StartsWith("waits")) { if (f.waitAt < 0f) f.waitAt = e.t; }
                else if (o.StartsWith("no-way line held by the voice rules")) f.gateParked = true;
                else if (f.why == null && (o.StartsWith("skipped") || o.StartsWith("dropped") || o.StartsWith("DROP") || o.StartsWith("no-way line dropped"))) { f.why = o; f.whyAt = e.t; }
            }
            f.atOnce = f.said && !f.dvHeld && f.waitAt < 0f && f.start - pr.ask <= AtOnce;
            return f;
        }

        /// <summary>V6: voice lines audible together with the probe's said line for more than Overlap s (every recorded and unregistered line).</summary>
        static int OverlapsWith(Fate f, List<(string who, string id, string clip, float a, float b, float len)> all)
        {
            int n = 0;
            foreach (var x in all)
            {
                if (x.who == f.who && Mathf.Abs(x.a - f.start) < 0.001f) continue;                     // the line itself
                if (Mathf.Min(x.b, f.end) - Mathf.Max(x.a, f.start) > Overlap) n++;
            }
            return n;
        }

        void Log(string t) { sb.AppendLine(t); Debug.Log("[OzVoiceOverlap] " + t); }
        void Check(string what, bool ok) { if (ok) passes++; else fails++; Log((ok ? "PASS " : "FAIL ") + what); }
        float Now => Time.unscaledTime;
        IEnumerator Wait(float s) { float end = Now + s; while (Now < end) { Scan(); yield return null; } }

        /// <summary>Any voice line playing on a source the floor does not know: the scan keeps its real start and end.</summary>
        void Scan()
        {
            if (Now < nextScan) return;
            nextScan = Now + 0.05f;
            var seen = new HashSet<AudioSource>();
            foreach (var s in FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
            {
                if (!s || OzVoiceFloor.Known(s) || !OzVoiceFloor.Audible(s)) continue;
                string n = s.clip.name; string l = n.ToLowerInvariant();
                bool voice = voiceClips.Contains(n)                                                    // fix round 4: every voice-channel clip of the sound table (alan2_*, shuttle_v*, shuttle_r*, rachel-battle-start-* too)
                             || l.StartsWith("zod_") || l.StartsWith("rachel") || l.StartsWith("alan") || l.StartsWith("brad") || l.StartsWith("ez_") || l.StartsWith("evilzod_");
                if (!voice || cries.Contains(n) || l.StartsWith("alan_death_") || l.StartsWith("alan_hit_") || l.Contains("giggle")) continue;
                seen.Add(s);
                if (strayOpen.TryGetValue(s, out int k) && strays[k].clip == n) continue;
                strayOpen[s] = strays.Count; strays.Add(new Stray { s = s, clip = n, a = Now, b = -1f, open = true });
            }
            var done = new List<AudioSource>();
            foreach (var kv in strayOpen) if (!seen.Contains(kv.Key)) done.Add(kv.Key);
            foreach (var s in done) { int k = strayOpen[s]; var x = strays[k]; x.b = Now; x.open = false; strays[k] = x; strayOpen.Remove(s); }
        }

        bool Played(string key, float since, string clipHas = null)
        {
            foreach (var e in OzSoundLog.Since(since))
                if (e.src == "VO" && e.key == key && e.outcome != null && e.outcome.StartsWith("PLAY") && (clipHas == null || (e.clip != null && e.clip.Contains(clipHas)))) return true;
            return false;
        }
        string RadarLinePlayed(float since)
        {
            foreach (var e in OzSoundLog.Since(since))
                if (e.src == "VO" && e.outcome != null && e.outcome.StartsWith("PLAY") && (e.key == OzRadarLineRule.First || e.key == OzRadarLineRule.Back || e.key == OzRadarLineRule.More)) return e.key;
            return null;
        }
        bool Toasted(string text, float since)
        {
            foreach (var e in OzSoundLog.Since(since)) if (e.src == "hud" && e.key == "toast" && e.clip == text) return true;
            return false;
        }
        IEnumerator WaitFor(System.Func<bool> ok, float max) { float end = Now + max; while (Now < end && !ok()) { Scan(); yield return null; } }
        IEnumerator Quiet(float max) { float end = Now + max; while (Now < end && (AudioDirector.VoiceBusy || OzVoiceFloor.QuietFor(Now) < 1f)) { Scan(); yield return null; } }

        /// <summary>Unit lines asked as the game asks them (AudioDirector.Say, the entry PollInput, the kill quips and the call-outs use),
        /// every 0.2 s for `seconds`: selections, order answers, kill quips.</summary>
        IEnumerator Spam(float seconds)
        {
            string[] ids = { "vo_select_generic", "vo_order_ack", "vo_kill_quip", "vo_select_generic", "vo_order_ack" };
            float end = Now + seconds; int i = 0;
            while (Now < end) { AudioDirector.Say(ids[i++ % ids.Length]); spamAsked++; yield return Wait(0.2f); }
        }

        // ---- real mouse input (queued Input System state events, as View/SoundV3PlayProof.cs): the Game view must have the focus
        static Vector2 mousePos; static bool lDown, rDown;
        static void MouseState(Vector2 p, bool left, bool right)
        {
            mousePos = p; lDown = left; rDown = right;
            if (Mouse.current == null) return;
            var st = new MouseState { position = p }; if (left) st = st.WithButton(MouseButton.Left); if (right) st = st.WithButton(MouseButton.Right);
            InputSystem.QueueStateEvent(Mouse.current, st);
        }
        static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }
        IEnumerator Click(Vector2 p, bool right)
        {
            Vector2 from = mousePos;
            for (int k = 1; k <= 4; k++) { MouseState(Vector2.Lerp(from, p, k / 4f), lDown, rDown); yield return Frames(1); }
            MouseState(p, !right, right); yield return Frames(3);
            MouseState(p, false, false); yield return Frames(4);
            clicksAsked++;
        }
        IEnumerator Clicks(float seconds)
        {
            if (squad == null || squad.Destroyed || input == null || !Camera.main) yield break;
            float end = Now + seconds; int i = 0;
            while (Now < end)
            {
                var c = input.ScreenRect(squad).center;
                if ((i++ & 1) == 0) { yield return Click(c, false); if (input.Selection.Contains(squad) || input.Selection.Contains(squad.Leader ?? squad)) clicksReached = true; }
                else yield return Click(c + new Vector2(40f + 10f * (i % 3), 0f), true);
                yield return Wait(0.15f);
            }
        }

        ZObject FlagOf(ZObject b) { foreach (var f in W.Flags) foreach (var x in f.LinkedObjects) if (x == b) return f; return null; }
        void Cam(ZObject o) { if (!rts || o == null) return; rts.FreeLookOff(); var p = g.CenterWorld(o); rts.focus = new Vector3(p.x, 0f, p.z); rts.SetHeightImmediate(70f); }
        void Ruin(ZObject b) { if (b != null && !b.Destroyed) { b.SetHealth(0); W.UpdateObjectHealth(b); } }
        string Why => AudioDirector.RadarLostWhy;

        IEnumerator All()
        {
            T0 = Now;
            g = ZGame.I; W = g ? g.World : null; rts = FindFirstObjectByType<RtsCamera>(); input = FindFirstObjectByType<PlayerInput>();
            if (W == null || !AudioDirector.I || !FlagView.Active || !OzRadarLine.I || !OpenZHud.I) { Log("no level in Play with AudioDirector, FlagView, OzRadarLine and the HUD"); fails++; Finish(); yield break; }
            float w0 = Now;
            while ((ZodBriefing.BriefingActive || (AudioDirector.HoldStartLines != null && AudioDirector.HoldStartLines())) && Now - w0 < 90f) yield return null;
            me = g.player; foe = me == Team.Red ? Team.Blue : Team.Red;
            Log($"1.4 radar_voice_fix voice check, {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}, {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}, {Screen.width}x{Screen.height}, world time {W.Time:F0} s, player {me}");
            int bots = W.Bots.Count; W.Bots.Clear();
            var dn = FindFirstObjectByType<DayNight>(); if (dn) { dn.timeOfDay = 0.10f; dn.Apply(0.10f); dn.running = false; }
            keepAnn = AudioDirector.Chatter.Level; keepRobot = AudioDirector.RobotChatter.Level;
            if (keepAnn != 0) AudioDirector.Chatter.Level = 0;
            if (keepRobot != 0) AudioDirector.RobotChatter.Level = 0;
            keepIdle = PlayerPrefs.GetInt(ZodBriefing.PrefIdle, 1); PlayerPrefs.SetInt(ZodBriefing.PrefIdle, 0);   // the Commander's 60 s idle line would land in a step at random: S10 plays a cutscene voice on purpose
            foreach (var id in new[] { "vo_death_yelp", "hit_grunt", "death_cry_crush", "death_cry_laser", "death_cry_fire", "death_cry_blast", "death_cry_bullet" })
            { var d = AudioDirector.I.Def(id); if (d != null && d.loaded != null) foreach (var c in d.loaded) if (c) cries.Add(c.name); }
            voiceClips = AudioDirector.VoiceClipNames(); foreach (var c in cries) voiceClips.Remove(c);
            Log($"set-up: CPU removed {bots}, day held; ANNOUNCER {AudioDirector.Chatter.Names[keepAnn]} -> FULL, ROBOT CHATTER {AudioDirector.RobotChatter.Names[keepRobot]} -> FULL, the Commander's idle line (oz_zod_idle {keepIdle}) off for the run, all put back after; cry clips left out {cries.Count}; {OzVoiceFloor.Status(Now)}; {OzRadarLine.Status()}");
            // a cutscene voice of the run's own (S5, S10, S10b): a Commander Zod clip on a source registered as an outside voice, as ZodBriefing's
            zodLong = Resources.Load<AudioClip>("OpenZ/Zod/Voice/zod_select_L02"); zodIdle = Resources.Load<AudioClip>("OpenZ/Zod/Voice/zod_idle_01");
            if (!zodLong) zodLong = zodIdle;
            zodSrc = gameObject.AddComponent<AudioSource>(); zodSrc.playOnAwake = false; zodSrc.spatialBlend = 0f; zodSrc.ignoreListenerPause = false;
            zodSrc.volume = Mathf.Clamp01(AudioDirector.I.VoiceBusLinear("Voice/Commander"));
            AudioDirector.RegisterExternalVoice(zodSrc);
            held0 = AudioDirector.FloorHeld; released0 = AudioDirector.FloorReleased; skipped0 = AudioDirector.FloorSkipped;
            rachelWaited0 = AudioDirector.FloorRachelWaited; guard0 = AudioDirector.FloorGuardBlocked; paired0 = AlertGate.Paired;
            v6Has = AudioDirector.Has(LandLine) && AudioDirector.Has(GateLine);                    // V6: the landing's robot line and the gate's line are in the sound table
            Log($"the floor's counters at the start (V4 counts from here): waited {held0}, started after waiting {released0}, skipped {skipped0}, Rachel waited {rachelWaited0}, refused {guard0}, AlertGate pairs {paired0}; cutscene clips {(zodLong ? zodLong.name + " " + zodLong.length.ToString("F2") + " s" : "none")} / {(zodIdle ? zodIdle.name : "none")}");

            var radars = new List<ZObject>();
            foreach (var o in W.Objects) if (o.Kind == ZKind.Building && o.Type == "radar" && !o.Destroyed && FlagOf(o) != null) radars.Add(o);
            if (radars.Count < 2) { Log($"this level has {radars.Count} standing radar(s) with a flag; the check needs 2 (L02 has them at the level start)"); fails++; Finish(); yield break; }
            ZObject r1 = radars[0], r2 = radars[1], f1 = FlagOf(r1), f2 = FlagOf(r2);
            Log($"radar 1 #{r1.Id} ({r1.Owner}, flag #{f1.Id}), radar 2 #{r2.Id} ({r2.Owner}, flag #{f2.Id})");
            if (f1.Owner == me || f2.Owner == me)
            {
                if (f1.Owner == me) W.AwardZone(f1, foe);
                if (f2.Owner == me) W.AwardZone(f2, foe);
                Log("INFO a radar zone was ours at the start: given to the enemy, then 32 s for the alert groups");
                yield return Wait(32f);
            }
            // our squad for the real clicks, away from every flag (it must not take a zone)
            var fort = (ZObject)null; foreach (var o in W.Objects) if (o.IsFort && o.Owner == me && !o.Destroyed) { fort = o; break; }
            if (fort != null)
                for (int k = 0; k < 24 && squad == null; k++)
                {
                    int px = fort.CenterX + 120 * Mathf.RoundToInt(Mathf.Cos(k * 0.79f) * (1 + k / 8)), py = fort.CenterY + 120 * Mathf.RoundToInt(Mathf.Sin(k * 0.79f) * (1 + k / 8));
                    bool nearFlag = false; foreach (var f in W.Flags) if (Mathf.Abs(f.CenterX - px) + Mathf.Abs(f.CenterY - py) < 260) { nearFlag = true; break; }
                    if (nearFlag || !OzCheatSpawner.FreeTile(W, px >> 4, py >> 4, 1, true, out int tx, out int ty)) continue;
                    squad = W.CreateRobotGroup("grunt", tx * 16, ty * 16, me);
                    if (squad != null) { g.SpawnUnitView(squad); foreach (var m in squad.Minions) g.SpawnUnitView(m); }
                }
            Log(squad != null ? $"our grunt squad #{squad.Id} for the real clicks at ({squad.CenterX}, {squad.CenterY})" : "INFO no free place for the click squad: the real-click part is skipped");
            yield return Quiet(10f);
            float t;

            // ---- S1: a zone with a standing radar taken, unit lines spammed
            Cam(r1); yield return Wait(0.5f); t = Now;
            W.AwardZone(f1, me); StartCoroutine(Spam(3f));
            yield return WaitFor(() => RadarLinePlayed(t) != null, LineWait);
            Check($"S1 standing radar taken: TERRITORY CAPTURED + RADAR ACTIVATED toasts, a radar-online line (toasts {Toasted("TERRITORY CAPTURED", t)} / {Toasted("RADAR ACTIVATED", t)}, line {RadarLinePlayed(t) ?? "none"})",
                  Toasted("TERRITORY CAPTURED", t) && Toasted("RADAR ACTIVATED", t) && RadarLinePlayed(t) != null);
            yield return Wait(KindWait);

            // ---- S2: the second radar zone taken while the first stands; real clicks meanwhile
            Cam(squad ?? r2); yield return Wait(0.5f); t = Now;
            W.AwardZone(f2, me); StartCoroutine(Clicks(4f));
            yield return WaitFor(() => RadarLinePlayed(t) != null, LineWait);
            Check($"S2 second standing radar taken: the RADAR ACTIVATED toast and the plain radar line (line {RadarLinePlayed(t) ?? "none"})", Toasted("RADAR ACTIVATED", t) && RadarLinePlayed(t) == OzRadarLineRule.More);
            if (squad != null) Check($"S2 the real clicks reached the game (select + move, {clicksAsked} clicks; the Game view must have the focus)", clicksReached);
            yield return Wait(5f);

            // ---- S3: the strike-group warning while lines speak
            t = Now;
            var sw = OzStrikeWarning.I;
            if (sw != null && sw.Rule != null)
            {
                int said0 = sw.Rule.Said, late0 = sw.Rule.TooLate, gap0 = sw.Rule.InGap;
                AudioDirector.Announce("ann_grenades_collected"); StartCoroutine(Spam(2f));
                sw.Rule.Note(ZBot.StrikeEventKind.Formed, W.Time, Time.unscaledTime, true);
                yield return WaitFor(() => Played(OzStrikeWarning.EventId, t), 10f);
                Check($"S3 the strike warning, asked while lines speak, is said (not dropped: said +{sw.Rule.Said - said0}, too late +{sw.Rule.TooLate - late0}, inside its 5-minute gap +{sw.Rule.InGap - gap0})", Played(OzStrikeWarning.EventId, t));
            }
            else Check("S3 the strike warning view is on the level (OzStrikeWarning)", false);
            yield return Wait(12f);

            // ---- S4: a zone lost while another radar of ours stands
            t = Now; W.AwardZone(f2, foe);
            yield return WaitFor(() => Played("ann_territory_lost", t), 8f); yield return Wait(4f);
            Check($"S4 zone lost while our other radar stands: \"Territory lost.\", no \"Radar lost.\" (territory {Played("ann_territory_lost", t)}, radar lost {Played("ann_radar_lost", t)}; {Why})",
                  Played("ann_territory_lost", t) && !Played("ann_radar_lost", t));
            yield return Wait(TerrWait);

            // ---- S5: our last radar zone lost while a cutscene voice speaks, unit lines spammed: both lines, back to back, after the voice
            yield return Quiet(10f);
            if (zodLong) { zodSrc.clip = zodLong; zodSrc.Play(); }
            yield return Frames(1);
            t = Now; W.AwardZone(f1, foe); StartCoroutine(Spam(4f));
            yield return WaitFor(() => Played("ann_radar_lost", t), 14f); yield return Wait(3f);
            var z5 = LineOf(zodSrc, t - 0.5f);
            PairRow("S5 our last radar zone lost while a cutscene voice speaks", t, z5, zodLong ? "the cutscene voice" : null);
            yield return Wait(KindWait);

            // ---- S6: a radar zone taken back
            Cam(r1); t = Now; W.AwardZone(f1, me);
            yield return WaitFor(() => RadarLinePlayed(t) != null, LineWait);
            Check($"S6 a radar zone taken back: \"Radar is back online.\" + the toast (line {RadarLinePlayed(t) ?? "none"})", RadarLinePlayed(t) == OzRadarLineRule.Back && Toasted("RADAR ACTIVATED", t));
            yield return Wait(KindWait);

            // ---- S5b: our last radar zone lost again just after an alert started (AlertGate's 7 s gap would hold "Territory lost.")
            yield return Quiet(10f);
            t = Now; AudioDirector.Announce("ann_base_under_attack");
            yield return WaitFor(() => Played("ann_base_under_attack", t), 3f);
            bool alertSaid = Played("ann_base_under_attack", t);
            W.AwardZone(f1, foe); StartCoroutine(Spam(3f));
            yield return WaitFor(() => Played("ann_radar_lost", t), 14f); yield return Wait(3f);
            if (alertSaid) PairRow("S5b our last radar zone lost just after an alert (the base under attack) started", t, Line("ann_base_under_attack", t), "the alert");
            else Check("S5b the alert line (ann_base_under_attack) was said before the zone was lost (its cooldown or group held it: the step cannot run)", false);
            yield return Wait(KindWait);

            // ---- S6b: the radar zone taken back again (S7 needs our radar standing in our zone)
            Cam(r1); t = Now; W.AwardZone(f1, me);
            yield return WaitFor(() => RadarLinePlayed(t) != null, LineWait);
            Check($"S6b the radar zone taken back again: \"Radar is back online.\" + the toast (line {RadarLinePlayed(t) ?? "none"})", RadarLinePlayed(t) == OzRadarLineRule.Back && Toasted("RADAR ACTIVATED", t));
            yield return Wait(KindWait);

            // ---- S7: our radar destroyed
            t = Now; Ruin(r1);
            yield return WaitFor(() => Played("ann_building_destroyed", t), 8f); yield return Wait(3f);
            if (OzSoundV3.Enabled) Check($"S7 our radar destroyed: \"Our radar has been destroyed!\" (sound_v3), no \"Radar lost.\" (destroyed {Played("ann_building_destroyed", t, "radar")}, radar lost {Played("ann_radar_lost", t)})",
                                         Played("ann_building_destroyed", t, "radar") && !Played("ann_radar_lost", t));
            else Check($"S7 our radar destroyed: no \"Radar lost.\" (sound_v3 off: its destroyed line is not in this game)", !Played("ann_radar_lost", t));
            yield return Wait(12f);

            // ---- S8: a zone with a ruined radar taken
            Ruin(r2); yield return Wait(2f);
            Cam(r2); t = Now; W.AwardZone(f2, me);
            yield return Wait(LineWait);
            Check($"S8 ruined radar taken: TERRITORY CAPTURED, no RADAR ACTIVATED toast, no radar line (toasts {Toasted("TERRITORY CAPTURED", t)} / {Toasted("RADAR ACTIVATED", t)}, line {RadarLinePlayed(t) ?? "none"}, rule {OzRadarLine.Last})",
                  r2.Destroyed && Toasted("TERRITORY CAPTURED", t) && !Toasted("RADAR ACTIVATED", t) && RadarLinePlayed(t) == null);
            yield return Wait(KindWait);

            // ---- S9: a zone with a ruined radar lost
            t = Now; W.AwardZone(f2, foe);
            yield return WaitFor(() => Played("ann_territory_lost", t), 8f); yield return Wait(4f);
            Check($"S9 ruined radar zone lost: \"Territory lost.\", no \"Radar lost.\" (territory {Played("ann_territory_lost", t)}, radar lost {Played("ann_radar_lost", t)}; {Why})",
                  Played("ann_territory_lost", t) && !Played("ann_radar_lost", t));
            yield return Quiet(10f);

            // ---- S10: a cutscene voice on a registered outside source: the in-match lines wait for it
            var clip = zodIdle;
            if (clip)
            {
                t = Now; zodSrc.clip = clip; zodSrc.Play();
                yield return Wait(0.3f);
                AudioDirector.Announce("ann_grenades_collected"); StartCoroutine(Spam(2f));
                yield return WaitFor(() => Played("ann_grenades_collected", t), clip.length + 6f); yield return Wait(2f);
                var z = LineOf(zodSrc, t); var gl = Line("ann_grenades_collected", t);
                Check($"S10 a cutscene voice speaks: Rachel waits and starts after it ({(z.HasValue && gl.HasValue ? (gl.Value.start - z.Value.end).ToString("F2") : "?")} s after its end), no line over it",
                      z.HasValue && gl.HasValue && gl.Value.start >= z.Value.end + 0.2f);

                // ---- S10b: the game paused during the cutscene voice, resumed 0.9 s later (the held voice keeps the floor)
                yield return Quiet(10f);
                t = Now; zodSrc.clip = clip; zodSrc.Play();
                yield return Wait(0.8f);
                g.paused = true; float tp = Now;
                yield return Wait(0.9f);                                       // was: "Game paused." 0.3 s after the pause, into the held voice
                g.paused = false; float tr = Now;
                yield return WaitFor(() => Played("ann_game_resumed", tr), clip.length + 6f); yield return Wait(1.5f);
                var zp = LineOf(zodSrc, t); var gr = Line("ann_game_resumed", tr);
                bool asked = Asked("ann_game_paused", tp);
                if (asked)
                    Check($"S10b paused during a cutscene voice and resumed: no \"Game paused.\" (it could only start after the resume: dropped {Dropped("ann_game_paused", tp)}), \"Game resumed.\" after the voice ({(zp.HasValue && gr.HasValue ? (gr.Value.start - zp.Value.end).ToString("F2") : "?")} s after its end)",
                          !Played("ann_game_paused", tp) && zp.HasValue && gr.HasValue && gr.Value.start >= zp.Value.end + 0.2f);
                else Check("S10b INFO the director did not ask \"Game paused.\" (its pause poll is off: uiPolling); nothing played over the held voice", !Played("ann_game_paused", tp) && !Played("ann_game_resumed", tp));
            }
            else Check("S10 the Commander Zod clip OpenZ/Zod/Voice/zod_idle_01 is in Resources", false);
            yield return Wait(1f);

            // ---- S11: a unit reply asked near the end of a Rachel line waits and starts 0.3 s after her
            yield return Quiet(10f); yield return Wait(3.3f); yield return Quiet(10f);   // the robot line gap (AudioDirector.AlanMainGap 3 s) is over
            int h11 = AudioDirector.FloorHeld, r11 = AudioDirector.FloorReleased;
            t = Now; AudioDirector.Announce("ann_grenades_collected");
            yield return WaitFor(() => OzVoiceFloor.LeftOn("rachel") > 0f, 3f);
            yield return WaitFor(() => { float l = OzVoiceFloor.LeftOn("rachel"); return l >= 0f && l <= 0.45f; }, 3f);
            float ask = Now; AudioDirector.Say("vo_select_generic");
            yield return Wait(2.5f);
            var rg = Line("ann_grenades_collected", t); var sel = Line("vo_select_generic", ask);
            float sGap = rg.HasValue && sel.HasValue ? sel.Value.start - rg.Value.end : -9f, sWait = sel.HasValue ? sel.Value.start - ask : -9f;
            Check($"S11 a unit reply asked {(rg.HasValue ? rg.Value.end - ask : 0f):F2} s before Rachel's end waits and starts after her (waited +{AudioDirector.FloorHeld - h11}, started after waiting +{AudioDirector.FloorReleased - r11}; {sGap:F2} s after her end, {sWait:F2} s after it was asked; wanted >= {WaitGapMin:F2} and <= {OzVoiceFloor.UnitReplyWait + WaitSlack:F2})",
                  rg.HasValue && sel.HasValue && AudioDirector.FloorHeld > h11 && AudioDirector.FloorReleased > r11 && sGap >= WaitGapMin && sWait <= OzVoiceFloor.UnitReplyWait + WaitSlack);
            yield return Wait(1f);

            // ---- S12 (V6, fix rounds 3 + 4, final test pass): a caller's test answered and its line asked in the same frame, in 14 set
            // moments; each probe is judged once its window is over (Settle, at the next ProbeQuiet), with P's fate MEASURED from the probe's
            // own timings (no hand label). Every set-up is tried up to 3 times; one that never comes is a MISSED probe and fails V6
            if (v6Has)
            {
                Log("S12 the V6 probes (judged in V6 below)");
                bool ran;
                yield return ProbeQuiet(); Plan(1); AskLanding("V6.1 the floor free", 3f); yield return Wait(3f);
                yield return ProbeQuiet(); Plan(2); AskGate("V6.2 the floor free", 2.5f); yield return Wait(2.5f);
                // K407, the fault of fix round 2: only Rachel speaks, her queue empty (P held the line behind her and said it 0.3 s after her)
                Plan(1); ran = false;
                for (int k = 0; k < 3 && !ran; k++)
                {
                    yield return ProbeQuiet(); Ann(k == 1 ? "ann_bridge_repaired" : "ann_grenades_collected");
                    yield return WaitFor(() => { float l = OzVoiceFloor.LeftOn("rachel"); return l > 0.2f && l <= 1.5f && AudioDirector.RachelWaiting == 0; }, 4f);
                    if (OzVoiceFloor.LeftOn("rachel") > 0.2f && AudioDirector.RachelWaiting == 0) { AskLanding("V6.3 K407: only Rachel speaks, her queue empty", 4.5f); ran = true; }
                    yield return Wait(4.5f);
                }
                if (!ran) Missed("V6.3 K407: Rachel's line did not start alone");
                // Rachel speaks: the gate's and the banter's tests (P said a line asked then over her)
                Plan(2); ran = false;
                for (int k = 0; k < 3 && !ran; k++)
                {
                    yield return ProbeQuiet(); Ann(k == 1 ? "ann_grenades_collected" : "ann_bridge_repaired");
                    yield return WaitFor(() => OzVoiceFloor.LeftOn("rachel") > 0.2f, 3f);
                    if (OzVoiceFloor.LeftOn("rachel") > 0.2f) { AskGate("V6.4 Rachel speaks", 3f); ran = true; }
                    yield return Wait(3f);
                }
                if (!ran) Missed("V6.4 Rachel's line did not start");
                // the fix-round-2 moment: Rachel's next line waits (her 0.3 s gap) while nothing is audible
                Plan(2); ran = false;
                for (int k = 0; k < 3 && !ran; k++)
                {
                    yield return ProbeQuiet(); Ann("ann_grenades_collected"); Ann("ann_production_resumed");
                    yield return WaitFor(() => OzVoiceFloor.LeftOn("rachel") > 0f, 3f);
                    yield return WaitFor(() => OzVoiceFloor.LeftOn("rachel") < 0f && AudioDirector.RachelWaiting > 0, 4f);
                    if (OzVoiceFloor.LeftOn("rachel") < 0f && AudioDirector.RachelWaiting > 0) { AskGate("V6.5 Rachel's next line waits, nothing audible", 3.5f); ran = true; }
                    yield return Wait(3.5f);
                }
                if (!ran) Missed("V6.5 the moment between two of Rachel's lines was not seen");
                // Rachel speaks and two lines of hers wait: DV118's hold (3 s + 0.3 s per waiting line) runs out before she is done
                Plan(1); ran = false;
                for (int k = 0; k < 3 && !ran; k++)
                {
                    yield return ProbeQuiet();
                    Ann("ann_grenades_collected"); Ann("ann_production_resumed"); Ann("ann_starting_repair");
                    yield return WaitFor(() => OzVoiceFloor.LeftOn("rachel") > 0.6f && AudioDirector.RachelWaiting == 2, 3f);
                    if (OzVoiceFloor.LeftOn("rachel") > 0.6f && AudioDirector.RachelWaiting == 2) { AskLanding("V6.6 Rachel speaks, two lines of hers wait", 6f); ran = true; }
                    yield return Wait(6f);
                }
                if (!ran) Missed("V6.6 Rachel did not speak with two lines waiting");
                // fix round 4 (the hold band, its first fault): Rachel has just started a line and ONE line of hers waits; her time left + that
                // line come to about 2.5 s: P (her lines back to back) said the landing line 0.3 s after her within 3 s; the kit puts 0.3 s
                // before her waiting line, so its hold needs that 0.3 s added (ExtendHold) or it drops the line ("held too long behind Rachel")
                Plan(1); ran = false;
                for (int k = 0; k < 3 && !ran; k++)
                {
                    if (k > 0) yield return Wait(KindWait);                                              // a retry: the factory kind's 15 s gap and the line's 8 s cooldown
                    yield return ProbeQuiet(); Ann("ann_repair_captured"); Ann("ann_unit_promoted");
                    yield return WaitFor(() => OzVoiceFloor.LeftOn("rachel") > 0f && AudioDirector.RachelWaiting == 1, 3f);
                    float l9 = OzVoiceFloor.LeftOn("rachel");
                    if (l9 > 1.3f && AudioDirector.RachelWaiting == 1) { AskLanding($"V6.9 Rachel has just started a line ({l9:F2} s left), one line of hers waits (the hold band)", 5f); ran = true; }
                    yield return Wait(5f);
                }
                if (!ran) Missed("V6.9 Rachel's line was not seen as it started with one line waiting");
                // fix round 4 (the robot slot, known row b): Rachel speaks, a unit reply waits in the pending slot behind her (it starts 0.3 s
                // after her, TickPendingAlan runs first), then the landing line is asked: DV118 lets it go into the reply on Alan's main channel
                // (robot line gap): the landing test must say no. P said the reply over her; the landing line then met it or not, by the reply's
                // length (measured: P said it when the reply ended before her end + 0.3 s)
                Plan(1); ran = false;
                for (int k = 0; k < 3 && !ran; k++)
                {
                    yield return ProbeQuiet(); Ann(k == 1 ? "ann_bridge_repaired" : "ann_grenades_collected");
                    yield return WaitFor(() => OzVoiceFloor.LeftOn("rachel") > 0f, 3f);
                    yield return WaitFor(() => { float l = OzVoiceFloor.LeftOn("rachel"); return l > 0.1f && l <= 0.5f; }, 3f);
                    float l11 = OzVoiceFloor.LeftOn("rachel");
                    if (l11 > 0.1f && l11 <= 0.5f && AudioDirector.RachelWaiting == 0)
                    {
                        Reply("vo_select_generic");
                        if (AudioDirector.RobotLineWaiting == "vo_select_generic")
                        { AskLanding("V6.11 Rachel speaks, a unit reply waits in the robot slot behind her", 3f); ran = true; }
                    }
                    yield return Wait(3f);
                }
                if (!ran) Missed("V6.11 the unit reply did not wait behind Rachel");
                // fix round 4 (the robot line gap, P's rule the landing test did not ask): a unit reply said less than 3 s ago, the floor free
                Plan(1); ran = false;
                for (int k = 0; k < 3 && !ran; k++)
                {
                    yield return ProbeQuiet(); Reply("vo_select_generic");
                    yield return WaitFor(() => OzVoiceFloor.LeftOn("alan_main") > 0f, 1f);
                    yield return WaitFor(() => OzVoiceFloor.LeftOn("alan_main") < 0f, 3f); yield return Wait(0.6f);
                    if (!AudioDirector.RobotLineFree && !AudioDirector.VoiceBusy && OzVoiceFloor.QuietFor(Now) >= 0.5f)
                    { AskLanding("V6.12 the floor free, a unit reply ended 0.6 s ago (the robot line gap)", 2.5f); ran = true; }
                    yield return Wait(2.5f);
                }
                if (!ran) Missed("V6.12 no unit reply was said before the probe");
                // final test pass (known row a): a line of Rachel's queued in the landing's own frame, nothing audible: the floor puts her first
                // (information first), the landing test says no; P said the landing line at once and held her (DV118 RachelWaitsForShuttleLine)
                Plan(1); ran = false;
                for (int k = 0; k < 3 && !ran; k++)
                {
                    yield return ProbeQuiet();
                    if (Ann(k == 1 ? "ann_bridge_repaired" : "ann_grenades_collected") && OzVoiceFloor.LeftOn("rachel") < 0f && AudioDirector.RachelWaiting == 1)
                    { AskLanding("V6.14 known row (a): a line of Rachel's queued in the landing's own frame, nothing audible", 3f); ran = true; }
                    yield return Wait(3f);
                }
                if (!ran) Missed("V6.14 Rachel's line was not taken into her queue alone");
                // a cutscene voice (the run's own outside source), Rachel silent: more than the line's 1 s wait left, then 0.5 s or less left;
                // fix round 4: and the gate's and the banter's tests while it speaks (V6.10: their floor term on an outside voice)
                string zn = OzVoiceFloor.NameOf(zodSrc);
                Plan(1); Plan(2); Plan(1); bool r7 = false, r10 = false, r8 = false;
                if (zodLong && zn != null && zodLong.length > 2f)
                {
                    for (int k = 0; k < 3 && !r7; k++)
                    {
                        yield return ProbeQuiet(); zodSrc.clip = zodLong; zodSrc.Play();
                        yield return WaitFor(() => OzVoiceFloor.LeftOn(zn) > 1.5f, 2f);
                        if (OzVoiceFloor.LeftOn(zn) > 1.5f) { AskLanding("V6.7 a cutscene voice speaks (more than 1.5 s left), Rachel silent", zodLong.length + 1f); r7 = true; }
                        yield return WaitFor(() => !zodSrc.isPlaying, zodLong.length + 1f); yield return Wait(1f);
                    }
                    for (int k = 0; k < 3 && !r10; k++)
                    {
                        yield return ProbeQuiet(); zodSrc.clip = zodLong; zodSrc.Play();
                        yield return WaitFor(() => OzVoiceFloor.LeftOn(zn) > 1.5f, 2f);
                        if (OzVoiceFloor.LeftOn(zn) > 1.5f && !AudioDirector.VoiceBusy) { AskGate("V6.10 a cutscene voice speaks (more than 1.5 s left), Rachel and Alan silent", zodLong.length + 1f); r10 = true; }
                        yield return WaitFor(() => !zodSrc.isPlaying, zodLong.length + 1f); yield return Wait(1f);
                    }
                    for (int k = 0; k < 3 && !r8; k++)
                    {
                        yield return ProbeQuiet(); zodSrc.clip = zodLong; zodSrc.Play();
                        yield return WaitFor(() => { float l = OzVoiceFloor.LeftOn(zn); return l > 0f && l <= 0.5f; }, zodLong.length + 1f);
                        float zl = OzVoiceFloor.LeftOn(zn);
                        if (zl > 0f && zl <= 0.5f) { AskLanding("V6.8 a cutscene voice about to end (0.5 s or less left), Rachel silent", 2.5f); r8 = true; }
                        yield return WaitFor(() => !zodSrc.isPlaying, 1f); yield return Wait(2.5f);
                    }
                }
                if (!r7) Missed("V6.7 the cutscene voice (" + (zodLong ? zodLong.name : "no clip") + ") was not heard with more than 1.5 s left");
                if (!r10) Missed("V6.10 the cutscene voice was not heard with more than 1.5 s left while the director's voices were silent");
                if (!r8) Missed("V6.8 the cutscene voice was not heard in its last 0.5 s");
                // final test pass (V6.13): the no-way gate's OWN flow (OrderVoice -> NoWaySay -> NoWayRun: NoWayQuiet polled once a frame up to
                // OzNoWayGate.Wait 3 s, the line 0.25 s after the voices went quiet, then SayImpl), not a direct Say: a grunt of ours sent alone
                // at an enemy heavy tank at the start of Rachel's "Repair station captured." (1.48-1.49 s; 1.3 s or more left, her queue empty).
                // P said the line 0.25 s after her (about +1.7 s), the kit 0.3 s + 0.25 s after her (about +2.0 s), inside the gate's 3 s: it
                // must be said (a gate that waits past a free floor drops it, and no allowed rule explains that). Last: its two units stay.
                Plan(1); ran = false;
                ZObject nwUnit, nwTarget; SpawnNoWayPair(out nwUnit, out nwTarget);
                Log($"V6.13 set-up: our grunt {(nwUnit != null ? "#" + nwUnit.Id : "NONE")}, the enemy heavy tank {(nwTarget != null ? "#" + nwTarget.Id : "NONE")} (a no-way pair: {AudioDirector.NoWayPair("grunt", "heavy")})");
                for (int k = 0; k < 3 && !ran && nwUnit != null && nwTarget != null; k++)
                {
                    if (k > 0) yield return Wait(KindWait);                                              // a retry: the factory kind's 15 s gap
                    yield return ProbeQuiet();
                    if (!Ann("ann_repair_captured")) continue;
                    yield return WaitFor(() => OzVoiceFloor.LeftOn("rachel") > 0f, 3f);
                    float l13 = OzVoiceFloor.LeftOn("rachel");
                    if (l13 >= 1.3f && AudioDirector.RachelWaiting == 0 && AskGateFlow($"V6.13 the no-way gate's own flow: an order {l13:F2} s before the end of Rachel's line, her queue empty", 4.5f, nwUnit, nwTarget)) ran = true;
                    yield return Wait(4.5f);
                }
                StopUnit(nwUnit);
                if (!ran) Missed("V6.13 the order did not reach the no-way gate at the start of Rachel's line" + (nwUnit == null || nwTarget == null ? " (the grunt or the heavy tank could not be placed)" : ""));
                yield return Quiet(10f); yield return Wait(0.5f);                                          // the last lines end before they are judged
            }

            Verdicts();
            Finish();
        }

        /// <summary>The first recorded line of event `id` since t0 on the director's channels.</summary>
        OzVoiceFloor.LineRec? Line(string id, float t0) { foreach (var r in OzVoiceFloor.Since(t0)) if (r.id == id) return r; return null; }
        /// <summary>The first recorded line since t0 on source `s` (its own name in the floor: fix round 2) with the clip it plays now.</summary>
        OzVoiceFloor.LineRec? LineOf(AudioSource s, float t0)
        {
            string name = OzVoiceFloor.NameOf(s);
            if (name == null || !s || !s.clip) return null;
            foreach (var r in OzVoiceFloor.Since(t0)) if (r.who == name && r.clip == s.clip.name) return r;
            return null;
        }

        /// <summary>The director logged anything for event `id` since t0 (asked, waited, dropped, played).</summary>
        bool Asked(string id, float t0) { foreach (var e in OzSoundLog.Since(t0)) if (e.key == id) return true; return false; }
        /// <summary>The director dropped a waiting pause line of event `id` since t0 (the game's state changed before it could start).</summary>
        bool Dropped(string id, float t0) { foreach (var e in OzSoundLog.Since(t0)) if (e.src == "dir" && e.key == id && e.outcome != null && e.outcome.StartsWith("dropped (the game was")) return true; return false; }

        /// <summary>S5 / S5b: "Territory lost." then "Radar lost." since t, back to back (0.2-1.5 s), both after `before` (a voice that spoke
        /// when the zone was lost) ended.</summary>
        void PairRow(string what, float t, OzVoiceFloor.LineRec? before, string beforeName)
        {
            var tl = Line("ann_territory_lost", t); var rl = Line("ann_radar_lost", t);
            float gap = tl.HasValue && rl.HasValue ? rl.Value.start - tl.Value.end : -9f;
            float after = before.HasValue && tl.HasValue ? tl.Value.start - before.Value.end : -9f;
            bool ok = tl.HasValue && rl.HasValue && gap >= 0.2f && gap <= 1.5f && (beforeName == null || (before.HasValue && after >= 0.2f));
            pairsChecked++; if (!(tl.HasValue && rl.HasValue)) pairsLost++;                       // final test pass: V6 counts a pair with a half lost too
            Check($"{what}: \"Territory lost.\" then \"Radar lost.\", back to back (gap {gap:F2} s, 0.2-1.5 s)"
                  + (beforeName != null ? $", after {beforeName} ({after:F2} s after its end)" : "")
                  + $"; territory {tl.HasValue}, radar lost {rl.HasValue}; {Why}", ok);
        }

        /// <summary>V1's counter: pairs of lines on two different sources (each source has its own name in the floor, every outside source
        /// too: fix round 2) that play together for more than Overlap s; `all` sorted by start. A source plays one line at a time, so two
        /// lines of one name are its restart, not an overlap; the unregistered ones are each counted.</summary>
        static int Overlaps(List<(string who, string id, string clip, float a, float b, float len)> all, StringBuilder bad, float t0)
        {
            int over = 0;
            for (int i = 0; i < all.Count; i++)
                for (int j = i + 1; j < all.Count && all[j].a < all[i].b; j++)
                {
                    if (all[i].who == all[j].who && all[i].who != "unregistered") continue;              // one source plays one line at a time
                    float o = Mathf.Min(all[i].b, all[j].b) - all[j].a;
                    if (o <= Overlap) continue;
                    over++; if (bad != null && over <= 20) bad.AppendLine($"   OVERLAP {o:F2} s: {all[i].who} {all[i].id ?? "-"} {all[i].clip} [{all[i].a - t0:F2}..{all[i].b - t0:F2}] and {all[j].who} {all[j].id ?? "-"} {all[j].clip} [{all[j].a - t0:F2}..{all[j].b - t0:F2}]");
                }
            return over;
        }

        // ============================================================================================ V6's oracle (final test pass)
        /// <summary>Every voice line of the run so far: the floor's line log (each source under its own name) and the unregistered ones the
        /// scan saw, sorted by start; an open line ends now.</summary>
        List<(string who, string id, string clip, float a, float b, float len)> AllLines()
        {
            var all = new List<(string who, string id, string clip, float a, float b, float len)>();
            foreach (var r in OzVoiceFloor.Since(T0)) all.Add((r.who, r.id, r.clip, r.start, r.end, r.len));
            foreach (var x in strays) all.Add(("unregistered", null, x.clip, x.a, x.open ? Now : x.b, 0f));
            all.Sort((p, q) => p.a.CompareTo(q.a));
            return all;
        }

        /// <summary>The longest take of event `id` at its slowest pitch (s); 0 = no take loaded.</summary>
        static float TakeMax(string id)
        {
            var d = AudioDirector.I ? AudioDirector.I.Def(id) : null; float m = 0f;
            if (d == null || d.loaded == null) return 0f;
            float pitch = Mathf.Max(0.05f, Mathf.Abs((d.pitchMul > 0f ? d.pitchMul : 1f) * (1f - Mathf.Max(0f, d.pitchVar))));
            foreach (var c in d.loaded) if (c) m = Mathf.Max(m, c.length / pitch);
            return m;
        }

        /// <summary>The kit's record of line `id` on source `who` that started at or after t and no later than `by` (index in `all`, -1 = none).</summary>
        static int Rec(List<(string who, string id, string clip, float a, float b, float len)> all, string who, string id, float t, float by = float.PositiveInfinity)
        {
            for (int i = 0; i < all.Count; i++) if (all[i].who == who && all[i].id == id && all[i].a >= t - 0.001f && all[i].a <= by) return i;
            return -1;
        }

        /// <summary>Final test pass review: how long Rachel's queue keeps set-up line `id` waiting (UpdateChannels' stale rule: ranks 4-9
        /// RStaleSec after it was asked, ranks 1-3 never).</summary>
        static float RStale(string id) => RankOf(id) <= 3 ? float.PositiveInfinity : RStaleSec;

        /// <summary>Final test pass review: the kit's record of set-up line q of Rachel's: the first line of its event on her channel that
        /// started at or after its ask and no later than her queue could keep it (RStale + 0.25 s for the frames). A later line of the same
        /// event (another set-up's) is not it, so the information checks hold whenever a probe is judged (was: any later record counted).</summary>
        static int RecQ(List<(string who, string id, string clip, float a, float b, float len)> all, RAsk q) => Rec(all, "rachel", q.id, q.t, q.t + RStale(q.id) + 0.25f);

        /// <summary>Final test pass review: set-up line q still in Rachel's queue at t: taken into it, not started by t, not dropped as
        /// stale by then (a line her stale rule dropped no longer holds the floor or her queue).</summary>
        static bool QWaits(List<(string who, string id, string clip, float a, float b, float len)> all, RAsk q, float t)
        {
            if (!q.queued || q.t > t) return false;
            int i = RecQ(all, q);
            return i >= 0 ? all[i].a > t + 0.001f : t < q.t + RStale(q.id) + 0.05f;
        }

        static string NameOfLine((string who, string id, string clip, float a, float b, float len) x) =>
            x.who == "rachel" ? "Rachel (" + (x.id ?? x.clip) + ")" : OzVoiceFloor.IsExternal(x.who) ? "the cutscene voice (" + x.clip + ")"
            : x.who == "unregistered" ? "an unregistered voice (" + x.clip + ")" : "a robot line (" + (x.id ?? x.clip) + ", " + x.who + ")";

        /// <summary>A robot line the floor made wait and then started (OzVoiceFloor's wait records): P started it earlier.</summary>
        static bool Moved((string who, string id, string clip, float a, float b, float len) x)
        {
            foreach (var w in OzVoiceFloor.WaitsSince(x.a - 10f)) if (w.id == x.id && w.started >= 0f && Mathf.Abs(w.started - x.a) < 0.1f) return true;
            return false;
        }

        static int RuleIndex(string rule)
        {
            if (rule == null) return -1;
            for (int i = 0; i < AllowedRules.Length; i++) if (rule.StartsWith(AllowedRules[i])) return i;
            return -1;
        }

        /// <summary>V6, P's Rachel for the probe: the lines its set-up asked that were taken into her queue, as P played them: the first where
        /// the kit started it when that was within 0.1 s of its ask (nothing held it), else a frame after its ask (P's Rachel waited at most
        /// 0.6 s for a robot line then); the rest back to back (P had no gap between her lines), in the order the kit played them; each its
        /// real take length on the line log (its longest take if the kit never said it). `mine` gets the kit's records of these lines (P's
        /// timeline replaces them).</summary>
        List<(RAsk q, float s, float e)> PRachel(Probe pr, List<(string who, string id, string clip, float a, float b, float len)> all, HashSet<int> mine)
        {
            var lines = new List<(RAsk q, float ks, float len)>();
            foreach (var q in pr.rachel)
            {
                if (!q.queued) continue;
                int i = RecQ(all, q);
                if (i >= 0) mine.Add(i);
                lines.Add((q, i >= 0 ? all[i].a : float.PositiveInfinity, i >= 0 ? all[i].len : TakeMax(q.id)));
            }
            lines.Sort((x, y) => x.ks.CompareTo(y.ks));
            var o = new List<(RAsk q, float s, float e)>(); float end = -1e9f;
            for (int k = 0; k < lines.Count; k++)
            {
                var x = lines[k];
                float s;
                if (k == 0 && x.ks - x.q.t <= 0.1f) s = x.ks;
                else
                {
                    s = x.q.t + pr.dt;
                    foreach (var y in all) if ((y.who == "alan_main" || y.who == "alan_alert") && y.a <= s && s < y.b) s = Mathf.Min(y.b, x.q.t + 0.6f);
                }
                s = Mathf.Max(s, end);
                o.Add((x.q, s, s + x.len)); end = s + x.len;
            }
            return o;
        }

        struct PF { public bool said, over; public float at; public string why, overWho; }

        /// <summary>V6: P's fate of the probe's line, MEASURED (no hand label): the probe's own timings (its ask, the unit reply and Rachel's
        /// lines its set-up asked), the kit's line log for the real take lengths and the other voices, and P's own numbers (PHoldSec, PGapSec,
        /// PRobotGapSec, PGateWait, PGateAfter). P had no floor: Rachel's lines back to back (PRachel); a shuttle line held behind her (DV118:
        /// she speaks, or ended less than 0.3 s ago), let go 0.3 s after her last line, dropped past 3.0 s, dropped when Alan's main channel
        /// speaks then; any other robot line dropped inside the robot line gap (Alan's main channel speaking, or less than 3.0 s after its
        /// last line), else said at once over any other voice (Rachel waited only for a shuttle line); the no-way gate said its line 0.25 s
        /// after Rachel's and both robot channels went quiet (outside voices not asked), dropped past 3.0 s. over = P's line would have been
        /// heard with another voice (P said it only over a voice).</summary>
        PF PModel(Probe pr, Fate f, List<(string who, string id, string clip, float a, float b, float len)> all)
        {
            var mine = new HashSet<int>();
            var rach = new List<Vector2>(); foreach (var r in PRachel(pr, all, mine)) rach.Add(new Vector2(r.s, r.e));
            int own = -1;
            if (f.said) for (int i = 0; i < all.Count; i++) if (all[i].who == f.who && Mathf.Abs(all[i].a - f.start) < 0.001f) { own = i; break; }
            if (own >= 0) mine.Add(own);
            var reply = new Vector2(-1f, -1f);
            if (pr.reply != null && pr.replyAt >= 0f)
            {
                int i = Rec(all, "alan_main", pr.reply, pr.replyAt, pr.replyAt + DUnitWait + WaitSlack + 0.25f);   // review: within its own wait (a later reply is not it)
                if (i >= 0) mine.Add(i);
                reply = new Vector2(pr.replyAt, pr.replyAt + (i >= 0 ? all[i].len : TakeMax(pr.reply)));   // P said it at once (no floor; the robot line gap was over)
            }
            for (int i = 0; i < all.Count; i++) if (!mine.Contains(i) && all[i].who == "rachel") rach.Add(new Vector2(all[i].a, all[i].b));   // her other lines, as the kit played them
            float a = pr.ask, len = own >= 0 ? all[own].len : TakeMax(pr.id);
            var pf = new PF { at = a };
            if (pr.kind == PKind.GateFlow)
            {   // P's NoWayQuiet: Rachel's channel and both robot channels quiet, then PGateAfter; dropped past PGateWait
                var busy = new List<Vector2>(rach);
                if (reply.x >= 0f) busy.Add(reply);
                for (int i = 0; i < all.Count; i++) if (!mine.Contains(i) && (all[i].who == "alan_main" || all[i].who == "alan_alert")) busy.Add(new Vector2(all[i].a, all[i].b));
                float q = a;
                for (bool grew = true; grew; ) { grew = false; foreach (var b in busy) if (b.x < q + PGateAfter && b.y > q) { q = b.y; grew = true; } }
                pf.at = q + PGateAfter;
                if (pf.at - a > PGateWait) { pf.why = $"P: the voices did not go quiet within {PGateWait:F1} s (quiet + {PGateAfter:F2} s at +{pf.at - a:F2} s)"; return pf; }
            }
            else
            {
                bool on = false; float lastEnd = -1e9f, e = -1e9f;
                foreach (var r in rach) { if (r.x <= a && a < r.y) { on = true; e = Mathf.Max(e, r.y); } else if (r.y <= a) lastEnd = Mathf.Max(lastEnd, r.y); }
                bool held = pr.kind == PKind.Landing && (on || a - lastEnd < PGapSec);                // DV118 holds a shuttle line only
                if (held)
                {
                    if (!on) e = lastEnd;
                    for (bool grew = true; grew; ) { grew = false; foreach (var r in rach) if (r.x < e + PGapSec && r.y > e) { e = r.y; grew = true; } }
                    pf.at = e + PGapSec;
                    if (pf.at - a > PHoldSec) { pf.why = $"P: held behind Rachel past DV118's {PHoldSec:F1} s (her lines back to back + {PGapSec:F1} s = +{pf.at - a:F2} s)"; return pf; }
                    if (reply.x >= 0f && reply.x <= pf.at && pf.at < reply.y) { pf.why = $"P: let go +{pf.at - a:F2} s into the unit reply on Alan's main channel (said over her at +{reply.x - a:F2} s, ends +{reply.y - a:F2} s): robot line gap"; return pf; }
                    for (int i = 0; i < all.Count; i++)
                        if (!mine.Contains(i) && all[i].who == "alan_main" && all[i].a <= pf.at && pf.at < all[i].b) { pf.why = $"P: let go +{pf.at - a:F2} s into {NameOfLine(all[i])}: robot line gap"; return pf; }
                }
                else
                {
                    bool mainOn = reply.x >= 0f && reply.x <= a && a < reply.y; float mainEnd = reply.x >= 0f && reply.y <= a ? reply.y : -1e9f;
                    for (int i = 0; i < all.Count; i++)
                        if (!mine.Contains(i) && all[i].who == "alan_main" && all[i].a <= a) { if (a < all[i].b) mainOn = true; else mainEnd = Mathf.Max(mainEnd, all[i].b); }
                    if (mainOn || a < mainEnd + PRobotGapSec) { pf.why = $"P: the robot line gap (Alan's main channel {(mainOn ? "speaking" : (a - mainEnd).ToString("F2") + " s after its last line")}, {PRobotGapSec:F1} s)"; return pf; }
                }
            }
            pf.said = true;
            // heard with another voice in P: Rachel waits for a shuttle line (DV118); every other voice line is heard with it
            float t1 = pf.at + Mathf.Max(0.05f, len);
            if (pr.kind != PKind.Landing) foreach (var r in rach) if (Mathf.Min(r.y, t1) - Mathf.Max(r.x, pf.at) > Overlap) { pf.over = true; pf.overWho = "Rachel"; return pf; }
            if (reply.x >= 0f && Mathf.Min(reply.y, t1) - Mathf.Max(reply.x, pf.at) > Overlap) { pf.over = true; pf.overWho = "the unit reply"; return pf; }
            for (int i = 0; i < all.Count; i++)
            {
                if (mine.Contains(i) || all[i].who == "rachel") continue;
                if (Mathf.Min(all[i].b, t1) - Mathf.Max(all[i].a, pf.at) > Overlap) { pf.over = true; pf.overWho = NameOfLine(all[i]); return pf; }
            }
            return pf;
        }

        /// <summary>V6: the first real time in [t0, t1] at which the voice floor was free for a robot line, from the records only: no voice
        /// line audible (every source; the probe's own line left out) and GapSec since the last one ended, and none of the probe's Rachel lines
        /// waiting (taken into her queue, not started yet, not dropped as stale: QWaits). +Infinity = not free in that span. The gap is the
        /// design's DGapSec (review: was the kit's live OzVoiceFloor.GapSec).</summary>
        static float FloorFreeAt(Probe pr, Fate f, List<(string who, string id, string clip, float a, float b, float len)> all, float t0, float t1)
        {
            var cand = new List<float> { t0 };
            foreach (var x in all) { float c = x.b + DGapSec; if (c > t0 && c <= t1) cand.Add(c); }
            foreach (var q in pr.rachel) { float c = q.t + RStale(q.id) + 0.05f; if (q.queued && c > t0 && c <= t1) cand.Add(c); }   // review: a stale-dropped line frees her queue
            cand.Sort();
            foreach (float t in cand)
            {
                bool free = true;
                foreach (var x in all)
                {
                    if (f.said && x.who == f.who && Mathf.Abs(x.a - f.start) < 0.001f) continue;
                    if (x.a <= t + 0.001f && t < x.b + DGapSec - 0.001f) { free = false; break; }
                }
                if (free)
                    foreach (var q in pr.rachel)
                        if (QWaits(all, q, t)) { free = false; break; }                                    // a line of hers waits: it goes first
                if (free) return t;
            }
            return float.PositiveInfinity;
        }

        /// <summary>V6: what held the floor at t for a line P said over no voice: one of the set-up's Rachel lines (waiting, speaking or in the
        /// 0.3 s after it) that P said AFTER the probe's line = information first (row a), one P said before it = the floor's 0.3 s gaps
        /// moved her later; the promotion; a robot line the floor moved (the robot slot); the 0.3 s gap after a voice. null = none of these.
        /// Review: the 0.3 s gap's note says which form it is: the kit started her later than P (her lines back to back in P), or her own
        /// line's end + the floor's 0.3 s ran past the line's wait (a single line of hers: the no-way gate's band).</summary>
        string HolderAt(Probe pr, Fate f, PF p, List<(string who, string id, string clip, float a, float b, float len)> all, float t)
        {
            foreach (var r in PRachel(pr, all, new HashSet<int>()))
            {
                if (r.q.t > t) continue;
                int i = RecQ(all, r.q);
                if (i >= 0 && all[i].b + DGapSec <= t) continue;                                           // over before t
                if (i < 0 && !QWaits(all, r.q, t)) continue;                                                 // dropped as stale before t
                if (r.s >= p.at - 0.001f) return RInfo + $" ({r.q.id}: P said the line first and held her, the kit puts her first)";
                if (i < 0) return RGap + $" ({r.q.id}: her line still waiting at +{t - pr.ask:F2} s, behind the floor's {DGapSec:F1} s gaps)";
                if (all[i].a > r.s + AtOnce) return RGap + $" ({r.q.id}: P had her lines back to back, the kit {DGapSec:F1} s after each voice: she started {all[i].a - r.s:F2} s later than in P)";
                return RGap + $" ({r.q.id}: she ended +{all[i].b - pr.ask:F2} s; the floor's {DGapSec:F1} s after her ran past the line's own wait)";
            }
            foreach (var x in all)
            {
                if (f.said && x.who == f.who && Mathf.Abs(x.a - f.start) < 0.001f) continue;
                if (!(x.a <= t && t < x.b)) continue;
                if (x.id == "vo_promoted") return RPromo;
                if ((x.who == "alan_main" || x.who == "alan_alert") && (Moved(x) || x.a > pr.ask)) return RSlot + $" ({x.id ?? x.clip})";
                return null;
            }
            foreach (var x in all) if (x.b <= t && t < x.b + DGapSec) return RGap + $" (after {NameOfLine(x)})";
            return null;
        }

        /// <summary>V6: the allowed rule that explains why the kit does not say a line P said, measured from the probe's records (null = none:
        /// V6 fails). edge = the probe's FloorSlack + 2 frames; nAfter = Rachel's lines that started after the ask and before the line.</summary>
        string SkipRule(Probe pr, Fate f, PF p, List<(string who, string id, string clip, float a, float b, float len)> all, float edge, int nAfter)
        {
            float a = pr.ask;
            if (f.said)
            {   // a landing line its test refused (the chooser gives the landing to Rachel or nobody) that would have been said
                if (pr.kind != PKind.Landing) return null;
                float lim = f.dvHeld ? PHoldSec + DGapSec * nAfter + (f.waitAt >= 0f ? DUnitWait : 0f)
                          : f.waitAt >= 0f ? f.waitAt - a + DUnitWait : -1f;
                if (lim < 0f) return null;                                                                 // said at once: no rule lets the test refuse that
                float margin = lim - (f.start - a);
                if (margin <= edge) return RSlack + $" (said +{f.start - a:F2} s, {margin:F2} s inside its limit +{lim:F2} s)";
                float surplus = 0f;
                foreach (var q in pr.rachel)
                {
                    int i = q.queued ? RecQ(all, q) : -1;
                    if (i >= 0 && all[i].a > a) surplus += Mathf.Max(0f, TakeMax(q.id) - all[i].len);
                }
                if (surplus > 0f && margin <= surplus + edge) return RLongest + $" (her waiting lines' longest takes {surplus:F2} s longer than the real ones; the line said {margin:F2} s inside its limit)";
                return null;
            }
            if (f.why == null) return null;                                                                // not said, no row says why: lost
            if (f.why.StartsWith("dropped (held too long"))
                return p.at - a > PHoldSec - edge ? RSlack + $" (P let it go +{p.at - a:F2} s, within {edge:F2} s of DV118's {PHoldSec:F1} s)" : null;
            if (f.why.StartsWith("dropped (robot line gap)"))
            {
                foreach (var x in all)
                {
                    if (x.who != "alan_main" || x.id == pr.id) continue;
                    bool on = x.a <= f.whyAt + pr.dt + 0.02f && x.b >= f.whyAt - pr.dt - 0.02f;
                    if (on && x.a > a - 0.001f) return (x.id == "vo_promoted" ? RPromo : RSlot) + $" ({x.id ?? x.clip} waited when the line was asked, started +{x.a - a:F2} s; the line let go +{f.whyAt - a:F2} s)";
                    if (x.b <= f.whyAt + 0.02f && f.whyAt - x.b < PRobotGapSec && Moved(x)) return RRobotGap + $" ({x.id ?? x.clip}, ended {f.whyAt - x.b:F2} s before)";
                }
                return null;
            }
            bool flow = pr.kind == PKind.GateFlow;
            if (f.why.StartsWith("skipped (waited") || (flow && f.why.StartsWith("no-way line dropped")))
            {
                float from = flow ? a : f.waitAt >= 0f ? f.waitAt : a;
                float wait = flow ? PGateWait - PGateAfter : DUnitWait;                                   // review: the design's / P's numbers (V6a: = the live game's), never the live statics
                float end = from + wait - edge;                                                            // the floor must be free this much before its wait runs out (the slot / the gate act on whole frames: FloorSlack + 2 frames)
                float free = FloorFreeAt(pr, f, all, from, end);
                if (free <= end) return null;                                                              // the floor was free inside its wait
                if (p.over) return RWait + $" ({(flow ? "its gate's " + PGateWait.ToString("F1") + " s" : "its " + wait.ToString("F1") + " s")}; P said it over {p.overWho})";
                return HolderAt(pr, f, p, all, end);
            }
            return null;
        }

        /// <summary>V6: the information line check of a probe's set-up: each of Rachel's lines it asked that was taken into her queue must be
        /// said; one not said is LOST when the floor was free for her before her queue's stale rule could drop it (ranks 4-9 after 4 s; ranks
        /// 1-3 never), else it went stale behind voices (allowed: information lines wait until stale). The first time the floor was free for
        /// set-up line `qi` (no voice audible or starting within 0.1 s, GapSec since the last one, none of her lines ahead of it in her queue
        /// still waiting: asked no later and of a higher priority, or of the same rank and asked first), +Infinity = none. A robot line that
        /// started after her line was asked does not hold her: the floor starts no robot line while a line of hers waits (RobotFloorFree,
        /// RachelNextFirst), so such a start means her line was no longer waiting. Review: used both ways: a line never said is LOST when
        /// this is before her stale time; a line said is HELD (said later than its rule allows) when it started more than RLate after this
        /// (information lines wait only for the floor: no voice, the 0.3 s after one, her lines ahead of it). The gap is DGapSec.</summary>
        static float FreeForRachel(Probe pr, List<(string who, string id, string clip, float a, float b, float len)> all, int qi, float end, int self = -1)
        {
            var q = pr.rachel[qi]; int qr = RankOf(q.id);
            var cand = new List<float> { q.t };
            foreach (var x in all) { float c = x.b + DGapSec; if (c > q.t && c <= end) cand.Add(c); }
            foreach (var o in pr.rachel) { float c = o.t + RStale(o.id) + 0.05f; if (o.queued && c > q.t && c <= end) cand.Add(c); }   // a line ahead of it dropped as stale
            cand.Sort();
            foreach (float t in cand)
            {
                bool free = true;
                for (int xi = 0; xi < all.Count; xi++)
                {
                    var x = all[xi];
                    if (xi == self) continue;                                                              // the line itself (the held check: its own start)
                    if ((x.who == "alan_main" || x.who == "alan_alert") && x.a > q.t) continue;           // a robot line started after her line was asked
                    if ((x.a <= t + 0.001f && t < x.b + DGapSec - 0.001f) || (x.a > t && x.a < t + 0.1f)) { free = false; break; }
                }
                if (free)
                    for (int oi = 0; oi < pr.rachel.Count; oi++)
                    {
                        var o = pr.rachel[oi]; int orank = RankOf(o.id);
                        if (oi == qi || !o.queued || o.t > q.t + 0.001f || !(orank < qr || (orank == qr && oi < qi))) continue;   // not ahead of it in her queue
                        if (QWaits(all, o, t)) { free = false; break; }
                    }
                if (free) return t;
            }
            return float.PositiveInfinity;
        }

        /// <summary>Review: how much later than the floor let it a set-up line of Rachel's may start: AtOnce + 2 frames (her line starts in
        /// the director's next Update, the floor's line log notes it a frame later).</summary>
        static float RLate(Probe pr) => AtOnce + 2f * pr.dt;

        static int RankOf(string id) { var d = AudioDirector.I ? AudioDirector.I.Def(id) : null; return d != null ? d.rank : 9; }

        /// <summary>V6: the probes whose windows are over, judged now (ProbeQuiet, Verdicts).</summary>
        void SettleDue()
        {
            List<(string who, string id, string clip, float a, float b, float len)> all = null;
            foreach (var pr in probes) if (!pr.settled && Now > pr.until) { if (all == null) all = AllLines(); Settle(pr, all); }
        }

        /// <summary>V6: one probe judged. The line itself (said: no other voice with it, 0.25 s or more after the one before, no later than its
        /// rule allows); the test against the line's fate (the landing: "yes" = the chooser asks the robot, so the line must be said; the gate
        /// and the banter: "go" = the caller says the line now, it must start at once, "wait" = the caller waits, the line must not have
        /// started at once); P's measured fate against the kit's (a line P said that the kit does not say needs an allowed rule); the set-up's
        /// information lines. One row in the V6 table.</summary>
        void Settle(Probe pr, List<(string who, string id, string clip, float a, float b, float len)> all)
        {
            pr.settled = true;
            var f = FateOf(pr); var p = PModel(pr, f, all);
            bool land = pr.kind == PKind.Landing, flow = pr.kind == PKind.GateFlow;
            float a = pr.ask, edge = pr.slack + 2f * pr.dt;
            var why = new List<string>(); var notes = new List<string>();
            int heard = 0; float prevEnd = -999f;
            if (f.said) { heard = OverlapsWith(f, all); foreach (var x in all) if (x.a < f.start - 0.001f && x.b > prevEnd) prevEnd = x.b; }
            int nAfter = 0; foreach (var x in all) if (x.who == "rachel" && x.a > a && x.a <= pr.until && (!f.said || x.a < f.start)) nAfter++;
            float limit = flow ? a + PGateWait + AtOnce                                               // review: P's / the design's numbers (V6a: = the live game's), never the live statics
                        : f.dvHeld ? a + PHoldSec + DGapSec * nAfter + (f.waitAt >= 0f ? DUnitWait : 0f) + WaitSlack
                        : f.waitAt >= 0f ? f.waitAt + DUnitWait + WaitSlack : a + AtOnce;
            if (!pr.twin && f.said)
            {
                if (heard > 0) { pr.over = true; why.Add($"{heard} voice line(s) audible with it"); }
                if (f.start - prevEnd < WaitGapMin) { pr.over = true; why.Add($"it started {f.start - prevEnd:F2} s after the voice line before it ended (under {WaitGapMin:F2} s)"); }
                if (f.start > limit) { pr.late = true; why.Add($"said +{f.start - a:F2} s after the ask, its rule allows +{limit - a:F2} s"); }
                if (f.gateParked) { pr.late = true; why.Add("the gate let it go and the voice rules parked it (the gate counted it unsaid; said later from the robot slot)"); }
            }
            pr.agrees = land ? pr.yes == f.said : flow || pr.yes == f.atOnce;
            if (land && pr.yes && !f.said) { pr.badSkip = true; why.Add("the test said yes, the line was dropped (" + (f.why ?? "no reason logged") + "): the landing lost its voice"); }
            if (pr.kind == PKind.Gate && pr.yes && !f.atOnce)
            {
                if (f.said) { pr.late = true; why.Add("the test said go, the line did not start at once (its caller counts it unsaid and it waits)"); }
                else { pr.badSkip = true; why.Add("the test said go, the line was not said"); }
            }
            if (pr.kind == PKind.Gate && !pr.yes && f.atOnce) { pr.badSkip = true; why.Add("the test said wait, the line started at once: its caller would wait past a free floor"); }
            pr.pSaid = p.said; pr.kitSays = land ? pr.yes && f.said : f.said;
            if (flow && f.gateParked && !f.said) { pr.badSkip = true; why.Add("the gate let it go, the voice rules parked it and skipped it (the gate counted it unsaid)"); }
            if (!pr.twin && p.said && !pr.kitSays && !pr.badSkip)
            {
                pr.rule = SkipRule(pr, f, p, all, edge, nAfter);
                if (pr.rule == null) { pr.badSkip = true; why.Add("P said it, the kit does not, and no allowed rule explains it"); }
            }
            if (!pr.twin)
                for (int qi = 0; qi < pr.rachel.Count; qi++)
                {
                    var q = pr.rachel[qi];
                    if (!q.queued) continue;
                    int ri = RecQ(all, q);
                    if (ri >= 0)
                    {   // review: said: no later than RLate after the floor was first free for it (information lines wait only for the floor; was:
                        // never looked at, so a fault holding her with nothing audible passed and made the robot line's skip look "information first")
                        float by = all[ri].a - RLate(pr), free = FreeForRachel(pr, all, qi, by, ri);
                        if (free <= by) { pr.infoLate++; why.Add($"Rachel's {q.id} (asked +{q.t - a:F2} s) started +{all[ri].a - a:F2} s although the floor was free for her at +{free - a:F2} s (more than {RLate(pr):F2} s): an information line held, said later than its rule allows"); }
                        continue;
                    }
                    float end = Mathf.Min(q.t + RStale(q.id), Now);
                    float free2 = FreeForRachel(pr, all, qi, end);
                    if (free2 <= end) { pr.infoLost++; why.Add($"Rachel's {q.id} (asked +{q.t - a:F2} s) never said although the floor was free for her at +{free2 - a:F2} s: an information line lost"); }
                    else notes.Add($"Rachel's {q.id} went stale behind voices (allowed: information lines wait until stale)");
                }
            string ans = land ? (pr.yes ? "yes" : "no") : flow ? "(the order itself)" : (pr.yes ? "go" : "wait");
            string kit = f.said ? $"said on {f.who} +{f.start - a:F2} s{(f.dvHeld ? " (held behind Rachel first)" : f.waitAt >= 0f ? " (waited for the floor first)" : f.atOnce ? " (at once)" : "")}, {f.start - prevEnd:F2} s after the voice line before it"
                                : f.why != null ? "not said: " + f.why : "NOT SAID, no reason logged";
            if (land && !pr.yes) kit += " [the test said no: the chooser gives the landing to Rachel or nobody]";
            string pt = p.said ? $"P said it +{p.at - a:F2} s " + (p.over ? "over " + p.overWho : "over no voice") : p.why;
            bool bad = pr.over || pr.late || pr.badSkip || pr.infoLost > 0 || pr.infoLate > 0;
            string tag = bad ? "PROBE BAD    " : pr.rule != null ? "ALLOWED SKIP " : "probe ok     ";
            if (land && pr.what.Contains("K407") && pr.yes && f.said && !bad) pr.k407 = true;
            v6Rows.AppendLine($"   {tag}[{pr.test}] {pr.what}: test {ans}; the kit: {kit}; P (measured): {pt}"
                              + (pr.rule != null ? "; ALLOWED SKIP (" + pr.rule + ")" : "") + (why.Count > 0 ? "; " + string.Join("; ", why) : "") + (notes.Count > 0 ? "; " + string.Join("; ", notes) : ""));
        }
        void Verdicts()
        {
            SettleDue();                                                                           // final test pass: the last probes (their windows are over)
            var lines = OzVoiceFloor.Since(T0);
            var all = AllLines();
            int cut = 0; var bad = new StringBuilder();
            int over = Overlaps(all, bad, T0);
            string zodName = OzVoiceFloor.NameOf(zodSrc);                                          // the run's own cutscene source (its own name)
            var outside = new HashSet<string>(); foreach (var r in lines) if (OzVoiceFloor.IsExternal(r.who)) outside.Add(r.who);
            foreach (var r in lines)
                if (!r.open && r.len > 0f && r.end - r.start < r.len - CutSlack && !(zodName != null && r.who == zodName))
                { cut++; if (cut <= 20) bad.AppendLine($"   CUT {r.who} {r.id ?? "-"} {r.clip}: played {r.end - r.start:F2} of {r.len:F2} s at {r.start - T0:F2}"); }
            Log("voice lines recorded: " + lines.Count + ", unregistered voice sources seen: " + strays.Count + "; unit lines asked by the spam: " + spamAsked + ", real clicks " + clicksAsked
                + "; outside voice sources heard: " + (outside.Count > 0 ? string.Join(", ", outside) : "none"));
            Check($"V1 zero overlapping voice lines over the whole run ({all.Count} lines, every source under its own name; tolerance {Overlap:F2} s)", over == 0);
            // V1a: the counter itself on a made-up pair: two different outside sources at once = 1; one source's two lines (a restart) = 0
            string a1 = zodName ?? OzVoiceFloor.External + ":OzVoiceOverlapProof#0", a2 = OzVoiceFloor.External + ":ZodBriefing#99";
            var fake = new List<(string who, string id, string clip, float a, float b, float len)>
            {
                (a1, null, "zod_test_a", 0f, 2f, 2f), (a2, null, "zod_test_b", 1f, 3f, 2f),
                ("rachel", "ann_test_x", "rachel_test_x", 4f, 5f, 1f), ("rachel", "ann_test_y", "rachel_test_y", 4.9f, 6f, 1.1f)
            };
            int fakeOver = Overlaps(fake, null, 0f);
            Check($"V1a the overlap counter counts two different outside sources speaking together ({a1} + {a2}: 1 wanted) and not one source's restart ({fakeOver} counted)", fakeOver == 1);
            Check("V2 no voice line cut before its clip's end (the run's own cutscene source left out)", cut == 0);
            Check("V3 no voice line from a source the floor does not know (every voice player registered)", strays.Count == 0);
            int dHeld = AudioDirector.FloorHeld - held0, dRel = AudioDirector.FloorReleased - released0, dSkip = AudioDirector.FloorSkipped - skipped0;
            int dRachel = AudioDirector.FloorRachelWaited - rachelWaited0, dGuard = AudioDirector.FloorGuardBlocked - guard0, dPair = AlertGate.Paired - paired0;
            Check($"V4 the floor worked in this run (counted from its start): robot lines waited {dHeld}, started after waiting {dRel}, skipped {dSkip}; Rachel lines waited {dRachel}; PlayVoice refused {dGuard}; AlertGate passes for one event's lines {dPair}",
                  dHeld > 0 && dRel > 0 && dRachel > 0 && dPair >= 2);
            // V5: every robot line that waited and started, measured on the line log (its real AudioSource start)
            int measured = 0, badW = 0;
            foreach (var w in OzVoiceFloor.WaitsSince(T0))
            {
                if (w.started < 0f) continue;
                OzVoiceFloor.LineRec? hit = null;
                foreach (var r in lines) if (r.id == w.id && r.start >= w.started - 0.02f) { hit = r; break; }
                if (!hit.HasValue) { badW++; bad.AppendLine($"   WAITED {w.id}: started at {w.started - T0:F2} by the director, but no line of it in the log"); continue; }
                var h = hit.Value; float prevEnd = -999f; string prev = "-";
                foreach (var x in all) if (x.a < h.start - 0.001f && x.b > prevEnd) { prevEnd = x.b; prev = (x.id ?? x.clip) + " (" + x.who + ")"; }
                float gapW = h.start - prevEnd, waited = h.start - w.asked;
                measured++;
                bool ok = gapW >= WaitGapMin && waited <= w.wait + WaitSlack;
                if (!ok) badW++;
                bad.AppendLine($"   {(ok ? "waited ok" : "WAITED BAD")} {w.id}: asked {w.asked - T0:F2}, started {h.start - T0:F2} ({waited:F2} s, may wait {w.wait:F1}), {gapW:F2} s after {prev} ended");
            }
            Check($"V5 every robot line that waited and started: {WaitGapMin:F2} s or more after the line before it ended, no later than its wait (+{WaitSlack:F1} s) after it was asked ({measured} measured, {badW} wrong)",
                  measured > 0 && badW == 0);
            // V6a (final test pass): V6's oracle measures P's fate with P's numbers, hard-coded; the live game must still have them
            var ng = AudioDirector.NoWayGate;
            bool pSame = Mathf.Approximately(OzShuttleVoiceGate.ShuttleHoldMaxSec, PHoldSec) && Mathf.Approximately(OzShuttleVoiceGate.GapSec, PGapSec)
                         && Mathf.Approximately(AudioDirector.AlanMainGap, PRobotGapSec) && ng != null && System.Math.Abs(ng.Wait - PGateWait) < 1e-4 && System.Math.Abs(ng.After - PGateAfter) < 1e-4
                         && Mathf.Approximately(OzVoiceFloor.UnitReplyWait, DUnitWait) && Mathf.Approximately(OzVoiceFloor.GapSec, DGapSec);   // review: the design's numbers for the kit's limits too
            Check($"V6a the numbers in V6's oracle (hard-coded) = the live game's: P's DV118 hold {PHoldSec:F2} s = OzShuttleVoiceGate.ShuttleHoldMaxSec {OzShuttleVoiceGate.ShuttleHoldMaxSec:F2}, "
                  + $"its gap {PGapSec:F2} s = OzShuttleVoiceGate.GapSec {OzShuttleVoiceGate.GapSec:F2}; the robot line gap {PRobotGapSec:F2} s = AudioDirector.AlanMainGap {AudioDirector.AlanMainGap:F2}; "
                  + $"the no-way gate's wait / breath {PGateWait:F2} / {PGateAfter:F2} s = OzNoWayGate.Wait / After {(ng != null ? ng.Wait.ToString("F2") + " / " + ng.After.ToString("F2") : "(no director)")}; "
                  + $"the design's unit reply wait {DUnitWait:F2} s = OzVoiceFloor.UnitReplyWait {OzVoiceFloor.UnitReplyWait:F2}, the floor's gap {DGapSec:F2} s = OzVoiceFloor.GapSec {OzVoiceFloor.GapSec:F2}", pSame);
            // V6 (fix round 3; final test pass): every probe of S12 judged (Settle) on its line's real fate against P's MEASURED fate; a line
            // P said that the kit does not say is an ALLOWED SKIP only with one of AllowedRules, counted here
            int judged = 0, pSaidN = 0, kitSaidN = 0, allowed = 0, over6 = 0, late6 = 0, held6 = 0, skip6 = 0, info6 = 0, agree6 = 0, tests6 = 0; bool k407 = false;
            var byRule = new int[AllowedRules.Length];
            var v6Tests = new HashSet<string>(); var v6Yes = new HashSet<string>(); var v6No = new HashSet<string>();
            foreach (var pr in probes)
            {
                if (!pr.settled) continue;
                judged++;
                if (pr.kind != PKind.GateFlow) { tests6++; if (pr.agrees) agree6++; v6Tests.Add(pr.test); (pr.yes ? v6Yes : v6No).Add(pr.test); }
                if (pr.over) over6++;
                if (pr.late) late6++;
                if (pr.badSkip) skip6++;
                info6 += pr.infoLost; held6 += pr.infoLate;
                if (pr.k407) k407 = true;
                if (pr.twin || !pr.pSaid) continue;
                pSaidN++;
                if (pr.kitSays) kitSaidN++;
                else if (pr.rule != null && !pr.badSkip && pr.infoLost == 0 && pr.infoLate == 0) { allowed++; int ri = RuleIndex(pr.rule); if (ri >= 0) byRule[ri]++; }   // review: a skip in a probe with a fault is not counted as allowed
            }
            var ruleText = new StringBuilder();
            for (int i = 0; i < AllowedRules.Length; i++) if (byRule[i] > 0) ruleText.Append((ruleText.Length > 0 ? ", " : "") + AllowedShort[i] + " " + byRule[i]);
            bool v6Both = v6Tests.Count == 3 && v6Yes.Count == 3 && v6No.Count == 3;
            bool v6All = v6Planned > 0 && judged == v6Planned && v6Missed.Count == 0;                    // fix round 4: every planned judgement made
            int v6Faults = over6 + info6 + pairsLost + late6 + held6 + skip6;
            bad.AppendLine("   V6 probes (the kit's fate from the director's records; P's fate measured from the probe's own timings with P's numbers):");
            bad.Append(v6Rows);
            foreach (var m in v6Missed) bad.AppendLine("   PROBE MISSED " + m);
            Check("V6 every S12 probe on its line's real fate (its AudioSource start / end, its held / waits / skipped / dropped rows) against P's fate MEASURED from the probe's own timings ("
                  + (v6Has ? $"{judged} of {v6Planned} planned judgements made, set-ups missed {v6Missed.Count}); lines P said {pSaidN}: the kit says {kitSaidN}, ALLOWED SKIPS {allowed}{(allowed > 0 ? " (" + ruleText + ")" : "")}; "
                             + $"faults (0 wanted each): two voices at once {over6}, information lines lost {info6}, paired lines lost {pairsLost} (S5 / S5b, {pairsChecked} pairs), said later than their rule allows {late6 + held6} (robot lines {late6}, Rachel's set-up lines held with the floor free {held6}), skips no allowed rule explains {skip6}; "
                             + $"INFO: the tests agree with their line's fate in {agree6} of {tests6}, each test answered both ways {v6Both}, the K407 probe said after her {k407}"
                           : $"not run: no {LandLine} or {GateLine} in the sound table)"),
                  v6Has && v6All && v6Faults == 0);
            Log("ALLOWED SKIPS (the rules V6 accepts for a line P said that the kit does not say; README \"Allowed skips\"): " + string.Join(" | ", AllowedRules));
            if (bad.Length > 0) Log(bad.ToString().TrimEnd());
            Log("the voice lines (who, event, clip, start .. end, clip length; seconds from the run's start):");
            foreach (var r in all) Log($"   {r.who,-32} {r.id ?? "-",-26} {r.clip ?? "-",-44} {r.a - T0,8:F2} .. {r.b - T0,8:F2}  len {r.len:F2}");
        }

        void Restore()
        {
            if (restored) return; restored = true;
            if (keepAnn >= 0 && AudioDirector.Chatter.Level != keepAnn) AudioDirector.Chatter.Level = keepAnn;
            if (keepRobot >= 0 && AudioDirector.RobotChatter.Level != keepRobot) AudioDirector.RobotChatter.Level = keepRobot;
            if (keepIdle >= 0) PlayerPrefs.SetInt(ZodBriefing.PrefIdle, keepIdle);
        }

        void Finish()
        {
            Restore();
            Log($"result: {passes} PASS, {fails} FAIL");
            Report = fails == 0 ? $"PASS ({passes})" : $"FAIL ({fails} of {passes + fails})";
            try { Directory.CreateDirectory(Path.GetDirectoryName(LogPath)); File.WriteAllText(LogPath, sb.ToString()); Debug.Log("[OzVoiceOverlap] " + Report + ", written " + LogPath); }
            catch (System.Exception ex) { Debug.LogWarning("[OzVoiceOverlap] " + ex.Message); }
            StopAllCoroutines();
            Destroy(gameObject);
        }

        void OnDestroy() { Restore(); }
    }
}
#endif
