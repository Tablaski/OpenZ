namespace OpenZ.Sim
{
    /// <summary>
    /// Counted before: one sniper squad beat two grunt squads, two psycho squads and two jeeps in 30 of 30 fights, nearly always
    /// 0.8 -> 0.75 and its reload 0.40 -> 0.45 s (here; its driver shot stays Zod's 0.8: snipers stay the fastest driver killers); its headshot chance 15 -> 8 %
    /// (ZWorld.HeadshotPct, Sim/ZObject.Headshot.cs); grunt and psycho rifles now hurt a sniper (ZWorld.RiflePair, Sim/ZObject.RiflePair.cs).
    /// Hero squads are grunt-based: their own numbers are never touched (their rifles count as grunts' against a sniper, as against grunts).
    /// A sniper crew in an APC and a sniper at a tank's hatch get the same numbers (HitChanceOf / ReloadOf).
    /// </summary>
    public sealed partial class ZWorld
    {
        /// <summary>The 1.2 sniper balance switch. Static like SnipeDeviation: View/ZGame.cs sets it at every start (off in 'Zod exact');
        /// off by default so headless worlds (parity rows, tests) keep Zod's numbers.</summary>
        public static bool SniperBalanceDeviation = false;
        public static double SniperHitChance = 0.75, SniperReload = 0.45;

        static void SniperBalance(ZObject o)
        {
            o.DamageChance = SniperHitChance; o.AttackSpeed = SniperReload;
        }

        /// <summary>The hit chance and the reload of a robot type's weapon wherever a row is read after creation (an APC's sniper
        /// crew, a tank's hatch driver, DV15 / DV87): the 1.2 numbers for a sniper while the switch is on, else the row's own.</summary>
        public static double HitChanceOf(string type, ZUnitSettings u) => SniperBalanceDeviation && type == "sniper" ? SniperHitChance : u.attack_damage_chance;
        public static double ReloadOf(string type, ZUnitSettings u) => SniperBalanceDeviation && type == "sniper" ? SniperReload : u.attack_speed;

        /// <summary>The Zod rows and the counted checks measure Zod's sniper whatever the last Play session left (like ProneTargetOff).</summary>
        public static SniperBalanceHold SniperBalanceOff() => new SniperBalanceHold();
        public sealed class SniperBalanceHold : System.IDisposable
        {
            readonly bool was;
            internal SniperBalanceHold() { was = SniperBalanceDeviation; SniperBalanceDeviation = false; }
            public void Dispose() { SniperBalanceDeviation = was; }
        }
    }
}
