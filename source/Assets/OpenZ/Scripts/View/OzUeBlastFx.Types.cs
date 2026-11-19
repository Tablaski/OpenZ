using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.5 kit ue_explosions_6: the six timelines (View/OzUeBlastFx.cs). Each has its own shape, clock, palette and sound:
    /// COLUMN a ball of fire that climbs into a tall sand-coloured column of rolling heads with fire inside (7 s), FUEL a red
    /// fireball that rolls up into black smoke with glowing cracks over a burning pool (6 s), CHAIN eleven sharp pops walking outward
    /// (1.3 s + wisps), GEYSER a brown fountain of tapering earth jets with falling clods and a wide dust skirt, no fire (6 s), FLAK an air
    /// burst: a hard flash, a dirty ball of fire that turns into a ragged black cloud with a hot heart, a rain of fragments on the
    /// ground under it (6 s), SHOCK a blue charge, a ring over the ground with a wall of dust behind it, a heavy head of dust and
    /// smoke with fire inside rising from its middle (5.6 s). Every number here is a look number (the numbers of the rules: View/OzUeBlasts.cs StagesOf).
    /// </summary>
    public sealed partial class OzUeBlastFx
    {
        /// <summary>The smoke a ball of fire cools into on this floor.</summary>
        Color Burnt(float k) => Color.Lerp(Dust(0.4f + 0.5f * k), Soot(1f), 0.2f + 0.2f * k);

        // ================================================================================================ 1 COLUMN
        const float ColumnTop = 17f;

        /// <summary>The column is not one tone: most heads are the floor's dust, every fourth is darker, burnt by the fire inside.</summary>
        Color ColumnTone() => R(0f, 1f) < 0.26f ? Color.Lerp(Dust(0.9f), Soot(0.4f), R(0.35f, 0.75f)) : Dust(R(0f, 0.8f));

        void ColumnBeat(int stage, Vector3 at)
        {
            Flash(O + Up * 1.4f, 11f, 0.14f, new Color(1f, 0.93f, 0.78f));
            Flash(O + Up * 2.4f, 7f, 0.36f, new Color(1f, 0.7f, 0.38f));
            Glow(O + Up * 2.5f, Up * 6f, 16f, 0.85f, FireGlow, 0.55f);                                     // the fire's halo: it climbs with the ball of fire
            Glow(O + Up * 5f, Up * 6f, 13f, 1.3f, FireDeep, 0.42f, 0.25f);
            AddL(O + Up * 3f, new Color(1f, 0.78f, 0.5f), 80f, 32f, 0.6f, 2.2f, 0f, true);
            GroundL(O, 3.4f, false, true);
            ScorchL(O, 3.0f, 2);
            Kick(O, 11f);
            for (int i = 0; i < 18; i++)                                                                 // the ball of fire that starts the column: it rolls up out of the hole
                P("ueb_hot", O + Ball(1.2f) + Up * R(0.8f, 2.6f), Up * R(4f, 28f) + Ring() * R(0f, 3.2f), R(1.9f, 2.7f), R(2.8f, 4.0f), Burnt(R(0f, 1f)));
            for (int i = 0; i < 12; i++)                                                                 // flames licking out low around it
            {
                var d = Ring();
                P("ueb_fire", O + d * R(0.4f, 1.8f) + Up * R(0.3f, 1.2f), d * R(1f, 5f) + Up * R(5f, 13f), R(0.5f, 0.9f), R(3.0f, 4.6f), Color.white);
            }
            for (int i = 0; i < 20; i++)                                                                 // the wide foot: heads pushed out low around the hole, born glowing
            {
                var d = Ring();
                P("ueb_cauli", O + d * R(0.8f, 2.8f) + Up * R(0.6f, 1.8f), d * R(2.0f, 5.0f) + Up * R(1.2f, 3.4f) + lean, R(4.0f, 5.8f), R(2.2f, 3.2f), Dust(R(0f, 0.8f)));
            }
            UeRingL(O, 9f, 0.3f, 0f, new Color(1.3f, 1.0f, 0.6f));
            float f0a = R(0f, TAU);
            for (int i = 0; i < 9; i++)                                                                  // thick jets of dust and earth thrown up and out, a dust trail behind each
            {
                float a = f0a + i * TAU / 9f + R(-0.25f, 0.25f), el = R(0.8f, 1.3f), sp = R(15f, 24f);
                var v = Az(a) * (Mathf.Cos(el) * sp) + Up * (Mathf.Sin(el) * sp);
                Launch(O + Up * 1.2f + Az(a) * 1.0f, v, 9.81f, R(1.0f, 1.5f), "ueb_finger", R(1.5f, 2.3f), Color.Lerp(Earth(R(0f, 0.6f)), Dust(0.8f), 0.5f), "ueb_trail", 0.025f, R(0.8f, 1.2f), R(1.8f, 2.6f), Dust(R(0f, 0.6f)), 0.7f, 1);
            }
            float a0 = R(0f, TAU);
            for (int i = 0; i < 20; i++)                                                                 // debris streaks thrown outward, each with its dust trail
            {
                float a = a0 + i * TAU / 20f + R(-0.15f, 0.15f), el = R(0.35f, 1.1f), sp = R(14f, 27f);
                var v = Az(a) * (Mathf.Cos(el) * sp) + Up * (Mathf.Sin(el) * sp);
                Launch(O + Up * 0.8f + Az(a) * 0.8f, v, 9.81f, R(1.3f, 2.1f), "ueb_dirt", R(0.22f, 0.4f), Earth(R(0.2f, 1f)), "ueb_trail", 0.02f, R(0.7f, 1.1f), R(1.5f, 2.3f), Dust(R(0.1f, 0.6f)), 0.6f, 1);
            }
            for (int i = 0; i < 7; i++)                                                                  // burning pieces thrown out: each burns on for a moment where it lands
            {
                float el = R(0.55f, 1.15f), sp = R(9f, 17f);
                Launch(O + Up, Ring() * (Mathf.Cos(el) * sp) + Up * (Mathf.Sin(el) * sp), 9.81f, 3f, null, 0f, Color.white, "ueb_fire", 0.035f, 1.0f, 0.35f, Color.white, 1f, 4);
            }
            Sparks(O + Up, 50, 7f, 17f, 0.45f, 0.95f);
            Skirt(O, 44, 1.5f, 11.5f, 16f, 1.5f, 2.1f, 2.4f, 3.6f, 0.95f);                                 // the rolling dust skirt
            Embers(O + Up * 6f, 80, 2f, 8f, 1.6f, 3.4f);
            Bank("vdeath", O, LegacyMatch.GainHeavy, 1.0f, LegacyMatch.BankHeavy);
            Snd("exp_large", O);
            Snd("exp_debris_rain", O, 0.9f, 1f, 0.3f);
            Snd("dst_debris_settle", O, 0.8f, 1f, 1.1f);
            end = 6.6f;
        }

        void ColumnTick(float dt)
        {
            if (!f0 && t >= 0.16f) { f0 = true; Skirt(O, 30, 3f, 5f, 10f, 1.6f, 2.2f, 2.8f, 4.2f, 0.8f); }  // a second, slower roll of dust
            if (t < 1.6f)
                for (int i = Ticks(ref c0, dt, t < 0.7f ? 0.013f : 0.035f); i > 0; i--)
                {   // the heads are thrown up out of the hole: the fast ones climb to the top and slow there, the slow ones fill the foot;
                    // every third is born as a ball of fire and cools on its way up
                    float k = t / 1.6f, up = Mathf.Lerp(2.5f, 30f, Mathf.Pow(R(0f, 1f), 1.2f)) * (1f - 0.4f * k);
                    P("ueb_cauli", O + Ring() * R(0f, 1.5f) + Up * R(0.8f, 2.6f), Up * up + Ring() * R(0f, 1.5f) + lean, R(4.4f, 6.4f), R(2.2f, 3.4f), ColumnTone());
                }
            if (t > 0.1f && t < 2.1f)
                for (int i = Ticks(ref c1, dt, 0.05f); i > 0; i--)
                {   // the fire inside: balls of fire climbing through the heads, cooling into the smoke of the column
                    P("ueb_hot", O + Up * R(0.8f, 2.4f) + Ball(0.9f), Up * R(4f, 27f) * (1f - 0.3f * t / 2.1f) + Ring() * R(0f, 1.2f), R(1.6f, 2.4f), R(2.1f, 3.1f) * (1f - 0.25f * t / 2.1f), Burnt(R(0f, 1f)));
                }
            if (t > 0.1f && t < 2.2f)
                for (int i = Ticks(ref c3, dt, 0.09f); i > 0; i--)                                        // flames licking up at the foot
                    P("ueb_fire", O + Up * R(0.4f, 4.5f) + Ball(1.0f), Up * R(4f, 9f), R(0.45f, 0.8f), R(1.8f, 3.0f) * (1f - 0.4f * t / 2.2f), Color.white);
            while (n0 < 3 && t >= 0.22f + 0.27f * n0)
            {   // three secondary pops on the flank of the column
                n0++;
                var pp = O + Ring() * R(2f, 4f) + Up * R(4f, 11f);
                Flash(pp, 5f, 0.1f, new Color(1f, 0.9f, 0.65f));
                Glow(pp, Up * 2f, 7f, 0.4f, FireGlow, 0.6f);
                AddL(pp, new Color(1f, 0.75f, 0.45f), 22f, 10f, 0.25f);
                for (int i = 0; i < 5; i++)
                {
                    var dv = Sphere(); dv.y = Mathf.Abs(dv.y) * 0.6f;
                    Launch(pp, dv * R(9f, 16f), 9.81f, R(0.8f, 1.3f), "ueb_dirt", R(0.18f, 0.3f), Earth(R(0.2f, 1f)), "ueb_trail", 0.025f, R(0.5f, 0.8f), R(1.1f, 1.6f), Dust(R(0.1f, 0.6f)), 0.55f, 1);
                }
                for (int i = 0; i < 4; i++) P("ueb_hot", pp + Ball(0.6f), Sphere() * R(1f, 4f) + Up * 2f, R(0.8f, 1.2f), R(1.6f, 2.4f), Burnt(R(0f, 1f)));
                for (int i = 0; i < 5; i++) P("ueb_cauli", pp + Ball(0.9f), Sphere() * R(1f, 3f) + Up * R(1.5f, 3f) + lean, R(3f, 4.4f), R(1.6f, 2.4f), Dust(R(0.1f, 0.8f)));
                Sparks(pp, 10, 5f, 11f, 0.3f, 0.6f);
                Bank("exp2_sat", pp, LegacyMatch.GainExplosion * 0.5f, R(1.0f, 1.12f), LegacyMatch.BankExplosion);
            }
            while (n1 < 7 && t >= 0.3f + 0.3f * n1)                                                       // the fire lights the column from inside while it burns
            {
                n1++;
                var lp = O + Up * R(2.5f, 7f) + Ball(1f);
                AddL(lp, new Color(1f, 0.6f, 0.28f), 26f * (1f - n1 / 9f), 15f, 0.5f, 2.2f, 0.5f);
                Glow(lp + Up * (1.5f * n1), Up * 3f, 12f * (1f - n1 / 10f), 0.6f, FireDeep, 0.5f);          // the halo stays for as long as the fire rolls inside
            }
            if (t > 1.6f && t < 5.4f)
                for (int i = Ticks(ref c2, dt, 0.1f); i > 0; i--)                                         // the column stands and drifts: a thin veil over it
                    P("ueb_haze", O + Up * R(1f, ColumnTop + 3f) + Ball(2.4f) + lean * (t * 1.2f), Up * R(0.4f, 1.2f) + lean, R(3.4f, 4.8f), R(2.6f, 3.8f), Dust(R(0.2f, 0.9f)), 0.4f);
        }

        // ================================================================================================ 2 FUEL
        Vector3 FuelCap(float tt) => O + Up * (2.2f + 10.5f * (1f - Mathf.Exp(-1.3f * tt)));
        float c4x = 99f;                                                                                // FUEL: when the pool flares up again

        void FuelBeat(int stage, Vector3 at)
        {
            if (stage > 0)
            {   // the ground burns on: a flare with each of the burn beats of the rules
                for (int i = 0; i < 8; i++) P("ueb_fuel", O + Ring() * R(0f, 2.8f) + Up * R(0.3f, 0.9f), Up * R(2f, 4.6f), R(0.5f, 0.85f), R(1.8f, 2.8f), Color.white);
                for (int i = 0; i < 3; i++) P("ueb_soot", O + Ring() * R(0f, 1.6f) + Up * R(0.8f, 1.6f), Up * R(3f, 5f) + lean, R(1.8f, 2.6f), R(1.3f, 1.9f), Soot(R(0f, 0.6f)));
                AddL(O + Up, new Color(1f, 0.5f, 0.18f), 30f, 14f, 0.5f, 2.2f, 0.5f);
                Snd("dst_fire_ignite", O, 0.6f, R(0.8f, 1.0f));
                return;
            }
            Flash(O + Up * 1.5f, 13f, 0.2f, new Color(1f, 0.74f, 0.42f));
            Glow(O + Up * 2f, Up * 4f, 17f, 0.9f, new Color(1f, 0.45f, 0.15f), 0.55f);                      // the fireball's halo
            Glow(O + Up * 4.5f, Up * 4f, 13f, 1.4f, new Color(1f, 0.36f, 0.1f), 0.42f, 0.3f);
            AddL(O + Up * 3f, new Color(1f, 0.55f, 0.2f), 90f, 34f, 2.2f, 1.0f, 0.5f, false);
            GroundL(O, 4f, false, true);
            ScorchL(O, 3.6f, 2);
            Kick(O, 9f);
            for (int i = 0; i < 9; i++)                                                                  // tongues of burning fuel licking out over the ground
            {
                var d = Ring();
                P("ueb_fuel", O + d * R(1.5f, 3.4f) + Up * R(0.3f, 1.0f), d * R(7f, 15f) + Up * R(1f, 3.6f), R(0.5f, 0.85f), R(1.7f, 2.5f), Color.white);
            }
            for (int i = 0; i < 14; i++)                                                                 // the wide low roll of fire over the ground
            {
                var d = Ring();
                P("ueb_soot", O + d * R(0.5f, 2.2f) + Up * R(0.8f, 1.8f), d * R(4f, 11f) + Up * R(1f, 4f), R(2.2f, 3.2f), R(2.2f, 3.2f), Soot(R(0f, 0.7f)));
            }
            for (int i = 0; i < 20; i++)                                                                 // the fireball itself: fire that rolls up and turns to black smoke with glowing cracks
                P("ueb_soot", O + Ball(1.8f) + Up * R(1.2f, 2.6f), Up * R(3f, 11f) + Ring() * R(0f, 5f), R(3.4f, 4.8f), R(2.8f, 4.0f), Soot(R(0f, 0.7f)));
            for (int i = 0; i < 16; i++)                                                                 // burning drops thrown out, each a small flame where it lands
            {
                float el = R(0.5f, 1.1f), sp = R(10f, 18f);
                Launch(O + Up, Ring() * (Mathf.Cos(el) * sp) + Up * (Mathf.Sin(el) * sp), 9.81f, 3f, null, 0f, Color.white, "ueb_fire", 0.03f, 1.2f, 0.4f, Color.white, 1f, 4);
            }
            Embers(O + Up * 2f, 90, 3f, 11f, 2f, 3.8f);
            Skirt(O, 30, 2f, 6f, 11f, 1.5f, 2.1f, 2.0f, 3.0f, 0.7f);                                      // the push of the blast over the ground
            Snd("exp_vehicle_death", O, 1f, 0.72f);
            Bank("heavy", O, LegacyMatch.GainHeavy * 0.9f, 0.62f, LegacyMatch.BankHeavy, 0.08f);
            Snd("dst_fire_ignite", O, 1f, 0.8f);
            Snd("dst_fire_ignite", O, 0.8f, 0.7f, 0.5f);
            c4x = R(2.7f, 3.5f);
            end = 6.2f;
        }

        void FuelTick(float dt)
        {
            if (t < 1.5f)
                for (int i = Ticks(ref c0, dt, 0.03f); i > 0; i--)
                {   // the cap: fire born in its middle, pushed up and outward at the top (it rolls over into a mushroom)
                    float k = Mathf.Clamp01(t / 1.5f);
                    P("ueb_soot", FuelCap(t) + Ball(1.4f + 1.8f * k), Up * R(1.5f, 3.6f) + Ring() * R(1.0f, 4.2f) + lean, R(3.6f, 5.6f), R(2.6f, 3.8f), Soot(R(0f, 0.8f)));
                }
            if (t < 1.0f)
                for (int i = Ticks(ref c1, dt, 0.06f); i > 0; i--)                                        // its bright heart: fresh fire born in the middle of the cap
                    P("ueb_soot", FuelCap(t) + Ball(1.0f), Up * R(2f, 5f) + Ring() * R(0.5f, 2.4f), R(1.6f, 2.2f), R(1.8f, 2.6f), Soot(R(0f, 0.5f)));
            if (t > 0.2f && t < 2.6f)
                for (int i = Ticks(ref c2, dt, 0.07f); i > 0; i--)
                {   // the stem under the cap
                    float top = Mathf.Max(1f, FuelCap(t).y - O.y - 2.0f);
                    P("ueb_soot", O + Up * R(0.8f, top) + Ball(0.8f), Up * R(2.5f, 4.6f) + lean, R(2.4f, 3.6f), R(1.5f, 2.2f), Soot(R(0.2f, 0.9f)));
                }
            if (t < 5.6f)
            {   // the pool of fire on the ground, dying down, with its flicker of light
                float kk = 1f - 0.65f * t / 5.6f;
                for (int i = Ticks(ref c3, dt, 0.03f); i > 0; i--)
                    P("ueb_fire", O + Ring() * R(0f, 4.6f * kk) + Up * R(0.2f, 0.7f), Up * R(1.5f, 3.6f), R(0.45f, 0.8f), R(1.6f, 2.8f) * kk, Color.white);
                while (t >= 0.35f * (n0 + 1))
                {
                    n0++; AddL(O + Up, new Color(1f, 0.5f, 0.18f), 22f * kk, 17f, 0.45f, 2.2f, 0.6f);
                    Glow(O + Up * 0.8f + Ring() * R(0f, 1.5f), Vector3.zero, R(6f, 8.5f) * kk, 0.45f, new Color(1f, 0.4f, 0.12f), 0.4f * kk);   // the pool's own flicker of glow
                }
            }
            while (n1 < 4 && t >= 0.3f + 0.36f * n1) { n1++; Glow(FuelCap(t), Up * 2f, 11f, 0.6f, new Color(1f, 0.4f, 0.12f), 0.5f); }   // the cap glows while fire is born in it
            if (!f0 && t >= c4x)
            {   // the pool does not die evenly: once it flares up again on one side, a gulp of fire and black smoke
                f0 = true;
                var fp = O + Ring() * R(1.2f, 2.6f);
                for (int i = 0; i < 9; i++) P("ueb_fuel", fp + Ring() * R(0f, 1.4f) + Up * R(0.3f, 0.9f), Up * R(3f, 6.5f) + Ring() * R(0f, 1.2f), R(0.55f, 0.9f), R(1.9f, 3.0f), Color.white);
                for (int i = 0; i < 5; i++) P("ueb_soot", fp + Ball(0.8f) + Up * R(0.8f, 1.6f), Up * R(3.5f, 6.5f) + lean, R(2.0f, 2.8f), R(1.5f, 2.2f), Soot(R(0f, 0.6f)));
                AddL(fp + Up, new Color(1f, 0.5f, 0.18f), 34f, 15f, 0.5f, 2.2f, 0.5f);
                Glow(fp + Up * 1.5f, Up * 2f, 10f, 0.55f, new Color(1f, 0.42f, 0.13f), 0.6f);
                Embers(fp + Up, 16, 2f, 7f, 1.2f, 2.4f);
                Snd("dst_fire_ignite", fp, 0.8f, R(0.85f, 1.0f));
            }
            if (t > 2.6f && t < 5.6f)
                for (int i = Ticks(ref c4, dt, 0.16f); i > 0; i--)                                        // thin black smoke off the pool, drifting
                    P("ueb_haze", O + Up * R(1f, 5f) + Ball(1.6f), Up * R(1.2f, 2.4f) + lean, R(2.6f, 3.6f), R(1.8f, 2.6f), Soot(R(0.2f, 1f)), 0.5f);
        }

        // ================================================================================================ 3 CHAIN
        void ChainBeat(int stage, Vector3 p)
        {
            bool last = stages != null && stage == stages.Length - 1;
            Flash(p + Up * 0.8f, 6.5f, 0.1f, new Color(1f, 0.95f, 0.7f));
            AddL(p + Up * 1.5f, new Color(1f, 0.85f, 0.55f), 30f, 13f, 0.22f, 2.2f, 0f, true);
            GroundL(p, 1.4f, false, true);
            ScorchL(p, 1.2f, 1);
            Kick(p, stage == 0 || last ? 6f : 3f, stage == 0 || last);
            var grey = Color.Lerp(Dust(R(0f, 0.8f)), Grey(R(0f, 0.6f)), 0.5f);
            float big = last ? 1.5f : stage == 0 ? 1.2f : R(0.85f, 1.15f);                                // no two pops alike; the first is strong, the last is the big one
            Glow(p + Up * 1.2f, Up * 2f, 7.5f * big, 0.34f, FireGlow, 0.5f);
            for (int i = 0; i < (last ? 10 : 6); i++) P("ueb_hot", p + Ball(0.5f * big) + Up * R(0.5f, 1.4f), Up * R(2f, 6.5f) * big + Ring() * R(0f, 2.6f), R(0.8f, 1.25f), R(1.7f, 2.5f) * big, Color.Lerp(grey, Soot(0.6f), 0.4f));
            for (int i = 0; i < (last ? 8 : 4); i++) P("ueb_fire", p + Ball(0.4f) + Up * R(0.3f, 1.0f), Up * R(4f, 9f) * big + Ring() * R(0f, 3f), R(0.35f, 0.55f), R(1.9f, 2.9f) * big, Color.white);
            if (last)
            {   // the last pop: more of everything, pieces thrown wide with their trails, a second ring
                for (int i = 0; i < 8; i++)
                {
                    float el2 = R(0.4f, 1.1f), sp2 = R(11f, 19f);
                    Launch(p + Up * 0.6f, Ring() * (Mathf.Cos(el2) * sp2) + Up * (Mathf.Sin(el2) * sp2), 9.81f, R(1.0f, 1.5f), "ueb_dirt", R(0.2f, 0.34f), Earth(R(0.2f, 1f)), "ueb_trail", 0.022f, R(0.6f, 0.9f), R(1.3f, 1.9f), grey, 0.6f, 1);
                }
                for (int i = 0; i < 8; i++) P("ueb_cauli", p + Up * R(0.8f, 3f) + Ball(1f), Up * R(3f, 7f) + Ring() * R(0f, 1.6f) + lean, R(3.0f, 4.2f), R(1.6f, 2.3f), grey);
                UeRingL(p, 6.5f, 0.34f, 0.03f, new Color(1.3f, 1.0f, 0.6f));
                Embers(p + Up * 2f, 24, 2f, 8f, 1.2f, 2.6f);
            }
            for (int i = 0; i < 12; i++)
            {   // a spray of dirt straight up out of the hole
                float tilt = R(0f, 0.5f); var az = Ring();
                P("ueb_dirt", p + az * R(0f, 0.5f) + Up * 0.3f, (Up * Mathf.Cos(tilt) + az * Mathf.Sin(tilt)) * R(8f, 15f), R(0.8f, 1.3f), R(0.2f, 0.36f), Earth(R(0f, 1f)), 1f);
            }
            P("ueb_spray", p + Up * 0.8f, Up * R(7f, 10f), R(0.9f, 1.2f), R(2.2f, 3.0f), Earth(R(0.1f, 0.7f)));
            Sparks(p + Up * 0.5f, 20, 6f, 16f, 0.35f, 0.75f);
            for (int i = 0; i < 4; i++)
            {
                float el = R(0.6f, 1.2f), sp = R(8f, 13f);
                Launch(p + Up * 0.5f, Ring() * (Mathf.Cos(el) * sp) + Up * (Mathf.Sin(el) * sp), 9.81f, R(0.7f, 1.0f), "ueb_dirt", 0.2f, Earth(R(0.3f, 1f)), "ueb_trail", 0.022f, R(0.5f, 0.8f), R(1.1f, 1.6f), grey, 0.55f, 0);
            }
            Skirt(p, 12, 0.6f, 4f, 7.5f, 0.9f, 1.4f, 1.5f, 2.2f, 0.8f);
            for (int i = 0; i < 4; i++) P("ueb_cauli", p + Up * R(0.6f, 2.4f) + Ball(0.7f), Up * R(2f, 4.6f) + Ring() * R(0f, 1.2f) + lean, R(2.6f, 3.8f), R(1.2f, 1.8f), grey);
            UeRingL(p, 3.6f, 0.28f, 0f, new Color(1.2f, 0.95f, 0.6f));
            Snd("exp_small", p, 1f, 0.9f + 0.045f * stage);
            if (stage % 3 == 1) Bank("exp2_sat", p, LegacyMatch.GainExplosion * 0.55f, 1f + 0.04f * stage, LegacyMatch.BankExplosion);
            if (last) Snd("exp_medium", p, 1f, 0.85f);
            sites.Add(p);
            end = t + 3.6f;
        }

        void ChainTick(float dt)
        {
            if (sites.Count == 0 || t > end - 1.0f) return;
            for (int i = Ticks(ref c0, dt, 0.11f); i > 0; i--)
            {   // a thin grey wisp over the hole of every pop
                var s = sites[rnd.Next(sites.Count)];
                P("ueb_haze", s + Up * R(0.8f, 3.5f) + Ball(0.7f), Up * R(0.8f, 1.8f) + lean, R(2.6f, 3.6f), R(1.5f, 2.2f), Color.Lerp(Dust(R(0f, 0.8f)), Grey(R(0f, 0.6f)), 0.5f), 0.45f);
            }
        }

        // ================================================================================================ 4 GEYSER
        /// <summary>Jets of thrown earth (GEYSER): each a string of soft heads launched together from one root along one line, the slow
        /// thick dark ones staying low, the fast small pale ones reaching the top, so the jet tapers; it bends a little, rises, hangs
        /// and falls back as dust. tilt0..tilt1 = the lean from straight up (radians), v0..v1 = the tip's speed, w0..w1 = the width.</summary>
        void EarthJets(Vector3 c, int n, float tilt0, float tilt1, float v0, float v1, float w0, float w1)
        {
            for (int j = 0; j < n; j++)
            {
                float tilt = R(tilt0, tilt1), vTop = R(v0, v1), w = R(w0, w1), dark = R(0.15f, 1f);
                var az = Ring(); var d = Up * Mathf.Cos(tilt) + az * Mathf.Sin(tilt);
                var root = c + az * R(0f, 1.2f) + Ring() * R(0f, 0.5f) + Up * 0.4f;
                var bend = Ring() * R(0.4f, 1.6f) + lean * 0.6f;
                int m = Mathf.Clamp(Mathf.RoundToInt(vTop * R(1.15f, 1.4f)), 8, 28);
                for (int i = 0; i < m; i++)
                {
                    float u = (i + R(0.15f, 0.85f)) / m;                                                 // 0 = the root, 1 = the tip
                    var v = d * (vTop * (0.2f + 0.8f * u)) + bend * (u * u) + Ball(0.3f);
                    float life = Mathf.Clamp(2f * v.y / 9.81f * R(0.8f, 0.95f), 0.5f, R(1.7f, 2.4f));
                    float size = w * Mathf.Lerp(1.5f, 0.5f, u) * R(0.85f, 1.15f);
                    var col = Color.Lerp(Earth(dark * (1f - 0.5f * u)), Dust(R(0.5f, 1f)), 0.55f * u * u);
                    P("ueb_jet", root + d * (u * 0.9f), v, life, size, col, Mathf.Lerp(1f, 0.82f, u), u < 0.35f ? R(0f, 0.08f) : 0f);
                }
                if ((j & 1) == 0)                                                                        // the tip breaks into clods that fall and lie
                    EmitL("m12_stone", root + d, d * (vTop * R(0.8f, 0.98f)) + Ball(0.9f), R(2.6f, 3.8f), R(0.25f, 0.55f), Color.white, 1f);
            }
        }

        void GeyserBeat(int stage, Vector3 at)
        {
            Flash(O + Up * 0.6f, 3.2f, 0.1f, new Color(1f, 0.7f, 0.4f));                                 // a dull glow only: this one is earth, not fire
            AddL(O + Up * 2f, new Color(1f, 0.7f, 0.45f), 22f, 14f, 0.25f);
            ScorchL(O, 3.4f, 2);
            Kick(O, 13f);
            // the fountain: jets and clumps of earth, each its own width, lean and height: thick and dark at the root, thinning to soft
            // dust at the top, blurred along their flight while fast; they rise, hang a moment and fall back as dust (no flat cards)
            EarthJets(O, 9, 0f, 0.16f, 8f, 12.5f, 1.25f, 1.85f);                                         // the heave itself: short thick clumps
            EarthJets(O, 20, 0.03f, 0.34f, 13.5f, 20f, 0.7f, 1.4f);                                      // the tall jets, 7 to 13 m up
            EarthJets(O, 8, 0.36f, 0.6f, 9.5f, 13f, 0.65f, 1.1f);                                        // the leaning ones, thrown outward
            for (int i = 0; i < 26; i++)                                                                 // clods: the stone chunks of the game, they land and lie
                EmitL("m12_stone", O + Up * R(0.6f, 1.4f) + Ball(0.5f), Ring() * R(2.5f, 8.5f) + Up * R(9f, 17f), R(2.6f, 4f), R(0.35f, 0.9f), Color.white, 1f);
            for (int i = 0; i < 9; i++)                                                                  // the big clods: a dust trail behind, a thud and a dust pop where they land
            {
                var cv = Ring() * R(4f, 8.5f) + Up * R(11f, 16.5f);
                EmitL("m12_stone", O + Up, cv, 4f, R(0.5f, 0.8f), Color.white, 1f);
                Launch(O + Up, cv, 9.81f, 4f, null, 0f, Color.white, "ueb_trail", 0.04f, R(0.6f, 0.9f), R(1.3f, 1.9f), Dust(R(0.3f, 0.9f)), 0.6f, 2);
            }
            for (int i = 0; i < 22; i++)
            {   // the heavy cloud of earth at the foot
                var d = Ring();
                P("ueb_dust", O + d * R(0.4f, 2.4f) + Up * R(0.6f, 2.2f), d * R(1.5f, 5f) + Up * R(1.5f, 6f) + lean, R(3.8f, 5.4f), R(2.2f, 3.2f), Color.Lerp(Dust(R(0.3f, 1f)), Earth(0.2f), 0.45f));
            }
            Skirt(O, 64, 1.2f, 13f, 19f, 1.7f, 2.5f, 3.2f, 4.8f, 1f);                                     // the thick dust skirt
            Snd("exp_medium", O, 1.1f, 0.58f);
            Bank("heavy", O, LegacyMatch.GainHeavy * 0.9f, 0.55f, LegacyMatch.BankHeavy);
            Snd("dst_debris_concrete", O, 1f, 0.9f, 0.15f);
            Snd("exp_debris_rain", O, 1f, 0.8f, 0.7f);
            Snd("dst_debris_settle", O, 0.9f, 0.9f, 1.7f);
            end = 6f;
        }

        void GeyserTick(float dt)
        {
            if (!f0 && t >= 0.25f) { f0 = true; Skirt(O, 44, 2f, 7f, 11f, 1.7f, 2.4f, 3.2f, 4.6f, 0.85f); }  // a second, slower roll of dust
            if (c4 == 0f && t >= 0.34f)
            {   // the ground gives a second time: a lower, wider heave of earth beside the first, its own thud
                c4 = 1f;
                var gp = O + Ring() * R(1.2f, 2.2f);
                EarthJets(gp, 5, 0f, 0.2f, 6.5f, 10f, 1.0f, 1.5f);
                EarthJets(gp, 8, 0.12f, 0.6f, 9f, 14f, 0.6f, 1.1f);
                for (int i = 0; i < 8; i++) P("ueb_dust", gp + Ring() * R(0.3f, 1.8f) + Up * R(0.5f, 1.6f), Ring() * R(1.5f, 4.5f) + Up * R(1.5f, 4.5f) + lean, R(3.4f, 4.8f), R(1.9f, 2.7f), Color.Lerp(Dust(R(0.3f, 1f)), Earth(0.2f), 0.45f));
                Skirt(gp, 20, 1f, 6f, 10f, 1.4f, 2.0f, 2.4f, 3.4f, 0.8f);
                Kick(O, 6f, false);
                Snd("exp_medium", gp, 0.8f, 0.5f);
                Snd("dst_debris_concrete", gp, 0.9f, 0.8f, 0.1f);
            }
            if (t < 1.2f)
                for (int i = Ticks(ref c0, dt, 0.035f); i > 0; i--)                                       // the dust dome over the hole
                    P("ueb_dust", O + Up * R(0.5f, 3f) + Ball(1.6f), Up * R(1.5f, 8f) + Ring() * R(0f, 2.2f) + lean, R(3.8f, 5.4f), R(2.0f, 3.0f), Color.Lerp(Dust(R(0.2f, 1f)), Earth(0.2f), 0.3f));
            if (t > 0.15f && t < 0.8f)
                for (int i = Ticks(ref c1, dt, 0.11f); i > 0; i--)                                        // more earth thrown after the first burst
                    EarthJets(O + Ball(0.9f), 1, 0.02f, 0.3f, 9f, 15f, 0.55f, 1.0f);
            if (t > 0.9f && t < 1.9f)
                for (int i = Ticks(ref c2, dt, 0.02f); i > 0; i--)
                {   // the earth comes down again: a rain of dirt, dust where it lands
                    var d = Ring() * R(0.5f, 7.5f);
                    P("ueb_jet", O + d + Up * R(5f, 11f), Up * R(-6f, -2f) + Ring() * R(0f, 1.2f), R(0.8f, 1.2f), R(0.3f, 0.6f), Color.Lerp(Earth(R(0f, 0.7f)), Dust(R(0.4f, 1f)), 0.4f), 0.75f);
                    if ((n0++ & 3) == 0) P("ueb_skirt", O + d * 1.05f + Up * 0.4f, d.normalized * R(0.5f, 2f) + Up * R(0.3f, 1f), R(1.6f, 2.4f), R(1.3f, 2.0f), Dust(R(0f, 0.7f)), 0.7f, R(0.5f, 0.9f));
                }
            if (t > 1.2f && t < 4.6f)
                for (int i = Ticks(ref c3, dt, 0.14f); i > 0; i--)                                        // the dust hangs over the hole and drifts
                    P("ueb_haze", O + Up * R(1f, 7f) + Ball(3f) + lean * t, Up * R(0.3f, 1f) + lean, R(3.0f, 4.4f), R(2.6f, 3.8f), Dust(R(0.2f, 0.9f)), 0.4f);
        }

        // ================================================================================================ 5 FLAK
        Vector3 FlakAt => O + Up * 9f;

        void FlakBeat(int stage, Vector3 at)
        {
            if (stage > 0)
            {   // a heavy fragment hits the ground (the small hit of the rules there): dirt kicked up, sparks, a burn mark
                Sparks(at + Up * 0.2f, 4, 4f, 9f, 0.2f, 0.4f);
                Skirt(at, 5, 0.2f, 2.5f, 4.5f, 0.7f, 1.0f, 0.9f, 1.4f, 0.8f);
                P("ueb_spray", at + Up * 0.5f, Up * R(3.5f, 5.5f), R(0.5f, 0.7f), R(1.1f, 1.6f), Earth(R(0.1f, 0.7f)));
                for (int i = 0; i < 5; i++) P("ueb_dirt", at + Up * 0.2f, (Up + Ring() * R(0f, 0.8f)).normalized * R(5f, 10f), R(0.4f, 0.7f), R(0.12f, 0.2f), Earth(R(0f, 1f)), 1f);
                AddL(at + Up * 0.5f, new Color(1f, 0.8f, 0.55f), 8f, 5f, 0.12f);
                ScorchL(at, 0.6f, 0);
                Snd("imp_shrapnel_tink", at, 1.5f, R(0.85f, 1.25f), 0.075f * n0 + R(0f, 0.03f));           // one after the other: every hit is heard
                n0++;
                return;
            }
            var B = FlakAt;
            Flash(B, 12f, 0.07f, new Color(1f, 0.96f, 0.86f));                                           // the burst: a hard white-orange flash, two frames
            Flash(B, 7f, 0.2f, new Color(1f, 0.62f, 0.25f));
            Glow(B, lean, 10f, 0.55f, FireGlow, 0.5f);                                                   // the hot heart glows on inside the cloud
            AddL(B, new Color(1f, 0.86f, 0.62f), 95f, 36f, 0.3f, 2.2f, 0f, true);                  // its light on the ground and on what stands under it
            AddL(B, new Color(1f, 0.55f, 0.25f), 30f, 24f, 1.0f, 2.2f, 0.5f);
            GroundL(O, 3.2f, false, true);
            Kick(O, 9f);
            for (int i = 0; i < 6; i++) P("ueb_fire", B + Ball(0.6f), Sphere() * R(2f, 7f), R(0.2f, 0.36f), R(2.6f, 3.8f), Color.white);                  // the dirty ball of fire: a few frames
            for (int i = 0; i < 10; i++) P("ueb_hot", B + Ball(0.8f), Sphere() * R(3f, 9f), R(0.5f, 0.85f), R(2.2f, 3.2f), Soot(R(0f, 0.5f)));
            // the flak cloud: dense, dark grey to black, ragged: thrown out in five uneven lobes, heavy, it hangs, drifts and thins slowly
            var lobes = new Vector3[5];
            for (int i = 0; i < lobes.Length; i++) { var ld = Sphere(); ld.y = ld.y * 0.6f + 0.15f; lobes[i] = ld.normalized * R(0.55f, 1.3f); }
            for (int i = 0; i < 30; i++)
            {
                var ld = lobes[i % lobes.Length]; float far = R(0.15f, 1f);
                P(FlakDust, B + ld * (far * 1.6f) + Ball(0.7f), ld * (R(3f, 9f) * far) + Sphere() * R(0.3f, 1.6f) + lean * 1.2f, R(4.2f, 6.2f) * q, R(1.7f, 2.9f) * (1.15f - 0.4f * far), Color.Lerp(new Color(0.08f, 0.08f, 0.085f), new Color(0.24f, 0.24f, 0.24f), R(0f, 1f)));   // 1.5 A38 (kit air_burst_smoke_fade_r11): in game its short life (View/OzUeBlastFx.Quick.cs)
            }
            for (int i = 0; i < 9; i++)                                                                  // its heart: heads born as fire, the fire draws back into their creases
                P(FlakCauli, B + Ball(0.9f), Sphere() * R(0.6f, 2.6f) + lean * 1.2f, R(3.6f, 5.0f) * q, R(1.8f, 2.5f), Color.Lerp(new Color(0.07f, 0.07f, 0.075f), new Color(0.17f, 0.17f, 0.17f), R(0f, 1f)));   // 1.5 A38: in game its short life
            // the pressure of the burst: a faint pale bubble in the air for a few frames, a ring over the ground, the dust under it
            // pressed flat and pushed outward
            P("ueb_haze", B, Vector3.zero, 0.2f, 7.5f, new Color(0.7f, 0.7f, 0.72f), 0.16f);
            UeRingL(O, 12f, 0.3f, 0.03f, new Color(0.75f, 0.68f, 0.56f));
            Skirt(O, 30, 2.5f, 8f, 13f, 1.2f, 1.7f, 1.1f, 1.8f, 0.5f);
            // the fragments: many small dark pieces thrown out unevenly, in sprays, most of them down and outward; fast, gone in half a
            // second; each draws a thin grey thread that fades quickly and kicks the ground where it hits
            int sprays = 8;
            for (int s = 0; s < sprays; s++)
            {
                float az0 = R(0f, TAU), el0 = R(-1.2f, -0.4f); int cnt = 4 + rnd.Next(8);
                for (int i = 0; i < cnt; i++)
                {
                    float a = az0 + R(-0.4f, 0.4f), el = el0 + R(-0.28f, 0.28f), sp = R(26f, 48f);
                    var v = Az(a) * (Mathf.Cos(el) * sp) + Up * (Mathf.Sin(el) * sp);
                    var p0 = B + v.normalized * R(0.3f, 1.4f);
                    Launch(p0, v, 9.81f, FallTime(v.y, p0.y - O.y) + 0.06f, "ueb_dirt", R(0.1f, 0.18f), new Color(0.1f, 0.095f, 0.09f), "ueb_trail", 0.008f, R(0.36f, 0.52f), R(0.4f, 0.7f), new Color(0.5f, 0.5f, 0.51f), 0.65f, 5);
                    if (i % 3 == 0) P("ueb_shard", p0, v, R(0.1f, 0.18f), R(0.06f, 0.09f), new Color(0.9f, 0.42f, 0.14f), 0.6f);     // the hot ones glint dull orange for a moment
                }
            }
            for (int i = 0; i < 6; i++)                                                                  // a few fly level or a little up: slower, they fall short
            {
                float el = R(-0.3f, 0.4f), sp = R(14f, 27f);
                var v = Ring() * (Mathf.Cos(el) * sp) + Up * (Mathf.Sin(el) * sp);
                Launch(B + v.normalized * R(0.3f, 1.2f), v, 9.81f, Mathf.Min(1.3f, FallTime(v.y, B.y - O.y) + 0.06f), "ueb_dirt", R(0.1f, 0.16f), new Color(0.1f, 0.095f, 0.09f), "ueb_trail", 0.012f, R(0.34f, 0.5f), R(0.4f, 0.65f), new Color(0.5f, 0.5f, 0.51f), 0.6f, 5);
            }
            for (int i = 0; i < 3; i++)                                                                  // three burning chunks: they arc down, bounce and burn out
            {
                float el = R(-0.2f, 0.7f), sp = R(7f, 13f);
                Launch(B + Ball(0.5f), Ring() * (Mathf.Cos(el) * sp) + Up * (Mathf.Sin(el) * sp), 9.81f, 4f, null, 0f, Color.white, "ueb_fire", 0.04f, R(0.55f, 0.75f), 0.22f, Color.white, 1f, 6);
            }
            Bank("explosion", B, LegacyMatch.GainExplosion * 1.1f, 1.18f, LegacyMatch.BankExplosion);       // a sharp crack high up, its boom, then the rain of fragments
            Snd("exp_medium", B, 1f, 1.3f);
            Bank("heavy", B, LegacyMatch.GainHeavy * 0.55f, 0.82f, LegacyMatch.BankHeavy, 0.07f);
            Snd("exp_debris_rain", O, 0.9f, 1.25f, 0.28f);
            end = 6f * q;                                                                                // 1.5 A38: in game the fog shape goes with its short cloud
        }

        /// <summary>Seconds until a piece thrown with the upward speed vy from h metres over the ground is down.</summary>
        static float FallTime(float vy, float h) => (vy + Mathf.Sqrt(vy * vy + 19.62f * Mathf.Max(0f, h))) / 9.81f;

        void FlakTick(float dt)
        {
            if (t < 0.7f)
                for (int i = Ticks(ref c0, dt, 0.08f); i > 0; i--)                                        // the heart burns on inside the cloud for a moment: fire seen through its creases
                    P("ueb_hot", FlakAt + lean * (t * 1.2f) + Ball(0.8f), Sphere() * R(0.4f, 1.8f) + lean, R(0.5f, 0.8f), R(1.5f, 2.1f), Soot(R(0f, 0.4f)));
            if (t > 0.5f && t < 0.5f + 3.3f * q)                                                          // 1.5 A38: in game the wisps stop sooner (3.8 s as made)
                for (int i = Ticks(ref c1, dt, 0.13f); i > 0; i--)                                        // the cloud drifts and frays: dark wisps torn off down wind
                    P(FlakHaze, FlakAt + lean * (t * 1.3f) + Ball(2.2f + 0.4f * t), lean * 1.2f + Up * R(-0.3f, 0.3f), R(2.6f, 3.6f) * q, R(2.3f, 3.3f), Soot(R(0f, 0.6f)), 0.5f);   // 1.5 A38: in game its short life
        }

        // ================================================================================================ 6 SHOCK
        static readonly Color Cyan = new Color(0.55f, 0.85f, 1f);
        int n2;                                                                                         // SHOCK: pulses of the centre's own light
        const float ShockRingM = 18f, ShockRingSec = 0.55f;

        /// <summary>At the drop, 0.3 s before the first beat of the rules: light streaks pulled in to the point, a growing blue core, a rising hum.</summary>
        void ShockCharge()
        {
            var c = O + Up * 1.4f;
            for (int i = 0; i < 30; i++)
            {
                var p = O + Ring() * R(7f, 10.5f) + Up * R(0.4f, 3.2f);
                P("ueb_shard", p, (c - p) / 0.29f, 0.29f, R(0.08f, 0.13f), Cyan);
            }
            for (int i = 0; i < 6; i++) P("ueb_plasma", c + Ball(0.3f), Vector3.zero, 0.34f - i * 0.04f, 1.2f + 0.5f * i, Color.white, 1f, i * 0.04f);
            AddL(c, Cyan, 22f, 16f, 0.35f);
            Snd("electro_deploy", O, 1f, 1.25f);
        }

        void ShockBeat(int stage, Vector3 at)
        {
            if (stage > 0)
            {   // the second and third step of the wave (the wider rings of the rules)
                GroundL(O, 2f + stage, true);
                Snd("electro_spark", O, 0.8f, R(0.9f, 1.1f));
                return;
            }
            Flash(O + Up * 1.5f, 13f, 0.12f, new Color(0.8f, 0.93f, 1f));
            AddL(O + Up * 3f, new Color(0.6f, 0.85f, 1f), 110f, 40f, 0.5f, 2.2f, 0f, true);
            GroundL(O, 4.5f, true);
            ScorchL(O, 2.6f, 2);
            Kick(O, 12f);
            for (int i = 0; i < 14; i++)
            {   // the electric flash of the start: a blue-white dome that bursts out and is gone in a third of a second
                var d = Sphere(); d.y = Mathf.Abs(d.y) * 0.8f;
                P("ueb_ion", O + Up * 1.2f + d * R(0f, 1.2f), d * R(5f, 17f), R(0.26f, 0.4f), R(2.6f, 3.8f), Dust(R(0.3f, 0.9f)));
            }
            for (int i = 0; i < 6; i++) P("ueb_plasma", O + Up * 1.5f + Ball(0.6f), Sphere() * R(1f, 4f), R(0.22f, 0.36f), R(3.0f, 4.4f), Color.white);      // its white heart
            // the centre belongs to the wave: a short hot core, then a heavy head of dust and smoke rising from the middle (ShockTick)
            Flash(O + Up * 1.6f, 9f, 0.22f, new Color(1f, 0.74f, 0.42f));
            Glow(O + Up * 2.2f, Up * 4f, 15f, 0.8f, FireGlow, 0.5f, 0.06f);
            Glow(O + Up * 4f, Up * 4f, 12f, 1.1f, FireDeep, 0.4f, 0.3f);
            AddL(O + Up * 2.5f, new Color(1f, 0.62f, 0.3f), 46f, 26f, 0.9f, 2.2f, 0.5f);
            for (int i = 0; i < 9; i++)
            {
                var d = Sphere(); d.y = Mathf.Abs(d.y);
                P("ueb_hot", O + Up * 1.2f + d * R(0f, 1.3f), d * R(3f, 9f) + Up * R(2f, 9f), R(0.9f, 1.3f), R(2.4f, 3.4f), Burnt(R(0f, 1f)), 1f, 0.07f);
            }
            for (int i = 0; i < 8; i++)
            {
                var d = Ring();
                P("ueb_fire", O + d * R(0.3f, 1.6f) + Up * R(0.3f, 1.2f), d * R(1f, 4f) + Up * R(5f, 11f), R(0.45f, 0.8f), R(2.6f, 3.8f), Color.white, 1f, 0.07f);
            }
            UeRingL(O, ShockRingM, ShockRingSec, 0f, new Color(1.1f, 1.5f, 1.9f));                      // the shock ring, its violet echo, a fast white inner ring
            UeRingL(O, ShockRingM * 0.8f, 0.72f, 0.06f, new Color(0.45f, 0.4f, 0.9f));
            UeRingL(O, ShockRingM * 0.5f, 0.34f, 0f, new Color(1.3f, 1.3f, 1.3f));
            for (int i = 0; i < 40; i++) P("m12_pop", O + Up * 0.4f, Ring() * R(14f, 30f) + Up * R(0f, 3f), R(0.25f, 0.5f), R(0.08f, 0.14f), Cyan);
            Snd("electro_death", O, 1.1f, 0.7f);
            Snd("exp_large", O, 1f, 0.8f);
            Bank("heavy", O, LegacyMatch.GainHeavy * 0.8f, 0.5f, LegacyMatch.BankHeavy, 0.25f);
            Snd("dst_debris_settle", O, 0.7f, 1.15f, 0.45f);                                                // the dust the wave threw up comes down
            c4 = R(0f, TAU);
            end = 5.6f;
        }

        void ShockTick(float dt)
        {
            if (t < ShockRingSec)
            {   // the wall of dust behind the ring: a closed band of rolling heads born on the front and carried with it, thinning behind;
                // blue sparks skip ahead of it. One new row of heads for every 1.7 m the front travels, a head every 1.25 m along it
                float r = ShockRingM * (0.06f + 0.94f * t / ShockRingSec), speed = ShockRingM * 0.94f / ShockRingSec;
                int rows = Mathf.Clamp(Mathf.CeilToInt(speed * dt / 1.7f), 1, 3);
                for (int row = 0; row < rows; row++)
                {
                    float rr = r - row * speed * dt / rows;
                    int n = Mathf.Clamp(Mathf.CeilToInt(TAU * rr / 1.25f), 8, 92);
                    c4 += 2.399963f;
                    for (int i = 0; i < n; i++)
                    {
                        var d = Az(c4 + (i + R(-0.3f, 0.3f)) * TAU / n); var p = O + d * (rr * R(0.96f, 1.02f));
                        P("ueb_wall", p + Up * R(0.5f, 1.7f), d * (speed * R(0.32f, 0.5f)) + Up * R(0.6f, 2.6f), R(0.5f, 0.75f) * (0.55f + 0.6f * rr / ShockRingM), R(2.3f, 3.3f), Dust(R(0f, 0.65f)));
                        if (i % 5 == 0) P("ueb_haze", p + Up * 0.8f, d * R(2f, 4f) + Up * R(0.3f, 1.2f), R(2.2f, 3.2f), R(3.0f, 4.4f), Dust(R(0.1f, 0.6f)), 0.26f);
                        if (i % 3 == 0) P("m12_pop", p + Up * 0.2f, d * R(4f, 9f) + Up * R(1f, 4f), R(0.2f, 0.4f), 0.1f, Cyan);
                    }
                }
            }
            if (t > 0.1f && t < 1.3f)
                for (int i = Ticks(ref c1, dt, 0.06f); i > 0; i--)
                {   // the crackle left over the ground
                    var p0 = O + Ring() * R(1f, 8f) + Up * R(0.2f, 2.5f);
                    for (int k = 0; k < 3; k++) { var d = Sphere(); P("m12_bolt", p0, d * R(6f, 14f), R(0.05f, 0.09f), R(0.12f, 0.2f), new Color(0.7f, 0.9f, 1f)); p0 += d * 0.8f; }
                    if ((n0++ & 3) == 0) AddL(p0, Cyan, 8f, 6f, 0.1f);
                    if (n0 % 4 == 1 && n1++ < 5) Snd("electro_spark", p0, 0.7f, R(0.9f, 1.25f));
                }
            if (t > 0.06f && t < 1.9f)
                for (int i = Ticks(ref c2, dt, t < 0.8f ? 0.024f : 0.045f); i > 0; i--)
                {   // the head of dust and smoke out of the middle: heavy, in the wave's own dust, darker and greyer as it rises; the fire
                    // lights it from inside. As wide as a third of the wave, about 12 m tall: the centre and the ring are one event
                    float k = Mathf.Clamp01(t / 1.9f), up = Mathf.Lerp(2f, 19f, Mathf.Pow(R(0f, 1f), 1.3f)) * (1f - 0.45f * k);
                    var tone = Color.Lerp(Dust(R(0.05f, 0.8f)), Soot(R(0.4f, 0.9f)), 0.4f * k * R(0f, 1f));
                    P(R(0f, 1f) < 0.45f ? "ueb_cauli" : "ueb_dust", O + Ring() * R(0f, 2.4f) + Up * R(0.8f, 2.6f), Up * up + Ring() * R(0f, 2.6f) + lean, R(3.8f, 5.6f), R(2.5f, 3.8f), tone);
                }
            if (t > 0.06f && t < 0.7f)
                for (int i = Ticks(ref c3, dt, 0.09f); i > 0; i--)
                {   // the fire inside it: balls of fire climbing through the heads, flames at the foot
                    P("ueb_hot", O + Up * R(0.8f, 2.6f) + Ball(1.0f), Up * R(4f, 15f) * (1f - 0.4f * t) + Ring() * R(0f, 1.4f), R(1.2f, 1.8f), R(2.0f, 2.9f), Burnt(R(0f, 1f)));
                    P("ueb_fire", O + Up * R(0.4f, 3.5f) + Ball(1.0f), Up * R(4f, 8f), R(0.45f, 0.75f), R(1.8f, 2.8f) * (1f - 0.4f * t), Color.white);
                }
            while (n2 < 3 && t >= 0.25f + 0.25f * n2)
            {   // its light and its halo for as long as it burns
                n2++;
                var lp = O + Up * R(2.5f, 6f) + Ball(1f);
                AddL(lp, new Color(1f, 0.6f, 0.28f), 24f * (1f - n2 / 6f), 15f, 0.5f, 2.2f, 0.5f);
                Glow(lp + Up * n2, Up * 3f, 11f * (1f - n2 / 7f), 0.55f, FireDeep, 0.5f);
            }
            if (t > 1.5f && t < 4.4f)
                for (int i = Ticks(ref c0, dt, 0.11f); i > 0; i--)                                        // the head stands and drifts: a thin veil over it
                    P("ueb_haze", O + Up * R(1f, 11f) + Ball(2.4f) + lean * (t * 1.1f), Up * R(0.4f, 1.1f) + lean, R(3.0f, 4.2f), R(2.6f, 3.6f), Color.Lerp(Dust(R(0.3f, 0.9f)), Soot(0.5f), 0.35f), 0.42f);
        }
    }
}
