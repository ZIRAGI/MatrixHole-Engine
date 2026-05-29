using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MatrixHole.Core
{
    /// <summary>
    /// Centralized secrets and configuration loaded from %LocalAppData%\MatrixHole\app_secrets.json.
    /// If the file does not exist, it is created with empty placeholders.
    /// </summary>
    public static class AppSecrets
    {
        private static readonly string SecretsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MatrixHole", "app_secrets.json");

        private static SecretsModel? _cached;
        private static readonly object _lock = new();

        public static string DiscordWebhook => Get().DiscordWebhook;
        public static string DiscordClientId => Get().DiscordClientId;
        public static string DiscordClientSecret => Get().DiscordClientSecret;
        public static string DiscordRequiredGuildId => Get().DiscordRequiredGuildId;
        public static string[] DiscordAllowedRoleIds => Get().DiscordAllowedRoleIds;
        public static string HmacKey => Get().HmacKey;
        public static string SteamApiKey => Get().SteamApiKey;
        public static string GitHubOwner => Get().GitHubOwner;
        public static string GitHubRepo => Get().GitHubRepo;

        public static SecretsModel Get()
        {
            if (_cached != null) return _cached;
            lock (_lock)
            {
                if (_cached != null) return _cached;
                _cached = LoadOrCreate();
                return _cached;
            }
        }

        public static void Reload()
        {
            lock (_lock)
            {
                _cached = null;
            }
        }

        private static SecretsModel LoadOrCreate()
        {
            try
            {
                if (File.Exists(SecretsPath))
                {
                    var json = File.ReadAllText(SecretsPath);
                    var model = JsonConvert.DeserializeObject<SecretsModel>(json);
                    if (model != null) return model;
                }
            }
            catch { }

            var fallback = new SecretsModel();
            try
            {
                var dir = Path.GetDirectoryName(SecretsPath)!;
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(SecretsPath, JsonConvert.SerializeObject(fallback, Formatting.Indented));
            }
            catch { }
            return fallback;
        }

        public class SecretsModel
        {
            [JsonProperty("discord_webhook")]
            public string DiscordWebhook { get; set; } = "";

            [JsonProperty("discord_client_id")]
            public string DiscordClientId { get; set; } = "";

            [JsonProperty("discord_client_secret")]
            public string DiscordClientSecret { get; set; } = "";

            [JsonProperty("discord_required_guild_id")]
            public string DiscordRequiredGuildId { get; set; } = "";

            [JsonProperty("discord_allowed_role_ids")]
            public string[] DiscordAllowedRoleIds { get; set; } = Array.Empty<string>();

            [JsonProperty("hmac_key")]
            public string HmacKey { get; set; } = "";

            [JsonProperty("steam_api_key")]
            public string SteamApiKey { get; set; } = "";

            [JsonProperty("github_owner")]
            public string GitHubOwner { get; set; } = "";

            [JsonProperty("github_repo")]
            public string GitHubRepo { get; set; } = "";
        }
    }
}
