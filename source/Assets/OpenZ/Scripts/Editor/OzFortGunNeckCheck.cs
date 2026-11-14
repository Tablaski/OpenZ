using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace OpenZ.EditorTools
{
    /// <summary>
    /// Version 1.3, the fort cannon's hydraulic neck and its nose (menu OpenZ/V1.3/Fort Gun Neck ...), in Play on a level with forts.
    /// <list type="bullet">
    /// <item>Check: every standing fort cannon has the modelled neck (6 renderers, none of the old rams), its rams follow the design over the head's whole
    /// tilt (staged 0..30 by 0.5 deg: positions against the design to 1e-4, scale 1, the rod always in its stage), the trunk and the head's collar wear the
    /// gun's own material and paint; then pictures of each cannon at tilt 0 / 15 / 30 from the RTS angle and two low free-camera angles, 4 headings.</item>
    /// <item>Watch shots (90 s): while the forts fire (any staging), every frame of every cannon: the head turns nose up about the hinge, the rams match the
    /// design for the head's real tilt, the nose rule (the barrel's angle in the head never more than 12 deg over its share of the move, the 1.2 s recoil
    /// after a shot left out), and at each shot the bore's elevation (the lob) and the head's tilt.</item>
    /// <item>Nose A/B: the nose fix on / off (FortGunView.NoseWithBody) for a before / after clip.</item>
    /// </list>
    /// </summary>
    public static class OzFortGunNeckCheck
    {
        const string M = "OpenZ/V1.3/Fort Gun Neck: ";
        const float NoseLimit = 12f, RecoilSkip = 1.2f;
        static string Out { get { var p = Path.Combine(OpenZPaths.Records, "milestones", "v1_3", "fort_gun_neck"); Directory.CreateDirectory(p); return p; } }

        static List<FortGunView> Guns() => Object.FindObjectsByType<FortGunView>(FindObjectsSortMode.None).Where(v => v && v.Bound && !v.Tossed && v.HasHead).ToList();

        static Quaternion NoseUp(float deg) => Quaternion.AngleAxis(-deg, Vector3.right);   // FortGunView: nose up about the hinge line (yaw space x)

        static void Design(float sx, float deg, out Vector3 stage, out Vector3 rod, out Vector3 up)
        {
            rod = FortGunNeck.PivotAtTilt(sx, deg);
            var p0 = new Vector3(sx * FortGunNeck.RX, FortGunNeck.BY, FortGunNeck.BZ);
            var d = rod - p0; float L = d.magnitude; up = d / L;
            stage = p0 + up * (FortGunNeck.G0 + (L - FortGunNeck.L0) * 0.5f);
        }

        [MenuItem(M + "Check (Play)")]
        static void Check()
        {
            if (!Application.isPlaying || !ZGame.I) { Debug.LogWarning("[OpenZ neck] enter Play on a level with forts first"); return; }
            var guns = Guns();
            var sb = new StringBuilder($"[OpenZ neck] fort gun neck check, level {ZGame.I.level}, {guns.Count} standing fort cannon(s)\n"); bool all = guns.Count > 0;
            foreach (var g in guns)
            {
                var yaw = g.YawT; var head = g.HeadT; var nk = g.Neck; string who = $"fort #{(g.Fort != null ? g.Fort.Id : -1)} ({(g.Fort != null ? g.Fort.Owner.ToString() : "-")})";
                int old = yaw.GetComponentsInChildren<Transform>(true).Count(t => t.name.StartsWith("FortGunRam_"));
                int mine = yaw.GetComponentsInChildren<MeshRenderer>(true).Count(r => r.name.StartsWith(FortGunNeck.Prefix));
                bool ok1 = nk != null && nk.Renderers == 6 && mine == 6 && old == 0;
                sb.AppendLine(OzV11Kit.Verdict(ok1, $"{who}: neck renderers {mine} (wanted 6: trunk, head collar, 2 stages, 2 rods), old ram parts {old} (wanted 0)"));
                all &= ok1; if (nk == null) continue;
                // staged sweep of the head's tilt
                var keepR = head.localRotation; var keepP = head.localPosition; var pivot = g.tiltPivot;
                float err = 0f, scale = 0f, minOv = 1f;
                for (float deg = 0f; deg <= 30.001f; deg += 0.5f)
                {
                    var hq = NoseUp(deg); head.localRotation = hq; head.localPosition = pivot - hq * pivot;
                    nk.Pose();
                    for (int s = 0; s < 2; s++)
                    {
                        float sx = s == 0 ? -1f : 1f; Design(sx, deg, out var st, out var rd, out var up);
                        err = Mathf.Max(err, Mathf.Max((nk.Stage(s).localPosition - st).magnitude, (nk.Rod(s).localPosition - rd).magnitude));
                        err = Mathf.Max(err, (nk.Stage(s).localRotation * Vector3.up - up).magnitude);
                        scale = Mathf.Max(scale, Mathf.Max((nk.Stage(s).localScale - Vector3.one).magnitude, (nk.Rod(s).localScale - Vector3.one).magnitude));
                    }
                    minOv = Mathf.Min(minOv, nk.MinOverlap);
                }
                head.localRotation = keepR; head.localPosition = keepP; nk.Pose();
                bool ok2 = err < 1e-4f && scale < 1e-6f && minOv > 0.01f;
                sb.AppendLine(OzV11Kit.Verdict(ok2, $"{who}: head staged 0..30 deg by 0.5: rams against the design, largest error {err:0.000000} (wanted < 0.0001), scale off by {scale:0.000000}, rod in its stage at least {minOv:0.0000} mesh units ({minOv * 10.53f:0.00} m)"));
                all &= ok2;
                // paint: the trunk and the head's collar wear the gun's material and blocks
                var gunR = yaw.GetComponent<MeshRenderer>(); var bR = nk.Base.GetComponent<MeshRenderer>(); var cR = nk.Collar.GetComponent<MeshRenderer>();
                bool ok3 = gunR && bR && cR && bR.sharedMaterial == gunR.sharedMaterial && cR.sharedMaterial == gunR.sharedMaterial && bR.HasPropertyBlock() == gunR.HasPropertyBlock();
                sb.AppendLine(OzV11Kit.Verdict(ok3, $"{who}: trunk and head collar wear the gun's material {(gunR && gunR.sharedMaterial ? gunR.sharedMaterial.name : "-")} (war paint {FortGunPaint(g)}) and its blocks"));
                all &= ok3;
                Pictures(g, who, sb);
            }
            sb.AppendLine(all ? "RESULT PASS" : "RESULT FAIL");
            File.WriteAllText(Path.Combine(Out, $"neck_check_{ZGame.I.level}.txt"), sb.ToString());
            Debug.Log(sb.ToString());
        }

        static string FortGunPaint(FortGunView g) { var w = g.GetComponent<FortGunWarPaint>(); return w ? w.Shown : "-"; }

        static void Pictures(FortGunView g, string who, StringBuilder sb)
        {
            var head = g.HeadT; var nk = g.Neck; var keepR = head.localRotation; var keepP = head.localPosition; var pivot = g.tiltPivot;
            float hdg = g.Heading; int n = 0;
            var cams = new (float dist, float pitch, string name)[] { (35f, 55f, "rts55"), (18f, 25f, "free25"), (14f, 10f, "free10") };
            try
            {
                foreach (float deg in new[] { 0f, 15f, 30f })
                {
                    var hq = NoseUp(deg); head.localRotation = hq; head.localPosition = pivot - hq * pivot; nk.Pose();
                    var at = g.YawT.position + Vector3.up * 1.2f;
                    foreach (var c in cams)
                        foreach (float h in new[] { 0f, 45f, 90f, 150f })
                        {
                            OpenZShot.Shot(at, c.dist, c.pitch, hdg + 180f + h, Path.Combine(Out, $"{ZGame.I.level}_fort{(g.Fort != null ? g.Fort.Id : 0)}_tilt{deg:0}_{c.name}_h{h:0}.png"), 1280, 720);
                            n++;
                        }
                }
            }
            catch (System.Exception e) { sb.AppendLine($"      pictures stopped: {e.Message}"); }
            finally { head.localRotation = keepR; head.localPosition = keepP; nk.Pose(); }
            sb.AppendLine($"      {who}: {n} pictures (tilt 0 / 15 / 30; RTS 35 m pitch 55, free 18 m pitch 25, free 14 m pitch 10; in front, 3/4, side, rear 3/4) in {Out}");
        }

        // ------------------------------------------------------------------ watch real shots
        sealed class Seen { public FortGunView g; public int shots; public float lastShot = -99f, worstNose, worstHinge, worstPose; public int frames, moveFrames; public readonly List<string> atShots = new List<string>(); public bool shotsOk = true; }
        static readonly List<Seen> seen = new List<Seen>(); static float until;

        [MenuItem(M + "Watch Shots 90 s (Play)")]
        static void Watch()
        {
            if (!Application.isPlaying || !ZGame.I) { Debug.LogWarning("[OpenZ neck] enter Play with the forts firing (e.g. the fort gun tilt proof's staging)"); return; }
            seen.Clear(); foreach (var g in Guns()) seen.Add(new Seen { g = g, shots = g.Shots });
            until = Time.realtimeSinceStartup + 90f;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            Debug.Log($"[OpenZ neck] watching {seen.Count} fort cannon(s) for 90 s (nose with body {(FortGunView.NoseWithBody ? "ON" : "OFF")})");
        }

        static void Tick()
        {
            if (!Application.isPlaying) { EditorApplication.update -= Tick; return; }
            foreach (var s in seen)
            {
                var g = s.g; if (!g || g.Tossed || g.Neck == null) continue;
                var head = g.HeadT; float tilt = Quaternion.Angle(Quaternion.identity, head.localRotation);
                s.worstHinge = Mathf.Max(s.worstHinge, Quaternion.Angle(head.localRotation, NoseUp(tilt)));                     // nose up about x, never the other way
                for (int i = 0; i < 2; i++)
                {
                    Design(i == 0 ? -1f : 1f, tilt, out var st, out var rd, out _);
                    s.worstPose = Mathf.Max(s.worstPose, Mathf.Max((g.Neck.Stage(i).localPosition - st).magnitude, (g.Neck.Rod(i).localPosition - rd).magnitude));
                }
                if (g.Shots != s.shots)
                {
                    s.shots = g.Shots; s.lastShot = Time.time;
                    bool ok = g.BoreElevationNow > g.mortarElevation - 1.5f && g.HeadTiltNow > g.headTilt * 0.9f;
                    s.shotsOk &= ok; s.atShots.Add($"bore {g.BoreElevationNow:0.0} deg (lob {g.mortarElevation:0}), head {g.HeadTiltNow:0.0} deg{(ok ? "" : " WRONG")}");
                }
                s.frames++;
                if (Time.time - s.lastShot < RecoilSkip) continue;
                float share = g.reloadElev + (g.mortarElevation - g.RestElevation - g.headTilt - g.reloadElev) * Mathf.Clamp01(tilt / Mathf.Max(1f, g.headTilt));
                if (tilt > 0.5f || g.BarrelInHeadNow > g.reloadElev + 1f) { s.moveFrames++; s.worstNose = Mathf.Max(s.worstNose, g.BarrelInHeadNow - share); }
            }
            if (Time.realtimeSinceStartup < until) return;
            EditorApplication.update -= Tick;
            var sb = new StringBuilder($"[OpenZ neck] watch of the fort cannons, level {ZGame.I.level}, nose with body {(FortGunView.NoseWithBody ? "ON" : "OFF")}\n"); bool all = seen.Count > 0;
            foreach (var s in seen)
            {
                if (!s.g) continue;
                string who = $"fort #{(s.g.Fort != null ? s.g.Fort.Id : -1)}";
                bool fired = s.atShots.Count > 0;
                sb.AppendLine(OzV11Kit.Verdict(s.worstHinge < 0.5f, $"{who}: the head turns nose up about the hinge line (largest turn off it {s.worstHinge:0.00} deg, {s.frames} frames)"));
                sb.AppendLine(OzV11Kit.Verdict(s.worstPose < 1e-3f, $"{who}: rams on the design for the head's real tilt (largest error {s.worstPose:0.00000} mesh units)"));
                sb.AppendLine(OzV11Kit.Verdict(fired && s.worstNose <= NoseLimit, $"{who}: nose with the body: the barrel's angle in the head at most {s.worstNose:0.0} deg over its share of the move (wanted <= {NoseLimit:0}; {s.moveFrames} frames of moves)"));
                sb.AppendLine(OzV11Kit.Verdict(fired && s.shotsOk, $"{who}: {s.atShots.Count} shot(s): {(fired ? string.Join("; ", s.atShots) : "none seen, stage a target in range")}"));
                all &= s.worstHinge < 0.5f && s.worstPose < 1e-3f && fired && s.worstNose <= NoseLimit && s.shotsOk;
            }
            sb.AppendLine(all ? "RESULT PASS" : "RESULT FAIL");
            File.WriteAllText(Path.Combine(Out, $"nose_watch_{ZGame.I.level}_{(FortGunView.NoseWithBody ? "on" : "off")}.txt"), sb.ToString());
            Debug.Log(sb.ToString());
        }

        [MenuItem(M + "Nose A-B: Toggle The Nose Fix (Play)")]
        static void Toggle()
        {
            FortGunView.NoseWithBody = !FortGunView.NoseWithBody;
            Debug.Log($"[OpenZ neck] nose with body {(FortGunView.NoseWithBody ? "ON (the fix)" : "OFF (the look before)")}");
        }
    }
}
