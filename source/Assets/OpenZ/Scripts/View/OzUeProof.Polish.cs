using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using OpenZ.Sim;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OpenZ
{
    /// <summary>
    /// 1.2 item 6 (the unit editor's Play proof, View/OzUeProof.cs): the polish checks, at each picture size (1920 x 1080, 1366 x 768,
    /// 2560 x 1600):
    ///  1 the drop preview: the arm bar names the team the dock's switch shows; every part of every preview model is its team's
    ///    see-through ghost (View/OzUeGhost.cs: no other paint, no per-object motion); in the picture the body and the head of each
    ///    robot (the hull and the top part of a vehicle or gun) show the team's hue and the body is about as bright as the head (0.55 ..
    ///    1.8 x its brightness; the weapon's colour is written too): in
    ///    the drop_preview_x5 picture of the fight (the models no live unit lies over) and on a quiet field for red grunts x1, blue
    ///    grunts x3, red grunts x5, a blue light tank, a red gatling and an empty (neutral, grey) light tank; at 1920 x 1080 also on the
    ///    MEDIUM and LOW graphics presets, the body's colour within 24 of 255 of HIGH's;
    ///  2 a plain click on the unit card's picture answers with the UI click, starts no drag, and the card stays open;
    ///  3 the drop's words (their plates) touch neither the squad's range ring nor its metres label (in every preview above);
    ///  4 no panel over another at every UI SCALE step (90 .. 160 %), a picture at 160 %;
    ///  5 the hover_card picture taken while the card shows (the field paused, the pointer kept on the grunt), and the card found in its
    ///    pixels (its HP bar's colour along the bar).
    /// The UI scale and the graphics preset it changes are put back (Restore, also after a stop by hand).
    /// </summary>
    public static partial class OzUeProof
    {
        static string lastShot;
        static Texture2D pic;
        static int quality0 = -1; static float uiScale0 = -1f;
        static readonly List<(GameObject go, string id, Team team)> shownModels = new List<(GameObject, string, Team)>();
        static readonly List<Material> matScratch = new List<Material>(8);
        /// <summary>The body colours of two graphics presets may differ this much per channel (of 255).</summary>
        const int PresetTolerance = 24;

        // ================================================================== 1 / 3 the drop preview of the fight
        /// <summary>Right after the drop_preview_x5 picture (armed, the mouse unmoved, the fight on the field).</summary>
        static IEnumerator DropPreviewChecks()
        {
            string word = ArmBarTeamWord();
            string want = OzUeUi.TeamWord(OzUeControls.Team) + (OzUePlace.CountFor(OzUePlace.Id) > 1 ? " x" + OzUePlace.CountFor(OzUePlace.Id) : "");
            Check(word == want && OzUePlace.Team == OzUeControls.Team, "1 the arm bar names the team the dock's switch shows: \"" + word + "\" (the dock " + OzUeUi.TeamWord(OzUeControls.Team)
                  + ", the armed drop " + OzUeUi.TeamWord(OzUePlace.Team) + ")");
            Check(GhostParts(out string parts), "1 every part of every preview model is its team's see-through ghost: " + parts);
            yield return Picture(lastShot);
            Check(GhostPixels(pic, true, out string px, null), "1 the drop_preview_x5 picture: " + px);
            DropPicture();
            Check(WordsClear(out string words), "3 the drop's words clear the squad's range ring: " + words);
        }

        // ================================================================== 1 / 3 the quiet previews
        static IEnumerator GhostPictures()
        {
            var f = Focus();
            var team0 = OzUeControls.Team; int count0 = OzUeControls.Count;
            OzUePlace.Stop(); OzUePick.Unpick(); OzUePick.ClearInspect(); OzUePlayground.ClearField(); yield return 2;
            OzUeControls.SetPlaying(false);                                                                  // a quiet field: nothing walks into the samples
            yield return HintWhileArmed(f);                                                                  // the empty field's hint hides while a drop is armed (View/OzUeProof.Last.cs)
            var cases = new (string id, Team team, int n, string name, string words)[]
            {
                ("grunt", OzUeKeys.ShownRed, 1, "x1_red", "a red grunt squad x1"), ("grunt", OzUeKeys.ShownBlue, 3, "x3_blue", "blue grunt squads x3"),
                ("grunt", OzUeKeys.ShownRed, 5, "x5_red", "red grunt squads x5"), ("light", OzUeKeys.ShownBlue, 1, "light_blue", "a blue light tank (a vehicle)"),
                ("gatling", OzUeKeys.ShownRed, 1, "gatling_red", "a red gatling (a gun)"), ("light", Team.Neutral, 1, "light_neutral", "an empty light tank (NEUTRAL)"),
            };
            var high = new List<Color32>(); int wordsOk = 0, wordsN = 0; var wordsBad = new StringBuilder();
            foreach (var c in cases)
            {
                yield return ArmGhost(c.id, c.team, c.n, f);
                if (!OzUePlace.Armed) { Check(false, "1 " + c.words + " armed for its preview"); continue; }
                yield return Shot("drop_ghost_" + c.name);
                bool mats = GhostParts(out string mw);
                yield return Picture(lastShot);
                bool px = GhostPixels(pic, false, out string pw, c.name == "x5_red" ? high : null);
                TintSpread(pic, c.name);                                                                     // every part of every model the same tint (View/OzUeProof.Last.cs)
                if (c.name == "x5_red") FloorOnHigh(pic);                                                    // the floor's colour on HIGH, for the presets
                DropPicture();
                Check(mats && px, "1 the drop preview of " + c.words + " is one see-through ghost in its team colour in every part: " + mw + "; " + pw);
                wordsN++;
                if (WordsClear(out string ww)) wordsOk++;
                else if (wordsBad.Length < 400) wordsBad.Append(' ').Append(c.name).Append(": ").Append(ww).Append(';');
                OzUePlace.Stop(); yield return 2;
            }
            Check(wordsN > 0 && wordsOk == wordsN, "3 the drop's words clear the range ring, its label and the blast ring in the " + wordsN + " quiet previews (" + wordsOk + " clear)"
                  + (wordsOk < wordsN ? ":" + wordsBad : ""));
            TintCheck();                                                                                     // one line: every quiet preview one tint (View/OzUeProof.Last.cs)
            if (size == "1920x1080")
            {
                if (high.Count > 0) yield return GhostPresets(f, high[0]);
                else Check(false, "1 the same ghost on every graphics preset: no body colour sampled on HIGH");
            }
            OzUeControls.SetTeam(team0); OzUeControls.SetCount(count0); OzUeControls.SetPlaying(true); yield return 2;
        }

        /// <summary>Arms a tile as a click on its dock tile does (the dock's team first), the mouse on the field.</summary>
        static IEnumerator ArmGhost(string id, Team team, int n, Vector3 f)
        {
            OzUePlace.Stop();
            OzUeControls.SetTeam(team); OzUeControls.SetCount(n); yield return 2;
            OzUePlace.ToggleArm(id, OzUeControls.Team);
            Move(ToScreen(f + new Vector3(0, 0, 6))); yield return 0.5f;
        }

        /// <summary>1920 x 1080: the red x5 preview on the MEDIUM and LOW graphics presets against HIGH's body colour.</summary>
        static IEnumerator GhostPresets(Vector3 f, Color32 high)
        {
            if (quality0 < 0) quality0 = OzQuality.Level;
            int lvl0 = OzQuality.Level;
            foreach (int lvl in new[] { 1, 2 })
            {
                OzQuality.Level = lvl; yield return 1.0f;
                yield return ShadersReady();                                                                 // the editor draws a variant it still compiles as a flat cyan stand-in (View/OzUeProof.Last.cs)
                yield return ArmGhost("grunt", OzUeKeys.ShownRed, 5, f);
                yield return ShadersReady();
                yield return Shot("drop_ghost_x5_red_" + OzQuality.Names[lvl].ToLowerInvariant());
                yield return Picture(lastShot);
                var bodies = new List<Color32>();
                bool px = GhostPixels(pic, false, out string pw, bodies);
                FloorOnPreset(pic, lvl);                                                                     // the floor as on HIGH (View/OzUeProof.Last.cs)
                DropPicture();
                int d = bodies.Count > 0 ? MaxDiff(bodies[0], high) : 999;
                Check(px && d <= PresetTolerance, "1 the same ghost on the " + OzQuality.Names[lvl] + " graphics preset: the first model's body " + (bodies.Count > 0 ? Hex(bodies[0]) : "-")
                      + ", on HIGH " + Hex(high) + " (" + d + " of 255 apart at most, " + PresetTolerance + " allowed); " + pw);
                OzUePlace.Stop(); yield return 2;
            }
            OzQuality.Level = lvl0; yield return 1.0f;
        }

        /// <summary>The arm bar's team word as shown ("RED x5").</summary>
        static string ArmBarTeamWord()
        {
            var bar = Named("OzUe_ArmBar", false); if (!bar) return "(no arm bar)";
            var t = bar.Find("Team"); var tm = t ? t.GetComponentInChildren<TextMeshProUGUI>(true) : null;
            return tm ? StripTags(tm.text) : "(no team word)";
        }

        /// <summary>Every drawn part of every shown preview model is its team's ghost, none writes per-object motion.</summary>
        static bool GhostParts(out string what)
        {
            var d = UnityEngine.Object.FindFirstObjectByType<OzUePlaceDriver>();
            if (!d) { what = "no placing driver"; return false; }
            d.Shown(shownModels);
            int parts = 0, ghost = 0, blur = 0, other = 0; var sb = new StringBuilder(); var teams = new HashSet<string>();
            foreach (var s in shownModels)
            {
                float a = OzUePlaceDriver.GhostAlpha(s.team);
                teams.Add(OzUeUi.TeamWord(s.team) + " " + Mathf.RoundToInt(a * 100f) + " %");
                foreach (var r in s.go.GetComponentsInChildren<Renderer>(true))
                {
                    if (!r.enabled || !r.gameObject.activeInHierarchy || r.forceRenderingOff) continue;
                    parts++;
                    if (OzUeGhost.Is(r, s.team, a)) ghost++;
                    else
                    {
                        other++;
                        if (sb.Length < 240)
                        {
                            r.GetSharedMaterials(matScratch);
                            sb.Append(' ').Append(r.name).Append(" (").Append(matScratch.Count > 0 && matScratch[0] && matScratch[0].shader ? matScratch[0].shader.name : "no material").Append(");");
                            matScratch.Clear();
                        }
                    }
                    if (r.motionVectorGenerationMode == MotionVectorGenerationMode.Object || (r is SkinnedMeshRenderer sm && sm.skinnedMotionVectors)) blur++;
                }
            }
            what = shownModels.Count + " model(s), " + parts + " drawn parts: " + ghost + " the ghost (" + string.Join(", ", teams) + "), " + other + " other paint" + (other > 0 ? ":" + sb : "")
                   + ", " + blur + " with per-object motion";
            return shownModels.Count > 0 && parts > 0 && ghost == parts && blur == 0;
        }

        /// <summary>The picture's pixels at the shown models' body and head (a vehicle's or gun's hull and top part): the team's hue in both,
        /// the body about as bright as the head. skipCovered: models a live unit lies over are left out and 80 % of the others must pass
        /// (the fight's picture: fire and wrecks behind a see-through ghost); else every model. bodies (if given) gets the first model's
        /// body colour.</summary>
        static bool GhostPixels(Texture2D tex, bool skipCovered, out string what, List<Color32> bodies)
        {
            var d = UnityEngine.Object.FindFirstObjectByType<OzUePlaceDriver>(); var cam = OzUeDraw.Cam;
            if (!d || !cam || !tex) { what = "the picture or the preview could not be read"; return false; }
            d.Shown(shownModels);
            var live = new List<Rect>();
            if (skipCovered)
            {
                var ed = OzUnitEditor.I;
                if (ed && ed.Input) foreach (var o in OzUePlayground.Placed()) { if (!o.IsUnit || Gone(o)) continue; var r = ed.Input.ScreenRect(o); if (r.width > 0f) live.Add(r); }
            }
            var order = new List<(GameObject go, string id, Team team, Rect scr)>();
            foreach (var s in shownModels) { var sr = ModelScreen(cam, s.go); if (sr.width > 0f) order.Add((s.go, s.id, s.team, sr)); }
            order.Sort((a, b) =>                                                                             // top row first, left to right (whole pixels: a steady order)
            {
                int ya = Mathf.RoundToInt(a.scr.center.y), yb = Mathf.RoundToInt(b.scr.center.y);
                return ya != yb ? yb.CompareTo(ya) : Mathf.RoundToInt(a.scr.center.x).CompareTo(Mathf.RoundToInt(b.scr.center.x));
            });
            int models = 0, good = 0, covered = 0; string fail = null, sample = null;
            foreach (var o in order)
            {
                if (skipCovered && Overlaps(o.scr, live)) { covered++; continue; }
                var parts = PickParts(o.go, OzUePlayground.KindOf(o.id));
                if (parts.Count == 0) continue;
                models++;
                bool ok = true; float bodyL = -1f, headL = -1f; var line = new StringBuilder();
                foreach (var p in parts)
                {
                    if (!Sample(tex, cam, p.r, o.team, out float share, out Color32 mean)) { ok = false; line.Append(' ').Append(p.what).Append(" off the picture;"); continue; }
                    bool hue = share >= 0.5f && TeamHue(mean, o.team);
                    if (p.what != "weapon") ok &= hue;                                                        // the body and the head (hull and top) decide; the thin weapon is reported
                    float l = Luma(mean);
                    if (p.what == "body" || p.what == "hull") { bodyL = l; if (bodies != null && models == 1) bodies.Add(mean); }
                    else if (p.what == "head" || p.what == "top") headL = l;
                    line.Append(' ').Append(p.what).Append(' ').Append(Hex(mean)).Append(" hue ").Append(HueDeg(mean)).Append(", ").Append(Mathf.RoundToInt(share * 100f)).Append(" % of its pixels")
                        .Append(hue ? "" : " NOT THE TEAM COLOUR").Append(';');
                }
                if (bodyL > 0f && headL > 0f)
                {
                    float k = bodyL / headL; bool even = k >= 0.55f && k <= 1.8f;
                    ok &= even;
                    line.Append(" body / head brightness ").Append(k.ToString("0.00", CultureInfo.InvariantCulture)).Append(even ? "" : " (NOT AS READABLE)").Append(';');
                }
                if (ok) good++;
                string desc = o.id + " " + OzUeUi.TeamWord(o.team) + ":" + line;
                if (!ok && fail == null) fail = desc;
                if (sample == null) sample = desc;
            }
            int need = skipCovered ? Mathf.CeilToInt(models * 0.8f) : models;                               // over the fight a fire or a wreck may shine through a ghost
            what = good + " of " + models + " model(s) show the team colour in every sampled part (" + need + " needed)" + (covered > 0 ? " (" + covered + " left out: a live unit lies over them)" : "") + "; "
                   + (fail ?? sample ?? "none sampled");
            return models > 0 && good >= need;
        }

        /// <summary>A robot's body (its skinned mesh), head (a part under a Head node, else the highest part) and weapon; a vehicle's or a
        /// gun's hull (the biggest part) and its top part (the highest of the others).</summary>
        static List<(string what, Renderer r)> PickParts(GameObject go, UeKind kind)
        {
            var l = new List<(string, Renderer)>();
            var rs = new List<Renderer>();
            foreach (var r in go.GetComponentsInChildren<Renderer>(false)) if (r.enabled && !r.forceRenderingOff) rs.Add(r);
            Renderer body = null, head = null, gun = null; float bv = -1f;
            foreach (var r in rs) { float v = Vol(r); if (v > bv) { bv = v; body = r; } }
            if (!body) return l;
            if (kind == UeKind.Robot)
            {
                float sv = -1f;
                foreach (var r in rs) if (r is SkinnedMeshRenderer && Vol(r) > sv) { sv = Vol(r); body = r; }
                foreach (var r in rs)
                {
                    if (r == body) continue;
                    if (!head && Under(r.transform, go.transform, "Head")) head = r;
                    else if (!gun && Under(r.transform, go.transform, "Weapon")) gun = r;
                }
            }
            if (!head)
            {
                float hy = float.MinValue; float bodyV = Vol(body);
                foreach (var r in rs)
                {
                    if (r == body || r == gun || Vol(r) < bodyV * 0.01f) continue;
                    if (r.bounds.center.y > hy) { hy = r.bounds.center.y; head = r; }
                }
            }
            bool robot = kind == UeKind.Robot;
            l.Add((robot ? "body" : "hull", body));
            if (head) l.Add((robot ? "head" : "top", head));
            if (gun) l.Add(("weapon", gun));
            return l;
        }

        static float Vol(Renderer r) { var s = r.bounds.size; return s.x * s.y * s.z; }

        static bool Under(Transform t, Transform root, string word)
        {
            for (var x = t; x && x != root; x = x.parent) if (x.name.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        /// <summary>The model's rect on the screen (pixels): its drawn parts' boxes.</summary>
        static Rect ModelScreen(Camera cam, GameObject go)
        {
            float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
            foreach (var r in go.GetComponentsInChildren<Renderer>(false))
            {
                if (!r.enabled || r.forceRenderingOff) continue;
                var b = r.bounds;
                for (int k = 0; k < 8; k++)
                {
                    var p = new Vector3((k & 1) == 0 ? b.min.x : b.max.x, (k & 2) == 0 ? b.min.y : b.max.y, (k & 4) == 0 ? b.min.z : b.max.z);
                    var s = cam.WorldToScreenPoint(p); if (s.z <= cam.nearClipPlane) continue;
                    x0 = Mathf.Min(x0, s.x); y0 = Mathf.Min(y0, s.y); x1 = Mathf.Max(x1, s.x); y1 = Mathf.Max(y1, s.y);
                }
            }
            return x1 > x0 ? new Rect(x0, y0, x1 - x0, y1 - y0) : default;
        }

        static bool Overlaps(Rect r, List<Rect> l) { foreach (var x in l) if (x.Overlaps(r)) return true; return false; }

        /// <summary>A window of pixels round the part's box centre on the screen: the share in the team's hue and the mean colour.</summary>
        static bool Sample(Texture2D tex, Camera cam, Renderer r, Team team, out float share, out Color32 mean)
        {
            share = 0f; mean = default;
            var s = cam.WorldToScreenPoint(r.bounds.center);
            if (s.z <= cam.nearClipPlane) return false;
            float kx = tex.width / (float)Mathf.Max(1, Screen.width), ky = tex.height / (float)Mathf.Max(1, Screen.height);
            int cx = Mathf.RoundToInt(s.x * kx), cy = Mathf.RoundToInt(s.y * ky), k = Mathf.Max(1, Mathf.RoundToInt(1.5f * OzUnitEditor.PxPerUnit * kx));
            if (cx - k < 0 || cy - k < 0 || cx + k >= tex.width || cy + k >= tex.height) return false;
            int n = 0, hit = 0; long sr = 0, sg = 0, sb = 0;
            for (int y = cy - k; y <= cy + k; y++)
                for (int x = cx - k; x <= cx + k; x++)
                {
                    Color32 c = tex.GetPixel(x, y);
                    n++; sr += c.r; sg += c.g; sb += c.b;
                    if (TeamHue(c, team)) hit++;
                }
            share = hit / (float)n;
            mean = new Color32((byte)(sr / n), (byte)(sg / n), (byte)(sb / n), 255);
            return true;
        }

        /// <summary>The team's colour as the picture shows a ghost of it: brighter than the dark floor and the team's hue (within 40 degrees
        /// of its ring colour, saturated), or for neutral a grey.</summary>
        static bool TeamHue(Color32 c, Team team)
        {
            Color.RGBToHSV(c, out float h, out float s, out float v);
            if (v < 0.22f) return false;                                                                   // the floor is about 0.09
            if (team == Team.Neutral) return s <= 0.28f;
            Color.RGBToHSV(OzUeGhost.Colour(team), out float th, out _, out _);
            return s >= 0.22f && Mathf.Abs(Mathf.DeltaAngle(h * 360f, th * 360f)) <= 40f;
        }

        static float Luma(Color32 c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
        static int HueDeg(Color32 c) { Color.RGBToHSV(c, out float h, out _, out _); return Mathf.RoundToInt(h * 360f); }
        static string Hex(Color32 c) => "#" + c.r.ToString("X2") + c.g.ToString("X2") + c.b.ToString("X2");
        static int MaxDiff(Color32 a, Color32 b) => Mathf.Max(Mathf.Abs(a.r - b.r), Mathf.Max(Mathf.Abs(a.g - b.g), Mathf.Abs(a.b - b.b)));

        /// <summary>The drop's word plates on the screen touch neither the range ring's line nor its metres label.</summary>
        static bool WordsClear(out string what)
        {
            var cam = OzUeDraw.Cam; var field = OzUnitEditor.Layer(UeLayer.Field);
            if (!cam || !field) { what = "no camera or field layer"; return false; }
            float rm = OzUePlaceDriver.WordsRangeM; string metres = rm > 0.1f ? OzUeMarks.Metres(rm) : null;
            var plates = new List<Rect>(); Rect label = default; bool hasLabel = false;
            foreach (var rt in field.GetComponentsInChildren<RectTransform>(false))
            {
                if (rt.name != "OzUe_WorldLabel") continue;
                var plate = rt.Find("Plate") as RectTransform;
                if (plate && plate.gameObject.activeInHierarchy) { plates.Add(ScreenRect(plate)); continue; }
                if (metres == null || hasLabel) continue;
                var tm = rt.GetComponentInChildren<TextMeshProUGUI>(false);
                if (tm && StripTags(tm.text) == metres) { label = TextScreenRect(tm); hasLabel = label.width > 0f; }
            }
            var xs = new float[96]; var ys = new float[96]; int n = 0;
            if (rm > 0.1f)
            {
                var c = OzUePlaceDriver.WordsCentre;
                for (int k = 0; k < 96; k++)
                {
                    float a = k * (2f * Mathf.PI / 96f);
                    var s = cam.WorldToScreenPoint(new Vector3(c.x + Mathf.Cos(a) * rm, c.y, c.z + Mathf.Sin(a) * rm));
                    if (s.z <= cam.nearClipPlane) continue;
                    xs[n] = s.x; ys[n] = s.y; n++;
                }
            }
            int touch = 0;
            foreach (var p in plates)
                if (OzUeWords.SegmentsHit(xs, ys, n, p.xMin - 1f, p.yMin - 1f, p.xMax + 1f, p.yMax + 1f) || (hasLabel && p.Overlaps(label))) touch++;
            int blast = BlastTouches(plates, out string blastWords); bool hint = HintOnScreen();                // the blast ring too; the empty field's hint hides while placing (View/OzUeProof.Last.cs)
            int ch = Mathf.Clamp(OzUePlaceDriver.WordsChoice, 0, OzUeWords.ChoiceWords.Length - 1);
            what = plates.Count + " plate(s), " + (rm > 0.1f ? F0(rm) + " m range ring" + (hasLabel ? " and its label \"" + metres + "\"" : "") : "no range ring") + ": " + touch + " touch it" + blastWords
                   + (hint ? "; THE EMPTY FIELD'S HINT SHOWS UNDER THEM" : "") + "; placed " + OzUeWords.ChoiceWords[ch];
            return plates.Count > 0 && touch == 0 && blast == 0 && !hint;
        }

        static Rect ScreenRect(RectTransform rt)
        {
            var c = new Vector3[4]; rt.GetWorldCorners(c);
            Vector2 a = RectTransformUtility.WorldToScreenPoint(null, c[0]), b = RectTransformUtility.WorldToScreenPoint(null, c[2]);
            return new Rect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y));
        }

        static Rect TextScreenRect(TextMeshProUGUI tm)
        {
            var b = tm.textBounds; if (b.size.x <= 0f) return default;
            Vector2 a = RectTransformUtility.WorldToScreenPoint(null, tm.rectTransform.TransformPoint(b.min)), z = RectTransformUtility.WorldToScreenPoint(null, tm.rectTransform.TransformPoint(b.max));
            return new Rect(Mathf.Min(a.x, z.x), Mathf.Min(a.y, z.y), Mathf.Abs(z.x - a.x), Mathf.Abs(z.y - a.y));
        }

        /// <summary>The Blueprint floor's mouse read-out on the screen now (View/OzUeFloor.cs TickLabels), 2 px padded; default = not shown.</summary>
        static Rect MouseReadoutScreen()
        {
            var root = Named("UE_FloorLabels", true); var t = root ? root.Find("Mouse") : null;
            var tm = t && t.gameObject.activeInHierarchy ? t.GetComponentInChildren<TextMeshProUGUI>() : null;
            var r = tm ? TextScreenRect(tm) : default; if (r.width <= 0f) return default;
            return Rect.MinMaxRect(r.xMin - 2f, r.yMin - 2f, r.xMax + 2f, r.yMax + 2f);
        }

        // ================================================================== 2 the unit card's picture
        static IEnumerator CardPictureClick()
        {
            var card = Named("OzUeCard", false); var picRt = card ? card.Find("Left/Picture") as RectTransform : null;
            if (!picRt || !picRt.gameObject.activeInHierarchy) { Check(false, "2 the unit card's picture is on the screen"); yield break; }
            float t = Time.unscaledTime; int drags = OzUePlace.Drags;
            yield return Click(Centre(picRt)); yield return 3;
            bool sound = UiSoundSince(t);
            Check(sound && OzUePlace.Drags == drags && !OzUePlace.Busy && OzUeStats.CardOpen, "2 a plain click on the unit card's picture (tooltip \"Drag onto the field\") answers with the UI click ("
                  + sound + "), starts no drag (" + (OzUePlace.Drags - drags) + ") and the card stays open (" + OzUeStats.CardOpen + ")");
        }

        // ================================================================== 4 every UI SCALE step
        static IEnumerator UiScaleSteps()
        {
            var hud = OzUnitEditor.I ? OzUnitEditor.I.Hud : null; if (!hud) yield break;
            if (uiScale0 < 0f) uiScale0 = OzText.UiScale;
            float s0 = OzText.UiScale;
            PlaceBlueGrunt(); yield return 0.6f;                                                             // a unit on the field: the empty field's hint stays away
            foreach (var s in OzText.ScalePresets)
            {
                OzText.UiScale = s; hud.ApplyUiScale(); yield return 0.8f;                                   // the HUD's scaler (the pixel floor kept), then the editor's relayout
                yield return Park();
                var cs = OzUnitEditor.CanvasSize; var ps = Panels(); int over = 0; var ob = new StringBuilder(); string play = "-", zoom = "-";
                for (int i = 0; i < ps.Count; i++)
                {
                    if (ps[i].name == "UE_PlayBar") play = R(ps[i].r);
                    if (ps[i].name == "UE_Zoom") zoom = R(ps[i].r);
                    for (int j = i + 1; j < ps.Count; j++)
                    {
                        var a = ps[i].r; var b = ps[j].r;
                        float w = Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.x, b.x), h = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.y, b.y);
                        if (w > 1f && h > 1f) { over++; if (over <= 6) ob.Append(' ').Append(ps[i].name).Append(" x ").Append(ps[j].name).Append(';'); }
                    }
                }
                Check(over == 0, "4 no panel overlaps another at UI SCALE " + Mathf.RoundToInt(s * 100f) + " % (canvas " + F0(cs.x) + " x " + F0(cs.y) + " u, " + ps.Count + " panels; play bar "
                      + play + ", zoom " + zoom + ")" + (over > 0 ? ":" + ob : ""));
                if (Mathf.Abs(s - OzText.ScaleMax) < 0.001f) yield return Shot("ui_scale_160");
            }
            OzText.UiScale = s0; hud.ApplyUiScale(); yield return 0.8f;
            OzUePlayground.ClearField(); yield return 2;
        }

        // ================================================================== 5 the hover card's picture
        static IEnumerator HoverCardPicture(ZObject unit)
        {
            if (unit == null || Gone(unit)) { unit = BlueGrunt() ?? PlaceBlueGrunt(); yield return 0.6f; }
            if (unit == null || Gone(unit)) { Check(false, "10 a blue grunt squad on the field for hover_card"); yield break; }
            OzUeControls.SetPlaying(false); yield return 2;                                                  // the field holds still: the grunt stays under the pointer
            yield return Uncover();                                                                          // nothing left open over the field: the hover card never shows under a card
            int shows = OzUeHoverCard.Shows; var card = Named("OzUeHoverCard", true); int on = 0;
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 3f && on < 4)
            {
                Move(UnitScreen(unit)); yield return 2;
                on = card && card.gameObject.activeInHierarchy ? on + 1 : 0;
            }
            Check(OzUeHoverCard.Shows > shows, "10 the hover card shows over a blue grunt (" + (OzUeHoverCard.Shows - shows) + " time(s))");
            yield return Shot("hover_card");
            bool shown = card && card.gameObject.activeInHierarchy;
            yield return Picture(lastShot);
            bool px = HoverCardPixels(pic, card, out string what);
            DropPicture();
            Check(shown && px, "5 the hover_card picture is taken while the card shows (" + (shown ? "shown" : "NOT shown") + " at the picture) and the picture has it: " + what);
            yield return HoverCovered(unit, card);                                                         // no hover card behind an open card, none over a panel (View/OzUeProof.Last.cs)
            OzUeControls.SetPlaying(true); yield return 2;
        }

        /// <summary>The hover card in the picture: its HP bar's fill colour along the bar's middle row.</summary>
        static bool HoverCardPixels(Texture2D tex, RectTransform card, out string what)
        {
            if (!tex || !card) { what = "no picture or no card"; return false; }
            var fillT = card.Find("BarFill"); var fill = fillT ? fillT.GetComponent<Image>() : null;
            if (!fill) { what = "the card has no HP bar"; return false; }
            var r = ScreenRect(fill.rectTransform);
            float kx = tex.width / (float)Mathf.Max(1, Screen.width), ky = tex.height / (float)Mathf.Max(1, Screen.height);
            int y = Mathf.RoundToInt(r.center.y * ky - 0.5f), x0 = Mathf.CeilToInt((r.xMin + 2f) * kx), x1 = Mathf.FloorToInt((r.xMax - 2f) * kx);
            Color32 want = fill.color;
            int n = 0, hit = 0;
            for (int x = x0; x <= x1; x++)
            {
                if (x < 0 || x >= tex.width || y < 0 || y >= tex.height) continue;
                Color32 c = tex.GetPixel(x, y); n++;
                if (Mathf.Abs(c.r - want.r) <= 14 && Mathf.Abs(c.g - want.g) <= 14 && Mathf.Abs(c.b - want.b) <= 14) hit++;
            }
            what = "its HP bar's colour " + Hex(want) + " in " + hit + " of " + n + " pixels along the bar (row " + y + ")";
            return n >= 8 && hit >= n * 0.7f;
        }

        // ================================================================== small helpers
        /// <summary>The last picture read back (a few tries while the file is still being written).</summary>
        static IEnumerator Picture(string path)
        {
            DropPicture();
            for (int i = 0; i < 20 && !pic; i++) { pic = LoadPicture(path); if (!pic) yield return 3; }
        }

        static Texture2D LoadPicture(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            try
            {
                var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (t.LoadImage(File.ReadAllBytes(path), false) && t.width > 2) return t;
                UnityEngine.Object.Destroy(t);
            }
            catch (Exception) { }
            return null;
        }

        static void DropPicture() { if (pic) UnityEngine.Object.Destroy(pic); pic = null; }

        /// <summary>A node of the HUD canvas by name.</summary>
        static RectTransform Named(string name, bool inactiveToo)
        {
            var hud = OpenZHud.I; if (!hud || !hud.Root) return null;
            foreach (var rt in hud.Root.GetComponentsInChildren<RectTransform>(inactiveToo)) if (rt.name == name) return rt;
            return null;
        }

        /// <summary>The UI scale and the graphics preset back as the player had them (also after a stop by hand).</summary>
        static void RestorePolish()
        {
            DropPicture();
            if (uiScale0 >= 0f)
            {
                OzText.UiScale = uiScale0; var hud = OzUnitEditor.I ? OzUnitEditor.I.Hud : OpenZHud.I; if (hud) hud.ApplyUiScale();
                uiScale0 = -1f;
            }
            if (quality0 >= 0) { if (OzQuality.Level != quality0) OzQuality.Level = quality0; quality0 = -1; }
        }
    }
}
