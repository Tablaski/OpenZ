namespace OpenZ
{
    /// <summary>1.2 item 154: a vehicle's or cannon's wreck (its hull and a landed turret) asks View/WreckSinkGate.cs before it starts to sink: on
    /// camera it waits (inside the cap), off camera it sinks as before. Its pieces (WreckPieces) share its unit id: one wreck.</summary>
    public partial class UnitView
    {
        WreckSinkGate.Ticket wreckSinkTicket;

        /// <summary>UpdateWreck, at its sink time: true = wait this frame.</summary>
        bool WreckSinkWaits() => WreckSinkGate.Hold(ref wreckSinkTicket, Obj != null ? Obj.Id : GetInstanceID(), gameObject, transform.position);
    }
}
