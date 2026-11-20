using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace OpenZ
{
    internal static class OzWindowsSound
    {
        internal static bool Available => Application.platform == RuntimePlatform.WindowsPlayer ||
            Application.platform == RuntimePlatform.WindowsEditor;

        static double refreshAt;
        static string outputStatus;
        internal static string OutputStatus
        {
            get
            {
                if (outputStatus != null && Time.realtimeSinceStartupAsDouble < refreshAt) return outputStatus;
                refreshAt = Time.realtimeSinceStartupAsDouble + .5;
                outputStatus = "WINDOWS OUTPUT UNAVAILABLE";
                WithEndpoint(endpoint =>
                {
                    endpoint.GetMasterVolumeLevelScalar(out float level);
                    endpoint.GetMute(out bool muted);
                    outputStatus = "WINDOWS OUTPUT · " + Mathf.RoundToInt(level * 100) + "%" + (muted ? " · MUTED" : "");
                });
                return outputStatus;
            }
        }

        internal static void StepVolume(float step)
        {
            WithEndpoint(endpoint =>
            {
                endpoint.GetMasterVolumeLevelScalar(out float level);
                endpoint.SetMasterVolumeLevelScalar(Mathf.Clamp01(level + step), IntPtr.Zero);
            });
            refreshAt = 0;
        }
        internal static void ToggleMute()
        {
            WithEndpoint(endpoint =>
            {
                endpoint.GetMute(out bool muted);
                endpoint.SetMute(!muted, IntPtr.Zero);
            });
            refreshAt = 0;
        }
        internal static void OpenSettings() => OpenWindowsPage("ms-settings:sound");
        internal static void OpenMixer() => OpenWindowsPage("ms-settings:apps-volume");
        static void OpenWindowsPage(string uri)
        {
            if (!Available) return;
            try
            {
                using var settings = System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo(uri) { UseShellExecute = true });
            }
            catch (System.ComponentModel.Win32Exception error)
            {
                Debug.LogWarning("[OpenZ Audio] " + error.Message);
            }
        }

        [DllImport("ole32.dll")] static extern int CoInitializeEx(IntPtr reserved, uint apartment);
        [DllImport("ole32.dll")] static extern void CoUninitialize();

        static void WithEndpoint(Action<IEndpointVolume> use)
        {
            if (!Available) return;
            // RPC_E_CHANGED_MODE means this thread already has a different COM apartment.
            int initialized = CoInitializeEx(IntPtr.Zero, 2);
            if (initialized < 0 && initialized != unchecked((int)0x80010106)) return;
            object enumerator = null, volume = null;
            IDevice device = null;
            try
            {
                enumerator = Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")));
                ((IDeviceEnumerator)enumerator).GetDefaultAudioEndpoint(0, 1, out device); // Render, multimedia
                Guid id = typeof(IEndpointVolume).GUID;
                device.Activate(ref id, 23, IntPtr.Zero, out volume); // CLSCTX_ALL
                use((IEndpointVolume)volume);
            }
            catch (COMException) { /* Windows can remove or switch the endpoint at any time. */ }
            finally
            {
                if (volume != null) Marshal.ReleaseComObject(volume);
                if (device != null) Marshal.ReleaseComObject(device);
                if (enumerator != null) Marshal.ReleaseComObject(enumerator);
                if (initialized >= 0) CoUninitialize();
            }
        }

        [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IDeviceEnumerator
        {
            void EnumAudioEndpoints(int flow, uint stateMask, [MarshalAs(UnmanagedType.Interface)] out object devices);
            void GetDefaultAudioEndpoint(int flow, int role, out IDevice device);
        }

        [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IDevice
        {
            void Activate(ref Guid id, uint context, IntPtr parameters, [MarshalAs(UnmanagedType.IUnknown)] out object instance);
        }

        [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IEndpointVolume
        {
            void RegisterControlChangeNotify(IntPtr notify);
            void UnregisterControlChangeNotify(IntPtr notify);
            void GetChannelCount(out uint count);
            void SetMasterVolumeLevel(float decibels, IntPtr context);
            void SetMasterVolumeLevelScalar(float level, IntPtr context);
            void GetMasterVolumeLevel(out float decibels);
            void GetMasterVolumeLevelScalar(out float level);
            void SetChannelVolumeLevel(uint channel, float decibels, IntPtr context);
            void SetChannelVolumeLevelScalar(uint channel, float level, IntPtr context);
            void GetChannelVolumeLevel(uint channel, out float decibels);
            void GetChannelVolumeLevelScalar(uint channel, out float level);
            void SetMute([MarshalAs(UnmanagedType.Bool)] bool muted, IntPtr context);
            void GetMute([MarshalAs(UnmanagedType.Bool)] out bool muted);
        }
    }
}
