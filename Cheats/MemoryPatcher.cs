using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace MatrixHole.Cheats
{
    /// <summary>
    /// Runtime memory patcher for IL2CPP GameAssembly.dll.
    /// 
    /// WARNING — HIGHEST DETECTION RISK:
    ///   This opens a handle to the game process and writes directly into
    ///   executable memory. ANY kernel-level anti-cheat will flag this instantly.
    ///   ONLY use after SL-AC.dll has been fully disabled (file replaced).
    /// 
    /// HOW TO UPDATE PATTERNS:
    ///   1. Open Cheat Engine, attach to SCPSL.exe
    ///   2. Load GameAssembly.dll symbols if available
    ///   3. Search for the function you want to patch (e.g. ShadowManager::Update)
    ///   4. Copy the first 16-32 bytes of the function as AOB
    ///   5. Replace the placeholder pattern below with your real bytes
    ///   6. Rebuild the tweaker
    /// </summary>
    public class MemoryPatcher
    {
        private readonly string _processName;
        private string _moduleName = "GameAssembly.dll";

        [DllImport("kernel32.dll")]
        private static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll")]
        private static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int dwSize, out int lpNumberOfBytesRead);

        [DllImport("kernel32.dll")]
        private static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int dwSize, out int lpNumberOfBytesWritten);

        [DllImport("kernel32.dll")]
        private static extern bool VirtualProtectEx(IntPtr hProcess, IntPtr lpAddress, int dwSize, uint flNewProtect, out uint lpflOldProtect);

        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("psapi.dll")]
        private static extern bool EnumProcessModules(IntPtr hProcess, [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.U8)] IntPtr[] lphModule, int cb, out int lpcbNeeded);

        [DllImport("psapi.dll")]
        private static extern bool GetModuleInformation(IntPtr hProcess, IntPtr hModule, out MODULEINFO lpmodinfo, int cb);

        [DllImport("psapi.dll", CharSet = CharSet.Auto)]
        private static extern int GetModuleBaseName(IntPtr hProcess, IntPtr hModule, StringBuilder lpBaseName, int nSize);

        private const int PROCESS_ALL_ACCESS = 0x1F0FFF;
        private const uint PAGE_EXECUTE_READWRITE = 0x40;

        [StructLayout(LayoutKind.Sequential)]
        private struct MODULEINFO
        {
            public IntPtr lpBaseOfDll;
            public uint SizeOfImage;
            public IntPtr EntryPoint;
        }

        public MemoryPatcher(string processName)
        {
            _processName = processName;
        }

        public class CheatPatch
        {
            public string Id { get; set; } = "";
            public string Name { get; set; } = "";
            public string Description { get; set; } = "";
            public string Pattern { get; set; } = "";
            public string Mask { get; set; } = "";
            public byte[] PatchBytes { get; set; } = Array.Empty<byte>();
            public int Offset { get; set; } = 0;
            public bool IsPlaceholder { get; set; } = true;
        }

        /// <summary>
        /// Returns the built-in patch database with descriptions.
        /// Placeholder patterns MUST be updated via Cheat Engine before use.
        /// </summary>
        public List<CheatPatch> GetPatchDatabase()
        {
            return new List<CheatPatch>
            {
                new CheatPatch
                {
                    Id = "no_shadows",
                    Name = "No Shadows",
                    Description = "NOPs the shadow rendering pipeline. Massive GPU savings. Pattern must point to ShadowManager::RenderShadows or equivalent.",
                    Pattern = "48 89 5C 24 ? 48 89 74 24 ? 57 48 83 EC 20 48 8B 05 ? ? ? ? 48 33 C4",
                    Mask = "xxxx?xxxx?xxxxxxxxxxxx????xxx",
                    PatchBytes = new byte[] { 0xC3 },
                    Offset = 0,
                    IsPlaceholder = true
                },
                new CheatPatch
                {
                    Id = "no_fog",
                    Name = "No Fog / Volumetrics",
                    Description = "Kills atmospheric fog calculations. Use if pattern points to Fog::Apply or PostProcess::RenderFog.",
                    Pattern = "40 53 48 83 EC 20 48 8B D9 E8 ? ? ? ? 84 C0 74 ? 48 8B CB",
                    Mask = "xxxxxxxxxxx????xxxx?xxx",
                    PatchBytes = new byte[] { 0xC3 },
                    Offset = 0,
                    IsPlaceholder = true
                },
                new CheatPatch
                {
                    Id = "fps_unlock",
                    Name = "FPS Unlock",
                    Description = "Removes the hard 60/120/144 fps cap inside Unity's main loop. Pattern targets Application::set_targetFrameRate write.",
                    Pattern = "C7 83 ? ? 00 00 3C 00 00 00",
                    Mask = "xx??xxxxxx",
                    PatchBytes = new byte[] { 0xC7, 0x83, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 },
                    Offset = 0,
                    IsPlaceholder = true
                },
                new CheatPatch
                {
                    Id = "no_postprocess",
                    Name = "No Post-Process",
                    Description = "Disables Bloom, SSAO, Motion Blur, DoF in one go by NOPing the PostProcessLayer::Render call.",
                    Pattern = "48 8B C4 48 89 58 08 48 89 68 10 48 89 70 18 48 89 78 20 41 56 48 83 EC 30",
                    Mask = "xxxxxxxxxxxxxxxxxxxxxxxxxxx",
                    PatchBytes = new byte[] { 0xC3 },
                    Offset = 0,
                    IsPlaceholder = true
                },
                new CheatPatch
                {
                    Id = "fov_unlock",
                    Name = "FOV Changer",
                    Description = "Patches Camera::get_fieldOfView setter so you can force any FOV. Pattern is placeholder.",
                    Pattern = "F3 0F 11 81 ? ? ? ? F3 0F 10 81 ? ? ? ?",
                    Mask = "xxxx????xxxx????",
                    PatchBytes = new byte[] { 0x90, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90 },
                    Offset = 0,
                    IsPlaceholder = true
                },
                new CheatPatch
                {
                    Id = "no_blood",
                    Name = "No Blood / Decals",
                    Description = "Disables blood splatter and decal projection. Saves GPU fill-rate and CPU physics.",
                    Pattern = "48 89 5C 24 ? 48 89 74 24 ? 57 48 83 EC 20 48 8B 59 ? 48 8B F2",
                    Mask = "xxxx?xxxx?xxxxxxxxxx?xxx",
                    PatchBytes = new byte[] { 0xC3 },
                    Offset = 0,
                    IsPlaceholder = true
                },
                new CheatPatch
                {
                    Id = "no_ragdolls",
                    Name = "No Ragdolls",
                    Description = "Kills Ragdoll::Update. Bodies instantly snap to ground pose instead of simulating physics. Huge CPU savings in firefights.",
                    Pattern = "40 53 48 83 EC 20 80 79 ? ? 48 8B D9 0F 84 ? ? ? ?",
                    Mask = "xxxxxxxx?xxxxxx????",
                    PatchBytes = new byte[] { 0xC3 },
                    Offset = 0,
                    IsPlaceholder = true
                },
                new CheatPatch
                {
                    Id = "no_particles",
                    Name = "No Particles",
                    Description = "NOPs ParticleSystem::Update and ::Render. No smoke, sparks, muzzle flash, blood mist.",
                    Pattern = "48 89 5C 24 ? 48 89 6C 24 ? 48 89 74 24 ? 57 48 83 EC 20 48 8B F9",
                    Mask = "xxxx?xxxx?xxxx?xxxxxxxxxx",
                    PatchBytes = new byte[] { 0xC3 },
                    Offset = 0,
                    IsPlaceholder = true
                },
                new CheatPatch
                {
                    Id = "no_reflections",
                    Name = "No Reflections",
                    Description = "Disables all ReflectionProbe rendering. Turns shiny surfaces into flat matte. Big GPU win in indoor maps.",
                    Pattern = "48 89 5C 24 ? 57 48 83 EC 20 48 8B F9 48 8B DA 48 8B 89 ? ? ? ?",
                    Mask = "xxxx?xxxxxxxxxxxxxxxx????",
                    PatchBytes = new byte[] { 0xC3 },
                    Offset = 0,
                    IsPlaceholder = true
                },
                new CheatPatch
                {
                    Id = "fast_animations",
                    Name = "Fast Animations (TimeScale)",
                    Description = "Patches Time::get_timeScale to return 2.0. EVERYTHING moves 2x faster including your character. This is a gameplay cheat, not just FPS.",
                    Pattern = "F3 0F 10 05 ? ? ? ? F3 0F 59 ? ? F3 0F 11 ? ? ?",
                    Mask = "xxxx????xxx??xxx???",
                    PatchBytes = new byte[] { 0xF3, 0x0F, 0x10, 0x05, 0x00, 0x00, 0x00, 0x00 },
                    Offset = 0,
                    IsPlaceholder = true
                },
                new CheatPatch
                {
                    Id = "lod_override",
                    Name = "LOD Override (Lowest)",
                    Description = "Forces LODGroup to always select the lowest quality mesh. Distant players and objects become blocky but render cheap.",
                    Pattern = "8B 81 ? ? ? ? 89 81 ? ? ? ? 48 8B 81 ? ? ? ?",
                    Mask = "xx????xx????xxx????",
                    PatchBytes = new byte[] { 0xC7, 0x81, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 },
                    Offset = 0,
                    IsPlaceholder = true
                }
            };
        }

        /// <summary>
        /// Applies all non-placeholder patches. Returns JSON report.
        /// </summary>
        public string ApplyAllPatches()
        {
            var patches = GetPatchDatabase();
            var results = new List<object>();
            int applied = 0;
            int skipped = 0;
            int failed = 0;

            foreach (var p in patches)
            {
                if (p.IsPlaceholder)
                {
                    skipped++;
                    results.Add(new { id = p.Id, status = "skipped", reason = "placeholder_pattern" });
                    continue;
                }

                var r = ApplyPatchInternal(p.Pattern, p.Mask, p.PatchBytes, p.Offset);
                if (r.Success)
                {
                    applied++;
                    results.Add(new { id = p.Id, status = "applied", address = r.Address.ToInt64().ToString("X") });
                }
                else
                {
                    failed++;
                    results.Add(new { id = p.Id, status = "failed", reason = r.Message });
                }
            }

            return Newtonsoft.Json.JsonConvert.SerializeObject(new
            {
                applied,
                skipped,
                failed,
                total = patches.Count,
                results
            });
        }

        /// <summary>
        /// Applies a custom user-provided patch.
        /// </summary>
        public PatchResult ApplyCustomPatch(string moduleName, string pattern, string mask, byte[] patchBytes, int offset)
        {
            _moduleName = moduleName;
            return ApplyPatchInternal(pattern, mask, patchBytes, offset);
        }

        public PatchResult ApplyCustomPatch(string pattern, string mask, string patchHex, int offset = 0)
        {
            var patchBytes = patchHex.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(b => Convert.ToByte(b, 16)).ToArray();
            return ApplyPatchInternal(pattern, mask, patchBytes, offset);
        }

        /// <summary>
        /// Loads a JSON array of CheatPatch objects from file and applies all non-placeholder entries.
        /// </summary>
        public string ApplyPatchDatabaseFromFile(string jsonPath)
        {
            if (!File.Exists(jsonPath)) return "{\"error\":\"file not found\"}";
            try
            {
                var json = File.ReadAllText(jsonPath);
                var patches = Newtonsoft.Json.JsonConvert.DeserializeObject<List<CheatPatch>>(json);
                if (patches == null) return "{\"error\":\"invalid json\"}";
                var results = new List<object>();
                int applied = 0, skipped = 0, failed = 0;
                foreach (var p in patches)
                {
                    if (p.IsPlaceholder) { skipped++; results.Add(new { id = p.Id, status = "skipped" }); continue; }
                    var r = ApplyPatchInternal(p.Pattern, p.Mask, p.PatchBytes, p.Offset);
                    if (r.Success) { applied++; results.Add(new { id = p.Id, status = "applied", address = r.Address.ToInt64().ToString("X") }); }
                    else { failed++; results.Add(new { id = p.Id, status = "failed", reason = r.Message }); }
                }
                return Newtonsoft.Json.JsonConvert.SerializeObject(new { applied, skipped, failed, total = patches.Count, results });
            }
            catch (Exception ex) { return $"{{\"error\":\"{ex.Message.Replace("\"", "'")}\"}}"; }
        }

        /// <summary>
        /// Applies a single patch by ID if the pattern is not a placeholder.
        /// </summary>
        public string ApplyPatchById(string id)
        {
            var patch = GetPatchDatabase().FirstOrDefault(p => p.Id == id);
            if (patch == null) return $"{{\"error\":\"Unknown patch ID: {id}\"}}";
            if (patch.IsPlaceholder) return $"{{\"error\":\"Patch {id} is still a placeholder. Update pattern in source code.\"}}";

            var r = ApplyPatchInternal(patch.Pattern, patch.Mask, patch.PatchBytes, patch.Offset);
            return Newtonsoft.Json.JsonConvert.SerializeObject(new { id, success = r.Success, message = r.Message, address = r.Address.ToInt64() });
        }

        private PatchResult ApplyPatchInternal(string pattern, string mask, byte[] patchBytes, int offset)
        {
            var procs = Process.GetProcessesByName(_processName);
            if (procs.Length == 0) return new PatchResult { Success = false, Message = "Process not found" };
            var proc = procs[0];
            var hProc = OpenProcess(PROCESS_ALL_ACCESS, false, proc.Id);
            if (hProc == IntPtr.Zero) return new PatchResult { Success = false, Message = "OpenProcess failed" };

            try
            {
                var mod = GetModule(hProc, _moduleName);
                if (mod.lpBaseOfDll == IntPtr.Zero) return new PatchResult { Success = false, Message = "Module not found: " + _moduleName };

                var address = FindPattern(hProc, mod.lpBaseOfDll, (int)mod.SizeOfImage, pattern, mask);
                if (address == IntPtr.Zero) return new PatchResult { Success = false, Message = "Pattern not found" };

                var target = IntPtr.Add(address, offset);
                if (!VirtualProtectEx(hProc, target, patchBytes.Length, PAGE_EXECUTE_READWRITE, out _))
                    return new PatchResult { Success = false, Message = "VirtualProtectEx failed" };

                if (!WriteProcessMemory(hProc, target, patchBytes, patchBytes.Length, out _))
                    return new PatchResult { Success = false, Message = "WriteProcessMemory failed" };

                return new PatchResult { Success = true, Message = "Patched at 0x" + target.ToString("X"), Address = target };
            }
            finally { CloseHandle(hProc); }
        }

        private PatchResult NopInternal(string pattern, string mask, int length, int offset = 0)
        {
            return ApplyPatchInternal(pattern, mask, Enumerable.Repeat((byte)0x90, length).ToArray(), offset);
        }

        private MODULEINFO GetModule(IntPtr hProc, string name)
        {
            var mods = new IntPtr[1024];
            if (!EnumProcessModules(hProc, mods, mods.Length * IntPtr.Size, out var needed)) return default;
            var count = needed / IntPtr.Size;
            for (int i = 0; i < count; i++)
            {
                var sb = new StringBuilder(256);
                if (GetModuleBaseName(hProc, mods[i], sb, sb.Capacity) > 0)
                {
                    if (sb.ToString().Equals(name, StringComparison.OrdinalIgnoreCase))
                    {
                        GetModuleInformation(hProc, mods[i], out var info, Marshal.SizeOf<MODULEINFO>());
                        return info;
                    }
                }
            }
            return default;
        }

        private IntPtr FindPattern(IntPtr hProc, IntPtr baseAddr, int size, string pattern, string mask)
        {
            var patBytes = pattern.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(b => b == "?" || b == "??" ? (byte)0 : Convert.ToByte(b, 16)).ToArray();
            var m = mask.ToCharArray();

            const int chunkSize = 512 * 1024; // 512 KB chunks to avoid OOM on large modules
            var buffer = new byte[chunkSize + patBytes.Length]; // overlap for patterns crossing chunk boundary

            for (int offset = 0; offset < size; offset += chunkSize)
            {
                int readSize = Math.Min(chunkSize + patBytes.Length, size - offset);
                if (!ReadProcessMemory(hProc, IntPtr.Add(baseAddr, offset), buffer, readSize, out _)) continue;

                for (int i = 0; i <= readSize - patBytes.Length; i++)
                {
                    bool found = true;
                    for (int j = 0; j < patBytes.Length; j++)
                    {
                        if (m[j] == 'x' && buffer[i + j] != patBytes[j]) { found = false; break; }
                    }
                    if (found) return IntPtr.Add(baseAddr, offset + i);
                }
            }
            return IntPtr.Zero;
        }

        public class PatchResult
        {
            public bool Success { get; set; }
            public string Message { get; set; } = "";
            public IntPtr Address { get; set; }
        }
    }
}
