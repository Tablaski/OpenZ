using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.5 DV190 (kit shuttle_nearest_landing) on the player's side; the rule: Sim/ZWorld.ShuttleNearestLanding.cs (switch
    /// ZWorld.ShuttleNearestLandingDeviation; off = everything here returns at once and the cursor, the mark and the click are as before).
    ///  * THE CURSOR and the ship's red mark (OzShuttleOrderAim.NeverAt): a place where the ship can never land shows the move cursor when the
    ///    rule finds a landing spot within its radius (the click flies it there); the NO cursor only when there is none. The search is cached
    ///    per tile and ship (OzShuttleOrderAim.CacheSec) and spread over frames (at most <see cref="BudgetMs"/> a frame); until its answer is
    ///    in, the move cursor (the answer for almost every place).
    ///  * THE CLICK (OzShuttleOrderAim.Filter): a log line for a click sent to the nearest landing spot; a refused one (nothing within the
    ///    radius) keeps its refusal blip and shows the short toast <see cref="NoPlaceText"/> (UI only, the HUD's own plate, no new art).
    /// </summary>
    public static class OzShuttleNearestAim
    {
        /// <summary>The toast of a refused click.</summary>
        public const string NoPlaceText = "NO PLACE TO LAND HERE";
        /// <summary>The search's time a frame (ms; at least one place is tried when the frame's budget is not spent yet).</summary>
        public static float BudgetMs = 1.5f;
        /// <summary>Proof counters: clicks sent to the nearest landing spot, refused clicks that showed the toast.</summary>
        public static int Redirected, NoPlace;

        sealed class Near { public int k, held = -1; public int state; public bool has, none; public float at; }   // state: 0 = searching, 1 = found, 2 = none
        static readonly Dictionary<long, Near> cache = new Dictionary<long, Near>();
        static ZWorld cacheWorld; static int frameAt = -1;
        static readonly System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();

        /// <summary>OzShuttleOrderAim.NeverAt, for a place where ship s can never land: true = no landing spot within the rule's radius either
        /// (the NO cursor, the red mark). The switch off: true (the old answer stands).</summary>
        public static bool NoneNear(ZWorld w, ZObject s, int x, int y)
        {
            if (!ZWorld.ShuttleNearestLandingDeviation || w == null || s == null) return true;
            float now = Time.unscaledTime; int frame = Time.frameCount;
            if (w != cacheWorld || cache.Count > 512) { cache.Clear(); cacheWorld = w; }
            long key = ((long)s.Id << 32) | ((long)((x >> 4) & 0xFFFF) << 16) | (long)((y >> 4) & 0xFFFF);
            if (!cache.TryGetValue(key, out var e)) { e = new Near(); cache[key] = e; }
            if (e.state != 0 && now - e.at >= OzShuttleOrderAim.CacheSec) { e.state = 0; e.k = 0; e.held = -1; }       // asked again; the last answer shown meanwhile
            if (e.state == 0)
            {
                if (frame != frameAt) { frameAt = frame; sw.Restart(); }
                while (e.state == 0 && sw.Elapsed.TotalMilliseconds < BudgetMs)
                {
                    int r = w.ShuttleNearestStep(x, y, s, false, ref e.k, ref e.held, 1, out _, out _);
                    if (r != 0) { e.state = r; e.has = true; e.none = r == 2; e.at = now; }
                }
            }
            return e.has && e.none;
        }

        /// <summary>OzShuttleOrderAim.Filter, right after ZWorld.ShuttleOrdersFilter (r0 = ZWorld.ShuttleNearestRedirects before it, refused =
        /// the ships it took out of the click).</summary>
        public static void Clicked(int r0, int refused)
        {
            if (!ZWorld.ShuttleNearestLandingDeviation) return;
            int d = ZWorld.ShuttleNearestRedirects - r0;
            if (d > 0)
            {
                Redirected += d;
                Debug.Log($"[OpenZ Orders] shuttle: it cannot land at ({ZWorld.ShuttleNearestLastFromX},{ZWorld.ShuttleNearestLastFromY}) ({ZWorld.ShuttleNearestLastWhy}): {d} ship(s) sent to the nearest place where it can, ({ZWorld.ShuttleNearestLastToX},{ZWorld.ShuttleNearestLastToY}) (DV190)");
            }
            if (refused <= 0) return;
            NoPlace++;
            var hud = OpenZHud.I;
            if (hud) hud.Toast(NoPlaceText, "alert", false, null, true, 2.2f);                                   // the plate only: the refusal blip is the click's own sound
        }
    }
}
