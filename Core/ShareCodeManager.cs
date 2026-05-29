using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using Newtonsoft.Json;

namespace MatrixHole.Core
{
    /// <summary>
    /// ShareCodeManager — сериализация конфига в короткий буквенно-цифровой код (п.67 OptimizatorPlan).
    /// </summary>
    public static class ShareCodeManager
    {
        public static string EncodeProfile(string profileId)
        {
            try
            {
                var json = ProfileManager.ExportProfileToJson(profileId);
                var bytes = Encoding.UTF8.GetBytes(json);
                using var ms = new MemoryStream();
                using (var gzip = new GZipStream(ms, CompressionLevel.Optimal))
                {
                    gzip.Write(bytes, 0, bytes.Length);
                }
                var compressed = ms.ToArray();
                return Convert.ToBase64String(compressed)
                    .Replace('+', '-')
                    .Replace('/', '_')
                    .TrimEnd('=');
            }
            catch (Exception ex)
            {
                return $"ERR:{ex.Message}";
            }
        }

        public static string? DecodeProfile(string shareCode)
        {
            try
            {
                var base64 = shareCode
                    .Replace('-', '+')
                    .Replace('_', '/');
                // Add padding
                switch (base64.Length % 4)
                {
                    case 2: base64 += "=="; break;
                    case 3: base64 += "="; break;
                }

                var compressed = Convert.FromBase64String(base64);
                using var ms = new MemoryStream(compressed);
                using var gzip = new GZipStream(ms, CompressionMode.Decompress);
                using var reader = new StreamReader(gzip, Encoding.UTF8);
                return reader.ReadToEnd();
            }
            catch { return null; }
        }

        public static string? ImportFromShareCode(string shareCode)
        {
            var json = DecodeProfile(shareCode);
            if (string.IsNullOrEmpty(json)) return null;
            return ProfileManager.ImportProfileFromJson(json);
        }
    }
}
