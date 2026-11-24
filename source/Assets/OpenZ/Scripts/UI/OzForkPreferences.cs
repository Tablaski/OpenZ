using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;

namespace OpenZ
{
    /// <summary>Versioned local history of presentation/game preferences. Never changes upstream storage identity or resets existing keys.</summary>
    public sealed class OzForkPreferences : MonoBehaviour
    {
        public const int Schema = 26;
        [Serializable] public sealed class Entry { public string key, kind, value; }
        [Serializable] public sealed class Snapshot { public int schema = Schema; public string fork = OzForkVersion.Number; public List<Entry> preferences = new List<Entry>(); }
        string lastJson;
        float nextSave;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            var go = new GameObject("OpenZ-VK preference history"); DontDestroyOnLoad(go); go.AddComponent<OzForkPreferences>();
        }
        void Start() { SaveSnapshot(); }
        void Update() { if (Time.realtimeSinceStartup >= nextSave) { nextSave = Time.realtimeSinceStartup + 30; SaveSnapshot(); } }
        void OnApplicationFocus(bool focused) { if (!focused) SaveSnapshot(); }
        void OnApplicationQuit() { SaveSnapshot(); }
        void SaveSnapshot()
        {
            try
            {
                PlayerPrefs.SetInt("oz_vk_settings_schema", Math.Max(Schema, PlayerPrefs.GetInt("oz_vk_settings_schema", 0))); PlayerPrefs.Save();
                var data = new Snapshot();
                void Add(string key, string kind)
                {
                    if (!PlayerPrefs.HasKey(key)) return;
                    string value = kind == "Int" ? PlayerPrefs.GetInt(key).ToString(System.Globalization.CultureInfo.InvariantCulture)
                        : kind == "Float" ? PlayerPrefs.GetFloat(key).ToString("R", System.Globalization.CultureInfo.InvariantCulture) : PlayerPrefs.GetString(key);
                    data.preferences.Add(new Entry { key = key, kind = kind, value = value });
                }
                Add("oz_announcer", "Int");
                Add("oz_apc_crew", "Int");
                Add("oz_armour_chip", "Int");
                Add("oz_attack_approach", "Int");
                Add("oz_autogate", "Int");
                Add("oz_behavior_draft_", "String");
                Add("oz_behavior_draft_0", "String");
                Add("oz_behavior_draft_1", "String");
                Add("oz_behavior_library_v1", "String");
                Add("oz_behavior_selected_0", "String");
                Add("oz_behavior_selected_1", "String");
                Add("oz_bfort_auto", "String");
                Add("oz_board_nearest", "Int");
                Add("oz_bot_commit", "Int");
                Add("oz_bot_finish", "Int");
                Add("oz_botmatch", "Int");
                Add("oz_building_reach", "Int");
                Add("oz_calm_default_v1", "Int");
                Add("oz_capture_after_board", "Int");
                Add("oz_capture_run", "Int");
                Add("oz_carpark_bot", "Int");
                Add("oz_cfeel_auto", "String");
                Add("oz_cheateb_auto", "String");
                Add("oz_clearance_sim", "Int");
                Add("oz_close_grab", "Int");
                Add("oz_crane_fix", "Int");
                Add("oz_crane_gun", "Int");
                Add("oz_croc_flee", "Int");
                Add("oz_croc_hurry", "Int");
                Add("oz_croc_water_cost", "Int");
                Add("oz_crocodiles", "Int");
                Add("oz_cursor_hw", "Int");
                Add("oz_demo_speed", "Float");
                Add("oz_dev", "Int");
                Add("oz_duel_chat_key", "Int");
                Add(OzForceBarOption.Pref, "Int");
                Add(OzForceBarOption.DuelPref, "Int");
                Add(Multiplayer.DuelRuntime.LastHostedRulesPreference, "String");
                foreach (string unit in OzBuildKeys.Units) Add(OzBuildKeys.Preference(unit), "Int");
                Add(OzBattleOptions.TankDamageKey, "Int");
                Add(OzBattleOptions.TankShakeKey, "Int");
                Add(OzBattleOptions.SoloRadarKey, "Int");
                Add(OzAiSoloChoice.Key, "Int");
                Add(OzAiSoloChoice.LastModernKey, "Int");
                Add(OzMapReadabilityOptions.RadarPref, "Int");
                Add(OzMapReadabilityOptions.LegacyRadarSweepPref, "Int");
                Add(OzMapReadabilityOptions.RadarSweepPref, "Float");
                Add(OzTacticalPause.PrefKey, "Int");
                Add(OzBindings.DefOf(OzAction.TacticalPause).pref, "Int");
                Add(OzRobotSizeOption.Pref, "Int");
                Add(OzLowPicture.Pref, "Int");
                Add(OzWaterPlayCalm.ReflectionPref, "Int");
                Add(HeatGuardRuntime.LimitPref, "Int");
                Add(OzQuality.LighterFogPref, "Int");
                Add(OzQuality.DisableFogPref, "Int");
                Add(OzQuality.LegacyReduceMsaaPref, "Int");
                Add(OzQuality.MsaaPref, "Int");
                Add(OzQuality.AntiAliasingPref, "Int");
                Add(OzQuality.CustomPref, "Int");
                Add(OzQuality.HardwareAaPref, "Int");
                Add(OzQuality.PostAaPref, "Int");
                Add(OzQuality.FogPref, "Int");
                Add("oz_duel_mic_device", "String");
                Add("oz_duel_mic_gain", "Float");
                Add("oz_duel_speaker_volume", "Float");
                Add("oz_duel_mic_mode", "Int");
                Add(Multiplayer.DuelMenus.ArmyPresetsPreference, "String");
                Add("oz_duel_sim", "Int");
                Add("oz_duel_talk_key", "Int");
                Add("oz_dusttrails", "Int");
                Add("oz_ebproof_auto", "String");
                Add("oz_edge_scroll", "Int");
                Add(OzSoloAllowedUnits.Preference, "String");
                Add(OzSoloMapTech.Preference, "String");
                Add(OzBindings.DefOf(OzAction.RestartProduction).pref, "Int");
                Add("oz_eject_lock", "Int");
                Add("oz_electrobot", "Int");
                Add("oz_electrobot_cpu", "Int");
                Add("oz_elloud_auto", "String");
                Add("oz_enter_fallback", "Int");
                Add("oz_exit_lanes", "Int");
                Add("oz_exposed_drivers", "Int");
                Add("oz_fgtilt_auto", "String");
                Add("oz_fire_on_move", "Int");
                Add(OzMapReadabilityOptions.LegacyFlagPref, "Int");
                Add(OzMapReadabilityOptions.FlagPref, "Float");
                Add("oz_flagview", "Int");
                Add("oz_fort_defender", "Int");
                Add("oz_fort_mortar", "Int");
                Add("oz_fort_rush", "Int");
                Add("oz_fortgun", "Int");
                Add("oz_fortgun_sim", "Int");
                Add("oz_gate_only", "String");
                Add("oz_gp3_auto", "String");
                Add("oz_gp53_auto", "String");
                Add("oz_grab_policy", "Int");
                Add("oz_grenade_boost", "Int");
                Add("oz_grenade_rule", "Int");
                Add("oz_gun_nose", "Int");
                Add("oz_heavy_pair", "Int");
                Add("oz_hp_always", "Int");
                Add("oz_htproof_auto", "String");
                Add("oz_hud", "Int");
                Add("oz_hud_intro", "Int");
                Add("oz_khaki", "Int");
                Add("oz_kit_cursor", "Int");
                Add("oz_knock_hold", "Int");
                Add("oz_l17_crews", "Int");
                Add("oz_l17_wrecks", "Int");
                Add("oz_loopaudit", "Int");
                Add("oz_los_sim", "Int");
                Add("oz_ltail_auto", "String");
                Add("oz_menu_take", "Int");
                Add("oz_music", "Int");
                Add("oz_music_drama_bag", "String");
                Add("oz_music_drama_last", "String");
                Add("oz_music_first_", "String");
                Add("oz_mv3fix_auto", "String");
                Add("oz_mv3proof_auto", "String");
                Add("oz_mv3proof_prevbrief", "Int");
                Add("oz_parked_paths", "Int");
                Add("oz_r3d_auto", "String");
                Add("oz_radar_dots", "Int");
                Add("oz_reduced_motion", "Int");
                Add("oz_repair_pad", "Int");
                Add("oz_robot_chatter", "Int");
                Add("oz_rocket_speed", "Int");
                Add("oz_run_over", "Int");
                Add("oz_shake", "Int");
                Add("oz_sheen", "Int");
                Add("oz_shore_beach", "Int");
                Add("oz_sndb_auto", "String");
                Add("oz_soft_cursor", "Int");
                Add("oz_squad_help", "Int");
                Add("oz_sv3_auto", "String");
                Add("oz_tank_duel_jeep", "Int");
                Add("oz_team_blue", "Int");
                Add("oz_territory_borders", "Int");
                Add("oz_testmode", "Int");
                Add("oz_tough_prepare", "Int");
                Add("oz_tower_diag", "Int");
                Add("oz_tower_perch", "Int");
                Add("oz_tower_seat", "Int");
                Add("oz_toy_barrels", "Int");
                Add("oz_tracks", "Int");
                Add("oz_ui_scale", "Float");
                Add("oz_veterans", "Int");
                Add("oz_vformation_sim", "Int");
                Add("oz_vol_effects", "Float");
                Add("oz_vol_effects_scale", "Int");
                Add("oz_vol_music", "Float");
                Add("oz_vol_robots", "Float");
                Add("oz_vol_voice", "Float");
                Add("oz_zod_briefing", "Int");
                Add("oz_zod_exact", "Int");
                string json = JsonUtility.ToJson(data, true);
                if (json == lastJson) return;
                string folder = Path.Combine(Application.persistentDataPath, "OpenZ-VK", "preferences", "schema-" + Schema);
                Directory.CreateDirectory(folder);
                string current = Path.Combine(folder, "current.json");
                if (File.Exists(current) && File.ReadAllText(current) == json) { lastJson = json; return; }
                File.WriteAllText(Path.Combine(folder, DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff") + ".json"), json);
                File.WriteAllText(current, json); lastJson = json;
            }
            catch (Exception error) { Debug.LogWarning("[OpenZ-VK] Could not back up preferences: " + error.Message); }
        }
    }
}
