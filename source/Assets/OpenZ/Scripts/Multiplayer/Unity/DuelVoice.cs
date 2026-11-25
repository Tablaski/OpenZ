using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OpenZ.Multiplayer
{
    /// <summary>Optional peer voice on UDP (game TCP port + 1), independent of the reliable game queue.</summary>
    public sealed class DuelVoice : MonoBehaviour
    {
        const int HeaderSize = 41;
        readonly ConcurrentQueue<float> playback = new ConcurrentQueue<float>();
        UdpClient udp;
        DuelRtcPeer online;
        SkirmishOnline skirmish;
        bool HasVoiceConnection => udp != null || online != null || skirmish != null;
        double roomVoiceUntil;
        IPEndPoint destination;
        IPAddress expectedAddress;
        byte[] token;
        AudioSource source;
        AudioClip microphone;
        string device;
        int readPosition, sendSequence, receivedSequence = -1, buffered;
        int receivedFrames;
        volatile bool primed;
        double lastProbe;
        double retryMicrophoneAt;
        int audioDeviceChanged;
        public bool MicrophoneEnabled { get; private set; }
        public string Device => device ?? "System default";
        public bool Monitoring { get; set; }
        public bool HudMonitoring { get; set; }
        public bool ChatMonitoring { get; set; }
        public float InputLevel { get; private set; }
        public float Gain { get => PlayerPrefs.GetFloat("oz_duel_mic_gain", 1f); set => PlayerPrefs.SetFloat("oz_duel_mic_gain", Mathf.Clamp(value, 0, 4)); }
        public float SpeakerVolume
        {
            get => PlayerPrefs.GetFloat("oz_duel_speaker_volume", .8f);
            set
            {
                value = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat("oz_duel_speaker_volume", value);
                if (source) source.volume = value;
            }
        }
        void Awake()
        {
            device = PlayerPrefs.GetString("oz_duel_mic_device", "");
            if (device == "" || !Microphone.devices.Contains(device)) device = null;
            Mode = (MicrophoneMode)Mathf.Clamp(PlayerPrefs.GetInt("oz_duel_mic_mode", 0), 0, 2);
            if (Mode != MicrophoneMode.Mute) lastLiveMode = Mode;
            AudioSettings.OnAudioConfigurationChanged += OnAudioConfigurationChanged;
        }
        void OnAudioConfigurationChanged(bool _) => Interlocked.Exchange(ref audioDeviceChanged, 1);
        public void SelectDevice(string selected)
        {
            var mode = Mode; StopMicrophone(); device = selected; retryMicrophoneAt = 0;
            PlayerPrefs.SetString("oz_duel_mic_device", device ?? ""); PlayerPrefs.Save();
            SetMode(mode);
        }
        void StartMicrophone()
        {
            if (microphone) return;
            if (Microphone.devices.Length == 0) throw new IOException("No microphone detected");
            microphone = Microphone.Start(device, true, 2, DuelVoiceCodec.SampleRate);
            if (!microphone) throw new IOException("The microphone could not start.");
            readPosition = 0;
        }
        public string Status { get; private set; } = "Microphone muted";
        public int FramesReceived => Volatile.Read(ref receivedFrames);
        internal void SendProofFrame(float[] frame) { if (HasVoiceConnection) Send(1, DuelVoiceCodec.Encode(frame)); }
        public enum MicrophoneMode { Mute, AlwaysOn, TapToSpeak }
        public MicrophoneMode Mode { get; private set; }
        MicrophoneMode lastLiveMode = MicrophoneMode.AlwaysOn;
        bool tapped;
        public bool Transmitting { get; private set; }
        public string ModeName => Mode == MicrophoneMode.AlwaysOn ? "ALWAYS ON" : Mode == MicrophoneMode.TapToSpeak ? "TAP TO SPEAK" : "MUTE";
        public void SetMode(MicrophoneMode mode)
        {
            if (mode != MicrophoneMode.Mute && DuelRuntime.I && DuelRuntime.I.RoomVoiceMuted) return;
            Mode = mode; tapped = false; PlayerPrefs.SetInt("oz_duel_mic_mode", (int)mode); PlayerPrefs.Save();
            if (mode == MicrophoneMode.Mute) StopMicrophone();
            else
            {
                lastLiveMode = mode;
                retryMicrophoneAt = 0;
            }
        }
        internal MicrophoneMode PreferredMode => Mode == MicrophoneMode.Mute ? lastLiveMode : Mode;

        internal void ChooseLiveMode(MicrophoneMode mode)
        {
            if (Mode == MicrophoneMode.Mute) lastLiveMode = mode;
            else SetMode(mode);
        }

        public void CycleMode() => SetMode((MicrophoneMode)(((int)Mode + 1) % 3));
        public Key TalkKey => (Key)PlayerPrefs.GetInt("oz_duel_talk_key", (int)Key.V);
        public void ToggleMicrophone()
        {
            SetMode(Mode == MicrophoneMode.Mute ? lastLiveMode : MicrophoneMode.Mute);
        }
        void Update()
        {
            Transmitting = false;
            var r = DuelRuntime.I;
            try
            {
                if (Interlocked.Exchange(ref audioDeviceChanged, 0) != 0)
                {
                    bool resumeTalk = tapped;
                    StopMicrophone();
                    tapped = resumeTalk;
                    retryMicrophoneAt = 0;
                    StopPlayback();
                    if (HasVoiceConnection) StartPlayback();
                }
                if (!r || !r.Connected) { if (HasVoiceConnection) StopVoice(); }
                else
                {
                    if (!HasVoiceConnection) StartVoice(r);
                    if (online != null) while (online.TryReceiveVoice(out var samples)) BufferVoice(samples);
                    if (skirmish != null) while (skirmish.TryReceiveVoice(out var samples)) BufferVoice(samples);
                    if (Time.realtimeSinceStartupAsDouble - lastProbe > 1)
                    { lastProbe = Time.realtimeSinceStartupAsDouble; Send(0, Array.Empty<byte>()); }
                }
                if (!Monitoring && !HudMonitoring && !ChatMonitoring && Mode == MicrophoneMode.Mute) { if (microphone) StopMicrophone(); return; }
                if (Time.realtimeSinceStartupAsDouble < retryMicrophoneAt) return;
                try { StartMicrophone(); }
                catch (Exception)
                {
                    StopMicrophone(); retryMicrophoneAt = Time.realtimeSinceStartupAsDouble + 3;
                    Status = "Microphone unavailable. Choose another input device."; return;
                }
                MicrophoneEnabled = Mode != MicrophoneMode.Mute;
                int position = Microphone.GetPosition(device);
                if (position < 0) { StopMicrophone(); Status = "Microphone unavailable"; return; }
                bool mutedByHost = r && r.RoomVoiceMuted;
                bool canTalk = MicrophoneEnabled && r && r.Connected && !mutedByHost && (!r.Game || r.Game.World?.GameOver != true) && Application.isFocused;
                if (!canTalk) tapped = false;
                // Text focus suppresses the shortcut. An active microphone keeps sending while you write.
                else if (Mode == MicrophoneMode.TapToSpeak && !r.Menus.IsTyping && Keyboard.current != null &&
                    TalkKey != Key.None && Keyboard.current[TalkKey].wasPressedThisFrame) tapped = !tapped;
                bool talking = canTalk && (Mode == MicrophoneMode.AlwaysOn || tapped);
                int frameSamples = DuelVoiceCodec.FrameSamples;
                int available = (position - readPosition + microphone.samples) % microphone.samples;
                if (available > frameSamples * 5)
                {
                    readPosition = (position + microphone.samples - frameSamples) % microphone.samples;
                    available = frameSamples;
                }
                float peak = 0, gain = Gain;
                int packets = 0;
                while (available >= DuelVoiceCodec.FrameSamples && packets++ < 4)
                {
                    int channels = microphone.channels;
                    var raw = new float[frameSamples * channels];
                    if (!microphone.GetData(raw, readPosition)) break;
                    var frame = new float[frameSamples];
                    for (int i = 0; i < frame.Length; i++)
                    {
                        for (int c = 0; c < channels; c++) frame[i] += raw[i * channels + c];
                        frame[i] = Mathf.Clamp(frame[i] / channels * gain, -1, 1); peak = Mathf.Max(peak, Mathf.Abs(frame[i]));
                    }
                    double now = Time.realtimeSinceStartupAsDouble;
                    if (r.SkirmishRoom && peak >= .015f) roomVoiceUntil = now + .20;
                    if (talking && (!r.SkirmishRoom || now < roomVoiceUntil) && Send(1, DuelVoiceCodec.Encode(frame)))
                        transmitUntil = Time.realtimeSinceStartupAsDouble + .15;
                    readPosition = (readPosition + frame.Length) % microphone.samples; available -= frame.Length;
                }
                InputLevel = Mathf.Max(peak, InputLevel - Time.unscaledDeltaTime * 2);
                Transmitting = talking && Time.realtimeSinceStartupAsDouble < transmitUntil;
                Status = mutedByHost ? "Voice muted by host" : !MicrophoneEnabled ? "Microphone muted · local meter only" :
                    Transmitting ? "TRANSMITTING" : talking ? "Waiting for voice connection" :
                    Mode == MicrophoneMode.AlwaysOn ? "Always on" : "Tap " + TalkKey + " to speak";
            }
            catch (SocketException error) when (error.SocketErrorCode == SocketError.WouldBlock) { }
            catch (Exception error) { StopVoice(); Status = error.Message; }
        }
        void StartVoice(DuelRuntime runtime)
        {
            online = runtime.OnlinePeer;
            skirmish = runtime.RoomTransport?.TcpPeer == null ? runtime.RoomTransport : null;
            if (online == null && skirmish == null)
            {
                token = Encoding.ASCII.GetBytes(runtime.VoiceSecret); expectedAddress = runtime.PeerAddress;
                udp = new UdpClient(runtime.IsHost ? runtime.TcpPort + 1 : 0); udp.Client.Blocking = false;
                if (!runtime.IsHost) destination = new IPEndPoint(expectedAddress, runtime.TcpPort + 1);
                _ = Receive(udp);
            }
            StartPlayback();
        }
        double transmitUntil;
        void StartPlayback()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.spatialBlend = 0; source.volume = SpeakerVolume; source.loop = true;
            source.ignoreListenerPause = true;
            source.clip = AudioClip.Create("Duel radio", DuelVoiceCodec.SampleRate, 1, DuelVoiceCodec.SampleRate, true, ReadAudio);
            source.Play();
        }
        bool Send(byte kind, byte[] payload)
        {
            if (online != null) return kind == 1 && online.SendVoice(payload);
            if (skirmish != null) return kind == 1 && skirmish.SendVoice(payload);
            if (destination == null || udp == null) return false;
            var packet = new byte[HeaderSize + payload.Length];
            packet[0] = (byte)'O'; packet[1] = (byte)'Z'; packet[2] = (byte)'V'; packet[3] = 1;
            Array.Copy(token, 0, packet, 4, 32); packet[36] = kind;
            Array.Copy(BitConverter.GetBytes(++sendSequence), 0, packet, 37, 4); Array.Copy(payload, 0, packet, HeaderSize, payload.Length);
            return udp.Send(packet, packet.Length, destination) == packet.Length;
        }
        async Task Receive(UdpClient socket)
        {
            try
            {
                while (udp == socket)
                {
                    var result = await socket.ReceiveAsync().ConfigureAwait(false);
                    var bytes = result.Buffer;
                    if (!result.RemoteEndPoint.Address.Equals(expectedAddress) || bytes.Length < HeaderSize ||
                        bytes[0] != 'O' || bytes[1] != 'Z' || bytes[2] != 'V' || bytes[3] != 1) continue;
                    int difference = 0; for (int i = 0; i < 32; i++) difference |= bytes[4 + i] ^ token[i];
                    if (difference != 0 || (bytes[36] != 0 && bytes[36] != 1)) continue;
                    if (bytes[36] == 0 && bytes.Length != HeaderSize) continue;
                    if (bytes[36] == 1 && bytes.Length != HeaderSize + DuelVoiceCodec.FrameSamples) continue;
                    int sequence = BitConverter.ToInt32(bytes, 37);
                    if (sequence <= receivedSequence) continue;
                    receivedSequence = sequence; destination = result.RemoteEndPoint;
                    if (bytes[36] == 0) continue;
                    var payload = new byte[DuelVoiceCodec.FrameSamples]; Array.Copy(bytes, HeaderSize, payload, 0, payload.Length);
                    BufferVoice(payload);
                }
            }
            catch (Exception) { /* Socket shutdown ends the receive loop; microphone status stays on the main thread. */ }
        }
        void BufferVoice(byte[] payload)
        {
            Interlocked.Increment(ref receivedFrames);
            foreach (float sample in DuelVoiceCodec.Decode(payload)) { playback.Enqueue(sample); Interlocked.Increment(ref buffered); }
            while (Volatile.Read(ref buffered) > 4800 && playback.TryDequeue(out _)) Interlocked.Decrement(ref buffered);
        }
        void ReadAudio(float[] data)
        {
            if (!primed && Volatile.Read(ref buffered) >= 1600) primed = true;
            for (int i = 0; i < data.Length; i++)
            {
                if (primed && playback.TryDequeue(out var sample)) { data[i] = sample; Interlocked.Decrement(ref buffered); }
                else { data[i] = 0; primed = false; }
            }
        }
        void StopMicrophone()
        {
            if (microphone) { Microphone.End(device); Destroy(microphone); microphone = null; }
            InputLevel = 0; MicrophoneEnabled = false; tapped = false; Transmitting = false; transmitUntil = 0; Status = "Microphone muted";
        }
        void StopVoice()
        {
            StopMicrophone(); var socket = udp; udp = null; socket?.Dispose(); destination = null; online = null; skirmish = null;
            sendSequence = 0; receivedSequence = -1;
            StopPlayback();
        }
        void StopPlayback()
        {
            if (source) { var clip = source.clip; source.Stop(); Destroy(source); if (clip) Destroy(clip); source = null; }
            while (playback.TryDequeue(out _)) { } Interlocked.Exchange(ref buffered, 0); primed = false;
        }
        void OnDestroy() { AudioSettings.OnAudioConfigurationChanged -= OnAudioConfigurationChanged; StopVoice(); }
    }
}
