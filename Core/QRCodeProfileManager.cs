using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace MatrixHole.Core
{
    /// <summary>
    /// QRCodeProfileManager — импорт/экспорт пресетов через share-код (п.84 OptimizatorPlan).
    /// Сериализация конфига в JSON → Base64. Сканирование → автозагрузка.
    /// QR generation requires QRCoder NuGet package; Base64 share is always available.
    /// </summary>
    public static class QRCodeProfileManager
    {
        public static string? ExportProfileToQR(string profileId, out string? error)
        {
            error = null;
            try
            {
                var json = ProfileManager.ExportProfileToJson(profileId);
                if (json.Contains("\"error\""))
                {
                    error = "Profile not found";
                    return null;
                }

                var b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

                if (b64.Length > 2000)
                {
                    error = "Profile too large for QR code";
                    return null;
                }

                // Save Base64 text that user can paste into any QR generator
                var path = Path.Combine(Path.GetTempPath(), $"sst_share_{profileId}.txt");
                File.WriteAllText(path, b64);
                return path;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return null;
            }
        }

        public static string? ImportProfileFromBase64(string base64Data)
        {
            try
            {
                var json = Encoding.UTF8.GetString(Convert.FromBase64String(base64Data));
                var id = ProfileManager.ImportProfileFromJson(json);
                return id != null
                    ? $"{{\"ok\":true,\"profile_id\":\"{id}\"}}"
                    : "{\"ok\":false,\"error\":\"Invalid profile data\"}";
            }
            catch (Exception ex)
            {
                return $"{{\"ok\":false,\"error\":\"{ex.Message.Replace("\"", "'")}\"}}";
            }
        }

        public static string GetShareableBase64(string profileId)
        {
            var json = ProfileManager.ExportProfileToJson(profileId);
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
        }
    }
}
