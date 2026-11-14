// OpenZ (GPL-3.0) - version 1.2, owner item 49: the frame rate bench looks at its own picture (View/OzFpsBench.cs, View/OzWorldLight.cs).
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// Shaders/FlybySky.hlsl), and the bench starts its level without one, so its runs could be timed on a black world with nothing in
    /// the result to say so. Now the bench reads the world's light once per view (<see cref="OzWorldLight.Request"/>: the game camera into
    /// a 96 x 54 picture, read back a few frames later without a wait) after the view's settle time and before its measure, so the
    /// measured frames never carry it. A view whose world is black (mean under <see cref="OzWorldLight.BlackBelow"/> of 255) or whose
    /// light could not be read makes the run NOT VALID ("the world drew black in view(s) ..."). The VIEW lines, the result line's views
    /// and the result file's DATA (worldLight, worldNan) carry the light.
    /// </summary>
    public sealed partial class OzFpsBench
    {
        /// <summary>The longest wait (real seconds, never measured) for a view's light reading.</summary>
        public static float LightWaitS = 2f;
        readonly Dictionary<string, OzWorldLight.Reading> lightSeen = new Dictionary<string, OzWorldLight.Reading>();

        /// <summary>Run(): after a view's settle time, before its measure (LateUpdate holds the camera on the view meanwhile).</summary>
        IEnumerator LightLook(View v)
        {
            phase = "light " + v.name;
            bool got = false; var r = default(OzWorldLight.Reading);
            var cam = Camera.main; if (!cam && rts) cam = rts.GetComponent<Camera>();
            OzWorldLight.Request(cam, x => { r = x; got = true; });
            float t = Time.realtimeSinceStartup;
            while (!got && Time.realtimeSinceStartup - t < LightWaitS) { if (done) yield break; yield return null; }
            if (!got) r = new OzWorldLight.Reading { why = "no answer in " + LightWaitS.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " s" };
            lightSeen[v.name] = r;
            Debug.Log($"{Tag} light {v.name}: {r}{(r.Black ? ": THE WORLD DREW BLACK" : "")}");
        }

        /// <summary>The view's light into its data (DATA line).</summary>
        void LightInto(ViewData d)
        {
            if (d == null) return;
            if (lightSeen.TryGetValue(d.name, out var r) && r.ok) { d.worldLight = r.mean; d.worldNan = r.nan; }
        }

        /// <summary>The VIEW line's light field.</summary>
        string LightText(ViewData d)
        {
            if (d == null || !lightSeen.TryGetValue(d.name, out var r)) return "world light not read";
            return r.ok ? "world light " + r.mean.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " of 255" + (r.nan > 0 ? " (" + r.nan + " NaN px)" : "")
                        : "world light not read (" + r.why + ")";
        }

        /// <summary>Flags(): a black view (or one with no reading) is flagged in its VIEW line.</summary>
        void LightFlag(ViewData d, List<string> f)
        {
            if (d == null) return;
            if (!lightSeen.TryGetValue(d.name, out var r) || !r.ok) f.Add("WORLD LIGHT NOT READ");
            else if (r.Black) f.Add("WORLD BLACK (light " + r.mean.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " of 255)");
        }

        /// <summary>Finish(): the verdict's reasons for the light; the totals get the darkest view's light and every view's NaN pixels.</summary>
        void LightVerdict(List<string> why, ViewData all)
        {
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            var black = new List<string>(); var unread = new List<string>();
            float darkest = float.MaxValue; int nans = 0;
            foreach (var v in views)
            {
                if (v.d == null) continue;
                if (!lightSeen.TryGetValue(v.name, out var r) || !r.ok) { unread.Add(v.name); continue; }
                darkest = Mathf.Min(darkest, r.mean); nans += r.nan;
                if (r.Black) black.Add(v.name + " " + r.mean.ToString("0.0", ci));
            }
            if (all != null) { all.worldLight = darkest < float.MaxValue ? darkest : -1f; all.worldNan = nans; }
            if (black.Count > 0) why.Add("the world drew black in view(s) " + string.Join(", ", black) + " (mean light under " + OzWorldLight.BlackBelow.ToString("0", ci) + " of 255: a broken picture, its numbers are not the game's)");
            if (unread.Count > 0) why.Add("the world's light could not be read in view(s) " + string.Join(", ", unread));
        }
    }
}
