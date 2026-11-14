using System;
using System.Collections.Generic;

namespace OpenZ.Sim
{
    /// <summary>
    /// OpenZ deviation DV148 "the computer builds mechs AND medium tanks" (1.2 owner item 180). DV145 (Sim/ZMTank.cs, ZBot.MTankPick) let
    /// the computer build the medium tank where Zod's pick is the mech, choosing whichever of the two its team had FEWER of on the field,
    /// the mech when even. That rule gave long runs of one type: every producer decided from the same snapshot (none counted the builds
    /// already under way), the start and captured mechs counted, and losses reset it, so a computer whose vehicles die soon after they come
    /// out (against a player) is at "even" (0 / 0) at nearly every decision and builds the mech each time; headless matches showed runs of
    /// up to 11 mechs in a row.
    /// DV148: each time a producer's next mech-tier unit is decided (its pick is the mech and it can make the medium tank), the computer
    /// counts what its team has BUILT of the two this game (its producers' finished units; captured or start vehicles do not count) plus
    /// what its other producers are making or about to make, and takes the one it has fewer of; when even, a coin on its own random
    /// stream (seeded from the bot's seed like its other draws, so no other stream moves) decides. So the two come out about one each, in a
    /// mixed order, whatever the losses. The choice holds for that producer until it finishes a unit (BuiltCount), so the build is never
    /// swapped back and forth; DV145's rules stay: a mech / medium tank build already <see cref="MTANK_KEEP_S"/> s along is finished, and
    /// DV115's vehicle in the making at 8:00 is finished first.
    /// Switch: <see cref="ZWorld.MechMixDeviation"/> (View/ZGame.cs: on, off in 'Zod exact'; headless default off). Off = DV145's own rule
    /// (MTankPick) exactly. Deterministic: counts of the bot's own team, the bot's own seeded stream drawn only on a tie, the same
    /// STOP_BUILDING + START_BUILDING commands.
    /// </summary>
    public sealed partial class ZBot
    {
        /// <summary>DV148 statistics (the counted check): mech-tier choices made (mechs / medium tanks), those a coin decided, and the team's
        /// finished mechs / medium tanks this game (counted whatever the switch).</summary>
        public int MechMixMechs { get; private set; }
        public int MechMixTanks { get; private set; }
        public int MechMixCoins { get; private set; }
        public int BuiltMechs { get; private set; }
        public int BuiltTanks { get; private set; }
        /// <summary>The team's mech-tier choices in order ("M" mech, "T" medium tank), for the checks.</summary>
        public string MechMixOrder => mixOrder.ToString();

        ZRng mixRng;                                                                                 // DV148: the bot's own stream for the coin (ZBot constructor)
        readonly Dictionary<int, (int built, string type)> mixChoice = new Dictionary<int, (int built, string type)>();
        readonly System.Text.StringBuilder mixOrder = new System.Text.StringBuilder();

        /// <summary>The ZBot constructor: the coin's stream from the bot's seed (its own, apart from the bot's rand()).</summary>
        static ZRng MixRng(int seed, Team team) => new ZRng(unchecked(seed * 6007 + (int)team * 131 + 17));

        /// <summary>ZWorld.BuildUnit (through ZWorld.MechMixNoteBuilt): one of this team's producers finished a mech or a medium tank.</summary>
        internal void NoteMechTierBuilt(string type)
        {
            if (type == ZMTank.Mech) BuiltMechs++; else if (type == ZMTank.Type) BuiltTanks++;
        }

        /// <summary>ChooseBuildOrders, in DV145's place: DV148's choice while its switch is on, DV145's MTankPick otherwise. Public for the checks.</summary>
        public string MechMixPick(ZObject b, string zodPick, string pick)
        {
            if (!ZWorld.MechMixDeviation) return MTankPick(b, zodPick, pick);
            if (!ZWorld.MTankDeviation || b == null || !ZBuildLists.InList(b.Type, b.BuildLevel, ZMTank.Type)) return pick;
            if (zodPick == ZMTank.Mech && pick != zodPick && b.Building && b.CurUnit == ZMTank.Type && b.BInit + TIME_EPS < HEAVY_FROM) return ZMTank.Type;   // DV115: the one in the making at 8:00 is finished first
            if (pick != ZMTank.Mech) return pick;
            if (b.Building && (b.CurUnit == ZMTank.Mech || b.CurUnit == ZMTank.Type) && W.Time - b.BInit > MTANK_KEEP_S) return b.CurUnit;   // DV145: a build under way is finished
            if (mixChoice.TryGetValue(b.Id, out var c) && c.built == b.BuiltCount) return c.type;      // this producer's choice holds until it finishes a unit
            int mechs = BuiltMechs, tanks = BuiltTanks;
            foreach (var p in W.Objects)                                                             // what the team's other producers make or are about to make
            {
                if (p == b || p.Owner != Team || p.Kind != ZKind.Building || !p.IsProducer || p.Destroyed) continue;
                string pt = p.Building ? p.CurUnit : mixChoice.TryGetValue(p.Id, out var pc) && pc.built == p.BuiltCount ? pc.type : null;
                if (pt == ZMTank.Mech) mechs++; else if (pt == ZMTank.Type) tanks++;
            }
            string t;
            if (mechs != tanks) t = mechs < tanks ? ZMTank.Mech : ZMTank.Type;
            else
            {
                if (mixRng == null) mixRng = new ZRng(unchecked((int)Team * 7919 + 104729));          // only when the constructor hook is missing (never in the game)
                t = mixRng.Next(2) == 0 ? ZMTank.Mech : ZMTank.Type;
                MechMixCoins++;
            }
            mixChoice[b.Id] = (b.BuiltCount, t);
            if (t == ZMTank.Type) MechMixTanks++; else MechMixMechs++;
            mixOrder.Append(t == ZMTank.Type ? 'T' : 'M');
            return t;
        }
    }

    public sealed partial class ZWorld
    {
        /// <summary>DV148 the computer's mech / medium tank mix (Sim/ZBot.MechMix.cs). View/ZGame.cs sets it at every start (off in 'Zod
        /// exact'); headless default off (DV145's own rule).</summary>
        public static bool MechMixDeviation = false;

        /// <summary>ZWorld.BuildUnit, after a unit is made: a mech or a medium tank tells the computer that plays its producer's team.</summary>
        void MechMixNoteBuilt(ZObject b, string type)
        {
            if (b == null || (type != ZMTank.Mech && type != ZMTank.Type)) return;
            foreach (var bot in Bots) if (bot.Team == b.Owner) bot.NoteMechTierBuilt(type);
        }

        /// <summary>The parity's Zod rows and the counted checks: DV145's own rule while this lives (a Play session leaves the switch on).</summary>
        public static IDisposable MechMixOff() => new MechMixHold();
        sealed class MechMixHold : IDisposable
        {
            readonly bool was = MechMixDeviation;
            public MechMixHold() { MechMixDeviation = false; }
            public void Dispose() { MechMixDeviation = was; }
        }
    }
}
