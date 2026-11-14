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
    /// shots_over_walls: the counted Play check (Play, the Unit editor open; menu OpenZ/V1.3/Shots Over Walls: Check). A high platform on the free field, then:
    ///  OW each direct weapon on the floor 3.5 m off the side wall at a target on the deck: no round stopped, nothing cut, no burst at a wall, the line went over, it reached its end, no bend;
    ///  OD the same from the deck at the floor;
    ///  OR sniper and heavy from the floor at a unit on the ramp, through its side; OM the same at the medium deck;
    ///  OF the switch off, grunt and heavy: the wall stops them again;
    ///  NF, NC, NE, NA with the switch on, what must not change: each direct weapon on the flat floor, a clear line up the ramp, across the deck, howitzer and MLRS at the deck: nothing stopped, cut or flagged;
    ///  OX, OP, OV, OL, OT, OM direct lines with the real ShotLine (the sim never fires across a platform): floor to floor stays stopped, a shot at the platform stays stopped,
    ///  a miss under the rim lands on the deck, a hit at a floor robot against the wall is never lifted, a rocket fired away from the wall or at its foot is not flagged, a missed rocket at a deck unit is.
    /// Frames go to Folder: up_ and down_ for each weapon with the head in flight, the same at camera heights 35 and 200, and at night.
    /// <see cref="FortLine"/>: the lines at a fort, on a level. <see cref="BeginFight"/>: a level's own fight, on then off: no second walk, every line over at a fort.
    /// </summary>
    public sealed class ShotsOverWallsCheck : MonoBehaviour
    {
        const string Tag = "[OpenZ ShotsOverWalls]";
        public static string Report = "not run", Folder;
        public static bool Running => run;
        static ShotsOverWallsCheck run;
        int pass, fail; bool fightRun, over0 = true;
        readonly StringBuilder lines = new StringBuilder();
        OzUnitEditor ed; ZGame g; ZWorld w; ZWorld.PlatformInfo pl;
        static readonly string[] Direct = { "grunt", "psycho", "sniper", "laser", "pyro", "electrobot", "gatling", "tough", "light", "heavy", "medium", "gun", "missile_cannon" };
        static readonly HashSet<string> Flying = new HashSet<string> { "tough", "light", "heavy", "medium", "gun", "missile_cannon" };
        const float KinkDeg = 25f, RiseM = 2f, LandM = 5f, FaceTiles = 0.03f, FightSeconds = 60f, FlightsEndS = 1.2f;

        public static void Begin()
        {
            if (run) { Debug.LogWarning(Tag + " a run is still going"); return; }
            if (!Application.isPlaying || !OzUnitEditor.UiBuilt || OzUnitEditor.I == null || OzUnitEditor.I.World == null) { Debug.LogWarning(Tag + " enter Play and open the unit editor first"); return; }
            var go = new GameObject("ShotsOverWallsCheck"); DontDestroyOnLoad(go);
            run = go.AddComponent<ShotsOverWallsCheck>();
        }

        public static void BeginFight()
        {
            if (run) { Debug.LogWarning(Tag + " a run is still going"); return; }
            if (!Application.isPlaying || !ZGame.I || ZGame.I.World == null) { Debug.LogWarning(Tag + " enter Play on a level with a fight first"); return; }
            var go = new GameObject("ShotsOverWallsFight"); DontDestroyOnLoad(go);
            run = go.AddComponent<ShotsOverWallsCheck>(); run.fightRun = true;
        }

        void Start() { over0 = ShotLine.OverWalls; StartCoroutine(fightRun ? Fight() : Main()); }
        void OnDestroy() { ShotLine.Seen = null; ShotLine.OverSeen = null; OpenZFxPlayer.ProjTrace = null; ShotLine.OverWalls = over0; if (run == this) run = null; }

        void Check(bool ok, string what) { if (ok) pass++; else fail++; lines.Append(ok ? "PASS  " : "FAIL  ").AppendLine(what); }
        static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }
        static ZObject Lead(UeDrop d) => d.ok && d.made != null && d.made.Count > 0 ? d.made[0] : null;
        static ZObject Heal(ZObject o) { if (o != null) o.MaxHealth = o.Health = 900000000; return o; }
        Vector3 Deck(float across, float along) => OzUePlatformViews.Point(pl, across, along, (float)pl.HeightAt(along));
        Vector3 Floor(float across, float along) => OzUePlatformViews.Point(pl, across, along, 0f);
        static float Mm(Vector3 a, Vector3 b) => Vector3.Distance(a, b) * 1000f;
        void Snap(string pic)
        {
            if (string.IsNullOrEmpty(Folder) || pic == null) return;
            Directory.CreateDirectory(Folder); ScreenCapture.CaptureScreenshot(Path.Combine(Folder, pic + ".png"));
        }

        // ---------------------------------------------------------------------- what one shooter does
        int rounds, stopped, reached, lifts, overLines, flown, finished, landed, hurt, cuts, bursts, overFlights, fired; float kink, rise, shooterY, targetY, overAt;
        ZObject shooter;
        readonly Dictionary<int, List<Vector3>> paths = new Dictionary<int, List<Vector3>>();
        readonly Dictionary<int, float> flightShare = new Dictionary<int, float>();                     // how far through its flight each rocket got: one still in the air at the end is not judged

        void OnRound(Vector3 from, Vector3 to, Vector3 end, bool stop)
        {
            rounds++;
            if (stop && ShotLine.LastOver) { lifts++; reached++; }                                       // a miss lifted onto the deck: no wall, its line went over
            else if (stop) stopped++;
            else reached++;
        }

        void OnOver(Vector3 from, Vector3 end) { overLines++; if (overAt < 0f) overAt = Time.realtimeSinceStartup; }

        void OnProj(int id, FxWeapon wp, float age, float life, Vector3 pos, bool ease)
        {
            if (!paths.TryGetValue(id, out var p)) paths[id] = p = new List<Vector3>();
            if (p.Count == 0 || (pos - p[p.Count - 1]).sqrMagnitude > 0.04f) p.Add(pos);
            flightShare[id] = life > 0f ? age / life : 1f;
        }

        void Update()
        {
            if (w == null || shooter == null) return;
            var ev = w.Events;
            for (int i = 0; i < ev.Count; i++)
            {
                var e = ev[i];
                if (e.type != ZEventType.MissileFired) continue;
                if (e.objId == shooter.Id || (shooter.Minions != null && shooter.Minions.Exists(m => m != null && m.Id == e.objId))) fired++;
            }
        }

        void Score(Vector3 at)
        {
            flown = paths.Count;
            foreach (var kv in paths)
            {
                var p = kv.Value;
                for (int i = 4; i + 1 < p.Count; i++) kink = Mathf.Max(kink, Vector3.Angle(p[i] - p[i - 1], p[i + 1] - p[i]));
                if (p.Count < 2) continue;
                float top = Mathf.Max(p[0].y, p[p.Count - 1].y);
                foreach (var q in p) rise = Mathf.Max(rise, q.y - top);
                if (flightShare[kv.Key] < 0.8f) continue;
                finished++;
                var e = p[p.Count - 1];
                if (Mathf.Sqrt((e.x - at.x) * (e.x - at.x) + (e.z - at.z) * (e.z - at.z)) < LandM) landed++;
            }
        }

        float ViewY(ZObject o) => o != null && g.Views.TryGetValue(o.Id, out var v) && v ? v.transform.position.y : float.NaN;

        IEnumerator Pair(string type, Vector3 from, Vector3 at, float seconds, string pic)
        {
            OzUePlayground.ClearUnits(); yield return Frames(3);
            yield return new WaitForSecondsRealtime(FlightsEndS);                                        // the last pair's rockets land first: they must not count here
            rounds = stopped = reached = lifts = overLines = flown = finished = landed = hurt = cuts = bursts = overFlights = fired = 0; kink = rise = 0f; shooterY = targetY = float.NaN; overAt = -1f; paths.Clear(); flightShare.Clear();
            var a = Lead(OzUePlayground.Drop(type, Team.Red, from, 1)); var t = Lead(OzUePlayground.Drop("heavy", Team.Blue, at, 1));
            yield return Frames(4);
            if (a == null || t == null) { Check(false, type + " set up"); yield break; }
            Heal(a); Heal(t); t.Damage = 0; t.MoveSpeed = 0; a.MoveSpeed = 0; foreach (var m in a.Minions) { Heal(m); m.MoveSpeed = 0; }
            shooter = a;
            w.SendWaypoints(Team.Red, a, new[] { new ZWaypoint { mode = WpMode.Attack, refId = t.Id, x = t.CenterX, y = t.CenterY, playerGiven = true } });
            int hp0 = t.Health, cut0 = ShotLine.Cuts, burst0 = ShotLine.WallBlasts, flight0 = ShotLine.OverFlights; float t0 = Time.realtimeSinceStartup; bool shot = false;
            while (Time.realtimeSinceStartup - t0 < seconds)
            {
                if (pic != null && !shot && overAt > 0f && Time.realtimeSinceStartup - overAt > 0.05f) { shot = true; Snap(pic); }   // the head in flight
                yield return null;
            }
            if (pic != null && !shot) Snap(pic);
            shooter = null; shooterY = ViewY(a); targetY = ViewY(t);
            Score(at);
            hurt = hp0 - t.Health; cuts = ShotLine.Cuts - cut0; bursts = ShotLine.WallBlasts - burst0; overFlights = ShotLine.OverFlights - flight0;
        }

        string Heights => "shooter at " + shooterY.ToString("0.0") + " m, target at " + targetY.ToString("0.0") + " m";
        bool Low(float y) => y < 1f;
        bool OnDeck(float y) => y > (float)pl.DeckM - 1f;

        /// <summary>One weapon, one way, in the situation.</summary>
        void Way(string row, string type, bool set)
        {
            bool flies = Flying.Contains(type);
            bool went = flies ? overFlights > 0 && finished > 0 && landed == finished && kink < KinkDeg && rise < RiseM : type == "pyro" ? overLines > 0 : rounds > 0 && reached == rounds && overLines > 0;
            Check(set && went && stopped == 0 && cuts == 0 && bursts == 0,
                row + " " + type + ", " + Heights + ": " + rounds + " rounds, " + reached + " reached their end, " + stopped + " stopped on a wall, " + lifts + " misses onto the deck, " + overLines + " lines over, "
                + flown + " flown, " + overFlights + " over, " + finished + " finished, " + landed + " of them by the target, " + cuts + " cut, " + bursts + " bursts at a wall, worst turn " + kink.ToString("0.0") + " deg, " + rise.ToString("0.00") + " m over its ends, " + hurt + " HP counted");
        }

        /// <summary>The switch on, a shot that is not the situation: nothing stopped, cut or flagged.</summary>
        void Plain(string row, string type, bool set, bool drew) =>
            Check(set && drew && stopped == 0 && cuts == 0 && bursts == 0 && overLines == 0 && overFlights == 0,
                row + " " + type + ", " + Heights + ": " + rounds + " rounds, " + flown + " flown, " + fired + " fired, " + hurt + " HP, " + stopped + " stopped, " + cuts + " cut, " + bursts + " bursts at a wall, " + overLines + " lines over, " + overFlights + " flights over");

        IEnumerator Stage(string platformId, Vector3 f, System.Action<ZObject> got)
        {
            var o = Lead(OzUePlayground.Drop(platformId, Team.Neutral, f, 1));
            yield return Frames(4);
            if (o != null) { pl = w.PlatformOf(o.Id); OzUeControls.CentreOn(Deck(0f, 5f)); }
            yield return Frames(20);
            got(o);
        }

        IEnumerator Main()
        {
            Report = "running";
            ed = OzUnitEditor.I; g = ed.Game; w = ed.World;
            bool playing0 = OzUeControls.Playing; float speed0 = OzUeControls.Speed;
            OzUePlace.Stop(); OzUePlayground.ClearField(); OzUePick.ClearInspect();
            OzUeControls.SetSpeed(2f); OzUeControls.SetPlaying(true);
            Vector3 f = ed.Rts ? ed.Rts.focus : new Vector3(160, 0, -100); f.y = 0f;
            var map = w.Map; if (map != null) { f.x = Mathf.Clamp(f.x, 70f, map.width * 2f - 70f); f.z = Mathf.Clamp(f.z, -(map.height * 2f - 60f), -60f); }
            ZObject high = null;
            yield return Stage(OzUePlayground.PlatformHigh, f, o => high = o);
            if (high == null) { Check(false, "the high platform drops on the free field"); yield return Finish(playing0, speed0); yield break; }
            ShotLine.OverWalls = true;
            ShotLine.Seen = OnRound; ShotLine.OverSeen = OnOver; OpenZFxPlayer.ProjTrace = OnProj;
            const float along = 5f;
            Vector3 foot = Floor(-1.75f, along), deck = Deck(2.5f, along);                               // 3.5 m off the wall, 5 m in from it: 8.5 m apart, inside every radius
            foreach (var type in Direct)
            {
                bool flies = Flying.Contains(type);
                yield return Pair(type, foot, deck, flies ? 5f : 4.5f, "up_" + type);
                Way("OW", type, Low(shooterY) && OnDeck(targetY));
                yield return Pair(type, deck, foot, flies ? 5f : 4.5f, "down_" + type);
                Way("OD", type, OnDeck(shooterY) && Low(targetY));
            }
            foreach (var type in new[] { "sniper", "heavy" })
            {
                yield return Pair(type, Floor(-1.75f, 12f), Deck(2.5f, 12f), 4.5f, "ramp_" + type);          // the ramp's upper half, through its side
                Way("OR", type, Low(shooterY) && targetY > 1.5f);
            }
            ed.Rts.SetHeightImmediate(35f); yield return Frames(3);
            yield return Pair("grunt", foot, deck, 2.5f, "cam035_grunt"); yield return Pair("heavy", foot, deck, 3f, "cam035_heavy");
            ed.Rts.SetHeightImmediate(200f); yield return Frames(3);
            yield return Pair("grunt", foot, deck, 2.5f, "cam200_grunt"); yield return Pair("heavy", foot, deck, 3f, "cam200_heavy");
            ed.Rts.SetHeightImmediate(70f); yield return Frames(3);
            yield return Night(foot, deck);
            ShotLine.OverWalls = false;                                                                  // the switch: today's walls are back, same stage
            foreach (var type in new[] { "grunt", "heavy" })
            {
                bool flies = Flying.Contains(type);
                yield return Pair(type, foot, deck, flies ? 5f : 4.5f, "off_" + type);
                Check(Low(shooterY) && OnDeck(targetY) && overLines == 0 && overFlights == 0 && (flies ? cuts > 0 && bursts > 0 : stopped > 0), "OF " + type + " with the switch off, " + Heights + ": " + rounds + " rounds, " + stopped + " stopped, " + cuts + " cut, " + bursts + " bursts at a wall, " + overLines + " lines over, " + overFlights + " flights over");
            }
            ShotLine.OverWalls = true;
            Vector3 flatA = Floor(-12f, along), flatB = Floor(-7.75f, along), pastFoot = Floor(5f, 26.5f), onRamp = Deck(5f, 19f), deckFar = Deck(7.5f, along);
            foreach (var type in Direct)
            {
                yield return Pair(type, flatB, flatA, Flying.Contains(type) ? 4f : 3.5f, null);          // away from the platform: a flat miss runs on to the walk surface, its wall would stop it
                Plain("NF", type, Low(shooterY) && Low(targetY), type == "pyro" ? hurt > 0 : rounds > 0 || flown > 0);
            }
            foreach (var type in new[] { "sniper", "heavy" })
            {
                yield return Pair(type, pastFoot, onRamp, 4f, null);
                Plain("NC", type, targetY > 1.5f, rounds > 0 || flown > 0);
                yield return Pair(type, deck, deckFar, 4f, null);
                Plain("NE", type, OnDeck(shooterY) && OnDeck(targetY), rounds > 0 || flown > 0);
            }
            foreach (var type in new[] { "howitzer", "missile_launcher" })
            {
                yield return Pair(type, Floor(-6f, along), deck, 7f, "arc_" + type);
                Plain("NA", type, Low(shooterY) && OnDeck(targetY), fired > 0 || hurt > 0);
            }
            yield return Lines(high, foot, deck, along);
            OzUePlayground.ClearField(); yield return Frames(4);
            ZObject mid = null;
            yield return Stage(OzUePlayground.PlatformMid, f, o => mid = o);
            if (mid == null) Check(false, "the medium platform drops on the free field");
            else
            {
                foot = Floor(-1.75f, along); deck = Deck(2.5f, along);
                foreach (var type in new[] { "sniper", "heavy" })
                {
                    yield return Pair(type, foot, deck, 4.5f, "mid_" + type);
                    Way("OM", type, Low(shooterY) && OnDeck(targetY));
                }
            }
            yield return Finish(playing0, speed0);
        }

        /// <summary>Night in the unit editor, which has no clock: the same pairs under the night grade, the light put back after.</summary>
        IEnumerator Night(Vector3 foot, Vector3 deck)
        {
            var sun = RenderSettings.sun; float t0 = NightGrade.TimeOfDay, i0 = sun ? sun.intensity : 0f, s0 = sun ? sun.shadowStrength : 0f;
            Color a0 = RenderSettings.ambientSkyColor, a1 = RenderSettings.ambientEquatorColor, a2 = RenderSettings.ambientGroundColor;
            NightGrade.Apply(sun, 0.65f); yield return Frames(3);
            yield return Pair("grunt", foot, deck, 2.5f, "night_grunt"); yield return Pair("heavy", foot, deck, 3f, "night_heavy");
            NightGrade.Apply(sun, t0);
            if (sun) { sun.intensity = i0; sun.shadowStrength = s0; }
            RenderSettings.ambientSkyColor = a0; RenderSettings.ambientEquatorColor = a1; RenderSettings.ambientGroundColor = a2;
            yield return Frames(3);
        }

        /// <summary>The lines the sim never fires or that take one exact end: the real ShotLine on the real platform, on and off.</summary>
        IEnumerator Lines(ZObject high, Vector3 foot, Vector3 deck, float along)
        {
            OzUePlayground.ClearUnits(); yield return Frames(3);
            var low = Lead(OzUePlayground.Drop("grunt", Team.Red, foot, 1)); var top = Lead(OzUePlayground.Drop("grunt", Team.Blue, deck, 1));
            yield return Frames(4);
            if (low == null || top == null) { Check(false, "the direct lines set up"); yield break; }
            var up = Vector3.up * 1.3f;
            // OX floor to floor across a corner of the platform: as off, stopped, not flagged
            Vector3 a = Floor(-1.5f, 3f) + up, b = Floor(3f, -1.5f) + Vector3.up;
            bool s1 = ShotLine.Stop(a, b, low, out var at1), f1 = ShotLine.LastOver;
            ShotLine.OverWalls = false; bool s0 = ShotLine.Stop(a, b, low, out var at0); ShotLine.OverWalls = true;
            Check(s1 && s0 && !f1 && Mm(at1, at0) < 1f, "OX a round floor to floor across the platform: stopped on and off, " + Mm(at1, at0).ToString("0.00") + " mm apart, flagged " + f1);
            float t1 = 1f, t0 = 1f; Vector3 e1 = b, e0 = b;
            bool c1 = ShotLine.Cut(a, ref e1, ref t1, FxWeapon.HeavyShell, low), fc = ShotLine.LastOver;
            ShotLine.OverWalls = false; bool c0 = ShotLine.Cut(a, ref e0, ref t0, FxWeapon.HeavyShell, low); ShotLine.OverWalls = true;
            Check(c1 && c0 && !fc && Mm(e1, e0) < 1f, "OX a shell floor to floor across the platform: cut on and off, " + Mm(e1, e0).ToString("0.00") + " mm apart, flagged " + fc);
            // OP a shot at the platform itself: stopped on its face, not flagged
            var aim = Deck(2.5f, along) + Vector3.up * 0.5f;
            bool sp = ShotLine.Stop(foot + up, aim, high, out var atP), fp = ShotLine.LastOver; pl.Local(atP.x * 8.0, -atP.z * 8.0, out double acP, out _);
            Check(sp && !fp && System.Math.Abs(acP) <= FaceTiles, "OP a round at the platform: stopped " + sp + ", " + acP.ToString("0.000") + " tiles off its face, flagged " + fp);
            float tp = 1f; var endP = aim; bool cp = ShotLine.Cut(foot + up, ref endP, ref tp, FxWeapon.HeavyShell, high), fq = ShotLine.LastOver;
            Check(cp && !fq, "OP a shell at the platform: cut " + cp + ", flagged " + fq);
            // OV a miss at the deck unit aimed at the outer metre of the rim: on the deck, on rock's sparks, not in the wall
            var rim = OzUePlatformViews.Point(pl, 0.25f, along, 0.05f);
            bool sv = ShotLine.Stop(foot + up, rim, top, out var atV), fv = ShotLine.LastOver;
            Check(sv && fv && Mathf.Abs(atV.y - (float)pl.DeckM) < 0.05f && ShotLine.HardAt(atV), "OV a miss under the rim: ends " + atV.y.ToString("0.00") + " m up (deck " + pl.DeckM.ToString("0.00") + "), flagged " + fv + ", hard " + ShotLine.HardAt(atV));
            // OL a deck man's hit on a floor robot drawn into the wall: the round ends on him, no lift
            var knee = OzUePlatformViews.Point(pl, 0.2f, along, 1.2f); int lifts0 = ShotLine.OverLifts;
            bool sl = ShotLine.Stop(deck + up, knee, low, out var atL), fl = ShotLine.LastOver;
            Check(!sl && fl && Mm(atL, knee) < 1f && ShotLine.OverLifts == lifts0, "OL a hit on a floor robot against the wall: stopped " + sl + ", flagged " + fl + ", " + Mm(atL, knee).ToString("0.00") + " mm off him, lifted " + (ShotLine.OverLifts - lifts0));
            // OT rockets that must stay plain: a muzzle in the wall firing away, a floor target at the wall's foot; one that must not: a missed rocket at the deck unit
            Vector3 inWall = OzUePlatformViews.Point(pl, 0.2f, along, 1.3f), away = Floor(-6f, along) + Vector3.up * 0.25f; float ta = 1f;
            ShotLine.Cut(inWall, ref away, ref ta, FxWeapon.Bazooka, low);
            bool oa = ShotLine.FlightOver(inWall, away, 0f);
            Vector3 lowM = Floor(-6f, along) + up, wallFoot = OzUePlatformViews.Point(pl, 0.25f, along, 0.25f); float tb = 1f;
            ShotLine.Cut(lowM, ref wallFoot, ref tb, FxWeapon.Bazooka, low);
            bool ob = ShotLine.FlightOver(lowM, wallFoot, 0f);
            var missed = Deck(4f, along) + Vector3.up * 0.25f; float tc = 1f;
            bool cm = ShotLine.Cut(foot + up, ref missed, ref tc, FxWeapon.Bazooka, top);
            bool om = ShotLine.FlightOver(foot + up, missed, 0f);
            Check(!oa && !ob && !cm && om, "OT flights: fired away out of the wall flagged " + oa + ", at the wall's foot flagged " + ob + ", a missed rocket at a deck unit cut " + cm + ", flagged " + om);
        }

        IEnumerator Finish(bool playing0, float speed0)
        {
            ShotLine.Seen = null; ShotLine.OverSeen = null; OpenZFxPlayer.ProjTrace = null; ShotLine.OverWalls = over0;
            OzUePlace.Stop(); OzUePick.ClearInspect(); OzUePlayground.ClearField();
            OzUeControls.SetSpeed(speed0); OzUeControls.SetPlaying(playing0);
            yield return Frames(2);
            Write("report.txt");
            Destroy(gameObject);
        }

        void Write(string file)
        {
            var sb = new StringBuilder();
            sb.Append(Tag).Append(' ').Append(fail == 0 ? "PASS" : "FAIL").Append(": ").Append(pass).Append(" of ").Append(pass + fail).Append(" checks").AppendLine();
            sb.Append(lines);
            Report = sb.ToString();
            if (!string.IsNullOrEmpty(Folder))
            {
                try { Directory.CreateDirectory(Folder); File.WriteAllText(Path.Combine(Folder, file), Report.Replace("\r\n", "\n")); }
                catch (System.Exception e) { Debug.LogWarning(Tag + " the report could not be written: " + e.Message); }
            }
            if (fail == 0) Debug.Log(Report); else Debug.LogError(Report);
        }

        /// <summary>The first live fort of the level playing, its real mesh. R: a roof man at the ground round his fort: some lines run under his parapet and are flagged, none is stopped.
        /// From the floor at his spot: flagged. A clear line over the fort, a line through it from outside at the ground, a shot at the fort, a sky shot at a point high over its
        /// roof: not flagged. T, after: the fort's top cannon is shot off (TestToss, as in a fight; it cannot be undone in this Play) and the same lines are flagged again,
        /// the shot at the fort still not. Run it last on a level.</summary>
        public static string FortLine()
        {
            var g = ZGame.I; FortGunView v = null;
            if (g && g.World != null)
                foreach (var o in g.World.Objects)
                    if (o.IsFort && !o.Destroyed && g.StaticViews.TryGetValue(o.Id, out var go) && go && (v = go.GetComponent<FortGunView>()) && v.Bound && !v.Tossed) break; else v = null;
            string text;
            if (!v) text = Tag + " FAIL: 0 of 2: a level with a live fort and its top cannon must be playing";
            else
            {
                var ft = v.transform; int shots = 0, flagged = 0, stopped = 0, fromFloor = 0, fromFloorOver = 0;
                void Count()
                {
                    shots = flagged = stopped = fromFloor = fromFloorOver = 0;
                    for (int s = 0; s < ZObject.FortDefenderDeckNodes; s++)
                    {
                        float sx = (float)ZObject.FortDefenderDeck[s * 2], sz = (float)ZObject.FortDefenderDeck[s * 2 + 1];
                        var from = ft.TransformPoint(new Vector3(sx, 7.8f, sz));
                        for (int a = 0; a < 72; a++)
                            for (float r = 3f; r <= 15f; r += 1f)
                            {
                                float tx = sx + Mathf.Cos(a * 5f * Mathf.Deg2Rad) * r, tz = sz + Mathf.Sin(a * 5f * Mathf.Deg2Rad) * r;
                                if ((Mathf.Abs(tx) < 10.35f && Mathf.Abs(tz) < 10.15f) || !ZObject.FortDefenderLineClear(sx, sz, tx, tz)) continue;
                                var to = ft.TransformPoint(new Vector3(tx, 0f, tz)); to.y = g.GroundY(to.x, to.z);
                                shots++;
                                if (FortGunView.CrossesOwn(from, to, 0f, null)) flagged++;
                                if (ShotLine.Stop(from, to, null, out var at)) { var l = ft.InverseTransformPoint(at); if (Mathf.Abs(l.x) < 10.35f && Mathf.Abs(l.z) < 10.15f) stopped++; }
                                var low = ft.TransformPoint(new Vector3(tx * 1.6f, 1.3f, tz * 1.6f)); low.y = g.GroundY(low.x, low.z) + 1.3f;
                                fromFloor++; if (FortGunView.CrossesOwn(low, from, 0f, null)) fromFloorOver++;
                            }
                    }
                }
                Count();
                var hi2 = ft.TransformPoint(new Vector3(0f, 7.8f, 0f));
                bool clear = FortGunView.CrossesOwn(ft.TransformPoint(new Vector3(-6f, 14f, -6f)), ft.TransformPoint(new Vector3(6f, 14f, 5f)), 0f, null);   // both ends over the fort, the line above its mesh
                bool outside = ShotLine.Stop(ft.TransformPoint(new Vector3(-25f, 1.5f, 0f)), ft.TransformPoint(new Vector3(25f, 1.5f, 0f)), null, out _), outOver = ShotLine.LastOver;
                bool atFort = FortGunView.CrossesOwn(ft.TransformPoint(new Vector3(-25f, 1.5f, 0f)), hi2, 0f, v.Fort);
                var sky = ft.TransformPoint(new Vector3(-2f, 24f, 1f));                                     // a dragon high over the roof: no target, nothing stands there
                bool skyOver = FortGunView.CrossesOwn(ft.TransformPoint(new Vector3(-12.5f, 1.3f, 0f)), sky, 0f, null);
                bool ok = shots > 0 && flagged > 0 && stopped == 0 && fromFloorOver > 0 && !clear && outside && !outOver && !atFort && !skyOver;
                string r1 = (ok ? "PASS  " : "FAIL  ") + "R a roof man at the ground: " + shots + " lines, " + flagged + " under the mesh and flagged, " + stopped + " stopped on his own fort; from the floor at him " + fromFloorOver + " of " + fromFloor + " flagged; a clear line flagged " + clear + "; through it from outside stopped " + outside + ", flagged " + outOver + "; a shot at the fort flagged " + atFort + "; a sky shot over the roof flagged " + skyOver;
                int flagged0 = flagged, fromFloorOver0 = fromFloorOver;
                v.TestToss();
                Count();
                bool atFortT = FortGunView.CrossesOwn(ft.TransformPoint(new Vector3(-25f, 1.5f, 0f)), hi2, 0f, v.Fort);
                bool okT = v.Tossed && flagged > 0 && fromFloorOver > 0 && !atFortT;
                string r2 = (okT ? "PASS  " : "FAIL  ") + "T the top cannon shot off, tossed " + v.Tossed + ": the same " + shots + " lines, " + flagged + " flagged (before " + flagged0 + "); from the floor at him " + fromFloorOver + " of " + fromFloor + " flagged (before " + fromFloorOver0 + "); a shot at the fort flagged " + atFortT;
                int n = (ok ? 1 : 0) + (okT ? 1 : 0);
                text = Tag + " " + (n == 2 ? "PASS" : "FAIL") + ": " + n + " of 2\n" + r1 + "\n" + r2;
            }
            if (!string.IsNullOrEmpty(Folder))
            {
                try { Directory.CreateDirectory(Folder); File.WriteAllText(Path.Combine(Folder, "fort_line.txt"), text + "\n"); }
                catch (System.Exception e) { Debug.LogWarning(Tag + " the report could not be written: " + e.Message); }
            }
            if (text.StartsWith(Tag + " PASS")) Debug.Log(text); else Debug.LogError(text);
            return text;
        }

        int awayFromFort;
        void OnFightOver(Vector3 from, Vector3 end) { if (!OnAFort(from) && !OnAFort(end)) awayFromFort++; }

        /// <summary>A point on a live fort, 5 m up and over its box (FortGunView.Holds): its top cannon there or shot off in the fight, which StandsOnFort leaves out.</summary>
        static bool OnAFort(Vector3 p)
        {
            var g = ZGame.I; if (!g || g.World == null) return false;
            foreach (var o in g.World.Objects)
            {
                if (!o.IsFort || o.Destroyed || !g.StaticViews.TryGetValue(o.Id, out var go) || !go) continue;
                var l = go.transform.InverseTransformPoint(p);
                if (l.y >= 5f && Mathf.Abs(l.x) <= 10.05f + 1.5f && Mathf.Abs(l.z) <= 9.85f + 1.5f) return true;
            }
            return false;
        }

        /// <summary>A level's own fight, on then off. On: no second walk (a level has no platform), every line over has an end on a fort. Off: nothing goes over.</summary>
        IEnumerator Fight()
        {
            Report = "running";
            ShotLine.OverWalls = true; ShotLine.OverSeen = OnFightOver;
            int walks0 = ShotLine.SecondWalks, over0n = ShotLine.OverLines, stop0 = ShotLine.Stopped, cut0 = ShotLine.Cuts;
            yield return new WaitForSecondsRealtime(FightSeconds);
            int walks = ShotLine.SecondWalks - walks0, over = ShotLine.OverLines - over0n, stopOn = ShotLine.Stopped - stop0, cutOn = ShotLine.Cuts - cut0;
            Check(walks == 0 && awayFromFort == 0, "FT switch on, " + FightSeconds + " s: " + walks + " second walks, " + over + " lines over, " + awayFromFort + " of them away from a fort, " + stopOn + " stopped, " + cutOn + " cut");
            ShotLine.OverWalls = false; ShotLine.OverSeen = null;
            int over1 = ShotLine.OverLines, stop1 = ShotLine.Stopped, cut1 = ShotLine.Cuts;
            yield return new WaitForSecondsRealtime(FightSeconds);
            Check(ShotLine.OverLines == over1, "FT switch off, " + FightSeconds + " s: " + (ShotLine.OverLines - over1) + " lines over, " + (ShotLine.Stopped - stop1) + " stopped, " + (ShotLine.Cuts - cut1) + " cut");
            ShotLine.OverWalls = over0;
            Write("fight_" + (ZGame.I ? ZGame.I.level : "level") + ".txt");
            Destroy(gameObject);
        }
    }
}
#endif
