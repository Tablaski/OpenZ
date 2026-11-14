#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// Menu OpenZ/V1.3/Gatling tilt/3, the unit editor open: on the floor at a floor target, from the floor up at both decks, from both decks down at the floor (a ramp platform's own case);
    /// menu 4, a level playing: a gatling on two tower pads of the player's fort at a tank below. Each case fires on a real attack order and is measured on the drawn transforms, frame by
    /// frame and at each round (UnitAim.Shots counts the rounds drawn):
    ///  A rounds drawn; B the barrels point the right way (up / down, to the wanted height or to their stop); C at a round the barrels' height against the line to the target, inside the stops;
    ///  D the bearing likewise; E every round starts at UnitView.MuzzlePos = the Muzzle node = the barrels' real end; F the head stays in its stops; G the skirt follows to its stop;
    ///  H the barrels spun; I the drum and yoke (TurretYaw) never tilted. On the field also J: the target gone, the head comes back to rest.
    /// Pictures for the eye go to the Folder.
    /// </summary>
    [DefaultExecutionOrder(1000)]                                                                        // after UnitAim's LateUpdate: the frame's final pose
    public sealed class OzGatlingTiltCheck : MonoBehaviour
    {
        const string Tag = "[OpenZ GatlingTilt]";
        const int MinRounds = 10, FieldChecks = 51, TowerChecks = 20; const float CaseSeconds = 9f, BarrelEnd = 1.40f, TipTol = 0.05f, SlopDeg = 7f;   // the barrels' mesh ends 1.40 from BarrelSpin; 7 = the gatling's tolerance 5 + 2, as P4 of the platform check
        public static string Report = "not run";
        public static string Folder;
        public static bool Running => run;
        static OzGatlingTiltCheck run;
        int pass, fail; readonly StringBuilder lines = new StringBuilder(); readonly List<string> pictures = new List<string>();
        ZGame g; ZWorld w; bool tower;

        sealed class Probe
        {
            public UnitView v, tv; public UnitAim aim; public Transform yaw, pitch, spin, skirt, muzzle; public float scale;
            public int shots0, rounds, frames, notMuzzle, offTip, outOfStops, skirtOff, yawTilted, atStop, inside;
            public float min = 999f, max = -999f, worstElev, worstYaw, worstTip, worstStop, spun, wantElev; public Quaternion spinWas;
            public bool picture; public string name;
        }
        Probe pr;

        public static void Begin(bool onTower)
        {
            if (run) { Debug.LogWarning(Tag + " a run is still going"); return; }
            if (!Application.isPlaying) return;
            if (!onTower && (!OzUnitEditor.UiBuilt || OzUnitEditor.I == null || OzUnitEditor.I.World == null)) { Debug.LogWarning(Tag + " open the unit editor first"); return; }
            if (onTower && (!ZGame.I || ZGame.I.World == null || OzUnitEditor.I != null)) { Debug.LogWarning(Tag + " a level must be playing"); return; }
            var go = new GameObject("OzGatlingTiltCheck"); DontDestroyOnLoad(go);
            run = go.AddComponent<OzGatlingTiltCheck>(); run.tower = onTower;
        }

        void Start() { StartCoroutine(tower ? TowerRun() : FieldRun()); }
        void OnDestroy() { if (run == this) run = null; }

        void Check(bool ok, string what) { if (ok) pass++; else fail++; lines.Append(ok ? "PASS  " : "FAIL  ").AppendLine(what); }
        void Info(string what) { lines.Append("INFO  ").AppendLine(what); }
        static IEnumerator Seconds(float s) { float t0 = Time.realtimeSinceStartup; while (Time.realtimeSinceStartup - t0 < s) yield return null; }
        static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }
        UnitView View(ZObject o) => o != null && g.Views.TryGetValue(o.Id, out var v) ? v : null;
        static ZObject Heal(ZObject o) { if (o != null) o.MaxHealth = o.Health = 900000000; return o; }
        static Transform Deep(Transform t, string name) { foreach (var c in t.GetComponentsInChildren<Transform>(true)) if (c.name == name) return c; return null; }
        static ZObject Lead(UeDrop d) => d.ok && d.made != null && d.made.Count > 0 ? d.made[0] : null;

        void Shot(string name)
        {
            if (string.IsNullOrEmpty(Folder)) return;
            Directory.CreateDirectory(Folder);
            string b = name + "_" + Screen.width + "x" + Screen.height, path = Path.Combine(Folder, b + ".png");
            int n = 1; while (File.Exists(path)) path = Path.Combine(Folder, b + "_" + (++n) + ".png");    // never over an older picture
            ScreenCapture.CaptureScreenshot(path); pictures.Add(path);
        }

        /// <summary>Height of a direction over the plane the gun turns in (deg).</summary>
        static float Elev(Vector3 dir, Vector3 up) => 90f - Vector3.Angle(up, dir);

        void LateUpdate()
        {
            var p = pr; if (p == null || !p.v || !p.aim || !p.muzzle) return;
            var up = p.yaw.up; float head = Elev(p.pitch.right, up), sk = Elev(p.skirt.right, up);
            p.frames++; p.min = Mathf.Min(p.min, head); p.max = Mathf.Max(p.max, head);
            if (head < UnitAim.GatlingPitchMin - 0.1f || head > UnitAim.GatlingPitchMax + 1.6f) p.outOfStops++;                  // + the shot's kick (1 deg x 1.5)
            if (Mathf.Abs(sk - Mathf.Max(head, UnitAim.GatlingSkirtStop)) > 0.2f) p.skirtOff++;
            if (Vector3.Angle(p.yaw.up, p.v.transform.up) > 0.05f) p.yawTilted++;
            p.spun += Quaternion.Angle(p.spinWas, p.spin.localRotation); p.spinWas = p.spin.localRotation;
            int n = p.aim.Shots - p.shots0; if (n <= p.rounds) return;
            p.rounds = n;
            var m = p.muzzle.position; var dir = p.muzzle.TransformDirection(Vector3.back).normalized; var at = p.tv ? p.tv.AimPos() : m + dir;
            if ((p.v.MuzzlePos() - m).magnitude > 0.002f) p.notMuzzle++;
            float tip = (m - p.spin.TransformPoint(new Vector3(BarrelEnd, 0f, 0f))).magnitude; p.worstTip = Mathf.Max(p.worstTip, tip);
            if (tip > TipTol * p.scale) p.offTip++;
            var want = (at - m).normalized; float eb = Elev(dir, up), ew = Elev(want, up);
            if (ew < UnitAim.GatlingPitchMin - 1f || ew > UnitAim.GatlingPitchMax + 1f) { p.atStop++; p.worstStop = Mathf.Max(p.worstStop, Mathf.Abs(eb - Mathf.Clamp(ew, UnitAim.GatlingPitchMin, UnitAim.GatlingPitchMax))); }
            else { p.inside++; p.worstElev = Mathf.Max(p.worstElev, Mathf.Abs(eb - ew)); }
            p.wantElev = ew;
            var fa = Vector3.ProjectOnPlane(dir, up); var fb = Vector3.ProjectOnPlane(want, up);
            if (fa.sqrMagnitude > 1e-4f && fb.sqrMagnitude > 1e-4f) p.worstYaw = Mathf.Max(p.worstYaw, Mathf.Abs(Vector3.SignedAngle(fa, fb, up)));
            if (!p.picture && p.rounds >= 5) { p.picture = true; Shot("tilt_" + p.name); }
        }

        /// <summary>One case: a fires at t on an attack order. sign = +1 up, -1 down, 0 level ground (a small tilt down). 9 checks, 10 with the head's return.</summary>
        IEnumerator Fire(string name, ZObject a, ZObject t, int sign, bool back)
        {
            var v = View(a); var tv = View(t); var aim = v ? v.GetComponent<UnitAim>() : null;
            bool rig = v && tv && aim && aim.GatlingRig;
            string how = name + ": ";
            if (!rig)
            {
                Check(false, how + "the gatling has its head and skirt bound: run menu 1 first");
                for (int i = 1; i < (back ? 10 : 9); i++) Check(false, how + "not measured");
                yield break;
            }
            var p = new Probe { v = v, tv = tv, aim = aim, yaw = Deep(v.transform, "TurretYaw"), pitch = Deep(v.transform, "BarrelPitch"), spin = Deep(v.transform, "BarrelSpin"), skirt = Deep(v.transform, UnitAim.GatlingSkirtNode), muzzle = Deep(v.transform, "Muzzle"), scale = v.transform.lossyScale.x, shots0 = aim.Shots, name = name.Replace(' ', '_') };
            p.spinWas = p.spin.localRotation; pr = p;
            Heal(a); Heal(t); t.Damage = 0; t.MoveSpeed = 0;
            var order = new[] { new ZWaypoint { mode = WpMode.Attack, refId = t.Id, x = t.CenterX, y = t.CenterY, playerGiven = true } };
            w.SendWaypoints(a.Owner, a, order);
            float t0 = Time.realtimeSinceStartup, nudge = 2f;
            while (Time.realtimeSinceStartup - t0 < CaseSeconds)
            {
                Heal(a); Heal(t);
                if ((nudge -= Time.unscaledDeltaTime) <= 0f) { nudge = 2f; if (a.AttackObject != t && a.Waypoints.Count == 0) w.SendWaypoints(a.Owner, a, order); }
                if (p.rounds >= MinRounds * 3 && Time.realtimeSinceStartup - t0 > 3f) break;
                yield return null;
            }
            pr = null;
            Info(how + p.rounds + " rounds, " + p.frames + " frames; the head " + p.min.ToString("0.0") + " .. " + p.max.ToString("0.0") + " deg; wanted " + p.wantElev.ToString("0.0") + " deg; " + p.inside + " rounds inside the stops, " + p.atStop + " at a stop");
            Check(p.rounds >= MinRounds, "A " + how + p.rounds + " rounds drawn");
            float reach = sign > 0 ? p.max : p.min, wanted = Mathf.Clamp(p.wantElev, UnitAim.GatlingPitchMin, UnitAim.GatlingPitchMax);
            Check(sign == 0 ? Mathf.Abs(wanted - p.min) <= 3f && p.min < 0f : sign * reach > 5f && Mathf.Abs(reach - wanted) <= 3f, "B " + how + "the barrels went to " + reach.ToString("0.0") + " deg, wanted " + wanted.ToString("0.0"));
            Check(p.worstElev <= SlopDeg && p.worstStop <= 2f, "C " + how + "worst height against the target at a round " + p.worstElev.ToString("0.0") + " deg (limit " + SlopDeg + "), against the stop " + p.worstStop.ToString("0.0"));
            Check(p.worstYaw <= SlopDeg, "D " + how + "worst bearing at a round " + p.worstYaw.ToString("0.0") + " deg");
            Check(p.notMuzzle == 0 && p.offTip == 0, "E " + how + "every round left the Muzzle (" + p.notMuzzle + " not), worst " + p.worstTip.ToString("0.000") + " m from the barrels' end (" + p.offTip + " over " + (TipTol * p.scale).ToString("0.000") + ")");
            Check(p.outOfStops == 0, "F " + how + p.outOfStops + " frames out of the stops " + UnitAim.GatlingPitchMin + " .. " + UnitAim.GatlingPitchMax);
            Check(p.skirtOff == 0, "G " + how + p.skirtOff + " frames with the skirt off the head or its stop");
            Check(p.spun > 360f, "H " + how + "the barrels spun " + p.spun.ToString("0") + " deg");
            Check(p.yawTilted == 0, "I " + how + p.yawTilted + " frames with the drum and yoke tilted");
            if (back)
            {
                t.KillAt = w.Time; float t1 = Time.realtimeSinceStartup, e = 99f;
                while (Time.realtimeSinceStartup - t1 < 4f) { e = Mathf.Abs(Elev(p.pitch.right, p.yaw.up)); if (e < 1f) break; yield return null; }
                Check(e < 1f, "J " + how + "target gone: the head back at " + e.ToString("0.0") + " deg in " + (Time.realtimeSinceStartup - t1).ToString("0.0") + " s");
            }
        }

        IEnumerator FieldRun()
        {
            Report = "running";
            var ed = OzUnitEditor.I; g = ed.Game; w = ed.World;
            bool playing0 = OzUeControls.Playing; float speed0 = OzUeControls.Speed; bool hidden0 = OzUnitEditor.PanelsHidden;
            if (hidden0) OzUnitEditor.SetPanelsHidden(false);
            OzUePlace.Stop(); OzUePlayground.ClearField(); OzUePick.ClearInspect();
            OzUeControls.SetSpeed(1f); OzUeControls.SetPlaying(true);
            Vector3 f = ed.Rts ? ed.Rts.focus : new Vector3(160f, 0f, -100f);
            var map = w.Map; f.x = Mathf.Clamp(f.x, 70f, map.width * 2f - 70f); f.z = Mathf.Clamp(f.z, -(map.height * 2f - 60f), -60f); f.y = 0f;
            float reach = Mathf.Clamp(w.S.Unit("gatling").attack_radius * 0.8f / 16f, 5f, 8.5f);                 // tiles: inside its range, as P3 of the platform check
            OzUeControls.CentreOn(f); yield return Seconds(1f);
            var a = Lead(OzUePlayground.Drop("gatling", Team.Red, f + new Vector3(-reach, 0f, 0f), 1)); var t = Lead(OzUePlayground.Drop("heavy", Team.Blue, f + new Vector3(reach, 0f, 0f), 1));
            yield return Frames(4);
            yield return Fire("on the floor at the floor", a, t, 0, true);
            OzUePlayground.ClearField(); yield return Frames(4);
            var mid = Lead(OzUePlayground.Drop(OzUePlayground.PlatformMid, Team.Neutral, f + new Vector3(-30f, 0f, 0f), 1));
            var high = Lead(OzUePlayground.Drop(OzUePlayground.PlatformHigh, Team.Neutral, f + new Vector3(30f, 0f, 0f), 1));
            yield return Frames(4);
            Check(mid != null && high != null, "P both platforms stand");
            if (mid != null && high != null)
                foreach (var (p, nm) in new[] { (w.PlatformOf(mid.Id), "4.5 m deck"), (w.PlatformOf(high.Id), "9.25 m deck") })
                    foreach (bool down in new[] { false, true })
                    {
                        OzUePlayground.ClearUnits(); yield return Frames(3);
                        var deck = OzUePlatformViews.Point(p, 2.5f, 5f, (float)p.HeightAt(5f)); var floor = OzUePlatformViews.Point(p, 2.5f - reach, 5f, 0f);
                        OzUeControls.CentreOn(deck); yield return Seconds(0.3f);
                        a = Lead(OzUePlayground.Drop("gatling", Team.Red, down ? deck : floor, 1)); t = Lead(OzUePlayground.Drop("heavy", Team.Blue, down ? floor : deck, 1));
                        yield return Frames(4);
                        yield return Fire(down ? "from the " + nm + " down" : "from the floor up at the " + nm, a, t, down ? -1 : 1, true);
                    }
            OzUeControls.SetSpeed(speed0); OzUeControls.SetPlaying(playing0);
            OzUePlace.Stop(); OzUePick.ClearInspect(); OzUePlayground.ClearField();
            if (hidden0) OzUnitEditor.SetPanelsHidden(true);
            yield return Frames(2);
            Done("field", FieldChecks);
        }

        IEnumerator TowerRun()
        {
            Report = "running";
            g = ZGame.I; w = g.World; var me = g.player; var them = me == Team.Red ? Team.Blue : Team.Red;
            w.Bots.Clear();
            var fort = w.Objects.FirstOrDefault(o => o.IsFort && !o.Destroyed && o.Owner == me && g.StaticViews.TryGetValue(o.Id, out var sv) && sv);
            if (fort == null) { Check(false, "T the player's fort stands"); Done("tower", TowerChecks); yield break; }
            var ft = g.StaticViews[fort.Id].transform; var rts = FindFirstObjectByType<RtsCamera>();
            if (rts) { rts.FreeLookOff(); rts.SetHeightImmediate(70f); }
            int mx = w.Map.width * 8, my = w.Map.height * 8, done = 0;
            foreach (var (lx, ly) in TowerMounts.TurretSpots)
            {
                if (done == 2) break;
                done++;
                int pad = TowerMounts.PadForSpot(ft, lx, ly); var hidden = new List<GameObject>();
                foreach (var kv in g.Views)
                    if (kv.Value && kv.Value.OnTower && kv.Value.TowerFort == ft && kv.Value.TowerPad == pad && kv.Value.gameObject.activeSelf) { kv.Value.gameObject.SetActive(false); hidden.Add(kv.Value.gameObject); }
                var a = w.CreateObject("cannon", "gatling", fort.LocX + lx, fort.LocY + ly, fort.Owner); a.DriverType = "grunt";
                var v = g.SpawnUnitView(a); yield return Frames(3);
                Check(v && v.OnTower, "T pad " + pad + ": the gatling is drawn on its tower");
                float bx = mx - a.CenterX, by = my - a.CenterY, ln = Mathf.Max(1f, Mathf.Sqrt(bx * bx + by * by)); int dist = w.S.Unit("gatling").attack_radius - 40;
                OzCheatSpawner.FreeTile(w, (a.CenterX + (int)(bx / ln * dist)) >> 4, (a.CenterY + (int)(by / ln * dist)) >> 4, 2, false, out int tx, out int ty);
                var t = w.CreateObject("vehicle", "heavy", tx * 16, ty * 16, them); g.SpawnUnitView(t); yield return Frames(3);
                var mid = ZGame.PxToWorld((a.CenterX + t.CenterX) / 2, (a.CenterY + t.CenterY) / 2);
                if (rts) rts.focus = new Vector3(mid.x, 0f, mid.z);
                yield return Seconds(0.3f);
                yield return Fire("tower pad " + pad + " down at a tank", a, t, -1, false);
                a.KillAt = w.Time; t.KillAt = w.Time; yield return Frames(3);
                foreach (var h in hidden) if (h) h.SetActive(true);
            }
            Done("tower", TowerChecks);
        }

        void Done(string which, int want)
        {
            var sb = new StringBuilder();
            bool ok = fail == 0 && pass == want;
            sb.Append(Tag).Append(' ').Append(which).Append(": ").Append(ok ? "PASS " : "FAIL ").Append(pass).Append('/').Append(want).AppendLine();
            sb.Append(lines);
            if (pictures.Count > 0) sb.Append("pictures: ").AppendLine(string.Join(", ", pictures));
            Report = sb.ToString();
            if (!string.IsNullOrEmpty(Folder))
            {
                try { Directory.CreateDirectory(Folder); File.WriteAllText(Path.Combine(Folder, "report_" + which + ".txt"), Report.Replace("\r\n", "\n")); }
                catch (System.Exception e) { Debug.LogWarning(Tag + " the report could not be written: " + e.Message); }
            }
            if (ok) Debug.Log(Report); else Debug.LogError(Report);
            Destroy(gameObject);
        }
    }
}
#endif
