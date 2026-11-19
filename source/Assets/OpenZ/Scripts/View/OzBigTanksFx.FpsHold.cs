// OpenZ (GPL-3.0)
// Cause: the big tanks' code-built parts (kit big_tanks_fx: pads, side neon, team marker lamps and their halos, the head
// lamp housings, the exhaust stubs, the searchlight's drum / yoke / lens / halo / beam and its light, the Bizon's amber work beacon and its turning
// light) are NOT children of the tank's view (three holders under OZ_BigTanksFx follow its nodes), so the first-person view's hold of the unit's
// own model (View/OzFpsView.cs) never covered them: with the hull held off they floated round the eye, their camera-facing halos filled the
// screen and the beacon's light swept over the gun in front of the eye at every turn. (The view's per-picture hold cannot take them either:
// measured 11 Nov, solid parts stayed drawn with forceRenderingOff set at the picture's start.)
// Now, while the first-person view holds that tank (OzFpsView.Holds: the view on, or its glide near the eye, the same rule as its lamp glows in
// View/VehicleLights.cs), its three holders are switched off (the parts and their lights with them) and switched on again exactly when the view
// leaves it (Esc, its key again, another unit, the tank gone). The searchlight's ground pool (on the ground ahead, not a holder part), the dust, the
// exhaust smoke and every other tank are untouched. View only; the rules (Scripts/Sim) are not touched.
using System.Collections.Generic;
using UnityEngine;

namespace OpenZ
{
    public sealed partial class OzBigTanksFx
    {
        /// <summary>N27 proof counters: holders switched off / on again, the parts (renderers) and real lights of the last tank switched off.</summary>
        public static int FpsHides, FpsShows, FpsHiddenParts, FpsHiddenLights;

        /// <summary>N27: the tank record's three holders on or off (false: the parts and their lights not drawn). Returns false without a record.</summary>
        internal bool SetHoldersShown(int id, bool shown, out int parts, out int lights)
        {
            parts = lights = 0;
            Tank t;
            if (!tanks.TryGetValue(id, out t) || t == null) return false;
            t.HoldersShown(shown, out parts, out lights);
            return true;
        }

        /// <summary>1.5 close_hide_all_parts_r6 counters: other big tanks' holders switched off / on again by the eye's fade, the parts (renderers)
        /// and real lights of the last one, the tanks held off now.</summary>
        public static int EyeOff, EyeOn, EyeOffParts, EyeOffLights, EyeOffNow;

        /// <summary>close_hide_all_parts_r6 (OzFpsFeel.Collect): the renderers of big tank v's code-built parts (its three holders, switched off ones
        /// too) appended to into; nothing for any other unit.</summary>
        public static void PartsOf(UnitView v, List<Renderer> into)
        {
            var fx = I; Tank t;
            if (!fx || !v || v.Obj == null || !fx.tanks.TryGetValue(v.Obj.Id, out t) || t == null || t.view != v) return;
            t.HolderRenderers(into);
        }

        /// <summary>close_hide_all_parts_r6: the ids of the big tanks the first-person view's eye has faded at least half away (OzFpsFeel.EyeHides).</summary>
        internal void EyeHidden(List<int> into)
        {
            into.Clear();
            foreach (var kv in tanks) if (kv.Value != null && OzFpsFeel.EyeHides(kv.Value.view)) into.Add(kv.Key);
        }

        public sealed partial class Tank
        {
            static readonly List<Renderer> closeHideBuf = new List<Renderer>();
            /// <summary>close_hide_all_parts_r6: every renderer under the root, hull and turret holders (inactive ones too), appended.</summary>
            public void HolderRenderers(List<Renderer> into)
            {
                if (hRoot) { hRoot.GetComponentsInChildren(true, closeHideBuf); into.AddRange(closeHideBuf); }
                if (hHull) { hHull.GetComponentsInChildren(true, closeHideBuf); into.AddRange(closeHideBuf); }
                if (hTurret) { hTurret.GetComponentsInChildren(true, closeHideBuf); into.AddRange(closeHideBuf); }
            }

            /// <summary>N27: the root, hull and turret holders active or not; the renderers and lights under them counted.</summary>
            public void HoldersShown(bool shown, out int parts, out int lights)
            {
                parts = lights = 0;
                foreach (var h in new[] { hRoot, hHull, hTurret })
                {
                    if (!h) continue;
                    parts += h.GetComponentsInChildren<Renderer>(true).Length; lights += h.GetComponentsInChildren<Light>(true).Length;
                    if (h.gameObject.activeSelf != shown) h.gameObject.SetActive(shown);
                }
            }
        }
    }

    /// <summary>N27 (PRIVATE): switches the viewed big tank's own code-built parts off while the first-person view holds it (see the file's head).
    /// One small object for the whole run; it runs after OzBigTanksFx placed the parts (order 32050) and before the camera draws.</summary>
    [DefaultExecutionOrder(32060)]
    public sealed class OzBigTanksFpsHide : MonoBehaviour
    {
        static OzBigTanksFpsHide inst;
        int hiddenId = -1; OzBigTanksFx hiddenFx;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (inst) return;
            var go = new GameObject("OZ_BigTanksFpsHide") { hideFlags = HideFlags.HideInHierarchy };
            DontDestroyOnLoad(go);
            inst = go.AddComponent<OzBigTanksFpsHide>();
        }

        void LateUpdate()
        {
            var fx = OzBigTanksFx.I;
            int want = -1;
            var fv = OzFpsView.I;
            if (fx && OzFpsView.On && fv)
            {
                var v = fv.View;
                if (v && v.Obj != null && OzBigTanks.Is(v.Obj.Type) && OzFpsView.Holds(v)) want = v.Obj.Id;
            }
            if (want != hiddenId || fx != hiddenFx)
            {
                int p, l;
                if (hiddenId >= 0 && hiddenFx && hiddenFx.SetHoldersShown(hiddenId, true, out p, out l)) OzBigTanksFx.FpsShows++;
                hiddenId = -1; hiddenFx = null;
                if (want >= 0 && fx.SetHoldersShown(want, false, out p, out l))
                {
                    hiddenId = want; hiddenFx = fx;
                    OzBigTanksFx.FpsHides++; OzBigTanksFx.FpsHiddenParts = p; OzBigTanksFx.FpsHiddenLights = l;
                }
            }
            Others(fx);                                                                                      // 1.5 close_hide_all_parts_r6
        }

        // stayed): every OTHER big tank the eye's fade (OzFpsFeel) has taken half away or more gets its three holders switched off too (marker lamps
        // and halos, head lamp housings, exhaust stubs, pads, neon, searchlight and beacon with their real lights), on again exactly when that
        // fade falls under half, the view's fades are over or the tank is gone. Never the viewed tank (its hold above). Its state (power, the
        // searchlight's sweep, the beacon, the dust and the exhaust smoke already in the air) runs on as always: nothing switches on with a sound.
        readonly List<int> eyeWant = new List<int>(), eyeDrop = new List<int>();
        readonly HashSet<int> eyeHidden = new HashSet<int>(); OzBigTanksFx eyeFx;

        void Others(OzBigTanksFx fx)
        {
            eyeWant.Clear();
            if (fx) fx.EyeHidden(eyeWant);
            eyeWant.Remove(hiddenId);
            int p, l;
            eyeDrop.Clear();
            foreach (var id in eyeHidden) if (fx != eyeFx || !eyeWant.Contains(id)) eyeDrop.Add(id);
            foreach (var id in eyeDrop)
            {
                eyeHidden.Remove(id);
                if (id != hiddenId && eyeFx && eyeFx.SetHoldersShown(id, true, out p, out l)) OzBigTanksFx.EyeOn++;  // now the viewed tank: its holders stay the hold's
            }
            eyeFx = fx;
            if (fx)
                foreach (var id in eyeWant)
                    if (!eyeHidden.Contains(id) && fx.SetHoldersShown(id, false, out p, out l))
                    { eyeHidden.Add(id); OzBigTanksFx.EyeOff++; OzBigTanksFx.EyeOffParts = p; OzBigTanksFx.EyeOffLights = l; }
            OzBigTanksFx.EyeOffNow = eyeHidden.Count;
        }

        void OnDestroy() { if (inst == this) inst = null; }
    }
}
