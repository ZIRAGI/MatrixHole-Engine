using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace MatrixHole.Core
{
    public static class UpdaterLauncher
    {
        private const string UpdaterExeName = "MatrixHole.Updater.exe";
        private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };

        public static bool IsUpdatePending()
        {
            var pendingDir = Path.Combine(Path.GetTempPath(), "MatrixHole_Update");
            return Directory.Exists(pendingDir) && Directory.GetFiles(pendingDir, "*.dll").Length > 0;
        }

        public static async Task<string?> CheckForUpdateAsync(string currentVersion)
        {
            try
            {
                // GitHub releases API
                var url = $"https://api.github.com/repos/{AppSecrets.GitHubOwner}/{AppSecrets.GitHubRepo}/releases/latest";
                _http.DefaultRequestHeaders.UserAgent.ParseAdd("MatrixHole-Updater/1.0");
                var response = await _http.GetStringAsync(url);
                var json = JObject.Parse(response);
                var latest = json["tag_name"]?.ToString()?.TrimStart('v') ?? "";
                if (string.IsNullOrEmpty(latest)) return null;
                if (Version.Parse(latest) > Version.Parse(currentVersion))
                    return latest;
                return null;
            }
            catch { return null; }
        }

        public static async Task<bool> DownloadUpdateAsync(string version)
        {
            try
            {
                var pendingDir = Path.Combine(Path.GetTempPath(), "MatrixHole_Update");
                if (Directory.Exists(pendingDir)) Directory.Delete(pendingDir, true);
                Directory.CreateDirectory(pendingDir);

                var zipUrl = $"https://github.com/{AppSecrets.GitHubOwner}/{AppSecrets.GitHubRepo}/releases/download/v{version}/MatrixHole-v{version}.zip";
                var zipPath = Path.Combine(pendingDir, "update.zip");

                using var response = await _http.GetAsync(zipUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();
                await using var fs = new FileStream(zipPath, FileMode.Create, FileAccess.Write);
                await response.Content.CopyToAsync(fs);
                fs.Close();

                ZipFile.ExtractToDirectory(zipPath, pendingDir);
                File.Delete(zipPath);
                return true;
            }
            catch { return false; }
        }

        public static string? PrepareUpdatePackage(string updateZipUrl)
        {
            try
            {
                var pendingDir = Path.Combine(Path.GetTempPath(), "MatrixHole_Update");
                Directory.CreateDirectory(pendingDir);
                return pendingDir;
            }
            catch { return null; }
        }

        public static bool LaunchUpdaterAndExit()
        {
            try
            {
                var currentExe = Process.GetCurrentProcess().MainModule?.FileName;
                if (string.IsNullOrEmpty(currentExe)) return false;

                var appDir = Path.GetDirectoryName(currentExe)!;
                var updaterPath = Path.Combine(appDir, UpdaterExeName);

                if (!File.Exists(updaterPath))
                {
                    updaterPath = CreateBatchUpdater(appDir, currentExe);
                }

                var pendingDir = Path.Combine(Path.GetTempPath(), "MatrixHole_Update");

                var psi = new ProcessStartInfo
                {
                    FileName = updaterPath,
                    Arguments = $"\"{currentExe}\" \"{pendingDir}\" {Process.GetCurrentProcess().Id}",
                    UseShellExecute = true,
                    Verb = "runas",
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                Process.Start(psi);
                return true;
            }
            catch { return false; }
        }

        public static async Task<bool> WaitForGameExitThenUpdate(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                var scpsl = Process.GetProcessesByName("SCPSL");
                if (scpsl.Length == 0) break;
                await Task.Delay(5000, token);
            }
            return LaunchUpdaterAndExit();
        }

        private static string CreateBatchUpdater(string appDir, string currentExe)
        {
            var batchPath = Path.Combine(Path.GetTempPath(), "sst_updater.bat");
            var pendingDir = Path.Combine(Path.GetTempPath(), "MatrixHole_Update");

            var batch = $@"
@echo off
setlocal
set TARGET_EXE=""{currentExe}""
set PENDING_DIR=""{pendingDir}""
set PID={Process.GetCurrentProcess().Id}

echo Waiting for process %PID% to exit...
:waitloop
tasklist | findstr ""%PID%"" >nul
if %errorlevel% == 0 (
    timeout /t 1 /nobreak >nul
    goto waitloop
)

timeout /t 2 /nobreak >nul

if exist %PENDING_DIR%\*.dll (
    xcopy /Y /E %PENDING_DIR%\* ""{appDir}\"" >nul 2>&1
    rmdir /S /Q %PENDING_DIR%
)

start "" %TARGET_EXE%
del ""%~f0"" >nul 2>&1
";

            File.WriteAllText(batchPath, batch);
            return batchPath;
        }
    }
}
