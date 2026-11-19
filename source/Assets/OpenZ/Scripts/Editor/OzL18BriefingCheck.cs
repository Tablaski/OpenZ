// OpenZ (GPL-3.0) - version 1.5, level 18 "Bridge Game": the counted check of its two start briefings' data (no Play, nothing written).
// Menu OpenZ/Checks/V1.5/L18 briefings data (no Play). It reads what the game will read: the commander's line zod_brief_L18 (host trio,
// the owner's six lines word for word), the blue scene L18_start (his four speeches word for word, every beat a name the stage knows by
// its own check in Play), both shot lists with their cuts anchored on the two voice clips (the press = the cut into three_blasts, the
// blasts inside the pause), the watch set's scene (every beat an existing state of the character's own Animator controller, no head
// turn over the limit, no crash zoom on the closing line), the stage's sound clips and tank models.
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace OpenZ.EditorTools
{
    public static class OzL18BriefingCheck
    {
        static readonly string[] RedWords =
        {
            "Alan: Sir! The enemy blew up all three bridges!", "EXCELLENT! THEY'RE TRAPPED!", "Alan: Actually, sir, so are we!",
            "EVEN BETTER! THEY'LL NEVER SEE US COMING!", "Brad: But sir! How do we get across?!", "SWIM! NOW GET ON WITH IT",
        };
        static readonly string[] BlueWords =
        {
            "Three bridges! Three armies! One glorious victory!", "Now, where's my coffee?!", "No! Not that one!", "Sir! You've destroyed all three bridges!",
            "Well, EXACTLY! Now the enemy can NOT cross!", "Neither can we, sir.",
        };
        static readonly string[] BlueWho = { OzBlueScenes.EvilZod, OzBlueScenes.EvilZod, OzBlueScenes.Sidekick, OzBlueScenes.Sidekick, OzBlueScenes.EvilZod, OzBlueScenes.Sidekick };

        [MenuItem("OpenZ/Checks/V1.5/L18 briefings data (no Play)")]
        public static void Run()
        {
            int pass = 0, all = 0;
            void C(string what, bool ok) { all++; if (ok) pass++; if (ok) Debug.Log("[L18 briefings] PASS " + what); else Debug.LogError("[L18 briefings] FAIL " + what); }

            // ---- red: the line, its clip, its words
            var lib = ZodLibrary.Shared; var line = lib.Line("zod_brief_L18"); var clip = lib.Clip("zod_brief_L18");
            C("the line zod_brief_L18 with its clip, host trio", line != null && clip && line.host == ZodBriefing.TrioHost);
            float lineSeconds = line != null ? line.duration : 0f;
            if (line != null && line.cues != null)
            {
                C("its six subtitle lines are the words: " + string.Join(" | ", line.cues.Select(c => c.text)), line.cues.Length == RedWords.Length && line.cues.Select(c => c.text).SequenceEqual(RedWords));
                C($"its length {line.duration:F2} s inside the clip {(clip ? clip.length : 0f):F2} s (on the line's clock, with its cuts: {(clip ? ZodBriefing.LineWarp(line.id, clip.length) : 0f):F2} s), cues rising", clip && line.duration <= ZodBriefing.LineWarp(line.id, clip.length) + 0.01f && line.cues.Zip(line.cues.Skip(1), (a, b) => a.end <= b.start + 0.001f).All(x => x));
                var cuts = ZodShotList.CutTimes(ZodShotList.L18.shots, line);
                C("red shot list L18: " + ZodShotList.L18.shots.Length + " shots, every cut anchored and rising: " + string.Join(" ", cuts.Skip(1).Select(t => t.ToString("F2"))),
                  ZodShotList.L18.shots.Length == 6 && cuts.Skip(1).All(t => !float.IsNaN(t)) && cuts.Skip(1).Zip(cuts.Skip(2), (a, b) => b - a >= 0.8f).All(x => x));
            }
            // ---- blue: the scene, its clip, its words, the press and the blasts inside the pause
            var scenes = OzBlueScenes.Shared; bool ready = scenes.Ready(OzCity.BlueStartScene, out string why); var sc = scenes.Scene(OzCity.BlueStartScene);
            C("the blue scene " + OzCity.BlueStartScene + " is playable" + (ready ? "" : " (" + why + ")"), ready);
            if (sc != null && sc.lines != null)
            {
                C("its six subtitle lines are the words, the right speaker on each: " + string.Join(" | ", sc.lines.Select(l => l.text)),
                  sc.lines.Length == BlueWords.Length && sc.lines.Select(l => l.text).SequenceEqual(BlueWords) && sc.lines.Select(l => l.who).SequenceEqual(BlueWho));
                var fly = new ZodLine { id = "blue_" + sc.id, group = "briefing", text = "", duration = sc.length,
                                        cues = sc.lines.Select(l => new ZodCue { text = l.text, start = l.start, end = l.end }).ToArray(),
                                        words = (sc.words ?? new OzBlueWord[0]).Select(w => new ZodWord { word = w.w, start = w.s, end = w.e }).ToArray() };
                var defs = ZodShotList.B18.shots; var cuts = ZodShotList.CutTimes(defs, fly);
                int pi = System.Array.FindIndex(defs, d => d != null && d.name == OzL18BriefingStage.PressShot);
                C("blue shot list B18: " + defs.Length + " shots with " + OzL18BriefingStage.PressShot + ", every cut anchored and rising: " + string.Join(" ", cuts.Skip(1).Select(t => t.ToString("F2"))),
                  defs.Length == 5 && pi > 0 && cuts.Skip(1).All(t => !float.IsNaN(t)) && cuts.Skip(1).Zip(cuts.Skip(2), (a, b) => b - a >= 0.8f).All(x => x));
                if (pi > 0 && !float.IsNaN(cuts[pi]))
                {
                    float press = cuts[pi], last = press + OzL18BriefingStage.PressToBlast + 2f * OzL18BriefingStage.BlastGap;
                    C($"the press {press:F2} s comes after his words ({sc.lines[1].end:F2} s is the line's end mark), BS-01's scream ({sc.lines[2].start:F2} s) begins before it and is cut by the first blast ({press + OzL18BriefingStage.PressToBlast:F2} s), the last blast {last:F2} s is over 1.2 s before BS-01 speaks again ({sc.lines[3].start:F2} s)",
                      press > sc.lines[1].start + 1f && sc.lines[2].start < press && sc.lines[2].end >= press + OzL18BriefingStage.PressToBlast - 0.1f && sc.lines[3].start - last >= 1.2f);
                    {
                        float under = press + OzL18BriefingStage.LastUnderAfterPress();
                        C($"nothing of the stage is left when our fort comes: the last hull is gone at {under:F2} s at the latest, the cut into the last shot is {cuts[cuts.Length - 1]:F2} s", under <= cuts[cuts.Length - 1] - 0.3f);
                        int flown = defs.Count(d => d != null && OzL18Drone.PathOf(d.name) != null);
                        C($"every blue shot but {OzL18BriefingStage.PressShot} is flown as a drone path ({flown} of {defs.Length - 1})", flown == defs.Length - 1 && OzL18Drone.PathOf(OzL18BriefingStage.PressShot) == null);
                        {   // owner addition 10 point 2: no flight turns its lens in jumps. Every drone path sampled at 30 pictures a second (the
                            // game's own pose function, level ground, the start camera 126 / 70 / -133 looking down 55 degrees): the largest
                            // change of the turn speed from one picture to the next, for yaw, pitch and roll
                            var hp = new Vector3(126f, 70f, -133f); var hr = Quaternion.Euler(55f, 0f, 0f); var fw = hr * Vector3.forward; var hg = hp + fw * (hp.y / -fw.y);
                            float worst = 0f, fastest = 0f; string where = "", list = "";
                            for (int di = 0; di < defs.Length; di++)
                            {
                                var path = defs[di] != null ? OzL18Drone.PathOf(defs[di].name) : null; if (path == null) continue;
                                float dur = di > 0 && di + 1 < cuts.Length && !float.IsNaN(cuts[di + 1]) && !float.IsNaN(cuts[di]) ? cuts[di + 1] - cuts[di] : defs[di].seconds > 0.2f ? defs[di].seconds : 2.77f; /* the first shot opens before the voice: its own length */ int n = Mathf.Max(2, Mathf.RoundToInt(dur * 30f));
                                var ang = new float[3][]; for (int k = 0; k < 3; k++) ang[k] = new float[n + 1];
                                for (int i = 0; i <= n; i++)
                                {
                                    OzL18Drone.Pose(path, i / (float)n, dur, hp, hr, 32f, hg, null, out _, out var rot, out _, out _, out _, out _);
                                    var e = rot.eulerAngles; ang[0][i] = e.y; ang[1][i] = e.x; ang[2][i] = e.z;
                                }
                                float shotWorst = 0f;
                                for (int k = 0; k < 3; k++)
                                {
                                    float prev = 0f;
                                    for (int i = 0; i < n; i++)
                                    {
                                        float w = Mathf.DeltaAngle(ang[k][i], ang[k][i + 1]) * 30f; if (Mathf.Abs(w) > fastest) fastest = Mathf.Abs(w);
                                        if (i > 0) { float st = Mathf.Abs(w - prev); if (st > shotWorst) shotWorst = st; if (st > worst) { worst = st; where = defs[di].name + " " + (k == 0 ? "yaw" : k == 1 ? "pitch" : "roll") + " at " + (i / 30f).ToString("F2") + " s"; } }
                                        prev = w;
                                    }
                                }
                                list += defs[di].name + " " + shotWorst.ToString("F1") + "  ";
                            }
                            C($"no drone flight turns its lens in jumps: the largest change of the turn speed from one picture to the next (30 a second) is {worst:F1} deg/s ({where}; limit 12; per flight: {list.Trim()}), the fastest turn {fastest:F0} deg/s (limit 130)", worst <= 12f && fastest <= 130f);
                        }
                        C("the three parades are written smallest first, the banner Psycho at the head of the middle one, the Mammoth and the Bizon last: " + string.Join(" | ", OzL18BriefingStage.Parade.Select(p => string.Join(" ", p))),
                          OzL18BriefingStage.Parade.Length == 3 && OzL18BriefingStage.Parade[1][0] == "banner" && OzL18BriefingStage.Parade[1].Last() == "mammoth" && OzL18BriefingStage.Parade[2].Last() == "bizon" && OzL18BriefingStage.Parade.All(p => p.Count(x => x == "mammoth") <= 1));
                    }
                    var wind = sc.beats.FirstOrDefault(b => b.who == "ez" && b.beat == "binoc_windup"); var tear = sc.beats.FirstOrDefault(b => b.who == "ez" && b.beat == "binoc_off");
                    var two = sc.beats.LastOrDefault(b => b.who == "cam" && b.beat == "l18_two" && tear != null && b.t <= tear.t); var next = sc.beats.FirstOrDefault(b => b.who == "cam" && tear != null && b.t > tear.t);
                    C($"the binoculars' tear-down is SEEN: both in the picture {(two != null && tear != null ? tear.t - two.t : -1f):F2} s before it (0.3 wanted), a wind-up {(wind != null && tear != null ? tear.t - wind.t : -1f):F2} s before it, the next cut {(next != null && tear != null ? next.t - tear.t : -1f):F2} s after it (0.35 wanted)",
                      wind != null && tear != null && two != null && next != null && tear.t - two.t >= 0.3f && tear.t - wind.t >= 0.08f && tear.t - wind.t <= 0.2f && next.t - tear.t >= 0.35f);
                    var win = sc.beats.FirstOrDefault(b => b.who == "cam" && b.beat == "win_two"); var after = sc.beats.FirstOrDefault(b => b.who == "cam" && b.beat != "push" && win != null && b.t > win.t);
                    C($"the HQ window picture holds {(win != null && after != null ? after.t - win.t : -1f):F2} s (2 s wanted: through \"Three bridges!\")", win != null && after != null && after.t - win.t >= 2f);
                    var pat = sc.beats.FirstOrDefault(b => b.who == "ez" && b.beat == "pat_console");
                    C($"the beat pat_console puts the palm on the button at the press ({(pat != null ? pat.t + pat.a : -1f):F2} s)", pat != null && Mathf.Abs(pat.t + pat.a - press) <= 0.05f);
                    var flashes = sc.beats.Where(b => b.who == "set" && b.beat == "flash").Select(b => b.t).ToArray();
                    C("three window flashes on the three blasts: " + string.Join(" ", flashes.Select(t => t.ToString("F2"))),
                      flashes.Length == 3 && Enumerable.Range(0, 3).All(i => Mathf.Abs(flashes[i] - (press + OzL18BriefingStage.PressToBlast + i * OzL18BriefingStage.BlastGap)) <= 0.05f));
                }
                C("no crash zoom inside the closing line (the last crash beat ends before it)", !sc.beats.Any(b => b.who == "cam" && b.beat == "crash" && b.t >= sc.lines[sc.lines.Length - 1].start - 0.4f));
                C("BS-01 never shivers: no shake or fidget beat in the scene, one head shake", !sc.beats.Any(b => b.who == "sk" && (b.beat == "shake" || b.beat == "fidget" || b.beat == "shrink")) && sc.beats.Count(b => b.who == "sk" && b.beat == "head_no") == 1);
                C("BS-01 lunges for the hand as he screams (beat lunge on the scream's first word)", sc.beats.Any(b => b.who == "sk" && b.beat == "lunge" && Mathf.Abs(b.t - sc.lines[2].start) <= 0.1f));
            }
            // ---- the watch set's scene
            var ta = Resources.Load<TextAsset>(OzL18WatchSet.DataPath); var ws = ta ? JsonUtility.FromJson<OzWatchFile>(ta.text) : null;
            C("the watch set's scene Resources/" + OzL18WatchSet.DataPath, ws != null && ws.shots != null && ws.shots.Length >= 5 && ws.actors != null && ws.actors.Length == 3);
            if (ws != null && ws.beats != null)
            {
                var states = new Dictionary<string, HashSet<string>>();
                foreach (var kv in new[] { ("zod", ZodStage.PrefabPath), ("alan", ZodStage.AlanPrefabPath), ("brad", ZodStage.BradPrefabPath) })
                {
                    var set = new HashSet<string>(); var pf = Resources.Load<GameObject>(kv.Item2); var an = pf ? pf.GetComponentInChildren<Animator>(true) : null;
                    var ac = an ? an.runtimeAnimatorController as AnimatorController : null;
                    if (ac != null) foreach (var st in ac.layers[0].stateMachine.states) set.Add(st.state.name);
                    states[kv.Item1] = set;
                }
                var bad = ws.beats.Where(b => !states.ContainsKey(b.who) || !states[b.who].Contains(b.clip) || (!string.IsNullOrEmpty(b.back) && !states[b.who].Contains(b.back))).Select(b => b.who + ":" + b.clip).ToArray();
                C("every beat of the watch set is an existing state of that character's Animator controller" + (bad.Length > 0 ? " (missing: " + string.Join(", ", bad) + ")" : $" ({ws.beats.Length} beats)"), bad.Length == 0);
                C("no head turn over " + OzL18WatchSet.HeadTurnMax + " degrees", ws.looks == null || ws.looks.All(l => Mathf.Abs(l.yaw) <= OzL18WatchSet.HeadTurnMax));
                C("the window's last shot (the closing line) is no crash zoom: " + ws.shots[ws.shots.Length - 1].kind, ws.shots[ws.shots.Length - 1].kind != "face");
                int takes = 0; if (ws.takes != null) foreach (var td in ws.takes) { var tclip = td != null ? Resources.Load<AnimationClip>(td.res) : null; if (tclip && tclip.length >= lineSeconds - td.t0 - 0.05f) takes++; }
                C($"Alan's and Brad's authored takes load and cover the line ({takes} of 2)", takes == 2 && ws.takes.Length == 2);
                var ab = string.IsNullOrEmpty(ws.alanBody) ? null : Resources.Load<GameObject>(ws.alanBody); var smr = ab ? ab.GetComponentInChildren<SkinnedMeshRenderer>(true) : null; bool point = false;
                if (smr && smr.sharedMesh) for (int i = 0; i < smr.sharedMesh.blendShapeCount; i++) if (smr.sharedMesh.GetBlendShapeName(i).EndsWith("PointR")) point = true;
                C("Alan's body with the pointing finger (Resources/" + ws.alanBody + ", shape PointR), " + (ws.shapes != null ? ws.shapes.Length : 0) + " shape track(s)", point && ws.shapes != null && ws.shapes.Length >= 1);
                C("no beat of the data moves a robot that plays a take, and a head turn only after the takes have come to rest (" + ws.takeStop + " s)", ws.beats.All(b => b.who == "zod") && (ws.looks == null || ws.looks.All(l => l.who == "zod" || (ws.takeStop > 0f && l.t >= ws.takeStop))));
                if (ws.zposes != null && ws.zkeys != null)
                {   // round 2: the commander moves from one held pose into the next
                    var names = new HashSet<string>(ws.zposes.Select(z => z.name));
                    C($"Zod's {ws.zposes.Length} poses are frames of states of his own controller", ws.zposes.Length >= 3 && ws.zposes.All(z => states["zod"].Contains(z.state) && (z.m == null || z.v == null || z.m.Length == z.v.Length)));
                    C($"Zod's {ws.zkeys.Length} moves name poses that exist, in rising order, none faster than 0.3 s", ws.zkeys.All(k => names.Contains(k.pose) && (k.t < 0f || k.ease >= 0.3f)) && ws.zkeys.Zip(ws.zkeys.Skip(1), (a, b) => b.t >= a.t).All(x => x));
                    var human = HumanTrait.MuscleName; bool allMuscles = ws.zposes.All(z => z.m == null || z.m.All(m => System.Array.IndexOf(human, m.TrimStart('+')) >= 0)) && (ws.stanceM == null || ws.stanceM.All(m => System.Array.IndexOf(human, m.TrimStart('+')) >= 0));
                    C("every muscle a pose or the stance names is one of the avatar's", allMuscles);
                    C("at least half of the window's pictures have their own focus and f-stop (filmed past something)", ws.shots.Count(x => x.aperture > 0f) * 2 >= ws.shots.Length);
                }
            }
            // ---- what the full-screen stage needs
            var missing = OzL18BriefingStage.WarmClips.Where(f => !Resources.Load<AudioClip>(OzL18BriefingStage.SfxRes + f)).ToArray();
            C("the stage's " + OzL18BriefingStage.WarmClips.Length + " sound clips" + (missing.Length > 0 ? " (missing: " + string.Join(", ", missing) + ")" : ""), missing.Length == 0);
            C("the Mammoth and the heavy tank models", Resources.Load<GameObject>(OzBigTanks.Dir + OzBigTanks.MammothKey) && Resources.Load<GameObject>(OzHeavyTank.Dir + OzHeavyTank.V2Key));
            bool longKit = AssetDatabase.FindAssets("Z_bridge_city_long_intact t:Prefab").Length > 0;
            C("the long city bridge family (Z_bridge_city_long_intact) is in the project", longKit);
            foreach (var res in OzEvilStage.L18Props) C("the blue window's modelled prop Resources/" + res, Resources.Load<GameObject>(res));
            foreach (var res in new[] { OzEvilStage.L18LensRes, OzEvilStage.L18LensGlowRes, OzEvilStage.L18PlateCoffeeRes, OzEvilStage.L18PlateCoffeeRes + "_E", OzEvilStage.L18PlateNukeRes, OzEvilStage.L18PlateNukeRes + "_E", OzL18BriefingStage.BannerTexRes })
                C("the picture Resources/" + res + " (the game's own art and real frames)", Resources.Load<Texture2D>(res));
            C("level 18's loading picture Resources/OpenZ/Loading/L18_loading_1 (a real frame of the blue briefing's stage)", Resources.Load<Texture2D>("OpenZ/Loading/L18_loading_1"));
            C("the red window's watch post Resources/" + OzL18WatchSet.ParapetRes, Resources.Load<GameObject>(OzL18WatchSet.ParapetRes));
            OzL18BriefingCheckR4.Rows(C);                                                                 // kit level18_briefings/kit_r4
            if (pass == all) Debug.Log($"[L18 briefings] DATA CHECK PASS {pass}/{all}"); else Debug.LogError($"[L18 briefings] DATA CHECK FAIL {pass}/{all}");
        }
    }
}
