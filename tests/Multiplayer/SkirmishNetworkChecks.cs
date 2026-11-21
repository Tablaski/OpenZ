using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OpenZ.Multiplayer;

static class SkirmishNetworkChecks
{
    internal static async Task Run(Action<bool, string> check)
    {
        var tuning = new SkirmishNetworkTuning();
        tuning.Update(0, 0, 0, 0, 28 * 1024, 7, true);
        check(tuning.Hz == 7, "SFU tuning must account for all replicated egress");
        tuning.Update(128 * 1024, 55, 70, 20, 0, 7, true);
        check(tuning.Hz == SkirmishNetworkTuning.MinHz, "Congestion lowers the rate within its bound");
        for (int i = 0; i < 3; i++) tuning.Update(0, 0, 0, 0, 0, 7, false);
        check(tuning.Hz == SkirmishNetworkTuning.MinHz, "Rate does not rise in a short clear window");
        tuning.Update(0, 0, 0, 0, 0, 7, false);
        check(tuning.Hz == SkirmishNetworkTuning.MinHz + 1, "Rate recovers after a sustained clear window");

        var payload = new byte[512 * 1024];
        var packet = new DuelPacket(DuelMessage.RoomBattlefield, payload, true);
        var packed = DuelWire.PackDelta(packet);
        check(packed.Kind == DuelMessage.RoomBattlefieldCompressed && packed.Payload.Length < 8192,
            "Full Skirmish state larger than a duel delta is compressed");
        check(ReferenceEquals(packed, DuelWire.PackDelta(packet)), "One packed frame is shared by all direct links");
        using var wire = new MemoryStream();
        await DuelWire.WriteAsync(wire, packed, CancellationToken.None);
        var bytes = wire.ToArray();
        var directFrame = DuelWire.FrameForDirectRoom(packet);
        check(directFrame.SequenceEqual(bytes) && ReferenceEquals(directFrame, DuelWire.FrameForDirectRoom(packet)),
            "Direct Skirmish links share exactly the standard WebRTC frame bytes");
        var decoder = new DuelRtcFrameDecoder();
        var result = Enumerable.Range(0, (bytes.Length + 8) / 9).SelectMany(index =>
        {
            int count = Math.Min(9, bytes.Length - index * 9);
            var fragment = new byte[count + sizeof(int)];
            Array.Copy(bytes, index * 9, fragment, sizeof(int), count);
            return decoder.Add(fragment, count, sizeof(int));
        }).Single();
        var received = DuelWire.UnpackDelta(result);
        check(received.Kind == DuelMessage.RoomBattlefield && received.Payload.SequenceEqual(payload),
            "Targeted SFU fragments reassemble and restore the exact battlefield payload");
    }
}
