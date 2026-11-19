using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// Robots drawn smaller than full size, view only, nothing of the rules changes.
    /// <list type="bullet">
    /// <item>The size comes from the Options row ROBOT SIZE (UI/OzRobotSizeOption.cs, saved, default 100 %), 100 to 60 % in fives
    /// (<see cref="Steps"/>, the vehicle size row's steps). Live: the row's click changes every robot on the next frame.</item>
    /// <item>What is drawn smaller: every robot view of both teams (grunt, psycho, tough, sniper, pyro, laser, the heroes, the fort defenders,
    /// Evil Zod) about its root, which is its feet on the ground, so it stays on the ground. Everything under the root follows by itself
    /// (body, gun, muzzle, ragdoll bones, the shadow it casts, a headshot's pieces, a squashed robot's remains). The figures BoardingView
    /// makes for a robot (walking to a vehicle and in, out again, the crew in a seat or hatch) are made at the same size: a clone of the
    /// robot's view is that size already, a new figure is scaled at its birth (BoardingView.SpawnFigure), an exit figure takes the size its
    /// robot will have (<see cref="ExitScaleX"/>).</item>
    /// <item>What follows the drawn size: the map's pick box, the selection frame and ring, the health bar (OzHeroSquadView.Tall), the aim
    /// point shots and hit sparks go to and the fallback muzzle (UnitView.AimPos / MuzzlePos, BoardingView.AimPoint), the water line of a
    /// wading robot (OpenZWading), the melt of a dying robot (UnitView Death.Melt, LavaMelt), the promotion pop's height (VeteranMarks).</item>
    /// <item>What stays: the rules (no rules file reads this class), the sim footprint, paths, speeds, hit chances, ranges and the view's unit
    /// spacing.</item>
    /// <item>At 100 % nothing is ever written: no robot is taken in, every reader returns exactly 1 (<see cref="Of(UnitView)"/>), so the game
    /// draws byte for byte as before. Going back to 100 % writes back the exact size the view had.</item>
    /// </list>
    /// Check: Editor/OzV15RobotSizeCheck.cs.
    /// </summary>
    [DefaultExecutionOrder(66)]   // after UnitView (60: its exit grow and fort-well writes), before BoardingView and FactoryView (70: they clone the drawn robot)
    public class OzRobotScale : MonoBehaviour
    {
        public const string Tag = "[OpenZ V1.5 robot size]";
        /// <summary>The drawn size of the steps (index 0 = full size), the same steps as the vehicle size row.</summary>
        public static readonly float[] Steps = { 1f, 0.95f, 0.9f, 0.85f, 0.8f, 0.75f, 0.7f, 0.65f, 0.6f };

        public static OzRobotScale I { get; private set; }
        /// <summary>The step now (0 = 100 %).</summary>
        public static int Step { get; private set; }
        public static float Factor => Steps[Step];
        public static int Percent => Mathf.RoundToInt(Factor * 100f);
        /// <summary>True while at least one drawn robot carries a scale other than 1. While false every reader returns 1 at once.</summary>
        public static bool AnyApplied { get; private set; }
        /// <summary>Check counters: scale writes to robot roots, robot views taken in, figures scaled at their birth, live steps of the row.</summary>
        public static int Writes, Taken, FiguresScaled, RowSteps;

        internal sealed class St { public bool known; public Vector3 baseScale, lastSet; public float applied = 1f; }
        static readonly Dictionary<UnitView, St> states = new Dictionary<UnitView, St>();
        static readonly List<UnitView> gone = new List<UnitView>();

        // ------------------------------------------------------------------ rules of the tool (pure: the check reads them)
        /// <summary>The step after a click: -1 = smaller (index up), +1 = bigger (index down), held at both ends.</summary>
        public static int StepAfter(int step, int dir) => Mathf.Clamp(step + (dir < 0 ? 1 : dir > 0 ? -1 : 0), 0, Steps.Length - 1);

        /// <summary>The scale a root gets: its own size exactly at 100 %.</summary>
        public static Vector3 Want(Vector3 baseScale, float k) => k == 1f ? baseScale : baseScale * k;

        /// <summary>Drawn at the robot size: a robot's view.</summary>
        public static bool Includes(UnitView v) => v && v.Obj != null && v.Obj.Kind == ZKind.Robot;

        /// <summary>The intake rule: a robot view is taken in only while the size is not 100 % and the view is shown.</summary>
        public static bool TakesIn(bool robot, bool active, float k) => robot && active && k != 1f;

        /// <summary>The step of a size in percent.</summary>
        public static int StepOf(int percent) { for (int i = 0; i < Steps.Length; i++) if (Mathf.RoundToInt(Steps[i] * 100f) == percent) return i; return 0; }

        // ------------------------------------------------------------------ readers (1 = no change)
        /// <summary>The drawn size this robot view carries now (1 = full size; always 1 at 100 %).</summary>
        public static float Of(UnitView v)
        {
            if (!AnyApplied || !v) return 1f;
            return states.TryGetValue(v, out var s) ? s.applied : 1f;
        }

        /// <summary>The same for a robot of the rules (its view).</summary>
        public static float Of(ZObject o)
        {
            if (!AnyApplied || o == null) return 1f;
            var g = ZGame.I;
            return g && g.Views.TryGetValue(o.Id, out var v) ? Of(v) : 1f;
        }

        /// <summary>x times the drawn size of robot o (x itself, untouched, at 100 %).</summary>
        public static float Times(float x, ZObject o) { if (!AnyApplied) return x; float k = Of(o); return k == 1f ? x : x * k; }

        /// <summary>A dying robot's own absolute scale (a melt): times its drawn size, so it melts from the size it had (s itself at 100 %).
        /// The tool leaves a dying view's own writes as they are.</summary>
        public static Vector3 Dying(UnitView v, Vector3 s) { float k = Of(v); return k == 1f ? s : s * k; }

        /// <summary>A share of a dying robot's scale made with <see cref="Dying"/>, without its drawn size (y itself at 100 %).</summary>
        public static float Unscaled(UnitView v, float y) { float k = Of(v); return k == 1f ? y : y / k; }

        /// <summary>The root's x scale this robot view is drawn at from this frame on: a view the tool has not taken in yet (made this frame,
        /// e.g. the robot that leaves a vehicle) gets the row's size on its first frame. Its own x scale at 100 %.</summary>
        public static float ExitScaleX(UnitView v)
        {
            if (!v) return 1f;
            float cur = v.transform.localScale.x, k = Factor;
            if (states.TryGetValue(v, out var s) && s.known) { float b = s.applied == 1f ? cur : s.baseScale.x; return k == 1f ? b : b * k; }
            return k == 1f ? cur : cur * k;
        }

        /// <summary>BoardingView.SpawnFigure: a new figure of a robot (a crew at the start of a level, a body thrown out by a snipe) is
        /// made at the row's size (nothing written at 100 %).</summary>
        public static void NewFigure(GameObject go)
        {
            float k = Factor;
            if (k == 1f || !go) return;
            go.transform.localScale = go.transform.localScale * k; FiguresScaled++;
        }

        // ------------------------------------------------------------------ life
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { I = null; AnyApplied = false; states.Clear(); Writes = Taken = FiguresScaled = RowSteps = 0; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (I) return;
            Step = StepOf(OzRobotSizeOption.Saved);
            var go = new GameObject("OzRobotScale"); DontDestroyOnLoad(go);
            I = go.AddComponent<OzRobotScale>();
        }

        void OnDestroy() { if (I == this) I = null; }

        /// <summary>The Options row: one step (-1 = smaller, +1 = bigger), live, saved. The steps stop at the ends.</summary>
        public static void StepQuiet(int dir)
        {
            int was = Step; Step = StepAfter(Step, dir); RowSteps++;
            OzRobotSizeOption.Save(Percent);
            if (Step != was) Debug.Log($"{Tag} robot size {Percent} % (oz_robot_size, saved, live)");
        }

        /// <summary>Check only: puts the step without the row (nothing saved).</summary>
        public static void SetStepForCheck(int step) => Step = Mathf.Clamp(step, 0, Steps.Length - 1);

        void LateUpdate()
        {
            float k = Factor;
            var g = ZGame.I;
            if (k != 1f && g) foreach (var kv in g.Views) { var v = kv.Value; if (TakesIn(Includes(v), v && v.gameObject.activeInHierarchy, k) && !states.ContainsKey(v)) { states[v] = new St(); Taken++; } }
            if (states.Count == 0) { AnyApplied = false; return; }                                  // 100 % and nothing taken in: nothing is ever written
            bool any = false; gone.Clear();
            foreach (var kv in states)
            {
                var v = kv.Key; var s = kv.Value;
                if (!v) { gone.Add(v); continue; }
                if (!v.gameObject.activeInHierarchy) { if (v.Dying) gone.Add(v); else if (s.applied != 1f) any = true; continue; }   // a body that has gone is forgotten; a view hidden for a moment keeps what it has
                Apply(v.transform, s, k, v.Dying);
                if (s.applied != 1f) any = true;
            }
            foreach (var v in gone) states.Remove(v);
            if (!any && k == 1f) states.Clear();                                                    // back at 100 %: every view has its own size again, nothing is held
            AnyApplied = any;
        }

        /// <summary>The root's scale = the size the view itself gave it x k. A size the view wrote this frame is taken as its new full size; a
        /// dying view's own death writes (a melt) are left as they are (they are made with <see cref="Dying"/>).</summary>
        static void Apply(Transform t, St s, float k, bool dying)
        {
            var cur = t.localScale;
            if (!s.known) { s.known = true; s.applied = 1f; s.lastSet = cur; }
            bool foreign = !Same(cur, s.lastSet);
            if (foreign && dying && s.applied != 1f) { s.lastSet = cur; return; }
            if (s.applied == 1f || foreign) s.baseScale = cur;
            var want = Want(s.baseScale, k);
            if (!Same(want, cur)) { t.localScale = want; Writes++; }
            s.lastSet = want; s.applied = k;
        }

        /// <summary>Check only (edit mode, plain transforms): one frame of the tool's rule on transform t with its own state.</summary>
        public sealed class Probe { internal readonly St s = new St(); public float Applied => s.applied; }
        public static void ApplyForCheck(Transform t, Probe p, float k, bool dying) => Apply(t, p.s, k, dying);

        static bool Same(Vector3 a, Vector3 b) => a.x == b.x && a.y == b.y && a.z == b.z;
    }
}
