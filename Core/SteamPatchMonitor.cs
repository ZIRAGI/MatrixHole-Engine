using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace MatrixHole.Core
{
    /// <summary>
    /// SteamPatchMonitor — мониторинг обновлений SCP:SL через Steam API (п.56 OptimizatorPlan).
    /// </summary>
    public static class SteamPatchMonitor
    {
        private const int ScpSlAppId = 700330;
        private static readonly string CacheFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MatrixHole", "steam_patch_cache.json");

        public class PatchInfo
        {
            public uint BuildId { get; set; }
            public DateTime Timestamp { get; set; }
            public string? ChangeNumber { get; set; }
        }

        public static async Task<PatchInfo?> CheckLatestPatchAsync()
        {
            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(10);
                // Steam Web API for app info (unofficial, may require key)
                var url = $"https://api.steamcmd.net/v1/info/{ScpSlAppId}";
                var response = await client.GetStringAsync(url);
                var data = JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JObject>(response);
                if (data == null) return null;

                var buildId = data["data"]?[ScpSlAppId.ToString()]?["depots"]?["branches"]?["public"]?["buildid"];
                if (buildId == null) return null;

                return new PatchInfo
                {
                    BuildId = (uint)buildId,
                    Timestamp = DateTime.Now
                };
            }
            catch { return null; }
        }

        public static async Task<bool> IsNewPatchAvailableAsync()
        {
            var latest = await CheckLatestPatchAsync();
            if (latest == null) return false;

            var cached = GetCachedPatch();
            if (cached == null || latest.BuildId != cached.BuildId)
            {
                SaveCachedPatch(latest);
                return cached != null; // Only report new if we had a previous cache
            }
            return false;
        }

        public static PatchInfo? GetCachedPatch()
        {
            try
            {
                if (!File.Exists(CacheFile)) return null;
                return JsonConvert.DeserializeObject<PatchInfo>(File.ReadAllText(CacheFile));
            }
            catch { return null; }
        }

        public static void SaveCachedPatch(PatchInfo info)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(CacheFile)!);
                File.WriteAllText(CacheFile, JsonConvert.SerializeObject(info));
            }
            catch { }
        }

        public static string GetStatusJson()
        {
            var cached = GetCachedPatch();
            return JsonConvert.SerializeObject(new
            {
                appId = ScpSlAppId,
                lastKnownBuild = cached?.BuildId ?? 0,
                lastCheck = cached?.Timestamp.ToString("yyyy-MM-dd HH:mm") ?? "never"
            });
        }
    }
}
