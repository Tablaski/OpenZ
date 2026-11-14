using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OpenZ
{
    /// <summary>
    /// DV149 (version 1.2), the view of the mech losing its hand gun (the rule: Sim/ZObject.MechGunLoss.cs). Both models (v1, v2), one code:
    /// <list type="bullet">
    /// <item>THE FALL: the frame the view first sees the loss (within <see cref="FreshS"/> sim seconds of it), the gun's forearm (the node
    /// MechGunRecoil with everything under it: the gun body, the spinning barrel ring, the muzzles) is copied as it is drawn this frame
    /// (meshes, materials, the team paint and burn of each part) into one of the game's wreck pieces (WreckPieces.Single: a rigid body
    /// with gravity and a convex box collider on the debris layer, the ground and the rocks, the debris clank when it lands, it settles,
    /// freezes and sinks into the ground later like every wreck piece), thrown out from the mech's side and a little away from the
    /// blast; a spark burst and a metal snap at the elbow, and a short smoke puff from the stub (<see cref="SmokeS"/>).</item>
    /// <item>THE STUB: the mech's own forearm parts are hidden for good (every renderer under MechGunRecoil, kept hidden every frame; their
    /// colliders off); the upper arm and the elbow stay. The hidden nodes stay in place, so everything that reads them (the aim, the muzzles,
    /// the rocket kick) works as before; the rocket pods are elsewhere on the body and stay. A view made later for a mech that lost its gun
    /// before (a model reload, Zod's repair swap's fresh vehicle) only hides it: nothing falls twice.</item>
    /// </list>
    /// View only: the sim decides the loss; nothing here changes a rule. The random spin of the piece is cosmetic. Switch:
    /// <see cref="Enabled"/> (off = the gun stays drawn but never fires).
    /// </summary>
    [DefaultExecutionOrder(86)]   // after UnitAim (62), the hand gun's view (64) and MechWalk (79) posed the mech this frame
    public sealed class MechGunLossView : MonoBehaviour
    {
        public static bool Enabled = true;
        public static MechGunLossView I { get; private set; }
        /// <summary>A loss first seen within this many sim seconds falls (older: hidden only); the throw's power (WreckPieces.Single, 0..1);
        /// the push away from the blast (m/s); the stub's smoke puff (s) and its beat (s).</summary>
        public static float FreshS = 1.0f, Power = 0.55f, BlastPush = 1.4f, SmokeS = 1.6f, SmokeEvery = 0.2f;
        public const string ForearmNode = "MechGunRecoil", ElbowNode = "MechGunElbow";

        /// <summary>Proof counters (since load): guns that fell, views that only hid a gun lost before, pieces made / not made, mechs whose
        /// model has no forearm node, renderers something else switched on again and this view hid again, views bound (v1 / v2).</summary>
        public static int Drops, HiddenOnly, PiecesMade, PieceFailed, NoNode, Rehidden, Bound, BoundV1, BoundV2;
        /// <summary>One falling gun (the Play check samples them; nothing in a game reads them).</summary>
        public sealed class Fall
        {
            public int mech, model, parts; public float t0, real0, startY, lowY = float.MaxValue, settledAt = -1f; public Transform piece; public Rigidbody rb; public Vector3 v0;
        }
        /// <summary>The last falls, oldest first (at most <see cref="RecentMax"/>).</summary>
        public static readonly List<Fall> Falls = new List<Fall>();
        public const int RecentMax = 24;

        sealed class St
        {
            public UnitView view; public Transform forearm, elbow; public int childCount = -1, frames; public float smokeUntil, nextSmoke;
            public readonly List<Renderer> hidden = new List<Renderer>();
        }
        readonly Dictionary<int, St> bound = new Dictionary<int, St>();
        ZGame game; ZWorld world;
        static readonly MaterialPropertyBlock block = new MaterialPropertyBlock();

        // ------------------------------------------------------------------ bootstrap (as MechHandGunView)
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot() { SceneManager.sceneLoaded -= OnSceneLoaded; SceneManager.sceneLoaded += OnSceneLoaded; }
        static void OnSceneLoaded(Scene s, LoadSceneMode m) { if (ZGame.I || FindFirstObjectByType<ZGame>()) Ensure(); }

        /// <summary>The scene's gun-loss view (created on demand).</summary>
        public static MechGunLossView Ensure()
        {
            if (I && I.isActiveAndEnabled) return I;
            var g = ZGame.I ? ZGame.I : FindFirstObjectByType<ZGame>();
            if (!g) return null;
            var go = new GameObject("MechGunLossView");
            if (g.gameObject.scene.IsValid() && go.scene != g.gameObject.scene) SceneManager.MoveGameObjectToScene(go, g.gameObject.scene);
            var v = go.AddComponent<MechGunLossView>(); v.game = g;
            return v;
        }

        void Awake()
        {
            if (I && I != this) { Destroy(gameObject); return; }
            I = this;
        }

        void OnDestroy() { if (I == this) I = null; }

        /// <summary>Proof: the counters back to 0 (the falls list too).</summary>
        public static void ResetStats() { Drops = HiddenOnly = PiecesMade = PieceFailed = NoNode = Rehidden = Bound = BoundV1 = BoundV2 = 0; Falls.Clear(); }

        static Transform Deep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t) { var f = Deep(c, name); if (f) return f; }
            return null;
        }

        // ------------------------------------------------------------------ every frame, after the mech is posed
        void LateUpdate()
        {
            if (!Enabled) return;
            if (!game) game = ZGame.I;
            if (!game || game.World == null) return;
            if (world != game.World) { world = game.World; bound.Clear(); }                                 // a new level: new units
            float now = Time.time;
            foreach (var kv in game.Views)
            {
                var v = kv.Value; if (!v) continue;
                var o = v.Obj; if (o == null || !o.HandGunLost) continue;
                bool seen = bound.TryGetValue(o.Id, out var st);
                if (!seen || st.view != v)
                {   // a mech without its gun this view has not handled yet: its forearm falls (a fresh loss) or is only hidden
                    st = new St { view = v, forearm = Deep(v.transform, ForearmNode), elbow = Deep(v.transform, ElbowNode) };
                    bound[o.Id] = st;
                    Bound++; if (OzMech.Models.VersionOf(o) == 2) BoundV2++; else BoundV1++;
                    if (!st.forearm) { NoNode++; continue; }
                    if (!seen && world.Time - o.HandGunLostAt <= FreshS && !o.Destroyed && o.KillAt < 0) Drop(o, st, now);
                    else HiddenOnly++;
                    Collect(st);
                }
                Keep(st, now);
            }
            for (int i = Falls.Count - 1; i >= 0; i--)
            {   // the proof's view of each falling gun: its lowest point and when it settled (WreckPieces freezes a resting piece)
                var f = Falls[i];
                if (!f.piece || f.settledAt >= 0f) continue;
                f.lowY = Mathf.Min(f.lowY, f.piece.position.y);
                if (f.rb && (f.rb.isKinematic || (now - f.t0 > 0.3f && f.rb.IsSleeping()))) f.settledAt = now;
            }
        }

        /// <summary>Every renderer under the forearm hidden (a part added under it later too), its colliders off.</summary>
        static void Collect(St st)
        {
            if (!st.forearm) return;
            foreach (var r in st.forearm.GetComponentsInChildren<Renderer>(true))
            {
                if (!st.hidden.Contains(r)) st.hidden.Add(r);
                r.enabled = false;
            }
            foreach (var c in st.forearm.GetComponentsInChildren<Collider>(true)) c.enabled = false;
            st.childCount = st.forearm.childCount;
        }

        void Keep(St st, float now)
        {
            if (!st.forearm) return;
            if (st.forearm.childCount != st.childCount || ++st.frames % 30 == 0) Collect(st);
            for (int i = 0; i < st.hidden.Count; i++)
            {
                var r = st.hidden[i];
                if (r && r.enabled) { r.enabled = false; Rehidden++; }
            }
            if (now < st.smokeUntil && now >= st.nextSmoke && st.elbow)
            {   // the stub smokes a moment, thinner as it ends
                st.nextSmoke = now + SmokeEvery;
                var fxp = OpenZFxPlayer.I;
                if (fxp && fxp.isActiveAndEnabled) fxp.WreckSmoke(st.elbow.position, Mathf.Lerp(0.35f, 0.8f, (st.smokeUntil - now) / Mathf.Max(0.01f, SmokeS)), false);
            }
        }

        /// <summary>The forearm as drawn now becomes a wreck piece: thrown out from the mech's side and away from the blast.</summary>
        void Drop(ZObject o, St st, float now)
        {
            var holder = new GameObject("OZ_LostHandGun");
            holder.transform.SetPositionAndRotation(st.forearm.position, st.forearm.rotation);
            int parts = 0;
            foreach (var mr in st.forearm.GetComponentsInChildren<MeshRenderer>(false))
            {
                if (!mr.enabled || !mr.TryGetComponent<MeshFilter>(out var mf) || !mf.sharedMesh) continue;
                if (mr.sharedMaterial && mr.sharedMaterial.renderQueue >= 2450) continue;                    // glows, decals, flashes: never debris (as WreckPieces)
                var go = new GameObject(mr.name) { layer = mr.gameObject.layer };
                go.transform.SetPositionAndRotation(mr.transform.position, mr.transform.rotation);
                go.transform.localScale = mr.transform.lossyScale;
                go.transform.SetParent(holder.transform, true);
                go.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                CopyLook(mr, go.AddComponent<MeshRenderer>());
                parts++;
            }
            Vector3 at = st.elbow ? st.elbow.position : st.forearm.position;
            Drops++;
            bool made = parts > 0 && WreckPieces.Single(holder.transform, st.view.transform.position, Power, o.Id);
            if (!made)
            {
                PieceFailed++; Destroy(holder);
            }
            else
            {
                PiecesMade++;
                var piece = holder.transform.parent; var rb = piece ? piece.GetComponent<Rigidbody>() : null;
                if (rb)
                {   // a little away from the blast too (the throw itself is out from the mech's side)
                    var away = piece.position - ZGame.PxToWorld(o.HandGunLostFromX, o.HandGunLostFromY); away.y = 0f;
                    if (away.sqrMagnitude > 1e-4f) rb.linearVelocity += away.normalized * BlastPush;
                    rb.WakeUp();
                }
                Falls.Add(new Fall
                {
                    mech = o.Id, model = OzMech.Models.VersionOf(o), parts = parts, t0 = now, real0 = Time.unscaledTime, piece = piece, rb = rb,
                    startY = piece ? piece.position.y : at.y, v0 = rb ? rb.linearVelocity : Vector3.zero,
                });
                while (Falls.Count > RecentMax) Falls.RemoveAt(0);
            }
            var fxp = OpenZFxPlayer.I;
            if (fxp && fxp.isActiveAndEnabled) fxp.Impact(at, FxSurface.Metal);                             // the spark burst (its metal ping)
            AudioDirector.Fire("dst_debris_metal", at, o.Id);                                               // the snap (it clanks again where it lands)
            st.smokeUntil = now + SmokeS; st.nextSmoke = now;
#if UNITY_EDITOR
            Debug.Log($"[OpenZ] mech #{o.Id} (v{OzMech.Models.VersionOf(o)}) lost its hand gun: {(made ? parts + " parts fall as one wreck piece" : "no wreck piece made (" + parts + " parts)")}");
#endif
        }

        static void CopyLook(MeshRenderer from, MeshRenderer to)
        {
            var mats = from.sharedMaterials;
            to.sharedMaterials = mats; to.shadowCastingMode = from.shadowCastingMode; to.receiveShadows = from.receiveShadows;
            to.lightProbeUsage = from.lightProbeUsage; to.reflectionProbeUsage = from.reflectionProbeUsage;
            to.motionVectorGenerationMode = MotionVectorGenerationMode.Object;
            block.Clear(); from.GetPropertyBlock(block); if (!block.isEmpty) to.SetPropertyBlock(block);
            for (int i = 0; i < mats.Length; i++) { block.Clear(); from.GetPropertyBlock(block, i); if (!block.isEmpty) to.SetPropertyBlock(block, i); }   // TeamPaint: per material slot (the paint, the burn)
            OzFireHeat.ColdCopy(to, mats.Length);
        }

        /// <summary>Proof: the counters in one line.</summary>
        public static string Stats =>
            $"MechGunLossView: guns fell {Drops} (pieces {PiecesMade}, not made {PieceFailed}), hidden only {HiddenOnly}, views bound {Bound} (v1 {BoundV1}, v2 {BoundV2}), " +
            $"no forearm node {NoNode}, renderers hidden again {Rehidden}";
    }
}
