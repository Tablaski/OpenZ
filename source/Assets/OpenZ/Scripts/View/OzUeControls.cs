using System;
using OpenZ.Sim;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.2 item 6 (View/OzUnitEditor.cs; owner 6j, 6k; spec E021-E049): the state behind the editor's bottom controls, for every module:
    /// the dock's team (RED / BLUE / NEUTRAL, remembered in oz_ue_team, default blue), the count per drop (x1 / x3 / x5 / x10 / x20, not remembered),
    /// the dock's tab (remembered in oz_ue_dtab), PLAY / PAUSE (exactly as the pause menu pauses: the game's paused flag and a time
    /// scale of 0, restored), the speed (the game's own speed, x1 x2 x4), the battle SOUND (remembered in oz_ue_sound), the dock open or
    /// closed, and the camera's Fit, zoom steps, compass and centring (View/OzUeCamera.cs).
    /// </summary>
    public static class OzUeControls
    {
        public const string TabRobots = "robots", TabVehicles = "vehicles", TabGuns = "guns", TabSpecial = "special";
        public static readonly string[] Tabs = { TabRobots, TabVehicles, TabGuns, TabSpecial };
        const string KeyTeam = "oz_ue_team", KeyTab = "oz_ue_dtab", KeySound = "oz_ue_sound";
        static bool loaded; static Team team = Team.Blue; static int count = 1; static string tab = TabRobots; static bool sound = true;
        static bool dockOpen = true;

        static void Load()
        {
            if (loaded) return;
            loaded = true;
            int t = PlayerPrefs.GetInt(KeyTeam, (int)Team.Blue);
            team = t == (int)Team.Red ? Team.Red : t == (int)Team.Neutral ? Team.Neutral : Team.Blue;
            string s = PlayerPrefs.GetString(KeyTab, TabRobots);
            tab = Array.IndexOf(Tabs, s) >= 0 ? s : TabRobots;
            if (team == Team.Neutral && (tab == TabRobots || tab == TabSpecial)) tab = TabVehicles;
            sound = PlayerPrefs.GetInt(KeySound, 1) == 1;
        }

        static ZGame Game => OzUnitEditor.I != null ? OzUnitEditor.I.Game : null;

        // ------------------------------------------------------------------------------------------------ dock: team, count, tab
        /// <summary>The team new drops join (RED / BLUE / NEUTRAL), remembered (oz_ue_team), default Blue.</summary>
        public static Team Team { get { Load(); return team; } }
        public static void SetTeam(Team t)
        {
            Load();
            if (t != Team.Red && t != Team.Blue) t = Team.Neutral;
            if (t == team) return;
            team = t; PlayerPrefs.SetInt(KeyTeam, (int)t); PlayerPrefs.Save();
            if (t == Team.Neutral && (tab == TabRobots || tab == TabSpecial)) { tab = TabVehicles; PlayerPrefs.SetString(KeyTab, tab); }   // NEUTRAL drops only vehicles and guns (E042)
            Debug.Log($"[OpenZ UnitEditor] dock team {OzUeUi.TeamWord(t)}, tab {tab}");
            Changed?.Invoke();
        }

        /// <summary></summary>
        public static readonly int[] CountSteps = { 1, 3, 5, 10, 20 };
        public static int Count => count;
        /// <summary>The dock cell of a count: the largest step it reaches (0 .. 4).</summary>
        public static int CountIndex(int n) { int k = 0; for (int i = 0; i < CountSteps.Length; i++) if (n >= CountSteps[i]) k = i; return k; }
        public static void SetCount(int n)
        {
            n = CountSteps[CountIndex(n)];
            if (n == count) return;
            count = n;
            Debug.Log($"[OpenZ UnitEditor] dock count x{n}");
            Changed?.Invoke();
        }

        /// <summary>"robots", "vehicles", "guns" or "special" (remembered, oz_ue_dtab).</summary>
        public static string Tab { get { Load(); return tab; } }
        public static void SetTab(string t)
        {
            Load();
            if (Array.IndexOf(Tabs, t) < 0 || t == tab) return;
            tab = t; PlayerPrefs.SetString(KeyTab, t); PlayerPrefs.Save();
            Changed?.Invoke();
        }

        /// <summary>The team, the count or the tab changed.</summary>
        public static event Action Changed;

        // ------------------------------------------------------------------------------------------------ play bar
        /// <summary>The fight runs (not paused).</summary>
        public static bool Playing { get { var g = Game; return g == null || !g.paused; } }
        public static void TogglePlay() => SetPlaying(!Playing);
        static float scaleBefore = 1f;
        /// <summary>Pause exactly as the pause menu does (OpenZPauseMenu.Open / Close): the game's paused flag and the time scale 0 (the
        /// sim and the animations stop); the HUD, the camera, picking, placing and stats editing go on (unscaled time).</summary>
        public static void SetPlaying(bool playing)
        {
            var g = Game; if (g == null || playing == !g.paused) return;
            if (!playing)
            {
                scaleBefore = Time.timeScale > 0f ? Time.timeScale : 1f;
                g.paused = true;
                OzUeStatics.Hold("Time.timeScale", () => Time.timeScale, v => Time.timeScale = v, 0f);
            }
            else
            {
                g.paused = false;
                OzUeStatics.Hold("Time.timeScale", () => Time.timeScale, v => Time.timeScale = v, scaleBefore > 0f ? scaleBefore : 1f);
            }
            Debug.Log($"[OpenZ UnitEditor] {(playing ? "PLAY: the fight goes on" : "PAUSE: the fight stands still")}");
            PlayChanged?.Invoke();
        }

        /// <summary>The game's speed (game.gameSpeed): x1 x2 x4 from the play bar; another value from the +/- keys shows no cell selected.</summary>
        public static float Speed { get { var g = Game; return g != null ? g.gameSpeed : 1f; } }
        public static void SetSpeed(float x)
        {
            var g = Game; if (g == null) return;
            x = Mathf.Clamp(x, 0.25f, 4f);
            if (Mathf.Abs(g.gameSpeed - x) < 0.001f) return;
            g.gameSpeed = x;
            Debug.Log($"[OpenZ UnitEditor] speed x{x:0.##}");
            PlayChanged?.Invoke();
        }

        /// <summary>The battle sound is on (E026), remembered (oz_ue_sound).</summary>
        public static bool SoundOn { get { Load(); return sound; } }
        /// <summary>SOUND OFF mutes the playground's world (weapons, explosions, engines, the ambience: the EFFECTS bus, and the robots'
        /// voices) through the same volume control as the OPTIONS sliders (OzAudio), never the interface sounds; the saved slider values
        /// are not touched and come back with SOUND ON and when the editor closes. Sounds keep playing silently (no AudioListener.pause),
        /// so nothing bursts out when the sound comes back.</summary>
        public static void SetSound(bool on)
        {
            Load();
            sound = on; PlayerPrefs.SetInt(KeySound, on ? 1 : 0); PlayerPrefs.Save();
            ApplySound();
            Debug.Log($"[OpenZ UnitEditor] battle sound {(on ? "ON" : "OFF")}");
            PlayChanged?.Invoke();
        }
        static bool muted;
        /// <summary>The editor opened: the remembered SOUND state applies (and is undone when it closes).</summary>
        internal static void ApplySound() => OzUeStatics.Hold("Unit editor battle sound off", () => muted, Mute, !SoundOn);
        static void Mute(bool off)
        {
            muted = off;
            OzAudio.Set("SFX", off ? 0f : PlayerPrefs.GetFloat("oz_vol_effects", 1f));       // the EFFECTS slider's bus (+ the ambience), as AudioDirector.ApplyUserVolumes reads it
            OzAudio.Set("Robots", off ? 0f : PlayerPrefs.GetFloat("oz_vol_robots", 1f));      // the ROBOT VOICES slider's bus
        }
        /// <summary>Checks: the world is muted now.</summary>
        public static bool WorldMuted => muted;
        /// <summary>Once a second while muted: the audio track sets its volumes from the saved sliders when its table loads (after the
        /// editor opened), so the mute is put on its buses again (no log line).</summary>
        internal static void KeepMute() { if (muted) Mute(true); }

        /// <summary>PLAY / PAUSE, the speed or the sound changed.</summary>
        public static event Action PlayChanged;

        // ------------------------------------------------------------------------------------------------ dock open / closed
        public static bool DockOpen => dockOpen;
        public static void ToggleDock() => SetDockOpen(!dockOpen);
        public static void SetDockOpen(bool open)
        {
            if (open == dockOpen) return;
            dockOpen = open;
            Debug.Log($"[OpenZ UnitEditor] dock {(open ? "open" : "closed")}");
            DockChanged?.Invoke();
        }
        /// <summary>The dock opened or closed.</summary>
        public static event Action DockChanged;

        // ------------------------------------------------------------------------------------------------ camera
        public static void Fit() { var c = OzUeCamera.I; if (c) c.Fit(); }
        public static void ZoomStep(int dir) { var c = OzUeCamera.I; if (c) c.ZoomStep(dir); }
        public static void CompassHome() { var c = OzUeCamera.I; if (c) c.CompassHome(); }
        /// <summary>The free field's centre glides to this floor point.</summary>
        public static void CentreOn(Vector3 worldPoint) { var c = OzUeCamera.I; if (c) c.CentreOn(worldPoint); }
        public static float ZoomPercent { get { var c = OzUeCamera.I; return c ? c.ZoomPercent : 100f; } }

        /// <summary>The editor opened: a fresh session (the count back to x1, the dock open).</summary>
        internal static void Begin()
        {
            Load();
            count = 1; dockOpen = true; scaleBefore = 1f;
        }

        /// <summary>Leaving the editor: no listener of a dead session stays (ARCHITECTURE 5.6).</summary>
        internal static void ResetEvents() { Changed = null; PlayChanged = null; DockChanged = null; }
    }
}
