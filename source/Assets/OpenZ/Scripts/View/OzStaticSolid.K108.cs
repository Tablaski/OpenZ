using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// The gun's break-apart at its death (View/OzGunBreakup.cs) and the statics' colliders. Every drawn part of a living gun carries a
    /// collider of its exact drawn shape (a concave MeshCollider, a child of the part: the barrel, the collar, the turret), so the big pieces
    /// of other deaths do not pass through it. A gun that dies loses them on its death event (Update, before its wreck is made); when the
    /// view's death came first (a death set by hand, a test's kill), they were still on the barrel and the collar when those became thrown
    /// wreck pieces, and the physics refused them with an error ("Concave Mesh Colliders are not supported when used with dynamic Rigidbody
    /// GameObjects", one per collider), at a cost of milliseconds on the break's frame. Now the break drops the gun's colliders first
    /// (<see cref="DropForBreak"/>, the same path as its death event), and any concave collider still under a part that is about to fly is
    /// taken off (<see cref="ConcaveOff"/>); a thrown part keeps only its wreck piece's box (convex). View only.
    /// </summary>
    public sealed partial class OzStaticSolid
    {
        /// <summary>The gun `simId` breaks apart now: its colliders go at once, as on its death event (what rested on it wakes). False: not
        /// listed, or already gone.</summary>
        public static bool DropForBreak(int simId)
        {
            if (simId == 0 || !bySim.TryGetValue(simId, out var st) || st == null || st.dead) return false;
            Lost(st);
            return true;
        }

        /// <summary>Takes every concave MeshCollider under `part` off (a static's collider holder goes with its collider; any other only loses
        /// the component), before the part becomes a moving body. Returns how many.</summary>
        public static int ConcaveOff(Transform part)
        {
            if (!part) return 0;
            int n = 0;
            foreach (var mc in part.GetComponentsInChildren<MeshCollider>(true))
            {
                if (!mc || mc.convex) continue;
                mc.enabled = false;
                if (mc.gameObject.name == "OZ_StaticSolid" && mc.transform != part) { mc.gameObject.SetActive(false); mc.transform.SetParent(null, false); Destroy(mc.gameObject); }
                else Destroy(mc);
                n++;
            }
            return n;
        }
    }
}
