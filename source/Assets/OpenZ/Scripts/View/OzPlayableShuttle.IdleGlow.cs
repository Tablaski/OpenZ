using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// His frame: a red captured shuttle standing in daylight, the round engine mouth at the
    /// end of its pod pale and dull, read as a hole). View only.
    /// <para>The engine mouths' glow (OzShuttleLights: a soft pulse, emission x engineGain by day / at night, x the crew's power) stood at the
    /// kit's 1.2 by day on the ground: its pulse then gives 0.34-0.86 x the emission picture, under the day's bloom and paler than the lit hull
    /// round it. Now, while the ship stands on the ground with its crew (the lights' power: a captured or crewed ship; an empty neutral ship
    /// stays dark as before), the day's gain is at least <see cref="IdleEngineDayGain"/>: a soft white-blue idle glow in the mouths day and night
    /// (the night's gain is unchanged), well under the flight's (the jets' flames, the fast pulse); at the take-off it eases into the flight's
    /// glow exactly as before (the lift share), and in the air nothing changes.</para>
    /// </summary>
    public sealed partial class OzPlayableShuttle
    {
        /// <summary>N20: the engine mouths' least glow by day while the ship stands crewed on the ground (the kit's 1.2 x).</summary>
        public static float IdleEngineDayGain = 2.4f;

        /// <summary>N20: the engine glow's gains (day, night) on the ground: the kit's, the day's lifted to IdleEngineDayGain.</summary>
        Vector2 IdleEngineGain() => new Vector2(Mathf.Max(engineGain0.x, IdleEngineDayGain), engineGain0.y);
    }
}
