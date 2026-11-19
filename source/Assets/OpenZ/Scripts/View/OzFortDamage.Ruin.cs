// OpenZ (GPL-3.0)
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// View only: no Sim file, no rule, nothing the rules read.
    ///
    /// Before: a fallen fort (destroyed in the sim, or dropped by the game-over rule, FortFallAtGameOver.Fallen) stands until the nuke lands
    /// (OpenZEnding / OpenZNukeStrike: 8-16 s of talk on a won battle), but the frame it fell OzFortDamage cleared every mark, rim and rubble
    /// chunk, so it stood clean; and the outro blast (FortGunView.Outro.cs) threw the cannon off without the
    /// wrecked mount (FortGunMount is built only for a cannon shot off a STANDING fort), so its socket was the pedestal's tidy dark disc.
    ///
    /// Now:
    /// <list type="bullet">
    /// <item><b>A fallen fort keeps its look while its body stands</b>: the damage it showed when it fell is held (no clear, no new marks
    /// from the sim's health), and it is cleared exactly as before (marks gone, edge pieces and rubble pop out) on the frame its body goes:
    /// the collapse (BuildingBreaker.Collapsed, the body's renderers off), under the nuke.</item>
    /// <item><b>The cannon blown off = heavy ruin</b>: when the ending's beat blows a fallen fort's cannon off (OutroBlastBeat -> StartToss),
    /// FortGunView builds the modelled wrecked mount (FortGunMount: the broken pedestal ring, the pit with snapped rebar, the twisted slewing
    /// ring, slab pieces and its soot-and-cracks mark on the roof plate, its fire and smoke) and calls <see cref="Ruin(int, Vector3, string)"/>:
    /// the fort's damage goes to 1 (the heavy ruin stage, the existing stage art: breaches, broken parapets, black walls, fires, smoke
    /// columns, sparks), eased in over about <see cref="RuinEaseSeconds"/> s under the blast's fireball. A fort whose cannon was shot off
    /// before it fell (its wrecked mount already on the roof) turns to the heavy ruin the moment it falls.</item>
    /// <item><b>The socket burns</b> for the rest of the ending: <see cref="SocketFires"/> small fires round the broken socket (FxManager.Burn,
    /// the breach fires' look and light) and a dark column of the game's lit smoke flipbook (m12_smoke, as the breach columns) rising from it,
    /// on top of the mount's own fire and smoke; until the collapse.</item>
    /// <item><b>The sweep is not a wall of noise</b>: for <see cref="RuinHushSeconds"/> s after the blast the stage sweep's crack / crumble /
    /// ignite sounds are left to the blast's boom, and no small chips are thrown (the blast throws its own debris); the falling parts are
    /// held to the existing RubbleCap.</item>
    /// </list>
    /// Nothing changes for a fort that has not fallen. Switch <see cref="RuinOn"/> (off = exactly as before this kit).
    /// </summary>
    public partial class OzFortDamage
    {
        /// <summary>Switch: a fallen fort keeps its look while it stands, a blown-off cannon turns it to the heavy ruin (on).</summary>
        public static bool RuinOn = true;
        /// <summary>How fast the ruin eases in (s, as EaseSeconds); how long the sweep's own sounds and chips are left out (s).</summary>
        public static float RuinEaseSeconds = 0.5f, RuinHushSeconds = 1.5f;
        /// <summary>The burning socket: fires round it, their distance from its centre (m) and size, a beat every SocketEvery s, the smoke column's
        /// first puff after the blast (s), its start over the roof (m).</summary>
        public static int SocketFires = 3;
        public static float SocketFireR = 1.7f, SocketFireSize = 1.25f, SocketEvery = 0.3f, SocketColumnFrom = 0.8f, SocketColumnLift = 0.9f;
        /// <summary>Proof counters: falls held, falls cleared at the collapse, ruins (by the outro blast / at the fall), socket puffs, socket flames.</summary>
        public static int FallHolds, FallClears, Ruins, RuinsAtFall, SocketPuffs, SocketFlames;
        public static float LastRuinAt = -100f;

        /// <summary>Step, first: a fallen fort while its body stands (true = it went: cleared, done). Not fallen (or the switch off): false, as before.</summary>
        bool FallStep(Fort f)
        {
            if (!RuinOn || !FortFallAtGameOver.Fallen(f.o))
            {
                if (f.fell) { f.fell = f.ruin = false; f.hushUntil = -100f; }                                     // it stands again (a test): as before
                return false;
            }
            if (BodyDown(f))
            {
                Clear(f, true); f.dead = true; FallClears++;
                LastLine = $"fort damage: {f.o.Type} #{f.o.Id} collapsed: marks gone, chunks out";
                return true;
            }
            if (!f.fell)
            {
                f.fell = true; f.fallTarget = f.target; FallHolds++;
                LastLine = $"fort damage: {f.o.Type} #{f.o.Id} ({f.o.Owner}) fell: its look held while it stands (D {f.d:0.00}, stage {f.stage})";
                Debug.Log("[OpenZ] " + LastLine);
                if (!f.ruin)
                    foreach (var m in FortGunMount.All)
                        if (m && m.Fort != null && m.Fort.Id == f.o.Id) { RuinsAtFall++; Ruin(f, m.Centre, "its cannon was shot off before it fell"); break; }
            }
            return false;
        }

        /// <summary>The fort's body went: collapsed (its renderers off) or gone.</summary>
        static bool BodyDown(Fort f)
        {
            if (!f.view || !f.baseT) return true;
            var br = f.view.GetComponent<BuildingBreaker>(); if (br && br.Collapsed) return true;
            if (!f.baseR) f.baseR = f.baseT.GetComponent<MeshRenderer>();
            return f.baseR && !f.baseR.enabled;
        }

        /// <summary>The damage a fallen fort shows: 1 once ruined, else what it showed when it fell.</summary>
        static float FallTarget(Fort f) => f.ruin ? 1f : f.fallTarget;

        /// <summary>FortGunView (the ending's outro blast): the fallen fort's cannon is blown off at <paramref name="socket"/>: heavy ruin, burning socket.
        /// False when the fort is not bound, not fallen, already gone or the switch is off.</summary>
        public static bool Ruin(int fortId, Vector3 socket, string why)
        {
            if (!RuinOn || !Enabled || !I || !I.forts.TryGetValue(fortId, out var f) || f.dead || !FortFallAtGameOver.Fallen(f.o)) return false;
            I.Ruin(f, socket, why);
            return true;
        }

        void Ruin(Fort f, Vector3 socket, string why)
        {
            if (f.ruin) return;
            if (!f.fell) { f.fell = true; f.fallTarget = f.target; FallHolds++; }
            f.ruin = true; f.ruinAt = LastRuinAt = Time.time; f.socket = socket; f.hushUntil = Time.time + RuinHushSeconds; f.nextSocket = Time.time + SocketColumnFrom;
            Ruins++;
            LastLine = $"fort damage: {f.o.Type} #{f.o.Id} ({f.o.Owner}) heavy ruin ({why}): D {f.d:0.00} -> 1 over ~{RuinEaseSeconds:0.0} s, burning socket at {socket}";
            Debug.Log("[OpenZ] " + LastLine);
        }

        /// <summary>The sweep's own sounds are left to the blast's boom for a moment (Play, the ignites).</summary>
        bool Hushed(int fortId) => forts.TryGetValue(fortId, out var f) && Time.time < f.hushUntil;

        /// <summary>Fall: during the sweep no small chips (the blast throws its own) and no more than RubbleCap pieces.</summary>
        static bool RuinSkips(Fort f, string cls) => Time.time < f.hushUntil && (cls == "XS" || f.chunks.Count >= RubbleCap);

        /// <summary>Step, after Stronger: the broken socket burns and smokes until the collapse.</summary>
        void RuinSocket(Fort f)
        {
            if (!f.ruin || Time.time < f.nextSocket) return;
            f.nextSocket = Time.time + SocketEvery;
            var at = f.socket;
            if (!WreckFx.OnScreen(at, 0.1f)) return;
            var px = OpenZFxPlayer.I;
            if (px && px.isActiveAndEnabled)
            {   // the column: the lit smoke flipbook, dark, 3-4 s puffs rising ~1.5 m/s (the breach columns' look, OzFortDamage.Stronger.cs)
                px.EmitChannel("m12_smoke", at + new Vector3(Random.Range(-0.6f, 0.6f), SocketColumnLift, Random.Range(-0.6f, 0.6f)),
                               new Vector3(Random.Range(-0.15f, 0.15f), Random.Range(1.2f, 1.8f), Random.Range(-0.15f, 0.15f)),
                               Random.Range(3.4f, 4.4f), ColumnSizeMax * Random.Range(0.85f, 1.15f), ColumnColour, 1f);
                SocketPuffs++;
            }
            var fx = FxManager.I; if (!fx) return;
            for (int i = 0; i < SocketFires; i++)
            {   // small fires at fixed places round the broken socket (the fort's own angles), their size flickering
                float a = f.o.Id * 2.399963f + i * 6.2831853f / Mathf.Max(1, SocketFires);
                var p = at + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (SocketFireR * (i % 2 == 0 ? 1f : 0.7f)) + Vector3.up * 0.2f;
                fx.Burn(p, SocketFireSize * Random.Range(0.85f, 1.15f));
                SocketFlames++;
            }
        }

        /// <summary>One line for the proof.</summary>
        public static string RuinReport() =>
            $"ending fort ruin: on {RuinOn}, falls held {FallHolds}, cleared at the collapse {FallClears}, ruins {Ruins} (at the fall {RuinsAtFall}), " +
            $"last at t {LastRuinAt:F2}, socket puffs {SocketPuffs}, socket flames {SocketFlames}";
    }
}
