namespace OpenZ
{
    /// <summary>1.2 item 154: the pieces of a broken-up fort gun ask View/WreckSinkGate.cs before they sink at the end of the wreck time (one wreck
    /// per break-up): on camera they wait (inside the cap), off camera they sink as before.</summary>
    public partial class OzFortGunBreakup
    {
        WreckSinkGate.Ticket k93Sink;

        /// <summary>Update, at the wreck time: true = wait this frame.</summary>
        bool SinkWaits() => WreckSinkGate.Hold(ref k93Sink, grp != null ? -1 - (System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(grp) & 0x3FFFFFFF) : -1, gameObject, transform.position);
    }
}
