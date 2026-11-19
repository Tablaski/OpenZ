#if UNITY_EDITOR
// OpenZ (GPL-3.0)
// 'OpenZ/1.5/Checks/Air Burst Smoke Fade/...' (editor only). FLAK's smoke cover measured frame by frame + frames at the RTS camera.
using System.Collections;
using System.IO;
using System.Text;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// A38: FLAK's (exp_v2_5, the thrown head's air burst) smoke fades away on a short life (OzExpInGame.FlakSmokeLife) while the player plays;
    /// every scene keeps the long smoke. Four cases in one level run, each fired through the game's own path (OzExpInGame.Play, as HeadApex does;
    /// case 4 the real head of a killed vehicle), the field quiet (the computer players held, restored after), the frames locked to 30 a second:
    /// <list type="number">
    /// <item>in game: the RTS camera his (OzExpInGame.SceneNow null): want the short channels, the smoke gone by 3.6 s, still whole at 0.8 s.</item>
    /// <item>a scene camera: the RTS camera off, the picture as it is (the gate), the way the films, the briefings and
    /// the endings hold it: want the long channels only, the smoke still there at 5.5 s (as before the kit).</item>
    /// <item>the switch off (OzExpInGame.QuickSmoke false), in game: want the long smoke (as before the kit).</item>
    /// <item>the real path: the nearest vehicle of ours that is not too small for a new explosion, killed by the rules (DamageHealth +
    /// UpdateObjectHealth) with OzExpInGame.ForceHead: its head bursts at the top of its flight (HeadApex): want a head burst and the short
    /// smoke (INFO when no head was thrown: a vehicle without a turret).</item>
    /// </list>
    /// The cover: the sum over FLAK's smoke channels (its cloud, heart and wisps, long and short) of every puff's alpha (its gradient at its age x
    /// its own alpha) x its drawn size squared (m2). Times after the burst: the peak, 50 %, 10 %, gone (under 1 % of the peak) and the last puff.
    /// The burst is fired at scale 1 (a head 9 m up); a smaller head runs x sqrt(scale). Frames at 0.5 .. 7 s after the burst. Report + frames:
    /// the kit's proof folder (check_&lt;date_time&gt;/).
    /// </summary>
    public sealed class OzAirBurstFadeCheck : MonoBehaviour
    {
        public const string Tag = "[OpenZ 1.5 Air Burst Smoke Fade]";
        public static bool Running { get; private set; }
        public static string Report { get; private set; } = "no run yet";
        /// <summary>Seconds after the burst at which a frame is kept; how long a case watches; the frame rate; the in-game limits.</summary>
        public static readonly float[] FrameAt = { 0.5f, 1f, 1.5f, 2f, 2.5f, 3f, 3.5f, 4f, 5f, 6f, 7f };
        public const float WatchS = 9f, Fps = 30f, ShortGoneMax = 3.6f, LongGoneMin = 5.5f, WholeAt = 0.8f;
        static readonly string[] Long = { "ueb_dust", "ueb_cauli", "ueb_haze" }, Short = { OzUeBlastFx.QDust, OzUeBlastFx.QCauli, OzUeBlastFx.QHaze };
        string folder; readonly StringBuilder md = new StringBuilder();
        ParticleSystem.Particle[] buf = new ParticleSystem.Particle[1024];
        bool blue0, red0, quick0, ok = true; RtsCamera rts;

        struct Run1 { public string name, scene; public float peak, tPeak, t50, t10, tGone, tLast, atWhole; public int shortN, longN, quickPlus, longPlus; }

        public static void Begin(string folder)
        {
            var g = ZGame.I;
            if (Running || !g || g.World == null || !OzExpInGame.InLevel()) { Debug.LogWarning(Tag + " enter Play in a level first"); return; }
            if (g.World.GameOver) { Debug.LogWarning(Tag + " the match is over: enter Play again"); return; }
            var k = g.gameObject.AddComponent<OzAirBurstFadeCheck>(); k.folder = folder;
        }

        void Start() { Running = true; StartCoroutine(Run()); }
        void OnDestroy() { Running = false; }

        static ParticleSystem Ch(string name)
        {
            var z = OpenZFxPlayer.I; if (!z) return null;
            var t = z.transform.Find("FxCh_" + name);
            if (t) return t.GetComponent<ParticleSystem>();
            var go = GameObject.Find("FxCh_" + name);
            return go ? go.GetComponent<ParticleSystem>() : null;
        }

        /// <summary>The cover (m2) and the live puffs of the given channels now.</summary>
        float Cover(string[] names, out int n)
        {
            float c = 0f; n = 0;
            foreach (var name in names)
            {
                var ps = Ch(name); if (!ps) continue;
                if (buf.Length < ps.main.maxParticles) buf = new ParticleSystem.Particle[ps.main.maxParticles];
                int k = ps.GetParticles(buf); n += k;
                var col = ps.colorOverLifetime; var grad = col.enabled ? col.color.gradient : null;
                for (int i = 0; i < k; i++)
                {
                    var p = buf[i];
                    float age = p.startLifetime > 0f ? 1f - p.remainingLifetime / p.startLifetime : 1f;
                    float a = (grad != null ? grad.Evaluate(Mathf.Clamp01(age)).a : 1f) * p.startColor.a / 255f;
                    float s = p.GetCurrentSize(ps);
                    c += a * s * s;
                }
            }
            return c;
        }

        static string[] All => new[] { Long[0], Long[1], Long[2], Short[0], Short[1], Short[2] };

        /// <summary>The ground under the middle of the picture.</summary>
        static Vector3 Aim(Camera cam)
        {
            var g = ZGame.I; var r = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            var p = cam.transform.position; float y = g.GroundY(p.x, p.z);
            for (int i = 0; i < 6; i++) { float t = (y - r.origin.y) / Mathf.Min(-1e-3f, r.direction.y); p = r.origin + r.direction * Mathf.Max(0f, t); y = g.GroundY(p.x, p.z); }
            p.y = y; return p;
        }

        IEnumerator Quiet()
        {   // no explosion of the seven running and FLAK's channels empty (at most 20 s)
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 20f) { Cover(All, out int n); if (OzUeBlastFx.Live == 0 && n == 0) break; yield return null; }
        }

        IEnumerator Run()
        {
            var g = ZGame.I; rts = FindFirstObjectByType<RtsCamera>();
            if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
            md.AppendLine($"# Air burst smoke fade check (A38) ({System.DateTime.Now:yyyy-MM-dd HH:mm})").AppendLine();
            md.AppendLine($"level {g.level}, human {g.player}; QuickSmoke {OzExpInGame.QuickSmoke}, FlakSmokeLife {OzExpInGame.FlakSmokeLife:0.00}, HeadOn {OzExpInGame.HeadOn}; fog of war wanted {OzFogOfWar.Wanted}").AppendLine();
            // the level's opening skipped as SPACE does, the flyover waited for, then the player's own camera
            for (int i = 0; i < 40; i++)
            {
                bool skipped = false;
                try { skipped = ZodBriefing.ProofSkipBriefing() || OzBlueCameo.ProofSkipBriefing(); } catch (System.Exception) { }
                if (skipped) break;
                if (!ZodBriefing.BriefingActive && !OzBlueCameo.BriefingActive && !(ZodFlyover.Live != null && ZodFlyover.Live.Active) && i > 6) break;
                yield return new WaitForSecondsRealtime(0.5f);
            }
            float w0 = Time.realtimeSinceStartup;
            while (OzExpInGame.SceneNow() != null && Time.realtimeSinceStartup - w0 < 60f) yield return new WaitForSecondsRealtime(0.5f);
            yield return new WaitForSecondsRealtime(1f);
            md.AppendLine($"before the cases: SceneNow = {OzExpInGame.SceneNow() ?? "null (the player plays)"}").AppendLine();
            blue0 = g.blueBot; red0 = g.redBot; quick0 = OzExpInGame.QuickSmoke;
            g.blueBot = false; g.redBot = false;
            var cam = Camera.main;
            if (!cam || !rts) { md.AppendLine("FAIL: no game camera / RTS camera"); ok = false; Finish(); yield break; }

            var r1 = new Run1 { name = "1 in game (the RTS camera his)" };
            yield return Case(1, r1, cam, false, true, x => r1 = x);
            var r2 = new Run1 { name = "2 a scene camera (the RTS camera off)" };
            yield return Case(2, r2, cam, true, true, x => r2 = x);
            var r3 = new Run1 { name = "3 the switch off (QuickSmoke false), in game" };
            yield return Case(3, r3, cam, false, false, x => r3 = x);

            md.AppendLine("| case | scene (SceneNow) | short puffs | long puffs | short / long count +1 | peak cover m2 at s | 50 % at s | 10 % at s | gone (< 1 %) at s | last puff at s | cover at " + WholeAt.ToString("0.0") + " s / peak |");
            md.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|");
            foreach (var r in new[] { r1, r2, r3 })
                md.AppendLine($"| {r.name} | {r.scene ?? "null"} | {r.shortN} | {r.longN} | {r.quickPlus} / {r.longPlus} | {r.peak:0} at {r.tPeak:0.00} | {r.t50:0.00} | {r.t10:0.00} | {r.tGone:0.00} | {r.tLast:0.00} | {(r.peak > 0f ? r.atWhole / r.peak : 0f):0.00} |");
            md.AppendLine();
            Row("case 1: the player plays (SceneNow null)", r1.scene == null);
            Row("case 1: the short channels drew the smoke (short puffs > 0), counted short once", r1.shortN > 0 && r1.quickPlus == 1 && r1.longPlus == 0);
            Row($"case 1: the cloud still whole at {WholeAt:0.0} s (cover >= 60 % of its peak)", r1.peak > 0f && r1.atWhole >= 0.6f * r1.peak);
            Row($"case 1: the smoke gone (< 1 % of the peak) by {ShortGoneMax:0.0} s: {r1.tGone:0.00} s", r1.tGone > 0f && r1.tGone <= ShortGoneMax);
            Row("case 2: a scene (SceneNow not null): " + (r2.scene ?? "null"), r2.scene != null);
            Row("case 2: the long smoke only (no short puff), counted long once", r2.shortN == 0 && r2.longN > 0 && r2.longPlus == 1 && r2.quickPlus == 0);
            Row($"case 2: the long smoke still there at {LongGoneMin:0.0} s (gone at {r2.tGone:0.00} s)", r2.tGone >= LongGoneMin);
            Row("case 3: the switch off: the long smoke only, counted long once", r3.scene == null && r3.shortN == 0 && r3.longN > 0 && r3.longPlus == 1);
            Row($"case 3: the long smoke still there at {LongGoneMin:0.0} s (gone at {r3.tGone:0.00} s)", r3.tGone >= LongGoneMin);
            float ratio = r3.tGone > 0f ? r1.tGone / r3.tGone : 0f;
            Row($"in game the smoke is gone at a third to a half of the long smoke's time: {r1.tGone:0.00} / {r3.tGone:0.00} s = {ratio:0.00}", ratio >= 0.3f && ratio <= 0.55f);
            md.AppendLine($"(info) the burst unchanged while whole: cover at 0.5 s in game / scene camera / switch off = see the frames case1/2/3 _t0.5s; the short puffs have the long puffs' sizes and speeds at the same second by construction (View/OzUeBlastFx.Quick.cs)").AppendLine();

            yield return Head(cam);
            Finish();
        }

        IEnumerator Case(int n, Run1 r, Camera cam, bool sceneCam, bool quickOn, System.Action<Run1> back)
        {
            yield return Quiet();
            OzExpInGame.QuickSmoke = quickOn;
            if (sceneCam) rts.enabled = false;
            yield return null;
            r.scene = OzExpInGame.SceneNow();
            var at = Aim(cam);
            int q0 = OzExpInGame.QuickSmokes, l0 = OzExpInGame.LongSmokes;
            Time.captureFramerate = (int)Fps;
            bool fired = OzExpInGame.Play(OzUeBlasts.Flak, at, 1f, 1f);
            float t0 = Time.time; int next = 0; r.tPeak = r.t50 = r.t10 = r.tGone = r.tLast = -1f;
            float last = -1f;
            while (Time.time - t0 <= WatchS)
            {
                yield return new WaitForEndOfFrame();
                float t = Time.time - t0;
                float c = Cover(All, out int live);
                Cover(Short, out int sn); r.shortN = Mathf.Max(r.shortN, sn);
                Cover(Long, out int ln); r.longN = Mathf.Max(r.longN, ln);
                if (c > r.peak) { r.peak = c; r.tPeak = t; r.t50 = r.t10 = r.tGone = -1f; }
                if (r.peak > 0f && t > r.tPeak)
                {
                    if (r.t50 < 0f && c < 0.5f * r.peak) r.t50 = t;
                    if (r.t10 < 0f && c < 0.1f * r.peak) r.t10 = t;
                    if (r.tGone < 0f && c < 0.01f * r.peak) r.tGone = t;
                }
                if (live > 0) last = t;
                if (r.atWhole <= 0f && t >= WholeAt) r.atWhole = c;
                if (next < FrameAt.Length && t >= FrameAt[next] - 0.5f / Fps)
                {
                    if (!string.IsNullOrEmpty(folder)) ScreenCapture.CaptureScreenshot(Path.Combine(folder, $"case{n}_t{FrameAt[next]:0.0}s.png".Replace(',', '.')));
                    next++;
                }
            }
            r.tLast = last;
            Time.captureFramerate = 0;
            if (sceneCam) rts.enabled = true;
            OzExpInGame.QuickSmoke = quick0;
            r.quickPlus = OzExpInGame.QuickSmokes - q0; r.longPlus = OzExpInGame.LongSmokes - l0;
            md.AppendLine($"case {n}: fired {fired} at ({at.x:0.0}, {at.y:0.0}, {at.z:0.0}) (the burst 9 m over it), scene: {r.scene ?? "none, the player plays"}; {OzExpInGame.LastLine}");
            back(r);
        }

        IEnumerator Head(Camera cam)
        {
            yield return Quiet();
            var g = ZGame.I; var w = g.World; ZObject pick = null; float bd = float.MaxValue; var c = Aim(cam);
            foreach (var o in w.Objects)
            {
                if (o == null || o.Destroyed || o.IsShuttle || o.Owner != g.player || (o.Kind != ZKind.Vehicle && o.Kind != ZKind.Cannon) || OzExpInGame.TooSmallForSwap(o)) continue;
                var p = ZGame.PxToWorld(o.CenterX, o.CenterY); float d = (p.x - c.x) * (p.x - c.x) + (p.z - c.z) * (p.z - c.z);
                if (d < bd) { bd = d; pick = o; }
            }
            md.AppendLine().AppendLine("## case 4: the real path (a vehicle of ours killed, its head bursts at the top of its flight)").AppendLine();
            if (pick == null) { md.AppendLine("INFO: no vehicle or turret of ours that may burst (not jeep / light / gatling / gun) in this level: case 4 not run"); yield break; }
            rts.FlyTo(g.CenterWorld(pick, false));
            float lead = Time.realtimeSinceStartup; while (Time.realtimeSinceStartup - lead < 1.5f) yield return null;
            int heads0 = OzExpInGame.Heads, q0 = OzExpInGame.QuickSmokes, l0 = OzExpInGame.LongSmokes;
            string scene = OzExpInGame.SceneNow();
            OzExpInGame.ForceHead = true;
            pick.DamageHealth(pick.Health); w.UpdateObjectHealth(pick);
            md.AppendLine($"killed {pick.Type} #{pick.Id} (ours), OzExpInGame.ForceHead on; scene before: {scene ?? "none, the player plays"}");
            float k0 = Time.realtimeSinceStartup;
            while (OzExpInGame.Heads == heads0 && Time.realtimeSinceStartup - k0 < 12f) yield return null;
            OzExpInGame.ForceHead = false;
            if (OzExpInGame.Heads == heads0) { md.AppendLine("INFO: no head burst within 12 s (this vehicle threw no head, or it landed low): case 4 not judged"); yield break; }
            Time.captureFramerate = (int)Fps;
            float t0 = Time.time; int next = 0; float peak = 0f, tGone = -1f, tPeak = 0f; int sn = 0;
            while (Time.time - t0 <= 5f)
            {
                yield return new WaitForEndOfFrame();
                float t = Time.time - t0, cv = Cover(Short, out int n); sn = Mathf.Max(sn, n);
                if (cv > peak) { peak = cv; tPeak = t; tGone = -1f; }
                if (peak > 0f && t > tPeak && tGone < 0f && cv < 0.01f * peak) tGone = t;
                if (next < FrameAt.Length && FrameAt[next] <= 4f && t >= FrameAt[next] - 0.5f / Fps)
                {
                    if (!string.IsNullOrEmpty(folder)) ScreenCapture.CaptureScreenshot(Path.Combine(folder, $"case4_head_t{FrameAt[next]:0.0}s.png".Replace(',', '.')));
                    next++;
                }
            }
            Time.captureFramerate = 0;
            md.AppendLine($"{OzExpInGame.LastLine}; short smoke peak {peak:0} m2 at {tPeak:0.00} s, gone at {tGone:0.00} s after the burst");
            Row("case 4: the head burst in the air (OzExpInGame.Heads +1)", OzExpInGame.Heads == heads0 + 1);
            Row("case 4: its smoke was the short one (QuickSmokes +1, short puffs " + sn + ")", OzExpInGame.QuickSmokes == q0 + 1 && OzExpInGame.LongSmokes == l0 && sn > 0);
            Row($"case 4: its short smoke gone by {ShortGoneMax:0.0} s (x sqrt of its scale at most 1): {tGone:0.00} s", tGone > 0f && tGone <= ShortGoneMax);
        }

        void Row(string what, bool pass) { md.AppendLine($"{(pass ? "PASS" : "FAIL")} {what}"); ok &= pass; }

        void Finish()
        {
            var g = ZGame.I;
            if (g) { g.blueBot = blue0; g.redBot = red0; }
            OzExpInGame.QuickSmoke = quick0; OzExpInGame.ForceHead = false;
            if (rts) rts.enabled = true;
            Time.captureFramerate = 0;
            md.AppendLine().AppendLine($"ALL: {(ok ? "PASS" : "FAIL")}");
            Report = md.ToString();
            if (!string.IsNullOrEmpty(folder)) File.WriteAllText(Path.Combine(folder, "report.md"), Report);
            Debug.Log(Tag + "\n" + Report);
            Destroy(this);
        }
    }
}
#endif
