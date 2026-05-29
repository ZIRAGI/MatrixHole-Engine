using System;
using System.IO;
using System.Reflection;
using System.Windows;
using SWF = System.Windows.Forms;
using SD = System.Drawing;

namespace MatrixHole.Core
{
    /// <summary>
    /// TrayIconManager — системный трей + Toast-уведомления (п.72 OptimizatorPlan).
    /// </summary>
    public static class TrayIconManager
    {
        private static SWF.NotifyIcon? _trayIcon;
        private static bool _initialized;

        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            _trayIcon = new SWF.NotifyIcon
            {
                Text = "MatrixHole-Engine",
                Visible = true,
                Icon = LoadAppIcon()
            };

            var menu = new SWF.ContextMenuStrip();
            menu.Items.Add("Открыть", null, (s, e) => ShowMainWindow());
            menu.Items.Add(new SWF.ToolStripSeparator());
            menu.Items.Add("Panic Reset (Ctrl+Shift+F12)", null, (s, e) => PanicReset.ExecuteFullReset());
            menu.Items.Add(new SWF.ToolStripSeparator());
            menu.Items.Add("Выход", null, (s, e) => ShutdownApp());
            _trayIcon.ContextMenuStrip = menu;

            _trayIcon.DoubleClick += (s, e) => ShowMainWindow();
        }

        public static void ShowBalloon(string title, string message, SWF.ToolTipIcon icon = SWF.ToolTipIcon.Info, int timeout = 3000)
        {
            if (_trayIcon == null) return;
            _trayIcon.BalloonTipTitle = title;
            _trayIcon.BalloonTipText = message;
            _trayIcon.BalloonTipIcon = icon;
            _trayIcon.ShowBalloonTip(timeout);
        }

        public static void ShowToast(string title, string message)
        {
            // Windows 10/11 native toast via PowerShell fallback
            try
            {
                var ps = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "powershell",
                        Arguments = $"-Command \"[Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType = WindowsRuntime] | Out-Null; $template = [Windows.UI.Notifications.ToastNotificationManager]::GetTemplateContent([Windows.UI.Notifications.ToastTemplateType]::ToastText02); $template.SelectSingleNode('//text[@id=\"1\"]').AppendChild($template.CreateTextNode('{EscapePs(title)}')); $template.SelectSingleNode('//text[@id=\"2\"]').AppendChild($template.CreateTextNode('{EscapePs(message)}')); $toast = [Windows.UI.Notifications.ToastNotification]::new($template); [Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier('MatrixHole').Show($toast);\"",
                        CreateNoWindow = true,
                        WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
                    }
                };
                ps.Start();
            }
            catch
            {
                ShowBalloon(title, message);
            }
        }

        public static void SetIconStatus(TrayStatus status)
        {
            if (_trayIcon == null) return;
            _trayIcon.Text = status switch
            {
                TrayStatus.Normal => "MatrixHole-Engine",
                TrayStatus.Active => "MatrixHole-Engine — DLL активна",
                TrayStatus.Warning => "MatrixHole-Engine — Внимание",
                TrayStatus.Error => "MatrixHole-Engine — Ошибка",
                _ => "MatrixHole-Engine"
            };
        }

        public static void HideTray() { if (_trayIcon != null) _trayIcon.Visible = false; }
        public static void ShowTray() { if (_trayIcon != null) _trayIcon.Visible = true; }

        public static void Dispose()
        {
            _trayIcon?.Dispose();
            _trayIcon = null;
            _initialized = false;
        }

        private static void ShowMainWindow()
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                var app = System.Windows.Application.Current;
                if (app?.MainWindow != null)
                {
                    app.MainWindow.Show();
                    app.MainWindow.WindowState = WindowState.Normal;
                    app.MainWindow.Activate();
                }
            });
        }

        private static void ShutdownApp()
        {
            Dispose();
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                System.Windows.Application.Current?.Shutdown();
            });
        }

        private static SD.Icon? LoadAppIcon()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                using var stream = assembly.GetManifestResourceStream("MatrixHole.app.ico");
                if (stream != null) return new SD.Icon(stream);
            }
            catch { }
            return SD.SystemIcons.Application;
        }

        private static string EscapePs(string input) => input.Replace("'", "''").Replace("\"", "`\"");

        public enum TrayStatus { Normal, Active, Warning, Error }
    }
}
