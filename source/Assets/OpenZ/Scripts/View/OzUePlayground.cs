using System;
using System.Collections.Generic;
using System.Text;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// order, then the two neutral extras), their names, and everything that changes the field, through the same Sim calls as the testing
    /// panel P (UI/OzCheatSpawner.cs Spawn): CreateRobotGroup with the world's own squad size (the set in use, P3), CreateObject for vehicles
    /// and guns (an owned one gets its grunt driver, a NEUTRAL one is empty), the shuttle as the testing panel drops it (SpawnShuttle +
    /// OzPlayableShuttle.SpawnView), the hero squad (CreateHeroSquad + OzHeroSquadView), the neutral FLAG (the game's capture flag, its
    /// view added at run time: View/FlagView.Runtime.cs) and the holo OBSTACLE (Sim/ZWorld.Playground.cs PlaceObstacle + the hologram,
    /// View/OzUeObstacleView.cs). Spots: the testing panel's FreeTile spiral. Removal never kills: ZWorld.RemoveNow + the views destroyed.
    /// Drops never show a toast: the caller shows UeDrop.why (P4 placing); the drop sound is the testing panel's (OzSoundV3.CheatDrop +
    /// ui_confirm), a refused drop ui_error, a removal or a cleared field ui_cancel.
    /// </summary>
    public static partial class OzUePlayground
    {
        public const string Heroes = "heroes", Flag = "flag", Obstacle = "obstacle";
        /// <summary>The GRENADES extra: the game's own grenade crate map item (its Sim type and its view Z_grenades).</summary>
        public const string Grenades = "grenades";
        /// <summary>1.3 item 2: the NUKE extra: the levels' ending bomber drops the nuke on the chosen spot (View/OzUeNuke.cs); no object is placed.</summary>
        public const string Nuke = "nuke";
        public static string Shuttle => ZWorld.ShuttleType;
        /// <summary>The game's own build-menu order (ZSettings lists), DECISION D8 of the architecture.</summary>
        public static readonly string[] Robots = { "grunt", "psycho", "sniper", "tough", "pyro", "laser", "electrobot" };
        public static readonly string[] Vehicles = { "jeep", "light", "medium", ZMTank.Type, "heavy", OzBigTanks.Mammoth, OzBigTanks.Bizon, "apc",   /* 1.5 DV180 / DV181: Unit editor only */ "missile_launcher", "crane" };   // DV145 the medium tank (Sim/ZMTank.cs)
        public static readonly string[] Guns = { "gatling", "gun", "howitzer", "missile_cannon" };
        public static readonly string[] Special = { Heroes, ZEvilZod.Type, ZWorld.ShuttleType };   // DV161: EvilZod between the heroes and the shuttle (Sim/ZEvilZod.cs)
        public static readonly string[] Extras = { Flag, Obstacle, Grenades, Nuke, PlatformMid, PlatformHigh, Box };
        /// <summary>The 22 units in game order (robots, vehicles, guns, heroes, EvilZod, shuttle).</summary>
        public static readonly string[] Units = { "grunt", "psycho", "sniper", "tough", "pyro", "laser", "electrobot", "jeep", "light", "medium", ZMTank.Type, "heavy", OzBigTanks.Mammoth, OzBigTanks.Bizon, "apc",   /* 1.5 DV180 / DV181 */   // DV145 the medium tank (Sim/ZMTank.cs)
                                                  "missile_launcher", "crane", "gatling", "gun", "howitzer", "missile_cannon", Heroes, ZEvilZod.Type, ZWorld.ShuttleType };
        public const int ObstacleTiles = 3, ObstacleHp = 10000;
        public const float ObstacleHeightM = 3f;

        /// <summary>Every leader / object a drop or a load made.</summary>
        public static event Action<ZObject> Spawned;
        /// <summary>Once a frame at most (raised in OzUeField.LateUpdate): drop, removal, clear, capture, climb-in, death, owner change.</summary>
        public static event Action FieldChanged;

        static readonly HashSet<int> obstacleIds = new HashSet<int>();
        static readonly Dictionary<int, int> flagTakes = new Dictionary<int, int>();
        static bool dirty;
        /// <summary>Proof counters: drops, groups made, refused drops, removals (objects), cleared fields, sample fights.</summary>
        public static int Drops, GroupsMade, Refused, Removed, Cleared, Samples;

        static OzUnitEditor Ed => OzUnitEditor.I;
        static ZGame G => OzUnitEditor.I ? OzUnitEditor.I.Game : null;
        static ZWorld W => OzUnitEditor.I ? OzUnitEditor.I.World : null;
        static PlayerInput In => OzUnitEditor.I ? OzUnitEditor.I.Input : null;

        // ------------------------------------------------------------------------------------------------ palette
        public static UeKind KindOf(string id)
        {
            if (string.IsNullOrEmpty(id)) return UeKind.None;
            if (Array.IndexOf(Robots, id) >= 0 || id == ZEvilZod.Type) return UeKind.Robot;
            if (Array.IndexOf(Vehicles, id) >= 0) return UeKind.Vehicle;
            if (Array.IndexOf(Guns, id) >= 0) return UeKind.Gun;
            if (id == Heroes) return UeKind.Hero;
            if (id == ZWorld.ShuttleType) return UeKind.Shuttle;
            if (id == Flag) return UeKind.Flag;
            if (id == Obstacle || id == Box) return UeKind.Obstacle;                                      // 1.5 ue_boxes: a box is an obstacle of the playground with its own id
            if (id == Grenades) return UeKind.Grenades;
            if (id == Nuke || OzUeBlasts.Is(id)) return UeKind.Nuke;   /* 1.5 ue_explosions_6 */
            if (IsPlatformId(id)) return UeKind.Platform;
            return UeKind.None;
        }
        /// <summary></summary>
        public static bool CanBeNeutral(string id) { var k = KindOf(id); return k == UeKind.Vehicle || k == UeKind.Gun; }
        /// <summary>The flag and the obstacle are always neutral.</summary>
        public static bool AlwaysNeutral(string id) { var k = KindOf(id); return k == UeKind.Flag || k == UeKind.Obstacle || k == UeKind.Grenades || k == UeKind.Nuke || k == UeKind.Platform; }
        /// <summary>Not in the 1996 Z: the Electrobot, the heroes, the shuttle.</summary>
        public static bool OpenZOnly(string id) => id == ZElectro.Type || id == ZEvilZod.Type || id == Heroes || id == ZWorld.ShuttleType || id == ZMTank.Type || OzBigTanks.Is(id);   // 1.5 DV180 / DV181   // DV145: the medium tank is OpenZ only too
        /// <summary>
        /// Neutral = a unit of every side.</summary>
        public static Team TeamOnly(string id) => id == ZEvilZod.Type ? Team.Blue : id == Heroes ? Team.Red : Team.Neutral;
        /// <summary>Rows 79 / 80: the dock offers this unit under the team button <paramref name="team"/>: a one-side hero only under his own
        /// red's only, so neither is offered under NEUTRAL either.</summary>
        public static bool ForTeam(string id, Team team) { var s = TeamOnly(id); return s == Team.Neutral || team == s; }
        /// <summary>Offered in the playground now: false while the game's rules switch the unit off (the original Z rules), why = the
        /// tooltip line.</summary>
        public static bool Offered(string id, out string why)
        {
            why = null;
            bool on = id == ZEvilZod.Type ? ZWorld.EvilZodDeviation : id == ZElectro.Type ? ZWorld.ElectrobotDeviation : id == Heroes ? ZWorld.HeroSquadDeviation : id == ZWorld.ShuttleType ? ZWorld.ShuttlePlayableDeviation : id == ZMTank.Type ? ZWorld.MTankDeviation : id == OzBigTanks.Mammoth ? ZWorld.MammothDeviation : id == OzBigTanks.Bizon ? ZWorld.BizonDeviation /* 1.5 DV180 / DV181 */ : KindOf(id) != UeKind.None;   // DV145: off in 'Zod exact' like the other OpenZ-only units
            if (!on) why = KindOf(id) == UeKind.None ? "Not a unit of the game." : "Off while the game plays the original Z rules.";
            return on;
        }
        /// <summary>GRUNT .. HOWITZ., MISSILE, HEROES, SHUTTLE, FLAG, OBSTACLE (UnitInfo.Short).</summary>
        public static string ShortName(string id)
        {
            switch (KindOf(id))
            {
                case UeKind.Hero: return "HEROES";
                case UeKind.Shuttle: return "SHUTTLE";
                case UeKind.Flag: return "FLAG";
                case UeKind.Obstacle: return id == Box ? "BOX" : "OBSTACLE";
                case UeKind.Grenades: return "GRENADES";
                case UeKind.Nuke: return OzUeBlasts.Is(id) ? OzUeBlasts.Short(id) : "NUKE";
                case UeKind.Platform: return id == PlatformHigh ? "TOWER" : "PLATFORM";
                case UeKind.None: return id ?? "";
            }
            return UnitInfo.Short(id);
        }
        /// <summary>UnitInfo.Name (GRUNT, LIGHT TANK, MECH, HEAVY TANK, MLRS, MISSILE CANNON ...), ALAN & BRAD, SHUTTLE, FLAG, OBSTACLE.</summary>
        public static string FullName(string id)
        {
            switch (KindOf(id))
            {
                case UeKind.Hero: { var k = ZHeroSquads.Get(ZHeroSquads.AlanBrad); return k != null ? k.Name : "ALAN & BRAD"; }
                case UeKind.Shuttle: return "SHUTTLE";
                case UeKind.Flag: return "FLAG";
                case UeKind.Obstacle: return id == Box ? "BOX" : "OBSTACLE";
                case UeKind.Grenades: return "GRENADE CRATE";
                case UeKind.Nuke: return OzUeBlasts.Is(id) ? OzUeBlasts.Full(id) : "NUKE STRIKE";
                case UeKind.Platform: return id == PlatformHigh ? "HIGH PLATFORM" : "MEDIUM PLATFORM";
                case UeKind.None: return id ?? "";
            }
            return UnitInfo.Name(id);
        }
        /// <summary>Robot squad, Vehicle, Gun, Hero squad, Transport, Capture flag, Holo obstacle (spec E084, E110).</summary>
        public static string ClassWord(string id)
        {
            switch (KindOf(id))
            {
                case UeKind.Robot: return id == ZEvilZod.Type ? "Hero" : "Robot squad";
                case UeKind.Vehicle: return "Vehicle";
                case UeKind.Gun: return "Gun";
                case UeKind.Hero: return "Hero squad";
                case UeKind.Shuttle: return "Transport";
                case UeKind.Flag: return "Capture flag";
                case UeKind.Obstacle: return id == Box ? "Holo box" : "Holo obstacle";
                case UeKind.Grenades: return "Grenade crate";
                case UeKind.Nuke: return OzUeBlasts.Is(id) ? "Test explosion" : "Nuke strike";
                case UeKind.Platform: return "Platform";
            }
            return "";
        }
        /// <summary>Robots per squad now in play: the playground world's own row (the set in use); the heroes 2; anything else 1.</summary>
        public static int SquadSize(string id)
        {
            var k = KindOf(id);
            if (k == UeKind.Hero) { var h = ZHeroSquads.Get(ZHeroSquads.AlanBrad); return h != null ? h.Members.Length : 2; }
            if (k != UeKind.Robot) return 1;
            var u = W != null ? W.S.Unit(id) : null;
            return u != null ? Mathf.Max(1, u.group_amount) : 1;
        }
        /// <summary>The sim footprint side in metres: robot 2, vehicle / gun 4, shuttle 6, flag 2, obstacle 6.</summary>
        public static float FootprintM(string id)
        {
            switch (KindOf(id))
            {
                case UeKind.Vehicle: case UeKind.Gun: return 4f;
                case UeKind.Shuttle: return 6f;
                case UeKind.Obstacle: return id == Box ? BoxSideM : ObstacleTiles * 2f;
                case UeKind.Platform: return ZWorld.PlatformWideTiles * 2f;
            }
            return 2f;
        }

        // ------------------------------------------------------------------------------------------------ formations (spec 9.3)
        static readonly Vector2[] G1 = { new Vector2(0f, 0f) };
        static readonly Vector2[] G3 = { new Vector2(-1f, 0f), new Vector2(0f, 0f), new Vector2(1f, 0f) };
        static readonly Vector2[] G5 = { new Vector2(-1f, 0.5f), new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-0.5f, -0.5f), new Vector2(0.5f, -0.5f) };
        static readonly Vector2[] G10 = RowBlock(5, 2), G20 = RowBlock(5, 4);
        /// <summary>A block of rows (the first row furthest up the view), centred on the drop point, one gap apart.</summary>
        static Vector2[] RowBlock(int across, int rows)
        {
            var a = new Vector2[across * rows]; int n = 0;
            for (int r = 0; r < rows; r++) for (int c = 0; c < across; c++) a[n++] = new Vector2(c - (across - 1) * 0.5f, (rows - 1) * 0.5f - r);
            return a;
        }
        static int Groups(string id, int count) { var k = KindOf(id); return k == UeKind.Flag || k == UeKind.Obstacle || k == UeKind.Grenades || k == UeKind.Nuke || k == UeKind.Platform ? 1 : count >= 20 ? 20 : count >= 10 ? 10 : count >= 5 ? 5 : count >= 3 ? 3 : 1; }
        static float GapM(string id) { var k = KindOf(id); return k == UeKind.Robot || k == UeKind.Hero ? 5f : k == UeKind.Shuttle ? 10f : 7f; }
        /// <summary>1.4: an x10 / x20 block's gap in whole 2 m tiles (squads 6 m, vehicles and guns 8 m, shuttles 10 m), so every unit snaps to its tile the same way.</summary>
        static float BlockGapM(string id) => Mathf.Ceil(GapM(id) / 2f) * 2f;
        static Vector3 Tile2(Vector3 v) => new Vector3(Mathf.Round(v.x / 2f) * 2f, 0f, Mathf.Round(v.z / 2f) * 2f);   // to a 2 m tile corner

        /// <summary>The floor points of one drop, one per group (squad, vehicle, gun, shuttle; a squad's robots start on its point as in the
        /// game): x3 = a line across the view, x5 = a row of 3 further up the view and 2 in front, x10 = 2 rows of 5, x20 = 4 rows of 5; 5 m between squads, 7 m between vehicles and
        /// guns, 10 m between shuttles (x10 / x20: whole 2 m tiles, 6 / 8 / 10 m apart at no turn); a flag or an obstacle one. yawDeg = the camera's turn (0 = north up).</summary>
        public static List<Vector3> Formation(string id, int count, Vector3 centre, float yawDeg)
        {
            var g = Groups(id, count); var offs = g == 20 ? G20 : g == 10 ? G10 : g == 5 ? G5 : g == 3 ? G3 : G1; float gap = GapM(id);
            var rot = Quaternion.Euler(0f, yawDeg, 0f); Vector3 right = rot * Vector3.right, fwd = rot * Vector3.forward;
            var list = new List<Vector3>(offs.Length);
            if (g >= 10)
            {   // 1.4 (the lead's X20 frame, 5 Nov: gaps 6 / 8 m alternating): every unit lands on whole 2 m tiles (FootTile), and a 7 m / 5 m gap is
                // 3.5 / 2.5 tiles, rounded unit by unit. Now the step along a row is rounded ONCE to whole tiles (never under the old gap), the step
                // between rows is it turned a quarter, and the first unit sits on a tile corner: every unit snaps the same way, at any camera turn.
                float step = BlockGapM(id); Vector3 sR;
                do { sR = Tile2(right * step); step += 2f; } while (sR.magnitude < gap - 0.01f);
                var sF = new Vector3(-sR.z, 0f, sR.x);                                                       // right turned a quarter = fwd
                var a = Tile2(centre + sR * offs[0].x + sF * offs[0].y);
                foreach (var o in offs) list.Add(a + sR * (o.x - offs[0].x) + sF * (o.y - offs[0].y));
                return list;
            }
            foreach (var o in offs) { var p = centre + right * (o.x * gap) + fwd * (o.y * gap); p.y = 0f; list.Add(p); }
            return list;
        }

        static float CamYaw => Ed && Ed.Rts ? Ed.Rts.yaw : 0f;

        // ------------------------------------------------------------------------------------------------ drops
        enum Spot { Ok, Neutral, Off, NoRoom, OnObstacle, ObstacleOverUnits, ObstacleOverFlag, ObstacleOverObstacle, ObstacleOverEmpty, NoWorld, OnPlatform, PlatformOverUnits, PlatformOverFlag, PlatformOverObstacle, PlatformOverEmpty, BoxOverUnits, BoxOverFlag, BoxOverObstacle, BoxOverEmpty, BoxTooHigh }   // 1.5 ue_boxes: the box's refusals

        static string Words(Spot s)
        {
            switch (s)
            {
                case Spot.Neutral: return "ROBOTS ARE NEVER NEUTRAL: PICK RED OR BLUE";
                case Spot.Off: return "THIS UNIT IS OFF WHILE THE GAME PLAYS THE ORIGINAL Z RULES";
                case Spot.OnObstacle: return "NO ROOM: NOT ON AN OBSTACLE";
                case Spot.ObstacleOverUnits: return "NO ROOM: AN OBSTACLE CANNOT OVERLAP UNITS";
                case Spot.ObstacleOverFlag: return "NO ROOM: AN OBSTACLE CANNOT OVERLAP THE FLAG";
                case Spot.ObstacleOverObstacle: return "NO ROOM: AN OBSTACLE CANNOT OVERLAP ANOTHER OBSTACLE";
                case Spot.ObstacleOverEmpty: return "NO ROOM: AN OBSTACLE CANNOT OVERLAP EMPTY HARDWARE";
                case Spot.OnPlatform: return "NO ROOM: NOT ON A PLATFORM";
                case Spot.PlatformOverUnits: return "NO ROOM: A PLATFORM CANNOT OVERLAP UNITS";
                case Spot.PlatformOverFlag: return "NO ROOM: A PLATFORM CANNOT OVERLAP THE FLAG";
                case Spot.PlatformOverObstacle: return "NO ROOM: A PLATFORM CANNOT OVERLAP AN OBSTACLE OR ANOTHER PLATFORM";
                case Spot.PlatformOverEmpty: return "NO ROOM: A PLATFORM CANNOT OVERLAP EMPTY HARDWARE";
                case Spot.BoxOverUnits: case Spot.BoxOverFlag: case Spot.BoxOverObstacle: case Spot.BoxOverEmpty: case Spot.BoxTooHigh: return BoxWords(s);   // 1.5 ue_boxes
                case Spot.NoRoom: case Spot.NoWorld: return "NO ROOM HERE";
            }
            return null;
        }

        static Spot TeamRule(string id, Team team)
        {
            var k = KindOf(id);
            if (k == UeKind.None) return Spot.NoWorld;
            if (!Offered(id, out _)) return Spot.Off;
            if (team == Team.Neutral && !CanBeNeutral(id) && !AlwaysNeutral(id)) return Spot.Neutral;
            return Spot.Ok;
        }

        static Vector2Int Px(Vector3 p) => ZGame.WorldToPx(p);

        /// <summary>The top-left tile of a foot x foot footprint centred as near the point as the grid allows, kept inside the map.</summary>
        static Vector2Int FootTile(ZWorld w, Vector3 p, int foot)
        {
            var px = Px(p);
            int tx = foot == 1 ? px.x >> 4 : Mathf.RoundToInt(px.x / 16f - foot * 0.5f), ty = foot == 1 ? px.y >> 4 : Mathf.RoundToInt(px.y / 16f - foot * 0.5f);
            tx = Mathf.Clamp(tx, 1, Mathf.Max(1, w.Map.width - 1 - foot)); ty = Mathf.Clamp(ty, 1, Mathf.Max(1, w.Map.height - 1 - foot));
            return new Vector2Int(tx, ty);
        }

        /// <summary>A point lies on a standing obstacle's footprint.</summary>
        static bool OnAnObstacle(ZWorld w, int tx, int ty, int foot)
        {
            var probe = new ZObject { WidthPix = foot * 16, HeightPix = foot * 16 }; probe.SetCords(tx * 16, ty * 16);
            foreach (var o in w.Objects) if (o.Type == ZWorld.WreckType && !o.Destroyed && o.KillAt < 0 && probe.IntersectsObject(o)) return true;
            return false;
        }

        static bool FlagOn(ZWorld w, int tx, int ty)
        {
            foreach (var f in w.Flags) if (f != null && !f.Destroyed && f.KillAt < 0 && (f.LocX >> 4) == tx && (f.LocY >> 4) == ty) return true;
            return false;
        }

        /// <summary>The testing panel's spiral (OzCheatSpawner.FreeTile, radius 6) with one more rule: no other flag on a flag's tile.
        /// deck or lands inside a wall; a gun stands on the deck only (it is drawn flat, a slope would bury one side); a standing gun's tiles are
        /// taken, so a drop onto a gun stands beside it, not inside it.</summary>
        static bool FreeSpot(ZWorld w, string id, Vector3 at, int cx, int cy, int foot, bool robot, bool flag, out int tx, out int ty)
        {
            var atPx = Px(at); int home = w.PlatformAtTile(atPx.x >> 4, atPx.y >> 4); bool deckOnly = home != 0 && KindOf(id) == UeKind.Gun;
            if (deckOnly && w.PlatformPartAt(atPx.x >> 4, atPx.y >> 4) != ZWorld.PlatPartDeck) w.PlatformDeckTile(atPx.x >> 4, atPx.y >> 4, out cx, out cy);   // on the ramp or a rail: from the nearest deck tile
            for (int r = 0; r <= 6; r++)
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;
                        int x = cx + dx, y = cy + dy; bool ok = x >= 1 && y >= 1 && x + foot <= w.Map.width - 1 && y + foot <= w.Map.height - 1;
                        for (int j = 0; j < foot && ok; j++) for (int i = 0; i < foot && ok; i++) ok = w.Path.TilePassable(x + i, y + j, robot) && w.PlatformAtTile(x + i, y + j) == home && (!deckOnly || w.PlatformPartAt(x + i, y + j) == ZWorld.PlatPartDeck);
                        if (ok && OnAGun(w, x, y, foot)) ok = false;
                        if (ok && flag && FlagOn(w, x, y)) ok = false;
                        if (ok) { tx = x; ty = y; return true; }
                    }
            tx = cx; ty = cy; return false;
        }

        // the shuttle's landing spot (ZWorld.SpawnShuttle's search: within 6 tiles, the landing spot rule), cached a quarter second per tile
        static readonly Dictionary<long, (float at, bool ok)> shuttleSpot = new Dictionary<long, (float, bool)>();
        static bool ShuttleSpotNear(ZWorld w, Vector3 p, float headingDeg)
        {
            var px = Px(p);
            long key = ((long)(px.x >> 4) << 32) | (uint)(px.y >> 4);
            float now = Time.unscaledTime;
            if (shuttleSpot.TryGetValue(key, out var c) && now - c.at < 0.25f) return c.ok;
            if (shuttleSpot.Count > 256) shuttleSpot.Clear();
            bool ok = false;
            var (cx0, cy0) = w.ShuttleClamp(px.x, px.y);
            int tx0 = cx0 >> 4, ty0 = cy0 >> 4;
            for (int r = 0; r <= 6 && !ok; r++)
                for (int dy = -r; dy <= r && !ok; dy++)
                    for (int dx = -r; dx <= r && !ok; dx++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;
                        var (cx, cy) = w.ShuttleClamp((tx0 + dx) * 16 + 8, (ty0 + dy) * 16 + 8);
                        if (cx >> 4 != tx0 + dx || cy >> 4 != ty0 + dy) continue;
                        ok = w.ShuttleLandSpot(cx, cy, null, headingDeg, out _, out _);
                    }
            shuttleSpot[key] = (now, ok);
            return ok;
        }

        /// <summary>Where one group would go (no object is made).</summary>
        static Spot Test(ZWorld w, string id, Team team, Vector3 p)
        {
            switch (KindOf(id))
            {
                case UeKind.Robot: case UeKind.Hero:
                { var t = FootTile(w, p, 1); return FreeSpot(w, id, p, t.x, t.y, 1, true, false, out _, out _) ? Spot.Ok : Spot.NoRoom; }
                case UeKind.Vehicle: case UeKind.Gun:
                {
                    var t = FootTile(w, p, 2);
                    if (team == Team.Neutral && OnAnObstacle(w, t.x, t.y, 2)) return Spot.OnObstacle;
                    return FreeSpot(w, id, p, t.x, t.y, 2, false, false, out _, out _) ? Spot.Ok : Spot.NoRoom;
                }
                case UeKind.Shuttle: return ShuttleSpotNear(w, p, CamYaw - 90f) ? Spot.Ok : Spot.NoRoom;
                case UeKind.Flag:
                {
                    var t = FootTile(w, p, 1);
                    if (OnAnObstacle(w, t.x, t.y, 1)) return Spot.OnObstacle;
                    return FreeSpot(w, id, p, t.x, t.y, 1, true, true, out _, out _) ? Spot.Ok : Spot.NoRoom;
                }
                case UeKind.Grenades:
                {
                    var t = FootTile(w, p, 1);
                    if (OnAnObstacle(w, t.x, t.y, 1)) return Spot.OnObstacle;
                    if (OnAPlatform(w, t.x, t.y, 1)) return Spot.OnPlatform;
                    return FreeSpot(w, id, p, t.x, t.y, 1, true, true, out _, out _) ? Spot.Ok : Spot.NoRoom;   // the crate: the flag's spot rule
                }
                case UeKind.Obstacle:
                {
                    if (id == Box) return TestBox(w, p);                                                    // 1.5 ue_boxes: on top of the boxes under it (View/OzUePlayground.Boxes.cs)
                    var t = FootTile(w, p, ObstacleTiles);
                    if (OnAPlatform(w, t.x, t.y, ObstacleTiles)) return Spot.OnPlatform;
                    if (w.ObstacleSpotFree(t.x, t.y, ObstacleTiles, out string why)) return Spot.Ok;
                    return why == "obstacle" ? Spot.ObstacleOverObstacle : why == "flag" ? Spot.ObstacleOverFlag : why == "empty" ? Spot.ObstacleOverEmpty
                         : why == "unit" ? Spot.ObstacleOverUnits : Spot.NoRoom;
                }
                case UeKind.Platform: return TestPlatform(w, id, p);
            }
            return Spot.NoWorld;
        }

        /// <summary>The drop preview's validity: every group of the formation has a place (why = the toast words of spec 9.3 when not).</summary>
        public static bool CanDrop(string id, Team team, Vector3 centre, int count, out string why)
        {
            why = null;
            var w = W; if (w == null) { why = Words(Spot.NoWorld); return false; }
            if (AlwaysNeutral(id)) team = Team.Neutral;
            var rule = TeamRule(id, team);
            if (rule != Spot.Ok) { why = Words(rule); return false; }
            if (OzUeBlasts.Is(id)) return OzUeBlasts.CanDrop(out why); /* 1.5 ue_explosions_6 */ if (KindOf(id) == UeKind.Nuke) return OzUeNuke.CanCall(out why);                                 // 1.3 item 2: anywhere on the map, once its 30 s are up (View/OzUeNuke.cs)
            if (!CapRoomNow(id, team, out why)) return false;                                                // not even one group fits the unit cap (View/OzUePlayground.Cap.cs)
            foreach (var p in Formation(id, count, centre, CamYaw))
            {
                var s = Test(w, id, team, p);
                if (s != Spot.Ok) { why = Words(s); return false; }
            }
            return true;
        }

        /// <summary>Drops count groups (x1 / x3 / x5 / x10 / x20) of id for team around the floor point centre, in the formation the preview shows.
        /// made = their leaders / objects; why = the toast words when nothing or not everything could be placed (the caller shows it).</summary>
        public static UeDrop Drop(string id, Team team, Vector3 centre, int count) => DropCore(id, team, centre, count, true, -1);

        static UeDrop DropCore(string id, Team team, Vector3 centre, int count, bool sound, int amount)
        {
            var d = new UeDrop { made = new List<ZObject>() };
            var g = G; var w = W;
            Drops++;
            if (id == ZEvilZod.Type) { team = Team.Blue; amount = 1; }                                        // DV161: a robot of the blue team, one of him, whatever the team button says (Sim/ZEvilZod.cs)
            if (id == Heroes && team == Team.Blue) team = Team.Red;
            if (g == null || w == null) { d.why = Words(Spot.NoWorld); Refused++; if (sound) AudioDirector.UI("ui_error"); return d; }
            if (AlwaysNeutral(id)) team = Team.Neutral;
            var rule = TeamRule(id, team);
            if (rule != Spot.Ok) { d.why = Words(rule); Refused++; if (sound) AudioDirector.UI("ui_error"); Debug.Log($"{OzUnitEditor.Tag} drop of {id} refused: {d.why}"); return d; }
            if (OzUeBlasts.Is(id)) return BlastDrop(g, w, id, centre, sound, d); /* 1.5 ue_explosions_6 */ if (KindOf(id) == UeKind.Nuke) return NukeDrop(g, w, centre, sound, d);                         // 1.3 item 2: the bomber is called, nothing is placed (View/OzUePlayground.Nuke.cs)
            Spot firstBad = Spot.Ok;
            Vector3 soundAt = centre;
            string full = null;                                                                               // the unit cap (View/OzUeCap.cs, View/OzUePlayground.Cap.cs)
            foreach (var p in Formation(id, count, centre, CamYaw))
            {
                if (!CapRoom(id, team, amount, out full)) break;                                              // this group would pass the cap: it and the rest stay out
                var o = MakeOne(g, w, id, team, p, amount, out Spot s);
                if (o == null) { if (firstBad == Spot.Ok) firstBad = s; continue; }
                if (d.made.Count == 0) soundAt = g.CenterWorld(o);
                d.made.Add(o);
            }
            d.groups = d.made.Count; d.ok = d.groups > 0;
            if (firstBad != Spot.Ok) d.why = Words(firstBad);
            if (full != null) d.why = CapWords(full, d.groups, Groups(id, count));
            GroupsMade += d.groups;
            if (!d.ok) Refused++;
            if (sound)
            {
                if (d.ok) { var k = KindOf(id); OzSoundV3.CheatDrop(id, k == UeKind.Robot || k == UeKind.Hero, k == UeKind.Gun || k == UeKind.Flag || k == UeKind.Obstacle || k == UeKind.Grenades || k == UeKind.Platform, soundAt); AudioDirector.UI("ui_confirm"); }
                else AudioDirector.UI("ui_error");
            }
            Debug.Log($"{OzUnitEditor.Tag} drop {Groups(id, count)} x {OzUeUi.TeamWord(team)} {id} at ({centre.x:0.0}, {centre.z:0.0}): {d.groups} made{(d.why != null ? ", " + d.why : "")}");
            if (d.ok) { MarkChanged(); foreach (var o in d.made) RaiseSpawned(o); }
            return d;
        }

        static ZObject MakeOne(ZGame g, ZWorld w, string id, Team team, Vector3 p, int amount, out Spot why)
        {
            why = Spot.Ok;
            switch (KindOf(id))
            {
                case UeKind.Robot:
                {
                    var t = FootTile(w, p, 1);
                    if (!FreeSpot(w, id, p, t.x, t.y, 1, true, false, out int tx, out int ty)) { why = Spot.NoRoom; return null; }
                    var o = w.CreateRobotGroup(id, tx * 16, ty * 16, team, amount > 0 ? Mathf.Clamp(amount, 1, 8) : -1);
                    g.SpawnUnitView(o); foreach (var m in o.Minions) g.SpawnUnitView(m);
                    return o;
                }
                case UeKind.Hero:
                {
                    var t = FootTile(w, p, 1);
                    if (!FreeSpot(w, id, p, t.x, t.y, 1, true, false, out int tx, out int ty)) { why = Spot.NoRoom; return null; }
                    var o = w.CreateHeroSquad(ZHeroSquads.AlanBrad, team, new[] { tx, ty });
                    if (o == null) { why = Spot.NoWorld; return null; }
                    g.SpawnUnitView(o); foreach (var m in o.Minions) g.SpawnUnitView(m);
                    return o;
                }
                case UeKind.Vehicle: case UeKind.Gun:
                {
                    var t = FootTile(w, p, 2);
                    if (team == Team.Neutral && OnAnObstacle(w, t.x, t.y, 2)) { why = Spot.OnObstacle; return null; }
                    if (!FreeSpot(w, id, p, t.x, t.y, 2, false, false, out int tx, out int ty)) { why = Spot.NoRoom; return null; }
                    var o = w.CreateObject(KindOf(id) == UeKind.Gun ? "cannon" : "vehicle", id, tx * 16, ty * 16, team);   // owned: its grunt driver inside; NEUTRAL: empty
                    g.SpawnUnitView(o);
                    return o;
                }
                case UeKind.Shuttle:
                {
                    var px = Px(p);
                    var s = w.SpawnShuttle(px.x, px.y, team, CamYaw - 90f, out string sw);   // nose up the screen, as the testing panel drops it
                    if (s == null) { why = sw == "no room" ? Spot.NoRoom : Spot.Off; return null; }
                    var v = OzPlayableShuttle.SpawnView(g, s);
                    if (v) OzShuttleEmptyLook.Ensure(v);
                    return s;
                }
                case UeKind.Flag:
                {
                    var t = FootTile(w, p, 1);
                    if (OnAnObstacle(w, t.x, t.y, 1)) { why = Spot.OnObstacle; return null; }
                    // ue_flag_anywhere: on a platform too (the deck or the ramp, never a rail); its view stands on it (View/FlagView.Deck.cs)
                    if (!FreeSpot(w, id, p, t.x, t.y, 1, true, true, out int tx, out int ty)) { why = Spot.NoRoom; return null; }
                    var f = w.CreateObject("map_item", "flag", tx * 16, ty * 16, Team.Neutral);    // the game's capture flag (in World.Flags)
                    FlagView.AddRuntimeFlag(f);
                    return f;
                }
                case UeKind.Grenades:
                {
                    var t = FootTile(w, p, 1);
                    if (OnAnObstacle(w, t.x, t.y, 1)) { why = Spot.OnObstacle; return null; }
                    if (OnAPlatform(w, t.x, t.y, 1)) { why = Spot.OnPlatform; return null; }
                    if (!FreeSpot(w, id, p, t.x, t.y, 1, true, true, out int tx, out int ty)) { why = Spot.NoRoom; return null; }
                    var c = w.CreateObject("map_item", Grenades, tx * 16, ty * 16, Team.Neutral);   // the levels' grenade crate (Grenades = grenades_per_box)
                    OzUeCrateViews.Add(g, c);                                                           // its view Z_grenades bound as a level's crate (ZGame.StaticViews), so
                                                                                                        // GrenadeCrateV2 plays the lid, the pop, the pickup and the break sounds (View/OzUeCrateViews.cs)
                    return c;
                }
                case UeKind.Obstacle:
                {
                    if (id == Box) return MakeBox(w, p, out why);                                           // 1.5 ue_boxes: the Sim's box on its level + its hologram (View/OzUePlayground.Boxes.cs)
                    var t = FootTile(w, p, ObstacleTiles);
                    if (OnAPlatform(w, t.x, t.y, ObstacleTiles)) { why = Spot.OnPlatform; return null; }
                    var o = w.PlaceObstacle(t.x, t.y, ObstacleTiles, ObstacleHp, out string ow);
                    if (o == null)
                    {
                        why = ow == "obstacle" ? Spot.ObstacleOverObstacle : ow == "flag" ? Spot.ObstacleOverFlag : ow == "empty" ? Spot.ObstacleOverEmpty : ow == "unit" ? Spot.ObstacleOverUnits : Spot.NoRoom;
                        return null;
                    }
                    obstacleIds.Add(o.Id);
                    OzUeObstacleViews.Add(o);
                    return o;
                }
                case UeKind.Platform: return MakePlatform(w, id, p, out why);
            }
            why = Spot.NoWorld;
            return null;
        }

        static void RaiseSpawned(ZObject o)
        {
            try { Spawned?.Invoke(o); }
            catch (Exception e) { Debug.LogError($"{OzUnitEditor.Tag} a Spawned listener threw: {e}"); }
        }

        // ------------------------------------------------------------------------------------------------ removal
        /// <summary>A squad (all members), vehicle, gun, shuttle (with whoever is aboard), flag or obstacle off the field at once: no death.</summary>
        public static int Remove(ZObject o)
        {
            int units = UnitsIn(o);
            int n = RemoveQuiet(o);
            if (n > 0) { AudioDirector.UI("ui_cancel"); MarkChanged(); Debug.Log($"{OzUnitEditor.Tag} removed {IdOf(o) ?? o.Type} #{o.Id} ({units} unit(s))"); }
            return n;
        }

        /// <summary>Delete: the picked units, else the inspected flag / obstacle / empty hardware / grenade crate; toast "N UNITS REMOVED" for more than one.</summary>
        public static int RemovePicked()
        {
            var input = In; int units = 0, n = 0;
            if (input && input.Selection.Count > 0)
            {
                var list = input.Selection.ToArray();
                foreach (var u in list) { int k = UnitsIn(u); if (RemoveQuiet(u) > 0) { units += k; n++; } }
            }
            else if (OzUePick.Inspected != null && (OzUePick.Kind == UeInspect.Flag || OzUePick.Kind == UeInspect.Obstacle || OzUePick.Kind == UeInspect.EmptyHardware
                                                    || OzUePick.Kind == UeInspect.Crate || OzUePick.Kind == UeInspect.Platform))
            {
                var o = OzUePick.Inspected; units = UnitsIn(o);
                if (RemoveQuiet(o) > 0) n++;
            }
            if (n == 0) return 0;
            AudioDirector.UI("ui_cancel"); MarkChanged();
            if (units > 1) OzUeToast.Show(units + " UNITS REMOVED");
            Debug.Log($"{OzUnitEditor.Tag} Delete: {n} object(s), {units} unit(s) removed");
            return Mathf.Max(units, n);
        }

        /// <summary>Units a field object stands for: a squad's live members, 1 for a vehicle, gun or shuttle, 0 for a flag or an obstacle.</summary>
        static int UnitsIn(ZObject o)
        {
            if (o == null || !o.IsUnit) return 0;
            var lead = o.Leader ?? o; int n = 1;
            foreach (var m in lead.Minions) if (m != null && !m.Destroyed && m.KillAt < 0) n++;
            return n;
        }

        static int RemoveQuiet(ZObject o)
        {
            var g = G; var w = W;
            if (o == null || g == null || w == null) return 0;
            if (IsPlatform(o)) PlatformLeaves(g, w, o);                                                     // 1.3: what stands on it goes first, quietly; its tiles open (View/OzUePlayground.Platforms.cs)
            var lead = o.Leader != null && w.Get(o.Leader.Id) == o.Leader ? o.Leader : o;
            var members = new List<ZObject>(1 + lead.Minions.Count) { lead }; members.AddRange(lead.Minions);
            bool flag = IsFlag(lead), obst = IsObstacle(lead);
            var input = In;
            if (input) foreach (var m in members) input.Selection.Remove(m);
            int n = w.RemoveNow(lead);
            if (obst) w.BoxSettle();                                                                         // 1.5 ue_boxes: the boxes above a removed box fall at once, also while paused (Sim/ZWorld.UeBoxes.cs)
            foreach (var m in members) DropView(g, m.Id);
            OzUeCrateViews.Remove(g, lead.Id);                                                              // a grenade crate: its level view goes too (nothing for anything else)
            if (flag) FlagView.RemoveRuntimeFlag(lead);
            if (obst) OzUeObstacleViews.Remove(lead);
            OzUePlatformViews.Remove(lead);                                                                  // nothing for anything else
            flagTakes.Remove(lead.Id);
            Removed += n;
            return n;
        }

        static void DropView(ZGame g, int id)
        {
            if (!g.Views.TryGetValue(id, out var v)) return;
            g.Views.Remove(id);
            if (v) { v.gameObject.SetActive(false); UnityEngine.Object.Destroy(v.gameObject); }       // 1.3 item 9: gone from the frame at once (Destroy waits for the frame's end; CLEAR FIELD counts what is still drawn)
        }

        /// <summary>CLEAR FIELD (E024): every unit, flag and obstacle off the field, the burning ground, rubble and scorch marks gone, the
        /// </summary>
        public static int ClearField()
        {
            var g = G; var w = W;
            if (g == null || w == null) return 0;
            int n = ClearQuiet(g, w);
            AudioDirector.UI("ui_cancel"); MarkChanged(); Cleared++;
            Debug.Log($"{OzUnitEditor.Tag} CLEAR FIELD: {n} object(s) removed");
            return n;
        }

        /// <summary>The check's clean field between two tests: every unit off, the platforms stay.</summary>
        internal static void ClearUnits()
        {
            var g = G; var w = W; if (g == null || w == null) return;
            var list = new List<ZObject>(Placed());
            foreach (var o in list) if (!IsPlatform(o)) RemoveQuiet(o);
        }

        static int ClearQuiet(ZGame g, ZWorld w)
        {
            int n = 0;
            ClearStep("units", () =>
            {
                var list = new List<ZObject>(Placed());
                foreach (var o in list) n += RemoveQuiet(o);
                var rest = new List<ZObject>();
                foreach (var o in w.Objects) if (!o.Destroyed && o.KillAt < 0 && (o.IsUnit || IsFlag(o) || IsCrate(o) || o.Type == ZWorld.WreckType)) rest.Add(o);   // anything Placed() did not list; the dead go by the sim's own next tick
                foreach (var o in rest) n += RemoveQuiet(o);
            });
            ClearStep("ground", () =>
            {
                w.GroundFires.Clear();
                w.PlaygroundNukeCancel();                                                                     // 1.3 item 2: the rest of a running NUKE wave goes with the field
                var mid = new Vector3(w.Map.width, 0f, -w.Map.height);                                       // the map's centre (m)
                float far = Mathf.Max(w.Map.width, w.Map.height) * 2f;
                if (FxManager.I) FxManager.I.ClearRubble(mid, far);
                ScorchDecals.ClearIn(mid, far);
            });
            ClearStep("wrecks", () =>
            {
                var dead = new List<UnitView>();
                foreach (var kv in g.Views) if (kv.Value && w.Get(kv.Key) == null) dead.Add(kv.Value);
                foreach (var v in dead) v.Hide();
                WreckViewsGone(w);                                                                           // 1.3 item 9: also a wreck's view the game's list does not hold, and one whose unit is dying
            });
            ClearStep("crates", () => OzUeCrateViews.Sweep(g, w));                                          // the views of crates collected or blown up (hidden, as in a level)
            ClearStep("debris", () => SweepDebris());
            ClearStep("structure rubble", () => OzUeStructureBreak.ClearAll());
            var input = In; if (input) input.Selection.Clear();
            ClearLeft = CountLeftAfterClear(w);                                                              // 1.3 item 9: what is still drawn of units and wrecks (want 0)
            if (ClearLeft > 0) Debug.LogWarning($"{OzUnitEditor.Tag} CLEAR FIELD: {ClearLeft} unit or wreck view(s) still drawn");
            return n;
        }

        /// <summary>1.3 item 9: CLEAR FIELD's passes each on their own: a pass that fails is logged and the others still run (before, one
        /// failing object ended the whole clear and every wreck after it stayed on the field).</summary>
        static void ClearStep(string what, System.Action step)
        {
            try { step(); }
            catch (System.Exception e) { ClearErrors++; Debug.LogError($"{OzUnitEditor.Tag} CLEAR FIELD ({what}): {e.Message}"); }
        }

        /// <summary>1.3 item 9 check: unit and wreck views still drawn after the last CLEAR FIELD (want 0); passes that failed and stray
        /// views hidden (all clears of this session).</summary>
        public static int ClearLeft, ClearErrors, ClearStrayViews;

        /// <summary>1.3 item 9: every unit view still drawn whose unit is gone from the rules or is dead (a wreck) is hidden as the other
        /// wrecks are, whether or not the game's view list holds it.</summary>
        static void WreckViewsGone(ZWorld w)
        {
            foreach (var v in UnityEngine.Object.FindObjectsByType<UnitView>(FindObjectsSortMode.None))
            {
                if (!v || !v.gameObject.activeInHierarchy || v.Obj == null) continue;                        // a view never bound to a unit is not a field object
                if (w.Get(v.Obj.Id) == v.Obj && !v.Dying) continue;
                v.Hide(); ClearStrayViews++;
            }
        }

        /// <summary>1.3 item 9: what is still drawn of units and wrecks: unit views of gone or dead units, wreck pieces, rolling wheels,
        /// shuttle pieces (the check wants 0 after CLEAR FIELD).</summary>
        public static int CountLeftAfterClear(ZWorld w)
        {
            int n = 0;
            foreach (var v in UnityEngine.Object.FindObjectsByType<UnitView>(FindObjectsSortMode.None))
                if (v && v.gameObject.activeInHierarchy && v.Obj != null && (w == null || w.Get(v.Obj.Id) != v.Obj || v.Dying)) n++;
            foreach (var p in UnityEngine.Object.FindObjectsByType<WreckPieces>(FindObjectsSortMode.None)) if (p && p.gameObject.activeInHierarchy) n++;
            foreach (var r in UnityEngine.Object.FindObjectsByType<RollingWheel>(FindObjectsSortMode.None)) if (r && r.gameObject.activeInHierarchy) n++;
            foreach (var s in UnityEngine.Object.FindObjectsByType<OzShuttleBreakup>(FindObjectsSortMode.None)) if (s && s.gameObject.activeInHierarchy) n++;
            return n;
        }

        /// <summary>Run 11 check: loose pieces CLEAR FIELD took (all clears of this session) and the chunks it ended.</summary>
        public static int DebrisSwept, ChunksSwept;

        /// <summary>
        /// are no field objects and so stayed after CLEAR FIELD: every wreck piece (WreckPieces: break-apart plates, torn track lengths,
        /// wheels, hatches, barrels, the mech's lost hand gun, the gun's plates), every shuttle piece, every other physics piece on the debris
        /// layer that is no part of a unit's view (rolling wheels, chunks), and every rubble / falling chunk of the effects (FxManager). Gone at
        /// once. The editor's floor and grid, the unit views and the field's own objects are not touched. Playground only.</summary>
        static int SweepDebris()
        {
            var gone = new HashSet<GameObject>();
            foreach (var wp in UnityEngine.Object.FindObjectsByType<WreckPieces>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (wp) gone.Add(wp.gameObject);
            foreach (var sp in UnityEngine.Object.FindObjectsByType<OzShuttleBreakup>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (sp) gone.Add(sp.gameObject);
            int layer = BuildingBreaker.DebrisLayer;
            foreach (var rb in UnityEngine.Object.FindObjectsByType<Rigidbody>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!rb || gone.Contains(rb.gameObject) || rb.GetComponentInParent<UnitView>(true)) continue;
                bool debris = rb.gameObject.layer == layer;
                if (!debris) foreach (var c in rb.GetComponentsInChildren<Collider>(true)) if (c.gameObject.layer == layer) { debris = true; break; }
                if (debris) gone.Add(rb.gameObject);
            }
            foreach (var go in gone) if (go) { go.SetActive(false); UnityEngine.Object.Destroy(go); }
            int chunks = FxManager.I ? FxManager.I.ClearChunks() : 0;
            DebrisSwept += gone.Count; ChunksSwept += chunks;
            Debug.Log($"{OzUnitEditor.Tag} CLEAR FIELD: {gone.Count} loose piece(s) and {chunks} chunk(s) taken");
            return gone.Count;
        }

        // ------------------------------------------------------------------------------------------------ sample fight (spec E025)
        /// <summary>SAMPLE FIGHT: round the free field's centre, left and right of the (turned) view, rows 6.5 m apart along its depth:
        /// blue grunt, psycho, light tank, gatling, sniper, jeep at -(6 + 3 (i mod 2)) m (the gatling 5 m further back), red grunt, tough,
        /// mech, pyro, howitzer, laser at +(6 + 3 ((i + 1) mod 2)) m (the howitzer 8 m further back), 1.5 m up the view from the blue row
        /// (UI/OzUePlan.cs). On free ground only (never over units, flags or obstacles), at most OzUeCap.SampleFightsMax on the field at
        /// once and within the unit cap (View/OzUePlayground.Cap.cs). One drop sound for the whole fight. Returns the groups made (0 =
        /// refused, with its toast and ui_error).</summary>
        public static int SampleFight() => SampleFightFree();

        /// <summary>The floor point under the middle of the free field (the view centre the camera keeps), else the camera's focus.</summary>
        static Vector3 ViewCentre(ZGame g)
        {
            var rts = Ed ? Ed.Rts : null;
            var cam = rts ? rts.GetComponent<Camera>() : Camera.main;
            var r = OzUeInsets.FreeFieldScreen;
            if (cam && r.width > 1f && r.height > 1f && g.GroundPoint(cam.ScreenPointToRay(r.center), out var p)) { p.y = 0f; return p; }
            return rts ? new Vector3(rts.focus.x, 0f, rts.focus.z) : new Vector3(160f, 0f, -100f);
        }

        // ------------------------------------------------------------------------------------------------ field queries
        /// <summary>Live units of a team, each robot of a squad counted (as the HUD's top strip counts).</summary>
        public static int CountUnits(Team team)
        {
            var w = W; if (w == null) return 0;
            int n = 0;
            foreach (var o in w.Objects) if (o.Owner == team && o.IsUnit && !o.Destroyed && o.KillAt < 0 && !o.IsFortDefender) n++;
            return n;
        }

        /// <summary>How many of this id are on the field (robots and heroes per member; empty hardware counts for its type).</summary>
        public static int OnField(string id)
        {
            var w = W; if (w == null) return 0;
            var k = KindOf(id); int n = 0;
            foreach (var o in w.Objects)
            {
                if (o.Destroyed || o.KillAt >= 0) continue;
                switch (k)
                {
                    case UeKind.Robot: if (o.Kind == ZKind.Robot && o.Type == id && !o.IsHero) n++; break;
                    case UeKind.Hero: if (o.IsHero) n++; break;
                    case UeKind.Vehicle: case UeKind.Gun: if (o.IsUnit && o.Type == id) n++; break;
                    case UeKind.Shuttle: if (o.IsShuttle) n++; break;
                    case UeKind.Flag: if (IsFlag(o)) n++; break;
                    case UeKind.Obstacle: if (IsObstacle(o) && IsBox(o) == (id == Box)) n++; break;   // 1.5 ue_boxes: boxes and obstacles counted apart
                    case UeKind.Grenades: if (IsCrate(o)) n++; break;
                    case UeKind.Platform: if (IsPlatform(o) && PlatformIdOf(o) == id) n++; break;
                }
            }
            return n;
        }

        /// <summary>The palette id of a field object (a hero member = "heroes"), null for anything else.</summary>
        public static string IdOf(ZObject o)
        {
            if (o == null) return null;
            if (o.IsHero) return Heroes;
            if (o.IsShuttle) return ZWorld.ShuttleType;
            if (IsFlag(o)) return Flag;
            if (IsObstacle(o)) return IsBox(o) ? Box : Obstacle;                                          // 1.5 ue_boxes
            if (IsCrate(o)) return Grenades;
            if (IsPlatform(o)) return PlatformIdOf(o);
            return Array.IndexOf(Units, o.Type) >= 0 ? o.Type : null;
        }

        public static bool IsFlag(ZObject o) => o != null && o.Kind == ZKind.MapItem && o.Type == "flag";
        public static bool IsObstacle(ZObject o) => o != null && o.Type == ZWorld.WreckType && obstacleIds.Contains(o.Id);
        /// <summary>A grenade crate (the GRENADES extra or any crate of the field), still standing or not.</summary>
        public static bool IsCrate(ZObject o) => o != null && o.Kind == ZKind.MapItem && o.Type == Grenades;
        /// <summary>A vehicle, gun or shuttle nobody crews (neutral), still standing.</summary>
        public static bool IsEmptyHardware(ZObject o) => o != null && (o.Kind == ZKind.Vehicle || o.Kind == ZKind.Cannon) && o.Owner == Team.Neutral && !o.Destroyed && o.KillAt < 0;

        /// <summary>Live leaders / vehicles / guns / shuttles / flags / obstacles on the field.</summary>
        public static IEnumerable<ZObject> Placed()
        {
            var w = W; if (w == null) yield break;
            for (int i = 0; i < w.Objects.Count; i++)
            {
                var o = w.Objects[i];
                if (o.Destroyed || o.KillAt >= 0 || o.IsFortDefender) continue;
                if (o.IsUnit) { if (o.Kind != ZKind.Robot || o.Leader == null) yield return o; }
                else if (IsFlag(o) || IsObstacle(o) || IsCrate(o) || IsPlatform(o)) yield return o;
            }
        }

        /// <summary>The floor box round everything placed (each footprint included); false when the field is empty.</summary>
        public static bool FieldBounds(out Bounds b)
        {
            b = default; var g = G; bool any = false;
            if (g == null) return false;
            foreach (var o in Placed())
            {
                var c = ZGame.PxToWorld(o.CenterX, o.CenterY);
                var e = new Bounds(c, new Vector3(o.WidthPix / 8f, 0.1f, o.HeightPix / 8f));
                if (!any) { b = e; any = true; } else b.Encapsulate(e);
            }
            return any;
        }

        /// <summary>How many times the flag changed its owner since it was placed.</summary>
        public static int FlagTakes(ZObject flag) => flag != null && flagTakes.TryGetValue(flag.Id, out int n) ? n : 0;

        internal static void NoteFlagTaken(int flagId) { flagTakes.TryGetValue(flagId, out int n); flagTakes[flagId] = n + 1; }
        internal static void MarkChanged() => dirty = true;
        /// <summary>OzUeField.LateUpdate: FieldChanged once a frame at most.</summary>
        internal static void RaiseFieldChangedIfDirty()
        {
            if (!dirty) return;
            dirty = false;
            try { FieldChanged?.Invoke(); }
            catch (Exception e) { Debug.LogError($"{OzUnitEditor.Tag} a FieldChanged listener threw: {e}"); }
        }

        internal static void Reset()
        {
            Spawned = null; FieldChanged = null; obstacleIds.Clear(); flagTakes.Clear(); shuttleSpot.Clear(); dirty = false;
            ResetCap();                                                                                      // the sample fights on the field and the cap's counters (View/OzUePlayground.Cap.cs)
        }

        // ------------------------------------------------------------------------------------------------ the saved file's field block
        static string TeamKey(Team t) => t == Team.Red ? "red" : t == Team.Blue ? "blue" : "neutral";
        static bool TeamOf(object v, out Team t)
        {
            t = Team.Neutral; var s = v as string;
            if (s == "red") { t = Team.Red; return true; }
            if (s == "blue") { t = Team.Blue; return true; }
            return s == "neutral";
        }
        static double R2(double v) => Math.Round(v, 2);

        /// <summary>The "field" block of a saved file (ARCHITECTURE 5.7): {"items": [{"id", "team", "x", "z", "hp": [0..1 per member]}],
        /// "flags": [{"x", "z", "owner"}], "obstacles": [{"tx", "ty", "hp"}]}; positions in metres (x east, z the world's z), as a tree of
        /// Dictionary&lt;string, object&gt; / List&lt;object&gt; / double / string / bool only.</summary>
        public static object FieldToTree()
        {
            var items = new List<object>(); var flags = new List<object>(); var obst = new List<object>(); var crates = new List<object>(); var plats = new List<object>();
            foreach (var o in Placed())
            {
                var c = ZGame.PxToWorld(o.CenterX, o.CenterY);
                if (IsCrate(o)) { crates.Add(new Dictionary<string, object> { ["x"] = R2(c.x), ["z"] = R2(c.z), ["grenades"] = (double)o.Grenades }); continue; }   // "crates": [{"x", "z", "grenades"}]
                if (IsFlag(o)) { flags.Add(new Dictionary<string, object> { ["x"] = R2(c.x), ["z"] = R2(c.z), ["owner"] = TeamKey(o.Owner) }); continue; }
                if (IsPlatform(o)) { plats.Add(PlatformToTree(o)); continue; }                               // 1.3: "platforms": [{"id", "tx", "ty", "dir", "hp"}]
                if (IsBox(o)) continue;                                                                       // 1.5 ue_boxes: the "boxes" block (BoxesToTree)
                if (IsObstacle(o)) { obst.Add(new Dictionary<string, object> { ["tx"] = (double)(o.LocX >> 4), ["ty"] = (double)(o.LocY >> 4), ["hp"] = (double)o.Health }); continue; }
                var id = IdOf(o); if (id == null) continue;
                var hp = new List<object> { R2(Frac(o)) };
                foreach (var m in o.Minions) if (m != null && !m.Destroyed && m.KillAt < 0) hp.Add(R2(Frac(m)));
                items.Add(new Dictionary<string, object> { ["id"] = id, ["team"] = TeamKey(o.Owner), ["x"] = R2(c.x), ["z"] = R2(c.z), ["hp"] = hp });
            }
            var tree = new Dictionary<string, object> { ["items"] = items, ["flags"] = flags, ["obstacles"] = obst, ["crates"] = crates };
            if (plats.Count > 0) tree["platforms"] = plats;
            BoxesToTree(tree);
            return tree;
        }

        static double Frac(ZObject o) => o.MaxHealth > 0 ? Mathf.Clamp01((float)o.Health / o.MaxHealth) : 1.0;

        static bool Num(Dictionary<string, object> d, string k, out double v)
        {
            v = 0; if (d == null || !d.TryGetValue(k, out var o) || !(o is double x) || double.IsNaN(x) || double.IsInfinity(x)) return false;
            v = x; return true;
        }

        /// <summary>Rebuilds the field from a saved "field" block: the field is cleared first, then every known obstacle, flag and item is
        /// placed, in that order (positions clamped inside the map, hit points clamped 1 .. max, squads with the saved number of members). Unknown keys and
        /// broken entries are skipped. Returns the objects made; error = null, or why nothing could be read.</summary>
        public static int FieldFromTree(object tree, out string error)
        {
            error = null;
            var g = G; var w = W;
            if (g == null || w == null) { error = "THE PLAYGROUND IS NOT RUNNING."; return 0; }
            if (!(tree is Dictionary<string, object> root)) { error = "THE FILE HAS NO FIELD BLOCK."; return 0; }
            ClearQuiet(g, w);
            float wM = w.Map.width * 2f, hM = w.Map.height * 2f;
            Vector3 At(double x, double z) => new Vector3(Mathf.Clamp((float)x, 2f, wM - 2f), 0f, Mathf.Clamp((float)z, -(hM - 2f), -2f));
            int made = 0, skipped = 0;
            made += PlatformsFromTree(w, root, ref skipped);                                               // 1.3: the ramp platforms before anything else: the units then land on their decks (View/OzUePlayground.Platforms.cs)
            // the obstacles first (they need a free spot, and the units' free-tile search then walks round them), then the flags, then the units
            if (root.TryGetValue("obstacles", out var ob) && ob is List<object> obs)
                foreach (var e in obs)
                {
                    var d = e as Dictionary<string, object>;
                    if (d == null || !Num(d, "tx", out double tx) || !Num(d, "ty", out double ty)) { skipped++; continue; }
                    var c = ZGame.PxToWorld((tx + ObstacleTiles * 0.5) * 16, (ty + ObstacleTiles * 0.5) * 16);
                    var r = DropCore(Obstacle, Team.Neutral, At(c.x, c.z), 1, false, -1);
                    if (!r.ok) { skipped++; continue; }
                    if (Num(d, "hp", out double hp)) foreach (var o in r.made) o.Health = Mathf.Clamp((int)Math.Round(hp), 1, o.MaxHealth);
                    made += r.groups;
                }
            made += BoxesFromTree(root, ref skipped);                                                      // 1.5 ue_boxes: the boxes, lowest level first, after the obstacles
            if (root.TryGetValue("flags", out var fl) && fl is List<object> flags)
                foreach (var e in flags)
                {
                    var d = e as Dictionary<string, object>;
                    if (d == null || !Num(d, "x", out double x) || !Num(d, "z", out double z)) { skipped++; continue; }
                    Team owner = Team.Neutral; if (d.TryGetValue("owner", out var ov)) TeamOf(ov, out owner);
                    var r = DropCore(Flag, Team.Neutral, At(x, z), 1, false, -1);
                    if (!r.ok) { skipped++; continue; }
                    if (owner != Team.Neutral) foreach (var f in r.made) { f.Owner = owner; FlagView.RemoveRuntimeFlag(f); FlagView.AddRuntimeFlag(f); }
                    made += r.groups;
                }
            if (root.TryGetValue("crates", out var cr) && cr is List<object> crates)                       // the grenade crates, after the flags
                foreach (var e in crates)
                {
                    var d = e as Dictionary<string, object>;
                    if (d == null || !Num(d, "x", out double x) || !Num(d, "z", out double z)) { skipped++; continue; }
                    var r = DropCore(Grenades, Team.Neutral, At(x, z), 1, false, -1);
                    if (!r.ok) { skipped++; continue; }
                    if (Num(d, "grenades", out double gn)) foreach (var o in r.made) o.Grenades = Mathf.Clamp((int)Math.Round(gn), 1, 99);
                    made += r.groups;
                }
            if (root.TryGetValue("items", out var it) && it is List<object> items)
                foreach (var e in items)
                {
                    var d = e as Dictionary<string, object>;
                    string id = d != null && d.TryGetValue("id", out var iv) ? iv as string : null;
                    if (d == null || KindOf(id) == UeKind.None || AlwaysNeutral(id) || !Num(d, "x", out double x) || !Num(d, "z", out double z)
                        || !d.TryGetValue("team", out var tv) || !TeamOf(tv, out Team team)) { skipped++; continue; }
                    var hp = new List<double>();
                    if (d.TryGetValue("hp", out var hv) && hv is List<object> hl) foreach (var h in hl) if (h is double hd && !double.IsNaN(hd) && !double.IsInfinity(hd)) hp.Add(Math.Max(0.01, Math.Min(1.0, hd)));
                    int amount = KindOf(id) == UeKind.Robot && hp.Count > 0 ? Mathf.Clamp(hp.Count, 1, 8) : -1;
                    var r = DropCore(id, team, At(x, z), 1, false, amount);
                    if (!r.ok) { skipped++; continue; }
                    foreach (var o in r.made) { SetHp(o, hp, 0); int k = 1; foreach (var m in o.Minions) SetHp(m, hp, k++); }
                    made += r.groups;
                }
            MarkChanged();
            Debug.Log($"{OzUnitEditor.Tag} field loaded: {made} object(s) made, {skipped} entr(ies) skipped");
            return made;
        }

        static void SetHp(ZObject o, List<double> hp, int i)
        {
            if (o == null || o.MaxHealth <= 0 || i >= hp.Count) return;
            o.Health = Mathf.Clamp((int)Math.Round(hp[i] * o.MaxHealth), 1, o.MaxHealth);
        }

        // ------------------------------------------------------------------------------------------------ counted checks
        /// <summary>P1's palette and drop rules on paper (edit mode or Play): the lists in the game's own order, every unit in a fresh parse
        /// of the game's settings, the names, the class words, the neutral rules, the formations (groups and spacing at two camera turns),
        /// and the grid file of the playground map.</summary>
        internal static int SelfCheck(StringBuilder log)
        {
            int f = 0;
            f += OzUnitEditor.Line(log, Same(Robots, ZSettings.Robots) && Same(Array.FindAll(Vehicles, v => !OzBigTanks.Is(v)) /* 1.5 DV180 / DV181: not in the build lists */, ZSettings.Vehicles) && Same(Guns, ZBuildLists.Cannons),
                                   "palette order = the game's build lists (ZSettings.Robots, ZSettings.Vehicles, ZBuildLists.Cannons)");
            f += OzUnitEditor.Line(log, Units.Length == 24 && new HashSet<string>(Units).Count == 24, $"24 distinct units in the palette ({Units.Length})");   // DV145: + the medium tank
            var st = Resources.Load<TextAsset>("OpenZ/default_settings");
            var s = st ? ZSettings.Parse(st.text) : null;
            int rows = 0; var missing = new List<string>();
            if (s != null) foreach (var id in Units) { var k = KindOf(id); if (k == UeKind.Hero || k == UeKind.Shuttle) continue; if (s.Unit(id) != null) rows++; else missing.Add(id); }
            f += OzUnitEditor.Line(log, s != null && missing.Count == 0, $"every robot / vehicle / gun has a row in a fresh parse of the game's settings ({rows} of 18{(missing.Count > 0 ? ", missing " + string.Join(" ", missing) : "")})");
            bool names = FullName("light") == "LIGHT TANK" && FullName("medium") == "MECH" && FullName("missile_launcher") == "MLRS" && FullName(Heroes) == "ALAN & BRAD"
                         && ShortName("electrobot") == "ELECTRO" && ShortName("howitzer") == "HOWITZ." && ShortName(Flag) == "FLAG" && FullName(Obstacle) == "OBSTACLE";
            f += OzUnitEditor.Line(log, names, "names: LIGHT TANK, MECH, MLRS, ALAN & BRAD, ELECTRO, HOWITZ., FLAG, OBSTACLE");
            bool words = ClassWord("grunt") == "Robot squad" && ClassWord("jeep") == "Vehicle" && ClassWord("gun") == "Gun" && ClassWord(Heroes) == "Hero squad"
                         && ClassWord(ZWorld.ShuttleType) == "Transport" && ClassWord(Flag) == "Capture flag" && ClassWord(Obstacle) == "Holo obstacle";
            f += OzUnitEditor.Line(log, words, "class words: Robot squad, Vehicle, Gun, Hero squad, Transport, Capture flag, Holo obstacle");
            bool neutral = CanBeNeutral("jeep") && CanBeNeutral("missile_cannon") && !CanBeNeutral("grunt") && !CanBeNeutral(Heroes) && !CanBeNeutral(ZWorld.ShuttleType)
                           && AlwaysNeutral(Flag) && AlwaysNeutral(Obstacle) && !AlwaysNeutral("jeep") && OpenZOnly("electrobot") && OpenZOnly(Heroes) && !OpenZOnly("grunt");
            f += OzUnitEditor.Line(log, neutral, "neutral rules: vehicles and guns can stand empty, robots / heroes / the shuttle never, flag and obstacle always neutral; OpenZ-only electro, heroes, shuttle");
            bool sides = TeamOnly(ZEvilZod.Type) == Team.Blue && TeamOnly(Heroes) == Team.Red && TeamOnly(ZWorld.ShuttleType) == Team.Neutral && TeamOnly("grunt") == Team.Neutral
                         && ForTeam(ZEvilZod.Type, Team.Blue) && !ForTeam(ZEvilZod.Type, Team.Red) && !ForTeam(ZEvilZod.Type, Team.Neutral) && ForTeam(Heroes, Team.Red) && !ForTeam(Heroes, Team.Blue) && !ForTeam(Heroes, Team.Neutral)
                         && ForTeam(ZWorld.ShuttleType, Team.Red) && ForTeam(ZWorld.ShuttleType, Team.Blue) && ForTeam("grunt", Team.Blue) && ForTeam("jeep", Team.Neutral);
            f += OzUnitEditor.Line(log, sides, "one-side heroes (rows 79 / 80): EvilZod offered under BLUE only (not RED, not NEUTRAL), ALAN & BRAD under RED only (not BLUE, not NEUTRAL); the shuttle and every other unit under every team button");
            foreach (float yaw in new[] { 0f, 90f })
                foreach (var (id, gap) in new[] { ("grunt", 5f), ("jeep", 7f), (ZWorld.ShuttleType, 10f) })
                {
                    var f3 = Formation(id, 3, Vector3.zero, yaw); var f5 = Formation(id, 5, Vector3.zero, yaw); var f1 = Formation(id, 1, Vector3.zero, yaw);
                    float min = float.MaxValue; for (int i = 0; i < f5.Count; i++) for (int j = i + 1; j < f5.Count; j++) min = Mathf.Min(min, Vector3.Distance(f5[i], f5[j]));
                    var across = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
                    bool line = Mathf.Abs(Vector3.Dot((f3[2] - f3[0]).normalized, across)) > 0.999f && Mathf.Abs(Vector3.Distance(f3[0], f3[1]) - gap) < 0.01f;
                    f += OzUnitEditor.Line(log, f1.Count == 1 && f3.Count == 3 && f5.Count == 5 && line && min >= gap * 0.999f,
                                           $"formation {id} at a {yaw:0} deg turn: x1 1, x3 a line of 3 across the view {gap:0} m apart, x5 3 + 2 (closest {min:0.00} m)");
                    var f10 = Formation(id, 10, Vector3.zero, yaw); var f20 = Formation(id, 20, Vector3.zero, yaw);
                    float min20 = float.MaxValue; for (int i = 0; i < f20.Count; i++) for (int j = i + 1; j < f20.Count; j++) min20 = Mathf.Min(min20, Vector3.Distance(f20[i], f20[j]));
                    float bgap = BlockGapM(id);                                                                 // 1.4: x10 / x20 step in whole 2 m tiles
                    f += OzUnitEditor.Line(log, f10.Count == 10 && f20.Count == 20 && min20 >= bgap * 0.999f && min20 <= bgap * 1.001f,
                                           $"formation {id} at a {yaw:0} deg turn: x10 2 rows of 5, x20 4 rows of 5, {bgap:0} m apart on the map grid (closest {min20:0.00} m)");
                }
            foreach (var (bId, bFoot) in new[] { ("grunt", 1), ("jeep", 2) })                                // 1.4: the tiles an x20 really lands on (FootTile's rounding)
                foreach (float bYaw in new[] { 0f, 30f, 45f })
                {
                    var bf = Formation(bId, 20, new Vector3(169.9f, 0f, -101.2f), bYaw); var bt = new List<Vector2Int>(bf.Count);
                    foreach (var bq in bf) { var bp = ZGame.WorldToPx(bq); bt.Add(bFoot == 1 ? new Vector2Int(bp.x >> 4, bp.y >> 4) : new Vector2Int(Mathf.RoundToInt(bp.x / 16f - 1f), Mathf.RoundToInt(bp.y / 16f - 1f))); }
                    bool bEven = bt.Count == 20; Vector2Int bA = bEven ? bt[1] - bt[0] : Vector2Int.zero, bR = bEven ? bt[5] - bt[0] : Vector2Int.zero;
                    for (int bk = 1; bk < bt.Count && bEven; bk++) { if (bk % 5 != 0 && bt[bk] - bt[bk - 1] != bA) bEven = false; if (bk >= 5 && bt[bk] - bt[bk - 5] != bR) bEven = false; }
                    int bn = bFoot == 1 ? 3 : 4; bool bHome = bYaw != 0f || (bA == new Vector2Int(bn, 0) && bR == new Vector2Int(0, bn));   // no turn: 3 tiles (6 m) for squads, 4 (8 m) for vehicles
                    float bClose = float.MaxValue; for (int bi = 0; bi < bf.Count; bi++) for (int bj = bi + 1; bj < bf.Count; bj++) bClose = Mathf.Min(bClose, Vector3.Distance(bf[bi], bf[bj]));
                    f += OzUnitEditor.Line(log, bEven && bHome && bClose >= GapM(bId) - 0.01f,
                                           $"x20 {bId} at a {bYaw:0} deg turn lands on evenly spaced tiles: {bA.x},{bA.y} tiles along a row, {bR.x},{bR.y} between rows, closest {bClose:0.00} m (at least {GapM(bId):0} m)");
                }
            f += OzUnitEditor.Line(log, Formation(Flag, 5, Vector3.zero, 0f).Count == 1 && Formation(Obstacle, 3, Vector3.zero, 0f).Count == 1, "a flag or an obstacle drops one at a time");
            var grid = Resources.Load<TextAsset>("OpenZ/Maps/UE_grid");
            ZMapData map = null; try { map = grid ? ZMapData.FromJson(grid.text) : null; } catch (Exception) { map = null; }
            bool flat = map != null && map.classes != null && map.tiles != null && map.classes.Length == map.width * map.height && map.tiles.Length == map.width * map.height;
            if (flat) { foreach (var c in map.classes) if (c != 0) { flat = false; break; } }
            f += OzUnitEditor.Line(log, map != null && map.width == 160 && map.height == 100 && flat && (map.zones == null || map.zones.Count == 0) && (map.objects == null || map.objects.Count == 0)
                                         && map.file == "playground" && map.planet == "city",
                                   $"the playground grid Resources/OpenZ/Maps/UE_grid: {(map != null ? map.width + " x " + map.height + " tiles, file " + map.file + ", planet " + map.planet : "missing")}, all ground, no zones, no objects");
            f += OzUnitEditor.Line(log, ObstacleTiles == 3 && ObstacleHp == 10000 && Mathf.Approximately(FootprintM(Obstacle), 6f) && Mathf.Approximately(ObstacleHeightM, 3f),
                                   "obstacle: 3 x 3 tiles (6 x 6 m), 3 m tall, 10000 HP (Q3)");
            f += OzUnitEditor.Line(log, KindOf(Grenades) == UeKind.Grenades && ShortName(Grenades) == "GRENADES" && FullName(Grenades) == "GRENADE CRATE"
                                         && ClassWord(Grenades) == "Grenade crate" && AlwaysNeutral(Grenades) && !CanBeNeutral(Grenades) && Formation(Grenades, 5, Vector3.zero, 0f).Count == 1
                                         && Array.IndexOf(Extras, Grenades) == 2 && Array.IndexOf(Units, Grenades) < 0 && UnitsOfGroup(Grenades, -1) == 0,
                                   "GRENADES: the third extra (FLAG, OBSTACLE, GRENADES), named GRENADES / GRENADE CRATE / Grenade crate, always neutral, one per drop, no unit of the cap");
            var crateTile = Resources.Load<Texture2D>(OzUeArt.GrenadesTilePath);
            f += OzUnitEditor.Line(log, crateTile && crateTile.width == 256 && crateTile.height == 256 && OzUeArt.Picture(Grenades, Team.Neutral) == crateTile,
                                   "the GRENADES tile picture Resources/" + OzUeArt.GrenadesTilePath + ": " + (crateTile ? crateTile.width + " x " + crateTile.height : "missing") + ", the dock's picture of the crate");
            int pics = 0; var picsBad = new List<string>();
            foreach (var pth in OzUeLoadingPicture.Paths) { var pt = Resources.Load<Texture2D>(pth); if (pt && pt.width == 2560 && pt.height == 1440) pics++; else picsBad.Add(pth); }
            var pickRng = new System.Random(74); int lastPick = -1, repeats = 0; var used = new int[OzUeLoadingPicture.Paths.Length];
            for (int i = 0; i < 600; i++) { int k = OzUeLoadingPicture.Pick(lastPick, pickRng); if (k == lastPick) repeats++; used[k]++; lastPick = k; }
            bool allUsed = true; foreach (int u in used) allUsed &= u > 100;
            f += OzUnitEditor.Line(log, pics == 3 && OzUeLoadingPicture.Paths.Length == 3 && Array.IndexOf(OzUeLoadingPicture.Paths, "OpenZ/Loading/UE_loading_1") < 0 && repeats == 0 && allUsed,
                                   "the Unit editor's loading pictures UE_loading_2 / _3 / _4: " + pics + " of 3 at 2560 x 1440" + (picsBad.Count > 0 ? " (bad: " + string.Join(" ", picsBad) + ")" : "")
                                   + "; 600 random picks: " + repeats + " the same twice in a row, each used (" + string.Join(" / ", used) + "); UE_loading_1 not shown");
            return f;
        }

        static bool Same(string[] a, string[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }
    }
}
