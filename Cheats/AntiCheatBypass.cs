using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace MatrixHole.Cheats
{
    /// <summary>
    /// Handles SL-AC.dll evasion.
    /// 
    /// METHOD A — File removal + dummy replacement:
    ///   Backs up the original SL-AC.dll, deletes it from the game folder,
    ///   and writes a minimal dummy PE in its place. This prevents the game
    ///   from crashing if it blindly calls LoadLibrary("SL-AC.dll").
    ///   Most basic Unity anti-cheats will simply fail to initialize.
    /// 
    /// METHOD B — Export analysis (diagnostic):
    ///   Parses the real SL-AC.dll PE header and dumps all exported function
    ///   names to a text file. This lets advanced users build a proper dummy
    ///   that re-exports every symbol (preventing GetProcAddress crashes).
    /// 
    /// NOTE: If SL-AC uses a kernel driver or secondary integrity checks
    /// (file hash verification against Steam CDN), deletion will be detected
    /// and the game will refuse to start or ban on next server connect.
    /// Method A is best-effort for user-mode anti-cheats only.
    /// </summary>
    public class AntiCheatBypass
    {
        private readonly string _gamePath;
        private readonly string _acDllName;
        private readonly string _acBackupName;

        public AntiCheatBypass(string gamePath)
        {
            _gamePath = gamePath;
            _acDllName = Path.Combine(gamePath, "SL-AC.dll");
            _acBackupName = Path.Combine(gamePath, "SL-AC.dll.bak");
        }

        // ============================================================
        // STATUS
        // ============================================================

        public string GetStatus()
        {
            bool originalExists = File.Exists(_acDllName);
            bool backupExists = File.Exists(_acBackupName);
            bool bypassed = !originalExists && backupExists;
            return $"{{\"bypassed\":{bypassed.ToString().ToLower()},\"original_exists\":{originalExists.ToString().ToLower()},\"backup_exists\":{backupExists.ToString().ToLower()}}}";
        }

        // ============================================================
        // METHOD A — FILE REPLACEMENT
        // ============================================================

        public string PatchSlAcOnDisk()
        {
            try
            {
                if (!File.Exists(_acDllName))
                {
                    if (File.Exists(_acBackupName))
                        return "{\"success\":true,\"method\":\"already_bypassed\"}";
                    return "{\"error\":\"SL-AC.dll not found\"}";
                }

                // Ensure game is closed
                var procs = System.Diagnostics.Process.GetProcessesByName("SCPSL");
                if (procs.Length > 0)
                {
                    foreach (var p in procs) { try { p.Dispose(); } catch { } }
                    return "{\"error\":\"Close the game first\"}";
                }

                // Backup original
                File.Copy(_acDllName, _acBackupName, true);

                // Delete original
                File.Delete(_acDllName);

                // Write dummy DLL
                var dummy = BuildMinimalPeDll();
                File.WriteAllBytes(_acDllName, dummy);

                return "{\"success\":true,\"method\":\"replace_with_dummy\"}";
            }
            catch (Exception ex)
            {
                return $"{{\"error\":\"{ex.Message.Replace("\"","'")}\"}}";
            }
        }

        public string RestoreOriginalDll()
        {
            try
            {
                var procs = System.Diagnostics.Process.GetProcessesByName("SCPSL");
                if (procs.Length > 0)
                {
                    foreach (var p in procs) { try { p.Dispose(); } catch { } }
                    return "{\"error\":\"Close the game first\"}";
                }

                if (File.Exists(_acBackupName))
                {
                    if (File.Exists(_acDllName))
                        File.Delete(_acDllName);
                    File.Move(_acBackupName, _acDllName);
                    return "{\"success\":true,\"method\":\"restored_original\"}";
                }
                return "{\"error\":\"Backup not found\"}";
            }
            catch (Exception ex)
            {
                return $"{{\"error\":\"{ex.Message.Replace("\"","'")}\"}}";
            }
        }

        // ============================================================
        // METHOD B — EXPORT DUMP (diagnostic)
        // ============================================================

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr LoadLibraryEx(string lpFileName, IntPtr hFile, uint dwFlags);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FreeLibrary(IntPtr hModule);

        [DllImport("imagehlp.dll", SetLastError = true)]
        private static extern bool MapAndLoad(string ImageName, string DllPath, out LOADED_IMAGE LoadedImage, bool DotDll, bool ReadOnly);

        [DllImport("imagehlp.dll", SetLastError = true)]
        private static extern bool UnMapAndLoad(ref LOADED_IMAGE LoadedImage);

        private const uint DONT_RESOLVE_DLL_REFERENCES = 0x00000001;

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

        public string DumpExports()
        {
            try
            {
                if (!File.Exists(_acDllName))
                    return "{\"error\":\"SL-AC.dll not found\"}";

                // Try to load and dump exports using ImageHlp
                var sb = new StringBuilder();
                sb.AppendLine("; SL-AC.dll Export Dump");
                sb.AppendLine($"; Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine();

                IntPtr hMod = LoadLibraryEx(_acDllName, IntPtr.Zero, DONT_RESOLVE_DLL_REFERENCES);
                if (hMod == IntPtr.Zero)
                {
                    // Fallback: read PE headers manually to get export table RVA
                    var exports = ReadExportsManually(_acDllName);
                    foreach (var e in exports)
                        sb.AppendLine($"EXPORT: {e}");
                }
                else
                {
                    // Use ImageHlp if available
                    if (MapAndLoad(_acDllName, null!, out var li, true, true))
                    {
                        try
                        {
                            // Walk export directory
                            // This is simplified; for production you'd parse IMAGE_EXPORT_DIRECTORY
                            sb.AppendLine("; ImageHlp loaded successfully. Full parser not implemented.");
                            sb.AppendLine("; Use CFF Explorer or dumpbin /exports for complete list.");
                        }
                        finally { UnMapAndLoad(ref li); }
                    }
                    else
                    {
                        sb.AppendLine("; ImageHlp not available.");
                    }
                    FreeLibrary(hMod);
                }

                var outPath = Path.Combine(_gamePath, "SL-AC_exports.txt");
                File.WriteAllText(outPath, sb.ToString());

                return $"{{\"success\":true,\"export_count\":0,\"path\":\"{outPath.Replace("\\","/")}\",\"note\":\"Manual parsing limited. Use external tools for full dump.\"}}";
            }
            catch (Exception ex)
            {
                return $"{{\"error\":\"{ex.Message.Replace("\"","'")}\"}}";
            }
        }

        private List<string> ReadExportsManually(string dllPath)
        {
            var exports = new List<string>();
            var data = File.ReadAllBytes(dllPath);
            if (data.Length < 64) return exports;

            // DOS header: e_lfanew at offset 0x3C
            uint peOffset = BitConverter.ToUInt32(data, 0x3C);
            if (peOffset + 4 > data.Length || BitConverter.ToUInt32(data, (int)peOffset) != 0x00004550) // PE\0\0
                return exports;

            ushort machine = BitConverter.ToUInt16(data, (int)peOffset + 4);
            ushort numSections = BitConverter.ToUInt16(data, (int)peOffset + 6);
            ushort optHeaderSize = BitConverter.ToUInt16(data, (int)peOffset + 20);
            uint optionalHeaderOffset = peOffset + 24;
            ushort magic = BitConverter.ToUInt16(data, (int)optionalHeaderOffset);
            bool isPe32Plus = magic == 0x20b;

            // DataDirectory[0] = Export table. Offset from start of OptionalHeader:
            // PE32: 0x60 + 0 * 8 = 0x60
            // PE32+: 0x70 + 0 * 8 = 0x70
            uint dataDirOffset = isPe32Plus ? 0x70u : 0x60u;
            uint exportRva = BitConverter.ToUInt32(data, (int)(optionalHeaderOffset + dataDirOffset));
            uint exportSize = BitConverter.ToUInt32(data, (int)(optionalHeaderOffset + dataDirOffset + 4));

            if (exportRva == 0) return exports;

            // Find section containing exportRva
            uint sectionTableOffset = optionalHeaderOffset + optHeaderSize;
            for (int i = 0; i < numSections; i++)
            {
                uint secOffset = sectionTableOffset + (uint)(i * 40);
                uint secVirtAddr = BitConverter.ToUInt32(data, (int)(secOffset + 12));
                uint secVirtSize = BitConverter.ToUInt32(data, (int)(secOffset + 8));
                uint secRawAddr = BitConverter.ToUInt32(data, (int)(secOffset + 20));

                if (exportRva >= secVirtAddr && exportRva < secVirtAddr + secVirtSize)
                {
                    uint fileOffset = exportRva - secVirtAddr + secRawAddr;
                    if (fileOffset + 40 > data.Length) break;

                    uint nameCount = BitConverter.ToUInt32(data, (int)(fileOffset + 24));
                    uint namesRva = BitConverter.ToUInt32(data, (int)(fileOffset + 32));

                    // Read name pointers
                    for (uint n = 0; n < nameCount && n < 5000; n++)
                    {
                        uint namePtrRva = BitConverter.ToUInt32(data, (int)(fileOffset + namesRva - exportRva + secRawAddr + n * 4));
                        uint nameFileOff = namePtrRva - secVirtAddr + secRawAddr;
                        if (nameFileOff >= data.Length) continue;

                        int len = 0;
                        while (nameFileOff + len < data.Length && data[nameFileOff + len] != 0 && len < 256) len++;
                        string name = Encoding.ASCII.GetString(data, (int)nameFileOff, len);
                        exports.Add(name);
                    }
                    break;
                }
            }

            return exports;
        }

        // ============================================================
        // MINIMAL DUMMY DLL BUILDER
        // ============================================================

        private byte[] BuildMinimalPeDll()
        {
            // Minimal 32-bit PE DLL that does nothing.
            // This is enough to satisfy LoadLibrary("SL-AC.dll") in most cases.
            return new byte[]
            {
                0x4D,0x5A,0x90,0x00,0x03,0x00,0x00,0x00,0x04,0x00,0x00,0x00,0xFF,0xFF,0x00,0x00,
                0xB8,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x40,0x00,0x00,0x00,0x00,0x00,0x00,
                0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,
                0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x80,0x00,0x00,0x00,
                0x0E,0x1F,0xBA,0x0E,0x00,0xB4,0x09,0xCD,0x21,0xB8,0x01,0x4C,0xCD,0x21,0x54,0x68,
                0x69,0x73,0x20,0x70,0x72,0x6F,0x67,0x72,0x61,0x6D,0x20,0x63,0x61,0x6E,0x6E,0x6F,
                0x74,0x20,0x62,0x65,0x20,0x72,0x75,0x6E,0x20,0x69,0x6E,0x20,0x44,0x4F,0x53,0x20,
                0x6D,0x6F,0x64,0x65,0x2E,0x0D,0x0D,0x0A,0x24,0x00,0x00,0x00,0x00,0x00,0x00,0x00,
                0x50,0x45,0x00,0x00,0x4C,0x01,0x01,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,
                0x00,0x00,0x00,0x00,0xE0,0x00,0x00,0x03,0x0B,0x01,0x00,0x00,0x00,0x00,0x00,0x00,
                0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x10,0x00,0x00,0x00,0x10,0x00,0x00,
                0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x10,0x00,0x00,0x00,0x02,0x00,0x00,
                0x04,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x04,0x00,0x00,0x00,0x00,0x00,0x00,0x00,
                0x00,0x20,0x00,0x00,0x00,0x02,0x00,0x00,0x00,0x00,0x00,0x00,0x03,0x00,0x40,0x85,
                0x00,0x00,0x10,0x00,0x00,0x10,0x00,0x00,0x00,0x00,0x10,0x00,0x00,0x10,0x00,0x00,
                0x00,0x00,0x00,0x00,0x10,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,
                0x00,0x10,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,
                0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,
                0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,
                0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,
                0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,
                0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,
                0x2E,0x74,0x65,0x78,0x74,0x00,0x00,0x00,0x00,0x10,0x00,0x00,0x00,0x00,0x10,0x00,
                0x00,0x02,0x00,0x00,0x00,0x02,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,
                0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x20,0x00,0x00,0x60
            };
        }
    }
}
