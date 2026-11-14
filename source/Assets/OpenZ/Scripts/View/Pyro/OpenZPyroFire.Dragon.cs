using System;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// DV163, level 7: the Pyro's own fire without a Pyro. The same jets, pool, sprites, light, shimmer, sticky fire, burnt patch and sounds.
    /// A dragon's jet starts at its mouth (read every frame in StepJets, beside the hatch driver's) and is keyed below every unit id. A Pyro's
    /// jet at a dragon starts at his muzzle and ends in the air, nothing lights up there. Neither has a bank arc: a stream that falls or
    /// climbs steeply goes straight. The team is Neutral: the rules' burning ground refuses it, the dragon's damage is its bites only.
    /// </summary>
    public sealed partial class OpenZPyroFire
    {
        static bool ReadyForDragon()
        {
            if (!Enabled) return false;
            if (!I)
            {
                if (!AutoCreate || !ZGame.I) return false;
                new GameObject("OpenZPyroFire").AddComponent<OpenZPyroFire>();
            }
            return I && I.ready && I.isActiveAndEnabled;
        }

        /// <summary>One flame beat of a dragon (every bite re-cues the jet, as a Pyro's shots do). t = what it hits, null for fire on the ground. False = not handled.</summary>
        public static bool DragonCue(int key, Func<Vector3> mouth, Transform soundAt, ZObject t, Vector3 to, bool hit)
        {
            if (!ReadyForDragon() || mouth == null) return false;
            I.CueFrom(key, mouth, soundAt, null, t, to, hit, false);
            return true;
        }

        /// <summary>The flame also caught this robot under it (ZDragonKind.Splash): the same sticky fire as the bite's target, at once, no second jet.</summary>
        public static void DragonSplash(int key, Vector3 from, ZObject t, Vector3 to)
        {
            if (ReadyForDragon()) I.pending.Add(new Pending { at = Time.time, jetId = key, target = t, hit = true, point = to, from = from, team = Team.Neutral });
        }

        /// <summary>A Pyro's flame shot at the dragon, from his muzzle, ending in the air. False = not handled.</summary>
        public static bool SkyCue(UnitView av, ZObject a, Vector3 to)
        {
            if (!ReadyForDragon() || !av || a == null) return false;
            I.CueFrom(a.Id, null, null, av, null, to, false, true);
            return true;
        }

        /// <summary>The flame ends now: the rules said so, it does not wait out JetHold.</summary>
        public static void DragonStop(int key)
        {
            if (I && I.jets.TryGetValue(key, out var j)) j.endAt = Time.time;
        }

        void CueFrom(int key, Func<Vector3> mouth, Transform soundAt, UnitView av, ZObject t, Vector3 to, bool hit, bool sky)
        {
            float now = Time.time; Shots++;
            if (!jets.TryGetValue(key, out var j)) { j = new Jet { id = key, loopKey = "pyro_jet:" + key, phase = Rand() * 10f }; jets[key] = j; j.endAt = -1f; }
            // the beats come on game time, the hold runs on real time: at the slow speeds (0.25, 0.5) the hold stretches with them, or the jet goes out between beats and its start and stop sounds repeat.
            // DragonStop still ends the flame on the rules' moment; above speed 1 nothing changes.
            float slow = 1f / Mathf.Clamp(ZGame.I ? ZGame.I.gameSpeed : 1f, 0.25f, 1f);
            bool fresh = now >= j.endAt || now - j.lastCue > 0.6f * slow;
            j.mouth = mouth; j.soundAt = soundAt; j.noArc = true; j.shooter = av;
            j.target = t; j.targetId = t != null ? t.Id : -1; j.hit = hit; j.lastCue = now; j.endAt = now + JetHold * slow;
            var game = ZGame.I;
            UnitView tv = null;
            if (game && t != null) game.Views.TryGetValue(t.Id, out tv);
            j.follow = null; j.targetRoot = tv ? tv.transform : null;
            if (hit && tv) { j.follow = tv.transform; j.toLocal = tv.transform.InverseTransformPoint(to); }
            j.toWorld = to;
            var from = mouth != null ? mouth() : av.MuzzlePos();
            if (fresh) { j.acc = 0f; j.at = from; j.aimSet = false; StartJetSound(j, OnScreen(from, 0.15f) ? from : to); }   // 1.3 U5: the mouth above the view, the flame on screen: its start is heard where it lands
            float travel = Vector3.Distance(from, to) / Speed;
            pending.Add(new Pending { at = now + travel, jetId = key, target = t, hit = hit, point = to, from = from, team = Team.Neutral, sky = sky });
            if (j.fallbackAudio || !AudioDirector.I) CombatAudio.I?.Play("flame", from, 0.9f, 0.9f, key, "flame_loop1", "flame_loop2");
        }
    }
}
