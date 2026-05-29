using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MatrixHole.Core
{
    /// <summary>
    /// GameCleanupManager — чистка мусора игры (п.71 OptimizatorPlan).
    /// Удаление шейдерного кэша Unity, логов крашей, временных файлов обновлений.
    /// </summary>
    public static class GameCleanupManager
    {
        public static List<CleanupTarget> GetCleanupTargets()
        {
            var targets = new List<CleanupTarget>();
            var game = GameDetector.Detect();
            string? gamePath = game?.InstallPath;

            // 1. Unity shader cache
            targets.Add(new CleanupTarget
            {
                Id = "shader_cache",
                Name = "Unity Shader Cache",
                Description = "Compiled shader cache. Safe to delete — recreated on launch.",
                Path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SCP Secret Laboratory", "ShaderCache"),
                IsSafe = true
            });

            // 2. Crash logs
            targets.Add(new CleanupTarget
            {
                Id = "crash_logs",
                Name = "Crash Logs",
                Description = "crash.dmp and output_log.txt from previous crashes.",
                Path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SCP Secret Laboratory", "CrashLogs"),
                IsSafe = true
            });

            // 3. Unity temp/update files
            if (!string.IsNullOrEmpty(gamePath))
            {
                targets.Add(new CleanupTarget
                {
                    Id = "temp_updates",
                    Name = "Update Temp Files",
                    Description = "Leftovers from interrupted Steam/game updates.",
                    Path = Path.Combine(gamePath, "_temp"),
                    IsSafe = true
                });
            }

            // 4. Unity log files
            targets.Add(new CleanupTarget
            {
                Id = "unity_logs",
                Name = "Unity Logs",
                Description = "Player.log and old session logs.",
                Path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "..", "LocalLow", "Northwood Studios", "SCP Secret Laboratory"),
                IsSafe = true
            });

            // 5. Downloaded workshop temp
            if (!string.IsNullOrEmpty(gamePath))
            {
                targets.Add(new CleanupTarget
                {
                    Id = "workshop_temp",
                    Name = "Workshop Temp Files",
                    Description = "Incomplete mod downloads from Steam Workshop.",
                    Path = Path.Combine(gamePath, "steamapps", "workshop", "downloads", "700330"),
                    IsSafe = true
                });
            }

            return targets;
        }

        public static CleanupResult CleanupById(string id, long sizeLimitBytes = 0)
        {
            var target = GetCleanupTargets().FirstOrDefault(t => t.Id == id);
            if (target == null) return new CleanupResult { Success = false, Error = "Target not found" };
            return ExecuteCleanup(target, sizeLimitBytes);
        }

        public static CleanupResult CleanupAll(long sizeLimitBytes = 0)
        {
            var result = new CleanupResult { Success = true };
            foreach (var target in GetCleanupTargets())
            {
                var r = ExecuteCleanup(target, sizeLimitBytes);
                if (!r.Success) result.Success = false;
                result.BytesFreed += r.BytesFreed;
                result.FilesDeleted += r.FilesDeleted;
                if (!string.IsNullOrEmpty(r.Error)) result.Error = (result.Error ?? "") + r.Error + "; ";
            }
            return result;
        }

        private static CleanupResult ExecuteCleanup(CleanupTarget target, long sizeLimitBytes)
        {
            var result = new CleanupResult();
            try
            {
                if (!Directory.Exists(target.Path))
                    return new CleanupResult { Success = true, BytesFreed = 0, FilesDeleted = 0 };

                var files = Directory.GetFiles(target.Path, "*", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    try
                    {
                        var info = new FileInfo(file);
                        if (sizeLimitBytes > 0 && info.Length > sizeLimitBytes)
                            continue; // skip files larger than limit

                        result.BytesFreed += info.Length;
                        File.Delete(file);
                        result.FilesDeleted++;
                    }
                    catch { }
                }

                // Clean empty directories
                foreach (var dir in Directory.GetDirectories(target.Path, "*", SearchOption.AllDirectories).OrderByDescending(d => d.Length))
                {
                    try
                    {
                        if (Directory.GetFiles(dir).Length == 0 && Directory.GetDirectories(dir).Length == 0)
                            Directory.Delete(dir);
                    }
                    catch { }
                }

                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = ex.Message;
            }
            return result;
        }

        public static long GetTargetSize(string id)
        {
            var target = GetCleanupTargets().FirstOrDefault(t => t.Id == id);
            if (target == null || !Directory.Exists(target.Path)) return 0;
            try
            {
                return Directory.GetFiles(target.Path, "*", SearchOption.AllDirectories)
                    .Sum(f => new FileInfo(f).Length);
            }
            catch { return 0; }
        }

        public class CleanupTarget
        {
            public string Id { get; set; } = "";
            public string Name { get; set; } = "";
            public string Description { get; set; } = "";
            public string Path { get; set; } = "";
            public bool IsSafe { get; set; } = true;
        }

        public class CleanupResult
        {
            public bool Success { get; set; }
            public long BytesFreed { get; set; }
            public int FilesDeleted { get; set; }
            public string? Error { get; set; }
        }
    }
}
