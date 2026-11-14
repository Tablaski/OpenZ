using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// OzEvilZodDashLook reads these every frame, so a value changed in Play shows at once.
    /// The asset holds the same numbers as the defaults below (the kit's proof compares them); with no asset the defaults are used. View only: no rule reads any of it.
    /// Colours marked HDR are linear values, used as they are (over 1 where they should bloom: the day bloom threshold is linear 1.63, the night one 0.60);
    /// sparkColor and dustColor are particle colours (sRGB, as every particle colour). Polish round 1 (critic, 3 Nov): day occlusion, narrower helix,
    /// overlapping afterimages, wider thin parts, a stronger break ripple, a zoom-aware smooth bend, a lower and less hood-like shell. Polish round 2 (the
    /// critic on the first game frames): a thin hot ground line that cools fast (and covers the sand by day), a clear shield with a thin blue rim, its fill
    /// and hex gathered at the rim, bright ends and visible bent flow streaks, afterimages that read as his outline, blue sparks that drift, a calmer break,
    /// the occlusion fading slower than the glow and the colours deepening with age, the core the longest and brightest line, more and lighter dust.
    /// </summary>
    public sealed class OzEvilZodDashKnobs : ScriptableObject
    {
        public const string ResPath = "OpenZ/FX/EvilZodDashKnobs";
        static OzEvilZodDashKnobs inst; static bool warned;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { inst = null; warned = false; }

        /// <summary>The asset in Resources, or the defaults when it is missing (one line in the log).</summary>
        public static OzEvilZodDashKnobs I
        {
            get
            {
                if (inst) return inst;
                inst = Resources.Load<OzEvilZodDashKnobs>(ResPath);
                if (!inst)
                {
                    inst = CreateInstance<OzEvilZodDashKnobs>(); inst.hideFlags = HideFlags.DontSave; inst.name = "EvilZodDashKnobs (defaults)";
                    if (!warned) { warned = true; Debug.LogWarning("[OpenZ EvilZod] Resources/" + ResPath + ".asset is missing: the dash look uses its default numbers"); }
                }
                return inst;
            }
        }

        [Header("Switches")]
        [Tooltip("The new dash look. Off = the look of round 2 (chest ribbon, eight quick ghosts); the start spark stays gone either way (row 72).")]
        public bool on = true;
        [Tooltip("The two thin red lines from his eyes during the dash (round 2).")]
        public bool eyeLines = true;
        [Tooltip("Seconds the red eye lines last while the new look is on (round 2: 0.4 s, about the whole dash, so the red ran through the blue braid and turned it pink at night). 0 = round 2's own.")]
        public float eyeLineLife = 0.25f;
        [Tooltip("Times every glow of the dash (trail, shield, ground glow): the one HDR knob.")]
        public float hdrMul = 1f;
        [Tooltip("Glow at night times this (night bloom is stronger: threshold 0.60 against 1.63 by day).")]
        public float nightGain = 0.45f;
        [Tooltip("Extra bloom intensity while the dash runs and half a second after, by day (only while he is on screen; with the look on, round 2's own +0.5 is off, so these two and calmBloom alone set it).")]
        public float bloomBoostDay = 0.6f;
        [Tooltip("Extra bloom intensity while the dash runs, at night.")]
        public float bloomBoostNight = 0.25f;

        [Header("Day colour: every glow also covers what is behind it (premultiplied)")]
        [Tooltip("By day a glow hides this share of what is behind it where it is full (0 = purely additive: blue over orange sand turns lavender / grey). Lerped to occludeNight by the night.")]
        public float occludeDay = 0.6f;
        [Tooltip("The same at night (the ground is dark: almost purely additive).")]
        public float occludeNight = 0.1f;
        [Tooltip("Times occludeDay / occludeNight for each part: the energy ribbons, the halo round the core, the core, the speed lines, the shield, the afterimages.")]
        public float occludeRibbon = 2f;
        public float occludeHalo = 1.4f;
        public float occludeCore = 0.6f;
        public float occludeLines = 1f;
        public float occludeShield = 1f;
        public float occludeAfterimage = 2.6f;
        [Tooltip("Times occludeDay / occludeNight for the ground trace's hot line (by day it covers the sand, so it reads blue, not white / lavender).")]
        public float occludeGround = 1.5f;
        [Tooltip("The occlusion fades slower than the glow: it goes with fade^this (1 = together, as round 1; 0.4 = the tail keeps covering the sand while its glow goes, so it stays blue instead of falling back to blue added on orange).")]
        public float occludeFadePow = 0.4f;

        [Header("Core streak (a bright white-blue line behind him)")]
        [Tooltip("Height of the trail's axis over his feet (m); he is 4.3 m tall.")]
        public float coreHeight = 2.3f;
        public float coreWidth = 0.38f;
        [Tooltip("Width at the old end of the core (m).")]
        public float coreTailWidth = 0.18f;
        [Tooltip("Seconds a piece of the core lives (the core is the longest line: longer than the ribbons).")]
        public float coreLife = 0.5f;
        [Tooltip("HDR gain of the core.")]
        public float coreIntensity = 6f;
        [ColorUsage(false, true)] public Color coreColor = new Color(0.78f, 0.9f, 1f, 1f);
        [Tooltip("A wide soft halo round the core (m), so it glows by day too.")]
        public float glowWidth = 1.4f;
        public float glowIntensity = 1f;
        public float glowLife = 0.45f;
        [ColorUsage(false, true)] public Color glowColor = new Color(0.08f, 0.3f, 1f, 1f);
        [Tooltip("The halo's colour at the end of its life (it deepens with age).")]
        [ColorUsage(false, true)] public Color glowColorOld = new Color(0.04f, 0.2f, 1f, 1f);
        [Tooltip("The trail fades in over this many metres behind his back.")]
        public float headGap = 0.45f;

        [Header("Energy ribbons (twisting round the core)")]
        public int ribbonCount = 3;
        public float ribbonWidth = 0.3f;
        [Tooltip("Helix radius far behind him (m).")]
        public float ribbonRadius = 0.62f;
        [Tooltip("Helix radius at his back, share of ribbonRadius.")]
        public float ribbonRadiusHead = 0.3f;
        [Tooltip("Metres behind him over which the helix opens to its full radius.")]
        public float ribbonSpread = 3f;
        [Tooltip("The helix keeps opening with age: radius x (1 + this x age in s).")]
        public float ribbonAgeGrow = 0.3f;
        [Tooltip("Turns per metre of trail.")]
        public float ribbonTwist = 0.12f;
        [Tooltip("The helix also turns in time (radians a second).")]
        public float ribbonSpin = 7f;
        [Tooltip("Height of the helix against its width (1 = round).")]
        public float ribbonUpScale = 0.75f;
        public float ribbonLife = 0.55f;
        public float ribbonIntensity = 3.8f;
        [Tooltip("0 = soft line, 1 = a band with bright edges.")]
        public float ribbonEdge = 0.65f;
        [Tooltip("How much a ribbon narrows when it turns edge-on (0 = not at all, 1 = to nothing).")]
        public float ribbonFlat = 0.55f;
        [ColorUsage(false, true)] public Color ribbonColor = new Color(0.08f, 0.26f, 1f, 1f);
        [Tooltip("A ribbon's colour at the end of its life (it deepens with age, so the tail stays blue on the sand).")]
        [ColorUsage(false, true)] public Color ribbonColorOld = new Color(0.04f, 0.2f, 1f, 1f);

        [Header("Speed lines (thin short streaks round the core)")]
        public int lineCount = 5;
        public float lineWidth = 0.11f;
        public float lineRadius = 0.6f;
        public float lineLife = 0.35f;
        public float lineIntensity = 2f;
        [ColorUsage(false, true)] public Color lineColor = new Color(0.35f, 0.6f, 1f, 1f);

        [Header("Trail texture (shader)")]
        [Tooltip("0 = smooth, 1 = broken into streaks along the trail.")]
        public float streakAmt = 0.55f;
        [Tooltip("Streaks per metre.")]
        public float streakFreq = 1.4f;
        [Tooltip("How fast the streaks run along the trail.")]
        public float streakSpeed = 7f;
        [Tooltip("Energy flicker of trail and shield (CALM SCREEN uses calmFlicker).")]
        public float flicker = 0.3f;
        [Tooltip("A strip is never thinner on screen than this many pixels: far out a thin line widens and dims by as much (same light), so it never crawls or vanishes at the RTS camera.")]
        public float minStripPx = 1.5f;
        [Tooltip("Particles and afterimages are made while his feet are within this share of the screen outside its edges (any game camera drawing to the screen).")]
        public float screenMargin = 0.1f;

        [Header("Sparks along the trail")]
        public float sparksPerMetre = 2.4f;
        [Tooltip("Sparks fly out from the ribbons at this speed (m/s); they drift back along the trail at sparkDrift and float up at sparkLift against their gravity, so they stream along the ribbons instead of falling.")]
        public float sparkSpeed = 1.2f;
        public float sparkDrift = 2.2f;
        public float sparkLift = 1.2f;
        public float sparkLife = 0.35f;
        public float sparkSize = 0.05f;
        [Tooltip("Soft glowing motes left along the trail.")]
        public float motesPerMetre = 1f;
        public float moteSize = 0.3f;
        public float moteLife = 0.45f;
        [Tooltip("Sparks and motes (particles: an ordinary sRGB colour, as every particle colour of the game).")]
        public Color sparkColor = new Color(0.3f, 0.55f, 1f, 1f);

        [Header("Afterimages (the motion blur: copies of his body along the path)")]
        [Tooltip("Afterimages alive at once (the code caps it at 12; each is 3 draws).")]
        public int afterimageCount = 5;
        [Tooltip("Metres between two afterimages: about his body depth (1.2 m) the newest reads as his outline and the older ones fall away into a streak; much less = they stack into a block.")]
        public float afterimageSpacing = 1.1f;
        [Tooltip("Most afterimages laid in one frame (a slow frame lays the missing ones along its move, with his pose of that frame).")]
        public int afterimageMaxPerFrame = 3;
        public float afterimageLife = 0.3f;
        public float afterimageAlpha = 0.28f;
        [Tooltip("Fade curve: 1 linear, more = fades faster at first.")]
        public float afterimageFadePow = 2.2f;
        [Tooltip("Edge power of the ghost shader: more = only the thin outline glows, less = a wider continuous outline.")]
        public float afterimagePower = 1.6f;
        [Tooltip("The glow on every pixel of the body inside the outline (round 1 had a fixed 0.25: copies 0.85 m apart stacked into a speckled slab).")]
        public float afterimageBody = 0.04f;
        [ColorUsage(false, true)] public Color afterimageNew = new Color(0.12f, 0.45f, 2.6f, 1f);
        [ColorUsage(false, true)] public Color afterimageOld = new Color(0.06f, 0.18f, 1.2f, 1f);

        [Header("Ground trace (a glowing, then scorched streak under his path)")]
        public bool groundOn = true;
        public float groundWidth = 1.2f;
        [Tooltip("Metres between two points of the trace.")]
        public float groundStep = 0.45f;
        [Tooltip("HDR gain of its blue hot line (just behind his feet; the core streak stays the brightest line).")]
        public float groundGlow = 1f;
        [Tooltip("Seconds the hot line takes to cool (then only the scorch is left).")]
        public float groundGlowTime = 0.4f;
        [Tooltip("Sharpness of the hot line across the trace: 14 = a wide soft glow (round 1), 30-40 = a thin hot line.")]
        public float groundGlowSharp = 35f;
        [Tooltip("0 = a steady line (round 1), 1 = broken into embers along the trace.")]
        public float groundGlowBreak = 0.7f;
        [Tooltip("Darkening of the ground (0..1).")]
        public float groundScorch = 0.4f;
        [Tooltip("Seconds the scorch takes to fade.")]
        public float groundFadeTime = 4.5f;
        [Tooltip("The start of the trace is this much wider (the push-off).")]
        public float groundStartFlare = 1.6f;
        public float groundLift = 0.03f;
        [ColorUsage(false, true)] public Color groundColor = new Color(0.15f, 0.45f, 1f, 1f);
        [ColorUsage(false, true)] public Color groundScorchColor = new Color(0.06f, 0.05f, 0.045f, 1f);

        [Header("Dust")]
        [Tooltip("Times every dust puff of the new look.")]
        public float dustAmount = 1f;
        [Tooltip("Puffs kicked back at the start.")]
        public int dustStart = 16;
        public float dustStartSpeed = 6f;
        [Tooltip("Metres between two puffs from his feet.")]
        public float footDustEvery = 0.6f;
        public float footDustSize = 1f;
        public float dustAlpha = 0.5f;
        [Tooltip("The new look's dust colour (a particle colour; light sand; ash on level 7, as all dust there).")]
        public Color dustColor = new Color(0.8f, 0.7f, 0.56f, 1f);
        [Tooltip("Metres between two faint blue wisps at his feet.")]
        public float wispEvery = 1.6f;
        public float wispAlpha = 0.35f;

        [Header("Front half-shield")]
        public bool shieldOn = true;
        [Tooltip("Radius of the shell (m), from a point at shieldHeight over his feet.")]
        public float shieldRadius = 2.5f;
        [Tooltip("Width of the arc (degrees).")]
        public float shieldAngle = 140f;
        [Tooltip("Lowest and highest edge of the shell (degrees over the horizontal through its centre).")]
        public float shieldElevMin = -40f;
        public float shieldElevMax = 40f;
        public float shieldHeight = 2.1f;
        [Tooltip("The shell's centre this far ahead of his feet (m, minus = behind).")]
        public float shieldForward = 0.15f;
        [Tooltip("Height of the shell against its radius.")]
        public float shieldSquash = 1f;
        [Tooltip("The bright rim where the shell turns away from the camera.")]
        public float shieldRim = 3f;
        [Tooltip("More = a thinner rim.")]
        public float shieldRimPow = 4.5f;
        [Tooltip("The rim is never thinner on screen than this many pixels (then wider and dimmer by as much): no shimmer at 70 m.")]
        public float shieldRimMinPx = 1.5f;
        [Tooltip("Brighter rim near both ends of the arc (his picture 2's bright brackets): extra rim brightness there, and the share of the arc it covers.")]
        public float shieldBracket = 1.2f;
        public float shieldBracketWidth = 0.14f;
        [Tooltip("The faint face of the shell.")]
        public float shieldFill = 0.04f;
        [Tooltip("The face and the hex gather at the rim: 0 = even over the whole face (a glass bubble), 1 = only near the rim (the middle clear, as his picture 2).")]
        public float shieldFillToRim = 0.8f;
        [Tooltip("Face and hex at night times this (night bloom is stronger).")]
        public float shieldNightFill = 0.5f;
        [Tooltip("Hex pattern strength.")]
        public float shieldHex = 0.1f;
        [Tooltip("Hex cell size (m).")]
        public float shieldHexSize = 0.34f;
        [Tooltip("The hex fades out where a cell is under this many pixels on screen (at 70 m a cell is about 4 px: it would blur into a tint).")]
        public float shieldHexMinPx = 8f;
        [Tooltip("Soft ends of the arc (share of it).")]
        public float shieldEdgeSoft = 0.18f;
        public float shieldIntensity = 1f;
        [ColorUsage(false, true)] public Color shieldColor = new Color(0.05f, 0.25f, 1f, 1f);
        [Tooltip("How far it shows during the windup (0..1).")]
        public float shieldWindup = 0.3f;
        [Tooltip("Seconds to come up, to go away.")]
        public float shieldIn = 0.06f;
        public float shieldOut = 0.2f;
        [Tooltip("While he brakes it fades with his speed down to this.")]
        public float shieldBrakeFloor = 0.4f;
        [Tooltip("The break at the stop: seconds, growth of the shell, brightness of the ripple, sparks.")]
        public float shieldBreakTime = 0.32f;
        public float shieldBreakGrow = 0.22f;
        public float shieldRingGain = 5f;
        [Tooltip("The break's ripple is at least this bright (share of a full shield), however far the shield faded in the brake: the stop's ripple has its own sound and must show by day.")]
        public float shieldBreakMin = 0.9f;
        public int shieldBreakSparks = 8;
        [Tooltip("The break's sparks: speed (m/s, times 0.6-1.4) and size (share of sparkSize).")]
        public float shieldBreakSparkSpeed = 2.5f;
        public float shieldBreakSparkSize = 0.8f;
        [Tooltip("The light-bending band just outside the shell: the picture behind it is pushed outwards.")]
        public bool bendOn = true;
        [Tooltip("true: the look's own smooth bend (bendCells soft waves along the arc); false: the game's heat distortion, wave mode (a fine grain made for the fort ring).")]
        public bool bendSmooth = true;
        [Tooltip("Push of the picture, share of the screen height, at the closest RTS zoom; further out it shrinks with the distance (bendRefDistance).")]
        public float bendStrength = 0.028f;
        [Tooltip("Full bend up to this camera distance (m; the closest RTS zoom is 42.7 m away, the start zoom 85 m): further out the push shrinks with the distance, so it keeps its size against the shield at every zoom.")]
        public float bendRefDistance = 42.7f;
        [Tooltip("Soft waves along the arc (the smooth bend).")]
        public float bendCells = 10f;
        public float bendBright = 0.05f;
        public float bendRadiusMul = 1.03f;
        [Tooltip("Bent flow streaks: thin bright lines just outside the arc that run from its front round its ends and sweep back past his sides (his picture 2), on each side (2 = 4 in all); 0 = none.")]
        public int bendStreaks = 2;
        public float bendStreakIntensity = 1.6f;
        [Tooltip("Width of a streak (m), the gap between two (share of the shield's radius), where it starts on the arc (share of the half arc) and how far it runs back past the arc's end (m).")]
        public float bendStreakWidth = 0.06f;
        public float bendStreakGap = 0.06f;
        public float bendStreakStart = 0.3f;
        public float bendStreakTail = 1.8f;
        [ColorUsage(false, true)] public Color bendStreakColor = new Color(0.2f, 0.5f, 1f, 1f);
        [Tooltip("The break's sound (an id of the sound table; empty = none), its volume and pitch.")]
        public string breakSound = "electro_fizzle";
        public float breakVolume = 0.6f;
        public float breakPitch = 1.15f;

        [Header("Lighter settings (GRAPHICS QUALITY LOW, LIGHTEST FOR SLOW PCS)")]
        public int liteRibbons = 2;
        public int liteLines = 2;
        public int liteAfterimages = 4;
        [Tooltip("Sparks, motes and dust times this.")]
        public float liteParticles = 0.5f;
        [Tooltip("Keep the light-bending band on the lighter settings.")]
        public bool liteBend = false;
        [Tooltip("Bent flow streaks on each side on the lighter settings.")]
        public int liteBendStreaks = 1;
        [Tooltip("MEDIUM counts as lighter too.")]
        public bool liteOnMedium = false;

        [Header("CALM SCREEN")]
        public float calmFlicker = 0f;
        public float calmBend = 0.5f;
        public float calmBloom = 0.5f;
        [Tooltip("The break's ripple flash under CALM SCREEN (share of shieldRingGain).")]
        public float calmRing = 0.3f;
    }
}
