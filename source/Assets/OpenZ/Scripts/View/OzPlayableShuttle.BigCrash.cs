using System.Collections;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// harder crash and shake, and ten or so new banter lines such as the thirty percent landing and the one about who should drive):
    /// the BIG crash landing's show, on top of the crash landing of View/OzPlayableShuttle.Crash.cs (the same pose, timing, dust, gouges,
    /// scrape mark and plating bits). The rules decide it (ZObject.ShuttleBigCrash, DV154, Sim/ZWorld.ShuttleBigCrash.cs: one crash landing
    /// Today's other crash landings keep today's show unchanged. View only:
    /// <list type="bullet">
    /// <item>MORE SPARKS: at every pad contact (the slam, the other side, the slump) two more spark sprays thrown out sideways and along the
    /// flight (<see cref="BigSparkSprays"/>), and while the pads scrape along the ground a spray from each scraping pad every 0.09 s.</item>
    /// <item>A HARDER CRASH SOUND at the slam, on top of today's wreck_crash + metal crash: a second, deeper take of the crash
    /// (<see cref="BigCrashPitch"/>), the heavy metal crunch (dst_turret_crash) and tearing metal with sparks clattering
    /// (dst_fortgun_break_metal). Existing sounds only; the scrape's sparks have the scrape's own sound.</item>
    /// <item>A STRONGER CAMERA SHAKE through the same shake gate (View/OzCrashLandingShake.cs BigSlamPx / BigBouncePx / BigKickK): the CAMERA
    /// SHAKE option and CALM MODE still turn it off, off screen there is none.</item>
    /// <item>THE BANTER: <see cref="BigBanterAtSec"/> after the slam the ship's crew (Alan or Brad, voices "Alan2" / "Brad 2") says one of
    /// the 11 lines of <see cref="BigCrashLines"/> (the event <see cref="BigCrashBanterId"/>, one clip per line), picked by the ship and its
    /// landing number, never the same line twice in a row (over every ship) and never the two "I told you" lines one right after the
    /// <see cref="BigBanterSameJoke"/>
    /// robots' "Coming in hot!" or Rachel's "Landing ... percent successful.", chosen as for every crash landing today:
    /// View/OzPlayableShuttle.LandingVoice.cs)
    /// and the robot line gap after it, never cuts a line, never plays over Rachel, and keeps every voice setting and line cooldown
    /// (AudioDirector.SayWouldPlay). <see cref="BigCrashRobotsOnly"/> (off) = on a big crash the landing's voice is always the robots'.
    /// Another side's ship (DV138) says nothing. Until the lines are voiced the event is not in the sound table: nothing plays and the log
    /// says so.</item>
    /// </list>
    /// </summary>
    public sealed partial class OzPlayableShuttle
    {
        // ------------------------------------------------------------------ the numbers
        /// <summary>Extra spark sprays per pad contact; the scrape's sprays' size; the deeper crash take's pitch.</summary>
        public static int BigSparkSprays = 2;
        public static float BigScrapeSparkK = 1.1f, BigCrashPitch = 0.72f;
        /// <summary>The banter: s after the slam it is asked, the longest it waits for the other voices (s, real time) and the beat after
        /// them (s); off = no banter (and Rachel may take a big crash's landing line as for every crash).</summary>
        public static float BigBanterAtSec = 1.0f, BigBanterWaitSec = 5f, BigBanterBeatSec = 0.3f;
        public static bool BigBanterOn = true;
        /// <summary>Off (default, today's choice kept): on a big crash the landing's voice is picked as on every crash landing (the robots'
        /// "Coming in hot!" or Rachel's percent line), and the banter follows it. On: a big crash's landing voice is always the robots' (no
        /// Rachel line before the banter).</summary>
        public static bool BigCrashRobotsOnly = false;

        /// <summary>The banter's sound event (one clip per line, channel alan_main like the other shuttle lines; vo_shuttle_* so Rachel
        /// waits for it, View/OzShuttleVoiceGate.cs).</summary>
        public const string BigCrashBanterId = "vo_shuttle_bigcrash";
        /// <summary>The 11 lines, word for word as the event's clip "line" texts (who says each: the kit's SOLUTION.md section 8 and
        /// voice/bigcrash_lines.json). Lines 1 and 11 are the owner's two wordings of the same joke, both kept (30 Oct 2026).</summary>
        public static readonly string[] BigCrashLines =
        {
            "Dang it! I told you I should drive!",
            "Landing thirty percent successful. I'll take it!",
            "My bolts! I think I left my bolts up there!",
            "Totally meant to do that, dude.",
            "Next time, I'm driving. No arguments.",
            "Hey, any landing you can walk away from, right?",
            "Who taught you to fly? A brick?",
            "Bumpy! But cool.",
            "I'm writing that one in the logbook. Twice!",
            "Parked it! More or less.",
            "Dammit! I told you I should drive!",
        };
        /// <summary>The two lines that are one joke in two wordings ("Dang it! / Dammit! I told you I should drive!"): never one right after the other.</summary>
        const int BigBanterDangIt = 0, BigBanterDammit = 10;

        // ------------------------------------------------------------------ read by the checks
        /// <summary>Every big crash drawn, extra spark sprays made, banter lines said, banters left out (another voice the whole wait, a voice
        /// setting, a cooldown, not voiced yet), a banter the same as (or the other wording of) the one before it (0).</summary>
        public static int BigCrashesDrawn, BigCrashSparkSprays, BigCrashBanters, BigCrashBanterDropped, BigCrashBanterRepeats;
        /// <summary>The last banter line picked (index into BigCrashLines, -1 = none), over every ship.</summary>
        public static int BigBanterLast => bigBanterLast;
        /// <summary>The crash being drawn (or the last one) is the big one.</summary>
        public bool CrashBig => crashBig;
        /// <summary>Proof only: the next landing is a big crash (the rules' switch ZWorld.ShuttleBigCrashProofNext; DV154 on).</summary>
        public static bool ProofBigNext { get => ZWorld.ShuttleBigCrashProofNext; set => ZWorld.ShuttleBigCrashProofNext = value; }

        static int bigBanterLast = -1;
        bool crashBig;

        /// <summary>PickLandingVoice: with BigCrashRobotsOnly on (default off), a big crash's landing voice is the robots' (never Rachel's then the banter).</summary>
        bool BigCrashVoiceIsRobots => BigCrashRobotsOnly && BigBanterOn && Obj != null && Obj.ShuttleBigCrash;

        /// <summary>CrashBegin of a big crash.</summary>
        void BigCrashBegin()
        {
            BigCrashesDrawn++;
            OzShuttleTouchdown.Log.AppendLine($"  BIG crash (1.3 item 7, DV154): health {Obj.ShuttleBigCrashHpBefore} -> {Obj.Health} of {Obj.MaxHealth} ({Obj.ShuttleBigCrashLost} lost), {BigSparkSprays} more spark sprays per pad, the harder crash sound, the shake {OzCrashLandingShake.BigSlamPx:F1} / {OzCrashLandingShake.BigBouncePx:F1} px, the banter at +{BigBanterAtSec:F1} s");
        }

        /// <summary>CrashContact of a big crash: two more sprays at the pad, thrown out sideways and along the flight (scale k as the pad's own).</summary>
        void BigCrashContactSparks(Vector3 at, float k)
        {
            var outward = transform.right * crashSide; outward.y = 0f;
            if (outward.sqrMagnitude < 1e-4f) outward = Vector3.right;
            outward.Normalize();
            for (int j = 0; j < BigSparkSprays; j++)
            {
                var spray = j == 0 ? outward : (crashDir + outward * 0.35f).normalized;            // sideways, then along the slide
                if (OpenZHitSparks.Burst(at + Vector3.up * 0.15f, -spray + Vector3.down * 0.6f, FxWeapon.MachineGun, FxSurface.Metal, k, false)) BigCrashSparkSprays++;   // dir = the hit's travel: the spray flies back along -dir
            }
        }

        /// <summary>CrashPoseStep while the pads scrape (every 0.09 s): a spray of sparks from each scraping pad.</summary>
        void BigCrashScrapeSparks()
        {
            var outward = transform.right * crashSide; outward.y = 0f;
            var spray = (outward + crashDir * 0.6f); if (spray.sqrMagnitude < 1e-4f) spray = Vector3.forward;
            spray.Normalize();
            for (int i = 0; i < 4; i++)
            {
                if (PadSoles[i].x * crashSide <= 0f) continue;
                if (OpenZHitSparks.Burst(PadWorld(i, 0.08f), -spray + Vector3.down * 0.5f, FxWeapon.MachineGun, FxSurface.Metal, BigScrapeSparkK, false)) BigCrashSparkSprays++;
            }
        }

        /// <summary>CrashSlam of a big crash: the harder hit on top of today's (existing sounds only).</summary>
        void BigCrashSlamSound(Vector3 at)
        {
            var au = CombatAudio.I;
            if (au) au.PlayPitched("wreck_crash", at, 1f, 0f, Obj.Id + 21, BigCrashPitch, LegacyMatch.BankTankImpact);   // a second, deeper take (the bank never repeats the take just played)
            if (AudioDirector.Has("dst_turret_crash")) AudioDirector.Fire("dst_turret_crash", at, Obj.Id + 23, 1f, 0.8f);   // the heavy metal crunch
            if (AudioDirector.Has("dst_fortgun_break_metal")) AudioDirector.Fire("dst_fortgun_break_metal", at, Obj.Id + 25, 0.8f, 0.95f);   // tearing metal, sparks clattering
            OzShuttleTouchdown.Log.AppendLine("  BIG crash: wreck_crash (deeper take) + dst_turret_crash + dst_fortgun_break_metal at the slam");
        }

        /// <summary>CrashPoseStep, BigBanterAtSec after the slam: the banter is asked (never for another side's ship).</summary>
        void BigCrashBanterBegin()
        {
            if (!BigBanterOn || Obj == null) return;
            if (CpuShipQuiet) { LastVoice = BigCrashBanterId; return; }                              // DV138: noted for the proofs, not said
            StartCoroutine(BigCrashBanter(Obj.ShuttleLandings));
        }

        /// <summary>Lines a and b must not follow each other: the same line, or the two wordings of "... I told you I should drive!".</summary>
        public static bool BigBanterSameJoke(int a, int b) => a >= 0 && b >= 0 && (a == b || (a == BigBanterDangIt && b == BigBanterDammit) || (a == BigBanterDammit && b == BigBanterDangIt));

        /// <summary>The line for this landing: by the ship and its landing number, never the one said last (over every ship), never the other
        /// wording of the one said last.</summary>
        public static int BigBanterPick(int shipId, int landing, int last)
        {
            int n = BigCrashLines.Length;
            int i = Hash(shipId + 131, landing) % n;
            if (BigBanterSameJoke(i, last) && n > 2)
            {
                i = (i + 1 + Hash(shipId + 137, landing) % (n - 1)) % n;                              // 1 .. n-1 places on: never the same
                for (int step = 0; step < n && BigBanterSameJoke(i, last); step++) i = (i + 1) % n;  // landed on the other wording: the next line
            }
            return i;
        }

        /// <summary>The banter: waits for the other voices (the landing's own line, the robots' or Rachel's, and the robot line gap after it), a beat, then one line.</summary>
        IEnumerator BigCrashBanter(int landing)
        {
            float t0 = Time.unscaledTime;
            while (Time.unscaledTime - t0 < BigBanterWaitSec && (AudioDirector.VoiceBusy || !AudioDirector.RobotLineFree)) yield return null;
            float t1 = Time.unscaledTime; while (Time.unscaledTime - t1 < BigBanterBeatSec) yield return null;
            if (BrokenUp || Obj == null || Obj.Destroyed) yield break;                               // destroyed meanwhile: no joke after its death
            string why = null;
            if (BigCrashRobotsOnly && LastLandingVoice == OzLandingVoice.Rachel) why = "Rachel had this landing (the proof switch): no banter after her (BigCrashRobotsOnly)";
            else if (AudioDirector.VoiceBusy || !AudioDirector.RobotLineFree) why = "another voice the whole " + BigBanterWaitSec.ToString("0") + " s";
            else if (!AudioDirector.Has(BigCrashBanterId)) why = "not voiced yet (no " + BigCrashBanterId + " in the sound table)";
            else if (!AudioDirector.SayWouldPlay(BigCrashBanterId)) why = "a voice setting or its cooldown";
            if (why != null)
            {
                BigCrashBanterDropped++;
                OzSoundLog.Add("dir", BigCrashBanterId, null, "big crash banter left out: " + why);
                yield break;
            }
            int k = BigBanterPick(Obj.Id, landing, bigBanterLast);
            if (BigBanterSameJoke(k, bigBanterLast)) BigCrashBanterRepeats++;
            bigBanterLast = k; BigCrashBanters++;
            LastVoice = BigCrashBanterId; LastAlanId = BigCrashBanterId; LastAlanAt = Time.unscaledTime;
            OzSoundLog.Add("dir", BigCrashBanterId, null, $"big crash banter {k + 1} of {BigCrashLines.Length}: \"{BigCrashLines[k]}\" (ship {Obj.Id}, landing {landing})");
            AudioDirector.Say(BigCrashBanterId, "=" + BigCrashLines[k]);                          // "=line": exactly that clip (AudioDirector.LineMatch)
        }
    }
}
