using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace MatrixHole
{
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.AutoDual)]
    [ProgId("MatrixHole.CsApi")]
    public class CsApi
    {
        private static readonly JsonSerializerSettings _jsonSettings = new JsonSerializerSettings
        {
            ContractResolver = new DefaultContractResolver { NamingStrategy = new SnakeCaseNamingStrategy() },
            Formatting = Formatting.None
        };

        private static string ReportWebhookUrl => Core.AppSecrets.DiscordWebhook;

        private void LogAction(string action, string detail) => Core.DevLogger.Action(action, "CsApi", detail);

        // ============================================================
        // APP SETTINGS (persistent language etc)
        // ============================================================

        private static string GetSettingsPath()
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MatrixHole");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return Path.Combine(dir, "app_settings.json");
        }

        public string GetAppSetting(string key)
        {
            try
            {
                var path = GetSettingsPath();
                if (!File.Exists(path)) return "";
                var json = File.ReadAllText(path);
                var dict = JsonConvert.DeserializeObject<System.Collections.Generic.Dictionary<string, string>>(json);
                return dict != null && dict.TryGetValue(key, out var val) ? val : "";
            }
            catch { return ""; }
        }

        public string SaveAppSetting(string key, string value)
        {
            try
            {
                var path = GetSettingsPath();
                var dict = new System.Collections.Generic.Dictionary<string, string>();
                if (File.Exists(path))
                {
                    var existing = JsonConvert.DeserializeObject<System.Collections.Generic.Dictionary<string, string>>(File.ReadAllText(path));
                    if (existing != null) dict = existing;
                }
                dict[key] = value;
                File.WriteAllText(path, JsonConvert.SerializeObject(dict, Formatting.Indented));
                return "{\"ok\":true}";
            }
            catch (Exception ex) { return JsonConvert.SerializeObject(new { ok = false, error = ex.Message }); }
        }

        // ============================================================
        // GAME DETECTION
        // ============================================================

        public string DetectGame()
        {
            var info = Core.GameDetector.Detect();
            if (info == null) return JsonConvert.SerializeObject(new { found = false });
            return JsonConvert.SerializeObject(new { found = true, path = info.InstallPath, is_steam = info.IsSteamVersion });
        }

        // ============================================================
        // CONFIG (config.ini)
        // ============================================================

        public string LoadConfig()
        {
            var cfg = Core.ConfigManager.Load();
            return cfg == null ? "{}" : JsonConvert.SerializeObject(cfg, _jsonSettings);
        }

        public string SaveConfig(string json)
        {
            try
            {
                var cfg = JsonConvert.DeserializeObject<Core.ConfigManager.ScpGameConfig>(json, _jsonSettings);
                if (cfg == null) return "{\"ok\":false,\"error\":\"parse\"}";
                var ok = Core.ConfigManager.Save(cfg);
                if (ok) LogAction("CONFIG_SAVE", json);
                return JsonConvert.SerializeObject(new { ok });
            }
            catch (Exception ex) { return JsonConvert.SerializeObject(new { ok = false, error = ex.Message }); }
        }

        public string ApplyPreset(string preset)
        {
            LogAction("PRESET_APPLY", preset);
            return JsonConvert.SerializeObject(Core.ConfigManager.ApplyPreset(preset), _jsonSettings);
        }

        // ============================================================
        // BACKUPS
        // ============================================================

        public string CreateBackup()
        {
            var path = Core.BackupManager.CreateBackup();
            return JsonConvert.SerializeObject(new { ok = !string.IsNullOrEmpty(path), path });
        }

        public string RestoreLatestBackup()
        {
            return JsonConvert.SerializeObject(new { ok = Core.BackupManager.RestoreLatest() });
        }

        public string ListBackups()
        {
            return JsonConvert.SerializeObject(Core.BackupManager.ListBackups());
        }

        // ============================================================
        // LAUNCH OPTIONS
        // ============================================================

        public string GetLaunchOptions()
        {
            return Core.GameDetector.GetSteamLaunchOptions() ?? "";
        }

        public string SetLaunchOptions(string options)
        {
            LogAction("LAUNCH_OPTIONS", options);
            return JsonConvert.SerializeObject(new { ok = Core.GameDetector.SetSteamLaunchOptions(options) });
        }

        public void LaunchGame()
        {
            LogAction("GAME_LAUNCH", "direct");
            Core.LaunchOptionsManager.LaunchGame();
        }

        public void OpenGameFolder()
        {
            var info = Core.GameDetector.Detect();
            if (info != null && info.IsInstalled)
            {
                LogAction("OPEN_FOLDER", info.InstallPath);
                System.Diagnostics.Process.Start("explorer.exe", info.InstallPath);
            }
        }

        // ============================================================
        // LAYER 1 — ANTI-CHEAT BYPASS (file level)
        // ============================================================

        public string EnableAcBypass()
        {
            var info = Core.GameDetector.Detect();
            if (info == null || !info.IsInstalled) return "{\"error\":\"Game not found\"}";
            var ac = new Cheats.AntiCheatBypass(info.InstallPath);
            LogAction("CHEAT_AC", "enable");
            return ac.PatchSlAcOnDisk();
        }

        public string DisableAcBypass()
        {
            var info = Core.GameDetector.Detect();
            if (info == null || !info.IsInstalled) return "{\"error\":\"Game not found\"}";
            var ac = new Cheats.AntiCheatBypass(info.InstallPath);
            LogAction("CHEAT_AC", "disable");
            return ac.RestoreOriginalDll();
        }

        // ============================================================
        // LAYER 1 — BOOT CONFIG OPTIMIZER (file level, undetectable)
        // ============================================================

        public string ApplyBootConfigFps()
        {
            var info = Core.GameDetector.Detect();
            if (info == null || !info.IsInstalled) return "{\"error\":\"Game not found\"}";
            var opt = new Cheats.BootConfigOptimizer(Path.Combine(info.InstallPath, "SCPSL_Data"));
            LogAction("CHEAT_BOOT", "apply");
            return opt.ApplyFpsTweaks();
        }

        public string RestoreBootConfig()
        {
            var info = Core.GameDetector.Detect();
            if (info == null || !info.IsInstalled) return "{\"error\":\"Game not found\"}";
            var opt = new Cheats.BootConfigOptimizer(Path.Combine(info.InstallPath, "SCPSL_Data"));
            LogAction("CHEAT_BOOT", "restore");
            return opt.RestoreLatestBackup();
        }

        // ============================================================
        // LAYER 1 — GLOBALGAMEMANAGERS PATCHER (file level, undetectable)
        // ============================================================

        public string ApplyGlobalGameManagersFps()
        {
            var info = Core.GameDetector.Detect();
            if (info == null || !info.IsInstalled) return "{\"error\":\"Game not found\"}";
            var patcher = new Cheats.GlobalGameManagersPatcher(Path.Combine(info.InstallPath, "SCPSL_Data"));
            LogAction("CHEAT_GGM", "apply");
            return patcher.ApplyFpsPatch();
        }

        public string RestoreGlobalGameManagers()
        {
            var info = Core.GameDetector.Detect();
            if (info == null || !info.IsInstalled) return "{\"error\":\"Game not found\"}";
            var patcher = new Cheats.GlobalGameManagersPatcher(Path.Combine(info.InstallPath, "SCPSL_Data"));
            LogAction("CHEAT_GGM", "restore");
            return patcher.RestoreLatestBackup();
        }

        // ============================================================
        // LAYER 1 — GAMEASSEMBLY HEX PATCHER (file level, NUCLEAR)
        // ============================================================

        public string AnalyzeGameAssembly()
        {
            var info = Core.GameDetector.Detect();
            if (info == null || !info.IsInstalled) return "{\"error\":\"Game not found\"}";
            var patcher = new Cheats.GameAssemblyPatcher(info.InstallPath);
            return patcher.AnalyzeFile();
        }

        public string ApplyGameAssemblyPatches()
        {
            var info = Core.GameDetector.Detect();
            if (info == null || !info.IsInstalled) return "{\"error\":\"Game not found\"}";
            var patcher = new Cheats.GameAssemblyPatcher(info.InstallPath);
            LogAction("CHEAT_GA", "apply");
            return patcher.ApplyAllPatches();
        }

        public string RestoreGameAssembly()
        {
            var info = Core.GameDetector.Detect();
            if (info == null || !info.IsInstalled) return "{\"error\":\"Game not found\"}";
            var patcher = new Cheats.GameAssemblyPatcher(info.InstallPath);
            LogAction("CHEAT_GA", "restore");
            return patcher.RestoreLatestBackup();
        }

        // ============================================================
        // LAYER 2 — STEALTH ENGINE (launch-time)
        // ============================================================

        public string PrepareStealthEnvironment(bool patchAc, bool patchBoot, bool patchGgm)
        {
            var info = Core.GameDetector.Detect();
            if (info == null || !info.IsInstalled) return "{\"error\":\"Game not found\"}";
            var engine = new Cheats.StealthEngine(info.InstallPath);
            LogAction("STEALTH_PREPARE", $"ac={patchAc},boot={patchBoot},ggm={patchGgm}");
            return engine.PrepareEnvironment(patchAc, patchBoot, patchGgm);
        }

        public string RestoreStealthEnvironment()
        {
            var info = Core.GameDetector.Detect();
            if (info == null || !info.IsInstalled) return "{\"error\":\"Game not found\"}";
            var engine = new Cheats.StealthEngine(info.InstallPath);
            return engine.RestoreEnvironment();
        }

        public string LaunchGameStealth(bool selfDestruct)
        {
            var info = Core.GameDetector.Detect();
            if (info == null || !info.IsInstalled) return "{\"error\":\"Game not found\"}";
            var engine = new Cheats.StealthEngine(info.InstallPath);
            LogAction("STEALTH_LAUNCH", $"selfDestruct={selfDestruct}");
            return engine.LaunchGameStealth(selfDestruct);
        }

        // ============================================================
        // LAYER 3 — MEMORY PATCHER (runtime, HIGH RISK)
        // ============================================================

        public string GetMemoryPatchDatabase()
        {
            var patcher = new Cheats.MemoryPatcher("SCPSL");
            var db = patcher.GetPatchDatabase().Select(p => new
            {
                p.Id,
                p.Name,
                p.Description,
                p.IsPlaceholder
            });
            return JsonConvert.SerializeObject(new { patches = db });
        }

        public string ApplyAllMemoryPatches()
        {
            LogAction("MEMORY_PATCH", "apply_all");
            var patcher = new Cheats.MemoryPatcher("SCPSL");
            return patcher.ApplyAllPatches();
        }

        public string ApplyMemoryPatchById(string id)
        {
            LogAction("MEMORY_PATCH", $"id={id}");
            var patcher = new Cheats.MemoryPatcher("SCPSL");
            return patcher.ApplyPatchById(id);
        }

        public string ApplyCustomMemoryPatch(string pattern, string mask, string patchHex, int offset = 0)
        {
            LogAction("MEMORY_PATCH", "custom");
            var patcher = new Cheats.MemoryPatcher("SCPSL");
            var r = patcher.ApplyCustomPatch(pattern, mask, patchHex, offset);
            return JsonConvert.SerializeObject(new { success = r.Success, message = r.Message, address = r.Address.ToInt64().ToString("X") });
        }

        public string ApplyMemoryPatchDatabaseFromFile(string path)
        {
            LogAction("MEMORY_PATCH", $"db_from_file={path}");
            var patcher = new Cheats.MemoryPatcher("SCPSL");
            return patcher.ApplyPatchDatabaseFromFile(path);
        }

        // Legacy generic memory patch (expert use)
        // ============================================================
        // SYSTEM OPTIMIZATIONS
        // ============================================================

        public string SetProcessHighPriority() { LogAction("SYS", "high_priority"); return JsonConvert.SerializeObject(new { ok = Sys.ProcessOptimizer.SetHighPriority() }); }
        public string SetProcessRealtime() { LogAction("SYS", "realtime"); return JsonConvert.SerializeObject(new { ok = Sys.ProcessOptimizer.SetRealtimePriority() }); }
        public string SetProcessAffinity() { LogAction("SYS", "affinity"); return JsonConvert.SerializeObject(new { ok = Sys.ProcessOptimizer.SetAffinityPhysicalCores() }); }
        public string ClearWorkingSet() { LogAction("SYS", "clear_workingset"); return JsonConvert.SerializeObject(new { ok = Sys.ProcessOptimizer.ClearWorkingSets() }); }
        public string SetTimerRes() { LogAction("SYS", "timer_res"); return JsonConvert.SerializeObject(new { ok = Sys.ProcessOptimizer.SetTimerResolution() }); }
        public string KillServices() { LogAction("SYS", "kill_services"); return JsonConvert.SerializeObject(new { ok = true, count = Sys.ProcessOptimizer.KillServices() }); }

        public string EnableGameMode() { LogAction("SYS", "game_mode_on"); return JsonConvert.SerializeObject(new { ok = Sys.SystemOptimizer.EnableGameMode() }); }
        public string DisableGameMode() { LogAction("SYS", "game_mode_off"); return JsonConvert.SerializeObject(new { ok = Sys.SystemOptimizer.DisableGameMode() }); }
        public string DisableGameBar() { LogAction("SYS", "gamebar_off"); return JsonConvert.SerializeObject(new { ok = Sys.SystemOptimizer.DisableGameBar() }); }
        public string EnableGameBar() { LogAction("SYS", "gamebar_on"); return JsonConvert.SerializeObject(new { ok = Sys.SystemOptimizer.EnableGameBar() }); }
        public string DisableCoreParking() { LogAction("SYS", "corepark_off"); return JsonConvert.SerializeObject(new { ok = Sys.SystemOptimizer.DisableCoreParking() }); }
        public string EnableCoreParking() { LogAction("SYS", "corepark_on"); return JsonConvert.SerializeObject(new { ok = Sys.SystemOptimizer.EnableCoreParking() }); }
        public string SetHighPerfPlan() { LogAction("SYS", "high_perf"); return JsonConvert.SerializeObject(new { ok = Sys.SystemOptimizer.SetHighPerformancePowerPlan() }); }
        public string SetBalancedPowerPlan() { LogAction("SYS", "balanced"); return JsonConvert.SerializeObject(new { ok = Sys.SystemOptimizer.SetBalancedPowerPlan() }); }
        public string DisableFullscreenOpt()
        {
            var info = Core.GameDetector.Detect();
            if (info != null && info.IsInstalled)
                return JsonConvert.SerializeObject(new { ok = Sys.SystemOptimizer.DisableFullscreenOptimizations(info.ExePath) });
            return JsonConvert.SerializeObject(new { ok = false, message = "Game not found" });
        }
        public string EnableFullscreenOpt()
        {
            var info = Core.GameDetector.Detect();
            if (info != null && info.IsInstalled)
                return JsonConvert.SerializeObject(new { ok = Sys.SystemOptimizer.EnableFullscreenOptimizations(info.ExePath) });
            return JsonConvert.SerializeObject(new { ok = false, message = "Game not found" });
        }

        // ============================================================
        // NETWORK
        // ============================================================

        public string ApplyRknBypass()
        {
            try
            {
                int ok = 0;
                if (Network.RknBypass.ApplyHosts()) ok++;
                if (Network.RknBypass.ApplyCloudflareDns()) ok++;
                if (Network.RknBypass.ApplyWinHttpProxy()) ok++;
                LogAction("RKN_BYPASS", "apply");
                return JsonConvert.SerializeObject(new { ok = true, applied = ok });
            }
            catch (Exception ex) { return JsonConvert.SerializeObject(new { ok = false, error = ex.Message }); }
        }

        public string ResetRknBypass()
        {
            try
            {
                Network.RknBypass.ResetHosts();
                Network.RknBypass.ResetDns();
                Network.RknBypass.ResetProxy();
                LogAction("RKN_BYPASS", "reset");
                return JsonConvert.SerializeObject(new { ok = true });
            }
            catch (Exception ex) { return JsonConvert.SerializeObject(new { ok = false, error = ex.Message }); }
        }

        public string AutoResetRknIfNoInternet()
        {
            var reset = Network.RknBypass.AutoResetIfNoInternet();
            return JsonConvert.SerializeObject(new { reset, message = reset ? "No internet detected — RKN bypass auto-reset to restore connectivity." : "Internet connection OK." });
        }

        public string IsZapretRunning() => JsonConvert.SerializeObject(new { running = Network.RknBypass.IsZapretRunning() });

        public string GetNetworkDiagnosticInfo() => Network.RknBypass.GetNetworkDiagnosticInfo();

        public string GetAppVersion() => "1.0.0-beta";

        // ============================================================
        // EXTRA SYSTEM TWEAKS
        // ============================================================
        public string DisableHPET() { LogAction("SYS", "hpet_off"); return JsonConvert.SerializeObject(new { ok = Sys.SystemOptimizer.DisableHPET() }); }
        public string EnableHPET() { LogAction("SYS", "hpet_on"); return JsonConvert.SerializeObject(new { ok = Sys.SystemOptimizer.EnableHPET() }); }
        public string DisableNagle() { LogAction("SYS", "nagle_off"); return JsonConvert.SerializeObject(new { ok = Sys.SystemOptimizer.DisableNagle() }); }
        public string EnableNagle() { LogAction("SYS", "nagle_on"); return JsonConvert.SerializeObject(new { ok = Sys.SystemOptimizer.EnableNagle() }); }
        public string DisableSysMain() { LogAction("SYS", "sysmain_off"); return JsonConvert.SerializeObject(new { ok = Sys.SystemOptimizer.DisableSysMain() }); }
        public string EnableSysMain() { LogAction("SYS", "sysmain_on"); return JsonConvert.SerializeObject(new { ok = Sys.SystemOptimizer.EnableSysMain() }); }
        public string DisableVisualEffects() { LogAction("SYS", "visfx_off"); return JsonConvert.SerializeObject(new { ok = Sys.SystemOptimizer.DisableVisualEffects() }); }
        public string EnableVisualEffects() { LogAction("SYS", "visfx_on"); return JsonConvert.SerializeObject(new { ok = Sys.SystemOptimizer.EnableVisualEffects() }); }

        // ============================================================
        // EXTRA FILE NUKES (FPS optimization)
        // ============================================================
        public string DisableCrashHandler()
        {
            try
            {
                var info = Core.GameDetector.Detect();
                if (info == null || !info.IsInstalled) return JsonConvert.SerializeObject(new { success = false, error = "game not found" });
                var exe = System.IO.Directory.GetFiles(info.InstallPath, "UnityCrashHandler*.exe").FirstOrDefault();
                if (exe != null && !System.IO.File.Exists(exe + ".bak"))
                {
                    System.IO.File.Move(exe, exe + ".bak");
                    LogAction("FILE_NUKE", "crash_handler_off");
                    return JsonConvert.SerializeObject(new { success = true, file = System.IO.Path.GetFileName(exe) });
                }
                return JsonConvert.SerializeObject(new { success = false, error = "not found or already patched" });
            }
            catch (Exception ex) { return JsonConvert.SerializeObject(new { success = false, error = ex.Message }); }
        }
        public string EnableCrashHandler()
        {
            try
            {
                var info = Core.GameDetector.Detect();
                if (info == null || !info.IsInstalled) return JsonConvert.SerializeObject(new { success = false, error = "game not found" });
                var bak = System.IO.Directory.GetFiles(info.InstallPath, "UnityCrashHandler*.exe.bak").FirstOrDefault();
                if (bak != null)
                {
                    var orig = bak.Substring(0, bak.Length - 4);
                    System.IO.File.Move(bak, orig);
                    LogAction("FILE_NUKE", "crash_handler_on");
                    return JsonConvert.SerializeObject(new { success = true, file = System.IO.Path.GetFileName(orig) });
                }
                return JsonConvert.SerializeObject(new { success = false, error = "backup not found" });
            }
            catch (Exception ex) { return JsonConvert.SerializeObject(new { success = false, error = ex.Message }); }
        }
        public string DisableAnalytics()
        {
            try
            {
                var info = Core.GameDetector.Detect();
                if (info == null || !info.IsInstalled) return JsonConvert.SerializeObject(new { success = false, error = "game not found" });
                var dll = System.IO.Directory.GetFiles(info.InstallPath, "*analytics*.dll", System.IO.SearchOption.AllDirectories).FirstOrDefault();
                if (dll != null && !System.IO.File.Exists(dll + ".bak"))
                {
                    System.IO.File.Move(dll, dll + ".bak");
                    LogAction("FILE_NUKE", "analytics_off");
                    return JsonConvert.SerializeObject(new { success = true, file = System.IO.Path.GetFileName(dll) });
                }
                return JsonConvert.SerializeObject(new { success = false, error = "not found or already patched" });
            }
            catch (Exception ex) { return JsonConvert.SerializeObject(new { success = false, error = ex.Message }); }
        }
        public string EnableAnalytics()
        {
            try
            {
                var info = Core.GameDetector.Detect();
                if (info == null || !info.IsInstalled) return JsonConvert.SerializeObject(new { success = false, error = "game not found" });
                var bak = System.IO.Directory.GetFiles(info.InstallPath, "*analytics*.dll.bak", System.IO.SearchOption.AllDirectories).FirstOrDefault();
                if (bak != null)
                {
                    var orig = bak.Substring(0, bak.Length - 4);
                    System.IO.File.Move(bak, orig);
                    LogAction("FILE_NUKE", "analytics_on");
                    return JsonConvert.SerializeObject(new { success = true, file = System.IO.Path.GetFileName(orig) });
                }
                return JsonConvert.SerializeObject(new { success = false, error = "backup not found" });
            }
            catch (Exception ex) { return JsonConvert.SerializeObject(new { success = false, error = ex.Message }); }
        }

        // ============================================================
        // SYNC — Read current system & game state for UI alignment
        // ============================================================

        public string GetSystemState()
        {
            var info = Core.GameDetector.Detect();
            return JsonConvert.SerializeObject(new
            {
                game_mode = Sys.SystemOptimizer.IsGameModeEnabled(),
                game_bar_disabled = Sys.SystemOptimizer.IsGameBarDisabled(),
                core_parking_disabled = Sys.SystemOptimizer.IsCoreParkingDisabled(),
                high_perf_plan = Sys.SystemOptimizer.IsHighPerformancePlanActive(),
                fso_disabled = info != null && Sys.SystemOptimizer.IsFullscreenOptDisabled(info.ExePath),
                hpet_disabled = Sys.SystemOptimizer.IsHPETDisabled(),
                nagle_disabled = Sys.SystemOptimizer.IsNagleDisabled(),
                sysmain_disabled = Sys.SystemOptimizer.IsSysMainDisabled(),
                visual_effects_disabled = Sys.SystemOptimizer.IsVisualEffectsDisabled()
            }, _jsonSettings);
        }

        public string GetCheatsState()
        {
            var info = Core.GameDetector.Detect();
            if (info == null || !info.IsInstalled) return "{}";
            var ac = new Cheats.AntiCheatBypass(info.InstallPath);
            var acStatus = ac.GetStatus();
            bool acActive = false;
            try { acActive = JsonConvert.DeserializeObject<dynamic>(acStatus)?.active == true; } catch { }
            bool bootPatched = System.IO.File.Exists(System.IO.Path.Combine(info.InstallPath, "SCPSL_Data", "boot.config.bak"));
            bool ggmPatched = System.IO.File.Exists(System.IO.Path.Combine(info.InstallPath, "SCPSL_Data", "globalgamemanagers.bak"));
            bool gaPatched = System.IO.File.Exists(System.IO.Path.Combine(info.InstallPath, "GameAssembly.dll.bak"));
            return JsonConvert.SerializeObject(new
            {
                ac_active = acActive,
                boot_patched = bootPatched,
                ggm_patched = ggmPatched,
                ga_patched = gaPatched
            }, _jsonSettings);
        }

        public string GetRknState()
        {
            try
            {
                bool hosts = Network.RknBypass.IsHostsModified();
                bool dns = Network.RknBypass.IsCloudflareDnsActive();
                bool proxy = Network.RknBypass.IsWinHttpProxySet();
                return JsonConvert.SerializeObject(new { active = hosts || dns || proxy, hosts, dns, proxy }, _jsonSettings);
            }
            catch (Exception ex) { return JsonConvert.SerializeObject(new { active = false, error = ex.Message }); }
        }

        public string GenerateReport(string userComment)
        {
            LogAction("REPORT_GENERATE", userComment);
            try
            {
                var info = Core.GameDetector.Detect();
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("=== MatrixHole-Engine Report ===");
                sb.AppendLine($"Version: {GetAppVersion()}");
                sb.AppendLine($"Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"OS: {Environment.OSVersion}");
                sb.AppendLine();
                sb.AppendLine("--- Game ---");
                sb.AppendLine($"Found: {info?.IsInstalled ?? false}");
                sb.AppendLine($"Path: {info?.InstallPath ?? "N/A"}");
                sb.AppendLine($"Steam: {info?.IsSteamVersion ?? false}");
                sb.AppendLine();
                sb.AppendLine("--- System Tweaks ---");
                sb.AppendLine($"Game Mode: {Sys.SystemOptimizer.IsGameModeEnabled()}");
                sb.AppendLine($"Game Bar Disabled: {Sys.SystemOptimizer.IsGameBarDisabled()}");
                sb.AppendLine($"Core Parking Disabled: {Sys.SystemOptimizer.IsCoreParkingDisabled()}");
                sb.AppendLine($"High Perf Plan: {Sys.SystemOptimizer.IsHighPerformancePlanActive()}");
                sb.AppendLine($"FSO Disabled: {info != null && Sys.SystemOptimizer.IsFullscreenOptDisabled(info.ExePath)}");
                sb.AppendLine($"HPET Disabled: {Sys.SystemOptimizer.IsHPETDisabled()}");
                sb.AppendLine($"Nagle Disabled: {Sys.SystemOptimizer.IsNagleDisabled()}");
                sb.AppendLine($"SysMain Disabled: {Sys.SystemOptimizer.IsSysMainDisabled()}");
                sb.AppendLine($"Visual Effects Disabled: {Sys.SystemOptimizer.IsVisualEffectsDisabled()}");
                sb.AppendLine();
                sb.AppendLine("--- Cheats State ---");
                var cheats = GetCheatsState();
                sb.AppendLine(cheats);
                sb.AppendLine();
                sb.AppendLine("--- RKN State ---");
                var rkn = GetRknState();
                sb.AppendLine(rkn);
                sb.AppendLine();
                sb.AppendLine("--- Network Diagnostics ---");
                sb.AppendLine(Network.RknBypass.GetNetworkDiagnosticInfo());
                sb.AppendLine();
                sb.AppendLine("--- User Comment ---");
                sb.AppendLine(string.IsNullOrWhiteSpace(userComment) ? "(none)" : userComment);

                var reportDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MatrixHole", "Reports");
                if (!Directory.Exists(reportDir)) Directory.CreateDirectory(reportDir);
                var reportPath = Path.Combine(reportDir, $"report_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
                File.WriteAllText(reportPath, sb.ToString());
                return JsonConvert.SerializeObject(new { ok = true, path = reportPath });
            }
            catch (Exception ex) { return JsonConvert.SerializeObject(new { ok = false, error = ex.Message }); }
        }

        // ============================================================
        // DISCORD INTEGRATION
        // ============================================================

        public string SendReportToDiscord(string webhookUrl, string reportPath)
        {
            try
            {
                var actualWebhook = !string.IsNullOrWhiteSpace(ReportWebhookUrl) ? ReportWebhookUrl : (!string.IsNullOrWhiteSpace(webhookUrl) ? webhookUrl : GetAppSetting("report_webhook"));
                if (string.IsNullOrWhiteSpace(actualWebhook))
                    return JsonConvert.SerializeObject(new { ok = false, error = "no webhook configured" });

                // Read user message from report file if exists
                string? userMessage = null;
                if (File.Exists(reportPath))
                    userMessage = File.ReadAllText(reportPath);

                // Build rich report
                var reportText = Core.DevLogger.GetSessionReport(userMessage);
                var reportFile = Path.Combine(Path.GetTempPath(), $"matrixhole_report_{Guid.NewGuid():N}.txt");
                File.WriteAllText(reportFile, reportText);

                // Send embed with summary + file attachment
                var ok = System.Threading.Tasks.Task.Run(async () =>
                {
                    var user = Environment.UserName;
                    var machine = Environment.MachineName;
                    var savedAuth = Core.DiscordAuth.GetSavedAuth();
                    var discordUser = savedAuth?.Username ?? "Not logged in";
                    var steamId = GetAppSetting("steam_id") ?? "Not linked";
                    var role = Core.DiscordAuth.GetPrimaryRole(savedAuth) ?? "N/A";

                    var fields = new (string, string, bool)[]
                    {
                        ("User", $"`{user}`", true),
                        ("Machine", $"`{machine}`", true),
                        ("Discord", $"`{discordUser}`", true),
                        ("Steam", $"`{steamId}`", true),
                        ("Role", $"`{role}`", true),
                        ("Time", $"`{DateTime.Now:yyyy-MM-dd HH:mm:ss}`", true)
                    };

                    await Core.DiscordService.SendEmbedAsync(actualWebhook,
                        "📋 User Report Received",
                        string.IsNullOrEmpty(userMessage) ? "No message provided." : $"**Message:**\n```{userMessage}```",
                        0x5865F2, fields);

                    return await Core.DiscordService.SendFileAsync(actualWebhook, reportFile,
                        $"📄 Full session log with errors and actions — {DateTime.Now:yyyy-MM-dd HH:mm}");
                }).GetAwaiter().GetResult();

                LogAction("REPORT_SENT", reportPath);
                return JsonConvert.SerializeObject(new { ok });
            }
            catch (Exception ex) { return JsonConvert.SerializeObject(new { ok = false, error = ex.Message }); }
        }

        public string TestDiscordWebhook(string webhookUrl)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(webhookUrl))
                    return JsonConvert.SerializeObject(new { ok = false, error = "empty url" });
                var ok = System.Threading.Tasks.Task.Run(async () =>
                    await Core.DiscordService.SendMessageAsync(webhookUrl, "MatrixHole-Engine webhook test ✅")
                ).GetAwaiter().GetResult();
                return JsonConvert.SerializeObject(new { ok });
            }
            catch (Exception ex) { return JsonConvert.SerializeObject(new { ok = false, error = ex.Message }); }
        }

        public string CheckForUpdates()
        {
            try
            {
                var info = System.Threading.Tasks.Task.Run(async () => await Core.UpdateChecker.CheckAsync()).GetAwaiter().GetResult();
                if (info == null)
                    return JsonConvert.SerializeObject(new { hasUpdate = false });
                return JsonConvert.SerializeObject(new
                {
                    hasUpdate = true,
                    version = info.Version,
                    downloadUrl = info.DownloadUrl,
                    releaseNotes = info.ReleaseNotes,
                    mandatory = info.Mandatory
                });
            }
            catch (Exception ex) { return JsonConvert.SerializeObject(new { hasUpdate = false, error = ex.Message }); }
        }

        public string DownloadAndInstallUpdate(string downloadUrl)
        {
            try
            {
                Core.UpdateChecker.DownloadAndInstall(downloadUrl);
                return JsonConvert.SerializeObject(new { ok = true });
            }
            catch (Exception ex) { return JsonConvert.SerializeObject(new { ok = false, error = ex.Message }); }
        }

        // ============================================================
        // RKN BYPASS — ADVANCED
        // ============================================================

        public string AnalyzeRkn()
        {
            return Network.RknBypass.AnalyzeAndRecommend();
        }

        public string GenerateZapretConfig()
        {
            var ok = Network.RknBypass.GenerateZapretConfig(out var configPath, out var listPath);
            return JsonConvert.SerializeObject(new { ok, config_path = configPath, list_path = listPath });
        }

        public string ApplyCustomHosts(string json)
        {
            try
            {
                var entries = JsonConvert.DeserializeObject<string[]>(json);
                var ok = Network.RknBypass.ApplyCustomHosts(entries ?? Array.Empty<string>());
                return JsonConvert.SerializeObject(new { ok });
            }
            catch (Exception ex) { return JsonConvert.SerializeObject(new { ok = false, error = ex.Message }); }
        }

        // ============================================================
        // STEAM AUTH
        // ============================================================
        private static System.Threading.Tasks.Task<(bool ok, string? steamId, string? error)>? _steamAuthTask;

        public string StartSteamAuth()
        {
            try
            {
                if (_steamAuthTask != null && !_steamAuthTask.IsCompleted)
                    return JsonConvert.SerializeObject(new { pending = true });

                _steamAuthTask = System.Threading.Tasks.Task.Run(async () => await Core.SteamAuth.AuthenticateAsync());
                return JsonConvert.SerializeObject(new { started = true });
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new { error = ex.Message });
            }
        }

        public string PollSteamAuthResult()
        {
            try
            {
                if (_steamAuthTask == null)
                    return JsonConvert.SerializeObject(new { pending = false, done = false });

                if (!_steamAuthTask.IsCompleted)
                    return JsonConvert.SerializeObject(new { pending = true });

                var result = _steamAuthTask.GetAwaiter().GetResult();
                if (result.ok)
                {
                    return JsonConvert.SerializeObject(new { ok = true, steam_id = result.steamId, done = true });
                }
                return JsonConvert.SerializeObject(new { ok = false, error = result.error, done = true });
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new { ok = false, error = ex.Message, done = true });
            }
        }

        public string GetSteamId()
        {
            return Core.SteamAuth.GetSavedSteamId() ?? "";
        }

        public string GetSteamPersonaName()
        {
            return Core.SteamAuth.GetSavedPersonaName() ?? "";
        }

        public bool IsSteamDeveloper()
        {
            return Core.SteamAuth.IsDeveloper();
        }

        public string GetDiscordRole()
        {
            var user = Core.DiscordAuth.GetSavedAuth();
            if (user == null) return "";
            return Core.DiscordAuth.GetPrimaryRole(user) ?? "";
        }

        public bool IsDiscordAdmin()
        {
            var user = Core.DiscordAuth.GetSavedAuth();
            if (user == null) return false;
            var role = Core.DiscordAuth.GetPrimaryRole(user);
            return role == "Administrator" || role == "Coder";
        }

        public bool IsDiscordCoder()
        {
            var user = Core.DiscordAuth.GetSavedAuth();
            if (user == null) return false;
            return Core.DiscordAuth.GetPrimaryRole(user) == "Coder";
        }

        public void SteamLogout()
        {
            var steamId = Core.SteamAuth.GetSavedSteamId();
            Core.DevLogger.Auth("Steam logout", steamId ?? "unknown", true);
            Core.SteamAuth.ClearAuth();
        }

        public string GetSteamAvatarUrl()
        {
            var url = Core.SteamAuth.GetSavedAvatarUrl();
            if (!string.IsNullOrEmpty(url)) return url;

            // Fallback: fetch synchronously if not cached yet
            var steamId = Core.SteamAuth.GetSavedSteamId();
            if (string.IsNullOrEmpty(steamId)) return "";

            return Core.SteamAuth.FetchSteamAvatarUrlSync(steamId) ?? "";
        }

        // ============================================================
        // SECURITY / LICENSING
        // ============================================================

        // ============================================================
        // NEW MODULES — Profile, Cleanup, Diagnostics, Conflicts
        // ============================================================

        public string GetProfiles() => JsonConvert.SerializeObject(Core.ProfileManager.GetAllProfiles(), _jsonSettings);
        public string GetActiveProfile() => JsonConvert.SerializeObject(Core.ProfileManager.GetActiveProfile(), _jsonSettings);
        public string SetActiveProfile(string id) { Core.ProfileManager.SetActiveProfile(id); return "{\"ok\":true}"; }
        public string SaveProfile(string json)
        {
            try { var p = JsonConvert.DeserializeObject<Core.ProfileManager.Profile>(json); if (p != null) Core.ProfileManager.SaveProfile(p); return "{\"ok\":true}"; }
            catch (Exception ex) { return JsonConvert.SerializeObject(new { ok = false, error = ex.Message }); }
        }
        public string GetCleanupTargets() => JsonConvert.SerializeObject(Core.GameCleanupManager.GetCleanupTargets(), _jsonSettings);
        public string CleanupTarget(string id) => JsonConvert.SerializeObject(Core.GameCleanupManager.CleanupById(id));
        public string CleanupAll() => JsonConvert.SerializeObject(Core.GameCleanupManager.CleanupAll());

        public string ExportDiagnostics() => JsonConvert.SerializeObject(new { path = Core.DiagnosticsExporter.ExportDiagnosticsZip() }, _jsonSettings);

        public string ScanConflicts() => Core.ConflictDetector.GetConflictsJson();

        // ============================================================
        // NEW MODULES — Ghost, Hotkeys, Launch Builder, Benchmark
        // ============================================================

        public string ToggleGhostMode() { Core.GhostMode.Toggle(); return Core.GhostMode.GetStatusJson(); }
        public string GetGhostStatus() => Core.GhostMode.GetStatusJson();

        public string GetLaunchFlags() => JsonConvert.SerializeObject(Core.LaunchParamBuilder.GetAllFlags(), _jsonSettings);
        public string BuildLaunchString(string json)
        {
            try { var flags = JsonConvert.DeserializeObject<System.Collections.Generic.Dictionary<string, string?>>(json); return Core.LaunchParamBuilder.BuildLaunchString(flags ?? new System.Collections.Generic.Dictionary<string, string?>()); }
            catch (Exception ex) { return JsonConvert.SerializeObject(new { ok = false, error = ex.Message }); }
        }

        public string RunBenchmark(int seconds = 60)
        {
            _ = System.Threading.Tasks.Task.Run(async () => await Core.GameBenchmark.RunBenchmarkAsync(seconds));
            return "{\"ok\":true,\"status\":\"started\"}";
        }
        public string StopBenchmark() { Core.GameBenchmark.StopBenchmark(); return "{\"ok\":true}"; }

        // ============================================================
        // NEW MODULES — Honest, AFK, SystemOnly, Logs, Focus
        // ============================================================

        public string ToggleHonestMode() { Core.HonestMode.Toggle(); return JsonConvert.SerializeObject(new { enabled = Core.HonestMode.IsEnabled }); }
        public string IsHonestMode() => JsonConvert.SerializeObject(new { enabled = Core.HonestMode.IsEnabled });

        public string ToggleAfkMode() { if (Core.AfkMode.IsEnabled) Core.AfkMode.Disable(); else Core.AfkMode.Enable(); return JsonConvert.SerializeObject(new { enabled = Core.AfkMode.IsEnabled }); }
        public string IsAfkMode() => JsonConvert.SerializeObject(new { enabled = Core.AfkMode.IsEnabled });

        public string ToggleSystemOnlyMode() { Core.SystemOnlyMode.Toggle(); return JsonConvert.SerializeObject(new { enabled = Core.SystemOnlyMode.IsEnabled }); }
        public string IsSystemOnlyMode() => JsonConvert.SerializeObject(new { enabled = Core.SystemOnlyMode.IsEnabled });


        public string ApplyGamingFocus() { Core.FocusAssistManager.ApplyGamingFocus(); return "{\"ok\":true}"; }
        public string RestoreNormalFocus() { Core.FocusAssistManager.RestoreNormalFocus(); return "{\"ok\":true}"; }

        // ============================================================
        // NEW MODULES — QR/Share codes, Updater, Disk, AutoSave, Sound
        // ============================================================

        public string ExportProfileToQR(string profileJson) { Core.QRCodeProfileManager.ExportProfileToQR(profileJson, out var err); return err ?? "ok"; }
        public string ImportProfileFromQR(string base64) => Core.QRCodeProfileManager.ImportProfileFromBase64(base64) ?? "";

        public string EncodeShareCode(string profileJson) => Core.ShareCodeManager.EncodeProfile(profileJson);
        public string DecodeShareCode(string code) => Core.ShareCodeManager.DecodeProfile(code) ?? "";

        public string RunDiskOptimizer() => Core.DiskOptimizer.AnalyzeGameDisk();
        public string RunDiskTrim(string driveLetter) => JsonConvert.SerializeObject(new { ok = Core.DiskOptimizer.RunTrim(driveLetter) });

        public void MarkAutoSave() => Core.AutoSaveManager.MarkUnsaved();
        public void ForceAutoSave() => Core.AutoSaveManager.ForceSaveNow();

        public void PlaySaveSound() => Core.SoundNotifications.PlaySave();
        public void PlayErrorSound() => Core.SoundNotifications.PlayError();
        public void PlaySuccessSound() => Core.SoundNotifications.PlaySuccess();

        // ============================================================
        // NEW MODULES — AutoReconnect, SteamPatch, CustomContent, Discord
        // ============================================================

        public string SaveLastServer(string ip) { Core.AutoReconnect.SaveLastServer(ip); return "{\"ok\":true}"; }
        public string ShouldOfferReconnect() => JsonConvert.SerializeObject(new { reconnect = Core.AutoReconnect.ShouldOfferReconnect(), last = Core.AutoReconnect.GetLastServer() });

        public string CheckSteamPatch() => JsonConvert.SerializeObject(new { available = Core.SteamPatchMonitor.IsNewPatchAvailableAsync().GetAwaiter().GetResult() });

        public string GetCustomAssets() => "[]";
        public string ApplyCustomAsset(string id) => "{\"ok\":false,\"error\":\"not implemented\"}";
        public string RestoreCustomAsset(string id) => "{\"ok\":false,\"error\":\"not implemented\"}";

        // ============================================================
        // NEW MODULES — SafeMode, Backup, GameplayConfig, Console, Perf
        // ============================================================

        public string EnterSafeMode() { Core.SafeModeLauncher.EnterSafeMode(); return "{\"ok\":true}"; }
        public string ExitSafeMode() { Core.SafeModeLauncher.ExitSafeMode(); return "{\"ok\":true}"; }
        public bool IsSafeMode() => Core.SafeModeLauncher.IsSafeModeActive;

        public string GetGameplayConfig() => JsonConvert.SerializeObject(Core.GameplayConfigEditor.ParseGameplayConfig());
        public string SetGameplayValue(string key, string value) => JsonConvert.SerializeObject(new { ok = Core.GameplayConfigEditor.SetGameplayValue(key, value) });

        public string GetConsoleCommands() => JsonConvert.SerializeObject(Core.ConsoleCommandSender.GetAvailableCommands(), _jsonSettings);
        public string SendConsoleCommand(string id, string args) => Core.ConsoleCommandSender.SendCommand(id, args);

        public string GetPerfHistory() => Core.PerformanceMonitor.GetHistoryJson();
        public string StartPerfMonitor() { Core.PerformanceMonitor.Start(); return "{\"ok\":true}"; }
        public string StopPerfMonitor() { Core.PerformanceMonitor.Stop(); return "{\"ok\":true}"; }

        // ============================================================
        // NEW MODULES — Dependencies, DiskSpace, Scheduler, Telemetry, Mirror
        // ============================================================

        public string CheckDependencies() => Core.DependencyChecker.GetStatusJson();
        public string CheckGameDiskSpace() => Core.DiskSpaceMonitor.CheckGameDisk();

        public string CreateStartupTask() => JsonConvert.SerializeObject(new { ok = Core.TaskSchedulerIntegration.CreateStartupTask() });
        public string RemoveStartupTask() => JsonConvert.SerializeObject(new { ok = Core.TaskSchedulerIntegration.RemoveStartupTask() });

        public string SetTelemetryConsent(bool consent) { Core.TelemetryService.SetConsent(consent); return "{\"ok\":true}"; }

        public string SyncConfigFromGame() => JsonConvert.SerializeObject(new { ok = Core.ConfigMirror.SyncFromGameToTweaker() });
        public string SyncConfigToGame() => JsonConvert.SerializeObject(new { ok = Core.ConfigMirror.SyncFromTweakerToGame() });

        // ============================================================
        // NEW MODULES — RTSS, CrashScreenshot, Restart, SteamFriends
        // ============================================================

        public string IsRTSSRunning() => JsonConvert.SerializeObject(new { running = Core.RTSSIntegration.IsRTSSRunning() });

        public string EnableCrashScreenshot() { _ = Core.CrashScreenshot.MonitorForCrashAsync(System.Threading.CancellationToken.None); return "{\"ok\":true}"; }

        public string SetUpdateReady() { Core.AutoRestartManager.SetUpdateReady(); return "{\"ok\":true}"; }
        public string WaitAndRestart() { _ = Core.AutoRestartManager.WaitAndRestartAsync(System.Threading.CancellationToken.None); return "{\"ok\":true}"; }

        public string GetSteamFriendsPlaying() => System.Threading.Tasks.Task.Run(async () => await Core.SteamFriendsIntegration.GetFriendsPlayingScpSlAsync(Core.SteamAuth.GetSavedSteamId() ?? "")).GetAwaiter().GetResult().ToString() ?? "[]";

        // ============================================================
        // NEW MODULES — PushNotifications, Background, Search, Magnifier, Map
        // ============================================================

        public string GetFavoriteServers() => JsonConvert.SerializeObject(Core.PushNotifications.GetFavorites(), _jsonSettings);
        public string AddFavoriteServer(string ip, string name, int maxPlayers) { Core.PushNotifications.AddFavorite(ip, name, maxPlayers); return "{\"ok\":true}"; }
        public string RemoveFavoriteServer(string ip) { Core.PushNotifications.RemoveFavorite(ip); return "{\"ok\":true}"; }
        public string StartPushMonitoring() { Core.PushNotifications.StartMonitoring(); return "{\"ok\":true}"; }

        public string GetBackgroundSettings() => JsonConvert.SerializeObject(Core.CustomBackgroundManager.GetSettings(), _jsonSettings);
        public string SetBackgroundImage(string path) { var s = Core.CustomBackgroundManager.GetSettings(); s.ImagePath = path; s.VideoPath = null; Core.CustomBackgroundManager.SaveSettings(s); return "{\"ok\":true}"; }
        public string SetBackgroundVideo(string path) { var s = Core.CustomBackgroundManager.GetSettings(); s.VideoPath = path; s.ImagePath = null; s.Animated = true; Core.CustomBackgroundManager.SaveSettings(s); return "{\"ok\":true}"; }

        public string GlobalSearch(string query) => JsonConvert.SerializeObject(Core.GlobalSearchProvider.Search(query), _jsonSettings);

        public string EnableMagnifier() { Core.ScreenMagnifier.Enable(); return "{\"ok\":true}"; }
        public string DisableMagnifier() { Core.ScreenMagnifier.Disable(); return "{\"ok\":true}"; }

        public string GetMapPoints() => JsonConvert.SerializeObject(Core.FacilityMap.GetDefaultPoints(), _jsonSettings);




        // ============================================================
        // PANIC RESET
        // ============================================================

        public string ExecutePanicReset() => Core.PanicReset.ExecuteFullReset();
        public string ExecuteSoftReset() => Core.PanicReset.ExecuteSoftReset();

        // ============================================================
        // TRAY
        // ============================================================

        // ============================================================
        // DISCORD INTEGRATION
        // ============================================================

        public string CheckDiscordAccess()
        {
            try
            {
                // Refresh roles from Discord if cache is older than 15 seconds
                Core.DiscordAuth.RefreshRolesIfStale(TimeSpan.FromSeconds(15));

                var user = Core.DiscordAuth.GetSavedAuth();
                if (user == null) return JsonConvert.SerializeObject(new { ok = false, reason = "not_logged_in" });
                var rolesStr = string.Join(",", user.GuildRoles);
                var hasRole = Core.DiscordAuth.HasRequiredRole(user);
                Core.DevLogger.Auth("Discord check", user.Username, hasRole, $"Roles: [{rolesStr}] HasRequired: {hasRole}");
                if (!hasRole)
                {
                    return JsonConvert.SerializeObject(new { ok = false, reason = "no_role", roles = user.GuildRoles });
                }
                return JsonConvert.SerializeObject(new { ok = true, username = user.Username, roles = user.GuildRoles });
            }
            catch (Exception ex) { return JsonConvert.SerializeObject(new { ok = false, reason = "error", error = ex.Message }); }
        }

        public string DiscordRpInitialize() => Core.DiscordRichPresence.Initialize();
        public string DiscordRpSetActivity(string state, string details) => Core.DiscordRichPresence.SetActivity(state, details);
        public string DiscordRpUpdateGame(string server, string role, int players, int max) => Core.DiscordRichPresence.UpdateGamePresence(server, role, players, max);
        public string DiscordRpClear() => Core.DiscordRichPresence.ClearPresence();
        public string DiscordRpGetState() => Core.DiscordRichPresence.GetCurrentPresenceJson();

        public string GetDiscordOAuthUrl() => Core.DiscordAuth.GetOAuthUrl();
        public string PollDiscordAuthResult(string code)
        {
            try
            {
                var task = Core.DiscordAuth.ExchangeCodeAsync(code);
                var (ok, user, error) = task.GetAwaiter().GetResult();
                if (!ok || user == null) return JsonConvert.SerializeObject(new { ok = false, error });
                var avatarUrl = !string.IsNullOrEmpty(user.Avatar) ? $"https://cdn.discordapp.com/avatars/{user.Id}/{user.Avatar}.png" : "";
                return JsonConvert.SerializeObject(new { ok = true, id = user.Id, username = user.Username, avatar = user.Avatar, avatar_url = avatarUrl, has_role = Core.DiscordAuth.HasRequiredRole(user), roles = user.GuildRoles, primary_role = Core.DiscordAuth.GetPrimaryRole(user) });
            }
            catch (Exception ex) { return JsonConvert.SerializeObject(new { ok = false, error = ex.Message }); }
        }
        public string GetSavedDiscordAuth()
        {
            try
            {
                var user = Core.DiscordAuth.GetSavedAuth();
                if (user == null) return JsonConvert.SerializeObject(new { ok = false });
                var avatarUrl = !string.IsNullOrEmpty(user.Avatar) ? $"https://cdn.discordapp.com/avatars/{user.Id}/{user.Avatar}.png" : "";
                return JsonConvert.SerializeObject(new { ok = true, id = user.Id, username = user.Username, avatar = user.Avatar, avatar_url = avatarUrl, has_role = Core.DiscordAuth.HasRequiredRole(user), roles = user.GuildRoles, primary_role = Core.DiscordAuth.GetPrimaryRole(user) });
            }
            catch (Exception ex) { return JsonConvert.SerializeObject(new { ok = false, error = ex.Message }); }
        }
        public string DiscordLogout()
        {
            var user = Core.DiscordAuth.GetSavedAuth();
            Core.DevLogger.Auth("Discord logout", user?.Username ?? "unknown", true);
            Core.DiscordAuth.ClearAuth();
            return "{\"ok\":true}";
        }

        public void OpenExternalUrl(string url)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch { }
        }

        public string StartDiscordAuthFlow()
        {
            try
            {
                // Start local callback server (returns the redirect URI used)
                var redirectUri = Core.DiscordAuth.StartCallbackServer((ok, msg) =>
                {
                    // Server handles everything; JS will poll CheckDiscordAccess()
                });

                // Warn if using a dynamic fallback port (Discord app must have this URI registered)
                bool usingFallbackPort = !redirectUri.Contains(":3000/");

                // Open browser with OAuth URL
                var url = Core.DiscordAuth.GetOAuthUrl();
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });

                if (usingFallbackPort)
                    return JsonConvert.SerializeObject(new { ok = true, warning = $"Port 3000 is busy. Using {redirectUri}. Make sure this URI is registered in your Discord app settings." });
                return "{\"ok\":true}";
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new { ok = false, error = ex.Message });
            }
        }

        // ============================================================
        // ADMIN / CODER NETWORK TOOLS
        // ============================================================

        public string GetNetworkInfo()
        {
            try
            {
                var sb = new System.Text.StringBuilder();
                foreach (var ni in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up) continue;
                    var ipProps = ni.GetIPProperties();
                    sb.AppendLine($"Adapter: {ni.Name}");
                    sb.AppendLine($"  MAC: {ni.GetPhysicalAddress()}");
                    sb.AppendLine($"  Type: {ni.NetworkInterfaceType}");
                    sb.AppendLine($"  Speed: {ni.Speed / 1_000_000} Mbps");
                    foreach (var ip in ipProps.UnicastAddresses)
                        sb.AppendLine($"  IP: {ip.Address}");
                    foreach (var gw in ipProps.GatewayAddresses)
                        sb.AppendLine($"  Gateway: {gw.Address}");
                    foreach (var dns in ipProps.DnsAddresses)
                        sb.AppendLine($"  DNS: {dns}");
                    sb.AppendLine();
                }
                return sb.ToString();
            }
            catch (Exception ex) { return "Error: " + ex.Message; }
        }

        public string PingMonitor()
        {
            try
            {
                var hosts = new[] { "1.1.1.1", "8.8.8.8", "discord.com", "steamcommunity.com" };
                var sb = new System.Text.StringBuilder();
                using var ping = new System.Net.NetworkInformation.Ping();
                foreach (var host in hosts)
                {
                    try
                    {
                        var reply = ping.Send(host, 2000);
                        sb.AppendLine($"{host}: {(reply.Status == System.Net.NetworkInformation.IPStatus.Success ? reply.RoundtripTime + "ms" : reply.Status.ToString())}");
                    }
                    catch (Exception ex) { sb.AppendLine($"{host}: Error ({ex.Message})"); }
                }
                return sb.ToString();
            }
            catch (Exception ex) { return "Error: " + ex.Message; }
        }

        public string FlushDns()
        {
            try
            {
                var proc = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("ipconfig", "/flushdns")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                proc?.WaitForExit();
                return JsonConvert.SerializeObject(new { ok = true });
            }
            catch (Exception ex) { return JsonConvert.SerializeObject(new { ok = false, error = ex.Message }); }
        }

        public string GetProcessConnections()
        {
            try
            {
                var proc = new System.Diagnostics.Process();
                proc.StartInfo.FileName = "powershell.exe";
                proc.StartInfo.Arguments = "-Command \"Get-NetTCPConnection | Select-Object LocalAddress,LocalPort,RemoteAddress,RemotePort,State,OwningProcess | Format-Table -AutoSize | Out-String -Width 200\"";
                proc.StartInfo.RedirectStandardOutput = true;
                proc.StartInfo.UseShellExecute = false;
                proc.StartInfo.CreateNoWindow = true;
                proc.Start();
                string output = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit();
                return string.IsNullOrWhiteSpace(output) ? "No active connections found." : output;
            }
            catch (Exception ex) { return "Error: " + ex.Message; }
        }

        public string ToggleFirewall()
        {
            try
            {
                // Check current state
                var check = new System.Diagnostics.Process();
                check.StartInfo.FileName = "netsh";
                check.StartInfo.Arguments = "advfirewall show allprofiles state";
                check.StartInfo.RedirectStandardOutput = true;
                check.StartInfo.UseShellExecute = false;
                check.StartInfo.CreateNoWindow = true;
                check.Start();
                string checkOutput = check.StandardOutput.ReadToEnd();
                check.WaitForExit();
                bool isOn = checkOutput.Contains("ON");
                string newState = isOn ? "off" : "on";

                var toggle = new System.Diagnostics.Process();
                toggle.StartInfo.FileName = "netsh";
                toggle.StartInfo.Arguments = $"advfirewall set allprofiles state {newState}";
                toggle.StartInfo.RedirectStandardOutput = true;
                toggle.StartInfo.UseShellExecute = false;
                toggle.StartInfo.CreateNoWindow = true;
                toggle.Start();
                toggle.WaitForExit();
                return JsonConvert.SerializeObject(new { ok = true, state = isOn ? "OFF" : "ON" });
            }
            catch (Exception ex) { return JsonConvert.SerializeObject(new { ok = false, error = ex.Message }); }
        }

        public string OptimizeNetworkAdapter()
        {
            try
            {
                var sb = new System.Text.StringBuilder();
                // Disable power management on all active adapters via PowerShell
                var proc = new System.Diagnostics.Process();
                proc.StartInfo.FileName = "powershell.exe";
                proc.StartInfo.Arguments = "-Command \"Get-NetAdapter | Where-Object {$_.Status -eq 'Up'} | Disable-NetAdapterPowerManagement -ErrorAction SilentlyContinue; Write-Output 'Done'\"";
                proc.StartInfo.RedirectStandardOutput = true;
                proc.StartInfo.RedirectStandardError = true;
                proc.StartInfo.UseShellExecute = false;
                proc.StartInfo.CreateNoWindow = true;
                proc.Start();
                string output = proc.StandardOutput.ReadToEnd();
                string err = proc.StandardError.ReadToEnd();
                proc.WaitForExit();
                sb.AppendLine("Power Management disabled for active adapters.");
                if (!string.IsNullOrWhiteSpace(err)) sb.AppendLine("Note: " + err.Trim());
                return JsonConvert.SerializeObject(new { ok = true, detail = sb.ToString() });
            }
            catch (Exception ex) { return JsonConvert.SerializeObject(new { ok = false, error = ex.Message }); }
        }

        public string GetPingStats()
        {
            try
            {
                using var ping = new System.Net.NetworkInformation.Ping();
                string PingHost(string host)
                {
                    try { var r = ping.Send(host, 1500); return r.Status == System.Net.NetworkInformation.IPStatus.Success ? $"{r.RoundtripTime} ms" : "timeout"; }
                    catch { return "error"; }
                }
                return JsonConvert.SerializeObject(new
                {
                    ok = true,
                    eu = PingHost("162.254.197.40"),
                    us = PingHost("208.78.164.9"),
                    ru = PingHost("185.25.182.21"),
                    steam = PingHost("cm1-lax1.cm.steampowered.com")
                });
            }
            catch (Exception ex) { return JsonConvert.SerializeObject(new { ok = false, error = ex.Message }); }
        }

        // ========== DLL Injector ==========

        public string InjectDll() => Core.Injector.Inject();
        public string IsDllInjected() => JsonConvert.SerializeObject(new { injected = Core.Injector.IsInjected() });
        public string ShowInjectorMenu() => Core.Injector.ShowMenu();
        public string HideInjectorMenu() => Core.Injector.HideMenu();
        public string ToggleInjectorPatch(string patchName) => Core.Injector.TogglePatch(patchName);
        public string ApplyAllInjectorPatches() => Core.Injector.ApplyAllPatches();
        public string RestoreAllInjectorPatches() => Core.Injector.RestoreAllPatches();

        public string SendInjectorConsoleCommand(string cmd)
        {
            return Core.Injector.SendCommand($"{{\"cmd\":\"console\",\"line\":\"{cmd}\"}}");
        }

        // ========== Licensing ==========
        public string IsLicensed() => Core.LicenseManager.IsLicensed() ? "{\"licensed\":true}" : "{\"licensed\":false}";
        public string GetLicenseStatus() => Core.LicenseManager.GetLicenseStatusJson();
        public string ActivateLicense(string key) => Core.LicenseManager.ActivateLicense(key);
        public string GetHwid() => "{\"hwid\":\"" + Core.LicenseManager.GetHwid() + "\"}";

        // ========== Updater ==========
        public async Task<string> CheckForUpdate()
        {
            var currentVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";
            var latest = await Core.UpdaterLauncher.CheckForUpdateAsync(currentVersion);
            return latest != null
                ? "{\"hasUpdate\":true,\"version\":\"" + latest + "\",\"current\":\"" + currentVersion + "\"}"
                : "{\"hasUpdate\":false,\"current\":\"" + currentVersion + "\"}";
        }

        public async Task<string> DownloadUpdate(string version)
        {
            var ok = await Core.UpdaterLauncher.DownloadUpdateAsync(version);
            return ok ? "{\"ok\":true}" : "{\"ok\":false,\"error\":\"Download failed\"}";
        }

        public string IsUpdatePending() => JsonConvert.SerializeObject(new { pending = Core.UpdaterLauncher.IsUpdatePending() });
        public string ApplyUpdate() => Core.UpdaterLauncher.LaunchUpdaterAndExit() ? "{\"ok\":true}" : "{\"ok\":false,\"error\":\"Failed to launch updater\"}";
    }
}
