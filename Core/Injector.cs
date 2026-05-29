using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MatrixHole.Core
{
    public static class Injector
    {
        private const string PIPE_NAME = "MatrixHoleInjector";
        private static IntPtr _hThread = IntPtr.Zero;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint flAllocationType, uint flProtect);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, uint nSize, out IntPtr lpNumberOfBytesWritten);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateRemoteThread(IntPtr hProcess, IntPtr lpThreadAttributes, uint dwStackSize, IntPtr lpStartAddress, IntPtr lpParameter, uint dwCreationFlags, out IntPtr lpThreadId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetModuleHandleA(string lpModuleName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool VirtualFreeEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint dwFreeType);

        private const uint PROCESS_CREATE_THREAD = 0x0002;
        private const uint PROCESS_QUERY_INFORMATION = 0x0400;
        private const uint PROCESS_VM_OPERATION = 0x0008;
        private const uint PROCESS_VM_WRITE = 0x0020;
        private const uint PROCESS_VM_READ = 0x0010;
        private const uint MEM_COMMIT = 0x1000;
        private const uint MEM_RESERVE = 0x2000;
        private const uint PAGE_READWRITE = 0x04;
        private const uint MEM_RELEASE = 0x8000;

        public static bool IsInjected()
        {
            return GetScpSlProcess() != null && IsPipeConnected();
        }

        private static Process? GetScpSlProcess()
        {
            foreach (var proc in Process.GetProcesses())
            {
                try
                {
                    var name = proc.ProcessName;
                    if (name.Equals("SCPSL", StringComparison.OrdinalIgnoreCase) ||
                        name.Equals("SCPSL.exe", StringComparison.OrdinalIgnoreCase))
                        return proc;
                }
                catch { }
            }
            return null;
        }

        public static string Inject()
        {
            var proc = GetScpSlProcess();
            if (proc == null)
                return "{\"ok\":false,\"error\":\"SCP:SL process not found\"}";

            string dllPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MatrixHole.Injector.dll");
            if (!File.Exists(dllPath))
            {
                dllPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Injector", "bin", "Injector", "MatrixHole.Injector.dll");
                if (!File.Exists(dllPath))
                {
                    dllPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "Injector", "bin", "Injector", "MatrixHole.Injector.dll");
                    dllPath = Path.GetFullPath(dllPath);
                    if (!File.Exists(dllPath))
                        return "{\"ok\":false,\"error\":\"DLL not found: " + dllPath.Replace("\\", "/") + "\"}";
                }
            }

            IntPtr hProcess = OpenProcess(PROCESS_CREATE_THREAD | PROCESS_QUERY_INFORMATION | PROCESS_VM_OPERATION | PROCESS_VM_WRITE | PROCESS_VM_READ, false, proc.Id);
            if (hProcess == IntPtr.Zero)
                return "{\"ok\":false,\"error\":\"OpenProcess failed: " + Marshal.GetLastWin32Error() + "\"}";

            try
            {
                byte[] pathBytes = Encoding.ASCII.GetBytes(dllPath + '\0');
                IntPtr allocAddr = VirtualAllocEx(hProcess, IntPtr.Zero, (uint)pathBytes.Length, MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
                if (allocAddr == IntPtr.Zero)
                    return "{\"ok\":false,\"error\":\"VirtualAllocEx failed: " + Marshal.GetLastWin32Error() + "\"}";

                if (!WriteProcessMemory(hProcess, allocAddr, pathBytes, (uint)pathBytes.Length, out _))
                {
                    VirtualFreeEx(hProcess, allocAddr, 0, MEM_RELEASE);
                    return "{\"ok\":false,\"error\":\"WriteProcessMemory failed: " + Marshal.GetLastWin32Error() + "\"}";
                }

                IntPtr hKernel32 = GetModuleHandleA("kernel32.dll");
                IntPtr loadLibraryAddr = GetProcAddress(hKernel32, "LoadLibraryA");

                _hThread = CreateRemoteThread(hProcess, IntPtr.Zero, 0, loadLibraryAddr, allocAddr, 0, out _);
                if (_hThread == IntPtr.Zero)
                {
                    VirtualFreeEx(hProcess, allocAddr, 0, MEM_RELEASE);
                    return "{\"ok\":false,\"error\":\"CreateRemoteThread failed: " + Marshal.GetLastWin32Error() + "\"}";
                }

                // Wait a bit for DLL to initialize
                Thread.Sleep(3000);

                VirtualFreeEx(hProcess, allocAddr, 0, MEM_RELEASE);
                CloseHandle(_hThread);
                _hThread = IntPtr.Zero;

                return IsPipeConnected()
                    ? "{\"ok\":true,\"status\":\"injected\"}"
                    : "{\"ok\":true,\"status\":\"injected_but_pipe_unavailable\"}";
            }
            finally
            {
                CloseHandle(hProcess);
            }
        }

        public static string SendCommand(string json)
        {
            try
            {
                using var client = new NamedPipeClientStream(".", PIPE_NAME, PipeDirection.InOut);
                client.Connect(2000);
                client.ReadMode = PipeTransmissionMode.Message;

                byte[] data = Encoding.UTF8.GetBytes(json + "\n");
                client.Write(data, 0, data.Length);
                client.Flush();

                var sb = new StringBuilder();
                byte[] buffer = new byte[4096];
                do
                {
                    int read = client.Read(buffer, 0, buffer.Length);
                    if (read == 0) break;
                    sb.Append(Encoding.UTF8.GetString(buffer, 0, read));
                } while (!client.IsMessageComplete);

                return sb.ToString().Trim();
            }
            catch (Exception ex)
            {
                return "{\"ok\":false,\"error\":\"" + ex.Message.Replace("\\", "/") + "\"}";
            }
        }

        public static bool IsPipeConnected()
        {
            try
            {
                using var client = new NamedPipeClientStream(".", PIPE_NAME, PipeDirection.InOut);
                client.Connect(500);
                return true;
            }
            catch { return false; }
        }

        public static string TogglePatch(string patchName)
        {
            return SendCommand($"{{\"cmd\":\"toggle\",\"patch\":\"{patchName}\"}}");
        }

        public static string ShowMenu()
        {
            return SendCommand("{\"cmd\":\"show\"}");
        }

        public static string HideMenu()
        {
            return SendCommand("{\"cmd\":\"hide\"}");
        }

        public static string ApplyAllPatches()
        {
            return SendCommand("{\"cmd\":\"apply_all\"}");
        }

        public static string RestoreAllPatches()
        {
            return SendCommand("{\"cmd\":\"restore_all\"}");
        }
    }
}
