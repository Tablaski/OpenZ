// OpenZ (GPL-3.0) - version 1.4, speed-up 2 (fewer garbage-collector stutters), view and sound only. Texts and small arrays the view
// made again on every frame or every sound are made once and kept: the same text and the same values, so nothing seen or heard changes.
//  * NameOf: Object.name makes a new string at every call; the sound code read it several times for every sound it played (the level
//    trim, the take gain, the sound log). The name is read once per clip.
//  * PlayNote: the sound log's outcome text (the word PLAY, distant or not, and the bus), built once per bus.
//  * SlewKey: the turret traverse loop's key, built once per unit (it was built every frame for every gun).
//  * ShadowCorners: the three viewport points the shadow distance looks at (an array made every frame before).
using System.Collections.Generic;
using UnityEngine;

namespace OpenZ
{
    public static class OzGcCache
    {
        static readonly Dictionary<int, string> names = new Dictionary<int, string>();

        /// <summary>The object's name as Object.name gives it, read once per object; null for none (a missing or destroyed object).</summary>
        public static string NameOf(Object o)
        {
            if (!o) return null;
            int id = o.GetInstanceID();
            if (!names.TryGetValue(id, out var n))
            {
                if (names.Count >= 4096) names.Clear();                                           // clips made at run time cannot grow it without end
                n = o.name; names[id] = n;
            }
            return n;
        }

        static readonly Dictionary<int, string> caNotes = new Dictionary<int, string>();
        /// <summary>CombatAudio's sound log outcome: the same text as the old concatenation of PLAY (or PLAY distant) and the bus.</summary>
        public static string PlayNote(bool distant, CombatAudio.Bus bus)
        {
            int k = ((int)bus << 1) | (distant ? 1 : 0);
            if (!caNotes.TryGetValue(k, out var s)) { s = (distant ? "PLAY distant " : "PLAY ") + bus; caNotes[k] = s; }
            return s;
        }

        static readonly Dictionary<string, string> adNotes = new Dictionary<string, string>();
        static string adNoteNull;
        /// <summary>AudioDirector's sound log outcome: the same text as the old concatenation of PLAY and the bus name.</summary>
        public static string PlayNote(string bus)
        {
            if (bus == null) return adNoteNull ??= "PLAY " + bus;
            if (!adNotes.TryGetValue(bus, out var s)) { s = "PLAY " + bus; adNotes[bus] = s; }
            return s;
        }

        static readonly Dictionary<int, string> slewKeys = new Dictionary<int, string>();
        /// <summary>OzSoundV3.Slew's loop key of a unit: the same text the old line built every frame.</summary>
        public static string SlewKey(int id)
        {
            if (!slewKeys.TryGetValue(id, out var k)) { k = "slew:" + id; slewKeys[id] = k; }
            return k;
        }

        /// <summary>ShadowDistanceByHeight: the viewport points it rays every frame, in the old order. Read only.</summary>
        public static readonly Vector3[] ShadowCorners = { new Vector3(0f, 1f, 0f), new Vector3(1f, 1f, 0f), new Vector3(0.5f, 1f, 0f) };
    }
}
