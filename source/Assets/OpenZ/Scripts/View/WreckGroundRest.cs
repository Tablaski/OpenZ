using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.3 item 9 (a jeep stuck in the ground in the Unit editor, a dead APC or crane stuck in the ground in a level): no vehicle wreck
    /// is left lying in the ground. View only; the rules are untouched.
    /// <para>What put them there (read in the project): the flipped light-vehicle wreck (DV17: jeep, APC and crane, one in three) is a
    /// real physics body. WreckContact froze it once it slept OR after 6 s, wherever it was then, and nothing ever looked at it again:
    /// the settle guard (DebrisAudit) skips bodies of a unit view and only lifts pieces frozen in the AIR, and the sink gate keeps a
    /// wreck that is on camera where it lies (1.2 item 154). A hull that the crash pushed into the ground, or that went through the
    /// ground collider (the Unit editor's ground is a box only 1 m deep: a body more than half a metre into it is pushed out at the
    /// BOTTOM; a level's ground is a mesh surface, which a body can also pass), was frozen half under the ground and stayed there.
    /// The torn-off plates, wheels and other wreck pieces (WreckPieces) had the same gap in the guard.</para>
    /// <para>The fix: (1) <see cref="Lift"/>, at the flipped hull's freeze: its hull box's lowest corner is put on the ground under it
    /// (only when it is more than <see cref="SinkTolM"/> in; over water the water rule's own rest is kept); (2) <see cref="LiftPiece"/>,
    /// the settle guard's other half: a vehicle wreck piece at rest with more than half of it and more than <see cref="PieceMinM"/> in the
    /// ground is lifted to lie on it (a piece sinking away at the end of its life is left alone); (3) <see cref="DeepenPlaygroundGround"/>:
    /// the Unit editor's ground box is made <see cref="PlaygroundDepthM"/> deep, its top kept at 0 (the ground the units stand on and
    /// every ground ray are the same). Switch: <see cref="Enabled"/> (off = as before).</para>
    /// </summary>
    public static class WreckGroundRest
    {
        public static bool Enabled = true;
        /// <summary>A hull box corner this far under the ground is still lying on it (the box is 0.9 x the drawn hull).</summary>
        public static float SinkTolM = 0.12f;
        /// <summary>A resting wreck piece is lifted when more than this and more than half of its height is in the ground.</summary>
        public static float PieceMinM = 0.3f, PieceShare = 0.5f, PieceKeepM = 0.03f;
        /// <summary>A lift deeper than this is only made when the hull's middle is under its ground too (else a corner reaches under a cliff).</summary>
        public static float CliffM = 2f;
        static readonly Vector3[] corners = new Vector3[8];
        /// <summary>The Unit editor's ground box depth (m).</summary>
        public static float PlaygroundDepthM = 12f;

        /// <summary>Proof counters: hulls lifted at their freeze, the deepest one (m), pieces lifted, the playground ground made deeper.</summary>
        public static int HullsLifted, PiecesLifted, GroundDeepened;
        public static float DeepestHullM;

        /// <summary>WreckContact's freeze: the flipped wreck `root` with its hull box (OZ_WreckHull) lies on the ground, not in it.</summary>
        public static void Lift(Transform root, BoxCollider hull)
        {
            var g = ZGame.I;
            if (!Enabled || !root || !hull || !g) return;
            if (OzWaterLanding.InWater(root)) return;                                                     // the water rule holds it partly under the surface
            var t = hull.transform; var c = hull.center; var e = hull.size * 0.5f;
            for (int i = 0; i < 8; i++) corners[i] = t.TransformPoint(c + new Vector3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z));
            System.Array.Sort(corners, (a, b) => a.y.CompareTo(b.y));
            float deep = float.MinValue;
            for (int i = 0; i < 4; i++) deep = Mathf.Max(deep, g.GroundY(corners[i].x, corners[i].z) - corners[i].y);   // its 4 lowest corners: the side it lies on
            if (deep <= SinkTolM) return;
            var mid = t.TransformPoint(c);
            if (deep > CliffM && mid.y > g.GroundY(mid.x, mid.z)) return;                                // a corner under a cliff or wall beside it, the hull itself above its ground: not sunk
            root.position += Vector3.up * deep;
            HullsLifted++; DeepestHullM = Mathf.Max(DeepestHullM, deep);
        }

        /// <summary>DebrisAudit's settle guard: a resting vehicle wreck piece (bounds b, ground gy under its middle) deep in the ground is
        /// lifted to lie on it. True = it was lifted.</summary>
        public static bool LiftPiece(Rigidbody rb, Bounds b, float gy)
        {
            if (!Enabled || !rb) return false;
            var wp = rb.GetComponent<WreckPieces>(); if (!wp || wp.Fading) return false;                // vehicle wreck pieces only; one sinking away on purpose is left alone
            if (OzWaterLanding.InWater(rb.transform)) return false;
            float deep = gy - b.min.y;
            if (deep <= PieceMinM || deep <= b.size.y * PieceShare) return false;
            rb.transform.position += Vector3.up * (deep - PieceKeepM);
            if (!rb.isKinematic) rb.WakeUp();
            PiecesLifted++;
            return true;
        }

        /// <summary>OzUnitEditor.Awake: the playground's ground box (COL_Terrain, 1 m deep in the scene) deeper, its top where it was.</summary>
        public static void DeepenPlaygroundGround()
        {
            if (!Enabled) return;
            var col = GameObject.Find("COL_Terrain");
            if (!col || !col.TryGetComponent<BoxCollider>(out var bc) || bc.size.y >= PlaygroundDepthM) return;
            float top = bc.center.y + bc.size.y * 0.5f;
            bc.size = new Vector3(bc.size.x, PlaygroundDepthM, bc.size.z);
            bc.center = new Vector3(bc.center.x, top - PlaygroundDepthM * 0.5f, bc.center.z);
            GroundDeepened++;
        }
    }
}
