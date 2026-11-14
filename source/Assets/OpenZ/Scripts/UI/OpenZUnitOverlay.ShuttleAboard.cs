using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace OpenZ
{
    /// <summary>
    /// who is aboard rides on the ship's own name chip, the one label over
    /// the health bar every unit has (Draw): "SHUTTLE ★ | (face) ×3", the same chip, font, height and gap to the bar as every other unit's.
    ///  * The tail: a thin divider, the round face of who is aboard (<see cref="OzRoundFace"/>: the passengers' type in the team's colours,
    ///    a hero his own; Brad and Alan both, the second overlapping the first) and the count in the chip's own font; who is aboard = who gets
    ///    out on EXIT (<see cref="OzShuttleHud.Who"/>: the passengers, a hero pilot; a pilot who stays in is never counted).
    ///  * When: with the pointer on the ship (the name chip's own rule) and, with someone aboard, while it is selected too. Nobody aboard:
    ///    the plain name chip on mouse-over only, as any unit. Nothing else is drawn round the bar (round 2's tag under it is gone).
    ///  * The chip grows to fit its words and faces (measured at their own size, never cut). Laid out again only when who is aboard
    ///    changes: no new object or string per frame. View only.
    /// </summary>
    public partial class OpenZUnitOverlay
    {
        /// <summary>The tail's divider gap, face size, the second face's step and the gap before the count (canvas units, like the chip).</summary>
        public static float AboardDivGap = 6f, AboardFace = 14f, AboardFaceStep = 9f, AboardCountGap = 4f;
        /// <summary>Proof: this frame's name chips with a tail, by ship id: the count text and the faces ("grunt", "hero:brad+hero:alan").</summary>
        public static readonly Dictionary<int, string> AboardTexts = new Dictionary<int, string>(), AboardFaces = new Dictionary<int, string>();
        static readonly string[] times = new string[16];
        static string Times(int n) => n >= 0 && n < times.Length ? (times[n] ??= "×" + n) : "×" + n;

        /// <summary>LateUpdate, before the marks are drawn: the proof's lists start empty.</summary>
        void AboardBegin() { if (AboardTexts.Count > 0) AboardTexts.Clear(); if (AboardFaces.Count > 0) AboardFaces.Clear(); }

        /// <summary>Draw: does this unit's name chip show, and does it carry who is aboard (a shuttle with someone aboard, pointed at or
        /// selected)?</summary>
        static bool AboardChip(ZObject o, int brackets, bool hovered) => o.IsShuttle && (hovered || brackets == 1) && OzShuttleHud.Who(o).Count > 0;

        /// <summary>Draw, laying out the name chip: the tail after the name part (x = where the name and its star end, w = the chip's width
        /// without a tail); returns the chip's width.</summary>
        float AboardTail(Mark m, ZObject o, bool on, float x, float w)
        {
            if (!on) { if (m.tail && m.tail.gameObject.activeSelf) m.tail.gameObject.SetActive(false); return w; }
            if (!m.tail)
            {   // built for the first shuttle this mark shows (the pool's marks serve every unit)
                m.tail = Hud.Node("Aboard", m.chip); m.tail.anchorMin = m.tail.anchorMax = new Vector2(0, 1); m.tail.pivot = new Vector2(0, 1); m.tail.sizeDelta = new Vector2(10, OzLayout.HoverChipH);
                m.tailDiv = Hud.Img(m.tail, "Divider", OzUi.A(OzUi.Ink, 0.28f));
                m.tailFace1 = new OzRoundFace(m.tail, "Face1", AboardFace);                                     // the second first: the leader lies over it
                m.tailFace0 = new OzRoundFace(m.tail, "Face0", AboardFace);
                m.tailCount = Hud.Label(m.tail, "Count", "", OzLayout.HoverChipSize, OzUi.Ink, 0.12f, OzStyle.Plain);
            }
            if (!m.tail.gameObject.activeSelf) m.tail.gameObject.SetActive(true);
            var who = OzShuttleHud.Who(o);
            if (m.tailShip != o.Id || !who.Same(m.tailWho))
            {
                m.tailShip = o.Id; m.tailWho = who;
                int f = 0;
                if (who.Hero != null && who.Hero.Members != null)                                             // the heroes first (Brad, Alan), then the passengers' type
                    for (int i = 0; i < who.Hero.Members.Length && f < 2; i++)
                        if ((who.HeroMask & (1 << i)) != 0) (f++ == 0 ? m.tailFace0 : m.tailFace1).Hero(who.Hero, i, who.Team);
                if (who.Plain > 0 && f < 2) (f++ == 0 ? m.tailFace0 : m.tailFace1).Robot(who.PlainType, who.Team);
                if (f < 2) m.tailFace1.Off();
                if (f < 1) m.tailFace0.Off();
                m.tailFaces = f;
                m.tailText = Times(who.Count); m.tailCount.Set(m.tailText);
                m.tailCountW = Mathf.Ceil(m.tailCount.WidthAt(m.tailText));
                m.tailShows = f == 0 ? "" : f == 1 ? m.tailFace0.Shows : m.tailFace0.Shows + "+" + m.tailFace1.Shows;
            }
            // the tail in chip units, its own origin at the name part's end
            float H = OzLayout.HoverChipH, fx = 2f * AboardDivGap, faces = m.tailFaces == 0 ? 0f : AboardFace + (m.tailFaces - 1) * AboardFaceStep;
            m.tail.anchoredPosition = new Vector2(x, 0f);
            m.tailDiv.rectTransform.Place(new OzRect(AboardDivGap, (H - 10f) * 0.5f, 1f, 10f));
            m.tailFace0.At(fx, (H - AboardFace) * 0.5f);
            if (m.tailFaces > 1) m.tailFace1.At(fx + AboardFaceStep, (H - AboardFace) * 0.5f);
            float cx = fx + faces + (m.tailFaces > 0 ? AboardCountGap : 0f);
            m.tailCount.At(cx, H * 0.5f, OzH.L, true, m.tailCountW + 4f);
            float tailW = cx + m.tailCountW;
            m.tail.sizeDelta = new Vector2(tailW, H);
            AboardTexts[o.Id] = m.tailText; AboardFaces[o.Id] = m.tailShows;
            return x + tailW + 7f;                                                                              // the chip's own right padding
        }
    }
}
