using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// The drop preview (View/OzUePlace.cs Preview) stands on the very surface the drop will stand on:
    /// <list type="bullet">
    /// <item><see cref="OnSurface"/>: on a ramp platform (deck, ramp, rails) the rules' own height there (ZWorld.PlatformHeightM: the numbers the
    /// walk deck is built from and the placed flag's pole foot, View/FlagView.Deck.cs DeckFoot), elsewhere what it was (the ground). The ring, the
    /// flag's ghost, the see-through models and the words all take it; with no platform on the field nothing changes (every level).</item>
    /// <item><see cref="DeckRay"/>: the pointer's point (FloorPoint) for a camera ray that passes through a platform but whose walk-collider pick
    /// landed on the floor beyond it (the deck's walk surface not hit) is the ray's first point on the platform: the preview sits under the mouse.</item>
    /// <item><see cref="FlagGhost"/>: the flag's ghost in the world, a pole of the flag's height with its pennant, standing on that surface, so the
    /// preview shows the height it will stand at (the words already kept its room: Preview's tallM 3.6 m).</item>
    /// </list>
    /// The hovered platform's own marks (its footprint outline on the FLOOR, its bar and words) are not drawn while a tile is armed or dragged
    /// (View/OzUeMarks.Platforms.cs): they lay on the drop's ring and their floor outline read as the preview at ground level.
    /// </summary>
    internal sealed partial class OzUePlaceDriver
    {
        /// <summary>The flag ghost's pole (m): the room the drop's words keep for a flag (Preview's tallM).</summary>
        internal const float FlagPoleM = 3.6f;
        const float ClothM = 1.2f;
        /// <summary>Checks (View/OzUeFlagDeckCheck.cs G1): preview points lifted onto a platform, pointer rays taken onto a platform, flag ghosts drawn;
        /// the last frame's preview centre (with a platform on the field) and the last flag ghost's foot height.</summary>
        internal static int SurfaceLifts, DeckRayFixes, FlagGhosts;
        internal static Vector3 LastCentre; internal static float LastFlagFootY;

        /// <summary>The height a drop stands on at p: on a ramp platform the rules' height there, else `other` (the caller's ground).</summary>
        internal static float OnSurface(ZGame g, Vector3 p, float other)
        {
            var w = g ? g.World : null;
            if (w == null || !w.PlatformTablesMade || OzUePlatformViews.Count == 0) return other;
            var px = ZGame.WorldToPx(p);
            if (w.PlatformAtTile(px.x >> 4, px.y >> 4) == 0) return other;
            SurfaceLifts++;
            return (float)w.PlatformHeightM(px.x, px.y);
        }

        /// <summary>FloorPoint: the ground pick p of this ray is off every platform, but the ray passes through one first: p becomes the ray's first
        /// point in it (marched from the highest deck's height down to p in 0.25 m steps, then halved down to under 1 mm). Else p stays as it is.</summary>
        internal static void DeckRay(ZGame g, Ray ray, ref Vector3 p)
        {
            var w = g ? g.World : null;
            if (w == null || !w.PlatformTablesMade || OzUePlatformViews.Count == 0) return;
            if (Inside(w, p, float.MaxValue)) return;                                                     // the pick is on a platform already (its walk deck, or MouseRay's spot)
            Vector3 o = ray.origin, d = ray.direction;
            if (!(d.y < -1e-3f)) return;
            double topM = 0.0; foreach (var h in ZWorld.PlatformDeckM) if (h > topM) topM = h;
            float t0 = Mathf.Max(0f, ((float)topM + 0.05f - o.y) / d.y), t1 = Vector3.Dot(p - o, d);
            if (!(t1 > t0)) return;
            const float Step = 0.25f;
            float before = t0;
            for (float t = t0; t <= t1; t += Step)
            {
                if (!Inside(w, o + d * t, -1f)) { before = t; continue; }
                float lo = before, hi = t;
                for (int i = 0; i < 10; i++) { float m = (lo + hi) * 0.5f; if (Inside(w, o + d * m, -1f)) hi = m; else lo = m; }
                p = o + d * hi; DeckRayFixes++;
                return;
            }
        }

        /// <summary>q on a platform tile and (yMax &lt; 0: always) at or under its rules' height there; yMax = float.MaxValue: on a platform tile at all.</summary>
        static bool Inside(ZWorld w, Vector3 q, float yMax)
        {
            var px = ZGame.WorldToPx(q);
            if (w.PlatformAtTile(px.x >> 4, px.y >> 4) == 0) return false;
            return yMax == float.MaxValue || q.y <= (float)w.PlatformHeightM(px.x, px.y) + 0.001f;
        }

        /// <summary>The flag's ghost: its pole from `foot` up FlagPoleM and a pennant to the screen's right, in the drop ring's colour (red: no room).</summary>
        void FlagGhost(Vector3 foot, Color ring)
        {
            var cam = OzUeDraw.Cam;
            Vector3 side = cam ? cam.transform.right : Vector3.right; side.y = 0f;
            if (side.sqrMagnitude < 1e-4f) side = Vector3.right;
            side.Normalize();
            Color c = OzUi.A(ring, 0.8f);
            Vector3 top = foot + Vector3.up * FlagPoleM, tip = foot + Vector3.up * (FlagPoleM - ClothM * 0.5f) + side * ClothM, low = foot + Vector3.up * (FlagPoleM - ClothM);
            OzUeDraw.Up(foot, FlagPoleM, c, 1.5f);
            OzUeDraw.Line(top, tip, c, 0f, 0f, 1.5f);
            OzUeDraw.Line(tip, low, c, 0f, 0f, 1.5f);
            FlagGhosts++; LastFlagFootY = foot.y;
        }
    }
}
