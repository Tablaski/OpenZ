using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using OpenZ.Sim;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// 1.4 speed-up 4 (View/OzViewScans.cs): the counted checks that the registries give every site exactly what its old whole-scene search
    /// gave, and what they save. Menus under OpenZ/V1.4/Perf (run 1-3 after a Play start: a level or the Unit editor running):
    ///  View scans 0  Walk deck order (no Play)    random object trees: the registry's walk deck list against the tree walk it replaces
    ///                                             (GetComponentsInChildren), shown and hidden decks, renames, moves, two colliders on one.
    ///  View scans 1  Shadow compare (Play, 120 s) every converted site runs the old search and the registry and keeps the old answer;
    ///                                             any difference is counted. Also the cost of the searches left as they are (read only).
    ///  View scans 2  A-B on this game (Play)      6 windows of 20 s, the old searches and the registries in turn: Update, LateUpdate,
    ///                                             main thread and rules ms, allocations, the sound log by key.
    ///  View scans 3  Unit editor 200 a side A-B   the same in the Unit editor with 200 robots a side (the cap bench's armies, topped up).
    /// Every run writes one line "[OpenZ v1.4 view scans] PASS / FAIL ..." to the console and to checks.txt in the output folder.
    /// </summary>
    public static class OzV14ViewScansCheck
    {
        const string Tag = "[OpenZ v1.4 view scans]";
        static string Dir => Path.GetFullPath(Path.Combine(Application.dataPath, "../../../../work/records/milestones/v1_4/perf_view_scans"));
        static readonly Stack<IEnumerator> stack = new Stack<IEnumerator>();
        static int waitFrame, ok, bad; static float waitUntil;
        static readonly StringBuilder fails = new StringBuilder(), table = new StringBuilder();

        static void Check(bool pass, string what) { if (pass) ok++; else { bad++; fails.Append(" | ").Append(what); } Debug.Log($"{Tag} {(pass ? "ok  " : "FAIL")} {what}"); }
        static void Begin() { ok = bad = 0; fails.Clear(); table.Clear(); Directory.CreateDirectory(Dir); }
        static void Finish(string what)
        {
            bool pass = bad == 0 && ok > 0;
            string line = $"{Tag} {(pass ? "PASS" : "FAIL")} {ok}/{ok + bad} {what}{(bad == 0 ? "" : "; FAILED:" + fails)}";
            if (pass) Debug.Log(line); else Debug.LogError(line);
            File.AppendAllText(Path.Combine(Dir, "checks.txt"), System.DateTime.Now.ToString("yyyy-MM-dd HH:mm") + " " + line + "\n");
            if (table.Length > 0)
            {
                string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                File.WriteAllText(Path.Combine(Dir, what.Split(' ')[0] + "_" + scene + "_" + System.DateTime.Now.ToString("MMdd_HHmm") + ".md"), table.ToString());
            }
        }
        static void Run(IEnumerator e) { stack.Clear(); stack.Push(e); waitFrame = 0; waitUntil = 0f; EditorApplication.update -= Step; EditorApplication.update += Step; }
        static void Step()
        {
            if (stack.Count == 0 || !EditorApplication.isPlaying) { stack.Clear(); EditorApplication.update -= Step; Restore(); return; }
            if (Time.frameCount < waitFrame || Time.realtimeSinceStartup < waitUntil) return;
            var top = stack.Peek(); bool more;
            try { more = top.MoveNext(); }
            catch (System.Exception ex) { Debug.LogException(ex); stack.Clear(); EditorApplication.update -= Step; Restore(); Debug.LogError(Tag + " FAIL: exception " + ex.Message); return; }
            if (!more) { stack.Pop(); if (stack.Count == 0) EditorApplication.update -= Step; return; }
            if (top.Current is IEnumerator child) { stack.Push(child); return; }                       // a step of its own (3: the A-B windows)
            if (top.Current is float s) waitUntil = Time.realtimeSinceStartup + s;
            else waitFrame = Time.frameCount + (top.Current is int n ? n : 1);
        }
        static bool bypassSet;
        static void Restore() { OzViewScans.On = true; OzViewScans.Shadow = false; if (bypassSet) { OzUeCap.Bypass = false; bypassSet = false; } }

        static bool InGame(string what)
        {
            if (!EditorApplication.isPlaying || !ZGame.I || ZGame.I.World == null) { Debug.LogError($"{Tag} FAIL {what}: run it in Play with a level or the Unit editor running"); return false; }
            return true;
        }

        // ------------------------------------------------------------------ 0 walk deck order (no Play)
        [MenuItem("OpenZ/V1.4/Perf/View scans 0 Walk deck order (no Play)")]
        public static void WalkDeckOrder()
        {
            Begin();
            var rnd = new System.Random(1404);
            int trees = 0, decksSeen = 0, sameLists = 0, multi = 0;
            var made = new List<GameObject>(); var nodes = new List<Transform>(); var decks = new List<GameObject>();
            var walk = new List<Transform>(); var reg = new List<Transform>();
            try
            {
                for (int tree = 0; tree < 300; tree++)
                {
                    nodes.Clear(); decks.Clear();
                    var root = new GameObject("OzV14_DeckRoot") { hideFlags = HideFlags.HideAndDontSave }; made.Add(root); nodes.Add(root.transform);
                    int n = 5 + rnd.Next(60);
                    for (int i = 0; i < n; i++)
                    {
                        var parent = nodes[rnd.Next(nodes.Count)];
                        bool deck = rnd.Next(4) == 0;
                        var go = new GameObject(deck ? "WalkDeck" : "Part" + i) { hideFlags = HideFlags.HideAndDontSave };
                        go.transform.SetParent(parent, false);
                        if (rnd.Next(3) == 0) go.transform.SetSiblingIndex(rnd.Next(parent.childCount));
                        if (deck) { go.AddComponent<MeshCollider>(); if (rnd.Next(10) == 0) { go.AddComponent<MeshCollider>(); multi++; } decks.Add(go); }
                        else if (rnd.Next(6) == 0) { var c = new GameObject("WalkDeck") { hideFlags = HideFlags.HideAndDontSave }; c.transform.SetParent(go.transform, false); c.AddComponent<MeshCollider>(); decks.Add(c); }
                        nodes.Add(go.transform);
                    }
                    foreach (var t in nodes) if (t != root.transform && rnd.Next(12) == 0) t.gameObject.SetActive(false);   // hidden decks and hidden branches
                    foreach (var d in decks) if (rnd.Next(15) == 0) d.name = "WalkDeckOld";                        // a renamed deck leaves the list
                    if (decks.Count > 1 && rnd.Next(3) == 0)
                    {   // a deck moved in the tree after it was made
                        var d = decks[rnd.Next(decks.Count)].transform; var to = nodes[rnd.Next(nodes.Count)];
                        if (!to.IsChildOf(d)) { d.SetParent(to, false); d.SetSiblingIndex(rnd.Next(to.childCount)); }
                    }
                    var shuffled = new List<GameObject>(decks);
                    for (int i = shuffled.Count - 1; i > 0; i--) { int j = rnd.Next(i + 1); (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]); }   // registered in any order
                    walk.Clear(); reg.Clear();
                    foreach (var mc in root.GetComponentsInChildren<MeshCollider>(false)) if (mc.name == "WalkDeck") walk.Add(mc.transform);
                    OzViewScans.DecksUnder(root.transform, shuffled, reg);
                    bool same = walk.Count == reg.Count;
                    for (int i = 0; same && i < walk.Count; i++) same = walk[i] == reg[i];
                    trees++; decksSeen += walk.Count; if (same) sameLists++;
                    if (!same && bad < 5) Check(false, $"tree {tree}: the walk gives {walk.Count} deck(s), the registry {reg.Count}");
                    foreach (var g in made) if (g) Object.DestroyImmediate(g);
                    made.Clear();
                }
            }
            finally { foreach (var g in made) if (g) Object.DestroyImmediate(g); }
            Check(sameLists == trees, $"O1 the registry's walk deck list = the tree walk, item for item: {sameLists} of {trees} random trees ({decksSeen} decks, {multi} with two colliders; hidden, renamed and moved decks among them)");
            Finish("0_walk_deck_order");
        }

        // ------------------------------------------------------------------ 1 shadow compare (Play, 120 s)
        [MenuItem("OpenZ/V1.4/Perf/View scans 1 Shadow compare (Play, 120 s)")]
        public static void ShadowCompare() { if (InGame("1 shadow compare")) Run(ShadowRun(120f)); }

        sealed class Cost { public string Name; public double Ms; public int N, Count; }

        static IEnumerator ShadowRun(float seconds)
        {
            Begin();
            OzViewScans.ResetCounts(); OzViewScans.On = true; OzViewScans.Shadow = true;
            var costs = new[] { new Cost { Name = "DebrisAudit's search (every Rigidbody + its debris layer test; left as is, its registry stays off)" },
                                new Cost { Name = "a bare walk over ZGame.Views (every view ever made: the cost per loop still walking it)" },
                                new Cost { Name = "the same walk over ZGame.LiveViews" },
                                new Cost { Name = "AudioDirector's voice search (every AudioSource; left as is)" },
                                new Cost { Name = "OzLensEdges' search (every Volume; left as is)" } };
            Debug.Log($"{Tag} shadow compare for {seconds:0} s: every converted site runs both ways (the old answer is kept). Place units, fight, damage a bridge on level 16.");
            float t0 = Time.realtimeSinceStartup, nextCost = 0f;
            while (Time.realtimeSinceStartup - t0 < seconds && EditorApplication.isPlaying)
            {
                if (Time.realtimeSinceStartup >= nextCost) { nextCost = Time.realtimeSinceStartup + 2f; MeasureCosts(costs); }
                yield return 0.25f;
            }
            OzViewScans.Shadow = false;
            table.AppendLine($"# View scans: shadow compare, {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}, {seconds:0} s, {System.DateTime.Now:yyyy-MM-dd HH:mm}");
            table.AppendLine();
            table.AppendLine("| site | calls | compared | different | old search ms a call | registry ms a call |");
            table.AppendLine("|---|---|---|---|---|---|");
            long compared = 0;
            foreach (var s in OzViewScans.Sites)
            {
                table.AppendLine($"| {s.Name} | {s.Calls} | {s.Timed} | {s.Diff} | {s.OldMs:0.0000} | {s.NewMs:0.0000} |");
                compared += s.Timed;
                Check(s.Diff == 0, $"S {s.Name}: {s.Diff} difference(s) in {s.Timed} compared call(s){(s.FirstDiff != null ? " (first: " + s.FirstDiff + ")" : "")}");
            }
            Check(compared > 0, $"S0 at least one site was compared ({compared} calls compared; walk decks known {OzViewScans.WalkDecksKnown})");
            table.AppendLine();
            table.AppendLine("Left as they are (read only, measured every 2 s):");
            table.AppendLine();
            table.AppendLine("| search | mean ms | objects |");
            table.AppendLine("|---|---|---|");
            foreach (var c in costs) table.AppendLine($"| {c.Name} | {(c.N > 0 ? c.Ms / c.N : 0):0.0000} | {c.Count} |");
            Finish("1_shadow_compare " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        }

        static void MeasureCosts(Cost[] c)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            int layer = BuildingBreaker.DebrisLayer, n = 0;
            foreach (var rb in Object.FindObjectsByType<Rigidbody>(FindObjectsSortMode.None))
                if (rb && rb.gameObject.layer == layer || (rb && rb.GetComponentInChildren<Collider>() is Collider col && col.gameObject.layer == layer)) n++;
            Add(c[0], sw, n);
            var g = ZGame.I; if (!g) return;
            sw.Restart(); n = 0; foreach (var kv in g.Views) if (kv.Value) n++; Add(c[1], sw, g.Views.Count);
            var live = g.LiveViews;
            sw.Restart(); n = 0; foreach (var kv in live) if (kv.Value) n++; Add(c[2], sw, live.Count);
            sw.Restart(); n = Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Length; Add(c[3], sw, n);
            sw.Restart(); n = Object.FindObjectsByType<Volume>(FindObjectsSortMode.None).Length; Add(c[4], sw, n);
        }
        static void Add(Cost c, System.Diagnostics.Stopwatch sw, int count) { c.Ms += sw.Elapsed.TotalMilliseconds; c.N++; c.Count = count; }

        // ------------------------------------------------------------------ 2 A-B on this game
        [MenuItem("OpenZ/V1.4/Perf/View scans 2 A-B on this game (Play, 6 x 20 s)")]
        public static void AbHere() { if (InGame("2 A-B")) Run(AbRun("2_ab", 20f, null)); }

        // ------------------------------------------------------------------ 3 Unit editor 200 a side
        [MenuItem("OpenZ/V1.4/Perf/View scans 3 Unit editor 200 a side A-B (Play)")]
        public static void AbUnitEditor()
        {
            if (!EditorApplication.isPlaying || !OzUnitEditor.UiBuilt || OzUnitEditor.I.World == null) { Debug.LogError($"{Tag} FAIL 3: open the Unit editor in Play first (main menu, UNIT EDITOR), then run it"); return; }
            Run(UnitEditorRun());
        }

        static IEnumerator UnitEditorRun()
        {
            const int perSide = 200;
            OzUeCap.Bypass = true; bypassSet = true;
            OzUePick.Unpick(); OzUePick.ClearInspect(); OzUePlayground.ClearField();
            OzUeControls.CompassHome(); OzUeControls.SetSpeed(1f); OzUeControls.SetPlaying(true);
            yield return 1.0f;
            var w = OzUnitEditor.I.World;
            Vector3 c = new Vector3(w.Map.width, 0f, -w.Map.height);
            int colB, colR;
            OzViewScansProbe.Army(Team.Blue, perSide, c, -1f, 0, out colB);
            OzViewScansProbe.Army(Team.Red, perSide, c, 1f, 0, out colR);
            yield return 2;
            OzUeControls.Fit();
            yield return OzUeCapBench.Settle;
            var top = new TopUp { c = c, perSide = perSide, colB = colB, colR = colR };
            yield return AbRun("3_ue200", 20f, top);
            OzUePlayground.ClearField();
            OzUeCap.Bypass = false; bypassSet = false;
        }

        sealed class TopUp { public Vector3 c; public int perSide, colB, colR, count; }

        static IEnumerator AbRun(string what, float windowS, TopUp top)
        {
            Begin();
            var probe = OzViewScansProbe.Ensure(); probe.Windows.Clear();
            OzViewScans.Shadow = false;
            for (int i = 0; i < 6; i++)
            {
                bool on = (i & 1) == 1;                                                                     // A B A B A B
                OzViewScans.On = on;
                yield return 2;
                probe.Begin(on ? "B registries" : "A old searches", on);
                float t0 = Time.realtimeSinceStartup, next = 1f;
                while (Time.realtimeSinceStartup - t0 < windowS && EditorApplication.isPlaying)
                {
                    if (top != null && Time.realtimeSinceStartup - t0 >= next)
                    {   // the cap bench's top-up: below 95 % a side, fill it up again (the dead views stay, as in a long fight)
                        next = Time.realtimeSinceStartup - t0 + 1f;
                        int nb = OzUePlayground.CountUnits(Team.Blue), nr = OzUePlayground.CountUnits(Team.Red);
                        if (nb < top.perSide * OzUeCapBench.TopUpAt) { probe.Exclude(0.5f); OzViewScansProbe.Army(Team.Blue, top.perSide - nb, top.c, -1f, top.colB, out top.colB); top.count++; }
                        if (nr < top.perSide * OzUeCapBench.TopUpAt) { probe.Exclude(0.5f); OzViewScansProbe.Army(Team.Red, top.perSide - nr, top.c, 1f, top.colR, out top.colR); top.count++; }
                    }
                    yield return 1;
                }
                probe.End();
            }
            yield return 2;                                                                                 // the last window's sound lines are counted a frame later
            OzViewScans.On = true;
            Report(what, probe.Windows, top);
            Object.Destroy(probe.gameObject);
        }

        static void Report(string what, List<OzViewScansProbe.Window> ws, TopUp top)
        {
            string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            table.AppendLine($"# View scans A-B, {scene}{(top != null ? ", Unit editor " + top.perSide + " a side, top-ups " + top.count : "")}, {System.DateTime.Now:yyyy-MM-dd HH:mm}");
            table.AppendLine();
            table.AppendLine("A = the old whole-scene searches (OzViewScans.On false, 1.3), B = the registries. Frame means; view Update = Update minus the rules' own time.");
            table.AppendLine();
            table.AppendLine("| window | s | frames | Update ms | view Update ms | LateUpdate ms | main ms | rules ms | KB a frame | views / live | sound plays |");
            table.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|");
            foreach (var w in ws)
                table.AppendLine($"| {w.Label} | {w.Seconds:0.0} | {w.Frames} | {w.MeanUpdate:0.00} | {w.MeanUpdate - w.MeanRules:0.00} | {w.MeanLate:0.00} | {w.MeanMain:0.00} | {w.MeanRules:0.00} | {w.MeanAllocKb:0.0} | {w.Views} / {w.Live} | {w.Plays} |");
            double Mean(bool on, System.Func<OzViewScansProbe.Window, double> f) { double s = 0; int n = 0; foreach (var w in ws) if (w.On == on && w.Frames > 0) { s += f(w); n++; } return n > 0 ? s / n : 0; }
            double Spread(bool on, System.Func<OzViewScansProbe.Window, double> f) { double lo = double.MaxValue, hi = double.MinValue; foreach (var w in ws) if (w.On == on && w.Frames > 0) { var v = f(w); lo = System.Math.Min(lo, v); hi = System.Math.Max(hi, v); } return hi >= lo ? hi - lo : 0; }
            System.Func<OzViewScansProbe.Window, double> scripts = w => w.MeanUpdate - w.MeanRules + w.MeanLate;
            double a = Mean(false, scripts), b = Mean(true, scripts), spread = Spread(false, scripts);
            table.AppendLine();
            table.AppendLine($"Means A / B: view Update {Mean(false, w => w.MeanUpdate - w.MeanRules):0.00} / {Mean(true, w => w.MeanUpdate - w.MeanRules):0.00} ms, LateUpdate {Mean(false, w => w.MeanLate):0.00} / {Mean(true, w => w.MeanLate):0.00} ms, main {Mean(false, w => w.MeanMain):0.00} / {Mean(true, w => w.MeanMain):0.00} ms; view Update + LateUpdate {a:0.00} / {b:0.00} ms (B - A {b - a:+0.00;-0.00} ms, the A windows' own spread {spread:0.00} ms)");
            // the sound log by key: A against B
            var keys = new SortedDictionary<string, int[]>();
            foreach (var w in ws) foreach (var kv in w.Sounds) { if (!keys.TryGetValue(kv.Key, out var ab)) keys[kv.Key] = ab = new int[2]; ab[w.On ? 1 : 0] += kv.Value; }
            int playsA = 0, playsB = 0, onlyA = 0, onlyB = 0; var odd = new StringBuilder();
            table.AppendLine();
            table.AppendLine("| sound key and outcome (the game's sound log) | A | B |");
            table.AppendLine("|---|---|---|");
            foreach (var kv in keys)
            {
                table.AppendLine($"| {kv.Key} | {kv.Value[0]} | {kv.Value[1]} |");
                bool play = kv.Key.EndsWith(" PLAY") || kv.Key.EndsWith(" LOOP");
                if (!play) continue;
                playsA += kv.Value[0]; playsB += kv.Value[1];
                if (kv.Value[0] >= 5 && kv.Value[1] == 0) { onlyA++; odd.Append(' ').Append(kv.Key); }
                if (kv.Value[1] >= 5 && kv.Value[0] == 0) { onlyB++; odd.Append(' ').Append(kv.Key); }
            }
            Check(ws.Count == 6 && ws.TrueForAll(w => w.Frames > 0), $"F1 6 windows with frames (A B A B A B; frames {string.Join(" ", ws.ConvertAll(w => w.Frames.ToString()))})");
            Check(b <= a + System.Math.Max(0.03 * a, spread), $"F2 the registries are not slower: view Update + LateUpdate A {a:0.00} ms, B {b:0.00} ms (allowed: A + the larger of 3 % and the A windows' spread {spread:0.00} ms)");
            Check(onlyA == 0 && onlyB == 0, $"N1 no sound that plays 5 times or more in one half is missing in the other ({onlyA + onlyB}{(odd.Length > 0 ? ":" + odd : "")})");
            double ratio = playsA > 0 ? playsB / (double)playsA : (playsB == 0 ? 1.0 : 99.0);
            Check(ratio >= 0.8 && ratio <= 1.25, $"N2 sound plays A {playsA}, B {playsB} (B / A {ratio:0.00}, wanted 0.80-1.25: the same fight, other moments)");
            Finish(what + " " + scene);
        }
    }
}
