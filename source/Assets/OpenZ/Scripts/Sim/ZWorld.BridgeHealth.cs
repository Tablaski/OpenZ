using System;

namespace OpenZ.Sim
{
    /// <summary>
    /// <para>Zod: every bridge has bridge_building_health x MAX_UNIT_HEALTH = 2000 / 240 x 10000 = 83333 hit points (zsettings.cpp:261,
    /// zobject.cpp:150-153), the original Z's 2000 in its damage units. The weapons hit it with Zod's numbers, which are Z's except the Mobile
    /// Missile launcher: Z's 360 per missile, cut by Zod to 62 / 74 (8378 HP, zsettings.cpp: "62.0 / 74;//360.0 / 240"). So in Z a bridge fell to
    /// 2000 / 360 = 5.6 missiles, in Zod (and OpenZ until now) to 10-11.</para>
    /// <para>DV207: a bridge has <see cref="BridgeHitPoints"/> = 46546 hit points: Z's own share of a bridge per MLRS missile (360 of 2000) with
    /// Zod's missile, 83333 x (62 / 74) / (360 / 240). Every bridge, short or long, every level; every weapon hits it as before (counted, kit
    /// bridge_health_r3: MLRS 11 -> 6 missiles, Tough squad 54 -> 32 rockets, light tank 43-44 -> 25-26 shells, heavy tank 36 -> 20 shots,
    /// howitzer 22-23 -> 13 shells). Only the bridge's maximum changes: a bridge that starts destroyed (L17, L18) stays destroyed, its damaged look
    /// below half, the death blasts, the auto repair time (BridgeRepairSeconds / Zod's 600-660 s) and the crane's repair are unchanged; a repaired
    /// bridge comes back at its (new) full health.</para>
    /// <para>Switch <see cref="BridgeHealthDeviation"/>: View/ZGame.cs turns it on at every level start, off in 'Zod exact' (83333, Zod's).
    /// Headless default off; off in every counted check unless the check sets it (Editor/OzV11Kit.cs); the parity report's Zod rows and probes
    /// hold it off; its own row DV207 (Editor/OpenZParity.V15BridgeHealth.cs). Read once, when the world makes the bridge (ZWorld.CreateObject):
    /// integers only, no random draw, deterministic.</para>
    /// </summary>
    public sealed partial class ZWorld
    {
        /// <summary>DV207 switch (static like the others). View/ZGame.cs sets it at every level start (off in 'Zod exact').</summary>
        public static bool BridgeHealthDeviation = false;

        /// <summary>DV207: a bridge's hit points with the rule on: 83333 x (62 / 74) / (360 / 240) = 46546 (Z's 5.6 MLRS missiles per bridge).</summary>
        public const int BridgeHitPoints = 46546;

        /// <summary>Zod's bridge hit points (the rule off): 2000 / 240 x 10000, truncated as zobject.cpp does.</summary>
        public const int ZodBridgeHitPoints = 83333;

        public static IDisposable BridgeHealthOff() => new BridgeHealthHold(false);
        public static IDisposable BridgeHealthOn() => new BridgeHealthHold(true);
        sealed class BridgeHealthHold : IDisposable
        {
            readonly bool was = BridgeHealthDeviation;
            public BridgeHealthHold(bool on) { BridgeHealthDeviation = on; }
            public void Dispose() { BridgeHealthDeviation = was; }
        }

        /// <summary>DV207, ZWorld.CreateObject: the maximum hit points of a map item / building of this type: a bridge's with the rule on, else
        /// Zod's (zod = the settings' value x MAX_UNIT_HEALTH, as before).</summary>
        static int BridgeMaxHealth(string type, int zod) =>
            BridgeHealthDeviation && (type == "bridge_vert" || type == "bridge_horz") ? BridgeHitPoints : zod;
    }
}
