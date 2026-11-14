#if !OPENZ_HARNESS
// OpenZ (GPL-3.0) - version 1.3, level 7: the counted Play check of a robot dying in lava (View/UnitView.LavaMelt.cs, View/OzLavaWade.cs). Editor only, never in a build.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using OpenZ.Sim;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// Menu OpenZ/V1.3/Lava death, in Play on L07, rule LAVA BURNS ROBOTS on, game speed 1. Report and frames under the milestones folder, v1_3/lava_death.
    /// Two rounds on the deepest lake, the camera on it: first a psycho and a sniper shot dead after 1.5 s in lava (their band still low), then a grunt, a tough
    /// and EvilZod left walking to and fro in it until the lava kills them (their band lit to the chest). Every frame each body's own skin block is read back.
    ///  * D1 each one melts and is gone after the melt's 3.2 s.  * D2 no drop: the band's top on the first melt frame is not under the last one alive.
    ///  * D3 the body's own red never rises during the melt and ends at 0.  * D4 the glow climbs: shot ones start under 60 % of the body, every one is lit to
    ///    90 % of its skins' top by 75 % of the melt.  * D5 3 to 5 small bubbles a melt, counted by the round (its deaths overlap).  * D6 nothing left on the body after it is gone.
    /// Frames at 0.25, 0.5, 1, 2 and 3 s after the first death of each round. Run it at 70 m, then again with the second menu at 35 m.
    /// </summary>
    public static class OzLavaDeathCheck
    {
        const string Tag = "[LavaDeath]", Root = "OpenZ/V1.3/Lava death/";
        static readonly int LegHeatId = Shader.PropertyToID("_OZLegHeat"), LegTopId = Shader.PropertyToID("_OZLegTop"), LegLineId = Shader.PropertyToID("_OZLegLine"), OzHeatId = Shader.PropertyToID("_OZHeat");
        static readonly float[] ShotTimes = { 0.25f, 0.5f, 1f, 2f, 3f };
        static string Dir => OzRelease.Milestones("v1_3/lava_death");

        sealed class Runner : MonoBehaviour { }
        static Runner runner;
        static Runner R()
        {
            if (!runner) { var go = new GameObject("OzLavaDeathCheckRunner") { hideFlags = HideFlags.HideAndDontSave }; runner = go.AddComponent<Runner>(); }
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

        sealed class Case
        {
            public string type; public bool shot; public ZObject o; public UnitView v; public SkinnedMeshRenderer skin; public TeamPaint paint;
            public float inAt = -1f, meltAt = -1f, goneAt = -1f, lastTop = -1f, firstTop = -1f, heat0 = -1f, heatMost = -1f, heatEnd = -1f, startShare = -1f, litAtK = -1f;
            public bool left, toB;
        }

        static ZGame g; static ZWorld W; static RtsCamera rts; static int ax, ay, bx2, by2, sx, sy, roundBubbles;
        static readonly MaterialPropertyBlock blk = new MaterialPropertyBlock();

        [MenuItem(Root + "1 Counted and frames, RTS camera 70 m (Play, L07, about 60 s)")]
        static void Run70() { if (Ready()) R().StartCoroutine(Run(70f)); }

        [MenuItem(Root + "2 Counted and frames, nearer 35 m (Play, L07, about 60 s)")]
        static void Run35() { if (Ready()) R().StartCoroutine(Run(35f)); }

        static bool Ready()
        {
            g = ZGame.I; W = g ? g.World : null;
            if (!Application.isPlaying || !g || W == null || !W.Map.HasLava) { Debug.LogWarning(Tag + " enter Play on L07 first"); return false; }
            if (!ZWorld.LavaDeviation || !OzLavaWade.I || !OzLava.I) { Debug.LogWarning(Tag + " OPTIONS: LAVA BURNS ROBOTS must be on"); return false; }
            rts = UnityEngine.Object.FindFirstObjectByType<RtsCamera>();
            return Spots();
        }

        /// <summary>The deepest lava tile, a second one two tiles from it inside the same lake (the walkers go to and fro, so they are never idle in lava), a dry start beside it.</summary>
        static bool Spots()
        {
            var m = W.Map; int best = -1; ax = ay = -1;
            for (int y = 0; y < m.height; y++)
                for (int x = 0; x < m.width; x++)
                {
                    if (!m.LavaAt(x, y)) continue;
                    int r = 0; while (r < 12 && Lake(m, x, y, r + 1)) r++;
                    if (r > best) { best = r; ax = x; ay = y; }
                }
            if (best < 2) { Debug.LogWarning(Tag + " no lake 5 tiles across"); return false; }
            bx2 = ax + 2; by2 = ay;
            sx = sy = -1;
            for (int r = 2; r <= 14 && sx < 0; r++)
                for (int y = ay - r; y <= ay + r && sx < 0; y++)
                    for (int x = ax - r; x <= ax + r; x++)
                        if (Math.Max(Math.Abs(x - ax), Math.Abs(y - ay)) == r && !m.LavaAt(x, y) && W.Path.TilePassable(x, y, true) && W.LavaLineClean(x * 16 + 8, y * 16 + 8, ax * 16 + 8, ay * 16 + 8)) { sx = x; sy = y; break; }
            if (sx < 0) { Debug.LogWarning(Tag + " no dry spot beside the deepest lava"); return false; }
            return true;
        }

        static bool Lake(ZMapData m, int x, int y, int r)
        {
            for (int d = -r; d <= r; d++) if (!m.LavaAt(x + d, y - r) || !m.LavaAt(x + d, y + r) || !m.LavaAt(x - r, y + d) || !m.LavaAt(x + r, y + d)) return false;
            return true;
        }

        static Case Stage(string type, bool shot, int i)
        {
            var o = OzV11Kit.Place(W, type, sx * 16 + 8 + (i - 1) * 6, sy * 16 + 8, g.player, 1);
            var v = g.SpawnUnitView(o);
            OzV11Kit.PlayerMove(W, o, ax * 16 + 8, ay * 16 + 8, false);
            SkinnedMeshRenderer skin = null; foreach (var s in v.GetComponentsInChildren<SkinnedMeshRenderer>()) if (s.enabled) { skin = s; break; }
            return new Case { type = type, shot = shot, o = o, v = v, skin = skin, paint = v.GetComponentInChildren<TeamPaint>() };
        }

        static void Read(Case c, out float heat, out float top, out float line, out float red)
        {
            heat = top = line = red = 0f;
            if (!c.skin) return;
            c.skin.GetPropertyBlock(blk, 0);
            heat = blk.GetFloat(LegHeatId); top = blk.GetFloat(LegTopId); line = blk.GetFloat(LegLineId); red = blk.GetFloat(OzHeatId);
        }

        static void Shot(string name)
        {
            Directory.CreateDirectory(Dir);
            ScreenCapture.CaptureScreenshot(Path.Combine(Dir, name + ".png"));
            Info("frame " + name);
        }

        static IEnumerator Run(float height)
        {
            Begin(); g.gameSpeed = 1f; g.paused = false;
            float h0 = rts ? rts.height : 70f; var f0 = rts ? rts.focus : Vector3.zero;
            if (rts) { rts.focus = ZGame.PxToWorld(ax * 16 + 8, ay * 16 + 8); rts.Place(); rts.SetHeightImmediate(height); }
            var all = new List<Case>();
            try
            {
                var shot = new List<Case> { Stage("psycho", true, 0), Stage("sniper", true, 2) };
                yield return Round(shot, $"L07_death_shot_{height:F0}m"); all.AddRange(shot);
                Check(roundBubbles >= 3 * shot.Count && roundBubbles <= 5 * shot.Count, $"D5 shot round: {roundBubbles} small bubbles for {shot.Count} melts (3 to 5 each; they need the spot on screen and the melt budget)");
                Case evil = null; try { evil = Stage(ZEvilZod.Type, false, 2); } catch (Exception e) { Info("EvilZod could not be placed: " + e.Message); }
                var walk = new List<Case> { Stage("grunt", false, 0), Stage("tough", false, 1) }; if (evil != null) walk.Add(evil);
                yield return Round(walk, $"L07_death_lava_{height:F0}m"); all.AddRange(walk);
                Check(roundBubbles >= 3 * walk.Count && roundBubbles <= 5 * walk.Count, $"D5 lava round: {roundBubbles} small bubbles for {walk.Count} melts (3 to 5 each)");
            }
            finally { if (rts) { rts.focus = f0; rts.SetHeightImmediate(h0); } }
            foreach (var c in all)
            {
                string n = c.type + (c.shot ? " (shot dead after 1.5 s in lava)" : " (the lava killed it)");
                float melt = c.goneAt - c.meltAt;
                Check(c.meltAt >= 0f && c.goneAt >= 0f && melt >= UnitView.LavaMeltSeconds - 0.15f && melt <= UnitView.LavaMeltSeconds + 0.4f, $"D1 {n}: melted, gone after {melt:F2} s");
                Check(c.lastTop < 0f || c.firstTop >= c.lastTop - 0.05f, $"D2 {n}: band top {c.lastTop:F2} m over the lava alive, {c.firstTop:F2} m on the first melt frame (no drop)");
                Check(c.heatMost <= c.heat0 + 0.005f && c.heatEnd <= 0.001f, $"D3 {n}: body red {c.heat0:F2} at the death, at most {c.heatMost:F2}, {c.heatEnd:F2} at the end (never rises)");
                Check((!c.shot || c.startShare < 0.6f) && c.litAtK >= 0f && c.litAtK <= 0.75f, $"D4 {n}: band starts at {c.startShare * 100f:F0} % of the body, reaches 90 % of its top at {c.litAtK * 100f:F0} % of the melt");
                Check(c.left, $"D6 {n}: nothing on the body after it is gone (leg heat, band, red all 0)");
            }
            End($"counted {height:F0}m");
        }

        /// <summary>One round: walk in; the shot ones are killed after 1.5 s in lava, the others walk to and fro until the lava kills them. Every frame each body is read.</summary>
        static IEnumerator Round(List<Case> cs, string frames)
        {
            float t0 = Time.time, first = -1f; int nextShot = 0, b0 = -1; roundBubbles = 0;
            while (Time.time - t0 < 45f)
            {
                yield return new WaitForEndOfFrame();
                float now = Time.time; bool allGone = true;
                foreach (var c in cs)
                {
                    if (c.goneAt >= 0f) continue;
                    allGone = false;
                    Read(c, out float heat, out float top, out float line, out float red);
                    if (c.meltAt < 0f)
                    {   // alive: in lava yet? the shot ones die after 1.5 s in it; the walkers go to and fro
                        if (c.o != null && c.o.InLava && c.inAt < 0f) c.inAt = now;
                        if (c.v && c.v.LavaMelting)
                        {
                            c.meltAt = now; c.firstTop = line + top - OzLava.I.lavaY; c.heat0 = c.paint ? c.paint.Heat : red; c.heatMost = c.heat0;
                            float body = BodyTop(c) - line; c.startShare = body > 0.05f ? top / body : 1f;
                            if (first < 0f) { first = now; b0 = OzLavaMelt.Bubbles; }
                            continue;
                        }
                        if (heat > 0f) c.lastTop = line + top - OzLava.I.lavaY;
                        if (c.o == null || c.o.Destroyed) continue;
                        if (c.shot && c.inAt >= 0f && now - c.inAt >= 1.5f) { c.o.Health = 1; c.o.DamageHealth(1); W.UpdateObjectHealth(c.o); }   // the batch B recipe: a kill the sim and the view both see
                        else if (!c.shot && c.inAt >= 0f && !c.o.IsMoving) { c.toB = !c.toB; OzV11Kit.PlayerMove(W, c.o, (c.toB ? bx2 : ax) * 16 + 8, (c.toB ? by2 : ay) * 16 + 8, false); }
                        continue;
                    }
                    if (c.v && c.v.gameObject.activeSelf)
                    {
                        float k = (now - c.meltAt) / UnitView.LavaMeltSeconds, h = c.paint ? c.paint.Heat : red;
                        c.heatMost = Mathf.Max(c.heatMost, h);
                        if (c.litAtK < 0f && top >= 0.9f * (BodyTop(c) - line)) c.litAtK = k;
                        continue;
                    }
                    c.goneAt = now; c.left = true; c.heatEnd = c.paint ? c.paint.Heat : 0f;
                    if (c.paint && c.paint.Heat > 0f) c.left = false;
                    foreach (var s in c.v ? c.v.GetComponentsInChildren<SkinnedMeshRenderer>(true) : new SkinnedMeshRenderer[0])
                        for (int m = 0; m < Mathf.Max(1, s.sharedMaterials.Length); m++) { s.GetPropertyBlock(blk, m); if (blk.GetFloat(LegHeatId) != 0f || blk.GetFloat(LegTopId) != 0f) c.left = false; }
                }
                if (first >= 0f && nextShot < ShotTimes.Length && now - first >= ShotTimes[nextShot]) { Shot($"{frames}_{ShotTimes[nextShot]:F2}s"); nextShot++; }
                if (allGone && nextShot >= ShotTimes.Length) break;
            }
            if (b0 >= 0) roundBubbles = OzLavaMelt.Bubbles - b0;
            Info($"round {frames}: {cs.Count} robots, {(first >= 0f ? "first death at " + (first - t0).ToString("F1") + " s" : "none died")}");
        }

        /// <summary>The top of the body now, from its skins (the melt scales the root, so their bounds follow it).</summary>
        static float BodyTop(Case c)
        {
            float top = float.NegativeInfinity;
            foreach (var s in c.v.GetComponentsInChildren<SkinnedMeshRenderer>()) if (s.enabled) top = Mathf.Max(top, s.bounds.max.y);
            return float.IsInfinity(top) ? c.v.transform.position.y : top;
        }
    }
}
#endif
