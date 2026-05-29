#include <windows.h>
#include <thread>
#include <atomic>
#include "dx11_hook.h"
#include "menu.h"
#include "patches.h"
#include "ipc.h"

static std::atomic<bool> g_Initialized{ false };
static std::atomic<bool> g_Shutdown{ false };

static void MainThread(HMODULE hModule)
{
    Sleep(2000);

    if (!DX11Hook::Initialize())
    {
        FreeLibraryAndExitThread(hModule, 1);
        return;
    }

    Patches::Initialize();
    IPC::StartPipeServer();

    while (!g_Shutdown.load())
    {
        Sleep(50);
    }

    IPC::StopPipeServer();
    Patches::Shutdown();
    DX11Hook::Shutdown();
    FreeLibraryAndExitThread(hModule, 0);
}

BOOL APIENTRY DllMain(HMODULE hModule, DWORD reason, LPVOID lpReserved)
{
    switch (reason)
    {
    case DLL_PROCESS_ATTACH:
        DisableThreadLibraryCalls(hModule);
        CreateThread(nullptr, 0, (LPTHREAD_START_ROUTINE)MainThread, hModule, 0, nullptr);
        break;
    case DLL_PROCESS_DETACH:
        g_Shutdown.store(true);
        break;
    }
    return TRUE;
}
