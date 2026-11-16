using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ.Experimental
{
    /// <summary>Regenerate geometry from the small tracked grid instead of distributing baked terrain meshes.</summary>
    [DefaultExecutionOrder(-500)]
    public sealed class OzShittyTerrain : MonoBehaviour
    {
        public string mapId, world;
        public Material[] materials;
        readonly HashSet<Mesh> meshes = new HashSet<Mesh>();
        void Awake()
        {
            var asset = Resources.Load<TextAsset>("OpenZ/Maps/" + mapId + "_grid");
            var map = ZMapData.FromJson(asset.text);
            OzDraftTerrain.Build(map.width, map.height, map.classes, world, materials, transform);
            foreach (var filter in GetComponentsInChildren<MeshFilter>()) meshes.Add(filter.sharedMesh);
            foreach (var collider in GetComponentsInChildren<MeshCollider>()) meshes.Add(collider.sharedMesh);
            Physics.SyncTransforms();
        }
        void OnDestroy() { foreach (var mesh in meshes) if (mesh) Destroy(mesh); }
    }
}
