// OpenZ (GPL-3.0) - version 1.2, the blue side's cutscenes: the props of the evil HQ (OzEvilStage).
//    (legacy_nuke_blue: the texture the strike itself uses for a blue attacker), on a steel cradle in the alcove behind the doors.
//    A box with a painted nose stands in only if the model is missing.
//    EvilZod's fist lands; its lamp flashes on the press. Carried in by the sidekick and set down on the BUTTON STAND before EvilZod
//    (fix 2: nothing changes hands, the box never turns); EvilZod bows to it and his fist lands on the dome at "fireworks".
//  - The button stand: a steel column with a plate top at StandTop, placed where EvilZod's right fist reaches the dome in his bow and
//    where the sidekick can set the box down from his carry (OzEvilStage.cs: the marks).
//    No rack, no walk, no grab; in LOSE the cut on "Fine, I'll do it myself!" finds it in his hands (GunReady), held across
//    the body (port arms: the barrel up across the chest to his left, the right fist on the grip, the left hand on the fore-end), and
//    he pumps it once (Shotgun_Pump slides to Shotgun_PumpBack and back; the slide's two stops land on the clip's two clacks). The rack
//    code of fix 2 is set aside (k75b_blue_cutscenes/_set_aside/redo/originals/OzEvilStage.Props.cs).
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace OpenZ
{
    public sealed partial class OzEvilStage
    {
        // ------------------------------------------------------------------------------------------------ the button
        Transform buttonRoot, buttonDome, buttonHolder, standRoot, standHolder; Light buttonLamp; Material domeMat; float domePress, lampK; bool buttonShown;
        /// <summary>Box 0.44 x 0.24 x 0.44 m (sized to the sidekick's short arms: he hugs it at his chest with the hands on its rear top
        /// corners), the dome 0.22 m across on the BACK half of the top (DomeZ: 9 cm behind the centre, toward the sidekick when it stands
        /// on the stand); hazard bands on all four sides so it reads the same from any side; the holder's point is the box's bottom centre.</summary>
        public const float BoxW = 0.44f, BoxH = 0.24f, BoxD = 0.44f, DomeR = 0.11f, DomeZ = -0.09f, DomeTravel = 0.028f;
        /// <summary>The dome's top in the box's frame (from the bottom centre): the fist lands FistOver m above it.</summary>
        public static readonly Vector3 DomeTopLocal = new Vector3(0f, BoxH + DomeR * 0.78f, DomeZ);
        public const float FistOver = 0.07f;
        public Vector3 ButtonTop => buttonRoot ? buttonRoot.TransformPoint(new Vector3(0f, DomeTopLocal.y - domePress, DomeZ)) : transform.position;
        public bool ButtonShown => buttonShown;
        public Transform ButtonRoot => buttonRoot;
        /// <summary>The stand's top (the box's bottom when it stands there), m above the floor.</summary>
        public const float StandTop = 0.48f;

        void BuildProps()
        {
            // the button
            buttonRoot = new GameObject("NukeButton").transform; buttonRoot.SetParent(transform, false);
            var gm = Lit(new Color32(48, 52, 58, 255), 0.4f, 0.6f); var dark = Lit(new Color32(18, 20, 23, 255), 0.35f, 0.4f);
            var hazard = Lit(new Color32(214, 170, 38, 255), 0.35f, 0.1f); var black = Lit(new Color32(14, 14, 15, 255), 0.3f, 0.2f);
            Box("Body", new Vector3(0f, BoxH * 0.5f, 0f), new Vector3(BoxW, BoxH, BoxD), gm, buttonRoot);
            Box("Top", new Vector3(0f, BoxH + 0.012f, 0f), new Vector3(BoxW * 1.02f, 0.024f, BoxD * 1.02f), dark, buttonRoot);
            for (int side = 0; side < 4; side++)
            {   // the hazard band round all four sides (fix 2: the box is seen from EvilZod's side and from the lens)
                var q = Quaternion.Euler(0f, 90f * side, 0f);
                for (int i = 0; i < 6; i++)
                {
                    var r = Box("Stripe" + side + "_" + i, Vector3.zero, new Vector3(BoxW / 6f, 0.07f, 0.006f), i % 2 == 0 ? hazard : black, buttonRoot, false);
                    r.transform.localRotation = q; r.transform.localPosition = q * new Vector3(-BoxW * 0.5f + (i + 0.5f) * BoxW / 6f, BoxH * 0.5f, BoxD * 0.5f + 0.003f);
                }
            }
            Box("HandleL", new Vector3(BoxW * 0.5f + 0.03f, BoxH * 0.55f, 0f), new Vector3(0.05f, 0.03f, 0.16f), dark, buttonRoot);
            Box("HandleR", new Vector3(-BoxW * 0.5f - 0.03f, BoxH * 0.55f, 0f), new Vector3(0.05f, 0.03f, 0.16f), dark, buttonRoot);
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder); ring.name = "Ring"; Destroy(ring.GetComponent<Collider>());
            ring.transform.SetParent(buttonRoot, false); ring.transform.localPosition = new Vector3(0f, BoxH + 0.03f, DomeZ); ring.transform.localScale = new Vector3(DomeR * 2.3f, 0.03f, DomeR * 2.3f);
            Dress(ring, Lit(new Color32(150, 155, 162, 255), 0.6f, 0.9f), true);
            buttonDome = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform; buttonDome.name = "Dome"; Destroy(buttonDome.GetComponent<Collider>());
            buttonDome.SetParent(buttonRoot, false); buttonDome.localPosition = new Vector3(0f, BoxH + 0.03f, DomeZ); buttonDome.localScale = new Vector3(DomeR * 2f, DomeR * 1.6f, DomeR * 2f);
            domeMat = Glow(new Color32(36, 92, 220, 255), new Color(0.25f, 0.5f, 1f), 0.9f); domeMat.SetFloat("_Smoothness", 0.75f);
            Dress(buttonDome.gameObject, domeMat, true);
            var lampGo = new GameObject("ButtonLamp") { layer = Layer }; lampGo.transform.SetParent(buttonRoot, false); lampGo.transform.localPosition = new Vector3(0f, BoxH + DomeR * 1.3f, DomeZ + 0.08f);
            buttonLamp = lampGo.AddComponent<Light>(); buttonLamp.type = LightType.Point; buttonLamp.color = new Color(0.4f, 0.65f, 1f); buttonLamp.range = 2.2f; buttonLamp.intensity = 0f; buttonLamp.shadows = LightShadows.None;
            buttonLamp.renderingLayerMask = (int)LightMask; var ld = buttonLamp.GetUniversalAdditionalLightData(); if (ld) { ld.renderingLayers = LightMask; ld.shadowRenderingLayers = LightMask; }
            ButtonOffstage();
            // the button stand (the nuke scene only): a foot plate, a column with a hazard collar, a plate top at StandTop; the holder's point
            // is the top's centre, its forward the sidekick's set-down facing (the box lands with its front that way)
            var steel = Lit(new Color32(60, 64, 70, 255), 0.45f, 0.7f);
            standRoot = new GameObject("ButtonStand").transform; standRoot.SetParent(transform, false);
            Box("StandFoot", new Vector3(0f, 0.02f, 0f), new Vector3(0.52f, 0.04f, 0.52f), dark, standRoot);
            Box("StandColumn", new Vector3(0f, 0.04f + (StandTop - 0.07f) * 0.5f, 0f), new Vector3(0.22f, StandTop - 0.07f, 0.22f), steel, standRoot);
            Box("StandCollar", new Vector3(0f, StandTop - 0.10f, 0f), new Vector3(0.24f, 0.05f, 0.24f), hazard, standRoot);
            Box("StandTop", new Vector3(0f, StandTop - 0.015f, 0f), new Vector3(0.52f, 0.03f, 0.52f), dark, standRoot);
            standHolder = new GameObject("StandHolder").transform; standHolder.SetParent(standRoot, false); standHolder.localPosition = new Vector3(0f, StandTop, 0f);
            standRoot.gameObject.SetActive(false);
            // the bomb on its cradle in the alcove
            var cradle = new GameObject("BombCradle").transform; cradle.SetParent(transform, false); cradle.localPosition = new Vector3(0f, 0f, -4.6f); bombCradle = cradle;
            foreach (float x in new[] { 0.95f, -0.95f })
            {
                Box("Trestle" + x, new Vector3(x, 0.36f, 0f), new Vector3(0.16f, 0.72f, 0.9f), steel, cradle);
                Box("TrestleFoot" + x, new Vector3(x, 0.04f, 0f), new Vector3(0.5f, 0.08f, 1.0f), dark, cradle);
                Box("Saddle" + x, new Vector3(x, 0.76f, 0f), new Vector3(0.18f, 0.08f, 0.7f), hazard, cradle);
            }
            var nukeLib = Resources.Load<GameObject>("OpenZ/FX/legacy_nuke");
            if (nukeLib)
            {
                var holder = new GameObject("Bomb").transform; holder.SetParent(cradle, false);
                var model = Instantiate(nukeLib, holder, false); model.name = "legacy_nuke";
                var blue = Resources.Load<Texture2D>("OpenZ/FX/legacy_nuke_blue");
                var rs = model.GetComponentsInChildren<Renderer>(true);
                var b = new Bounds(); bool any = false;
                foreach (var r in rs)
                {
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On; r.receiveShadows = true; r.renderingLayerMask = LightMask; r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                    if (blue) { var mats = r.materials; foreach (var m in mats) if (m && m.HasProperty("baseColorTexture")) m.SetTexture("baseColorTexture", blue); r.materials = mats; }
                    if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
                }
                // the model's nose is its local +Z (OpenZNukeStrike): laid along the stage's X (nose to screen left), 3.0 m long, on the saddles
                float len = Mathf.Max(0.1f, Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z)));
                float s = 3.0f / len; holder.localScale = Vector3.one * s;
                holder.localRotation = Quaternion.Euler(0f, 90f, 0f);
                var bb = new Bounds(); any = false;
                foreach (var r in rs) { if (!any) { bb = r.bounds; any = true; } else bb.Encapsulate(r.bounds); }
                Vector3 want = cradle.TransformPoint(new Vector3(0f, 0.8f + bb.extents.y, 0f));
                holder.position += want - bb.center;
                bomb = holder;
            }
            else
            {   // no model: a bomb-shaped stand-in (a blue cylinder with a cream nose and fins)
                var holder = new GameObject("Bomb").transform; holder.SetParent(cradle, false); holder.localPosition = new Vector3(0f, 1.25f, 0f);
                var bodyM = Lit(new Color32(47, 79, 168, 255), 0.5f, 0.3f); var noseM = Lit(new Color32(230, 220, 190, 255), 0.4f, 0.1f);
                var body = GameObject.CreatePrimitive(PrimitiveType.Capsule); body.name = "Body"; Destroy(body.GetComponent<Collider>()); body.transform.SetParent(holder, false);
                body.transform.localRotation = Quaternion.Euler(0f, 0f, 90f); body.transform.localScale = new Vector3(0.9f, 1.5f, 0.9f); Dress(body, bodyM, true);
                Box("Nose", new Vector3(1.25f, 0f, 0f), new Vector3(0.5f, 0.5f, 0.5f), noseM, holder);
                foreach (float a in new[] { 0f, 90f, 180f, 270f }) { var f = Box("Fin" + a, Vector3.zero, new Vector3(0.6f, 0.5f, 0.04f), bodyM, holder); f.transform.localRotation = Quaternion.Euler(a, 0f, 0f); f.transform.localPosition = f.transform.localRotation * new Vector3(-1.3f, 0.4f, 0f); }
                bomb = holder;
                Debug.LogWarning("[EvilZod] Resources/OpenZ/FX/legacy_nuke missing: a stand-in bomb");
            }
            SaveBombHome();                                                                              // the launch's home pose, flame and smoke (OzEvilStage.Launch.cs)
        }

        /// <summary>The stand in the nuke scene: at StandPos, turned to the sidekick's set-down facing; the box lands on standHolder.</summary>
        void PlaceStand(bool on)
        {
            if (!standRoot) return;
            standRoot.gameObject.SetActive(on);
            if (!on) return;
            standRoot.localPosition = StandPos; standRoot.localRotation = Quaternion.Euler(0f, SetYaw, 0f);
        }
        public Transform StandHolder => standHolder;

        /// <summary>The button out of sight (under the floor), not pressed.</summary>
        void ButtonOffstage()
        {
            if (!buttonRoot) return;
            buttonRoot.SetParent(transform, false); buttonRoot.localPosition = new Vector3(5f, -3f, 5f); buttonRoot.localRotation = Quaternion.identity;
            buttonShown = false; domePress = 0f; lampK = 0f; if (buttonDome) buttonDome.localPosition = new Vector3(0f, BoxH + 0.03f, DomeZ);
            if (buttonLamp) buttonLamp.intensity = 0f;
        }
        /// <summary>The button follows a holder (a transform whose point is the box's bottom centre, its forward the box's front).</summary>
        public void ButtonFollow(Transform holder) { buttonHolder = holder; buttonShown = holder != null; }
        /// <summary>The fist landed: the dome sinks, the lamp flashes, OnPress once.</summary>
        public void ButtonPress()
        {
            if (Pressed) return;
            Pressed = true; lampK = 1f; ContactFrame = Time.frameCount;
            Sfx("OpenZ/Zod/Sfx/zod_button_slam", 0f, 1f, "button_slam");
            Sfx("OpenZ/Audio/Sfx/sd_seat_thud_a", 0f, 0.85f, "button_thump", sfxB);
            Sfx("OpenZ/Audio/Sfx/sd_door_heavy_close_b", -3f, 1.05f, "button_thump2", sfxB);
            Kick(0.6f);
            BombLaunch();
            var cb = OnPress; OnPress = null;
            try { cb?.Invoke(); } catch (System.Exception e) { Debug.LogException(e); }
            Debug.Log($"[EvilZod] the fist hit the blue button (frame {ContactFrame}, clock {clock:F2})");
        }
        float pressReleaseAt = -1f;
        void TickButton(float dt)
        {
            if (!buttonRoot) return;
            if (buttonHolder && buttonShown) { buttonRoot.position = buttonHolder.position; buttonRoot.rotation = buttonHolder.rotation; }
            float wantPress = Pressed && (pressReleaseAt < 0f || clock < pressReleaseAt) ? DomeTravel : 0f;
            if (Pressed && pressReleaseAt < 0f) pressReleaseAt = clock + 0.5f;
            if (Pressed && pressReleaseAt > 0f && clock >= pressReleaseAt && domePress > 0.001f && domePress <= DomeTravel && wantPress == 0f && !releaseClicked)
            { releaseClicked = true; Sfx("OpenZ/Zod/Sfx/zod_button_slam", -10f, 1.1f, "button_release", sfxB); }
            domePress = Mathf.MoveTowards(domePress, wantPress, dt / (wantPress > domePress ? 0.05f : 0.12f) * DomeTravel);
            if (buttonDome) buttonDome.localPosition = new Vector3(0f, BoxH + 0.03f - domePress, DomeZ);
            lampK = Mathf.MoveTowards(lampK, 0f, dt / 0.9f);
            float glow = buttonShown ? 0.9f + 5f * lampK + 0.25f * Mathf.Sin(clock * 6f) : 0f;
            if (domeMat) domeMat.SetVector("_EmissionColor", (Vector4)(new Color(0.25f, 0.5f, 1f) * glow));
            if (buttonLamp) buttonLamp.intensity = buttonShown ? 0.8f + 7f * lampK : 0f;
        }
        bool releaseClicked;

        // ------------------------------------------------------------------------------------------------ the gun
        Transform gun, gunPump, gunPumpBack, gunPumpGrip, gunMuzzle; Vector3 gunHomePos, gunHomeScale, pumpRest; Quaternion gunHomeRot; bool gunInHand;
        float pumpAt = -10f; Vector3 pumpBackLocal;
        Vector3 gunBarrelInSocket = Vector3.forward, gunUpInSocket = Vector3.up; Quaternion socketInHand = Quaternion.identity; bool gunAxesOk;
        /// <summary>The gun hold (his frame at 2.30 m, scaled): the grip at his right waist, a little forward; the barrel's direction (x his right,
        /// y up, z forward: up across the chest to his left) and the gun's top facing forward and up.</summary>
        public static Vector3 GunGripAt = new Vector3(0.30f, 1.42f, 0.36f), GunAim = new Vector3(-0.72f, 0.52f, 0.42f), GunTop = new Vector3(0.10f, 0.45f, 0.88f);

        /// <summary>At load: the gun's fitted pose on the right hand socket is remembered (and its barrel / top axes in the socket's frame,
        /// for the hold), then the gun goes out of sight.</summary>
        void CaptureGun(OzPoseRig rig)
        {
            gun = rig.Find("Shotgun"); gunPump = rig.Find("Shotgun_Pump"); gunPumpBack = rig.Find("Shotgun_PumpBack"); gunPumpGrip = rig.Find("Shotgun_PumpGrip"); gunMuzzle = rig.Find("Shotgun_Muzzle");
            if (!gun || !rig.SocketR) { Debug.LogWarning("[EvilZod] the shotgun or the right hand socket is missing in the model"); return; }
            gunHomePos = gun.localPosition; gunHomeRot = gun.localRotation; gunHomeScale = gun.localScale;
            if (gunPump) pumpRest = gunPump.localPosition;
            if (gunPump && gunPumpBack) pumpBackLocal = gunPumpBack.localPosition;
            if (gunMuzzle)
            {   // the barrel: grip -> muzzle; the gun's top: its up axis made square to the barrel; both in the socket's frame
                var sock = rig.SocketR;
                Vector3 barrel = (gunMuzzle.position - gun.position).normalized;
                Vector3 top = Vector3.ProjectOnPlane(gun.up, barrel).normalized;
                gunBarrelInSocket = Quaternion.Inverse(sock.rotation) * barrel; gunUpInSocket = Quaternion.Inverse(sock.rotation) * top;
                socketInHand = Quaternion.Inverse(rig.HandR.rotation) * sock.rotation;
                gunAxesOk = barrel.sqrMagnitude > 0.5f && top.sqrMagnitude > 0.5f;
            }
            GunHidden();
        }
        void GunHidden()
        {
            if (!gun) return;
            gun.SetParent(transform, true); gun.gameObject.SetActive(false); gunInHand = false;
        }

        /// <summary>LOSE: the shotgun in his right hand socket in its fitted pose, at once (the cut hides how it got there).</summary>
        void GunReady(OzPoseRig rig)
        {
            if (!gun || !rig.SocketR) return;
            gun.SetParent(rig.SocketR, false); gun.localPosition = gunHomePos; gun.localRotation = gunHomeRot; gun.localScale = gunHomeScale; gun.gameObject.SetActive(true);
            if (gunPump) gunPump.localPosition = pumpRest;
            gunInHand = true; pumpAt = -10f; pumpSoundOn = false;
        }
        /// <summary>Every frame the gun is in the hand: its fitted pose (the hold's turn is the hand's, GunHandRotation).</summary>
        void TickGunSeat()
        {
            if (!gun || !gunInHand) return;
            gun.localPosition = gunHomePos; gun.localRotation = gunHomeRot;
        }

        /// <summary>The hold: after the right arm's IK, the hand is turned so the gun's barrel points along GunAim and its top along GunTop
        /// (his frame), the small pump kick added (the muzzle tips up a little while the slide is back).</summary>
        void GunHandRotation(OzPoseRig rig)
        {
            if (!gunAxesOk || !rig.SocketR) return;
            Vector3 fwd = rig.Fwd, rgt = rig.Right;
            Vector3 aim = (rgt * GunAim.x + Vector3.up * GunAim.y + fwd * GunAim.z).normalized;
            Vector3 top = (rgt * GunTop.x + Vector3.up * GunTop.y + fwd * GunTop.z).normalized;
            float kick = PumpKick();
            if (kick > 0f) aim = Quaternion.AngleAxis(-7f * kick, Vector3.Cross(aim, Vector3.up).normalized) * aim;
            top = Vector3.ProjectOnPlane(top, aim).normalized;
            // the socket's rotation that carries the gun's barrel / top axes onto aim / top, then the hand's rotation from it
            Quaternion inSock = Quaternion.LookRotation(gunBarrelInSocket, gunUpInSocket);
            Quaternion want = Quaternion.LookRotation(aim, top);
            Quaternion sockRot = want * Quaternion.Inverse(inSock);
            rig.TwistSpread(true, sockRot * Quaternion.Inverse(socketInHand), 0.55f);                   // redo 2: the turn shared with the forearm (no twisted wrist)
        }
        /// <summary>The pump's small body move (0..1..0 while the slide travels back and stays back).</summary>
        float PumpKick()
        {
            if (!gunInHand || pumpAt < 0f) return 0f;
            float u = clock - pumpAt;
            if (u < PumpBackAt - 0.1f || u > PumpClackB + 0.25f) return 0f;
            if (u < PumpClackA) return Mathf.SmoothStep(0f, 1f, (u - (PumpBackAt - 0.1f)) / (PumpClackA - PumpBackAt + 0.1f));
            if (u < PumpFwdAt) return 1f;
            return 1f - Mathf.SmoothStep(0f, 1f, (u - PumpFwdAt) / (PumpClackB + 0.25f - PumpFwdAt));
        }
        /// <summary>The right wrist's extra offset of the pump (the gun comes back toward the body a few cm while the slide is back).</summary>
        Vector3 GunKickOffset() { float k = PumpKick(); return k > 0f && ez != null && ez.Ok ? (-ez.Rig.Fwd * 0.04f + Vector3.up * 0.02f) * k * (EzH / 2.3f) : Vector3.zero; }
        /// <summary>The trunk's lean of the pump (deg, a little back) and the head's nod on the second clack.</summary>
        float GunLean() => -2.5f * PumpKick();
        float GunNod()
        {
            if (!gunInHand || pumpAt < 0f) return 0f;
            float u = clock - PumpClackB - pumpAt;
            return u > 0f && u < 0.35f ? Mathf.Sin(u / 0.35f * Mathf.PI) * 4f : 0f;
        }

        public bool GunInHand => gunInHand;
        public Vector3 PumpGrip => gunPumpGrip ? gunPumpGrip.position : gun ? gun.position : transform.position;
        /// <summary>The rack starts: the slide goes back after PumpBackAt s for PumpBackDur s and hits its stop = the clip's first clack (the
        /// sound starts then: its first clack is at 0.00 s), stays back, and comes forward for PumpFwdDur s so that it lands at the clip's
        /// second clack (measured: it rises at 0.68 s and peaks at 0.82-0.84 s).</summary>
        void PumpStart() { pumpAt = clock; pumpSoundOn = false; pumpKickB = false; }
        public static float PumpBackAt = 0.12f, PumpBackDur = 0.16f, PumpFwdDur = 0.16f;
        /// <summary>The clip's second clack (its peak) after its start, s; the forward stroke ends there.</summary>
        public const float PumpClipClack2 = 0.82f;
        public static float PumpFwdAt => PumpBackAt + PumpBackDur + PumpClipClack2 - PumpFwdDur;
        /// <summary>The two clacks after the pump beat (s): the slide's rear stop and its front stop.</summary>
        public static float PumpClackA => PumpBackAt + PumpBackDur;
        public static float PumpClackB => PumpFwdAt + PumpFwdDur;
        bool pumpSoundOn, pumpKickB;
        void TickPump()
        {
            if (!gunPump || !gunInHand || pumpAt < 0f) return;
            float u = clock - pumpAt, k;
            if (u < PumpBackAt) k = 0f;
            else if (u < PumpBackAt + PumpBackDur) k = Mathf.SmoothStep(0f, 1f, (u - PumpBackAt) / PumpBackDur);
            else if (u < PumpFwdAt) k = 1f;
            else if (u < PumpFwdAt + PumpFwdDur) k = Mathf.SmoothStep(1f, 0f, (u - PumpFwdAt) / PumpFwdDur);
            else k = 0f;
            gunPump.localPosition = Vector3.Lerp(pumpRest, pumpBackLocal, k);
            if (!pumpSoundOn && u >= PumpClackA) { pumpSoundOn = true; Sfx(SfxFolder + "blue_shotgun_pump", -2f, 1f, "shotgun_pump"); Kick(0.35f); }
            if (pumpSoundOn && !pumpKickB && u >= PumpClackB) { pumpKickB = true; Kick(0.3f); }
        }
        public bool Pumping => pumpAt >= 0f && clock - pumpAt < PumpClackB + 0.12f;
        /// <summary>Seconds since the pump beat (negative = no pump yet).</summary>
        public float PumpAge => pumpAt >= 0f ? clock - pumpAt : -1f;
    }
}
