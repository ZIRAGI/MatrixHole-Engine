using System;
using System.IO;
using System.Linq;
using System.Windows;

namespace MatrixHole.Core
{
    /// <summary>
    /// SafeModeLauncher — безопасный режим запуска (п.48 OptimizatorPlan).
    /// Зажал Shift при старте → дефолтные настройки, все твики отключены.
    /// </summary>
    public static class SafeModeLauncher
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        private const int VK_SHIFT = 0x10;
        private static readonly string SafeModeFlag = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MatrixHole", "safe_mode.flag");

        public static bool IsShiftHeld()
        {
            return (GetAsyncKeyState(VK_SHIFT) & 0x8000) != 0;
        }

        public static bool IsSafeModeRequested()
        {
            return IsShiftHeld() || File.Exists(SafeModeFlag);
        }

        public static void EnterSafeMode()
        {
            try
            {
                File.WriteAllText(SafeModeFlag, $"safe_mode_entered={DateTime.Now:O}");

                // Reset all tweaks to default
                Sys.SystemOptimizer.EnableGameMode();
                Sys.SystemOptimizer.EnableGameBar();
                Sys.SystemOptimizer.EnableCoreParking();
                Sys.SystemOptimizer.SetBalancedPowerPlan();
                Sys.SystemOptimizer.EnableHPET();
                Sys.SystemOptimizer.EnableNagle();
                Sys.SystemOptimizer.EnableSysMain();
                Sys.SystemOptimizer.EnableVisualEffects();

                // Disable all plugins
                var plugins = PluginManager.GetPlugins();
                foreach (var p in plugins.Where(pl => pl.Enabled))
                    PluginManager.TogglePlugin(p.Id);

                // Reset network
                Network.RknBypass.ResetHosts();
                Network.RknBypass.ResetCustomHosts();
                Network.RknBypass.ResetDns();
                Network.RknBypass.ResetProxy();

                // Restore game files
                var game = GameDetector.Detect();
                if (game != null && game.IsInstalled)
                {
                    var engine = new Cheats.StealthEngine(game.InstallPath);
                    engine.RestoreEnvironment();
                }

                TrayIconManager.ShowBalloon("Safe Mode", "Твикер запущен в безопасном режиме. Все твики отключены.", System.Windows.Forms.ToolTipIcon.Warning, 5000);
            }
            catch { }
        }

        public static void ExitSafeMode()
        {
            if (File.Exists(SafeModeFlag)) File.Delete(SafeModeFlag);
        }

        public static bool IsSafeModeActive => File.Exists(SafeModeFlag);
    }
}
