using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.4 (Sim/ZEvilZod.AutoDash.cs), view only: the first <see cref="LogLines"/> auto dashes of a run go to the log (in a built exe that is Player.log, the proof
    /// outside the editor). The dash itself looks and sounds as an ordered one: OzEvilZodFx and the AudioDirector take the same EvilDashStart.
    /// </summary>
    public static class OzEvilZodAutoLog
    {
        /// <summary>How many auto dashes of a run go to the log.</summary>
        public static int LogLines = 8;
        /// <summary>Play check: auto dashes seen, lines logged.</summary>
        public static int Seen, Logged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Seen = Logged = 0; }

        /// <summary>CombatView: the rules' EvilDashStart with flag set (an auto dash).</summary>
        public static bool Wants(ZEvent e) => e.type == ZEventType.EvilDashStart && e.flag;

        /// <summary>CombatView, one line per event: a = EvilZod, b = the enemy he dashes at.</summary>
        public static void Handle(ZEvent e, ZObject a, ZObject b)
        {
            Seen++;
            if (Logged >= LogLines || a == null) return;
            Logged++;
            var w = ZGame.I ? ZGame.I.World : null;
            double d = b == null ? -1 : System.Math.Sqrt((double)(b.CenterX - a.CenterX) * (b.CenterX - a.CenterX) + (double)(b.CenterY - a.CenterY) * (b.CenterY - a.CenterY));
            double hit = w == null ? -1 : w.Time - a.LastEnemyHit;
            Debug.Log("[OpenZ EvilZod] auto dash (1.4): #" + a.Id + " at " + (b != null ? b.Type : "enemy") + " #" + e.otherId + " " + d.ToString("0") + " px away, hit "
                      + hit.ToString("0.00") + " s ago, burst " + e.radius + " ticks, cooldown " + (a.EvilCooldownTicks * ZConst.TICK).ToString("0.0") + " s (auto dashes so far " + Seen + ")");
        }
    }
}
