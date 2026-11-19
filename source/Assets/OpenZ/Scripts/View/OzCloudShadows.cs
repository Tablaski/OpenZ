// OpenZ (GPL-3.0) - version 1.5: soft shadows of drifting clouds on the ground (view only: the rules, the computer and the sim never read it).
using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OpenZ
{
    /// <summary>
    /// Version 1.5 cloud shadows: soft, slow shadow patches of clouds sliding over the ground, as in the classic RTS games. 1.5 kit
    /// 1.5 m/s) measured right but could not be seen; now several soft patches are on screen at once at the RTS camera (about 3 in a view, 20 to
    /// 45 m across, edges fading over about 9 m), sunlit sand under a cloud's core is about 30 % darker at noon (the Play check's band: 22 to
    /// 38 %), and a patch crosses the screen in about 45 s. Ground already in a cast shadow, every lamp, the sky light, emission, lava glow and
    /// the fog of war are unchanged; units, vehicles, guns, buildings and map items take a smaller share (<see cref="UnitShare"/>), so the light
    /// on a unit under a cloud matches the ground around it a little without making it hard to read.
    /// <list type="bullet">
    /// <item>Drawing: the ground shaders (OpenZ/Sand: L02, L07, L17; OpenZ/JungleGround: L16) and the environment class of OpenZ/TeamLit
    /// (rocks, cliffs, the map-edge skirts, bridges, trees, palette props) read one tiling cloud picture (Resources/OpenZ/FX/OzCloudShadow.png)
    /// twice, at two sizes and two drifts, through Shaders/OzCloudShadow.hlsl. TeamLit's other classes (units, vehicles, guns, buildings, map
    /// items) take <see cref="UnitShare"/> of it (cloud_shadows_visible); water is never touched. The Unit editor's floor
    /// (Resources/OpenZ/UnitEditor/OzUeFloor.shader) takes it on its two light floors (Desert dunes, Snow field) only. No keyword, no new shader
    /// variant, no material change: globals only.</item>
    /// <item>This component writes those globals every frame (LateUpdate) while a level with a row in <see cref="Rows"/> plays, and puts the
    /// strength back to 0 when it goes (the menu, the Unit editor, a level without a row: the shaders read nothing then).</item>
    /// <item>Strength now = the row's strength x (1 - NightGrade.NightK) (none at night: the moon's light keeps its shadows only) x (1 - the
    /// rain: none under a shower, the sky is overcast) x <see cref="Master"/>. Wind: the fog of war's wind (OzFogOfWar.WindDeg), so both kinds
    /// of cloud travel the same way.</item>
    /// <item>Off switches (no menu row): PlayerPrefs <see cref="PrefKey"/> = 0 (read at the level start; nothing writes it), the player's
    /// command line <c>-oz-clouds off</c> (the frame rate bench's A / B run), <see cref="Master"/> = 0 (checks, this session).</item>
    /// </list>
    /// Boot: by itself after every scene load in Play (as OzFogOfWar), on the ZGame object of a level that has a row.
    /// </summary>
    public sealed class OzCloudShadows : MonoBehaviour
    {
        /// <summary>One level's clouds. Strength: the share of the direct sun a full cloud takes (0 = none; the Unit editor's floor: the share of
        /// its colour). Threshold / Softness: the shader's cover cut on the two-layer field (0.57 / 0.32 cover about 40 % of the ground, edges
        /// fading over about 9 m at the 200 m tile: tools/sim_cloud_field.py of kit cloud_shadows_visible). TileA / TileB: the
        /// picture's size on the ground (m) of the two layers; WeightB: layer B's share; SpeedA / SpeedB (m/s) and TurnB (deg, layer B's wind
        /// turned from the main wind): two drifts, so the shapes slowly change as they travel.</summary>
        public struct Row
        {
            public string Level;
            public float Strength, Threshold, Softness, TileA, TileB, WeightB, SpeedA, SpeedB, TurnB;
            // cloud_shadows_visible: tiles 360 / 210 m -> 200 / 125 m (the picture's patches repeat about every third of a tile: ~65 m, so about 3
            // patches in a view instead of one that fills it), layer B 0.35 -> 0.25 (less grey mush between the patches), cut 0.56 / 0.20 ->
            // 0.57 / 0.32 (the same ~40 % cover, edges ~9 m at the smaller tile), drift 1.5 / 2.0 -> 2.4 / 3.0 m/s (a patch crosses the ~90 m
            // wide view in about 45 s; the first numbers took over a minute for a patch bigger than the view, so nothing seemed to move)
            public Row(string level, float strength, float threshold = 0.57f, float softness = 0.32f, float tileA = 200f, float tileB = 125f,
                       float weightB = 0.25f, float speedA = 2.4f, float speedB = 3.0f, float turnB = 10f)
            {
                Level = level; Strength = strength; Threshold = threshold; Softness = softness; TileA = tileA; TileB = tileB; WeightB = weightB;
                SpeedA = speedA; SpeedB = speedB; TurnB = turnB;
            }
        }

        /// <summary>Every level with cloud shadows (all four are outdoor levels with a day; at night and in rain the clouds fade out by
        /// themselves) and the Unit editor's playground (UE: on its two light floors only, Desert dunes and Snow field; the dark floors are night
        /// sky graphics without a sun). A level without a row has none: the menu, the intro, any level added later until it gets its own row (an
        /// indoor or space level stays without one).
        /// cloud_shadows_visible strengths: measured in Play on 9 Nov (cloud_shadows' check), screen darkening under a full cloud = 0.41 x the
        /// strength on L02 sand, 0.45 x on L16; aimed at about 30 % (the band 22 to 38 %): L02 0.40 -> 0.72, L16 0.38 -> 0.66, L07 / L17 (not yet
        /// measured, the L02 factor assumed) 0.35 -> 0.70. If the Play check's NOON row is out of band: strength x (30 / measured %) once.</summary>
        public static readonly Row[] Rows =
        {
            new Row("L02", 0.72f),                                  // desert: warm sand, the clearest sky (the sand's warm shadow bounce gives back a sixth)
            new Row("L07", 0.70f),                                  // volcano: ash haze
            new Row("L16", 0.66f, threshold: 0.59f),                // jungle: a little less cover (the canopy already shades much of it)
            new Row("L17", 0.70f), new Row("L18", 0.70f),                                  // city: concrete
            new Row(OzUnitEditor.LevelId, 0.28f),                   // the Unit editor: its floor is darkened directly (no sun term), light floors only
        };

        /// <summary>cloud_shadows_visible: the share of a cloud that TeamLit's units, vehicles, guns, buildings and map items take (the ground
        /// takes 1), written to _OzCloudA.y. Under a full L02 cloud a unit's sunlit side is about 12 % darker: the same light as the ground
        /// around it, still clearly readable. 0 = the units are never touched (cloud_shadows' first rule).
        /// everything under it): 0.85, so buildings, decor items, vehicles and units darken almost like the ground (about 24 % under a full L02 core).</summary>
        public static float UnitShare = 0.85f;

        public const string TexRes = "OpenZ/FX/OzCloudShadow", PrefKey = "oz_cloud_shadows", Arg = "-oz-clouds";
        const string Tag = "[OpenZ] cloud shadows";
        /// <summary>The rotation of layer B's frame in the shader (OzCloudShadow.hlsl: cos, sin of 37 deg); its drift is given in that frame.</summary>
        const float CosB = 0.79864f, SinB = 0.60181f;

        /// <summary>Checks: a multiplier on the strength this session (0 = off, 1 = as the row says). Never saved.</summary>
        public static float Master = 1f;
        /// <summary>Checks: the drift stops (frames before / after at the same cloud spot). Never saved.</summary>
        public static bool Freeze;
        /// <summary>The player was started with <c>-oz-clouds off</c>.</summary>
        public static bool ArgOff { get; private set; }
        public static OzCloudShadows I { get; private set; }

        /// <summary>This level's row (Strength 0 = none).</summary>
        public Row Now { get; private set; }
        /// <summary>The strength written last frame (after night, rain, Master).</summary>
        public float StrengthNow { get; private set; }
        /// <summary>The two layers' drift offsets (picture tiles, 0..1) and the metres layer A has travelled since the level start.</summary>
        public Vector2 OffsetA => offA;
        public Vector2 OffsetB => offB;
        public float TravelledA { get; private set; }
        public bool PrefOff { get; private set; }
        public Texture2D Picture => tex;

        static readonly int IdTex = Shader.PropertyToID("_OzCloudTex"), IdA = Shader.PropertyToID("_OzCloudA"), IdB = Shader.PropertyToID("_OzCloudB"),
            IdParams = Shader.PropertyToID("_OzCloudParams"), IdRain = Shader.PropertyToID("_OzRain");

        Vector2 offA, offB;
        Texture2D tex;

        // ------------------------------------------------------------------ boot (as OzFogOfWar)
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoInstall()
        {
            ArgOff = ReadArgOff();
            Master = 1f; Freeze = false;                                                  // a check's switches never outlive its Play
            SceneManager.sceneLoaded -= OnSceneLoaded; SceneManager.sceneLoaded += OnSceneLoaded;
            TryInstall();
        }

        static void OnSceneLoaded(Scene s, LoadSceneMode m) => TryInstall();

        static void TryInstall()
        {
            if (!Application.isPlaying) return;
            var g = ZGame.I ? ZGame.I : FindFirstObjectByType<ZGame>();
            if (!g || g.GetComponent<OzCloudShadows>()) return;
            if (RowOf(g.level).Strength <= 0f) return;
            g.gameObject.AddComponent<OzCloudShadows>();
        }

        static bool ReadArgOff()
        {
            try
            {
                var a = Environment.GetCommandLineArgs();
                for (int i = 0; i + 1 < a.Length; i++)
                    if (string.Equals(a[i], Arg, StringComparison.OrdinalIgnoreCase)) return string.Equals(a[i + 1], "off", StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception) { }
            return false;
        }

        /// <summary>The row of a level id (a row with Strength 0 when it has none).</summary>
        public static Row RowOf(string level)
        {
            foreach (var r in Rows) if (r.Level == level) return r;
            return new Row(level ?? "", 0f);
        }

        // ------------------------------------------------------------------ life
        void Awake()
        {
            if (I && I != this) { Destroy(this); return; }
            I = this;
        }

        void Start()
        {
            var g = GetComponent<ZGame>();
            Now = RowOf(g ? g.level : "");
            PrefOff = PlayerPrefs.GetInt(PrefKey, 1) == 0;
            tex = Resources.Load<Texture2D>(TexRes);
            if (!tex) Debug.LogWarning($"{Tag}: the cloud picture Resources/{TexRes} is missing; none on this level");
            else Shader.SetGlobalTexture(IdTex, tex);
            offA = Vector2.zero; offB = Vector2.zero; TravelledA = 0f;
            Debug.Log($"{Tag}: {Now.Level} strength {Now.Strength:0.00}, cover cut {Now.Threshold:0.00} / {Now.Softness:0.00}, tiles {Now.TileA:0} m + {Now.TileB:0} m, drift {Now.SpeedA:0.0} / {Now.SpeedB:0.0} m/s"
                      + (ArgOff ? " (off: -oz-clouds off)" : PrefOff ? $" (off: {PrefKey} = 0)" : ""));
        }

        void LateUpdate()
        {
            if (!tex || Now.Strength <= 0f || ArgOff || PrefOff || OzFogOfWar.Wanted) { Write(0f); return; }
            if (!Freeze)
            {
                float dt = Time.deltaTime;
                float a = OzFogOfWar.WindDeg * Mathf.Deg2Rad, b = (OzFogOfWar.WindDeg + Now.TurnB) * Mathf.Deg2Rad;
                // the picture moves WITH the wind: uv = p / tile + offset, so the offset goes against it
                offA -= new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (Now.SpeedA * dt / Now.TileA);
                var wb = new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * Now.SpeedB;
                offB -= new Vector2(CosB * wb.x - SinB * wb.y, SinB * wb.x + CosB * wb.y) * (dt / Now.TileB);   // in layer B's turned frame
                offA = new Vector2(Mathf.Repeat(offA.x, 1f), Mathf.Repeat(offA.y, 1f));                         // the picture tiles: no precision lost over a long match
                offB = new Vector2(Mathf.Repeat(offB.x, 1f), Mathf.Repeat(offB.y, 1f));
                TravelledA += Now.SpeedA * dt;
            }
            float k = Now.Strength * (1f - Mathf.Clamp01(NightGrade.NightK)) * Mathf.Clamp01(Master);
            if (Now.Level == OzUnitEditor.LevelId) k = OzUeBackdrop.LightFloor ? Now.Strength * Mathf.Clamp01(Master) : 0f;   // the Unit editor: fixed noon, the light floors only
            float rain = Shader.GetGlobalFloat(IdRain);
            k *= 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.05f, 0.5f, rain));
            Write(k);
        }

        void Write(float k)
        {
            StrengthNow = k;
            Shader.SetGlobalVector(IdA, new Vector4(1f / Mathf.Max(Now.TileA, 1f), Mathf.Clamp01(UnitShare), offA.x, offA.y));   // y: TeamLit's units' share (cloud_shadows_visible)
            Shader.SetGlobalVector(IdB, new Vector4(1f / Mathf.Max(Now.TileB, 1f), 0f, offB.x, offB.y));
            Shader.SetGlobalVector(IdParams, new Vector4(k, Now.Threshold, Now.Softness, Now.WeightB));
        }

        void OnDisable() => Shader.SetGlobalVector(IdParams, Vector4.zero);       // the shaders read nothing from here on

        void OnDestroy()
        {
            Shader.SetGlobalVector(IdParams, Vector4.zero);
            if (I == this) I = null;
        }
    }
}
