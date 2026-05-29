#pragma once
#include <windows.h>
#include <string>
#include <thread>
#include <atomic>

namespace IPC
{
    void StartPipeServer();
    void StopPipeServer();
    void ProcessCommand(const std::string& cmd);
    extern std::atomic<bool> g_Running;
}
