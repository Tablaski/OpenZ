#if UNITY_EDITOR && !OZ_RELEASE
// OpenZ (GPL-3.0) - version 1.5 kit bizon_mammoth_driver_up_r7: the Play check of DV211 (editor only, never in a build).
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.5 kit bizon_mammoth_driver_up_r7: the Play check of DV211 "the big tanks' driver is up while they fight" (Sim/ZObject.BigTankUp.cs);
    /// menus OpenZ/Checks/V1.5/Big tanks: driver up ... (Editor/OpenZParity.V15BigTankUp.cs). In a running level (L02, or L18 where both big
    /// tanks are on the map) it finds free open ground near the camera and stages two rows one after the other: a player's BIZON, then a
    /// player's MAMMOTH (crewed, its psycho driver, the real hull and driver), each on an attack-move order through three enemy sniper squads
    /// standing 9-16 tiles ahead. Each row runs 60 s of sim time from its first fighting tick (or until the driver is shot out) and logs:
    /// seconds fighting, seconds the driver was up while fighting (and per minute), the snipers' shots at the tank, those while the driver was
    /// up, their hits and driver hits, whether the driver was shot out and when, the tank's and the hatch driver's shots. One frame per row at
    /// the normal RTS camera (the player's height, the camera flown to the tank) once the driver has been up 1.5 s in the fight.
    /// Runs (<see cref="Mode"/>): 0 NORMAL (DV211 as the game set it: on), 1 BEFORE (DV211 off for the run, put back after: today's numbers).
    /// The level's other units are quieted (Damage 0, put back) while a row runs; everything the check made is removed after each row.
    /// Report and frames: the kit's proof/play (play_report_normal.md / play_report_before.md, BIZON_*.png, MAMMOTH_*.png).
    /// </summary>
    public class OzBigTankUpPlayCheck : MonoBehaviour
    {
        public const string Tag = "[OpenZ V1.5 big tanks driver up Play]";
        public static string Folder = "";
        public static int Mode;
        public static bool Running { get; private set; }
        public static string Report { get; private set; } = "no Play check run yet";
        public const float RowSimSeconds = 60f, MaxRowSimSeconds = 100f, PictureAfterUpSec = 1.5f;
        /// <summary>The lead's goal: with DV211 the driver is up 85 % or more of the fighting time.</summary>
        public const double MinShare = 0.85;

        ZGame G; ZWorld W; RtsCamera rts; Team me, foe;
        readonly StringBuilder md = new StringBuilder();
        int pass, fail;
        readonly List<ZObject> made = new List<ZObject>();
        readonly Dictionary<ZObject, int> quieted = new Dictionary<ZObject, int>();

        // the row's per-tick count (ZWorld.AfterTickHook: sim ticks, not frames)
        ZObject tank; double lastShot = -100, firstFight = -1, fightSec, upFightSec, upSince = -1; bool counting;
        // the row's event count (every frame in Update, also while the coroutine waits for a picture)
        int shots, shotsUp, hits, driverHits, gunShots, driverShots; bool sniped; double snipedAt = -1;

        public static void Begin()
        {
            if (Running || !ZGame.I || ZGame.I.World == null) { Debug.LogWarning(Tag + " enter Play on a level first"); return; }
            ZGame.I.gameObject.AddComponent<OzBigTankUpPlayCheck>();
        }

        void Start() { Running = true; StartCoroutine(Run()); }
        void OnDestroy() { Running = false; if (ZWorld.AfterTickHook == (System.Action<ZWorld>)Tick) ZWorld.AfterTickHook = null; }

        static bool Up(ZObject t) => t != null && !t.Destroyed && t.Owner != Team.Neutral && t.Drivers.Count > 0 && t.CanBeSniped();

        void Update()
        {
            if (!counting || tank == null || W == null) return;
            bool upNow = Up(tank);
            foreach (var e in W.Events)
            {
                if (e.type == ZEventType.Hitscan && e.otherId == tank.Id)
                {
                    var a = W.Get(e.objId);
                    if (a != null && a.Type == "sniper") { shots++; if (upNow) shotsUp++; if (e.flag) hits++; if (e.flag2) driverHits++; }
                }
                else if (e.type == ZEventType.MissileFired && e.objId == tank.Id) { gunShots++; lastShot = W.Time; }
                else if (e.type == ZEventType.DriverShot && e.objId == tank.Id) driverShots++;
                else if (e.type == ZEventType.Sniped && e.objId == tank.Id && !sniped) { sniped = true; snipedAt = firstFight >= 0 ? W.Time - firstFight : 0; }
            }
        }

        void Tick(ZWorld w)
        {
            if (!counting || tank == null) return;
            bool owned = !tank.Destroyed && tank.Owner == me && tank.Drivers.Count > 0, up = Up(tank);
            bool fight = owned && (tank.BigTankFighting() || w.Time - lastShot < ZWorld.BigTankUpLingerSeconds);
            if (fight) { if (firstFight < 0) firstFight = w.Time; fightSec += ZConst.TICK; if (up) upFightSec += ZConst.TICK; }
            if (up && fight) { if (upSince < 0) upSince = w.Time; } else upSince = -1;
        }

        IEnumerator Run()
        {
            G = ZGame.I; W = G.World; rts = FindFirstObjectByType<RtsCamera>();
            me = G.player; foe = me == Team.Red ? Team.Blue : Team.Red;
            if (!string.IsNullOrEmpty(Folder)) Directory.CreateDirectory(Folder);
            bool was = ZWorld.BigTankUpDeviation; float speed = G.gameSpeed; var hook0 = ZWorld.AfterTickHook;
            string mode = Mode == 1 ? "BEFORE (DV211 off for this run)" : "NORMAL (DV211 as the game set it)";
            md.AppendLine($"# Big tanks: driver up while they fight, Play check ({System.DateTime.Now:yyyy-MM-dd HH:mm}), level {G.level}, run {mode}").AppendLine();
            try
            {
                if (!ZWorld.MammothDeviation || !ZWorld.BizonDeviation) { md.AppendLine("DV180 / DV181 off ('Zod exact'?): nothing to check"); yield break; }
                if (Mode == 0 && !ZWorld.BigTankUpDeviation) Row("switch", false, "DV211 is OFF in a NORMAL run (the game turns it on at every start; 'Zod exact'?)");
                if (Mode == 1) ZWorld.BigTankUpDeviation = false;
                G.gameSpeed = 1f;
                ZWorld.AfterTickHook = Tick;
                md.AppendLine($"switches: DV211 {(ZWorld.BigTankUpDeviation ? "on" : "off")}, DV87 hatch driver {(ZWorld.DriverFireDeviation ? "on" : "off")}, DC2 driver sniping {(ZWorld.SnipeDeviation ? "on" : "off")}; the player {me}, the snipers {foe}").AppendLine();
                foreach (var type in new[] { ZBigTanks.Bizon, ZBigTanks.Mammoth }) yield return RowRun(type);
            }
            finally
            {
                counting = false; tank = null;
                ZWorld.AfterTickHook = hook0; ZWorld.BigTankUpDeviation = was; if (G) G.gameSpeed = speed;
                Unquiet(); Tidy();
                md.AppendLine().AppendLine($"{pass} PASS, {fail} FAIL. Frames: {Folder}");
                Report = md.ToString();
                if (!string.IsNullOrEmpty(Folder)) { try { File.WriteAllText(Path.Combine(Folder, Mode == 1 ? "play_report_before.md" : "play_report_normal.md"), Report); } catch { } }
                Debug.Log(Tag + "\n" + Report);
                Mode = 0; Destroy(this);
            }
        }

        IEnumerator RowRun(string type)
        {
            string name = type == ZBigTanks.Bizon ? "BIZON" : "MAMMOTH";
            if (!FreeArea(out int ox, out int oy)) { Row(name, false, "no free 30 x 14 tiles of open dry ground near the camera: move the camera to open ground and run again"); yield break; }
            int tx = ox + 3 * 16, ty = oy + 7 * 16;
            tank = W.CreateObject("vehicle", type, tx - 16, ty - 16, me); made.Add(tank); W.Emit(ZEventType.Created, tank);
            var squads = new List<ZObject>();
            foreach (var (dx, dy) in new[] { (10, 0), (13, -4), (16, 3) })
            {
                var s = W.CreateRobotGroup("sniper", tx + dx * 16 - 8, ty + dy * 16 - 8, foe);
                squads.Add(s); made.Add(s); G.SpawnUnitView(s); foreach (var m in s.Minions) { made.Add(m); G.SpawnUnitView(m); }
            }
            var reds = squads.SelectMany(s => new[] { s }.Concat(s.Minions)).ToList();
            Quiet();
            yield return null;
            if (rts) rts.FlyTo(ZGame.PxToWorld(tx + 6 * 16, ty));
            yield return new WaitForSeconds(1.0f);
            lastShot = -100; firstFight = -1; fightSec = upFightSec = 0; upSince = -1;
            shots = shotsUp = hits = driverHits = gunShots = driverShots = 0; sniped = false; snipedAt = -1; counting = true;
            double start = W.Time; string picture = "";
            W.SendWaypoints(me, tank, new[] { ZWaypoint.Move(tx + 26 * 16, ty, true) });
            float nextFly = 0f;
            while (W.Time - start < MaxRowSimSeconds && !(firstFight >= 0 && W.Time - firstFight >= RowSimSeconds))
            {
                if (Time.time >= nextFly && rts && !tank.Destroyed) { rts.FlyTo(ZGame.PxToWorld(tank.CenterX, tank.CenterY)); nextFly = Time.time + 1.0f; }
                if (picture.Length == 0 && upSince >= 0 && W.Time - upSince >= PictureAfterUpSec)
                {
                    if (rts) rts.FlyTo(ZGame.PxToWorld(tank.CenterX, tank.CenterY));
                    yield return new WaitForSeconds(0.6f);
                    picture = $"{name}_{(Mode == 1 ? "before" : "normal")}_driver_up_t{(W.Time - firstFight):0.0}s.png";
                    if (!Up(tank)) picture = "";                                                   // he went down meanwhile: the next one
                    else { yield return new WaitForEndOfFrame(); if (!string.IsNullOrEmpty(Folder)) ScreenCapture.CaptureScreenshot(Path.Combine(Folder, picture)); }
                    continue;
                }
                if (sniped || tank.Destroyed) break;
                if (firstFight >= 0 && reds.All(m => m == null || m.Destroyed || m.KillAt >= 0) && W.Time - lastShot > ZWorld.BigTankUpLingerSeconds + 0.5) break;
                yield return null;
            }
            counting = false;
            double share = fightSec > 0 ? upFightSec / fightSec : 0;
            int down = reds.Count(m => m == null || m.Destroyed || m.KillAt >= 0);
            string what = $"DV211 {(ZWorld.BigTankUpDeviation ? "on" : "off")}: fighting {fightSec:0.0} s, the driver up {upFightSec:0.0} s of it ({100 * share:0.0} %, {60 * share:0.0} s per minute of fighting); "
                        + $"sniper shots at the tank {shots} ({shotsUp} while the driver was up), hits {hits}, driver hits {driverHits}; the driver shot out: {(sniped ? $"YES at {snipedAt:0.0} s" : "no")}; "
                        + $"snipers down {down} of {reds.Count}; tank shots {gunShots}, hatch driver shots {driverShots}; frame {(picture.Length > 0 ? picture : "NONE (the driver was never up 1.5 s)")}";
            bool ok = fightSec > 5 && (Mode == 1 || (share >= MinShare && picture.Length > 0));                    // BEFORE: today's numbers, measured only
            Row(name, ok, what);
            Unquiet(); Tidy();
            tank = null;
            yield return new WaitForSeconds(1.0f);
        }

        void Quiet()
        {
            foreach (var o in W.Objects)
                if (o.IsUnit && !made.Contains(o) && !o.Destroyed && o.Damage > 0 && !quieted.ContainsKey(o)) { quieted[o] = o.Damage; o.Damage = 0; }
        }

        void Unquiet()
        {
            foreach (var kv in quieted) if (kv.Key != null && !kv.Key.Destroyed) kv.Key.Damage = kv.Value;
            quieted.Clear();
        }

        void Tidy()
        {
            foreach (var o in made)
                if (o != null && !o.Destroyed && o.KillAt < 0) { o.SetHealth(0); W.UpdateObjectHealth(o, -1); }
            made.Clear();
        }

        bool FreeArea(out int ox, out int oy)
        {
            ox = oy = 0;
            var f = rts ? rts.focus : Vector3.zero;
            int cx = Mathf.RoundToInt(f.x * 8f) >> 4, cy = Mathf.RoundToInt(-f.z * 8f) >> 4;
            for (int r = 0; r < 50; r += 2)
                for (int dy = -r; dy <= r; dy += 2)
                    for (int dx = -r; dx <= r; dx += 2)
                    {
                        int tx = cx + dx - 15, ty = cy + dy - 7;
                        if (Free(tx, ty, 30, 14)) { ox = tx * 16; oy = ty * 16; return true; }
                    }
            return false;
        }

        bool Free(int tx, int ty, int w, int h)
        {
            if (tx < 2 || ty < 2 || tx + w >= W.Path.W - 2 || ty + h >= W.Path.H - 2) return false;
            for (int j = ty; j < ty + h; j++)
                for (int i = tx; i < tx + w; i++)
                {
                    if (!W.Path.TilePassableGrid(i, j, false, false)) return false;
                    var k = W.Map.ClassAt(i, j); if (k != ZClass.Ground && k != ZClass.Road) return false;
                }
            int x0 = tx * 16 - 32, y0 = ty * 16 - 32, x1 = (tx + w) * 16 + 32, y1 = (ty + h) * 16 + 32;
            if (W.Pathing != null) foreach (var L in W.Pathing.Lanes) if (L.ZX1 > x0 && L.ZX0 < x1 && L.ZY1 > y0 && L.ZY0 < y1) return false;
            foreach (var o in W.Objects) if (!o.Destroyed && o.CenterX > x0 && o.CenterX < x1 && o.CenterY > y0 && o.CenterY < y1 && (o.IsUnit || o.Kind == ZKind.Building)) return false;
            return true;
        }

        void Row(string row, bool ok, string what)
        {
            md.AppendLine($"- {(ok ? "PASS" : "FAIL")} {row}: {what}");
            if (ok) pass++; else fail++;
        }
    }
}
#endif
