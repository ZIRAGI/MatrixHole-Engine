using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;

namespace MatrixHole.Core
{
    /// <summary>
    /// Checks for updates via GitHub Releases API.
    /// Just create a release on GitHub and upload MatrixHole-Engine-Setup.exe as an asset.
    /// </summary>
    public static class UpdateChecker
    {
        private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };

        private static string GitHubOwner => AppSecrets.GitHubOwner;
        private static string GitHubRepo => AppSecrets.GitHubRepo;
        private const string AssetName = "MatrixHole-Engine-Setup.exe";

        private static string ReleasesApiUrl => $"https://api.github.com/repos/{GitHubOwner}/{GitHubRepo}/releases/latest";

        public class UpdateInfo
        {
            public string Version { get; set; } = "";
            public string DownloadUrl { get; set; } = "";
            public string ReleaseNotes { get; set; } = "";
            public bool Mandatory { get; set; } = false;
        }

        public static async Task<UpdateInfo?> CheckAsync()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(GitHubOwner) || string.IsNullOrWhiteSpace(GitHubRepo))
                {
                    DevLogger.Log("UpdateChecker", "Skip", "GitHubOwner/GitHubRepo not configured in app_secrets.json");
                    return null;
                }

                var current = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0);

                _http.DefaultRequestHeaders.UserAgent.ParseAdd("MatrixHole-Engine-UpdateChecker");
                var json = await _http.GetStringAsync(ReleasesApiUrl);
                var release = JsonConvert.DeserializeObject<GitHubRelease>(json);
                if (release == null || string.IsNullOrWhiteSpace(release.TagName)) return null;

                // Parse version from tag (e.g. "v1.2.3" -> "1.2.3")
                var tag = release.TagName.TrimStart('v', 'V');
                if (!System.Version.TryParse(tag, out var latest)) return null;

                if (latest <= current) return null;

                // Find the setup asset
                var asset = release.Assets?.FirstOrDefault(a => a.Name.Equals(AssetName, StringComparison.OrdinalIgnoreCase));
                if (asset == null) return null;

                return new UpdateInfo
                {
                    Version = tag,
                    DownloadUrl = asset.BrowserDownloadUrl,
                    ReleaseNotes = release.Body ?? "",
                    Mandatory = false
                };
            }
            catch { return null; }
        }

        public static void DownloadAndInstall(string downloadUrl)
        {
            try
            {
                var tempPath = Path.Combine(Path.GetTempPath(), $"MatrixHole-Update_{Guid.NewGuid():N}.exe");
                Task.Run(async () =>
                {
                    var fileBytes = await _http.GetByteArrayAsync(downloadUrl);
                    await File.WriteAllBytesAsync(tempPath, fileBytes);
                }).GetAwaiter().GetResult();

                var psi = new ProcessStartInfo
                {
                    FileName = tempPath,
                    Arguments = "/SILENT /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS",
                    UseShellExecute = true
                };
                Process.Start(psi);
                System.Windows.Application.Current?.Dispatcher.BeginInvoke(() => System.Windows.Application.Current.Shutdown());
            }
            catch (Exception ex)
            {
                DevLogger.Error("UpdateChecker", ex);
            }
        }

        // GitHub API response models
        private class GitHubRelease
        {
            [JsonProperty("tag_name")]
            public string TagName { get; set; } = "";

            [JsonProperty("body")]
            public string Body { get; set; } = "";

            [JsonProperty("assets")]
            public GitHubAsset[]? Assets { get; set; }
        }

        private class GitHubAsset
        {
            [JsonProperty("name")]
            public string Name { get; set; } = "";

            [JsonProperty("browser_download_url")]
            public string BrowserDownloadUrl { get; set; } = "";
        }
    }
}
