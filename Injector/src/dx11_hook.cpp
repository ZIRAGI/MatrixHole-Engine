#include "dx11_hook.h"
#include "menu.h"
#include "patches.h"
#include <imgui.h>
#include <backends/imgui_impl_dx11.h>
#include <backends/imgui_impl_win32.h>
#include <d3d11.h>
#include <dxgi.h>
#include <windows.h>
#include <atomic>

#pragma comment(lib, "d3d11.lib")
#pragma comment(lib, "dxgi.lib")

// Forward declaration from imgui_impl_win32.cpp
LRESULT ImGui_ImplWin32_WndProcHandler(HWND hWnd, UINT msg, WPARAM wParam, LPARAM lParam);

namespace DX11Hook
{
    ID3D11Device* g_Device = nullptr;
    ID3D11DeviceContext* g_Context = nullptr;
    IDXGISwapChain* g_SwapChain = nullptr;

    static std::atomic<bool> g_MenuVisible{ false };
    static HWND g_hWnd = nullptr;
    static WNDPROC g_OrigWndProc = nullptr;

    using Present_t = HRESULT(STDMETHODCALLTYPE*)(IDXGISwapChain*, UINT, UINT);
    static Present_t g_OriginalPresent = nullptr;
    static void** g_SwapChainVTable = nullptr;


    static LRESULT WINAPI WndProc(HWND hWnd, UINT msg, WPARAM wParam, LPARAM lParam)
    {
        if (g_MenuVisible.load())
        {
            if (ImGui_ImplWin32_WndProcHandler(hWnd, msg, wParam, lParam))
                return true;

            if (msg == WM_KEYDOWN && wParam == VK_INSERT)
            {
                g_MenuVisible.store(!g_MenuVisible.load());
                return 0;
            }

            if (ImGui::GetIO().WantCaptureMouse &&
                (msg == WM_LBUTTONDOWN || msg == WM_LBUTTONUP || msg == WM_RBUTTONDOWN || msg == WM_RBUTTONUP || msg == WM_MOUSEMOVE || msg == WM_MOUSEWHEEL))
                return 0;
            if (ImGui::GetIO().WantCaptureKeyboard &&
                (msg == WM_KEYDOWN || msg == WM_KEYUP || msg == WM_CHAR))
                return 0;
        }
        else if (msg == WM_KEYDOWN && wParam == VK_INSERT)
        {
            g_MenuVisible.store(true);
            return 0;
        }

        return CallWindowProcW(g_OrigWndProc, hWnd, msg, wParam, lParam);
    }

    static HRESULT STDMETHODCALLTYPE HookedPresent(IDXGISwapChain* pSwapChain, UINT SyncInterval, UINT Flags)
    {
        static bool init = false;
        if (!init)
        {
            if (SUCCEEDED(pSwapChain->GetDevice(__uuidof(ID3D11Device), (void**)&g_Device)))
            {
                g_Device->GetImmediateContext(&g_Context);
                DXGI_SWAP_CHAIN_DESC sd;
                pSwapChain->GetDesc(&sd);
                g_hWnd = sd.OutputWindow;
                g_OrigWndProc = (WNDPROC)SetWindowLongPtrW(g_hWnd, GWLP_WNDPROC, (LONG_PTR)WndProc);

                ImGui::CreateContext();
                ImGuiIO& io = ImGui::GetIO();
                io.ConfigFlags |= ImGuiConfigFlags_NavEnableKeyboard;
                ImGui::StyleColorsDark();
                ImGui_ImplWin32_Init(g_hWnd);
                ImGui_ImplDX11_Init(g_Device, g_Context);

                Menu::Initialize(g_Device, g_Context);
                init = true;
            }
        }

        if (init && g_MenuVisible.load())
        {
            ImGui_ImplDX11_NewFrame();
            ImGui_ImplWin32_NewFrame();
            ImGui::NewFrame();

            Menu::Render();

            ImGui::Render();
            g_Context->OMSetRenderTargets(0, nullptr, nullptr);
            ImGui_ImplDX11_RenderDrawData(ImGui::GetDrawData());
        }

        return g_OriginalPresent(pSwapChain, SyncInterval, Flags);
    }

    bool Initialize()
    {
        WNDCLASSEXW wc = { sizeof(wc), CS_CLASSDC, DefWindowProcW, 0L, 0L, GetModuleHandleW(nullptr), nullptr, nullptr, nullptr, nullptr, L"MH_Dummy", nullptr };
        RegisterClassExW(&wc);
        HWND dummyWnd = CreateWindowExW(0, wc.lpszClassName, L"Dummy", WS_OVERLAPPEDWINDOW, 0, 0, 100, 100, nullptr, nullptr, wc.hInstance, nullptr);

        D3D_FEATURE_LEVEL fl;
        DXGI_SWAP_CHAIN_DESC sd = {};
        sd.BufferCount = 2;
        sd.BufferDesc.Width = 100;
        sd.BufferDesc.Height = 100;
        sd.BufferDesc.Format = DXGI_FORMAT_R8G8B8A8_UNORM;
        sd.BufferDesc.RefreshRate.Numerator = 60;
        sd.BufferDesc.RefreshRate.Denominator = 1;
        sd.BufferUsage = DXGI_USAGE_RENDER_TARGET_OUTPUT;
        sd.OutputWindow = dummyWnd;
        sd.SampleDesc.Count = 1;
        sd.SampleDesc.Quality = 0;
        sd.Windowed = TRUE;
        sd.SwapEffect = DXGI_SWAP_EFFECT_DISCARD;
        sd.Flags = 0;

        ID3D11Device* dev = nullptr;
        IDXGISwapChain* sc = nullptr;
        HRESULT hr = D3D11CreateDeviceAndSwapChain(nullptr, D3D_DRIVER_TYPE_HARDWARE, nullptr, 0, nullptr, 0, D3D11_SDK_VERSION, &sd, &sc, &dev, &fl, nullptr);
        if (FAILED(hr))
        {
            DestroyWindow(dummyWnd);
            UnregisterClassW(wc.lpszClassName, wc.hInstance);
            return false;
        }

        void** vtable = *(void***)sc;
        g_SwapChainVTable = vtable;
        g_OriginalPresent = (Present_t)vtable[8];

        dev->Release();
        sc->Release();
        DestroyWindow(dummyWnd);
        UnregisterClassW(wc.lpszClassName, wc.hInstance);

        DWORD oldProtect;
        if (!VirtualProtect(&vtable[8], sizeof(void*), PAGE_EXECUTE_READWRITE, &oldProtect))
            return false;

        vtable[8] = (void*)HookedPresent;
        VirtualProtect(&vtable[8], sizeof(void*), oldProtect, &oldProtect);

        return true;
    }

    void Shutdown()
    {
        if (g_OriginalPresent && g_SwapChainVTable)
        {
            DWORD oldProtect;
            VirtualProtect(&g_SwapChainVTable[8], sizeof(void*), PAGE_EXECUTE_READWRITE, &oldProtect);
            g_SwapChainVTable[8] = (void*)g_OriginalPresent;
            VirtualProtect(&g_SwapChainVTable[8], sizeof(void*), oldProtect, &oldProtect);
        }

        Menu::Shutdown();
        ImGui_ImplDX11_Shutdown();
        ImGui_ImplWin32_Shutdown();
        ImGui::DestroyContext();

        if (g_OrigWndProc && g_hWnd)
            SetWindowLongPtrW(g_hWnd, GWLP_WNDPROC, (LONG_PTR)g_OrigWndProc);
    }

    void SetMenuVisible(bool visible) { g_MenuVisible.store(visible); }
    bool IsMenuVisible() { return g_MenuVisible.load(); }
    void ToggleMenu() { g_MenuVisible.store(!g_MenuVisible.load()); }
}
