#include "menu.h"
#include "patches.h"
#include "dx11_hook.h"
#include <imgui.h>
#include <d3d11.h>
#include <vector>
#include <string>

namespace Menu
{
    static ID3D11Device* s_Device = nullptr;
    static ID3D11DeviceContext* s_Context = nullptr;
    static int s_SelectedTab = 0;

    void Initialize(ID3D11Device* device, ID3D11DeviceContext* context)
    {
        s_Device = device;
        s_Context = context;

        ImGuiStyle& style = ImGui::GetStyle();
        style.WindowRounding = 4.0f;
        style.FrameRounding = 3.0f;
        style.GrabRounding = 3.0f;
        style.ChildRounding = 4.0f;
        style.ItemSpacing = ImVec2(8, 6);
        style.WindowPadding = ImVec2(12, 12);

        ImVec4* colors = style.Colors;
        colors[ImGuiCol_WindowBg] = ImVec4(0.06f, 0.06f, 0.07f, 0.94f);
        colors[ImGuiCol_Border] = ImVec4(0.18f, 0.18f, 0.20f, 0.50f);
        colors[ImGuiCol_FrameBg] = ImVec4(0.12f, 0.12f, 0.14f, 1.00f);
        colors[ImGuiCol_FrameBgHovered] = ImVec4(0.18f, 0.18f, 0.22f, 1.00f);
        colors[ImGuiCol_FrameBgActive] = ImVec4(0.24f, 0.24f, 0.28f, 1.00f);
        colors[ImGuiCol_TitleBg] = ImVec4(0.08f, 0.08f, 0.10f, 1.00f);
        colors[ImGuiCol_TitleBgActive] = ImVec4(0.10f, 0.10f, 0.14f, 1.00f);
        colors[ImGuiCol_Button] = ImVec4(0.15f, 0.15f, 0.18f, 1.00f);
        colors[ImGuiCol_ButtonHovered] = ImVec4(0.25f, 0.25f, 0.30f, 1.00f);
        colors[ImGuiCol_ButtonActive] = ImVec4(0.35f, 0.35f, 0.42f, 1.00f);
        colors[ImGuiCol_Header] = ImVec4(0.15f, 0.15f, 0.20f, 1.00f);
        colors[ImGuiCol_HeaderHovered] = ImVec4(0.25f, 0.25f, 0.32f, 1.00f);
        colors[ImGuiCol_HeaderActive] = ImVec4(0.35f, 0.35f, 0.45f, 1.00f);
        colors[ImGuiCol_CheckMark] = ImVec4(0.45f, 0.75f, 1.00f, 1.00f);
        colors[ImGuiCol_SliderGrab] = ImVec4(0.35f, 0.55f, 0.85f, 1.00f);
        colors[ImGuiCol_SliderGrabActive] = ImVec4(0.45f, 0.70f, 1.00f, 1.00f);
        colors[ImGuiCol_Separator] = ImVec4(0.20f, 0.20f, 0.25f, 0.50f);
    }

    static void DrawTabButton(const char* label, int tab, float width)
    {
        bool active = s_SelectedTab == tab;
        if (active)
        {
            ImGui::PushStyleColor(ImGuiCol_Button, ImVec4(0.25f, 0.40f, 0.65f, 1.00f));
            ImGui::PushStyleColor(ImGuiCol_ButtonHovered, ImVec4(0.30f, 0.48f, 0.75f, 1.00f));
            ImGui::PushStyleColor(ImGuiCol_ButtonActive, ImVec4(0.35f, 0.55f, 0.85f, 1.00f));
        }
        if (ImGui::Button(label, ImVec2(width, 28)))
            s_SelectedTab = tab;
        if (active)
            ImGui::PopStyleColor(3);
    }

    void Render()
    {
        ImGui::SetNextWindowSize(ImVec2(520, 420), ImGuiCond_FirstUseEver);
        ImGui::Begin("MatrixHole | SCP:SL Optimizer", nullptr, ImGuiWindowFlags_NoCollapse);

        const char* tabs[] = { "Visual", "Performance", "Network", "Misc" };
        float btnWidth = (ImGui::GetContentRegionAvail().x - ImGui::GetStyle().ItemSpacing.x * 3) / 4.0f;
        for (int i = 0; i < 4; i++)
        {
            DrawTabButton(tabs[i], i, btnWidth);
            if (i < 3) ImGui::SameLine();
        }

        ImGui::Separator();
        ImGui::Spacing();

        auto names = Patches::GetPatchNames();

        if (s_SelectedTab == 0)
        {
            ImGui::TextColored(ImVec4(0.5f, 0.7f, 1.0f, 1.0f), "Visual Optimizations");
            ImGui::Separator();

            const char* visualPatches[] = {
                "No Shadows", "No Fog", "No PostProcess", "No Bloom",
                "No Vignette", "No MotionBlur", "No DoF", "Wireframe Mode"
            };
            for (const char* name : visualPatches)
            {
                bool applied = Patches::IsPatchApplied(name);
                if (ImGui::Checkbox(name, &applied))
                    Patches::TogglePatch(name);
            }
        }
        else if (s_SelectedTab == 1)
        {
            ImGui::TextColored(ImVec4(0.5f, 0.7f, 1.0f, 1.0f), "Performance");
            ImGui::Separator();

            const char* perfPatches[] = {
                "Unlock FPS", "Low Particles", "Disable Reflections",
                "Fast TimeScale", "No Audio Reverb"
            };
            for (const char* name : perfPatches)
            {
                bool applied = Patches::IsPatchApplied(name);
                if (ImGui::Checkbox(name, &applied))
                    Patches::TogglePatch(name);
            }

            ImGui::Spacing();
            ImGui::Text("Info: FPS unlock removes 60/120 cap.");
            ImGui::Text("Low particles reduces GPU load in heavy scenes.");
        }
        else if (s_SelectedTab == 2)
        {
            ImGui::TextColored(ImVec4(0.5f, 0.7f, 1.0f, 1.0f), "Network");
            ImGui::Separator();

            const char* netPatches[] = {
                "Reduce Latency", "No Packet Delay", "Fast Connect"
            };
            for (const char* name : netPatches)
            {
                bool applied = Patches::IsPatchApplied(name);
                if (ImGui::Checkbox(name, &applied))
                    Patches::TogglePatch(name);
            }
        }
        else if (s_SelectedTab == 3)
        {
            ImGui::TextColored(ImVec4(0.5f, 0.7f, 1.0f, 1.0f), "Miscellaneous");
            ImGui::Separator();

            const char* miscPatches[] = {
                "God Mode", "Infinite Ammo", "No Clip", "Speed Hack"
            };
            for (const char* name : miscPatches)
            {
                bool applied = Patches::IsPatchApplied(name);
                if (ImGui::Checkbox(name, &applied))
                    Patches::TogglePatch(name);
            }

            ImGui::Spacing();
            ImGui::TextColored(ImVec4(1.0f, 0.3f, 0.3f, 1.0f), "WARNING: Misc patches are high-risk.");
            ImGui::Text("Use at own risk. May trigger anti-cheat.");
        }

        ImGui::Spacing();
        ImGui::Separator();
        ImGui::TextColored(ImVec4(0.4f, 0.4f, 0.5f, 1.0f), "Press INSERT to toggle menu");

        ImGui::End();
    }

    void Shutdown()
    {
        s_Device = nullptr;
        s_Context = nullptr;
    }
}
