#if UNITY_EDITOR
// OpenZ (GPL-3.0)
// switched on with their numbers, the red start is left alone, and the game's blue lists are exactly the lists the off-line framing
// check proved (the kit's proof/blue_lists.json, read from the staging folder when it is there).
using System;
using System.IO;
using UnityEngine;

namespace OpenZ
{
    public static partial class OzBlueFlyoverMenu
    {
        [Serializable] sealed class ProvedLists { public ZodCineShotDef[] B02 = null, B16 = null, B17 = null; }         // filled by JsonUtility

        static void Counted2(Action<string, bool> Check)
        {
            {   // level 7 (gap 8): both fort frames are the numbers shot_math.py proved, in the code list and in the data file that beats it
                var e7 = ZodShotList.L07.shots[3]; var r7 = ZodShotList.B07.shots[2];
                Check("L07: enemy_fort is the proved frame (yaw " + e7.yawA + " to " + e7.yawB + ", pitch " + e7.pitchA + " to " + e7.pitchB + ", " + e7.distA + " to " + e7.distB + " m); blue red_fort too (yaw " + r7.yawA + " to " + r7.yawB + ", pitch " + r7.pitchA + " to " + r7.pitchB + ", " + r7.distA + " to " + r7.distB + " m, frameYaw " + r7.frameYaw + ", aimUp " + r7.aimUp + ")",
                    e7.name == "enemy_fort" && Near(e7.yawA, 252f) && Near(e7.yawB, 264f) && Near(e7.pitchA, 39f) && Near(e7.pitchB, 36f) && Near(e7.distA, 112f) && Near(e7.distB, 100.8f) && Near(e7.frameYaw, 12f)
                    && r7.name == "red_fort" && Near(r7.yawA, 288f) && Near(r7.yawB, 276f) && Near(r7.pitchA, 41f) && Near(r7.pitchB, 38f) && Near(r7.distA, 114f) && Near(r7.distB, 102.6f) && Near(r7.frameYaw, -14f) && Near(r7.aimUp, 6f));   // row 68
            }
            Check("the blue start hold is on: the start sting and lines wait for the blue flyover's hand-back (ZodFlyover.BlueStartHold), at most " + ZodFlyover.BlueHoldMaxSeconds.ToString("0") + " s after the level load (20..60)",
                ZodFlyover.BlueStartHold && ZodFlyover.BlueHoldMaxSeconds >= 20f && ZodFlyover.BlueHoldMaxSeconds <= 60f);
            Check("the robot antics run under the blue flyover's hold (AnticDirector.FlyoverAntics) and under the red briefing's flyover too (FlyoverAnticsRed on: the same frozen look was wrong there), the drive on (FlyoverAll: every standing robot, the phone or the drink)",
                AnticDirector.FlyoverAntics && AnticDirector.FlyoverAnticsRed && AnticDirector.FlyoverAll);
            Check($"the flyover drive's numbers: everyone within {AnticDirector.FlyoverFirstWindow:F2} s of the hold ({AnticDirector.FlyoverStartsPerFrame} starts a frame), a restart within {AnticDirector.FlyoverRestartGap:F2} s (at most 0.35), pre-rolled up to {AnticDirector.FlyoverPrerollMax:F2} of a segment, the end {AnticDirector.FlyoverEndLead:F2} s before the hand-back (0.3..1), the can cap {AnticDirector.FlyoverCanCap} under the hold (16..64)",
                AnticDirector.FlyoverFirstWindow <= 0.35f && AnticDirector.FlyoverStartsPerFrame >= 8 && AnticDirector.FlyoverRestartGap <= 0.35f && AnticDirector.FlyoverPrerollMax > 0.3f && AnticDirector.FlyoverPrerollMax <= 1f
                && AnticDirector.FlyoverEndLead >= 0.3f && AnticDirector.FlyoverEndLead <= 1f && AnticDirector.FlyoverCanCap >= 16 && AnticDirector.FlyoverCanCap <= 64);
            Check($"the drive's second pass: the held sim's soft rules stand down (orders, the drawn step, a selection, a far enemy: FlyoverIgnoresThreats {AnticDirector.FlyoverIgnoresThreats}), the alive layer on (head {AnticDirector.AliveHeadDeg:F1} deg in an antic, look-around {AnticDirector.AliveLookDeg:F0} deg and a weight shift of {AnticDirector.AliveShiftDeg:F1} deg without one; 1..6 / 10..35 / 1..5)",
                AnticDirector.FlyoverIgnoresThreats && AnticDirector.FlyoverAlive && AnticDirector.AliveHeadDeg >= 1f && AnticDirector.AliveHeadDeg <= 6f && AnticDirector.AliveLookDeg >= 10f && AnticDirector.AliveLookDeg <= 35f && AnticDirector.AliveShiftDeg >= 1f && AnticDirector.AliveShiftDeg <= 5f);
            Check("the normal antics rules are the old ones (40 s of play, 12-20 s of idling, 2 a team, 3 on screen, 5 s between starts)",
                AnticDirector.MinLevelTime == 40f && AnticDirector.IdleMin == 12f && AnticDirector.IdleJitter == 8f && AnticDirector.MaxPerTeam == 2 && AnticDirector.MaxOnScreen == 3 && AnticDirector.MinStartGap == 5f);
            // the lists against the proved numbers
            string st = OzBlueFlyoverProof.Staging();
            string path = st.Length > 0 ? st + "/k75c2_flyover_fixes/proof/blue_lists.json" : "";
            if (path.Length == 0 || !File.Exists(path)) { Debug.Log("[OpenZ V1.2] Blue Flyovers counted check: the kit's proof/blue_lists.json is not beside this project (" + (path.Length > 0 ? path : "no staging folder") + "): the lists are not compared with the off-line framing check here"); return; }
            ProvedLists pl = null;
            try { pl = JsonUtility.FromJson<ProvedLists>(File.ReadAllText(path)); } catch (Exception e) { Check("the kit's proof/blue_lists.json reads (" + e.Message + ")", false); return; }
            Check("the kit's proof/blue_lists.json reads: three lists of four shots", pl != null && pl.B02 != null && pl.B16 != null && pl.B17 != null && pl.B02.Length == 4 && pl.B16.Length == 4 && pl.B17.Length == 4);
            if (pl == null) return;
            foreach (var (L, mine, proved) in new[] { ("L02", ZodShotList.B02, pl.B02), ("L16", ZodShotList.B16, pl.B16), ("L17", ZodShotList.B17, pl.B17) })
            {
                string diff = Diff(mine, proved);
                Check(L + " blue: the game's list is the list the off-line framing check proved (every number of every shot)" + (diff.Length > 0 ? ": DIFFERS " + diff : ""), diff.Length == 0);
            }
        }

        static string Diff(ZodShotList mine, ZodCineShotDef[] proved)
        {
            if (mine?.shots == null || proved == null) return "no list";
            if (mine.shots.Length != proved.Length) return $"{mine.shots.Length} shots in the game, {proved.Length} proved";
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < proved.Length; i++)
            {
                var a = mine.shots[i]; var b = proved[i];
                void F(string what, float x, float y) { if (Mathf.Abs(x - y) > 0.01f) sb.Append($" shot {i + 1} {what} {x:0.##} vs {y:0.##};"); }
                void V(string what, Vector3 x, Vector3 y) { if (Vector3.Distance(x, y) > 0.01f) sb.Append($" shot {i + 1} {what} {x} vs {y};"); }
                if (a.name != b.name) sb.Append($" shot {i + 1} name {a.name} vs {b.name};");
                if (a.subject != b.subject) sb.Append($" shot {i + 1} subject {a.subject} vs {b.subject};");
                if (a.ease != b.ease) sb.Append($" shot {i + 1} ease {a.ease} vs {b.ease};");
                F("yawA", a.yawA, b.yawA); F("yawB", a.yawB, b.yawB); F("pitchA", a.pitchA, b.pitchA); F("pitchB", a.pitchB, b.pitchB);
                F("distA", a.distA, b.distA); F("distB", a.distB, b.distB); F("fovA", a.fovA, b.fovA); F("fovB", a.fovB, b.fovB);
                F("frameYaw", a.frameYaw, b.frameYaw); F("aimUp", a.aimUp, b.aimUp); F("dip", a.dip, b.dip); F("floor", a.floor, b.floor);
                F("heightA", a.heightA, b.heightA); F("heightB", a.heightB, b.heightB);
                F("point.x", a.point.x, b.point.x); F("point.y", a.point.y, b.point.y);
                V("camA", a.camA, b.camA); V("camB", a.camB, b.camB); V("lookA", a.lookA, b.lookA); V("lookB", a.lookB, b.lookB);
                if (a.whoosh != b.whoosh) sb.Append($" shot {i + 1} whoosh;");
                if ((a.cutWord ?? "") != (b.cutWord ?? "") || a.cutAt != b.cutAt || a.cutCue != b.cutCue) sb.Append($" shot {i + 1} cut anchor;");
            }
            return sb.ToString();
        }
    }
}
#endif
