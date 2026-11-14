// OpenZ (GPL-3.0) - version 1.3, item 25 (g): the heat guard's temperature read inside the game.
// The graphics driver ships its own management library (nvml.dll, in the Windows system folder with every current driver;
// nvidia-smi is a small front end of the same library). Reading it takes microseconds and starts no program, where the old way
// started nvidia-smi.exe every 2 s for the whole session. Same reading: GPU 0 (nvidia-smi without -i lists GPU 0 first), the
// core sensor (temperature.gpu = NVML_TEMPERATURE_GPU), whole degrees C. Any failure (no such library, an older driver, an
// error code) returns false, and HeatGuardRuntime goes on with nvidia-smi exactly as before. Windows players only.
using System;
using System.Runtime.InteropServices;

namespace OpenZ
{
    public static class OzNvml
    {
        const string Lib = "nvml.dll";
        const int Success = 0, SensorGpu = 0;
        [DllImport(Lib, EntryPoint = "nvmlInit_v2")] static extern int Init();
        [DllImport(Lib, EntryPoint = "nvmlShutdown")] static extern int Shutdown();
        [DllImport(Lib, EntryPoint = "nvmlDeviceGetHandleByIndex_v2")] static extern int HandleByIndex(uint index, out IntPtr device);
        [DllImport(Lib, EntryPoint = "nvmlDeviceGetTemperature")] static extern int Temperature(IntPtr device, int sensor, out uint celsius);

        static IntPtr device = IntPtr.Zero;
        static bool open;

        /// <summary>Opens the library and finds GPU 0 (the heat guard's thread). False = not available here.</summary>
        public static bool Open()
        {
            try
            {
                if (Init() != Success) return false;
                if (HandleByIndex(0, out device) != Success || device == IntPtr.Zero) { Shutdown(); return false; }
                open = true;
                return Read(out _);                                                                    // one reading must work, or the old way runs
            }
            catch (Exception) { open = false; return false; }                                           // DllNotFoundException, EntryPointNotFoundException, ...
        }

        /// <summary>GPU 0's core temperature in whole degrees C. False = the reading failed (the caller falls back).</summary>
        public static bool Read(out int celsius)
        {
            celsius = -1;
            if (!open) return false;
            try
            {
                if (Temperature(device, SensorGpu, out uint t) != Success) return false;
                celsius = (int)t; return true;
            }
            catch (Exception) { return false; }
        }

        public static void Close()
        {
            if (!open) return;
            open = false;
            try { Shutdown(); } catch (Exception) { }
        }
    }
}
