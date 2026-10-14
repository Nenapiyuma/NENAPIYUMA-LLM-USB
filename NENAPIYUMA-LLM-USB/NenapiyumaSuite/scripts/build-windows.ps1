[CmdletBinding()]
param(
    [ValidateSet("Release", "Debug")]
    [string]$Configuration = "Release",
    [ValidateSet("win-x64")]
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"
$SuiteRoot = Split-Path -Parent $PSScriptRoot
$Solution = Join-Path $SuiteRoot "NenapiyumaSuite.sln"
$ReleaseRoot = Join-Path $SuiteRoot "release"
$TestsProject = Join-Path $SuiteRoot "tests/NENAPIYUMA.SelfTests/NENAPIYUMA.SelfTests.csproj"

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw ".NET SDK was not found. Install the supported .NET 10 SDK and run this script again."
}
$sdkVersion = (& dotnet --version).Trim()
if ($LASTEXITCODE -ne 0) { throw "Could not query the installed .NET SDK." }
$sdkMajor = 0
if ($sdkVersion -match '^(\d+)\.') { $sdkMajor = [int]$Matches[1] }
if ($sdkMajor -lt 10) { throw "This solution targets .NET 10. Installed SDK: $sdkVersion. Install the .NET 10 SDK." }

Write-Host "NENAPIYUMA Windows Suite build"
Write-Host "SDK: $sdkVersion | Configuration: $Configuration | Runtime: $Runtime"
Write-Host "Restoring solution..."
& dotnet restore $Solution
if ($LASTEXITCODE -ne 0) { throw "dotnet restore failed with exit code $LASTEXITCODE." }

Write-Host "Running core self-tests..."
& dotnet run --project $TestsProject --configuration $Configuration --no-restore
if ($LASTEXITCODE -ne 0) { throw "Core self-tests failed with exit code $LASTEXITCODE." }

if (Test-Path $ReleaseRoot) { Remove-Item $ReleaseRoot -Recurse -Force }
$null = New-Item -ItemType Directory -Force -Path $ReleaseRoot

$projects = @(
    @{ Name = "NENAPIYUMA-Chat"; Project = (Join-Path $SuiteRoot "src/NENAPIYUMA.Chat/NENAPIYUMA.Chat.csproj"); Executable = "NENAPIYUMA.Chat.exe" },
    @{ Name = "NENAPIYUMA-Studio"; Project = (Join-Path $SuiteRoot "src/NENAPIYUMA.Studio/NENAPIYUMA.Studio.csproj"); Executable = "NENAPIYUMA.Studio.exe" }
)

foreach ($item in $projects) {
    $destination = Join-Path $ReleaseRoot $item.Name
    $null = New-Item -ItemType Directory -Force -Path $destination
    Write-Host "Publishing $($item.Name)..."
    $publishArgs = @(
        "publish", $item.Project,
        "--configuration", $Configuration,
        "--runtime", $Runtime,
        "--self-contained", "true",
        "--output", $destination,
        "-p:PublishSingleFile=true",
        "-p:IncludeNativeLibrariesForSelfExtract=true",
        "-p:EnableCompressionInSingleFile=true"
    )
    & dotnet @publishArgs
    if ($LASTEXITCODE -ne 0) { throw "Publishing $($item.Name) failed with exit code $LASTEXITCODE." }

    $expectedExe = Join-Path $destination $item.Executable
    if (-not (Test-Path $expectedExe -PathType Leaf)) { throw "Expected executable is missing: $expectedExe" }
    if ((Get-Item $expectedExe).Length -le 0) { throw "Expected executable is empty: $expectedExe" }

    foreach ($folder in @("models", "tools", "config")) {
        $null = New-Item -ItemType Directory -Force -Path (Join-Path $destination $folder)
    }
    Copy-Item (Join-Path $SuiteRoot "README_SI.md") (Join-Path $destination "README_SI.md") -Force
    Copy-Item (Join-Path $SuiteRoot "models/README.md") (Join-Path $destination "models/README.md") -Force
    Copy-Item (Join-Path $SuiteRoot "tools/README.md") (Join-Path $destination "tools/README.md") -Force
    Copy-Item (Join-Path $SuiteRoot "config/appsettings.json") (Join-Path $destination "config/appsettings.json") -Force
    $null = New-Item -ItemType Directory -Force -Path (Join-Path $destination "data")
    Write-Host "[OK] $expectedExe ($((Get-Item $expectedExe).Length) bytes)"
}

Write-Host ""
Write-Host "Build passed. Output folders:"
Write-Host " - $(Join-Path $ReleaseRoot 'NENAPIYUMA-Chat')"
Write-Host " - $(Join-Path $ReleaseRoot 'NENAPIYUMA-Studio')"
Write-Host "No GGUF model or llama.cpp runtime is downloaded by this script. Add them explicitly or use the workflow's optional offline-bundle input."
