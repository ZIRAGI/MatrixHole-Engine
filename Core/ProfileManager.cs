using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace MatrixHole.Core
{
    /// <summary>
    /// ProfileManager — система профилей конфигов (п.32 OptimizatorPlan).
    /// Профили с привязкой к IP/названию сервера. Авто-подгрузка при запуске игры.
    /// </summary>
    public static class ProfileManager
    {
        private static readonly string ProfilesDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MatrixHole", "profiles");

        private static readonly string ActiveProfileFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MatrixHole", "active_profile.json");

        private static readonly string ServerBindingsFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MatrixHole", "server_bindings.json");

        static ProfileManager()
        {
            Directory.CreateDirectory(ProfilesDir);
        }

        public class Profile
        {
            public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
            public string Name { get; set; } = "Новый профиль";
            public string? Description { get; set; }
            public DateTime CreatedAt { get; set; } = DateTime.Now;
            public DateTime ModifiedAt { get; set; } = DateTime.Now;
            public Dictionary<string, object> Config { get; set; } = new();
            public bool IsDefault { get; set; } = false;
        }

        public class ServerBinding
        {
            public string ServerIp { get; set; } = "";
            public string ServerName { get; set; } = "";
            public string ProfileId { get; set; } = "";
        }

        public static List<Profile> GetAllProfiles()
        {
            var list = new List<Profile>();
            foreach (var file in Directory.GetFiles(ProfilesDir, "*.json"))
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var prof = JsonConvert.DeserializeObject<Profile>(json);
                    if (prof != null) list.Add(prof);
                }
                catch { }
            }
            if (!list.Any())
            {
                var def = CreateDefaultProfile();
                list.Add(def);
            }
            return list.OrderByDescending(p => p.IsDefault).ThenBy(p => p.Name).ToList();
        }

        public static Profile CreateDefaultProfile()
        {
            var def = new Profile { Name = "По умолчанию", IsDefault = true, Description = "Стандартный профиль" };
            SaveProfile(def);
            return def;
        }

        public static Profile? GetProfileById(string id)
        {
            return GetAllProfiles().FirstOrDefault(p => p.Id == id);
        }

        public static void SaveProfile(Profile profile)
        {
            profile.ModifiedAt = DateTime.Now;
            var path = Path.Combine(ProfilesDir, $"{profile.Id}.json");
            File.WriteAllText(path, JsonConvert.SerializeObject(profile, Formatting.Indented));
        }

        public static bool DeleteProfile(string id)
        {
            var path = Path.Combine(ProfilesDir, $"{id}.json");
            if (!File.Exists(path)) return false;
            File.Delete(path);
            return true;
        }

        public static void SetActiveProfile(string id)
        {
            File.WriteAllText(ActiveProfileFile, JsonConvert.SerializeObject(new { ActiveProfileId = id, SwitchedAt = DateTime.Now }));
        }

        public static string? GetActiveProfileId()
        {
            try
            {
                if (!File.Exists(ActiveProfileFile)) return null;
                var json = File.ReadAllText(ActiveProfileFile);
                var obj = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                return obj?["ActiveProfileId"]?.ToString();
            }
            catch { return null; }
        }

        public static Profile? GetActiveProfile()
        {
            var id = GetActiveProfileId();
            if (string.IsNullOrEmpty(id)) return GetAllProfiles().FirstOrDefault(p => p.IsDefault);
            return GetProfileById(id);
        }

        // --- Server bindings ---

        public static List<ServerBinding> GetServerBindings()
        {
            try
            {
                if (!File.Exists(ServerBindingsFile)) return new List<ServerBinding>();
                var json = File.ReadAllText(ServerBindingsFile);
                return JsonConvert.DeserializeObject<List<ServerBinding>>(json) ?? new List<ServerBinding>();
            }
            catch { return new List<ServerBinding>(); }
        }

        public static void BindServerToProfile(string serverIp, string serverName, string profileId)
        {
            var bindings = GetServerBindings();
            bindings.RemoveAll(b => b.ServerIp == serverIp || b.ServerName == serverName);
            bindings.Add(new ServerBinding { ServerIp = serverIp, ServerName = serverName, ProfileId = profileId });
            File.WriteAllText(ServerBindingsFile, JsonConvert.SerializeObject(bindings, Formatting.Indented));
        }

        public static void UnbindServer(string serverIp)
        {
            var bindings = GetServerBindings();
            bindings.RemoveAll(b => b.ServerIp == serverIp);
            File.WriteAllText(ServerBindingsFile, JsonConvert.SerializeObject(bindings, Formatting.Indented));
        }

        public static string? GetProfileIdForServer(string serverIp, string? serverName = null)
        {
            var bindings = GetServerBindings();
            return bindings.FirstOrDefault(b => b.ServerIp == serverIp || (!string.IsNullOrEmpty(serverName) && b.ServerName == serverName))?.ProfileId;
        }

        /// <summary>
        /// Авто-подгрузка профиля по серверу. Вызывать при обнаружении подключения к серверу.
        /// </summary>
        public static Profile? AutoSwitchForServer(string serverIp, string? serverName = null)
        {
            var profileId = GetProfileIdForServer(serverIp, serverName);
            if (string.IsNullOrEmpty(profileId)) return null;
            var profile = GetProfileById(profileId);
            if (profile != null) SetActiveProfile(profileId);
            return profile;
        }

        public static string ExportProfileToJson(string profileId)
        {
            var prof = GetProfileById(profileId);
            if (prof == null) return "{\"error\":\"Profile not found\"}";
            return JsonConvert.SerializeObject(prof, Formatting.Indented);
        }

        public static string? ImportProfileFromJson(string json)
        {
            try
            {
                var prof = JsonConvert.DeserializeObject<Profile>(json);
                if (prof == null) return null;
                prof.Id = Guid.NewGuid().ToString("N")[..8];
                prof.Name += " (импорт)";
                prof.CreatedAt = DateTime.Now;
                SaveProfile(prof);
                return prof.Id;
            }
            catch { return null; }
        }
    }
}
