// OpenZ (GPL-3.0) - version 1.2: the comms window's subtitle as it stands now (for the checks of the blue side's card lines).
namespace OpenZ
{
    public sealed partial class ZodComms
    {
        /// <summary>Checks: the words the window's subtitle place shows now ("" when the layout has none).</summary>
        public string SubtitleText => subBody != null ? subBody.text : "";
    }
}
