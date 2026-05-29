using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MatrixHole.Core
{
    /// <summary>
    /// CrashScreenshot — скриншот экрана за секунду до падения (п.81 OptimizatorPlan).
    /// </summary>
    public static class CrashScreenshot
    {
        public static string CaptureScreen()
        {
            try
            {
                var bounds = Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, 1920, 1080);
                using var bmp = new Bitmap(bounds.Width, bounds.Height);
                using var g = Graphics.FromImage(bmp);
                g.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);

                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "MatrixHole", "crash_screenshots");
                Directory.CreateDirectory(dir);
                var path = Path.Combine(dir, $"crash_{DateTime.Now:yyyyMMdd_HHmmss}.png");
                bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                return path;
            }
            catch (Exception ex)
            {
                return $"ERROR: {ex.Message}";
            }
        }

        public static async Task MonitorForCrashAsync(CancellationToken token)
        {
            bool wasRunning = false;
            while (!token.IsCancellationRequested)
            {
                var running = System.Diagnostics.Process.GetProcessesByName("SCPSL").Any();
                if (wasRunning && !running)
                {
                    // Process died — take screenshot immediately
                    var path = CaptureScreen();
                    if (!path.StartsWith("ERROR"))
                    {
                        TrayIconManager.ShowBalloon("Crash Detected", $"Screenshot saved: {path}", System.Windows.Forms.ToolTipIcon.Warning, 5000);
                    }
                }
                wasRunning = running;
                await Task.Delay(2000, token);
            }
        }
    }
}
