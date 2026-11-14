// OpenZ (GPL-3.0) - version 1.2, the blue side's cutscenes: the stage camera of the evil HQ (OzEvilStage).
//  - SINGLES of the speaker most of the time;
//  a two-shot only where both react together.
//  - EvilZod from a LOW camera looking up at him (towering); BS-01 from slightly ABOVE looking down at him.
//  - Every framing is fitted every frame to the character's REAL bones (head, hat top, chest, hips, feet) for the feed's aspect, so
//    nobody is cut in half: a framing names the part of the body it shows (full / medium = from the belt / close = head and
//    shoulders) and the margins round it; the aim and the distance are smoothed (a robot that leans or bows is followed, not jerked).
//  - CRASH ZOOMS on the strong words (beat "crash": a = strength, hold = seconds, 0 = until the next cut): the commander's under-damped
//    zoom spring (snap in, a small overshoot, hold, ease back) with the aim sliding to the face, and a kick.
//  - The commander's hand-held field camera: layered sway + tremor, a footstep bob, the operator's re-frames, kicks on the big moments.
//    Reduced motion keeps 15 %.
// Framings: two / two_tight / two_wide (both), ez_full / ez_med / ez_close / ez_gun / ez_point (the board) / ez_doors / ez_press (the
// button on its stand) / bomb_reveal, sk_full / sk_med / sk_close / sk_box (with the button on its stand). A beat cuts (a = 0) or eases
// (a = seconds); "lock" freezes the picture where it is (a robot walking off leaves the picture); "push" dollies in slowly over the shot.
using UnityEngine;

namespace OpenZ
{
    public sealed partial class OzEvilStage
    {
        /// <summary>A framing: the stage-local point the lens aims at, the distance, the field of view, the elevation of the camera seen from
        /// that point (deg: below 0 = the camera is lower and looks UP), its heading (deg about the stage's up axis, 0 = straight from +Z).</summary>
        struct Framing { public Vector3 look; public float dist, fov, elev, yaw; public bool wide; public int subject; }

        string camName = "two"; Framing camFrom, camNow, camLockF; bool camLockOn; float camT0 = -10f, camDur, camKick, camReframeAt, camReframeT0; Vector2 reframeFrom, reframeTo, reframeCur;
        Vector3 camLookS, camLookVel; float camDistS, camDistVel, camYawS, camYawVel, camElevS, camElevVel; bool camSnap = true;
        float camFovMul = 1f, camFovVel, camPunchUntil = -1f, camPunchK = 1f, camPushT0 = -10f, camPushDur, camPushK;
        /// <summary>Hand-held amount in the window / on the screen (1 = the commander's transmission look, ZodStage.handheldAmount).</summary>
        public static float HandheldWindow = 1.0f, HandheldScreen = 0.5f;
        /// <summary>The crash zoom: the field of view multiplier at strength 1 (0.58 = 30 -> 17 deg) and the aim's slide to the face.</summary>
        public static float CrashFov = 0.58f;
        /// <summary>The low angle on EvilZod / the high angle on BS-01 (deg of camera elevation seen from the aim point).</summary>
        public static float EzLowDeg = -13f, SkHighDeg = 17f;

        void ResetCamera()
        {
            camName = "two"; camLockOn = false; camNow = camFrom = Resolve("two"); camT0 = -10f; camDur = 0f; camKick = 0f; camReframeAt = 0f;
            reframeCur = reframeFrom = reframeTo = Vector2.zero; camSnap = true; camFovMul = 1f; camFovVel = 0f; camPunchUntil = -1f; camPushT0 = -10f;
        }

        void CamBeat(OzBlueBeat b)
        {
            if (b.beat == "kick") { Kick(Mathf.Max(0.2f, b.a)); return; }
            if (b.beat == "lock") { camLockF = camNow; camLockOn = true; camDur = 0f; return; }           // the picture stays where it is now
            if (b.beat == "crash") { Crash(b.a > 0f ? b.a : 1f, b.hold); return; }
            if (b.beat == "push") { camPushT0 = clock; camPushDur = Mathf.Max(0.5f, b.hold > 0f ? b.hold : 3f); camPushK = b.a > 0f ? b.a : 0.1f; return; }
            if (!Known(b.beat)) { Debug.LogWarning("[EvilZod] unknown camera framing " + b.beat); return; }
            camFrom = camNow; camLockOn = false; camName = b.beat; camT0 = clock; camDur = Mathf.Max(0f, b.a);
            if (camDur <= 0f)
            {   // a cut: the new picture at once, the zoom and the push of the last shot gone (as the commander's SnapFraming)
                camSnap = true; camFovMul = 1f; camFovVel = 0f; camPunchUntil = -1f; camPushT0 = -10f;
                ezLookInit = false; skLookInit = false;                                                 // a head still turning finishes its turn under the cut (r3: the hat brim hid his face for 0.3 s after the cut on "EXCELLENT!")
            }
        }
        public bool CamLocked => camLockOn;
        static bool Known(string n) => n == "two" || n == "two_tight" || n == "two_wide" || n == "ez_full" || n == "ez_wide" || n == "ez_med" || n == "ez_face" || n == "ez_close" || n == "ez_gun" || n == "ez_gunclose" || n == "ez_point"
                                     || n == "ez_doors" || n == "ez_press" || n == "two_box" || n == "bomb_reveal" || n == "sk_full" || n == "sk_med" || n == "sk_close" || n == "sk_box";

        /// <summary>A short jolt of the hand-held camera (0..1), gone in about 0.4 s (CALM SCREEN keeps 15 %).</summary>
        public void Kick(float k) { camKick = Mathf.Clamp01(Mathf.Max(camKick, k * (OzMotion.Reduced ? 0.15f : 1f))); }
        /// <summary>The crash zoom: snap in on the subject's face (strength 1 = CrashFov), held `hold` s (0 = until the next cut).</summary>
        public void Crash(float strength, float hold)
        {
            camPunchK = Mathf.Clamp(strength, 0.2f, 1.4f); camPunchUntil = hold > 0f ? clock + hold : clock + 99f;
            Kick(0.3f * camPunchK);
        }

        // ------------------------------------------------------------------------------------------------ the characters' measures (stage-local)
        Vector3 L(Vector3 world) => transform.InverseTransformPoint(world);
        float FeedAspect => cam && cam.aspect > 0.2f ? cam.aspect : 4f / 3f;
        /// <summary>EvilZod's top (hat), face, chest, hips and feet, stage-local, from his bones (the pose of this frame).</summary>
        void EzPoints(out Vector3 top, out Vector3 face, out Vector3 chest, out Vector3 hips, out Vector3 feet)
        {
            float H = EzHeight; Vector3 p = EzPos;
            if (ez == null || !ez.Ok) { feet = p; hips = p + Vector3.up * 0.5f * H; chest = p + Vector3.up * 0.72f * H; face = p + Vector3.up * 0.88f * H; top = p + Vector3.up * H; return; }
            var r = ez.Rig;
            // the face = his eyes: 40 % of the way from the head bone up to the hat top, a little ahead along the head's forward (HeadFront
            // sits at the jaw: the round 1 crash zooms centred his chin and lost the eyes at the frame's edge)
            Vector3 hatW = r.HatTop ? r.HatTop.position : r.Head.position + Vector3.up * 0.35f * H / 2.3f;
            Vector3 fwdW = r.HeadFront ? Vector3.ProjectOnPlane(r.HeadFront.position - r.Head.position, Vector3.up).normalized : r.Fwd;
            face = L(Vector3.Lerp(r.Head.position, hatW, 0.4f) + fwdW * 0.08f * H / 2.3f);
            top = L(hatW) + Vector3.up * 0.04f;
            if (top.y < face.y + 0.1f * H) top = face + Vector3.up * 0.14f * H;
            chest = L(r.Spine2 ? r.Spine2.position : r.Head.position - Vector3.up * 0.25f * H);
            hips = L(r.Hips.position);
            feet = new Vector3(hips.x, p.y, hips.z);
        }
        /// <summary>EvilZod's whole head, stage-local: the hat's top and his chin (the neck bone a little up: the jaw plate's lower edge, the
        /// </summary>
        void EzHead(out Vector3 top, out Vector3 chin)
        {
            float H = EzHeight; Vector3 p = EzPos;
            if (ez == null || !ez.Ok) { chin = p + Vector3.up * 0.81f * H; top = p + Vector3.up * 1.0f * H; return; }
            var r = ez.Rig; float k = H / 2.3f;
            top = (r.HatTop ? L(r.HatTop.position) : L(r.Head.position) + Vector3.up * 0.36f * k) + Vector3.up * 0.04f * k;   // + the brim's rise
            chin = r.Neck ? L(r.Neck.position) + Vector3.up * 0.02f * k : L(r.Head.position) - Vector3.up * 0.07f * k;
        }
        /// <summary>The share of the frame height kept free above the hat and below the chin in a head shot.</summary>
        public static float HeadMargin = 0.12f;

        void SkPoints(out Vector3 top, out Vector3 eyes, out Vector3 hips, out Vector3 feet)
        {
            float h = SkHeight; Vector3 p = SkPos;
            if (sk == null || !sk.Ok || !sk.Go.activeInHierarchy) { feet = p; hips = p + Vector3.up * 0.4f * h; eyes = p + Vector3.up * 0.75f * h; top = p + Vector3.up * h; return; }
            var r = sk.Rig;
            eyes = eyeL != null && eyeR != null ? L((eyeL.root.position + eyeR.root.position) * 0.5f) : L(r.Head.position) + Vector3.up * 0.1f * h;
            top = eyes + Vector3.up * 0.30f * h;                                                           // the dome and the dish over the eyes
            hips = L(r.Hips.position);
            feet = new Vector3(hips.x, p.y, hips.z);
        }

        /// <summary>The distance that fits a span of `h` m high and `w` m wide in the feed (vertical fov `fov`).</summary>
        float FitDist(float h, float w, float fov)
        {
            float tv = Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad), th = tv * FeedAspect;
            return Mathf.Max(h * 0.5f / tv, w * 0.5f / th);
        }
        /// <summary>A single: the span from `lo` to `hi` (stage-local y) round the subject's x / z, `w` m wide, from `elev` deg, heading `yaw`.</summary>
        Framing Single(Vector3 at, float lo, float hi, float w, float fov, float elev, float yaw, int subject)
        {
            var look = new Vector3(at.x, (lo + hi) * 0.5f, at.z);
            // a camera below the aim sees the top half of the span from further away: a little more room at the top
            float extra = elev < 0f ? (hi - lo) * Mathf.Clamp01(-elev / 40f) * 0.25f : 0f;
            look.y += extra * 0.5f;
            return new Framing { look = look, dist = FitDist(hi - lo + extra, w, fov), fov = fov, elev = elev, yaw = yaw, subject = subject };
        }
        Framing Pair(Vector3 a, Vector3 b, float lo, float hi, float extraW, float fov, float elev, float yaw)
        {
            // both in the picture: the span covers both robots seen from the heading (their spread across the lens + margins)
            Vector3 right = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
            float spread = Mathf.Abs(Vector3.Dot(a - b, right));
            var mid = (a + b) * 0.5f; mid.y = (lo + hi) * 0.5f;
            return new Framing { look = mid, dist = FitDist(hi - lo, spread + extraW, fov), fov = fov, elev = elev, yaw = yaw, wide = true, subject = 0 };
        }

        Framing Resolve(string n) { var f = ResolveHq(n); return LavaWalkOn ? LavaFrame(n, f) : f; }      // level 7: his own framings on the lava (OzEvilStage.L07Walk.cs)
        Framing ResolveHq(string n)
        {
            EzPoints(out var eTop, out var eFace, out var eChest, out var eHips, out var eFeet);
            SkPoints(out var sTop, out var sEyes, out var sHips, out var sFeet);
            float H = EzHeight, h = SkHeight;
            switch (n)
            {
                // EvilZod: low camera, looking up (towering); a little to his right side of the lens so the face reads three-quarter
                case "ez_full": return Single(eChest, eFeet.y - 0.06f, eTop.y + 0.24f, 1.5f, 30f, EzLowDeg + 3f, 8f, 1);
                case "ez_med": return Single(eChest, eHips.y - 0.10f, eTop.y + 0.40f, 1.45f, 30f, EzLowDeg, 16f, 1);
                case "ez_wide":
                    {   // the arms spread ("ONE STEP CLOSER TO WORLD DOMINATION!"): from the belt up, 2.4 m wide, the aim a little to his left side
                        // of the lens so the little one beside him stays out of the picture
                        var at = eChest + new Vector3(-0.25f, 0f, 0f);
                        return Single(at, eHips.y - 0.15f, eTop.y + 0.30f, 2.4f, 30f, EzLowDeg, 6f, 1);
                    }
                case "ez_face":
                    {
                        // a raised fist, a point, a shrug or the arms spread stay out of the picture
                        // (his hand gestures are not made under these shots at all: blue_scenes.json, tools/redo_beats.py FACE_DROP; from either side a
                        // raised fist or the fist at the lens showed at an edge: proof/redo/f4_brief_feeds.jpg, f5_brief_feeds.jpg)
                        // frame free above and below
                        EzHead(out var hTop, out var hChin);
                        float span = hTop.y - hChin.y, m = span * HeadMargin / (1f - 2f * HeadMargin);
                        var mid = (hTop + hChin) * 0.5f;
                        return Single(mid, hChin.y - m, hTop.y + m, 0.62f, 26f, EzLowDeg + 4f, 8f, 1);
                    }
                case "ez_close": return Single(eFace, eChest.y - 0.12f, eTop.y + 0.18f, 0.95f, 28f, EzLowDeg + 2f, 22f, 1);   // from BS-01's side of the lens: his face, turned down at the little one, comes to the lens
                case "ez_gunclose": return Single(eChest, eChest.y - 0.32f, eTop.y + 0.16f, 1.0f, 28f, EzLowDeg + 2f, 12f, 1);   // "Fine!": the push in on his face and the gun's top
                case "ez_gun": return Single(eChest, eHips.y + 0.02f, eTop.y + 0.22f, 1.30f, 30f, EzLowDeg + 1f, 14f, 1);
                case "ez_point":
                    {   // "THAT RED FORT!": wide and low: EvilZod whole at the right, his arm out to the board at the left (+X is screen left), BS-01
                        // whole between them (he stands there; round 2 cut him at the bottom edge and EvilZod at the right)
                        Vector3 board = BoardPoint; var at = new Vector3(Mathf.Lerp(eChest.x, board.x, 0.33f), 0f, Mathf.Lerp(eChest.z, board.z, 0.15f));
                        var f = Single(at, eFeet.y - 0.06f, eTop.y + 0.32f, Mathf.Abs(board.x - eChest.x) + 2.0f, 32f, -5f, 4f, 1); f.wide = true; return f;
                    }
                case "ez_doors":
                    {   // EvilZod beside the doors: him whole at the right, the doors in the middle
                        var at = new Vector3(Mathf.Lerp(eChest.x, 0f, 0.5f), 0f, Mathf.Lerp(eChest.z, -3.4f, 0.35f));
                        var f = Single(at, eFeet.y - 0.05f, Mathf.Max(eTop.y, 3.1f) + 0.2f, Mathf.Abs(eChest.x) + 2.2f, 34f, -4f, 3f, 1); f.wide = true; return f;
                    }
                case "bomb_reveal":
                    {   // the World-Domination-inator in the alcove, centred, EvilZod out of the picture
                        var f = Single(new Vector3(0.05f, 0f, -4.6f), -0.05f, 3.0f, 5.4f, 34f, -3f, 6f, 0); f.wide = true; return f;
                    }
                case "ez_press":
                    {   // the slam: EvilZod whole, the button box on its stand and BS-01 behind it whole (he covers his eyes), a little low
                        Vector3 dome = buttonShown ? L(ButtonTop) : DomePoint;
                        var f = Pair(eChest, sEyes, Mathf.Min(sFeet.y, eFeet.y) - 0.06f, eTop.y + 0.26f, 1.0f, 32f, -5f, 14f);
                        f.look.x = Mathf.Lerp(f.look.x, dome.x, 0.2f); f.subject = 1; return f;
                    }
                // BS-01: a little above him, looking down; his face (the two eyes) to the lens
                // (from EvilZod's side of the lens, -X: his face, turned a little toward EvilZod, comes to the lens and EvilZod stays out of the
                // picture at its right edge; the aim a little to his far side)
                case "sk_full": return Single(sHips + new Vector3(0.12f, 0f, 0f), sFeet.y - 0.05f, sTop.y + 0.14f, 1.1f, 32f, SkHighDeg, -18f, 2);
                case "sk_med": return Single(sHips + new Vector3(0.10f, 0f, 0f), sFeet.y + 0.12f * h, sTop.y + 0.12f, 0.95f, 32f, SkHighDeg, -18f, 2);
                case "sk_close": return Single(sEyes + new Vector3(0.05f, 0f, 0f), sEyes.y - 0.36f * h, sTop.y + 0.08f, 0.70f, 30f, SkHighDeg - 3f, -16f, 2);
                case "two_box":
                    {   // "Right here, sir!": both whole, BS-01 behind the box on its stand at the left, EvilZod turning to it at the right
                        var f = Pair(eChest, sEyes, Mathf.Min(sFeet.y, eFeet.y) - 0.06f, eTop.y + 0.45f, 1.4f, 32f, -4f, -10f); f.subject = 2; return f;
                    }
                case "sk_box":
                    {   // "Right here, sir!": BS-01 and the box on its stand before him (EvilZod's leg kept out at the right)
                        Vector3 box = buttonShown ? L(ButtonTop) : DomePoint;
                        var at = new Vector3(Mathf.Lerp(sEyes.x, box.x, 0.25f) + 0.10f, 0f, Mathf.Lerp(sEyes.z, box.z, 0.25f));
                        var f = Single(at, Mathf.Min(box.y - 0.28f, sFeet.y + 0.25f), sTop.y + 0.14f, 0.9f, 32f, SkHighDeg - 5f, -6f, 2); f.wide = true; return f;
                    }
                // both: a neutral camera at about EvilZod's belt (he still towers over the little one)
                case "two_tight": return Pair(eChest, sEyes, Mathf.Min(sFeet.y + 0.35f * h, eHips.y - 0.1f), eTop.y + 0.14f, 1.1f, 32f, -6f, -6f);
                case "two_wide": return Pair(eChest, sEyes, eFeet.y - 0.06f, eTop.y + 0.3f, 1.8f, 34f, -3f, -4f);
                default: if (LavaWalkOn) return LavaTwo(eChest, sEyes, eTop.y); return Pair(eChest, sEyes, Mathf.Min(sFeet.y, eFeet.y) - 0.20f, eTop.y + 0.18f, 1.25f, 32f, -5f, -6f);   // the feet clear of the bottom edge
            }
        }

        static Framing Mix(Framing a, Framing b, float k) => new Framing
        {
            look = Vector3.Lerp(a.look, b.look, k), dist = Mathf.Lerp(a.dist, b.dist, k), fov = Mathf.Lerp(a.fov, b.fov, k),
            elev = Mathf.Lerp(a.elev, b.elev, k), yaw = Mathf.Lerp(a.yaw, b.yaw, k), wide = k < 0.5f ? a.wide : b.wide, subject = k < 0.5f ? a.subject : b.subject
        };

        static float N(float t, float freq, float seed) => (Mathf.PerlinNoise(t * freq, seed) - 0.5f) * 2f;

        /// <summary>The face the crash zoom slides to (stage-local): EvilZod's or BS-01's, by the framing's subject.</summary>
        Vector3 FaceOf(int subject)
        {
            if (subject == 2) { SkPoints(out _, out var e, out _, out _); return e + Vector3.up * 0.04f; }
            EzHead(out var t, out var c); return (t + c) * 0.5f;                                          // the middle of his whole head (hat to chin)
        }

        void PlaceCamera(float dt)
        {
            float t = clock;
            Framing want = camLockOn ? camLockF : Resolve(camName);
            if (camDur > 0f && t - camT0 < camDur) { float u = Mathf.Clamp01((t - camT0) / camDur); want = Mix(camFrom, want, u * u * (3f - 2f * u)); }
            // the aim and the distance follow the bones smoothly (a lean / a bow / a step is followed, never jerked); a cut snaps
            if (camSnap || dt <= 0f && camDistS <= 0f) { camLookS = want.look; camDistS = want.dist; camYawS = want.yaw; camElevS = want.elev; camLookVel = Vector3.zero; camDistVel = camYawVel = camElevVel = 0f; camSnap = false; }
            else if (dt > 0f)
            {
                camLookS = Vector3.SmoothDamp(camLookS, want.look, ref camLookVel, 0.16f, 8f, dt);
                camDistS = Mathf.SmoothDamp(camDistS, want.dist, ref camDistVel, 0.2f, 20f, dt);
                camYawS = Mathf.SmoothDamp(camYawS, want.yaw, ref camYawVel, 0.2f, 200f, dt);
                camElevS = Mathf.SmoothDamp(camElevS, want.elev, ref camElevVel, 0.2f, 200f, dt);
            }
            camNow = want; camNow.look = camLookS; camNow.dist = camDistS; camNow.yaw = camYawS; camNow.elev = camElevS;
            float hh = (fullScreen ? HandheldScreen : HandheldWindow) * (OzMotion.Reduced ? 0.15f : 1f);
            bool punching = t < camPunchUntil;
            if (dt > 0f)
            {
                camKick = Mathf.MoveTowards(camKick, 0f, dt * 2.5f);
                // the crash zoom: the commander's zoom rocker (under-damped spring: snap in with a small overshoot, softer back out)
                float goal = punching ? Mathf.LerpUnclamped(1f, CrashFov, camPunchK) : 1f;
                if (OzMotion.Reduced) goal = Mathf.Lerp(1f, goal, 0.35f);
                float w = goal < camFovMul ? 24f : 13f, zeta = 0.62f;
                int steps = Mathf.CeilToInt(dt / 0.008f); float hs = dt / steps;
                for (int i = 0; i < steps; i++) { camFovVel += (-2f * zeta * w * camFovVel - w * w * (camFovMul - goal)) * hs; camFovMul += camFovVel * hs; }
                camFovMul = Mathf.Clamp(camFovMul, 0.4f, 1.6f);
                if (t >= camReframeAt)
                {   // the operator re-frames every 2-4.5 s: a quick small correction (0.35 s, eased)
                    reframeFrom = reframeCur; reframeTo = new Vector2(Random.Range(-1f, 1f) * 0.9f, Random.Range(-1f, 1f) * 0.55f);
                    camReframeT0 = t; camReframeAt = t + Random.Range(2.0f, 4.5f);
                }
                reframeCur = Vector2.Lerp(reframeFrom, reframeTo, Mathf.SmoothStep(0f, 1f, (t - camReframeT0) / 0.35f));
            }
            // the zoom never cuts his head: on EvilZod the field of view stays wide enough for his whole head (hat top to chin) with
            if (camNow.subject == 1 && camDistS > 0.05f)
            {
                EzHead(out var hT, out var hC);
                float need = (hT.y - hC.y) / (1f - 2f * HeadMargin);
                float dHead = Vector3.Distance(transform.InverseTransformPoint(cam.transform.position), (hT + hC) * 0.5f);
                float minFov = 2f * Mathf.Atan(need * 0.5f / Mathf.Max(0.2f, dHead)) * Mathf.Rad2Deg;
                float minMul = Mathf.Min(1f, minFov / Mathf.Max(1f, camNow.fov));
                if (camFovMul < minMul) { camFovMul = minMul; if (camFovVel < 0f) camFovVel = 0f; }
            }
            // the aim slides toward the face while zoomed in (the zoom's share of the way)
            float zoomK = Mathf.Clamp01((1f - camFovMul) / Mathf.Max(0.05f, 1f - CrashFov));
            Vector3 lookLocal = camLookS;
            if (zoomK > 0f && camNow.subject > 0) lookLocal = LavaWalkOn && camNow.subject == 1 ? LavaCrashAim(camLookS, zoomK) : Vector3.Lerp(camLookS, FaceOf(camNow.subject), 0.85f * zoomK);
            float push = camPushT0 >= 0f ? camPushK * Mathf.SmoothStep(0f, 1f, (t - camPushT0) / camPushDur) : 0f;
            Vector3 look = transform.TransformPoint(lookLocal);
            float el = camElevS * Mathf.Deg2Rad, yw = camYawS * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Sin(yw) * Mathf.Cos(el), Mathf.Sin(el), Mathf.Cos(yw) * Mathf.Cos(el));
            Vector3 posLocal = lookLocal + dir * (camDistS * (1f - push));
            if (posLocal.y < 0.28f) posLocal.y = 0.28f;                                                     // never under the floor
            Vector3 pos = transform.TransformPoint(posLocal);
            if (LavaWalkOn && camNow.subject == 1 && zoomK > 0f) look = LavaCrashTilt(look, pos, zoomK);   // level 7: his landed crash zoom on ez_med looks a little lower (OzEvilStage.L07Walk.cs, row 61)
            var baseRot = Quaternion.LookRotation(look - pos, transform.up);
            // hand-held noise (the commander's layers): slow sway + faster tremor, a footstep bob, the re-frame; zoomed in = a little calmer
            float zoomCalm = Mathf.Lerp(0.7f, 1f, Mathf.Clamp01((camFovMul - 0.6f) / 0.4f));
            float pitch = (0.75f * N(t, 0.40f, 3.3f) + 0.45f * N(t, 2.3f, 4.4f) + reframeCur.y) * hh * zoomCalm;
            float yaw = (0.9f * N(t, 0.33f, 6.6f) + 0.45f * N(t, 2.1f, 7.7f) + reframeCur.x) * hh * zoomCalm;
            float roll = (1.1f * N(t, 0.25f, 11.1f) + 0.45f * N(t, 1.6f, 12.2f)) * hh;
            Vector3 off = new Vector3(0.020f * N(t, 0.35f, 1.3f) + 0.007f * N(t, 1.7f, 5.1f),
                                      0.013f * N(t, 0.30f, 2.7f) + 0.005f * N(t, 1.9f, 8.3f) + 0.005f * Mathf.Sin(t * 2.2f * Mathf.PI),
                                      0.018f * N(t, 0.22f, 9.1f)) * hh;
            float k = camKick * camKick;
            if (k > 0f)
            {
                pitch += k * 0.9f * Mathf.Sin(t * 21f); yaw += k * 0.7f * Mathf.Sin(t * 25f + 1.1f); roll += k * 0.6f * Mathf.Sin(t * 19f + 2.3f);
                off += new Vector3(Mathf.Sin(t * 22f), Mathf.Sin(t * 26f + 0.7f), 0f) * 0.004f * k;
            }
            cam.transform.position = pos + baseRot * off;
            cam.transform.rotation = baseRot * Quaternion.Euler(pitch, yaw, roll);
            cam.fieldOfView = camNow.fov * camFovMul;
            if (dof != null && !LavaWalkOn) { float dd = camDistS, far = RunnersRunning ? 4f : 1.2f; dof.gaussianStart.value = dd + far; dof.gaussianEnd.value = dd + 6f + far; }   // the fleeing clones stay sharp
        }

        /// <summary>The camera's world position and the stage-local point it looks at (the robots look into the lens with these).</summary>
        public Vector3 CamPos => cam ? cam.transform.position : transform.TransformPoint(new Vector3(0f, 1.5f, 3.5f));
        public string CamName => camName;
        /// <summary>The Play checks: the crash zoom's field of view multiplier now (1 = none).</summary>
        public float CamZoom => camFovMul;
    }
}
