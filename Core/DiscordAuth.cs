using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace MatrixHole.Core
{
    /// <summary>
    /// DiscordAuth — вход через Discord + проверка ролей.
    /// OAuth2 flow с guild member intent. Uses dynamic port via TcpListener (no admin rights needed).
    /// </summary>
    public static class DiscordAuth
    {
        private static string ClientId => AppSecrets.DiscordClientId;
        private static string ClientSecret => AppSecrets.DiscordClientSecret;
        private static string RequiredGuildId => AppSecrets.DiscordRequiredGuildId;
        private static string[] AllowedRoleIds => AppSecrets.DiscordAllowedRoleIds;

        private static readonly string AuthFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MatrixHole", "discord_auth.json");

        private static TcpListener? _listener;
        private static CancellationTokenSource? _cts;
        private static string _currentRedirectUri = "http://localhost:3000/discord/callback";

        public class DiscordUser
        {
            public string Id { get; set; } = "";
            public string Username { get; set; } = "";
            public string? Avatar { get; set; }
            public string? AccessToken { get; set; }
            public DateTime ExpiresAt { get; set; }
            public List<string> GuildRoles { get; set; } = new();
            public DateTime RolesUpdatedAt { get; set; }
        }

        public static string GetOAuthUrl()
        {
            var scopes = Uri.EscapeDataString("identify guilds guilds.members.read");
            return $"https://discord.com/api/oauth2/authorize?client_id={ClientId}&redirect_uri={Uri.EscapeDataString(_currentRedirectUri)}&response_type=code&scope={scopes}";
        }

        public static async Task<(bool ok, DiscordUser? user, string? error)> ExchangeCodeAsync(string code)
        {
            try
            {
                using var client = new HttpClient();
                var payload = new Dictionary<string, string>
                {
                    ["client_id"] = ClientId,
                    ["client_secret"] = ClientSecret,
                    ["grant_type"] = "authorization_code",
                    ["code"] = code,
                    ["redirect_uri"] = _currentRedirectUri
                };

                var response = await client.PostAsync("https://discord.com/api/v10/oauth2/token", new FormUrlEncodedContent(payload));
                var json = await response.Content.ReadAsStringAsync();
                var tokenData = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                if (tokenData == null || !tokenData.ContainsKey("access_token"))
                    return (false, null, "Invalid token response: " + json);

                var accessToken = tokenData["access_token"];
                var user = await FetchUserAsync(accessToken);
                if (user == null) return (false, null, "Failed to fetch user");

                user.AccessToken = accessToken;
                var expiresIn = tokenData.GetValueOrDefault("expires_in", "60480");
                if (int.TryParse(expiresIn, out var seconds))
                    user.ExpiresAt = DateTime.UtcNow.AddSeconds(seconds);
                else
                    user.ExpiresAt = DateTime.UtcNow.AddDays(7);

                // Check guild roles
                user.GuildRoles = await FetchGuildRolesAsync(accessToken);
                user.RolesUpdatedAt = DateTime.UtcNow;
                SaveAuth(user);
                return (true, user, null);
            }
            catch (Exception ex)
            {
                return (false, null, ex.Message);
            }
        }

        private static async Task<DiscordUser?> FetchUserAsync(string accessToken)
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            var response = await client.GetStringAsync("https://discord.com/api/v10/users/@me");
            var data = JsonConvert.DeserializeObject<DiscordUser>(response);
            return data;
        }

        private static async Task<List<string>> FetchGuildRolesAsync(string accessToken)
        {
            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                var response = await client.GetStringAsync($"https://discord.com/api/v10/users/@me/guilds/{RequiredGuildId}/member");
                var data = JsonConvert.DeserializeObject<Dictionary<string, object>>(response);
                if (data != null && data.TryGetValue("roles", out var rolesObj))
                    return JsonConvert.DeserializeObject<List<string>>(rolesObj.ToString()!) ?? new List<string>();
            }
            catch { }
            return new List<string>();
        }

        public static bool HasRequiredRole(DiscordUser? user)
        {
            if (user == null) return false;
            return AllowedRoleIds.Any(roleId => user.GuildRoles.Contains(roleId));
        }

        // Priority: Coder > Administrator > Beta-Tester
        public static string? GetPrimaryRole(DiscordUser? user)
        {
            if (user == null) return null;
            var roles = AppSecrets.DiscordAllowedRoleIds;
            if (roles.Length >= 3 && user.GuildRoles.Contains(roles[2])) return "Coder";
            if (roles.Length >= 2 && user.GuildRoles.Contains(roles[1])) return "Administrator";
            if (roles.Length >= 1 && user.GuildRoles.Contains(roles[0])) return "Beta-Tester";
            return null;
        }

        public static DiscordUser? GetSavedAuth()
        {
            try
            {
                if (!File.Exists(AuthFile)) return null;
                return JsonConvert.DeserializeObject<DiscordUser>(File.ReadAllText(AuthFile));
            }
            catch { return null; }
        }

        public static void SaveAuth(DiscordUser user)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(AuthFile)!);
            File.WriteAllText(AuthFile, JsonConvert.SerializeObject(user, Formatting.Indented));
        }

        public static void ClearAuth()
        {
            if (File.Exists(AuthFile)) File.Delete(AuthFile);
        }

        /// <summary>
        /// Refresh guild roles from Discord API if cached data is older than maxAge.
        /// Returns true if roles were actually refreshed.
        /// </summary>
        public static bool RefreshRolesIfStale(TimeSpan maxAge)
        {
            try
            {
                var user = GetSavedAuth();
                if (user == null) return false;
                if (string.IsNullOrEmpty(user.AccessToken)) return false;
                if (DateTime.UtcNow - user.RolesUpdatedAt < maxAge) return false;

                // Run on thread-pool to avoid WPF COM bridge deadlocks
                var roles = Task.Run(async () => await FetchGuildRolesAsync(user.AccessToken)).GetAwaiter().GetResult();
                if (roles.Count == 0 && user.GuildRoles.Count > 0)
                {
                    // API returned empty roles but we had roles before — likely a temporary error.
                    // Don't overwrite valid cached roles; just bump timestamp to avoid hammering.
                    user.RolesUpdatedAt = DateTime.UtcNow.Add(maxAge); // defer next retry
                    SaveAuth(user);
                    Core.DevLogger.Action("Discord roles refresh skipped", "DiscordAuth", "API returned empty, keeping cached roles");
                    return false;
                }
                user.GuildRoles = roles;
                user.RolesUpdatedAt = DateTime.UtcNow;
                SaveAuth(user);
                Core.DevLogger.Action("Discord roles refreshed", "DiscordAuth", $"Roles: {string.Join(",", roles)}");
                return true;
            }
            catch (Exception ex)
            {
                Core.DevLogger.Action("Discord roles refresh failed", "DiscordAuth", ex.Message);
                return false;
            }
        }

        private static string ExtractQueryParam(string query, string key)
        {
            var pattern = $@"(?:^|&){key}=([^&]*)";
            var match = System.Text.RegularExpressions.Regex.Match(query, pattern);
            return match.Success ? Uri.UnescapeDataString(match.Groups[1].Value) : "";
        }

        // ============================================================
        // Local TCP callback server for OAuth flow (dynamic port, no admin needed)
        // ============================================================

        public static string StartCallbackServer(Action<bool, string> onResult)
        {
            StopCallbackServer();
            _cts = new CancellationTokenSource();

            // Use fixed port 3000 (must match Discord app redirect URI)
            int port = 3000;
            try
            {
                _listener = new TcpListener(IPAddress.Loopback, port);
                _listener.Start();
            }
            catch (SocketException)
            {
                // Port 3000 is taken — try a dynamic port as fallback
                // NOTE: Discord OAuth will fail unless this URI is registered in Discord app settings
                _listener = new TcpListener(IPAddress.Loopback, 0);
                _listener.Start();
                port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            }
            _currentRedirectUri = $"http://localhost:{port}/discord/callback";

            Task.Run(async () =>
            {
                try
                {
                    while (!_cts.Token.IsCancellationRequested)
                    {
                        var client = await _listener.AcceptTcpClientAsync(_cts.Token);
                        _ = Task.Run(async () => await HandleClientAsync(client, onResult));
                    }
                }
                catch (OperationCanceledException)
                {
                    // Normal shutdown
                }
                catch (Exception ex)
                {
                    onResult(false, ex.Message);
                }
                finally
                {
                    StopCallbackServer();
                }
            });

            return _currentRedirectUri;
        }

        private static async Task HandleClientAsync(TcpClient client, Action<bool, string> onResult)
        {
            try
            {
                using var stream = client.GetStream();
                await Task.Delay(100); // Give browser a moment to send full request

                using var reader = new StreamReader(stream, Encoding.UTF8);
                var requestLine = await reader.ReadLineAsync();
                if (string.IsNullOrEmpty(requestLine))
                {
                    SendHttpResponse(stream, 400, "Bad Request");
                    return;
                }

                // Drain headers
                while (true)
                {
                    var line = await reader.ReadLineAsync();
                    if (string.IsNullOrEmpty(line)) break;
                }

                var reqMatch = System.Text.RegularExpressions.Regex.Match(requestLine, @"GET\s+(/[^\s]*)\s+HTTP");
                if (!reqMatch.Success)
                {
                    SendHttpResponse(stream, 400, "Bad Request");
                    return;
                }

                var pathAndQuery = reqMatch.Groups[1].Value;
                var queryIdx = pathAndQuery.IndexOf('?');
                var query = queryIdx >= 0 ? pathAndQuery.Substring(queryIdx + 1) : "";
                var path = queryIdx >= 0 ? pathAndQuery.Substring(0, queryIdx) : pathAndQuery;

                if (path != "/discord/callback")
                {
                    SendHttpResponse(stream, 404, "Not Found");
                    return;
                }

                var code = ExtractQueryParam(query, "code");
                var error = ExtractQueryParam(query, "error");

                if (!string.IsNullOrEmpty(error))
                {
                    SendHtmlResponse(stream, false, "Authorization was cancelled or denied.");
                    onResult(false, "Authorization denied");
                    StopCallbackServer();
                    return;
                }

                if (!string.IsNullOrEmpty(code))
                {
                    var (ok, user, err) = await ExchangeCodeAsync(code);
                    if (ok && user != null)
                    {
                        Core.DevLogger.Auth("Discord login", user.Username, true, $"Roles: {string.Join(",", user.GuildRoles)}");
                        SendHtmlResponse(stream, true, $"Welcome, {user.Username}! You can close this tab and return to the app.");
                        onResult(true, user.Username);
                    }
                    else
                    {
                        string msg;
                        if (user != null && !HasRequiredRole(user))
                            msg = "Access denied: your account does not have the required Discord role.";
                        else
                            msg = !string.IsNullOrEmpty(err) ? $"Authentication failed: {err}" : "Authentication failed.";
                        SendHtmlResponse(stream, false, msg);
                        onResult(false, msg);
                    }
                    StopCallbackServer();
                    return;
                }

                SendHtmlResponse(stream, false, "Invalid callback. Missing authorization code.");
            }
            catch (Exception ex)
            {
                try { SendHtmlResponse(client.GetStream(), false, "Server error."); } catch { }
                onResult(false, ex.Message);
                StopCallbackServer();
            }
        }

        public static void StopCallbackServer()
        {
            try { _cts?.Cancel(); } catch { }
            try { _listener?.Stop(); } catch { }
            _listener = null;
            _cts = null;
        }

        private static void SendHttpResponse(System.Net.Sockets.NetworkStream stream, int statusCode, string statusText)
        {
            var response = $"HTTP/1.1 {statusCode} {statusText}\r\nContent-Length: 0\r\nConnection: close\r\n\r\n";
            var bytes = Encoding.UTF8.GetBytes(response);
            stream.Write(bytes, 0, bytes.Length);
        }

        private static void SendHtmlResponse(System.Net.Sockets.NetworkStream stream, bool success, string message)
        {
            var accent = success ? "#5865F2" : "#c73e1d";
            var accentSoft = success ? "#4752C4" : "#a03020";
            var icon = success
                ? @"<svg class='icon' viewBox='0 0 24 24' fill='none' stroke='url(#iconGrad)' stroke-width='1.5' stroke-linecap='round' stroke-linejoin='round'><defs><linearGradient id='iconGrad' x1='0%' y1='0%' x2='100%' y2='100%'><stop offset='0%' stop-color='#5865F2'/><stop offset='100%' stop-color='#EB459E'/></linearGradient></defs><path d='M22 11.08V12a10 10 0 1 1-5.93-9.14'/><polyline points='22 4 12 14.01 9 11.01'/></svg>"
                : @"<svg class='icon' viewBox='0 0 24 24' fill='none' stroke='#c73e1d' stroke-width='1.5' stroke-linecap='round' stroke-linejoin='round'><circle cx='12' cy='12' r='10'/><line x1='15' y1='9' x2='9' y2='15'/><line x1='9' y1='9' x2='15' y2='15'/></svg>";

            var html = $@"<!DOCTYPE html>
<html lang='en'>
<head>
<meta charset='utf-8'>
<meta name='viewport' content='width=device-width, initial-scale=1.0'>
<title>MatrixHole-Engine — {(success ? "Authorized" : "Access Denied")}</title>
<style>
* {{ margin:0; padding:0; box-sizing:border-box; }}
@keyframes fadeIn {{ from {{ opacity:0; transform:translateY(20px) scale(0.96); }} to {{ opacity:1; transform:translateY(0) scale(1); }} }}
@keyframes iconIn {{ from {{ opacity:0; transform:scale(0.5); }} to {{ opacity:1; transform:scale(1); }} }}
@keyframes ringPulse {{ 0%,100% {{ transform:scale(1); opacity:0.6; }} 50% {{ transform:scale(1.15); opacity:0.2; }} }}
@keyframes float {{ 0%,100% {{ transform:translateY(0); }} 50% {{ transform:translateY(-6px); }} }}
body {{
  margin:0; height:100vh;
  display:flex; align-items:center; justify-content:center;
  background:#060608;
  font-family:'Segoe UI',system-ui,sans-serif;
  overflow:hidden;
  -webkit-font-smoothing:antialiased;
}}
.orb {{
  position:absolute; border-radius:50%; filter:blur(80px); opacity:0.25; pointer-events:none;
}}
.orb-1 {{ width:400px; height:400px; background:radial-gradient(circle,{accent},transparent 70%); top:-100px; left:-80px; }}
.orb-2 {{ width:300px; height:300px; background:radial-gradient(circle,{accentSoft},transparent 70%); bottom:-60px; right:-60px; opacity:0.15; }}
.card {{
  position:relative;
  text-align:center;
  padding:56px 52px;
  background:rgba(12,12,18,0.65);
  backdrop-filter:blur(24px);
  border:1px solid rgba(255,255,255,0.07);
  border-radius:20px;
  box-shadow:0 25px 80px rgba(0,0,0,0.55), inset 0 1px 0 rgba(255,255,255,0.05);
  max-width:420px;
  width:90%;
  animation:fadeIn 0.6s cubic-bezier(0.16,1,0.3,1) forwards;
}}
.card::before {{
  content:''; position:absolute; top:0; left:15%; right:15%; height:1px;
  background:linear-gradient(90deg,transparent,rgba(88,101,242,0.4),rgba(235,69,158,0.3),transparent);
}}
.icon-wrap {{
  position:relative;
  width:72px; height:72px;
  margin:0 auto 24px;
  display:flex; align-items:center; justify-content:center;
}}
.icon-ring {{
  position:absolute; width:72px; height:72px; border-radius:50%;
  border:1.5px solid {accent}; opacity:0.3;
  animation:ringPulse 3s ease-in-out infinite;
}}
.icon {{
  width:32px; height:32px;
  animation:iconIn 0.5s cubic-bezier(0.16,1,0.3,1) 0.15s both, float 3.5s ease-in-out infinite;
}}
.badge {{
  display:inline-flex; align-items:center; gap:6px;
  font-size:10px; font-weight:700; letter-spacing:0.12em; text-transform:uppercase;
  color:#5a5a6a; background:rgba(255,255,255,0.025);
  border:1px solid rgba(255,255,255,0.06); padding:5px 14px;
  border-radius:100px; margin-bottom:20px;
}}
.badge-dot {{
  width:5px; height:5px; border-radius:50%; background:{accent};
  box-shadow:0 0 8px {accent}80;
}}
h1 {{
  font-size:24px; font-weight:800;
  background:linear-gradient(135deg,#e8e8f0 0%,#b0b0c0 50%,#e8e8f0 100%);
  -webkit-background-clip:text; -webkit-text-fill-color:transparent; background-clip:text;
  margin-bottom:12px; line-height:1.2;
}}
.status {{
  display:inline-block; font-size:13px; font-weight:600; color:{accent};
  margin-bottom:16px;
}}
p {{
  font-size:13.5px; color:#7a7a8a; line-height:1.7; margin:0;
}}
.footer {{
  display:flex; align-items:center; justify-content:center; gap:6px;
  margin-top:28px; font-size:10px; color:#3a3a48; font-weight:500;
}}
</style>
</head>
<body>
<div class='orb orb-1'></div>
<div class='orb orb-2'></div>
<div class='card'>
  <div class='badge'><span class='badge-dot'></span> MatrixHole-Engine</div>
  <div class='icon-wrap'>
    <div class='icon-ring'></div>
    {icon}
  </div>
  <div class='status'>{(success ? "✓ Authorized" : "✕ Access Denied")}</div>
  <h1>{(success ? "Welcome Back" : "Authorization Failed")}</h1>
  <p>{message}</p>
  <div class='footer'>
    <svg width='10' height='10' viewBox='0 0 24 24' fill='none' stroke='#3a3a48' stroke-width='2.5' stroke-linecap='round' stroke-linejoin='round'><rect x='3' y='11' width='18' height='11' rx='2' ry='2'/><path d='M7 11V7a5 5 0 0 1 10 0v4'/></svg>
    <span>End-to-end encrypted session</span>
  </div>
</div>
</body>
</html>";

            var htmlBytes = Encoding.UTF8.GetBytes(html);
            var response = $"HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: {htmlBytes.Length}\r\nConnection: close\r\n\r\n";
            var respBytes = Encoding.UTF8.GetBytes(response);
            stream.Write(respBytes, 0, respBytes.Length);
            stream.Write(htmlBytes, 0, htmlBytes.Length);
        }
    }
}
