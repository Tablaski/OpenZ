using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OpenZ
{
    /// <summary>
    ///
    /// Cause: the fort only explodes on its sim Death event. Zod destroys a team's forts when its LAST unit is destroyed
    /// (zserver.cpp:1925-1965 CheckNoUnitsDestroyFort, called from UpdateObjectHealth), but the game also ends when the last unit is
    /// lost without dying: its driver sniped (the vehicle turns neutral), a crew ejected, a vehicle entering the repair station.
    /// EndGameRequirementsMet (zserver.cpp:337-362) then sees one team left and ends the game with the loser's fort still standing
    /// (ZWorld.CheckEndGame, 1:1) - no Death event, no collapse, no nuke.
    ///
    /// Fix (view only; the sim is untouched and the game is already over): the frame the sim reports GameOver, every fort of a
    /// losing team that still stands gets a Death event put into ZWorld.Events before any reader sees the list (order -48: after
    /// ZGame's tick at -50 and OpenZEnding's scan at -49, before AudioDirector 40 / FortGunView 45 / CombatView 50). Every listener
    /// then plays the fort's fall exactly as for a sim kill: RulesView.BuildingDeath (the nuke strike, or with strikes off the
    /// immediate collapse + NukeFx: the fort always explodes), the fort gun blown off, the fort-lost lines, OpenZVictory's
    /// sequence waits for the strike. A draw (no winner) and a fort the sim already destroyed are left alone.
    /// </summary>
    [DefaultExecutionOrder(-48)]
    public sealed class FortFallAtGameOver : MonoBehaviour
    {
        public static bool Enabled = true;
        /// <summary>Forts exploded by this rule this level (Play proof).</summary>
        public static int Injected { get; private set; }
        /// <summary>
        /// the nuke): a fort this rule drops still stands in the sim (Destroyed stays false, the game is already over) and no TeamEliminated event comes, so the
        /// ending scenes refused it and what leaves the roof on fort.Destroyed (the cannon's wrecked mount, the roof defender, the tower
        /// plinths, the alarm beacons) stayed in the air where the roof had been. The view reads such a fort as fallen: <see cref="Fallen"/>, <see cref="AnyFell"/>.
        /// Off = as before.</summary>
        public static bool ViewFallen = true;
        static readonly HashSet<int> fell = new HashSet<int>();
        /// <summary>This level's game over dropped a fort by this rule: its strike, mushroom and collapse are there (no TeamEliminated event).</summary>
        public static bool AnyFell => ViewFallen && fell.Count > 0;
        /// <summary>The fort is down: destroyed in the sim, or dropped by this rule at the game over.</summary>
        public static bool Fallen(ZObject f) => f != null && (f.Destroyed || (ViewFallen && fell.Contains(f.Id)));
        ZGame game; bool done;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() { SceneManager.sceneLoaded -= OnLoaded; SceneManager.sceneLoaded += OnLoaded; Attach(); }
        static void OnLoaded(Scene s, LoadSceneMode m) => Attach();
        static void Attach()
        {
            var g = ZGame.I ? ZGame.I : FindFirstObjectByType<ZGame>();
            if (g && !g.GetComponent<FortFallAtGameOver>()) g.gameObject.AddComponent<FortFallAtGameOver>();
        }

        void Awake() { game = GetComponent<ZGame>(); if (!game) game = ZGame.I; Injected = 0; fell.Clear(); }

        /// <summary>open_fixes_3nov rows 76 / 77, a check (Play: execute code; a non-release player: -ozdev with Ctrl+Alt+N, or -ozend leave &lt;game s&gt;; refused
        /// in the release exe): the enemy's units go without its fort being destroyed in the sim, as when its last driver is sniped or its last gun is taken.
        /// Every enemy unit but one dies through the sim's own death path (a vehicle is kept when there is one), then that one turns neutral. The sim's game
        /// over follows within 1 s with the enemy fort standing, and this rule drops it. Returns the line it logs.</summary>
        public static string DebugLeave(string why)
        {
            if (global::OpenZ.OzRelease.IsRelease) return "refused in the release exe";
            var g = ZGame.I; if (!g || g.World == null || g.World.GameOver) return "no match running";
            var W = g.World; var enemy = g.player == Team.Red ? Team.Blue : Team.Red;
            ZObject keep = null;
            foreach (var o in W.Objects)
                if (o.Owner == enemy && o.IsUnit && !o.Destroyed && o.KillAt < 0 && !o.IsFortDefender && (keep == null || (o.Kind == ZKind.Vehicle && keep.Kind != ZKind.Vehicle))) keep = o;
            if (keep == null) return "no enemy unit left";
            int n = 0;
            foreach (var o in new List<ZObject>(W.Objects))
                if (o != keep && o.Owner == enemy && o.IsUnit && !o.Destroyed && o.KillAt < 0 && !o.IsFortDefender) { o.DamageHealth(o.Health + 1); W.UpdateObjectHealth(o, -1); n++; }
            keep.Owner = Team.Neutral;
            string line = $"[OpenZ] check ({why}): {n} {enemy} unit(s) killed, the last one ({keep.Type} #{keep.Id}) turned neutral without dying: the game over comes with the {enemy} fort standing";
            Debug.Log(line);
            return line;
        }

        void Update()
        {
            if (!Enabled || done || game == null || game.World == null || !game.World.GameOver) return;
            done = true;
            var w = game.World;
            if (w.Winner == Team.Neutral) return;                                                     // a draw: nobody's fort falls
            foreach (var f in w.Objects)
            {
                if (!f.IsFort || f.Destroyed || f.Owner == Team.Neutral || f.Owner == w.Winner) continue;
                w.Events.Add(new ZEvent { type = ZEventType.Death, objId = f.Id, otherId = -1, x = f.CenterX, y = f.CenterY });
                Injected++; fell.Add(f.Id);
                Debug.Log($"[OpenZ] game over with the {f.Owner} fort standing (last unit lost without dying): it falls now (combat_nb_v1 NB30)");
            }
        }
    }
}
