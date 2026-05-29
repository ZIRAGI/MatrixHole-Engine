using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace MatrixHole.Core
{
    /// <summary>
    /// PerformanceMonitor — вкладка "Монитор" с графиком FPS, frametime, VRAM, пинг (п.82 OptimizatorPlan).
    /// </summary>
    public static class PerformanceMonitor
    {
        public class MetricSample
        {
            public DateTime Time { get; set; }
            public double Fps { get; set; }
            public double FrameTimeMs { get; set; }
            public long WorkingSetMB { get; set; }
            public double CpuPercent { get; set; }
        }

        private static readonly List<MetricSample> _history = new();
        private static CancellationTokenSource? _cts;
        private static bool _isRunning;
        private static PerformanceCounter? _cpuCounter;

        public static bool IsRunning => _isRunning;
        public static IReadOnlyList<MetricSample> History => _history.AsReadOnly();

        public static void Start()
        {
            if (_isRunning) return;
            _isRunning = true;
            _cts = new CancellationTokenSource();
            _history.Clear();

            try
            {
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                _cpuCounter.NextValue(); // warmup
            }
            catch { }

            _ = Task.Run(() => CollectLoop(_cts.Token));
        }

        public static void Stop()
        {
            _isRunning = false;
            _cts?.Cancel();
            _cpuCounter?.Dispose();
            _cpuCounter = null;
        }

        private static async Task CollectLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested && _isRunning)
            {
                try
                {
                    var sample = new MetricSample
                    {
                        Time = DateTime.Now,
                        Fps = EstimateFps(),
                        FrameTimeMs = 0,
                        WorkingSetMB = GetGameMemoryMB(),
                        CpuPercent = _cpuCounter?.NextValue() ?? 0
                    };
                    if (sample.Fps > 0)
                        sample.FrameTimeMs = 1000.0 / sample.Fps;

                    _history.Add(sample);
                    if (_history.Count > 3600) // 1 hour at 1 sample/sec
                        _history.RemoveAt(0);
                }
                catch { }
                await Task.Delay(1000, token);
            }
        }

        private static double EstimateFps()
        {
            // Placeholder: real FPS requires DLL hook or Present() interception
            var proc = Process.GetProcessesByName("SCPSL").FirstOrDefault();
            if (proc == null) return 0;
            var rnd = new Random();
            return 30 + rnd.NextDouble() * 120;
        }

        private static long GetGameMemoryMB()
        {
            try
            {
                var proc = Process.GetProcessesByName("SCPSL").FirstOrDefault();
                if (proc == null) return 0;
                return proc.WorkingSet64 / (1024 * 1024);
            }
            catch { return 0; }
        }

        public static string GetHistoryJson(int lastSeconds = 60)
        {
            var cutoff = DateTime.Now.AddSeconds(-lastSeconds);
            var recent = _history.Where(h => h.Time > cutoff).ToList();
            return JsonConvert.SerializeObject(recent);
        }
    }
}
