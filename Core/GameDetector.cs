using Microsoft.Win32;
using System;
using System.IO;
using System.Linq;

namespace MatrixHole.Core
{
    public static class GameDetector
    {
        public const string SteamAppId = "700330";
        public const string GameExeName = "SCPSL.exe";

        public class GameInfo
        {
            public string InstallPath { get; set; } = string.Empty;
            public string ExePath => Path.Combine(InstallPath, GameExeName);
            public bool IsSteamVersion { get; set; }
            public bool IsInstalled => Directory.Exists(InstallPath) && File.Exists(ExePath);
        }

        public static GameInfo? Detect()
        {
            var steamPath = FindSteamGamePath();
            if (!string.IsNullOrEmpty(steamPath))
                return new GameInfo { InstallPath = steamPath, IsSteamVersion = true };

            var defaults = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common", "SCP Secret Laboratory"),
                Path.Combine("D:", "SteamLibrary", "steamapps", "common", "SCP Secret Laboratory"),
                Path.Combine("E:", "SteamLibrary", "steamapps", "common", "SCP Secret Laboratory"),
                Path.Combine("F:", "SteamLibrary", "steamapps", "common", "SCP Secret Laboratory")
            };

            foreach (var path in defaults)
            {
                if (Directory.Exists(path) && File.Exists(Path.Combine(path, GameExeName)))
                    return new GameInfo { InstallPath = path, IsSteamVersion = true };
            }
            return null;
        }

        private static string? FindSteamGamePath()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Steam App " + SteamAppId);
                var loc = key?.GetValue("InstallLocation") as string;
                if (!string.IsNullOrEmpty(loc) && Directory.Exists(loc)) return loc;
            }
            catch { }

            try
            {
                using var steamKey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Valve\Steam");
                var steamPath = steamKey?.GetValue("SteamPath") as string;
                if (!string.IsNullOrEmpty(steamPath))
                {
                    var vdf = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
                    if (File.Exists(vdf))
                    {
                        foreach (var line in File.ReadAllText(vdf).Split('\n'))
                        {
                            if (line.Contains("\"path\""))
                            {
                                var path = line.Trim().Replace("\"path\"", "").Replace("\"", "").Trim();
                                var gp = Path.Combine(path, "steamapps", "common", "SCP Secret Laboratory");
                                if (Directory.Exists(gp) && File.Exists(Path.Combine(gp, GameExeName))) return gp;
                            }
                        }
                    }
                    var def = Path.Combine(steamPath, "steamapps", "common", "SCP Secret Laboratory");
                    if (Directory.Exists(def) && File.Exists(Path.Combine(def, GameExeName))) return def;
                }
            }
            catch { }
            return null;
        }

        public static string? GetSteamLaunchOptions()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Valve\Steam\Apps\" + SteamAppId);
                return key?.GetValue("LaunchOptions") as string;
            }
            catch { return null; }
        }

        public static bool SetSteamLaunchOptions(string options)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Valve\Steam\Apps\" + SteamAppId);
                key?.SetValue("LaunchOptions", options);
                return true;
            }
            catch { return false; }
        }
    }
}
