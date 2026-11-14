// 1.2 item 6, View/OzUnitEditor.cs: module P3's counted checks (OzUeStatsRoot.SelfCheck): run in edit mode by the editor menu
// "2 Counted Check" and in Play by the proof. The sets (20 units x 14 stats, the cell kinds, the shipped ZodEngine / Z-Wiki data), how
// every value reaches the Sim, a set's plan (OpenZ leaves every row exactly as parsed), and the layouts at the canvas sizes of DESIGN_SPEC
// 2.1: the box's place, no stat name cut (TextMeshPro's preferred width against its cell), no text under 11 u, everything inside the canvas.
using System;
using System.Collections.Generic;
using System.Text;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    internal static class UeStatsChecks
    {
        static int fails;
        static StringBuilder log;

        static void Check(bool ok, string what)
        {
            log.Append(ok ? "PASS " : "FAIL ").Append("P3 ").Append(what).Append('\n');
            if (!ok) fails++;
        }

        /// <summary>The canvas sizes of spec 2.1 (screen -> canvas with the 0.85 pixel floor): 1920x1080 (also 1600x900 and 2560x1440),
        /// 1366x768 = 1607 x 904, 1536x730 = 1807 x 859, 2560x1600 = 1728 x 1080.</summary>
        public static readonly Vector2[] Sizes = { new Vector2(1920, 1080), new Vector2(1607, 904), new Vector2(1807, 859), new Vector2(1728, 1080) };
        public const float InsetTop = 40f, InsetBottom = 84f;

        public static int Run(StringBuilder sb)
        {
            log = sb ?? new StringBuilder(); fails = 0;
            GameObject temp = null;
            try
            {
                Data();
                temp = new GameObject("OzUeStatsCheck") { hideFlags = HideFlags.HideAndDontSave };
                var rt = temp.AddComponent<RectTransform>();
                var probe = Hud.Label(rt, "Probe", "", 12, OzUi.Ink, 0f, OzStyle.Plain);
                foreach (var s in Sizes) Layouts(s, probe);
            }
            catch (Exception e) { Check(false, "the checks ran without an exception: " + e.GetType().Name + ": " + e.Message); }
            finally
            {
                if (temp != null) { if (Application.isPlaying) UnityEngine.Object.Destroy(temp); else UnityEngine.Object.DestroyImmediate(temp); }
            }
            return fails;
        }

        // ------------------------------------------------------------------ data
        static void Data()
        {
            var oz = OzUeSets.OpenZTable;
            Check(oz != null && oz.Count == 22, "the OpenZ set (live from the game's rule tables) has 22 units (" + (oz != null ? oz.Count : 0) + ")");   // DV145: + the medium tank
            bool order = OzUePlayground.Units.Length == OzUeLiveStats.Units.Length;
            for (int i = 0; order && i < OzUeLiveStats.Units.Length; i++) order = OzUePlayground.Units[i] == OzUeLiveStats.Units[i];
            Check(order, "the stats' unit order is the playground palette's (the game's build-menu order, then heroes, shuttle)");
            int bad = 0, fixedNoRule = 0, editable = 0, fixedN = 0;
            var r = OzUeSets.Rules;
            foreach (var id in OzUeLiveStats.Units)
            {
                if (oz == null || !oz.TryGetValue(id, out var c) || c.Length != 14) { bad++; continue; }
                for (int i = 0; i < 13; i++)
                {
                    var route = OzUeLiveStats.Route(id, (UeStat)i, r, out string rule);
                    if (OzUeLiveStats.Editable(route)) { editable++; if (!c[i].HasNumber) bad++; }
                    else if (route == UeRoute.Fixed) { fixedN++; if (string.IsNullOrEmpty(rule)) fixedNoRule++; }
                    else if (c[i].HasNumber && !(id == "crane" && !r.craneGun)) bad++;
                }
            }
            Check(bad == 0, "every editable value has an OpenZ number and every value nothing can edit is n/a or no roll (" + bad + " wrong)");
            Check(fixedNoRule == 0, "every value fixed by a rule names the rule (" + fixedN + " fixed, " + editable + " editable)");
            var sh = OzUeSets.Shipped;
            Check(sh != null && sh.error == null, "the shipped ZodEngine / Z-Wiki sets load (Resources/" + OzUeSetData.ResourcePath + ".json)" + (sh != null && sh.error != null ? ": " + sh.error : ""));
            if (sh != null)
            {
                int zin = 0, win = 0;
                foreach (var id in OzUeLiveStats.Units)
                {
                    if (sh.zod[id][0].kind != UeCellKind.NotInSet) zin++;
                    if (sh.wiki[id][0].kind != UeCellKind.NotInSet) win++;
                }
                Check(zin == 17 && win == 17, "ZodEngine and Z-Wiki list the 17 units of Z (electrobot, heroes and shuttle are OpenZ only): " + zin + " / " + win);
                Check(sh.values >= 300, "the shipped sets hold their numbers (" + sh.values + " values)");
            }
            // a plan of OpenZ changes no row of the game's own parse; the plan's table is the OpenZ table
            var openzPlan = OzUeStatsApply.Make(OzUeSets.D.settingsText, r, oz, null);
            Check(openzPlan.rowWrites == 0, "the OpenZ set leaves every rule-table row exactly as parsed (" + openzPlan.rowWrites + " row writes)");
            int diff = 0;
            foreach (var id in OzUeLiveStats.Units)
                for (int i = 0; i < 14; i++)
                {
                    var a = oz[id][i]; var b = openzPlan.table[id][i];
                    if (a.kind != b.kind || (a.HasNumber && !OzUeLiveStats.Same(a.value, b.value))) diff++;
                }
            Check(diff == 0, "with OpenZ in use the playground uses exactly the OpenZ column (" + diff + " cells differ)");
            // ZodEngine's plan: every editable ZodEngine number is what the playground then uses (the tough's slower-rocket rule inverted)
            var zplan = OzUeStatsApply.Make(OzUeSets.D.settingsText, r, oz, (id, st) => { var c = OzUeSets.Cell(id, st, UeSet.ZodEngine); return c.HasNumber ? c.value : double.NaN; });
            int zd = 0, zn = 0;
            foreach (var id in OzUeLiveStats.Units)
                for (int i = 0; i < 13; i++)
                {
                    var st = (UeStat)i;
                    if (!OzUeLiveStats.Editable(OzUeLiveStats.Route(id, st, r, out _))) continue;
                    var c = OzUeSets.Cell(id, st, UeSet.ZodEngine);
                    if (!c.HasNumber) continue;
                    zn++;
                    var p = zplan.table[id][i];
                    if (!p.HasNumber || !OzUeSets.SameShown(st, OzUeLiveStats.Clamp(id, st, c.value), p.value)) { zd++; if (zd <= 5) log.Append("     ZodEngine ").Append(id).Append(' ').Append(OzUeLiveStats.Def(st).key).Append(": set ").Append(c.value).Append(", in play ").Append(p.value).Append('\n'); }
                }
            Check(zd == 0, "ZodEngine in use: the playground uses its " + zn + " editable values (" + zd + " differ)");
            var tree = OzUeSets.CustomToTree() as Dictionary<string, object>;
            bool tok = tree != null && tree.ContainsKey("name") && tree.ContainsKey("based_on") && tree.ContainsKey("changed") && tree["fields"] is Dictionary<string, object> f && f.Count == 13 && tree["units"] is Dictionary<string, object> u && u.Count >= 19;
            Check(tok, "the Custom values give the saved file's keys (name, based_on, changed, 13 fields, the units)");
        }

        // ------------------------------------------------------------------ layouts
        static void Layouts(Vector2 cs, OzText probe)
        {
            var L = UeBoxLayout.For(cs, InsetTop, InsetBottom);
            string at = " at " + cs.x.ToString("0") + " x " + cs.y.ToString("0") + " u";
            bool inside = L.x >= 12f - 0.01f && L.x + L.w <= cs.x - 12f + 0.01f && L.y >= InsetTop + 12f - 0.01f && L.y + L.h <= cs.y - InsetBottom - 12f + 0.01f;
            Check(inside, "the stats box stands inside the free canvas (" + L.x.ToString("0.#") + "," + L.y.ToString("0.#") + " " + L.w.ToString("0.#") + " x " + L.h.ToString("0.#") + ")" + at);
            if (Mathf.Abs(cs.x - 1920f) < 0.5f && Mathf.Abs(cs.y - 1080f) < 0.5f)
                Check(Mathf.Abs(L.x - 1486f) <= 2f && Mathf.Abs(L.y - 52f) <= 2f && Mathf.Abs(L.w - 422f) <= 2f && Mathf.Abs(L.h - 860f) <= 2f, "E050 the box at 1486,52 422 x 860 within 2 u (" + L.x.ToString("0.#") + "," + L.y + " " + L.w.ToString("0.#") + " x " + L.h + ")");
            float worst = 1e9f; string worstName = "";
            foreach (var d in OzUeLiveStats.Defs)
            {
                probe.FontSize = 12f; probe.Tracking = 0f;
                string nm = d.full + (d.unit.Length > 0 ? " " + d.unit : "");
                float room = L.NameW - 4f - 2f - probe.WidthAt(nm);
                if (room < worst) { worst = room; worstName = nm; }
            }
            Check(worst >= 0f, "no stat name is cut in the inspector (tightest \"" + worstName + "\": " + worst.ToString("0.0") + " u to spare)" + at);
            // the list and balance names
            float lw = 1e9f, bw = 1e9f; string ln = "", bn = "";
            float listRoom = ListNameRoom(L), balRoom = BalanceNameRoom(L);
            foreach (var id in OzUeLiveStats.Units)
            {
                string s = OzUePlayground.ShortName(id);
                probe.FontSize = 12f; probe.Tracking = 0.06f;
                float a = listRoom - probe.WidthAt(s + " 99");
                if (a < lw) { lw = a; ln = s; }
                probe.Tracking = 0.05f;
                float b = balRoom - probe.WidthAt(s);
                if (b < bw) { bw = b; bn = s; }
            }
            Check(lw >= 0f, "every list name and its on-field count fit (tightest " + ln + ": " + lw.ToString("0.0") + " u)" + at);
            Check(bw >= 0f, "every balance name fits (tightest " + bn + ": " + bw.ToString("0.0") + " u)" + at);
            // table
            float unitW = OzUeTable.UnitWidth(probe), colW = OzUeTable.ColumnWidth(cs.x, probe, unitW);
            Check(unitW + 14 * colW <= cs.x - 32f + 0.01f, "the table's 15 columns fit the canvas (unit column " + unitW.ToString("0.#") + " u, stat columns " + colW.ToString("0.#") + " u)" + at);
            probe.FontSize = 12f; probe.Tracking = 0f;
            float chip = 6f + probe.WidthAt(OzText.Tab("88")) + 10f + 4f, un = 1e9f; string unn = "";
            foreach (var id in OzUeLiveStats.Units)
            {
                probe.FontSize = 12f; probe.Tracking = 0.08f;
                float a = unitW - OzUeTable.NameX - chip - probe.WidthAt(OzUePlayground.FullName(id));
                if (a < un) { un = a; unn = OzUePlayground.FullName(id); }
            }
            Check(un >= -0.01f, "every table unit name and a two-digit on-field chip fit the unit column (tightest " + unn + ": " + un.ToString("0.0") + " u to spare)" + at);
            float th = 1e9f; string tn = "";
            foreach (var d in OzUeLiveStats.Defs)
            {
                probe.FontSize = 12f; probe.Tracking = 0.06f;
                float a = colW - 6f - 2f - probe.WidthAt(d.head);
                probe.Tracking = 0f;
                a = Mathf.Min(a, colW - 6f - 2f - probe.WidthAt(d.unitLong));
                if (a < th) { th = a; tn = d.head; }
            }
            Check(th >= 0f, "no table head is cut (tightest " + tn + ": " + th.ToString("0.0") + " u)" + at);
            float tableTop = OzUeTable.Top + OzUeTable.HeaderH + OzUeTable.HeadH;
            Check(cs.y - tableTop >= 5 * OzUeTable.RowH, "the table shows its rows under its head (" + (cs.y - tableTop).ToString("0") + " u, scrolls when needed)" + at);
            // the unit card
            float cw = Mathf.Clamp(0.38f * cs.x, 600f, 720f);
            float ch = OzUeCard.HeaderH + 8f + 15 * OzUeCard.RowH + 10f + OzUeCard.FootH;
            float cardRoom = cs.y - (InsetBottom + 8f + 38f) - 10f - (InsetTop + 12f);         // the card may rise to the top bar + 12 when the window is short
            Check(cw <= cs.x - 24f && ch <= cardRoom, "the unit card fits over the play bar (" + cw.ToString("0") + " x " + ch.ToString("0") + " in " + cardRoom.ToString("0") + " u)" + at);
            float cstat = (cw - Mathf.Max(210f, cw * 287.2f / 720f) - 24f) * 110.2f / 406.8f, cr = 1e9f; string cn = "";
            foreach (var d in OzUeLiveStats.Defs)
            {
                probe.FontSize = 13f; probe.Tracking = 0f;
                float a = cstat - 10f - probe.WidthAt(OzUeCard.Label(d.stat) + (d.unit.Length > 0 ? " " + d.unit : ""));
                if (a < cr) { cr = a; cn = OzUeCard.Label(d.stat); }
            }
            Check(cr >= 0f, "no stat name is cut in the unit card (tightest " + cn + ": " + cr.ToString("0.0") + " u)" + at);
            // texts: no size under 11 u (OzText.MinSize raises anything smaller; the module's smallest sizes are 11 and the compact words 12)
            float min = Mathf.Min(L.words, 11f);
            Check(min >= OzText.MinSize - 0.01f, "no text under 11 u (smallest " + min + ")" + at);
            // compact rows: the inspector fits its box
            float need = L.bodyTop + L.strip + L.head + 3 * L.wordsLine + L.wordsPad + L.gridHead + 14 * L.gridRow + 2f + L.foot;
            Check(need <= L.h + 0.5f || L.compact, "the inspector fits the box at " + (L.compact ? "compact" : "normal") + " rows (" + need.ToString("0") + " of " + L.h.ToString("0") + " u)" + at);
            // hover card cells
            float inner = OzUeHoverCard.W - 2 * OzUeHoverCard.Pad, k = (inner - 16f) / 228f;
            string[] caps = { "BUILD", "HP", "DMG", "RANGE", "DPS" }; float[] w = { 46f, 48f, 44f, 46f, 44f };
            float hr = 1e9f;
            for (int i = 0; i < 5; i++) { probe.FontSize = 11f; probe.Tracking = 0.08f; hr = Mathf.Min(hr, w[i] * k - probe.WidthAt(caps[i])); }
            Check(hr >= 0f, "the hover card's five captions never touch (" + hr.ToString("0.0") + " u to spare)" + at);
        }

        static float ListNameRoom(UeBoxLayout L)
        {
            bool dps = !L.narrowList;
            float gap = dps ? 6f : 5f, right = L.w - 12f;
            float[] w = dps ? new[] { 46f, 50f, 56f, 48f, 42f } : new[] { 44f, 48f, 54f, 46f };
            foreach (var x in w) right -= x + gap;
            float nameX = 10f + OzUeStatsList.PicS + gap;
            return right - nameX - 4f;
        }

        static float BalanceNameRoom(UeBoxLayout L)
        {
            float gap = L.narrowBal ? 4f : 6f, gapR = L.w - 10f, gapW = L.narrowBal ? 42f : 46f, valueW = L.narrowBal ? 60f : 64f;
            float valueR = gapR - gapW - gap, mid = valueR - valueW - gap - (10f + 26f + gap);
            float nameW = L.narrowBal ? 64f : Mathf.Clamp(mid - 50f - gap, 60f, 92f);
            return nameW - 4f;                                                                     // the name's own text width (OzUeBalance.MakeRow: 4 u left pad, as the prototype's .br .n)
        }
    }
}
