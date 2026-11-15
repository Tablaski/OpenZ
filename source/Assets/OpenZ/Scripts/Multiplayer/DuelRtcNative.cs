using System;
using System.Runtime.InteropServices;
using System.Text;

namespace OpenZ.Multiplayer
{
    // ABI pinned to libdatachannel v0.24.6. No native callbacks cross into Unity.
    internal static class DuelRtcNative
    {
        const string Library = "datachannel";
        [StructLayout(LayoutKind.Sequential)] internal struct Configuration
        {
            public IntPtr IceServers;
            public int IceServersCount;
            public IntPtr ProxyServer, BindAddress;
            public int CertificateType, TransportPolicy;
            [MarshalAs(UnmanagedType.I1)] public bool EnableIceTcp;
            [MarshalAs(UnmanagedType.I1)] public bool EnableIceUdpMux;
            [MarshalAs(UnmanagedType.I1)] public bool DisableAutoNegotiation;
            [MarshalAs(UnmanagedType.I1)] public bool ForceMediaTransport;
            public ushort PortRangeBegin, PortRangeEnd;
            public int Mtu, MaxMessageSize;
        }
        [StructLayout(LayoutKind.Sequential)] internal struct Reliability
        {
            [MarshalAs(UnmanagedType.I1)] public bool Unordered;
            [MarshalAs(UnmanagedType.I1)] public bool Unreliable;
            public uint MaxPacketLifeTime, MaxRetransmits;
        }
        [StructLayout(LayoutKind.Sequential)] internal struct ChannelInit
        {
            public Reliability Reliability;
            public IntPtr Protocol;
            [MarshalAs(UnmanagedType.I1)] public bool Negotiated;
            [MarshalAs(UnmanagedType.I1)] public bool ManualStream;
            public ushort Stream;
        }
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate void GatheringCallback(int pc, int state, IntPtr user);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int rtcCreatePeerConnection(ref Configuration configuration);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int rtcSetGatheringStateChangeCallback(int pc, GatheringCallback callback);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int rtcSetLocalDescription(int pc, string type);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int rtcSetRemoteDescription(int pc, string sdp, string type);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int rtcGetLocalDescription(int pc, StringBuilder sdp, int size);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int rtcCreateDataChannelEx(int pc, string label, ref ChannelInit init);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] [return: MarshalAs(UnmanagedType.I1)] internal static extern bool rtcIsOpen(int channel);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] [return: MarshalAs(UnmanagedType.I1)] internal static extern bool rtcIsClosed(int channel);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int rtcSendMessage(int channel, byte[] message, int size);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int rtcReceiveMessage(int channel, byte[] message, ref int size);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int rtcGetBufferedAmount(int channel);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int rtcGetSelectedCandidatePair(int pc, StringBuilder local, int localSize, StringBuilder remote, int remoteSize);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int rtcClosePeerConnection(int pc);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int rtcDeletePeerConnection(int pc);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int rtcDeleteDataChannel(int channel);
        internal static int Check(int result)
        {
            if (result < 0) throw new InvalidOperationException("WebRTC operation failed (" + result + ").");
            return result;
        }
    }
}
