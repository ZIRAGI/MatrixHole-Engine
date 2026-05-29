using System;
using System.Collections.Generic;
using System.Linq;

namespace MatrixHole.Core
{
    /// <summary>
    /// GlobalSearchProvider — глобальный поиск по функциям твикера (п.60 OptimizatorPlan).
    /// Как в VS Code: печатаешь → мгновенно телепортируешься в нужную вкладку.
    /// </summary>
    public static class GlobalSearchProvider
    {
        public class SearchItem
        {
            public string Id { get; set; } = "";
            public string Title { get; set; } = "";
            public string Description { get; set; } = "";
            public string Tab { get; set; } = "";
            public string[] Keywords { get; set; } = Array.Empty<string>();
            public int Priority { get; set; } = 0;
        }

        private static readonly List<SearchItem> _index = new()
        {
            new() { Id = "graphics_quality", Title = "Качество графики", Description = "Общий пресет качества Unity", Tab = "graphics", Keywords = new[] { "графика", "quality", "пресет", "настройки" }, Priority = 10 },
            new() { Id = "shadow_distance", Title = "Дистанция теней", Description = "Насколько далеко рендерятся тени", Tab = "graphics", Keywords = new[] { "тени", "shadow", "distance", "дальность" }, Priority = 8 },
            new() { Id = "vsync", Title = "VSync", Description = "Вертикальная синхронизация", Tab = "graphics", Keywords = new[] { "vsync", "синхронизация", "вертикал", " tearing" }, Priority = 9 },
            new() { Id = "fps_limit", Title = "Ограничение FPS", Description = "Лимит кадров в секунду", Tab = "graphics", Keywords = new[] { "fps", "лимит", "кадры", "frame", "rate" }, Priority = 10 },
            new() { Id = "resolution", Title = "Разрешение экрана", Description = "Ширина и высота окна игры", Tab = "graphics", Keywords = new[] { "разрешение", "resolution", "экран", "width", "height" }, Priority = 10 },
            new() { Id = "inject_dll", Title = "Инжект DLL", Description = "Активация чит-модулей", Tab = "inject", Keywords = new[] { "инжект", "dll", "чит", "hack", "memory" }, Priority = 10 },
            new() { Id = "panic_reset", Title = "Panic Reset", Description = "Аварийный сброс всех твиков", Tab = "system", Keywords = new[] { "panic", "сброс", "reset", "аварийный", "ctrl+shift+f12" }, Priority = 10 },
            new() { Id = "game_mode", Title = "Игровой режим Windows", Description = "Game Mode для приоритета игре", Tab = "system", Keywords = new[] { "game mode", "игровой режим", "приоритет" }, Priority = 8 },
            new() { Id = "core_parking", Title = "Отключение парковки ядер", Description = "Заставляет CPU работать на всех ядрах", Tab = "system", Keywords = new[] { "core parking", "парковка", "ядра", "cpu" }, Priority = 7 },
            new() { Id = "rkn_bypass", Title = "Обход РКН", Description = "Настройки обхода блокировок", Tab = "network", Keywords = new[] { "ркн", "обход", "блокировка", "zapret", "hosts" }, Priority = 9 },
            new() { Id = "steam_auth", Title = "Авторизация Steam", Description = "Вход через Steam OpenID", Tab = "settings", Keywords = new[] { "steam", "авторизация", "вход", "login" }, Priority = 8 },
            new() { Id = "profile_manager", Title = "Профили конфигов", Description = "Переключение между пресетами", Tab = "settings", Keywords = new[] { "профиль", "пресет", "profile", "config" }, Priority = 8 },
            new() { Id = "benchmark", Title = "Бенчмарк", Description = "Встроенный тест производительности", Tab = "system", Keywords = new[] { "бенчмарк", "benchmark", "тест", "fps", "производительность" }, Priority = 7 },
            new() { Id = "ghost_mode", Title = "Ghost Mode", Description = "Скрытый режим твикера", Tab = "system", Keywords = new[] { "ghost", "скрытый", "трей", "горячие клавиши" }, Priority = 6 },
        };

        public static List<SearchItem> Search(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return new List<SearchItem>();
            var q = query.ToLowerInvariant().Trim();
            return _index
                .Select(item => new
                {
                    Item = item,
                    Score = CalculateScore(item, q)
                })
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .ThenByDescending(x => x.Item.Priority)
                .Select(x => x.Item)
                .ToList();
        }

        private static int CalculateScore(SearchItem item, string query)
        {
            int score = 0;
            if (item.Title.ToLower().Contains(query)) score += 10;
            if (item.Description.ToLower().Contains(query)) score += 5;
            foreach (var kw in item.Keywords)
            {
                if (kw.ToLower().Contains(query)) score += 3;
            }
            // Word boundary match
            var words = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var word in words)
            {
                if (item.Title.ToLower().Contains(word)) score += 2;
                if (item.Keywords.Any(k => k.ToLower().Contains(word))) score += 1;
            }
            return score;
        }

        public static string GetAllItemsJson()
        {
            return Newtonsoft.Json.JsonConvert.SerializeObject(_index);
        }
    }
}
