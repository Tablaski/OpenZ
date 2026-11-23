// OpenZ-VK (GPL-3.0).
using System.Collections.Generic;
using UnityEngine;

namespace OpenZ.Experimental
{
    public partial class OzConversionTerrainV2
    {
        void DressWetDetails(Material scenery, List<(Vector3, Vector3)> outfalls, List<Vector3> mist)
        {
            if (outfalls.Count == 0 && mist.Count == 0) return;
            var vapor = new SceneryMesh();
            foreach (var p in mist)
            {
                var x = Vector3.right * 2.5f;
                var z = Vector3.forward * 1.7f;
                var center = p + Vector3.up * .22f;
                vapor.Quad(center - x - z, center + x - z, center - x + z, center + x + z,
                    new Color(.63f, .66f, .52f, .22f));
            }
            var plumbing = new SceneryMesh();
            var flow = new SceneryMesh();
            foreach (var (bank, direction) in outfalls)
            {
                var side = Vector3.Cross(Vector3.up, direction);
                var mouth = bank + direction * 1.52f;
                mouth.y = Mathf.Max(.18f, bank.y - .28f);
                var back = mouth - direction * 1.25f;
                var end = mouth + direction * .38f;
                end.y = -.30f;
                var rust = new Color(.28f, .22f, .14f);
                plumbing.Tube(back, mouth, .39f, .39f, rust, 12);
                plumbing.Tube(mouth - direction * .11f, mouth + direction * .035f, .45f, .44f, rust * 1.22f, 12);
                var lip = mouth - Vector3.up * .11f;
                flow.Quad(lip - side * .31f, lip + side * .31f, end - side * .43f, end + side * .43f,
                    new Color(.40f, .35f, .17f, .76f));
            }
            if (plumbing.Vertices.Count > 0) AddScenery(plumbing, scenery, "V5 harbour outfalls");
            AddWet(vapor, false);
            AddWet(flow, true);

            void AddWet(SceneryMesh mesh, bool pouring)
            {
                if (mesh.Vertices.Count == 0) return;
                var material = new Material(Shader.Find("OpenZ/AI Map V5 Water Detail"));
                material.SetFloat("_Pouring", pouring ? 1 : 0);
                generated.Add(material);
                var visual = AddScenery(mesh, material, pouring ? "V5 harbour runoff" : "V5 swamp mist");
                visual.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }
    }
}
