// OpenZ (GPL-3.0) - version 1.5, level 18 briefing stage: the middle parade's BANNER (PLAY AS BLUE only).
// with a cross bar, the cloth hanging from it face forward (the picture is the game's own EvilZod portrait art on team blue:
// Resources/OpenZ/L18Cut/L18_ParadeBanner). He holds the pole upright in both hands in front of his chest (his arms are bent to it
// after his walking clip has posed him: two-bone, elbows down and out, inside a person's reach); the pole leans a little into the
// march and nods with his steps; the cloth's hem trails back and ripples (the mesh itself moves, a formula of the clock).
// THE PAY-OFF: when his bridge's charge throws him the banner flies up alone, turns over once and comes down flat on the red bank just
// past the bridge's south end, picture up, its top away from our side: what is left of the parade.
// A pure function of the clock (a held frame shows the same banner). Nothing of it survives the hand-back.
using UnityEngine;
using UnityEngine.Rendering;

namespace OpenZ
{
    public sealed partial class OzL18BriefingStage
    {
        public const string BannerTexRes = "OpenZ/L18Cut/L18_ParadeBanner";
        /// <summary>The banner: the pole's top over the ground (m), its butt over the ground in his hands, the cloth (m), the pole's lean into the
        /// march and its nod (deg), the hem's trail (deg) and the ripple (m), how far ahead of him the pole stands.</summary>
        public static float PoleTop = 10.2f, PoleButt = 0.75f, ClothWide = 4.0f, ClothHigh = 6.0f, BannerLean = 4f, BannerNod = 1.3f, ClothTrail = 10f, ClothWave = 0.2f, PoleAhead = 0.62f;
        /// <summary>The flight after the blast: seconds in the air, the apex over the straight line (m), where the pole's butt comes to rest
        /// from the deck's south end (m: east, up, north) and the turn it lies at (deg).</summary>
        public static float BannerFly = 2.7f, BannerApex = 13f, BannerLandYaw = 8f; public static Vector3 BannerLand = new Vector3(-5.2f, 0.2f, -1.6f);
        /// <summary>The cloth's nearest distance in front of the pole's middle line (m; the pole is 0.065 thick).</summary>
        public static float ClothOffPole = 0.085f;
        /// <summary>The cloth's own lift on the material (the dawn turns team blue dusty): 1 = the picture as painted.</summary>
        public static float BannerBright = 1.25f;
        /// <summary>Play check: the banner's state ("none", "carried", "in the air", "on the red bank").</summary>
        public static string BannerState = "none";

        sealed class BannerRig
        {
            public Transform root; public Mesh mesh; public Vector3[] pos; public Material cloth, steel, gold; public Car owner;
            public Transform upR, loR, haR, upL, loL, haL; public bool landed; public int nx, ny;
        }
        BannerRig banner;

        static Mesh poleMesh;
        /// <summary>A closed cylinder along +Y from 0 to 1, radius 1 (scaled by its transform).</summary>
        static Mesh PoleMesh()
        {
            if (poleMesh) return poleMesh;
            const int n = 10; var v = new Vector3[n * 2 + 2]; var tr = new int[n * 12];
            for (int i = 0; i < n; i++) { float a = i * Mathf.PI * 2f / n; v[i] = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)); v[n + i] = new Vector3(Mathf.Cos(a), 1f, Mathf.Sin(a)); }
            v[n * 2] = Vector3.zero; v[n * 2 + 1] = Vector3.up; int t = 0;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                tr[t++] = i; tr[t++] = n + i; tr[t++] = j; tr[t++] = j; tr[t++] = n + i; tr[t++] = n + j;
                tr[t++] = n * 2; tr[t++] = i; tr[t++] = j; tr[t++] = n * 2 + 1; tr[t++] = n + j; tr[t++] = n + i;
            }
            poleMesh = new Mesh { name = "L18BannerPole" }; poleMesh.vertices = v; poleMesh.triangles = tr; poleMesh.RecalculateNormals(); poleMesh.RecalculateBounds();
            return poleMesh;
        }

        static Material LitOf(Color c, float smooth, float metal)
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit"); if (!sh) return null;
            var m = new Material(sh); m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", metal);
            return m;
        }

        Transform Rod(Transform parent, string name, Vector3 from, Vector3 to, float radius, Material m)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = PoleMesh(); var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = m; r.shadowCastingMode = ShadowCastingMode.On;
            Vector3 d = to - from; go.transform.localPosition = from; go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, d.normalized); go.transform.localScale = new Vector3(radius, d.magnitude, radius);
            return go.transform;
        }

        void BuildBanner(Car car)
        {
            DisposeBanner();
            var b = new BannerRig { owner = car, nx = 10, ny = 16 };
            b.root = new GameObject("L18ParadeBanner").transform; b.root.SetParent(root, false);
            b.steel = LitOf(new Color(0.16f, 0.17f, 0.19f), 0.45f, 0.7f); b.gold = LitOf(new Color(0.83f, 0.62f, 0.18f), 0.62f, 0.9f);
            float len = PoleTop - PoleButt, bar = len - 0.35f;
            Rod(b.root, "Pole", Vector3.zero, new Vector3(0f, len, 0f), 0.065f, b.steel);
            Rod(b.root, "Bar", new Vector3(-ClothWide * 0.5f - 0.22f, bar, 0f), new Vector3(ClothWide * 0.5f + 0.22f, bar, 0f), 0.05f, b.gold);
            Rod(b.root, "TipA", new Vector3(0f, len, 0f), new Vector3(0f, len + 0.3f, 0f), 0.17f, b.gold);
            Rod(b.root, "TipB", new Vector3(0f, len + 0.3f, 0f), new Vector3(0f, len + 0.62f, 0f), 0.08f, b.gold);
            Rod(b.root, "KnobL", new Vector3(-ClothWide * 0.5f - 0.36f, bar, 0f), new Vector3(-ClothWide * 0.5f - 0.2f, bar, 0f), 0.09f, b.gold);
            Rod(b.root, "KnobR", new Vector3(ClothWide * 0.5f + 0.2f, bar, 0f), new Vector3(ClothWide * 0.5f + 0.36f, bar, 0f), 0.09f, b.gold);
            // ---- the cloth: a grid that hangs from the bar, the picture's upright 2:3 area on it (u 0.167 .. 0.833)
            int nx = b.nx, ny = b.ny; var verts = new Vector3[(nx + 1) * (ny + 1)]; var uv = new Vector2[verts.Length]; var tris = new int[nx * ny * 6];
            for (int j = 0; j <= ny; j++)
                for (int i = 0; i <= nx; i++) { int n = j * (nx + 1) + i; float u = i / (float)nx, v = j / (float)ny; uv[n] = new Vector2(Mathf.Lerp(0.167f, 0.833f, u), 1f - v); verts[n] = new Vector3((u - 0.5f) * ClothWide, bar - v * ClothHigh, 0.1f); }
            int t = 0;
            for (int j = 0; j < ny; j++)
                for (int i = 0; i < nx; i++) { int a = j * (nx + 1) + i, c = a + 1, d = a + nx + 1, e = d + 1; tris[t++] = a; tris[t++] = c; tris[t++] = d; tris[t++] = c; tris[t++] = e; tris[t++] = d; }
            b.mesh = new Mesh { name = "L18BannerCloth" }; b.mesh.MarkDynamic(); b.mesh.vertices = verts; b.mesh.uv = uv; b.mesh.triangles = tris; b.mesh.RecalculateNormals();
            b.mesh.bounds = new Bounds(new Vector3(0f, bar - ClothHigh * 0.5f, 0f), new Vector3(ClothWide + 3f, ClothHigh + 3f, 6f));
            b.pos = verts;
            var tex = Resources.Load<Texture2D>(BannerTexRes);
            if (!tex) Warn("the parade banner's picture Resources/" + BannerTexRes + " is missing: a plain blue cloth");
            b.cloth = LitOf(tex ? new Color(BannerBright, BannerBright, BannerBright, 1f) : new Color(0.18f, 0.31f, 0.66f), 0.2f, 0f);
            if (b.cloth)
            {
                if (tex) b.cloth.SetTexture("_BaseMap", tex);
                b.cloth.SetFloat("_Cull", 0f); b.cloth.SetFloat("_AlphaClip", 1f); b.cloth.SetFloat("_Cutoff", 0.5f); b.cloth.EnableKeyword("_ALPHATEST_ON"); b.cloth.renderQueue = (int)RenderQueue.AlphaTest;
                b.cloth.doubleSidedGI = true;
            }
            var cg = new GameObject("Cloth"); cg.transform.SetParent(b.root, false);
            cg.AddComponent<MeshFilter>().sharedMesh = b.mesh; var cr = cg.AddComponent<MeshRenderer>(); cr.sharedMaterial = b.cloth; cr.shadowCastingMode = ShadowCastingMode.TwoSided;
            // ---- his arms and his rifle: both hands are the pole's
            if (car.anim && car.anim.isHuman)
            {
                b.upR = car.anim.GetBoneTransform(HumanBodyBones.RightUpperArm); b.loR = car.anim.GetBoneTransform(HumanBodyBones.RightLowerArm); b.haR = car.anim.GetBoneTransform(HumanBodyBones.RightHand);
                b.upL = car.anim.GetBoneTransform(HumanBodyBones.LeftUpperArm); b.loL = car.anim.GetBoneTransform(HumanBodyBones.LeftLowerArm); b.haL = car.anim.GetBoneTransform(HumanBodyBones.LeftHand);
            }
            // both hands are the pole's: his gun is put away and nothing else poses his arms (the Psycho's own gun hold and stance)
            foreach (var mb in car.root.GetComponentsInChildren<MonoBehaviour>(true)) if (mb is WeaponHold || mb is BS01Stance) mb.enabled = false;
            foreach (var tr in car.model.GetComponentsInChildren<Transform>(true)) if (tr.name == "RightHandWeapon" || tr.name == "Rifle_Heavy" || tr.name == "Bazooka" || tr.name.StartsWith("Rifle") || tr.name.StartsWith("BS01_Weapon")) tr.gameObject.SetActive(false);
            b.root.gameObject.SetActive(false);
            banner = b; BannerState = "none";
        }

        void DisposeBanner()
        {
            if (banner == null) return;
            if (banner.root) Destroy(banner.root.gameObject);
            if (banner.mesh) Destroy(banner.mesh);
            if (banner.cloth) Destroy(banner.cloth); if (banner.steel) Destroy(banner.steel); if (banner.gold) Destroy(banner.gold);
            banner = null; BannerState = "none";
        }

        /// <summary>The pole's butt and its turn while he carries it, at the clock's second (he stands at `at`, turned `rot`).</summary>
        void BannerCarryPose(Vector3 at, Quaternion rot, float t, out Vector3 pos, out Quaternion r)
        {
            float step = t * Mathf.PI * 2f * 1.55f;
            pos = at + rot * new Vector3(0f, PoleButt + 0.03f * Mathf.Sin(step * 2f), PoleAhead);
            r = rot * Quaternion.Euler(BannerLean + BannerNod * Mathf.Sin(step * 2f + 0.6f), 0f, 0.8f * Mathf.Sin(step));
        }

        /// <summary>PlaceCars, each frame he walks: the banner in his hands, his arms on the pole.</summary>
        void CarryBanner(Car c, float t)
        {
            var b = banner; if (b == null || b.owner != c || !b.root) return;
            BannerCarryPose(c.root.position, c.root.rotation, t, out var p, out var r);
            if (!b.root.gameObject.activeSelf) b.root.gameObject.SetActive(true);
            b.root.SetPositionAndRotation(p, r); b.landed = false; BannerState = "carried";
            ClothShape(b, t, 1f, ClothTrail + 3f * Mathf.Sin(t * 2.1f), 1f);
            // the hands: the right one low on the pole, the left one above it; elbows down and out (after his clip has posed him this frame)
            Vector3 side = c.root.rotation * Vector3.right, down = Vector3.down;
            if (b.upR && b.loR && b.haR) TwoBone(b.upR, b.loR, b.haR, b.root.TransformPoint(new Vector3(0.05f, 0.42f, -0.02f)), side * 0.8f + down);
            if (b.upL && b.loL && b.haL) TwoBone(b.upL, b.loL, b.haL, b.root.TransformPoint(new Vector3(-0.05f, 0.98f, -0.02f)), -side * 0.8f + down);
        }

        /// <summary>A two-bone reach: the elbow bends to the distance, the upper arm swings the hand onto the target, the elbow turns to `pole`.
        /// The reach is capped just short of the straight arm (never a locked elbow).</summary>
        static void TwoBone(Transform upper, Transform lower, Transform hand, Vector3 target, Vector3 pole)
        {
            Vector3 a = upper.position, b = lower.position, c = hand.position;
            float lab = (b - a).magnitude, lcb = (c - b).magnitude; if (lab < 1e-4f || lcb < 1e-4f) return;
            Vector3 at = target - a; float lat = Mathf.Clamp(at.magnitude, Mathf.Abs(lab - lcb) + 0.02f, (lab + lcb) * 0.97f);
            float want = Mathf.Acos(Mathf.Clamp((lab * lab + lcb * lcb - lat * lat) / (2f * lab * lcb), -1f, 1f)) * Mathf.Rad2Deg, cur = Vector3.Angle(a - b, c - b);
            Vector3 axis = Vector3.Cross(a - b, c - b); if (axis.sqrMagnitude < 1e-8f) axis = Vector3.Cross(a - b, pole); if (axis.sqrMagnitude < 1e-8f) return;
            lower.rotation = Quaternion.AngleAxis(want - cur, axis.normalized) * lower.rotation;
            c = hand.position; upper.rotation = Quaternion.FromToRotation(c - a, at) * upper.rotation;
            b = lower.position; c = hand.position; Vector3 n = (c - a).normalized;
            Vector3 eb = Vector3.ProjectOnPlane(b - a, n), ep = Vector3.ProjectOnPlane(pole, n);
            if (eb.sqrMagnitude > 1e-6f && ep.sqrMagnitude > 1e-6f) upper.rotation = Quaternion.AngleAxis(Vector3.SignedAngle(eb, ep, n), n) * upper.rotation;
        }

        /// <summary>The cloth's vertices at the clock's second: hanging from the bar, the hem trailed back by `trail` degrees, rippling by
        /// `wave` (1 = the march's own); `hang` 1 = hanging, 0 = lying flat (the trail and the big waves are gone, small folds stay).</summary>
        void ClothShape(BannerRig b, float t, float hang, float trail, float wave)
        {
            int nx = b.nx, ny = b.ny; float bar = PoleTop - PoleButt - 0.35f, tn = Mathf.Tan(trail * Mathf.Deg2Rad) * hang;
            for (int j = 0; j <= ny; j++)
                for (int i = 0; i <= nx; i++)
                {
                    int n = j * (nx + 1) + i; float u = i / (float)nx, v = j / (float)ny, s = v * v;
                    float w = ClothWave * wave * (v * Mathf.Sin(6.2832f * (1.25f * t - 1.1f * v) + 2.6f * u) + 0.45f * s * Mathf.Sin(6.2832f * (0.7f * t + 0.35f * u) + 4.1f * v));
                    float folds = 0.035f * (1f - hang) * Mathf.Sin(9f * u + 5f * v) * Mathf.Sin(7f * v + 1.3f);
                    float sway = 0.14f * hang * s * Mathf.Sin(6.2832f * 0.45f * t + 0.8f);
                    float x = (u - 0.5f) * ClothWide + sway, z = 0.1f - ClothHigh * s * tn + w * Mathf.Lerp(0.15f, 1f, hang) + folds;
                    // the cloth hangs in FRONT of the pole: where the trailing hem meets the pole it lies against it (the march's own air presses it on),
                    // its two wings trail on behind: never through the pole
                    z = Mathf.Lerp(z, Mathf.Max(z, ClothOffPole), 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.18f, 1.2f, Mathf.Abs(x))));
                    b.pos[n] = new Vector3(x, bar - v * ClothHigh, z);
                }
            b.mesh.vertices = b.pos; b.mesh.RecalculateNormals();
        }

        /// <summary>PlaceCars, each frame after the units: once he is thrown the banner flies its own way and comes down on the red bank.</summary>
        void TickBanner(float t, bool forward)
        {
            var b = banner; if (b == null || !b.root || b.owner == null) return;
            var c = b.owner;
            if (!c.hasThrow || t < c.launchAt) return;                                                    // he carries it (CarryBanner wrote this frame)
            var sp = spans[c.span];
            RoadPose(sp, RoadAt(c, sp, c.launchAt), out var at, out var rot);
            BannerCarryPose(at, rot, c.launchAt, out var p0, out var r0);
            float u = t - c.launchAt, T = Mathf.Max(0.5f, BannerFly), s = Mathf.Clamp01(u / T), e = 1f - (1f - s) * (1f - s);
            float zS = sp.c.z - sp.half; float gx = sp.c.x + BannerLand.x, gz = zS + BannerLand.z;
            Vector3 land = new Vector3(gx, game.GroundY(gx, gz) + BannerLand.y, gz);
            Vector3 p = Vector3.Lerp(p0, land, e) + Vector3.up * (BannerApex * 4f * s * (1f - s));
            float turn = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(s / 0.92f));
            // over forward once, then flat: the pole's top away from our bank, the picture's back side up (the same picture, mirrored)
            Quaternion r = Quaternion.Euler(0f, rot.eulerAngles.y + BannerLandYaw * s, 0f) * Quaternion.Euler(Mathf.Lerp(BannerLean, 450f, turn), 0f, 6f * Mathf.Sin(s * Mathf.PI));
            if (!b.root.gameObject.activeSelf) b.root.gameObject.SetActive(true);
            b.root.SetPositionAndRotation(p, r);
            float hang = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 0.98f, s));
            ClothShape(b, t, hang, ClothTrail * (1f - s), 1f + 2.2f * Mathf.Sin(s * Mathf.PI));
            bool down = s >= 1f; BannerState = down ? "on the red bank" : "in the air";
            if (down && !b.landed && forward)
            {
                Play("brief_l18_banner_land", "sd_body_whoosh_a", GainBannerLand, 1f, false, 0.8f);
                var fx = FxManager.I; if (fx) fx.Dust(land + Vector3.forward * -4f, 0.5f);
            }
            if (!down && b.landed) { }                                                                    // the clock went back: it flies again
            b.landed = down;
        }
    }
}
