using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;

namespace MatrixHole.Network
{
    public static class RknBypass
    {
        private static readonly string HostsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "drivers", "etc", "hosts");
        private static readonly string BackupMarker = "# SCP-SL-BYPASS";
        private static readonly string CustomMarker = "# SCP-SL-BYPASS-CUSTOM";

        // Expanded Cloudflare-based hosts for SCP:SL + related CDN domains
        private static readonly string[] HostsEntries = new[]
        {
            "104.16.248.249 cdn.scpslgame.com",
            "104.16.249.249 api.scpslgame.com",
            "104.16.86.23 steamcdn-a.akamaihd.net",
            "104.16.249.249 sbg1.scpslgame.com",
            "104.16.248.249 scpslgame.com",
            "104.16.249.249 slac.scpslgame.com",
            "104.16.248.249 www.scpslgame.com",
            "13.32.121.77 cloudfront.com",
            "104.16.132.229 cloudflare-ech.com",
            "104.16.133.229 cloudflare-esni.com",
            "104.16.132.229 cloudflare-gateway.com",
            "104.16.133.229 cloudflare-quic.com",
            "104.16.249.249 cloudflare.com",
            "104.16.248.249 cloudflare-dns.com",
            "104.16.132.229 www.cloudflare.com",
        };

        // Cloudflare IP ranges for zapret list-general
        private static readonly string[] CloudflareRanges = new[]
        {
            "103.21.244.0/22", "103.22.200.0/22", "103.31.4.0/22",
            "104.16.0.0/13", "104.24.0.0/14", "108.162.192.0/18",
            "131.0.72.0/22", "141.101.64.0/18", "162.158.0.0/15",
            "172.64.0.0/13", "173.245.48.0/20", "188.114.96.0/20",
            "190.93.240.0/20", "197.234.240.0/22", "198.41.128.0/17"
        };

        private static readonly string[] ZapretDomains = new[]
        {
            "sbg1.scpslgame.com",
            "scpslgame.com",
            "slac.scpslgame.com",
            "cloudfront.com",
            "cloudflare-ech.com",
            "cloudflare.com",
            "cloudflare-dns.com",
            "cloudflare-esni.com",
            "cloudflare-gateway.com",
            "cloudflare-quic.com"
        };

        public static bool ApplyHosts()
        {
            try
            {
                var lines = File.ReadAllLines(HostsPath).ToList();
                if (lines.Any(l => l.Contains(BackupMarker))) return true; // already applied

                lines.Add("");
                lines.Add(BackupMarker);
                foreach (var entry in HostsEntries)
                    lines.Add(entry);
                lines.Add(BackupMarker);
                File.WriteAllLines(HostsPath, lines);
                FlushDns();
                return true;
            }
            catch { return false; }
        }

        public static bool ApplyCustomHosts(string[] customEntries)
        {
            try
            {
                ResetCustomHosts();
                if (customEntries == null || customEntries.Length == 0) return true;
                var lines = File.ReadAllLines(HostsPath).ToList();
                lines.Add("");
                lines.Add(CustomMarker);
                foreach (var entry in customEntries)
                    if (!string.IsNullOrWhiteSpace(entry))
                        lines.Add(entry.Trim());
                lines.Add(CustomMarker);
                File.WriteAllLines(HostsPath, lines);
                FlushDns();
                return true;
            }
            catch { return false; }
        }

        public static void ResetHosts()
        {
            try
            {
                if (!File.Exists(HostsPath)) return;
                var lines = File.ReadAllLines(HostsPath).ToList();
                bool inside = false;
                var cleaned = new System.Collections.Generic.List<string>();
                foreach (var line in lines)
                {
                    if (line.Trim() == BackupMarker)
                    {
                        inside = !inside;
                        continue;
                    }
                    if (!inside) cleaned.Add(line);
                }
                File.WriteAllLines(HostsPath, cleaned);
                FlushDns();
            }
            catch { }
        }

        public static void ResetCustomHosts()
        {
            try
            {
                if (!File.Exists(HostsPath)) return;
                var lines = File.ReadAllLines(HostsPath).ToList();
                bool inside = false;
                var cleaned = new System.Collections.Generic.List<string>();
                foreach (var line in lines)
                {
                    if (line.Trim() == CustomMarker)
                    {
                        inside = !inside;
                        continue;
                    }
                    if (!inside) cleaned.Add(line);
                }
                File.WriteAllLines(HostsPath, cleaned);
                FlushDns();
            }
            catch { }
        }

        public static bool ApplyCloudflareDns()
        {
            try
            {
                var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(ni => ni.OperationalStatus == OperationalStatus.Up &&
                        (ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet || ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211));
                foreach (var ni in interfaces)
                {
                    var name = ni.Name;
                    var psi = new ProcessStartInfo("netsh", $"interface ip set dns \"{name}\" static 1.1.1.1") { CreateNoWindow = true, UseShellExecute = false };
                    Process.Start(psi)?.WaitForExit();
                    psi = new ProcessStartInfo("netsh", $"interface ip add dns \"{name}\" 1.0.0.1 index=2") { CreateNoWindow = true, UseShellExecute = false };
                    Process.Start(psi)?.WaitForExit();
                }
                FlushDns();
                return true;
            }
            catch { return false; }
        }

        public static void ResetDns()
        {
            try
            {
                var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(ni => ni.OperationalStatus == OperationalStatus.Up &&
                        (ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet || ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211));
                foreach (var ni in interfaces)
                {
                    var psi = new ProcessStartInfo("netsh", $"interface ip set dns \"{ni.Name}\" dhcp") { CreateNoWindow = true, UseShellExecute = false };
                    Process.Start(psi)?.WaitForExit();
                }
                FlushDns();
            }
            catch { }
        }

        public static bool ApplyWinHttpProxy()
        {
            try
            {
                var psi = new ProcessStartInfo("netsh", "winhttp set proxy proxy-server=\"socks=127.0.0.1:9050\" bypass-list=\"localhost;127.*;10.*;192.168.*\"") { CreateNoWindow = true, UseShellExecute = false };
                Process.Start(psi)?.WaitForExit();
                return true;
            }
            catch { return false; }
        }

        public static void ResetProxy()
        {
            try
            {
                var psi = new ProcessStartInfo("netsh", "winhttp reset proxy") { CreateNoWindow = true, UseShellExecute = false };
                Process.Start(psi)?.WaitForExit();
            }
            catch { }
        }

        public static bool IsAnyBypassActive()
        {
            return IsHostsModified() || IsCloudflareDnsActive() || IsWinHttpProxySet();
        }

        public static bool AutoResetIfNoInternet()
        {
            try
            {
                // If no bypass is active, there is nothing to reset — stay silent.
                if (!IsAnyBypassActive()) return false;

                using var ping = new Ping();
                var reply = ping.Send("1.1.1.1", 3000);
                if (reply?.Status == IPStatus.Success) return false; // internet OK

                // Double-check with Google DNS to avoid false positives on transient packet loss.
                reply = ping.Send("8.8.8.8", 3000);
                if (reply?.Status == IPStatus.Success) return false;

                ResetHosts();
                ResetCustomHosts();
                ResetDns();
                ResetProxy();
                return true;
            }
            catch
            {
                // On exception: only reset if we actually have an active bypass to restore.
                if (IsAnyBypassActive())
                {
                    ResetHosts();
                    ResetCustomHosts();
                    ResetDns();
                    ResetProxy();
                    return true;
                }
                return false;
            }
        }

        public static bool IsZapretRunning()
        {
            try
            {
                return Process.GetProcessesByName("zapret").Length > 0 ||
                       Process.GetProcessesByName("winws").Length > 0 ||
                       Process.GetProcessesByName("tpws").Length > 0 ||
                       Process.GetProcessesByName("winws2").Length > 0;
            }
            catch { return false; }
        }

        public static string? FindZapretInstallation()
        {
            try
            {
                var candidates = new[]
                {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "zapret"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "zapret"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "zapret"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "zapret"),
                    Path.Combine("C:\\", "zapret"),
                };
                foreach (var dir in candidates)
                {
                    if (Directory.Exists(dir) && (File.Exists(Path.Combine(dir, "winws.exe")) || File.Exists(Path.Combine(dir, "zapret.exe"))))
                        return dir;
                }
                return null;
            }
            catch { return null; }
        }

        public static bool GenerateZapretConfig(out string configPath, out string listPath)
        {
            configPath = "";
            listPath = "";
            try
            {
                var zapretDir = FindZapretInstallation();
                if (string.IsNullOrEmpty(zapretDir)) return false;

                listPath = Path.Combine(zapretDir, "list-general.txt");
                var lines = new List<string>();
                lines.AddRange(ZapretDomains);
                lines.AddRange(CloudflareRanges);
                File.WriteAllLines(listPath, lines);

                configPath = Path.Combine(zapretDir, "config.txt");
                File.WriteAllLines(configPath, new[]
                {
                    "# MatrixHole-Engine — Zapret config",
                    $"# Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                    "",
                    "--filter-tcp=80",
                    "--dpi-desync=fake,fakedsplit",
                    "--dpi-desync-ttl=8",
                    "--dpi-desync-split-pos=2",
                    "--hostlist=list-general.txt",
                    "",
                    "--filter-tcp=443",
                    "--dpi-desync=fake,multisplit",
                    "--dpi-desync-ttl=8",
                    "--dpi-desync-split-pos=1,midsld",
                    "--hostlist=list-general.txt",
                    "",
                    "--filter-udp=443",
                    "--dpi-desync=fake",
                    "--dpi-desync-repeats=6",
                    "--hostlist=list-general.txt",
                    "",
                    "--new"
                });

                return true;
            }
            catch { return false; }
        }

        public static string AnalyzeAndRecommend()
        {
            try
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("=== RKN Bypass Analysis ===");

                bool zapretRunning = IsZapretRunning();
                string? zapretPath = FindZapretInstallation();
                bool hostsOk = IsHostsModified();
                bool dnsOk = IsCloudflareDnsActive();
                bool proxyOk = IsWinHttpProxySet();

                long pingCf = -1, pingScp = -1;
                try
                {
                    using var ping = new Ping();
                    var cf = ping.Send("1.1.1.1", 2000);
                    if (cf?.Status == IPStatus.Success) pingCf = cf.RoundtripTime;
                    var scp = ping.Send("scpslgame.com", 3000);
                    if (scp?.Status == IPStatus.Success) pingScp = scp.RoundtripTime;
                }
                catch { }

                sb.AppendLine($"Cloudflare ping: {(pingCf >= 0 ? pingCf + "ms" : "unreachable")}");
                sb.AppendLine($"SCP:SL domain ping: {(pingScp >= 0 ? pingScp + "ms" : "unreachable")}");
                sb.AppendLine($"Zapret running: {zapretRunning}");
                sb.AppendLine($"Zapret found: {(!string.IsNullOrEmpty(zapretPath))}");
                sb.AppendLine($"Hosts patched: {hostsOk}");
                sb.AppendLine($"Cloudflare DNS: {dnsOk}");
                sb.AppendLine($"WinHTTP proxy: {proxyOk}");
                sb.AppendLine();

                if (pingScp >= 0)
                {
                    sb.AppendLine("✅ SCP:SL domain is reachable. No bypass needed or current bypass works.");
                }
                else if (zapretRunning)
                {
                    sb.AppendLine("⚠️ Zapret is running but SCP:SL is unreachable. Try restarting zapret with updated config.");
                }
                else if (!string.IsNullOrEmpty(zapretPath))
                {
                    sb.AppendLine("💡 Zapret installed but not running. Start zapret for best bypass results.");
                }
                else if (pingCf >= 0)
                {
                    sb.AppendLine("💡 Cloudflare is reachable. Apply Hosts + DNS bypass for quick fix.");
                }
                else
                {
                    sb.AppendLine("🚨 Internet seems heavily restricted. Try all methods: Hosts + DNS + Proxy.");
                }

                return sb.ToString();
            }
            catch (Exception ex) { return "Analysis error: " + ex.Message; }
        }

        public static string GetNetworkDiagnosticInfo()
        {
            try
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("=== Network Diagnostics ===");
                using (var ping = new Ping())
                {
                    var google = ping.Send("8.8.8.8", 2000);
                    sb.AppendLine($"Google DNS: {google?.Status} ({google?.RoundtripTime}ms)");
                    var cf = ping.Send("1.1.1.1", 2000);
                    sb.AppendLine($"Cloudflare DNS: {cf?.Status} ({cf?.RoundtripTime}ms)");
                    var scp = ping.Send("scpslgame.com", 3000);
                    sb.AppendLine($"SCP:SL domain: {scp?.Status} ({scp?.RoundtripTime}ms)");
                }
                sb.AppendLine($"Zapret running: {IsZapretRunning()}");
                sb.AppendLine($"Zapret found: {(!string.IsNullOrEmpty(FindZapretInstallation()))}");
                sb.AppendLine($"Hosts modified: {IsHostsModified()}");
                sb.AppendLine($"Cloudflare DNS active: {IsCloudflareDnsActive()}");
                sb.AppendLine($"WinHTTP proxy: {IsWinHttpProxySet()}");
                return sb.ToString();
            }
            catch (Exception ex) { return "Diagnostic error: " + ex.Message; }
        }

        private static void FlushDns()
        {
            try
            {
                Process.Start(new ProcessStartInfo("ipconfig", "/flushdns") { CreateNoWindow = true, UseShellExecute = false })?.WaitForExit();
            }
            catch { }
        }

        public static bool IsHostsModified()
        {
            try { return File.Exists(HostsPath) && File.ReadAllText(HostsPath).Contains(BackupMarker); }
            catch { return false; }
        }

        public static bool IsCloudflareDnsActive()
        {
            try
            {
                var psi = new ProcessStartInfo("netsh", "interface ip show dns") { CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true };
                var proc = Process.Start(psi);
                proc?.WaitForExit();
                var output = proc?.StandardOutput.ReadToEnd() ?? "";
                return output.Contains("1.1.1.1");
            }
            catch { return false; }
        }

        public static bool IsWinHttpProxySet()
        {
            try
            {
                var psi = new ProcessStartInfo("netsh", "winhttp show proxy") { CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true };
                var proc = Process.Start(psi);
                proc?.WaitForExit();
                var output = proc?.StandardOutput.ReadToEnd() ?? "";
                return output.Contains("127.0.0.1:9050");
            }
            catch { return false; }
        }
    }
}
