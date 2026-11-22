using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace OpenZ
{
    /// <summary>Opt-in renderer experiment for intact pillars on Desert P08M01.</summary>
    internal sealed class OzDesertRockBatches : MonoBehaviour
    {
        internal const string Argument = "-oz-batch-desert-rocks";
        const float CellSize = 32f;
        const int MaxBatch = 511;
        static bool? optedIn;

        internal sealed class Entry
        {
            public MeshRenderer Renderer;
            public Group Batch;
            public int Index;
        }

        internal sealed class Group
        {
            public Mesh Mesh;
            public RenderParams Parameters;
            public Bounds Bounds;
            public readonly List<Entry> Entries = new List<Entry>();
            public readonly List<Matrix4x4> Matrices = new List<Matrix4x4>();
        }

        readonly Dictionary<(Mesh mesh, Material material, Vector2Int cell, int layer, uint renderingLayers,
            ShadowCastingMode shadows, bool receive, LightProbeUsage probes, ReflectionProbeUsage reflections,
            MotionVectorGenerationMode motion, int priority), Group> groups = new();
        bool logged;

        internal static Entry Register(RockPillar rock)
        {
            optedIn ??= Array.IndexOf(Environment.GetCommandLineArgs(), Argument) >= 0;
            var game = ZGame.I;
            if (!optedIn.Value || !game || game.level != "SMP08M01" || !SystemInfo.supportsInstancing) return null;
            var intact = rock.pillar;
            if (!intact || !intact.activeInHierarchy || !intact.TryGetComponent<MeshRenderer>(out var renderer) ||
                !intact.TryGetComponent<MeshFilter>(out var filter) || !renderer.enabled || renderer.forceRenderingOff ||
                renderer.HasPropertyBlock() || renderer.probeAnchor || renderer.lightProbeProxyVolumeOverride ||
                (renderer.lightmapIndex >= 0 && renderer.lightmapIndex < 65534) || renderer.isPartOfStaticBatch)
                return null;
            var mesh = filter.sharedMesh;
            var material = renderer.sharedMaterial;
            if (!mesh || mesh.subMeshCount != 1 || !material || !material.enableInstancing ||
                material.renderQueue > (int)RenderQueue.GeometryLast || renderer.sharedMaterials.Length != 1 ||
                (renderer.lightProbeUsage != LightProbeUsage.Off && renderer.lightProbeUsage != LightProbeUsage.BlendProbes))
                return null;

            if (!game.TryGetComponent<OzDesertRockBatches>(out var batches))
                batches = game.gameObject.AddComponent<OzDesertRockBatches>();
            if (!batches.enabled) return null;
            var p = intact.transform.position;
            var cell = new Vector2Int(Mathf.FloorToInt(p.x / CellSize), Mathf.FloorToInt(p.z / CellSize));
            var key = (mesh, material, cell, intact.layer, renderer.renderingLayerMask, renderer.shadowCastingMode,
                renderer.receiveShadows, renderer.lightProbeUsage, renderer.reflectionProbeUsage,
                renderer.motionVectorGenerationMode, renderer.rendererPriority);
            if (!batches.groups.TryGetValue(key, out var group))
            {
                group = new Group { Mesh = mesh, Bounds = renderer.bounds, Parameters = new RenderParams(material)
                {
                    layer = intact.layer,
                    renderingLayerMask = renderer.renderingLayerMask,
                    shadowCastingMode = renderer.shadowCastingMode,
                    receiveShadows = renderer.receiveShadows,
                    lightProbeUsage = renderer.lightProbeUsage,
                    reflectionProbeUsage = renderer.reflectionProbeUsage,
                    motionVectorMode = renderer.motionVectorGenerationMode,
                    rendererPriority = renderer.rendererPriority
                } };
                batches.groups.Add(key, group);
            }
            else group.Bounds.Encapsulate(renderer.bounds);
            var entry = new Entry { Renderer = renderer, Batch = group, Index = group.Entries.Count };
            group.Entries.Add(entry);
            group.Matrices.Add(intact.transform.localToWorldMatrix);
            renderer.forceRenderingOff = true;
            return entry;
        }

        internal static void Unregister(Entry entry)
        {
            if (entry?.Batch == null) return;
            var group = entry.Batch;
            int last = group.Entries.Count - 1;
            group.Entries[entry.Index] = group.Entries[last];
            group.Entries[entry.Index].Index = entry.Index;
            group.Matrices[entry.Index] = group.Matrices[last];
            group.Entries.RemoveAt(last);
            group.Matrices.RemoveAt(last);
            entry.Batch = null;
            if (entry.Renderer) entry.Renderer.forceRenderingOff = false;
        }

        void LateUpdate()
        {
            try
            {
                int count = 0, draws = 0;
                foreach (var group in groups.Values)
                {
                    int total = group.Matrices.Count;
                    count += total;
                    for (int start = 0; start < total; start += MaxBatch)
                    {
                        var parameters = group.Parameters;
                        parameters.worldBounds = group.Bounds;
                        Graphics.RenderMeshInstanced(parameters, group.Mesh, 0, group.Matrices,
                            Math.Min(MaxBatch, total - start), start);
                        draws++;
                    }
                }
                if (!logged)
                {
                    logged = true;
                    Debug.Log($"[OpenZ Rock Batches] {count} intact pillars in {draws} instanced submissions");
                }
            }
            catch (Exception error)
            {
                Debug.LogWarning($"[OpenZ Rock Batches] Restoring the original renderers: {error.Message}");
                enabled = false;
            }
        }

        void OnDisable()
        {
            foreach (var group in groups.Values)
                while (group.Entries.Count != 0)
                    Unregister(group.Entries[group.Entries.Count - 1]);
            groups.Clear();
        }
    }
}
