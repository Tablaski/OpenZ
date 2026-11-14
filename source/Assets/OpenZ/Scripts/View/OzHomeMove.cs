using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using System.Runtime.InteropServices;
#endif

namespace OpenZ
{
    /// <summary>
    /// 1.3: the project's company name became OpenZ, and Unity builds the save folder and the settings place from that name. What a
    /// build or an editor left under the old name is copied here once, at the first start: files when the new folder holds none of the
    /// game's own, settings when the new place holds none of the game's own. The old place is left alone, nothing is overwritten and
    /// nothing is asked. Runs in the one start slot no other hook uses (AfterAssembliesLoaded): the first read of a setting is
    /// OzWeakPcOptions.Boot, BeforeSplashScreen. Settings go through PlayerPrefs, so the engine's own copy of them stays right.
    /// Windows reads the registry, Linux the prefs file of the old folder. A Mac keeps settings under the bundle id, which never
    /// carried the company, so there is no file to read and only the folder moves. Unity's own values (screen, session ids) stay.
    /// </summary>
    public static class OzHomeMove
    {
        public const string Done = "oz_home_moved";
        /// <summary>FNV-1a of the old company name: the name itself stays out of the game, its dll and the public source.</summary>
        public const uint Old = 0x07B73C39;
        /// <summary>The engine's or this session's own, already in the new folder at the first start, and not worth a copy.</summary>
        static readonly string[] EngineOwn = { "Player.log", "Player-prev.log", OzCrashLog.Marker, "prefs", "Unity" };

        internal sealed class Pref { public string name, s; public int i; public float f; public char kind; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        public static void Run()
        {
            if (Hash(Application.companyName) == Old || PlayerPrefs.GetInt(Done, 0) == 1) return;
            try
            {
                string now = Application.persistentDataPath, was = FindOld(now);
                if (was != null)
                {
                    int files = CopyFiles(was, now), prefs = CopyPrefs(was, now);
                    if (files + prefs > 0) Debug.Log("[OpenZ] settings and saves of an older build copied: " + prefs + " settings, " + files + " files");
                }
                PlayerPrefs.SetInt(Done, 1);
                PlayerPrefs.Save();
            }
            catch (Exception e) { Debug.Log("[OpenZ] settings and saves of an older build stay where they are (" + e.GetType().Name + ")"); }
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        static void EditorStart() => Run();
#endif

        internal static uint Hash(string s)
        {
            uint h = 2166136261;
            foreach (char c in s ?? "") { h ^= c; h *= 16777619; }
            return h;
        }

        /// <summary>The old home next to this one: its folder's name hashes to <see cref="Old"/>. Null when there is none.</summary>
        static string FindOld(string now)
        {
            string product = Path.GetFileName(now), root = Path.GetDirectoryName(Path.GetDirectoryName(now));
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) return null;
            foreach (string d in Directory.GetDirectories(root))
                if (Hash(Path.GetFileName(d)) == Old && Directory.Exists(Path.Combine(d, product))) return Path.Combine(d, product);
            return null;
        }

        static bool EngineFile(string name) => Array.Exists(EngineOwn, e => e.Equals(name, StringComparison.OrdinalIgnoreCase));

        static bool HasGameFiles(string home)
        {
            if (!Directory.Exists(home)) return false;
            foreach (string e in Directory.GetFileSystemEntries(home)) if (!EngineFile(Path.GetFileName(e))) return true;
            return false;
        }

        static int CopyFiles(string from, string to)
        {
            if (HasGameFiles(to)) return 0;
            char[] seps = { '/', Path.DirectorySeparatorChar };
            int n = 0;
            foreach (string f in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
            {
                string rel = f.Substring(from.Length).TrimStart(seps);
                int cut = rel.IndexOfAny(seps);
                if (EngineFile(cut < 0 ? rel : rel.Substring(0, cut))) continue;
                string target = Path.Combine(to, rel);
                try { Directory.CreateDirectory(Path.GetDirectoryName(target)); File.Copy(f, target, false); n++; }
                catch (IOException) { }                                                     // locked or already there: the old copy stays
                catch (UnauthorizedAccessException) { }
            }
            return n;
        }

        static bool IsGame(string name) => !name.StartsWith("unity", StringComparison.OrdinalIgnoreCase) && !name.StartsWith("Screenmanager", StringComparison.OrdinalIgnoreCase);

        static int CopyPrefs(string from, string to)
        {
            if (Read(to).Exists(p => IsGame(p.name))) return 0;
            int n = 0;
            foreach (Pref p in Read(from))
            {
                if (!IsGame(p.name)) continue;
                if (p.kind == 'i') PlayerPrefs.SetInt(p.name, p.i);
                else if (p.kind == 'f') PlayerPrefs.SetFloat(p.name, p.f);
                else PlayerPrefs.SetString(p.name, p.s);
                n++;
            }
            return n;
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        const int KeyRead = 0x20019, TypeBinary = 3, TypeDword = 4, TypeQword = 11, MoreData = 234;

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
        static extern int RegOpenKeyExW(IntPtr root, string sub, int options, int access, out IntPtr key);
        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
        static extern int RegEnumValueW(IntPtr key, int index, [Out] char[] name, ref int nameLen, IntPtr reserved, out int type, [Out] byte[] data, ref int dataLen);
        [DllImport("advapi32.dll")]
        static extern int RegCloseKey(IntPtr key);

        /// <summary>Every value of a home's settings key, read only. No such key: an empty list.</summary>
        internal static List<Pref> Read(string home)
        {
            var list = new List<Pref>();
            string sub = (Application.isEditor ? @"Software\Unity\UnityEditor\" : @"Software\") + Path.GetFileName(Path.GetDirectoryName(home)) + @"\" + Path.GetFileName(home);
            if (RegOpenKeyExW(new IntPtr(unchecked((int)0x80000001)), sub, 0, KeyRead, out IntPtr key) != 0) return list;      // HKEY_CURRENT_USER
            try
            {
                var name = new char[16384];
                var data = new byte[1 << 16];
                for (int i = 0; ;)
                {
                    int nameLen = name.Length, dataLen = data.Length;
                    int r = RegEnumValueW(key, i, name, ref nameLen, IntPtr.Zero, out int type, data, ref dataLen);
                    if (r == MoreData && data.Length < 1 << 26) { data = new byte[data.Length * 4]; continue; }
                    if (r != 0) break;
                    i++;
                    Pref p = FromRegistry(new string(name, 0, nameLen), type, data, dataLen);
                    if (p != null) list.Add(p);
                }
            }
            finally { RegCloseKey(key); }
            return list;
        }

        /// <summary>Unity's registry form: the name ends in _h and a hash, an int is 4 bytes, a float 8 (a double, marked as 4 byte),
        /// a string is UTF-8 with a closing zero.</summary>
        static Pref FromRegistry(string raw, int type, byte[] d, int n)
        {
            int cut = raw.LastIndexOf("_h", StringComparison.Ordinal);
            string name = cut > 0 ? raw.Substring(0, cut) : raw;
            if (type == TypeDword && n == 4) return new Pref { name = name, kind = 'i', i = BitConverter.ToInt32(d, 0) };
            if ((type == TypeDword || type == TypeQword) && n == 8) return new Pref { name = name, kind = 'f', f = (float)BitConverter.ToDouble(d, 0) };
            if (type == TypeBinary) return new Pref { name = name, kind = 's', s = Encoding.UTF8.GetString(d, 0, n > 0 && d[n - 1] == 0 ? n - 1 : n) };
            return null;
        }
#else
        static readonly Regex Row = new Regex(@"<pref name=""([^""]*)"" type=""(\w+)"">([^<]*)</pref>");

        /// <summary>Every row of the prefs file in a home folder, read only. No such file (a Mac, a first start): an empty list.</summary>
        internal static List<Pref> Read(string home)
        {
            var list = new List<Pref>();
            string file = Path.Combine(home, "prefs");
            if (!File.Exists(file)) return list;
            foreach (Match m in Row.Matches(File.ReadAllText(file)))
            {
                string name = WebUtility.HtmlDecode(m.Groups[1].Value), kind = m.Groups[2].Value, v = m.Groups[3].Value;
                if (kind == "int" && int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i)) list.Add(new Pref { name = name, kind = 'i', i = i });
                else if (kind == "float" && float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out float f)) list.Add(new Pref { name = name, kind = 'f', f = f });
                else if (kind == "string") list.Add(new Pref { name = name, kind = 's', s = Text(v) });
            }
            return list;
        }

        /// <summary>Strings in that file are base 64.</summary>
        static string Text(string v)
        {
            try { return Encoding.UTF8.GetString(Convert.FromBase64String(v)); }
            catch (FormatException) { return WebUtility.HtmlDecode(v); }
        }
#endif
    }
}
