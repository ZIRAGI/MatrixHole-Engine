using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace MatrixHole.Core
{
    /// <summary>
    /// ConfigMirror — двусторонняя синхронизация графики между SCP:SL и твикером (п.76 OptimizatorPlan).
    /// </summary>
    public static class ConfigMirror
    {
        private static readonly string MirrorStateFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MatrixHole", "mirror_state.json");

        public class MirrorState
        {
            public DateTime LastSync { get; set; }
            public string Source { get; set; } = "none";
        }

        public static MirrorState? GetLastSync()
        {
            try
            {
                if (!File.Exists(MirrorStateFile)) return null;
                return JsonConvert.DeserializeObject<MirrorState>(File.ReadAllText(MirrorStateFile));
            }
            catch { return null; }
        }

        public static bool SyncFromGameToTweaker()
        {
            try
            {
                var gameCfg = GameplayConfigEditor.ParseGameplayConfig();
                var tweakerCfg = ConfigManager.Load();

                // Map known keys
                if (gameCfg.TryGetValue("fullscreen", out var fs))
                    tweakerCfg.FullscreenMode = fs == "true" ? 1 : 3;
                if (gameCfg.TryGetValue("vsync", out var vs))
                    tweakerCfg.Vsync = vs == "true" ? 1 : 0;
                if (gameCfg.TryGetValue("resolution", out var res))
                {
                    var parts = res.Split('x');
                    if (parts.Length == 2)
                    {
                        tweakerCfg.ScreenWidth = int.Parse(parts[0]);
                        tweakerCfg.ScreenHeight = int.Parse(parts[1]);
                    }
                }

                ConfigManager.Save(tweakerCfg);
                SaveSyncState("game");
                return true;
            }
            catch { return false; }
        }

        public static bool SyncFromTweakerToGame()
        {
            try
            {
                var tweakerCfg = ConfigManager.Load();
                GameplayConfigEditor.SetGameplayValue("fullscreen", (tweakerCfg.FullscreenMode == 1).ToString().ToLower());
                GameplayConfigEditor.SetGameplayValue("vsync", (tweakerCfg.Vsync == 1).ToString().ToLower());
                GameplayConfigEditor.SetGameplayValue("resolution", $"{tweakerCfg.ScreenWidth}x{tweakerCfg.ScreenHeight}");
                SaveSyncState("tweaker");
                return true;
            }
            catch { return false; }
        }

        private static void SaveSyncState(string source)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(MirrorStateFile)!);
            File.WriteAllText(MirrorStateFile, JsonConvert.SerializeObject(new MirrorState { LastSync = DateTime.Now, Source = source }));
        }
    }
}
