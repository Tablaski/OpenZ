using UnityEngine;
using UnityEngine.SceneManagement;

namespace OpenZ
{
    /// <summary>
    /// 1.5 kit ue_dust_night (the last Unity pass: the Unit editor has "no day/night clock", so the big tanks' night lamps, neon, searchlights and
    /// beacons never show where the owner looks at the new tanks): the DAY / NIGHT switch in the Unit editor's BACKGROUND popover
    /// (UI/OzUeBackdropMenu.cs). View only: no rule, nothing in any level changes. Default DAY at every visit (not remembered).
    /// <list type="bullet">
    /// <item><b>The clock</b>: NIGHT eases over <see cref="EaseSeconds"/> (smoothstep, real seconds: it also switches while the playground is
    /// paused). The day clock fed to NightGrade.Apply goes DayTime (0.06) -> 0.40 .. 0.52 (NightGrade.Night's ramp, so NightGrade.NightK rises
    /// smoothly with the ease) -> 0.62 (DayNight's moonlit night) when the ease ends; back the same way. So every night system of the levels
    /// switches by its own rules: NightGrade.LightsOn (lamps, neon, searchlights, beacons: on from 0.34, off at 0.895), NightK (the glows, the
    /// halos, the beams, the night grade volume: exposure, contrast, cool white balance, bloom threshold, vignette; TeamLit's moonlit rim and
    /// fill on units).</item>
    /// <item><b>The light</b>: the sun's colour and strength and the trilight ambient blend from the playground's own day (as the scene has
    /// them) to DayNight's moonlit night keys (DayNight.Apply at 0.62 with the playground's city warmth, read once on a probe light, nothing
    /// of the scene touched), by NightK. The sun keeps its direction (no shadow sweep across the field in one second).</item>
    /// <item><b>The floor</b> (Resources/OpenZ/UnitEditor/OzUeFloor.shader, its colour is drawn in display colour and undoes the post chain):
    /// globals _OZUeNight (rgb = the floor's night multiply, a = NightK) and _OZUeNightPool (x = how much the lamps' real lights light the
    /// floor at night, as the levels' ground). Light floors (Desert dunes, Snow field) go to a cool moonlit tone, the dark studio floors dim a
    /// little (their lines stay readable). The camera's clear colour follows (OzUeFloor sets it to the floor colour).</item>
    /// <item><b>Leaving</b>: the next scene that loads gets the day grade back (NightGrade.Apply at DayTime), as OzUnitEditor.Start does for the
    /// playground.</item>
    /// </list>
    /// </summary>
    [DefaultExecutionOrder(-30)]
    public sealed class OzUeNight : MonoBehaviour
    {
        public const float EaseSeconds = 1f;
        /// <summary>DayNight's moonlit night (its keys 0.56 - 0.80).</summary>
        public const float NightTime = 0.62f;
        /// <summary>The playground's planet warmth (Editor/OzUePlaygroundMenu.cs CityWarmth: the scene's sun was built with it).</summary>
        public const float PlaygroundWarmth = 0.35f;
        /// <summary>The floor's night multiply (linear): light floors, dark studio floors.</summary>
        public static Color LightFloorNight = new Color(0.24f, 0.27f, 0.36f), DarkFloorNight = new Color(0.55f, 0.58f, 0.68f);
        /// <summary>The lamps' real lights on the floor at night (x the floor colour, as on the levels' ground).</summary>
        public static float PoolGain = 1f;
        public static readonly string[] Words = { "DAY", "NIGHT" };
        public const float SegW = 84f, SegH = 22f;
        public static readonly float[] SegCells = { 41f, 41f };

        public static OzUeNight I { get; private set; }
        /// <summary>NIGHT chosen (the ease may still run).</summary>
        public static bool Night { get; private set; }
        /// <summary>0 day .. 1 night, the eased share (smoothstep of the linear ramp).</summary>
        public static float Share { get; private set; }
        /// <summary>Counters for the Play check.</summary>
        public static int Switches, Frames;
        public static string Last = "";

        static readonly int IdNight = Shader.PropertyToID("_OZUeNight"), IdPool = Shader.PropertyToID("_OZUeNightPool");

        float ramp;                                                     // 0 .. 1 linear
        bool haveDay, haveNight, applied;
        Color daySun, nightSun, dayAmbSky, dayAmbEq, dayAmbGround, nightAmbSky, nightAmbEq, nightAmbGround;
        float dayInt, nightInt;
        Light sun; Camera cam;
        int clears = -1, kind = -1;

        /// <summary>UI/OzUeBackdropMenu.cs Build: the switch's driver on the editor's root (one per visit, DAY).</summary>
        public static void Ensure(OzUnitEditor ed)
        {
            if (!ed || ed.GetComponent<OzUeNight>()) return;
            ed.gameObject.AddComponent<OzUeNight>();
        }

        /// <summary>The DAY / NIGHT switch.</summary>
        public static void Set(bool night)
        {
            if (Night == night) return;
            Night = night; Switches++;
            Last = night ? "NIGHT" : "DAY";
            Debug.Log($"{OzUnitEditor.Tag} {(night ? "NIGHT: lamps, neon, searchlights and beacons on (eased " : "DAY (eased ")}{EaseSeconds:0.0} s; ue_dust_night)");
        }

        void Awake()
        {
            if (I && I != this) { Destroy(this); return; }
            I = this; Night = false; Share = 0f; ramp = 0f;
            Shader.SetGlobalVector(IdNight, Vector4.zero); Shader.SetGlobalVector(IdPool, Vector4.zero);
            if (!hooked) { hooked = true; SceneManager.sceneLoaded += AfterLeave; }
        }

        void OnDestroy()
        {
            if (I != this) return;
            I = null;
            leftNight = applied && Share > 0f;                            // the next scene gets the day grade (AfterLeave)
            Night = false; Share = 0f;
            Shader.SetGlobalVector(IdNight, Vector4.zero); Shader.SetGlobalVector(IdPool, Vector4.zero);
        }

        static bool hooked, leftNight;
        static void AfterLeave(Scene s, LoadSceneMode m)
        {
            if (!leftNight || OzUnitEditor.IsPlayground(s)) return;
            leftNight = false;
            NightGrade.Apply(RenderSettings.sun, OzUnitEditor.DayTime);   // a level's DayNight sets its own time again in its next Update
        }

        void Update()
        {
            Floors();
            float want = Night ? 1f : 0f;
            if (ramp == want && !(applied && ramp == 0f)) { if (ramp > 0f) Apply(); return; }
            ramp = Mathf.MoveTowards(ramp, want, Time.unscaledDeltaTime / Mathf.Max(0.05f, EaseSeconds));
            Share = Mathf.SmoothStep(0f, 1f, ramp);
            Apply();
        }

        void LateUpdate()
        {   // after OzUeFloor (it sets the clear colour to the floor's on a style change)
            if (!applied || !cam) return;
            var b = OzUeBackdrop.Current.floor; var day = new Color(b.r / 255f, b.g / 255f, b.b / 255f, 1f);
            var mul = (OzUeBackdrop.LightFloor ? LightFloorNight : DarkFloorNight).gamma;
            cam.backgroundColor = Color.Lerp(day, new Color(day.r * mul.r, day.g * mul.g, day.b * mul.b, 1f), NightGrade.NightK);
        }

        /// <summary>The dust's floor bookkeeping (View/OzUeDust.cs): another kind of floor or CLEAR FIELD wipes the prints and the dust.</summary>
        void Floors()
        {
            int k = OzUeDust.KindNow, c = OzUePlayground.Cleared;
            if (kind >= 0 && k != kind) OzUeDust.Wipe("floor " + OzUeBackdrop.Names[OzUeBackdrop.Style]);
            else if (clears >= 0 && c != clears) OzUeDust.Wipe("CLEAR FIELD");
            kind = k; clears = c;
        }

        void Apply()
        {
            if (!sun) sun = RenderSettings.sun;
            if (!cam) cam = Camera.main;
            if (!haveDay)
            {   // the playground's own day, as the scene has it (built by DayNight.Apply at DayTime, Editor/OzUePlaygroundMenu.cs)
                haveDay = true;
                if (sun) { daySun = sun.color; dayInt = sun.intensity; }
                dayAmbSky = RenderSettings.ambientSkyColor; dayAmbEq = RenderSettings.ambientEquatorColor; dayAmbGround = RenderSettings.ambientGroundColor;
            }
            if (!haveNight) ReadNight();
            float t = ramp <= 0f ? OzUnitEditor.DayTime : ramp >= 1f ? NightTime : Mathf.Lerp(0.40f, 0.52f, Share);
            float n = NightGrade.Night(t);
            if (sun) { sun.color = Color.Lerp(daySun, nightSun, n); sun.intensity = Mathf.Lerp(dayInt, nightInt, n); }
            RenderSettings.ambientSkyColor = Color.Lerp(dayAmbSky, nightAmbSky, n);
            RenderSettings.ambientEquatorColor = Color.Lerp(dayAmbEq, nightAmbEq, n);
            RenderSettings.ambientGroundColor = Color.Lerp(dayAmbGround, nightAmbGround, n);
            NightGrade.Apply(sun, t);                                       // NightK, TimeOfDay, the shader globals, the moon's shadows, the night volume
            var mul = OzUeBackdrop.LightFloor ? LightFloorNight : DarkFloorNight;
            Shader.SetGlobalVector(IdNight, new Vector4(mul.r, mul.g, mul.b, NightGrade.NightK));
            Shader.SetGlobalVector(IdPool, new Vector4(PoolGain, 0f, 0f, 0f));
            applied = ramp > 0f; Frames++;
            if (ramp <= 0f && cam) { var b = OzUeBackdrop.Current.floor; cam.backgroundColor = new Color(b.r / 255f, b.g / 255f, b.b / 255f, 1f); }
        }

        /// <summary>DayNight's moonlit night for the playground, read once on a probe: an inactive object (no Awake, no OnEnable, not found by
        /// any FindFirstObjectByType) with its own light; the scene's ambient and clear colour are put back at once (the same frame).</summary>
        void ReadNight()
        {
            haveNight = true;
            Color sky = RenderSettings.ambientSkyColor, eq = RenderSettings.ambientEquatorColor, gr = RenderSettings.ambientGroundColor, fog = RenderSettings.fogColor;
            var amb = RenderSettings.ambientMode; var bg = cam ? cam.backgroundColor : Color.black;
            var go = new GameObject("OZ_UeNightProbe"); go.SetActive(false); go.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                var l = go.AddComponent<Light>(); l.type = LightType.Directional;
                var dn = go.AddComponent<DayNight>(); dn.sun = l; dn.warmth = PlaygroundWarmth; dn.running = false;
                dn.Apply(NightTime);
                nightSun = l.color; nightInt = l.intensity;
                nightAmbSky = RenderSettings.ambientSkyColor; nightAmbEq = RenderSettings.ambientEquatorColor; nightAmbGround = RenderSettings.ambientGroundColor;
            }
            catch (System.Exception e)
            {   // the keys as DayNight had them on 9 Nov 2026 (its 0.56 key, city warmth 0.35)
                Debug.LogWarning($"{OzUnitEditor.Tag} night look probe failed ({e.Message}): DayNight's night key used as written");
                nightSun = Cool(new Color(0.68f, 0.76f, 0.94f)); nightInt = 0.42f;
                nightAmbSky = Cool(new Color(0.13f, 0.16f, 0.26f)); nightAmbEq = Cool(new Color(0.12f, 0.13f, 0.19f)); nightAmbGround = Cool(new Color(0.06f, 0.06f, 0.08f));
            }
            finally
            {
                RenderSettings.ambientSkyColor = sky; RenderSettings.ambientEquatorColor = eq; RenderSettings.ambientGroundColor = gr;
                RenderSettings.fogColor = fog; RenderSettings.ambientMode = amb;
                if (cam) cam.backgroundColor = bg;
                Destroy(go);
            }
            Debug.Log($"{OzUnitEditor.Tag} night look (DayNight {NightTime:0.00}, warmth {PlaygroundWarmth:0.00}): moon {nightSun} x {nightInt:0.00}, ambient sky {nightAmbSky}; day sun {daySun} x {dayInt:0.00}");
        }

        static Color Cool(Color c)
        {
            float g = c.grayscale; var cool = new Color(g * 0.96f, g * 1.0f, g * 1.06f);
            return Color.Lerp(cool, c, PlaygroundWarmth);
        }

        public static string Status() => $"night switch: {(Night ? "NIGHT" : "DAY")}, share {Share:0.00}, NightK {NightGrade.NightK:0.00}, clock {NightGrade.TimeOfDay:0.000}, switches {Switches}, frames {Frames}; dust: kind {OzUeDust.KindNow}, wipes {OzUeDust.Clears}";
    }
}
