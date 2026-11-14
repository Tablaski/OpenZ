using System.Collections.Generic;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.2 items 167 / 167b / 167c: the NEUTRAL (empty, nobody's) pictures of the vehicles, the guns and the shuttle, one look for the whole
    /// set: each model's own empty picture with every team-colour part in the empty units' white-grey (no red, no blue), the same soft
    /// light and the same grey studio backdrop for all. Resources/OpenZ/UI/Neutral/neutral_&lt;type&gt;.png, a two-model unit's per model
    /// (neutral_&lt;type&gt;_v1 / _v2); 512 x 512. The testing overlay's EMPTY row (UI/OzCheatSpawner.cs) and the Unit editor's neutral
    /// tiles, rows, cards and ghost (View/OzUeArt.cs) draw them as they are, without the old grey filter or grey tint. A type with no
    /// picture here gives null and its caller keeps its old look.
    /// </summary>
    public static class OzNeutralArt
    {
        public const string Dir = "OpenZ/UI/Neutral/";
        static readonly Dictionary<string, Texture2D> loaded = new Dictionary<string, Texture2D>();
        static readonly HashSet<Texture> mine = new HashSet<Texture>();
        static readonly HashSet<string> missing = new HashSet<string>();

        /// <summary>The testing overlay's tile ("jeep", "light@v1", "heavy@v2", ...): the picture of that tile's model, or null.</summary>
        public static Texture2D ForTile(string tile)
        {
            if (string.IsNullOrEmpty(tile)) return null;
            int v = OzHeavyTank.TileVersion(tile);
            if (v != 0) return Load(OzHeavyTank.Type, v);
            foreach (var u in OzV2Units.All) { v = u.TileVersion(tile); if (v != 0) return Load(u.Type, v); }
            return Load(tile, Drawn(tile));
        }

        /// <summary>A unit type as the game draws it now (the switch's model of a two-model unit): its picture, or null.</summary>
        public static Texture2D ForUnit(string type) => string.IsNullOrEmpty(type) ? null : Load(type, Drawn(type));

        /// <summary>True for a picture of this set (drawn as it is: no grey tint).</summary>
        public static bool Is(Texture t) => t != null && mine.Contains(t);

        static int Drawn(string type)
        {
            if (type == OzHeavyTank.Type) return OzHeavyTank.Drawn;
            var u = OzV2Units.Of(type);
            return u != null ? u.Drawn : 1;
        }

        // two models with their own pictures: the heavy tank and every two-model unit whose v2 is its own model (the medium tank has one)
        static bool TwoModels(string type)
        {
            if (type == OzHeavyTank.Type) return true;
            var u = OzV2Units.Of(type);
            return u != null && u.V1Key != u.V2Key;
        }

        static Texture2D Load(string type, int version)
        {
            string name = "neutral_" + type + (TwoModels(type) ? (version == 2 ? "_v2" : "_v1") : "");
            if (loaded.TryGetValue(name, out var t) && (t || missing.Contains(name))) return t ? t : null;   // a picture unloaded meanwhile is loaded again
            t = Resources.Load<Texture2D>(Dir + name);
            loaded[name] = t;
            if (t) { mine.Add(t); missing.Remove(name); } else { missing.Add(name); t = null; }
            return t;
        }
    }
}
