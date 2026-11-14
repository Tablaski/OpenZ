// OpenZ (GPL-3.0) - version 1.4, speed-up 2 (fewer garbage-collector stutters), view only. The foot IK pass of every robot on screen made
// three small arrays every frame (the two foot goals, their turns and the ground under them). They are kept per robot now. Every slot is
// written before it is read in each pass (RobotLegIK.cs OnAnimatorIK and StanceLockStep), so the feet are placed exactly as before.
using UnityEngine;

namespace OpenZ
{
    public partial class RobotLegIK
    {
        readonly Vector3[] ikP = new Vector3[2];                                                   // OnAnimatorIK: the foot goals (was new each frame)
        readonly Quaternion[] ikQ = new Quaternion[2];                                             // OnAnimatorIK: the foot turns (was new each frame)
        readonly float[] ikGy = new float[2];                                                      // StanceLockStep: the ground under each foot goal (was new each frame)
    }
}
