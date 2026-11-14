using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// DV161 EvilZod, what his shot and his power dash look like. One component on his view, fed by CombatView from the rules' events; nothing here reads a random number of the rules.
    /// <list type="bullet">
    /// <item>The shot: a heavy muzzle, a fan of smoke and a spread of pellets in a fixed cone. The rules' shot always lands, and the cone being fixed shows the range: close, every pellet is
    /// on the body; far, most of them fly past and dust the ground. A dash shot packs more pellets in a tighter cone.</item>
    /// <item>The dash: no post effect. Eight ghost silhouettes baked from his own mesh smear him alone, a hot blue ribbon runs at chest height, two thin red lines trail from his eyes, dust
    /// and light mark the line, the colours are over the bloom threshold and the bloom is held up for the burst. It works on every setting, and the game's MOTION BLUR option does not touch it.</item>
    /// <item>Hits spark as metal, he smokes from one shoulder under 30 % health, his death is blue arcs through his standing body, then it bursts into pieces of his own mesh (OzEvilZodBurst); in lava he melts.</item>
    /// </list>
    /// </summary>
    [DefaultExecutionOrder(82)]   // after his upper body (81): the rules' event comes at CombatView's 50, while the gun still lies in the Animator's rifle pose, so the shot waits for this frame's real muzzle
    public sealed class OzEvilZodFx : MonoBehaviour
    {
        /// <summary>The flash, metres: the hot core, the main cone's width; the ground lights' strengths, the lights' power.</summary>
        public static float FlashCore = 2.4f, FlashCone = 0.7f, GroundFlash = 2.8f, GroundBlast = 2.4f, LightPower = 26f, LightLow = 20f, SkidEvery = 0.6f;
        public static int Pellets = 20, DashPellets = 28, MaxImpacts = 3, GhostCount = 8;
        public static float PelletSize = 0.2f, SleeveSize = 0.46f, PelletStart = 0.3f, LandSparkMin = 7f, LandSparkMax = 14f;
        public static float ConeDeg = 9f, DashConeDeg = 6f, PelletMps = 70f, DashMuzzleK = 1.3f, GhostLife = 0.14f, GhostAlpha = 0.22f, EyeFlare = 2f, SmokeBelow = 0.3f;
        /// <summary>
        /// 1 - PelletString x (i x 0.618 mod 1) of PelletMps, a fixed pace of its own, so the volley strings out along its line and reads as pellets, not as one cone
        /// (the centre pellet leads at the full pace). View only: the rules' hit is not moved.</summary>
        public static float PelletString = 0.18f;
        /// <summary>RibbonCore and EyeLine are linear HDR values, written through <see cref="SetBeam"/>.</summary>
        public static readonly Color Ice = new Color(0.2f, 0.4f, 1f), RibbonCore = new Color(1.2f, 2.4f, 6f), EyeLine = new Color(5f, 0.3f, 0.12f), EyeBase = new Color(6.5f, 0.39f, 0.156f), PelletHot = new Color(1f, 0.82f, 0.5f), PelletSleeve = new Color(1f, 0.5f, 0.16f), Dirt = new Color(0.62f, 0.54f, 0.44f);
        /// <summary>Counters for the Play proof.</summary>
        public static int Shots, PelletsOnTarget, PelletsPast, Dashes, Bakes;
        /// <summary>1.4 DV174 (Sim/ZEvilZod.Face.cs): a waiting shot is drawn only once his drawn gun points within FaceTolDeg of the target, at most FaceWaitMax game seconds
        /// after the rules' shot (the rules already turned him: tools/view_lag.py of the kit). The Play check's counters: shots drawn, held for the gun, drawn at the cap;
        /// the worst gun and body angle (deg, on the ground) and the longest wait (game s) at a drawn shot; the last FaceLogMax shots.</summary>
        public static float FaceTolDeg = ZEvilFace.TolCenti / 100f, FaceWaitMax = 0.35f;
        public static int FaceDrawn, FaceHeld, FaceTimeouts; public static float FaceWorstGun, FaceWorstBody, FaceWorstWait;
        public struct FaceRow { public int id, target; public float gun, body, wait, at; public bool held, timeout; }
        public static readonly List<FaceRow> FaceLog = new List<FaceRow>(); public const int FaceLogMax = 512;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void FaceReset() { FaceDrawn = FaceHeld = FaceTimeouts = 0; FaceWorstGun = FaceWorstBody = FaceWorstWait = 0f; FaceLog.Clear(); }
        /// <summary>1.4 DV174: a shot of this view's EvilZod waits to be drawn (OzEvilZodCone holds the cone's sparks for it).</summary>
        public static bool ShotWaiting(UnitView v) { var f = v ? v.GetComponent<OzEvilZodFx>() : null; return f && f.waiting.Count > 0; }
        /// <summary>1.4 DV174 fix round 1: the target of a volley of this view's EvilZod that the gate holds for his drawn gun (null: none held);
        /// UnitView.EvilFaceYaw turns his body onto it.</summary>
        public static ZObject ShotHeldTarget(UnitView v)
        {
            var f = v ? v.GetComponent<OzEvilZodFx>() : null; var w = ZGame.I ? ZGame.I.World : null;
            if (!f || w == null) return null;
            foreach (var s in f.waiting) if (s.faceHeld) return w.GetAny(s.target);
            return null;
        }
        static readonly int EmisId = Shader.PropertyToID("_EmissionMapColor");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Shots = PelletsOnTarget = PelletsPast = Dashes = Bakes = 0; ghostMat = null; ribbonMat = null; eyeMat = null; rts = null; }

        /// <summary>CombatView: his eight events.</summary>
        public static bool Wants(ZEventType t) => t >= ZEventType.EvilDashStart && t <= ZEventType.EvilHealStep;

        /// <summary>CombatView, one line per event.</summary>
        public static void Handle(ZEvent e, ZObject a)
        {
            if (a == null || !ZGame.I || !ZGame.I.Views.TryGetValue(a.Id, out var v) || !v) return;
            var f = v.GetComponent<OzEvilZodFx>(); if (f) f.OnEvent(e, a);
        }

        struct Due { public float at; public Vector3 pos, dir; public FxSurface surf; public bool spark; public bool hard; public int tid; }   // 1.3 row 85: tid = the vehicle / gun it lands on (0 = none)
        struct Waiting { public int id, target, rangePx; public bool dash, kill; public float at; public bool faceHeld; }   // 1.4 DV174: held for the gun
        readonly List<Waiting> waiting = new List<Waiting>(); bool braking; Vector3 lastSkidAt;
        struct Ghost { public Mesh mesh; public Matrix4x4 at, gunAt, slideAt; public float born; public bool on; }

        UnitView view; OzEvilZodBody body; SkinnedMeshRenderer skin; Mesh gunMesh, slideMesh; Transform gunT, slideT;
        readonly List<Due> due = new List<Due>(); Ghost[] ghosts; int ghostNext; MaterialPropertyBlock block, skinBlock;
        TrailRenderer ribbon, eyeL, eyeR; Transform ribbonT, eyeLT, eyeRT;
        bool windup, burst; float dustLeft, lightLeft, flare, smokeAt, deathAt = -1f, fountainLeft; Vector3 lastDustAt; bool deadDone;
        static Material ghostMat, ribbonMat, eyeMat; static RtsCamera rts;

        public void Bind(UnitView v, OzEvilZodBody b)
        {
            view = v; body = b;
            skin = GetComponentInChildren<SkinnedMeshRenderer>();
            foreach (var r in GetComponentsInChildren<MeshFilter>(true))
            {
                if (r.name == "Shotgun") { gunMesh = r.sharedMesh; gunT = r.transform; }
                else if (r.name == "Shotgun_Pump") { slideMesh = r.sharedMesh; slideT = r.transform; }
            }
            block = new MaterialPropertyBlock(); skinBlock = new MaterialPropertyBlock();
            OzEvilZodDashLook.Attach(this, v, b);                                               // 1.3 row 73: the dash's trail, afterimages, ground trace, dust and front shield (View/OzEvilZodDashLook.cs)
        }

        // ---------------------------------------------------------------------------- the rules' events
        void OnEvent(ZEvent e, ZObject a)
        {
            switch (e.type)
            {
                case ZEventType.EvilDashStart: Windup(a); break;
                case ZEventType.EvilDashGo: Go(a); break;
                case ZEventType.EvilDashEnd: Stop(a); body.OnDashEnd(); break;
                case ZEventType.EvilDashCancel: Stop(a); break;
                case ZEventType.EvilShot: waiting.Add(new Waiting { id = a.Id, target = e.otherId, rangePx = e.radius, dash = e.flag, kill = e.flag2, at = Time.time }); body.ShotComing(e.otherId); break;
                case ZEventType.EvilDashReady: OzEvilZodCircle.Ready(a.Id); break;
                case ZEventType.EvilHealStep: HealSpark(); break;
            }
        }

        OpenZFxPlayer Fx => OpenZFxPlayer.I;
        static void Shake(float amount, Vector3 at, string src)
        {
            if (!rts) rts = FindFirstObjectByType<RtsCamera>();
            if (rts) rts.Shake(amount, at, src);
        }
        Vector3 Feet => transform.position;

        void Windup(ZObject a)
        {
            windup = true; flare = 1f;
            var z = Fx; if (!z) return;
            for (int i = 0; i < 10; i++)
            {
                float t = i / 10f * Mathf.PI * 2f; var d = new Vector3(Mathf.Cos(t), 0f, Mathf.Sin(t));
                z.EmitChannel("m12_dust", Feet + d * 1.6f + Vector3.up * 0.1f, -d * 1.4f + Vector3.up * 0.1f, 0.5f, 0.6f, Dirt, 0.28f);
            }
        }

        void Go(ZObject a)
        {
            windup = false; burst = true; Dashes++; lastDustAt = Feet; dustLeft = 0f; lightLeft = 0f;
            EnsureTrails();
            SetTrails(true);
            var z = Fx; if (!z) return;
            z.Dust(Feet + Vector3.up * 0.2f, 0.26f * 1.1f); z.Dust(Feet, 1f);
            for (int i = 0; i < 14; i++)
            {
                float t = i / 14f * Mathf.PI * 2f; var d = new Vector3(Mathf.Cos(t), 0f, Mathf.Sin(t));
                z.EmitChannel("m12_dust", Feet + d * 0.6f + Vector3.up * 0.15f, d * 5.5f + Vector3.up * 0.5f, 0.7f, 0.9f, Dirt, 0.34f);
            }
            z.AddLight(Feet + Vector3.up, Ice, 9f, 8f, 0.25f);
            Shake(0.35f, Feet, "evilzod_dash");
        }

        void Stop(ZObject a)
        {
            windup = false; burst = false; flare = 0f;
            SetTrails(false);
        }

        void Plant(ZObject a)
        {
            var z = Fx; if (!z) return;
            var f = Quaternion.Euler(0f, transform.eulerAngles.y + 180f, 0f) * Vector3.forward;
            for (int i = 0; i < 14; i++)
            {
                var d = Quaternion.AngleAxis(-55f + 110f * i / 13f, Vector3.up) * f;
                z.EmitChannel("m12_dust", Feet + d * 0.5f + Vector3.up * 0.15f, d * Random.Range(4f, 7.5f) + Vector3.up * Random.Range(0.3f, 1.2f), 0.8f, 0.8f, Dirt, 0.4f);
                if (i % 2 == 0) z.EmitChannel("wv2_spark", Feet + d * 0.3f + Vector3.up * 0.1f, d * Random.Range(5f, 10f) + Vector3.up * Random.Range(1f, 3f), Random.Range(0.2f, 0.4f), 0.05f, Color.white, 1f);
            }
            z.Dust(Feet, 0.9f);
            Shake(0.5f, Feet, "evilzod_plant");
        }

        // ---------------------------------------------------------------------------- the shot
        /// <summary>Frame, after his upper body: a waiting shot leaves the gun where the gun is now, once the hold is up (at most ShotWaitMax after the rules' event; the sound does not wait).</summary>
        void Shoot()
        {
            if (waiting.Count == 0) return;
            float gs = Mathf.Max(0.25f, ZGame.I.gameSpeed);
            for (int i = 0; i < waiting.Count; i++)
            {
                var s = waiting[i]; float late = (Time.time - s.at) * gs;
                if (body.FireWeight < OzEvilZodBody.ShotReadyAt && late < OzEvilZodBody.ShotWaitMax && !view.Dying) continue;
                float gunOff = GunOff(s.target, out float bodyOff);                              // 1.4 DV174: his drawn gun onto the target first (the rules turned him already)
                if (gunOff > FaceTolDeg && late < FaceWaitMax && !view.Dying) { s.faceHeld = true; waiting[i] = s; continue; }
                FaceNote(s, gunOff, bodyOff, late);
                waiting.RemoveAt(i--); Shot(s, s.faceHeld ? Mathf.Min(late, OzEvilZodBody.ShotWaitMax) : late);   // 1.4 DV174: a shot held for the gun keeps its whole recoil
            }
        }

        void Shot(Waiting e, float late)
        {
            Shots++;
            var w = ZGame.I.World; var t = w != null ? w.GetAny(e.target) : null;
            body.OnShot(e.target, late, e.dash);
            var z = Fx; if (!z) return;
            Vector3 from = body.MuzzlePos, aim = t != null ? AimPointOf(t) : from + body.BarrelDir * 8f;
            if (t == null) aim.y = from.y;
            Vector3 dir = (aim - from).sqrMagnitude > 0.01f ? (aim - from).normalized : body.BarrelDir;
            bool dash = e.dash; float k = dash ? DashMuzzleK : 1f;
            z.Muzzle(from, dir, FxWeapon.Cannon);
            var side = Vector3.Cross(Vector3.up, dir).normalized;
            var warm = new Color(1f, 0.74f, 0.4f);
            if (OzEvilZodMuzzle.On) OzEvilZodMuzzle.Blast(z, from, dir, k);
            else
            {   // round 2's flash (OzEvilZodMuzzle.On = false): a soft core born at size 0, three soft cones, a warm glow, a fan of thin smoke; 0.07-0.1 s
                z.EmitChannel("m12_core", from + dir * 0.3f, Vector3.zero, 0.07f, FlashCore * k, new Color(1f, 0.86f, 0.6f), 1f);
                z.EmitChannel("leg_cone", from + dir * 1.3f * k, dir * 0.05f, 0.1f, FlashCone * k, new Color(1f, 0.8f, 0.42f), 1f);
                for (int i = -1; i <= 1; i += 2)
                    z.EmitChannel("leg_cone", from + (dir + side * i * 0.12f).normalized * 0.9f * k, (dir + side * i * 0.12f).normalized * 0.05f, 0.09f, 0.5f * k, new Color(1f, 0.72f, 0.3f), 1f);
                z.EmitChannel("wv2_glow", from, Vector3.zero, 0.09f, 3f * k, warm, 1f);
                for (int i = 0; i < 5; i++) z.EmitChannel("m12_smoke", from + dir * (0.5f + i * 0.45f), dir * Random.Range(2.5f, 5f) + Vector3.up * 0.3f, Random.Range(0.6f, 1f), 0.45f + 0.12f * i, new Color(0.56f, 0.54f, 0.5f), 0.34f);
            }
            Vector3 flatDir = new Vector3(dir.x, 0f, dir.z).sqrMagnitude > 1e-4f ? new Vector3(dir.x, 0f, dir.z).normalized : Vector3.forward, ground = new Vector3(from.x, ZGame.I.GroundY(from.x, from.z), from.z), ahead = ground + flatDir * 3.5f;
            ahead.y = ZGame.I.GroundY(ahead.x, ahead.z);
            if (z.pointLights)
            {
                z.AddLight(from, warm, LightPower * k, 16f, 0.18f, 2.2f, 0f, true);
                z.AddLight(ahead + Vector3.up * 1.4f, warm, LightLow * k, 13f, 0.16f);          // low over the ground in front of him: the terrain itself lights up
            }
            if (z.groundLights) { z.GroundLight(ground, GroundFlash * k); z.GroundLight(ahead, GroundBlast * k, false, true, 0.8f); }
            for (int i = 0; i < 9; i++)                                                         // the blast's breath flattens the dust under the muzzle
            {
                var d = Quaternion.AngleAxis(-50f + 100f * i / 8f, Vector3.up) * flatDir; d.y = 0f;
                z.EmitChannel("m12_dust", ground + d * 1.2f + Vector3.up * 0.15f, d * Random.Range(3.5f, 7f) + Vector3.up * 0.25f, 0.6f, 0.7f, Dirt, 0.3f);
            }
            Shake(dash ? 0.6f : 0.5f, from, "evilzod_shot");
            Spread(from, dir, t, aim, dash ? DashPellets : Pellets, dash ? DashConeDeg : ConeDeg, e.id, e.rangePx, e.kill);
            float out0 = Mathf.Max(0f, 0.45f - late) / Mathf.Max(0.25f, ZGame.I.gameSpeed);
            due.Add(new Due { at = Time.time + out0, pos = default, dir = dir, surf = FxSurface.Auto, spark = false, hard = false });   // the shell, at the slide's rear stop
        }

        Vector3 AimPointOf(ZObject t) => ZGame.I.Views.TryGetValue(t.Id, out var v) && v ? v.AimPos() : ZGame.I.CenterWorld(t, false) + Vector3.up;

        /// <summary>1.4 DV174: how far (deg, on the ground) his drawn gun's line and his body's front are off the line from his muzzle to the target now (the line his
        /// pellets fly); 0 without a target or right on top of it.</summary>
        float GunOff(int targetId, out float bodyOff)
        {
            bodyOff = 0f;
            var w = ZGame.I.World; var t = w != null ? w.GetAny(targetId) : null;
            if (t == null || !body) return 0f;
            Vector3 to = AimPointOf(t) - body.MuzzlePos, g = body.BarrelDir, f = -view.transform.forward; to.y = 0f; g.y = 0f; f.y = 0f;   // every robot model faces its root's -z
            if (to.sqrMagnitude < 1f || g.sqrMagnitude < 1e-6f) return 0f;
            if (f.sqrMagnitude > 1e-6f) bodyOff = Vector3.Angle(f, to);
            return Vector3.Angle(g, to);
        }

        void FaceNote(Waiting s, float gun, float bodyOff, float late)
        {
            bool cap = gun > FaceTolDeg && !view.Dying;
            FaceDrawn++; if (s.faceHeld) FaceHeld++; if (cap) FaceTimeouts++;
            FaceWorstGun = Mathf.Max(FaceWorstGun, gun); FaceWorstBody = Mathf.Max(FaceWorstBody, bodyOff); FaceWorstWait = Mathf.Max(FaceWorstWait, late);
            FaceLog.Add(new FaceRow { id = s.id, target = s.target, gun = gun, body = bodyOff, wait = late, at = Time.time, held = s.faceHeld, timeout = cap });
            if (FaceLog.Count > FaceLogMax) FaceLog.RemoveAt(0);
        }

        /// <summary>The pellets: a ring and a centre in a fixed cone, turned by the shooter's id and range. The target's width decides who lands on it; the others fly on to the ground.</summary>
        void Spread(Vector3 from, Vector3 dir, ZObject t, Vector3 aim, int n, float coneDeg, int id, int rangePx, bool killed)
        {
            var z = Fx; float gs = Mathf.Max(0.25f, ZGame.I.gameSpeed), speed = PelletMps * gs; float dist = (aim - from).magnitude;
            float bodyR = t == null ? 0f : t.Kind == ZKind.Robot ? 0.55f : t.Type == "heavy" ? 2f : t.Kind == ZKind.Building ? 3f : 1.5f;
            Vector3 u = Vector3.Cross(dir, Vector3.up).sqrMagnitude > 1e-4f ? Vector3.Cross(dir, Vector3.up).normalized : Vector3.right, v = Vector3.Cross(dir, u).normalized;
            float turn = (id * 0.6180339f + rangePx * 0.137f) % 1f * Mathf.PI * 2f, tan = Mathf.Tan(coneDeg * Mathf.Deg2Rad);
            float gy = ZGame.I.GroundY(aim.x, aim.z); int impacts = 0;
            ShotLine.LastOver = ShotLine.Through(from, aim, 0f, t);                                     // shots_over_walls: one answer for the volley; they have no Stop
            for (int i = 0; i < n; i++)
            {
                float a = turn + i * 2.399963f, r = i == 0 ? 0f : Mathf.Sqrt(i / (float)(n - 1)) * tan;
                Vector3 d = (dir + (u * Mathf.Cos(a) + v * Mathf.Sin(a)) * r).normalized;
                bool on = t != null && r * dist <= bodyR;
                Vector3 end; float len;
                if (on) { end = from + d * dist; len = dist; PelletsOnTarget++; }
                else
                {
                    len = dist + 3f + 5f * (i % 3) / 2f;
                    end = from + d * len;
                    if (d.y < -0.002f) { float hit = (from.y - gy) / -d.y; if (hit > 0f && hit < len) { len = hit; end = from + d * len; } }
                    PelletsPast++;
                }
                float pace = speed * (1f - PelletString * (i * 0.6180339f % 1f)), flight = Mathf.Max(0.01f, len / pace);   // row 66: the shot string, each pellet its own fixed pace
                z.EmitRound("wv2_mg_tracer", "leg_tracer_sleeve", from, end, PelletStart, pace, flight, PelletSize, PelletHot, 1f, SleeveSize, PelletSleeve, 0.7f);
                bool real = impacts < MaxImpacts && (on || i % 3 == 0); if (real) impacts++;
                due.Add(new Due { at = Time.time + flight, pos = end, dir = d, surf = on ? (t.Kind == ZKind.Robot ? FxSurface.Unit : FxSurface.Metal) : FxSurface.Auto, spark = !real, hard = true, tid = on && t.Kind != ZKind.Robot ? t.Id : 0 });
            }
            ShotLine.LastOver = false;
        }

        void Land(Due d)
        {
            OzEvilZodHits.Land(this, d.pos, d.dir, d.surf, d.spark, d.hard);                    // 1.3 row 71 (View/OzEvilZodHits.cs): more metal sparks on vehicles and guns; the close kill's throw
            if (d.hard && d.tid != 0) OzVehicleRock.Hit(d.tid, d.dir);                          // 1.3 row 85 (View/OzVehicleRock.cs): the vehicle's hull and turret rock away from the shot
            var z = Fx; if (!z) return;
            if (d.hard && OzShakeGate.OnScreen(d.pos))
                for (int i = 0; i < 3; i++)                                                     // fast sparks where a pellet lands
                    z.EmitChannel("wv2_spark", d.pos + Vector3.up * 0.1f, (Vector3.Reflect(d.dir, Vector3.up) + Random.insideUnitSphere * 0.7f).normalized * Random.Range(LandSparkMin, LandSparkMax), Random.Range(0.12f, 0.28f), 0.06f, PelletHot, 1f);
            if (!d.hard)
            {   // the spent shell leaves the port on his right side and falls
                var o = body.MuzzlePos - body.BarrelDir * 0.9f; var side = Vector3.Cross(Vector3.up, body.BarrelDir).normalized;
                z.EmitChannel("wv2_casing", o, side * Random.Range(2f, 3f) + Vector3.up * Random.Range(2.5f, 3.5f), Random.Range(1.2f, 1.8f), 0.8f, Color.white, 1f);
                z.EmitChannel("m12_smoke", o, Vector3.up * 0.5f, 0.6f, 0.2f, new Color(0.6f, 0.58f, 0.54f), 0.25f);
                return;
            }
            if (d.surf != FxSurface.Auto)
            {
                if (d.spark || !OpenZHitSparks.Burst(d.pos, d.dir, FxWeapon.Rifle, d.surf, 1.2f, !d.spark))
                    for (int i = 0; i < 4; i++) z.EmitChannel("wv2_spark", d.pos, (-d.dir + Random.insideUnitSphere) * Random.Range(2f, 5f), Random.Range(0.15f, 0.3f), 0.05f, new Color(1f, 0.8f, 0.5f), 1f);
                return;
            }
            if (d.spark) z.EmitChannel("m12_dust", d.pos + Vector3.up * 0.1f, Vector3.up * 0.7f, 0.5f, 0.35f, Dirt, 0.3f);
            else z.Impact(d.pos);
        }

        void HealSpark()
        {
            var z = Fx; if (!z || !OzShakeGate.OnScreen(body.ChestPos)) return;
            for (int i = 0; i < 3; i++) z.EmitChannel("wv2_spark", body.ChestPos + Random.onUnitSphere * 0.3f, Vector3.up * Random.Range(0.6f, 1.2f), 0.3f, 0.04f, Ice, 1f);
        }

        // ---------------------------------------------------------------------------- frame
        /// <summary>The brake, from UnitView: the skid's sound at its start, heel dust and sparks along the way, the stop's dust and shake at its end.</summary>
        void BrakeFrame()
        {
            bool on = view.EvilBraking;
            if (on && !braking) { braking = true; lastSkidAt = Feet; if (view.Obj != null) AudioDirector.Fire(AudioDirector.EzSkidId, Feet, view.Obj.Id); }
            if (on)
            {
                var z = Fx; var at = Feet; float d = (at - lastSkidAt).magnitude;
                if (z && d >= SkidEvery && OzShakeGate.OnScreen(at))
                {
                    lastSkidAt = at;
                    var f = Quaternion.Euler(0f, transform.eulerAngles.y + 180f, 0f) * Vector3.forward; var r = Vector3.Cross(Vector3.up, f);
                    for (int i = -1; i <= 1; i += 2)
                    {
                        var heel = at + r * (0.4f * i) + Vector3.up * 0.12f;
                        z.EmitChannel("m12_dust", heel, -f * Random.Range(1f, 2.2f) + r * (i * 0.7f) + Vector3.up * 0.5f, 0.7f, 0.65f, Dirt, 0.34f);
                        z.EmitChannel("wv2_spark", heel + f * 0.3f, f * Random.Range(4f, 8f) + r * Random.Range(-1.5f, 1.5f) + Vector3.up * Random.Range(0.6f, 2f), Random.Range(0.15f, 0.3f), 0.05f, Color.white, 1f);
                    }
                }
            }
            else if (braking) { braking = false; Plant(view.Obj); }
        }

        void LateUpdate()
        {
            if (!view) return;
            Shoot(); BrakeFrame();
            float now = Time.time;
            for (int i = due.Count - 1; i >= 0; i--) if (now >= due[i].at) { var d = due[i]; due.RemoveAt(i); Land(d); }
            if (view.EvilShocked) { Shock(); return; }
            if (view.Dying) { Dying(); return; }
            var o = view.Obj; if (o == null) return;
            if (windup) DustCreep();
            if (burst) BurstFrame();
            Eyes();
            DrawGhosts();
            if (o.MaxHealth > 0 && o.Health < o.MaxHealth * SmokeBelow && now >= smokeAt)
            {
                smokeAt = now + 0.3f;
                var z = Fx; if (z && body.ShoulderR) z.EmitChannel("m12_smoke", body.ShoulderR.position + Vector3.up * 0.3f, new Vector3(0f, 0.7f, 0f), 1.2f, 0.32f, new Color(0.2f, 0.2f, 0.22f), 0.42f);
            }
        }

        void DustCreep()
        {
            var z = Fx; if (!z || Random.value > 0.35f) return;
            z.EmitChannel("m12_dust", Feet + Random.onUnitSphere * 0.9f + Vector3.up * 0.2f, Vector3.up * 0.3f, 0.4f, 0.35f, Dirt, 0.22f);
        }

        void BurstFrame()
        {
            var at = Feet; var z = Fx;
            if (!OzShakeGate.OnScreen(at)) return;
            if (!OzEvilZodDashLook.On) OpenZBloomDirector.Sustain(0.5f);                       // 1.3 row 73: with the new look its own bloom knobs set the dash's bloom (bloomBoostDay / Night, calmBloom)
            if (!OzEvilZodDashLook.On) BakeGhost();                                             // 1.3 row 73: with the new look its afterimages are the blur (knob on = false: round 2's ghosts)
            float step = (at - lastDustAt).magnitude; lastDustAt = at;
            dustLeft -= step; lightLeft -= step;
            if (!z) return;
            var back = -(Quaternion.Euler(0f, transform.eulerAngles.y + 180f, 0f) * Vector3.forward);
            while (dustLeft <= 0f)
            {
                dustLeft += 1.2f;
                z.EmitChannel("m12_dust", at + Vector3.up * 0.2f, back * 2f + Vector3.up * 0.4f, 0.8f, 0.8f, Dirt, 0.34f);
            }
            if (lightLeft <= 0f) { lightLeft += 2f; z.AddLight(at + Vector3.up * 1.6f, Ice, 8f, 7f, 0.25f); }
        }

        void Eyes()
        {
            float want = windup || burst ? 1f : 0f;
            flare = Mathf.MoveTowards(flare, want, Time.deltaTime / 0.15f);
            if (!skin || (flare <= 0f && !flared)) return;
            skin.GetPropertyBlock(skinBlock);
            skinBlock.SetColor(EmisId, EyeBase * Mathf.Lerp(1f, EyeFlare, flare));
            skin.SetPropertyBlock(skinBlock);
            flared = flare > 0f;
        }
        bool flared;

        void Dying()
        {
            if (!deadDone)
            {
                deadDone = true; deathAt = Time.time; fountainLeft = 24; SetTrails(false); burst = windup = false;
                var z = Fx;
                if (z) { z.Dust(Feet, 1.1f); for (int i = 0; i < 16; i++) { float t = i / 16f * Mathf.PI * 2f; var d = new Vector3(Mathf.Cos(t), 0f, Mathf.Sin(t)); z.EmitChannel("m12_dust", Feet + d + Vector3.up * 0.2f, d * 3.5f + Vector3.up * 0.3f, 0.9f, 0.9f, Dirt, 0.3f); } }
                Shake(0.6f, Feet, "evilzod_death");
            }
            float u = (Time.time - deathAt) / 1.5f;
            if (skin && u <= 1.05f)
            {
                skin.GetPropertyBlock(skinBlock); skinBlock.SetColor(EmisId, EyeBase * Mathf.Clamp01(1f - u)); skin.SetPropertyBlock(skinBlock);
            }
            if (fountainLeft > 0f && Fx)
            {
                int n = Mathf.Min(3, Mathf.CeilToInt(fountainLeft)); fountainLeft -= n;
                for (int i = 0; i < n; i++) Fx.EmitChannel("wv2_spark", body.ChestPos, Random.onUnitSphere * Random.Range(2f, 5f) + Vector3.up * 2.5f, Random.Range(0.4f, 0.9f), 0.06f, new Color(1f, 0.8f, 0.45f), 1f);
            }
        }

        /// <summary>Game seconds the arcs run before the body bursts; the width of an arc (m); how far they lie in front of his bones, to the camera (m); the armour's flicker.</summary>
        public static float ShockSeconds = 1.15f, ArcWidth = 0.4f, ArcFront = 0.7f, ShockFlash = 0.5f;
        public static int ArcsFirst = 6, ArcsLast = 13, Bursts;
        static readonly int EmisColorId = Shader.PropertyToID("_EmissionColor");
        float shockAge, nextCrackle; Vector3 shockBlast; Transform[] arcAt;
        readonly float[] arcT = new float[17]; readonly Vector2[] arcOff = new Vector2[17]; readonly Vector3[] arcPts = new Vector3[17];

        /// <summary>UnitView.Die: the shock starts, with the electrobot's shot and its hit.</summary>
        public void BeginShock(Vector3 blast)
        {
            shockBlast = blast; shockAge = 0f; nextCrackle = 0f; deadDone = true; waiting.Clear();
            burst = windup = false; SetTrails(false);
            arcAt = body.ArcPoints;
            int id = view.Obj != null ? view.Obj.Id : -1; var c = body.ChestPos;
            AudioDirector.Fire("wpn_electro_fire", c, id); AudioDirector.Fire("imp_electro_hit", c, id);
            Shake(0.6f, Feet, "evilzod_death");
        }

        void Shock()
        {
            float gs = ZGame.I ? Mathf.Max(0.25f, ZGame.I.gameSpeed) : 1f, u = Mathf.Clamp01(shockAge / ShockSeconds); shockAge += Time.deltaTime * gs;
            var ef = OpenZElectroFx.I; var z = Fx; var c = body.ChestPos; int id = view.Obj != null ? view.Obj.Id : 0;
            if (ef && ef.Arcs != null && arcAt != null && Camera.main)
            {
                var to = (Camera.main.transform.position - c).normalized; int n = Mathf.RoundToInt(Mathf.Lerp(ArcsFirst, ArcsLast, u)), tick = Mathf.FloorToInt(Time.time * 24f);
                for (int k = 0; k < n; k++)
                {
                    var r = new ElectroBoltShape.Rng(ElectroBoltShape.Hash(id, tick, k));
                    var a = arcAt[r.Int(arcAt.Length)]; var b = arcAt[r.Int(arcAt.Length)]; if (!a || !b || a == b) continue;
                    Vector3 p0 = a.position + to * ArcFront, p1 = b.position + to * ArcFront;
                    if (k % 4 == 3) { float ang = r.Range(0f, 6.283f), rad = r.Range(1.6f, 3.6f); p1 = Feet + new Vector3(Mathf.Cos(ang) * rad, 0f, Mathf.Sin(ang) * rad); p1.y = ZGame.I.GroundY(p1.x, p1.z) + 0.05f; }   // and out to the ground
                    int m = ElectroBoltShape.Jag(ref r, 3, 0.16f, 0.6f, arcT, arcOff);
                    ElectroBoltShape.Place(p0, p1, arcT, arcOff, m, arcPts, 0f, 0f, 0f, 0.15f, to);
                    ef.Arcs.Ribbon(arcPts, m, ArcWidth * r.Range(0.7f, 1.2f), 0.22f, 7f, 1.5f, OpenZElectroFx.Ice, r.Range(0.7f, 1f), tick + k, 0.6f, 0.6f, 3f);
                }
            }
            if (Time.time >= nextCrackle)
            {
                nextCrackle = Time.time + Random.Range(0.08f, 0.16f);
                if (z)
                {
                    for (int i = 0; i < 5; i++) z.EmitChannel("wv2_spark", c + Random.onUnitSphere * 1.1f, Random.onUnitSphere * Random.Range(3f, 7f) + Vector3.up * 1.5f, Random.Range(0.2f, 0.45f), 0.05f, i % 2 == 0 ? Color.white : OpenZElectroFx.Ice, 1f);
                    if (z.pointLights) z.AddLight(c, OpenZElectroFx.Ice, 18f, 11f, 0.12f);
                }
                AudioDirector.Fire("electro_spark", c, id, 0.8f, Random.Range(0.9f, 1.2f));
                OpenZBloomDirector.Sustain(0.5f);
            }
            if (skin)
            {   // his armour flickers with it: as the stun's emission flash, written as a linear value
                skin.GetPropertyBlock(skinBlock); skinBlock.SetVector(EmisColorId, ElectroColour.Shader(OpenZElectroFx.Ice * (ShockFlash * Random.Range(0.3f, 1f)))); skin.SetPropertyBlock(skinBlock);
            }
            if (shockAge >= ShockSeconds) BurstBody(c, id);
        }

        /// <summary>The end of the shock: the short-circuit bang, the flash, the pop's sparks, chips, gun and cloak (RobotPopDeath, its electric variant), and his own body in pieces (OzEvilZodBurst).</summary>
        void BurstBody(Vector3 c, int id)
        {
            Bursts++;
            var z = Fx;
            AudioDirector.Fire("electro_death", c, id);
            if (z)
            {
                for (int i = 0; i < 60; i++) z.EmitChannel("wv2_spark", c, Random.onUnitSphere * Random.Range(4f, 12f) + Vector3.up * 2f, Random.Range(0.3f, 0.8f), 0.07f, i % 3 == 0 ? Color.white : OpenZElectroFx.Ice, 1f);
                z.EmitChannel("m12_smoke", c, Vector3.up * 1.2f, 1.8f, 2.2f, new Color(0.32f, 0.33f, 0.36f), 0.6f);
                if (z.pointLights) z.AddLight(c, OpenZElectroFx.Ice, 40f, 16f, 0.5f);
                if (z.groundLights) z.GroundLight(new Vector3(c.x, ZGame.I.GroundY(c.x, c.z), c.z), 2.4f, true);
            }
            Shake(1.2f, c, "evilzod_death");
            if (skin) { skin.GetPropertyBlock(skinBlock); skinBlock.SetVector(EmisColorId, Vector4.zero); skin.SetPropertyBlock(skinBlock); OzEvilZodBurst.Play(skin, c); }
            if (ZGame.I && view.Obj != null) RobotPopDeath.Play(view, 1, shockBlast, RobotDeathRule.Hash(id, 7, 3u));
            view.Hide();
        }

        // ---------------------------------------------------------------------------- the blur and the lines
        void BakeGhost()
        {
            if (!skin || !GhostMaterial) return;
            if (ghosts == null) ghosts = new Ghost[GhostCount];
            ref var g = ref ghosts[ghostNext]; ghostNext = (ghostNext + 1) % ghosts.Length;
            if (!g.mesh) { g.mesh = new Mesh { name = "EvilZod ghost" }; g.mesh.MarkDynamic(); }
            skin.BakeMesh(g.mesh, true);
            g.at = Matrix4x4.TRS(skin.transform.position, skin.transform.rotation, Vector3.one);
            if (gunT) g.gunAt = gunT.localToWorldMatrix;
            if (slideT) g.slideAt = slideT.localToWorldMatrix;
            g.born = Time.time; g.on = true; Bakes++;
        }

        void DrawGhosts()
        {
            if (ghosts == null || !GhostMaterial) return;
            float now = Time.time; int layer = gameObject.layer;
            for (int i = 0; i < ghosts.Length; i++)
            {
                ref var g = ref ghosts[i]; if (!g.on) continue;
                float u = (now - g.born) / GhostLife;
                if (u >= 1f) { g.on = false; continue; }
                block.SetFloat("_Alpha", GhostAlpha * (1f - u));
                Graphics.DrawMesh(g.mesh, g.at, GhostMaterial, layer, null, 0, block, false, false);
                if (gunMesh) for (int s = 0; s < gunMesh.subMeshCount; s++) Graphics.DrawMesh(gunMesh, g.gunAt, GhostMaterial, layer, null, s, block, false, false);
                if (slideMesh) Graphics.DrawMesh(slideMesh, g.slideAt, GhostMaterial, layer, null, 0, block, false, false);
            }
        }

        static Material GhostMaterial { get { if (!ghostMat) ghostMat = Resources.Load<Material>(OzHeroSquadView.PrefabDir + "M_EvilZod_Ghost"); return ghostMat; } }

        /// <summary>OpenZ/Beam _Color takes a linear value through SetVector, as OpenZFxPlayer.SetLinear: SetColor would gamma-convert the HDR value (6 arrives near 52).</summary>
        static void SetBeam(Material m, Color linear) => m.SetVector("_Color", new Vector4(linear.r, linear.g, linear.b, linear.a));

        void EnsureTrails()
        {
            if (ribbon) return;
            var sh = Shader.Find("OpenZ/Beam"); if (!sh) return;
            if (!ribbonMat) { ribbonMat = new Material(sh) { name = "M_EvilZod_Ribbon (runtime)" }; SetBeam(ribbonMat, RibbonCore); }
            if (!eyeMat) { eyeMat = new Material(sh) { name = "M_EvilZod_EyeLine (runtime)" }; SetBeam(eyeMat, EyeLine); }
            TrailRenderer Make(string n, Transform parent, Vector3 local, float time, float w0, float w1, Material m, out Transform t)
            {
                var go = new GameObject(n); t = go.transform; t.SetParent(parent, false); t.localPosition = local;
                var tr = go.AddComponent<TrailRenderer>();
                tr.sharedMaterial = m; tr.time = time; tr.minVertexDistance = 0.25f; tr.numCapVertices = 2; tr.emitting = false;
                tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; tr.receiveShadows = false;
                tr.widthCurve = new AnimationCurve(new Keyframe(0f, w0), new Keyframe(1f, w1));
                var c = new Gradient(); c.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
                tr.colorGradient = c;
                return tr;
            }
            float lift = 2.7f;
            ribbon = Make("EvilZodRibbon", transform, Vector3.up * lift, 0.26f, 0.7f, 0.03f, ribbonMat, out ribbonT);
            var hf = body.HeadFrontBone;
            eyeL = Make("EvilZodEyeL", hf, Vector3.zero, 0.40f, 0.06f, 0.01f, eyeMat, out eyeLT);
            eyeR = Make("EvilZodEyeR", hf, Vector3.zero, 0.40f, 0.06f, 0.01f, eyeMat, out eyeRT);
            float sc = Mathf.Max(0.01f, hf.lossyScale.x), s = 0.05f * OzEvilZodView.ModelScale / sc;
            eyeLT.localPosition = Vector3.right * s; eyeRT.localPosition = -Vector3.right * s;
        }

        void SetTrails(bool on)
        {
            bool look = OzEvilZodDashLook.On, eyes = !look || OzEvilZodDashLook.EyeLines;          // 1.3 row 73: the new core streak replaces the chest ribbon
            if (ribbon) { ribbon.emitting = on && !look; if (on) ribbon.Clear(); }
            if (eyeL) { eyeL.emitting = on && eyes; if (on) eyeL.Clear(); }
            if (eyeR) { eyeR.emitting = on && eyes; if (on) eyeR.Clear(); }
        }

        void OnDestroy()
        {
            if (ghosts != null) foreach (var g in ghosts) if (g.mesh) Destroy(g.mesh);
        }
    }
}
