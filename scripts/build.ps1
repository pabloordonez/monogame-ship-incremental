$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
dotnet restore "$root/ShipGame.sln"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& "$PSScriptRoot/build-content.ps1"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet build "$root/ShipGame.sln" --configuration Release --no-restore
exit $LASTEXITCODE
