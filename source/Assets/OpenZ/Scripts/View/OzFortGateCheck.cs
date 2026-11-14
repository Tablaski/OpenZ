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
    /// 1.4, the bigger enter-the-fort click (View/PlayerInput.FortGate.cs): the Play check, one level per run (menu OpenZ/V1.4/Fort: Gate Click Check
    /// (Play, a level playing)). Nothing is ordered; every point is probed through PlayerInput's own hover pick and order builder.
    /// <list type="number">
    /// <item>The normal RTS camera (height <see cref="CamHeight"/> m, the level's pitch and turn) over the enemy fort's gate; one of the player's robots selected.</item>
    /// <item>A grid of screen points round the door's picture (<see cref="Half"/> px each way, every <see cref="Step"/> px, x the UI scale): for each, the
    /// right-click before this change (HoverAt + Zod's door rectangle) and now (gate zone, body, shooting parts), and with Ctrl.</item>
    /// <item>A test enemy grunt (the fort's team, cannot move or fire) dropped on the gate point: the click on its body attacks it.</item>
    /// <item>One picture with the points drawn (green: enter before and now; cyan: enter now only; red: a shooting part, still the attack; yellow: a unit,
    /// its own order; grey: neither) and the report: Folder/fort_gate_&lt;level&gt;.png and fort_gate_report.txt.</item>
    /// </list>
    /// PASS: enter points now at least <see cref="MinGain"/> x before, the door's centre point enters, no point that entered before lost, every unit point and
    /// every shooting-part point keeps its order, the test grunt is attacked.
    /// </summary>
    public sealed class OzFortGateCheck : MonoBehaviour
    {
        const string Tag = "[OpenZ V1.4 fort gate]";
        public static string Report = "not run", Folder;
        public static bool Running;
        public const float CamHeight = 70f, Half = 260f, Step = 20f, MinGain = 3f;

        public static void Begin()
        {
            if (Running) { Debug.LogWarning(Tag + " already running"); return; }
            new GameObject("OZ_FortGateCheck").AddComponent<OzFortGateCheck>();
        }

        IEnumerator Start()
        {
            Running = true;
            try { yield return Main(); }
            finally { Running = false; Destroy(gameObject); }
        }

        IEnumerator Main()
        {
            var g = ZGame.I ? ZGame.I : FindFirstObjectByType<ZGame>(); var input = FindFirstObjectByType<PlayerInput>(); var cam = FindFirstObjectByType<RtsCamera>();
            var W = g ? g.World : null;
            if (!g || W == null || !input || !cam) { Report = Tag + " FAIL: no level playing (ZGame, PlayerInput, RtsCamera)"; Debug.LogWarning(Report); yield break; }
            var fort = W.Objects.FirstOrDefault(o => o.IsFort && !o.Destroyed && o.Owner != g.player && o.Owner != Team.Neutral);
            if (fort == null) { Report = Tag + " FAIL: no enemy fort standing"; Debug.LogWarning(Report); yield break; }
            fort.GetCreationMovePoint(out int ex, out int ey); fort.GetCreationPoint(out _, out int cy);
            var u = W.OwnedSelectable(g.player).FirstOrDefault(o => o.Kind == ZKind.Robot && o.CanMove);
            string spawned = "";
            if (u == null)
            {
                var own = W.Objects.First(o => o.IsFort && o.Owner == g.player); own.GetCreationMovePoint(out int ox, out int oy);
                u = W.CreateRobotGroup("grunt", ox - 8, oy - 8, g.player, 1); spawned = " (no robot of the player: one grunt made at its own gate)";
                yield return new WaitForSecondsRealtime(0.5f);
            }
            var keepSel = new List<ZObject>(input.Selection);
            input.Selection.Clear(); input.Selection.Add(u);
            var door = ZGame.PxToWorld(ex, (ey + cy) / 2);
            cam.focus = new Vector3(door.x, 0f, door.z); cam.SetHeightImmediate(CamHeight);
            yield return new WaitForSecondsRealtime(0.6f);
            var c = cam.GetComponent<Camera>();
            var fv = FactoryViews.I ? FactoryViews.I.For(fort.Id) : null;
            Vector3 doorW = fv ? fv.transform.TransformPoint((fv.DoorMin + fv.DoorMax) * 0.5f) : g.CenterWorld(fort);
            Vector2 d0 = c.WorldToScreenPoint(doorW);
            float k = Mathf.Max(0.75f, Screen.height / 1080f), half = Half * k, step = Step * k;

            int n = 0, oldE = 0, newE = 0, gate = 0, body = 0, unitP = 0, unitKept = 0, shootP = 0, shootKept = 0, lost = 0, ctrlBody = 0, bodyP = 0;
            bool centre = false;
            var dots = new List<(Vector2 p, Color c)>();
            for (float y = -half; y <= half + 0.5f; y += step)
                for (float x = -half; x <= half + 0.5f; x += step)
                {
                    var mp = d0 + new Vector2(x, y);
                    if (mp.x < 0 || mp.y < 0 || mp.x > Screen.width || mp.y > Screen.height) continue;
                    var r = input.FortGateProbe(mp, u); n++;
                    if (r.oldEnter) oldE++;
                    if (r.newEnter) { newE++; if (r.part == PlayerInput.FortPart.Gate) gate++; else if (r.part == PlayerInput.FortPart.Body) body++; }
                    if (r.oldEnter && !r.newEnter) lost++;
                    if (r.part == PlayerInput.FortPart.Body) { bodyP++; if (!r.newEnterCtrl) ctrlBody++; }
                    Color col = new Color(0.5f, 0.5f, 0.5f);
                    if (r.hoverOld != null && r.hoverOld.IsUnit) { unitP++; if (r.hoverNew == r.hoverOld && !r.newEnter) unitKept++; col = Color.yellow; }
                    else if (r.part == PlayerInput.FortPart.Shoot) { shootP++; if (!r.newEnter) shootKept++; col = Color.red; }
                    else if (r.newEnter) col = r.oldEnter ? Color.green : Color.cyan;
                    if (Mathf.Abs(x) < step * 0.5f && Mathf.Abs(y) < step * 0.5f) centre = r.newEnter;
                    dots.Add((mp, col));
                }

            // the test enemy grunt on the gate point: a click on its body attacks it
            var t = W.CreateRobotGroup("grunt", ex - 8, ey - 8, fort.Owner, 1);
            t.MoveSpeed = 0; t.RealMoveSpeed = 0; t.Damage = 0;
            g.SpawnUnitView(t); foreach (var tm in t.Minions) g.SpawnUnitView(tm);   // w1 5 Nov: a unit made by the sim API gets its view only from the spawner's call (as OzCheatSpawner.Spawn)
            yield return new WaitForSecondsRealtime(1.0f);
            bool attacked = false; string tWhy = "no view";
            if (g.Views.TryGetValue(t.Id, out var tv) && tv)
            {
                Vector2 tp = c.WorldToScreenPoint(tv.transform.position + Vector3.up * 0.9f);
                var r = input.FortGateProbe(tp, u);
                attacked = r.hoverOld == t && r.hoverNew == t && r.newMode == WpMode.Attack && r.newRef == t.Id;
                tWhy = $"hover {(r.hoverOld == null ? "none" : r.hoverOld.Type + " #" + r.hoverOld.Id)}, order {r.newMode} on #{r.newRef}";
                dots.Add((tp, Color.magenta));
            }
            if (!t.Destroyed) { t.SetHealth(0); W.UpdateObjectHealth(t); }

            yield return new WaitForEndOfFrame();
            string png = null;
            try
            {
                var tex = ScreenCapture.CaptureScreenshotAsTexture();
                foreach (var (p, col) in dots)
                    for (int dy = -2; dy <= 2; dy++) for (int dx = -2; dx <= 2; dx++)
                    {
                        int px = (int)p.x + dx, py = (int)p.y + dy;
                        if (px >= 0 && py >= 0 && px < tex.width && py < tex.height) tex.SetPixel(px, py, Mathf.Abs(dx) == 2 || Mathf.Abs(dy) == 2 ? Color.black : col);
                    }
                tex.Apply();
                if (!string.IsNullOrEmpty(Folder)) { Directory.CreateDirectory(Folder); png = Path.Combine(Folder, "fort_gate_" + g.level + ".png"); File.WriteAllBytes(png, tex.EncodeToPNG()); }
                Destroy(tex);
            }
            catch (System.Exception ex2) { png = "picture failed: " + ex2.Message; }

            input.Selection.Clear(); foreach (var s in keepSel) if (s != null && !s.Destroyed) input.Selection.Add(s);
            bool pass = newE >= MinGain * Mathf.Max(1, oldE) && centre && lost == 0 && unitKept == unitP && shootKept == shootP && attacked && ctrlBody == bodyP;
            var sb = new StringBuilder();
            sb.Append($"{Tag} {(pass ? "PASS" : "FAIL")} {g.level} {fort.Type} #{fort.Id} ({fort.Owner}), camera {CamHeight:0} m, pitch {cam.Tilt:0} deg, turn {cam.yaw:0} deg, screen {Screen.width}x{Screen.height}; ");
            sb.Append($"grid {n} points every {step:0} px within {half:0} px of the door's picture; enter before {oldE}, now {newE} (gate zone {gate}, body {body}; x{newE / (float)Mathf.Max(1, oldE):0.0}); ");
            sb.Append($"door centre {(centre ? "enters" : "NOT")}; lost {lost}; unit points kept {unitKept}/{unitP}; shooting parts kept as the attack {shootKept}/{shootP}; body with Ctrl = attack {ctrlBody}/{bodyP}; ");
            sb.Append($"test grunt on the gate point: {(attacked ? "attacked" : "NOT attacked")} ({tWhy}); unit {u.Type} #{u.Id}{spawned}; picture {png ?? "-"}");
            Report = sb.ToString();
            Debug.Log(Report);
            if (!string.IsNullOrEmpty(Folder)) { try { File.AppendAllText(Path.Combine(Folder, "fort_gate_report.txt"), System.DateTime.Now.ToString("dd MMM HH:mm") + " " + Report + "\n"); } catch { } }
        }
    }
}
