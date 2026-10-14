$ErrorActionPreference = "Stop"
$SuiteRoot = Split-Path -Parent $PSScriptRoot
$ReleaseRoot = Join-Path $SuiteRoot "release"

$expected = @(
    (Join-Path $ReleaseRoot "NENAPIYUMA-Chat/NENAPIYUMA.Chat.exe"),
    (Join-Path $ReleaseRoot "NENAPIYUMA-Studio/NENAPIYUMA.Studio.exe")
)
foreach ($file in $expected) {
    if (-not (Test-Path $file -PathType Leaf)) { throw "Missing build output: $file" }
    if ((Get-Item $file).Length -le 0) { throw "Empty build output: $file" }
    Write-Host "[OK] $file ($((Get-Item $file).Length) bytes)"
}
$bytes = (Get-ChildItem $ReleaseRoot -Recurse -File | Measure-Object -Property Length -Sum).Sum
Write-Host ("Total release bytes: {0:N0} ({1:N2} GiB)" -f $bytes, ($bytes / 1GB))
