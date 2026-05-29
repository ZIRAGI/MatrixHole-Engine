using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Threading;

namespace MatrixHole.Core
{
    public static class SecurityManager
    {
        private static string AppDir => Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
        private static string EulaDir => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MatrixHole");
        private static string LicensePath => Path.Combine(EulaDir, "license.dat");
        private static string LastRunPath => Path.Combine(EulaDir, "last_run");

        // ============================================================
        // LAYER 1: ANTI-DEBUG
        // ============================================================
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool IsDebuggerPresent();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CheckRemoteDebuggerPresent(IntPtr hProcess, ref bool isDebuggerPresent);

        public static bool IsDebuggerDetected()
        {
            if (Debugger.IsAttached) return true;
            try { if (IsDebuggerPresent()) return true; } catch { }
            try
            {
                bool remoteDebug = false;
                CheckRemoteDebuggerPresent(Process.GetCurrentProcess().Handle, ref remoteDebug);
                if (remoteDebug) return true;
            }
            catch { }
            return false;
        }

        // ============================================================
        // LAYER 2: ANTI-VM / SANDBOX
        // ============================================================
        public static bool IsVmDetected()
        {
            try
            {
                // VM processes
                var vmProcs = new[] { "vmtoolsd", "vmwaretray", "vmwareuser", "VBoxService", "VBoxTray", "qemu-ga", "df5serv", "vboxservice" };
                if (vmProcs.Any(p => Process.GetProcessesByName(p).Length > 0)) return true;

                // WMI checks
                try
                {
                    using var mc = new System.Management.ManagementClass("Win32_ComputerSystem");
                    foreach (var mo in mc.GetInstances())
                    {
                        var man = mo["Manufacturer"]?.ToString()?.ToLower() ?? "";
                        var model = mo["Model"]?.ToString()?.ToLower() ?? "";
                        if (man.Contains("vmware") || man.Contains("virtualbox") || man.Contains("xen") ||
                            man.Contains("innotek") || man.Contains("qemu") ||
                            (man.Contains("microsoft corporation") && model.Contains("virtual")) ||
                            model.Contains("vmware") || model.Contains("virtual"))
                            return true;
                    }
                }
                catch { }

                // BIOS serial check
                try
                {
                    using var mc = new System.Management.ManagementClass("Win32_BIOS");
                    foreach (var mo in mc.GetInstances())
                    {
                        var serial = mo["SerialNumber"]?.ToString()?.ToLower() ?? "";
                        if (serial.Contains("vmware") || serial.Contains("virtual") || serial.Contains("innotek") || serial == "0")
                            return true;
                    }
                }
                catch { }

                return false;
            }
            catch { return false; }
        }

        // ============================================================
        // LAYER 3: INTEGRITY (self-hash check)
        // ============================================================
        public static bool VerifyIntegrity()
        {
            try
            {
                var exePath = Path.Combine(AppDir, "MatrixHole.exe");
                var dllPath = Path.Combine(AppDir, "MatrixHole.dll");
                if (!File.Exists(exePath) || !File.Exists(dllPath)) return false;

                // Size sanity check
                var exeInfo = new FileInfo(exePath);
                var dllInfo = new FileInfo(dllPath);
                if (exeInfo.Length < 50_000 || dllInfo.Length < 50_000) return false;

                // Self hash: ensure the running DLL hasn't been tampered with post-build
                var currentHash = ComputeFileHash(dllPath);
                var storedHashPath = Path.Combine(AppDir, "MatrixHole.dll.hash");
                if (File.Exists(storedHashPath))
                {
                    var expected = File.ReadAllText(storedHashPath).Trim();
                    if (!string.Equals(expected, currentHash, StringComparison.OrdinalIgnoreCase))
                    {
#if DEBUG || BETA
                        // Auto-update hash in dev builds for convenience
                        try { File.WriteAllText(storedHashPath, currentHash); } catch { }
#else
                        return false;
#endif
                    }
                }
                else
                {
                    // First run after build: store the hash
                    try { File.WriteAllText(storedHashPath, currentHash); } catch { }
                }

                return true;
            }
            catch { return false; }
        }

        private static string ComputeFileHash(string path)
        {
            using var sha = SHA256.Create();
            using var stream = File.OpenRead(path);
            return Convert.ToHexString(sha.ComputeHash(stream));
        }

        // ============================================================
        // LAYER 4: ANTI-TAMPER (time rollback detection)
        // ============================================================
        public static bool IsTimeTampered()
        {
            try
            {
                if (!File.Exists(LastRunPath)) return false;
                var lastRunText = File.ReadAllText(LastRunPath);
                if (!DateTime.TryParse(lastRunText, out var lastRun)) return false;
                if (DateTime.Now < lastRun.AddMinutes(-5)) return true; // clock turned back more than 5 min
                return false;
            }
            catch { return false; }
        }

        public static void UpdateLastRun()
        {
            try
            {
                if (!Directory.Exists(EulaDir)) Directory.CreateDirectory(EulaDir);
                File.WriteAllText(LastRunPath, DateTime.Now.ToString("O"));
            }
            catch { }
        }

        // ============================================================
        // LAYER 5: HWID
        // ============================================================
        public static string GetHwid()
        {
            try
            {
                var cpuId = GetCpuId();
                var mac = GetMacAddress();
                var disk = GetDiskSerial();
                var combined = $"{cpuId}-{mac}-{disk}";
                var bytes = Encoding.UTF8.GetBytes(combined);
                var hash = SHA256.HashData(bytes);
                return Convert.ToHexString(hash).Substring(0, 16);
            }
            catch { return "UNKNOWN"; }
        }

        private static string GetCpuId()
        {
            try
            {
                using var mc = new System.Management.ManagementClass("win32_processor");
                foreach (var mo in mc.GetInstances())
                {
                    var id = mo["processorId"]?.ToString() ?? "cpu";
                    mo.Dispose();
                    return id;
                }
                return "cpu";
            }
            catch { return "cpu"; }
        }

        private static string GetMacAddress()
        {
            try
            {
                var nics = NetworkInterface.GetAllNetworkInterfaces()
                    .FirstOrDefault(n => n.OperationalStatus == OperationalStatus.Up &&
                                         n.NetworkInterfaceType != NetworkInterfaceType.Loopback);
                return nics?.GetPhysicalAddress().ToString() ?? "mac";
            }
            catch { return "mac"; }
        }

        private static string GetDiskSerial()
        {
            try
            {
                using var mc = new System.Management.ManagementClass("Win32_DiskDrive");
                foreach (var mo in mc.GetInstances())
                {
                    var serial = mo["SerialNumber"]?.ToString()?.Trim() ?? "disk";
                    mo.Dispose();
                    return serial;
                }
                return "disk";
            }
            catch { return "disk"; }
        }

        // ============================================================
        // LAYER 6: LOCAL LICENSE (HWID-bound + signed + encrypted)
        // ============================================================
        public static bool IsLicensed()
        {
            try
            {
                if (!File.Exists(LicensePath)) return true; // free mode until server is ready
                var encrypted = File.ReadAllText(LicensePath);
                var decrypted = Unprotect(encrypted);
                var parts = decrypted.Split('|');
                if (parts.Length != 3) return false;

                var hwid = parts[0];
                var expires = DateTime.Parse(parts[1]);
                var sig = parts[2];

                var expectedSig = ComputeHmac($"{hwid}|{expires:O}");
                if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(sig), Encoding.UTF8.GetBytes(expectedSig))) return false;
                if (hwid != GetHwid()) return false;
                if (DateTime.UtcNow > expires.ToUniversalTime()) return false;
                return true;
            }
            catch { return false; }
        }

        public static void SaveLicense(DateTime expiration)
        {
            try
            {
                var hwid = GetHwid();
                var payload = $"{hwid}|{expiration:O}";
                var sig = ComputeHmac(payload);
                var full = $"{payload}|{sig}";
                var encrypted = Protect(full);
                if (!Directory.Exists(EulaDir)) Directory.CreateDirectory(EulaDir);
                File.WriteAllText(LicensePath, encrypted);
            }
            catch { }
        }

        private static string ComputeHmac(string data)
        {
            var keyText = AppSecrets.HmacKey;
            if (string.IsNullOrWhiteSpace(keyText)) keyText = "MatrixHoleLocalKey_v1";
            var key = Encoding.UTF8.GetBytes(keyText);
            var hash = HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(data));
            return Convert.ToHexString(hash).Substring(0, 32);
        }

        private static string Protect(string plain)
        {
            var bytes = Encoding.UTF8.GetBytes(plain);
            var protectedBytes = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(protectedBytes);
        }

        private static string Unprotect(string b64)
        {
            var bytes = Convert.FromBase64String(b64);
            var plain = ProtectedData.Unprotect(bytes, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plain);
        }

        // ============================================================
        // LAYER 7: PERIODIC SECURITY SWEEP
        // ============================================================
        public static bool RunPeriodicCheck()
        {
            if (IsDebuggerDetected()) return false;
            if (IsVmDetected()) return false;
            if (IsTimeTampered()) return false;
            if (!IsLicensed()) return false;
            UpdateLastRun();
            return true;
        }

        // ============================================================
        // LAYER 8: PASSIVE ANTI-TAMPER (Fail-Safe)
        // If cracker bypassed all active checks, this layer silently
        // validates method integrity and self-destructs on detection.
        // Developer SteamID (76561198184930081) is whitelisted — never triggers.
        // ============================================================
        public static class PassiveAntiTamper
        {
            private static readonly Dictionary<string, byte[]> _ilBaselines = new();
            private static int _canary;
            private static readonly Random _rng = new();
            private static bool _armed;
            private static bool _isDev;

            public static void Arm()
            {
                if (_armed) return;
                _armed = true;

                // Whitelist developer account — do not arm self-destruct on dev machine
                _isDev = SteamAccountResolver.IsDeveloper();
                if (_isDev) return;

                CaptureBaseline(nameof(IsDebuggerDetected));
                CaptureBaseline(nameof(IsVmDetected));
                CaptureBaseline(nameof(VerifyIntegrity));
                CaptureBaseline(nameof(IsLicensed));

                _canary = _rng.Next(100000, int.MaxValue);
                StartHiddenTimer();
            }

            private static void CaptureBaseline(string methodName)
            {
                try
                {
                    var method = typeof(SecurityManager).GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
                    var body = method?.GetMethodBody();
                    var il = body?.GetILAsByteArray();
                    if (il == null || il.Length == 0) return;
                    _ilBaselines[methodName] = SHA256.HashData(il);
                }
                catch { }
            }

            public static bool Validate()
            {
                if (_isDev) return true;
                if (_canary == 0) return false;

                // Double-check: if Debugger.IsAttached directly but wrapper says no,
                // the wrapper was patched in-memory.
                try
                {
                    if (Debugger.IsAttached && !IsDebuggerDetected()) return false;
                }
                catch { }

                // IL integrity: ensure critical methods weren't rewritten (e.g. patched to ret false)
                try
                {
                    foreach (var kv in _ilBaselines)
                    {
                        var method = typeof(SecurityManager).GetMethod(kv.Key, BindingFlags.Public | BindingFlags.Static);
                        var body = method?.GetMethodBody();
                        var il = body?.GetILAsByteArray();
                        if (il == null) return false;
                        var current = SHA256.HashData(il);
                        if (!current.SequenceEqual(kv.Value)) return false;
                    }
                }
                catch { return false; }

                return true;
            }

            public static void TriggerFailSafe(string reason)
            {
                if (_isDev) return;

                try
                {
                    // Gather Steam account info before corruption
                    var (steamId, accountName, personaName) = SteamAccountResolver.GetMostRecentAccount();
                    var steamDisplay = !string.IsNullOrEmpty(personaName) ? personaName : accountName ?? "Unknown";

                    // Corrupt license so cracked copy becomes useless
                    if (File.Exists(LicensePath))
                        File.WriteAllText(LicensePath, "TAMPERED_" + Guid.NewGuid());

                    // Reset settings (webhook, preferences)
                    var settingsPath = Path.Combine(EulaDir, "app_settings.json");
                    if (File.Exists(settingsPath))
                        File.WriteAllText(settingsPath, "{}");

                    // Delete all config backups
                    var backupDir = Path.Combine(EulaDir, "Backups");
                    if (Directory.Exists(backupDir))
                    {
                        foreach (var f in Directory.GetFiles(backupDir))
                        {
                            try { File.Delete(f); } catch { }
                        }
                    }

                    // Log and notify
                    DevLogger.Log("SECURITY", "Tamper Detected", $"Reason: {reason}",
                        new Dictionary<string, string> { ["Steam"] = steamDisplay, ["HWID"] = GetHwid(), ["Reason"] = reason });

                    try
                    {
                        var webhookUrl = GetWebhookUrlFromSettings();
                        if (!string.IsNullOrEmpty(webhookUrl))
                        {
                            var fields = new (string, string, bool)[]
                            {
                                ("Steam", $"`{steamDisplay}` (`{steamId ?? "N/A"}`)", false),
                                ("HWID", $"`{GetHwid()}`", true),
                                ("Reason", $"`{reason}`", true),
                                ("Time", $"`{DateTime.Now:yyyy-MM-dd HH:mm:ss}`", true)
                            };
                            DiscordService.SendEmbedAsync(
                                webhookUrl,
                                "🚨 Security Breach Detected",
                                "Anti-tamper triggered. Application self-destructed.",
                                0xFF0000,
                                fields,
                                "MatrixHole-Engine"
                            ).GetAwaiter().GetResult();
                        }
                    }
                    catch { }
                }
                catch { }

                // Hard crash that looks like a native/runtime bug, not protection.
                Environment.FailFast("Fatal error in CLR runtime. Error code: 0xC0000005");
            }

            private static void StartHiddenTimer()
            {
                if (_isDev) return;
                var timer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(_rng.Next(3, 8)) };
                timer.Tick += (s, e) =>
                {
                    if (!Validate())
                    {
                        TriggerFailSafe("hidden_timer_validation_failed");
                        timer.Stop();
                        return;
                    }
                    // Randomize interval to avoid predictable pattern
                    timer.Interval = TimeSpan.FromMinutes(_rng.Next(3, 10));
                };
                timer.Start();
            }

            private static string? GetWebhookUrlFromSettings()
            {
                var url = AppSecrets.DiscordWebhook;
                return string.IsNullOrWhiteSpace(url) ? null : url;
            }
        }
    }
}
