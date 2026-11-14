// OpenZ (GPL-3.0)
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// The cloth faces the camera (the rule: View/OzFlagFace.cs). Every zone flag and both fort flags, every level: each frame the cloth's yaw about its pole keeps its
    /// face to the main camera's heading, flying to the side of the pole nearer the wind, so all flags still agree; eased, with a dead band; a side change sweeps.
    /// PoseCloth adds the sway on top and still keeps a fort's cloth out of the cone toward its turret. The wave, the wind strength, the capture drop, the new
    /// cloth's rise, the fort's dip and flinch are untouched. FortGunView's shells read the heading a fort's cloth flies at (FortClothDir).
    /// FaceCamera off = the 1.2 cloth (downwind on one windYaw), the A/B switch of the check. No main camera: the cloth holds where it is.
    /// View only: no rule, no random number, no allocation per frame.
    /// </summary>
    public partial class FlagView
    {
        /// <summary>The cloth faces the camera. Off = downwind on one windYaw, as in 1.2.</summary>
        public static bool FaceCamera = true;
        Vector3 viewRight = Vector3.right; bool viewOk; int viewFrame = -1, faceWindSide;

        /// <summary>PoseCloth: the yaw (deg) the cloth flies at before its sway.</summary>
        float FaceYaw(Flag f)
        {
            if (!FaceCamera) { f.face.set = false; return windYaw; }
            if (viewFrame != Time.frameCount)
            {   // once a frame: the view, and the side the wind wants (one for every flag, so all agree)
                viewFrame = Time.frameCount; viewOk = FaceView(out viewRight);
                if (viewOk) faceWindSide = OzFlagFace.WindSide(faceWindSide, viewRight.x, viewRight.z, windDir.x, windDir.z);
            }
            if (!viewOk) return f.face.set ? f.face.yaw : windYaw;
            float bx = 0f, bz = 0f, bc = 2f;
            if (f.blockCos < 1f) { bx = f.blockDir.x; bz = f.blockDir.z; bc = f.blockCos; }
            return OzFlagFace.Step(ref f.face, faceWindSide, viewRight.x, viewRight.z, windDir.x, windDir.z, bx, bz, bc, Time.deltaTime);
        }

        /// <summary>The main camera's right on the ground. Its heading is taken from forward + up, so a camera that looks straight down still has one.</summary>
        static bool FaceView(out Vector3 right)
        {
            right = Vector3.right;
            var cam = Camera.main; if (!cam) return false;
            var t = cam.transform; var h = t.forward + t.up; h.y = 0f;
            if (h.sqrMagnitude < 1e-6f) return false;
            h.Normalize(); right = new Vector3(h.z, 0f, -h.x);
            return true;
        }

        static Vector3 FaceDir(float yaw) { float r = yaw * Mathf.Deg2Rad; return new Vector3(Mathf.Cos(r), 0f, -Mathf.Sin(r)); }

        /// <summary>FortGunView.UpdateCloth: the heading on the ground that fort fortId's cloth flies at now, its sway left out. False: no such flag yet, or FaceCamera
        /// off (then FortGunView's own downwind rule is the cloth's).</summary>
        public static bool FortClothDir(int fortId, out Vector3 dir)
        {
            dir = Vector3.right; var v = I;
            if (!FaceCamera || !v) return false;
            foreach (var f in v.flags)
                if (f.fort && f.obj != null && f.obj.Id == fortId && f.face.set) { dir = FaceDir(f.face.yaw); return true; }
            return false;
        }

        // ================================================================== for the check (Editor/OzFlagRingsClothCheck.cs), read only
        /// <summary>Flag i: its id, fort or not, whether it shows, the yaw before its sway (NaN while it does not face the camera), the side it shows, its fort cone
        /// (blockCos 1 or more: none).</summary>
        public void FaceProbe(int i, out int id, out bool fort, out bool shown, out float faceYaw, out int side, out Vector3 blockDir, out float blockCos)
        {
            var f = flags[i]; id = f.obj != null ? f.obj.Id : -1; fort = f.fort; shown = f.root && f.root.gameObject.activeInHierarchy;
            faceYaw = FaceCamera && f.face.set ? f.face.yaw : float.NaN; side = f.face.side; blockDir = f.blockDir; blockCos = f.blockCos;
        }
        /// <summary>Flag i's pivot (its yaw is the cloth's) and cloth.</summary>
        public Transform PivotForCheck(int i) => flags[i].pivot;
        public Transform ClothForCheck(int i) => flags[i].cloth;
        public float WindYawForCheck => windYaw;
        public Vector3 WindDirForCheck => windDir;
    }
}
