using System;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace MatrixHole.Core
{
    /// <summary>
    /// LogRotation — ротация логов (п.74 OptimizatorPlan).
    /// Ограничение по размеру, архивация старых сессий.
    /// </summary>
    public static class LogRotation
    {
        private static readonly string LogDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MatrixHole", "logs");

        private const long MaxTotalSizeBytes = 50 * 1024 * 1024; // 50 MB
        private const long MaxSingleFileSizeBytes = 5 * 1024 * 1024; // 5 MB
        private const int MaxLogFiles = 20;

        public static void Rotate()
        {
            try
            {
                if (!Directory.Exists(LogDir)) return;

                var files = Directory.GetFiles(LogDir, "*.log")
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(f => f.LastWriteTime)
                    .ToList();

                // Archive old logs (> 30 days)
                var archiveDir = Path.Combine(LogDir, "archive");
                Directory.CreateDirectory(archiveDir);

                foreach (var file in files.Where(f => f.LastWriteTime < DateTime.Now.AddDays(-30)))
                {
                    var zipPath = Path.Combine(archiveDir, $"{file.Name}.gz");
                    using (var source = file.OpenRead())
                    using (var target = File.Create(zipPath))
                    using (var gzip = new GZipStream(target, CompressionLevel.Optimal))
                    {
                        source.CopyTo(gzip);
                    }
                    file.Delete();
                }

                // Delete oldest if too many files
                if (files.Count > MaxLogFiles)
                {
                    foreach (var file in files.Skip(MaxLogFiles))
                    {
                        try { file.Delete(); } catch { }
                    }
                }

                // Check total size and trim
                var totalSize = Directory.GetFiles(LogDir, "*.log", SearchOption.TopDirectoryOnly)
                    .Sum(f => new FileInfo(f).Length);
                while (totalSize > MaxTotalSizeBytes && files.Count > 1)
                {
                    var oldest = files.Last();
                    totalSize -= oldest.Length;
                    try { oldest.Delete(); files.RemoveAt(files.Count - 1); } catch { break; }
                }

                // Rotate current log if too large
                var currentLog = Path.Combine(LogDir, "session.log");
                if (File.Exists(currentLog) && new FileInfo(currentLog).Length > MaxSingleFileSizeBytes)
                {
                    var rotated = Path.Combine(LogDir, $"session_{DateTime.Now:yyyyMMdd_HHmmss}.log");
                    File.Move(currentLog, rotated);
                }
            }
            catch { }
        }

        public static long GetTotalLogSize()
        {
            try
            {
                if (!Directory.Exists(LogDir)) return 0;
                return Directory.GetFiles(LogDir, "*.log", SearchOption.AllDirectories)
                    .Sum(f => new FileInfo(f).Length);
            }
            catch { return 0; }
        }
    }
}
