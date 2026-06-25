Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    dotnet build .\CodexStatus.sln
    dotnet test .\CodexStatus.sln --no-build
}
finally {
    Pop-Location
}
