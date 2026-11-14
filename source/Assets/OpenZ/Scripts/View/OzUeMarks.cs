using System.Collections.Generic;
using System.Globalization;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.2 item 6 (the unit editor, View/OzUnitEditor.cs): the editor's world marks on the field (DESIGN_SPEC E206, E208-E212), on
    /// top of the game's own marks (selection brackets in the team colour, health bars, the hover chip, order lines and markers):
    ///  E206 the dashed weapon range ring (6 / 5, 1 u) of the hovered unit (team ring 45 %) and of the picked one (75 %), its metres at
    ///       the far top of the ring, a solid INK 30 % blast ring for explosive weapons (blast over 0.6 m);
    ///  E208 from the picked unit a faint dashed line (3 / 4) with a small cross to every enemy in its range that an obstacle hides
    ///       (the Sim's own line test, ZWorld.EngageBarrierBetween);
    ///  E209 the picked units' names (the first one with "522 / 1081 HP" and, after a middle dot, the stats it runs: "CUSTOM";
    ///       heroes in AMBER_HI), and with the Background option "Name labels on every unit" one name over every squad and vehicle; the
    ///       hovered unit keeps the game's own hover chip;
    ///  E210 every flag's dashed 1.2 m ring (#CFCDBF; hovered / picked INK 1.5 u); hovered / picked its words in two lines, "FLAG  neutral"
    ///       over "Robots take it by touch" (View/OzUeMarks.Words.cs);
    ///  E211 the obstacle's outline when hovered / picked, its 40 x 3 u bar when hit, hovered or picked; its words only when hovered or
    ///       picked, "OBSTACLE  5405 / 10000 HP" over "Ctrl + right-click attacks it";
    ///  E212 every empty vehicle / gun's dashed ring (4 / 3), its 30 x 3 u bar; hovered / picked "EMPTY LIGHT TANK  3378 / 3378 HP" over
    ///       "A robot can climb in".
    /// The lists of what stands on the field are refreshed 4 times a second (and when the field changes), the lines of sight 5 times.
    /// </summary>
    [DefaultExecutionOrder(880)]
    internal sealed partial class OzUeMarks : MonoBehaviour
    {
        static readonly Color RingNeutral = new Color32(0xCF, 0xCD, 0xBF, 255), Blocked = new Color(1f, 140f / 255f, 110f / 255f, 0.55f), BlockedX = new Color(1f, 140f / 255f, 110f / 255f, 0.8f),
                              NeutralText = new Color32(0xE4, 0xE2, 0xD4, 255), BarPale = new Color32(0xE4, 0xE2, 0xD4, 255);
        readonly List<ZObject> flags = new List<ZObject>(), obstacles = new List<ZObject>(), empty = new List<ZObject>(), units = new List<ZObject>();
        readonly List<(ZObject a, ZObject b)> hidden = new List<(ZObject, ZObject)>();
        float listAt = -10f, sightAt = -10f; bool dirty = true; ZObject sightFor;
        readonly Dictionary<int, (int hp, int max, Team owner, string text)> textCache = new Dictionary<int, (int, int, Team, string)>();
        /// <summary>Checks: marks drawn in the last frame by kind.</summary>
        internal static int RangeRings, BlockedLines, FlagRings, EmptyRings, ObjectLabels, UnitLabels;

        void OnEnable() { OzUePlayground.FieldChanged += Dirty; }
        void OnDisable() { OzUePlayground.FieldChanged -= Dirty; }
        void Dirty() { dirty = true; }

        void LateUpdate()
        {
            var ed = OzUnitEditor.I;
            if (!ed || !OzUnitEditor.UiBuilt || ed.World == null) return;
            float now = Time.unscaledTime;
            if (dirty || now - listAt > 0.25f) { Collect(ed.World); listAt = now; dirty = false; textCache.Clear(); }
            RangeRings = BlockedLines = FlagRings = EmptyRings = ObjectLabels = UnitLabels = 0;
            ObstacleWords = FlagWords = EmptyWords = 0;                                                       // the words' checks (View/OzUeMarks.Words.cs)
            var hover = OzUePick.Hover; var kind = OzUePick.Kind; var insp = OzUePick.Inspected;
            bool all = OzUeBackdrop.NameLabels;
            var game = ed.Game;

            foreach (var f in flags)
            {
                if (Gone(f)) continue;
                bool on = f == hover || (kind == UeInspect.Flag && insp == f);
                Vector3 p = Pos(game, f);
                Color fr = f.Owner == Team.Neutral ? RingNeutral : OzUeUi.Ring(f.Owner);                       // a taken flag's ring in its owner's ring colour (prototype)
                OzUeDraw.Ring(p, 1.2f, on ? (Color)OzUi.Ink : OzUi.A(fr, 0.9f), 4f, 3f, on ? 1.5f : 1f); FlagRings++;
                if (on || all)                                                                               // two lines while pointed at or picked; its name alone with "Name labels on every unit"
                {
                    Words(p + Vector3.up * 3.2f, FlagText(f.Owner, false), on ? OzUePlan.FlagHint : null, f.Owner == Team.Neutral ? NeutralText : OzUeUi.TeamText(f.Owner), OzUePlan.FlagWordsUp);
                    if (on) { FlagWords = 2; LastFlagTop = FlagText(f.Owner, false); }
                }
            }
            foreach (var o in obstacles)
            {
                if (Gone(o)) continue;
                bool on = o == hover || (kind == UeInspect.Obstacle && insp == o);
                Vector3 p = Pos(game, o);
                float hx = Mathf.Max(1f, o.WidthPix * ZConst.PX_TO_M * 0.5f), hz = Mathf.Max(1f, o.HeightPix * ZConst.PX_TO_M * 0.5f);
                if (on) OzUeDraw.RectOutline(p, hx + 0.3f, hz + 0.3f, OzUi.Ink, 5f, 4f, 1.5f);
                Vector3 top = p + Vector3.up * OzUePlayground.ObstacleHeightM;
                bool hit = o.Health < o.MaxHealth;
                if (on || hit) OzUeDraw.HealthBar(top, OzUePlan.ObstacleBarUp, 40f, OzUePlan.ObstacleBarH, Frac(o), BarColour(Frac(o)));
                if (on || all)                                                                               // the words only while pointed at or picked (a hit one shows its bar alone); its name alone with "Name labels on every unit"
                {
                    Words(top, on ? Text(o, "OBSTACLE  ", "", true) : "OBSTACLE", on ? OzUePlan.ObstacleHint : null, NeutralText, OzUePlan.ObstacleWordsUp);
                    if (on) { ObstacleWords = 2; LastObstacleTop = Text(o, "OBSTACLE  ", "", true); }
                }
            }
            foreach (var o in empty)
            {
                if (Gone(o) || !OzUePlayground.IsEmptyHardware(o)) continue;
                bool on = o == hover || (kind == UeInspect.EmptyHardware && insp == o);
                Vector3 p = Pos(game, o);
                float r = SizeM(OzUePlayground.IdOf(o)) * 0.5f;                                              // the prototype's ground ring (Look.ground: SIZE / 2)
                OzUeDraw.Ring(p, r, on ? (Color)OzUi.Ink : OzUi.A(RingNeutral, 0.9f), 4f, 3f, on ? 1.5f : 1.2f); EmptyRings++;
                if (on || o.Health < o.MaxHealth) OzUeDraw.HealthBar(p + Vector3.up * TopOf(o), 8f, 30f, 3f, Frac(o), BarColour(Frac(o)));
                if (on || all)                                                                               // two lines while pointed at or picked; its name alone with "Name labels on every unit"
                {
                    string name = EmptyName(OzUePlayground.IdOf(o));
                    Words(p + Vector3.up * TopOf(o), on ? Text(o, name + "  ", "", true) : name, on ? OzUePlan.EmptyHint : null, NeutralText, OzUePlan.EmptyWordsUp);
                    if (on) { EmptyWords = 2; LastEmptyTop = Text(o, name + "  ", "", true); }
                }
            }

            PlatformMarks(game, hover, kind, insp, all);                                                    // the ramp platforms (View/OzUeMarks.Platforms.cs)
            CrateMarks(game, hover, kind, insp, all);                                                       // the GRENADES crates (View/OzUeMarks.Crates.cs)

            // units: range rings, lines of sight, labels
            var picked = OzUePick.Picked;
            ZObject primary = null;
            if (kind == UeInspect.Unit && insp != null && !Gone(insp)) primary = insp;
            else if (picked != null && picked.Count > 0 && !Gone(picked[0])) primary = picked[0];
            ZObject hovUnit = hover != null && hover.IsUnit && !Gone(hover) && !OzUePlayground.IsEmptyHardware(hover) ? (hover.IsMinion ? hover.Leader : hover) : null;
            if (hovUnit != null && hovUnit != primary) Range(game, hovUnit, 0.45f);
            if (primary != null) { Range(game, primary, 0.75f); Sight(ed.World, game, primary, now); }
            if (picked != null)
                for (int i = 0; i < picked.Count; i++)
                {
                    var u = picked[i];
                    if (Gone(u) || u == hovUnit) continue;
                    UnitLabel(game, u, u == primary);
                }
            if (all)
                foreach (var u in units)
                {
                    if (Gone(u) || u == hovUnit || Contains(picked, u)) continue;
                    UnitLabel(game, u, false);
                }
        }

        void UnitLabel(ZGame game, ZObject u, bool hp)
        {
            string id = OzUePlayground.IdOf(u);
            string name = OzUePlayground.FullName(id);
            string text = hp ? Text(u, name + "  ", OzUeSets.LabelTag(id), false) : name;                   // the picked one: its HP and the stats it runs (View/OzUeSets.Reset.cs)
            if (hp) LastUnitLabel = text;
            Color c = id == OzUePlayground.Heroes ? (Color)OzUi.AmberHi : OzUeUi.TeamText(u.Owner);
            OzUeDraw.Label(Pos(game, u) + Vector3.up * TopOf(u), text, c, 12f, 22f);                        // above the game's own bar and hover chip
            UnitLabels++;
        }

        void Range(ZGame game, ZObject u, float a)
        {
            float r = u.RingRadius * ZConst.PX_TO_M;                                                        // DV162: the range from where it stands (Sim/ZObject.HighGround.cs)
            if (r <= 0.1f || u.Damage == 0) return;
            Vector3 p = Pos(game, u);
            Color ring = OzUeUi.Ring(u.Owner);
            OzUeDraw.Ring(p, r, OzUi.A(ring, a), 6f, 5f, 1f);
            float blast = u.DamageRadius * ZConst.PX_TO_M;
            if (blast > 0.6f) OzUeDraw.Ring(p, blast, OzUi.A(OzUi.Ink, 0.3f), 0f, 0f, 1f);
            OzUeDraw.Label(FarTop(p, r), Metres(r), OzUi.A(OzUeUi.TeamText(u.Owner), Mathf.Min(1f, a + 0.15f)), 12f, 3f);
            RangeRings++;
        }

        void Sight(ZWorld W, ZGame game, ZObject u, float now)
        {
            if (obstacles.Count == 0 || u.Damage == 0 || u.AttackRadius <= 0) { hidden.Clear(); return; }
            if (now - sightAt > 0.2f || sightFor != u)
            {
                sightAt = now; sightFor = u; hidden.Clear();
                long r2 = (long)u.RingRadius * u.RingRadius;
                foreach (var v in W.Objects)
                {
                    if (!v.IsUnit || Gone(v) || v.Owner == Team.Neutral || v.Owner == u.Owner) continue;
                    long dx = v.CenterX - u.CenterX, dy = v.CenterY - u.CenterY;
                    if (dx * dx + dy * dy > r2) continue;
                    if (W.EngageBarrierBetween(u.CenterX, u.CenterY, v.CenterX, v.CenterY)) hidden.Add((u, v));
                    if (hidden.Count >= 40) break;
                }
            }
            foreach (var (a, b) in hidden)
            {
                if (Gone(a) || Gone(b)) continue;
                Vector3 pa = Pos(game, a), pb = Pos(game, b);
                OzUeDraw.Line(pa, pb, Blocked, 3f, 4f, 1f);
                OzUeDraw.Cross((pa + pb) * 0.5f, 6f, BlockedX);
                BlockedLines++;
            }
        }

        void Collect(ZWorld W)
        {
            flags.Clear(); obstacles.Clear(); empty.Clear(); units.Clear(); crates.Clear(); platforms.Clear();
            foreach (var o in OzUePlayground.Placed())
            {
                if (o == null || Gone(o)) continue;
                if (OzUePlayground.IsFlag(o)) flags.Add(o);
                else if (OzUePlayground.IsObstacle(o)) obstacles.Add(o);
                else if (OzUePlayground.IsPlatform(o)) platforms.Add(o);                                               // View/OzUeMarks.Platforms.cs
                else if (OzUePlayground.IsEmptyHardware(o)) empty.Add(o);
                else if (OzUePlayground.IsCrate(o)) crates.Add(o);                                                    // View/OzUeMarks.Crates.cs
                else if (o.IsUnit && !o.IsMinion) units.Add(o);
            }
        }

        /// <summary>"hp / max HP" text, rebuilt only when the numbers or the owner changed (no strings per frame).</summary>
        string Text(ZObject o, string head, string tail, bool hp)
        {
            int h = Mathf.Max(0, o.Health), m = o.MaxHealth;
            if (textCache.TryGetValue(o.Id, out var c) && c.hp == h && c.max == m && c.owner == o.Owner && c.text != null && c.text.StartsWith(head, System.StringComparison.Ordinal) && c.text.EndsWith(tail, System.StringComparison.Ordinal)) return c.text;
            string t = head + h.ToString(CultureInfo.InvariantCulture) + " / " + m.ToString(CultureInfo.InvariantCulture) + " HP" + tail;
            textCache[o.Id] = (h, m, o.Owner, t);
            return t;
        }

        static bool Contains(IReadOnlyList<ZObject> l, ZObject o) { if (l == null) return false; for (int i = 0; i < l.Count; i++) if (l[i] == o) return true; return false; }
        static bool Gone(ZObject o) => o == null || o.Destroyed || o.KillAt >= 0;
        static float Frac(ZObject o) => o.MaxHealth > 0 ? Mathf.Clamp01((float)o.Health / o.MaxHealth) : 1f;
        static Color BarColour(float f) => f > 0.4f ? BarPale : f > 0.2f ? (Color)OzUi.Amber : OzUi.Alert;

        /// <summary>Height of a unit's top above the floor (m), for its labels and bars.</summary>
        static float TopOf(ZObject o) => o.Kind == ZKind.Robot ? 1.9f : o.Kind == ZKind.Cannon ? 2.4f : o.Type == ZWorld.ShuttleType ? 3.6f : 2.8f;

        /// <summary>Where a unit is DRAWN (its view), else its Sim centre; on the floor.</summary>
        internal static Vector3 Pos(ZGame game, ZObject o)
        {
            if (game && game.Views.TryGetValue(o.Id, out var v) && v && v.gameObject.activeInHierarchy) { var p = v.transform.position; p.y = OzUePlatformViews.MarkY(game, p); return p; }   // 1.3: on a ramp platform's deck, not under it (0 with none on the field, as before)
            var w = ZGame.PxToWorld(o.CenterX, o.CenterY); w.y = OzUePlatformViews.MarkY(game, w); return w;
        }

        /// <summary>The point of a floor ring farthest from the camera (its metres label goes there: rarely under a unit).</summary>
        internal static Vector3 FarTop(Vector3 centre, float r)
        {
            var cam = OzUeDraw.Cam;
            Vector3 f = cam ? cam.transform.forward : Vector3.forward; f.y = 0f;
            if (f.sqrMagnitude < 1e-4f && cam) { f = cam.transform.up; f.y = 0f; }
            if (f.sqrMagnitude < 1e-4f) f = Vector3.forward;
            return centre + f.normalized * r;
        }

        /// <summary>The size a unit is drawn at in the prototype (Look.SIZE, m): its ground ring's diameter on the field
        /// and in the drop preview (robots 1.6, the tough and the electrobot 1.9, the heroes 1.7, the light tank 4.2, the heavy 5 ...).</summary>
        internal static float SizeM(string id)
        {
            switch (id)
            {
                case "tough": case "electrobot": return 1.9f;
                case OzUePlayground.Heroes: return 1.7f;
                case ZEvilZod.Type: return 2.9f;                                          // DV161
                case "jeep": case "medium": return 3.6f;
                case "light": case "howitzer": return 4.2f;
                case ZMTank.Type: return 4.6f;                                            // DV145: the medium tank, between the light tank and the heavy
                case "heavy": return 5f;
                case "apc": case "missile_launcher": case "crane": return 4.8f;
                case "gatling": case "gun": return 3.4f;
                case "missile_cannon": return 4f;
            }
            if (id != null && id == OzUePlayground.Shuttle) return 7f;
            return 1.6f;
        }

        /// <summary>"EMPTY LIGHT TANK" (cached per id).</summary>
        static string EmptyName(string id)
        {
            if (id == null) return "EMPTY";
            if (!emptyNames.TryGetValue(id, out var s)) { s = "EMPTY " + OzUePlayground.FullName(id); emptyNames[id] = s; }
            return s;
        }
        static readonly Dictionary<string, string> emptyNames = new Dictionary<string, string>();

        /// <summary>"15 m", "16.5 m" (cached per tenth of a metre: no new string per frame).</summary>
        internal static string Metres(float m)
        {
            int k = Mathf.RoundToInt(m * 10f);
            if (!metres.TryGetValue(k, out var s)) { s = (k / 10f).ToString("0.#", CultureInfo.InvariantCulture) + " m"; metres[k] = s; }
            return s;
        }
        static readonly Dictionary<int, string> metres = new Dictionary<int, string>();

        /// <summary>"FLAG  neutral" / "FLAG  red  -  robots take it by touch", the team word as the player sees it (cached).</summary>
        static string FlagText(Team owner, bool on)
        {
            var key = (owner, on, TeamColors.Swap);
            if (!flagTexts.TryGetValue(key, out var s))
            {
                string w = owner == Team.Neutral ? "neutral" : OzUeUi.TeamWordSentence(owner).ToLowerInvariant();
                s = "FLAG  " + w + (on ? "  -  robots take it by touch" : "");
                flagTexts[key] = s;
            }
            return s;
        }
        static readonly Dictionary<(Team, bool, bool), string> flagTexts = new Dictionary<(Team, bool, bool), string>();
    }
}
