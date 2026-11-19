using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// 1.5 kit ue_bar_r2 (tracker A13; editor only): the Unit editor's counted check, then its bottom band (dock, play bar, zoom control)
    /// laid out by UI/OzUeBottomLayout.cs in the game's font for a 1920 x 1080, a 1600 x 900 and a 1280 x 720 screen (UI SCALE 100 %, the
    /// stats box open) with every part checked: one dock row inside the canvas, its parts in order without overlap, every tile label of
    /// all four tabs inside its cell (at most the allowed hang, 1 u between neighbours), the extras' labels inside their cells, the play
    /// bar / zoom / dock apart, and at 1920 x 1080 the zoom control beside the dock. Writes Logs/ue_bar_r2_counted_check.txt,
    /// Logs/ue_bar_r2_layout.txt and Logs/ue_bar_r2_layout.json (every rect, for the kit's pictures).
    /// Menu OpenZ/V1.5/Unit Editor/Bottom bar r2: Check + layouts (edit mode); batch: -executeMethod OpenZ.EditorTools.OzUeBarR2Proof.Batch
    /// </summary>
    public static class OzUeBarR2Proof
    {
        static readonly (float w, float h)[] Screens = { (1920f, 1080f), (1600f, 900f), (1280f, 720f) };
        static readonly string[] TabNames = { "ROBOTS", "VEHICLES", "GUNS", "SPECIAL" };

        [MenuItem("OpenZ/V1.5/Unit Editor/Bottom bar r2: Check + layouts")]
        public static void Menu()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[OpenZ UeBarR2] run it in edit mode (not playing)"); return; }
            var s = Run(out int fails);
            if (fails == 0) Debug.Log(s); else Debug.LogWarning(s);
        }

        public static void Batch()
        {
            int code = 1;
            try { var s = Run(out int fails); Debug.Log(s); code = fails == 0 ? 0 : 2; }
            catch (Exception e) { Debug.LogError("[OpenZ UeBarR2] the proof threw: " + e); }
            EditorApplication.Exit(code);
        }

        static string F(float v) => v.ToString("0.##", CultureInfo.InvariantCulture);
        static string J(UeBox b) => "[" + F(b.x) + "," + F(b.y) + "," + F(b.w) + "," + F(b.h) + "]";
        static string Q(string s) => "\"" + (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

        public static string Run(out int fails)
        {
            string logs = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs")).Replace('\\', '/');
            Directory.CreateDirectory(logs);
            string check = OzUePlaygroundMenu.Check();
            File.WriteAllText(logs + "/ue_bar_r2_counted_check.txt", check);
            string head = check.Split('\n')[0];
            int cfail = 0; foreach (var l in check.Split('\n')) if (l.StartsWith("FAIL ", StringComparison.Ordinal)) cfail++;

            var log = new StringBuilder(); var json = new StringBuilder("{\"screens\":[");
            int f = 0;
            Func<bool, string, int> line = (ok, what) => { log.Append(ok ? "PASS " : "FAIL ").Append(what).Append('\n'); return ok ? 0 : 1; };
            var temp = new GameObject("UE_BarR2Measure", typeof(RectTransform)) { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var m = new OzUeMeasure(temp.transform);
                var labels = OzUeDock.TileLabels();
                for (int k = 0; k < Screens.Length; k++)
                {
                    var sc = Screens[k];
                    var cs = OzUnitEditor.CanvasFor(sc.w, sc.h, 1f);
                    float box = Mathf.Clamp(0.22f * cs.x, 360f, 470f);
                    var i = new OzUeBottomLayout.In { cw = cs.x, ch = cs.y, insetR = OzUeBottomLayout.Gap + box, insetT = 40f, tiles = labels, measure = m.Width,
                                                      flagLabel = OzUePlayground.ShortName(OzUePlayground.Flag), obstacleLabel = OzUePlayground.ShortName(OzUePlayground.Obstacle) };
                    var o = OzUeBottomLayout.Lay(i); var d = o.dock; var p = o.play.plate; var z = o.zoom.plate;
                    string at = $"at a {sc.w:0}x{sc.h:0} screen (canvas {F(cs.x)} x {F(cs.y)} u)";
                    var pl = d.plate;
                    f += line(pl.x >= OzUeBottomLayout.Gap - 0.01f && pl.Right <= cs.x - OzUeBottomLayout.Gap + 0.01f && (Mathf.Abs(pl.h - 72f) < 0.01f || Mathf.Abs(pl.h - 80f) < 0.01f),
                              $"dock one row inside the canvas {at}: {pl}, tiles {F(d.tile)} u, tile gap {F(d.tileGap)}, tab padding {F(d.tabPad)}");
                    // the parts in order, inside the plate (x relative to the plate)
                    float inner = pl.w - OzUeBottomLayout.Border - OzUeBottomLayout.DockPadR;
                    var chain = new (string n, float x, float r)[]
                    {
                        ("team / count column", d.teamWell.x, d.teamWell.Right), ("tabs", d.tabsWell.x, d.tabsWell.Right), ("tiles", d.tileArea.x, d.tileArea.Right),
                        ("caption", d.caption.x, d.caption.Right), ("FLAG", d.flagTile.x, d.flagTile.Right), ("OBSTACLE", d.obstacleTile.x, d.obstacleTile.Right),
                        ("BOX", d.boxTile.x, d.boxTile.Right), ("GRENADES", d.grenadesTile.x, d.grenadesTile.Right), ("NUKE", d.nukeTile.x, d.nukeTile.Right),
                        ("PLATFORM", d.platMidTile.x, d.platMidTile.Right), ("TOWER", d.platHighTile.x, d.platHighTile.Right), ("PIN", d.pin.x, d.pin.Right),
                    };
                    string bad = ""; float minGap = 1e9f;
                    for (int c = 0; c < chain.Length; c++)
                    {
                        if (c > 0) { float g = chain[c].x - chain[c - 1].r; minGap = Mathf.Min(minGap, g); if (g < -0.01f) bad += chain[c - 1].n + " over " + chain[c].n + "; "; }
                    }
                    if (chain[0].x < 0f || chain[chain.Length - 1].r > inner + 0.01f) bad += "a part outside the plate; ";
                    f += line(bad.Length == 0, $"dock parts in order, none over another, all inside the plate {at} (closest {F(minGap)} u){(bad.Length > 0 ? ": " + bad : "")}");
                    // every tab's labels in their cells
                    float hang = d.labelsInCell ? -OzUeBottomLayout.LabelPad : Mathf.Min(OzUeBottomLayout.LabelHang, Mathf.Max(0f, (d.tileGap - 1f) * 0.5f));
                    float minApart = d.labelsInCell ? d.tileGap + 2f * OzUeBottomLayout.LabelPad : 1f;   // the old rule: 1 u; the smaller tiles: gap + 2 u
                    if (Mathf.Abs(cs.x - 1920f) < 0.5f && Mathf.Abs(cs.y - 1080f) < 0.5f)   // 1.5 kit ue_bottom_left_r10 (A36): the zoom control left the band, the dock keeps its full tiles
                        f += line(!d.labelsInCell && Mathf.Abs(d.tile - OzUeBottomLayout.TileStd) < 0.01f, $"A36 the full {F(OzUeBottomLayout.TileStd)} u tiles (no room kept for a zoom control beside the dock; a label may hang {F(hang)} u, two labels at least {F(minApart)} u apart) {at}: tiles {F(d.tile)} u, labels in their cells {d.labelsInCell}");
                    json.Append(k > 0 ? "," : "").Append("{\"screen\":[").Append(F(sc.w)).Append(',').Append(F(sc.h)).Append("],\"canvas\":[").Append(F(cs.x)).Append(',').Append(F(cs.y))
                        .Append("],\"tile\":").Append(F(d.tile)).Append(",\"tileGap\":").Append(F(d.tileGap)).Append(",\"plate\":").Append(J(pl))
                        .Append(",\"team\":").Append(J(d.teamWell)).Append(",\"count\":").Append(J(d.countWell)).Append(",\"tabsWell\":").Append(J(d.tabsWell))
                        .Append(",\"tabCells\":[").Append(string.Join(",", Array.ConvertAll(d.tabCells, F))).Append("],\"teamCells\":[").Append(string.Join(",", Array.ConvertAll(d.teamCells, F)))
                        .Append("],\"countCells\":[").Append(string.Join(",", Array.ConvertAll(d.countCells, F))).Append("],\"tileArea\":").Append(J(d.tileArea))
                        .Append(",\"separatorX\":").Append(F(d.separatorX)).Append(",\"caption\":").Append(J(d.caption)).Append(",\"pin\":").Append(J(d.pin))
                        .Append(",\"zoom\":").Append(J(z)).Append(",\"zoomWhere\":").Append(Q(o.zoom.Where)).Append(",\"play\":").Append(J(p))
                        .Append(",\"playParts\":[").Append(J(o.play.play)).Append(',').Append(J(o.play.speed)).Append(',').Append(J(o.play.clear)).Append(',').Append(J(o.play.sample)).Append(',').Append(J(o.play.sound))
                        .Append("],\"playWords\":[").Append(Q(o.play.clearWord)).Append(',').Append(Q(o.play.sampleWord)).Append("],\"tabs\":[");
                    for (int t = 0; t < labels.Length; t++)
                    {
                        float x = d.tileArea.x, prevR = -1e9f, worstHang = -1e9f, worstGap = 1e9f; string worstName = "", cut = "";
                        json.Append(t > 0 ? "," : "").Append("{\"name\":").Append(Q(TabNames[t])).Append(",\"tiles\":[");
                        for (int j = 0; j < labels[t].Length; j++)
                        {
                            float cell = d.tileCells[t][j], lw = m.Width(labels[t][j], OzUeBottomLayout.TileLabelText, OzUeBottomLayout.TileLabelTrack);
                            float lx = x + (cell - lw) * 0.5f, h = (lw - cell) * 0.5f;
                            if (h > worstHang) { worstHang = h; worstName = labels[t][j]; }
                            if (h > hang + 0.01f) cut += labels[t][j] + " " + F(h) + " u past its cell; ";
                            if (j > 0) { worstGap = Mathf.Min(worstGap, lx - prevR); if (lx - prevR < minApart - 0.01f) cut += labels[t][j - 1] + " / " + labels[t][j] + " " + F(lx - prevR) + " u apart; "; }
                            json.Append(j > 0 ? "," : "").Append("{\"label\":").Append(Q(labels[t][j])).Append(",\"x\":").Append(F(x)).Append(",\"cell\":").Append(F(cell)).Append(",\"labelW\":").Append(F(lw)).Append('}');
                            prevR = lx + lw; x += cell + d.tileGap;
                        }
                        float tabW = x - d.tileGap - d.tileArea.x;
                        if (tabW > d.tileArea.w + 0.01f) cut += "the tab is " + F(tabW) + " u, the tile area " + F(d.tileArea.w) + " u; ";
                        json.Append("]}");
                        f += line(cut.Length == 0, $"{TabNames[t]}: {labels[t].Length} tiles in {F(tabW)} of {F(d.tileArea.w)} u, every label in its cell (the widest {worstName} {F(Mathf.Max(0f, worstHang))} u past its cell of the {F(hang)} allowed, neighbours at least {F(worstGap < 1e8f ? worstGap : 0f)} u apart) {at}{(cut.Length > 0 ? ": " + cut : "")}");
                    }
                    json.Append("],\"extras\":[");
                    var ex = new (string label, UeBox b)[] { (i.flagLabel, d.flagTile), (i.obstacleLabel, d.obstacleTile), (i.boxLabel, d.boxTile), (i.grenadesLabel, d.grenadesTile),
                                                            (i.nukeLabel, d.nukeTile), (i.platMidLabel, d.platMidTile), (i.platHighLabel, d.platHighTile) };
                    string exBad = ""; float exSpare = 1e9f;
                    for (int e = 0; e < ex.Length; e++)
                    {
                        float lw = m.Width(ex[e].label, OzUeBottomLayout.TileLabelText, OzUeBottomLayout.TileLabelTrack);
                        exSpare = Mathf.Min(exSpare, ex[e].b.w - lw);
                        if (lw > ex[e].b.w + 0.01f) exBad += ex[e].label + " " + F(lw - ex[e].b.w) + " u wider than its cell; ";
                        json.Append(e > 0 ? "," : "").Append("{\"label\":").Append(Q(ex[e].label)).Append(",\"box\":").Append(J(ex[e].b)).Append(",\"labelW\":").Append(F(lw)).Append('}');
                    }
                    json.Append("]}");
                    f += line(exBad.Length == 0, $"the 7 extras' labels inside their own cells {at} (least spare {F(exSpare)} u){(exBad.Length > 0 ? ": " + exBad : "")}");
                    f += line(!p.Overlaps(pl) && !p.Overlaps(z) && !z.Overlaps(pl) && p.x >= OzUeBottomLayout.Gap - 0.01f && p.Right <= cs.x - i.insetR - OzUeBottomLayout.Gap + 0.01f,
                              $"play bar {p}, zoom {z} ({o.zoom.Where}) and dock apart, the play bar inside the free field {at}");
                    if (Mathf.Abs(cs.x - 1920f) < 0.5f && Mathf.Abs(cs.y - 1080f) < 0.5f)
                        f += line(o.zoom.inMenu && z.w <= 0f && !z.Overlaps(pl), $"E030 (1.5 A36) the zoom control is the BACKGROUND popover's VIEW row, not in the bottom band {at}: band plate {z}, row {F(o.zoom.rowW)} u");
                }
            }
            catch (Exception e) { f += line(false, "the layout proof threw " + e.GetType().Name + ": " + e.Message); }
            finally { UnityEngine.Object.DestroyImmediate(temp); }
            json.Append("]}");
            File.WriteAllText(logs + "/ue_bar_r2_layout.json", json.ToString());
            int lp = 0; foreach (var l in log.ToString().Split('\n')) if (l.StartsWith("PASS ", StringComparison.Ordinal)) lp++;
            string text = $"[OpenZ UeBarR2] counted check: {head.Replace("[OpenZ UnitEditor] COUNTED CHECK: ", "")}; bottom band at 3 screens: {lp} PASS, {f} FAIL\n";
            foreach (var l in check.Split('\n')) if (l.StartsWith("FAIL ", StringComparison.Ordinal) || l.Contains("E040") || l.Contains("E030") || l.Contains("balance name")) text += "  check: " + l + "\n";
            text += log.ToString();
            File.WriteAllText(logs + "/ue_bar_r2_layout.txt", text);
            fails = f + cfail;
            return text;
        }
    }
}
