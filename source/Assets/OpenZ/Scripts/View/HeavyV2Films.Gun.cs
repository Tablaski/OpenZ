#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// The gun in the v2 films (View/HeavyV2Films.cs + .Subject.cs; menus OpenZ/Gun v2/5 .. 15). The gun is a cannon (a building that never
    /// moves): the vehicle jobs (tread, plant, battle, board, fight, snipe, turret, recoil, combat) are the tanks'; the gun has its own, on the
    /// same fixed open spots of L02 (View/HeavyV2Spots.cs: a gun's 2 x 2 tiles fit where a heavy tank's do; every slot asked again with the sim's
    /// own tests), the same take machinery (1920 x 1080, 30 fps, the game's sound, the RTS camera at the game's play zoom, frame sheets, crops)
    /// and its own take names (gn2_*) and proof folder (v2_units/gun/proof/):
    ///  "gun_side" (lessons 5 / 6, item 140): a red v2 and a red v1 side by side fire at an unkillable blue jeep and at an unkillable blue grunt
    ///     (no weapons), switched every 6 s so both turrets turn about 150 deg each time; every shot that finds a gun at rest is measured on the
    ///     drawn model for 1.2 s (the turret's slide and rock against its own design, the base: never a millimetre or a hundredth of a degree);
    ///     the turrets' headings every frame (flutters, jumps); CSV gn2_gun_side_frames_L02.csv;
    ///  "gun_close": the same at 32 m on the v2 (the turret and its kick close at RTS distance);
    ///  "gun_board" / "gun_board_close": a red grunt mans an empty (neutral) v2: he walks in behind it and sits on its chair, the red block and
    ///     the tag stripes turn from grey to red;
    ///  "gun_fight": one red v2 fights blue robots and a light tank to its death (the chair breaks off, the turret is tossed, the base breaks
    ///     apart 1 in 2 by the game's own seed, the wreck stays);
    ///  "gun_turret" (lesson 5): a red v2 that cannot be destroyed switches between five blue grunts round it at 6 to 13 m every 1.5 s, then
    ///     re-obtains them one by one; once with the gun's world-heading slew off (UnitAim.CombatGun: the CONTROL, k81b's steady turret alone),
    ///     then on; then v1 with it on.
    /// </summary>
    public partial class HeavyV2Films
    {
        static readonly FilmSubject GunSubject = new FilmSubject
        {
            Type = OzGun.Type, Label = "gun", Prefix = "gn2", ProofDir = "work/staging/v1_2_20261022/v2_units/gun/proof", BuildMenu = "menu OpenZ/Gun v2/1",
            Yaw = "CannonTurretYaw", Approach = "CannonGunnerSeat", ShotKey = "gunshot", Tracks = new string[0],
            PrefabV2 = () => OzGun.Models.PrefabV2, Version = () => OzGun.Version, Pin = (o, v) => OzGun.Models.Pin(o, v),
        };

        readonly List<GunWatch> gunWatch = new List<GunWatch>();

        /// <summary>Every frame of a take (OnFrame, after every LateUpdate): the watched guns.</summary>
        void SampleGun(Texture2D tex)
        {
            foreach (var w in gunWatch) w.Sample(TakeT);
        }

        bool GunJob() { if (S.Type == OzGun.Type) return true; Finish("the 'gun_*' takes are the gun's (menus OpenZ/Gun v2/5 .. 15)"); return false; }
        ZObject GunUnit(int tx, int ty, Team t, int model, string tag)
        {
            var o = W.CreateObject("cannon", OzGun.Type, tx * 16, ty * 16, t);
            S.Pin(o, model); g.SpawnUnitView(o); cast.Add((o, tag));
            Note($"{tag}: {t} gun drawn as v{model}, id {o.Id}, tile ({tx},{ty}){(W.SnipeSafeSpot(o) ? ", ON A BRIDGE / DOOR SPOT" : "")}");
            return o;
        }
        ZObject Target(string what, int tx, int ty)
        {
            ZObject o = what == "jeep" ? Vehicle("jeep", tx, ty, Team.Blue, "blue jeep (a target)") : Squad("grunt", tx, ty, Team.Blue, 1, "blue grunt (a target)");
            foreach (var m in Members(o)) { m.MaxHealth = 100000000; m.SetHealth(m.MaxHealth); W.UpdateObjectHealth(m); m.Damage = 0; m.AttackRadius = 0; m.MoveSpeed = 0; }
            Note($"staged: the {what} cannot be destroyed, has no weapon and does not move (a target only)");
            return o;
        }

        /// <summary>"gun_side" / "gun_close": v1 and v2 side by side, the turret's turn and kick and the barrel at each shot, the base still.</summary>
        IEnumerator GunSideTake(bool close)
        {
            if (!GunJob()) yield break;
            if (!Spot("v2", out int ax2, out int ay2, "recoil") || !Spot("v1", out int ax1, out int ay1, "recoil") || !Spot("jeep", out int jx, out int jy, "recoil")) yield break;
            RecoilTargetInReach(ax2, ay2, ax1, ay1, ref jx, ref jy);                                   // k84b: the target within the gun's reach (128 px) from both guns
            var v2 = GunUnit(ax2, ay2, Team.Red, 2, "red v2"); var v1 = GunUnit(ax1, ay1, Team.Red, 1, "red v1");
            Unhurt(v2); Unhurt(v1);
            if (!CheckHeavy(v2, "red v2 (gun)", false) || !CheckHeavy(v1, "red v1 (gun)", false)) yield break;
            CombatStageK87b(v2, "gun test");                                                           // k87b: the level's own enemies near it unarmed
            var jeep = Target("jeep", jx, jy);
            int sx = (ax1 + ax2) / 2 + 1, sy = ay2 + 5;
            if (!OzCheatSpawner.FreeTile(W, sx, sy, 1, true, out int gx, out int gy)) { gx = sx; gy = sy; }
            var grunt = Target("grunt", gx, gy);
            Note($"the two targets: the jeep at tile ({jx},{jy}), the grunt at ({gx},{gy}) on the other side: each switch turns both turrets about 150 deg");
            recWatch.Add(new RecWatch { o = v2, tag = "v2", model = 2 }); recWatch.Add(new RecWatch { o = v1, tag = "v1", model = 1 });
            TrackYaw(v2, "v2"); TrackYaw(v1, "v1");
            hero = v2; follow.Add(v2); if (!close) follow.Add(v1);
            if (close) { h0 = CloseHeight; Note($"the camera at {h0:0} m on v2 (the turret and its kick close at RTS distance)"); }
            yield return Hold(2.5f);
            gunWatch.Add(new GunWatch("v2", View(v2), 2)); gunWatch.Add(new GunWatch("v1", View(v1), 1));
            foreach (var w in gunWatch) Fact($"{w.Tag}: turret, barrel and plinth nodes found: {(w.Ok ? "PASS" : "FAIL")}");
            yield return StartTake();
            yield return TakeWait(1.0f);
            var targets = new[] { jeep, grunt };
            int switches = close ? 3 : 5;
            for (int k = 0; k < switches && !done; k++)
            {
                var tg = targets[k % 2];
                yawPhase = "target " + (k % 2 == 0 ? "jeep" : "grunt");
                foreach (var o in new[] { v2, v1 }) o.Engage(tg);
                Note($"both guns engage the {(k % 2 == 0 ? "jeep" : "grunt")} (ZObject.Engage, as the sim's own pick)");
                yield return TakeWait(6f);
            }
            yawPhase = "-";
            yield return TakeWait(1.0f);
            StopTake(); WriteCrops();
            var sb = new StringBuilder("unit,window,frame,t,shot,head_slide_m,head_rock_deg,hull_rock_deg,hull_slide_m,spring_head_m,spring_head_deg,spring_hull_deg,spring_hull_m\n");
            foreach (var w in recWatch) foreach (var c in w.done) foreach (var r in c.rows) sb.Append(r).Append('\n');
            string csv = Path.Combine(Dir, S.Prefix + "_" + job + "_recoil_L02.csv"); File.WriteAllText(csv, sb.ToString());
            var sb2 = new StringBuilder(GunWatch.CsvHead + "\n");
            foreach (var w in gunWatch) foreach (var r in w.Rows) sb2.Append(r).Append('\n');
            string csv2 = Path.Combine(Dir, S.Prefix + "_" + job + "_frames_L02.csv"); File.WriteAllText(csv2, sb2.ToString());
            Fact($"the turret and the base every frame round each shot (measured on the drawn model; spring_* = what HeavyRecoil drew): {csv}; every frame of the take: {csv2}");
            foreach (var w in recWatch)
            {
                if (w.done.Count == 0) { Fact($"{w.tag}: NO shot found the gun at rest: FAIL"); continue; }
                for (int i = 0; i < w.done.Count; i++) Fact(GunRecVerdict(w, w.done[i], i + 1));
            }
            foreach (var w in gunWatch)
            {
                Fact($"base {w.BaseLine()}: {(w.BaseStill ? "PASS" : "FAIL")}");
                Fact($"turret {w.TurnLine()}; {w.KickLine()}: {(w.TurnMax > 90f && w.Jumps == 0 ? "PASS" : "FAIL")} (it turned onto each target)");
            }
            YawFacts("gun side by side");
            Fact($"{S.ShotKey} plays in the take {Count(S.ShotKey)} (one per shot; sound log)");
            Fact("pass rule (the lines above + the MP4 + the crops): each switch turns both turrets in one eased move onto the other target (no shake, no jump); on every shot the barrel slides back and climbs a little, the turret (the box, v2's red block and the seated gunner with it) kicks back about 8 cm and tips its front up 2 deg, then eases home in about a quarter second; the plinth and the base never move; v1 and v2 the same; the gunshot sound and its muzzle blast on each shot");
        }

        /// <summary>One shot window of a gun: the turret's slide and rock against the gun's OWN design (View/HeavyRecoil.Gun.cs) and the base:
        /// no rock, no slide in any frame (a building).</summary>
        string GunRecVerdict(RecWatch w, RecWin c, int index)
        {
            int n = c.head.Count; if (n < 2) return $"{w.tag} shot window {index}: too short ({n} frames): FAIL";
            float hp = 0f, hrp = 0f, hull = 0f, slide = 0f, stepHead = 0f; int hpF = 0;
            int first = c.shots.Count > 0 ? c.shots[0] : 0, end = c.shots.Count > 1 ? c.shots[1] : n;
            for (int i = 0; i < n; i++)
            {
                if (i < end && c.head[i] > hp) { hp = c.head[i]; hpF = i; }
                hrp = Mathf.Max(hrp, c.headRock[i]); hull = Mathf.Max(hull, Mathf.Abs(c.hull[i])); slide = Mathf.Max(slide, Mathf.Abs(c.slide[i]));
                if (i > 0) stepHead = Mathf.Max(stepHead, Mathf.Abs(c.head[i] - c.head[i - 1]));
            }
            int homeF = Mathf.Min(n - 1, first + 12);
            float home = Mathf.Abs(c.head[homeF]);
            var v = w.v ? w.v : View(w.o);
            float scale = v && w.yaw && w.yaw.parent ? Mathf.Abs(w.yaw.parent.lossyScale.x) / Mathf.Max(1e-4f, Mathf.Abs(v.transform.lossyScale.x)) : 1f;
            float dsl = HeavyRecoil.DesignHeadSlide(OzGun.Type, w.model) * scale, drk = HeavyRecoil.DesignHeadRock(OzGun.Type);
            bool okHead = hp >= 0.85f * dsl && hp <= 1.25f * dsl && hpF >= 1 && hpF <= 4 && home < 0.2f * dsl && hrp >= 0.72f * drk && hrp <= 1.36f * drk && stepHead <= 1.15f * dsl;
            bool okBase = hull < 0.01f && slide < 0.001f;
            return $"{w.tag} shot window {index} (shots at frames {string.Join("/", c.shots)}): TURRET slide peak {hp:0.000} m at frame {hpF} (+{hpF / (float)Fps:0.000} s), rock peak {hrp:0.00} deg, " +
                   $"{home:0.000} m off rest 0.4 s after the shot, largest frame step {stepHead:0.000} m -> {(okHead ? "PASS" : "FAIL")} (its own design: {0.85f * dsl:0.000}-{1.25f * dsl:0.000} m = 0.85-1.25 x {dsl:0.000} within 1-4 frames, rock {0.72f * drk:0.00}-{1.36f * drk:0.00} deg, home under {0.2f * dsl:0.000} m, no jump); " +
                   $"BASE rock {hull:0.000} deg, slide {slide * 1000f:0.0} mm -> {(okBase ? "PASS" : "FAIL")} (a building: never, under 0.01 deg and 1 mm)";
        }

        /// <summary>"gun_board" / "gun_board_close": a red grunt mans an empty v2 gun (walks in behind it, sits on its chair; grey -> red).</summary>
        IEnumerator GunBoardTake(bool close)
        {
            if (!GunJob()) yield break;
            if (!Spot("empty v2", out int cx, out int cy, "board") || !Spot("grunt east", out int ex, out int ey, "board")) yield break;
            hero = GunUnit(cx, cy, Team.Neutral, 2, "empty v2");
            if (close) { h0 = CloseHeight; Note($"the camera at {h0:0} m on the empty v2 (close)"); }
            if (!CheckHeavy(hero, "empty v2 (gun)", true)) yield break;
            CombatStageK87b(hero, "gun boarding");
            yield return Hold(2.5f);
            var rb = Squad("grunt", ex, ey, Team.Red, 1, "red grunt (walks to the gun)");
            Fact($"the empty v2's paint before: {Paint(View(hero))} (TeamPaint), owner {hero.Owner}, drivers {hero.Drivers.Count}");
            yield return StartTake();
            yield return TakeWait(1.5f);
            bool sent = Order(rb, Enter(hero));
            Note($"order: the grunt mans the empty v2 ({(sent ? "taken" : "REFUSED")})");
            int ownedAt = -1, fromF = -1, toF = -1, startF = frameNo;
            while (!done && frameNo - startF < 40 * Fps)
            {
                var v = View(hero); int bd = Boarding(v);
                if (bd > 0 && fromF < 0) fromF = frameNo;
                if (bd > 0) toF = frameNo;
                if (ownedAt < 0 && hero.Owner == Team.Red) { ownedAt = frameNo; Note("the v2 is red now (the sim's enter)"); }
                if (ownedAt >= 0 && bd == 0 && frameNo - ownedAt > 2 * Fps) break;
                yield return null;
            }
            yield return TakeWait(2.0f);
            var hv = View(hero); var rig = RigOf(hv); var fig = RigGet<GameObject>(rig, "crew", null); var seat = Node(hv, "CannonGunnerSeat");
            float d = fig && seat ? Vector3.Distance(fig.transform.position, seat.position) : 99f;
            bool ok = hero.Owner == Team.Red && fig && fig.activeInHierarchy && d < 1.2f;
            StopTake(); WriteCrops();
            Fact($"boarding: the view's walk-in from {(fromF >= 0 ? (fromF / (float)Fps).ToString("0.0") : "-")} s to {(toF >= 0 ? (toF / (float)Fps).ToString("0.0") : "-")} s; owner {hero.Owner} from {(ownedAt >= 0 ? (ownedAt / (float)Fps).ToString("0.0") : "never")} s; paint now {Paint(hv)}; the gunner figure {(fig && fig.activeInHierarchy ? "shown" : "NOT shown")} {d:0.00} m from CannonGunnerSeat: {(ok ? "PASS" : "FAIL")}");
            Fact("pass rule (crops + MP4): the robot walks round to the back of the gun and sits behind the box facing the barrel, as on v1 (never inside the box or the base); the box top and the tags' stripes turn from grey to red; he turns with the turret");
        }

        /// <summary>"gun_fight": one red v2 gun (the camera on it) fights blue robots and a light tank until it is destroyed; a fallback kill after
        /// 80 s is written as FORCED.</summary>
        IEnumerator GunFightTake()
        {
            if (!GunJob()) yield break;
            var at = new Dictionary<string, (int x, int y)>();
            foreach (var n in new[] { "red v2", "blue light", "blue toughs", "blue grunts", "blue goal" })
            { if (!Spot(n, out int sx, out int sy, "fight")) yield break; at[n] = (sx, sy); }
            hero = GunUnit(at["red v2"].x, at["red v2"].y, Team.Red, 2, "red v2 (the one filmed)");
            if (!CheckHeavy(hero, "red v2 (gun)", false)) yield break;
            TrackYaw(hero, "red v2"); yawPhase = "fight";
            var e1 = Vehicle("light", at["blue light"].x, at["blue light"].y, Team.Blue, "blue light");
            var e4 = Squad("tough", at["blue toughs"].x, at["blue toughs"].y, Team.Blue, -1, "blue toughs"); var e5 = Squad("grunt", at["blue grunts"].x, at["blue grunts"].y, Team.Blue, -1, "blue grunts");
            int ign0 = OpenZDamageStates.Ignited, chairs0 = GunChairDebris.Broken;
            yield return Hold(2.5f);
            gunWatch.Add(new GunWatch("v2 (fight)", View(hero), 2));
            yield return StartTake();
            yield return TakeWait(1.0f);
            foreach (var o in new[] { e1, e4, e5 }) Order(o, Attack(hero));
            Note("orders: the blue units attack the gun (the game's Attack order); the gun fires back by itself");
            int deadAt = -1, startF = frameNo; bool forced = false; float minHp = 100f;
            while (!done && frameNo - startF < 95 * Fps)
            {
                minHp = Mathf.Min(minHp, 100f * hero.Health / Mathf.Max(1, hero.MaxHealth));
                if (deadAt < 0 && hero.Destroyed) { deadAt = frameNo; Note("the v2 is destroyed"); }
                if (deadAt >= 0 && frameNo - deadAt > 9 * Fps) break;
                if (deadAt < 0 && !forced && frameNo - startF > 80 * Fps) { hero.SetHealth(0); W.UpdateObjectHealth(hero); forced = true; Note("FORCED: the v2 still stood after 80 s; destroyed by the script"); }
                yield return null;
            }
            yield return TakeWait(0.5f);
            StopTake(); WriteCrops();
            var hv = View(hero);
            Fact($"the v2 destroyed at {(deadAt >= 0 ? (deadAt / (float)Fps).ToString("0.0") + " s" : "-")}{(forced ? $" (FORCED; its lowest health before: {minHp:0} %)" : " (in the fight)")}; fires lit {OpenZDamageStates.Ignited - ign0}; chairs broken off {GunChairDebris.Broken - chairs0}; {Census()}");
            foreach (var w in gunWatch) Fact($"while it fought: {w.TurnLine()}; {w.KickLine()}; base {w.BaseLine()}: {(w.BaseStill && w.Flutters == 0 && w.Jumps == 0 ? "PASS" : "FAIL")}");
            YawFacts("fight");
            Fact("pass rule (MP4 + crops): the camera stays on the v2; its turret turns smoothly onto each attacker (no shake, no jump); each shot slides the barrel back and kicks the turret back, the base never moves, the gunshot sound; hits on it and on the blue units; smoke at about half health, fire at a quarter; then the blast, 2-4 s later the turret is tossed whole (no chair to break off), the base breaks apart (1 in 2 by the game's seed) or burns as a wreck");
        }

        /// <summary>"gun_turret" (lesson 5): the turret's heading every frame while it switches between five grunts round it and re-obtains them.</summary>
        IEnumerator GunTurretTake()
        {
            if (!GunJob()) yield break;
            if (!Spot("v2", out int cx, out int cy, "turret") || !Spot("v2", out int bx, out int by, "turret_v1")) yield break;
            var v2 = GunUnit(cx, cy, Team.Red, 2, "red v2 (turret test)"); Unhurt(v2);
            if (!CheckHeavy(v2, "red v2 (turret test)", false)) yield break;
            CombatStageK87b(v2, "turret test");
            var ring2 = GunRing(cx, cy, "v2");
            hero = v2; follow.Add(v2); TrackYaw(v2, "v2");
            int snaps0 = UnitAim.StatSnaps;
            yield return Hold(2.5f);
            yield return StartTake();
            yield return TakeWait(1.0f);
            UnitAim.CombatGun = false; Note("UnitAim.CombatGun OFF (the CONTROL: k81b's steady turret alone, the gun's code before this kit)");
            yawPhase = "v2 switching [gun slew OFF]"; yield return Cycle(v2, ring2, 12f);
            UnitAim.CombatGun = true; Note("UnitAim.CombatGun ON");
            yawPhase = "v2 switching"; yield return Cycle(v2, ring2, 12f);
            yawPhase = "v2 re-obtaining"; yield return Reobtain(v2, ring2);
            yawPhase = "-";
            var v1 = GunUnit(bx, by, Team.Red, 1, "red v1 (turret test)"); Unhurt(v1);
            if (!CheckHeavy(v1, "red v1 (turret test)", false)) { UnitAim.CombatGun = true; yield break; }
            CombatStageK87b(v1, "turret test");
            var ring1 = GunRing(bx, by, "v1");
            hero = v1; follow.Clear(); follow.Add(v1); TrackYaw(v1, "v1");
            yield return TakeWait(2.5f);
            yawPhase = "v1 switching"; yield return Cycle(v1, ring1, 12f);
            yawPhase = "v1 re-obtaining"; yield return Reobtain(v1, ring1);
            yawPhase = "-";
            yield return TakeWait(0.5f);
            UnitAim.CombatGun = true;
            StopTake(); WriteCrops();
            YawFacts("turret test");
            Fact($"snaps at a wait cap during the test: {UnitAim.StatSnaps - snaps0}");
            Fact("pass rule (the counts above + the MP4): every line without [...] PASS (no flutter, no jump); in the MP4 the turret swings onto each grunt in one eased move and holds still on it, never shaking back and forth, also on the grunts 6-7 m away; the [gun slew OFF] control line shows the gun with k81b's steady turret alone");
        }
        /// <summary>Five blue grunts round a gun whose top-left tile is (cx, cy): bearings 0 / 75 / 150 / 220 / 300 deg at 6 / 7 / 9 / 11 / 13 m from
        /// its centre (inside its 16 m reach; staged: no weapon, unkillable until the re-obtain takes them out).</summary>
        List<ZObject> GunRing(int cx, int cy, string tag)
        {
            var list = new List<ZObject>();
            foreach (var (b, d) in new[] { (0f, 6f), (75f, 7f), (150f, 9f), (220f, 11f), (300f, 13f) })
            {
                float r = b * Mathf.Deg2Rad;
                int tx = Mathf.RoundToInt(cx + 1f + d * 0.5f * Mathf.Sin(r) - 0.5f), ty = Mathf.RoundToInt(cy + 1f - d * 0.5f * Mathf.Cos(r) - 0.5f);
                var o = Squad("grunt", tx, ty, Team.Blue, 1, $"{tag} target {b:0} deg / {d:0} m");
                foreach (var m in Members(o)) { m.Damage = 0; m.AttackRadius = 0; m.MaxHealth = 100000000; m.SetHealth(m.MaxHealth); W.UpdateObjectHealth(m); }
                list.Add(o);
            }
            Note($"staged ({tag}): the five grunts have no weapon and cannot be destroyed until the re-obtain phase");
            return list;
        }
    }
}
#endif
