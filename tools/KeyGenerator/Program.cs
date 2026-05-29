using System;
using System.Security.Cryptography;
using System.Text;

namespace KeyGenerator
{
    class Program
    {
        // MUST match MatrixHole.Core.LicenseManager.KeySeed
        private static readonly byte[] KeySeed = Encoding.UTF8.GetBytes("MH_2025_LICENSE_V1");

        static void Main(string[] args)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(@"
   __  ___      __    _ __                          __
  /  |/  /___  / /_  (_) /__________  ___  ____ _  / /
 / /|_/ / __ \/ __ \/ / __/ ___/ _ \/ _ \/ __ `/ / / 
/ /  / / /_/ / /_/ / / /_/ /  /  __/  __/ /_/ / /_/  
/_/  /_/\____/_.___/_/\__/_/   \___/\___/\__,_/ (_)   
");
            Console.ResetColor();
            Console.WriteLine("MatrixHole License Key Generator (Offline Mode)");
            Console.WriteLine("===============================================\n");

            if (args.Length >= 1 && args[0] == "--batch")
            {
                GenerateBatch();
                return;
            }

            Console.Write("Enter HWID (from user's app): ");
            var hwid = Console.ReadLine()?.Trim() ?? "";
            if (string.IsNullOrEmpty(hwid))
            {
                Console.WriteLine("HWID cannot be empty.");
                return;
            }

            Console.Write("Enter tier [standard/pro/lifetime]: ");
            var tier = Console.ReadLine()?.Trim().ToLower() ?? "standard";

            Console.Write("Enter days (0 = 3650 ~ 10 years): ");
            var daysInput = Console.ReadLine()?.Trim() ?? "30";
            var days = int.TryParse(daysInput, out var d) ? d : 30;
            if (days <= 0) days = 3650;

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var expires = now + (days * 86400);

            var key = ComputeKeySignature(hwid, now, expires, tier);

            Console.WriteLine("\n" + new string('=', 50));
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("GENERATED LICENSE");
            Console.ResetColor();
            Console.WriteLine(new string('=', 50));
            Console.WriteLine($"HWID:      {hwid}");
            Console.WriteLine($"Key:       {key}");
            Console.WriteLine($"Tier:      {tier}");
            Console.WriteLine($"Issued:    {DateTimeOffset.FromUnixTimeSeconds(now):yyyy-MM-dd HH:mm:ss} UTC");
            Console.WriteLine($"Expires:   {DateTimeOffset.FromUnixTimeSeconds(expires):yyyy-MM-dd HH:mm:ss} UTC");
            Console.WriteLine($"Days:      {days}");
            Console.WriteLine(new string('=', 50));
            Console.WriteLine("\nSend this KEY to the user. It is bound to their HWID.");
            Console.WriteLine("They enter it in the License Gate inside the app.");
        }

        static void GenerateBatch()
        {
            Console.Write("How many keys? ");
            var count = int.TryParse(Console.ReadLine(), out var c) ? c : 10;

            Console.Write("Tier [standard/pro/lifetime]: ");
            var tier = Console.ReadLine()?.Trim().ToLower() ?? "standard";

            Console.Write("Days: ");
            var days = int.TryParse(Console.ReadLine(), out var d) ? d : 30;

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var expires = now + (days * 86400);

            Console.WriteLine("\nkey,tier,issued,expires");
            for (int i = 0; i < count; i++)
            {
                var hwid = GenerateRandomHwid();
                var key = ComputeKeySignature(hwid, now, expires, tier);
                Console.WriteLine($"{key},{tier},{now},{expires}");
            }
        }

        static string ComputeKeySignature(string hwid, long issued, long expires, string tier)
        {
            var data = $"{hwid}:{issued}:{expires}:{tier}";
            using var hmac = new HMACSHA256(KeySeed);
            return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(data)))[..32];
        }

        static string GenerateRandomHwid()
        {
            var bytes = new byte[12];
            RandomNumberGenerator.Fill(bytes);
            return Convert.ToHexString(bytes);
        }
    }
}
