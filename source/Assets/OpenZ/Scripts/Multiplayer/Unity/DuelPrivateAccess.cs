using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace OpenZ.Multiplayer
{
    /// <summary>Private relay access is local to the Windows user, never a PlayerPrefs or build secret.</summary>
    public static class DuelPrivateAccess
    {
        [StructLayout(LayoutKind.Sequential)] struct Blob { public int Size; public IntPtr Data; }
        [DllImport("crypt32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool CryptProtectData(ref Blob input, IntPtr description, IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, out Blob output);
        [DllImport("crypt32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool CryptUnprotectData(ref Blob input, IntPtr description, IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, out Blob output);
        [DllImport("kernel32.dll")] static extern IntPtr LocalFree(IntPtr pointer);
        static string Location => Path.Combine(Application.persistentDataPath, "duel-private-access.bin");
        public static bool Enabled => File.Exists(Location);
        static byte[] Transform(byte[] bytes, bool protect)
        {
            var input = new Blob { Size = bytes.Length, Data = Marshal.AllocHGlobal(bytes.Length) };
            try
            {
                Marshal.Copy(bytes, 0, input.Data, bytes.Length);
                Blob output;
                bool ok = protect ? CryptProtectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out output) :
                    CryptUnprotectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out output);
                if (!ok) throw new IOException("Unable to save private access for this Windows user.");
                try { var result = new byte[output.Size]; Marshal.Copy(output.Data, result, 0, result.Length); return result; }
                finally { LocalFree(output.Data); }
            }
            finally { Marshal.FreeHGlobal(input.Data); }
        }
        public static string Read()
        {
            string testToken = Environment.GetEnvironmentVariable("OPENZ_DUEL_RELAY_ACCESS");
            if (!string.IsNullOrEmpty(testToken)) return testToken;
            if (!Enabled) return "";
            try { return Encoding.UTF8.GetString(Transform(File.ReadAllBytes(Location), false)); }
            catch (Exception) { return ""; }
        }
        public static void Save(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) { if (Enabled) File.Delete(Location); return; }
            token = token.Trim();
            if (token.Length < 32 || token.Length > 256) throw new ArgumentException("Paste the complete private access code.");
            Directory.CreateDirectory(Application.persistentDataPath);
            File.WriteAllBytes(Location, Transform(Encoding.UTF8.GetBytes(token), true));
        }
    }
}
