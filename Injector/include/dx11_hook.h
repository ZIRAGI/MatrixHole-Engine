#pragma once
#include <windows.h>
#include <d3d11.h>
#include <dxgi.h>

namespace DX11Hook
{
    bool Initialize();
    void Shutdown();
    void SetMenuVisible(bool visible);
    bool IsMenuVisible();
    void ToggleMenu();

    extern ID3D11Device* g_Device;
    extern ID3D11DeviceContext* g_Context;
    extern IDXGISwapChain* g_SwapChain;
}
