// OpenZ (GPL-3.0)
// the level's own jungle beyond the rim's edge (it stood on nothing, "floating side trees"), so the flybys' low shots showed bare
// hills where the approved cuts had a thick canopy. Now every one of those plants (the level's own trees, palms and bushes, the same
// prefabs and the same mix the old far jungle had) comes back, each one SEATED on the land round the map: its foot on the land's
// drawn height, sunk FootSink, never above the land anywhere round its trunk (16 points on a ring of its own trunk size), never
// in a pool and never on a slope steeper than JungleSinkMax allows. A plant that cannot stand there stays hidden. The plants of the
// rim's own floor (within GroundMargin) in the cells round 4 hid come back where they stood (they have the level's floor).
// They are drawn at every distance by their near shapes (no far mesh: the cells' joined far meshes still hold the old heights),
// and they stand back with the land in the start flyover's low look, as the land does. Trees inside the map are not touched.
// Drawn only: no rule reads any of it. Play check: return OpenZ.OzL16Outskirts.JungleCheck();
using System.Collections.Generic;
using UnityEngine;

namespace OpenZ
{
    public sealed partial class OzL16Outskirts
    {
        /// <summary>Round 6 on (default). Off = round 5 as it was (set before the level starts).</summary>
        public static bool JungleOn = true;
        /// <summary>Plants at most this far beyond the rim's edge come back (m), and the most a foot may sink under the land's middle (m).</summary>
        public static float JungleOut = 230f, JungleSinkMax = 0.2f;   // run 8: = FootMax (round 5's limit): sunk only slightly
        /// <summary>Counters: plants seated on the land, restored on the rim's floor, left hidden (pool, slope, off the land's grid), and the
        /// worst foot found: deepest under the land's middle, highest over the land's lowest point round the trunk (must stay at or under 0).</summary>
        public static int JungleSeated, JungleRim, JungleRefused, JungleFloating, JungleCells; public static float JungleDeepest, JungleHighest = -1f;
        static readonly HashSet<Renderer> seatedPlants = new HashSet<Renderer>();
        readonly List<GameObject> jungle = new List<GameObject>();   // the plants seated on the outskirts land: they stand back with it in the low look
        readonly HashSet<Renderer> rimBack = new HashSet<Renderer>();   // the rim's own plants brought back: always drawn (the low look's jungle edge, as the old flybys had it)

        static void ResetJungleStatics() { JungleSeated = JungleRim = JungleRefused = JungleFloating = JungleCells = 0; JungleDeepest = 0f; JungleHighest = -1f; seatedPlants.Clear(); }

        /// <summary>A plant round 6 brought back (seated on the land or restored on the rim's floor).</summary>
        public static bool IsSeated(Renderer r) => r != null && seatedPlants.Contains(r);

        /// <summary>The build's last step: every plant beyond the rim's edge that rounds 4 / 5 hid comes back seated on the land.</summary>
        void SeatJungle()
        {
            ResetJungleStatics(); jungle.Clear(); rimBack.Clear();
            if (!JungleOn) return;
            var root = GameObject.Find(FloraRoot); if (!root) return;
            float m = OzL16FlyoverFlora.GroundMargin;
            foreach (Transform cell in root.transform)
            {
                var lg = cell.GetComponent<LODGroup>();
                var back = new List<Renderer>();
                foreach (var r in cell.GetComponentsInChildren<Renderer>(true))
                {
                    if (!r || r.name == "FAR") continue;
                    if (r.gameObject.activeInHierarchy && r.enabled) continue;                        // drawn already (the rim's plants, round 5's trees)
                    if (!(r is MeshRenderer)) continue;
                    var t = r.transform; var p = t.position; float o = Outside(p);
                    if (o <= m + 0.01f) { back.Add(r); rimBack.Add(r); JungleRim++; continue; }        // the rim's own floor: back where it stood (drawn in the low look too: it has the level's floor)
                    if (o > m + JungleOut || gh == null) { JungleRefused++; continue; }
                    float fi = (p.x - gx0) / Cell, fj = (p.z - gz0) / Cell;
                    if (fi < 1f || fj < 1f || fi > gnx - 2 || fj > gnz - 2) { JungleRefused++; continue; }   // off the land's grid
                    var bb = r.bounds;
                    float ring = Mathf.Clamp(Mathf.Min(bb.extents.x, bb.extents.z) * 0.18f, FootRing, 1.4f);
                    float h = Gen(p.x, p.z), lo = h;
                    for (int k = 0; k < 16; k++) { float a = k * Mathf.PI / 8f; lo = Mathf.Min(lo, Gen(p.x + Mathf.Cos(a) * ring, p.z + Mathf.Sin(a) * ring)); }
                    if (h < waterY + PoolClear || lo < waterY + 0.1f) { JungleRefused++; continue; }    // in a pool
                    float y = Mathf.Min(h - FootSink, lo - 0.02f), sunk = h - y, gap = y - lo;
                    if (sunk > JungleSinkMax) { JungleRefused++; continue; }                           // a slope too steep for its trunk
                    t.position = new Vector3(p.x, p.y + (y - FootY(t, bb)), p.z);
                    if (gap > 0.001f) JungleFloating++;
                    JungleDeepest = Mathf.Max(JungleDeepest, sunk); JungleHighest = Mathf.Max(JungleHighest, gap);
                    back.Add(r); JungleSeated++;
                }
                if (back.Count == 0) continue;
                foreach (var r in back) { r.gameObject.SetActive(true); r.enabled = true; seatedPlants.Add(r); if (!rimBack.Contains(r)) jungle.Add(r.gameObject); }
                if (lg)
                {   // drawn by their near shapes at every distance: in the cell's first level and its last one (its far mesh keeps the old heights)
                    var lods = lg.GetLODs();
                    if (lods.Length > 0)
                    {
                        var near = new List<Renderer>(); if (lods[0].renderers != null) foreach (var x in lods[0].renderers) if (x) near.Add(x);
                        foreach (var r in back) if (!near.Contains(r)) near.Add(r);
                        lods[0].renderers = near.ToArray();
                        if (lods.Length > 1)
                        {
                            int last = lods.Length - 1; var far = new List<Renderer>();
                            if (lods[last].renderers != null) foreach (var x in lods[last].renderers) if (x && x.enabled && x.gameObject.activeInHierarchy) far.Add(x);
                            foreach (var r in back) if (!far.Contains(r)) far.Add(r);
                            lods[last].renderers = far.ToArray();
                        }
                        lg.SetLODs(lods); lg.RecalculateBounds();
                    }
                }
                JungleCells++;
            }
            Debug.Log($"[OzL16Outskirts] round 6: {JungleSeated} plants of the level's jungle beyond the rim seated on the land, {JungleRim} back on the rim's floor, " +
                      $"{JungleRefused} left hidden (pool, slope, off the land), in {JungleCells} cells; feet at most {JungleDeepest:0.00} m under the land, " +
                      $"highest over the land round a trunk {JungleHighest:0.000} m; floating {JungleFloating}");
        }

        /// <summary>The plant's foot height: its own point (the level's plants are placed by their foot), or its drawing's lowest point when
        /// that lies more than half a metre above it.</summary>
        static float FootY(Transform t, Bounds b) => b.min.y > t.position.y + 0.5f ? b.min.y : t.position.y;

        /// <summary>LateUpdate: round 6's plants stand back with the land in the low look.</summary>
        void JungleShow(bool on) { foreach (var g in jungle) if (g) g.SetActive(on); }

        /// <summary>Play check: the counts, and every seated plant measured again on the land now (floating = over the land round its trunk).</summary>
        public static string JungleCheck()
        {
            if (!I) return "OzL16Outskirts: not in this level";
            int floatNow = 0, checkedN = 0; float worst = -1f;
            foreach (var r in seatedPlants)
            {
                if (!r || Outside(r.transform.position, I.map) <= OzL16FlyoverFlora.GroundMargin + 0.01f) continue;
                var bb = r.bounds; var p = r.transform.position; float foot = FootY(r.transform, bb);
                float ring = Mathf.Clamp(Mathf.Min(bb.extents.x, bb.extents.z) * 0.18f, FootRing, 1.4f), lo = I.Gen(p.x, p.z);
                for (int k = 0; k < 16; k++) { float a = k * Mathf.PI / 8f; lo = Mathf.Min(lo, I.Gen(p.x + Mathf.Cos(a) * ring, p.z + Mathf.Sin(a) * ring)); }
                float gap = foot - lo; worst = Mathf.Max(worst, gap); if (gap > 0.001f) floatNow++; checkedN++;
            }
            return $"OzL16Outskirts round 6: {(JungleOn ? "on" : "off")}, seated {JungleSeated}, rim {JungleRim}, refused {JungleRefused}, cells {JungleCells}; " +
                   $"floating at the build {JungleFloating}, re-measured now {floatNow} of {checkedN} (highest {worst:0.000} m over the land round a trunk), deepest {JungleDeepest:0.00} m";
        }

        static float Outside(Vector3 p, Vector2 map) => Mathf.Max(Mathf.Max(-p.x, p.x - map.x), Mathf.Max(p.z, -map.y - p.z), 0f);
    }
}
