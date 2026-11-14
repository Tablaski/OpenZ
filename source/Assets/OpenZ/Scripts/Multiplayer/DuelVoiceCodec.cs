using System;

namespace OpenZ.Multiplayer
{
    /// <summary>Independent 20 ms G.711 mu-law frames at 16 kHz: 16 KB/s before UDP headers.</summary>
    public static class DuelVoiceCodec
    {
        public const int SampleRate = 16000, FrameSamples = 320;
        public static byte[] Encode(float[] samples)
        {
            if (samples == null || samples.Length != FrameSamples) throw new ArgumentException("A voice frame is 320 samples.");
            var bytes = new byte[FrameSamples];
            for (int i = 0; i < bytes.Length; i++)
            {
                float value = float.IsNaN(samples[i]) ? 0 : Math.Max(-1, Math.Min(1, samples[i]));
                int pcm = (int)(value * 32767), sign = pcm < 0 ? 128 : 0;
                pcm = Math.Min(32635, Math.Abs(pcm)) + 132;
                int exponent = 7;
                for (int mask = 0x4000; exponent > 0 && (pcm & mask) == 0; mask >>= 1) exponent--;
                bytes[i] = (byte)~(sign | (exponent << 4) | ((pcm >> (exponent + 3)) & 15));
            }
            return bytes;
        }
        public static float[] Decode(byte[] bytes)
        {
            if (bytes == null || bytes.Length != FrameSamples) throw new ArgumentException("Invalid voice frame.");
            var samples = new float[FrameSamples];
            for (int i = 0; i < samples.Length; i++)
            {
                int code = (~bytes[i]) & 255;
                int pcm = ((((code & 15) << 3) + 132) << ((code >> 4) & 7)) - 132;
                samples[i] = ((code & 128) != 0 ? -pcm : pcm) / 32768f;
            }
            return samples;
        }
    }
}
