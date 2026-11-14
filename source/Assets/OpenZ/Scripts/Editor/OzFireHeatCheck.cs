#if !OPENZ_HARNESS
// OpenZ (GPL-3.0) - version 1.3: the counted Play check of metal heated by fire (View/OzFireHeat.cs). Editor only, never in a build.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using OpenZ.Sim;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// Menu OpenZ/V1.3/Fire heat. Every row runs in Play, at game speed 1, on the test's own units, on a level that is thrown away after. Each stages its units on open ground near the
    /// camera focus, reads the blocks back from the renderers (r.GetPropertyBlock), and writes one report and its frames under the milestones folder, v1_3/fire_heat.
    ///  * Counted check (L02, then L17): F1 .. F16 below.
    ///  * Frames (L02, then L17): day and night, the normal RTS camera, full screen, and 34 m at the peak.
    ///  * Lava bank (L07): a burning robot walks into lava: every frame the value is the stronger of the two, and lava's band stays positive while lava wins.
    ///  * No trace (a level with no fire, no lava): 1000 frames, nothing runs.
    /// Most rows feed the flame through OzFireHeat.Flame, as OpenZPyroFire.Arrive does. F1 to F3, F5 and F6 use a real Pyro. Burning ground is lit through OpenZPyroFire.IgniteGround by reflection.
    /// </summary>
    public static class OzFireHeatCheck
    {
        const string Tag = "[FireHeat]", Root = "OpenZ/V1.3/Fire heat/";
        const float Tol = 0.02f;
        static readonly int LegHeatId = Shader.PropertyToID("_OZLegHeat"), LegLineId = Shader.PropertyToID("_OZLegLine"), LegTopId = Shader.PropertyToID("_OZLegTop"), TeamColorId = Shader.PropertyToID("_TeamColor");
        static string Dir => OzRelease.Milestones("v1_3/fire_heat");

        sealed class Runner : MonoBehaviour { }
        static Runner runner;
        static Runner R()
        {
            if (!runner) { var go = new GameObject("OzFireHeatCheckRunner") { hideFlags = HideFlags.HideAndDontSave }; runner = go.AddComponent<Runner>(); }
            return runner;
        }

        static int ok, bad; static readonly StringBuilder notes = new StringBuilder();
        static void Begin() { ok = 0; bad = 0; notes.Length = 0; }
        static void Check(bool pass, string what) { if (pass) ok++; else bad++; notes.Append(pass ? "\n  [ok]   " : "\n  [FAIL] ").Append(what); Debug.Log(Tag + (pass ? " ok   " : " FAIL ") + what); }
        static void Info(string what) { notes.Append("\n  [info] ").Append(what); Debug.Log(Tag + " " + what); }
        static void End(string part)
        {
            var text = $"{Tag} {part}: {(bad == 0 ? "PASS" : "FAIL")} {ok}/{ok + bad}{notes}";
            if (bad == 0) Debug.Log(text); else Debug.LogError(text);
            try { Directory.CreateDirectory(Dir); File.WriteAllText(Path.Combine(Dir, part.Replace(' ', '_') + ".txt"), text + "\n"); }
            catch (Exception e) { Debug.LogWarning(Tag + " could not write the report: " + e.Message); }
        }

        // ------------------------------------------------------------------ the stage
        static ZGame g; static ZWorld W; static OpenZPyroFire pf; static OzFireHeat hf; static RtsCamera rts;
        static readonly List<Vector2Int> cells = new List<Vector2Int>(); static int cellN;

        static bool Ready(bool lavaWanted)
        {
            g = ZGame.I; pf = OpenZPyroFire.I; W = g ? g.World : null;
            if (!Application.isPlaying || !g || W == null || !pf) { Debug.LogWarning(Tag + " start a level in Play first"); return false; }
            if (W.Map.HasLava != lavaWanted) { Debug.LogWarning(Tag + (lavaWanted ? " this row needs level 7" : " this row needs a level with no lava")); return false; }
            hf = pf.HeatForCheck; rts = UnityEngine.Object.FindFirstObjectByType<RtsCamera>();
            cells.Clear(); cellN = 0; used.Clear();
            var f = rts ? rts.focus : Vector3.zero; int fx = Mathf.RoundToInt(f.x / 2f), fy = Mathf.RoundToInt(-f.z / 2f);
            for (int y = 0; y < W.Map.height; y += 2) for (int x = 0; x < W.Map.width; x += 2) cells.Add(new Vector2Int(x, y));   // RUN7: every second tile of the map, nearest first; a used cell keeps 8 tiles to itself (Cell)
            cells.Sort((a, b) => ((a.x - fx) * (a.x - fx) + (a.y - fy) * (a.y - fy)).CompareTo((b.x - fx) * (b.x - fx) + (b.y - fy) * (b.y - fy)));
            return true;
        }

        static bool OpenAt(int tx, int ty, int r)
        {
            for (int y = ty - r; y <= ty + r; y++)
                for (int x = tx - r; x <= tx + r; x++)
                {
                    if (!W.Path.TilePassable(x, y, false) || W.Map.LavaAt(x, y)) return false;
                    var c = W.Map.ClassAt(x, y); if (c != ZClass.Ground && c != ZClass.Road) return false;
                }
            return true;
        }

        /// <summary>RUN7: 9 x 9 tiles of open ground: level 2 has no 15 x 15 piece at all (0 of its cells), so every row failed there with "no open ground".</summary>
        const int CellR = 4;
        static readonly List<Vector2Int> used = new List<Vector2Int>();

        /// <summary>The next open piece of ground: 9 x 9 tiles, one for each row, 8 tiles from any other this row set used, so one row's fire never touches another's. The camera goes with it.</summary>
        static bool Cell(out int tx, out int ty)
        {
            for (; cellN < cells.Count; cellN++)
            {
                var c = cells[cellN]; if (!OpenAt(c.x, c.y, CellR)) continue;
                bool near = false; foreach (var u in used) if (Mathf.Abs(u.x - c.x) < 8 && Mathf.Abs(u.y - c.y) < 8) { near = true; break; }
                if (near) continue;
                tx = c.x; ty = c.y; cellN++; used.Add(c);
                if (rts) { rts.focus = ZGame.PxToWorld(tx * 16 + 8, ty * 16 + 8); rts.Place(); }
                return true;
            }
            tx = ty = 0; Check(false, "no open ground left on this level"); return false;
        }

        static ZObject Unit(string type, int tx, int ty, Team team = Team.Blue)
        {
            var o = OzV11Kit.Place(W, type, tx * 16 + 8, ty * 16 + 8, team, 1);
            if (o == null) return null;
            o.MaxHealth = o.Health = 10000000; o.AttackRadius = 0; o.Damage = 0;
            g.SpawnUnitView(o);
            return o;
        }

        static ZObject Pyro(int tx, int ty, ZObject target, Team team = Team.Red)
        {
            var o = OzV11Kit.Place(W, "pyro", tx * 16 + 8, ty * 16 + 8, team, 1);
            o.MaxHealth = o.Health = 10000000; g.SpawnUnitView(o);
            OzV11Kit.PlayerAttack(W, o, target);
            return o;
        }

        /// <summary>This Pyro stops for good: an idle one in reach of an enemy would start again by itself.</summary>
        static void Halt(ZObject o) { o.AttackRadius = 0; o.Damage = 0; OzV11Kit.PlayerMove(W, o, o.CenterX - 48, o.CenterY, false); }
        static UnitView View(ZObject o) => o != null && g.Views.TryGetValue(o.Id, out var v) ? v : null;
        static void Kill(ZObject o) { o.Health = 1; o.DamageHealth(1); W.UpdateObjectHealth(o); }

        static void Ignite(Vector3 at, float radius)
        {
            at.y = g.GroundY(at.x, at.z);
            typeof(OpenZPyroFire).GetMethod("IgniteGround", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(pf, new object[] { at, radius, Time.time });
        }

        /// <summary>Flame on these units for this long, as Arrive gives it.</summary>
        static IEnumerator Flames(float seconds, params UnitView[] vs)
        {
            float end = Time.time + seconds;
            while (Time.time < end) { foreach (var v in vs) if (v) hf.Flame(v, Time.time); yield return null; }
        }

        // ------------------------------------------------------------------ reading the blocks back
        struct Hot { public float v, line, top; public int blocks; }
        static MaterialPropertyBlock rb;

        static void Look(Renderer r, ref Hot h)
        {
            rb ??= new MaterialPropertyBlock();
            r.GetPropertyBlock(rb); Take(ref h);
            int n = r.sharedMaterials.Length;
            for (int s = 0; s < n; s++) { r.GetPropertyBlock(rb, s); Take(ref h); }
        }

        static void Take(ref Hot h)
        {
            if (rb.isEmpty || !rb.HasFloat(LegHeatId)) return;
            h.blocks++; float x = rb.GetFloat(LegHeatId);
            if (x >= h.v) { h.v = x; h.line = rb.GetFloat(LegLineId); h.top = rb.GetFloat(LegTopId); }
        }

        static Hot Read(UnitView v)
        {
            var h = new Hot(); if (!v) return h;
            var paint = v.GetComponent<TeamPaint>();
            foreach (var r in v.GetComponentsInChildren<Renderer>(true)) if (r.GetComponentInParent<TeamPaint>(true) == paint) Look(r, ref h);
            return h;
        }

        /// <summary>The figures riding on it: anything under it with a paint of its own.</summary>
        static float Riders(UnitView v)
        {
            var h = new Hot(); var paint = v.GetComponent<TeamPaint>();
            foreach (var r in v.GetComponentsInChildren<Renderer>(true)) if (r.GetComponentInParent<TeamPaint>(true) != paint) Look(r, ref h);
            return h.v;
        }

        /// <summary>Every renderer in the scene with heat in a block that no held unit lists.</summary>
        static int Leaks(out string first)
        {
            int n = 0; first = "";
            foreach (var r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var h = new Hot(); Look(r, ref h);
                if (h.v <= 0f || hf.Lists(r)) continue;
                if (n++ == 0) first = r.name;
            }
            return n;
        }

        static float RobotHeight(UnitView v)
        {
            float top = float.NegativeInfinity;
            foreach (var s in v.GetComponentsInChildren<SkinnedMeshRenderer>()) if (s.enabled) top = Mathf.Max(top, s.bounds.max.y);
            return Mathf.Clamp(top - v.transform.position.y, 1.2f, 4.5f);
        }

        static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }

        // ------------------------------------------------------------------ the counted check
        [MenuItem(Root + "Counted check (Play, L02 then L17)")]
        static void Counted()
        {
            if (!Ready(false)) return;
            R().StartCoroutine(CountedRun());
        }

        static IEnumerator CountedRun()
        {
            float speed = g.gameSpeed; g.gameSpeed = 1f;
            Begin(); Info($"level {g.level}, game speed 1, peaks robot {OzFireHeat.PeakRobot} machine {OzFireHeat.PeakMachine}");
            yield return F1();
            yield return RealBurn("F2 tank", "light", OzFireHeat.PeakMachine, 0.60f, true);
            yield return RealBurn("F3 gatling", "gatling", OzFireHeat.PeakMachine, 0.55f, false);
            yield return F4();
            yield return F5();
            yield return F6();
            yield return F7();
            yield return F8();
            yield return F9();
            yield return F10();
            yield return F11();
            yield return F12();
            yield return F13();
            yield return F14();
            yield return F15();
            yield return F16();
            g.gameSpeed = speed;
            End("counted " + g.level);
        }

        static IEnumerator F1()
        {
            if (!Cell(out int tx, out int ty)) yield break;
            var t = Unit("grunt", tx, ty); var v = View(t); var py = Pyro(tx - 6, ty, t);
            float start = Time.time, t0 = -1f, early = 0f, firstAt = -1f, peakAt = -1f, H = RobotHeight(v); var last = new Hot();
            while (Time.time - start < 16f)
            {
                yield return null;
                if (t0 < 0f) { if (hf.Probe(v, out _, out _, out _)) t0 = Time.time; else continue; }
                float e = Time.time - t0; var r = Read(v);
                if (e <= 1.1f) early = Mathf.Max(early, r.v);
                if (r.v > 0f && firstAt < 0f) firstAt = e;
                if (r.v >= OzFireHeat.PeakRobot - 0.01f) { peakAt = e; last = r; break; }
            }
            Halt(py);
            Check(t0 >= 0f, "F1 grunt: the Pyro's flame arrived");
            Check(early == 0f, "F1 grunt: nothing shows for the first 1.1 s: " + early.ToString("F3"));
            Check(firstAt > 0f && firstAt <= 1.7f, "F1 grunt: first heat at " + firstAt.ToString("F2") + " s (by 1.7)");
            Check(peakAt > 0f && peakAt <= 4f, "F1 grunt: " + OzFireHeat.PeakRobot + " at " + peakAt.ToString("F2") + " s (2.8 with no miss; three in ten miss, the patch under him holds it once its flames are up)");
            Check(Mathf.Abs(last.top + OzFireHeat.RobotBand * H) <= Tol, "F1 grunt: band " + last.top.ToString("F2") + " m, wanted -" + (OzFireHeat.RobotBand * H).ToString("F2") + " (height " + H.ToString("F2") + ")");
            Check(v && Mathf.Abs(last.line - v.transform.position.y) <= Tol, "F1 grunt: line on the feet: " + last.line.ToString("F2"));
        }

        static IEnumerator RealBurn(string row, string type, float peak, float top, bool tank)
        {
            if (!Cell(out int tx, out int ty)) yield break;
            var t = Unit(type, tx, ty); var v = View(t); var py = Pyro(tx - 6, ty, t);
            float start = Time.time, peakAt = -1f; var last = new Hot();
            while (Time.time - start < 16f) { yield return null; var r = Read(v); if (r.v >= peak - 0.001f) { peakAt = Time.time - start; last = r; break; } }
            Halt(py);
            Check(peakAt > 0f, row + ": " + peak + " reached at " + peakAt.ToString("F2") + " s from the order");
            Check(Mathf.Abs(last.top + top) <= Tol, row + ": band " + last.top.ToString("F2") + ", wanted -" + top);
            var list = new List<Renderer>(); hf.Parts(v, list); var names = new StringBuilder(); bool same = true, turret = false;
            foreach (var r in list)
            {
                names.Append(r.name).Append(' '); var h = new Hot(); Look(r, ref h);
                if (Mathf.Abs(h.v - last.v) > 0.005f) same = false;
                if (r.name.IndexOf("turret", StringComparison.OrdinalIgnoreCase) >= 0) turret = true;
            }
            Check(list.Count > 0 && same, row + ": every listed part holds the value: " + list.Count + " parts");
            Info(row + " listed: " + names);
            if (tank) Check(!turret, row + ": no turret part listed");
            else Check(Riders(v) == 0f, row + ": the gunner's blocks hold no heat");
        }

        static readonly (string tile, float band)[] Kinds =
        {
            ("jeep", 0.55f), ("light@v1", 0.60f), ("light@v2", 0.60f), ("medium@v1", 1.10f), ("medium@v2", 1.10f), ("mtank", 0.60f), ("heavy@v1", 0.65f), ("heavy@v2", 0.65f),
            ("missile_launcher@v1", 0.70f), ("missile_launcher@v2", 0.70f), ("apc", 0.70f), ("crane", 0.70f), ("gun", 0.40f), ("howitzer", 0.45f), ("missile_cannon", 0.60f),
        };

        static IEnumerator F4()
        {
            int not0 = OzFireHeat.NotTeamLit, over0 = OzFireHeat.RendererOverflow; var vs = new List<UnitView>(); var bands = new List<float>(); var tiles = new List<string>();
            int k = 0;
            for (int c = 0; c < 2 && k < Kinds.Length; c++)
            {
                if (!Cell(out int tx, out int ty)) yield break;
                for (int i = 0; i < 8 && k < Kinds.Length; i++, k++)
                {
                    var (tile, band) = Kinds[k]; int x = tx - 6 + (i % 4) * 4, y = ty - 2 + (i / 4) * 5;
                    var o = tile == "gun" || tile == "howitzer" || tile == "missile_cannon" ? Unit(tile, x, y) : OzVehicleScaleTest.CheckSpawn(g, tile, Team.Blue, x, y);
                    if (o == null) { Check(false, "F4 " + tile + ": no room to place it"); continue; }
                    o.AttackRadius = 0; o.Damage = 0;
                    vs.Add(View(o)); bands.Add(band); tiles.Add(tile);
                }
            }
            yield return Flames(3.1f, vs.ToArray());
            for (int i = 0; i < vs.Count; i++)
            {
                var r = Read(vs[i]); hf.Probe(vs[i], out _, out _, out int parts);
                Check(Mathf.Abs(r.v - OzFireHeat.PeakMachine) <= 0.01f && Mathf.Abs(r.top + bands[i]) <= Tol && parts > 0, "F4 " + tiles[i] + ": value " + r.v.ToString("F2") + ", band " + r.top.ToString("F2") + " (wanted -" + bands[i] + "), " + parts + " parts");
            }
            Info("F4 low parts off TeamLit " + (OzFireHeat.NotTeamLit - not0) + ", renderers past the limit " + (OzFireHeat.RendererOverflow - over0));
            Check(OzFireHeat.RendererOverflow == over0, "F4 no unit has more low renderers than " + OzFireHeat.MaxParts);
            UnitView shuttle = null;
            foreach (var kv in g.Views) if (kv.Value && kv.Value.Obj != null && kv.Value.Obj.IsShuttle && !kv.Value.Obj.ShuttleAirborne) { shuttle = kv.Value; break; }
            if (!shuttle) { Info("F4 landed shuttle: none on this level"); yield break; }
            yield return Flames(3.1f, shuttle);
            var s = Read(shuttle); Check(Mathf.Abs(s.v - OzFireHeat.PeakMachine) <= 0.01f, "F4 landed shuttle: value " + s.v.ToString("F2"));
        }

        /// <summary>Two sides of one lick. A real Pyro's 0.4 s with the ground fire off: the flame alone shows nothing. The patch a lick lights, at his feet as Arrive lights it and never fed again:
        /// he stands in fire, which heats as any burning ground, then it cools. Lit by hand, as a real lick of two shots misses both about one time in ten.</summary>
        static IEnumerator F5()
        {
            if (!Cell(out int tx, out int ty)) yield break;
            var t = Unit("grunt", tx, ty); var v = View(t); int shots0 = pf.Shots; bool groundOn = pf.groundFire; pf.groundFire = false; var py = Pyro(tx - 5, ty, t);
            float start = Time.time; while (pf.Shots == shots0 && Time.time - start < 8f) yield return null;
            yield return new WaitForSeconds(0.4f);
            Halt(py);
            float end = Time.time + 6.5f, most = 0f; int parts = 0;
            while (Time.time < end) { yield return null; most = Mathf.Max(most, Read(v).v); if (hf.Probe(v, out _, out _, out int n)) parts = Mathf.Max(parts, n); }
            pf.groundFire = groundOn;
            Check(most == 0f && parts == 0, "F5 lick: 0.4 s of flame, no ground lit: value " + most.ToString("F3") + " through the whole sticky fire, nothing bound");

            if (!Cell(out tx, out ty)) yield break;
            t = Unit("grunt", tx, ty); v = View(t);
            yield return Frames(3);
            Ignite(v.transform.position, 1.3f); yield return Flames(0.4f, v);
            start = Time.time; float peakAt = -1f, fall = -1f;
            while (Time.time - start < 9.5f && fall < 0f)
            {
                yield return null;
                float x = Read(v).v;
                if (peakAt < 0f && x >= OzFireHeat.PeakRobot - 0.01f) peakAt = Time.time - start;
                if (peakAt > 0f && x < OzFireHeat.PeakRobot - 0.01f) fall = Time.time - start;
            }
            Check(peakAt > 0f && peakAt <= 4f, "F5 stays: he stays on the patch of one lick, never fed: " + OzFireHeat.PeakRobot + " at " + peakAt.ToString("F2") + " s after the lick");
            Check(fall > peakAt, "F5 stays: it falls when that patch goes out, at " + fall.ToString("F2") + " s");
        }

        static IEnumerator F6()
        {
            if (!Cell(out int tx, out int ty)) yield break;
            var t = Unit("grunt", tx, ty); var v = View(t); var py = Pyro(tx - 6, ty, t);
            float start = Time.time; while (Read(v).v < OzFireHeat.PeakRobot - 0.01f && Time.time - start < 12f) yield return null;
            yield return new WaitForSeconds(2f);
            Halt(py); float stop = Time.time, fall = -1f, zero = -1f;
            while (Time.time - stop < 22f)
            {
                yield return null;
                if (!hf.Probe(v, out float h, out _, out _)) { zero = Time.time; break; }
                if (fall < 0f && h < 0.995f) fall = Time.time;
            }
            Check(fall > 0f && fall - stop >= 3.5f && fall - stop <= 8.5f, "F6 cool: holds " + (fall - stop).ToString("F1") + " s after the flame: the later of sticky fire's end (5 s) and the patch's end");
            Check(zero > 0f && zero - fall >= 7.5f && zero - fall <= 8.5f, "F6 cool: then down in " + (zero - fall).ToString("F1") + " s (8 wanted)");
            Check(Read(v).v == 0f, "F6 cool: all 0 and the entry freed");
        }

        static IEnumerator F7()
        {
            if (!Cell(out int tx, out int ty)) yield break;
            var at = ZGame.PxToWorld(tx * 16 + 8, ty * 16 + 8);
            Ignite(at, PatchR);
            var t = Unit("grunt", tx - 5, ty); var v = View(t); int hp = t.Health;
            OzV11Kit.PlayerMove(W, t, (tx + 6) * 16 + 8, ty * 16 + 8, false);
            float start = Time.time, inAt = -1f, heatAt = -1f, most = 0f, end = -1f;
            while (Time.time - start < 16f)
            {
                yield return null;
                bool held = hf.Probe(v, out float h, out _, out _);
                if (held && inAt < 0f) inAt = Time.time;
                if (h > 0f && heatAt < 0f) heatAt = Time.time;
                most = Mathf.Max(most, h); if (heatAt > 0f && h < most) { end = Time.time; if (Time.time - inAt > 7f) break; }
            }
            Check(inAt > 0f, "F7 ground: on the burning ground at " + (inAt - start).ToString("F1") + " s");
            Check(heatAt > 0f && heatAt - inAt >= 1.1f, "F7 ground: heat " + (heatAt - inAt).ToString("F1") + " s after stepping in, peak " + most.ToString("F2"));
            Check(end > 0f, "F7 ground: heat falls after he leaves");
            Check(t.Health == hp, "F7 ground: drawn fire takes no health");
        }
        const float PatchR = 4f;

        static IEnumerator F8()
        {
            int b0 = OzFireHeat.Binds, n0 = hf.Count; var vs = new List<UnitView>(); bool tower = false, fort = false;
            foreach (var kv in g.Views)
            {
                var v = kv.Value; if (!v || v.Obj == null) continue;
                if (!tower && v.Obj.IsTowerGun) { tower = true; vs.Add(v); }
                if (!fort && v.Obj.IsFortDefender) { fort = true; vs.Add(v); }
            }
            Info("F8 on this level: tower gun " + tower + ", fort defender " + fort);
            foreach (var o in W.Objects) if (o.Kind == ZKind.Building) { Check(!OzFireHeat.Wanted(o), "F8 a building is refused"); break; }
            if (!Cell(out int tx, out int ty)) yield break;
            var dead = Unit("light", tx, ty); var dv = View(dead); dv.Die(true, false, dv.transform.position); Kill(dead);
            yield return Frames(2);
            vs.Add(dv);
            yield return Flames(2.2f, vs.ToArray());
            bool none = true; foreach (var v in vs) if (v && (hf.Probe(v, out _, out _, out _) || Read(v).v > 0f)) none = false;
            Check(none && OzFireHeat.Binds == b0, "F8 tower gun, fort defender, wreck under flame: nothing held, no float, no bind");
            foreach (var kv in g.Views)
            {
                var s = kv.Value; if (!s || s.Obj == null || !s.Obj.IsShuttle || s.Obj.ShuttleAirborne) continue;
                double lift = s.Obj.ShuttleLift; s.Obj.ShuttleLift = 1; hf.Flame(s, Time.time); bool held = hf.Probe(s, out _, out _, out _); s.Obj.ShuttleLift = lift;
                Check(!held, "F8 the shuttle in the air is refused"); break;
            }
        }

        /// <summary>A water tile a robot can stand on, with open dry ground beside it.</summary>
        static bool Shore(out int wx, out int wy, out int dx, out int dy)
        {
            for (int y = 2; y < W.Map.height - 2; y++)
                for (int x = 2; x < W.Map.width - 2; x++)
                {
                    if (W.Map.ClassAt(x, y) != ZClass.Water || !W.Path.TilePassable(x, y, true)) continue;
                    for (int k = 2; k <= 5; k++)
                        foreach (var d in new[] { new Vector2Int(k, 0), new Vector2Int(-k, 0), new Vector2Int(0, k), new Vector2Int(0, -k) })
                            if (OpenAt(x + d.x, y + d.y, 1)) { wx = x; wy = y; dx = x + d.x; dy = y + d.y; return true; }
                }
            wx = wy = dx = dy = 0; return false;
        }

        static IEnumerator F9()
        {
            if (!Shore(out int wx, out int wy, out int dx, out int dy)) { Info("F9 water: no wading water on this level"); yield break; }
            if (rts) { rts.focus = ZGame.PxToWorld(wx * 16 + 8, wy * 16 + 8); rts.Place(); }
            var t = Unit("grunt", wx, wy); var v = View(t);
            yield return Frames(30);
            Check(OpenZPyroFire.InWater(v.transform.position), "F9 water: he stands in water");
            Ignite(ZGame.PxToWorld(dx * 16 + 8, dy * 16 + 8), PatchR);
            float end = Time.time + 4f, most = 0f;
            while (Time.time < end) { hf.Flame(v, Time.time); yield return null; most = Mathf.Max(most, Read(v).v); }
            Check(most == 0f && !(hf.Probe(v, out float h0, out _, out _) && h0 > 0f), "F9 water: a burning patch beside him and a flame on him: value " + most.ToString("F3"));
            var d2 = Unit("grunt", dx, dy); var w = View(d2);
            yield return Flames(3.2f, w);
            Check(Read(w).v > 0.8f, "F9 water: the dry robot is hot: " + Read(w).v.ToString("F2"));
            OzV11Kit.PlayerMove(W, d2, wx * 16 + 8, wy * 16 + 8, false);
            float start = Time.time, wetAt = -1f, zero = -1f;
            while (Time.time - start < 14f)
            {
                hf.Flame(w, Time.time); yield return null;
                if (wetAt < 0f && OpenZPyroFire.InWater(w.transform.position)) wetAt = Time.time;
                if (wetAt > 0f && Read(w).v == 0f) { zero = Time.time; break; }
            }
            if (wetAt < 0f) Info("F9 water: he never reached the water in 14 s (blocked by the first robot?)");
            else Check(zero > 0f && zero - wetAt <= 1.15f, "F9 water: a hot robot steps in: 0 in " + (zero - wetAt).ToString("F2") + " s (1.1 wanted), under a flame");
        }

        static void Leak(string row) { Check(Leaks(out var first) == 0, "F10 " + row + ": no heat outside the listed units" + (first.Length > 0 ? ": first " + first : "")); }

        static IEnumerator Dead(string row, UnitView v, bool fire, bool missile)
        {
            v.Die(fire, missile, v.transform.position + Vector3.right); Kill(v.Obj);
            yield return Frames(2); Leak(row);
        }

        static IEnumerator F10()
        {
            if (!Cell(out int tx, out int ty)) yield break;
            var mix = RobotDeathRule.Mix; RobotDeathRule.Mix = RobotDeathRule.DeathMix.Zod;                  // no missile, no flame = pop; flame = melt; missile = ragdoll
            UnitView G(int i) => View(Unit("grunt", tx - 6 + i * 2, ty - 5));
            UnitView pop = G(0), head = G(1), melt = G(2), rag = G(3), squash = G(4);
            UnitView M(string type, int i) => View(Unit(type, tx - 6 + i * 4, ty));
            UnitView tank = M("light", 0), gat = M("gatling", 1), mlrs = M("missile_launcher", 2), mech = M("medium", 3), hull = View(Unit("heavy", tx, ty + 5));
            yield return Flames(3.2f, pop, head, melt, rag, squash, tank, gat, mlrs, mech, hull);
            Check(Read(tank).v > 0.8f && Read(pop).v > 0.8f, "F10 all hot before the deaths");
            yield return Dead("pop", pop, false, false);
            typeof(ZObject).GetProperty("HeadshotBy").GetSetMethod(true).Invoke(head.Obj, new object[] { tank.Obj.Id });
            yield return Dead("headshot", head, false, false);
            yield return Dead("melt", melt, true, false);
            yield return Dead("ragdoll", rag, false, true);
            squash.Squash(tank.Obj); Kill(squash.Obj); yield return Frames(2); Leak("squash");
            yield return Dead("tank", tank, true, false);
            yield return Dead("gun", gat, true, false);
            yield return Dead("MLRS", mlrs, true, false);
            // the two copies from a living unit
            var list = new List<Renderer>(); hf.Parts(mech, list); MeshRenderer from = null;
            foreach (var r in list) if (r is MeshRenderer mr) { var h = new Hot(); Look(mr, ref h); if (h.v > 0f) { from = mr; break; } }
            if (!from) Info("F10 mech: no hot plain part to copy");
            else
            {
                var go = new GameObject("OZ_FireHeatCheckCopy"); var to = go.AddComponent<MeshRenderer>();
                typeof(MechGunLossView).GetMethod("CopyLook", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { from, to });
                var c = new Hot(); Look(to, ref c);
                Check(c.v == 0f && c.blocks > 0, "F10 mech gun lost: the copy took the paint and no heat: " + c.v.ToString("F2")); UnityEngine.Object.Destroy(go);
            }
            yield return Flames(0.5f, hull);
            int knocks = RobotPopDeath.KnockOffs; bool part = RobotPopDeath.KnockOff(hull.transform, hull.Obj.Id, hull.transform.position + Vector3.up * 0.4f, hull.transform.position + Vector3.right * 3f, 77u);
            yield return Frames(2);
            if (part && RobotPopDeath.KnockOffs > knocks) Leak("part knocked off a living hot tank"); else Info("F10 no part to knock off this model");
            for (int i = 0; i < 4; i++) { var c = View(Unit("grunt", tx - 6 + i * 2, ty + 3)); c.Die(false, false, c.transform.position + Vector3.right); Kill(c.Obj); yield return Frames(1); }
            yield return Frames(4); Leak("the pool's pieces thrown again");
            yield return new WaitForSeconds(1.3f);
            Leak("1.3 s on, the bodies out");
            Check(Read(melt).v == 0f && Read(rag).v == 0f, "F10 the bodies are out after their second");
            RobotDeathRule.Mix = mix;
        }

        static IEnumerator F11()
        {
            if (!Cell(out int tx, out int ty)) yield break;
            foreach (var team in new[] { Team.Red, Team.Blue })
            {
                var a = Unit("grunt", tx - 2, ty, team); var v = View(a);
                yield return Flames(3.2f, v);
                v.Die(false, false, v.transform.position + Vector3.right); Kill(a); yield return Frames(3);
                var b = Unit("grunt", tx + 2, ty, team); var w = View(b);
                yield return Frames(3);
                var c = Paint(w); Check(Read(w).v == 0f, "F11 pool " + team + ": a new unit starts cold, before its own fire");
                yield return Flames(3.2f, w);
                Check(Read(w).v >= OzFireHeat.PeakRobot - 0.01f && Paint(w) == c, "F11 pool " + team + ": hot in its own turn, team paint as it was");
            }
        }

        static Color Paint(UnitView v)
        {
            rb ??= new MaterialPropertyBlock();
            foreach (var r in v.GetComponentsInChildren<Renderer>()) { r.GetPropertyBlock(rb, 0); if (!rb.isEmpty && rb.HasColor(TeamColorId)) return rb.GetColor(TeamColorId); }
            return Color.clear;
        }

        static IEnumerator F12()
        {
            if (!Cell(out int tx, out int ty)) yield break;
            var t = Unit("light", tx, ty); var v = View(t); var paint = v.GetComponent<TeamPaint>();
            yield return Flames(3.2f, v);
            var a = Read(v); paint.Team = Team.Red; paint.Charred = 0.5f;
            var b = Read(v);
            Check(a.v > 0.8f && Mathf.Abs(a.v - b.v) < 0.001f && Mathf.Abs(a.line - b.line) < 0.001f && Mathf.Abs(a.top - b.top) < 0.001f, "F12 repaint: the floats stay: " + b.v.ToString("F2"));
            float end = Time.time + 9.5f; while (hf.Probe(v, out _, out _, out _) && Time.time < end) yield return null;
            Check(Read(v).v == 0f, "F12 cooled: all 0");
        }

        static IEnumerator F13()
        {
            if (!Cell(out int tx, out int ty)) yield break;
            var tank = View(Unit("light", tx - 3, ty)); var gun = View(Unit("gatling", tx + 3, ty)); var bot = View(Unit("grunt", tx, ty + 4));
            yield return Flames(3.2f, tank, gun, bot);
            Hot t0 = Read(tank), g0 = Read(gun), b0 = Read(bot); int was = OzVehicleScaleTest.Step;
            OzVehicleScaleTest.SetStepForCheck(4);
            yield return Flames(0.6f, tank, gun, bot);
            Hot t1 = Read(tank), g1 = Read(gun), b1 = Read(bot); float k = OzVehicleScaleTest.Of(tank);
            OzVehicleScaleTest.SetStepForCheck(was);
            Check(Mathf.Abs(k - 0.75f) < 0.001f && Mathf.Abs(t1.top - t0.top * 0.75f) <= 0.01f && Mathf.Abs(t1.line - t0.line) <= Tol, "F13 scale: tank band " + t0.top.ToString("F2") + " to " + t1.top.ToString("F2") + " at " + k.ToString("F2"));
            Check(Mathf.Abs(g1.top - g0.top) <= 0.005f && Mathf.Abs(b1.top - b0.top) <= 0.005f, "F13 scale: guns and robots as at 100 %");
            yield return Frames(3);
        }

        static IEnumerator F14()
        {
            if (!Cell(out int tx, out int ty)) yield break;
            var jeep = Unit("jeep", tx + 3, ty, Team.Neutral); var bot = Unit("grunt", tx - 2, ty); var v = View(bot);
            yield return Flames(3.2f, v);
            Check(Read(v).v > 0.8f, "F14 boarding: hot before it");
            OzV11Kit.PlayerEnter(W, bot, jeep);
            float start = Time.time; while (v && v.isActiveAndEnabled && Time.time - start < 20f) { hf.Flame(v, Time.time); yield return null; }
            yield return Frames(3);
            if (v && v.isActiveAndEnabled) { Info("F14 boarding: he did not go in within 20 s"); yield break; }
            Check(Read(v).v == 0f && !hf.Probe(v, out _, out _, out _), "F14 boarding: cold at the hide, entry freed; the same view comes back cold, an exit is not staged here");
        }

        static IEnumerator F15()
        {
            if (!Cell(out int tx, out int ty)) yield break;
            ZObject z = null;
            try { z = Unit(ZEvilZod.Type, tx, ty); } catch (Exception e) { Info("F15 EvilZod: " + e.Message); }
            if (z == null || !View(z)) { Info("F15 EvilZod: not placed on this level"); yield break; }
            var v = View(z); float H = RobotHeight(v);
            yield return Flames(3.2f, v);
            var r = Read(v);
            Check(r.v >= OzFireHeat.PeakRobot - 0.01f && Mathf.Abs(r.top + OzFireHeat.RobotBand * H) <= Tol, "F15 EvilZod: value " + r.v.ToString("F2") + ", band " + r.top.ToString("F2") + ", height " + H.ToString("F2"));
        }

        static IEnumerator F16()
        {
            if (!Cell(out int tx, out int ty)) yield break;
            var vs = new List<UnitView>();
            for (int i = 0; i < 30; i++) vs.Add(View(Unit("grunt", tx - 6 + (i % 6) * 2, ty - 6 + (i / 6) * 2)));
            var step = (Action<float, float>)Delegate.CreateDelegate(typeof(Action<float, float>), pf, typeof(OpenZPyroFire).GetMethod("StepHeat", BindingFlags.NonPublic | BindingFlags.Instance));
            var arr = vs.ToArray(); double worst = 0.0, sum = 0.0;
            for (int f = 0; f < 600; f++)
            {
                if (f < 300) foreach (var v in arr) hf.Flame(v, Time.time);
                long t0 = Stopwatch.GetTimestamp(); step(Time.deltaTime, Time.time);
                double ms = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency; worst = Math.Max(worst, ms); sum += ms;
                yield return null;
            }
            Info("F16 cost, 30 hot units, 600 frames: average " + (sum / 600.0).ToString("F3") + " ms, worst " + worst.ToString("F3") + " ms (the first run sets the limit)");
            foreach (var v in arr) hf.Flame(v, Time.time);
            long m0 = GC.GetTotalMemory(false); for (int i = 0; i < 4000; i++) step(0.0004f, Time.time); long m1 = GC.GetTotalMemory(false);
            var junk = new byte[20000]; long m2 = GC.GetTotalMemory(false);
            Check(m1 - m0 <= 0L, "F16 no garbage: heap growth over 4000 steps with 30 held " + (m1 - m0) + " bytes (a control of 20,000 bytes reads " + (m2 - m1) + ", length " + junk.Length + ")");
            Check(OzFireHeat.Overflow == 0, "F16 no unit past " + OzFireHeat.MaxHot);
        }

        // ------------------------------------------------------------------ frames
        [MenuItem(Root + "Frames (Play, L02 then L17: day, night, RTS camera)")]
        static void FramesRow()
        {
            if (!Ready(false)) return;
            R().StartCoroutine(FramesRun());
        }

        static void Shot(string name)
        {
            Directory.CreateDirectory(Dir);
            ScreenCapture.CaptureScreenshot(Path.Combine(Dir, $"{g.level}_{name}.png"));
            Info("frame " + name);
        }

        static IEnumerator Close(string name)
        {
            float h = rts ? rts.height : 0f;
            if (rts) rts.SetHeightImmediate(34f);
            yield return Frames(3); Shot(name + "_34m"); yield return Frames(3);
            if (rts) rts.SetHeightImmediate(h);
        }

        static IEnumerator ShotSet(string type, string night)
        {
            if (!Cell(out int tx, out int ty)) yield break;
            var t = Unit(type, tx, ty); var py = Pyro(tx - 6, ty, t);
            var v = View(t); float start = Time.time; while (!hf.Probe(v, out _, out _, out _) && Time.time - start < 8f) yield return null;
            start = Time.time; string n = type + "_" + night;
            foreach (var s in new[] { 0.5f, 1.5f, 3f, 5f })
            {
                while (Time.time - start < s) yield return null;
                Shot($"{n}_flame_{s:F1}s");
            }
            yield return Close(n + "_flame");
            Halt(py); start = Time.time;
            foreach (var s in new[] { 1f, 4f, 8f, 10f })
            {
                while (Time.time - start < s) yield return null;
                Shot($"{n}_after_{s:F0}s");
            }
        }

        static IEnumerator WalkSet(string type, string night)
        {
            if (!Cell(out int tx, out int ty)) yield break;
            var at = ZGame.PxToWorld(tx * 16 + 8, ty * 16 + 8);
            Ignite(at, PatchR);
            var t = Unit(type, tx - 5, ty); var v = View(t); OzV11Kit.PlayerMove(W, t, (tx + 6) * 16 + 8, ty * 16 + 8, false);
            float start = Time.time; string n = "walk_" + type + "_" + night; int k = 0;
            while (Time.time - start < 12f && k < 3)
            {
                yield return null; hf.Probe(v, out float h, out _, out _);
                if (k == 0 && Mathf.Abs(v.transform.position.x - at.x) < 3.5f) { Shot(n + "_entering"); k = 1; }
                else if (k == 1 && Mathf.Abs(v.transform.position.x - at.x) < 0.8f) { Shot(n + "_middle"); k = 2; }
                else if (k == 2 && v.transform.position.x - at.x > 4.5f) { Shot(n + "_leaving"); k = 3; }
            }
            yield return new WaitForSeconds(6f); Shot(n + "_plus6s");
        }

        static IEnumerator FramesRun()
        {
            float speed = g.gameSpeed; g.gameSpeed = 1f; Begin();
            var dn = UnityEngine.Object.FindFirstObjectByType<DayNight>(); float tod = dn ? dn.timeOfDay : 0.08f; bool running = dn && dn.running;
            if (dn) dn.running = false;
            foreach (var (night, name) in new[] { (0.08f, "day"), (0.62f, "night") })
            {
                NightLights.JumpTo(night);
                yield return ShotSet("grunt", name);
                yield return ShotSet("light", name);
                yield return ShotSet("gatling", name);
                yield return WalkSet("grunt", name);
                yield return WalkSet("light", name);
                if (Cell(out int tx, out int ty))
                {   // a kneeling or lying sniper: his body stands low in a band in world height
                    var s = Unit("sniper", tx, ty); s.AttackRadius = 200; s.Damage = 1;
                    var far = Unit("grunt", tx + 8, ty); OzV11Kit.PlayerAttack(W, s, far);
                    yield return Flames(3.5f, View(s)); Shot("sniper_low_" + name); yield return Close("sniper_low_" + name);
                }
                if (Shore(out int wx, out int wy, out int dx, out int dy))
                {
                    if (rts) { rts.focus = ZGame.PxToWorld(wx * 16 + 8, wy * 16 + 8); rts.Place(); }
                    var w = Unit("grunt", wx, wy); yield return Frames(30); Shot("wading_" + name);
                    Pyro(dx, dy, w); yield return new WaitForSeconds(4f); Shot("wading_flame_" + name);
                }
            }
            if (dn) { dn.timeOfDay = tod; dn.Apply(tod); dn.running = running; }
            g.gameSpeed = speed;
            End("frames " + g.level);
        }

        // ------------------------------------------------------------------ level 7
        [MenuItem(Root + "Lava bank (Play, L07)")]
        static void LavaRow()
        {
            if (!Ready(true)) return;
            if (!OzLavaWade.I || !ZWorld.LavaDeviation) { Debug.LogWarning(Tag + " OPTIONS: LAVA BURNS ROBOTS must be on"); return; }
            R().StartCoroutine(LavaRun());
        }

        static IEnumerator LavaRun()
        {
            Begin(); var m = W.Map; var wade = OzLavaWade.I;
            int bx = -1, by = -1, best = -1;
            for (int y = 0; y < m.height; y++)
                for (int x = 0; x < m.width; x++)
                {
                    if (!m.LavaAt(x, y)) continue;
                    int r = 0; while (r < 12 && Lake(m, x, y, r + 1)) r++;
                    if (r > best) { best = r; bx = x; by = y; }
                }
            int sx = -1, sy = -1;
            for (int r = 2; r <= 12 && sx < 0; r++)
                for (int y = by - r; y <= by + r && sx < 0; y++)
                    for (int x = bx - r; x <= bx + r; x++)
                        if (Math.Max(Math.Abs(x - bx), Math.Abs(y - by)) == r && !m.LavaAt(x, y) && W.Path.TilePassable(x, y, true) && W.LavaLineClean(x * 16 + 8, y * 16 + 8, bx * 16 + 8, by * 16 + 8)) { sx = x; sy = y; break; }
            if (sx < 0) { Check(false, "no dry spot beside the deepest lava"); End("lava"); yield break; }
            if (rts) { rts.focus = ZGame.PxToWorld(sx * 16 + 8, sy * 16 + 8); rts.Place(); }
            var t = OzV11Kit.Place(W, "grunt", sx * 16 + 8, sy * 16 + 8, g.player, 1); t.MaxHealth = t.Health = 10000000; var v = g.SpawnUnitView(t);
            yield return Flames(3.2f, v);
            Check(Read(v).v >= OzFireHeat.PeakRobot - 0.01f, "the robot on the bank is hot from fire: " + Read(v).v.ToString("F2"));
            OzV11Kit.PlayerMove(W, t, bx * 16 + 8, by * 16 + 8, false);
            int frames = 0, off = 0, lavaWon = 0, topBad = 0; float start = Time.time, worst = 0f;
            var skin = v.GetComponentInChildren<SkinnedMeshRenderer>();
            while (Time.time - start < 16f)
            {
                yield return new WaitForEndOfFrame();
                hf.Probe(v, out float h, out _, out _);
                float fire = OzFireHeatCurve.Shown(h, OzFireHeat.PeakRobot), lava = OzFireHeatCurve.Smooth(wade.LegHeatOf(v)), want = Mathf.Max(fire, lava);
                var r = new Hot(); Look(skin, ref r); frames++;
                float d = Mathf.Abs(r.v - want); worst = Mathf.Max(worst, d); if (d > 0.012f) off++;
                if (lava > 0f && !OzFireHeatCurve.FireWins(fire, lava)) { lavaWon++; if (r.top <= 0f) topBad++; }
            }
            Check(frames > 200 && off == 0, "the stronger of the two on every frame: " + frames + " frames, " + off + " off, worst " + worst.ToString("F3"));
            Check(lavaWon > 0, "lava won on " + lavaWon + " frames");
            Check(topBad == 0, "the band stays positive while lava wins: " + topBad + " frames wrong");
            Directory.CreateDirectory(Dir); Shot("lava_wade_day");
            End("lava");
        }

        static bool Lake(ZMapData m, int x, int y, int r)
        {
            for (int d = -r; d <= r; d++) if (!m.LavaAt(x + d, y - r) || !m.LavaAt(x + d, y + r) || !m.LavaAt(x - r, y + d) || !m.LavaAt(x + r, y + d)) return false;
            return true;
        }

        // ------------------------------------------------------------------ no trace
        [MenuItem(Root + "No trace (Play, a level with no fire)")]
        static void NoTrace()
        {
            if (!Ready(false)) return;
            R().StartCoroutine(NoTraceRun());
        }

        static IEnumerator NoTraceRun()
        {
            Begin(); int s0 = OzFireHeat.Steps, b0 = OzFireHeat.Binds, r0 = OzFireHeat.RampPushes, frames = 0, burning = 0;
            for (; frames < 1000; frames++) { burning = Mathf.Max(burning, pf.Patches + hf.Count); yield return null; }
            if (burning > 0) Info("fire was burning on this level during the run: the row says nothing, run it on a quiet level");
            else
            {
                Check(OzFireHeat.Steps == s0, "1000 frames: no step: " + (OzFireHeat.Steps - s0));
                Check(OzFireHeat.Binds == b0 && OzFireHeat.RampPushes == r0, "no bind, no ramp");
            }
            End("no trace " + g.level);
        }
    }
}
#endif
