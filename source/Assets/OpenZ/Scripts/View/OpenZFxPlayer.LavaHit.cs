// OpenZ (GPL-3.0) - version 1.3, level 7 (LV_CONTRACT.md, kit LV3): what lands in lava, and the lava answers of the water calls.
using UnityEngine;

namespace OpenZ
{
    public partial class OpenZFxPlayer
    {
        /// <summary>A blast this strong (M12 strength) makes the big pop.</summary>
        public const float LavaBigBlast = 1f;

        /// <summary>Anything heavy landing in lava: orange splash, smoke, rings, the crust opens, a pop with its sound. No dust, no crater, no
        /// scorch. big = the big pop. LV4's bombs use this too. at = any point over the lava.</summary>
        public void LavaHit(Vector3 at, float s, bool big)
        {
            if (!enabled) return;
            if (OzLava.SurfaceAt(at, out float y)) at.y = y;
            LavaSpray(at, s, big);
            var lava = OzLava.I; if (lava) lava.PopAt(at, big);
        }

        /// <summary>The same with no pop and no sound (OzWaterLanding plays its own, spaced, sound).</summary>
        public void LavaSpray(Vector3 at, float s, bool big)
        {
            s = Mathf.Clamp(s, 0.15f, 3f);
            LavaDroplets(at, s, Mathf.Clamp(Mathf.RoundToInt(4f + 7f * s), 3, 20));
            LavaSmoke(at + Vector3.up * 0.2f, 0.8f * s);
            LavaSteam(at, 0.6f * s);
            LavaEmber(at, Mathf.Clamp(Mathf.RoundToInt(2f * s), 1, 5));
            LavaRing(at, 1f + 1.2f * s, 0.8f + 0.3f * s);
            if (big)
            {
                LavaSmoke(at + Vector3.up * 0.8f, 1.1f * s);
                LavaRing(at, 2f + 1.6f * s, 1.7f);
            }
            var wade = OzLavaWade.I;
            if (wade && wade.Wake != null) wade.Wake.Stamp(at, 1f + s, 1f);
        }

        /// <summary>BlastInternal on lava. The shell sound on map tiles is AudioDirector's (sound kit); it does not know the border sheets.</summary>
        void LavaBlast(Vector3 flat, float s)
        {
            if (QuietBlast) { LavaSpray(flat, s, s >= LavaBigBlast); return; }                       // a cosmetic ripple blast stays quiet: no pop, no sound
            LavaHit(flat, s, s >= LavaBigBlast);
            var g = ZGame.I; if (!g || g.World == null) return;
            var px = ZGame.WorldToPx(flat);
            if (!g.World.Map.LavaAt(px.x >> 4, px.y >> 4)) AudioDirector.Fire("lava_hit_shell", flat, -1);
        }

        /// <summary>Splash on lava: true = done.</summary>
        bool LavaSplash(Vector3 at, float s)
        {
            if (!OzLava.SurfaceAt(at, out float y)) return false;
            at.y = y; LavaDroplets(at, s, Mathf.Clamp(Mathf.RoundToInt(6f * s), 3, 12)); LavaRing(at, 1.2f * s, 0.9f);
            return true;
        }

        /// <summary>Droplets on lava: true = done.</summary>
        bool LavaDrops(Vector3 at, float s, int count)
        {
            if (!OzLava.SurfaceAt(at, out float y)) return false;
            at.y = y; LavaDroplets(at, s, count);
            return true;
        }

        /// <summary>A ring on lava: true = done. A water ring of strength s ends 4.65 s m across.</summary>
        bool LavaRipple(Vector3 at, float s, float life)
        {
            if (!OzLava.SurfaceAt(at, out float y)) return false;
            at.y = y; LavaRing(at, 2.3f * s, life);
            return true;
        }
    }
}
