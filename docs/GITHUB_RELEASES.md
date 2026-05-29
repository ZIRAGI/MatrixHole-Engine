# GitHub Releases Auto-Updater Setup

## How It Works

1. App calls GitHub API: `repos/{owner}/{repo}/releases/latest`
2. Compares `tag_name` with current assembly version
3. If newer → shows "Update Available" banner
4. User clicks "Update Now" → downloads ZIP from release assets
5. Batch updater waits for app exit → replaces files → restarts app

## 1. Create GitHub Repository

1. Go to https://github.com/new
2. Name it `MatrixHole-Engine` (or whatever)
3. Make it **Private** (unless you want public source)
4. Note the owner (your username) and repo name

## 2. Configure AppSecrets

```json
{
  "GitHubOwner": "yourusername",
  "GitHubRepo": "MatrixHole-Engine"
}
```

## 3. Create a Release

### Manual (GitHub Web UI)
1. Go to repo → **Releases** → **Draft a new release**
2. Tag version: `v1.0.0` (MUST match `AssemblyVersion`)
3. Title: `MatrixHole v1.0.0`
4. Upload asset: `MatrixHole-v1.0.0.zip` containing:
   ```
   MatrixHole.exe
   MatrixHole.dll
   MatrixHole.runtimeconfig.json
   MatrixHole.deps.json
   wwwroot/
   Injector/bin/Injector/MatrixHole.Injector.dll
   ... (all output files)
   ```
5. Publish release

### Automated (GitHub Actions)

Create `.github/workflows/release.yml`:
```yaml
name: Release
on:
  push:
    tags: ['v*']
jobs:
  build:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with: { dotnet-version: '8.0.x' }
      - run: dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
      - run: cd Injector && build-injector.bat
      - name: Zip
        run: Compress-Archive -Path bin/Release/net8.0-windows/win-x64/publish/* -DestinationPath MatrixHole-${GITHUB_REF#refs/tags/}.zip
      - uses: softprops/action-gh-release@v1
        with:
          files: MatrixHole-*.zip
```

## 4. ZIP Naming Convention

The updater expects this exact URL format:
```
https://github.com/{owner}/{repo}/releases/download/v{version}/MatrixHole-v{version}.zip
```

Example:
```
https://github.com/ZIRAGI/MatrixHole-Engine/releases/download/v1.1.0/MatrixHole-v1.1.0.zip
```

## 5. Version Format

- Tag: `v1.0.0`
- Assembly version: `1.0.0`
- Updater strips `v` prefix before comparison

## 6. Testing Updates

1. Build current version (e.g., `1.0.0`)
2. Change assembly version to `1.0.1` in `.csproj`:
   ```xml
   <AssemblyVersion>1.0.1</AssemblyVersion>
   <FileVersion>1.0.1</FileVersion>
   ```
3. Build release ZIP, publish as `v1.0.1`
4. Run old `1.0.0` build → should detect update

## 7. Troubleshooting

| Issue | Solution |
|-------|----------|
| API rate limit | GitHub API allows 60 requests/hour unauthenticated. Add auth header if needed. |
| ZIP not found | Check exact filename matches `MatrixHole-v{version}.zip` |
| Files not replaced | Run updater as admin (`Verb = "runas"`). Check Windows Defender isn't blocking batch. |
| App doesn't restart | Check batch file isn't deleted before execution completes |

## 8. Self-Contained vs Framework-Dependent

**Recommended**: Self-contained single-file (`--self-contained true /p:PublishSingleFile=true`)
- Users don't need .NET 8 runtime
- Larger file (~150MB) but zero dependencies

For smaller size, use framework-dependent + bundle runtime installer.
