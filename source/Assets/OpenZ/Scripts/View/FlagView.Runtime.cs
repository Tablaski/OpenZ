using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// the game runs. FlagView builds its flags once in Start from World.Flags, so a flag the Unit editor's playground drops later gets
    /// the same view here (the pole, the waving cloth, the capture ring, the sweep and sounds of a capture, the 1.2 day / night looks)
    /// and a removed flag loses it. The looks keep one shared mesh for every flag, so they are built again for the new list.
    /// </summary>
    public partial class FlagView
    {
        /// <summary>A flag added to the world after the start gets its view (true = drawn; false = no FlagView in this scene).</summary>
        public static bool AddRuntimeFlag(ZObject o)
        {
            var v = I;
            if (!v || o == null || v.game == null || v.root == null) return false;
            foreach (var f in v.flags) if (f.obj == o) return true;
            if (!v.AddZoneFlag(o)) return false;
            var nf = v.flags[v.flags.Count - 1];
            if (v.fx) { nf.ring = v.NewRing(nf); v.EnqueueDrape(nf.ring); }
            v.RebuildLooks();
            v.PoseCloth(nf, Time.time);
            return true;
        }

        /// <summary>A flag taken out of the world: its pole, cloth, ring and looks go (true = it had a view).</summary>
        public static bool RemoveRuntimeFlag(ZObject o)
        {
            var v = I;
            if (!v || o == null) return false;
            int k = v.flags.FindIndex(f => f.obj == o && !f.fort);
            if (k < 0) return false;
            var x = v.flags[k];
            v.flags.RemoveAt(k);
            for (int i = 0; i < v.flags.Count; i++) v.flags[i].index = i;
            if (x.ring != null)
            {
                v.buildQueue.Remove(x.ring);
                if (x.ring.r) Destroy(x.ring.r.gameObject);
                if (x.ring.mesh) Destroy(x.ring.mesh);
            }
            if (x.mesh) Destroy(x.mesh);
            if (x.root) Destroy(x.root.gameObject);
            v.RebuildLooks();
            return true;
        }

        /// <summary>The 1.2 looks (FlagView.Looks.cs) over the flags as they are now: the old shared ring / halo and pool meshes and the
        /// looks' own pole copies go, LooksStart builds them again.</summary>
        void RebuildLooks()
        {
            foreach (var L in looks)
            {
                if (L.pole)
                {
                    var mf = L.f != null && L.f.poleR ? L.f.poleR.GetComponent<MeshFilter>() : null;
                    if (mf && mf.sharedMesh == L.pole) mf.sharedMesh = zonePole;
                    Destroy(L.pole);
                }
            }
            if (marksR) Destroy(marksR.gameObject);
            if (poolsR) Destroy(poolsR.gameObject);
            if (marks) Destroy(marks);
            if (pools) Destroy(pools);
            if (marksMat) Destroy(marksMat);
            if (poolMat) Destroy(poolMat);
            marksR = poolsR = null; marks = pools = null; marksMat = poolMat = null;
            mkV = pv = null; mkC = pc = null; mk1 = p0 = null;
            looks.Clear();
            LooksStart();
        }
    }
}
