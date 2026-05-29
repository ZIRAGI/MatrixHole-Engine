using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace MatrixHole.Core
{
    /// <summary>
    /// PushNotifications — мониторинг слотов на избранных серверах (п.57 OptimizatorPlan).
    /// Windows Toast при освобождении слота.
    /// </summary>
    public static class PushNotifications
    {
        public class FavoriteServer
        {
            public string Ip { get; set; } = "";
            public string Name { get; set; } = "";
            public int LastPlayerCount { get; set; }
            public int MaxPlayers { get; set; }
            public bool Notified { get; set; }
        }

        private static readonly string FavoritesFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MatrixHole", "favorite_servers.json");

        private static CancellationTokenSource? _cts;

        public static List<FavoriteServer> GetFavorites()
        {
            try
            {
                if (File.Exists(FavoritesFile))
                    return JsonConvert.DeserializeObject<List<FavoriteServer>>(File.ReadAllText(FavoritesFile)) ?? new List<FavoriteServer>();
            }
            catch { }
            return new List<FavoriteServer>();
        }

        public static void SaveFavorites(List<FavoriteServer> favorites)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FavoritesFile)!);
            File.WriteAllText(FavoritesFile, JsonConvert.SerializeObject(favorites, Formatting.Indented));
        }

        public static void AddFavorite(string ip, string name, int maxPlayers)
        {
            var list = GetFavorites();
            if (!list.Any(s => s.Ip == ip))
            {
                list.Add(new FavoriteServer { Ip = ip, Name = name, MaxPlayers = maxPlayers });
                SaveFavorites(list);
            }
        }

        public static void RemoveFavorite(string ip)
        {
            var list = GetFavorites();
            list.RemoveAll(s => s.Ip == ip);
            SaveFavorites(list);
        }

        public static void StartMonitoring()
        {
            StopMonitoring();
            _cts = new CancellationTokenSource();
            _ = Task.Run(() => MonitorLoop(_cts.Token));
        }

        public static void StopMonitoring()
        {
            _cts?.Cancel();
        }

        private static async Task MonitorLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                var favorites = GetFavorites();
                foreach (var server in favorites)
                {
                    try
                    {
                        var currentPlayers = await QueryPlayerCountAsync(server.Ip);
                        if (currentPlayers < server.MaxPlayers && server.LastPlayerCount >= server.MaxPlayers && !server.Notified)
                        {
                            TrayIconManager.ShowToast("Свободный слот!", $"На сервере {server.Name} освободилось место.");
                            server.Notified = true;
                        }
                        if (currentPlayers >= server.MaxPlayers)
                            server.Notified = false;
                        server.LastPlayerCount = currentPlayers;
                    }
                    catch { }
                }
                SaveFavorites(favorites);
                await Task.Delay(30000, token); // check every 30s
            }
        }

        private static async Task<int> QueryPlayerCountAsync(string ip)
        {
            // Placeholder: real implementation would query Steam master server or game server directly
            await Task.Delay(100);
            return new Random().Next(20, 35); // Mock data
        }
    }
}
