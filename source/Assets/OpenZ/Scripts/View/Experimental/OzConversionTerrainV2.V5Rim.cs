// OpenZ-VK (GPL-3.0).
using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ.Experimental
{
    public partial class OzConversionTerrainV2
    {
        Mesh RenderV5Rim(ZMapData map, List<Vector3> original, List<Vector2> uv, List<int>[] channels)
        {
            var visual = new List<Vector3>(original.Count);
            foreach (var vertex in original)
            {
                float x = vertex.x, z = -vertex.z;
                float d = OzAiTerrainV5.RimDistance(x, z, map.width, map.height);
                float weight = OzAiTerrainV5.RimWeight(d);
                visual.Add(weight > 0 ? vertex + Vector3.up * (RimRelief(map.planet, x, z, d) * weight) : vertex);
            }
            var mesh = NewMesh("AI Maps V5 render surface", visual);
            mesh.SetUVs(0, uv);
            mesh.subMeshCount = 4;
            var outside = new List<int>();
            for (int i = 0; i < channels.Length; i++)
            {
                var inside = new List<int>();
                var channel = channels[i];
                for (int j = 0; j < channel.Count; j += 3)
                {
                    int a = channel[j], b = channel[j + 1], c = channel[j + 2];
                    var centroid = (original[a] + original[b] + original[c]) / 3;
                    var group = OzAiTerrainV5.RimDistance(centroid.x, -centroid.z, map.width, map.height) > 2 ? outside : inside;
                    group.Add(a);
                    group.Add(b);
                    group.Add(c);
                }
                mesh.SetTriangles(inside, i, false);
            }
            mesh.SetTriangles(outside, 3, false);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static float RimRelief(string world, float x, float z, float distance)
        {
            float broad = Mathf.PerlinNoise(x * .045f + 13, z * .045f + 27);
            float detail = Mathf.PerlinNoise(x * .28f - 9, z * .26f);
            float warp = Mathf.PerlinNoise(x * .14f + 9, z * .12f) * 6;
            float bench = Mathf.SmoothStep(0, 1, Mathf.Repeat((distance + warp) / 8.5f, 1));
            return world switch
            {
                "desert" => 1.6f + broad * 8.2f + bench * 2.6f + detail * .95f,
                "volcanic" => 1 + broad * 5.6f + detail * 1.8f,
                "arctic" => 2.2f + broad * 7.5f + bench * 2.2f + detail * 1.4f,
                "jungle" => 2.8f + broad * 7.0f + bench * 1.7f + detail * 1.6f,
                _ => 1.4f + broad * 5.9f + bench * 2.5f + detail * 1.35f
            };
        }

        Material V5Scenery(string planet, string name, bool rim)
        {
            var material = new Material(Shader.Find("OpenZ/AI Map V5 Scenery")) { name = name };
            material.SetFloat("_Biome", rim ? OzAiTerrainV4.Profile(planet) : 0);
            generated.Add(material);
            return material;
        }
    }
}
