$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
dotnet run --project "$root/tools/ShipGame.ContentBuilder/ShipGame.ContentBuilder.csproj" --configuration Release -- --rebuild
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
