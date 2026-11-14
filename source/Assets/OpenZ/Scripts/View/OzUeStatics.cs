using System;
using System.Collections.Generic;
using UnityEngine;

namespace OpenZ
{
    /// <summary>
    /// 1.2 item 6 (View/OzUnitEditor.cs, ARCHITECTURE 2.4): every static the playground changes (rule switches and tables shared by every
    /// world, the camera's free-look tilt, the time scale ...) goes through Hold: its value outside the playground is saved ONCE, the
    /// playground's value is set, and RestoreAll (OzUnitEditor.OnDestroy, when the editor closes) puts every saved value back, in the
    /// reverse order, each logged. A level start resets most rule switches itself, but not all of them (the calm start, the rifle pair
    /// table, the sniper values ...): nothing the playground changed may leak into the next match.
    /// </summary>
    public static class OzUeStatics
    {
        sealed class Held { public string name; public Action restore; public string before; }
        static readonly List<Held> held = new List<Held>();
        static readonly List<string> names = new List<string>();
        static readonly List<string> restoreLog = new List<string>();

        /// <summary>Saves the static's current value once (by name), then sets the playground's value. A second Hold of the same name only
        /// sets the new value (the saved one stays the value from before the playground).</summary>
        public static void Hold<T>(string name, Func<T> get, Action<T> set, T playgroundValue)
        {
            if (string.IsNullOrEmpty(name) || get == null || set == null) { Debug.LogError($"{OzUnitEditor.Tag} Hold without a name, getter or setter: ignored"); return; }
            Held h = null;
            foreach (var x in held) if (x.name == name) { h = x; break; }
            if (h == null)
            {
                T before = get();
                h = new Held { name = name, before = Show(before), restore = () => set(before) };
                held.Add(h); names.Add(name);
            }
            T was = get();
            set(playgroundValue);
            Debug.Log($"{OzUnitEditor.Tag} static {name}: {Show(was)} -> {Show(playgroundValue)} for the playground (restored to {h.before} when the editor closes)");
        }

        /// <summary>Every held static back to its value from before the playground, the last held first; the list is then empty.</summary>
        public static void RestoreAll()
        {
            restoreLog.Clear();
            for (int i = held.Count - 1; i >= 0; i--)
            {
                var h = held[i];
                try { h.restore(); restoreLog.Add(h.name + " = " + h.before); Debug.Log($"{OzUnitEditor.Tag} static {h.name} restored to {h.before}"); }
                catch (Exception e) { Debug.LogError($"{OzUnitEditor.Tag} static {h.name} could not be restored: {e.Message}"); }
            }
            held.Clear(); names.Clear();
        }

        /// <summary>The statics held now, in the order they were first held.</summary>
        public static IReadOnlyList<string> Names => names;
        /// <summary>The last RestoreAll: "name = value" per static, in the order restored (the Play proof compares it with its snapshot).</summary>
        public static IReadOnlyList<string> LastRestored => restoreLog;

        static string Show<T>(T v) => v == null ? "null" : v is float f ? f.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
                                                         : v is double d ? d.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) : v.ToString();
    }
}
