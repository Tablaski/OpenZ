// OpenZ (GPL-3.0)
// View only: reads the Sim (the Zod object's health and death), never writes it; no collider, no rule, no Sim file.
using System.Collections;
using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// Level 18's twelve Zod map objects 14 (the original Z map's destroyable cover blocks beside its flags, factories, guns and bridgeheads:
    /// Sim map items that block their tile and direct fire, worn down by explosives, a debris missile when one dies) are drawn as sandbag
    /// positions from the approved SandbagsV2 kit (level 17's bags, atlas and material), built under each map_object14_x_y by
    /// Editor/OzL18SandbagNests.cs: a front wall toward the other side of the river and two side walls, open at the back. Before this kit they
    /// were a pale textured cube (L18S_ConcreteBlock, kit l18_street_r3) that read as a placeholder box with a face.
    /// Stages like the rock pillars (RockPillar): "Intact" until the Zod object is at half health, then "Damaged" (the top bags knocked off:
    /// two bags fly, sand puffs, the sandbag burst + pour sounds); when the Sim destroys it CombatView calls <see cref="SimDied"/> before its
    /// stone burst: "Rubble" (slumped bags and spilled sand), the thrown bags, sand and grit, the same sounds as the level 17 sandbag walls
    /// (SandbagBreakables). The tile is walkable after the death, so the thrown bags and later the rubble pop out like any debris there.
    /// </summary>
    public class OzSandbagNest : MonoBehaviour
    {
        /// <summary>The Zod type these nests stand for (level 18 only has the component: Editor/OzL18SandbagNests.cs).</summary>
        public const string ZodType = "map_object14";
        public GameObject intact, damaged, rubble;
        [Tooltip("SB2_Bag_A / B / C (the bags the level 17 walls throw)")] public Mesh[] bagMeshes;
        [Tooltip("M_SandbagsV2_L17 (level 17's sandbag material)")] public Material material;
        public int simId = -1;
        int stage;                                                     // 0 intact, 1 damaged, 2 rubble
        float nextPoll;
        bool warned;

        public static int Damages, Breaks, BagsThrown;                // counts for the Play check
        const int MaxChunks = 40;
        static readonly List<Transform> live = new List<Transform>();
        static int burstFrame = -1, pourFrame = -1;
        static PhysicsMaterial sandMat;
        static readonly string[] BurstClips = { "sd_sandbag_burst_a", "sd_sandbag_burst_b" };
        static readonly string[] FallbackClips = { "v3-collapse-small-a", "v3-collapse-small-b" };

        void Start()
        {
            if (intact) intact.SetActive(true);
            if (damaged) damaged.SetActive(false);
            if (rubble) rubble.SetActive(false);
        }

        void Update()
        {
            if (stage >= 1 || Time.time < nextPoll) return;
            nextPoll = Time.time + 0.25f;
            var g = ZGame.I;
            if (g == null || g.World == null) return;
            if (simId < 0)
            {
                simId = FindSimId(g);
                if (simId < 0)
                {
                    if (!warned) { warned = true; Debug.LogWarning("[OpenZ] sandbag nest " + Name + ": no Zod object bound to it (ZGame.StaticViews): it never shows damage"); }
                    nextPoll = Time.time + 5f;
                    return;
                }
            }
            var o = g.World.Get(simId);
            if (o != null && o.MaxHealth > 0 && o.Health > 0 && o.Health <= o.MaxHealth / 2) Damage();   // the rock pillars' half-health stage
        }

        string Name => transform.parent ? transform.parent.name : name;

        int FindSimId(ZGame g)
        {
            var root = transform.parent ? transform.parent.gameObject : gameObject;
            foreach (var kv in g.StaticViews) if (kv.Value == root) return kv.Key;
            return -1;
        }

        /// <summary>CombatView, the Sim removed a map item: a level 18 sandbag position breaks as sandbags (true = handled, no stone burst).</summary>
        public static bool SimDied(ZGame game, ZObject o, Vector3 blast)
        {
            if (game == null || o == null || o.Type != ZodType) return false;
            if (!game.StaticViews.TryGetValue(o.Id, out var go) || !go) return false;
            var n = go.GetComponentInChildren<OzSandbagNest>(true);
            if (!n) return false;
            n.simId = o.Id;
            n.Break(blast);
            return true;
        }

        /// <summary>Half health: the top bags are knocked off (Intact -> Damaged).</summary>
        public void Damage()
        {
            if (stage >= 1) return;
            var b = NestBounds(intact);
            stage = 1; Damages++;
            if (intact) intact.SetActive(false);
            if (damaged) damaged.SetActive(true);
            var c = b.center; float top = b.max.y;
            for (int k = 0; k < 2; k++)
            {
                var p = new Vector3(c.x, top - 0.12f, c.z) + Random.insideUnitSphere * 0.35f;
                var away = p - c; away.y = 0; if (away.sqrMagnitude < 0.01f) away = Random.insideUnitSphere; away.y = 0; away.Normalize();
                Throw(p, away * Random.Range(1.4f, 3.2f) + Vector3.up * Random.Range(3f, 5f), k);
            }
            var fx = FxManager.I;
            if (fx)
            {
                fx.Dust(new Vector3(c.x, top - 0.1f, c.z), 0.45f);
                fx.Breakup(new Vector3(c.x, top - 0.1f, c.z), "stone", 0.8f, 5, 0.35f);
            }
            Sounds(c, false);
            Debug.Log($"[OpenZ] sandbag nest {Name}: Damaged (its Zod object #{simId} at half health or less)");
        }

        /// <summary>The Sim destroyed the Zod object: Rubble, the bags fly away from the blast, sand and grit, the sounds; the rubble pops out later
        /// (the tile is walkable now).</summary>
        public void Break(Vector3 blast)
        {
            if (stage >= 2) return;
            bool wasIntact = stage == 0;
            var b = NestBounds(wasIntact ? intact : damaged);
            stage = 2; Breaks++;
            if (intact) intact.SetActive(false);
            if (damaged) damaged.SetActive(false);
            if (rubble) rubble.SetActive(true);
            var c = b.center; float floor = b.min.y; float len = Mathf.Max(b.size.x, b.size.z);
            int n = wasIntact ? 5 : 3;
            for (int k = 0; k < n; k++)
            {
                var p = new Vector3(c.x, floor + 0.35f + 0.1f * (k % 2), c.z) + new Vector3(Random.Range(-0.6f, 0.6f), 0f, Random.Range(-0.6f, 0.6f));
                var away = p - blast; away.y = 0; if (away.sqrMagnitude < 0.01f) away = Random.insideUnitSphere; away.y = 0; away.Normalize();
                away = Quaternion.Euler(0, Random.Range(-35f, 35f), 0) * away;
                Throw(p, away * Random.Range(2f, 4.5f) + Vector3.up * Random.Range(3.5f, 6.5f), k);
            }
            var fx = FxManager.I;
            if (fx)
            {   // = SandbagBreakables' full break: puffs along the walls, grit, a low cloud
                for (int k = 0; k < 3; k++) fx.Dust(new Vector3(c.x, floor + 0.25f, c.z) + new Vector3(Random.Range(-0.6f, 0.6f), 0f, Random.Range(-0.6f, 0.6f)), 0.75f);
                fx.Breakup(new Vector3(c.x, floor + 0.35f, c.z), "stone", Mathf.Max(1f, len), 10, 0.45f);
                fx.DemolitionDust(new Vector3(c.x, floor, c.z), len, 0.8f, 0.3f);
            }
            Sounds(c, true);
            Debug.Log($"[OpenZ] sandbag nest {Name}: Rubble (its Zod object #{simId} destroyed; was {(wasIntact ? "intact" : "damaged")}, {n} bags thrown)");
            if (rubble && isActiveAndEnabled) StartCoroutine(RubbleOut(Random.Range(7f, 9f)));
        }

        IEnumerator RubbleOut(float after)
        {
            yield return new WaitForSeconds(after);
            if (rubble && rubble.activeSelf) PopOut.Begin(rubble.transform, PopOut.Style.Debris, () => { if (rubble) rubble.SetActive(false); }, false, false);
        }

        static Bounds NestBounds(GameObject g)
        {
            var b = new Bounds(g ? g.transform.position : Vector3.zero, Vector3.zero); bool any = false;
            if (g)
                foreach (var mf in g.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (!mf.sharedMesh || !mf.name.StartsWith("LOD0")) continue;
                    var mb = mf.sharedMesh.bounds; var m = mf.transform.localToWorldMatrix;
                    for (int i = 0; i < 8; i++)
                    {
                        var p = m.MultiplyPoint3x4(mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
                        if (!any) { b = new Bounds(p, Vector3.zero); any = true; } else b.Encapsulate(p);
                    }
                }
            return b;
        }

        void Sounds(Vector3 c, bool full)
        {
            if (AudioDirector.Has("dst_sandbag_burst"))
            {
                if (burstFrame != Time.frameCount) AudioDirector.Fire("dst_sandbag_burst", c, simId, full ? 1f : 0.8f);
                burstFrame = Time.frameCount;
            }
            else { var au = CombatAudio.I; if (au) au.Play("prop", c, full ? 0.8f : 0.65f, 0.05f, -1, au.HasClip(BurstClips[0]) ? BurstClips : FallbackClips); }
            if (AudioDirector.Has("dst_sandbag_pour"))
            {
                if (pourFrame != Time.frameCount) AudioDirector.Fire("dst_sandbag_pour", c, simId, full ? 1f : 0.75f);
                pourFrame = Time.frameCount;
            }
        }

        /// <summary>One thrown bag (SandbagBreakables.SpawnChunk's body): heavy, floppy, the burlap thud on landing, a plop in water; it lies
        /// still when it stops and pops out at the end of its 4.5-7 s life (the tile it lands on is walkable).</summary>
        void Throw(Vector3 p, Vector3 v, int k)
        {
            if (bagMeshes == null || bagMeshes.Length == 0 || !material) return;
            var mesh = bagMeshes[k % bagMeshes.Length];
            if (!mesh) return;
            live.RemoveAll(x => !x);
            while (live.Count >= MaxChunks) { var old = live[0]; live.RemoveAt(0); Retire(old); }
            var go = new GameObject("SandbagNestChunk");
            go.layer = BuildingBreaker.DebrisLayer;
            var t = go.transform; t.position = p; t.rotation = Random.rotation; t.localScale = Vector3.one * Random.Range(0.92f, 1f) * transform.lossyScale.x;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = material;
            mr.motionVectorGenerationMode = MotionVectorGenerationMode.Object;
            var bc = go.AddComponent<BoxCollider>(); bc.size = mesh.bounds.size * 0.92f; bc.center = mesh.bounds.center;
            bc.sharedMaterial = sandMat ??= new PhysicsMaterial("SandbagNest") { bounciness = 0.08f, dynamicFriction = 0.9f, staticFriction = 1f, bounceCombine = PhysicsMaterialCombine.Minimum };
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 18f; rb.linearDamping = 0.2f; rb.angularDamping = 1.6f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            rb.linearVelocity = v; rb.angularVelocity = Random.insideUnitSphere * Random.Range(3f, 8f);
            AudioDirector.AttachImpacts(go, AudioDirector.Has("imp_bullet_sandbag") ? "imp_bullet_sandbag" : "imp_bullet_sand", AudioDirector.Has("wtr_debris_plop") ? "wtr_debris_plop" : "dst_debris_splash");
            OzWaterLanding.Watch(t, "sandbag", rb);
            live.Add(t); BagsThrown++;
            StartCoroutine(Life(t, rb, Random.Range(4.5f, 7f)));
        }

        IEnumerator Life(Transform t, Rigidbody rb, float life)
        {
            float born = Time.time;
            while (t && Time.time < born + life)
            {
                if (rb && !rb.isKinematic && Time.time > born + 2.5f && rb.linearVelocity.sqrMagnitude < 0.01f && rb.angularVelocity.sqrMagnitude < 0.04f) rb.isKinematic = true;   // lies still
                yield return null;
            }
            if (t) { live.Remove(t); Retire(t); }
        }

        static void Retire(Transform t)
        {
            if (!t) return;
            var rb = t.GetComponent<Rigidbody>();
            if (rb) { rb.isKinematic = true; rb.interpolation = RigidbodyInterpolation.None; }
            if (!PopOut.IsPopping(t)) PopOut.Begin(t, PopOut.Style.Debris, null, true, false);
        }
    }
}
