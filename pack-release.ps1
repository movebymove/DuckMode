param(
    [string]$ApiKey,
    [string]$Runtime = "win-x64",
    [string]$Configuration = "Release",
    [string]$ZipName = "DuckMode-App-win-x64.zip"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Write-Host "==> Publishing DuckMode.App ($Configuration, $Runtime)..." -ForegroundColor Cyan

$solutionRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
if (-not $solutionRoot) { $solutionRoot = Get-Location }

$appProj = Join-Path $solutionRoot "DuckMode.App/DuckMode.App.csproj"
if (-not (Test-Path $appProj)) {
    throw "Could not find DuckMode.App.csproj at $appProj"
}

dotnet publish $appProj -c $Configuration -r $Runtime -p:PublishSingleFile=true -p:SelfContained=true -p:IncludeAllContentForSelfExtract=true | Out-Host

$publishDir = Join-Path $solutionRoot "DuckMode.App/bin/$Configuration/net8.0-windows/$Runtime/publish"
if (-not (Test-Path $publishDir)) {
    throw "Publish folder not found: $publishDir"
}

Write-Host "==> Publish output: $publishDir" -ForegroundColor Green

if (-not $ApiKey) {
    $ApiKey = Read-Host -Prompt "Enter Gemini API key (will be written to gemini.key)"
}

$keyFile = Join-Path $publishDir "gemini.key"
[System.IO.File]::WriteAllText($keyFile, $ApiKey)
Write-Host "==> Wrote gemini.key" -ForegroundColor Green

# Create zip next to publish directory (one level up is fine) or in solution root
$zipPath = Join-Path $publishDir $ZipName
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }

Write-Host "==> Zipping to: $zipPath" -ForegroundColor Cyan
Compress-Archive -Path (Join-Path $publishDir '*') -DestinationPath $zipPath

Write-Host "==> Done." -ForegroundColor Green
Write-Host "Zip file: $zipPath"
Write-Host "Distribute the zip. Users just unzip and run DuckMode.App.exe." -ForegroundColor Yellow


