// OpenZ (GPL-3.0) - 1.4, kit wreck_landing: a thrown turret settles where it lands, on any ground: no rise, no slide up a hill, no pop.
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// After the 1.3 kit and its update some landed turrets still moved in their first frames on bumpy or steep ground (the 1.3 check on level
    /// 2: a light tank's turret moved 1.38 m in one frame, a gun's rose 3.58 m). The movers were the turret's own heavy landing (item 172,
    /// OzHeavyLanding.Update), which drew it for a whole second: a bounce of up to 12 cm, a rock about its pivot, a skid of 0.15-0.6 m along its
    /// flight, and a new ground fit on every frame from the 0.5 m ground lattice (its lowest point 5 cm in). On a slope the skid went uphill;
    /// over a bump, a ledge or a cliff the lattice smooths the wall into a 0.5 m ramp and the fit climbed it, and the fit's band (five ground
    /// samples under its box) let a point sink into a wall and come out of it again in one frame.
    /// <see cref="TurretSettleInPlace"/>: a turret's landing is planned once, on its landing frame (<see cref="SettlePlan"/>), and then only
    /// played (<see cref="SettleStep"/>). It keeps the pose its arc ended in (its real lowest vertex 5 cm in: UnitView.TossAimRest). It slides
    /// on along its flight while its sideways speed runs out (at most 0.24 s and 0.6 m, at most 4 m/s at the start, 12 % of its speed in
    /// metres as before). The slide is cut where the fit would rise (uphill, a bump) or drop more than its own length (an edge), and there is
    /// no slide at all where the ground under it and its way is steeper than 45 degrees (a cliff, a ledge, rough ground). Its height follows
    /// the planned fit, which only ever goes down; each frame it is moved by the plan's change only, so another mover's step is never undone.
    /// No bounce, no rock. Done after the slide: nothing moves it until its wreck sinks. A small puff where it stops (the bounce's second puff
    /// before). The water and lava rules take it as before. The fort cannon's heavy landing and every other one are unchanged. View only.
    /// Switch: <see cref="TurretSettleInPlace"/> (false = the 1.3 landing: bounce, rock, skid and the ground fit every frame).
    /// </summary>
    public partial class OzHeavyLanding
    {
        public static bool TurretSettleInPlace = true;
        /// <summary>The slide's longest time (s) and its top speed at the start (m/s); the steepest step between two lattice nodes 0.5 m apart
        /// that still lets it slide (m); the steepest drop it follows (m per m of slide); what counts as no rise between two plan points (m).</summary>
        public static float SettleSkidSeconds = 0.24f, SettleSkidTopSpeed = 4f, SettleSteepStep = 0.5f, SettleDropSlope = 1f, SettleRiseSlack = 0.002f;
        const int PlanSteps = 8, PlanExact = 12, PlanMaxNodes = 2500;
        /// <summary>Play checks: turret landings planned, slides cut by a rise / by an edge, no slide on steep ground; the last plan in words.</summary>
        public static int Plans, PlanCutRise, PlanCutEdge, PlanSteep;
        public static string LastPlan = "-";

        bool settleOn; float planFull, planS, planT; Vector3 planMoved; string planCut = "-";
        readonly float[] planDy = new float[PlanSteps + 1];
        static Vector3[] planPts = new Vector3[1024];
        static float[] planGrid = new float[256]; static int gridI0, gridJ0, gridW, gridH;
        static readonly float[] planE = new float[PlanSteps + 1], lowV = new float[PlanExact];
        static readonly int[] lowI = new int[PlanExact];

        /// <summary>Editor checks: this landing is the planned one (still sliding); the move its last step made and that step's frame; the
        /// planned slide (m) and why it ended there.</summary>
        public bool SettlingInPlace => settleOn;
        public Vector3 SettleMoved { get; private set; }
        public int SettleMovedFrame { get; private set; } = -1;
        public float PlanSlide => planS;
        public string PlanEnd => planCut;

        /// <summary>Begin, a turret, after its fit in the pose it touched down in: the whole landing, planned now.</summary>
        void SettlePlan(Vector3 vel)
        {
            settleOn = false;
            if (!TurretSettleInPlace) return;
            var h = new Vector3(vel.x, 0f, vel.z); float hs = h.magnitude; bool moving = h.sqrMagnitude > 0.01f;      // as Begin's skidDir
            float v0 = moving ? Mathf.Min(hs, SettleSkidTopSpeed) : 0f;
            planFull = moving ? Mathf.Min(hs * 0.12f, 0.6f, v0 * SettleSkidSeconds * 0.5f) : 0f;
            planS = 0f; planT = 0f; planMoved = Vector3.zero; planCut = "none";
            for (int j = 0; j <= PlanSteps; j++) planDy[j] = 0f;
            if (!fits) planCut = "no fit";
            else if (planFull < 0.02f) planCut = "no sideways speed";
            else if (ZGame.I) PlanTheSlide(ZGame.I);
            if (planS > 0.005f) planT = Mathf.Clamp(2f * planS / Mathf.Max(v0, 0.1f), 0.06f, SettleSkidSeconds);   // its sideways speed runs out over the slide
            bounceH = 0f; rockDeg = 0f; skid = planS;                                                                   // the numbers the checks read (LastBounce, LastSkid)
            settleOn = true; Plans++;
            LastPlan = $"{kind}: slide {planS:0.00} of {planFull:0.00} m in {planT:0.00} s (ended by: {planCut}), height change {planDy[PlanSteps]:+0.00;-0.00} m";
        }

        /// <summary>The slide along its flight, in PlanSteps points: the fit's height change at each (its points as drawn on the landing frame
        /// moved sideways; the ground lattice for all, the exact probe for the lowest few), kept only while it never rises and never drops
        /// more than SettleDropSlope per metre; none on steep ground.</summary>
        void PlanTheSlide(ZGame g)
        {
            int n = Fill(out float minY, out float x0, out float x1, out float z0, out float z1);
            if (n == 0) { planCut = "nothing drawn"; return; }
            float f = planFull;
            int i0 = Mathf.FloorToInt((x0 - f) / Cell), i1 = Mathf.CeilToInt((x1 + f) / Cell), j0 = Mathf.FloorToInt((z0 - f) / Cell), j1 = Mathf.CeilToInt((z1 + f) / Cell);
            gridI0 = i0; gridJ0 = j0; gridW = i1 - i0 + 2; gridH = j1 - j0 + 2;                                // one node more each way: every bilinear read stays inside
            if (gridW * gridH > PlanMaxNodes) { planCut = "too big"; return; }
            if (planGrid.Length < gridW * gridH) planGrid = new float[Mathf.NextPowerOfTwo(gridW * gridH)];
            float hi = float.MinValue, lo = float.MaxValue, steep = 0f;
            for (int j = 0; j < gridH; j++)
                for (int i = 0; i < gridW; i++) planGrid[j * gridW + i] = Node(i0 + i, j0 + j);                     // the landing's own lattice (the same nodes Lift reads)
            for (int j = 0; j < gridH - 1; j++)
                for (int i = 0; i < gridW - 1; i++)
                {
                    float a = planGrid[j * gridW + i];
                    if (a > hi) hi = a;
                    if (a < lo) lo = a;
                    steep = Mathf.Max(steep, Mathf.Max(Mathf.Abs(planGrid[j * gridW + i + 1] - a), Mathf.Abs(planGrid[(j + 1) * gridW + i] - a)));
                }
            if (steep > SettleSteepStep) { planCut = "steep ground"; PlanSteep++; return; }
            float band = hi - lo + 0.4f + (BoxSink - Sink);                                                    // a point over this above the lowest can never touch first
            int m = 0;
            for (int i = 0; i < n; i++)
            {
                var q = wp[i]; if (q.y > minY + band) continue;
                if (m == planPts.Length) System.Array.Resize(ref planPts, m * 2);
                planPts[m++] = new Vector3(q.x, q.y + ws[i], q.z);                                                // its height at its sink: 0 over the ground = where the fit puts it
            }
            if (m == 0) { planCut = "nothing drawn"; return; }
            for (int s = 0; s <= PlanSteps; s++)
            {
                float ox = skidDir.x * f * s / PlanSteps, oz = skidDir.z * f * s / PlanSteps; int c = 0;
                for (int i = 0; i < m; i++)
                {   // the lowest few by the lattice, kept in order
                    var p = planPts[i]; float e = p.y - GridGround(p.x + ox, p.z + oz);
                    if (c == PlanExact && e >= lowV[c - 1]) continue;
                    int k = c < PlanExact ? c++ : c - 1;
                    while (k > 0 && lowV[k - 1] > e) { lowV[k] = lowV[k - 1]; lowI[k] = lowI[k - 1]; k--; }
                    lowV[k] = e; lowI[k] = i;
                }
                float best = float.MaxValue;
                for (int k = 0; k < c; k++) { var p = planPts[lowI[k]]; best = Mathf.Min(best, p.y - g.GroundY(p.x + ox, p.z + oz)); }
                planE[s] = best;
            }
            float step = f / PlanSteps; int last = 0;
            for (int s = 1; s <= PlanSteps; s++)
            {
                float d = planE[0] - planE[s], env = planDy[s - 1];                                                // d: how far the fit would move it up (+) or down (-) there
                if (d > env + SettleRiseSlack) { planCut = "a rise"; PlanCutRise++; break; }
                if (d < env - SettleDropSlope * step) { planCut = "an edge"; PlanCutEdge++; break; }
                planDy[s] = Mathf.Min(d, env); last = s;
            }
            for (int s = last + 1; s <= PlanSteps; s++) planDy[s] = planDy[last];
            planS = f * last / PlanSteps;
        }

        /// <summary>Ground(): the same bilinear lattice, read from the plan's copy of its nodes.</summary>
        static float GridGround(float x, float z)
        {
            float fx = x / Cell - gridI0, fz = z / Cell - gridJ0; int i = Mathf.Clamp(Mathf.FloorToInt(fx), 0, gridW - 2), j = Mathf.Clamp(Mathf.FloorToInt(fz), 0, gridH - 2);
            float u = fx - i, w = fz - j; int a = j * gridW + i, b = a + gridW;
            return Mathf.Lerp(Mathf.Lerp(planGrid[a], planGrid[a + 1], u), Mathf.Lerp(planGrid[b], planGrid[b + 1], u), w);
        }

        float PlanDy(float s)
        {
            if (planFull <= 0f) return 0f;
            float x = s / planFull * PlanSteps; int j = Mathf.Clamp(Mathf.FloorToInt(x), 0, PlanSteps - 1);
            return Mathf.Lerp(planDy[j], planDy[j + 1], Mathf.Clamp01(x - j));
        }

        /// <summary>Update, every frame after the landing frame (t counted): true = the planned landing drew this frame (the 1.3 landing is
        /// skipped). Moves the piece by the plan's change only; done at the slide's end.</summary>
        bool SettleStep()
        {
            if (!settleOn) return false;
            if (OzWaterLanding.Watched(transform) || OzLavaMelt.Busy(transform))
            {   // the water or the lava rule moves it from here
                settleOn = false; SettleMoved = Vector3.zero; SettleMovedFrame = Time.frameCount; Destroy(this); return true;
            }
            float k = planT > 0f ? Mathf.Clamp01(t / planT) : 1f, s = planS * (1f - (1f - k) * (1f - k));
            var want = skidDir * s + Vector3.up * PlanDy(s);
            var move = want - planMoved; planMoved = want;
            if (move.sqrMagnitude > 0f) transform.position += move;
            SettleMoved = move; SettleMovedFrame = Time.frameCount;
            if (k >= 1f) SettleDone();
            return true;
        }

        void SettleDone()
        {
            settleOn = false;
            LastPop = 0f;
            LastGap = fits ? ExactGap(out LastBoxGap, out LastContactVertex) : 0f;
            if (!fits) { LastBoxGap = 0f; LastContactVertex = false; }
            LastFit = fit; LastKind = kind; LastSettle = t; Settled++;
            if (planS >= 0.05f && FxManager.I) FxManager.I.Dust(touch + planMoved, 0.6f);                         // where it comes to rest
            Destroy(this);
        }
    }
}
