using System.IO;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// Kit k93 (1.2 items 150-154) checks, menu OpenZ/k93 v2 play notes/...: the exit fit out of the factory and fort gates, the wrecks on and off
    /// camera with the cap, the hull rock probe, a report of the three switches. Reports, frame tables and pictures go to
    /// </summary>
    public static class OzK93Menu
    {
        const string M = "OpenZ/k93 v2 play notes/";
        static string Out { get { var p = Path.Combine(OpenZPaths.Records, "milestones", "k93_v2_play_notes"); Directory.CreateDirectory(p); return p; } }

        static bool Ready()
        {
            if (!Application.isPlaying || !ZGame.I || ZGame.I.World == null) { Debug.LogWarning("[OpenZ K93] enter Play first (a level running, e.g. L02, fresh session)"); return false; }
            return true;
        }

        [MenuItem(M + "1 Exit fit: every vehicle out of the factory and the fort gate, per frame (Play)", false, 1)]
        static void Exit() { if (Ready()) K93Proof.Run(Out, "exit"); }

        [MenuItem(M + "2 Wrecks: stay on camera, sink off camera, the cap zoomed out (Play)", false, 2)]
        static void Wrecks() { if (Ready()) K93Proof.Run(Out, "wrecks"); }

        [MenuItem(M + "3 Body rock: log every tank / MLRS shot on screen (Play; the same probe as -ozrockprobe)", false, 3)]
        static void Rock() { if (Ready()) { OzRockProbe.Run(); Debug.Log("[OpenZ K93] " + RecoilShow.Report()); } }

        [MenuItem(M + "4 Report", false, 4)]
        static void State() => Debug.Log($"[OpenZ K93] check {K93Proof.Report}\n{ExitDoors.Report()}\n{WreckSinkGate.Report()}\n{RecoilShow.Report()}MLRS v2 turret x{OzMlrsLauncher.ScaleV2} (one uniform scale, nothing stretched, +{OzMlrsLauncher.LiftV2M} m), resting {11.35f + OzMlrsLauncher.RestRaiseV2:0.00} deg up (v1's tilt), aim {OzMlrs.PitchMinV2:0.00}..{OzMlrs.PitchMaxV2:0.00} deg about it");

        [MenuItem(M + "5 MLRS launchers: v1 and v2 side by side, sizes on screen (Play)", false, 5)]
        static void Launchers() { if (Ready()) K93Proof.Run(Out, "mlrs"); }

        /// <summary>The MLRS v2 parity's hatch line in 3D: the smallest distance (m) between the turret (every mesh vertex of MLRSRackYaw's group, the
        /// launcher at its lowest drawn joint, at rest and at its highest) turned round its axis every 10 deg and the hatch lid opened +-95 deg about
        /// its hinge. The open lid stands out of the turret's way when this is 0.1 m or more (the horizontal sweep alone reaches the lid's hinge since
        /// k93's bigger launcher, item 151, but passes high over it).</summary>
        public static float MlrsLidClear(GameObject v2, out string note)
        {
            note = "no turret / lid"; if (!v2) return -1f;
            Transform yaw = null, pitch = null, hinge = null, lid = null;
            foreach (var t in v2.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "MLRSRackYaw") yaw = t; else if (t.name == "MLRSRackPitch") pitch = t;
                else if (t.name == "mlrs_HatchHinge") hinge = t; else if (t.name == "mlrs_HatchLid") lid = t;
            }
            if (!yaw || !pitch || !hinge || !lid) return -1f;
            var turret = new System.Collections.Generic.List<(Vector3 p, bool onPitch)>();
            foreach (var mf in yaw.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!mf.sharedMesh || !mf.sharedMesh.isReadable || mf.name.StartsWith("frag_")) continue;
                bool under = false; for (var t = mf.transform; t; t = t.parent) { if (t.name == "OZ_WreckFragments") { under = true; break; } if (t == yaw) break; }
                if (under) continue;
                bool onP = mf.transform.IsChildOf(pitch);
                foreach (var v in mf.sharedMesh.vertices) turret.Add((mf.transform.TransformPoint(v), onP));
            }
            var lidPts = new System.Collections.Generic.List<Vector3>();
            var lmf = lid.GetComponent<MeshFilter>();
            if (!lmf || !lmf.sharedMesh || !lmf.sharedMesh.isReadable) { note = "the lid mesh is not readable"; return -1f; }
            foreach (var ang in new[] { 95f, -95f })
            {
                var q = Quaternion.AngleAxis(ang, hinge.right);
                foreach (var v in lmf.sharedMesh.vertices) lidPts.Add(hinge.position + q * (lid.TransformPoint(v) - hinge.position));
            }
            float best = 1e9f, bestYaw = 0f, bestJoint = 0f;
            float low = OzMlrs.PitchMinV2 - MlrsSideKick.UpDeg * 0.3f, high = OzMlrs.PitchMaxV2 + 8f * 1.133f + MlrsSideKick.UpDeg * 1.2f;
            foreach (var joint in new[] { low, 0f, high })
            {
                var qp = Quaternion.AngleAxis(joint, pitch.right);                                         // + joint = the front (-z) up
                for (int yd = 0; yd < 360; yd += 10)
                {
                    var qy = Quaternion.AngleAxis(yd, Vector3.up);
                    foreach (var (p, onP) in turret)
                    {
                        var w = onP ? pitch.position + qp * (p - pitch.position) : p;
                        w = yaw.position + qy * (w - yaw.position);
                        if ((new Vector2(w.x - hinge.position.x, w.z - hinge.position.z)).sqrMagnitude > 4f) continue;
                        foreach (var l in lidPts) { float d = (w - l).sqrMagnitude; if (d < best) { best = d; bestYaw = yd; bestJoint = joint; } }
                    }
                }
            }
            best = best < 1e8f ? Mathf.Sqrt(best) : 9f;
            note = $"the open lid (+-95 deg) at least {best:0.00} m from the turret at every heading and the launcher's joint {low:0.0} / 0 / {high:0.0} deg (closest at {bestYaw:0} deg, joint {bestJoint:0.0})";
            return best;
        }
    }
}
