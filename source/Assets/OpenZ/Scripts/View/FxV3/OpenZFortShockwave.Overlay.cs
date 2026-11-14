// OpenZ (GPL-3.0)
// 45 "shockwave from nuke - it's sides too ringish.. needs more motion/blur transparency etc so its not looking like exact ring but more
//    subtle": the edge more transparent, a little noise, not a hard circle (his idea picture: a soft glowing ring with radial streaks).
// 46 "sometimes the wave goes over the mountains and breaks ... draw it on top as a render, do not bend it over the mountains".
// Before: the bright front and the orange trail were two meshes of rows laid ON the ground along the ground (item 215, Surface.cs). Where
// the front crossed a ridge it climbed, fell behind and was lifted along the slope, so on screen the ring folded into kinks; each band had
// a zero row just inside and just outside it, so its edges were hard lines.
// Now (OverlayRing on): one FLAT annulus at the blast's floor (centre.y + OverlayLift), drawn with OpenZ/ShockOverlay with ZTest Always:
// no hill, cliff or ruin cuts it or folds it, so it stays one smooth curve over every mountain, and it lies right on flat ground. The
// look is in the shader (OzShockOverlay.shader): a soft core broken up along the ring, a warm trail with radial streaks that ride outward
// with the front, short streaks ahead, a little noise on the edge, a slight smooth wobble. Its radius is RadiusAt (the clock of the shove,
// the deaths, the dust and the boom); its brightness fades with the radius as the old ring's did (k^1.3 front, k^2.2 trail, the same
// fade-in); per direction it fades where the level's ground ends (Level 16's apron), as the old ring stopped there.
// Untouched: the refraction wall, the ground shimmer, the area shimmer, the dust wall, the shove, the jolts, the trees, the sound, the
// shake, the timing, the damage. SurfaceRing still lays those over the ground. OverlayRing false before the blast = the ring of item 215,
// as before (the before / after frames, and the way back).
// Play check: return OpenZ.OpenZFortShockwave.OverlayStatus();
using UnityEngine;
using UnityEngine.Rendering;

namespace OpenZ
{
    public sealed partial class OpenZFortShockwave
    {
        /// <summary>On = the shock ring is one soft flat overlay drawn on top (rows 45, 46); off = the ring over the ground of item 215, as before.
        /// Read when a wave starts.</summary>
        public static bool OverlayRing = true;
        /// <summary>Peak gains of the front and of the trail (x the old fades k^1.3, k^2.2), the height over the blast's floor (m), and the metres
        /// over which a direction fades where the level's ground ends.</summary>
        public static float OverlayFrontGain = 2.6f, OverlayTrailGain = 2f, OverlayLift = 0.3f, OverlayEndFade = 8f;
        /// <summary>The look, read when a wave starts (shares of the radius unless said): core softness behind and ahead of the front, trail
        /// fall-off behind it, streak reach ahead, streak density (about 6.3 x this many round the ring), streak strength, core break-up, edge
        /// noise, front wobble.</summary>
        public static float OverlayCoreIn = 0.03f, OverlayCoreOut = 0.018f, OverlayTrailLen = 0.07f, OverlayGlowLen = 0.025f, OverlayStreakN = 34f,
                            OverlayStreakAmt = 0.85f, OverlayCoreBreak = 0.65f, OverlayNoiseAmt = 0.45f, OverlayWobble = 0.012f;
        /// <summary>For the checks: waves drawn as the overlay since load, the share of directions faded at the end of the ground now.</summary>
        public static int OverlayWaves { get; private set; }
        public static float OverlayFadedShare { get; private set; }

        const float OverlayInnerU = 0.62f, OverlayOuterU = 1.1f; const int OverlaySeg = 240;
        static readonly int OColorId = Shader.PropertyToID("_Color"), OTrailId = Shader.PropertyToID("_TrailColor"), OCenterId = Shader.PropertyToID("_RingCenter"),
                            ORadiusId = Shader.PropertyToID("_RingRadius"), OAgeId = Shader.PropertyToID("_Age");
        bool overlayOn; Mesh oMesh; Material oMat; Transform oTr; Color[] oC;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOverlay() { OverlayWaves = 0; OverlayFadedShare = 0f; }

        /// <summary>Build: the overlay's material, mesh and renderer; the old ring's two renderers are switched off while it draws.
        /// No shader (or not supported here) = the old ring, as before.</summary>
        void OverlayBuild()
        {
            overlayOn = false;
            if (!OverlayRing) return;
            var src = Resources.Load<Material>("OpenZ/FX/M_OzShockOverlay");
            var sh = src && src.shader ? src.shader : Shader.Find("OpenZ/ShockOverlay");
            if (!sh || !sh.isSupported) return;
            oMat = src && src.shader == sh ? new Material(src) : new Material(sh);
            oMat.name = "ShockOverlay (runtime)"; oMat.renderQueue = 3072;                              // where the old front drew
            oMat.SetFloat("_Seed", Mathf.Repeat(centre.x * 0.137f + centre.z * 0.071f + t0 * 3.1f, 97f));
            oMat.SetFloat("_CoreIn", OverlayCoreIn); oMat.SetFloat("_CoreOut", OverlayCoreOut); oMat.SetFloat("_TrailLen", OverlayTrailLen);
            oMat.SetFloat("_GlowLen", OverlayGlowLen); oMat.SetFloat("_StreakN", OverlayStreakN); oMat.SetFloat("_StreakAmt", OverlayStreakAmt);
            oMat.SetFloat("_CoreBreak", OverlayCoreBreak); oMat.SetFloat("_NoiseAmt", OverlayNoiseAmt); oMat.SetFloat("_Wobble", OverlayWobble);
            oMat.SetFloat("_InnerU", OverlayInnerU); oMat.SetFloat("_OuterU", OverlayOuterU);
            oMat.SetFloat("_RingRadius", 4f); oMat.SetFloat("_Age", 0f); oMat.SetVector(OCenterId, centre);
            oMat.SetVector(OColorId, Vector4.zero); oMat.SetVector(OTrailId, Vector4.zero);

            // a flat annulus of unit radius (its outer edge round the circle it must cover), scaled to the radius each frame
            var v = new Vector3[(OverlaySeg + 1) * 2]; oC = new Color[v.Length]; var tri = new int[OverlaySeg * 6];
            float outer = OverlayOuterU / Mathf.Cos(Mathf.PI / OverlaySeg);
            for (int i = 0; i <= OverlaySeg; i++)
            {
                float a = i * Mathf.PI * 2f / OverlaySeg, c = Mathf.Cos(a), s = Mathf.Sin(a);
                v[i * 2] = new Vector3(c * OverlayInnerU, 0f, s * OverlayInnerU); v[i * 2 + 1] = new Vector3(c * outer, 0f, s * outer);
                oC[i * 2] = oC[i * 2 + 1] = Color.white;
            }
            for (int i = 0, k = 0; i < OverlaySeg; i++)
            {
                int a0 = i * 2, a1 = a0 + 1, b0 = a0 + 2, b1 = a0 + 3;
                tri[k++] = a0; tri[k++] = b0; tri[k++] = a1; tri[k++] = a1; tri[k++] = b0; tri[k++] = b1;
            }
            oMesh = new Mesh { name = "ShockOverlay (runtime)" }; oMesh.MarkDynamic();
            oMesh.vertices = v; oMesh.colors = oC; oMesh.triangles = tri; oMesh.RecalculateBounds();
            var go = new GameObject("ShockOverlay"); go.transform.SetParent(transform, false);
            oTr = go.transform; oTr.position = new Vector3(centre.x, centre.y + OverlayLift, centre.z); oTr.localScale = new Vector3(4f, 1f, 4f);
            go.AddComponent<MeshFilter>().sharedMesh = oMesh;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = oMat; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off; r.reflectionProbeUsage = ReflectionProbeUsage.Off;   // black (nothing added) until its first update
            foreach (var n in new[] { "ShockFront", "ShockTrail" })
            {
                var t = transform.Find(n);
                if (t && t.TryGetComponent<MeshRenderer>(out var old)) old.enabled = false;
            }
            overlayOn = true; OverlayWaves++;
        }

        /// <summary>UpdateRing while the overlay is on: radius, fades, per direction the end of the level's ground. false = not built.</summary>
        bool OverlayRingUpdate(float k)
        {
            if (!oMat || !oTr || !oMesh) return false;
            float R = Mathf.Max(radius, 0.5f);
            oTr.localScale = new Vector3(R, 1f, R);
            if (SurfaceReady())
            {   // the same ground the old ring stopped at (OzBlastGround through the profile): a direction fades as the front passes its end
                int nFaded = 0, shown = 0, up = 0;
                for (int i = 0; i < OverlaySeg; i++)
                {
                    int a = (int)((long)i * pN / OverlaySeg) % pN, o = a * pMax;
                    Extend(a, 0f, R + Mathf.Max(0f, OverlayEndFade) + pStep);                        // read OverlayEndFade m ahead: the end is known before its fade starts (no pop)
                    float alpha = 1f;
                    if (pEnd[a] == 1)
                    {
                        float endX = (pCount[a] - 1) * pStep;
                        alpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(endX - OverlayEndFade, endX, R));
                    }
                    oC[i * 2].a = oC[i * 2 + 1].a = alpha;
                    if (alpha < 0.5f) { nFaded++; continue; }
                    shown++;
                    int n = pCount[a]; float fi = R / pStep; int j = Mathf.Min((int)fi, n - 2);
                    if (j >= 0)
                    {
                        float rise = Mathf.Lerp(pH[o + j], pH[o + j + 1], Mathf.Clamp01(fi - j)) - centre.y;
                        if (rise > 0.9f) up++;
                        if (rise > SurfaceTopMost) SurfaceTopMost = rise;
                    }
                }
                oC[OverlaySeg * 2].a = oC[0].a; oC[OverlaySeg * 2 + 1].a = oC[1].a;
                oMesh.colors = oC;
                OverlayFadedShare = nFaded / (float)OverlaySeg;
                SurfaceShownShare = shown / (float)OverlaySeg;                                        // the checks' "share of the front drawn" (OzKSCheck)
                if (up > SurfaceUpMost) SurfaceUpMost = up;
            }
            float fadeIn = Mathf.Clamp01(age / 0.06f);
            float gf = OverlayFrontGain * Mathf.Pow(k, 1.3f) * fadeIn, gt = OverlayTrailGain * Mathf.Pow(k, 2.2f) * fadeIn;
            oMat.SetVector(OColorId, new Vector4(1f, 0.82f, 0.58f, 1f) * gf);
            oMat.SetVector(OTrailId, new Vector4(1f, 0.34f, 0.07f, 1f) * gt);
            oMat.SetVector(OCenterId, centre); oMat.SetFloat(ORadiusId, R); oMat.SetFloat(OAgeId, age);
            return true;
        }

        void OverlayDestroy()
        {
            if (oMat) Destroy(oMat);
            if (oMesh) Destroy(oMesh);
        }

        /// <summary>For the checks (read only): the wave on screen now. false = none, or it draws the old ring. radius = the overlay's radius,
        /// drawnY = the overlay's height, floorY = the blast's floor, oldOff = the old ring's two renderers are off.</summary>
        public static bool OverlayProbe(out float waveAge, out float drawnRadius, out float drawnY, out float floorY, out bool oldOff)
        {
            var w = last; waveAge = drawnRadius = drawnY = floorY = 0f; oldOff = false;
            if (!w || !w.built || !w.overlayOn || !w.oTr) return false;
            waveAge = w.age; drawnRadius = w.oTr.localScale.x; drawnY = w.oTr.position.y; floorY = w.centre.y; oldOff = true;
            foreach (var n in new[] { "ShockFront", "ShockTrail" })
            {
                var t = w.transform.Find(n);
                if (t && t.TryGetComponent<MeshRenderer>(out var r) && r.enabled) oldOff = false;
            }
            return true;
        }

        /// <summary>For the checks (read only): the boom of the wave on screen has played.</summary>
        public static bool ProbeBoomed => last && last.boomed;

        /// <summary>Play check: the overlay ring in one line.</summary>
        public static string OverlayStatus()
        {
            var w = last;
            string s = $"shock ring overlay: {(OverlayRing ? "on" : "OFF (the ring over the ground of item 215)")}, waves drawn as the overlay {OverlayWaves}, front gain {OverlayFrontGain:0.##}, trail gain {OverlayTrailGain:0.##}, lift {OverlayLift:0.##} m";
            if (w && w.built && !w.faded)
                s += w.overlayOn
                    ? $"; now: age {w.age:0.00} s, radius {w.radius:0.0} m (RadiusAt {RadiusAt(w.age):0.0}), drawn at height {(w.oTr ? w.oTr.position.y : 0f):0.00} m (blast floor {w.centre.y:0.00}), directions faded at the end of the ground {OverlayFadedShare * 100f:0} %, ZTest {(w.oMat ? "Always (shader)" : "-")}"
                    : "; now: this wave draws the old ring";
            Debug.Log("[OpenZ] " + s);
            return s;
        }
    }
}
