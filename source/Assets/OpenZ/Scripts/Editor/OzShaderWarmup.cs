using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// 1.2 items 171 / 171b (a dropped unit drawn as a flat cyan shape for a few seconds in the Unit editor, then its real model): the editor
    /// compiles a shader variant the first time something draws with it and shows a cyan stand-in until that is done. Every change of a game
    /// shader (TeamLit above all, which draws every unit) throws that shader's compiled variants away, so after such a change the first unit
    /// of each look came in cyan. This compiles the variants the game draws with BEFORE they are needed:
    ///   - the list: Resources/OpenZ/Shaders/OzWarmVariants.shadervariants (the built game warms the same list: View/OzShaderWarm.cs). Made
    ///     from every game shader under Assets/OpenZ (the ones its materials, its Resources folders, the always-included list and
    ///     <see cref="UnitShaders"/> name) x the keyword sets the render pipeline sets at run time (<see cref="GlobalSets"/>), each pass taking
    ///     only its own keywords and the materials' own keywords; plus every variant of those shaders, of the pipeline's lit / unlit / particle
    ///     shaders and of the UI shader that the editor saw drawn in Play (merged after each Play, <see cref="SeenPath"/>). Variants are only
    ///     ever added;
    ///   - editor load, every script reload and every import of a game shader or material: each variant is drawn once into a tiny hidden
    ///     texture with BACKGROUND compiling, the unit shaders first, never more than <see cref="InFlight"/> compiles at a time, so nothing
    ///     waits and the editor stays usable while it runs;
    ///   - Play start: what the units need first (<see cref="Rank"/> 0 and 1) and is still not compiled is compiled at once before the game
    ///     starts (a progress bar when it takes long, at most <see cref="PlayStartCapSeconds"/> s, it can be cancelled; usually nothing is left).
    /// The editor's own background compiling stays on (Project Settings, Editor); only these hidden draws pick their compile mode.
    /// Report: Logs/oz_shader_warmup.txt (menu OpenZ/Shaders/Warm-up report).
    /// </summary>
    [InitializeOnLoad]
    public static class OzShaderWarmup
    {
        public const string ListPath = "Assets/OpenZ/Resources/OpenZ/Shaders/OzWarmVariants.shadervariants";
        public const string SeenPath = "Assets/OpenZ/Settings/OzSeenShaderVariants.shadervariants";
        public const string ReportPath = "Logs/oz_shader_warmup.txt";
        /// <summary>Background compiles started at once (each takes one compiler process; more would load every core).</summary>
        public static int InFlight = 2;
        /// <summary>Editor time per update spent on the hidden draws (ms).</summary>
        public static double TickBudgetMs = 6.0;
        /// <summary>The most the Play start may wait for the units' variants (s); the rest goes on in the background.</summary>
        public static double PlayStartCapSeconds = 25.0;
        /// <summary>The shaders a dropped or produced unit is drawn with, the Unit editor's playground and its pictures: warmed first.</summary>
        public static readonly string[] UnitShaders =
        {
            "OpenZ/LavaVeins", "OpenZ/EvilZodGhost", "OpenZ/TeamLit", "OpenZ/UeFloor", "OpenZ/UeGhost", "OpenZ/UeHolo", "OpenZ/UI/UeFloorThumb", "OpenZ/UI/PosterFigure",
            "OpenZ/UnitBlinkers", "OpenZ/VehicleGlow", "OpenZ/SoftGroundBlob", "OpenZ/ShadowFadeCaster",
        };
        /// <summary>The last run, one line (also in the console and the report).</summary>
        public static string LastLine { get; private set; } = "";

        const string DoneKey = "OpenZ.ShaderWarm.Done";
        const string SeenCountKey = "OpenZ.ShaderWarm.SeenCount";
        const string Instancing = "INSTANCING_ON";
        const string Punctual = "_CASTING_PUNCTUAL_LIGHT_SHADOW";
        static readonly ShaderTagId LightMode = new ShaderTagId("LightMode");
        static readonly string[] SkipModes = { "Meta", "Universal2D", "UniversalGBuffer", "SceneSelectionPass", "Picking", "ScenePickingPass" };

        sealed class Item
        {
            public Shader shader;
            public int pass;
            public PassType type;
            public string[] globals;   // set on the whole editor for the draw (the shader declares them global, or they are built in)
            public string[] locals;    // set on the hidden material (the shader declares them local)
            public bool inst;          // drawn instanced (INSTANCING_ON)
            public int rank;           // 0 seen in Play, 1 unit shader + the usual set, 2 unit shader, 3 game shader + the usual set, 4 the rest
            public string key;
        }

        static List<Item> queue, lastItems;
        static int at, requested, drawn, notCompiled, skipped;
        static double runStart;
        static string runReason = "", runSig = "";
        static Mesh tri;
        static RenderTexture target;
        static CommandBuffer cmd;
        static readonly Dictionary<Shader, Material> mats = new Dictionary<Shader, Material>();
        static readonly Dictionary<string, bool> saved = new Dictionary<string, bool>();
        static readonly Matrix4x4[] one = { Matrix4x4.identity };
        static bool scheduled, ticking;

        static OzShaderWarmup()
        {
            EditorApplication.playModeStateChanged += OnPlayMode;
            AssemblyReloadEvents.beforeAssemblyReload += Cleanup;
            Schedule("editor load / script reload");
        }

        // ------------------------------------------------------------------------------------------------ when
        /// <summary>Starts a run two editor frames from now (after the import or reload that asked for it has finished).</summary>
        public static void Schedule(string why)
        {
            runReason = why;
            if (scheduled) return;
            scheduled = true;
            EditorApplication.delayCall += () => EditorApplication.delayCall += () => { scheduled = false; Begin(); };
        }

        static void OnPlayMode(PlayModeStateChange s)
        {
            if (s == PlayModeStateChange.ExitingEditMode) PlayStart();
            else if (s == PlayModeStateChange.EnteredEditMode) MergeSeen();
        }

        static void Begin()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) { Schedule(runReason); return; }
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;   // batch mode without graphics: nothing draws
            List<Item> items;
            try { items = Build(!EditorApplication.isPlayingOrWillChangePlaymode); }
            catch (Exception e) { Debug.LogWarning("[OpenZ Shaders] warm-up list failed: " + e.Message); return; }
            lastItems = items;
            string sig = Signature(items);
            if (SessionState.GetString(DoneKey, "") == sig) return;   // this editor session warmed exactly this list already
            queue = items; at = 0; requested = 0; drawn = 0; notCompiled = 0; skipped = 0; runSig = sig;
            runStart = EditorApplication.timeSinceStartup;
            if (!ticking) { ticking = true; EditorApplication.update += Tick; }
        }

        static void Tick()
        {
            if (queue == null) { Stop(); return; }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (requested >= InFlight)
            {
                if (ShaderUtil.anythingCompiling) return;   // wait for the compiles started so far
                requested = 0;
            }
            var sw = Stopwatch.StartNew();
            Save();
            try
            {
                while (at < queue.Count && requested < InFlight && sw.Elapsed.TotalMilliseconds < TickBudgetMs)
                {
                    var it = queue[at++];
                    if (!Prepare(it)) { skipped++; continue; }
                    if (!it.inst && ShaderUtil.IsPassCompiled(mats[it.shader], it.pass)) continue;
                    bool busy = ShaderUtil.anythingCompiling;
                    Draw(it, false);
                    drawn++;
                    if (!it.inst) { notCompiled++; requested++; }
                    else if (ShaderUtil.anythingCompiling) { if (!busy) notCompiled++; requested++; }   // busy: it may have started one too, so it counts against InFlight
                }
            }
            finally { Restore(); }
            if (at >= queue.Count && !ShaderUtil.anythingCompiling)
            {
                double s = EditorApplication.timeSinceStartup - runStart;
                LastLine = $"editor warm-up ({runReason}): {queue.Count} variant passes of {queue.Select(i => i.shader).Distinct().Count()} shaders checked in {s:0.0} s; {notCompiled} were compiled now in the background, {skipped} skipped";
                if (notCompiled > 0 || skipped > 0) Debug.Log("[OpenZ Shaders] " + LastLine);
                SessionState.SetString(DoneKey, runSig);
                WriteReport(queue, LastLine);
                queue = null;
                Stop();
            }
        }

        static void Stop()
        {
            if (ticking) { EditorApplication.update -= Tick; ticking = false; }
        }

        /// <summary>Play start: the units' variants (ranks 0 and 1) still not compiled are compiled now, so the first unit draws its real model.</summary>
        static void PlayStart()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;
            List<Item> items = lastItems;   // the list of the last run (every shader / material import makes it again)
            if (items == null)
            {
                try { items = Build(true); }
                catch (Exception e) { Debug.LogWarning("[OpenZ Shaders] warm-up list failed at Play start: " + e.Message); return; }
            }
            var sw = Stopwatch.StartNew();
            int n = 0, checkedN = 0, compiledNow = 0; bool cancelled = false, capped = false, bar = false;
            Save();
            try
            {
                var first = items.Where(i => i.rank <= 1).ToList();
                foreach (var it in first)
                {
                    if (sw.Elapsed.TotalSeconds > PlayStartCapSeconds) { capped = true; break; }
                    if (!Prepare(it)) continue;
                    checkedN++;
                    if (!it.inst && ShaderUtil.IsPassCompiled(mats[it.shader], it.pass)) continue;
                    double t0 = sw.Elapsed.TotalMilliseconds;
                    Draw(it, true);
                    n++;
                    if (sw.Elapsed.TotalMilliseconds - t0 > 50.0) compiledNow++;
                    if (sw.Elapsed.TotalMilliseconds > 400.0)
                    {
                        bar = true;
                        if (EditorUtility.DisplayCancelableProgressBar("OpenZ", $"Preparing the unit shaders ({checkedN} / {first.Count}) - only after a shader change", checkedN / (float)Mathf.Max(1, first.Count)))
                        { cancelled = true; break; }
                    }
                }
            }
            finally { Restore(); if (bar) EditorUtility.ClearProgressBar(); }
            string line = $"Play start: {checkedN} unit variant passes checked, {n} drawn, {compiledNow} compiled now, {sw.Elapsed.TotalMilliseconds:0} ms{(capped ? " (stopped at the cap: the rest compile in the background)" : "")}{(cancelled ? " (cancelled)" : "")}";
            LastLine = line;
            if (compiledNow > 0 || capped || cancelled || sw.Elapsed.TotalMilliseconds > 300.0) Debug.Log("[OpenZ Shaders] " + line);
        }

        // ------------------------------------------------------------------------------------------------ the list
        /// <summary>The keyword sets the pipeline sets at run time, with "likely" = the set the game shows most (PC look: cascaded sun shadows,
        /// soft, SSAO). All three renderers (Settings/PC_Renderer, Quality/OZ_Renderer_Medium and _Low) are Forward+: the cluster light loop is
        /// always on and the per-pixel / per-vertex additional-light keywords never are; shadows use per-light soft quality, so only
        /// _SHADOWS_SOFT (never the LOW / MEDIUM / HIGH ones); the sun has cascades (PC, Medium, the lite presets), one map (Low, a lite preset)
        /// or no shadow; SSAO is on or off (test mode); fog is off (menus, Unit editor) or linear (every level); additional-light shadows
        /// (spot / point lights with shadows) on or off. The first sets are the Unit editor's and a level's usual ones.</summary>
        public static List<(string[] kw, bool likely)> GlobalSets()
        {
            var o = new List<(string[], bool)>();
            foreach (var main in new[] { "_MAIN_LIGHT_SHADOWS_CASCADE", "", "_MAIN_LIGHT_SHADOWS" })
                foreach (bool ao in new[] { true, false })
                    foreach (bool soft in new[] { true, false })
                        foreach (bool add in new[] { false, true })
                            foreach (var fog in new[] { "", "FOG_LINEAR" })
                            {
                                if (soft && main.Length == 0 && !add) continue;   // soft shadows need a shadow
                                var k = new List<string> { "_CLUSTER_LIGHT_LOOP" };
                                if (main.Length > 0) k.Add(main);
                                if (ao) k.Add("_SCREEN_SPACE_OCCLUSION");
                                if (soft) k.Add("_SHADOWS_SOFT");
                                if (add) k.Add("_ADDITIONAL_LIGHT_SHADOWS");
                                if (fog.Length > 0) k.Add(fog);
                                o.Add((k.ToArray(), main == "_MAIN_LIGHT_SHADOWS_CASCADE" && ao && soft));
                            }
            return o;
        }

        static readonly Regex ShaderRef = new Regex(@"m_Shader: \{fileID: -?\d+, guid: ([0-9a-f]{32})");
        static readonly Regex OldKeywords = new Regex(@"^\s*m_ShaderKeywords: ?(.*)$", RegexOptions.Multiline);

        /// <summary>The game shaders (under Assets/OpenZ) with the local keyword sets their materials use (read from the .mat text: no
        /// material or texture is loaded).</summary>
        static Dictionary<Shader, HashSet<string>> GameShaders()
        {
            var sets = new Dictionary<Shader, HashSet<string>>();
            void Add(Shader s, string localKw)
            {
                if (!s) return;
                string p = AssetDatabase.GetAssetPath(s);
                if (string.IsNullOrEmpty(p) || !p.StartsWith("Assets/OpenZ/")) return;
                if (!sets.TryGetValue(s, out var h)) sets[s] = h = new HashSet<string> { "" };
                if (localKw != null) h.Add(localKw);
            }
            string root = Path.Combine(Application.dataPath, "OpenZ");
            foreach (var f in Directory.GetFiles(root, "*.mat", SearchOption.AllDirectories))
            {
                string t;
                try { t = File.ReadAllText(f); } catch (IOException) { continue; }
                var m = ShaderRef.Match(t);
                if (!m.Success) continue;
                string sp = AssetDatabase.GUIDToAssetPath(m.Groups[1].Value);
                if (string.IsNullOrEmpty(sp) || !sp.StartsWith("Assets/OpenZ/")) continue;
                var s = AssetDatabase.LoadAssetAtPath<Shader>(sp);
                Add(s, string.Join(" ", MaterialKeywords(t).OrderBy(k => k, StringComparer.Ordinal)));
            }
            foreach (var f in Directory.GetFiles(root, "*.shader", SearchOption.AllDirectories))
            {
                string rel = "Assets" + f.Substring(Application.dataPath.Length).Replace('\\', '/');
                if (rel.Contains("/Resources/")) Add(AssetDatabase.LoadAssetAtPath<Shader>(rel), null);
            }
            var gs = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
            if (gs != null && gs.Length > 0 && gs[0])
            {
                var arr = new SerializedObject(gs[0]).FindProperty("m_AlwaysIncludedShaders");
                if (arr != null && arr.isArray)
                    for (int i = 0; i < arr.arraySize; i++) Add(arr.GetArrayElementAtIndex(i).objectReferenceValue as Shader, null);
            }
            foreach (var n in UnitShaders) Add(Shader.Find(n), null);
            return sets;
        }

        static IEnumerable<string> MaterialKeywords(string matText)
        {
            int i = matText.IndexOf("m_ValidKeywords:", StringComparison.Ordinal);
            if (i >= 0)
            {
                int eol = matText.IndexOf('\n', i);
                string head = eol < 0 ? matText.Substring(i) : matText.Substring(i, eol - i);
                if (head.Contains("[]") || eol < 0) yield break;
                foreach (var raw in matText.Substring(eol + 1).Split('\n'))
                {
                    string l = raw.TrimEnd('\r');
                    if (!l.TrimStart().StartsWith("- ")) break;
                    yield return l.TrimStart().Substring(2).Trim();
                }
                yield break;
            }
            var old = OldKeywords.Match(matText);
            if (old.Success)
                foreach (var k in old.Groups[1].Value.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)) yield return k;
        }

        /// <summary>The active SubShader's index (the one whose passes are the shader's passes, tag for tag), for the pass keyword queries.</summary>
        static int ActiveSub(Shader s)
        {
            int n = s.passCount;
            for (int sub = 0; sub < s.subshaderCount; sub++)
            {
                if (s.GetPassCountInSubshader(sub) != n) continue;
                bool same = true;
                for (int p = 0; p < n && same; p++) same = s.FindPassTagValue(sub, p, LightMode).name == s.FindPassTagValue(p, LightMode).name;
                if (same) return sub;
            }
            return 0;
        }

        static bool PassTypeOf(Shader s, int p, out PassType t)
        {
            string lm = s.FindPassTagValue(p, LightMode).name ?? "";
            t = PassType.ScriptableRenderPipeline;
            if (Array.IndexOf(SkipModes, lm) >= 0) return false;
            if (lm == "ShadowCaster") t = PassType.ShadowCaster;
            else if (lm == "MotionVectors") t = PassType.MotionVectors;
            else if (lm.Length == 0 || lm == "SRPDefaultUnlit") t = PassType.ScriptableRenderPipelineDefaultUnlit;
            return true;
        }

        sealed class ShaderInfo
        {
            public HashSet<string>[] passKw;     // per pass: every keyword the pass declares
            public bool[] passOk;
            public PassType[] passType;
            public HashSet<string> local;        // keywords the shader declares local (set on the material)
            public bool instancing;              // the shader declares INSTANCING_ON somewhere
            public bool passReportsInstancing;   // a pass's own keyword list names INSTANCING_ON (then each pass is trusted on it)
        }

        static ShaderInfo Info(Shader s)
        {
            var info = new ShaderInfo { passKw = new HashSet<string>[s.passCount], passOk = new bool[s.passCount], passType = new PassType[s.passCount], local = new HashSet<string>() };
            foreach (var lk in s.keywordSpace.keywords)
            {
                if (lk.name == Instancing) info.instancing = true;
                else if (!lk.isOverridable && lk.type == ShaderKeywordType.UserDefined) info.local.Add(lk.name);
            }
            int sub = ActiveSub(s);
            for (int p = 0; p < s.passCount; p++)
            {
                info.passOk[p] = PassTypeOf(s, p, out info.passType[p]);
                var pid = new PassIdentifier((uint)sub, (uint)p);
                var h = new HashSet<string>();
                foreach (var lk in ShaderUtil.GetPassKeywords(s, pid)) h.Add(lk.name);
                if (h.Contains(Instancing)) info.instancing = info.passReportsInstancing = true;
                info.passKw[p] = h;
            }
            if (s.passCount > 0 && info.passKw.All(h => h.Count == 0) && s.keywordSpace.keywordCount > 0)   // no pass lists any keyword: every pass takes the shader's
                for (int p = 0; p < s.passCount; p++) info.passKw[p] = new HashSet<string>(s.keywordSpace.keywordNames);
            return info;
        }

        /// <summary>Whether the pass has an instanced variant: its own keyword list says so, or (when no pass list names the built-in
        /// keyword at all) the shader declares it and the pass is a lit / depth / shadow pass.</summary>
        static bool PassInstanced(ShaderInfo info, int p) => info.passKw[p].Contains(Instancing)
            || (!info.passReportsInstancing && info.instancing && info.passType[p] != PassType.ScriptableRenderPipelineDefaultUnlit);

        static void AddItem(List<Item> list, HashSet<string> keys, Shader s, ShaderInfo info, int p, IEnumerable<string> kw, bool inst, int rank)
        {
            var pk = info.passKw[p];
            var g = new List<string>(); var l = new List<string>();
            foreach (var k in kw.Distinct())
            {
                if (k == Instancing || k.Length == 0 || !pk.Contains(k)) continue;
                if (info.local.Contains(k)) l.Add(k); else g.Add(k);
            }
            g.Sort(StringComparer.Ordinal); l.Sort(StringComparer.Ordinal);
            string key = s.name + "|" + p + "|" + string.Join(" ", g) + "|" + string.Join(" ", l) + "|" + (inst ? "i" : "");
            if (!keys.Add(key)) return;
            list.Add(new Item { shader = s, pass = p, type = info.passType[p], globals = g.ToArray(), locals = l.ToArray(), inst = inst, rank = rank, key = key });
        }

        /// <summary>Makes the list (and writes the collection when it gained variants and <paramref name="write"/>): the hidden draws, in
        /// warm-up order.</summary>
        static List<Item> Build(bool write)
        {
            var list = new List<Item>(); var keys = new HashSet<string>();
            var infos = new Dictionary<Shader, ShaderInfo>();
            ShaderInfo InfoOf(Shader s)
            {
                if (!infos.TryGetValue(s, out var i)) infos[s] = i = Info(s);
                return i;
            }
            // seen in Play (rank 0): every pass of the entry's pass type, each with its own keywords
            var seen = AssetDatabase.LoadAssetAtPath<ShaderVariantCollection>(SeenPath);
            if (seen)
                foreach (var (s, t, kw) in Entries(seen))
                {
                    if (!Wanted(s) || ShaderUtil.ShaderHasError(s)) continue;
                    var info = InfoOf(s);
                    bool inst = kw.Contains(Instancing);
                    for (int p = 0; p < s.passCount; p++)
                        if (info.passOk[p] && info.passType[p] == t && (!inst || PassInstanced(info, p))) AddItem(list, keys, s, info, p, kw, inst, 0);
                }
            // every game shader x the pipeline's sets x its materials' local sets (ranks 1 - 4)
            var game = GameShaders();
            var sets = GlobalSets();
            var unit = new HashSet<string>(UnitShaders);
            var order = game.Keys.OrderBy(s => unit.Contains(s.name) ? Array.IndexOf(UnitShaders, s.name) : 1000).ThenBy(s => s.name, StringComparer.Ordinal).ToList();
            foreach (bool likelyPass in new[] { true, false })
                foreach (var s in order)
                {
                    if (ShaderUtil.ShaderHasError(s)) continue;
                    var info = InfoOf(s);
                    bool u = unit.Contains(s.name);
                    int rank = likelyPass ? (u ? 1 : 3) : (u ? 2 : 4);
                    foreach (var localSet in game[s])
                    {
                        var lk = localSet.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var (g, likely) in sets)
                        {
                            if (likely != likelyPass) continue;
                            for (int p = 0; p < s.passCount; p++)
                            {
                                if (!info.passOk[p]) continue;
                                var kw = g.Concat(lk).ToList();
                                var extra = info.passKw[p].Contains(Punctual) ? new[] { false, true } : new[] { false };
                                foreach (bool punct in extra)
                                {
                                    var k2 = punct ? kw.Concat(new[] { Punctual }).ToList() : kw;
                                    foreach (bool inst in PassInstanced(info, p) ? new[] { true, false } : new[] { false })
                                        AddItem(list, keys, s, info, p, k2, inst, rank);
                                }
                            }
                        }
                    }
                }
            var sorted = list.OrderBy(i => i.rank).ToList();   // stable: keeps the order inside a rank
            if (write && !EditorApplication.isPlayingOrWillChangePlaymode) WriteList(sorted);
            return sorted;
        }

        static bool Wanted(Shader s)
        {
            if (!s) return false;
            string p = AssetDatabase.GetAssetPath(s) ?? "";
            if (p.StartsWith("Assets/OpenZ/")) return true;
            if (p.StartsWith("Packages/com.unity.render-pipelines.universal/") && !s.name.StartsWith("Hidden/")) return true;
            return s.name == "UI/Default";
        }

        static IEnumerable<(Shader s, PassType t, string[] kw)> Entries(ShaderVariantCollection c)
        {
            var arr = new SerializedObject(c).FindProperty("m_Shaders");
            if (arr == null || !arr.isArray) yield break;
            for (int i = 0; i < arr.arraySize; i++)
            {
                var e = arr.GetArrayElementAtIndex(i);
                var s = e.FindPropertyRelative("first")?.objectReferenceValue as Shader;
                var vs = e.FindPropertyRelative("second.variants");
                if (!s || vs == null || !vs.isArray) continue;
                for (int j = 0; j < vs.arraySize; j++)
                {
                    var v = vs.GetArrayElementAtIndex(j);
                    var kw = v.FindPropertyRelative("keywords")?.stringValue ?? "";
                    var pt = v.FindPropertyRelative("passType")?.intValue ?? 0;
                    yield return (s, (PassType)pt, kw.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
                }
            }
        }

        /// <summary>Adds every listed variant the collection has not got yet (one entry per pass type and keyword set; never removes one).</summary>
        static void WriteList(List<Item> items)
        {
            var svc = AssetDatabase.LoadAssetAtPath<ShaderVariantCollection>(ListPath);
            if (!svc)
            {
                string dir = Path.GetDirectoryName(ListPath).Replace('\\', '/');
                if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder("Assets/OpenZ/Resources/OpenZ", "Shaders");
                svc = new ShaderVariantCollection();
                AssetDatabase.CreateAsset(svc, ListPath);
            }
            int added = 0, refused = 0;
            var done = new HashSet<string>();
            foreach (var it in items)
            {
                var kw = it.globals.Concat(it.locals).Concat(it.inst ? new[] { Instancing } : new string[0]).OrderBy(k => k, StringComparer.Ordinal).ToArray();
                if (!done.Add(it.shader.name + "|" + (int)it.type + "|" + string.Join(" ", kw))) continue;
                try { if (svc.Add(new ShaderVariantCollection.ShaderVariant(it.shader, it.type, kw))) added++; }
                catch (ArgumentException) { refused++; }
            }
            if (added > 0)
            {
                EditorUtility.SetDirty(svc);
                AssetDatabase.SaveAssetIfDirty(svc);
                Debug.Log($"[OpenZ Shaders] warm-up list: {added} variants added ({svc.variantCount} in {svc.shaderCount} shaders; {refused} sets a pass type does not have were left out): {ListPath}");
            }
        }

        /// <summary>After Play: the variants the editor saw drawn (all shaders) go to <see cref="SeenPath"/>, and the game's ones join the list.</summary>
        static void MergeSeen()
        {
            try
            {
                var countM = typeof(ShaderUtil).GetMethod("GetCurrentShaderVariantCollectionVariantCount", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                var saveM = typeof(ShaderUtil).GetMethod("SaveCurrentShaderVariantCollection", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public, null, new[] { typeof(string) }, null);
                if (countM == null || saveM == null) { Debug.LogWarning("[OpenZ Shaders] the editor's list of drawn variants cannot be read here: the warm-up keeps its own list only"); return; }
                int n = Convert.ToInt32(countM.Invoke(null, null));
                if (n <= SessionState.GetInt(SeenCountKey, 0)) return;
                SessionState.SetInt(SeenCountKey, n);
                saveM.Invoke(null, new object[] { SeenPath });
                AssetDatabase.ImportAsset(SeenPath);
                Schedule("variants seen in Play");
            }
            catch (Exception e) { Debug.LogWarning("[OpenZ Shaders] reading the drawn variants failed: " + e.Message); }
        }

        static string Signature(List<Item> items)
        {
            var sb = new StringBuilder();
            foreach (var s in items.Select(i => i.shader).Distinct().OrderBy(s => s.name, StringComparer.Ordinal))
                sb.Append(s.name).Append('=').Append(AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(s)).ToString()).Append(';');
            sb.Append(items.Count);
            foreach (var i in items) sb.Append(i.key).Append(',');
            return Hash128.Compute(sb.ToString()).ToString();
        }

        // ------------------------------------------------------------------------------------------------ the hidden draw
        static bool Prepare(Item it)
        {
            if (!it.shader) return false;
            if (!tri)
            {
                tri = new Mesh { name = "OzShaderWarmTri", hideFlags = HideFlags.HideAndDontSave };
                tri.vertices = new[] { new Vector3(0f, 0f, 0.5f), new Vector3(0f, 1f, 0.5f), new Vector3(1f, 0f, 0.5f) };
                tri.normals = new[] { Vector3.back, Vector3.back, Vector3.back };
                tri.tangents = new[] { new Vector4(1f, 0f, 0f, 1f), new Vector4(1f, 0f, 0f, 1f), new Vector4(1f, 0f, 0f, 1f) };
                tri.colors = new[] { Color.white, Color.white, Color.white };
                var uv = new[] { Vector2.zero, Vector2.up, Vector2.right };
                tri.uv = uv; tri.uv2 = uv; tri.uv3 = uv; tri.uv4 = uv;
                tri.triangles = new[] { 0, 1, 2 };
            }
            if (!target)
            {
                target = new RenderTexture(4, 4, 24, RenderTextureFormat.ARGB32) { name = "OzShaderWarmTarget", hideFlags = HideFlags.HideAndDontSave };
                target.Create();
            }
            if (cmd == null) cmd = new CommandBuffer { name = "OpenZ shader warm-up" };
            if (!mats.TryGetValue(it.shader, out var m) || !m)
                mats[it.shader] = m = new Material(it.shader) { name = "OzShaderWarm " + it.shader.name, hideFlags = HideFlags.HideAndDontSave };
            m.shaderKeywords = it.locals;
            m.enableInstancing = it.inst;
            foreach (var g in Universe)
                if (Array.IndexOf(it.globals, g) >= 0) Shader.EnableKeyword(g); else Shader.DisableKeyword(g);
            foreach (var g in it.globals)
                if (!Universe.Contains(g)) { Universe.Add(g); SaveOne(g); Shader.EnableKeyword(g); }
            return true;
        }

        /// <summary>Every global keyword the hidden draws switch (saved before and put back after each batch of draws).</summary>
        static readonly HashSet<string> Universe = new HashSet<string>
        {
            "_CLUSTER_LIGHT_LOOP", "_MAIN_LIGHT_SHADOWS", "_MAIN_LIGHT_SHADOWS_CASCADE", "_MAIN_LIGHT_SHADOWS_SCREEN", "_SCREEN_SPACE_OCCLUSION",
            "_SHADOWS_SOFT", "_SHADOWS_SOFT_LOW", "_SHADOWS_SOFT_MEDIUM", "_SHADOWS_SOFT_HIGH", "_ADDITIONAL_LIGHT_SHADOWS", "_ADDITIONAL_LIGHTS",
            "_ADDITIONAL_LIGHTS_VERTEX", "FOG_LINEAR", "FOG_EXP", "FOG_EXP2", Punctual,
        };

        static void Draw(Item it, bool sync)
        {
            var m = mats[it.shader];
            cmd.Clear();
            ShaderUtil.SetAsyncCompilation(cmd, !sync);
            cmd.SetRenderTarget(target);
            if (it.inst) cmd.DrawMeshInstanced(tri, 0, m, it.pass, one, 1);
            else cmd.DrawMesh(tri, Matrix4x4.identity, m, 0, it.pass);
            ShaderUtil.RestoreAsyncCompilation(cmd);
            Graphics.ExecuteCommandBuffer(cmd);
        }

        static void Save()
        {
            saved.Clear();
            foreach (var g in Universe) saved[g] = Shader.IsKeywordEnabled(g);
        }

        static void SaveOne(string g) { if (!saved.ContainsKey(g)) saved[g] = Shader.IsKeywordEnabled(g); }

        static void Restore()
        {
            foreach (var kv in saved)
                if (kv.Value) Shader.EnableKeyword(kv.Key); else Shader.DisableKeyword(kv.Key);
            saved.Clear();
        }

        static void Cleanup()
        {
            Stop();
            foreach (var m in mats.Values) if (m) UnityEngine.Object.DestroyImmediate(m);
            mats.Clear();
            if (tri) UnityEngine.Object.DestroyImmediate(tri);
            if (target) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
            cmd?.Release(); cmd = null;
        }

        // ------------------------------------------------------------------------------------------------ report + menus
        static void WriteReport(List<Item> items, string headline)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("OpenZ shader warm-up report");
                sb.AppendLine(headline);
                sb.AppendLine("collection: " + ListPath + " (" + (AssetDatabase.LoadAssetAtPath<ShaderVariantCollection>(ListPath) is ShaderVariantCollection c && c ? c.variantCount + " variants in " + c.shaderCount + " shaders" : "MISSING") + ")");
                sb.AppendLine("shader | variant passes | rank 0-1 | instanced | not instanced compiled now / of");
                Save();
                try
                {
                    foreach (var grp in items.GroupBy(i => i.shader))
                    {
                        int all = 0, first = 0, inst = 0, comp = 0, plain = 0;
                        foreach (var it in grp)
                        {
                            all++; if (it.rank <= 1) first++;
                            if (it.inst) { inst++; continue; }
                            plain++;
                            if (Prepare(it) && ShaderUtil.IsPassCompiled(mats[it.shader], it.pass)) comp++;
                        }
                        sb.AppendLine($"{grp.Key.name} | {all} | {first} | {inst} | {comp} / {plain}");
                    }
                }
                finally { Restore(); }
                Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
                File.WriteAllText(ReportPath, sb.ToString());
            }
            catch (Exception e) { Debug.LogWarning("[OpenZ Shaders] report not written: " + e.Message); }
        }

        [MenuItem("OpenZ/Shaders/Warm up now")]
        static void MenuWarm() { SessionState.EraseString(DoneKey); Schedule("menu"); }

        [MenuItem("OpenZ/Shaders/Warm-up report")]
        static void MenuReport()
        {
            var items = Build(false);
            WriteReport(items, "report (menu): " + items.Count + " variant passes; last run: " + (LastLine.Length > 0 ? LastLine : "none yet in this session"));
            Debug.Log("[OpenZ Shaders] report written: " + Path.GetFullPath(ReportPath));
        }

        [MenuItem("OpenZ/Shaders/Player warm-up test (in Play)")]
        static void MenuPlayerTest()
        {
            if (!EditorApplication.isPlaying) { Debug.Log("[OpenZ Shaders] the player warm-up test runs in Play"); return; }
            OpenZ.OzShaderWarm.StartNow();
        }
    }

    /// <summary>A game shader or material imported (a kit changed it): the warm-up runs again for it.</summary>
    public sealed class OzShaderWarmupImports : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            foreach (var p in imported)
            {
                if (!p.StartsWith("Assets/OpenZ/")) continue;
                if (p.EndsWith(".shader") || p.EndsWith(".hlsl") || p.EndsWith(".cginc") || p.EndsWith(".mat"))
                {
                    OzShaderWarmup.Schedule("import of " + Path.GetFileName(p));
                    return;
                }
            }
        }
    }
}
