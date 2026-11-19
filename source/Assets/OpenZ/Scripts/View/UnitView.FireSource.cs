// OpenZ (GPL-3.0)
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// The wreck's own fires tied to what is drawn of the hull (View/OzFireSource.cs). UnitView.Deaths.cs calls these from the wreck's burn
    /// beat (UpdateWreck) and from its flame tongues (UpdateWreckFlames); with OzFireSource.On off both do nothing.
    /// </summary>
    public partial class UnitView
    {
        float n33Spread = 0.7f, n33T0 = float.NaN; int n33Kept; Vector3[] n33Keep;

        /// <summary>One burn beat: the smoke / light point and the fire's foot on the hull as it is drawn NOW (dented, turned, shoved on, without
        /// its turret). False: nothing of the hull is drawn: no fire, no smoke, its sticky fire and its crackle end.</summary>
        bool N33Wreck(ref Vector3 at, ref Vector3 fireAt)
        {
            n33Spread = 0.7f;
            if (OzFireSource.WreckBurn(this, flipWreck, ref at, ref fireAt, ref n33Spread)) return true;
            if (wreckPyro && Obj != null) { OpenZPyroFire.StopBurning(Obj.Id); wreckPyro = false; }
            StopWreckFire(0.5f);
            return false;
        }

        /// <summary>The fire puffs' sideways spread: never wider than the hull under them.</summary>
        float N33Spread(float normal) => OzFireSource.On ? Mathf.Min(normal, n33Spread) : normal;

        /// <summary>One flame spot, every frame: the deck spot on the top of what is drawn under it, a ground spot kept in the hull's frame
        /// (it goes with the hull) right beside its nearest drawn part. False: no flame there now.</summary>
        bool N33Tongue(int i, bool onHull, ref Vector3 root)
        {
            if (n33Keep == null) n33Keep = new Vector3[4];
            if (n33T0 != wreckFlameT0) { n33T0 = wreckFlameT0; n33Kept = 0; }                             // the spots were picked anew
            if (i < 0 || i >= n33Keep.Length) return true;
            bool fresh = (n33Kept & (1 << i)) == 0; n33Kept |= 1 << i;
            return OzFireSource.Tongue(this, onHull, fresh, ref n33Keep[i], ref root);
        }
    }
}
