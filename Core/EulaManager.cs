using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace MatrixHole.Core
{
    public static class EulaManager
    {
        private static string EulaDir => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MatrixHole");

        private static string EulaVersionPath => Path.Combine(EulaDir, "eula_version");

        private static string ComputeHash(string text)
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            var hash = MD5.HashData(bytes);
            return Convert.ToHexString(hash);
        }

        private static string CurrentVersion => ComputeHash(GetEulaText());

        public static bool IsAccepted()
        {
            try
            {
                if (!File.Exists(EulaVersionPath)) return false;
                var saved = File.ReadAllText(EulaVersionPath).Trim();
                return saved == CurrentVersion;
            }
            catch { return false; }
        }

        public static void Accept()
        {
            try
            {
                if (!Directory.Exists(EulaDir)) Directory.CreateDirectory(EulaDir);
                File.WriteAllText(EulaVersionPath, CurrentVersion);
            }
            catch { }
        }

        public static string GetEulaText()
        {
            return @"END USER LICENSE AGREEMENT (EULA)

IMPORTANT — PLEASE READ CAREFULLY BEFORE USING THIS SOFTWARE.

1. DEFINITIONS
   'Software' refers to MatrixHole-Engine and all associated files, documentation, and updates. 'User' refers to any individual or entity installing or using the Software. 'Authors' refers to the creators and maintainers of the Software.

2. LICENSE GRANT
   The Authors grant the User a limited, non-exclusive, non-transferable, revocable license to use the Software for personal, non-commercial purposes only. This license does not grant any ownership rights.

3. ACCEPTABLE USE
   The User agrees to use the Software only for lawful purposes. The Software is designed to modify game files and system settings to improve performance. The User acknowledges that such modifications carry inherent risks including but not limited to game bans, account suspensions, data loss, and system instability.

4. PROHIBITED ACTIVITIES
   The User shall NOT: (a) distribute, sell, lease, or sublicense the Software; (b) reverse engineer, decompile, or disassemble the Software; (c) use the Software for commercial purposes; (d) remove or alter any copyright notices; (e) use the Software to develop competing products.

5. NO WARRANTY / DISCLAIMER
   THE SOFTWARE IS PROVIDED 'AS IS' WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE, AND NON-INFRINGEMENT. THE AUTHORS DO NOT WARRANT THAT THE SOFTWARE WILL BE ERROR-FREE, UNINTERRUPTED, OR FREE OF HARMFUL COMPONENTS.

6. LIMITATION OF LIABILITY
   IN NO EVENT SHALL THE AUTHORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, CONSEQUENTIAL, OR PUNITIVE DAMAGES ARISING FROM OR RELATED TO THE USE OR INABILITY TO USE THE SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGES. THIS INCLUDES DAMAGES FOR LOSS OF PROFITS, DATA, ACCOUNTS, OR GOODWILL.

7. ONLINE GAMES & TERMS OF SERVICE
   The User acknowledges that SCP: Secret Laboratory and associated services are governed by their own Terms of Service. Use of this Software may violate those terms. The User accepts sole responsibility for any consequences including permanent account bans. The Authors are not affiliated with Northwood Studios or the SCP:SL development team.

8. SYSTEM MODIFICATIONS
   The Software modifies Windows registry entries, network settings, power profiles, and game files. These changes are made at the User's explicit direction. The User assumes full responsibility for any system instability, security vulnerabilities, or hardware damage resulting from these modifications.

9. DATA COLLECTION
   The Software does not collect personal data. Update checks and crash reports (if enabled) are anonymous. The User's Steam account, credentials, and gameplay data are never accessed, stored, or transmitted.

10. TERMINATION
    This license is effective until terminated. The Authors reserve the right to terminate this license at any time for any reason. Upon termination, the User must cease all use of the Software and destroy all copies.

11. GOVERNING LAW
    This Agreement shall be governed by and construed in accordance with the laws of the jurisdiction in which the User resides, without regard to conflict of law principles.

12. ENTIRE AGREEMENT
    This EULA constitutes the entire agreement between the User and the Authors concerning the Software and supersedes all prior agreements, understandings, and representations.

BY CLICKING 'ACCEPT' OR USING THE SOFTWARE, YOU ACKNOWLEDGE THAT YOU HAVE READ, UNDERSTOOD, AND AGREE TO BE BOUND BY THE TERMS OF THIS AGREEMENT. IF YOU DO NOT AGREE, YOU MUST NOT INSTALL OR USE THE SOFTWARE.";
        }
    }
}
