using System;

namespace OpenZ.Multiplayer
{
    public interface IDuelPeer : IDisposable
    {
        string RemoteName { get; }
        string Failure { get; }
        bool Closed { get; }
        long StatePayloadBytesSent { get; }
        bool Send(DuelPacket packet);
        bool TryReceive(out DuelPacket packet);
    }
}
