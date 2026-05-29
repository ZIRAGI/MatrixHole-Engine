using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using System.Threading.Tasks;

namespace MatrixHole
{
    public partial class MainWindow : Window
    {
        private readonly CsApi _api;
        private DateTime _lastRknNotify = DateTime.MinValue;
        private const int RknNotifyCooldownMin = 5;

        public MainWindow()
        {
            InitializeComponent();
            _api = new CsApi();
            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
        }

        private void Log(string msg)
        {
            try
            {
                var logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MatrixHole", "startup.log");
                File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss.fff}] {msg}\n");
            }
            catch { }
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                Log("=== STARTUP BEGIN ===");

                Log("Checking debugger...");
                if (Core.SecurityManager.IsDebuggerDetected())
                {
                    Log("Debugger detected — exiting");
                    System.Windows.MessageBox.Show("Debugger detected. Application will close.", "Security", MessageBoxButton.OK, MessageBoxImage.Error);
                    Close();
                    return;
                }

                Log("Checking VM...");
                if (Core.SecurityManager.IsVmDetected())
                {
                    Log("VM detected — exiting");
                    System.Windows.MessageBox.Show("Virtual machine or sandbox detected. Application will close.", "Security", MessageBoxButton.OK, MessageBoxImage.Error);
                    Close();
                    return;
                }

                Log("Checking integrity...");
                if (!Core.SecurityManager.VerifyIntegrity())
                {
                    Log("Integrity check failed — exiting");
                    System.Windows.MessageBox.Show("Application integrity check failed. Files may be corrupted or modified.", "Security", MessageBoxButton.OK, MessageBoxImage.Error);
                    Close();
                    return;
                }

                Log("Checking time tamper...");
                if (Core.SecurityManager.IsTimeTampered())
                {
                    Log("Time tamper detected — exiting");
                    System.Windows.MessageBox.Show("System time tampering detected. Application will close.", "Security", MessageBoxButton.OK, MessageBoxImage.Error);
                    Close();
                    return;
                }

                Log("UpdateLastRun...");
                Core.SecurityManager.UpdateLastRun();

                Log("PassiveAntiTamper disabled (dev mode)");
                // DISABLED: hidden timer causes false-positive FailFast in debug builds
                // Core.SecurityManager.PassiveAntiTamper.Arm();

                Log("Starting DevLogger...");
                Core.DevLogger.Start(
                    discordUser: Core.DiscordAuth.GetSavedAuth()?.Username,
                    steamId: Core.SteamAuth.GetSavedSteamId()
                );

                Log("EULA check...");
                if (!Core.EulaManager.IsAccepted())
                {
                    var eulaWnd = new EulaWindow();
                    eulaWnd.ShowDialog();
                    if (!eulaWnd.Accepted)
                    {
                        Close();
                        return;
                    }
                    Core.EulaManager.Accept();
                }

                Log("WebView2 init...");
                var userDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MatrixHole");
                if (!Directory.Exists(userDataFolder)) Directory.CreateDirectory(userDataFolder);
                var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
                Log("WebView2 environment created");
                await WebView.EnsureCoreWebView2Async(env);
                Log("WebView2 ensured");

                WebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
#if BETA
                WebView.CoreWebView2.Settings.AreDevToolsEnabled = true;
                Title = "MatrixHole-Engine [BETA]";
#else
                WebView.CoreWebView2.Settings.AreDevToolsEnabled = true;
#endif
                Log("Adding host object...");
                WebView.CoreWebView2.AddHostObjectToScript("csApi", _api);
                Log("Host object added");

                // Periodic network monitor
                var netTimer = new DispatcherTimer(TimeSpan.FromSeconds(30), DispatcherPriority.Background, (s, args) =>
                {
                    var reset = Network.RknBypass.AutoResetIfNoInternet();
                    if (reset && (DateTime.Now - _lastRknNotify).TotalMinutes >= RknNotifyCooldownMin)
                    {
                        _lastRknNotify = DateTime.Now;
                        try
                        {
                            if (WebView?.CoreWebView2 != null)
                            {
                                _ = WebView.CoreWebView2.ExecuteScriptAsync(
                                    "if(window.showRknAutoResetToast)window.showRknAutoResetToast('Internet connection dropped — RKN bypass auto-reset to restore connectivity.');");
                            }
                        }
                        catch { }
                    }
                }, Dispatcher);
                netTimer.Start();

                // Periodic security sweep
                var secTimer = new DispatcherTimer(TimeSpan.FromMinutes(5), DispatcherPriority.Background, (s, args) =>
                {
                    if (!Core.SecurityManager.RunPeriodicCheck())
                    {
                        System.Windows.MessageBox.Show("Security violation detected. Application will close.", "Security", MessageBoxButton.OK, MessageBoxImage.Error);
                        Close();
                    }
                }, Dispatcher);
                secTimer.Start();

                Log("=== STARTUP COMPLETE ===");

                // Auto-checks before showing UI
                _ = Task.Run(async () =>
                {
                // 1. RKN auto-reset if no internet (silent — no modal popups on startup)
                var rknReset = Network.RknBypass.AutoResetIfNoInternet();
                if (rknReset)
                {
                    _lastRknNotify = DateTime.Now;
                    _ = Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            if (WebView?.CoreWebView2 != null)
                            {
                                _ = WebView.CoreWebView2.ExecuteScriptAsync(
                                    "if(window.showRknAutoResetToast)window.showRknAutoResetToast('Internet connection dropped — RKN bypass auto-reset to restore connectivity.');");
                            }
                        }
                        catch { /* silent fail */ }
                    }));
                }

                // 2. Update check
                var update = await Core.UpdateChecker.CheckAsync();
                if (update != null)
                {
                    var msg = string.IsNullOrEmpty(update.ReleaseNotes)
                        ? $"New version {update.Version} is available.\n\nOpen download page?"
                        : $"{update.ReleaseNotes}\n\nVersion: {update.Version}\n\nOpen download page?";
                    var result = System.Windows.MessageBox.Show(msg, "Update Available", MessageBoxButton.YesNo,
                        update.Mandatory ? MessageBoxImage.Warning : MessageBoxImage.Information);
                    if (result == MessageBoxResult.Yes && !string.IsNullOrEmpty(update.DownloadUrl))
                    {
                        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(update.DownloadUrl) { UseShellExecute = true }); } catch { }
                    }
                }
            });

            var wwwroot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot");
            var indexPath = Path.Combine(wwwroot, "index.html");
            Log($"Navigating to: {indexPath}");
            if (File.Exists(indexPath))
            {
                try
                {
                    WebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                        "app.matrixhole", wwwroot,
                        Microsoft.Web.WebView2.Core.CoreWebView2HostResourceAccessKind.Allow);
                    WebView.CoreWebView2.Navigate("https://app.matrixhole/index.html");
                    Log("Navigation called (via virtual host)");
                }
                catch (Exception navEx)
                {
                    Log($"Navigation exception: {navEx.Message}");
                }
            }
            else
            {
                Log("index.html NOT FOUND");
                System.Windows.MessageBox.Show("index.html not found in wwwroot", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            }
            catch (Exception ex)
            {
                Log($"FATAL: {ex.GetType().Name}: {ex.Message}");
                Log($"STACK: {ex.StackTrace}");
                System.Windows.MessageBox.Show($"Startup failed:\n{ex.GetType().Name}: {ex.Message}\n\nSee startup.log for details.", "Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            }
            else if (WindowState == WindowState.Maximized)
            {
                WindowState = WindowState.Normal;
                DragMove();
            }
            else
            {
                DragMove();
            }
        }

        private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        private void Maximize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            Core.DevLogger.End();
        }
    }
}
