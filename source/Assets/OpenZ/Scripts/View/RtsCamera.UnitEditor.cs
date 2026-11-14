using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.2 item 6 (View/OzUnitEditor.cs, View/OzUeCamera.cs; owner 6k "middle mouse hold free camera on default to rotate"; spec 9.6, E036):
    /// what the Unit editor's camera needs of the RTS camera's own free look. The playground switches the free look on for the whole
    /// editor (the `Testing` line of RtsCamera.cs also asks OzUeHooks.Active), so the middle mouse button turns and tilts the view; the
    /// compass sends it back to north and the fixed tilt with the free look's own timed glide (FreeBackSeconds), without switching the
    /// free look off. Nothing here runs outside the playground (only the editor's camera calls it).
    /// </summary>
    public partial class RtsCamera
    {
        /// <summary>The compass (E036): the free look's glide back to north and the fixed tilt starts now (the view stays where it looks).</summary>
        internal void UeGlideHome()
        {
            if (!UeTurned && freeBack < 0f) return;
            freeHold = false;
            freeBack = 0f; freeYaw0 = yaw; freeTilt0 = freeTilt;
        }

        /// <summary>The glide home is under way.</summary>
        internal bool UeGliding => freeBack >= 0f;
        /// <summary>The view is turned or tilted away from the fixed RTS view.</summary>
        internal bool UeTurned => Mathf.Abs(yaw) > 0.05f || Mathf.Abs(freeTilt) > 0.05f;
        /// <summary>The height the zoom is easing to (the wheel, ZoomTo, ZoomBy).</summary>
        internal float UeTargetHeight => targetHeight < 0f ? height : targetHeight;
        /// <summary>A fly-to in progress ends (the editor's own glides move the view instead).</summary>
        internal void UeStopFly() { flying = false; }
    }
}
