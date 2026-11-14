// OpenZ (GPL-3.0) - version 1.3, item 25 (view side): small registries instead of whole-scene object searches.
//  * OzLiveSet<T>: the components of one kind that exist now. The kind adds itself in Awake and leaves in OnDestroy, so the set
//    holds exactly what Object.FindObjectsByType<T> walks the whole scene for (a reader skips the ones on a hidden object, as the
//    search does). Used by AudioDirector.ScanPhysics (ragdolls, building cells, rock chunks).
//  * OzDebrisBodies: every physics body that was handed to the water landing rule or thrown as a ragdoll, the bodies DebrisAudit's
//    settle guard looks through (it filters them by the debris layer exactly as before).
//  * OzGrd: keeps a renderer off the GPU Resident Drawer where the game asks the engine whether the renderer was drawn
//    (Renderer.isVisible is only filled for renderers the engine culls itself).
// View only: no rule of the game reads any of it.
using System.Collections.Generic;
using UnityEngine;

namespace OpenZ
{
    /// <summary>The components of kind T that exist now (added in their Awake, removed in their OnDestroy). Order is not kept, as
    /// FindObjectsByType with FindObjectsSortMode.None keeps none. Read only; entries on hidden objects are the reader's to skip.</summary>
    public static class OzLiveSet<T> where T : Component
    {
        public static readonly HashSet<T> All = new HashSet<T>();
        public static void Add(T c) { if (c) All.Add(c); }
        public static void Remove(T c) { All.Remove(c); }
    }

    /// <summary>Physics bodies of debris, wrecks, chunks and ragdolls, for DebrisAudit's settle guard (it used to search the whole
    /// scene for every Rigidbody every 2 s). Fed by OzWaterLanding.Watch (every piece the water landing rule is told about) and
    /// RobotRagdoll.Launch; a body that is gone is dropped at the next Prune.</summary>
    public static class OzDebrisBodies
    {
        public static readonly HashSet<Rigidbody> All = new HashSet<Rigidbody>();
        static readonly System.Predicate<Rigidbody> Gone = rb => !rb;
        public static void Add(Rigidbody rb) { if (rb) All.Add(rb); }
        /// <summary>Drops bodies that were destroyed (returns how many).</summary>
        public static int Prune() => All.RemoveWhere(Gone);
    }

    /// <summary>The n nearest of a stream, kept in fixed arrays: the same picks in the same order as LINQ's
    /// OrderBy(key).Take(n) (a stable sort: of equal keys the one that came first stays first; keys compared with float.CompareTo,
    /// as OrderBy's default comparer does). No allocation. AudioDirector's footstep and track loops (v1.3 item 25 e).</summary>
    public static class OzNearestN
    {
        /// <summary>Offers one item with its key; best / keys hold `count` items sorted (capacity = best.Length). Returns the new count.</summary>
        public static int Insert<T>(T[] best, float[] keys, int count, T item, float key)
        {
            int cap = best.Length; if (cap == 0) return 0;
            int at = count;
            while (at > 0 && key.CompareTo(keys[at - 1]) < 0) at--;                                         // after every equal key
            if (at >= cap) return count;
            for (int j = count < cap ? count : cap - 1; j > at; j--) { best[j] = best[j - 1]; keys[j] = keys[j - 1]; }
            best[at] = item; keys[at] = key;
            return count < cap ? count + 1 : cap;
        }
    }

    /// <summary>GPU Resident Drawer helpers (v1.3 item 25 f).</summary>
    public static class OzGrd
    {
        /// <summary>The engine culls these mesh renderers itself, so Renderer.isVisible keeps working for the code that reads it
        /// (critters and animals that only animate while seen). Skinned renderers are never taken by the drawer.
        /// How: the drawer never takes a renderer that carries a property block (the same reason units, buildings and wrecks with
        /// their team paint block stay on today's path), so each mesh renderer without a block gets one small block with a value no
        /// shader reads (_OzKeepCpuCulling): the picture is the same. (MeshRenderer.allowGPUDrivenRendering, which URP's own
        /// </summary>
        public static void KeepCpuCulling(Renderer[] rs)
        {
            if (rs == null) return;
            for (int i = 0; i < rs.Length; i++)
            {
                if (!(rs[i] is MeshRenderer mr) || !mr || mr.HasPropertyBlock()) continue;                 // a renderer with a block is never taken already
                if (keep == null) { keep = new MaterialPropertyBlock(); keep.SetFloat(KeepId, 1f); }
                mr.SetPropertyBlock(keep);
            }
        }
        static MaterialPropertyBlock keep;
        static readonly int KeepId = Shader.PropertyToID("_OzKeepCpuCulling");
    }
}
