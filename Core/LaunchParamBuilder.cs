using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MatrixHole.Core
{
    /// <summary>
    /// LaunchParamBuilder — визуальный конструктор параметров запуска (п.95 OptimizatorPlan).
    /// Чекбоксы для каждого флага Unity/Steam, автосборка строки.
    /// </summary>
    public static class LaunchParamBuilder
    {
        public class LaunchFlag
        {
            public string Id { get; set; } = "";
            public string Name { get; set; } = "";
            public string Description { get; set; } = "";
            public string Flag { get; set; } = "";
            public bool RequiresValue { get; set; } = false;
            public string? DefaultValue { get; set; }
            public string Category { get; set; } = "Unity";
            public bool IsAdvanced { get; set; } = false;
        }

        public static List<LaunchFlag> GetAllFlags()
        {
            return new List<LaunchFlag>
            {
                new() { Id = "force_d3d11", Name = "Force Direct3D 11", Description = "Принудительно использовать DX11 вместо DX12.", Flag = "-force-d3d11", Category = "Graphics" },
                new() { Id = "force_vulkan", Name = "Force Vulkan", Description = "Использовать Vulkan API (экспериментально).", Flag = "-force-vulkan", Category = "Graphics", IsAdvanced = true },
                new() { Id = "force_opengl", Name = "Force OpenGL", Description = "Использовать OpenGL (для старых GPU).", Flag = "-force-opengl", Category = "Graphics" },
                new() { Id = "nographics", Name = "No Graphics", Description = "Запуск без графики (для сервера).", Flag = "-nographics", Category = "Graphics", IsAdvanced = true },
                new() { Id = "batchmode", Name = "Batch Mode", Description = "Фоновый режим без UI.", Flag = "-batchmode", Category = "System", IsAdvanced = true },
                new() { Id = "screen_width", Name = "Screen Width", Description = "Ширина окна при запуске.", Flag = "-screen-width", RequiresValue = true, DefaultValue = "1920", Category = "Graphics" },
                new() { Id = "screen_height", Name = "Screen Height", Description = "Высота окна при запуске.", Flag = "-screen-height", RequiresValue = true, DefaultValue = "1080", Category = "Graphics" },
                new() { Id = "screen_fullscreen", Name = "Fullscreen", Description = "Запуск в полноэкранном режиме.", Flag = "-screen-fullscreen", RequiresValue = true, DefaultValue = "1", Category = "Graphics" },
                new() { Id = "screen_refresh_rate", Name = "Refresh Rate", Description = "Частота обновления экрана.", Flag = "-screen-refresh-rate", RequiresValue = true, DefaultValue = "144", Category = "Graphics" },
                new() { Id = "popupwindow", Name = "Popup Window", Description = "Окно без рамки.", Flag = "-popupwindow", Category = "Graphics" },
                new() { Id = "windowed", Name = "Windowed", Description = "Оконный режим.", Flag = "-windowed", Category = "Graphics" },
                new() { Id = "single_instance", Name = "Single Instance", Description = "Запретить несколько копий игры.", Flag = "-single-instance", Category = "System" },
                new() { Id = "force_cpu_count", Name = "CPU Count", Description = "Ограничить количество потоков Unity.", Flag = "-force-cpu-count", RequiresValue = true, DefaultValue = "4", Category = "System", IsAdvanced = true },
                new() { Id = "force_job_worker_count", Name = "Job Workers", Description = "Количество Job System воркеров.", Flag = "-force-job-worker-count", RequiresValue = true, DefaultValue = "4", Category = "System", IsAdvanced = true },
                new() { Id = "gc_boehm", Name = "GC Boehm", Description = "Использовать Boehm GC вместо IL2CPP дефолтного.", Flag = "-gc-boehm", Category = "Memory", IsAdvanced = true },
                new() { Id = "disable_gpu_skinnin", Name = "Disable GPU Skinning", Description = "Отключить скиннинг на GPU.", Flag = "-disable-gpu-skinning", Category = "Graphics" },
                new() { Id = "weak_http_security", Name = "Weak HTTP Security", Description = "Обход блокировок РКН (пониженная безопасность HTTP).", Flag = "--weak-http-security", Category = "Network" },
                new() { Id = "log_file", Name = "Log File", Description = "Путь для записи логов Unity.", Flag = "-logFile", RequiresValue = true, DefaultValue = "Player.log", Category = "System", IsAdvanced = true },
            };
        }

        public static string BuildLaunchString(Dictionary<string, string?> selectedFlags)
        {
            var sb = new StringBuilder();
            var flags = GetAllFlags();

            foreach (var kvp in selectedFlags)
            {
                var flag = flags.FirstOrDefault(f => f.Id == kvp.Key);
                if (flag == null) continue;

                if (flag.RequiresValue && !string.IsNullOrEmpty(kvp.Value))
                {
                    sb.Append($"{flag.Flag} {EscapeValue(kvp.Value)} ");
                }
                else if (!flag.RequiresValue && bool.TryParse(kvp.Value, out var enabled) && enabled)
                {
                    sb.Append($"{flag.Flag} ");
                }
            }

            return sb.ToString().Trim();
        }

        public static Dictionary<string, string> ParseLaunchString(string launchArgs)
        {
            var result = new Dictionary<string, string>();
            var flags = GetAllFlags();
            var parts = launchArgs.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                var flag = flags.FirstOrDefault(f => f.Flag.Equals(part, StringComparison.OrdinalIgnoreCase));
                if (flag == null) continue;

                if (flag.RequiresValue && i + 1 < parts.Length)
                {
                    result[flag.Id] = parts[i + 1];
                    i++;
                }
                else
                {
                    result[flag.Id] = "true";
                }
            }

            return result;
        }

        public static string Validate(Dictionary<string, string?> selectedFlags)
        {
            var conflicts = new List<string>();
            var ids = selectedFlags.Keys.ToHashSet();

            // Graphics API conflicts
            var graphicsApis = new[] { "force_d3d11", "force_vulkan", "force_opengl" };
            if (graphicsApis.Count(ids.Contains) > 1)
                conflicts.Add("Выбрано несколько графических API. Оставьте только один.");

            // Window mode conflicts
            var windowModes = new[] { "popupwindow", "windowed", "screen_fullscreen" };
            if (windowModes.Count(ids.Contains) > 1)
                conflicts.Add("Конфликт режимов окна.");

            // Resolution validation
            if (ids.Contains("screen_width") && ids.Contains("screen_height"))
            {
                if (!int.TryParse(selectedFlags["screen_width"], out var w) || w < 640)
                    conflicts.Add("Ширина экрана должна быть >= 640.");
                if (!int.TryParse(selectedFlags["screen_height"], out var h) || h < 480)
                    conflicts.Add("Высота экрана должна быть >= 480.");
            }

            return string.Join("; ", conflicts);
        }

        private static string EscapeValue(string value)
        {
            if (value.Contains(' ')) return $"\"{value}\"";
            return value;
        }
    }
}
