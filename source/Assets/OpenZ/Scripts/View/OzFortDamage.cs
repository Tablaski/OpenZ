using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OpenZ
{
    /// <summary>
    /// 1.5 fort_damage: the fort shows how hurt it is. View only: the sim's fort health is read, nothing is written to the sim, no random draw
    /// of the sim, no change to any rule (deterministic-safe). Both fort types (front / back: one model, Z_fort's FortBase) and every team colour.
    /// <list type="bullet">
    /// <item><b>Damage D</b> = 1 - health / max health, eased (EaseSeconds) so a big jump plays as a quick sweep through the stages, never a cut.
    /// At full health (D = 0) nothing of this is drawn.</item>
    /// <item><b>Four stages</b> (health): 0 clean (100-90 %: only soot where hits land), 1 scorched + smoke (90-65 %: soot spreads, thin smoke,
    /// the paint darkens a little), 2 cracked + parts missing + fire (65-30 %: cracks, then breaches where parts fall off, fires in the breaches),
    /// 3 heavy ruin (30-0 %: more breaches, big pieces off the parapet, thick smoke, sparks, more fires). Every mark fades / grows in over about
    /// a second, the paint's char eases; nothing pops.</item>
    /// <item><b>Marks on the walls</b>: soot, cracks and breaches drawn by the game's own multiply decal shader (OpenZ/ScorchDecal) on grids
    /// that follow the real wall (Resources/OpenZ/FortV2/fort_damage_sites.json, measured off the model by the kit's
    /// tools/measure_fort_walls.py: 11 x 11 points a mark, 3.5 cm off the wall, faded at steps, cut where there is no wall). The atlas is
    /// made here at the first fort (OzFortDamage.Atlas.cs).</item>
    /// <item><b>Where</b>: hits steer it. A shell's blast (MissileExploded on the fort) soots the wall site nearest to it; a Pyro's flame
    /// (Hitscan on the fort) soots the low sites facing the Pyro; new cracks and breaches go to the sooted sites near the latest hits. With no
    /// hits (a test that sets the health) a fixed order seeded by the fort's id.</item>
    /// <item><b>Parts fall off</b> (OzFortDamage.Chunks.cs): at every new breach and at the parapet steps, modelled concrete chunks of the game's
    /// chunk library (ConcreteChunkLib, the fort death's own pieces) break out with dust, sparks and the crumble sound, fall with physics, knock
    /// on landing (dust + sound), and lie as rubble at the foot; a few chunks stay embedded round each breach as its broken edge.</item>
    /// <item><b>Sounds</b> (every visible action has one; all existing clips): crack = dst_structure_groan + dst_rock_chip; part breaks off =
    /// dst_rock_crumble_medium; a big parapet piece = dst_collapse_medium; landings = dst_debris_concrete (big: dst_rock_crash_large);
    /// a fire starts = OzSoundV3.Ignite, one dst_fire_loop per fort (not while OpenZDamageStates' own building loop plays); sparks =
    /// ngt_lamp_spark.</item>
    /// <item><b>Kept as it is</b>: OpenZDamageStates ZF24 (smoke from 50 %, the roof fire from 25 %), the Pyro's sticky fire and char, the
    /// fort's death (BuildingBreaker), the gate rig, the tower guns. The paint's char is only ever raised by this view (never lowered below
    /// what another system set). The fort falls: every mark goes at once (the death's blast covers it), the chunks pop out.</item>
    /// </list>
    /// Switch <see cref="Enabled"/> (code, on). Auto-boots in every level scene like FortAlarmBeacons.
    /// </summary>
    [DefaultExecutionOrder(45)]   // LateUpdate after AudioDirector (40) and before CombatView (50), which clears the sim's events
    public partial class OzFortDamage : MonoBehaviour
    {
        public static OzFortDamage I { get; private set; }
        public static bool Enabled = true;
        /// <summary>Set false (e.g. in a test) to add OzFortDamage by hand.</summary>
        public static bool AutoBoot = true;

        // ------------------------------------------------------------------ tuning (damage fraction D = 1 - health / max health)
        /// <summary>Where stages 1, 2 and 3 begin (health 90 %, 65 %, 30 %).</summary>
        public const float Stage1At = 0.05f, Stage2At = 0.22f, Stage3At = 0.60f;   // 1.5 last_small_fixes: readable early at the RTS camera (was 0.10 / 0.35 / 0.70)
        public static readonly string[] StageNames = { "clean", "scorched + smoke", "cracked + parts missing + fire", "heavy ruin" };
        /// <summary>D at which the 1st, 2nd ... crack, breach, parapet break and breach fire appear (each goes again Hysteresis below).</summary>
        public static readonly float[] CrackAt = { 0.06f, 0.11f, 0.16f, 0.21f, 0.28f, 0.36f, 0.45f, 0.56f, 0.68f, 0.82f };   /* 1.5 last_small_fixes: from 94 %, 4 by 75 % */   // 1.5 kit fort_damage_stronger: from 86 % health, 10 (was 7 from 65 %)
        public static readonly float[] BreachAt = { 0.22f, 0.32f, 0.42f, 0.50f, 0.58f, 0.67f, 0.76f, 0.85f, 0.93f };   /* 1.5 last_small_fixes: the first by 78 %, 4 by 50 % */   // 1.5 kit fort_damage_stronger: from 70 % health, 9 (was 6 from 56 %)
        public static readonly float[] ParapetAt = { 0.20f, 0.36f, 0.48f, 0.62f, 0.78f, 0.92f };   /* 1.5 last_small_fixes: the first broken parapet part by 80 % */   // 1.5 kit fort_damage_stronger: from 60 % health, 6 (was 3 from 29 %)
        public static readonly float[] FireAt = { 0.34f, 0.44f, 0.56f, 0.70f, 0.84f };   /* 1.5 last_small_fixes: the first fire by 66 %, 2 by 50 % */   // 1.5 kit fort_damage_stronger: from 62 % health, 5 (was 3 from 50 %)
        /// <summary>Wall sites sooted by the damage itself (besides the hits): 0 at Stage1At, SootSitesMax at D = 0.75.</summary>
        public static int SootSitesMax = 32;   // 1.5 kit fort_damage_stronger (was 14)
        public static float EaseSeconds = 0.9f, MarkFadeIn = 1.1f, CrackGrow = 1.4f, Hysteresis = 0.03f;
        /// <summary>The whole fort's paint char at D = 1 (TeamPaint.Charred; 0.16 = 12.5 % darker). Raised only, never lowered below another's.</summary>
        public static float CharMax = 0.55f;   // 1.5 kit fort_damage_stronger: much darker by the ruin stage (was 0.16)
        /// <summary>Soot a blast adds to its wall site (0 .. 1, x its damage / 5000 on top), a flame hit.</summary>
        public static float BlastSoot = 0.22f, BlastSootPer5000 = 0.25f, FlameSoot = 0.01f;   // 1.5 kit fort_damage_stronger (was 0.12 / 0.18 / 0.006)
        /// <summary>Recent hits steer new marks for this long (s).</summary>
        public static float HitMemory = 25f;

        /// <summary>Proof counters (the Play check and the Editor preview read them).</summary>
        public static int Bound, StageSteps, CracksMade, BreachesMade, ParapetBreaks, FiresLit, SparkBursts, HitsSeen, SoundsPlayed;
        public static string LastLine = "";

        // ------------------------------------------------------------------ sites (fort_damage_sites.json)
        [System.Serializable] internal sealed class SiteJ { public float[] p, n; public float top, yaw, good; public float[] off; public string a; }
        [System.Serializable] class SiteFile { public int version, vertex_count, grid; public string mesh; public float[] bounds_min, bounds_max; public float scale_m_per_unit, half_m, lift_m; public SiteJ[] sites; }
        static SiteFile file; static bool fileTried;

        internal sealed class Site
        {
            public int i; public Vector3 p, n, t; public float top; public SiteJ j;
            public float hitSoot, baseSoot, sootShown; public Mark soot, crack, breach; public bool baseOn; public float lastHit = -100f;
        }

        internal sealed class Mark
        {
            public GameObject go; public MeshRenderer r; public Mesh mesh; public int kind; public float shown, want, born; public Transform tr;
            public Color32[] cols; public byte[] baseA; public float[] rad; public float reveal = 2f, reveal0 = 2f;   // reveal: the crack / breach spreading out from the centre (grid radius)
        }

        internal sealed class Fort
        {
            public ZObject o; public GameObject view; public Transform baseT, root; public bool marks; public float d, target; public int stage, lastHp = -1;
            public Site[] sites; public System.Random rnd; public int[] order; public int orderNext;
            public readonly List<Site> cracks = new List<Site>(), breaches = new List<Site>();
            public int parapets, fires, baseSooted; public float nextSmoke, nextFire, nextSpark, nextChar, myChar; public int smokeRR, fireRR;
            public float nextColumn, nextTopFire; public int columnRR;   // 1.5 kit fort_damage_stronger: smoke columns, parapet-top fires (OzFortDamage.Stronger.cs)
            public TeamPaint tp; public bool loop, dead; public float scaleM = 10.53449f;
            public Vector3 hitLocal; public float hitAt = -100f; public readonly List<Rim> rims = new List<Rim>();
            public readonly List<Chunk> chunks = new List<Chunk>();
            public bool fell, ruin; public float fallTarget, ruinAt = -100f, hushUntil = -100f, nextSocket; public Vector3 socket; public MeshRenderer baseR;   // 1.5 ending_fort_ruin (OzFortDamage.Ruin.cs)
        }

        ZGame game; float nextScan; MaterialPropertyBlock mpb;
        readonly Dictionary<int, Fort> forts = new Dictionary<int, Fort>();
        static readonly int OpacityId = Shader.PropertyToID("_Opacity");

        // ------------------------------------------------------------------ boot
        static bool hooked;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Early()
        {
            Bound = StageSteps = CracksMade = BreachesMade = ParapetBreaks = FiresLit = SparkBursts = HitsSeen = SoundsPlayed = 0;
            if (!hooked) { hooked = true; SceneManager.sceneLoaded += (s, m) => TryAttach(); }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() => TryAttach();

        /// <summary>Level scenes only (a ZGame is present).</summary>
        public static void TryAttach()
        {
            if (!AutoBoot) return;
            var g = ZGame.I ? ZGame.I : FindFirstObjectByType<ZGame>();
            if (!g || FindFirstObjectByType<OzFortDamage>()) return;
            new GameObject("OzFortDamage").AddComponent<OzFortDamage>();
        }

        void Awake()
        {
            if (I && I != this) { Destroy(this); return; }
            I = this;
            mpb = new MaterialPropertyBlock();
            game = ZGame.I ? ZGame.I : FindFirstObjectByType<ZGame>();
            if (!game) { Debug.LogWarning("[OpenZ] fort damage: no ZGame"); enabled = false; }
        }

        void OnDestroy()
        {
            foreach (var f in forts.Values) Clear(f, false);
            if (I == this) I = null;
        }

        static bool LoadFile()
        {
            if (fileTried) return file != null && file.sites != null && file.sites.Length > 0;
            fileTried = true;
            var ta = Resources.Load<TextAsset>("OpenZ/FortV2/fort_damage_sites");
            if (!ta) { Debug.LogWarning("[OpenZ] fort damage: Resources/OpenZ/FortV2/fort_damage_sites missing (no wall marks)"); return false; }
            try { file = JsonUtility.FromJson<SiteFile>(ta.text); }
            catch (System.Exception e) { Debug.LogWarning("[OpenZ] fort damage: sites file unreadable: " + e.Message); file = null; }
            return file != null && file.sites != null && file.sites.Length > 0;
        }

        // ------------------------------------------------------------------ bind
        void Scan()
        {
            nextScan = Time.time + 1f;
            if (game.World == null) return;
            foreach (var o in game.World.Objects)
            {
                if (!o.IsFort || forts.ContainsKey(o.Id)) continue;
                if (!game.StaticViews.TryGetValue(o.Id, out var go) || !go) continue;
                Bind(o, go);
            }
            List<int> gone = null;
            foreach (var kv in forts) if (game.World.Get(kv.Key) == null || !kv.Value.view) (gone ??= new List<int>()).Add(kv.Key);   // the Unit editor took it away
            if (gone != null) foreach (var id in gone) { Clear(forts[id], false); forts.Remove(id); }
        }

        void Bind(ZObject o, GameObject go)
        {
            var f = new Fort { o = o, view = go, rnd = new System.Random(o.Id * 7919 + 17), tp = go.GetComponent<TeamPaint>() };
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) if (t.name == "FortBase") { f.baseT = t; break; }
            if (!f.baseT) { Debug.LogWarning($"[OpenZ] fort damage: {o.Type} #{o.Id} has no FortBase (no damage stages on it)"); forts[o.Id] = f; f.dead = true; return; }
            f.root = new GameObject($"OZ_FortDamage_{o.Id}").transform; f.root.SetParent(transform, false);
            f.root.SetPositionAndRotation(f.baseT.position, f.baseT.rotation); f.root.localScale = f.baseT.lossyScale;
            f.scaleM = Mathf.Max(0.01f, f.baseT.lossyScale.x);
            var sites = new List<Site>();
            if (LoadFile())
            {
                var mf = f.baseT.GetComponent<MeshFilter>(); var m = mf ? mf.sharedMesh : null;
                f.marks = m && m.vertexCount == file.vertex_count && Close(m.bounds.min, file.bounds_min) && Close(m.bounds.max, file.bounds_max);
                if (!f.marks) Debug.LogWarning($"[OpenZ] fort damage: {o.Type} #{o.Id}: the fort model is not the one the wall sites were measured on " +
                                               $"({(m ? m.vertexCount : 0)} vertices, file {file.vertex_count}): no wall marks; smoke, fire and falling parts stay");
                for (int i = 0; i < file.sites.Length; i++)
                {
                    var j = file.sites[i]; if (j.p == null || j.n == null || j.p.Length < 3 || j.n.Length < 3) continue;
                    var n = new Vector3(j.n[0], 0f, j.n[2]).normalized;
                    sites.Add(new Site { i = sites.Count, j = j, p = new Vector3(j.p[0], j.p[1], j.p[2]), n = n, t = Vector3.Cross(Vector3.up, n).normalized, top = j.top });
                }
            }
            f.sites = sites.ToArray();
            f.order = new int[f.sites.Length]; for (int i = 0; i < f.order.Length; i++) f.order[i] = i;
            for (int i = f.order.Length - 1; i > 0; i--) { int k = f.rnd.Next(i + 1); (f.order[i], f.order[k]) = (f.order[k], f.order[i]); }
            forts[o.Id] = f; Bound++;
            if (f.marks) { EnsureAtlas(); f.marks = AtlasReady; }
            ConcreteChunkLib.Load();                                                                              // the falling parts' library now, not at the first breach
            Debug.Log($"[OpenZ] fort damage: {o.Type} #{o.Id} ({o.Owner}) bound, {f.sites.Length} wall sites, marks {(f.marks ? "on" : "off")}, health {o.Health}/{o.MaxHealth}");
        }

        static bool Close(Vector3 a, float[] b) => b != null && b.Length >= 3 && Mathf.Abs(a.x - b[0]) < 0.003f && Mathf.Abs(a.y - b[1]) < 0.003f && Mathf.Abs(a.z - b[2]) < 0.003f;

        // ------------------------------------------------------------------ tick
        void LateUpdate()
        {
            if (!Enabled || game == null || game.World == null) return;
            if (Time.time >= nextScan) Scan();
            ReadHits();
            float dt = Time.deltaTime;
            foreach (var f in forts.Values) if (!f.dead) Step(f, dt);
        }

        /// <summary>The sim's events of this frame (CombatView clears them after us): blasts and flame hits on a fort steer the soot.</summary>
        void ReadHits()
        {
            var ev = game.World.Events;
            for (int i = 0; i < ev.Count; i++)
            {
                var e = ev[i];
                if (e.type != ZEventType.MissileExploded && !(e.type == ZEventType.Hitscan && e.flag)) continue;
                if (!forts.TryGetValue(e.otherId, out var f) || f.dead || !f.root || f.sites.Length == 0) continue;
                if (e.type == ZEventType.MissileExploded && e.flag2) continue;                                    // debris of a death, not a shot
                var a = game.World.GetAny(e.objId);
                if (a == null || a.Owner == f.o.Owner) continue;
                HitsSeen++;
                if (e.type == ZEventType.MissileExploded)
                {
                    var w = ZGame.PxToWorld(e.x, e.y); var l = f.root.InverseTransformPoint(new Vector3(w.x, f.root.position.y, w.z));
                    var s = Nearest(f, l, false);
                    if (s != null) { s.hitSoot = Mathf.Min(1f, s.hitSoot + BlastSoot + BlastSootPer5000 * Mathf.Clamp01(e.value / 5000f)); s.lastHit = Time.time; }
                    f.hitLocal = l; f.hitAt = Time.time;
                }
                else
                {
                    var w = game.CenterWorld(a, false); var l = f.root.InverseTransformPoint(new Vector3(w.x, f.root.position.y, w.z));
                    var dir = new Vector3(l.x, 0f, l.z); if (dir.sqrMagnitude < 1e-6f) continue;
                    var s = Nearest(f, dir.normalized * 0.8f, true);                                              // the low wall facing the Pyro
                    if (s != null) { s.hitSoot = Mathf.Min(1f, s.hitSoot + FlameSoot); s.lastHit = Time.time; }
                    f.hitLocal = dir.normalized * 0.8f; f.hitAt = Time.time;
                }
            }
        }

        /// <summary>The wall site nearest to a fort-local point (x, z); low = a flame (the lower sites first).</summary>
        static Site Nearest(Fort f, Vector3 l, bool low)
        {
            Site best = null; float bd = float.MaxValue;
            foreach (var s in f.sites)
            {
                float dx = s.p.x - l.x, dz = s.p.z - l.z, d = dx * dx + dz * dz;
                d += low ? Mathf.Max(0f, s.p.y + 0.30f) * 0.25f : 0f;                                             // flames lick the lower wall
                if (d < bd) { bd = d; best = s; }
            }
            return best;
        }

        void Step(Fort f, float dt)
        {
            var o = f.o;
            if (FallStep(f)) return;
            if (o.Destroyed && !f.fell) { Clear(f, true); f.dead = true; LastLine = $"fort damage: {o.Type} #{o.Id} fell: marks gone, chunks out"; return; }
            bool shown = f.view && f.view.activeInHierarchy && f.root;
            f.target = f.fell ? FallTarget(f) : o.MaxHealth > 0 ? Mathf.Clamp01(1f - o.Health / (float)o.MaxHealth) : 0f;   // 1.5 ending_fort_ruin: fallen = held, ruined = 1
            float k = 1f - Mathf.Exp(-dt / Mathf.Max(0.05f, (f.ruin ? RuinEaseSeconds : EaseSeconds) * 0.5f));   // 1.5 ending_fort_ruin: the ruin eases in under the blast
            f.d = Mathf.Abs(f.target - f.d) < 0.0005f ? f.target : Mathf.Lerp(f.d, f.target, k);
            int stage = f.d >= Stage3At ? 3 : f.d >= Stage2At ? 2 : f.d >= Stage1At ? 1 : 0;
            if (stage != f.stage)
            {
                StageSteps++;
                LastLine = $"fort damage: {o.Type} #{o.Id} ({o.Owner}) stage {stage} {StageNames[stage]} at {o.Health}/{o.MaxHealth} ({(1f - f.target) * 100f:0} % health)";
                Debug.Log("[OpenZ] " + LastLine);
                f.stage = stage;
            }
            if (!shown) { SetMarksVisible(f, false); return; }
            SetMarksVisible(f, true);
            Grow(f);
            Soot(f, dt);
            Marks(f, dt);
            Char(f);
            Effects(f);
            Stronger(f);   // 1.5 kit fort_damage_stronger: smoke columns + fires on the broken parapet tops (OzFortDamage.Stronger.cs)
            RuinSocket(f);   // 1.5 ending_fort_ruin: the broken socket burns and smokes until the collapse (OzFortDamage.Ruin.cs)
            StepChunks(f, dt);
        }

        // ------------------------------------------------------------------ what grows with D
        void Grow(Fort f)
        {
            float d = f.d;
            // soot by the damage itself (a test that sets the health, or a fort hurt where no site took the hits)
            int wantSoot = d < Stage1At ? 0 : Mathf.RoundToInt(SootSitesMax * Mathf.Clamp01((d - Stage1At) / (0.45f - Stage1At)));   // 1.5 last_small_fixes: all sites by 55 % (was 25 %)
            while (f.baseSooted < wantSoot) { var s = Pick(f, 0); if (s == null) break; s.baseOn = true; f.baseSooted++; }
            while (f.baseSooted > wantSoot + 1) { Site last = null; foreach (var s in f.sites) if (s.baseOn) last = s; if (last == null) break; last.baseOn = false; f.baseSooted--; }
            // cracks, breaches, parapet breaks, fires: up past each step, back down past it less the hysteresis (a heal in a test)
            int wantCracks = Count(CrackAt, d, f.cracks.Count);
            while (f.cracks.Count < wantCracks) { var s = Pick(f, 1); if (s == null) break; AddCrack(f, s); }
            while (f.cracks.Count > wantCracks) { var s = f.cracks[f.cracks.Count - 1]; f.cracks.RemoveAt(f.cracks.Count - 1); Retire(s.crack); s.crack = null; }
            int wantBreaches = Count(BreachAt, d, f.breaches.Count);
            while (f.breaches.Count < wantBreaches) { var s = Pick(f, 2); if (s == null) break; AddBreach(f, s); }
            while (f.breaches.Count > wantBreaches)
            {
                var s = f.breaches[f.breaches.Count - 1]; f.breaches.RemoveAt(f.breaches.Count - 1); Retire(s.breach); s.breach = null; RimsOff(f, s);
                if (!f.cracks.Contains(s)) { Retire(s.crack); s.crack = null; }                                  // the crack the breach brought goes with it
            }
            int wantParapet = Count(ParapetAt, d, f.parapets);
            while (f.parapets < wantParapet) { ParapetBreak(f, f.parapets); f.parapets++; }
            if (f.parapets > wantParapet) f.parapets = wantParapet;                                              // fallen pieces lie where they fell
            int wantFires = Mathf.Min(f.breaches.Count, Count(FireAt, d, f.fires));
            if (wantFires > f.fires)
            {
                for (int i = f.fires; i < wantFires; i++) { var at = SiteWorld(f, f.breaches[i], 0.25f); if (!Hushed(f.o.Id)) { OzSoundV3.Ignite($"fdmg_{f.o.Id}_{i}", at, 0.6f); SoundsPlayed++; } FiresLit++; }   /* 1.5 ending_fort_ruin: hushed in the ruin sweep */
                f.nextFire = 0f;
            }
            f.fires = wantFires;
        }

        static int Count(float[] at, float d, int now)
        {
            int n = 0;
            for (int i = 0; i < at.Length; i++) if (d >= at[i] || (i < now && d >= at[i] - Hysteresis)) n = i + 1;
            return n;
        }

        /// <summary>The next wall site for kind 0 soot / 1 crack / 2 breach: near the latest hits when there are some, else the fort's own order.
        /// A crack prefers a sooted site, a breach a cracked one.</summary>
        Site Pick(Fort f, int kind)
        {
            if (f.sites.Length == 0) return null;
            bool recent = Time.time - f.hitAt < HitMemory;
            Site best = null; float bs = float.MaxValue;
            for (int r = 0; r < f.order.Length; r++)
            {
                var s = f.sites[f.order[r]];
                if (kind == 0 && s.baseOn) continue;
                if (kind == 1 && (s.crack != null || s.breach != null)) continue;
                if (kind == 2 && s.breach != null) continue;
                float score = r * 0.01f;                                                                         // the fort's own order breaks ties
                if (recent) { float dx = s.p.x - f.hitLocal.x, dz = s.p.z - f.hitLocal.z; score += Mathf.Sqrt(dx * dx + dz * dz) * 4f; }
                else score += r;
                if (kind == 1 && (s.hitSoot + (s.baseOn ? 0.5f : 0f)) > 0.05f) score -= 1.5f;
                if (kind == 2 && s.crack != null) score -= 2.5f;
                if (kind == 2) foreach (var b in f.breaches) if ((b.p - s.p).sqrMagnitude < Sq(2.4f / f.scaleM)) score += 3f;   // breaches spread out a little
                if (score < bs) { bs = score; best = s; }
            }
            return best;
        }

        static float Sq(float v) => v * v;

        // ------------------------------------------------------------------ marks
        void Soot(Fort f, float dt)
        {
            bool any = f.target > 0.0005f;                                                                       // full health: nothing at all
            foreach (var s in f.sites)
            {
                s.baseSoot = Mathf.MoveTowards(s.baseSoot, s.baseOn ? 0.75f + 0.25f * Mathf.Clamp01((f.d - Stage1At) * 2f) : 0f, dt / MarkFadeIn);
                if (!any) s.hitSoot = 0f;                                                                        // back to full health (a test): the old hit marks go
                float want = any ? Mathf.Max(s.hitSoot, s.baseSoot) : 0f;
                s.sootShown = Mathf.MoveTowards(s.sootShown, want, dt / MarkFadeIn);
                if (f.marks && s.sootShown > 0.004f && s.soot == null) s.soot = MakeMark(f, s, 0);
                if (s.soot != null) s.soot.want = s.sootShown;
            }
        }

        void AddCrack(Fort f, Site s)
        {
            f.cracks.Add(s); CracksMade++;
            if (f.marks) { s.crack = MakeMark(f, s, 2); s.crack.want = 1f; s.crack.reveal = s.crack.reveal0 = 0.15f; }
            var at = SiteWorld(f, s, 0.05f);
            Play("dst_structure_groan", at, f.o.Id, 0.5f, Random.Range(0.92f, 1.06f));
            Play("dst_rock_chip", at, f.o.Id, 0.55f, 1f);
            FxManager.I?.Dust(at, 0.35f);
            int chips = 2 + f.rnd.Next(2);
            for (int i = 0; i < chips; i++) Fall(f, s, "XS", 0.35f);
            LocalBlasts(f, s, at, 1, false);   // 1.5 kit view_notes11 N3: a local blast where the chips break off (OzFortDamage.Blasts.cs)
        }

        void AddBreach(Fort f, Site s)
        {
            f.breaches.Add(s); BreachesMade++;
            if (s.crack == null && f.marks) { s.crack = MakeMark(f, s, 2); s.crack.want = 1f; s.crack.reveal = s.crack.reveal0 = 0.3f; }
            if (f.marks) { s.breach = MakeMark(f, s, 3); s.breach.want = 1f; s.breach.reveal = s.breach.reveal0 = 0.35f; }
            var at = SiteWorld(f, s, 0.15f);
            Play("dst_rock_crumble_medium", at, f.o.Id, 0.8f, Random.Range(0.94f, 1.05f));
            var fx = FxManager.I; if (fx) { fx.Dust(at, 1.1f); fx.Dust(at + Vector3.up * 0.6f, 0.8f); }
            Sparks(at, SiteN(f, s), 8);
            Fall(f, s, f.d >= Stage3At ? "L" : f.d >= Stage2At + 0.1f ? "M" : "S", 0.25f);   // 1.5 kit fort_damage_stronger: a big piece at the ruin stage
            int n = 3 + f.rnd.Next(2); for (int i = 0; i < n; i++) Fall(f, s, "S", 0.45f);
            n = 4 + f.rnd.Next(3); for (int i = 0; i < n; i++) Fall(f, s, "XS", 0.6f);
            RimsOn(f, s);
            LocalBlasts(f, s, at, 3, false);   // 1.5 kit view_notes11 N3: local blasts where the parts break out (OzFortDamage.Blasts.cs)
            Debug.Log($"[OpenZ] fort damage: {f.o.Type} #{f.o.Id} breach {f.breaches.Count} at wall site {s.i} (yaw {s.j.yaw:0} deg), {f.o.Health}/{f.o.MaxHealth}");
        }

        Mark MakeMark(Fort f, Site s, int kind)
        {
            var mk = new Mark { kind = kind, born = Time.time };
            mk.go = new GameObject(kind == 0 ? "OZ_FortSoot" : kind == 2 ? "OZ_FortCrack" : "OZ_FortBreach");
            mk.tr = mk.go.transform; mk.tr.SetParent(f.root, false);
            mk.mesh = BuildMarkMesh(f, s, kind, out var centre);
            mk.tr.localPosition = centre;
            mk.cols = mk.mesh.colors32; mk.baseA = new byte[mk.cols.Length]; mk.rad = new float[mk.cols.Length];
            int G = Mathf.Max(2, file.grid);
            for (int q = 0; q < mk.cols.Length; q++)
            {
                mk.baseA[q] = mk.cols[q].a;
                float gu = (q % G) / (G - 1f) * 2f - 1f, gv = (q / G) / (G - 1f) * 2f - 1f; mk.rad[q] = Mathf.Sqrt(gu * gu + gv * gv);
            }
            mk.go.AddComponent<MeshFilter>().sharedMesh = mk.mesh;
            mk.r = mk.go.AddComponent<MeshRenderer>(); mk.r.sharedMaterial = atlasMat;
            mk.r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mk.r.receiveShadows = false;
            mk.r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off; mk.r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            mk.shown = 0f; Apply(mk);
            return mk;
        }

        /// <summary>The mark's grid: the site's 11 x 11 points (fort-local, relative to the site centre so a mark can grow from it), the atlas tile
        /// of its kind turned by a random angle, alpha = the site's point flag (1 on the wall, 2 on a step: 0) x the tile's edge; triangles that
        /// touch a point with no wall are left out.</summary>
        Mesh BuildMarkMesh(Fort f, Site s, int kind, out Vector3 centre)
        {
            int G = Mathf.Max(2, file.grid); float half = file.half_m / Mathf.Max(0.001f, file.scale_m_per_unit);
            centre = s.p;
            var v = new List<Vector3>(G * G); var uv = new List<Vector2>(G * G); var col = new List<Color32>(G * G); var tri = new List<int>((G - 1) * (G - 1) * 6);
            float ang = kind == 0 ? (float)(f.rnd.NextDouble() * 40.0 - 20.0) : (float)(f.rnd.NextDouble() * 360.0);      // soot rises: nearly upright
            float size = kind == 0 ? 0.80f + 0.25f * (float)f.rnd.NextDouble() : kind == 2 ? 0.85f + 0.2f * (float)f.rnd.NextDouble() : 0.62f + 0.12f * (float)f.rnd.NextDouble();
            int tile = kind == 0 ? (f.rnd.NextDouble() < 0.6 ? 0 : 1) : kind == 2 ? 2 : 3;
            float ca = Mathf.Cos(ang * Mathf.Deg2Rad), sa = Mathf.Sin(ang * Mathf.Deg2Rad);
            var u0 = new Vector2((tile % 2) * 0.5f, (tile / 2) * 0.5f);
            for (int j = 0; j < G; j++)
                for (int i = 0; i < G; i++)
                {
                    int q = j * G + i;
                    float gu = i / (G - 1f) * 2f - 1f, gv = j / (G - 1f) * 2f - 1f;
                    float off = s.j.off != null && q < s.j.off.Length ? s.j.off[q] : 0f;
                    v.Add(s.t * (gu * half) + Vector3.up * (gv * half) + s.n * off);
                    float tu = (ca * gu - sa * gv) / size, tv = (sa * gu + ca * gv) / size;                                   // the tile, turned and sized
                    float edge = Mathf.Clamp01((1f - Mathf.Max(Mathf.Abs(tu), Mathf.Abs(tv))) / 0.08f);
                    tu = Mathf.Clamp(tu, -0.98f, 0.98f); tv = Mathf.Clamp(tv, -0.98f, 0.98f);
                    uv.Add(u0 + new Vector2((tu * 0.5f + 0.5f) * 0.5f, (tv * 0.5f + 0.5f) * 0.5f));
                    char a = s.j.a != null && q < s.j.a.Length ? s.j.a[q] : '0';
                    col.Add(new Color32(255, 255, 255, (byte)Mathf.RoundToInt((a == '1' ? 1f : 0f) * edge * 255f)));
                }
            for (int j = 0; j < G - 1; j++)
                for (int i = 0; i < G - 1; i++)
                {   // (k0, k1, k3) and (k0, k3, k2): cross(t, up) = n, so both face out of the wall (the shader culls back faces)
                    int k0 = j * G + i, k1 = k0 + 1, k2 = k0 + G, k3 = k2 + 1;
                    if (Dropped(s, k0) || Dropped(s, k1) || Dropped(s, k2) || Dropped(s, k3)) continue;
                    tri.Add(k0); tri.Add(k1); tri.Add(k3); tri.Add(k0); tri.Add(k3); tri.Add(k2);
                }
            var m = new Mesh { name = "OZ_FortDamageMark" };
            m.SetVertices(v); m.SetUVs(0, uv); m.SetColors(col); m.SetTriangles(tri, 0);
            m.RecalculateBounds();
            return m;
        }

        static bool Dropped(Site s, int q) => s.j.a == null || q >= s.j.a.Length || s.j.a[q] == '0';

        void Marks(Fort f, float dt)
        {
            foreach (var s in f.sites)
            {
                StepMark(s.soot, dt, MarkFadeIn); StepMark(s.crack, dt, 0.5f); StepMark(s.breach, dt, 0.6f);
                if (s.soot != null && s.soot.want <= 0f && s.soot.shown <= 0f) { Retire(s.soot); s.soot = null; }
            }
        }

        void StepMark(Mark mk, float dt, float fade)
        {
            if (mk == null || !mk.go) return;
            float was = mk.shown;
            mk.shown = Mathf.MoveTowards(mk.shown, Mathf.Clamp01(mk.want), dt / Mathf.Max(0.05f, fade));
            if (mk.reveal < 1.6f && mk.cols != null)
            {   // a crack / breach spreads out of its centre over CrackGrow s (the wall-following points never move: nothing clips)
                mk.reveal = Mathf.Min(1.6f, mk.reveal + dt * (1.6f - mk.reveal0) / Mathf.Max(0.1f, CrackGrow));
                for (int q = 0; q < mk.cols.Length; q++) mk.cols[q].a = (byte)(mk.baseA[q] * Mathf.Clamp01((mk.reveal - mk.rad[q]) / 0.3f));
                if (mk.mesh) mk.mesh.colors32 = mk.cols;
            }
            if (!Mathf.Approximately(was, mk.shown)) Apply(mk);
        }

        void Apply(Mark mk)
        {
            if (!mk.r) return;
            mpb.Clear(); mpb.SetFloat(OpacityId, Mathf.SmoothStep(0f, 1f, mk.shown) /* 1.5 kit fort_damage_stronger: soot at full strength (was x 0.85) */); mk.r.SetPropertyBlock(mpb);
            mk.r.enabled = mk.shown > 0.002f;
        }

        void Retire(Mark mk)
        {
            if (mk == null) return;
            if (mk.mesh) Destroy(mk.mesh);
            if (mk.go) Destroy(mk.go);
        }

        void SetMarksVisible(Fort f, bool on)
        {
            if (f.root && f.root.gameObject.activeSelf != on) f.root.gameObject.SetActive(on);
            foreach (var r in f.rims) if (r.go && r.go.activeSelf != on) r.go.SetActive(on);
        }

        // ------------------------------------------------------------------ paint, smoke, fire, sparks
        void Char(Fort f)
        {
            if (!f.tp || Time.time < f.nextChar) return;
            f.nextChar = Time.time + 0.2f;
            var br = f.view ? f.view.GetComponent<BuildingBreaker>() : null; if (br && br.Collapsed) return;
            float want = CharMax * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((f.d - Stage1At) / (0.6f - Stage1At)));   // 1.5 last_small_fixes: full char by 40 % (was 5 %)
            if (want > f.tp.Charred + 0.008f) { f.tp.Charred = want; f.myChar = want; }                                 // raised: the paint darkens a little
            else if (f.myChar > 0f && Mathf.Abs(f.tp.Charred - f.myChar) < 0.002f && want < f.myChar - 0.008f) { f.tp.Charred = want; f.myChar = want; }   // our own char eases back (a heal in a test), never another's
        }

        void Effects(Fort f)
        {
            float now = Time.time, d = f.d;
            var px = OpenZFxPlayer.I; bool pxOn = px && px.isActiveAndEnabled; var fx = FxManager.I;
            if (f.stage >= 1 && now >= f.nextSmoke)
            {   // smoke from the sooted sites and the breaches: thin at stage 1, a column by the ruin stage
                f.nextSmoke = now + Mathf.Lerp(0.8f, 0.2f, Mathf.Clamp01((d - Stage1At) / 0.85f));   /* 1.5 kit fort_damage_stronger: was 1.1-0.28 s */
                Site s = NextSmoker(f);
                if (s != null && pxOn)
                {
                    var at = SiteWorld(f, s, 0.35f) + Vector3.up * 0.4f;
                    if (WreckFx.OnScreen(at, 0.1f)) px.WreckSmoke(at, Mathf.Lerp(1.4f, 3.6f, d) /* 1.5 kit fort_damage_stronger: was 0.9-2.4 */, s.breach != null && f.stage >= 2);
                }
            }
            if (f.fires > 0 && now >= f.nextFire)
            {   // the breach fires: every lit breach every 0.28 s (a flame lives 0.55 s: one at a time in turn left each fire dark for gaps)
                f.nextFire = now + 0.28f;
                for (int i = 0; i < f.fires && i < f.breaches.Count; i++)
                {
                    var at = SiteWorld(f, f.breaches[i], 0.25f);
                    if (fx && WreckFx.OnScreen(at, 0.1f)) fx.Burn(at, Mathf.Lerp(1.1f, 1.9f, d)) /* 1.5 kit fort_damage_stronger: was 0.7-1.1 */;
                }
            }
            bool zf24 = AudioDirector.I && AudioDirector.I.LoopActive("bldfire:" + f.o.Id);
            if (f.fires > 0 && !f.loop && !zf24 && AudioDirector.I && AudioDirector.Has("dst_fire_loop"))
            { AudioDirector.I.StartLoop("fortdmg:" + f.o.Id, "dst_fire_loop", SiteWorld(f, f.breaches[0], 0.25f), null, 0.5f); f.loop = true; SoundsPlayed++; }
            else if (f.loop && (f.fires == 0 || zf24)) { if (AudioDirector.I) AudioDirector.I.StopLoop("fortdmg:" + f.o.Id, 0.8f); f.loop = false; }
            if (f.stage >= 3 && f.breaches.Count > 0 && now >= f.nextSpark)
            {   // torn cables in the breaches spark now and then
                f.nextSpark = now + Random.Range(1.6f, 3.2f);
                var s = f.breaches[Random.Range(0, f.breaches.Count)];
                var at = SiteWorld(f, s, 0.1f);
                if (WreckFx.OnScreen(at, 0.1f)) { Sparks(at, SiteN(f, s), 10); Play("ngt_lamp_spark", at, f.o.Id, 0.35f, Random.Range(0.9f, 1.15f)); SparkBursts++; }
            }
        }

        Site NextSmoker(Fort f)
        {
            int n = f.sites.Length; if (n == 0) return null;
            for (int k = 0; k < n; k++)
            {
                var s = f.sites[(f.smokeRR++) % n];
                if (s.breach != null || s.sootShown > 0.45f) return s;
            }
            return null;
        }

        void Sparks(Vector3 at, Vector3 n, int count)
        {
            var px = OpenZFxPlayer.I; if (!px || !px.isActiveAndEnabled) return;
            for (int i = 0; i < count; i++)
                px.EmitChannel("leg_spark", at + Random.insideUnitSphere * 0.15f, n * Random.Range(1.2f, 3.2f) + Vector3.up * Random.Range(0.4f, 2.2f) + Random.insideUnitSphere * 0.8f,
                               Random.Range(0.35f, 0.7f), Random.Range(0.04f, 0.08f), new Color(1f, 0.72f, 0.36f), 1f);
        }

        // ------------------------------------------------------------------ helpers
        /// <summary>A site's centre on the wall in the world, `outM` metres out of it.</summary>
        Vector3 SiteWorld(Fort f, Site s, float outM) => f.root ? f.root.TransformPoint(s.p) + SiteN(f, s) * outM : f.view.transform.position;
        Vector3 SiteN(Fort f, Site s) => f.root ? f.root.TransformDirection(s.n).normalized : Vector3.up;

        void Play(string id, Vector3 at, int emitter, float vol, float pitch)
        {
            if (!AudioDirector.Has(id) || Hushed(emitter)) return;   // 1.5 ending_fort_ruin: the ruin sweep's sounds left to the blast's boom
            AudioDirector.Fire(id, at, emitter, vol, pitch); SoundsPlayed++;
        }

        /// <summary>The fort fell (or the view goes): every mark and embedded edge at once, the chunks pop out, the fire loop stops.</summary>
        void Clear(Fort f, bool pop)
        {
            if (f.loop && AudioDirector.I) AudioDirector.I.StopLoop("fortdmg:" + f.o.Id, 0.5f);
            f.loop = false;
            foreach (var s in f.sites ?? new Site[0]) { Retire(s.soot); Retire(s.crack); Retire(s.breach); s.soot = s.crack = s.breach = null; }
            foreach (var r in f.rims) if (r.go) { if (pop) PopOut.Begin(r.go.transform, PopOut.Style.Debris, null, true, false); else Destroy(r.go); }
            f.rims.Clear();
            foreach (var c in f.chunks) if (c) { if (pop) c.PopNow(); else Destroy(c.gameObject); }
            f.chunks.Clear();
            if (f.root) Destroy(f.root.gameObject);
            f.cracks.Clear(); f.breaches.Clear(); f.fires = 0;
        }

        // ------------------------------------------------------------------ reports (the Editor preview, the Play check)
        /// <summary>One line per bound fort.</summary>
        public static string Report()
        {
            if (!I) return "fort damage: not running (no level)";
            var sb = new System.Text.StringBuilder();
            sb.Append($"fort damage: {I.forts.Count} fort(s), stage steps {StageSteps}, cracks {CracksMade}, breaches {BreachesMade}, parapet breaks {ParapetBreaks}, fires {FiresLit}, " +
                      $"spark bursts {SparkBursts}, chunks {ChunksMade} (landed {ChunkLandings}), hits seen {HitsSeen}, sounds {SoundsPlayed}");
            foreach (var f in I.forts.Values)
            {
                int soot = 0; foreach (var s in f.sites ?? new Site[0]) if (s.soot != null && s.sootShown > 0.05f) soot++;
                sb.Append($"\n  {f.o.Type} #{f.o.Id} ({f.o.Owner}) {(f.dead ? "fallen / off" : "stage " + f.stage + " " + StageNames[f.stage])}: health {f.o.Health}/{f.o.MaxHealth}, " +
                          $"D {f.d:0.000} (target {f.target:0.000}), soot marks {soot}, cracks {f.cracks.Count}, breaches {f.breaches.Count}, parapet breaks {f.parapets}, fires {f.fires}, " +
                          $"rims {f.rims.Count}, chunks {f.chunks.Count}, char {(f.tp ? f.tp.Charred : 0f):0.000}, marks {(f.marks ? "on" : "off")}");
            }
            return sb.ToString();
        }

        /// <summary>The bound forts' ids (Editor preview).</summary>
        public static List<int> FortIds() { var l = new List<int>(); if (I) l.AddRange(I.forts.Keys); return l; }
        /// <summary>The eased damage of a fort, -1 when not bound (Editor preview: wait until the view caught up).</summary>
        public static float Shown(int id) => I && I.forts.TryGetValue(id, out var f) ? f.d : -1f;
    }
}
