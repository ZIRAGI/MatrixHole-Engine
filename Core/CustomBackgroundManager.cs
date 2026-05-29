using System;
using System.IO;
using Newtonsoft.Json;

namespace MatrixHole.Core
{
    /// <summary>
    /// CustomBackgroundManager — кастомные фоны и темы оформления (п.59 OptimizatorPlan).
    /// </summary>
    public static class CustomBackgroundManager
    {
        private static readonly string SettingsFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MatrixHole", "background_settings.json");

        public class BackgroundSettings
        {
            public string? ImagePath { get; set; }
            public string? VideoPath { get; set; }
            public double Opacity { get; set; } = 0.15;
            public string BlendMode { get; set; } = "overlay"; // overlay, multiply, screen
            public bool Animated { get; set; } = false;
        }

        public static BackgroundSettings GetSettings()
        {
            try
            {
                if (File.Exists(SettingsFile))
                    return JsonConvert.DeserializeObject<BackgroundSettings>(File.ReadAllText(SettingsFile)) ?? new BackgroundSettings();
            }
            catch { }
            return new BackgroundSettings();
        }

        public static void SaveSettings(BackgroundSettings settings)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsFile)!);
            File.WriteAllText(SettingsFile, JsonConvert.SerializeObject(settings, Formatting.Indented));
        }

        public static string GetCssForBackground()
        {
            var s = GetSettings();
            if (string.IsNullOrEmpty(s.ImagePath) || !File.Exists(s.ImagePath))
                return "";

            var fileUrl = $"file:///{s.ImagePath.Replace('\\', '/')}";
            return $@"
                body::before {{
                    content: '';
                    position: fixed;
                    top: 0; left: 0; right: 0; bottom: 0;
                    background-image: url('{fileUrl}');
                    background-size: cover;
                    background-position: center;
                    opacity: {s.Opacity};
                    mix-blend-mode: {s.BlendMode};
                    pointer-events: none;
                    z-index: -1;
                }}
            ";
        }
    }
}
