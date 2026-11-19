using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.5 ue_structure_destruction: a flag on a ramp platform that collapses comes DOWN with the deck instead of appearing on the floor in the
    /// same frame. PlatformGone (FlagView.Deck.cs) still rebuilds it on the floor (pole, cloth, ring, looks, wave: the rules' place is the
    /// floor at once); here its drawn root starts where it stood on the deck, holds while the deck gives (<see cref="DeckFallDelay"/>), falls
    /// under gravity, lands on the floor with a small bounce, a dust puff and a metal knock, upright. The ring lies on the floor from the start
    /// (where the flag will stand). Every level: nothing (no platform there).
    /// </summary>
    public partial class FlagView
    {
        public static float DeckFallDelay = 0.2f;
        public static int DeckFalls, DeckLandings;

        /// <summary>OzUePlatformViews.Died, before PlatformGone: the drawn foot height of every flag standing on the platform (flag id -> y).</summary>
        internal static Dictionary<int, float> DeckHeights(ZObject platform)
        {
            var d = new Dictionary<int, float>();
            var v = I; if (!v || platform == null) return d;
            foreach (var f in v.flags)
                if (!f.fort && f.obj != null && !f.obj.Destroyed && f.root && platform.Contains(f.obj.CenterX, f.obj.CenterY)) d[f.obj.Id] = f.root.position.y;
            return d;
        }

        /// <summary>After PlatformGone: each rebuilt flag falls from its old height to where it now stands.</summary>
        internal static void DropFrom(Dictionary<int, float> heights)
        {
            var v = I; if (!v || heights == null || heights.Count == 0) return;
            foreach (var f in v.flags)
            {
                if (f.obj == null || !f.root || !heights.TryGetValue(f.obj.Id, out float y0)) continue;
                float y1 = f.root.position.y;
                if (y0 - y1 < 0.3f || !OzUeStructureBreak.On) continue;
                var fall = f.root.gameObject.AddComponent<OzUeFlagFall>();
                fall.Begin(y0, y1);
                DeckFalls++;
            }
        }
    }

    /// <summary>The fall of one flag's drawn root (FlagView.DropFrom).</summary>
    public sealed class OzUeFlagFall : MonoBehaviour
    {
        float from, to, t, vy, wait, bounceT = -1f; bool landed;

        internal void Begin(float y0, float y1)
        {
            from = y0; to = y1; wait = FlagView.DeckFallDelay; vy = 0f;
            var p = transform.position; p.y = from; transform.position = p;
        }

        void Update()
        {
            float dt = Time.deltaTime; if (dt <= 0f) return;
            var p = transform.position;
            if (wait > 0f) { wait -= dt; return; }
            if (!landed)
            {
                vy -= 9.81f * dt; p.y += vy * dt;
                if (p.y <= to)
                {
                    p.y = to; landed = true; bounceT = 0f; FlagView.DeckLandings++;
                    OzUeStructureBreak.DustAt(new Vector3(p.x, to + 0.05f, p.z), 1.2f, 2);
                    OzUeStructureBreak.Sound("dst_debris_metal", p + Vector3.up * 0.5f, 0.35f, 1.25f);
                }
                transform.position = p;
                return;
            }
            bounceT += dt;
            const float B = 0.32f;
            float k = Mathf.Clamp01(bounceT / B);
            p.y = to + Mathf.Sin(k * Mathf.PI) * 0.18f * (1f - k);                                                 // one small bounce, then still
            transform.position = p;
            if (k >= 1f) { p.y = to; transform.position = p; Destroy(this); }
        }
    }
}
