#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.5 ue_structure_destruction: the counted Play check (the Unit editor open; hands off ~70 s). The report and the dense frames (every
    /// 0.1 s over the first 3 s of each collapse, then +4.5 s and +9 s, at the Unit editor's own camera) go to <see cref="Folder"/>.
    ///  S1 TOWER with a flag on its deck and a grunt + a light tank on it: the body swapped for its own pieces (slabs, panels, kerb, chunks),
    ///     the old stone burst not used, released in order, the first heavy landing = the crash, the fog dust, the sounds, the kick on screen;
    ///     the flag mid-fall at +0.45 s, on the floor at +2.5 s; after 9 s the pieces at rest, none under the floor, the pile low;
    ///  S2 PLATFORM (medium) the same, smaller;
    ///  S3 OBSTACLE: the hologram shatters into its panels, every panel gone by +7 s, no dust;
    ///  S4 a PLATFORM dropped on the tower's rubble: the rubble in its footprint sinks;
    ///  S5 CLEAR FIELD: no structure piece left;
    ///  S6 the switch off: the 1.3 fall exactly (no pieces).
    /// </summary>
    public sealed class OzUeStructureBreakCheck : MonoBehaviour
    {
        const string Tag = "[OpenZ StructureBreakCheck]";
        public static string Report = "not run";
        public static string Folder;
        public static bool Running => run;
        static OzUeStructureBreakCheck run;
        int pass, fail;
        readonly StringBuilder lines = new StringBuilder();
        readonly List<string> pictures = new List<string>();
        OzUnitEditor ed; ZGame g; ZWorld w;

        public static void Begin()
        {
            if (run) { Debug.LogWarning(Tag + " a run is still going"); return; }
            if (!Application.isPlaying || !OzUnitEditor.UiBuilt || OzUnitEditor.I == null || OzUnitEditor.I.World == null) { Debug.LogWarning(Tag + " enter Play and open the unit editor first"); return; }
            var go = new GameObject("OzUe_StructureBreakCheck"); DontDestroyOnLoad(go);
            run = go.AddComponent<OzUeStructureBreakCheck>();
        }

        void Start() { StartCoroutine(Main()); }
        void OnDestroy() { if (run == this) run = null; }

        void Check(bool ok, string what) { if (ok) pass++; else fail++; lines.Append(ok ? "PASS  " : "FAIL  ").AppendLine(what); }
        void Info(string what) { lines.Append("INFO  ").AppendLine(what); }
        static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }
        static IEnumerator Seconds(float s) { float t0 = Time.realtimeSinceStartup; while (Time.realtimeSinceStartup - t0 < s) yield return null; }
        static ZObject Lead(UeDrop d) => d.ok && d.made != null && d.made.Count > 0 ? d.made[0] : null;
        static Vector3 Deck(ZWorld.PlatformInfo p, float across, float along) => OzUePlatformViews.Point(p, across, along, (float)p.HeightAt(along));
        static Transform FlagRoot(ZObject f) { var go = f != null ? GameObject.Find("Flag_" + f.Id) : null; return go ? go.transform : null; }

        void Shot(string name)
        {
            if (string.IsNullOrEmpty(Folder)) return;
            Directory.CreateDirectory(Folder);
            string b = name + "_" + Screen.width + "x" + Screen.height, path = Path.Combine(Folder, b + ".png");
            int n = 1; while (File.Exists(path)) path = Path.Combine(Folder, b + "_" + (++n) + ".png");
            ScreenCapture.CaptureScreenshot(path);
            pictures.Add(path);
        }

        /// <summary>Dense frames of one collapse: every 0.1 s for 3 s, then two late ones.</summary>
        IEnumerator Dense(string name, float flagCheckAt, System.Action atFlag)
        {
            float t0 = Time.realtimeSinceStartup; int k = 0; bool flagDone = false;
            while (Time.realtimeSinceStartup - t0 < 3f)
            {
                float tt = Time.realtimeSinceStartup - t0;
                if (!flagDone && tt >= flagCheckAt) { flagDone = true; atFlag?.Invoke(); }
                if (tt >= k * 0.1f) { Shot(name + "_t" + (k * 100).ToString("0000") + "ms"); k++; }
                yield return null;
            }
            yield return Seconds(1.5f); Shot(name + "_t4500ms");
        }

        static int CountSince(float t0, string key) { int n = 0; string rk = RouteKey(key); foreach (var e in OzSoundLog.Since(t0)) if ((e.key == key || (rk != null && e.key == rk && e.src == "CA")) && e.outcome != null && !e.outcome.StartsWith("DROP")) n++; return n; }
        /// <summary>1.5 last_small_fixes: AudioDirector forwards these events to CombatAudio (its Combat table), which logs the play under the
        /// route key, not the event id (the check counted 0 for collapses that played).</summary>
        static string RouteKey(string ev) => ev == "dst_collapse_large" || ev == "dst_collapse_medium" ? "collapse" : ev == "dst_debris_settle" ? "settle" : null;

        IEnumerator Collapse(string what, ZWorld.PlatformInfo p, ZObject flag)
        {
            var v = OzUePlatformViews.Of(p.O);
            var body = v ? v.transform.Find("Body") : null;
            int c0 = OzUeStructureBreak.Collapses, made0 = OzUeStructureBreak.PiecesMade, old0 = OzUeStructureBreak.OldPath, land0 = OzUeStructureBreak.Landings, kick0 = OzUeStructureBreak.Kicks, fog0 = OzUeStructureBreak.FogPuffs;
            int s0 = OzUeStructureBreak.SlabsMade, w0 = OzUeStructureBreak.WallPanelsMade, k0 = OzUeStructureBreak.KerbsMade, ch0 = OzUeStructureBreak.ChunksMade;
            float t0 = Time.unscaledTime;
            float flagY0 = FlagRoot(flag) ? FlagRoot(flag).position.y : -1f, flagMid = -1f;
            OzUeControls.CentreOn(p.O != null ? OzUePlatformViews.Point(p, 5f, p.Rows * 0.5f, 0f) : Vector3.zero);
            yield return Seconds(0.6f);
            Shot(what + "_before");
            p.O.SetHealth(0); w.UpdateObjectHealth(p.O);
            yield return null;
            Check(OzUeStructureBreak.Collapses == c0 + 1 && OzUeStructureBreak.OldPath == old0, what + ": collapses into its pieces (the old sink and stone burst not used)");
            Check(!body || !body.gameObject.activeInHierarchy, what + ": the standing body hidden the same frame");
            int slabs = OzUeStructureBreak.SlabsMade - s0, walls = OzUeStructureBreak.WallPanelsMade - w0, kerbs = OzUeStructureBreak.KerbsMade - k0, chunks = OzUeStructureBreak.ChunksMade - ch0;
            Check(slabs >= 12 && walls >= 10 && kerbs >= 6, what + ": its own pieces: " + slabs + " deck / ramp slabs, " + walls + " wall panels, " + kerbs + " kerb lengths, " + chunks + " library chunks (" + (OzUeStructureBreak.PiecesMade - made0) + " in all), built in " + OzUeStructureBreak.LastBuildMs.ToString("0.0") + " ms");
            yield return Dense(what, 0.45f, () => { var r = FlagRoot(flag); flagMid = r ? r.position.y : -1f; });
            Check(OzUeStructureBreak.Landings > land0, what + ": pieces landed: " + (OzUeStructureBreak.Landings - land0));
            Check(OzUeStructureBreak.FogPuffs - fog0 >= 40, what + ": the rolling fog dust: " + (OzUeStructureBreak.FogPuffs - fog0) + " puffs");
            Info(what + ": camera kicks " + (OzUeStructureBreak.Kicks - kick0) + " (on screen only)");
            if (flag != null)
            {
                var r = FlagRoot(flag); float y = r ? r.position.y : 99f, floor = r ? g.GroundY(r.position.x, r.position.z) : -99f;
                Check(flagMid > 0.3f && flagMid < flagY0 - 0.1f, what + ": the flag comes down with the deck: " + flagY0.ToString("0.00") + " m on the deck, " + flagMid.ToString("0.00") + " m at +0.45 s");
                Check(r && Mathf.Abs(y - (floor - 0.05f)) < 0.1f && !flag.Destroyed, what + ": the flag on the floor at +4.5 s: foot " + y.ToString("0.00") + " m, floor " + floor.ToString("0.00") + " m, owner " + flag.Owner);
            }
            yield return Seconds(4.5f); Shot(what + "_t9000ms");
            var log = new List<string>();
            foreach (var key in new[] { "dst_structure_groan", "dst_rock_chunks", "dst_l16_pyramid_collapse", "dst_rock_crash_large", p.Kind == ZWorld.PlatformHigh ? "dst_collapse_large" : "dst_collapse_medium", "dst_debris_concrete", "dst_debris_settle" })
            {
                int n = CountSince(t0, key); log.Add(key + " " + n);
                if (key != "dst_debris_concrete") Check(n >= 1, what + ": sound " + key + " " + n);
            }
            Info(what + ": sounds " + string.Join(", ", log) + "; metal " + CountSince(t0, "dst_debris_metal"));
            int resting = 0, all = 0, under = 0; float topY = 0f;
            foreach (var pc in FindObjectsByType<OzUeStructurePiece>(FindObjectsSortMode.None))
            {
                if (!pc.gameObject.activeInHierarchy || pc.Sinking) continue;
                all++; if (pc.Resting) resting++;
                var y = pc.transform.position.y; if (y < -0.2f) under++; topY = Mathf.Max(topY, y);
            }
            Check(all > 0 && resting >= all * 0.85f, what + ": at rest after 9 s: " + resting + " of " + all);
            Check(under == 0, what + ": no piece under the floor: " + under);
            Check(topY < (p.Kind == ZWorld.PlatformHigh ? 3.2f : 2.4f), what + ": the rubble pile is low: highest piece middle " + topY.ToString("0.00") + " m");
        }

        IEnumerator Main()
        {
            Report = "running";
            ed = OzUnitEditor.I; g = ed.Game; w = ed.World;
            bool playing0 = OzUeControls.Playing; float speed0 = OzUeControls.Speed; bool on0 = OzUeStructureBreak.On;
            OzUeStructureBreak.On = true;
            OzUePlace.Stop(); OzUePlayground.ClearField(); OzUePick.ClearInspect();
            OzUeControls.SetSpeed(1f); OzUeControls.SetPlaying(true);
            Vector3 c = ed.Rts ? ed.Rts.focus : new Vector3(160f, 0f, -100f); c.y = 0f;
            OzUeControls.CentreOn(c); yield return Seconds(1f);
            Info("Game view " + Screen.width + " x " + Screen.height);

            // S1 the tower with a flag, a grunt and a tank on its deck
            var high = Lead(OzUePlayground.Drop(OzUePlayground.PlatformHigh, Team.Neutral, c + new Vector3(25f, 0f, 0f), 1));
            var mid = Lead(OzUePlayground.Drop(OzUePlayground.PlatformMid, Team.Neutral, c + new Vector3(-30f, 0f, 0f), 1));
            yield return Frames(3);
            var hi = high != null ? w.PlatformOf(high.Id) : null; var mi = mid != null ? w.PlatformOf(mid.Id) : null;
            if (hi == null || mi == null) { Check(false, "S0 the two platforms drop"); yield return Finish(playing0, speed0, on0); yield break; }
            var flag = Lead(OzUePlayground.Drop(OzUePlayground.Flag, Team.Red, Deck(hi, 2.5f, 4f), 1));
            Lead(OzUePlayground.Drop("grunt", Team.Blue, Deck(hi, 6f, 5f), 1));
            Lead(OzUePlayground.Drop("light", Team.Blue, Deck(hi, 5f, 7f), 1));
            yield return Seconds(1f);
            yield return Collapse("S1_tower", hi, flag);

            // S2 the medium platform
            yield return Collapse("S2_platform", mi, null);

            // S3 the obstacle
            var obst = Lead(OzUePlayground.Drop(OzUePlayground.Obstacle, Team.Neutral, c + new Vector3(0f, 0f, 30f), 1));
            yield return Frames(3);
            if (obst != null)
            {
                OzUeControls.CentreOn(c + new Vector3(0f, 0f, 30f)); yield return Seconds(0.6f); Shot("S3_obstacle_before");
                int h0 = OzUeStructureBreak.HoloShatters, p0 = OzUeStructureBreak.HoloPanels; int fog0 = OzUeStructureBreak.FogPuffs;
                obst.SetHealth(0); w.UpdateObjectHealth(obst);
                yield return null;
                Check(OzUeStructureBreak.HoloShatters == h0 + 1 && OzUeStructureBreak.HoloPanels - p0 >= 30, "S3 the hologram shatters into its panels: " + (OzUeStructureBreak.HoloPanels - p0));
                yield return Dense("S3_obstacle", 99f, null);
                Check(OzUeStructureBreak.FogPuffs == fog0, "S3 no dust from a hologram");
                yield return Seconds(3f);
                int left = 0; foreach (var s in FindObjectsByType<OzUeHoloShatter>(FindObjectsSortMode.None)) if (s) left += s.Count > 0 ? 1 : 0;
                Check(left == 0, "S3 every panel gone by +7.5 s (shatters left " + left + ")");
            }
            else Check(false, "S3 the obstacle drops");

            // S4 a platform dropped on the tower's rubble
            int live0 = OzUeStructureBreak.LivePieces;
            var again = Lead(OzUePlayground.Drop(OzUePlayground.PlatformHigh, Team.Neutral, c + new Vector3(25f, 0f, 0f), 1));
            yield return Seconds(1f);
            int live1 = OzUeStructureBreak.LivePieces;
            Check(again != null && live1 < live0, "S4 a platform dropped on the rubble: the rubble in its footprint sinks (" + live0 + " -> " + live1 + " pieces)");
            Shot("S4_new_platform_on_rubble");

            // S5 CLEAR FIELD
            OzUePlayground.ClearField(); yield return Frames(4);
            Check(OzUeStructureBreak.LivePieces == 0, "S5 CLEAR FIELD: structure pieces left " + OzUeStructureBreak.LivePieces);

            // S6 the switch off: the 1.3 fall
            OzUeStructureBreak.On = false;
            var off = Lead(OzUePlayground.Drop(OzUePlayground.PlatformMid, Team.Neutral, c, 1));
            yield return Frames(3);
            if (off != null)
            {
                int made0 = OzUeStructureBreak.PiecesMade, old0 = OzUeStructureBreak.OldPath;
                off.SetHealth(0); w.UpdateObjectHealth(off); yield return Frames(3);
                Check(OzUeStructureBreak.PiecesMade == made0 && OzUeStructureBreak.OldPath == old0 + 1, "S6 switch off: the 1.3 fall, no pieces");
            }
            OzUeStructureBreak.On = true;
            yield return Seconds(2f);
            yield return Finish(playing0, speed0, on0);
        }

        IEnumerator Finish(bool playing0, float speed0, bool on0)
        {
            OzUeStructureBreak.On = on0;
            OzUePlace.Stop(); OzUePick.ClearInspect(); OzUePlayground.ClearField();
            OzUeControls.SetSpeed(speed0); OzUeControls.SetPlaying(playing0);
            yield return Frames(2);
            var sb = new StringBuilder();
            sb.Append(Tag).Append(' ').Append(fail == 0 ? "PASS" : "FAIL").Append(": ").Append(pass).Append(" of ").Append(pass + fail).Append(" checks").AppendLine();
            sb.Append(lines);
            sb.Append("frames: ").Append(pictures.Count).Append(" in ").Append(Folder ?? "(no folder)").AppendLine();
            Report = sb.ToString();
            if (!string.IsNullOrEmpty(Folder)) { Directory.CreateDirectory(Folder); File.WriteAllText(Path.Combine(Folder, "report.txt"), Report); }
            Debug.Log(Report);
            Destroy(gameObject);
        }
    }
}
#endif
