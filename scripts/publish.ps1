Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$publishRoot = Join-Path $root "artifacts\publish"

Push-Location $root
try {
    New-Item -ItemType Directory -Force -Path $publishRoot | Out-Null

    dotnet publish .\src\CodexStatus.Tray\CodexStatus.Tray.csproj -c Release -r win-x64 --self-contained true -o (Join-Path $publishRoot "CodexStatus.Tray")
    dotnet publish .\src\CodexStatus.Hook\CodexStatus.Hook.csproj -c Release -r win-x64 --self-contained true -o (Join-Path $publishRoot "CodexStatus.Hook")
    dotnet publish .\src\CodexStatus.ExecAdapter\CodexStatus.ExecAdapter.csproj -c Release -r win-x64 --self-contained true -o (Join-Path $publishRoot "CodexStatus.ExecAdapter")
}
finally {
    Pop-Location
}
