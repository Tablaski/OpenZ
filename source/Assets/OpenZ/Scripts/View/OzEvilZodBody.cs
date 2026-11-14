using System;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// DV161 EvilZod, his upper body. On the Animator's object, order 81: after OzHeroLegs (61), UnitAim (62), RobotLegIK (70) and the disabled WeaponHold (80). Every frame spine,
    /// neck, head, shoulders, arms and hands are written fresh from his bind pose, so the grunt's rifle clips and the torso solve leave no trace above the hips.
    /// <list type="bullet">
    /// <item>Two holds. PORT, idle and walking: the gun carried low and ahead on his right, barrel across, so it stands clear of him at the game camera. FIRE: level over the belt with the left shoulder to the target, the chest turned away,
    /// the head turned back onto it. Both were searched inside the joint table (tools/search_holds.py): hip height is out of his reach. Between them a 0.26 s swing out and 0.34 s back.</item>
    /// <item></item>
    /// <item>The power dash: crouched and tucked in the windup, one held drive pose in the burst. On the last metres the brake: UnitView eases the drawn body out, he leans back hard on dug-in heels, then his weight rocks forward over his feet and the gun comes up. The rules hold the shot for it (ZEvilZod.PlantTicks).</item>
    /// <item></item>
    /// </list>
    /// The numbers are metres in his own frame on the rig at 2.30 m (x his right, y up from his feet, z forward), scaled by the model; tools/gate_holds.py measures the same steps
    /// against the joint table. Data: Resources/OpenZ/HeroSquad/Legs/evilzod_hands.json (bind rotations, finger poses, hinges). View only.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(81)]
    public sealed class OzEvilZodBody : MonoBehaviour
    {
        [Serializable] public sealed class PoseJson { public string name; public float[] q; }
        [Serializable] public sealed class HandsJson { public string[] bones; public float[] rest; public string[] fingers; public PoseJson[] poses; public float[] hingeR; public float[] hingeL; public float[] headForward; }

        public const string HandsPath = OzHeroLegs.DataDir + "evilzod_hands", Member = "evilzod";
        public static Vector3 PortGrip = new Vector3(0.26f, 1.54f, 0.16f), PortAim = new Vector3(-0.495f, -0.139f, 0.858f), PortTop = new Vector3(0.503f, 0.759f, 0.414f);   // carried ahead on his right, barrel low and across: the gun stands clear of his body at the game camera
        public static Vector3 PortPoleR = new Vector3(0.6f, -0.8f, -0.3f), PortPoleL = new Vector3(-0.4f, -0.8f, -0.2f);
        public static Vector3 FireGrip = new Vector3(0.26f, 1.56f, 0.14f), FireTop = new Vector3(0.174f, 0.985f, 0f), FirePoleR = new Vector3(0.5f, -0.9f, 0.25f), FirePoleL = new Vector3(-0.4f, -0.8f, -0.2f);
        public static float PortChest = 30f, FireChest = 64f, PortLean = 2f, PortUp = 4f, FireUp = 2f, SpineMax = 40f, NeckMax = 68f, PitchDown = 18f, PitchUp = 10f, PitchEasy = 10f, PitchBend = 8f;
        public static float SwingOut = 0.09f, SwingBack = 0.11f, HoldAfterShot = 2f, Climb = 20f, TipUp = 14f, TipTop = 13f;
        /// <summary>The recoil at its peak. Shove, KickBack, KickDrop: metres on the rig, along the bore or under him. KickLean, KickChest, KickHead: degrees. DashKick: a dash shot is that much harder. ShotReadyAt: how far up the hold must be for the shot to leave it.</summary>
        public static float Shove = 0.12f, KickBack = 0.13f, KickDrop = 0.045f, KickLean = 18f, KickChest = 6f, KickHead = 10f, DashKick = 1.25f, ShotReadyAt = 0.85f, ShotWaitMax = 0.1f;
        // quick, its peak under the muzzle flash. Now stronger and slower: Climb 14 -> 20, Shove 0.09 -> 0.12, KickBack 0.085 -> 0.13, KickDrop 0.03 -> 0.045, KickLean 12 -> 18,
        // KickChest 4 -> 6, KickHead 7 -> 10; RecoilPeak 0.05 -> 0.10 s (after the flash), RecoilEnd 0.46 -> 0.55 s. Gun and body keep one clock and round 2's step between
        // the climb and the lean: the wrists stay in their range (joint gate, open_fixes_2nov/ez_shot_feel).
        /// <summary>The brake, degrees and rig metres: the lean back on his heels, the chest turned further so his left hand keeps the slide, the rock forward when he stops, the hips carried forward by it; the seconds of the rock and of the settling.</summary>
        public static float BrakeLean = 16f, BrakeChest = 14f, RockLean = 9f, RockHips = 0.05f, RockSeconds = 0.13f, SettleSeconds = 0.25f;
        /// <summary>At rest. Seconds of game time: after a shot before he rests, between looks, between weight shifts, between two works of the slide. Degrees: the look's turn left and right of his front (the chest is 30 to his right), its up and down in metres at 10 m. Rig metres: the weight shift.</summary>
        public static float RestAfterShot = 4f, LookEveryMin = 2.6f, LookEveryMax = 5.5f, ShiftEveryMin = 6f, ShiftEveryMax = 11f, PumpEveryMin = 18f, PumpEveryMax = 32f, PumpFirstMin = 7f, PumpFirstMax = 12f, PumpRetry = 1f;
        /// <summary>The left hand on the fore-end, in both holds. PortRoll: degrees the carry's grip turns about the bore so the palm faces up, not along the gun's cant; none in the fire hold.</summary>
        public static float HandSide = 20f, HandCrossMin = 35f, HandCrossMax = 85f, HandLift = 0.053f, PortRoll = 20f;
        public static float ShockLean = 2.5f, ShockHead = 6f, LookLeft = 35f, LookRight = 60f, LookUpDown = 1.6f, ShiftHips = 0.035f, ShiftTilt = 1.8f, IdleTip = 0.8f;
        /// <summary>Into the slide's work at rest, the second the reload take starts: its stops lie 0.16 and 0.55 s into the take, on the slide's 0.44 and 0.83.</summary>
        public const float IdleSoundAt = 0.28f;
        /// <summary>The seconds of one shot after its event: recoil peak, its end, slide starts back, rear stop, starts forward, front stop, the gun settled.</summary>
        public const float RecoilPeak = 0.10f, RecoilEnd = 0.55f, PumpBackFrom = 0.32f, PumpBackAt = 0.44f, PumpFwdFrom = 0.72f, PumpFwdAt = 0.83f, PumpSettled = 1.05f;
        public const float WindupSeconds = 0.20f, PlantSeconds = 0.45f;
        // The sim's shot cycle 3.00 -> 2.00 s (ZEvilZod.CycleTicks), and after every shot, once the slide is forward again, one shell
        // from his belt into the gun: the left hand leaves the fore-end, takes a shell at his left hip, brings it under the receiver, thumbs it into the loading port (the
        // shell-in sound, AudioDirector.EzShellId) and slides forward onto the fore-end again; the gun dips a little and turns its port toward the hand meanwhile.
        /// <summary>Seconds after the shot's event: the hand leaves the fore-end, is at the belt, leaves it, is at the port, has pushed the shell in, is back on the fore-end;
        /// the shell-in (its sound is AudioDirector's, on the rules' clock, so it never hangs on this view). All before the earliest next shot (2.00 s) and the earliest dash after a shot (1.80 s).</summary>
        public const float LoadFrom = 0.92f, LoadAtBelt = 1.12f, LoadBeltOff = 1.20f, LoadAtPort = 1.40f, LoadPushed = 1.52f, LoadBack = 1.74f, ShellSoundAt = AudioDirector.EzShellAt;
        /// <summary>The shell pouch (where his wrist goes): rig metres from his left hip joint, x along the hip line (his right), y up, z forward. The port: this share of the
        /// way from the fore-end back toward the grip along the bore, this far (rig m) lower than the fore-end hold. The thumb's push up (rig m), the bow of the hand's way
        /// forward of the straight line (rig m), the gun's turn toward the hand and its dip (degrees, at most).</summary>
        public static Vector3 ShellBelt = new Vector3(-0.10f, 0.10f, 0.13f);
        public static float PortBack = 0.55f, PortDrop = 0.02f, LoadPush = 0.03f, LoadArc = 0.08f, LoadRoll = 24f, LoadDip = 6f;
        /// <summary>Checks: shells loaded (one after every shot, counted at the push, on camera or not).</summary>
        public static int Shells;
        float shellClock = 99f; bool shellOwed;
        static readonly float[] SpineShare = { 0.30f, 0.40f, 0.30f };

        static HandsJson hands; static bool handsLooked;
        /// <summary>Forgets the loaded data (the editor's check after a re-import).</summary>
        public static void Forget() { hands = null; handsLooked = false; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Forget(); }

        static HandsJson Hands
        {
            get
            {
                if (hands == null && !handsLooked)
                {
                    handsLooked = true;
                    var ta = Resources.Load<TextAsset>(HandsPath);
                    if (ta) { try { hands = JsonUtility.FromJson<HandsJson>(ta.text); } catch (Exception e) { Debug.LogWarning("[OpenZ EvilZod] " + HandsPath + ": " + e.Message); } }
                    else Debug.LogWarning("[OpenZ EvilZod] Resources/" + HandsPath + " is missing");
                }
                return hands;
            }
        }

        [SerializeField] bool bodyOn;                                                     // kept on an instantiated boarding clone
        Animator anim; UnitView view; bool viewLooked; OzHeroLegs legs; bool ready, inited;
        Transform[] upper; Quaternion[] rest, animQ;
        Transform spine, spine1, spine2, neck, head, headFront, handR, handL, shoulderL, shoulderR, armR, armL, socketL, upLegL, upLegR, legL, legR, footL, footR, hips;
        Transform weapon, grip, muzzle, foregrip, slide; Vector3 slideRest, slideBack; bool slideOk;
        OzEvilZodArms armRi, armLi; Vector3 headFwdLocal = Vector3.forward, leftOff; bool leftOffOk;
        readonly Quaternion[] qa = new Quaternion[16];

        float fw, fwVel, weight = 1f, sinceShot = 99f, sinceAim = 99f, breath, plantAge = 99f, stepKick; bool lastPlantL, lastPlantR;
        float dWindup, dDrive, dPlant, dVel, ddVel; int shotTarget = -1; bool shotDue; float shotKick = 1f;
        float brakeAge = 99f, brakeT = 0.3f, brakeLean, rock; bool brakeWas;
        float idleFor, nextLook, nextShift, nextPump = -1f, lookYaw, shift, shiftWant, shiftVel, idlePump = 99f; bool lookOn, reloadHeard = true;
        Vector3 lookPt, lookVel; bool lookInit; float nextScan; ZObject alert;

        public bool Ready => ready;
        /// <summary>0 port, 1 fire.</summary>
        public float FireWeight => fw;
        public Vector3 MuzzlePos => muzzle ? muzzle.position : transform.position + Vector3.up * 2.2f;
        public Vector3 BarrelDir => weapon ? -weapon.forward : Front;
        public Transform Head => head;
        public Transform HeadFrontBone => headFront ? headFront : head;
        public Vector3 ChestPos => spine2 ? spine2.position : transform.position + Vector3.up * 2.4f;
        public Transform ShoulderR => armR;
        /// <summary>Where the arcs of his death run: head, chest, hips, both hands, elbows, knees and feet, the muzzle.</summary>
        public Transform[] ArcPoints => new[] { head, spine2, hips, handL, handR, armL && armL.childCount > 0 ? armL.GetChild(0) : armL, armR && armR.childCount > 0 ? armR.GetChild(0) : armR, legL, legR, footL, footR, muzzle };
        Transform RootT => view ? view.transform : transform.parent ? transform.parent : transform;
        /// <summary>His front on the ground: every robot model faces its root's -z.</summary>
        Vector3 Front { get { var f = -RootT.forward; f.y = 0f; return f.sqrMagnitude > 1e-6f ? f.normalized : Vector3.forward; } }

        /// <summary>OzEvilZodView.OnSpawn: on the view's Animator object; a boarding clone copies the component and starts itself in Awake.</summary>
        public static OzEvilZodBody Attach(UnitView v)
        {
            if (!v) return null;
            var an = v.GetComponentInChildren<Animator>(); if (!an) return null;
            var b = an.GetComponent<OzEvilZodBody>(); if (!b) b = an.gameObject.AddComponent<OzEvilZodBody>();
            b.view = v; b.viewLooked = true; b.bodyOn = true; b.Init();
            return b;
        }

        void Awake() { if (bodyOn && !inited) Init(); }

        /// <summary>OzEvilZodView.Figure: a figure with no unit behind it.</summary>
        public void Start2() { bodyOn = true; viewLooked = true; if (!inited) Init(); }

        void Init()
        {
            inited = true; ready = false;
            anim = GetComponent<Animator>(); var h = Hands;
            if (!anim || h == null || h.bones == null || h.rest == null || h.rest.Length != h.bones.Length * 4) return;
            var all = anim.GetComponentsInChildren<Transform>(true);
            Transform F(string n) { foreach (var t in all) if (t.name == n) return t; return null; }
            upper = new Transform[h.bones.Length]; rest = new Quaternion[upper.Length]; animQ = new Quaternion[upper.Length];
            for (int i = 0; i < upper.Length; i++)
            {
                upper[i] = F(h.bones[i]); int k = i * 4;
                if (!upper[i]) { Debug.LogWarning("[OpenZ EvilZod] bone " + h.bones[i] + " not found"); return; }
                rest[i] = new Quaternion(h.rest[k], h.rest[k + 1], h.rest[k + 2], h.rest[k + 3]);
            }
            void Grip(string pose, string side)
            {
                if (h.poses == null) return;
                foreach (var p in h.poses)
                {
                    if (p == null || p.name != pose || p.q == null || p.q.Length != h.fingers.Length * 4) continue;
                    for (int i = 0; i < h.fingers.Length; i++)
                    {
                        if (!h.fingers[i].Contains(side)) continue;
                        int u = Array.IndexOf(h.bones, h.fingers[i]), k = i * 4;
                        if (u >= 0) rest[u] = new Quaternion(p.q[k], p.q[k + 1], p.q[k + 2], p.q[k + 3]);
                    }
                }
            }
            Grip("GripShotgun", "Right"); Grip("GripPump", "Left");
            const string M = "mixamorig";
            hips = F(M + "Hips"); spine = F(M + "Spine"); spine1 = F(M + "Spine1"); spine2 = F(M + "Spine2"); neck = F(M + "Neck"); head = F(M + "Head"); headFront = F("HeadFront");
            shoulderL = F(M + "LeftShoulder"); shoulderR = F(M + "RightShoulder"); armR = F(M + "RightArm"); armL = F(M + "LeftArm"); handR = F(M + "RightHand"); handL = F(M + "LeftHand");
            socketL = F("LeftHandSocket"); upLegL = F(M + "LeftUpLeg"); upLegR = F(M + "RightUpLeg"); legL = F(M + "LeftLeg"); legR = F(M + "RightLeg"); footL = F(M + "LeftFoot"); footR = F(M + "RightFoot");
            weapon = F("BS01_Weapon_EvilZod"); grip = F("Grip"); muzzle = F("Muzzle"); foregrip = F("Foregrip"); slide = F("Shotgun_Pump");
            var back = F("Shotgun_PumpBack"); slideOk = slide && back;
            if (slideOk) { slideRest = slide.localPosition; slideBack = back.localPosition; }
            var foreR = F(M + "RightForeArm"); var foreL = F(M + "LeftForeArm");
            if (!hips || !spine || !spine1 || !spine2 || !neck || !head || !armR || !armL || !foreR || !foreL || !handR || !handL || !upLegL || !upLegR || !weapon || !grip || !foregrip || h.hingeR == null || h.hingeR.Length != 3 || h.hingeL == null || h.hingeL.Length != 3)
            { Debug.LogWarning("[OpenZ EvilZod] the prefab is not his: bones or sockets missing (menu OpenZ/V1.3/EvilZod/1 Build)"); return; }
            armRi = new OzEvilZodArms(armR, foreR, handR, new Vector3(h.hingeR[0], h.hingeR[1], h.hingeR[2]));
            armLi = new OzEvilZodArms(armL, foreL, handL, new Vector3(h.hingeL[0], h.hingeL[1], h.hingeL[2]));
            if (h.headForward != null && h.headForward.Length == 3) headFwdLocal = new Vector3(h.headForward[0], h.headForward[1], h.headForward[2]).normalized;
            legs = GetComponent<OzHeroLegs>();
            ready = true;
        }

        // ---------------------------------------------------------------------------- what the rules told the view (OzEvilZodFx)
        /// <summary>The rules fired: the hold comes up at once if it was still on its way; the flash waits for it (OzEvilZodFx).</summary>
        public void ShotComing(int targetId) { sinceAim = 0f; shotTarget = targetId; shotDue = true; }
        /// <summary>His shot left the gun: the recoil and the pump run from here. late: game seconds since the rules' event, so the slide stays on the sound.</summary>
        public void OnShot(int targetId, float late, bool dash) { shotDue = false; sinceShot = late; sinceAim = 0f; shotTarget = targetId; shotKick = dash ? DashKick : 1f; }
        /// <summary>The burst ended: the plant runs from here.</summary>
        public void OnDashEnd() { plantAge = 0f; }

        // ---------------------------------------------------------------------------- frame
        void LateUpdate()
        {
            if (!ready || !anim || !anim.enabled) return;
            if (!viewLooked) { viewLooked = true; view = GetComponentInParent<UnitView>(); }
            if (view && view.Dying && !view.EvilShocked) { enabled = false; return; }           // the melt: nothing of his holds; the shock keeps his pose and shakes it
            if (!legs) legs = GetComponent<OzHeroLegs>();
            float dt = Time.deltaTime, sim = dt * (ZGame.I ? Mathf.Max(0.25f, ZGame.I.gameSpeed) : 1f);
            string prog = legs ? legs.ProgramName : "-";
            bool free = prog == "jump_takeoff" || prog == "jump_air" || prog == "landing" || prog == "climb_ladder" || prog == "blast_airborne" || prog == "death_back" || prog == "death_side";
            weight = Mathf.MoveTowards(weight, free ? 0f : 1f, dt / 0.15f);
            if (weight <= 0f) return;
            bool blend = weight < 1f;
            if (blend) for (int i = 0; i < upper.Length; i++) animQ[i] = upper[i].localRotation;
            PoseFrame(sim);
            if (blend) for (int i = 0; i < upper.Length; i++) upper[i].localRotation = Quaternion.Slerp(animQ[i], upper[i].localRotation, weight);
        }

        /// <summary>The editor's portrait: the port pose on the stand legs, chin up and face turned, with no game running (edit mode).</summary>
        public void PortraitPose(float chinUp, float faceTurn)
        {
            if (!inited) Init();
            if (!ready) return;
            var d = OzHeroLegs.Load(Member); var p = d != null ? d.Find("stand") : null;
            if (p != null) WriteLegs(p, 1f, d);
            portraitChin = chinUp; portraitTurn = faceTurn; portrait = true;
            PoseFrame(0f);
            portrait = false;
        }
        bool portrait; float portraitChin, portraitTurn;

        void PoseFrame(float sim)
        {
            var o = view ? view.Obj : null;
            Vector3 up = Vector3.up, F = Front, R = Vector3.Cross(up, F), root = RootT.position;
            float k = anim.transform.lossyScale.y;
            sinceShot += sim; sinceAim += sim; breath += sim; plantAge += sim; brakeAge += sim; idlePump += sim;
            KickClock(sim);                                                                     // rows 86 / 87: the kick's own clock, the look's turn of the shoulders
            Brake();
            var phase = o != null ? o.EvilPhase : ZEvilPhase.None;
            ZObject tgt = Target(o);
            Think(o, phase, tgt, sim, F);
            DashLegs(phase, sim);
            Rest(o, phase, sim);
            IdleTick(o, phase, sim);                                                            // row 87: the idle look-around and gun check

            // the aim: the barrel on the target, within what the hold can give. The transforms still lie in the Animator's rifle pose here, so the line starts from the hold's own numbers.
            float yawOff = 0f, pitch = 0f, bend = 0f;
            bool has = tgt != null;
            Vector3 tp = has ? AimPoint(tgt) : default;
            if (has)
            {
                var to = tp - (root + (R * FireGrip.x + up * (FireGrip.y + 0.174f) + F * FireGrip.z) * k); var flat = Vector3.ProjectOnPlane(to, up);
                if (flat.sqrMagnitude > 0.04f)
                {
                    yawOff = Mathf.Clamp(Vector3.SignedAngle(F, flat, up), -30f, 30f);
                    float raw = Mathf.Atan2(to.y, flat.magnitude) * Mathf.Rad2Deg;
                    pitch = Mathf.Clamp(raw, -PitchDown, PitchUp);
                    bend = Mathf.Clamp(-pitch - PitchEasy, 0f, PitchBend);                      // at his height a near target lies far below: the trunk bends into a low shot, the wrist alone can not
                }
            }
            float idleMul = ezPumpMine && ezT >= 0f ? ezW : 1f;                                // row 87: the idle gun check's rack blends out with it
            float rUp = Recoil(sinceShot) * shotKick, rTip = Mathf.Max(Tip(sinceShot), Tip(idlePump) * IdleTip * idleMul), rPump = Mathf.Max(Pump(sinceShot), Pump(idlePump) * idleMul);
            Quaternion yq = Quaternion.AngleAxis(yawOff * fw, up);
            Vector3 F2 = yq * F, R2 = yq * R;
            HipsTo(-F2 * (KickBack * k * rUp) - up * (KickDrop * k * rUp) + F * (RockHips * k * rock) + R * (ShiftHips * k * shift));   // over planted feet
            KickLegs(F2, k);                                                                    // rows 86 / 87: the hips sit back, the figure slides back on its soles
            if (Mathf.Abs(ezYaw) > 0.01f) HipsTurn(ezYaw * IdleHipsShare * (1f - fw));          // row 87: the hips carry part of the look, the feet stay planted

            // the trunk: the chest's turn against the hips, shared over the spine; the lean of the moment on top
            for (int i = 0; i < upper.Length; i++) { upper[i].localRotation = rest[i]; }
            Vector3 hipLine = Vector3.ProjectOnPlane(upLegR.position - upLegL.position, up);
            float hipsYaw = hipLine.sqrMagnitude > 1e-6f ? Vector3.SignedAngle(R, hipLine, up) : 0f;
            float chestWant = Mathf.Lerp(PortChest, FireChest, fw) + yawOff * fw + BrakeChest * Mathf.Clamp01(-brakeLean / BrakeLean) * (1f - fw);
            float tw = Mathf.Clamp(chestWant - hipsYaw, -SpineMax, SpineMax);
            spine.rotation = Quaternion.AngleAxis(tw * SpineShare[0], up) * spine.rotation;
            spine1.rotation = Quaternion.AngleAxis(tw * SpineShare[1], up) * spine1.rotation;
            spine2.rotation = Quaternion.AngleAxis(tw * SpineShare[2], up) * spine2.rotation;
            float swing = legs && legs.ProgramName == "walk" ? Mathf.Sin(legs.Phase * Mathf.PI * 2f) * 4f : 0f;
            if (Mathf.Abs(swing) > 0.01f) spine2.rotation = Quaternion.AngleAxis(swing * (1f - fw), up) * spine2.rotation;
            float dashLean = dWindup * 20f + dDrive * 24f + brakeLean, trunk = dashLean + bend * fw - KickLean * rUp;   // trunk: what the fire hold rides on
            float jolt = view && view.EvilShocked ? UnityEngine.Random.Range(-1f, 1f) : 0f;      // the current through him
            float lean = Mathf.Lerp(PortLean, 0f, fw) + trunk + 1f * Mathf.Sin(breath * 1.15f) + 1.2f * stepKick + ShockLean * jolt;
            spine1.rotation = Quaternion.AngleAxis(lean, R) * spine1.rotation;
            spine1.rotation = KickTurn(up, R2) * spine1.rotation;                                // rows 86 / 87: the kick's rock and the look's turn, the whole upper body as one piece
            spine2.rotation = Quaternion.AngleAxis(-(Mathf.Lerp(PortUp, FireUp, fw) + KickChest * rUp + 1.4f * Mathf.Sin(breath * 1.15f)), R) * spine2.rotation;
            if (Mathf.Abs(shift) > 0.01f) spine1.rotation = Quaternion.AngleAxis(-shift * ShiftTilt, F) * spine1.rotation;   // the shoulders stay over the weight
            float sh = 0.8f * Mathf.Sin(breath * 1.15f);
            if (shoulderL) shoulderL.rotation = Quaternion.AngleAxis(-sh, F) * shoulderL.rotation;
            if (shoulderR) shoulderR.rotation = Quaternion.AngleAxis(sh, F) * shoulderR.rotation;

            // the gun's wanted lie
            float hipsRef = legs && legs.Data != null && legs.Data.J != null ? legs.Data.J.standHipsY : 1.318f;
            Vector3 carry = hips.position - (root + up * (hipsRef * k));
            Vector3 P(Vector3 v) => root + (R2 * v.x + up * v.y + F2 * v.z) * k + carry;
            Vector3 D(Vector3 v) => (R2 * v.x + up * v.y + F2 * v.z).normalized;
            Vector3 portAim = Quaternion.AngleAxis(dashLean, R) * (R * PortAim.x + up * PortAim.y + F * PortAim.z).normalized;
            Vector3 portTop = Quaternion.AngleAxis(dashLean, R) * (R * PortTop.x + up * PortTop.y + F * PortTop.z).normalized;
            Vector3 fireAim = Quaternion.AngleAxis(-pitch, R2) * F2;
            Vector3 aim = Vector3.Slerp(portAim, fireAim, fw).normalized, top = Vector3.Slerp(portTop, D(FireTop), fw).normalized;
            if (rUp > 0f || rTip > 0f)
            {
                float tipNow = Mathf.Min(TipUp * rTip, Mathf.Max(0f, TipTop - pitch * fw));
                var ax = Vector3.Cross(aim, up); if (ax.sqrMagnitude > 1e-5f) aim = Quaternion.AngleAxis(Climb * rUp + tipNow, ax.normalized) * aim;   // aim x up, a positive turn: the muzzle climbs
            }
            aim = Quaternion.AngleAxis(0.6f * stepKick, Vector3.Cross(up, aim).normalized) * aim;
            top = Vector3.ProjectOnPlane(top, aim).normalized;
            Vector3 portW = spine1.position + Quaternion.AngleAxis(dashLean, R) * (P(PortGrip) - spine1.position);   // the hold leans with the trunk
            Vector3 fireW = spine1.position + Quaternion.AngleAxis(trunk, R) * (P(FireGrip) - spine1.position);      // the kick and the bend carry the hold with the trunk
            Vector3 gripW = Vector3.Lerp(portW, fireW, fw) - aim * (Shove * k * rUp);
            gripW = spine1.position + kq * (gripW - spine1.position); aim = kq * aim; top = kq * top;   // rows 86 / 87: the gun rides the rock and the turn
            float load = Mathf.Max(Load01(sinceShot), ezT >= 0f && ezKind == 1 ? Load01(ezT + LoadFrom) * ezW : 0f);   // row 82: the gun for the shell after a shot; row 87: the idle gun check
            Vector3 beltW = upLegL.position + (Quaternion.AngleAxis(hipsYaw, up) * (R * ShellBelt.x + F * ShellBelt.z) + up * ShellBelt.y) * k;
            if (load > 0f)
            {   // a little dip, and the underside (the loading port) turned toward the hand's way to the belt, by at most LoadRoll
                var dax = Vector3.Cross(aim, up); if (dax.sqrMagnitude > 1e-5f) aim = Quaternion.AngleAxis(-LoadDip * load, dax.normalized) * aim;
                top = Vector3.ProjectOnPlane(top, aim).normalized;
                var toBelt = Vector3.ProjectOnPlane(beltW - gripW, aim);
                if (toBelt.sqrMagnitude > 1e-6f) top = Quaternion.AngleAxis(Mathf.Clamp(Vector3.SignedAngle(-top, toBelt, aim), -LoadRoll, LoadRoll) * load, aim) * top;
            }
            Quaternion wantW = Quaternion.LookRotation(-aim, top);
            Quaternion h2w = Quaternion.Inverse(handR.rotation) * weapon.rotation;
            Quaternion wantH = wantW * Quaternion.Inverse(h2w);
            Vector3 gripIn = Quaternion.Inverse(handR.rotation) * (grip.position - handR.position);
            Vector3 wrist = gripW - wantH * gripIn;
            Vector3 poleR = Vector3.Slerp(D(PortPoleR), D(FirePoleR), fw);
            armRi.Reach(wrist, kq * poleR);                                                     // rows 86 / 87: the elbow's side turns with them
            armRi.TurnHand(wantH, 0.55f);
            if (slideOk) slide.localPosition = Vector3.Lerp(slideRest, slideBack, rPump);

            // the left hand rides the slide
            Vector3 off = socketL ? socketL.position - handL.position : Vector3.zero;
            if (!leftOffOk) { leftOff = off; leftOffOk = true; } else if (sim > 0f) leftOff = Vector3.Lerp(leftOff, off, 1f - Mathf.Exp(-sim / 0.35f));
            Vector3 poleL = Vector3.Slerp(D(PortPoleL), D(FirePoleL), fw), leftPort = foregrip.position - leftOff * 0.6f;
            if (Mathf.Abs(ezYaw) > 0.01f) poleL = Quaternion.AngleAxis(ezYaw * (1f - fw), up) * poleL;   // row 87: the left elbow's side turns with the look
            if (PalmReady())
            {
                // across it toward his right, as far from his forearm's line as the wrist allows, the thumb on the near side. In the carry the grip turns PortRoll about the bore.
                Vector3 palmUp = Quaternion.AngleAxis(PortRoll * (1f - fw), aim) * top;
                Vector3 line = Vector3.ProjectOnPlane(foregrip.position - armL.position, palmUp); float rightOf = Vector3.SignedAngle(aim, R2, palmUp) >= 0f ? 1f : -1f;
                float across = Mathf.Clamp((line.sqrMagnitude > 1e-6f ? Vector3.SignedAngle(aim, line, palmUp) * rightOf : 0f) + HandSide, HandCrossMin, HandCrossMax);
                Quaternion palmW = Quaternion.LookRotation(Quaternion.AngleAxis(across * rightOf, palmUp) * aim, palmUp) * palmLocal;
                LeftLoad(foregrip.position + top * (HandLift * k) - palmW * (socketIn * k), beltW, palmW, true, poleL, k, F);   // the socket on the fore-end's middle, not on its lower face; row 82: the shell's way
            }
            else LeftLoad(leftPort, beltW, Quaternion.identity, false, poleL, k, F);

            // the head
            Vector3 chestF = Quaternion.AngleAxis(hipsYaw + tw, up) * F, chestR = Vector3.Cross(up, chestF);
            bool ezLook = ezT >= 0f && ezKind == 0, ezGun = ezT >= 0f && ezKind == 1 && ezW > 0.5f;
            Vector3 lookWant = portrait ? head.position + F * 10f : has && fw > 0.3f ? tp : ezGun || idlePump < PumpSettled ? foregrip.position : ezLook ? head.position + Quaternion.AngleAxis(ezYaw, up) * F * 10f : lookOn ? head.position + Quaternion.AngleAxis(lookYaw, up) * F * 10f + up * lookRise : head.position + F * 10f;
            if (!lookInit || sim <= 0f) { lookPt = lookWant; lookVel = Vector3.zero; lookInit = true; }
            else lookPt = Vector3.SmoothDamp(lookPt, lookWant, ref lookVel, fw > 0.3f ? 0.14f : ezLook ? IdleHeadLag : 0.5f, 60f, sim);   // row 87: in the look the head keeps with the trunk
            var hd = lookPt - head.position; float hy = 0f, hp = 0f;
            if (hd.sqrMagnitude > 1e-4f)
            {
                var flat = Vector3.ProjectOnPlane(hd, up);
                hy = Mathf.Clamp(Vector3.SignedAngle(chestF, flat, up), -NeckMax, NeckMax);
                hp = Mathf.Clamp(-Mathf.Asin(Mathf.Clamp(hd.normalized.y, -1f, 1f)) * Mathf.Rad2Deg, -30f, 30f);
            }
            if (portrait) { hy += portraitTurn; hp -= portraitChin; }
            float nod = sinceShot > PumpBackFrom && sinceShot < PumpBackAt + 0.2f ? Mathf.Sin((sinceShot - PumpBackFrom) / 0.32f * Mathf.PI) * 4f : 0f;
            nod -= KickHead * rUp - ShockHead * jolt;
            Quaternion yawQ = Quaternion.AngleAxis(hy, up), turn = yawQ * Quaternion.AngleAxis(hp + nod, yawQ * chestR);
            neck.rotation = Quaternion.Slerp(Quaternion.identity, turn, 0.35f) * neck.rotation;
            head.rotation = Quaternion.FromToRotation(head.TransformDirection(headFwdLocal), turn * chestF) * head.rotation;

            if (sim > 0f) { stepKick = Mathf.MoveTowards(stepKick, 0f, sim / 0.2f); }
        }

        /// <summary>His left hand's own numbers, read once with the fingers in their grip: the turn from the hand to (fingers, palm normal), and where the held fore-end lies in it.</summary>
        bool PalmReady()
        {
            if (palmLooked) return palmOk;
            palmLooked = true;
            Transform m1 = null, m3 = null;
            foreach (var t in anim.GetComponentsInChildren<Transform>(true)) { if (t.name == "mixamorigLeftHandMiddle1") m1 = t; else if (t.name == "mixamorigLeftHandMiddle3") m3 = t; }
            if (!m1 || !m3 || !socketL) return false;
            var inv = Quaternion.Inverse(handL.rotation);
            Vector3 fingers = inv * (m1.position - handL.position), palm = Vector3.ProjectOnPlane(inv * (m3.position - m1.position), fingers);
            if (palm.sqrMagnitude < 1e-8f || fingers.sqrMagnitude < 1e-8f) return false;
            palmLocal = Quaternion.Inverse(Quaternion.LookRotation(fingers.normalized, palm.normalized));
            socketIn = inv * (socketL.position - handL.position) / Mathf.Max(1e-4f, anim.transform.lossyScale.y);   // per unit of his scale k: a boarding figure grows or shrinks after its first frame
            return palmOk = true;
        }
        bool palmLooked, palmOk; Quaternion palmLocal = Quaternion.identity; Vector3 socketIn;

        /// <summary>What he has his gun on: his order, the running dash's target, the last shot's for a moment, else nearest in front of him.</summary>
        ZObject Target(ZObject o)
        {
            if (o == null || o.World == null) return null;
            if (shotDue && shotTarget >= 0) { var q = o.World.GetAny(shotTarget); if (q != null) return q; }   // 1.4 DV174 fix round 2: a waiting volley's own target first, dead or not (the gate and the pellets go by it)
            var t = o.AttackObject;
            if (t != null && !t.Destroyed) return t;
            if (o.EvilDashing || plantAge < PlantSeconds + 0.1f) { var d = o.World.Get(o.EvilDashTargetId); if (d != null && !d.Destroyed) return d; }
            if ((shotDue || sinceShot < HoldAfterShot) && shotTarget >= 0) { var s = o.World.Get(shotTarget); if (s != null && !s.Destroyed) return s; }
            return alert != null && !alert.Destroyed ? alert : null;
        }

        Vector3 AimPoint(ZObject t)
        {
            var g = ZGame.I; if (!g) return ChestPos;
            return g.Views.TryGetValue(t.Id, out var v) && v ? v.AimPos() : g.CenterWorld(t, false) + Vector3.up * 1f;
        }

        void Think(ZObject o, ZEvilPhase phase, ZObject tgt, float sim, Vector3 F)
        {
            if (sim <= 0f) { if (portrait) { fw = 0f; dWindup = dDrive = dPlant = 0f; } return; }
            if (o != null && Time.time >= nextScan && !portrait)
            {   // a look around four times a second: an enemy close in front brings the gun up before the first shot, so the shot leaves a level gun
                nextScan = Time.time + 0.25f; alert = null;
                var w = o.World; double best = (o.AttackRadius + 24) * (double)(o.AttackRadius + 24);
                if (w != null) foreach (var u in w.Objects)
                    {
                        if (u.Destroyed || !u.IsUnit || u.Owner == o.Owner || u.Owner == Team.Neutral || u.KillAt >= 0) continue;
                        double dx = u.CenterX - o.CenterX, dy = u.CenterY - o.CenterY, d = dx * dx + dy * dy;
                        if (d >= best) continue;
                        var to = new Vector3((float)dx, 0f, (float)-dy);
                        if (to.sqrMagnitude > 1f && Vector3.Angle(F, to) > 35f) continue;
                        best = d; alert = u;
                    }
            }
            bool moving = o != null && o.IsMoving;
            bool attack = o != null && o.AttackObject != null && !o.AttackObject.Destroyed;
            bool brakeHold = brakeAge < brakeT - 0.06f;                                         // the gun stays tucked while he skids, and comes up with the rock
            bool wantFire = phase == ZEvilPhase.None && !portrait && !brakeHold && (attack || shotDue || sinceShot < HoldAfterShot || (alert != null && !moving) || plantAge < PlantSeconds + 0.1f);
            float tau = wantFire ? (plantAge < PlantSeconds ? 0.045f : sinceAim < 0.2f ? 0.03f : SwingOut) : phase != ZEvilPhase.None ? 0.05f : SwingBack;
            fw = Mathf.SmoothDamp(fw, wantFire ? 1f : 0f, ref fwVel, tau, 40f, sim);
            if (legs)
            {
                bool l = legs.Planted(0), r = legs.Planted(1);
                if (legs.ProgramName == "walk" && ((l && !lastPlantL) || (r && !lastPlantR))) stepKick = 1f;
                lastPlantL = l; lastPlantR = r;
            }
        }

        void DashLegs(ZEvilPhase phase, float sim)
        {
            if (!legs || !legs.Ready || legs.Data == null) return;
            if (sim > 0f)
            {
                dWindup = Mathf.SmoothDamp(dWindup, phase == ZEvilPhase.Windup ? 1f : 0f, ref dVel, 0.05f, 40f, sim);
                dDrive = Mathf.SmoothDamp(dDrive, phase == ZEvilPhase.Burst && !brakeWas ? 1f : 0f, ref ddVel, 0.03f, 60f, sim);
                float end = brakeT + RockSeconds;
                dPlant = brakeAge < end ? Mathf.Clamp01(brakeAge / 0.05f) : brakeAge < end + SettleSeconds ? 1f - Mathf.SmoothStep(0f, 1f, (brakeAge - end) / SettleSeconds) : 0f;
            }
            if (dWindup > 0.001f) WriteLegs(legs.Data.Find("dash_windup"), dWindup, legs.Data);
            if (dDrive > 0.001f) WriteLegs(legs.Data.Find("dash_drive"), dDrive, legs.Data);
            if (dPlant > 0.001f) WriteLegs(legs.Data.Find("dash_plant"), dPlant, legs.Data);
        }

        /// <summary>The brake's clock, read from UnitView: the lean back on the heels while he skids, the rock forward over his feet when he stops, then upright.</summary>
        void Brake()
        {
            bool on = view && view.EvilBraking;
            if (on && !brakeWas) { brakeAge = 0f; brakeT = view.EvilBrakeSeconds; }
            brakeWas = on;
            float end = brakeT + RockSeconds;
            if (brakeAge < brakeT) { brakeLean = -BrakeLean * Mathf.SmoothStep(0f, 1f, brakeAge / 0.07f); rock = 0f; }
            else if (brakeAge < end) { float u = Mathf.SmoothStep(0f, 1f, (brakeAge - brakeT) / RockSeconds); brakeLean = Mathf.Lerp(-BrakeLean, RockLean, u); rock = u; }
            else if (brakeAge < end + SettleSeconds) { float u = Mathf.SmoothStep(0f, 1f, (brakeAge - end) / SettleSeconds); brakeLean = RockLean * (1f - u); rock = 1f - u; }
            else { brakeLean = 0f; rock = 0f; }
        }

        /// <summary>Standing with nothing to do and no enemy in front of him: a look around, a shift of his weight, and now and then, on camera only, the slide worked with its sound.</summary>
        void Rest(ZObject o, ZEvilPhase phase, float sim)
        {
            if (sim <= 0f || portrait) return;
            if (!reloadHeard && idlePump >= IdleSoundAt) { reloadHeard = true; if (o != null) AudioDirector.Fire(AudioDirector.EzReloadId, ChestPos, o.Id); }
            bool rest = o != null && phase == ZEvilPhase.None && !o.IsMoving && !shotDue && fw < 0.05f && alert == null && sinceShot > RestAfterShot && brakeAge > brakeT + 1f && weight >= 1f && legs && legs.ProgramName == "stand";
            if (nextPump < 0f) nextPump = UnityEngine.Random.Range(PumpFirstMin, PumpFirstMax);
            if (!rest) { idleFor = 0f; lookOn = false; shiftWant = 0f; }
            else
            {
                if (idleFor <= 0f) { nextLook = UnityEngine.Random.Range(0.8f, 2.4f); nextShift = UnityEngine.Random.Range(2.5f, 5f); }
                idleFor += sim; nextPump -= sim;
                bool seen = OzShakeGate.OnScreen(ChestPos);
                if (idleFor >= nextLook)
                {
                    nextLook = idleFor + UnityEngine.Random.Range(LookEveryMin, LookEveryMax);
                    bool wasOn = lookOn; lookOn = !lookOn || UnityEngine.Random.value < 0.7f;   // now and then back to the front
                    lookYaw = UnityEngine.Random.Range(-LookLeft, LookRight); lookRise = UnityEngine.Random.Range(-LookUpDown, LookUpDown);
                    if (seen && (lookOn || wasOn)) AudioDirector.Fire("rob_servo", ChestPos, o.Id, 0.3f);
                }
                if (idleFor >= nextShift)
                {
                    nextShift = idleFor + UnityEngine.Random.Range(ShiftEveryMin, ShiftEveryMax);
                    shiftWant = Mathf.Abs(shiftWant) > 0.5f ? 0f : UnityEngine.Random.value < 0.5f ? -1f : 1f;
                    if (seen) AudioDirector.Fire(AudioDirector.EzArmourId, ChestPos, o.Id, 0.55f);
                }
                if (nextPump <= 0f && idlePump >= PumpSettled && ezT < 0f)
                {
                    if (seen) { idlePump = 0f; reloadHeard = false; nextPump = UnityEngine.Random.Range(PumpEveryMin, PumpEveryMax); }
                    else nextPump = PumpRetry;
                }
            }
            shift = Mathf.SmoothDamp(shift, shiftWant, ref shiftVel, 0.6f, 4f, sim);
        }
        float lookRise;

        /// <summary>Row 82: how far the gun is turned for the shell, 0..1, from sinceShot.</summary>
        static float Load01(float t)
        {
            if (t < LoadFrom || t >= LoadBack) return 0f;
            if (t < LoadAtBelt) return Mathf.SmoothStep(0f, 1f, (t - LoadFrom) / (LoadAtBelt - LoadFrom));
            if (t < LoadPushed) return 1f;
            return 1f - Mathf.SmoothStep(0f, 1f, (t - LoadPushed) / (LoadBack - LoadPushed));
        }

        /// <summary>Row 82: the left hand from sinceShot. Outside the load it holds the fore-end exactly as before (onFore, the palm turned to palmW when turn). In the load: to
        /// the belt (the palm's turn let go on the way), a moment there, back under the receiver (the turn taken up again), the thumb's push up into the port (the shell-in
        /// sound plays then, from AudioDirector.EzShot), then forward along the gun onto the fore-end. Each leg eased in and out, the hand's way bowed forward of his body.</summary>
        void LeftLoad(Vector3 onFore, Vector3 belt, Quaternion palmW, bool turn, Vector3 poleL, float k, Vector3 F)
        {
            float s = sinceShot, w = 1f, bw = 1f; Vector3 at = onFore;
            if (!portrait && s < shellClock) shellOwed = s < ShellSoundAt;                              // a new shot started the clock again
            shellClock = s;
            if (!(s >= LoadFrom && s < LoadBack) && ezT >= 0f && ezKind == 1) { s = ezT + LoadFrom; bw = ezW; }   // row 87: the idle gun check, the same hand on its own clock
            if (s >= LoadFrom && s < LoadBack)
            {
                Vector3 onPort = onFore + Vector3.Project(grip.position - foregrip.position, weapon.forward) * PortBack - weapon.up * (PortDrop * k);
                float U(float a, float b) => Mathf.SmoothStep(0f, 1f, (s - a) / (b - a));
                Vector3 Bow(float u) => F * (LoadArc * k * Mathf.Sin(u * Mathf.PI));
                if (s < LoadAtBelt) { float u = U(LoadFrom, LoadAtBelt); at = Vector3.Lerp(onFore, belt, u) + Bow(u); w = 1f - u; }
                else if (s < LoadBeltOff) { at = belt; w = 0f; }
                else if (s < LoadAtPort) { float u = U(LoadBeltOff, LoadAtPort); at = Vector3.Lerp(belt, onPort, u) + Bow(u); w = u; }
                else if (s < LoadPushed) at = onPort + weapon.up * (LoadPush * k * Mathf.Sin(U(LoadAtPort, LoadPushed) * Mathf.PI));
                else at = Vector3.Lerp(onPort, onFore, U(LoadPushed, LoadBack));
            }
            if (bw < 1f) { at = Vector3.Lerp(onFore, at, bw); w = Mathf.Lerp(1f, w, bw); }          // row 87: cancelled, back onto the fore-end in IdleCancel
            armLi.Reach(at, poleL);
            if (turn) armLi.TurnHand(w >= 1f ? palmW : Quaternion.Slerp(handL.rotation, palmW, w), 0.55f);
            if (shellOwed && sinceShot >= ShellSoundAt && !portrait) { shellOwed = false; Shells++; }                // the shell is in
        }

        /// <summary>The hips moved by off (world) with the feet where they stand: both legs follow by a small two-bone solve, the knees keep their side, the soles keep their lie.</summary>
        void HipsTo(Vector3 off)
        {
            if (off.sqrMagnitude < 1e-7f || !legL || !legR || !footL || !footR) return;
            Vector3 al = footL.position, ar = footR.position; Quaternion ql = footL.rotation, qr = footR.rotation;
            hips.position += off;
            Leg(upLegL, legL, footL, al, ql); Leg(upLegR, legR, footR, ar, qr);
        }

        static void Leg(Transform up, Transform lo, Transform foot, Vector3 ankle, Quaternion keep)
        {
            Vector3 a = up.position, b = lo.position, c = foot.position;
            float la = (b - a).magnitude, lb = (c - b).magnitude, d = Mathf.Clamp((ankle - a).magnitude, Mathf.Abs(la - lb) + 1e-3f, la + lb - 1e-3f);
            Vector3 u = (ankle - a).normalized, side = Vector3.ProjectOnPlane(b - a, u);
            if (side.sqrMagnitude < 1e-8f) return;
            float ca = (la * la + d * d - lb * lb) / (2f * la * d);
            Vector3 knee = a + u * (ca * la) + side.normalized * (Mathf.Sqrt(Mathf.Max(0f, 1f - ca * ca)) * la);
            up.rotation = Quaternion.FromToRotation(b - a, knee - a) * up.rotation;
            Vector3 b1 = lo.position;
            lo.rotation = Quaternion.FromToRotation(foot.position - b1, a + u * d - b1) * lo.rotation;
            foot.rotation = keep;
        }

        /// <summary>A static leg program over what OzHeroLegs wrote this frame, w of it (bones and hips height as that component writes them).</summary>
        void WriteLegs(OzHeroLegs.Program p, float w, OzHeroLegs.LegData d)
        {
            if (p == null || d == null || d.J == null) return;
            if (bonesL == null) { bonesL = new Transform[d.Bones.Length]; var all = anim.GetComponentsInChildren<Transform>(true); for (int i = 0; i < bonesL.Length; i++) foreach (var t in all) if (t.name == d.Bones[i]) { bonesL[i] = t; break; } }
            p.Sample(0f, qa, out var hp);
            float sc = Mathf.Max(0.1f, anim.transform.lossyScale.y);
            var lift = hips.parent ? hips.parent.InverseTransformVector(Vector3.up * (d.J.soleLift - d.J.soleY * sc)) : Vector3.zero;
            hips.localPosition = Vector3.Lerp(hips.localPosition, hp + lift, w);
            for (int b = 0; b < p.NBones && b < bonesL.Length; b++) if (bonesL[b]) bonesL[b].localRotation = Quaternion.Slerp(bonesL[b].localRotation, qa[b], w);
        }
        Transform[] bonesL;

        // ---------------------------------------------------------------------------- 1.3 rows 86 / 87: the kick and the look, readable at the game camera
        // Both ran in the exe as in the editor (one code path, the same numbers) but read as nothing from
        // the RTS camera: a shot moved his hat 16-17 px and the gun's front 26-27 px at 35 m for about a quarter of a second (about half at 70 m, less again firing up or
        // down the screen), and standing he moved under 5 px in 18 s: his look turned only his head, under a round hat (open_fixes_3nov/ez_recoil_visible).
        /// <summary>The kick, over row 66's recoil. The upper body and the gun rock back together as one piece about the lower back (so the hands keep their grip) and turn
        /// a little to his right as the butt drives the right shoulder back; the hips sit back and down over his feet; the whole figure slides back on its soles and comes
        /// home. KickRock, KickTwist: degrees; KickSit, KickSink, KickSlide: rig metres. A dash shot is DashKick harder.</summary>
        public static float KickRock = 10f, KickTwist = 5f, KickSit = 0.06f, KickSink = 0.07f, KickSlide = 0.06f;
        /// <summary>The kick's clock, seconds after the shot leaves the gun: up by KickUpAt, held to KickHoldTo, back and KickOver of it past rest by KickBackAt, at rest
        /// at KickEndAt; the slide back by SlideBackAt, held to SlideHoldTo, home at SlideEndAt. KickMaxRate: at a raised game speed this clock runs at most that much
        /// faster than real time, so the kick stays readable.</summary>
        public static float KickUpAt = 0.07f, KickHoldTo = 0.24f, KickBackAt = 0.62f, KickEndAt = 0.92f, KickOver = 0.12f, SlideBackAt = 0.12f, SlideHoldTo = 0.34f, SlideEndAt = 0.90f, KickMaxRate = 1.25f;
        /// <summary>At rest his look turns his shoulders and the carried gun with it: LookTrunk of the look's angle, at most LookTrunkLeft / LookTrunkRight degrees (the
        /// head turns the rest), eased over LookTrunkSeconds; gone while the fire hold comes up.</summary>
        public static float LookTrunk = 0.4f, LookTrunkLeft = 14f, LookTrunkRight = 12f, LookTrunkSeconds = 0.45f;
        float kickT = 99f, kickSeen = 99f, kick, slideK, lookTurn, lookTurnVel; Quaternion kq = Quaternion.identity;

        /// <summary>Checks: this frame's kick (1 at its peak, a dash shot more), the slide back (0..1, a dash shot more), the shoulders' turn with the look (degrees).</summary>
        public float Kick => kick;
        public float SlideBack => slideK;
        public float LookTurn => lookTurn;

        /// <summary>The frame's kick, slide and look turn. A shot is seen by sinceShot falling (OnShot sets it to the shot's lateness); the kick's clock starts there one
        /// frame in, as the flash is first drawn. While he is a unit's view his Animator keeps animating off screen too (UnitView.Bind sets CullUpdateTransforms).</summary>
        void KickClock(float sim)
        {
            if (sim <= 0f) return;
            if (view && anim.cullingMode != AnimatorCullingMode.AlwaysAnimate) anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            float gs = ZGame.I ? Mathf.Max(0.25f, ZGame.I.gameSpeed) : 1f, step = Time.deltaTime * Mathf.Min(gs, KickMaxRate);
            kickT = sinceShot < kickSeen ? step : kickT + step;
            kickSeen = sinceShot;
            kick = KickCurve(kickT) * shotKick;
            slideK = (Ramp(kickT, 0f, SlideBackAt) - Ramp(kickT, SlideHoldTo, SlideEndAt)) * shotKick;
            float want = lookOn && !(ezT >= 0f && ezKind == 0) ? Mathf.Clamp(LookTrunk * lookYaw, -LookTrunkLeft, LookTrunkRight) : 0f;
            lookTurn = Mathf.SmoothDamp(lookTurn, want, ref lookTurnVel, LookTrunkSeconds, 90f, sim);
        }

        /// <summary>The kick's rock back about his right (turned with the aim) and its turn to his right, with the look's turn at rest: one turn for the upper body about
        /// the lower back (spine1) and for the gun's wanted lie about the same point.</summary>
        Quaternion KickTurn(Vector3 up, Vector3 right)
        {
            kq = Quaternion.AngleAxis((lookTurn + ezYaw) * (1f - fw) + KickTwist * kick, up) * Quaternion.AngleAxis(-KickRock * kick, right);
            return kq;
        }

        /// <summary>The kick's hips and slide, after HipsTo: the hips back and down over the feet, and the whole figure slid back on its soles, the feet with it; both legs
        /// follow by HipsTo's small solve. front: his front turned with the aim.</summary>
        void KickLegs(Vector3 front, float k)
        {
            if ((Mathf.Abs(kick) < 1e-4f && slideK < 1e-4f) || !legL || !legR || !footL || !footR) return;
            Vector3 back = -front * (KickSlide * k * slideK), sit = -front * (KickSit * k * kick) - Vector3.up * (KickSink * k * Mathf.Max(0f, kick));
            Vector3 al = footL.position + back, ar = footR.position + back; Quaternion ql = footL.rotation, qr = footR.rotation;
            hips.position += sit + back;
            Leg(upLegL, legL, footL, al, ql); Leg(upLegR, legR, footR, ar, qr);
        }

        // ---------------------------------------------------------------------------- 1.3 row 87: the idle, readable at the game camera
        // Once he has stood IdleStart seconds with nothing to do (no order, no move, no shot, no enemy he holds the gun on), every IdleEveryMin..Max seconds one action,
        // alternating: a look-around (trunk, head and the carried gun turn together IdleLookMin..Max degrees, left and right in turn: in, hold, back, eased; the hips carry
        // IdleHipsShare of it over planted feet; the head keeps its carry angle to the chest, about 30 degrees) and a gun check (the reload kit's hand to the belt and the
        // shell into the port, then the rack, with their sounds). Any order, move or shot blends it out in IdleCancel. A per-instance view clock: no Random, nothing in the rules.
        public static float IdleStart = 3f, IdleEveryMin = 6f, IdleEveryMax = 9f, IdleLookMin = 30f, IdleLookMax = 40f, IdleLookIn = 0.5f, IdleLookHold = 1.2f, IdleLookOut = 0.6f;
        public static float IdleHipsShare = 0.5f, IdleHeadLag = 0.06f, IdleCancel = 0.15f, IdleRackAt = 0.95f;
        float ezIdle, ezNext = -1f, ezT = -1f, ezW, ezYaw, ezSideYaw; int ezN, ezKind; bool ezShellHeard, ezPumpMine, ezBackHeard;
        /// <summary>Checks: idle actions started so far, the one running (-1 none, 0 look, 1 gun check), the look's turn this frame (degrees).</summary>
        public int IdleActions => ezN;
        public int IdleKind => ezT >= 0f ? ezKind : -1;
        public float IdleYaw => ezYaw;

        static float Hash01(int id, int n) { unchecked { uint x = (uint)id * 2654435761u ^ (uint)(n + 7) * 2246822519u; x ^= x >> 15; x *= 2246822519u; x ^= x >> 13; return (x & 0xFFFFFFu) / 16777216f; } }

        void IdleTick(ZObject o, ZEvilPhase phase, float sim)
        {
            if (sim <= 0f || portrait) return;
            var at = o != null ? o.AttackObject : null;
            bool idle = o != null && phase == ZEvilPhase.None && !o.IsMoving && !shotDue && sinceShot > IdleStart && (at == null || at.Destroyed || at.KillAt >= 0)
                && fw < 0.05f && weight >= 1f && brakeAge > brakeT + 1f && legs && legs.ProgramName == "stand";   // standing on his feet: never seated in a vehicle, never mid-step
            if (!idle)
            {
                ezIdle = 0f; ezNext = IdleStart;
                if (ezT >= 0f) { ezW = Mathf.MoveTowards(ezW, 0f, sim / IdleCancel); if (ezW <= 0f) EzEnd(); }   // cancelled: blended out, its clock held
            }
            else
            {
                if (ezNext < 0f) ezNext = IdleStart;
                ezIdle += sim;
                bool seen = OzShakeGate.OnScreen(ChestPos);
                if (ezT < 0f && ezIdle >= ezNext)
                {
                    ezKind = ezN % 2; ezT = 0f; ezW = 1f; ezShellHeard = ezPumpMine = ezBackHeard = false;
                    ezSideYaw = ezKind == 0 ? ((ezN / 2) % 2 == 0 ? -1f : 1f) * Mathf.Lerp(IdleLookMin, IdleLookMax, Hash01(o.Id, ezN)) : 0f;
                    ezN++; ezNext = ezIdle + Mathf.Lerp(IdleEveryMin, IdleEveryMax, Hash01(o.Id, ezN + 101));
                    if (ezKind == 0) { lookOn = false; if (seen) AudioDirector.Fire("rob_servo", ChestPos, o.Id, 0.45f); }
                }
                else if (ezT >= 0f)
                {
                    ezT += sim; ezW = Mathf.MoveTowards(ezW, 1f, sim / IdleCancel);
                    if (ezKind == 0)
                    {
                        if (!ezBackHeard && ezT >= IdleLookIn + IdleLookHold) { ezBackHeard = true; if (seen) AudioDirector.Fire(AudioDirector.EzArmourId, ChestPos, o.Id, 0.55f); }
                        if (ezT >= IdleLookIn + IdleLookHold + IdleLookOut) EzEnd();
                    }
                    else
                    {
                        if (!ezShellHeard && ezT >= ShellSoundAt - LoadFrom) { ezShellHeard = true; if (seen) AudioDirector.Fire(AudioDirector.EzShellId, ChestPos, o.Id); }
                        if (!ezPumpMine && ezT >= IdleRackAt) { ezPumpMine = true; idlePump = 0f; reloadHeard = !seen; }   // the rack: the slide's work at rest, its reload sound from Rest
                        if (ezT >= IdleRackAt + PumpSettled) EzEnd();
                    }
                }
            }
            float u = 0f;
            if (ezT >= 0f && ezKind == 0)
            {
                float a = IdleLookIn, b = IdleLookIn + IdleLookHold;
                u = ezT < a ? Mathf.SmoothStep(0f, 1f, ezT / a) : ezT < b ? 1f : 1f - Mathf.SmoothStep(0f, 1f, (ezT - b) / IdleLookOut);
            }
            ezYaw = ezSideYaw * u * ezW;
        }

        void EzEnd()
        {
            if (ezPumpMine && idlePump < PumpSettled) { idlePump = 99f; reloadHeard = true; }        // a rack cut short does not come back
            ezT = -1f; ezW = 0f; ezYaw = 0f; ezPumpMine = false;
        }

        /// <summary>Row 87: the hips turned deg about the vertical with the feet where they stand; both legs follow by HipsTo's small solve.</summary>
        void HipsTurn(float deg)
        {
            if (!legL || !legR || !footL || !footR) return;
            Vector3 al = footL.position, ar = footR.position; Quaternion ql = footL.rotation, qr = footR.rotation;
            hips.rotation = Quaternion.AngleAxis(deg, Vector3.up) * hips.rotation;
            Leg(upLegL, legL, footL, al, ql); Leg(upLegR, legR, footR, ar, qr);
        }

        static float Ramp(float t, float a, float b) => t <= a ? 0f : t >= b ? 1f : Mathf.SmoothStep(0f, 1f, (t - a) / (b - a));
        /// <summary>The kick: up fast, held, back and a little past rest, settled.</summary>
        static float KickCurve(float t)
        {
            if (t < 0f || t >= KickEndAt) return 0f;
            return Ramp(t, 0f, KickUpAt) - (1f + KickOver) * Ramp(t, KickHoldTo, KickBackAt) + KickOver * Ramp(t, KickBackAt, KickEndAt);
        }

        static float Recoil(float t)
        {
            if (t < 0f || t >= RecoilEnd) return 0f;
            return t < RecoilPeak ? Mathf.SmoothStep(0f, 1f, t / RecoilPeak) : 1f - Mathf.SmoothStep(0f, 1f, (t - RecoilPeak) / (RecoilEnd - RecoilPeak));
        }
        /// <summary>The slide: 0 forward, 1 at its rear stop.</summary>
        static float Pump(float t)
        {
            if (t < PumpBackFrom || t >= PumpFwdAt) return 0f;
            if (t < PumpBackAt) return Mathf.SmoothStep(0f, 1f, (t - PumpBackFrom) / (PumpBackAt - PumpBackFrom));
            if (t < PumpFwdFrom) return 1f;
            return 1f - Mathf.SmoothStep(0f, 1f, (t - PumpFwdFrom) / (PumpFwdAt - PumpFwdFrom));
        }
        /// <summary>The gun tipped up while he works the slide.</summary>
        static float Tip(float t)
        {
            if (t < PumpBackFrom || t >= PumpSettled) return 0f;
            if (t < PumpBackAt) return Mathf.SmoothStep(0f, 1f, (t - PumpBackFrom) / (PumpBackAt - PumpBackFrom));
            if (t < PumpFwdAt) return 1f;
            return 1f - Mathf.SmoothStep(0f, 1f, (t - PumpFwdAt) / (PumpSettled - PumpFwdAt));
        }
    }
}
