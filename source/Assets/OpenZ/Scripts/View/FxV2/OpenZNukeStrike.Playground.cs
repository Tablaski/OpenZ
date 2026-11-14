using System;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.3 item 2 (View/OzUeNuke.cs): the Unit editor's NUKE flies the levels' own ending strike. BeginPlayground makes one more strike of
    /// this class on a floor point: the same bomber (in the player's colour, PaintPlane with ours = false), flight, engine, fly-by whoosh,
    /// launch alarm, bomb release, whistle, fall, exhaust and impact (the secondary blasts, the rumble, the bloom flash), with onImpact
    /// at the impact (the caller's blast). What belongs to a level's ending stays out: no voice plan (it launches at once), no "Here comes
    /// the delivery!", no slow motion (the editor owns the time scale), no Impacted event and no LastImpactTime / LastImpactPoint (the
    /// endings, the victory, the end screen and the level fauna read those), no ending vignette, and it is never the current fort strike
    /// (Active, SecondsToImpact, HoldSeconds and LaunchNow do not see it; a fort strike never joins it).
    /// <see cref="PlaneBlurShare"/>
    /// </summary>
    public sealed partial class OpenZNukeStrike
    {
        bool playground;

        /// <summary>Checks: Unit editor strikes begun since the game started; the latest one (null when it is gone).</summary>
        public static int PlaygroundBegun;
        public static OpenZNukeStrike LastPlayground { get; private set; }
        /// <summary>This strike is the Unit editor's NUKE.</summary>
        public bool Playground => playground;
        /// <summary>The bomb has hit (checks).</summary>
        public bool HasImpacted => impacted;
        /// <summary>Game seconds (time scale 1) from BeginPlayground to the impact.</summary>
        public static float PlaygroundImpactDelay => Mathf.Max(0.3f, PlaneLeadIn - ReleaseLead) / PlaneSpeed + FallSeconds;

        /// <summary>Starts a Unit editor strike on the floor point at; onImpact runs at the impact. False when strikes are off (the
        /// caller then blasts at once, as RulesView does for a fort).</summary>
        public static bool BeginPlayground(Vector3 at, Action onImpact)
        {
            if (!Enabled) return false;
            var go = new GameObject("OpenZNukeStrike_UnitEditor");
            var s = go.AddComponent<OpenZNukeStrike>();
            s.playground = true; s.target = at; s.ours = false; s.onImpact = onImpact; s.hold = 0f;
            s.Build();
            PlaygroundBegun++; LastPlayground = s;
            Debug.Log($"[OpenZ] nuke strike: the Unit editor's NUKE on {at}, impact in {PlaygroundImpactDelay:F1} s");
            return true;
        }

        /// <summary>
        /// bomber writes into the motion vectors, so the game's motion blur (UnitLookMotionGate: CameraAndObjects, the MOTION BLUR option)
        /// streaks it this much of 1.3's: a third = a light hint of speed. The option, its strength and every other object's blur are
        /// untouched; the levels' ending bomber keeps its full streak. 1 = as in 1.3 (the plane's own renderer draws it).</summary>
        public static float PlaneBlurShare = 1f / 3f;

        MeshRenderer[] lightRs; MeshFilter[] lightMfs; Material[][] lightMats; Matrix4x4[] lightPrev; bool lightHadPrev;

        /// <summary>The bomber's renderers stop drawing themselves (forceRenderingOff: still enabled, bounds and materials as they are,
        /// the attacker's paint kept); LateUpdate draws the same meshes with the same materials, layer, rendering layers, probes and
        /// shadows, with object motion whose last pose lies only PlaneBlurShare of the way back to the true one.</summary>
        void LightPlaneBlur(GameObject model)
        {
            if (PlaneBlurShare >= 0.999f || !model) return;
            var rs = model.GetComponentsInChildren<MeshRenderer>(true);
            var keep = new System.Collections.Generic.List<MeshRenderer>(rs.Length); var mfs = new System.Collections.Generic.List<MeshFilter>(rs.Length);
            var mats = new System.Collections.Generic.List<Material[]>(rs.Length);
            foreach (var r in rs)
            {
                var mf = r.GetComponent<MeshFilter>();
                if (!mf || !mf.sharedMesh) continue;
                r.forceRenderingOff = true; keep.Add(r); mfs.Add(mf); mats.Add(r.sharedMaterials);                    // after PaintPlane: the attacker's colour
            }
            lightRs = keep.ToArray(); lightMfs = mfs.ToArray(); lightMats = mats.ToArray(); lightPrev = new Matrix4x4[lightRs.Length]; lightHadPrev = false;
            Debug.Log($"[OpenZ] nuke strike: the Unit editor's bomber streaks x{PlaneBlurShare:0.00} of its motion ({lightRs.Length} renderer(s) drawn with a lighter motion)");
        }

        void LateUpdate()
        {
            if (lightRs == null) return;
            if (!plane || !plane.gameObject.activeInHierarchy) { lightHadPrev = false; return; }       // before the launch or after the flight: nothing drawn, no motion kept
            float k = Mathf.Clamp01(PlaneBlurShare);
            for (int i = 0; i < lightRs.Length; i++)
            {
                var r = lightRs[i]; var mf = lightMfs[i];
                if (!r || !mf || !mf.sharedMesh || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                var now = r.localToWorldMatrix; var prev = now;
                if (lightHadPrev) { var was = lightPrev[i]; for (int e = 0; e < 16; e++) prev[e] = now[e] + (was[e] - now[e]) * k; }   // each vertex's last place k of the way back: its motion x k
                lightPrev[i] = now;
                var mats = lightMats[i]; var mesh = mf.sharedMesh;
                for (int s = 0; s < mesh.subMeshCount && s < mats.Length; s++)
                {
                    if (!mats[s]) continue;
                    var rp = new RenderParams(mats[s])
                    {
                        layer = r.gameObject.layer, renderingLayerMask = r.renderingLayerMask, rendererPriority = r.rendererPriority, worldBounds = r.bounds,
                        shadowCastingMode = r.shadowCastingMode, receiveShadows = r.receiveShadows, lightProbeUsage = r.lightProbeUsage,
                        reflectionProbeUsage = r.reflectionProbeUsage, motionVectorMode = MotionVectorGenerationMode.Object
                    };
                    Graphics.RenderMesh(rp, mesh, s, now, prev);
                }
            }
            lightHadPrev = true;
        }
    }
}
