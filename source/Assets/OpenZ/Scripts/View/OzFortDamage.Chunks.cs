using System.Collections.Generic;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.5 fort_damage: parts falling off. The pieces are the game's modelled concrete chunks (ConcreteChunkLib: XS chips, S and M lumps with
    /// broken faces, L slabs; the fort death's own library), in the level's concrete look (ConcreteChunkLib.ApplyLook) and one in six with the
    /// owner's team band (SetTrim): never boxes. A piece breaks out of the wall just in front of it (never inside a wall), is thrown out and
    /// down with a spin, collides with the ground only (BuildingBreaker's debris layer: pieces never hit each other), raises dust and knocks
    /// on landing (dst_debris_concrete; an L slab: dst_rock_crash_large and a small shake), and lies as rubble: at the fort's foot it stays
    /// (kinematic once asleep) until the fort falls; one that rolled further out pops out after 15-22 s (nothing left on the walkways). A
    /// piece in water is handed to the shared water landing (OzWaterLanding). Caps: RubbleCap per fort (the oldest small one pops out first).
    /// <para>The breach's broken edge: 3 S chunks set half into the wall round each breach (no physics, no collider), popping in under the
    /// break's dust; they go with the breach (a heal in a test) or with the fort.</para>
    /// </summary>
    public partial class OzFortDamage
    {
        public static int RubbleCap = 40;
        public static int ChunksMade, ChunkLandings;
        static PhysicsMaterial chunkMat;
        static PhysicsMaterial ChunkMat() => chunkMat ??= new PhysicsMaterial("FortDamageConcrete") { bounciness = 0.06f, dynamicFriction = 0.8f, staticFriction = 0.95f, bounceCombine = PhysicsMaterialCombine.Minimum, frictionCombine = PhysicsMaterialCombine.Maximum };

        internal sealed class Rim { public GameObject go; public Site site; }

        /// <summary>One piece off site s (or off the parapet over it): class XS / S / M / L; spread = how far round the site's centre it starts (m).</summary>
        Chunk Fall(Fort f, Site s, string cls, float spread, bool fromTop = false)
        {
            if (!ConcreteChunkLib.Load() || !f.root || RuinSkips(f, cls)) return null;   // 1.5 ending_fort_ruin: no chips and at most RubbleCap pieces in the ruin sweep
            bool band = cls != "L" && f.o.Owner != Team.Neutral && f.rnd.Next(6) == 0;                           // one in six wears the owner's band (Pick never gives a band chunk; v1.4 has no band L)
            var e = band ? ConcreteChunkLib.PickBand(f.rnd, cls) : ConcreteChunkLib.Pick(f.rnd, cls, true); if (e == null || !e.mesh) return null;
            var n = SiteN(f, s); var t = f.root.TransformDirection(s.t).normalized;
            var scale = ConcreteChunkLib.RandomScale(f.rnd, e.max_scale) * (cls == "L" ? 0.8f : 1f);
            float r = e.Longest * Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z)) * 0.5f;
            Vector3 at;
            if (fromTop)
            {   // off the parapet over the site: its top edge, a little out of the wall
                var top = f.root.TransformPoint(new Vector3(s.p.x, s.top, s.p.z) - s.n * (0.15f / f.scaleM));
                at = top + n * (r + 0.25f) + Vector3.up * (r * 0.3f) + t * Rand(f, -spread, spread);
            }
            else at = SiteWorld(f, s, r + 0.08f) + t * Rand(f, -spread, spread) + Vector3.up * Rand(f, -spread * 0.6f, spread * 0.6f);
            var go = new GameObject("OZ_FortDamageChunk_" + e.cls); go.layer = BuildingBreaker.DebrisLayer;
            go.transform.SetPositionAndRotation(at, ConcreteChunkLib.RandomRotation(f.rnd)); go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = e.mesh;
            var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterials = e.mats;
            mr.motionVectorGenerationMode = MotionVectorGenerationMode.Object;
            ConcreteChunkLib.ApplyLook(mr, e, f.rnd);
            if (e.band) ConcreteChunkLib.SetTrim(mr, e, TeamColors.Of(f.o.Owner), f.rnd);
            Collider col;
            if (e.col) { var mc = go.AddComponent<MeshCollider>(); mc.sharedMesh = e.col; mc.convex = true; col = mc; }
            else { var bc = go.AddComponent<BoxCollider>(); col = bc; }
            col.sharedMaterial = ChunkMat();
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = Mathf.Clamp(e.suggested_mass > 0f ? e.suggested_mass : 20f, 1f, 2500f);
            rb.linearDamping = 0.15f; rb.angularDamping = 0.6f; rb.maxAngularVelocity = 12f;
            rb.interpolation = RigidbodyInterpolation.Interpolate; rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;   // as BuildingBreaker.Ruin (its "was ContinuousDynamic" fix)
            float out1 = fromTop ? Rand(f, 0.8f, 1.8f) : Rand(f, 1.0f, 3.0f);
            rb.linearVelocity = n * out1 + Vector3.up * (fromTop ? Rand(f, 0f, 0.8f) : Rand(f, 0.4f, 2.2f)) + t * Rand(f, -0.9f, 0.9f);
            rb.angularVelocity = Random.insideUnitSphere * Rand(f, 1.5f, 5f);
            var c = go.AddComponent<Chunk>();
            c.owner = this; c.fort = f; c.rb = rb; c.big = e.cls == "L"; c.small = e.cls == "XS";
            c.life = Rand(f, 15f, 22f); c.foot = Foot(f);
            f.chunks.Add(c); ChunksMade++;
            OzWaterLanding.Watch(go.transform, "fort damage chunk", rb);
            Cap(f);
            return c;
        }

        static float Rand(Fort f, float a, float b) => a + (float)f.rnd.NextDouble() * (b - a);

        /// <summary>The fort's footprint on the ground, 1.2 m wider: rubble that rests in it stays.</summary>
        Bounds Foot(Fort f)
        {
            var lo = file != null && file.bounds_min != null && file.bounds_min.Length >= 3 ? new Vector3(file.bounds_min[0], file.bounds_min[1], file.bounds_min[2]) : new Vector3(-0.95f, -0.58f, -0.94f);
            var hi = file != null && file.bounds_max != null && file.bounds_max.Length >= 3 ? new Vector3(file.bounds_max[0], file.bounds_max[1], file.bounds_max[2]) : new Vector3(0.95f, 0.58f, 0.94f);
            var b = new Bounds(f.root.TransformPoint((lo + hi) * 0.5f), Vector3.zero);
            for (int i = 0; i < 8; i++) b.Encapsulate(f.root.TransformPoint(new Vector3((i & 1) == 0 ? lo.x : hi.x, (i & 2) == 0 ? lo.y : hi.y, (i & 4) == 0 ? lo.z : hi.z)));
            b.Expand(new Vector3(2.4f, 100f, 2.4f));
            return b;
        }

        void Cap(Fort f)
        {
            f.chunks.RemoveAll(c => !c || c.Popping);                                                       // a popping chunk is already on its way out
            int over = f.chunks.Count - RubbleCap;
            for (int i = 0; i < f.chunks.Count && over > 0; i++)
                if (f.chunks[i].resting && (f.chunks[i].small || over > 4)) { f.chunks[i].PopNow(); over--; }
        }

        void StepChunks(Fort f, float dt)
        {
            if ((Time.frameCount & 31) == 0) f.chunks.RemoveAll(c => !c);
        }

        /// <summary>A big piece off the parapet: the k-th break (M, then an L slab, then two M), over a breach when there is one.</summary>
        void ParapetBreak(Fort f, int k)
        {
            if (f.sites.Length == 0) return;
            var s = f.breaches.Count > 0 ? f.breaches[Mathf.Min(k, f.breaches.Count - 1)] : Pick(f, 1);
            if (s == null) return;
            ParapetBreaks++;
            var top = f.root.TransformPoint(new Vector3(s.p.x, s.top, s.p.z));
            bool slab = k % 2 == 1;   // 1.5 kit fort_damage_stronger: every second break a slab (6 breaks now)
            Play(slab ? "dst_collapse_medium" : "dst_rock_crumble_medium", top, f.o.Id, slab ? 0.8f : 0.7f, Random.Range(0.92f, 1.05f));
            var fx = FxManager.I; if (fx) { fx.Dust(top, 0.9f); fx.Dust(top + Vector3.down * 1.5f, 0.6f); }
            Sparks(top, SiteN(f, s), 6);
            Fall(f, s, slab ? "L" : "M", 0.4f, true);
            if (k >= 2) Fall(f, s, "M", 0.8f, true);
            for (int i = 0; i < 3; i++) Fall(f, s, "S", 0.7f, true);
            LocalBlasts(f, s, top, 2, true);   // 1.5 kit view_notes11 N3: local blasts where the parapet breaks off (OzFortDamage.Blasts.cs)
            ParapetJag(f, s, top, k);   // 1.5 kit fort_damage_stronger: the broken top edge + its fire (OzFortDamage.Stronger.cs)
            Debug.Log($"[OpenZ] fort damage: {f.o.Type} #{f.o.Id} parapet break {k + 1} ({(slab ? "a slab" : "a lump")}) over wall site {s.i}");
        }

        /// <summary>The breach's broken edge: 3 S chunks half in the wall round its centre.</summary>
        void RimsOn(Fort f, Site s)
        {
            if (!ConcreteChunkLib.Load() || !f.root) return;
            var n = SiteN(f, s); var t = f.root.TransformDirection(s.t).normalized;
            float phase = Rand(f, 0f, 6.283f);
            for (int k = 0; k < 3; k++)
            {
                var e = ConcreteChunkLib.Pick(f.rnd, "S", false); if (e == null || !e.mesh) continue;
                float ang = phase + k * 2.094f + Rand(f, -0.4f, 0.4f), ring = Rand(f, 0.55f, 0.75f);
                var scale = ConcreteChunkLib.RandomScale(f.rnd, e.max_scale) * Rand(f, 0.75f, 0.95f);
                float r = e.Longest * Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z)) * 0.5f;
                var at = SiteWorld(f, s, 0f) + t * (Mathf.Cos(ang) * ring) + Vector3.up * (Mathf.Sin(ang) * ring) - n * (r * 0.45f);   // half in the wall
                var go = new GameObject("OZ_FortBreachEdge");
                go.transform.SetParent(transform, true);
                go.transform.SetPositionAndRotation(at, ConcreteChunkLib.RandomRotation(f.rnd));
                go.AddComponent<MeshFilter>().sharedMesh = e.mesh;
                var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterials = e.mats; ConcreteChunkLib.ApplyLook(mr, e, f.rnd);
                go.transform.localScale = Vector3.zero;
                var pop = go.AddComponent<RimPop>(); pop.to = scale;
                f.rims.Add(new Rim { go = go, site = s });
            }
        }

        void RimsOff(Fort f, Site s)
        {
            for (int i = f.rims.Count - 1; i >= 0; i--)
                if (f.rims[i].site == s) { if (f.rims[i].go) PopOut.Begin(f.rims[i].go.transform, PopOut.Style.Debris, null, true, false); f.rims.RemoveAt(i); }
        }

        /// <summary>The edge chunk grows in over 0.25 s (under the break's dust).</summary>
        internal sealed class RimPop : MonoBehaviour
        {
            public Vector3 to; float t;
            void Update() { t += Time.deltaTime / 0.25f; float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t)); transform.localScale = to * k; if (t >= 1f) enabled = false; }
        }

        /// <summary>One falling piece: contact dust + knock (age > .12 s, speed > 1.3, .3 s apart, 4 contacts at most), asleep = rubble.</summary>
        internal sealed class Chunk : MonoBehaviour
        {
            public OzFortDamage owner; public Fort fort; public Rigidbody rb; public float life; public Bounds foot; public bool big, small, resting;
            float age, contactAt = -10f; int contacts; bool popping;
            public bool Popping => popping;
            void OnCollisionEnter(Collision c)
            {
                float speed = c.relativeVelocity.magnitude;
                if (age < 0.12f || speed < 1.3f || age - contactAt < 0.3f || contacts >= 4) return;
                contactAt = age; contacts++; ChunkLandings++;
                var p = c.contactCount > 0 ? c.GetContact(0).point : transform.position;
                if (OzWaterLanding.UnderSurface(p)) return;                                                     // the water landing makes its splash
                FxManager.I?.Dust(p, Mathf.Min(big ? 1.4f : 1.0f, 0.2f + speed * 0.05f));
                if (!owner || !owner.TryKnock()) return;
                if (big && contacts == 1) { owner.Play("dst_rock_crash_large", p, GetInstanceID(), 0.8f, 1f); OpenZFxShake.I?.Impact(p, 0.3f); }
                else owner.Play("dst_debris_concrete", p, GetInstanceID(), Mathf.Clamp(0.18f + speed * 0.03f, 0.18f, small ? 0.3f : 0.45f), small ? 1.15f : 1f);
            }
            void Update()
            {
                if (popping) return;
                age += Time.deltaTime;
                if (!resting && age > 1.2f && rb && rb.IsSleeping())
                {
                    if (foot.Contains(transform.position)) { rb.isKinematic = true; resting = true; return; }   // rubble at the fort's foot
                }
                if (!resting && age > life) PopNow();                                                            // rolled out onto the open ground
            }
            public void PopNow()
            {
                if (popping) return;
                popping = true;
                if (rb) { rb.isKinematic = true; }
                PopOut.Begin(transform, PopOut.Style.Debris, null, true, false);
            }
        }

        float knockBudget; int knockFrame;
        /// <summary>Landing knocks share a small budget (3 at once, 6 a second): a shower of chips is not a wall of noise.</summary>
        bool TryKnock()
        {
            if (knockFrame != Time.frameCount) { knockFrame = Time.frameCount; knockBudget = Mathf.Min(3f, knockBudget + Time.deltaTime * 6f); }
            if (knockBudget < 1f) return false;
            knockBudget -= 1f; return true;
        }
    }
}
