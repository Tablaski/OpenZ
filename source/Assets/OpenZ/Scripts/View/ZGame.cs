using System.Collections.Generic;
using System.Linq;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// Scene bootstrap: loads the Zod settings and the original map, runs the Zod simulation at its ~100 Hz server rate
    /// (fixed 10 ms ticks, game speed scaling) and binds every sim unit to the 3D object the level builder placed for it
    /// (name "type_x_y[_member]"). World mapping: x = px / 8, z = -py / 8 (1 tile = 16 px = 2 m).
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public partial class ZGame : MonoBehaviour
    {
        public static ZGame I { get; private set; }

        [Tooltip("L02 or L17")] public string level = "L02";
        public Team player = Team.Red;
        [Range(0.25f, 4f)] public float gameSpeed = 1f;
        public bool paused;
        public int seed = 1;
        [Tooltip("Zod CPU players (zbot.cpp). Z: the player is red, the CPU is blue; both on = bot vs bot")] public bool blueBot = true, redBot = false;
        [Tooltip("CPU strength vs the human: Normal = the Zod bot 1:1, Easy = OpenZ handicap. PlayerPrefs oz_difficulty (0/1) overrides; bot vs bot is always Normal")]
        public ZBotDifficulty difficulty = ZBotDifficulty.Normal;
        internal ZBotDifficulty SoloAi2026 { get; private set; }
        [Tooltip("Easy / Medium / Hard: CPU handicap, the player's unit unlocks and the L02 head start. Hard = the Zod CPU and build lists, plus a missile cannon on each fort")]
        public GameDifficulty gameDifficulty = GameDifficulty.Hard;
        /// <summary>
        /// Mayhem set in the inspector). View only: gameDifficulty stays Hard, so every rule, the computer, the shuttle box, the tech bonus
        /// and DV73 are HARD's; the fog of war is on (View/OzFogOfWar.cs WhyOff); the end screen and its table say MAYHEM.</summary>
        [System.NonSerialized] public bool mayhem;
        /// <summary>1.4 MAYHEM: a MAYHEM match that still plays HARD (a live change of the difficulty takes it out).</summary>
        public bool IsMayhem => mayhem && gameDifficulty == GameDifficulty.Hard;
        /// <summary>1.4 MAYHEM: the difficulty the player is shown (the end screen, the score table): MAYHEM in a MAYHEM match.</summary>
        public GameDifficulty ShownDifficulty => IsMayhem ? GameDifficulty.Mayhem : gameDifficulty;

        [Tooltip("Z_* unit prefabs for units created at run time (ejected crews, staged fights, factory output)")] public GameObject[] unitPrefabs;

        public ZWorld World { get; private set; }
        public readonly Dictionary<int, UnitView> Views = new Dictionary<int, UnitView>();
        public readonly Dictionary<int, GameObject> StaticViews = new Dictionary<int, GameObject>();   // rocks, huts, crates, buildings
        /// <summary>v1.3: the entries of Views whose UnitView is active and enabled, in the order Views walks them. A dead, boarded
        /// or removed unit's view is hidden (UnitView.Hide) and drops out, so the per-frame loops that skip hidden views anyway walk
        /// only these instead of every view made since the level started. Views stays the one table for id lookups. Rebuilt only
        /// after a view was added, shown, hidden or destroyed (UnitView.LiveVersion, Views.Count), and into a new list, so a loop that
        /// is still walking the old one is never changed under it. Read only.</summary>
        public List<KeyValuePair<int, UnitView>> LiveViews { get { RefreshLive(); return liveKv; } }
        /// <summary>The same views as LiveViews, without their ids.</summary>
        public List<UnitView> LiveViewValues { get { RefreshLive(); return liveV; } }
        List<KeyValuePair<int, UnitView>> liveKv = new List<KeyValuePair<int, UnitView>>();
        List<UnitView> liveV = new List<UnitView>();
        int liveVersion = -1, liveCount = -1;
        /// <summary>How often the live list was rebuilt (checks).</summary>
        public int LiveRebuilds { get; private set; }
        void RefreshLive()
        {
            if (liveVersion == UnitView.LiveVersion && liveCount == Views.Count) return;
            var kv = new List<KeyValuePair<int, UnitView>>(liveKv.Count + 8); var vs = new List<UnitView>(liveKv.Count + 8);
            foreach (var p in Views) if (p.Value && p.Value.isActiveAndEnabled) { kv.Add(p); vs.Add(p.Value); }
            liveKv = kv; liveV = vs; liveVersion = UnitView.LiveVersion; liveCount = Views.Count; LiveRebuilds++;
        }
        Collider ground;
        // share one layer; GroundY takes the topmost walkable surface, so units, rings and effects sit on the deck.
        public static int WalkLayer { get; private set; } = -1;
        int walkMask;
        double acc; bool worldLost;

        void Awake()
        {
            if (!OzResourcePackage.Compatible) { enabled = false; return; }
            I = this; ZRuleProf.On = true; ZRuleProf.Reset();   // b109: rule tick timer per part, log only (Sim/ZRuleProf.cs)
            Time.maximumDeltaTime = OzSmoothFrames.FrameStep;                                   // v1.3 SMOOTH FRAMES ON SLOW PCS (Options, default OFF = MaxFrameStep as before; View/OzWeakPcOptions.cs)
            // Every level starts at the warm sunrise, whatever time a proof left saved in the scene
            ZodShotList.EnsureFile();
            var dn = FindFirstObjectByType<DayNight>();
            if (dn) { dn.timeOfDay = DayNight.SunriseStart; DayNight.LevelStartTod = dn.timeOfDay; Debug.Log($"[OpenZ] day / night: level starts at sunrise (t {DayNight.SunriseStart:F3})"); }
            var st = Resources.Load<TextAsset>("OpenZ/default_settings");
            var map = Resources.Load<TextAsset>($"OpenZ/Maps/{level}_grid");
            // The demo used to force 4x here, and PlayerPrefs survive editor restarts, so one leftover demo flag made the whole game
            // run 4x. A faster demo only through the explicit pref oz_demo_speed.
            bool demo = PlayerPrefs.GetInt("oz_botmatch", 0) == 1 || CaptureKit.DemoRun;
            var skirmishMap = OzAiMapCatalog.Find(level);
            var localSkirmish = OpenZ.Multiplayer.DuelRuntime.I?.Menus.LocalSkirmishMatch(level);
            bool onlineSkirmish = OpenZ.Multiplayer.DuelRuntime.Active && OpenZ.Multiplayer.DuelRuntime.I.SkirmishRoom;
            bool skirmish = skirmishMap?.Starts >= 3 || localSkirmish != null || onlineSkirmish;
            if (skirmish)
            {
                demo = blueBot = redBot = false;
                player = localSkirmish == null ? Team.Red : (Team)(localSkirmish.HostPosition + 1);
            }
            else
            {
                if (demo) blueBot = redBot = true;
                OzPlayBlue.Sides(this, demo);
            }
            gameSpeed = StartSpeed(gameSpeed, demo, PlayerPrefs.GetFloat("oz_demo_speed", 1f));
            Debug.Log($"[OpenZ] time base: gameSpeed {gameSpeed} timeScale {Time.timeScale} tick {ZConst.TICK} s, bots red {redBot} blue {blueBot}, demo {demo}");
            if (PlayerPrefs.HasKey(OpenZDifficulty.Key)) { gameDifficulty = OpenZDifficulty.Level; difficulty = OpenZDifficulty.Bot(gameDifficulty); }   // menu choice (0 = Hard, 1 = Easy, 2 = Medium)
            mayhem = blueBot != redBot && (gameDifficulty == GameDifficulty.Mayhem || (PlayerPrefs.HasKey(OpenZDifficulty.Key) && OpenZDifficulty.MayhemChosen)); if (gameDifficulty == GameDifficulty.Mayhem) { gameDifficulty = GameDifficulty.Hard; difficulty = OpenZDifficulty.Bot(gameDifficulty); }   // 1.4 MAYHEM: HARD for every rule and the computer, the fog of war on (View/OzFogOfWar.cs WhyOff); only a human against the CPU (the fog's own test, WhyOff): a computer-only match or a scene with no CPU (the Unit editor) is never MAYHEM
            // OpenZ deviation DC2: drivers get sniped in real fights; 'Zod exact' mode (PlayerPrefs oz_zod_exact = 1) turns it off
            ZWorld.SnipeDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0;
            ZWorld.FortGunDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0 && PlayerPrefs.GetInt("oz_fortgun_sim", 1) == 1;   // DV7 fort gun
            ZWorld.ApcCrewDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0 && PlayerPrefs.GetInt("oz_apc_crew", 1) == 1;       // DV15 APC driver + passengers (apc_v1)
            ZWorld.DuelDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0 && PlayerPrefs.GetInt("oz_duel_sim", 1) == 1;       // DV9 vehicle duel manoeuvres
            ZWorld.VeteranDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0 && PlayerPrefs.GetInt("oz_veterans", 1) == 1;    // veterancy_v1 DV12 veteran crews
            ZWorld.ToughPrepareDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0 && PlayerPrefs.GetInt("oz_tough_prepare", 1) == 1;   // weapons_v2 DV11 Tough prepare time
            ZWorld.FormationDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0 && PlayerPrefs.GetInt("oz_vformation_sim", 1) == 1;   // DV24 squad V formation (los_clearance_v1)
            ZWorld.ClearanceDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0 && PlayerPrefs.GetInt("oz_clearance_sim", 1) == 1;   // DV22 building clearance (los_clearance_v1)
            ZWorld.LosDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0 && PlayerPrefs.GetInt("oz_los_sim", 1) == 1;               // DV23 line of sight for direct fire
            ZWorld.LowPitsDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0;                                                        // 1.5 DV205 (kit pits_no_block_r3): low pits, ravines, gorges and moats are no wall for sight or shots; off in 'Zod exact' as DV152 (Sim/ZWorld.LowPits.cs)
            ZWorld.FortMortarDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0 && PlayerPrefs.GetInt("oz_fort_mortar", 1) == 1;       // combat_nb_v1 DV25 fort mortar (NB27)
            ZWorld.BuildingReachDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0 && PlayerPrefs.GetInt("oz_building_reach", 1) == 1;   // combat_nb_v1 DV26 range to a building's walls (NB14)
            ZWorld.ExposedDriverDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0 && PlayerPrefs.GetInt("oz_exposed_drivers", 1) == 1;   // combat_nb_v1 DV27 exposed drivers, rear hits (NB11)
            ZWorld.ApproachDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0 && PlayerPrefs.GetInt("oz_attack_approach", 1) == 1;     // combat_nb_v1 DV28 attack approach (NB13)
            ZWorld.ArmourChipDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0 && PlayerPrefs.GetInt("oz_armour_chip", 0) == 1;       // combat_nb_v1 DV29 armour chip (NB5 proposal, default OFF)
            ZWorld.BridgeHealthDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0;                                                     // 1.5 DV207 (kit bridge_health_r3): bridges as weak as in Z (46546 hit points, Zod 83333); off in 'Zod exact' (Sim/ZWorld.BridgeHealth.cs)
            ZWorld.BridgeDeckAimDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0;                                                   // 1.5 DV210 (kit bridge_aim_further_r6): a shot at a bridge lands on its deck a few metres in, not at the entrance; off in 'Zod exact' (Sim/ZObject.BridgeDeckAim.cs)
            ZWorld.BigTankUpDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0;                                                       // 1.5 DV211 (kit bizon_mammoth_driver_up_r7): the Mammoth's and the Bizon's driver up in the hatch while they fight and for 8 s after; off in 'Zod exact' (Sim/ZObject.BigTankUp.cs)
            ZWorld.SmallArmsDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0; ZWorld.SmallArmsZ95Deviation = false;   // 1.5 DV212 (kit small_arms_balance_r8): grunts, psychos, the jeep's gun and the gatling hit robots on foot harder (off in 'Zod exact'); part 2 (the owner's Z idea: bullets barely scratch hulls) OFF until he says yes (Sim/ZObject.SmallArms.cs)
            ZWorld.NoTankDuelVsJeep = PlayerPrefs.GetInt("oz_tank_duel_jeep", 0) == 0;                                                  // combat_nb_v1 NB12: tanks stand and fire at a jeep (DV9)
            ZWorld.HeavyPairDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0 && PlayerPrefs.GetInt("oz_heavy_pair", 1) == 1;     // DV30 heavy tank twin barrels (vehicles_nb_v1)
            ZWorld.CraneGunDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0 && PlayerPrefs.GetInt("oz_crane_gun", 1) == 1;       // DV31 crane driver shoots back (vehicles_nb_v1)
            ZWorld.CraneSnipeDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0;                                                 // 1.2 items 32-34 DV141: the crane's driver can be sniped while he is up in the cab hatch, and stays inside while the crane works (Sim/ZObject.CraneSnipe.cs)
            ZWorld.CraneLandEndDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0;                                               // 1.5 DV206 (kit crane_deck_sound_r3): a crane repairs a bridge from its land end, never over the water; off in 'Zod exact' (Sim/ZObject.CraneLandEnd.cs)
            ZWorld.ApcSnipeDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0;                                                   // 1.2 item 69 DV143: the APC's lone driver can be sniped while he is up in the roof hatch (Sim/ZObject.ApcSnipe.cs)
            ZWorld.HarbourWrecksDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0 && PlayerPrefs.GetInt("oz_l17_wrecks", 1) == 1;   // l17_details_v1 DV39: L17 harbour wrecks block their tiles until destroyed
            ZWorld.L16PyramidsDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0;                                       // level 16 DV89: the pyramids are destroyable map items
            ZWorld.L16ShuttleDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0;                                        // level 16 DV95: the shuttle stands in the place of one hut
            ZWorld.ShuttlePlayableDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0; ZWorld.ShuttleOn = OzShuttleStart.RuleAtStart(level, gameDifficulty); ZWorld.ShuttleTeam = player;   // DV118: the level 16 shuttle is the player's transport; K347: the battlefield screen's SHUTTLE box (oz_start_shuttle, default on; level 16 on EASY / MEDIUM, none on HARD; UI/OzShuttleStart.cs), read at the level start (Sim/ZWorld.Shuttle.cs)
            ZWorld.ShuttleBigCrashDeviation = ZWorld.ShuttlePlayableDeviation; ZWorld.ShuttleBigCrashProofNext = false;   // 1.3 item 7 DV154: one crash landing in 3 is the BIG crash (a quarter of the full health, never under a tenth, never destroyed), with DV118 (off in 'Zod exact') (Sim/ZWorld.ShuttleBigCrash.cs)
            ZWorld.L18ShuttleDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0; ZWorld.L18StartShuttle = OzShuttleStart.L18StartAtStart(level, gameDifficulty);   // 1.5 DV200 (kit level18_rules): level 18's SHUTTLE box ship by the player's fort (EASY / MEDIUM, the box on) (Sim/ZWorld.L18Rules.cs, UI/OzShuttleStart.cs)
            ZWorld.L17ShuttlesDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0; ZWorld.L17StartShuttle = OzShuttleStart.L17StartAtStart(level, gameDifficulty);   // K444 DV134: level 17's EMPTY car-park shuttle (every difficulty) and the SHUTTLE box's one by the red fort (EASY / MEDIUM, the box on) (Sim/ZWorld.L17Shuttles.cs, UI/OzShuttleStart.cs)
            ZWorld.CpuShuttleDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0;                                                   // 1.2 item 12 DV138: the computer flies its shuttles: takes empty ones, lands squads on undefended flags, runs 2 min apart (Sim/ZBot.CpuShuttle.cs)
            ZWorld.SideShuttlesDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0;                                                 // 1.2 item 111 DV144: the SHUTTLE box for both sides, level 17's two empty car-park ships, the head start off every shuttle (Sim/ZWorld.SideShuttles.cs)
            ZWorld.ShuttleNearestLandingDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0;                                       // 1.5 DV190 (kit shuttle_nearest_landing): a player's shuttle ordered where it cannot land flies to the nearest place where it can; off in 'Zod exact' (Sim/ZWorld.ShuttleNearestLanding.cs)
            ZWorld.PyroForceBurnDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0;                                     // DV113: with Ctrl held, Pyros burn buildings and bridges (Sim/ZObject.PyroForceBurn.cs)
            ZWorld.DetailPropsDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0;
            ZWorld.TrafficLightsKerbDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0;                                 // 1.5 DV209 (kit traffic_lights_53): level 17's smaller traffic lights at the kerb, their map items with them
            ZWorld.FortRushDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0 && PlayerPrefs.GetInt("oz_fort_rush", 1) == 1;       // boarding_nb_v1 DV34 robots rush the fort door (NB15)
            ZWorld.BoardNearestDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0 && PlayerPrefs.GetInt("oz_board_nearest", 1) == 1;   // boarding_nb_v1 DV35 the nearest robot boards (NB16)
            ZWorld.ParkedPathDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0 && PlayerPrefs.GetInt("oz_parked_paths", 1) == 1;   // boarding_nb_v1 DV36 paths round parked units, bridge re-plans (NB18)
            ZWorld.CarParkBotDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0 && PlayerPrefs.GetInt("oz_carpark_bot", 1) == 1;    // boarding_nb_v1 DV37 L17 CPU car park drive (NB20)
            CaptureHold.Enabled = PlayerPrefs.GetInt("oz_capture_after_board", 1) == 1;                                                // boarding_nb_v1 NB17 (view only)
            ZWorld.GrabPolicyDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0 && PlayerPrefs.GetInt("oz_grab_policy", 1) == 1;   // DV38 one auto-grab per target, ordered units hold
            ZWorld.GrenadeRuleDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0 && PlayerPrefs.GetInt("oz_grenade_rule", 1) == 1;   // DV40 grenades: only grunts throw them at robots
            ZWorld.GrenadeBoostDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0 && PlayerPrefs.GetInt("oz_grenade_boost", 1) == 1;   // DV104 a thrown grenade: radius x 1.2; a direct hit x 1.15, an area hit x 0.9 (ZWorld.GrenadeBoost.cs)
            ZWorld.FortDefenderDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0 && PlayerPrefs.GetInt("oz_fort_defender", 1) == 1;   // fort_defender_v1 DV41: a grunt defends the roof of a fort under attack
            ZWorld.PathV3Deviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0 && PlayerPrefs.GetInt(OzSettingTips.PrefPathV3, 1) == 1;
            ZWorld.ElectrobotDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0 && PlayerPrefs.GetInt("oz_electrobot", 1) == 1;       // electrobot_v1 DV45: the Electrobot in the build lists
            ZWorld.ElectrobotBotDeviation = ZWorld.ElectrobotDeviation && PlayerPrefs.GetInt("oz_electrobot_cpu", 1) == 1;              // electrobot_v1 DV46: the CPU builds one at a time
            ZWorld.ElectrobotEscortDeviation = ZWorld.ElectrobotBotDeviation;                                                           // DV72: the CPU's Electrobot leaves with another unit
            ZWorld.ElectrobotAllLevels = false;
            ZWorld.KnockHoldDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0 && PlayerPrefs.GetInt("oz_knock_hold", 1) == 1;
            ZWorld.RunOverDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0 && PlayerPrefs.GetInt("oz_run_over", 1) == 1;
            ZWorld.JeepChargeDeviation = ZWorld.RunOverDeviation;                                                                       // DV121: the jeep runs robots over, with the run-over rule (off in 'Zod exact') (Sim/ZObject.JeepCharge.cs)
            ZWorld.SetCrocodileRules(PlayerPrefs.GetInt("oz_zod_exact", 0) == 0, PlayerPrefs.GetInt("oz_crocodiles", 1) == 1, PlayerPrefs.GetInt("oz_croc_hurry", 1) == 1, PlayerPrefs.GetInt("oz_croc_flee", 1) == 1, PlayerPrefs.GetInt("oz_croc_water_cost", 1) == 1);   // level 16: the crocodile rule and its three companions, each with its own switch (ZWorld.Crocodiles.cs)
            ZWorld.RocketSpeedDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0 && PlayerPrefs.GetInt("oz_rocket_speed", 1) == 1;
            ZWorld.HelpDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0 && PlayerPrefs.GetInt("oz_squad_help", 1) == 1;
            ZWorld.CaptureRunDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0 && PlayerPrefs.GetInt("oz_capture_run", 1) == 1;    // DV54: captures run
            ZWorld.CloseGrabDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0 && PlayerPrefs.GetInt("oz_close_grab", 1) == 1;      // DV55: very close flags / vehicles, the closest unit
            ZWorld.BotCommitDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0 && PlayerPrefs.GetInt("oz_bot_commit", 1) == 1;      // DV56: the CPU sends its idle groups
            ZWorld.BotLaserDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0;                                                      // DV71: the CPU builds Lasers sometimes (ZBot.LaserPick)
            ZWorld.BotHeavyDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0;                                                      // DV115: the CPU builds a mix of units, heavy tanks / missile launchers from 8:00 and the robot squads in turn (ZBot.HeavyMix.cs)
            ZWorld.MTankDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0;                                                         // DV145: the MEDIUM TANK in the build lists after the mech and in the CPU's production (Sim/ZMTank.cs)
            ZWorld.MTankAimDeviation = ZWorld.MTankDeviation;                                                                            // DV151: a medium tank that is not a veteran scatters its shells wider (Sim/ZObject.MTankAim.cs)
            ZWorld.MTankHurryDeviation = ZWorld.MTankDeviation;                                                                          // DV150: the medium tank picks up speed when it fights under fire (Sim/ZObject.MTankHurry.cs)
            ZWorld.TowerDrawnReachDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0;                                              // 1.5 DV208 (kit tower_reach_dv208): the reach to a fort tower gun is measured to where its tower is drawn (Sim/ZObject.TowerDrawnReach.cs)
            ZWorld.FortGunAnswerDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0;                                                // 1.5 DV204 (kit fort_gun_target_r3): an enemy fort tower gun shooting at a unit is answered first, also on a move (Sim/ZObject.FortGunAnswer.cs)
            ZWorld.TowerAnswerDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0;                                                   // 1.3 item 3 DV153: fort tower guns are answered: never dropped as 'unreachable' while in reach, chased to a spot with a clear shot (Sim/ZObject.TowerAnswer.cs)
            ZWorld.MechHpDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0;                                                       // DV147: the mech's health x 0.95, it carries the hand gun too (Sim/ZMechHp.cs)
            ZWorld.MechMixDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0;                                                      // DV148: the CPU builds mechs and medium tanks in a mix, about one each (Sim/ZBot.MechMix.cs)
            ZWorld.CpuRepairDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0;                                                     // 1.2 item 13 DV137: the CPU sends its strongest damaged vehicle to its repair station and back, once every 5 minutes (Sim/ZBot.CpuRepair.cs)
            ZWorld.CpuGunManDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0 && PlayerPrefs.GetInt(CpuGunManPref, 1) == 1;   // 1.3 item 11 DV157: the computer mans the empty guns it passes and the ones at its frontier (Sim/ZBot.CpuGunMan.cs)
            ZWorld.CpuDefendDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0;                                                     // 1.2 items 14 + 14a DV136: from 8:00 one in four CPU orders defends a frontier flag for 2 minutes (Sim/ZBot.CpuDefend.cs)
            ZWorld.BotFinishDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0 && PlayerPrefs.GetInt("oz_bot_finish", 1) == 1;
            ZWorld.RepairInPlaceDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0 && PlayerPrefs.GetInt("oz_repair_pad", 1) == 1;
            ZWorld.CraneFixDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0 && PlayerPrefs.GetInt("oz_crane_fix", 1) == 1;
            ZPathing.VehicleFirst = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0;                                                       // vehicles first: own robots never hold a vehicle (ZYield.cs)
            ZWorld.CraneAnyRuinDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0;                                                  // DV74: a crane rebuilds a destroyed building / bridge in any zone
            ZWorld.RepairBayFireDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0;                                                 // DV77: a vehicle fires out of its repair bay on the way in and out too
            ZWorld.UnmannedStopDeviation = true;                                                                                       // DV79: a vehicle with nobody inside never moves, turns or fires (ZObject.Unmanned.cs)
            ZWorld.DriverFireDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0;                                                   // DV87: hatch drivers fire their own weapon; no driver comes up on a bridge or in a door lane (ZObject.DriverFire.cs)
            ZWorld.MechHandGunDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0;                                                // DV146: the mech's hand gun fires the Psycho's machine gun at robots while its rockets reload (Sim/ZObject.MechHandGun.cs)
            ZWorld.MechGunLossDeviation = ZWorld.MechHandGunDeviation;                                                                // DV149: a badly damaged mech can lose its hand gun to a heavy hit, for good (with DV146; Sim/ZObject.MechGunLoss.cs)
            ZWorld.EvilZodDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0;                                                      // DV161: EvilZod, the Unit editor's and the testing panel's hero (Sim/ZEvilZod.cs)
            ZWorld.MammothDeviation = ZWorld.BizonDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0;                             // 1.5 DV180 / DV181: the MAMMOTH and the BIZON, Unit editor only (Sim/ZBigTanks.cs)
            ZWorld.MammothRamDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0; Debug.Log($"[OpenZ] the Mammoth's ram (DV182): {(ZWorld.MammothRamDeviation ? "ON" : "off")}");   // 1.5 DV182 (kit mammoth_ram): the Mammoth rams an enemy vehicle close in front (Sim/ZObject.MammothRam.cs); off in 'Zod exact'
            ZWorld.BizonCrushDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0; Debug.Log($"[OpenZ] the Bizon's crush (DV183): {(ZWorld.BizonCrushDeviation ? "ON" : "off")}");   // 1.5 DV183 (kit bizon_crush): the Bizon rolls over an enemy vehicle or ground gun close in front (Sim/ZObject.BizonCrush.cs); off in 'Zod exact'
            ZWorld.RamDuelDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0; Debug.Log($"[OpenZ] the ram duel (DV185): {(ZWorld.RamDuelDeviation ? "ON" : "off")}");   // 1.5 DV185 (kit big_tanks_fx): two big tanks ramming each other get one clean result (Sim/ZObject.RamDuel.cs); off in 'Zod exact'
            ZWorld.CrushRollOffDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0; Debug.Log($"[OpenZ] the Bizon rolls off the far side (DV187): {(ZWorld.CrushRollOffDeviation ? "ON" : "off")}");   // 1.5 DV187 (kit bizon_crush_climb): Sim/ZObject.CrushRollOff.cs; off in 'Zod exact'
            ZWorld.RamDebrisDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0; Debug.Log($"[OpenZ] a rammed / crushed vehicle's turret lands near its wreck (DV186): {(ZWorld.RamDebrisDeviation ? "ON" : "off")}");   // 1.5 DV186 (kit ram_throw_fix): Sim/ZWorld.RamDebris.cs; off in 'Zod exact'
            ZWorld.RamPolishDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0; Debug.Log($"[OpenZ] the ram polish (DV188): {(ZWorld.RamPolishDeviation ? "ON" : "off")}");   // 1.5 DV188 (kit ram_polish): Sim/ZObject.RamPolish.cs; off in 'Zod exact'
            ZWorld.BigTankRamFirstDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0; Debug.Log($"[OpenZ] the big tanks ram first (DV189): {(ZWorld.BigTankRamFirstDeviation ? "ON" : "off")}");   // 1.5 DV189 (kit ram_first): Sim/ZObject.RamFirst.cs; off in 'Zod exact'
            ZWorld.HeroSquadDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0;                                                    // DV108: hero squads placed by the level data (Sim/ZHeroSquad.cs)
            ZWorld.HeroSquadsOffAsBlue = OzPlayBlue.Active && player == Team.Blue;                                                 // DV108 part 2 (1.3 row 80): ALAN & BRAD are red's heroes: a PLAY AS BLUE start places none (Sim/ZHeroSquad.cs)
            ZWorld.HeroBoostDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0;                                                    // DV112: hero squads stronger + self-repair (Sim/ZHeroSquad.Kh4.cs)
            ZWorld.GrabCertainDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0;                                                  // DV84: one unit takes a free flag / empty vehicle for certain (ZObject.GrabCertain.cs)
            ZWorld.MarchGrabDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0;                                                    // 1.5 DV203 (kit march_grab_r3): one robot of a walking squad takes a flag / an empty gun on the way (Sim/ZObject.MarchGrab.cs)
            ZWorld.ElectroSteadyDeviation = ZWorld.ElectrobotDeviation;                                                                // DV83: the Electrobot stands and fires (ZObject.ElectroSteady.cs)
            ZWorld.SnipeSafeSpotDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0;                                                // DV82: no driver sniping on a bridge or in a door lane (ZWorld.SnipeSafe.cs)
            ZWorld.RiflePairDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0;                                                     // DV78: a grunt's rifle on a grunt (ZObject.RiflePair.cs)
            ZWorld.FireOnMoveDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0 && PlayerPrefs.GetInt("oz_fire_on_move", 1) == 1;
            ZWorld.EjectLockDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0 && PlayerPrefs.GetInt("oz_eject_lock", 1) == 1;
            ZWorld.VehicleExitDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0; Debug.Log($"[OpenZ] EXIT DRIVER for tanks, the mech and cranes (DV171): {(ZWorld.VehicleExitDeviation ? "ON" : "off")}");   // 1.4 DV171: the driver of a tank / the mech climbs out, 30 s without auto entry (Sim/ZObject.VehicleExit.cs); the computer never uses it
            ZWorld.DriverThrownDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0; Debug.Log($"[OpenZ] a driver getting out is thrown by his vehicle's blast (DV179): {(ZWorld.DriverThrownDeviation ? "ON" : "off")}");   // 1.5 DV179 (kit exit_leap_death): a tank / the mech destroyed before its driver's take-off throws him (Sim/ZObject.DriverThrown.cs, View/BoardingView.HatchThrow.cs); off in 'Zod exact'
            ZWorld.PremiumDodgeDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0; Debug.Log($"[OpenZ] PREMIUM DRIVER DODGE for tanks and the mech (DV173): {(ZWorld.PremiumDodgeDeviation ? "ON" : "off")}");   // 1.4 DV173: a tank / the mech with a non-grunt driver steps aside x 1.08 faster, its hull turns quicker (Sim/ZObject.PremiumDodge.cs)
            ZWorld.EmptiedTargetsDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0; Debug.Log($"[OpenZ] nobody keeps shooting an emptied vehicle (DV176): {(ZWorld.EmptiedTargetsDeviation ? "ON" : "off")}");   // 1.4 DV176: EXIT DRIVER, a crew shot out, a hero pilot's shuttle EXIT, a gun's EJECT: every team drops the emptied vehicle (Sim/ZWorld.DropEmptied.cs); off in 'Zod exact'
            ZWorld.SprintOrderDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0;                               // 1.5 DV178: an order with the sprint mark moves x 1.5 (Sim/ZObject.SprintOrder.cs); off in 'Zod exact'
            ZWorld.EnterFallbackDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0 && PlayerPrefs.GetInt("oz_enter_fallback", 1) == 1;
            ZWorld.L17CrewsDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0 && PlayerPrefs.GetInt("oz_l17_crews", 1) == 1 && gameDifficulty == GameDifficulty.Easy && !(blueBot && redBot);
            ZWorld.L17CrewsTeam = player;
            ZWorld.FortDoorGunDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0 && gameDifficulty == GameDifficulty.Hard && !(blueBot && redBot);   // DV73 Hard: a missile cannon on each fort's door tower (ZWorld.FortDoorGun.cs)
            ZWorld.ExitLanesDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0 && PlayerPrefs.GetInt("oz_exit_lanes", 1) == 1;
            ZWorld.StrikeGroupDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0 && PlayerPrefs.GetInt(OzStrikeGroups.Pref, 1) == 1;   // DV107 enemy strike groups: the Options row ENEMY STRIKE GROUPS (oz_strike_groups, default on; ZBot.StrikeGroup.cs)
            ZWorld.OvertakeDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0 && OzOvertake.On; Debug.Log($"[OpenZ] fast units pass slow ones: {(ZWorld.OvertakeDeviation ? "ON" : "off")}");   // DV105: the Options row FAST UNITS PASS SLOW ONES, OFF by default; off in 'Zod exact' (Sim/ZOvertake.cs)
            ZWorld.CalmMoveDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0; Debug.Log($"[OpenZ] calm movement (b49): {(ZWorld.CalmMoveDeviation ? "ON" : "off")}");   // b49 DV101: on in the game, off in 'Zod exact' (Sim/ZCalm.cs)
            ZWorld.FortRunDeviation = ZWorld.RockRoundDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0; OzOverlapOption.Apply();   // DV102 fort run, DV103 round the rocks, the option FRIENDLY UNITS OVERLAP (never in 'Zod exact')
            Debug.Log($"[OpenZ] fort run (DV102): {(ZWorld.FortRunDeviation ? "ON" : "off")}, round the rocks (DV103): {(ZWorld.RockRoundDeviation ? "ON" : "off")}, friendly units overlap: {(ZWorld.FriendsOverlap ? "ON" : "off")}");
            ZWorld.FortAutoRushDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0; Debug.Log($"[OpenZ] fort run from near the enemy gate (DV168): {(ZWorld.FortAutoRushDeviation ? "ON" : "off")}");   // 1.4 DV168 (Sim/ZWorld.FortAutoRush.cs): never in 'Zod exact'; a bot match has no player's team, so nothing happens there
            ZWorld.CalmFollowDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0; Debug.Log($"[OpenZ] calm follow behind a slower unit (DV109): {(ZWorld.CalmFollowDeviation ? "ON" : "off")}");   // DV109: on in the game, off in 'Zod exact' (Sim/ZPathing.CalmFollow.cs)
            ZWorld.SniperHeadshotDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0;                                                // DV116: sniper headshots, 15 % of a sniper's hits on a robot (Sim/ZObject.Headshot.cs)
            ZWorld.SniperSteadyDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0;                                                  // DV120: snipers stand and fire: no walk past an enemy in range, no walk back (Sim/ZObject.SniperSteady.cs)
            ZWorld.ToughSteadyDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0;                                                   // DV123: Toughs stand and fire: no walk past an enemy in range, no walk back, fewer small steps after a kill (Sim/ZObject.ToughSteady.cs)
            ZWorld.NoAutoFlagUnderFireDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0;                                           // DV130: a player's robot under fire runs to no flag by itself; a clicked flag always (Sim/ZObject.NoAutoFlag.cs)
            ZWorld.HeavyFallDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0; ZWorld.HeavyTurretRockClearM = OzTreeCrush.TurretRockClear(); OzTreeCrush.Ensure();   // DV124: a big faller fells the tree it comes down on (Sim/ZWorld.HeavyFall.cs, View/OzTreeCrush.cs)
            ZWorld.GroundFireDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0;                                                   // DV117: burning ground bites an enemy who stays in it (Sim/ZWorld.GroundFire.cs)
            ZWorld.HighGroundDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0 && OzHighGroundOption.On;                       // DV162: high ground shoots 10 % farther at a target below (Sim/ZObject.HighGround.cs, OPTIONS "HIGH GROUND SHOOTS FARTHER", oz_high_ground, default on)
            ZWorld.LavaDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0 && OzLavaOption.On;                                    // DV152: level 7 lava: robots go in only on the player's order and melt if they stay (Sim/ZWorld.Lava.cs, OPTIONS "LAVA BURNS ROBOTS", oz_lava_burns, default on)
            ZWorld.HeavyRockDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0;                                                     // DV164: a heavy tank standing idle clears the rock it clips (Sim/ZObject.HeavyRock.cs)
            ZWorld.DragonDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0;                                                         // DV163: level 7, a dragon that happens to pass close may burn a robot, and robots shoot back (Sim/ZWorld.Dragons.cs)
            ZWorld.SniperBalanceDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0;                                                  // 1.2: snipers slightly weaker: hit 0.75, reload 0.45 s (Sim/ZWorld.SniperBalance.cs)
            ZWorld.ShiftAppendDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0;                                                    // 1.2 item 9 DV135: a Shift queue goes after the units' current order (View/OzShiftAppend.cs)
            ZWorld.RepeatQueueDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0; Debug.Log($"[OpenZ] REPEAT QUEUE in the build menu (DV169): {(ZWorld.RepeatQueueDeviation ? "ON" : "off")}");   // 1.4 DV169: the build menu's switch that loops a building's whole queue (Sim/ZObject.RepeatQueue.cs); the computer never uses it
            ZWorld.GatlingBuffDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0; Debug.Log($"[OpenZ] gatling 10 % farther, 15 % harder per shot (DV170): {(ZWorld.GatlingBuffDeviation ? "ON" : "off")}");   // 1.4 DV170 (Sim/ZGatlingBuff.cs): set before ZWorld.Create parses the settings; the Unit editor's playground starts here too
            ZWorld.FortTurretRofDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0; Debug.Log($"[OpenZ] fort tower missile cannons reload 2x slower (DV177): {(ZWorld.FortTurretRofDeviation ? "ON" : "off")}");   // 1.5 DV177 (Sim/ZObject.FortTurretRof.cs): ground missile cannons unchanged
            ZWorld.FlagStepOffDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0; Debug.Log($"[OpenZ] a vehicle steps off the flag it captured (DV184): {(ZWorld.FlagStepOffDeviation ? "ON" : "off")}");   // 1.5 DV184 (kit vehicle_off_flag): Sim/ZObject.FlagStepOff.cs; off in 'Zod exact'
            ZWorld.MissileTurretBuildTimeDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0; Debug.Log($"[OpenZ] the missile cannon builds in the howitzer's time x 1.4 (DV191): {(ZWorld.MissileTurretBuildTimeDeviation ? "ON" : "off")}");   // 1.5 DV191 (kit missile_build_time): Sim/ZMissileBuildTime.cs; set before ZWorld.Create parses the settings; off in 'Zod exact'
            ZWorld.RamFightDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0; Debug.Log($"[OpenZ] the Mammoth fights on while it rams (DV196): {(ZWorld.RamFightDeviation ? "ON" : "off")}");   // 1.5 DV196 (kit mammoth_ram_fight_n26c): Sim/ZObject.RamFight.cs; off in 'Zod exact'
            ZWorld.RamChainDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0; Debug.Log($"[OpenZ] the rammed stay still, the big tanks ram on (DV195): {(ZWorld.RamChainDeviation ? "ON" : "off")}");   // 1.5 DV195 (kit ram_chain_n24_n25): Sim/ZObject.RamChain.cs; off in 'Zod exact'
            ZWorld.L18StumpsDeviation = ZWorld.L18BigTanksDeviation = ZWorld.BotBigTanksDeviation = ZWorld.BigTankDriveDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0;   /* 1.5 DV202 (kit l18_cpu_bigtank_r2): the computer drives its big tank */ Debug.Log($"[OpenZ] level 18: the river stumps (DV197), the Mammoth and the Bizon in the start set (DV198), the computer and the big tanks (DV199): {(ZWorld.L18BigTanksDeviation ? "ON" : "off")}");   // 1.5 DV197 / DV198 / DV199 (kit level18_rules): Sim/ZWorld.L18Rules.cs, Sim/ZBot.BigTanks.cs; set before ZWorld.Create; off in 'Zod exact'
            ZWorld.L18ContainersDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0;   // 1.5 DV201 (kit level18_containers): level 18's containers in the water, destructible (Sim/ZWorld.L18Containers.cs); set before ZWorld.Create; off in 'Zod exact'
            ZWorld.BizonAmphibDeviation = ZWorld.BigTankWreckDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0; Debug.Log($"[OpenZ] the Bizon amphibious (DV192) + the big tanks crush wrecks (DV193): {(ZWorld.BizonAmphibDeviation ? "ON" : "off")}");   // 1.5 DV192 / DV193 (kit sim_notes11): Sim/ZBigTanks.Ground.cs; set before ZWorld.Create; off in 'Zod exact'
            ZWorld.OrderMoveFallbackDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0;                                              // 1.3 item 29 DV158: a player's unit that cannot attack the clicked target walks to the clicked point (Sim/ZWorld.OrderFallback.cs)
            ZWorld.AheadWalkDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0;                                                      // 1.3 item 29 DV166: a squad member ahead of its leader, out of his reach, walks its own path to the goal instead of standing (Sim/ZCommit.AheadWalk.cs)
            ZWorld.ProneTargetDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0;                                                  // DV122: a kneeling / lying sniper is a smaller target, the stance a rule (Sim/ZObject.ProneTarget.cs)
            if (level == "L17") Debug.Log($"[OpenZ] L17 start crews (DV63): {(ZWorld.L17CrewsDeviation && ParkedStartOn(level) ? "ON for " + player + " (Easy)" : "off (" + OpenZDifficulty.Label(gameDifficulty) + ")")}");
            Debug.Log($"[OpenZ] driver sniping: {(ZWorld.SnipeDeviation ? "OpenZ deviation DC2 (driver-hit floor, snipers x3, open-seat snipe chance, resting crews)" : "Zod exact")}");
            SetParkingKeepOut(map.text, level);                                                          // DV66: no vehicle parked on a lamp / sign / hydrant (ZWorld.ExitLanes.cs)
            OzLevelStatics.NewWorld();
            ZWorld.CustomStatsDeviation = PlayerPrefs.GetInt("oz_zod_exact", 0) == 0 && OzLevelStats.Wanted(level, demo); OzLevelStats.AtLevelStart(this, st.text);   // 1.2 item 60 DV142: UNIT STATS CUSTOM (the battlefield screen's choice, armed by the player's own DEPLOY; off in 'Zod exact', a bot match, the Unit editor): every unit of both teams is made from the Unit editor's Custom stats; OPENZ: nothing changes (View/OzLevelStats.cs, Sim/ZWorld.CustomStats.cs)
            // duel game creation
            OpenZ.Multiplayer.DuelRuntime.PrepareGame(this); World = ZWorld.Create(st.text, map.text, seed, !skirmish && ParkedStartOn(level)); if (onlineSkirmish) World.Map.skirmish = true; OzBehaviorLibrary.AtWorldStart(this); OpenZ.Multiplayer.DuelRuntime.WorldCreated(this);
            if (!skirmish && !OpenZ.Multiplayer.DuelRuntime.Active && !demo && blueBot != redBot && !OzUeHooks.Active &&
                (skirmishMap?.Solo == true || OzLevelDef.All.Any(definition => definition.Id == level)))
            {
                SoloAi2026 = OzAiSoloChoice.Level;
                World.ModernBotFog = IsMayhem;
                World.CampaignFogTeam = IsMayhem ? player : Team.Neutral;
                World.ModernBotLineOfSight = OzFogOfWar.LosOn;
                World.ModernBotSoloRadar = OzBattleOptions.SoloRadar;
            }
            if (!skirmish) OzSoloAllowedUnits.Apply(this);
            World.ClassicMissileLauncher = !OpenZ.Multiplayer.DuelRuntime.Active && !demo &&
                (!skirmish || skirmishMap?.Solo == true) && OzBattleOptions.ClassicMissileLauncher;
            if (!skirmish && !OpenZ.Multiplayer.DuelRuntime.Active &&
                (skirmishMap?.Solo == true || OzLevelDef.All.Any(definition => definition.Id == level)))
            {
                var tech = OzSoloMapTech.Read(level);
                World.ApplySoloMapTech(tech.Vehicle, tech.Robot, tech.Native);
            }
            Debug.Log($"[OpenZ] hero squads at the start (DV108): {World.HeroSquadsMade}{(ZWorld.HeroSquadsOffAsBlue ? " (PLAY AS BLUE: ALAN & BRAD are red's heroes, none placed; row 80)" : ZWorld.HeroSquadDeviation ? "" : " ('Zod exact')")}");
            if (localSkirmish != null)
            {
                World.Map.skirmish = true;
            }
            else if (skirmish && !OpenZ.Multiplayer.DuelRuntime.Active)
            {
                World.ConfigureSkirmishBots(Enumerable.Range(1, skirmishMap.Starts - 1)
                    .Select(position => ((Team)(position + 1), ZBotDifficulty.Normal)), seed);
            }
            else
            {
                if (blueBot) World.Bots.Add(new ZBot(World, Team.Blue, seed + 101, BotDifficulty(Team.Blue)));
                if (redBot) World.Bots.Add(new ZBot(World, Team.Red, seed, BotDifficulty(Team.Red)));
            }
            if (skirmish && !OpenZ.Multiplayer.DuelRuntime.Active) World.LocalSkirmishPlayer = player;
            foreach (var b in World.Bots) Debug.Log($"[OpenZ] CPU {b.Team}: {b.Difficulty}{(ZAiDifficulty.IsModern(b.Difficulty) ? " (2026)" : b.Difficulty == ZBotDifficulty.Easy ? " (1996 Easy)" : " (1996)")}");
            var col = GameObject.Find("COL_Terrain");
            ground = col ? col.GetComponent<Collider>() : null;
            BindViews();
            if (localSkirmish != null)
            {
                localSkirmish.ConfigureWorld(World, seed);
                DuelBindStartingViews();
                if (player != Team.Red)
                {
                    var camera = FindFirstObjectByType<RtsCamera>();
                    var fort = World.Objects.FirstOrDefault(obj => obj.IsFort && obj.Owner == player);
                    if (camera && fort != null) { camera.focus = PxToWorld(fort.CenterX, fort.CenterY); camera.Place(); }
                }
                StartSkirmishPlanning(localSkirmish.Rules.PlanningSeconds);
            }
            else if (!World.HasDuelRules) HideReplacedStartingUnitViews(false);
            OzHeroSquadView.Spawn(this);                                                          // DV108: the hero squads' own models; the scene's robots of the entry they replace are hidden
            OzPlayableShuttle.Spawn(this);                                                        // DV118: the scene's ship becomes the playable shuttle's view (View/OzPlayableShuttle.cs)
            OzShuttleEmptyLook.SpawnMapShuttles(this);                                            // K256f: every other shuttle of the rules (a map's entry) its full view; the empty look on each
            OzL18Rules.AtStart(this);                                                             // 1.5 DV197 / DV198 / DV200 (kit level18_rules): level 18's river stumps as the rules have them, one log line (View/OzL18Rules.cs)
            foreach (var c in World.FortDoorGuns)
            {   // DV73 (Hard): the door tower's missile cannon: the scene's own cannon of that tower is hidden, the new one gets its view
                if (Views.ContainsKey(c.Id)) continue;
                var zroot = GameObject.Find("ZodObjects");
                if (zroot) foreach (var ct in ZBuildLists.Cannons) { var old = zroot.transform.Find($"{ct}_{c.LocX / 16}_{c.LocY / 16}"); if (old) old.gameObject.SetActive(false); }
                SpawnUnitView(c);
            }
            if (World.FortDoorGuns.Count > 0) Debug.Log($"[OpenZ] Hard: {World.FortDoorGuns.Count} fort door-tower missile cannons (DV73)");
            OzTowerCannonViews.Spawn(this);                                                           // the level data's fort tower cannons the scene does not show (level 16: DV99)
            // mech), crewed, at the fort, on top of the map's start units. Only human vs bot; never in bot matches or tests.
            ApplyTechBonus();
            // choice is kept and counts again on EASY, MEDIUM and HARD. Every start path comes here (the level-start screen, quick restart, next level,
            // the scores popup's replay, a test start): ShownDifficulty is MAYHEM for a MAYHEM match. The rules only get the vehicles or not (Sim untouched).
            // duel no solo starting army
            bool headStartNow = !skirmish && !OpenZ.Multiplayer.DuelRuntime.Active && OpenZDifficulty.HeadStartFor(ShownDifficulty); HeadStartMade = 0;
            HeadStartRefusedMayhem = IsMayhem && OpenZDifficulty.HeadStartToggle && !(blueBot && redBot);
            if (HeadStartRefusedMayhem) Debug.Log($"[ZGame] head start: none on MAYHEM ({level}; the saved HEAD START ON is kept for the other difficulties)");
            if (!(blueBot && redBot) && headStartNow)
            {
                var types = ZWorld.SelectHeadStart(OpenZDifficulty.HeadStartDefaults(level), World.DuelAllowsUnit);
                if (types.Length > 0)
                {
                    bool parked = ParkedStartOn(level);
                    var extra = World.AddHeadStart(player, parked, types);
                    HeadStartMade = extra.Count;
                    foreach (var unit in extra)
                    {
                        var view = SpawnUnitView(unit);
                        if (view && unit.Owner == Team.Neutral) view.FaceSide();
                    }
                    Debug.Log($"[ZGame] {level} head start for {player}: {string.Join(", ", extra.Select(unit => unit.Type))}");
                }
            }
            // 1.3 rows 58-59 DV165 (Sim/ZEvilZod.Appear.cs): EvilZod in the four campaign levels, a human against the computer, with DV161 (off in 'Zod exact').
            // Row 58: a blue player's start right after a DEFEAT (the lose screen's REPLAY / RETRY, UI/OzUi.cs OzScenes) has him at the blue fort, the player's own unit.
            bool ezRetry = OzScenes.TakeRetryAfterLoss();
            bool ezLevel = level == "L02" || level == "L16" || OzCity.Is(level) || OzVolcanic.Is(level), ezHuman = blueBot != redBot;
            if (ZWorld.EvilZodDeviation && ezLevel && ezHuman && ezRetry && player == Team.Blue && World.DuelAllowsUnit("evilzod"))
            {
                var ez = World.AddEvilRetryHelp(Team.Blue);
                if (ez != null) SpawnUnitView(ez);
                Debug.Log($"[OpenZ] EvilZod retry help (DV165): {(ez != null ? $"at the blue fort ({ez.CenterX}, {ez.CenterY}) px, yours" : "no free spot at the blue fort")}");
            }
            World.EvilLastDefenseTeam = ZWorld.EvilZodDeviation && ezLevel && ezHuman && player == Team.Red && blueBot ? Team.Blue : Team.Neutral;
            Debug.Log($"[OpenZ] EvilZod (DV165): retry after a defeat {(ezRetry ? "yes" : "no")}, blue's last defense {(World.EvilLastDefenseTeam == Team.Blue ? "watched from 10:00" : "off")}");
            if (!OpenZ.Multiplayer.DuelRuntime.Active && localSkirmish == null && !demo && !OzUeHooks.Active &&
                (skirmish || skirmishMap?.Solo == true || ZAiDifficulty.IsModern(SoloAi2026)))
            {
                World.LimitModernBotStartingUnits();
                World.BalanceNeutralStartingUnits(preserveMatchedMultiplayerPlacements: skirmish);
                BindStartingArmyViews(false);
            }
            SetupWalkable();
            if (!GetComponent<CombatView>()) gameObject.AddComponent<CombatView>();
            if (!FindFirstObjectByType<FxManager>()) new GameObject("FxManager").AddComponent<FxManager>();
            if (!FindFirstObjectByType<CombatAudio>()) new GameObject("CombatAudio").AddComponent<CombatAudio>();
            if (!GetComponent<RulesView>()) gameObject.AddComponent<RulesView>();
            if (!FindFirstObjectByType<ZoneMarkers>()) new GameObject("ZoneMarkers").AddComponent<ZoneMarkers>();
            if (!FindFirstObjectByType<PropBreakables>()) new GameObject("PropBreakables").AddComponent<PropBreakables>();
            if (!FindFirstObjectByType<BridgeRepairBadges>()) new GameObject("BridgeRepairBadgesHost").AddComponent<BridgeRepairBadges>();
            if (!FindFirstObjectByType<VeteranMarks>()) new GameObject("VeteranMarks").AddComponent<VeteranMarks>();   // veterancy_v1 DV12
            if (level == "L16" && !FindFirstObjectByType<OzBridgeWobble>()) new GameObject("OzBridgeWobble").AddComponent<OzBridgeWobble>();   // level 16: the plank bridge gives a little under a vehicle (view only)
            if (level == "L16" && !FindFirstObjectByType<OzTreeFall>()) new GameObject("OzTreeFall").AddComponent<OzTreeFall>();   // DV88: the trees that close a tile get their props here (before the first DetailPropHub.Update binds them) and fall when the rules destroy them (view only)
            if (OzCity.Is(level) && !FindFirstObjectByType<OzWeather>()) new GameObject("OzWeather").AddComponent<OzWeather>();   // weather_l17_v1: night rain showers + thunder (view only, no sim change)
            if ((level == "L16" || World.Map.aiMap && World.Map.planet == "jungle") && !FindFirstObjectByType<OzJungleWeather>())
                new GameObject("OzJungleWeather").AddComponent<OzJungleWeather>();
            if (World.Map.HasLava && !FindFirstObjectByType<OzLava>()) new GameObject("OzLava").AddComponent<OzLava>();   // level 7: the lava surface, pops, glow light, robots' heat (View/OzLava.cs; view only)
        }

        /// <summary>1.4 mayhem_headstart, checks: the head start vehicles made at the last level start, and whether a saved HEAD START ON was refused
        /// there because the match is MAYHEM.</summary>
        public static int HeadStartMade; public static bool HeadStartRefusedMayhem;
        /// <summary>OpenZ head start (deliberate deviation DV3, listed in the parity report's Deviations block): the crewed vehicles the
        /// human gets on L02 in human vs CPU games while PlayerPrefs <see cref="HeadStartPref"/> is 1 (the default).</summary>
        public static readonly string[] HeadStartTypes = { "light", "light", "jeep", "jeep", "medium" };
        public static readonly string[] EasyL17Types = { "missile_launcher", "heavy" };
        /// <summary>Level 16 head start: two jeeps and a light tank, crewed (the map itself starts each side with a heavy and a medium tank).</summary>
        public static readonly string[] HeadStartL16Types = { "jeep", "jeep", "light" };
        /// <summary>Level 7 head start: as level 16, two jeeps and a light tank, crewed (the map starts each side with one light tank and no jeep).</summary>
        public static readonly string[] HeadStartL07Types = { "jeep", "jeep", "light" };
        /// <summary>Level 18 head start (1.5, kit level18_map; owner addition 49, kit level18_rules/blue_start_v3): two jeeps and a light tank, crewed,
        /// as on levels 7 and 16. No crane: in Z a crane is found EMPTY further away, never given at the start (the map's four neutral cranes).</summary>
        public static readonly string[] HeadStartL18Types = ZWorld.L18HeadStartTypes;
        public const string HeadStartPref = "oz_headstart";
        /// <summary>
        /// PlayerPrefs oz_l17_parked (1 = on, default); 'Zod exact' turns it off. Bot matches use the same switch.</summary>
        public const string ParkedStartPref = "oz_l17_parked";
        /// <summary>1.3 item 11 DV157: PlayerPrefs oz_cpu_man_guns (1 = on, default; 0 = the engine's computer); 'Zod exact' turns it off.</summary>
        public const string CpuGunManPref = "oz_cpu_man_guns";
        public static bool ParkedStartOn(string level) => level == "L17" && PlayerPrefs.GetInt("oz_zod_exact", 0) == 0 && PlayerPrefs.GetInt(ParkedStartPref, 1) == 1;

        /// <summary>Easy only applies to a CPU playing against the human; bot vs bot (both CPUs) always runs the Zod bot.</summary>
        public ZBotDifficulty BotDifficulty(Team t) => blueBot && redBot || t == player ? ZBotDifficulty.Normal :
            ZAiDifficulty.IsModern(SoloAi2026) ? SoloAi2026 : difficulty;

        /// <summary>Difficulty unlocks (deviation, Hard = none): the human's factories and fort read their build list higher up.
        /// Bots never get a bonus; bot vs bot runs Zod lists on both sides.</summary>
        // duel no solo technology bonus
            public void ApplyTechBonus()
        {
            if (OpenZ.Multiplayer.DuelRuntime.Active) return;
            if (World == null || World.Map.skirmish) return;
            for (int i = 0; i < World.TechBonus.Length; i++) World.TechBonus[i] = 0;
            if (!(blueBot && redBot) && !World.UseNativeMapTech) World.TechBonus[(int)player] = OpenZDifficulty.TechBonus(gameDifficulty);
            World.SetCpuBuildFaster(IsMayhem, player, blueBot, redBot);
            if (IsMayhem) Debug.Log($"[OpenZ] MAYHEM: the CPU builds in {ZWorld.CpuBuildFasterScale * 100:0} % of the player's time (DV175): red {World.CpuBuildFaster(Team.Red)}, blue {World.CpuBuildFaster(Team.Blue)}");
            Debug.Log($"[OpenZ] difficulty {OpenZDifficulty.Label(ShownDifficulty)}{(IsMayhem ? " (HARD rules + the fog of war, 1.4 MAYHEM)" : "")}: CPU {difficulty}, {player} tech bonus +{World.TechBonus[(int)player]}");
        }

        void BindViews()
        {
            var root = GameObject.Find("ZodObjects");
            if (!root) { Debug.LogWarning("ZGame: no ZodObjects in the scene"); return; }
            var byName = new Dictionary<string, Transform>();
            foreach (Transform t in root.transform) byName[t.name] = t;
            int bound = 0, missing = 0; List<ZObject> noPlaced = null;
            foreach (var o in World.Objects)
            {
                if (o.Source != null && (o.Kind == ZKind.MapItem || o.Kind == ZKind.Building) && byName.TryGetValue($"{o.Type}_{o.Source.x}_{o.Source.y}", out var st))
                { StaticViews[o.Id] = st.gameObject; var rp = st.GetComponent<RockPillar>(); if (rp) rp.BindSimulation(o); }
                else if (o.Source != null && o.Kind == ZKind.MapItem && o.Type != "rock")
                {   // critic L02 #9 (crate stays after pickup): rebuilt props may carry another name; bind the nearest same-type object
                    var c = CenterWorld(o, false); Transform best = null; float bd = 9f;
                    foreach (Transform t2 in root.transform)
                    {
                        if (!t2.name.StartsWith(o.Type) || StaticViews.ContainsValue(t2.gameObject)) continue;
                        var d = t2.position - c; d.y = 0; if (d.sqrMagnitude < bd) { bd = d.sqrMagnitude; best = t2; }
                    }
                    if (best) StaticViews[o.Id] = best.gameObject;
                }
                if (o.Source == null || !(o.Kind == ZKind.Robot || o.Kind == ZKind.Vehicle || o.Kind == ZKind.Cannon)) continue;
                string n = $"{o.Type}_{o.Source.x}_{o.Source.y}";
                int group = o.Kind == ZKind.Robot ? (o.Leader ?? o).Minions.Count + 1 : 1;
                if (group > 1) n += $"_{o.MemberIndex}";
                if (!byName.TryGetValue(n, out var t) && !byName.TryGetValue(OzL18Rules.SceneAlias(o), out t)) { missing++; if (o.Kind == ZKind.Vehicle && !o.IsShuttle) (noPlaced ?? (noPlaced = new List<ZObject>())).Add(o); continue; }   // a map vehicle the scene has no object for gets its own view below, so it is never invisible
                t = OzHeavyTank.SceneView(t, o);                                                             // 1.2 item 110: a heavy tank placed in the level scene (v1's Z_heavy) drawn as the switch says
                t = OzV2Units.SceneView(t, o);                                                               // 1.2 v2 units: a light tank placed in the level scene (v1's Z_light) drawn as its switch says
                var v = t.GetComponent<UnitView>(); if (!v) v = t.gameObject.AddComponent<UnitView>();
                v.Bind(this, o);
                Views[o.Id] = v; bound++;
            }
            int madeViews = 0;
            if (noPlaced != null) foreach (var o in noPlaced) if (SpawnUnitView(o)) madeViews++;
            Debug.Log($"ZGame {level}: {World.Objects.Count} sim objects, {bound} unit views bound, {madeViews} made for map vehicles the scene does not hold, {missing - madeViews} missing, {StaticViews.Count} static views");
        }

        void HideReplacedStartingUnitViews(bool replaceCrewed)
        {
            var hidden = new HashSet<string>(System.StringComparer.Ordinal);
            foreach (var source in World.Map.objects)
            {
                if (source.Category != "robot" && source.Category != "vehicle" && source.Category != "cannon") continue;
                if (!World.DuelAllowsUnit(source.Type) || World.RemovedStartingSources.Contains(source) ||
                    replaceCrewed && (source.owner == "red" || source.owner == "blue"))
                    hidden.Add($"{source.Type}_{source.x}_{source.y}");
            }
            if (hidden.Count == 0) return;
            var root = GameObject.Find("ZodObjects");
            if (!root) return;
            var liveViews = new HashSet<GameObject>(Views.Where(pair => pair.Value && World.Get(pair.Key) != null)
                .Select(pair => pair.Value.gameObject));
            foreach (Transform child in root.transform)
            {
                if (liveViews.Contains(child.gameObject)) continue;
                string name = child.name;
                int suffix = name.LastIndexOf('_');
                if (hidden.Contains(name) || suffix > 0 && hidden.Contains(name.Substring(0, suffix)))
                    child.gameObject.SetActive(false);
            }
        }

        /// <summary>View for a unit created at run time (ejected crew, staged fight, later factory output).</summary>
        /// <summary>
        /// PropBreakables set) plus the street lamps NightLights adds later (Resources/OpenZ/Night/&lt;level&gt;_night.json "lamps", foot
        /// + arm 1.4 m) as keep-out discs for the sim's parking spots, so a re-parked start vehicle never stands on a lamp post.</summary>
        [System.Serializable] class KeepLampFile { public KeepLamp[] lamps; }
        [System.Serializable] class KeepLamp { public float x, z; }
        static void SetParkingKeepOut(string mapJson, string lv)
        {
            var list = new List<(int x, int y, int r)>();
            var env = GameObject.Find("Environment");
            if (env)
                foreach (Transform t in env.transform)
                {
                    if (!t.name.StartsWith("DEC_") || !t.gameObject.activeInHierarchy) continue;
                    var rs = t.GetComponentsInChildren<Renderer>(); if (rs.Length == 0) continue;
                    var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                    if (b.size.x > 7f || b.size.z > 7f || b.size.y > 12f) continue;                      // islands, ridges, big decor (as PropBreakables)
                    list.Add((Mathf.RoundToInt(b.center.x * 8f), Mathf.RoundToInt(-b.center.z * 8f), Mathf.CeilToInt(Mathf.Max(b.extents.x, b.extents.z) * 8f)));
                }
            var night = Resources.Load<TextAsset>($"OpenZ/Night/{lv}_night");
            var lamps = night ? JsonUtility.FromJson<KeepLampFile>(night.text) : null;
            if (lamps?.lamps != null) foreach (var l in lamps.lamps) list.Add((Mathf.RoundToInt(l.x * 8f), Mathf.RoundToInt(-l.z * 8f), 11));
            ZWorld.ParkingKeepOut = list;
            ZWorld.ParkingKeepOutMap = ZMapData.FromJson(mapJson).file;
            Debug.Log($"[OpenZ] DV66 parking keep-out: {list.Count} decor discs ({lamps?.lamps?.Length ?? 0} night lamps)");
        }

        public UnitView SpawnUnitView(ZObject o)
        {
            if (Views.TryGetValue(o.Id, out var existing)) return existing;
            if (o.IsHero) { var hv = OzHeroSquadView.SpawnView(this, o); if (hv) return hv; }   // DV108: Alan and Brad out of a vehicle are drawn as themselves (View/OzHeroSquadView.cs)
            string key = o.Kind == ZKind.Robot ? (o.Type == "laser" && !Prefab("Z_bs01_laser") ? "Z_robot" : "Z_bs01_" + o.Type) : "Z_" + o.Type;   // robots_v3: the v3 laser when present
            GameObject prefab = unitPrefabs == null ? null : System.Array.Find(unitPrefabs, g => g && g.name == key);
            if (!prefab && unitPrefabs != null && o.Kind == ZKind.Robot) prefab = System.Array.Find(unitPrefabs, g => g && g.name == (o.Type == "tough" ? "Z_tough" : "Z_robot"));
            prefab = OzHeavyTank.ForUnit(o, prefab);                                                      // 1.2 item 110: heavy tank v1 / v2 (the switch, or the testing overlay's pick; View/OzHeavyTank.cs)
            prefab = OzEvilZodView.ForUnit(o, prefab);                                                    // DV161: EvilZod's prefab, from Resources (View/OzEvilZodView.cs)
            prefab = OzV2Units.ForUnit(o, prefab);                                                        // 1.2 v2 units: the light tank v1 / v2 (its switch, or the testing overlay's pick; View/OzV2Units.cs)
            if (!prefab) { Debug.LogWarning("no view prefab for " + o.Type); return null; }
            var root = OzViewScans.ZodRoot();                                                 // 1.4 speed-up 4: the kept root (View/OzViewScans.cs)
            var go = Instantiate(prefab, root ? root.transform : null);
            go.name = $"{o.Type}_rt{o.Id}";
            var tp = go.GetComponent<TeamPaint>(); if (tp) tp.Team = o.Owner;
            OzEvilZodView.Dress(go, o);                                                                   // DV161: his own paint, before the view binds and paints
            var v = go.AddComponent<UnitView>(); v.Bind(this, o);
            OzEvilZodView.OnSpawn(v);                                                                     // DV161: his legs, upper body and effects
            Views[o.Id] = v;
            return v;
        }

        public GameObject Prefab(string key) => OzEvilZodView.PrefabFor(key, OzV2Units.PrefabFor(key, OzHeavyTank.PrefabFor(key, unitPrefabs == null ? null : System.Array.Find(unitPrefabs, g => g && g.name == key))));   // 1.2 item 110: "Z_heavy" = the model the switch draws; 1.2 v2 units: "Z_light" too

        public void HideStatic(int id) { if (StaticViews.TryGetValue(id, out var g) && g) g.SetActive(false); }
        public void RuinStatic(int id) { if (StaticViews.TryGetValue(id, out var g) && g) { var tp = g.GetComponent<TeamPaint>(); if (!tp) tp = g.AddComponent<TeamPaint>(); tp.Charred = 0.85f; } }
        public void ReviveStatic(int id) { if (StaticViews.TryGetValue(id, out var g) && g) { g.SetActive(true); var tp = g.GetComponent<TeamPaint>(); if (tp) tp.Charred = 0; } }

        /// <summary>Game speed at scene start: the serialized value (1 = Zod), or the explicit opt-in pref `oz_demo_speed`
        /// (default 1) when the bot demo is on. Never an implicit 2x/4x.</summary>
        public static float StartSpeed(float serialized, bool botDemo, float demoSpeed) => Mathf.Clamp(botDemo ? demoSpeed : serialized, 0.25f, 4f);

        /// <summary>Largest step of game time one frame may carry (s). The engine's own value is a third of a second: after one long
        /// frame the next one then runs up to 17 physics steps and 33 rule ticks, which makes it long too. With 0.1 s a slow frame is
        /// followed by at most 5 physics steps and 10 rule ticks: a hitch only slows the game down, it cannot feed itself. The same
        /// limit as the rule clock's own cap (40 ticks a frame at 4x speed).</summary>
        public const float MaxFrameStep = 0.1f;
        /// <summary>Ending profile (log only): rule ticks run in the last frame and the time they took (ms).</summary>
        public static int RuleTicksLastFrame; public static float RuleMsLastFrame;

        internal int SkirmishPlanningSeconds { get; private set; }
        double skirmishPlanningUntil, skirmishPlanningPausedAt = -1;
        internal double SkirmishPlanningLeft => System.Math.Max(0, skirmishPlanningUntil -
            (skirmishPlanningPausedAt >= 0 ? skirmishPlanningPausedAt : Time.realtimeSinceStartupAsDouble));
        internal void StartSkirmishPlanning(int seconds)
        {
            skirmishPlanningPausedAt = -1;
            SkirmishPlanningSeconds = seconds;
            if (seconds > 0) skirmishPlanningUntil = Time.realtimeSinceStartupAsDouble + seconds;
        }

        internal void PauseSkirmishPlanning(bool pause)
        {
            double now = Time.realtimeSinceStartupAsDouble;
            if (pause && SkirmishPlanningLeft > 0 && skirmishPlanningPausedAt < 0) skirmishPlanningPausedAt = now;
            if (pause || skirmishPlanningPausedAt < 0) return;
            skirmishPlanningUntil += now - skirmishPlanningPausedAt;
            skirmishPlanningPausedAt = -1;
        }

        // duel game clock
            void Update()
        {
            if (SkirmishPlanningSeconds > 0 && SkirmishPlanningLeft <= 0)
            {
                SkirmishPlanningSeconds = 0;
                OpenZ.Multiplayer.DuelRuntime.I?.Menus.Refresh();
            }
            if (OpenZ.Multiplayer.DuelRuntime.Drive(this)) return;
            RuleTicksLastFrame = 0; RuleMsLastFrame = 0f;
            if (paused || SkirmishPlanningLeft > 0) return;
            if (World == null) { if (!worldLost) { worldLost = true; Debug.LogError("[ZGame] World is null (domain reload during Play?): sim stopped; restart Play"); } return; }
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            RuleTicksLastFrame = Advance(World, ref acc, Time.deltaTime, gameSpeed);
            RuleMsLastFrame = (float)((System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency);
            var ruleLine = ZRuleProf.MinuteLine(World); if (ruleLine != null) Debug.Log(ruleLine);   // b109: rule tick timer per part, log only (Sim/ZRuleProf.cs)
        }

        /// <summary>The only sim clock (Zod ztime.cpp:21: game time = real seconds x game_speed): fixed 10 ms ticks fed by the
        /// real frame time x gameSpeed, at most 40 ticks a frame (a hitch only slows the game down). Static so the parity suite
        /// drives exactly this code on a synthetic real-time clock. Returns the ticks run.</summary>
        public static int Advance(ZWorld world, ref double acc, double realDt, double speed)
        {
            acc += realDt * speed;
            int n = 0;
            while (acc >= ZConst.TICK && n < 40) { world.Tick(ZConst.TICK); acc -= ZConst.TICK; n++; }
            if (n >= 40) acc = 0;                                   // never spiral after a hitch
            return n;
        }

        /// <summary>Part of the next 10 ms tick already elapsed (0..1).</summary>
        public float TickAlpha => paused ? 0f : Mathf.Clamp01((float)(acc / ZConst.TICK));

        // ------------------------------------------------------------------ coordinates
        public static Vector3 PxToWorld(double px, double py) => new Vector3((float)(px / 8.0), 0, (float)(-py / 8.0));
        public static Vector2Int WorldToPx(Vector3 w) => new Vector2Int(Mathf.RoundToInt(w.x * 8f), Mathf.RoundToInt(-w.z * 8f));

        void SetupWalkable()
        {
            if (WalkLayer < 0)
                for (int i = 30; i >= 8; i--)
                    if (string.IsNullOrEmpty(LayerMask.LayerToName(i)) && i != BuildingBreakerDebrisLayerHint) { WalkLayer = i; break; }
            if (WalkLayer < 0) WalkLayer = 0;
            walkMask = 1 << WalkLayer;
            if (ground) ground.gameObject.layer = WalkLayer;
            foreach (var kv in StaticViews)
            {
                var o = World.Get(kv.Key);
                if (o == null || !o.IsBridge) continue;
                if (World.Map.aiMap) SeatAiBridge(o, kv.Value);
                if (!o.Destroyed) MakeWalkable(kv.Value);
            }
        }

        void SeatAiBridge(ZObject bridge, GameObject art)
        {
            if (!ground || !art) return;
            bool vertical = bridge.Type == "bridge_vert";
            var center = PxToWorld(bridge.CenterX, bridge.CenterY);
            var axis = vertical ? Vector3.forward : Vector3.right;
            float halfSpan = (vertical ? bridge.HeightPix : bridge.WidthPix) / 16f;
            float Bank(Vector3 position)
            {
                return ground.Raycast(new Ray(position + Vector3.up * 200, Vector3.down), out var hit, 400)
                    ? hit.point.y : 0;
            }
            float near = Bank(center + axis * (halfSpan + .5f));
            float far = Bank(center - axis * (halfSpan + .5f));
            var p = art.transform.position;
            p.y = Mathf.Max(-.08f, (near + far) * .5f);
            art.transform.position = p;
            Physics.SyncTransforms();
        }
        const int BuildingBreakerDebrisLayerHint = 31;                                       // BuildingBreaker takes the highest free layer

        /// <summary>
        /// Give a bridge a clean walk surface on the walkable layer: the art mesh has plank gaps and railings, so the deck
        /// height is measured once (most common top-surface height over a 25 cm grid, railings excluded) and an invisible
        /// flat deck with a 2 m ramp at each end down to the bank is built as a child (it hides with the intact model).
        /// </summary>
        public void MakeWalkable(GameObject art)
        {
            if (!art || WalkLayer < 0 || art.transform.Find("WalkDeck")) return;
            var temps = new List<MeshCollider>();
            foreach (var mf in art.GetComponentsInChildren<MeshFilter>(true))
                if (mf.sharedMesh) { var mc = mf.gameObject.AddComponent<MeshCollider>(); mc.sharedMesh = mf.sharedMesh; temps.Add(mc); }
            var rs = art.GetComponentsInChildren<Renderer>(true); if (rs.Length == 0 || temps.Count == 0) { foreach (var t in temps) DestroyImmediate(t); return; }
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            var hits = new List<Vector3>(); var hist = new Dictionary<int, int>();
            for (float x = b.min.x; x <= b.max.x; x += 0.25f)
                for (float z = b.min.z; z <= b.max.z; z += 0.25f)
                {
                    float top = float.NegativeInfinity; var ray = new Ray(new Vector3(x, b.max.y + 5, z), Vector3.down);
                    foreach (var mc in temps) if (mc.Raycast(ray, out var h, 100)) top = Mathf.Max(top, h.point.y);
                    if (float.IsNegativeInfinity(top)) continue;
                    hits.Add(new Vector3(x, top, z)); int k = Mathf.RoundToInt(top * 20); hist[k] = hist.TryGetValue(k, out var c) ? c + 1 : 1;
                }
            foreach (var t in temps) DestroyImmediate(t);
            if (hist.Count == 0) return;
            float deckY = hist.OrderByDescending(kv => kv.Value).First().Key / 20f;
            float x0 = float.MaxValue, x1 = float.MinValue, z0 = float.MaxValue, z1 = float.MinValue;
            foreach (var h in hits) if (Mathf.Abs(h.y - deckY) < 0.12f) { x0 = Mathf.Min(x0, h.x); x1 = Mathf.Max(x1, h.x); z0 = Mathf.Min(z0, h.z); z1 = Mathf.Max(z1, h.z); }
            bool alongZ = (z1 - z0) >= (x1 - x0);                                                // ramps at the two short ends
            float ramp = art.name.Contains("desert") || level == "L02" ? 1.1f : 2f;           // Z kit desert bridges have their own ~1.1 m stone ramps
            float Bank(float x, float z) => ground && ground.Raycast(new Ray(new Vector3(x, 200, z), Vector3.down), out var gh, 400) ? gh.point.y : deckY;
            var v = new List<Vector3>();
            if (alongZ)
            {
                float zm = (x0 + x1) * 0.5f;
                v.Add(new Vector3(x0, Bank(zm, z0 - ramp), z0 - ramp)); v.Add(new Vector3(x1, Bank(zm, z0 - ramp), z0 - ramp));
                v.Add(new Vector3(x0, deckY, z0)); v.Add(new Vector3(x1, deckY, z0));
                v.Add(new Vector3(x0, deckY, z1)); v.Add(new Vector3(x1, deckY, z1));
                v.Add(new Vector3(x0, Bank(zm, z1 + ramp), z1 + ramp)); v.Add(new Vector3(x1, Bank(zm, z1 + ramp), z1 + ramp));
            }
            else
            {
                float zm = (z0 + z1) * 0.5f;
                v.Add(new Vector3(x0 - ramp, Bank(x0 - ramp, zm), z1)); v.Add(new Vector3(x0 - ramp, Bank(x0 - ramp, zm), z0));
                v.Add(new Vector3(x0, deckY, z1)); v.Add(new Vector3(x0, deckY, z0));
                v.Add(new Vector3(x1, deckY, z1)); v.Add(new Vector3(x1, deckY, z0));
                v.Add(new Vector3(x1 + ramp, Bank(x1 + ramp, zm), z1)); v.Add(new Vector3(x1 + ramp, Bank(x1 + ramp, zm), z0));
            }
            var go = new GameObject("WalkDeck") { layer = WalkLayer };
            go.transform.SetParent(art.transform, false);
            for (int i = 0; i < v.Count; i++) v[i] = go.transform.InverseTransformPoint(v[i]);
            var tri = new List<int>();
            for (int r = 0; r < 3; r++) { int a = r * 2; tri.AddRange(new[] { a, a + 2, a + 1, a + 1, a + 2, a + 3 }); }
            var m = new Mesh { name = "WalkDeck" }; m.SetVertices(v); m.SetTriangles(tri, 0); m.RecalculateNormals(); m.RecalculateBounds();
            if (m.normals[0].y < 0) { for (int i = 0; i < tri.Count; i += 3) { var x = tri[i + 1]; tri[i + 1] = tri[i + 2]; tri[i + 2] = x; } m.SetTriangles(tri, 0); m.RecalculateNormals(); }
            go.AddComponent<MeshCollider>().sharedMesh = m;
            OzViewScans.AddWalkDeck(go);                                                     // 1.4 speed-up 4: the walk decks' registry (View/OzViewScans.cs)
            OzBridgeRails.Prepare(art, go);                                                  // 1.5 kit view_notes11 N18: the railing cut into sections that break apart (View/OzBridgeRails.cs)
            Debug.Log($"[OpenZ] walk deck {art.name}: deck {deckY:F2} m, {(x1 - x0):F1} x {(z1 - z0):F1} m, ends {v.Count} pts");
        }

        public float GroundY(float x, float z)
        {
            if (GroundGridTry(x, z, out float gridY)) return gridY;   // 1.4 perf_ground_grid: the baked grid, the ray's answer within 0.5 mm; elsewhere the ray below answers as before (View/ZGame.GroundGrid.cs)
            if (walkMask != 0 && Physics.Raycast(new Ray(new Vector3(x, 200, z), Vector3.down), out var hit, 400, walkMask, QueryTriggerInteraction.Ignore)) return hit.point.y + CraterField.GroundAdd(hit.collider, x, z) - OzBridgeWobble.DipAt(hit.collider, x, z);
            if (ground && ground.Raycast(new Ray(new Vector3(x, 200, z), Vector3.down), out var h, 400)) return h.point.y + CraterField.DentRender(x, z);
            return 0;
        }

        /// <summary>The terrain collider only (no bridge decks or anything else on the walk layer): ground effects that must lie
        /// flat on the map (flag rings, zone washes; critic L17: a capture ring drew as a dome over a tank).</summary>
        public float TerrainY(float x, float z)
        {
            if (ground && ground.Raycast(new Ray(new Vector3(x, 200, z), Vector3.down), out var h, 400)) return h.point.y + CraterField.DentRender(x, z);
            return GroundY(x, z);
        }

        public bool GroundPoint(Ray ray, out Vector3 p)
        {
            p = default;
            if (walkMask != 0 && Physics.Raycast(ray, out var wh, 5000, walkMask, QueryTriggerInteraction.Ignore)) { p = wh.point; OzUePlatformViews.MouseRay(ray, ref p); return true; }   // 1.3 the Unit editor: the mouse on a ramp platform's rail or kerb (View/OzUePlatformView.cs); nothing in a level
            if (ground && ground.Raycast(ray, out var h, 5000)) { p = h.point; OzUePlatformViews.MouseRay(ray, ref p); return true; }
            var plane = new Plane(Vector3.up, Vector3.zero);
            if (plane.Raycast(ray, out float d)) { p = ray.GetPoint(d); OzUePlatformViews.MouseRay(ray, ref p); return true; }
            return false;
        }

        public Vector3 CenterWorld(ZObject o, bool ground = true)
        {
            var p = PxToWorld(o.PosX + o.WidthPix * 0.5, o.PosY + o.HeightPix * 0.5);
            if (ground) p.y = GroundY(p.x, p.z);
            return p;
        }

        /// <summary>Centre moved on by the part of the tick already elapsed, like the Zod client's SmoothMove (zobject.cpp:3802-3814:
        /// loc + dx x time since the last server update). Without it a 60 fps view jumps 1 or 2 ticks a frame (a 1.75 m/s grunt
        /// shows 1.05-2.1 m/s frame to frame) and a 144 Hz view stands still on 31 % of its frames.</summary>
        public Vector3 CenterWorldSmooth(ZObject o)
        {
            var p = CenterWorld(o, false);
            if (!o.IsMoving) return p;
            float t = TickAlpha * (float)ZConst.TICK;
            if (o.BehaviorUsesActualMotion)
                return p + new Vector3(o.BehaviorStepDx * t / 8f, 0f, -o.BehaviorStepDy * t / 8f);
            return p + new Vector3(o.Dx * t / 8f, 0f, -o.Dy * t / 8f);
        }
    }
}
