using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// The Play proof of 1.3 owner item 3 (
    /// View/OzTowerAim.cs), menu OpenZ/V1.3/Tower guns: shots Play proof (Editor/OzTowerShotProofMenu.cs), on the
    /// level that is playing (2, 16 or 17). The enemy's tower gun nearest the map's middle is the target; every weapon in turn fires at it on the
    /// player's attack order from inside its range (outward from the fort), up to 20 s each, shooter and gun healed every frame. Counted per
    /// weapon (OzTowerAim's probe, the drawn rounds): rounds, drawn ends on the gun (within 2.5 m of its middle) / low (under 4 m over the
    /// ground) / into the sky, blasts on the gun / low; the rules' first shot after the order. Pictures from the normal RTS camera at 70 m, a
    /// fixed focus between the shooter and the tower: a round in flight and its end at the gun, per weapon. Output: <see cref="Folder"/>.
    /// Test only.
    /// </summary>
    public sealed class OzTowerShotProof : MonoBehaviour
    {
        public static string Report = "idle";
        /// <summary>The health the proof stages the target side's tower guns and every shooter with (test only; the ratio stays full).</summary>
        const int StagedHp = 1000000000;
        public static string Folder()
        {
            const string sub = "v13_tower_shots";
            var d = new System.IO.DirectoryInfo(Application.dataPath);
            for (int i = 0; i < 8 && d != null; i++, d = d.Parent)
                if (System.IO.Directory.Exists(System.IO.Path.Combine(d.FullName, "work", "records")))
                    return System.IO.Path.Combine(d.FullName, "work", "records", "milestones", sub);
            return System.IO.Path.Combine(Application.persistentDataPath, sub);
        }
        public static void Run() { Report = "running"; var go = new GameObject("OzTowerShotProof"); go.AddComponent<OzTowerShotProof>(); }

        struct Weapon { public string key, label, kind, type; public Weapon(string k, string l, string kd, string t) { key = k; label = l; kind = kd; type = t; } }
        static readonly Weapon[] Weapons =
        {
            new Weapon("grunt", "grunt rifles", "squad", "grunt"), new Weapon("psycho", "psycho machine guns", "squad", "psycho"),
            new Weapon("sniper", "sniper rifles", "squad", "sniper"), new Weapon("laser", "laser bolts", "squad", "laser"),
            new Weapon("pyro", "pyro flames", "squad", "pyro"), new Weapon("tough", "tough rockets", "squad", "tough"),
            new Weapon("grenade grunt", "grunt grenades", "grenades", "grunt"), new Weapon("electrobot", "electrobot bolts", "squad", "electrobot"),
            new Weapon("jeep", "jeep machine gun", "vehicle", "jeep"), new Weapon("light", "light tank", "vehicle", "light"),
            new Weapon("medium", "mech rockets", "vehicle", "medium"), new Weapon(ZMTank.Type, "medium tank", "vehicle", ZMTank.Type),
            new Weapon("heavy", "heavy tank twin barrels", "vehicle", "heavy"), new Weapon("missile_launcher", "MLRS salvo", "vehicle", "missile_launcher"),
            new Weapon("gatling", "gatling gun", "cannon", "gatling"), new Weapon("gun", "gun cannon", "cannon", "gun"),
            new Weapon("howitzer", "howitzer", "cannon", "howitzer"), new Weapon("missile_cannon", "missile cannon", "cannon", "missile_cannon"),
        };

        ZGame g; ZWorld W; RtsCamera rts; ZObject gun, fort; Team me = Team.Red, them = Team.Blue;
        readonly StringBuilder sb = new StringBuilder();
        int fails, passes, frames; string dir; Vector3? camAt;

        void Start() { dir = Folder(); System.IO.Directory.CreateDirectory(dir); StartCoroutine(All()); }
        void Update() { if (camAt.HasValue && rts) rts.focus = new Vector3(camAt.Value.x, 0f, camAt.Value.z); }

        void Log(string t) { sb.AppendLine(t); Debug.Log("[OzTowerShotProof] " + t); }
        void Check(string what, bool ok) { if (ok) passes++; else fails++; Log((ok ? "PASS " : "FAIL ") + what); }
        void Shot(string name) { ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, name + ".png")); frames++; Log($"frame {name}.png"); }
        static IEnumerator WaitReal(float s) { float t = 0f; while (t < s) { t += Time.unscaledDeltaTime; yield return null; } }
        static Vector3 W3(ZObject o) => ZGame.PxToWorld(o.CenterX, o.CenterY);

        IEnumerator All()
        {
            yield return null;
            g = ZGame.I; W = g ? g.World : null;
            if (W == null) { Check("a level is playing", false); Finish(); yield break; }
            me = g.player; them = me == Team.Red ? Team.Blue : Team.Red;
            rts = FindFirstObjectByType<RtsCamera>(); if (rts) { rts.FreeLookOff(); rts.SetHeightImmediate(70f); }
            W.Bots.Clear();                                                                         // nobody else gives orders during the proof
            int mx = W.Map.width * 8, my = W.Map.height * 8;
            gun = W.Objects.Where(o => o.IsTowerGun && o.Owner == them && !o.Destroyed && OzTowerAim.On(o))
                           .OrderBy(o => (long)(o.CenterX - mx) * (o.CenterX - mx) + (long)(o.CenterY - my) * (o.CenterY - my)).ThenBy(o => o.Id).FirstOrDefault();
            if (gun == null) { Check("an enemy gun drawn on a fort tower", false); Finish(); yield break; }
            foreach (var q in W.Objects) if (q.IsTowerGun && q.Owner == them && !q.Destroyed) { q.MaxHealth = StagedHp; q.Health = StagedHp; }   // 3 Nov: a burst killed the gun through the frame's healing and the L17 runs ended after 7 of the 13 weapons; staged, every weapon fires at the same gun
            fort = W.Objects.FirstOrDefault(f => f.IsFort && gun.CenterX >= f.LocX && gun.CenterX < f.LocX + f.WidthPix && gun.CenterY >= f.LocY && gun.CenterY < f.LocY + f.HeightPix);
            OzTowerAim.On(gun, out var gv);
            var mid = OzTowerAim.Middle(gun, gv);
            Log($"target: {gun.Owner} {gun.Type} #{gun.Id} on the fort tower at tile ({gun.LocX >> 4},{gun.LocY >> 4}); drawn middle {mid.y - g.GroundY(mid.x, mid.z):F1} m over the ground, {Vector3.Distance(new Vector3(mid.x, 0f, mid.z), new Vector3(W3(gun).x, 0f, W3(gun).z)):F1} m from its sim centre");
            OzTowerAim.ProbeReset(); OzTowerAim.Recording = true;
            foreach (var wpn in Weapons) yield return One(wpn);
            OzTowerAim.Recording = false;
            Log("the drawn rounds per weapon:\n" + OzTowerAim.ProbeText().TrimEnd());
            try { System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "tower_shots.txt"), OzTowerAim.ProbeText() + "\n" + OzTowerAim.Lines); } catch { }
            Finish();
        }

        IEnumerator One(Weapon wpn)
        {
            if (gun != null && gun.Destroyed)
            {   // one burst can kill the gun through the proof's healing: the next weapon takes the next tower gun of that side, so every weapon runs
                var was = gun;
                gun = W.Objects.Where(q => q.IsTowerGun && q.Owner == was.Owner && !q.Destroyed && OzTowerAim.On(q)).OrderBy(q => (long)(q.CenterX - was.CenterX) * (q.CenterX - was.CenterX) + (long)(q.CenterY - was.CenterY) * (q.CenterY - was.CenterY)).ThenBy(q => q.Id).FirstOrDefault();
                Log($"{wpn.label}: gun #{was.Id} was destroyed, next gun {(gun != null ? "#" + gun.Id : "none")}");
            }
            var u = W.S.Unit(wpn.type); if (u == null) yield break;
            if (gun == null) { Check($"{wpn.label}: a live tower gun left", false); yield break; }
            bool robot = wpn.kind == "squad" || wpn.kind == "grenades";
            double b = fort != null ? Mathf.Atan2(gun.CenterY - fort.CenterY, gun.CenterX - fort.CenterX) : 0.0;
            int dist = u.attack_radius - 24;
            int cx = gun.CenterX + (int)(Mathf.Cos((float)b) * dist), cy = gun.CenterY + (int)(Mathf.Sin((float)b) * dist);
            OzCheatSpawner.FreeTile(W, cx >> 4, cy >> 4, robot ? 1 : 2, robot, out int tx, out int ty);
            ZObject o = robot ? W.CreateRobotGroup(wpn.type, tx * 16, ty * 16, me) : W.CreateObject(wpn.kind == "cannon" ? "cannon" : "vehicle", wpn.type, tx * 16, ty * 16, me);
            if (o == null) { Check($"{wpn.label}: placed", false); yield break; }
            if (wpn.kind == "grenades") o.Grenades = 8;
            g.SpawnUnitView(o); foreach (var m in o.Minions) g.SpawnUnitView(m);
            var all = new List<ZObject> { o }; all.AddRange(o.Minions);
            foreach (var x in all) { x.MaxHealth = StagedHp; x.Health = StagedHp; }            // 3 Nov: one shell of the fort's other guns no longer takes the shooter off mid-count
            var ids = new HashSet<int>(all.Select(x => x.Id));
            var p0 = ZGame.PxToWorld(o.CenterX, o.CenterY); var pg = W3(gun);
            camAt = (p0 + pg) * 0.5f;
            OzTowerAim.Probes.TryGetValue(wpn.key, out var q0);
            int s0 = q0 != null ? q0.shots : 0, b0 = q0 != null ? q0.blasts : 0, on0 = q0 != null ? q0.endsOnGun : 0, low0 = q0 != null ? q0.endsLow : 0, sky0 = q0 != null ? q0.inSky : 0, e0 = q0 != null ? q0.ends : 0, bo0 = q0 != null ? q0.blastsOnGun : 0, bl0 = q0 != null ? q0.blastsLow : 0;
            yield return WaitReal(0.6f);
            Attack(o);
            int fresh = 0;
            double tOrder = W.Time, tFirst = -1; float t = 0f, nudge = 2f, flightPicAt = -1f; bool picFlight = false, picEnd = false;
            while (t < 20f)
            {
                foreach (var x in all) if (!x.Destroyed && x.KillAt < 0) x.Health = x.MaxHealth;
                if (!gun.Destroyed) gun.Health = gun.MaxHealth;
                if (t < 19f && !all.Exists(x => x != null && !x.Destroyed && x.KillAt < 0))
                {
                    Clear(all); fresh++;
                    o = robot ? W.CreateRobotGroup(wpn.type, tx * 16, ty * 16, me) : W.CreateObject(wpn.kind == "cannon" ? "cannon" : "vehicle", wpn.type, tx * 16, ty * 16, me);
                    if (o == null) break;
                    if (wpn.kind == "grenades") o.Grenades = 8;
                    g.SpawnUnitView(o); foreach (var m in o.Minions) g.SpawnUnitView(m);
                    all.Add(o); all.AddRange(o.Minions); foreach (var x in all) ids.Add(x.Id);
                    Attack(o);
                }
                bool endNow = false;
                for (int i = 0; i < W.Events.Count; i++)
                {
                    var e = W.Events[i];
                    if (!ids.Contains(e.objId) || e.otherId != gun.Id) continue;
                    if ((e.type == ZEventType.MissileFired || e.type == ZEventType.Hitscan || e.type == ZEventType.ElectroBolt) && tFirst < 0) tFirst = W.Time - tOrder;
                    if (e.type == ZEventType.MissileFired && !picFlight && flightPicAt < 0f) flightPicAt = t + Mathf.Max(0.05f, e.value * 0.45f / Mathf.Max(0.05f, g.gameSpeed));
                    if (!picEnd && (e.type == ZEventType.MissileExploded || e.type == ZEventType.Hitscan || e.type == ZEventType.ElectroBolt)) endNow = true;
                }
                if (endNow) { picEnd = true; t += Time.deltaTime; yield return null; Shot($"tower_{wpn.key.Replace(' ', '_')}_end"); }
                if (!picFlight && flightPicAt >= 0f && t >= flightPicAt) { picFlight = true; Shot($"tower_{wpn.key.Replace(' ', '_')}_flight"); }
                if ((nudge -= Time.deltaTime) <= 0f) { nudge = 2f; if (o.AttackObject != gun && o.Waypoints.Count == 0) Attack(o); }
                OzTowerAim.Probes.TryGetValue(wpn.key, out var q);
                if (q != null && q.shots - s0 >= 6 && t >= 6f) break;
                t += Time.deltaTime; yield return null;
            }
            OzTowerAim.Probes.TryGetValue(wpn.key, out var p);
            int shots = (p != null ? p.shots : 0) - s0, ends = (p != null ? p.ends : 0) - e0, onGun = (p != null ? p.endsOnGun : 0) - on0, low = (p != null ? p.endsLow : 0) - low0, sky = (p != null ? p.inSky : 0) - sky0;
            int blasts = (p != null ? p.blasts : 0) - b0, blastsOn = (p != null ? p.blastsOnGun : 0) - bo0, blastsLow = (p != null ? p.blastsLow : 0) - bl0;
            Log($"{wpn.label}: fresh shooters {fresh}; first shot {(tFirst >= 0 ? tFirst.ToString("0.0") + " s" : "NONE")} after the order; drawn rounds {shots}: ends {ends} (on the gun {onGun}, low {low}), into the sky {sky}; blasts {blasts} (on the gun {blastsOn}, low {blastsLow})");
            Check($"{wpn.label}: fires at the tower gun (>= 3 drawn rounds in 20 s)", shots >= 3);
            Check($"{wpn.label}: no round or blast drawn low (under {OzTowerAim.LowM:0} m over the ground: the tower's foot)", low == 0 && blastsLow == 0);
            Check($"{wpn.label}: >= 90 % of the drawn ends and blasts on the gun up there (within {OzTowerAim.OnGunM:0.0} m of its middle)", ends + blasts > 0 && onGun + blastsOn >= 0.9 * (ends + blasts));
            Clear(all);
            yield return WaitReal(1.0f);
        }

        void Attack(ZObject o)
        {
            if (o == null || o.Destroyed || gun == null || gun.Destroyed) return;
            W.SendWaypoints(me, o, new[] { new ZWaypoint { mode = WpMode.Attack, refId = gun.Id, x = gun.CenterX, y = gun.CenterY, playerGiven = true } });
        }

        void Clear(List<ZObject> list)
        {
            foreach (var x in list) { if (x == null) continue; x.KillAt = 0; if (g.Views.TryGetValue(x.Id, out var v)) { g.Views.Remove(x.Id); if (v) Destroy(v.gameObject); } }
            list.Clear();
        }

        void Finish()
        {
            Report = $"VERDICTS {passes}/{passes + fails} PASS, {frames} frames";
            Log("## " + Report);
            try { System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "proof_log_tower_shots.txt"), sb.ToString()); } catch { }
            Destroy(gameObject);
        }
    }
}
