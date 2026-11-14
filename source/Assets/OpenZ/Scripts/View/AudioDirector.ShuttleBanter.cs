using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.3 item 7 (the big crash landing's banter, View/OzPlayableShuttle.BigCrash.cs): the voice director answers, without saying anything,
    /// whether a robot line on the main channel would start now: nothing is speaking there and the robot line gap after the last line
    /// (AlanMainGap, 3 s) is over. The banter waits for it instead of being dropped by that gap right after "Coming in hot!". Nothing else
    /// changes: SayImpl keeps every one of its tests.
    /// </summary>
    public partial class AudioDirector
    {
        /// <summary>A robot line on the main channel would start now (nothing speaking there, the gap after the last one over).</summary>
        public static bool RobotLineFree => I && I.RobotLineFreeImpl();

        bool RobotLineFreeImpl()
        {
            if (alanMain != null && alanMain.busy && alanMain.src && alanMain.src.isPlaying) return false;
            return Time.unscaledTime >= alanMainUntil + AlanMainGap && !RobotLineWouldWait();      // 1.4 radar_voice_fix: and a robot line would not have to wait for the voice floor
        }
    }
}
