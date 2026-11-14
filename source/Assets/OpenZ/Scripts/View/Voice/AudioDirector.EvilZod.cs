using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    // ============================================================================================================================
    // DV161 EvilZod's sounds and lines (Sim/ZEvilZod.cs). The sim only sends events; everything here is view code, and nothing here runs
    // unless he stands on the field.
    //  - shot: one random take of the shuffled bag per shot, the reload on the sim's own moment, the shell after it. A dash shot is 1.5 dB up.
    //    Row 75 / 81: every shot (the dash shot too) EzShotOnCamDb up while he is on camera (3 dB since row 81); off camera as before.
    //    Row 82: the reload is the rack's take only (in step with the slide), 4 dB up in the sound table; one shell-in (EzShellId) per shot, EzShellAt after it, on the rules'
    //    clock (as the shell after the shot), when OzEvilZodBody's thumb pushes it into the port.
    //  - dash: servo at the windup, kick at the start point, whoosh at the middle of the line (it stays in view on a long dash), metal stop
    //    and ground landing at the end point, then the plant, then the shot. No loop, no following source: any dash length works.
    //  - walk: the surface step of every robot, plus a heavy stomp on each step and armour on every second one. His slow walk outlasts the
    //    window in which an order's steps are heard, so his steps always count.
    //  - voice: his own lines on the robot channel. He is Blue and the human is often not, so his lines reach the human on three ways (EzHeard): his own side, the Unit
    //    editor (the human orders both teams there), and on screen as the other side's hero. They are finished takes: the radio filter of that channel is off while one
    //    speaks. The robot voice's grunts, cries and quips never come out of him.
    // ============================================================================================================================
    public partial class AudioDirector
    {
        public const string EzSelectId = "vo_select_hero_evilzod", EzMoveId = "vo_order_hero_evilzod_move", EzAttackId = "vo_order_hero_evilzod_attack", EzDashId = "vo_order_hero_evilzod_dash",
            EzKillId = "vo_hero_evilzod_kill", EzLowId = "vo_hero_evilzod_low", EzBoardId = "vo_hero_evilzod_board",
            EzShotId = "wpn_evilzod_shot", EzReloadId = "wpn_evilzod_reload", EzShellId = "wpn_evilzod_shell", EzKickId = "wpn_evilzod_dash_kick", EzWhooshId = "wpn_evilzod_dash_whoosh", EzStopId = "wpn_evilzod_dash_stop", EzSkidId = "wpn_evilzod_dash_skid",
            EzLandSandId = "wpn_evilzod_dash_land_sand", EzLandHardId = "wpn_evilzod_dash_land_hard", EzStompId = "rob_evilzod_stomp", EzArmourId = "rob_evilzod_armour";
        /// <summary>The share of dashes with a line (row cooldown 20 s), the dash shot's gain, the shell after a shot (s), his step on the ground (m: half the baked walk cycle of 1.329 m on the rig x his model scale 1.85).</summary>
        public const float EzDashLineChance = 0.6f, EzDashShotGain = 1.19f, EzCasingsAfter = 0.45f, EzStride = 1.23f;
        /// <summary>The rules stand before the drawn body does: the stop and the landing wait for it (game seconds; the skid is OzEvilZodFx's, at the brake's start).</summary>
        public const float EzStopAfter = 0.24f;
        /// <summary>
        /// (EzShotCamUp over his ground point) is on screen, by OzShakeGate.OnScreen, the test of his idle sounds; off screen as before. Read live, 0 = as before,
        /// held to 0..EzShotOnCamDbMax. It rides on the shot's own gain (and the dash shot's x EzDashShotGain): the buses, the ducks, the zoom gain and the master
        /// compressor's -1 dBFS ceiling come after it unchanged.</summary>
        public static float EzShotOnCamDb = 3f;
        /// <summary>Row 82: the shell-in after every shot (game seconds after the shot's event): OzEvilZodBody's thumb pushes the shell into the port then.</summary>
        public const float EzShellAt = 1.46f;
        public const float EzShotOnCamDbMax = 3f, EzShotCamUp = 2.4f;
        /// <summary>Checks: the last shot's step (dB, 0 off camera) and whether he was on camera. The sound log has the same as key EzShotGainKey.</summary>
        public static float EzShotLastDb; public static bool EzShotLastOnCam;
        public const string EzShotGainKey = "ez_shot_gain";
        /// <summary>How long an attack order waits for the sim's dash before it speaks after all (s).</summary>
        public const float EzDashLineWait = 0.3f;

        /// <summary>This unit is EvilZod.</summary>
        public static bool EzIs(ZObject o) => o != null && o.IsEvilZod;

        /// <summary>His lines reach the human: his own side, the Unit editor where the human orders both teams, or on screen as the other side's hero.</summary>
        bool EzHeard(ZObject o) => o != null && (o.Owner == Player || OzUeHooks.Active || OnScreen(PosOf(o), 0.2f));

        // ------------------------------------------------------------------ the radio filter
        bool ezFilterOff;
        /// <summary>Checks: the robot channel's radio filter is off for his line.</summary>
        public static bool EzFilterOff => I && I.ezFilterOff;

        /// <summary>PlayVoice: his takes carry the voice's own chain, as in his briefings: the channel's radio filter steps aside for them and comes back for the next other line.</summary>
        void EzVoiceFilter(Voice ch, SoundEventDef d)
        {
            if (ch != alanMain || !alanRadioFilter) return;
            bool his = d.id != null && d.id.Contains("hero_evilzod");
            if (his == ezFilterOff) return;
            ezFilterOff = his;
            var go = ch.src.gameObject;
            var hp = go.GetComponent<AudioHighPassFilter>(); if (hp) hp.enabled = !his;
            var ds = go.GetComponent<AudioDistortionFilter>(); if (ds) ds.enabled = !his;
            var cf = go.GetComponent<AudioChorusFilter>(); if (cf) cf.enabled = !his;
        }

        /// <summary>PlayVoice: his attack and dash lines leave the combat buses alone: the full damage shot lands during them.</summary>
        static bool EzNoDuck(SoundEventDef d) => d != null && (d.id == EzDashId || d.id == EzAttackId);

        // ------------------------------------------------------------------ sim events
        void EzEvent(ZEvent e, ZObject a, ZObject b)
        {
            if (a == null) return;
            switch (e.type)
            {
                case ZEventType.EvilDashStart:
                {
                    Play("rob_servo", PosOf(a) + Vector3.up, a.Id);
                    bool owed = ezDashOwed == a.Id; ezDashOwed = -1;                                // the attack order kept quiet for this line
                    if (EzHeard(a) && (owed || UnityEngine.Random.value < EzDashLineChance)) HvSay(EzDashId, 5);
                    break;
                }
                case ZEventType.EvilDashGo:
                    Play(EzKickId, PosOf(a), a.Id);
                    Play(EzWhooshId, Px((a.EvilDashFromX + e.x) / 2, (a.EvilDashFromY + e.y) / 2), a.Id);
                    break;
                case ZEventType.EvilDashEnd:
                {
                    var at = Px(e.x, e.y); int id = a.Id;
                    Schedule(EzStopAfter / Mathf.Max(0.25f, ZGame.I ? ZGame.I.gameSpeed : 1f), () => { Play(EzStopId, at, id); Play(City || ClassAt(at) == ZClass.Road ? EzLandHardId : EzLandSandId, at, id); });
                    break;
                }
                case ZEventType.EvilGuardOut: EzGuardOut(a); break;                           // DV165: out of his fort as its last defense
                case ZEventType.EvilShot: EzShot(e, a, b); break;
                case ZEventType.EvilReload:
                    if (!a.Destroyed) Play(EzReloadId, PosOf(a), a.Id);
                    break;
            }
        }

        void EzShot(ZEvent e, ZObject a, ZObject t)
        {
            bool onCam = OzShakeGate.OnScreen(PosOf(a) + Vector3.up * EzShotCamUp);                    // row 75: a little louder on camera
            float step = onCam ? Mathf.Clamp(EzShotOnCamDb, 0f, EzShotOnCamDbMax) : 0f;
            EzShotLastOnCam = onCam; EzShotLastDb = step;
            bool played = Play(EzShotId, PosOf(a), a.Id, (e.flag ? EzDashShotGain : 1f) * Db(step));
            OzSoundLog.Add("AD", EzShotGainKey, null, (onCam ? "ON camera " : "OFF camera ") + step.ToString("+0.0;-0.0;0.0") + " dB" + (e.flag ? " dash" : "") + (played ? "" : " (shot not played)"), 0f, PosOf(a));
            Schedule(EzCasingsAfter, () => { if (!a.Destroyed) Play("wpn_casings", PosOf(a), a.Id); });
            Schedule(EzShellAt / Mathf.Max(0.25f, ZGame.I ? ZGame.I.gameSpeed : 1f), () => { if (!a.Destroyed) Play(EzShellId, PosOf(a) + Vector3.up * 1.5f, a.Id); });   // row 82: the shell-in, on every reload
            if (t == null) return;
            if (t.Kind == ZKind.Robot && !e.flag2) HitGrunt(t);
            UnderAttack(a, t, true);
        }

        // ------------------------------------------------------------------ steps
        int ezSteps;
        /// <summary>UpdateRobots, after the surface step: the stomp, and the armour on every second step.</summary>
        void EzStep(ZObject o, Vector3 pos, float g)
        {
            if (!EzIs(o)) return;
            Play(EzStompId, pos, o.Id, g);
            if ((++ezSteps & 1) == 0) Play(EzArmourId, pos + Vector3.up, o.Id, g);
        }

        // ------------------------------------------------------------------ lines
        /// <summary>UvSelect hook: his own line. True = handled.</summary>
        bool EzSelect(ZObject u)
        {
            if (!EzIs(u) || !HvHas(EzSelectId)) return false;
            if (AlanMainSpeaking) return true;                                                     // a quick re-click while it speaks stays silent
            HvSay(EzSelectId, 0);
            return true;
        }

        /// <summary>An attack order that the sim will answer with a dash: the dash's own line has the word then.</summary>
        bool EzDashDue(ZObject u)
        {
            var w = u.Waypoints[u.Waypoints.Count - 1];
            if (w.mode != WpMode.Attack || u.EvilDashReadyTick != 0) return false;
            var t = W.Get(w.refId); if (t == null) return false;
            double dx = t.CenterX - u.CenterX, dy = t.CenterY - u.CenterY, d = System.Math.Sqrt(dx * dx + dy * dy);
            return d > ZEvilZod.FullBandPx && d <= ZEvilZod.DashRangePx;
        }

        /// <summary>The dash's line can speak: its row is not cooling.</summary>
        bool EzDashLineFree()
        {
            var d = Def(EzDashId); if (d == null || !HvHas(EzDashId)) return false;
            return !lastLine.TryGetValue(EzDashId, out var lt) || Time.unscaledTime - lt >= RobotChatter.Cooldown(EzDashId, d.cooldown);
        }

        int ezDashOwed = -1;
        /// <summary>An attack order that the sim will answer with a dash keeps quiet for the dash's line. If the rules refuse the dash (no line of sight), the attack line comes after all.</summary>
        void EzHoldForDash(ZObject u)
        {
            int id = u.Id; ezDashOwed = id;
            Schedule(EzDashLineWait, () => { if (ezDashOwed != id) return; ezDashOwed = -1; if (!u.Destroyed) HvSay(EzAttackId, 2); });
        }

        /// <summary>UvOrder hook: his own move or attack line. True = handled, or silent on purpose. He never refuses.</summary>
        bool EzOrder(ZObject u, bool attack, bool noWay)
        {
            if (!EzIs(u) || u.Waypoints.Count == 0) return false;
            bool atk = attack || u.Waypoints[u.Waypoints.Count - 1].mode == WpMode.Attack;
            string id = atk ? EzAttackId : EzMoveId;
            if (!HvHas(id)) return false;
            if (noWay) return true;
            if (AlanMainSpeaking && (alanMainClipLeft() > unitOrderWaitMax || UnitVoices.IsOrderLine(alanMainId))) return true;   // never late, never chained
            if (Time.unscaledTime - hvLastOrder < unitOrderGap) return true;                       // right-click spam: one answer
            hvLastOrder = Time.unscaledTime;
            if (atk && EzDashDue(u) && EzDashLineFree()) EzHoldForDash(u);
            else HvSay(id, atk ? 2 : 1);
            return true;
        }

        /// <summary>OnDeath, before the human's kill logic: his kill for the other side. His own side goes through that logic, as every unit.</summary>
        void EzKillOther(ZObject killer, ZObject o)
        {
            if (EzIs(killer) && killer.Owner != Player && o.Owner != killer.Owner && o.Owner != Team.Neutral && o.IsUnit && EzHeard(killer)) EzKill(killer);
        }

        /// <summary>OnDeath, the human's kill: his own line instead of the robot voice's cheer, quip and taunt. True = the killer was he.</summary>
        bool EzKill(ZObject killer)
        {
            if (!EzIs(killer)) return false;
            if (!EzKillDue()) { OzSoundLog.Add("dir", EzKillId, null, "capped (1.4: one kill line per 30 s of game time, then 1 kill in 3)"); return true; }
            if (HvHas(EzKillId)) HvSay(EzKillId, 6);
            return true;
        }

        // ------------------------------------------------------------------ 1.4 mayhem_headstart: his kill lines capped
        /// <summary>
        /// At most one kill line per EzKillGapGameSec seconds of GAME time (the rules' clock: a pause does not count down), and after that gap
        /// only on every EzKillEvery-th kill (a counter: the view's own pick, never the Sim's random stream); the first kill of a match speaks. The line
        /// is the bag's next (HvNext: never the same twice in a row while the event has 2 or more lines). A kill the cap keeps quiet still has no robot
        /// cheer (EzKill stays true). His once-a-game lines (out of the fort, boarded, low health) keep their own rules.</summary>
        public const float EzKillGapGameSec = 30f; public const int EzKillEvery = 3;
        /// <summary>Checks, this match (a new match starts them at 0): his kills that reached his line, the lines asked for (past the cap), the kills
        /// the cap kept quiet, and the game time (s) of every line asked (the first 256).</summary>
        public static int EzKills, EzKillLinesAsked, EzKillCapped;
        public static readonly System.Collections.Generic.List<double> EzKillAskTimes = new System.Collections.Generic.List<double>(32);
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void EzKillReset() { EzKills = EzKillLinesAsked = EzKillCapped = 0; EzKillAskTimes.Clear(); }
        double ezKillAt = double.NegativeInfinity; int ezKillSkip; object ezKillWorld;
        /// <summary>The cap's rule (plain, the check runs it without Play): true = this kill at game time <paramref name="now"/> (s) gets a line.</summary>
        public static bool EzKillGate(double now, ref double lastAt, ref int skip)
        {
            if (now < lastAt) { lastAt = double.NegativeInfinity; skip = 0; }                     // the clock started again (a new match)
            if (now - lastAt < EzKillGapGameSec) return false;
            if (skip > 0) { skip--; return false; }
            lastAt = now; skip = EzKillEvery - 1; return true;
        }
        bool EzKillDue()
        {
            var w = ZGame.I ? ZGame.I.World : null;
            if (!ReferenceEquals(w, ezKillWorld)) { ezKillWorld = w; ezKillAt = double.NegativeInfinity; ezKillSkip = 0; EzKillReset(); }   // a new match: its first kill speaks
            double now = w != null ? w.Time : Time.timeSinceLevelLoad;
            EzKills++;
            if (!EzKillGate(now, ref ezKillAt, ref ezKillSkip)) { EzKillCapped++; return false; }
            EzKillLinesAsked++; if (EzKillAskTimes.Count < 256) EzKillAskTimes.Add(now);
            return true;
        }

        /// <summary>HealthCheck: he under a quarter of his health.</summary>
        void EzLow() { if (HvHas(EzLowId)) HvSay(EzLowId, 7); }

        /// <summary>EnteredLines, before the human's lines: he got into his own team's shuttle on the other side.</summary>
        void EzBoardedOther(ZObject r, Team before)
        {
            if (EzIs(r) && r.Owner != Player && before == r.Owner && EzHeard(r)) EzBoarded(r);
        }

        /// <summary>EnteredLines: he got into his own team's shuttle. True = it was he. The shuttle's own lines stay.</summary>
        bool EzBoarded(ZObject r)
        {
            if (!EzIs(r)) return false;
            if (HvHas(EzBoardId)) HvSay(EzBoardId, 3);
            return true;
        }

        // ------------------------------------------------------------------ DV165: out of his fort
        /// <summary>DV165 (Sim/ZEvilZod.Appear.cs): he walks out of the blue fort as its last defense, once a game: his approved selected line "Fine, I'll do it myself."
        /// (the select row's take, no new recording), heard wherever the camera is: it happens once and tells the red player who came out. The door, the gate and the
        /// roll-out sound with the UnitBuilt event, as for every unit a fort sends out. A robot line speaking: tried again every EzOutRetry s for EzOutWait s.</summary>
        public const string EzOutLine = "Fine, I'll do it myself.";
        public const float EzOutDelay = 0.6f, EzOutRetry = 0.5f, EzOutWait = 6f;
        /// <summary>Checks: his out line was said.</summary>
        public static int EzOutSaid;
        void EzGuardOut(ZObject a) { if (a != null) Schedule(EzOutDelay, () => EzOutTry(a, 0f)); }
        void EzOutTry(ZObject a, float waited)
        {
            if (a == null || a.Destroyed || !HvHas(EzSelectId)) return;
            float t0 = Time.unscaledTime;
            SayImpl(EzSelectId, "=" + EzOutLine);
            if (lastLine.TryGetValue(EzSelectId, out var lt) && lt >= t0 && alanMainId == EzSelectId) { EzOutSaid++; OzSoundLog.Add("VO", EzSelectId, EzOutLine, "EVILZOD OUT (DV165)"); return; }
            if (EzOutWaits(a, waited, t0)) return;                                                  // 1.4 radar_voice_fix: it waits for the voice floor: no retry meanwhile (a retry after it played would say it twice); counted when it starts
            if (waited + EzOutRetry <= EzOutWait) Schedule(EzOutRetry, () => EzOutTry(a, waited + EzOutRetry));
        }

#if !OZ_RELEASE
        /// <summary>The sound proof (View/Voice/OzEvilZodSoundProof.cs): a line through the game's own path; the channel's filter as it stands.</summary>
        public bool EzProofLine(string id, int cat) => HvSay(id, cat);
        public void EzProofOtherLine(string id) => SayImpl(id, null);
        public bool EzProofHeard(ZObject o) => EzHeard(o);
        public bool EzProofFilterOn { get { var hp = alanMain.src.gameObject.GetComponent<AudioHighPassFilter>(); return hp && hp.enabled; } }
        /// <summary>1.4 mayhem_headstart check: the kill event's next <paramref name="n"/> lines taken as if each started (nothing plays), and its count of
        /// distinct lines. The live bag and last line are put back afterwards, so the match's own next line is not changed by the check.</summary>
        public System.Collections.Generic.List<string> EzProofKillLines(int n)
        {
            var got = new System.Collections.Generic.List<string>(n);
            var keepBag = hvBag.TryGetValue(EzKillId, out var b0) && b0 != null ? new System.Collections.Generic.List<string>(b0) : null;
            bool hadLast = hvLast.TryGetValue(EzKillId, out var last0);
            for (int i = 0; i < n; i++) { var l = HvNext(EzKillId); got.Add(l); if (l != null) { if (hvBag.TryGetValue(EzKillId, out var b)) b.Remove(l); hvLast[EzKillId] = l; } }
            if (keepBag != null) hvBag[EzKillId] = keepBag; else hvBag.Remove(EzKillId);
            if (hadLast) hvLast[EzKillId] = last0; else hvLast.Remove(EzKillId);
            return got;
        }
        public int EzProofKillPool() { var d = Def(EzKillId); var seen = new System.Collections.Generic.HashSet<string>(); if (d?.lines != null) foreach (var s in d.lines) if (!string.IsNullOrEmpty(s)) seen.Add(s); return seen.Count; }
#endif
    }
}
