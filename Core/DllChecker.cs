using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Newtonsoft.Json;

namespace MatrixHole.Core
{
    /// <summary>
    /// DllChecker — проверка DLL перед инжектом (п.100 OptimizatorPlan).
    /// Проверяет: PE-заголовок, архитектуру, зависимости, подпись, сигнатуры.
    /// </summary>
    public static class DllChecker
    {
        [DllImport("imagehlp.dll", SetLastError = true)]
        private static extern bool MapAndLoad(string ImageName, string? DllPath, out LOADED_IMAGE LoadedImage, bool DotDll, bool ReadOnly);

        [DllImport("imagehlp.dll", SetLastError = true)]
        private static extern bool UnMapAndLoad(ref LOADED_IMAGE LoadedImage);

        [StructLayout(LayoutKind.Sequential)]
        private struct LOADED_IMAGE
        {
            public IntPtr ModuleName;
            public IntPtr hFile;
            public IntPtr MappedAddress;
            public IntPtr FileHeader;
            public IntPtr LastRvaSection;
            public uint NumberOfSections;
            public IntPtr Sections;
            public uint Characteristics;
            public ushort fSystemImage;
            public ushort fDOSImage;
            public ushort fReadOnly;
            public ushort Version;
            public IntPtr Links;
            public uint SizeOfImage;
        }

        public class DllCheckResult
        {
            public bool IsValidPe { get; set; }
            public bool IsDll { get; set; }
            public string Architecture { get; set; } = "Unknown";
            public bool Is64Bit { get; set; }
            public bool HasExports { get; set; }
            public int ExportCount { get; set; }
            public string[] Dependencies { get; set; } = Array.Empty<string>();
            public bool IsSigned { get; set; }
            public string? SignatureIssuer { get; set; }
            public string[] Warnings { get; set; } = Array.Empty<string>();
            public string[] Blockers { get; set; } = Array.Empty<string>();
            public long FileSize { get; set; }
            public string FileHash { get; set; } = "";
            public string? ProductName { get; set; }
            public string? FileVersion { get; set; }
            public string? OriginalFilename { get; set; }
        }

        public static string AnalyzeDll(string path)
        {
            try
            {
                if (!File.Exists(path))
                    return "{\"ok\":false,\"error\":\"file not found\"}";

                var result = new DllCheckResult();
                var warnings = new System.Collections.Generic.List<string>();
                var blockers = new System.Collections.Generic.List<string>();

                var data = File.ReadAllBytes(path);
                result.FileSize = data.Length;
                result.FileHash = ComputeHash(path);

                // DOS header check
                if (data.Length < 64 || data[0] != 0x4D || data[1] != 0x5A)
                {
                    blockers.Add("Invalid PE header (missing MZ signature)");
                    result.IsValidPe = false;
                    return JsonConvert.SerializeObject(new { ok = true, result });
                }
                result.IsValidPe = true;

                uint peOffset = BitConverter.ToUInt32(data, 0x3C);
                if (peOffset + 4 > data.Length || BitConverter.ToUInt32(data, (int)peOffset) != 0x00004550)
                {
                    blockers.Add("Invalid PE signature");
                    return JsonConvert.SerializeObject(new { ok = true, result });
                }

                ushort machine = BitConverter.ToUInt16(data, (int)peOffset + 4);
                result.Architecture = machine switch
                {
                    0x014c => "x86",
                    0x8664 => "x64",
                    0xAA64 => "ARM64",
                    _ => $"Unknown(0x{machine:X4})"
                };
                result.Is64Bit = machine == 0x8664;

                ushort characteristics = BitConverter.ToUInt16(data, (int)peOffset + 22);
                result.IsDll = (characteristics & 0x2000) != 0;
                if (!result.IsDll)
                    warnings.Add("File is not a DLL (may be an EXE)");

                // Optional header
                ushort optionalHeaderSize = BitConverter.ToUInt16(data, (int)peOffset + 20);
                uint optionalHeaderOffset = peOffset + 24;
                bool isPe32Plus = BitConverter.ToUInt16(data, (int)optionalHeaderOffset) == 0x20b;

                // Data directories
                uint exportDirOffset = isPe32Plus ? 0x88u : 0x78u; // relative to optional header
                uint exportRva = BitConverter.ToUInt32(data, (int)(optionalHeaderOffset + exportDirOffset));
                uint exportSize = BitConverter.ToUInt32(data, (int)(optionalHeaderOffset + exportDirOffset + 4));
                result.HasExports = exportRva != 0 && exportSize > 0;

                if (result.HasExports)
                {
                    result.ExportCount = CountExports(data, peOffset, optionalHeaderOffset, optionalHeaderSize, exportRva);
                }
                else
                {
                    warnings.Add("No export table found — may not be injectable");
                }

                // Import table dependencies
                uint importDirOffset = isPe32Plus ? 0x90u : 0x80u;
                uint importRva = BitConverter.ToUInt32(data, (int)(optionalHeaderOffset + importDirOffset));
                result.Dependencies = ReadImports(data, peOffset, optionalHeaderOffset, optionalHeaderSize, importRva);

                // Suspicious dependencies
                var suspicious = new[] { "kernel32.dll", "ntdll.dll", "wininet.dll", "ws2_32.dll" };
                foreach (var dep in result.Dependencies)
                {
                    if (suspicious.Any(s => dep.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0))
                        warnings.Add($"Uses low-level API: {dep}");
                }

                // File version info
                try
                {
                    var vi = System.Diagnostics.FileVersionInfo.GetVersionInfo(path);
                    result.ProductName = vi.ProductName;
                    result.FileVersion = vi.FileVersion;
                    result.OriginalFilename = vi.OriginalFilename;
                }
                catch { }

                // Signature check (basic)
                try
                {
                    var certOffset = BitConverter.ToUInt32(data, (int)(optionalHeaderOffset + (isPe32Plus ? 0xA8u : 0x98u)));
                    var certSize = BitConverter.ToUInt32(data, (int)(optionalHeaderOffset + (isPe32Plus ? 0xACu : 0x9Cu)));
                    result.IsSigned = certOffset != 0 && certSize > 0;
                }
                catch { }

                // Architecture mismatch warning
                var osArch = Environment.Is64BitProcess ? "x64" : "x86";
                if (result.Is64Bit != Environment.Is64BitProcess)
                    blockers.Add($"Architecture mismatch: DLL is {result.Architecture}, app is {osArch}");

                // File size sanity
                if (result.FileSize < 1024)
                    warnings.Add("Very small file — possible stub");
                if (result.FileSize > 50 * 1024 * 1024)
                    warnings.Add("Very large file — unusual for a plugin");

                result.Warnings = warnings.ToArray();
                result.Blockers = blockers.ToArray();

                return JsonConvert.SerializeObject(new { ok = true, result });
            }
            catch (Exception ex)
            {
                return $"{{\"ok\":false,\"error\":\"{ex.Message.Replace("\"", "'")}\"}}";
            }
        }

        public static string QuickCheck(string path)
        {
            var full = AnalyzeDll(path);
            try
            {
                var obj = JsonConvert.DeserializeObject<dynamic>(full);
                if (obj?.ok != true) return full;
                var r = obj.result;
                bool canInject = r.IsValidPe == true && r.IsDll == true && (r.Blockers == null || r.Blockers.Count == 0);
                return JsonConvert.SerializeObject(new
                {
                    ok = true,
                    canInject,
                    arch = (string)r.Architecture,
                    is64 = (bool)r.Is64Bit,
                    hasExports = (bool)r.HasExports,
                    warnings = r.Warnings,
                    blockers = r.Blockers
                });
            }
            catch { return full; }
        }

        private static int CountExports(byte[] data, uint peOffset, uint optionalHeaderOffset, ushort optHeaderSize, uint exportRva)
        {
            try
            {
                uint sectionTableOffset = optionalHeaderOffset + optHeaderSize;
                for (int i = 0; i < BitConverter.ToUInt16(data, (int)peOffset + 6); i++)
                {
                    uint secOffset = sectionTableOffset + (uint)(i * 40);
                    uint secVirtAddr = BitConverter.ToUInt32(data, (int)(secOffset + 12));
                    uint secVirtSize = BitConverter.ToUInt32(data, (int)(secOffset + 8));
                    uint secRawAddr = BitConverter.ToUInt32(data, (int)(secOffset + 20));

                    if (exportRva >= secVirtAddr && exportRva < secVirtAddr + secVirtSize)
                    {
                        uint fileOffset = exportRva - secVirtAddr + secRawAddr;
                        if (fileOffset + 24 > data.Length) break;
                        uint nameCount = BitConverter.ToUInt32(data, (int)(fileOffset + 24));
                        return (int)nameCount;
                    }
                }
            }
            catch { }
            return 0;
        }

        private static string[] ReadImports(byte[] data, uint peOffset, uint optionalHeaderOffset, ushort optHeaderSize, uint importRva)
        {
            var deps = new System.Collections.Generic.List<string>();
            if (importRva == 0) return deps.ToArray();
            try
            {
                uint sectionTableOffset = optionalHeaderOffset + optHeaderSize;
                for (int i = 0; i < BitConverter.ToUInt16(data, (int)peOffset + 6); i++)
                {
                    uint secOffset = sectionTableOffset + (uint)(i * 40);
                    uint secVirtAddr = BitConverter.ToUInt32(data, (int)(secOffset + 12));
                    uint secVirtSize = BitConverter.ToUInt32(data, (int)(secOffset + 8));
                    uint secRawAddr = BitConverter.ToUInt32(data, (int)(secOffset + 20));

                    if (importRva >= secVirtAddr && importRva < secVirtAddr + secVirtSize)
                    {
                        uint fileOffset = importRva - secVirtAddr + secRawAddr;
                        int idx = 0;
                        while (true)
                        {
                            uint descOffset = (uint)(fileOffset + idx * 20);
                            if (descOffset + 12 > data.Length) break;
                            uint nameRva = BitConverter.ToUInt32(data, (int)(descOffset + 12));
                            if (nameRva == 0) break;
                            uint nameFileOff = nameRva - secVirtAddr + secRawAddr;
                            if (nameFileOff >= data.Length) break;
                            int len = 0;
                            while (nameFileOff + len < data.Length && data[nameFileOff + len] != 0 && len < 256) len++;
                            var name = Encoding.ASCII.GetString(data, (int)nameFileOff, len);
                            if (!string.IsNullOrEmpty(name)) deps.Add(name);
                            idx++;
                        }
                        break;
                    }
                }
            }
            catch { }
            return deps.ToArray();
        }

        private static string ComputeHash(string path)
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            using var stream = File.OpenRead(path);
            return Convert.ToHexString(sha.ComputeHash(stream))[..16];
        }
    }
}
