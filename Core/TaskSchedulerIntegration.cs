using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace MatrixHole.Core
{
    /// <summary>
    /// TaskSchedulerIntegration — планировщик задач Windows (п.68 OptimizatorPlan).
    /// Авто-запуск твикера со Steam, авто-применение профиля при обнаружении SCPSL.exe.
    /// </summary>
    public static class TaskSchedulerIntegration
    {
        private static readonly string SettingsFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MatrixHole", "scheduler_settings.json");

        public class SchedulerSettings
        {
            public bool AutoLaunchWithSteam { get; set; }
            public bool AutoApplyProfileOnGameStart { get; set; }
            public bool RestoreSettingsAfterGameExit { get; set; }
            public string? DefaultProfileId { get; set; }
        }

        public static SchedulerSettings GetSettings()
        {
            try
            {
                if (File.Exists(SettingsFile))
                    return JsonConvert.DeserializeObject<SchedulerSettings>(File.ReadAllText(SettingsFile)) ?? new SchedulerSettings();
            }
            catch { }
            return new SchedulerSettings();
        }

        public static void SaveSettings(SchedulerSettings settings)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsFile)!);
            File.WriteAllText(SettingsFile, JsonConvert.SerializeObject(settings, Formatting.Indented));
        }

        public static bool CreateStartupTask()
        {
            try
            {
                var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                if (string.IsNullOrEmpty(exePath)) return false;

                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = $"/Create /F /TN \"MatrixHole_AutoStart\" /TR \"\\\"{exePath}\\\"\" /SC ONLOGON /RL HIGHEST",
                    UseShellExecute = true,
                    Verb = "runas",
                    CreateNoWindow = true
                };
                var proc = System.Diagnostics.Process.Start(psi);
                proc?.WaitForExit();
                return proc?.ExitCode == 0;
            }
            catch { return false; }
        }

        public static bool RemoveStartupTask()
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = "/Delete /F /TN \"MatrixHole_AutoStart\"",
                    UseShellExecute = true,
                    Verb = "runas",
                    CreateNoWindow = true
                };
                var proc = System.Diagnostics.Process.Start(psi);
                proc?.WaitForExit();
                return proc?.ExitCode == 0;
            }
            catch { return false; }
        }
    }
}
