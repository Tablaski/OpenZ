using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OpenZ
{
    /// <summary>
    /// 1.2 owner item 168 ("tracks look like they are turning when i zoom in and zoom out ... but when not zooming they are normally static").
    /// <para>What does NOT move: the tread scroll. VehicleMotion.Step draws the belts' scroll from the distance the hull drives along itself
    /// (VehicleSlip.AlongSpeed, 0 for a standing tank) and its turn (the hull's yaw rate, 0 for a standing tank); no camera value, zoom,
    /// distance or level of detail reaches it, and no other code writes a belt's _BaseMap_ST (View/OzK103Check.cs counts it in Play).</para>
    /// <para>What the eye sees: the v2 belt pictures are a fine repeating pattern, 12 track shoes in each 4 m of belt (0.33 m a shoe): about
    /// 6-7 pixels a shoe at the 70 m default height, 2-3 at the widest zoom. A wheel notch eases the height by 15 % over about 0.2 s
    /// (RtsCamera.wheelStep, zoomSmooth), so the picture of a tank away from the screen centre jumps by up to half a shoe and more in ONE
    /// frame (one notch at 60 frames a second: half a shoe 600 px from the centre at 70 m, 300 px at 140 m; a fast spin or a lower frame rate:
    /// a shoe or more; the kit's tools/zoom_alias.py). Then each drawn shoe lands nearer its neighbour's old place than its own and the eye
    /// pairs them wrongly: the tread seems to run along the hull (the wagon-wheel effect) while the hull, which has no repeating pattern,
    /// stands. As soon as the zoom stops the picture is still and the tread is seen to stand.</para>
    /// <para>Fix, view only (the scroll and the rules untouched): while the ZOOM moves the picture of a belt along that belt by more than
    /// <see cref="BlurFrom"/> of a shoe in a frame, the belt's shoe pattern is softened toward its own average colour and relief (TeamLit's
    /// own de-grime blend to a blurred mip, _Degrime / _DegrimeLod, and _BumpScale, in each material slot's own block, keeping every other
    /// value there), as a camera's shutter would smear it: fully at <see cref="BlurFull"/> of a shoe a frame, back to the sharp belt over
    /// <see cref="ReleaseS"/> s once the zoom slows. Only the zoom counts: a ground point under the belt is projected with this frame's view
    /// and with this frame's view moved back to last frame's distance from the RTS camera's focus (RtsCamera). A pan, a screen shake, a
    /// flyby's camera or a tank driving under a still camera soften nothing: its tread stays sharp and rolls. Belts of the scrolling belt
    /// materials only (VehicleMotion.ScrollMaterials: the v2 belts and the medium tank); v1 belts sit in shared texture sheets.
    /// Switch: <see cref="Enabled"/> (off = drawn as before).</para>
    /// <para>
    /// Standing vehicles only. While a vehicle drives or turns (VehicleMotion.TreadsRolling) its belts are never softened: they
    /// roll and are drawn exactly as when the camera does not zoom, at every zoom speed (a belt that was soft goes sharp at once).
    /// <see cref="RollingSkipped"/> counts the belt frames left alone that way.</para>
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public sealed class OzTreadSteady : MonoBehaviour
    {
        /// <summary>The switch (off = the belts drawn as before, also while the camera moves).</summary>
        public static bool Enabled = true;
        public static bool AutoCreate = true;
        /// <summary>Shoes a frame (along the belt, on screen) where the softening starts and where it is full; seconds it takes to ease out.</summary>
        public static float BlurFrom = 0.2f, BlurFull = 0.45f, ReleaseS = 0.15f;
        /// <summary>The blurred mip the soft belt is read from (the 256 x 1024 belt pictures: mip 7 = 2 x 8 texels, under one texel a shoe)
        /// and how much of the shoes' relief stays when fully soft.</summary>
        public static float BlurLod = 7f, BumpKeep = 0.15f;
        /// <summary>The belt pictures: 12 shoes a texture tile; VehicleMotion scrolls 0.25 tile a metre (a tile = 4 m of belt).</summary>
        public const float ShoesPerTile = 12f, TileM = 4f;
        public static float ShoeM => TileM / ShoesPerTile;

        public static OzTreadSteady I { get; private set; }
        /// <summary>Statistics (the counted check): frames with any belt softened, the largest softening, the most shoes a frame measured.</summary>
        public static int FramesSoft;
        /// <summary>Run 11: belt frames left sharp (while the camera zoomed) because their vehicle was rolling.</summary>
        public static int RollingSkipped;
        public static float MaxSoft, MaxShoesPerFrame;

        sealed class Belt { public Renderer r; public VehicleMotion vm; public int id, slots; public float k, written; public float[] deg, lod, bump; }
        readonly List<Belt> belts = new List<Belt>();
        readonly Dictionary<int, Belt> byId = new Dictionary<int, Belt>();
        readonly List<Renderer> scratch = new List<Renderer>();
        MaterialPropertyBlock mpb;
        bool havePrev; Camera prevCam; RtsCamera rts; float prevDist, nextScan;
        static readonly int DegrimeId = Shader.PropertyToID("_Degrime"), DegrimeLodId = Shader.PropertyToID("_DegrimeLod"), BumpId = Shader.PropertyToID("_BumpScale");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot() { SceneManager.sceneLoaded -= OnSceneLoaded; SceneManager.sceneLoaded += OnSceneLoaded; }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!AutoCreate || I) return;
            if (!ZGame.I && !FindFirstObjectByType<ZGame>()) return;                                  // game levels only (as UnitLookMotionGate)
            new GameObject("OzTreadSteady").AddComponent<OzTreadSteady>();
        }

        void Awake()
        {
            if (I && I != this) { Destroy(gameObject); return; }
            I = this;
        }

        void OnDestroy() { if (I == this) I = null; }

        /// <summary>How soft the belt r is drawn now (0 = sharp; -1 = not a belt this component looks after).</summary>
        public static float SoftOf(Renderer r) => I && r && I.byId.TryGetValue(r.GetInstanceID(), out var b) ? b.k : -1f;

        /// <summary>Every second: the belts of every vehicle whose belt material scrolls (new vehicles join within a second).</summary>
        void Scan()
        {
            foreach (var vm in OzViewScans.VehicleMotions())                                 // 1.4 speed-up 4: the same vehicles from a registry (View/OzViewScans.cs); each is looked after on its own
            {
                vm.TrackRenderers(scratch);
                foreach (var r in scratch)
                {
                    if (!r || byId.ContainsKey(r.GetInstanceID()) || !VehicleMotion.ScrollsBelt(r)) continue;
                    var mats = r.sharedMaterials; int n = mats.Length;
                    var b = new Belt { r = r, vm = vm, id = r.GetInstanceID(), slots = n, deg = new float[n], lod = new float[n], bump = new float[n] };
                    for (int i = 0; i < n; i++)
                    {
                        var m = mats[i];
                        b.deg[i] = m && m.HasProperty(DegrimeId) ? m.GetFloat(DegrimeId) : 0f;
                        b.lod[i] = m && m.HasProperty(DegrimeLodId) ? m.GetFloat(DegrimeLodId) : 2.5f;
                        b.bump[i] = m && m.HasProperty(BumpId) ? m.GetFloat(BumpId) : 1f;
                    }
                    belts.Add(b); byId[b.id] = b;
                }
            }
            scratch.Clear();
        }

        static bool Px(Matrix4x4 vp, Vector3 p, float w, float h, out Vector2 px)
        {
            var c = vp * new Vector4(p.x, p.y, p.z, 1f);
            if (c.w < 1e-3f) { px = default; return false; }                                         // behind the camera
            px = new Vector2((c.x / c.w * 0.5f + 0.5f) * w, (c.y / c.w * 0.5f + 0.5f) * h);
            return true;
        }

        void LateUpdate()
        {
            if (Time.unscaledTime >= nextScan) { nextScan = Time.unscaledTime + 1f; Scan(); }
            var cam = Camera.main;
            bool camOk = cam && cam.isActiveAndEnabled;
            if (camOk && (prevCam != cam || !rts)) rts = cam.GetComponent<RtsCamera>();
            camOk = camOk && rts && rts.isActiveAndEnabled;                                             // the RTS view only (a flyby's or a cinematic's camera: nothing softens)
            var vp = camOk ? cam.projectionMatrix * cam.worldToCameraMatrix : Matrix4x4.identity;
            float dist = camOk ? Vector3.Distance(cam.transform.position, rts.focus) : 0f;
            bool measure = Enabled && camOk && havePrev && prevCam == cam && Mathf.Abs(prevDist - dist) > 1e-4f;
            // this frame's view moved back along its axis to last frame's distance from the focus: the zoom alone (no pan, no shake)
            var prevVP = measure ? cam.projectionMatrix * (cam.worldToCameraMatrix * Matrix4x4.Translate(cam.transform.forward * (prevDist - dist))) : vp;
            float rel = 1f - Mathf.Exp(-Mathf.Max(Time.unscaledDeltaTime, 1e-4f) / Mathf.Max(0.01f, ReleaseS));
            float w = camOk ? cam.pixelWidth : 1f, h = camOk ? cam.pixelHeight : 1f;
            bool anySoft = false;
            for (int i = belts.Count - 1; i >= 0; i--)
            {
                var b = belts[i];
                if (!b.r) { byId.Remove(b.id); belts.RemoveAt(i); continue; }
                float want = 0f;
                bool rolling = b.vm && b.vm.TreadsRolling;                                              // run 11: a driving / turning vehicle's belts: as without zoom
                if (rolling)
                {
                    if (measure) RollingSkipped++;
                    b.k = 0f;
                    if (b.written != 0f) Write(b);
                    continue;
                }
                if (measure && b.r.isVisible)
                {
                    var bb = b.r.bounds; var c = new Vector3(bb.center.x, bb.min.y, bb.center.z);      // a ground point under the belt (it stands still on the ground)
                    var a = b.r.transform.TransformDirection(Vector3.forward); a.y = 0f;                // the belt's length (its mesh runs along z)
                    if (a.sqrMagnitude < 1e-8f) a = Vector3.forward;
                    a.Normalize();
                    if (Px(vp, c, w, h, out var p0) && Px(vp, c + a * ShoeM, w, h, out var p1) && Px(prevVP, c, w, h, out var q0))
                    {
                        var shoe = p1 - p0; float sl = shoe.magnitude;
                        if (sl > 1e-3f)
                        {
                            float shoes = Mathf.Abs(Vector2.Dot(p0 - q0, shoe / sl)) / sl;           // this frame's zoom move of the belt's picture, along the belt, in shoes
                            if (shoes > MaxShoesPerFrame) MaxShoesPerFrame = shoes;
                            want = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(BlurFrom, BlurFull, shoes));
                        }
                    }
                }
                b.k = want >= b.k ? want : Mathf.Lerp(b.k, want, rel);                                // at once while the picture runs, eased back
                if (b.k < 0.002f) b.k = 0f;
                if (b.k > 0f) { anySoft = true; if (b.k > MaxSoft) MaxSoft = b.k; }
                if (Mathf.Abs(b.k - b.written) > 0.002f || (b.k == 0f && b.written != 0f)) Write(b);
            }
            if (anySoft) FramesSoft++;
            if (camOk) { prevDist = dist; havePrev = true; prevCam = cam; } else { havePrev = false; prevCam = null; }
        }

        /// <summary>The softening into each material slot's own block of the belt (the block the draw uses, VehicleMotion.Tread.cs), keeping
        /// every other value there; 0 = the material's own values exactly.</summary>
        void Write(Belt b)
        {
            mpb ??= new MaterialPropertyBlock();
            float k = b.k; int slots = Mathf.Min(b.slots, b.r.sharedMaterials.Length);              // a wreck may have changed its materials since
            for (int i = 0; i < slots; i++)
            {
                b.r.GetPropertyBlock(mpb, i);
                mpb.SetFloat(DegrimeId, Mathf.Lerp(b.deg[i], 1f, k));
                mpb.SetFloat(DegrimeLodId, k > 0f ? BlurLod : b.lod[i]);
                mpb.SetFloat(BumpId, b.bump[i] * Mathf.Lerp(1f, BumpKeep, k));
                b.r.SetPropertyBlock(mpb, i);
            }
            b.written = k;
        }
    }
}
