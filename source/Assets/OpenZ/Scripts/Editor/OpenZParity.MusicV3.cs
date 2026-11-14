#if !OPENZ_HARNESS
using System.Collections.Generic;
using System.Globalization;

namespace OpenZ.EditorTools
{
    /// <summary>music_v3: the Deviations row of the dramatic fort-attack music (view only: the sim is read, never written).</summary>
    public static partial class OpenZParity
    {
        static void AddMusicV3Deviation(List<Deviation> list)
        {
            var c = new MusicCoreConfig();
            string f(float v) => v.ToString("0.##", CultureInfo.InvariantCulture);
            list.Add(new Deviation
            {
                id = "DV50", change = "Dramatic fort-attack music (view only)",
                zod = "FORT music when ONE enemy unit (cannons included) is within 250 px of our fort (zmusic_engine.cpp:258-263); a new danger level needs 3 s (:291-301, 15 s once the fort is destroyed) and FORT holds at least 3 s (:367). " +
                      "OpenZ before music_v3: after a close push line (vo_push) every ATTACKING state also played the fort music (AudioDirector finalPush); the music stopped at the game over",
                openz = $"the dramatic music when EITHER fort is really attacked: a BIG attack (>= {c.minUnits} engaged hostile mobile units worth >= {f(c.hotScore)}, >= {f(c.hotScoreUnderFire)} while the fort takes hits) " +
                        $"or the fort UNDER FIRE (the fort building hit by the other side within {f(c.hitWindow)} s, the same record as the 'fort under attack' line, and >= {f(c.fireScore)} engaged from any number of units or >= {c.fireUnits} engaged units: one tank or missile launcher, a Tough squad, a squad with grenades; never a lone robot or jeep), " +
                        $"held {f(c.enterHold)} s (leaky hold); engaged = within {f(c.rIn / 16f)} tiles of the fort's WALLS (DV26's wall point, so a unit shelling it from its full range counts) and attacking something of that side inside the fort area " +
                        $"({f(c.rCore / 16f)} tiles of the walls), heading into it, or in it while the fort takes hits; robot {f(c.wRobot)}, jeep / APC {f(c.wJeep)}, light {f(c.wLight)}, medium / MLRS {f(c.wMedium)}, heavy {f(c.wHeavy)}; " +
                        $"no cannons, cranes, idle or passing units or the fort defender; fights anywhere else never count; " +
                        $"it ends when less than {f(c.warmScore)} stay engaged for {f(c.exitHold)} s with no hit for {f(c.calmAfterHit)} s, " +
                        $"or {f(c.hotLapse)} s after the last hot reading, never before {f(c.minDrama)} s; at most one drama per {f(c.cooldown)} s of play (5 minutes, no exception, an attack under way at a fort's fall still starts it unless a drama faded back < {f(c.gameOverGrace)} s before); " +
                        "the drama tracks play strongest first (intensity) and each sits at least +4 LU over the level's battle songs, in its first 60 s and to its end (MusicDramaGain, measured); " +
                        $"the regular song is paused and resumed at its sample; a drama playing at the fort's fall plays on to the end screen, where its own lift comes off to +{f(c.dramaDb)} dB in {f(c.endLiftOff)} s under the sting (as before music_drama). Zod's CALM / ATTACKING / FORT state is unchanged for the fort alarm, " +
                        "the announcement and the ambience; for the music FORT selects the battle playlist (the finalPush fort music is gone)",
                measured = "headless: the music v3 folder harness (MusicCoreScenarios: 26 timelines + 2 x 200 random 40 min matches; the state-machine reviewer's timelines); bot-vs-bot calibration on the live Sim (reports/botcal_L02.txt, botcal_L17.txt); " +
                           "music_drama (5 Nov): MusicCoreScenarios 31 timelines + MusicFortReaderScenarios on L02 / L17 worlds, the end sting over the drama with ffmpeg ebur128; Play: OpenZ > Music v3 > Status / Force",
                toggle = "AudioDirector.musicV3 (off = music_v2 with Zod's rule)",
                status = DevActive,
            });
        }
    }
}
#endif
