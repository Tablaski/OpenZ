using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    ///
    /// THE VOICE FLOOR (the rule and its numbers: View/OzVoiceFloor.cs). Before this kit, during a match:
    ///  - a unit reply (alan_main) started while Rachel spoke (SayImpl looked only at Alan's own channel and its 3 s gap);
    ///  - Alan's call-outs (alan_alert) started while Rachel spoke (AlanBusy is Alan's channels only);
    ///  - a unit reply started while an Alan call-out spoke (two Alans at once);
    ///  - Rachel waited at most 0.6 s for Alan, then spoke over him (and DV118 let an older queued line go over him);
    ///  - a higher-ranked Rachel line cut the speaking one after 0.3 s, an end line cut a call-out: words cut off;
    ///  - Brad's answer to Alan's boarding line (HvAnswer) could start over Rachel.
    /// Now (hooks in AudioDirector.cs, Voice/AudioDirector.HeroVoices.cs): Rachel's queue waits while any voice is audible (or held mid-line
    /// by the pause) and starts GapSec after it (her queue's stale rule is unchanged); a robot line asked while a voice is audible (or Rachel
    /// has a line waiting) waits in the director's one pending slot (pendingAlan, the PA7 slot UnitVoices already watches) and starts GapSec
    /// after the floor is free, or is skipped when it waited too long (UnitReplyWait / RobotCallWait / EndLineWait; the promotion line keeps
    /// its own 4 s slot); Brad's answer waits the same way and keeps the floor for itself between Alan's line and its own (one exchange);
    /// PlayVoice refuses to start a line while another voice is audible (the safety net for every other path; the coverage sweeps and proofs
    /// that pick a clip are exempt); no line is stopped for another one any more.
    /// Fix round 2: the callers that judge "would a robot line speak now" before they ask use the same floor: the no-way / lava gate
    /// (NoWayQuiet) and the boom cheer wait until the floor is free (QuietLineFloorFree), the big crash banter (RobotLineFree,
    /// AudioDirector.ShuttleBanter.cs) waits while a robot line would have to wait (RobotLineWouldWait). Fix round 3: the shuttle landing's
    /// voice chooser (SayWouldPlay, AudioDirector.LandingVoice.cs) hears "no" only where the director would really DROP the line
    /// (RobotLineWouldDrop): a landing line held behind Rachel and said 0.3 s after her, or one that waits and starts within its wait, is
    /// "yes" as in P (round 2 said "no" for every line that would wait, so a landing while only Rachel spoke lost its only voice). Fix round
    /// 4: DV118's hold behind Rachel keeps a held shuttle line behind her waiting lines and adds the floor's 0.3 s before each to its 3 s
    /// (VoiceFloorGateTick, OzShuttleVoiceGate.RachelNextFirst / ExtendHold), so it is said where P said it; the landing test also counts
    /// the robot slot, the robot line gap and the frame steps.
    ///
    /// RADAR LOST (View/OzRadarTruth.cs): "Radar lost." only when the player's last standing radar was taken. It is the second line of the
    /// zone-lost event: queued right behind "Territory lost." (FlagView asks for that one 0.35 s after the flag turns, up to 1.6 s later behind
    /// another flag line), or alone after RadarLostAlone s when no "Territory lost." came (its own rules dropped it; a late "Territory lost."
    /// still goes in front of a waiting lone "Radar lost."). Both lines of the event pass AlertGate's 7 s gap between two alerts
    /// (AlertGate.PairNext); "Radar lost." has its own 30 s group. When "Territory lost." starts, the waiting "Radar lost." is held at the head
    /// of Rachel's queue (no newer line goes between them) and its age starts again (Rachel's 4 s stale rule cannot drop it while its partner
    /// speaks; it is kept up to RadarPairWindow s). Its 15 s kind gap, its cooldown and the announcer chatter setting (MINIMAL keeps only
    /// critical lines) still apply. View only: nothing here writes into the match.
    /// </summary>
    public partial class AudioDirector
    {
        // ================================================================================================================ the floor
        /// <summary>Checks: robot lines that waited, started after waiting, skipped; lines PlayVoice refused; Rachel lines that waited.</summary>
        public static int FloorHeld, FloorReleased, FloorSkipped, FloorGuardBlocked, FloorRachelWaited;
        bool floorRelease, pendingBypass;
        float pendingAlanWait = 1f; int pendingAlanRank;
        SoundEventDef floorRachelHead;
        float floorScanAt;
        AudioSource floorCaAlan, floorCaRachel;

        /// <summary>UpdateChannels, once a frame: the director's three channels, the registered briefing / cutscene voices and CombatAudio's
        /// own two voice sources (they speak only when nothing routes here) go to the floor, which follows who is audible; then the radar
        /// pair and the pause lines in Rachel's queue are looked after.</summary>
        void VoiceFloorTick(float now)
        {
            if (alanMain != null) OzVoiceFloor.Sample(alanMain.src, "alan_main", alanMain.def != null ? alanMain.def.id : null);
            if (alanAlert != null) OzVoiceFloor.Sample(alanAlert.src, "alan_alert", alanAlert.def != null ? alanAlert.def.id : null);
            if (rachel != null) OzVoiceFloor.Sample(rachel.src, "rachel", rachel.def != null ? rachel.def.id : null);
            for (int i = 0; i < externalVoices.Count; i++) if (externalVoices[i]) OzVoiceFloor.SampleExternal(externalVoices[i]);   // fix round 2: each under its own name
            if (now >= floorScanAt)
            {
                floorScanAt = now + 1f;
                var ca = CA;
                if (ca && (!floorCaAlan || !floorCaRachel))
                {
                    var a = ca.transform.Find("VoiceAlan"); var r = ca.transform.Find("VoiceRachel");      // CombatAudio.VoiceSource (made on its first line)
                    if (a) floorCaAlan = a.GetComponent<AudioSource>(); if (r) floorCaRachel = r.GetComponent<AudioSource>();
                }
            }
            if (floorCaAlan) OzVoiceFloor.Sample(floorCaAlan, "combat_alan", null);
            if (floorCaRachel) OzVoiceFloor.Sample(floorCaRachel, "combat_rachel", null);
            OzVoiceFloor.Tick(now);
            if (rachelQueue.Count == 0 || (rachel != null && rachel.busy && rachel.src && rachel.src.isPlaying)) rachelGapFrom = -1f;   // fix round 4: no line of hers in the gap now
            RadarPairTick(now);
            PauseLinesTick();
        }

        /// <summary>Fix round 1: "Game paused." / "Game resumed." still waiting when the game is no longer in that state (a voice the pause
        /// holds kept the floor) would say something false: dropped, logged.</summary>
        void PauseLinesTick()
        {
            for (int i = rachelQueue.Count - 1; i >= 0; i--)
            {
                var id = rachelQueue[i].d.id;
                if ((id == "ann_game_paused" && !lastPaused) || (id == "ann_game_resumed" && lastPaused))
                {
                    rachelQueue.RemoveAt(i);
                    OzSoundLog.Add("dir", id, null, "dropped (the game was " + (lastPaused ? "paused" : "resumed") + " again before it could start)");
                }
            }
        }

        /// <summary>A robot line must wait now: a voice is audible (or held by the pause), a briefing / cutscene voice speaks, Rachel has a
        /// line waiting, or a hero exchange keeps the floor for Brad's answer.</summary>
        bool RobotFloorBusy() => OzVoiceFloor.AnyAudible() || OzVoiceFloor.ExternalBusy() || rachelQueue.Count > 0 || Time.unscaledTime < hvExchangeUntil;

        /// <summary>A waiting robot line may start now: nothing audible for GapSec, no briefing voice, Rachel's queue empty, no exchange open.</summary>
        bool RobotFloorFree(float now) => !OzVoiceFloor.Busy(now) && rachelQueue.Count == 0 && now >= hvExchangeUntil;

        /// <summary>Fix round 2: a robot line asked now would go to wait for the floor in SayImpl (VoiceFloorHoldsRobotLine's test).</summary>
        bool RobotLineWouldWait() => !floorRelease && !sweeping && !forceAll && RobotFloorBusy();

        /// <summary>Fix round 2: the lines that wait for quiet voices THEMSELVES before they ask SayImpl (the no-way and lava lines through
        /// NoWayQuiet, Voice/AudioDirector.NoWayLines.cs, also used by AudioDirector.LavaLines.cs; the boom cheer in BoomTick) ask the floor
        /// a waiting robot line asks: Rachel's queue empty, nothing audible or held for GapSec, no briefing voice, no hero exchange open. So
        /// their own wait (no-way 3 s, lava 2 s, the cheer BoomWait 3 s) covers Rachel's waiting lines too, and when they ask, SayImpl finds
        /// the floor free: the line starts at once (or its own rules refuse it) and never goes into the robot pending slot. Was: they looked
        /// at the speaking channels only; a line asked 0.25 s after the voices went quiet met Rachel's line waiting for her 0.3 s gap, went
        /// into the 1 s pending slot and was skipped behind her, and their gate had already counted it unsaid (Spoke(false)), so a waited line
        /// that did play started neither the 25 s no-way gap nor the no-repeat rule, nor OzLava.OrderLineSaid.</summary>
        bool QuietLineFloorFree() => RobotFloorFree(Time.unscaledTime);

        /// <summary>Fix round 3, completed in fix round 4 (K407, SayWouldPlay: the shuttle landing's voice chooser,
        /// OzPlayableShuttle.LandingVoice.cs:69-71, and the big crash banter, OzPlayableShuttle.BigCrash.cs:167): would the director DROP robot
        /// line `id` (event `d`, both callers ask alan_main lines) asked now? Nothing is played, held or changed. It follows SayImpl's own
        /// order for the line:
        /// 1. DV118's hold first: a shuttle line (vo_shuttle_*) that meets Rachel speaking, or less than 0.3 s after her, is held behind her
        ///    (ShuttleLineWaitsForRachel, AudioDirector.ShuttleRound3.cs) and let go 0.3 s after her last line (her waiting lines go first:
        ///    VoiceFloorGateTick); dropped when that is more than ShuttleHoldMaxSec (3 s) + about 0.3 s for each of her waiting lines after the
        ///    ask (the gaps the floor puts before them, RachelGapDone -> ExtendHold). Let go, the robot line gap is not asked (shuttleReleasing), Alan's main channel
        ///    speaking is: a line there then (heard now, or the pending robot line that starts first, TickPendingAlan runs before
        ///    ShuttleRound3Tick) drops it.
        /// 2. Not held: the robot line gap (Alan's main channel speaking, or less than AlanMainGap after its last line) drops it, as in P (fix
        ///    round 4: P's SayWouldPlay did not ask it, so the chooser picked a robot line SayImpl then dropped: a silent landing).
        /// 3. The floor: free = said now (or, held, when let go); taken = it waits in the pending slot: skipped at once behind a more
        ///    important waiting robot line, else said when the floor is free within its wait (1 s for a unit reply), else skipped. The floor's
        ///    end (FloorFreeAt) is every voice heard now, Rachel's waiting lines at their longest take, then (fix round 4) the promotion slot and
        ///    the pending robot line when they will start first.
        /// False = it is said; true = it is dropped. Each limit is met FloorSlack early (the gate and the slot act on whole frames, 1-2 frames
        /// after the exact time). Round 2 answered "dropped" for every line that would wait (RobotLineWouldWait), the held-then-said one too:
        /// a landing while only Rachel spoke lost its only voice when she could not take it either (off screen, her landing rest,
        /// BigCrashVoiceIsRobots).</summary>
        bool RobotLineWouldDrop(string id, SoundEventDef d)
        {
            if (d == null) return false;
            float now = Time.unscaledTime;
            bool main = d.channel == "alan_main";
            bool held = OzShuttleVoiceGate.IsShuttleLine(id) && (RachelPlayingNow || ShuttleGate.ShuttleMustWait(now));   // = SayImpl's hold test after its gate Tick (read only here)
            if (!held && main && (AlanMainPlaying || now < alanMainUntil + AlanMainGap)) return true;   // SayImpl
            if (floorRelease || sweeping || forceAll) return false;                                   // the floor is off (coverage sweeps, proofs)
            float slack = FloorSlack, floor = FloorFreeAt(now), from = now;                           // from: when the line meets the floor
            if (held)
            {
                int n = rachelQueue.Count; float left = OzVoiceFloor.LeftOn("rachel");
                from = n > 0 ? floor                                                                  // let go 0.3 s after her last waiting line (it ends the floor's line-up)
                     : left >= 0f ? now + left + OzShuttleVoiceGate.GapSec                            // 0.3 s after the line she speaks
                     : Mathf.Max(now, now - OzVoiceFloor.QuietFor(now) + OzShuttleVoiceGate.GapSec);  // she just ended
                if (from + slack - now > OzShuttleVoiceGate.ShuttleHoldMaxSec + n * OzVoiceFloor.GapSec) return true;   // held too long behind Rachel (ShuttleRound3Tick)
                float am = OzVoiceFloor.LeftOn("alan_main");
                if (main && am >= 0f && now + am + slack > from) return true;                        // let go into a line on Alan's main channel: robot line gap
            }
            else if (!RobotFloorBusy()) return false;                                                 // the floor is free: said now
            int rank = d.channel == "alan_end" ? 2 : d.channel == "alan_alert" ? 1 : 0;
            bool promo = id == "vo_promoted";
            float wait = promo ? PromoWait : rank == 2 ? OzVoiceFloor.EndLineWait : rank == 1 ? OzVoiceFloor.RobotCallWait : OzVoiceFloor.UnitReplyWait;   // VoiceFloorHoldsRobotLine's waits
            float t = floor;                                                                          // the floor's end for this line
            if (!promo && pendingPromo != null && Mathf.Max(t, now) <= pendingPromoAt + PromoWait + slack)
                t = Mathf.Max(t, now) + LongestTake(Def(pendingPromo), pendingPromoFilter) + OzVoiceFloor.GapSec;   // the promotion's slot goes first (TickPendingAlan)
            if (!promo && pendingAlan != null && pendingAlan != id)
            {   // the one pending robot line: it starts when the floor is free within its own wait
                float s = Mathf.Max(t, now); bool goes = s <= pendingAlanAt + pendingAlanWait + slack;
                if (!held) { if (rank < pendingAlanRank) return true; }                              // a more important line waits: this one is skipped at once (else it replaces it)
                else if (goes && s <= from + slack)
                {   // it starts before the held line is let go
                    if (main && pendingAlanRank == 0) return true;                                    // on Alan's main channel: robot line gap
                    t = s + LongestTake(Def(pendingAlan), pendingAlanFilter) + OzVoiceFloor.GapSec;
                }
                else if (goes && rank < pendingAlanRank) return true;                                // still waiting, more important: this one is skipped
            }
            else if (!promo && pendingAlan != null && pendingAlan == id) from = Mathf.Min(from, pendingAlanAt);   // asked again while it waits: its clock runs from the first ask
            return t + slack > from + wait;                                                           // free by then: said (at once, or after waiting); else skipped
        }

        /// <summary>Fix round 4: the gate and the pending slot act on whole frames, up to 3 frames after the exact time a limit is met (the
        /// floor's last audible frame, the gate's first silent frame of Rachel, then 0.3 s counted in frames; tools/model_round4.py): each
        /// limit in RobotLineWouldDrop is met this much early (3.5 frames: room for an uneven frame).</summary>
        static float FloorSlack => Mathf.Clamp(3.5f * Time.unscaledDeltaTime, 0.05f, 0.25f);

        bool AlanMainPlaying => alanMain != null && alanMain.busy && alanMain.src && alanMain.src.isPlaying;

        /// <summary>Fix round 3: the real time from which a robot line waiting now could start (RobotFloorFree), from what is known now with
        /// no new line asked: GapSec after the end of every audible voice line (or of the last one, when none is audible), not before the hero
        /// exchange closes, then Rachel's waiting lines one after the other (they go first; fix round 4: the first one not before now, it has
        /// not started yet), each its longest take + GapSec, so "said" is never answered for a line her longer take would push out.
        /// +Infinity: a line the pause holds, or a briefing voice without a known end (ExternalVoiceBusy).</summary>
        float FloorFreeAt(float now)
        {
            float left = OzVoiceFloor.LongestLeft();
            if (float.IsPositiveInfinity(left) || OzVoiceFloor.ExternalBusy()) return float.PositiveInfinity;
            float t = Mathf.Max((left >= 0f ? now + left : now - OzVoiceFloor.QuietFor(now)) + OzVoiceFloor.GapSec, hvExchangeUntil);
            if (rachelQueue.Count > 0) t = Mathf.Max(t, now);                                        // fix round 4: was counted from a time already past
            for (int i = 0; i < rachelQueue.Count; i++) t += LongestTake(rachelQueue[i].d, rachelQueue[i].f) + OzVoiceFloor.GapSec;
            return t;
        }

        // ================================================================================================================ DV118's hold behind Rachel
        /// <summary>Fix round 4, Update once a frame just before ShuttleRound3Tick: while Rachel has a line waiting, a shuttle line DV118
        /// holds behind her stays held (OzShuttleVoiceGate.RachelNextFirst): her waiting line starts before any robot line may. Was: the gate
        /// reads her end one frame after the floor and polls before UpdateChannels, so on a long frame (or when her next line was queued in her
        /// 0.3 s gap) it let the line go in the very frame her next line started; the line met her queue, waited 1 s in the pending slot and
        /// was skipped behind her next line, though the landing test had said "yes" (P held it through her lines, back to back, and said it
        /// 0.3 s after the last). The other half of the cure is in VoiceFloorHoldsRachel (RachelGapDone).</summary>
        void VoiceFloorGateTick() => ShuttleGate.RachelNextFirst = !sweeping && !forceAll && rachelQueue.Count > 0;

        /// <summary>Fix round 4: the real time from which Rachel's head line has waited in the floor's gap after a voice (nothing audible,
        /// less than GapSec since the last voice); -1 = not waiting there.</summary>
        float rachelGapFrom = -1f;

        /// <summary>Fix round 4, VoiceFloorHoldsRachel when her waiting line goes: the time the floor's gap held it back (P started it the
        /// frame the voice before ended), counted from when a shuttle line was held, at most GapSec, is added to that line's 3 s hold
        /// (OzShuttleVoiceGate.ExtendHold), so the hold ends where P's did with her lines back to back. Was: a landing line P said (her time
        /// left + her waiting lines + 0.3 s within 3 s) was dropped "held too long behind Rachel" when that came to about 2.1-2.7 s.</summary>
        void RachelGapDone(float now)
        {
            if (rachelGapFrom >= 0f && ShuttleGate.HeldId != null)
                ShuttleGate.ExtendHold(Mathf.Min(OzVoiceFloor.GapSec, now - Mathf.Max(rachelGapFrom, ShuttleGate.HeldAt)));
            rachelGapFrom = -1f;
        }

        /// <summary>Fix round 4, checks (the 1.4 Play check's scan for voices on sources the floor does not know): the clip names of every
        /// event on a voice channel (rachel, alan_main, alan_alert, alan_end) in the sound table, loaded or not.</summary>
        public static System.Collections.Generic.HashSet<string> VoiceClipNames()
        {
            var o = new System.Collections.Generic.HashSet<string>();
            if (!I) return o;
            foreach (var d in I.defs.Values)
            {
                if (d == null || (d.channel != "rachel" && d.channel != "alan_main" && d.channel != "alan_alert" && d.channel != "alan_end")) continue;
                if (d.loaded != null) foreach (var c in d.loaded) if (c) o.Add(c.name);
                if (d.clips != null) foreach (var c in d.clips) if (c != null && !string.IsNullOrEmpty(c.res)) o.Add(c.res.Substring(c.res.LastIndexOf('/') + 1));
            }
            return o;
        }

        /// <summary>Fix round 3: the longest take of event `d` its line filter `f` lets Pick play (every take when the filter matches none, as
        /// Pick), at its slowest pitch, in seconds; 0 = no take loaded (PlayVoice then plays nothing).</summary>
        static float LongestTake(SoundEventDef d, string f)
        {
            if (d == null || d.loaded == null) return 0f;
            float pitch = Mathf.Max(0.05f, Mathf.Abs(d.pitchMul * (1f - Mathf.Max(0f, d.pitchVar)))), all = 0f, match = -1f;
            for (int i = 0; i < d.loaded.Count; i++)
            {
                var c = d.loaded[i]; if (!c) continue;
                float len = c.length / pitch; all = Mathf.Max(all, len);
                if (!string.IsNullOrEmpty(f) && d.lines != null && i < d.lines.Count && LineMatch(d.lines[i], f)) match = Mathf.Max(match, len);
            }
            return match >= 0f ? match : all;
        }

        /// <summary>Checks: the no-way / lava gate's quiet test now (NoWayQuiet: the speaking channels, the shuttle landing line, the
        /// promotion slot and the floor).</summary>
        public static bool QuietLineMayGoNow => I && I.NoWayQuiet();
        /// <summary>Checks: Rachel's waiting lines.</summary>
        public static int RachelWaiting => I ? I.rachelQueue.Count : 0;
        /// <summary>Checks (fix round 4): the robot line waiting in the pending slot (null = none).</summary>
        public static string RobotLineWaiting => I ? I.pendingAlan : null;

        string FloorWhy(float now)
        {
            if (OzVoiceFloor.AnyHeld()) return "a voice line the pause holds goes on after it";
            if (rachel != null && rachel.src && OzVoiceFloor.Audible(rachel.src)) return "the announcer speaks";
            if ((alanMain != null && OzVoiceFloor.Audible(alanMain.src)) || (alanAlert != null && OzVoiceFloor.Audible(alanAlert.src))) return "a robot line speaks";
            if (OzVoiceFloor.ExternalBusy() || OzVoiceFloor.AnyAudible()) return "a briefing / cutscene voice speaks";
            if (now < hvExchangeUntil) return "Brad answers Alan first";
            if (rachelQueue.Count > 0) return "the announcer's next line goes first";
            return "0.3 s after a voice line";
        }

        /// <summary>SayImpl, just before a robot line plays: true = it waits (or was skipped) instead of starting over another voice.</summary>
        bool VoiceFloorHoldsRobotLine(string id, string filter, SoundEventDef d)
        {
            if (floorRelease || sweeping || forceAll || d == null) return false;
            if (!RobotFloorBusy()) return false;
            float now = Time.unscaledTime;
            if (id == "vo_promoted")
            {
                if (pendingPromo != id) { pendingPromo = id; pendingPromoFilter = filter; pendingPromoAt = now; FloorHeld++; OzSoundLog.Add("dir", id, null, "waits (" + FloorWhy(now) + ")"); }
                return true;
            }
            bool end = d.channel == "alan_end", call = d.channel == "alan_alert";
            int rank = end ? 2 : call ? 1 : 0;
            if (pendingAlan != null)
            {
                if (pendingAlan == id && pendingAlanFilter == filter) return true;                 // asked again while it waits: keeps its place and its clock
                if (rank < pendingAlanRank) { FloorSkipped++; OzSoundLog.Add("dir", id, null, "skipped (a more important robot line waits for the floor)"); return true; }
                FloorSkipped++; OzSoundLog.Add("dir", pendingAlan, null, "skipped (a newer robot line waits for the floor)");
            }
            pendingAlan = id; pendingAlanFilter = filter; pendingAlanAt = now; pendingAlanRank = rank; pendingBypass = shuttleReleasing;
            pendingAlanWait = end ? OzVoiceFloor.EndLineWait : call ? OzVoiceFloor.RobotCallWait : OzVoiceFloor.UnitReplyWait;
            FloorHeld++; OzVoiceFloor.NoteWait(id, now, pendingAlanWait); OzSoundLog.Add("dir", id, null, "waits (" + FloorWhy(now) + ")");
            return true;
        }

        /// <summary>TickPendingAlan: true = the waiting robot line stays waiting (or was skipped now); false = it may start.</summary>
        bool VoiceFloorPendingWaits()
        {
            float now = Time.unscaledTime;
            if (now - pendingAlanAt > pendingAlanWait)
            {
                FloorSkipped++; OzSoundLog.Add("dir", pendingAlan, null, "skipped (waited " + pendingAlanWait.ToString("0.0") + " s: another voice spoke)");
                pendingAlan = null; return true;
            }
            return !RobotFloorFree(now);
        }

        /// <summary>TickPendingAlan: the waiting robot line starts now (its own rules are asked again; the floor was just found free).</summary>
        void VoiceFloorRelease(string id, string f)
        {
            FloorReleased++;
            float t0 = Time.unscaledTime;
            bool keep = shuttleReleasing; shuttleReleasing = pendingBypass; floorRelease = true;
            try { SayImpl(id, f); } finally { floorRelease = false; shuttleReleasing = keep; pendingBypass = false; }
            if (lastLine.TryGetValue(id, out var lt) && lt >= t0) OzVoiceFloor.NoteRelease(id, t0);       // it really started (the Play check measures it)
        }

        /// <summary>The promotion line's slot may go: Alan is free and the floor too (TickPendingAlan).</summary>
        bool PromoFloorFree => RobotFloorFree(Time.unscaledTime);

        /// <summary>The briefing / cutscene flag alone (ExternalVoiceBusy without an audible source: Zod's preroll, a stuck flag) holds a
        /// Rachel line at most this long, as before (UpdateChannels' 12 s); an audible voice holds her as long as it speaks.</summary>
        public const float ExternalFlagWaitMax = 12f;

        /// <summary>UpdateChannels, before Rachel's next line (asked at `askedAt`): true = it waits (a voice is audible or held by the pause,
        /// a briefing voice speaks, a voice ended less than GapSec ago, or Brad still answers Alan). Her queue's own stale rule decides when a
        /// waiting line is too old.</summary>
        bool VoiceFloorHoldsRachel(float now, SoundEventDef head, float askedAt)
        {
            if (sweeping || forceAll) return false;
            bool busy = OzVoiceFloor.QuietFor(now) < OzVoiceFloor.GapSec                                // a voice audible (or held), or ended less than GapSec ago
                        || (OzVoiceFloor.ExternalBusy() && now - askedAt < ExternalFlagWaitMax)
                        || now < hvExchangeUntil;                                                        // one exchange: Alan's boarding line, then Brad's answer
            if (!busy) { floorRachelHead = null; RachelGapDone(now); return false; }
            if (head != null && floorRachelHead != head) { floorRachelHead = head; FloorRachelWaited++; OzSoundLog.Add("dir", head.id, null, "waits (" + FloorWhy(now) + ")"); }
            if (rachelGapFrom < 0f && !OzVoiceFloor.AnyAudible() && !OzVoiceFloor.ExternalBusy() && now >= hvExchangeUntil) rachelGapFrom = now;   // fix round 4: held by the gap alone
            return true;
        }

        /// <summary>PlayVoice: the safety net for every path: no line starts while another voice line is audible (or held by the pause) on a
        /// known source (true = refused). The coverage sweeps and the proofs that pick a clip (forcePick) play their lines one by one themselves.</summary>
        bool VoiceFloorGuard(SoundEventDef d, Voice ch)
        {
            if (sweeping || forceAll || forcePick >= 0) return false;
            if (!OzVoiceFloor.AnyAudible()) return false;
            FloorGuardBlocked++;
            OzSoundLog.Add("VO", d.id, null, "DROP voice floor (" + FloorWhy(Time.unscaledTime) + ")", 0f);
            return true;
        }

        /// <summary>AnnounceImpl: the new line (added last) moves up past lines of a lower priority only, so two lines of one rank keep the
        /// order they were asked in (List.Sort is not stable: "Radar lost." could jump before "Territory lost."). It never passes a "Radar
        /// lost." held at the head while its "Territory lost." speaks.</summary>
        void QueueSortStable()
        {
            int stop = RadarPairPinnedHead ? 1 : 0;
            for (int i = rachelQueue.Count - 1; i > stop && rachelQueue[i].d.rank < rachelQueue[i - 1].d.rank; i--)
            { var tmp = rachelQueue[i]; rachelQueue[i] = rachelQueue[i - 1]; rachelQueue[i - 1] = tmp; }
        }

        /// <summary>AnnounceImpl: at most 3 lines wait, as before (the lowest priority, newest goes); "Territory lost." with its "Radar lost."
        /// right behind it count as one, so a busy moment drops the pair together or keeps it together, never one half.</summary>
        void QueueTrim()
        {
            while (rachelQueue.Count > 3 + (RadarPairQueued() ? 1 : 0))
            {
                var x = rachelQueue[rachelQueue.Count - 1]; rachelQueue.RemoveAt(rachelQueue.Count - 1);
                if (x.d.id == RadarLostLine) { RadarLostNot++; RadarLostWhy = "Rachel's queue was full"; OzSoundLog.Add("dir", RadarLostLine, null, "not said: Rachel's queue was full (3 lines of a higher priority wait)"); }
            }
        }

        // ================================================================================================================ the hero exchange
        /// <summary>Fix round 1: Brad's answer keeps the floor until this real time (Alan's boarding line + HeroAnswerGap + its own wait), so
        /// neither Rachel nor another robot line takes the gap between the two halves of one exchange; -1 = no exchange open.</summary>
        float hvExchangeUntil = -1f;
        float hvAnswerSince = -1f; bool hvAnswerWaiting;

        // ================================================================================================================ "said" when it starts
        // Fix round 1: callers that count a line as said only when it starts at once (HvSay, EvilZod's out line, the kill quip, the repair
        // lines) now also follow a line that went to wait for the floor: their bookkeeping runs when it really starts, and nothing asks it
        // again meanwhile (EvilZod's out line retried every 0.5 s for 6 s: a retry after the waited line played would say it twice).
        readonly System.Collections.Generic.HashSet<string> floorWatchKeys = new System.Collections.Generic.HashSet<string>();

        /// <summary>A robot line of event `id`, take `line`, asked at t0, waits in the pending slot: true = followed (`started` runs when it
        /// really starts, `skipped` when it is skipped or replaced); false = it is not waiting (dropped by its own rules).</summary>
        bool FloorWatch(string id, string line, float t0, System.Action started, System.Action skipped = null)
        {
            string f = "=" + line;
            if (line == null || pendingAlan != id || pendingAlanFilter != f) return false;
            string key = id + "|" + line;
            if (!floorWatchKeys.Add(key)) return true;                                             // already followed (asked again while it waits)
            StartCoroutine(FloorWatchRun(id, line, f, t0, key, started, skipped));
            return true;
        }

        System.Collections.IEnumerator FloorWatchRun(string id, string line, string f, float t0, string key, System.Action started, System.Action skipped)
        {
            while (pendingAlan == id && pendingAlanFilter == f) yield return null;
            floorWatchKeys.Remove(key);
            if (lastLine.TryGetValue(id, out var lt) && lt >= t0 && ChannelPlays(id, line)) started?.Invoke(); else skipped?.Invoke();
        }

        /// <summary>Alan's main or call-out channel plays event `id`, take `line`, now.</summary>
        bool ChannelPlays(string id, string line)
        {
            foreach (var ch in new[] { alanMain, alanAlert })
            {
                if (ch == null || !ch.busy || !ch.src || !ch.src.isPlaying || ch.def == null || ch.def.id != id) continue;
                var d = ch.def;
                if (d.lines != null && d.lastIdx >= 0 && d.lastIdx < d.lines.Count && d.lines[d.lastIdx] == line) return true;
            }
            return false;
        }

        /// <summary>HvSay: the hero line waits for the floor: when it really starts it is counted (HvStarted: the no-repeat bag moves on) and
        /// `started` runs (HvBoarded: Brad's answer is scheduled from that start). Skipped or replaced: nothing.</summary>
        void HvWatchWaiting(string id, string line, int cat, float t0, System.Action started) =>
            FloorWatch(id, line, t0, () => { HvStarted(id, line, cat); started?.Invoke(); });

        /// <summary>EzOutTry (Voice/AudioDirector.EvilZod.cs): his out line waits for the floor: no retry while it waits; counted when it
        /// starts; its retries (every EzOutRetry s up to EzOutWait s) go on only when it was skipped.</summary>
        bool EzOutWaits(ZObject a, float waited, float t0) =>
            FloorWatch(EzSelectId, EzOutLine, t0, () => { EzOutSaid++; OzSoundLog.Add("VO", EzSelectId, EzOutLine, "EVILZOD OUT (DV165), after waiting for the floor"); },
                       () => { if (waited + EzOutRetry <= EzOutWait) Schedule(EzOutRetry, () => EzOutTry(a, waited + EzOutRetry)); });

        /// <summary>KillQuip (Voice/AudioDirector.KillLines.cs): the quip waits for the floor: when it starts, the lines KillQuip runs for a
        /// quip said (unchanged: the bag takes the line, so the next kill gets the next one); skipped: not said.</summary>
        bool KillQuipWaits(string line) =>
            FloorWatch(KillQuipEvent, line, Time.unscaledTime,
                       () => { QuipSaid++; quipBag.Take(line); if (line == SmokinLine) Smokins++; QuipLines.Add(line); if (QuipLines.Count > 32) QuipLines.RemoveAt(0); },
                       () => QuipNotSaid++);

        /// <summary>RvSay (Voice/AudioDirector.RepairLines.cs): the repair line waits for the floor: true = it waits (nobody asks again); when
        /// it starts, the lines RvSay runs for a line that started (unchanged). false = not waiting (as before).</summary>
        bool RvSayWaits(ZObject u, string id, string line, float t0) =>
            FloorWatch(id, line, t0, () =>
            {
                OzRepairLines.Commit(line);
                RepairLastEvent = id; RepairLastLine = line; RepairLastUnit = u != null ? u.Id : -1; RepairLastAt = Time.unscaledTime;
                RepairLastClip = alanMain.src && alanMain.src.clip ? alanMain.src.clip.name : null;
                if (unitVoicesLog && rvFirst.Add(id))
                    Debug.Log($"[repair_lines] first {id}: \"{line}\" (playing after waiting for the voice floor, {(u != null ? u.Type + "#" + u.Id : "?")}; {RvLines(id).Count} lines in the pool)");
            });

        /// <summary>HvBoarded, once Alan's boarding line started (at once, or after it waited for the floor): the lines that were in
        /// HvBoarded (Voice/AudioDirector.HeroVoices.cs:164-169), unchanged, plus the floor kept for Brad's answer.</summary>
        void HvBoardAnswer(bool bradAnswers, string answerId)
        {
            hvBoardSaid = true; hvExchangeLeft = Mathf.Max(0f, alanMainClipLeft());
            if (bradAnswers && UnityEngine.Random.value < HeroAnswerChance)
            {
                float wait = (alanMainClipLeft() + HeroAnswerGap) * Mathf.Max(0.01f, Time.timeScale);   // Schedule runs on game time
                Schedule(wait, () => HvAnswer(answerId));
                hvExchangeLeft += HeroAnswerGap + HeroAnswerLen;
                hvExchangeUntil = Time.unscaledTime + alanMainClipLeft() + HeroAnswerGap + OzVoiceFloor.UnitReplyWait + 0.1f;
            }
        }

        /// <summary>HvAnswer: the floor is taken (a voice audible or held, a briefing voice, or a voice ended under GapSec ago).</summary>
        bool HvAnswerMustWait() => !sweeping && !forceAll && OzVoiceFloor.Busy(Time.unscaledTime);

        /// <summary>HvAnswer met a taken floor: true = the answer waits (at most UnitReplyWait s from the first time it had to wait) and is
        /// asked again the moment the floor is free; false = it waited too long (skipped).</summary>
        bool HvAnswerWaits(string id)
        {
            if (sweeping || forceAll) return false;
            float now = Time.unscaledTime;
            if (hvAnswerSince < 0f) { hvAnswerSince = now; FloorHeld++; OzVoiceFloor.NoteWait(id, now, OzVoiceFloor.UnitReplyWait); OzSoundLog.Add("dir", id, null, "waits (" + FloorWhy(now) + ")"); }
            if (now - hvAnswerSince > OzVoiceFloor.UnitReplyWait) { hvAnswerSince = -1f; FloorSkipped++; return false; }
            if (!hvAnswerWaiting) { hvAnswerWaiting = true; StartCoroutine(HvAnswerLater(id)); }
            return true;
        }

        System.Collections.IEnumerator HvAnswerLater(string id)
        {
            while (true)
            {
                yield return null;
                float now = Time.unscaledTime;
                if (hvAnswerSince < 0f) { hvAnswerWaiting = false; yield break; }
                if (now - hvAnswerSince > OzVoiceFloor.UnitReplyWait)
                {
                    hvAnswerWaiting = false; hvAnswerSince = -1f; hvExchangeUntil = -1f; FloorSkipped++;
                    OzSoundLog.Add("dir", id, null, "skipped (waited " + OzVoiceFloor.UnitReplyWait.ToString("0.0") + " s: another voice spoke)");
                    yield break;
                }
                if (!AlanBusy && !ShuttleRachelLanding && !OzVoiceFloor.Busy(now)) { hvAnswerWaiting = false; HvAnswer(id); yield break; }
            }
        }

        /// <summary>HvAnswer, once the answer started (played) or was given up: the exchange no longer keeps the floor (a waiting
        /// HvAnswerLater sees hvAnswerSince -1 and ends).</summary>
        void HvExchangeDone(string id, bool played)
        {
            hvExchangeUntil = -1f;
            if (played && hvAnswerSince >= 0f) { FloorReleased++; OzVoiceFloor.NoteRelease(id, Time.unscaledTime); OzSoundLog.Add("dir", id, null, "said after waiting for the floor"); }
            hvAnswerSince = -1f;
        }

        // ================================================================================================================ radar lost
        public const string RadarLostLine = "ann_radar_lost", TerritoryLostLine = "ann_territory_lost";
        /// <summary>Game seconds after the zone-lost event when "Radar lost." goes alone if no "Territory lost." was queued. FlagView asks
        /// for that one dropTime 0.34 + 0.35 s after the flag turns, up to its voiceGap 1.6 s later behind another flag line (FlagView.cs:88,
        /// :782, :97, :952): 2.29 s at most for one flag; 2.6 s leaves room. A later one still goes in front of the waiting lone line.</summary>
        public const float RadarLostAlone = 2.6f;
        /// <summary>Real seconds in which the paired "Radar lost." may pass AlertGate's 7 s gap, and how long it is kept in Rachel's queue
        /// after "Territory lost." started (her 4 s stale rule does not drop it while it waits behind its partner).</summary>
        public const float RadarPairWindow = 8f;
        /// <summary>Real seconds in which this event's "Territory lost." may pass AlertGate's 7 s gap (its 0.69-2.3 s arrival + Rachel's 4 s).</summary>
        public const float TerritoryPairWindow = 12f;
        /// <summary>Checks: "Radar lost." queued (after "Territory lost." / alone), not said (and the last reason); held right behind its partner.</summary>
        public static int RadarLostPaired, RadarLostAloneQueued, RadarLostNot, RadarLostPinned;
        public static string RadarLostWhy = "none yet";
        float radarLostUntil = -1f, territoryLostQueuedAt = -100f, radarPinnedAt = -1f;
        bool radarLostLone;

        bool TerritoryLostSpeaking => rachel != null && rachel.busy && rachel.src && rachel.src.isPlaying && rachel.def != null && rachel.def.id == TerritoryLostLine;

        /// <summary>"Radar lost." sits at the head of Rachel's queue, held there while (or since) its "Territory lost." speaks.</summary>
        bool RadarPairPinnedHead => radarPinnedAt >= 0f && rachelQueue.Count > 0 && rachelQueue[0].d.id == RadarLostLine;

        /// <summary>Rachel's stale rule (UpdateChannels): the paired "Radar lost." is kept while its partner speaks and up to RadarPairWindow
        /// s after the partner started (its age counts from then, not from when the pair was queued).</summary>
        bool RadarPairKeeps(SoundEventDef d, float now) => d != null && d.id == RadarLostLine && radarPinnedAt >= 0f && now - radarPinnedAt <= RadarPairWindow;

        /// <summary>"Territory lost." with "Radar lost." right behind it are both waiting in Rachel's queue.</summary>
        bool RadarPairQueued()
        {
            for (int i = 1; i < rachelQueue.Count; i++) if (rachelQueue[i].d.id == RadarLostLine && rachelQueue[i - 1].d.id == TerritoryLostLine) return true;
            return false;
        }

        /// <summary>VoiceFloorTick: once "Territory lost." speaks, the waiting "Radar lost." moves to the head of Rachel's queue, its age
        /// starts again and its AlertGate pass is renewed, so it follows its partner 0.3 s after it ends (fix round 1: it was dropped by the
        /// 4 s stale rule when the pair had waited behind another voice first).</summary>
        void RadarPairTick(float now)
        {
            if (radarPinnedAt >= 0f)
            {
                bool still = false; for (int i = 0; i < rachelQueue.Count; i++) if (rachelQueue[i].d.id == RadarLostLine) { still = true; break; }
                if (!still) radarPinnedAt = -1f;                                                    // said (or dropped): the pair is over
            }
            if (radarPinnedAt >= 0f || !TerritoryLostSpeaking) return;
            for (int i = 0; i < rachelQueue.Count; i++)
            {
                if (rachelQueue[i].d.id != RadarLostLine) continue;
                var q = rachelQueue[i]; q.t = now; rachelQueue.RemoveAt(i); rachelQueue.Insert(0, q);
                radarPinnedAt = now; RadarLostPinned++;
                AlertGate.PairNext(RadarLostLine, RadarPairWindow);
                OzSoundLog.Add("dir", RadarLostLine, null, "next: right after Territory lost. (held at the head of the queue)");
                return;
            }
        }

        /// <summary>OnFlag: one of our zones with a radar (FlagCaptured.flag) was taken: is "Radar lost." true (OzRadarTruth)?</summary>
        void RadarLostAsk(ZEvent e)
        {
            bool due = OzRadarTruth.RadarLostTrue(W, e.objId, Player, OzRadarLine.Rule, out string why);
            RadarLostWhy = why;
            OzSoundLog.Add("dir", RadarLostLine, null, (due ? "due: " : "not said: ") + why);
            Debug.Log($"[OpenZ] radar lost line: {(due ? "due" : "none")} (flag {e.objId}: {why})");
            if (!due) { RadarLostNot++; return; }
            AlertGate.PairNext(TerritoryLostLine, TerritoryPairWindow);                            // fix round 1: this event's "Territory lost." passes the 7 s gap too
            radarLostUntil = Time.time + RadarLostAlone + 0.5f;
            if (Time.time - territoryLostQueuedAt < 0.05f || TerritoryLostSpeaking) { RadarLostQueue(true); return; }   // "Territory lost." is already asked
            Schedule(RadarLostAlone, () => { if (radarLostUntil > 0f) RadarLostQueue(false); });
        }

        /// <summary>AnnounceImpl: a line was queued; "Territory lost." takes a due "Radar lost." right behind it, or goes in front of a lone
        /// "Radar lost." that still waits (it came after RadarLostAlone).</summary>
        void RadarLostAfter(string id)
        {
            if (id != TerritoryLostLine) return;
            territoryLostQueuedAt = Time.time;
            if (radarLostUntil > 0f && Time.time <= radarLostUntil) { RadarLostQueue(true); return; }
            if (!radarLostLone) return;
            int tl = -1, rl = -1;
            for (int i = 0; i < rachelQueue.Count; i++) { if (rachelQueue[i].d.id == TerritoryLostLine) tl = i; else if (rachelQueue[i].d.id == RadarLostLine && rl < 0) rl = i; }
            if (tl < 0 || rl < 0 || tl < rl || (rl == 0 && RadarPairPinnedHead)) return;
            var t = rachelQueue[tl]; rachelQueue.RemoveAt(tl); rachelQueue.Insert(rl, t);
            radarLostLone = false; RadarLostAloneQueued--; RadarLostPaired++;
            AlertGate.PairNext(RadarLostLine, RadarPairWindow);
            OzSoundLog.Add("dir", RadarLostLine, null, "paired: a late Territory lost. goes in front of it");
        }

        void RadarLostQueue(bool paired)
        {
            radarLostUntil = -1f;
            OzRadarLine.Rule.Observe(W);
            if (OzRadarLine.Rule.Covered(Player))
            {   // the picture came back meanwhile (a radar retaken): nothing is lost
                RadarLostNot++; RadarLostWhy = "a radar of ours stands again"; OzSoundLog.Add("dir", RadarLostLine, null, "not said: a radar of ours stands again"); return;
            }
            AlertGate.PairNext(RadarLostLine, RadarPairWindow);
            bool queued = AnnounceImpl(RadarLostLine);
            radarLostLone = queued && !paired;
            if (paired) RadarLostPaired++; else RadarLostAloneQueued++;
            OzSoundLog.Add("dir", RadarLostLine, null, (queued ? "queued " : "not queued (its own rules: kind gap, cooldown, chatter) ") + (paired ? "right after Territory lost." : "alone (no Territory lost. came)"));
        }
    }
}
