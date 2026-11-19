#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.5 kits new_tanks / mammoth_ram / bizon_crush: the Play check of the MAMMOTH and the BIZON in the Unit editor (editor only; started from
    /// code: OzBigTanksPlayCheck.Begin(folder)). Rows, one after another on free open ground near the camera, at the normal RTS camera:
    ///   L  both tanks dropped RED, BLUE and NEUTRAL beside the heavy tank v2 (one frame): Psycho driver in the owned ones, empty neutral, hatch lid there;
    ///   D  each tank drives 10 tiles and back (links move, sprockets turn, left / right opposite in the turn; the Bizon's exhaust bursts + chuffs);
    ///   F  each fires at an enemy heavy (the Mammoth's two barrels kick one after the other, the Bizon's one);
    ///   N  the Bizon ordered at a far enemy howitzer: its distance never grows (no backing off) until in range;
    ///   R  a Mammoth ram on an enemy heavy;  C1-C3 a Bizon crush on an enemy heavy, a jeep and a ground gun;
    ///   E  EXIT DRIVER (the Unit editor tab's own click action) on each: the driver out, the lid still there;
    ///   V  the select / move / attack voices of each (the sound director's own calls; what played is listed);
    ///   X  each destroyed: the many-piece break, its sounds.
    /// Dense frames (every 2nd rendered frame, JPEG) of the drives, the fire, the ram, the crushes and the deaths go to folder/&lt;row&gt;/; a report
    /// with every row's numbers to folder/play_report.md. Selection marks are cleared before every frame series. The staged units are removed after each row.
    /// </summary>
    public class OzBigTanksPlayCheck : MonoBehaviour
    {
        public const string Tag = "[OpenZ V1.5 Big Tanks Play]";
        public static string Folder = "";
        public static bool Running { get; private set; }
        public static string Report { get; private set; } = "no Play check run yet";
        public static int FramesWritten;

        ZGame G; ZWorld W; RtsCamera rts; PlayerInput input;
        readonly StringBuilder md = new StringBuilder();
        int pass, fail; int capUntilFrame = -1; string capDir; int capEvery = 2;
        readonly List<ZObject> made = new List<ZObject>();

        public static void Begin(string folder)
        {
            if (Running || !ZGame.I || ZGame.I.World == null || !OzUnitEditor.Active) { Debug.LogWarning(Tag + " open the Unit editor in Play first"); return; }
            if (ZGame.I.GetComponent<OzBigTanksPlayCheck>()) { Debug.LogWarning(Tag + " already running"); return; }   // a repeated start call (a tool retry) never runs a second copy
            Running = true; Folder = folder; ZGame.I.gameObject.AddComponent<OzBigTanksPlayCheck>();
        }

        void Start() { Running = true; StartCoroutine(Run()); StartCoroutine(Capture()); }
        void OnDestroy() { Running = false; }

        // ---- frames: every capEvery-th rendered frame while a series runs, encoded off the main thread
        IEnumerator Capture()
        {
            var eof = new WaitForEndOfFrame();
            while (true)
            {
                yield return eof;
                if (capDir == null || Time.frameCount > capUntilFrame) { capDir = null; continue; }
                if (Time.frameCount % capEvery != 0) continue;
                var tex = ScreenCapture.CaptureScreenshotAsTexture();
                if (!tex) continue;
                var raw = tex.GetRawTextureData(); int w = tex.width, h = tex.height; var fmt = tex.graphicsFormat;
                string path = Path.Combine(capDir, $"f{Time.frameCount:000000}_{Time.time:0000.000}s.jpg");
                Destroy(tex);
                System.Threading.Tasks.Task.Run(() => { File.WriteAllBytes(path, ImageConversion.EncodeArrayToJPG(raw, fmt, (uint)w, (uint)h, 0, 92)); });
                FramesWritten++;
            }
        }

        void Frames(string row, float seconds)
        {
            capDir = Path.Combine(Folder, row); Directory.CreateDirectory(capDir);
            capUntilFrame = Time.frameCount + Mathf.CeilToInt(seconds * Mathf.Max(20f, 1f / Mathf.Max(0.005f, Time.smoothDeltaTime)));
        }

        IEnumerator Still(string name)
        {
            yield return new WaitForEndOfFrame();
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            if (tex) { File.WriteAllBytes(Path.Combine(Folder, name + ".jpg"), tex.EncodeToJPG(95)); Destroy(tex); }
        }

        IEnumerator Run()
        {
            G = ZGame.I; W = G.World; rts = FindFirstObjectByType<RtsCamera>(); input = OzUnitEditor.I.Input;
            Directory.CreateDirectory(Folder);
            md.AppendLine($"# Big tanks Play check ({System.DateTime.Now:yyyy-MM-dd HH:mm}), the Unit editor, Game view {Screen.width} x {Screen.height}").AppendLine();
            md.AppendLine($"switches: Mammoth {ZWorld.MammothDeviation}, Bizon {ZWorld.BizonDeviation}, ram {ZWorld.MammothRamDeviation}, crush {ZWorld.BizonCrushDeviation}").AppendLine();
            if (rts) rts.ZoomTo(62f);
            OzUnitEditor.SetPanelsHidden(true);
            var me = G.player; var foe = me == Team.Red ? Team.Blue : Team.Red;
            string[] big = { ZBigTanks.Mammoth, ZBigTanks.Bizon };

            // ---- L: the line-up
            if (Area(out int lx, out int ly))
            {
                int x0 = lx + 4 * 16, y0 = ly + 2 * 16;
                string[] types = { "heavy", ZBigTanks.Mammoth, ZBigTanks.Bizon };
                var rows = new[] { me, foe, Team.Neutral };
                var lineup = new List<(ZObject o, string type, Team team, string how)>();
                for (int r = 0; r < 3; r++)
                    for (int c = 0; c < 3; c++)
                    {
                        if (rows[r] == Team.Neutral && c == 0) continue;
                        var o = Drop(types[c], rows[r], x0 + c * 7 * 16, y0 + r * 5 * 16, out string how);
                        if (o != null) { o.Damage = 0; lineup.Add((o, types[c], rows[r], how)); }
                    }
                yield return new WaitForSeconds(2.5f);
                Fly(x0 + 7 * 16, y0 + 5 * 16); yield return new WaitForSeconds(1.8f);
                Deselect(); yield return Still("L_lineup_heavy_mammoth_bizon_red_blue_neutral");
                foreach (var e in lineup.Where(e => e.type != "heavy"))
                {
                    string drv = e.o.Drivers.Count > 0 ? e.o.DriverType : "none";
                    bool lid = LidShown(e.o);
                    bool ok = e.team == Team.Neutral ? e.o.Drivers.Count == 0 && lid : e.o.Drivers.Count > 0 && drv == "psycho" && lid;
                    Row($"L {e.type} {e.team}", ok, $"placed by {e.how}; driver {drv} (DriverType {e.o.DriverType}); hatch lid shown {lid}; view {(G.Views.ContainsKey(e.o.Id) ? "built" : "MISSING")}");
                }
                foreach (var e in lineup) OzUePlayground.Remove(e.o);
                yield return new WaitForSeconds(1f);
            }
            else Row("L line-up", false, "no free ground");

            // ---- D: drives
            foreach (var x in big)
            {
                if (!Area(out int ox, out int oy)) { Row("D " + x, false, "no free ground"); continue; }
                int sx = ox + 4 * 16, sy = oy + 5 * 16;
                var m = Make(x, sx, sy, me); yield return new WaitForSeconds(1.5f);
                Fly(sx + 5 * 16, sy); yield return new WaitForSeconds(1.5f);
                var v = ViewOf(m); var link = Deep(v, "Link_L_000"); var spL = Deep(v, "Sprocket_L"); var spR = Deep(v, "Sprocket_R");
                int w0 = OzTrackLinks.Writes, b0 = OzBigTankExhaust.Bursts, c0 = OzBigTankExhaust.Chuffs; float t0 = Time.unscaledTime;
                Vector3 l0 = link ? link.localPosition : Vector3.zero; float spin = 0f;
                Deselect(); Frames("D_" + x + "_drive", 5f);
                W.SendWaypoints(m.Owner, m, new[] { ZWaypoint.Move(sx + 10 * 16, sy) });
                Quaternion pq = spL ? spL.localRotation : Quaternion.identity; float linkMove = 0f; Vector3 lp = l0;
                float until = Time.time + 9f;
                while (Time.time < until && (m.Waypoints.Count > 0)) { if (spL) { spin += Quaternion.Angle(pq, spL.localRotation); pq = spL.localRotation; } if (link) { linkMove += (link.localPosition - lp).magnitude; lp = link.localPosition; } yield return null; }
                yield return new WaitForSeconds(0.6f);
                // back to the start: the hull turns round (sides opposite while it turns on the spot) and drives back
                int turnOpp = 0, turnSame = 0; float yawPrev = v ? v.transform.eulerAngles.y : 0f; Vector3 posPrev = v ? v.transform.position : Vector3.zero;
                Quaternion pl = spL ? spL.localRotation : Quaternion.identity, pr = spR ? spR.localRotation : Quaternion.identity;
                Frames("D_" + x + "_turn_back", 5f);
                W.SendWaypoints(m.Owner, m, new[] { ZWaypoint.Move(sx - 2 * 16, sy + 1 * 16) });
                until = Time.time + 12f;
                while (Time.time < until && m.Waypoints.Count > 0)
                {
                    yield return null;
                    if (!v || !spL || !spR) continue;
                    float dyaw = Mathf.DeltaAngle(yawPrev, v.transform.eulerAngles.y) / Mathf.Max(1e-4f, Time.deltaTime); float sp = (v.transform.position - posPrev).magnitude / Mathf.Max(1e-4f, Time.deltaTime);
                    float aL = Signed(pl, spL.localRotation), aR = Signed(pr, spR.localRotation);
                    if (Mathf.Abs(dyaw) > 15f && sp < 0.4f && Mathf.Abs(aL) > 0.05f && Mathf.Abs(aR) > 0.05f) { if (Mathf.Sign(aL) != Mathf.Sign(aR)) turnOpp++; else turnSame++; }
                    yawPrev = v.transform.eulerAngles.y; posPrev = v.transform.position; pl = spL.localRotation; pr = spR.localRotation;
                }
                var snd = OzSoundLog.Since(t0).Select(e => e.key).Distinct().ToList();
                bool exh = x != ZBigTanks.Bizon || (OzBigTankExhaust.Bursts > b0 && OzBigTankExhaust.Chuffs > c0 && snd.Contains(OzBigTankExhaust.ChuffEvent));
                bool ok = OzTrackLinks.Writes > w0 && linkMove > 1f && spin > 90f && exh && turnOpp > turnSame;
                Row("D " + x, ok, $"link writes {OzTrackLinks.Writes - w0}, Link_L_000 moved {linkMove:0.0} m, Sprocket_L turned {spin:0} deg on 10 tiles; on-the-spot turn frames: sides opposite {turnOpp}, same {turnSame}; "
                    + (x == ZBigTanks.Bizon ? $"exhaust bursts {OzBigTankExhaust.Bursts - b0}, chuffs {OzBigTankExhaust.Chuffs - c0}; " : "") + "sounds: " + string.Join(", ", snd));
                OzUePlayground.Remove(m); yield return new WaitForSeconds(1f);
            }

            // ---- F: fire
            foreach (var x in big)
            {
                if (!Area(out int ox, out int oy)) { Row("F " + x, false, "no free ground"); continue; }
                int sx = ox + 4 * 16, sy = oy + 5 * 16;
                var m = Make(x, sx, sy, me); var t = Make("heavy", sx + 8 * 16, sy, foe); t.Damage = 0; t.MaxHealth = t.Health = 100000000;
                yield return new WaitForSeconds(1.5f);
                Fly(sx + 4 * 16, sy); yield return new WaitForSeconds(1.5f);
                var v = ViewOf(m); string X = x == ZBigTanks.Mammoth ? "Mammoth" : "Bizon";
                var recs = v ? v.GetComponentsInChildren<Transform>(true).Where(q => q.name.StartsWith(X + "Recoil")).OrderBy(q => q.name).ToArray() : new Transform[0];
                var rest = recs.Select(q => q.localPosition).ToArray(); var kicks = recs.Select(_ => new List<float>()).ToArray(); var inKick = new bool[recs.Length];
                float t0 = Time.unscaledTime, tt = Time.time;
                Deselect(); Frames("F_" + x + "_fire", 7f);
                W.SendWaypoints(m.Owner, m, new[] { new ZWaypoint { mode = WpMode.Attack, refId = t.Id, x = t.CenterX, y = t.CenterY } });
                float until = Time.time + 11f;
                while (Time.time < until)
                {
                    for (int i = 0; i < recs.Length; i++)
                    {
                        float d = recs[i] ? (recs[i].localPosition - rest[i]).magnitude : 0f;
                        if (d > 0.04f && !inKick[i]) { inKick[i] = true; kicks[i].Add(Time.time - tt); } else if (d < 0.01f) inKick[i] = false;
                    }
                    yield return null;
                }
                var snd = OzSoundLog.Since(t0).Select(e => e.key).Distinct().ToList();
                string ks = string.Join("; ", recs.Select((q, i) => q.name + " kicks at " + string.Join(" ", kicks[i].Select(a => a.ToString("0.00")))));
                bool ok;
                if (recs.Length == 2) ok = kicks[0].Count > 0 && kicks[1].Count > 0 && Mathf.Abs(kicks[0][0] - kicks[1][0]) > 0.15f;
                else ok = recs.Length == 1 && kicks[0].Count >= 2;
                Row("F " + x, ok, $"{recs.Length} barrel(s): {ks}; sounds: {string.Join(", ", snd)}");
                OzUePlayground.Remove(m); OzUePlayground.Remove(t); yield return new WaitForSeconds(1f);
            }

            // ---- N: the Bizon never backs off a far howitzer
            if (Area(out int nx, out int ny))
            {
                int sx = nx + 2 * 16, sy = ny + 5 * 16;
                var b = Make(ZBigTanks.Bizon, sx, sy, me); b.MaxHealth = b.Health = 100000000;
                var h = MakeCannon("howitzer", sx + 15 * 16, sy, foe); h.Damage = 0;
                yield return new WaitForSeconds(1.5f); Fly(sx + 8 * 16, sy); if (rts) rts.ZoomTo(70f); yield return new WaitForSeconds(1.2f);
                W.SendWaypoints(b.Owner, b, new[] { new ZWaypoint { mode = WpMode.Attack, refId = h.Id, x = h.CenterX, y = h.CenterY } });
                float until = Time.time + 60f, prev = 1e9f, worstBack = 0f, minD = 1e9f, lateral = 0f; int samples = 0; bool inRange = false;
                Deselect(); Frames("N_bizon_vs_howitzer", 3f);
                while (Time.time < until && !h.Destroyed)
                {
                    float d = Mathf.Sqrt((b.CenterX - h.CenterX) * (b.CenterX - h.CenterX) + (b.CenterY - h.CenterY) * (b.CenterY - h.CenterY));
                    if (prev < 1e8f) worstBack = Mathf.Max(worstBack, d - prev);
                    lateral = Mathf.Max(lateral, Mathf.Abs(b.CenterY - sy)); minD = Mathf.Min(minD, d); prev = d; samples++;
                    if (d <= 120f + 40f) { inRange = true; if (samples > 4 && d >= prev - 0.01f) break; }
                    yield return new WaitForSeconds(0.25f);
                }
                Row("N bizon vs a far howitzer", inRange && worstBack <= 3f, $"{samples} samples, closest {minD:0} px (range 120 + bodies), the most it ever backed off {worstBack:0.0} px, the most off its line {lateral:0} px, the howitzer destroyed {h.Destroyed}");
                OzUePlayground.Remove(b); if (!h.Destroyed) OzUePlayground.Remove(h); if (rts) rts.ZoomTo(62f); yield return new WaitForSeconds(1f);
            }

            // ---- R: the Mammoth ram, C1-C3: the Bizon crush
            foreach (var row in new[] { "R mammoth rams a heavy", "C1 bizon crushes a heavy", "C2 bizon crushes a jeep", "C3 bizon crushes a ground gun" })
            {
                if (!Area(out int ox, out int oy)) { Row(row, false, "no free ground"); continue; }
                bool ram = row.StartsWith("R"); string x = ram ? ZBigTanks.Mammoth : ZBigTanks.Bizon;
                int mx = ox + 3 * 16, my = oy + 5 * 16;
                var m = Make(x, mx, my, me);
                int ax = mx + 6 * 16 + ZPathing.Radius(m) + 20 + 26;
                ZObject t = row.Contains("gun") ? MakeCannon("gun", ax, my, foe) : Make(row.Contains("jeep") ? "jeep" : "heavy", ax, my, foe);
                t.Damage = 0; m.Damage = 0;
                yield return new WaitForSeconds(1.5f); Fly(mx + 8 * 16, my); yield return new WaitForSeconds(1.5f);
                int s0 = ram ? OzMammothRamView.Starts : OzBizonCrushView.Starts, i0 = ram ? OzMammothRamView.Impacts : OzBizonCrushView.Contacts,
                    f0 = ram ? OzMammothRamView.Flips : OzBizonCrushView.Squashes, k0 = ram ? OzMammothRamView.Landings : OzBizonCrushView.Climbs,
                    br0 = ram ? OzMammothRamView.WreckBreaks : OzBizonCrushView.WreckBreaks;
                float t0 = Time.unscaledTime;
                Deselect();
                W.SendWaypoints(me, m, new[] { ZWaypoint.Move(mx + 20 * 16, my) });
                float until = Time.time + 30f; while (Time.time < until && !m.Ramming && !t.RamDoomed) yield return null;
                Frames(row.Substring(0, 2).Trim() + "_" + x + "_" + t.Type, 9f);
                until = Time.time + 20f; while (Time.time < until && !t.RamDoomed) yield return null;
                float hitAt = Time.time, deathAt = -1f;
                while (Time.time - hitAt < 9f && deathAt < 0f) { if (t.Destroyed || t.KillAt >= 0) deathAt = Time.time - hitAt; yield return null; }
                yield return new WaitForSeconds(3f);
                var snd = OzSoundLog.Since(t0).Select(e => e.key).Distinct().ToList();
                int s = (ram ? OzMammothRamView.Starts : OzBizonCrushView.Starts) - s0, i = (ram ? OzMammothRamView.Impacts : OzBizonCrushView.Contacts) - i0,
                    f = (ram ? OzMammothRamView.Flips : OzBizonCrushView.Squashes) - f0, k = (ram ? OzMammothRamView.Landings : OzBizonCrushView.Climbs) - k0,
                    br = (ram ? OzMammothRamView.WreckBreaks : OzBizonCrushView.WreckBreaks) - br0;
                string[] need = ram ? new[] { "veh_mammoth_ram_rev", "veh_mammoth_ram" } : new[] { "veh_bizon_crush_rev", "veh_bizon_crush" };
                bool ok = t.RamDoomed && deathAt >= 0f && s > 0 && i > 0 && f > 0 && k > 0 && br > 0 && need.All(snd.Contains) && (ram || t.RamCrushed);
                Row(row, ok, $"doomed {t.RamDoomed} (crushed {t.RamCrushed}), destroyed {deathAt:0.00} s after the contact; view: starts {s}, contacts {i}, {(ram ? "flips" : "squashes")} {f}, {(ram ? "landings" : "climbs")} {k}, wreck breaks {br}; sounds: {string.Join(", ", snd)}");
                OzUePlayground.Remove(m); yield return new WaitForSeconds(1.2f);
            }

            // ---- E: EXIT DRIVER, V: voices, X: death
            foreach (var x in big)
            {
                if (!Area(out int ox, out int oy)) { Row("E " + x, false, "no free ground"); continue; }
                int sx = ox + 6 * 16, sy = oy + 5 * 16;
                var m = Make(x, sx, sy, me); yield return new WaitForSeconds(1.5f); Fly(sx, sy); yield return new WaitForSeconds(1.2f);
                // V: voices through the sound director's own select / order calls
                var ad = AudioDirector.I; float tv0 = Time.unscaledTime;
                var mSel = typeof(AudioDirector).GetMethod("SelectionVoice", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var mOrd = typeof(AudioDirector).GetMethod("OrderVoice", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                input.Selection.Clear(); input.Selection.Add(m);
                for (int rep = 0; rep < 3; rep++)
                {
                    mSel?.Invoke(ad, null); yield return new WaitForSeconds(2.6f);
                    W.SendWaypoints(m.Owner, m, new[] { ZWaypoint.Move(m.CenterX + 16, m.CenterY) }); mOrd?.Invoke(ad, new object[] { false }); yield return new WaitForSeconds(2.6f);
                    W.SendWaypoints(m.Owner, m, new[] { ZWaypoint.Move(m.CenterX - 16, m.CenterY, true) }); mOrd?.Invoke(ad, new object[] { true }); yield return new WaitForSeconds(2.6f);
                }
                var vo = OzSoundLog.Since(tv0).Where(e => e.key.StartsWith("vo_")).ToList();
                var own = vo.Where(e => e.key.Contains("_" + x)).ToList();
                Row("V " + x, own.Count >= 3, $"{vo.Count} voice events, {own.Count} the {x}'s own pools: " + string.Join("; ", vo.Select(e => $"{e.t - tv0:0.0}s {e.key} ({e.clip}, {e.outcome})")));
                // E: the Unit editor tab's click action (the same action as a click on the tab)
                OzUnitEditor.SetPanelsHidden(false);                                                         // 1.5 pass 4: EXIT DRIVER sits in the STATS box's unit card (ue_exit_placement): shown for the click
                input.Selection.Clear(); input.Selection.Add(m); yield return new WaitForSeconds(1.0f);
                var tab = OzUeDriverTab.I; bool shown = tab && tab.Button != null && tab.Button.Rt && tab.Button.Rt.gameObject.activeInHierarchy;
                float te0 = Time.unscaledTime; int drv0 = m.Drivers.Count;
                if (shown) tab.Button.OnClick?.Invoke(default(HudClick));
                OzUnitEditor.SetPanelsHidden(true);                                                          // 1.5 pass 4: clean frames again
                Deselect(); yield return new WaitForSeconds(0.25f); yield return Still("E_" + x + "_exit_0.25s");
                yield return new WaitForSeconds(0.9f); yield return Still("E_" + x + "_exit_1.15s");
                yield return new WaitForSeconds(1.6f); yield return Still("E_" + x + "_exit_2.75s");
                var outR = W.Objects.Where(o => !o.Destroyed && o.Kind == ZKind.Robot && o.Owner == me && Mathf.Abs(o.CenterX - m.CenterX) < 80 && Mathf.Abs(o.CenterY - m.CenterY) < 80).ToList();
                var es = OzSoundLog.Since(te0).Select(e => e.key).Distinct().ToList();
                bool lid = LidShown(m);
                Row("E " + x, shown && drv0 > 0 && m.Drivers.Count == 0 && outR.Count > 0 && lid,
                    $"tab shown {shown}; drivers {drv0} -> {m.Drivers.Count}; robots out beside it {outR.Count} ({string.Join(", ", outR.Select(o => o.Type))}); hatch lid shown {lid}; tank now {m.Owner}; sounds: {string.Join(", ", es)}");
                foreach (var r in outR) OzUePlayground.Remove(r);
                OzUePlayground.Remove(m); yield return new WaitForSeconds(1f);

                // X: the death
                if (!Area(out ox, out oy)) { Row("X " + x, false, "no free ground"); continue; }
                sx = ox + 6 * 16; sy = oy + 5 * 16;
                m = Make(x, sx, sy, me); yield return new WaitForSeconds(1.5f); Fly(sx, sy); yield return new WaitForSeconds(1.2f);
                int b0 = OzBigTankWreck.Breaks, p0 = OzBigTankWreck.PiecesShown, o0 = OzBigTankWreck.BarrelsOff, n0 = OzBigTankWreck.NoSet; float tx0 = Time.unscaledTime;
                Deselect(); Frames("X_" + x + "_death", 6f);
                m.SetHealth(0); W.UpdateObjectHealth(m, -1);
                float xd = Time.time, brokeAt = -1f;
                while (Time.time - xd < 45f) { if (OzBigTankWreck.Breaks > b0) { brokeAt = Time.time - xd; break; } yield return null; }
                if (brokeAt > 5.5f) Frames("X_" + x + "_break_after_burn", 4f);
                yield return new WaitForSeconds(5f);
                var xs = OzSoundLog.Since(tx0).Select(e => e.key).Distinct().ToList();
                Row("X " + x, OzBigTankWreck.Breaks > b0 && OzBigTankWreck.PiecesShown - p0 >= 16 && OzBigTankWreck.NoSet == n0,
                    $"broke {brokeAt:0.0} s after the death; breaks {OzBigTankWreck.Breaks - b0}, pieces shown {OzBigTankWreck.PiecesShown - p0}, barrels off {OzBigTankWreck.BarrelsOff - o0}, no-set {OzBigTankWreck.NoSet - n0}; sounds: {string.Join(", ", xs)}");
                yield return new WaitForSeconds(1f);
            }

            OzUnitEditor.SetPanelsHidden(false);
            md.AppendLine().AppendLine($"{pass} PASS, {fail} FAIL. Frames written {FramesWritten}: {Folder}");
            Report = md.ToString();
            File.WriteAllText(Path.Combine(Folder, "play_report.md"), Report);
            Debug.Log(Tag + "\n" + Report);
            yield return new WaitForSeconds(1.5f);
            Destroy(this);
        }

        // ---- helpers
        ZObject Drop(string type, Team team, int cx, int cy, out string how)
        {
            var d = OzUePlayground.Drop(type, team, ZGame.PxToWorld(cx, cy), 1);
            if (d.ok && d.made != null && d.made.Count > 0) { how = "the Unit editor's drop"; return d.made[0]; }
            how = "CreateObject (the drop said: " + (d.why ?? "nothing") + ")";
            return Make(type, cx, cy, team);
        }

        ZObject Make(string type, int cx, int cy, Team t)
        {
            var o = W.CreateObject("vehicle", type, cx - 16, cy - 16, t); W.Emit(ZEventType.Created, o); made.Add(o); Changed(); return o;
        }

        ZObject MakeCannon(string type, int cx, int cy, Team t)
        {
            var o = W.CreateObject("cannon", type, cx - 16, cy - 16, t); W.Emit(ZEventType.Created, o); made.Add(o); Changed(); return o;
        }

        static void Changed() { typeof(OzUePlayground).GetMethod("MarkChanged", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)?.Invoke(null, null); }

        UnitView ViewOf(ZObject o) => o != null && G.Views.TryGetValue(o.Id, out var v) ? v : null;

        static Transform Deep(Component c, string n) => c ? c.GetComponentsInChildren<Transform>(true).FirstOrDefault(q => q.name == n) : null;

        bool LidShown(ZObject o)
        {
            var v = ViewOf(o); var lid = Deep(v, o.Type + "_v2_HatchLid");
            return lid && lid.gameObject.activeInHierarchy && lid.GetComponentsInChildren<Renderer>(true).Any(r => r.enabled && r.gameObject.activeInHierarchy);
        }

        static float Signed(Quaternion a, Quaternion b) { var d = b * Quaternion.Inverse(a); d.ToAngleAxis(out float ang, out Vector3 ax); if (ang > 180f) ang -= 360f; return ang * Mathf.Sign(Vector3.Dot(ax, Vector3.right) + 1e-6f); }

        void Fly(int px, int py) { if (rts) rts.FlyTo(ZGame.PxToWorld(px, py)); }

        void Deselect() { if (input) input.Selection.Clear(); }

        bool Area(out int ox, out int oy)
        {
            ox = oy = 0;
            var f = rts ? rts.focus : Vector3.zero;
            int cx = Mathf.RoundToInt(f.x * 8f) >> 4, cy = Mathf.RoundToInt(-f.z * 8f) >> 4;
            for (int r = 0; r < 50; r += 2)
                for (int dy = -r; dy <= r; dy += 2)
                    for (int dx = -r; dx <= r; dx += 2)
                    {
                        int tx = cx + dx - 14, ty = cy + dy - 6;
                        if (Free(tx, ty, 28, 13)) { ox = tx * 16; oy = ty * 16; return true; }
                    }
            return false;
        }

        bool Free(int tx, int ty, int w, int h)
        {
            for (int j = ty; j < ty + h; j++)
                for (int i = tx; i < tx + w; i++)
                {
                    if (!W.Path.TilePassableGrid(i, j, false, false)) return false;
                    var k = W.Map.ClassAt(i, j); if (k != ZClass.Ground && k != ZClass.Road) return false;
                }
            int x0 = tx * 16 - 32, y0 = ty * 16 - 32, x1 = (tx + w) * 16 + 32, y1 = (ty + h) * 16 + 32;
            if (W.Pathing != null) foreach (var L in W.Pathing.Lanes) if (L.ZX1 > x0 && L.ZX0 < x1 && L.ZY1 > y0 && L.ZY0 < y1) return false;
            foreach (var o in W.Objects) if (!o.Destroyed && o.CenterX > x0 && o.CenterX < x1 && o.CenterY > y0 && o.CenterY < y1 && (o.IsUnit || o.Kind == ZKind.Building)) return false;
            return true;
        }

        void Row(string row, bool ok, string what)
        {
            md.AppendLine($"- {(ok ? "PASS" : "FAIL")} {row}: {what}");
            if (ok) pass++; else fail++;
        }
    }
}
#endif
