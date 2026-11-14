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
    /// The MLRS in the v2 films (View/HeavyV2Films.cs + .Subject.cs; menus OpenZ/MLRS v2/4 .. 16). Every job of the light tank v2 (tread,
    /// battle, board, fight, snipe, turret, recoil, combat, plant) films the MLRS on the same fixed open spots of L02 (an MLRS fits where a
    /// heavy does; every slot asked again with the sim's own tests), with its node names (its belts, its launcher's turn joint, its boarding
    /// approach) and its salvo sound, into v2_units/mlrs/proof/ (take names ml2_*). Two takes of its own:
    ///  "rockets": a red v2 and a red v1 side by side fire salvos at a blue jeep (staged: it cannot be destroyed and has no weapon), the
    ///     camera close (22 m) on v2: every launch and every drawn rocket measured on the drawn model (View/MlrsV2Proof.cs MlrsV2Watch: the
    ///     tube that fired in v1's order, where the rocket is first drawn against that tube's mouth and axis, the reload slide along the tube),
    ///     and crops 1/30 s apart round each launcher from the frame before a salvo's first rocket, and round v2 while its tubes reload;
    ///  "launcher": the same pair at the game's play zoom: every frame the launcher kick (MLRS_rack's turn from its rest), the pitch joint, the
    ///     hull's rock over its planted tracks (UnitView.ProofGear) and HeavyRecoil's springs; per salvo the peaks; crops 1/30 s apart round
    ///     each launcher through a salvo; the CSV.
    /// </summary>
    public partial class HeavyV2Films
    {
        static readonly FilmSubject MlrsSubject = new FilmSubject
        {
            Type = OzMlrs.Type, Label = "MLRS", Prefix = "ml2", ProofDir = "work/staging/v1_2_20261022/v2_units/mlrs/proof", BuildMenu = "menu OpenZ/MLRS v2/1",
            Yaw = "MLRSRackYaw", Approach = "mlrs_Board_Approach", ShotKey = "mlrs_salvo", Tracks = new[] { "MLRS_RegularTread_L", "MLRS_RegularTread_R" },
            PrefabV2 = () => OzMlrs.Models.PrefabV2, Version = () => OzMlrs.Version, Pin = (o, v) => OzMlrs.Models.Pin(o, v),
        };
        /// <summary>The camera height of the "rockets" take (m): close enough to see a rocket leave its tube.</summary>
        public const float RocketsHeight = 22f;

        // ---------------------------------------------------------------- per frame (OnFrame, after every LateUpdate)
        sealed class MlrsFilm
        {
            public MlrsV2Watch w; public ZObject o; public string tag; public UnitView v;
            public int lastLaunches, lastReloads, salvo; public float salvoT0 = -1f;
            public float kickPeak, hullPeak, springPeak, trackPitchMin = 99f, trackPitchMax = -99f, trackRollMin = 99f, trackRollMax = -99f, sinkMax = -9f;
            public readonly List<string> salvoRows = new List<string>();
            public List<Color32[]> burst; public int burstLeft; public Rect burstRect; public string burstLabel; public bool burstSalvoDone, burstReloadDone;
        }
        readonly List<MlrsFilm> mlrsFilms = new List<MlrsFilm>();
        readonly List<(string label, List<Color32[]> cells)> mlrsBursts = new List<(string, List<Color32[]>)>();
        readonly StringBuilder mlrsCsv = new StringBuilder();
        const int MlrsCells = 24, MlrsCellW = 220, MlrsCellH = 165;

        /// <summary>The screen box round a launcher (its box / rack and rockets), grown a little.</summary>
        Rect LauncherBox(UnitView v)
        {
            var pitch = Node(v, "MLRSRackPitch");
            return pitch ? ScreenBox(pitch.gameObject, 0.35f) : ScreenBox(v.gameObject, 0.2f);
        }

        void SampleMlrs(Texture2D tex)
        {
            if (mlrsFilms.Count == 0) return;
            foreach (var f in mlrsFilms)
            {
                var v = View(f.o); if (!v || v.Dying || f.o.Destroyed) continue;
                f.w.Sample(Time.unscaledTime);
                // a new salvo: its first rocket after a pause
                if (f.w.Launches.Count > f.lastLaunches)
                {
                    if (f.salvoT0 < 0f || TakeT - f.salvoT0 > 2.0f) { f.salvo++; f.salvoT0 = TakeT; }
                    f.lastLaunches = f.w.Launches.Count;
                }
                bool rel = f.w.Reloads > f.lastReloads; f.lastReloads = f.w.Reloads;
                // the frame's numbers
                float kick = f.w.Rack ? Quaternion.Angle(Quaternion.identity, f.w.Rack.localRotation) : 0f;
                var hr = v.HeavyRecoilView;
                float spring = hr ? hr.BodyRockNow : 0f;
                float hp = 0f, hrl = 0f;
                if (v.ProofGear(out float tp, out float trl, out float mid, out float sink, out hp, out hrl, out float lift))
                {
                    f.trackPitchMin = Mathf.Min(f.trackPitchMin, tp); f.trackPitchMax = Mathf.Max(f.trackPitchMax, tp);
                    f.trackRollMin = Mathf.Min(f.trackRollMin, trl); f.trackRollMax = Mathf.Max(f.trackRollMax, trl); f.sinkMax = Mathf.Max(f.sinkMax, sink);
                }
                f.kickPeak = Mathf.Max(f.kickPeak, f.w.KickDegMax); f.hullPeak = Mathf.Max(f.hullPeak, Mathf.Max(Mathf.Abs(hp), Mathf.Abs(hrl))); f.springPeak = Mathf.Max(f.springPeak, spring);
                if (f.salvo > 0 && f.salvo <= 3)
                    mlrsCsv.Append($"{f.tag},{f.salvo},{frameNo},{TakeT:0.000},{f.w.Launches.Count},{kick:0.000},{hp:0.000},{hrl:0.000},{spring:0.000},{tp:0.000},{trl:0.000},{sink:0.0000}\n");
                // crops 1/30 s apart: the frame of a salvo's first rocket (salvo 2) and, on v2, the first reload slide after it
                if (f.burst == null && !f.burstSalvoDone && f.salvo == 2 && TakeT - f.salvoT0 < 0.05f)
                {
                    f.burst = new List<Color32[]>(); f.burstLeft = MlrsCells; f.burstRect = LauncherBox(v); f.burstSalvoDone = true;
                    f.burstLabel = $"{f.tag}: salvo 2 from its first rocket, {MlrsCells} frames 1/30 s apart (left to right)";
                }
                else if (f.burst == null && !f.burstReloadDone && f.burstSalvoDone && rel && f.tag.Contains("v2"))
                {
                    f.burst = new List<Color32[]>(); f.burstLeft = 18; f.burstRect = LauncherBox(v); f.burstReloadDone = true;
                    f.burstLabel = $"{f.tag}: a tube reloads (its rocket slides back in), 18 frames 1/30 s apart";
                }
                if (f.burst != null)
                {
                    f.burst.Add(Cell(tex, f.burstRect, MlrsCellW, MlrsCellH, false)); f.burstLeft--;
                    if (f.burstLeft <= 0) { mlrsBursts.Add((f.burstLabel, f.burst)); f.burst = null; }
                }
            }
        }

        void WriteMlrsCrops(string name)
        {
            if (mlrsBursts.Count == 0) { Fact("no crop burst was taken: FAIL"); return; }
            var t = new Texture2D(MlrsCells * (MlrsCellW + 4), mlrsBursts.Count * (MlrsCellH + 4), TextureFormat.RGB24, false);
            var fill = new Color32[t.width * t.height]; for (int i = 0; i < fill.Length; i++) fill[i] = new Color32(0, 0, 0, 255); t.SetPixels32(fill);
            for (int r = 0; r < mlrsBursts.Count; r++)
                for (int c = 0; c < mlrsBursts[r].cells.Count && c < MlrsCells; c++)
                    t.SetPixels32(c * (MlrsCellW + 4), t.height - (r + 1) * (MlrsCellH + 4), MlrsCellW, MlrsCellH, mlrsBursts[r].cells[c]);
            t.Apply(false);
            string p = Path.Combine(Dir, name);
            File.WriteAllBytes(p, t.EncodeToPNG()); Destroy(t);
            Fact($"crops 1/30 s apart round the launchers: {p}; rows top to bottom:");
            for (int r = 0; r < mlrsBursts.Count; r++) log.AppendLine($"  row {r + 1}: {mlrsBursts[r].label}");
        }

        /// <summary>The red v2 and v1 on the recoil spots, a blue jeep as the target (staged: unkillable, no weapon); both watched.</summary>
        bool MlrsPair(out ZObject v2, out ZObject v1)
        {
            v2 = v1 = null;
            if (!Spot("v2", out int ax2, out int ay2, "recoil") || !Spot("v1", out int ax1, out int ay1, "recoil") || !Spot("jeep", out int jx, out int jy, "recoil")) return false;
            v2 = Heavy(ax2, ay2, Team.Red, 2, "red v2"); v1 = Heavy(ax1, ay1, Team.Red, 1, "red v1");
            if (!CheckHeavy(v2, "red v2", false) || !CheckHeavy(v1, "red v1", false)) return false;
            var jeep = Vehicle("jeep", jx, jy, Team.Blue, "blue jeep (the target)");
            jeep.MaxHealth = 100000000; jeep.SetHealth(jeep.MaxHealth); W.UpdateObjectHealth(jeep); jeep.Damage = 0; jeep.AttackRadius = 0;
            Note("staged: the jeep cannot be destroyed and has no weapon (it only stands as the target); both MLRS engage it by themselves (in range)");
            foreach (var (o, tag) in new[] { (v2, "v2"), (v1, "v1") })
                mlrsFilms.Add(new MlrsFilm { o = o, tag = tag, v = View(o), w = new MlrsV2Watch(tag, View(o), tag == "v2" && View(o) && OzMlrs.BoxModel(View(o).transform) ? 2 : 1) });   // the v2 on v1's model: v1's geometry
            foreach (var f in mlrsFilms) Fact($"{f.tag}: launcher nodes found (rack, pitch, turn, 8 tubes, rockets, muzzles): {(f.w.Ok ? "PASS" : "FAIL")}");
            MlrsV2Watch.Watch(mlrsFilms.ConvertAll(f => f.w));
            return true;
        }

        IEnumerator MlrsUntilSalvos(int want, float maxS)
        {
            int f0 = frameNo;
            while (!done && frameNo - f0 < maxS * Fps)
            {
                bool enough = true; foreach (var f in mlrsFilms) if (f.salvo < want) enough = false;
                if (enough) break;
                yield return null;
            }
        }

        // ================================================================================================ "rockets": every rocket leaves its own tube
        IEnumerator MlrsRockets()
        {
            if (S.Type != OzMlrs.Type) { Finish("the 'rockets' take is the MLRS's (menu OpenZ/MLRS v2/15)"); yield break; }
            if (!MlrsPair(out var v2, out var v1)) yield break;
            hero = v2; follow.Add(v2);
            h0 = RocketsHeight;
            Note($"the camera at {h0:0} m on v2 (close: a rocket leaving its tube is a few pixels at the play zoom)");
            yield return Hold(2.5f);
            yield return StartTake();
            yield return MlrsUntilSalvos(3, 24f);
            yield return TakeWait(3.2f);                                                              // the last salvo's tubes reload (2.4 s + 0.5 s slide)
            StopTake(); WriteCrops(); WriteMlrsCrops(S.Prefix + "_rockets_crops_L02.png");
            MlrsV2Watch.Stop();
            var csv = new StringBuilder("unit,serial,tube,age_s,along_m,off_axis_m,own_tube\n");
            foreach (var f in mlrsFilms) foreach (var r in f.w.RocketRows) csv.Append(r).Append('\n');
            string cp = Path.Combine(Dir, S.Prefix + "_rockets_L02.csv"); File.WriteAllText(cp, csv.ToString());
            Fact($"every drawn rocket's first frame against the tube mouth it is nearest: {cp}");
            foreach (var f in mlrsFilms)
            {
                var w = f.w; Fact("LAUNCHER " + w.Summary());
                bool order = w.Launches.Count >= 16 && w.OrderBreaks == 0;
                bool own = w.Rockets >= 16 && w.RocketsWrongTube == 0 && w.AlongMin > -0.05f && w.AlongMax < 1.2f && w.OffAxisMax < 0.15f;
                bool reload = w.Reloads >= 16 && w.SlideBackMax > 0.3f && w.SlideBackMax < 0.44f && w.SlideOutMax < 0.005f && w.SlideOffAxisMax < 0.01f;
                if (f.tag == "v2" && w.Model == 2) Fact($"v2 rockets: tube order {(order ? "PASS" : "FAIL")}, every rocket from its own tube mouth along its axis {(own ? "PASS" : "FAIL")}, the reload slide inside the tube {(reload ? "PASS" : "FAIL")}");
                else if (f.tag == "v2") Fact($"v2 rockets (v1's model, its tubes and muzzles): tube order {(order ? "PASS" : "FAIL")}, the reload slide inside the tube {(reload ? "PASS" : "FAIL")}, every rocket from its tube as v1 (its rockets point 33 deg above its muzzle axis: shown, not judged)");
                else Fact($"v1 rockets (the control, not judged): tube order {(order ? "as before" : "BROKEN")}, reload slide {(reload ? "inside" : "OFF")} (its rockets point 33 deg above its muzzle axis)");
            }
            Fact($"{S.ShotKey} plays in the take {Count(S.ShotKey)} (rockets 2-8 of each salvo), launch {Count("launch")} (the sim's rocket 1), mlrs_reload {Count("mlrs_reload")}");
            Fact("pass rule (the lines above + the crops): from the frame before a salvo, each rocket appears at the mouth of the tube that just emptied (tubes in v1's order: the lower row from the vehicle's left, then the upper row), its exhaust behind it; no rocket ever shows through the box walls or out of its back; the white tips of the loaded tubes sit inside their tubes; on reload each white tip slides back in along its tube and stops at the rim");
        }

        // ================================================================================================ "launcher": the kick and the hull rock, v1 and v2 side by side
        IEnumerator MlrsLauncher()
        {
            if (S.Type != OzMlrs.Type) { Finish("the 'launcher' take is the MLRS's (menu OpenZ/MLRS v2/16)"); yield break; }
            if (!MlrsPair(out var v2, out var v1)) yield break;
            hero = v2; follow.Add(v2); follow.Add(v1);
            mlrsCsv.Append("unit,salvo,frame,t,rockets,rack_kick_deg,hull_pitch_deg,hull_roll_deg,spring_rock_deg,track_pitch_deg,track_roll_deg,track_sink_m\n");
            yield return Hold(2.5f);
            yield return StartTake();
            yield return MlrsUntilSalvos(3, 24f);
            yield return TakeWait(1.5f);
            StopTake(); WriteCrops(); WriteMlrsCrops(S.Prefix + "_launcher_crops_L02.png");
            MlrsV2Watch.Stop();
            string cp = Path.Combine(Dir, S.Prefix + "_launcher_frames_L02.csv"); File.WriteAllText(cp, mlrsCsv.ToString());
            Fact($"the launcher, the hull and the tracks every frame of the first three salvos of each: {cp}");
            foreach (var f in mlrsFilms)
            {
                var sk = f.v ? f.v.GetComponent<MlrsSideKick>() : null;
                bool kick = f.kickPeak > 2f && f.kickPeak < 16f, hull = f.springPeak >= 0.5f * HeavyRecoil.DesignBodyRock(OzMlrs.Type) && f.springPeak < Mathf.Max(2.5f, 1.25f * HeavyRecoil.DesignBodyRock(OzMlrs.Type));   // k93, item 151: the salvo's bigger rock along the hull, visible (at least half its design)
                bool tracks = f.trackPitchMax - f.trackPitchMin <= 0.3f && f.trackRollMax - f.trackRollMin <= 0.3f && f.sinkMax <= 0.03f;
                Fact($"{f.tag}: launcher kick peak {f.kickPeak:0.0} deg (MlrsSideKick back {(sk ? sk.KickBackNow : -1f):0.00} m, roll {(sk ? sk.KickRollNow : -1f):0.0} deg) {(kick ? "PASS" : "FAIL")}; " +
                     $"the hull rock (HeavyRecoil, once per salvo) peak {f.springPeak:0.00} deg, the hull over its tracks up to {f.hullPeak:0.00} deg {(hull ? "PASS" : "FAIL")}; " +
                     $"the tracks' pitch {f.trackPitchMin:0.00}..{f.trackPitchMax:0.00} deg, roll {f.trackRollMin:0.00}..{f.trackRollMax:0.00} deg, deepest {f.sinkMax:0.000} m in the ground {(tracks ? "PASS" : "FAIL")}; {f.salvo} salvos");
            }
            Fact($"{S.ShotKey} plays in the take {Count(S.ShotKey)}, launch {Count("launch")}");
            Fact("pass rule (the lines above + the crops): on every rocket the launcher box tips up about its hinge and rolls a little to the side that fired, then settles springily; v2's box never slides back into the red block; on every salvo the hull rocks back a little (the side it fires toward up) and settles with one smaller opposite rock; the tracks and road wheels never move with it");
        }
    }
}
#endif
