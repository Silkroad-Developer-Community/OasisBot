# Usage:
# `scripts\build.ps1
# -Clean[False, optional]
# -DoNotStart[False, optional]
# -Configuration[Debug, optional]`

param(
    [string]$Configuration = "Debug",
    [switch]$Clean,
    [switch]$DoNotStart
)

if (-not (Test-Path ".\SDUI\SDUI\SDUI.csproj")) {
    Write-Output "SDUI submodule is missing or incomplete. Initializing and updating submodules..."
    git submodule update --init --recursive
}

taskkill /F /IM OasisBot.exe
taskkill /F /IM sro_client.exe

if ($Clean) {
    Write-Output "Performing a clean build..."
    New-Item  -ItemType Directory ".\temp" -ErrorAction SilentlyContinue > $null
    Move-Item ".\Build\User" ".\temp" -ErrorAction SilentlyContinue > $null
    Remove-Item -Recurse -Force ".\Build" -ErrorAction SilentlyContinue > $null
}

Write-Output "Building with '$Configuration' configuration..."
$vsPath = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -property installationPath
$msBuildPath = Join-Path $vsPath "MSBuild\Current\Bin\MSBuild.exe"
& $msBuildPath /p:Configuration=$Configuration /p:Platform=x86 OasisBot.sln | Tee-Object -FilePath build.log
$buildExitCode = $LASTEXITCODE

if ($Clean) {
    Move-Item ".\temp\User" ".\Build\User" -ErrorAction SilentlyContinue > $null
    Remove-Item -Recurse -Force ".\temp" -ErrorAction SilentlyContinue > $null
}

if ($buildExitCode -eq 0) {
    if (!$DoNotStart) {
        Write-Output "Starting OasisBot..."
        & ".\Build\OasisBot.exe"
    }
}
else {
    Write-Output "Build failed. Check build.log for details."
    exit $buildExitCode
}