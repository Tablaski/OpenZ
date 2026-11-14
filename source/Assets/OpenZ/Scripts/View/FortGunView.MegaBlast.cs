using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// Every time a fort's top mega cannon dies and is thrown (shot off while its fort stands, or with
    /// its dying fort), a big explosion goes off at the cannon in that moment, once (StartToss runs once per cannon):
    /// fireball and smoke (the building blast), a second fire burst, a metal debris breakup with its rain sound, dust, a short exposure
    /// flash, a camera shake when it is on screen (OzShakeGate) and a big sound (the fort booms + the large blast). Every fort, red and
    /// blue, on every level: it hangs on FortGunView.StartToss, which every fort's top cannon runs when it is shot off or its fort is destroyed.
    /// MegaBlastOn = false: the old toss (one 2.4 explosion and the toss sound).
    /// </summary>
    public partial class FortGunView
    {
        public static bool MegaBlastOn = true;
        public static int MegaBlasts;
        public static float LastMegaBlastAt = -100f;
        public static Vector3 LastMegaBlastPos;

        void MegaBlast(Vector3 at)
        {
            if (!MegaBlastOn) return;
            MegaBlasts++; LastMegaBlastAt = Time.time; LastMegaBlastPos = at;
            var p = at + Vector3.up * 0.8f;
            var z = OpenZFxPlayer.I;
            if (z) z.Blast(p, 0f, FxBlastKind.Building, Fort != null ? Fort.Id : -1);                  // fireball + smoke column
            var fx = FxManager.I;
            if (fx)
            {
                if (!z) fx.Explosion(p, 3.2f);
                fx.Explosion(p + Vector3.up * 1.5f, 2.8f);                                             // the second fire burst over it
                fx.Breakup(p, "metal", 4.5f, 22, 1.5f);                                               // cannon plates and shells flying (their landing sound: OzSoundV3.Debris)
                fx.Dust(at, 3f);
            }
            OpenZBloomDirector.Flash(0.55f);
            if (rts) rts.Shake(1.2f, at, "fort_megablast");
            if (!Sfx("exp_fort", at, 1f)) Legacy("fort_megablast", at, 1f, "explosion-v1-20260909", "explosion-v2-20260909");
            Sfx("exp_large", at, 1f);
        }
    }
}
