using System;
using System.IO;
using System.Linq;

namespace MatrixHole.Cheats
{
    /// <summary>
    /// HEX-PATCHES GameAssembly.dll ON DISK.
    /// 
    /// THIS IS NUCLEAR. You are modifying the compiled IL2CPP native binary.
    /// If the pattern is wrong, the game will crash on startup or run silently corrupted.
    /// 
    /// HOW TO UPDATE PATTERNS:
    ///   1. Open GameAssembly.dll in x64dbg or Cheat Engine
    ///   2. Search for string references like "PostProcess", "ShadowManager", "ParticleSystem"
    ///   3. Find the nearest CALL or JMP instruction before the function body
    ///   4. Copy 16-32 bytes as AOB pattern (use ? for wildcards)
    ///   5. Count how many NOP bytes (0x90) are needed to overwrite the CALL
    ///   6. Update the pattern below and rebuild.
    /// </summary>
    public class GameAssemblyPatcher
    {
        private readonly string _gameAssemblyPath;
        private readonly string _backupDir;

        public GameAssemblyPatcher(string gamePath)
        {
            _gameAssemblyPath = Path.Combine(gamePath, "GameAssembly.dll");
            _backupDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MatrixHole", "Backups", "GameAssembly");
        }

        public class HexPatch
        {
            public string Id { get; set; } = "";
            public string Name { get; set; } = "";
            public string Description { get; set; } = "";
            public string Pattern { get; set; } = "";
            public string Mask { get; set; } = "";
            public byte[] ReplaceBytes { get; set; } = Array.Empty<byte>();
            public bool IsPlaceholder { get; set; } = true;
        }

        public System.Collections.Generic.List<HexPatch> GetPatchDatabase()
        {
            return new System.Collections.Generic.List<HexPatch>
            {
                new HexPatch
                {
                    Id = "nop_postprocess",
                    Name = "NOP PostProcess",
                    Description = "Finds the PostProcess render call in GameAssembly.dll and NOPs it. Removes bloom, motion blur, DoF, chromatic aberration at engine level.",
                    Pattern = "48 89 5C 24 ? 48 89 74 24 ? 57 48 83 EC 20 48 8B 05 ? ? ? ?",
                    Mask = "xxxx?xxxx?xxxxxxxxxxx????",
                    ReplaceBytes = new byte[] { 0x90, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90 },
                    IsPlaceholder = true
                },
                new HexPatch
                {
                    Id = "nop_shadows",
                    Name = "NOP Shadow Render",
                    Description = "Disables the native shadowmap rendering pass. Massive GPU savings. Pattern targets ShadowManager::RenderShadows or equivalent.",
                    Pattern = "40 53 48 83 EC 20 48 8B D9 E8 ? ? ? ? 84 C0 74 ? 48 8B CB",
                    Mask = "xxxxxxxxxxx????xxxx?xxx",
                    ReplaceBytes = new byte[] { 0xC3 }, // early ret
                    IsPlaceholder = true
                },
                new HexPatch
                {
                    Id = "nop_particles",
                    Name = "NOP Particle Update",
                    Description = "Kills ParticleSystem::Update. No smoke, blood mist, sparks, bullet trails.",
                    Pattern = "48 89 5C 24 ? 48 89 6C 24 ? 48 89 74 24 ? 57 48 83 EC 20 48 8B F9",
                    Mask = "xxxx?xxxx?xxxx?xxxxxxxxxx",
                    ReplaceBytes = new byte[] { 0xC3 },
                    IsPlaceholder = true
                },
                new HexPatch
                {
                    Id = "nop_reflections",
                    Name = "NOP Reflection Probes",
                    Description = "Disables realtime and baked reflection probe rendering. Flat lighting, huge GPU savings.",
                    Pattern = "48 89 5C 24 ? 57 48 83 EC 20 48 8B F9 48 8B DA 48 8B 89 ? ? ? ?",
                    Mask = "xxxx?xxxxxxxxxxxxxxxx????",
                    ReplaceBytes = new byte[] { 0xC3 },
                    IsPlaceholder = true
                },
                new HexPatch
                {
                    Id = "fast_timescale",
                    Name = "Fast Time Scale",
                    Description = "Patches Time::get_timeScale to return 2.0 instead of 1.0. Everything moves 2x faster (including you). This is a cheat, not just FPS.",
                    Pattern = "F3 0F 10 05 ? ? ? ? F3 0F 59 ? ? F3 0F 11 ? ? ?",
                    Mask = "xxxx????xxx??xxx???",
                    ReplaceBytes = new byte[] { 0xF3, 0x0F, 0x10, 0x05, 0x00, 0x00, 0x00, 0x00 }, // needs address fix
                    IsPlaceholder = true
                }
            };
        }

        public string ApplyAllPatches()
        {
            try
            {
                if (!File.Exists(_gameAssemblyPath))
                    return "{\"error\":\"GameAssembly.dll not found\"}";

                Directory.CreateDirectory(_backupDir);
                var backupFile = Path.Combine(_backupDir, $"GameAssembly_{DateTime.Now:yyyyMMdd_HHmmss}.dll.bak");
                File.Copy(_gameAssemblyPath, backupFile, true);

                var data = File.ReadAllBytes(_gameAssemblyPath);
                var patches = GetPatchDatabase();
                int applied = 0, skipped = 0, failed = 0;
                var results = new System.Collections.Generic.List<object>();

                foreach (var p in patches)
                {
                    if (p.IsPlaceholder)
                    {
                        skipped++;
                        results.Add(new { id = p.Id, status = "skipped", reason = "placeholder" });
                        continue;
                    }

                    var patBytes = p.Pattern.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                        .Select(b => b == "?" || b == "??" ? (byte)0 : Convert.ToByte(b, 16)).ToArray();
                    var mask = p.Mask.ToCharArray();

                    int foundAt = -1;
                    for (int i = 0; i <= data.Length - patBytes.Length; i++)
                    {
                        bool ok = true;
                        for (int j = 0; j < patBytes.Length; j++)
                            if (mask[j] == 'x' && data[i + j] != patBytes[j]) { ok = false; break; }
                        if (ok) { foundAt = i; break; }
                    }

                    if (foundAt >= 0)
                    {
                        for (int k = 0; k < p.ReplaceBytes.Length && foundAt + k < data.Length; k++)
                            data[foundAt + k] = p.ReplaceBytes[k];
                        applied++;
                        results.Add(new { id = p.Id, status = "applied", offset = foundAt });
                    }
                    else
                    {
                        failed++;
                        results.Add(new { id = p.Id, status = "failed", reason = "pattern not found" });
                    }
                }

                File.WriteAllBytes(_gameAssemblyPath, data);

                return System.Text.Json.JsonSerializer.Serialize(new
                {
                    success = true,
                    applied,
                    skipped,
                    failed,
                    backup = backupFile.Replace("\\", "/"),
                    results
                });
            }
            catch (Exception ex)
            {
                return $"{{\"error\":\"{ex.Message.Replace("\"", "'")}\"}}";
            }
        }

        public string RestoreLatestBackup()
        {
            try
            {
                if (!Directory.Exists(_backupDir))
                    return "{\"error\":\"No backups found\"}";

                var backups = Directory.GetFiles(_backupDir, "GameAssembly_*.dll.bak")
                    .OrderByDescending(File.GetLastWriteTime)
                    .ToList();

                if (backups.Count == 0)
                    return "{\"error\":\"No backups found\"}";

                File.Copy(backups[0], _gameAssemblyPath, true);
                return $"{{\"success\":true,\"restored\":\"{Path.GetFileName(backups[0])}\"}}";
            }
            catch (Exception ex)
            {
                return $"{{\"error\":\"{ex.Message.Replace("\"", "'")}\"}}";
            }
        }

        public string AnalyzeFile()
        {
            try
            {
                if (!File.Exists(_gameAssemblyPath))
                    return "{\"error\":\"GameAssembly.dll not found\"}";

                var info = new FileInfo(_gameAssemblyPath);
                var data = File.ReadAllBytes(_gameAssemblyPath);

                var strings = new System.Collections.Generic.List<string>();
                var terms = new[] { "PostProcess", "ShadowManager", "ParticleSystem", "ReflectionProbe", "QualitySettings", "TimeScale", "RenderShadows", "Bloom", "SSAO", "MotionBlur" };
                foreach (var t in terms)
                {
                    var tBytes = System.Text.Encoding.ASCII.GetBytes(t);
                    bool found = false;
                    for (int i = 0; i <= data.Length - tBytes.Length; i++)
                    {
                        bool ok = true;
                        for (int j = 0; j < tBytes.Length; j++)
                            if (data[i + j] != tBytes[j]) { ok = false; break; }
                        if (ok) { found = true; break; }
                    }
                    if (found) strings.Add(t);
                }

                return System.Text.Json.JsonSerializer.Serialize(new
                {
                    size = info.Length,
                    found_strings = strings,
                    note = "These strings exist in the binary. Use x64dbg to find actual CALL/JMP patterns near them."
                });
            }
            catch (Exception ex)
            {
                return $"{{\"error\":\"{ex.Message.Replace("\"", "'")}\"}}";
            }
        }
    }
}
