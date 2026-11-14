using System.Collections.Generic;
using System.Globalization;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.2 item 73 (View/OzUeMarks.cs): the GRENADES crate's marks, as the flag's: while the pointer is on a crate or it is picked, an INK
    /// ring (the drop preview's 0.9 m) and its words in two lines, "GRENADE CRATE  20 grenades" over "Robots take it by walking over it";
    /// with the Background option "Name labels on every unit" the other crates show "GRENADE CRATE" alone. The crate's own amber glow ring
    /// stays the game's.
    /// </summary>
    internal sealed partial class OzUeMarks
    {
        /// <summary>Checks (the last frame): crate rings drawn, the word lines of the crate pointed at or picked (2 = both), its first line.</summary>
        internal static int CrateRings, CrateWords;
        internal static string LastCrateTop;
        public const string CrateName = "GRENADE CRATE", CrateHint = "Robots take it by walking over it";
        /// <summary>The ring's radius (m, the drop preview's), the words' point over the floor (m, the crate's top) and their lift (u).</summary>
        public const float CrateRingM = 0.9f, CrateTopM = 1.4f, CrateWordsUp = 6f;
        readonly List<ZObject> crates = new List<ZObject>();
        static readonly Dictionary<int, string> crateTexts = new Dictionary<int, string>();

        void CrateMarks(ZGame game, ZObject hover, UeInspect kind, ZObject insp, bool all)
        {
            CrateRings = CrateWords = 0;
            foreach (var o in crates)
            {
                if (Gone(o)) continue;
                bool on = o == hover || (kind == UeInspect.Crate && insp == o);
                if (!on && !all) continue;
                Vector3 p = Pos(game, o);
                if (on) { OzUeDraw.Ring(p, CrateRingM, (Color)OzUi.Ink, 4f, 3f, 1.5f); CrateRings++; }
                string top = on ? CrateText(o.Grenades) : CrateName;
                Words(p + Vector3.up * CrateTopM, top, on ? CrateHint : null, NeutralText, CrateWordsUp);
                if (on) { CrateWords = 2; LastCrateTop = top; }
            }
        }

        /// <summary>"GRENADE CRATE  20 grenades" (cached per count).</summary>
        static string CrateText(int n)
        {
            if (!crateTexts.TryGetValue(n, out var s))
            {
                s = CrateName + "  " + n.ToString(CultureInfo.InvariantCulture) + (n == 1 ? " grenade" : " grenades");
                crateTexts[n] = s;
            }
            return s;
        }
    }
}
