// OpenZ (GPL-3.0)
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.5 review R11 (view only): every ground dust puff of the moving units goes on the levels' lit sand wake dust (the channel m12_dust_wake
    /// of OpenZFxPlayer: the lit smoke flipbook, soft irregular lobes, sun + ambient + self shadow, so dark at night; the colour of the ground
    /// it came from), the dust the L02 desert shows behind its tanks since pass 6b (gate A14).
    /// <para>What still drew round discs: (1) DustTrails.Emit, its own OpenZ/TrackDust ball per puff (unlit, so as bright at night as by day):
    /// the robots' footfall puffs, the mech's step dust and every wake puff when the lit channel was not there; (2) the MAMMOTH's and the
    /// BIZON's track dust (OzBigTanksDust.DustPuff: its own particle system, a soft round young puff at the Unit editor's close camera: the
    /// pale circles behind both tanks in the clip). Both now emit on that channel with their own place, speed, size, life, strength and ground
    /// colour (a big-tank puff's final size given as the channel's start size, which grows 0.5 -> 2.3 x like m12_dust). The Unit editor (desert
    /// dunes / snow floors: OzUeDust) and every level take the same road. The big tanks' exhaust is not dust: unchanged.</para>
    /// Off (<see cref="On"/> false) = the puffs as before.
    /// </summary>
    public static class OzLevelDust
    {
        public static bool On = true;
        /// <summary>A big-tank puff's final size (m) -> the channel's size (it grows 0.5 -> 2.3 x).</summary>
        public static float BigTankSizeK = 1f / 2.3f;
        static readonly Color Pale = new Color(0.9f, 0.86f, 0.8f);
        /// <summary>Counted proof: DustTrails puffs and big-tank puffs drawn on the channel.</summary>
        public static int Puffs, BigTankPuffs;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Puffs = BigTankPuffs = 0; }

        static bool Ready(out OpenZFxPlayer fx)
        {
            fx = On && OpenZFxCatalog.WakeChannel ? OpenZFxPlayer.I : null;
            return fx && fx.isActiveAndEnabled && fx.HasChannel(OpenZFxPlayer.WakeChannelName);
        }

        /// <summary>DustTrails.Emit: one puff (its size and life as DustTrails reads them: the size grows 0.5 -> 2.3 x as the channel's).
        /// False = no channel here: DustTrails draws its own puff as before.</summary>
        public static bool Emit(Vector3 at, Vector3 vel, float size, float life, float alpha, Color srgb, float brightness)
        {
            if (!Ready(out var fx)) return false;
            var c = Color.Lerp(srgb, Pale, OpenZFxCatalog.WakePale) * brightness; c.a = 1f;
            fx.EmitChannel(OpenZFxPlayer.WakeChannelName, at, vel, life, size, c, alpha * OpenZFxCatalog.WakeAlphaK);
            Puffs++;
            return true;
        }

        /// <summary>OzBigTanksDust.DustPuff: one big-tank track puff (c = the ground colour and its strength in alpha). False = no channel.</summary>
        public static bool BigTankPuff(Vector3 at, Vector3 vel, float life, float finalSize, Color c)
        {
            if (!Ready(out var fx)) return false;
            float a = c.a; c.a = 1f;
            fx.EmitChannel(OpenZFxPlayer.WakeChannelName, at, vel, life, finalSize * BigTankSizeK, c, a * OpenZFxCatalog.WakeAlphaK);
            BigTankPuffs++;
            return true;
        }

        public static string Status() => $"level dust {(On ? "on" : "off")}: DustTrails puffs on the wake channel {Puffs}, big-tank puffs {BigTankPuffs}";
    }
}
