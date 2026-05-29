#pragma once
#include <windows.h>
#include <vector>
#include <string>

namespace Patches
{
    struct PatchDef
    {
        std::string name;
        std::string module;
        std::string pattern;
        std::string mask;
        std::vector<BYTE> patchBytes;
        std::vector<BYTE> originalBytes;
        int offset;
        bool applied;
    };

    bool Initialize();
    void Shutdown();

    void TogglePatch(const std::string& name);
    bool IsPatchApplied(const std::string& name);
    std::vector<std::string> GetPatchNames();

    void ApplyAll();
    void RestoreAll();
}
