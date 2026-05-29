using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace MatrixHole.Core
{
    /// <summary>
    /// AutoReconnect — авто-реконнект при дисконнекте/краше (п.53 OptimizatorPlan).
    /// Сохраняет IP последнего сервера, предлагает переподключиться.
    /// </summary>
    public static class AutoReconnect
    {
        private static readonly string LastServerFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MatrixHole", "last_server.json");

        public class ServerInfo
        {
            public string? Ip { get; set; }
            public string? Name { get; set; }
            public string? Role { get; set; }
            public DateTime ConnectedAt { get; set; }
            public DateTime? DisconnectedAt { get; set; }
            public string? DisconnectReason { get; set; }
        }

        public static void SaveLastServer(string ip, string? name = null, string? role = null)
        {
            var info = new ServerInfo
            {
                Ip = ip,
                Name = name,
                Role = role,
                ConnectedAt = DateTime.Now
            };
            Directory.CreateDirectory(Path.GetDirectoryName(LastServerFile)!);
            File.WriteAllText(LastServerFile, JsonConvert.SerializeObject(info, Formatting.Indented));
        }

        public static void MarkDisconnected(string? reason = null)
        {
            try
            {
                if (!File.Exists(LastServerFile)) return;
                var json = File.ReadAllText(LastServerFile);
                var info = JsonConvert.DeserializeObject<ServerInfo>(json);
                if (info == null) return;
                info.DisconnectedAt = DateTime.Now;
                info.DisconnectReason = reason;
                File.WriteAllText(LastServerFile, JsonConvert.SerializeObject(info, Formatting.Indented));
            }
            catch { }
        }

        public static ServerInfo? GetLastServer()
        {
            try
            {
                if (!File.Exists(LastServerFile)) return null;
                return JsonConvert.DeserializeObject<ServerInfo>(File.ReadAllText(LastServerFile));
            }
            catch { return null; }
        }

        public static bool ShouldOfferReconnect()
        {
            var info = GetLastServer();
            if (info == null || string.IsNullOrEmpty(info.Ip)) return false;
            // Offer if disconnected within last 10 minutes
            if (info.DisconnectedAt.HasValue && (DateTime.Now - info.DisconnectedAt.Value).TotalMinutes < 10)
                return true;
            return false;
        }

        public static async Task MonitorGameProcessAsync(CancellationToken token)
        {
            bool wasRunning = false;
            while (!token.IsCancellationRequested)
            {
                var running = Process.GetProcessesByName("SCPSL").Any();
                if (wasRunning && !running)
                {
                    // Game crashed or closed
                    MarkDisconnected("process_exit");
                    TrayIconManager.ShowBalloon("SCP:SL", "Игра закрыта. Нажмите для реконнекта.", System.Windows.Forms.ToolTipIcon.Info, 10000);
                }
                wasRunning = running;
                await Task.Delay(5000, token);
            }
        }
    }
}
