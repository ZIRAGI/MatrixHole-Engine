@echo off
set VCVARS="C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Auxiliary\Build\vcvarsall.bat"
if not exist %VCVARS% (
    echo ERROR: Visual Studio 2022 not found at expected path.
    echo Please adjust VCVARS path in this batch file.
    exit /b 1
)

call %VCVARS% amd64
msbuild MatrixHole.Injector.vcxproj /p:Configuration=Release /p:Platform=x64
if %ERRORLEVEL% NEQ 0 (
    echo Build failed.
    exit /b 1
)

echo Build succeeded: bin\Injector\MatrixHole.Injector.dll
