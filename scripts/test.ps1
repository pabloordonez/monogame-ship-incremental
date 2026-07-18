$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
& "$PSScriptRoot/build.ps1"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet test "$root/ShipGame.sln" --configuration Release --no-build --no-restore
exit $LASTEXITCODE
