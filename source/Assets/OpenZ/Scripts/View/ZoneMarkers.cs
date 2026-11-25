using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OpenZ
{
    /// <summary>
    /// Bright round territory markers draped over the terrain. On a shared edge the first zone in map order
    /// supplies the markers and their colour, which updates when that zone changes hands.
    /// </summary>
    public class ZoneMarkers : MonoBehaviour
    {
        ZGame game;
        readonly List<(Material m, int zone, Team shown)> zones = new List<(Material, int, Team)>();
        readonly List<MeshRenderer> borders = new List<MeshRenderer>();
        readonly List<Mesh> meshes = new List<Mesh>();
        float waterY = float.NegativeInfinity;
        const float Lift = 0.10f;
        static readonly float[] DotRadii = { 1f, 1.32f, 1.65f, 1.9f };
        static readonly Color[] DotColors = { Color.white, Color.red, Color.black, Color.clear };
        const int DotSides = 16;
        int shownSize;
        const float HoldSeconds = .25f;
        float pressedAt = -1f;
        int bindingVersion;
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
                m.SetFloat("_OutlinedDots", 1f);
                zones.Add((m, z.Id, (Team)(-1)));
            }
            shownSize = OzMapReadabilityOptions.VisibleBorderSize;
            LateUpdate();
        }

        void LateUpdate()
        {
            if (game == null || game.World == null) return;
            UpdateShortcut();
            bool visible = ShowBorders || pressedAt >= 0f;
            if (shownSize != OzMapReadabilityOptions.VisibleBorderSize && visible)
            {
                shownSize = OzMapReadabilityOptions.VisibleBorderSize;
                for (int i = 0; i < borders.Count; i++)
                {
                    var mesh = BuildZoneMesh(game.World.Zones[zones[i].zone]);
                    borders[i].GetComponent<MeshFilter>().sharedMesh = mesh;
                    Destroy(meshes[i]); meshes[i] = mesh;
                }
            }
            foreach (var border in borders) if (border && border.enabled != visible) border.enabled = visible;
            for (int i = 0; i < zones.Count; i++)
            {
                var (m, id, shown) = zones[i];
                var owner = game.World.Zones[id].Owner;
                // Track displayed colours as well, so a live team-colour swap updates the borders.
                var painted = TeamColors.Shown(owner);
                if (painted == shown) continue;
                m.SetColor("_Color", BorderColor(owner));
                zones[i] = (m, id, painted);
            }
        }

        void UpdateShortcut()
        {
            const OzAction action = OzAction.TerritoryBorders;
            var hud = OpenZHud.I;
            var keys = Keyboard.current;
            if (!Application.isFocused || keys == null || keys.ctrlKey.isPressed || keys.altKey.isPressed ||
                Multiplayer.DuelRuntime.CapturesInput || OzConfirm.Blocks || (hud && hud.Pause && hud.Pause.IsOpen))
            {
                pressedAt = -1f;
                return;
            }
            if (OzBindings.Pressed(action))
            {
                pressedAt = Time.unscaledTime;
                bindingVersion = OzBindings.Version;
            }
            if (pressedAt < 0f) return;
            if (bindingVersion == OzBindings.Version && OzBindings.Released(action))
                ShowBorders = Time.unscaledTime - pressedAt < HoldSeconds && !ShowBorders;
            else if (bindingVersion == OzBindings.Version && OzBindings.Held(action)) return;
            pressedAt = -1f;
        }

        Color BorderColor(Team owner) => TeamColors.Shown(owner) switch
        {
            Team.Neutral when game.World.Map.planet == "arctic" => new Color(.22f, .22f, .22f),
            Team.Neutral when game.World.Map.planet == "city" || OzCity.IsBridgeGame(game.level) => Color.white,
            Team.Neutral => new Color(.8f, .8f, .8f),
            Team.Red => new Color(1f, .24f, .19f),
            Team.Blue => new Color(.30f, .68f, 1f),
            var shown => TeamColors.OriginalPaint(shown),
        };

        Mesh BuildZoneMesh(ZZone z)
        {
            var map = game.World.Map;
            var v = new List<Vector3>(); var col = new List<Color>(); var t = new List<int>();
            AddBorder(true, z.Y, z.X, z.X + z.W);
            AddBorder(true, z.Y + z.H, z.X, z.X + z.W);
            AddBorder(false, z.X, z.Y, z.Y + z.H);
            AddBorder(false, z.X + z.W, z.Y, z.Y + z.H);
            if (v.Count == 0) return null;
            var m = new Mesh { name = "ZoneBorder", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            m.SetVertices(v); m.SetColors(col); m.SetTriangles(t, 0); m.RecalculateBounds();
            return m;

            void AddBorder(bool horizontal, int fixedCoordinate, int start, int end)
            {
                var remaining = new List<(int start, int end)> { (start, end) };
                foreach (var earlier in game.World.Zones)
                {
                    if (earlier == z || remaining.Count == 0) break;
                    int across = horizontal ? earlier.Y : earlier.X;
                    int acrossSize = horizontal ? earlier.H : earlier.W;
                    if (fixedCoordinate != across && fixedCoordinate != across + acrossSize) continue;
                    int otherStart = horizontal ? earlier.X : earlier.Y;
                    int otherEnd = otherStart + (horizontal ? earlier.W : earlier.H);
                    // Subtract only the shared portion, including where several zones meet this side.
                    for (int i = remaining.Count - 1; i >= 0; i--)
                    {
                        var span = remaining[i];
                        if (otherEnd <= span.start || otherStart >= span.end) continue;
                        remaining.RemoveAt(i);
                        if (span.start < otherStart) remaining.Add((span.start, otherStart));
                        if (otherEnd < span.end) remaining.Add((otherEnd, span.end));
                    }
                }
                foreach (var span in remaining)
                {
                    if (span.start >= span.end) continue;
                    Edge(v, col, t, Position(span.start), Position(span.end), map);
                }

                Vector3 Position(int coordinate) => horizontal
                    ? new Vector3(coordinate / 8f, 0, -fixedCoordinate / 8f)
                    : new Vector3(fixedCoordinate / 8f, 0, -coordinate / 8f);
            }
        }

        // A solid coloured core with a light rim and a dark outer edge, readable on both bright and dark terrain.
        void Edge(List<Vector3> v, List<Color> col, List<int> t, Vector3 a, Vector3 b, ZMapData map)
        {
            float len = Vector3.Distance(a, b), radius = .06f * OzMapReadabilityOptions.VisibleBorderSize;
            int count = Mathf.Max(1, Mathf.FloorToInt(len / 2.8f));
            for (int dot = 0; dot < count; dot++)
            {
                var p = Vector3.Lerp(a, b, (dot + .5f) / count);
                var px = ZGame.WorldToPx(p); var cls = map.ClassAt(px.x >> 4, px.y >> 4);
                if (cls == ZClass.Impassable || cls == ZClass.DeepWater) continue;
                bool blocked = false;
                for (int j = 0; j < 8; j++)
                {
                    float angle = j * Mathf.PI / 4;
                    var q = p + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * (radius * DotRadii[DotRadii.Length - 1]);
                    if (game.GroundY(q.x, q.z) > game.TerrainY(q.x, q.z) + 1.2f) { blocked = true; break; }
                }
                if (blocked) continue;
                int center = v.Count;
                v.Add(DotGround(p, cls)); col.Add(Color.white);
                int start = center + 1;
                for (int ring = 0; ring < DotRadii.Length; ring++)
                    for (int j = 0; j < DotSides; j++)
                    {
                        float angle = j * (2f * Mathf.PI / DotSides);
                        var q = p + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * (radius * DotRadii[ring]);
                        v.Add(DotGround(q, cls)); col.Add(DotColors[ring]);
                    }
                for (int j = 0; j < DotSides; j++)
                {
                    t.Add(center); t.Add(start + (j + 1) % DotSides); t.Add(start + j);
                }
                for (int ring = 0; ring < DotRadii.Length - 1; ring++)
                    for (int j = 0; j < DotSides; j++)
                    {
                        int k = start + ring * DotSides + j, next = start + ring * DotSides + (j + 1) % DotSides;
                        t.Add(k); t.Add(next); t.Add(k + DotSides);
                        t.Add(next); t.Add(next + DotSides); t.Add(k + DotSides);
                    }
            }
        }

        Vector3 DotGround(Vector3 p, ZClass cls)
        {
            float y = game.GroundY(p.x, p.z);
            if (cls == ZClass.Water) y = Mathf.Max(y, OzWaterLevel.At(game.level, p.x, p.z, waterY));
            p.y = y + Lift;
            return p;
        }
        public bool BordersUsePreference()
        { foreach (var b in borders) if (b && b.enabled != ShowBorders) return false; return borders.Count > 0; }
        public bool ColoursMatchOwners()
        {
            foreach (var z in zones)
            {
                var owner = game.World.Zones[z.zone].Owner;
                var expected = BorderColor(owner);
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
