using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace MatrixHole.Core
{
    /// <summary>
    /// Plugin Manager — управление DLL-плагинами (п.34 OptimizatorPlan).
    /// Поддерживает: установка, удаление, включение/выключение, автообновление, Drag&Drop.
    /// </summary>
    public static class PluginManager
    {
        private static readonly string PluginsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MatrixHole", "Plugins");

        private static readonly string PluginDbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MatrixHole", "plugin_db.json");

        public class PluginEntry
        {
            public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
            public string Name { get; set; } = "";
            public string FileName { get; set; } = "";
            public string FullPath { get; set; } = "";
            public string Hash { get; set; } = "";
            public string Version { get; set; } = "1.0.0";
            public string Author { get; set; } = "Unknown";
            public string Description { get; set; } = "";
            public bool Enabled { get; set; } = true;
            public bool IsLoaded { get; set; } = false;
            public DateTime InstalledAt { get; set; } = DateTime.Now;
            public DateTime? LastErrorAt { get; set; }
            public string? LastError { get; set; }
            public string? SourceUrl { get; set; }
            public bool AutoUpdate { get; set; } = false;
        }

        private static List<PluginEntry> LoadDb()
        {
            try
            {
                if (!File.Exists(PluginDbPath)) return new List<PluginEntry>();
                var json = File.ReadAllText(PluginDbPath);
                return JsonConvert.DeserializeObject<List<PluginEntry>>(json) ?? new List<PluginEntry>();
            }
            catch { return new List<PluginEntry>(); }
        }

        private static void SaveDb(List<PluginEntry> db)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(PluginDbPath)!);
                File.WriteAllText(PluginDbPath, JsonConvert.SerializeObject(db, Formatting.Indented));
            }
            catch { }
        }

        public static List<PluginEntry> GetPlugins()
        {
            return LoadDb();
        }

        public static string InstallPlugin(string sourcePath, string? name = null, string? author = null, string? description = null, bool autoUpdate = false)
        {
            try
            {
                if (!File.Exists(sourcePath)) return "{\"ok\":false,\"error\":\"file not found\"}";
                if (!Path.GetExtension(sourcePath).Equals(".dll", StringComparison.OrdinalIgnoreCase))
                    return "{\"ok\":false,\"error\":\"not a dll\"}";

                Directory.CreateDirectory(PluginsDir);
                var fileName = Path.GetFileName(sourcePath);
                var destPath = Path.Combine(PluginsDir, fileName);

                // If file exists, backup old version
                if (File.Exists(destPath))
                {
                    var bakPath = destPath + "." + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".bak";
                    File.Move(destPath, bakPath);
                }

                File.Copy(sourcePath, destPath, true);
                var hash = ComputeFileHash(destPath);

                var db = LoadDb();
                var existing = db.FirstOrDefault(p => p.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    existing.Hash = hash;
                    existing.FullPath = destPath;
                    existing.LastError = null;
                    existing.LastErrorAt = null;
                    existing.Version = BumpVersion(existing.Version);
                }
                else
                {
                    db.Add(new PluginEntry
                    {
                        Name = name ?? Path.GetFileNameWithoutExtension(fileName),
                        FileName = fileName,
                        FullPath = destPath,
                        Hash = hash,
                        Author = author ?? "Unknown",
                        Description = description ?? "",
                        AutoUpdate = autoUpdate,
                        Enabled = true
                    });
                }
                SaveDb(db);
                return "{\"ok\":true,\"path\":\"" + destPath.Replace("\\", "/") + "\"}";
            }
            catch (Exception ex)
            {
                return $"{{\"ok\":false,\"error\":\"{ex.Message.Replace("\"", "'")}\"}}";
            }
        }

        public static string UninstallPlugin(string id)
        {
            try
            {
                var db = LoadDb();
                var plugin = db.FirstOrDefault(p => p.Id == id);
                if (plugin == null) return "{\"ok\":false,\"error\":\"not found\"}";

                if (File.Exists(plugin.FullPath))
                {
                    var bak = plugin.FullPath + ".removed";
                    File.Move(plugin.FullPath, bak, true);
                }
                db.Remove(plugin);
                SaveDb(db);
                return "{\"ok\":true}";
            }
            catch (Exception ex)
            {
                return $"{{\"ok\":false,\"error\":\"{ex.Message.Replace("\"", "'")}\"}}";
            }
        }

        public static string TogglePlugin(string id)
        {
            try
            {
                var db = LoadDb();
                var plugin = db.FirstOrDefault(p => p.Id == id);
                if (plugin == null) return "{\"ok\":false,\"error\":\"not found\"}";
                plugin.Enabled = !plugin.Enabled;
                SaveDb(db);
                return $"{{\"ok\":true,\"enabled\":{plugin.Enabled.ToString().ToLower()}}}";
            }
            catch (Exception ex)
            {
                return $"{{\"ok\":false,\"error\":\"{ex.Message.Replace("\"", "'")}\"}}";
            }
        }

        public static string SetAutoUpdate(string id, bool value)
        {
            try
            {
                var db = LoadDb();
                var plugin = db.FirstOrDefault(p => p.Id == id);
                if (plugin == null) return "{\"ok\":false,\"error\":\"not found\"}";
                plugin.AutoUpdate = value;
                SaveDb(db);
                return "{\"ok\":true}";
            }
            catch (Exception ex)
            {
                return $"{{\"ok\":false,\"error\":\"{ex.Message.Replace("\"", "'")}\"}}";
            }
        }

        public static string ValidateAllPlugins()
        {
            var db = LoadDb();
            var results = new List<object>();
            int ok = 0, failed = 0;
            foreach (var p in db)
            {
                if (!File.Exists(p.FullPath))
                {
                    p.LastError = "File missing";
                    p.LastErrorAt = DateTime.Now;
                    failed++;
                    results.Add(new { id = p.Id, ok = false, error = "File missing" });
                }
                else
                {
                    var currentHash = ComputeFileHash(p.FullPath);
                    if (!string.Equals(p.Hash, currentHash, StringComparison.OrdinalIgnoreCase))
                    {
                        p.Hash = currentHash; // updated externally
                    }
                    ok++;
                    results.Add(new { id = p.Id, ok = true });
                }
            }
            SaveDb(db);
            return JsonConvert.SerializeObject(new { ok = true, valid = ok, failed, results });
        }

        public static string GetPluginDbJson()
        {
            return JsonConvert.SerializeObject(LoadDb());
        }

        private static string ComputeFileHash(string path)
        {
            using var sha = SHA256.Create();
            using var stream = File.OpenRead(path);
            return Convert.ToHexString(sha.ComputeHash(stream))[..16];
        }

        private static string BumpVersion(string v)
        {
            var parts = v.Split('.');
            if (parts.Length >= 3 && int.TryParse(parts[2], out var patch))
            {
                parts[2] = (patch + 1).ToString();
                return string.Join(".", parts);
            }
            return v;
        }
    }
}
