// OpenZ (GPL-3.0) - version 1.4 kit fog_gameplay_only: the FOG OF WAR's one gate as a plain rule and its frame clock (no Unity in it).
// View/OzFogOfWar.Gate.cs fills its inputs every frame and runs the clock; Editor/OzV14FogGameplayCount.cs and the kit's off-Unity harness
// run this same code.
using System;

namespace OpenZ
{
    /// <summary>
    /// What the game is doing now, as the fog's gate reads it (View/OzFogOfWar.Gate.cs <c>GateInputsNow</c> fills it every frame). Only the
    /// moments the game takes the camera or the screen for a scene, a scene's window, Commander Zod's window over the match, the match being
    /// decided and the computer playing the player's side are here. The pause menu, the in-match OPTIONS, the game speed, camera shakes, zooms, follow, free look,
    /// fly-tos, the minimap and the testing panel are gameplay: the gate does not read them at all (the counted check scans the reader for them).
    /// </summary>
    public struct OzFowGateInputs
    {
        /// <summary>Red's level-start briefing, also one started mid-match (ZodBriefing.BriefingActive: Briefing or Handing).</summary>
        public bool Briefing;
        /// <summary>Blue's level-start briefing (OzBlueCameo.BriefingActive: Briefing or Handing; the old gate missed its last frame).</summary>
        public bool BlueBriefing;
        /// <summary>A flight owns the camera (ZodFlyover.Live.Active: both briefings' flyovers, ZodCinePreview).</summary>
        public bool Flyover;
        /// <summary>The lounger (L16) or hot tub (L07) ending scene holds the screen (ZodBriefing.EndingHolding).</summary>
        public bool EndingHold;
        /// <summary>The editor's menu-flyby recorder (OzEpicFlyby.Busy; it exists in the editor only).</summary>
        public bool EpicFlyby;
        /// <summary>A capture camera (CaptureKit.CineActive: -cinematic, -showcase, F9, the menu flyby and level card takes, OzStillCam).</summary>
        public bool CaptureCine;
        /// <summary>The level has no RTS camera at all.</summary>
        public bool NoRtsCamera;
        /// <summary>The player's RtsCamera is switched off: every hard-cut takeover does that (the flyovers, the ending set, capture, the gate
        /// battery's free camera). The catch-all for any scene that takes the camera.</summary>
        public bool RtsCameraOff;
        /// <summary>The player's camera itself is switched off.</summary>
        public bool CameraOff;
        /// <summary>The player's camera draws nothing (culling mask 0): a scene camera has the whole screen (OzEvilStage.SetLive(on, onScreen: true)).</summary>
        public bool ScreenTaken;
        /// <summary>The game waits for a scene (ZGame switched off: the briefings, ZodCinePreview). The pause menu pauses another way and is gameplay.</summary>
        public bool SimWaits;
        /// <summary>The match is decided (PlayerInput.MatchDecided: the ending's glide and orbit with the RTS camera locked, the strike, the
        /// celebration, the blue endings, the end screen).</summary>
        public bool MatchDecided;
        /// <summary>The computer plays the player's side now (CaptureKit's demo, F8): attract / demo.</summary>
        public bool Demo;
        /// <summary>A scene's own window, open or still folding: always a scene with the fade, whatever <see cref="OzFowGate.CornerWindowKeepsFog"/>
        /// says. A blue scene's window (OzBlueCameo.SceneShowing, or its comms window still on screen: that window shows only the blue scenes,
        /// the briefing, the nuke scene, the endings) and Commander Zod's briefing window still folding after his briefing (<see cref="OzFowWindowTail"/>).</summary>
        public bool SceneWindow;
        /// <summary>Commander Zod's comms window over the running match (ZodBriefing.CommsVisible): his idle barb, any line in his in-game
        /// window. The player keeps the camera, the screen and the controls: gameplay by default (<see cref="OzFowGate.CornerWindowKeepsFogDefault"/>).</summary>
        public bool CornerWindow;
    }

    /// <summary>
    /// The fog of war is on screen only while the player plays, the normal RTS camera under his control in a running match.
    /// <list type="bullet">
    /// <item><see cref="Decide"/>: why the fog is not on screen now (null: it is) and whether that moment came in with a hard cut.</item>
    /// <item><see cref="FadeStep"/> and <see cref="Smootherstep"/>: the fade, <see cref="FadeSeconds"/> each way on smootherstep in real time
    /// (a frame longer than <see cref="MaxStep"/> counts as <see cref="MaxStep"/>, so a hitch is a short ramp, never a jump); 0 at once on a
    /// hard cut; covered at once on a match's first frame when play starts with no scene before it.</item>
    /// <item><see cref="CoversAt"/>: <c>OzFogOfWar.Covers</c> (the hover, the click, the attack orders, the health bars, the badges, the crate
    /// markers) answers at the same moment the units are held off: whenever the player plays and the fog can be drawn, whatever the fade
    /// (covers change, 5 Nov: no bar, badge or marker over a hidden enemy and no hover, click or attack order on one in the fade in).</item>
    /// <item><see cref="HoldsOff"/>: the units in covered ground are held off (not drawn) whenever the player plays, from the first frame of
    /// play whatever the fade (holdoff change, 5 Nov: no covered enemy drawn under the thin cloud of a fade in, an information leak, worse at
    /// 90 / 85 % cover); during a scene (the gate closed, cut or blend) they show from its first frame, as before the kit.</item>
    /// <item><see cref="OzFowGateClock"/>: the frame order of the live gate (Update, then the reads after it), run by the game and by the counted
    /// check; <see cref="OzFowWindowTail"/>: Zod's briefing window still folding after the briefing (a scene's window).</item>
    /// </list>
    /// </summary>
    public static class OzFowGate
    {
        /// <summary>The fade out when a scene starts and in when the player has the camera again (s, real time, smootherstep).</summary>
        public const float FadeSeconds = 0.4f;
        /// <summary>
        /// The fade IN when the player gets the camera takes this long (s, real time, same smootherstep);
        /// the fade out stays <see cref="FadeSeconds"/>. Gameplay is not touched: Covers and the hold-off follow the gate, not the fade.</summary>
        public const float FadeInSeconds = 1.6f;
        /// <summary>The longest frame the fade's clock takes in one step (s): a hitch (the hand-over frame, a capture) moves the fade by at
        /// most 1/8 of its way, so the ease stays a ramp. The fog's own clock FowDt caps a frame at 0.25 s.</summary>
        public const float MaxStep = 0.05f;

        /// <summary>The kit's default for <see cref="CornerWindowKeepsFog"/>: true (fix round 2). Commander Zod's comms window over the running
        /// match (his idle barb, any line in his in-game window) takes neither the camera nor the screen: the player keeps both and his controls,
        /// so it is gameplay (the lead's DECISION 1 test and DECISION 2; the owner: the fog during gameplay). A fade there would open the whole
        /// map for the barb's ~5 s: the cloud, the minimap cover and Covers (hover, click, attack orders, bars) all follow the gate. A scene's
        /// own window (<see cref="OzFowGateInputs.SceneWindow"/>: the blue scenes, the briefing's window folding) is a scene whatever this says.
        /// false: Zod's window fades the fog out and back in. To flip it, change this constant (the statics are reset to it at every Play and
        /// exe start).</summary>
        public const bool CornerWindowKeepsFogDefault = true;
        /// <summary>Commander Zod's comms window over the running match keeps the fog (true, <see cref="CornerWindowKeepsFogDefault"/>) or is a
        /// scene with a 0.4 s fade out and back in (false). Checks may set it for a run; View/OzFogOfWar.Gate.cs resets it to the default.</summary>
        public static bool CornerWindowKeepsFog = CornerWindowKeepsFogDefault;

        /// <summary>Why the fog is not on screen now (null: the player plays and it is). <paramref name="cut"/>: the moment took the camera or
        /// the screen with a hard cut, so the fog is gone on its first frame; otherwise (the match decided, the demo, a scene's window, Zod's
        /// window with the switch false) it fades out.</summary>
        public static string Decide(in OzFowGateInputs s, out bool cut)
        {
            cut = true;                                                                     // the game holds the camera or the screen
            if (s.Briefing) return "the briefing";
            if (s.BlueBriefing) return "the blue briefing";
            if (s.Flyover) return "a flyover";
            if (s.EndingHold) return "the ending";
            if (s.EpicFlyby) return "a flyby recording";
            if (s.CaptureCine) return "a capture camera";
            if (s.NoRtsCamera) return "the game holds the camera (no RTS camera)";
            if (s.RtsCameraOff) return "the game holds the camera (the RTS camera is off)";
            if (s.CameraOff) return "the game holds the camera (the player's camera is off)";
            if (s.ScreenTaken) return "a scene camera has the screen";
            if (s.SimWaits) return "the game waits for a scene";
            cut = false;                                                                    // moments that blend in: the fade
            if (s.MatchDecided) return "the match is decided (ending)";
            if (s.Demo) return "the computer plays the player's side (demo)";
            if (s.SceneWindow) return "a scene's window";
            if (s.CornerWindow && !CornerWindowKeepsFog) return "a transmission";
            return null;
        }

        /// <summary>The fade's clock one step on: 0 no fog .. 1 full fog, linear in time (the drawn strength is <see cref="Smootherstep"/> of
        /// it). first: the match's first read (play with no scene before it starts covered at once; a scene first has nothing to fade from);
        /// a cut: 0 at once; advance false: a second read in the same frame (LateUpdate, the player's camera) does not move the clock; a step
        /// is at most <see cref="MaxStep"/>.</summary>
        public static float FadeStep(float fade, bool first, bool open, bool cut, float dt, bool advance)
        {
            if (first) return open ? 1f : 0f;
            if (!open && cut) return 0f;
            fade = Clamp01(fade);
            if (!advance || !(dt > 0f)) return fade;                                        // (also a NaN or negative step: no move)
            float k = (dt < MaxStep ? dt : MaxStep) / (open ? FadeInSeconds : FadeSeconds);   // fog_fade_in_ease: a slower, eased fade in
            if (open) { fade += k; return fade > 1f - SnapEps ? 1f : fade; }                // (the last float bit: 24 steps at 60 fps land on 1 exactly)
            fade -= k; return fade < SnapEps ? 0f : fade;
        }

        /// <summary>A fade this close to its end is at its end (float rounding over the fade's steps only).</summary>
        const float SnapEps = 1e-5f;

        /// <summary>6x^5 - 15x^4 + 10x^3: 0 and 1 with zero speed and zero acceleration at both ends (no pop at the start or the end).</summary>
        public static float Smootherstep(float x)
        {
            x = Clamp01(x);
            return x * x * x * (x * (x * 6f - 15f) + 10f);
        }

        /// <summary><c>OzFogOfWar.Covers</c> answers (the hover, the click, the attack orders, the bars, the badges, the crate markers skip what
        /// the fog covers): whenever the gate is open (the player plays) and the fog can be drawn, the same moment the units are held off
        /// (<see cref="HoldsOff"/>), from the first frame of play whatever the fade (covers change, 5 Nov: before, from half the drawn cloud,
        /// so for ~0.2 s of every fade in the bars showed over hidden enemies and those could be hovered, clicked and attacked). No from a
        /// scene's first frame (cut or blend: the gate closed), as the units then show.</summary>
        public static bool CoversAt(bool open, bool canDraw) => open && canDraw;

        /// <summary>Units in covered ground held off on the player's camera: whenever the gate is open (the player plays) and the fog can be
        /// drawn, from the first frame of play whatever the fade (holdoff change, 5 Nov: the fade in never shows a covered enemy under a thin
        /// cloud). Off from a scene's first frame (cut or blend: the gate closed). On the fade in's first frames the cloud is not drawn yet
        /// (strength 0.001 or under): View/OzFogOfWar.Gate.cs GateCamera holds the units off itself then.</summary>
        public static bool HoldsOff(bool open, bool canDraw) => open && canDraw;

        static float Clamp01(float x) => float.IsNaN(x) ? 0f : x < 0f ? 0f : x > 1f ? 1f : x;
    }

    /// <summary>
    /// The live gate's frame order on the rule (View/OzFogOfWar.Gate.cs holds one and only copies its outputs into the fog; the counted check
    /// runs the same class): <see cref="Update"/> once a frame (the fog's Update: the clock moves), <see cref="Recheck"/> after it in the same
    /// frame (LateUpdate, the player's camera: a close acts at once, the clock does not move), then <see cref="Draws"/>, <see cref="Covers"/>
    /// and <see cref="HoldOff"/>.
    /// </summary>
    public sealed class OzFowGateClock
    {
        /// <summary>The fade's clock (0 .. 1, linear in time) and the drawn strength (its smootherstep).</summary>
        public float Fade, Strength;
        /// <summary>Read once at least; the gate open (the player plays); closed by a hard cut; the reason it is closed (null: open).</summary>
        public bool Seen, Open, Cut; public string Why = "not read yet";
        /// <summary><c>OzFogOfWar.Covers</c> answers; units in covered ground held off on the player's camera.</summary>
        public bool Covers, HoldOff;
        /// <summary>The last read: -1 the gate closed, +1 it opened, 0 no change; and whether it was the match's first read with the gate open
        /// (covered at once).</summary>
        public int Change; public bool FirstOpen;
        int frame = int.MinValue;

        /// <summary>The fog's Update: the gate read, the clock one step on (once a frame). drawnNow: the fog's strength as it stands (put to 0
        /// elsewhere, by the testing panel or the off path: the clock starts again from 0).</summary>
        public void Update(string why, bool cut, float dt, int f, bool canDraw, float drawnNow)
        {
            if (drawnNow <= 0f) Fade = 0f;
            bool open = why == null, first = !Seen;
            Fade = OzFowGate.FadeStep(Fade, first, open, cut, dt, f != frame);
            FirstOpen = first && open;
            Seen = true; frame = f;
            Apply(why, cut, canDraw);
        }

        /// <summary>A read after Update in the same frame (LateUpdate, the player's camera): a close acts now (a cut: no fog at all), the
        /// clock itself does not move.</summary>
        public void Recheck(string why, bool cut, bool canDraw)
        {
            Fade = OzFowGate.FadeStep(Fade, false, why == null, cut, 0f, false);
            FirstOpen = false;
            Apply(why, cut, canDraw);
        }

        void Apply(string why, bool cut, bool canDraw)
        {
            bool open = why == null;
            Change = Open && !open ? -1 : !Open && open ? 1 : 0;
            Open = open; Cut = cut && !open; Why = why;
            Strength = OzFowGate.Smootherstep(Fade);
            Covers = OzFowGate.CoversAt(open, canDraw);
            HoldOff = OzFowGate.HoldsOff(open, canDraw);
        }

        /// <summary>The cloud pass goes into the player's camera this frame (read, not closed by a cut, strength over 0.001).</summary>
        public bool Draws => Seen && !Cut && Strength > 0.001f;
    }

    /// <summary>
    /// A scene's window after the scene (fix round 2): Commander Zod's one comms window serves his briefing and his lines over the match, so
    /// the briefing's window, still folding when phase Play comes (the CRT fold, about 0.4 s), is told from a later line by this latch: set
    /// while the scene is on, kept while the window stays on screen, gone on the first read with the window closed. Reading it again in the
    /// same frame (LateUpdate, the player's camera) gives the same answer.
    /// </summary>
    public sealed class OzFowWindowTail
    {
        bool held;
        /// <summary>scene: the scene that owns the window is on (ZodBriefing.BriefingActive); window: the window is on screen, open or
        /// folding (ZodBriefing.CommsVisible). True while the window is still on screen after the scene.</summary>
        public bool Step(bool scene, bool window)
        {
            if (scene) held = true;
            else if (!window) held = false;
            return held && window && !scene;
        }
        /// <summary>The latch is set (the scene was on and its window has not closed since).</summary>
        public bool Held => held;
    }
}
