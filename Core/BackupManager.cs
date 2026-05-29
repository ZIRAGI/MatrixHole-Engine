using System;
using System.IO;
using System.Linq;

namespace MatrixHole.Core
{
    public static class BackupManager
    {
        private static string BackupDir => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MatrixHole", "Backups");

        public static string CreateBackup()
        {
            var game = GameDetector.Detect();
            if (game == null || !game.IsInstalled) return "";

            Directory.CreateDirectory(BackupDir);
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            var backupPath = Path.Combine(BackupDir, $"config_backup_{timestamp}.ini");
            var configPath = Path.Combine(game.InstallPath, "config.ini");

            try
            {
                if (File.Exists(configPath))
                    File.Copy(configPath, backupPath, true);
                else
                    File.WriteAllText(backupPath, "# empty backup - no config found");
                return backupPath;
            }
            catch { return ""; }
        }

        public static bool RestoreLatest()
        {
            if (!Directory.Exists(BackupDir)) return false;
            var latest = Directory.GetFiles(BackupDir, "config_backup_*.ini")
                .OrderByDescending(f => new FileInfo(f).LastWriteTime)
                .FirstOrDefault();
            if (latest == null) return false;

            var game = GameDetector.Detect();
            if (game == null || !game.IsInstalled) return false;
            try
            {
                File.Copy(latest, Path.Combine(game.InstallPath, "config.ini"), true);
                return true;
            }
            catch { return false; }
        }

        public static string[] ListBackups()
        {
            if (!Directory.Exists(BackupDir)) return Array.Empty<string>();
            return Directory.GetFiles(BackupDir, "config_backup_*.ini")
                .Select(Path.GetFileName)
                .ToArray()!;
        }
    }
}
