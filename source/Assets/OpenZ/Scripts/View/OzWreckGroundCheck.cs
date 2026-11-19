#if UNITY_EDITOR
// OpenZ (GPL-3.0)
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// In Play in the Unit editor, at night (0.62), frame-locked at 60 fps. Sets: "before" = OzWreckGround.On off (what test exe 14b does), "after" = on.
    /// <list type="bullet">
    /// <item>LIFTED (every vehicle kind: jeep, light v1 / v2, mech v1 / v2, medium tank, heavy v1 / v2, MLRS v1 / v2, crane, APC, Bizon, Mammoth): the
    /// vehicle is killed as a whole wreck (no break, no cook-off, big tanks too), then its hull is put in the air as in the owner's frame (3 / 4.5 /
    /// 6 m up, turned 40 / 60 / 75 deg: one on its end). After: it must come down by itself (gravity; no frame that jumps, no slow glide), lie with its
    /// lowest point within 5 cm of the ground, stable (its middle over what it lies on), still (no turn over 0.5 deg/s).</item>
    /// <item>PIECES: a Bizon broken into its pieces; at 8.3 s (stopped) up to 12 of them are put 2-5 m up, turned; they must come down and lie within
    /// 5 cm (each piece's real lowest mesh point).</item>
    /// <item>SUPPORT GONE: a piece and a whole jeep wreck laid on a still block 2 m up: they must STAY while the block stands (truly supported) and fall
    /// when it goes.</item>
    /// <item>EDGES (critic round): a whole Bizon wreck laid flat on a still deck 2.5 m high, its middle 1 m past the deck's edge: it must tip over the
    /// edge, slide off, fall and lie flat on the floor (no frame that jumps down, no point more than 5 cm into the deck while it is on it); a whole heavy
    /// wreck tilted 15 deg with one track edge on the floor and its other side on a prop: it must STAY while the prop stands and lie flat soon after the
    /// prop goes (the look at every recheck).</item>
    /// <item>PILES (the owner's case): 6 Bizons, 3 Mammoths and 3 heavies (v1 / v2) packed in a pile; the player's 3 Mammoths, 3 Bizons and 2 heavies
    /// drive through it (rams, crushes, shoves); the pile is shot dead meanwhile (2 of the player's Bizons too, on top of their crush); at game speed
    /// x1 and x4. Every 0.1 s every wreck thing is measured (whole hulls incl. flipped ones, pieces, thrown turrets): its lowest drawn point against
    /// the support under it (OzWreckGround.Under: ground, decks, what stands still); a thing that stands still in the air (over 5 cm, 0.5 s) counts
    /// its time there. PASS: none rests in the air longer than <see cref="AirRestMaxS"/>; at the end every wreck thing lies within 5 cm. COST (critic
    /// round): the real frame time of every frame (the check's own measuring frames left out: mean, 99 %, worst) and the rule's own time (its 99 %
    /// frame, its worst frame, its mean, its rays in a frame); PASS: its own 99 % frame under <see cref="KitP99Ms"/>, its mean under
    /// <see cref="KitMeanMs"/>, and the 99 % frame time after x4 within <see cref="FrameP99SlackMs"/> of before x4 (the worst frames are shown).</item>
    /// </list>
    /// </summary>
    public class OzWreckGroundCheck : MonoBehaviour
    {
        public const string Tag = "[OpenZ 1.5 Wreck Rest Ground]";
        public static string Folder = "", Mode = "all";
        public static bool Running { get; private set; }
        public static string Report { get; private set; } = "no run yet";
        public const float TolM = 0.05f, AirRestMaxS = 1.2f, StillDegS = 0.5f;
        /// <summary>The cost rows' limits (ms, in the editor): the rule's own 99 % frame and mean per frame in a pile; the 99 % frame time after x4
        /// against before x4.</summary>
        public const float KitP99Ms = 1.5f, KitMeanMs = 0.3f, FrameP99SlackMs = 3f;
        readonly Dictionary<string, (float mean, float p99, float worst, float kitP99, float kitWorst, float kitMean, int rays)> cost = new Dictionary<string, (float mean, float p99, float worst, float kitP99, float kitWorst, float kitMean, int rays)>();
        public static readonly (string type, int ver, string label)[] Kinds =
        {
            ("jeep", 0, "jeep"), ("light", 1, "light v1"), ("light", 2, "light v2"), ("medium", 1, "mech v1"), ("medium", 2, "mech v2"),
            (ZMTank.Type, 0, "medium tank"), ("heavy", 1, "heavy v1"), ("heavy", 2, "heavy v2"), ("missile_launcher", 1, "MLRS v1"),
            ("missile_launcher", 2, "MLRS v2"), ("crane", 0, "crane"), ("apc", 0, "APC"), (ZBigTanks.Bizon, 0, "Bizon"), (ZBigTanks.Mammoth, 0, "Mammoth"),
        };

        ZGame G; ZWorld W; RtsCamera rts; DayNight day;
        readonly StringBuilder md = new StringBuilder();
        int pass, fail;
        bool on0, hulls0, pieces0, big0; int force0; float speed0;

        public static void Begin(string folder, string mode)
        {
            if (Running || !ZGame.I || ZGame.I.World == null || !OzUnitEditor.Active) { Debug.LogWarning(Tag + " enter Play in the Unit editor first"); return; }
            Folder = folder; Mode = string.IsNullOrEmpty(mode) ? "all" : mode;
            ZGame.I.gameObject.AddComponent<OzWreckGroundCheck>();
        }

        void Start() { Running = true; StartCoroutine(Run()); }
        void OnDestroy() { Running = false; Time.captureFramerate = 0; OzWreckGround.MeterRecord = false; Restore(); }

        void Restore()
        {
            if (G == null) return;
            OzWreckGround.On = on0; OzWreckGround.Hulls = hulls0; OzWreckGround.Pieces = pieces0; OzBigTankWreck.On = big0; WreckFx.Force = force0; G.gameSpeed = speed0;
        }

        static void Set(string set) { OzWreckGround.On = set != "before"; OzWreckGround.Hulls = true; OzWreckGround.Pieces = true; }

        IEnumerator Run()
        {
            G = ZGame.I; W = G.World; rts = FindFirstObjectByType<RtsCamera>(); day = FindFirstObjectByType<DayNight>();
            on0 = OzWreckGround.On; hulls0 = OzWreckGround.Hulls; pieces0 = OzWreckGround.Pieces; big0 = OzBigTankWreck.On; force0 = WreckFx.Force; speed0 = G.gameSpeed;
            if (!string.IsNullOrEmpty(Folder)) Directory.CreateDirectory(Folder);
            md.AppendLine($"# Wreck rest ground check ({System.DateTime.Now:yyyy-MM-dd HH:mm}), the Unit editor, 60 fps frame-locked, night (0.62), mode {Mode}").AppendLine();
            md.AppendLine("gap = the lowest drawn point (real mesh points of every solid part) over the support right under it (OzWreckGround.Under: the walk layer, the ground, "
                + "a standing bridge deck, what stands still under it); 'before' = OzWreckGround.On off (test exe 14b), 'after' = on. The 'before' rows are not counted.").AppendLine();
            if (OzUnitEditor.Active) OzUnitEditor.SetPanelsHidden(true);
            Time.captureFramerate = 60;
            if (Mode == "lifted" || Mode == "all")
                foreach (var set in new[] { "before", "after" })
                {
                    Set(set);
                    var e = LiftedHulls(set); while (e.MoveNext()) yield return e.Current;
                    e = LiftedPieces(set); while (e.MoveNext()) yield return e.Current;
                    e = SupportGone(set); while (e.MoveNext()) yield return e.Current;
                    e = Edges(set); while (e.MoveNext()) yield return e.Current;
                }
            if (Mode == "piles" || Mode == "all")
            {
                foreach (var (set, sp) in new[] { ("after", 1f), ("after", 4f), ("before", 4f) })
                {
                    Set(set);
                    var e = Pile(set, sp); while (e.MoveNext()) yield return e.Current;
                }
                if (cost.TryGetValue("after x4", out var a4) && cost.TryGetValue("before x4", out var b4))
                {   // the critic's row: the frame time of the owner's case, before (test exe 14b) and after
                    bool ok = a4.p99 <= b4.p99 + FrameP99SlackMs;
                    md.AppendLine().AppendLine("## cost: the pile at x4, before and after (real frame time; the check's own measuring frames left out)").AppendLine();
                    md.AppendLine("| set | mean ms | 99 % ms | worst ms | the rule's own 99 % frame ms | its worst frame ms | its mean ms | its rays in a frame (most) | result |");
                    md.AppendLine("|---|---|---|---|---|---|---|---|---|");
                    md.AppendLine($"| before x4 (test exe 14b) | {F(b4.mean)} | {F(b4.p99)} | {F(b4.worst)} | {F(b4.kitP99)} | {F(b4.kitWorst)} | {F(b4.kitMean, "0.000")} | {b4.rays} | - |");
                    md.AppendLine($"| after x4 | {F(a4.mean)} | {F(a4.p99)} | {F(a4.worst)} | {F(a4.kitP99)} | {F(a4.kitWorst)} | {F(a4.kitMean, "0.000")} | {a4.rays} | {(ok ? "PASS" : "FAIL")} (99 % within {F(FrameP99SlackMs, "0")} ms of before) |");
                    Count("after", ok);
                }
            }
            Time.captureFramerate = 0;
            Restore();
            if (OzUnitEditor.Active) { OzUePlayground.ClearField(); OzUnitEditor.SetPanelsHidden(false); }
            md.AppendLine().AppendLine($"{pass} PASS, {fail} FAIL. {OzWreckGround.Status()}");
            md.AppendLine(OzRammedWreckRest.Status());
            Report = md.ToString();
            if (!string.IsNullOrEmpty(Folder)) File.WriteAllText(Path.Combine(Folder, "wreck_rest_ground_report.md"), Report);
            Debug.Log(Tag + "\n" + Report);
            Destroy(this);
        }

        void Count(string set, bool ok) { if (set == "before") return; if (ok) pass++; else fail++; }
        static string F(float v, string f = "0.00") => float.IsNaN(v) ? "-" : v.ToString(f, System.Globalization.CultureInfo.InvariantCulture);
        void Night() { if (day) day.timeOfDay = 0.62f; }

        IEnumerator Wait(float s) { float until = Time.time + s; while (Time.time < until) { Night(); yield return null; } }

        // ------------------------------------------------------------------ 1 lifted hulls
        IEnumerator LiftedHulls(string set)
        {
            md.AppendLine().AppendLine($"## {set}: whole hulls put in the air (the frame), every kind").AppendLine();
            md.AppendLine("| kind | lifted | tilt | came down after (s) | fastest fall (m/s) | jump frames | gap end | up.y end | stable | turn after rest (deg/s) | result |");
            md.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|");
            var foe = G.player == Team.Red ? Team.Blue : Team.Red;
            for (int i = 0; i < Kinds.Length; i++)
            {
                var k = Kinds[i];
                OzUePlayground.ClearField(); yield return null;
                if (!Area(out int ox, out int oy, 16, 12)) { md.AppendLine($"| {k.label} | no free ground near the camera | | | | | | | | | {(set == "before" ? "-" : "FAIL")} |"); Count(set, false); continue; }
                WreckFx.Force = 2; OzBigTankWreck.On = false;                                                  // a whole wreck: no flip, no break, no cook-off
                var o = Make(k.type, ox + 8 * 16, oy + 6 * 16, foe, k.ver);
                yield return null; yield return null;
                var mid = ZGame.PxToWorld(o.CenterX, o.CenterY); if (rts) rts.FlyTo(mid);
                var e0 = Wait(0.5f); while (e0.MoveNext()) yield return e0.Current;
                o.SetHealth(0); W.UpdateObjectHealth(o, -1);
                UnitView v = null; float until = Time.time + 4f;
                while (Time.time < until) { Night(); if (G.Views.TryGetValue(o.Id, out v) && v && v.Dying) break; yield return null; }
                if (!v || !v.Dying) { md.AppendLine($"| {k.label} | no death drawn | | | | | | | | | {(set == "before" ? "-" : "FAIL")} |"); Count(set, false); WreckFx.Force = force0; OzBigTankWreck.On = big0; continue; }
                e0 = Wait(0.4f); while (e0.MoveNext()) yield return e0.Current;
                float H = 3f + 1.5f * (i % 3), tilt = i % 3 == 0 ? 40f : i % 3 == 1 ? 60f : 75f;
                var tr = v.transform;
                tr.position += Vector3.up * H; tr.rotation = Quaternion.AngleAxis(tilt, tr.right) * tr.rotation;
                var probe = new Probe();
                float liftAt = Time.time, downAt = -1f, still = 0f, restAt = -1f, fastest = 0f, turnAfter = 0f, prevY = tr.position.y, prevV = 0f;
                int jumps = 0; Quaternion prevR = tr.rotation; Vector3 prevP = tr.position;
                while (Time.time - liftAt < 10f && v && v.gameObject.activeInHierarchy && !v.WreckSinking)
                {
                    Night(); yield return null;
                    if (!v) break;
                    float dt = Time.deltaTime; if (dt <= 0f) continue;
                    float y = tr.position.y, vDown = (prevY - y) / dt;
                    if (prevY - y > (prevV + 9.81f * dt * 1.25f) * dt + 0.02f || y - prevY > 0.08f) jumps++;   // a jump down faster than gravity, or up more than a tip's lift
                    fastest = Mathf.Max(fastest, vDown); prevV = Mathf.Max(0f, vDown); prevY = y;
                    float w = Quaternion.Angle(prevR, tr.rotation) / dt, sp = (tr.position - prevP).magnitude / dt; prevR = tr.rotation; prevP = tr.position;
                    float gap = probe.Gap(tr);
                    if (downAt < 0f && !float.IsNaN(gap) && gap <= TolM) downAt = Time.time - liftAt;
                    if (restAt < 0f) { still = !float.IsNaN(gap) && Mathf.Abs(gap) <= TolM && w < 1f && sp < 0.02f ? still + dt : 0f; if (still >= 0.5f) restAt = Time.time - liftAt; }
                    else turnAfter = Mathf.Max(turnAfter, w);
                }
                float gapEnd = v ? probe.Gap(tr) : float.NaN, upY = v ? tr.up.y : float.NaN;
                bool stable = v && probe.Stable(tr);
                float budget = Mathf.Sqrt(2f * H / 9.81f) * 1.5f + 6.5f;                                        // the fall + a whole tip-over
                bool ok = restAt >= 0f && restAt <= budget && Mathf.Abs(gapEnd) <= TolM && jumps == 0 && stable && turnAfter <= StillDegS;
                md.AppendLine($"| {k.label} | {F(H, "0.0")} m | {F(tilt, "0")} | {(restAt >= 0f ? F(restAt) : downAt >= 0f ? "down " + F(downAt) + ", never still" : "never")} | {F(fastest, "0.0")} | {jumps} | {F(gapEnd)} | {F(upY)} | {(stable ? "yes" : "NO")} | {F(turnAfter, "0.0")} | {(set == "before" ? "-" : ok ? "PASS" : "FAIL")} |");
                Count(set, ok);
                WreckFx.Force = force0; OzBigTankWreck.On = big0;
                Tidy(mid);
            }
        }

        // ------------------------------------------------------------------ 2 lifted pieces
        IEnumerator LiftedPieces(string set)
        {
            md.AppendLine().AppendLine($"## {set}: stopped wreck pieces of a Bizon put 2-5 m up").AppendLine();
            OzUePlayground.ClearField(); yield return null;
            var foe = G.player == Team.Red ? Team.Blue : Team.Red;
            if (!Area(out int ox, out int oy, 16, 12)) { md.AppendLine("no free ground near the camera: FAIL"); Count(set, false); yield break; }
            WreckFx.Force = 1; OzBigTankWreck.On = true;                                                       // it breaks into its pieces at once
            var o = Make(ZBigTanks.Bizon, ox + 8 * 16, oy + 6 * 16, foe, 0);
            yield return null; yield return null;
            var mid = ZGame.PxToWorld(o.CenterX, o.CenterY); if (rts) rts.FlyTo(mid);
            var e0 = Wait(0.5f); while (e0.MoveNext()) yield return e0.Current;
            o.SetHealth(0); W.UpdateObjectHealth(o, -1);
            e0 = Wait(8.3f); while (e0.MoveNext()) yield return e0.Current;
            var list = new List<(WreckPieces p, float h, int jumps, float prevY, float prevV, float downAt, float still, float restAt)>();
            int j = 0;
            foreach (var p in FindObjectsByType<WreckPieces>(FindObjectsSortMode.None))
            {
                if (!p || p.UnitId != o.Id || p.Fading || list.Count >= 12) continue;
                var rb = p.GetComponent<Rigidbody>(); if (!rb || !rb.isKinematic) continue;                    // stopped ones only
                float h = 2f + 3f * (j % 4) / 3f;
                p.transform.position += Vector3.up * h;
                p.transform.rotation = Quaternion.AngleAxis(25f + 12f * (j % 5), new Vector3(Mathf.Sin(j * 1.7f), 0.3f, Mathf.Cos(j * 1.7f)).normalized) * p.transform.rotation;
                list.Add((p, h, 0, p.transform.position.y, 0f, -1f, 0f, -1f)); j++;
            }
            float t0 = Time.time;
            while (Time.time - t0 < 5f)
            {
                Night(); yield return null;
                float dt = Time.deltaTime; if (dt <= 0f) continue;
                for (int i = 0; i < list.Count; i++)
                {
                    var x = list[i]; if (!x.p || x.p.Fading) continue;
                    float y = x.p.transform.position.y, vDown = (x.prevY - y) / dt;
                    if (x.prevY - y > (x.prevV + 9.81f * dt * 1.25f) * dt + 0.02f) x.jumps++;
                    x.prevV = Mathf.Max(0f, vDown); x.prevY = y;
                    float gap = PieceGap(x.p.transform);
                    if (x.downAt < 0f && gap <= TolM) x.downAt = Time.time - t0;
                    if (x.restAt < 0f) { x.still = gap <= TolM && Mathf.Abs(vDown) < 0.02f ? x.still + dt : 0f; if (x.still >= 0.3f) x.restAt = Time.time - t0; }
                    list[i] = x;
                }
            }
            md.AppendLine("| piece | lifted | came down after (s) | jump frames | gap end | result |");
            md.AppendLine("|---|---|---|---|---|---|");
            int sank = 0;
            foreach (var x in list)
            {
                if (!x.p || x.p.Fading) { sank++; if (x.restAt < 0f) { md.AppendLine($"| {(x.p ? x.p.name : "gone")} | {F(x.h, "0.0")} m | sank at its end before it came down | {x.jumps} | - | {(set == "before" ? "-" : "FAIL")} |"); Count(set, false); } continue; }
                float gEnd = PieceGap(x.p.transform);
                bool ok = x.restAt >= 0f && x.restAt <= Mathf.Sqrt(2f * x.h / 9.81f) + 1.4f && gEnd <= TolM && x.jumps == 0;
                md.AppendLine($"| {Names(x.p.transform)} | {F(x.h, "0.0")} m | {(x.restAt >= 0f ? F(x.restAt) : "never")} | {x.jumps} | {F(gEnd)} | {(set == "before" ? "-" : ok ? "PASS" : "FAIL")} |");
                Count(set, ok);
            }
            md.AppendLine().AppendLine($"{list.Count} pieces lifted, {sank} sank at their end of life during the 5 s (counted only if they never came down).");
            WreckFx.Force = force0; OzBigTankWreck.On = big0;
            Tidy(mid);
        }

        // ------------------------------------------------------------------ 3 support gone
        IEnumerator SupportGone(string set)
        {
            md.AppendLine().AppendLine($"## {set}: truly supported, then the support goes (a still block 2 m up under a piece and a whole jeep wreck)").AppendLine();
            OzUePlayground.ClearField(); yield return null;
            var foe = G.player == Team.Red ? Team.Blue : Team.Red;
            if (!Area(out int ox, out int oy, 20, 12)) { md.AppendLine("no free ground near the camera: FAIL"); Count(set, false); yield break; }
            OzBigTankWreck.On = big0;
            var jeep = Make("jeep", ox + 6 * 16, oy + 6 * 16, foe, 0);
            var heavy = Make("heavy", ox + 14 * 16, oy + 6 * 16, foe, 2);
            yield return null; yield return null;
            var mid = ZGame.PxToWorld(jeep.CenterX + 64, jeep.CenterY); if (rts) rts.FlyTo(mid);
            var e0 = Wait(0.5f); while (e0.MoveNext()) yield return e0.Current;
            UnitView jv = null, hv = null; float until = Time.time + 4f;
            WreckFx.Force = 2; jeep.SetHealth(0); W.UpdateObjectHealth(jeep, -1);                               // a whole jeep wreck (no flip, no break, no cook-off)
            while (Time.time < until && !(G.Views.TryGetValue(jeep.Id, out jv) && jv && jv.Dying)) { Night(); yield return null; }
            WreckFx.Force = 1; heavy.SetHealth(0); W.UpdateObjectHealth(heavy, -1);                             // the heavy breaks into its pieces
            until = Time.time + 4f;
            while (Time.time < until && !(G.Views.TryGetValue(heavy.Id, out hv) && hv && hv.Dying)) { Night(); yield return null; }
            WreckFx.Force = force0;
            e0 = Wait(8.3f); while (e0.MoveNext()) yield return e0.Current;
            WreckPieces piece = null; float best = 0f;
            foreach (var p in FindObjectsByType<WreckPieces>(FindObjectsSortMode.None))
            {
                if (!p || p.UnitId != heavy.Id || p.Fading) continue;
                var rb = p.GetComponent<Rigidbody>(); if (!rb || !rb.isKinematic) continue;
                if (OzWreckGround.Box(p.transform, out var pb) && pb.size.magnitude > best) { best = pb.size.magnitude; piece = p; }
            }
            if (!jv || !jv.Dying || !piece) { md.AppendLine($"no whole jeep wreck ({(jv ? "ok" : "missing")}) or no stopped heavy piece ({(piece ? "ok" : "missing")}): FAIL"); Count(set, false); Tidy(mid); yield break; }
            // the block: a plain still box collider (Default layer, no body), its top 2 m over the ground, under both; both laid on it in this frame
            var jt = jv.transform; var pt = piece.transform;
            var probe = new Probe();
            float top = ZGame.I.GroundY(jt.position.x, jt.position.z) + 2f;
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube); block.name = "OZ_CheckBlock"; Destroy(block.GetComponent<MeshRenderer>());
            block.transform.position = new Vector3(jt.position.x, top - 0.5f, jt.position.z); block.transform.localScale = new Vector3(9f, 1f, 9f);
            jt.position += Vector3.up * (top - probe.LowestY(jt));
            pt.position = new Vector3(jt.position.x + 2.5f, pt.position.y, jt.position.z + 2.5f);
            if (OzWreckGround.Lowest(pt, out float pl, out _)) pt.position += Vector3.up * (top - pl);
            Physics.SyncTransforms();
            Vector3 j0 = jt.position, p0 = pt.position;
            e0 = Wait(1.5f); while (e0.MoveNext()) yield return e0.Current;
            float jMove = jv ? (jt.position - j0).magnitude : -1f, pMove = piece && !piece.Fading ? (pt.position - p0).magnitude : -1f;
            Destroy(block); yield return null;
            float tg = Time.time, jDown = -1f, pDown = -1f;
            while (Time.time - tg < 4f)
            {
                Night(); yield return null;
                if (jv && jDown < 0f && probe.Gap(jt) <= TolM) jDown = Time.time - tg;
                if (piece && !piece.Fading && pDown < 0f && PieceGap(pt) <= TolM) pDown = Time.time - tg;
            }
            bool okJ = jMove >= 0f && jMove < 0.01f && jDown >= 0f && jDown <= 1.4f, okP = pMove >= 0f && pMove < 0.01f && pDown >= 0f && pDown <= 1.6f;
            md.AppendLine("| what | moved while on the block (m) | down after the block went (s) | result |");
            md.AppendLine("|---|---|---|---|");
            md.AppendLine($"| jeep wreck (whole) | {F(jMove, "0.000")} | {(jDown >= 0f ? F(jDown) : "never")} | {(set == "before" ? "-" : okJ ? "PASS" : "FAIL")} |");
            md.AppendLine($"| heavy v2 piece {(piece ? Names(pt) : "")} | {F(pMove, "0.000")} | {(pDown >= 0f ? F(pDown) : "never / sank")} | {(set == "before" ? "-" : okP ? "PASS" : "FAIL")} |");
            Count(set, okJ); Count(set, okP);
            WreckFx.Force = force0; OzBigTankWreck.On = big0;
            Tidy(mid);
        }

        // ------------------------------------------------------------------ 3b over an edge, propped up (critic round)
        /// <summary>A whole wreck of `type` killed in a free area near the camera; null = none.</summary>
        IEnumerator Whole(string type, int ver, System.Action<UnitView, Vector3> got)
        {
            got(null, Vector3.zero);
            OzUePlayground.ClearField(); yield return null;
            var foe = G.player == Team.Red ? Team.Blue : Team.Red;
            if (!Area(out int ox, out int oy, 24, 16)) yield break;
            WreckFx.Force = 2; OzBigTankWreck.On = false;                                                      // a whole wreck: no flip, no break, no cook-off
            var o = Make(type, ox + 12 * 16, oy + 8 * 16, foe, ver);
            yield return null; yield return null;
            var mid = ZGame.PxToWorld(o.CenterX, o.CenterY); if (rts) rts.FlyTo(mid);
            var e0 = Wait(0.5f); while (e0.MoveNext()) yield return e0.Current;
            o.SetHealth(0); W.UpdateObjectHealth(o, -1);
            UnitView v = null; float until = Time.time + 4f;
            while (Time.time < until) { Night(); if (G.Views.TryGetValue(o.Id, out v) && v && v.Dying) break; yield return null; }
            e0 = Wait(0.4f); while (e0.MoveNext()) yield return e0.Current;
            got(v && v.Dying ? v : null, mid);
        }

        static GameObject Block(string name, Vector3 centre, Vector3 size, float yawDeg)
        {   // a plain still box collider (Default layer, no body, not drawn): a deck or a prop
            var b = GameObject.CreatePrimitive(PrimitiveType.Cube); b.name = name; Destroy(b.GetComponent<MeshRenderer>());
            b.transform.SetPositionAndRotation(centre, Quaternion.Euler(0f, yawDeg, 0f)); b.transform.localScale = size;
            Physics.SyncTransforms();
            return b;
        }

        IEnumerator Edges(string set)
        {
            md.AppendLine().AppendLine($"## {set}: over a deck edge, and propped up on one side (still blocks: a deck 2.5 m high, a prop under one side)").AppendLine();
            md.AppendLine("| case | lay flat after (s) | jump frames | deepest into the deck while over it (m) | moved on the prop (m / deg) | gap end | stable | result |");
            md.AppendLine("|---|---|---|---|---|---|---|---|");
            // EDGE: a whole Bizon wreck flat on the deck, its middle 1 m past the deck's edge (it must tip over the edge, slide off, fall, lie flat)
            UnitView v = null; Vector3 mid = Vector3.zero;
            var e = Whole(ZBigTanks.Bizon, 0, (a, b) => { v = a; mid = b; }); while (e.MoveNext()) yield return e.Current;
            if (!v) { md.AppendLine($"| edge (Bizon) | no whole wreck | | | | | | {(set == "before" ? "-" : "FAIL")} |"); Count(set, false); }
            else
            {
                var tr = v.transform; var probe = new Probe();
                var fwd = Vector3.ProjectOnPlane(tr.forward, Vector3.up).normalized; if (fwd.sqrMagnitude < 0.5f) fwd = Vector3.forward;
                float yaw = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg, g0 = G.GroundY(tr.position.x, tr.position.z), top = g0 + 2.5f;
                var c0 = probe.Middle(tr);
                var deck = Block("OZ_CheckDeck", new Vector3(c0.x, g0 + 1.25f, c0.z) - fwd * (1f + 6f), new Vector3(12f, 2.5f, 12f), yaw);   // its +forward face 1 m short of the hull's middle
                tr.position += Vector3.up * (top - probe.LowestY(tr)); Physics.SyncTransforms();
                float t0 = Time.time, prevY = tr.position.y, prevV = 0f, deepest = 0f, still = 0f, restAt = -1f; int jumps = 0; Quaternion prevR = tr.rotation; Vector3 prevP = tr.position;
                while (Time.time - t0 < 12f && v && v.gameObject.activeInHierarchy && !v.WreckSinking)
                {
                    Night(); yield return null;
                    float dt = Time.deltaTime; if (dt <= 0f || !v) continue;
                    float y = tr.position.y, vDown = (prevY - y) / dt;
                    if (prevY - y > (prevV + 9.81f * dt * 1.25f) * dt + 0.02f) jumps++;
                    prevV = Mathf.Max(0f, vDown); prevY = y;
                    deepest = Mathf.Max(deepest, probe.Inside(tr, deck.transform, 1.0f));
                    float w = Quaternion.Angle(prevR, tr.rotation) / dt, sp = (tr.position - prevP).magnitude / dt; prevR = tr.rotation; prevP = tr.position;
                    float gap = probe.Gap(tr);
                    if (restAt < 0f) { still = !float.IsNaN(gap) && Mathf.Abs(gap) <= TolM && w < 1f && sp < 0.02f ? still + dt : 0f; if (still >= 0.5f) restAt = Time.time - t0; }
                }
                float gapEnd = v ? probe.Gap(tr) : float.NaN; bool stable = v && probe.Stable(tr);
                bool off = v && !probe.Over(tr, deck.transform);
                bool ok = restAt >= 0f && restAt <= 8f && jumps == 0 && deepest <= TolM && Mathf.Abs(gapEnd) <= TolM && stable && off;
                md.AppendLine($"| edge (Bizon, middle 1 m past a 2.5 m deck) | {(restAt >= 0f ? F(restAt) : "never")}{(off ? "" : ", still over the deck")} | {jumps} | {F(deepest)} | - | {F(gapEnd)} | {(stable ? "yes" : "NO")} | {(set == "before" ? "-" : ok ? "PASS" : "FAIL")} |");
                Count(set, ok);
                Destroy(deck);
                WreckFx.Force = force0; OzBigTankWreck.On = big0; Tidy(mid);
            }
            // PROPPED: a whole heavy v2 wreck tilted 15 deg, one track edge on the floor, the other side on a prop: it stays; the prop goes: it lies flat
            e = Whole("heavy", 2, (a, b) => { v = a; mid = b; }); while (e.MoveNext()) yield return e.Current;
            if (!v) { md.AppendLine($"| propped (heavy v2) | no whole wreck | | | | | | {(set == "before" ? "-" : "FAIL")} |"); Count(set, false); yield break; }
            {
                var tr = v.transform; var probe = new Probe();
                float g0 = G.GroundY(tr.position.x, tr.position.z);
                tr.rotation = Quaternion.AngleAxis(15f, tr.forward) * tr.rotation;
                tr.position += Vector3.up * (g0 - probe.LowestY(tr)); Physics.SyncTransforms();
                var c0 = probe.Middle(tr); var side = Vector3.ProjectOnPlane(tr.right, Vector3.up).normalized; var fwd = Vector3.ProjectOnPlane(tr.forward, Vector3.up).normalized;
                float lowR = probe.LowestIn(tr, c0 + side * 1.0f, side, fwd, 0.5f, 2.5f), lowL = probe.LowestIn(tr, c0 - side * 1.0f, side, fwd, 0.5f, 2.5f);
                var up = lowR > lowL ? side : -side; float h = Mathf.Max(lowR, lowL);
                float yaw = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg;
                var propC = c0 + up * 1.0f;
                var prop = Block("OZ_CheckProp", new Vector3(propC.x, (g0 + h) * 0.5f, propC.z), new Vector3(1f, Mathf.Max(0.05f, h - g0), 5f), yaw);
                Vector3 p0 = tr.position; Quaternion r0 = tr.rotation;
                var e0 = Wait(1.5f); while (e0.MoveNext()) yield return e0.Current;
                float moved = v ? (tr.position - p0).magnitude : -1f, turned = v ? Quaternion.Angle(tr.rotation, r0) : -1f;
                Destroy(prop); yield return null;
                float t0 = Time.time, prevY = tr.position.y, prevV = 0f, still = 0f, restAt = -1f; int jumps = 0; Quaternion prevR = tr.rotation; Vector3 prevP = tr.position;
                while (Time.time - t0 < 6f && v && v.gameObject.activeInHierarchy && !v.WreckSinking)
                {
                    Night(); yield return null;
                    float dt = Time.deltaTime; if (dt <= 0f || !v) continue;
                    float y = tr.position.y, vDown = (prevY - y) / dt;
                    if (prevY - y > (prevV + 9.81f * dt * 1.25f) * dt + 0.02f) jumps++;
                    prevV = Mathf.Max(0f, vDown); prevY = y;
                    float w = Quaternion.Angle(prevR, tr.rotation) / dt, sp = (tr.position - prevP).magnitude / dt; prevR = tr.rotation; prevP = tr.position;
                    float gap = probe.Gap(tr);
                    if (restAt < 0f && Time.time - t0 > 0.1f) { still = !float.IsNaN(gap) && Mathf.Abs(gap) <= TolM && w < 1f && sp < 0.02f && probe.Stable(tr) ? still + dt : 0f; if (still >= 0.5f) restAt = Time.time - t0; }
                }
                float gapEnd = v ? probe.Gap(tr) : float.NaN; bool stable = v && probe.Stable(tr);
                bool stayed = moved >= 0f && moved < 0.01f && turned < 0.5f;
                bool ok = stayed && restAt >= 0f && restAt <= 0.7f + 3.5f && jumps == 0 && Mathf.Abs(gapEnd) <= TolM && stable;
                md.AppendLine($"| propped (heavy v2 tilted 15 deg, then the prop taken away) | {(restAt >= 0f ? F(restAt) : "never (stays tilted)")} | {jumps} | - | {F(moved, "0.000")} / {F(turned, "0.0")} | {F(gapEnd)} | {(stable ? "yes" : "NO")} | {(set == "before" ? "-" : ok ? "PASS" : "FAIL")} |");
                Count(set, ok);
                WreckFx.Force = force0; OzBigTankWreck.On = big0; Tidy(mid);
            }
        }

        // ------------------------------------------------------------------ 4 piles (the owner's case)
        sealed class Track { public string kind, label; public float air, maxAir, lastGap, firstGap = float.NaN; public Vector3 p; public Quaternion r; public float still; public bool seen; }

        IEnumerator Pile(string set, float speed)
        {
            md.AppendLine().AppendLine($"## {set}: the pile at night, game speed x{F(speed, "0")} (6 Bizons, 3 Mammoths, 3 heavies; the player's 3 Mammoths, 3 Bizons, 2 heavies drive through)").AppendLine();
            OzUePlayground.ClearField(); yield return null;
            var me = G.player; var foe = me == Team.Red ? Team.Blue : Team.Red;
            if (!Area(out int ox, out int oy, 44, 24)) { md.AppendLine("no free ground near the camera: FAIL"); Count(set, false); yield break; }
            WreckFx.Force = 0; OzBigTankWreck.On = big0; G.gameSpeed = speed;
            var pile = new List<ZObject>(); var mine = new List<ZObject>();
            int px = ox + 30 * 16, py = oy + 7 * 16;
            string[] pileT = { ZBigTanks.Bizon, ZBigTanks.Bizon, ZBigTanks.Bizon, ZBigTanks.Mammoth, "heavy", ZBigTanks.Bizon, ZBigTanks.Bizon, ZBigTanks.Bizon, ZBigTanks.Mammoth, "heavy", ZBigTanks.Mammoth, "heavy" };
            for (int i = 0; i < pileT.Length; i++) pile.Add(Make(pileT[i], px + (i % 4) * 4 * 16, py + (i / 4) * 4 * 16, foe, pileT[i] == "heavy" ? 1 + i % 2 : 0));
            string[] mineT = { ZBigTanks.Mammoth, ZBigTanks.Bizon, ZBigTanks.Mammoth, ZBigTanks.Bizon, ZBigTanks.Mammoth, ZBigTanks.Bizon, "heavy", "heavy" };
            for (int i = 0; i < mineT.Length; i++) mine.Add(Make(mineT[i], ox + 6 * 16, RowY(oy, i), me, mineT[i] == "heavy" ? 1 + i % 2 : 0));
            foreach (var o in pile) o.Damage = 0; foreach (var o in mine) o.Damage = 0;
            yield return null; yield return null;
            var mid = ZGame.PxToWorld(px + 6 * 16, py + 4 * 16); if (rts) rts.FlyTo(mid);
            var e0 = Wait(1f); while (e0.MoveNext()) yield return e0.Current;
            for (int i = 0; i < mine.Count; i++) W.SendWaypoints(me, mine[i], new[] { ZWaypoint.Move(ox + 42 * 16, RowY(oy, i)) });
            var tracks = new Dictionary<string, Track>(); float t0 = Time.time, lastDeath = -1f, sample = 0f; int killed = 0, myKilled = 0;
            var ft = new List<float>(8192); double lastR = Time.realtimeSinceStartupAsDouble; bool measured = true;   // critic round: the real frame time of every frame
            OzWreckGround.MeterReset(); OzWreckGround.MeterRecord = true;
            while (Time.time - t0 < 120f)
            {
                Night(); yield return null;
                double nowR = Time.realtimeSinceStartupAsDouble; if (!measured) ft.Add((float)((nowR - lastR) * 1000.0)); lastR = nowR; measured = false;   // a frame that ran the census below is left out
                float el = Time.time - t0;
                // the shooting: one of the pile every 2.5 s from 4 s on; two of the player's Bizons on top of their crush; the rest at 40 s
                if (el > 4f + killed * 2.5f && killed < pile.Count) { var o = pile[killed]; killed++; if (!o.Destroyed && o.KillAt < 0) { o.SetHealth(0); W.UpdateObjectHealth(o, -1); } }
                if (myKilled < 2) foreach (var o in mine) if (myKilled < 2 && o.Type == ZBigTanks.Bizon && !o.Destroyed && o.KillAt < 0 && o.Crushing) { o.SetHealth(0); W.UpdateObjectHealth(o, -1); myKilled++; }
                if (el > 40f) foreach (var o in pile) if (!o.Destroyed && o.KillAt < 0) { o.SetHealth(0); W.UpdateObjectHealth(o, -1); }
                bool anyAlive = false; foreach (var o in pile) if (!o.Destroyed) anyAlive = true;
                if (!anyAlive && lastDeath < 0f) lastDeath = Time.time;
                if (lastDeath > 0f && Time.time - lastDeath > 25f) break;
                if ((sample -= Time.deltaTime) > 0f) continue;
                sample = 0.1f;
                Census(tracks, 0.1f); measured = true;
            }
            float kitWorst = OzWreckGround.MeterWorstMs, kitMean = OzWreckGround.MeterMeanMs, kitP99 = OzWreckGround.MeterP99Ms; int kitRays = OzWreckGround.MeterWorstRays;
            OzWreckGround.MeterRecord = false;
            int over = 0; float worst = 0f; string worstWhat = "-";
            var kinds = new SortedDictionary<string, (int n, float maxAir, float maxGapEnd, float maxFirst)>();
            foreach (var kv in tracks)
            {
                var t = kv.Value;
                var x = kinds.TryGetValue(t.kind, out var y) ? y : (0, 0f, float.MinValue, float.MinValue);
                kinds[t.kind] = (x.Item1 + 1, Mathf.Max(x.Item2, t.maxAir), t.seen ? Mathf.Max(x.Item3, t.lastGap) : x.Item3, float.IsNaN(t.firstGap) ? x.Item4 : Mathf.Max(x.Item4, t.firstGap));
                if (t.maxAir > AirRestMaxS) over++;
                if (t.maxAir > worst) { worst = t.maxAir; worstWhat = t.label; }
            }
            md.AppendLine("| wreck thing (how it died) | measured | largest gap when first seen (m) | longest at rest in the air (s) | largest gap at the last look (m) |");
            md.AppendLine("|---|---|---|---|---|");
            foreach (var kv in kinds) md.AppendLine($"| {kv.Key} | {kv.Value.n} | {(kv.Value.maxFirst > float.MinValue ? F(kv.Value.maxFirst) : "-")} | {F(kv.Value.maxAir)} | {(kv.Value.maxGapEnd > float.MinValue ? F(kv.Value.maxGapEnd) : "-")} |");
            bool ok = over == 0 && tracks.Count > 0;
            md.AppendLine().AppendLine($"{tracks.Count} wreck things measured; {over} rested in the air longer than {F(AirRestMaxS, "0.0")} s (worst {F(worst)} s: {worstWhat}); the pile killed {killed}, the player's Bizons killed on a crush {myKilled}. {(set == "before" ? "(not counted)" : ok ? "PASS" : "FAIL")}");
            Count(set, ok);
            // the cost (critic round): the real frame time and the rule's own time
            ft.Sort(); float mean = 0f; foreach (var x in ft) mean += x; mean /= Mathf.Max(1, ft.Count);
            float p99 = ft.Count > 0 ? ft[Mathf.Clamp(Mathf.CeilToInt(0.99f * ft.Count) - 1, 0, ft.Count - 1)] : float.NaN, fworst = ft.Count > 0 ? ft[ft.Count - 1] : float.NaN;
            cost[set + " x" + F(speed, "0")] = (mean, p99, fworst, kitP99, kitWorst, kitMean, kitRays);
            bool okCost = kitP99 <= KitP99Ms && kitMean <= KitMeanMs;
            md.AppendLine().AppendLine($"Cost: {ft.Count} frames, real frame time mean {F(mean)} ms, 99 % {F(p99)} ms, worst {F(fworst)} ms; the rule's own time: 99 % frame {F(kitP99)} ms, worst frame {F(kitWorst)} ms, " +
                $"mean {F(kitMean, "0.000")} ms a frame, at most {kitRays} rays in a frame ({OzWreckGround.MeterRays} in all; measures {OzWreckGround.Measures}, used again in a fall {OzWreckGround.Reused}). " +
                $"{(set == "before" ? "(not counted)" : okCost ? "PASS" : "FAIL")} (its 99 % frame under {F(KitP99Ms, "0.0")} ms, its mean under {F(KitMeanMs)} ms)");
            Count(set, okCost);
            G.gameSpeed = speed0; WreckFx.Force = force0;
            Tidy(mid);
        }

        static int RowY(int oy, int i) => oy + Mathf.RoundToInt((2f + i * 2.5f) * 16f);

        /// <summary>Every wreck thing now: its gap and whether it stands still; the time it stands still in the air adds up.</summary>
        void Census(Dictionary<string, Track> tracks, float dt)
        {
            foreach (var kv in G.Views)
            {
                var v = kv.Value;
                if (!v || !v.gameObject.activeInHierarchy || !v.Dying || !v.IsWreckNow || v.WreckSinking || v.Obj == null || v.Obj.Kind != ZKind.Vehicle) continue;
                var rb = v.GetComponent<Rigidbody>(); if (rb && !rb.isKinematic) continue;                  // still flying: physics has it
                string how = v.FlippedWreck ? "flipped" : OzMammothRamView.Rammed(v.Obj) ? "rammed" : OzBizonCrushView.Crushed(v.Obj) ? "crushed" : "shot";
                One(tracks, "hull " + v.GetInstanceID(), "hull " + v.Obj.Type + " (" + how + ")", v.Obj.Type + " #" + v.Obj.Id + " (" + how + ")", v.transform, dt, true);
                var tz = v.RamRestTossed; if (tz && tz.gameObject.activeInHierarchy) One(tracks, "turret " + tz.GetInstanceID(), "thrown turret " + v.Obj.Type, "turret of " + v.Obj.Type + " #" + v.Obj.Id, tz, dt, true);
            }
            foreach (var p in FindObjectsByType<WreckPieces>(FindObjectsSortMode.None))
            {
                if (!p || p.Fading) continue;
                var rb = p.GetComponent<Rigidbody>(); if (rb && !rb.isKinematic && !rb.IsSleeping()) continue;
                One(tracks, "piece " + p.GetInstanceID(), "wreck piece", "piece of #" + p.UnitId + " " + Names(p.transform), p.transform, dt, false);
            }
        }

        readonly Dictionary<string, Probe> probes = new Dictionary<string, Probe>();
        void One(Dictionary<string, Track> tracks, string key, string kind, string label, Transform t, float dt, bool hull)
        {
            if (!tracks.TryGetValue(key, out var x)) { x = new Track { kind = kind, label = label, p = t.position, r = t.rotation }; tracks[key] = x; }
            if (!probes.TryGetValue(key, out var pr)) { pr = new Probe(); probes[key] = pr; }
            float gap = hull ? pr.Gap(t) : PieceGap(t);
            bool still = (t.position - x.p).magnitude < 0.01f && Quaternion.Angle(t.rotation, x.r) < 1f;
            x.p = t.position; x.r = t.rotation;
            x.still = still ? x.still + dt : 0f;
            if (!float.IsNaN(gap)) { x.lastGap = gap; x.seen = true; if (float.IsNaN(x.firstGap)) x.firstGap = gap; }
            if (x.still >= 0.5f && !float.IsNaN(gap) && gap > TolM) { x.air += dt; x.maxAir = Mathf.Max(x.maxAir, x.air + 0.5f); }
            else if (!still || float.IsNaN(gap) || gap <= TolM) x.air = 0f;
        }

        // ------------------------------------------------------------------ measuring
        /// <summary>A piece's real lowest point over the support under it (OzWreckGround's own measure).</summary>
        static float PieceGap(Transform t)
        {
            if (!OzWreckGround.Lowest(t, out float low, out var b)) return float.NaN;
            float s = OzWreckGround.SupportUnder(b, low, t);
            return s == float.MinValue ? float.NaN : low - s;
        }

        static string Names(Transform t) { var sb = new StringBuilder(); foreach (Transform c in t) { if (sb.Length > 40) break; if (c.name != "Hull") sb.Append(c.name).Append(' '); } return sb.ToString().Trim(); }

        /// <summary>The lowest drawn solid points of a hull (every part, real mesh points) against the support right under each (OzWreckGround.Under).</summary>
        sealed class Probe
        {
            readonly List<Vector3> pts = new List<Vector3>(); readonly List<Vector3> vb = new List<Vector3>(); readonly Vector3[] low = new Vector3[32]; int sig = int.MinValue;
            static bool Solid(Renderer r)
            {
                if (!r || !r.enabled || !r.gameObject.activeInHierarchy || !(r is MeshRenderer || r is SkinnedMeshRenderer)) return false;
                if (r.sharedMaterial && r.sharedMaterial.renderQueue >= 2450) return false;
                var n = r.name.ToLowerInvariant();
                if (n.Contains("shadow") || n.Contains("label") || n.Contains("selection") || n.Contains("ring") || n.Contains("marker") || n.Contains("decal") || OzWreckGround.FxName(n)) return false;
                return !(n.Contains("lamp") || n.Contains("beacon") || n.Contains("neon") || n.Contains("glow") || n.Contains("halo") || n.Contains("lens") || n.Contains("bulb") || n.Contains("beam") || n.Contains("cone"));
            }
            void Build(Transform root)
            {
                var rs = root.GetComponentsInChildren<Renderer>();
                int s = 17; foreach (var r in rs) if (Solid(r)) unchecked { s = s * 31 + r.GetInstanceID(); s = s * 31 + r.transform.localPosition.GetHashCode() + r.transform.localRotation.GetHashCode(); }
                if (s == sig) return;
                sig = s; pts.Clear(); var inv = root.worldToLocalMatrix;
                foreach (var r in rs)
                {
                    if (!Solid(r)) continue;
                    Mesh m = null; if (r is MeshRenderer && r.TryGetComponent<MeshFilter>(out var mf)) m = mf.sharedMesh;
                    var to = inv * r.localToWorldMatrix;
                    if (m && m.isReadable && m.vertexCount > 0) { vb.Clear(); m.GetVertices(vb); int st = Mathf.Max(1, Mathf.CeilToInt(vb.Count / 600f)); for (int i = 0; i < vb.Count; i += st) pts.Add(to.MultiplyPoint3x4(vb[i])); }
                    else { var b = r.bounds; for (int c = 0; c < 8; c++) pts.Add(inv.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1)))); }
                }
            }
            public float LowestY(Transform root) { Build(root); var m = root.localToWorldMatrix; float y = float.MaxValue; foreach (var p in pts) y = Mathf.Min(y, m.MultiplyPoint3x4(p).y); return y; }
            /// <summary>The mean of its points (world).</summary>
            public Vector3 Middle(Transform root) { Build(root); var m = root.localToWorldMatrix; var s = Vector3.zero; foreach (var p in pts) s += m.MultiplyPoint3x4(p); return pts.Count > 0 ? s / pts.Count : root.position; }
            /// <summary>The lowest of its points within a band (world): +-hs along `side`, +-hf along `fwd` from `c`; float.MinValue = none.</summary>
            public float LowestIn(Transform root, Vector3 c, Vector3 side, Vector3 fwd, float hs, float hf)
            {
                Build(root); var m = root.localToWorldMatrix; float y = float.MaxValue;
                foreach (var p in pts) { var w = m.MultiplyPoint3x4(p); var d = w - c; if (Mathf.Abs(Vector3.Dot(d, side)) <= hs && Mathf.Abs(Vector3.Dot(d, fwd)) <= hf) y = Mathf.Min(y, w.y); }
                return y == float.MaxValue ? float.MinValue : y;
            }
            /// <summary>How deep its deepest point is inside a still box (a Cube primitive scaled to its size), counted only within `layer` m under the
            /// box's top (the deck: a hull passing into it); 0 = none inside.</summary>
            public float Inside(Transform root, Transform box, float layer)
            {
                Build(root); var m = root.localToWorldMatrix; float deep = 0f, h = box.localScale.y;
                foreach (var p in pts)
                {
                    var q = box.InverseTransformPoint(m.MultiplyPoint3x4(p));
                    if (Mathf.Abs(q.x) >= 0.5f || Mathf.Abs(q.z) >= 0.5f || q.y >= 0.5f) continue;
                    float d = (0.5f - q.y) * h; if (d <= layer) deep = Mathf.Max(deep, d);
                }
                return deep;
            }
            /// <summary>Its middle is over the box's footprint.</summary>
            public bool Over(Transform root, Transform box) { var q = box.InverseTransformPoint(Middle(root)); return Mathf.Abs(q.x) < 0.5f && Mathf.Abs(q.z) < 0.5f; }
            public float Gap(Transform root)
            {
                Build(root);
                if (pts.Count == 0) return float.NaN;
                var mtx = root.localToWorldMatrix; int n = 0;
                foreach (var p in pts)
                {   // the 32 lowest
                    var w = mtx.MultiplyPoint3x4(p);
                    if (n < low.Length) { low[n++] = w; continue; }
                    int hi = 0; for (int k = 1; k < n; k++) if (low[k].y > low[hi].y) hi = k;
                    if (w.y < low[hi].y) low[hi] = w;
                }
                float gap = float.MaxValue;
                for (int i = 0; i < n; i++)
                {
                    float s = OzWreckGround.Under(low[i], root);
                    if (s > float.MinValue) gap = Mathf.Min(gap, low[i].y - s);
                }
                return gap == float.MaxValue ? float.NaN : gap;
            }
            /// <summary>It lies stable: the ground point under its middle (its points' mean) is inside the box of the points that touch (within 6 cm).</summary>
            public bool Stable(Transform root)
            {
                Build(root);
                if (pts.Count < 3) return true;
                var mtx = root.localToWorldMatrix; Vector3 com = Vector3.zero; float x0 = float.MaxValue, x1 = float.MinValue, z0 = float.MaxValue, z1 = float.MinValue; int n = 0;
                int every = Mathf.Max(1, pts.Count / 3000), seen = 0;
                for (int i = 0; i < pts.Count; i += every)
                {
                    var w = mtx.MultiplyPoint3x4(pts[i]); com += w; seen++;
                    float s = OzWreckGround.Under(w, root);
                    if (s > float.MinValue && w.y - s <= 0.06f) { n++; x0 = Mathf.Min(x0, w.x); x1 = Mathf.Max(x1, w.x); z0 = Mathf.Min(z0, w.z); z1 = Mathf.Max(z1, w.z); }
                }
                com /= Mathf.Max(1, seen);
                return n >= 3 && com.x > x0 - 0.03f && com.x < x1 + 0.03f && com.z > z0 - 0.03f && com.z < z1 + 0.03f;
            }
        }

        // ------------------------------------------------------------------ staging (as kit rammed_wreck_rest_n30's check)
        ZObject Make(string type, int cx, int cy, Team team, int ver)
        {
            var o = W.CreateObject("vehicle", type, cx - 16, cy - 16, team);
            if (ver > 0)
            {
                if (type == "heavy") OzHeavyTank.Pin(o, ver);
                else foreach (var u in OzV2Units.All) if (u.Type == type) u.Pin(o, ver);
            }
            W.Emit(ZEventType.Created, o);
            if (OzUnitEditor.Active) OzUePlayground.MarkChanged();
            return o;
        }

        void Tidy(Vector3 mid)
        {
            if (!OzUnitEditor.Active) return;
            OzUePlayground.SweepDebrisIn(q => (q - mid).sqrMagnitude < 80f * 80f);
            OzUePlayground.ClearField();
            probes.Clear();
        }

        bool Area(out int ox, out int oy, int w, int h)
        {
            ox = oy = 0;
            var f = rts ? rts.focus : Vector3.zero;
            int cx = Mathf.RoundToInt(f.x * 8f) >> 4, cy = Mathf.RoundToInt(-f.z * 8f) >> 4;
            for (int r = 0; r < 50; r += 2)
                for (int dy = -r; dy <= r; dy += 2)
                    for (int dx = -r; dx <= r; dx += 2)
                    {
                        int tx = cx + dx - w / 2, ty = cy + dy - h / 2;
                        if (Free(tx, ty, w, h)) { ox = tx * 16; oy = ty * 16; return true; }
                    }
            return false;
        }

        bool Free(int tx, int ty, int w, int h)
        {
            if (tx < 1 || ty < 1 || tx + w > W.Map.width - 1 || ty + h > W.Map.height - 1) return false;
            for (int j = ty; j < ty + h; j++)
                for (int i = tx; i < tx + w; i++)
                {
                    if (!W.Path.TilePassableGrid(i, j, false, false)) return false;
                    var k = W.Map.ClassAt(i, j); if (k != ZClass.Ground && k != ZClass.Road) return false;
                    if (W.PlatformAtTile(i, j) != 0) return false;
                }
            int x0 = tx * 16 - 32, y0 = ty * 16 - 32, x1 = (tx + w) * 16 + 32, y1 = (ty + h) * 16 + 32;
            if (W.Pathing != null) foreach (var L in W.Pathing.Lanes) if (L.ZX1 > x0 && L.ZX0 < x1 && L.ZY1 > y0 && L.ZY0 < y1) return false;
            foreach (var o in W.Objects) if (!o.Destroyed && o.CenterX > x0 && o.CenterX < x1 && o.CenterY > y0 && o.CenterY < y1 && (o.IsUnit || o.Kind == ZKind.Building || o.Type == ZWorld.PlatformType)) return false;
            return true;
        }
    }
}
#endif
