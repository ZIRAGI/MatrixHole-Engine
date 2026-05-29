using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace MatrixHole.Core
{
    /// <summary>
    /// CustomContentManager — кастомные тексты и звуки SCP:SL (п.1 OptimizatorPlan).
    /// Замена ассетов через AssetBundle или файловую подмену.
    /// </summary>
    public static class CustomContentManager
    {
        private static readonly string ContentDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MatrixHole", "custom_content");

        public class CustomAsset
        {
            public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
            public string Name { get; set; } = "";
            public AssetType Type { get; set; }
            public string OriginalPath { get; set; } = "";
            public string CustomPath { get; set; } = "";
            public bool IsEnabled { get; set; } = false;
            public DateTime AddedAt { get; set; } = DateTime.Now;
        }

        public enum AssetType { Text, Sound, Texture, Other }

        static CustomContentManager()
        {
            Directory.CreateDirectory(ContentDir);
        }

        public static List<CustomAsset> GetLibrary()
        {
            var list = new List<CustomAsset>();
            var dbPath = Path.Combine(ContentDir, "library.json");
            if (File.Exists(dbPath))
            {
                try
                {
                    list = JsonConvert.DeserializeObject<List<CustomAsset>>(File.ReadAllText(dbPath)) ?? list;
                }
                catch { }
            }
            return list;
        }

        public static void SaveLibrary(List<CustomAsset> assets)
        {
            var dbPath = Path.Combine(ContentDir, "library.json");
            File.WriteAllText(dbPath, JsonConvert.SerializeObject(assets, Formatting.Indented));
        }

        public static bool AddAsset(CustomAsset asset)
        {
            try
            {
                var list = GetLibrary();
                list.Add(asset);
                SaveLibrary(list);
                return true;
            }
            catch { return false; }
        }

        public static bool ToggleAsset(string id)
        {
            var list = GetLibrary();
            var asset = list.FirstOrDefault(a => a.Id == id);
            if (asset == null) return false;

            asset.IsEnabled = !asset.IsEnabled;
            if (asset.IsEnabled)
                ApplyAsset(asset);
            else
                RestoreAsset(asset);

            SaveLibrary(list);
            return true;
        }

        public static void ApplyAsset(CustomAsset asset)
        {
            if (!File.Exists(asset.CustomPath)) return;
            try
            {
                // Backup original if not backed up
                var backupPath = Path.Combine(ContentDir, "backups", Path.GetFileName(asset.OriginalPath));
                Directory.CreateDirectory(Path.GetDirectoryName(backupPath)!);
                if (File.Exists(asset.OriginalPath) && !File.Exists(backupPath))
                    File.Copy(asset.OriginalPath, backupPath, true);

                File.Copy(asset.CustomPath, asset.OriginalPath, true);
            }
            catch { }
        }

        public static void RestoreAsset(CustomAsset asset)
        {
            var backupPath = Path.Combine(ContentDir, "backups", Path.GetFileName(asset.OriginalPath));
            if (File.Exists(backupPath))
            {
                try { File.Copy(backupPath, asset.OriginalPath, true); }
                catch { }
            }
        }

        public static void RestoreAll()
        {
            foreach (var asset in GetLibrary().Where(a => a.IsEnabled))
            {
                RestoreAsset(asset);
            }
        }
    }
}
