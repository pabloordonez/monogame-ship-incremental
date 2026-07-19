param(
    [ValidateSet("win-x64")]
    [string]$Runtime = "win-x64",
    [switch]$SkipZip,
    [switch]$SkipContent
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$project = "$root/src/ShipGame.Game/ShipGame.Game.csproj"
$outDir = "$root/artifacts/publish/$Runtime"
$zipPath = "$root/artifacts/ShipGame-$Runtime.zip"

Write-Host "Publishing Ship Game ($Runtime, self-contained)..."

if (-not $SkipContent) {
    & "$PSScriptRoot/build-content.ps1"
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

if (Test-Path $outDir) {
    Remove-Item -Recurse -Force $outDir
}

dotnet publish $project `
    --configuration Release `
    --runtime $Runtime `
    --self-contained true `
    --output $outDir `
    /p:PublishSingleFile=false `
    /p:DebugType=None `
    /p:DebugSymbols=false

if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$readme = @"
Ship Game
=========

Windows playtest build (self-contained - no .NET install needed).

How to play
-----------
1. Unzip this folder anywhere.
2. Double-click ShipGame.exe
3. Create a profile, visit Station, open Map, launch Cinder Belt.

Controls
--------
- Move: WASD or left stick
- Aim / fire / mine: mouse or gamepad (see in-game prompts)
- Escape / Back: leave menus or pause where available

Notes
-----
- Requires a 64-bit Windows PC with Vulkan-capable graphics.
- Saves are stored locally under your user profile AppData.
- If the window fails to open, update your GPU drivers and retry.
"@
Set-Content -Path "$outDir/README.txt" -Value $readme -Encoding UTF8

if (-not $SkipZip) {
    if (Test-Path $zipPath) {
        Remove-Item -Force $zipPath
    }
    Compress-Archive -Path "$outDir\*" -DestinationPath $zipPath -CompressionLevel Optimal
    Write-Host "Zip ready: $zipPath"
}

Write-Host "Folder ready: $outDir"
Write-Host "Share the zip (or the whole folder). Friends run ShipGame.exe - no install required."
exit 0
