using System.Collections.Generic;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.2 items 170 / 170b / 170c (the empty v2 look, redone): every empty (neutral) v2 model reads like the empty light, MLRS and mech v1:
    /// a LIGHT GREY body, its team-colour parts a little whiter, its tracks and small dark metal a little darker; subtle tone steps, no black
    /// body, no strong black and white, the paint placed smoothly. One rule for every v2 model (heavy, light, medium tank, MLRS, mech, gun),
    /// worked out at run time from the model's own texture, so a swapped or new v2 model needs no edit.
    ///   The shader's k98b block (Shaders/TeamLit.shader, the heavy branch every v2 model is drawn with), per texel:
    ///     1. the mask, smooth: the texture's alpha (0 metal, 0.5 paint, 1 team) read on a blurred mip (one mip texel = 1 / <see cref="MaskCells"/>
    ///        of the texture, never finer than the screen's own mip), so the parts change on whole panels, with soft edges, never per texel;
    ///     2. the texel's own detail (shading, panel lines, rivets, wear), turned grey: its luminance on a mip <see cref="DetailBias"/> softer
    ///        than the screen's, over the material's paint reference (_HeavyLook.x), to the power <see cref="Contrast"/>, kept within
    ///        <see cref="Lo"/> .. <see cref="Hi"/> (metal parts against <see cref="MetalRef"/> of that reference);
    ///     3. value-mapped to v1's empty greys: metal <see cref="Metal"/>, body <see cref="Body"/>, team <see cref="TeamPart"/> (linear), x the
    ///        detail, in v1's empty warm grey. The gun takes its own three greys (<see cref="GunBody"/> ...), like the empty v1 gun.
    ///   Owned looks, v1 models and wrecks (Charred at <see cref="WreckFrom"/> or more: the burned look as before) are not touched.
    /// The numbers are v1's own, measured through TeamLit's maths at the RTS camera: body median 0.129-0.145 linear (light / MLRS / mech v1),
    /// metal 0.085-0.097; the gun v1 body 0.172. Every number is a static field: change it in Play, then call <see cref="Push"/>.
    /// </summary>
    public static class OzV2EmptyLook
    {
        /// <summary>On = the look above. Off = the v2 models' old empty paint (a test aid only; the game keeps it on).</summary>
        public static bool Enabled = true;
        /// <summary>The vehicles' greys (linear albedo, before the texel's detail): body (the paint), team-colour parts, tracks and dark metal.</summary>
        public static float Body = 0.125f, TeamPart = 0.13f, Metal = 0.085f;   // item 198: TeamPart 0.15 -> 0.13 (the team areas the same grey as the body, as the empty light v1)
        /// <summary>The gun's greys (linear albedo), like the empty v1 gun's.</summary>
        public static float GunBody = 0.165f, GunTeam = 0.18f, GunMetal = 0.10f;
        /// <summary>How much of the texel's own light and dark is kept (1 = all of it, 0 = flat), and the limits of that detail (x the grey).</summary>
        public static float Contrast = 0.4f, Lo = 0.75f, Hi = 1.25f;
        /// <summary>The mask's blur: one mip texel covers 1 / MaskCells of the texture (128 on a 2048 texture = 16 x 16 texels).</summary>
        public static float MaskCells = 128f;
        /// <summary>The detail is read this many mips softer than the screen's own (no grain, no shimmer while the camera moves).</summary>
        public static float DetailBias = 1.5f;
        /// <summary>The metal parts' own reference: this share of the paint reference (they are darker in the texture).</summary>
        public static float MetalRef = 0.3f;
        /// <summary>A paint burned this far or more is a wreck: it keeps the burned look.</summary>
        public const float WreckFrom = 0.5f;
        /// <summary>Each v2 model's own level: its empty look x this, as a colour (0.93 = about 7 % darker on screen; 1 = the greys above
        /// unchanged), so every empty v2 reads like the empty light / MLRS / mech v1 in the game's own measurements: the six L02 Play proofs'
        /// EMPTY LOOK means (pixels under the upward faces, RTS and close frames) and the lineup picture's medians. With the shared greys the
        /// heavy v2 read 112 / 107 against that family's 99 / 102 (lineup 116.5 against 112) and the light v2 110 / 107 against its v1's 94 / 92
        /// (lineup 108.5 against 111.5: the two disagree; the light's level sits between them); the medium tank (101 / 92 against 94 / 90),
        /// the MLRS (100 / 103 against 97 / 107), the mech (108 / 105 against 106 / 107) and the gun (100 / 106 against 107 / 106) were within
        /// 8 % already. Change in Play, then <see cref="Push"/> (it repaints every empty v2).</summary>
        public static float HeavyLevel = 1.0f, LightLevel = 1.0f, MediumTankLevel = 1f, MlrsLevel = 1f, MechLevel = 1f, GunLevel = 1f;
        /// <summary>The empty light / MLRS / mech v1 family's value in the L02 Play proofs (their v1 EMPTY LOOK means: RTS 94 / 97 / 106, close
        /// 92 / 107 / 107) and spread (p10-p90: RTS 67 / 89 / 64, close 68 / 130 / 67). The heavy v2 is judged against it (its own v1 is the
        /// black and white heavy, not the grey the empty v2s follow).</summary>
        public static float FamilyRts = 99f, FamilyClose = 102f, FamilySpreadRts = 73f, FamilySpreadClose = 88f;

        internal static readonly int StyleId = Shader.PropertyToID("_OZEmptyV1");
        static readonly int LookId = Shader.PropertyToID("_OZEmptyLook"), Look2Id = Shader.PropertyToID("_OZEmptyLook2"), GunId = Shader.PropertyToID("_OZEmptyGun");
        internal static readonly int LegacyId = Shader.PropertyToID("_LegacyHeavy");
        static Vector4 sentLook, sentLook2, sentGun;
        static bool sent;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Init() { sent = false; tally.Clear(); tags.Clear(); LastRule = ""; }

        /// <summary>The shader's globals (sent again whenever a number above changed).</summary>
        internal static void Globals()
        {
            var look = new Vector4(Body, TeamPart, Metal, Contrast);
            var look2 = new Vector4(Lo, Hi, Mathf.Max(1f, MaskCells), DetailBias);
            var gun = new Vector4(GunBody, GunTeam, GunMetal, MetalRef);
            if (sent && look == sentLook && look2 == sentLook2 && gun == sentGun) return;
            sent = true; sentLook = look; sentLook2 = look2; sentGun = gun;
            Shader.SetGlobalVector(LookId, look);
            Shader.SetGlobalVector(Look2Id, look2);
            Shader.SetGlobalVector(GunId, gun);
        }

        /// <summary>Sends the numbers now (after changing them in Play: every empty v2 on screen changes at once, its level too).</summary>
        public static void Push() { sent = false; Globals(); TeamPaint.RepaintEmptyV2(); }

        /// <summary>
        /// *lid*) is its body grey x <see cref="LidStep"/> (the empty light v1's lid against its body on screen: 0.46 / 0.17 linear), the heavy's
        /// x <see cref="HeavyLidStep"/> (the empty heavy v1's grey hatch on its dark body: 0.25 / 0.06). Off = the mask only.</summary>
        public static bool HatchLidWhite = true;
        public static float LidStep = 2.8f, HeavyLidStep = 4.2f;
        /// <summary>Item 198: the empty heavy v2 copies the empty heavy v1 (dark body 0.06, white parts 0.57, hatch 0.25 linear on screen):
        /// its own body and team greys (linear albedo, before the detail). 0 = the shared Body / TeamPart.</summary>
        public static float HeavyBody = 0.065f, HeavyTeam = 0.43f;   // run 8: 0.045 -> 0.065 (reads dark grey, not black, in the L17 dusk)
        /// <summary>Item 198 : the heavy v2's second hatch is part of its turret mesh (HeavyTankTurret):
        /// a disc there (mesh x, z, radius m, measured on a top view) drawn as light as its lid (HeavyLidStep). Radius 0 = off.</summary>
        public static Vector3 HeavyHatch2 = new Vector3(-0.62f, 0.115f, 0.27f);
        internal static readonly int DiscId = Shader.PropertyToID("_OZEmptyDisc");
        /// <summary>The renderer's disc for the shader (_OZEmptyDisc): the heavy v2's turret only, else none.</summary>
        internal static Vector4 RendererDisc(Renderer r, int unit)
        {
            if (!r || unit != 1 || HeavyHatch2.z <= 0f || r.name != "HeavyTankTurret") return Vector4.zero;
            return new Vector4(HeavyHatch2.x, HeavyHatch2.y, HeavyHatch2.z, Mathf.Max(1f, HeavyLidStep));
        }
        /// <summary>The renderer's per-renderer values for the shader (_OZEmptyV1.yzw): lid step (0 = not a lid), body, team (0 = shared).</summary>
        internal static Vector3 RendererLook(Renderer r, int unit)
        {
            if (!r) return Vector3.zero;
            string n = r.name.ToLowerInvariant();
            bool lid = HatchLidWhite && n.Contains("hatch") && n.Contains("lid");
            float step = lid ? Mathf.Max(1f, unit == 1 ? HeavyLidStep : LidStep) : 0f;
            return unit == 1 ? new Vector3(step, HeavyBody, HeavyTeam) : new Vector3(step, 0f, 0f);
        }

        /// <summary>Which v2 model (for its level): 1 heavy, 2 light, 3 medium tank, 4 MLRS, 5 mech, 6 gun, 0 none.</summary>
        public static int UnitOf(Transform root)
        {
            if (!root) return 0;
            if (OzGun.Models.IsV2(root)) return 6;
            if (OzHeavyTank.IsV2(root)) return 1;
            if (OzLightTank.Models.IsV2(root)) return 2;
            if (OzMediumTank.Models.IsV2(root)) return 3;
            if (OzMlrs.Models.IsV2(root)) return 4;
            if (OzMech.Models.IsV2(root)) return 5;
            return 0;
        }

        /// <summary>The level of a model <see cref="UnitOf"/> found (1 = unchanged).</summary>
        public static float LevelOf(int unit)
        {
            float l = unit == 1 ? HeavyLevel : unit == 2 ? LightLevel : unit == 3 ? MediumTankLevel : unit == 4 ? MlrsLevel : unit == 5 ? MechLevel : unit == 6 ? GunLevel : 1f;
            return Mathf.Clamp(l, 0.5f, 1.5f);
        }

        /// <summary>1 = a v2 vehicle, 2 = the gun v2, 0 = any other model (the v2 models carry frame nodes only they have).</summary>
        public static int KindOf(Transform root)
        {
            if (!root) return 0;
            if (OzGun.Models.IsV2(root)) return 2;
            if (OzHeavyTank.IsV2(root)) return 1;
            foreach (var u in OzV2Units.All) if (u != OzGun.Models && u.IsV2(root)) return 1;
            return 0;
        }

        // ---------------------------------------------------------------- the empty-look proofs (the v2 units' Play proofs, EMPTY LOOK lines)
        static readonly Dictionary<string, List<float>> tally = new Dictionary<string, List<float>>();
        static readonly List<string> tags = new List<string>();                                // the tags in the order the proof tallied them
        /// <summary>The proofs: one screen pixel (sRGB value 0-255) under an empty model's upward faces ("v1" / "v2"; a proof with other
        /// tags, e.g. the medium tank's "reference" / "medium": its first tag is the v1, its second the v2).</summary>
        public static void Tally(string tag, float l)
        {
            if (!tally.TryGetValue(tag, out var c)) { c = new List<float>(); tally[tag] = c; tags.Add(tag); }
            c.Add(l);
        }
        static List<float> Tagged(string tag, int index)
        {
            if (tally.TryGetValue(tag, out var c)) return c;
            if (index >= tags.Count || tags[index] == "v1" || tags[index] == "v2") return null;   // a missing v1 (or v2) never borrows the other one's pixels
            return tally.TryGetValue(tags[index], out c) ? c : null;
        }
        /// <summary>The rule of the last <see cref="Verdict"/> with its numbers, for the proof's line.</summary>
        public static string LastRule = "";

        static float Pct(List<float> s, float p)
        {
            if (s == null || s.Count == 0) return -1f;
            int i = Mathf.Clamp(Mathf.RoundToInt(p * (s.Count - 1)), 0, s.Count - 1);
            return s[i];
        }
        static float Share(List<float> s, System.Func<float, bool> f)
        {
            if (s == null || s.Count == 0) return 0f;
            int n = 0; foreach (var x in s) if (f(x)) n++;
            return 100f * n / s.Count;
        }
        static float Mean(List<float> s)
        {
            if (s == null || s.Count == 0) return -1f;
            float t = 0f; foreach (var x in s) t += x;
            return t / s.Count;
        }

        /// <summary>The proofs' EMPTY LOOK verdict on the pixels tallied since the last call (then cleared), the same for every v2 model:
        /// v2's mean within 12 % of its v1's, under 5 % near white (over 200), under 5 % near black (under 45), and v2's spread (p90 - p10)
        /// no wider than v1's + 10 (subtle tones, no strong black and white).</summary>
        public static bool Verdict(bool gun)
        {
            var a = Tagged("v1", 0); var b = Tagged("v2", 1);
            a?.Sort(); b?.Sort();
            float am = Mean(a), bm = Mean(b);
            float aS = a != null && a.Count > 0 ? Pct(a, 0.9f) - Pct(a, 0.1f) : -1f, bS = b != null && b.Count > 0 ? Pct(b, 0.9f) - Pct(b, 0.1f) : -1f;
            float white = Share(b, x => x > 200f), black = Share(b, x => x < 45f);
            bool ok = am > 0f && bm > 0f && bm >= am * 0.88f && bm <= am * 1.12f && white < 5f && black < 5f && bS <= aS + 10f;
            LastRule = $"k98b / item {(gun ? "153 + 170b" : "170b")}: v2 mean {bm:0} within 12 % of v1's {am:0}, near white {white:0.0} % and near black {black:0.0} % " +
                       $"(each under 5), spread p10-p90 v2 {bS:0} vs v1 {aS:0} (no more than v1 + 10)";
            tally.Clear(); tags.Clear();
            return ok;
        }

        /// <summary>The heavy proof's EMPTY LOOK verdict (item 170b: every empty v2 like the empty light / MLRS / mech v1): the v2's pixels
        /// against the family's value (<see cref="FamilyRts"/> / <see cref="FamilyClose"/>, the `rts` frame or the close one), not against the
        /// heavy v1 beside it (black and white: shown, not judged); the same tolerances as <see cref="Verdict"/>.</summary>
        public static bool VerdictFamily(bool rts)
        {
            var a = Tagged("v1", 0); var b = Tagged("v2", 1);
            a?.Sort(); b?.Sort();
            float am = Mean(a), bm = Mean(b), fm = rts ? FamilyRts : FamilyClose, fs = rts ? FamilySpreadRts : FamilySpreadClose;
            float bS = b != null && b.Count > 0 ? Pct(b, 0.9f) - Pct(b, 0.1f) : -1f;
            float white = Share(b, x => x > 200f), black = Share(b, x => x < 45f);
            bool ok = bm > 0f && bm >= fm * 0.88f && bm <= fm * 1.12f && white < 5f && black < 5f && bS <= fs + 10f;
            LastRule = $"item 170b: v2 mean {bm:0} within 12 % of the empty light / MLRS / mech v1 family's {fm:0} ({(bm > 0f ? (bm / fm - 1f) * 100f : -100f):+0;-0} %; " +
                       $"the heavy v1 beside it {am:0}: shown, not judged), near white {white:0.0} % and near black {black:0.0} % (each under 5), spread p10-p90 v2 {bS:0} vs the family's {fs:0} (no more than + 10)";
            tally.Clear(); tags.Clear();
            return ok;
        }
    }

    /// <summary>1.2 items 170 / 170b / 170c: the empty v2 look's per-renderer flag (<see cref="OzV2EmptyLook"/>), set with the rest of the paint,
    /// and each v2 model's own level (<see cref="OzV2EmptyLook.HeavyLevel"/> ...) on the colour TeamPaint.Apply has just set.</summary>
    public partial class TeamPaint
    {
        int emptyKind = -1, emptyUnit;                                                         // -1 = not looked at yet

        /// <summary>TeamPaint.Apply, every material of every renderer: the empty v2 look on (an empty, unburned v2 model's own parts) or off.</summary>
        void EmptyV1(Renderer r, Material m, MaterialPropertyBlock b, bool mine)
        {
            int style = 0;
            if (mine && m && m.HasProperty(OzV2EmptyLook.LegacyId) && m.GetFloat(OzV2EmptyLook.LegacyId) > 0.5f)
            {
                if (emptyKind < 0) { emptyKind = OzV2EmptyLook.KindOf(transform); emptyUnit = OzV2EmptyLook.UnitOf(transform); }
                if (OzV2EmptyLook.Enabled && team == Team.Neutral && charred < OzV2EmptyLook.WreckFrom) style = emptyKind;
            }
            if (style > 0)
            {
                OzV2EmptyLook.Globals();
                float lv = OzV2EmptyLook.LevelOf(emptyUnit);
                if (lv != 1f && m.HasProperty(BaseColorId))
                {   // the same colour Apply has just set (the material's, x the burn factor), x this model's level
                    float k = 1f - 0.78f * charred, kg = (glowLevel < 1f && OzGlowFade.IsGlow(m) ? k * glowLevel : k) * lv;
                    b.SetColor(BaseColorId, m.GetColor(BaseColorId) * new Color(kg, kg, kg, 1));
                }
            }
            var rl = style > 0 ? OzV2EmptyLook.RendererLook(r, emptyUnit) : Vector3.zero;   // item 198: yzw = the lid step, this model's own body / team grey
            b.SetVector(OzV2EmptyLook.StyleId, new Vector4(style, rl.x, rl.y, rl.z));
            b.SetVector(OzV2EmptyLook.DiscId, style > 0 ? OzV2EmptyLook.RendererDisc(r, emptyUnit) : Vector4.zero);   // item 198: the heavy v2's second hatch
        }

        /// <summary>OzV2EmptyLook.Push: every empty v2 on screen painted again (a level changed in Play).</summary>
        internal static void RepaintEmptyV2()
        {
            foreach (var p in FindObjectsByType<TeamPaint>(FindObjectsSortMode.None))
                if (p && p.emptyKind > 0) p.Apply();
        }
    }
}
