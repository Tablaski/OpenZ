// OpenZ 1.4 speed-up 4: the frame sampler of the counted check (Editor/OzV14ViewScansCheck.cs), added only while that check runs.
// Per window (the old searches or the registries): frames, the engine's Update and LateUpdate script time, the main thread, the rules'
// own time (ZGame.RuleMsLastFrame), the managed memory allocated, and every line of the game's sound log (OzSoundLog) by its key
// and outcome. Reads only: it changes nothing in the game.
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

namespace OpenZ
{
    [DefaultExecutionOrder(2000)]
    public sealed class OzViewScansProbe : MonoBehaviour
    {
        public sealed class Window
        {
            public string Label; public bool On;
            public int Frames, UpdN, LateN, MainN, AllocN;
            public double Update, Late, Main, Rules, AllocKb;
            public float Seconds;
            public int Views, Live;
            public readonly Dictionary<string, int> Sounds = new Dictionary<string, int>();
            public double MeanUpdate => UpdN > 0 ? Update / UpdN : 0.0;
            public double MeanLate => LateN > 0 ? Late / LateN : 0.0;
            public double MeanMain => MainN > 0 ? Main / MainN : 0.0;
            public double MeanRules => Frames > 0 ? Rules / Frames : 0.0;
            public double MeanAllocKb => AllocN > 0 ? AllocKb / AllocN : 0.0;
            public int Plays { get { int n = 0; foreach (var kv in Sounds) if (kv.Key.EndsWith(" PLAY") || kv.Key.EndsWith(" LOOP")) n += kv.Value; return n; } }
        }

        public readonly List<Window> Windows = new List<Window>();
        Window cur, lastFrameWindow;
        float t0, excludeUntil, prevT = -1f;
        ProfilerRecorder upd, late, mainRec, gc;
        readonly FrameTiming[] ft = new FrameTiming[1];

        public static OzViewScansProbe Ensure()
        {
            var p = FindFirstObjectByType<OzViewScansProbe>();
            if (!p) { var go = new GameObject("OzViewScansProbe"); DontDestroyOnLoad(go); p = go.AddComponent<OzViewScansProbe>(); }
            return p;
        }

        public void Begin(string label, bool on)
        {
            cur = new Window { Label = label, On = on };
            Windows.Add(cur);
            t0 = Time.realtimeSinceStartup;
        }

        public Window End()
        {
            var w = cur; if (w == null) return null;
            w.Seconds = Time.realtimeSinceStartup - t0;
            var g = ZGame.I; if (g) { w.Views = g.Views.Count; w.Live = g.LiveViews.Count; }
            cur = null;
            return w;
        }

        /// <summary>The cap bench's army (View/OzUePlayground.Cap.cs BenchArmy, internal to the game's code) for the check in the editor.</summary>
        public static int Army(Team team, int units, Vector3 centre, float sign, int fromCol, out int nextCol)
            => OzUePlayground.BenchArmy(team, units, centre, Vector3.right, Vector3.forward, sign, fromCol, out nextCol);

        /// <summary>Frames in the next `seconds` are left out of the frame times (a top-up of the armies), not out of the sound count.</summary>
        public void Exclude(float seconds) { excludeUntil = Time.realtimeSinceStartup + seconds; }

        void OnEnable()
        {
            try { upd = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Update.ScriptRunBehaviourUpdate", 1); } catch (System.Exception) { }
            try { late = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "PreLateUpdate.ScriptRunBehaviourLateUpdate", 1); } catch (System.Exception) { }
            try { mainRec = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 1); } catch (System.Exception) { }
            try { gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1); } catch (System.Exception) { }
        }

        void OnDisable() { upd.Dispose(); late.Dispose(); mainRec.Dispose(); gc.Dispose(); }

        void LateUpdate()
        {
            // the sound log: every line of the frame before (all of them are written by now), counted for the window open then
            if (prevT >= 0f && lastFrameWindow != null)
                foreach (var e in OzSoundLog.Since(prevT))
                {
                    if (e.t != prevT) continue;
                    string o = e.outcome ?? "-"; int sp = o.IndexOf(' '); if (sp > 0) o = o.Substring(0, sp);
                    string k = (e.key ?? "-") + " " + o;
                    lastFrameWindow.Sounds.TryGetValue(k, out int n); lastFrameWindow.Sounds[k] = n + 1;
                }
            prevT = Time.unscaledTime; lastFrameWindow = cur;
            if (cur == null || Time.realtimeSinceStartup < excludeUntil) return;
            cur.Frames++;
            cur.Rules += ZGame.RuleMsLastFrame;
            if (upd.Valid && upd.LastValue > 0) { cur.Update += upd.LastValue * 1e-6; cur.UpdN++; }
            if (late.Valid && late.LastValue > 0) { cur.Late += late.LastValue * 1e-6; cur.LateN++; }
            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, ft) > 0 && ft[0].cpuMainThreadFrameTime > 0.0) { cur.Main += ft[0].cpuMainThreadFrameTime; cur.MainN++; }
            else if (mainRec.Valid && mainRec.LastValue > 0) { cur.Main += mainRec.LastValue * 1e-6; cur.MainN++; }
            if (gc.Valid) { cur.AllocKb += gc.LastValue / 1024.0; cur.AllocN++; }
        }
    }
}
