using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace OpenZ
{
    /// <summary>
    /// serves every big tank view it finds (Unit editor or a level), so no prefab changes.
    /// <list type="bullet">
    /// <item>
    /// Thick rolling clouds from both tracks (DustTrails, the ground's own colour and strength: sand full, dirt less, roads and concrete
    /// faint, none on water): behind each track's rear and beside the tracks, spaced by the distance driven, bigger and denser with the speed,
    /// x <see cref="DustScale"/> for the tank's size and weight; a ring of heavy puffs round both tracks when it sets off, more while it turns
    /// (the tracks scrub), more on sand. <see cref="DustScale"/>: Mammoth 1.2, Bizon 1.3 (their size against the heavy tank).</item>
    /// <item>EXHAUST: dark diesel smoke from the Bizon's two pipes and from the Mammoth's two rear deck stubs (code-built short pipes on pads,
    /// see the pads below): puffs while it stands crewed, more and greyer driving, a thick dark burst on setting off and on a hard speed-up
    /// (OzBigTankExhaust's own bursts and diesel chuff sound; the Mammoth gets the same component here). The smoke is the game's soft smoke
    /// channel (leg_smoke) drawn denser than the shuttle's wisps (the old Bizon wisps read too faint); none empty, neutral, dead or off screen.</item>
    /// <item>
    /// tail lamps which already burn): a thin team-colour NEON tube along each side skirt's lower rim (one straight run per flat panel, 6 mm
    /// proud of it, in a dark channel; soft bloom, grey and off when neutral); team-colour marker lamps blinking (two each); a turret SEARCHLIGHT
    /// at night (the fort searchlights' drum and yoke, beam cone, ground pool, a real light only inside the night budget) sweeping slowly
    /// round the turret's front; on the Bizon an amber rotating WORK BEACON (on while crewed). Every lamp fades with the vehicle colour fill
    /// (its team colour follows TeamPaint's fill), switches with the crew, and dies with the tank (off at once, a fizzle).</item>
    /// <item>PADS: every code-built part stands on a measured pad (kit_tools/pick_spots.py on the game GLBs: 13 roof probes, the pad's foot 2 cm
    /// under the lowest, its top 3 cm over the highest), so nothing floats or sinks. The parts are not children of the tank (TeamPaint, the
    /// burn and the wreck never touch them): three holders follow the tank's TankRoot, TankChassis and turret yaw node every frame, and the
    /// parts are shown only while the hull (or the turret) renderer they stand on is (the break-up hides them with the hull).</item>
    /// <item>
    /// (OzBigTankWreck.Prepare) <see cref="CanMin"/>-<see cref="CanMax"/> of the game's own rocket fuel cans (OzFuelCan: the red painted
    /// aluminium can with the grey metal top, a few crushed empties) are thrown out of the hull with physics, tumble, bounce, roll and lie with
    /// the wreck (its own time); <see cref="CanFireMin"/>-<see cref="CanFireMax"/> of them burst on landing with a small fire puff and the
    /// ignite sound.</item>
    /// </list>
    /// Switch: <see cref="On"/> (false before the level loads = 1.5 before this kit). Play check: View/OzBigTanksFxPlayCheck.cs.
    /// </summary>
    [DefaultExecutionOrder(32050)]   // LateUpdate after UnitView, UnitAim and the flips placed the tanks
    public sealed partial class OzBigTanksFx : MonoBehaviour
    {
        public static bool On = true;
        public static OzBigTanksFx I { get; private set; }

        // ------------------------------------------------------------------ numbers
        /// <summary>Dust x this for the tank's size and weight (the heavy tank = 1).</summary>
        public static float DustScale(string type) => type == OzBigTanks.Bizon ? 1.3f : 1.2f;
        /// <summary>Metres driven between two dust volleys (slow .. full speed).</summary>
        public static float DustEverySlow = 0.75f, DustEveryFast = 0.38f;
        /// <summary>Peak opacity of a big tank's dust puff (DustTrails' wake is 0.34) and its size (m across at t .28).</summary>
        public static float DustAlpha = 0.5f, DustSize = 1.55f, DustLife = 2.4f;
        /// <summary>Exhaust: puffs per second standing / at full drive, alpha, size (m), life (s), the burst's darkness.</summary>
        public static float SmokeRateIdle = 2.2f, SmokeRateDrive = 7f, SmokeAlpha = 0.42f, SmokeSize = 0.62f, SmokeLife = 2.8f;
        public static readonly Color SmokeIdle = new Color(0.62f, 0.62f, 0.62f), SmokeDrive = new Color(0.42f, 0.41f, 0.40f), SmokeBurst = new Color(0.12f, 0.115f, 0.11f);
        /// <summary>The neon tube: height x depth (m) and its glow (HDR multiples of the team glow colour) by day / at night.</summary>
        public static float NeonH = 0.026f, NeonD = 0.014f, NeonDay = 2.2f, NeonNight = 6.5f;
        /// <summary>Searchlight: head scale (the fort's 0.64 m lens x this), cone angle, reach, real-light strength, budget rank, sweep (deg each side), period (s).</summary>
        public static float SearchScale = 0.42f, SearchAngle = 24f, SearchRange = 15f, SearchIntensity = 34f, SearchPriority = 2.4f, SearchSweep = 38f, SearchPeriod = 9f, SearchPitch = 14f;
        /// <summary>Lamps on at dusk, off at dawn (the day clock, as the street lamps and the vehicle headlights).</summary>
        public static float LampsOnAt = 0.34f, LampsOffAt = 0.895f;
        /// <summary>The Bizon's death: fuel cans thrown, of them burst into a fire puff.</summary>
        public const int CanMin = 10, CanMax = 16, CanFireMin = 2, CanFireMax = 4;
        public const string NeonOnEvent = "ngt_lamp_flicker", LampOffEvent = "ngt_light_fizzle", SearchOnEvent = "ngt_searchlight_on", CanFireEvent = "dst_fire_ignite";

        // ------------------------------------------------------------------ proof counters (the Play check reads them)
        public static int Attached, DustVolleys, DustPuffs, SmokePuffs, SmokeBursts, NeonSwitchOns, SearchSwitchOns, BeaconSwitchOns, LampDeaths, PartsBuilt, PadsBuilt,
                          CanDeaths, CansThrown, CanFires, Breaks;
        public static string Last = "";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Attached = DustVolleys = DustPuffs = SmokePuffs = SmokeBursts = NeonSwitchOns = SearchSwitchOns = BeaconSwitchOns = LampDeaths = PartsBuilt = PadsBuilt = CanDeaths = CansThrown = CanFires = Breaks = 0; Last = ""; I = null; }

        readonly Dictionary<int, Tank> tanks = new Dictionary<int, Tank>();
        readonly List<int> gone = new List<int>();
        float nextScan;
        MaterialPropertyBlock mpb;

        // ------------------------------------------------------------------ boot (the FactoryViews pattern)
        static bool hooked;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (!hooked) { hooked = true; SceneManager.sceneLoaded += (s, m) => TryAttach(); }
            TryAttach();
        }

        static void TryAttach()
        {
            if (!On || !ZGame.I || FindFirstObjectByType<OzBigTanksFx>()) return;
            new GameObject("OZ_BigTanksFx").AddComponent<OzBigTanksFx>();
        }

        void Awake() { I = this; mpb = new MaterialPropertyBlock(); }
        void OnDestroy() { if (I == this) I = null; foreach (var t in tanks.Values) t.Dispose(); tanks.Clear(); }

        /// <summary>The fx record of a big tank's view (the Play check), or null.</summary>
        public Tank Of(int id) => tanks.TryGetValue(id, out var t) ? t : null;

        void Update()
        {
            var g = ZGame.I;
            if (!On || !g || g.World == null) return;
            if (Time.unscaledTime >= nextScan)
            {
                nextScan = Time.unscaledTime + 0.25f;
                foreach (var kv in g.Views)
                {
                    var v = kv.Value;
                    if (!v || v.Obj == null || !OzBigTanks.Is(v.Obj.Type) || tanks.ContainsKey(kv.Key)) continue;
                    var t = Tank.Make(this, v);
                    if (t != null) { tanks[kv.Key] = t; Attached++; }
                }
                gone.Clear();
                foreach (var kv in tanks) if (!kv.Value.view || kv.Value.view.Obj == null) gone.Add(kv.Key);
                foreach (var id in gone) { tanks[id].Dispose(); tanks.Remove(id); }
            }
        }

        void LateUpdate()
        {
            var g = ZGame.I;
            if (!On || !g || g.World == null) return;
            float dt = g.paused ? 0f : Time.deltaTime;
            foreach (var t in tanks.Values) t.Step(dt);
        }

        // ------------------------------------------------------------------ the break-up hook (OzBigTankWreck.Prepare)
        /// <summary>UnitView.BreakApart through OzBigTankWreck.Prepare: the hull opens (the Bizon's fuel cans fly).</summary>
        public static void OnBreak(UnitView v)
        {
            if (!On || !v || v.Obj == null) return;
            Breaks++;
            var fx = I; var t = fx ? fx.Of(v.Obj.Id) : null;
            if (t != null) t.Broken();
            if (v.Obj.Type == OzBigTanks.Bizon) ThrowCans(v);
        }

        static Material Lit(string name, Color c, float smooth, float metal, Color emission)
        {
            var sh = Shader.Find("OpenZ/TeamLit");                                                   // the game's own lit shader (NightMeshes' fixtures do the same)
            if (!sh) sh = Shader.Find("Universal Render Pipeline/Lit");
            var m = new Material(sh) { name = name, hideFlags = HideFlags.DontSave, enableInstancing = true };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_TeamMode")) m.SetFloat("_TeamMode", 0f);
            if (m.HasProperty("_Degreen")) m.SetFloat("_Degreen", 0f);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metal);
            if (emission.maxColorComponent > 0f && m.HasProperty("_EmissionColor")) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", emission); }
            return m;
        }
        static Material soot, amberDome, ledGlass;
        internal static Material Soot => soot ? soot : soot = Lit("OZ_BigTank_ExhaustSoot", new Color(0.14f, 0.13f, 0.12f), 0.2f, 0.4f, Color.black);
        internal static Material AmberDome => amberDome ? amberDome : amberDome = Lit("OZ_BigTank_AmberDome", new Color(1f, 0.55f, 0.08f), 0.85f, 0f, new Color(0.35f, 0.16f, 0.02f));
        internal static Material LedGlass => ledGlass ? ledGlass : ledGlass = Lit("OZ_BigTank_LedGlass", new Color(0.16f, 0.16f, 0.17f), 0.9f, 0f, Color.black);

        /// <summary>A short closed pipe along +y (radius, height), 16 sides, its top capped (the exhaust stubs, the lamp feet).</summary>
        static Mesh pipe;
        internal static Mesh Pipe()
        {
            if (pipe) return pipe;
            const int S = 16; var v = new List<Vector3>(); var n = new List<Vector3>(); var tr = new List<int>();
            for (int i = 0; i <= S; i++)
            {
                float a = i * Mathf.PI * 2f / S; var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                v.Add(d * 0.5f); n.Add(d); v.Add(d * 0.5f + Vector3.up); n.Add(d);
            }
            for (int i = 0; i < S; i++) { int a = i * 2; tr.AddRange(new[] { a, a + 1, a + 3, a, a + 3, a + 2 }); }
            int c = v.Count; v.Add(Vector3.up); n.Add(Vector3.up);
            for (int i = 0; i <= S; i++) { float a = i * Mathf.PI * 2f / S; v.Add(new Vector3(Mathf.Cos(a) * 0.5f, 1f, Mathf.Sin(a) * 0.5f)); n.Add(Vector3.up); }
            for (int i = 0; i < S; i++) tr.AddRange(new[] { c, c + 2 + i, c + 1 + i });
            pipe = new Mesh { name = "OZ_BigTank_Pipe" }; pipe.SetVertices(v); pipe.SetNormals(n); pipe.SetTriangles(tr, 0); pipe.RecalculateBounds();
            return pipe;
        }

        internal void Glow(Renderer r, Color c, ref float shown)
        {
            if (!r) return;
            float key = c.r + c.g * 1.7f + c.b * 0.6f;
            if (shown >= 0f && (key > 0.0015f) == r.enabled && Mathf.Abs(key - shown) <= Mathf.Max(0.003f, shown * 0.01f)) return;   // an on/off change always goes through (a fade could stop just above 0 and leave the glow lit)
            shown = key;
            bool on = key > 0.0015f;
            if (r.enabled != on) r.enabled = on;
            if (!on) return;
            mpb.Clear(); mpb.SetColor(NightMeshes.ColorId, c); r.SetPropertyBlock(mpb);
        }

        internal static bool Night(out float nightK)
        {
            nightK = NightGrade.NightK;
            return NightGrade.LightsOn(NightGrade.TimeOfDay, LampsOnAt, LampsOffAt);
        }

        public static string Status()
        {
            return $"big tanks fx {(On ? "on" : "off")}: tanks {Attached}, pads {PadsBuilt}, parts {PartsBuilt}; dust volleys {DustVolleys} ({DustPuffs} puffs), smoke puffs {SmokePuffs} "
                   + $"(bursts {SmokeBursts}); switch-ons: neon {NeonSwitchOns}, searchlight {SearchSwitchOns}, beacon {BeaconSwitchOns}; lamp deaths {LampDeaths}; breaks {Breaks}; "
                   + $"Bizon cans: deaths {CanDeaths}, thrown {CansThrown}, fire puffs {CanFires}; ram clear: pre {UnitAim.PreClearFrames} / ram {UnitAim.RamClearFrames} frames, "
                   + $"heaves held {OzRamBarrels.FlipWaits} (longest {OzRamBarrels.FlipWaitMax:0.00} s); last: {(Last.Length > 0 ? Last : "-")}";
        }
    }
}
