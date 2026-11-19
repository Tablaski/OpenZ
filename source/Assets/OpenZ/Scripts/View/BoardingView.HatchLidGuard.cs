// OpenZ (GPL-3.0) - version 1.5 kit hatch_lid: the guard's pass over BoardingView's rigs (called by View/OzHatchLidGuard.cs).
using System.Collections.Generic;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// held every frame for each rig of a hatch vehicle (ZObject.IsHatchVehicle: the light, medium and heavy tank and the mech) whose view is
    /// shown (active, not dying, not destroyed):
    /// <list type="bullet">
    /// <item>every lid part (each mesh renderer under the rig's hinge that has a mesh, read once per hinge the first time it is seen; a part
    /// RoundHatch.Build is taking out has its mesh cleared and is never one of them) is active, its renderer enabled, its scale and the
    /// hinge's scale as they were when first seen;</item>
    /// <item>the hinge stands on the lid's own pose (StepRig's: rest x the open share's turn about x, 0 closed .. hatchOpenDeg open) within
    /// <see cref="LidPoseTolDeg"/>; an open share that is not a number (it would stay so: MoveTowards keeps a NaN) is set closed.</item>
    /// </list>
    /// What it puts back is counted and logged (the first 12 lines a Play). Nothing else is changed: the fog of war's and the camera's
    /// per-picture holds (Renderer.forceRenderingOff, set and given back inside a camera's own picture) are not read or touched here; a dying
    /// hull (its turret thrown, the wreck) is left to its death, and a hull hidden WHOLE by its own code (no body part drawn: the level 16
    /// ending scene) is left hidden, lid and all (review fix). With the fix in (View/OzHatchLid.cs) the guard has nothing to do: its counters
    /// stay 0 in the Play check's normal run (View/OzHatchLidCheck.cs).
    /// </summary>
    public partial class BoardingView
    {
        /// <summary>The hinge may stand this far (deg) from the lid's own pose before it is put back on it.</summary>
        public static float LidPoseTolDeg = 2f;
        /// <summary>Proof counters (a Play): passes made; repairs: a lid part (or a node between it and its hinge) switched on again, a lid
        /// renderer enabled again, a scale put back, the hinge put back on the lid's pose, an open share that was not a number set closed; lid
        /// parts found destroyed (they cannot be put back); lines logged; the last repair in words.</summary>
        public static int LidGuardPasses, LidGuardShown, LidGuardEnabled, LidGuardScale, LidGuardPose, LidGuardNaN, LidGuardGone, LidGuardLogged;
        /// <summary>Review fix (9 Nov): hull-frames left alone because the WHOLE hull was hidden by its own code (no body part drawn: the
        /// level 16 ending scene switches every renderer of the units near its set off, View/Zod/ZodBriefing.EndingScene.cs; a melt / sink
        /// hide). The guard only puts a lid back on a hull that is drawn, so it never draws a lone lid where the hull is meant to be hidden.</summary>
        public static int LidGuardHullHidden;
        public static string LidGuardLast = "";
        /// <summary>All repairs made (the check's one number).</summary>
        public static int LidGuardRepairs => LidGuardShown + LidGuardEnabled + LidGuardScale + LidGuardPose + LidGuardNaN;
        /// <summary>At most this many body parts (opaque mesh renderers of the hull outside the hinge) are kept per hull for the drawn test.</summary>
        const int LidBodyMax = 16;

        sealed class LidSet
        {
            public Transform hinge; public Vector3 hingeScale;
            public readonly List<Renderer> rends = new List<Renderer>();
            public readonly List<Vector3> scales = new List<Vector3>();
            public readonly List<Renderer> body = new List<Renderer>();                                   // review fix: the hull's own parts (not the lid), read when first drawn
        }
        readonly Dictionary<int, LidSet> lidSets = new Dictionary<int, LidSet>();                         // by the hinge's instance id
        static readonly List<Renderer> lidScan = new List<Renderer>();
        static readonly List<int> lidGone = new List<int>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void LidGuardReset()
        {
            LidGuardPasses = LidGuardShown = LidGuardEnabled = LidGuardScale = LidGuardPose = LidGuardNaN = LidGuardGone = LidGuardLogged = LidGuardHullHidden = 0;
            LidGuardLast = "";
        }

        /// <summary>OzHatchLidGuard.LateUpdate (order 97): the invariant above for every shown hatch vehicle.</summary>
        internal void HatchLidGuardPass()
        {
            if (!OzHatchLid.GuardOn || rigs.Count == 0) return;
            LidGuardPasses++;
            foreach (var kv in rigs)
            {
                var r = kv.Value;
                if (r == null || !r.hinge || !r.view || r.obj == null || !r.obj.IsHatchVehicle || r.obj.Destroyed || r.view.Dying || !r.view.gameObject.activeInHierarchy) continue;
                var s = LidSetOf(r);
                if (!HullDrawn(s, r)) { LidGuardHullHidden++; continue; }                                 // review fix: the whole hull hidden by its own code: leave the lid with it
                // 1. the lid drawn, at its own size
                for (int i = s.rends.Count - 1; i >= 0; i--)
                {
                    var rr = s.rends[i];
                    if (!rr) { s.rends.RemoveAt(i); s.scales.RemoveAt(i); LidGuardGone++; LidNote(r, "a lid part was destroyed (it cannot be put back)"); continue; }
                    if (!rr.gameObject.activeInHierarchy)
                    {
                        for (var t = rr.transform; t && t != r.hinge.parent; t = t.parent) if (!t.gameObject.activeSelf) t.gameObject.SetActive(true);
                        if (rr.gameObject.activeInHierarchy) { LidGuardShown++; LidNote(r, rr.name + " was switched off"); }
                    }
                    if (!rr.enabled) { rr.enabled = true; LidGuardEnabled++; LidNote(r, rr.name + "'s renderer was off"); }
                    if (BadScale(rr.transform.localScale) && !BadScale(s.scales[i])) { rr.transform.localScale = s.scales[i]; LidGuardScale++; LidNote(r, rr.name + " was scaled to nothing"); }
                }
                if (BadScale(r.hinge.localScale) && !BadScale(s.hingeScale)) { r.hinge.localScale = s.hingeScale; LidGuardScale++; LidNote(r, "its hinge was scaled to nothing"); }
                // 2. the hinge on the lid's own pose, closed .. open
                float h = r.hatch;
                if (float.IsNaN(h) || float.IsInfinity(h)) { r.hatch = h = 0f; LidGuardNaN++; LidNote(r, "its lid's open share was not a number: set closed"); }
                else if (h < 0f || h > 1f) r.hatch = h = Mathf.Clamp01(h);
                if (!Finite(r.hingeRest)) continue;                                                       // no pose to put it back on (never seen)
                var want = r.hingeRest * Quaternion.AngleAxis(r.hingeSign * hatchOpenDeg * Mathf.SmoothStep(0f, 1f, h), Vector3.right);
                var q = r.hinge.localRotation;
                if (!Finite(q) || Quaternion.Angle(q, want) > LidPoseTolDeg) { r.hinge.localRotation = want; LidGuardPose++; LidNote(r, "its hinge stood off the lid's pose"); }
            }
            if (lidSets.Count > rigs.Count + 64) LidSetPurge();
        }

        /// <summary>The lid parts under this rig's hinge, read the first time the hinge is seen (kept by the hinge, so a rig made again keeps them).</summary>
        LidSet LidSetOf(Rig r)
        {
            int key = r.hinge.GetInstanceID();
            if (lidSets.TryGetValue(key, out var s) && s != null && s.hinge == r.hinge) return s;
            s = new LidSet { hinge = r.hinge, hingeScale = r.hinge.localScale };
            lidScan.Clear(); r.hinge.GetComponentsInChildren(true, lidScan);
            for (int i = 0; i < lidScan.Count; i++)
            {
                var rr = lidScan[i];
                if (rr is MeshRenderer) { var mf = rr.GetComponent<MeshFilter>(); if (!mf || !mf.sharedMesh) continue; }      // a part being taken out (RoundHatch.Build clears its mesh first)
                else if (rr is SkinnedMeshRenderer sk) { if (!sk.sharedMesh) continue; }
                else continue;                                                                             // particles, trails, lines: not the lid
                s.rends.Add(rr); s.scales.Add(rr.transform.localScale);
            }
            lidScan.Clear();
            lidSets[key] = s;
            return s;
        }

        /// <summary>Review fix: true when the hull itself is drawn (one of its own opaque body parts outside the hinge is active and enabled).
        /// The body parts are read the first time the hull is seen drawn; while none was found yet (a hull first seen hidden) it reads again.
        /// The knock-off takes at most 3 parts a unit, so a hull that lost its lid always has body parts left drawn.</summary>
        bool HullDrawn(LidSet s, Rig r)
        {
            if (s.body.Count == 0)
            {
                var root = r.view.transform;
                lidScan.Clear(); root.GetComponentsInChildren(false, lidScan);
                for (int i = 0; i < lidScan.Count && s.body.Count < LidBodyMax; i++)
                {
                    var b = lidScan[i];
                    if (!(b is MeshRenderer) || !b.enabled || b.transform.IsChildOf(r.hinge)) continue;
                    if (b.sharedMaterial && b.sharedMaterial.renderQueue >= 2450) continue;                   // glows, lenses, decals: not the hull
                    var mf = b.GetComponent<MeshFilter>(); if (!mf || !mf.sharedMesh) continue;
                    s.body.Add(b);
                }
                lidScan.Clear();
            }
            // Unity pass 1.5: a hull counts as drawn only when at least half of its body parts are drawn (the knock-off takes at most 3 of
            // up to 16); one part switched back on by its own code while the hull is hidden whole no longer brings a lone lid back.
            int drawn = 0, known = 0;
            for (int i = 0; i < s.body.Count; i++) { var b = s.body[i]; if (!b) continue; known++; if (b.enabled && b.gameObject.activeInHierarchy) drawn++; }
            return drawn > 0 && drawn * 2 >= known;
        }

        void LidSetPurge()
        {
            lidGone.Clear();
            foreach (var kv in lidSets) if (kv.Value == null || !kv.Value.hinge) lidGone.Add(kv.Key);
            for (int i = 0; i < lidGone.Count; i++) lidSets.Remove(lidGone[i]);
            lidGone.Clear();
        }

        void LidNote(Rig r, string what)
        {
            LidGuardLast = (r.obj != null ? r.obj.Type + " #" + r.obj.Id : "?") + ": " + what;
            if (LidGuardLogged++ < 12) Debug.Log("[OpenZ] hatch lid put back: " + LidGuardLast);
        }

        static bool BadScale(Vector3 v) => !(Mathf.Abs(v.x) > 1e-4f && Mathf.Abs(v.y) > 1e-4f && Mathf.Abs(v.z) > 1e-4f) || float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z);

        static bool Finite(Quaternion q)
        {
            if (float.IsNaN(q.x) || float.IsNaN(q.y) || float.IsNaN(q.z) || float.IsNaN(q.w)) return false;
            if (float.IsInfinity(q.x) || float.IsInfinity(q.y) || float.IsInfinity(q.z) || float.IsInfinity(q.w)) return false;
            return q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w > 1e-6f;
        }

        /// <summary>The Play check (View/OzHatchLidCheck.cs): the lid's open share BoardingView draws for vehicle `vehicleId` (0 shut .. 1 open);
        /// false = no rig for it yet.</summary>
        public static bool HatchLidOpenShare(int vehicleId, out float open)
        {
            open = 0f;
            if (!I || I.game == null || !I.game.Views.TryGetValue(vehicleId, out var v) || !v || !I.rigs.TryGetValue(v, out var r)) return false;
            open = r.hatch; return true;
        }
    }
}
