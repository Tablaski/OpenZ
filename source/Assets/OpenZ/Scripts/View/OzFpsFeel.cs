// OpenZ (GPL-3.0) - version 1.5 kit fps_feel: the feel of the private first-person view (View/OzFpsView.cs). View only, except the sprint
// move it asks for (Sim/ZObject.SprintOrder.cs, DV178), which only this view ever sends.
using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace OpenZ
{
    /// <summary>
    /// Version 1.5 kit fps_feel. Four things on top of the first-person view (OzFpsView), all on the view's own camera, all off while the view is off:
    /// <list type="bullet">
    /// <item>(1) Weapon in range: while an enemy the viewed unit can attack is inside its weapon's reach (the rules' own test,
    /// ZObject.CanAttackObject + WithinAttackRadius, as View/BS01Stance.cs asks it; one the fog of war covers does not count), the gun copy in the
    /// lower part of the view eases slightly whiter over <see cref="WhitenSeconds"/> s (an emission lift on the copy only, by day
    /// <see cref="WhitenDay"/>, at night <see cref="WhitenNight"/>, linear) and back the same way when nothing is in reach. A target must stay in
    /// (or out of) reach <see cref="RangeHold"/> s before the copy changes, so a target at the edge of the reach never makes it blink. The
    /// rising edge plays the game's own lock blip (ui_target_lock), at most every <see cref="LockBlipGap"/> s.</item>
    /// <item>(2) Shift held while walking: the view's move order carries the sprint mark (PlayerInput.FpsSprint; the rule DV178 in
    /// Sim/ZObject.SprintOrder.cs: x ZWorld.SprintPct / 100 while that order is the unit's first waypoint). Pressing or letting go of Shift
    /// sends the next order at once (with or without the mark). A gun does not walk; in 'Zod exact' the rule's switch is off and Shift changes
    /// nothing.</item>
    /// <item>(3) In and out: the camera glides between the RTS camera's pose and the eye over <see cref="GlideSeconds"/> s (a cubic ease in and
    /// out; the field of view and the near clip with it), a soft whoosh (ui_band_whoosh) each way. A second Alt+F (or Esc) during the glide
    /// in turns it round smoothly (the speed it had is carried on), an Alt+F during the glide out goes back in the same way. While the camera
    /// glides out the RTS camera, the keys and the clicks wait (OzFpsView.On stays true until the glide ends; the bound keys and Esc mean
    /// nothing for those 0.75 s). Leaves that belong to the game (the level ends, the pause menu, a scene, a question, the camera switched off)
    /// are instant, exactly as before. While the camera is high above the eye the gun copy is lowered out of sight and the crosshair waits.</item>
    /// <item>(4) Units by the eye fade: a unit (a squad mate, any robot, vehicle or gun) or a crew figure coming near the eye is dithered away
    /// with OpenZ/TeamLit's own 4 x 4 screen-door cut (_OZCamFade, written as OzShuttleCamFade writes it), from <see cref="FadeClearM"/> m (drawn)
    /// to <see cref="FadeGoneM"/> m (gone), eased over at least <see cref="FadeSeconds"/> s, never in one frame; its non-TeamLit parts (glows,
    /// particles) are held off inside the camera's pictures once it is half gone. Before, OzFpsView held the whole unit off at 1.5 m and gave it
    /// back at 2.5 m (a snap). The viewed unit's own model and crew fade the same way during the glide (their own cut follows the glide), and
    /// are held off by OzFpsView as before once the camera is at the eye.</item>
    /// </list>
    /// <para>
    /// (4) took only the renderers under
    /// the unit's own view and held its non-TeamLit parts off inside the pictures only. Left drawn were a big tank's code-built parts (team
    /// marker lamps and halos, head lamp housings, exhaust stubs, pads, neon, searchlight, beacon: OzBigTanksFx keeps them under OZ_BigTanksFx),
    /// a vehicle's crew figure (BoardingView keeps it under OZ_BoardingCrew, and it was judged by its own distance: the Mammoth's hatch
    /// commander, 3 m and more from an eye at the hull, stayed whole), VehicleLights' lamp glows, haze cones and ground pool (Graphics.DrawMesh,
    /// no renderer at all), a block-less non-TeamLit mesh part (the GPU Resident Drawer draws it from its own copy: a switch inside one picture
    /// never reaches it), sprites and billboards (not collected) and its lights. Now everything of a unit by the eye goes and comes back with it:
    /// its crew figure takes the vehicle's cut (never less than its own; a crewman who gets out is his own unit again, BoardingView then gives
    /// the vehicle no crew figure), a big tank's code-built parts are dithered with it and their holders switched off from
    /// <see cref="HideOthersAt"/> (View/OzBigTanksFx.FpsHold.cs, as for the viewed tank), VehicleLights draws no glow, cone or pool and keeps
    /// its real light off from there (as for a vehicle the fog hides), every renderer kind is taken, a block-less mesh part gets the badge
    /// hold's block (OzFpsBadgeHold.Keep: the engine culls it again, so the per-picture hold reaches it), the unit's lights are off inside the
    /// pictures from there, and its parts are collected again every frame while it is faded at all (a part another kit adds then joins at
    /// once). World smoke and dust already in the air stay as they are. <see cref="EyeHides"/> is the readers' test.</para>
    /// The 1.4 Play checks of the view (OzFpsViewPlayCheck) measure the 1.4 view itself: while one of them runs, the glide, the fade and the
    /// whitening are not used (the view behaves exactly as 1.4). The 1.5 Play check (View/OzFpsFeelPlayCheck.cs) measures these.
    /// Hook lines in View/OzFpsView.cs (each a static read, false or the old value while the view is off): On, Holds, Begin (Entering), Leave
    /// (Leaving), OnBeginCam (OwnDrawn, Fades), OnGUI (HideGui), Walk (SprintFlip, Sprint).
    /// </summary>
    [DefaultExecutionOrder(31950)]                                                                       // LateUpdate after OzFpsView (31900) placed the eye, before the 32000 readers of the camera's pose
    public sealed partial class OzFpsFeel : MonoBehaviour
    {
        // ------------------------------------------------------------------ switches (for a before / after look; on in the game)
        public static bool GlideOn = true, FadeOn = true, WhitenOn = true, SprintOn = true;

        // ------------------------------------------------------------------ the numbers
        /// <summary>The glide's length for the whole way (s); a turn round takes the share of the way left, at least <see cref="GlideMinSeconds"/>.</summary>
        public const float GlideSeconds = 0.75f, GlideMinSeconds = 0.2f;
        /// <summary>The gun copy is this far (m, camera space) under its place at the start of the glide in, and rises into place from
        /// <see cref="GunRiseFrom"/> of the way on; the crosshair waits until <see cref="GuiFrom"/> of the way.</summary>
        public const float GunDrop = 0.35f, GunRiseFrom = 0.6f, GuiFrom = 0.9f;
        /// <summary>The viewed vehicle's own lamp glows are held from this share of the way (the eye sits in their halos at the end).</summary>
        public const float LampHoldFrom = 0.85f;
        /// <summary>The whoosh: volume and pitch out (in: pitch 1).</summary>
        public const float WhooshVol = 0.75f, WhooshOutPitch = 0.9f;
        /// <summary>The fade of units by the eye: gone at or under FadeGoneM (m, the nearest part box), drawn from FadeClearM; at most one full
        /// fade per FadeSeconds (FadeFastSeconds while a part is within FadeFastM, the camera nearly in it); non-TeamLit parts held off from
        /// HideOthersAt of the cut. Roots looked at within ScanRootM (unit views) / ScanCrewM (crew figures).</summary>
        public const float FadeGoneM = 1.2f, FadeClearM = 2.8f, FadeSeconds = 0.25f, FadeFastSeconds = 0.08f, FadeFastM = 0.5f, HideOthersAt = 0.5f;
        public const float ScanRootM = 12f, ScanCrewM = 6f, CollectEvery = 0.5f;
        /// <summary>The viewed unit's own model during the glide: drawn up to OwnFadeFrom of the way, gone from OwnFadeTo.</summary>
        public const float OwnFadeFrom = 0.55f, OwnFadeTo = 0.9f;
        /// <summary>The weapon in range: the ease (s), the emission lift by day and at night (linear, added), how often the reach is asked (s),
        /// how long the answer must hold (s), the lock blip's gap (s) and volume.</summary>
        public const float WhitenSeconds = 0.25f, WhitenDay = 0.10f, WhitenNight = 0.045f, RangeScan = 0.1f, RangeHold = 0.15f, LockBlipGap = 2.5f, LockVol = 0.7f;

        // ------------------------------------------------------------------ the hooks' reads (View/OzFpsView.cs)
        static OzFpsFeel inst;
        public static OzFpsFeel I => inst;
        /// <summary>OzFpsView.On: the camera glides out (the view's camera, keys and Esc stay the view's until it is back).</summary>
        public static bool GlideOutNow => inst && inst.gliding && inst.dir < 0;
        /// <summary>OzFpsView.OnBeginCam: the viewed unit's own model is drawn (and faded here) while the camera glides; held off by the view at the eye.</summary>
        public static bool OwnDrawn => inst && inst.active && inst.gliding;
        /// <summary>OzFpsView.OnBeginCam: figure t (a crew figure or another unit's view by the eye) is faded here, not held off by the view. The
        /// unit's own crew figure (crewT) only while the camera glides.</summary>
        public static bool Fades(Transform t, Transform crewT) => inst && inst.active && FadeOn && t && (t != crewT || inst.gliding);
        /// <summary>OzFpsView.OnGUI: the crosshair and the ESC hint wait while the camera is still high.</summary>
        public static bool HideGui => inst && inst.active && inst.gliding && inst.dir > 0 && Mathf.Clamp01(inst.g) < GuiFrom;
        /// <summary>OzFpsView.Holds: the viewed vehicle's lamp glows held (VehicleLights). held = the view's own answer; during a glide only from
        /// <see cref="LampHoldFrom"/> of the way (the camera near the eye), so they are not switched off while the camera is still far.</summary>
        public static bool LampHold(UnitView v, bool held)
        {
            if (!inst || !inst.active || !inst.gliding) return held;
            var fv = OzFpsView.I;
            return fv && v && fv.View == v && Mathf.Clamp01(inst.g) >= LampHoldFrom;
        }

        // ------------------------------------------------------------------ counters and values for the checks
        public static int GlidesIn, GlidesOut, Reversals, Instants, Whooshes, ReEntries, FadeWrites, OthersHeld, WhitenRises, LockBlips, SprintOrders, SprintFlips, CopyMaterials;
        public static float LastGlideSeconds = -1f, WhitenNow;
        public static string LastInstant = "";
        public static float GlideS => inst ? Mathf.Clamp01(inst.g) : 0f;
        public static bool Gliding => inst && inst.gliding;
        public static int GlideDir => inst ? inst.dir : 0;
        public static bool InRangeNow => inst && inst.inRange;
        /// <summary>The cut this writes on the object under t now (0 = drawn as always, or not faded here).</summary>
        public static float CutOf(Transform t) => inst && t && inst.fades.TryGetValue(t, out var f) ? f.shown : 0f;
        public static int FadeEntries => inst ? inst.fades.Count : 0;
        public static int HeldNow => inst ? inst.heldNow.Count : 0;
        /// <summary>1.5 close_hide_all_parts_r6: unit view v is another unit by the eye faded at least <see cref="HideOthersAt"/> of the way (from
        /// there its non-TeamLit parts and its lights are held off): View/VehicleLights.cs then draws none of its lamp glows, cones or pool and
        /// keeps its real light off, View/OzBigTanksFx.FpsHold.cs switches a big tank's code-built parts off. Never the viewed unit itself; false
        /// always once the view is over and its fades have eased out.</summary>
        public static bool EyeHides(UnitView v) => inst && inst.active && v && inst.fades.TryGetValue(v.transform, out var f) && !f.own && f.shown >= HideOthersAt;
        /// <summary>close_hide_all_parts_r6 counters: crew figures linked to their vehicle this frame (and the most at once), block-less mesh parts
        /// given the badge hold's block, lights held off in the last picture, big-tank code-built parts taken into a fade.</summary>
        public static int CrewLinked, CrewLinkedMax, KeepBlocks, LightsHeld, TankPartsTaken;
        /// <summary>A 1.4 Play check of the view is running (the view then behaves as 1.4).</summary>
        public static bool Legacy => inst && inst.legacy;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            inst = null; GlideOn = FadeOn = WhitenOn = SprintOn = true; sprintSent = false;
            GlidesIn = GlidesOut = Reversals = Instants = Whooshes = ReEntries = FadeWrites = OthersHeld = WhitenRises = LockBlips = SprintOrders = SprintFlips = CopyMaterials = 0;
            LastGlideSeconds = -1f; WhitenNow = 0f; LastInstant = "";
            CrewLinked = CrewLinkedMax = KeepBlocks = LightsHeld = TankPartsTaken = 0;                       // close_hide_all_parts_r6
        }

        // ------------------------------------------------------------------ state
        Camera cam; RtsCamera rts; PlayerInput pi;
        bool viewOn, legacy, hooked;
        bool active => !legacy;
        // the glide: g 0 = the RTS camera's pose .. 1 = the eye; one cubic segment from (g0, v0) to gTo over gD s
        bool gliding; int dir; float g = 1f, gv, g0, v0, gTo, gT, gD, glideStart;
        Pose a0, eyePose, eyeLast; float aFov = 32f, aNear = 0.5f;
        Vector3 focusEntry; bool everIn, restoreFocus, pendingLook, fogSet;
        Quaternion lookWant;

        static readonly HashSet<string> GlideWhys = new HashSet<string> { "Esc", "Alt+F again", "the unit died", "the unit got in (it left the map)", "the crew got out", "the selection changed" };

        void OnEnable()
        {
            inst = this; cam = GetComponent<Camera>(); rts = GetComponent<RtsCamera>();
            if (!hooked) { RenderPipelineManager.beginCameraRendering += OnBeginCam; RenderPipelineManager.endCameraRendering += OnEndCam; hooked = true; }
        }

        void OnDisable()
        {
            if (hooked) { RenderPipelineManager.beginCameraRendering -= OnBeginCam; RenderPipelineManager.endCameraRendering -= OnEndCam; hooked = false; }
            GiveBack();
            if (gliding) EndGlide(false);
            RestoreAllFades(); ResetWhiten(true);
            if (fogSet) { Shader.SetGlobalVector(OzFpsView.IdFowUnder, Vector4.zero); fogSet = false; }
            viewOn = false;
            if (inst == this) inst = null;
        }

        // ------------------------------------------------------------------ in (OzFpsView.Begin, before it changes anything)
        /// <summary>OzFpsView.Begin, right after it read the field of view and the near clip it gives back at the leave: the glide in starts here
        /// (from the RTS camera's pose as it is now, or, during a glide out, from where that glide is: then fov0 / near0 are the RTS camera's own
        /// values, not the glide's).</summary>
        public static void Entering(OzFpsView v, Camera c, RtsCamera r, ref float fov0, ref float near0)
        {
            if (!c) return;
            var f = c.GetComponent<OzFpsFeel>(); if (!f) f = c.gameObject.AddComponent<OzFpsFeel>();
            f.enabled = true;
            f.In(r, ref fov0, ref near0);
        }

        void In(RtsCamera r, ref float fov0, ref float near0)
        {
            cam = GetComponent<Camera>(); rts = r ? r : GetComponent<RtsCamera>();
            legacy = LegacyCheckRunning();
            bool back = gliding && dir < 0;                                                                  // an Alt+F during the glide out: turn round
            viewOn = true; pendingLook = false; restoreFocus = false;
            ResetWhiten(true);
            if (legacy || !GlideOn)
            {
                if (back) { fov0 = aFov; near0 = aNear; EndGlide(false); }
                gliding = false; g = 1f; gv = 0f; everIn = true; return;
            }
            if (back)
            {
                fov0 = aFov; near0 = aNear;                                                                  // the RTS camera's own lens, not the glide's
                pendingLook = true; lookWant = eyeLast.rotation;                                             // the view keeps looking where it looked
                Reversals++; ReEntries++;
            }
            else
            {
                a0 = new Pose(transform.position, transform.rotation); aFov = fov0; aNear = near0; g = 0f; gv = 0f;
            }
            if (!back || everIn) focusEntry = rts ? rts.focus : Vector3.zero;                              // review: a turn round before the camera ever got in keeps the player's own place from before the first Alt+F (Leave has put the focus on the unit by now)
            everIn = false; dir = 1; Segment(1f); GlidesIn++;
            Whoosh(1f);
        }

        // ------------------------------------------------------------------ out (OzFpsView.Leave, right after it decided who keeps the RTS camera)
        /// <summary>OzFpsView.Leave, right after the view was switched off and LastLeaveKeptCamera was set (review: the hook sits after that line, so
        /// a leave whose camera belongs to the ending or to another script is known here): a leave by the player (Esc, Alt+F again) or by the unit
        /// (died, got in, crew out, the selection changed) glides out; every other leave (the level ends, the pause menu, a scene, a question, the
        /// camera switched off: quiet, the ending or another script owning the camera) is instant, as before, with no whoosh.</summary>
        public static void Leaving(string why, bool quiet)
        {
            var f = inst; if (!f) return;
            f.Out(why, quiet);
        }

        void Out(string why, bool quiet)
        {
            viewOn = false;
            ResetWhiten(true);                                                                               // the copy goes with the view (OzFpsView.Leave drops it right after)
            if (legacy || !GlideOn || quiet || OzFpsView.LastLeaveKeptCamera || !GlideWhys.Contains(why ?? ""))
            {
                if (gliding) { Instants++; LastInstant = why ?? ""; }
                EndGlide(false); g = 0f; gv = 0f; return;                                                    // the view's own leave already put the RTS camera back
            }
            if (!everIn) restoreFocus = true;                                                                // never fully in: back where the player was (Leave puts the focus on the unit after this call)
            if (gliding && dir > 0) Reversals++;
            dir = -1; Segment(0f); GlidesOut++;
            Whoosh(WhooshOutPitch);
        }

        void Whoosh(float pitch) { Whooshes++; AudioDirector.UI(pitch >= 1f ? OzFpsGlideSound.In : OzFpsGlideSound.Out, WhooshVol, 1f); OzFpsVignette.Ensure(); }   // 1.5 fps_feel_whoosh_vignette: a deep air rush falling in / rising out (the direction is in the clip), the vignette's volume (View/OzFpsVignette.cs)

        /// <summary>A new cubic segment from where the glide is (its place and speed) to `to`, over the share of the way left.</summary>
        void Segment(float to)
        {
            g0 = g; v0 = gliding ? gv : 0f; gTo = to; gT = 0f;
            gD = Mathf.Max(GlideMinSeconds, GlideSeconds * Mathf.Abs(to - g));
            if (!gliding) glideStart = Time.unscaledTime;
            gliding = true;
        }

        void Step(float dt)
        {
            gT += dt;
            float u = Mathf.Clamp01(gT / gD), u2 = u * u, u3 = u2 * u;
            float h00 = 2f * u3 - 3f * u2 + 1f, h10 = u3 - 2f * u2 + u, h01 = -2f * u3 + 3f * u2;
            g = h00 * g0 + h10 * gD * v0 + h01 * gTo;
            gv = ((6f * u2 - 6f * u) * g0 + (-6f * u2 + 6f * u) * gTo) / gD + (3f * u2 - 4f * u + 1f) * v0;
            if (u >= 1f) { g = gTo; gv = 0f; EndGlide(true); }
        }

        /// <summary>The glide ends: at the eye (the view's own pose and lens from here on) or at the RTS camera (its pose, lens and keys back).
        /// done false: stopped where it is (an instant leave or a scene): the lens goes back to the RTS camera's own when out.</summary>
        void EndGlide(bool done)
        {
            if (!gliding) return;
            gliding = false;
            if (done) LastGlideSeconds = Time.unscaledTime - glideStart;
            if (dir > 0 && done)
            {
                everIn = true;
                if (cam) { cam.fieldOfView = OzFpsView.FovDeg; cam.nearClipPlane = OzFpsView.NearClip; }
                GunOffset(0f);
            }
            else if (dir < 0)
            {
                if (cam) { cam.fieldOfView = aFov; cam.nearClipPlane = aNear; }
                if (done && rts && !OzFpsView.LastLeaveKeptCamera) rts.Place();                             // the RTS camera's own pose this frame (its Update goes on from it next frame)
                if (fogSet) { Shader.SetGlobalVector(OzFpsView.IdFowUnder, Vector4.zero); fogSet = false; }
            }
        }

        /// <summary>A moment that takes the camera from the glide out at once (the pause menu, the ending, a scene, the Unit editor).</summary>
        static bool Interrupt()
        {
            var h = OpenZHud.I;
            return OpenZEnding.Running || OpenZEnding.LocksCamera || OzFpsView.LastLeaveKeptCamera || (h && h.Pause && h.Pause.IsOpen) || (h && h.Modal)   // 1.5 fps_in_ue: the Unit editor glides out as a level does (its dialogs and the leave question are h.Modal)
                   || ZodBriefing.BriefingActive || OzBlueCameo.BriefingActive || CaptureKit.CineActive || (ZodFlyover.Live != null && ZodFlyover.Live.Active);
        }

        static bool LegacyCheckRunning()
        {
#if !OZ_RELEASE
            return FindAnyObjectByType<OzFpsViewPlayCheck>() != null;
#else
            return false;
#endif
        }

        // ------------------------------------------------------------------ every frame
        void Update()
        {
            // an Alt+F during the glide out goes back in (PlayerInput waits while OzFpsView.On, which the glide out keeps true)
            if (!gliding || dir > 0 || legacy) return;
            var kb = Keyboard.current;
            if (!OzFpsView.Combo(kb) || OzFpsView.LeftFrame == Time.frameCount) return;                    // not the Alt+F that left this frame
            if (!pi) pi = FindFirstObjectByType<PlayerInput>();
            if (pi) OzFpsView.TryEnter(pi);                                                                  // refused: its quiet ui_disabled, the glide goes on
        }

        void LateUpdate() => Tick();

        /// <summary>Once a frame: in LateUpdate, or (the frame this component was added in the middle of an Update, which may get no LateUpdate)
        /// at the start of the camera's picture, before it is culled, so the first picture of the glide is the glide's and never the eye's.</summary>
        int tickFrame = -1;
        void Tick()
        {
            if (tickFrame == Time.frameCount) return;
            tickFrame = Time.frameCount;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            var fv = OzFpsView.I;
            if (restoreFocus) { restoreFocus = false; if (rts) rts.focus = focusEntry; }
            if (gliding && dir < 0 && Interrupt())
            {   // the pause menu, the ending, a scene: the camera is the RTS camera's (or the scene's) at once, as before the kit
                Instants++; LastInstant = "a moment took the camera during the glide out"; EndGlide(false); g = 0f; gv = 0f;
                if (rts && !OzFpsView.LastLeaveKeptCamera && !OpenZEnding.Running && !OpenZEnding.LocksCamera) rts.Place();
            }
            if (viewOn && fv)
            {
                if (pendingLook)
                {   // turned round during the glide out: the view looks where it looked (Begin turned it to the unit's front)
                    pendingLook = false;
                    var e = lookWant.eulerAngles; float k = Mathf.Max(1e-4f, RtsCamera.FreeLookSpeed);
                    fv.LookBy(new Vector2(Mathf.DeltaAngle(fv.Yaw, e.y) / k, (-Mathf.DeltaAngle(0f, e.x) - fv.Pitch) / k));
                    transform.rotation = Quaternion.Euler(-fv.Pitch, fv.Yaw, 0f);
                }
                eyePose = new Pose(transform.position, transform.rotation);                                 // the eye as the view placed it this frame
                eyeLast = eyePose;
            }
            if (gliding) Step(dt);
            if (gliding || (viewOn && active && g < 1f)) Place(fv);
            FadeStep(dt, fv);
            WhitenStep(dt, fv);
        }

        /// <summary>The camera between the RTS pose and the eye at the glide's share g (eased already by the segment).</summary>
        void Place(OzFpsView fv)
        {
            if (!cam) return;
            float s = Mathf.Clamp01(g);
            Pose a = viewOn || !rts ? a0 : (a0 = RtsPose());                                                 // out: the RTS camera's own pose now (kept for a turn round)
            Pose e = viewOn ? eyePose : eyeLast;
            var gp = Vector3.LerpUnclamped(a.position, e.position, s);
            transform.SetPositionAndRotation(gp, AimOn ? AimRot(a, e, gp, s) : Quaternion.Slerp(a.rotation, e.rotation, s));   // 1.5 N15: aimed at the unit all the way in / out (OzFpsFeel.AimIn.cs)
            cam.fieldOfView = Mathf.Lerp(aFov, OzFpsView.FovDeg, s);
            cam.nearClipPlane = Mathf.Exp(Mathf.Lerp(Mathf.Log(Mathf.Max(1e-3f, aNear)), Mathf.Log(OzFpsView.NearClip), s));
            if (viewOn) GunOffset(1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(GunRiseFrom, 1f, s)));
            // the MAYHEM fog's wall (OzFpsView sets it while the view is on): only while the camera is under its top
            var gm = ZGame.I; var p = transform.position;
            bool under = gm && p.y - gm.GroundY(p.x, p.z) < OzFpsView.FogWallTop;
            if (under) { Shader.SetGlobalVector(OzFpsView.IdFowUnder, new Vector4(1f, OzFpsView.FogWallTop, Mathf.Min(cam.farClipPlane, OzFpsView.FogSkyMax), 0f)); fogSet = !viewOn; }
            else { Shader.SetGlobalVector(OzFpsView.IdFowUnder, Vector4.zero); fogSet = false; }
        }

        Pose RtsPose()
        {
            rts.Place();                                                                                     // writes the RTS pose; the glide's pose is written over it right after
            return new Pose(transform.position, transform.rotation);
        }

        void GunOffset(float k)
        {
            var fv = OzFpsView.I; if (!fv || fv.GunRenderers.Count == 0 || !fv.GunRenderers[0]) return;
            var piv = fv.GunRenderers[0].transform.parent; var root = piv ? piv.parent : null;
            if (root && root.parent == transform) root.localPosition = Vector3.down * (GunDrop * k);
        }

        // ------------------------------------------------------------------ (2) Shift: the sprint mark (OzFpsView.Walk)
        static bool sprintSent;
        static bool Wants(Keyboard kb, ZObject u) => SprintOn && ZWorld.SprintOrderDeviation && kb != null && kb.shiftKey.isPressed && u != null && u.Kind != ZKind.Cannon;
        /// <summary>OzFpsView.Walk: Shift pressed or let go since the last order: a new order at once.</summary>
        public static bool SprintFlip(Keyboard kb, ZObject u) { bool f = Wants(kb, u) != sprintSent; if (f) SprintFlips++; return f; }
        /// <summary>OzFpsView.Walk, right before it sends a move: true = send it as the sprint move (PlayerInput.FpsSprint) instead.</summary>
        public static bool Sprint(Keyboard kb, ZObject u) { sprintSent = Wants(kb, u); if (sprintSent) SprintOrders++; return sprintSent; }

        // ------------------------------------------------------------------ (4) units by the eye fade
        sealed class Fade
        {
            public Transform root; public bool own, seen; public float cut, shown, collectAt;
            public readonly List<Renderer> fade = new List<Renderer>(), other = new List<Renderer>(), box = new List<Renderer>();
            public readonly List<Light> lights = new List<Light>();                                         // close_hide_all_parts_r6: off inside the pictures with its other parts
            public readonly Dictionary<Renderer, int> mats = new Dictionary<Renderer, int>();
            public readonly HashSet<Renderer> written = new HashSet<Renderer>(), mvOff = new HashSet<Renderer>();
        }
        readonly Dictionary<Transform, Fade> fades = new Dictionary<Transform, Fade>();
        readonly List<Transform> fadeDrop = new List<Transform>();
        static readonly List<Renderer> rbuf = new List<Renderer>();
        static readonly List<Material> mbuf = new List<Material>();
        static readonly List<Renderer> xbuf = new List<Renderer>();                                         // close_hide_all_parts_r6: a big tank's code-built parts (OzBigTanksFx.PartsOf)
        static readonly List<Light> lbuf = new List<Light>();
        /// <summary>close_hide_all_parts_r6, rebuilt every frame: a vehicle's or a gun's crew figure (BoardingView.CrewFigure, kept under
        /// OZ_BoardingCrew) -> its unit's view; the figure's cut is never less than its vehicle's.</summary>
        readonly Dictionary<Transform, Transform> crewOf = new Dictionary<Transform, Transform>();

        Fade Entry(Transform t, bool own)
        {
            if (!fades.TryGetValue(t, out var f)) { f = new Fade { root = t, own = own }; fades[t] = f; Collect(f); }
            f.own = own; f.seen = true;
            return f;
        }

        void Collect(Fade f)
        {
            f.fade.Clear(); f.other.Clear(); f.box.Clear(); f.lights.Clear(); f.collectAt = Time.unscaledTime + CollectEvery;
            f.root.GetComponentsInChildren(true, rbuf);
            for (int i = 0; i < rbuf.Count; i++) Take(f, rbuf[i], true);
            // close_hide_all_parts_r6: a big tank's code-built parts (under OZ_BigTanksFx, not under its view) are dithered with it; never in its
            // box (the searchlight's long beam would fade the tank from metres in front of it). Its lights: off inside the pictures (OnBeginCam).
            xbuf.Clear(); var uv = f.root.GetComponent<UnitView>(); if (uv) OzBigTanksFx.PartsOf(uv, xbuf);
            for (int i = 0; i < xbuf.Count; i++) if (Take(f, xbuf[i], false)) TankPartsTaken++;
            f.root.GetComponentsInChildren(true, lbuf); f.lights.AddRange(lbuf);
        }

        /// <summary>close_hide_all_parts_r6: one renderer of a fade entry. A TeamLit with the cut is dithered (and measured, when box); every other
        /// kind (mesh, skinned mesh, particles, trail, line, sprite, billboard: before, only the first five) is held off inside the pictures from
        /// <see cref="HideOthersAt"/>, and a block-less mesh part gets the badge hold's block (the GPU Resident Drawer draws a block-less mesh
        /// from its own copy, which a switch inside one picture never reaches; with the block the engine culls it, as the body). A shadow-only part
        /// keeps its shadow; never the gun copy (under the camera). True: taken.</summary>
        bool Take(Fade f, Renderer r, bool box)
        {
            if (!r || r.transform.IsChildOf(transform)) return false;
            if (OzShuttleCamFade.TakesFade(r)) { f.fade.Add(r); r.GetSharedMaterials(mbuf); f.mats[r] = mbuf.Count; if (box) f.box.Add(r); return true; }
            if (r.shadowCastingMode == ShadowCastingMode.ShadowsOnly) return false;
            f.other.Add(r);
            if (box && (r is MeshRenderer || r is SkinnedMeshRenderer)) f.box.Add(r);
            if (OzFpsBadgeHold.Keep(r)) KeepBlocks++;
            return true;
        }

        void FadeStep(float dt, OzFpsView fv)
        {
            bool want = active && FadeOn && (viewOn || gliding) && fv && fv.View;
            foreach (var kv in fades) kv.Value.seen = false;
            crewOf.Clear(); CrewLinked = 0;                                                                  // close_hide_all_parts_r6: linked again below
            if (want)
            {
                var eye = transform.position;
                float s = Mathf.Clamp01(g);
                float ownCut = gliding ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(OwnFadeFrom, OwnFadeTo, s)) : viewOn ? 1f : 0f;
                var own = Entry(fv.View.transform, true); own.cut = ownCut;
                var crewGo = fv.Unit != null ? BoardingView.CrewFigure(fv.Unit.Id) : null;
                Transform crew = crewGo ? crewGo.transform : null;
                if (crew) Entry(crew, true).cut = ownCut;
                var gm = ZGame.I; var lv = gm ? gm.LiveViewValues : null;
                if (lv != null)
                    for (int i = 0; i < lv.Count; i++)
                    {
                        var uv = lv[i]; if (!uv || uv == fv.View) continue;
                        var o = uv.Obj; if (o == null || !o.IsUnit || o.IsShuttle) continue;                // a shuttle: OzShuttleCamFade's own fade
                        var t = uv.transform;
                        if ((t.position - eye).sqrMagnitude > ScanRootM * ScanRootM) continue;              // farther: not seen this frame (its cut eases back to 0, then it is dropped)
                        Entry(t, false);
                        var cg = BoardingView.CrewFigure(o.Id);                                             // close_hide_all_parts_r6: its crew figure (under OZ_BoardingCrew) goes with it, however far it sits from the eye
                        if (cg && cg.transform != crew && !cg.transform.IsChildOf(t)) { Entry(cg.transform, false); crewOf[cg.transform] = t; }   // a gun's gunner sits under the gun's own view: taken with it already
                    }
                var cb = BoardingView.CrewBox;
                if (cb)
                    for (int i = 0; i < cb.childCount; i++)
                    {
                        var t = cb.GetChild(i);
                        if (t == crew || !t.gameObject.activeInHierarchy) continue;
                        if ((t.position - eye).sqrMagnitude > ScanCrewM * ScanCrewM) continue;
                        Entry(t, false);
                    }
                foreach (var kv in fades)
                {
                    var f = kv.Value; if (f.own || !f.seen || !f.root) continue;
                    if (Time.unscaledTime >= f.collectAt || f.cut > 0f) Collect(f);                         // close_hide_all_parts_r6: every frame while it is faded at all (a part added now is taken at once)
                    float d = OzShuttleCamFade.BoxDistance(f.box, eye);
                    float target = float.IsInfinity(d) ? 0f : 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(FadeGoneM, FadeClearM, d));
                    float rate = target > f.cut && d < FadeFastM ? 1f / FadeFastSeconds : 1f / FadeSeconds;
                    f.cut = Mathf.MoveTowards(f.cut, target, dt * rate);
                }
                foreach (var kv in crewOf)                                                                   // close_hide_all_parts_r6: a crew figure is never drawn more than its vehicle
                    if (fades.TryGetValue(kv.Key, out var cf) && fades.TryGetValue(kv.Value, out var vf) && vf.cut > cf.cut) cf.cut = vf.cut;
                CrewLinked = crewOf.Count; if (CrewLinked > CrewLinkedMax) CrewLinkedMax = CrewLinked;
            }
            fadeDrop.Clear();
            foreach (var kv in fades)
            {
                var f = kv.Value;
                if (!f.root) { fadeDrop.Add(kv.Key); continue; }                                             // destroyed: its renderers went with it
                if (!f.seen) f.cut = f.own ? 0f : Mathf.MoveTowards(f.cut, 0f, dt / FadeSeconds);           // out of reach (or the view is over): drawn again, others eased
                if (f.own && Time.unscaledTime >= f.collectAt) Collect(f);
                f.shown = f.own ? f.cut : Mathf.SmoothStep(0f, 1f, f.cut);
                bool heldByView = f.own && viewOn && !gliding;                                              // at the eye the view holds its own model and crew off itself
                if (!heldByView) Apply(f);
                if (f.shown <= 0f && f.cut <= 0f && !f.seen) { Restore(f); fadeDrop.Add(kv.Key); }         // kept while seen (no new scan of its parts every frame)
            }
            foreach (var t in fadeDrop) fades.Remove(t);
        }

        void Apply(Fade f)
        {
            float v = f.shown; bool on = v > 0.0005f;
            for (int i = 0; i < f.fade.Count; i++)
            {
                var r = f.fade[i]; if (!r) continue;
                int n = f.mats.TryGetValue(r, out int m) ? m : 1;
                if (on) { OzShuttleCamFade.WriteFade(r, v, n); f.written.Add(r); FadeWrites++; }           // every frame while faded: a writer that rebuilds its block drops it
                else if (f.written.Remove(r)) OzShuttleCamFade.WriteFade(r, 0f, n);
                if (on) { if (r.motionVectorGenerationMode == MotionVectorGenerationMode.Object) { r.motionVectorGenerationMode = MotionVectorGenerationMode.Camera; f.mvOff.Add(r); } }
                else if (f.mvOff.Remove(r) && r.motionVectorGenerationMode == MotionVectorGenerationMode.Camera) r.motionVectorGenerationMode = MotionVectorGenerationMode.Object;
            }
        }

        void Restore(Fade f)
        {
            foreach (var r in f.written) if (r) OzShuttleCamFade.WriteFade(r, 0f, f.mats.TryGetValue(r, out int n) ? n : 1);
            f.written.Clear();
            foreach (var r in f.mvOff) if (r && r.motionVectorGenerationMode == MotionVectorGenerationMode.Camera) r.motionVectorGenerationMode = MotionVectorGenerationMode.Object;
            f.mvOff.Clear(); f.cut = f.shown = 0f;
        }

        void RestoreAllFades() { foreach (var kv in fades) if (kv.Value.root) Restore(kv.Value); fades.Clear(); }

        // the non-TeamLit parts of a half-gone unit: held off inside this camera's pictures only (as OzFpsView holds its renderers)
        readonly List<Renderer> heldNow = new List<Renderer>();
        void OnBeginCam(ScriptableRenderContext ctx, Camera c)
        {
            if (c != cam) return;
            if (tickFrame != Time.frameCount && (gliding || viewOn)) Tick();                                 // no LateUpdate this frame yet (added during an Update): the glide's pose now
            if (fades.Count == 0) return;
            GiveBack();
            foreach (var kv in fades)
            {
                var f = kv.Value;
                if (f.shown < HideOthersAt || (f.own && viewOn && !gliding)) continue;
                for (int i = 0; i < f.other.Count; i++)
                {
                    var r = f.other[i];
                    if (!r || !r.enabled || r.forceRenderingOff) continue;                                    // held off by other code: its state, not ours
                    r.forceRenderingOff = true; heldNow.Add(r); OthersHeld++;
                }
                if (f.own) continue;                                                                       // close_hide_all_parts_r6: the viewed unit's own lights stay (its headlights light the way)
                for (int i = 0; i < f.lights.Count; i++)                                                     // close_hide_all_parts_r6: another unit's lights go with its parts (a bay lamp, a lamp's light)
                {
                    var l = f.lights[i];
                    if (!l || !l.enabled) continue;                                                          // off already: its state, not ours
                    l.enabled = false; lightsNow.Add(l);
                }
            }
            LightsHeld = lightsNow.Count;
        }
        void OnEndCam(ScriptableRenderContext ctx, Camera c) { if (c == cam) GiveBack(); }
        readonly List<Light> lightsNow = new List<Light>();                                                // close_hide_all_parts_r6: given back at the picture's end, exactly as they were
        void GiveBack()
        {
            for (int i = 0; i < heldNow.Count; i++) if (heldNow[i]) heldNow[i].forceRenderingOff = false; heldNow.Clear();
            for (int i = 0; i < lightsNow.Count; i++) if (lightsNow[i]) lightsNow[i].enabled = true; lightsNow.Clear();
        }

        // ------------------------------------------------------------------ (1) the weapon in range eases whiter
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor"), NightId = Shader.PropertyToID("_OZNight");
        static readonly MaterialPropertyBlock wpb = new MaterialPropertyBlock();
        sealed class Slot { public Renderer r; public int index; public Color baseEm; }                   // index: the material's own block
        readonly List<Slot> slots = new List<Slot>();
        readonly List<Material> copyMats = new List<Material>();
        Renderer builtFor; float wRamp, wLast = -1f, rangeAt, rawSince, lockAt = -10f; bool inRange, rawIn;

        void WhitenStep(float dt, OzFpsView fv)
        {
            if (!viewOn || !active || !WhitenOn || !fv || fv.Unit == null || fv.GunRenderers.Count == 0 || !fv.GunRenderers[0]) { if (builtFor || wLast > 0f) ResetWhiten(true); return; }
            if (builtFor != fv.GunRenderers[0]) BuildWhiten(fv);
            float now = Time.unscaledTime;
            if (now >= rangeAt) { rangeAt = now + RangeScan; bool r = TargetInRange(fv.Unit); if (r != rawIn) { rawIn = r; rawSince = now; } }
            if (inRange != rawIn && now - rawSince >= RangeHold)
            {
                inRange = rawIn;
                if (inRange) { WhitenRises++; if (now - lockAt >= LockBlipGap) { lockAt = now; LockBlips++; AudioDirector.UI("ui_target_lock", LockVol, 1f); } }   // the game's own "you can shoot it" blip
            }
            wRamp = Mathf.MoveTowards(wRamp, inRange ? 1f : 0f, dt / WhitenSeconds);
            float night = Mathf.Clamp01(Shader.GetGlobalFloat(NightId));
            float k = Mathf.SmoothStep(0f, 1f, wRamp) * Mathf.Lerp(WhitenDay, WhitenNight, night);
            WhitenNow = Mathf.SmoothStep(0f, 1f, wRamp);
            if (Mathf.Abs(k - wLast) < 0.0005f) return;
            wLast = k; Write(k);
        }

        /// <summary>An enemy the unit can attack inside its weapon's reach now (the rules' own test; one the fog of war covers does not count).</summary>
        static bool TargetInRange(ZObject u)
        {
            var gm = ZGame.I; var w = gm ? gm.World : null;
            if (w == null || u == null || !u.CanAttack()) return false;
            var l = w.Objects;
            for (int i = 0; i < l.Count; i++)
            {
                var o = l[i];
                if (o == null || o == u || o.Destroyed || o.KillAt >= 0 || o.Owner == Team.Neutral || o.Owner == u.Owner) continue;
                if (!o.IsUnit && o.Kind != ZKind.Building) continue;
                if (OzFogOfWar.Covers(o)) continue;
                if (u.CanAttackObject(o) && u.WithinAttackRadius(o)) return true;
            }
            return false;
        }

        /// <summary>The copy's emission slots and their own values (the copy's blocks are copies of the real gun's: team paint and all). A
        /// material that does not take an emission without its keyword (URP Lit with _EMISSION off) gets a copy of its own with the keyword on,
        /// for the gun copy only (dropped with it).</summary>
        void BuildWhiten(OzFpsView fv)
        {
            ResetWhiten(true);
            builtFor = fv.GunRenderers[0];
            foreach (var r in fv.GunRenderers)
            {
                if (!r) continue;
                var mats = r.sharedMaterials; bool swapped = false;
                for (int j = 0; j < mats.Length; j++)
                {
                    var m = mats[j]; if (!m || !m.HasProperty(EmissionId)) continue;
                    bool teamLit = m.shader && m.shader.name == "OpenZ/TeamLit";                             // TeamLit adds _EmissionColor always (no keyword)
                    if (!teamLit && !m.IsKeywordEnabled("_EMISSION"))
                    {
                        var c = new Material(m) { name = m.name + " (view copy)", hideFlags = HideFlags.DontSave };
                        c.EnableKeyword("_EMISSION"); mats[j] = c; copyMats.Add(c); swapped = true; CopyMaterials++;
                    }
                }
                if (swapped) r.sharedMaterials = mats;
                // review: one slot per material, always written as that material's own block (Write starts it from the renderer's block, so the
                // team paint stays). A single renderer-wide slot with material 0's emission put that colour on every other material of the part
                // (a glow or a dark trim of a two-material weapon lost its own emission for the rest of the view).
                for (int j = 0; j < mats.Length; j++)
                {
                    r.GetPropertyBlock(wpb, j);
                    Color b;
                    if (!wpb.isEmpty) b = wpb.HasColor(EmissionId) ? wpb.GetColor(EmissionId) : Em(mats[j]);
                    else { r.GetPropertyBlock(wpb); b = !wpb.isEmpty && wpb.HasColor(EmissionId) ? wpb.GetColor(EmissionId) : Em(mats[j]); }
                    slots.Add(new Slot { r = r, index = j, baseEm = b });
                }
            }
            wRamp = 0f; wLast = -1f; inRange = rawIn = false; rangeAt = 0f; rawSince = Time.unscaledTime; WhitenNow = 0f;
        }

        static Color Em(Material m) => m && m.HasProperty(EmissionId) ? m.GetColor(EmissionId) : Color.black;

        void Write(float k)
        {
            var add = new Color(k, k, k, 0f);
            for (int i = 0; i < slots.Count; i++)
            {
                var s = slots[i]; if (!s.r) continue;
                s.r.GetPropertyBlock(wpb, s.index);
                if (wpb.isEmpty) s.r.GetPropertyBlock(wpb);                                                // the first write of a material without its own block: start from the renderer's (team paint kept)
                wpb.SetColor(EmissionId, s.baseEm + add); s.r.SetPropertyBlock(wpb, s.index);
            }
        }

        /// <summary>The copy back to its own emission; drop: forget the slots and destroy this kit's material copies (the gun copy goes with the view).</summary>
        void ResetWhiten(bool drop)
        {
            if (wLast > 0f) Write(0f);
            wRamp = 0f; wLast = -1f; inRange = rawIn = false; WhitenNow = 0f;
            if (!drop) return;
            slots.Clear(); builtFor = null;
            foreach (var m in copyMats) if (m) Destroy(m);
            copyMats.Clear();
        }
    }
}
