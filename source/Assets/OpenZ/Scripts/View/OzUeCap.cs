using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.2 item 6 (the Unit editor, View/OzUnitEditor.cs): the field's unit cap, so a very big fight cannot bring the computer down while
    /// battles of a few hundred units a side still work. One number per side and one in all; every way units come in respects them (the
    /// dock's drag, click-to-place, Shift placing, x3 / x5, a picture dragged from the stats box, SAMPLE FIGHT, LOAD WITH UNITS: all of them
    /// place through OzUePlayground's one drop, View/OzUePlayground.Cap.cs), each refusal in plain words ("RED IS FULL: 200 UNITS, THE
    /// EDITOR'S LIMIT"); the counts plate shows the limit as a side comes near it (UI/OzUeCounts.cs). Units are counted as the counts plate
    /// counts them: every robot of a squad, every vehicle, gun and shuttle, empty ones too (a crew inside a vehicle is no unit of its own).
    /// The numbers are provisional: menu OpenZ/V1.2/Unit Editor/5 Unit Cap Bench (Play) measures 100 .. 400 units a side.
    /// </summary>
    public static class OzUeCap
    {
        /// <summary>The most units of one side (red or blue) on the field.</summary>
        public const int PerSide = 200;
        /// <summary>The most units on the field in all (red, blue and empty hardware).</summary>
        public const int Total = 400;
        /// <summary>SAMPLE FIGHT on the field at once (a fight counts while one of its units stands): one for each 30 units a side allows.</summary>
        public const int SampleFightsMax = PerSide / 30 > 0 ? PerSide / 30 : 1;
        /// <summary>The cap bench only (View/OzUeCapBench.cs): its armies pass the cap.</summary>
        public static bool Bypass;
        /// <summary>Checks: drops refused (or cut short) by the cap since the editor opened, and the last words.</summary>
        public static int Refusals;
        public static string LastFull;

        static int frame = -1, red, blue, neutral;

        /// <summary>"RED IS FULL: 200 UNITS, THE EDITOR'S LIMIT" (the team word as the player sees it).</summary>
        public static string FullSide(Team t) => OzUeUi.TeamWord(t) + " IS FULL: " + PerSide + " UNITS, THE EDITOR'S LIMIT";
        public static string FullAll => "THE FIELD IS FULL: " + Total + " UNITS, THE EDITOR'S LIMIT";

        /// <summary>Live units of a team on the field now (fresh = counted again, else once a frame).</summary>
        public static int Count(Team t, bool fresh = false) { Tally(fresh); return t == Team.Red ? red : t == Team.Blue ? blue : neutral; }
        public static int All(bool fresh = false) { Tally(fresh); return red + blue + neutral; }

        static void Tally(bool fresh)
        {
            if (!fresh && frame == Time.frameCount) return;
            frame = Time.frameCount; red = blue = neutral = 0;
            var w = OzUnitEditor.I ? OzUnitEditor.I.World : null;
            if (w == null) return;
            for (int i = 0; i < w.Objects.Count; i++)
            {
                var o = w.Objects[i];
                if (!o.IsUnit || o.Destroyed || o.KillAt >= 0 || o.IsFortDefender) continue;                  // as OzUePlayground.CountUnits
                if (o.Owner == Team.Red) red++; else if (o.Owner == Team.Blue) blue++; else neutral++;
            }
        }

        /// <summary>Room for `units` more units of team t (neutral hardware: the total only). why = the refusal's words.</summary>
        public static bool Room(Team t, int units, bool fresh, out string why)
        {
            why = null;
            if (Bypass || units <= 0) return true;
            Tally(fresh);
            if ((t == Team.Red || t == Team.Blue) && Count(t) + units > PerSide) { why = FullSide(t); return false; }
            if (red + blue + neutral + units > Total) { why = FullAll; return false; }
            return true;
        }

        /// <summary>A drop the cap refused or cut short (the words kept for LOAD WITH UNITS' message).</summary>
        public static void Note(string why) { Refusals++; LastFull = why; frame = -1; }

        /// <summary>The counts plate's number: the count alone, "165/200" from 80 % of the cap on.</summary>
        public static string CountText(int n) => n * 5 >= PerSide * 4 ? n + "/" + PerSide : n.ToString();
        /// <summary>Its colour: the team's text colour, AMBER from 80 % of the cap, ALERT at the cap.</summary>
        public static Color CountColour(Team t, int n) => n >= PerSide ? (Color)OzUi.AlertText : n * 5 >= PerSide * 4 ? (Color)OzUi.Amber : OzUeUi.TeamText(t);
        public static string CountTip => "At most " + PerSide + " a side and " + Total + " in all: the Unit editor's limit";

        /// <summary>A new editor session.</summary>
        internal static void Reset() { Refusals = 0; LastFull = null; frame = -1; Bypass = false; }
    }
}
