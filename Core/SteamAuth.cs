using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace MatrixHole.Core
{
    public static class SteamAuth
    {
        private static readonly string SettingsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MatrixHole");

        public static string? GetSavedSteamId()
        {
            try
            {
                var path = Path.Combine(SettingsDir, "steam_auth.json");
                if (!File.Exists(path)) return null;
                var json = File.ReadAllText(path);
                var match = Regex.Match(json, "\"steam_id\"\\s*:\\s*\"(\\d+)\"");
                return match.Success ? match.Groups[1].Value : null;
            }
            catch { return null; }
        }

        public static string? GetSavedPersonaName()
        {
            try
            {
                var path = Path.Combine(SettingsDir, "steam_auth.json");
                if (!File.Exists(path)) return null;
                var json = File.ReadAllText(path);
                var match = Regex.Match(json, "\"persona_name\"\\s*:\\s*\"([^\"]+)\"");
                return match.Success ? match.Groups[1].Value : null;
            }
            catch { return null; }
        }

        public static void SaveAuth(string steamId, string? personaName = null)
        {
            try
            {
                if (!Directory.Exists(SettingsDir)) Directory.CreateDirectory(SettingsDir);
                var path = Path.Combine(SettingsDir, "steam_auth.json");
                var pn = string.IsNullOrEmpty(personaName) ? "" : personaName.Replace("\\", "\\\\").Replace("\"", "\\\"");
                File.WriteAllText(path, $"{{\"steam_id\":\"{steamId}\",\"persona_name\":\"{pn}\",\"login_time\":\"{DateTime.UtcNow:O}\"}}");
            }
            catch { }
        }

        public static void ClearAuth()
        {
            try
            {
                var path = Path.Combine(SettingsDir, "steam_auth.json");
                if (File.Exists(path)) File.Delete(path);
                // Also clear avatar cache
                foreach (var f in Directory.GetFiles(SettingsDir, "avatar_*.cache"))
                {
                    try { File.Delete(f); } catch { }
                }
            }
            catch { }
        }

        public static bool IsDeveloper()
        {
            var id = GetSavedSteamId();
            return id == SteamAccountResolver.DeveloperSteamId;
        }

        public static string? GetSavedAvatarUrl()
        {
            try
            {
                var path = Path.Combine(SettingsDir, "steam_auth.json");
                if (!File.Exists(path)) return null;
                var json = File.ReadAllText(path);
                var match = Regex.Match(json, "\"avatar_url\"\\s*:\\s*\"([^\"]+)\"");
                return match.Success ? match.Groups[1].Value : null;
            }
            catch { return null; }
        }

        public static async Task<string?> FetchSteamAvatarUrlAsync(string steamId)
        {
            try
            {
                var cachePath = Path.Combine(SettingsDir, $"avatar_{steamId}.cache");
                if (File.Exists(cachePath) && File.GetLastWriteTime(cachePath) > DateTime.Now.AddHours(-24))
                    return File.ReadAllText(cachePath);

                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
                var xml = await client.GetStringAsync($"https://steamcommunity.com/profiles/{steamId}/?xml=1");

                var url = ExtractAvatarUrlFromXml(xml);
                if (!string.IsNullOrEmpty(url))
                {
                    File.WriteAllText(cachePath, url);
                    EmbedAvatarUrlIntoAuth(url);
                }
                return url;
            }
            catch { return null; }
        }

        public static string? FetchSteamAvatarUrlSync(string steamId)
        {
            try
            {
                var cachePath = Path.Combine(SettingsDir, $"avatar_{steamId}.cache");
                if (File.Exists(cachePath) && File.GetLastWriteTime(cachePath) > DateTime.Now.AddHours(-24))
                    return File.ReadAllText(cachePath);

                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
                var xml = client.GetStringAsync($"https://steamcommunity.com/profiles/{steamId}/?xml=1").GetAwaiter().GetResult();

                var url = ExtractAvatarUrlFromXml(xml);
                if (!string.IsNullOrEmpty(url))
                {
                    File.WriteAllText(cachePath, url);
                    EmbedAvatarUrlIntoAuth(url);
                }
                return url;
            }
            catch { return null; }
        }

        private static string? ExtractAvatarUrlFromXml(string xml)
        {
            // Try CDATA first (common format), then plain text
            var match = Regex.Match(xml, "<avatarFull><!\\[CDATA\\[(.*?)\\]\\]></avatarFull>");
            if (!match.Success)
                match = Regex.Match(xml, "<avatarFull>([^<]+)</avatarFull>");
            if (!match.Success)
                match = Regex.Match(xml, "<avatarMedium><!\\[CDATA\\[(.*?)\\]\\]></avatarMedium>");
            if (!match.Success)
                match = Regex.Match(xml, "<avatarMedium>([^<]+)</avatarMedium>");
            if (!match.Success)
                match = Regex.Match(xml, "<avatarIcon><!\\[CDATA\\[(.*?)\\]\\]></avatarIcon>");
            if (!match.Success)
                match = Regex.Match(xml, "<avatarIcon>([^<]+)</avatarIcon>");
            return match.Success ? match.Groups[1].Value : null;
        }

        private static void EmbedAvatarUrlIntoAuth(string url)
        {
            try
            {
                var authPath = Path.Combine(SettingsDir, "steam_auth.json");
                if (File.Exists(authPath))
                {
                    var json = File.ReadAllText(authPath);
                    if (!json.Contains("\"avatar_url\""))
                    {
                        json = json.Replace("}", $",\"avatar_url\":\"{url}\"}}");
                        File.WriteAllText(authPath, json);
                    }
                }
            }
            catch { }
        }

        public static async Task<(bool ok, string? steamId, string? error)> AuthenticateAsync()
        {
            TcpListener? listener = null;
            try
            {
                // Find free port
                listener = new TcpListener(IPAddress.Loopback, 0);
                listener.Start();
                int port = ((IPEndPoint)listener.LocalEndpoint).Port;

                var prefix = $"http://localhost:{port}/";
                var returnTo = Uri.EscapeDataString($"{prefix}steamauth");
                var realm = Uri.EscapeDataString(prefix);

                var openIdUrl = "https://steamcommunity.com/openid/login?" +
                    "openid.ns=http%3A%2F%2Fspecs.openid.net%2Fauth%2F2.0" +
                    "&openid.mode=checkid_setup" +
                    $"&openid.return_to={returnTo}" +
                    $"&openid.realm={realm}" +
                    "&openid.identity=http%3A%2F%2Fspecs.openid.net%2Fauth%2F2.0%2Fidentifier_select" +
                    "&openid.claimed_id=http%3A%2F%2Fspecs.openid.net%2Fauth%2F2.0%2Fidentifier_select";

                // Open system browser
                Process.Start(new ProcessStartInfo(openIdUrl) { UseShellExecute = true });

                // Wait for callback (5 min timeout)
                using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
                var client = await listener.AcceptTcpClientAsync(cts.Token);

                using var stream = client.GetStream();
                // Give browser a moment to send full request
                await Task.Delay(100);

                using var reader = new StreamReader(stream, Encoding.UTF8);
                var requestLine = await reader.ReadLineAsync();
                if (string.IsNullOrEmpty(requestLine))
                    return (false, null, "Empty request from browser");

                // Drain headers
                while (true)
                {
                    var line = await reader.ReadLineAsync();
                    if (string.IsNullOrEmpty(line)) break;
                }

                // Parse GET path
                var reqMatch = Regex.Match(requestLine, @"GET\s+(/[^\s]*)\s+HTTP");
                if (!reqMatch.Success)
                    return (false, null, "Invalid HTTP request");

                var pathAndQuery = reqMatch.Groups[1].Value;

                // Send styled success page
                var html = @"<!DOCTYPE html>
<html lang=""en"">
<head>
<meta charset=""utf-8"">
<meta name=""viewport"" content=""width=device-width, initial-scale=1"">
<title>MatrixHole-Engine — Authorized</title>
<style>
@import url('https://fonts.googleapis.com/css2?family=JetBrains+Mono:wght@400;700&family=Inter:wght@400;600;700&display=swap');
* { margin:0; padding:0; box-sizing:border-box; }
body {
  font-family:'Inter',system-ui,-apple-system,sans-serif;
  background:#060608;
  color:#fff;
  min-height:100vh;
  display:flex;
  align-items:center;
  justify-content:center;
  overflow:hidden;
}
.bg-glow {
  position:fixed;
  width:700px; height:700px;
  border-radius:50%;
  background:radial-gradient(circle, rgba(199,62,29,0.12) 0%, transparent 70%);
  animation:pulse 4s ease-in-out infinite;
  top:50%; left:50%;
  transform:translate(-50%,-50%);
}
@keyframes pulse {
  0%,100%{ transform:translate(-50%,-50%) scale(1); opacity:0.4; }
  50%{ transform:translate(-50%,-50%) scale(1.25); opacity:0.7; }
}
.card {
  position:relative;
  background:#12121a;
  border:1px solid #1e1e2e;
  border-radius:24px;
  padding:56px 64px;
  text-align:center;
  max-width:460px;
  width:92%;
  box-shadow:0 32px 80px rgba(0,0,0,0.45), inset 0 1px 0 rgba(255,255,255,0.03);
  animation:slideUp 0.7s cubic-bezier(0.16,1,0.3,1);
}
@keyframes slideUp {
  from{ opacity:0; transform:translateY(40px); }
  to{ opacity:1; transform:translateY(0); }
}
.check-circle {
  width:80px; height:80px;
  border-radius:50%;
  background:#00ff7f;
  display:flex; align-items:center; justify-content:center;
  margin:0 auto 28px;
  animation:scaleIn 0.55s cubic-bezier(0.34,1.56,0.64,1) 0.15s both;
  box-shadow:0 0 40px rgba(0,255,127,0.25);
}
@keyframes scaleIn {
  from{ transform:scale(0); }
  to{ transform:scale(1); }
}
.check-circle svg { width:40px; height:40px; stroke:#060608; stroke-width:3.5; fill:none; stroke-linecap:round; stroke-linejoin:round; }
h2 { font-size:26px; font-weight:700; margin-bottom:14px; color:#e8e8f0; letter-spacing:-0.3px; }
p { font-size:15px; color:#888; line-height:1.65; }
.app-name { color:#c73e1d; font-weight:600; }
.footer { margin-top:32px; padding-top:22px; border-top:1px solid #1e1e2e; font-family:'JetBrains Mono',monospace; font-size:11px; color:#444; letter-spacing:0.5px; text-transform:uppercase; }
</style>
</head>
<body>
<div class=""bg-glow""></div>
<div class=""card"">
  <div class=""check-circle"">
    <svg viewBox=""0 0 24 24""><polyline points=""20 6 9 17 4 12""/></svg>
  </div>
  <h2>Authorization Successful</h2>
  <p>You can close this tab and return to <span class=""app-name"">MatrixHole-Engine</span>.</p>
  <div class=""footer"">Session validated &bull; Steam OpenID 2.0</div>
</div>
</body>
</html>";

                var htmlBytes = Encoding.UTF8.GetBytes(html);
                var response = $"HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: {htmlBytes.Length}\r\nConnection: close\r\n\r\n";
                var respBytes = Encoding.UTF8.GetBytes(response);
                await stream.WriteAsync(respBytes);
                await stream.WriteAsync(htmlBytes);

                // Extract claimed_id from query
                var queryIdx = pathAndQuery.IndexOf('?');
                var query = queryIdx >= 0 ? pathAndQuery.Substring(queryIdx + 1) : "";

                var parsed = ParseQueryString(query);
                parsed.TryGetValue("openid.claimed_id", out var claimedId);

                if (string.IsNullOrEmpty(claimedId))
                    return (false, null, "Steam did not return claimed_id");

                var steamMatch = Regex.Match(claimedId, @"/openid/id/(\d+)$");
                if (!steamMatch.Success)
                    return (false, null, "Invalid claimed_id format");

                var steamId = steamMatch.Groups[1].Value;

                // Try to get persona name from local Steam data
                var (_, _, personaName) = SteamAccountResolver.GetMostRecentAccount();
                SaveAuth(steamId, personaName);

                // Fetch avatar in background (fire-and-forget)
                _ = Task.Run(async () => await FetchSteamAvatarUrlAsync(steamId));

                return (true, steamId, null);
            }
            catch (OperationCanceledException)
            {
                return (false, null, "Authorization timed out (5 minutes)");
            }
            catch (Exception ex)
            {
                return (false, null, ex.Message);
            }
            finally
            {
                listener?.Stop();
            }
        }

        private static Dictionary<string, string> ParseQueryString(string query)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in query.Split('&'))
            {
                var parts = pair.Split('=', 2);
                if (parts.Length == 2)
                {
                    var key = Uri.UnescapeDataString(parts[0]);
                    var val = Uri.UnescapeDataString(parts[1]);
                    result[key] = val;
                }
            }
            return result;
        }
    }
}
