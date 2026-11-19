using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OpenZ
{
    /// <summary>
    /// Level 18 "Bridge Game" (kit level18_containers), the view of DV201 (Sim/ZWorld.L18Containers.cs): the rusty shipping containers half sunk
    /// and tilted in the level's water. View only: it reads the rules, it never writes them.
    /// <list type="bullet">
    /// <item>The containers are static scene objects made by menu OpenZ/L18 Bridge Game/22 (Environment/L18_Containers, one host per container
    /// named "&lt;id&gt;_container", a child "Standing" with its LOD group and a hidden child "Wreck"). At the start each host is bound to its sim
    /// object (by the footprint of the rule's table); a host without one ('Zod exact', a refused footprint) is hidden, so nothing stands in open
    /// water the rules do not know.</item>
    /// <item>Hit (its hit points fall): sparks, rust and paint flakes, a splash at the water line, a metal clang, the box shudders a little.</item>
    /// <item>Destroyed (the rule's object is gone): the game's metal break-up and debris, rust and paint bits, a big splash and spray, the metal
    /// crash and the debris splash; the standing box is swapped for its crushed wreck, which slumps lower over 3 s with bubbles and then stays,
    /// mostly under the water (its top about 0.4 m over the surface, placed so by the menu: the tiles are open again, robots wade past its submerged hull).</item>
    /// <item>While standing: water laps along its sides, and now and then the steel creaks.</item>
    /// </list>
    /// Boots itself on level 18 (as HarbourDetails does on level 17).
    /// </summary>
    [DefaultExecutionOrder(74)]
    public class OzL18Containers : MonoBehaviour
    {
        public const string RootName = "L18_Containers";
        public static OzL18Containers I { get; private set; }
        public static bool AutoBoot = true;
        /// <summary>Proof counters (the last level start and this session).</summary>
        public static int Bound, Hidden, Hits, Breaks;

        [Tooltip("seconds the wreck takes to slump to its rest")] public float slumpSeconds = 3f;
        public bool playSounds = true, verbose;

        sealed class Box
        {
            public string id; public int index, simId = -1, lastHp; public ZObject sim;
            public Transform host, standing, wreck; public Vector3 centre, wreckRest, wreckFrom; public Quaternion standRot;
            public bool dead; public float slump = -1f, shake, lap, creak; public Vector3 shakeAxis;
        }

        ZGame game; Camera cam; float waterY; System.Random rng; bool subscribed;
        readonly List<Box> boxes = new List<Box>();
        DetailKit.Bits bits;
        const byte BRust = 0, BRed = 1, BBlue = 2, BSteel = 3;
        static readonly Color Foam = new Color(0.72f, 0.7f, 0.58f);

        static bool hooked;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (!hooked) { hooked = true; SceneManager.sceneLoaded += (s, m) => TryAttach(); }
            TryAttach();
        }

        public static void TryAttach()
        {
            if (!AutoBoot || !ZGame.I || !OzCity.IsBridgeGame(ZGame.I.level) || FindFirstObjectByType<OzL18Containers>()) return;
            new GameObject("OzL18Containers").AddComponent<OzL18Containers>();
        }

        void Awake() { I = this; }

        void Start()
        {
            game = ZGame.I ? ZGame.I : FindFirstObjectByType<ZGame>();
            if (!game || game.World == null || !OzCity.IsBridgeGame(game.level)) { enabled = false; return; }
            cam = Camera.main; rng = new System.Random(1801);
            waterY = DetailKit.WaterY(game.level);
            bits = new DetailKit.Bits(new[] { new Color(0.55f, 0.27f, 0.12f), new Color(0.5f, 0.22f, 0.17f), new Color(0.38f, 0.47f, 0.55f), new Color(0.55f, 0.56f, 0.57f) });
            Bind();
            FxManager.Exploded += OnExplosion; subscribed = true;
        }

        void OnDestroy()
        {
            if (subscribed) FxManager.Exploded -= OnExplosion;
            if (I == this) I = null;
        }

        /// <summary>Each scene host to its rule object; hosts without one hidden.</summary>
        void Bind()
        {
            Bound = Hidden = 0; boxes.Clear();
            var w = game.World;
            var env = GameObject.Find("Environment");
            var root = env ? env.transform.Find(RootName) : null;
            if (!root) { Debug.Log($"[OpenZ] level 18 containers (DV201): no Environment/{RootName} in the scene (menu OpenZ/L18 Bridge Game/22 Containers places them); {w.L18Containers.Count} in the rules"); return; }
            float deepest = 0f;
            for (int i = 0; i < root.childCount; i++)
            {
                var h = root.GetChild(i);
                int k = System.Array.FindIndex(ZWorld.L18ContainerIds, id => h.name.StartsWith(id + "_"));
                ZObject sim = null;
                if (k >= 0) foreach (var o in w.L18Containers) if (w.L18ContainerIndex(o) == k && !o.Destroyed) { sim = o; break; }
                var st = h.Find("Standing"); var wr = h.Find("Wreck");
                if (sim == null || !st) { h.gameObject.SetActive(false); Hidden++; continue; }
                h.gameObject.SetActive(true);
                if (wr) wr.gameObject.SetActive(false);
                var b = new Box { id = ZWorld.L18ContainerIds[k], index = k, sim = sim, simId = sim.Id, lastHp = sim.Health, host = h, standing = st, wreck = wr };
                var bounds = Bounds(st);
                b.centre = bounds.center;
                float bed = game.TerrainY(b.centre.x, b.centre.z);
                deepest = Mathf.Max(deepest, bed - bounds.min.y);   // level 18's water is 0.8 m deep: a half-sunk box sits deep in the silt (proof/bed_heights.txt)
                b.standRot = st.localRotation;
                if (wr) { b.wreckRest = wr.position; b.wreckFrom = b.wreckRest + Vector3.up * 0.9f; }   // the menu placed the wreck at its rest (its top wreckTopOverWater over the water)
                b.lap = (float)rng.NextDouble() * 2f; b.creak = 10f + (float)rng.NextDouble() * 40f;
                boxes.Add(b); Bound++;
            }
            Debug.Log($"[OpenZ] level 18 containers (DV201 {(ZWorld.L18ContainersDeviation ? "on" : "off")}): {w.L18Containers.Count} in the rules, {Bound} drawn and bound, {Hidden} hidden (no rule object), the deepest low end {deepest:F1} m into the river bed's silt; water y {waterY:F2}");
        }

        static Bounds Bounds(Transform t)
        {
            bool any = false; var b = new Bounds(t.position, Vector3.zero);
            foreach (var r in t.GetComponentsInChildren<Renderer>())
            {
                if (r.GetComponentInParent<LODGroup>() is LODGroup lg && lg.GetLODs().Length > 0 && System.Array.IndexOf(lg.GetLODs()[0].renderers, r) < 0) continue;   // LOD 0 only
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            return b;
        }

        Vector3 lastBlastAt; float lastBlastTime = -99f;
        void OnExplosion(Vector3 at, float strength) { lastBlastAt = at; lastBlastTime = Time.time; }

        Vector3 From(Box b)
        {
            var d = lastBlastAt - b.centre; d.y = 0f;
            if (Time.time - lastBlastTime < 0.6f && d.sqrMagnitude < 14f * 14f) return lastBlastAt;
            return b.centre + Quaternion.Euler(0f, (b.simId * 97) % 360, 0f) * Vector3.forward * 3f;
        }

        void Update()
        {
            if (game == null || game.World == null) return;
            float dt = Time.deltaTime, now = Time.time;
            var fx = OpenZFxPlayer.I;
            foreach (var b in boxes)
            {
                if (!b.dead)
                {
                    var o = b.sim;
                    bool gone = o.Destroyed || o.KillAt >= 0 || game.World.Get(b.simId) == null;
                    int hp = gone ? 0 : o.Health;
                    if (gone || hp <= 0) Break(b, From(b));
                    else if (hp < b.lastHp) { b.lastHp = hp; Hit(b, From(b)); }
                }
                if (b.shake > 0f && b.standing && !b.dead)
                {   // a short shudder after a hit (a few degrees, gone in 0.4 s)
                    b.shake = Mathf.Max(0f, b.shake - dt);
                    float k = b.shake / 0.4f, a = Mathf.Sin(b.shake * 55f) * 2.2f * k;
                    b.standing.localRotation = Quaternion.AngleAxis(a, b.shakeAxis) * b.standRot;
                    if (b.shake <= 0f) b.standing.localRotation = b.standRot;
                }
                if (b.slump >= 0f && b.wreck)
                {   // the wreck slumps to its rest, bubbles rise round it
                    b.slump += dt;
                    float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(b.slump / slumpSeconds));
                    b.wreck.position = Vector3.Lerp(b.wreckFrom, b.wreckRest, u);
                    if (fx && b.slump < slumpSeconds + 1.5f && rng.NextDouble() < dt * 7f)
                    {
                        var bp = b.centre + new Vector3(((float)rng.NextDouble() - 0.5f) * 4f, 0f, ((float)rng.NextDouble() - 0.5f) * 4f); bp.y = waterY;
                        float ozR = 0.4f + (float)rng.NextDouble() * 0.5f; if (!OzWaterBlast.Lap(bp, ozR * 3f)) fx.EmitChannel("m12_ripple", bp + Vector3.up * 0.09f, Vector3.zero, 1.2f, ozR, Foam, 0.7f);   // 1.5 kit water_rings_rest_r6: bubbles of the slumping wreck = faint small stamps (the same random draws), no flat rings
                        fx.Droplets(bp, 0.25f, 3);
                    }
                    if (b.slump > slumpSeconds + 2f) b.slump = -1f;
                }
                if (!DetailKit.OnScreen(cam, b.centre, 0.1f)) continue;
                b.lap -= dt; b.creak -= dt;
                if (fx && b.lap <= 0f)
                {   // water laps along the hull at the water line (the wreck too, more quietly)
                    b.lap = (b.dead ? 2.2f : 1.0f) + (float)rng.NextDouble() * 1.6f;
                    float a = (float)rng.NextDouble() * Mathf.PI * 2f, r = 1.4f + (float)rng.NextDouble() * 1.6f;
                    var p = b.centre + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r); p.y = waterY + 0.09f;
                    float ozL = 0.45f + (float)rng.NextDouble() * 0.45f; if (!OzWaterBlast.Lap(p, ozL * (b.dead ? 2.4f : 3f))) fx.EmitChannel("m12_ripple", p, Vector3.zero, 1.8f, ozL, Foam, b.dead ? 0.4f : 0.55f);   // 1.5 kit water_rings_rest_r6: the hull's lap = a faint small stamp (the same random draw), no flat ring
                    if (!b.dead && rng.NextDouble() < 0.25) fx.Droplets(p, 0.15f, 2);
                }
                if (b.creak <= 0f && !b.dead)
                {
                    b.creak = 25f + (float)rng.NextDouble() * 45f;
                    if (playSounds) DetailKit.Sound("dst_wreck_creak", b.centre, 0.55f, "prop", 1f, -1, "sd_metal_creak_a", "sd_metal_creak_b");
                }
            }
            bits?.StepAndDraw(dt, game);
        }

        byte Paint(Box b)
        {
            var r = b.standing ? b.standing.GetComponentInChildren<Renderer>() : null;
            return r && r.sharedMaterial && r.sharedMaterial.name.Contains("Blue") ? BBlue : BRed;
        }

        /// <summary>A hit that does not break it: sparks, flakes, a splash, a clang, a shudder.</summary>
        void Hit(Box b, Vector3 from)
        {
            Hits++;
            var dir = b.centre - from; dir.y = 0f; dir = dir.sqrMagnitude > 0.01f ? dir.normalized : Vector3.forward;
            var at = b.centre - dir * 1.1f + Vector3.up * 0.6f;
            FxManager.I?.Pop(at, 0.55f);
            bits?.Burst(at, 6, BRust, 2.6f, 0.09f, rng); bits?.Burst(at, 4, Paint(b), 2.4f, 0.08f, rng);
            var fx = OpenZFxPlayer.I; if (fx) fx.SplashStamped(new Vector3(at.x, waterY, at.z), 0.8f);   // 1.5 kit water_rings_rest_r6: a container hit: the splash's droplets + a small stamp in the water, no flat ring
            b.shake = 0.4f; b.shakeAxis = Vector3.Cross(Vector3.up, dir).normalized;
            if (playSounds) DetailKit.Sound("dtl_wreck_hit", b.centre, 0.85f, "tank_impact", 0.15f, -1, "final-metal-land-a", "final-metal-land-b", "final-metal-land-c");
            if (verbose) Debug.Log($"[OpenZ] level 18 container {b.id}: hit, {b.lastHp} HP left");
        }

        /// <summary>Destroyed by the rules: the game's metal break-up, debris and sounds; the crushed wreck takes the box's place and slumps.</summary>
        void Break(Box b, Vector3 from)
        {
            if (b.dead) return;
            b.dead = true; Breaks++;
            var dir = b.centre - from; dir.y = 0f; dir = dir.sqrMagnitude > 0.01f ? dir.normalized : Vector3.forward;
            var p = new Vector3(b.centre.x, waterY, b.centre.z);
            var fx = OpenZFxPlayer.I;
            FxManager.I?.Breakup(p + Vector3.up * 0.9f, "metal", 2.6f, 14, 1.1f);
            bits?.Burst(p + Vector3.up * 1.0f, 16, BRust, 3.4f, 0.11f, rng); bits?.Burst(p + Vector3.up * 1.1f, 12, Paint(b), 3.2f, 0.12f, rng); bits?.Burst(p + Vector3.up * 0.9f, 6, BSteel, 3.6f, 0.07f, rng);
            if (fx) { if (OzWaterBlast.Heavy(p, 1.8f)) fx.SplashStamped(p, 1.8f); else { fx.Splash(p, 1.8f); fx.Ripple(p, 1.6f); } fx.DemolitionDust(p + Vector3.up * 0.4f, 3.2f, 2.2f, 0.5f); fx.Droplets(p + dir * 1.2f, 0.6f, 10); }   // 1.5 kit water_rings_rest_r6: the broken container: a heavy stamp (4.8 m of broken crests, flecks, a brief hole) + the splash's droplets; no 8.4 m / 7.4 m flat rings
            if (b.standing) b.standing.gameObject.SetActive(false);
            if (b.wreck)
            {
                b.wreck.gameObject.SetActive(true);
                b.wreck.position = b.wreckFrom; b.slump = 0f;
            }
            if (playSounds)
            {
                DetailKit.Sound("dtl_wreck_break", b.centre, 1f, "wreck_crash", 0.1f, -1, "final-metal-land-a", "final-metal-land-b", "final-metal-land-c");
                DetailKit.Sound("dst_debris_splash", b.centre, 0.9f, "debris", 0.05f, -1, "sd_debris_splash_a", "sd_debris_splash_b");
            }
            Debug.Log($"[OpenZ] level 18 container {b.id} destroyed (DV201): broke up, its wreck settles low in the water, its tiles are open again");
        }

        /// <summary>Checks / proof clips only: one line per container.</summary>
        public string Report()
        {
            var sb = new System.Text.StringBuilder($"level 18 containers: bound {Bound}, hidden {Hidden}, hits {Hits}, breaks {Breaks}\n");
            foreach (var b in boxes) sb.Append($"  {b.id} sim #{b.simId} hp {(b.dead ? 0 : b.sim.Health)}/{b.sim.MaxHealth} {(b.dead ? "WRECK" : "standing")} centre {b.centre}\n");
            return sb.ToString();
        }
    }
}
