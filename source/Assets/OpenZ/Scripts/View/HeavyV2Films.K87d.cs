#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.2 item 181, the medium tank's slower turret (View/UnitAim.K87d.cs) and bigger head kick (View/HeavyRecoil.K87d.cs) in the v2 films
    /// (editor only):
    ///  * THE COMBAT TEST (medium menu 14, light menu 13): after its turret lines, the slew each tank turns with (its top speed against the
    ///    light tank's) and its fastest FREE turn (frames with no shot waiting: the hurried slew and the waiting shot's deadline left out) and
    ///    the median of its turning frames, so the medium's run and the light's run can be set side by side.
    ///  * THE RECOIL TEST (medium menu 13) needs nothing new: k84b's rule judges each unit against its own design (HeavyRecoil.DesignHeadSlide),
    ///    so the medium's head is now judged against 0.26 x its head's scale (0.283 m drawn; the old 0.217 m is under the new band's floor).
    ///  * THE TURN FILM ("turn_k87d", medium menu 18): a red light tank v2 and a red medium tank side by side on the proof's open lineup, both
    ///    first on one target to the north (so both turrets start from the same bearing), then a target to the south, the north again and
    ///    the south again, one at a time (blue grunts, unarmed, unkillable until the next comes). For the first three turns the gun is still
    ///    reloading when the target comes (staged: the sim's next shot <see cref="TurnReloadK87d"/> s after the order, logged), so the turn is
    ///    the free eased slew and both fire when it is over; the last turn comes with the shot ready (the waiting shot's deadline: told, the
    ///    shell's flight time bounds it for both). Per turn and tank: the angle turned, the time until the barrel is within the gun's
    ///    tolerance, the mean turn speed and the first shot; the medium's mean speed over the light's must be 0.72-0.90 on the free turns.
    ///    The RTS camera at the game's play height on both tanks, the sound captured, the turret lines (flutters 0, jumps 0) per turn.
    /// </summary>
    public partial class HeavyV2Films
    {
        /// <summary>The turn film: the sim's next shot this many seconds after each staged order (the gun "still reloading").</summary>
        public static float TurnReloadK87d = 1.8f;
        /// <summary>The turn film: the medium's mean turn speed over the light's on a free turn must be within these.</summary>
        public static float TurnRatioLoK87d = 0.72f, TurnRatioHiK87d = 0.90f;
        /// <summary>The turn film: a gun has started its turn once its aim error is over this (deg); its turn is timed from there (the
        /// error the frame of the order is still the old target's, and the view sees the new target a tick or a held shot later).</summary>
        public static float TurnStartDegK87d = 12f;
        static readonly FieldInfo NextShotK87d = typeof(ZObject).GetField("nextDamageTime", BindingFlags.NonPublic | BindingFlags.Instance);

        /// <summary>A tracked gun's fastest drawn turn with no shot waiting (in this frame and the one before) in a phase, and the median of its
        /// turning frames (over 20 deg/s).</summary>
        static void FreeTurnK87d(YawTrack y, string phase, out float fastest, out float median, out int turning)
        {
            fastest = 0f; median = 0f; var w = new List<float>();
            for (int i = 1; i < y.head.Count; i++)
            {
                if (y.phase[i] != phase || y.phase[i - 1] != phase || y.frame[i] != y.frame[i - 1] + 1) continue;
                if (y.held[i] != 0 || y.held[i - 1] != 0) continue;
                float s = Mathf.Abs(Mathf.DeltaAngle(y.head[i - 1], y.head[i])) * Fps;
                fastest = Mathf.Max(fastest, s);
                if (s > 20f) w.Add(s);
            }
            turning = w.Count;
            if (w.Count > 0) { w.Sort(); median = w[w.Count / 2]; }
        }

        /// <summary>The combat test (light and medium), after its turret lines: the slew this tank turns with and its free turns in the fight.</summary>
        void CombatTurnK87d()
        {
            if (S.Type != OzMediumTank.Type && S.Type != OzLightTank.Type) return;
            var mine = UnitAim.SlewFor(S.Type, ZKind.Vehicle); var light = UnitAim.SlewFor(OzLightTank.Type, ZKind.Vehicle);
            float k = mine.yawSpeed / Mathf.Max(1f, light.yawSpeed);
            bool isLight = S.Type == OzLightTank.Type;
            bool setOk = isLight ? Mathf.Abs(k - 1f) < 1e-4f : k >= 0.80f && k <= 0.85f && mine.yawSmooth >= light.yawSmooth;
            Fact($"turret turn (k87d): the {S.Label}'s slew {mine.yawSpeed:0} deg/s, smoothing {mine.yawSmooth:0.000} s; the light tank's {light.yawSpeed:0} deg/s, {light.yawSmooth:0.000} s: " +
                 $"{k:0.00} x the light's top speed: {(setOk ? "PASS" : "FAIL")} ({(isLight ? "the light tank unchanged" : "the medium 0.80-0.85 x the light, eased at least as long")})");
            foreach (var y in yawTracks)
            {
                FreeTurnK87d(y, "combat", out float fast, out float med, out int n);
                if (n == 0 && fast <= 0f) continue;
                string head = $"turret {y.tag} / combat, the free turn (k87d: frames with no shot waiting): fastest {fast:0} deg/s, median of {n} turning frames {med:0} deg/s";
                if (isLight) Fact(head + $" (told: the light tank's run, for the comparison with the medium's; its slew's top speed {mine.yawSpeed:0} deg/s)");
                else Fact(head + $": {(fast <= light.yawSpeed ? "PASS" : "FAIL")} (never as fast as the light tank's top speed {light.yawSpeed:0} deg/s; its own {mine.yawSpeed:0})");
            }
        }

        sealed class TurnRowK87d { public string tag; public ZObject o; public UnitAim aim; public float h0, leftAt = -1f, onAt = -1f, shotAt = -1f; public int shots0; }

        /// <summary>The turn film (medium menu 18): the light tank v2 and the medium tank turning onto the same targets and firing.</summary>
        IEnumerator TurnK87d()
        {
            if (S.Type != OzMediumTank.Type) { Fact($"the turn film is the medium tank's (this take films the {S.Label}): FAIL"); yield break; }
            if (NextShotK87d == null) { Fact("the sim's next-shot field (ZObject.nextDamageTime) was not found: the turn film cannot stage a reloading gun: FAIL"); yield break; }
            var at = new Dictionary<string, (int x, int y)>();
            foreach (var n in new[] { "red_v1", "red_v2" }) { if (!Spot(n, out int sx, out int sy, "proof")) yield break; at[n] = (sx, sy); }
            var lt = Pinned(OzLightTank.Type, at["red_v1"].x, at["red_v1"].y, Team.Red, "red light tank v2");
            var mt = Pinned(OzMediumTank.Type, at["red_v2"].x, at["red_v2"].y, Team.Red, "red medium tank");
            if (!CheckHeavy(lt, "light", false) || !CheckHeavy(mt, "medium", false)) yield break;
            Unhurt(lt); Unhurt(mt);
            CombatStageK87b(mt, "turn film");
            // every target on the tile whose centre is midway between the two tanks' centres: both turn through the same angle (mirrored)
            int mx = Mathf.RoundToInt(((lt.CenterX + mt.CenterX) * 0.5f - 8f) / 16f), my = Mathf.RoundToInt(((lt.CenterY + mt.CenterY) * 0.5f - 8f) / 16f);
            Note($"the targets stand on tile column {mx}, 5 tiles north and south of row {my} (midway between the tanks' centres {lt.CenterX},{lt.CenterY} and {mt.CenterX},{mt.CenterY} px)");
            var places = new[] { (mx, my - 5, "north", false), (mx, my + 5, "south", true), (mx, my - 5, "north", true), (mx, my + 5, "south", true) };
            hero = mt; follow.Clear(); follow.AddRange(new[] { lt, mt });
            TrackYaw(lt, "light"); TrackYaw(mt, "medium");
            var rows = new[] { new TurnRowK87d { tag = "light", o = lt }, new TurnRowK87d { tag = "medium", o = mt } };
            int snaps0 = UnitAim.StatSnaps;
            yield return Hold(2.0f);
            yield return StartTake();
            yield return TakeWait(1.0f);
            ZObject foe = null;
            for (int p = 0; p < places.Length; p++)
            {
                var (tx, ty, where, timed) = places[p];
                bool reload = p < places.Length - 1;
                if (foe != null) foreach (var m in Members(foe)) if (!m.Destroyed) { m.SetHealth(0); W.UpdateObjectHealth(m); }
                foe = Squad("grunt", tx, ty, Team.Blue, 1, $"turn {p} target ({where})");
                foreach (var m in Members(foe)) { m.Damage = 0; m.AttackRadius = 0; m.MaxHealth = 100000000; m.SetHealth(m.MaxHealth); W.UpdateObjectHealth(m); }
                foreach (var r in rows)
                {
                    var v = View(r.o); r.aim = v ? v.GetComponent<UnitAim>() : null;
                    r.h0 = r.aim && r.aim.ProofBarrelHeading(out float h) ? h : float.NaN;
                    r.leftAt = r.onAt = r.shotAt = -1f; r.shots0 = r.aim ? r.aim.Shots : 0;
                    Order(r.o, Attack(foe));
                    if (reload) NextShotK87d.SetValue(r.o, W.Time + TurnReloadK87d);
                }
                Note(reload ? $"turn {p}: a blue grunt to the {where} (unarmed, unkillable); both tanks ordered to attack it; staged: each gun still reloading (the sim's next shot {TurnReloadK87d:0.0} s after the order)"
                            : $"turn {p}: a blue grunt to the {where}; both tanks ordered to attack it with the shot ready (the waiting shot's deadline)");
                yawPhase = $"turn {p}";
                int f0 = frameNo, end = frameNo + Mathf.RoundToInt(4.0f * Fps);
                while (!done && frameNo < end)
                {
                    float t = (frameNo - f0) / (float)Fps;
                    foreach (var r in rows)
                    {
                        if (!r.aim) continue;
                        if (r.leftAt < 0f && r.aim.AimError > TurnStartDegK87d) r.leftAt = t;                  // the turn starts: the view has the new target
                        if (r.leftAt >= 0f && r.onAt < 0f && r.aim.AimError <= UnitAim.SlewFor(r.o.Type, ZKind.Vehicle).tol) r.onAt = t;
                        if (r.shotAt < 0f && r.aim.Shots > r.shots0) r.shotAt = t;
                    }
                    yield return null;
                }
                yawPhase = "-";
                if (!timed) { Note($"turn {p}: both on the first target (the start bearing of the timed turns)"); continue; }
                var spd = new float[2];
                for (int i = 0; i < rows.Length; i++)
                {
                    var r = rows[i];
                    float turned = r.aim && r.aim.ProofBarrelHeading(out float h1) && !float.IsNaN(r.h0) ? Mathf.Abs(Mathf.DeltaAngle(r.h0, h1)) : float.NaN;
                    float dur = r.leftAt >= 0f && r.onAt > r.leftAt ? r.onAt - r.leftAt : 0f;
                    spd[i] = dur > 0f && !float.IsNaN(turned) ? turned / dur : 0f;
                    Fact($"turn {p} ({where}, {(reload ? "gun reloading: the free slew" : "shot ready: the waiting shot's deadline")}): the {r.tag} tank turned {turned:0} deg, started " +
                         $"{(r.leftAt >= 0f ? r.leftAt.ToString("0.00") + " s" : "NEVER")} after the order (aim error over {TurnStartDegK87d:0} deg), on target (within {UnitAim.SlewFor(r.o.Type, ZKind.Vehicle).tol:0} deg) " +
                         $"{(r.onAt >= 0f ? r.onAt.ToString("0.00") + " s" : "NEVER in 4 s")} after the order: the turn took {dur:0.00} s, mean {spd[i]:0} deg/s; first shot {(r.shotAt >= 0f ? r.shotAt.ToString("0.00") + " s" : "none")} after the order");
                }
                float ratio = spd[0] > 0f ? spd[1] / spd[0] : 0f;
                if (reload) Fact($"turn {p}: the medium's mean turn speed {ratio:0.00} x the light's: {(ratio >= TurnRatioLoK87d && ratio <= TurnRatioHiK87d ? "PASS" : "FAIL")} ({TurnRatioLoK87d:0.00}-{TurnRatioHiK87d:0.00}: about 15-20 % slower)");
                else Fact($"turn {p}: the medium's mean turn speed {ratio:0.00} x the light's (told: with the shot ready the shell's flight time bounds the wait for both, the deadline brings each gun on before it)");
            }
            if (foe != null) foreach (var m in Members(foe)) if (!m.Destroyed) { m.SetHealth(0); W.UpdateObjectHealth(m); }
            yield return TakeWait(1.0f);
            StopTake(); WriteCrops();
            YawFacts("turn film");
            foreach (var y in yawTracks)
                for (int p = 1; p < places.Length - 1; p++)
                {
                    FreeTurnK87d(y, $"turn {p}", out float fast, out float med, out int n);
                    Fact($"turret {y.tag} / turn {p}, the free turn: fastest {fast:0} deg/s, median of {n} turning frames {med:0} deg/s (its slew's top speed {UnitAim.SlewFor(y.o.Type, ZKind.Vehicle).yawSpeed:0})");
                }
            Fact($"snaps at a wait cap during the film: {UnitAim.StatSnaps - snaps0}: {(UnitAim.StatSnaps - snaps0 == 0 ? "PASS" : "FAIL")} (0)");
            Fact("pass rule (the counts above + the MP4): the two tanks stand side by side; on each new target both turrets swing round in one eased move, the medium's visibly a little slower than the light's, and each fires when on it (the tankshot sound); the medium's turret kicks back and eases forward on each shot; no shake, no jump; the tracks stay on the ground");
        }
    }
}
#endif
