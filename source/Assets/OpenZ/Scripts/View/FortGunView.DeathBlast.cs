// OpenZ (GPL-3.0)
using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// View only: the rules, the gun's health, the toss path, its
    /// landing, the trees under it and the break-up are untouched.
    ///
    /// Why run 9's MegaBlast (FortGunView.MegaBlast.cs, kept as it is) did not read: its fireball is the table blast of a BUILDING (s 2.15,
    /// about a heavy tank's death, the size of the fort's own shells landing next to it), and a building blast is never an air burst, so its
    /// ground light, shock rings, outer / echo rings and LEG fire clouds are laid on the terrain UNDER the fort's roof, inside the fort; its
    /// point (0.8 m over the ring foot) is inside the turret head, which rises along that same line from the next frame; its "exp_fort"
    /// sound id is no event of the sound table (AudioDirector drops it, and with the director up the CombatAudio fallback is skipped), so
    /// only the heavy bank's one boom plays, the same boom as every heavy shell; its camera shake asks the shared gate without "always",
    /// so the killing shells' own kicks of the last 2.5 s hold it back.
    ///
    /// Now, on the frame the cannon is thrown off a STANDING fort (StartToss, once per cannon; a cannon that goes with its fort is left to
    /// the fort's fall: the nuke strike or the collapse, and run 9's blast, as before):
    /// <list type="bullet">
    /// <item>the camera first: one strong kick (<see cref="DeathKickPx"/> px at the 1080 px reference, OpenZFxShake.Kick: on screen only,
    /// smaller the further the camera is, the SCREEN SHAKE and CALM SCREEN settings decide, not held back by the gate's pause) and an
    /// after-kick of <see cref="DeathAfterKickPx"/> px at <see cref="DeathAfterKickAt"/> s; under a second in all (the nuke's rumble is
    /// 14 px over 3.6 s, the fort's death 8.6 px over 3.4 s);</item>
    /// <item>the second explosion type's BIG look (OpenZFxPlayer.Exp2Air, Exp2Kind.Big, the air form: nothing of it on the ground under
    /// the roof) sized <see cref="DeathBlastU"/> m (the heavy tank's is 4.8, the shuttle's 6.2-6.8), its foot on the roof at the turret
    /// ring, so the white core and the orange body come up round the cannon on the frame it is thrown: white core, orange billows,
    /// red halo, spikes, hundreds of sparks, embers, dark metal debris, maroon smoke and its orange light, with its own fire roar,
    /// debris rain, shrapnel tinks and metal landings;</item>
    /// <item>under it the table blast of a big vehicle (s <see cref="DeathBlastTableS"/>, the heavy tank's 2.1; up on the roof it is the
    /// air-burst form: dark smoke, sparks, embers, fire clouds round it);</item>
    /// <item><see cref="DeathChunks"/> metal chunks thrown out over the walls, <see cref="DeathSecondaries"/> burning pockets bursting on
    /// the roof round the fireball in its first half second (exp2 Small, each with the medium blast's satellite boom), roof fire for
    /// <see cref="DeathRoofFireSec"/> s (the wrecked mount's own fire and smoke, FortGunMount, go on after it), a dark smoke column for
    /// <see cref="DeathColumnSec"/> s, and fire then smoke trailing the flying cannon (<see cref="DeathTrailFireSec"/> /
    /// <see cref="DeathTrailSmokeSec"/> s): it reads as thrown by the blast;</item>
    /// <item>the sound: the heavy vehicle death boom pitched down for the bigger gun (the "vdeath" heavy bank) and,
    /// <see cref="DeathRumbleDelay"/> s later, the deep rumble of the big shells (the heavy bank pitched to
    /// <see cref="DeathRumblePitch"/>), on top of the toss whoosh and run 9's heavy boom.</item>
    /// </list>
    /// Not the nuke: no white-out, no mushroom, no ground shock dome, no nuke sound, no long rumble. Switch <see cref="DeathBlastOn"/>
    /// (off = as before this kit). Hook: one line in StartToss (FortGunView.cs), before the toss sound and run 9's MegaBlast.
    /// </summary>
    public partial class FortGunView
    {
        /// <summary>Switch: the massive blast of a cannon shot off its standing fort (on). Off = run 9's blast alone, as before.</summary>
        public static bool DeathBlastOn = true;
        /// <summary>The exp2 Big size unit (m); the fireball's foot over the ring foot (m); the table blast's M12 strength and LEG radius.</summary>
        public static float DeathBlastU = 7.2f, DeathBlastBaseLift = 0.25f, DeathBlastTableS = 2.6f, DeathBlastTableRadius = 6.5f;
        /// <summary>The boom (x the heavy bank's gain, its pitch), the rumble (x the heavy bank's gain, its pitch, its delay s).</summary>
        public static float DeathBoomGain = 1f, DeathBoomPitch = 0.82f, DeathRumbleGain = 0.85f, DeathRumblePitch = 0.68f, DeathRumbleDelay = 0.12f;
        /// <summary>The camera: the kick and the after-kick (px at the 1080 px reference), when the after-kick comes (s).</summary>
        public static float DeathKickPx = 26f, DeathAfterKickPx = 13f, DeathAfterKickAt = 0.22f;
        /// <summary>The burning pockets: how many, when (s), their size (x U), how far out on the roof (x U).</summary>
        public static int DeathSecondaries = 3;
        public static float DeathSecondaryFrom = 0.1f, DeathSecondaryTo = 0.55f, DeathSecondaryU = 0.34f, DeathSecondaryOut0 = 0.35f, DeathSecondaryOut1 = 0.75f;
        /// <summary>Metal chunks thrown out of the blast.</summary>
        public static int DeathChunks = 12;
        /// <summary>Smoke column (s, puff every s, climb m/s), roof fire (s), fire / smoke trailing the flying cannon (s).</summary>
        public static float DeathColumnSec = 6f, DeathColumnEvery = 0.09f, DeathColumnRise = 1.8f, DeathRoofFireSec = 2.5f, DeathTrailFireSec = 0.55f, DeathTrailSmokeSec = 2.0f;

        // ------------------------------------------------------------------ counts (the Unity proof reads them: DeathBlastReport)
        public static int DeathBlasts, DeathBlastExp2, DeathBlastFallbacks, DeathBlastLeftToFort, DeathBlastKicks, DeathBlastAfterKicks, DeathBlastBooms, DeathBlastRumbles,
            DeathBlastSecondaries, DeathBlastChunks, DeathBlastColumnPuffs, DeathBlastRoofFlames, DeathBlastTrailPuffs;
        public static float LastDeathBlastAt = -100f, LastDeathBlastU;
        public static Vector3 LastDeathBlastPos;
        public static bool LastDeathBlastKick, LastDeathBlastBoom, LastDeathBlastExp2;

        /// <summary>StartToss (FortGunView.cs), on the frame the cannon is thrown: <paramref name="at"/> = tossFrom, the turret ring's foot
        /// on the roof, the cannon still seated there in this frame.</summary>
        void DeathBlast(Vector3 at)
        {
            if (!DeathBlastOn || Fort == null) return;
            if (FortFallAtGameOver.Fallen(Fort) && (!outroBlast || GeyserOnlyStanding)) { DeathBlastLeftToFort++; return; }   // with its fort: the fort's fall owns the moment (run 9's blast plays as before); 1.5 outro_megacannon_blast: not when the ending's beat blows it off (View/FortGunView.Outro.cs); 1.5 A33 (kit megacannon_blast_split_r10): with GeyserOnlyStanding on, the ending's beat too (the 1.4 blast there, no DeathBlast)
            var z = OpenZFxPlayer.I; bool live = z && z.isActiveAndEnabled;
            float U = DeathBlastU;
            var foot = at + Vector3.up * DeathBlastBaseLift;                                         // the fireball's foot: the roof at the turret ring
            var centre = foot + Vector3.up * (0.45f * 1.18f * U);                                    // Exp2 puts the fireball's base 0.45 U (x its own size spread, up to 1.18) under its point
            var rnd = new System.Random(Fort.Id * 7919 + DeathBlasts * 131 + 571);
            float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);

            // 1. the camera first: it claims the shake gate over the blasts below and the killing shell's own kick (CombatView runs after us)
            var sh = OpenZFxShake.I;
            LastDeathBlastKick = sh != null && DeathKickPx > 0f && sh.Kick(foot, DeathKickFor(DeathKickPx, false), "fortgun_megablast", true);   // 1.5 megacannon_shake
            if (LastDeathBlastKick) DeathBlastKicks++;

            // 2. the table blast of a big vehicle (up on the roof: the air-burst form) and the exp2 Big fireball round the cannon
            var spec = new LegacyMatch.BlastSpec
            {
                label = "fort mega cannon", kind = FxBlastKind.Vehicle, s = DeathBlastTableS, legRadius = DeathBlastTableRadius,
                key = "vdeath", clips = LegacyMatch.BankHeavy, gain = LegacyMatch.GainHeavy, pitch = DeathBoomPitch
            };
            LegacyMatch.DoBlast(foot + Vector3.up * 1.2f, spec, Fort.Id * 7919 + 61, FxManager.I);
            LastDeathBlastExp2 = live && z.Exp2Air(centre, U, Exp2Kind.Big, Fort.Id * 613 + 47, spec.s, spec.kind, true);
            if (LastDeathBlastExp2) DeathBlastExp2++;
            else { DeathBlastFallbacks++; FxManager.I?.Explosion(foot + Vector3.up * 2f, 3.2f); }   // no exp2 (the fx player off or not built): a big legacy blast in its place

            // 3. metal chunks thrown out over the walls (they land with the exp2 Big's own metal landings and run 9's debris rain)
            int chunks = 0;
            if (live)
                for (int i = 0; i < DeathChunks; i++)
                {
                    float a = i * 2.399963f + R(-0.3f, 0.3f), h = R(5f, 11f);
                    var p = foot + new Vector3(Mathf.Cos(a) * R(0.4f, 1.4f), R(0.8f, 2.0f), Mathf.Sin(a) * R(0.4f, 1.4f));
                    z.EmitChannel("m12_metal", p, new Vector3(Mathf.Cos(a) * h, R(6f, 11f), Mathf.Sin(a) * h), R(3.5f, 6f), R(0.35f, 0.9f), Color.white, 1f);
                    chunks++;
                }
            DeathBlastChunks += chunks;

            // 4. the sound
            DeathBooms(foot);

            // 5. what follows: the after-kick, the burning pockets, roof fire, the smoke column, the trail behind the flying cannon
            var after = FxRoot().gameObject.AddComponent<DeathBlastAfter>();
            Vector3 gunLocal = Vector3.up;
            if (yaw) { var b = WorldBounds(yaw); gunLocal = yaw.InverseTransformPoint(b.center); }
            after.Begin(foot, U, Fort.Id, yaw, gunLocal, rnd.Next());
            DeathShakeTail(foot);                                                                    // 1.5 megacannon_shake: the short rumble tail after the kicks (View/FortGunView.DeathShake.cs)

            DeathBlasts++; LastDeathBlastAt = Time.time; LastDeathBlastPos = foot; LastDeathBlastU = U;
            Debug.Log($"[OpenZ] fort #{Fort.Id} ({Fort.Owner}) mega cannon blown off its standing fort: exp2 Big {(LastDeathBlastExp2 ? "air" : "NOT played")} U {U:F1} m at {foot}, " +
                      $"table blast s {spec.s:F1}, kick {LastDeathBlastKick}, boom {LastDeathBlastBoom}, {DeathSecondaries} burning pockets, {chunks} chunks");
        }

        /// <summary>The heavy vehicle death boom now (pitched down: a bigger gun), the big shells' deep rumble a moment later.</summary>
        static void DeathBooms(Vector3 at)
        {
            LastDeathBlastBoom = false;
            var au = CombatAudio.I; if (!au) return;
            int p0 = au.Played;
            au.PlayPitched("vdeath", at, LegacyMatch.GainHeavy * DeathBoomGain, 0f, -1, DeathBoomPitch, LegacyMatch.BankHeavy);
            LastDeathBlastBoom = au.Played > p0; if (LastDeathBlastBoom) DeathBlastBooms++;
            void Rumble()
            {
                var a = CombatAudio.I; if (!a) return;
                int q0 = a.Played;
                a.PlayPitched("heavy", at, LegacyMatch.GainHeavy * DeathRumbleGain, 0f, -1, DeathRumblePitch, LegacyMatch.BankHeavy);
                if (a.Played > q0) DeathBlastRumbles++;
            }
            var ad = AudioDirector.I;
            if (ad && DeathRumbleDelay > 0f) ad.Schedule(DeathRumbleDelay, Rumble); else Rumble();
        }

        // ------------------------------------------------------------------ the Unity proof (Play mode; an editor script or the console)
        /// <summary>Throws the cannon of the index-th standing fort as if it were shot off (TestToss: the view only, the rules' gun stays alive).
        /// False when there is no such fort.</summary>
        public static bool DeathBlastTest(int index = 0)
        {
            int k = 0;
            for (int i = 0; i < all.Count; i++)
            {
                var v = all[i];
                if (!v || !v.Bound || v.tossed || v.Fort == null || v.Fort.Destroyed) continue;
                if (k++ == index) { v.TestToss(); return true; }
            }
            return false;
        }

        /// <summary>One line of every count of this kit (and of run 9's blast and the toss), for the proof.</summary>
        public static string DeathBlastReport() =>
            $"fort gun death blast: on {DeathBlastOn}, blasts {DeathBlasts} (exp2 Big {DeathBlastExp2}, fallbacks {DeathBlastFallbacks}, left to the fort's fall {DeathBlastLeftToFort}), " +
            $"kicks {DeathBlastKicks} + after-kicks {DeathBlastAfterKicks} (shake setting {(OzShakeGate.SettingOn ? "on" : "off")}), booms {DeathBlastBooms}, rumbles {DeathBlastRumbles}, " +
            $"pockets {DeathBlastSecondaries}, chunks {DeathBlastChunks}, column puffs {DeathBlastColumnPuffs}, roof flames {DeathBlastRoofFlames}, trail puffs {DeathBlastTrailPuffs}; " +
            $"last at t {LastDeathBlastAt:F2} {LastDeathBlastPos} U {LastDeathBlastU:F1} exp2 {LastDeathBlastExp2} kick {LastDeathBlastKick} boom {LastDeathBlastBoom}; " +
            $"older blasts {MegaBlasts}, tosses {Tosses}";

        // ------------------------------------------------------------------ after the blast
        sealed class DeathBlastAfter : MonoBehaviour
        {
            Vector3 foot; float U, t, colT, roofT, trailT, end; int id, si; bool afterKicked;
            Transform gun; Vector3 gunLocal; System.Random rnd;
            readonly List<(float at, Vector3 pos)> secs = new List<(float, Vector3)>();

            float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);

            internal void Begin(Vector3 roofFoot, float u, int fortId, Transform gunT, Vector3 gunCentreLocal, int seed)
            {
                foot = roofFoot; U = u; id = fortId; gun = gunT; gunLocal = gunCentreLocal; rnd = new System.Random(seed);
                end = Mathf.Max(Mathf.Max(DeathColumnSec, DeathRoofFireSec), Mathf.Max(DeathTrailSmokeSec, DeathAfterKickAt));
                float a0 = R(0f, 6.2831853f);
                int n = Mathf.Max(0, DeathSecondaries);
                for (int i = 0; i < n; i++)
                {
                    float a = a0 + i * 6.2831853f / Mathf.Max(1, n) + R(-0.5f, 0.5f), r = R(DeathSecondaryOut0, DeathSecondaryOut1) * U;
                    secs.Add((R(DeathSecondaryFrom, DeathSecondaryTo), foot + new Vector3(Mathf.Cos(a) * r, R(0.5f, 2.5f), Mathf.Sin(a) * r)));
                }
                secs.Sort((x, y) => x.at.CompareTo(y.at));
            }

            void Update()
            {
                var g = ZGame.I; float dt = g && g.paused ? 0f : Time.deltaTime;
                if (dt <= 0f) return;
                t += dt;
                if (!afterKicked && t >= DeathAfterKickAt)
                {
                    afterKicked = true;
                    var sh = OpenZFxShake.I;
                    if (sh != null && DeathAfterKickPx > 0f && sh.Kick(foot, DeathKickFor(DeathAfterKickPx, true), "fortgun_megablast_after", true)) DeathBlastAfterKicks++;   // 1.5 megacannon_shake
                }
                var z = OpenZFxPlayer.I; bool live = z && z.isActiveAndEnabled;
                while (si < secs.Count && t >= secs[si].at)
                {   // a burning pocket bursts on the roof, with the medium blast's satellite boom
                    var p = secs[si].pos; si++;
                    if (live && z.Exp2Air(p, U * DeathSecondaryU, Exp2Kind.Small, id * 41 + 900 + si, 1f, FxBlastKind.Vehicle, true)) DeathBlastSecondaries++;
                    var au = CombatAudio.I; if (au) au.PlayPitched("exp2_sat", p, LegacyMatch.GainExplosion * 0.5f, 0.03f, -1, 1.0f + 0.1f * (float)rnd.NextDouble(), LegacyMatch.BankExplosion);
                }
                if (live)
                {
                    if (t < DeathColumnSec && (colT -= dt) <= 0f)
                    {   // the dark column: puffs climbing from the roof, wider and slower as it rises
                        colT = DeathColumnEvery;
                        float k = t / DeathColumnSec;
                        var at = foot + Vector3.up * (1.0f + DeathColumnRise * t * 0.8f) + new Vector3(R(-1f, 1f), R(-0.3f, 0.6f), R(-1f, 1f)) * (0.18f * U);
                        z.EmitChannel("leg_smoke", at, new Vector3(R(-0.25f, 0.25f), DeathColumnRise * R(0.8f, 1.2f), R(-0.25f, 0.25f)),
                            R(3.2f, 4.4f), R(2.8f, 4.4f) * Mathf.Lerp(1f, 1.7f, k), R(0f, 1f) < 0.6f ? OpenZFxCatalog.WreckSmokeDark : OpenZFxCatalog.WreckBurnSmoke, 0.9f);
                        DeathBlastColumnPuffs++;
                    }
                    if (t < DeathRoofFireSec && (roofT -= dt) <= 0f)
                    {   // fire on the roof where the cannon stood, dying down into the wrecked mount's own fire
                        roofT = 0.07f;
                        float k = 1f - 0.6f * t / DeathRoofFireSec;
                        float a = R(0f, 6.2831853f), r = R(0f, 0.25f) * U;
                        var at = foot + new Vector3(Mathf.Cos(a) * r, R(0.3f, 1.2f), Mathf.Sin(a) * r);
                        float c = R(0f, 1f);
                        z.EmitChannel("leg_fire", at, new Vector3(R(-0.3f, 0.3f), R(1.6f, 2.6f), R(-0.3f, 0.3f)), R(0.45f, 0.7f), R(2.4f, 3.6f) * k,
                            c < 0.4f ? OpenZFxCatalog.WreckFlameB : c < 0.8f ? OpenZFxCatalog.WreckFlameC : OpenZFxCatalog.WreckFlameA, 1f);
                        DeathBlastRoofFlames++;
                    }
                    if (gun && gun.gameObject.activeInHierarchy && t < DeathTrailSmokeSec && (trailT -= dt) <= 0f)
                    {   // fire, then smoke, trailing the flying cannon: it was thrown by the blast
                        trailT = 0.04f;
                        var at = gun.TransformPoint(gunLocal) + new Vector3(R(-0.4f, 0.4f), R(-0.4f, 0.4f), R(-0.4f, 0.4f));
                        if (t < DeathTrailFireSec)
                            z.EmitChannel("leg_fire", at, new Vector3(R(-0.2f, 0.2f), R(0.2f, 0.8f), R(-0.2f, 0.2f)), R(0.35f, 0.5f), R(1.6f, 2.4f) * (1f - 0.5f * t / DeathTrailFireSec),
                                R(0f, 1f) < 0.5f ? OpenZFxCatalog.WreckFlameB : OpenZFxCatalog.WreckFlameC, 1f);
                        z.EmitChannel("leg_smoke", at, new Vector3(R(-0.15f, 0.15f), R(0.2f, 0.6f), R(-0.15f, 0.15f)), R(1.6f, 2.4f), R(1.4f, 2.2f) * Mathf.Lerp(1f, 0.6f, t / DeathTrailSmokeSec),
                            OpenZFxCatalog.WreckSmokeDark, 0.85f);
                        DeathBlastTrailPuffs++;
                    }
                }
                if (t >= end && si >= secs.Count && afterKicked) Destroy(this);
            }
        }
    }
}
