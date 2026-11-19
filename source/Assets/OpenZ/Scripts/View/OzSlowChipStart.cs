// OpenZ (GPL-3.0) - 1.5 kit issue8_linux_settings (GitHub issue 8): the first start on a slow graphics chip.
// lighter settings himself. Now: when the game starts on a slow graphics chip and the player has never chosen any of the six
// settings below, the game chooses them once and saves them:
//   GRAPHICS QUALITY LOW, LIGHTEST FOR SLOW PCS on (SHADOWS off, AMBIENT OCCLUSION off, TREES AND PLANTS lightest, WRECKS AND
//   DEBRIS light), FRAME RATE LIMIT 30.
// 1.5 kit issue8_slow_chip_picture: and the LIGHTEST FOR SLOW PCS row 3D PICTURE SOFTEST (View/OzLowPicture.cs: the battlefield drawn
// within 800x450 pixels on LOW, text sharp), under the same rules as the four rows below (it belongs to LOW, back to SHARP by itself above it).
// Player.log says so ("[OpenZ Settings] first start on a slow graphics chip ..."). Every one of them stays a normal row in
// OPTIONS > GRAPHICS; the first change by the player makes them his for good (the game never chooses again).
// The owner's condition (12 Nov): the four LIGHTEST FOR SLOW
// PCS rows chosen here belong to GRAPHICS QUALITY LOW only. They are only ever set together with LOW, and while a row still holds the
// value this start gave it (oz_slow_chip_rows_v1, one bit per row), a higher GRAPHICS QUALITY (MEDIUM or HIGH) puts that row back on
// its default by itself (SHADOWS on, AMBIENT OCCLUSION on, TREES AND PLANTS FULL, WRECKS AND DEBRIS FULL), live in OPTIONS or at the
// next start, with one Player.log line. A row the player set himself (any change of it, even back to the same value later) is his
// and keeps his value. Back on LOW nothing is chosen again. FRAME RATE LIMIT is no LIGHTEST row: it stays as chosen (its own row).
// A slow chip = by its name (IsSlowChip): Intel's built-in graphics (HD, UHD, Iris, Xe; not Arc), the older built-in Radeon chips
// (Vega, "Radeon Graphics", R2-R7 Graphics) and software drawing (llvmpipe, Microsoft Basic Render Driver ...). Everything else,
// every NVIDIA chip, every Radeon RX and the newer built-in Radeons (680M and up) included, starts as before (HIGH).
// here, they go back to the defaults (HIGH); on the slow chip again they are chosen again.
// Not in the editor, not on the Mac (View/OzMacPerf.cs has its own budget), not in a test or capture start (-oz..., -take,
// -batchmode). Called by View/OzPrefsMirror.cs before any setting is read (the Linux safety copy has given its values back by then).
// Fast chips, and every player whose settings were not chosen here, never meet any of this (no row bit is ever saved for them, no
// watcher is made).
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace OpenZ
{
    public static class OzSlowChipStart
    {
        /// <summary>0 / not there: nothing chosen yet; 1: chosen by the game and still as chosen; 2: the player's own from now on.</summary>
        public const string MarkKey = "oz_slow_chip_start_v1";
        /// <summary>The LIGHTEST FOR SLOW PCS rows that still hold the value this start gave them (bit i = RowKeys[i]); 0 / not there = none.</summary>
        public const string RowsKey = "oz_slow_chip_rows_v1";
        const string Tag = "[OpenZ Settings]";
        public const string Chosen = "GRAPHICS QUALITY LOW, LIGHTEST FOR SLOW PCS on, 3D PICTURE SOFTEST and FRAME RATE LIMIT 30";   // 1.5 kit issue8_slow_chip_picture: 3D PICTURE SOFTEST

        /// <summary>The seven saved settings (1.5 kit issue8_slow_chip_picture: + 3D PICTURE SOFTEST = 2, Keys[5]) of the choice and their values (LOW = 2; shadows off, ambient occlusion off, trees lightest,
        /// wrecks light = the four rows of LIGHTEST FOR SLOW PCS; frame rate limit 30).</summary>
        public static readonly string[] Keys = { OzQuality.Pref, OzLightRows.ShadowsPref, OzLightRows.AoPref, OzLightRows.TreesPref, OzLightRows.WrecksPref, OzLowPicture.Pref, OzFpsCap.CapPref };   // 1.5 kit issue8_slow_chip_picture: + 3D PICTURE (Keys[5])
        public static readonly int[] Pick = { 2, 1, 1, 2, 1, OzLowPicture.Softest, 30 };

        /// <summary>The five LIGHTEST FOR SLOW PCS rows (Keys[1..5]; 1.5 kit issue8_slow_chip_picture: + 3D PICTURE, default SHARP); their default value is 0 (SHADOWS on, AMBIENT OCCLUSION on, TREES AND
        /// PLANTS FULL, WRECKS AND DEBRIS FULL). The values chosen for them belong to GRAPHICS QUALITY LOW (LowLevel).</summary>
        public static readonly string[] RowKeys = { OzLightRows.ShadowsPref, OzLightRows.AoPref, OzLightRows.TreesPref, OzLightRows.WrecksPref, OzLowPicture.Pref };   // 1.5 kit issue8_slow_chip_picture: + 3D PICTURE (bit 4 = 16, Keys[5])
        public static readonly string[] RowDefaults = { "SHADOWS on", "AMBIENT OCCLUSION on", "TREES AND PLANTS FULL", "WRECKS AND DEBRIS FULL", "3D PICTURE SHARP" };
        public const int AllRows = 31, LowLevel = 2;

        public enum Step { Nothing, Pick, TakeBack, PlayersOwn }

        /// <summary>The graphics chip's name says it is too slow for the default look.</summary>
        public static bool IsSlowChip(string name)
        {
            string n = (name ?? "").ToLowerInvariant();
            if (n.Length == 0) return false;
            foreach (var s in new[] { "llvmpipe", "lavapipe", "softpipe", "swiftshader", "microsoft basic render" }) if (n.Contains(s)) return true;
            if (n.Contains("nvidia") || n.Contains("geforce")) return false;
            if (n.Contains("intel")) return !Regex.IsMatch(n, @"\barc\b");
            if (n.Contains("radeon"))
            {
                if (Regex.IsMatch(n, @"\brx\b|\bpro\b|firepro|\b[5-9]\d\dm\b")) return false;
                return n.Contains("vega") || Regex.IsMatch(n, @"radeon(\(tm\))?\s+(r[2-7]\s+)?graphics");
            }
            return false;
        }

        /// <summary>What this start does. mark: the saved mark; slow: this start's chip; anySaved: one of the six settings has a saved
        /// value; asPicked: all six are exactly the chosen values.</summary>
        public static Step Decide(int mark, bool slow, bool anySaved, bool asPicked)
        {
            if (mark <= 0) return !slow ? Step.Nothing : anySaved ? Step.PlayersOwn : Step.Pick;
            if (mark == 1) return !asPicked ? Step.PlayersOwn : slow ? Step.Nothing : Step.TakeBack;
            return Step.Nothing;
        }

        // ------------------------------------------------------------------ the LIGHTEST rows belong to LOW (the owner's condition, 12 Nov)
        /// <summary>The rows of `rows` (bits) that still hold the value this start gave them; a row with another value is the player's.
        /// values: the four rows' saved values in RowKeys order.</summary>
        public static int StillChosen(int rows, int[] values)
        {
            int m = 0;
            for (int i = 0; i < RowKeys.Length; i++)
                if ((rows & (1 << i)) != 0 && values != null && i < values.Length && values[i] == Pick[i + 1]) m |= 1 << i;
            return m;
        }

        /// <summary>The rows that go back to their defaults now: every row still as chosen, once GRAPHICS QUALITY is above LOW.</summary>
        public static int GoBack(int rows, int quality) => Mathf.Clamp(quality, 0, 2) == LowLevel ? 0 : rows & AllRows;

        /// <summary>The defaults of the rows in `rows`, for the log ("SHADOWS on, AMBIENT OCCLUSION on").</summary>
        public static string Names(int rows)
        {
            var l = new List<string>();
            for (int i = 0; i < RowKeys.Length; i++) if ((rows & (1 << i)) != 0) l.Add(RowDefaults[i]);
            return string.Join(", ", l);
        }

        /// <summary>The start's look at the rows on a store (the engine's settings; the check uses a table), after the start's own step:
        /// rows the player changed lose their bit, and above LOW the rows still as chosen are set to their defaults (0). Returns the
        /// rows set back; `left` = the rows still as chosen afterwards (saved under RowsKey). With no row bit saved nothing is read or
        /// written (fast chips, the player's own settings).</summary>
        public static int Settle(OzPrefsMirror.IStore s, out int left)
        {
            left = 0;
            int rows = s.GetInt(RowsKey, 0) & AllRows;
            if (rows == 0) return 0;
            var values = new int[RowKeys.Length];
            for (int i = 0; i < RowKeys.Length; i++) values[i] = s.GetInt(RowKeys[i], 0);
            rows = StillChosen(rows, values);
            int back = GoBack(rows, s.GetInt(OzQuality.Pref, 0));
            for (int i = 0; i < RowKeys.Length; i++) if ((back & (1 << i)) != 0) s.SetInt(RowKeys[i], 0);
            left = rows & ~back;
            if (s.GetInt(RowsKey, 0) != left) s.SetInt(RowsKey, left);
            return back;
        }

        sealed class Engine : OzPrefsMirror.IStore
        {
            public bool Has(string key) => PlayerPrefs.HasKey(key);
            public int GetInt(string key, int fallback) => PlayerPrefs.GetInt(key, fallback);
            public float GetFloat(string key, float fallback) => PlayerPrefs.GetFloat(key, fallback);
            public string GetString(string key, string fallback) => PlayerPrefs.GetString(key, fallback);
            public void SetInt(string key, int v) => PlayerPrefs.SetInt(key, v);
            public void SetFloat(string key, float v) => PlayerPrefs.SetFloat(key, v);
            public void SetString(string key, string v) => PlayerPrefs.SetString(key, v);
        }

        /// <summary>The one Player.log line when rows go back (live or at a start).</summary>
        public static string BackLine(int quality, int back) =>
            $"{Tag} GRAPHICS QUALITY {OzQuality.Names[Mathf.Clamp(quality, 0, 2)]}: the LIGHTEST FOR SLOW PCS rows the slow graphics chip start chose for LOW are back on their defaults ({Names(back)}); a row the player set himself keeps his value";

        // ------------------------------------------------------------------ the start
        /// <summary>This session: the rows still as chosen (0 = nothing to watch; the watcher is only made when it is not 0).</summary>
        static int liveRows;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { liveRows = 0; }

        public static void Run()
        {
            if (Application.isEditor || Application.platform == RuntimePlatform.OSXPlayer) return;
            foreach (var a in Environment.GetCommandLineArgs())
            {
                if (a.Equals("-take", StringComparison.OrdinalIgnoreCase) || a.Equals("-batchmode", StringComparison.OrdinalIgnoreCase)) return;
                if (a.StartsWith("-oz", StringComparison.OrdinalIgnoreCase) && !a.Equals("-oz-api-restart", StringComparison.OrdinalIgnoreCase)) return;
            }
            string chip = SystemInfo.graphicsDeviceName ?? "";
            bool slow = IsSlowChip(chip), any = false, asPicked = true;
            for (int i = 0; i < Keys.Length; i++)
            {
                if (PlayerPrefs.HasKey(Keys[i])) any = true;
                if (PlayerPrefs.GetInt(Keys[i], int.MinValue) != Pick[i]) asPicked = false;
            }
            switch (Decide(PlayerPrefs.GetInt(MarkKey, 0), slow, any, asPicked))
            {
                case Step.Pick:
                    for (int i = 0; i < Keys.Length; i++) PlayerPrefs.SetInt(Keys[i], Pick[i]);
                    PlayerPrefs.SetInt(MarkKey, 1); PlayerPrefs.SetInt(RowsKey, AllRows); PlayerPrefs.Save();
                    Debug.Log($"{Tag} first start on a slow graphics chip ({chip}): {Chosen} chosen and saved; the LIGHTEST FOR SLOW PCS rows belong to LOW (a higher GRAPHICS QUALITY puts them back on their defaults); change them any time in OPTIONS > GRAPHICS");
                    break;
                case Step.TakeBack:
                    foreach (var k in Keys) PlayerPrefs.DeleteKey(k);
                    PlayerPrefs.DeleteKey(MarkKey); PlayerPrefs.DeleteKey(RowsKey); PlayerPrefs.Save();
                    Debug.Log($"{Tag} the game now runs on {chip}: the settings chosen for the slow graphics chip ({Chosen}) are back on their defaults");
                    break;
                case Step.PlayersOwn:
                    PlayerPrefs.SetInt(MarkKey, 2); PlayerPrefs.Save();
                    Debug.Log($"{Tag} slow graphics chip start: the player has his own graphics settings, nothing is chosen for him{(slow ? " (" + chip + ")" : "")}");
                    break;
            }
            // The rows chosen for LOW: a start above LOW (a quit in the same frame as the change, a settings copy put back) sets them back
            // here, before any setting is read (OzLightRows reads its values at BeforeSceneLoad); while some still stand, the watcher
            // follows OPTIONS.
            int had = PlayerPrefs.GetInt(RowsKey, 0);
            int back = Settle(new Engine(), out liveRows);
            if (back != 0 || had != PlayerPrefs.GetInt(RowsKey, 0)) PlayerPrefs.Save();
            if (back != 0) Debug.Log(BackLine(PlayerPrefs.GetInt(OzQuality.Pref, 0), back));
        }

        /// <summary>The watcher, only while some row still holds the value chosen for LOW (never on a fast chip, never for the player's own settings).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Host()
        {
            if (liveRows == 0 || Application.isEditor) return;
            var go = new GameObject("OzSlowChipWatch") { hideFlags = HideFlags.HideInHierarchy };
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<OzSlowChipWatch>();
        }

        static readonly int[] live = new int[5];                                         // 1.5 kit issue8_slow_chip_picture: + 3D PICTURE

        /// <summary>Once a frame (OzSlowChipWatch): a row the player changed becomes his; a GRAPHICS QUALITY above LOW sets the rows still
        /// as chosen back on their defaults, live (saved values + OzLightRows.Reload, the path the 1.3 light rows check uses to put saved
        /// values in force). False = nothing left to watch.</summary>
        internal static bool Tick()
        {
            if (liveRows == 0) return false;
            live[0] = OzLightRows.ShadowsOn ? 0 : 1; live[1] = OzLightRows.AoOn ? 0 : 1; live[2] = OzLightRows.Trees; live[3] = OzLightRows.WrecksLight ? 1 : 0; live[4] = OzLowPicture.Value;   // 1.5 kit issue8_slow_chip_picture
            int now = StillChosen(liveRows, live);
            int q = OzQuality.Level, back = GoBack(now, q);
            if (now == liveRows && back == 0) return true;
            for (int i = 0; i < RowKeys.Length; i++) if ((back & (1 << i)) != 0) PlayerPrefs.SetInt(RowKeys[i], 0);
            liveRows = now & ~back;
            PlayerPrefs.SetInt(RowsKey, liveRows); PlayerPrefs.Save();
            if (back != 0) { OzLightRows.Reload(); OzLowPicture.Reload(); Debug.Log(BackLine(q, back)); }   // 1.5 kit issue8_slow_chip_picture: 3D PICTURE back to SHARP too (OzLowScale follows the next frame)
            return liveRows != 0;
        }
    }

    /// <summary>Follows OPTIONS while a LIGHTEST FOR SLOW PCS row still holds the value the slow graphics chip start chose for LOW (made by
    /// OzSlowChipStart; gone once no such row is left).</summary>
    public sealed class OzSlowChipWatch : MonoBehaviour
    {
        void Update() { if (!OzSlowChipStart.Tick()) Destroy(gameObject); }
    }
}
