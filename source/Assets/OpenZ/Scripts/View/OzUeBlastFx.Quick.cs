// VIEW ONLY: no Sim file, no rule, no damage, no DV row.
// FLAK (exp_v2_5, the air burst of a thrown turret / tank head) gets a SHORT smoke while the player plays (View/OzExpInGame.cs SmokeLife): its
// dark cloud, its heart and its wisps go to three channels of their own (ueb_q_dust, ueb_q_cauli, ueb_q_haze). Each is the long channel
// (ueb_dust, ueb_cauli, ueb_haze: Defs + Dress, made by the same code) on a life q times as long: its size and speed curves cut to the first q
// of the long ones and the shader's clocks over life (flipbook, turn, boil, fire, age colour) slowed by q, so while a puff is seen it has the
// same size, speed and look at the same second as in the long smoke: the burst, its cloud and its growth stay as they are; only the fade starts
// sooner (the cloud and its heart stay whole for half their short life, then go on a smooth S down to nothing, no pop) and the wisps stop
// sooner. The flash, the fire, the fragments, the dust on the ground, the sounds and the beats are as made. Every scene keeps the long smoke.
// Not in Defs(): the twenty channels stay twenty (the Unit editor's counted check); made once per level (OzExpInGame's warm at its start) or
// at the first short FLAK. No asset, no shader change.
using UnityEngine;

namespace OpenZ
{
    public sealed partial class OzUeBlastFx
    {
        /// <summary>The short channels: FLAK's cloud, its heart and its wisps on a short life.</summary>
        public const string QDust = "ueb_q_dust", QCauli = "ueb_q_cauli", QHaze = "ueb_q_haze";
        /// <summary>The share of its short life the cloud and its heart stay whole before they fade (a smooth S to nothing at its end).</summary>
        public const float QuickHold = 0.5f;
        /// <summary>Checks: times the short channels were made (all levels).</summary>
        public static int QuickMade;

        /// <summary>This explosion's smoke life (x, 1 = as made; FLAK while the player plays: OzExpInGame.FlakSmokeLife).</summary>
        float q = 1f;
        /// <summary>Checks: this explosion's smoke life (x, 1 = as made).</summary>
        internal float SmokeLife => q;
        string FlakDust => q < 1f ? QDust : "ueb_dust";
        string FlakCauli => q < 1f ? QCauli : "ueb_cauli";
        string FlakHaze => q < 1f ? QHaze : "ueb_haze";

        /// <summary>The player the short channels were made on, and the life they were made for (a new number takes the next level).</summary>
        static OpenZFxPlayer quickOn; static float quickMade = 1f;

        static string QuickOf(string name) => name == "ueb_dust" ? QDust : name == "ueb_cauli" ? QCauli : name == "ueb_haze" ? QHaze : null;

        /// <summary>The short smoke on z for a life of f: the life the short channels on z are made for (0.2 .. 1); 1 = none (the long smoke).</summary>
        static float QuickOn(OpenZFxPlayer z, float f)
        {
            if (!z || !(f < 1f)) return 1f;
            if (quickOn == z && z.HasChannel(QDust) && z.HasChannel(QCauli) && z.HasChannel(QHaze)) return quickMade;
            if (!EnsureChannels(z) || !QuickLook()) return 1f;                                              // the blast look is missing: the long smoke (as made)
            f = Mathf.Clamp(f, 0.2f, 1f);
            bool ok = true;
            foreach (var d in Defs())
            {
                string qn = QuickOf(d.name); if (qn == null) continue;
                var c = d; c.name = qn; c.capacity = Mathf.Min(d.capacity, 300);                              // one FLAK a minute in game: 30 + 9 + about 10 puffs
                string nm = d.name; float ff = f;
                ok &= z.UeAddChannel(c, (m, ps, r) => { Dress(nm, m, ps, r); Quicken(nm, m, ps, ff); });
            }
            if (!ok || !z.HasChannel(QDust) || !z.HasChannel(QCauli) || !z.HasChannel(QHaze)) return 1f;
            quickOn = z; quickMade = f; QuickMade++;
            return f;
        }

        /// <summary>The blast shader and the billow flipbooks the long smoke is drawn with (loaded again after StopAll).</summary>
        static bool QuickLook()
        {
            if (!blastShader) blastShader = Resources.Load<Shader>(ShaderPath);
            if (!billowA) billowA = Resources.Load<Texture2D>(BillowPath + "A");
            if (!billowB) billowB = Resources.Load<Texture2D>(BillowPath + "B");
            if (!detailTex) detailTex = Resources.Load<Texture2D>(DetailPath);
            return blastShader && billowA && billowB && detailTex;
        }

        /// <summary>The long puff of `name` (Dress) on a life f times as long: its size and speed curves cut to their first f (the same size and
        /// speed at the same second), the shader's clocks over life slowed by f; the cloud and its heart then fade from QuickHold of the short life
        /// on a smooth S (their fade in at birth as long as before). The wisps keep their own fade on their short life.</summary>
        static void Quicken(string name, Material m, ParticleSystem ps, float f)
        {
            var sol = ps.sizeOverLifetime;
            if (sol.enabled && sol.size.mode == ParticleSystemCurveMode.Curve) sol.size = new ParticleSystem.MinMaxCurve(sol.size.curveMultiplier, Head(sol.size.curve, f));
            var lv = ps.limitVelocityOverLifetime;
            if (lv.enabled && lv.limit.mode == ParticleSystemCurveMode.Curve) lv.limit = new ParticleSystem.MinMaxCurve(lv.limit.curveMultiplier, Head(lv.limit.curve, f));
            m.SetFloat("_FlipRate", m.GetFloat("_FlipRate") * f);
            m.SetFloat("_Spin", m.GetFloat("_Spin") * f);
            m.SetFloat("_Boil", m.GetFloat("_Boil") * f);
            float heat = m.GetFloat("_HeatTime"); if (heat > 0f) m.SetFloat("_HeatTime", heat / f);
            m.SetVector("_AgeColor", Vector4.Lerp(Vector4.one, m.GetVector("_AgeColor"), f));
            if (name == "ueb_haze") return;
            float h = QuickHold, w = 1f - QuickHold;
            Fade(ps, 0f, 0f, Mathf.Min(0.03f / f, h * 0.5f), 1f, h, 1f, h + 0.25f * w, 0.844f, h + 0.5f * w, 0.5f, h + 0.75f * w, 0.156f, 1f, 0f);
        }

        /// <summary>The first f of curve c, stretched over 0 .. 1 (17 keys, smooth).</summary>
        static AnimationCurve Head(AnimationCurve c, float f)
        {
            const int n = 16; var keys = new Keyframe[n + 1];
            for (int i = 0; i <= n; i++) { float u = i / (float)n; keys[i] = new Keyframe(u, c.Evaluate(u * f)); }
            var h = new AnimationCurve(keys);
            for (int i = 0; i <= n; i++) h.SmoothTangents(i, 0f);
            return h;
        }

        /// <summary>OzExpInGame's warm at a level's start: the short channels made and drawn once, unseen, so the first short FLAK does not pay for them.</summary>
        public static void WarmQuick(Vector3 at, float f)
        {
            var z = OpenZFxPlayer.I;
            if (!z || !z.isActiveAndEnabled || QuickOn(z, f) >= 1f) return;
            z.EmitChannel(QDust, at + Vector3.up, Vector3.zero, 0.1f, 0.02f, Color.white, 0f);
            z.EmitChannel(QCauli, at + Vector3.up, Vector3.zero, 0.1f, 0.02f, Color.white, 0f);
            z.EmitChannel(QHaze, at + Vector3.up, Vector3.zero, 0.1f, 0.02f, Color.white, 0f);
        }
    }
}
