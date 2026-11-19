using UnityEngine;

namespace OpenZ
{
    public partial class AudioDirector
    {
        /// <summary>1.5 quick fix 14d (OzFuelCan's first-landing retries): true when <see cref="Play"/> would refuse this event at `pos` right
        /// now for being off screen (the same view test as Play: Zod's PlayWavRestricted margins, the off-screen "distant battle" band for
        /// events that allow it, the fps view's own sounds in view). Asks nothing, logs nothing.</summary>
        public bool OffScreenNow(string id, Vector3 pos)
        {
            id = Canon(id);
            if (id == null || Combat.ContainsKey(id)) return false;
            var d = Def(id);
            if (d == null || !d.is3D || !d.inViewOnly || sweeping || !cam) return false;
            if (OzFpsView.Own(pos)) return false;
            var vp = cam.WorldToViewportPoint(pos);
            bool inView = vp.z > 0 && vp.x > -0.35f && vp.x < 1.35f && vp.y > -0.35f && vp.y < 1.35f;
            if (inView) return false;
            bool near = vp.z > 0 && vp.x > -1.5f && vp.x < 2.5f && vp.y > -1.5f && vp.y < 2.5f;
            return !(d.offscreen && near);
        }
    }
}
