// OpenZ (GPL-3.0) - version 1.3, level 7, kit LV5: a body thrown out of a shuttle melts where it comes to the lava.
using UnityEngine;

namespace OpenZ
{
    public sealed partial class OzShuttleCrewFall
    {
        bool lavaTaken;

        /// <summary>A ragdoll came to the lava, every frame on a lava level. As UnitView.RagdollInLava: frozen, and the root brought under the body, because a ragdoll's root stays where it was
        /// thrown from and the melt tilts and flattens about the root. True = it has gone to the melt. No free slot: it falls on and asks again.</summary>
        bool RagdollInLava()
        {
            if (lavaTaken) return true;
            if (age < 0.2f || !OzLavaMelt.HasRoom) return false;
            var p = BodyPos;
            if (!OzLava.SurfaceAt(p, out float ly) || p.y > ly + 0.4f || GroundY(p) > ly - 0.05f) return false;
            float speed = hips && hips.TryGetComponent<Rigidbody>(out var body) && !body.isKinematic ? body.linearVelocity.magnitude : 0f;
            if (!Landed) { Landed = true; LandedAt = age; LandedCount++; FellM = startY - p.y; LandedNow(null); }
            RestOn = "water"; InWater++; checkedRest = true; lavaTaken = true;
            rd.Freeze();
            if (TryGetComponent<RagdollFlail>(out var flail)) Destroy(flail);
            var at = new Vector3(p.x, ly - 0.2f, p.z); var shift = at - transform.position;
            transform.position = at;
            for (int i = 0; i < transform.childCount; i++) transform.GetChild(i).position -= shift;          // the corpse stays where it lies
            OzWaterLanding.Splash(new Vector3(p.x, ly, p.z), OzWaterSize.Small, "robot body", speed);
            OzLavaMelt.Take(transform, speed, OzMeltEnd.Hide, "robot body");
            return true;
        }
    }
}
