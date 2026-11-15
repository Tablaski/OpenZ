using System;
using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace OpenZ
{
    /// <summary>Pooled native-canvas damage numbers and per-object scores. No dependency on selection or HUD health-bar settings.</summary>
    public sealed class OzUnitTestingOverlay : MonoBehaviour
    {
        sealed class Badge { public RectTransform Root; public OzText Kills, Deaths, Ratio, Debug; public ZObject Unit; public float GoneAt = -1; }
        sealed class Number { public RectTransform Root; public OzText Text; public float Until, Started; public int Id; public Vector3 At; }
        readonly Dictionary<int, Badge> badges = new Dictionary<int, Badge>();
        readonly List<Number> numbers = new List<Number>();
        readonly List<int> remove = new List<int>();
        readonly List<RectTransform> debugLines = new List<RectTransform>();
        int usedLines;
        RectTransform root;
        readonly Dictionary<Team, RectTransform> respawnMarkers = new Dictionary<Team, RectTransform>();
        ZWorld World => OzUnitEditor.I != null ? OzUnitEditor.I.World : null;
        void LateUpdate()
        {
            if (!OzUnitEditor.Active || World?.UnitTesting == null || !Camera.main) return;
            if (!root)
            {
                var layer = OzUnitEditor.Layer(UeLayer.Field); if (!layer) return;
                root = Hud.Node("UnitTestingOverlays", layer); root.Fill();
                var group = root.gameObject.AddComponent<CanvasGroup>(); group.blocksRaycasts = false; group.interactable = false;
            }
            var testing = World.UnitTesting;
            usedLines = 0;
            var panel = OzBehaviorPanel.I;
            if (panel && (panel.SimulationDebug || panel.RouteDebug)) foreach (var o in World.Objects)
            {
                if (!ZPlaygroundTesting.Eligible(o) || o.Destroyed) continue;
                var sim = OzUnitEditor.I.Game.CenterWorld(o) + Vector3.up * .3f;
                if (panel.SimulationDebug)
                {
                    DebugLine(sim - Vector3.right * .45f, sim + Vector3.right * .45f, Color.cyan);
                    DebugLine(sim - Vector3.forward * .45f, sim + Vector3.forward * .45f, Color.cyan);
                    // Compare ground-plane positions: model elevation is not movement lag.
                    var model = Position(o); model.y = sim.y;
                    if ((model - sim).sqrMagnitude > .0025f)
                    {
                        DebugLine(sim, model, OzForkOptions.Accent);
                        DebugLine(model - Vector3.right * .25f, model + Vector3.right * .25f, OzForkOptions.Accent);
                        DebugLine(model - Vector3.forward * .25f, model + Vector3.forward * .25f, OzForkOptions.Accent);
                    }
                }
                if (panel.RouteDebug)
                {
                    DebugArrow(sim, new Vector3(o.Dx, 0, -o.Dy) * ZConst.PX_TO_M, Color.green);
                    if (o.IsMoving && o.Waypoints.Count > 0)
                    {
                        // Orders may contain stale/unused coordinates (notably attack orders).
                        var target = o.CurrentTarget;
                        var end = ZGame.PxToWorld(target.x, target.y); end.y = sim.y;
                        DebugArrow(sim, end - sim, Color.yellow);
                    }
                }
            }
            for (int i = usedLines; i < debugLines.Count; i++) debugLines[i].gameObject.SetActive(false);
            foreach (Team team in new[] { Team.Red, Team.Blue })
            {
                if (!testing.TryGetRespawnPoint(team, out int x, out int y)) continue;
                if (!respawnMarkers.TryGetValue(team, out var marker))
                {
                    marker = Hud.Node(team + "Respawn", root); marker.sizeDelta = new Vector2(180, 28);
                    Hud.Label(marker, "Point", "+ " + team.ToString().ToUpperInvariant() + " RESPAWN", 15,
                        team == Team.Red ? new Color(1, .3f, .3f) : new Color(.35f, .65f, 1), 0, OzStyle.Outline).FillParent(OzH.C);
                    respawnMarkers[team] = marker;
                }
                var point = ZGame.PxToWorld(x, y); point.y = OzUnitEditor.I.Game.GroundY(point.x, point.z) + .2f;
                marker.gameObject.SetActive(Place(marker, point));
            }
            while (testing.TryReadHit(out var hit))
            {
                if (!testing.ShowDamage || hit.Damage <= 0) continue;
                Number number = numbers.Find(n => n.Until <= Time.unscaledTime);
                if (number == null && numbers.Count < 256)
                {
                    number = new Number { Root = Hud.Node("Damage", root) }; number.Root.sizeDelta = new Vector2(190, 32);
                    number.Text = Hud.Label(number.Root, "HP", "", 22, OzUi.Ink, 0, OzStyle.Outline).FillParent(OzH.C);
                    numbers.Add(number);
                }
                if (number == null) continue;
                number.Id = hit.Victim; number.At = new Vector3(hit.X * ZConst.PX_TO_M, 1.5f, -hit.Y * ZConst.PX_TO_M);
                number.Started = Time.unscaledTime; number.Until = number.Started + 1.25f;
                number.Text.Set("-" + hit.Damage + (hit.Theoretical ? " *" : "") + (hit.Driver ? " crew" : ""));
                number.Text.color = hit.Theoretical ? (Color)OzUi.AmberHi : new Color(1, .4f, .32f);
            }
            bool show = testing.ShowCounts, debug = OzBehaviorPanel.I && OzBehaviorPanel.I.DebugEnabled;
            if (show || debug) foreach (var o in World.Objects)
            {
                if (!ZPlaygroundTesting.Eligible(o)) continue;
                if (!badges.TryGetValue(o.Id, out var b))
                {
                    b = new Badge { Root = Hud.Node("Score_" + o.Id, root), Unit = o }; b.Root.sizeDelta = new Vector2(144, 35);
                    MakeTrophy(b.Root, 4, 7); MakeSkull(b.Root, 78, 7);
                    b.Kills = Hud.Label(b.Root, "Kills", "0", 23, OzUi.AmberHi, 0, OzStyle.Outline).At(29, 18, OzH.L, true, 48);
                    b.Deaths = Hud.Label(b.Root, "Deaths", "0", 23, OzUi.Ink, 0, OzStyle.Outline).At(103, 18, OzH.L, true, 48);
                    b.Ratio = Hud.Label(b.Root, "Success", "0%", 21, OzUi.Ink, 0, OzStyle.Outline).At(72, -10, OzH.C, true, 144);
                    b.Debug = Hud.Label(b.Root, "Reason", "", 11, OzUi.Ink, 0, OzStyle.Outline).At(72, -32, OzH.C, true, 260);
                    badges.Add(o.Id, b);
                }
                if (!o.Destroyed) b.GoneAt = -1;
                var score = testing.For(o.Id); b.Kills.Set(score.Kills.ToString()); b.Deaths.Set(score.Deaths.ToString());
                b.Ratio.Set(score.SuccessPercent.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "%");
                b.Kills.SetActive(show); b.Deaths.SetActive(show); b.Ratio.SetActive(show);
                b.Root.Find("Trophy").gameObject.SetActive(show); b.Root.Find("Skull").gameObject.SetActive(show);
                bool physical = o.Behavior != null && o.Behavior.On(ZBehaviorKey.Physical);
                double dx = physical ? o.BehaviorStepDx : o.Dx, dy = physical ? o.BehaviorStepDy : o.Dy;
                b.Debug.SetActive(debug); b.Debug.Set(o.BehaviorDodgeReason + (physical ? " | " + o.BehaviorMotionPhase : "") + "\n" +
                    (o.IsRunning ? "BOOST" : "WALK") + " | stamina " + o.Stamina.ToString("0.0") +
                    " | sim " + (Math.Sqrt(dx * dx + dy * dy) * ZConst.PX_TO_M).ToString("0.0") + " m/s" +
                    " | gap " + Vector3.Distance(Position(o), OzUnitEditor.I.Game.CenterWorld(o)).ToString("0.0") + " m");
            }
            remove.Clear();
            foreach (var pair in badges)
            {
                var b = pair.Value; var o = World.Get(pair.Key);
                if (o == null || o.Destroyed || !ZPlaygroundTesting.Eligible(o))
                {
                    if (b.GoneAt < 0) b.GoneAt = Time.unscaledTime;
                    if (Time.unscaledTime - b.GoneAt > 2.5f) { remove.Add(pair.Key); b.Root.gameObject.SetActive(false); continue; }
                    var score = testing.For(pair.Key); b.Kills.Set(score.Kills.ToString()); b.Deaths.Set(score.Deaths.ToString());
                    b.Ratio.Set(score.SuccessPercent.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "%");
                }
                b.Root.gameObject.SetActive((show || debug) && Place(b.Root, Position(b.Unit) + Vector3.up * (b.Unit.Kind == ZKind.Vehicle ? 3.3f : 2.1f)));
            }
            foreach (int id in remove) { Destroy(badges[id].Root.gameObject); badges.Remove(id); }
            foreach (var n in numbers)
            {
                bool active = testing.ShowDamage && n.Until > Time.unscaledTime;
                var o = World.Get(n.Id); var at = o != null ? Position(o) + Vector3.up * (o.Kind == ZKind.Vehicle ? 3.9f : 2.8f) : n.At;
                n.Root.gameObject.SetActive(active && Place(n.Root, at));
                n.Root.anchoredPosition += Vector2.up * ((Time.unscaledTime - n.Started) * 65);
                var c = n.Text.color; c.a = Mathf.Clamp01((n.Until - Time.unscaledTime) / .4f); n.Text.color = c;
            }
        }
        void DebugLine(Vector3 from, Vector3 to, Color color)
        {
            var a = Camera.main.WorldToScreenPoint(from); var b = Camera.main.WorldToScreenPoint(to);
            if (a.z <= 0 || b.z <= 0) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, a, null, out var start);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, b, null, out var end);
            ScreenLine(start, end, color);
        }
        void DebugArrow(Vector3 from, Vector3 offset, Color color)
        {
            // These are local direction hints, not a full route or a trajectory prediction.
            var camera = Camera.main;
            var a = camera.WorldToScreenPoint(from);
            var b = camera.WorldToScreenPoint(from + Vector3.ClampMagnitude(offset, 6f));
            if (a.z <= camera.nearClipPlane || b.z <= camera.nearClipPlane ||
                a.x < 0 || a.x > Screen.width || a.y < 0 || a.y > Screen.height) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, a, null, out var start);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, b, null, out var end);
            if (!root.rect.Contains(start)) return;
            var delta = Vector2.ClampMagnitude(end - start, 100f);
            if (delta.sqrMagnitude < 9f) return;
            end = start + delta;
            var back = -delta.normalized * Mathf.Min(7f, delta.magnitude * .3f);
            var side = new Vector2(-back.y, back.x) * .5f;
            ScreenLine(start, end, color);
            ScreenLine(end, end + back + side, color);
            ScreenLine(end, end + back - side, color);
        }
        void ScreenLine(Vector2 start, Vector2 end, Color color)
        {
            if (usedLines == debugLines.Count)
            {
                var img = Hud.Img(root, "SimulationDebug", color); img.raycastTarget = false;
                debugLines.Add(img.rectTransform);
            }
            var line = debugLines[usedLines++]; line.gameObject.SetActive(true);
            line.GetComponent<Image>().color = color;
            line.anchorMin = line.anchorMax = root.pivot; line.pivot = new Vector2(0, .5f);
            line.anchoredPosition = start; line.sizeDelta = new Vector2(Vector2.Distance(start, end), 2);
            line.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(end.y - start.y, end.x - start.x) * Mathf.Rad2Deg);
        }
        Vector3 Position(ZObject o)
        {
            var game = OzUnitEditor.I.Game;
            return game.Views.TryGetValue(o.Id, out var view) && view ? view.transform.position : game.CenterWorld(o);
        }
        bool Place(RectTransform rect, Vector3 at)
        {
            var p = Camera.main.WorldToScreenPoint(at);
            if (p.z <= 0 || p.x < 0 || p.x > Screen.width || p.y < 0 || p.y > Screen.height) return false;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, p, null, out var local);
            rect.anchorMin = rect.anchorMax = root.pivot; rect.pivot = new Vector2(.5f, .5f); rect.anchoredPosition = local; return true;
        }
        static void Pixel(Transform parent, string name, Color c, float x, float y, float w, float h)
        { var p = Hud.Img(parent, name, c); p.rectTransform.TL(x, y, w, h); p.raycastTarget = false; }
        static void MakeTrophy(Transform parent, float x, float y)
        {
            var r = Hud.Node("Trophy", parent); r.TL(x, y, 21, 24);
            Pixel(r, "Cup", OzUi.AmberHi, 5, 1, 11, 10); Pixel(r, "Stem", OzUi.AmberHi, 9, 11, 3, 6); Pixel(r, "Base", OzUi.AmberHi, 4, 18, 13, 3);
            Pixel(r, "HandleL", OzUi.AmberHi, 1, 3, 3, 7); Pixel(r, "HandleR", OzUi.AmberHi, 17, 3, 3, 7);
        }
        static void MakeSkull(Transform parent, float x, float y)
        {
            var r = Hud.Node("Skull", parent); r.TL(x, y, 21, 24);
            Pixel(r, "Head", OzUi.Ink, 2, 1, 17, 14); Pixel(r, "Jaw", OzUi.Ink, 5, 15, 11, 5);
            Pixel(r, "EyeL", OzUi.GM950, 5, 6, 4, 4); Pixel(r, "EyeR", OzUi.GM950, 12, 6, 4, 4); Pixel(r, "Nose", OzUi.GM950, 9, 11, 3, 3);
        }
        void OnDestroy() { if (root) Destroy(root.gameObject); }
    }
}
