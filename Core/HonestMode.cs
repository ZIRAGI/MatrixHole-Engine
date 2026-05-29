using System;
using System.IO;
using Newtonsoft.Json;

namespace MatrixHole.Core
{
    /// <summary>
    /// HonestMode — режим "Честный" (п.97 OptimizatorPlan).
    /// Все читы/DLL/обходы/твики файлов отключаются. Остаются только системные оптимизации.
    /// </summary>
    public static class HonestMode
    {
        private static readonly string HonestFlagFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MatrixHole", "honest_mode.flag");

        public static bool IsEnabled => File.Exists(HonestFlagFile);

        public static void Enable()
        {
            File.WriteAllText(HonestFlagFile, JsonConvert.SerializeObject(new
            {
                enabled = true,
                timestamp = DateTime.Now,
                note = "Все чит-функции отключены. Разрешены только системные оптимизации."
            }));

            // Disable all cheat layers
            try
            {
                var game = GameDetector.Detect();
                if (game != null && game.IsInstalled)
                {
                    var engine = new Cheats.StealthEngine(game.InstallPath);
                    engine.RestoreEnvironment();
                }
                var plugins = PluginManager.GetPlugins();
                foreach (var p in plugins)
                {
                    if (p.Enabled) PluginManager.TogglePlugin(p.Id);
                }
            }
            catch { }
        }

        public static void Disable()
        {
            if (File.Exists(HonestFlagFile)) File.Delete(HonestFlagFile);
        }

        public static void Toggle()
        {
            if (IsEnabled) Disable();
            else Enable();
        }

        public static bool CanUseCheatFunction(string functionName)
        {
            if (!IsEnabled) return true;
            // In honest mode, only system optimizations are allowed
            var allowed = new[] { "priority", "affinity", "ram_clean", "game_mode", "timer_res", "power_plan" };
            return allowed.Any(a => functionName.ToLower().Contains(a));
        }
    }
}
