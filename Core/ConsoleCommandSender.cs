using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace MatrixHole.Core
{
    /// <summary>
    /// ConsoleCommandSender — ручной ввод команд консоли SCP:SL (п.80 OptimizatorPlan).
    /// Команды шлются в игру через DLL hook или memory write (placeholder).
    /// </summary>
    public static class ConsoleCommandSender
    {
        private const string PIPE_NAME = "MatrixHoleInjector";

        public class ConsoleCommand
        {
            public string Id { get; set; } = "";
            public string Name { get; set; } = "";
            public string Command { get; set; } = "";
            public string Description { get; set; } = "";
            public bool RequiresDll { get; set; } = true;
        }

        public static List<ConsoleCommand> GetAvailableCommands()
        {
            return new List<ConsoleCommand>
            {
                new() { Id = "fpscap", Name = "FPS Cap", Command = "setfpscap {0}", Description = "Ограничение FPS. 0 = без ограничения.", RequiresDll = true },
                new() { Id = "renderdist", Name = "Render Distance", Command = "renderdist {0}", Description = "Дальность прорисовки.", RequiresDll = true },
                new() { Id = "noshadows", Name = "No Shadows", Command = "noshadows {0}", Description = "Отключение теней. 1 = выкл.", RequiresDll = true },
                new() { Id = "nofog", Name = "No Fog", Command = "nofog {0}", Description = "Отключение тумана.", RequiresDll = true },
                new() { Id = "vsync", Name = "VSync", Command = "vsync {0}", Description = "Вертикальная синхронизация. 0/1.", RequiresDll = true },
                new() { Id = "quality", Name = "Quality Level", Command = "quality {0}", Description = "Уровень качества. 0-5.", RequiresDll = true },
                new() { Id = "timescale", Name = "Time Scale", Command = "timescale {0}", Description = "Масштаб времени. 1.0 = норма.", RequiresDll = true },
                new() { Id = "gc", Name = "Force GC", Command = "gc.collect", Description = "Принудительная сборка мусора.", RequiresDll = true },
                new() { Id = "memreport", Name = "Memory Report", Command = "mem.report", Description = "Отчёт о памяти в консоль Unity.", RequiresDll = true },
            };
        }

        public static string SendCommand(string commandId, params object[] args)
        {
            var cmd = GetAvailableCommands().FirstOrDefault(c => c.Id == commandId);
            if (cmd == null) return "{\"ok\":false,\"error\":\"Unknown command\"}";

            if (cmd.RequiresDll && !IsDllActive())
                return "{\"ok\":false,\"error\":\"DLL not active\"}";

            var fullCommand = string.Format(cmd.Command, args);

            var pipePath = $"\\\\.\\pipe\\{PIPE_NAME}";
            try
            {
                // Try to connect to named pipe created by DLL
                using var client = new System.IO.Pipes.NamedPipeClientStream(".", PIPE_NAME, System.IO.Pipes.PipeDirection.Out);
                client.Connect(100);
                using var writer = new System.IO.StreamWriter(client);
                writer.WriteLine(fullCommand);
                return $"{{\"ok\":true,\"command\":\"{fullCommand.Replace("\"", "'")}\"}}";
            }
            catch
            {
                return $"{{\"ok\":false,\"error\":\"DLL pipe not available. Command queued: {fullCommand.Replace("\"", "'")}\"}}";
            }
        }

        public static string SendRaw(string rawCommand)
        {
            if (!IsDllActive())
                return "{\"ok\":false,\"error\":\"DLL not active\"}";

            try
            {
                using var client = new System.IO.Pipes.NamedPipeClientStream(".", PIPE_NAME, System.IO.Pipes.PipeDirection.Out);
                client.Connect(100);
                using var writer = new System.IO.StreamWriter(client);
                writer.WriteLine(rawCommand);
                return $"{{\"ok\":true,\"command\":\"{rawCommand.Replace("\"", "'")}\"}}";
            }
            catch (Exception ex)
            {
                return $"{{\"ok\":false,\"error\":\"{ex.Message.Replace("\"", "'")}\"}}";
            }
        }

        private static bool IsDllActive()
        {
            // Check if any plugin is active or memory patcher is injected
            return PluginManager.GetPlugins().Any(p => p.Enabled);
        }
    }
}
