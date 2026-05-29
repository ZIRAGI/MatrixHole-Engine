using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace MatrixHole.Cheats
{
    public class GlobalGameManagersPatcher
    {
        private readonly string _gameDataPath;
        private readonly string _backupPath;
        private readonly string _tpkPath;

        public GlobalGameManagersPatcher(string gameDataPath)
        {
            _gameDataPath = gameDataPath;
            _backupPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MatrixHole", "Backups", "globalgamemanagers");
            _tpkPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "classdata.tpk");
        }

        public string AnalyzeQualitySettings()
        {
            try
            {
                var ggmPath = Path.Combine(_gameDataPath, "globalgamemanagers");
                if (!File.Exists(ggmPath))
                    return "{\"error\":\"globalgamemanagers not found\"}";

                var manager = new AssetsManager();
                bool hasTpk = File.Exists(_tpkPath);
                if (hasTpk)
                    manager.LoadClassPackage(_tpkPath);

                var inst = manager.LoadAssetsFile(ggmPath, true);
                var afile = inst.file;

                bool typeTreeEnabled = afile.Metadata.TypeTreeEnabled;
                if (!typeTreeEnabled && hasTpk)
                    manager.LoadClassDatabaseFromPackage(afile.Metadata.UnityVersion);

                var infos = afile.GetAssetsOfType(AssetClassID.QualitySettings).ToList();
                if (infos.Count == 0)
                    return "{\"error\":\"QualitySettings asset not found\",\"typeTree\":" + typeTreeEnabled.ToString().ToLower() + "}";

                var qsInfo = infos[0];
                var qsBase = manager.GetBaseField(inst, qsInfo);
                if (qsBase == null)
                    return "{\"error\":\"GetBaseField returned null. Check classdata.tpk version.\"}";

                // Discover field names
                var children = qsBase.Children;
                var fieldNames = children.Select(c => c.FieldName).ToList();
                var qualitiesField = children.FirstOrDefault(c => c.FieldName == "m_QualitySettings" || c.FieldName == "qualitySettings" || c.FieldName.Contains("Quality"));
                if (qualitiesField == null)
                    return "{\"error\":\"Cannot find quality settings array field\",\"fields\":[" + string.Join(",", fieldNames.Select(f => "\"" + f + "\"")) + "]}";

                var arrField = qualitiesField.Children.FirstOrDefault(c => c.FieldName == "Array" || c.FieldName == "data");
                if (arrField == null)
                    return "{\"error\":\"Cannot find Array child in quality field\",\"qualityChildren\":[" + string.Join(",", qualitiesField.Children.Select(c => "\"" + c.FieldName + "\"")) + "]}";

                var qualities = arrField;
                int count = qualities.Children.Count;

                var result = new System.Text.StringBuilder();
                result.Append("{\"found\":true,\"levels\":" + count + ",\"typeTree\":" + typeTreeEnabled.ToString().ToLower() + ",\"settings\":[");

                for (int i = 0; i < count; i++)
                {
                    var q = qualities.Children[i];
                    var qChildren = q.Children.Select(c => c.FieldName).ToList();
                    string name = GetFieldString(q, "name", "m_Name");
                    int shadowRes = GetFieldInt(q, "shadowResolution");
                    int shadowCascades = GetFieldInt(q, "shadowCascades");
                    float shadowDist = GetFieldFloat(q, "shadowDistance");
                    int vSync = GetFieldInt(q, "vSyncCount");
                    int antiAlias = GetFieldInt(q, "antiAliasing");
                    float lodBias = GetFieldFloat(q, "lodBias");

                    if (i > 0) result.Append(",");
                    result.Append("{");
                    result.Append($"\"name\":\"{name}\",");
                    result.Append($"\"shadowResolution\":{shadowRes},");
                    result.Append($"\"shadowCascades\":{shadowCascades},");
                    result.Append($"\"shadowDistance\":{shadowDist:F1},");
                    result.Append($"\"vSyncCount\":{vSync},");
                    result.Append($"\"antiAliasing\":{antiAlias},");
                    result.Append($"\"lodBias\":{lodBias:F2}");
                    result.Append("}");
                }
                result.Append("]}");
                manager.UnloadAll();
                return result.ToString();
            }
            catch (Exception ex)
            {
                return $"{{\"error\":\"{ex.Message.Replace("\"","'")}\"}}";
            }
        }

        public string ApplyFpsPatch()
        {
            try
            {
                var ggmPath = Path.Combine(_gameDataPath, "globalgamemanagers");
                if (!File.Exists(ggmPath))
                    return "{\"error\":\"globalgamemanagers not found\"}";

                // Backup
                Directory.CreateDirectory(_backupPath);
                var backupFile = Path.Combine(_backupPath, $"globalgamemanagers_{DateTime.Now:yyyyMMdd_HHmmss}.bak");
                File.Copy(ggmPath, backupFile, true);

                var manager = new AssetsManager();
                bool hasTpk = File.Exists(_tpkPath);
                if (hasTpk)
                    manager.LoadClassPackage(_tpkPath);

                var inst = manager.LoadAssetsFile(ggmPath, true);
                var afile = inst.file;

                if (!afile.Metadata.TypeTreeEnabled && hasTpk)
                    manager.LoadClassDatabaseFromPackage(afile.Metadata.UnityVersion);

                var infos = afile.GetAssetsOfType(AssetClassID.QualitySettings).ToList();
                if (infos.Count == 0)
                    return "{\"error\":\"QualitySettings asset not found\"}";

                var qsInfo = infos[0];
                var qsBase = manager.GetBaseField(inst, qsInfo);
                var qualities = qsBase["m_QualitySettings"]["Array"];

                int modified = 0;
                foreach (var q in qualities.Children)
                {
                    q["shadowResolution"].AsInt = 0;        // Low
                    q["shadowCascades"].AsInt = 0;          // No cascades
                    q["shadowDistance"].AsFloat = 0f;       // No shadow distance
                    q["shadowmaskMode"].AsInt = 0;          // Shadowmask disabled
                    q["vSyncCount"].AsInt = 0;              // No vsync
                    q["antiAliasing"].AsInt = 0;            // No AA
                    q["lodBias"].AsFloat = 0.1f;            // Aggressive LOD
                    q["maximumLODLevel"].AsInt = 0;         // Keep 0 (allow all LODs)
                    q["particleRaycastBudget"].AsInt = 4;   // Minimal
                    q["softParticles"].AsBool = false;
                    q["softVegetation"].AsBool = false;
                    q["realtimeReflectionProbes"].AsBool = false;
                    q["billboardsFaceCameraPosition"].AsBool = false;
                    modified++;
                }

                // Also set current quality level to lowest
                qsBase["m_CurrentQuality"].AsInt = 0;

                qsInfo.SetNewData(qsBase);

                var outPath = ggmPath + ".tmp";
                using (var writer = new AssetsFileWriter(outPath))
                {
                    afile.Write(writer);
                }

                manager.UnloadAll();

                // Replace original
                File.Copy(outPath, ggmPath, true);
                File.Delete(outPath);

                return $"{{\"success\":true,\"levels_modified\":{modified},\"backup\":\"{backupFile.Replace("\\","/")}\"}}";
            }
            catch (Exception ex)
            {
                return $"{{\"error\":\"{ex.Message.Replace("\"","'")}\"}}";
            }
        }

        public string RestoreLatestBackup()
        {
            try
            {
                var ggmPath = Path.Combine(_gameDataPath, "globalgamemanagers");
                if (!Directory.Exists(_backupPath))
                    return "{\"error\":\"No backups found\"}";

                var backups = Directory.GetFiles(_backupPath, "globalgamemanagers_*.bak")
                    .OrderByDescending(File.GetLastWriteTime)
                    .ToList();

                if (backups.Count == 0)
                    return "{\"error\":\"No backups found\"}";

                File.Copy(backups[0], ggmPath, true);
                return $"{{\"success\":true,\"restored\":\"{Path.GetFileName(backups[0])}\"}}";
            }
            catch (Exception ex)
            {
                return $"{{\"error\":\"{ex.Message.Replace("\"","'")}\"}}";
            }
        }

        // Helpers for safe field access
        private string GetFieldString(AssetTypeValueField parent, params string[] names)
        {
            foreach (var n in names)
            {
                var child = parent.Children.FirstOrDefault(c => c.FieldName == n);
                if (child != null) return child.AsString;
            }
            return "";
        }

        private int GetFieldInt(AssetTypeValueField parent, string name)
        {
            var child = parent.Children.FirstOrDefault(c => c.FieldName == name);
            return child?.AsInt ?? 0;
        }

        private float GetFieldFloat(AssetTypeValueField parent, string name)
        {
            var child = parent.Children.FirstOrDefault(c => c.FieldName == name);
            return child?.AsFloat ?? 0f;
        }
    }
}
