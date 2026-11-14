// OpenZ (GPL-3.0) - version 1.2, owner item 160: the comms window closed at once, without its fold (ZodBriefing.LoadingCut.cs).
namespace OpenZ
{
    public sealed partial class ZodComms
    {
        /// <summary>Closes the window in this frame: no signal burst, no CRT-off fold; nothing of it is drawn from this frame on (used when
        /// the menu's loading screen comes up). The next <see cref="Open"/> builds the window as always.</summary>
        public void CloseNow()
        {
            if (state == State.Closed) return;
            state = State.Closed; stateAt = OzMotion.Now;
            burstK = 0f; burstFrom = burstUntil = -10f; Burst = 0f; blueOn = false;
            if (win) win.gameObject.SetActive(false);
        }
    }
}
