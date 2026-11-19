// OpenZ (GPL-3.0)
// row; the rules' events of this frame are only read.
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// A fort taken by a unit walking in dies in the rules at once (Sim/ZWorld.Rules.cs RulesSideEffects: the FortEntered event, health 0, the Death
    /// event; its other half with the team's elimination in the same tick). On that Death LegacyMatch.BuildingDeath played exp_v2_6 SHOCK (kit
    /// explosions_in_game_1_5, owner addition 45: on top of every building destruction; a fort at full size: an 18 m blue-white ring and its wall of
    /// dust) on the fort the moment the unit got in, seconds before the bomb. The fort's fall belongs to the nuke strike (RulesView.BuildingDeath: the
    /// fort stands until the bomb lands), so for a taken fort the SHOCK is left out while the strike is on. The 1.4 blast of that moment, the strike,
    /// the mushroom, the collapse, the megacannon's toss and every other building's SHOCK are untouched.
    /// </summary>
    public static class OzCaptureNoShock
    {
        /// <summary>On = a fort taken by a unit walking in plays no exp_v2_6 at its fall (while the nuke strike is on); off = as before this kit.</summary>
        public static bool On = true;
        /// <summary>Off (the default) = only a taken fort. On = also every other fort fall the strike plays (the team's last unit lost, the game-over
        /// drop of FortFallAtGameOver); not asked for by the owner, left for the lead.</summary>
        public static bool AllStrikeFalls = true;
        /// <summary>A fort part this close (m) to the entered one falls with it (fort_front + fort_back; the strike's own one-bomb rule is 60 m).</summary>
        public const float PairM = 60f;
        /// <summary>Counts for the Play check: fort parts that fell taken by a unit walking in (whatever the switch); SHOCKs left out.</summary>
        public static int TakenFalls, Skipped;
        public static string LastLine = "";

        /// <summary>LegacyMatch.BuildingDeath, before OzExpInGame.BuildingDied: true = leave the SHOCK out for this building.</summary>
        public static bool Skips(ZObject o)
        {
            if (o == null || !o.IsFort) return false;
            bool taken = Taken(o);
            if (taken) TakenFalls++;
            if (!On || !OpenZNukeStrike.Enabled || !(taken || AllStrikeFalls)) return false;
            Skipped++;
            LastLine = $"{OzExpInGame.Tag} capture: {o.Type} #{o.Id} {(taken ? "taken by a unit walking in" : "falls")}: its exp_v2_6 ground wave left out, the nuke strike plays its fall";
            Debug.Log(LastLine);
            return true;
        }

        /// <summary>A FortEntered event in this frame's list of the rules (read before CombatView clears it) names this fort part or a fort part next to it.</summary>
        public static bool Taken(ZObject o)
        {
            var g = ZGame.I; var w = g ? g.World : null;
            if (w == null || o == null) return false;
            var ev = w.Events;
            for (int i = 0; i < ev.Count; i++)
            {
                if (ev[i].type != ZEventType.FortEntered) continue;
                var f = w.GetAny(ev[i].otherId);
                if (f == null || !f.IsFort) continue;
                if (f.Id == o.Id) return true;
                float dx = (f.CenterX - o.CenterX) / 8f, dy = (f.CenterY - o.CenterY) / 8f;                   // px -> m (ZGame.PxToWorld)
                if (dx * dx + dy * dy < PairM * PairM) return true;
            }
            return false;
        }
    }
}
