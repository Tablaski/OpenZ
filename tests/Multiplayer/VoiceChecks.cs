using System;
using OpenZ.Multiplayer;

static class VoiceChecks
{
    public static void Run(Action<bool, string> check, Action<Action, string> reject)
    {
        var frame = new float[DuelVoiceCodec.FrameSamples];
        for (int i = 0; i < frame.Length; i++) frame[i] = .5f * (float)Math.Sin(2 * Math.PI * 440 * i / DuelVoiceCodec.SampleRate);
        byte[] packet = DuelVoiceCodec.Encode(frame);
        float[] decoded = DuelVoiceCodec.Decode(packet);
        double error = 0;
        for (int i = 0; i < frame.Length; i++) error += Math.Pow(decoded[i] - frame[i], 2);
        check(packet.Length == 320 && error / frame.Length < .0002, "Voice codec has excessive distortion");
        check(DuelVoiceCodec.Decode(DuelVoiceCodec.Encode(new float[320]))[0] == 0, "Voice silence produces noise");
        frame[0] = float.NaN; frame[1] = float.PositiveInfinity; frame[2] = float.NegativeInfinity;
        decoded = DuelVoiceCodec.Decode(DuelVoiceCodec.Encode(frame));
        check(decoded[0] == 0 && decoded[1] > .9 && decoded[2] < -.9, "Voice codec does not clamp invalid samples");
        reject(() => DuelVoiceCodec.Decode(new byte[100]), "Truncated voice frame accepted");
    }
}
