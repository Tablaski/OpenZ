#if UNITY_EDITOR
// OpenZ (GPL-3.0)
// 'OpenZ/1.5/Checks/Capture Dust Ring/...' (editor only). Counts + frames at the RTS camera.
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// A37: no exp_v2_6 SHOCK ground wave at a fort TAKEN by a unit walking in (the nuke strike plays its fall); everything else as before. Each case
    /// from a fresh Play in a level (each one ends the match):
    /// <list type="number">
    /// <item>capture, the fix on: a grunt squad of ours is put 3 tiles out of the enemy fort's door and sent in with the rules' own ENTER_FORT order
    /// (as GateBattery.WalkIntoFort; a new squad if it dies, 3 tries). Want: FortEntered seen; SHOCK on a fort part 0; OzCaptureNoShock.Skipped =
    /// TakenFalls &gt;= 1; the bomb lands (OpenZNukeStrike.LastImpactTime); A33 unchanged: GEYSER 0, the 1.4 blast = the cannons thrown.</item>
    /// <item>capture, the fix OFF (A/B; the switch is put back when the case ends): want SHOCK on a fort part &gt;= 1 (the ring of exe 15g), Skipped 0.</item>
    /// <item>control, a fort falls WITHOUT a capture (OpenZVictory.DebugFortFalls(true), the gate battery's kill): want SHOCK on a fort part &gt;= 1
    /// (only the capture is gated), TakenFalls 0, Skipped 0, the bomb lands.</item>
    /// </list>
    /// Frames: the RTS camera flies to the enemy fort (RtsCamera.FlyTo, its own zoom and angle) 1.5 s before; a frame every 1 s while the squad walks
    /// (at most 12), every 0.1 s for 3 s from the fall (the window of the ring), every 1 s until the bomb lands, every 0.25 s for 4 s after it.
    /// Report + frames: the kit's proof folder.
    /// </summary>
    public sealed class OzCaptureDustRingCheck : MonoBehaviour
    {
        public const string Tag = "[OpenZ 1.5 Capture Dust Ring]";
        public enum Case { Capture = 1, CaptureFixOff = 2, FallNoCapture = 3 }
        public const float LeadS = 1.5f, WaitFallS = 75f, WaitImpactS = 45f, RingS = 3f, AfterImpactS = 4f, ResendS = 4f;
        public const float DenseEveryS = 0.1f, SparseEveryS = 1f, ImpactEveryS = 0.25f;
        public const int Tries = 3, MaxWalkFrames = 12, OutTiles = 3;
        public static bool Running { get; private set; }
        public static string Report { get; private set; } = "no run yet";
        Case which; string folder; bool on0 = true, switched;
        int fortShocks, otherShocks; string firstFortShock = "";
        readonly StringBuilder md = new StringBuilder();

        public static void Begin(Case c, string folder)
        {
            if (Running || !ZGame.I || ZGame.I.World == null) { Debug.LogWarning(Tag + " enter Play in a level first"); return; }
            if (ZGame.I.World.GameOver) { Debug.LogWarning(Tag + " the match is over: enter Play again (every case ends the match)"); return; }
            var k = ZGame.I.gameObject.AddComponent<OzCaptureDustRingCheck>(); k.which = c; k.folder = folder;
        }

        void Start() { Running = true; Application.logMessageReceived += OnLog; StartCoroutine(Run()); }
        void OnDestroy() { Application.logMessageReceived -= OnLog; if (switched) OzCaptureNoShock.On = on0; Running = false; }

        void OnLog(string line, string stack, LogType t)
        {
            if (line == null || !line.StartsWith(OzExpInGame.Tag + " building: exp_v2_6")) return;
            if (line.Contains(" on fort_")) { fortShocks++; if (firstFortShock.Length == 0) firstFortShock = line; }
            else otherShocks++;
        }

        static float Now => Time.realtimeSinceStartup;

        static ZObject Send(ZGame G, ZWorld W, ZObject fort, Team me)
        {
            if (!fort.GetCreationMovePoint(out int ex, out int ey)) return null;
            float ox = ex - fort.CenterX, oy = ey - fort.CenterY, ol = Mathf.Sqrt(ox * ox + oy * oy);
            if (ol < 1f) { ox = 0f; oy = 1f; ol = 1f; }
            ox /= ol; oy /= ol;                                                                               // out of the door
            int cx = Mathf.RoundToInt((ex + ox * OutTiles * 16f) / 16f), cy = Mathf.RoundToInt((ey + oy * OutTiles * 16f) / 16f);
            if (!OzCheatSpawner.FreeTile(W, cx, cy, 1, true, out int tx, out int ty)) return null;
            var s = W.CreateRobotGroup("grunt", tx * 16, ty * 16, me);
            if (s == null) return null;
            G.SpawnUnitView(s); foreach (var m in s.Minions) G.SpawnUnitView(m);
            Order(W, s, fort, me);
            return s;
        }

        static bool Order(ZWorld W, ZObject s, ZObject fort, Team me) =>
            W.SendWaypoints(me, s, new[] { new ZWaypoint { mode = WpMode.EnterFort, refId = fort.Id, x = fort.CenterX, y = fort.CenterY } });

        static bool Alive(ZObject s) => s != null && ((!s.Destroyed && s.KillAt < 0) || s.Minions.Any(m => !m.Destroyed && m.KillAt < 0));

        IEnumerator Run()
        {
            var G = ZGame.I; var W = G.World; var rts = FindFirstObjectByType<RtsCamera>(); var me = G.player;
            int n = (int)which;
            bool capture = which != Case.FallNoCapture;
            string name = which == Case.Capture ? "capture, the fix on" : which == Case.CaptureFixOff ? "capture, the fix OFF (A/B)" : "control: a fort falls without a capture";
            if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
            on0 = OzCaptureNoShock.On;
            if (which == Case.CaptureFixOff) { OzCaptureNoShock.On = false; switched = true; }
            md.AppendLine($"# Capture dust ring check, case {n}: {name} ({System.DateTime.Now:yyyy-MM-dd HH:mm})").AppendLine();
            md.AppendLine($"level {G.level}, human {me}, PLAY AS BLUE {OzPlayBlue.Active}; switches: OzCaptureNoShock.On {OzCaptureNoShock.On} (was {on0}), AllStrikeFalls {OzCaptureNoShock.AllStrikeFalls}, "
                          + $"OzExpInGame.BuildingOn {OzExpInGame.BuildingOn}, OpenZNukeStrike.Enabled {OpenZNukeStrike.Enabled}, FortGunView.GeyserOnlyStanding {FortGunView.GeyserOnlyStanding}").AppendLine();
            var fort = W.Objects.FirstOrDefault(o => o.IsFort && !o.Destroyed && o.Owner != me && o.Owner != Team.Neutral && o.GetCreationMovePoint(out _, out _));
            if (fort == null) { md.AppendLine("FAIL: no standing enemy fort with a door in this level"); Finish(false); yield break; }
            md.AppendLine($"the enemy fort: {fort.Type} #{fort.Id} ({fort.Owner})");
            if (rts) rts.FlyTo(G.CenterWorld(fort, false));
            float lead = Now; while (Now - lead < LeadS) yield return null;
            int taken0 = OzCaptureNoShock.TakenFalls, skip0 = OzCaptureNoShock.Skipped, tosses0 = FortGunView.Tosses, megas0 = OzExpInGame.Megas, old0 = FortGunView.WithFort14;
            float impact0 = OpenZNukeStrike.LastImpactTime;
            fortShocks = otherShocks = 0; firstFortShock = "";
            float start = Now, fallAt = -1f, impactAt = -1f, sentAt = Now, next = Now;
            int frames = 0, walkFrames = 0, tries = 0; bool entered = false; ZObject squad = null;
            if (capture)
            {
                squad = Send(G, W, fort, me); tries++;
                md.AppendLine(squad != null ? $"squad {tries}: grunt #{squad.Id} ({1 + squad.Minions.Count} robots) by the door, ENTER_FORT sent" : "squad 1: NOT made (no free tile by the door)");
            }
            else md.AppendLine($"the enemy fort falls: OpenZVictory.DebugFortFalls(true) = {OpenZVictory.DebugFortFalls(true)} fort part(s)");
            while (true)
            {
                yield return null;                                                                            // after every Update: this frame's events are still listed (CombatView clears them in LateUpdate)
                float now = Now;
                if (fallAt < 0f)
                {
                    foreach (var e in W.Events)
                        if (e.type == ZEventType.FortEntered && !entered)
                        {
                            entered = true; fallAt = now;
                            md.AppendLine($"FortEntered: unit #{e.objId} walked into fort #{e.otherId} at t={W.Time:0.0} s, {now - start:0.0} s after the order");
                        }
                    if (fallAt < 0f && fort.Destroyed) { fallAt = now; md.AppendLine($"the fort fell at t={W.Time:0.0} s, {now - start:0.0} s after the trigger{(capture ? " (no FortEntered seen)" : "")}"); }
                    if (fallAt < 0f && capture)
                    {
                        if (!Alive(squad) && tries < Tries)
                        {
                            squad = Send(G, W, fort, me); tries++; sentAt = now;
                            md.AppendLine(squad != null ? $"squad {tries}: grunt #{squad.Id} by the door, ENTER_FORT sent (the one before is gone)" : $"squad {tries}: NOT made");
                        }
                        else if (Alive(squad) && !squad.Destroyed && now - sentAt > ResendS && (squad.Waypoints.Count == 0 || squad.Waypoints[0].mode != WpMode.EnterFort))
                        { sentAt = now; Order(W, squad, fort, me); }
                    }
                    if (fallAt < 0f && now - start > WaitFallS) break;
                }
                if (impactAt < 0f && OpenZNukeStrike.LastImpactTime > impact0 + 0.001f) impactAt = now;
                if (impactAt >= 0f && now - impactAt > AfterImpactS) break;
                if (fallAt >= 0f && impactAt < 0f && now - fallAt > WaitImpactS) break;
                if (string.IsNullOrEmpty(folder) || now < next) continue;
                string shot;
                if (fallAt < 0f)
                {
                    next = now + SparseEveryS;
                    if (walkFrames >= MaxWalkFrames) continue;
                    walkFrames++; shot = $"case{n}_walk_{now - start:00.0}s";
                }
                else if (impactAt < 0f && now - fallAt <= RingS) { next = now + DenseEveryS; shot = $"case{n}_fall_{now - fallAt:00.0}s"; }
                else if (impactAt < 0f) { next = now + SparseEveryS; shot = $"case{n}_wait_{now - fallAt:00.0}s"; }
                else { next = now + ImpactEveryS; shot = $"case{n}_impact_{now - impactAt:00.0}s"; }
                ScreenCapture.CaptureScreenshot(Path.Combine(folder, shot.Replace(',', '.') + ".png")); frames++;
            }
            int taken = OzCaptureNoShock.TakenFalls - taken0, skipped = OzCaptureNoShock.Skipped - skip0, tosses = FortGunView.Tosses - tosses0,
                megas = OzExpInGame.Megas - megas0, old = FortGunView.WithFort14 - old0;
            md.AppendLine().AppendLine($"the fort fell: {(fallAt >= 0f ? $"yes, {fallAt - start:0.0} s after the trigger" : $"NO within {WaitFallS:0} s")}; "
                                       + $"the bomb landed: {(impactAt >= 0f && fallAt >= 0f ? $"yes, {impactAt - fallAt:0.0} s after the fall" : "NO")}; frames {frames}");
            md.AppendLine().AppendLine("| count (this case) | got | want |").AppendLine("|---|---|---|");
            bool ok = fallAt >= 0f;
            ok &= Row("the nuke strike's bomb landed (OpenZNukeStrike.LastImpactTime)", impactAt >= 0f ? 1 : 0, impactAt >= 0f, "1 (the nuke ending as before)");
            if (which == Case.Capture)
            {
                ok &= Row("FortEntered (a unit walked in)", entered ? 1 : 0, entered, "1");
                ok &= Row("fort parts that fell taken (OzCaptureNoShock.TakenFalls)", taken, taken >= 1, ">= 1");
                ok &= Row("exp_v2_6 SHOCK on a fort part (the dust ring)", fortShocks, fortShocks == 0, "0");
                ok &= Row("SHOCKs left out (OzCaptureNoShock.Skipped)", skipped, skipped == taken && skipped >= 1, "= the taken fort parts");
            }
            else if (which == Case.CaptureFixOff)
            {
                ok &= Row("FortEntered (a unit walked in)", entered ? 1 : 0, entered, "1");
                ok &= Row("fort parts that fell taken (OzCaptureNoShock.TakenFalls)", taken, taken >= 1, ">= 1");
                ok &= Row("exp_v2_6 SHOCK on a fort part (the dust ring, back with the switch off)", fortShocks, fortShocks >= 1, ">= 1");
                ok &= Row("SHOCKs left out (OzCaptureNoShock.Skipped)", skipped, skipped == 0, "0");
            }
            else
            {
                Row("FortEntered", entered ? 1 : 0, true, "info (0: no capture)");
                ok &= Row("fort parts that fell taken (OzCaptureNoShock.TakenFalls)", taken, taken == 0, "0");
                if (OzCaptureNoShock.AllStrikeFalls)                                      // lead decision 16 Nov round 11: a strike fall without a capture drops it too
                {
                    ok &= Row("exp_v2_6 SHOCK on a fort part (AllStrikeFalls on: a fall before the nuke drops it too)", fortShocks, fortShocks == 0, "0");
                    ok &= Row("SHOCKs left out (OzCaptureNoShock.Skipped)", skipped, skipped >= 1, ">= 1");
                }
                else
                {
                    ok &= Row("exp_v2_6 SHOCK on a fort part (a fall without a capture keeps it)", fortShocks, fortShocks >= 1, ">= 1");
                    ok &= Row("SHOCKs left out (OzCaptureNoShock.Skipped)", skipped, skipped == 0, "0");
                }
            }
            Row("exp_v2_6 SHOCK on the loser's other buildings (unchanged)", otherShocks, true, "info");
            Row("megacannons thrown", tosses, true, "info");
            ok &= Row("GEYSER exp_v2_4 (A33: none with its fort)", megas, megas == 0, "0");
            ok &= Row("the 1.4 blast with its fort (A33: FortGunView.WithFort14)", old, old == tosses, "= the cannons thrown");
            md.AppendLine().AppendLine("first SHOCK on a fort part: " + (firstFortShock.Length > 0 ? firstFortShock : "none"));
            md.AppendLine("OzCaptureNoShock: " + (OzCaptureNoShock.LastLine.Length > 0 ? OzCaptureNoShock.LastLine : "no line"));
            Finish(ok);
        }

        bool Row(string what, int got, bool ok, string want) { md.AppendLine($"| {what} | {got} | {want}{(ok ? "" : " **FAIL**")} |"); return ok; }

        void Finish(bool ok)
        {
            if (switched) { OzCaptureNoShock.On = on0; switched = false; md.AppendLine().AppendLine($"OzCaptureNoShock.On put back to {on0}"); }
            md.AppendLine().AppendLine($"case {(int)which}: {(ok ? "PASS" : "FAIL")}");
            Report = md.ToString();
            if (!string.IsNullOrEmpty(folder)) File.WriteAllText(Path.Combine(folder, $"case{(int)which}_report.md"), Report);
            Debug.Log(Tag + "\n" + Report);
            Destroy(this);
        }
    }
}
#endif
