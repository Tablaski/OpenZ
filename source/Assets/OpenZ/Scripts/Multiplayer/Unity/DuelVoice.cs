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
        public bool MicrophoneEnabled { get; private set; }
        public string Device => device ?? "Default";
        public string Status { get; private set; } = "Microphone muted";
        public int FramesReceived => Volatile.Read(ref receivedFrames);
        internal void SendProofFrame(float[] frame) { if (udp != null || online != null) Send(1, DuelVoiceCodec.Encode(frame)); }
        public Key TalkKey => (Key)PlayerPrefs.GetInt("oz_duel_talk_key", (int)Key.V);
        public void ToggleMicrophone()
        {
            if (MicrophoneEnabled) { StopMicrophone(); return; }
            try
            {
                if (Microphone.devices.Length == 0) { Status = "No microphone detected"; return; }
                microphone = Microphone.Start(device, true, 2, DuelVoiceCodec.SampleRate);
                if (!microphone) throw new IOException("The microphone could not start.");
                readPosition = 0; MicrophoneEnabled = true; Status = "Hold " + TalkKey + " to talk";
            }
            catch (Exception error) { StopMicrophone(); Status = error.Message; }
        }
        public void NextDevice()
        {
            bool resume = MicrophoneEnabled; StopMicrophone();
            var devices = Microphone.devices;
            if (devices.Length > 0) device = devices[(Array.IndexOf(devices, device) + 1) % devices.Length];
            if (resume) ToggleMicrophone();
        }
        void Update()
        {
            var r = DuelRuntime.I;
            if (!r || !r.Connected || !r.InMatch) { if (udp != null || online != null) StopVoice(); return; }
            try
            {
                if (udp == null && online == null) StartVoice(r);
                if (online != null) while (online.TryReceiveVoice(out var samples)) BufferVoice(samples);
                if (Time.realtimeSinceStartupAsDouble - lastProbe > 1)
                { lastProbe = Time.realtimeSinceStartupAsDouble; Send(0, Array.Empty<byte>()); }
                if (!MicrophoneEnabled || !microphone) return;
                int position = Microphone.GetPosition(device);
                if (position < 0) { StopMicrophone(); Status = "Microphone unavailable"; return; }
                bool talking = Application.isFocused && Keyboard.current != null && Keyboard.current[TalkKey].isPressed && !r.Menus.IsTyping;
                if (!talking) { readPosition = position / DuelVoiceCodec.FrameSamples * DuelVoiceCodec.FrameSamples; Status = "Microphone ready · " + TalkKey; return; }
                Status = "TRANSMITTING";
                int available = (position - readPosition + microphone.samples) % microphone.samples;
                if (available > 1600) { readPosition = (position / 320 * 320 + microphone.samples - 320) % microphone.samples; available = 320; }
                int packets = 0;
                while (available >= DuelVoiceCodec.FrameSamples && packets++ < 4)
                {
                    int channels = microphone.channels;
                    var raw = new float[DuelVoiceCodec.FrameSamples * channels]; microphone.GetData(raw, readPosition);
                    var frame = new float[DuelVoiceCodec.FrameSamples];
                    for (int i = 0; i < frame.Length; i++)
                    { for (int c = 0; c < channels; c++) frame[i] += raw[i * channels + c]; frame[i] /= channels; }
                    Send(1, DuelVoiceCodec.Encode(frame)); readPosition = (readPosition + frame.Length) % microphone.samples; available -= frame.Length;
                }
            }
            catch (SocketException error) when (error.SocketErrorCode == SocketError.WouldBlock) { }
            catch (Exception error) { Status = error.Message; StopVoice(); }
        }
        void StartVoice(DuelRuntime runtime)
        {
            online = runtime.OnlinePeer;
            if (online == null)
            {
                token = Encoding.ASCII.GetBytes(runtime.VoiceSecret); expectedAddress = runtime.PeerAddress;
                udp = new UdpClient(runtime.IsHost ? runtime.TcpPort + 1 : 0); udp.Client.Blocking = false;
                if (!runtime.IsHost) destination = new IPEndPoint(expectedAddress, runtime.TcpPort + 1);
                _ = Receive(udp);
            }
            source = gameObject.AddComponent<AudioSource>(); source.spatialBlend = 0; source.volume = .8f; source.loop = true;
            source.clip = AudioClip.Create("Duel radio", DuelVoiceCodec.SampleRate, 1, DuelVoiceCodec.SampleRate, true, ReadAudio); source.Play();
            sendSequence = 0; receivedSequence = -1;
        }
        void Send(byte kind, byte[] payload)
        {
            if (online != null) { if (kind == 1) online.SendVoice(payload); return; }
            if (destination == null || udp == null) return;
            var packet = new byte[HeaderSize + payload.Length];
            packet[0] = (byte)'O'; packet[1] = (byte)'Z'; packet[2] = (byte)'V'; packet[3] = 1;
            Array.Copy(token, 0, packet, 4, 32); packet[36] = kind;
            Array.Copy(BitConverter.GetBytes(++sendSequence), 0, packet, 37, 4); Array.Copy(payload, 0, packet, HeaderSize, payload.Length);
            udp.Send(packet, packet.Length, destination);
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
            MicrophoneEnabled = false; Status = "Microphone muted";
        }
        void StopVoice()
        {
            StopMicrophone(); var socket = udp; udp = null; socket?.Dispose(); destination = null; online = null;
            if (source) { var clip = source.clip; source.Stop(); Destroy(source); if (clip) Destroy(clip); source = null; }
            while (playback.TryDequeue(out _)) { } Interlocked.Exchange(ref buffered, 0); primed = false;
        }
        void OnDestroy() { StopVoice(); }
    }
}
