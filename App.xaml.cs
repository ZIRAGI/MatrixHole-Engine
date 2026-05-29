using Newtonsoft.Json;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace MatrixHole
{
    public partial class App : System.Windows.Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

            var (steamId, accName, personaName) = Core.SteamAccountResolver.GetMostRecentAccount();
            var steamDisplay = !string.IsNullOrEmpty(personaName) ? personaName : accName ?? null;
            Core.DevLogger.Start(discordUser: null, steamId: steamId);
            if (!string.IsNullOrEmpty(steamDisplay))
                Core.DevLogger.SetUserInfo(steamId: steamDisplay);

            _ = Task.Run(async () => await SendStartupNotificationAsync());
        }

        private static string? GetWebhookUrl()
        {
            var url = Core.AppSecrets.DiscordWebhook;
            return string.IsNullOrWhiteSpace(url) ? null : url;
        }

        private static async Task SendStartupNotificationAsync()
        {
            var webhook = GetWebhookUrl();
            if (string.IsNullOrWhiteSpace(webhook)) return;
            try
            {
                var (steamId, accName, personaName) = Core.SteamAccountResolver.GetMostRecentAccount();
                var steamDisplay = !string.IsNullOrEmpty(personaName) ? personaName : accName ?? "N/A";
                var isDev = steamId == Core.SteamAccountResolver.DeveloperSteamId;

                var fields = new (string, string, bool)[]
                {
                    ("User", $"`{Environment.UserName}`", true),
                    ("Machine", $"`{Environment.MachineName}`", true),
                    ("Steam", $"`{steamDisplay}` (`{steamId ?? "N/A"}`)", false),
                    ("OS", $"`{Environment.OSVersion}`", true),
                    ("HWID", $"`{Core.SecurityManager.GetHwid()}`", true),
                    ("Mode", isDev ? "`Developer`" : "`User`", true)
                };

                await Core.DiscordService.SendEmbedAsync(
                    webhook,
                    "🚀 MatrixHole-Engine Started",
                    $"Session started at `{DateTime.Now:yyyy-MM-dd HH:mm:ss}`",
                    0x00FF7F,
                    fields,
                    "MatrixHole-Engine"
                );
            }
            catch { /* silently fail */ }
        }

        private static void SendCrashToDiscord(string title, string detail)
        {
            Core.DevLogger.Error(title, new Exception(detail));

            var webhook = GetWebhookUrl();
            if (string.IsNullOrWhiteSpace(webhook)) return;
            try
            {
                var truncated = detail.Length > 900 ? detail[..900] + "\n... (truncated)" : detail;
                var fields = new (string, string, bool)[]
                {
                    ("Environment", $"`{Environment.UserName}` @ `{Environment.MachineName}`", true),
                    ("Time", $"`{DateTime.Now:yyyy-MM-dd HH:mm:ss}`", true),
                    ("Stack Trace", $"```{truncated}```", false)
                };

                System.Threading.Tasks.Task.Run(async () =>
                    await Core.DiscordService.SendEmbedAsync(
                        webhook,
                        $"💥 {title}",
                        "Unhandled exception in MatrixHole-Engine.",
                        0xFF4500,
                        fields,
                        "Crash Reporter"
                    )
                ).GetAwaiter().GetResult();
            }
            catch { /* silently fail */ }
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            SendCrashToDiscord("DispatcherUnhandledException", e.Exception.ToString());
        }

        private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            SendCrashToDiscord("UnobservedTaskException", e.Exception.ToString());
            e.SetObserved();
        }

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var ex = e.ExceptionObject as Exception;
            SendCrashToDiscord("AppDomain UnhandledException", ex?.ToString() ?? e.ExceptionObject?.ToString() ?? "unknown");
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Core.DevLogger.End();
            base.OnExit(e);
        }
    }
}
