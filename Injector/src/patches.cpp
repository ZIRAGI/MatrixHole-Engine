#include "patches.h"
#include <windows.h>
#include <psapi.h>
#include <vector>
#include <string>
#include <map>
#include <mutex>

#pragma comment(lib, "psapi.lib")

namespace Patches
{
    static std::map<std::string, PatchDef> g_Patches;
    static std::mutex g_Mutex;

    static uintptr_t GetModuleBase(const std::string& modName)
    {
        HMODULE hMods[1024];
        HANDLE hProcess = GetCurrentProcess();
        DWORD cbNeeded;
        if (EnumProcessModules(hProcess, hMods, sizeof(hMods), &cbNeeded))
        {
            for (unsigned int i = 0; i < (cbNeeded / sizeof(HMODULE)); i++)
            {
                char szModName[MAX_PATH];
                if (GetModuleFileNameExA(hProcess, hMods[i], szModName, sizeof(szModName)))
                {
                    std::string fullPath(szModName);
                    size_t pos = fullPath.find_last_of("\\/");
                    std::string name = (pos != std::string::npos) ? fullPath.substr(pos + 1) : fullPath;
                    if (_stricmp(name.c_str(), modName.c_str()) == 0)
                        return (uintptr_t)hMods[i];
                }
            }
        }
        return 0;
    }

    static MODULEINFO GetModuleInfo(const std::string& modName)
    {
        MODULEINFO mi = { 0 };
        HMODULE hMod = GetModuleHandleA(modName.c_str());
        if (hMod)
        {
            HANDLE hProcess = GetCurrentProcess();
            GetModuleInformation(hProcess, hMod, &mi, sizeof(mi));
        }
        return mi;
    }

    static uintptr_t PatternScan(uintptr_t start, size_t size, const std::string& pattern, const std::string& mask)
    {
        std::vector<int> bytes;
        for (size_t i = 0; i < pattern.size(); i += 3)
        {
            std::string byteStr = pattern.substr(i, 2);
            if (byteStr == "??" || byteStr == "? ")
                bytes.push_back(-1);
            else
                bytes.push_back(std::stoi(byteStr, nullptr, 16));
        }

        const size_t patLen = bytes.size();
        if (patLen == 0 || patLen != mask.size()) return 0;

        const BYTE* data = (BYTE*)start;
        for (size_t i = 0; i <= size - patLen; i++)
        {
            bool found = true;
            for (size_t j = 0; j < patLen; j++)
            {
                if (mask[j] == 'x' && bytes[j] != -1 && data[i + j] != (BYTE)bytes[j])
                {
                    found = false;
                    break;
                }
            }
            if (found)
                return start + i;
        }
        return 0;
    }

    static bool ApplyPatchInternal(PatchDef& patch)
    {
        if (patch.applied) return true;
        uintptr_t base = GetModuleBase(patch.module);
        if (!base) return false;

        MODULEINFO mi = GetModuleInfo(patch.module);
        if (!mi.SizeOfImage) return false;

        uintptr_t addr = PatternScan(base, mi.SizeOfImage, patch.pattern, patch.mask);
        if (!addr) return false;

        addr += patch.offset;

        DWORD oldProtect;
        SIZE_T patchSize = patch.patchBytes.size();
        if (!VirtualProtect((LPVOID)addr, patchSize, PAGE_EXECUTE_READWRITE, &oldProtect))
            return false;

        patch.originalBytes.resize(patchSize);
        memcpy(patch.originalBytes.data(), (void*)addr, patchSize);
        memcpy((void*)addr, patch.patchBytes.data(), patchSize);

        VirtualProtect((LPVOID)addr, patchSize, oldProtect, &oldProtect);
        patch.applied = true;
        return true;
    }

    static bool RestorePatchInternal(PatchDef& patch)
    {
        if (!patch.applied) return true;
        uintptr_t base = GetModuleBase(patch.module);
        if (!base) return false;

        MODULEINFO mi = GetModuleInfo(patch.module);
        if (!mi.SizeOfImage) return false;

        uintptr_t addr = PatternScan(base, mi.SizeOfImage, patch.pattern, patch.mask);
        if (!addr) return false;

        addr += patch.offset;

        DWORD oldProtect;
        SIZE_T patchSize = patch.originalBytes.size();
        if (!VirtualProtect((LPVOID)addr, patchSize, PAGE_EXECUTE_READWRITE, &oldProtect))
            return false;

        memcpy((void*)addr, patch.originalBytes.data(), patchSize);
        VirtualProtect((LPVOID)addr, patchSize, oldProtect, &oldProtect);
        patch.applied = false;
        return true;
    }

    bool Initialize()
    {
        std::lock_guard<std::mutex> lock(g_Mutex);

        // Visual patches (placeholders - need real AOBs from x64dbg)
        g_Patches["No Shadows"] = { "No Shadows", "UnityPlayer.dll", "48 8B ?? ?? ?? ?? ?? 48 8B ?? FF 50 ?? 84 C0", "xxxx????xxxxxxx", { 0x31, 0xC0, 0x90 }, {}, 0, false };
        g_Patches["No Fog"] = { "No Fog", "UnityPlayer.dll", "F3 0F ?? ?? ?? 0F ?? ?? F3 0F ?? ?? ?? 0F ?? ??", "xx??xxxx??xx??xx", { 0x0F, 0x57, 0xC0, 0x90, 0x90 }, {}, 0, false };
        g_Patches["No PostProcess"] = { "No PostProcess", "UnityPlayer.dll", "48 89 ?? ?? ?? 57 48 83 EC ?? 48 8B ?? ?? ?? ?? ??", "xx????xxxxxx?????", { 0xC3 }, {}, 0, false };
        g_Patches["No Bloom"] = { "No Bloom", "UnityPlayer.dll", "48 89 ?? ?? ?? 57 48 83 EC ?? 80 3D ?? ?? ?? ?? ??", "xx????xxxxxx?????", { 0xC3 }, {}, 0, false };
        g_Patches["No Vignette"] = { "No Vignette", "UnityPlayer.dll", "48 8B ?? ?? 48 89 ?? ?? 57 48 83 EC ??", "xx??xx??xxxx", { 0xC3 }, {}, 0, false };
        g_Patches["No MotionBlur"] = { "No MotionBlur", "UnityPlayer.dll", "40 53 48 83 EC ?? 48 8B ?? ?? ?? ?? ?? 48 8B ??", "xxxx??x?????x?", { 0xC3 }, {}, 0, false };
        g_Patches["No DoF"] = { "No DoF", "UnityPlayer.dll", "48 89 ?? ?? ?? 57 48 83 EC ?? 48 8B ?? ?? ?? ?? ?? 48 8B ??", "xx????xxxxxx?????x?", { 0xC3 }, {}, 0, false };
        g_Patches["Wireframe Mode"] = { "Wireframe Mode", "UnityPlayer.dll", "48 89 ?? ?? ?? 57 48 83 EC ?? 48 8B ?? ?? ?? ?? ??", "xx????xxxxxx?????", { 0xC3 }, {}, 0, false };

        // Performance patches
        g_Patches["Unlock FPS"] = { "Unlock FPS", "UnityPlayer.dll", "C7 ?? ?? ?? ?? ?? 3C 00 00 00", "xx?????xxxx", { 0xC7, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xF0, 0x42 }, {}, 0, false };
        g_Patches["Low Particles"] = { "Low Particles", "UnityPlayer.dll", "48 89 ?? ?? ?? 57 48 83 EC ?? 48 8B ?? ?? ?? ?? ??", "xx????xxxxxx?????", { 0xC3 }, {}, 0, false };
        g_Patches["Disable Reflections"] = { "Disable Reflections", "UnityPlayer.dll", "48 89 ?? ?? ?? 57 48 83 EC ?? 48 8B ?? ?? ?? ?? ?? 48 8B ??", "xx????xxxxxx?????x?", { 0xC3 }, {}, 0, false };
        g_Patches["Fast TimeScale"] = { "Fast TimeScale", "UnityPlayer.dll", "F3 0F ?? ?? ?? ?? ?? ?? F3 0F ?? ?? ??", "xx???????xx??", { 0xF3, 0x0F, 0x59, 0x05, 0x00, 0x00, 0x00, 0x00 }, {}, 0, false };
        g_Patches["No Audio Reverb"] = { "No Audio Reverb", "UnityPlayer.dll", "48 89 ?? ?? ?? 57 48 83 EC ?? 48 8B ?? ?? ?? ?? ??", "xx????xxxxxx?????", { 0xC3 }, {}, 0, false };

        // Network patches
        g_Patches["Reduce Latency"] = { "Reduce Latency", "GameAssembly.dll", "48 89 ?? ?? ?? 57 48 83 EC ?? 48 8B ?? ?? ?? ?? ??", "xx????xxxxxx?????", { 0xC3 }, {}, 0, false };
        g_Patches["No Packet Delay"] = { "No Packet Delay", "GameAssembly.dll", "48 89 ?? ?? ?? 57 48 83 EC ?? 48 8B ?? ?? ?? ?? ??", "xx????xxxxxx?????", { 0xC3 }, {}, 0, false };
        g_Patches["Fast Connect"] = { "Fast Connect", "GameAssembly.dll", "48 89 ?? ?? ?? 57 48 83 EC ?? 48 8B ?? ?? ?? ?? ??", "xx????xxxxxx?????", { 0xC3 }, {}, 0, false };

        // Misc patches (high risk)
        g_Patches["God Mode"] = { "God Mode", "GameAssembly.dll", "48 89 ?? ?? ?? 57 48 83 EC ?? 48 8B ?? ?? ?? ?? ??", "xx????xxxxxx?????", { 0xC3 }, {}, 0, false };
        g_Patches["Infinite Ammo"] = { "Infinite Ammo", "GameAssembly.dll", "48 89 ?? ?? ?? 57 48 83 EC ?? 48 8B ?? ?? ?? ?? ??", "xx????xxxxxx?????", { 0xC3 }, {}, 0, false };
        g_Patches["No Clip"] = { "No Clip", "GameAssembly.dll", "48 89 ?? ?? ?? 57 48 83 EC ?? 48 8B ?? ?? ?? ?? ??", "xx????xxxxxx?????", { 0xC3 }, {}, 0, false };
        g_Patches["Speed Hack"] = { "Speed Hack", "GameAssembly.dll", "48 89 ?? ?? ?? 57 48 83 EC ?? 48 8B ?? ?? ?? ?? ??", "xx????xxxxxx?????", { 0xC3 }, {}, 0, false };

        return true;
    }

    void Shutdown()
    {
        std::lock_guard<std::mutex> lock(g_Mutex);
        for (auto& pair : g_Patches)
        {
            if (pair.second.applied)
                RestorePatchInternal(pair.second);
        }
        g_Patches.clear();
    }

    void TogglePatch(const std::string& name)
    {
        std::lock_guard<std::mutex> lock(g_Mutex);
        auto it = g_Patches.find(name);
        if (it == g_Patches.end()) return;

        if (it->second.applied)
            RestorePatchInternal(it->second);
        else
            ApplyPatchInternal(it->second);
    }

    bool IsPatchApplied(const std::string& name)
    {
        std::lock_guard<std::mutex> lock(g_Mutex);
        auto it = g_Patches.find(name);
        if (it == g_Patches.end()) return false;
        return it->second.applied;
    }

    std::vector<std::string> GetPatchNames()
    {
        std::lock_guard<std::mutex> lock(g_Mutex);
        std::vector<std::string> names;
        for (const auto& pair : g_Patches)
            names.push_back(pair.first);
        return names;
    }

    void ApplyAll()
    {
        std::lock_guard<std::mutex> lock(g_Mutex);
        for (auto& pair : g_Patches)
        {
            if (!pair.second.applied)
                ApplyPatchInternal(pair.second);
        }
    }

    void RestoreAll()
    {
        std::lock_guard<std::mutex> lock(g_Mutex);
        for (auto& pair : g_Patches)
        {
            if (pair.second.applied)
                RestorePatchInternal(pair.second);
        }
    }
}
