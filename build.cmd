@echo off
setlocal

REM  HexGrid Generator build script.
REM
REM    build.cmd              framework-dependent exe (~200 KB, needs the .NET 10 Desktop Runtime)
REM    build.cmd standalone   self-contained exe (~150 MB, runs on any Windows machine)

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
