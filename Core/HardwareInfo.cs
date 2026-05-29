using System;
using System.Linq;
using System.Management;
using Newtonsoft.Json;

namespace MatrixHole.Core
{
    /// <summary>
    /// HardwareInfo — WMI-информация о железе (п.66 OptimizatorPlan).
    /// CPU, GPU, RAM, Motherboard, Disk, OS, Monitor.
    /// </summary>
    public static class HardwareInfo
    {
        public static string GetFullReport()
        {
            try
            {
                var report = new
                {
                    cpu = GetCpuInfo(),
                    gpu = GetGpuInfo(),
                    ram = GetRamInfo(),
                    motherboard = GetMotherboardInfo(),
                    disks = GetDiskInfo(),
                    os = GetOsInfo(),
                    network = GetNetworkInfo(),
                    timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                };
                return JsonConvert.SerializeObject(report, Formatting.Indented);
            }
            catch (Exception ex)
            {
                return $"{{\"error\":\"{ex.Message.Replace("\"", "'")}\"}}";
            }
        }

        public static string GetSummary()
        {
            try
            {
                dynamic cpu = GetCpuInfo();
                dynamic gpu = GetGpuInfo();
                dynamic ram = GetRamInfo();
                dynamic os = GetOsInfo();
                return JsonConvert.SerializeObject(new
                {
                    cpu_name = cpu.Name,
                    cpu_cores = cpu.Cores,
                    cpu_threads = cpu.Threads,
                    cpu_clock = cpu.MaxClockMHz,
                    gpu_name = gpu.Name,
                    gpu_vram_mb = gpu.AdapterRAM_MB,
                    ram_total_gb = ram.TotalGB,
                    ram_speed = ram.SpeedMHz,
                    os = os.Name
                });
            }
            catch (Exception ex)
            {
                return $"{{\"error\":\"{ex.Message.Replace("\"", "'")}\"}}";
            }
        }

        private static object GetCpuInfo()
        {
            try
            {
                using var mc = new ManagementClass("win32_processor");
                foreach (var mo in mc.GetInstances())
                {
                    return new
                    {
                        Name = mo["Name"]?.ToString()?.Trim() ?? "Unknown",
                        Manufacturer = mo["Manufacturer"]?.ToString() ?? "Unknown",
                        Cores = int.TryParse(mo["NumberOfCores"]?.ToString(), out var c) ? c : 0,
                        Threads = int.TryParse(mo["NumberOfLogicalProcessors"]?.ToString(), out var t) ? t : 0,
                        MaxClockMHz = int.TryParse(mo["MaxClockSpeed"]?.ToString(), out var m) ? m : 0,
                        Architecture = mo["Architecture"]?.ToString() ?? "Unknown",
                        Socket = mo["SocketDesignation"]?.ToString() ?? "Unknown"
                    };
                }
            }
            catch { }
            return new { Name = "Unknown", Manufacturer = "Unknown", Cores = 0, Threads = 0, MaxClockMHz = 0, Architecture = "Unknown", Socket = "Unknown" };
        }

        private static object GetGpuInfo()
        {
            try
            {
                using var mc = new ManagementClass("Win32_VideoController");
                foreach (var mo in mc.GetInstances())
                {
                    var vramRaw = mo["AdapterRAM"];
                    long vram = 0;
                    if (vramRaw != null)
                    {
                        try { vram = Convert.ToInt64(vramRaw) / (1024 * 1024); }
                        catch { }
                    }
                    return new
                    {
                        Name = mo["Name"]?.ToString()?.Trim() ?? "Unknown",
                        AdapterRAM_MB = vram,
                        VideoMode = mo["VideoModeDescription"]?.ToString() ?? "Unknown",
                        DriverVersion = mo["DriverVersion"]?.ToString() ?? "Unknown",
                        Status = mo["Status"]?.ToString() ?? "Unknown"
                    };
                }
            }
            catch { }
            return new { Name = "Unknown", AdapterRAM_MB = 0L, VideoMode = "Unknown", DriverVersion = "Unknown", Status = "Unknown" };
        }

        private static object GetRamInfo()
        {
            try
            {
                long total = 0;
                int speed = 0;
                using var mc = new ManagementClass("Win32_PhysicalMemory");
                foreach (var mo in mc.GetInstances())
                {
                    total += long.TryParse(mo["Capacity"]?.ToString(), out var cap) ? cap : 0;
                    if (speed == 0) speed = int.TryParse(mo["Speed"]?.ToString(), out var s) ? s : 0;
                }
                return new
                {
                    TotalGB = Math.Round(total / (1024.0 * 1024 * 1024), 2),
                    SpeedMHz = speed,
                    Slots = mc.GetInstances().Count
                };
            }
            catch { }
            return new { TotalGB = 0.0, SpeedMHz = 0, Slots = 0 };
        }

        private static object GetMotherboardInfo()
        {
            try
            {
                using var mc = new ManagementClass("Win32_BaseBoard");
                foreach (var mo in mc.GetInstances())
                {
                    return new
                    {
                        Manufacturer = mo["Manufacturer"]?.ToString() ?? "Unknown",
                        Product = mo["Product"]?.ToString() ?? "Unknown",
                        Model = mo["Model"]?.ToString() ?? "Unknown"
                    };
                }
            }
            catch { }
            return new { Manufacturer = "Unknown", Product = "Unknown", Model = "Unknown" };
        }

        private static object[] GetDiskInfo()
        {
            try
            {
                using var mc = new ManagementClass("Win32_DiskDrive");
                return mc.GetInstances().Cast<ManagementObject>().Select(mo => new
                {
                    Model = mo["Model"]?.ToString()?.Trim() ?? "Unknown",
                    SizeGB = long.TryParse(mo["Size"]?.ToString(), out var sz) ? Math.Round(sz / (1024.0 * 1024 * 1024), 0) : 0,
                    InterfaceType = mo["InterfaceType"]?.ToString() ?? "Unknown",
                    MediaType = mo["MediaType"]?.ToString() ?? "Unknown"
                }).ToArray();
            }
            catch { }
            return Array.Empty<object>();
        }

        private static object GetOsInfo()
        {
            try
            {
                using var mc = new ManagementClass("Win32_OperatingSystem");
                foreach (var mo in mc.GetInstances())
                {
                    return new
                    {
                        Name = mo["Caption"]?.ToString()?.Trim() ?? "Unknown",
                        Version = mo["Version"]?.ToString() ?? "Unknown",
                        Architecture = mo["OSArchitecture"]?.ToString() ?? "Unknown",
                        InstallDate = mo["InstallDate"]?.ToString() ?? "Unknown",
                        TotalVisibleMemoryGB = long.TryParse(mo["TotalVisibleMemorySize"]?.ToString(), out var mem) ? Math.Round(mem / (1024.0 * 1024), 2) : 0
                    };
                }
            }
            catch { }
            return new { Name = "Unknown", Version = "Unknown", Architecture = "Unknown", InstallDate = "Unknown", TotalVisibleMemoryGB = 0.0 };
        }

        private static object GetNetworkInfo()
        {
            try
            {
                using var mc = new ManagementClass("Win32_NetworkAdapter");
                var adapters = mc.GetInstances().Cast<ManagementObject>()
                    .Where(mo => mo["NetConnectionStatus"] != null && (ushort)mo["NetConnectionStatus"] == 2)
                    .Select(mo => new
                    {
                        Name = mo["Name"]?.ToString() ?? "Unknown",
                        SpeedMbps = long.TryParse(mo["Speed"]?.ToString(), out var sp) ? sp / 1000000 : 0,
                        MacAddress = mo["MACAddress"]?.ToString() ?? "Unknown"
                    }).ToArray();
                return new { Adapters = adapters };
            }
            catch { }
            return new { Adapters = Array.Empty<object>() };
        }
    }
}
