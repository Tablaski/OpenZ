using System.Collections.Generic;
using UnityEngine;

namespace OpenZ
{
    public partial class AmbientLife
    {
        /// <summary>1.4 ptero_scream: the attack scream of a dragon, at its pass's start (DragonAsk, right after the rules said yes) and every frame of a
        /// dragon (DragonStep) while its scream is still due. Plays one of the owner's three screams at the dragon, once per pass, when the dragon is
        /// inside the camera view (OzDragonScream).</summary>
        void DragonScream(Bird b)
        {
            if (!OzDragonScream.Due(b.id)) return;
            var id = OzDragonScream.Frame(b.id, b.simPass, OzDragonScream.OnScreen(cam, b.pos));
            if (id != null) Sound(id, b.pos, 1f, 110 + b.id);
        }
    }

    /// <summary>
    /// The level 7 dragon's attack scream. Each attack (a pass of the rules, DV163) plays ONE of the owner's three screams
    /// (sound events amb_l07_dragon_attack_1 / _2 / _3, clips Resources/OpenZ/Audio/Ambience/sd_lava_dragon_attack_a / _b / _c), at the dragon:
    /// at the pass's start (the lunge) when the dragon is inside the camera view then, else on the first frame of the pass it is inside the view.
    /// A pass that never shows the dragon inside the view plays nothing. The pick is random on this class's own generator (view side, never the
    /// sim's), never the same scream twice in a row. View only: no rule, no sim number, nothing saved.
    /// </summary>
    public static class OzDragonScream
    {
        public static readonly string[] Ids = { "amb_l07_dragon_attack_1", "amb_l07_dragon_attack_2", "amb_l07_dragon_attack_3" };
        public static readonly string[] Clips = { "OpenZ/Audio/Ambience/sd_lava_dragon_attack_a", "OpenZ/Audio/Ambience/sd_lava_dragon_attack_b", "OpenZ/Audio/Ambience/sd_lava_dragon_attack_c" };

        static readonly System.Random rng = new System.Random(System.Environment.TickCount);           // the view's own: the sim's random numbers are never drawn
        static readonly Dictionary<int, float> due = new Dictionary<int, float>();                     // dragon id -> the pass's start (unscaled time)
        static int last = -1;

        /// <summary>Counted for the checks (Editor/OzV14PteroScreamCount.cs): passes started, screams played, passes over without the dragon on screen,
        /// the last scream's event and its delay after the pass's start (0 = at the lunge).</summary>
        public static int Started, Screams, Missed;
        public static string LastId;
        public static float LastDelay;

        /// <summary>The next scream: random, never the one before.</summary>
        public static int Pick()
        {
            int n = Ids.Length, k = last < 0 ? rng.Next(n) : (last + 1 + rng.Next(n - 1)) % n;
            last = k;
            return k;
        }

        /// <summary>Inside the camera view: in front of the lens and within the frame.</summary>
        public static bool OnScreen(Camera c, Vector3 p)
        {
            if (!c) return false;
            var v = c.WorldToViewportPoint(p);
            return v.z > 0f && v.x >= 0f && v.x <= 1f && v.y >= 0f && v.y <= 1f;
        }

        /// <summary>A pass started (the rules said yes): its scream is due.</summary>
        public static void Start(int dragon) { due[dragon] = Time.unscaledTime; Started++; }

        public static bool Due(int dragon) => due.Count > 0 && due.ContainsKey(dragon);

        /// <summary>One frame of a dragon whose scream is due: the event to play now, or null. pass = its pass still runs; onScreen = it is inside the view.</summary>
        public static string Frame(int dragon, bool pass, bool onScreen)
        {
            if (!due.TryGetValue(dragon, out float from)) return null;
            if (!pass) { due.Remove(dragon); Missed++; return null; }
            if (!onScreen) return null;
            due.Remove(dragon); Screams++;
            LastDelay = Time.unscaledTime - from;
            return LastId = Ids[Pick()];
        }

        public static void Reset() { due.Clear(); last = -1; Started = Screams = Missed = 0; LastId = null; LastDelay = 0f; }
    }
}
