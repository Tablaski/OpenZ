using System.Text;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.Profiling;

namespace OpenZ
{
    /// <summary>
    /// The line itself: OzPlayEdge (the level's own tiles, once per map). A dot outside it is simply not
    /// drawn (alpha 0, as a dot inside another ring of its type), so a ring's dots stop at the edge, on all four sides and in the corners, and the
    /// crawl takes them in and out softly over the last half metre. The ring's cached heights (Build: ZGame.GroundY) on a hidden sample take the
    /// height of the nearest shown sample, so the last dot never climbs the slope beside it (Build lifts a dot to the higher of the two samples
    /// round it). A hill or rock inside the play area stays as it was. A blind sector's grey dot is the ring's own dot (a share of its alpha),
    /// so it goes with it; the blind ground and its side lines stay as they are (their cells are the rules' cells). View only. Nothing runs
    /// while no ring shows; nothing is allocated per frame.
    /// </summary>
    public partial class AttackRangeRings
    {
        [Header("The ring at the edge of the play area")]
        [Tooltip("The ring's dots outside the play area (border cliffs, the pockets behind them, off the map: OzPlayEdge) are not drawn")] public bool edgeClip = true;
        [Tooltip("m from the first outside tile: closer than edgeGone a dot is gone (the cliff's foot starts up to this far inside the walkable tile), from edgeFull on it is whole, eased between (at most OzPlayEdge.Reach)")]
        public float edgeGone = 1f, edgeFull = 1.5f;

        OzPlayEdge edge; ZMapData edgeMap; bool[] edgeHid;

        /// <summary>The line of this match's map, made at the first ring that needs it.</summary>
        OzPlayEdge PlayEdge
        {
            get
            {
                var m = game.World.Map;
                if (edge == null || !ReferenceEquals(edgeMap, m)) { edgeMap = m; edge = new OzPlayEdge(m.width, m.height, m.classes, m.lava); }
                return edge;
            }
        }

        /// <summary>Build's hook: a dot's alpha at world (x, z).</summary>
        byte EdgeAlpha(byte a, float x, float z)
        {
            float d = PlayEdge.Dist(x, z);
            if (d >= edgeFull) return a;
            if (d <= edgeGone) return 0;
            float t = (d - edgeGone) / (edgeFull - edgeGone);
            return (byte)Mathf.RoundToInt(a * t * t * (3f - 2f * t));
        }

        /// <summary>Build's hook, right after the heights are sampled: a sample whose dot would be gone takes the height of the nearest
        /// shown sample along the ring, so the shown dots beside it stay on their own ground. Runs only when the heights do.</summary>
        void EdgeHeights(Ring r)
        {
            var e = PlayEdge; int g = r.h.Length, shown = 0;
            if (edgeHid == null || edgeHid.Length < g) edgeHid = new bool[Mathf.NextPowerOfTwo(Mathf.Max(256, g))];
            for (int i = 0; i < g; i++)
            {
                float ga = i * 2f * Mathf.PI / g;
                bool hid = e.Dist(r.center.x + Mathf.Cos(ga) * r.curR, r.center.z + Mathf.Sin(ga) * r.curR) <= edgeGone;
                edgeHid[i] = hid; if (!hid) shown++;
            }
            if (shown == 0 || shown == g) return;
            for (int i = 0; i < g; i++)
            {
                if (!edgeHid[i]) continue;
                for (int s = 1; s < g; s++)
                {
                    int a = (i + s) % g, b = (i - s + g) % g;
                    if (!edgeHid[a]) { r.h[i] = r.h[a]; break; }
                    if (!edgeHid[b]) { r.h[i] = r.h[b]; break; }
                }
            }
        }

        // ------------------------------------------------------------------ audit (Play mode, the counted check)
        static readonly string[] EdgeFaces = { "W", "E", "N", "S", "NW", "NE", "SW", "SE" };

        /// <summary>Which side of a map of w x h tiles the tile (x, y) looks at: 0-3 W, E, N, S; 4-7 the corners (within 14 tiles of two sides).</summary>
        static int EdgeFace(int x, int y, int w, int h)
        {
            int l = x, r = w - 1 - x, t = y, b = h - 1 - y, ew = l < r ? l : r, ns = t < b ? t : b;
            if (ew <= 14 && ns <= 14) return l < r ? (t < b ? 4 : 6) : (t < b ? 5 : 7);
            if (ew < ns) return l < r ? 0 : 1;
            return t < b ? 2 : 3;
        }

        /// <summary>This level's ring at its edge: the true Begin, Timing, Build on the true ground, at the camera's zoom, on every third play
        /// tile, for a grunt's range and the longest gun's. Counted: the ring meets the edge on all four sides and in all four corners; no shown
        /// dot on an outside tile or within edgeGone of one; every dot from edgeFull on is whole; next to the edge a shown dot stands on its own
        /// ground, not on a slope's sample (0.5 % at most: a quay's drop beside a shown sample is as it was); a ring that reaches no edge is the same, bit for bit, with the rule off; nothing is hidden deep inside and a hill
        /// inside the play area keeps its dots; rebuilding allocates nothing; every gun's blind sectors audit clean and no hidden dot has a
        /// grey twin. violations = the lines that must be 0.</summary>
        public static string AuditEdge(out int violations)
        {
            violations = 0;
            var sb = new StringBuilder("the ring at the edge of the play area (AttackRangeRings.PlayEdge: OzPlayEdge, Build)\n");
            var z = I; var g = z ? (z.game ? z.game : (z.input && z.input.game ? z.input.game : ZGame.I)) : null;
            if (!z || !g || g.World == null) { violations = 1; return sb.Append("FAIL no game / no AttackRangeRings in the scene\n").ToString(); }
            z.game = g;
            bool was = z.edgeClip; z.edgeClip = true;
            var w = g.World; var map = w.Map; var e = z.PlayEdge;
            var grunt = w.S != null ? w.S.Unit("grunt") : null;
            int shortPx = grunt != null && grunt.attack_radius > 0 ? grunt.attack_radius : 120, longPx = shortPx;
            foreach (var o in w.Objects) if (o.Kind == ZKind.Cannon && o.RingRadius > longPx) longPx = o.RingRadius;
            var cam = Camera.main; var a = new Ring(); var b = new Ring();
            float age = z.expandTime + 0.5f * z.holdTime, worst = 0f;
            int[] faced = new int[8], radii = { shortPx, longPx };
            int rings = 0, hidden = 0, shown = 0, shownOut = 0, shownNear = 0, nearEdge = 0, notWhole = 0, floating = 0, inner = 0, innerDiff = 0, keptShown = 0, keptHid = 0;
            for (int ty = 0; ty < map.height; ty += 3)
                for (int tx = 0; tx < map.width; tx += 3)
                {
                    if (e.OutAt(tx, ty) || map.ClassAt(tx, ty) == ZClass.Impassable) continue;
                    var c = new Vector3(tx * OzPlayEdge.TileM + 1f, 0f, -(ty * OzPlayEdge.TileM + 1f));
                    float cd = cam ? Vector3.Distance(cam.transform.position, c) : 40f;
                    float size = Mathf.Clamp(cd / 40f, z.zoomScale.x, z.zoomScale.y);
                    float up = z.lift + 0.35f * (z.dotWidth * 0.5f * size);
                    foreach (int px in radii)
                    {
                        z.Begin(a, AuditKey + "edge", Team.Red, px, 0f); a.center = c; z.Timing(a, age, age);
                        z.Build(a, 3.7f, size);
                        rings++;
                        byte core = (byte)Mathf.RoundToInt(Mathf.Clamp01(a.a) * 255f);
                        bool reached = false;
                        for (int d = 0; d < a.n; d++)
                        {
                            var p = a.v[d * V]; byte al = a.c[d * V].a; float ds = e.Dist(p.x, p.z);
                            int ptx = Mathf.FloorToInt(p.x / OzPlayEdge.TileM), pty = Mathf.FloorToInt(-p.z / OzPlayEdge.TileM);
                            bool outer = e.OutAt(ptx, pty);
                            if (ds < OzPlayEdge.Reach) reached = true;
                            if (ds >= z.edgeFull && al != core) notWhole++;
                            if (al == 0)
                            {
                                hidden++;
                                if (outer) faced[EdgeFace(ptx, pty, map.width, map.height)]++;
                                else if (ds >= z.edgeFull) keptHid++;
                                continue;
                            }
                            shown++;
                            if (outer) shownOut++;
                            if (ds <= z.edgeGone) shownNear++;
                            if (ds < OzPlayEdge.Reach)
                            {
                                float over = p.y - up - g.GroundY(p.x, p.z);
                                nearEdge++;
                                if (over > worst) worst = over;
                                if (over > 0.8f) floating++;
                            }
                            if (map.ClassAt(ptx, pty) == ZClass.Impassable && !map.LavaAt(ptx, pty)) keptShown++;
                        }
                        if (reached || inner >= 200) continue;
                        inner++;                                                                              // reaches no edge: the same with the rule off, bit for bit
                        z.edgeClip = false;
                        z.Begin(b, AuditKey + "edge", Team.Red, px, 0f); b.center = c; z.Timing(b, age, age);
                        z.Build(b, 3.7f, size);
                        z.edgeClip = true;
                        for (int q = 0; q < a.n * V; q++) if (a.v[q] != b.v[q] || !a.c[q].Equals(b.c[q])) innerDiff++;
                    }
                }
            // rebuilding at the edge, heights included, allocates nothing (Boehm grows in blocks: a loop that allocates shows far more than this)
            long grew = -1;
            for (int ty = 0; ty < map.height && grew < 0; ty++)
                for (int tx = 0; tx < map.width && grew < 0; tx++)
                {
                    if (e.OutAt(tx, ty) || e.Dist(tx * 2f + 1f, -(ty * 2f + 1f)) >= OzPlayEdge.Reach) continue;
                    z.Begin(a, AuditKey + "edge", Team.Red, shortPx, 0f); a.center = new Vector3(tx * 2f + 1f, 0f, -(ty * 2f + 1f)); z.Timing(a, age, age);
                    z.Build(a, 1f, 1f);
                    long m0 = Profiler.GetMonoUsedSizeLong();
                    for (int q = 0; q < 400; q++) { a.hR = -1f; z.Build(a, q * 0.1f, 1f); }
                    grew = Profiler.GetMonoUsedSizeLong() - m0;
                }
            // blind sectors: every gun's ring with its sectors, as a selected gun's ring is drawn (a new ring each: the arcs are cached per ring and frame)
            int grey = 0, gunRings = 0, greyShare = 0;
            foreach (var o in w.Objects)
            {
                if (o.Kind != ZKind.Cannon || o.Destroyed || o.AttackRadius <= 0) continue;
                gunRings++;
                var r = new Ring();
                z.Begin(r, AuditKey + "edge", o.Owner, o.RingRadius, 0f); r.units.Add(o);
                r.center = g.Views.TryGetValue(o.Id, out var view) && view ? view.transform.position : g.CenterWorld(o, false); r.center.y = 0f;
                z.Timing(r, age, age);
                z.DrawBlind(r, age, 1f); z.Build(r, 3.7f, 1f);
                if (z.blinds.TryGetValue(r, out var bl))
                {
                    if (bl.greyA != null && bl.greyFrame == Time.frameCount)
                        for (int d = 0; d < r.n && d < bl.greyA.Length; d++)
                        {
                            if (bl.greyA[d] <= 0f) continue;
                            greyShare++;
                            if (e.Dist(r.v[d * V].x, r.v[d * V].z) <= z.edgeGone) grey++;
                        }
                    if (bl.mesh) Destroy(bl.mesh);
                    if (bl.greyMesh) Destroy(bl.greyMesh);
                    z.blinds.Remove(r);
                }
                if (r.mesh) Destroy(r.mesh);
            }
            AuditBlind(out int blindBad);
            if (a.mesh) Destroy(a.mesh);
            if (b.mesh) Destroy(b.mesh);
            z.edgeClip = was;

            int bad = 0;
            void Line(bool ok, string s) { if (!ok) bad++; sb.Append(ok ? "OK   " : "FAIL ").Append(s).Append('\n'); }
            var sides = new StringBuilder(); bool all = true;
            for (int i = 0; i < 8; i++) { sides.Append(EdgeFaces[i]).Append(' ').Append(faced[i]).Append("  "); all &= faced[i] > 0; }
            Line(rings > 0 && hidden > 0, rings + " rings (" + shortPx + " and " + longPx + " px): " + hidden + " dots hidden, " + shown + " shown");
            Line(all, "the ring meets the edge on every side and corner, hidden dots on outside tiles by side: " + sides);
            Line(shownOut == 0, "shown dots on an outside tile or off the map: " + shownOut);
            Line(shownNear == 0, "shown dots within " + z.edgeGone.ToString("0.0") + " m of the outside: " + shownNear);
            Line(notWhole == 0, "dots " + z.edgeFull.ToString("0.0") + " m or more from the outside that are not whole: " + notWhole);
            Line(floating * 200 <= nearEdge, "shown dots next to the edge more than 0.8 m over their own ground: " + floating + " of " + nearEdge + " (0.5 % at most), worst " + worst.ToString("0.00") + " m");
            Line(inner > 0 && innerDiff == 0, inner + " rings that reach no edge, against the rule off: " + innerDiff + " vertices or colours differ");
            Line(keptHid == 0 && (e.KeptTiles == 0 || keptShown > 0), "dots hidden deep inside the play area: " + keptHid + "; dots shown on rock inside it: " + keptShown + " (" + e.KeptTiles + " kept tiles)");
            Line(grew >= 0 && grew <= 16384, "400 rebuilds of a ring at the edge, heights included: the managed heap grew " + grew + " bytes");
            Line(blindBad == 0, "blind sectors audit (AuditBlind): " + blindBad + " cells differ");
            Line(grey == 0, gunRings + " guns' rings with their sectors: " + grey + " grey dots at hidden places (" + greyShare + " grey dots in all)");
            sb.Append("the line: ").Append(e.PlayTiles).Append(" play tiles, ").Append(e.OuterTiles).Append(" outside, ").Append(e.KeptTiles).Append(" kept, map ").Append(map.width).Append(" x ").Append(map.height).Append('\n');
            violations = bad;
            return sb.ToString();
        }
    }
}
