using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OpenZ
{
    /// <summary>
    /// 1.2 owner item 176 (the gun falls apart at its death, View/OzGunBreakup.cs): the FIRST break of a session cost 11.12 ms (wanted under
    /// 5), every later one about 1.25 ms. Its turret split is not in that number (counted on its own, 0.32 ms), and the second gun of the
    /// same run, the other model with its own split made then too, cost 1.25 ms: so the extra 10 ms is not the per-model split but the
    /// work a break does once per session: its own code and the code it calls compiled on their first call (the break, the lists of
    /// value pairs it fills, walks and sorts, the wreck piece, the rock body, the statics' drop, the engine's body / collider / renderer
    /// calls), the debris layer's collision setting and the wreck's physics material made on their first use.
    /// Now that work is done AHEAD, one small step per frame from the <see cref="StartFrame"/>th frame of every level with a game (the
    /// code steps once per session, the split step every level); nothing of the break changes (its look, parts, sounds, counters and
    /// costs are measured as before), later breaks unchanged:
    ///  1. the debris layer and the wreck material; the classes the break uses set up;
    ///  2. the break's code and the code it calls compiled: each called once with nothing to work on (it returns at its first test);
    ///  3. the lists of value pairs the break uses, filled, walked and sorted on empty entries; one line formatted as the break's;
    ///  4. each gun model's turret split made (the break's own cache: v2, v1 and the guns already in the level) and its parts' meshes
    ///     sent to the graphics card;
    ///  5. two practice parts made with the engine calls a thrown part gets (mesh, renderer with the gun's look, collider, body, the
    ///     two ignoring each other), far under the map, never drawn (renderer off), no gravity, no contact, no sound, no wreck rules;
    ///  6. the practice parts taken away.
    /// Each step's milliseconds: <see cref="PrewarmReport"/>. Switch: <see cref="Prewarm"/> (false = as before: the first break does it).
    /// </summary>
    public static partial class OzGunBreakup
    {
        /// <summary>The switch (false = no prewarm: the first break of a session compiles and makes its things itself, as before).</summary>
        public static bool Prewarm = true;
        /// <summary>The frame after a level's load the prewarm starts at (one step per frame after it).</summary>
        public static int StartFrame = 10;
        /// <summary>Statistics: prewarms run (levels), code steps done (once per session), turret splits made by the prewarm, the slowest
        /// step (ms); the last prewarm in words.</summary>
        public static int Prewarms, PrewarmSplits; public static bool PrewarmCodeDone; public static float PrewarmStepMaxMs;
        public static string PrewarmReport = "";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void PrewarmBoot() { SceneManager.sceneLoaded -= PrewarmOnScene; SceneManager.sceneLoaded += PrewarmOnScene; }
        /// <summary>Run 6: the first scene of a session (a level started from its own scene in the editor never called the hook above).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void PrewarmFirst() { if (Object.FindFirstObjectByType<GunBreakPrewarm>() == null) PrewarmOnScene(SceneManager.GetActiveScene(), LoadSceneMode.Single); }

        static void PrewarmOnScene(Scene s, LoadSceneMode m)
        {
            if (!Prewarm || m != LoadSceneMode.Single) return;
            // run 6: the game object may come up after this call (it did not run on a level started from its own scene): the runner waits for it
            var go = new GameObject("OZ_GunBreakPrewarm");
            if (s.IsValid() && go.scene != s) SceneManager.MoveGameObjectToScene(go, s);
            go.AddComponent<GunBreakPrewarm>();
        }

        /// <summary>The prewarm's runner (one per level; it removes itself when done).</summary>
        sealed class GunBreakPrewarm : MonoBehaviour
        {
            IEnumerator Start()
            {
                for (int i = 0; i < 600 && !(ZGame.I && ZGame.I.World != null); i++) yield return null;   // run 6: a level only (the menu has none)
                if (!(ZGame.I && ZGame.I.World != null)) { Destroy(gameObject); yield break; }
                for (int i = 0; i < StartFrame; i++) yield return null;
                var steps = new System.Text.StringBuilder();
                var sw = new System.Diagnostics.Stopwatch();
                float Took(string what) { sw.Stop(); float ms = (float)sw.Elapsed.TotalMilliseconds; PrewarmStepMaxMs = Mathf.Max(PrewarmStepMaxMs, ms); steps.Append(what).Append(' ').Append(ms.ToString("0.00")).Append(" ms; "); sw.Reset(); return ms; }
                Prewarms++;
                if (!PrewarmCodeDone)
                {
                    PrewarmCodeDone = true;
                    sw.Start(); WarmStatics(); Took("statics"); yield return null;
                    sw.Start(); WarmCalls(); Took("code"); yield return null;
                    sw.Start(); WarmPairs(); Took("pairs"); yield return null;
                }
                var practice = new List<GameObject>();
                int splits0 = Splits;
                foreach (var (src, look, v2) in TurretMeshes())
                {
                    sw.Start(); var parts = Split(src, v2 ? V2Spec : V1Spec, out bool made); if (made) PrewarmSplits++;
                    if (parts != null) foreach (var p in parts) if (p) p.UploadMeshData(false);   // to the graphics card now (kept readable)
                    Took("split v" + (v2 ? 2 : 1) + (made ? " made" : " cached"));
                    if (practice.Count == 0 && parts != null && look)
                    {
                        yield return null;
                        if (look) { sw.Start(); Practice(parts, look, practice); Took("practice parts"); }   // (the gun whose look it copies may have gone in that frame)
                    }
                    yield return null;
                }
                yield return null;
                sw.Start();
                foreach (var go in practice) if (go) { go.SetActive(false); Destroy(go); }
                Took("practice parts away");
                PrewarmReport = $"gun break prewarm (level {Prewarms}): {steps.ToString().Trim()} splits made now {Splits - splits0}, the slowest step {PrewarmStepMaxMs:0.00} ms";
                Debug.Log("[OpenZ] " + PrewarmReport);   // run 6: in the player log too (the exe check reads it)
                Destroy(gameObject);
            }
        }

        /// <summary>Step 1: the debris layer's collision setting, the wreck's physics material, the classes the break uses.</summary>
        static void WarmStatics()
        {
            _ = BuildingBreaker.DebrisLayer; _ = WreckFx.Material;
            foreach (var t in new[] { typeof(WreckPieces), typeof(OzRockBody), typeof(OzStaticSolid), typeof(RobotDeathRule), typeof(WreckFx), typeof(AudioDirector) })
                RuntimeHelpers.RunClassConstructor(t.TypeHandle);
        }

        /// <summary>Step 2: the code of the break compiled: the break and the code it calls, each called with nothing to work on (each returns
        /// at its first test: no gun, no part, no body, no listed static), then every method of the break and of its helper classes made
        /// ready (where the runtime can).</summary>
        static void WarmCalls()
        {
            AtDeath(null, null, null);
            WreckPieces.Single(null, Vector3.zero, 1f, 0);
            OzRockBody.Add(null, null, OzRockBody.Kind.WreckPiece, Vector3.one, 1f);
            OzStaticSolid.DropForBreak(0); OzStaticSolid.ConcaveOff(null);
            uint s = RobotDeathRule.Hash(1, 6, 176u) | 1u; WreckFx.Rnd(ref s);
            var all = new List<System.Type> { typeof(OzGunBreakup) };
            all.AddRange(typeof(OzGunBreakup).GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public));
            foreach (var t in all)
            {
                if (t.ContainsGenericParameters || t == typeof(GunBreakPrewarm)) continue;
                foreach (var m in t.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (m.IsAbstract || m.ContainsGenericParameters) continue;
                    try { RuntimeHelpers.PrepareMethod(m.MethodHandle); } catch (System.Exception) { }    // a runtime that cannot: the first break does it
                }
            }
        }

        /// <summary>Step 3: the break's lists of value pairs (its parts, its bodies sorted by mass), filled, walked and sorted on empty
        /// entries, and one line formatted as the break's own.</summary>
        static void WarmPairs()
        {
            var parts = new List<(Transform t, int kind)>(8) { (null, Barrel), (null, Collar), (null, Top) };
            int n = 0;
            foreach (var (t, kind) in parts) n += t ? kind : 1;
            var made = new List<(Rigidbody rb, WreckPieces wp, Collider[] cols, Part rec)>(8);
            for (int i = 0; i < 6; i++) made.Add((null, null, null, new Part { kind = KindName[i % KindName.Length] }));
            made.Sort((a, b) => string.CompareOrdinal(b.rec.kind, a.rec.kind));
            for (int i = 0; i < made.Count; i++) n += made[i].cols == null ? 1 : 0;
            foreach (var m in made) n += m.rec != null ? 1 : 0;
            var names = new System.Text.StringBuilder(); foreach (var m in made) names.Append(m.rec.kind).Append(' ');
            _ = $"gun #{n} v{(n > 0 ? 2 : 1)}: {made.Count} parts ({names.ToString().Trim()}) in {0.5f:0.00} ms (its colliders off by {0.1f:0.00} ms, the parts made by {0.2f:0.00} ms, their bodies by {0.3f:0.00} ms; statics dropped {StaticsDropped}, concave colliders taken off parts {ConcaveDropped})";
        }

        /// <summary>Step 4's meshes: each gun model's turret (the v2 view prefab, v1's unit prefab) and those of the guns already in the level,
        /// with the renderer whose look a thrown part copies.</summary>
        static IEnumerable<(Mesh src, MeshRenderer look, bool v2)> TurretMeshes()
        {
            var seen = new HashSet<Mesh>();
            var roots = new List<(Transform t, bool v2)>();
            var g = ZGame.I;
            var p2 = OzGun.Models.PrefabV2; if (p2) roots.Add((p2.transform, true));
            if (g && g.unitPrefabs != null) { var p1 = System.Array.Find(g.unitPrefabs, x => x && x.name == OzGun.V1Key); if (p1) roots.Add((p1.transform, OzGun.Models.IsV2(p1.transform))); }
            if (g && g.Views != null)
                foreach (var kv in g.Views)
                    if (kv.Value && !kv.Value.Dying && OzGun.Is(kv.Value.Obj)) roots.Add((kv.Value.transform, OzGun.Models.IsV2(kv.Value.transform)));
            foreach (var (t, v2) in roots)
            {
                if (!t) continue;                                                                           // a gun gone while the prewarm waited a frame
                var surf = Deep(t, "CannonTurretYaw_Surface");
                var mf = surf ? surf.GetComponent<MeshFilter>() : null; var mr = surf ? surf.GetComponent<MeshRenderer>() : null;
                if (!mf || !mf.sharedMesh || !seen.Add(mf.sharedMesh)) continue;
                yield return (mf.sharedMesh, mr, v2);
            }
        }

        /// <summary>Step 5: two practice parts made with a thrown part's engine calls (View/WreckPieces.cs Single, without its wreck rules):
        /// a holder with the part's mesh and the gun's look (renderer off: never drawn), a box collider on the debris layer with the wreck
        /// material, a body (no gravity, no contact, still), the two ignoring each other; 2 km under the map's origin.</summary>
        static void Practice(Mesh[] parts, MeshRenderer look, List<GameObject> keep)
        {
            var cols = new List<Collider>(2);
            for (int k = 1, made = 0; k < parts.Length && made < 2; k++)
            {
                if (!parts[k]) continue;
                var go = new GameObject("OZ_GunPart_prewarm") { layer = look.gameObject.layer };
                go.transform.SetPositionAndRotation(new Vector3(0f, -2000f - 10f * made, 0f), Quaternion.identity);
                go.AddComponent<MeshFilter>().sharedMesh = parts[k];
                var mr = go.AddComponent<MeshRenderer>(); CopyLook(look, mr); Quiet(go.transform); mr.enabled = false;
                var piece = new GameObject("OZ_WreckPiece_prewarm"); piece.transform.position = go.transform.position;
                go.transform.SetParent(piece.transform, true);
                var col = new GameObject("Hull") { layer = BuildingBreaker.DebrisLayer };
                col.transform.SetParent(piece.transform, false);
                var bc = col.AddComponent<BoxCollider>(); bc.size = Vector3.one * 0.5f; bc.sharedMaterial = WreckFx.Material;
                var body = piece.AddComponent<Rigidbody>();
                body.useGravity = false; body.detectCollisions = false;
                body.mass = 10f; body.linearDamping = 0.28f; body.angularDamping = 0.95f; body.maxAngularVelocity = 16f;
                body.interpolation = RigidbodyInterpolation.Interpolate; body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; body.WakeUp();
                foreach (var r in piece.GetComponentsInChildren<Renderer>()) r.motionVectorGenerationMode = MotionVectorGenerationMode.Object;
                OzStaticSolid.ConcaveOff(piece.transform); _ = Deep(piece.transform, "Hull");
                cols.AddRange(piece.GetComponentsInChildren<Collider>());
                keep.Add(piece); made++;
            }
            if (cols.Count >= 2 && cols[0] && cols[1]) Physics.IgnoreCollision(cols[0], cols[1], true);
        }
    }
}
