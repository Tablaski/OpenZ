// OpenZ (GPL-3.0) - version 1.2, owner item 56: no match-state lines in the Unit editor's playground (View/AudioDirector.cs).
using System.Collections.Generic;
using UnityEngine.SceneManagement;

namespace OpenZ
{
    /// <summary>
    /// The playground has no match, so no line about the state of a match may speak there: winning or
    /// losing (the losing watcher's "They're gonna win!", the push watcher's "Let's do 'em!" / "They're on the run!"), victory or defeat
    /// near (the nuke lines), the end lines, territory, the fort, the start of a match. SayImpl (the robots' lines) and AnnounceImpl (the
    /// announcer, already silent there since the editor came) ask <see cref="HoldMatchLine(string)"/> first; a held line is written to
    /// the sound log ("HELD match-state line ...") and never queued. The robots' help cries under heavy fire stay (a unit voice), but
    /// their takes that speak of losing (<see cref="LosingTakes"/>: "We're losing it!", "It's hopeless! We can't hold them!") are
    /// swapped there for another cry of the same call. Every other unit voice (selection, orders, under attack, hurt, kills, vehicles
    /// and the rest) and every other sound stay. In a level nothing changes: the rule holds only while the Unit editor runs
    /// (OzUnitEditor) or its playground scene is open.
    /// </summary>
    public partial class AudioDirector
    {
        /// <summary>The lines about the state of a match (the announcer's and the robots').</summary>
        public static readonly HashSet<string> MatchStateLines = new HashSet<string>
        {
            "ann_youre_losing", "ann_victory", "ann_defeat", "ann_mission_completed", "ann_enemy_eliminated", "ann_enemy_fort_destroyed",
            "ann_fort_under_attack", "ann_fort_lost", "ann_territory_captured", "ann_territory_lost", "ann_level_start", "ann_battle_start",
            "ann_nuke_explain", "ann_nuke_incoming", "ann_nuke_detected", "ann_nuke_launched",
            "vo_push", "vo_win_lines", "vo_lose_lines", "vo_victory_cheer", "vo_we_won", "vo_game_start", "vo_territory_taken", "vo_nuke_delivery",
        };

        /// <summary>The help cries' takes (vo_under_attack_repeat) that speak of losing: swapped in the playground for another take.</summary>
        public static readonly HashSet<string> LosingTakes = new HashSet<string> { "We're losing it!", "It's hopeless! We can't hold them!" };

        /// <summary>Checks: match-state lines held, and losing takes swapped, in the playground since the game started.</summary>
        public static int MatchLinesHeld { get; private set; }
        public static int LosingTakesSwapped { get; private set; }

        /// <summary>True while the Unit editor's playground runs: there is no match there.</summary>
        public static bool NoMatchHere => OzUnitEditor.Active || OzUnitEditor.IsPlayground(SceneManager.GetActiveScene());

        /// <summary>SayImpl / AnnounceImpl: true = the line is held (a match-state line in the playground).</summary>
        public static bool HoldMatchLine(string id)
        {
            if (id == null || !MatchStateLines.Contains(id) || !NoMatchHere) return false;
            MatchLinesHeld++;
            OzSoundLog.Add("dir", id, null, "HELD match-state line (the Unit editor's playground has no match)");
            return true;
        }

        /// <summary>SayImpl: <see cref="HoldMatchLine(string)"/>, and in the playground a help cry asked as one whole take ("=line", the
        /// radio's line bag: Distress) that speaks of losing becomes another take of the same cry (the cry itself is not held).</summary>
        bool HoldMatchLine(string id, ref string filter)
        {
            if (HoldMatchLine(id)) return true;
            if (filter == null || filter.Length < 2 || filter[0] != '=' || !LosingTakes.Contains(filter.Substring(1)) || !NoMatchHere) return false;
            var d = Def(id);
            if (d == null || d.loaded == null || d.lines == null) return false;
            var other = new List<string>();
            for (int i = 0; i < d.loaded.Count && i < d.lines.Count; i++)
                if (!string.IsNullOrEmpty(d.lines[i]) && !LosingTakes.Contains(d.lines[i]) && !other.Contains(d.lines[i])) other.Add(d.lines[i]);
            if (other.Count == 0) return false;
            string was = filter.Substring(1);
            filter = "=" + other[UnityEngine.Random.Range(0, other.Count)];
            LosingTakesSwapped++;
            OzSoundLog.Add("dir", id, null, "SWAPPED take \"" + was + "\" for \"" + filter.Substring(1) + "\" (the Unit editor's playground has no match)");
            return false;
        }
    }
}
