using System;
using System.IO;
using Newtonsoft.Json;

namespace MatrixHole.Core
{
    /// <summary>
    /// DiskSpaceMonitor — авто-оптимизация диска (п.96 OptimizatorPlan).
    /// Проверка свободного места, предупреждение если меньше 10%.
    /// </summary>
    public static class DiskSpaceMonitor
    {
        public static string CheckGameDisk()
        {
            var game = GameDetector.Detect();
            if (game == null || !game.IsInstalled)
                return "{\"error\":\"Game not found\"}";

            return CheckDisk(game.InstallPath);
        }

        public static string CheckDisk(string path)
        {
            try
            {
                var root = Path.GetPathRoot(path);
                if (string.IsNullOrEmpty(root))
                    return "{\"error\":\"Invalid path\"}";

                var drive = new DriveInfo(root);
                var totalGB = drive.TotalSize / (1024.0 * 1024 * 1024);
                var freeGB = drive.AvailableFreeSpace / (1024.0 * 1024 * 1024);
                var usedPercent = ((drive.TotalSize - drive.AvailableFreeSpace) / (double)drive.TotalSize) * 100;
                var freePercent = (drive.AvailableFreeSpace / (double)drive.TotalSize) * 100;
                var isLow = freePercent < 10;

                return JsonConvert.SerializeObject(new
                {
                    drive = root,
                    total_gb = Math.Round(totalGB, 2),
                    free_gb = Math.Round(freeGB, 2),
                    used_percent = Math.Round(usedPercent, 1),
                    free_percent = Math.Round(freePercent, 1),
                    is_low_space = isLow,
                    warning = isLow ? "Мало места на диске с игрой (<10%). Игра может фризить при подгрузке ассетов." : null
                });
            }
            catch (Exception ex)
            {
                return $"{{\"error\":\"{ex.Message.Replace("\"", "'")}\"}}";
            }
        }

        public static bool IsLowSpace(string path, double thresholdPercent = 10.0)
        {
            try
            {
                var root = Path.GetPathRoot(path);
                if (string.IsNullOrEmpty(root)) return false;
                var drive = new DriveInfo(root);
                var freePercent = (drive.AvailableFreeSpace / (double)drive.TotalSize) * 100;
                return freePercent < thresholdPercent;
            }
            catch { return false; }
        }
    }
}
