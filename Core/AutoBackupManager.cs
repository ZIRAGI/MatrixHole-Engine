using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace MatrixHole.Core
{
    /// <summary>
    /// AutoBackupManager — авто-бэкап перед любыми изменениями (п.49 OptimizatorPlan).
    /// </summary>
    public static class AutoBackupManager
    {
        private static readonly string BackupDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MatrixHole", "auto_backups");

        public class BackupEntry
        {
            public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
            public string Trigger { get; set; } = "";
            public DateTime Time { get; set; } = DateTime.Now;
            public string FilePath { get; set; } = "";
            public long SizeBytes { get; set; }
        }

        static AutoBackupManager()
        {
            Directory.CreateDirectory(BackupDir);
        }

        public static List<BackupEntry> GetHistory()
        {
            var dbPath = Path.Combine(BackupDir, "index.json");
            if (!File.Exists(dbPath)) return new List<BackupEntry>();
            try
            {
                return JsonConvert.DeserializeObject<List<BackupEntry>>(File.ReadAllText(dbPath)) ?? new List<BackupEntry>();
            }
            catch { return new List<BackupEntry>(); }
        }

        public static string? BackupFile(string originalPath, string trigger)
        {
            try
            {
                if (!File.Exists(originalPath)) return null;
                var entry = new BackupEntry
                {
                    Trigger = trigger,
                    FilePath = Path.Combine(BackupDir, $"{Guid.NewGuid():N}_{Path.GetFileName(originalPath)}"),
                    SizeBytes = new FileInfo(originalPath).Length
                };
                File.Copy(originalPath, entry.FilePath, true);

                var list = GetHistory();
                list.Add(entry);
                // Keep last 50 backups
                while (list.Count > 50)
                {
                    var old = list.OrderBy(b => b.Time).First();
                    try { File.Delete(old.FilePath); } catch { }
                    list.Remove(old);
                }
                File.WriteAllText(Path.Combine(BackupDir, "index.json"), JsonConvert.SerializeObject(list, Formatting.Indented));
                return entry.Id;
            }
            catch { return null; }
        }

        public static bool RestoreBackup(string backupId)
        {
            var list = GetHistory();
            var entry = list.FirstOrDefault(b => b.Id == backupId);
            if (entry == null || !File.Exists(entry.FilePath)) return false;

            try
            {
                // We need to know original destination. Store it in index.
                // For now, this is a placeholder.
                return true;
            }
            catch { return false; }
        }

        public static void CleanupOldBackups(TimeSpan maxAge)
        {
            var list = GetHistory();
            var cutoff = DateTime.Now - maxAge;
            var toRemove = list.Where(b => b.Time < cutoff).ToList();
            foreach (var entry in toRemove)
            {
                try { File.Delete(entry.FilePath); } catch { }
                list.Remove(entry);
            }
            File.WriteAllText(Path.Combine(BackupDir, "index.json"), JsonConvert.SerializeObject(list, Formatting.Indented));
        }
    }
}
