using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OpenZ
{
    /// <summary>
    /// <para><b>Guard</b> (always on in game levels, cheap): every 0.5 s it looks at up to 24 physics debris bodies on the debris layer
    /// (building cells, big parts, bridge / rock chunks, wreck pieces; the list is refreshed every 2 s) that have been frozen
    /// (kinematic) or fell asleep: one that rests more than 0.35 m above the ground under it (frozen mid-air by a lifetime timer,
    /// or left on a collider that went away) is woken so it falls and settles again (handed to the water landing rule again, so it
    /// splashes when that is into water). A piece that rule holds in the water is left alone. Pieces that are popping out or
    /// fading (sinking on purpose) are skipped.</para>
    /// <para><b>Audit</b> (OpenZ/Deaths/Debris audit, Play): after a delay it measures every debris piece: floating (bottom &gt; 0.3 m
    /// above the ground and at rest), sunk (top below the ground) or mostly buried (&gt; 75 % below; the intended rubble pile sits
    /// ~30 % in), the builtin-cube count, distinct meshes and size spread, per source (BuildingBreaker cells / big parts, bridge
    /// </para>
    /// View only.
    /// </summary>
    [DefaultExecutionOrder(210)]
    public class DebrisAudit : MonoBehaviour
    {
        public static bool Guard = true;
        /// <summary>v1.3: the guard's list of bodies comes from OzDebrisBodies (true) or from a search of the whole scene every 2 s (false,
        /// the old way, the DEFAULT until check OpenZ/V1.3/Perf/2 E3 reads 0 missing / 0 extra on L02, L16 and L17 through an ending: a
        /// body source the registry misses would no longer be woken when frozen in the air, a visible change).</summary>
        public static bool UseRegistry = false;
        public static int Woken { get; private set; }
        /// <summary>1.3 item 9: vehicle wreck pieces found at rest deep in the ground and lifted onto it (this session).</summary>
        public static int Lifted { get; private set; }
        static DebrisAudit inst;
        readonly List<Rigidbody> bodies = new List<Rigidbody>();
        float nextScan, nextCheck; int cursor;
        float auditAt = -1f; string auditLabel;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot() { SceneManager.sceneLoaded -= OnLoaded; SceneManager.sceneLoaded += OnLoaded; }
        static void OnLoaded(Scene s, LoadSceneMode m) { if (!inst && (ZGame.I || FindFirstObjectByType<ZGame>())) inst = new GameObject("DebrisAudit").AddComponent<DebrisAudit>(); }

        /// <summary>Writes the audit `delay` s from now (Play).</summary>
        public static void Schedule(float delay, string label)
        {
            if (!inst) inst = new GameObject("DebrisAudit").AddComponent<DebrisAudit>();
            inst.auditAt = Time.time + delay; inst.auditLabel = label;
        }

        /// <summary>b27: the shared water landing rule took this piece into the water (it rests there partly under the surface, or it
        /// went under): it is where it belongs. Any other piece frozen in the air falls again, over water too.</summary>
        static bool InWater(Rigidbody rb) { try { return OzWaterLanding.InWater(rb.transform); } catch { return false; } }

        static bool Bounds(Transform t, out Bounds b)
        {
            b = new Bounds(t.position, Vector3.zero); bool any = false;
            foreach (var r in t.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            return any;
        }

        static bool Leaving(Rigidbody rb)
        {
            if (PopOut.IsPopping(rb.transform) || OzLavaMelt.Busy(rb.transform)) return true;              // level 7, kit LV5: the melt holds it, no second splash
            var wp = rb.GetComponent<WreckPieces>(); if (wp && wp.Fading) return true;
            return rb.transform.lossyScale.sqrMagnitude < 0.01f;
        }

        void Update()
        {
            var g = ZGame.I; if (!g) return;
            if (auditAt > 0f && Time.time >= auditAt) { auditAt = -1f; Audit(auditLabel); }
            if (!Guard) return;
            int layer = BuildingBreaker.DebrisLayer;
            if (Time.time >= nextScan)
            {
                nextScan = Time.time + 2f; bodies.Clear();
                OzDebrisBodies.Prune();                                                                   // v1.3: gone bodies leave the registry on either path (it never grows over a session)
                if (UseRegistry)
                {   // v1.3 (item 25): the bodies the game made (View/OzLiveSet.cs OzDebrisBodies), not a search of the whole scene; a hidden
                    // body is left out as the search left it out; the layer test is the old one
                    foreach (var rb in OzDebrisBodies.All)
                        if (rb && rb.gameObject.activeInHierarchy && (rb.gameObject.layer == layer || (rb.GetComponentInChildren<Collider>() is Collider c && c.gameObject.layer == layer))) bodies.Add(rb);
                }
                else
                foreach (var rb in FindObjectsByType<Rigidbody>(FindObjectsSortMode.None))
                    if (rb && rb.gameObject.layer == layer || (rb && rb.GetComponentInChildren<Collider>() is Collider c && c.gameObject.layer == layer)) bodies.Add(rb);
            }
            if (Time.time < nextCheck || bodies.Count == 0) return;
            nextCheck = Time.time + 0.5f;
            for (int n = 0; n < 24 && bodies.Count > 0; n++)
            {
                cursor = (cursor + 1) % bodies.Count;
                var rb = bodies[cursor];
                if (!rb || !rb.gameObject.activeInHierarchy || Leaving(rb)) continue;
                if (!rb.isKinematic && !rb.IsSleeping()) continue;
                if (rb.GetComponentInParent<UnitView>() || OzRockBody.RestsOnRock(rb)) continue;                                     // ragdolls / flipped wrecks: their own settle
                if (!Bounds(rb.transform, out var b)) continue;
                var c = b.center; if (InWater(rb)) continue;                                             // b27: only what the water landing rule holds is left alone (a piece frozen on a deck that is gone must fall)
                float gy = g.GroundY(c.x, c.z);
                if (b.min.y - gy > 0.35f)
                {   // frozen above the ground: let it fall again
                    rb.isKinematic = false; foreach (var col in rb.GetComponentsInChildren<Collider>()) col.enabled = true;
                    rb.WakeUp(); Woken++;
                    OzWaterLanding.Watch(rb.transform, "debris", rb);                                   // b27: when it now falls into water it splashes and rests like any other piece
                }
                else if (WreckGroundRest.LiftPiece(rb, b, gy)) Lifted++;                                // 1.3 item 9: a vehicle wreck piece at rest deep in the ground comes up to lie on it (View/WreckGroundRest.cs)
            }
        }

        void Audit(string label)
        {
            var g = ZGame.I; if (!g) return;
            int layer = BuildingBreaker.DebrisLayer;
            var rows = new List<(string src, string name, Vector3 at, float gap, float below, float h, string mesh, Vector3 size)>();
            foreach (var rb in FindObjectsByType<Rigidbody>(FindObjectsSortMode.None))
            {
                if (!rb || !rb.gameObject.activeInHierarchy || rb.GetComponentInParent<UnitView>()) continue;
                var col = rb.GetComponentInChildren<Collider>(true);
                if (rb.gameObject.layer != layer && !(col && col.gameObject.layer == layer)) continue;
                if (Leaving(rb) || !Bounds(rb.transform, out var b)) continue;
                string src = rb.GetComponent<WreckPieces>() ? "wreck piece" : rb.GetComponent<BuildingBreaker.BigPart>() ? "building big part"
                           : rb.GetComponent<BuildingBreaker.Piece>() ? "building cell" : rb.GetComponent<RockChunkLife>() ? (rb.name.Contains("Rock") ? "rock chunk" : "bridge / rock chunk") : "other";
                var mf = rb.GetComponentInChildren<MeshFilter>(); string mesh = mf && mf.sharedMesh ? mf.sharedMesh.name : "-";
                float gy = g.GroundY(b.center.x, b.center.z);
                bool rest = rb.isKinematic || rb.IsSleeping() || rb.linearVelocity.sqrMagnitude < 0.04f;
                float gap = rest && !InWater(rb) && !OzRockBody.RestsOnRock(rb) ? b.min.y - gy : 0f;                                   // b27: a piece the water landing rule holds is not "floating"; one left in the air over water is
                float below = b.size.y > 0.01f ? Mathf.Clamp01((gy - b.min.y) / b.size.y) : 0f;
                rows.Add((src, rb.name, b.center, gap, below, b.size.y, mesh, b.size));
            }
            var sb = new StringBuilder();
            string stamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
            sb.AppendLine($"# Debris audit {stamp} ({label})");
            sb.AppendLine();
            sb.AppendLine($"{rows.Count} debris bodies alive; settle guard woke {Woken} frozen-in-the-air pieces and lifted {Lifted} sunk wreck pieces this session.");
            sb.AppendLine();
            sb.AppendLine("| source | pieces | floating > 0.3 m | fully sunk | > 75 % buried | builtin cubes | distinct meshes | size spread (max/min height) |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|");
            var bad = new List<string>();
            foreach (var grp in GroupBy(rows))
            {
                int fl = 0, sunk = 0, buried = 0, cubes = 0; var meshes = new HashSet<string>(); float hMin = float.MaxValue, hMax = 0f;
                foreach (var r in grp.Value)
                {
                    if (r.gap > 0.3f) { fl++; bad.Add($"floating {r.gap:0.00} m: {r.src} {r.name} at {r.at:F1}"); }
                    if (r.below >= 0.999f) { sunk++; bad.Add($"sunk: {r.src} {r.name} at {r.at:F1}"); }
                    else if (r.below > 0.75f) buried++;
                    if (r.mesh == "Cube") cubes++;
                    meshes.Add(r.mesh); hMin = Mathf.Min(hMin, r.h); hMax = Mathf.Max(hMax, r.h);
                }
                sb.AppendLine($"| {grp.Key} | {grp.Value.Count} | {fl} | {sunk} | {buried} | {cubes} | {meshes.Count} | {(hMin > 0.001f ? hMax / hMin : 0f):0.0} |");
            }
            sb.AppendLine();
            sb.AppendLine(bad.Count == 0 ? "No floating or sunk piece: PASS" : $"FAIL: {bad.Count} pieces");
            foreach (var s in bad) sb.AppendLine("- " + s);
            string dir = OzProofWords.Records("qc");
            try { Directory.CreateDirectory(dir); File.WriteAllText(Path.Combine(dir, $"debris_audit_{stamp}.md"), sb.ToString()); } catch { }
            Debug.Log($"[OpenZ] debris audit ({label}): {Path.Combine(dir, $"debris_audit_{stamp}.md")}\n{sb}");
        }

        static Dictionary<string, List<(string src, string name, Vector3 at, float gap, float below, float h, string mesh, Vector3 size)>> GroupBy(
            List<(string src, string name, Vector3 at, float gap, float below, float h, string mesh, Vector3 size)> rows)
        {
            var d = new Dictionary<string, List<(string, string, Vector3, float, float, float, string, Vector3)>>();
            foreach (var r in rows) { if (!d.TryGetValue(r.src, out var l)) d[r.src] = l = new List<(string, string, Vector3, float, float, float, string, Vector3)>(); l.Add(r); }
            return d;
        }
    }
}
