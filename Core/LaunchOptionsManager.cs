using System.Diagnostics;
using System.IO;

namespace MatrixHole.Core
{
    public static class LaunchOptionsManager
    {
        public static string BuildOptions(bool dx11, bool fastest, bool borderless, bool noGpuSkinning)
        {
            var opts = "";
            if (dx11) opts += " -force-d3d11";
            if (fastest) opts += " -screen-quality Fastest";
            if (borderless) opts += " -screen-fullscreen 0 -popupwindow";
            if (noGpuSkinning) opts += " -disable-gpu-skinning";
            return opts.Trim();
        }

        public static void LaunchGame()
        {
            var steamExe = Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFilesX86),
                "Steam", "Steam.exe");
            if (!File.Exists(steamExe))
            {
                steamExe = Path.Combine(
                    System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFiles),
                    "Steam", "Steam.exe");
            }
            if (File.Exists(steamExe))
                Process.Start(steamExe, $"-applaunch {GameDetector.SteamAppId}");
        }
    }
}
