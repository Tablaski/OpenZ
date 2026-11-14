#if UNITY_EDITOR
// OpenZ (GPL-3.0) - version 1.2, the blue flyovers (View/Zod/ZodFlyover.Blue.cs): the menus of the counted check (no Play), the Play proofs,
// the MP4 takes, the skip test and the start frame trace of owner item 86 (View/OzBlueFlyoverProof.cs, View/OzStartFrameTrace.cs).
using System.Text;
using UnityEditor;
using UnityEngine;

namespace OpenZ
{
    public static partial class OzBlueFlyoverMenu                                                   // + OzBlueFlyoverMenu.Checks2.cs
    {
        const string Root = "OpenZ/V1.2/Blue Flyovers: 75c ";

        // ------------------------------------------------------------------------------------------------ the counted check
        [MenuItem(Root + "1 Counted Check (no Play)")]
        static void Counted()
        {
            var sb = new StringBuilder(); int pass = 0, fail = 0;
            void Check(string what, bool ok) { if (ok) pass++; else fail++; sb.AppendLine((ok ? "PASS  " : "FAIL  ") + what); }
            ZodShotList.EnsureFile();
            int force = ZodShotList.ForceBlueList;
            try
            {
                ZodShotList.ForceBlueList = 0;
                Check("the red side: For answers the red lists (L02 " + ZodShotList.For("L02").level + ", L16 " + ZodShotList.For("L16").level + ", L17 " + ZodShotList.For("L17").level + ")",
                    ZodShotList.For("L02") == ZodShotList.L02 && ZodShotList.For("L16") == ZodShotList.L16 && ZodShotList.For("L17") == ZodShotList.L17);
                var f = ZodShotList.File;
                Check("the red lists are the data file's (L02 " + ZodShotList.L02.shots.Length + ", L17 " + ZodShotList.L17.shots.Length + ", L16 " + ZodShotList.L16.shots.Length + " shots; the kit does not touch the file)",
                    f != null && ZodShotList.L02.shots == f.L02 && ZodShotList.L17.shots == f.L17 && ZodShotList.L16.shots == f.L16);
                Check("the red lists keep their names (L02 whole_map, sunrise, enemy_fort, our_base_home; L16 dawn_swamp first, our_base_home last)",
                    Names(ZodShotList.L02) == "whole_map>sunrise>enemy_fort>our_base_home" && ZodShotList.L16.shots[0].name == "dawn_swamp" && ZodShotList.L16.shots[ZodShotList.L16.shots.Length - 1].name == "our_base_home");
                Check("the data file carries no blue lists yet (the code lists fly; B02 / B16 / B17 empty)", f == null || ((f.B02 == null || f.B02.Length == 0) && (f.B16 == null || f.B16.Length == 0) && (f.B17 == null || f.B17.Length == 0)));
                ZodShotList.ForceBlueList = 1;
                Check("the blue side: For answers the blue lists (L02 " + ZodShotList.For("L02").level + ", L16 " + ZodShotList.For("L16").level + ", L17 " + ZodShotList.For("L17").level + ")",
                    ZodShotList.For("L02") == ZodShotList.B02 && ZodShotList.For("L16") == ZodShotList.B16 && ZodShotList.For("L17") == ZodShotList.B17);
                var line = ZodShotList.BlueBriefingLine();
                Check("the blue briefing line: 5 cues at 0.10 / 2.48 / 5.58 / 9.00 / 11.42 s, the words where 7.24 and now 11.42, the last word ends 11.98 s",
                    line.cues.Length == 5 && Near(line.cues[1].start, 2.48f) && Near(line.cues[3].start, 9.00f) && Near(line.cues[4].start, 11.42f) && line.words.Length == 2
                    && Near(line.words[0].start, 7.24f) && Near(line.words[1].start, 11.42f) && Near(line.duration, 11.98f));
                {   // the briefing kit's own line may come with the owner's FOUR lines and no word times: the cuts then come from the letters of the cues
                    var four = ZodShotList.BlueLine(new[] { 0.10f, 2.48f, 5.58f, 9.00f }, 11.98f, new[] { "Sidekick: What's the mission today, sir?",
                        "EvilZod: SAME AS ALWAYS! TAKE OVER THE WORLD!", "Sidekick: The whole world? Where do we start?", "EvilZod: THAT RED FORT! PAINT IT BLUE! NOW MOVE!" });
                    var at4 = ZodShotList.CutTimes(ZodShotList.B02.shots, four); float ld = ZodFlyover.CutLead;
                    Check($"a four-line briefing without word times cuts close to the words: {at4[1]:F2} / {at4[2]:F2} / {at4[3]:F2} s (the words: {2.48f - ld:F2} / {7.24f - ld:F2} / {11.42f - ld:F2}, within 0.3 s)",
                        !float.IsNaN(at4[3]) && Mathf.Abs(at4[1] - (2.48f - ld)) < 0.3f && Mathf.Abs(at4[2] - (7.24f - ld)) < 0.3f && Mathf.Abs(at4[3] - (11.42f - ld)) < 0.3f);
                }
                foreach (var L in new[] { "L02", "L16", "L17" })
                {
                    var b = ZodShotList.ForBlue(L);
                    Check(L + " blue: four shots " + Names(b), Names(b) == string.Join(">", ZodShotList.BlueShotNames));
                    var redAt = L == "L17" ? new Vector2(172f, -83f) : L == "L16" ? new Vector2(104f, -229f) : new Vector2(106f, -155f);
                    Check(L + " blue: shot 1 is OwnFortDawn (from our fort into the sun), shot 2 a Free glide over the map, shot 3 a Point orbit round the red fort (" + redAt.x + ", " + redAt.y + ") with a dip and the wind swell, shot 4 OwnBaseHome easing into the start camera",
                        b.shots[0].subject == ZodCineSubject.OwnFortDawn && b.shots[1].subject == ZodCineSubject.Free && b.shots[2].subject == ZodCineSubject.Point && b.shots[2].point == redAt && b.shots[2].dip > 0f && b.shots[2].whoosh
                        && b.shots[3].subject == ZodCineSubject.OwnBaseHome && b.shots[3].ease == ZodCineEase.InOut);
                    var at = ZodShotList.CutTimes(b.shots, line);
                    float lead = ZodFlyover.CutLead;
                    Check(L + $" blue: the cuts anchored on the line: {at[1]:F2} / {at[2]:F2} / {at[3]:F2} s (want {2.48f - lead:F2} / {7.24f - lead:F2} / {11.42f - lead:F2})",
                        Near(at[1], 2.48f - lead) && Near(at[2], 7.24f - lead) && Near(at[3], 11.42f - lead));
                    Check(L + $" blue: a quiet opening of {b.open:F2} s, the last shot at least {b.lastMin:F1} s, at most {b.maxTotal:F0} s in all", b.open > 0.5f && b.lastMin >= 2f && b.maxTotal >= 15f);
                    float pre = 0.55f, end = 11.98f + 0.35f;
                    ZodShotList.BriefingRoom(L, line, ref pre, 11.98f, ref end);
                    Check(L + $" blue: the briefing room gives {pre:F2} s before the first word and ends {end:F2} s after the voice start", Near(pre, b.open) && end >= 11.42f - lead + 2f - 0.01f);
                }
                {   // level 7: the same beats; its framing is proved off-line at every frame (l07_finish/cutscene/tools/shot_math.py), its cut words are its own scene's
                    var b7 = ZodShotList.ForBlue("L07");
                    Check("L07 blue: four shots " + Names(b7), Names(b7) == string.Join(">", ZodShotList.BlueShotNames));
                    Check("L07 blue: shot 1 is OwnFortDawn, shot 2 a Free glide, shot 3 a Point orbit round the red fort (84, -197) with a dip and the wind swell, shot 4 OwnBaseHome easing into the start camera",
                        b7.shots[0].subject == ZodCineSubject.OwnFortDawn && b7.shots[1].subject == ZodCineSubject.Free && b7.shots[2].subject == ZodCineSubject.Point && b7.shots[2].point == new Vector2(84f, -197f) && b7.shots[2].dip > 0f && b7.shots[2].whoosh
                        && b7.shots[3].subject == ZodCineSubject.OwnBaseHome && b7.shots[3].ease == ZodCineEase.InOut);
                }
                Check("the jungle's land beyond the rim builds in gentle slices on a blue start (SliceMsBlue " + OzL16Outskirts.SliceMsBlue + " ms, the flyover's " + OzL16Outskirts.SliceMs + " ms, without a flyover " + OzL16Outskirts.SliceMsNoFlyover + " ms)",
                    OzL16Outskirts.SliceMsBlue <= OzL16Outskirts.SliceMs && OzL16Outskirts.SliceMsBlue > 0f && OzL16Outskirts.SliceMsNoFlyover == 40f);
                Counted2(Check);                                                                      // the hold, the flyover antics, the lists against the proved numbers (OzBlueFlyoverMenu.Checks2.cs)
            }
            finally { ZodShotList.ForceBlueList = force; }
            string head = $"[OpenZ V1.2] Blue Flyovers 75c counted check: {(fail == 0 ? "PASS" : "FAIL")} {pass}/{pass + fail}";
            Debug.Log(head + "\n" + sb);
            try
            {
                string dir = OzBlueFlyoverProof.Folder("");
                System.IO.Directory.CreateDirectory(dir);
                System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "counted_check.txt"), head + "\n" + sb);
            }
            catch (System.Exception) { }
        }

        static bool Near(float a, float b) => !float.IsNaN(a) && Mathf.Abs(a - b) < 0.02f;

        static string Names(ZodShotList l)
        {
            var parts = new System.Collections.Generic.List<string>();
            if (l?.shots != null) foreach (var d in l.shots) if (d != null) parts.Add(d.name);
            return string.Join(">", parts);
        }

        // ------------------------------------------------------------------------------------------------ Play
        static void Go(string level, OzBlueFlyoverProof.Job job)
        {
            if (!Application.isPlaying) { Debug.LogWarning("[OpenZ] Blue Flyovers 75c: start the MainMenu scene in Play first (or a level running as blue for the proof / take)"); return; }
            OzBlueFlyoverProof.Run(level, job);
            Debug.Log("[OpenZ] Blue Flyovers 75c " + job + " " + level + " started; its report: OzBlueFlyoverProof.Report, the pictures in " + OzBlueFlyoverProof.Folder(level));
        }

        [MenuItem(Root + "2 Play Proof L02 (main menu or a blue L02, Play)")] static void ProofL02() => Go("L02", OzBlueFlyoverProof.Job.Proof);
        [MenuItem(Root + "2 Play Proof L16 (main menu or a blue L16, Play)")] static void ProofL16() => Go("L16", OzBlueFlyoverProof.Job.Proof);
        [MenuItem(Root + "2 Play Proof L17 (main menu or a blue L17, Play)")] static void ProofL17() => Go("L17", OzBlueFlyoverProof.Job.Proof);
        [MenuItem(Root + "3 Record MP4 L02 (main menu, Play)")] static void RecordL02() => Go("L02", OzBlueFlyoverProof.Job.Record);
        [MenuItem(Root + "3 Record MP4 L16 (main menu, Play)")] static void RecordL16() => Go("L16", OzBlueFlyoverProof.Job.Record);
        [MenuItem(Root + "3 Record MP4 L17 (main menu, Play)")] static void RecordL17() => Go("L17", OzBlueFlyoverProof.Job.Record);
        [MenuItem(Root + "4 Skip Test L16 (main menu, Play)")] static void SkipL16() => Go("L16", OzBlueFlyoverProof.Job.SkipTest);
        [MenuItem(Root + "5 Frame Trace L16 BLUE start (main menu, Play)")] static void TraceBlue() => Go("L16", OzBlueFlyoverProof.Job.TraceBlue);
        [MenuItem(Root + "5 Frame Trace L16 RED start (main menu, Play)")] static void TraceRed() => Go("L16", OzBlueFlyoverProof.Job.TraceRed);
    }
}
#endif
