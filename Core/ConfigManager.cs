using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Win32;

namespace MatrixHole.Core
{
    public static class ConfigManager
    {
        private const string UnityRegPath = @"SOFTWARE\Northwood Studios\SCP_ Secret Laboratory";
        private const string OldRegPath = @"SOFTWARE\Northwood Studios\SCP Secret Laboratory";

        public class ScpGameConfig
        {
            // ===== UNITY QUALITY / SCREEN SETTINGS (registry) =====
            public int GraphicsQuality { get; set; } = 2;
            public int ShadowDistance { get; set; } = 100;
            public int AntiAliasing { get; set; } = 2;
            public int ParticleRaycastBudget { get; set; } = 256;
            public float LodBias { get; set; } = 1.0f;
            public int ShadowCascades { get; set; } = 2;
            public int AnisotropicFiltering { get; set; } = 1;
            public int SoftParticles { get; set; } = 1;
            public int Vsync { get; set; } = 0;
            public int MasterTextureLimit { get; set; } = 0; // 0=Full, 1=Half, 2=Quarter, 3=Eighth
            public int TargetFrameRate { get; set; } = -1; // -1 = unlimited (Unity stores as uint max)
            public int ScreenWidth { get; set; } = 1920;
            public int ScreenHeight { get; set; } = 1080;
            public int FullscreenMode { get; set; } = 1; // 0=Exclusive, 1=FullscreenWindow, 2=MaximizedWindow, 3=Windowed

            // ===== CUSTOM SCP:SL SETTINGS (config.ini fallback) =====
            public bool Shadows { get; set; } = true;
            public bool Bloom { get; set; } = true;
            public bool Ssao { get; set; } = true;
            public bool MotionBlur { get; set; } = false;
            public bool AmbientOcclusion { get; set; } = true;
            public int DrawDistance { get; set; } = 150;
            public int CameraFog { get; set; } = 150;
            public int FpsLimit { get; set; } = 0;
            public int TextureQuality { get; set; } = 2;
            public int AntiAliasingCustom { get; set; } = 2;
            public bool DepthOfField { get; set; } = false;
            public bool ChromaticAberration { get; set; } = false;
            public bool NoiseSuppression { get; set; } = true;
            public int VoiceNoiseSuppression { get; set; } = 1;
            public int MasterVolume { get; set; } = 100;
            public int SfxVolume { get; set; } = 100;
            public int VoiceVolume { get; set; } = 100;
            public int MusicVolume { get; set; } = 50;
            public bool ShowBlood { get; set; } = true;
            public bool RagdollCleanup { get; set; } = true;
            public int RagdollLimit { get; set; } = 20;
            public bool FlashlightShadows { get; set; } = true;
            public bool CompressPackets { get; set; } = true;
            public int MaxPing { get; set; } = 300;
        }

        private static string? GetIniPath()
        {
            var info = GameDetector.Detect();
            if (info == null || !info.IsInstalled) return null;
            return Path.Combine(info.InstallPath, "config.ini");
        }

        private static RegistryKey? OpenUnityRegKey(bool writable = false)
        {
            try
            {
                var key = Registry.CurrentUser.OpenSubKey(UnityRegPath, writable);
                if (key != null) return key;
                return Registry.CurrentUser.OpenSubKey(OldRegPath, writable);
            }
            catch { return null; }
        }

        public static ScpGameConfig Load()
        {
            var cfg = new ScpGameConfig();
            LoadFromRegistry(cfg);
            LoadFromIni(cfg);
            return cfg;
        }

        private static void LoadFromRegistry(ScpGameConfig cfg)
        {
            using var key = OpenUnityRegKey(false);
            if (key == null) return;

            cfg.GraphicsQuality = RInt(key, "unity.graphics-quality", cfg.GraphicsQuality);
            cfg.ShadowDistance = RInt(key, "Shadowdistance", cfg.ShadowDistance);
            cfg.AntiAliasing = RInt(key, "Antialiasing", cfg.AntiAliasing);
            cfg.ParticleRaycastBudget = RInt(key, "ParticleRaycastBudget", cfg.ParticleRaycastBudget);
            cfg.LodBias = RFloat(key, "LODBias", cfg.LodBias);
            cfg.ShadowCascades = RInt(key, "Shadowcascades", cfg.ShadowCascades);
            cfg.AnisotropicFiltering = RInt(key, "AnisotropicFiltering", cfg.AnisotropicFiltering);
            cfg.SoftParticles = RInt(key, "SoftParticles", cfg.SoftParticles);
            cfg.Vsync = RInt(key, "unity.vSync", cfg.Vsync);
            cfg.MasterTextureLimit = RInt(key, "MasterTextureLimit", cfg.MasterTextureLimit);
            cfg.TargetFrameRate = RUintToInt(key, "unity.targetFrameRate", cfg.TargetFrameRate);
            cfg.ScreenWidth = RInt(key, "Screenmanager Resolution Width_h182942802", cfg.ScreenWidth);
            cfg.ScreenHeight = RInt(key, "Screenmanager Resolution Height_h2627697771", cfg.ScreenHeight);
            cfg.FullscreenMode = RInt(key, "Screenmanager Fullscreen mode_h3630240806", cfg.FullscreenMode);
        }

        private static void LoadFromIni(ScpGameConfig cfg)
        {
            var path = GetIniPath();
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
            try
            {
                var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var line in File.ReadAllLines(path))
                {
                    var parts = line.Split(new[] { '=' }, 2);
                    if (parts.Length == 2) dict[parts[0].Trim()] = parts[1].Trim();
                }
                if (dict.TryGetValue("shadows", out var v)) cfg.Shadows = PBool(v, true);
                if (dict.TryGetValue("bloom", out v)) cfg.Bloom = PBool(v, true);
                if (dict.TryGetValue("ssao", out v)) cfg.Ssao = PBool(v, true);
                if (dict.TryGetValue("motion_blur", out v)) cfg.MotionBlur = PBool(v, false);
                if (dict.TryGetValue("ambient_occlusion", out v)) cfg.AmbientOcclusion = PBool(v, true);
                if (dict.TryGetValue("draw_distance", out v)) cfg.DrawDistance = PInt(v, 150);
                if (dict.TryGetValue("camera_fog", out v)) cfg.CameraFog = PInt(v, 150);
                if (dict.TryGetValue("fps_limit", out v)) cfg.FpsLimit = PInt(v, 0);
                if (dict.TryGetValue("texture_quality", out v)) cfg.TextureQuality = PInt(v, 2);
                if (dict.TryGetValue("anti_aliasing", out v)) cfg.AntiAliasingCustom = PInt(v, 2);
                if (dict.TryGetValue("depth_of_field", out v)) cfg.DepthOfField = PBool(v, false);
                if (dict.TryGetValue("chromatic_aberration", out v)) cfg.ChromaticAberration = PBool(v, false);
                if (dict.TryGetValue("noise_suppression", out v)) cfg.NoiseSuppression = PBool(v, true);
                if (dict.TryGetValue("voice_noise_suppression", out v)) cfg.VoiceNoiseSuppression = PInt(v, 1);
                if (dict.TryGetValue("master_volume", out v)) cfg.MasterVolume = PInt(v, 100);
                if (dict.TryGetValue("sfx_volume", out v)) cfg.SfxVolume = PInt(v, 100);
                if (dict.TryGetValue("voice_volume", out v)) cfg.VoiceVolume = PInt(v, 100);
                if (dict.TryGetValue("music_volume", out v)) cfg.MusicVolume = PInt(v, 50);
                if (dict.TryGetValue("show_blood", out v)) cfg.ShowBlood = PBool(v, true);
                if (dict.TryGetValue("ragdoll_cleanup", out v)) cfg.RagdollCleanup = PBool(v, true);
                if (dict.TryGetValue("ragdoll_limit", out v)) cfg.RagdollLimit = PInt(v, 20);
                if (dict.TryGetValue("flashlight_shadows", out v)) cfg.FlashlightShadows = PBool(v, true);
                if (dict.TryGetValue("compress_packets", out v)) cfg.CompressPackets = PBool(v, true);
                if (dict.TryGetValue("max_ping", out v)) cfg.MaxPing = PInt(v, 300);
            }
            catch { }
        }

        public static bool Save(ScpGameConfig cfg)
        {
            SaveToRegistry(cfg);
            SaveToIni(cfg);
            return true;
        }

        private static void SaveToRegistry(ScpGameConfig cfg)
        {
            using var key = OpenUnityRegKey(true);
            if (key == null) return;
            WInt(key, "unity.graphics-quality", cfg.GraphicsQuality);
            WInt(key, "UnityGraphicsQuality", cfg.GraphicsQuality);
            WInt(key, "Shadowdistance", cfg.ShadowDistance);
            WInt(key, "Antialiasing", cfg.AntiAliasing);
            WInt(key, "ParticleRaycastBudget", cfg.ParticleRaycastBudget);
            WFloat(key, "LODBias", cfg.LodBias);
            WInt(key, "Shadowcascades", cfg.ShadowCascades);
            WInt(key, "AnisotropicFiltering", cfg.AnisotropicFiltering);
            WInt(key, "SoftParticles", cfg.SoftParticles);
            WInt(key, "unity.vSync", cfg.Vsync);
            WInt(key, "MasterTextureLimit", cfg.MasterTextureLimit);
            WUint(key, "unity.targetFrameRate", cfg.TargetFrameRate == -1 ? 4294967295 : (uint)cfg.TargetFrameRate);
            WInt(key, "Screenmanager Resolution Width_h182942802", cfg.ScreenWidth);
            WInt(key, "Screenmanager Resolution Height_h2627697771", cfg.ScreenHeight);
            WInt(key, "Screenmanager Resolution Width", cfg.ScreenWidth);
            WInt(key, "Screenmanager Resolution Height", cfg.ScreenHeight);
            WInt(key, "Screenmanager Fullscreen mode_h3630240806", cfg.FullscreenMode);
            WInt(key, "Screenmanager Fullscreen mode", cfg.FullscreenMode);
            WInt(key, "Screenmanager Is Fullscreen mode", cfg.FullscreenMode == 0 ? 1 : 0);
        }

        private static void SaveToIni(ScpGameConfig cfg)
        {
            var path = GetIniPath();
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                var lines = new List<string>
                {
                    "# MatrixHole-Engine Config",
                    $"# {DateTime.Now:yyyy-MM-dd HH:mm:ss}", "",
                    "[Graphics]",
                    $"shadows={cfg.Shadows.ToString().ToLower()}",
                    $"bloom={cfg.Bloom.ToString().ToLower()}",
                    $"ssao={cfg.Ssao.ToString().ToLower()}",
                    $"motion_blur={cfg.MotionBlur.ToString().ToLower()}",
                    $"ambient_occlusion={cfg.AmbientOcclusion.ToString().ToLower()}",
                    $"draw_distance={cfg.DrawDistance}",
                    $"camera_fog={cfg.CameraFog}",
                    $"fps_limit={cfg.FpsLimit}",
                    $"texture_quality={cfg.TextureQuality}",
                    $"anti_aliasing={cfg.AntiAliasingCustom}",
                    $"depth_of_field={cfg.DepthOfField.ToString().ToLower()}",
                    $"chromatic_aberration={cfg.ChromaticAberration.ToString().ToLower()}",
                    "",
                    "[Audio]",
                    $"noise_suppression={cfg.NoiseSuppression.ToString().ToLower()}",
                    $"voice_noise_suppression={cfg.VoiceNoiseSuppression}",
                    $"master_volume={cfg.MasterVolume}",
                    $"sfx_volume={cfg.SfxVolume}",
                    $"voice_volume={cfg.VoiceVolume}",
                    $"music_volume={cfg.MusicVolume}",
                    "",
                    "[Gameplay]",
                    $"show_blood={cfg.ShowBlood.ToString().ToLower()}",
                    $"ragdoll_cleanup={cfg.RagdollCleanup.ToString().ToLower()}",
                    $"ragdoll_limit={cfg.RagdollLimit}",
                    $"flashlight_shadows={cfg.FlashlightShadows.ToString().ToLower()}",
                    "",
                    "[Network]",
                    $"compress_packets={cfg.CompressPackets.ToString().ToLower()}",
                    $"max_ping={cfg.MaxPing}",
                };
                File.WriteAllLines(path, lines);
            }
            catch { }
        }

        public static ScpGameConfig ApplyPreset(string preset)
        {
            var cfg = Load() ?? new ScpGameConfig();
            switch (preset.ToLower())
            {
                case "max_fps":
                case "maximum":
                    cfg.GraphicsQuality = 0;
                    cfg.ShadowDistance = 0;
                    cfg.AntiAliasing = 0;
                    cfg.ParticleRaycastBudget = 16;
                    cfg.LodBias = 0.5f;
                    cfg.ShadowCascades = 0;
                    cfg.AnisotropicFiltering = 0;
                    cfg.SoftParticles = 0;
                    cfg.MasterTextureLimit = 2;
                    cfg.Vsync = 0;
                    cfg.TargetFrameRate = -1;
                    cfg.Shadows = false; cfg.Bloom = false; cfg.Ssao = false;
                    cfg.MotionBlur = false; cfg.AmbientOcclusion = false;
                    cfg.DepthOfField = false; cfg.ChromaticAberration = false;
                    cfg.DrawDistance = 50; cfg.CameraFog = 50;
                    cfg.FpsLimit = 0; cfg.TextureQuality = 0; cfg.AntiAliasingCustom = 0;
                    cfg.NoiseSuppression = false; cfg.RagdollLimit = 5;
                    cfg.FlashlightShadows = false;
                    break;
                case "balanced":
                    cfg.GraphicsQuality = 2;
                    cfg.ShadowDistance = 70;
                    cfg.AntiAliasing = 2;
                    cfg.ParticleRaycastBudget = 256;
                    cfg.LodBias = 1.0f;
                    cfg.ShadowCascades = 2;
                    cfg.AnisotropicFiltering = 1;
                    cfg.SoftParticles = 1;
                    cfg.MasterTextureLimit = 1;
                    cfg.Vsync = 0;
                    cfg.TargetFrameRate = -1;
                    cfg.Shadows = true; cfg.Bloom = false; cfg.Ssao = false;
                    cfg.MotionBlur = false; cfg.AmbientOcclusion = true;
                    cfg.DepthOfField = false; cfg.ChromaticAberration = false;
                    cfg.DrawDistance = 100; cfg.CameraFog = 100;
                    cfg.FpsLimit = 0; cfg.TextureQuality = 1; cfg.AntiAliasingCustom = 1;
                    cfg.NoiseSuppression = false; cfg.RagdollLimit = 15;
                    cfg.FlashlightShadows = true;
                    break;
                case "quality":
                    cfg.GraphicsQuality = 4;
                    cfg.ShadowDistance = 150;
                    cfg.AntiAliasing = 8;
                    cfg.ParticleRaycastBudget = 1024;
                    cfg.LodBias = 2.0f;
                    cfg.ShadowCascades = 4;
                    cfg.AnisotropicFiltering = 2;
                    cfg.SoftParticles = 1;
                    cfg.MasterTextureLimit = 0;
                    cfg.Vsync = 1;
                    cfg.TargetFrameRate = 144;
                    cfg.Shadows = true; cfg.Bloom = true; cfg.Ssao = true;
                    cfg.MotionBlur = false; cfg.AmbientOcclusion = true;
                    cfg.DepthOfField = true; cfg.ChromaticAberration = true;
                    cfg.DrawDistance = 200; cfg.CameraFog = 200;
                    cfg.FpsLimit = 144; cfg.TextureQuality = 2; cfg.AntiAliasingCustom = 2;
                    cfg.NoiseSuppression = true; cfg.RagdollLimit = 30;
                    cfg.FlashlightShadows = true;
                    break;
            }
            Save(cfg);
            return cfg;
        }

        // Registry helpers
        private static int RInt(RegistryKey key, string name, int fallback)
        {
            try { return Convert.ToInt32(key.GetValue(name, fallback)); }
            catch { return fallback; }
        }
        private static float RFloat(RegistryKey key, string name, float fallback)
        {
            try { return Convert.ToSingle(key.GetValue(name, fallback)); }
            catch { return fallback; }
        }
        private static int RUintToInt(RegistryKey key, string name, int fallback)
        {
            try
            {
                var val = key.GetValue(name, fallback);
                if (val is uint u && u == uint.MaxValue) return -1;
                return Convert.ToInt32(val);
            }
            catch { return fallback; }
        }
        private static void WInt(RegistryKey key, string name, int value)
        {
            try { key.SetValue(name, value, RegistryValueKind.DWord); } catch { }
        }
        private static void WFloat(RegistryKey key, string name, float value)
        {
            try { key.SetValue(name, value, RegistryValueKind.String); } catch { }
        }
        private static void WUint(RegistryKey key, string name, uint value)
        {
            try { key.SetValue(name, value, RegistryValueKind.DWord); } catch { }
        }

        private static int PInt(string s, int fb) => int.TryParse(s, out var v) ? v : fb;
        private static bool PBool(string s, bool fb)
        {
            if (bool.TryParse(s, out var b)) return b;
            if (s == "1" || s.Equals("true", StringComparison.OrdinalIgnoreCase)) return true;
            if (s == "0" || s.Equals("false", StringComparison.OrdinalIgnoreCase)) return false;
            return fb;
        }
    }
}
