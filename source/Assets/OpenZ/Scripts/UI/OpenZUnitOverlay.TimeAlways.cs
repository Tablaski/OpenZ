using System.Text;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.2 item 71 (a player:
    /// above OzLayout.TagCompactEnter m of camera height an ENEMY badge (CompactEnemy; an own one too with
    /// CompactOwn) turned compact, the medallion only: its progress ring kept running while the plate with the time faded off. Now a badge
    /// whose building is producing keeps its plate and its time at every camera height; the compact medallion is left for the badges
    /// that have no time to show (IDLE, SELECT, PLACE with nothing building). TimeCheck: every badge state x both teams x own / enemy x
    /// the compact switches x camera heights 35..200 m (up and back down, the hysteresis): a ring with a running build => its time.
    /// </summary>
    public partial class OpenZUnitOverlay
    {
        /// <summary>The badge's compact form this frame (DrawBadges): the overview's compact view, the switch for its side, and never
        /// while its building produces (the time stays readable at every height).</summary>
        internal static bool BadgeCompact(bool own, bool compactView, ZObject b) =>
            compactView && (own ? CompactOwn : CompactEnemy) && !(b != null && b.Building && b.CurUnit != null);

        /// <summary>The overview's compact view with its hysteresis (DrawBadges' rule).</summary>
        internal static bool CompactViewAt(bool was, float camH) => was ? camH >= OzLayout.TagCompactExit : camH >= OzLayout.TagCompactEnter;

        /// <summary>The counted check (menu OpenZ/V1.2/Factory Time: 71, edit mode or Play): the badge's own view (OzBadgeState.Build,
        /// as DrawBadges calls it) for each case, the camera swept 35 -> 200 -> 35 m in 1 m steps 0.05 s apart. pass / fail = frames
        /// of a producing building whose ring shows: the time (or READY in the unit-ready moment) in the well. oldMiss = the same
        /// frames under the former rule (compact whenever the view is), where the ring showed without its number.</summary>
        public static string TimeCheck(out int pass, out int fail, out int oldMiss)
        {
            pass = fail = oldMiss = 0;
            int quiet = 0, cases = 0;
            var bad = new StringBuilder();
            bool co0 = CompactOwn, ce0 = CompactEnemy; var f0 = OzBadgeState.Force; bool ev0 = OzBadgeState.UseEventFeed;
            string[] states = { "producing", "producing, unit limit (PAUSED)", "producing + a stored gun (PLACE)", "unit ready (READY)", "a stored gun only (PLACE)", "idle (SELECT / IDLE)" };
            double[] lefts = { 0.3, 1, 9, 59, 61, 133, 599.4, 600, 3599, 3661 };
            string[] types = { "robot_factory", "vehicle_factory", "fort_front" };
            try
            {
                OzBadgeState.Force = OzBadgeForce.None; OzBadgeState.UseEventFeed = true;
                var w = new ZWorld { Time = 5000 };
                int id = 900000;
                for (int sw = 0; sw < 4; sw++)
                    for (int own = 0; own < 2; own++)
                        for (int tm = 0; tm < 2; tm++)
                            for (int s = 0; s < states.Length; s++)
                                for (int li = 0; li < (s < 4 ? lefts.Length : 1); li++)
                                {
                                    CompactOwn = (sw & 1) != 0; CompactEnemy = (sw & 2) != 0;
                                    bool isOwn = own == 0, producing = s < 4, paused = s == 1, ready = s == 3;
                                    string type = types[(li + s) % types.Length];
                                    var b = new ZObject { Id = ++id, Owner = tm == 0 ? Team.Red : Team.Blue, Kind = ZKind.Building, Type = type, Level = (li + s) % 6, Health = 100, MaxHealth = 100, World = w };
                                    if (producing) { b.Building = true; b.CurUnit = type == "vehicle_factory" ? "jeep" : "grunt"; b.BFinal = w.Time + lefts[li]; b.BInit = b.BFinal - System.Math.Max(lefts[li] + 20, 140); }
                                    if (s == 2 || s == 4) b.StoredCannons.Add("gun");
                                    if (!isOwn && s == 5) continue;                                          // an idle enemy shows no badge (EnemyTimerIdle off)
                                    cases++;
                                    var now0 = new OzBadgeState(id); var old = new OzBadgeState(id);
                                    bool view = false; float now = 100f; int step = 0;
                                    for (int pass2 = 0; pass2 < 2; pass2++)
                                        for (int h = 35; h <= 200; h++, step++)
                                        {
                                            float camH = pass2 == 0 ? h : 235 - h;
                                            now += 0.05f;
                                            view = CompactViewAt(view, camH);
                                            bool built = ready && isOwn && step % 30 == 0;
                                            now0.Observe(b, isOwn, true, built, b.CurUnit, w.Time, now);
                                            old.Observe(b, isOwn, true, built, b.CurUnit, w.Time, now);
                                            now0.Build(b, isOwn, paused, BadgeCompact(isOwn, view, b), now, 0.05f);
                                            old.Build(b, isOwn, paused, view && (isOwn ? CompactOwn : CompactEnemy), now, 0.05f);
                                            var v = now0.View; var o = old.View;
                                            if (!producing || !(v.fill > 0.0005f && v.fillAlpha > 0.004f)) { quiet++; continue; }
                                            if (Shown(v)) pass++;
                                            else
                                            {
                                                fail++;
                                                if (fail <= 12) bad.Append($"\n  FAIL {states[s]}, {(isOwn ? "own" : "enemy")} {b.Owner} {type}, {lefts[li]:0.#} s left, camera {camH:0} m, compact switches own {CompactOwn} / enemy {CompactEnemy}: the ring shows, the well shows \"{v.digits}\" at {v.digAlpha * (1f - v.compact):0.00} (compact {v.compact:0.00})");
                                            }
                                            if (o.fill > 0.0005f && o.fillAlpha > 0.004f && !Shown(o)) oldMiss++;
                                        }
                                }
            }
            finally { CompactOwn = co0; CompactEnemy = ce0; OzBadgeState.Force = f0; OzBadgeState.UseEventFeed = ev0; }
            return $"[OpenZ FactoryTime] COUNTED CHECK 71 {(fail == 0 && pass > 0 ? "PASS" : "FAIL")}: {pass} of {pass + fail} frames of a producing building with its ring shown also show its time "
                 + $"(or READY in the unit-ready moment); {cases} cases (6 states x own / enemy x red / blue x 10 times left x the 4 compact switch settings) x the camera 35 -> 200 -> 35 m; "
                 + $"{quiet} frames with no running build (the word or the medallion only); the former rule hid the time in {oldMiss} of those frames{bad}";
        }

        static bool Shown(OzBadgeView v)
        {
            float full = OzBadgeArt.Available ? 1f - v.compact : 1f;
            if (full <= 0.004f) return false;
            bool time = !string.IsNullOrEmpty(v.digits) && v.digAlpha * full > 0.004f;
            bool readyWord = v.word == "READY" && v.wordAlpha * full > 0.004f;
            return time || readyWord;
        }
    }
}
