using System;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace MatrixHole.Core
{
    /// <summary>
    /// TelemetryService — анонимный сбор метрик (п.73 OptimizatorPlan).
    /// Opt-in: FPS, железо, применённые твики для улучшения авто-пресетов.
    /// </summary>
    public static class TelemetryService
    {
        private static readonly string ConsentFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MatrixHole", "telemetry_consent.json");

        public static bool IsConsentGiven
        {
            get
            {
                try
                {
                    if (!File.Exists(ConsentFile)) return false;
                    var data = JsonConvert.DeserializeObject<dynamic>(File.ReadAllText(ConsentFile));
                    return data?.consent == true;
                }
                catch { return false; }
            }
        }

        public static void SetConsent(bool consent)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ConsentFile)!);
            File.WriteAllText(ConsentFile, JsonConvert.SerializeObject(new { consent, timestamp = DateTime.Now }));
        }

        public static Task SendTelemetryAsync()
        {
            if (!IsConsentGiven) return Task.CompletedTask;
            try
            {
                var payload = new
                {
                    hwid = SecurityManager.GetHwid(),
                    hardware = HardwareInfo.GetSummary(),
                    tweaks_applied = new
                    {
                        game_mode = Sys.SystemOptimizer.IsGameModeEnabled(),
                        core_parking = Sys.SystemOptimizer.IsCoreParkingDisabled(),
                        nagle = Sys.SystemOptimizer.IsNagleDisabled(),
                        hpet = Sys.SystemOptimizer.IsHPETDisabled()
                    },
                    timestamp = DateTime.Now
                };

                // Placeholder: send to your telemetry endpoint
                // using var client = new HttpClient();
                // await client.PostAsync("https://your-telemetry-endpoint.com/collect",
                //     new StringContent(JsonConvert.SerializeObject(payload), System.Text.Encoding.UTF8, "application/json"));
            }
            catch { }
            return Task.CompletedTask;
        }
    }
}
