param(
    [switch]$Smoke,
    [switch]$WindowSmoke
)
$ErrorActionPreference = "Stop"
if ($Smoke -and $WindowSmoke) { throw "Choose either -Smoke or -WindowSmoke." }
$root = Split-Path -Parent $PSScriptRoot
& "$PSScriptRoot/build.ps1"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
$arguments = @("run", "--project", "$root/src/ShipGame.Game/ShipGame.Game.csproj", "--configuration", "Release", "--no-build")
if ($Smoke) { $arguments += @("--", "--smoke") }
elseif ($WindowSmoke) { $arguments += @("--", "--window-smoke") }
dotnet @arguments
exit $LASTEXITCODE
