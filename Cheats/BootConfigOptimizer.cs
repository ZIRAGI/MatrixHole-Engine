using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MatrixHole.Cheats
{
    /// <summary>
    /// Patches Unity boot.config for maximum FPS.
    /// This is a FILE-ONLY patch — anti-cheat cannot detect it.
    /// </summary>
    public class BootConfigOptimizer
    {
        private readonly string _bootConfigPath;
        private readonly string _backupDir;

        public BootConfigOptimizer(string gameDataPath)
        {
            _bootConfigPath = Path.Combine(gameDataPath, "boot.config");
            _backupDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MatrixHole", "Backups", "boot.config");
        }

        public string ApplyFpsTweaks()
        {
            try
            {
                if (!File.Exists(_bootConfigPath))
                    return "{\"error\":\"boot.config not found\"}";

                Directory.CreateDirectory(_backupDir);
                var backup = Path.Combine(_backupDir, $"boot.config_{DateTime.Now:yyyyMMdd_HHmmss}.bak");
                File.Copy(_bootConfigPath, backup, true);

                var lines = File.ReadAllLines(_bootConfigPath).ToList();
                var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var line in lines)
                {
                    var trimmed = line.Trim();
                    if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#")) continue;
                    var parts = trimmed.Split(new[] { '=' }, 2);
                    if (parts.Length == 2)
                        dict[parts[0].Trim()] = parts[1].Trim();
                }

                // ===== ILLEGAL FPS TWEAKS =====
                // Reduce GC max time slice — shorter GC pauses = less stutter
                dict["gc-max-time-slice"] = "1";

                // Keep gfx jobs enabled (should already be on)
                dict["gfx-enable-gfx-jobs"] = "1";
                dict["gfx-enable-native-gfx-jobs"] = "1";

                // Force multi-threaded renderer (do NOT force single-threaded)
                dict["force-gfx-direct"] = "0";

                // Disable single-instance lock (allows multiple clients — useful for testing)
                dict["single-instance"] = "0";

                // Disable HDR to avoid extra GPU cost
                dict["hdr-display-enabled"] = "0";

                // Disable waiting for native debugger
                dict["wait-for-native-debugger"] = "0";

                // Disable player connection debug overhead
                dict["player-connection-debug"] = "0";

                // Write back
                var outLines = new List<string>();
                foreach (var kvp in dict)
                {
                    outLines.Add($"{kvp.Key}={kvp.Value}");
                }
                File.WriteAllLines(_bootConfigPath, outLines);

                return $"{{\"success\":true,\"backup\":\"{backup.Replace("\\","/")}\",\"tweaks_applied\":{dict.Count}}}";
            }
            catch (Exception ex)
            {
                return $"{{\"error\":\"{ex.Message.Replace("\"","'")}\"}}";
            }
        }

        public string RestoreLatestBackup()
        {
            try
            {
                if (!Directory.Exists(_backupDir))
                    return "{\"error\":\"No backups found\"}";

                var backups = Directory.GetFiles(_backupDir, "boot.config_*.bak")
                    .OrderByDescending(File.GetLastWriteTime)
                    .ToList();

                if (backups.Count == 0)
                    return "{\"error\":\"No backups found\"}";

                File.Copy(backups[0], _bootConfigPath, true);
                return $"{{\"success\":true,\"restored\":\"{Path.GetFileName(backups[0])}\"}}";
            }
            catch (Exception ex)
            {
                return $"{{\"error\":\"{ex.Message.Replace("\"","'")}\"}}";
            }
        }

        public string GetCurrentSettings()
        {
            try
            {
                if (!File.Exists(_bootConfigPath))
                    return "{\"error\":\"boot.config not found\"}";

                var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var line in File.ReadAllLines(_bootConfigPath))
                {
                    var trimmed = line.Trim();
                    if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#")) continue;
                    var parts = trimmed.Split(new[] { '=' }, 2);
                    if (parts.Length == 2)
                        dict[parts[0].Trim()] = parts[1].Trim();
                }

                return Newtonsoft.Json.JsonConvert.SerializeObject(dict);
            }
            catch (Exception ex)
            {
                return $"{{\"error\":\"{ex.Message.Replace("\"","'")}\"}}";
            }
        }
    }
}
