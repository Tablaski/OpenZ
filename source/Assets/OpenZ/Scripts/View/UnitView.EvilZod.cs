using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>DV161 EvilZod in UnitView.Sync. In a burst the rules move him 3 px a tick with Dx and Dy at 0, so the speed cap of the drawn position would hold him back and the 6 m rule
    /// it leaves the rules' point at the burst's own speed and eases out onto the landing spot, so he arrives after the rules do; ZEvilZod.PlantTicks holds the shot until he stands.</summary>
    public partial class UnitView
    {
        /// <summary>Sync, before the drawn position's spring: true = this frame's drawn point is p as it is.</summary>
        bool EvilZodDrawn(Vector3 p)
        {
            bool burst = OzEvilZodView.InBurst(Obj);
            if (!burst && !evilBrake && !evilBurstSeen) return false;
            float dx = 0f, dz = 0f, left = 0f;
            if (burst) { dx = (Obj.EvilDashToX - Obj.CenterX) / 8f; dz = -(Obj.EvilDashToY - Obj.CenterY) / 8f; left = Mathf.Sqrt(dx * dx + dz * dz); }
            if (!evilBrake)
            {
                if (burst)
                {
                    evilBurstSeen = true;
                    float all = new Vector2(Obj.EvilDashToX - Obj.EvilDashFromX, Obj.EvilDashToY - Obj.EvilDashFromY).magnitude / 8f, len = Mathf.Min(EvilBrakeMetres, all * 0.6f);
                    if (left > len || left < 0.05f) { EvilDrawnAt(p); return true; }
                    evilBrakeLeft = left; evilBrakeDir = new Vector3(dx, 0f, dz) / left;
                }
                else { evilBrakeLeft = 0f; evilBrakeDir = Vector3.zero; }                        // the burst ended between two frames
                evilBrake = true; evilBrakeAge = 0f; evilBrakeFrom = disp; EvilBrakeSeconds = Mathf.Max(EvilBrakeMin, 3f * evilBrakeLeft / EvilBurstMps);   // a cubic ease starts at 3 x its mean speed: the burst's own speed
            }
            evilBurstSeen = burst;
            evilBrakeAge += Time.deltaTime * Mathf.Max(0.25f, game.gameSpeed);
            float u = Mathf.Clamp01(evilBrakeAge / EvilBrakeSeconds), e = 1f - (1f - u) * (1f - u) * (1f - u);
            var to = burst ? evilBrakeFrom + evilBrakeDir * evilBrakeLeft : p;                  // once the rules stand he eases onto their point, wherever a blocked line left it
            var at = Vector3.Lerp(evilBrakeFrom, to, e); at.y = p.y;
            EvilDrawnAt(at);
            if (u >= 1f) evilBrake = false;
            return true;
        }
        void EvilDrawnAt(Vector3 at) { disp = at; dispVel = Vector3.zero; dispInit = true; lastDisp = at; bodyVel = Vector3.zero; strideSpeed = 0f; walkShown = false; sepOffset = Vector3.zero; }

        /// <summary>The brake: how far before the end of the burst it starts (m), the burst's speed (m/s), its shortest time (s).</summary>
        public static float EvilBrakeMetres = 5f, EvilBurstMps = 37.5f, EvilBrakeMin = 0.22f;
        bool evilBrake, evilBurstSeen; float evilBrakeAge, evilBrakeLeft; Vector3 evilBrakeFrom, evilBrakeDir;
        /// <summary>The drawn body is braking (OzEvilZodBody, OzEvilZodFx).</summary>
        public bool EvilBraking => evilBrake;
        /// <summary>How long this brake lasts, in game seconds.</summary>
        public float EvilBrakeSeconds { get; private set; } = 0.3f;

        /// <summary>Sync, after the body's yaw: in the windup and the burst he turns to the line of the dash, fast, and holds it.</summary>
        void EvilZodYaw(float dt)
        {
            EvilFaceYaw(dt);                                                                  // 1.4 DV174 fix round 1: onto his target after a stop and while a volley waits (View/UnitView.EvilFace.cs)
            if (Obj == null || !(Obj.EvilDashing || evilBrake)) return;
            float dx = Obj.EvilDashToX - Obj.EvilDashFromX, dz = -(Obj.EvilDashToY - Obj.EvilDashFromY);
            if (dx * dx + dz * dz < 1f) return;
            yaw = Mathf.MoveTowardsAngle(yaw, Mathf.Atan2(dx, dz) * Mathf.Rad2Deg, 900f * dt);
        }

        /// <summary></summary>
        bool EvilZodShock(Vector3 blast)
        {
            if (Obj == null || !Obj.IsEvilZod) return false;
            var f = GetComponent<OzEvilZodFx>(); if (!f) return false;
            death = Death.Evil; Popped = true; strideSpeed = 0f; walkShown = false; OzDeathTrace.Mode(this, "electric burst");
            f.BeginShock(blast);
            return true;
        }
        /// <summary>His death's first part runs: the body still holds its pose.</summary>
        public bool EvilShocked => death == Death.Evil;

        /// <summary>OzEvilZodView.OnSpawn: his place among the other units is as wide as he is.</summary>
        public void EvilZodSpacing() { radius = Mathf.Max(radius, OzEvilZodView.ViewRadius); }
    }
}
