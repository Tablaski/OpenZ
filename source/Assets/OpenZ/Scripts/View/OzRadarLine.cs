using OpenZ.Sim;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OpenZ
{
    /// <summary>
    /// 1.2 item 16: the announcer's radar line says what really happened. Follows every team's radars each frame (the rule:
    /// OzRadarLineRule.cs) and gives FlagView / AudioDirector the event for a radar capture: ann_radar_first ("Radar online. Enemy
    /// units now tracked on the minimap.") for the team's first radar picture of the game, ann_radar_back_online ("Radar is back
    /// online.") when the team had one, lost every standing radar and got one again, ann_radar_online (the plain "Radar online." /
    /// "Radar active.") while another standing radar already gives the picture, nothing for a ruin. View only: writes nothing into
    /// the match. Attached to the game object at every level start, like the other view layers.
    /// </summary>
    [DefaultExecutionOrder(110)]                                                                  // after ZGame (-50, sim tick): the same frame's owners
    public sealed class OzRadarLine : MonoBehaviour
    {
        public static bool AutoAttach = true;
        public static OzRadarLine I { get; private set; }
        /// <summary>The rule (one for the session; a new world starts it over).</summary>
        public static readonly OzRadarLineRule Rule = new OzRadarLineRule();
        /// <summary>The last choice, for a Play check: event (or "none"), team, flag, the team's picture count.</summary>
        public static string Last = "none yet";

        ZGame game;

        static bool hooked;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (!hooked) { hooked = true; SceneManager.sceneLoaded += (s, m) => Attach(); }
            Attach();
        }
        public static void Attach()
        {
            if (!AutoAttach || !ZGame.I || ZGame.I.GetComponent<OzRadarLine>()) return;
            ZGame.I.gameObject.AddComponent<OzRadarLine>();
        }

        void Awake() { I = this; game = GetComponent<ZGame>(); }
        void OnDestroy() { if (I == this) I = null; }
        void Update()
        {
            if (!game) game = GetComponent<ZGame>();
            if (game) Rule.Observe(game.World);
        }

        /// <summary>The announcer event for the radar of flag `flagId`'s zone that `team` has just taken; null = no radar line.
        /// A sound table without this event falls back to the plain ann_radar_online.</summary>
        public static string EventFor(ZWorld w, Team team, int flagId)
        {
            string id = Rule.LineFor(w, team, flagId);
            if (id != null && id != OzRadarLineRule.More && !AudioDirector.Has(id)) id = OzRadarLineRule.More;
            Last = $"{id ?? "none"} (team {team}, flag {flagId}, picture {Rule.Pictures(team)})";
            Debug.Log($"[OpenZ] radar line: {Last}");
            return id;
        }

        /// <summary>One-line state for a Play check.</summary>
        public static string Status()
        {
            var w = Rule.World;
            return $"radars {Rule.RadarCount}, red picture {(Rule.Covered(Team.Red) ? "on" : "off")} x{Rule.Pictures(Team.Red)}, blue picture {(Rule.Covered(Team.Blue) ? "on" : "off")} x{Rule.Pictures(Team.Blue)}; "
                 + $"lines first {Rule.Firsts}, back {Rule.Backs}, plain {Rule.Mores}, none (ruin) {Rule.Skipped}; last: {Last}; world {(w != null ? "followed" : "none")}";
        }
    }
}
