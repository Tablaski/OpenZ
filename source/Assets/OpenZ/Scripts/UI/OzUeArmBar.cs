using OpenZ.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace OpenZ
{
    /// <summary>
    /// 1.2 item 6 (the unit editor, View/OzUnitEditor.cs): the arm bar, DESIGN_SPEC E183 (prototype C round 2d, the armed state):
    /// top centre at top bar + 66 u, 40 u tall, GM900 94 % with a 1 u AMBER_LO edge, padding 0 6 0 14, gap 10: a 10 u team dot, the
    /// name (13 u caps 0.10 INK; EMPTY ... for a neutral vehicle or gun), the team (12 u caps 0.12 in the team's text colour, "x3"
    /// when 3 drop at once), the hint (13 u INK_2) and DONE. 620 x 40 at 650,106 for a red sniper at 1920 (measured). It slides 10 u
    /// down in 180 ms; H hides it with the other panels (placing stays armed).
    /// </summary>
    internal sealed class OzUeArmBar
    {
        internal const float H = 40f, PadL = 14f, PadR = 6f, GapX = 10f, Dot = 10f;
        internal const string Hint = "Click the field to place. Shift-click keeps placing. Right-click or Esc stops.";
        internal const string DoneTip = "DONE|Stop placing (Esc or right-click)|ESC";
        readonly RectTransform root; readonly CanvasGroup cg;
        readonly Image dot; readonly OzText name, team, hint;
        internal readonly HudButton done;
        float shownAt = -10f; bool on; string shownId; Team shownTeam; int shownCount = -1;
        internal bool Visible => on && root.gameObject.activeSelf;
        internal RectTransform Root => root;

        internal OzUeArmBar(RectTransform parent, OpenZHud hud)
        {
            root = Hud.Node("OzUe_ArmBar", parent);
            root.Place(new OzRect(0, 0, 600, H));
            cg = root.gameObject.AddComponent<CanvasGroup>();
            OzUeShell.EdgePlate(root, OzUi.AmberLo, OzUi.A(OzUi.GM900, 0.94f), true);
            dot = Hud.Kit(root, "Dot", "Bits/dot", new OzRect(PadL, (H - Dot) * 0.5f, Dot, Dot), OzUi.RedHi, Image.Type.Simple);
            name = Hud.Label(root, "Name", "", 13, OzUi.Ink, 0.10f);
            team = Hud.Label(root, "Team", "", 12, OzUi.RedText, 0.12f);
            hint = Hud.Label(root, "Hint", Hint, 13, OzUi.Ink2, 0f, OzStyle.Plain);
            done = OzUeShell.Button(root, "Done", "DONE", 0, (H - 28f) * 0.5f, 28f, HudButtonStyle.Normal, DoneTip, () => OzUePlace.Disarm(true));
            if (hud) hud.AddBlocker(root);
            root.gameObject.SetActive(false);
        }

        /// <summary>The words of the armed drop (OzUePlace.Changed / OzUeControls.Changed).</summary>
        internal void Refresh()
        {
            bool want = OzUePlace.Armed && !OzUnitEditor.PanelsHidden;
            if (!want) { if (on) { on = false; root.gameObject.SetActive(false); } return; }
            string id = OzUePlace.Id; Team t = OzUePlace.Team; int n = OzUePlace.CountFor(id);
            if (!on) { on = true; shownAt = OzMotion.Now; root.gameObject.SetActive(true); }
            if (id == shownId && t == shownTeam && n == shownCount) return;
            shownId = id; shownTeam = t; shownCount = n;
            var kind = OzUePlayground.KindOf(id);
            string nm = kind == UeKind.Flag ? "NEUTRAL FLAG" : kind == UeKind.Obstacle ? "HOLO OBSTACLE" : kind == UeKind.Grenades ? "GRENADE CRATE" : kind == UeKind.Nuke ? "NUKE STRIKE" : kind == UeKind.Platform ? OzUePlayground.FullName(id)
                      : (t == Team.Neutral ? "EMPTY " : "") + OzUePlayground.FullName(id);
            string tw = OzUeUi.TeamWord(t) + (n > 1 && kind != UeKind.Flag && kind != UeKind.Obstacle ? " x" + n : "");
            name.Set(nm.ToUpperInvariant()); team.Set(tw);
            team.color = t == Team.Neutral ? (Color)OzUi.Khaki : OzUeUi.TeamText(t);                     // the prototype's neutral word is KHAKI
            dot.color = t == Team.Neutral ? (Color)OzUi.Khaki : OzUeUi.TeamColor(t);
            Arrange();
        }

        void Arrange()
        {
            float x = PadL + Dot + GapX;
            float nw = name.WidthAt(name.text), tw = team.WidthAt(team.text), hw = hint.WidthAt(Hint);
            name.At(x, H * 0.5f, OzH.L, true, nw + 4f); x += nw + GapX;
            team.At(x, H * 0.5f, OzH.L, true, tw + 4f); x += tw + GapX;
            hint.At(x, H * 0.5f, OzH.L, true, hw + 4f); x += hw + GapX;
            done.Rt.Place(new OzRect(x, (H - 28f) * 0.5f, done.Rt.sizeDelta.x, 28f));
            x += done.Rt.sizeDelta.x + PadR;
            Width = Mathf.Ceil(x);
        }

        internal float Width { get; private set; } = 600f;

        /// <summary>Per frame: its place on the canvas and the 180 ms slide.</summary>
        internal void Tick(float topBar, Vector2 canvas)
        {
            if (OzUePlace.Armed != on || (on && OzUnitEditor.PanelsHidden)) Refresh();
            if (!on) return;
            float k = OzEase.OutCubic(OzMotion.T(shownAt, OzMotion.Dur(0.18f)));
            cg.alpha = Mathf.Clamp01(OzMotion.T(shownAt, OzMotion.Dur(0.18f, true)));
            float w = Mathf.Min(Width, canvas.x - 24f);
            root.Place(new OzRect(Mathf.Round((canvas.x - w) * 0.5f), Mathf.Round(topBar + 66f - 10f * (1f - k)), w, H));
            OzUeShell.HoverInk(done);
        }

        /// <summary>The bar's rect on a canvas (SelfCheck: inside, centred, under the top bar).</summary>
        internal static Rect Plan(Vector2 canvas, float topBar, float width) { float w = Mathf.Min(width, canvas.x - 24f); return new Rect((canvas.x - w) * 0.5f, topBar + 66f, w, H); }
    }
}
