using UnityEngine;

namespace OpenZ
{
    public sealed partial class OzBigTanksFx
    {
        public sealed partial class Tank
        {
            // 1.5 kit big_tanks_dust_redo: the track dust and the exhaust are drawn by this tank's own OzBigTanksDust emitter (View/OzBigTanksDust.cs
            // says what and why); the old DustStep / Exhaust above stay for OzBigTanksDust.On = false (a before / after only)
            OzBigTanksDust dfx; bool dfxDone; float dfxRetry;

            /// <summary>The emitter (null before the first frame, after the death fade or with the switch off).</summary>
            public OzBigTanksDust DustFx => dfx;

            /// <summary>Tank.Step: alive -> the emitter steps; dead / broken -> it stops and fades out (once).</summary>
            void DustFxStep(float dt, bool dead, bool crewed, bool hullShown)
            {
                if (dfxDone) return;
                if (dead) { DustFxRelease(); return; }
                if (!dfx)
                {
                    if (Time.unscaledTime < dfxRetry) return;
                    dfxRetry = Time.unscaledTime + 0.5f;
                    if (!fx || !view) return;
                    dfx = OzBigTanksDust.Make(fx.transform, view, DustScale(type) / 1.2f, hullR, turretR);
                    if (!dfx) return;
                }
                int d0 = OzBigTanksDust.DustEmitted, s0 = OzBigTanksDust.SmokeEmitted;
                bool bizon = type == OzBigTanks.Bizon;
                dfx.Step(dt, crewed, hullShown, vm, exh, mouths, type, bizon ? 1.83f : 1.8f, bizon ? 3.0f : 2.55f);
                Dust += OzBigTanksDust.DustEmitted - d0; Smoke += OzBigTanksDust.SmokeEmitted - s0;
            }

            /// <summary>Death, break-up or the tank gone (Dispose): no new particle, the live ones fade out, then the emitter is destroyed.</summary>
            void DustFxRelease()
            {
                dfxDone = true;
                if (dfx) dfx.Release();
                dfx = null;
            }
        }
    }
}
