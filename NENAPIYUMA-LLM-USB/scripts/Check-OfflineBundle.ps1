$ErrorActionPreference = "Continue"
$Root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$failed = $false

function Check-File([string]$Name, [string]$Path) {
  if (Test-Path $Path -PathType Leaf) {
    $item = Get-Item $Path
    if ($item.Length -gt 0) { Write-Host "[OK] $Name ($($item.Length) bytes)" -ForegroundColor Green }
    else { Write-Host "[FAIL] $Name is empty: $Path" -ForegroundColor Red; $script:failed = $true }
  } else {
    Write-Host "[MISSING] $Name : $Path" -ForegroundColor Yellow
  }
}

Check-File "Standalone app (optional for source checkout)" (Join-Path $Root "NENAPIYUMA.exe")
Check-File "App source" (Join-Path $Root "app\main.py")
Check-File "Local llama.cpp server" (Join-Path $Root "tools\llama-server.exe")

$models = @(Get-ChildItem (Join-Path $Root "models") -Filter "*.gguf" -File -ErrorAction SilentlyContinue)
if ($models.Count -gt 0) {
  foreach ($model in $models) {
    if ($model.Length -ge 10000000) {
      Write-Host "[OK] Model: $($model.Name) ($([math]::Round($model.Length / 1GB, 2)) GB)" -ForegroundColor Green
    } else {
      Write-Host "[FAIL] Model file looks incomplete: $($model.Name)" -ForegroundColor Red
      $failed = $true
    }
  }
} else {
  Write-Host "[MISSING] GGUF model in models\" -ForegroundColor Yellow
}

if ((Test-Path (Join-Path $Root "NENAPIYUMA.exe")) -and
    (Test-Path (Join-Path $Root "tools\llama-server.exe")) -and
    $models.Count -gt 0 -and -not $failed) {
  Write-Host "Bundle check passed. Test offline on a Windows PC before relying on it." -ForegroundColor Green
  exit 0
}
Write-Host "Bundle is incomplete. Run Prepare-OfflineBundle.ps1 on an internet-connected Windows PC or download the full USB bundle artifact." -ForegroundColor Yellow
exit 1
