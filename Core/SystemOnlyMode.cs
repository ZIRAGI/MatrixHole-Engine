using System;
using System.IO;
using Newtonsoft.Json;

namespace MatrixHole.Core
{
    /// <summary>
    /// SystemOnlyMode — режим "Только система" (п.90 OptimizatorPlan).
    /// Твикер работает только как системный оптимизатор, без модификации файлов SCP:SL.
    /// </summary>
    public static class SystemOnlyMode
    {
        private static readonly string FlagFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MatrixHole", "system_only.flag");

        public static bool IsEnabled => File.Exists(FlagFile);

        public static void Enable()
        {
            File.WriteAllText(FlagFile, JsonConvert.SerializeObject(new
            {
                enabled = true,
                timestamp = DateTime.Now,
                allowed = new[] { "process_priority", "ram_clean", "game_mode", "timer_resolution", "power_plan", "core_parking", "nagle", "hpet", "sysmain", "visual_effects" }
            }));
        }

        public static void Disable()
        {
            if (File.Exists(FlagFile)) File.Delete(FlagFile);
        }

        public static void Toggle()
        {
            if (IsEnabled) Disable();
            else Enable();
        }

        public static bool CanModifyGameFiles()
        {
            return !IsEnabled;
        }
    }
}
