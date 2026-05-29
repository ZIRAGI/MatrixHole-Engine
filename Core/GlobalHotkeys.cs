using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace MatrixHole.Core
{
    /// <summary>
    /// GlobalHotkeys — глобальные горячие клавиши (п.64 OptimizatorPlan).
    /// Переключение профилей/пресетов без разворачивания окна.
    /// </summary>
    public static class GlobalHotkeys
    {
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;
        private const uint MOD_WIN = 0x0008;
        private const uint MOD_NOREPEAT = 0x4000;

        private static IntPtr _windowHandle;
        private static HwndSource? _source;
        private static readonly Dictionary<int, Action> _hotkeyActions = new();
        private static int _currentId = 9000;

        public static void RegisterWindow(IntPtr handle)
        {
            _windowHandle = handle;
            _source = HwndSource.FromHwnd(handle);
            _source?.AddHook(HwndHook);
        }

        public static void UnregisterAll()
        {
            foreach (var id in _hotkeyActions.Keys.ToList())
            {
                UnregisterHotKey(_windowHandle, id);
            }
            _hotkeyActions.Clear();
            _source?.RemoveHook(HwndHook);
        }

        public static int AddHotkey(ModifierKeys modifiers, uint key, Action action)
        {
            var id = _currentId++;
            uint fsModifiers = 0;
            if (modifiers.HasFlag(ModifierKeys.Alt)) fsModifiers |= MOD_ALT;
            if (modifiers.HasFlag(ModifierKeys.Control)) fsModifiers |= MOD_CONTROL;
            if (modifiers.HasFlag(ModifierKeys.Shift)) fsModifiers |= MOD_SHIFT;
            if (modifiers.HasFlag(ModifierKeys.Win)) fsModifiers |= MOD_WIN;
            fsModifiers |= MOD_NOREPEAT;

            if (RegisterHotKey(_windowHandle, id, fsModifiers, key))
            {
                _hotkeyActions[id] = action;
                return id;
            }
            return -1;
        }

        public static bool RemoveHotkey(int id)
        {
            if (!_hotkeyActions.ContainsKey(id)) return false;
            UnregisterHotKey(_windowHandle, id);
            _hotkeyActions.Remove(id);
            return true;
        }

        private static IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;
            if (msg == WM_HOTKEY)
            {
                var id = wParam.ToInt32();
                if (_hotkeyActions.TryGetValue(id, out var action))
                {
                    action?.Invoke();
                    handled = true;
                }
            }
            return IntPtr.Zero;
        }

        public static void SetupDefaultHotkeys()
        {
            // Ctrl+Shift+F12 — Panic Reset (already in PanicReset, but register global too)
            AddHotkey(ModifierKeys.Control | ModifierKeys.Shift, 0x7B, () =>
            {
                PanicReset.ExecuteFullReset();
                TrayIconManager.ShowBalloon("Panic Reset", "Все твики сброшены.", System.Windows.Forms.ToolTipIcon.Warning);
            });

            // Ctrl+Shift+1 — Профиль 1
            AddHotkey(ModifierKeys.Control | ModifierKeys.Shift, 0x31, () =>
            {
                var profiles = ProfileManager.GetAllProfiles();
                if (profiles.Count > 0)
                {
                    ProfileManager.SetActiveProfile(profiles[0].Id);
                    TrayIconManager.ShowBalloon("Профиль", $"Активен: {profiles[0].Name}");
                }
            });

            // Ctrl+Shift+2 — Профиль 2
            AddHotkey(ModifierKeys.Control | ModifierKeys.Shift, 0x32, () =>
            {
                var profiles = ProfileManager.GetAllProfiles();
                if (profiles.Count > 1)
                {
                    ProfileManager.SetActiveProfile(profiles[1].Id);
                    TrayIconManager.ShowBalloon("Профиль", $"Активен: {profiles[1].Name}");
                }
            });

            // Ctrl+Shift+G — Ghost Mode toggle
            AddHotkey(ModifierKeys.Control | ModifierKeys.Shift, 0x47, () =>
            {
                GhostMode.Toggle();
            });
        }

        [Flags]
        public enum ModifierKeys
        {
            None = 0,
            Alt = 1,
            Control = 2,
            Shift = 4,
            Win = 8
        }
    }
}
