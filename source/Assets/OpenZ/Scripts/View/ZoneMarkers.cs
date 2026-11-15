using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
        /// Optional Zod-style dotted borders, in each zone owner's colour: a soft band 0.18 m wide, 0.45 m
    /// inside the zone edge (two neighbouring zones show two lines side by side), draped over the terrain 6 cm above it (no
    /// z-fighting), on passable and water tiles only, recoloured live when the zone changes hands.
    /// </summary>
    public class ZoneMarkers : MonoBehaviour
    {
        ZGame game;
        readonly List<(Material m, int zone, Team shown)> zones = new List<(Material, int, Team)>();
        readonly List<MeshRenderer> borders = new List<MeshRenderer>();
        readonly List<Mesh> meshes = new List<Mesh>();
        float waterY = float.NegativeInfinity;
        const float Inset = 0.45f, Width = 0.18f, Lift = 0.06f, Alpha = 0.55f;
        /// <summary>Z draws no zone borders in the world (territory reads from flags, building colours and the minimap);
        /// </summary>
        public static bool ShowBorders { get => OzMapReadabilityOptions.Borders; set => OzMapReadabilityOptions.Borders = value; }

        void Start()
        {
            game = ZGame.I;
            if (game == null || game.World == null) { enabled = false; return; }
            foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                if (r.name.StartsWith("WAT_") && r.gameObject.activeInHierarchy)
                    waterY = Mathf.Max(waterY, r.bounds.min.y + 0.02f);   // the flat river is the lowest part; bounds.max was the 28 m
                                                                           // waterfall (it lifted zone lines up the cliffs); meshes aren't readable
            var shader = Shader.Find("OpenZ/Overlay");
            foreach (var z in game.World.Zones)
            {
                var mesh = BuildZoneMesh(z);
                if (mesh == null) continue;
                var go = new GameObject("ZoneBorder" + z.Id); go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>(); mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
                mr.enabled = ShowBorders;
                borders.Add(mr); meshes.Add(mesh);
                var m = new Material(shader) { name = "ZoneBorder" + z.Id }; mr.sharedMaterial = m;
                zones.Add((m, z.Id, (Team)(-1)));
            }
            LateUpdate();
        }

        void LateUpdate()
        {
            if (game == null || game.World == null) return;
            foreach (var border in borders) if (border && border.enabled != ShowBorders) border.enabled = ShowBorders;
            for (int i = 0; i < zones.Count; i++)
            {
                var (m, id, shown) = zones[i];
                var owner = game.World.Zones[id].Owner;
                // Track displayed colours as well, so a live team-colour swap updates the borders.
                var painted = TeamColors.Shown(owner);
                if (painted == shown) continue;
                var c = owner == Team.Neutral ? new Color(.48f, .48f, .48f) : TeamColors.Paint(owner); c.a = owner == Team.Neutral ? Alpha * 0.85f : Alpha;
                m.SetColor("_Color", c);
                zones[i] = (m, id, painted);
            }
        }

        Mesh BuildZoneMesh(ZZone z)
        {
            var map = game.World.Map;
            var v = new List<Vector3>(); var col = new List<Color>(); var t = new List<int>();
            // zone rectangle in world metres: x = px / 8, z = -py / 8
            float x0 = z.X / 8f + Inset, x1 = (z.X + z.W) / 8f - Inset, z0 = -z.Y / 8f - Inset, z1 = -(z.Y + z.H) / 8f + Inset;
            Edge(v, col, t, new Vector3(x0, 0, z0), new Vector3(x1, 0, z0), Vector3.back, map);      // top edge, band grows inward
            Edge(v, col, t, new Vector3(x1, 0, z1), new Vector3(x0, 0, z1), Vector3.forward, map);   // bottom
            Edge(v, col, t, new Vector3(x0, 0, z1), new Vector3(x0, 0, z0), Vector3.right, map);     // left
            Edge(v, col, t, new Vector3(x1, 0, z0), new Vector3(x1, 0, z1), Vector3.left, map);      // right
            if (v.Count == 0) return null;
            var m = new Mesh { name = "ZoneBorder", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            m.SetVertices(v); m.SetColors(col); m.SetTriangles(t, 0); m.RecalculateBounds();
            return m;
        }

        /// <summary>One soft band along a zone edge, sampled every 0.5 m; gaps over impassable tiles (cliffs, rock walls).</summary>
        void Edge(List<Vector3> v, List<Color> col, List<int> t, Vector3 a, Vector3 b, Vector3 inward, ZMapData map)
        {
            float len = Vector3.Distance(a, b); int n = Mathf.Max(1, Mathf.CeilToInt(len / 0.2f));
            int prev = -1;
            for (int i = 0; i <= n; i++)
            {
                var p = Vector3.Lerp(a, b, i / (float)n);
                // Zod places small coloured markers on the passable perimeter tiles
                // (zmap.cpp SetupAllZoneInfo / DoZoneEffects); use short, spaced ground dashes in 3D.
                if ((len * i / n) % 2f > .8f) { prev = -1; continue; }
                var px = ZGame.WorldToPx(p); var cls = map.ClassAt(px.x >> 4, px.y >> 4);
                if (cls == ZClass.Impassable || cls == ZClass.DeepWater) { prev = -1; continue; }
                // long red lines across the view), so the band breaks wherever the surface is well above the play floor
                bool high = false;
                for (int k = 0; k < 3 && !high; k++) { var q0 = p + inward * (Width * k * 0.5f); high = game.GroundY(q0.x, q0.z) > game.TerrainY(q0.x, q0.z) + 1.2f; }
                if (high) { prev = -1; continue; }
                int start = v.Count;
                for (int k = 0; k < 3; k++)
                {
                    var q = p + inward * (Width * k * 0.5f);
                    float y = game.GroundY(q.x, q.z);
                    if (cls == ZClass.Water) { float wq = OzWaterLevel.At(game.level, q.x, q.z, waterY); if (wq > y) y = wq; }   // each pool at its own height (level 16)
                    v.Add(new Vector3(q.x, y + Lift, q.z));
                    col.Add(new Color(1, 1, 1, k == 1 ? 1f : 0f));                                    // soft edges, solid centre
                }
                if (prev >= 0)
                    for (int k = 0; k < 2; k++)
                    { t.Add(prev + k); t.Add(start + k); t.Add(prev + k + 1); t.Add(prev + k + 1); t.Add(start + k); t.Add(start + k + 1); }
                prev = start;
            }
        }
        public bool BordersUsePreference()
        { foreach (var b in borders) if (b && b.enabled != ShowBorders) return false; return borders.Count > 0; }
        public bool ColoursMatchOwners()
        {
            foreach (var z in zones)
            {
                var owner = game.World.Zones[z.zone].Owner;
                var expected = owner == Team.Neutral ? new Color(.48f, .48f, .48f) : TeamColors.Paint(owner);
                var actual = z.m.GetColor("_Color");
                if (Mathf.Abs(actual.r - expected.r) + Mathf.Abs(actual.g - expected.g) + Mathf.Abs(actual.b - expected.b) > .01f) return false;
            }
            return zones.Count > 0;
        }
        void OnDestroy()
        {
            foreach (var z in zones) if (z.m) Destroy(z.m);
            foreach (var mesh in meshes) if (mesh) Destroy(mesh);
        }
    }
}
