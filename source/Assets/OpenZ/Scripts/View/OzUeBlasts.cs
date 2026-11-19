using System;
using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// Six test explosions as tiles of the dock's SPECIAL tab (UI/OzUeDock.cs), dragged or
    /// click-placed on the field like the NUKE (their palette kind is the NUKE's: always neutral, nothing placed, no stats card):
    /// <list type="bullet">
    /// <item>COLUMN: a tall fire and dust column with debris streaks, secondary pops and a rolling dust ring (the owner's frame).</item>
    /// <item>FUEL: a fuel fireball mushrooming into black smoke; the ground burns on for a few seconds.</item>
    /// <item>CHAIN: eleven small blasts walking outward on a spiral.</item>
    /// <item>GEYSER: the ground bursts: a dirt fountain, falling clods, a thick dust skirt; almost no fire.</item>
    /// <item>FLAK: an air burst: white-hot shards rain down trailing smoke, an ember shower.</item>
    /// <item>SHOCK: a blue flash, a shock ring racing over the ground with dust behind it, then a thin smoke pillar.</item>
    /// </list>
    /// Rules: every beat that hurts is ONE ordinary neutral missile of the rules (Sim/ZWorld.UeBlasts.cs PlaygroundBlast: the game's
    /// normal blast damage with its fall-off, both teams alike). The view draws each beat when the rules' own MissileExploded event of
    /// that missile arrives (<see cref="Take"/>, hooked in CombatView), so the picture, the sound and the damage stay together at every
    /// game speed and through a pause; the usual shell blast and its sound are left out for these events (<see cref="Mine"/>).
    /// Never in a level: only the Unit editor's tiles and <see cref="TestDrop"/> call <see cref="Drop"/>.
    /// </summary>
    public static class OzUeBlasts
    {
        public const int Count = 7;
        public const int Column = 0, Fuel = 1, Chain = 2, Geyser = 3, Flak = 4, Shock = 5, Airburst = 6;
        public static readonly string[] Ids = { "ueblast_column", "ueblast_fuel", "ueblast_chain", "ueblast_geyser", "ueblast_flak", "ueblast_shock", "ueblast_airburst" };
        static readonly string[] shorts = { "COLUMN", "FUEL", "CHAIN", "GEYSER", "FLAK", "SHOCK", "BURST" };
        static readonly string[] fulls = { "DUST COLUMN", "FUEL FIREBALL", "CLUSTER CHAIN", "DIRT GEYSER", "FLAK AIRBURST", "SHOCKWAVE", "WHITE AIRBURST" };
        static readonly string[] tipTitles = { "Dust column", "Fuel fireball", "Cluster chain", "Dirt geyser", "Flak airburst", "Shockwave", "White airburst" };
        static readonly string[] looks =
        {
            "A tall column of fire and dust, flying debris and a rolling dust ring",
            "A fuel fireball that rolls up into black smoke; the ground burns on",
            "Eleven small blasts walking outward one after another",
            "The ground bursts: a fountain of dirt, falling clods and a thick dust cloud",
            "A burst in the air: white-hot shards rain down trailing smoke",
            "A blue flash, a shock ring racing over the ground, then a thin smoke pillar",
            "A white star in the air: white-hot shards rain down on long white smoke trails",
        };
        /// <summary>The tile pictures (256 x 256, the EXTRAS family's import), Resources paths.</summary>
        public static readonly string[] TilePaths =
        {
            "OpenZ/UnitEditor/ue_blast_column_tile", "OpenZ/UnitEditor/ue_blast_fuel_tile", "OpenZ/UnitEditor/ue_blast_chain_tile",
            "OpenZ/UnitEditor/ue_blast_geyser_tile", "OpenZ/UnitEditor/ue_blast_flak_tile", "OpenZ/UnitEditor/ue_blast_shock_tile",
            "OpenZ/UnitEditor/ue_blast_airburst_tile",
        };
        /// <summary>At most this many test explosions run at once (a drop meanwhile is refused with its words).</summary>
        public const int MaxLive = 8;
        public const string BusyWords = "TOO MANY EXPLOSIONS AT ONCE";
        public const string PreviewHint = "Click to set it off  -  Esc cancels";

        /// <summary>One beat of the rules: a neutral blast dx, dy rule px from the drop point (8 px a metre), bursting in rule tick `tick`
        /// after the drop (100 ticks a second).</summary>
        public struct Stage { public int dx, dy, radiusPx, damage, tick; }

        // twelve directions, 30 degrees apart, x 1024 (whole numbers: the beats' places are whole rule px)
        static readonly int[] dirX = { 1024, 887, 512, 0, -512, -887, -1024, -887, -512, 0, 512, 887 };
        static readonly int[] dirY = { 0, 512, 887, 1024, 887, 512, 0, -512, -887, -1024, -887, -512 };

        public static int TypeOf(string id) => id == null ? -1 : Array.IndexOf(Ids, id);
        public static bool Is(string id) => TypeOf(id) >= 0;
        public static string Short(string id) { int t = TypeOf(id); return t >= 0 ? shorts[t] : ""; }
        public static string Full(string id) { int t = TypeOf(id); return t >= 0 ? fulls[t] : ""; }
        public static string TipTitle(string id) { int t = TypeOf(id); return t >= 0 ? tipTitles[t] + " (test explosion)" : ""; }
        public static string TipLook(string id) { int t = TypeOf(id); return t >= 0 ? looks[t] : ""; }
        public static string TipDamage(string id) => "Both teams alike: damage out to " + Mathf.RoundToInt(ReachM(TypeOf(id))) + " m, strongest in the middle";
        public static string PreviewLine(string id) => Full(id) + ": BOTH TEAMS - DAMAGE INSIDE THE RING";

        /// <summary>The beats of one drop. turn (0..11) turns the CHAIN's spiral and the FLAK's shard ring; whole numbers only.</summary>
        public static Stage[] StagesOf(int type, int turn)
        {
            turn = ((turn % 12) + 12) % 12;
            switch (type)
            {
                case Column: return new[] { S(0, 0, 80, 6000, 1) };
                case Fuel: return new[] { S(0, 0, 96, 4500, 1), S(0, 0, 56, 500, 60), S(0, 0, 56, 500, 120), S(0, 0, 56, 500, 180), S(0, 0, 56, 500, 240) };
                case Chain:
                {
                    var a = new Stage[11];
                    for (int k = 0; k < a.Length; k++)
                    {
                        int d = (turn + k * 5) % 12, r = 12 + 10 * k;                                   // 150 degrees on, 1.5 m + 1.25 m a pop: out to 14 m
                        a[k] = S(dirX[d] * r / 1024, dirY[d] * r / 1024, 36, 2200, 1 + 12 * k);
                    }
                    return a;
                }
                case Geyser: return new[] { S(0, 0, 64, 8000, 1) };
                case Flak:
                case Airburst:                                                                                  // round 3's FLAK: the same rule beats
                {
                    var a = new Stage[9];
                    a[0] = S(0, 0, 104, 2500, 1);
                    for (int k = 0; k < 8; k++)
                    {
                        int d = (turn + (k * 3) / 2) % 12, r = (k & 1) == 0 ? 60 : 84;                   // eight shard hits on two rings, 0.36 s after the burst
                        a[k + 1] = S(dirX[d] * r / 1024, dirY[d] * r / 1024, 20, 1200, 36);
                    }
                    return a;
                }
                case Shock: return new[] { S(0, 0, 48, 5000, 30), S(0, 0, 96, 3000, 38), S(0, 0, 144, 1500, 46) };   // 0.3 s of charge, then the wave in three steps
            }
            return new Stage[0];
        }
        static Stage S(int dx, int dy, int r, int dmg, int tick) => new Stage { dx = dx, dy = dy, radiusPx = r, damage = dmg, tick = tick };

        /// <summary>How far from the drop point the damage reaches (m): the preview's ring.</summary>
        public static float ReachM(int type)
        {
            float best = 0f;
            foreach (var s in StagesOf(type, 0)) best = Mathf.Max(best, (Mathf.Sqrt(s.dx * s.dx + s.dy * s.dy) + s.radiusPx) / 8f);
            return best;
        }

        // ------------------------------------------------------------------------------------------------ checks
        public static int Drops, Refusals, Beats, Unmatched, Sounds, Kicks;
        public static readonly int[] DropsByType = new int[Count];
        public static Vector3 LastAt; public static int LastType = -1; public static float LastDropAt = -100f;

        // ------------------------------------------------------------------------------------------------ the beats waiting for the rules
        sealed class Entry { public OzUeBlastFx fx; public int stage, type; public bool fired; public float firedAt, madeAt; public Vector3 at; }
        static readonly Dictionary<(int, int, int, int), List<Entry>> table = new Dictionary<(int, int, int, int), List<Entry>>();
        static readonly List<(int, int, int, int)> sweepKeys = new List<(int, int, int, int)>();

        public static bool CanDrop(out string why)
        {
            why = OzUeBlastFx.Live >= MaxLive ? BusyWords : null;
            return why == null;
        }

        /// <summary>Sets off test explosion `type` on the floor point at. False (why) without a field or while MaxLive of them run.</summary>
        public static bool Drop(ZGame g, ZWorld w, int type, Vector3 at, out string why)
        {
            why = null;
            if (g == null || w == null || type < 0 || type >= Count) { why = "NO ROOM HERE"; Refusals++; return false; }
            if (!CanDrop(out why)) { Refusals++; return false; }
            at.y = g.GroundY(at.x, at.z);
            var px = ZGame.WorldToPx(at);
            int turn = Mathf.Abs(px.x * 7 + px.y * 13 + Drops * 5) % 12;
            int seed = unchecked(px.x * 73856093 ^ px.y * 19349663 ^ (Drops + 1) * 83492791);
            var st = StagesOf(type, turn);
            var fx = OzUeBlastFx.Begin(type, at, seed, st);                                                    // null: no effect player in this scene (the damage still goes)
            Sweep();
            for (int k = 0; k < st.Length; k++)
            {
                int x = px.x + st[k].dx, y = px.y + st[k].dy;
                w.PlaygroundBlast(x, y, st[k].radiusPx, st[k].damage, st[k].tick);
                var key = (x, y, st[k].radiusPx, st[k].damage);
                if (!table.TryGetValue(key, out var list)) table[key] = list = new List<Entry>(2);
                var p = ZGame.PxToWorld(x, y); p.y = g.GroundY(p.x, p.z);
                list.Add(new Entry { fx = fx, stage = k, type = type, madeAt = Time.unscaledTime, at = p });
            }
            Drops++; DropsByType[type]++; LastAt = at; LastType = type; LastDropAt = Time.time;
            Debug.Log($"{OzUnitEditor.Tag} test explosion {fulls[type]} on ({at.x:0.0}, {at.z:0.0}): {st.Length} beat(s), damage out to {ReachM(type):0.#} m");
            return true;
        }

        /// <summary>CombatView and AudioDirector, a MissileExploded event: is it a beat of a test explosion (then the usual shell blast
        /// and its sound are left out; the kit draws and sounds it).</summary>
        public static bool Mine(ZEvent e)
        {
            if (table.Count == 0 || e.objId != -1) return false;
            return table.ContainsKey((e.x, e.y, e.radius, Mathf.RoundToInt(e.value)));
        }

        /// <summary>CombatView, a MissileExploded event: a beat of a test explosion draws itself now. True = it was one.</summary>
        public static bool Take(ZEvent e)
        {
            if (table.Count == 0 || e.objId != -1) return false;
            if (!table.TryGetValue((e.x, e.y, e.radius, Mathf.RoundToInt(e.value)), out var list)) return false;
            Entry en = null;
            for (int i = 0; i < list.Count; i++) if (!list[i].fired) { en = list[i]; break; }
            if (en == null) { Unmatched++; return true; }                                                    // its picture already ran (two events for one key): never the shell blast on top
            en.fired = true; en.firedAt = Time.unscaledTime; Beats++;
            if (en.fx) en.fx.Beat(en.stage, en.at);
            return true;
        }

        static void Sweep()
        {
            if (table.Count == 0) return;
            float now = Time.unscaledTime;
            sweepKeys.Clear();
            foreach (var kv in table)
            {
                var l = kv.Value;
                for (int i = l.Count - 1; i >= 0; i--)
                    if ((l[i].fired && now - l[i].firedAt > 1.5f) || (!l[i].fired && now - l[i].madeAt > 60f)) l.RemoveAt(i);   // a beat the rules never burst (the field was left): dropped
                if (l.Count == 0) sweepKeys.Add(kv.Key);
            }
            for (int i = 0; i < sweepKeys.Count; i++) table.Remove(sweepKeys[i]);
        }

        /// <summary>The Unity proof (Play, the Unit editor open): test explosion `type` on the floor point (x, z).</summary>
        public static bool TestDrop(int type, float x, float z)
        {
            var ed = OzUnitEditor.I;
            if (!ed || ed.Game == null || ed.World == null) return false;
            return Drop(ed.Game, ed.World, type, new Vector3(x, 0f, z), out _);
        }

        /// <summary>One line of every count, for the proof.</summary>
        public static string Report()
        {
            var z = OpenZFxPlayer.I; var ed = OzUnitEditor.I;
            return $"test explosions: drops {Drops} (column {DropsByType[0]}, fuel {DropsByType[1]}, chain {DropsByType[2]}, geyser {DropsByType[3]}, flak {DropsByType[4]}, shock {DropsByType[5]}, airburst {DropsByType[6]}), " +
                   $"refused {Refusals}, beats drawn {Beats}, double events {Unmatched}, beats waiting {Waiting}, running {OzUeBlastFx.Live}, particles emitted {OzUeBlastFx.Emitted}, " +
                   $"live particles {(z ? z.UeParticles("ueb_") : 0)}, channels {(z ? z.UeBlastChannels : 0)}, sounds played {Sounds}, kicks {Kicks}, " +
                   $"rules' blasts {(ed && ed.World != null ? ed.World.PlaygroundBlasts : 0)}";
        }

        /// <summary>Checks: beats queued whose rules' event has not come yet.</summary>
        public static int Waiting { get { int n = 0; foreach (var kv in table) foreach (var en in kv.Value) if (!en.fired) n++; return n; } }

        /// <summary>The editor closed: nothing waits, nothing runs.</summary>
        internal static void Reset()
        {
            table.Clear(); OzUeBlastFx.StopAll();
            Drops = Refusals = Beats = Unmatched = Sounds = Kicks = 0; LastType = -1; LastDropAt = -100f; LastAt = default;
            for (int i = 0; i < Count; i++) DropsByType[i] = 0;
        }
    }

    /// <summary>The tiles' drop (View/OzUePlayground.cs DropCore): nothing is placed, the explosion goes off on the floor point.</summary>
    public static partial class OzUePlayground
    {
        static UeDrop BlastDrop(ZGame g, ZWorld w, string id, Vector3 centre, bool sound, UeDrop d)
        {
            if (!OzUeBlasts.Drop(g, w, OzUeBlasts.TypeOf(id), centre, out string why))
            {
                d.why = why; Refused++;
                if (sound) AudioDirector.UI("ui_error");
                Debug.Log($"{OzUnitEditor.Tag} test explosion refused: {why}");
                return d;
            }
            d.ok = true; d.groups = 0; d.why = null;
            return d;
        }
    }

    /// <summary>The tiles' drop preview (View/OzUePlace.cs Preview): no model, the damage ring on the floor at its true size, a cross on
    /// the aim point, its metres and the explosion's name; the no-room colour and words while MaxLive explosions run.</summary>
    internal sealed partial class OzUePlaceDriver
    {
        static readonly List<Vector3> blastShape = new List<Vector3>(1);
        /// <summary>Checks: the last frame's preview (its ring in m, its words).</summary>
        internal static float BlastRingM; internal static string BlastWords;

        void BlastPreview(string id, Vector3 centre, bool ok, string why, bool armed)
        {
            OzUeBlastFx.Warm(centre);                                                                    // ready before the first drop (once per field)
            float r = OzUeBlasts.ReachM(OzUeBlasts.TypeOf(id));
            Color ring = ok ? OzUi.A(OzUi.Ink, 0.8f) : OzUi.A(NoRoom, 0.9f);
            Color text = ok ? NeutralText : NoRoom;
            OzUeDraw.Ring(centre, r, ring, 0f, 0f, 1.5f);
            OzUeDraw.Cross(centre, 10f, ring);
            OzUeDraw.Label(OzUeMarks.FarTop(centre, r), OzUeMarks.Metres(r), OzUi.A(text, 0.85f), 12f, 3f);
            string line = ok ? OzUeBlasts.PreviewLine(id) : string.IsNullOrEmpty(why) ? OzUeBlasts.BusyWords : why;
            blastShape.Clear(); blastShape.Add(centre);
            Words(centre, blastShape, r, 0.5f, 0f, 0f, armed, line, out float lineUp, out float hintUp);
            OzUeDraw.Label(centre, line, text, 12f, lineUp, true);
            if (armed) OzUeDraw.Label(centre, OzUeBlasts.PreviewHint, OzUi.A(OzUi.Ink2, 0.92f), 12f, hintUp, true);
            BlastRingM = r; BlastWords = line;
        }
    }
}
