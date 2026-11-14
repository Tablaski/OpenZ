using System.Collections.Generic;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// transition. but cap it, if zoomed out so not
    /// View only.
    /// The wrecks that sink at the end of their time (a vehicle's or a cannon's burnt hull and its landed turret, View/UnitView.Deaths.cs; the
    /// pieces of a broken-up wreck, View/WreckPieces.cs; the pieces of a broken-up fort gun, View/OzFortGunBreakup.cs) ask this gate before their
    /// sink starts; their sink itself (the smooth slide into the ground with its puff of dust) is unchanged, so nothing ever pops:
    ///  * off camera (outside the game camera's frame with <see cref="Margin"/> to spare, its own size included): it sinks at once, as before;
    ///  * on camera: it stays where it lies, and sinks as soon as the camera leaves it;
    ///  * the cap: at most <see cref="CapWrecks"/> wrecks (a vehicle's hull and its pieces count as one) and <see cref="CapRenderers"/> drawn
    ///    parts wait on camera; past either, the oldest sink even on camera, as before. Sizes from the frame-rate bench zoomed out at 165 m (L16:
    ///    5393 draw batches, 12.5 ms on the main thread, about 2.3 us a batch): 480 waiting parts cost about 1.1 ms there (12 whole wrecks of
    ///    up to 34 parts, or 8 broken-up MLRS of 57 pieces), against the 0 of a frame with every wreck sunk.
    ///  * A sink that started goes on to its end (on camera or not); with no game camera (a test without one) everything sinks as before.
    /// The global piece cap of the wreck pieces (WreckPieces.MaxLive) stays as it was. Switch: <see cref="Enabled"/> (off = the time alone, as before).
    /// </summary>
    public static class WreckSinkGate
    {
        /// <summary>The switch (off = every wreck sinks at the end of its time, wherever it is, as before).</summary>
        public static bool Enabled = true;
        /// <summary>Viewport share counted as on camera outside the frame's edges (a wreck just off the edge does not sink in view of a small scroll).</summary>
        public static float Margin = 0.08f;
        /// <summary>The cap: the most wrecks (groups) and drawn parts (renderers) that wait on camera.</summary>
        public static int CapWrecks = 12, CapRenderers = 480;

        /// <summary>One waiting wreck part (made by its first Hold).</summary>
        public sealed class Ticket
        {
            public int group, renderers, frame; public float due, radius; public Vector3 at; public bool release, listed; public GameObject owner;
        }

        static readonly List<Ticket> held = new List<Ticket>();
        static readonly List<int> order = new List<int>();
        static readonly Dictionary<int, int> groupParts = new Dictionary<int, int>();
        static int evalFrame = -1;

        /// <summary>Statistics (the Play check): parts waiting now, waiting on camera now (wrecks / parts), the most seen, released off camera / by
        /// the cap / with no camera, the longest wait (s).</summary>
        public static int Waiting => held.Count;
        public static int OnCameraWrecks, OnCameraParts, MostWrecks, MostParts, ReleasedOff, ReleasedCap, ReleasedNoCamera;
        public static float LongestWaitS;

        /// <summary>Asked every frame by a wreck part whose time to sink has come: true = wait (on camera, inside the cap), false = sink now. `group`
        /// ties a vehicle's hull and pieces into one wreck; `at` = where it lies now.</summary>
        public static bool Hold(ref Ticket t, int group, GameObject owner, Vector3 at)
        {
            if (OzLavaMelt.Live && owner && OzLavaMelt.Busy(owner.transform)) return true;                    // level 7, kit LV5: it is melting, the melt ends it
            if (!Enabled || !owner) return false;
            if (t == null)
            {
                t = new Ticket { group = group, owner = owner, due = Time.time, at = at, frame = Time.frameCount };
                var b = new Bounds(at, Vector3.zero); bool any = false; int n = 0;
                foreach (var r in owner.GetComponentsInChildren<Renderer>())
                {
                    if (!r.enabled || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                    n++; if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
                }
                t.renderers = Mathf.Max(1, n); t.radius = any ? Mathf.Max(0.3f, b.extents.magnitude) : 1f;
                held.Add(t); t.listed = true;                                                                 // the list stays in the order they came due
            }
            if (t.release) return false;
            if (!t.listed)
            {   // it stopped asking for a while (a paused game: the fort gun pieces skip their update) and left the list: back in, in the order it came due (else it waited for ever, uncounted)
                int i = 0; while (i < held.Count && held[i].due <= t.due) i++;
                held.Insert(i, t); t.listed = true;
            }
            t.at = at; t.frame = Time.frameCount;
            Evaluate();
            return !t.release;
        }

        /// <summary>Once a frame: the parts gone or no longer asking leave; off camera ones go; then the oldest on-camera wrecks past the cap.</summary>
        static void Evaluate()
        {
            int f = Time.frameCount;
            if (evalFrame == f) return;
            evalFrame = f;
            for (int i = held.Count - 1; i >= 0; i--) { var h = held[i]; if (h.release || !h.owner || f - h.frame > 2) { h.listed = false; held.RemoveAt(i); } }
            var cam = Camera.main;
            if (!cam)
            {
                foreach (var h in held) { h.release = true; ReleasedNoCamera++; }
                held.Clear(); OnCameraWrecks = OnCameraParts = 0; return;
            }
            order.Clear(); groupParts.Clear(); int parts = 0;
            foreach (var h in held)
            {
                if (!Seen(cam, h.at, h.radius)) { h.release = true; ReleasedOff++; Waited(h); continue; }
                if (!groupParts.ContainsKey(h.group)) { groupParts[h.group] = 0; order.Add(h.group); }
                groupParts[h.group] += h.renderers; parts += h.renderers;
            }
            int wrecks = order.Count, k = 0;
            while ((wrecks > CapWrecks || parts > CapRenderers) && k < order.Count)
            {
                int g = order[k++];
                foreach (var h in held) if (!h.release && h.group == g) { h.release = true; ReleasedCap++; Waited(h); }
                wrecks--; parts -= groupParts[g];
            }
            OnCameraWrecks = wrecks; OnCameraParts = parts;
            MostWrecks = Mathf.Max(MostWrecks, wrecks); MostParts = Mathf.Max(MostParts, parts);
        }

        static void Waited(Ticket h) => LongestWaitS = Mathf.Max(LongestWaitS, Time.time - h.due);

        /// <summary>On the game camera's frame with the margin, its own size counted (a sphere of `radius` m round `at`): the shared viewport test
        /// (DetailKit.OnScreen) with its margin widened by the sphere's size on screen at its distance.</summary>
        public static bool Seen(Camera cam, Vector3 at, float radius)
        {
            float z = Vector3.Dot(at - cam.transform.position, cam.transform.forward);
            float h = cam.orthographic ? 2f * cam.orthographicSize : 2f * Mathf.Max(0.1f, z) * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float ry = radius / Mathf.Max(1e-3f, h), rx = ry / Mathf.Max(0.1f, cam.aspect);
            return DetailKit.OnScreen(cam, at, Margin + Mathf.Max(rx, ry));
        }

        public static string Report() => $"WreckSinkGate: on {Enabled}; waiting {Waiting} part(s), on camera now {OnCameraWrecks} wreck(s) / {OnCameraParts} part(s) (cap {CapWrecks} / {CapRenderers}), most {MostWrecks} / {MostParts}; sank off camera {ReleasedOff}, by the cap {ReleasedCap}, with no camera {ReleasedNoCamera}; longest wait {LongestWaitS:0.0} s";
    }
}
