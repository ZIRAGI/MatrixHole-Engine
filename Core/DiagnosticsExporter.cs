using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Management;
using Newtonsoft.Json;

namespace MatrixHole.Core
{
    /// <summary>
    /// DiagnosticsExporter — одна кнопка для сборки ZIP с логами, железом и конфигом (п.75 OptimizatorPlan).
    /// </summary>
    public static class DiagnosticsExporter
    {
        public static string ExportDiagnosticsZip(string? userComment = null)
        {
            try
            {
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var zipName = $"MatrixHole_Diagnostics_{timestamp}.zip";
                var zipPath = Path.Combine(Path.GetTempPath(), zipName);
                var tempDir = Path.Combine(Path.GetTempPath(), $"sst_diag_{timestamp}");
                Directory.CreateDirectory(tempDir);

                // 1. Current config
                try
                {
                    var configPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "MatrixHole", "app_settings.json");
                    if (File.Exists(configPath))
                        File.Copy(configPath, Path.Combine(tempDir, "app_settings.json"), true);
                }
                catch { }

                // 2. Session logs
                try
                {
                    var logDir = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "MatrixHole", "logs");
                    if (Directory.Exists(logDir))
                    {
                        var recentLogs = Directory.GetFiles(logDir, "*.log")
                            .OrderByDescending(File.GetLastWriteTime)
                            .Take(5);
                        foreach (var log in recentLogs)
                            File.Copy(log, Path.Combine(tempDir, Path.GetFileName(log)), true);
                    }
                }
                catch { }

                // 3. Hardware report
                try
                {
                    File.WriteAllText(Path.Combine(tempDir, "hardware.json"), HardwareInfo.GetFullReport());
                }
                catch { }

                // 4. Process list
                try
                {
                    var processes = Process.GetProcesses()
                        .Select(p => new { p.ProcessName, p.Id, p.WorkingSet64 })
                        .OrderByDescending(p => p.WorkingSet64)
                        .Take(50);
                    File.WriteAllText(Path.Combine(tempDir, "processes.json"), JsonConvert.SerializeObject(processes, Formatting.Indented));
                }
                catch { }

                // 5. Tweaker state
                try
                {
                    var state = new
                    {
                        GameDetected = GameDetector.Detect()?.IsInstalled ?? false,
                        GamePath = GameDetector.Detect()?.InstallPath ?? "N/A",
                        ActiveProfile = ProfileManager.GetActiveProfile()?.Name ?? "N/A",
                        Plugins = PluginManager.GetPlugins().Select(p => new { p.Name, p.Enabled }),
                        NetworkBypassActive = Network.RknBypass.IsHostsModified(),
                        SystemTweaks = GetSystemTweakStates(),
                        Timestamp = DateTime.Now
                    };
                    File.WriteAllText(Path.Combine(tempDir, "tweaker_state.json"), JsonConvert.SerializeObject(state, Formatting.Indented));
                }
                catch { }

                // 6. User comment
                if (!string.IsNullOrEmpty(userComment))
                {
                    File.WriteAllText(Path.Combine(tempDir, "user_comment.txt"), userComment);
                }

                // 7. Event log errors (last 20)
                try
                {
                    var eventData = new List<string>();
                    using var log = new EventLog("Application");
                    var entries = log.Entries.Cast<EventLogEntry>()
                        .Where(e => e.EntryType == EventLogEntryType.Error && e.TimeGenerated > DateTime.Now.AddDays(-1))
                        .OrderByDescending(e => e.TimeGenerated)
                        .Take(20)
                        .Select(e => $"[{e.TimeGenerated:yyyy-MM-dd HH:mm:ss}] {e.Source}: {e.Message}");
                    File.WriteAllLines(Path.Combine(tempDir, "recent_errors.txt"), entries);
                }
                catch { }

                // Pack to ZIP
                if (File.Exists(zipPath)) File.Delete(zipPath);
                ZipFile.CreateFromDirectory(tempDir, zipPath, CompressionLevel.Optimal, false);
                Directory.Delete(tempDir, true);

                return zipPath;
            }
            catch (Exception ex)
            {
                return $"ERROR: {ex.Message}";
            }
        }

        private static Dictionary<string, bool> GetSystemTweakStates()
        {
            return new Dictionary<string, bool>
            {
                ["GameMode"] = Sys.SystemOptimizer.IsGameModeEnabled(),
                ["GameBarDisabled"] = Sys.SystemOptimizer.IsGameBarDisabled(),
                ["CoreParkingDisabled"] = Sys.SystemOptimizer.IsCoreParkingDisabled(),
                ["NagleDisabled"] = Sys.SystemOptimizer.IsNagleDisabled(),
                ["HPETDisabled"] = Sys.SystemOptimizer.IsHPETDisabled(),
                ["SysMainDisabled"] = Sys.SystemOptimizer.IsSysMainDisabled()
            };
        }
    }
}
