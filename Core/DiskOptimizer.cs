using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;

namespace MatrixHole.Core
{
    /// <summary>
    /// DiskOptimizer — авто-дефрагментация / TRIM (п.94 OptimizatorPlan).
    /// HDD → дефрагментация папки игры. SSD → TRIM.
    /// </summary>
    public static class DiskOptimizer
    {
        public enum DiskType { SSD, HDD, Unknown }

        public static DiskType GetDiskTypeForPath(string path)
        {
            try
            {
                var drive = Path.GetPathRoot(path);
                if (string.IsNullOrEmpty(drive)) return DiskType.Unknown;

                using var searcher = new ManagementObjectSearcher(
                    "SELECT * FROM Win32_DiskDrive WHERE InterfaceType != 'USB'");
                foreach (ManagementObject disk in searcher.Get())
                {
                    var model = disk["Model"]?.ToString()?.ToLower() ?? "";
                    if (model.Contains("ssd") || model.Contains("nvme") || model.Contains("solid state"))
                        return DiskType.SSD;
                }
                return DiskType.HDD;
            }
            catch { return DiskType.Unknown; }
        }

        public static string AnalyzeGameDisk()
        {
            var game = GameDetector.Detect();
            if (game == null || !game.IsInstalled) return "{\"error\":\"Game not found\"}";

            var diskType = GetDiskTypeForPath(game.InstallPath);
            var drive = new DriveInfo(Path.GetPathRoot(game.InstallPath)!);

            return $"{{\"disk_type\":\"{diskType}\",\"free_space_gb\":{drive.AvailableFreeSpace / (1024.0 * 1024 * 1024):F1},\"total_size_gb\":{drive.TotalSize / (1024.0 * 1024 * 1024):F1},\"recommended_action\":\"{(diskType == DiskType.HDD ? "defrag" : diskType == DiskType.SSD ? "trim" : "unknown")}\"}}";
        }

        public static bool RunDefrag(string folderPath)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "defrag.exe",
                    Arguments = $"\"{folderPath}\" /U /V",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    Verb = "runas"
                };
                var proc = Process.Start(psi);
                proc?.WaitForExit();
                return proc?.ExitCode == 0;
            }
            catch { return false; }
        }

        public static bool RunTrim(string driveLetter)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "defrag.exe",
                    Arguments = $"{driveLetter.TrimEnd('\\', ':')} /L /U /V",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    Verb = "runas"
                };
                var proc = Process.Start(psi);
                proc?.WaitForExit();
                return proc?.ExitCode == 0;
            }
            catch { return false; }
        }

        public static bool CheckLowDiskSpace(string path, double thresholdPercent = 10.0)
        {
            try
            {
                var drive = new DriveInfo(Path.GetPathRoot(path)!);
                var freePercent = (drive.AvailableFreeSpace / (double)drive.TotalSize) * 100;
                return freePercent < thresholdPercent;
            }
            catch { return false; }
        }

        public static string GetDiskSpaceWarning(string path)
        {
            if (CheckLowDiskSpace(path, 10))
                return "Мало места на диске с игрой (<10%). Игра может фризить при подгрузке ассетов.";
            return "";
        }
    }
}
