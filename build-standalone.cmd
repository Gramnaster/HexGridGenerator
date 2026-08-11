@echo off
setlocal

REM  HexGrid Generator release build script - standalone (self-contained) exe (~47 MB), runs on
REM  any Windows machine with nothing else installed. This is what gets attached to a GitHub
REM  release. For local iteration, see build.cmd instead.
REM
REM  publish-standalone\ also contains the loose runtime DLLs and .pdb/.deps.json/.runtimeconfig.json
REM  files dotnet publish uses to build the bundle - PublishSingleFile does not delete them from
REM  the output folder. Only HexGridGenerator.exe itself is needed; it was verified to run
REM  standalone from an otherwise-empty folder. Don't attach the whole publish-standalone\ folder
REM  to a release, just the exe.

set OUTDIR=%~dp0publish-standalone

echo Building standalone (self-contained) exe...
dotnet publish "%~dp0src\HexGrid.App\HexGrid.App.csproj" ^
    -c Release -r win-x64 --self-contained true ^
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true ^
    -p:EnableCompressionInSingleFile=true ^
    -o "%OUTDIR%"

if errorlevel 1 (
    echo.
    echo BUILD FAILED.
    exit /b 1
)

echo.
echo Done. Executable is at:
echo   %OUTDIR%\HexGridGenerator.exe
endlocal
