// OpenZ (GPL-3.0) - version 1.3, level 7: the line a robot says when the player sends it into lava (rule DV152, Sim/ZWorld.Lava.cs).
using System.Collections;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// A click on lava is obeyed, and one robot of the squad complains at the order ("Not into lava!", "You gotta be kidding me!" and the rest of
    /// the vo_order_lava pool), then they go. One line of the pool through the gate of the no-way answer (View/Voice/OzOrderKillRules.cs): it waits
    /// for quiet voices (at most Wait s), it cuts nobody, at most one every Gap seconds, never the same line twice running. Inside the gap the click
    /// sound alone answers (no "Yes sir" after a refusal). The speaker: the selected robot that spoke last when it is on the order, else the first
    /// one on it. EvilZod says nothing of the kind: alone he gives his own move line. BRAD neither (the lines are not in his voice, as with the
    /// no-way line): alone he gives his own hero line. The step-in line of View/OzLava.cs keeps its gap from this one.
    /// View only: reads ZWorld.LavaCalls (a new click on lava) and the selected robots' waypoints; nothing here writes the simulation.
    /// </summary>
    public partial class AudioDirector
    {
        public const string LavaOrderEvent = "vo_order_lava";
        readonly OzNoWayGate lvGate = new OzNoWayGate { Gap = 5.0, Wait = 2.0, After = 0.1 };
        int lvCalls; ZWorld lvWorld;
        /// <summary>Checks: the gate of the lava order line of this level's director (its counts and the last line).</summary>
        public static OzNoWayGate LavaLineGate => I ? I.lvGate : null;

        /// <summary>OrderVoice, first: true = this order is answered here (the line, or the click alone inside the gap); false = the usual answer.</summary>
        bool LavaOrderVoice()
        {
            if (!ZWorld.LavaDeviation || game == null || game.World == null || !game.World.Map.HasLava) return false;
            var w = game.World;
            if (w != lvWorld) { lvWorld = w; lvCalls = 0; }                    // a new match: its own count
            if (w.LavaCalls == lvCalls) return false;                            // no new click on lava in this order
            lvCalls = w.LavaCalls;
            ZObject who = null;
            foreach (var u in input.Selection)
                if (u != null && !u.Destroyed && u.Kind == ZKind.Robot && !u.IsEvilZod && !(u.IsHero && u.HeroMemberName == "brad") && OnLavaOrder(u)) { who = u; if (u.Id == uvSpeaker) break; }
            var d = Def(LavaOrderEvent);
            if (who == null || d == null || d.loaded == null || d.loaded.Count == 0 || !voices) return false;
            double now = Time.unscaledTimeAsDouble;
            if (!lvGate.Due(now)) { OzSoundLog.Add("dir", LavaOrderEvent, null, lvGate.Waiting ? "lava line already waiting: the click alone" : "lava line gap: the click alone"); return true; }
            string line = lvGate.Ask(d.lines, now, () => Random.value);
            if (line == null) return false;
            uvSpeaker = who.Id;
            OzSoundLog.Add("dir", LavaOrderEvent, null, "lava order: " + who.Type + " #" + who.Id + " (it goes); \"" + line + "\" waits for quiet voices");
            StartCoroutine(LavaLineRun());
            return true;
        }

        static bool OnLavaOrder(ZObject u)
        {
            foreach (var w in u.Waypoints) if (w.refId == ZWorld.RefLavaIn || w.refId == ZWorld.RefLavaEdge) return true;
            return false;
        }

        IEnumerator LavaLineRun()
        {
            while (true)
            {
                var s = lvGate.Tick(Time.unscaledTimeAsDouble, NoWayQuiet());
                if (s == OzNoWayGate.Step.Say) break;
                if (s != OzNoWayGate.Step.Wait) { OzSoundLog.Add("dir", LavaOrderEvent, null, "lava line dropped: the voices did not go quiet within " + lvGate.Wait.ToString("0.0") + " s"); yield break; }
                yield return null;
            }
            string line = lvGate.Pending;
            int before = Plays.TryGetValue(LavaOrderEvent, out var n0) ? n0 : 0;
            bool keep = shuttleReleasing; shuttleReleasing = true;              // it waited for quiet voices itself: no robot line gap on top
            try { SayImpl(LavaOrderEvent, "=" + line); } finally { shuttleReleasing = keep; }
            bool played = (Plays.TryGetValue(LavaOrderEvent, out var n1) ? n1 : 0) > before && alanMain.src && alanMain.src.isPlaying;
            lvGate.Spoke(Time.unscaledTimeAsDouble, played);
            if (!played) { OzSoundLog.Add("dir", LavaOrderEvent, null, "lava line held by the voice rules: \"" + line + "\""); yield break; }
            OzLava.OrderLineSaid();
            OzSoundLog.Add("dir", LavaOrderEvent, alanMain.src.clip ? alanMain.src.clip.name : null, "lava order line: \"" + line + "\"");
        }
    }
}
