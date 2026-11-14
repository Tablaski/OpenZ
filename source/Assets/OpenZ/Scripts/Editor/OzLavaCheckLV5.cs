// OpenZ (GPL-3.0) - version 1.3, level 7, kit LV5: the counted checks of what melts in lava. Editor only, never in a build.
using System.Collections.Generic;
using OpenZ.Sim;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// Run in Play on L07, game speed 1, not paused. Drops test pieces through the game's real door (OzWaterLanding.Watch, FxManager.Breakup, DetailKit.Chunks) onto the deepest lake and the dry
    /// bank beside it, moves the camera there, and watches each piece frame by frame: it must take the heat from the lava line, go down over a few seconds, and be under the sheet when it goes.
    /// The test pieces are its own cubes on a level thrown away after; a stand-in hull is an object watched as "wreck hull" with no unit behind it, as no vehicle can walk into lava.
    /// The last stage uses real WreckPieces, breaks the MaxLive cap over the melting ones and asks PopOut for one: nothing may end a piece mid-melt.
    /// Every line starts [LV5].
    /// </summary>
    public static class OzLavaCheckLV5
    {
        const string Root = "OpenZ/L07 Lava/Checks/";
        const float HeatAt = 2f, SecondsMin = 1.9f, SecondsMax = 7.5f, TopTol = 0.02f;
        static readonly int LegHeat = Shader.PropertyToID("_OZLegHeat"), OzHeat = Shader.PropertyToID("_OZHeat"), LegLine = Shader.PropertyToID("_OZLegLine"), LegTop = Shader.PropertyToID("_OZLegTop");

        sealed class Case
        {
            public string name; public Transform t; public Renderer r; public int cls; public bool land, seenTaken, gone, teamLit; public float takenAt = -1f, goneAt = -1f, heatMax, redMax, topMax, topAtGone = 99f, share0 = -1f, minShare = 9f, lastY;
            public bool yUp; public int yUps; public float y0;
        }
        static readonly List<Case> cases = new List<Case>(), owners = new List<Case>();
        static Case popCase; static WreckPieces lit0; static int cut0, meltingAtCap; static bool popAsked, popWaited;
        static int pass, fail, stage; static float t0, lavaY; static Vector3 lake, bank;
        static readonly MaterialPropertyBlock blk = new MaterialPropertyBlock();
        static ProfilerRecorder gc; static readonly List<long> quiet = new List<long>(), busy = new List<long>();

        static void V(bool ok, string what) { if (ok) pass++; else fail++; Debug.Log("[LV5] " + (ok ? "PASS  " : "FAIL  ") + what); }

        // ------------------------------------------------------------------ no Play: the time line
        [MenuItem(Root + "LV5 melt time line (no Play)")]
        static void Plan()
        {
            pass = fail = 0;
            for (int cls = 0; cls <= 3; cls++)
            {
                float lo = 99f, hi = 0f;
                for (uint h = 0; h < 2000u; h++) { float s = OzLavaMeltPlan.Seconds(cls, h * 2654435761u); lo = Mathf.Min(lo, s); hi = Mathf.Max(hi, s); }
                V(lo >= SecondsMin && hi <= SecondsMax, $"size {cls}: {lo:F2}-{hi:F2} s, a few seconds");
                bool down = true, heat = true; float top = 2f, pt = 2f, ph = -1f;
                for (int i = 0; i <= 400; i++) { float k = i / 400f, y = OzLavaMeltPlan.TopOver(top, 0.2f, k), H = OzLavaMeltPlan.Heat(k); down &= y <= pt + 1e-5f; heat &= H >= ph - 1e-5f; pt = y; ph = H; }
                V(down && pt <= -0.2f + 1e-4f, $"size {cls}: its top only ever goes down, ends {pt:F2} m under");
                V(heat && OzLavaMeltPlan.Heat(0f) == 0f && ph == 1f, $"size {cls}: heat rises 0 to 1 and never falls");
            }
            V(OzLavaMeltPlan.Sink(0.08f) < 0.01f && OzLavaMeltPlan.Heat(0.08f) > 0.1f, "it glows before it goes down");
            V(OzLavaMeltPlan.Band(0.02f) < 0.01f && OzLavaMeltPlan.Band(0.3f) > 0.3f && OzLavaMeltPlan.Band(0.56f) >= 0.999f, "the glow climbs from the lava line");
            V(OzLavaMeltPlan.Flatten < 0.5f, "it softens, it never scales away");
            Debug.Log($"[LV5] {(fail == 0 ? "PASS" : "FAIL")} {pass}/{pass + fail}");
        }

        // ------------------------------------------------------------------ other levels
        [MenuItem(Root + "LV5 no trace (Play, on L02 L16 L17)")]
        static void NoTrace()
        {
            if (!Application.isPlaying || !ZGame.I) { Debug.LogWarning("[LV5] enter Play first"); return; }
            pass = fail = 0;
            V(!ZGame.I.World.Map.HasLava, "this level has no lava");
            V(!OzLavaMelt.Live && OzLavaMelt.I == null, "nothing live");
            V(Object.FindObjectsByType<OzLavaMelt>(FindObjectsSortMode.None).Length == 0, "no component in the scene");
            V(OzLavaMelt.Taken == 0 && OzLavaMelt.Rubble == 0, "nothing taken");
            Debug.Log($"[LV5] {(fail == 0 ? "PASS" : "FAIL")} {pass}/{pass + fail} on {ZGame.I.level}");
        }

        // ------------------------------------------------------------------ Play on L07
        static GameObject Cube(string name, Vector3 at, Vector3 size, bool teamLit, Material lit, Material other)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.layer = BuildingBreaker.DebrisLayer;
            go.transform.SetPositionAndRotation(at, Quaternion.Euler(Random.Range(0f, 30f), Random.Range(0f, 360f), Random.Range(0f, 30f))); go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = teamLit ? lit : other;
            return go;
        }

        static Case Drop(string name, Vector3 at, Vector3 size, bool teamLit, int cls, bool land, Material lit, Material other)
        {
            var go = Cube(name, at, size, teamLit, lit, other);
            var rb = go.AddComponent<Rigidbody>(); rb.mass = 20f; rb.interpolation = RigidbodyInterpolation.Interpolate;
            OzWaterLanding.Watch(go.transform, name, rb);
            var c = new Case { name = name, t = go.transform, r = go.GetComponent<MeshRenderer>(), cls = cls, land = land, teamLit = teamLit, lastY = at.y, y0 = at.y };
            cases.Add(c); return c;
        }

        [MenuItem(Root + "LV5 melting (Play, on L07, about 45 s)")]
        static void Run()
        {
            var g = ZGame.I;
            if (!Application.isPlaying || !g || g.World == null || !g.World.Map.HasLava || !OzLava.I) { Debug.LogWarning("[LV5] enter Play on L07 first"); return; }
            pass = fail = stage = 0; cases.Clear(); owners.Clear(); popCase = null; lit0 = null; quiet.Clear(); busy.Clear(); OzLavaMelt.ClearCounts();
            V(OzLavaMelt.Live && OzLavaMelt.I, "the melt is live on this level");
            V(Resources.Load<Material>(OzLavaMelt.MaterialRes) && Shader.Find("OpenZ/LavaMelt"), "M_L07_Melt and OpenZ/LavaMelt load");
            V(AudioDirector.Has(OzLava.SndEnter) && AudioDirector.Has(OzLava.SndMelt), "the sounds lava_sizzle_enter and lava_melt exist");
            var m = g.World.Map; var best = Deepest(m); lavaY = OzLava.I.lavaY;
            lake = new Vector3(best.x * 2f + 1f, lavaY, -best.y * 2f - 1f);
            if (!Dry(g, best, out bank)) { V(false, "no dry bank near the deepest lava tile"); return; }
            Debug.Log($"[LV5] test lake at {lake}, bank at {bank}: the camera goes there, game speed 1, not paused");
            var rts = Object.FindFirstObjectByType<RtsCamera>(); if (rts) rts.focus = new Vector3(lake.x, 0f, lake.z);                 // the melt draws its glow on screen only
            gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            t0 = Time.time; EditorApplication.update -= Tick; EditorApplication.update += Tick;
        }

        static Vector3 OnLava(Vector3 off) { var at = lake + off; return OzLava.SurfaceAt(at, out _) ? at : lake + new Vector3(0f, off.y, 0f); }

        static void Launch()
        {
            var lit = DetailKit.ToyMat("LV5_Test"); lit.color = new Color(0.45f, 0.45f, 0.42f);
            var other = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "LV5_TestOther" };
            float y = lavaY + 6f;
            Drop("lv5 small", lake + new Vector3(-3f, 6f, 0f), Vector3.one * 0.35f, true, 0, false, lit, other);
            Drop("lv5 medium", lake + new Vector3(-1f, 6f, 0f), new Vector3(1f, 0.6f, 0.8f), true, 1, false, lit, other);
            Drop("lv5 large", lake + new Vector3(1.5f, 6f, 0f), new Vector3(2.4f, 1.1f, 1.6f), true, 2, false, lit, other);
            var plate = Drop("lv5 plate", OnLava(new Vector3(4f, 6f, 3.5f)), new Vector3(3.4f, 0.3f, 1.6f), true, 2, false, lit, other);    // long and flat: the tilt lifts one end of it
            plate.t.rotation = Quaternion.Euler(Random.Range(-4f, 4f), Random.Range(0f, 360f), Random.Range(-4f, 4f));
            Drop("lv5 other shader", lake + new Vector3(0f, 6f, 2.5f), new Vector3(1f, 0.6f, 0.8f), false, 1, false, lit, other);
            Drop("lv5 on land", bank + Vector3.up * 3f, new Vector3(1f, 0.6f, 0.8f), true, 1, true, lit, other);
            var hull = Cube("lv5 hull", lake + new Vector3(0f, 0.2f, -3f), new Vector3(3f, 1.4f, 2f), true, lit, other); Object.Destroy(hull.GetComponent<Collider>());
            hull.transform.rotation = Quaternion.identity; OzWaterLanding.Watch(hull.transform, "wreck hull", null, true, true);
            cases.Add(new Case { name = "lv5 hull", t = hull.transform, r = hull.GetComponent<MeshRenderer>(), cls = 3, teamLit = true, lastY = hull.transform.position.y });
            FxManager.I.Breakup(lake + new Vector3(0f, 3f, 4f), "metal", 3f, 12);
            var hub = DetailPropHub.I;
            if (hub)
            {
                var d = Cube("lv5 chunk", lake + new Vector3(3.5f, 4f, 0f), Vector3.one * 0.5f, true, lit, other); Object.Destroy(d.GetComponent<Collider>());
                hub.Chunks.Throw(d.transform, Vector3.down, Vector3.one * 60f);
                cases.Add(new Case { name = "lv5 chunk", t = d.transform, r = d.GetComponent<MeshRenderer>(), cls = 1, teamLit = true, lastY = d.transform.position.y });
            }
        }

        static void Crowd()
        {
            var lit = DetailKit.ToyMat("LV5_Crowd");
            for (int i = 0; i < 80; i++)
            {
                float a = i * 2.399f, r = 0.7f * Mathf.Sqrt(i);
                var at = lake + new Vector3(Mathf.Cos(a) * r, 5f + (i % 5) * 0.8f, Mathf.Sin(a) * r);
                if (!OzLava.SurfaceAt(at, out _)) at = lake + new Vector3(Mathf.Cos(a), 5f + (i % 5) * 0.8f, Mathf.Sin(a));
                var go = Cube("lv5 crowd " + i, at, Vector3.one * (0.3f + (i % 4) * 0.15f), true, lit, null);
                var rb = go.AddComponent<Rigidbody>(); rb.mass = 5f; OzWaterLanding.Watch(go.transform, "lv5 crowd", rb);
            }
            for (int i = 0; i < 6; i++) FxManager.I.Breakup(lake + new Vector3(i - 3f, 3f + i * 0.2f, 1f), i % 2 == 0 ? "stone" : "metal", 3f, 12);
        }

        /// <summary>Real wreck pieces onto the lava, one of them on fire, and one more cube for the pop-out.</summary>
        static void Owners()
        {
            var lit = DetailKit.ToyMat("LV5_Owners"); cut0 = OzLavaMelt.Cut;
            for (int i = 0; i < 8; i++)
            {
                float a = i * 0.785f;
                var part = Cube("lv5 real " + i, lake + new Vector3(Mathf.Cos(a) * 1.5f, 3f, Mathf.Sin(a) * 1.5f), new Vector3(1.2f, 0.7f, 0.9f), true, lit, null);
                Object.Destroy(part.GetComponent<Collider>());
                if (!WreckPieces.Single(part.transform, lake + Vector3.up * 3f, 0f, 7700 + i)) continue;
                var root = part.transform.parent;
                owners.Add(new Case { name = part.name, t = root, r = part.GetComponent<MeshRenderer>(), cls = 1, teamLit = true });
                if (!lit0 && root.TryGetComponent<WreckPieces>(out var wp)) { wp.OzIgnite(30f); lit0 = wp; }
            }
            popCase = Drop("lv5 pop", OnLava(new Vector3(4f, 5f, -3f)), new Vector3(1f, 0.6f, 0.8f), true, 1, false, lit, null);
            cases.Remove(popCase);
        }

        /// <summary>While they melt: PopOut is asked for one, and enough new real pieces are made on the bank for the cap to take every older one.</summary>
        static void Cap()
        {
            popAsked = popCase != null && OzLavaMelt.Busy(popCase.t);
            if (popAsked) { PopOut.Begin(popCase.t, PopOut.Style.Debris, null, true, false); popWaited = !PopOut.IsPopping(popCase.t); }
            meltingAtCap = 0; foreach (var c in owners) if (c.t && OzLavaMelt.Busy(c.t)) meltingAtCap++;
            var lit = DetailKit.ToyMat("LV5_Cap");
            int n = WreckPieces.MaxLive + 1;
            for (int i = 0; i < n; i++)
            {
                var part = Cube("lv5 cap " + i, bank + new Vector3((i % 9 - 4) * 0.8f, 1f + (i / 9) * 0.8f, (i % 7 - 3) * 0.6f), Vector3.one * 0.3f, true, lit, null);
                Object.Destroy(part.GetComponent<Collider>());
                WreckPieces.Single(part.transform, bank, 0f, 7800 + i);
            }
        }

        static void Tick()
        {
            if (!Application.isPlaying) { EditorApplication.update -= Tick; gc.Dispose(); return; }
            float t = Time.time - t0;
            if (stage == 0 && t > 1f) { if (gc.Valid) quiet.Add(gc.LastValue); if (t > 2f) { Launch(); stage = 1; } }
            foreach (var c in cases) Sample(c);
            foreach (var c in owners) Sample(c);
            if (popCase != null) Sample(popCase);
            if (stage == 1 && t > 16f) { Report1(); Crowd(); stage = 2; }
            if (stage == 2 && gc.Valid && OzLavaMelt.I && OzLavaMelt.I.LiveNow > 40) busy.Add(gc.LastValue);
            if (stage == 2 && t > 30f) { Report2(); Owners(); stage = 3; }
            if (stage == 3 && t > 33.5f) { Cap(); stage = 4; }
            if (stage == 4 && t > 43f) { Report3(); stage = 5; EditorApplication.update -= Tick; gc.Dispose(); }
        }

        static void Sample(Case c)
        {
            if (c.gone) return;
            if (!c.t || !c.r || !c.r.enabled || !c.t.gameObject.activeInHierarchy)
            {
                c.gone = true; c.goneAt = Time.time;
                return;
            }
            bool melting = OzLavaMelt.Busy(c.t);
            if (melting && !c.seenTaken) { c.seenTaken = true; c.takenAt = Time.time; c.lastY = c.t.position.y; }
            c.r.GetPropertyBlock(blk, 0);
            c.heatMax = Mathf.Max(c.heatMax, blk.GetFloat(LegHeat)); c.redMax = Mathf.Max(c.redMax, blk.GetFloat(OzHeat)); c.topMax = Mathf.Max(c.topMax, blk.GetFloat(LegTop));
            var s = c.t.localScale; float share = Mathf.Min(s.x, Mathf.Min(s.y, s.z)) / Mathf.Max(0.001f, Mathf.Max(s.x, Mathf.Max(s.y, s.z)));   // thinnest side over longest, against its own first sample: a flat plate starts thin
            if (c.share0 < 0f) c.share0 = share;
            c.minShare = Mathf.Min(c.minShare, share / Mathf.Max(0.001f, c.share0));
            c.topAtGone = c.r.bounds.max.y;
        }

        static void Report1()
        {
            foreach (var c in cases)
            {
                if (c.land)
                {
                    V(!c.seenTaken && c.heatMax == 0f && c.r && c.r.enabled, $"{c.name}: stays normal, heat {c.heatMax:F2}");
                    continue;
                }
                V(c.seenTaken, $"{c.name}: reached the melt");
                float s = c.goneAt - c.takenAt;
                V(c.gone && s >= SecondsMin && s <= SecondsMax + 0.3f, $"{c.name}: gone after {s:F2} s");
                V(c.topAtGone <= lavaY + 0.08f, $"{c.name}: its top {c.topAtGone - lavaY:F2} m from the surface on its last frame: under, no pop");
                if (c.teamLit) V(c.heatMax >= 0.95f * OzLavaMelt.PeakGlow && c.redMax > 0.3f && c.topMax > 0.2f, $"{c.name}: heat {c.heatMax:F2}, red {c.redMax:F2}, band up to {c.topMax:F2} m");
                V(c.minShare >= 0.5f, $"{c.name}: it never scaled away, thinnest share {c.minShare:F2} of its start (the slump ends at {(1f - OzLavaMeltPlan.Flatten) / (1f + OzLavaMeltPlan.Widen):F2})");
            }
            V(OzLavaMelt.HullsTaken >= 1, $"a whole wreck melted: {OzLavaMelt.HullsTaken}");
            V(OzLavaMelt.Rubble >= 6 && OzLavaMelt.RubbleUnder >= 6, $"rubble: {OzLavaMelt.Rubble} touched, {OzLavaMelt.RubbleUnder} under");
            V(OzLavaMelt.ChipSounds >= 2 && OzLavaMelt.ChipBubbles >= 1 && OzLavaMelt.ChipRings >= 1, $"rubble chips: {OzLavaMelt.ChipSounds} sounds, {OzLavaMelt.ChipBubbles} bubbles, {OzLavaMelt.ChipRings} rings of their own");
            V(OzLavaMelt.Smokes >= 10, $"smoke puffs {OzLavaMelt.Smokes}");
            V(OzLavaMelt.Bubbles >= 6, $"bubbles {OzLavaMelt.Bubbles}");
            V(OzLavaMelt.Rings >= 6, $"rings {OzLavaMelt.Rings}");
            V(OzLavaMelt.Sizzles >= 5 && OzLavaMelt.MeltSounds >= 3, $"sounds: {OzLavaMelt.Sizzles} sizzles, {OzLavaMelt.MeltSounds} melts");
            V(OzLavaMelt.Cut == 0, $"nothing cut short ({OzLavaMelt.Cut})");
        }

        static long Median(List<long> l) { if (l.Count == 0) return -1; l.Sort(); return l[l.Count / 2]; }

        static void Report2()
        {
            V(OzLavaMelt.MostLive >= 60, $"many at once: {OzLavaMelt.MostLive} pieces, {OzLavaMelt.MostRubble} chips");
            V(OzLavaMelt.Overflow == 0, $"none past the slots ({OzLavaMelt.Overflow})");
            V(OzLavaMelt.WorstMs < 2.5f, $"worst frame of the melt {OzLavaMelt.WorstMs:F2} ms");
            long q = Median(quiet), b = Median(busy);
            V(q >= 0 && b >= 0 && b - q <= 1024, $"garbage: median {b} B a frame with {OzLavaMelt.MostLive} melting against {q} B without (the frames with no new piece count: the median)");
            V(OzLavaMelt.Gone >= OzLavaMelt.Taken - 30 && OzLavaMelt.Cut == 0, $"gone {OzLavaMelt.Gone} of {OzLavaMelt.Taken}, the rest still going down");
        }

        static void Report3()
        {
            int taken = 0, under = 0;
            foreach (var c in owners) { if (!c.seenTaken) continue; taken++; if (c.gone && c.topAtGone <= lavaY + 0.08f) under++; }
            V(meltingAtCap >= 4, $"real wreck pieces: {meltingAtCap} of {owners.Count} were melting when the cap came");
            V(taken >= 4 && under == taken, $"real wreck pieces: {under} of {taken} ended under the sheet");
            V(OzLavaMelt.Cut == cut0, $"the cap and the pop-out cut nothing short ({OzLavaMelt.Cut - cut0})");
            V(popAsked && popWaited, $"PopOut waited for the melt (asked {popAsked}, waited {popWaited}, held {OzLavaMelt.PopsHeld})");
            V(popCase != null && !popCase.t, "the piece PopOut was asked for is destroyed after its melt");
            V(lit0 && !lit0.OzBurning, "a burning piece stops burning when it has gone under");
            Debug.Log("[LV5] " + OzLavaMelt.Status());
            Debug.Log($"[LV5] {(fail == 0 ? "PASS" : "FAIL")} {pass}/{pass + fail}");
        }

        static Vector2Int Deepest(ZMapData m)
        {
            int best = -1; var at = Vector2Int.zero;
            for (int y = 0; y < m.height; y++)
                for (int x = 0; x < m.width; x++)
                {
                    if (!m.LavaAt(x, y)) continue;
                    int r = 0;
                    while (r < 12 && Ring(m, x, y, r + 1)) r++;
                    if (r > best) { best = r; at = new Vector2Int(x, y); }
                }
            return at;
        }

        static bool Ring(ZMapData m, int x, int y, int r)
        {
            for (int d = -r; d <= r; d++) if (!m.LavaAt(x + d, y - r) || !m.LavaAt(x + d, y + r) || !m.LavaAt(x - r, y + d) || !m.LavaAt(x + r, y + d)) return false;
            return true;
        }

        static bool Dry(ZGame g, Vector2Int tile, out Vector3 at)
        {
            at = default;
            for (int r = 2; r <= 14; r++)
                for (int y = tile.y - r; y <= tile.y + r; y++)
                    for (int x = tile.x - r; x <= tile.x + r; x++)
                    {
                        if (System.Math.Max(System.Math.Abs(x - tile.x), System.Math.Abs(y - tile.y)) != r || g.World.Map.LavaAt(x, y) || !g.World.Path.TilePassable(x, y, true)) continue;
                        var p = new Vector3(x * 2f + 1f, 0f, -y * 2f - 1f);
                        if (OzLava.SurfaceAt(p, out _)) continue;
                        p.y = g.GroundY(p.x, p.z); at = p; return true;
                    }
            return false;
        }
    }
}
