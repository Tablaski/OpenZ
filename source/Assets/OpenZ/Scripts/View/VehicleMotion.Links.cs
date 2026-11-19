using System.Collections.Generic;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.5 (the Mammoth and the Bizon, View/OzTrackLinks.cs): VehicleMotion drives their REAL track links. Called from Step right after its own
    /// wheel loop (one line there), with the same travel and turn every wheel of the game rolls with: OzTrackLinks moves every link round its
    /// loop and turns the sprockets, idlers and guide wheels; the road wheels (TankWheel_N, spun by the loop above with the old 1.3 m constant)
    /// are turned again here with the tank's REAL half gauge, so in a turn on the spot both sides run opposite ways at the speed their links
    /// run. The side roots (Track_Left / Track_Right) are the running gear the planted gear puts on the ground (<see cref="LinkGearNodes"/>, one
    /// line in GearNodes; their footprint: UnitView.LinkGear.cs). Tanks without OzTrackLinks: nothing changes. View only.
    /// Also the drive's speed and acceleration for the Bizon's exhaust (View/OzBigTankExhaust.cs).
    /// </summary>
    public partial class VehicleMotion
    {
        OzTrackLinks links; bool linksLooked;

        /// <summary>The tank's link tracks (null for every other vehicle).</summary>
        public OzTrackLinks Links { get { if (!linksLooked) { linksLooked = true; links = GetComponentInChildren<OzTrackLinks>(true); if (links && !links.Valid) links = null; } return links; } }

        /// <summary>The drive as Step last drew it: the smoothed speed (m/s, &lt; 0 backing up), its change (m/s2), seconds since it set off, moving now.</summary>
        public float DriveSpeed => speedSmooth;
        public float DriveAccel => accel;
        public float DriveAge => motionAge;
        public bool DriveMoving => wasMoving;

        /// <summary>Step, right after the wheels: the links, the corner wheels and the road wheels of a link-track tank.</summary>
        void LinksStep()
        {
            var l = Links; if (!l) return;
            l.Step(travel, yawTravel, transform);
            for (int i = 0; i < wheels.Count; i++)
            {   // the road wheels with the real half gauge (the loop above used 1.3 m for every tank)
                var w = wheels[i];
                float dist = travel + yawTravel * w.side * l.halfGauge;
                w.t.localRotation = w.rest * Quaternion.AngleAxis(-dist / w.radius * Mathf.Rad2Deg, Vector3.right);
            }
        }

        /// <summary>GearNodes: a link-track tank's side roots are its running gear (planted; their links, corner wheels and side plates ride on them).</summary>
        void LinkGearNodes(List<(Transform t, bool spun)> into)
        {
            var l = Links; if (!l || !body) return;
            foreach (var s in l.sides)
                if (s != null && s.root && s.root != body && GearUnder(s.root)) into.Add((s.root, false));
        }
    }
}
