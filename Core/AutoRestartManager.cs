using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace MatrixHole.Core
{
    /// <summary>
    /// AutoRestartManager — авто-рестарт твикера при обновлении (п.88 OptimizatorPlan).
    /// </summary>
    public static class AutoRestartManager
    {
        private static readonly string StateFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MatrixHole", "restart_state.json");

        public class RestartState
        {
            public bool UpdateReady { get; set; }
            public DateTime ReadySince { get; set; }
            public bool WaitingForGameExit { get; set; }
        }

        public static RestartState GetState()
        {
            try
            {
                if (File.Exists(StateFile))
                    return JsonConvert.DeserializeObject<RestartState>(File.ReadAllText(StateFile)) ?? new RestartState();
            }
            catch { }
            return new RestartState();
        }

        public static void SetUpdateReady()
        {
            var state = new RestartState { UpdateReady = true, ReadySince = DateTime.Now };
            Directory.CreateDirectory(Path.GetDirectoryName(StateFile)!);
            File.WriteAllText(StateFile, JsonConvert.SerializeObject(state));
        }

        public static async Task WaitAndRestartAsync(CancellationToken token)
        {
            var state = GetState();
            if (!state.UpdateReady) return;

            // If user is in game, wait
            while (!token.IsCancellationRequested)
            {
                if (!Process.GetProcessesByName("SCPSL").Any()) break;
                await Task.Delay(5000, token);
            }

            UpdaterLauncher.LaunchUpdaterAndExit();
        }
    }
}
