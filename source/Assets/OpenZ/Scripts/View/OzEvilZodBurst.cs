using System.Collections.Generic;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// paint: head with hat, chest, hips, upper and lower arms, upper and lower legs. They fly out from the chest, fall, bounce, lie for some seconds and sink into the ground. View only: nothing of the rules
    /// is read, its random is the view's. The gun, the cloak, sparks and chips are RobotPopDeath's. No new art.
    /// </summary>
    [DefaultExecutionOrder(96)]
    public sealed class OzEvilZodBurst : MonoBehaviour
    {
        public static float Gravity = 13f, OutMin = 4f, OutMax = 9f, UpMin = 3.5f, UpMax = 8f, SpinMax = 300f, Bounce = 0.32f, LieSeconds = 7f, SinkSeconds = 1f;
        public static int Pieces { get; private set; }
        /// <summary>
        /// same frame throws no made-up chunks (plates, boots, domes) with them; his gun, the sparks, chips and smoke stay. Off = as before.</summary>
        public static bool OwnPartsOnly = true;
        /// <summary>Checks: bursts whose bake gave no points (a built player with Read/Write off on Hero_EvilZod.fbx: the editor keeps mesh data, a player does not).</summary>
        public static int EmptyBakes { get; private set; }
        const int Groups = 11;

        sealed class Part { public Transform t; public Mesh mesh; public Vector3 v, w, box, thinL, box0, s0, c0; public float rad, lie, smokeT, squash = 1f; public int bounces; public bool rest, cloth; public Transform inner; public Vector3[] pts; }   // 1.5 kit robot_pieces_ground_r10 (A35): pts = a body piece's own points (its lowest drawn one). U9h: + the cape (cloth: its half sizes, its thin axis, its cloth node and how flat it lies)
        readonly List<Part> parts = new List<Part>();
        float lastClang = -10f, age;

        static int GroupOf(string n)
        {
            if (n.Contains("Head") && !n.Contains("Hand") || n.Contains("Neck")) return 0;
            if (n.Contains("LeftForeArm") || n.Contains("LeftHand")) return 1;
            if (n.Contains("LeftArm") || n.Contains("LeftShoulder")) return 2;
            if (n.Contains("RightForeArm") || n.Contains("RightHand")) return 3;
            if (n.Contains("RightArm") || n.Contains("RightShoulder")) return 4;
            if (n.Contains("LeftLeg") || n.Contains("LeftFoot") || n.Contains("LeftToe")) return 5;
            if (n.Contains("LeftUpLeg")) return 6;
            if (n.Contains("RightLeg") || n.Contains("RightFoot") || n.Contains("RightToe")) return 7;
            if (n.Contains("RightUpLeg")) return 8;
            if (n.Contains("Spine1") || n.Contains("Spine2")) return 9;
            return 10;
        }

        /// <summary>OzEvilZodFx, the frame the body bursts, before the view hides. centre: his chest. The number of pieces made.</summary>
        public static int Play(SkinnedMeshRenderer skin, Vector3 centre)
        {
            if (!skin || !skin.sharedMesh) return 0;
            var go = new GameObject("EvilZod burst"); var me = go.AddComponent<OzEvilZodBurst>();
            int n = me.Cut(skin, centre); bool cape = me.Cape(skin, centre);                                // U9h (row 88): his cape one more piece
            if (n == 0 && !cape) Destroy(go);
            else if (n > 0 && OwnPartsOnly) RobotPopDeath.NoChunksFrame = Time.frameCount;                // open_fixes_3nov row 84: OzEvilZodFx.BurstBody calls the pop next, this frame
            if (cape) Capes++;
            Debug.Log($"[OpenZ EvilZod] burst: {n} pieces of his own mesh ({skin.sharedMesh.name}, readable {skin.sharedMesh.isReadable}, {skin.sharedMesh.vertexCount} points){(cape ? " + his cape" : ", no cape piece")}{(n > 0 && OwnPartsOnly ? ", no made-up chunks" : "")}");
            Pieces += n;
            return n;
        }

        /// <summary>Checks: bursts whose cape became a piece.</summary>
        public static int Capes { get; private set; }

        /// <summary>
        /// one more piece with his body pieces' flight, bounce, rest and sink; it turns flat as it comes down and lies on the ground, its lowest point on it. Its live
        /// renderer is switched off: the robot pop that follows takes only shown renderers, so it no longer throws the cloak's mesh about a point far from the cloth
        /// (that piece landed on its pivot and left the cloth hanging in the air). The view is hidden right after and never shown again.</summary>
        bool Cape(SkinnedMeshRenderer skin, Vector3 centre)
        {
            var an = skin.GetComponentInParent<Animator>(); var cape = an ? an.GetComponentInChildren<OzEvilZodCape>() : null;
            if (!cape) return false;
            var cr = cape.GetComponent<MeshRenderer>(); var cf = cape.GetComponent<MeshFilter>();
            if (!cr || !cr.enabled || !cf || !cf.sharedMesh) return false;
            var ct = cape.transform; var lb = cf.sharedMesh.bounds; var sc = ct.lossyScale;
            var m = Instantiate(cf.sharedMesh); m.name = "EvilZod piece cape";
            var go = new GameObject(m.name) { layer = cape.gameObject.layer }; var t = go.transform; t.SetParent(transform, false);
            t.SetPositionAndRotation(ct.TransformPoint(lb.center), ct.rotation);                         // the piece turns about the cloth's middle
            var inner = new GameObject("cloth") { layer = cape.gameObject.layer }; var it = inner.transform; it.SetParent(t, false);
            it.localPosition = -Vector3.Scale(sc, lb.center); it.localRotation = Quaternion.identity; it.localScale = sc;
            inner.AddComponent<MeshFilter>().sharedMesh = m;
            var r = inner.AddComponent<MeshRenderer>(); var mats = cr.sharedMaterials; r.sharedMaterials = mats; r.shadowCastingMode = cr.shadowCastingMode;
            if (cr.HasPropertyBlock())
            {
                var b = new MaterialPropertyBlock(); cr.GetPropertyBlock(b); r.SetPropertyBlock(b);
                for (int i = 0; i < mats.Length; i++) { var bi = new MaterialPropertyBlock(); cr.GetPropertyBlock(bi, i); if (!bi.isEmpty) r.SetPropertyBlock(bi, i); }
            }
            cr.enabled = false;
            var half = Vector3.Scale(sc, lb.extents); half = new Vector3(Mathf.Abs(half.x), Mathf.Abs(half.y), Mathf.Abs(half.z));
            var thin = half.x <= half.y && half.x <= half.z ? Vector3.right : half.y <= half.z ? Vector3.up : Vector3.forward;
            var away = t.position - centre; away.y = 0f; if (away.sqrMagnitude < 0.04f) away = Random.onUnitSphere; away.y = 0f; away.Normalize();
            parts.Add(new Part { t = t, mesh = m, rad = half.magnitude, box = half, box0 = half, s0 = sc, c0 = lb.center, inner = it, thinL = thin, cloth = true, w = Random.onUnitSphere * Random.Range(40f, 120f),
                                 v = away * Random.Range(OutMin, OutMax) * 0.6f + Vector3.up * Random.Range(UpMin, UpMax) * 0.7f + Vector3.ProjectOnPlane(Random.onUnitSphere, Vector3.up) });
            return true;
        }

        /// <summary>The cape's half height over its middle as it is turned now (its box).</summary>
        static float HalfY(Part p) { var q = p.t.rotation; return Mathf.Abs((q * Vector3.right).y) * p.box.x + Mathf.Abs((q * Vector3.up).y) * p.box.y + Mathf.Abs((q * Vector3.forward).y) * p.box.z; }
        /// <summary>The cape eases flat once it touches the ground: its thin axis to the vertical (either face up; the cloth is two-sided) and the cloth's
        /// fold along that axis pressed out (to about ClothLieM thick), so the whole cloth lies on the ground, not a curved shell standing on its edge.</summary>
        static void Flat(Part p, float dt)
        {
            var a = p.t.rotation * p.thinL; var nu = OzRestSurface.UpAt(p.t.position); var q = Quaternion.FromToRotation(a, Vector3.Dot(a, nu) >= 0f ? nu : -nu);   /* 1.5 kit robot_pieces_ground_r11b (A35): along the ground's slope (OzRestSurface.UpAt; off: up) */ p.t.rotation = Quaternion.Slerp(Quaternion.identity, q, 1f - Mathf.Exp(-9f * dt)) * p.t.rotation;
            float thin0 = Vector3.Dot(p.box0, p.thinL), goal = thin0 > ClothLieM ? ClothLieM / thin0 : 1f;
            p.squash = Mathf.MoveTowards(p.squash, goal, dt / 0.3f);
            var k = Vector3.one - p.thinL * (1f - p.squash); p.box = Vector3.Scale(p.box0, k);
            if (p.inner) { var s = Vector3.Scale(p.s0, k); p.inner.localScale = s; p.inner.localPosition = -Vector3.Scale(s, p.c0); }
        }
        public static float ClothLieM = 0.05f;

        int Cut(SkinnedMeshRenderer skin, Vector3 centre)
        {
            var baked = new Mesh { name = "EvilZod baked" }; skin.BakeMesh(baked, true);
            if (baked.vertexCount == 0) { EmptyBakes++; Debug.LogWarning("[OpenZ EvilZod] burst: the bake of " + skin.sharedMesh.name + " gave no points (readable " + skin.sharedMesh.isReadable + "; a built player needs Read/Write on Hero_EvilZod.fbx): no pieces"); Destroy(baked); return 0; }
            Vector3[] vs = baked.vertices, ns = baked.normals; Vector2[] uv = baked.uv; Vector4[] ts = baked.tangents;
            var bones = skin.bones; int nv = vs.Length;
            var at = Matrix4x4.TRS(skin.transform.position, skin.transform.rotation, Vector3.one);
            var vg = new int[nv];
            var near = new List<(Vector3 p, int g)>();                                          // his mesh is not kept readable, so no weights: each point goes to the nearest of his main bones
            foreach (var b in bones) if (b && !b.name.Contains("Thumb") && !b.name.Contains("Index") && !b.name.Contains("Middle") && !b.name.Contains("Ring") && !b.name.Contains("Pinky")) near.Add((b.position, GroupOf(b.name)));
            for (int i = 0; i < nv; i++)
            {
                var w = at.MultiplyPoint3x4(vs[i]); float best = float.MaxValue; int pick = 10;
                for (int b = 0; b < near.Count; b++) { float d = (near[b].p - w).sqrMagnitude; if (d < best) { best = d; pick = near[b].g; } }
                vg[i] = pick;
            }
            int subs = baked.subMeshCount; var remap = new int[nv];
            var mats = skin.sharedMaterials; var block = new MaterialPropertyBlock(); skin.GetPropertyBlock(block);
            // RUN7: TeamPaint writes the team paint per material slot (SetPropertyBlock(block, i)); the pieces take those slot blocks too, or the plates
            // fall white (no _TeamColor) and a white plate in the sun's mirror angle blooms into a big white glare for seconds
            var slot = new MaterialPropertyBlock[mats.Length];
            if (skin.HasPropertyBlock()) for (int i = 0; i < mats.Length; i++) { var b = new MaterialPropertyBlock(); skin.GetPropertyBlock(b, i); if (!b.isEmpty) slot[i] = b; }
            int made = 0; var keep = skin.transform.rotation;
            for (int g = 0; g < Groups; g++)
            {
                for (int i = 0; i < nv; i++) remap[i] = -1;
                var pv = new List<Vector3>(); var pn = new List<Vector3>(); var pu = new List<Vector2>(); var pt = new List<Vector4>(); var sub = new List<List<int>>();
                var mid = Vector3.zero;
                for (int s = 0; s < subs; s++)
                {
                    var src3 = baked.GetTriangles(s); var dst = new List<int>();
                    for (int i = 0; i + 2 < src3.Length; i += 3)
                    {
                        if (vg[src3[i]] != g) continue;
                        for (int k = 0; k < 3; k++)
                        {
                            int o = src3[i + k];
                            if (remap[o] < 0) { remap[o] = pv.Count; pv.Add(vs[o]); mid += vs[o]; if (ns.Length == nv) pn.Add(ns[o]); if (uv.Length == nv) pu.Add(uv[o]); if (ts.Length == nv) pt.Add(ts[o]); }
                            dst.Add(remap[o]);
                        }
                    }
                    sub.Add(dst);
                }
                if (pv.Count == 0) continue;
                mid /= pv.Count; float rad = 0f;
                for (int i = 0; i < pv.Count; i++) { pv[i] -= mid; rad = Mathf.Max(rad, pv[i].magnitude); }
                var m = new Mesh { name = "EvilZod piece " + g, indexFormat = pv.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
                m.SetVertices(pv); if (pn.Count > 0) m.SetNormals(pn); if (pu.Count > 0) m.SetUVs(0, pu); if (pt.Count > 0) m.SetTangents(pt);
                m.subMeshCount = subs; for (int s = 0; s < subs; s++) m.SetTriangles(sub[s], s); m.RecalculateBounds();
                var go = new GameObject(m.name) { layer = skin.gameObject.layer }; var t = go.transform; t.SetParent(transform, false);
                t.SetPositionAndRotation(at.MultiplyPoint3x4(mid), keep);
                go.AddComponent<MeshFilter>().sharedMesh = m;
                var r = go.AddComponent<MeshRenderer>(); r.sharedMaterials = mats; r.SetPropertyBlock(block);
                for (int i = 0; i < slot.Length; i++) if (slot[i] != null) r.SetPropertyBlock(slot[i], i);
                var away = t.position - centre; away.y = 0f; if (away.sqrMagnitude < 0.04f) away = Random.onUnitSphere; away.y = 0f; away.Normalize();
                parts.Add(new Part { pts = OzRobotPieceRest.Sample(pv), t = t, mesh = m, rad = rad, w = Random.onUnitSphere * Random.Range(SpinMax * 0.4f, SpinMax), v = away * Random.Range(OutMin, OutMax) + Vector3.up * Random.Range(UpMin, UpMax) + Vector3.ProjectOnPlane(Random.onUnitSphere, Vector3.up) * 2f });
                made++;
            }
            Destroy(baked);
            return made;
        }

        void LateUpdate()
        {
            var g = ZGame.I; if (!g) { Destroy(gameObject); return; }
            float dt = Time.deltaTime * Mathf.Max(0.25f, g.gameSpeed); age += dt; var z = OpenZFxPlayer.I; bool any = false;
            for (int i = 0; i < parts.Count; i++)
            {
                var p = parts[i]; if (!p.t) continue;
                if (p.rest)
                {
                    if (p.cloth && p.lie < LieSeconds) { Flat(p, dt); var q = p.t.position; float cy = OzRestSurface.CapeY(p.t); q.y = float.IsNaN(cy) ? g.GroundY(q.x, q.z) + HalfY(p) + 0.02f : cy; p.t.position = q; }
                    else if (p.pts != null && p.lie < LieSeconds) { if (!OzRestSurface.EvilRest(p.t, p.mesh, dt, g)) OzRobotPieceRest.Settle(p.t, p.pts, g, dt, ref p.v.y); }   /* 1.5 kit robot_pieces_ground_r11b (A35): on a box face, on the real surface, lifted out of the ground too (View/OzRestSurface.cs) */
                    p.lie += dt;
                    if (p.lie > LieSeconds)
                    {
                        p.t.position += Vector3.down * (p.rad * 1.6f / SinkSeconds * dt);
                        if (p.lie > LieSeconds + SinkSeconds) { Destroy(p.t.gameObject); p.t = null; continue; }
                    }
                    any = true; continue;
                }
                any = true;
                p.v.y -= Gravity * dt;
                var at = p.t.position + p.v * dt;
                p.t.rotation = Quaternion.AngleAxis(p.w.magnitude * dt, p.w.normalized) * p.t.rotation;
                if (p.cloth && p.bounces > 0) Flat(p, dt);
                float floor = g.GroundY(at.x, at.z) + (p.cloth ? HalfY(p) + 0.02f : p.rad * 0.28f);              // U9h: the cape stops on its lowest point
                if (at.y <= floor && p.v.y < 0f)
                {
                    at.y = floor; bool hard = p.v.y < -4f; p.bounces++;
                    p.v = new Vector3(p.v.x * 0.5f, -p.v.y * Bounce, p.v.z * 0.5f); p.w *= 0.45f;
                    if (hard && z) z.EmitChannel("m12_dust", at + Vector3.up * 0.1f, Vector3.up * 0.6f, 0.7f, 0.8f, OzEvilZodFx.Dirt, 0.34f);
                    if (hard && p.cloth) AudioDirector.Fire(OzBodySounds.ThudEvent, at, -1, 0.35f, Random.Range(0.9f, 1.05f));   // U9h: the cloth lands soft
                    else if (hard && Time.time - lastClang >= 0.09f) { lastClang = Time.time; AudioDirector.Fire("dst_debris_metal", at, -1, 0.55f, Random.Range(0.8f, 1.05f)); }
                    if (p.bounces >= 3 || p.v.y < 1f) { p.rest = true; p.v = Vector3.zero; }
                }
                p.t.position = at;
                if (z && age < 0.8f && Time.time >= p.smokeT)
                {
                    p.smokeT = Time.time + 0.07f;
                    z.EmitChannel("wv2_spark", at, Random.onUnitSphere * 2.5f, 0.25f, 0.05f, OpenZElectroFx.Ice, 1f);
                    z.EmitChannel("m12_smoke", at, Vector3.up * 0.4f, 0.9f, 0.32f, new Color(0.26f, 0.27f, 0.3f), 0.32f);
                }
            }
            if (!any) Destroy(gameObject);
        }

        void OnDestroy() { foreach (var p in parts) if (p.mesh) Destroy(p.mesh); }
    }
}
