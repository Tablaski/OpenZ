using System.Collections.Generic;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.2 item 6 (the unit editor, View/OzUePlace.cs): the drop preview's models as their team's see-through ghost (View/OzUeGhost.cs)
    /// and the pieces the drop's words need: the squad's range ring on the canvas and the words' widths (View/OzUeWords.cs places them).
    /// </summary>
    internal sealed partial class OzUePlaceDriver
    {
        /// <summary>Frames between two looks over a model's parts (a part made or re-painted later becomes ghost too).</summary>
        const int GhostRecheckFrames = 30;
        /// <summary>Points of the range ring's polyline on the canvas (the words keep clear of it).</summary>
        const int RingSamples = 96;
        readonly float[] ringX = new float[RingSamples], ringY = new float[RingSamples];
        static readonly List<Renderer> ghostBuf = new List<Renderer>(64);
        OzUeMeasure wordsMeasure;

        /// <summary>Checks (the Play proof): the last frame's words: the rule OzUeWords took (1 .. 4, 0 = none was clear), clear of the
        /// range ring and its label, the preview's centre and its range ring (m), the words' centre x and half width (canvas u).</summary>
        internal static int WordsChoice;
        internal static bool WordsClearRing = true;
        internal static float WordsRangeM, WordsX, WordsHalfW;
        internal static Vector3 WordsCentre;

        /// <summary>The model as its team's ghost: every part that takes it (looked over again every GhostRecheckFrames frames); the
        /// game's screen-door cut instead when the ghost shader is missing.</summary>
        void Ghost(Model m, Team team, float alpha)
        {
            if (!OzUeGhost.Ready)
            {
                for (int i = 0; i < m.rs.Length; i++) if (m.rs[i]) OzShuttleCamFade.WriteFade(m.rs[i], 1f - alpha, m.mats[i]);   // a repaint keeps it (TeamPaint reads the blocks back)
                return;
            }
            if (m.seen != null && Time.frameCount < m.checkAt) return;
            m.checkAt = Time.frameCount + GhostRecheckFrames;
            if (m.seen == null) { m.seen = new HashSet<Renderer>(); m.ghosts = new HashSet<Renderer>(m.rs); }
            m.go.GetComponentsInChildren(true, ghostBuf);
            for (int i = 0; i < ghostBuf.Count; i++)
            {
                var r = ghostBuf[i]; if (!r) continue;
                if (m.seen.Add(r) && !m.ghosts.Contains(r))
                {
                    if (OzUeGhost.Takes(r)) m.ghosts.Add(r);
                    else { if (r.enabled) { r.enabled = false; OzUeGhost.LeftOut++; } continue; }               // a part made after the model (a glow, a cable of another paint)
                }
                if (m.ghosts.Contains(r)) OzUeGhost.Apply(r, team, alpha);
            }
            ghostBuf.Clear();
        }

        /// <summary>The squad's range ring on the canvas (rangeM round the centre on the floor), the points in front of the camera.</summary>
        int RangePolyline(Camera cam, OpenZHud hud, Vector3 centre, float rangeM)
        {
            if (!(rangeM > 0.1f) || !cam || !hud) return 0;
            int n = 0;
            for (int k = 0; k < RingSamples; k++)
            {
                float a = k * (2f * Mathf.PI / RingSamples);
                var s = cam.WorldToScreenPoint(new Vector3(centre.x + Mathf.Cos(a) * rangeM, centre.y, centre.z + Mathf.Sin(a) * rangeM));
                if (s.z <= cam.nearClipPlane) continue;
                var c = hud.ScreenToCanvas(s);
                ringX[n] = c.x; ringY[n] = c.y; n++;
            }
            return n;
        }

        /// <summary>A world label's text width at 12 u (the game font, measured once per text).</summary>
        float WordsWidth(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0f;
            if (wordsMeasure == null)
            {
                var layer = OzUnitEditor.Layer(UeLayer.Field);
                if (!layer) return text.Length * 7f;
                wordsMeasure = new OzUeMeasure(layer);
            }
            return wordsMeasure.Width(text, 12f, 0f);
        }

        /// <summary>Checks (the Play proof): the models of the drop preview shown in the last frame, with their unit and team.</summary>
        internal void Shown(List<(GameObject go, string id, Team team)> into)
        {
            into.Clear();
            foreach (var kv in models)
            {
                int bar = kv.Key.LastIndexOf('|'); if (bar <= 0) continue;
                string id = kv.Key.Substring(0, bar);
                if (!int.TryParse(kv.Key.Substring(bar + 1), out int t)) continue;
                modelsUsed.TryGetValue(kv.Key, out int n);
                for (int i = 0; i < n && i < kv.Value.Count; i++)
                {
                    var m = kv.Value[i];
                    if (m != null && m.go && m.go.activeInHierarchy) into.Add((m.go, id, (Team)t));
                }
            }
        }

        /// <summary>The opacity of a preview model of this team (an empty vehicle or gun: NeutralAlpha).</summary>
        internal static float GhostAlpha(Team team) => team == Team.Neutral ? OzUeGhost.NeutralAlpha : OzUeGhost.UnitAlpha;
    }
}
