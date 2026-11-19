// OpenZ 1.5 round 3, R3.19: on level 18 the water's sun lane, glints and sky reflection are about 35 % weaker (the fire / lamp reflection
// half that) while the game camera plays; during the briefings and flyovers the water stays exactly as approved. View only: one global
// shader value (_OZWaterCalm, read by Shaders/OpenZWaterStylized.shader), no material is changed, no rule is read or written.
// plays (never in a briefing, a flyover or the menu): _OZWaterCalmA = how much weaker (x the sparse glints, y the sun lane, z the sky
// reflection, w the fire / lamp reflection), _OZWaterCalmB.x = how much lower the ripples / waves are (their slope), both read by
// Shaders/OpenZWaterStylized.shader; _OZWaterObjRefl = the reflection of objects (banks, bridges, buildings, units) on every water
// (Shaders/OzWaterObjReflect.hlsl, both water shaders; skipped on GRAPHICS QUALITY LOW). Unset = 0 = every water exactly as before.
// about 40 % lower, reflections about 60 % less than in test exe 13a): the materials already halved the sun lane and the fire on 13 Nov
// (R3.24), so here glints 60 %, sun lane 20 %, sky 60 %, fire 20 % = every reflection at 40 % or less of exe 13a; ripples 40 % lower.
// Colours of the water, foam, the bed, the night look: untouched. No material is changed, no rule is read or written.
// Measured on game frames: right after the briefing 1.4-2.1 % of the river was bright
// flecks, but the day clock (held at sunrise during every briefing) then runs, and with the sun up it was 5.2-6.6 % (the briefing at the
// same camera: 3.3 %); at night every street lamp and headlight drew a white sheet on the water (20-27 % of it). Level 18 in play now:
// glints and sun lane at 12 % of the briefing (were 40 %), the fire / lamp / lightning reflection at 25 % (was 70 %), and the broad
// glow of any ONE light (the white sheet: a lamp 1.6, a blast or a lightning flash 3.0 over the whole river) capped at 0.3
// lamps and the lightning come down to it. Sky reflection, ripple height, colours, level 17, briefings and flyovers: as they were.
using UnityEngine;

namespace OpenZ
{
    public sealed class OzWaterPlayCalm : MonoBehaviour
    {
        /// <summary>Level 18 in play: how much weaker (x glints, y sun lane, z sky reflection, w fire / lamp reflection).</summary>
        public static Vector4 L18Calm = new Vector4(0.88f, 0.88f, 0.4f, 0.75f);   // kit l18_water_subtle_r6: was (0.6, 0.6, 0.4, 0.3) in test exe 14b
        /// <summary>1.5 kit l18_water_subtle_r6: level 18 in play, the most the broad mirror glow of ONE lamp, headlight, fire, blast or
        /// lightning flash may be before L18Calm.w (the shader's _FireReflect term; 3 = no cap = as test exe 14b). A wreck fire is
        /// 0.2-0.45, a street lamp 1.6, a blast or a flash 3. The shader reads it as _OZWaterCalmB.y = 1 - cap / 3.</summary>
        public static float L18GlowCap = 0.3f;
        /// <summary>Proof only (kit l18_water_subtle_r6's PLAY BEFORE frames): true = level 18 in play as test exe 14b drew it.</summary>
        public static bool Exe14b;
        /// <summary>Level 18 in play: how much lower the ripples / waves (0.35 = 35 % lower).</summary>
        public static float L18Ripple = 0.35f;
        /// <summary>Level 17 in play (on top of the materials' 13 Nov halving of the sun lane and the fire).</summary>
        public static Vector4 L17Calm = new Vector4(0.6f, 0.2f, 0.6f, 0.2f);
        public static float L17Ripple = 0.4f;
        /// <summary>Objects reflected on every water in play: strength (0 = off) and how much the ripples bend the reflection.</summary>
        public static float ObjReflect = 0.32f, ObjReflectBend = 0.3f;
        /// <summary>Proof only (kit water_tune_r3's before frames): the look before the kit = P on 13 Nov (level 18: glints, sun lane and sky
        /// 35 % weaker, fire 17.5 %; no ripple change, no object reflection anywhere).</summary>
        public static bool Before;
        /// <summary>The glint calm applied now (0 = as the material says).</summary>
        public static float Now { get; private set; }
        static readonly int IdA = Shader.PropertyToID("_OZWaterCalmA"), IdB = Shader.PropertyToID("_OZWaterCalmB"), IdR = Shader.PropertyToID("_OZWaterObjRefl");
        static OzWaterPlayCalm run;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            Put(Vector4.zero, Vector4.zero, Vector4.zero);
            if (run) return;
            var go = new GameObject("OZ_WaterPlayCalm"); DontDestroyOnLoad(go);
            run = go.AddComponent<OzWaterPlayCalm>();
        }

        static void Put(Vector4 a, Vector4 b, Vector4 r)
        {
            Now = a.x;                                                                   // three globals a frame: set every frame (no stale value)
            Shader.SetGlobalVector(IdA, a); Shader.SetGlobalVector(IdB, b); Shader.SetGlobalVector(IdR, r);
        }

        void LateUpdate()
        {
            var g = ZGame.I;
            var fly = ZodFlyover.Live;
            bool cine = (fly != null && fly.Active) || ZodBriefing.BriefingActive || OzBlueCameo.BriefingActive;
            bool play = g && g.World != null && !cine;
            if (!play) { Put(Vector4.zero, Vector4.zero, Vector4.zero); return; }
            Apply(g.level);
        }

        /// <summary>Sets the globals for this level in play now (the game's LateUpdate; the kit's proof renders call it between two pictures of one frame).</summary>
        public static void Apply(string lv)
        {
            if (Before)
            {
                Put(lv == OzCity.BridgeGame ? new Vector4(0.35f, 0.35f, 0.35f, 0.175f) : Vector4.zero, Vector4.zero, Vector4.zero);
                return;
            }
            Vector4 a = Vector4.zero; float rip = 0f;
            float glow = 0f;
            if (lv == OzCity.BridgeGame) { a = Exe14b ? new Vector4(0.6f, 0.6f, 0.4f, 0.3f) : L18Calm; rip = L18Ripple; glow = Exe14b ? 0f : 1f - Mathf.Clamp(L18GlowCap, 0.03f, 3f) / 3f; }   // kit l18_water_subtle_r6
            else if (lv == OzCity.CarPark) { a = L17Calm; rip = L17Ripple; }
            Put(a, new Vector4(rip, glow, 0f, 0f), new Vector4(ObjReflect, ObjReflectBend, 0f, 0f));   // kit l18_water_subtle_r6: y = the glow cap (level 18 only, else 0)
        }

        void OnDestroy() { if (run == this) { run = null; Put(Vector4.zero, Vector4.zero, Vector4.zero); } }
    }
}
