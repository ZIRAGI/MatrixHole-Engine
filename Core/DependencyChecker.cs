using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Win32;
using Newtonsoft.Json;

namespace MatrixHole.Core
{
    /// <summary>
    /// DependencyChecker — проверка .NET 6/8, VC++ Redist и т.д. (п.92 OptimizatorPlan).
    /// </summary>
    public static class DependencyChecker
    {
        public class Dependency
        {
            public string Name { get; set; } = "";
            public string MinimumVersion { get; set; } = "";
            public bool IsInstalled { get; set; }
            public string? InstalledVersion { get; set; }
            public string? DownloadUrl { get; set; }
        }

        public static List<Dependency> CheckAll()
        {
            return new List<Dependency>
            {
                CheckDotNet8(),
                CheckVcRedist(),
                CheckWebView2(),
                CheckDirectX()
            };
        }

        public static Dependency CheckDotNet8()
        {
            var result = new Dependency
            {
                Name = ".NET 8 Runtime",
                MinimumVersion = "8.0.0",
                DownloadUrl = "https://dotnet.microsoft.com/download/dotnet/8.0"
            };
            try
            {
                var proc = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "dotnet",
                        Arguments = "--list-runtimes",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                proc.Start();
                var output = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit();

                result.IsInstalled = output.Contains("Microsoft.NETCore.App 8.");
                var line = output.Split('\n').FirstOrDefault(l => l.Contains("Microsoft.NETCore.App 8."));
                result.InstalledVersion = line?.Split(' ')[1]?.Trim('[', ']');
            }
            catch
            {
                result.IsInstalled = false;
            }
            return result;
        }

        public static Dependency CheckVcRedist()
        {
            var result = new Dependency
            {
                Name = "Visual C++ Redistributable 2015-2022",
                MinimumVersion = "14.30",
                DownloadUrl = "https://aka.ms/vs/17/release/vc_redist.x64.exe"
            };
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64");
                if (key != null)
                {
                    var version = key.GetValue("Version")?.ToString();
                    result.InstalledVersion = version;
                    result.IsInstalled = version != null;
                }
            }
            catch { result.IsInstalled = false; }
            return result;
        }

        public static Dependency CheckWebView2()
        {
            var result = new Dependency
            {
                Name = "Microsoft Edge WebView2 Runtime",
                MinimumVersion = "109.0",
                DownloadUrl = "https://developer.microsoft.com/microsoft-edge/webview2/"
            };
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}");
                var version = key?.GetValue("pv")?.ToString();
                result.InstalledVersion = version;
                result.IsInstalled = !string.IsNullOrEmpty(version) && version != "0.0.0.0";
            }
            catch { result.IsInstalled = false; }
            return result;
        }

        public static Dependency CheckDirectX()
        {
            var result = new Dependency
            {
                Name = "DirectX Runtime",
                MinimumVersion = "9.29",
                DownloadUrl = "https://www.microsoft.com/download/details.aspx?id=35"
            };
            try
            {
                var dxPath = Path.Combine(Environment.SystemDirectory, "dxdiag.exe");
                result.IsInstalled = File.Exists(dxPath);
                result.InstalledVersion = "System built-in";
            }
            catch { result.IsInstalled = false; }
            return result;
        }

        public static string GetStatusJson()
        {
            var deps = CheckAll();
            return JsonConvert.SerializeObject(new
            {
                allOk = deps.All(d => d.IsInstalled),
                missing = deps.Where(d => !d.IsInstalled).Select(d => new { d.Name, d.DownloadUrl }),
                dependencies = deps
            });
        }
    }
}
