@echo off
setlocal

REM  HexGrid Generator local build script - framework-dependent exe (~280 KB), needs the
REM  .NET 10 Desktop Runtime installed. For the standalone release build, see build-standalone.cmd.
REM
REM  publish\ also contains the loose DLLs and .pdb/.deps.json/.runtimeconfig.json files dotnet
REM  publish uses to build the bundle - PublishSingleFile does not delete them from the output
REM  folder. Only HexGridGenerator.exe itself is needed to run or redistribute the app; it was
REM  verified to run standalone from an otherwise-empty folder.

set OUTDIR=%~dp0publish

echo Building framework-dependent exe...
dotnet publish "%~dp0src\HexGrid.App\HexGrid.App.csproj" ^
    -c Release -r win-x64 --self-contained false ^
    -p:PublishSingleFile=true ^
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
