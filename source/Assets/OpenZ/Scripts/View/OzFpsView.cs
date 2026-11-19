// OpenZ (GPL-3.0)
// View only: the rules (Scripts/Sim) are not touched, the unit is moved by the game's normal move orders.
using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace OpenZ
{
    /// <summary>
    /// Version 1.4 kit fps_view: the first-person view of one own unit. Hidden: in no controls table, help page, tooltip, text or key list.
    /// <list type="bullet">
    /// <item>The key: Alt+F (F = first person; Alt+Enter is the Windows player's fullscreen switch, ProjectSettings allowFullscreenSwitch). With exactly
    /// one own unit selected that can see (a robot, an own crewed vehicle or gun; not the shuttle) it goes in, else the quiet ui_disabled. Alt+F again or
    /// Esc leaves (that Esc never opens the pause menu).</item>
    /// <item>The view: the player's own camera at the unit's eye (a robot's head bone; a tank's or the mech's hatch, a jeep's seat, a gun's top), the
    /// unit's own model held off from drawing (Renderer.forceRenderingOff, given back exactly), field of view <see cref="FovDeg"/>, near clip
    /// <see cref="NearClip"/>; a copy of the unit's real gun mesh (its materials and team paint) in the lower right pointing where the player looks,
    /// with a small kick at each of the unit's own shots; a small crosshair; the HUD, the selection rings and the cursor hidden; a thin "ESC" for
    /// <see cref="HintSeconds"/> s. The RTS camera stays enabled (the fog of war's gate and its hidden units stay exactly as they are) but its Update
    /// does nothing while the view holds the camera (one hook line, <see cref="HoldsCamera"/>); its focus follows the unit (the listener, the weather and
    /// the scenery that read it stay with the player's eye).</item>
    /// <item>Mouse look: the free look's own rate (RtsCamera.FreeLookSpeed, deg per pixel), yaw free, pitch <see cref="PitchMin"/> .. <see cref="PitchMax"/>,
    /// the cursor locked and hidden.</item>
    /// <item>Moving: the bound scroll keys (W A S D) and the arrows: forward / back / strafe relative to the look, only through the game's normal move
    /// order (PlayerInput's right-click waypoint, SEND_WAYPOINTS): a point <see cref="StepPx"/> px (3 tiles) ahead in the wanted direction, sent again
    /// only when a key goes down or up, the wanted way turned more than <see cref="TurnDeg"/> deg, or the unit is within <see cref="NearPx"/> px (a tile)
    /// of the point or its walk ended (fix round 3: each player move drops the unit's target, DV16, so a straight walk sends one order per 2 tiles), a
    /// stop order on release. A gun does not walk. The unit's own auto-fire stays as it is; no new shooting.</item>
    /// <item>MAYHEM fog of war: the fog's cloud sheets are drawn only for a camera above them, so for this eye on the ground the fog's shader gets
    /// <see cref="IdFowUnder"/> (_OzFowUnder, a global, zero while the view is off): the ground not explored yet carries a wall of cloud up to
    /// <see cref="FogWallTop"/> m over the cloud's floor, which covers that ground, every building on it and the sky low over it (the units there
    /// stay held off by the fog as always).</item>
    /// <item>Leaving: Esc, Alt+F, the unit dies, gets in (leaves the map) or its crew gets out, the level ends, the pause menu opens, a scene takes the
    /// camera: back to the RTS camera centred on the unit (or where it was last), the old zoom and tilt, the field of view and near clip, the cursor,
    /// the HUD and the rings back, the keys back to their RTS meaning. Fix round 3: when the level ends (or another script took the camera's follow
    /// while the view was on) the RTS camera's follow, focus and zoom stay as the ending set them (OpenZEnding glides to the fort and orbits it).</item>
    /// </list>
    /// Hook lines in game files (each reads one member, false while the view is off): View/RtsCamera.cs Update (<see cref="HoldsCamera"/>),
    /// View/PlayerInput.cs Update (PlayerInput.FpsViewStep in View/PlayerInput.FpsView.cs), View/OzBindings.cs Control (<see cref="On"/>: every bound
    /// action silent), UI/OpenZPauseMenu.cs Update (<see cref="EscTaken"/>); fix round 2: the "only sounds in view" gates of View/CombatAudio.cs,
    /// View/AudioDirector.cs and View/MechFoley.cs (<see cref="Own"/>, <see cref="On"/>: the unit's own shots, steps and tracks are heard). Fix round 2
    /// also holds off the vehicle's crew figure, which BoardingView keeps outside the unit's view (View/BoardingView.FpsView.cs: CrewFigure, CrewBox),
    /// closes the CONTROLS HELP card at the entry, and locks the cursor only while the window has the focus (alt-tab). Fix round 3: the level's end
    /// leaves the RTS camera to the ending, a straight walk sends one move order per 2 tiles (the unit keeps firing), and other units' views (a
    /// robot's own squad mates) with a part by the eye are held off as the crew figures are.
    /// fps_view_fix (7 Nov 2026): (c) the model, the crew figure and the units by the eye are held off ONLY while the player's camera draws
    /// (RenderPipelineManager begin / end of that camera, as the fog of war holds off its covered units): a renderer drawn at the start of a
    /// picture is held for that picture and given back at its end, one already held off by other code is left alone, so between pictures every
    /// renderer is exactly as the game set it and the leave has nothing to undo (the gatling gunner's back part the BS01 type kit hides while he
    /// sits, the kit cables, anything another script switches meanwhile); (b) a vehicle's or a gun's copy is its barrel only (the thin parts
    /// along the barrel's line: no mantlet, receiver, shield or housing block), centred at the bottom of the view and reaching forward like a
    /// tank sight, sized by its thickness; (d) the moves are Alt + right-click moves (View/PlayerInput.FpsView.cs: an attack order never stops
    /// the drive). The MAYHEM wall (a) is the fog shader's (Resources/OpenZ/FX/OzFogOfWar.shader).
    /// The eye a little higher for every eye kind (<see cref="EyeLiftRobot"/> m over a robot's eyes,
    /// <see cref="EyeLiftVehicle"/> m over a hatch, a seat or a top; the units by the eye are judged from the raised eye as before); a robot's
    /// gun copy sits lower (its grip <see cref="GripRobot"/>: the copy is placed in camera space, so the raised eye alone does not move it on
    /// the screen) and a chunky one is made smaller until its parts' boxes cover at most <see cref="RobotCoverMax"/> of the screen (an upper
    /// bound of its pixels; at most down to <see cref="RobotFitMin"/> of its size); the vehicle and gun barrels (3 - 6 % of the screen) as they
    /// were. The sky dome, the sun disc and its bloom while the view is on: View/FlybySky.FpsView.cs reads <see cref="On"/> and <see cref="Cam"/>.
    /// Mark: fps_view_sky (7 Nov 2026): the eye a little higher for every eye kind, the robot copy lower and fitted, the sky in the view.
    /// </summary>
    [DefaultExecutionOrder(31900)]                                                                       // LateUpdate after the game's scripts, before the 32000 readers of the camera's pose (OzShuttleCamFade, OzSoftCursor): this frame's pose is the eye's
    public sealed class OzFpsView : MonoBehaviour
    {
        // ------------------------------------------------------------------ the hooks' reads
        static bool on;
        /// <summary>The view is on (View/OzBindings.cs: every bound action reads as not pressed).</summary>
        public static bool On => (on && I) || OzFpsFeel.GlideOutNow;                                   // 1.5 fps_feel: also while the camera glides out (the RTS camera, the bound keys, Esc and the clicks wait those 0.75 s; View/OzFpsFeel.cs)
        /// <summary>View/RtsCamera.cs Update: the RTS camera leaves the camera's pose, the cursor and its keys alone.</summary>
        public static bool HoldsCamera => On;
        /// <summary>UI/OpenZPauseMenu.cs: Esc belongs to the view (while it is on, and on the frame an Esc left it).</summary>
        public static bool EscTaken => On || EscFrame == Time.frameCount;
        public static OzFpsView I { get; private set; }
        /// <summary>The frames the view was entered, left, left by Esc (-1 = never).</summary>
        public static int EnteredFrame = -1, LeftFrame = -1, EscFrame = -1;
        /// <summary>Fix round 2: a sound made by the viewed unit itself (its shots, steps, tracks, wheels, servos: within <see cref="OwnRadius"/> m of
        /// its root) counts as in view for the game's "only sounds in view" gates (View/CombatAudio.cs PlayPitched, View/AudioDirector.cs Play, OnScreen
        /// and CentreDist2, View/MechFoley.cs OnScreen): the eye is that unit, its own muzzle and feet sit under or behind the camera. False always while
        /// the view is off (one static read), so every other sound goes exactly the way it went.</summary>
        public static bool Own(Vector3 p) => on && I && I.view && (p - I.view.transform.position).sqrMagnitude < OwnRadius * OwnRadius;
        /// <summary>How far from the viewed unit's root its own sounds are taken as its own (m): the heavy tank's muzzle is about 5 m out.</summary>
        public const float OwnRadius = 6f;
        /// <summary>fps_view_sky (2b): the view holds this unit view now. View/VehicleLights.cs does not draw its lamp glows and haze cones while the
        /// eye sits in them (the halo billboards are pulled toward the camera, so from the hatch one filled the screen white), View/VeteranMarks.cs
        /// holds its promotion pop while the view is on. False always with the view off.</summary>
        public static bool Holds(UnitView v) => OzFpsFeel.LampHold(v, on && I && v && I.view == v);   // 1.5 fps_feel: during a glide its lamp glows are held only near the eye (View/OzFpsFeel.cs)

        // ------------------------------------------------------------------ fps_view_sky (2c): left click fires, like a first-person game
        /// <summary>Left click (held: again every <see cref="FireRetarget"/> s when the centre points at another target): the target under the view's
        /// centre (PlayerInput.FpsTargetAtCentre: the game's own hover pick at the screen centre, an enemy unit, gun or building within
        /// <see cref="FireReach"/> m in front) gets the game's own attack order (PlayerInput.FpsAttack: BuildWaypoint exactly as an RTS right-click
        /// on it, SEND_WAYPOINTS; no new rules), and the unit fires at its own rate while that order holds. Ground, sky, an own or a neutral
        /// object: no order (the game has no attack-ground order). While W A S D drive, the drive wins (fps_view_fix (d)): the click sends no
        /// attack order (it would stop the unit) and the unit keeps firing on the move at what is in its reach. A click never selects
        /// (PlayerInput's Update waits while the view is on).</summary>
        public const float FireRetarget = 0.25f, FireReach = 160f;
        /// <summary>Play check counters: left presses, attack orders sent (of them while held), presses with nothing to fire at, presses while driving.</summary>
        public static int FireClicks, FireOrders, FireHeldOrders, FireNone, FireDriving;
        public static string LastFire = "";
        float fireSentAt = -10f; int fireSentId = -1;

        // ------------------------------------------------------------------ the numbers
        public const float FovDeg = 60f, NearClip = 0.05f, PitchMin = -60f, PitchMax = 60f, TurnReissue = 0.08f, HintSeconds = 2f, EyeTau = 0.05f;
        /// <summary>Fix round 3: how far ahead the move order points (Zod px, 16 = one tile): 3 tiles; the next order goes when the unit is within
        /// <see cref="NearPx"/> px of it (a tile), so a straight walk sends one order per 2 tiles (each player move drops the unit's target, DV16).</summary>
        public const int StepPx = 48, NearPx = 16;
        /// <summary>Fix round 3: a turn of the wanted way by more than this (deg) sends a new order at once (at most every <see cref="TurnReissue"/> s).</summary>
        public const float TurnDeg = 20f;
        /// <summary>A robot's gun copy: its length on screen (m in front of the eye), where its grip sits (camera space); the distance the copy's line
        /// meets the view's centre. fps_view_fix (b): a vehicle's or a gun's copy is its barrel at the lower middle (SightCross, SightNear below).</summary>
        public const float GunLenRobot = 0.62f, GunConverge = 12f;
        /// <summary>fps_view_sky: the robot copy's grip in camera space, 4 cm lower and 1 cm further out than fps_view_fix's (0.16, -0.17, 0.26):
        /// the copy's near end goes about 6 deg lower on the screen (its muzzle 2 deg), where the hands are seen from the raised eye.</summary>
        public static readonly Vector3 GripRobot = new Vector3(0.16f, -0.21f, 0.27f);
        /// <summary>fps_view_sky: how much higher the eye is than fps_view_fix's (m, world): a robot's over its eyes (up to the brow, not over the
        /// head), a vehicle's or a gun's over its hatch, seat or top.</summary>
        public const float EyeLiftRobot = 0.20f, EyeLiftVehicle = 0.30f;
        /// <summary>fps_view_sky: a robot's copy is made smaller (about its grip) until its parts' boxes cover at most this share of the screen
        /// (the boxes hold the mesh: its pixels are fewer), never under RobotFitMin of the size GunLenRobot gives it; a grid of FitCols x FitRows
        /// rays from the eye (the view's own field of view and the screen's shape) against each part's box.</summary>
        public const float RobotCoverMax = 0.14f, RobotFitMin = 0.6f;
        const int FitCols = 96, FitRows = 54;
        /// <summary>fps_view_sky: the robot copy's size against GunLenRobot alone (1 = as fps_view_fix had it), its boxes' share of the screen
        /// before and after the fit (vehicles and guns: 1, and their boxes are not measured: -1).</summary>
        public float GunFit { get; private set; } = 1f;
        public float GunCoverBox0 { get; private set; } = -1f;
        public float GunCoverBox { get; private set; } = -1f;
        /// <summary>fps_view_sky: the eye before the lift (fps_view_fix's point, this frame) and the lift of this unit (m).</summary>
        public Vector3 EyeBase { get; private set; }
        public float EyeLift => unit != null && unit.Kind == ZKind.Robot ? EyeLiftRobot : EyeLiftVehicle;
        /// <summary>fps_view_sky: the player's camera the view holds (FlybySky draws the sky dome for it while the view is on).</summary>
        public Camera Cam => cam;

        /// <summary>Play check only: stands in for the level's end (a leave path that cannot be played without ending the match).</summary>
        public static bool CheckEndsMatch;
        /// <summary>Counters for the Play check (since the Play started).</summary>
        public static int Enters, Leaves, Refusals, MoveOrders, StopOrders, MoveRefused, GuiFrames, HideFights, FiguresTaken, FocusRegained, HelpClosed, UnitsTaken;
        public static string LastLeave = "", LastRefusal = "";
        /// <summary>Fix round 3: the last leave left the RTS camera's follow, focus and zoom to the level's ending (or to the script that took the follow).</summary>
        public static bool LastLeaveKeptCamera;

        /// <summary>fps_view_fix (c), Play check only: called inside every picture of the view's camera, right after the view held its renderers
        /// off (what is drawn now can be read there; outside the picture nothing is held).</summary>
        public static System.Action<Camera> AtRender;
        /// <summary>fps_view_fix fix round 1, Play check only (the hold read by pixels): while true the view holds nothing in its camera's
        /// pictures (the model is drawn), so the check can grab the same still picture with and without the hold. False always in the game.</summary>
        public static bool CheckDrawOwn;
        /// <summary>fps_view_fix (c): pictures the view held renderers off for; renderers it holds off right now (0 between pictures).</summary>
        public static int RenderHolds;
        public static int HeldNow => I ? I.heldNow.Count : 0;
        /// <summary>fps_view_fix (c), at the last leave: the renderers the view held off in its last picture (given back at its end) and the ones it
        /// found held off by other code then (left alone: the game's own state).</summary>
        public static readonly HashSet<Renderer> LastHeldSet = new HashSet<Renderer>(), LastForeignOff = new HashSet<Renderer>();
        /// <summary>fps_view_fix (check fault 'focus on the unit'): the RTS focus the view set in its last LateUpdate, and that frame.</summary>
        public static Vector3 LastFocusSet; public static int LastFocusFrame = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            AtRender = null; CheckDrawOwn = false; RenderHolds = 0; LastHeldSet.Clear(); LastForeignOff.Clear(); LastFocusFrame = -1;
            on = false; I = null; CheckEndsMatch = false; EnteredFrame = LeftFrame = EscFrame = -1; LastLeaveKeptCamera = false;
            Enters = Leaves = Refusals = MoveOrders = StopOrders = MoveRefused = GuiFrames = HideFights = FiguresTaken = FocusRegained = HelpClosed = UnitsTaken = 0; LastLeave = LastRefusal = "";
            FireClicks = FireOrders = FireHeldOrders = FireNone = FireDriving = 0; LastFire = "";                 // fps_view_sky (2c)
        }

        // ------------------------------------------------------------------ one session
        PlayerInput pi; ZGame g; RtsCamera rts; Camera cam; OpenZHud hud; SelectionOverlay sel; AttackRangeRings rings;
        ZObject unit; UnitView view;
        float fov0, near0; bool hudWas, hudPending, selWas, ringsWas;
        readonly List<Renderer> hidden = new List<Renderer>(); readonly HashSet<Renderer> hiddenSet = new HashSet<Renderer>(); float rescanAt;
        static readonly List<Renderer> scan = new List<Renderer>();
        // fps_view_fix (c): held off inside the camera's picture only
        readonly List<Renderer> heldNow = new List<Renderer>(); readonly HashSet<Renderer> heldSet = new HashSet<Renderer>();
        readonly HashSet<Renderer> foreignOff = new HashSet<Renderer>(); readonly List<Renderer> lastHeld = new List<Renderer>();
        static readonly List<Renderer> rscan = new List<Renderer>();
        bool hooked; int heldModel, lastGivenBack;
        float yaw, pitch, enterAt, shake; Vector3 eye, lastPos; bool eyeInit;
        float lagYaw, lagPitch;   // 1.5 fps_look_smooth: how far the drawn look trails the mouse's look (deg, unwrapped; eased to exactly 0, View/OzFpsLook.cs)
        enum EyeKind { Head, Hatch, Seat, Top }
        EyeKind eyeKind; Transform eyeT; float topUp;
        GameObject gun; Transform gunPivot; Quaternion gunRot; Vector3 gunGrip, gunDir, gunAxis, gripLocal; float lastFire, kick, gunScale; int gunParts;
        bool held; Vector2 moveDir, sentKeys; float sentAt = -10f, heardAt = -10f; Vector2Int sentTo;
        Transform followAtEntry;                                                                             // fix round 3: the RTS camera's follow when the view went in
        bool unfocused; int lookSkipFrame = -1;                                                              // fix round 2: alt-tab
        static Texture2D white; GUIStyle hintStyle;

        public ZObject Unit => unit;
        public UnitView View => view;
        public float Yaw => yaw;
        public float Pitch => pitch;
        /// <summary>1.5 fps_look_smooth: the look as drawn (the camera, the crosshair, the fire pick, the walk): Yaw / Pitch less the eased lag.</summary>
        public float LookYaw => Mathf.Repeat(yaw - lagYaw + 180f, 360f) - 180f;
        public float LookPitch => pitch - lagPitch;
        /// <summary>Renderers of the unit held off in the last picture (fps_view_fix (c): only while the camera draws); renderers of the gun copy;
        /// what the gun search found ("" = a gun).</summary>
        public int HiddenCount => heldModel;
        /// <summary>The range rings component held off by the view (null = none on the input).</summary>
        public AttackRangeRings Rings => rings;
        public int GunParts => gun ? gunParts : 0;
        public string GunWhy { get; private set; } = "";
        public string GunRootName { get; private set; } = "";
        /// <summary>The gun copy's barrel line in the world, read off the copy itself (zero without a gun).</summary>
        public Vector3 GunAxisWorld => gun && gunPivot ? gunPivot.TransformDirection(gunAxis).normalized : Vector3.zero;
        public string EyeName => (eyeT ? eyeT.name : "-") + " (" + eyeKind + ")";
        /// <summary>The renderers given back at the last leave, and how many of them were still held off right after (must be 0).</summary>
        public static int LastRestored, LastStillOff;
        public static Vector3 LastLeavePos;

        // ------------------------------------------------------------------ the fog of war under its sheets (fix round 1)
        static int idFowUnder = -1;
        /// <summary>Resources/OpenZ/FX/OzFogOfWar.shader's _OzFowUnder (a global): x 1 = the camera stands under the cloud sheets (this view), y the
        /// cloud wall's top over the cloud's floor (m), z the sky's distance (m). Zero whenever the view is off: every other picture is drawn as before.</summary>
        public static int IdFowUnder => idFowUnder >= 0 ? idFowUnder : (idFowUnder = Shader.PropertyToID("_OzFowUnder"));
        /// <summary>The cloud wall's top over the cloud's floor (m): over the crowns of the highest sheet and their relief (CLOUD BANKS 21 + 11 m),
        /// over every building; the sky's distance at most <see cref="FogSkyMax"/> m (the camera's far clip below that).</summary>
        public const float FogWallTop = 40f, FogSkyMax = 1500f;
        static void FogUnder(Camera c) => Shader.SetGlobalVector(IdFowUnder, c ? new Vector4(1f, FogWallTop, Mathf.Min(c.farClipPlane, FogSkyMax), 0f) : Vector4.zero);
        /// <summary>The value the fog reads now (the Play check).</summary>
        public static Vector4 FogUnderNow => Shader.GetGlobalVector(IdFowUnder);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void FogReset() => FogUnder(null);                                                           // a Play never starts with the wall on (the global outlives a Play in the editor)

        // ------------------------------------------------------------------ the key
        /// <summary>Alt+F went down this frame (left or right Alt; AltGr = Ctrl + right Alt counts too).</summary>
        public static bool Combo(Keyboard kb) => kb != null && kb.fKey.wasPressedThisFrame && kb.altKey.isPressed;

        /// <summary>PlayerInput.FpsViewStep: the combo with the view off. In with exactly one own unit that can see, else the quiet refusal sound.</summary>
        public static bool TryEnter(PlayerInput p)
        {
            string why = Refuse(p, out var u, out var v);
            if (why != null)
            {
                Refusals++; LastRefusal = why;
                AudioDirector.UI("ui_disabled");
                Debug.Log("[OpenZ FPS] Alt+F: not entered (" + why + ")");
                return false;
            }
            var c = p.cam.GetComponent<OzFpsView>(); if (!c) c = p.cam.gameObject.AddComponent<OzFpsView>();
            c.enabled = true;
            c.Begin(p, u, v);
            return true;
        }

        static string Refuse(PlayerInput p, out ZObject u, out UnitView v)
        {
            u = null; v = null;
            var gm = p ? p.game : null; var w = gm ? gm.World : null;
            if (w == null) return "no level";
            if (!p.cam || !p.cam.isActiveAndEnabled || !p.cam.GetComponent<Camera>()) return "no RTS camera";
            string busy = Busy(w); if (busy != null) return busy;
            if (p.Placing != null) return "a gun is being placed";                                           // fix round 1: its ghost would stay frozen in front of the eye
            var hh = OpenZHud.I; if (hh && hh.Rally && hh.Rally.Armed) return "the rally tool is armed";
            if (p.Selection.Count != 1) return p.Selection.Count == 0 ? "nothing selected" : p.Selection.Count + " units selected (one only)";
            u = p.Selection[0];
            if (u == null || !OzFpsUe.Mine(u, gm.player)) return "not an own unit";   // 1.5 fps_in_ue: either team in the Unit editor (the player orders both there; View/OzFpsUe.cs), the player's own in a level as before
            if (!Sees(u)) return (u.Type ?? "?") + " cannot be looked out of (a robot, an own crewed vehicle or gun only)";
            if (u.Destroyed || u.KillAt >= 0 || w.Get(u.Id) != u) return "the unit is not on the map";
            if (!gm.Views.TryGetValue(u.Id, out v) || !v || !v.gameObject.activeInHierarchy || v.Dying) return "the unit has no view";
            return null;
        }

        /// <summary>A unit that can see: a robot, or an own vehicle or gun with its crew (never the shuttle, never a fort's roof defender).</summary>
        static bool Sees(ZObject u) => !u.IsFortDefender && (u.Kind == ZKind.Robot || ((u.Kind == ZKind.Vehicle || u.Kind == ZKind.Cannon) && !u.IsShuttle && u.Drivers.Count > 0)) || OzFpsShuttleView.Sees(u);   // 1.5 fps_shuttle_badges: an own crewed shuttle too (View/OzFpsShuttleView.cs)

        /// <summary>A moment the view never runs in (null = none).</summary>
        static string Busy(ZWorld w)
        {
            if (w.GameOver || PlayerInput.MatchDecided(w) || OpenZEnding.LocksCamera || CheckEndsMatch) return "the level ends";
            var h = OpenZHud.I;
            if (h && h.Pause && h.Pause.IsOpen) return "the pause menu is open";
            if (h && h.Modal) return "a menu owns the screen";
            string ue = OzFpsUe.Busy(); if (ue != null) return ue;   // 1.5 fps_in_ue: the Unit editor is no longer refused whole (the cause of "Alt+F does nothing there"); only its placing, typing and dialogs
            if (OzConfirm.Blocks) return "a question is open";                                               // fix round 1: its Esc is the question's
            if (ZodBriefing.BriefingActive || OzBlueCameo.BriefingActive || CaptureKit.CineActive || (ZodFlyover.Live != null && ZodFlyover.Live.Active)) return "a scene has the camera";
            return null;
        }

        // ------------------------------------------------------------------ in
        void Begin(PlayerInput p, ZObject u, UnitView v)
        {
            pi = p; g = p.game; rts = p.cam; cam = rts.GetComponent<Camera>(); hud = OpenZHud.I; unit = u; view = v;
            fov0 = cam.fieldOfView; near0 = cam.nearClipPlane;
            OzFpsFeel.Entering(this, cam, rts, ref fov0, ref near0);                                       // 1.5 fps_feel: the glide in starts from the RTS camera's pose (or turns a glide out round; then fov0 / near0 are the RTS camera's own)
            hudWas = hud && hud.hudVisible; hudPending = false;
            if (hudWas) hud.SetVisible(false);
            if (hud && hud.Help && hud.Help.IsOpen) { hud.Help.Toggle(false); HelpClosed++; }               // fix round 2: the CONTROLS HELP card is its own canvas (not hidden with the HUD): closed, with its own close sound
            sel = p.GetComponent<SelectionOverlay>(); selWas = sel && sel.enabled; if (sel) sel.enabled = false;
            rings = p.GetComponent<AttackRangeRings>(); if (!rings) rings = AttackRangeRings.I;                // fix round 1: the range rings too (drawn every LateUpdate from the selection)
            ringsWas = rings && rings.enabled; if (rings) rings.enabled = false;
            rts.UeStopFly();                                                                                 // fix round 1: a fly-to under way ends here (it would go on after the leave)
            followAtEntry = rts.follow;                                                                      // fix round 3: a follow set by another script while the view is on (the ending's) is kept at the leave
            hidden.Clear(); hiddenSet.Clear(); figs.Clear(); unitFigs.Clear(); HideModel(); rescanAt = Time.unscaledTime + 0.5f;
            heldNow.Clear(); heldSet.Clear(); foreignOff.Clear(); lastHeld.Clear(); heldModel = 0; lastGivenBack = 0; Hook(true);   // fps_view_fix (c): held off inside the camera's pictures only
            unfocused = false; lookSkipFrame = -1;
            FindEye();
            BuildGun(out Vector3 barrelWorld);
            Vector3 f = barrelWorld; f.y = 0f;
            if (u.Kind == ZKind.Robot || f.sqrMagnitude < 1e-4f) { f = -v.transform.forward; f.y = 0f; }      // every unit model faces its root's -z
            yaw = f.sqrMagnitude > 1e-6f ? Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg : 0f; pitch = -4f;
            lagYaw = lagPitch = 0f;   // 1.5 fps_look_smooth: the view goes in with no lag (drawn = the look)
            if (OzFpsShuttleView.Flies(u)) yaw = OzFpsShuttleView.NoseYaw(u, v, yaw);                         // 1.5 fps_shuttle_badges: the shuttle's first look is along its nose (its model faces its root's +z)
            yaw = OzFpsFeel.ApproachYaw(cam, v, u, yaw);                                                     // 1.5 N15: a glide in looks along the way in (View/OzFpsFeel.AimIn.cs)
            eyeInit = false; enterAt = Time.unscaledTime; held = false; moveDir = sentKeys = Vector2.zero; sentAt = -10f;
            shake = 0f; rts.TakeShake(); lastFire = v.LastFireAt; kick = 0f; lastPos = v.transform.position;
            on = true; I = this; Enters++; EnteredFrame = Time.frameCount;
            cam.fieldOfView = FovDeg; cam.nearClipPlane = NearClip;
            FogUnder(cam);
            OzFpsGlow.Entered(cam);                                                                          // fps_glow: Stop NaN on from here to the level's end (View/OzFpsGlow.cs)
            if (Application.isFocused) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; } else unfocused = true;   // fix round 2: never lock a window without the focus
            AudioDirector.UI("ui_confirm");
            PlaceCamera(true);
            Debug.Log($"[OpenZ FPS] in: {u.Type} #{u.Id}, eye {EyeName} raised {EyeLift:0.00} m, gun {(gun ? GunRootName + " (" + gunParts + " parts)" + (GunCoverBox0 >= 0f ? $", fitted {GunFit:0.00} (its boxes {GunCoverBox0 * 100f:0.0} -> {GunCoverBox * 100f:0.0} % of the screen)" : "") : "none: " + GunWhy)}, {hidden.Count} model renderers held off while the camera draws");
        }

        // ------------------------------------------------------------------ out
        /// <summary>Back to the RTS camera (every leave path ends here).</summary>
        public void Leave(string why, bool esc = false, bool quiet = false)
        {
            if (!on || I != this) return;
            on = false; LeftFrame = Time.frameCount; if (esc) EscFrame = Time.frameCount; Leaves++; LastLeave = why;
            bool alive = unit != null && !unit.Destroyed && unit.KillAt < 0 && g && g.World != null && g.World.Get(unit.Id) == unit && OzFpsUe.Mine(unit, g.player);   // 1.5 fps_in_ue: a blue unit's walk stops too
            if (held && alive && pi && pi.FpsStop(unit)) StopOrders++;                                        // a walk in progress ends with the view
            held = false;
            ShowModel(); DropGun();
            if (view) lastPos = view.transform.position;
            LastLeavePos = lastPos;
            if (cam) { cam.fieldOfView = fov0; cam.nearClipPlane = near0; }
            FogUnder(null);
            // fix round 3: the level ends (the fort fell: OpenZEnding.Begin took the camera earlier this frame, follow = its target, ZoomTo the fort)
            // or another script set the camera's follow while the view was on: follow, focus and zoom stay theirs (the glide and the orbit go on; the
            // ending took its start from the focus the view kept on the unit). Else back on the unit, the follow cleared.
            bool keepCam = EndingOwnsCamera(); LastLeaveKeptCamera = keepCam;
            OzFpsFeel.Leaving(why, quiet);                                                                 // 1.5 fps_feel: a leave by the player or the unit glides out; the game's own leaves (and one whose camera the ending keeps) stay instant
            if (rts)
            {
                rts.UeStopFly();
                if (!keepCam) { rts.focus = new Vector3(lastPos.x, 0f, lastPos.z); rts.follow = null; }
                rts.Place();                                                                                 // the old height and tilt were never touched; no fly-to goes on
            }
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            if (sel) sel.enabled = selWas;
            if (rings) rings.enabled = ringsWas;
            if (hud && hudWas)
            {
                if (hud.Pause && hud.Pause.IsOpen) hudPending = true;                                         // the pause menu holds the HUD hidden: it comes back when the menu closes
                else if (!hud.hudVisible && !(hud.EndScreen && hud.EndScreen.IsShown)) hud.SetVisible(true);
            }
            OzFpsGlow.Left(cam);                                                                             // fps_glow: the lights re-picked for the RTS focus now, Stop NaN kept on, oversized stars held
            if (!quiet) AudioDirector.UI("ui_cancel");                                                       // quiet: the camera is being switched off (a scene unloads): no sound, nothing made
            Debug.Log($"[OpenZ FPS] out: {why}; the RTS camera on ({lastPos.x:0.0}, {lastPos.z:0.0}), {LastRestored} renderers given back, {LastStillOff} still off");
        }

        /// <summary>Fix round 3: the RTS camera belongs to the level's ending (the match decided: OpenZEnding runs or the sim's game over; the Play
        /// check's stand-in) or to the script that changed its follow while the view was on: the leave leaves follow, focus and zoom alone.</summary>
        bool EndingOwnsCamera()
        {
            var w = g ? g.World : null;
            if (OpenZEnding.Running || OpenZEnding.LocksCamera || CheckEndsMatch || (w != null && (w.GameOver || PlayerInput.MatchDecided(w)))) return true;
            return rts && rts.follow != followAtEntry;
        }

        void OnDisable() { if (on && I == this) Leave("the camera was switched off", false, true); }

        void OnDestroy()
        {
            GiveBack(); Hook(false);                                                                         // fps_view_fix (c): never a renderer left held, never a callback left
            OzFpsBadgeHold.Release();                                                                        // 1.5 fps_shuttle_badges: and here
            if (I != this) return;
            if (on) { on = false; LeftFrame = Time.frameCount; ShowModel(); FogUnder(null); Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
            I = null;
        }

        string LeaveWhy()
        {
            var w = g ? g.World : null;
            if (w == null || !pi || !rts || !cam) return "the level went";
            if (unit == null || unit.Destroyed) return "the unit died";
            if (unit.KillAt >= 0 || w.Get(unit.Id) != unit) return "the unit got in (it left the map)";
            if (!OzFpsUe.Mine(unit, g.player) || (unit.Kind != ZKind.Robot && unit.Drivers.Count == 0)) return "the crew got out";   // 1.5 fps_in_ue: either team in the Unit editor
            if (!view || !view.gameObject.activeInHierarchy || view.Dying) return "the unit died";
            if (!pi.Selection.Contains(unit)) return "the selection changed";
            string busy = Busy(w); if (busy != null) return busy;
            if (!rts.isActiveAndEnabled || !cam.isActiveAndEnabled) return "another camera took over";
            return null;
        }

        // ------------------------------------------------------------------ every frame
        void Update()
        {
            if (!on || I != this)
            {
                OzFpsBadgeHold.ReleaseWhenOut();                                                            // 1.5 fps_shuttle_badges: once the view is over, the blocks it made are taken off again
                if (hudPending && hud && !(hud.Pause && hud.Pause.IsOpen))
                {
                    hudPending = false;
                    if (!hud.hudVisible && !(hud.EndScreen && hud.EndScreen.IsShown)) hud.SetVisible(true);
                }
                return;
            }
            string why = LeaveWhy(); if (why != null) { Leave(why); return; }
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) { Leave("Esc", true); return; }
            if (Combo(kb) && EnteredFrame != Time.frameCount) { Leave("Alt+F again"); return; }
            // fix round 2 (alt-tab; ProjectSettings runInBackground: 1, so this runs without the focus too): the cursor is locked and hidden only while
            // the window has the focus (as RtsCamera.UpdateConfine checks Application.isFocused); out of focus it is let go at once, so the desktop
            // mouse is never clipped or re-centred; on the first focused frame it is locked again and that frame's mouse travel (the jump back into
            // the window) is not taken as a look
            if (!Application.isFocused)
            {
                if (!unfocused) { unfocused = true; Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
            }
            else
            {
                if (unfocused) { unfocused = false; lookSkipFrame = Time.frameCount; FocusRegained++; }
                if (Cursor.lockState != CursorLockMode.Locked) Cursor.lockState = CursorLockMode.Locked;
                if (Cursor.visible) Cursor.visible = false;
                var m = Mouse.current;
                if (m != null && Time.frameCount - EnteredFrame > 1 && Time.frameCount != lookSkipFrame) MouseLook(m.delta.ReadValue());   // not the frame the cursor was locked (its jump to the centre)   // 1.5 fps_look_smooth: the mouse's look, drawn eased
            }
            if (OzFpsFortButton.Holds(unit)) { held = false; moveDir = sentKeys = Vector2.zero; lastPos = view.transform.position; return; }
            Walk(kb);
            if (Application.isFocused && Time.frameCount - EnteredFrame > 1) Fire(Mouse.current);         // fps_view_sky (2c): left click fires
            lastPos = view.transform.position;
        }

        /// <summary>fps_view_sky (2c): the left button (see <see cref="FireRetarget"/>).</summary>
        void Fire(Mouse m)
        {
            if (m == null || unit == null || !pi || !cam) return;
            if (OzFpsShuttleView.Flies(unit)) return;                                                      // 1.5 fps_shuttle_badges: the shuttle has no weapon (a click means nothing to it)
            bool down = m.leftButton.wasPressedThisFrame;
            if (!m.leftButton.isPressed) { fireSentId = -1; return; }
            if (down) FireClicks++;
            if (held) { if (down) { FireDriving++; LastFire = "driving: the drive wins, the unit fires on the move at what is in its reach"; } return; }
            float now = Time.unscaledTime;
            if (!down && now - fireSentAt < FireRetarget) return;
            var t = pi.FpsTargetAtCentre(cam, unit, FireReach);
            if (t == null) { if (down) { FireNone++; LastFire = "nothing to fire at under the centre (ground, sky, own or neutral): no order"; } return; }
            if (t.Id == fireSentId && Attacking(t)) return;                                                  // the order on it holds: the unit fires at its own rate
            if (!pi.FpsAttack(unit, t)) { if (down) LastFire = t.Type + " #" + t.Id + ": the game gives this unit no attack order on it"; return; }
            FireOrders++; if (!down) FireHeldOrders++;
            fireSentId = t.Id; fireSentAt = now; LastFire = "attack order on " + t.Type + " #" + t.Id;
        }

        /// <summary>fps_view_sky (2c): the unit's waypoints hold an attack on t.</summary>
        bool Attacking(ZObject t)
        {
            var l = unit.Waypoints;
            for (int i = 0; i < l.Count; i++) if (l[i].mode == WpMode.Attack && l[i].refId == t.Id) return true;
            return false;
        }

        /// <summary>Mouse look by a travel in pixels: right = turn right, up = look up, at the free look's rate (RtsCamera.FreeLookSpeed).</summary>
        public void LookBy(Vector2 px)
        {
            float k = RtsCamera.FreeLookSpeed;
            yaw = Mathf.Repeat(yaw + px.x * k + 180f, 360f) - 180f;
            pitch = Mathf.Clamp(pitch + px.y * k, PitchMin, PitchMax);
            lagYaw = lagPitch = 0f;   // 1.5 fps_look_smooth: a look set by code (the Play checks, the glide's turn round) is drawn at once, as before
        }

        /// <summary>1.5 fps_look_smooth: the mouse's look. The look (yaw, pitch: the aim's target) moves at once as LookBy moves it; the drawn
        /// look trails it by the lag, which PlaceCamera eases off (View/OzFpsLook.cs: exp(-dt / 40 ms), no overshoot, ends exactly on the look).</summary>
        void MouseLook(Vector2 px)
        {
            float k = RtsCamera.FreeLookSpeed, p0 = pitch;
            yaw = Mathf.Repeat(yaw + px.x * k + 180f, 360f) - 180f;
            pitch = Mathf.Clamp(pitch + px.y * k, PitchMin, PitchMax);
            OzFpsLook.Trail(ref lagYaw, ref lagPitch, px.x * k, pitch - p0);   // the yaw's travel unwrapped; the pitch's after the clamp
        }

        static bool Down(Keyboard kb, OzAction a)
        {
            var k = OzBindings.KeyOf(a);                                                                     // the player's own scroll keys (W A S D by default); read here, not through OzBindings.Held (silent while the view is on)
            if (k == Key.None) return false;
            try { return kb[k].isPressed; } catch (System.ArgumentOutOfRangeException) { return false; }
        }

        void Walk(Keyboard kb)
        {
            Vector2 d = Vector2.zero;                                                                        // x right, y forward
            if (kb != null)
            {
                if (Down(kb, OzAction.ScrollUp) || kb.upArrowKey.isPressed) d.y += 1f;
                if (Down(kb, OzAction.ScrollDown) || kb.downArrowKey.isPressed) d.y -= 1f;
                if (Down(kb, OzAction.ScrollRight) || kb.rightArrowKey.isPressed) d.x += 1f;
                if (Down(kb, OzAction.ScrollLeft) || kb.leftArrowKey.isPressed) d.x -= 1f;
            }
            if (unit.Kind == ZKind.Cannon) d = Vector2.zero;                                                 // a gun does not walk
            if (d == Vector2.zero)
            {
                if (held) { held = false; if (pi.FpsStop(unit)) StopOrders++; }
                moveDir = sentKeys = Vector2.zero; return;
            }
            float now = Time.unscaledTime;
            var f = Quaternion.Euler(0f, yaw - lagYaw, 0f) * new Vector3(d.x, 0f, d.y).normalized;                     // the wanted direction on the ground (world)   // 1.5 fps_look_smooth: along the drawn look
            var nd = new Vector2(f.x, f.z);
            int mx = Mathf.RoundToInt(rts.mapSizeMeters.x * 8f), my = Mathf.RoundToInt(rts.mapSizeMeters.y * 8f);   // 8 Zod px = 1 m (ZGame.PxToWorld)
            var to = new Vector2Int(Mathf.Clamp(Mathf.RoundToInt(unit.CenterX + f.x * StepPx), 12, Mathf.Max(12, mx - 12)),
                                    Mathf.Clamp(Mathf.RoundToInt(unit.CenterY - f.z * StepPx), 12, Mathf.Max(12, my - 12)));
            if (OzFpsShuttleView.Flies(unit)) to = OzFpsShuttleView.Ahead(unit, f, mx, my);                // 1.5 fps_shuttle_badges: the shuttle's point is past its braking distance (it never plans its stop while a key is held)
            if (held)
            {
                // fix round 3: every player move drops the unit's target (ZWorld.SendWaypoints, DV16) and its fire picks one up again only on the
                // half-second passive check, so the order under way holds while the walk goes straight on: a new one only for a key pressed or let
                // go (at once), a turn of the wanted way past TurnDeg, the unit within NearPx of the point or its walk ended short of it (blocked,
                // settled); those three at most every TurnReissue s and never to the same point again (the map's edge)
                bool keys = d != sentKeys;
                if (OzFpsFeel.SprintFlip(kb, unit)) keys = true;                                                // 1.5 fps_feel: Shift pressed or let go = a new order at once
                if (now - heardAt >= 1f) { heardAt = now; AudioDirector.FpsWalkOrder(unit); }               // its walk stays heard between the orders (AudioDirector.WalkSoundHold 3.5 s; an order can be 2 - 5 s apart now)
                if (!keys)
                {
                    bool turned = Vector2.Angle(moveDir, nd) > TurnDeg;
                    int ex = unit.CenterX - sentTo.x, ey = unit.CenterY - sentTo.y;
                    bool near = ex * ex + ey * ey <= NearPx * NearPx, ended = unit.Waypoints.Count == 0;
                    if (OzFpsShuttleView.Flies(unit)) { near = OzFpsShuttleView.Near(unit, sentTo); ended = false; }   // 1.5 fps_shuttle_badges: its next point once it is within its braking distance of this one (its waypoint list is always empty)
                    if (!turned && !near && !ended) return;                                                // straight on: the order under way holds
                    if (now - sentAt < TurnReissue) return;
                    if (!turned && (to - sentTo).sqrMagnitude < 36) return;                                // the same point: nothing new to say
                }
            }
            held = true; moveDir = nd; sentKeys = d; sentAt = heardAt = now; sentTo = to;
            if (OzFpsFeel.Sprint(kb, unit)) { if (pi.FpsSprint(unit, to.x, to.y)) MoveOrders++; else MoveRefused++; return; }   // 1.5 fps_feel: Shift held = the sprint move (DV178, Sim/ZObject.SprintOrder.cs)
            if (pi.FpsMove(unit, to.x, to.y)) MoveOrders++; else MoveRefused++;
        }

        void LateUpdate() { if (on && I == this) PlaceCamera(false); }

        void PlaceCamera(bool snap)
        {
            if (!view || !rts) return;
            float dt = Time.unscaledDeltaTime;
            var raw = EyePoint();
            eye = snap || !eyeInit ? raw : Vector3.Lerp(eye, raw, 1f - Mathf.Exp(-dt / EyeTau));                   // smooth: the hull's bounce and the head's sway eased, no lag to speak of
            if (OzFpsShuttleView.Flies(unit)) eye = raw;                                                    // 1.5 fps_shuttle_badges: the ship's pose is eased already (an eased eye would trail into the nose at speed)
            eyeInit = true;
            shake = Mathf.Max(shake, rts.TakeShake());                                                         // the game's own hit shakes (gated as always), small at the eye
            Vector3 j = Vector3.zero;
            if (shake > 0.001f && OzShakeGate.SettingOn)
            {
                float t = Time.time;
                j = new Vector3(Mathf.PerlinNoise(t * 31f, 0.3f) - 0.5f, Mathf.PerlinNoise(0.7f, t * 29f) - 0.5f, Mathf.PerlinNoise(t * 23f, 5.1f) - 0.5f) * (shake * 0.12f);
                shake *= Mathf.Exp(-dt * 7f);
            }
            else shake = 0f;
            OzFpsLook.Ease(ref lagYaw, ref lagPitch, dt, snap);   // 1.5 fps_look_smooth: lag *= exp(-dt / Tau) on unscaled time (the first pose: none)
            transform.SetPositionAndRotation(eye + j, Quaternion.Euler(-(pitch - lagPitch), yaw - lagYaw, 0f));   // 1.5 fps_look_smooth: the drawn look (the crosshair, the fire pick and the gun copy follow the camera)
            FogUnder(cam);                                                                                   // the fog's wall stays on while the view is (cheap: one global)
            var vp = view.transform.position; rts.focus = new Vector3(vp.x, 0f, vp.z);                      // what reads the RTS focus (the listener, weather, the outskirts' scenery) follows the unit
            LastFocusSet = rts.focus; LastFocusFrame = Time.frameCount;                                     // fps_view_fix: the check compares the focus with this (never scrolled by the keys)
            HoldHidden();
            OzFpsBadgeHold.Mark(unit, view, figs, unitFigs);                                                // 1.5 fps_shuttle_badges: its parts the GPU Resident Drawer draws (the rank chevrons) get a block, so the hold below takes them too
            GunStep(dt);
        }

        // ------------------------------------------------------------------ the eye
        void FindEye()
        {
            eyeT = null; eyeKind = EyeKind.Top; topUp = 2f;
            var root = view.transform;
            if (unit.Kind == ZKind.Robot)
            {
                var an = view.GetComponentInChildren<Animator>();
                if (an && an.isHuman) eyeT = an.GetBoneTransform(HumanBodyBones.Head);
                if (!eyeT) eyeT = Named(root, n => n == "mixamorigHead" || n == "Head");
                if (eyeT) { eyeKind = EyeKind.Head; return; }
            }
            else
            {
                eyeT = Named(root, n => n.EndsWith("_Board_HatchTop"));
                if (eyeT) { eyeKind = EyeKind.Hatch; return; }
                eyeT = Named(root, n => n.EndsWith("DriverSeat") || n.EndsWith("GunnerSeat"));
                if (eyeT) { eyeKind = EyeKind.Seat; return; }
                eyeT = Named(root, n => n.Contains("TurretYaw") || n == "MechWaistYaw");
            }
            if (!eyeT) eyeT = root;
            float top = float.MinValue; scan.Clear(); view.GetComponentsInChildren(true, scan);
            foreach (var r in scan) if (Model(r)) top = Mathf.Max(top, r.bounds.max.y);
            topUp = (top > float.MinValue ? top - eyeT.position.y : 2f) + 0.3f;
        }

        static Transform Named(Transform root, System.Func<string, bool> f)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t != root && f(t.name)) return t;
            return null;
        }

        Vector3 EyePoint()
        {
            var vt = view.transform; float s = Mathf.Max(0.2f, vt.lossyScale.y);
            if (OzFpsShuttleView.Eye(unit, view, out var shuttleEye)) { EyeBase = shuttleEye; return shuttleEye; }   // 1.5 fps_shuttle_badges: the shuttle's eye, slightly in front of its nose (View/OzFpsShuttleView.cs)
            if (!eyeT) eyeT = vt;
            Vector3 front = -vt.forward; front.y = 0f; front = front.sqrMagnitude > 1e-6f ? front.normalized : Vector3.forward;
            Vector3 p;
            switch (eyeKind)
            {
                case EyeKind.Head: p = eyeT.position + Vector3.up * (0.07f * s) + front * (0.09f * s); break;     // the eyes: a little over and in front of the head bone
                case EyeKind.Hatch: p = eyeT.position + Vector3.up * (0.5f * s); break;                            // a head out of the hatch
                case EyeKind.Seat: p = eyeT.position + Vector3.up * (0.85f * s); break;                            // a seated head
                default: p = eyeT.position + Vector3.up * topUp; break;                                          // over the top of the gun / hull
            }
            if (g) { float gy = g.GroundY(p.x, p.z); if (p.y < gy + 0.9f) p.y = gy + 0.9f; }
            EyeBase = p;                                                                                     // fps_view_sky: fps_view_fix's eye (the check compares)
            return p + Vector3.up * EyeLift;                                                                 // fps_view_sky: a little higher, every eye kind
        }

        // ------------------------------------------------------------------ the unit's own model, held off
        static bool Model(Renderer r) => r && (r is MeshRenderer || r is SkinnedMeshRenderer);

        /// <summary>fps_view_fix (c): the unit's own model renderers (the set HoldFigures leaves to the model). Nothing is written here: they are
        /// held off inside the camera's pictures only (<see cref="OnBeginCam"/>).</summary>
        void HideModel()
        {
            if (!view) return;
            hidden.Clear(); hiddenSet.Clear();
            scan.Clear(); view.GetComponentsInChildren(true, scan);
            foreach (var r in scan) if (Model(r) && hiddenSet.Add(r)) hidden.Add(r);
        }

        /// <summary>Every frame after the eye is placed: the model's list is renewed every half second, the figures by the eye every frame (the
        /// pictures read both; fps_view_fix (c): nothing is held off here).</summary>
        void HoldHidden()
        {
            if (Time.unscaledTime >= rescanAt) { rescanAt = Time.unscaledTime + 0.5f; HideModel(); }
            HoldFigures();
        }

        /// <summary>The leave: whatever the last picture still holds is given back (nothing normally: every picture gives back at its end), the
        /// callbacks go, and the last picture's record is kept for the check (<see cref="LastHeldSet"/>, <see cref="LastForeignOff"/>).</summary>
        void ShowModel()
        {
            GiveBack(); Hook(false); CheckDrawOwn = false;
            LastHeldSet.Clear(); foreach (var r in lastHeld) if (r) LastHeldSet.Add(r);
            LastForeignOff.Clear(); foreach (var r in foreignOff) if (r) LastForeignOff.Add(r);
            int still = 0; foreach (var r in heldNow) if (r && r.forceRenderingOff) still++;
            LastRestored = lastGivenBack; LastStillOff = still + heldNow.Count;                              // the view holds nothing after the leave (0 always)
            hidden.Clear(); hiddenSet.Clear(); figs.Clear(); unitFigs.Clear(); crewT = null; CrewHeld = 0; heldModel = 0;
        }

        // ------------------------------------------------------------------ fps_view_fix (c): held off inside the camera's pictures only
        void Hook(bool want)
        {
            if (want == hooked) return;
            hooked = want;
            if (want) { RenderPipelineManager.beginCameraRendering += OnBeginCam; RenderPipelineManager.endCameraRendering += OnEndCam; }
            else { RenderPipelineManager.beginCameraRendering -= OnBeginCam; RenderPipelineManager.endCameraRendering -= OnEndCam; }
        }

        /// <summary>The player's camera begins a picture (before its culling and its shadows): every model renderer of the unit, of its crew figure
        /// and of the figures by the eye that is drawn now is held off for this picture; one held off already (by the fog, the BS01 type kit, a
        /// cable, anything) is not touched and is noted as the game's (<see cref="LastForeignOff"/>).</summary>
        void OnBeginCam(ScriptableRenderContext ctx, Camera c)
        {
            if (!on || I != this || c != cam || !view) return;
            if (heldNow.Count > 0) GiveBack();
            if (CheckDrawOwn) { AtRender?.Invoke(c); return; }                                                 // Play check only: this picture with the model drawn
            foreignOff.Clear();
            int own = 0, crew = 0;
            rscan.Clear(); view.GetComponentsInChildren(true, rscan);                                        // every picture: a part added this frame is held too
            if (OzFpsFeel.OwnDrawn) rscan.Clear();                                                          // 1.5 fps_feel: while the camera glides the unit's own model is drawn (faded by View/OzFpsFeel.cs)
            for (int i = 0; i < rscan.Count; i++) if (Model(rscan[i]) && Take(rscan[i])) own++;
            foreach (var kv in figs)
            {
                if (!kv.Key) continue;
                if (OzFpsFeel.Fades(kv.Key, crewT)) continue;                                                   // 1.5 fps_feel: a unit or figure by the eye fades (View/OzFpsFeel.cs) instead of being held off in one frame
                int n = 0; var l = kv.Value;
                for (int i = 0; i < l.Count; i++) if (Take(l[i])) n++;
                if (kv.Key == crewT) crew = n;
            }
            heldModel = own; CrewHeld = crew; RenderHolds++;
            AtRender?.Invoke(c);
        }

        bool Take(Renderer r)
        {
            if (!r || heldSet.Contains(r)) return false;
            if (r.forceRenderingOff) { foreignOff.Add(r); return false; }                                     // held off by other code: its state, not ours
            r.forceRenderingOff = true; heldNow.Add(r); heldSet.Add(r); return true;
        }

        void OnEndCam(ScriptableRenderContext ctx, Camera c) { if (c == cam) GiveBack(); }

        /// <summary>fps_view_fix fix round 1, Play check only: the renderers the last picture held off (the model, the crew figure, the units by
        /// the eye), copied into into.</summary>
        public void HeldInLastPicture(List<Renderer> into) { into.Clear(); foreach (var r in lastHeld) if (r) into.Add(r); }

        /// <summary>Every renderer this picture held off is drawn again (exactly as it was before the picture began).</summary>
        void GiveBack()
        {
            if (heldNow.Count == 0) return;
            lastHeld.Clear(); int n = 0;
            for (int i = 0; i < heldNow.Count; i++) { var r = heldNow[i]; if (r) { r.forceRenderingOff = false; lastHeld.Add(r); n++; } }
            heldNow.Clear(); heldSet.Clear(); lastGivenBack = n;
        }

        // ------------------------------------------------------------------ fix round 2: the crew figures BoardingView draws outside the unit's view
        /// <summary>A part of a figure within this distance of the eye (m): the figure is held off (the camera would stand in it).</summary>
        public const float NearHide = 1.5f;
        /// <summary>A figure held off for being near is given back once every part of it is this far from the eye (m).</summary>
        public const float NearGive = 2.5f;
        readonly Dictionary<Transform, List<Renderer>> figs = new Dictionary<Transform, List<Renderer>>();
        /// <summary>Fix round 3: the entries of figs that are other units' views (own squad mates, any unit by the eye).</summary>
        readonly HashSet<Transform> unitFigs = new HashSet<Transform>();
        static readonly List<Transform> figGone = new List<Transform>();
        Transform crewT;
        /// <summary>Fix round 3: a unit view whose root is within this distance of the eye (m) is looked at for parts near the eye (the long
        /// hulls: a part 1.5 m from the eye has its root well inside it); one held off is given back past <see cref="UnitRootGive"/> m too.</summary>
        public const float UnitRoot = 10f, UnitRootGive = 11f;
        /// <summary>Renderers of the unit's own BoardingView crew figure held off now (the jeep's seated driver, a tank's, the APC's or the crane's
        /// hatch commander, a gun's gunner); 0 without one.</summary>
        public int CrewHeld { get; private set; }
        /// <summary>Figures under OZ_BoardingCrew and other units' views held off now (the crew and anything by the eye).</summary>
        public int FiguresHeld => figs.Count;
        /// <summary>Fix round 3: other units' views held off now for a part within <see cref="NearHide"/> m of the eye (a squad mate at 0.75 - 1.5 m).</summary>
        public int UnitsHeld => unitFigs.Count;
        public bool CrewFigureUp => crewT && crewT.gameObject.activeInHierarchy;

        /// <summary>Every frame, after the eye is placed: BoardingView keeps a vehicle's (or a gun's) crew under its own root OZ_BoardingCrew, not under
        /// the unit's view, so HideModel never sees it. The unit's own crew figure (BoardingView.CrewFigure) is held off with every part, the hidden
        /// ones too (a hatch commander is spawned and popped up in BoardingView's LateUpdate, before this one: never drawn for a frame); any other
        /// figure there with a part within <see cref="NearHide"/> m of the eye (a robot boarding or getting out by the hatch, a passenger) is held
        /// off until it is <see cref="NearGive"/> m away. Fix round 3: the same for every other unit's view (the robot's own squad mates, which stand
        /// 0.75 - 1.5 m from it, any robot, vehicle or gun the eye comes up to). All of them are given back at the leave, as the model is.</summary>
        void HoldFigures()
        {
            var crew = BoardingView.CrewFigure(unit.Id); crewT = crew ? crew.transform : null;
            Vector3 e = transform.position;
            if (crewT && !figs.ContainsKey(crewT)) { figs[crewT] = new List<Renderer>(); FiguresTaken++; }
            var box = BoardingView.CrewBox;
            if (box)
                for (int i = 0; i < box.childCount; i++)
                {
                    var t = box.GetChild(i);
                    if (t == crewT || figs.ContainsKey(t) || !t.gameObject.activeInHierarchy || (t.position - e).sqrMagnitude > 25f) continue;   // a figure's root is its hips or feet: 5 m holds every part of it
                    if (Reach(t, e) < NearHide * NearHide) { figs[t] = new List<Renderer>(); FiguresTaken++; }
                }
            // fix round 3: other units' views by the eye (a robot's own squad mates stand 0.75 - 1.5 m from it; any unit the eye walks into), the same rule
            var lv = g ? g.LiveViewValues : null;
            if (lv != null)
                for (int i = 0; i < lv.Count; i++)
                {
                    var uv = lv[i]; if (!uv || uv == view) continue;
                    var t = uv.transform; var o = uv.Obj;
                    if (o == null || !o.IsUnit || figs.ContainsKey(t) || (t.position - e).sqrMagnitude > UnitRoot * UnitRoot) continue;
                    if (Reach(t, e) < NearHide * NearHide) { figs[t] = new List<Renderer>(); unitFigs.Add(t); FiguresTaken++; UnitsTaken++; }
                }
            figGone.Clear();
            foreach (var kv in figs)
            {
                var t = kv.Key; var l = kv.Value;
                if (!t) { figGone.Add(t); continue; }                                                        // destroyed: its renderers went with it
                float rootOut = unitFigs.Contains(t) ? UnitRootGive * UnitRootGive : 36f;
                if (t != crewT && ((t.position - e).sqrMagnitude > rootOut || Reach(t, e) > NearGive * NearGive)) { figGone.Add(t); continue; }   // fps_view_fix (c): nothing to give back (held inside the pictures only)
                l.Clear(); scan.Clear(); t.GetComponentsInChildren(true, scan);                              // every frame: a weapon or a part added to the figure joins at once
                foreach (var r in scan) if (Model(r) && !hiddenSet.Contains(r)) l.Add(r);                    // the pictures hold the drawn ones and leave the others as they are
            }
            foreach (var t in figGone) { figs.Remove(t); unitFigs.Remove(t); }
        }

        /// <summary>The nearest drawn part of the figure t to the point e (squared m; a large number when it has none).</summary>
        static float Reach(Transform t, Vector3 e)
        {
            float best = 1e9f; scan.Clear(); t.GetComponentsInChildren(false, scan);
            foreach (var r in scan) if (Model(r)) best = Mathf.Min(best, r.bounds.SqrDistance(e));
            return best;
        }

        // ------------------------------------------------------------------ the gun copy
        /// <summary>The unit's real gun: a robot's weapon (BS01_Weapon_*, Rifle_Heavy, Bazooka); a vehicle's or gun's barrel part (BarrelPitch, the mech's
        /// gun arm, BarrelSpin, Barrel), else its turret.</summary>
        Transform GunRoot()
        {
            var root = view.transform;
            if (unit.Kind == ZKind.Robot)
                return Named(root, n => n.StartsWith("BS01_Weapon_") || n == "Rifle_Heavy" || n == "Bazooka");
            System.Func<string, bool>[] tries =
            {
                n => n.Contains("BarrelPitch") && !n.Contains("_Surface"),
                n => n.Contains("GunRecoil"),
                n => n.Contains("GunShoulder"),
                n => n.Contains("BarrelSpin"),
                n => n.Contains("Barrel") && !n.Contains("_Surface"),
                n => n.Contains("TurretYaw") && !n.Contains("_Surface"),
            };
            foreach (var f in tries)
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (t != root && f(t.name) && Solid(t).Count > 0) return t;
            return null;
        }

        static readonly List<MeshRenderer> parts = new List<MeshRenderer>();
        static List<MeshRenderer> Solid(Transform t)
        {
            parts.Clear(); var all = t.GetComponentsInChildren<MeshRenderer>(false);
            foreach (var r in all)
            {
                if (!r.enabled) continue;
                var mf = r.GetComponent<MeshFilter>(); if (!mf || !mf.sharedMesh) continue;
                bool skip = false;
                for (var a = r.transform; a && a != t.parent; a = a.parent)
                {
                    var n = a.name;
                    if (n.StartsWith("crew_") || n.Contains("Gunner") || n.Contains("Driver") || n.Contains("Commander") || n.Contains("Flash") || n.Contains("Glow")
                        || n.Contains("Fx") || n.Contains("Beam") || n.Contains("Halo") || n.EndsWith("Light") || n.Contains("Lamp") || n.Contains("Smoke") || n.Contains("Shadow") || n.Contains("Decal")) { skip = true; break; }
                }
                var sm = r.sharedMaterial; if (sm && sm.renderQueue >= 2900) skip = true;                         // transparent / additive effects
                if (!skip) parts.Add(r);
            }
            return parts;
        }

        // ------------------------------------------------------------------ fps_view_fix (b): a vehicle's or a gun's copy is its barrel, like a tank sight
        /// <summary>A part of a vehicle's or a gun's gun is a barrel part when it is at least this many times longer along the barrel's line than
        /// it is thick (the light tank's barrel 2.6, the heavy's 4.7, the mech's spinning barrels 1.7, the gun's 2.4, the jeep's rotor 3.1, the
        /// gatling's barrels 2.1; the blocks are under 1.2: the light and medium tank's mantlet 0.55, the gun's mantlet 1.15, the mech's receiver
        /// 0.84, the jeep's receiver 1.1 and its shield band 0.06, the gatling's housing 1.1). Measured on the models
        /// (kits/fps_view_fix/_work/gun_roots.py, asset_mesh_bounds.py, fbx_bounds.py, sight_sim.py).</summary>
        public const float BarrelThin = 1.5f;
        /// <summary>The barrel copy: its thickness in front of the eye (m) and its longest length (m); the near end of its centre line in camera
        /// space (below the bottom edge of the view: the barrel comes up from the lower middle); its line meets the view's centre GunConverge m
        /// ahead. On a 16:9 screen the light tank's barrel covers about 3.4 % of it, x 0.42 .. 0.58, from the bottom edge up to 0.26 of the
        /// height (the heavy's twin barrels x 0.43 .. 0.57 up to 0.20: sized by both together), as sight_sim.py drew it.</summary>
        public const float SightCross = 0.20f, SightLenMax = 1.1f;
        static readonly Vector3 SightNear = new Vector3(0f, -0.42f, 0.60f);
        /// <summary>The parts the copy took and the parts it left out (names and how thin each is; for the check and the log).</summary>
        public string GunKept { get; private set; } = "";
        public string GunDropped { get; private set; } = "";
        /// <summary>True: the copy is a vehicle's or a gun's barrel at the lower middle (a robot's gun sits in the lower right).</summary>
        public bool GunSight => gun && sight;
        /// <summary>The copy's renderers (the check reads their picture and their box on the screen).</summary>
        public readonly List<Renderer> GunRenderers = new List<Renderer>();
        Vector3 anchorLocal; bool sight;
        static readonly List<Vector3> verts = new List<Vector3>();

        /// <summary>A mesh's box in its own space: its real vertices when it can be read (a few generated meshes carry a stale box: the gatling's
        /// split meshes keep the whole turret's), else the union of its sub-meshes' boxes, else its own box.</summary>
        static Bounds LocalBox(Mesh m)
        {
            if (m.isReadable)
            {
                verts.Clear(); m.GetVertices(verts);
                if (verts.Count > 0) { var b = new Bounds(verts[0], Vector3.zero); for (int i = 1; i < verts.Count; i++) b.Encapsulate(verts[i]); return b; }
            }
            bool any = false; Bounds u = default;
            for (int i = 0; i < m.subMeshCount; i++)
            {
                var sb = m.GetSubMesh(i).bounds; if (sb.size.sqrMagnitude < 1e-8f) continue;
                if (!any) { u = sb; any = true; } else u.Encapsulate(sb);
            }
            return any ? u : m.bounds;
        }

        static void Corners(Bounds mb, Matrix4x4 m, List<Vector3> into)
        {
            for (int i = 0; i < 8; i++)
                into.Add(m.MultiplyPoint3x4(mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))));
        }

        void BuildGun(out Vector3 barrelWorld)
        {
            barrelWorld = Vector3.zero; DropGun(); GunWhy = ""; GunRootName = ""; GunKept = ""; GunDropped = "";
            var root = GunRoot();
            if (OzFpsShuttleView.Flies(unit)) root = null;                                                  // 1.5 fps_shuttle_badges: the shuttle has no gun to copy
            if (!root) { GunWhy = "no gun part in the model"; return; }
            GunRootName = root.name;
            var rs = new List<MeshRenderer>(Solid(root));
            if (rs.Count == 0) { GunWhy = "no solid mesh under " + root.name; return; }
            bool robot = unit.Kind == ZKind.Robot;
            var w2l = root.worldToLocalMatrix;
            // each part's 8 box corners in the gun's own space (a robot's as before: the mesh's own box, so the approved robot copies stay as they are)
            var box = new List<List<Vector3>>(rs.Count);
            foreach (var r in rs)
            {
                var mesh = r.GetComponent<MeshFilter>().sharedMesh; var c = new List<Vector3>(8);
                Corners(robot ? mesh.bounds : LocalBox(mesh), w2l * r.transform.localToWorldMatrix, c); box.Add(c);
            }
            // the barrel's line in the gun's own space: from its pivot (a robot's grip, a barrel's root) to the muzzle socket, else its longest side
            Vector3 axis = Vector3.zero; int nm = 0; gripLocal = Vector3.zero;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t != root && t.name.Contains("Muzzle") && !t.name.Contains("Flash")) { axis += w2l.MultiplyPoint3x4(t.position); nm++; }
                if (t != root && (t.name == "Grip" || t.name == "Rifle_grip" || t.name == "Bazooka_grip")) gripLocal = w2l.MultiplyPoint3x4(t.position);   // a robot's grip socket: the hand's place
            }
            if (nm > 0) axis = axis / nm - gripLocal;
            if (axis.sqrMagnitude < 0.0025f)
            {
                var b = new Bounds(box[0][0], Vector3.zero); foreach (var c in box) foreach (var p in c) b.Encapsulate(p);
                var e = b.size; int k = e.x >= e.y && e.x >= e.z ? 0 : e.y >= e.z ? 1 : 2;
                axis = Vector3.zero; axis[k] = b.center[k] >= gripLocal[k] ? 1f : -1f;           // toward the far end from the grip
            }
            axis.Normalize(); gunAxis = axis;
            Vector3 up = w2l.MultiplyVector(Vector3.up); up -= axis * Vector3.Dot(up, axis);
            if (up.sqrMagnitude < 1e-6f) up = Vector3.Cross(axis, Mathf.Abs(axis.x) < 0.9f ? Vector3.right : Vector3.forward);
            up.Normalize();
            var side = Vector3.Cross(up, axis).normalized;
            // which parts: a robot's whole weapon; a vehicle's or a gun's thin parts along the line (no mantlet, receiver, shield or housing)
            var keep = new List<int>(); var kept = new List<string>(); var dropped = new List<string>();
            float bestE = -1f; int best = -1;
            for (int i = 0; i < rs.Count; i++)
            {
                if (robot) { keep.Add(i); continue; }
                Extent(box[i], axis, up, side, out float l0, out float l1, out float u0, out float u1, out float s0, out float s1);
                float e = (l1 - l0) / Mathf.Max(Mathf.Max(u1 - u0, s1 - s0), 1e-3f);
                if (e > bestE) { bestE = e; best = i; }
                if (e >= BarrelThin) { keep.Add(i); kept.Add(rs[i].name + " " + e.ToString("0.00")); } else dropped.Add(rs[i].name + " " + e.ToString("0.00"));
            }
            if (!robot && keep.Count == 0 && best >= 0)
            {
                keep.Add(best); string bn = rs[best].name + " ";
                kept.Add(rs[best].name + " (the thinnest: no barrel part)"); dropped.RemoveAll(s => s.StartsWith(bn)); GunWhy = "no thin barrel part: the longest one only";
            }
            GunKept = robot ? rs.Count + " parts (a robot's weapon)" : string.Join(", ", kept); GunDropped = string.Join(", ", dropped);
            // the copy: the kept parts' meshes, materials and paint blocks
            gun = new GameObject("OzFpsGun"); gun.transform.SetParent(transform, false);
            gunPivot = new GameObject("OzFpsGunPivot").transform; gunPivot.SetParent(gun.transform, false);
            var pb = new MaterialPropertyBlock(); var all = new List<Vector3>(); gunParts = 0; GunRenderers.Clear();
            fitM.Clear(); fitB.Clear();
            foreach (int i in keep)
            {
                var r = rs[i];
                var mf = r.GetComponent<MeshFilter>(); var m = w2l * r.transform.localToWorldMatrix;
                fitM.Add(m); fitB.Add(robot ? mf.sharedMesh.bounds : LocalBox(mf.sharedMesh));                  // fps_view_sky: each kept part's box (the robot fit)
                var go = new GameObject("OzFpsGun_" + r.name); go.layer = r.gameObject.layer;
                go.transform.SetParent(gunPivot, false);
                go.transform.localPosition = m.GetColumn(3); go.transform.localRotation = m.rotation; go.transform.localScale = m.lossyScale;
                go.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                var mr = go.AddComponent<MeshRenderer>(); var mats = r.sharedMaterials; mr.sharedMaterials = mats;
                if (r.HasPropertyBlock())
                {
                    r.GetPropertyBlock(pb); if (!pb.isEmpty) mr.SetPropertyBlock(pb);
                    for (int j = 0; j < mats.Length; j++) { r.GetPropertyBlock(pb, j); if (!pb.isEmpty) mr.SetPropertyBlock(pb, j); }   // the team paint as the real gun has it
                }
                mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = r.receiveShadows; mr.lightProbeUsage = r.lightProbeUsage;
                mr.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;                        // it moves with the eye: never smeared
                all.AddRange(box[i]); GunRenderers.Add(mr);
                gunParts++;
            }
            Extent(all, axis, up, side, out float lo, out float hi, out float ua, out float ub, out float sa, out float sb);
            float len = Mathf.Max(0.01f, hi - lo);
            sight = !robot;
            if (robot)
            {   // as before: the grip at the hand's place in the lower right
                gunGrip = GripRobot; anchorLocal = gripLocal;
                gunScale = GunLenRobot / len;
            }
            else
            {   // the barrel's near end on its own centre line, just under the bottom edge of the view, in the middle; sized by its thickness
                float cross = Mathf.Max(Mathf.Max(ub - ua, sb - sa), 0.01f);
                gunScale = Mathf.Min(SightCross / cross, SightLenMax / len);
                anchorLocal = axis * lo + up * (0.5f * (ua + ub)) + side * (0.5f * (sa + sb));
                gunGrip = SightNear;
            }
            gunDir = (new Vector3(0f, 0f, GunConverge) - gunGrip).normalized;                                // its line meets the view's centre GunConverge m ahead
            gunRot = Quaternion.LookRotation(gunDir, Vector3.up) * Quaternion.Inverse(Quaternion.LookRotation(axis, up));
            GunFit = 1f; GunCoverBox0 = GunCoverBox = -1f;
            if (robot) FitRobot();                                                                           // fps_view_sky: at most RobotCoverMax of the screen
            gunPivot.localScale = Vector3.one * gunScale;
            gunPivot.localRotation = gunRot; gunPivot.localPosition = gunGrip - gunRot * (anchorLocal * gunScale);   // the grip (the barrel's near end) at its place
            barrelWorld = root.TransformDirection(axis);
        }

        // ------------------------------------------------------------------ fps_view_sky: the robot copy no bigger than RobotCoverMax of the screen
        readonly List<Matrix4x4> fitM = new List<Matrix4x4>(); readonly List<Bounds> fitB = new List<Bounds>();

        /// <summary>The robot copy at gunScale: if its parts' boxes cover more than RobotCoverMax of the screen it is made smaller about its grip
        /// (the grip stays at GripRobot), at most to RobotFitMin of its size. Once at the entry (a few thousand rays).</summary>
        void FitRobot()
        {
            float s0 = gunScale, s = s0, c = BoxCover(s);
            GunCoverBox0 = c;
            for (int k = 0; k < 6 && c > RobotCoverMax && s > s0 * RobotFitMin + 1e-5f; k++)
            {
                s = Mathf.Max(s0 * RobotFitMin, s * Mathf.Sqrt(RobotCoverMax / c) * 0.98f);
                c = BoxCover(s);
            }
            gunScale = s; GunFit = s / s0; GunCoverBox = c;
        }

        /// <summary>The share of the view's screen the kept parts' boxes cover with the copy at scale s (camera space: x right, y up, z ahead;
        /// a ray from the eye through each cell's centre against each part's box in its own space; only what lies past the near clip counts).</summary>
        float BoxCover(float s)
        {
            if (fitM.Count == 0 || !cam) return 0f;
            var piv = Matrix4x4.TRS(gunGrip - gunRot * (anchorLocal * s), gunRot, Vector3.one * s);
            int n = fitM.Count; var inv = new Matrix4x4[n]; var org = new Vector3[n];
            for (int i = 0; i < n; i++) { inv[i] = (piv * fitM[i]).inverse; org[i] = inv[i].MultiplyPoint3x4(Vector3.zero); }
            float th = Mathf.Tan(0.5f * FovDeg * Mathf.Deg2Rad), tw = th * Mathf.Max(0.1f, cam.aspect);
            int hit = 0;
            for (int y = 0; y < FitRows; y++)
                for (int x = 0; x < FitCols; x++)
                {
                    var d = new Vector3(((x + 0.5f) / FitCols * 2f - 1f) * tw, ((y + 0.5f) / FitRows * 2f - 1f) * th, 1f);   // its z is 1: the ray's parameter is the depth
                    for (int i = 0; i < n; i++) if (RayBox(org[i], inv[i].MultiplyVector(d), fitB[i])) { hit++; break; }
                }
            return hit / (float)(FitCols * FitRows);
        }

        static bool RayBox(Vector3 o, Vector3 d, Bounds b)
        {
            float t0 = NearClip, t1 = 1e6f; Vector3 lo = b.min, hi = b.max;
            for (int k = 0; k < 3; k++)
            {
                if (Mathf.Abs(d[k]) < 1e-9f) { if (o[k] < lo[k] || o[k] > hi[k]) return false; continue; }
                float a = (lo[k] - o[k]) / d[k], c = (hi[k] - o[k]) / d[k];
                if (a > c) { float t = a; a = c; c = t; }
                t0 = Mathf.Max(t0, a); t1 = Mathf.Min(t1, c);
                if (t0 > t1) return false;
            }
            return true;
        }

        /// <summary>The span of the points along the line (l), its up (u) and its side (s) directions.</summary>
        static void Extent(List<Vector3> pts, Vector3 axis, Vector3 up, Vector3 side, out float l0, out float l1, out float u0, out float u1, out float s0, out float s1)
        {
            l0 = u0 = s0 = float.MaxValue; l1 = u1 = s1 = float.MinValue;
            foreach (var p in pts)
            {
                float l = Vector3.Dot(p, axis), u = Vector3.Dot(p, up), s = Vector3.Dot(p, side);
                l0 = Mathf.Min(l0, l); l1 = Mathf.Max(l1, l); u0 = Mathf.Min(u0, u); u1 = Mathf.Max(u1, u); s0 = Mathf.Min(s0, s); s1 = Mathf.Max(s1, s);
            }
        }

        void GunStep(float dt)
        {
            if (!gun || !gunPivot || !view) return;
            if (view.LastFireAt != lastFire) { lastFire = view.LastFireAt; kick = 1f; }                            // the unit's own shot: a small kick of the copy (no camera shake)
            kick *= Mathf.Exp(-dt / 0.09f);
            float kr = sight ? 1f : 3f, ks = sight ? 0.06f : 0.035f;                                          // fps_view_fix (b): a barrel slides back more and lifts less
            var r = Quaternion.AngleAxis(-kr * kick, Vector3.right) * gunRot;                                   // the muzzle up a little, round the grip
            gunPivot.localRotation = r;
            gunPivot.localPosition = gunGrip - r * (anchorLocal * gunScale) - gunDir * (ks * kick) + Vector3.up * (0.01f * kick);
        }

        void DropGun() { if (gun) Destroy(gun); gun = null; gunPivot = null; gunParts = 0; GunRenderers.Clear(); }

        // ------------------------------------------------------------------ crosshair and the ESC hint
        void OnGUI()
        {
            if (!on || I != this || Event.current.type != EventType.Repaint) return;
            if (OzFpsFeel.HideGui) return;                                                                   // 1.5 fps_feel: no crosshair while the camera is still high in the glide in
            GuiFrames++;
            if (!white) { white = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave }; white.SetPixel(0, 0, Color.white); white.Apply(); }
            float k = Mathf.Max(0.75f, Screen.height / 1080f), cx = Mathf.Round(Screen.width * 0.5f), cy = Mathf.Round(Screen.height * 0.5f);
            float len = Mathf.Round(7f * k), gap = Mathf.Round(4f * k), th = Mathf.Max(1f, Mathf.Round(1.5f * k));
            GUI.color = new Color(0f, 0f, 0f, 0.45f); Cross(cx + 1f, cy + 1f, len, gap, th);
            GUI.color = new Color(1f, 1f, 1f, 0.85f); Cross(cx, cy, len, gap, th);
            float age = Time.unscaledTime - enterAt, fade = 0.4f;
            if (age < HintSeconds + fade)
            {
                if (hintStyle == null) hintStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
                hintStyle.fontSize = Mathf.RoundToInt(15f * k);
                GUI.color = new Color(1f, 1f, 1f, 0.7f * Mathf.Clamp01((HintSeconds + fade - age) / fade));
                GUI.Label(new Rect(cx - 60f * k, Screen.height - 64f * k, 120f * k, 24f * k), "ESC", hintStyle);
            }
            GUI.color = Color.white;
        }

        static void Cross(float cx, float cy, float len, float gap, float th)
        {
            GUI.DrawTexture(new Rect(cx - gap - len, cy - th * 0.5f, len, th), white);
            GUI.DrawTexture(new Rect(cx + gap, cy - th * 0.5f, len, th), white);
            GUI.DrawTexture(new Rect(cx - th * 0.5f, cy - gap - len, th, len), white);
            GUI.DrawTexture(new Rect(cx - th * 0.5f, cy + gap, th, len), white);
        }
    }
}
