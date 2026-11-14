#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace OpenZ
{
    /// <summary>
    /// k75a3 Play proof of the faction screen's VS sign, live prints and DOSSIER cards (1.2 owner items 101 / 102); menu OpenZ/V1.2/Faction
    /// Screen: 75a3 Play Proof (Play), Editor/OzV12FactionDeckCheck.cs. Started on the MainMenu scene in Play it walks by itself (about two
    /// minutes), hands off, at 1920 x 1080 and then 1366 x 768:
    ///  Q1  the faction screen opens; its VS sign, cards, fonts and motion maps load; the posters stand where the layout says (their
    ///      middles Spread u from the screen's middle), the VS sign centred between them, clear of both.
    ///  Q2  at rest (the pointer away): no card out, no poster alive (their print layers on the print's own material, as k75a2).
    ///  Q3  the VS sign pulses (its scale 1.00 .. 1.04, smooth, 2.5 s) and its light band passes; pictures at three pulse phases (the
    ///      whole screen and the sign close up).
    ///  Q4  the pointer on Zod's poster: his card comes out (a paper sound), settles at rest on his poster's left, its five rows say the
    ///  Q5  the card coming out, frame by frame (its clock held at 0.04 .. 0.75 s): seven pictures.
    ///  Q6  the pointer jumps to EvilZod's poster: Zod's card goes back first (a paper sound), then EvilZod's comes out on his poster's right;
    ///      never both at once (counted every frame). Picture. The pointer on EvilZod's card itself: it stays.
    ///  Q7  the keys (the pad's D-pad sends them): LEFT brings Zod's card, RIGHT EvilZod's. Picture.
    ///  Q8  the pointer away: the cards back, both prints at rest again (the print's own material).
    ///  Q9  1366 x 768: everything fits (posters, VS, the card when out, titles, tips, BACK: inside the screen, apart); the card's rows not cut.
    ///      Pictures: idle, hover Zod, hover EvilZod.
    ///  Q10 BACK to the main menu: the VS sign's, cards', icons' and motion maps' pictures unloaded, the cards' font assets gone.
    /// Video frames (menu ... 75a3 Video Frames): 10 s of the pointer resting, on Zod's poster, across to EvilZod's and onto his card, and
    /// away, every frame at 1280 x 720 (JPG, their times in frames.txt); tools/frames_to_mp4.py makes the MP4. Test only (editor).
    /// </summary>
    public sealed class OzFactionDeckProof : MonoBehaviour
    {
        public static string Report = "idle";
        public static OzFactionDeckProof I { get; private set; }
        const string T = "[OzFactionDeckProof] ";

        bool frames, started, putBack; int viewWas = -1;
        string dir; readonly StringBuilder sb = new StringBuilder(); int passes, fails, pics;

        /// <summary></summary>
        public static string Folder()
        {
            var d = new DirectoryInfo(Application.dataPath);
            for (int i = 0; i < 8 && d != null; i++, d = d.Parent)
            {
                var staging = Path.Combine(d.FullName, "work", "staging");
                if (!Directory.Exists(staging)) continue;
                foreach (var stage in Directory.GetDirectories(staging))
                {
                    var kit = Path.Combine(stage, "k75a3_faction_vs_dossiers");
                    if (Directory.Exists(kit)) return Path.Combine(kit, "proof");
                }
            }
            return Path.Combine(Application.persistentDataPath, "k75a3_faction_vs_dossiers");
        }

        public static void Run(bool videoFrames)
        {
            if (!Application.isPlaying) { Debug.LogWarning(T + "enter Play on the MainMenu scene first"); return; }
            if (I) { Debug.LogWarning(T + "already running"); return; }
            if (!FindFirstObjectByType<OpenZMainMenu>()) { Debug.LogWarning(T + "start it on the main menu (the MainMenu scene in Play)"); return; }
            Report = "running";
            var go = new GameObject("OzFactionDeckProof"); DontDestroyOnLoad(go);
            go.AddComponent<OzFactionDeckProof>().frames = videoFrames;
        }

        public static void Stop()
        {
            if (!I) { Debug.Log(T + "not running"); return; }
            I.StopAllCoroutines(); I.Log("stopped by the menu"); I.Finish();
        }

        void Awake() { I = this; }

        void Start()
        {
            string root = Folder(); int n = 1;
            string stem = frames ? "video_" : "play_";
            while (Directory.Exists(Path.Combine(root, stem + n))) n++;
            dir = Path.Combine(root, stem + n); Directory.CreateDirectory(dir);
            viewWas = OzFinalGateView.SelectedIndex();
            started = true;
            Log("k75a3 faction VS / dossiers " + (frames ? "video frames" : "Play proof") + ", Game view " + Screen.width + " x " + Screen.height + ", into " + dir);
            StartCoroutine(frames ? Video() : All());
        }

        void OnDestroy()
        {
            if (started && !putBack) { PutBack(); Debug.Log(T + "stopped before its end: the settings put back"); }
            if (I == this) I = null;
        }

        void PutBack()
        {
            putBack = true;
            var m = Menu(); if (m) { m.ProofFactionPointer(null); m.ProofDossierClock(null); }
            if (viewWas >= 0) OzFinalGateView.Select(viewWas);
        }

        void Finish()
        {
            if (!putBack) PutBack();
            Report = $"{T}{(fails == 0 ? "PASS" : "FAIL")} {passes}/{passes + fails} checks, {pics} pictures; {dir.Replace('\\', '/')}";
            Log(Report);
            File.WriteAllText(Path.Combine(dir, "checks.txt"), sb.ToString());
            Destroy(gameObject);
        }

        // ------------------------------------------------------------------------------------------------ helpers
        void Log(string t) { sb.AppendLine(t); Debug.Log(T + t); }
        void Check(string what, bool ok) { if (ok) passes++; else fails++; Log((ok ? "PASS " : "FAIL ") + what); }
        static IEnumerator Real(float s) { float t = Time.realtimeSinceStartup + s; while (Time.realtimeSinceStartup < t) yield return null; }
        static IEnumerator Until(System.Func<bool> c, float max) { float t = Time.realtimeSinceStartup + max; while (Time.realtimeSinceStartup < t && !SafeTrue(c)) yield return null; }
        static bool SafeTrue(System.Func<bool> c) { try { return c(); } catch (System.Exception) { return false; } }
        static OpenZMainMenu Menu() => FindFirstObjectByType<OpenZMainMenu>();
        static string Plain(string s) => System.Text.RegularExpressions.Regex.Replace(s ?? "", "<[^>]+>", "").Replace("\n", " ");
        static float LogNow => Time.unscaledTime;
        static int Sounds(float l0, string key) => OzSoundLog.Since(l0).Count(e => e.key == key && e.outcome != null && e.outcome.StartsWith("PLAY"));
        static float PxPerU => Screen.height / OzLayout.RefH;

        IEnumerator Shot(string name)
        {
            string p = Path.Combine(dir, name + "_" + Screen.width + "x" + Screen.height + ".png");
            ScreenCapture.CaptureScreenshot(p); pics++; Log("   picture " + Path.GetFileName(p));
            yield return OzProofPictures.AfterGrab(); yield return OzProofPictures.Written(p);
        }

        /// <summary>A close-up of a screen rect (the VS sign), from a capture of the frame.</summary>
        IEnumerator Crop(string name, Rect r)
        {
            yield return new WaitForEndOfFrame();
            var full = ScreenCapture.CaptureScreenshotAsTexture();
            int x0 = Mathf.Clamp(Mathf.FloorToInt(r.xMin), 0, full.width - 1), y0 = Mathf.Clamp(Mathf.FloorToInt(r.yMin), 0, full.height - 1);
            int w = Mathf.Clamp(Mathf.CeilToInt(r.width), 1, full.width - x0), h = Mathf.Clamp(Mathf.CeilToInt(r.height), 1, full.height - y0);
            var c = new Texture2D(w, h, TextureFormat.RGBA32, false);
            c.SetPixels(full.GetPixels(x0, y0, w, h)); c.Apply(false);
            File.WriteAllBytes(Path.Combine(dir, name + "_" + Screen.width + "x" + Screen.height + ".png"), c.EncodeToPNG());
            Destroy(c); Destroy(full); pics++;
            Log("   picture " + name + " (a close-up)");
        }

        IEnumerator SetSize(int w, int h)
        {
            OzFinalGateView.SetFixed(w, h, "OpenZ " + w + "x" + h, out string info);
            yield return Until(() => Screen.width == w && Screen.height == h, 5f);
            yield return Real(0.8f);
            if (Screen.width != w || Screen.height != h) Check($"the Game view shows {w} x {h} ({info}; it shows {Screen.width} x {Screen.height})", false);
            else Log("Game view " + w + " x " + h + " (" + info + ")");
        }

        static Rect ScreenRect(RectTransform rt)
        {
            if (!rt) return new Rect();
            var c = new Vector3[4]; rt.GetWorldCorners(c);
            return Rect.MinMaxRect(Mathf.Min(c[0].x, c[1].x, c[2].x, c[3].x), Mathf.Min(c[0].y, c[1].y, c[2].y, c[3].y), Mathf.Max(c[0].x, c[1].x, c[2].x, c[3].x), Mathf.Max(c[0].y, c[1].y, c[2].y, c[3].y));
        }

        IEnumerator Open(string tag)
        {
            var m = Menu();
            if (!m) { Check(tag + " the main menu", false); yield break; }
            if (m.ProofView != "Main") { m.ProofMain(); yield return Real(0.8f); }
            m.ProofFactionPointer(new Vector2(Screen.width * 0.5f, Screen.height * 0.06f));
            m.ProofFaction();
            yield return Until(() => m.ProofFactionShown && m.ProofRig && m.ProofRig.Ready && m.ProofDeckReady && m.ProofPrintAlpha(0) > 0.99f && m.ProofPrintAlpha(1) > 0.99f, 15f);
            yield return Real(0.8f);
        }

        static bool Out(OpenZMainMenu m, int i) => m.ProofDossierState(i) != "Hidden";

        // ------------------------------------------------------------------------------------------------ the walk
        IEnumerator All()
        {
            yield return Real(1f);
            yield return SetSize(1920, 1080);
            yield return Open("Q1");
            var m = Menu();
            var deck = m.ProofDeck;
            var L = m.ProofLayout;
            Rect r0 = ScreenRect(m.ProofPoster(0)), r1 = ScreenRect(m.ProofPoster(1)), vs = ScreenRect(m.ProofVs);
            float k = PxPerU * L.scale, mid = Screen.width * 0.5f;
            bool place = Mathf.Abs(r0.center.x - (mid - OzFactionDeck.Spread * k)) < 3f && Mathf.Abs(r1.center.x - (mid + OzFactionDeck.Spread * k)) < 3f && Mathf.Abs(vs.center.x - mid) < 3f
                         && vs.xMin > r0.xMax && vs.xMax < r1.xMin;
            Check($"Q1 the faction screen opened; its VS / cards loaded in {(deck ? deck.LoadSeconds : -1f):0.00} s ({(deck && deck.BodyTmp ? "TextMeshPro" : "plain text")} cards, the {(deck && deck.VsSign == 1 ? "glowing" : "painted")} VS); "
                  + $"the posters' middles at {r0.center.x - mid:0} / {r1.center.x - mid:0} px from the screen's middle (+-{OzFactionDeck.Spread * k:0} wanted), the VS sign {vs.xMin:0}..{vs.xMax:0} px between them ({r0.xMax:0} / {r1.xMin:0})",
                  deck && deck.Ready && place && m.ProofVsSign && m.ProofVsSign.enabled && m.ProofVsSign.texture == deck.Vs);
            // Q2 at rest
            yield return Real(1.2f);
            var rig = m.ProofRig;
            bool rest = !Out(m, 0) && !Out(m, 1) && m.ProofLive(0) == 0f && m.ProofLive(1) == 0f
                        && rig && m.ProofLayer(0, 0).material == rig.Print[0] && m.ProofLayer(0, 1).material == rig.Print[0] && m.ProofLayer(1, 0).material == rig.Print[1] && m.ProofLayer(1, 1).material == rig.Print[1];
            Check($"Q2 at rest: cards out {Out(m, 0)} / {Out(m, 1)}, the prints alive {m.ProofLive(0):0.00} / {m.ProofLive(1):0.00}, the print layers on the print's own material {rest}", rest);
            yield return Shot("faction_idle");
            // Q3 the VS pulse and light
            {
                float lo = 9f, hi = -9f, step = 0f, prev = m.ProofVs.localScale.x; float sweepLo = 9f, sweepHi = -9f; float t0 = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - t0 < 3.8f)
                {
                    yield return null;
                    float s = m.ProofVs.localScale.x; lo = Mathf.Min(lo, s); hi = Mathf.Max(hi, s); step = Mathf.Max(step, Mathf.Abs(s - prev)); prev = s;
                    var sw = m.ProofVsMat ? m.ProofVsMat.GetVector(OzFactionDeck.IdSweep) : Vector4.zero;
                    if (sw.x > -0.5f) { sweepLo = Mathf.Min(sweepLo, sw.x); sweepHi = Mathf.Max(sweepHi, sw.x); }
                }
                Check($"Q3 the VS sign pulses: scale {lo:0.000} .. {hi:0.000} (1.00 .. 1.04), the largest step in a frame {step:0.0000} (smooth); its light band ran from {sweepLo:0.00} to {sweepHi:0.00} along it",
                      lo < 1.004f && lo >= 0.999f && hi > 1.035f && hi <= 1.0405f && step < 0.004f && sweepLo < 0.1f && sweepHi > 0.9f);
                string[] names = { "vs_pulse_low", "vs_pulse_mid", "vs_pulse_high" }; float[] want = { 1.0f, 1.02f, 1.04f };
                for (int q = 0; q < 3; q++)
                {
                    float target = want[q];
                    yield return Until(() => Mathf.Abs(m.ProofVs.localScale.x - target) < 0.0015f, 3f);
                    var vr = ScreenRect(m.ProofVs);
                    yield return Crop(names[q], new Rect(vr.x - 40f, vr.y - 40f, vr.width + 80f, vr.height + 80f));
                    yield return Shot(names[q]);
                }
            }
            // Q4 hover Zod
            {
                int in0 = OpenZMainMenu.FactionPaperIn; float l0 = LogNow;
                m.ProofFactionPointer(r0.center); yield return Real(1.4f);
                var cr = ScreenRect(m.ProofDossier(0));
                var rows = Enumerable.Range(0, OzFactionDeck.Rows).Select(r => Plain(m.ProofDossierRow(0, r))).ToArray();
                bool words = Enumerable.Range(0, OzFactionDeck.Rows).All(r => rows[r] == OzFactionDeck.Line(0, r));
                bool rowsIn = Enumerable.Range(0, OzFactionDeck.Rows).All(r => m.ProofDossierRowAlpha(0, r) > 0.99f);
                var heading = m.ProofDossierLabel(0, "General"); string head = heading is TMP_Text ht ? ht.text : heading is Text lt ? lt.text : "";
                bool card = m.ProofDossierState(0) == "Shown" && Mathf.Abs(m.ProofDossierSlide(0) - 1f) < 0.001f && Mathf.Abs(m.ProofDossierTilt(0) - OzFactionDeck.RestTilt[0]) < 0.3f && !Out(m, 1)
                            && cr.xMax < r0.center.x && cr.xMin >= 0f && cr.center.x < r0.xMin;
                var lm = m.ProofLiveMat(0, 0);
                bool live = m.ProofLive(0) > 0.95f && m.ProofLive(1) == 0f && lm && m.ProofLayer(0, 0).material == lm && lm.GetFloat(OzFactionDeck.IdLive) > 0.9f && m.ProofLayer(1, 0).material == rig.Print[1];
                Check($"Q4 the pointer on Zod's poster: his card out ({m.ProofDossierState(0)}, slide {m.ProofDossierSlide(0):0.00}, tilt {m.ProofDossierTilt(0):0.00} deg, rest {OzFactionDeck.RestTilt[0]}) at {cr.xMin:0}..{cr.xMax:0} px left of his poster ({r0.xMin:0}) {card}, "
                      + $"EvilZod's not; the paper sound {OpenZMainMenu.FactionPaperIn - in0} x (played {Sounds(l0, OzFactionDeck.SoundIn)} x); \"{head}\"; the rows {string.Join(" / ", rows)} word for word {words}, all in {rowsIn}; his print alive {m.ProofLive(0):0.00} (the live layers drawing {live}), EvilZod's {m.ProofLive(1):0.00}",
                      card && words && rowsIn && head == "GENERAL ZOD" && OpenZMainMenu.FactionPaperIn - in0 == 1 && live);
                yield return Shot("hover_zod_card");
            }
            // Q5 the card coming out, frame by frame
            {
                m.ProofFactionPointer(new Vector2(Screen.width * 0.5f, Screen.height * 0.06f)); yield return Until(() => !Out(m, 0), 2f); yield return Real(0.4f);
                m.ProofFactionPointer(r0.center); yield return null; yield return null;
                float[] at = { 0.04f, 0.10f, 0.17f, 0.25f, 0.34f, 0.45f, 0.75f }; float lastX = -1f; bool outward = true;
                for (int q = 0; q < at.Length; q++)
                {
                    m.ProofDossierClock(at[q]); yield return null; yield return null;
                    var cr = ScreenRect(m.ProofDossier(0)); if (q > 0 && q < 4 && cr.center.x > lastX + 0.5f) outward = false; lastX = cr.center.x;
                    yield return Shot("card_in_" + (q + 1));
                }
                m.ProofDossierClock(null);
                Check($"Q5 Zod's card comes out frame by frame (its clock held at {string.Join(", ", at.Select(a => a.ToString("0.00")))} s): it moves out to the left {outward}, seven pictures card_in_1..7", outward);
            }
            // Q6 the jump to EvilZod, never both at once; his card itself keeps it out
            {
                yield return Real(1f);
                int both0 = OpenZMainMenu.FactionBothCards, in0 = OpenZMainMenu.FactionPaperIn, out0 = OpenZMainMenu.FactionPaperOut;
                float t0 = Time.realtimeSinceStartup, evilAt = -1f; int bothSeen = 0;
                m.ProofFactionPointer(r1.center);
                while (Time.realtimeSinceStartup - t0 < 1.5f)
                {
                    yield return null;
                    if (Out(m, 0) && Out(m, 1)) bothSeen++;
                    if (evilAt < 0f && Out(m, 1)) evilAt = Time.realtimeSinceStartup - t0;
                }
                var cr = ScreenRect(m.ProofDossier(1));
                var rows = Enumerable.Range(0, OzFactionDeck.Rows).Select(r => Plain(m.ProofDossierRow(1, r))).ToArray();
                bool words = Enumerable.Range(0, OzFactionDeck.Rows).All(r => rows[r] == OzFactionDeck.Line(1, r));
                Check($"Q6 the pointer jumps to EvilZod's poster: Zod's card back first (paper back {OpenZMainMenu.FactionPaperOut - out0} x), EvilZod's out after {evilAt * 1000f:0} ms (paper {OpenZMainMenu.FactionPaperIn - in0} x) at {cr.xMin:0}..{cr.xMax:0} px right of his poster ({r1.xMax:0}); "
                      + $"frames with both out {bothSeen} (the menu's count {OpenZMainMenu.FactionBothCards - both0}); the rows {string.Join(" / ", rows)} word for word {words}",
                      bothSeen == 0 && OpenZMainMenu.FactionBothCards == both0 && evilAt > 0.15f && evilAt < 0.6f && OpenZMainMenu.FactionPaperOut - out0 == 1 && OpenZMainMenu.FactionPaperIn - in0 == 1
                      && m.ProofDossierState(1) == "Shown" && cr.center.x > r1.xMax && cr.xMax <= Screen.width && words);
                yield return Shot("hover_evilzod_card");
                var paper = ScreenRect(m.ProofDossier(1));
                m.ProofFactionPointer(new Vector2(Mathf.Min(paper.center.x + paper.width * 0.15f, Screen.width - 4f), paper.center.y)); yield return Real(0.8f);
                Check($"Q6 the pointer on EvilZod's card itself: the card stays out ({m.ProofDossierState(1)}), EvilZod stays in focus ({m.ProofActive})", m.ProofDossierState(1) == "Shown" && m.ProofActive == 1);
            }
            // Q7 keys
            {
                m.ProofFactionPointer(null);
                m.ProofFactionKeys(true, false, false, false); yield return Real(1.4f);
                bool left = m.ProofDossierState(0) == "Shown" && !Out(m, 1);
                m.ProofFactionKeys(false, true, false, false); yield return Real(1.4f);
                bool right = m.ProofDossierState(1) == "Shown" && !Out(m, 0);
                Check($"Q7 keys: LEFT brings Zod's card {left}, RIGHT EvilZod's {right} (one at a time)", left && right);
                yield return Shot("keys_evilzod_card");
            }
            // Q8 away: at rest again
            {
                m.ProofFactionPointer(new Vector2(Screen.width * 0.5f, Screen.height * 0.06f)); yield return Real(2.5f);
                bool rest2 = !Out(m, 0) && !Out(m, 1) && m.ProofLive(0) == 0f && m.ProofLive(1) == 0f && m.ProofLayer(0, 0).material == rig.Print[0] && m.ProofLayer(1, 1).material == rig.Print[1];
                Check($"Q8 the pointer away: the cards back {(!Out(m, 0) && !Out(m, 1))}, both prints at rest again on the print's own material {rest2}", rest2);
            }
            // Q9 1366 x 768
            m.ProofFactionBack(); yield return Real(1.5f);
            yield return SetSize(1366, 768);
            yield return Open("Q9");
            yield return Small(m = Menu());
            // Q10 back to the main menu: unloaded
            m.ProofFactionBack(); yield return Real(2.5f);
            {
                var left = Resources.FindObjectsOfTypeAll<Texture2D>().Where(t => OzFactionDeckAssets.Names.Contains(t.name)).Select(t => t.name).ToList();
                var fonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>().Where(f => f.name.StartsWith("OzFaction_")).Select(f => f.name).ToList();
                Check($"Q10 back on the main menu: the deck closed ({OzFactionDeckAssets.Closed} x, {OzFactionDeckAssets.LastUnloadCount} assets unloaded, {OzFactionDeckAssets.LastFontsDestroyed} font assets destroyed); its pictures left {left.Count}{(left.Count > 0 ? " (" + string.Join(", ", left.Take(6)) + ")" : "")}, its fonts left {fonts.Count}",
                      left.Count == 0 && fonts.Count == 0 && !OzFactionDeckAssets.I);
            }
            yield return SetSize(1920, 1080);
            Finish();
        }

        IEnumerator Small(OpenZMainMenu m)
        {
            var s = new Rect(0, 0, Screen.width, Screen.height);
            Rect r0 = ScreenRect(m.ProofPoster(0)), r1 = ScreenRect(m.ProofPoster(1)), vs = ScreenRect(m.ProofVs), t0 = ScreenRect(m.ProofTipRt(0)), t1 = ScreenRect(m.ProofTipRt(1));
            var back = m.ProofFactionBackRt ? ScreenRect(m.ProofFactionBackRt) : new Rect();
            yield return Shot("faction");
            m.ProofFactionPointer(r0.center); yield return Real(1.4f);
            var c0 = ScreenRect(m.ProofDossier(0)); bool cut0 = Cut(m, 0);
            yield return Shot("hover_zod_card");
            m.ProofFactionPointer(r1.center); yield return Real(1.6f);
            var c1 = ScreenRect(m.ProofDossier(1)); bool cut1 = Cut(m, 1);
            yield return Shot("hover_evilzod_card");
            bool inside = s.Contains(r0.min) && s.Contains(r0.max) && s.Contains(r1.min) && s.Contains(r1.max) && s.Contains(c0.min) && s.Contains(c0.max) && s.Contains(c1.min) && s.Contains(c1.max) && s.Contains(vs.min) && s.Contains(vs.max);
            float tuck = (OzFactionDeck.CardTuck + 12f) * PxPerU;
            bool apart = !vs.Overlaps(r0) && !vs.Overlaps(r1) && !back.Overlaps(c1) && !back.Overlaps(r1) && !c0.Overlaps(t0) && !c1.Overlaps(t1) && c0.xMax - r0.xMin <= tuck && r1.xMax - c1.xMin <= tuck && !c0.Overlaps(vs) && !c1.Overlaps(vs);
            Check($"Q9 1366 x 768: inside the screen {inside} (posters {r0.xMin:0}..{r0.xMax:0} / {r1.xMin:0}..{r1.xMax:0}, VS {vs.xMin:0}..{vs.xMax:0}, cards {c0.xMin:0}..{c0.xMax:0} / {c1.xMin:0}..{c1.xMax:0} px), apart (the VS, the BACK button, the tips; the cards only tucked behind their posters) {apart}, the rows not cut {(!cut0 && !cut1)}",
                  inside && apart && !cut0 && !cut1);
            m.ProofFactionPointer(new Vector2(Screen.width * 0.5f, Screen.height * 0.06f)); yield return Real(0.8f);
        }

        /// <summary>A row's words wider than their box (cut or spilling).</summary>
        static bool Cut(OpenZMainMenu m, int i)
        {
            var root = m.ProofDossier(i); if (!root) return true;
            foreach (var t in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (t.name != "Words") continue;
                var rt = (RectTransform)t.transform;
                if (t.GetPreferredValues(t.text, rt.rect.width, 0f).x > rt.rect.width + 1f || t.isTextOverflowing) return true;
            }
            return false;
        }

        // ------------------------------------------------------------------------------------------------ the video frames
        IEnumerator Video()
        {
            yield return Real(1f);
            if (Screen.width != 1920 || Screen.height != 1080) yield return SetSize(1920, 1080);
            yield return Open("V");
            var m = Menu();
            Rect r0 = ScreenRect(m.ProofPoster(0)), r1 = ScreenRect(m.ProofPoster(1));
            var away = new Vector2(Screen.width * 0.5f, Screen.height * 0.06f);
            m.ProofFactionPointer(away); yield return Real(0.8f);
            yield return Shot("frames_ref");                                                                        // the picture the first frame is compared with (its way up)
            int W = 1280, H = 720, written = 0, requested = 0, failed = 0;
            var full = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32);
            var small = new RenderTexture(W, H, 0, RenderTextureFormat.ARGB32);
            var times = new StringBuilder();
            float t0 = Time.realtimeSinceStartup, len = 10f; int n = 0; Rect card1 = new Rect();
            while (Time.realtimeSinceStartup - t0 < len)
            {
                float t = Time.realtimeSinceStartup - t0;
                Vector2 p;
                if (t < 1f) p = away;
                else if (t < 4f) { float u = (t - 1f) / 3f; p = new Vector2(Mathf.Lerp(r0.xMax - r0.width * 0.2f, r0.xMin + r0.width * 0.25f, Mathf.SmoothStep(0f, 1f, u)), Mathf.Lerp(r0.center.y + r0.height * 0.15f, r0.center.y - r0.height * 0.1f, u)); }
                else if (t < 4.6f) { float u = Mathf.SmoothStep(0f, 1f, (t - 4f) / 0.6f); p = Vector2.Lerp(new Vector2(r0.xMin + r0.width * 0.25f, r0.center.y - r0.height * 0.1f), r1.center, u); }
                else if (t < 7.6f) { float u = (t - 4.6f) / 3f; p = new Vector2(Mathf.Lerp(r1.center.x, r1.xMax - r1.width * 0.15f, Mathf.SmoothStep(0f, 1f, u)), r1.center.y + Mathf.Sin(u * Mathf.PI) * r1.height * 0.12f); }
                else if (t < 8.6f) { if (card1.width <= 0f) card1 = ScreenRect(m.ProofDossier(1)); p = card1.width > 0f ? new Vector2(Mathf.Min(card1.center.x + card1.width * 0.1f, Screen.width - 4f), card1.center.y) : r1.center; }
                else p = away;
                m.ProofFactionPointer(p);
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshotIntoRenderTexture(full);
                Graphics.Blit(full, small);
                int idx = n++; string file = Path.Combine(dir, "frame_" + idx.ToString("00000") + ".jpg");
                times.AppendLine(idx.ToString() + " " + t.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture));
                Interlocked.Increment(ref requested);
                AsyncGPUReadback.Request(small, 0, TextureFormat.RGBA32, req =>
                {
                    if (req.hasError) { Interlocked.Increment(ref failed); return; }
                    var data = req.GetData<byte>().ToArray();
                    Task.Run(() =>
                    {
                        try { File.WriteAllBytes(file, ImageConversion.EncodeArrayToJPG(data, GraphicsFormat.R8G8B8A8_SRGB, (uint)W, (uint)H, 0, 90)); Interlocked.Increment(ref written); }
                        catch (System.Exception) { Interlocked.Increment(ref failed); }
                    });
                });
                yield return null;
            }
            yield return Until(() => written + failed >= requested, 20f);
            full.Release(); Destroy(full); small.Release(); Destroy(small);
            File.WriteAllText(Path.Combine(dir, "frames.txt"), times.ToString());
            float fps = n / len;
            Check($"V the video frames: {written} of {requested} written ({failed} failed), {fps:0.0} frames a second over {len:0} s, 1280 x 720 (frames.txt has their times); the MP4: python tools/frames_to_mp4.py {dir.Replace('\\', '/')}",
                  written == requested && failed == 0 && fps > 12f);
            m.ProofFactionPointer(null);
            m.ProofFactionBack(); yield return Real(1.5f);
            Finish();
        }
    }
}
#endif
