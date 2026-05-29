# MatrixHole — Agent Guide

## Build

### .NET App
```bash
dotnet build
```

### C++ Injector DLL
```bash
cd Injector
build-injector.bat
```

Or manually:
```bash
"C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Auxiliary\Build\vcvarsall.bat" amd64
msbuild MatrixHole.Injector.vcxproj /p:Configuration=Release /p:Platform=x64
```

## Project Structure

```
MatrixHole/
  MatrixHole.csproj           # Main WPF + WebView2 app
  MatrixHole.sln              # Solution (C# + C++)
  Core/                       # C# backend
    CsApi.cs                  # COM-visible API bridge (~130 endpoints)
    Injector.cs               # DLL injection + named pipe IPC
    LicenseManager.cs         # HWID-based licensing
    UpdaterLauncher.cs        # Auto-updater (GitHub releases)
    AppSecrets.cs             # Externalized secrets
    ...
  Cheats/                     # Game patchers
    MemoryPatcher.cs          # Runtime AOB patches
    GameAssemblyPatcher.cs    # On-disk hex patches
  wwwroot/                    # WebView2 frontend
    index.html                # Single-page UI
    js/app.js                 # Main frontend logic
    css/app.css               # Premium dark theme
  Injector/                   # C++ DLL project
    MatrixHole.Injector.vcxproj
    src/                      # dx11_hook, menu, patches, ipc
    include/                  # Headers
    imgui-1.91.0/             # Dear ImGui source
```

## Key Features

### Premium UI
- **Glassmorphism** cards with backdrop blur
- **Neon cyan/purple** accent system
- **Animated background** with gradient orbs and grid overlay
- **Splash screen** with loader animation
- **Toast notifications** (success/error/warning/info)
- **Smooth tab transitions** with slide/fade
- **Onboarding overlay** for first-run experience

### Licensing
- **HWID binding** — license locked to CPU + Disk + Motherboard + MAC
- **Offline validation** — HMAC signature check
- **Online validation** — optional API endpoint
- **Trial support** — 30-day default if no server
- License stored encrypted at `%LocalAppData%/MatrixHole/license.dat`

### Auto-Updater
- Checks GitHub releases API
- Downloads ZIP, extracts to temp
- Batch updater replaces files atomically after app exit
- Waits for SCPSL.exe to close before updating

### Injector
- DX11 Present hook via vtable swapping
- ImGui overlay menu (INSERT to toggle)
- Named pipe IPC for remote control
- AOB scanner with chunked reads
- Reversible patches

## Security Notes
- Debugger/VM detection on startup
- Integrity verification (`SecurityManager`)
- Anti-tamper with `Environment.FailFast()` in release
- Secrets externalized to `%LocalAppData%/MatrixHole/app_secrets.json`

## TODO Before Release
1. Replace placeholder AOBs in `patches.cpp` with real signatures
2. Set up license server endpoint
3. Configure GitHub releases for auto-updater
4. Add real Discord OAuth + role verification
5. Test injector on latest SCPSL build
