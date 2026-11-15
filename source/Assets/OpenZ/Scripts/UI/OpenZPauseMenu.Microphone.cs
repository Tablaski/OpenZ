using System;
using System.Linq;
using OpenZ.Multiplayer;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace OpenZ
{
    public partial class OpenZPauseMenu
    {
        public const int MicrophoneTab = 4;
        RectTransform microphonePage, microphoneTrack, microphoneDevices, microphoneDeviceContent;
        Image microphoneGainFill, microphoneThumb, microphoneMeter;
        OzText microphoneGainText, microphoneLevelText, microphoneStatus;
        HudButton microphoneToggle, microphoneDevice, microphoneKey;
        bool microphoneBinding, microphoneDragging;
        float microphoneDeviceScroll, microphoneDeviceExtent;
        readonly System.Collections.Generic.List<HudButton> microphoneDeviceButtons = new System.Collections.Generic.List<HudButton>();
        public bool MicrophoneBinding => microphoneBinding;
        DuelVoice MicrophoneVoice => DuelRuntime.I ? DuelRuntime.I.Voice : null;

        HudButton MicrophoneButton(string name, string text, float y, Action action, float height = 42)
        {
            float w = OzLayout.PauseW - 2 * OzLayout.OptX;
            var b = HudButton.Make(microphonePage, name, text, new Vector2(w, height), HudButtonKind.Small, 12, .04f);
            b.Rt.TL(OzLayout.OptX, y, w, height); b.Priority = 100; b.OnClick = c => action();
            return b;
        }

        void BuildMicrophoneTab()
        {
            microphonePage = Hud.Node("TabMicrophone", optionsRoot); microphonePage.Fill();
            float x = OzLayout.OptX, w = OzLayout.PauseW - 2 * x;
            microphoneToggle = MicrophoneButton("MicrophoneMute", "", 90, () => MicrophoneVoice.ToggleMicrophone());
            Hud.Label(microphonePage, "InputDeviceLabel", "INPUT DEVICE", 11, OzUi.Muted).At(x, 150);
            microphoneDevice = MicrophoneButton("MicrophoneDevice", "", 174, ToggleMicrophoneDevices, 54);
            microphoneDevice.Tooltip = "Choose the microphone used for voice chat. System default follows Windows.";
            microphoneGainText = Hud.Label(microphonePage, "MicrophoneGainLabel", "", 12, OzUi.Ink).At(x, 252);
            microphoneTrack = Hud.Node("MicrophoneGainTrack", microphonePage); microphoneTrack.TL(x, 284, w, 10);
            var track = Hud.Img(microphoneTrack, "Track", OzUi.GM950); track.rectTransform.Fill();
            microphoneGainFill = Hud.Img(microphoneTrack, "Gain", OzUi.Amber); microphoneGainFill.rectTransform.Fill();
            microphoneThumb = Hud.Kit(microphoneTrack, "Thumb", "Buttons/slider_thumb", new OzRect(0, -5, 14, 20), null, Image.Type.Simple);
            var hot = MicrophoneButton("MicrophoneGainHotspot", "", 272, () => { }, 34); hot.Bg.enabled = false;
            Hud.Label(microphonePage, "GainMinimum", "0%", 10, OzUi.Muted).At(x, 310);
            Hud.Label(microphonePage, "GainMaximum", "400%", 10, OzUi.Muted).At(x + w, 310, OzH.R, false, 60);
            microphoneLevelText = Hud.Label(microphonePage, "MicrophoneLevelLabel", "", 12, OzUi.Ink).At(x, 348);
            var meter = Hud.Img(microphonePage, "MicrophoneLevelTrack", OzUi.GM950); meter.rectTransform.TL(x, 380, w, 16);
            microphoneMeter = Hud.Img(meter.transform, "MicrophoneLevel", OzUi.Go); microphoneMeter.rectTransform.Fill();
            microphoneStatus = Hud.Label(microphonePage, "MicrophoneStatus", "", 11, OzUi.Muted).Body(x, 412, w);
            microphoneKey = MicrophoneButton("MicrophoneTalkKey", "", 486, () => microphoneBinding = true);
            Hud.Label(microphonePage, "MicrophoneHint", "Hold the shortcut to talk.\nThe input meter also works while muted.", 11, OzUi.Muted).Body(x, 544, w);
            microphonePage.gameObject.SetActive(false);
        }

        void ToggleMicrophoneDevices()
        {
            if (microphoneDevices)
            {
                if (microphoneDevices.gameObject.activeSelf) { microphoneDevices.gameObject.SetActive(false); return; }
                Destroy(microphoneDevices.gameObject); microphoneDeviceButtons.Clear(); microphoneDeviceScroll = 0;
            }
            float w = OzLayout.PauseW - 2 * OzLayout.OptX;
            microphoneDevices = Hud.Node("MicrophoneDevices", microphonePage); microphoneDevices.TL(OzLayout.OptX, 230, w, 320);
            var bg = Hud.Img(microphoneDevices, "Background", OzUi.GM950); bg.rectTransform.Fill();
            microphoneDevices.gameObject.AddComponent<RectMask2D>();
            var hit = HudButton.Make(microphoneDevices, "DeviceListHit", null, new Vector2(w, 320), HudButtonKind.Small);
            hit.Rt.TL(0, 0, w, 300); hit.Bg.enabled = false; hit.Priority = 109;
            microphoneDeviceContent = Hud.Node("Devices", microphoneDevices);
            string[] devices = new[] { "System default" }.Concat(Microphone.devices).ToArray();
            microphoneDeviceExtent = devices.Length * 64; microphoneDeviceContent.TL(0, 0, w, microphoneDeviceExtent);
            for (int i = 0; i < devices.Length; i++)
            {
                string selected = i == 0 ? null : devices[i];
                var b = HudButton.Make(microphoneDeviceContent, "InputDevice" + i, null, new Vector2(w, 60), HudButtonKind.Small);
                b.Rt.TL(0, i * 64, w, 60); b.Priority = 110;
                microphoneDeviceButtons.Add(b); b.Rt.gameObject.SetActive(i < 5);
                Hud.Label(b.Content, "DeviceName", devices[i], 14, OzUi.Ink).Body(10, 6, w - 20);
                b.OnClick = c => { MicrophoneVoice.SelectDevice(selected); microphoneDevices.gameObject.SetActive(false); };
            }
        }

        void SetMicrophoneTabVisible(bool visible)
        {
            if (!microphonePage) return;
            microphonePage.gameObject.SetActive(visible);
            if (!visible) { microphoneBinding = microphoneDragging = false; if (microphoneDevices) microphoneDevices.gameObject.SetActive(false); }
            if (MicrophoneVoice) MicrophoneVoice.Monitoring = visible && IsOpen && !showControls;
        }

        void TickMicrophoneBinding()
        {
            if (!microphoneBinding || Keyboard.current == null) return;
            foreach (var key in Keyboard.current.allKeys)
                if (key.wasPressedThisFrame)
                {
                    if (key.keyCode != Key.Escape) { PlayerPrefs.SetInt("oz_duel_talk_key", (int)key.keyCode); PlayerPrefs.Save(); }
                    microphoneBinding = false; break;
                }
        }

        void TickMicrophone()
        {
            if (!microphonePage || !MicrophoneVoice) return;
            bool active = IsOpen && !showControls && microphonePage.gameObject.activeInHierarchy;
            MicrophoneVoice.Monitoring = active;
            if (!active) { microphoneBinding = microphoneDragging = false; return; }
            var voice = MicrophoneVoice;
            microphoneToggle.Label.Set(voice.MicrophoneEnabled ? "MICROPHONE: ON · CLICK TO MUTE" : "MICROPHONE: MUTED · CLICK TO ENABLE");
            string device = voice.Device;
            microphoneDevice.Label.Set((device.Length > 36 ? device.Substring(0, 33) + "…" : device) + "  ▼");
            microphoneGainText.Set("INPUT VOLUME: " + Mathf.RoundToInt(voice.Gain * 100) + "%");
            microphoneGainFill.rectTransform.anchorMax = new Vector2(voice.Gain / 4, 1);
            microphoneThumb.rectTransform.anchoredPosition = new Vector2(microphoneTrack.rect.width * voice.Gain / 4 - 7, 5);
            microphoneMeter.rectTransform.anchorMax = new Vector2(voice.InputLevel, 1);
            microphoneMeter.color = voice.InputLevel > .95f ? OzUi.AlertText : OzUi.Go;
            microphoneLevelText.Set("TEST YOUR MICROPHONE: " + Mathf.RoundToInt(voice.InputLevel * 100) + "%");
            microphoneStatus.Set(voice.Status);
            microphoneKey.Label.Set(microphoneBinding ? "PRESS A KEY · ESC TO CANCEL" : "PUSH TO TALK: " + voice.TalkKey);
            var mouse = Mouse.current;
            if (mouse == null || OzConfirm.Blocks) return;
            bool devicesOpen = microphoneDevices && microphoneDevices.gameObject.activeSelf;
            if (devicesOpen)
            {
                if (OpenZHud.Contains(microphoneDevices, mouse.position.ReadValue()))
                {
                    microphoneDeviceScroll = Mathf.Clamp(Mathf.Round((microphoneDeviceScroll - mouse.scroll.ReadValue().y * .4f) / 64) * 64, 0, Mathf.Max(0, microphoneDeviceExtent - 320));
                    int first = Mathf.RoundToInt(microphoneDeviceScroll / 64);
                    for (int i = 0; i < microphoneDeviceButtons.Count; i++) microphoneDeviceButtons[i].Rt.gameObject.SetActive(i >= first && i < first + 5);
                    microphoneDeviceContent.anchoredPosition = new Vector2(0, microphoneDeviceScroll);
                }
                return;
            }
            RectTransformUtility.ScreenPointToLocalPointInRectangle(microphoneTrack, mouse.position.ReadValue(), null, out var point);
            var rect = microphoneTrack.rect;
            if (mouse.leftButton.wasPressedThisFrame && point.x >= rect.xMin && point.x <= rect.xMax && point.y >= rect.yMin - 12 && point.y <= rect.yMax + 12) microphoneDragging = true;
            if (microphoneDragging)
            {
                voice.Gain = Mathf.Clamp01((point.x - rect.xMin) / rect.width) * 4;
                if (!mouse.leftButton.isPressed) { microphoneDragging = false; PlayerPrefs.Save(); }
            }
        }
    }
}
