using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.2 item 73 (the GRENADES extra of the Unit editor, View/OzUePlayground.cs): the playground's grenade crate gets the levels' own view.
    /// In a level the crate is a Z_grenades object of the scene, bound in ZGame.StaticViews (ZGame.BindViews), and every crate sound and
    /// motion finds it there: the lid opening and the ammo pickup sound with the pop (GrenadeCrateV2.Picked), the collected crate's quiet
    /// end and a shot crate's burst with its pieces, its flying grenades and its break sound (GrenadeCrateV2.OnDeath, CombatView.Death), the
    /// collector's grab on the approach (GrenadeCrateV2.Update). A unit view (ZGame.SpawnUnitView, in ZGame.Views) is not found there: the
    /// crate vanished without its lid, its pop or its pickup sound, and a collected crate drew the crate blast with a rock sound. So the
    /// playground places Z_grenades as the level builder places a crate (square on its tile's centre, on the ground, 3 cm sunk, no
    /// UnitView) and binds it in StaticViews. Removing the crate (Delete, REMOVE, CLEAR FIELD, LOAD) destroys its view; CLEAR FIELD also
    /// takes the views of crates that were collected or blown up (those hide themselves, as in a level).
    /// </summary>
    public static class OzUeCrateViews
    {
        public const string PrefabName = "Z_grenades";
        /// <summary>The level builder's rule for small map items: 3 cm into the ground.</summary>
        public const float SinkM = 0.03f;
        static readonly Dictionary<int, GameObject> views = new Dictionary<int, GameObject>();
        /// <summary>Checks: crate views made, removed with their crate (Delete, REMOVE, CLEAR FIELD, LOAD), spent ones swept by CLEAR FIELD.</summary>
        public static int Made, Removed, Swept;

        /// <summary>The playground crate's view, null when it has none (or it was destroyed).</summary>
        public static GameObject Of(ZObject o) => o != null && views.TryGetValue(o.Id, out var go) && go ? go : null;

        /// <summary>Crate views the playground holds now (standing, or spent and hidden).</summary>
        public static int Count { get { int n = 0; foreach (var kv in views) if (kv.Value) n++; return n; } }

        /// <summary>The crate is drawn as in a level: its view is the one in ZGame.StaticViews (no unit view) and carries GrenadeCrateV2.</summary>
        public static bool LevelView(ZGame g, ZObject o)
        {
            var go = Of(o);
            return go && g != null && g.StaticViews.TryGetValue(o.Id, out var sv) && sv == go && !g.Views.ContainsKey(o.Id)
                   && go.GetComponent<GrenadeCrateV2>() && !go.GetComponent<UnitView>();
        }

        /// <summary>OzUePlayground.MakeOne: the crate's view, made and bound as a level's crate.</summary>
        internal static GameObject Add(ZGame g, ZObject c)
        {
            if (g == null || c == null) return null;
            var have = Of(c); if (have) return have;
            var prefab = g.Prefab(PrefabName);
            if (!prefab) { Debug.LogWarning($"{OzUnitEditor.Tag} no {PrefabName} among the playground's unit prefabs: the grenade crate #{c.Id} has no view"); return null; }
            var at = g.CenterWorld(c);                                                                      // the tile's centre, on the ground
            at.y -= SinkM;
            var root = GameObject.Find("ZodObjects");
            var go = Object.Instantiate(prefab, at, Quaternion.identity, root ? root.transform : null);
            go.name = $"{c.Type}_rt{c.Id}";
            var tp = go.GetComponent<TeamPaint>(); if (tp) tp.Team = Team.Neutral;
            g.StaticViews[c.Id] = go;
            views[c.Id] = go;
            Made++;
            return go;
        }

        /// <summary>OzUePlayground.RemoveQuiet: the crate left the field; its view goes (nothing for any other object).</summary>
        internal static void Remove(ZGame g, int id)
        {
            if (Drop(g, id)) Removed++;
        }

        /// <summary>CLEAR FIELD: the views of crates no longer in the world (collected or blown up: hidden by GrenadeCrateV2, as in a level).</summary>
        internal static int Sweep(ZGame g, ZWorld w)
        {
            var gone = new List<int>();
            foreach (var kv in views)
            {
                var o = w != null ? w.Get(kv.Key) : null;
                if (!kv.Value || o == null || o.Destroyed || o.KillAt >= 0) gone.Add(kv.Key);
            }
            int n = 0;
            foreach (int id in gone) if (Drop(g, id)) n++;
            Swept += n;
            return n;
        }

        static bool Drop(ZGame g, int id)
        {
            if (!views.TryGetValue(id, out var go)) return false;
            views.Remove(id);
            if (g != null && g.StaticViews.TryGetValue(id, out var sv) && sv == go) g.StaticViews.Remove(id);
            if (!go) return false;
            Object.Destroy(go);
            return true;
        }

        /// <summary>The editor closed: no view of a dead session is kept.</summary>
        internal static void Reset() { views.Clear(); }

        /// <summary>Checks: how often the sound log lists a played sound of this key (CombatAudio, AudioDirector) since unscaled time t0.</summary>
        public static int Played(float t0, string key)
        {
            int n = 0;
            foreach (var e in OzSoundLog.Since(t0)) if (e.key == key && e.outcome != null && e.outcome.StartsWith("PLAY", System.StringComparison.Ordinal)) n++;
            return n;
        }
    }
}
