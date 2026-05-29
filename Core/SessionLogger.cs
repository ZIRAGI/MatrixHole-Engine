using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace MatrixHole.Core
{
    public static class SessionLogger
    {
        private static readonly StringBuilder _log = new();
        private static readonly string _sessionId = Guid.NewGuid().ToString("N")[..8];
        private static readonly DateTime _startTime = DateTime.Now;
        private static bool _ended;

        private static string SessionLogWebhookUrl => AppSecrets.DiscordWebhook;

        public static void Start(string? steamId = null)
        {
            _log.AppendLine($"=== SESSION {_sessionId} STARTED ===");
            _log.AppendLine($"Timestamp : {_startTime:yyyy-MM-dd HH:mm:ss}");
            _log.AppendLine($"User      : {Environment.UserName}");
            _log.AppendLine($"Machine   : {Environment.MachineName}");
            _log.AppendLine($"OS        : {Environment.OSVersion}");
            _log.AppendLine($"HWID      : {SecurityManager.GetHwid()}");
            if (!string.IsNullOrEmpty(steamId))
                _log.AppendLine($"SteamID   : {steamId}");
            _log.AppendLine();
        }

        public static void Log(string category, string detail)
        {
            _log.AppendLine($"[{DateTime.Now:HH:mm:ss}] [{category}] {detail}");
        }

        public static void LogConfig(Dictionary<string, object?> settings)
        {
            _log.AppendLine($"[{DateTime.Now:HH:mm:ss}] [CONFIG SAVE]");
            foreach (var kv in settings)
                _log.AppendLine($"    {kv.Key}: {kv.Value ?? "null"}");
        }

        public static void End()
        {
            if (_ended) return;
            _ended = true;
            var duration = DateTime.Now - _startTime;
            _log.AppendLine();
            _log.AppendLine($"=== SESSION {_sessionId} ENDED ===");
            _log.AppendLine($"Duration  : {duration:hh\\:mm\\:ss}");
            _log.AppendLine($"Ended at  : {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        }

        public static string GetLogText() => _log.ToString();

        public static void SendSessionLogToDiscord()
        {
            if (string.IsNullOrWhiteSpace(SessionLogWebhookUrl) || SessionLogWebhookUrl.Contains("PASTE_YOUR"))
                return;

            try
            {
                var tempPath = Path.Combine(Path.GetTempPath(), $"scptweaker_session_{_sessionId}.txt");
                File.WriteAllText(tempPath, GetLogText());

                Task.Run(async () =>
                    await DiscordService.SendFileAsync(
                        SessionLogWebhookUrl,
                        tempPath,
                        $"📝 Session Log — `{_sessionId}` — {Environment.UserName} @ {Environment.MachineName}"
                    )
                ).GetAwaiter().GetResult();

                try { File.Delete(tempPath); } catch { }
            }
            catch { /* silently fail */ }
        }

        public static async Task SendSessionLogToDiscordAsync()
        {
            if (string.IsNullOrWhiteSpace(SessionLogWebhookUrl) || SessionLogWebhookUrl.Contains("PASTE_YOUR"))
                return;

            try
            {
                var tempPath = Path.Combine(Path.GetTempPath(), $"scptweaker_session_{_sessionId}.txt");
                File.WriteAllText(tempPath, GetLogText());

                await DiscordService.SendFileAsync(
                    SessionLogWebhookUrl,
                    tempPath,
                    $"📝 Session Log — `{_sessionId}` — {Environment.UserName} @ {Environment.MachineName}"
                );

                try { File.Delete(tempPath); } catch { }
            }
            catch { /* silently fail */ }
        }
    }
}
