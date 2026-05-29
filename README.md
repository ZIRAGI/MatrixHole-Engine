# MatrixHole Engine

Premium optimization tool for SCP: Secret Laboratory.

![Screenshot](docs/screenshot.png)

## Features

- **Graphics Tweaks** — Shadows, Fog, PostProcess, Bloom, Vignette, MotionBlur, DoF
- **Performance** — FPS Unlock, Low Particles, Disable Reflections, Fast TimeScale
- **Network Tools** — RKN Bypass, Latency Optimizer, DNS Flush, Hosts Editor
- **DLL Injector** — In-game ImGui overlay menu with real-time patches (press INSERT)
- **Auto-Updater** — Automatic updates from GitHub Releases
- **License System** — HWID-bound keys with online/offline validation

## Quick Start

### For Users

1. Download latest release from [Releases](../../releases)
2. Run `MatrixHole.exe`
3. Activate license (or use free tier)
4. Connect Discord for full access
5. Apply tweaks & launch SCP:SL

### For Developers

```bash
# Build .NET app
dotnet build

# Build C++ Injector DLL
cd Injector
build-injector.bat
```

## Configuration

### License (Offline Mode)

1. User copies their HWID from the app
2. You generate a key with `tools/KeyGenerator`:
   ```bash
   cd tools/KeyGenerator
   dotnet run
   # Enter HWID → tier → days
   ```
3. Send key to user

### Discord Auth

See [docs/DISCORD_SETUP.md](docs/DISCORD_SETUP.md)

### GitHub Auto-Updater

See [docs/GITHUB_RELEASES.md](docs/GITHUB_RELEASES.md)

### Reverse Engineering (Finding AOBs)

See [docs/REVERSE_ENGINEERING.md](docs/REVERSE_ENGINEERING.md)

## Project Structure

```
MatrixHole/
  Core/           # C# backend (API, licensing, updater, injection)
  Cheats/         # Memory & assembly patchers
  Injector/       # C++ DLL (DX11 hook, ImGui menu, IPC)
  wwwroot/        # WebView2 frontend
  docs/           # Documentation
  tools/          # KeyGenerator, utilities
```

## License

Proprietary. All rights reserved.
