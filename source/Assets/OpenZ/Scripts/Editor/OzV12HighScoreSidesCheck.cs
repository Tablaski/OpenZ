using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using OpenZ.Sim;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// OpenZ/V1.2/High Score Sides: 90 Counted Check. It works on table files of its own in the temporary folder (each written fresh by
    /// the check; the player's and the editor's own tables are never touched) and goes through the same code the end screen uses
    /// (OzScoreSides.Entry, OzHighScores.Score / Submit / Top / TableLines):
    ///  C0 the end screen: no side is kept out of the table any more (UnrankedWhy has no PLAY AS BLUE line), the entry is made by
    ///     OzScoreSides.Entry with the human player's team; the other reasons that keep a result out are all still there.
    ///  C1 the sides: red = 0 ZOD on the red paint, blue = 1 EVILZOD on the blue paint, anything else red.
    ///  C2 a red and a blue victory on each difficulty (EASY, MEDIUM, HARD) land in that difficulty's table of the level, with their own
    ///     side and their result exactly as made; the other difficulties' tables hold only their own two.
    ///  C3 the file read again from disk: the same rows in the same order with the same sides; the file names each entry's side.
    ///  C4 the same rules for both sides: equal results keep the older one above whichever side it is; twelve results of mixed sides
    ///     leave the ten best, best first, every one with the side it was won with.
    ///  C5 old rows load: a file from before the side was kept (one entry with a name, as the earliest builds wrote) reads every entry
    ///     as ZOD with all its numbers; a blue victory then joins the same table; the next write keeps the old rows ZOD, adds the blue
    ///     row as EVILZOD and holds no name.
    ///  C6 an earlier build reads the new file (the entry as it was, without the side): every score, time and count is there.
    ///  C7 a side number that is not 0 or 1 reads as ZOD.
    ///  C8 the end screen's TOP 10 tooltip: every line carries its side in the side's colour, in table order.
    /// Writes its lines to the kit's proof folder (k90_blue_high_scores/proof/counted_check.txt) when the folder is there.
    /// </summary>
    public static class OzV12HighScoreSidesCheck
    {
        const string T = "[OzV12HighScoreSidesCheck] ";
        public const string CheckMenu = "OpenZ/V1.2/High Score Sides: 90 Counted Check";
        public const string ProofMenu = "OpenZ/V1.2/High Score Sides: 90 Play Proof (Play)";
        public const string StopMenu = "OpenZ/V1.2/High Score Sides: 90 Stop Play Proof";

        [MenuItem(CheckMenu)]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning(T + "run it in edit mode (no Play)"); return; }
            string text = Report(out bool pass);
            Debug.Log(text);
            string dir = OzHighScoreSidesProof.Folder();
            try { Directory.CreateDirectory(dir); File.WriteAllText(Path.Combine(dir, "counted_check.txt"), text); }
            catch (Exception e) { Debug.LogWarning(T + "the lines could not be written to " + dir + ": " + e.Message); }
        }

        [MenuItem(ProofMenu)] static void RunProof() => OzHighScoreSidesProof.Run();
        [MenuItem(ProofMenu, true)] static bool ProofReady() => Application.isPlaying;
        [MenuItem(StopMenu)] static void StopProof() => OzHighScoreSidesProof.Stop();
        [MenuItem(StopMenu, true)] static bool StopReady() => Application.isPlaying;

        // ------------------------------------------------------------------------------------------------ the old shape of an entry
        // (the file as builds before the side wrote and read it: C6 reads the new file the way they do)
        [Serializable] public class OldEntry { public int score, seconds, kills, losses, built, zones, zonesTotal; }
        [Serializable] public class OldTable { public string level, difficulty; public List<OldEntry> entries = new List<OldEntry>(); }
        [Serializable] public class OldFile { public int version = 1; public List<OldTable> tables = new List<OldTable>(); }

        static readonly GameDifficulty[] Diffs = { GameDifficulty.Easy, GameDifficulty.Medium, GameDifficulty.Hard };

        /// <summary>A table as "E7:ZOD:21000/500 E2:EVILZOD:19901/754 ...": each entry by the number in its kills field, its side, score, time.</summary>
        static string Line(IReadOnlyList<OzScoreEntry> list)
        {
            var sb = new StringBuilder();
            foreach (var e in list) { if (sb.Length > 0) sb.Append(' '); sb.Append('E').Append(e.kills).Append(':').Append(OzScoreSides.TagOf(e)).Append(':').Append(e.score).Append('/').Append(e.seconds); }
            return sb.ToString();
        }

        /// <summary>A table file of the check's own, written fresh (empty, or with the given text).</summary>
        static string Fresh(string name, string json = null)
        {
            string p = Path.Combine(Application.temporaryCachePath, name);
            File.WriteAllText(p, json ?? "{\"version\":1,\"tables\":[]}", new UTF8Encoding(false));
            OzHighScores.UseFile(p);
            return p;
        }

        static string Code(string src)
        {
            var sb = new StringBuilder();
            foreach (var line in src.Split('\n'))
            {
                int cut = line.Length; bool q = false;
                for (int i = 0; i + 1 < line.Length; i++)
                {
                    if (line[i] == '"' && (i == 0 || line[i - 1] != '\\')) q = !q;
                    else if (!q && line[i] == '/' && line[i + 1] == '/') { cut = i; break; }
                }
                sb.Append(line, 0, cut).Append('\n');
            }
            return sb.ToString();
        }

        public static string Report(out bool pass)
        {
            int checks = 0, ok = 0; var lines = new StringBuilder();
            void Check(string what, bool good) { checks++; if (good) ok++; lines.Append(good ? "PASS " : "FAIL ").AppendLine(what); }
            lines.AppendLine(T + "the high scores' sides, edit mode");

            // C0 the end screen's own code
            string src = "";
            try { src = File.ReadAllText(Path.Combine(Application.dataPath, "OpenZ/Scripts/UI/OpenZEndScreen.Score.cs")); } catch (Exception) { }
            string code = Code(src.Replace("\r\n", "\n"));
            int a = code.IndexOf("string UnrankedWhy()", StringComparison.Ordinal), b = a >= 0 ? code.IndexOf("void BuildScore", a, StringComparison.Ordinal) : -1;
            string why = a >= 0 && b > a ? code.Substring(a, b - a) : "";
            bool noSide = why.Length > 0 && !why.Contains("OzPlayBlue") && !why.Contains("Team.Blue") && !why.Contains("Team.Red");
            bool reasons = why.Contains("OzLevelStats.UnrankedWhy()") && why.Contains("\"WIN THE LEVEL TO ENTER THE TABLE\"") && why.Contains("\"A DRAW DOES NOT ENTER THE TABLE\"")
                           && why.Contains("THE CPU PLAYED YOUR SIDE") && why.Contains("TEST SESSION") && why.Contains("return OzHighScores.Unranked;");
            bool entry = code.Contains("scoreEntry = OzScoreSides.Entry(scoreParts.total, W.Time, kills, losses, built, zonesMine, zonesTotal, me);")
                         && code.Contains("var W = game.World; var me = hud.Player;") && !code.Contains("new OzScoreEntry");
            Check($"C0 the end screen: no side kept out of the table ({noSide}), the other reasons still there: Custom stats, a defeat, a draw, the CPU on your side, a test session, the testing overlay ({reasons}); the entry made by OzScoreSides.Entry with the human player's team ({entry})",
                  noSide && reasons && entry);

            // C1 the sides
            bool sides = OzScoreSides.Of(Team.Red) == 0 && OzScoreSides.Of(Team.Blue) == 1 && OzScoreSides.Of(Team.Neutral) == 0
                         && OzScoreSides.Tag[0] == "ZOD" && OzScoreSides.Tag[1] == "EVILZOD"
                         && OzScoreSides.Paint(0) == (Color)OzUi.Red && OzScoreSides.Paint(1) == (Color)OzUi.Blue
                         && OzScoreSides.Text(0) == (Color)OzUi.RedText && OzScoreSides.Text(1) == (Color)OzUi.BlueText
                         && OzScoreSides.Side(null) == 0 && OzScoreSides.TagOf(new OzScoreEntry()) == "ZOD" && OzScoreSides.TagOf(new OzScoreEntry { side = 1 }) == "EVILZOD";
            Check($"C1 the sides: red {OzScoreSides.Of(Team.Red)} {OzScoreSides.Tag[0]} on the red paint, blue {OzScoreSides.Of(Team.Blue)} {OzScoreSides.Tag[1]} on the blue paint, neutral / none / an empty entry red", sides);

            try
            {
                // C2 + C3: a red and a blue victory on each difficulty
                string p2 = Fresh("oz_hs_sides_check_c2.json");
                var made = new Dictionary<GameDifficulty, (OzScoreEntry red, OzScoreEntry blue, int pr, int pb)>();
                for (int i = 0; i < Diffs.Length; i++)
                {
                    var d = Diffs[i];
                    double tr = 754.9 + 100 * i, tb = 600.2 + 100 * i;                                       // other times per difficulty: other scores
                    var sr = OzHighScores.Score(true, tr, 42, 18, 6, 7); var sbl = OzHighScores.Score(true, tb, 50, 10, 7, 7);
                    var red = OzScoreSides.Entry(sr.total, tr, 42, 18, 30, 6, 7, Team.Red);
                    var blue = OzScoreSides.Entry(sbl.total, tb, 50, 10, 33, 7, 7, Team.Blue);
                    int pr = OzHighScores.Submit("L02", d, red), pb = OzHighScores.Submit("L02", d, blue);
                    made[d] = (red, blue, pr, pb);
                }
                var got2 = new StringBuilder(); bool c2 = true;
                foreach (var d in Diffs)
                {
                    var m = made[d]; var top = OzHighScores.Top("L02", d);
                    string l = Line(top); got2.Append(' ').Append(OpenZDifficulty.Label(d)).Append(": places red ").Append(m.pr).Append(", blue ").Append(m.pb).Append(", table ").Append(l).Append(';');
                    var sBlue = OzHighScores.Score(true, m.blue.seconds + 0.2, 50, 10, 7, 7);
                    bool good = top.Count == 2 && top[0] == m.blue && top[1] == m.red && m.pr == 1 && m.pb == 1
                                && top[0].side == 1 && top[1].side == 0 && OzScoreSides.TagOf(top[0]) == "EVILZOD" && OzScoreSides.TagOf(top[1]) == "ZOD"
                                && m.blue.score == sBlue.total && m.blue.kills == 50 && m.blue.losses == 10 && m.blue.built == 33 && m.blue.zones == 7 && m.blue.zonesTotal == 7
                                && OzHighScores.Best("L02", d) == m.blue && OzHighScores.Top("L16", d).Count == 0 && OzHighScores.Top("L17", d).Count == 0;
                    c2 &= good;
                }
                Check("C2 a red and a blue victory on each difficulty: each in its own difficulty's table of the level, the blue one (the better result) first, the sides kept, the result as made, the other levels empty:" + got2, c2);

                var before = new Dictionary<GameDifficulty, string>(); foreach (var d in Diffs) before[d] = Line(OzHighScores.Top("L02", d));
                OzHighScores.UseFile(p2);                                                                    // forget the tables in memory: read the file again
                bool c3 = true; var got3 = new StringBuilder();
                foreach (var d in Diffs) { string l = Line(OzHighScores.Top("L02", d)); c3 &= l == before[d]; got3.Append(' ').Append(OpenZDifficulty.Label(d)).Append(": ").Append(l).Append(';'); }
                string file2 = File.ReadAllText(p2);
                int ones = Regex.Matches(file2, "\"side\"\\s*:\\s*1").Count, zeros = Regex.Matches(file2, "\"side\"\\s*:\\s*0").Count;
                Check($"C3 the file read again: the same rows, order and sides ({c3});{got3} the file names the side of every entry ({ones} x 1, {zeros} x 0)", c3 && ones == 3 && zeros == 3);

                // C4 the same rules for both sides
                Fresh("oz_hs_sides_check_c4.json");
                var r1 = new OzScoreEntry { kills = 1, score = 15000, seconds = 900, side = 0 }; var b1 = new OzScoreEntry { kills = 2, score = 15000, seconds = 900, side = 1 };
                OzHighScores.Submit("L16", GameDifficulty.Medium, r1); OzHighScores.Submit("L16", GameDifficulty.Medium, b1);
                var b2 = new OzScoreEntry { kills = 3, score = 15000, seconds = 900, side = 1 }; var r2 = new OzScoreEntry { kills = 4, score = 15000, seconds = 900, side = 0 };
                OzHighScores.Submit("L17", GameDifficulty.Medium, b2); OzHighScores.Submit("L17", GameDifficulty.Medium, r2);
                string t16 = Line(OzHighScores.Top("L16", GameDifficulty.Medium)), t17 = Line(OzHighScores.Top("L17", GameDifficulty.Medium));
                bool ties = t16 == "E1:ZOD:15000/900 E2:EVILZOD:15000/900" && t17 == "E3:EVILZOD:15000/900 E4:ZOD:15000/900";
                (int n, int score, int sec)[] rows =
                {
                    (1, 13750, 2000), (2, 19901, 754), (3, 1607, 400), (4, 15000, 900), (5, 15000, 800), (6, 15000, 900),
                    (7, 21000, 500), (8, 11000, 1500), (9, 12500, 1300), (10, 18000, 700), (11, 17000, 760), (12, 16000, 820),
                };
                foreach (var r in rows) OzHighScores.Submit("L02", GameDifficulty.Hard, new OzScoreEntry { kills = r.n, score = r.score, seconds = r.sec, side = r.n % 2 });   // odd = EVILZOD, even = ZOD
                const string want = "E7:EVILZOD:21000/500 E2:ZOD:19901/754 E10:ZOD:18000/700 E11:EVILZOD:17000/760 E12:ZOD:16000/820 E5:EVILZOD:15000/800 E4:ZOD:15000/900 E6:ZOD:15000/900 E1:EVILZOD:13750/2000 E9:EVILZOD:12500/1300";
                string mixed = Line(OzHighScores.Top("L02", GameDifficulty.Hard));
                Check($"C4 the same rules for both sides: equal results keep the older above (L16 \"{t16}\", L17 \"{t17}\"); twelve mixed results leave the ten best with their sides: \"{mixed}\"", ties && mixed == want);

                // C5 old rows load
                string old = "{\"version\":1,\"tables\":[{\"level\":\"L02\",\"difficulty\":\"" + OpenZDifficulty.Label(GameDifficulty.Easy) + "\",\"entries\":["
                             + "{\"name\":\"AAA\",\"score\":19901,\"seconds\":754,\"kills\":42,\"losses\":18,\"built\":30,\"zones\":6,\"zonesTotal\":7},"
                             + "{\"score\":10250,\"seconds\":1800,\"kills\":5,\"losses\":5,\"built\":9,\"zones\":3,\"zonesTotal\":7}]}]}";
                string p5 = Fresh("oz_hs_sides_check_c5.json", old);
                var had = OzHighScores.Top("L02", GameDifficulty.Easy);
                bool read = had.Count == 2 && had[0].score == 19901 && had[0].seconds == 754 && had[0].kills == 42 && had[0].losses == 18 && had[0].built == 30 && had[0].zones == 6 && had[0].zonesTotal == 7
                            && had[1].score == 10250 && had[1].seconds == 1800 && OzScoreSides.TagOf(had[0]) == "ZOD" && OzScoreSides.TagOf(had[1]) == "ZOD";
                var sb5 = OzHighScores.Score(true, 900.4, 30, 10, 5, 7);
                int p5place = OzHighScores.Submit("L02", GameDifficulty.Easy, OzScoreSides.Entry(sb5.total, 900.4, 30, 10, 20, 5, 7, Team.Blue));
                string written = File.ReadAllText(p5);
                OzHighScores.UseFile(p5);
                string again = Line(OzHighScores.Top("L02", GameDifficulty.Easy));
                string want5 = $"E42:ZOD:19901/754 E30:EVILZOD:{sb5.total}/900 E5:ZOD:10250/1800";
                Check($"C5 old rows load: {had.Count} rows read as ZOD with all their numbers ({read}); a blue victory ({sb5.total}) joins the same table at place {p5place}; read again \"{again}\"; the file holds no name ({!written.Contains("name")})",
                      read && p5place == 2 && again == want5 && !written.Contains("name"));

                // C6 an earlier build reads the new file
                var asOld = JsonUtility.FromJson<OldFile>(written);
                var newer = OzHighScores.Top("L02", GameDifficulty.Easy);
                bool c6 = asOld != null && asOld.tables != null && asOld.tables.Count == 1 && asOld.tables[0].entries.Count == newer.Count;
                if (c6) for (int i = 0; i < newer.Count; i++)
                    {
                        var o = asOld.tables[0].entries[i]; var n = newer[i];
                        c6 &= o.score == n.score && o.seconds == n.seconds && o.kills == n.kills && o.losses == n.losses && o.built == n.built && o.zones == n.zones && o.zonesTotal == n.zonesTotal;
                    }
                Check($"C6 an earlier build reads the new file (the entry without the side): {(asOld != null && asOld.tables != null && asOld.tables.Count > 0 ? asOld.tables[0].entries.Count : -1)} of {newer.Count} entries, every score, time and count there", c6);

                // C7 odd side numbers
                string odd = "{\"version\":1,\"tables\":[{\"level\":\"L17\",\"difficulty\":\"" + OpenZDifficulty.Label(GameDifficulty.Hard) + "\",\"entries\":["
                             + "{\"score\":14000,\"seconds\":600,\"kills\":1,\"side\":7},{\"score\":13000,\"seconds\":600,\"kills\":2,\"side\":-1},{\"score\":12000,\"seconds\":600,\"kills\":3,\"side\":1}]}]}";
                Fresh("oz_hs_sides_check_c7.json", odd);
                string l7 = Line(OzHighScores.Top("L17", GameDifficulty.Hard));
                Check($"C7 a side that is not 0 or 1 reads as ZOD: \"{l7}\"", l7 == "E1:ZOD:14000/600 E2:ZOD:13000/600 E3:EVILZOD:12000/600");

                // C8 the TOP 10 tooltip's lines
                Fresh("oz_hs_sides_check_c8.json");
                var e1 = new OzScoreEntry { kills = 1, score = 19901, seconds = 754, side = 1 }; var e2 = new OzScoreEntry { kills = 2, score = 13750, seconds = 2000, side = 0 };
                OzHighScores.Submit("L02", GameDifficulty.Medium, e2); OzHighScores.Submit("L02", GameDifficulty.Medium, e1);
                string tip = OzHighScores.TableLines("L02", GameDifficulty.Medium, e1);
                string blueTag = "<color=" + OzUi.Hex(OzUi.BlueText) + ">EVILZOD</color>", redTag = "<color=" + OzUi.Hex(OzUi.RedText) + ">ZOD</color>";
                var tl = tip.Split('\n');
                bool c8 = tl.Length == 2 && tl[0].StartsWith("1.  " + blueTag + "  MAJOR GENERAL  19 901  ", StringComparison.Ordinal) && tl[1].StartsWith("2.  " + redTag + "  MAJOR  13 750  ", StringComparison.Ordinal)
                          && tl[0].Contains(UnitInfo.Duration(754)) && tl[1].Contains(UnitInfo.Duration(2000)) && tl[0].EndsWith("◀", StringComparison.Ordinal) && !tl[1].Contains("◀");
                Check($"C8 the TOP 10 tooltip: \"{Regex.Replace(tip, "<[^>]+>", "").Replace("\n", " / ")}\", the tags in the sides' colours ({blueTag.Substring(0, 15)} / {redTag.Substring(0, 15)}), the new entry marked", c8);
            }
            catch (Exception e) { Check("the table checks ran without an error: " + e.GetType().Name + ": " + e.Message, false); }
            finally { OzHighScores.UseFile(null); }                                                         // the editor's own table again

            pass = ok == checks;
            lines.AppendLine($"{T}{(pass ? "PASS" : "FAIL")} {ok}/{checks}");
            return lines.ToString();
        }
    }
}
