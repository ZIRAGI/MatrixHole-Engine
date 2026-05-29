using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace MatrixHole.Sys
{
    public static class ProcessOptimizer
    {
        [DllImport("kernel32.dll")]
        private static extern bool SetProcessPriorityClass(IntPtr hProcess, uint dwPriorityClass);

        [DllImport("kernel32.dll")]
        private static extern bool SetProcessAffinityMask(IntPtr hProcess, IntPtr dwProcessAffinityMask);

        [DllImport("kernel32.dll")]
        private static extern bool EmptyWorkingSet(IntPtr hProcess);

        [DllImport("ntdll.dll")]
        private static extern int NtSetTimerResolution(uint DesiredResolution, bool SetResolution, out uint CurrentResolution);

        private const uint REALTIME_PRIORITY_CLASS = 0x00000100;
        private const uint HIGH_PRIORITY_CLASS = 0x00000080;
        private const uint ABOVE_NORMAL_PRIORITY_CLASS = 0x00008000;

        public static bool SetHighPriority(string processName = "SCPSL")
        {
            var procs = Process.GetProcessesByName(processName);
            if (procs.Length == 0) return false;
            return SetProcessPriorityClass(procs[0].Handle, HIGH_PRIORITY_CLASS);
        }

        public static bool SetRealtimePriority(string processName = "SCPSL")
        {
            var procs = Process.GetProcessesByName(processName);
            if (procs.Length == 0) return false;
            return SetProcessPriorityClass(procs[0].Handle, REALTIME_PRIORITY_CLASS);
        }

        public static bool SetAffinityPhysicalCores(string processName = "SCPSL")
        {
            var procs = Process.GetProcessesByName(processName);
            if (procs.Length == 0) return false;
            var mask = GetPhysicalCoreMask();
            return SetProcessAffinityMask(procs[0].Handle, (IntPtr)mask);
        }

        public static bool ClearWorkingSets(string processName = "SCPSL")
        {
            var procs = Process.GetProcessesByName(processName);
            if (procs.Length == 0) return false;
            return EmptyWorkingSet(procs[0].Handle);
        }

        public static bool SetTimerResolution(uint desired = 5000) // 0.5ms = 5000*100ns
        {
            try
            {
                NtSetTimerResolution(desired, true, out _);
                return true;
            }
            catch { return false; }
        }

        public static int KillServices()
        {
            string[] targets = { "DiagTrack", "dmwappushservice", "WSearch", "WerSvc", "wisvc" };
            int killed = 0;
            foreach (var svc in targets)
            {
                try
                {
                    var sc = new System.ServiceProcess.ServiceController(svc);
                    if (sc.Status == System.ServiceProcess.ServiceControllerStatus.Running)
                    {
                        sc.Stop();
                        killed++;
                    }
                }
                catch { }
            }
            return killed;
        }

        private static ulong GetPhysicalCoreMask()
        {
            try
            {
                var procCount = Environment.ProcessorCount;
                // Simple heuristic: if HT, use even cores
                if (procCount > 4 && (procCount & (procCount - 1)) != 0) // not power of two = likely HT
                {
                    ulong mask = 0;
                    for (int i = 0; i < procCount; i += 2) mask |= (1UL << i);
                    return mask;
                }
                return (1UL << procCount) - 1;
            }
            catch { return (1UL << Environment.ProcessorCount) - 1; }
        }

        public static void AutoOptimizeLoop(CancellationToken token)
        {
            Task.Run(() =>
            {
                while (!token.IsCancellationRequested)
                {
                    var procs = Process.GetProcessesByName("SCPSL");
                    if (procs.Length > 0)
                    {
                        SetHighPriority();
                        SetAffinityPhysicalCores();
                        ClearWorkingSets();
                    }
                    Thread.Sleep(5000);
                }
            }, token);
        }
    }
}
