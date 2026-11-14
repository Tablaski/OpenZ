using System;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// The Unit editor's NUKE extra. The dock tile (UI/OzUeDock.cs) is dragged or
    /// click-placed on the field like the flag and the crate (View/OzUePlace.cs, View/OzUePlayground.Nuke.cs); Call then starts the levels'
    /// own ending strike on that floor point (View/FxV2/OpenZNukeStrike.cs, its playground form: the same bomber in the player's colour, the
    /// same launch alarm, engine, fly-by, release, whistle, slow fall and exhaust; View/FxV2/OpenZNukeStrike.Playground.cs). At the impact:
    /// the levels' own blast (NukeFx: flash, mushroom, shock ring, side explosions, rumble) and the rules' blast (Sim/ZWorld.Playground.cs
    /// PlaygroundNuke): every robot, vehicle, gun, obstacle and crate whose centre is inside <see cref="KillRadiusM"/> loses all its HP,
    /// both teams and empty hardware alike (flags are immune, as to every blast), each one when the levels' shockwave ring reaches it
    /// field's game speed, 1 to 2 rule ticks behind the ring, nearest first). Nothing outside it is touched by the rules.
    /// At most one call every <see cref="CooldownSec"/> seconds: the tile shows the time left (a dark curtain going down and the seconds),
    /// a click on it, a drag or a drop meanwhile is refused ("NUKE READY IN 12 S", ui_error), and ui_unit_ready rings when it is ready.
    /// The cooldown counts the seconds the player watches (game time: it stops while the field is paused and runs the same at x1, x2 and
    /// x4); <see cref="OnRuleClock"/> = true makes it count the field's rule clock instead (x4 = four times faster).
    /// No game end, no voice plan, no slow motion, no camera move, no ending vignette: those stay the levels' (OpenZNukeStrike's playground
    /// form leaves them out).
    /// </summary>
    public static class OzUeNuke
    {
        /// <summary>The owner's limit: one nuke every 30 s.</summary>
        public const float CooldownSec = 30f;
        /// <summary>Everything whose centre is under this many metres from the aim point is destroyed (100 % damage), both teams, whoever
        /// 30 m, was 15 m (the strike's own fire zone).</summary>
        public const float KillRadiusM = 30f;
        /// <summary>1.3 final: the outer band, from <see cref="KillRadiusM"/> to this many metres: 75 % of the full HP at the inner ring down to
        /// 25 % at this edge (Sim/ZWorld.Playground.cs PlaygroundNukeDamage), both teams alike. Nothing beyond it is touched by the rules.</summary>
        public const float DamageRadiusM = 45f;
        /// <summary>The kill radius in rule pixels (8 px a metre: ZGame.PxToWorld).</summary>
        public static int KillRadiusPx => Mathf.RoundToInt(KillRadiusM * 8f);
        /// <summary>The damage band's outer edge in rule pixels.</summary>
        public static int DamageRadiusPx => Mathf.RoundToInt(DamageRadiusM * 8f);
        /// <summary>Default false: the cooldown counts game seconds (Time.time). True: the field's rule clock (ZWorld.Time).</summary>
        public static bool OnRuleClock = false;
        public const string InboundWords = "NUKE INBOUND";

        /// <summary>Checks: calls accepted, refused, impacts reached, ready chimes rung; the last impact's game time and point.</summary>
        public static int Calls, Refusals, Impacts, ReadyChimes;
        public static float LastImpactAt = -100f;
        public static Vector3 LastImpactPoint;
        /// <summary>Checks: objects alive inside the inner (kill) ring just before the last blast was queued; LastInBand: those in the outer band.</summary>
        public static int LastInside, LastInBand;

        static bool called, chimed = true;
        static double calledAt;

        static ZWorld W => OzUnitEditor.I ? OzUnitEditor.I.World : null;
        static double Now { get { if (!OnRuleClock) return Time.time; var w = W; return w != null ? w.Time : 0.0; } }

        /// <summary>Seconds until the next nuke may be called (0 = ready).</summary>
        public static float Remaining
        {
            get
            {
                if (!called) return 0f;
                double gone = Now - calledAt;
                if (gone < 0.0) { called = false; return 0f; }                                                   // a clock that started again (another field): ready
                return Mathf.Clamp(CooldownSec - (float)gone, 0f, CooldownSec);
            }
        }
        public static bool Ready => Remaining <= 0f;
        /// <summary>The share of the cooldown still to run (1 = just called, 0 = ready): the tile's curtain.</summary>
        public static float CoolShare => Remaining / CooldownSec;
        /// <summary>The whole seconds left, as the tile and the refusal show them (12 for 11.2 s).</summary>
        public static int SecondsLeft => Mathf.CeilToInt(Remaining);
        /// <summary>"NUKE READY IN 12 S".</summary>
        public static string WaitWords => "NUKE READY IN " + SecondsLeft + " S";

        /// <summary>May a nuke be called now (why = the refusal's words when not).</summary>
        public static bool CanCall(out string why)
        {
            why = Ready ? null : WaitWords;
            return why == null;
        }

        /// <summary>Calls the bomber onto the floor point at (the drop's centre). False (why) while the cooldown runs or without a field.</summary>
        public static bool Call(ZGame g, ZWorld w, Vector3 at, out string why)
        {
            if (g == null || w == null) { why = "NO ROOM HERE"; Refusals++; return false; }
            if (!CanCall(out why)) { Refusals++; return false; }
            called = true; calledAt = Now; chimed = false; Calls++;
            at.y = g.GroundY(at.x, at.z);
            var px = ZGame.WorldToPx(at);
            int r = DamageRadiusPx; var world = w; var point = at;
            Action boom = () => Impact(world, px.x, px.y, r, point);
            if (!OpenZNukeStrike.BeginPlayground(at, boom)) boom();                                             // strikes off: the blast at once
            return true;
        }

        static void Impact(ZWorld w, int x, int y, int r, Vector3 at)
        {
            var ed = OzUnitEditor.I;
            if (!ed) return;                                                                                     // the editor closed while the bomb fell
            Impacts++; LastImpactAt = Time.time; LastImpactPoint = at;
            if (ed.World == w)
            {
                LastInside = Inside(w, x, y, KillRadiusPx);                                                     // the ones the inner ring destroys (r = the band's edge)
                LastInBand = Inside(w, x, y, r) - LastInside;
                var g = ed.Game;
                LastPace = g != null ? Mathf.Max(0.05f, g.gameSpeed) : 1f;
                w.PlaygroundNuke(x, y, r, LastPace, KillRadiusPx);                                                           // the rules' wave: all the HP of everything in the ring, each when the ring reaches it (its clock = the ring's x the game speed)
            }
            if (!OzUeMushroom.Blast(at)) NukeFx.Trigger(at);
            Debug.Log($"{OzUnitEditor.Tag} NUKE impact at ({at.x:0.0}, {at.z:0.0}): {LastInside} object(s) inside {KillRadiusM:0} m (destroyed), {LastInBand} in the band to {DamageRadiusM:0} m (damaged), the wave at x{LastPace:0.##}");
            AfterImpact?.Invoke();
        }

        /// <summary>Checks: the game speed the last wave started with (rule seconds per second of the ring).</summary>
        public static float LastPace = 1f;
        /// <summary>Checks only (View/OzUeNukeCheck.cs): called at the end of the impact frame's Impact, after the rules' wave was queued and
        /// the levels' blast started (pause or CLEAR FIELD exactly at the impact). Null in play.</summary>
        public static Action AfterImpact;

        /// <summary>
        /// rules' NUKE blast has no MissileExploded event and no attacker, so CombatView.Death would throw its robots from a random point
        /// beside them. For a robot that dies by a blast within <see cref="ThrowWindowSec"/> of the last NUKE impact and inside its ring
        /// (+2 m), this gives the level ending's own throw point (OpenZEnding.Release: the bomb's point jittered 0.8 m, 2.5 m under the
        /// robot), so it flies out and up, away from the bomb, both teams alike. Only the editor's NUKE counts Impacts: in a level this
        /// is one int compare and false.</summary>
        public static bool ThrowFrom(Vector3 p, out Vector3 from)
        {
            from = p;
            if (Impacts == 0 || Time.time - LastImpactAt > ThrowWindowSec || !OzUnitEditor.I) return false;
            float dx = p.x - LastImpactPoint.x, dz = p.z - LastImpactPoint.z, reach = DamageRadiusM + 2f;
            if (dx * dx + dz * dz > reach * reach) return false;
            from = LastImpactPoint + UnityEngine.Random.insideUnitSphere * 0.8f; from.y = p.y - 2.5f;          // = OpenZEnding.Release
            Thrown++;
            return true;
        }
        /// <summary>Game seconds after the NUKE impact in which its rule deaths are thrown from the bomb: the ring's time to the ring's
        /// edge + 2 m (the last of the wave's deaths; at every game speed the rules' wave keeps the ring's pace) + the old 2 s (the deaths
        /// reach the view in the next frames; a paused field holds Time.time).</summary>
        public static float ThrowWindowSec => (float)ZWorld.NukeWaveTimeAt(DamageRadiusM + 2f) + 2f;
        /// <summary>Checks: robots thrown from the bomb by <see cref="ThrowFrom"/>.</summary>
        public static int Thrown;

        /// <summary>Objects with HP standing with their centre inside the ring (as ProcessMissileDamage measures it).</summary>
        public static int Inside(ZWorld w, int x, int y, int r)
        {
            if (w == null) return 0;
            int n = 0;
            foreach (var o in w.Objects)
            {
                if (o.Destroyed || o.KillAt >= 0 || o.MaxHealth <= 0) continue;
                int dx = x - o.CenterX, dy = y - o.CenterY;
                if ((float)Math.Sqrt((float)(dx * dx + dy * dy)) < r) n++;
            }
            return n;
        }

        /// <summary>Once a frame (the dock's Tick, also while the panels are hidden): the ready chime when the cooldown has run out.</summary>
        internal static void TickReady()
        {
            if (chimed || !called || !Ready) return;
            chimed = true; ReadyChimes++;
            AudioDirector.UI("ui_unit_ready");
            Debug.Log($"{OzUnitEditor.Tag} NUKE ready again");
        }

        /// <summary>The editor closed: the next session starts ready.</summary>
        internal static void Reset()
        {
            called = false; chimed = true; calledAt = 0.0;
            Calls = Refusals = Impacts = ReadyChimes = LastInside = Thrown = 0; LastImpactAt = -100f; LastImpactPoint = default; LastPace = 1f;
        }
    }
}
