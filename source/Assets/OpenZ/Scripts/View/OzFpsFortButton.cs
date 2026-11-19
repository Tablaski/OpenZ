// OpenZ (GPL-3.0) - 1.5 kit fps_fort_button_win (PRIVATE: the owner's first-person test view, never in player text).
using System.Collections.Generic;
using OpenZ.Sim;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OpenZ
{
    /// <summary>
    /// PRIVATE: the hidden first-person view only (OzFpsView); never in any player text, help or controls list.
    /// <list type="bullet">
    /// <item>When the unit of the view (a robot) stands within <see cref="ReachM"/> m of an ENEMY fort's wall SELF DESTRUCT panel (OzFortSelfDestructPanels,
    /// the eye in front of the panel's face) and looks at its red button (within <see cref="FacingDeg"/> deg of the look) for <see cref="DwellSeconds"/> s,
    /// it presses it: the press sound (event <see cref="PressEvent"/> at the button, 3D, heard close by), the wall beacon above the panel goes wild
    /// (OzFortSelfDestructAlarm: urgent at once, spinning <see cref="WildSpin"/> x faster, pulsing <see cref="WildPulse"/> x faster, its light
    /// <see cref="WildLight"/> x brighter, its hum urgent), and <see cref="LeadSeconds"/> s later the unit gets the order a plain right-click on that
    /// fort's door gives it: ENTER_FORT, through the normal order path (PlayerInput.FpsEnterFort: BuildWaypoint + SEND_WAYPOINTS, one command on a sim
    /// tick, as every player order). The rules then run the fort's fall and the win exactly as in play (deterministic: nothing here writes the Sim).</item>
    /// <item>From the press until the fort falls (or <see cref="HoldSeconds"/> s after the order) the view's walk keys and fire click send nothing
    /// (OzFpsView.Update, one hook line): a held or released W never replaces or stops the ENTER_FORT order; the mouse look stays free.</item>
    /// <item>Never on an own or neutral fort, never on a fort that is down (its panel is hidden then), never twice for one fort while its order runs.
    /// An order the rules refuse: the view's refusal sound (ui_disabled), the beacon calms down.</item>
    /// </list>
    /// Boots itself in every level scene (ZGame present). Switch: <see cref="Enabled"/>.
    /// </summary>
    [DefaultExecutionOrder(32120)]   // after the panels (32100) and the beacons (32110) set this frame's shown state
    public sealed class OzFpsFortButton : MonoBehaviour
    {
        public static bool Enabled = true;
        public const string PressEvent = "sfx_selfdestruct_press";
        /// <summary>Reach (m, eye to the button), the look's cone (deg), how long the look rests on it (s), the eye in front of the panel's face (cos).</summary>
        public static float ReachM = 2.4f, FacingDeg = 32f, DwellSeconds = 0.25f, FrontCos = 0.15f;
        /// <summary>The beacon's wild moment before the order (s); the longest the beacon stays wild (s); the walk / fire hold after the order (s).</summary>
        public static float LeadSeconds = 0.8f, WildMaxSeconds = 8f, HoldSeconds = 4f;
        /// <summary>The wild beacon: spin period x, pulse rate x, light x.</summary>
        public static float WildSpin = 0.45f, WildPulse = 2.2f, WildLight = 1.6f;
        /// <summary>Proof counters: presses, orders sent, orders refused, frames the walk / fire were held, the last press (text).</summary>
        public static int Presses, Orders, Refused, HeldFrames, AimFrames;
        public static string LastPress = "";
        public static OzFpsFortButton I { get; private set; }

        static readonly Dictionary<int, float> wild = new Dictionary<int, float>();
        ZObject pendUnit, pendFort; float pendAt = -1f, holdUntil = -1f, dwell; int dwellFort = -1;
        readonly HashSet<int> running = new HashSet<int>();
        PlayerInput pi;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Presses = Orders = Refused = HeldFrames = AimFrames = 0; LastPress = ""; wild.Clear(); I = null; }

        static bool hooked;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (!hooked) { hooked = true; SceneManager.sceneLoaded += (s, m) => TryAttach(); }
            TryAttach();
        }

        static void TryAttach()
        {
            if (!Enabled || !ZGame.I || FindFirstObjectByType<OzFpsFortButton>()) return;
            new GameObject("OZ_FortWallButton").AddComponent<OzFpsFortButton>();
        }

        void Awake() { I = this; wild.Clear(); }
        void OnDestroy() { if (I == this) I = null; }

        // ------------------------------------------------------------------ the hooks
        /// <summary>OzFortSelfDestructAlarm: this fort's panel was pressed: its beacon goes wild.</summary>
        public static bool IsWild(ZObject fort) => fort != null && wild.TryGetValue(fort.Id, out var until) && Time.time < until;

        /// <summary>OzFpsView.Update: the walk keys and the fire click send nothing for this unit now (the press and its ENTER_FORT order run).</summary>
        public static bool Holds(ZObject unit)
        {
            var b = I; if (!b || unit == null) return false;
            bool h = (b.pendAt >= 0f && b.pendUnit == unit) || (Time.time < b.holdUntil && b.pendUnit == unit && b.pendFort != null && !b.pendFort.Destroyed);
            if (h) HeldFrames++;
            return h;
        }

        // ------------------------------------------------------------------ every frame
        void LateUpdate()
        {
            var g = ZGame.I;
            if (!Enabled || !g || g.World == null) return;
            if (pendAt >= 0f && !g.paused && Time.time >= pendAt) SendOrder();
            if (pendFort != null && pendFort.Destroyed) { wild.Remove(pendFort.Id); running.Remove(pendFort.Id); }
            var v = OzFpsView.I;
            if (!OzFpsView.On || OzFpsFeel.GlideOutNow || !v || v.Unit == null || !v.Cam || g.paused) { dwell = 0f; dwellFort = -1; return; }
            var u = v.Unit;
            if (u.Kind != ZKind.Robot || u.Destroyed || pendAt >= 0f) { dwell = 0f; dwellFort = -1; return; }
            var fort = Aimed(g, u, v.Cam, out var button);
            if (fort == null) { dwell = 0f; dwellFort = -1; return; }
            AimFrames++;
            if (fort.Id != dwellFort) { dwellFort = fort.Id; dwell = 0f; }
            dwell += Time.deltaTime;
            if (dwell >= DwellSeconds) Press(u, fort, button);
        }

        /// <summary>The enemy fort whose shown panel's button the eye is within reach of, in front of, and looking at; null if none.</summary>
        ZObject Aimed(ZGame g, ZObject u, Camera c, out Vector3 button)
        {
            button = Vector3.zero;
            var fvs = FactoryViews.I; if (!fvs) return null;
            var eye = c.transform.position; var look = c.transform.forward;
            var objs = g.World.Objects;
            for (int i = 0; i < objs.Count; i++)
            {
                var o = objs[i];
                if (o == null || !o.IsFort || o.Destroyed || o.Owner == Team.Neutral || o.Owner == u.Owner || !o.CanEnterFort(u.Owner) || running.Contains(o.Id)) continue;
                if (FortFallAtGameOver.Fallen(o)) continue;
                var fv = fvs.For(o.Id);
                var panel = OzFortSelfDestructPanels.PanelOf(fv);
                if (!panel || !panel.gameObject.activeInHierarchy) continue;
                var mr = panel.GetComponent<MeshRenderer>(); if (!mr || !mr.enabled) continue;            // shown only while the hall stands
                var b = panel.TransformPoint(new Vector3(OzFortSelfDestructPanels.BtnX, OzFortSelfDestructPanels.BtnY, OzFortSelfDestructPanels.CollarZ1));
                var to = b - eye; float d = to.magnitude;
                if (d > ReachM || d < 1e-3f) continue;
                var face = panel.rotation * Vector3.back;                                                 // the panel's front looks into the hall (its mesh's -z)
                if (Vector3.Dot(-to / d, face) < FrontCos) continue;                                      // the eye stands in front of it, not behind the wall
                if (Vector3.Angle(look, to) > FacingDeg) continue;
                button = b;
                return o;
            }
            return null;
        }

        void Press(ZObject u, ZObject fort, Vector3 button)
        {
            Presses++; dwell = 0f; dwellFort = -1;
            running.Add(fort.Id);
            wild[fort.Id] = Time.time + WildMaxSeconds;
            AudioDirector.Fire(PressEvent, button);
            if (!pi) pi = FindFirstObjectByType<PlayerInput>();
            if (pi && u.Waypoints.Count > 0) pi.FpsStop(u);                                              // it stands at the panel while it presses (the view's STOP order)
            pendUnit = u; pendFort = fort; pendAt = Time.time + LeadSeconds; holdUntil = pendAt + HoldSeconds;
            LastPress = $"{u.Type} #{u.Id} pressed the panel of {fort.Type} #{fort.Id} ({fort.Owner}) at t {Time.time:F2}";
#if !OZ_RELEASE
            Debug.Log("[OpenZ] fort wall button: " + LastPress);
#endif
        }

        void SendOrder()
        {
            var u = pendUnit; var f = pendFort; pendAt = -1f;
            if (!pi) pi = FindFirstObjectByType<PlayerInput>();
            bool ok = u != null && !u.Destroyed && f != null && !f.Destroyed && pi && pi.FpsEnterFort(u, f);
            if (ok) { Orders++; holdUntil = Time.time + HoldSeconds; }
            else
            {
                Refused++; holdUntil = -1f;
                if (f != null) { wild.Remove(f.Id); running.Remove(f.Id); }
                AudioDirector.UI("ui_disabled");
            }
#if !OZ_RELEASE
            Debug.Log("[OpenZ] fort wall button: " + (ok ? "ENTER_FORT order sent" : "no ENTER_FORT order (refused)") + $" for {(u != null ? u.Type + " #" + u.Id : "-")} on {(f != null ? f.Type + " #" + f.Id : "-")}");
#endif
        }

        // ------------------------------------------------------------------ the Unity proof (Play mode; the console or a check)
        /// <summary>Presses the panel of the index-th enemy fort for unit u as if its eye reached it (the reach test skipped: the sound, the wild
        /// beacon, the ENTER_FORT order after the lead). False when there is no such fort or u is not a robot of a side.</summary>
        public static bool TestPress(ZObject u, int index = 0)
        {
            var b = I; var g = ZGame.I;
            if (!b || !g || g.World == null || u == null || u.Kind != ZKind.Robot || b.pendAt >= 0f) return false;
            int k = 0;
            foreach (var o in g.World.Objects)
            {
                if (o == null || !o.IsFort || o.Destroyed || o.Owner == Team.Neutral || o.Owner == u.Owner) continue;
                if (k++ != index) continue;
                var panel = FactoryViews.I ? OzFortSelfDestructPanels.PanelOf(FactoryViews.I.For(o.Id)) : null;
                b.Press(u, o, panel ? panel.position : g.CenterWorld(o));
                return true;
            }
            return false;
        }

        /// <summary>One line of every count, for the proof.</summary>
        public static string Report() =>
            $"fort wall button: on {Enabled}, presses {Presses}, orders {Orders}, refused {Refused}, held frames {HeldFrames}, aimed frames {AimFrames}; last: {LastPress}";
    }
}
