// OpenZ (GPL-3.0) - the painted sky on a hard cut: an ending scene that starts on the plain sky would fade its clouds in over a second.
using UnityEngine;

namespace OpenZ
{
    public sealed partial class FlybySky
    {
        /// <summary>The panorama finished now and the sky full at once, as a flight's first frame (FlybySky.Preload.cs). The caller holds the sky on with ForceOn from this frame.</summary>
        public void CutFull()
        {
            EnsureLoaded();
            if (!tex && req != null)
            {
                var a = req.asset as Texture2D;                                                            // read before isDone: the load is finished now, on this frame
                if (req != null && req.isDone) PollLoad();
                if (!tex && a) { tex = a; req = null; }
            }
            if (tex && Mat()) Weight = 1f;
        }
    }
}
