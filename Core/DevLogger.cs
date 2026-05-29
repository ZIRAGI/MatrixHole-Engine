using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MatrixHole.Core
{
    /// <summary>
    /// Unified session logger. Collects actions, errors, config changes and sends rich Discord reports.
    /// </summary>
    public static class DevLogger
    {
        private static string DevLogWebhookUrl => AppSecrets.DiscordWebhook;

        private static readonly StringBuilder _localLog = new();
        private static readonly List<LogEntry> _entries = new();
        private static readonly string _sessionId = Guid.NewGuid().ToString("N")[..8];
        private static readonly DateTime _startTime = DateTime.Now;
        private static bool _ended;
        private static readonly object _lock = new();

        private static string? _discordUser;
        private static string? _steamId;
        private static string? _discordRole;

        private class LogEntry
        {
            public string Category { get; set; } = "";
            public string Title { get; set; } = "";
            public string Detail { get; set; } = "";
            public DateTime Time { get; set; }
            public Dictionary<string, string>? Extra { get; set; }
        }

        public static void SetUserInfo(string? discordUser = null, string? steamId = null, string? discordRole = null)
        {
            _discordUser = discordUser;
            _steamId = steamId;
            _discordRole = discordRole;
        }

        public static void Start(string? discordUser = null, string? steamId = null)
        {
            SetUserInfo(discordUser, steamId);
            LogLocal("========================================");
            LogLocal("  MatrixHole-Engine Session Log");
            LogLocal("========================================");
            LogLocal($"Session ID : {_sessionId}");
            LogLocal($"Started    : {_startTime:yyyy-MM-dd HH:mm:ss}");
            LogLocal($"User       : {Environment.UserName}");
            LogLocal($"Machine    : {Environment.MachineName}");
            LogLocal($"OS         : {Environment.OSVersion}");
            LogLocal($"HWID       : {SecurityManager.GetHwid()}");
            if (!string.IsNullOrEmpty(discordUser)) LogLocal($"Discord    : {discordUser}");
            if (!string.IsNullOrEmpty(steamId)) LogLocal($"Steam ID   : {steamId}");
            LogLocal("----------------------------------------");
            LogLocal("");
        }

        public static void Log(string category, string title, string detail, Dictionary<string, string>? extra = null)
        {
            lock (_lock)
            {
                var entry = new LogEntry
                {
                    Category = category.ToUpper(),
                    Title = title,
                    Detail = detail,
                    Time = DateTime.Now,
                    Extra = extra
                };

                _entries.Add(entry);
                LogLocal($"[{entry.Time:HH:mm:ss}] [{entry.Category,8}] {title}");
                if (!string.IsNullOrEmpty(detail))
                    LogLocal($"           Detail: {detail}");
                if (extra != null)
                    foreach (var kv in extra)
                        LogLocal($"           {kv.Key}: {kv.Value}");
                LogLocal("");

                // Send critical events immediately
                if (IsCritical(category))
                {
                    _ = Task.Run(() => SendEmbedNow(entry));
                }
            }
        }

        public static void Auth(string action, string user, bool success, string? detail = null)
        {
            var status = success ? "SUCCESS" : "FAILED";
            var extra = new Dictionary<string, string> { ["User"] = user, ["Status"] = status };
            if (!string.IsNullOrEmpty(detail)) extra["Detail"] = detail;
            Log("AUTH", $"{action} — {status}", $"User: {user}", extra);
        }

        public static void Action(string action, string target, string? detail = null)
        {
            var extra = new Dictionary<string, string> { ["Target"] = target };
            if (!string.IsNullOrEmpty(detail)) extra["Detail"] = detail;
            Log("ACTION", action, $"Target: {target}", extra);
        }

        public static void Error(string context, Exception ex)
        {
            var stack = ex.StackTrace;
            if (stack != null && stack.Length > 1500)
                stack = stack[..1500] + "\n... (truncated)";

            Log("ERROR", $"Exception in {context}", $"{ex.GetType().Name}: {ex.Message}",
                new Dictionary<string, string> { ["Stack Trace"] = stack ?? "N/A" });
        }

        public static void Warning(string context, string message)
        {
            Log("WARNING", $"Warning in {context}", message);
        }

        public static void Config(string action, Dictionary<string, object?> settings)
        {
            var extra = settings.ToDictionary(kv => kv.Key, kv => (kv.Value ?? "null").ToString()!);
            Log("CONFIG", $"Config {action}", $"{settings.Count} settings changed", extra);
        }

        public static void Report(string message, string? screenshotPath = null)
        {
            var extra = new Dictionary<string, string> { ["User Message"] = message };
            if (!string.IsNullOrEmpty(screenshotPath)) extra["Screenshot"] = screenshotPath;
            Log("REPORT", "User Report", message, extra);
        }

        public static void End()
        {
            lock (_lock)
            {
                if (_ended) return;
                _ended = true;

                var duration = DateTime.Now - _startTime;
                LogLocal("");
                LogLocal("----------------------------------------");
                LogLocal("  Session End");
                LogLocal($"  Duration : {duration:hh\\:mm\\:ss}");
                LogLocal($"  Ended    : {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                LogLocal("========================================");

                SaveLocalLog();
            }
        }

        public static string GetLocalLog() => _localLog.ToString();

        public static string GetSessionReport(string? userMessage = null)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== MATRIXHOLE-ENGINE SESSION REPORT ===");
            sb.AppendLine($"Session ID : {_sessionId}");
            sb.AppendLine($"Time       : {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"User       : {Environment.UserName}");
            sb.AppendLine($"Machine    : {Environment.MachineName}");
            sb.AppendLine($"OS         : {Environment.OSVersion}");
            sb.AppendLine($"HWID       : {SecurityManager.GetHwid()}");
            sb.AppendLine($"Discord    : {_discordUser ?? "Not logged in"}");
            sb.AppendLine($"Steam      : {_steamId ?? "Not linked"}");
            sb.AppendLine($"Role       : {_discordRole ?? "N/A"}");
            sb.AppendLine();

            if (!string.IsNullOrEmpty(userMessage))
            {
                sb.AppendLine("=== USER REPORT ===");
                sb.AppendLine(userMessage);
                sb.AppendLine();
            }

            var errors = _entries.Where(e => e.Category == "ERROR").ToList();
            if (errors.Count > 0)
            {
                sb.AppendLine($"=== ERRORS ({errors.Count}) ===");
                foreach (var e in errors)
                {
                    sb.AppendLine($"[{e.Time:HH:mm:ss}] {e.Title}");
                    sb.AppendLine($"  {e.Detail}");
                }
                sb.AppendLine();
            }

            var actions = _entries.Where(e => e.Category == "ACTION").TakeLast(20).ToList();
            if (actions.Count > 0)
            {
                sb.AppendLine($"=== LAST ACTIONS ({actions.Count}) ===");
                foreach (var e in actions)
                {
                    sb.AppendLine($"[{e.Time:HH:mm:ss}] {e.Title}");
                }
                sb.AppendLine();
            }

            sb.AppendLine("=== FULL LOG ===");
            sb.AppendLine(_localLog.ToString());

            return sb.ToString();
        }

        private static bool IsCritical(string category)
        {
            return category.ToUpper() switch
            {
                "ERROR" or "SECURITY" or "PANIC" => true,
                _ => false
            };
        }

        private static void LogLocal(string line)
        {
            _localLog.AppendLine(line);
        }

        private static void SaveLocalLog()
        {
            try
            {
                var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MatrixHole", "logs");
                Directory.CreateDirectory(dir);
                var path = Path.Combine(dir, $"session_{_sessionId}_{_startTime:yyyyMMdd_HHmmss}.txt");
                File.WriteAllText(path, _localLog.ToString());
            }
            catch { }
        }

        private static async Task SendEmbedNow(LogEntry entry)
        {
            if (string.IsNullOrWhiteSpace(DevLogWebhookUrl) || DevLogWebhookUrl.Contains("PASTE_YOUR"))
                return;

            var color = entry.Category switch
            {
                "ERROR" => 0xc73e1d,
                "WARNING" => 0xe08020,
                "SECURITY" => 0xff0000,
                _ => 0x808080
            };

            var fields = entry.Extra?.Select(kv =>
                (kv.Key, kv.Value.Length > 1000 ? kv.Value[..1000] + "..." : kv.Value, true)
            ).ToArray() ?? Array.Empty<(string, string, bool)>();

            try
            {
                await DiscordService.SendEmbedAsync(DevLogWebhookUrl,
                    $"[{entry.Category}] {entry.Title}",
                    entry.Detail.Length > 4000 ? entry.Detail[..4000] + "..." : entry.Detail,
                    color, fields,
                    $"Session {_sessionId} • {DateTime.Now:HH:mm:ss}");
            }
            catch { }
        }
    }
}
