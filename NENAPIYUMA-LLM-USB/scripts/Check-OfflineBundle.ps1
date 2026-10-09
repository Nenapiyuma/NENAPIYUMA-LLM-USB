$Root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$checks = @(
  @{ Name = "App source"; Path = (Join-Path $Root "app\main.py") },
  @{ Name = "Local runtime"; Path = (Join-Path $Root "tools\llama-server.exe") }
)
foreach ($c in $checks) {
  if (Test-Path $c.Path) { Write-Host "[OK] $($c.Name)" -ForegroundColor Green }
  else { Write-Host "[MISSING] $($c.Name): $($c.Path)" -ForegroundColor Yellow }
}
$models = Get-ChildItem (Join-Path $Root "models") -Filter "*.gguf" -ErrorAction SilentlyContinue
if ($models) { $models | ForEach-Object { Write-Host "[OK] Model: $($_.Name) ($([math]::Round($_.Length/1GB,2)) GB)" -ForegroundColor Green } }
else { Write-Host "[MISSING] GGUF model in models\" -ForegroundColor Yellow }
Write-Host "Run this check again after preparing the offline bundle."
