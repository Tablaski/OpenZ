using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace OpenZ
{
    /// <summary>
    /// only; the Sim is never touched; the v1 mech is never touched.
    /// <list type="bullet">
    /// <item><b>Why</b>: the eye picture (OzMechV2Eyes, TeamLit's emission map) is a few centimetres wide; at the RTS camera it covers one or
    /// two pixels and the texture's small mips mix it with the dark glass, so by day it never rose over the bloom threshold (linear 1.63).</item>
    /// <item><b>What</b>: per eye a small white-blue hot core and a soft blue halo, the game's own lamp glow path (OpenZ/VehicleGlow through
    /// VehicleLightMeshes: one additive draw per mech, billboards pulled a little towards the camera, soft depth fade, facing the eye's normal
    /// so it shows from the front only). The core is bright enough to bloom by day; at night (where the night bloom and the eye lamps' own
    /// glows already burn) it is much lower, so the eyes never blind.</item>
    /// <item><b>When</b>: exactly with the eyes (OzMechV2Eyes.Probe: the eye lamps' power): a crew = lit, empty / sniped driver / wreck /
    /// dying / the level 16 ending = dark, the engine-start flicker on boarding. A failing eye lamp (HeadlightFaults) turns that eye's glow
    /// out together with its picture. Nothing is drawn for a mech whose body is hidden or off screen.</item>
    /// </list>
    /// The two eye sockets come from vehicle_lights.json ("medium@v2" eye_L / eye_R, the mount node MechWaistYaw), the same points the
    /// eye lamps use. Auto-boots in level scenes (a ZGame is present). Status(): the counted state (editor menu "Mech v2 / 27").
    /// </summary>
    [DefaultExecutionOrder(86)]   // after OzMechV2Eyes (85): this frame's eye tint is set
    public sealed class OzMechV2EyeGlow : MonoBehaviour
    {
        public static OzMechV2EyeGlow I { get; private set; }
        /// <summary>Set false to add the component by hand.</summary>
        public static bool AutoBoot = true;

        /// <summary>The glow's shape (metres in the game world; fixed: the mesh is built once and shared by every v2 mech).</summary>
        public const float CoreRadius = 0.085f, HaloRadius = 0.40f, Push = 0.03f;
        static readonly Color CoreRgb = new Color(0.80f, 0.93f, 1.0f), HaloRgb = new Color(0.38f, 0.70f, 1.0f);

        [Tooltip("Hot core by day (linear channel; x the core colour, x 1.6 at its centre): over the day bloom threshold, so it blooms")]
        [Range(0f, 8f)] public float dayCore = 3.4f;
        [Tooltip("Soft halo by day (linear channel; x the halo colour)")]
        [Range(0f, 3f)] public float dayHalo = 0.95f;
        [Tooltip("Hot core at full night (the night bloom is wider and the eye lamps' glows burn on top)")]
        [Range(0f, 8f)] public float nightCore = 0.6f;
        [Tooltip("Soft halo at full night")]
        [Range(0f, 3f)] public float nightHalo = 0.2f;
        [Tooltip("Halo falloff (OpenZ/VehicleGlow _Falloff: lower = softer, wider)")]
        [Range(0.5f, 12f)] public float haloFalloff = 2.6f;
        [Range(0f, 3f)] public float glowGain = 1f;

        static readonly int ChAId = Shader.PropertyToID("_ChA"), ChBId = Shader.PropertyToID("_ChB"), FalloffId = Shader.PropertyToID("_Falloff"),
            DepthFadeId = Shader.PropertyToID("_DepthFade");

        sealed class Glow
        {
            public UnitView View; public Renderer Body; public Transform Mount; public Mesh Mesh; public int Id;
            public HeadlightFaults.Plan Plan; public MaterialPropertyBlock Block; public Vector4 Shown = new Vector4(-1f, 0f, 0f, 0f), Ch;
            public bool OneEye, Drawn;
        }

        readonly List<Glow> glows = new List<Glow>();
        readonly HashSet<UnitView> seen = new HashSet<UnitView>();
        ZGame game; Material mat; VLVehicle spec; float nextScan; float shownFalloff = -1f;
        /// <summary>v2 mechs with a glow / drawn this frame / v2 views without the mount node or the body.</summary>
        public int Count => glows.Count;
        public int DrawnNow { get; private set; }
        public int Missing { get; private set; }

        // ------------------------------------------------------------------ boot
        static bool hooked;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Early() { if (!hooked) { hooked = true; SceneManager.sceneLoaded += (s, m) => TryAttach(); } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() => TryAttach();

        /// <summary>Level scenes only (a ZGame is present).</summary>
        public static void TryAttach()
        {
            if (!AutoBoot) return;
            var g = ZGame.I ? ZGame.I : FindFirstObjectByType<ZGame>();
            if (!g || FindFirstObjectByType<OzMechV2EyeGlow>()) return;
            new GameObject("OzMechV2EyeGlow").AddComponent<OzMechV2EyeGlow>();
        }

        void Awake()
        {
            if (I && I != this) { Destroy(this); return; }
            I = this;
            game = ZGame.I ? ZGame.I : FindFirstObjectByType<ZGame>();
            var ta = Resources.Load<TextAsset>("OpenZ/VehicleLights/vehicle_lights");
            var file = ta ? JsonUtility.FromJson<VLFile>(ta.text) : null;
            if (file?.vehicles != null) foreach (var v in file.vehicles) if (v != null && v.type == OzMech.Models.LightsKey) spec = v;
            if (!game || spec?.sockets == null) { Debug.LogWarning("[OpenZ] OzMechV2EyeGlow: " + (!game ? "no ZGame" : "no '" + OzMech.Models.LightsKey + "' entry in vehicle_lights.json") + ": no eye glow"); enabled = false; return; }
            mat = VehicleLightMeshes.GlowMaterial("VehicleGlow (mech v2 eye glow)");
            mat.SetFloat(DepthFadeId, 0.2f);
        }

        void OnDestroy()
        {
            if (I == this) I = null;
            if (mat) Destroy(mat);
        }

        // ------------------------------------------------------------------ the v2 mechs
        void Scan()
        {
            for (int i = glows.Count - 1; i >= 0; i--) if (!glows[i].View || !glows[i].Mount || !glows[i].Body) glows.RemoveAt(i);
            seen.RemoveWhere(IsGone);
            foreach (var kv in game.Views)
            {
                var v = kv.Value;
                if (!v || v.Obj == null || v.Obj.Type != OzMech.Type || seen.Contains(v)) continue;
                seen.Add(v);
                if (!OzV2Units.IsV2(v)) continue;
                var g = Build(v);
                if (g != null) glows.Add(g); else Missing++;
            }
        }

        static readonly System.Predicate<UnitView> IsGone = v => !v;

        static Vector3 V3(float[] a, Vector3 fallback) => a != null && a.Length >= 3 ? new Vector3(a[0], a[1], a[2]) : fallback;

        Glow Build(UnitView v)
        {
            Renderer body = null; Transform mount = null; string node = null;
            foreach (var s in spec.sockets) if (s != null && s.kind == "eye" && !string.IsNullOrEmpty(s.node)) { node = s.node; break; }
            foreach (var t in v.GetComponentsInChildren<Transform>(true))
            {
                if (!mount && node != null && t.name == node) mount = t;
                if (!body && t.name == OzMechV2Eyes.Surface && t.TryGetComponent<Renderer>(out var r)) body = r;
            }
            if (!body) return null;
            bool nodeFrame = mount;
            if (!mount) mount = v.transform;                                     // the node is missing: the root frame (the sockets' root coordinates)
            var elems = new List<VehicleLightMeshes.Elem>();
            foreach (var s in spec.sockets)
            {
                if (s == null || s.kind != "eye") continue;
                var pos = nodeFrame ? V3(s.pos, Vector3.zero) : V3(s.root, Vector3.zero);
                var nrm = nodeFrame ? V3(s.nrm, Vector3.forward) : V3(s.rootNrm, Vector3.forward);
                nrm = nrm.sqrMagnitude > 1e-6f ? nrm.normalized : Vector3.forward;
                float unit = nodeFrame ? 1f / Mathf.Max(0.05f, s.nodeScale) : 1f;
                int ch = V3(s.root, Vector3.zero).x > 0f ? 0 : 2;                // the eye on the prefab's +x side: channels 0 / 1, the other 2 / 3
                var c = pos + nrm * (Push * unit);
                elems.Add(new VehicleLightMeshes.Elem { mode = 2, pos = c, nrm = nrm, size = CoreRadius * unit, rgb = CoreRgb, ch = ch });
                elems.Add(new VehicleLightMeshes.Elem { mode = 0, pos = c, nrm = nrm, size = HaloRadius * unit, rgb = HaloRgb, ch = ch + 1 });
            }
            if (elems.Count == 0) return null;
            var g = new Glow
            {
                View = v, Body = body, Mount = mount, Id = v.Obj.Id, Block = new MaterialPropertyBlock(),
                Mesh = VehicleLightMeshes.GlowMesh("OzMechV2EyeGlow|" + (nodeFrame ? "node" : "root"), elems),
                Plan = HeadlightFaults.Enabled ? HeadlightFaults.For(v.Obj.Id) : default,
            };
            g.Block.SetVector(ChBId, Vector4.zero);
            return g;
        }

        void LateUpdate()
        {
            if (!game) return;
            float now = Time.time;
            if (now >= nextScan) { nextScan = now + 0.5f; Scan(); }
            if (!Mathf.Approximately(shownFalloff, haloFalloff)) { shownFalloff = haloFalloff; mat.SetFloat(FalloffId, haloFalloff); }
            var eyes = OzMechV2Eyes.I;
            int drawn = 0;
            if (eyes && eyes.isActiveAndEnabled)
            {
                float night = NightGrade.NightK, gain = Mathf.Lerp(eyes.dayGain, eyes.nightGain, night);
                float core = Mathf.Lerp(dayCore, nightCore, night) * glowGain, halo = Mathf.Lerp(dayHalo, nightHalo, night) * glowGain;
                for (int i = 0; i < glows.Count; i++)
                {
                    var g = glows[i]; g.Drawn = false;
                    if (!g.View || !g.Mount || !g.Body) continue;
                    if (!eyes.Probe(g.Id, out float tint, out bool oneEye)) { g.Ch = Vector4.zero; continue; }
                    float p = gain > 1e-3f ? Mathf.Clamp01(tint / gain) : 0f;   // the eye lamps' power (0 empty / wreck / ending)
                    g.OneEye = oneEye;
                    // a failing eye lamp (HeadlightFaults side = the sign of its root x): that eye's glow goes out with its picture
                    float kp = oneEye && g.Plan.side > 0 ? 0f : 1f, kn = oneEye && g.Plan.side < 0 ? 0f : 1f;
                    g.Ch = new Vector4(core * p * kp, halo * p * kp, core * p * kn, halo * p * kn);
                    if (p < 0.01f || !g.Body.enabled || !g.Body.isVisible) continue;   // dark, hidden or off screen: nothing drawn
                    if (Changed(g.Ch, g.Shown)) { g.Shown = g.Ch; g.Block.SetVector(ChAId, g.Ch); }
                    Graphics.DrawMesh(g.Mesh, g.Mount.localToWorldMatrix, mat, 0, null, 0, g.Block, ShadowCastingMode.Off, false, null, LightProbeUsage.Off);
                    g.Drawn = true; drawn++;
                }
            }
            DrawnNow = drawn;
        }

        static bool Changed(Vector4 a, Vector4 b)
        {
            var d = a - b;
            return Mathf.Abs(d.x) + Mathf.Abs(d.y) + Mathf.Abs(d.z) + Mathf.Abs(d.w) > 0.005f + 0.01f * (Mathf.Abs(a.x) + Mathf.Abs(a.y) + Mathf.Abs(a.z) + Mathf.Abs(a.w));
        }

        // ------------------------------------------------------------------ checks
        /// <summary>One v2 mech's glow now: found, drawn this frame, its four channels (core / halo of the +x eye, core / halo of the other).</summary>
        public bool Probe(int id, out bool drawn, out Vector4 channels)
        {
            foreach (var g in glows)
                if (g.Id == id && g.View) { drawn = g.Drawn; channels = g.Ch; return true; }
            drawn = false; channels = Vector4.zero; return false;
        }

        public string Status()
        {
            var sb = new StringBuilder();
            float n = NightGrade.NightK;
            sb.Append($"[OpenZ] OzMechV2EyeGlow {(game ? game.level : "?")}: {glows.Count} mech v2 with the eye glow, {DrawnNow} drawn now, night {n:F2}, core {Mathf.Lerp(dayCore, nightCore, n) * glowGain:F2} " +
                      $"(day {dayCore:F2} / night {nightCore:F2}), halo {Mathf.Lerp(dayHalo, nightHalo, n) * glowGain:F2} (day {dayHalo:F2} / night {nightHalo:F2}), radii core {CoreRadius:F3} m / halo {HaloRadius:F2} m" +
                      $"{(Missing > 0 ? $", {Missing} v2 view(s) without the body" : "")}{(OzMechV2Eyes.I ? "" : ", NO OzMechV2Eyes (nothing drawn)")}");
            foreach (var g in glows)
            {
                if (!g.View) continue;
                sb.Append($"\n  #{g.Id}: {(g.Drawn ? "DRAWN" : "not drawn")}, eye +x core {g.Ch.x:F2} halo {g.Ch.y:F2}, eye -x core {g.Ch.z:F2} halo {g.Ch.w:F2}{(g.OneEye ? $" (ONE EYE: the lamp on the {(g.Plan.side > 0 ? "+x" : "-x")} side is out)" : "")}, " +
                          $"body {(g.Body && g.Body.isVisible ? "on screen" : "hidden / off screen")}, mount {(g.Mount ? g.Mount.name : "-")}");
            }
            return sb.ToString();
        }
    }
}
