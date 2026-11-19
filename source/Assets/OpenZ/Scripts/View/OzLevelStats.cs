using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// deviation DV142 (Sim/ZWorld.CustomStats.cs): UNIT STATS OPENZ | CUSTOM for the levels.
    /// <list type="bullet">
    /// <item>The profile is the Unit editor's SAVED Custom stats (the Unit editor's contract, View/OzUeProfile.cs: PlayerPrefs
    /// <see cref="ProfileKey"/> "unit.value=number;..." of every value a Custom set can change, in the format of the editor's own working
    /// copy, with <see cref="ProfileNameKey"/> and <see cref="ProfileBaseKey"/>; written only by a successful SAVE in any form, removed only
    /// by RESET STATS; never by the editor's edits or its working copy oz_ue_custom). A level reads it, never a file (the file
    /// Documents/OpenZ/UnitEditor/openz_custom_stats.json is for sharing and for the editor's LOAD). The keys are read here by name, so this
    /// works whether the Unit editor's kit is in the build or not (without it nothing writes them: no selector, OPENZ).</item>
    /// <item>A profile "exists" when its text can be read and at least one value differs from OpenZ's own (a profile saved with every value
    /// OpenZ's own would play as OPENZ: there is nothing to choose). Only then the battlefield screen shows the selector
    /// (UI/OpenZMainMenu.UnitStats.cs); without one the screen is exactly as before and the choice is set back to OPENZ.</item>
    /// <item>The choice is remembered (<see cref="Pref"/>: 0 OPENZ, 1 CUSTOM). Only the player's own DEPLOY arms it (<see cref="Arm"/>,
    /// as PLAY AS BLUE), keeping the profile's text as it was at that moment; RESTART / NEXT keep it, the main menu ends it. A level
    /// opened any other way (the editor's Play on a level scene, a proof or the soak calling the menu's Deploy, the frame-rate bench, a bot
    /// match, the Unit editor's playground) plays OpenZ's own numbers. Not offered in 'Zod exact'.</item>
    /// <item>View/ZGame.cs Awake, before the world is built: the DV142 switch, then <see cref="AtLevelStart"/>: the text read and checked
    /// value by value (View/OzLevelStatsPlan.cs: unknown units / values, fixed values and non-numbers left out, numbers kept inside the
    /// Unit editor's limits, each said in the log; OpenZ's own number for every value not given), the Unit editor's own plan of it, the
    /// rows handed to the Sim, the rule values set (put back when the level goes). A profile with no usable change falls back to OPENZ.</item>
    /// <item>In the match: a small CUSTOM STATS tag under the MENU button and the mission banner's foot say so (<see cref="OzLevelStatsLevel"/>,
    /// UI/OpenZHud.cs); the unit cards read the Sim's rows, so they show the Custom numbers; the end screen says
    /// <see cref="Unranked"/> and the high score table takes no Custom match (UI/OpenZEndScreen.Score.cs).</item>
    /// </list>
    /// </summary>
    public static class OzLevelStats
    {
        public const string Pref = "oz_unit_stats";
        public const string Label = "UNIT STATS", OpenZWord = "OPENZ", CustomWord = "CUSTOM";
        public const string TipTitle = "UNIT STATS · KEY ";
        /// <summary>The tooltip's first lines (the player's profile and the table line follow; UI/OpenZMainMenu.UnitStats.cs).</summary>
        public const string TipLead = "<color=" + CyanHex + ">CUSTOM</color> plays this level with the unit stats\nyou saved in the Unit editor, for both teams.\n<color=#F2B84B>OPENZ</color> is the game's own numbers.";
        public const string TipTable = "Custom matches are not entered in the high scores.";
        /// <summary>The in-match tag and the mission banner's words.</summary>
        public const string Tag = "CUSTOM STATS";
        /// <summary>The end screen's score strip for a Custom match.</summary>
        public const string Unranked = "CUSTOM STATS · NOT IN THE TABLE";
        /// <summary>The Unit editor's colour of the Custom set (View/OzUeSets.cs ColCustom), so the word looks the same in both places.</summary>
        public const string CyanHex = "#6FD3E8";
        public static readonly Color Cyan = new Color32(0x6F, 0xD3, 0xE8, 255);
        const string Log = "[OpenZ] unit stats";

        // ---------------------------------------------------------------- the choice
        /// <summary>The battlefield screen's choice: CUSTOM (saved at once; OPENZ when nothing is saved yet).</summary>
        public static bool Box
        {
            get => PlayerPrefs.GetInt(Pref, 0) == 1;
            set { PlayerPrefs.SetInt(Pref, value ? 1 : 0); PlayerPrefs.Save(); }
        }
        /// <summary>Offered outside 'Zod exact' (there every number is Z's).</summary>
        public static bool Offered() => PlayerPrefs.GetInt("oz_zod_exact", 0) == 0;

        // ---------------------------------------------------------------- the profile on this computer
        /// <summary>The Unit editor's saved Custom profile (its View/OzUeProfile.cs KeyValues, KeyName, KeyBase).</summary>
        public const string ProfileKey = OzUeProfile.KeyValues, ProfileNameKey = OzUeProfile.KeyName, ProfileBaseKey = OzUeProfile.KeyBase;   // settings_tidy (1.5): the profile's keys have one home, View/OzUeProfile.cs (the same three strings)
        public static string SavedText => OzUeProfile.Text;                                                 // settings_tidy: the profile's one read (the same code it had here: GetString(key, ""), "" on an error)
        public static string SavedName => OzUeProfile.Name;                                                 // settings_tidy: the same ("" -> OzUeSets.DefaultName)
        public static int SavedBase => (int)OzUeProfile.Base;                                               // settings_tidy: the same default 0 (OPENZ); 0..2 kept, anything else 0, as OzLevelStatsPlan.Read already made of it
        public static string SettingsText { get { var st = Resources.Load<TextAsset>("OpenZ/default_settings"); return st ? st.text : ""; } }

        /// <summary>The rules a level start reads (UeRules.FromGame after View/ZGame.cs Awake has set the switches): the same switch lines
        /// from the same settings, for the battlefield screen, where no level has set them yet; the rule values as they stand (the game's:
        /// a Custom level puts its own back when it goes).</summary>
        public static UeRules RulesAsLevelStart()
        {
            bool z = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0;
            var r = UeRules.FromGame();
            r.sniperOn = z; r.pairOn = z;                                                                   // ZGame: SniperBalanceDeviation, RiflePairDeviation
            r.smallArmsOn = z;                                                                              // 1.5 DV212 (kit small_arms_balance_r8): ZGame sets ZWorld.SmallArmsDeviation the same
            ZWorld.MechHpDeviation = z;                                                                     // DV147: the mech's health as the level start parses it (ZGame sets the same; Sim/ZMechHp.cs)
            ZWorld.GatlingBuffDeviation = z;                                                                // DV170: the gatling's range and damage per shot as the level start parses them (ZGame sets the same; Sim/ZGatlingBuff.cs)
            ZWorld.MissileTurretBuildTimeDeviation = z;                                                     // 1.5 DV191 (kit missile_build_time): the missile cannon's build time as the level start parses it (ZGame sets the same; Sim/ZMissileBuildTime.cs)
            r.craneGun = z && PlayerPrefs.GetInt("oz_crane_gun", 1) == 1;                                  // CraneGunDeviation (DV31)
            r.apcCrew = z && PlayerPrefs.GetInt("oz_apc_crew", 1) == 1;                                    // ApcCrewDeviation (DV15)
            r.rocketOn = z && PlayerPrefs.GetInt("oz_rocket_speed", 1) == 1;                               // RocketSpeedDeviation (DV52)
            return r;
        }

        /// <summary>The saved profile, read and checked (the battlefield screen and DEPLOY).</summary>
        public static OzStatsProfile ReadProfile()
        {
            var r = RulesAsLevelStart();
            var p = OzLevelStatsPlan.Read(SavedText, SavedName, SavedBase, OzLevelStatsPlan.OpenZTable(SettingsText, r), r);
            Reads++;
            return p;
        }
        /// <summary>Checks: profile reads, DEPLOYs armed with CUSTOM.</summary>
        public static int Reads, Arms;

        // ---------------------------------------------------------------- DEPLOY arms it, the main menu ends it
        /// <summary>The level now loading or running plays the Custom profile (armed by DEPLOY; the match itself: <see cref="InUse"/>).</summary>
        public static bool Active { get; private set; }
        public static string ActiveLevel { get; private set; } = "";
        static string armedText, armedName; static int armedBase;
        /// <summary>The profile DEPLOY armed (checks, the log).</summary>
        public static OzStatsProfile ArmedProfile { get; private set; }
        /// <summary>The last level start (checks, the Play proof).</summary>
        public static OzLevelStatsPlan.Start LastStart { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSession() { Active = false; ActiveLevel = ""; armedText = armedName = null; ArmedProfile = null; LastStart = null; OzLevelStatsPlan.End(null); }

        /// <summary>DEPLOY (UI/OpenZMainMenu.cs), before the loading screen: the next level plays the Custom profile when the choice is
        /// CUSTOM, it is offered, a profile exists and the player deployed it himself (the DEPLOY button, ENTER, a double click on the
        /// level's card). A proof, the soak or a tool calling the menu's Deploy starts an OPENZ level whatever the choice.</summary>
        public static void Arm(string level, bool byPlayer)
        {
            Active = false; ActiveLevel = ""; armedText = armedName = null; ArmedProfile = null;
            if (!Box) { Debug.Log($"{Log} for {level}: OPENZ ({Pref} 0)"); return; }
            if (!Offered()) { Debug.Log($"{Log} for {level}: OPENZ ('Zod exact': CUSTOM is not offered)"); return; }
            if (!byPlayer) { Debug.Log($"{Log} for {level}: OPENZ (not the player's own DEPLOY: a proof or a test)"); return; }
            var p = ReadProfile();
            if (!p.Exists) { Debug.Log($"{Log} for {level}: OPENZ (CUSTOM chosen, but the Custom stats cannot be used: {p.Error ?? "no value differs from OpenZ's"})"); return; }
            armedText = p.Text; armedName = p.Name; armedBase = p.Base; ArmedProfile = p;
            Active = true; ActiveLevel = level; Arms++;
            Debug.Log($"{Log} for {level}: CUSTOM \"{p.Name}\" ({p.Changed} value(s) differ from OpenZ; {p.Read} read, {p.Clamped} kept inside the limits, {p.Ignored} left out)");
        }

        /// <summary>The main menu when it loads (and Play stopped in the editor): no level plays Custom stats any more; every rule value
        /// a Custom level set is put back. The choice stays as it is.</summary>
        public static void Disarm(string why)
        {
            if (Active) Debug.Log($"{Log}: CUSTOM ends ({why})");
            Active = false; ActiveLevel = ""; armedText = armedName = null; ArmedProfile = null;
            OzLevelStatsPlan.End(null);
        }

        /// <summary>View/ZGame.cs: this level start may play the Custom profile (DEPLOY armed it; never a bot match or the Unit editor).</summary>
        public static bool Wanted(string level, bool demo) => Active && !demo && Offered() && level != OzUnitEditor.LevelId && armedText != null;

        /// <summary>View/ZGame.cs Awake, right after the DV142 switch is set and before ZWorld.Create: the rows and rule values of this
        /// level (OzLevelStatsPlan.Begin), one log line; a Custom level gets its component (the HUD tag, the teardown).</summary>
        public static void AtLevelStart(ZGame g, string settingsText)
        {
            var s = OzLevelStatsPlan.Begin(ZWorld.CustomStatsDeviation ? armedText : null, armedName, armedBase, settingsText, g);
            LastStart = s;
            string lv = g ? g.level : "?";
            if (s.custom)
            {
                Debug.Log($"{Log} on {lv}: {s.line}");
                if (g && !g.GetComponent<OzLevelStatsLevel>()) g.gameObject.AddComponent<OzLevelStatsLevel>();
            }
            else if (s.profile != null) Debug.LogWarning($"{Log} on {lv}: {s.line}");                       // CUSTOM was armed, the text could not be used: said once, the level plays OpenZ's numbers
            else Debug.Log($"{Log} on {lv}: {s.line}");
        }

        // ---------------------------------------------------------------- in the match
        /// <summary>The running match was built with the Custom rows.</summary>
        public static bool InUse => ZGame.I && ZGame.I.World != null && ZGame.I.World.CustomStatsInUse != null;
        /// <summary>UI/OpenZEndScreen.Score.cs: a Custom match is not entered in the table (null = this kit has no say).</summary>
        public static string UnrankedWhy() => InUse ? Unranked : null;
        /// <summary>UI/OpenZHud.cs, the mission banner's foot: its rules words, and CUSTOM STATS in the Custom colour in a Custom match.</summary>
        public static string BannerRules(string rules) => InUse ? "<color=" + CyanHex + ">" + Tag + "</color>" : rules;
    }

    /// <summary>
    /// A Custom level's own part (added by <see cref="OzLevelStats.AtLevelStart"/>): the small CUSTOM STATS tag under the HUD's MENU button
    /// (a child of the button, so it moves with ARRANGE INTERFACE, slides in with the HUD and hides with it; under the FPS COUNTER chip or
    /// the touch keys while they stand there: KeepClear), and when the level goes the rule values it set are put back (only its own: a
    /// level built before this one was torn down keeps its values). View only.
    /// </summary>
    public sealed class OzLevelStatsLevel : MonoBehaviour
    {
        public static OzLevelStatsLevel I { get; private set; }
        /// <summary>The tag: as wide as the MENU button, 20 u tall, 6 u under it; its word at 11 u with a tighter tracking than a chip's 0.16, so
        /// CUSTOM STATS keeps 6 u on each side of the 112 u button's width.</summary>
        public const float TagGap = 6f, TagH = 20f, TagTracking = 0.1f;
        public RectTransform TagRt { get; private set; }
        ZGame game;

        void Awake() { I = this; game = GetComponent<ZGame>(); }

        void OnDestroy()
        {
            if (I == this) I = null;
            OzLevelStatsPlan.End(game);
        }

        void Update()
        {
            if (TagRt) { KeepClear(); return; }
            var hud = OpenZHud.I;
            if (!hud || !hud.Root) return;
            var menu = hud.Root.Find("MenuButton") as RectTransform;
            if (!menu) return;
            var sz = menu.sizeDelta;
            var chip = Hud.Chip(menu, "CustomStatsTag", new OzRect(0f, sz.y + TagGap, sz.x, TagH), OzLevelStats.Tag, OzUi.A(OzUi.GM950, 0.9f), OzLevelStats.Cyan, 10, TagTracking, OzUi.A(OzLevelStats.Cyan, 0.5f));
            TagRt = (RectTransform)chip.bg.transform.parent;
            Debug.Log($"[OpenZ] unit stats: the CUSTOM STATS tag under the MENU button ({sz.x:0} x {TagH:0} u, {TagGap:0} u under it)");
            KeepClear();
        }

        /// <summary>Under the MENU button stand, while they show, the FPS COUNTER chip (its own canvas at the screen's right edge,
        /// View/OzQuality.cs OzFpsChip) and the touch / gamepad keys (UI/OpenZHud.TouchKeys.cs). Where one of them would lie on the tag,
        /// the tag stands TagGap under it instead (at every UI SCALE: measured in the screen's pixels), and under MENU again when it goes.
        /// A MENU button arranged at the foot of the screen has the tag over it. Every frame, a few corners.</summary>
        void KeepClear()
        {
            var menu = TagRt.parent as RectTransform;
            if (!menu) return;
            float sf = Mathf.Abs(menu.lossyScale.y);                                                    // screen pixels per HUD unit (an overlay canvas)
            if (sf < 1e-4f) return;
            menu.GetWorldCorners(Corners);
            float x0 = Corners[0].x, x1 = Corners[2].x, top = Corners[1].y;                             // MENU's left, right and top edges in screen pixels, y up
            float y = menu.rect.height + TagGap;                                                         // the tag's top, HUD units under MENU's top edge
            var hud = OpenZHud.I;
            var touch = hud && hud.Root && hud.TouchKeysShown ? hud.Root.Find("TouchKeys") as RectTransform : null;
            for (int pass = 0; pass < 3; pass++)
            {
                bool moved = Under(FpsChipRt(), x0, x1, top, sf, ref y);
                moved |= Under(touch, x0, x1, top, sf, ref y);
                if (!moved) break;
            }
            if (top - (y + TagH) * sf < 0f) y = -(TagH + TagGap);                                       // it would leave the screen's foot: over MENU
            var at = TagRt.anchoredPosition;
            if (Mathf.Abs(at.y + y) > 0.01f) TagRt.anchoredPosition = new Vector2(at.x, -y);
        }

        /// <summary>The tag (its top y HUD units under MENU's top edge) meets the part r: y becomes TagGap under r's lower edge (true).</summary>
        bool Under(RectTransform r, float x0, float x1, float top, float sf, ref float y)
        {
            if (!r || !r.gameObject.activeInHierarchy) return false;
            r.GetWorldCorners(Corners);
            float rx0 = Corners[0].x, rx1 = Corners[2].x, rBottom = Corners[0].y, rTop = Corners[1].y;
            float tTop = top - y * sf, tBottom = tTop - TagH * sf;
            if (!(x0 < rx1 - 0.5f && rx0 < x1 - 0.5f && tBottom < rTop - 0.5f && rBottom < tTop - 0.5f)) return false;
            y = (top - rBottom) / sf + TagGap;
            return true;
        }

        /// <summary>The FPS COUNTER chip while it is drawn (null: the counter off or hidden); looked for twice a second until found.</summary>
        RectTransform FpsChipRt()
        {
            if (!OzQuality.FpsOn) return null;
            if (!fpsRt && Time.unscaledTime >= fpsLookAt)
            {
                fpsLookAt = Time.unscaledTime + 0.5f;
                var c = FindFirstObjectByType<OzFpsChip>();
                var cv = c ? c.transform.Find("OpenZ_FpsCanvas") : null;
                fpsCanvas = cv ? cv.GetComponent<Canvas>() : null;
                fpsRt = cv ? cv.Find("Root/FpsChip") as RectTransform : null;
            }
            return fpsRt && fpsCanvas && fpsCanvas.enabled ? fpsRt : null;
        }

        static readonly Vector3[] Corners = new Vector3[4];
        RectTransform fpsRt; Canvas fpsCanvas; float fpsLookAt;

        /// <summary>Play check: one line (the switch, the rows, the shuttle values, the rule values, the tag).</summary>
        public static string Status()
        {
            var g = ZGame.I; var W = g ? g.World : null;
            if (!g || W == null) return "no level";
            var c = W.CustomStatsInUse;
            var keys = new List<string>(); if (c != null) { keys.AddRange(c.Rows.Keys); keys.Sort(System.StringComparer.Ordinal); }
            return $"unit stats {(c != null ? "CUSTOM \"" + c.Name + "\"" : "OPENZ")} on {g.level}: switch {ZWorld.CustomStatsDeviation}, armed {OzLevelStats.Active} ({OzLevelStats.ActiveLevel}), "
                   + $"rows {(keys.Count > 0 ? string.Join(", ", keys) : "none")}, shuttle {(c != null ? c.ShuttleHp + " HP " + c.ShuttleSpeedPx + " px/s" : "the rule's")}, "
                   + $"rule values {(OzLevelStatsPlan.RulesHeld ? "the set's (held)" : "the game's")}, tag {(I && I.TagRt ? "shown" : "none")}";
        }
    }
}
