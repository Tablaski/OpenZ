using UnityEngine;
using UnityEngine.UI;

namespace OpenZ
{
    /// <summary>
    /// 1.2 item 6 (UI/OzUeControlsRoot.cs; owner 6j "pause / play is the main button needs better location", 6k "play and pause toggle
    /// to redo and not same color", "the whole play menu should be back down where it was"; spec E021-E027): the play bar right above the
    /// dock, centred on the free field: ONE PLAY / PAUSE toggle (running: PAUSE, dark with amber bars; paused: PLAY, bright green with a
    /// dark triangle; Space does the same through P4's keys), x1 x2 x4, CLEAR FIELD, SAMPLE FIGHT, SOUND ON / OFF.
    /// The toggle and the sound button keep one width in both states, so nothing in the bar moves when they change.
    /// </summary>
    public sealed class OzUePlayBar
    {
        public static readonly Color PauseTop = new Color32(0x25, 0x2A, 0x27, 255), PauseLow = new Color32(0x17, 0x1A, 0x18, 255);
        public static readonly Color GreenTop = new Color32(0x8A, 0xF0, 0xA8, 255), GreenMid = new Color32(0x3F, 0xCF, 0x6C, 255), GreenLow = new Color32(0x26, 0xA6, 0x50, 255);
        public static readonly Color GreenEdge = new Color32(0x1D, 0x7A, 0x3B, 255), GreenInk = new Color32(0x06, 0x21, 0x0F, 255);
        /// <summary>Checks: clicks on each control since the editor opened.</summary>
        public static int PlayClicks, SpeedClicks, ClearClicks, SampleClicks, SoundClicks;

        OpenZHud hud;
        public RectTransform Plate;
        HudButton play; RawImage playFill, playGlow; Image[] playEdge; Image playIcon, playWash; OzText playLabel; OzUeKeycap playKey;
        OzUeSeg speed; HudButton clear, sample, sound;
        OzUeBottomLayout.Play lay;
        bool shownPlaying, shownHover, shownSound, first = true;

        public UeBox PlateBox => lay != null ? lay.plate : default;

        public void Build(OzUnitEditor ed, Transform panels)
        {
            hud = ed.Hud;
            PlayClicks = SpeedClicks = ClearClicks = SampleClicks = SoundClicks = 0;
            Plate = Hud.Node("UE_PlayBar", panels);
            Hud.Plate(Plate, "sm");
            hud.AddBlocker(Plate);

            play = OzUeW.Bare(Plate, "PlayPause", 155f, OzUeBottomLayout.PlayBtnH);
            playGlow = OzUeW.Raw(play.Rt, "Glow", null, 0f, 0f, 1f, 1f, OzUi.A(GreenMid, 0.35f));
            playGlow.transform.SetSiblingIndex(0);
            playFill = OzUeW.Gradient(play.Rt, "Fill", PauseTop, PauseLow);
            playFill.transform.SetSiblingIndex(1);
            playWash = OzUeW.Fill(play.Rt, "Hover", new Color(1f, 1f, 1f, 0.04f)); playWash.transform.SetSiblingIndex(2); playWash.enabled = false;
            playEdge = Hud.Border(play.Rt, OzUi.GM400);
            playIcon = Hud.Icon(play.Content, "Icon", "pause", OzUeBottomLayout.Border + OzUeBottomLayout.PlayPadL, 9f, OzUeBottomLayout.PlayIcon, OzUi.Amber);
            playLabel = Hud.Label(play.Content, "Label", OzUeBottomLayout.PauseWord, OzUeBottomLayout.PlayText, OzUi.Ink, OzUeBottomLayout.PlayTrack, OzStyle.Plain);
            playKey = new OzUeKeycap(play.Content, "Key", OzUeBottomLayout.KeySpace, OzUeBottomLayout.KeyTrack);
            play.OnClick = c => { PlayClicks++; OzUeControls.TogglePlay(); };

            speed = new OzUeSeg(Plate, "Speed", OzUeBottomLayout.SpeedWords, OzUeBottomLayout.SpeedText, OzUeBottomLayout.SpeedTrack);
            speed.OnPick = k => { SpeedClicks++; OzUeControls.SetSpeed(k == 0 ? 1f : k == 1 ? 2f : 4f); };
            speed.SetTip(0, OzUeW.Tip("Normal speed")); speed.SetTip(1, OzUeW.Tip("Twice as fast")); speed.SetTip(2, OzUeW.Tip("Four times as fast"));

            clear = HudButton.Make(Plate, "Clear", OzUeBottomLayout.ClearLong, new Vector2(113f, OzUeBottomLayout.PlayBtnH), HudButtonKind.Small, OzUeBottomLayout.BtnText, OzUeBottomLayout.BtnTrack, HudButtonStyle.Danger);
            clear.Tooltip = OzUeW.Tip("Clear field", null, "Remove every unit, flag and obstacle from the field");
            clear.OnClick = c => { ClearClicks++; int n = OzUePlayground.ClearField(); AudioDirector.UI("ui_cancel"); Debug.Log($"[OpenZ UnitEditor] CLEAR FIELD: {n} removed"); };
            sample = HudButton.Make(Plate, "Sample", OzUeBottomLayout.SampleLong, new Vector2(127f, OzUeBottomLayout.PlayBtnH), HudButtonKind.Small, OzUeBottomLayout.BtnText, OzUeBottomLayout.BtnTrack);
            sample.Tooltip = OzUeW.Tip("Sample fight", null, "Drop a small mixed fight on both sides, on free ground (never on units, flags or obstacles)",
                                       "At most " + OzUeCap.SampleFightsMax + " on the field at once; CLEAR FIELD makes room");
            sample.OnClick = c => { SampleClicks++; int n = OzUePlayground.SampleFight(); Debug.Log($"[OpenZ UnitEditor] SAMPLE FIGHT: {n} groups dropped"); };
            sound = HudButton.Make(Plate, "Sound", OzUeBottomLayout.SoundOnWord, new Vector2(95f, OzUeBottomLayout.PlayBtnH), HudButtonKind.Small, OzUeBottomLayout.BtnText, OzUeBottomLayout.BtnTrack);
            sound.Tooltip = OzUeW.Tip("Battle sound on / off");
            sound.OnClick = c => { SoundClicks++; OzUeControls.SetSound(!OzUeControls.SoundOn); };
        }

        public void Layout(OzUeBottomLayout.Play p)
        {
            lay = p;
            Plate.TL(p.plate.x, p.plate.y, p.plate.w, p.plate.h);
            play.Rt.TL(p.play.x, p.play.y, p.play.w, p.play.h);
            playGlow.texture = OzUeArt.Glow(p.play.w, p.play.h, 10f);
            playGlow.rectTransform.TL(-10f, -10f, p.play.w + 20f, p.play.h + 20f);
            float lx = OzUeBottomLayout.Border + OzUeBottomLayout.PlayPadL + OzUeBottomLayout.PlayIcon + OzUeBottomLayout.PlayInner;
            playLabel.At(lx, p.play.h * 0.5f, OzH.L, true, 0f);
            playKey.Rt.TL(p.play.w - OzUeBottomLayout.Border - OzUeBottomLayout.PlayPadR - p.keyW, (p.play.h - OzUeBottomLayout.KeyH) * 0.5f, p.keyW, OzUeBottomLayout.KeyH);
            speed.Layout(p.speed, p.speedCells);
            clear.Rt.TL(p.clear.x, p.clear.y, p.clear.w, p.clear.h);
            if (clear.Label != null) clear.Label.Set(p.clearWord);
            sample.Rt.TL(p.sample.x, p.sample.y, p.sample.w, p.sample.h);
            if (sample.Label != null) sample.Label.Set(p.sampleWord);
            sound.Rt.TL(p.sound.x, p.sound.y, p.sound.w, p.sound.h);
            first = true;
        }

        public void Tick(bool hidden)
        {
            Plate.gameObject.SetActiveFast(!hidden);
            if (hidden) return;
            speed.Tick();
            float sp = OzUeControls.Speed;
            speed.Select(Mathf.Abs(sp - 1f) < 0.01f ? 0 : Mathf.Abs(sp - 2f) < 0.01f ? 1 : Mathf.Abs(sp - 4f) < 0.01f ? 2 : -1);
            bool playing = OzUeControls.Playing, hov = play.Hovered, snd = OzUeControls.SoundOn;
            if (!first && playing == shownPlaying && hov == shownHover && snd == shownSound) return;
            first = false; shownPlaying = playing; shownHover = hov; shownSound = snd;
            if (playing)
            {   // RUNNING: the button offers PAUSE, calm and dark, amber pause bars
                playFill.texture = OzUeArt.Gradient(PauseTop, PauseLow);
                foreach (var e in playEdge) e.color = hov ? (Color)OzUi.Amber : (Color)OzUi.GM400;
                playGlow.enabled = false; playWash.enabled = false;
                Hud.SetIcon(playIcon, "pause", OzUeBottomLayout.PlayIcon); playIcon.color = OzUi.Amber;
                playLabel.Set(OzUeBottomLayout.PauseWord); playLabel.color = OzUi.Ink;
                playKey.Colours(OzUi.GM700, OzUi.GM400, OzUi.Ink);
                play.Tooltip = OzUeW.Tip("Pause the fight", "SPACE");
            }
            else
            {   // PAUSED: the button offers PLAY, bright green, a dark play triangle
                playFill.texture = OzUeArt.Gradient(GreenTop, GreenLow, GreenMid, 0.55f);
                foreach (var e in playEdge) e.color = GreenEdge;
                playGlow.enabled = true; playWash.enabled = hov;
                Hud.SetIcon(playIcon, "play", OzUeBottomLayout.PlayIcon); playIcon.color = GreenInk;
                playLabel.Set(OzUeBottomLayout.PlayWord); playLabel.color = GreenInk;
                playKey.Colours(OzUi.A(GreenInk, 0.12f), OzUi.A(GreenInk, 0.4f), GreenInk);
                play.Tooltip = OzUeW.Tip("Play: the fight goes on", "SPACE");
            }
            if (sound.Label != null) sound.Label.Set(snd ? OzUeBottomLayout.SoundOnWord : OzUeBottomLayout.SoundOffWord);
        }
    }
}
