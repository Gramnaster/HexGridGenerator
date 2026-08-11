@echo off
setlocal

REM  HexGrid Generator build script.
REM
REM    build.cmd              framework-dependent exe (~280 KB, needs the .NET 10 Desktop Runtime)
REM    build.cmd standalone   self-contained exe (~47 MB, runs on any Windows machine)
REM
REM  Either way, publish\ also contains the loose DLLs and .pdb/.deps.json/.runtimeconfig.json
REM  files dotnet publish uses to build the bundle - PublishSingleFile does not delete them from
REM  the output folder. Only HexGridGenerator.exe itself is needed to run or redistribute the app;
REM  it was verified to run standalone from an otherwise-empty folder in both modes.

set MODE=%1
set OUTDIR=%~dp0publish

if /I "%MODE%"=="standalone" (
    echo Building standalone ^(self-contained^) exe...
    dotnet publish "%~dp0src\HexGrid.App\HexGrid.App.csproj" ^
        -c Release -r win-x64 --self-contained true ^
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true ^
        -p:EnableCompressionInSingleFile=true ^
        -o "%OUTDIR%"
) else (
    echo Building framework-dependent exe...
    dotnet publish "%~dp0src\HexGrid.App\HexGrid.App.csproj" ^
        -c Release -r win-x64 --self-contained false ^
        -p:PublishSingleFile=true ^
        -o "%OUTDIR%"
)

if errorlevel 1 (
    echo.
    echo BUILD FAILED.
    exit /b 1
)

echo.
echo Done. Executable is at:
echo   %OUTDIR%\HexGridGenerator.exe
endlocal
