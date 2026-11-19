// The seventh test explosion, BURST (WHITE AIRBURST):
// round 3's FLAK restored exactly as it was (its FlakBeat / FlakTick of round 3, renamed; kit _set_aside/round4_20261112/r3_files).
// Its rule beats are FLAK's (View/OzUeBlasts.cs StagesOf). Only the Unit editor's tile and the checks set it off.
using UnityEngine;

namespace OpenZ
{
    public sealed partial class OzUeBlastFx
    {
        Vector3 AirAt => O + Up * 9f;

        void AirBeat(int stage, Vector3 at)
        {
            if (stage > 0)
            {   // a shard hits the ground (the small hit of the rules there)
                Sparks(at + Up * 0.2f, 10, 4f, 10f, 0.3f, 0.6f);
                Skirt(at, 4, 0.2f, 2f, 3.5f, 0.6f, 0.9f, 1.1f, 1.6f, 0.75f);
                AddL(at + Up * 0.5f, new Color(1f, 0.9f, 0.7f), 10f, 6f, 0.15f);
                ScorchL(at, 0.6f, 0);
                Snd("imp_shrapnel_tink", at, 1.5f, R(0.85f, 1.25f), 0.075f * n0 + R(0f, 0.03f));           // one after the other: every hit is heard
                n0++;
                return;
            }
            var B = AirAt;
            Flash(B, 11f, 0.1f, Color.white);
            Flash(B, 6f, 0.26f, new Color(1f, 0.95f, 0.8f));
            AddL(B, new Color(1f, 0.97f, 0.9f), 95f, 36f, 0.3f, 2.2f, 0f, true);
            GroundL(O, 3.2f, false, true);
            Kick(O, 8f);
            for (int i = 0; i < 14; i++) P("ueb_shard", B, Sphere() * 3f, 0.16f, R(0.35f, 0.55f), Color.white);        // the spikes of the star
            float a0 = R(0f, TAU);
            for (int i = 0; i < 40; i++)
            {   // white-hot shards, mostly out and down, each leaving a thin smoke trail that stays
                float a = a0 + i * TAU / 40f + R(-0.1f, 0.1f), el = R(-0.95f, 0.6f), sp = R(20f, 36f);
                var v = Az(a) * (Mathf.Cos(el) * sp) + Up * (Mathf.Sin(el) * sp);
                Launch(B + v.normalized * 0.5f, v, 12f, R(0.9f, 1.5f), "ueb_shard", R(0.12f, 0.2f), new Color(1f, 0.96f, 0.82f), "ueb_trail", 0.012f, R(0.55f, 0.8f), R(1.9f, 2.8f), new Color(0.80f, 0.80f, 0.82f), 0.6f, 3);
            }
            for (int i = 0; i < 7; i++) P("ueb_hot", B + Ball(0.7f), Sphere() * R(1.5f, 5f), R(0.7f, 1.1f), R(1.6f, 2.4f), new Color(0.2f, 0.2f, 0.21f));        // the hot heart
            for (int i = 0; i < 6; i++) P("ueb_fire", B + Ball(0.6f), Sphere() * R(2f, 6f), R(0.3f, 0.5f), R(2.4f, 3.6f), Color.white);
            for (int i = 0; i < 26; i++)                                                                 // the dark burst cloud where it went off: one knot of smoke that hangs and drifts
                P("ueb_cauli", B + Ball(1.6f), Sphere() * R(1f, 4.5f) + lean, R(3.6f, 5.2f), R(1.9f, 2.9f), Color.Lerp(new Color(0.2f, 0.2f, 0.21f), new Color(0.4f, 0.39f, 0.38f), R(0f, 1f)));
            Embers(B, 70, 3f, 11f, 1.8f, 3.2f);
            Snd("exp_small", B, 1.1f, 1.55f);
            Bank("explosion", B, LegacyMatch.GainExplosion, 1.35f, LegacyMatch.BankExplosion);
            Snd("lava_sizzle_enter", B, 0.5f, 1.3f, 0.1f);
            end = 4.5f;
        }

        void AirTick(float dt)
        {
            if (t < 0.5f)
                for (int i = Ticks(ref c0, dt, 0.05f); i > 0; i--)                                        // the burst twinkles out
                    P("ueb_ember", AirAt + Ball(2.5f + 4f * t), Sphere() * 2f, R(0.3f, 0.6f), R(0.25f, 0.45f), Color.white);
        }
    }
}
