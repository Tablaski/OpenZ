using System.Collections.Generic;
using System.Text;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OpenZ
{
    /// <summary>
    /// dark visor glass, as the reference picture has them. View side only; the Sim is never touched; the v1 mech is never touched.
    /// <list type="bullet">
    /// <item><b>The picture</b>: TeamLit's own emission map path (the fort / shuttle windows use it): Resources/OpenZ/MechV2/mech_v2b_eyes
    /// (1024, the toy atlas's UV layout, black but the two eyes: a white-blue core with a soft round blue glow, only on the visor glass)
    /// is set per renderer on the v2's Mech_body slot that wears mech_v2b_toy, with its tint (_EmissionMapColor, linear HDR) and
    /// _EmissionNight 0 (lit by day too). The material itself keeps no emission (tint black): a wreck piece, a menu picture or any copy of
    /// the model that this script does not drive never glows.</item>
    /// <item><b>When they burn</b>: exactly with the mech's eye lamps (VehicleLights: the same lamp power, so a crew = lit, an empty mech =
    /// dark, boarding / capture = the engine-start flicker, a sniped driver = out, destroyed = out, the level 16 ending's hidden units =
    /// out). Bright enough to read at the RTS camera by day (tint <see cref="dayGain"/>, a little over the day bloom threshold, so the core
    /// blooms softly), a little lower at night (<see cref="nightGain"/>: the night bloom is wider and the eye lamps' own glows burn on top).</item>
    /// <item><b>A failing eye</b> (HeadlightFaults: below ~40 % health one eye lamp of a mech dies or flickers): the picture of that eye goes
    /// out with its lamp (mech_v2b_eyes_px / _nx = only the eye on the prefab's -x / +x side burns while the faulty lamp is below
    /// <see cref="faultCut"/>).</item>
    /// <item>The v2's eye lamps (vehicle_lights.json "medium@v2" eye_L / eye_R) sit <see cref="SocketDrop"/> lower than v1's, in the middle
    /// of the painted eyes, so the night glow and the eyes are one.</item>
    /// </list>
    /// Auto-boots in level scenes (a ZGame is present), like VehicleLights. Status(): the counted state (editor menu "Mech v2 / 26").
    /// </summary>
    [DefaultExecutionOrder(85)]   // after VehicleLights (80): its lamp power of this frame is set
    public sealed class OzMechV2Eyes : MonoBehaviour
    {
        public static OzMechV2Eyes I { get; private set; }
        /// <summary>Set false to add the component by hand.</summary>
        public static bool AutoBoot = true;

        /// <summary>The pictures (Resources/OpenZ/MechV2/): both eyes; only the eye on the prefab's +x side; only the one on its -x side.</summary>
        public const string Both = "mech_v2b_eyes", OnlyPx = "mech_v2b_eyes_px", OnlyNx = "mech_v2b_eyes_nx";
        /// <summary>The renderer and the material slot that carry the visor glass.</summary>
        public const string Surface = "Mech_body", ToyMaterial = "mech_v2b_toy";
        /// <summary>The v2's two eye lamps sit this much lower than v1's (MechWaistYaw units; x the node scale in the root frame).</summary>
        public const float SocketDrop = 0.071f;

        [Tooltip("Eye tint by day (linear, x the picture's white-blue core): the core a little over the day bloom threshold")]
        [Range(0f, 8f)] public float dayGain = 3.0f;
        [Tooltip("Eye tint at full night (the night bloom is wider and the eye lamps' glows burn on top)")]
        [Range(0f, 8f)] public float nightGain = 2.4f;
        [Tooltip("A faulty eye lamp below this level (0 out .. 1 burning) shows that eye dark")]
        [Range(0f, 1f)] public float faultCut = 0.5f;

        static readonly int MapId = Shader.PropertyToID("_EmissionMap"), TintId = Shader.PropertyToID("_EmissionMapColor"),
            NightOnlyId = Shader.PropertyToID("_EmissionNight");

        sealed class Eyes
        {
            public UnitView View; public Renderer R; public int Slot; public int Id;
            public float Power, Tint, Shown = -1f, Refresh; public Texture Map, ShownMap; public bool Alive, Crewed, OneEye;
        }

        readonly List<Eyes> eyes = new List<Eyes>();
        readonly HashSet<UnitView> seen = new HashSet<UnitView>();
        Texture2D both, onlyPx, onlyNx; MaterialPropertyBlock mpb; ZGame game; float nextScan;
        /// <summary>v2 mechs with eyes found / their eyes burning now / views of type medium found without the v2 visor slot.</summary>
        public int Count => eyes.Count;
        public int LitNow { get; private set; }
        public int NoSlot { get; private set; }

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
            if (!g || FindFirstObjectByType<OzMechV2Eyes>()) return;
            new GameObject("OzMechV2Eyes").AddComponent<OzMechV2Eyes>();
        }

        void Awake()
        {
            if (I && I != this) { Destroy(this); return; }
            I = this;
            mpb = new MaterialPropertyBlock();
            game = ZGame.I ? ZGame.I : FindFirstObjectByType<ZGame>();
            both = Resources.Load<Texture2D>(OzMech.Dir + Both);
            onlyPx = Resources.Load<Texture2D>(OzMech.Dir + OnlyPx);
            onlyNx = Resources.Load<Texture2D>(OzMech.Dir + OnlyNx);
            if (!game || !both) { Debug.LogWarning("[OpenZ] OzMechV2Eyes: " + (!game ? "no ZGame" : "Resources/" + OzMech.Dir + Both + " missing") + ": the mech v2 eyes stay dark"); enabled = false; }
        }

        void OnDestroy() { if (I == this) I = null; }

        // ------------------------------------------------------------------ the v2 mechs
        void Scan()
        {
            for (int i = eyes.Count - 1; i >= 0; i--) if (!eyes[i].View || !eyes[i].R) eyes.RemoveAt(i);
            seen.RemoveWhere(IsGone);                                    // views of units that are gone
            foreach (var kv in game.Views)
            {
                var v = kv.Value;
                if (!v || v.Obj == null || v.Obj.Type != OzMech.Type || seen.Contains(v)) continue;
                seen.Add(v);
                if (!OzV2Units.IsV2(v)) continue;
                var e = Find(v);
                if (e != null) eyes.Add(e); else NoSlot++;
            }
        }

        static readonly System.Predicate<UnitView> IsGone = v => !v;

        static Eyes Find(UnitView v)
        {
            foreach (var r in v.GetComponentsInChildren<Renderer>(true))
            {
                if (r.name != Surface) continue;
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                    if (mats[i] && mats[i].name.StartsWith(ToyMaterial) && mats[i].HasProperty(MapId) && mats[i].HasProperty(TintId))
                        return new Eyes { View = v, R = r, Slot = i, Id = v.Obj.Id };
            }
            return null;
        }

        void LateUpdate()
        {
            if (!game) return;
            float now = Time.time, dt = Time.deltaTime;
            if (now >= nextScan) { nextScan = now + 0.5f; Scan(); }
            float night = NightGrade.NightK, gain = Mathf.Lerp(dayGain, nightGain, night);
            var vl = VehicleLights.I;
            int lit = 0;
            for (int i = 0; i < eyes.Count; i++)
            {
                var e = eyes[i];
                if (!e.View || !e.R) continue;
                var o = e.View.Obj;
                e.Alive = e.View.isActiveAndEnabled && o != null && !o.Destroyed && !e.View.Dying;
                e.Crewed = e.Alive && CaptureHold.Shown(o) != Team.Neutral;
                var map = both; e.OneEye = false;
                var lamp = vl && o != null ? vl.Probe(o.Id) : default;
                if (lamp.Ok)
                {   // the eye lamps' own power (crew, engine-start flicker, death) and their fault
                    e.Power = lamp.Power;
                    if (lamp.Split && lamp.Plan.mode != HeadlightFaults.Mode.None && lamp.FaultK < faultCut)
                    {   // the faulty lamp is the eye on the side Plan.side (the sign of its root-frame x): the other eye burns alone
                        map = lamp.Plan.side > 0 ? onlyNx : onlyPx; e.OneEye = true;
                        if (!map) map = both;
                    }
                }
                else e.Power = Mathf.MoveTowards(e.Power, e.Crewed ? 1f : 0f, dt / (e.Crewed ? 0.2f : 0.4f));   // no lamp rig: the same rule, crew = lit
                if (!e.Alive || ZodBriefing.EndingHides(e.View)) e.Power = 0f;
                e.Tint = e.Power * gain;
                e.Map = map;
                if (e.Tint > 0.05f) lit++;
                if (e.Shown >= 0f && e.ShownMap == map && now < e.Refresh && Mathf.Abs(e.Tint - e.Shown) <= Mathf.Max(0.004f, e.Shown * 0.015f)) continue;
                e.Shown = e.Tint; e.ShownMap = map; e.Refresh = now + 1f;   // set again at least once a second (a block another script cleared)
                e.R.GetPropertyBlock(mpb, e.Slot);                       // keep TeamPaint's colours in the same block
                mpb.SetTexture(MapId, map);
                mpb.SetFloat(NightOnlyId, 0f);
                mpb.SetVector(TintId, new Vector4(e.Tint, e.Tint, e.Tint, 1f));   // linear
                e.R.SetPropertyBlock(mpb, e.Slot);
            }
            LitNow = lit;
        }

        // ------------------------------------------------------------------ checks
        /// <summary>One v2 mech's eyes now: found, burning, the tint (linear), one eye only (a faulty lamp).</summary>
        public bool Probe(int id, out float tint, out bool oneEye)
        {
            foreach (var e in eyes)
                if (e.Id == id && e.View) { tint = e.Tint; oneEye = e.OneEye; return true; }
            tint = 0f; oneEye = false; return false;
        }

        public string Status()
        {
            var sb = new StringBuilder();
            sb.Append($"[OpenZ] OzMechV2Eyes {(game ? game.level : "?")}: {eyes.Count} mech v2 with eyes, {LitNow} burning now, night {NightGrade.NightK:F2}, gain day {dayGain:F2} / night {nightGain:F2} (now {Mathf.Lerp(dayGain, nightGain, NightGrade.NightK):F2}), pictures {(both ? both.width + "x" + both.height : "MISSING")} / {(onlyPx ? "px" : "px MISSING")} / {(onlyNx ? "nx" : "nx MISSING")}{(NoSlot > 0 ? $", {NoSlot} v2 view(s) without the visor slot" : "")}");
            foreach (var e in eyes)
            {
                if (!e.View) continue;
                var o = e.View.Obj;
                string state = !e.Alive ? "WRECK / dying" : !e.Crewed ? "EMPTY" : "crewed " + CaptureHold.Shown(o);
                sb.Append($"\n  #{e.Id} {state}: lamp power {e.Power:F2}, eye tint {e.Tint:F2}{(e.OneEye ? " (ONE EYE: the faulty lamp is out)" : "")}, picture {(e.Map ? e.Map.name : "-")}, slot {e.R.name}[{e.Slot}]");
            }
            return sb.ToString();
        }
    }
}
