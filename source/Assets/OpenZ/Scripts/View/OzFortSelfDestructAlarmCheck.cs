#if !OZ_RELEASE
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
    /// 1.5 kit self_destruct_v2: the Play check of the bigger wall panel's alarm beacon (View/OzFortSelfDestructAlarm.cs), one level per run (menu
    /// OpenZ/V1.5/Fort: Wall Alarm Check (Play, a level playing)). Not in the release exe. The panel itself (bigger now) is checked by the panel's
    /// own Play check (OpenZ/V1.5/Fort: Wall Panel Check; its P3 box limits widened by this kit). For every fort of the level:
    /// <list type="number">
    /// <item>A1 the beacon built on the fort's rig beside the panel, on the panel's pose (1 mm);</item>
    /// <item>A2 every mount box and the dome clip into nothing: each gate_* mesh and FortBase triangle against each part's box grown 3 mm (exact
    /// triangle / box test, the part's own space) - 0 touching; its top under the hall's light strip, its parts inside the last bay;</item>
    /// <item>A3 always on: the reflector turns one turn in about <see cref="OzFortSelfDestructAlarm.SpinCalm"/> s (measured over 2 s);</item>
    /// <item>A4 the fort below 25 % (its health set for the check, put back after): the turn about <see cref="OzFortSelfDestructAlarm.SpinUrgent"/> s,
    /// the glow pulsing, the hum pitch up;</item>
    /// <item>A5 the hum: the camera in the hallway (borrowed, given back) - its loop plays; the camera at the RTS height - no loop, no real light;</item>
    /// <item>A6 gone with the fort: the panel hidden for a frame (as the hall's collapse does) - the beacon dark and silent; shown again - back.</item>
    /// </list>
    /// Report wall_alarm_report.txt in <see cref="Folder"/>.
    /// </summary>
    public sealed class OzFortSelfDestructAlarmCheck : MonoBehaviour
    {
        const string Tag = "[OpenZ V1.5 fort wall alarm]";
        public static string Report = "not run", Folder;
        public static bool Running;
        readonly StringBuilder sb = new StringBuilder(); int pass, fail;

        public static void Begin()
        {
            if (Running || !ZGame.I || ZGame.I.World == null) { Debug.LogWarning(Tag + " a level must be playing"); return; }
            Running = true; new GameObject("OZ_FortWallAlarmCheck").AddComponent<OzFortSelfDestructAlarmCheck>();
        }

        void Row(string id, bool ok, string what) { sb.AppendLine($"{(ok ? "PASS" : "FAIL")}  {id} {what}"); if (ok) pass++; else fail++; }

        IEnumerator Start()
        {
            yield return new WaitForSeconds(1.5f);
            var g = ZGame.I; var fvs = FactoryViews.I; var al = OzFortSelfDestructAlarm.I;
            sb.AppendLine($"# fort wall alarm check, {System.DateTime.Now:yyyy-MM-dd HH:mm}, {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
            if (!fvs || !al) { Row("A0", false, "no FactoryViews or no OzFortSelfDestructAlarm"); }
            else
                foreach (var o in g.World.Objects.Where(x => x != null && x.IsFort && !x.Destroyed).ToList())
                {
                    var fv = fvs.For(o.Id); if (!fv) continue;
                    sb.AppendLine($"## {o.Type} #{o.Id} ({o.Owner})");
                    yield return Fort(g, fv, o, al);
                }
            sb.AppendLine($"{pass} PASS, {fail} FAIL");
            Report = sb.ToString();
            if (!string.IsNullOrEmpty(Folder)) { Directory.CreateDirectory(Folder); File.WriteAllText(Path.Combine(Folder, "wall_alarm_report.txt"), Report); }
            Debug.Log(Tag + "\n" + Report);
            Running = false; Destroy(gameObject);
        }

        IEnumerator Fort(ZGame g, FactoryView fv, ZObject o, OzFortSelfDestructAlarm al)
        {
            var bt = OzFortSelfDestructAlarm.BeaconOf(fv); var panel = OzFortSelfDestructPanels.PanelOf(fv);
            Row("A1", bt && panel && (bt.position - panel.position).magnitude < 0.001f && Quaternion.Angle(bt.rotation, panel.rotation) < 0.05f, bt ? $"on the rig {fv.name}, on the panel's pose" : "no beacon");
            if (!bt || !panel) yield break;
            // A2: the parts against the gate kit
            var b = fv.Building; int touch = 0, tested = 0; var touched = new List<string>();
            var parts = bt.GetComponentsInChildren<MeshFilter>(true).Where(m => m.sharedMesh && (m.name == "Mount" || m.name == "Dome")).ToList();
            float topY = float.NegativeInfinity;
            foreach (var p in parts)
            {
                var mb = p.sharedMesh.bounds; Vector3 c = mb.center, h = mb.extents + Vector3.one * 0.003f;
                for (int i = 0; i < 8; i++) { var q = b.InverseTransformPoint(p.transform.TransformPoint(mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)))); topY = Mathf.Max(topY, q.y); }
                foreach (var m in b.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (!m.sharedMesh || !(m.name.StartsWith("gate_") || m.name == "FortBase") || !m.sharedMesh.isReadable) continue;
                    var M = p.transform.worldToLocalMatrix * m.transform.localToWorldMatrix; var v = m.sharedMesh.vertices; int hits = 0;
                    for (int s = 0; s < m.sharedMesh.subMeshCount; s++)
                    {
                        var t = m.sharedMesh.GetTriangles(s);
                        for (int i = 0; i < t.Length; i += 3) { tested++; if (TriBox(c, h, M.MultiplyPoint3x4(v[t[i]]), M.MultiplyPoint3x4(v[t[i + 1]]), M.MultiplyPoint3x4(v[t[i + 2]]))) hits++; }
                    }
                    if (hits > 0) { touch += hits; touched.Add(p.name + "/" + m.name + " " + hits); }
                }
            }
            var minT = Find(b, "gate_mark_min"); float floor = minT ? b.InverseTransformPoint(minT.position).y : OzFortSelfDestructPanels.KitMin.y;
            Row("A2", touch == 0 && parts.Count >= 5 && topY < floor + 2.093f, $"{parts.Count} parts, {tested} kit triangles tested: {touch} touch (+3 mm){(touched.Count > 0 ? ": " + string.Join(", ", touched) : "")}; top {topY:0.000} (the strip's bottom about {floor + 2.098f:0.000})");
            // A3 / A4: the spin calm and urgent
            var spin = bt.Find("Spin");
            float a0 = spin ? spin.localEulerAngles.y : 0f, acc = 0f, prev = a0, t0 = Time.time;
            while (Time.time - t0 < 2f) { yield return null; if (spin) { float a = spin.localEulerAngles.y; acc += Mathf.Repeat(a - prev, 360f); prev = a; } }
            float calm = acc > 1f ? 360f * (Time.time - t0) / acc : 0f;
            Row("A3", Mathf.Abs(calm - OzFortSelfDestructAlarm.SpinCalm) < 0.15f, $"one turn in {calm:0.00} s (want {OzFortSelfDestructAlarm.SpinCalm:0.00})");
            int h0 = o.Health; o.SetHealth(o.MaxHealth / 5);
            yield return new WaitForSeconds(1.2f);
            acc = 0f; prev = spin ? spin.localEulerAngles.y : 0f; t0 = Time.time; int u0 = OzFortSelfDestructAlarm.UrgentFrames;
            while (Time.time - t0 < 2f) { yield return null; if (spin) { float a = spin.localEulerAngles.y; acc += Mathf.Repeat(a - prev, 360f); prev = a; } }
            float urgent = acc > 1f ? 360f * (Time.time - t0) / acc : 0f;
            al.StateOf(fv, out _, out bool urg, out _, out _, out _);
            Row("A4", urg && Mathf.Abs(urgent - OzFortSelfDestructAlarm.SpinUrgent) < 0.1f && OzFortSelfDestructAlarm.UrgentFrames > u0, $"the fort at {100f * o.Health / Mathf.Max(1, o.MaxHealth):0} %: urgent {urg}, one turn in {urgent:0.00} s (want {OzFortSelfDestructAlarm.SpinUrgent:0.00})");
            o.SetHealth(h0);
            // A5: the hum near (the camera borrowed into the hallway) and far (the RTS camera)
            var cam = Camera.main; bool humNear = false, humFar = true, lightFar = true;
            if (cam)
            {
                var cp = cam.transform.position; var cr = cam.transform.rotation;
                var eye = b.TransformPoint(new Vector3(-0.1f, floor + 1.8f, OzFortSelfDestructPanels.KitInZ + 1.3f));
                for (int i = 0; i < 40; i++) { cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(bt.position - eye)); yield return null; }
                al.StateOf(fv, out _, out _, out humNear, out _, out _);
                cam.transform.SetPositionAndRotation(cp, cr);
                for (int i = 0; i < 30; i++) yield return null;
                al.StateOf(fv, out _, out _, out humFar, out lightFar, out _);
            }
            Row("A5", humNear && !humFar && !lightFar, $"the hum in the hallway {humNear}; at the RTS camera: hum {humFar}, real light {lightFar}");
            // A6: gone with the panel
            // the hall's collapse hides the hall renderer; OzFortSelfDestructPanels then hides the panel and the beacon follows (hiding the panel's own
            // renderer here would be shown again by OzFortSelfDestructPanels the same frame)
            var hallT = Find(fv.Building, "gate_hall"); var hr = hallT ? hallT.GetComponent<Renderer>() : null; bool was = hr && hr.forceRenderingOff;
            if (hr) hr.forceRenderingOff = true;
            yield return null; yield return null;
            al.StateOf(fv, out _, out _, out _, out _, out bool shownOff);
            bool solidsOff = hr && bt.GetComponentsInChildren<Renderer>(true).All(r => !r.enabled);
            if (hr) hr.forceRenderingOff = was; yield return null; yield return null;
            al.StateOf(fv, out _, out _, out _, out _, out bool shownOn);
            Row("A6", !shownOff && solidsOff && shownOn, $"panel hidden: beacon shown {shownOff}, every beacon renderer off {solidsOff}; panel back: shown {shownOn}");
        }

        static Transform Find(Transform t, string n)
        {
            if (t.name == n) return t;
            foreach (Transform c in t) { var f = Find(c, n); if (f) return f; }
            return null;
        }

        static bool TriBox(Vector3 c, Vector3 h, Vector3 a, Vector3 b, Vector3 d)
        {
            Vector3 v0 = a - c, v1 = b - c, v2 = d - c, e0 = v1 - v0, e1 = v2 - v1, e2 = v0 - v2;
            if (Sep(Vector3.right, v0, v1, v2, h) || Sep(Vector3.up, v0, v1, v2, h) || Sep(Vector3.forward, v0, v1, v2, h)) return false;
            if (Sep(Vector3.Cross(e0, e1), v0, v1, v2, h)) return false;
            var axes = new[] { Vector3.right, Vector3.up, Vector3.forward }; var edges = new[] { e0, e1, e2 };
            foreach (var u in axes) foreach (var e in edges) if (Sep(Vector3.Cross(u, e), v0, v1, v2, h)) return false;
            return true;
        }

        static bool Sep(Vector3 L, Vector3 v0, Vector3 v1, Vector3 v2, Vector3 h)
        {
            if (L.sqrMagnitude < 1e-12f) return false;
            float p0 = Vector3.Dot(L, v0), p1 = Vector3.Dot(L, v1), p2 = Vector3.Dot(L, v2);
            float r = h.x * Mathf.Abs(L.x) + h.y * Mathf.Abs(L.y) + h.z * Mathf.Abs(L.z);
            return Mathf.Min(p0, Mathf.Min(p1, p2)) > r || Mathf.Max(p0, Mathf.Max(p1, p2)) < -r;
        }
    }
}
#endif
