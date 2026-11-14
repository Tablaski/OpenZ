// OpenZ (GPL-3.0) - Level 16: the far fall stands back with the far jungle it fell down, in every look (testing the free camera
// The wall falls' sheet mesh (L16_WallFallSheets) holds, beside the four falls inside the map, a fifth one far north of it: a column of
// falling water 68 m tall (x 104 to 121, z 93 to 152, 6.6 to 74.5 m up), made to fall down the jungle hill that closed the map there.
// The low shots never draw it (Set, BuildFall: its cliff is not in a low shot). With the land beyond the rim (OzL16Outskirts.cs) that
// hill's jungle is not drawn and the land under the column lies at about 0.5 m: a low, turned camera (the FREE CAMERA option) saw the
// column hang over the land like a grey curtain of rain, its mist puffs 8 m up in the air. Now the steep camera draws the same sheet as
// the low shots while the land beyond the rim is on (OzL16Outskirts.On): the four falls inside the map are untouched, the far fall's mist and
// foam puffs stand back with it (L16WaterFx, SpotHeld), its far roar stays (the jungle's sound: a fall somewhere beyond the trees). The
// game's fixed view never frames that place. FarFallHoldOn off = the steep camera draws it as before.
// Play check: return OpenZ.OzL16FlyoverFlora.FarFallStatus();
using UnityEngine;

namespace OpenZ
{
    public sealed partial class OzL16FlyoverFlora
    {
        /// <summary>On: the far fall stands back in every look while the land beyond the rim is on. Off: the steep camera draws it as
        /// before (for a later round that gives it a hill to fall from).</summary>
        public static bool FarFallHoldOn = true;
        /// <summary>True while the far fall is held off: its part of the sheet, its mist and foam puffs.</summary>
        public static bool FarFallHeld => FarFallHoldOn && OzL16Outskirts.On && I;
        /// <summary>Counters: times the steep camera's sheet was set to the one without the far fall; spot-frames a mist or foam spot beyond
        /// the rim was held (L16WaterFx asks once a frame per spot on screen).</summary>
        public static int FarFallSets, FarSpotsHeld;
        Bounds farFallBox; bool farFallBoxed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void FarFallResetStatics() { FarFallHoldOn = true; FarFallSets = FarSpotsHeld = 0; }

        /// <summary>The map's size, then the sheet without the far fall (BuildFall, once) and the far fall's box.</summary>
        void FarFallEnsure()
        {
            if (map == Vector2.zero) { var rts = FindFirstObjectByType<RtsCamera>(); map = rts ? rts.mapSizeMeters : new Vector2(208f, 256f); }
            if (!fallTried) { fallTried = true; BuildFall(); }
            if (farFallBoxed || !fallMf || !fallFull || !fallFull.isReadable) return;
            farFallBoxed = true;
            var v = fallFull.vertices; var tr = fallMf.transform; bool any = false;
            for (int i = 0; i < v.Length; i++)
            {
                var p = tr.TransformPoint(v[i]);
                if (Outside(p) <= GroundMargin) continue;                                                // the rule BuildFall cuts by
                if (!any) { farFallBox = new Bounds(p, Vector3.zero); any = true; } else farFallBox.Encapsulate(p);
            }
            if (!any) farFallBoxed = false;
        }

        /// <summary>From LateUpdate, after the low look's own switch: the steep camera's sheet without the far fall while it is held.</summary>
        void FarFallHold()
        {
            if (on) return;                                                                               // a low shot: Set() drew the sheet without it already
            if (!FarFallHeld)
            {
                if (fallMf && fallFull && fallNear && fallMf.sharedMesh == fallNear) fallMf.sharedMesh = fallFull;   // the switch went off: as before
                return;
            }
            FarFallEnsure();
            if (fallMf && fallNear && fallMf.sharedMesh != fallNear) { fallMf.sharedMesh = fallNear; FarFallSets++; }
        }

        /// <summary>L16WaterFx: a mist or foam spot of the water dressing that stands beyond the rim (the far fall's) stands back with it.</summary>
        public static bool SpotHeld(Vector3 at)
        {
            var o = I;
            if (!FarFallHeld || !o) return false;
            if (o.map == Vector2.zero) { var rts = FindFirstObjectByType<RtsCamera>(); o.map = rts ? rts.mapSizeMeters : new Vector2(208f, 256f); }
            if (o.Outside(at) <= GroundMargin) return false;
            FarSpotsHeld++;
            return true;
        }

        /// <summary>Play check: the far fall's box against a camera: framed = any of 27 points of it in the frame, in front of the camera and
        /// nearer than its far plane; drawn = the wall falls' sheet holds it now (the whole mesh, its renderer on).</summary>
        public static bool FarFallInView(Camera cam, out bool drawn)
        {
            drawn = false;
            var o = I; if (!o || !cam) return false;
            o.FarFallEnsure();
            if (!o.farFallBoxed) return false;
            var r = o.fallMf ? o.fallMf.GetComponent<Renderer>() : null;
            drawn = r && r.enabled && r.gameObject.activeInHierarchy && o.fallMf.sharedMesh != o.fallNear;
            var b = o.farFallBox;
            for (int i = 0; i < 27; i++)
            {
                var p = new Vector3(Mathf.Lerp(b.min.x, b.max.x, (i % 3) * 0.5f), Mathf.Lerp(b.min.y, b.max.y, (i / 3 % 3) * 0.5f), Mathf.Lerp(b.min.z, b.max.z, (i / 9) * 0.5f));
                var v = cam.WorldToViewportPoint(p);
                if (v.z > cam.nearClipPlane && v.z < cam.farClipPlane && v.x >= 0f && v.x <= 1f && v.y >= 0f && v.y <= 1f) return true;
            }
            return false;
        }

        /// <summary>Play check: the far fall is held and the steep camera's sheet is the one without it now; box = the far fall's box.</summary>
        public static bool FarFallCutNow(out Bounds box)
        {
            box = default;
            var o = I; if (!o) return false;
            o.FarFallEnsure(); box = o.farFallBox;
            return FarFallHeld && o.farFallBoxed && o.fallMf && o.fallNear && o.fallMf.sharedMesh == o.fallNear;
        }

        /// <summary>One line for the Play check.</summary>
        public static string FarFallStatus()
        {
            var o = I; if (!o) return "far fall: not in this scene (level 16 only)";
            string sheet = !o.fallMf ? (o.fallTried ? "not found or not readable" : "not looked at yet")
                : o.fallMf.sharedMesh == o.fallNear ? "drawn WITHOUT the far fall" : "drawn WHOLE (with the far fall)";
            var b = o.farFallBox;
            return $"far fall: {(FarFallHeld ? "held with its jungle" : FarFallHoldOn ? "not held (the land beyond the rim is off: the level as in 1.1)" : "not held (FarFallHoldOn off)")}"
                + $" | the wall falls' sheet {sheet}, {FallCut} of its triangles stand beyond the rim"
                + (o.farFallBoxed ? $", the far fall's box x {b.min.x:F1}..{b.max.x:F1} y {b.min.y:F1}..{b.max.y:F1} z {b.min.z:F1}..{b.max.z:F1}" : "")
                + $" | low look {(o.on ? "ON" : "off")} | steep sheet set {FarFallSets} time(s), mist / foam spot-frames held beyond the rim {FarSpotsHeld}";
        }
    }
}
