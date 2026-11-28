using OpenZ.Multiplayer;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    internal static class OzBattleOptions
    {
        internal const string SoloLabel = "GAMING OPTIONS", RadarLabel = "RADAR LOSS", SoloRadarKey = "oz_vk_solo_radar_loss";
        internal const string ClassicMissileLabel = "MISSILE LAUNCHER", ClassicMissileKey = "oz_vk_solo_classic_missile_launcher";
        internal static bool ClassicMissileLauncher
        {
            get => PlayerPrefs.GetInt(ClassicMissileKey, 0) == 1;
            set { PlayerPrefs.SetInt(ClassicMissileKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        internal static RadarLossRule SoloRadar
        {
            get
            {
                int value = PlayerPrefs.GetInt(SoloRadarKey, 0);
                return value >= 0 && value <= (int)RadarLossRule.FogAndZoom ? (RadarLossRule)value : RadarLossRule.Off;
            }
            set { PlayerPrefs.SetInt(SoloRadarKey, (int)value); PlayerPrefs.Save(); }
        }
        internal static string RadarTip(RadarLossRule rule) => rule switch
        {
            RadarLossRule.Fog => "Hide enemy movement beyond your territory and units' sight.",
            RadarLossRule.Zoom => "Lock the camera at a fixed zoom level.",
            RadarLossRule.FogAndZoom => "Hide unseen enemies and lock the camera at a fixed zoom level.",
            _ => "Keep the standard OpenZ radar behavior."
        };
        internal const string DamageLabel = "DAMAGED TANKS OIL TRAILS & SHAKE", TankDamageKey = "oz_vk_tank_damage_effects";
        internal const string DamageTip = "Tanks at 30% health or less shake and leave oil trails while moving. Leaks intensify as health drops. Reduced motion suppresses the shake.";
        internal const string TankShakeKey = "oz_vk_tank_damage_shake";
        internal static bool SoloDamage
        {
            get => PlayerPrefs.GetInt(TankDamageKey, 0) != 0 || PlayerPrefs.GetInt(TankShakeKey, 0) != 0;
            set
            {
                PlayerPrefs.SetInt(TankDamageKey, value ? 1 : 0);
                PlayerPrefs.SetInt(TankShakeKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }
        internal static bool Damage(ZWorld world) => SoloDamage;
        internal static bool Shake(ZWorld world) => SoloDamage;
        internal static string RadarName(RadarLossRule rule) => rule switch
        {
            RadarLossRule.Fog => "FOG OF WAR",
            RadarLossRule.Zoom => "ZOOM LOCK",
            RadarLossRule.FogAndZoom => "FOG + ZOOM",
            _ => "OFF"
        };
    }
}
