using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace MatrixHole.Core
{
    /// <summary>
    /// GameplayConfigEditor — правка config_gameplay.txt и UserData через UI (п.65 OptimizatorPlan).
    /// </summary>
    public static class GameplayConfigEditor
    {
        public static string? GetGameplayConfigPath()
        {
            var game = GameDetector.Detect();
            if (game == null || !game.IsInstalled) return null;
            return Path.Combine(game.InstallPath, "config_gameplay.txt");
        }

        public static string? GetUserDataPath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "..", "LocalLow", "Northwood Studios", "SCP Secret Laboratory", "UserData.txt");
        }

        public static Dictionary<string, string> ParseGameplayConfig()
        {
            var path = GetGameplayConfigPath();
            var result = new Dictionary<string, string>();
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return result;

            foreach (var line in File.ReadAllLines(path))
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;
                var parts = line.Split(new[] { '=' }, 2);
                if (parts.Length == 2)
                    result[parts[0].Trim()] = parts[1].Trim();
            }
            return result;
        }

        public static bool SetGameplayValue(string key, string value)
        {
            try
            {
                var path = GetGameplayConfigPath();
                if (string.IsNullOrEmpty(path)) return false;
                AutoBackupManager.BackupFile(path, $"gameplay_set_{key}");

                var lines = File.Exists(path) ? File.ReadAllLines(path).ToList() : new List<string>();
                bool found = false;
                for (int i = 0; i < lines.Count; i++)
                {
                    if (lines[i].StartsWith(key + "=") || lines[i].StartsWith(key + " ="))
                    {
                        lines[i] = $"{key}={value}";
                        found = true;
                        break;
                    }
                }
                if (!found) lines.Add($"{key}={value}");
                File.WriteAllLines(path, lines);
                return true;
            }
            catch { return false; }
        }

        public static bool ValidateValue(string key, string value)
        {
            // Known validations
            return key switch
            {
                "server_ip" => System.Net.IPAddress.TryParse(value, out _),
                "server_port" => int.TryParse(value, out var port) && port > 0 && port < 65536,
                "fullscreen" or "vsync" or "motion_blur" or "ambient_occlusion" => value == "true" || value == "false",
                _ => true
            };
        }
    }
}
