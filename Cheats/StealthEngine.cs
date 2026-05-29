using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace MatrixHole.Cheats
{
    /// <summary>
    /// Multi-layer anti-cheat evasion engine.
    /// 
    /// LAYER 1 — FILE LEVEL (pre-launch):
    ///   • Backups and patches SL-AC.dll on disk (NOP entry point)
    ///   • Replaces SL-AC with a dummy DLL that exports the same functions but does nothing
    ///   • Patches boot.config and globalgamemanagers for FPS
    ///   
    /// LAYER 2 — PROCESS LEVEL (launch-time):
    ///   • Launches the game via Steam so timestamps look legit
    ///   • Optionally self-terminates this tweaker process after launch
    ///   • Hides window from taskbar / alt-tab (if kept alive)
    ///   
    /// LAYER 3 — MEMORY LEVEL (runtime, optional):
    ///   • Only available if user explicitly enables "Unsafe Mode"
    ///   • Patches GameAssembly.dll in memory (requires SL-AC disabled first)
    ///   
    /// DISCLAIMER: No user-mode C# tool can guarantee 100% evasion against a kernel-level
    /// anti-cheat. This engine maximizes odds by removing the anti-cheat BEFORE it loads,
    /// and eliminating the tweaker process before the game starts.
    /// </summary>
    public class StealthEngine
    {
        private readonly string _gamePath;
        private readonly string _gameDataPath;
        private readonly AntiCheatBypass _acBypass;
        private readonly BootConfigOptimizer _bootOpt;
        private readonly GlobalGameManagersPatcher _ggmPatcher;

        public StealthEngine(string gamePath)
        {
            _gamePath = gamePath;
            _gameDataPath = Path.Combine(gamePath, "SCPSL_Data");
            _acBypass = new AntiCheatBypass(gamePath);
            _bootOpt = new BootConfigOptimizer(_gameDataPath);
            _ggmPatcher = new GlobalGameManagersPatcher(_gameDataPath);
        }

        // ============================================================
        // LAYER 1 — FILE PREPARATION
        // ============================================================

        /// <summary>
        /// Prepares the game folder for stealth launch.
        /// Returns JSON status of every layer.
        /// </summary>
        public string PrepareEnvironment(bool patchAc, bool patchBoot, bool patchGgm)
        {
            var sb = new StringBuilder();
            sb.Append("{");

            // --- SL-AC bypass ---
            if (patchAc)
            {
                var acResult = _acBypass.PatchSlAcOnDisk();
                sb.Append($"\"anti_cheat\":{acResult},");
            }
            else
            {
                sb.Append("\"anti_cheat\":{\"skipped\":true},");
            }

            // --- boot.config ---
            if (patchBoot)
            {
                var bootResult = _bootOpt.ApplyFpsTweaks();
                sb.Append($"\"boot_config\":{bootResult},");
            }
            else
            {
                sb.Append("\"boot_config\":{\"skipped\":true},");
            }

            // --- globalgamemanagers ---
            if (patchGgm)
            {
                var ggmResult = _ggmPatcher.ApplyFpsPatch();
                sb.Append($"\"global_gm\":{ggmResult},");
            }
            else
            {
                sb.Append("\"global_gm\":{\"skipped\":true},");
            }

            sb.Append("\"ready\":true}");
            return sb.ToString();
        }

        /// <summary>
        /// Restores original game files (AC, boot.config, globalgamemanagers).
        /// </summary>
        public string RestoreEnvironment()
        {
            var sb = new StringBuilder();
            sb.Append("{");

            var acResult = _acBypass.RestoreOriginalDll();
            sb.Append($"\"anti_cheat\":{acResult},");

            var bootResult = _bootOpt.RestoreLatestBackup();
            sb.Append($"\"boot_config\":{bootResult},");

            var ggmResult = _ggmPatcher.RestoreLatestBackup();
            sb.Append($"\"global_gm\":{ggmResult},");

            sb.Append("\"restored\":true}");
            return sb.ToString();
        }

        // ============================================================
        // LAYER 2 — STEALTH LAUNCH
        // ============================================================

        /// <summary>
        /// Launches the game via Steam with optional self-destruct of this process.
        /// If selfDestruct = true, the tweaker EXE terminates immediately after
        /// Steam accepts the launch command. This leaves ZERO running processes
        /// for the anti-cheat to scan.
        /// </summary>
        public string LaunchGameStealth(bool selfDestruct)
        {
            try
            {
                // Use Steam browser protocol to launch — looks like a normal Steam launch
                var startInfo = new ProcessStartInfo
                {
                    FileName = "steam.exe",
                    Arguments = "-applaunch 700330",
                    UseShellExecute = true,
                    CreateNoWindow = true
                };

                // Try to find steam.exe in common locations
                string? steamPath = FindSteamExe();
                if (!string.IsNullOrEmpty(steamPath))
                    startInfo.FileName = steamPath;

                Process.Start(startInfo);

                if (selfDestruct)
                {
                    // Give Steam a few seconds to register the launch
                    Thread.Sleep(3000);
                    // Terminate self
                    TerminateSelf();
                    return "{\"launched\":true,\"self_destruct\":true}";
                }

                return "{\"launched\":true,\"self_destruct\":false}";
            }
            catch (Exception ex)
            {
                return $"{{\"error\":\"{ex.Message.Replace("\"","'")}\"}}";
            }
        }

        /// <summary>
        /// Kills every running instance of this EXE. Use with caution.
        /// </summary>
        private void TerminateSelf()
        {
            try
            {
                var current = Process.GetCurrentProcess();
                var exeName = Path.GetFileNameWithoutExtension(current.MainModule?.FileName ?? "MatrixHole");
                foreach (var proc in Process.GetProcessesByName(exeName))
                {
                    try { proc.Kill(); } catch { /* ignore */ }
                }
                Environment.Exit(0);
            }
            catch { Environment.Exit(1); }
        }

        private string? FindSteamExe()
        {
            // Common Steam paths
            var candidates = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam", "steam.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam", "steam.exe"),
                Path.Combine("C:\\Steam", "steam.exe"),
                Path.Combine("D:\\Steam", "steam.exe")
            };
            foreach (var c in candidates)
                if (File.Exists(c)) return c;
            return null;
        }

        /// <summary>
        /// Checks whether SCP:SL is currently running.
        /// </summary>
        public string IsGameRunning()
        {
            try
            {
                var procs = Process.GetProcessesByName("SCPSL");
                bool running = procs.Length > 0;
                foreach (var p in procs) { try { p.Dispose(); } catch { } }
                return $"{{\"running\":{running.ToString().ToLower()}}}";
            }
            catch (Exception ex)
            {
                return $"{{\"error\":\"{ex.Message.Replace("\"","'")}\"}}";
            }
        }

        // ============================================================
        // LAYER 3 — RUNTIME MEMORY (UNSAFE)
        // ============================================================

        /// <summary>
        /// Applies runtime memory patches. ONLY call this AFTER SL-AC is disabled
        /// and the game is already running. This is the HIGHEST RISK layer.
        /// </summary>
        public string ApplyUnsafeMemoryPatches()
        {
            try
            {
                var patcher = new MemoryPatcher("SCPSL");
                var result = patcher.ApplyAllPatches();
                return result;
            }
            catch (Exception ex)
            {
                return $"{{\"error\":\"{ex.Message.Replace("\"","'")}\"}}";
            }
        }
    }
}
