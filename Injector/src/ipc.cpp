#include "ipc.h"
#include "dx11_hook.h"
#include "patches.h"
#include <windows.h>
#include <string>
#include <thread>
#include <atomic>
#include <sstream>

namespace IPC
{
    std::atomic<bool> g_Running{ false };
    static std::thread g_ServerThread;
    static HANDLE g_Pipe = INVALID_HANDLE_VALUE;

    static std::string ReadLine(HANDLE pipe)
    {
        std::string result;
        char ch;
        DWORD read;
        while (g_Running.load())
        {
            if (!ReadFile(pipe, &ch, 1, &read, nullptr) || read == 0)
                break;
            if (ch == '\n')
                break;
            result.push_back(ch);
        }
        return result;
    }

    static void WriteLine(HANDLE pipe, const std::string& line)
    {
        DWORD written;
        WriteFile(pipe, line.c_str(), (DWORD)line.size(), &written, nullptr);
        WriteFile(pipe, "\n", 1, &written, nullptr);
        FlushFileBuffers(pipe);
    }

    void ProcessCommand(const std::string& cmd)
    {
        if (cmd.empty()) return;

        // Simple JSON-like parsing: {"cmd":"toggle","patch":"No Shadows"}
        size_t cmdPos = cmd.find("\"cmd\"");
        if (cmdPos == std::string::npos) return;

        size_t colon = cmd.find(':', cmdPos);
        if (colon == std::string::npos) return;

        size_t q1 = cmd.find('"', colon);
        if (q1 == std::string::npos) return;
        size_t q2 = cmd.find('"', q1 + 1);
        if (q2 == std::string::npos) return;

        std::string action = cmd.substr(q1 + 1, q2 - q1 - 1);

        if (action == "toggle")
        {
            size_t pPos = cmd.find("\"patch\"");
            if (pPos != std::string::npos)
            {
                size_t pc = cmd.find(':', pPos);
                size_t pq1 = cmd.find('"', pc);
                size_t pq2 = cmd.find('"', pq1 + 1);
                if (pq1 != std::string::npos && pq2 != std::string::npos)
                {
                    std::string patch = cmd.substr(pq1 + 1, pq2 - pq1 - 1);
                    Patches::TogglePatch(patch);
                }
            }
        }
        else if (action == "show")
        {
            DX11Hook::SetMenuVisible(true);
        }
        else if (action == "hide")
        {
            DX11Hook::SetMenuVisible(false);
        }
        else if (action == "apply_all")
        {
            Patches::ApplyAll();
        }
        else if (action == "restore_all")
        {
            Patches::RestoreAll();
        }
        else if (action == "console")
        {
            // Console commands are received but not yet executed in-game.
            // Future: forward to IL2CPP UnityEngine.Debug.Log or similar hook.
        }
    }

    static void ServerLoop()
    {
        while (g_Running.load())
        {
            g_Pipe = CreateNamedPipeA(
                "\\\\.\\pipe\\MatrixHoleInjector",
                PIPE_ACCESS_DUPLEX,
                PIPE_TYPE_MESSAGE | PIPE_READMODE_MESSAGE | PIPE_WAIT,
                PIPE_UNLIMITED_INSTANCES,
                4096, 4096, 0, nullptr);

            if (g_Pipe == INVALID_HANDLE_VALUE)
            {
                Sleep(500);
                continue;
            }

            if (ConnectNamedPipe(g_Pipe, nullptr) || GetLastError() == ERROR_PIPE_CONNECTED)
            {
                while (g_Running.load())
                {
                    std::string line = ReadLine(g_Pipe);
                    if (line.empty()) break;

                    ProcessCommand(line);

                    // Send status response
                    std::ostringstream resp;
                    resp << "{\"ok\":true}";
                    WriteLine(g_Pipe, resp.str());
                }
            }

            DisconnectNamedPipe(g_Pipe);
            CloseHandle(g_Pipe);
            g_Pipe = INVALID_HANDLE_VALUE;
        }
    }

    void StartPipeServer()
    {
        g_Running.store(true);
        g_ServerThread = std::thread(ServerLoop);
    }

    void StopPipeServer()
    {
        g_Running.store(false);
        if (g_Pipe != INVALID_HANDLE_VALUE)
        {
            CancelIoEx(g_Pipe, nullptr);
            CloseHandle(g_Pipe);
            g_Pipe = INVALID_HANDLE_VALUE;
        }
        if (g_ServerThread.joinable())
            g_ServerThread.join();
    }
}
