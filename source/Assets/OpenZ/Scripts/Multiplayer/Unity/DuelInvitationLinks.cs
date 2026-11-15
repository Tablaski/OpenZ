using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace OpenZ.Multiplayer
{
    public static class DuelInvitationLinks
    {
        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
        static extern int RegCreateKeyEx(IntPtr key, string subkey, int reserved, string keyClass, int options, int access, IntPtr security, out IntPtr result, out int disposition);
        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
        static extern int RegSetValueEx(IntPtr key, string name, int reserved, int type, byte[] data, int length);
        [DllImport("advapi32.dll")] static extern int RegCloseKey(IntPtr key);
        static void SetValue(string path, string name, string value)
        {
            if (RegCreateKeyEx(new IntPtr(unchecked((int)0x80000001)), path, 0, null, 0, 0x20006, IntPtr.Zero, out var key, out _) != 0)
                throw new IOException("Unable to register invitation links.");
            try
            {
                var bytes = Encoding.Unicode.GetBytes(value + "\0");
                if (RegSetValueEx(key, name, 0, 1, bytes, bytes.Length) != 0) throw new IOException("Unable to register invitation links.");
            }
            finally { RegCloseKey(key); }
        }
        public static void Register()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            // Per-user URL association: no administrator rights or background process.
            try
            {
                string executable = Path.GetFullPath(Path.Combine(Application.dataPath, "..", Application.productName + ".exe"));
                using (var process = System.Diagnostics.Process.GetCurrentProcess()) executable = process.MainModule.FileName;
                SetValue(@"Software\Classes\openz", "", "URL:OpenZ Duel invitation");
                SetValue(@"Software\Classes\openz", "URL Protocol", "");
                SetValue(@"Software\Classes\openz\shell\open\command", "", "\"" + executable + "\" \"%1\"");
            }
            catch (Exception) { Debug.LogWarning("OpenZ invitation links could not be registered. Room codes can still be pasted into Play Online."); }
#endif
        }
    }
}
