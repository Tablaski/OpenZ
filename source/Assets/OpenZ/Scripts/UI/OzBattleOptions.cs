using OpenZ.Multiplayer;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    internal static class OzBattleOptions
    {
        internal const string DamageLabel = "DAMAGED TANK OIL TRAILS", TankDamageKey = "oz_vk_tank_damage_effects";
        internal const string DamageTip = "Moving tanks leave oil trails at 30% health or less. Leaks intensify as health drops.";
        internal const string ShakeLabel = "DAMAGED TANK SHAKE", TankShakeKey = "oz_vk_tank_damage_shake";
        internal const string ShakeTip = "Tanks shake at 30% health or less, even when stationary. Reduced motion suppresses the shake.";
        internal static bool SoloDamage
        {
            get => PlayerPrefs.GetInt(TankDamageKey, 0) != 0;
            set { PlayerPrefs.SetInt(TankDamageKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }
        internal static bool Damage(ZWorld world) => world.HasDuelRules ? world.DuelTankDamageEffects : SoloDamage;
        internal static bool SoloShake
        {
            get => PlayerPrefs.GetInt(TankShakeKey, 0) != 0;
            set { PlayerPrefs.SetInt(TankShakeKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }
        internal static bool Shake(ZWorld world) => world.HasDuelRules ? world.DuelTankDamageShake : SoloShake;
        internal static string RadarName(RadarLossRule rule) => rule switch
        {
            RadarLossRule.Fog => "FOG OF WAR",
            RadarLossRule.Zoom => "ZOOM LOCK",
            RadarLossRule.FogAndZoom => "FOG + ZOOM",
            _ => "OFF"
        };
    }
}
