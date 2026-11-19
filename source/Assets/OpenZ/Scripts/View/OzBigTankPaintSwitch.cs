using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// drawn with their main body + turret paint DESERT or the old GREEN so he can compare both in game.
    /// DESERT (the R15 game-frame calibrated maps) is now the paint on disk (Models/BigTanks/textures/{bizon,mammoth}_{body,head}_albedo.png),
    /// so every level, the Unit editor, the menu and the wrecks show it with no switch at all; this switch now flips to the OLD GREEN maps
    /// (Resources/OpenZ/BigTanks/GreenPaint/) and back. Only the albedo (_BaseMap) of the four shared materials
    /// Materials/Units/{BizonV2,MammothV2}_{body,head} changes, at runtime, in memory: tracks, wells, normals, every other material and the
    /// team colour / decals (in the albedo itself and its alpha paint mask) stay. The views draw those shared materials (TeamPaint only adds
    /// property blocks), so live tanks, tanks dropped later and their wreck pieces all follow at once.
    /// Never written to the .mat files: DESERT puts the materials' own maps back, and leaving Play / quitting does too.
    /// </summary>
    public static class OzBigTankPaintSwitch
    {
        public const string Dir = "OpenZ/BigTanks/GreenPaint/";
        static readonly string[] MatNames = { "BizonV2_body", "BizonV2_head", "MammothV2_body", "MammothV2_head" };
        static readonly string[] TexNames = { "bizon_body_albedo_green", "bizon_head_albedo_green", "mammoth_body_albedo_green", "mammoth_head_albedo_green" };
        static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");

        /// <summary>true while DESERT (the default: the materials' own maps) is drawn; false while the old GREEN is.</summary>
        public static bool Desert => !greenNow;
        /// <summary>Checks: paint changes since start.</summary>
        public static int Swaps;

        static bool greenNow;
        static Material[] mats; static Texture[] own; static Texture2D[] green; static bool hooked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Restore(); mats = null; own = null; green = null; wasDirty = null; Swaps = 0; }

        /// <summary>The four shared materials as the big tanks' prefabs (and so every view made from them) use them.</summary>
        static bool Find()
        {
            if (mats != null) return true;
            var m = new Material[MatNames.Length];
            foreach (var models in new[] { OzBigTanks.BizonModels, OzBigTanks.MammothModels })
            {
                var p = models.PrefabV2; if (!p) continue;
                foreach (var r in p.GetComponentsInChildren<Renderer>(true))
                    foreach (var sm in r.sharedMaterials)
                    {
                        if (!sm) continue;
                        int i = System.Array.IndexOf(MatNames, sm.name);
                        if (i >= 0 && !m[i]) m[i] = sm;
                    }
            }
            var g = new Texture[m.Length];
            for (int i = 0; i < m.Length; i++)
            {
                if (!m[i] || !m[i].HasProperty(BaseMapId)) { Debug.LogWarning("[OzBigTankPaint] material " + MatNames[i] + " not found"); return false; }
                g[i] = m[i].GetTexture(BaseMapId);
            }
            mats = m; own = g; Clean();                                                                          // editor: notes which were already marked changed
            return true;
        }

        static bool LoadGreen()
        {
            if (green != null) return true;
            var d = new Texture2D[TexNames.Length];
            for (int i = 0; i < d.Length; i++)
                if (!(d[i] = Resources.Load<Texture2D>(Dir + TexNames[i]))) { Debug.LogWarning("[OzBigTankPaint] missing Resources/" + Dir + TexNames[i]); return false; }
            green = d;
            return true;
        }

        /// <summary>DESERT (true, the default) or the old GREEN (false) on every big tank now and later. False when the maps or materials
        /// are missing (nothing changed).</summary>
        public static bool Set(bool toDesert)
        {
            if (toDesert == Desert) return true;
            if (!Find() || (!toDesert && !LoadGreen())) return false;
            for (int i = 0; i < mats.Length; i++) if (mats[i]) mats[i].SetTexture(BaseMapId, toDesert ? own[i] : green[i]);
            greenNow = !toDesert; Swaps++;
            if (toDesert) Clean();
            if (!hooked)
            {
                hooked = true; Application.quitting += Restore;                                                // the editor calls it on leaving Play too
#if UNITY_EDITOR
                UnityEditor.EditorApplication.playModeStateChanged += s => { if (s == UnityEditor.PlayModeStateChange.ExitingPlayMode) Restore(); };
#endif
            }
            Debug.Log("[OzBigTankPaint] big tank paint " + (toDesert ? "DESERT" : "GREEN"));
            return true;
        }

        public static bool Toggle() => Set(!Desert);

        /// <summary>The materials' own maps back (DESERT): the in-memory materials equal the .mat files again (editor: and are not marked
        /// changed, so no later save of the project writes them).</summary>
        public static void Restore()
        {
            if (mats != null && own != null) for (int i = 0; i < mats.Length; i++) if (mats[i]) mats[i].SetTexture(BaseMapId, own[i]);
            greenNow = false;
            Clean();
        }

        static bool[] wasDirty;
        static void Clean()
        {
#if UNITY_EDITOR
            if (mats == null) return;
            if (wasDirty == null) { wasDirty = new bool[mats.Length]; for (int i = 0; i < mats.Length; i++) wasDirty[i] = mats[i] && UnityEditor.EditorUtility.IsDirty(mats[i]); return; }
            if (!greenNow) for (int i = 0; i < mats.Length; i++) if (mats[i] && !wasDirty[i]) UnityEditor.EditorUtility.ClearDirty(mats[i]);
#endif
        }
    }
}
