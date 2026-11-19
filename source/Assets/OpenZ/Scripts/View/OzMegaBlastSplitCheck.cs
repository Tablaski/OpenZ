#if UNITY_EDITOR
// OpenZ (GPL-3.0)
// 'OpenZ/1.5/Checks/Megacannon Blast Split/...' (editor only). Counts + frames at the RTS camera.
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// A33: the megacannon's GEYSER (exp_v2_4) + COLUMN (exp_v2_1) only when it is shot off a STANDING fort; with its fort the 1.4 blast (the 2.4
    /// explosion, the toss sound, run 9's MegaBlast; no DeathBlast). Three cases, each from a running level (the game's own paths, nothing faked in
    /// the view):
    /// <list type="number">
    /// <item>shot off a standing fort: OUR fort's cannon (so the enemy's stays for case 3 in the same run) takes the sim's own gun hit
    /// (ZObject.FortGunTakeHit, its whole health): want GEYSER 1, COLUMN 1, the 1.4 blast 0, MegaBlast 0, DeathBlast 0.</item>
    /// <item>with its fort, the normal fall: our fort falls (OpenZVictory.DebugFortFalls(false), the gate battery's kill): the gun goes with the
    /// impact. Want GEYSER 0, COLUMN 0, the 1.4 blast = the cannons thrown (1 per fort), MegaBlast the same, DeathBlast 0. Ends the match.</item>
    /// <item>with its fort, the nuke ending's beat: the enemy fort falls (DebugFortFalls(true)); the nuke intro's beat blows its cannon off before
    /// the talking scene (View/FortGunView.Outro.cs). Want the beat ran (OutroBlasts +1), GEYSER 0, COLUMN 0, the 1.4 blast 1, DeathBlast 0. The
    /// owner's case is the BLUE win (deploy with PLAY AS BLUE); as red the same beat runs before the red-button transmission. Ends the match.</item>
    /// </list>
    /// Frames: the RTS camera flies to the fort (RtsCamera.FlyTo, its own zoom and angle) 1.5 s before; a frame every 1 s until the cannon leaves
    /// its ring (case 3: the ending's camera glide takes over, as in the game), then every 0.1 s for 4 s. Report + frames: the kit's proof folder.
    /// </summary>
    public sealed class OzMegaBlastSplitCheck : MonoBehaviour
    {
        public const string Tag = "[OpenZ 1.5 Megacannon Blast Split]";
        public enum Case { ShotOff = 1, FortFalls = 2, NukeOutro = 3 }
        public const float LeadS = 1.5f, WaitTossS = 60f, AfterS = 4f, DenseEveryS = 0.1f, SparseEveryS = 1f;
        public static bool Running { get; private set; }
        public static string Report { get; private set; } = "no run yet";
        Case which; string folder;
        readonly StringBuilder md = new StringBuilder();

        public static void Begin(Case c, string folder)
        {
            if (Running || !ZGame.I || ZGame.I.World == null) { Debug.LogWarning(Tag + " enter Play in a level first"); return; }
            if (ZGame.I.World.GameOver) { Debug.LogWarning(Tag + " the match is over: enter Play again (cases 2 and 3 end the match)"); return; }
            var k = ZGame.I.gameObject.AddComponent<OzMegaBlastSplitCheck>(); k.which = c; k.folder = folder;
        }

        void Start() { Running = true; StartCoroutine(Run()); }
        void OnDestroy() { Running = false; }

        static FortGunView Standing(bool ours)
        {
            var me = ZGame.I.player;
            return FindObjectsByType<FortGunView>(FindObjectsSortMode.None).FirstOrDefault(v => v && v.Bound && !v.Tossed && v.Fort != null
                && !FortFallAtGameOver.Fallen(v.Fort) && !v.Fort.FortGunDestroyed
                && (ours ? v.Fort.Owner == me : v.Fort.Owner != me && v.Fort.Owner != Team.Neutral));
        }

        static float Now => Time.realtimeSinceStartup;

        IEnumerator Run()
        {
            var G = ZGame.I; var rts = FindFirstObjectByType<RtsCamera>();
            int n = (int)which;
            string name = which == Case.ShotOff ? "shot off a standing fort" : which == Case.FortFalls ? "with its fort: the normal fall (our fort)" : "with its fort: the nuke ending's beat (the enemy fort)";
            if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
            md.AppendLine($"# Megacannon blast split check, case {n}: {name} ({System.DateTime.Now:yyyy-MM-dd HH:mm})").AppendLine();
            md.AppendLine($"level {G.level}, human {G.player}, PLAY AS BLUE {OzPlayBlue.Active}; switches: GeyserOnlyStanding {FortGunView.GeyserOnlyStanding}, MegaOn {OzExpInGame.MegaOn}, MegaBlastOn {FortGunView.MegaBlastOn}, DeathBlastOn {FortGunView.DeathBlastOn}, OutroBlastOn {FortGunView.OutroBlastOn}").AppendLine();
            var gun = Standing(which != Case.NukeOutro);
            if (!gun)
            {
                md.AppendLine($"FAIL: no mega cannon standing on {(which == Case.NukeOutro ? "an enemy" : "our")} standing fort (shot off earlier, or the level has none)");
                Finish(false); yield break;
            }
            var fort = gun.Fort;
            md.AppendLine($"the cannon: fort #{fort.Id} ({fort.Owner}, {fort.Type})");
            if (rts) rts.FlyTo(G.CenterWorld(fort, false));
            float lead = Now; while (Now - lead < LeadS) yield return null;
            int tosses0 = FortGunView.Tosses, megas0 = OzExpInGame.Megas, cols0 = OzExpInGame.MegaColumns, old0 = FortGunView.WithFort14, geys0 = FortGunView.StandingGeysers,
                mb0 = FortGunView.MegaBlasts, db0 = FortGunView.DeathBlasts, left0 = FortGunView.DeathBlastLeftToFort, ob0 = FortGunView.OutroBlasts, beats0 = FortGunView.OutroBeats;
            float start = Now;
            switch (which)
            {
                case Case.ShotOff:
                    if (!ZWorld.FortGunDeviation || !ZWorld.FortMortarDeviation) { md.AppendLine("FAIL: the fort gun rules are off (ZWorld.FortGunDeviation / ZObject.FortMortarDeviation): no gun can be shot off"); Finish(false); yield break; }
                    fort.FortGunTakeHit(fort.FortGunHealth);                                                     // the sim's own path: an enemy hit takes the gun's whole health
                    md.AppendLine($"shot off: ZObject.FortGunTakeHit({FortGunHealthLeft(fort)} left after), FortGunDestroyed {fort.FortGunDestroyed}, the fort stands (destroyed {fort.Destroyed})");
                    break;
                case Case.FortFalls: md.AppendLine($"our fort falls: OpenZVictory.DebugFortFalls(false) = {OpenZVictory.DebugFortFalls(false)} fort part(s)"); break;
                case Case.NukeOutro: md.AppendLine($"the enemy fort falls: OpenZVictory.DebugFortFalls(true) = {OpenZVictory.DebugFortFalls(true)} fort part(s)"); break;
            }
            float tossAt = -1f, next = Now; int frames = 0;
            while (true)
            {
                yield return null;
                float now = Now;
                if (tossAt < 0f && gun.Tossed) tossAt = now;
                if (tossAt < 0f && now - start > WaitTossS) break;
                if (tossAt >= 0f && now - tossAt > AfterS) break;
                if (now < next || string.IsNullOrEmpty(folder)) continue;
                next = now + (tossAt >= 0f ? DenseEveryS : SparseEveryS);
                string shot = tossAt >= 0f ? $"case{n}_after_toss_{now - tossAt:00.0}s" : $"case{n}_before_{now - start:00.0}s";
                ScreenCapture.CaptureScreenshot(Path.Combine(folder, shot.Replace(',', '.') + ".png")); frames++;
            }
            int tosses = FortGunView.Tosses - tosses0, megas = OzExpInGame.Megas - megas0, cols = OzExpInGame.MegaColumns - cols0, old = FortGunView.WithFort14 - old0,
                geys = FortGunView.StandingGeysers - geys0, mb = FortGunView.MegaBlasts - mb0, db = FortGunView.DeathBlasts - db0, left = FortGunView.DeathBlastLeftToFort - left0,
                ob = FortGunView.OutroBlasts - ob0, beats = FortGunView.OutroBeats - beats0;
            md.AppendLine().AppendLine($"the cannon left its ring: {(tossAt >= 0f ? $"yes, {tossAt - start:0.00} s after the trigger" : $"NO within {WaitTossS:0} s")}; frames {frames}");
            md.AppendLine().AppendLine("| count (this case) | got | want |").AppendLine("|---|---|---|");
            bool ok = tossAt >= 0f;
            if (which == Case.ShotOff)
            {
                ok &= Row("GEYSER exp_v2_4 (OzExpInGame.Megas)", megas, megas == 1, "1");
                ok &= Row("COLUMN exp_v2_1 (OzExpInGame.MegaColumns)", cols, cols == 1, "1");
                ok &= Row("GEYSER off a standing fort (FortGunView.StandingGeysers)", geys, geys == 1, "1");
                ok &= Row("the 1.4 blast with its fort (FortGunView.WithFort14)", old, old == 0, "0");
                ok &= Row("run 9's MegaBlast", mb, mb == 0, "0 (left out while the GEYSER plays)");
                ok &= Row("1.5 DeathBlast", db, db == 0, "0 (left out while the GEYSER plays)");
            }
            else
            {
                ok &= Row("cannons thrown", tosses, tosses >= 1, ">= 1 (one per fallen fort)");
                ok &= Row("GEYSER exp_v2_4 (OzExpInGame.Megas)", megas, megas == 0, "0");
                ok &= Row("COLUMN exp_v2_1 (OzExpInGame.MegaColumns)", cols, cols == 0, "0");
                ok &= Row("the 1.4 blast with its fort (FortGunView.WithFort14: the 2.4 explosion + the toss sound + MegaBlast)", old, old >= 1 && old == tosses, "= cannons thrown (1 per fort)");
                ok &= Row("run 9's MegaBlast (part of the 1.4 blast)", mb, mb == old, "= the 1.4 blasts");
                ok &= Row("1.5 DeathBlast", db, db == 0, "0 (left to the fort's fall)");
                Row("DeathBlast left to the fort's fall", left, true, "info");
                if (which == Case.NukeOutro) ok &= Row("the ending's beat blew a cannon off (FortGunView.OutroBlasts)", ob, ob >= 1, ">= 1 (beats run " + beats + ")");
                else Row("the ending's beat (none expected on our own fall)", ob, true, "info");
            }
            md.AppendLine().AppendLine(OzExpInGame.LastLine).AppendLine(FortGunView.OutroBlastReport());
            Finish(ok);
        }

        static int FortGunHealthLeft(ZObject f) => f.FortGunHealth;

        bool Row(string what, int got, bool ok, string want) { md.AppendLine($"| {what} | {got} | {want}{(ok ? "" : " **FAIL**")} |"); return ok; }

        void Finish(bool ok)
        {
            md.AppendLine().AppendLine($"case {(int)which}: {(ok ? "PASS" : "FAIL")}");
            Report = md.ToString();
            if (!string.IsNullOrEmpty(folder)) File.WriteAllText(Path.Combine(folder, $"case{(int)which}_report.md"), Report);
            Debug.Log(Tag + "\n" + Report);
            Destroy(this);
        }
    }
}
#endif
