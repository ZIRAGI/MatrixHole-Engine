using System;
using Microsoft.Win32;

namespace MatrixHole.Core
{
    /// <summary>
    /// FocusAssistManager — режим "Не беспокоить" Windows (п.62 OptimizatorPlan).
    /// При запуске игры авто-отключение уведомлений, Focus Assist, скрытие панели задач.
    /// </summary>
    public static class FocusAssistManager
    {
        // Focus Assist settings
        private const string FocusAssistKey = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Notifications\Settings";
        private const string NocGlobalKey = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Notifications\Settings\Windows.System.Notifications.NotificationSettings";

        // Taskbar auto-hide
        private const string TaskbarKey = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\StuckRects3";

        public static bool EnableFocusAssist()
        {
            try
            {
                // Enable Focus Assist (priority only) via registry
                Registry.SetValue(NocGlobalKey, "NOC_GLOBAL_SETTING_ALLOW_NOTIFICATION_SOUND", 0, RegistryValueKind.DWord);
                Registry.SetValue(NocGlobalKey, "NOC_GLOBAL_SETTING_ALLOW_TOASTS_ABOVE_LOCK", 0, RegistryValueKind.DWord);
                return true;
            }
            catch { return false; }
        }

        public static bool DisableFocusAssist()
        {
            try
            {
                Registry.SetValue(NocGlobalKey, "NOC_GLOBAL_SETTING_ALLOW_NOTIFICATION_SOUND", 1, RegistryValueKind.DWord);
                Registry.SetValue(NocGlobalKey, "NOC_GLOBAL_SETTING_ALLOW_TOASTS_ABOVE_LOCK", 1, RegistryValueKind.DWord);
                return true;
            }
            catch { return false; }
        }

        public static bool EnableTaskbarAutoHide()
        {
            try
            {
                // Toggle auto-hide via shell notification
                var taskbarHandle = NativeMethods.FindWindow("Shell_TrayWnd", null);
                if (taskbarHandle != IntPtr.Zero)
                {
                    NativeMethods.SetWindowPos(taskbarHandle, IntPtr.Zero, 0, 0, 0, 0,
                        NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_HIDEWINDOW);
                }
                return true;
            }
            catch { return false; }
        }

        public static bool ShowTaskbar()
        {
            try
            {
                var taskbarHandle = NativeMethods.FindWindow("Shell_TrayWnd", null);
                if (taskbarHandle != IntPtr.Zero)
                {
                    NativeMethods.SetWindowPos(taskbarHandle, IntPtr.Zero, 0, 0, 0, 0,
                        NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_SHOWWINDOW);
                }
                return true;
            }
            catch { return false; }
        }

        public static void ApplyGamingFocus()
        {
            EnableFocusAssist();
            // Hide taskbar is optional and aggressive; keep commented by default
            // EnableTaskbarAutoHide();
        }

        public static void RestoreNormalFocus()
        {
            DisableFocusAssist();
            // ShowTaskbar();
        }

        private static class NativeMethods
        {
            [System.Runtime.InteropServices.DllImport("user32.dll")]
            public static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);

            [System.Runtime.InteropServices.DllImport("user32.dll")]
            public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

            public const uint SWP_HIDEWINDOW = 0x0080;
            public const uint SWP_SHOWWINDOW = 0x0040;
            public const uint SWP_NOMOVE = 0x0002;
            public const uint SWP_NOSIZE = 0x0001;
        }
    }
}
