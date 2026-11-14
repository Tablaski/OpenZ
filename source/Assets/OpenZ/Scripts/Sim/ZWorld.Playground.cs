using System;
using System.Collections.Generic;

namespace OpenZ.Sim
{
    /// <summary>
    /// 1.2 item 6 (the Unit editor's playground, View/OzUnitEditor.cs): the switches and helpers of the sandbox world. Every switch is a
    /// field of ONE world and defaults off, so every match, the headless bot matches and the parity suite are unchanged (no rule of a
    /// match changes, no deviation number):
    ///  * <see cref="NoGameEnd"/>: CheckEndGame never ends the game, so a one-sided or an empty field keeps running (hook line in
    ///    Sim/ZWorld.Rules.cs CheckEndGame);
    ///  * <see cref="FlagHoldSec"/>: a flag that changed its owner cannot change again for that many seconds; 0 = the game's rule (taken
    ///    back at once). The capture itself stays the game's (CheckFlagCaptures every 0.2 s, the first robot or vehicle of another team,
    ///    never the shuttle, a gun or a fort defender): hook line in CheckFlagCaptures;
    ///  * <see cref="PlaceObstacle"/>: the holographic obstacle on the harbour-wreck rule (DV39, Sim/ZWorld.Wrecks.cs): a neutral map item
    ///    of <see cref="WreckType"/> with a square footprint, every footprint tile a destroyable impassable (robots and vehicles path round
    ///    it, EngageBarrierBetween stops every shot across it, arcing too; only explosives attack it, any splash hurts it; its tiles get
    ///    their map class back when it dies or is removed);
    ///  * <see cref="RemoveNow"/>: an object (a squad with every member) taken out of the world at once, with no death (no Death event,
    ///    no debris), also while the sim is paused: the game's own DeleteObject (group hand-over, barriers released, every object forgets
    ///    it), the flag list and the playable-shuttle slot cleared too.
    /// Pure C# like the rest of the Sim folder (headless checks: k6 tools/p1_headless.py).
    /// </summary>
    public sealed partial class ZWorld
    {
        /// <summary>1.2 item 6: this world never ends its game (the playground). Default off.</summary>
        public bool NoGameEnd;
        /// <summary>1.2 item 6: seconds a flag holds after it changed its owner (the playground: 1). Default 0 = the game's rule.</summary>
        public double FlagHoldSec;
        /// <summary>The obstacle's default hit points (the playground's holo obstacle).</summary>
        public const int PlaygroundObstacleHp = 10000;

        Dictionary<int, double> flagChangedAt;

        /// <summary>CheckFlagCaptures hook: may flag f change its owner now? True at once while <see cref="FlagHoldSec"/> is 0 (nothing is
        /// recorded). Else false inside the hold; true after it, and the time is recorded, because an award always follows a true.</summary>
        bool FlagCaptureAllowed(ZObject f)
        {
            if (FlagHoldSec <= 0 || f == null) return true;
            if (flagChangedAt == null) flagChangedAt = new Dictionary<int, double>();
            if (flagChangedAt.TryGetValue(f.Id, out double at) && Time - at < FlagHoldSec) return false;
            flagChangedAt[f.Id] = Time;
            return true;
        }

        /// <summary>Sim time of the flag's last change of owner under the hold rule (-1 = never, or no hold in this world).</summary>
        public double FlagChangedAt(ZObject f) => f != null && flagChangedAt != null && flagChangedAt.TryGetValue(f.Id, out double at) ? at : -1;

        /// <summary>Can a square obstacle of tiles x tiles stand with its top-left tile at (tx, ty)? why (when not): "edge" (outside the map
        /// or on its border tile), "ground" (not a ground or road tile), "obstacle" (another obstacle or a barrier), "flag", "empty" (a
        /// vehicle or gun nobody crews), "unit" (a robot, vehicle, gun or shuttle).</summary>
        public bool ObstacleSpotFree(int tx, int ty, int tiles, out string why)
        {
            why = null;
            if (Map == null || tiles < 1) { why = "edge"; return false; }
            if (tx < 1 || ty < 1 || tx + tiles > Map.width - 1 || ty + tiles > Map.height - 1) { why = "edge"; return false; }
            var probe = new ZObject { WidthPix = tiles * 16, HeightPix = tiles * 16 };
            probe.SetCords(tx * 16, ty * 16);
            foreach (var o in Objects)
                if (!o.Destroyed && o.KillAt < 0 && o.Type == WreckType && probe.IntersectsObject(o)) { why = "obstacle"; return false; }
            foreach (var o in Objects)
                if (!o.Destroyed && o.KillAt < 0 && o.Type == "flag" && probe.IntersectsObject(o)) { why = "flag"; return false; }
            foreach (var o in Objects)
                if (!o.Destroyed && o.KillAt < 0 && (o.Kind == ZKind.Vehicle || o.Kind == ZKind.Cannon) && o.Owner == Team.Neutral && probe.IntersectsObject(o)) { why = "empty"; return false; }
            foreach (var o in Objects)
                if (!o.Destroyed && o.KillAt < 0 && o.IsUnit && probe.IntersectsObject(o)) { why = "unit"; return false; }
            for (int j = ty; j < ty + tiles; j++)
                for (int i = tx; i < tx + tiles; i++)
                {
                    var c = Map.ClassAt(i, j);
                    if (c != ZClass.Ground && c != ZClass.Road) { why = "ground"; return false; }
                    if (!Path.TilePassable(i, j, true)) { why = "obstacle"; return false; }
                }
            return true;
        }

        /// <summary>The playground's holo obstacle: a neutral map item of <see cref="WreckType"/>, tiles x tiles with its top-left tile at
        /// (tx, ty), hp hit points, made exactly as AddHarbourWrecks makes a wreck (Sim/ZWorld.Wrecks.cs): created, sized, marked a
        /// destroyable impassable, its whole footprint set impassable (destroyable). Null (why = <see cref="ObstacleSpotFree"/>'s word)
        /// when it cannot stand there.</summary>
        public ZObject PlaceObstacle(int tx, int ty, int tiles, int hp, out string why)
        {
            if (!ObstacleSpotFree(tx, ty, tiles, out why)) return null;
            var o = CreateObject("map_item", WreckType, tx * 16, ty * 16, Team.Neutral);
            o.WidthPix = o.HeightPix = tiles * 16; o.SetCords(tx * 16, ty * 16);
            o.IsDestroyableImpass = true;                                                 // SetClassFlags gave AttackedByExplosives
            o.MaxHealth = o.Health = Math.Max(1, hp);
            SetMapImpassables(o);                                                         // the whole footprint, destroyable
            return o;
        }

        /// <summary>Takes o out of the world at once, without a death: a squad member takes its whole squad (the members first, then the
        /// leader, so no hand-over happens), each through the game's DeleteObject (group, barriers: an obstacle's tiles get their map
        /// class back, every object forgets it). Its KillAt is set, so the view and the selection treat it as gone. The flag list, the flag
        /// hold and the playable-shuttle slot forget it too. Returns the objects removed (0 = not in this world).</summary>
        public int RemoveNow(ZObject o)
        {
            if (o == null) return 0;
            var lead = o.Leader != null && Get(o.Leader.Id) == o.Leader ? o.Leader : o;
            int n = 0;
            for (int i = lead.Minions.Count - 1; i >= 0; i--)
                if (TakeOutNow(lead.Minions[i])) n++;
            if (TakeOutNow(lead)) n++;
            return n;
        }

        /// <summary>1.3 item 2: the Unit editor's NUKE blasts queued so far in this world (checks).</summary>
        public int PlaygroundNukes;

        /// <summary>
        /// A wave from (x, y) px that takes ALL the HP of every object whose centre is under
        /// radiusPx from it (robots, vehicles, guns, the shuttle on the ground, obstacles, crates; both teams and neutral; flags are immune
        /// as to every blast), each one at the moment the levels' shockwave ring reaches it, not all at the impact:
        /// <list type="bullet">
        /// <item>The front runs on the ring's own curve (<see cref="NukeWaveRadiusM"/> = View/FxV3/OpenZFortShockwave.RadiusAt). pace = the
        /// rule seconds per second of the ring's clock = the field's game speed at the impact (x4: 4), so the deaths keep pace with the
        /// ring the player sees at every speed; pause stops both clocks.</item>
        /// <item>The wave is one missile of the world's own list per rule tick (step k bursts in the k-th tick after the call), each with
        /// the front's radius one tick earlier (k - 1 ticks of the ring), rounded up to a whole px and capped at radiusPx; the last step
        /// is the full ring. So a death comes at most 2 ticks after the front passed its centre (0.7 to 2 at x1 .. x4), never before it,
        /// nearest first; the tick held back covers the part of a tick the field's clock may run ahead of the frame.</item>
        /// <item>An object is decided once, by its distance at the tick the front passes it (<see cref="PlaygroundNukeReach"/>): a unit that
        /// walks into the ring ahead of the front dies when the front catches it; one that walks out past radiusPx first lives (the
        /// rules' wave ends at radiusPx; the picture's wave only rocks it).</item>
        /// <item>The steps go through ProcessMissileDamage, so the deaths, the debris and the crews go the game's own way, marked
        /// <see cref="ZMissile.nuke"/>: no MissileExploded event (the bomber's strike draws the blast), no fall-off.</item>
        /// </list>
        /// No random draw; the steps are fixed at the call (deterministic: rule ticks only). Never called in a level. CLEAR FIELD drops the
        /// rest of a running wave (<see cref="PlaygroundNukeCancel"/>).</summary>
        /// <remarks>
        /// killPx = the inner ring that takes ALL the HP (both teams, whoever called it: the wave is Team.Neutral); from killPx
        /// to radiusPx the outer band takes <see cref="NukeBandMaxPct"/> % of the full HP at killPx down to <see cref="NukeBandMinPct"/> % at radiusPx
        /// (<see cref="PlaygroundNukeDamage"/>). killPx under 0 or over radiusPx = radiusPx (all the HP everywhere in the ring, the old wave).</remarks>
        public void PlaygroundNuke(int x, int y, int radiusPx, double pace = 1.0, int killPx = -1)
        {
            if (radiusPx <= 0) return;
            if (double.IsNaN(pace) || pace <= 0.0) pace = 1.0;
            pace = Math.Max(0.05, Math.Min(40.0, pace));
            if (killPx < 0 || killPx > radiusPx) killPx = radiusPx;
            var wv = new PlaygroundNukeWave { Number = PlaygroundNukes + 1, X = x, Y = y, RadiusPx = radiusPx, KillPx = killPx, T0 = Time, Pace = pace };
            for (int k = 1; k <= 100000; k++)
            {
                double front = NukeWaveRadiusM((k - 1) * ZConst.TICK / pace) * 8.0;                                 // px (8 a metre); the front one tick back: never ahead of the ring
                int r = front >= radiusPx ? radiusPx : (int)Math.Ceiling(front);                                   // a centre under it: the front of this very tick is at least 0.35 m further (x4, 15 m)
                missiles.Add(new ZMissile { x = x, y = y, radius = r, damage = ZConst.MAX_UNIT_HEALTH, team = Team.Neutral, nuke = true, explodeTime = Time + (k - 0.5) * ZConst.TICK });   // bursts in the k-th tick from now (the half tick: no float slip); added between ticks (the View's impact), so no queue tick
                wv.Steps++;
                if (r >= radiusPx) break;
            }
            (nukeWaves ??= new List<PlaygroundNukeWave>()).Add(wv);
            PlaygroundNukes++;
        }

        // 1.3 item 2: the numbers of the levels' shockwave ring (View/FxV3/OpenZFortShockwave.RadiusAt, the inverse of OpenZEnding.Delay):
        // 4 m at the blast, 4 + 91 (1 - (1 - t/1.8)^3) m up to 95 m at 1.8 s, then OpenZEnding.WaveOuterSpeed (35) m/s. Copied because the
        // Sim is pure C#; the counted check (View/OzUeNukeCheck.cs N9) proves this curve and the View's are the same.
        public const double NukeWaveStartM = 4.0, NukeWaveGrowM = 91.0, NukeWaveEaseSec = 1.8, NukeWaveOuterMps = 35.0;

        /// <summary>1.3 item 2: the front's radius (m) t seconds of the ring's clock after the blast (= OpenZFortShockwave.RadiusAt).</summary>
        public static double NukeWaveRadiusM(double t)
        {
            if (t <= 0.0) return NukeWaveStartM;
            if (t <= NukeWaveEaseSec) { double k = 1.0 - t / NukeWaveEaseSec; return NukeWaveStartM + NukeWaveGrowM * (1.0 - k * k * k); }
            return NukeWaveStartM + NukeWaveGrowM + (t - NukeWaveEaseSec) * NukeWaveOuterMps;
        }

        /// <summary>1.3 item 2: seconds of the ring's clock until the front reaches distM (the inverse of <see cref="NukeWaveRadiusM"/>; 0
        /// inside the first 4 m, where the ring starts).</summary>
        public static double NukeWaveTimeAt(double distM)
        {
            if (distM <= NukeWaveStartM) return 0.0;
            double top = NukeWaveStartM + NukeWaveGrowM;
            if (distM <= top) return NukeWaveEaseSec * (1.0 - Math.Pow(1.0 - (distM - NukeWaveStartM) / NukeWaveGrowM, 1.0 / 3.0));
            return NukeWaveEaseSec + (distM - top) / NukeWaveOuterMps;
        }

        /// <summary>1.3 item 2: one NUKE's wave in the rules: its number (1 = the first of this world), centre and kill radius (px), the rule
        /// time of the call, the pace (rule seconds per ring second), the steps queued, and the objects its front has reached.</summary>
        public sealed class PlaygroundNukeWave
        {
            /// <summary>KillPx: the inner ring that takes all the HP (1.3 final); RadiusPx: the wave's outer edge (the band between them is damaged).</summary>
            public int Number, X, Y, RadiusPx, KillPx, Steps;
            public double T0, Pace;
            /// <summary>CLEAR FIELD came before its last step: the rest was dropped.</summary>
            public bool Cancelled;
            internal readonly HashSet<int> reached = new HashSet<int>();
            public int Reached => reached.Count;
        }

        /// <summary>1.3 item 2 (checks): one object the wave's front reached: the wave's number, the object, the rule seconds after the call,
        /// its centre's distance (px) at that tick, the step's front (px), and whether it was destroyed.</summary>
        public struct PlaygroundNukeHit
        {
            public int Wave, Id, StepRadiusPx;
            public string Type;
            public double AfterSec, DistPx;
            public bool Killed;
        }

        List<PlaygroundNukeWave> nukeWaves;
        List<PlaygroundNukeHit> nukeHits;
        static readonly PlaygroundNukeWave[] noWaves = new PlaygroundNukeWave[0];
        static readonly PlaygroundNukeHit[] noHits = new PlaygroundNukeHit[0];
        /// <summary>1.3 item 2 (checks): the NUKE waves of this world, oldest first.</summary>
        public IReadOnlyList<PlaygroundNukeWave> PlaygroundNukeWaves => nukeWaves != null ? (IReadOnlyList<PlaygroundNukeWave>)nukeWaves : noWaves;
        /// <summary>1.3 item 2 (checks): every object a NUKE wave's front reached, in the order of the ticks.</summary>
        public IReadOnlyList<PlaygroundNukeHit> PlaygroundNukeHits => nukeHits != null ? (IReadOnlyList<PlaygroundNukeHit>)nukeHits : noHits;

        /// <summary>The wave a NUKE step belongs to: the newest one at its centre that started at or before now.</summary>
        PlaygroundNukeWave NukeWaveOf(ZMissile m)
        {
            if (nukeWaves == null) return null;
            for (int i = nukeWaves.Count - 1; i >= 0; i--)
            {
                var w = nukeWaves[i];
                if (w.X == m.x && w.Y == m.y && w.T0 <= Time) return w;
            }
            return null;
        }

        /// <summary>ProcessMissileDamage hook (a NUKE step, after the radius and flag tests): true the first time the wave's front reaches o
        /// (the step then takes all its HP), false for every later step of the same wave (it was decided when the front passed it).</summary>
        bool PlaygroundNukeReach(ZMissile m, ZObject o)
        {
            var w = NukeWaveOf(m);
            return w == null || w.reached.Add(o.Id);
        }

        /// <summary>1.3 final: the outer band's share of the full HP, at the inner ring and at the wave's edge (%).</summary>
        public const int NukeBandMaxPct = 75, NukeBandMinPct = 25;

        /// <summary>ProcessMissileDamage hook (a NUKE step, after PlaygroundNukeReach): the HP the wave takes from o at distance mag (px). Inside the inner
        /// ring (or a wave without a band): all of it. In the band: NukeBandMaxPct % of its full HP at the inner ring, falling to NukeBandMinPct % at the
        /// edge, at most what it has. Integer per cent, no random draw.</summary>
        int PlaygroundNukeDamage(ZMissile m, ZObject o, float mag)
        {
            var w = NukeWaveOf(m);
            if (w == null || w.KillPx >= w.RadiusPx || mag < w.KillPx) return o.Health;
            double share = Math.Max(0.0, Math.Min(1.0, (w.RadiusPx - (double)mag) / (w.RadiusPx - w.KillPx)));   // 1 at the inner ring, 0 at the edge
            int pct = NukeBandMinPct + (int)Math.Floor((NukeBandMaxPct - NukeBandMinPct) * share);
            long dmg = ((long)o.MaxHealth * pct + 99) / 100;
            return (int)Math.Max(1, Math.Min(o.Health, dmg));
        }

        /// <summary>ProcessMissileDamage hook (a NUKE step, after the damage): the record the counted check reads.</summary>
        void PlaygroundNukeNote(ZMissile m, ZObject o, float mag, int hp0)
        {
            var w = NukeWaveOf(m);
            if (w == null) return;
            (nukeHits ??= new List<PlaygroundNukeHit>()).Add(new PlaygroundNukeHit
            {
                Wave = w.Number, Id = o.Id, Type = o.Type, AfterSec = Time - w.T0, DistPx = mag, StepRadiusPx = m.radius, Killed = hp0 > 0 && o.Health <= 0
            });
        }

        /// <summary>1.3 item 2: CLEAR FIELD (View/OzUePlayground.cs ClearQuiet): the steps of a running NUKE wave that have not burst yet are
        /// dropped, so a unit placed on the cleared field is not hit by a wave that came before it. Returns the steps dropped.</summary>
        public int PlaygroundNukeCancel()
        {
            int n = missiles.RemoveAll(m => m.nuke) + newMissiles.RemoveAll(m => m.nuke);
            if (n > 0 && nukeWaves != null) nukeWaves[nukeWaves.Count - 1].Cancelled = true;
            return n;
        }

        bool TakeOutNow(ZObject x)
        {
            if (x == null || Get(x.Id) != x) return false;
            if (x.KillAt < 0) x.KillAt = Time;
            DeleteObject(x);
            Flags.Remove(x);
            flagChangedAt?.Remove(x.Id);
            if (PlayableShuttle == x) PlayableShuttle = null;
            return true;
        }
    }
}
