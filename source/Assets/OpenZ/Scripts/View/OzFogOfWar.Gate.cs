// OpenZ (GPL-3.0)
// fog output reads; the rule and its frame clock are View/OzFowGate.cs (plain C#, counted without Play).
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// Version 1.4 kit fog_gameplay_only: the fog of war (when it is on in OPTIONS) is on screen only while the player plays: the normal RTS
    /// camera under his control in a running match. Every moment the game takes the camera or the screen for a scene shows no fog: the
    /// level-start flyover and briefing (red and blue) and the briefing's window until it has folded, a mid-match flyover (ZodCinePreview,
    /// DebugBriefing), the blue scene windows, the ending (the glide, the strike, the orbit, the lounger and hot tub scenes, the blue endings
    /// and nuke, the end screen), capture and flyby cameras, a scene camera on the whole screen, the computer playing the player's side (demo).
    /// The main menu with its flybys and level cards, the loading screen and the intro have no fog at all (no ZGame there), and the Unit
    /// editor never has it (WhyOff). Commander Zod's own window over the running match (the idle barb) takes neither the camera nor the
    /// screen: gameplay, the fog stays (<see cref="OzFowGate.CornerWindowKeepsFogDefault"/>).
    /// <list type="bullet">
    /// <item>One gate (<see cref="GameplayView"/>, <see cref="GateWhy"/>, the rule <see cref="OzFowGate.Decide"/> and its frame clock
    /// <see cref="OzFowGateClock"/>): read in Update, again in LateUpdate (a scene a coroutine or an earlier LateUpdate began) and again at the
    /// player's camera (a scene begun later in the frame), so a hard cut's first picture has no cloud and no unit held off.</item>
    /// <item>Every output follows it: the drawn sheets and the camera's dithering and the minimap cover (with the fade); <see cref="Covers"/>
    /// (the hover, the click, the attack orders, the health bars, the badges, the crate markers, <see cref="Hiding"/>) and the units held off
    /// under the fog (<see cref="HoldingOff"/>), both whenever the player plays: from the first frame of play whatever the fade, so the fade in
    /// never shows a covered enemy under a thin cloud nor a bar or a marker over a hidden one; both off from a scene's first frame, where the
    /// units show. A kit that adds an output (a see-through
    /// look, a radar picture) reads <see cref="GameplayView"/>, <see cref="Hiding"/> or <see cref="HoldingOff"/>.</item>
    /// <item>The fade: <see cref="OzFowGate.FadeSeconds"/> out when a scene starts and in when the player has the camera again, on
    /// smootherstep, in real time (the capture step while a take records; a frame counts at most <see cref="OzFowGate.MaxStep"/>); 0 at once
    /// on a hard cut; covered at once when play starts with no scene before it. The reveal map keeps running under every scene, so play comes
    /// back with the right explored ground.</item>
    /// <item>Gameplay keeps the fog: the pause menu and the in-match OPTIONS, the game speed, camera shakes, zooms, follow, free look, fly-tos,
    /// the minimap, the testing panel, Commander Zod's window over the match (<see cref="OzFowGate.CornerWindowKeepsFogDefault"/>).</item>
    /// <item>FOG OF WAR off in OPTIONS (or any WhyOff reason): nothing changes, no fog anywhere (Update returns before the gate).</item>
    /// </list>
    /// Neither Covers nor the units' hiding follows the strength any more (<see cref="OzFowGate.CoversAt"/> and <see cref="OzFowGate.HoldsOff"/>:
    /// the gate open and the fog drawable); HideAt (OzFogOfWar.cs) is no longer read once the kit is in.
    /// </summary>
    public sealed partial class OzFogOfWar
    {
        readonly OzFowGateClock gateClock = new OzFowGateClock();                       // the fade's clock and the gate's outputs (View/OzFowGate.cs)
        readonly OzFowWindowTail gateBriefTail = new OzFowWindowTail();                 // Zod's briefing window still folding after the briefing (View/OzFowGate.cs)
        int gateCamFrame = -1;
        RtsCamera gateRts; Camera gateCam; float gateFindAt;

        /// <summary>The one gate: true while the player plays with the fog on (the normal RTS camera his in a running match). Every fog
        /// output follows it; read it from any new one.</summary>
        public static bool GameplayView => I && I.wanted && I.mask != null && I.gateClock.Open;
        /// <summary>Why the fog is not on screen now (null = the player plays and it is): the gate's reason, or the match's WhyOff reason.</summary>
        public static string GateReason => !I ? "no level" : !I.wanted || I.mask == null ? (I.why ?? "off") : I.gateClock.Why;
        /// <summary>The gate is closed by a moment that came in with a hard cut (the fog went at once).</summary>
        public static bool GateCut => I && I.wanted && I.gateClock.Cut;
        /// <summary>The fade's clock now (0 .. 1, linear in time; <see cref="Strength"/> is its smootherstep).</summary>
        public static float GateFade => I ? I.gateClock.Fade : 0f;
        /// <summary><see cref="Covers"/> answers now (the hover, the click, the attack orders, the bars, the badges, the crate markers skip
        /// covered things): whenever the player plays (the gate open, from its first frame whatever the fade), the same moment as
        /// <see cref="HoldingOff"/>; never in a scene.</summary>
        public static bool Hiding => hidingNow;
        /// <summary>Units in covered ground are held off on the player's camera now: whenever the player plays (the gate open, from its first
        /// frame whatever the fade).</summary>
        public static bool HoldingOff => I && I.wanted && I.mask != null && I.gateClock.HoldOff;
        /// <summary>Checks: the gate's closes (and those by a hard cut, those found after Update: LateUpdate or the camera), its opens, the
        /// cuts found only at the player's camera (a scene begun after LateUpdate), the matches that started covered at once, and the real
        /// time of the last close and open.</summary>
        public static int GateCloses, GateCutCloses, GateLateCloses, GateOpens, GateCameraCuts, GateFirstFrameCovers;
        public static float GateClosedAt = -1f, GateOpenedAt = -1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void GateResetStatics()
        {
            GateCloses = GateCutCloses = GateLateCloses = GateOpens = GateCameraCuts = GateFirstFrameCovers = 0; GateClosedAt = GateOpenedAt = -1f;
            OzFowGate.CornerWindowKeepsFog = OzFowGate.CornerWindowKeepsFogDefault;       // a check's setting never outlives its Play (the default is the constant)
        }

        /// <summary>The time step of the fade: the capture step while a take records at a fixed rate, else real time (OzFowGate.FadeStep
        /// counts a frame at most OzFowGate.MaxStep, so a hitch is a short ramp).</summary>
        static float GateDt => Time.captureDeltaTime > 0f ? Time.captureDeltaTime : Time.unscaledDeltaTime;

        /// <summary>The gate's inputs now (static flags, the cached RTS camera, the short bot list: cheap, read up to three times a frame).
        /// Never read here: the pause menu, Time.timeScale, the game speed, shakes, zooms, follow, free look, the HUD, the testing panel.</summary>
        public OzFowGateInputs GateInputsNow()
        {
            GateFindCamera();
            bool zodWindow = ZodBriefing.CommsVisible;
            bool briefTail = gateBriefTail.Step(ZodBriefing.BriefingActive, zodWindow);   // stepped on every read (a blue window must not skip it)
            return new OzFowGateInputs
            {
                Briefing = ZodBriefing.BriefingActive,
                BlueBriefing = OzBlueCameo.BriefingActive,
                Flyover = ZodFlyover.Live != null && ZodFlyover.Live.Active,
                EndingHold = ZodBriefing.EndingHolding,
#if UNITY_EDITOR
                EpicFlyby = OzEpicFlyby.Busy,                                            // the editor's flyby recorder (it exists in the editor only)
#endif
                CaptureCine = CaptureKit.CineActive,
                NoRtsCamera = !gateRts,
                RtsCameraOff = gateRts && !gateRts.isActiveAndEnabled,
                CameraOff = gateRts && !(gateCam && gateCam.isActiveAndEnabled),
                ScreenTaken = gateCam && gateCam.cullingMask == 0,
                SimWaits = game && !game.enabled,
                MatchDecided = W != null && PlayerInput.MatchDecided(W),
                Demo = ComputerPlaysPlayer(),
                SceneWindow = briefTail || OzBlueCameo.SceneShowing || GateBlueWindow(),
                CornerWindow = zodWindow,
            };
        }

        /// <summary>A blue scene's comms window on screen, open or folding (show.done comes at the fold's start, the window stays Visible until
        /// it has folded). OzBlueCameo's window shows only its own scenes (the briefing, the nuke scene, the endings): its two opens are the
        /// settled briefing and StartShow.</summary>
        static bool GateBlueWindow() { var b = OzBlueCameo.I; return b && b.Comms != null && b.Comms.Visible; }

        /// <summary>The gate now: why the fog is not on screen (null = it is), and whether that moment came in with a hard cut.</summary>
        public string GateNow(out bool cut) => OzFowGate.Decide(GateInputsNow(), out cut);

        /// <summary>The player's RTS camera, found once (Camera.main's first, else any) and again a second later when it went.</summary>
        void GateFindCamera()
        {
            if (gateRts) { if (!gateCam) gateCam = gateRts.GetComponent<Camera>(); return; }
            if (Time.unscaledTime < gateFindAt) return;
            gateFindAt = Time.unscaledTime + 1f;
            var main = Camera.main;
            gateRts = main && main.TryGetComponent<RtsCamera>(out var r) ? r : FindFirstObjectByType<RtsCamera>();
            gateCam = gateRts ? gateRts.GetComponent<Camera>() : null;
        }

        /// <summary>A computer player drives the player's side now (CaptureKit's F8 demo; a bot the human took the side back from is Ignored).</summary>
        bool ComputerPlaysPlayer()
        {
            if (W == null) return false;
            var bots = W.Bots;
            for (int i = 0; i < bots.Count; i++) { var b = bots[i]; if (b != null && b.Team == player && !b.Ignored) return true; }
            return false;
        }

        /// <summary>Update (the hook in OzFogOfWar.cs, after the reveal step): the gate read, the fade one step on, strength, Covers and the
        /// hiding set. Returns true while the player plays.</summary>
        bool GateStep()
        {
            string w = GateNow(out bool cut);
            gateClock.Update(w, cut, GateDt, Time.frameCount, mat, strength);
            if (gateClock.FirstOpen) GateFirstFrameCovers++;
            GateAfter(cut, false);
            return w == null;
        }

        /// <summary>The gate read again after Update (LateUpdate, the player's camera): a close acts now (a cut: no fog at all; a blend: no
        /// unit held off, Covers no), an open lets the fade start and holds the covered units off and sets Covers at once; the fade's clock
        /// itself moves once a frame, in Update.</summary>
        void GateRecheck(bool atCamera)
        {
            float was = strength;
            string w = GateNow(out bool cut);
            gateClock.Recheck(w, cut, mat);
            if (atCamera && gateClock.Change < 0 && cut) GateCameraCuts++;               // a cut found only here (a scene begun after LateUpdate)
            GateAfter(cut, true);
            if (strength <= 0.001f) RestoreDither();
            if (!atCamera && strength != was) MinimapStep();                              // (inside the camera's drawing the frame's UI is built already: the minimap follows next Update)
        }

        /// <summary>The clock's outputs into the fog: strength (the pass, the dithering, the minimap cover), hidingNow (Covers) and the counters.</summary>
        void GateAfter(bool cut, bool late)
        {
            int c = gateClock.Change;
            if (c < 0) { GateCloses++; if (cut) GateCutCloses++; if (late) GateLateCloses++; GateClosedAt = Time.unscaledTime; }
            else if (c > 0) { GateOpens++; GateOpenedAt = Time.unscaledTime; }
            strength = gateClock.Strength;
            hidingNow = gateClock.Covers;
            if (!gateClock.HoldOff) { HiddenViews = 0; HiddenRenderers = 0; HiddenSkippedOutside = 0; }   // nothing is held off now (the checks read these)
        }

        void LateUpdate()
        {
            if (!decided || !wanted || mask == null || !gateClock.Seen) return;
            GateRecheck(false);                                                           // a scene a coroutine or an earlier LateUpdate began (the flyover ticks there)
        }

        /// <summary>BeginCamera (the hook in OzFogOfWar.Render.cs), on the player's camera: the gate read again (once a frame), so a scene
        /// begun later in the frame draws no cloud and holds no unit off on its first picture. True: the pass goes in (and the hiding, when
        /// <see cref="OzFowGateClock.HoldOff"/>); false: no cloud is drawn, and the units are held off here when HoldOff all the same (the fade
        /// in's first frames, strength 0.001 or under: BeginCamera returns before its own hiding then).</summary>
        bool GateCamera(Camera cam)
        {
            if (!gateClock.Seen) return false;
            int f = Time.frameCount;
            if (gateCamFrame != f) { gateCamFrame = f; GateRecheck(true); }
            if (!gateClock.Draws && gateClock.HoldOff) GateHoldOffNow(cam);             // holdoff change: the covered units held off from the first frame of play, the cloud drawn or not yet
            return gateClock.Draws;
        }

        /// <summary>The units in covered ground held off at the player's camera on a frame the cloud is not drawn yet (the fade in's first
        /// frames): the fog's own HideCovered, timed as BeginCamera times it; EndCamera gives every one of them back after the camera.</summary>
        void GateHoldOffNow(Camera cam)
        {
            long t = System.Diagnostics.Stopwatch.GetTimestamp();
            HideCovered(cam);
            long dt = System.Diagnostics.Stopwatch.GetTimestamp() - t; cHide += dt; lastHide += dt;
        }

        /// <summary>One line for logs and checks: the gate, the fade, the strength, Covers, the hiding.</summary>
        public static string GateLine() =>
            $"gate {(GameplayView ? "open (the player plays)" : "closed: " + GateReason + (GateCut ? " (hard cut)" : ""))}, fade {GateFade:0.000}, strength {Strength:0.000}, covers {(Hiding ? "on" : "off")}, units held off {(HoldingOff ? "on" : "off")}";
    }
}
