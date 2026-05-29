using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Newtonsoft.Json;

namespace MatrixHole.Core
{
    /// <summary>
    /// RTSSIntegration — вывод статистики твикера в оверлей MSI Afterburner / Rivatuner (п.77 OptimizatorPlan).
    /// Использует shared memory RTSS (RTSSSharedMemoryV2).
    /// </summary>
    public static class RTSSIntegration
    {
        private const uint RTSS_VERSION = 0x00020000;
        private const string RTSS_MEMORY_NAME = "RTSSSharedMemoryV2";

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct RTSS_APP_ENTRY
        {
            public uint dwProcessID;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szSrc;
        }

        public static bool IsRTSSRunning()
        {
            return System.Diagnostics.Process.GetProcessesByName("RTSS").Any();
        }

        public static string GetStatus()
        {
            return JsonConvert.SerializeObject(new
            {
                rtss_running = IsRTSSRunning(),
                note = "Full RTSS shared memory integration requires native RTSS API hooks. Placeholder."
            });
        }

        public static void PushOverlayText(string text)
        {
            if (!IsRTSSRunning()) return;
            // Placeholder: real implementation uses RTSS shared memory or OSD API
        }
    }
}
