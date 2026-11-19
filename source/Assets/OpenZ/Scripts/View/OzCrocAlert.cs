// OpenZ (GPL-3.0) - version 1.5 kit croc_alert: no siren at a crocodile attack; a robot of ours that sees it warns in words.
using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// View only: no rule, no sim state, no sim random number.
    /// <para>THE SIREN: CrocodileView.OnNote played robot_croc_alarm (sd_robot_croc_alarm_a, a 1 s rising alarm) for every squad that started
    /// to hurry away from a hunting crocodile. With <see cref="SirenOff"/> that one call is left out; the clip stays, and the level 16 briefing
    /// (Zod/OzL16BriefingStage.cs, its own scripted chase) and the flyover lists keep their use of it.</para>
    /// <para>THE WARNING: when a hunt starts (the crocodile's Warn state, CrocodileView.OnNote) it is watched for <see cref="WatchSec"/> s
    /// while the animal still hunts. As soon as the attacked robot or the crocodile is inside the main camera's picture and not under the fog
    /// of war (MAYHEM), and a robot of the player's side stands within <see cref="SpeakerM"/> m of the robot or the animal (the attacked
    /// robot itself counts), the voice director says the robot line <see cref="LineId"/> (openz_sound_events.json: six lines, Alan's voice,
    /// the robot call-out channel alan_alert on Voice/Robots at 0 dB like vo_sniper_warning / vo_grenade_warning, a shuffled bag that never
    /// says the same line twice in a row). It goes out only into silence: no robot or announcer voice speaking or waiting
    /// (AudioDirector.VoiceBusy), the robot line gap over (AudioDirector.RobotLineFree), the director would really say it now
    /// (AudioDirector.SayWouldPlay: voices on, the ROBOT CHATTER setting, the line's 20 s cool-down, no briefing voice) and no crocodile shout
    /// of the robots (vo_croc_hurry / flee / bank, 3D world sounds of CrocodileView) still sounding. While it speaks those shouts are left
    /// out (<see cref="HoldsCrocLine"/>), so two robot voices never overlap. One line covers every hunt seen at that moment; a hunt that
    /// ends, or is not seen within its watch, says nothing.</para>
    /// </summary>
    public static class OzCrocAlert
    {
        /// <summary>The robot line event (openz_sound_events.json).</summary>
        public const string LineId = "vo_croc_warning";
        /// <summary>On = no siren at a crocodile attack (the fix). Off = 1.4 as it is (the siren for every squad that starts to hurry).</summary>
        public static bool SirenOff = true;
        /// <summary>On = a robot of ours that sees a crocodile attack warns in words. Off = no warning line (1.4).</summary>
        public static bool LinesOn = true;
        /// <summary>How long a started hunt is watched for a moment to say the line (real seconds; the hunt must still be on).</summary>
        public const float WatchSec = 6f;
        /// <summary>A robot of the player's side within this many metres of the attacked robot or of the animal can say it.</summary>
        public const float SpeakerM = 24f;
        /// <summary>The picture's inner rim (share of the screen) a point must be inside to count as seen.</summary>
        public const float ScreenInset = 0.02f;
        /// <summary>The crocodile shouts are left out while the line speaks, at most this long after it started (s).</summary>
        public const float HoldShoutsSec = 4.5f;

        /// <summary>Checks: hunts started, hunts seen on screen, lines said, hunts that ended unseen, seen hunts that ended without a line
        /// (with the last reason), shouts left out for the line, sirens left out.</summary>
        public static int Hunts, Seen, Said, Unseen, NotSaid, ShoutsHeld, SirensLeftOut;
        public static string LastWhy = "none yet";
        /// <summary>When the last line was said (Time.unscaledTime, -100 = never).</summary>
        public static float SaidAt = -100f;

        sealed class Watch { public int croc, prey; public float since; public float y; public bool seen; public string why; }
        static readonly List<Watch> watches = new List<Watch>();
        static ZWorld world;
        static float nextLook, shoutUntil = -100f;

        /// <summary>CrocodileView.OnNote: a hunt started (the animal's Warn state began). crocAt: the animal's place on the water (world).</summary>
        public static void HuntStarted(ZWorld w, int croc, int prey, Vector3 crocAt)
        {
            if (w != world) { world = w; watches.Clear(); }
            Hunts++;
            if (!LinesOn) return;
            foreach (var x in watches) if (x.croc == croc) { x.prey = prey; x.since = Time.unscaledTime; x.y = crocAt.y; x.seen = false; x.why = null; return; }
            watches.Add(new Watch { croc = croc, prey = prey, since = Time.unscaledTime, y = crocAt.y });
        }

        /// <summary>CrocodileView.OnNote, Hurry: the siren of 1.4 left out.</summary>
        public static void NoSiren(Vector3 at)
        {
            SirensLeftOut++;
            OzSoundLog.Add("L16", "robot_croc_alarm", null, "left out (1.5 croc_alert: no siren at a crocodile attack)", 0f, at);
        }

        /// <summary>CrocodileView.CrocLine: a crocodile shout (vo_croc_hurry / flee / bank) is left out while the warning line speaks.</summary>
        public static bool HoldsCrocLine(string evt, Vector3 at)
        {
            if (!LinesOn) return false;
            float now = Time.unscaledTime, t = now - SaidAt;
            if (t < 0f || t >= HoldShoutsSec || (t >= 0.3f && !AudioDirector.VoiceBusy)) return false;
            ShoutsHeld++;
            OzSoundLog.Add("L16", evt, null, "DROP the warning line " + LineId + " speaks (1.5 croc_alert: never two robot voices at once)", 0.8f, at);
            return true;
        }

        /// <summary>CrocodileView.CrocLine: the shout that played (null = none); the warning line waits until it has ended.</summary>
        public static void CrocLineSaid(AudioClip c)
        {
            if (c) shoutUntil = Mathf.Max(shoutUntil, Time.unscaledTime + c.length + 0.3f);
        }

        /// <summary>CrocodileView.LateUpdate, once a frame (looks 10 times a second while a hunt is watched).</summary>
        public static void Tick(ZWorld w, Camera cam)
        {
            if (watches.Count == 0) return;
            if (w == null || w != world) { watches.Clear(); world = w; return; }
            float now = Time.unscaledTime;
            if (now < nextLook) return;
            nextLook = now + 0.1f;
            for (int i = watches.Count - 1; i >= 0; i--)
            {
                var x = watches[i];
                var c = CrocOf(w, x.croc);
                if (c == null || !c.Hunting || now - x.since > WatchSec)
                {
                    if (x.seen) { NotSaid++; LastWhy = x.why ?? "the hunt ended"; OzSoundLog.Add("dir", LineId, null, "not said: " + LastWhy); }
                    else Unseen++;
                    watches.RemoveAt(i); continue;
                }
                var prey = c.PreyId >= 0 ? w.Get(c.PreyId) : null;
                if (prey == null && x.prey >= 0) prey = w.Get(x.prey);
                var crocAt = new Vector3((float)(c.X / ZWorld.CrocPx), x.y, (float)(-c.Y / ZWorld.CrocPx));
                if (!SeenNow(cam, c, crocAt, prey)) continue;
                if (!x.seen) { x.seen = true; Seen++; }
                var speaker = Speaker(w, c, prey);
                if (speaker == null) { x.why = "no robot of ours within " + SpeakerM.ToString("0") + " m"; continue; }
                string busy = Busy(now);
                if (busy != null) { x.why = busy; continue; }
                int before = Plays();
                AudioDirector.Say(LineId);
                bool held = Plays() <= before && AudioDirector.RobotLineWaiting == LineId;          // the voice floor holds it a moment: it is said from there
                if (Plays() <= before && !held) { x.why = "the director did not start it"; continue; }
                Said++; SaidAt = now;
                OzSoundLog.Add("dir", LineId, null, (held ? "asked (the voice floor holds it a moment): robot " : "said: robot ") + speaker.Id + " saw crocodile " + c.Index + " go for robot " + (prey != null ? prey.Id.ToString() : "?")
                               + " (1.5 croc_alert)", 0f, crocAt);
                watches.Clear();                                                                 // one line covers every hunt seen now
                return;
            }
        }

        static ZCroc CrocOf(ZWorld w, int index)
        {
            var list = w.Crocs;
            if (index >= 0 && index < list.Count && list[index].Index == index) return list[index];
            for (int k = 0; k < list.Count; k++) if (list[k].Index == index) return list[k];
            return null;
        }

        /// <summary>The attacked robot or the animal is inside the main camera's picture and not under the fog of war for the player.</summary>
        static bool SeenNow(Camera cam, ZCroc c, Vector3 crocAt, ZObject prey)
        {
            if (!cam) return false;
            if (CrocodileView.Showing) return false;                                               // a flyover lends the animals: no game voice then
            if (ZodFlyover.Live != null && ZodFlyover.Live.Active) return false;                   // the mission-start flyover
#if UNITY_EDITOR
            if (OzEpicFlyby.Busy) return false;                                                    // the editor's flyby recorder (it exists in the editor only)
#endif
            if (InPicture(cam, crocAt) && !FogHides((int)c.X, (int)c.Y)) return true;
            var g = ZGame.I;
            return prey != null && g && InPicture(cam, g.CenterWorld(prey)) && !OzFogOfWar.Covers(prey);
        }

        static bool InPicture(Camera cam, Vector3 p)
        {
            var v = cam.WorldToViewportPoint(p);
            return v.z > 0f && v.x > ScreenInset && v.x < 1f - ScreenInset && v.y > ScreenInset && v.y < 1f - ScreenInset;
        }

        static bool FogHides(int px, int py)
        {
            if (!OzFogOfWar.Hiding) return false;
            var m = OzFogOfWar.Mask;
            return m != null && m.ShownAtPx(px, py) < OzFowMask.Revealed;
        }

        /// <summary>The nearest robot of the player's side within SpeakerM of the attacked robot or of the animal (null = none).</summary>
        static ZObject Speaker(ZWorld w, ZCroc c, ZObject prey)
        {
            var g = ZGame.I; if (!g) return null;
            Team me = g.player;
            double r2 = SpeakerM * ZWorld.CrocPx * SpeakerM * ZWorld.CrocPx, best = double.MaxValue;
            ZObject who = null;
            var all = w.Objects;
            for (int i = 0; i < all.Count; i++)
            {
                var o = all[i];
                if (o == null || o.Kind != ZKind.Robot || o.Owner != me || o.Destroyed || o.KillAt >= 0 || o.Eaten) continue;
                double dx = o.CenterX - c.X, dy = o.CenterY - c.Y, d = dx * dx + dy * dy;
                if (prey != null) { double ex = o.CenterX - prey.CenterX, ey = o.CenterY - prey.CenterY; d = System.Math.Min(d, ex * ex + ey * ey); }
                if (d <= r2 && d < best) { best = d; who = o; }
            }
            return who;
        }

        /// <summary>Why the line cannot go out now (null = it can).</summary>
        static string Busy(float now)
        {
            if (now < shoutUntil) return "a crocodile shout of the robots was still sounding";
            if (AudioDirector.VoiceBusy) return "another voice was speaking";
            if (!AudioDirector.RobotLineFree) return "the robot line gap after the last line";
            if (!AudioDirector.SayWouldPlay(LineId)) return "a voice setting or the line's cool-down";
            return null;
        }

        static int Plays()
        {
            var a = AudioDirector.I; if (!a) return 0;
            a.Plays.TryGetValue(LineId, out int n); return n;
        }

        /// <summary>The counters in one line (the menu OpenZ/V1.5/Croc Alert: report).</summary>
        public static string Summary() =>
            "[OpenZ] croc alert: hunts " + Hunts + ", seen on screen " + Seen + ", lines said " + Said + ", unseen " + Unseen + ", seen but not said " + NotSaid
            + " (last: " + LastWhy + "), crocodile shouts held for the line " + ShoutsHeld + ", sirens left out " + SirensLeftOut
            + "; switches SirenOff " + SirenOff + ", LinesOn " + LinesOn + "; robot_croc_alarm played this level "
            + (L16AnimalKit.Played.TryGetValue("robot_croc_alarm", out int s) ? s : 0);

        /// <summary>Checks: the counters back to 0 (the menu).</summary>
        public static void ResetCounters() { Hunts = Seen = Said = Unseen = NotSaid = ShoutsHeld = SirensLeftOut = 0; LastWhy = "none yet"; }
    }
}
