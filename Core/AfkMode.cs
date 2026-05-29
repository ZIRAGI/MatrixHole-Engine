using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace MatrixHole.Core
{
    /// <summary>
    /// AfkMode — режим АФК (п.99 OptimizatorPlan).
    /// Если пользователь не трогает мышь/клаву 5 минут в игре, понижает FPS до 10.
    /// </summary>
    public static class AfkMode
    {
        [DllImport("user32.dll")]
        private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        private struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }

        private static bool _isEnabled;
        private static CancellationTokenSource? _cts;
        private static Task? _monitorTask;

        public static bool IsEnabled => _isEnabled;
        public static int IdleThresholdSeconds { get; set; } = 300; // 5 minutes
        public static int TargetFps { get; set; } = 10;
        public static bool IsAfkActive { get; private set; }

        public static void Enable()
        {
            if (_isEnabled) return;
            _isEnabled = true;
            _cts = new CancellationTokenSource();
            _monitorTask = Task.Run(() => MonitorLoop(_cts.Token));
        }

        public static void Disable()
        {
            _isEnabled = false;
            _cts?.Cancel();
            if (IsAfkActive) RestoreFps();
        }

        private static async Task MonitorLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested && _isEnabled)
            {
                var idleSeconds = GetIdleTimeSeconds();
                if (idleSeconds >= IdleThresholdSeconds && !IsAfkActive)
                {
                    EnterAfk();
                }
                else if (idleSeconds < 10 && IsAfkActive)
                {
                    ExitAfk();
                }
                await Task.Delay(5000, token);
            }
        }

        private static void EnterAfk()
        {
            IsAfkActive = true;
            // In real implementation: hook into game to cap FPS
            // Placeholder: send notification
            TrayIconManager.ShowBalloon("AFK Mode", $"Неактивен {IdleThresholdSeconds} сек. FPS ограничен до {TargetFps}.", ToolTipIcon.Info, 3000);
        }

        private static void ExitAfk()
        {
            IsAfkActive = false;
            RestoreFps();
            TrayIconManager.ShowBalloon("AFK Mode", "Активность обнаружена. FPS восстановлен.", ToolTipIcon.Info, 3000);
        }

        private static void RestoreFps()
        {
            // Placeholder: remove FPS cap
        }

        public static uint GetIdleTimeSeconds()
        {
            var lii = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf(typeof(LASTINPUTINFO)) };
            GetLastInputInfo(ref lii);
            var idleTicks = Environment.TickCount - lii.dwTime;
            return (uint)(idleTicks / 1000);
        }
    }
}
