// OpenZ (GPL-3.0) - 1.3, open_fixes_2nov wreck_toss_landing update 1 (the residual rows of the counted check): a thrown turret turns into its landing pose over its whole flight, and sinks under the ground everywhere before it is removed.
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// <see cref="TossWholePath"/>: row 25's ease turned the spinning turret into the pose it lands in by ONE fixed correction, but all of it
    /// in the last 0.6 s (SmoothStep over that span): up to 180 degrees in 0.6 s, at most 450 degrees a second, on top of the spin that slows
    /// there. That alone reached 18-20 degrees in one frame at 30 fps and more on a long frame (the check's T1 20.1-32.8 degrees 9-14 frames
    /// before the landing). The landing pose is now picked at the throw (UnitView.TossAim.cs), so the same correction is planned there and
    /// spread over the whole flight like the fort cannon's (FortGunView.TossPath.cs): rotation = Slerp(identity, fix, SmoothStep(k)) x
    /// AngleAxis(spin so far) x the pose at the throw, fix = the turn that takes the spin's own end onto the landing pose. The spin is row 25's:
    /// full speed, then slowing to nothing over the last 0.6 s (or 30 % of a shorter flight), so it still eases into the pose it lands in and
    /// is exactly in it on the landing frame. The correction adds at most 1.5 x 180 / flight degrees a second (90 for a 3 s flight) and almost
    /// nothing near the landing. Only when the pose was picked at the throw (ground, not open water, the heavy landing on); else row 25 as before.
    /// <see cref="TossSinkUnderAll"/>: the landed turret's sink (TossFadeBegin) measured its top against the ground under its pivot only; lying
    /// on its side across a slope its top ended 0.15-0.18 m over the ground under its middle and was removed there in one frame (the check's
    /// P3 on L07 / L16 guns). Now against the lowest ground under its drawn box (3 x 3 points over its footprint, and the pivot).
    /// View only. Off = as the kit before this update.
    /// </summary>
    public partial class UnitView
    {
        public static bool TossWholePath = true, TossSinkUnderAll = true;
        /// <summary>Play check: whole-flight turns planned, the last plan's correction (deg), the last sink's extra depth over the pivot's ground (m).</summary>
        public static int TossWholePlanned; public static float LastTossWholeFixDeg, LastTossSinkExtra;
        bool tossWholeOn; Quaternion tossWholeStart = Quaternion.identity, tossWholeFix = Quaternion.identity; float tossWholeEaseAt, tossWholeSpan;

        /// <summary>TossTurret, right after TossAimRest: the whole flight's turn, once the landing pose is known.</summary>
        void TossWholePlan()
        {
            tossWholeOn = false;
            if (!TossWholePath || !tossRestPicked || !tossed || tossDur <= 0f) return;
            tossWholeSpan = Mathf.Max(0.001f, Mathf.Min(TossEaseSeconds, tossDur * TossEaseShare));        // row 25's ease span
            tossWholeEaseAt = Mathf.Max(0f, tossDur - tossWholeSpan);
            tossWholeStart = tossed.rotation;
            var spinEnd = Quaternion.AngleAxis(TossWholeSpinDeg(tossDur), tossAxis) * tossWholeStart;
            tossWholeFix = tossEaseRest * Quaternion.Inverse(spinEnd);
            LastTossWholeFixDeg = Quaternion.Angle(Quaternion.identity, tossWholeFix); TossWholePlanned++;
            tossWholeOn = true;
        }

        /// <summary>The spin turned so far (deg) at flight time t: full speed, then row 25's slow-down to nothing on the landing frame.</summary>
        float TossWholeSpinDeg(float t)
        {
            t = Mathf.Clamp(t, 0f, tossDur);
            if (t <= tossWholeEaseAt) return tossSpin * t;
            float u = Mathf.Clamp01((t - tossWholeEaseAt) / tossWholeSpan), un = 1f - u;
            return tossSpin * tossWholeEaseAt + tossSpin * tossWholeSpan * (1f - un * un * un) / 3f;
        }

        /// <summary>UpdateToss: the turn drawn at flight share k (exactly the landing pose at k = 1); false = not planned (row 25's ease as before).</summary>
        bool TossWholeStep(float k)
        {
            if (!tossWholeOn || !TossWholePath || !tossed) return false;
            tossed.rotation = k >= 1f ? tossEaseRest
                : Quaternion.Slerp(Quaternion.identity, tossWholeFix, Mathf.SmoothStep(0f, 1f, k)) * Quaternion.AngleAxis(TossWholeSpinDeg(tossT), tossAxis) * tossWholeStart;
            return true;
        }

        /// <summary>TossFadeBegin: the ground its top must go under: the lowest under its drawn box (3 x 3 points over the footprint) and its pivot.</summary>
        float TossFadeGround(Vector3 pivot)
        {
            float atPivot = game ? game.GroundY(pivot.x, pivot.z) : pivot.y, low = atPivot;
            if (!TossSinkUnderAll || !game || !tossed) return atPivot;
            bool any = false; var b = new Bounds();
            foreach (var r in tossed.GetComponentsInChildren<Renderer>())
            {
                if (!r || !r.enabled || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                if (any) b.Encapsulate(r.bounds); else { b = r.bounds; any = true; }
            }
            if (any)
                for (int i = 0; i < 3; i++)
                    for (int j = 0; j < 3; j++)
                        low = Mathf.Min(low, game.GroundY(Mathf.Lerp(b.min.x, b.max.x, i * 0.5f), Mathf.Lerp(b.min.z, b.max.z, j * 0.5f)));
            LastTossSinkExtra = atPivot - low;
            return low;
        }
    }
}
