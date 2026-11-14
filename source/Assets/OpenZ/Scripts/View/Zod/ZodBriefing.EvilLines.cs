// OpenZ (GPL-3.0) - version 1.2, owner items 98c / 99: General EvilZod speaks on the blue side's channel card (the battlefield screen).
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// The blue side's card on the battlefield screen (ZodBriefing.EvilCard.cs: EvilZod's live feed) is not silent: EvilZod speaks the way
    /// the commander's level lines work on the red side (MenuRules, zod_select_L02 / L16 / L17):
    ///  * when: the player clicked a level card since the screen opened, it stayed focused for <see cref="quipFocus"/> (0.8 s), and no
    ///    other voice is on or ended in the last 0.3 s (the announcer, the robots, the commander, the faction screen's joke on its own
    ///    source, any outside voice the director knows);
    ///  * what: ONE of that level's lines (<see cref="OzEvilCardLines"/>: two, three for the jungle), picked at random every time he speaks,
    ///    each line as likely (never two: one line on the air at a time); a level speaks once per visit of the screen (a new visit = the
    ///    screen opened again, from the faction screen or the main menu);
    ///  * how: his own 2D source on the commander's bus (Voice/Commander at voiceDb), registered with the director as an outside voice;
    ///    <see cref="Speaking"/> is true meanwhile, so the other voices wait and the music dips under him as under the commander; the
    ///    card's camera eases in to his close-up before his first word (the commander's level lines are framed tight) with a jolt on one
    ///    stressed word, and back to the two-shot after; his jaw plate and red eyes follow the live level of his voice (OzEvilStage.Talk,
    ///    as in the blue scenes), the card's signal meter too; the subtitle on the card sentence by sentence under GEN EVILZOD, the company's
    ///    name again after it;
    ///  * the screen left or the side changed mid-line: the line fades out in 0.3 s (as the commander's when his screen is left).
    /// Logged in OzSoundLog (source "EvilZod", key "evil_card_line": PLAY with the clip, FADE when cut short).
    /// 1.3 item 30: a line may be an exchange (level 7: BS-01 first, EvilZod cuts him off). Each cue names its speaker: that one's
    /// mouth / eyes follow the voice (OzEvilStage.Talk), the card's speaker label shows BS-01 or GEN EVILZOD, the card holds the
    /// two-shot while BS-01 talks and eases to EvilZod's close-up on his first word. The 1.2 lines are all EvilZod's: unchanged.
    /// </summary>
    public sealed partial class ZodBriefing
    {
        AudioSource evilVoice; readonly float[] evilRms = new float[1024];
        OzEvilCardLine evilLine; bool evilTalking;
        float evilVisitAt = float.NaN, evilFocusSince, evilOtherAt = -100f, evilNextDuck; int evilFocus = -1;
        readonly HashSet<string> evilSpoken = new HashSet<string>();
        /// <summary>Checks: the card lines started / heard (the voice began) / cut short (faded) since the game started.</summary>
        public static int EvilLinesStarted { get; private set; }
        public static int EvilLinesPlayed { get; private set; }
        public static int EvilLinesFaded { get; private set; }
        public static string EvilLastLineId { get; private set; } = "";
        /// <summary>Checks: a card line is up (from the camera's push-in to the end of its tail) and which one.</summary>
        public static bool EvilLineOn => I && I.evilLine != null;
        public static string EvilLineId => I && I.evilLine != null ? I.evilLine.Id : null;
        /// <summary>Checks: his voice source, the commander's own one, the card's subtitle, camera framing and EvilZod's mouth (0..1).</summary>
        public static AudioSource EvilVoice => I ? I.evilVoice : null;
        public static AudioSource CommanderVoice => I ? I.voice : null;
        public static string EvilCardCue => I && I.comms != null ? I.comms.SubtitleText : "";
        public static string EvilCardCamera => I && I.evilStage ? I.evilStage.CamName : "";
        public static float EvilMouth => I && I.evilStage ? I.evilStage.EzMouth : 0f;
        public static RectTransform EvilCardWindow => I && I.comms != null ? I.comms.Window : null;

        /// <summary>Part of <see cref="ZodSpeaking"/>: a card line is on the air (from its push-in to 0.15 s after his last word).</summary>
        static bool EvilLineSpeaking() => I && I.evilTalking && !AudioListener.pause;

        float MenuViewAt() { try { return fViewAt != null ? (float)fViewAt.GetValue(menu) : 0f; } catch (System.Exception) { return 0f; } }

        /// <summary>Another voice is on: the announcer / the robots (the director's, playing or queued), the commander's line or source, an
        /// outside voice that reports itself, the faction screen's joke (its own source on the main menu).</summary>
        bool EvilOtherVoice()
        {
            if (AudioDirector.VoiceBusy || perf != null || (voice && voice.isPlaying)) return true;
            if (!evilTalking && AudioDirector.ExternalVoiceBusy != null && AudioDirector.ExternalVoiceBusy()) return true;
            var fv = menu ? menu.ProofFactionVoice : null;
            return fv && fv.isPlaying;
        }

        /// <summary>EvilCardTick, every frame the card is up: the rule of the commander's level lines, for EvilZod.</summary>
        void EvilLinesTick(float now)
        {
            float va = MenuViewAt();
            if (va != evilVisitAt) { evilVisitAt = va; evilSpoken.Clear(); evilFocus = -1; }                  // a new visit of the screen: every level may speak again
            if (EvilOtherVoice()) evilOtherAt = now;
            if (evilLine != null || fFocus == null) return;
            int f = MenuFocus();
            if (f != evilFocus) { evilFocus = f; evilFocusSince = now; }
            if (f < 0 || f >= OzLevelDef.All.Length) return;
            string lvl = OzLevelDef.All[f].Id;
            if (evilSpoken.Contains(lvl) || !ClickedSinceOpen(f) || now - evilFocusSince < quipFocus) return;
            if (now - lastBusyAt < 0.3f || now - evilOtherAt < 0.3f || perf != null) return;                // only into silence
            var line = OzEvilCardLines.Pick(lvl);                                                           // ONE of its lines, a new draw each time
            evilSpoken.Add(lvl);
            if (line != null) EvilLineStart(line);
        }

        void EvilLineStart(OzEvilCardLine line)
        {
            if (evilLine != null) return;                                                                   // never two lines at once
            var clip = OzEvilCardLines.Clip(line);
            if (!clip)
            {
                Debug.LogWarning("[ZodCameo] EvilZod's card line " + line.Id + ": no voice clip Resources/" + OzEvilCardLines.VoicePath + line.Id + " (he stays silent)");
                OzSoundLog.Add("EvilZod", "evil_card_line", line.Id, "missing clip");
                return;
            }
            if (!evilVoice)
            {
                evilVoice = gameObject.AddComponent<AudioSource>();
                evilVoice.playOnAwake = false; evilVoice.loop = false; evilVoice.spatialBlend = 0f; evilVoice.priority = 8; evilVoice.dopplerLevel = 0f;
                evilVoice.bypassReverbZones = true; evilVoice.ignoreListenerPause = false;
                AudioDirector.RegisterExternalVoice(evilVoice);                                                // the radio never talks over him
            }
            evilVoice.Stop(); evilVoice.clip = clip; evilVoice.pitch = 1f; evilVoice.volume = VoiceGain();
            evilLine = line; evilTalking = true; EvilLinesStarted++;
            if (evilStage) evilStage.SetBoardLevel(line.Level);                                                // his board shows the level he talks about
            Debug.Log($"[ZodCameo] EvilZod on the blue card: {line.Id} for {line.Level} ({clip.length:F2} s): \"{line.Text}\"");
            StartCoroutine(EvilLineRoutine(line, clip));
        }

        IEnumerator EvilLineRoutine(OzEvilCardLine line, AudioClip clip)
        {
            float t = -OzEvilCardLines.Preroll, fade = 1f, fadeSpeed = 0f, mouth = 0f, after = 0f;
            bool voiceOn = false, ended = false, kicked = false, stay = OzEvilCardLines.StaysOnHim(line);
            int moveStep = 0, cueSeen = 0;
            bool exchange = line.Exchange; string speaker = null;                                           // 1.3 item 30: BS-01 then EvilZod on one take
            if (evilStage) evilStage.Beat(new OzBlueBeat { who = "cam", beat = stay ? "ez_med" : exchange && line.Cues[0].Who != OzBlueScenes.EvilZod ? "two" : "ez_close", a = OzEvilCardLines.Preroll });
            while (true)
            {
                float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
                bool paused = AudioListener.pause;
                bool up = evilCardOn && comms != null && comms.IsOpen && MenuView() == "Select" && OzPlayBlue.BoxOn;
                if (!up && fadeSpeed <= 0f)
                {
                    fadeSpeed = 1f / 0.3f;
                    if (voiceOn && !ended) { EvilLinesFaded++; OzSoundLog.Add("EvilZod", "evil_card_line", clip.name, "FADE (the card folds)", evilVoice.volume); }
                    Debug.Log("[ZodCameo] EvilZod's card line " + line.Id + " fades out (the card folds: the screen left or the side changed)");
                }
                if (!paused)
                {
                    if (!voiceOn)
                    {
                        if (fadeSpeed > 0f) break;                                                              // gone before his first word: nothing was said
                        t += dt;
                        if (t >= 0f)
                        {
                            evilVoice.volume = VoiceGain(); evilVoice.Play(); voiceOn = true; t = 0f;
                            EvilLinesPlayed++; EvilLastLineId = line.Id;
                            OzSoundLog.Add("EvilZod", "evil_card_line", clip.name, "PLAY", evilVoice.volume);
                        }
                    }
                    else if (evilVoice.isPlaying) t = evilVoice.timeSamples / (float)Mathf.Max(1, clip.frequency);
                    else { ended = true; t += dt; after += dt; }
                    if (fadeSpeed > 0f) { fade = Mathf.MoveTowards(fade, 0f, dt * fadeSpeed); if (fade <= 0f) break; }
                    if (ended && after >= OzEvilCardLines.Tail) break;
                    if (ended && after >= 0.15f) evilTalking = false;                                           // the other voices may go 0.15 s after his last word
                    if (stay && voiceOn && fadeSpeed <= 0f && evilStage)
                    {   // talk motion without hands on the lines that stay on him: a lean in, a nod on each new sentence, the chest and the
                        // shoulders up into the stressed word, then settled again (the stage eases every one of them)
                        if (moveStep == 0 && t >= 0.15f) { moveStep = 1; evilStage.Beat(new OzBlueBeat { who = "ez", beat = "lean_down", a = 0.35f }); evilStage.Beat(new OzBlueBeat { who = "ez", beat = "nod" }); }
                        if (moveStep == 1 && t >= line.KickAt - 0.4f) { moveStep = 2; evilStage.Beat(new OzBlueBeat { who = "ez", beat = "chest_up", a = 1f }); evilStage.Beat(new OzBlueBeat { who = "ez", beat = "shoulders", a = 0.7f }); }
                        if (moveStep == 2 && t >= line.KickAt + 0.8f) { moveStep = 3; evilStage.Beat(new OzBlueBeat { who = "ez", beat = "chest_up", a = 0f }); evilStage.Beat(new OzBlueBeat { who = "ez", beat = "shoulders", a = 0f }); evilStage.Beat(new OzBlueBeat { who = "ez", beat = "lean_down", a = 0f }); }
                        for (int ci = cueSeen + 1; ci < line.Cues.Length; ci++) if (t >= line.Cues[ci].Start) { cueSeen = ci; evilStage.Beat(new OzBlueBeat { who = "ez", beat = "nod" }); }
                    }
                    if (voiceOn && !kicked && t >= line.KickAt && fadeSpeed <= 0f)
                    {   // the stressed word: a jolt; on a line that stays on him the crash zoom into his face (0.9 s, then it eases back to the mid shot)
                        kicked = true;
                        if (evilStage) { if (stay) evilStage.Crash(0.9f, 0.9f); evilStage.Kick(0.6f); }
                    }
                    if (voiceOn && fadeSpeed <= 0f && staticEdge && staticEdge.isPlaying)
                    {   // the card's static gets out of the way of his first word (as under the commander's)
                        staticEdge.volume = Mathf.MoveTowards(staticEdge.volume, 0f, dt / 0.12f * Mathf.Max(0.05f, VoiceGain()));
                        if (staticEdge.volume <= 0.001f) staticEdge.Stop();
                    }
                }
                if (evilVoice) evilVoice.volume = VoiceGain() * fade;
                float m = voiceOn && !ended && fadeSpeed <= 0f ? EvilLiveMouth() : 0f;
                mouth += (m - mouth) * (1f - Mathf.Exp(-Mathf.Max(dt, 0.0001f) / (m > mouth ? 0.025f : 0.045f)));
                if (evilStage) { evilStage.VoiceGain = VoiceGain(); evilStage.Talk(m > 0f ? line.WhoAt(t) : null, m); }   // 1.3 item 30: the cue's speaker (EvilZod on every 1.2 line)
                if (exchange && voiceOn && fadeSpeed <= 0f && comms != null && up && line.WhoAt(t) != speaker)
                {   // 1.3 item 30: the exchange's speaker changes: the label, and the close-up when EvilZod cuts in
                    speaker = line.WhoAt(t);
                    string h = ZodComms.Host; ZodComms.Host = speaker == OzBlueScenes.Sidekick ? OzBlueScenes.SidekickLabel : OzBlueScenes.EvilZodLabel; comms.ApplyRank(); ZodComms.Host = h;
                    if (speaker == OzBlueScenes.EvilZod && evilStage && t > 0.05f) evilStage.Beat(new OzBlueBeat { who = "cam", beat = "ez_close", a = 0.2f });
                }
                if (comms != null && up && fadeSpeed <= 0f)
                {
                    comms.SetCue(voiceOn ? line.CueAt(t) : OzBlueScenes.Company);                              // his words from his first one; the last holds through the tail
                    comms.SetVu(mouth);
                }
                if (voiceOn && !ended && !paused && Time.unscaledTime >= evilNextDuck)
                {   // as under the commander: the music, the ambience and the robots' bus dip (the director's subtle duck follows Speaking)
                    evilNextDuck = Time.unscaledTime + 0.3f;
                    if (AudioDirector.I) AudioDirector.I.AddDuck(AudioDirector.ZodMusicDuckOwned ? 0f : duckMusicDb, 0f, duckAmbienceDb, duckAlanBusDb, 0.55f, 0.2f, 1.0f);
                    if (CombatAudio.I) CombatAudio.I.DuckFor(0.55f, -6f, -2f);
                }
                yield return null;
            }
            if (evilVoice) evilVoice.Stop();
            bool stillUp = evilCardOn && comms != null && comms.IsOpen;
            if (evilStage)
            {
                evilStage.Talk(null, 0f);
                if (stillUp && !stay) evilStage.Beat(new OzBlueBeat { who = "cam", beat = "two", a = 0.8f });  // back to the two-shot (a line that stays on him keeps his mid shot)
            }
            if (stillUp) { comms.SetCue(OzBlueScenes.Company); comms.SetVu(0f); if (exchange) SetEvilSpeaker(); }   // 1.3 item 30: the card's label back to GEN EVILZOD
            evilTalking = false; evilLine = null; evilOtherAt = Time.unscaledTime;                              // the next line waits 0.3 s after this one
        }

        /// <summary>His voice's live level (0..1), the source's own volume taken out (as the blue scenes' director reads it).</summary>
        float EvilLiveMouth()
        {
            if (!evilVoice || !evilVoice.isPlaying) return 0f;
            evilVoice.GetOutputData(evilRms, 0);
            double s = 0; for (int i = 0; i < evilRms.Length; i++) s += evilRms[i] * evilRms[i];
            float db = 10f * Mathf.Log10((float)(s / evilRms.Length) + 1e-10f) - 20f * Mathf.Log10(Mathf.Max(0.05f, evilVoice.volume));
            float k = Mathf.Clamp01((db + 36f) / 20f);
            return k * k;
        }

        /// <summary>One-line state of the card lines (Play checks).</summary>
        public static string EvilLinesStatus()
        {
            if (!I) return "ZodBriefing: none in this scene";
            return $"EvilZod card lines: card {(EvilCardShown ? "up" : "down")}, line {(I.evilLine != null ? I.evilLine.Id : "none")}, speaking {I.evilTalking}, "
                 + $"started {EvilLinesStarted}, heard {EvilLinesPlayed}, faded {EvilLinesFaded}, last {EvilLastLineId}, spoken this visit [{string.Join(",", I.evilSpoken)}], "
                 + $"camera {EvilCardCamera}, mouth {EvilMouth:F2}, subtitle \"{EvilCardCue}\", voice gain {I.VoiceGain():F2}";
        }
    }
}
