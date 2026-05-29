# Reverse Engineering Guide — Finding AOB Patterns for SCP:SL

## Prerequisites

1. **Cheat Engine 7.5+** (https://cheatengine.org)
2. **x64dbg** (https://x64dbg.com) — optional, for deeper analysis
3. **IL2CPP Dumper** (https://github.com/Perfare/Il2CppDumper) — extracts C# method names from GameAssembly.dll
4. **Ghidra** or **IDA Free** — for static analysis

## Step 1: Extract Unity Method Addresses

SCP:SL uses Unity + IL2CPP. All C# methods are compiled into native code inside `GameAssembly.dll`.

```bash
# 1. Copy GameAssembly.dll + global-metadata.dat from game folder
#    (SCP Secret Laboratory/SCPSL_Data/il2cpp_data/Metadata/global-metadata.dat)

# 2. Run Il2CppDumper
Il2CppDumper.exe GameAssembly.dll global-metadata.dat ./output

# 3. Open output/script.json — this contains ALL C# class/method names with their RVA offsets
```

Look for methods related to graphics:
```
UnityEngine.QualitySettings::set_shadows
UnityEngine.QualitySettings::set_shadowResolution
UnityEngine.RenderSettings::set_fog
UnityEngine.RenderSettings::set_fogMode
UnityEngine.RenderSettings::set_ambientLight
UnityEngine.PostProcessing.PostProcessLayer::OnRenderImage  (if using built-in PPv2)
```

## Step 2: Find Real Patterns with Cheat Engine

### Method A: String References (Easiest)

1. Launch SCP:SL
2. Attach Cheat Engine to `SCPSL.exe`
3. Memory View → Search → Find Memory
4. Search for ASCII string: `set_shadows` or `UnityEngine.QualitySettings`
5. Find references to that string — the calling code is nearby

### Method B: Value Scanning

1. In-game, open console (`` ` ``) and type: `shadows 0`
2. In Cheat Engine, scan for changed value in `UnityPlayer.dll` or `GameAssembly.dll`
3. Change shadows in-game again, filter for changed values
4. When you find the address, Memory View → right click → "Find out what writes to this address"
5. The instruction you see is your patch target

### Method C: IL2CPP Dumper + x64dbg

1. From `script.json`, find the method RVA (e.g., `0x1A3B4C0`)
2. Open `GameAssembly.dll` in x64dbg
3. Go to address: `GameAssembly.dll + 0x1A3B4C0`
4. Look at the assembly code. The first 5-10 bytes form your AOB signature

## Step 3: Build AOB Signature

Example: you found this code at `GameAssembly.dll+1A3B4C0`:
```asm
48 89 5C 24 08       mov [rsp+08], rbx
48 89 74 24 10       mov [rsp+10], rsi
57                   push rdi
48 83 EC 20          sub rsp, 20
40 8A FA             mov dil, dl
```

Your AOB pattern:
```
48 89 5C 24 08 48 89 74 24 10 57 48 83 EC 20 40 8A FA
```

Mask (x = exact match, ? = wildcard):
```
xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx
```

If some bytes change between game updates (e.g., offsets), make them wildcards:
```
48 89 5C 24 ? 48 89 74 24 ? 57 48 83 EC ? 40 8A FA
xxxxxxxxxx?xxxxxxxxx?xxxxxx?xxxxxx
```

## Step 4: Test the Patch

1. In x64dbg, select the bytes and press Space (Assemble)
2. Replace with `ret` (C3) or `nop` (90) to disable the function
3. If shadows disappear → signature is correct!
4. Copy exact bytes from x64dbg into `patches.cpp`

## Common SCP:SL Patterns (Examples)

These are EDUCATIONAL examples for Unity games in general. Real bytes differ per version.

### No Shadows
Target: `QualitySettings.set_shadows` setter
```cpp
// Look for mov into shadow quality global
// Usually writes a byte (0-3) into a static address
```

### No Fog
Target: `RenderSettings.set_fog` setter
```cpp
// Look for mov [fog_enabled], al
// NOP it or force 0
```

### FPS Unlock
Target: `Application.targetFrameRate` setter
```cpp
// Look for mov [target_fps], ecx
// Replace with mov [target_fps], 0x0000 (unlimited)
```

### PostProcess
Target: `PostProcessLayer.OnRenderImage` or `PostProcessVolume.Update`
```cpp
// Return immediately at function start
patchBytes = { 0xC3 };
```

## Step 5: Update patches.cpp

```cpp
g_Patches["No Shadows"] = {
    "No Shadows",
    "GameAssembly.dll",
    "48 89 5C 24 08 48 89 74 24 10 57 48 83 EC 20",  // YOUR real bytes
    "xxxxxxxxxxxxxxxxxxxxxxxxxx",                       // mask
    { 0xC3 },                                           // ret
    {},
    0,
    false
};
```

## Pro Tips

1. **Use pointermaps in CE** — save found addresses between sessions
2. **Version changes break AOBs** — expect to update every game patch
3. **Prefer function start patching** (ret = C3) over mid-function patches — more stable
4. **Use `UnityExplorer` or `RuntimeUnityEditor`** as training wheels before writing memory patches
5. **Dump mono/il2cpp strings** — search for `Command:` or `Console:` to find console command handlers

## Alternative: Use Unity AssetBundle Patches

Instead of runtime memory patches, many Unity optimizations can be done by editing:
- `globalgamemanagers.assets` — quality settings, render pipeline
- `level0` / `level1` scenes — lightmap, fog, PP volumes
- `boot.config` — player settings

This is more stable than memory patches but requires re-patch after game updates.
