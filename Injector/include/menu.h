#pragma once
#include <d3d11.h>

namespace Menu
{
    void Initialize(ID3D11Device* device, ID3D11DeviceContext* context);
    void Render();
    void Shutdown();
}
