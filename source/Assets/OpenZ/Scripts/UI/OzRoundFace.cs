using OpenZ.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace OpenZ
{
    /// <summary>
    /// unit face in the HUD's own style, for the shuttle's who-is-aboard (the card's type chip, its PILOT slot, the name chip over the ship).
    ///  * a robot type: the production badge's own round face (OzBadgeArt face_&lt;type&gt;_&lt;team&gt;: the BS-01 portrait cut round, team
    ///    colours, crisp size tiers), no square, no box;
    ///  * a hero member (or the pair): his own portrait (View/OzHeroSquadView.MemberPortrait) cut round by the same disc (a Mask on a badge
    ///    face's shape), so both read as the same kind of face.
    /// Set only on a change (the caller keeps who it shows); the size tier follows the screen (OzPx.K) when set.
    /// </summary>
    public sealed class OzRoundFace
    {
        public readonly RectTransform Rt;
        readonly Image disc; readonly Mask mask; readonly RawImage photo; readonly float size;
        /// <summary>Proof: what the face shows ("grunt", "hero:brad", "" when off).</summary>
        public string Shows { get; private set; } = "";

        public OzRoundFace(Transform parent, string name, float size)
        {
            this.size = size;
            Rt = Hud.Node(name, parent); Rt.sizeDelta = new Vector2(size, size);
            disc = Hud.Img(Rt, "Face", Color.white); disc.rectTransform.Fill(); disc.preserveAspect = true;
            mask = disc.gameObject.AddComponent<Mask>(); mask.showMaskGraphic = true; mask.enabled = false;
            var pr = Hud.Node("Photo", disc.transform); pr.Fill();
            photo = pr.gameObject.AddComponent<RawImage>(); photo.raycastTarget = false; photo.enabled = false;
            Rt.gameObject.SetActive(false);
        }

        /// <summary>The badge face's size tier for this face's size on the screen now (the art is TagFaceBox u at tier 1).</summary>
        int Tier => Mathf.Clamp(Mathf.CeilToInt(size * OzPx.K / OzLayout.TagFaceBox - 0.05f), 1, OzBadgeArt.MaxTier);

        /// <summary>A robot type's own round face in the team's colours.</summary>
        public void Robot(string type, Team team)
        {
            var s = OzBadgeArt.Get(Tier, OzBadgeArt.FaceFor(type, team));
            if (mask.enabled) { mask.enabled = false; mask.showMaskGraphic = true; }
            photo.enabled = false;
            if (disc.sprite != s) disc.sprite = s;
            disc.enabled = s != null; Shows = type ?? "";
            Rt.gameObject.SetActiveFast(true);
        }

        /// <summary>A hero member's own portrait (member -1 = the pair's), cut round by a badge face's disc.</summary>
        public void Hero(ZHeroKind k, int member, Team team)
        {
            var tex = OzHeroSquadView.MemberPortrait(k, member, team);
            if (tex == null) { Robot(k != null ? k.BaseType : "grunt", team); return; }                  // no picture: his base type's face
            var s = OzBadgeArt.Get(Tier, OzBadgeArt.FaceFor(k.BaseType, team));                          // the disc that cuts it round
            if (disc.sprite != s) disc.sprite = s;
            disc.enabled = s != null;
            if (!mask.enabled || mask.showMaskGraphic) { mask.showMaskGraphic = false; mask.enabled = s != null; }
            if (photo.texture != tex) photo.texture = tex;
            photo.enabled = true; Shows = "hero:" + (member < 0 ? OzHeroPair.KindKey(k) : k.Members[member]);
            Rt.gameObject.SetActiveFast(true);
        }

        /// <summary>1.5 (Discord 10 Nov, the vehicle card's DRIVER slot, UI/OpenZHud.DriverSlot.cs): a robot type's round face; a type the
        /// production badge has no face for (the Electrobot: no factory builds it, so only the class glyph) shows its own card portrait
        /// (UnitArt.Portrait) cut round by a grunt face's disc, as <see cref="Hero"/> does, instead of the bare glyph.</summary>
        public void RobotOrPortrait(string type, Team team)
        {
            if (!OzBadgeArt.FaceFor(type, team).StartsWith("face_glyph_")) { Robot(type, team); return; }
            var tex = UnitArt.Portrait(type, team);
            var s = OzBadgeArt.Get(Tier, OzBadgeArt.FaceFor("grunt", team));                             // the disc that cuts it round
            if (tex == null || s == null) { Robot(type, team); return; }
            if (disc.sprite != s) disc.sprite = s;
            disc.enabled = true;
            if (!mask.enabled || mask.showMaskGraphic) { mask.showMaskGraphic = false; mask.enabled = true; }
            if (photo.texture != tex) photo.texture = tex;
            photo.enabled = true; Shows = type ?? "";
            Rt.gameObject.SetActiveFast(true);
        }

        public void Off() { if (Rt.gameObject.activeSelf) Rt.gameObject.SetActive(false); Shows = ""; }

        public void At(float x, float y) => Rt.Place(new OzRect(x, y, size, size));
        public void SetAlpha(float a) { var c = disc.color; if (Mathf.Abs(c.a - a) > 0.004f) { c.a = a; disc.color = c; } c = photo.color; if (Mathf.Abs(c.a - a) > 0.004f) { c.a = a; photo.color = c; } }
    }
}
