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
    /// bullets_straight: the counted Play check (Play, the Unit editor open; menu OpenZ/V1.3/Unit Editor/4). A high platform on the free field, then for each weapon:
    ///  W on the floor 3.5 m off the side wall, target on the deck 8.5 m away: every round ends on the drawn wall's face, on its line, none inside the body; rockets and shells are cut there, burst there, no kink;
    ///  F on the flat floor beside the platform, 8.5 m apart, firing away from it: nothing stopped, nothing cut, no burst at a wall;
    ///  C on the floor past the ramp's foot, target on the ramp 15 m away: a clear line over the body, nothing stopped, the rounds reach it;
    ///  E on the deck, target on the deck 10 m away: nothing stopped;
    ///  D from the deck at a target on the floor: the line ends on the deck or its kerb, on its line, none inside the body;
    ///  A arcing fire at the deck: it fires, never cut, the MLRS's path smooth (a howitzer's shell is not on the rocket path: its arc is read by eye).
    /// <see cref="FortLine"/> is the one line with a fort (the unit editor has none): on a level, a roof man's lines from every deck spot at the ground round his fort.
    /// Places are in the platform's tiles (OzUePlatformViews.Point: across 0 .. 10, 2 m a tile, under 0 = outside the wall), the same as OzUePlatformCheck. Report and pictures: Folder.
    /// </summary>
    public sealed class ShotLineCheck : MonoBehaviour
    {
        const string Tag = "[OpenZ ShotLineCheck]";
        public static string Report = "not run", Folder;
        public static bool Running => run;
        static ShotLineCheck run;
        int pass, fail;
        readonly StringBuilder lines = new StringBuilder();
        OzUnitEditor ed; ZGame g; ZWorld w; ZWorld.PlatformInfo hi; bool over0 = true;
        static readonly string[] Direct = { "grunt", "psycho", "sniper", "laser", "pyro", "electrobot", "gatling", "tough", "light", "heavy", "medium", "gun", "missile_cannon" };
        static readonly HashSet<string> Flying = new HashSet<string> { "tough", "light", "heavy", "medium", "gun", "missile_cannon" };
        const float KinkDeg = 25f, FaceTiles = 0.03f, DeepTiles = 0.15f;

        public static void Begin()
        {
            if (run) { Debug.LogWarning(Tag + " a run is still going"); return; }
            if (!Application.isPlaying || !OzUnitEditor.UiBuilt || OzUnitEditor.I == null || OzUnitEditor.I.World == null) { Debug.LogWarning(Tag + " enter Play and open the unit editor first"); return; }
            var go = new GameObject("ShotLineCheck"); DontDestroyOnLoad(go);
            run = go.AddComponent<ShotLineCheck>();
        }

        /// <summary>A fort never stops a shooter on it (FortGunView.MeshTopAt). The first live fort of the level playing, its real mesh: from each of the sim's deck spots, muzzle 7.8 m, at the ground
        /// up to 15 m off, every line the sim lets him fire (ZObject.FortDefenderLineClear), the stops on that fort counted. One line through the fort from outside proves its mesh stops a round.</summary>
        public static string FortLine()
        {
            var g = ZGame.I; FortGunView v = null;
            if (g && g.World != null)
                foreach (var o in g.World.Objects)
                    if (o.IsFort && !o.Destroyed && g.StaticViews.TryGetValue(o.Id, out var go) && go && (v = go.GetComponent<FortGunView>()) && v.Bound) break; else v = null;
            string text;
            if (!v) text = Tag + " FAIL: 0 of 1: a level with a live fort must be playing";
            else
            {
                var ft = v.transform; int shots = 0, own = 0;
                bool control = ShotLine.Stop(ft.TransformPoint(new Vector3(-25f, 1.5f, 0f)), ft.TransformPoint(new Vector3(25f, 1.5f, 0f)), null, out _);
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
                            if (!ShotLine.Stop(from, to, null, out var at)) continue;
                            var l = ft.InverseTransformPoint(at);
                            if (Mathf.Abs(l.x) < 10.35f && Mathf.Abs(l.z) < 10.15f) own++;
                        }
                }
                bool ok = control && shots > 0 && own == 0;
                text = Tag + " " + (ok ? "PASS: 1 of 1" : "FAIL: 0 of 1") + ": R a roof man at the ground round his fort: " + shots + " lines, " + own + " stopped on his own fort; a line through it from outside " + (control ? "stopped" : "NOT stopped");
            }
            if (!string.IsNullOrEmpty(Folder))
            {
                try { Directory.CreateDirectory(Folder); File.WriteAllText(Path.Combine(Folder, "fort_line.txt"), text + "\n"); }
                catch (System.Exception e) { Debug.LogWarning(Tag + " the report could not be written: " + e.Message); }
            }
            if (text.StartsWith(Tag + " PASS")) Debug.Log(text); else Debug.LogError(text);
            return text;
        }

        void Start() { StartCoroutine(Main()); }
        void OnDestroy() { ShotLine.Seen = null; ShotLine.CutSeen = null; OpenZFxPlayer.ProjTrace = null; ShotLine.OverWalls = over0; if (run == this) run = null; }

        void Check(bool ok, string what) { if (ok) pass++; else fail++; lines.Append(ok ? "PASS  " : "FAIL  ").AppendLine(what); }
        static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }
        static ZObject Lead(UeDrop d) => d.ok && d.made != null && d.made.Count > 0 ? d.made[0] : null;
        static ZObject Heal(ZObject o) { if (o != null) o.MaxHealth = o.Health = 900000000; return o; }
        Vector3 Deck(float across, float along) => OzUePlatformViews.Point(hi, across, along, (float)hi.HeightAt(along));
        Vector3 Floor(float across, float along) => OzUePlatformViews.Point(hi, across, along, 0f);

        /// <summary>Well inside the drawn body, not just on its skin.</summary>
        bool Deep(Vector3 p)
        {
            hi.Local(p.x * 8.0, -p.z * 8.0, out double a, out double l);
            return ShotLine.InBody(hi, p) && a > DeepTiles && a < ZWorld.PlatformWideTiles - DeepTiles && l > DeepTiles && l < hi.Rows - DeepTiles;
        }

        /// <summary>On the side wall's outer face (across 0), the wall the W shooters face.</summary>
        bool OnFace(Vector3 p)
        {
            hi.Local(p.x * 8.0, -p.z * 8.0, out double a, out double l);
            return System.Math.Abs(a) <= FaceTiles && p.y <= (float)hi.HeightAt(l) + OzUePlatformView.KerbM + 0.05f;
        }

        // ---------------------------------------------------------------------- what one shooter does
        int rounds, stopped, offLine, through, offFace, flown, lost, cuts, bursts, fired; float kink, rise, shooterY, targetY;
        ZObject shooter;
        readonly Dictionary<int, List<Vector3>> paths = new Dictionary<int, List<Vector3>>();

        void OnRound(Vector3 from, Vector3 to, Vector3 end, bool stop)
        {
            rounds++; if (stop) { stopped++; if (!OnFace(end)) offFace++; }
            var d = to - from; float m = d.magnitude; if (m < 1e-3f) return;
            if (Vector3.Cross(end - from, d / m).magnitude > 0.02f) offLine++;
            var e = end - from; float len = e.magnitude;                                                 // what is drawn never runs through the drawn body
            for (float s = 0.6f; s < len - 0.1f; s += 0.3f) if (Deep(from + e * (s / len))) { through++; break; }
        }

        void OnCut(Vector3 at) { if (!OnFace(at)) offFace++; }

        void OnProj(int id, FxWeapon wp, float age, float life, Vector3 pos, bool ease)
        {
            if (!paths.TryGetValue(id, out var p)) paths[id] = p = new List<Vector3>();
            if (p.Count == 0 || (pos - p[p.Count - 1]).sqrMagnitude > 0.04f) p.Add(pos);
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

        void Score()
        {
            flown = paths.Count;
            foreach (var p in paths.Values)
            {
                for (int i = 4; i + 1 < p.Count; i++) kink = Mathf.Max(kink, Vector3.Angle(p[i] - p[i - 1], p[i + 1] - p[i]));
                if (p.Count < 2) continue;
                float top = Mathf.Max(p[0].y, p[p.Count - 1].y);
                foreach (var q in p) { rise = Mathf.Max(rise, q.y - top); if (Deep(q)) { through++; break; } }
            }
        }

        float ViewY(ZObject o) => o != null && g.Views.TryGetValue(o.Id, out var v) && v ? v.transform.position.y : float.NaN;

        IEnumerator Pair(string type, Vector3 from, Vector3 at, float seconds, string pic)
        {
            OzUePlayground.ClearUnits(); yield return Frames(3);
            rounds = stopped = offLine = through = offFace = flown = lost = cuts = bursts = fired = 0; kink = rise = 0f; shooterY = targetY = float.NaN; paths.Clear();
            var a = Lead(OzUePlayground.Drop(type, Team.Red, from, 1)); var t = Lead(OzUePlayground.Drop("heavy", Team.Blue, at, 1));
            yield return Frames(4);
            if (a == null || t == null) { Check(false, type + " set up"); yield break; }
            Heal(a); Heal(t); t.Damage = 0; t.MoveSpeed = 0; a.MoveSpeed = 0; foreach (var m in a.Minions) { Heal(m); m.MoveSpeed = 0; }
            shooter = a;
            w.SendWaypoints(Team.Red, a, new[] { new ZWaypoint { mode = WpMode.Attack, refId = t.Id, x = t.CenterX, y = t.CenterY, playerGiven = true } });
            int hp0 = t.Health, cut0 = ShotLine.Cuts, burst0 = ShotLine.WallBlasts; float t0 = Time.realtimeSinceStartup; bool shot = false;
            while (Time.realtimeSinceStartup - t0 < seconds)
            {
                if (pic != null && !shot && rounds + paths.Count + fired > 0 && Time.realtimeSinceStartup - t0 > 1.2f)
                {
                    shot = true;
                    if (!string.IsNullOrEmpty(Folder)) { Directory.CreateDirectory(Folder); ScreenCapture.CaptureScreenshot(Path.Combine(Folder, pic + ".png")); }
                }
                yield return null;
            }
            shooter = null; shooterY = ViewY(a); targetY = ViewY(t);
            Score();
            lost = hp0 - t.Health; cuts = ShotLine.Cuts - cut0; bursts = ShotLine.WallBlasts - burst0;
        }

        string Heights => "shooter at " + shooterY.ToString("0.0") + " m, target at " + targetY.ToString("0.0") + " m";
        bool Low(float y) => y < 1f;
        bool OnDeck(float y) => y > (float)hi.DeckM - 1f;

        IEnumerator Main()
        {
            Report = "running";
            over0 = ShotLine.OverWalls; ShotLine.OverWalls = false;                                       // shots_over_walls: these lines count the walls that stop a shot, the switch's off path
            ed = OzUnitEditor.I; g = ed.Game; w = ed.World;
            bool playing0 = OzUeControls.Playing; float speed0 = OzUeControls.Speed;
            OzUePlace.Stop(); OzUePlayground.ClearField(); OzUePick.ClearInspect();
            OzUeControls.SetSpeed(2f); OzUeControls.SetPlaying(true);
            Vector3 f = ed.Rts ? ed.Rts.focus : new Vector3(160, 0, -100); f.y = 0f;
            var map = w.Map; if (map != null) { f.x = Mathf.Clamp(f.x, 70f, map.width * 2f - 70f); f.z = Mathf.Clamp(f.z, -(map.height * 2f - 60f), -60f); }
            var high = Lead(OzUePlayground.Drop(OzUePlayground.PlatformHigh, Team.Neutral, f, 1));
            yield return Frames(4);
            if (high == null) { Check(false, "the high platform drops on the free field"); yield return Finish(playing0, speed0); yield break; }
            hi = w.PlatformOf(high.Id);
            OzUeControls.CentreOn(Deck(0f, 5f));
            yield return Frames(20);
            ShotLine.Seen = OnRound; ShotLine.CutSeen = OnCut; OpenZFxPlayer.ProjTrace = OnProj;
            const float along = 5f;
            Vector3 foot = Floor(-1.75f, along), deck = Deck(2.5f, along), deckFar = Deck(7.5f, along);   // 3.5 m off the wall; 5 m in from it: 8.5 m apart, inside every radius (120 px = 15 m is the least)
            Vector3 flatA = Floor(-12f, along), flatB = Floor(-7.75f, along);                             // the same 8.5 m on the open floor
            Vector3 pastFoot = Floor(5f, 26.5f), onRamp = Deck(5f, 19f);                                  // 15 m apart, the ramp's lower half: it blocks no sight (ZWorld.PlatformSightTiles)

            foreach (var type in Direct)
            {
                bool flies = Flying.Contains(type);
                yield return Pair(type, foot, deck, flies ? 5f : 4.5f, type == "grunt" || type == "tough" || type == "heavy" ? "wall_" + type : null);
                bool set = Low(shooterY) && OnDeck(targetY);
                if (flies) Check(set && cuts > 0 && bursts > 0 && offFace == 0 && through == 0 && kink < KinkDeg && rise < 2f, "W " + type + " at the wall, " + Heights + ": " + cuts + " cut, " + bursts + " bursts where they stopped, " + offFace + " off the wall's face, " + through + " inside the body, worst turn " + kink.ToString("0.0") + " deg, " + rise.ToString("0.00") + " m over its ends, " + lost + " HP counted");
                else Check(set && rounds > 0 && stopped > 0 && offFace == 0 && offLine == 0 && through == 0, "W " + type + " at the wall, " + Heights + ": " + rounds + " rounds, " + stopped + " stopped, " + offFace + " off the wall's face, " + offLine + " off their line, " + through + " inside the body, " + lost + " HP counted");
                yield return Pair(type, flatB, flatA, flies ? 4f : 3.5f, null);                          // away from the platform: a flat miss runs on to the walk surface, its wall would stop it
                bool drew = type == "pyro" ? lost > 0 : rounds > 0 || flown > 0;                         // a flame on flat ground has no round to count: its burn on the target is the proof it reached
                Check(drew && stopped == 0 && cuts == 0 && bursts == 0, "F " + type + " on the flat floor: " + rounds + " rounds, " + flown + " flown, " + lost + " HP, " + stopped + " stopped, " + cuts + " cut, " + bursts + " bursts at a wall");
            }
            foreach (var type in new[] { "sniper", "heavy" })
            {
                yield return Pair(type, pastFoot, onRamp, 4f, null);
                Check(targetY > 1.5f && stopped == 0 && cuts == 0 && through == 0 && (rounds > 0 || flown > 0), "C " + type + " on a clear line up the ramp, " + Heights + ": " + rounds + " rounds, " + flown + " flown, " + stopped + " stopped, " + cuts + " cut, " + through + " inside the body");
                yield return Pair(type, deck, deckFar, 4f, null);
                Check(OnDeck(shooterY) && OnDeck(targetY) && stopped == 0 && cuts == 0 && (rounds > 0 || flown > 0), "E " + type + " across the deck, " + Heights + ": " + rounds + " rounds, " + flown + " flown, " + stopped + " stopped, " + cuts + " cut");
                yield return Pair(type, deck, foot, 4f, null);
                Check(OnDeck(shooterY) && Low(targetY) && stopped + cuts > 0 && offLine == 0 && through == 0, "D " + type + " from the deck at the floor, " + Heights + ": " + rounds + " rounds, " + flown + " flown, " + stopped + " stopped, " + cuts + " cut, " + offLine + " off their line, " + through + " inside the body");
            }
            foreach (var type in new[] { "howitzer", "missile_launcher" })
            {
                bool mlrs = type == "missile_launcher";
                yield return Pair(type, Floor(-6f, along), deck, 7f, "arc_" + type);
                Check((fired > 0 || lost > 0) && cuts == 0 && (!mlrs || (flown > 0 && kink < KinkDeg)), "A " + type + " at the deck: " + fired + " fired, " + lost + " HP, " + cuts + " cut, " + (mlrs ? "worst turn " + kink.ToString("0.0") + " deg, " + flown + " flown" : "its arc by eye"));
            }
            yield return Finish(playing0, speed0);
        }

        IEnumerator Finish(bool playing0, float speed0)
        {
            ShotLine.Seen = null; ShotLine.CutSeen = null; OpenZFxPlayer.ProjTrace = null;
            OzUePlace.Stop(); OzUePick.ClearInspect(); OzUePlayground.ClearField();
            OzUeControls.SetSpeed(speed0); OzUeControls.SetPlaying(playing0);
            yield return Frames(2);
            var sb = new StringBuilder();
            sb.Append(Tag).Append(' ').Append(fail == 0 ? "PASS" : "FAIL").Append(": ").Append(pass).Append(" of ").Append(pass + fail).Append(" checks").AppendLine();
            sb.Append(lines);
            Report = sb.ToString();
            if (!string.IsNullOrEmpty(Folder))
            {
                try { Directory.CreateDirectory(Folder); File.WriteAllText(Path.Combine(Folder, "report.txt"), Report.Replace("\r\n", "\n")); }
                catch (System.Exception e) { Debug.LogWarning(Tag + " the report could not be written: " + e.Message); }
            }
            if (fail == 0) Debug.Log(Report); else Debug.LogError(Report);
            Destroy(gameObject);
        }
    }
}
#endif
