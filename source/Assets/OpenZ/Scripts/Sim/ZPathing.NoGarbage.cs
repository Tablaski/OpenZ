// OpenZ (GPL-3.0) - version 1.4, speed-up 2 (fewer garbage-collector stutters). No rule change: the same tiles are tested in the same
// order with the same numbers; only the small objects the old lines made on every rule tick are gone (proof: the kit's headless
// fingerprints, every level, tick for tick).
//  * The way-ahead tests of the v3 zones (UpdateZones, PassLiveStep, ActivatePassages, RouteEntersZone in ZPathV3.cs) made a new
//    lambda at each call, and the locals it captured made a new closure object for every unit on every tick. Now each test is one
//    kept delegate that reads the values its caller sets just before the call. AheadHits and RouteHits call it at once and keep
//    nothing, so it reads exactly what the old closure held.
//  * SegmentHits (ZLanes.cs) made two arrays of 4 numbers for every path segment it tested. ClipEdge is the same Liang-Barsky clip,
//    one edge at a time, in the same order, with the same arithmetic.
using System;

namespace OpenZ.Sim
{
    public sealed partial class ZPathing
    {
        byte[] gcZm, gcRouteZm; double gcNow, gcRouteNow; int gcComp; ZObject gcUnit;
        Func<int, bool> gcInner, gcActive, gcSqueeze, gcStaticInner, gcPassTile, gcCompIs, gcRouteZone;

        // was: k => (zm[k] & ZInner) != 0
        Func<int, bool> HitInner(byte[] zm) { gcZm = zm; return gcInner ??= k => (gcZm[k] & ZInner) != 0; }
        // was: k => nComp[k] >= 0 && nActive[nComp[k]] >= now
        Func<int, bool> HitActive(double now) { gcNow = now; return gcActive ??= k => nComp[k] >= 0 && nActive[nComp[k]] >= gcNow; }
        // was: k => nComp[k] >= 0 && nSqueeze[nComp[k]]
        Func<int, bool> HitSqueeze() => gcSqueeze ??= k => nComp[k] >= 0 && nSqueeze[nComp[k]];
        // was: k => (zR[k] & ZInner) != 0
        Func<int, bool> HitStaticInner() => gcStaticInner ??= k => (zR[k] & ZInner) != 0;
        // was: k => PassTileFor(u, k)
        Func<int, bool> HitPassTile(ZObject u) { gcUnit = u; return gcPassTile ??= k => PassTileFor(gcUnit, k); }
        // was: k => nComp[k] == c
        Func<int, bool> HitComp(int c) { gcComp = c; return gcCompIs ??= k => nComp[k] == gcComp; }
        // was: t => (zm[t] & ZInner) != 0 || (nComp[t] >= 0 && nActive[nComp[t]] >= now)
        Func<int, bool> HitRouteZone(byte[] zm, double now)
        {
            gcRouteZm = zm; gcRouteNow = now;
            return gcRouteZone ??= t => (gcRouteZm[t] & ZInner) != 0 || (nComp[t] >= 0 && nActive[nComp[t]] >= gcRouteNow);
        }

        /// <summary>One edge of SegmentHits' Liang-Barsky clip, p and q as the old arrays held them. False: the segment misses.</summary>
        static bool ClipEdge(double p, double q, ref double t0, ref double t1)
        {
            if (Math.Abs(p) < 1e-12) return !(q < 0);
            double t = q / p;
            if (p < 0) { if (t > t1) return false; if (t > t0) t0 = t; }
            else { if (t < t0) return false; if (t < t1) t1 = t; }
            return true;
        }
    }
}
