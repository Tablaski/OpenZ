#if UNITY_EDITOR
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// k84b, the recoil test (menu "12") for every unit it films, judged against that unit's OWN design (k84's light tank was judged
    /// against the heavy's numbers printed in the rule: v2 head 0.25 m, v1 0.11 m, hull 1.4-2.8 deg):
    ///  * the heavy tank: its rule exactly as before (the same numbers, the same words);
    ///  * every other unit (the light tank v1 and v2): the sizes View/HeavyRecoil.Types.cs gives its springs (HeavyRecoil.DesignHeadSlide /
    ///    DesignHeadRock / DesignBodyRock / DesignBodySlide), the head's slide as DRAWN (its design in the head's parent units x that node's
    ///    scale: the light tank's Model node 0.9302 x OzLightTank.V2Size), with the bands the heavy's rule allowed round its own design
    ///    (the head's rock 0.72-1.36 x design) or tighter (the head's slide 0.85-1.25 x: the heavy's had no upper bound, the hull's rock 0.85-1.25 x, its slide 0.75-1.5 x,
    ///    the frame steps 0.85 / 0.92 / 1.15 x the design: no jump);
    ///  * a target the shooting tanks reach from where they stand: the heavy's jeep tile stands 122-132 px from the two recoil tanks, beyond
    ///    the light gun's 120 px (the heavy's 144): the light v1 crept toward it during its shot window (its hull numbers were taken while it
    ///    moved). A gun that cannot reach that tile from both tanks with a tile to spare gets the nearer open tile of <see cref="LightV2Spots"/>.
    /// </summary>
    public partial class HeavyV2Films
    {
        /// <summary>One shot window's rule: the bands and the words the verdict prints.</summary>
        struct RecRule
        {
            public float headMin, headMax, homeMax, rockLo, rockHi, hullLo, hullHi, slideLo, slideHi, stepHull, stepSlide, stepHead;
            public string head, hull, steps;
        }

        /// <summary>The rule for this watched tank: the heavy's as before; any other type its own design sizes.</summary>
        RecRule RecRuleFor(RecWatch w)
        {
            var r = new RecRule { homeMax = 0.04f, rockLo = 1.8f, rockHi = 3.4f, hullLo = 1.4f, hullHi = 2.8f, slideLo = 0.06f, slideHi = 0.18f, stepHull = 1.7f, stepSlide = 0.11f };
            string type = w.o != null ? w.o.Type : S.Type;
            if (type == OzHeavyTank.Type)
            {   // the heavy: exactly the rule before k84b
                r.headMin = w.model == 2 ? 0.25f : 0.11f; r.headMax = float.MaxValue; r.stepHead = w.model == 2 ? 0.32f : 0.16f;
                r.head = $"peak >= {r.headMin:0.00} m within 1-4 frames, rock 1.8-3.4 deg, home under 0.04 m";
                r.hull = "1.4-2.8 deg, opposite under 45 %, settled under 0.2 deg, slide 0.06-0.18 m";
                r.steps = $"no jump: <= 1.7 deg, 0.11 m, {r.stepHead:0.00} m";
                return r;
            }
            var v = w.v ? w.v : View(w.o);
            float scale = 1f;
            if (v && w.yaw && w.yaw.parent) scale = Mathf.Abs(w.yaw.parent.lossyScale.x) / Mathf.Max(1e-4f, Mathf.Abs(v.transform.lossyScale.x));
            float raw = HeavyRecoil.DesignHeadSlide(type, w.model), head = raw * scale, rock = HeavyRecoil.DesignHeadRock(type);
            float hull = HeavyRecoil.DesignBodyRock(type), slide = HeavyRecoil.DesignBodySlide(type);
            r.headMin = 0.85f * head; r.headMax = 1.25f * head; r.homeMax = 0.2f * head; r.stepHead = 1.15f * head;
            r.rockLo = 0.72f * rock; r.rockHi = 1.36f * rock;
            r.hullLo = 0.85f * hull; r.hullHi = 1.25f * hull; r.slideLo = 0.75f * slide; r.slideHi = 1.5f * slide;
            r.stepHull = 0.85f * hull; r.stepSlide = 0.92f * slide;
            r.head = $"its own design: peak {r.headMin:0.000}-{r.headMax:0.000} m = 0.85-1.25 x {head:0.000} m drawn ({raw:0.00} x the head's scale {scale:0.000}) within 1-4 frames, " +
                     $"rock {r.rockLo:0.00}-{r.rockHi:0.00} deg (0.72-1.36 x {rock:0.0}), home under {r.homeMax:0.000} m";
            r.hull = $"its own design: {r.hullLo:0.00}-{r.hullHi:0.00} deg (0.85-1.25 x {hull:0.00}), opposite under 45 %, settled under 0.2 deg, slide {r.slideLo:0.000}-{r.slideHi:0.000} m (0.75-1.5 x {slide:0.000})";
            r.steps = $"no jump: <= {r.stepHull:0.00} deg, {r.stepSlide:0.000} m, {r.stepHead:0.000} m (0.85 / 0.92 / 1.15 x its design)";
            return r;
        }

        /// <summary>The recoil take's pass rule in words: the heavy's as before; the others' with one barrel and their own shot sound.</summary>
        string RecPassRule()
        {
            if (S.Type == OzHeavyTank.Type)
                return "pass rule (the lines above + the crops): on every shot the turret (head) itself kicks back along its barrels and its front tips up, then eases home in about a quarter second, v2 about twice v1's kick; the whole hull rocks back (the side it fires toward up) about 2 deg and slides back about 0.1 m, then settles with one smaller opposite rock; the second barrel 0.36 s later adds to the motion with no jump; the heavyshot sound on each shot";
            return $"pass rule (the lines above + the crops): on every shot the turret (head) itself kicks back along its barrel and its front tips up, then eases home in about a quarter second, v2 about twice v1's kick (each judged against its OWN design: View/HeavyRecoil.Types.cs); the whole hull rocks back (the side it fires toward up) and slides back a little, then settles with one smaller opposite rock; the next shot adds to the motion with no jump; the {S.ShotKey} sound on each shot; both tanks stand still (their target within their gun's reach)";
        }

        /// <summary>The recoil take's target tile: the heavy's jeep (the heavy's take is not changed), unless this unit's gun cannot reach it
        /// from both tanks with a tile to spare (centre to centre, the sim's attack radius); then the nearer open tile of LightV2Spots
        /// ("recoil/jeep near").</summary>
        void RecoilTargetInReach(int ax2, int ay2, int ax1, int ay1, ref int jx, ref int jy)
        {
            if (S.Type == OzHeavyTank.Type && !HeavyReachK102) return;                                // the heavy's take exactly as before (k102: the heavy too gets a target in its reach: View/HeavyV2Films.K102.cs)
            var u = W.S.Unit(S.Type); if (u == null) return;
            int reach = u.attack_radius - 16;
            float Far(int x, int y)
            {
                float m = 0f;
                foreach (var (tx, ty) in new[] { (ax2, ay2), (ax1, ay1) })
                    foreach (int off in new[] { 8, 16 })
                        m = Mathf.Max(m, Mathf.Sqrt((x * 16 + off - (tx * 16 + 16)) * (float)(x * 16 + off - (tx * 16 + 16)) + (y * 16 + off - (ty * 16 + 16)) * (float)(y * 16 + off - (ty * 16 + 16))));
                return m;
            }
            float f0 = Far(jx, jy);
            if (f0 <= reach) { Note($"the target tile ({jx},{jy}) is {f0:0} px from the farther tank: within the {S.Label}'s gun ({u.attack_radius} px) with a tile to spare"); return; }
            if (LightV2Spots.Get(g.level, "recoil", "jeep near", out int nx, out int ny) && Far(nx, ny) <= reach)
            {
                Note($"k84b: the heavy's target tile ({jx},{jy}) is {f0:0} px from the farther tank, beyond the {S.Label}'s gun ({u.attack_radius} px): the target stands at the open tile ({nx},{ny}), {Far(nx, ny):0} px (View/LightV2Spots: the open-tile scan)");
                jx = nx; jy = ny; return;
            }
            Note($"k84b: the target tile ({jx},{jy}) is {f0:0} px from the farther tank, beyond the {S.Label}'s gun ({u.attack_radius} px), and no nearer open tile is listed for {g.level}: the tanks may drive to it");
        }
    }
}
#endif
