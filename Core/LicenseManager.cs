using System;
using System.IO;
using System.Linq;
using System.Management;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MatrixHole.Core
{
    public static class LicenseManager
    {
        private static readonly string LicenseFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MatrixHole", "license.dat");

        private static readonly byte[] KeySeed = Encoding.UTF8.GetBytes("MH_2025_LICENSE_V1");

        public class LicenseInfo
        {
            public string Hwid { get; set; } = "";
            public string Key { get; set; } = "";
            public long ExpiresAt { get; set; }
            public string Tier { get; set; } = "standard";
            public long IssuedAt { get; set; }
        }

        public static string GetHwid()
        {
            try
            {
                var cpuId = "";
                using (var mc = new ManagementClass("win32_processor"))
                using (var instances = mc.GetInstances())
                {
                    foreach (ManagementObject mo in instances)
                    {
                        cpuId = mo.Properties["processorId"].Value?.ToString() ?? "";
                        break;
                    }
                }

                var diskSerial = "";
                using (var mc = new ManagementClass("Win32_DiskDrive"))
                using (var instances = mc.GetInstances())
                {
                    foreach (ManagementObject mo in instances)
                    {
                        diskSerial = mo.Properties["SerialNumber"].Value?.ToString()?.Trim() ?? "";
                        if (!string.IsNullOrEmpty(diskSerial)) break;
                    }
                }

                var boardId = "";
                using (var mc = new ManagementClass("Win32_BaseBoard"))
                using (var instances = mc.GetInstances())
                {
                    foreach (ManagementObject mo in instances)
                    {
                        boardId = mo.Properties["SerialNumber"].Value?.ToString()?.Trim() ?? "";
                        break;
                    }
                }

                var mac = "";
                using (var mc = new ManagementClass("Win32_NetworkAdapterConfiguration"))
                using (var instances = mc.GetInstances())
                {
                    foreach (ManagementObject mo in instances)
                    {
                        if ((bool)(mo["IPEnabled"] ?? false))
                        {
                            mac = mo["MacAddress"]?.ToString() ?? "";
                            break;
                        }
                    }
                }

                var combined = $"{cpuId}|{diskSerial}|{boardId}|{mac}";
                using var sha = SHA256.Create();
                return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(combined)))[..24];
            }
            catch
            {
                // Fallback
                var fallback = $"{Environment.MachineName}|{Environment.UserName}|{Environment.SystemDirectory}";
                using var sha = SHA256.Create();
                return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(fallback)))[..24];
            }
        }

        public static bool IsLicensed()
        {
            try
            {
                if (!File.Exists(LicenseFile)) return false;
                var encrypted = File.ReadAllBytes(LicenseFile);
                var json = Encoding.UTF8.GetString(Unprotect(encrypted));
                var lic = JsonConvert.DeserializeObject<LicenseInfo>(json);
                if (lic == null) return false;

                var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                if (now > lic.ExpiresAt) return false;

                // Validate HWID binding
                var currentHwid = GetHwid();
                if (!string.Equals(lic.Hwid, currentHwid, StringComparison.OrdinalIgnoreCase))
                    return false;

                // Validate key signature (simple HMAC)
                var expected = ComputeKeySignature(lic.Hwid, lic.IssuedAt, lic.ExpiresAt, lic.Tier);
                return lic.Key == expected;
            }
            catch { return false; }
        }

        public static LicenseInfo? GetLicenseInfo()
        {
            try
            {
                if (!File.Exists(LicenseFile)) return null;
                var encrypted = File.ReadAllBytes(LicenseFile);
                var json = Encoding.UTF8.GetString(Unprotect(encrypted));
                return JsonConvert.DeserializeObject<LicenseInfo>(json);
            }
            catch { return null; }
        }

        public static string ActivateLicense(string key)
        {
            try
            {
                var hwid = GetHwid();
                var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var expires = now + (30 * 86400); // Default 30 days trial if no server

                // Try online validation first
                try
                {
                    using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
                    var payload = new { hwid, key, version = "1.0.0" };
                    var response = client.PostAsync(
                        "https://api.matrixhole.dev/license/activate",
                        new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json")).Result;

                    if (response.IsSuccessStatusCode)
                    {
                        var body = response.Content.ReadAsStringAsync().Result;
                        var obj = JObject.Parse(body);
                        if (obj["ok"]?.Value<bool>() == true)
                        {
                            expires = obj["expires_at"]?.Value<long>() ?? expires;
                            var tier = obj["tier"]?.Value<string>() ?? "standard";
                            SaveLicense(hwid, key, now, expires, tier);
                            return "{\"ok\":true,\"tier\":\"" + tier + "\"}";
                        }
                        return "{\"ok\":false,\"error\":\"" + (obj["error"]?.Value<string>() ?? "invalid") + "\"}";
                    }
                }
                catch { /* offline fallback */ }

                // Offline validation: key must match HMAC of HWID
                var expected = ComputeKeySignature(hwid, now, expires, "standard");
                if (key != expected)
                    return "{\"ok\":false,\"error\":\"Invalid license key\"}";

                SaveLicense(hwid, key, now, expires, "standard");
                return "{\"ok\":true,\"tier\":\"standard\"}";
            }
            catch (Exception ex)
            {
                return "{\"ok\":false,\"error\":\"" + ex.Message.Replace("\\", "/") + "\"}";
            }
        }

        public static string GetLicenseStatusJson()
        {
            var lic = GetLicenseInfo();
            if (lic == null) return "{\"licensed\":false}";
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var daysLeft = Math.Max(0, (int)((lic.ExpiresAt - now) / 86400));
            return JsonConvert.SerializeObject(new
            {
                licensed = true,
                tier = lic.Tier,
                daysLeft,
                hwid = GetHwid()
            });
        }

        private static void SaveLicense(string hwid, string key, long issued, long expires, string tier)
        {
            var lic = new LicenseInfo { Hwid = hwid, Key = key, IssuedAt = issued, ExpiresAt = expires, Tier = tier };
            var json = JsonConvert.SerializeObject(lic);
            var encrypted = Protect(Encoding.UTF8.GetBytes(json));
            Directory.CreateDirectory(Path.GetDirectoryName(LicenseFile)!);
            File.WriteAllBytes(LicenseFile, encrypted);
        }

        private static string ComputeKeySignature(string hwid, long issued, long expires, string tier)
        {
            var data = $"{hwid}:{issued}:{expires}:{tier}";
            using var hmac = new HMACSHA256(KeySeed);
            return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(data)))[..32];
        }

        private static byte[] Protect(byte[] data)
        {
            // Simple XOR + entropy obfuscation
            var entropy = new byte[16];
            RandomNumberGenerator.Fill(entropy);
            var result = new byte[1 + entropy.Length + data.Length];
            result[0] = (byte)entropy.Length;
            Buffer.BlockCopy(entropy, 0, result, 1, entropy.Length);
            for (int i = 0; i < data.Length; i++)
                result[1 + entropy.Length + i] = (byte)(data[i] ^ entropy[i % entropy.Length] ^ KeySeed[i % KeySeed.Length]);
            return result;
        }

        private static byte[] Unprotect(byte[] data)
        {
            if (data.Length < 17) throw new InvalidDataException();
            var entropyLen = data[0];
            var entropy = new byte[entropyLen];
            Buffer.BlockCopy(data, 1, entropy, 0, entropyLen);
            var payloadLen = data.Length - 1 - entropyLen;
            var result = new byte[payloadLen];
            for (int i = 0; i < payloadLen; i++)
                result[i] = (byte)(data[1 + entropyLen + i] ^ entropy[i % entropy.Length] ^ KeySeed[i % KeySeed.Length]);
            return result;
        }
    }
}
