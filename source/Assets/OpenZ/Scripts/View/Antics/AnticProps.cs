// OpenZ (GPL-3.0)
// plays the phone or the rocket-fuel drink (AnticDirector.FlyoverAll.cs): a hundred and more phones and cans come and go in a few seconds.
// Instead of an Instantiate and a Destroy for each, a holder that leaves the hand (its pop-out played as always) goes to a pool and the next
// antic takes it from there: re-parented onto the socket, its renderers on, the phone's home screen back. A holder made for something that
// takes it over as a free body (the crushed can the robot throws: AnticCan adds its physics and disposes of it) is never pooled; a holder
// without the pool mark is destroyed as before. The pool lives in the level scene and goes with it. View only.
using System.Collections.Generic;
using UnityEngine;

namespace OpenZ
{
    public static class AnticProps
    {
        /// <summary>Off = every prop is made and destroyed as before.</summary>
        public static bool Pool = true;
        public static int Taken, Made, Reused, Returned;

        sealed class Pooled : MonoBehaviour { public GameObject model; }
        static Transform root; static readonly List<Pooled> free = new List<Pooled>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { root = null; free.Clear(); Taken = Made = Reused = Returned = 0; }

        /// <summary>A holder with `model` under it (AnticFx.Make's shape): a pooled one when the pool has one of this model, else a new one.</summary>
        public static Transform Take(GameObject model, string name, Material mat)
        {
            if (!model) return null;
            Taken++;
            if (Pool)
                for (int i = free.Count - 1; i >= 0; i--)
                {
                    var p = free[i];
                    if (!p) { free.RemoveAt(i); continue; }
                    if (p.model != model) continue;
                    free.RemoveAt(i); Reused++;
                    var t = p.transform;
                    t.SetParent(null, false); t.gameObject.name = name; t.gameObject.SetActive(true);
                    foreach (var r in t.GetComponentsInChildren<Renderer>(true)) r.enabled = true;
                    var kit = AnticDirector.Kit;
                    if (kit && kit.phoneOwnMats && model == kit.phone && kit.phoneScreenHome)
                    {   // the last run may have left the call screen on
                        var st = AnticFx.Find(t, "Screen"); var sr = st ? st.GetComponent<Renderer>() : null;
                        if (sr) sr.sharedMaterial = kit.phoneScreenHome;
                    }
                    return t;
                }
            var h = AnticFx.Make(model, name, mat);
            if (!h) return null;
            Made++;
            if (Pool) h.gameObject.AddComponent<Pooled>().model = model;
            return h;
        }

        /// <summary>The prop leaves the hand: popped out as always (puff + pop when `fx`), then back to the pool; destroyed when it is not pooled.</summary>
        public static void PopAway(Transform t, bool fx, string what, int emitter)
        {
            if (!t) return;
            var p = t.GetComponent<Pooled>();
            if (!p) { AnticFx.PopOut(t, true, fx, what, emitter); return; }
            AnticFx.PopOut(t, false, fx, what, emitter, () => Return(p));
        }

        /// <summary>Gone at once, no pop (a death, a removal, the full can swapped for the crushed one): back to the pool, or destroyed.</summary>
        public static void Release(Transform t)
        {
            if (!t) return;
            var p = t.GetComponent<Pooled>();
            if (!p) { Object.Destroy(t.gameObject); return; }
            Return(p);
        }

        static void Return(Pooled p)
        {
            if (!p) return;
            if (!root) root = new GameObject("AnticProps (pool)").transform;
            var t = p.transform;
            t.SetParent(root, false); t.localPosition = Vector3.zero; t.localRotation = Quaternion.identity; t.localScale = Vector3.one;
            t.gameObject.SetActive(false);
            Returned++;
            if (!free.Contains(p)) free.Add(p);
        }

        /// <summary>Play check: one line.</summary>
        public static string Status() => $"prop pool {(Pool ? "on" : "OFF")}: taken {Taken} (made {Made}, reused {Reused}), returned {Returned}, free now {free.Count}";
    }
}
