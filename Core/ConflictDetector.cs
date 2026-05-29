using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace MatrixHole.Core
{
    /// <summary>
    /// ConflictDetector — авто-обнаружение конфликтующего ПО (п.85 OptimizatorPlan).
    /// MSI Afterburner/Rivatuner, другие инжекторы, конфликтующие с Zapret.
    /// </summary>
    public static class ConflictDetector
    {
        public class ConflictInfo
        {
            public string ProcessName { get; set; } = "";
            public string DisplayName { get; set; } = "";
            public string ConflictType { get; set; } = "";
            public string Description { get; set; } = "";
            public string Recommendation { get; set; } = "";
            public ConflictSeverity Severity { get; set; }
        }

        public enum ConflictSeverity { Warning, Critical, Info }

        private static readonly List<ConflictInfo> KnownConflicts = new()
        {
            new ConflictInfo
            {
                ProcessName = "RTSS",
                DisplayName = "RivaTuner Statistics Server",
                ConflictType = "overlay",
                Description = "Оверлей RTSS может конфликтовать с инжектом DLL твикера.",
                Recommendation = "Закройте RTSS или отключите оверлей для SCPSL.exe.",
                Severity = ConflictSeverity.Warning
            },
            new ConflictInfo
            {
                ProcessName = "MSIAfterburner",
                DisplayName = "MSI Afterburner",
                ConflictType = "overlay",
                Description = "Оверлей Afterburner через RTSS может вызвать падение FPS или краш.",
                Recommendation = "Отключите оверлей или закройте Afterburner.",
                Severity = ConflictSeverity.Warning
            },
            new ConflictInfo
            {
                ProcessName = "winws",
                DisplayName = "Zapret (winws)",
                ConflictType = "network",
                Description = "Zapret уже запущен. Сетевые обходы твикера могут конфликтовать.",
                Recommendation = "Используйте либо Zapret, либо обход твикера — не оба сразу.",
                Severity = ConflictSeverity.Warning
            },
            new ConflictInfo
            {
                ProcessName = "dnspy",
                DisplayName = "dnSpy",
                ConflictType = "debugger",
                Description = "Отладчик dnSpy может быть расценён античитом как угроза.",
                Recommendation = "Закройте dnSpy перед запуском игры.",
                Severity = ConflictSeverity.Critical
            },
            new ConflictInfo
            {
                ProcessName = "cheatengine",
                DisplayName = "Cheat Engine",
                ConflictType = "injector",
                Description = "Cheat Engine — известный инжектор. Риск бана 100%.",
                Recommendation = "Немедленно закройте Cheat Engine перед запуском SCP:SL.",
                Severity = ConflictSeverity.Critical
            },
            new ConflictInfo
            {
                ProcessName = "x64dbg",
                DisplayName = "x64dbg",
                ConflictType = "debugger",
                Description = "Отладчик x64dbg может вызвать детект античита.",
                Recommendation = "Закройте x64dbg перед запуском игры.",
                Severity = ConflictSeverity.Critical
            },
            new ConflictInfo
            {
                ProcessName = "processhacker",
                DisplayName = "Process Hacker",
                ConflictType = "system",
                Description = "Process Hacker предоставляет доступ к памяти процессов.",
                Recommendation = "Закройте Process Hacker перед запуском SCP:SL.",
                Severity = ConflictSeverity.Warning
            },
            new ConflictInfo
            {
                ProcessName = "obs64",
                DisplayName = "OBS Studio",
                ConflictType = "capture",
                Description = "OBS может конфликтовать с оверлеями и хуками.",
                Recommendation = "Используйте Game Capture вместо Display Capture.",
                Severity = ConflictSeverity.Info
            }
        };

        public static List<ConflictInfo> ScanConflicts()
        {
            var running = Process.GetProcesses().Select(p => p.ProcessName.ToLowerInvariant()).ToHashSet();
            var found = new List<ConflictInfo>();

            foreach (var conflict in KnownConflicts)
            {
                var names = conflict.ProcessName.Split('|');
                if (names.Any(n => running.Contains(n.ToLowerInvariant())))
                    found.Add(conflict);
            }

            return found;
        }

        public static bool HasCriticalConflicts()
        {
            return ScanConflicts().Any(c => c.Severity == ConflictSeverity.Critical);
        }

        public static string GetConflictsJson()
        {
            var conflicts = ScanConflicts();
            return Newtonsoft.Json.JsonConvert.SerializeObject(new
            {
                hasConflicts = conflicts.Any(),
                hasCritical = conflicts.Any(c => c.Severity == ConflictSeverity.Critical),
                conflicts = conflicts.Select(c => new
                {
                    c.DisplayName,
                    c.ConflictType,
                    c.Description,
                    c.Recommendation,
                    severity = c.Severity.ToString()
                })
            });
        }
    }
}
