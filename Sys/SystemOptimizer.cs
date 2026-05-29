using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Linq;

namespace MatrixHole.Sys
{
    public static class SystemOptimizer
    {
        public static bool EnableGameMode()
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Microsoft\GameBar");
                key?.SetValue("AllowAutoGameMode", 1, RegistryValueKind.DWord);
                using var key2 = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\GameBar");
                key2?.SetValue("AutoGameModeEnabled", 1, RegistryValueKind.DWord);
                return true;
            }
            catch { return false; }
        }

        public static bool DisableFullscreenOptimizations(string exePath)
        {
            try
            {
                var exeName = System.IO.Path.GetFileName(exePath);
                using var key = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers");
                key?.SetValue(exePath, "~ DISABLEDXMAXIMIZEDWINDOWEDMODE", RegistryValueKind.String);
                return true;
            }
            catch { return false; }
        }

        public static bool DisableGameBar()
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\GameBar");
                key?.SetValue("AutoGameModeEnabled", 0, RegistryValueKind.DWord);
                using var key2 = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Microsoft\GameBar");
                key2?.SetValue("AllowAutoGameMode", 0, RegistryValueKind.DWord);
                key2?.SetValue("ShowStartupPanel", 0, RegistryValueKind.DWord);
                return true;
            }
            catch { return false; }
        }

        public static bool DisableCoreParking()
        {
            try
            {
                var psi = new ProcessStartInfo("powercfg", "-setacvalueindex scheme_current sub_processor CPMINCORES 100") { CreateNoWindow = true, UseShellExecute = false };
                Process.Start(psi)?.WaitForExit();
                psi = new ProcessStartInfo("powercfg", "-setacvalueindex scheme_current sub_processor CPMAXCORES 100") { CreateNoWindow = true, UseShellExecute = false };
                Process.Start(psi)?.WaitForExit();
                psi = new ProcessStartInfo("powercfg", "-setactive scheme_current") { CreateNoWindow = true, UseShellExecute = false };
                Process.Start(psi)?.WaitForExit();
                return true;
            }
            catch { return false; }
        }

        public static bool SetHighPerformancePowerPlan()
        {
            try
            {
                var psi = new ProcessStartInfo("powercfg", "/setactive 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c") { CreateNoWindow = true, UseShellExecute = false };
                Process.Start(psi)?.WaitForExit();
                return true;
            }
            catch { return false; }
        }

        public static bool DisableGameMode()
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Microsoft\GameBar");
                key?.SetValue("AllowAutoGameMode", 0, RegistryValueKind.DWord);
                using var key2 = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\GameBar");
                key2?.SetValue("AutoGameModeEnabled", 0, RegistryValueKind.DWord);
                return true;
            }
            catch { return false; }
        }

        public static bool EnableGameBar()
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\GameBar");
                key?.SetValue("AutoGameModeEnabled", 1, RegistryValueKind.DWord);
                using var key2 = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Microsoft\GameBar");
                key2?.SetValue("AllowAutoGameMode", 1, RegistryValueKind.DWord);
                key2?.SetValue("ShowStartupPanel", 1, RegistryValueKind.DWord);
                return true;
            }
            catch { return false; }
        }

        public static bool EnableCoreParking()
        {
            try
            {
                var psi = new ProcessStartInfo("powercfg", "-setacvalueindex scheme_current sub_processor CPMINCORES 0") { CreateNoWindow = true, UseShellExecute = false };
                Process.Start(psi)?.WaitForExit();
                psi = new ProcessStartInfo("powercfg", "-setacvalueindex scheme_current sub_processor CPMAXCORES 100") { CreateNoWindow = true, UseShellExecute = false };
                Process.Start(psi)?.WaitForExit();
                psi = new ProcessStartInfo("powercfg", "-setactive scheme_current") { CreateNoWindow = true, UseShellExecute = false };
                Process.Start(psi)?.WaitForExit();
                return true;
            }
            catch { return false; }
        }

        public static bool SetBalancedPowerPlan()
        {
            try
            {
                var psi = new ProcessStartInfo("powercfg", "/setactive 381b4222-f694-41f0-9685-ff5bb260df2e") { CreateNoWindow = true, UseShellExecute = false };
                Process.Start(psi)?.WaitForExit();
                return true;
            }
            catch { return false; }
        }

        public static bool EnableFullscreenOptimizations(string exePath)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers");
                key?.DeleteValue(exePath, false);
                return true;
            }
            catch { return false; }
        }

        // ============================================================
        // READBACK — Sync UI with actual Windows state
        // ============================================================

        public static bool IsGameModeEnabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\GameBar");
                var val = key?.GetValue("AllowAutoGameMode");
                return val is int i && i == 1;
            }
            catch { return false; }
        }

        public static bool IsGameBarDisabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\GameBar");
                var val = key?.GetValue("AutoGameModeEnabled");
                return val is int i && i == 0;
            }
            catch { return false; }
        }

        public static bool IsCoreParkingDisabled()
        {
            try
            {
                var psi = new ProcessStartInfo("powercfg", "-query scheme_current sub_processor CPMINCORES") { CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true };
                var proc = Process.Start(psi);
                proc?.WaitForExit();
                var output = proc?.StandardOutput.ReadToEnd() ?? "";
                // If min cores is 100%, core parking is effectively disabled
                return output.Contains("0x00000064"); // 100 in hex
            }
            catch { return false; }
        }

        public static bool IsHighPerformancePlanActive()
        {
            try
            {
                var psi = new ProcessStartInfo("powercfg", "/getactivescheme") { CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true };
                var proc = Process.Start(psi);
                proc?.WaitForExit();
                var output = proc?.StandardOutput.ReadToEnd() ?? "";
                return output.Contains("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");
            }
            catch { return false; }
        }

        public static bool IsFullscreenOptDisabled(string exePath)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers");
                var val = key?.GetValue(exePath) as string;
                return val?.Contains("DISABLEDXMAXIMIZEDWINDOWEDMODE") == true;
            }
            catch { return false; }
        }

        // ============================================================
        // HPET (High Precision Event Timer)
        // ============================================================
        public static bool DisableHPET()
        {
            try
            {
                var psi = new ProcessStartInfo("bcdedit", "/set useplatformclock false") { CreateNoWindow = true, UseShellExecute = false };
                Process.Start(psi)?.WaitForExit();
                return true;
            }
            catch { return false; }
        }
        public static bool EnableHPET()
        {
            try
            {
                var psi = new ProcessStartInfo("bcdedit", "/set useplatformclock true") { CreateNoWindow = true, UseShellExecute = false };
                Process.Start(psi)?.WaitForExit();
                return true;
            }
            catch { return false; }
        }
        public static bool IsHPETDisabled()
        {
            try
            {
                var psi = new ProcessStartInfo("bcdedit", "/enum") { CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true };
                var proc = Process.Start(psi);
                proc?.WaitForExit();
                var output = proc?.StandardOutput.ReadToEnd() ?? "";
                return output.Contains("useplatformclock") && output.Contains("No");
            }
            catch { return false; }
        }

        // ============================================================
        // Nagle's Algorithm (TCPNoDelay)
        // ============================================================
        public static bool DisableNagle()
        {
            try
            {
                using var key = Registry.LocalMachine.CreateSubKey(@"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces");
                foreach (var subName in key?.GetSubKeyNames() ?? Array.Empty<string>())
                {
                    using var sub = key?.OpenSubKey(subName, true);
                    sub?.SetValue("TcpAckFrequency", 1, RegistryValueKind.DWord);
                    sub?.SetValue("TCPNoDelay", 1, RegistryValueKind.DWord);
                }
                return true;
            }
            catch { return false; }
        }
        public static bool EnableNagle()
        {
            try
            {
                using var key = Registry.LocalMachine.CreateSubKey(@"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces");
                foreach (var subName in key?.GetSubKeyNames() ?? Array.Empty<string>())
                {
                    using var sub = key?.OpenSubKey(subName, true);
                    sub?.DeleteValue("TcpAckFrequency", false);
                    sub?.DeleteValue("TCPNoDelay", false);
                }
                return true;
            }
            catch { return false; }
        }
        public static bool IsNagleDisabled()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces");
                foreach (var subName in key?.GetSubKeyNames() ?? Array.Empty<string>())
                {
                    using var sub = key?.OpenSubKey(subName);
                    if (sub?.GetValue("TCPNoDelay") is int v && v == 1) return true;
                }
                return false;
            }
            catch { return false; }
        }

        // ============================================================
        // SysMain (Superfetch)
        // ============================================================
        public static bool DisableSysMain()
        {
            try
            {
                var psi = new ProcessStartInfo("sc", "config sysmain start= disabled") { CreateNoWindow = true, UseShellExecute = false };
                Process.Start(psi)?.WaitForExit();
                var psi2 = new ProcessStartInfo("sc", "stop sysmain") { CreateNoWindow = true, UseShellExecute = false };
                Process.Start(psi2)?.WaitForExit();
                return true;
            }
            catch { return false; }
        }
        public static bool EnableSysMain()
        {
            try
            {
                var psi = new ProcessStartInfo("sc", "config sysmain start= auto") { CreateNoWindow = true, UseShellExecute = false };
                Process.Start(psi)?.WaitForExit();
                return true;
            }
            catch { return false; }
        }
        public static bool IsSysMainDisabled()
        {
            try
            {
                var psi = new ProcessStartInfo("sc", "query sysmain") { CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true };
                var proc = Process.Start(psi);
                proc?.WaitForExit();
                var output = proc?.StandardOutput.ReadToEnd() ?? "";
                return output.Contains("STATE") && output.Contains("STOPPED");
            }
            catch { return false; }
        }

        // ============================================================
        // Visual Effects (Best Performance)
        // ============================================================
        public static bool DisableVisualEffects()
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects");
                key?.SetValue("VisualFXSetting", 2, RegistryValueKind.DWord);
                using var key2 = Registry.CurrentUser.CreateSubKey(@"Control Panel\Desktop");
                key2?.SetValue("UserPreferencesMask", new byte[] { 0x90, 0x12, 0x03, 0x80, 0x10, 0x00, 0x00, 0x00 }, RegistryValueKind.Binary);
                return true;
            }
            catch { return false; }
        }
        public static bool EnableVisualEffects()
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects");
                key?.SetValue("VisualFXSetting", 1, RegistryValueKind.DWord);
                using var key2 = Registry.CurrentUser.CreateSubKey(@"Control Panel\Desktop");
                key2?.SetValue("UserPreferencesMask", new byte[] { 0x9E, 0x12, 0x07, 0x80, 0x12, 0x00, 0x00, 0x00 }, RegistryValueKind.Binary);
                return true;
            }
            catch { return false; }
        }
        public static bool IsVisualEffectsDisabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects");
                return key?.GetValue("VisualFXSetting") is int v && v == 2;
            }
            catch { return false; }
        }
    }
}
