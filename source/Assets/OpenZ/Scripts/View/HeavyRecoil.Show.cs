using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// v2 "missing recoil on body when shoting"). View only; the sim is never touched.
    /// Cause (read in the code and the Play logs): the body rock IS drawn in the built game as in the editor (no release-only path: no conditional
    /// code in HeavyRecoil, UnitView, the planted gear or VehicleMotion; RecoilFeel's switch and gain change only from an editor menu; the planted
    /// gear runs BEFORE the rock and only lifts the hull afterwards, it never overwrites it), but it was designed too small to see: the hull's rock
    /// peaked at 2.0 deg (heavy), 1.40 (light), 1.27 (medium tank) and 1.2 (MLRS, once per salvo), with a 0.07-0.12 m slide: 2-3 pixels at the
    /// game's 70 m camera (the editor checks measured it and noted it "hard to see" from the RTS camera); and the planted gear keeps only a quarter
    /// (TrackGround.HullFeel) of the suspension's own per-barrel kick that the heavy used to add to it. Before the v2 work the hulls nodded 4.5
    /// (light) to 7.8 deg (heavy) per shot.
    /// Now the rock and slide of a shot ALONG the hull (ahead or behind, the usual case) reach <see cref="RecoilShow"/>'s drawn peaks: the heavy
    /// 6.5 deg and 0.22 m, the medium tank 5.0 deg and 0.18 m, the light tank 4.5 deg and 0.15 m, the MLRS 4.0 deg per salvo (no slide), with the
    /// same eased spring (peak at about 0.09 s, one smaller opposite rock, settled in about 0.6 s). ACROSS the hull (a shot to the side) the roll
    /// and slide stay as they were: the v2 hulls sit 3-4 cm inside their planted side plates (measured on the models: light 3.9 cm, medium 3.8 cm,
    /// MLRS 3.0 cm at the plates' tops), and a bigger roll would take the hull walls through them. The tracks and wheels stay planted (the gear
    /// is placed on the ground after the rock, as before); the mech and the gun keep their sizes. Switch: <see cref="RecoilShow.Enabled"/>
    /// (off = the k81b / k84 sizes).
    /// </summary>
    public static class RecoilShow
    {
        /// <summary>The switch (off = the hull rock and slide as designed before: 2.0 / 1.40 / 1.27 / 1.2 deg).</summary>
        public static bool Enabled = true;
        /// <summary>The drawn peak of the hull's rock back (deg) for a shot along the hull; 0 = the type keeps its old size.</summary>
        public static float HeavyRockDeg = 6.5f, MediumRockDeg = 5.0f, LightRockDeg = 4.5f, MlrsRockDeg = 4.0f;
        /// <summary>The drawn peak of the hull's slide back (m) for a shot along the hull; 0 = the type keeps its old size.</summary>
        public static float HeavySlideM = 0.22f, MediumSlideM = 0.18f, LightSlideM = 0.15f;
        /// <summary>k112, item 151 (a visible rock on every salvo): the MLRS hull's drawn rock (deg) for a salvo straight ACROSS the hull (to
        /// a side; between ahead and across it blends as before); before 1.2 (k85's size: 1.45 deg measured on v1's side salvos, too small
        /// to see). A roll about the far track's bottom took v2's hull walls into its planted side plates past about 1.7 deg (they sit 3 cm
        /// inside them), so an across rock turns about an axis MlrsRollAxisUpM (m, model units) over the far track's bottom, the middle of
        /// the hull wall the plates cover: measured off the engine (the game's own motion, 5 grounds, 1x and 4x ripple), 2.5 deg there keeps
        /// v2's hull out of its plates and belts on every ground and v1's within its rule; 0 = the old across size.</summary>
        public static float MlrsAcrossDeg = 2.5f, MlrsRollAxisUpM = 0.75f;
        /// <summary>Statistics (the checks): shots kicked with the bigger rock.</summary>
        public static int Kicks;

        public static float RockDeg(string t) => !Enabled ? 0f : t == OzHeavyTank.Type ? HeavyRockDeg : t == OzMediumTank.Type ? MediumRockDeg : t == OzLightTank.Type ? LightRockDeg : t == OzMlrs.Type ? MlrsRockDeg : 0f;
        public static float SlideM(string t) => !Enabled ? 0f : t == OzHeavyTank.Type ? HeavySlideM : t == OzMediumTank.Type ? MediumSlideM : t == OzLightTank.Type ? LightSlideM : 0f;
        public static float AcrossDeg(string t) => !Enabled ? 0f : t == OzMlrs.Type ? MlrsAcrossDeg : 0f;                       // k112, item 151
        public static float RollAxisUpM(string t) => !Enabled || AcrossDeg(t) <= 0f ? 0f : t == OzMlrs.Type ? MlrsRollAxisUpM : 0f;   // k112, item 151

        public static string Report()
        {
            var sb = new System.Text.StringBuilder($"RecoilShow: on {Enabled}; drawn peaks for a shot along the hull (across: as before), kicks {Kicks}\n");
            foreach (var t in new[] { OzHeavyTank.Type, OzMediumTank.Type, OzLightTank.Type, OzMlrs.Type })
                sb.Append($"  {t,-17} rock {HeavyRecoil.DesignBodyRock(t):0.00} deg (across {HeavyRecoil.DesignBodyRockAcross(t):0.00}), slide {HeavyRecoil.DesignBodySlide(t):0.000} m (across {HeavyRecoil.DesignBodySlideAcross(t):0.000})\n");
            return sb.ToString();
        }
    }

    public partial class HeavyRecoil
    {
        /// <summary>The rock's gain along the hull for this type (>= 1): RecoilShow's peak over the old design peak.</summary>
        static float ShowRockGain(string t)
        {
            float old = BodyRockFor(t) * RecoilFeel.HullTilt(t, ZKind.Vehicle), want = RecoilShow.RockDeg(t);
            return want > 0f && old > 1e-4f ? Mathf.Max(1f, want / old) : 1f;
        }

        /// <summary>The slide's gain along the hull for this type (>= 1; a type with no slide keeps none).</summary>
        static float ShowSlideGain(string t)
        {
            float old = BodyPushFor(t) * RecoilFeel.Hull(t, ZKind.Vehicle), want = RecoilShow.SlideM(t);
            return want > 0f && old > 1e-4f ? Mathf.Max(1f, want / old) : 1f;
        }

        /// <summary>k112, item 151: the rock's gain ACROSS the hull for this type (>= 1; 1 = the old across size).</summary>
        static float ShowRockAcrossGain(string t)
        {
            float old = BodyRockFor(t) * RecoilFeel.HullTilt(t, ZKind.Vehicle), want = RecoilShow.AcrossDeg(t);
            return want > 0f && old > 1e-4f ? Mathf.Max(1f, want / old) : 1f;
        }

        /// <summary>k112, item 151: how high (m, model units) the rock's axis sits over the far track's bottom for the rock direction dl (the
        /// root's frame, unit length): RecoilShow.RollAxisUpM times its share across the hull (a rock ahead or behind: 0, as before).</summary>
        float RollUpNow(Vector3 dl) => obj == null ? 0f : RecoilShow.RollAxisUpM(obj.Type) * dl.x * dl.x;

        /// <summary>Kick: the shot's direction with its part along the hull (the root's z) times the gain and its part across the hull times
        /// gainX (1 = as it is; k112: the MLRS's across rock).</summary>
        Vector3 ShowSplit(Vector3 dir, float gain, float gainX = 1f)
        {
            if ((gain <= 1.0001f && gainX <= 1.0001f) || !view) return dir;
            var root = view.transform;
            var dl = root.InverseTransformDirection(dir);
            var w = root.TransformDirection(new Vector3(dl.x * gainX, 0f, dl.z * gain)); w.y = 0f;
            return w;
        }

        /// <summary>Kick: both directions (the slide's and the rock's) of one shot.</summary>
        void ShowDirs(Vector3 dir, string t, out Vector3 pushDir, out Vector3 rockDir)
        {
            float gs = ShowSlideGain(t), gr = ShowRockGain(t), grx = ShowRockAcrossGain(t);              // k112, item 151: + the across gain of the rock
            pushDir = ShowSplit(dir, gs); rockDir = ShowSplit(dir, gr, grx);
            if (gs > 1.0001f || gr > 1.0001f || grx > 1.0001f) RecoilShow.Kicks++;
        }

        /// <summary>The design sizes of a shot straight across the hull (deg / m, RecoilFeel's gains included): the old ones.</summary>
        public static float DesignBodyRockAcross(string type) => BodyRockFor(type) * RecoilFeel.HullTilt(type, ZKind.Vehicle) * ShowRockAcrossGain(type);   // k112, item 151: the MLRS's across rock drawn bigger
        public static float DesignBodySlideAcross(string type) => BodyPushFor(type) * RecoilFeel.Hull(type, ZKind.Vehicle);

        /// <summary>The test's expected peak for a shot whose direction in the hull's frame is (x across, z along), normalised: the rock (deg) and the
        /// slide (m) the springs reach (along x gain x z^2 + across x x^2, the part in the shot's own direction).</summary>
        public static void DesignBodyToward(string type, Vector3 local, out float rockDeg, out float slideM)
        {
            local.y = 0f; if (local.sqrMagnitude < 1e-6f) local = Vector3.forward; local.Normalize();
            float a2 = local.z * local.z, c2 = local.x * local.x;
            rockDeg = BodyRockFor(type) * RecoilFeel.HullTilt(type, ZKind.Vehicle) * (ShowRockGain(type) * a2 + ShowRockAcrossGain(type) * c2);   // k112, item 151: the across part with its own gain
            slideM = DesignBodySlideAcross(type) * (ShowSlideGain(type) * a2 + c2);
        }
    }
}
