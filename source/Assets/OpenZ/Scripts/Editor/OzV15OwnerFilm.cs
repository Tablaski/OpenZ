// OpenZ 1.5 (editor only, kit owner_film_1_5): the owner's clips and frames of 12 Nov in real levels, from ONE take list (Logs/oz_owner_film.txt).
// Menu OpenZ/V1.5/Owner Film/Run take list (MainMenu or a level, Play). For each take the level is deployed from the main menu when it is not
// the one playing (briefings off, the side, the difficulty and the options of the take; every setting is put back at the end), the opening is
// skipped, both computer players rest (unless "bots"), and the take is recorded frame-locked (30 frames a second, the game's own sound beside
// the frames) from the game's RTS camera ("rts") or a film camera on a slow orbit with a push-in ("orb"). Nothing in the game changes.
// Line 1 = the output folder (a trailing slash). Every further line is one take:
//   name | level | side r/b | light (DayNight.timeOfDay, -1 = as it is) | frames before | kept frames | camera | flags | events
//   camera: rts:x:z:height (the game camera, focus x,z)  |  orb:tx:ty:tz:distance:pitch:yaw:yaw rate:distance at the end (yaw 999 = looking toward the sun, 999 + n = n deg off it)
//   flags (comma): clean (hide the zone wash / flag rings, selection and range rings, every screen canvas), hud (keep the screen canvases),
//          wbefore (the water's new rows of kit water_fx_1_5 at 0 on instances = the look before the kit), splashold (OzWaterSplash.On false),
//          rain0 / rain1 (the NIGHT & RAIN option for the deploy), ship (MEDIUM + the SHUTTLE box: the player's ship), bots (computers play), follow
//          (the film camera's aim follows the playable shuttle)
//   events (;): f,B,type,x,z a test explosion (its damage) | f,W,kind,R/B/N,x,z[,hp] a vehicle | f,K,x,z kill the nearest vehicle (it burns)
//          | f,BR,x,z destroy the nearest bridge | f,SM,x,z the playable shuttle flies there | f,KS the playable shuttle destroyed outright
//          | f,SMR,dx,dz the playable shuttle flies dx, dz metres on | f,T,tx,ty,tz,distance the film camera's new aim | f,STORM a storm now
// Per take: f_00001.jpg ..., take.wav, done.txt, log lines in log.txt (output folder). Encoded off Unity (ffmpeg, low priority).
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using OpenZ.Sim;
using UnityEditor;
using UnityEngine;

namespace OpenZ
{
    public static class OzV15OwnerFilm
    {
        const string Tag = "[OpenZ owner film]";
        const string ListPath = "Logs/oz_owner_film.txt";
        static readonly CultureInfo ic = CultureInfo.InvariantCulture;
        static float F(string s) => float.Parse(s.Trim(), ic);
        static OzOwnerFilmHost runner;
        static object[] wlSaved;   // round 6 B: the level 18 water light numbers before a WL event

        [MenuItem("OpenZ/V1.5/Owner Film/Run take list (MainMenu or a level, Play)")]
        static void Film()
        {
            if (!Application.isPlaying) { Debug.LogWarning($"{Tag} enter Play first"); return; }
            if (!File.Exists(ListPath)) { Debug.LogWarning($"{Tag} no take list at {ListPath}"); return; }
            var lines = File.ReadAllLines(ListPath).Where(s => s.Trim().Length > 0 && !s.StartsWith("#")).ToArray();
            if (lines.Length < 2) { Debug.LogWarning($"{Tag} the take list is empty"); return; }
            string root = lines[0].Trim();
            if (File.Exists(root + "started.txt")) { Debug.LogWarning($"{Tag} this take list was already started ({root})"); return; }
            Directory.CreateDirectory(root); File.WriteAllText(root + "started.txt", "x");
            if (runner) Object.Destroy(runner.gameObject);
            var go = new GameObject("OZ_OwnerFilm"); Object.DontDestroyOnLoad(go);
            runner = go.AddComponent<OzOwnerFilmHost>();
            runner.StartCoroutine(Run(root, lines));
        }

        static bool Flying => ZodFlyover.Live != null && ZodFlyover.Live.Active;

        static IEnumerator Run(string root, string[] L)
        {
            var log = new StringBuilder();
            void Log(string s) { log.Append(System.DateTime.Now.ToString("HH:mm:ss ") + s + "\n"); File.WriteAllText(root + "log.txt", log.ToString()); }
            int prefBrief = PlayerPrefs.GetInt(ZodBriefing.PrefBriefing, 1); bool boxWas = OzPlayBlue.Box;
            int diffWas = PlayerPrefs.GetInt(OpenZDifficulty.Key, 2), mayWas = PlayerPrefs.GetInt(OpenZDifficulty.MayhemKey, 0);
            int headWas = PlayerPrefs.GetInt(OpenZDifficulty.HeadStartPref, 0), shipWas = PlayerPrefs.GetInt(OzShuttleStart.Pref, 1), rainWas = PlayerPrefs.GetInt(OzWeather.Pref, 1);
            bool splashWas = OzWaterSplash.On;
            int FPS = 30; int rate = AudioSettings.outputSampleRate; const int ch = 2; var eof = new WaitForEndOfFrame();
            for (int ci = 1; ci < L.Length; ci++)
            {
                var sh = L[ci].Split('|'); for (int i = 0; i < sh.Length; i++) sh[i] = sh[i].Trim();
                string nm = sh[0], level = sh[1]; bool blue = sh[2] == "b"; float tod = F(sh[3]); int pre = int.Parse(sh[4]), N = int.Parse(sh[5]);
                var cc = sh[6].Split(':'); var flags = new HashSet<string>((sh.Length > 7 ? sh[7] : "").Split(',').Select(x => x.Trim()).Where(x => x.Length > 0));
                var evs = sh.Length > 8 && sh[8].Length > 0 ? sh[8].Split(';') : new string[0];
                string dir = root + nm + "\\"; Directory.CreateDirectory(dir);
                // ---- the level (deployed from the main menu when it is not the one playing with the take's side and options)
                bool want = flags.Contains("ship"); int wantDiff = want ? 2 : 0;
                bool same = ZGame.I && ZGame.I.World != null && ZGame.I.level == level && (ZGame.I.player == Team.Blue) == blue
                            && PlayerPrefs.GetInt(OpenZDifficulty.Key, 2) == wantDiff && PlayerPrefs.GetInt(OzShuttleStart.Pref, 1) == (want ? 1 : 0)
                            && !flags.Contains("fresh") && (!flags.Contains("rain0") || PlayerPrefs.GetInt(OzWeather.Pref, 1) == 0) && (!flags.Contains("rain1") || PlayerPrefs.GetInt(OzWeather.Pref, 1) == 1);
                if (!same)
                {
                    if (!Object.FindFirstObjectByType<OpenZMainMenu>())
                    {
                        OzScenes.MainMenu(); float tm = Time.realtimeSinceStartup;
                        while (!Object.FindFirstObjectByType<OpenZMainMenu>() && Time.realtimeSinceStartup - tm < 40f) yield return new WaitForSecondsRealtime(0.2f);
                        yield return new WaitForSecondsRealtime(2f);
                    }
                    var m = Object.FindFirstObjectByType<OpenZMainMenu>();
                    if (!m) { Log(nm + ": no main menu, stopped"); break; }
                    PlayerPrefs.SetInt(ZodBriefing.PrefBriefing, 0);
                    PlayerPrefs.SetInt(OpenZDifficulty.Key, wantDiff); PlayerPrefs.SetInt(OpenZDifficulty.MayhemKey, 0);
                    PlayerPrefs.SetInt(OpenZDifficulty.HeadStartPref, 0); PlayerPrefs.SetInt(OzShuttleStart.Pref, want ? 1 : 0);
                    if (flags.Contains("rain0")) OzWeather.Enabled = false; else if (flags.Contains("rain1")) OzWeather.Enabled = true;
                    PlayerPrefs.Save(); OzPlayBlue.Box = blue;
                    int card = System.Array.FindIndex(OzLevelDef.All, d => d.Id == level);
                    if (card < 0) { Log(nm + ": no card " + level); continue; }
                    yield return new WaitForSecondsRealtime(0.8f); m.ProofSelect(); yield return new WaitForSecondsRealtime(0.8f); m.ProofFocus(card); yield return new WaitForSecondsRealtime(0.6f); m.ProofDeploy();
                    float t0 = Time.realtimeSinceStartup;
                    while ((!ZGame.I || ZGame.I.World == null || ZGame.I.level != level) && Time.realtimeSinceStartup - t0 < 120f) yield return new WaitForSecondsRealtime(0.2f);
                    if (!ZGame.I || ZGame.I.World == null) { Log(nm + ": " + level + " did not load"); continue; }
                    yield return new WaitForSecondsRealtime(2f);
                    for (int i = 0; i < 40; i++)
                    {
                        bool a = false;
                        try { a = ZodBriefing.ProofSkipBriefing() || OzBlueCameo.ProofSkipBriefing(); } catch (System.Exception) { }
                        if (a) break;
                        if (!ZodBriefing.BriefingActive && !OzBlueCameo.BriefingActive && !Flying && i > 4) break;
                        yield return new WaitForSecondsRealtime(0.5f);
                    }
                    float w0 = Time.realtimeSinceStartup; while (Flying && Time.realtimeSinceStartup - w0 < 60f) yield return new WaitForSecondsRealtime(0.5f);
                    yield return new WaitForSecondsRealtime(1.5f);
                    Log($"{nm}: {level} deployed, human {ZGame.I.player}, difficulty {wantDiff}, ship box {want}, weather {OzWeather.Enabled}");
                }
                var g = ZGame.I; var w = g.World; var rts = Object.FindFirstObjectByType<RtsCamera>(); var cam = Camera.main; var dn = Object.FindFirstObjectByType<DayNight>();
                bool clean = flags.Contains("clean"), hud = flags.Contains("hud") && !clean;
                bool hudOff = false;
                var hide = hud ? new List<Canvas>() : Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.isRootCanvas && c.enabled && c.renderMode != RenderMode.WorldSpace).ToList();
                var pin = Object.FindFirstObjectByType<PlayerInput>(); var hovP = typeof(PlayerInput).GetProperty("Hover");
                var offB = new List<Behaviour>();
                if (clean) foreach (var b in Object.FindObjectsByType<SelectionOverlay>(FindObjectsSortMode.None)) if (b.enabled) { b.enabled = false; offB.Add(b); }
                var music = new List<AudioSource>();
                foreach (var s in Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (s && s.gameObject.name.StartsWith("Music")) music.Add(s);
                void Quiet()
                {
                    foreach (var s in music) if (s) s.mute = true;
                    foreach (var c in hide) if (c) c.enabled = false;
                    if (!hud && OpenZHud.I && OpenZHud.I.hudVisible) { OpenZHud.I.SetVisible(false); hudOff = true; }
                    if (pin) { pin.enabled = false; if (hovP != null) hovP.SetValue(pin, null, null); pin.Selection.Clear(); }
                    if (clean)
                        foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                            if (r && (r.gameObject.name == "ZoneWash" || r.gameObject.name == "FlagRing")) r.gameObject.SetActive(false);
                }
                bool bb = g.blueBot, rb = g.redBot; if (!flags.Contains("bots")) { g.blueBot = false; g.redBot = false; }
                float tod0 = dn ? dn.timeOfDay : 0f; bool run0 = dn && dn.running; float h0 = rts ? rts.height : 70f; Vector3 focus0 = rts ? rts.focus : Vector3.zero;
                if (dn && tod >= 0f) { dn.running = false; dn.timeOfDay = tod; dn.Apply(tod); }
                OzWaterSplash.On = !flags.Contains("splashold");
                OzL18WaterClear.On = !flags.Contains("clear0");
                // wbefore: the water materials' new rows (kit water_fx_1_5) at 0 for this take, every value put back after it (the materials
                // are drawn by more than scene renderers, so the loaded materials themselves are set and restored)
                var saved = new List<(Material m, string p, Vector4 v, bool vec)>();
                if (flags.Contains("wbefore"))
                    foreach (var mi in Resources.FindObjectsOfTypeAll<Material>())
                    {
                        if (!mi || !mi.shader || !mi.shader.name.Contains("Water")) continue;
                        void SetF(string p) { if (mi.HasProperty(p)) { saved.Add((mi, p, new Vector4(mi.GetFloat(p), 0, 0, 0), false)); mi.SetFloat(p, 0f); } }
                        if (mi.HasProperty("_ProcDrift")) { saved.Add((mi, "_ProcDrift", mi.GetVector("_ProcDrift"), true)); mi.SetVector("_ProcDrift", Vector4.zero); }
                        SetF("_ProcMorph");
                        if (level != "L18") { SetF("_FireReflect"); SetF("_SunLane"); if (level == "L17") SetF("_RippleProc"); }
                    }
                var instances = saved;
                bool orb = cc[0] == "orb", follow = flags.Contains("follow");
                Vector3 tgt = Vector3.zero, goalT = Vector3.zero, velT = Vector3.zero; float dist = 60f, dEnd = 60f, pitch = 35f, yaw = 0f, yawRate = 0f, rtsH = 70f;
                if (orb) { tgt = goalT = new Vector3(F(cc[1]), F(cc[2]), F(cc[3])); dist = F(cc[4]); pitch = F(cc[5]); yaw = F(cc[6]); yawRate = F(cc[7]); dEnd = cc.Length > 8 ? F(cc[8]) : dist; if (rts) rts.enabled = false;
                       if (yaw > 900f && RenderSettings.sun) { var sf = RenderSettings.sun.transform.forward; yaw = Mathf.Atan2(-sf.x, -sf.z) * Mathf.Rad2Deg + (yaw - 999f);   Log($"{nm}: yaw toward the sun {yaw:0.0} deg (sun {RenderSettings.sun.name})"); } }
                else { tgt = new Vector3(F(cc[1]), 0f, F(cc[2])); rtsH = cc.Length > 3 ? F(cc[3]) : 70f; if (rts) { rts.enabled = true; rts.focus = tgt; rts.SetHeightImmediate(rtsH); } }
                float d0 = dist;
                var ship = w.PlayableShuttle; var shipView = OzPlayableShuttle.I;
                int breaks0 = OzShuttleBreakup.Breaks, pieces0 = OzShuttleBreakup.Pieces, air0 = OzShuttleAirBurst.Played;
                FPS = flags.Contains("fps60") ? 60 : 30;
                if (flags.Contains("q1440")) { OpenZ.EditorTools.OzV15UeBlastsPalette.GameViewSize(2560, 1440); for (int q = 0; q < 10; q++) yield return null; }   // q1440 = Game view 2560 x 1440 for this take (put back to 1920 x 1080 after it)
                var samples = new List<float>(N * rate / FPS * ch + 4096); bool audioOn = false;
                Time.captureFramerate = FPS;
                fw = null; Log($"{nm}: DV204 fort guns answered (ZWorld.FortGunAnswerDeviation) = {ZWorld.FortGunAnswerDeviation}");   // round 3 (R3.39)
                Log($"{nm}: start ({level}, side {(blue ? "blue" : "red")}, light {tod}, {pre} + {N} frames, camera {sh[6]}, flags {string.Join(",", flags)}, water rows set to 0: {saved.Count})");
                float sndT0 = Time.unscaledTime;
                for (int f0 = 0; ; f0++)
                {
                    Quiet();
                    int f = f0 - pre;
                    if (f == 0) audioOn = !flags.Contains("nosound") && AudioRenderer.Start();
                    if (f > 0)
                    {
                        var tex = ScreenCapture.CaptureScreenshotAsTexture();
                        if (tex)
                        {
                            var raw = tex.GetRawTextureData(); int twd = tex.width, th = tex.height; var fmt = tex.graphicsFormat; string path = dir + string.Format("f_{0:00000}.jpg", f);
                            Object.Destroy(tex);
                            System.Threading.Tasks.Task.Run(() => File.WriteAllBytes(path, ImageConversion.EncodeArrayToJPG(raw, fmt, (uint)twd, (uint)th, 0, 93)));
                        }
                        if (audioOn)
                        {
                            int avail = AudioRenderer.GetSampleCountForCaptureFrame();
                            var nb = new Unity.Collections.NativeArray<float>(Mathf.Max(0, avail) * ch, Unity.Collections.Allocator.Temp);
                            AudioRenderer.Render(nb);
                            for (int k = 0; k < nb.Length; k++) samples.Add(nb[k]);
                            nb.Dispose();
                        }
                    }
                    foreach (var es in evs)
                    {
                        var e = es.Trim().Split(','); if (e.Length < 2 || int.Parse(e[0]) != f) continue;
                        try { Event(g, w, e, ref goalT, ref dEnd, Log, nm); }
                        catch (System.Exception ex) { Log(nm + " event " + es + ": " + ex.Message); }
                    }
                    FortWatchStep(w, Log, nm, f, FPS);
                    if (orb)
                    {
                        if (follow && shipView && !shipView.BrokenUp) goalT = shipView.transform.position + Vector3.up * 2.6f;
                        tgt = Vector3.SmoothDamp(tgt, goalT, ref velT, follow ? 0.35f : 0.7f, Mathf.Infinity, 1f / FPS);
                        float k01 = N > 0 ? Mathf.Clamp01((float)Mathf.Max(0, f) / N) : 0f; k01 = k01 * k01 * (3f - 2f * k01);
                        dist = Mathf.Lerp(d0, dEnd, k01);
                        yaw += yawRate / FPS;
                        if (cam) { var rot = Quaternion.Euler(pitch, yaw, 0f); cam.transform.SetPositionAndRotation(tgt - rot * Vector3.forward * dist, rot); }
                        if (rts) rts.focus = new Vector3(tgt.x, 0f, tgt.z);
                    }
                    else if (rts)
                    {   // round 5 (dust_devil_trace_r5 frames): flag "devil" = the game camera follows DustDevil0 slowly (like a player keeping it in view)
                        if (flags.Contains("devil")) { var dd = GameObject.Find("DustDevil0"); if (dd) tgt = Vector3.SmoothDamp(tgt, new Vector3(dd.transform.position.x, 0f, dd.transform.position.z), ref velT, 2.5f, Mathf.Infinity, 1f / FPS); }
                        if (!flags.Contains("free")) rts.focus = tgt;   // round 6 A: flag free = a menu inside the take drives the game camera
                    }
                    if (f >= N) break;
                    yield return eof;
                }
                if (audioOn) AudioRenderer.Stop();
                Time.captureFramerate = 0;
                if (flags.Contains("q1440")) OpenZ.EditorTools.OzV15UeBlastsPalette.GameViewSize(1920, 1080);
                using (var fs = new FileStream(dir + "take.wav", FileMode.Create)) using (var bw = new BinaryWriter(fs))
                {
                    int bytes = samples.Count * 4;
                    bw.Write(Encoding.ASCII.GetBytes("RIFF")); bw.Write(36 + bytes); bw.Write(Encoding.ASCII.GetBytes("WAVE")); bw.Write(Encoding.ASCII.GetBytes("fmt ")); bw.Write(16); bw.Write((short)3); bw.Write((short)ch);
                    bw.Write(rate); bw.Write(rate * ch * 4); bw.Write((short)(ch * 4)); bw.Write((short)32); bw.Write(Encoding.ASCII.GetBytes("data")); bw.Write(bytes);
                    foreach (var s in samples) bw.Write(s);
                }
                if (ship != null)
                    Log($"{nm}: shuttle counters: break-ups {breaks0} -> {OzShuttleBreakup.Breaks}, pieces {pieces0} -> {OzShuttleBreakup.Pieces}, last in the air {OzShuttleBreakup.LastInAir}, plan {OzShuttleBreakup.LastHow}, exp_v2_7 bursts {air0} -> {OzShuttleAirBurst.Played} (ground deaths left alone {OzShuttleAirBurst.GroundLeft})");
                if (fw != null) { Log($"{nm}: FORT WATCH SUMMARY: {fw.Summary()}"); fw = null; }
                if (flags.Contains("sndlog")) { File.WriteAllText(dir + "sound_log.txt", OzSoundLog.Format(OzSoundLog.Since(sndT0))); Log($"{nm}: sound log {OzSoundLog.Since(sndT0).Count} lines -> sound_log.txt"); }   // round 4 (14 Nov)
                Log($"{nm}: end ({N} frames, {samples.Count / ch} samples)");
                File.WriteAllText(dir + "done.txt", "frames " + N + " fps " + FPS);
                // ---- put back what the take changed (the level stays for the next take)
                foreach (var s in saved) if (s.m) { if (s.vec) s.m.SetVector(s.p, s.v); else s.m.SetFloat(s.p, s.v.x); }
                foreach (var b in offB) if (b) b.enabled = true;
                g.blueBot = bb; g.redBot = rb;
                if (dn) { dn.timeOfDay = tod0; dn.Apply(tod0); dn.running = run0; }
                if (rts) { rts.enabled = true; rts.focus = focus0; rts.SetHeightImmediate(h0); }
                foreach (var c in hide) if (c) c.enabled = true;
                if (hudOff && OpenZHud.I) OpenZHud.I.SetVisible(true);
                foreach (var s in music) if (s) s.mute = false;
                if (pin) pin.enabled = true;
                yield return new WaitForSecondsRealtime(1f);
            }
            Time.captureFramerate = 0; OzWaterSplash.On = splashWas; OzL18WaterClear.On = true;
            PlayerPrefs.SetInt(ZodBriefing.PrefBriefing, prefBrief); OzPlayBlue.Box = boxWas;
            PlayerPrefs.SetInt(OpenZDifficulty.Key, diffWas); PlayerPrefs.SetInt(OpenZDifficulty.MayhemKey, mayWas);
            PlayerPrefs.SetInt(OpenZDifficulty.HeadStartPref, headWas); PlayerPrefs.SetInt(OzShuttleStart.Pref, shipWas); OzWeather.Enabled = rainWas == 1; PlayerPrefs.Save();
            Log($"all takes done; put back: briefing {prefBrief}, PLAY AS BLUE {boxWas}, difficulty {diffWas}, mayhem {mayWas}, head start {headWas}, shuttle box {shipWas}, weather {rainWas}");
            File.WriteAllText(root + "done.txt", "ok");
            Debug.Log($"{Tag} takes filmed: {root}");
        }

        // gun in its reach (WithinAttackRadius: with DV204 the tower's body), its first shot at the gun, every shot at the gun / elsewhere,
        // the gun's own shots, deaths; read from the world's events each frame (a new event list per frame is caught by its count).
        sealed class FortWatch
        {
            public ZObject gun; public Team side; public int seen, shotsGun, shotsOther, gunShots, lastCount; public double reachAt = -1, firstAt = -1, gunFirst = -1, gunDeadAt = -1, t0 = -1;
            public string reachBy = "", firstBy = ""; public readonly HashSet<int> units = new HashSet<int>(); public readonly List<string> deaths = new List<string>();
            public string Summary() =>
                $"tower gun {gun.Type}#{gun.Id}; units of the other side seen {units.Count}; in reach at {S(reachAt)} ({reachBy}); first shot at the gun {S(firstAt)} ({firstBy})"
                + $" = {(reachAt >= 0 && firstAt >= 0 ? (firstAt - reachAt).ToString("0.00", ic) + " s after in reach" : "-")}; shots at the gun {shotsGun}, at others with the gun in reach before the first {shotsOther};"
                + $" the gun's shots {gunShots} (first {S(gunFirst)}); gun destroyed {(gunDeadAt >= 0 ? "yes at " + S(gunDeadAt) : "no, health " + gun.Health)}; deaths: {(deaths.Count == 0 ? "none" : string.Join(", ", deaths))}";
            public string S(double t) => t < 0 ? "never" : (t - t0).ToString("0.00", ic) + " s";
        }
        static FortWatch fw;

        // the world's events live one frame: View/CombatView.cs clears them in its LateUpdate; a coroutine step after 'yield return null'
        // runs after every Update (the sim ticks in ZGame.Update) and before LateUpdate, so it sees each frame's events once
        static IEnumerator FortWatchEvents(FortWatch me, ZWorld w, System.Action<string> log, string nm)
        {
            while (fw == me)
            {
                foreach (var e in w.Events)
                {
                    if (e.type != ZEventType.MissileFired && e.type != ZEventType.Hitscan) continue;
                    var a = w.GetAny(e.objId); if (a == null) continue;
                    if (e.objId == me.gun.Id) { me.gunShots++; if (me.gunFirst < 0) { me.gunFirst = w.Time; var t = w.GetAny(e.otherId); log($"{nm} the tower gun's first shot at {(t != null ? t.Type + "#" + t.Id : "?")} (take {me.S(me.gunFirst)})"); } continue; }
                    if (a.Owner != me.side) continue;
                    if (e.otherId == me.gun.Id) { me.shotsGun++; if (me.firstAt < 0) { me.firstAt = w.Time; me.firstBy = a.Type + "#" + a.Id; log($"{nm} FIRST SHOT AT THE TOWER GUN by {me.firstBy} (take {me.S(me.firstAt)})"); } }
                    else { var o = w.GetAny(e.otherId); if (me.firstAt < 0 && me.reachAt >= 0) me.shotsOther++; log($"{nm} shot by {a.Type}#{a.Id} at {(o != null ? o.Type + "#" + o.Id : "?")} (take {me.S(w.Time)}, gun in reach {(me.reachAt >= 0 ? "yes" : "no")})"); }
                }
                yield return null;
            }
        }

        static void FortWatchStep(ZWorld w, System.Action<string> log, string nm, int f, int fps)
        {
            if (fw == null || w == null) return;
            if (fw.t0 < 0) fw.t0 = w.Time - (double)f / fps;   // take time 0 = recorded frame 0
            bool gunAlive = !fw.gun.Destroyed && fw.gun.KillAt < 0;
            if (fw.gunDeadAt < 0 && !gunAlive) { fw.gunDeadAt = w.Time; log($"{nm} f{f} tower gun destroyed (take {fw.S(fw.gunDeadAt)})"); }
            foreach (var o in w.Objects)
            {
                if (o == null || o.Owner != fw.side || !(o.Kind == ZKind.Vehicle || o.IsRobot) || o.IsShuttle) continue;
                var p = o.CenterX - fw.gun.CenterX; var q = o.CenterY - fw.gun.CenterY; if (p * p + q * q > 400 * 400) continue;
                if (fw.units.Add(o.Id)) log($"{nm} f{f} watching {o.Type}#{o.Id} ({o.CenterX},{o.CenterY}) health {o.Health}");
                if (o.Destroyed || o.KillAt >= 0) continue;
                if (fw.reachAt < 0 && gunAlive && o.CanAttackObject(fw.gun) && o.WithinAttackRadius(fw.gun))
                { fw.reachAt = w.Time; fw.reachBy = o.Type + "#" + o.Id; log($"{nm} f{f} IN REACH: {fw.reachBy} at ({o.CenterX},{o.CenterY}), {Mathf.Sqrt(p * p + q * q):0} px from the gun centre (take {fw.S(fw.reachAt)})"); }
            }
            if (f % 15 == 0)
                foreach (var id in fw.units)
                {
                    var o = w.GetAny(id); if (o == null || o.Destroyed) continue;
                    double ddx = o.CenterX - fw.gun.CenterX, ddy = o.CenterY - fw.gun.CenterY;
                    log($"{nm} f{f} trace {o.Type}#{o.Id} ({o.CenterX},{o.CenterY}) d {System.Math.Sqrt(ddx * ddx + ddy * ddy):0} reach {(o.WithinAttackRadius(fw.gun) ? 1 : 0)} hp {o.Health} wp {string.Join(",", o.Waypoints.Select(q => q.mode + ":" + q.refId + (q.playerGiven ? "p" : "")))} att {(o.AttackObject == null ? "-" : o.AttackObject.Type + "#" + o.AttackObject.Id)} | gun hp {fw.gun.Health} on {(fw.gun.AttackObject == null ? "-" : fw.gun.AttackObject.Type + "#" + fw.gun.AttackObject.Id)}");
                }
            foreach (var id in fw.units)
            {
                var o = w.GetAny(id); string key = "#" + id + " ";
                if (fw.deaths.Any(x => x.Contains(key))) continue;
                if (o == null || o.Destroyed || o.KillAt >= 0 || !w.Objects.Contains(o)) { string d = (o != null ? o.Type : "unit") + key + "at " + fw.S(w.Time); fw.deaths.Add(d); log($"{nm} f{f} died: {d}"); }
            }
        }

        static IEnumerator ZoomRamp(RtsCamera rc, float h0, float h1, int nf)
        {   // round 4 (A7): one height step a frame (the takes are frame-locked), smoothstep eased
            for (int i = 0; i <= nf && rc; i++) { float k = (float)i / Mathf.Max(1, nf); k = k * k * (3f - 2f * k); rc.SetHeightImmediate(Mathf.Lerp(h0, h1, k)); yield return null; }
        }

        static ZObject Nearest(ZWorld w, Vector3 at, System.Func<ZObject, bool> ok)
        {
            ZObject best = null; float bd = float.MaxValue;
            foreach (var o in w.Objects)
            {
                if (o == null || o.Destroyed || !ok(o)) continue;
                var p = ZGame.PxToWorld(o.CenterX, o.CenterY); float d = (p.x - at.x) * (p.x - at.x) + (p.z - at.z) * (p.z - at.z);
                if (d < bd) { bd = d; best = o; }
            }
            return best;
        }

        static void Event(ZGame g, ZWorld w, string[] e, ref Vector3 goalT, ref float dEnd, System.Action<string> log, string nm)
        {
            switch (e[1])
            {
                case "KF": { bool ok = EditorApplication.ExecuteMenuItem("OpenZ/V1.5/Crane Restore/2 Film: factory repair (Play, a level; record with OBS)"); log($"{nm} f{e[0]} kit factory film started {ok}"); break; }
                case "KB": { bool ok = EditorApplication.ExecuteMenuItem("OpenZ/V1.5/Crane Restore/3 Film: long bridge destroyed + crane repair (Play, a level; record with OBS)"); log($"{nm} f{e[0]} kit bridge film started {ok}"); break; }
                case "WC": { OzWaterPlayCalm.Before = e[2].Trim() == "1"; log($"{nm} f{e[0]} water_tune_r3 Before = {OzWaterPlayCalm.Before}"); break; }   // f,WC,1 = the water as before kit water_tune_r3, f,WC,0 = after
                // DT = kit dust_devil_trace_r5 (f,DT,0/1); DS = its Status(); GQ = graphics quality (f,GQ,0 HIGH / 1 MEDIUM / 2 LOW, saved: put back with another GQ); BS = 'bridge repaired' counters
                case "RF": { var md = typeof(OzWaterPlayCalm).GetField("ObjReflectMaxDeg"); if (md != null) md.SetValue(null, F(e[2])); OzWaterPlayCalm.ObjReflect = F(e[3]); OzWaterPlayCalm.ObjReflectBend = F(e[4]); log($"{nm} f{e[0]} object reflections maxdeg {(md != null ? md.GetValue(null) : "n/a")} strength {OzWaterPlayCalm.ObjReflect} bend {OzWaterPlayCalm.ObjReflectBend} (session only)"); break; }   // reflection: works with and without kit object_reflections_r5
                case "OF": { OzOutfallFoam.On = e[2].Trim() == "1"; log($"{nm} f{e[0]} OzOutfallFoam.On = {OzOutfallFoam.On}"); break; }
                case "OS": { log($"{nm} f{e[0]} {OzOutfallFoam.Status()}"); break; }
                case "DT": { OzDustDevilTrace.On = e[2].Trim() == "1"; log($"{nm} f{e[0]} OzDustDevilTrace.On = {OzDustDevilTrace.On}"); break; }
                case "DS": { log($"{nm} f{e[0]} {OzDustDevilTrace.Status()}"); break; }
                case "GQ": { OzQuality.Level = int.Parse(e[2].Trim()); log($"{nm} f{e[0]} graphics quality {OzQuality.Name}"); break; }
                case "MN": { string mp = string.Join(",", e.Skip(2)); bool ok = EditorApplication.ExecuteMenuItem(mp); log($"{nm} f{e[0]} menu '{mp}' started {ok}"); break; }   // round 5: f,MN,<menu path> (a Play check menu inside a take)
                case "BS": { log($"{nm} f{e[0]} bridge repaired: Rachel said {OzBridgeSaidOnce.RachelSaid}, robot dropped {OzBridgeSaidOnce.RobotDropped}, Rachel dropped {OzBridgeSaidOnce.RachelDropped}, same-cluster dropped {OzBridgeSaidOnce.ClusterDropped}"); break; }
                // EN = f,EN,x,z,tx,tz the nearest robot leader / vehicle to (x, z) gets the player's ENTER order on the nearest gun / vehicle to (tx, tz); BD = kit bridge_death_exp_r3's Report()
                case "WW": { OzWadeWake.On = OzWadeRipples.On = e[2].Trim() == "1"; log($"{nm} f{e[0]} wake switches OzWadeWake.On = OzWadeRipples.On = {OzWadeWake.On}"); break; }
                case "WS": { log($"{nm} f{e[0]} {OzWadeWake.Status()} || {OzWadeRipples.Status()}"); break; }
                case "BD": { log($"{nm} f{e[0]} {OzBridgeDeathExp.Report()}"); break; }
                case "AB":
                {   // round 4 (A5): f,AB,x,z,tx,tz the nearest vehicle / robot leader to (x, z) gets the player's ATTACK order on the nearest standing bridge to (tx, tz)
                    var u = Nearest(w, new Vector3(F(e[2]), 0f, F(e[3])), x => (x.Kind == ZKind.Vehicle && !x.IsShuttle && x.Owner != Team.Neutral) || (x.IsRobot && !x.IsMinion && x.Owner != Team.Neutral));
                    var tg = Nearest(w, new Vector3(F(e[4]), 0f, F(e[5])), x => x.IsBridge && !x.Destroyed);
                    bool ok = u != null && tg != null && w.SendWaypoints(u.Owner, u, new[] { new ZWaypoint { mode = WpMode.Attack, refId = tg.Id, x = tg.CenterX, y = tg.CenterY, playerGiven = true } });
                    log($"{nm} f{e[0]} attack bridge: {(u != null ? u.Type + "#" + u.Id : "no unit")} -> {(tg != null ? tg.Type + "#" + tg.Id + " health " + tg.Health + " of " + tg.MaxHealth : "none")} ok={ok}"); break;
                }
                case "ZR":
                {   // round 4 (A7): f,ZR,h0,h1,frames = the game camera's height from h0 to h1 over that many frames (eased), as a slow wheel zoom
                    var rc = Object.FindFirstObjectByType<RtsCamera>(); float h0 = F(e[2]), h1 = F(e[3]); int nf = int.Parse(e[4]);
                    if (rc && runner) runner.StartCoroutine(ZoomRamp(rc, h0, h1, nf));
                    log($"{nm} f{e[0]} zoom {h0} -> {h1} m over {nf} frames"); break;
                }
                case "GF": { OzL16GravelTracks.Far = e[2].Trim() == "0" ? new Vector2(0.075f, 0.1f) : new Vector2(1000f, 1001f); OzL16GravelTracks.Apply(); log($"{nm} f{e[0]} L16 gravel paths Far = {OzL16GravelTracks.Far} ({(e[2].Trim() == "0" ? "BEFORE = exe 13c" : "AFTER kit factory_path_zoom_r3")})"); break; }   // round 4 (A7)
                case "OR": { float was = OzWaterPlayCalm.ObjReflect; OzWaterPlayCalm.ObjReflect = F(e[2]); log($"{nm} f{e[0]} water_tune_r3 ObjReflect {was} -> {OzWaterPlayCalm.ObjReflect} (session only; put it back with another OR)"); break; }   // round 4: B3 object reflections test
                case "EN":
                {
                    var u = Nearest(w, new Vector3(F(e[2]), 0f, F(e[3])), x => (x.IsRobot && !x.IsMinion && x.Owner != Team.Neutral) || (x.Kind == ZKind.Vehicle && !x.IsShuttle && x.Owner != Team.Neutral));
                    var tg = Nearest(w, new Vector3(F(e[4]), 0f, F(e[5])), x => (x.Kind == ZKind.Cannon && !x.IsTowerGun) || (x.Kind == ZKind.Vehicle && !x.IsShuttle));
                    bool ok = u != null && tg != null && w.SendWaypoints(u.Owner, u, new[] { new ZWaypoint { mode = WpMode.Enter, refId = tg.Id, x = tg.CenterX, y = tg.CenterY, playerGiven = true } });
                    log($"{nm} f{e[0]} enter: {(u != null ? u.Type + "#" + u.Id + " " + u.Owner : "no unit")} -> {(tg != null ? tg.Type + "#" + tg.Id + " " + tg.Owner + " tile (" + (tg.CenterX >> 4) + "," + (tg.CenterY >> 4) + ")" : "none")} ok={ok}"); break;
                }
                case "B": { bool ok = OzUeBlasts.Drop(g, w, int.Parse(e[2]), new Vector3(F(e[3]), 0f, F(e[4])), out string why); log($"{nm} f{e[0]} blast {e[2]} ok={ok} {why}"); break; }
                case "W":
                {
                    var px = ZGame.WorldToPx(new Vector3(F(e[4]), 0f, F(e[5]))); int tx = (px.x >> 4) - 1, ty = (px.y >> 4) - 1;
                    var o = w.CreateObject("vehicle", e[2], tx * 16, ty * 16, e[3] == "R" ? Team.Red : e[3] == "N" ? Team.Neutral : Team.Blue);
                    if (o != null) { g.SpawnUnitView(o); if (e.Length > 6) o.Health = Mathf.Min(o.Health, int.Parse(e[6])); }
                    log($"{nm} f{e[0]} vehicle {e[2]} {(o != null ? "made, health " + o.Health : "NOT made")}"); break;
                }
                case "TG":
                {
                    // nearest to (x, z), the way ZWorld.PlaceCannon puts a cannon there (not ejectable); then the fort watch on it starts
                    var team = e[2] == "R" ? Team.Red : Team.Blue; var at = ZGame.WorldToPx(new Vector3(F(e[4]), 0f, F(e[5])));
                    ZObject best = null; int bx = 0, by = 0; long bd = long.MaxValue;
                    foreach (var fo in w.Objects)
                        if (fo != null && fo.IsFort && fo.Owner == team && !fo.Destroyed)
                            foreach (var (ox, oy) in new[] { (16, 0), (16, 48), (112, 0), (112, 48) })
                            { long dx = fo.LocX + ox + 16 - at.x, dy = fo.LocY + oy + 16 - at.y, d = dx * dx + dy * dy; if (d < bd) { bd = d; best = fo; bx = fo.LocX + ox; by = fo.LocY + oy; } }
                    ZObject c = null;
                    if (best != null) { c = w.CreateObject("cannon", e[3], bx, by, team); if (c != null) { c.Ejectable = false; g.SpawnUnitView(c); fw = new FortWatch { gun = c, side = team == Team.Blue ? Team.Red : Team.Blue }; if (runner) runner.StartCoroutine(FortWatchEvents(fw, w, log, nm)); } }
                    log($"{nm} f{e[0]} tower gun {e[3]} {(c != null ? "#" + c.Id + " at px (" + bx + "," + by + ") on fort #" + best.Id + ", health " + c.Health + ", tower gun " + c.IsTowerGun : "NOT made")}"); break;
                }
                case "TW":
                {
                    // (level 7's blue fort carries a missile cannon on turret spot (112, 48) from the start)
                    var gun = Nearest(w, new Vector3(F(e[2]), 0f, F(e[3])), x => x.IsTowerGun);
                    if (gun != null) { fw = new FortWatch { gun = gun, side = gun.Owner == Team.Blue ? Team.Red : Team.Blue }; if (runner) runner.StartCoroutine(FortWatchEvents(fw, w, log, nm)); }
                    log($"{nm} f{e[0]} watching the tower gun {(gun != null ? gun.Type + "#" + gun.Id + " (" + gun.Owner + ") centre px (" + gun.CenterX + "," + gun.CenterY + "), range " + gun.AttackRadius + " px, health " + gun.Health : "NONE")}"); break;
                }
                case "CLR":
                {
                    // radius taken off quietly (robots that are not fort defenders, vehicles, guns that are not tower guns): the fort's start squads
                    // otherwise kill any unit at the gate before it is near the tower; the fort, its roof defenders and its tower guns stay
                    var team = e[2] == "R" ? Team.Red : Team.Blue; var at = ZGame.WorldToPx(new Vector3(F(e[3]), 0f, F(e[4]))); long r = (long)(F(e[5]) * 8f); var sb = new StringBuilder();
                    foreach (var o in w.Objects)
                    {
                        if (o == null || o.Owner != team || o.Destroyed) continue;
                        bool pick = (o.IsRobot && !o.IsFortDefender) || (o.Kind == ZKind.Vehicle && !o.IsShuttle) || (o.Kind == ZKind.Cannon && !o.IsTowerGun);
                        long dx = o.CenterX - at.x, dy = o.CenterY - at.y; if (!pick || dx * dx + dy * dy > r * r) continue;
                        o.KillAt = w.Time; sb.Append(o.Type + "#" + o.Id + " ");
                    }
                    log($"{nm} f{e[0]} cleared ({team}, {F(e[5]):0} m): {sb}"); break;
                }
                case "RG":
                {
                    var px = ZGame.WorldToPx(new Vector3(F(e[4]), 0f, F(e[5])));
                    var o = w.CreateRobotGroup(e[2], px.x - 8, px.y - 8, e[3] == "R" ? Team.Red : Team.Blue);
                    if (o != null) { g.SpawnUnitView(o); foreach (var m in o.Minions) g.SpawnUnitView(m); }
                    log($"{nm} f{e[0]} robot group {e[2]} {(o != null ? "#" + o.Id + " + " + o.Minions.Count : "NOT made")}"); break;
                }
                case "MV":
                {
                    // (right-click on the ground: ZWaypoint.Move, View/PlayerInput.cs) to (tx, tz); 'a' = attack-move
                    var u = Nearest(w, new Vector3(F(e[2]), 0f, F(e[3])), x => (x.Kind == ZKind.Vehicle && !x.IsShuttle) || (x.IsRobot && !x.IsMinion && x.Owner != Team.Neutral));
                    var to = ZGame.WorldToPx(new Vector3(F(e[4]), 0f, F(e[5])));
                    bool ok = u != null && w.SendWaypoints(u.Owner, u, new[] { ZWaypoint.Move(to.x, to.y, e.Length > 6 && e[6] == "a") });
                    log($"{nm} f{e[0]} move: {(u != null ? u.Type + "#" + u.Id + " (" + u.CenterX + "," + u.CenterY + ")" : "no unit")} -> px ({to.x},{to.y}) ok={ok}"); break;
                }
                case "U":
                {
                    var px = ZGame.WorldToPx(new Vector3(F(e[4]), 0f, F(e[5]))); int tx = px.x >> 4, ty = px.y >> 4;
                    var o = w.CreateObject("robot", e[2], tx * 16, ty * 16, e[3] == "R" ? Team.Red : e[3] == "N" ? Team.Neutral : Team.Blue);
                    if (o != null) g.SpawnUnitView(o);
                    log($"{nm} f{e[0]} robot {e[2]} {(o != null ? "made" : "NOT made")}"); break;
                }
                case "A":
                {
                    // to (tx, tz) (a container: an explosive-only target) the player's own way (an Attack waypoint); hp = that item's health first (staging)
                    var u = Nearest(w, new Vector3(F(e[2]), 0f, F(e[3])), x => x.Kind == ZKind.Vehicle && !x.IsShuttle);
                    var tg = Nearest(w, new Vector3(F(e[4]), 0f, F(e[5])), x => x.Kind == ZKind.MapItem && !x.IsBridge && x.Health > 0);
                    if (tg != null && e.Length > 6) tg.SetHealth(int.Parse(e[6]));
                    bool ok = u != null && tg != null && w.SendWaypoints(u.Owner, u, new[] { new ZWaypoint { mode = WpMode.Attack, refId = tg.Id, x = tg.CenterX, y = tg.CenterY, playerGiven = true } });
                    log($"{nm} f{e[0]} attack: {(u != null ? u.Type + "#" + u.Id : "no unit")} -> {(tg != null ? tg.Type + "#" + tg.Id + " health " + tg.Health : "no target")} ok={ok}"); break;
                }
                case "K":
                {
                    var o = Nearest(w, new Vector3(F(e[2]), 0f, F(e[3])), x => x.Kind == ZKind.Vehicle && !x.IsShuttle);
                    if (o != null) { o.DamageHealth(o.Health); w.UpdateObjectHealth(o); }
                    log($"{nm} f{e[0]} kill {(o != null ? o.Type + "#" + o.Id : "nothing")}"); break;
                }
                case "BR":
                {
                    var o = Nearest(w, new Vector3(F(e[2]), 0f, F(e[3])), x => x.IsBridge);
                    if (o != null) { o.SetHealth(0); }
                    log($"{nm} f{e[0]} bridge {(o != null ? o.Type + "#" + o.Id + " at tile (" + (o.CenterX >> 4) + "," + (o.CenterY >> 4) + ")" : "none")}"); break;
                }
                case "RB":
                {
                    ZObject o = null; float od = float.MaxValue; var rpx = ZGame.WorldToPx(new Vector3(F(e[2]), 0f, F(e[3])));   // Nearest() skips destroyed objects
                    foreach (var x in w.Objects) { if (x == null || !x.IsBridge || !x.Destroyed) continue; float dx = x.CenterX - rpx.x, dy = x.CenterY - rpx.y, d = dx * dx + dy * dy; if (d < od) { od = d; o = x; } }
                    var ar = typeof(ZWorld).GetMethod("AutoRepair", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    if (o != null && ar != null) ar.Invoke(w, new object[] { o });
                    log($"{nm} f{e[0]} bridge repaired: {(o != null ? o.Type + "#" + o.Id + " " + o.Health + "/" + o.MaxHealth : "none")}"); break;
                }
                case "BRK":
                {
                    var o = Nearest(w, new Vector3(F(e[2]), 0f, F(e[3])), x => x.IsBridge && !x.Destroyed);
                    int before = 0; foreach (var x in w.Objects) if (x != null && !x.Destroyed && (x.Kind == ZKind.Robot || x.Kind == ZKind.Vehicle)) before++;
                    if (o != null) { o.DamageHealth(o.Health); w.UpdateObjectHealth(o); }
                    int after = 0; foreach (var x in w.Objects) if (x != null && !x.Destroyed && (x.Kind == ZKind.Robot || x.Kind == ZKind.Vehicle)) after++;
                    log($"{nm} f{e[0]} bridge destroyed: {(o != null ? o.Type + "#" + o.Id : "none")}, robots + vehicles alive {before} -> {after}"); break;
                }
                case "WR":
                {
                    var px = ZGame.WorldToPx(new Vector3(F(e[4]), 0f, F(e[5]))); int tx = (px.x >> 4), ty = (px.y >> 4);
                    var o = w.CreateObject("robot", e[2], tx * 16, ty * 16, e[3] == "R" ? Team.Red : Team.Blue);
                    if (o != null) g.SpawnUnitView(o);
                    log($"{nm} f{e[0]} robot {e[2]} {(o != null ? "made #" + o.Id : "NOT made")}"); break;
                }
                case "SL":
                {   // a piece lands in open water by the game's own way (OzWaterLanding.Splash; size 0 small, 1 medium, 2 large): the look as OzWaterSplash.On says
                    var p = new Vector3(F(e[2]), 0f, F(e[3])); bool water = OzWaterLanding.OpenWaterAt(p, out float wy);
                    var size = e.Length > 4 && e[4] == "0" ? OzWaterSize.Small : e.Length > 4 && e[4] == "1" ? OzWaterSize.Medium : OzWaterSize.Large;
                    if (water) OzWaterLanding.Splash(new Vector3(p.x, wy, p.z), size, "owner film", 6f);
                    log($"{nm} f{e[0]} splash {size} at ({p.x:0.0}, {p.z:0.0}): open water {water}, new look {OzWaterSplash.On}"); break;
                }
                case "SJ":
                {   // the briefing's column (OzWaterSplash.Jet, strength 1): drawn only with the new look
                    var p = new Vector3(F(e[2]), 0f, F(e[3])); bool water = OzWaterLanding.OpenWaterAt(p, out float wy);
                    bool ok = water && OzWaterSplash.Jet(new Vector3(p.x, wy, p.z), 1f, 0f, OpenZFxCatalog.RippleColorFor(g.level));
                    log($"{nm} f{e[0]} column at ({p.x:0.0}, {p.z:0.0}): open water {water}, drawn {ok}"); break;
                }
                case "BF":
                {   // the nearest bridge whole again (its health to full: a frame of the intact model)
                    var o = Nearest(w, new Vector3(F(e[2]), 0f, F(e[3])), x => x.IsBridge || false);
                    if (o == null) foreach (var x in w.Objects) if (x != null && x.IsBridge) { var p = ZGame.PxToWorld(x.CenterX, x.CenterY); if (o == null || (p - new Vector3(F(e[2]), 0f, F(e[3]))).sqrMagnitude < (ZGame.PxToWorld(o.CenterX, o.CenterY) - new Vector3(F(e[2]), 0f, F(e[3]))).sqrMagnitude) o = x; }
                    if (o != null) { o.SetHealth(o.MaxHealth); w.UpdateObjectHealth(o); }
                    log($"{nm} f{e[0]} bridge whole: {(o != null ? o.Type + "#" + o.Id + " health " + o.Health + " of " + o.MaxHealth : "none")}"); break;
                }
                case "CR":
                {   // the nearest crane of the side (R / B) repairs the nearest bridge
                    var at = new Vector3(F(e[3]), 0f, F(e[4])); var side = e[2] == "B" ? Team.Blue : Team.Red;
                    var c = Nearest(w, at, x => x.Type == "crane" && x.Owner == side);
                    ZObject br = null; float bd = float.MaxValue;
                    foreach (var x in w.Objects) if (x != null && x.IsBridge) { var p = ZGame.PxToWorld(x.CenterX, x.CenterY); float d = (p.x - at.x) * (p.x - at.x) + (p.z - at.z) * (p.z - at.z); if (d < bd) { bd = d; br = x; } }
                    bool ok = c != null && br != null && w.SendWaypoints(side, c, new[] { new ZWaypoint { mode = WpMode.CraneRepair, refId = br.Id, x = br.CenterX, y = br.CenterY, playerGiven = true } });
                    log($"{nm} f{e[0]} crane {(c != null ? "#" + c.Id : "none")} repairs bridge {(br != null ? br.Type + "#" + br.Id + " at tile (" + (br.CenterX >> 4) + "," + (br.CenterY >> 4) + "), health " + br.Health + " of " + br.MaxHealth + ", destroyed " + br.Destroyed : "none")}: order ok={ok}"); break;
                }
                case "BL":
                {   // every bridge's health (the counted repair check)
                    var sb = new StringBuilder();
                    foreach (var x in w.Objects) if (x != null && x.IsBridge) sb.Append($" {x.Type}#{x.Id} tile ({x.CenterX >> 4},{x.CenterY >> 4}) {x.Health}/{x.MaxHealth}{(x.Destroyed ? " destroyed" : "")};");
                    log($"{nm} f{e[0]} bridges:{sb}"); break;
                }
                case "SM":
                {
                    var s = w.PlayableShuttle; if (s == null) { log($"{nm} f{e[0]} no playable shuttle"); break; }
                    var px = ZGame.WorldToPx(new Vector3(F(e[2]), 0f, F(e[3]))); var (qx, qy) = w.ShuttleClamp(px.x, px.y);
                    bool ok = w.SendWaypoints(s.Owner, s, new[] { new ZWaypoint { mode = WpMode.Move, x = qx, y = qy, playerGiven = true } });
                    log($"{nm} f{e[0]} shuttle #{s.Id} to ({qx >> 4},{qy >> 4}) ok={ok}"); break;
                }
                case "SMR":
                {   // the playable shuttle flies dx, dz metres from where it stands
                    var s = w.PlayableShuttle; if (s == null) { log($"{nm} f{e[0]} no playable shuttle"); break; }
                    int dx = Mathf.RoundToInt(F(e[2]) * 8f), dy = Mathf.RoundToInt(-F(e[3]) * 8f); var (qx, qy) = w.ShuttleClamp(s.CenterX + dx, s.CenterY + dy);
                    bool ok = w.SendWaypoints(s.Owner, s, new[] { new ZWaypoint { mode = WpMode.Move, x = qx, y = qy, playerGiven = true } });
                    log($"{nm} f{e[0]} shuttle #{s.Id} from ({s.CenterX >> 4},{s.CenterY >> 4}) to ({qx >> 4},{qy >> 4}) ok={ok}"); break;
                }
                case "KS":
                {
                    var s = w.PlayableShuttle; var v = OzPlayableShuttle.I; if (s == null) { log($"{nm} f{e[0]} no playable shuttle"); break; }
                    log($"{nm} f{e[0]} shuttle #{s.Id} destroyed outright: state {s.ShuttleState}, airborne {s.ShuttleAirborne}, pads {(v ? v.FeetHeight : -1f):0.0} m over the ground");
                    OzShuttleAim.LastHitAt = -99f; s.DamageHealth(s.Health); w.UpdateObjectHealth(s); break;
                }
                case "T": goalT = new Vector3(F(e[2]), F(e[3]), F(e[4])); if (e.Length > 5) dEnd = F(e[5]); break;
                case "SF":
                {   // round 6 A (14 Nov): f,SF,Type,Field,value = a public static number / switch of an OpenZ view type set for the takes (tuning on frames)
                    var t = typeof(ZGame).Assembly.GetType("OpenZ." + e[2].Trim()); var fi = t != null ? t.GetField(e[3].Trim(), System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static) : null;
                    object was = fi != null ? fi.GetValue(null) : null;
                    if (fi != null) fi.SetValue(null, fi.FieldType == typeof(bool) ? (object)(e[4].Trim() == "1" || e[4].Trim().ToLower() == "true") : fi.FieldType == typeof(int) ? (object)int.Parse(e[4].Trim()) : (object)F(e[4]));
                    log($"{nm} f{e[0]} set {e[2]}.{e[3]}: {was} -> {(fi != null ? fi.GetValue(null) : "NO SUCH FIELD")}"); break;
                }
                case "ST":
                {   // round 6 A: f,ST,Type[,Method] = log Type.Status() (or Report() / the named static method)
                    var t = typeof(ZGame).Assembly.GetType("OpenZ." + e[2].Trim()); string mn = e.Length > 3 ? e[3].Trim() : null;
                    var mi = t == null ? null : (mn != null ? t.GetMethod(mn, System.Type.EmptyTypes) : (t.GetMethod("Status", System.Type.EmptyTypes) ?? t.GetMethod("Report", System.Type.EmptyTypes)));
                    log($"{nm} f{e[0]} {e[2]}: {(mi != null ? mi.Invoke(null, null) : "no status method")}"); break;
                }
                case "FW": { File.WriteAllText(e[2].Trim(), string.Join(",", e.Skip(3))); log($"{nm} f{e[0]} wrote {e[2]}"); break; }   // round 6 A: f,FW,path,text (a menu's input file)
                case "DD":
                {   // round 6 A: f,DD,i,x,z,deg = dust devil i placed at (x, z), heading deg (staging: over open sand); it wanders on by itself
                    var keeper = Resources.FindObjectsOfTypeAll<OzDustDevils>().FirstOrDefault(k => k && k.gameObject.scene.IsValid() && k.gameObject.activeInHierarchy); var fl = typeof(OzDustDevils).GetField("devils", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);   // FindFirstObjectByType misses OZ_DustDevils (batch R6b)
                    var list = keeper && fl != null ? fl.GetValue(keeper) as System.Collections.IList : null; int i = int.Parse(e[2]);
                    if (list != null && i < list.Count) { var d = list[i]; var dt = d.GetType(); var root = (Transform)dt.GetField("root").GetValue(d); root.position = new Vector3(F(e[3]), root.position.y, F(e[4])); dt.GetField("dir").SetValue(d, Quaternion.Euler(0f, F(e[5]), 0f) * Vector3.forward); }
                    log($"{nm} f{e[0]} dust devil {e[2]} {(list != null && i < list.Count ? "placed" : "NOT found")} at ({e[3]}, {e[4]}) heading {e[5]}"); break;
                }
                case "UP":
                {   // round 6 A: f,UP,x,z = where the nearest robot group / vehicle is (with its squad) (the WADE count)
                    var u = Nearest(w, new Vector3(F(e[2]), 0f, F(e[3])), x => (x.IsRobot && !x.IsMinion) || (x.Kind == ZKind.Vehicle && !x.IsShuttle));
                    var sb = new StringBuilder();
                    if (u != null) { var p = ZGame.PxToWorld(u.CenterX, u.CenterY); sb.Append($"{u.Type}#{u.Id} {u.Owner} at ({p.x:0.0}, {p.z:0.0}) hp {u.Health}"); foreach (var m in u.Minions) if (m != null) { var q = ZGame.PxToWorld(m.CenterX, m.CenterY); sb.Append($"; #{m.Id} ({q.x:0.0}, {q.z:0.0}){(m.Destroyed ? " dead" : "")}"); } }
                    log($"{nm} f{e[0]} unit: {(u != null ? sb.ToString() : "none")}"); break;
                }
                case "WL":
                {   // round 6 B (14 Nov, A15): f,WL,mode = level 18 water light look: 2 = as a briefing camera draws it (INTRO), 1 = as test exe 14b (PLAY BEFORE), 0 = now (PLAY AFTER)
                    if (wlSaved == null) wlSaved = new object[] { OzWaterPlayCalm.L18Calm, OzWaterPlayCalm.L18Ripple, OzWaterPlayCalm.L18GlowCap, OzWaterPlayCalm.ObjReflect };
                    int md = int.Parse(e[2].Trim());
                    OzWaterPlayCalm.L18Calm = md == 2 ? Vector4.zero : (Vector4)wlSaved[0]; OzWaterPlayCalm.L18Ripple = md == 2 ? 0f : (float)wlSaved[1];
                    OzWaterPlayCalm.L18GlowCap = md == 2 ? 3f : (float)wlSaved[2]; OzWaterPlayCalm.ObjReflect = md == 2 ? 0f : (float)wlSaved[3]; OzWaterPlayCalm.Exe14b = md == 1;
                    log($"{nm} f{e[0]} L18 water light look {(md == 2 ? "INTRO" : md == 1 ? "PLAY BEFORE (exe 14b)" : "PLAY AFTER")}: calm {OzWaterPlayCalm.L18Calm}, glow cap {OzWaterPlayCalm.L18GlowCap}"); break;
                }
                case "FL":
                {   // round 6 B: f,FL,0/1 = a lightning flash now (0 close, 1 mid)
                    if (OzWeather.I) { OzWeather.I.ForceFlash(e[2].Trim() == "1" ? OzStormPlan.Mid : OzStormPlan.Close); log($"{nm} f{e[0]} flash forced ({(e[2].Trim() == "1" ? "mid" : "close")})"); } else log($"{nm} f{e[0]} no weather on this level");
                    break;
                }
                case "FP": { log($"{nm} f{e[0]} flash point now {(OzWeather.I ? OzWeather.I.FlashPointNow : -1f):0.00}"); break; }
                case "AU":
                {   // round 6 B: f,AU,x,z,tx,tz = the nearest vehicle / robot leader to (x, z) gets the player's ATTACK order on the nearest unit of the OTHER side (vehicle, robot leader, gun) to (tx, tz)
                    var u = Nearest(w, new Vector3(F(e[2]), 0f, F(e[3])), x => (x.Kind == ZKind.Vehicle && !x.IsShuttle && x.Owner != Team.Neutral) || (x.IsRobot && !x.IsMinion && x.Owner != Team.Neutral));
                    var tg = u == null ? null : Nearest(w, new Vector3(F(e[4]), 0f, F(e[5])), x => x.Owner != u.Owner && ((x.Kind == ZKind.Vehicle && !x.IsShuttle) || (x.IsRobot && !x.IsMinion) || x.Kind == ZKind.Cannon));
                    bool ok = u != null && tg != null && w.SendWaypoints(u.Owner, u, new[] { new ZWaypoint { mode = WpMode.Attack, refId = tg.Id, x = tg.CenterX, y = tg.CenterY, playerGiven = true } });
                    log($"{nm} f{e[0]} attack unit: {(u != null ? u.Type + "#" + u.Id + " " + u.Owner : "no unit")} -> {(tg != null ? tg.Type + "#" + tg.Id + " " + tg.Owner + " health " + tg.Health : "none")} ok={ok}"); break;
                }
                case "HP":
                {   // round 6 B: f,HP,x,z,hp = the nearest vehicle to (x, z) gets this health (a staged target that lasts / falls)
                    var o = Nearest(w, new Vector3(F(e[2]), 0f, F(e[3])), x => x.Kind == ZKind.Vehicle && !x.IsShuttle);
                    if (o != null) { o.SetHealth(int.Parse(e[4].Trim())); w.UpdateObjectHealth(o); }
                    log($"{nm} f{e[0]} health: {(o != null ? o.Type + "#" + o.Id + " " + o.Owner + " -> " + o.Health : "nothing")}"); break;
                }
                case "STORM": if (OzWeather.I) { OzWeather.I.ForceStorm(); log($"{nm} f{e[0]} storm forced"); } else log($"{nm} f{e[0]} no weather on this level"); break;
            }
        }
    }

}
