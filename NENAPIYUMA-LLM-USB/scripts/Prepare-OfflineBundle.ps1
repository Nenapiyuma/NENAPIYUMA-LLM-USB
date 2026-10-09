param(
  [ValidateSet("RAM4GB","RAM8GB")]
  [string]$Profile = "RAM4GB"
)
$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$Tools = Join-Path $Root "tools"
$Models = Join-Path $Root "models"
New-Item -ItemType Directory -Force -Path $Tools,$Models | Out-Null

Write-Host "NENAPIYUMA offline bundle preparation"
Write-Host "Selected profile: $Profile"
Write-Host "This step needs internet. Once complete, normal app use is offline."

# Find the latest llama.cpp Windows CPU x64 release asset via GitHub API.
$headers = @{ "User-Agent" = "NENAPIYUMA-Portable-Setup" }
$release = Invoke-RestMethod -Uri "https://api.github.com/repos/ggml-org/llama.cpp/releases/latest" -Headers $headers
$asset = $release.assets | Where-Object { $_.name -match "bin-win-cpu-x64\.zip$" } | Select-Object -First 1
if (-not $asset) {
  $asset = $release.assets | Where-Object { $_.name -match "win.*cpu.*x64.*\.zip$" } | Select-Object -First 1
}
if (-not $asset) {
  throw "Could not find a Windows CPU x64 ZIP in latest llama.cpp release. Download a matching Windows CPU build manually from https://github.com/ggml-org/llama.cpp/releases and place llama-server.exe plus required DLL files in tools\."
}
$runtimeZip = Join-Path $env:TEMP "nenapiyuma-llama-runtime.zip"
$extract = Join-Path $env:TEMP "nenapiyuma-llama-runtime"
Write-Host "Downloading runtime: $($asset.name)"
Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $runtimeZip
if (Test-Path $extract) { Remove-Item $extract -Recurse -Force }
Expand-Archive -Path $runtimeZip -DestinationPath $extract -Force
$server = Get-ChildItem $extract -Filter "llama-server.exe" -Recurse | Select-Object -First 1
if (-not $server) { throw "llama-server.exe not found in downloaded archive." }
$binDir = $server.Directory.FullName
Copy-Item (Join-Path $binDir "*") $Tools -Recurse -Force
Write-Host "Runtime copied to tools\"

if ($Profile -eq "RAM4GB") {
  $repo = "bartowski/Qwen2.5-0.5B-Instruct-GGUF"
  $filename = "Qwen2.5-0.5B-Instruct-Q4_K_M.gguf"
} else {
  $repo = "bartowski/Qwen2.5-1.5B-Instruct-GGUF"
  $filename = "Qwen2.5-1.5B-Instruct-Q4_K_M.gguf"
}
$modelPath = Join-Path $Models $filename
$modelUrl = "https://huggingface.co/$repo/resolve/main/$filename"
if (-not (Test-Path $modelPath)) {
  Write-Host "Downloading model file: $filename"
  Write-Host "Model file may take several minutes and needs free USB/disk space."
  Invoke-WebRequest -Uri $modelUrl -OutFile $modelPath
}
if ((Get-Item $modelPath).Length -lt 10000000) {
  Remove-Item $modelPath -Force
  throw "Downloaded model appears too small; download failed or URL changed. Check the model repository manually."
}
Write-Host ""
Write-Host "Preparation complete. Keep the entire folder together and copy it to the USB drive."
Write-Host "For offline use, disconnect internet and run START_NENAPIYUMA.bat."
