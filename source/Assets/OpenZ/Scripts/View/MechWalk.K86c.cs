using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.2 (the k92 leftover, v1 AND v2: one walk code): a standing foot must stay put while the mech turns on the spot. The Play proof (L02,
    /// k92) measured a standing foot moved up to 2.0-2.4 m and turned ~45 deg in one stance, always at a walk's start, where the drawn hull
    /// pivots at up to 150 deg/s while the drawn body (View/VehicleSlip.cs) also catches up with its sim point (up to 2.6 m behind it): the
    /// legs then run out of reach and the over-stretch guard DRAGS the standing foot along (MechWalk.cs), and the hurried
    /// swing of the other foot still had 0.10 s to go. Now:
    ///  * QUICK LAND: a standing foot past <see cref="LandNowShare"/> of its reach while the other foot swings: that swing lands in this
    ///    frame when only the last part of its eased arc is left (else the rest of the arc runs in <see cref="LandNowLeft"/> s: never a
    ///    jump from mid-arc), so the far foot may step as soon as it stands
    ///    (MechWalk.K92: it lifts as soon as the other foot stands). The far foot pivots on its spot instead of being dragged.
    ///  * STANCE MONITOR: every standing foot's DRAWN place and yaw (after the solve, what the camera sees) against where it landed; a stance
    ///    that moved more than <see cref="WarnCm"/> or turned more than <see cref="WarnDeg"/> is counted and logged once with its parts:
    ///    the drag, the IK miss, frames the legs were not posed (the walk skipped a frame while the body moved), the body's own turn and
    ///    move in that stance, turning on the spot or not. Statistics only.
    /// Switch: <see cref="QuickLand"/> (off = as before, statement for statement; the monitor only counts).
    /// </summary>
    public partial class MechWalk
    {
        /// <summary>The switch for the quick landing (off = the walk as before).</summary>
        public static bool QuickLand = true;
        /// <summary>The share of a standing leg's reach at which the other foot's swing lands at once; the monitor's thresholds (cm, deg).</summary>
        public static float LandNowShare = 0.98f, WarnCm = 3f, WarnDeg = 3f;
        /// <summary>The quick landing: a swing with at most LandNowTail of its arc left lands in this frame; one with more runs the rest in
        /// LandNowLeft s (the same eased arc, faster: never a jump from mid-arc).</summary>
        public static float LandNowTail = 0.1f, LandNowLeft = 0.04f;
        /// <summary>Statistics (the Play checks): quick landings; stances over the thresholds; the largest drawn move (cm) and turn (deg) of a
        /// standing foot in one stance, all stances and those that saw the body turn on the spot (over 60 deg/s); frames a planted foot was
        /// not posed by the walk.</summary>
        public static int QuickLands, StanceWarnN, UnposedFrames;
        public static float StanceMoveMaxCm, StanceTurnMaxDeg, TurnStanceMoveMaxCm, TurnStanceTurnMaxDeg;
        public static void ResetK86c() { QuickLands = StanceWarnN = UnposedFrames = 0; StanceMoveMaxCm = StanceTurnMaxDeg = TurnStanceMoveMaxCm = TurnStanceTurnMaxDeg = 0f; }

        Vector3[] k86At, k86Land; Quaternion[] k86Rot; float[] k86Move, k86Turn, k86BodyYaw0; Vector3[] k86Body0; bool[] k86Spin, k86On; int k86Frame = -1;
        static float k86LastLog = -9f;

        /// <summary>The hurry pass, for a standing foot l while o swings: past LandNowShare of l's reach, o lands in this frame (its arc's tail) or within LandNowLeft s.</summary>
        void K86cLandNow(Leg l, Leg o, float reachH)
        {
            if (!QuickLand || !l.planted || o.planted || o.swingT >= 1f) return;
            if (new Vector2(l.hip.position.x - l.plant.x, l.hip.position.z - l.plant.z).magnitude <= LandNowShare * reachH) return;
            float rest = 1f - Mathf.Clamp01(o.swingT);
            if (rest <= LandNowTail) { o.swingT = 1f; QuickLands++; }                                   // the arc's last part (a few cm): the feet loop below lands it now (s = 1: its aim, its plant event)
            else if (rest * Mathf.Max(0.05f, o.swingDur) > LandNowLeft) { o.swingDur = LandNowLeft / rest; QuickLands++; }
        }

        /// <summary>After the solve: the drawn standing feet against their landing (statistics and one log line per bad stance).</summary>
        void K86cMonitor(float bodyYaw, float dt, bool tele)
        {
            if (legs == null || legs.Length != 2) return;
            if (k86At == null) { k86At = new Vector3[2]; k86Land = new Vector3[2]; k86Rot = new Quaternion[2]; k86Move = new float[2]; k86Turn = new float[2]; k86BodyYaw0 = new float[2]; k86Body0 = new Vector3[2]; k86Spin = new bool[2]; k86On = new bool[2]; }
            bool skipped = k86Frame >= 0 && Time.frameCount - k86Frame > 1; k86Frame = Time.frameCount;
            bool spin = dt > 0f && Mathf.Abs(turnRate) > 60f;
            for (int i = 0; i < 2; i++)
            {
                var l = legs[i]; var foot = l.ankle;
                if (!l.planted) { if (k86On[i]) K86cEnd(i, l); k86On[i] = false; continue; }
                if (tele) { k86On[i] = false; continue; }                                                     // a teleport re-stands both feet: no stance runs through it
                if (!k86On[i] || l.stanceStart != k86Land[i])
                {   // a new stance (it landed since the last frame): its drawn place and yaw now
                    if (k86On[i]) K86cEnd(i, l);
                    k86On[i] = true; k86Land[i] = l.stanceStart; k86At[i] = foot.position; k86Rot[i] = foot.rotation; k86Move[i] = 0f; k86Turn[i] = 0f;
                    k86BodyYaw0[i] = bodyYaw; k86Body0[i] = body.position; k86Spin[i] = false;
                    continue;
                }
                if (skipped) UnposedFrames++;
                k86Spin[i] |= spin;
                k86Move[i] = Mathf.Max(k86Move[i], (foot.position - k86At[i]).magnitude * 100f);
                k86Turn[i] = Mathf.Max(k86Turn[i], Quaternion.Angle(k86Rot[i], foot.rotation));
            }
        }

        void K86cEnd(int i, Leg l)
        {
            StanceMoveMaxCm = Mathf.Max(StanceMoveMaxCm, k86Move[i]); StanceTurnMaxDeg = Mathf.Max(StanceTurnMaxDeg, k86Turn[i]);
            if (k86Spin[i]) { TurnStanceMoveMaxCm = Mathf.Max(TurnStanceMoveMaxCm, k86Move[i]); TurnStanceTurnMaxDeg = Mathf.Max(TurnStanceTurnMaxDeg, k86Turn[i]); }
            if (k86Move[i] <= WarnCm && k86Turn[i] <= WarnDeg) return;
            StanceWarnN++;
            if (!SlideLog || Time.unscaledTime - k86LastLog < 0.25f) return;
            k86LastLog = Time.unscaledTime;
            float turned = body ? Mathf.Abs(Mathf.DeltaAngle(k86BodyYaw0[i], body.eulerAngles.y)) : 0f, moved = body ? (body.position - k86Body0[i]).magnitude : 0f;
            Debug.Log($"[MechWalk] standing foot moved {k86Move[i]:0.0} cm / turned {k86Turn[i]:0.0} deg: unit {(view && view.Obj != null ? view.Obj.Id : -1)} {(l.left ? "left" : "right")} foot, " +
                      $"the body turned {turned:0} deg and moved {moved:0.00} m in the stance{(k86Spin[i] ? " (turning on the spot)" : "")}, drag {l.dragCm:0.0} cm, IK miss max {l.residCm:0.0} cm, " +
                      $"soft-reach frames {l.softFrames}, frames not posed {UnposedFrames} (all mechs)");
        }
    }
}
