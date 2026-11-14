#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OpenZ
{
    /// <summary>
    /// 1.2 item 111 Play proof (rule DV144, Sim/ZWorld.SideShuttles.cs; menu OpenZ/V1.2/Shuttle: 111 Both Sides Proof, Editor/OzSideShuttleMenu.cs).
    /// Started on the MainMenu scene in Play it deploys four levels itself as the player does (the battlefield screen's DEPLOY), MEDIUM, the
    /// SHUTTLE box ON, HEAD START ON, Commander Zod's start briefing off for the run (oz_zod_briefing 0; put back after): level 16 red, level
    /// 16 blue, level 17 red, level 17 blue, and survives the scene loads. On the battlefield screen first:
    ///  M  the SHUTTLE block offered to the side ("ON / PARKED BY YOUR FORT"), the key U turns it off and on again; a picture.
    /// In each level, once the start is over:
    ///  S0 the side and the rules as the level started (the human's team, DV144 on, the box's answer);
    ///  S1 the player's ship on its own spot by his own fort (red: level 16's pocket / DV134's fort spot; blue: DV144's spots), crewed, his
    ///     team, the level's playable shuttle, drawn and lit; with blue on level 16 the red fort's pocket empty (no rules object, the scene's
    ///     ship hidden); on level 17 the two EMPTY car-park ships on their spots, neutral, drawn, dark;
    ///  S2 the room round every ship with the lists the level handed the rules (the drawn props of ZWorld.ParkingKeepOut, the plant crowns of
    ///     ZWorld.ShuttleSetKeepOut): vehicles and guns 4 m, robots 3 m, buildings 2 m, flags 4 m, items 1.5 m, drawn props 1.5 m, the ramp
    ///     lane, no exit lane, no gate face, the landing spot rule with the plants, nobody within 2 m (ZWorld.ShuttleStartRoom);
    ///  S3 no drawn thing of the scene inside a ship (every renderer whose box comes within 0.3 m of the outline: lamps, bollards, boulders,
    ///     plants, rocks, details; not the ships, not the units, not the ground) and the ground under each ship nearly flat (the terrain's
    ///     height over the outline: at most <see cref="MaxGroundSpreadM"/> m from its lowest to its highest point);
    ///  S4 the ramp foot on the robots' ground of the player's fort (the rules' reach map: the same robot region as the fort's gate);
    ///  S5 HEAD START: every extra vehicle placed, 4 m from every ship's outline and off every ramp lane;
    ///  pictures, full screen, the normal RTS camera at 70 m: the start view as the game gives it, the player's ship (and the head start
    ///  beside it), and on level 17 each car-park ship and the car park between them.
    /// The player's settings are put back at the end (the side, the box, the difficulty, HEAD START, the briefing). Counted PASS / FAIL lines
    /// and the pictures in <see cref="Folder"/>. Test only (editor).
    /// </summary>
    public sealed class OzSideShuttleProof : MonoBehaviour
    {
        public static string Report = "idle";
        public static OzSideShuttleProof I { get; private set; }
        public const float CamHeight = 70f, MaxGroundSpreadM = 1.0f;
        public static string Folder()
        {
            var d = new System.IO.DirectoryInfo(Application.dataPath);
            for (int i = 0; i < 8 && d != null; i++, d = d.Parent)
                if (System.IO.Directory.Exists(System.IO.Path.Combine(d.FullName, "work", "records")))
                    return System.IO.Path.Combine(d.FullName, "work", "records", "milestones", "blue_shuttle_k82");
            return System.IO.Path.Combine(Application.persistentDataPath, "blue_shuttle_k82");
        }
        public static void Run()
        {
            if (I) { Debug.LogWarning("[OzSideShuttleProof] already running"); return; }
            Report = "running"; var go = new GameObject("OzSideShuttleProof"); DontDestroyOnLoad(go); go.AddComponent<OzSideShuttleProof>();
        }

        static readonly (string lv, bool blue)[] Cases = { ("L16", false), ("L16", true), ("L17", false), ("L17", true) };
        readonly StringBuilder sb = new StringBuilder(); int fails, passes; string dir;
        bool kept, putBack, boxWas, shuttleWas, headWas, briefHad; GameDifficulty diffWas; int briefWas;
        ZGame g; ZWorld W; RtsCamera rts;

        void Awake() { I = this; }
        void OnDestroy() { if (I == this) I = null; if (kept && !putBack) PutBack("stopped before its end"); }
        void Start() { dir = Folder(); System.IO.Directory.CreateDirectory(dir); StartCoroutine(All()); }

        void Log(string t) { sb.AppendLine(t); Debug.Log("[OzSideShuttleProof] " + t); }
        void Check(string what, bool ok) { if (ok) passes++; else fails++; Log((ok ? "PASS " : "FAIL ") + what); }
        static IEnumerator Real(float s) { float t = Time.realtimeSinceStartup + s; while (Time.realtimeSinceStartup < t) yield return null; }
        static IEnumerator Until(System.Func<bool> c, float max) { float t = Time.realtimeSinceStartup + max; while (Time.realtimeSinceStartup < t && !c()) yield return null; }
        IEnumerator Shot(string name)
        {
            string p = System.IO.Path.Combine(dir, name + ".png");
            ScreenCapture.CaptureScreenshot(p); Log("   picture " + name + ".png");
            yield return OzProofPictures.AfterGrab(); yield return OzProofPictures.Written(p);
        }
        void Cam(Vector3 focus) { if (!rts) return; rts.FreeLookOff(); rts.focus = new Vector3(focus.x, 0f, focus.z); rts.SetHeightImmediate(CamHeight); }
        static string F(double v) => v.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
        static bool AtSpot(ZObject s, int[] spot) => s != null && s.CenterX == spot[0] * 16 + 8 && s.CenterY == spot[1] * 16 + 8 && Mathf.Abs((float)ZObject.ShuttleNormDeg(s.ShuttleHeadingDeg - spot[2])) < 0.01f;
        static string Where(ZObject s) => s == null ? "none" : $"#{s.Id} {s.Owner} tile {s.CenterX >> 4},{s.CenterY >> 4} heading {s.ShuttleHeadingDeg:0}, pilots {s.Drivers.Count}, aboard {s.Passengers.Count}";

        void Keep()
        {
            boxWas = OzPlayBlue.Box; shuttleWas = OzShuttleStart.On; headWas = OpenZDifficulty.HeadStartToggle; diffWas = OpenZDifficulty.Level;
            briefHad = PlayerPrefs.HasKey(ZodBriefing.PrefBriefing); briefWas = PlayerPrefs.GetInt(ZodBriefing.PrefBriefing, 1); kept = true;
        }
        void PutBack(string why)
        {
            OzPlayBlue.Box = boxWas; OzShuttleStart.On = shuttleWas; OpenZDifficulty.HeadStartToggle = headWas; OpenZDifficulty.Level = diffWas;
            if (briefHad) PlayerPrefs.SetInt(ZodBriefing.PrefBriefing, briefWas); else PlayerPrefs.DeleteKey(ZodBriefing.PrefBriefing);
            PlayerPrefs.Save(); putBack = true;
            Debug.Log($"[OzSideShuttleProof] the player's settings put back ({why}): side {(boxWas ? "blue" : "red")}, SHUTTLE {(shuttleWas ? "ON" : "OFF")}, HEAD START {(headWas ? "ON" : "OFF")}, {OpenZDifficulty.Label(diffWas)}, briefing {(briefHad ? briefWas.ToString() : "default")}");
        }

        IEnumerator All()
        {
            var menu = FindFirstObjectByType<OpenZMainMenu>();
            if (!menu || SceneManager.GetActiveScene().name != OzPlayBlue.MenuScene) { Check("started on the MainMenu scene in Play", false); Finish(); yield break; }
            Keep();
            Log($"1.2 item 111 (DV144) both sides proof, {Screen.width}x{Screen.height}");
            foreach (var (lv, blue) in Cases)
            {
                yield return Until(() => FindFirstObjectByType<OpenZMainMenu>() && SceneManager.GetActiveScene().name == OzPlayBlue.MenuScene, 60f);
                menu = FindFirstObjectByType<OpenZMainMenu>();
                if (!menu) { Check($"back on the main menu before {lv} {(blue ? "blue" : "red")}", false); break; }
                yield return Real(1.5f);
                OzPlayBlue.Box = blue; OzShuttleStart.On = true; OpenZDifficulty.HeadStartToggle = true; OpenZDifficulty.Level = GameDifficulty.Medium;
                PlayerPrefs.SetInt(ZodBriefing.PrefBriefing, 0); PlayerPrefs.Save();
                int card = System.Array.FindIndex(OzLevelDef.All, d => d.Id == lv);
                if (card < 0) { Check($"{lv}: a level card on the battlefield screen", false); continue; }
                menu.ProofGoSelect(card); yield return Real(1.0f);
                // ---- M the battlefield screen: the SHUTTLE block offered to this side, the key U off and on again
                bool shown = menu.ProofShuttleShown; string t1 = menu.ProofShuttleText;
                menu.ProofToggleShuttle(true); yield return Real(0.4f); string t2 = menu.ProofShuttleText; bool off = !OzShuttleStart.On;
                menu.ProofToggleShuttle(true); yield return Real(0.4f); string t3 = menu.ProofShuttleText; bool onAgain = OzShuttleStart.On;
                Check($"M {lv} {(blue ? "BLUE" : "RED")}: the battlefield screen's SHUTTLE block offered (\"{t1}\"); the key U turns it off (\"{t2}\") and on again (\"{t3}\")",
                      shown && t1.Contains(OzShuttleStart.HintOn) && off && t2.Contains(OzShuttleStart.HintOffFor(lv)) && onAgain && t3.Contains(OzShuttleStart.HintOn));
                yield return Shot($"{lv}_{(blue ? "blue" : "red")}_0_battlefield_screen");
                menu.ProofDeploy();
                Log($"{lv} {(blue ? "BLUE" : "RED")}: DEPLOY (MEDIUM, SHUTTLE ON, HEAD START ON)");
                yield return Until(() => ZGame.I && ZGame.I.World != null && SceneManager.GetActiveScene().name != OzPlayBlue.MenuScene, 120f);
                if (!ZGame.I || ZGame.I.World == null) { Check($"{lv}: the level loaded after DEPLOY", false); break; }
                yield return Level(lv, blue);
                OzScenes.MainMenu();
            }
            PutBack("the end");
            Finish();
        }

        void Finish()
        {
            Report = $"[OzSideShuttleProof] {(fails == 0 && passes > 0 ? "PASS" : "FAIL")} {passes}/{passes + fails} checks; pictures in {dir}";
            Log(Report);
            try { System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "checks.txt"), sb.ToString()); } catch (System.Exception ex) { Debug.LogWarning("[OzSideShuttleProof] " + ex.Message); }
            Destroy(gameObject);
        }

        IEnumerator Level(string lv, bool blue)
        {
            yield return Until(() => OpenZHud.I && Time.timeSinceLevelLoad > 4f && !ZodBriefing.BriefingActive, 150f);
            yield return Real(1.5f);
            g = ZGame.I; W = g.World; rts = FindFirstObjectByType<RtsCamera>();
            string tag = $"{lv}_{(blue ? "blue" : "red")}";
            var human = blue ? Team.Blue : Team.Red;
            if (lv == "L16") yield return Until(() => W.ShuttleKeepOutCount > 0, 8f);                         // the plants are handed to the rules in the first frames
            Log($"{tag}: {OzPlayBlueLevel.Status()}");
            Log($"{tag}: rule {ZWorld.SideShuttlesDeviation}, shuttle rule {ZWorld.ShuttleOn}, level 17 box answer {ZWorld.L17StartShuttle}, made {W.SideShuttlesMade}, moved {W.SideShuttlesMoved} {W.SideShuttleWhy}, "
                + $"drawn props known {(ZWorld.ParkingKeepOutMap == W.Map.file ? ZWorld.ParkingKeepOut.Count : 0)}, plant crowns known {W.ShuttleKeepOutCount}");
            // ---- S0
            Check($"S0 {tag}: the human commands {human} (ZGame.player {g.player}, PLAY AS BLUE {OzPlayBlue.Active}), DV144 on, the box asked for a ship ({OpenZDifficulty.Label(g.gameDifficulty)}, SHUTTLE {(OzShuttleStart.On ? "ON" : "OFF")})",
                  g.player == human && OzPlayBlue.Active == blue && ZWorld.SideShuttlesDeviation && ZWorld.ShuttlePlayableDeviation && ZWorld.ShuttleOn && g.gameDifficulty == GameDifficulty.Medium && OzShuttleStart.On);
            // ---- S1 the player's ship
            ZObject own; int[] spot = null;
            if (blue) { own = W.BlueStartShuttle; spot = lv == "L16" ? ZWorld.L16BlueFortShuttleSpot : ZWorld.L17BlueFortShuttleSpot; }
            else if (lv == "L16") own = W.PlayableShuttle;
            else { own = W.L17FortShuttle; spot = ZWorld.L17FortShuttleSpot; }
            var ov = OzPlayableShuttle.Of(own); var ol = ov ? ov.GetComponent<OzShuttleEmptyLook>() : null;
            bool onSpot = spot == null ? own != null : AtSpot(own, spot);
            Check($"S1 {tag}: the player's ship by his fort: {Where(own)}{(spot != null ? $" (its spot {spot[0]},{spot[1]} heading {spot[2]})" : " (level 16's pocket)")}, the level's playable shuttle {(W.PlayableShuttle == own)}, drawn {(ov ? ov.name : "NO")}, dark {(ol ? ol.IsDark.ToString() : "no look")}",
                  own != null && onSpot && own.Owner == human && own.Drivers.Count == 1 && W.PlayableShuttle == own && ov && ov.gameObject.activeInHierarchy && !(ol && ol.IsDark));
            if (lv == "L16")
            {
                var deco = FindFirstObjectByType<L16Shuttle>(FindObjectsInactive.Include);
                if (blue) Check($"S1 {tag}: the red fort's pocket empty: no rules object there (decoration {(W.L16Shuttle != null ? "PRESENT" : "none")}, pocket off {W.L16ShuttleOptionOff}), the scene's ship {(deco ? (deco.gameObject.activeInHierarchy ? "SHOWN" : "hidden") : "not in the scene")}",
                                W.L16Shuttle == null && W.L16ShuttleOptionOff && (!deco || !deco.gameObject.activeInHierarchy) && W.Objects.Count(o => o.IsShuttle && !o.Destroyed) == 1);
                else Check($"S1 {tag}: the scene's ship draws the player's ship in the pocket ({(deco ? deco.name : "none")}), no ship by the blue fort ({Where(W.BlueStartShuttle)})", W.BlueStartShuttle == null && ov && deco && ov.gameObject == deco.gameObject);
            }
            var ships = new List<ZObject> { own };
            if (lv == "L17")
            {
                var e = W.L17CarParkShuttle; var wv = W.L17BlueCarParkShuttle;
                var ev = OzPlayableShuttle.Of(e); var wvv = OzPlayableShuttle.Of(wv);
                var el = ev ? ev.GetComponent<OzShuttleEmptyLook>() : null; var wl = wvv ? wvv.GetComponent<OzShuttleEmptyLook>() : null;
                Check($"S1 {tag}: the two EMPTY car-park ships: the red side's {Where(e)} (dark {(el ? el.IsDark.ToString() : "no look")}), the blue side's {Where(wv)} (dark {(wl ? wl.IsDark.ToString() : "no look")}); "
                      + $"by the other fort: {(blue ? Where(W.L17FortShuttle) : Where(W.BlueStartShuttle))}",
                      AtSpot(e, ZWorld.L17CarParkShuttleSpot) && AtSpot(wv, ZWorld.L17BlueCarParkShuttleSpot) && e.Owner == Team.Neutral && wv.Owner == Team.Neutral && e.Drivers.Count == 0 && wv.Drivers.Count == 0
                      && ev && wvv && el && el.IsDark && wl && wl.IsDark && (blue ? W.L17FortShuttle == null : W.BlueStartShuttle == null) && W.Objects.Count(o => o.IsShuttle && !o.Destroyed) == 3);
                ships.Add(e); ships.Add(wv);
            }
            ships = ships.Where(s => s != null).ToList();
            // ---- S2 the room with the level's own lists
            foreach (var s in ships)
            {
                string bad = W.ShuttleStartRoom(s, out string near);
                Check($"S2 {tag}: the room round {Where(s)}: {near}{(bad.Length > 0 ? "; TOO NEAR:" + bad : "")}", bad.Length == 0);
            }
            // ---- S3 no drawn thing inside, the ground nearly flat
            foreach (var s in ships)
            {
                int n = PropsInside(s, out string nearest, out string names);
                float lo = 999f, hi = -999f; var c = g.CenterWorldSmooth(s);
                for (float dz = -4.5f; dz <= 4.5f; dz += 0.5f)
                    for (float dx = -4.5f; dx <= 4.5f; dx += 0.5f)
                    {
                        float x = c.x + dx, z = c.z + dz;
                        if (s.ShuttleOutlineGapM(x * 8.0, -z * 8.0) > 0) continue;
                        float y = g.GroundY(x, z); lo = Mathf.Min(lo, y); hi = Mathf.Max(hi, y);
                    }
                Check($"S3 {tag}: {Where(s)}: no drawn thing inside (nearest {nearest}){(n > 0 ? "; INSIDE: " + names : "")}; the ground under it {F(hi - lo)} m from its lowest to its highest point (at most {F(MaxGroundSpreadM)})",
                      n == 0 && hi >= lo && hi - lo <= MaxGroundSpreadM);
            }
            // ---- S4 the crew's ground
            {
                var fort = W.Objects.FirstOrDefault(o => o.Kind == ZKind.Building && o.IsFort && o.Owner == human && !o.Destroyed && (o.Type == "fort_front" || o.Type == "fort_back"));
                int gateComp = -1, footComp = -2;
                if (fort != null && fort.GetCreationMovePoint(out int ex, out int ey) && own != null)
                {
                    double hd = own.ShuttleHeadingDeg * System.Math.PI / 180.0;
                    int fx = (int)System.Math.Round(own.CenterX - System.Math.Cos(hd) * ZWorld.ShuttleBackM * 8.0), fy = (int)System.Math.Round(own.CenterY - System.Math.Sin(hd) * ZWorld.ShuttleBackM * 8.0);
                    gateComp = W.Pathing.Reach.Comp(ex >> 4, ey >> 4, true, false); footComp = W.Pathing.Reach.Comp(fx >> 4, fy >> 4, true, false);
                }
                Check($"S4 {tag}: the player's ship's ramp foot is on the robots' ground of his fort's gate (robot region {footComp} / the gate's {gateComp})", gateComp >= 0 && gateComp == footComp);
            }
            // ---- S5 HEAD START
            {
                var head = W.Objects.Where(o => o.Kind == ZKind.Vehicle && o.Source == null && !o.IsShuttle).ToList();
                int want = lv == "L16" ? ZGame.HeadStartL16Types.Length : ZGame.EasyL17Types.Length;
                var bad = new StringBuilder(); var where = new StringBuilder();
                foreach (var v in head)
                {
                    where.Append($" {v.Type}@{v.LocX >> 4},{v.LocY >> 4}");
                    foreach (var s in W.Objects.Where(o => o.IsShuttle && !o.Destroyed))
                    {
                        ZWorld.ShuttleBoxGaps(s, v.LocX, v.LocY, v.LocX + v.WidthPix, v.LocY + v.HeightPix, out double gap, out double lane);
                        if (gap < ZWorld.HeadStartShuttleClearM || lane < ZWorld.HeadStartLaneClearM) bad.Append($" {v.Type}#{v.Id} {F(gap)} m / lane {F(lane)} m from #{s.Id};");
                    }
                }
                Check($"S5 {tag}: HEAD START {head.Count} of {want} placed ({where.ToString().Trim()}; {W.HeadStartShuttleRefusals} places refused for a shuttle), each 4 m from every ship's outline and off every ramp lane{(bad.Length > 0 ? ";" + bad : "")}",
                      head.Count == want && bad.Length == 0);
            }
            // ---- pictures
            yield return Shot($"{tag}_1_start_view");
            if (own != null) { Cam(g.CenterWorldSmooth(own)); yield return Real(0.8f); yield return Shot($"{tag}_2_player_ship_70m"); }
            if (lv == "L17" && W.L17CarParkShuttle != null && W.L17BlueCarParkShuttle != null)
            {
                Vector3 a = g.CenterWorldSmooth(W.L17BlueCarParkShuttle), b = g.CenterWorldSmooth(W.L17CarParkShuttle);
                Cam(a); yield return Real(0.8f); yield return Shot($"{tag}_3_car_park_blue_side_ship_70m");
                Cam(b); yield return Real(0.8f); yield return Shot($"{tag}_4_car_park_red_side_ship_70m");
                Cam((a + b) * 0.5f); yield return Real(0.8f); yield return Shot($"{tag}_5_car_park_both_70m");
            }
        }

        /// <summary>S3: renderers of the scene whose box comes within 0.3 m of the ship's outline (not the ships', not a unit's, not the ground).</summary>
        int PropsInside(ZObject s, out string nearest, out string names)
        {
            int n = 0; float best = 99f; nearest = "none"; var nm = new List<string>();
            var c = g.CenterWorldSmooth(s); float gy = g.GroundY(c.x, c.z);
            foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (!r || !r.enabled || !r.gameObject.activeInHierarchy || r is ParticleSystemRenderer || r is LineRenderer || r is TrailRenderer) continue;
                if (OzPlayableShuttle.OfRenderer(r) || r.GetComponentInParent<UnitView>()) continue;
                var b = r.bounds;
                if (b.size.x > 12f || b.size.z > 12f || b.max.y < gy + 0.15f || b.min.y > gy + 6f) continue;
                if (Mathf.Abs(b.center.x - c.x) > 14f || Mathf.Abs(b.center.z - c.z) > 14f) continue;
                float gmin = 99f;
                for (int i = 0; i <= 4; i++)
                    for (int j = 0; j <= 4; j++)
                    {
                        float x = Mathf.Lerp(b.min.x, b.max.x, i / 4f), z = Mathf.Lerp(b.min.z, b.max.z, j / 4f);
                        gmin = Mathf.Min(gmin, (float)s.ShuttleOutlineGapM(x * 8.0, -z * 8.0));
                    }
                if (gmin < best) { best = gmin; nearest = r.name + " " + F(gmin) + " m"; }
                if (gmin < 0.3f) { n++; if (nm.Count < 6) nm.Add(r.name + " (" + F(gmin) + " m)"); }
            }
            names = string.Join(", ", nm);
            return n;
        }
    }
}
#endif
