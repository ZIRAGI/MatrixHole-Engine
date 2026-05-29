using System;
using System.Windows;

namespace MatrixHole.Core
{
    /// <summary>
    /// GhostMode — скрытый режим, только трей-иконка (п.79 OptimizatorPlan).
    /// Управление только через горячие клавиши.
    /// </summary>
    public static class GhostMode
    {
        private static bool _isGhostMode;
        private static WindowState _previousState;
        private static bool _previousShowInTaskbar;

        public static bool IsGhostMode => _isGhostMode;

        public static void Enable()
        {
            if (_isGhostMode) return;
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                var mainWindow = System.Windows.Application.Current.MainWindow;
                if (mainWindow == null) return;

                _previousState = mainWindow.WindowState;
                _previousShowInTaskbar = mainWindow.ShowInTaskbar;

                mainWindow.Hide();
                mainWindow.ShowInTaskbar = false;
                _isGhostMode = true;

                TrayIconManager.ShowBalloon("Ghost Mode", "Твикер скрыт. Ctrl+Shift+G — показать.", ToolTipIcon.Info, 5000);
            });
        }

        public static void Disable()
        {
            if (!_isGhostMode) return;
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                var mainWindow = System.Windows.Application.Current.MainWindow;
                if (mainWindow == null) return;

                mainWindow.ShowInTaskbar = _previousShowInTaskbar;
                mainWindow.Show();
                mainWindow.WindowState = _previousState;
                mainWindow.Activate();
                _isGhostMode = false;
            });
        }

        public static void Toggle()
        {
            if (_isGhostMode) Disable();
            else Enable();
        }

        public static string GetStatusJson()
        {
            return $"{{\"enabled\":{_isGhostMode.ToString().ToLower()},\"hotkey\":\"Ctrl+Shift+G\"}}";
        }
    }
}
