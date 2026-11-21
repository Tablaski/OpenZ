using System.Collections.Generic;

namespace OpenZ
{
    /// <summary>
    /// One clear name and a one-line tooltip for every OPTIONS row, shared by the pause menu (HUD tooltip) and the
    /// main menu (its own tooltip plate). Format = the HUD tooltip's "TITLE|body|key".
    /// </summary>
    public static class OzSettingTips
    {
        /// <summary>The old "REDUCED MOTION" switch (OzMotion.Reduced, saved as before).</summary>
        public const string CalmLabel = "CALM MODE · NO FLASHES OR BLINKING";
        /// <summary>
        /// (= the live pathing bit for bit), saved in oz_path_v3, read by ZGame at the next match start.</summary>
        public const string PathV3Label = "SMOOTHER PATHING", PrefPathV3 = "oz_path_v3";
        public static bool PathV3 { get => UnityEngine.PlayerPrefs.GetInt(PrefPathV3, 1) == 1; set { UnityEngine.PlayerPrefs.SetInt(PrefPathV3, value ? 1 : 0); UnityEngine.PlayerPrefs.Save(); UnityEngine.Debug.Log($"[OpenZ Options] smoother pathing: {(value ? "ON" : "OFF (rollback: the old pathing)")} (oz_path_v3 {(value ? 1 : 0)}, saved, from the next match)"); } }
        /// <summary>
        /// panels and the message plates out; the MENU button, the minimap and everything opened on demand stay).</summary>
        public const string MinimalLabel = "MINIMAL UI", PrefMinimal = "oz_minimal_ui";
        static int minimal = -1;
        public static bool MinimalUi
        {
            get { if (minimal < 0) minimal = UnityEngine.PlayerPrefs.GetInt(PrefMinimal, 0) == 1 ? 1 : 0; return minimal == 1; }
            set { minimal = value ? 1 : 0; UnityEngine.PlayerPrefs.SetInt(PrefMinimal, minimal); UnityEngine.PlayerPrefs.Save(); UnityEngine.Debug.Log($"[OpenZ Options] minimal UI: {(value ? "ON" : "OFF")} (oz_minimal_ui {minimal}, saved, live)"); }
        }

        public const string Speed = "GAME SPEED|How fast the battle runs. 100 % is the original Z speed; lower gives you more time to think.|";
        public const string Music = "MUSIC VOLUME|Loudness of the soundtrack. 0 % mutes it.|";
        public const string Effects = "EFFECTS VOLUME|Weapons, explosions, engines, ambience and interface sounds. 50% is the standard mix, higher is louder.|";
        public const string Voice = "VOICE VOLUME|Rachel the announcer, Commander Zod and the robots' own voices.|";
        public const string Announcer = "ANNOUNCER VOLUME|Rachel the announcer and Commander Zod.|";
        public const string Robots = "ROBOT VOICES VOLUME|The robots' own voices: acknowledgements, orders, barks and jokes.|";
        public const string RobotChatterLabel = "ROBOT CHATTER";
        public const string RobotChatter = "ROBOT CHATTER|Full: every robot line (default). Less: longer gaps, no idle jokes or repeat-click lines. Minimal: only order acknowledgements and key events (under attack, captures, promotions, the ending).|";
        public const string Difficulty = "DIFFICULTY|How strong the CPU opponent plays. HARD: Z's build lists, and each fort starts with a missile cannon. MAYHEM: the same as HARD, under the fog of war: the map starts covered and enemies stay hidden in ground you have not explored. On MAYHEM the enemy also builds 10 % faster.|";   // 1.4 MAYHEM (+ DV175's last sentence)

        static readonly Dictionary<string, string> Tips = new Dictionary<string, string>
        {
            [OzBehaviorLibrary.TanksLabel] = "TANK BEHAVIOR|Choose OpenZ Native, Zod Classic or a named behavior preset saved in Unit Editor > BEHAVIOR. Applies to the next match, independently of robots. Multiplayer uses the host's profiles for both teams.|",
            [OzMapReadabilityOptions.FlagLabel] = OzMapReadabilityOptions.FlagTip,
            [OzMapReadabilityOptions.BorderLabel] = OzMapReadabilityOptions.BorderTip,
            [OzMapReadabilityOptions.RadarLabel] = OzMapReadabilityOptions.RadarTip,
            [OzMapReadabilityOptions.RadarSweepLabel] = OzMapReadabilityOptions.RadarSweepTip,
            [OzBehaviorLibrary.RobotsLabel] = "ROBOT BEHAVIOR|Choose native behavior or a named robot preset. Tune live in Unit Editor > BEHAVIOR. Applies to the next match, independently of tanks. Zod Classic still prevents engaged robots from dodging; Custom can override it.|",
            ["EDGE SCROLL"] = "EDGE SCROLL|The map scrolls when the mouse touches the edge of the screen. WASD, the arrow keys and the minimap always scroll.|",
            ["HEALTH BARS ON EVERY DAMAGED UNIT"] = "HEALTH BARS ON EVERY DAMAGED UNIT|On: every damaged unit shows its health bar. Off: only selected and hovered units show it. Enemy units show theirs only while you own a standing radar.|",   // 1.4 DV172
            ["CAMERA SHAKE ON EXPLOSIONS"] = "CAMERA SHAKE ON EXPLOSIONS|The camera shakes on big explosions and heavy hits.|",
            [CalmLabel] = "CALM MODE|For comfort: no blinking alerts or flashes, no camera shake, softer lightning and instant menu animations.|",
            ["UI SCALE"] = "UI SCALE|Size of the whole interface. Left arrow: smaller. Right arrow: bigger.|",
            ["HUD SCALE"] = "HUD SCALE|Size of the in-game interface. Left arrow: smaller. Right arrow: bigger.|",
            [OzNightRain.Label] = OzNightRain.Tip,                                          // NIGHT & RAIN: off = always day, no rain (OzNightRain.cs)
            ["YOUR TEAM COLOUR · VISUAL ONLY"] = "YOUR TEAM COLOUR|Play as red (like Z) or blue. Only the colours change, never the rules or the map. From the next match.|",
            ["HIT FLASH ON DAMAGED UNITS"] = "HIT FLASH|Units flash briefly when they take a hit.|",
            ["MOTION BLUR · FAST DEBRIS AND SHELLS"] = "MOTION BLUR|On (default): fast flying debris, blown-off turrets and wreck parts blur, shells, rockets and tracers leave a soft tail, sparks streak, and the smoke and fire of blasts are a little softer. The ground, buildings and standing units stay sharp, also while the camera moves or shakes.|",
            [PathV3Label] = "SMOOTHER PATHING|On (default): less back-and-forth in open ground, groups settle when close enough, units stuck over 5 s may step over wreck debris; bridges and bottlenecks keep the classic planner. Off = the classic pathing (rollback). Applies from the next match.|",
            ["MUSIC · SOUNDTRACK"] = "MUSIC|Switches the soundtrack on or off, in the menu and in battle.|",
            [OzSettings.FullLabel] = "FULLSCREEN|On: the game fills the screen, at the size the RESOLUTION row sets. Off: a window.|",
            [OzSettings.ResLabel] = "RESOLUTION|Lower it on a slower computer. NATIVE is the sharpest. Left click: one size larger. Right click (or Shift + click): one size smaller. After NATIVE comes the smallest size again. A window always fits on your screen.|",   // 1.5 issue 8
            [NightShadows.Label] = NightShadows.Tip,
            [CraterField.Label] = CraterField.Tip,
            [OzQuality.Label] = OzQuality.Tip,
            [OzQuality.FpsLabel] = OzQuality.FpsTip,
            [OzFpsCap.Label] = OzFpsCap.Tip,                                               // FRAME RATE LIMIT: 30 / 40 / 60 / UNLIMITED (VSYNC)
            [OzSmoothFrames.Label] = OzSmoothFrames.Tip,                                   // v1.3 item 25 (View/OzWeakPcOptions.cs)
            [OzSunSteps.Label] = OzSunSteps.Tip,
            [OzEndingDebris.Label] = OzEndingDebris.Tip,
            [OzGfxApi.Label] = OzGfxApi.Tip,
            [MechWalk.Label] = MechWalk.Tip,
            [OzOvertake.Label] = OzOvertake.Tip,                                           // DV105: fast units pass slow ones (OzOvertake.cs)
            [OzOverlapOption.Label] = OzOverlapOption.Tip,                                 // FRIENDLY UNITS OVERLAP (OzOverlapOption.cs)
            [OzVehicleSizeOption.Label] = OzVehicleSizeOption.Tip,                         // VEHICLE SIZE (OzVehicleSizeOption.cs)
            [OzRobotSizeOption.Label] = OzRobotSizeOption.Tip,                             // 1.5 R3.25 ROBOT SIZE (UI/OzRobotSizeOption.cs)
            [OzRangeRingsOption.Label] = OzRangeRingsOption.Tip,                           // 1.4 RANGE RINGS ON SELECTED UNITS: FADE (default) / ALWAYS (UI/OzRangeRingsOption.cs)
            [OzForkOptions.ChatLabel] = "CHAT SHORTCUT|Click and press the key that opens duel chat. ESC cancels. Chat is also available beside the command bar.|",
            [OzForceBarOption.Label] = OzForceBarOption.Tip,
            [OzBattleOptions.DamageLabel] = OzBattleOptions.DamageLabel + "|" + OzBattleOptions.DamageTip + "|",
            [OzBattleOptions.ShakeLabel] = OzBattleOptions.ShakeLabel + "|" + OzBattleOptions.ShakeTip + "|",
            [OzTouch.Label] = OzTouch.Tip,                                                  // touch and gamepad (OzTouch.cs)
            [OzTrackpadScroll.Label] = OzTrackpadScroll.Tip,                                // TRACKPAD SCROLL: ZOOM / MOVE MAP (OzTrackpadScroll.cs)
            [MinimalLabel] = "MINIMAL UI|Hide the permanent HUD panels; keep the menu button and the minimap. In a match the MINIMAL UI key (U, see CONTROLS) switches it.|",
            [OzTacticalPause.Label] = OzTacticalPause.Tip,                                  // 1.5 TACTICAL PAUSE, default OFF (View/OzTacticalPause.cs)
            [OzStrikeGroups.Label] = OzStrikeGroups.Tip,                                    // enemy strike groups (DV107)
            [OzLavaOption.Label] = OzLavaOption.Tip,                                        // level 7 lava (DV152, OzLavaOption.cs)
            [OzHighGroundOption.Label] = OzHighGroundOption.Tip,                            // high ground shoots farther (DV162, OzHighGroundOption.cs)
            // 1.4 MAYHEM: not registered, no OPTIONS row (the fog of war is MAYHEM's): [OzFogOfWarOption.Label] = OzFogOfWarOption.Tip,                                // 1.4 fog of war, default OFF (OzFogOfWarOption.cs)
            // 1.4 MAYHEM: not registered, no OPTIONS row: [OzFogLookOption.Label] = OzFogLookOption.Tip,                                  // 1.4 fog look: CLOUD SEA (default) / CLOUD BANKS / MIST LAYERS (OzFogLookOption.cs)
            [OzShuttleOption.Label] = OzShuttleOption.Tip,                                  // the level 16 shuttle (DV118)
            [OzShuttleOrdersOption.Label] = OzShuttleOrdersOption.Tip,                      // K256g: the shuttle's orders only to landable places (OzShuttleOrdersOption.cs)
            [OzFreeCam.Label] = OzFreeCam.Tip,                                             // FREE CAMERA (OzFreeCam.cs)
            [OzCamControls.SpeedLabel] = OzCamControls.SpeedTip,                                 // 1.4 SCROLL SPEED (UI/OzCamControls.cs)
            [OzCamControls.RightLabel] = OzCamControls.RightTip,                                 // 1.4 RIGHT-DRAG SCROLL (UI/OzCamControls.cs)
            [OzLensEdges.Label] = OzLensEdges.Tip,                                         // CHROMATIC ABERRATION · LENS EDGES, default OFF (View/OzLensEdges.cs)
            [OzMassLod.Label] = OzMassLod.Tip,                                             // 1.3 MASS BATTLES · LIGHTER WHEN SLOW, default OFF (View/OzMassLod.cs)
            [OzLightRows.LightestLabel] = OzLightRows.LightestTip,                         // 1.3 item 40: lighter rows for a slower processor (View/OzLightRows.cs)
            [OzLightRows.ShadowsLabel] = OzLightRows.ShadowsTip,
            [OzLightRows.AoLabel] = OzLightRows.AoTip,
            [OzWaterPlayCalm.ReflectionLabel] = OzWaterPlayCalm.ReflectionTip,
            [HeatGuardRuntime.LimitLabel] = HeatGuardRuntime.LimitTip,
            [OzQuality.LighterFogLabel] = OzQuality.LighterFogTip,
            [OzQuality.DisableFogLabel] = OzQuality.DisableFogTip,
            [OzQuality.MsaaLabel] = OzQuality.MsaaTip,
            [OzLightRows.TreesLabel] = OzLightRows.TreesTip,
            [OzLightRows.WrecksLabel] = OzLightRows.WrecksTip,
            [OzLowPicture.Label] = OzLowPicture.Tip,                                       // 1.5 issue 8: 3D PICTURE SHARP / SOFT / SOFTEST (View/OzLowPicture.cs)
        };

        // ---- options tabs: one table for both menus (the main menu's OPTIONS modal, the pause menu's OPTIONS panel)
        public const int TabMain = 0, TabGame = 1, TabGraphics = 2, TabAdvanced = 3;
        public static readonly string[] TabNames = { "MAIN", "GAME", "GRAPHICS", "ADVANCED" };
        /// <summary>Row label -> tab; the order here is the order of the rows on their tab.</summary>
        static readonly (string label, int tab)[] TabRows =
        {
            ("MUSIC · SOUNDTRACK", TabMain), ("ANNOUNCER CHATTER", TabMain), (RobotChatterLabel, TabMain), (OzSettings.FullLabel, TabMain), (OzSettings.ResLabel, TabMain), ("HUD SCALE", TabMain), ("UI SCALE", TabMain),
            ("EDGE SCROLL", TabGame), (OzFreeCam.Label, TabGame), (OzCamControls.SpeedLabel, TabGame), (OzCamControls.RightLabel, TabGame), ("HEALTH BARS ON EVERY DAMAGED UNIT", TabGame), (OzRangeRingsOption.Label, TabGame), (MinimalLabel, TabGame), (OzHudLayout.Label, TabGame), ("YOUR TEAM COLOUR · VISUAL ONLY", TabGame),
            ("CAMERA SHAKE ON EXPLOSIONS", TabGame), (CalmLabel, TabGame), ("HIT FLASH ON DAMAGED UNITS", TabGame), (OzOverlapOption.Label, TabGame), (OzVehicleSizeOption.Label, TabGame), (OzRobotSizeOption.Label, TabGame),   // 1.5 R3.25: ROBOT SIZE right under VEHICLE SIZE
            (OzTacticalPause.Label, TabGame),                                                // 1.5 TACTICAL PAUSE: the GAME tab's last row
            (OzForkOptions.ChatLabel, TabGame), (OzForceBarOption.Label, TabGame), (OzMapReadabilityOptions.FlagLabel, TabGame), (OzMapReadabilityOptions.BorderLabel, TabGame), (OzBehaviorLibrary.TanksLabel, TabGame), (OzBehaviorLibrary.RobotsLabel, TabGame), (OzBattleOptions.DamageLabel, TabGame), (OzBattleOptions.ShakeLabel, TabGame),

            (OzMapReadabilityOptions.RadarLabel, TabGame), (OzMapReadabilityOptions.RadarSweepLabel, TabGame),
            (OzQuality.Label, TabGraphics), (OzFpsCap.Label, TabGraphics), (NightShadows.Label, TabGraphics), (OzNightRain.Label, TabGraphics),
            ("MOTION BLUR · FAST DEBRIS AND SHELLS", TabGraphics), (OzLensEdges.Label, TabGraphics), (OzQuality.FpsLabel, TabGraphics),
            (OzSmoothFrames.Label, TabGraphics), (OzSunSteps.Label, TabGraphics), (OzEndingDebris.Label, TabGraphics), (OzGfxApi.Label, TabGraphics), (OzMassLod.Label, TabGraphics),   // v1.3 item 25: rows for slower computers; ue_mass_battles: MASS BATTLES last
            (OzLightRows.LightestLabel, TabGraphics), (OzLightRows.ShadowsLabel, TabGraphics), (OzLightRows.AoLabel, TabGraphics), (OzLightRows.TreesLabel, TabGraphics), (OzLightRows.WrecksLabel, TabGraphics),   // 1.3 item 40: after MASS BATTLES
            (OzLowPicture.Label, TabGraphics),                                               // 1.5 issue 8: 3D PICTURE, the GRAPHICS tab's last row (a LIGHTEST FOR SLOW PCS row, after WRECKS AND DEBRIS)
            (OzWaterPlayCalm.ReflectionLabel, TabGraphics),
            (HeatGuardRuntime.LimitLabel, TabGraphics),
            (OzQuality.LighterFogLabel, TabGraphics),
            (OzQuality.DisableFogLabel, TabGraphics),
            (OzQuality.MsaaLabel, TabGraphics),
            (OzStrikeGroups.Label, TabAdvanced), (OzTouch.Label, TabAdvanced), (OzTrackpadScroll.Label, TabAdvanced), (PathV3Label, TabAdvanced), (OzOvertake.Label, TabAdvanced), (CraterField.Label, TabAdvanced), (MechWalk.Label, TabAdvanced), ("PLAY INTRO · ON GAME START", TabAdvanced),
        };
        static int TabRow(string label) { for (int i = 0; i < TabRows.Length; i++) if (TabRows[i].label == label) return i; return -1; }
        /// <summary>The options tab of a row label. A label that is not in the table shows on ADVANCED, so a new row is never lost.</summary>
        public static int TabOf(string label) { int i = TabRow(label); return i >= 0 ? TabRows[i].tab : TabAdvanced; }
        /// <summary>The place of a row on its tab (rows that are not in the table come last, in the order they were built).</summary>
        public static int TabOrder(string label) { int i = TabRow(label); return i >= 0 ? i : int.MaxValue; }

        /// <summary>The tooltip for an option row label (null when it has none).</summary>
        public static string For(string label) => label != null && Tips.TryGetValue(label, out var t) ? t : null;

        /// <summary>1.3 item 40 (review): a tab packed tighter than its rows were laid out for (GRAPHICS with 17 rows: plates 24 tall, 28 apart)
        /// gets each row's contents (label, value, switch) moved up to the plate's middle. They are laid out for an OzLayout.OptToggleH (44)
        /// row (the label's middle 22 from the top, the 18-unit switch 13-31), so on a 24-unit plate they sat on its lower edge and the switch
        /// reached into the next row. A plate of 31 units or more (every tab of 13 rows or fewer) is left exactly as it is. Safe to call again.</summary>
        public static void CentreRow(HudButton row)
        {
            if (row == null || !row.Rt || !row.Content) return;
            float h = row.Rt.sizeDelta.y;                                                                    // the plate's height (OzRect Place sets it)
            if (h >= OzLayout.OptToggleH - 13f) return;                                                      // the switch still fits: nothing moves
            var lab = row.Content.Find("Label") as UnityEngine.RectTransform; if (!lab) return;
            float mid = -lab.anchoredPosition.y + lab.sizeDelta.y * 0.5f;                                    // OzText.At (mid): the label's middle from the plate's top
            float d = h * 0.5f - mid; if (UnityEngine.Mathf.Abs(d) < 0.01f) return;                          // already in the middle
            foreach (UnityEngine.Transform c in row.Content) if (c is UnityEngine.RectTransform r) r.anchoredPosition += new UnityEngine.Vector2(0f, -d);
        }
    }
}
