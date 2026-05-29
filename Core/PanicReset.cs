using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace MatrixHole.Core
{
    /// <summary>
    /// Panic Reset — аварийный сброс всех изменений (п.83 OptimizatorPlan).
    /// Хоткей: Ctrl+Shift+F12. Откатывает: файлы игры, DLL, сетевые настройки, системные твики.
    /// </summary>
    public static class PanicReset
    {
        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        private const int VK_CONTROL = 0x11;
        private const int VK_SHIFT = 0x10;
        private const int VK_F12 = 0x7B;

        public static bool IsPanicKeyPressed()
        {
            return (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0 &&
                   (GetAsyncKeyState(VK_SHIFT) & 0x8000) != 0 &&
                   (GetAsyncKeyState(VK_F12) & 0x8000) != 0;
        }

        public static string ExecuteFullReset()
        {
            var report = new System.Collections.Generic.List<string>();
            int restored = 0;
            int failed = 0;

            try
            {
                // 1. Restore game files (AC, boot.config, globalgamemanagers)
                try
                {
                    var game = GameDetector.Detect();
                    if (game != null && game.IsInstalled)
                    {
                        var engine = new Cheats.StealthEngine(game.InstallPath);
                        engine.RestoreEnvironment();
                        report.Add("Game files restored");
                        restored++;
                    }
                }
                catch (Exception ex) { report.Add($"Game files failed: {ex.Message}"); failed++; }

                // 2. Unload all plugins
                try
                {
                    var plugins = PluginManager.GetPlugins();
                    foreach (var p in plugins.Where(pl => pl.Enabled))
                    {
                        PluginManager.TogglePlugin(p.Id);
                    }
                    report.Add("All plugins disabled");
                    restored++;
                }
                catch (Exception ex) { report.Add($"Plugins failed: {ex.Message}"); failed++; }

                // 3. Reset network bypass
                try
                {
                    Network.RknBypass.ResetHosts();
                    Network.RknBypass.ResetCustomHosts();
                    Network.RknBypass.ResetDns();
                    Network.RknBypass.ResetProxy();
                    report.Add("Network bypass reset");
                    restored++;
                }
                catch (Exception ex) { report.Add($"Network reset failed: {ex.Message}"); failed++; }

                // 4. Reset system tweaks
                try
                {
                    Sys.SystemOptimizer.EnableGameMode();
                    Sys.SystemOptimizer.EnableGameBar();
                    Sys.SystemOptimizer.EnableCoreParking();
                    Sys.SystemOptimizer.SetBalancedPowerPlan();
                    Sys.SystemOptimizer.EnableHPET();
                    Sys.SystemOptimizer.EnableNagle();
                    Sys.SystemOptimizer.EnableSysMain();
                    Sys.SystemOptimizer.EnableVisualEffects();
                    report.Add("System tweaks reset to defaults");
                    restored++;
                }
                catch (Exception ex) { report.Add($"System tweaks failed: {ex.Message}"); failed++; }

                // 5. Kill SCPSL process if running (emergency)
                try
                {
                    foreach (var proc in Process.GetProcessesByName("SCPSL"))
                    {
                        try { proc.Kill(); proc.WaitForExit(3000); }
                        catch { }
                    }
                    report.Add("SCPSL process terminated");
                    restored++;
                }
                catch (Exception ex) { report.Add($"Kill SCPSL failed: {ex.Message}"); failed++; }

                // 6. Clear session log
                try
                {
                    DevLogger.End();
                    report.Add("Session logged");
                }
                catch { }

                // 7. Log and notify
                DevLogger.Log("PANIC", "Panic Reset Executed", $"Restored: {restored}, Failed: {failed}",
                    new Dictionary<string, string> { ["Restored"] = restored.ToString(), ["Failed"] = failed.ToString() });

                try
                {
                    var webhook = GetWebhookFromSettings();
                    if (!string.IsNullOrEmpty(webhook))
                    {
                        Task.Run(async () => await DiscordService.SendEmbedAsync(
                            webhook,
                            "🚨 Panic Reset Triggered",
                            $"User `{Environment.UserName}` triggered full reset on `{Environment.MachineName}` at `{DateTime.Now:yyyy-MM-dd HH:mm:ss}`",
                            0xFF0000,
                            new[] { ("Restored", $"`{restored}`", true), ("Failed", $"`{failed}`", true) },
                            "MatrixHole-Engine"
                        ));
                    }
                }
                catch { }

                return JsonConvert.SerializeObject(new
                {
                    ok = true,
                    restored,
                    failed,
                    actions = report.ToArray(),
                    message = "Полный сброс выполнен. Все изменения откачены."
                });
            }
            catch (Exception ex)
            {
                return $"{{\"ok\":false,\"error\":\"{ex.Message.Replace("\"", "'")}\"}}";
            }
        }

        public static string ExecuteSoftReset()
        {
            // Soft reset: only disable active cheats and plugins, don't touch system
            try
            {
                var game = GameDetector.Detect();
                if (game != null && game.IsInstalled)
                {
                    var engine = new Cheats.StealthEngine(game.InstallPath);
                    engine.RestoreEnvironment();
                }
                var plugins = PluginManager.GetPlugins();
                foreach (var p in plugins.Where(pl => pl.Enabled))
                    PluginManager.TogglePlugin(p.Id);

                return "{\"ok\":true,\"mode\":\"soft\",\"message\":\"Soft reset: cheats and plugins disabled\"}";
            }
            catch (Exception ex)
            {
                return $"{{\"ok\":false,\"error\":\"{ex.Message.Replace("\"", "'")}\"}}";
            }
        }

        private static string? GetWebhookFromSettings()
        {
            var url = AppSecrets.DiscordWebhook;
            return string.IsNullOrWhiteSpace(url) ? null : url;
        }
    }
}
