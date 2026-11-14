#if UNITY_EDITOR
using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// k93 item 152 (the bigger hull rock along the hull), the recoil test (menus light "12", medium "13", heavy recoil) after the Play run:
    /// the DRAWN rock was right in every failed window; the test judged it wrongly (frame tables lt2 / mt2 / hv2_recoil_frames_L02.csv):
    ///  * TWO SHOTS IN ONE WINDOW: a single-barrel gun fires again about 1.07 s later (frame 32-33), inside the 1.2 s window, after its hull had
    ///    settled; the window judged that second shot by the FIRST shot's direction and size (the light v2: first shot 1.48 deg against its
    ///    design 1.45, second shot 1.91 in the first's plane, its first-frame rise 1.26 deg = 0.66 of its own peak, the spring's normal rise).
    ///    Now a shot that finds the tank settled again ends the open window and opens its own (<see cref="SplitShotsK102"/>); the heavy's second
    ///    barrel comes before the hull settles and stays in its pair's window, and a window runs on until 22 frames after its last shot (the
    ///    0.7 s settle sample was read 0.57 s after the heavy's second barrel at frame 18);
    ///  * THE SHOT'S DIRECTION IN THE HULL was read at the END of the take, with the hull's heading then: the heavy v2 had turned (it drove
    ///    off), so its window was judged as 87 % along (4.62-7.61 deg); its spring peaked at 3.42 deg, the design for about 56 % along, and the
    ///    hull drew 4.13 (with the barrel's side kick), inside that shot's band. Now the direction is kept from the shot;
    ///  * THE HEAVIES DROVE toward their target during the window (the hero's sim position moves from 17.7 s, the hull slide column falls to
    ///    -0.32 / -0.55 m, the drive tilted the heavy v1's head 2.2 deg and 4.5 cm in the hull's frame: its "0.045 m off rest"): their target
    ///    stood 122-132 px off, the case k84b fixed for the light tank; the heavy now gets the same nearer target in its gun's reach with a
    ///    tile to spare (<see cref="HeavyReachK102"/>);
    ///  * NO SHOT ALONG A V2 HULL: the light / medium v2 spots fire 3-19 % along their hulls, so the bigger along rock was only seen on the v1
    ///    windows. A tank with three windows and none at least <see cref="AlongShareK102"/> along its hull gets a second target (a blue jeep,
    ///    unkillable, no weapon) straight ahead of or behind its hull within its gun's reach, and is ordered to fire at it
    ///    (<see cref="AlongStageK102"/>); its next windows are judged by the along design.
    /// </summary>
    public partial class HeavyV2Films
    {
        public static bool SplitShotsK102 = true, AlongStageK102 = true, HeavyReachK102 = true;
        /// <summary>Frames a window runs past its last shot (the 21-frame settle sample + 1); the least share along the hull that counts as a shot
        /// along it; the seconds a staged along-hull target is given before it is told and left.</summary>
        public static int SettleFramesK102 = 22;
        public static float AlongShareK102 = 0.6f, AlongWaitK102 = 8f;
        readonly Dictionary<RecWin, Vector3> recLocalK102 = new Dictionary<RecWin, Vector3>();
        readonly Dictionary<RecWatch, (ZObject jeep, int frame, bool left)> recAlongK102 = new Dictionary<RecWatch, (ZObject, int, bool)>();
        int recSplitsK102;

        /// <summary>SampleRecoil, a shot: the open window ends when the tank had settled again before this shot (the shot opens its own).</summary>
        void RecSplitAtK102(RecWatch w)
        {
            if (!SplitShotsK102 || w.cur == null || !w.prevRest || w.cur.shots.Count == 0 || frameNo - w.cur.start < 12) return;
            if (frameNo - w.cur.start - w.cur.shots[w.cur.shots.Count - 1] < SettleFramesK102) return;    // verify: settled = its settle time passed too (a spring zero crossing reads as rest; the heavy's second barrel stays in its pair's window)
            w.done.Add(w.cur); w.cur = null; recSplitsK102++;
        }

        /// <summary>SampleRecoil, a window opened: the shot's direction in the hull's frame, as the hull stood at the shot.</summary>
        void RecOpenK102(RecWatch w, UnitView v) { if (w.cur != null && v) recLocalK102[w.cur] = v.transform.InverseTransformDirection(w.cur.fire); }

        /// <summary>The window's length in frames: at least the old 36, and 22 frames past its last shot.</summary>
        int RecEndK102(RecWin c) => !SplitShotsK102 || c.shots.Count == 0 ? WinFrames : Mathf.Max(WinFrames, c.shots[c.shots.Count - 1] + SettleFramesK102 + 1);

        /// <summary>RecRuleK93: the shot's direction in the hull's frame (kept from the shot; the hull now when none was kept).</summary>
        Vector3 RecLocalK102(RecWin c, UnitView v) => recLocalK102.TryGetValue(c, out var l) ? l : v.transform.InverseTransformDirection(c.fire);

        float RecShareK102(RecWin c, UnitView v) { var l = RecLocalK102(c, v); l.y = 0f; return l.sqrMagnitude > 1e-6f ? Mathf.Abs(l.normalized.z) : 0f; }

        /// <summary>The take's loop, for a tank with its three windows: true when it has a window along its hull (or none could be staged);
        /// else its along-hull target is staged once and the take waits for its window (at most AlongWaitK102 s).</summary>
        bool RecAlongK102(RecWatch w)
        {
            if (!AlongStageK102 || w.o == null || w.o.Destroyed) return true;
            var v = w.v ? w.v : View(w.o); if (!v) return true;
            foreach (var c in w.done) if (RecShareK102(c, v) >= AlongShareK102) return true;
            if (recAlongK102.TryGetValue(w, out var st))
            {
                if (st.left) return true;
                if (frameNo - st.frame <= AlongWaitK102 * Fps) return false;
                recAlongK102[w] = (st.jeep, st.frame, true);
                Note($"k102: {w.tag}: no shot pair at rest along its hull {AlongWaitK102:0} s after its target ahead was staged: told");
                return true;
            }
            var u = W.S.Unit(w.o.Type); int reach = (u != null ? u.attack_radius : 120) - 24;
            var fwd = v.transform.forward; fwd.y = 0f; fwd.Normalize();
            ZObject j = null; float share = 0f, dist = 0f;
            foreach (var sgn in new[] { -1f, 1f })                                                         // the model's front (-z) first, then its back
            {
                var px = ZGame.WorldToPx(v.transform.position + fwd * sgn * (Mathf.Max(48, reach) / 8f));
                if (!OzCheatSpawner.FreeTile(W, px.x / 16, px.y / 16, 2, false, out int fx, out int fy)) continue;
                var to = ZGame.PxToWorld(fx * 16 + 16, fy * 16 + 16) - v.transform.position; to.y = 0f;
                dist = Vector2.Distance(new Vector2(fx * 16 + 16, fy * 16 + 16), new Vector2(w.o.CenterX, w.o.CenterY));
                share = to.sqrMagnitude > 1e-4f ? Mathf.Abs(Vector3.Dot(to.normalized, fwd)) : 0f;
                if (dist > reach + 8 || share < 0.8f) continue;
                j = Vehicle("jeep", fx, fy, Team.Blue, $"blue jeep along the {w.tag}'s hull (k102)");
                j.MaxHealth = 100000000; j.SetHealth(j.MaxHealth); W.UpdateObjectHealth(j); j.Damage = 0; j.AttackRadius = 0;
                break;
            }
            if (j == null) { recAlongK102[w] = (null, frameNo, true); Note($"k102: {w.tag}: no open tile along its hull within its gun's reach: told (its windows are all across the hull)"); return true; }
            Order(w.o, Attack(j));
            recAlongK102[w] = (j, frameNo, false);
            Note($"k102: {w.tag}: its windows were all across its hull; staged a target along it: the jeep #{j.Id} {dist:0} px off, {share * 100f:0} % along (unkillable, no weapon), ordered to fire at it");
            return false;
        }

        /// <summary>The verdicts' note: how many windows were split at a later settled shot.</summary>
        string RecSplitNoteK102() => $"k102: windows ended at a later shot that found the tank settled (that shot judged in its own window): {recSplitsK102}";
    }
}
#endif
