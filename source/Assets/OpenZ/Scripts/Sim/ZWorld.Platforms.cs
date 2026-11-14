using System;
using System.Collections.Generic;

namespace OpenZ.Sim
{
    /// <summary>
    /// The rules have no height (only the shuttle has one), so this file carries it, for the playground alone: a table per tile, null in
    /// every level, for ever (<see cref="PlacePlatform"/> is called from View/OzUePlayground.Platforms.cs only).
    ///  * A platform is a neutral map item of <see cref="PlatformType"/>, one rectangle of tiles, 10 wide. Rows from the back: 1 closed wall,
    ///    8 open deck, 7 (medium) or 14 (high) open ramp; the two side columns are closed the whole length. So the ramp's foot is the only way
    ///    on, and paths, ZReach and the lanes work unchanged. Closed = Path.SetImpassable(x, y, true, false), never "destroyable", so
    ///    EngageBarrierBetween does not stop shots from the deck and no unit plans to shoot its way through.
    ///  * The height: <see cref="PlatformHeightM"/>: the deck's height on the deck and over the rails, the straight slope on the ramp. The view
    ///    builds its walk surface from the same numbers (View/OzUePlatformView.cs), so ZGame.GroundY lifts every unit, ghost and shot end.
    ///  * Sight (DV23): the rule of a fort tower gun (ZSight.ExemptOf). A platform blocks direct fire and sight for those beside it; a unit on
    ///    its deck or the upper half of its ramp is exempt: it sees and fires over it, and can be fired at.
    ///  * Hit chance and damage do not change with height. Range: DV162 (Sim/ZObject.HighGround.cs): the deck reaches 10 % farther at a target below, as a fort tower gun does.
    ///  * A blast does not cross levels unless it was aimed at that unit (<see cref="PlatformBlastOff"/>, ShuttleAirBlast's idea).
    ///  * Destroyed: everything on it dies, the same two calls as the bridge rule and the fort rule (<see cref="PlatformFell"/>), nothing is
    ///    left in the air: the view turns its walk surface off and takes what lay on the deck; the burning ground on it goes in
    ///    <see cref="PlatformClearTiles"/>, on a fall and on a removal.
    ///  * The mouse: <see cref="PlatformInfo.RayOnBody"/>: the drawn body as a ray sees it, so a click on a far rail or kerb counts on the platform.
    /// Hook lines (each a call that returns at once in a level): ZSight constructor and ExemptOf, ZWorld.ProcessMissileDamage and
    /// UpdateObjectHealth. Every call starts on the null table; no random draw, no object, no event, no field written on that path.
    /// Pure C# like the rest of the Sim folder.
    /// </summary>
    public sealed partial class ZWorld
    {
        /// <summary>The map item's type.</summary>
        public const string PlatformType = "ue_platform";
        /// <summary>The two kinds, 0 medium, 1 high.</summary>
        public const int PlatformMid = 0, PlatformHigh = 1;
        /// <summary>Tiles across, the closed back wall's rows, the open deck's rows.</summary>
        public const int PlatformWideTiles = 10, PlatformBackRows = 1, PlatformDeckRows = 8;
        /// <summary>The ramp's rows per kind (7 = 14 m, 14 = 28 m: slope 0.32 and 0.33, 18 degrees).</summary>
        public static readonly int[] PlatformRampRows = { 7, 14 };
        /// <summary>The deck's height per kind (m): medium; a fort tower's gun pad (View/TowerMounts.cs, fort-local 9.24 + 0.10 plinth).</summary>
        public static readonly double[] PlatformDeckM = { 4.5, 9.25 };
        /// <summary>Hit points per kind: the holo obstacle's; about one heavy tank for 20 s (level 16's small pyramid: 20800).</summary>
        public static readonly int[] PlatformHp = { 10000, 20000 };
        /// <summary>A blast reaches an object at most this far over or under its own level (m).</summary>
        public const double PlatformBlastLevelM = 3.0;
        /// <summary><see cref="PlatformInfo.RayOnBody"/>: its step along the ray (m), and how far inside the walkable tiles its answer stays (tiles): rounded to a pixel it is still on them.</summary>
        const double PlatformRayStepM = 0.1, PlatformRimPullTiles = 0.25;

        /// <summary>What a tile of a platform is.</summary>
        public const byte PlatPartNone = 0, PlatPartRail = 1, PlatPartDeck = 2, PlatPartRampLow = 3, PlatPartRampHigh = 4, PlatPartRailLow = 5;

        /// <summary>One platform: its object, kind, ramp side (where the ramp's foot looks: 0 +ty, 1 -tx, 2 -ty, 3 +tx, so the camera's turn
        /// / 90 with the foot toward the viewer) and its rectangle in tiles.</summary>
        public sealed class PlatformInfo
        {
            public ZObject O;
            public int Kind, Dir, TileX, TileY, Wide, Deep;
            /// <summary>Rows from the back wall to the ramp's foot.</summary>
            public int Rows => PlatformBackRows + PlatformDeckRows + RampRows;
            public int RampRows => PlatformRampRows[Kind];
            public double DeckM => PlatformDeckM[Kind];

            /// <summary>A map point (px) as (across, along) in tiles of the platform: across 0 .. 10, along 0 at the back wall .. Rows at the
            /// ramp's foot.</summary>
            public void Local(double px, double py, out double across, out double along)
            {
                double dx = px / 16.0 - TileX, dy = py / 16.0 - TileY;
                switch (Dir)
                {
                    case 0: across = dx; along = dy; break;
                    case 2: across = dx; along = Rows - dy; break;
                    case 3: across = dy; along = dx; break;
                    default: across = dy; along = Rows - dx; break;
                }
            }

            /// <summary>The map point (px) of a platform point.</summary>
            public void Map(double across, double along, out double px, out double py)
            {
                double dx, dy;
                switch (Dir)
                {
                    case 0: dx = across; dy = along; break;
                    case 2: dx = across; dy = Rows - along; break;
                    case 3: dx = along; dy = across; break;
                    default: dx = Rows - along; dy = across; break;
                }
                px = (TileX + dx) * 16.0; py = (TileY + dy) * 16.0;
            }

            /// <summary>The height (m) at `along`: the deck's up to the ramp's top, then the straight slope down to 0 at the foot.</summary>
            public double HeightAt(double along)
            {
                double top = PlatformBackRows + PlatformDeckRows;
                if (along <= top) return DeckM;
                return along >= Rows ? 0.0 : DeckM * (Rows - along) / RampRows;
            }

            /// <summary>The mouse's ray and the drawn body (View/OzUePlatformView.cs MouseRay; no rule asks). The walk surface stops 1 m in from the drawn
            /// edge, so a ray on the outer metre of the far rail or kerb misses it and lands on the floor behind the wall. This walks the ray through the
            /// body as drawn: the deck and the slope out to the footprint's edge, the kerb's height over its band. Ray: map px for x and y, metres up for h,
            /// its direction per metre of ray, `before` = metres to the point the ray has already (the body must come first).
            /// True: t = metres to the body, (across, along) = the walkable spot nearest it: a rail or a wall is no place to stand, so the point
            /// a click means is the deck or the ramp beside it, as a drop on a wall lands on the deck.</summary>
            public bool RayOnBody(double ox, double oy, double oh, double dx, double dy, double dh, double before, double kerbM, double kerbTiles, out double t, out double across, out double along)
            {
                t = 0.0; across = along = 0.0;
                Local(ox, oy, out double a0, out double l0);
                Local(ox + dx, oy + dy, out double a1, out double l1);
                double da = a1 - a0, dl = l1 - l0, from = 0.0, to = before;
                void Clip(double o, double d, double lo, double hi)
                {
                    if (Math.Abs(d) < 1e-9) { if (o < lo || o > hi) to = -1.0; return; }
                    double t0 = (lo - o) / d, t1 = (hi - o) / d;
                    from = Math.Max(from, Math.Min(t0, t1)); to = Math.Min(to, Math.Max(t0, t1));
                }
                Clip(a0, da, 0.0, PlatformWideTiles); Clip(l0, dl, 0.0, Rows); Clip(oh, dh, 0.0, DeckM + kerbM);
                for (t = from; t < to; t += PlatformRayStepM)
                {
                    double a = a0 + da * t, l = l0 + dl * t, top = HeightAt(l);
                    if (a < kerbTiles || a > PlatformWideTiles - kerbTiles || l < kerbTiles) top += kerbM;
                    if (oh + dh * t > top) continue;
                    across = Math.Max(1.0 + PlatformRimPullTiles, Math.Min(PlatformWideTiles - 1.0 - PlatformRimPullTiles, a));
                    along = Math.Max(PlatformBackRows + PlatformRimPullTiles, Math.Min(Rows - PlatformRimPullTiles, l));
                    return true;
                }
                return false;
            }
        }

        List<PlatformInfo> platforms;                                                       // null until the first PlacePlatform: null in every level
        int[] platId;                                                                       // tile -> the platform's id, 0 = none (null with no platform)
        byte[] platPart;                                                                    // tile -> PlatPart...
        HashSet<int> platFallen;                                                            // the units a platform took down with it
        static readonly PlatformInfo[] noPlatforms = new PlatformInfo[0];

        /// <summary>The standing platforms (empty in every level).</summary>
        public IReadOnlyList<PlatformInfo> Platforms => platforms != null ? (IReadOnlyList<PlatformInfo>)platforms : noPlatforms;
        /// <summary>Tables made in this world (checks: false in every level).</summary>
        public bool PlatformTablesMade => platId != null;

        /// <summary>Rectangle of a kind on a ramp side: (wide, deep) in tiles.</summary>
        public static void PlatformSize(int kind, int dir, out int wide, out int deep)
        {
            int rows = PlatformBackRows + PlatformDeckRows + PlatformRampRows[kind];
            if ((dir & 1) == 0) { wide = PlatformWideTiles; deep = rows; } else { wide = rows; deep = PlatformWideTiles; }
        }

        /// <summary>Can a platform stand on this rectangle? why (when not): as <see cref="ObstacleSpotFree"/>; another platform = "obstacle". A parked
        /// shuttle counts as drawn, <see cref="PlatformOnShip"/>: its sim box is 32 x 32 px, its plan radius 3.889 m.</summary>
        public bool PlatformSpotFree(int tx, int ty, int wide, int deep, out string why)
        {
            why = null;
            if (Map == null || wide < 1 || deep < 1) { why = "edge"; return false; }
            if (tx < 1 || ty < 1 || tx + wide > Map.width - 1 || ty + deep > Map.height - 1) { why = "edge"; return false; }
            var probe = new ZObject { WidthPix = wide * 16, HeightPix = deep * 16 };
            probe.SetCords(tx * 16, ty * 16);
            foreach (var o in Objects)
                if (!o.Destroyed && o.KillAt < 0 && (o.Type == WreckType || o.Type == PlatformType) && probe.IntersectsObject(o)) { why = "obstacle"; return false; }
            foreach (var o in Objects)
                if (!o.Destroyed && o.KillAt < 0 && o.Type == "flag" && probe.IntersectsObject(o)) { why = "flag"; return false; }
            foreach (var o in Objects)
                if (!o.Destroyed && o.KillAt < 0 && (o.Kind == ZKind.Vehicle || o.Kind == ZKind.Cannon) && o.Owner == Team.Neutral && probe.IntersectsObject(o)) { why = "empty"; return false; }
            foreach (var o in Objects)
                if (!o.Destroyed && o.KillAt < 0 && (o.IsUnit || o.Type == "grenades") && probe.IntersectsObject(o)) { why = "unit"; return false; }
            foreach (var o in Objects)
                if (!o.Destroyed && o.KillAt < 0 && o.IsShuttle && !o.ShuttleAirborne && PlatformOnShip(tx, ty, wide, deep, o)) { why = o.Owner == Team.Neutral ? "empty" : "unit"; return false; }
            for (int j = ty; j < ty + deep; j++)
                for (int i = tx; i < tx + wide; i++)
                {
                    var c = Map.ClassAt(i, j);
                    if (c != ZClass.Ground && c != ZClass.Road) { why = "ground"; return false; }
                    if (!Path.TilePassable(i, j, true)) { why = "obstacle"; return false; }
                }
            return true;
        }

        /// <summary>The ramp lane behind a ship in its root space (x right, z nose, m, counter-clockwise): ZWorld.ShuttleSpotPart's lane.</summary>
        static readonly double[] platformLanePlan =
        {
            -ShuttleLaneHalfW, -(ShuttleBackM + ShuttleLaneBehindM), ShuttleLaneHalfW, -(ShuttleBackM + ShuttleLaneBehindM),
            ShuttleLaneHalfW, -ZObject.ShuttleHullBackM, -ShuttleLaneHalfW, -ZObject.ShuttleHullBackM,
        };

        /// <summary>This rectangle of tiles reaches a parked ship: its drawn outline + ShuttleSpotClearM or its ramp lane, the two parts of the landing
        /// spot rule, exact per tile. So a platform never enters a parked hull, and ZWorld.ShuttlePadFreeAt at the ship's own spot stays true.</summary>
        bool PlatformOnShip(int tx, int ty, int wide, int deep, ZObject ship)
        {
            double hd = ship.ShuttleHeadingDeg * Math.PI / 180.0, nx = Math.Cos(hd), ny = Math.Sin(hd);
            for (int j = ty; j < ty + deep; j++)
                for (int i = tx; i < tx + wide; i++)
                    if (ShuttleTileGap(ZObject.ShuttlePlan, i, j, ship.CenterX, ship.CenterY, nx, ny, -ny, nx) < ShuttleSpotClearM
                        || ShuttleTileGap(platformLanePlan, i, j, ship.CenterX, ship.CenterY, nx, ny, -ny, nx) <= 0) return true;
            return false;
        }

        /// <summary>The Unit editor's platform with its top-left tile at (tx, ty). Null (why = <see cref="PlatformSpotFree"/>'s word) when it
        /// cannot stand there. hp under 1 = full.</summary>
        public ZObject PlacePlatform(int tx, int ty, int kind, int dir, int hp, out string why)
        {
            kind = kind == PlatformHigh ? PlatformHigh : PlatformMid; dir &= 3;
            PlatformSize(kind, dir, out int wide, out int deep);
            if (!PlatformSpotFree(tx, ty, wide, deep, out why)) return null;
            var o = CreateObject("map_item", PlatformType, tx * 16, ty * 16, Team.Neutral);    // SetClassFlags gave AttackedByExplosives
            o.WidthPix = wide * 16; o.HeightPix = deep * 16; o.SetCords(tx * 16, ty * 16);
            o.MaxHealth = PlatformHp[kind]; o.Health = hp >= 1 ? Math.Min(hp, o.MaxHealth) : o.MaxHealth;
            var p = new PlatformInfo { O = o, Kind = kind, Dir = dir, TileX = tx, TileY = ty, Wide = wide, Deep = deep };
            (platforms ??= new List<PlatformInfo>()).Add(p);
            if (platId == null) { platId = new int[Map.width * Map.height]; platPart = new byte[Map.width * Map.height]; }
            int rampTop = PlatformBackRows + PlatformDeckRows, lowFrom = rampTop + p.RampRows / 2;
            for (int j = ty; j < ty + deep; j++)
                for (int i = tx; i < tx + wide; i++)
                {
                    p.Local(i * 16 + 8, j * 16 + 8, out double ax, out double al);
                    int across = (int)Math.Floor(ax), along = (int)Math.Floor(al);
                    byte part;
                    if (across == 0 || across == PlatformWideTiles - 1 || along < PlatformBackRows) part = along >= lowFrom ? PlatPartRailLow : PlatPartRail;
                    else if (along < rampTop) part = PlatPartDeck;
                    else part = along >= lowFrom ? PlatPartRampLow : PlatPartRampHigh;
                    int k = j * Map.width + i;
                    platId[k] = o.Id; platPart[k] = part;
                    if (part == PlatPartRail || part == PlatPartRailLow) Path.SetImpassable(i, j, true, false);
                }
            sight = null;                                                                   // built again with this platform in it (as AddL16Pyramids does)
            return o;
        }

        /// <summary>The platform on a tile: its id, 0 = none.</summary>
        public int PlatformAtTile(int tx, int ty)
        {
            if (platId == null || tx < 0 || ty < 0 || tx >= Map.width || ty >= Map.height) return 0;
            return platId[ty * Map.width + tx];
        }

        /// <summary>What a tile of a platform is (PlatPart..., 0 = none).</summary>
        public byte PlatformPartAt(int tx, int ty)
        {
            if (platPart == null || tx < 0 || ty < 0 || tx >= Map.width || ty >= Map.height) return PlatPartNone;
            return platPart[ty * Map.width + tx];
        }

        /// <summary>The platform of an id, null = none.</summary>
        public PlatformInfo PlatformOf(int id)
        {
            if (platforms == null) return null;
            for (int i = 0; i < platforms.Count; i++) if (platforms[i].O.Id == id) return platforms[i];
            return null;
        }

        /// <summary>The floor's height at a map point (px) in metres: 0 off a platform. (0 in every level.)</summary>
        public double PlatformHeightM(double px, double py)
        {
            if (platId == null) return 0.0;
            int id = PlatformAtTile((int)Math.Floor(px / 16.0), (int)Math.Floor(py / 16.0));
            if (id == 0) return 0.0;
            var p = PlatformOf(id); if (p == null) return 0.0;
            p.Local(px, py, out _, out double along);
            return p.HeightAt(along);
        }

        /// <summary>The nearest deck tile on the same across line (a gun dropped on the ramp stands on the deck). False = not on a platform.</summary>
        public bool PlatformDeckTile(int tx, int ty, out int dx, out int dy)
        {
            dx = tx; dy = ty;
            var p = PlatformOf(PlatformAtTile(tx, ty)); if (p == null) return false;
            p.Local(tx * 16 + 8, ty * 16 + 8, out double ax, out double al);
            double across = Math.Max(1.5, Math.Min(PlatformWideTiles - 2.5, ax)), along = Math.Max(PlatformBackRows + 0.5, Math.Min(PlatformBackRows + PlatformDeckRows - 1.5, al));
            p.Map(across, along, out double px, out double py);
            dx = (int)Math.Floor(px / 16.0); dy = (int)Math.Floor(py / 16.0);
            return true;
        }

        /// <summary>ZSight.ExemptOf: the platform a unit stands high on (its centre on the deck, the rails or the ramp's upper half); 0 = none.</summary>
        public int PlatformSeatId(ZObject o)
        {
            if (platId == null || o == null) return 0;
            int tx = o.CenterX >> 4, ty = o.CenterY >> 4;
            if (tx < 0 || ty < 0 || tx >= Map.width || ty >= Map.height) return 0;
            int k = ty * Map.width + tx;
            byte part = platPart[k];
            return part == PlatPartDeck || part == PlatPartRail || part == PlatPartRampHigh ? platId[k] : 0;
        }

        /// <summary>ZSight constructor hook: each platform's tall tiles under its own id (rails, wall, deck, the ramp's upper half): it blocks
        /// direct fire across it while it stands and can itself be fired at. The ramp's lower half never blocks.</summary>
        internal void PlatformSightTiles(int[] tileObj, List<ZObject> blockers)
        {
            if (platforms == null) return;
            foreach (var p in platforms)
            {
                for (int j = p.TileY; j < p.TileY + p.Deep; j++)
                    for (int i = p.TileX; i < p.TileX + p.Wide; i++)
                    {
                        int k = j * Map.width + i; byte part = platPart[k];
                        if (part == PlatPartRail || part == PlatPartDeck || part == PlatPartRampHigh) tileObj[k] = p.O.Id;
                    }
                blockers.Add(p.O);
            }
        }

        /// <summary>ProcessMissileDamage hook: a blast does not cross levels. True = o is left out: it stands more than
        /// <see cref="PlatformBlastLevelM"/> over or under the blast's level (the height of the blast's target while it lives, else of the
        /// point), and it is not the aimed unit, not a platform, and the blast no NUKE.</summary>
        internal bool PlatformBlastOff(ZMissile m, ZObject o)
        {
            if (platId == null || m.nuke || m.targetId == o.Id || o.Type == PlatformType) return false;
            var t = m.targetId >= 0 ? GetAny(m.targetId) : null;
            double level = t != null && !t.Destroyed ? PlatformHeightM(t.CenterX, t.CenterY) : PlatformHeightM(m.x, m.y);
            return Math.Abs(PlatformHeightM(o.CenterX, o.CenterY) - level) > PlatformBlastLevelM;
        }

        /// <summary>UpdateObjectHealth hook: a platform destroyed: every robot, vehicle and gun whose centre is on it dies the same tick (the
        /// bridge rule, zserver.cpp:1639-1673: SetHealth(0) + UpdateObjectHealth; LastMissileHit makes the death a blast's, so robots are
        /// thrown). A flying shuttle stays out. The closed tiles open again. Centre test, not the box: a unit passing the ramp's foot lives.</summary>
        void PlatformFell(ZObject o)
        {
            if (platforms == null || o.Type != PlatformType) return;
            var all = new List<ZObject>(Objects);
            foreach (var x in all)
            {
                if (x.Destroyed || x.KillAt >= 0 || x.ShuttleAirborne || x.MaxHealth <= 0) continue;
                if ((x.Kind == ZKind.Robot || x.Kind == ZKind.Vehicle || x.Kind == ZKind.Cannon) && o.Contains(x.CenterX, x.CenterY))
                {
                    (platFallen ??= new HashSet<int>()).Add(x.Id);
                    x.LastMissileHit = Time; x.SetHealth(0); UpdateObjectHealth(x);
                }
            }
            PlatformClearTiles(o);
        }

        /// <summary>This unit died with its platform (the view: it falls with it).</summary>
        public bool PlatformFallen(int id) => platFallen != null && platFallen.Contains(id);

        /// <summary>The platform's tiles get their map class back, its table entries go, and the burning ground lit on it: it has no floor now, a patch that
        /// stayed would bite on the ground under it, unseen. Called when it is destroyed, and before a removal (ZWorld.RemoveNow keeps no tile table of its own).</summary>
        public void PlatformClearTiles(ZObject o)
        {
            if (platforms == null || o == null) return;
            var p = PlatformOf(o.Id); if (p == null) return;
            GroundFires.RemoveAll(f => o.Contains(f.x, f.y));
            for (int j = p.TileY; j < p.TileY + p.Deep; j++)
                for (int i = p.TileX; i < p.TileX + p.Wide; i++)
                {
                    int k = j * Map.width + i;
                    if (platPart[k] == PlatPartRail || platPart[k] == PlatPartRailLow)
                    {
                        var c = Map.ClassAt(i, j);
                        Path.SetTileInfo(i, j, c == ZClass.Water ? PfTile.Water : c == ZClass.Road ? PfTile.Road : c == ZClass.Ground ? PfTile.Normal : PfTile.Impassable);   // as WreckImpassables
                    }
                    platId[k] = 0; platPart[k] = PlatPartNone;
                }
            platforms.Remove(p);
            sight = null;
        }

        /// <summary>The units standing on a platform now (each robot on its own), for a quiet removal.</summary>
        public List<ZObject> PlatformLoad(ZObject o)
        {
            var l = new List<ZObject>();
            if (o == null) return l;
            foreach (var x in Objects)
                if (x.IsUnit && !x.Destroyed && x.KillAt < 0 && !x.ShuttleAirborne && o.Contains(x.CenterX, x.CenterY)) l.Add(x);
            return l;
        }
    }
}
