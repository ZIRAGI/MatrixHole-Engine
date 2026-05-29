using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace MatrixHole.Core
{
    /// <summary>
    /// AutoSaveManager — авто-сохранение конфига при любом изменении (п.29 OptimizatorPlan).
    /// Дебаунс 500 мс, индикатор "Изменения сохранены".
    /// </summary>
    public static class AutoSaveManager
    {
        private static CancellationTokenSource? _debounceCts;
        private static readonly object _lock = new();
        private static DateTime _lastSaveTime;

        public static bool IsEnabled { get; set; } = true;
        public static DateTime LastSaveTime => _lastSaveTime;
        public static bool HasUnsavedChanges { get; private set; }

        public static event Action? OnAutoSave;
        public static event Action? OnUnsavedChanges;

        public static void MarkUnsaved()
        {
            HasUnsavedChanges = true;
            OnUnsavedChanges?.Invoke();

            if (!IsEnabled) return;

            lock (_lock)
            {
                _debounceCts?.Cancel();
                _debounceCts = new CancellationTokenSource();
                var token = _debounceCts.Token;
                Task.Delay(500, token).ContinueWith(t =>
                {
                    if (!t.IsCanceled && !token.IsCancellationRequested)
                    {
                        SaveInternal();
                    }
                }, TaskScheduler.Default);
            }
        }

        public static void ForceSaveNow()
        {
            lock (_lock)
            {
                _debounceCts?.Cancel();
                SaveInternal();
            }
        }

        private static void SaveInternal()
        {
            try
            {
                // Trigger save through ConfigManager if available
                var cfg = ConfigManager.Load();
                ConfigManager.Save(cfg);
                _lastSaveTime = DateTime.Now;
                HasUnsavedChanges = false;
                OnAutoSave?.Invoke();
            }
            catch { }
        }

        public static string GetStatusJson()
        {
            return JsonConvert.SerializeObject(new
            {
                lastSave = _lastSaveTime.ToString("HH:mm:ss"),
                hasUnsaved = HasUnsavedChanges,
                autoSaveEnabled = IsEnabled
            });
        }
    }
}
