using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace MatrixHole.Core
{
    /// <summary>
    /// SteamFriendsIntegration — кто из друзей в SCP:SL (п.91 OptimizatorPlan).
    /// Требует Steam Web API Key.
    /// </summary>
    public static class SteamFriendsIntegration
    {
        private static string SteamApiKey => AppSecrets.SteamApiKey;
        private const int ScpSlAppId = 700330;

        public class FriendStatus
        {
            public string SteamId { get; set; } = "";
            public string PersonaName { get; set; } = "";
            public bool IsPlayingScpSl { get; set; }
            public string? GameServerIp { get; set; }
            public string? LobbyId { get; set; }
        }

        public static async Task<List<FriendStatus>> GetFriendsPlayingScpSlAsync(string steamId64)
        {
            var result = new List<FriendStatus>();
            try
            {
                using var client = new HttpClient();
                // Get friend list
                var friendsUrl = $"https://api.steampowered.com/ISteamUser/GetFriendList/v1/?key={SteamApiKey}&steamid={steamId64}&relationship=friend";
                var friendsResponse = await client.GetStringAsync(friendsUrl);
                var friendsData = JsonConvert.DeserializeObject<dynamic>(friendsResponse);
                var friendIds = new List<string>();
                foreach (var f in friendsData?.friendslist?.friends ?? new List<dynamic>())
                    friendIds.Add(f.steamid.ToString());

                if (!friendIds.Any()) return result;

                // Get player summaries in batches of 100
                foreach (var batch in friendIds.Chunk(100))
                {
                    var ids = string.Join(",", batch);
                    var summaryUrl = $"https://api.steampowered.com/ISteamUser/GetPlayerSummaries/v2/?key={SteamApiKey}&steamids={ids}";
                    var summaryResponse = await client.GetStringAsync(summaryUrl);
                    var summaryData = JsonConvert.DeserializeObject<dynamic>(summaryResponse);

                    foreach (var player in summaryData?.response?.players ?? new List<dynamic>())
                    {
                        var gameId = player?.gameid?.ToString();
                        if (gameId == ScpSlAppId.ToString())
                        {
                            result.Add(new FriendStatus
                            {
                                SteamId = player!.steamid.ToString(),
                                PersonaName = player.personaname.ToString(),
                                IsPlayingScpSl = true,
                                GameServerIp = player?.gameserverip?.ToString(),
                                LobbyId = player?.lobbysteamid?.ToString()
                            });
                        }
                    }
                }
            }
            catch { }
            return result;
        }

        public static string GetJoinUrl(string steamId) => $"steam://joinlobby/{ScpSlAppId}/{steamId}";
    }
}
