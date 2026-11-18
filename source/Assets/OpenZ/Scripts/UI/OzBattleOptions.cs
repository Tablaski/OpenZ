using OpenZ.Multiplayer;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    internal static class OzBattleOptions
    {
        internal const string DamageLabel = "DAMAGED TANK SMOKE & OIL", TankDamageKey = "oz_vk_tank_damage_effects";
        internal static bool SoloDamage
        {
            get => PlayerPrefs.GetInt(TankDamageKey, 0) != 0;
            set { PlayerPrefs.SetInt(TankDamageKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }
        internal static bool Damage(ZWorld world) => world.HasDuelRules ? world.DuelTankDamageEffects : SoloDamage;
        internal static string RadarName(RadarLossRule rule) => rule switch
        {
            RadarLossRule.Fog => "FOG OF WAR",
            RadarLossRule.Zoom => "ZOOM LOCK",
            RadarLossRule.FogAndZoom => "FOG + ZOOM",
            _ => "OFF"
        };
    }
}
